#!/usr/bin/env -S dotnet fsi
// Seed the Indus codebook database — single source of truth for all
// sign-to-field mappings, weight tiers, commodities, routes, merchants.
// Conforms to: spec/fsharp/reference/fsharp_coding_standard.tex
//
// Usage:
//   dotnet fsi indus_seed_codebook.fsx <output-db>
//   dotnet fsi indus_seed_codebook.fsx indus_codebook.db
//
// The DB this creates is read by indus_decoder.fsx and indus_lssc.fsx.
// To change the codebook, edit the seed data below and re-run.

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open System.IO
open Microsoft.Data.Sqlite

// === ARGUMENT PARSING (S1) ===================================================

let args = fsi.CommandLineArgs |> Array.tail

let dbPath =
    match args with
    | [| db |] -> db
    | _ ->
        eprintfn "Usage: dotnet fsi indus_seed_codebook.fsx <output-db>"
        exit 1

// === SCHEMA (S4: schema as string literal) ===================================

let schema = """
CREATE TABLE weight_tier (
    multiplier    INTEGER PRIMARY KEY,
    grams         REAL    NOT NULL,
    series        TEXT    NOT NULL CHECK(series IN ('binary','decimal')),
    application   TEXT
);

CREATE TABLE commodity (
    code          TEXT    PRIMARY KEY,
    label         TEXT    NOT NULL,
    description   TEXT,
    corpus_note   TEXT
);

CREATE TABLE route (
    code          TEXT    PRIMARY KEY,
    label         TEXT    NOT NULL,
    destination   TEXT
);

CREATE TABLE quantity_code (
    sign_id       INTEGER PRIMARY KEY,
    multiplier    INTEGER NOT NULL
);

CREATE TABLE merchant_mark (
    code          TEXT    PRIMARY KEY,
    label         TEXT    NOT NULL,
    note          TEXT
);

CREATE TABLE sign_role (
    sign_id       INTEGER NOT NULL,
    role          TEXT    NOT NULL CHECK(role IN (
                      'commodity','weight','quantity','terminal','structural')),
    ref_code      TEXT,
    ref_multiplier INTEGER,
    PRIMARY KEY (sign_id)
);

CREATE TABLE positional_rule (
    position      TEXT    PRIMARY KEY CHECK(position IN ('first','last','other')),
    default_role  TEXT    NOT NULL
)
"""

// === SEED DATA ===============================================================

let baseUnit = 0.856

let weightTiers = [|
    // Binary series — precious/indivisible goods
    (1,    baseUnit,         "binary",  "Gold dust, gem weights")
    (2,    baseUnit * 2.0,   "binary",  "")
    (4,    baseUnit * 4.0,   "binary",  "Silver, carnelian")
    (8,    baseUnit * 8.0,   "binary",  "")
    (16,   baseUnit * 16.0,  "binary",  "Copper ingot reference unit")
    (32,   baseUnit * 32.0,  "binary",  "")
    (64,   baseUnit * 64.0,  "binary",  "")
    // Decimal series — bulk goods
    (160,  137.0,            "decimal", "Cotton bale tier 1")
    (200,  171.2,            "decimal", "")
    (500,  685.0,            "decimal", "Sesame oil jar class")
    (1000, 1370.0,           "decimal", "Bulk grain")
|]

let commodities = [|
    ("jar",       "JAR GOODS",      "sesame oil/grain/resin",        "S-342 ~10% corpus")
    ("iron",      "IRON GOODS",     "Tamil Nadu supply corridor",    "fish sign")
    ("carnelian", "CARNELIAN",      "agate/carnelian beads",         "Gujarat source")
    ("copper",    "COPPER-BRONZE",  "ingots/tools",                  "Khetri mines")
    ("textile",   "TEXTILES",       "cotton bales",                  "Indus bulk export")
    ("timber",    "TIMBER",         "structural logs",               "Akkadian records")
    ("ivory",     "IVORY/SHELL",    "ornaments",                     "Akkadian records")
    ("gold",      "GOLD",           "jewellery",                     "south India source")
|]

let routes = [|
    ("mesopotamia",    "MESOPOTAMIA",  "Ur / Kish / Tell Asmar")
    ("dilmun",         "DILMUN",       "Bahrain entrepot")
    ("magan",          "MAGAN",        "Oman copper return")
    ("internal_north", "NORTH",        "Harappa / Mohenjo-daro")
    ("internal_south", "SOUTH",        "Tamil Nadu supply")
|]

let quantityCodes = [|
    (10, 1); (11, 2); (12, 5); (13, 10); (14, 20); (15, 50)
|]

let merchantMarks = [|
    ("unicorn",    "UNICORN GUILD",    "dominant trading house, most common motif")
    ("bull",       "BULL GUILD",       "")
    ("elephant",   "ELEPHANT GUILD",   "")
    ("tiger",      "TIGER GUILD",      "")
    ("rhinoceros", "RHINOCEROS GUILD", "")
|]

let signRoles = [|
    // Commodity signs
    (342, "commodity",  Some "jar",       None)
    (218, "commodity",  Some "iron",      None)
    (176, "commodity",  Some "carnelian", None)
    (184, "commodity",  Some "copper",    None)
    (301, "commodity",  Some "textile",   None)
    (200, "commodity",  Some "timber",    None)
    (211, "commodity",  Some "ivory",     None)
    (89,  "commodity",  Some "gold",      None)
    // Weight tier signs (sign_id -> binary multiplier)
    (1,   "weight",     None, Some 1)
    (2,   "weight",     None, Some 2)
    (3,   "weight",     None, Some 4)
    (4,   "weight",     None, Some 8)
    (5,   "weight",     None, Some 16)
    (6,   "weight",     None, Some 32)
    (7,   "weight",     None, Some 64)
    // Quantity signs
    (10,  "quantity",   None, Some 1)
    (11,  "quantity",   None, Some 2)
    (12,  "quantity",   None, Some 5)
    (13,  "quantity",   None, Some 10)
    (14,  "quantity",   None, Some 20)
    (15,  "quantity",   None, Some 50)
    // Terminal / route signs
    (59,  "terminal",   Some "mesopotamia",    None)
    (60,  "terminal",   Some "dilmun",         None)
    (61,  "terminal",   Some "magan",          None)
    (62,  "terminal",   Some "internal_north", None)
    (63,  "terminal",   Some "internal_south", None)
    // Structural (no field content, positional glue)
    (99,  "structural", None, None)
    (267, "structural", None, None)
|]

let positionalRules = [|
    ("first", "commodity")
    ("last",  "terminal")
    ("other", "structural")
|]

// === WRITE (C2: imperative for terminal side effects) ========================

if File.Exists dbPath then File.Delete dbPath
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath dbPath)) |> ignore

use con = new SqliteConnection(sprintf "Data Source=%s" dbPath)
con.Open()

let exec (sql: string) =
    use cmd = new SqliteCommand(sql, con)
    cmd.ExecuteNonQuery() |> ignore

schema.Split(';', StringSplitOptions.RemoveEmptyEntries)
|> Array.iter (fun s -> exec (s.Trim()))

let tx = con.BeginTransaction()

for (m, g, series, app) in weightTiers do
    use cmd = new SqliteCommand(
        "INSERT INTO weight_tier VALUES (@m,@g,@s,@a)", con, tx)
    cmd.Parameters.AddWithValue("@m", m) |> ignore
    cmd.Parameters.AddWithValue("@g", g) |> ignore
    cmd.Parameters.AddWithValue("@s", series) |> ignore
    cmd.Parameters.AddWithValue("@a", app) |> ignore
    cmd.ExecuteNonQuery() |> ignore

for (code, label, desc, note) in commodities do
    use cmd = new SqliteCommand(
        "INSERT INTO commodity VALUES (@c,@l,@d,@n)", con, tx)
    cmd.Parameters.AddWithValue("@c", code) |> ignore
    cmd.Parameters.AddWithValue("@l", label) |> ignore
    cmd.Parameters.AddWithValue("@d", desc) |> ignore
    cmd.Parameters.AddWithValue("@n", note) |> ignore
    cmd.ExecuteNonQuery() |> ignore

for (code, label, dest) in routes do
    use cmd = new SqliteCommand(
        "INSERT INTO route VALUES (@c,@l,@d)", con, tx)
    cmd.Parameters.AddWithValue("@c", code) |> ignore
    cmd.Parameters.AddWithValue("@l", label) |> ignore
    cmd.Parameters.AddWithValue("@d", dest) |> ignore
    cmd.ExecuteNonQuery() |> ignore

for (signId, mult) in quantityCodes do
    use cmd = new SqliteCommand(
        "INSERT INTO quantity_code VALUES (@s,@m)", con, tx)
    cmd.Parameters.AddWithValue("@s", signId) |> ignore
    cmd.Parameters.AddWithValue("@m", mult) |> ignore
    cmd.ExecuteNonQuery() |> ignore

for (code, label, note) in merchantMarks do
    use cmd = new SqliteCommand(
        "INSERT INTO merchant_mark VALUES (@c,@l,@n)", con, tx)
    cmd.Parameters.AddWithValue("@c", code) |> ignore
    cmd.Parameters.AddWithValue("@l", label) |> ignore
    cmd.Parameters.AddWithValue("@n", note) |> ignore
    cmd.ExecuteNonQuery() |> ignore

for (signId, role, refCode, refMult) in signRoles do
    use cmd = new SqliteCommand(
        "INSERT INTO sign_role VALUES (@s,@r,@rc,@rm)", con, tx)
    cmd.Parameters.AddWithValue("@s", signId) |> ignore
    cmd.Parameters.AddWithValue("@r", role) |> ignore
    cmd.Parameters.AddWithValue("@rc", refCode  |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("@rm", refMult  |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.ExecuteNonQuery() |> ignore

for (pos, defRole) in positionalRules do
    use cmd = new SqliteCommand(
        "INSERT INTO positional_rule VALUES (@p,@d)", con, tx)
    cmd.Parameters.AddWithValue("@p", pos) |> ignore
    cmd.Parameters.AddWithValue("@d", defRole) |> ignore
    cmd.ExecuteNonQuery() |> ignore

tx.Commit()
con.Close()

// === VERIFY ==================================================================

use con2 = new SqliteConnection(sprintf "Data Source=%s" dbPath)
con2.Open()

let count (table: string) =
    use cmd = new SqliteCommand(sprintf "SELECT COUNT(*) FROM %s" table, con2)
    cmd.ExecuteScalar() :?> int64

printfn "INDUS CODEBOOK SEEDED: %s" dbPath
printfn "  weight_tier     : %d rows" (count "weight_tier")
printfn "  commodity       : %d rows" (count "commodity")
printfn "  route           : %d rows" (count "route")
printfn "  quantity_code   : %d rows" (count "quantity_code")
printfn "  merchant_mark   : %d rows" (count "merchant_mark")
printfn "  sign_role       : %d rows" (count "sign_role")
printfn "  positional_rule : %d rows" (count "positional_rule")

con2.Close()
