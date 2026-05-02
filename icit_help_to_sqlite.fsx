#!/usr/bin/env -S dotnet fsi
// SPDX-License-Identifier: BSD-2-Clause
// Ingest the structured data extractable from
// https://www.user.tu-berlin.de/fuls/Homepage/indus/help_onlinedatabase.pdf
// (Fuls, Documentation of the Online Indus Writing Database, 21 pages, Sep 2019).
//
// This is the only freely-accessible Wells-numbered data source for ICIT
// content. The actual ICIT online database at caddy.igg.tu-berlin.de is
// unreachable as of 2026-05-02 (DNS resolves, host does not respond). The
// help PDF gives ten fully-spelled-out ICIT inscriptions, the canonical
// 16-row sign-function vocabulary, the 4-row reading-direction vocabulary,
// six published TMK signs with their adjacency matrix, the NUM context
// signature of sign 700, and four documented paradigmatic sign clusters.
//
// Captured before access closes further. Source code: ICIT_HELP_2019.
//
// Usage:
//   dotnet fsi icit_help_to_sqlite.fsx <output-db>

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open System.IO
open Microsoft.Data.Sqlite

let args = fsi.CommandLineArgs |> Array.tail

let dbPath =
    match args with
    | [| db |] -> db
    | _ ->
        eprintfn "Usage: dotnet fsi icit_help_to_sqlite.fsx <db>"
        exit 1

if not (File.Exists dbPath) then
    eprintfn "ERROR: database not found: %s" dbPath
    exit 1

let sourceCode = "ICIT_HELP_2019"

// === SCHEMA: 7 new tables (CREATE IF NOT EXISTS) =============================

let migrations = [
    """
    CREATE TABLE IF NOT EXISTS sign_function (
        source_code   TEXT NOT NULL REFERENCES source(code),
        function_code TEXT NOT NULL,
        label         TEXT NOT NULL,
        PRIMARY KEY (source_code, function_code)
    ) STRICT;
    """
    """
    CREATE TABLE IF NOT EXISTS reading_direction_code (
        source_code TEXT NOT NULL REFERENCES source(code),
        code        TEXT NOT NULL,
        label       TEXT NOT NULL,
        PRIMARY KEY (source_code, code)
    ) STRICT;
    """
    """
    CREATE TABLE IF NOT EXISTS sign_role (
        source_code   TEXT NOT NULL REFERENCES source(code),
        sign_id       INTEGER NOT NULL,
        function_code TEXT NOT NULL,
        evidence      TEXT,
        PRIMARY KEY (source_code, sign_id, function_code)
    ) STRICT;
    """
    """
    CREATE TABLE IF NOT EXISTS icit_inscription (
        icit_id        INTEGER NOT NULL,
        source_code    TEXT NOT NULL REFERENCES source(code),
        site           TEXT,
        artefact_type  TEXT,
        text_class     TEXT,
        excavation_idno TEXT,
        text_code      TEXT NOT NULL,
        PRIMARY KEY (source_code, icit_id)
    ) STRICT;
    """
    """
    CREATE TABLE IF NOT EXISTS sign_cluster (
        source_code   TEXT NOT NULL REFERENCES source(code),
        signs         TEXT NOT NULL,
        frequency     INTEGER NOT NULL,
        contexts      TEXT,
        evidence      TEXT,
        PRIMARY KEY (source_code, signs)
    ) STRICT;
    """
    """
    CREATE TABLE IF NOT EXISTS sign_pair_frequency (
        source_code TEXT NOT NULL REFERENCES source(code),
        sign_left   INTEGER NOT NULL,
        sign_right  INTEGER NOT NULL,
        frequency   INTEGER NOT NULL,
        context     TEXT,
        PRIMARY KEY (source_code, sign_left, sign_right)
    ) STRICT;
    """
    """
    CREATE TABLE IF NOT EXISTS bibliography_reference (
        citation_key  TEXT NOT NULL,
        source_code   TEXT NOT NULL REFERENCES source(code),
        author        TEXT NOT NULL,
        year          INTEGER,
        title         TEXT NOT NULL,
        venue         TEXT,
        url           TEXT,
        PRIMARY KEY (source_code, citation_key)
    ) STRICT;
    """ ]

// === DATA ====================================================================

let sourceRow =
    {| Code = sourceCode
       Name = "Fuls 2019 — Documentation of the Online Indus Writing Database (ICIT help PDF)"
       Year = 2019
       Note = "21-page PDF, ModDate 2019-09-04, hosted at https://www.user.tu-berlin.de/fuls/Homepage/indus/help_onlinedatabase.pdf. Only freely-accessible source for Wells-numbered ICIT content as of 2026-05-02 (caddy.igg.tu-berlin.de is unreachable; epigraphica.de subscription requires functioning links the user has not received)." |}

// Page 2-3: 16 sign-function codes
let signFunctions = [
    "ITM", "Initial Cluster Terminal Marker"
    "LOG", "Logogram"
    "LON", "Long Numeral"
    "FSH", "Fish sign"
    "NUM", "Numeral"
    "PTM", "Post Terminal Marker"
    "S17", "Set 17"
    "S28", "Set 28"
    "S30", "Set 30"
    "S36", "Set 36"
    "SHN", "Short Numeral"
    "SSN", "Short Stacked Numeral"
    "SPN", "Special Numeral"
    "SYL", "Syllable"
    "TMK", "Terminal Marker"
    "XXX", "Test function" ]

// Page 6-7: 4 reading-direction codes
let readingDirCodes = [
    "R/L", "Right to left (default; 87% of texts)"
    "L/R", "Left to right"
    "T/B", "Top to bottom"
    "BUS", "Boustrophedon" ]

// Page 7-8: text encoding rules — captured in the inscription text_code field
// directly. Page 8 example: 8 ICIT inscriptions, all Mohenjo-daro:
let icitInscriptions = [
    1202, "Mohenjo-daro", "TAB:B",  None,        "E232 CXVI:6",  "+520-033-705-803-853+"
    1254, "Mohenjo-daro", "SEAL:S", None,        "DK1104",       "+740-760-033-705-803-002-920-317+"
    815,  "Mohenjo-daro", "SEAL:S", None,        "SD2445/105",   "+520-220-033-705-803-055-002-820+"
    814,  "Mohenjo-daro", "SEAL:S", None,        "VS1779/104",   "+740-773-033-705-233-803-002-861+"
    1470, "Mohenjo-daro", "SEAL:S", None,        "DK-i 1066",    "+520-070-033-705-233-235-803+"
    404,  "Mohenjo-daro", "SEAL:S", None,        "DK6357/446",   "+740-585-017-033-705-233-798-803-002-861-603+"
    856,  "Mohenjo-daro", "SEAL:S", None,        "DK221/147",    "+520-033-705-236-803+"
    385,  "Mohenjo-daro", "SEAL:S", None,        "DK5821/426",   "+740-176-033-705-240-235-803+"
    // Page 20: 2 more from the Set 17 search example
    1296, "Harappa",      "SEAL:S", Some "LP",   "11453 619",    "+740-585-017-240-002-305-032-904+"
    3119, "Mohenjo-daro", "SEAL:S", Some "LP",   "DK12124/174",  "+740-798-231-002-298-460-032+" ]

// Sign roles aggregated across the help PDF.
let signRoles = [
    // TMK — Terminal Markers (page 4 caption "terminal marker sign 760"; page 7
    // structure analysis "TMK 520 or 740"; page 18 matrix lists 90/226/400/520/
    // 527/740 explicitly as TMK signs).
    90,  "TMK", "page 18 matrix (TMK row)"
    226, "TMK", "page 18 matrix (TMK row)"
    400, "TMK", "page 18 matrix (TMK row)"
    520, "TMK", "page 7 example + page 18 matrix"
    527, "TMK", "page 18 matrix (TMK row)"
    740, "TMK", "page 7 example + page 18 matrix"
    760, "TMK", "page 4 caption — 'terminal marker sign 760'"

    // ITM — Initial Cluster Terminal Markers (page 1 figure showed sign 2 as ICTM;
    // page 15 structure analysis used "ITM 060").
    2,  "ITM", "page 1 figure caption — 'Initial Cluster Terminal Marker sign 2'"
    60, "ITM", "page 15 structure analysis — 'ITM 060'"

    // NUM — Numerals (page 19 column headers list these explicitly as NUMs in
    // the right-sign axis of the 700×NUM matrix).
    1, "NUM",  "page 19 matrix column header"
    2, "NUM",  "page 19 matrix column header"  // also ITM — multi-role allowed by PK
    3, "NUM",  "page 19 matrix column header"
    4, "NUM",  "page 19 matrix column header"
    5, "NUM",  "page 19 matrix column header"
    6, "NUM",  "page 19 matrix column header"
    7, "NUM",  "page 19 matrix column header"
    13, "NUM", "page 19 matrix column header"
    14, "NUM", "page 19 matrix column header"
    15, "NUM", "page 19 matrix column header"
    16, "NUM", "page 19 matrix column header"
    17, "NUM", "page 19 matrix column header"
    18, "NUM", "page 19 matrix column header"
    19, "NUM", "page 19 matrix column header"
    20, "NUM", "page 19 matrix column header"
    31, "NUM", "page 19 matrix column header"
    32, "NUM", "page 19 matrix column header"
    33, "NUM", "page 19 matrix column header"
    34, "NUM", "page 19 matrix column header"
    35, "NUM", "page 19 matrix column header"
    36, "NUM", "page 19 matrix column header"
    37, "NUM", "page 19 matrix column header"
    39, "NUM", "page 19 matrix column header"
    55, "NUM", "page 19 matrix column header"
    65, "NUM", "page 19 matrix column header"
    415, "NUM", "page 19 matrix column header"
    416, "NUM", "page 19 matrix column header"
    705, "NUM", "page 19 matrix column header"
    900, "NUM", "page 19 matrix column header"
    909, "NUM", "page 19 matrix column header"

    // initial sign — sign 820 noted as "typical initial sign" page 4
    820, "INITIAL", "page 4 caption — 'typical initial sign 820'" ]

// Page 18 matrix: TMK × TMK adjacency frequencies (left → right). Only non-zero
// entries are stored.
let tmkPairs = [
    // (sign_left, sign_right, frequency)
    90,  400, 1
    90,  520, 1
    90,  740, 115
    400, 90,  12
    400, 226, 3
    400, 520, 15
    400, 527, 3
    400, 740, 216
    520, 400, 8
    740, 90,  1
    740, 400, 14
    740, 740, 1 ]

// Page 19 matrix: sign 700 (left) × NUM signs (right). Non-zero entries only.
let sign700NumPairs = [
    700, 3,  2
    700, 6,  8
    700, 14, 1
    700, 31, 3
    700, 32, 89
    700, 33, 136
    700, 34, 105
    700, 36, 2 ]

// Page 16: documented sign clusters
let signClusters = [
    "400-740-176", 42, "TAB:B + TAB:I from Harappa", "page 16 — sign cluster analysis introductory example"
    "740-176",     4,  "POT:T:g across 401 texts (ICIT IDs 702, 719, 1954, 2165)", "page 16 — 2-sign cluster minimum-4 search result"
    "001-480",     4,  "ICIT IDs 1872, 1873, 2152, 2161",                            "page 16 — 2-sign cluster minimum-4 search result"
    "002-861",     5,  "ICIT IDs 18, 323, 2142, 2165, 4122",                         "page 16 — 2-sign cluster minimum-4 search result" ]

// Page 21: References
let bibliography = [
    "Wells1999",  "Wells, Bryan K.",  1999, "An Introduction to Indus Writing", "MA Thesis, Early Site Research Foundation, Independence", None
    "Wells2011",  "Wells, Bryan K.",  2011, "Epigraphic Approaches To Indus Writing", "Oxbow books, Oakville and Oxford", None
    "Wells2015",  "Wells, Bryan K.",  2015, "The Archaeology and Epigraphy of Indus Writing", "Archaeopress, Oxford", None
    "Fuls2010",   "Fuls, Andreas",    2010, "Entwicklung einer geographisch-epigraphischen Datenbank der Indusschrift", "In: Sven Weisbrich, Robert Kaden (Ed.): Entwicklerforum Geoinformationstechnik 2010. Shaker Verlag, Aachen, pp. 29-45", None
    "Fuls2014",   "Fuls, Andreas",    2014, "Positional Analysis of Indus Signs", "Epigrafika, Vol. 7 (1), pp. 253-275", None
    "Fuls2015a",  "Fuls, Andreas",    2015, "Appendix I: Automated Segmentation of Indus Texts", "In: Bryan K. Wells, The Archaeology and Epigraphy of Indus Writing. Archaeopress, Oxford, pp. 100-118", None
    "Fuls2015b",  "Fuls, Andreas",    2015, "Appendix II: Positional Analysis of Indus Signs", "In: Bryan K. Wells, The Archaeology and Epigraphy of Indus Writing. Archaeopress, Oxford, pp. 119-133", None
    "Fuls2015c",  "Fuls, Andreas",    2015, "Appendix III: Classifying Undeciphered Writing Systems", "In: Bryan K. Wells, The Archaeology and Epigraphy of Indus Writing. Archaeopress, Oxford, pp. 134-140", None ]

// === EXEC ====================================================================

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
    for sql in migrations do
        exec sql []

    exec
        ("INSERT INTO source (code, name, year, note) VALUES ($c, $n, $y, $note) " +
         "ON CONFLICT(code) DO UPDATE SET name=excluded.name, year=excluded.year, note=excluded.note")
        [ "$c",    box sourceRow.Code
          "$n",    box sourceRow.Name
          "$y",    box sourceRow.Year
          "$note", box sourceRow.Note ]

    let mutable nFn = 0
    for (c, l) in signFunctions do
        exec
            ("INSERT INTO sign_function (source_code, function_code, label) VALUES ($s, $c, $l) " +
             "ON CONFLICT(source_code, function_code) DO UPDATE SET label=excluded.label")
            [ "$s", box sourceCode; "$c", box c; "$l", box l ]
        nFn <- nFn + 1

    let mutable nDir = 0
    for (c, l) in readingDirCodes do
        exec
            ("INSERT INTO reading_direction_code (source_code, code, label) VALUES ($s, $c, $l) " +
             "ON CONFLICT(source_code, code) DO UPDATE SET label=excluded.label")
            [ "$s", box sourceCode; "$c", box c; "$l", box l ]
        nDir <- nDir + 1

    let mutable nIns = 0
    for (id, site, atype, cls, idno, code) in icitInscriptions do
        exec
            ("INSERT INTO icit_inscription (icit_id, source_code, site, artefact_type, text_class, excavation_idno, text_code) " +
             "VALUES ($id, $s, $site, $at, $cl, $idno, $tc) " +
             "ON CONFLICT(source_code, icit_id) DO UPDATE SET site=excluded.site, artefact_type=excluded.artefact_type, text_class=excluded.text_class, excavation_idno=excluded.excavation_idno, text_code=excluded.text_code")
            [ "$id",   box id
              "$s",    box sourceCode
              "$site", box site
              "$at",   box atype
              "$cl",   nullable cls
              "$idno", box idno
              "$tc",   box code ]
        nIns <- nIns + 1

    let mutable nRole = 0
    for (sid, fcode, evid) in signRoles do
        exec
            ("INSERT INTO sign_role (source_code, sign_id, function_code, evidence) VALUES ($s, $sid, $f, $e) " +
             "ON CONFLICT(source_code, sign_id, function_code) DO UPDATE SET evidence=excluded.evidence")
            [ "$s", box sourceCode; "$sid", box sid; "$f", box fcode; "$e", box evid ]
        nRole <- nRole + 1

    let mutable nPair = 0
    for (l, r, f) in tmkPairs do
        exec
            ("INSERT INTO sign_pair_frequency (source_code, sign_left, sign_right, frequency, context) VALUES ($s, $l, $r, $f, $ctx) " +
             "ON CONFLICT(source_code, sign_left, sign_right) DO UPDATE SET frequency=excluded.frequency, context=excluded.context")
            [ "$s", box sourceCode; "$l", box l; "$r", box r; "$f", box f; "$ctx", box "TMK-TMK adjacency, page 18 matrix" ]
        nPair <- nPair + 1
    for (l, r, f) in sign700NumPairs do
        exec
            ("INSERT INTO sign_pair_frequency (source_code, sign_left, sign_right, frequency, context) VALUES ($s, $l, $r, $f, $ctx) " +
             "ON CONFLICT(source_code, sign_left, sign_right) DO UPDATE SET frequency=excluded.frequency, context=excluded.context")
            [ "$s", box sourceCode; "$l", box l; "$r", box r; "$f", box f; "$ctx", box "sign 700 → NUM right-adjacency, page 19 matrix" ]
        nPair <- nPair + 1

    let mutable nClu = 0
    for (signs, freq, ctx, evid) in signClusters do
        exec
            ("INSERT INTO sign_cluster (source_code, signs, frequency, contexts, evidence) VALUES ($s, $sg, $f, $c, $e) " +
             "ON CONFLICT(source_code, signs) DO UPDATE SET frequency=excluded.frequency, contexts=excluded.contexts, evidence=excluded.evidence")
            [ "$s", box sourceCode; "$sg", box signs; "$f", box freq; "$c", box ctx; "$e", box evid ]
        nClu <- nClu + 1

    let mutable nBib = 0
    for (key, author, year, title, venue, url) in bibliography do
        exec
            ("INSERT INTO bibliography_reference (citation_key, source_code, author, year, title, venue, url) VALUES ($k, $s, $a, $y, $t, $v, $u) " +
             "ON CONFLICT(source_code, citation_key) DO UPDATE SET author=excluded.author, year=excluded.year, title=excluded.title, venue=excluded.venue, url=excluded.url")
            [ "$k", box key
              "$s", box sourceCode
              "$a", box author
              "$y", box year
              "$t", box title
              "$v", box venue
              "$u", nullable url ]
        nBib <- nBib + 1

    txn.Commit()

    printfn "OK: ingested ICIT help PDF data"
    printfn "  source_code         : %s" sourceCode
    printfn "  sign_function       : %d" nFn
    printfn "  reading_direction   : %d" nDir
    printfn "  icit_inscription    : %d" nIns
    printfn "  sign_role           : %d" nRole
    printfn "  sign_pair_frequency : %d" nPair
    printfn "  sign_cluster        : %d" nClu
    printfn "  bibliography_ref    : %d" nBib
with
| ex ->
    txn.Rollback()
    eprintfn "FAIL: %s" ex.Message
    eprintfn "%s" ex.StackTrace
    exit 1
