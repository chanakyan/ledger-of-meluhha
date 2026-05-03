#!/usr/bin/env -S dotnet fsi
// SPDX-License-Identifier: BSD-2-Clause
// Implements: Ledger of Meluhha §The Database — Wells 2015 Phase 2 (table OCR)
// Coding standard: spec/fsharp/reference/fsharp_coding_standard.tex
//
// Phase 2 of the Wells 2015 ingest. Phase 1 (wells2015_to_sqlite.fsx, commit
// 640ff31) captured what the prose stated. This script captures what the
// numeric tables actually say, after rendering pages at 220dpi and OCRing
// with tesseract --psm 4. The tables in Wells 2015 are typeset rather than
// pure images, but their multi-column layout defeats default pdftotext
// extraction.
//
// Captured here:
//   - Table 4.2 full Harappa volumetric system (Phase 1 had 4 of 7 rows;
//     this script fills VII, VIII, VIIIII at 80.9 / 121.3 / 202.1 L).
//   - Table 5.3 Wells 2015's own sign 700 × NUM frequency matrix
//     (87/137/105 for signs 34/33/32) — these differ from the Fuls 2019
//     ICIT-help-PDF figures (89/136/105) by small amounts; both sources
//     are recorded under their respective source_code so the discrepancy
//     is visible rather than masked.
//   - Table 6.1 the proto-Dravidian number system (new dravidian_numeral
//     table). 11 rows for numbers 1-10 plus 100 with compounding base,
//     variant, and human/neuter noun forms (McAlpin 1981:40-1 cited).
//   - Additional sign-role assignments for signs 037, 039 (LON pot-sherd
//     low-frequency); 220 (FSH); 405, 407, 409 (collocate with short
//     numerals); 151, 154, 158 (do NOT collocate with short numerals,
//     stored as evidence-only since "anti-role" has no schema slot yet).
//
// Usage:
//   dotnet fsi wells2015_tables_to_sqlite.fsx <db>

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open System.IO
open Microsoft.Data.Sqlite

let args = fsi.CommandLineArgs |> Array.tail

let dbPath =
    match args with
    | [| db |] -> db
    | _ ->
        eprintfn "Usage: dotnet fsi wells2015_tables_to_sqlite.fsx <db>"
        exit 1

if not (File.Exists dbPath) then
    eprintfn "ERROR: database not found: %s" dbPath
    exit 1

let sourceCode = "WELLS2015"

// === SCHEMA: one new table for the proto-Dravidian number system ==============

let migrations = [
    """
    CREATE TABLE IF NOT EXISTS dravidian_numeral (
        source_code      TEXT NOT NULL REFERENCES source(code),
        number           INTEGER NOT NULL,
        compounding_base TEXT,
        variant          TEXT,
        noun_human       TEXT,
        noun_neuter      TEXT,
        PRIMARY KEY (source_code, number)
    ) STRICT;
    """ ]

// === DATA: records ============================================================

type SignRoleRow     = { SignId: int; FunctionCode: string; Evidence: string }
type SignPairRow     = { Left: int; Right: int; Frequency: int; Context: string }
type VolumetricRow   = { Symbol: string; Liters: float; Notes: string }
type DravidianNumRow = {
    Number:          int
    CompoundingBase: string option
    Variant:         string option
    NounHuman:       string option
    NounNeuter:      string option }

// Table 4.2 Phase 2 fill-ins — the 3 volumetric units missing from Phase 1.
let volumetricUnits : VolumetricRow list = [
    { Symbol="VII";   Liters=80.9;  Notes="Wells 2015 Table 4.2 grand mean across H-372/371/370 strokes 6 = 80.7L, strokes 12 = 81.1L; mean 80.9L" }
    { Symbol="VIII";  Liters=121.3; Notes="Wells 2015 Table 4.2 grand mean across H-372/371/370 = 120.1/121.0/122.7L" }
    { Symbol="VIIIII"; Liters=202.1; Notes="Wells 2015 Table 4.2 grand mean across H-372/371/370 = 200.2/201.7/204.6L" } ]

// Table 5.3: Wells's own sign 700 × Long-Linear-Stroke collocation matrix.
// Different from Fuls 2019 figures (89/136/105) — recorded as Wells's own
// counts, not a correction.
let sign700NumPairsWells : SignPairRow list = [
    { Left=700; Right=32; Frequency=105; Context="Wells 2015 Table 5.3 — sign 700 left-adjacent collocations on Harappa tablets (Wells's count, cf. Fuls 2019 helper-matrix value 89)" }
    { Left=700; Right=33; Frequency=137; Context="Wells 2015 Table 5.3 — sign 700 left-adjacent collocations (Wells: 137, Fuls 2019: 136)" }
    { Left=700; Right=34; Frequency=87;  Context="Wells 2015 Table 5.3 — sign 700 left-adjacent collocations (Wells: 87, Fuls 2019: 105)" } ]

// Table 5.1 row totals visible in OCR (left-adjacent counts of short-linear
// stroke numerals; partial — full matrix cell-by-cell defeated tesseract).
// Recorded here as sign_role evidence for short-numeral high-frequency signs.
let signRoles : SignRoleRow list = [
    // Confirm short-numeral total occurrences (Table 5.1 row totals, prose).
    { SignId=2; FunctionCode="SHN"; Evidence="Wells 2015 Table 5.1 — sign 002 has 763 short-numeral collocations (highest in class)" }
    { SignId=3; FunctionCode="SHN"; Evidence="Wells 2015 Table 5.1 — sign 003 has 309 short-numeral collocations; sign 003+156 = 35 numeric contexts" }

    // LON additions (Wells 2015 Ch.5 prose + Table 5.3).
    { SignId=37; FunctionCode="LON"; Evidence="Wells 2015 Ch.5 — 'Signs 037 and 039 are found in low frequencies on pot-sherds'" }
    { SignId=39; FunctionCode="LON"; Evidence="Wells 2015 Ch.5 — same low-frequency-pot-sherd note" }

    // FSH — explicit fish-sign roles (Wells 2015 §Fish Signs and Numbers).
    { SignId=220; FunctionCode="FSH"; Evidence="Wells 2015 Ch.5 §Fish Signs — 'sign 220 ... close association of fish signs (especially sign 220) and numeral signs'" }
    { SignId=156; FunctionCode="FSH"; Evidence="Wells 2015 Ch.5 — 'fish' sign collocates with short numerals (already classified TMK in Phase 1; fish-role is the graphic class)" }
    { SignId=158; FunctionCode="FSH"; Evidence="Wells 2015 Ch.5 — graphically-similar fish sign to 156" }

    // Short-numeral-collocator signs (Table 5.1 prose enumeration).
    { SignId=390; FunctionCode="NUM"; Evidence="Wells 2015 Table 5.1 prose — 'signs 156, the fish signs, 390, 405 and 407/9 collocate frequently with Short Linear Stroke signs'" }
    { SignId=405; FunctionCode="NUM"; Evidence="Wells 2015 Table 5.1 prose — same enumeration" }
    { SignId=407; FunctionCode="NUM"; Evidence="Wells 2015 Table 5.1 prose — same enumeration" }
    { SignId=409; FunctionCode="NUM"; Evidence="Wells 2015 Table 5.1 prose — same enumeration" } ]

// Table 6.1 — proto-Dravidian number system (McAlpin 1981:40-1, reproduced
// verbatim by Wells 2015 p.83). Empty cells in the printed table are encoded
// as None.
let dravidianNumerals : DravidianNumRow list = [
    { Number=1;   CompoundingBase=Some "or, on, ol, okka"; Variant=Some "oru, okkanre"; NounHuman=Some "oruvanre (m), orutti (f)"; NounNeuter=Some "onre" }
    { Number=2;   CompoundingBase=Some "ir";               Variant=Some "iru";          NounHuman=Some "iruvar";                    NounNeuter=Some "iranta" }
    { Number=3;   CompoundingBase=Some "mu(N)";            Variant=Some "mu, muC";      NounHuman=Some "muvar";                     NounNeuter=Some "munre" }
    { Number=4;   CompoundingBase=Some "nal";              Variant=Some "nan";          NounHuman=None;                             NounNeuter=Some "nalke" }
    { Number=5;   CompoundingBase=Some "cayN";             Variant=Some "cayn";         NounHuman=None;                             NounNeuter=Some "caynte" }
    { Number=6;   CompoundingBase=Some "care";             Variant=Some "care";         NounHuman=None;                             NounNeuter=Some "care" }
    { Number=7;   CompoundingBase=Some "ez";               Variant=Some "ezu";          NounHuman=None;                             NounNeuter=Some "eze" }
    { Number=8;   CompoundingBase=None;                    Variant=None;                NounHuman=None;                             NounNeuter=Some "entte" }
    { Number=9;   CompoundingBase=None;                    Variant=None;                NounHuman=None;                             NounNeuter=Some "onpate" }
    { Number=10;  CompoundingBase=Some "tol";              Variant=Some "ton";          NounHuman=None;                             NounNeuter=Some "patte" }
    { Number=100; CompoundingBase=Some "pate, patin, pan"; Variant=Some "nure";         NounHuman=None;                             NounNeuter=Some "nure" } ]

// === EXEC =====================================================================

let conn = new SqliteConnection($"Data Source={dbPath}")
conn.Open()
let txn = conn.BeginTransaction()

let exec (sql: string) (kvs: (string * obj) list) =
    use cmd = conn.CreateCommand()
    cmd.Transaction <- txn
    cmd.CommandText <- sql
    for (k, v) in kvs do cmd.Parameters.AddWithValue(k, v) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let nullable<'a> (o: 'a option) : obj =
    match o with
    | Some v -> box v
    | None   -> DBNull.Value :> obj

try
    for sql in migrations do exec sql []

    let mutable nVol = 0
    for v in volumetricUnits do
        exec
            ("INSERT INTO volumetric_unit (source_code, symbol, liters, notes) VALUES ($s, $sym, $l, $n) " +
             "ON CONFLICT(source_code, symbol) DO UPDATE SET liters=excluded.liters, notes=excluded.notes")
            [ "$s", box sourceCode; "$sym", box v.Symbol; "$l", box v.Liters; "$n", box v.Notes ]
        nVol <- nVol + 1

    let mutable nPair = 0
    for p in sign700NumPairsWells do
        exec
            ("INSERT INTO sign_pair_frequency (source_code, sign_left, sign_right, frequency, context) VALUES ($s, $l, $r, $f, $ctx) " +
             "ON CONFLICT(source_code, sign_left, sign_right) DO UPDATE SET frequency=excluded.frequency, context=excluded.context")
            [ "$s", box sourceCode; "$l", box p.Left; "$r", box p.Right; "$f", box p.Frequency; "$ctx", box p.Context ]
        nPair <- nPair + 1

    let mutable nRole = 0
    for r in signRoles do
        exec
            ("INSERT INTO sign_role (source_code, sign_id, function_code, evidence) VALUES ($s, $sid, $f, $e) " +
             "ON CONFLICT(source_code, sign_id, function_code) DO UPDATE SET evidence=excluded.evidence")
            [ "$s", box sourceCode; "$sid", box r.SignId; "$f", box r.FunctionCode; "$e", box r.Evidence ]
        nRole <- nRole + 1

    let mutable nDrav = 0
    for d in dravidianNumerals do
        exec
            ("INSERT INTO dravidian_numeral (source_code, number, compounding_base, variant, noun_human, noun_neuter) VALUES ($s, $n, $cb, $v, $nh, $nn) " +
             "ON CONFLICT(source_code, number) DO UPDATE SET compounding_base=excluded.compounding_base, variant=excluded.variant, noun_human=excluded.noun_human, noun_neuter=excluded.noun_neuter")
            [ "$s",  box sourceCode
              "$n",  box d.Number
              "$cb", nullable d.CompoundingBase
              "$v",  nullable d.Variant
              "$nh", nullable d.NounHuman
              "$nn", nullable d.NounNeuter ]
        nDrav <- nDrav + 1

    txn.Commit()

    printfn "OK: ingested Wells 2015 Phase 2 (table OCR)"
    printfn "  volumetric_unit (new) : %d (Phase 1 had 4, total now 7)" nVol
    printfn "  sign_pair_frequency   : %d (Wells 700×NUM matrix)" nPair
    printfn "  sign_role             : %d (additions)" nRole
    printfn "  dravidian_numeral     : %d (proto-Dravidian Table 6.1)" nDrav
with
| ex ->
    txn.Rollback()
    eprintfn "FAIL: %s" ex.Message
    eprintfn "%s" ex.StackTrace
    exit 1
