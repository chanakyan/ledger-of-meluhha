#!/usr/bin/env -S dotnet fsi
// SPDX-License-Identifier: BSD-2-Clause
// Ingest Fuls 2022 'Corpus of Indus Inscriptions' (Mathematica Epigraphica No. 3,
// ISBN 978-1-671-80486-9) metadata into indus_corpus.db.
//
// The published PDF (35 pages) contains only Chapters 1-2 + bibliography. The
// Chapter 3 site sections (book pp. 25-568) and the four cross-reference indices
// (ICIT/CISI/excavation/M77) are NOT in any PDF Fuls distributes; they exist
// only in the ICIT online database (epigraphica.de, 25 EUR/mo subscription whose
// links Raj reports as broken). This script ingests what the book itself yields:
//
//   - source row (FULS2022, ICIT v2.8 May 2022)
//   - 73 site rows from Chapter 3 ToC (with Fuls book page anchors)
//   - artefact typology from Section 1.1 (10 top-level types + 17 subtypes)
//   - corpus headline statistics from Tables 1.1, 2.5
//   - reading direction statistics from Table 2.5
//
// The 709-sign sign list (Tables 2.6-2.8) is rendered as glyph images in the
// PDF; sign IDs without glyphs are not loaded here. That's a separate ingest
// once we have either OCR'd glyphs or a public Wells-2015 sign list export.
//
// Usage:
//   dotnet fsi indus_fuls2022_to_sqlite.fsx <output-db>
//   dotnet fsi indus_fuls2022_to_sqlite.fsx indus_corpus.db

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open System.IO
open Microsoft.Data.Sqlite

let args = fsi.CommandLineArgs |> Array.tail

let dbPath =
    match args with
    | [| db |] -> db
    | _ ->
        eprintfn "Usage: dotnet fsi indus_fuls2022_to_sqlite.fsx <output-db>"
        exit 1

if not (File.Exists dbPath) then
    eprintfn "ERROR: database not found: %s" dbPath
    exit 1

// === DATA: source row =========================================================

let sourceCode = "FULS2022"

let sourceRow =
    {| Code = sourceCode
       Name = "Fuls 2022 — Corpus of Indus Inscriptions (ICIT v2.8, May 2022)"
       Year = 2022
       Note = "Andreas Fuls. Mathematica Epigraphica No. 3, Berlin. ISBN 978-1-671-80486-9. ICIT online: https://www.epigraphica.de/" |}

// === DATA: 73 sites with book-page anchors (parsed from ToC pp. iv-vi) =======

let sites : (int * string * int) list = [
    (1,  "Alamgirpur",          28)
    (2,  "Allahdino",            28)
    (3,  "Altyn Depe",           29)
    (4,  "Amri",                 29)
    (5,  "Bakkar Buthi",         30)
    (6,  "Bala-kot",             30)
    (7,  "Banawali",             31)
    (8,  "Baror",                33)
    (9,  "Bhirrana",             33)
    (10, "Chandigarh",           34)
    (11, "Chanhujo-daro",        34)
    (12, "Daimabad",             41)
    (13, "Derawar Ther",         41)
    (14, "Desalpur",             41)
    (15, "Dholavira",            42)
    (16, "Failaka",              58)
    (17, "Farmana",              59)
    (18, "Ganweriwala",          59)
    (19, "Gharo Bhiro",          60)
    (20, "Girsu",                60)
    (21, "Gola Dhoro (Bagasra)", 60)
    (22, "Gonur Depe",           61)
    (23, "Guddal A",             61)
    (24, "Gumla",                61)
    (25, "Hajar",                62)
    (26, "Harappa",              62)
    (27, "Hissam-dheri",        256)
    (28, "Hulas",               257)
    (29, "Janabiyah",           257)
    (30, "Jhukar",              257)
    (31, "Kalibangan",          258)
    (32, "Kanmer",              273)
    (33, "Karanpura",           274)
    (34, "Karzakan",            274)
    (35, "Khirsara",            274)
    (36, "Kish",                275)
    (37, "Kot-Diji",            275)
    (38, "Lakhanjo-daro",       276)
    (39, "Lohumjo-daro",        278)
    (40, "Lothal",              278)
    (41, "Luristan",            293)
    (42, "Miri Qalat",          293)
    (43, "Mohenjo-daro",        294)
    (44, "Naru-Waro-dharo",     436)
    (45, "Nausharo",            436)
    (46, "Nindowari-damb",      439)
    (47, "Nippur",              440)
    (48, "Nuhato",              440)
    (49, "Pabumath",            441)
    (50, "Pirak",               441)
    (51, "Qala'at Al-Bahrain",  441)
    (52, "Ra's Al-Junayz",      441)
    (53, "Rahman-deri",         442)
    (54, "Rajanpur",            442)
    (55, "Rakhigarhi",          443)
    (56, "Rappwala Ther",       443)
    (57, "Rodji",               444)
    (58, "Rupar",               444)
    (59, "Saar",                445)
    (60, "Salut",               445)
    (61, "Shikarpur",           445)
    (62, "Shortughai",          446)
    (63, "Sibri",               446)
    (64, "Surkotada",           446)
    (65, "Susa",                447)
    (66, "Tarkhanewala-dera",   447)
    (67, "Tell Umma",           448)
    (68, "Tello",               448)
    (69, "Tepe Yahya",          448)
    (70, "Tigrana",             449)
    (71, "Unknown",             449)
    (72, "Ur",                  451)
    (73, "Wattoowala",          451) ]

// Sites outside the Indus core that Fuls includes (Mesopotamia / Iran / Gulf /
// Central Asia / Bahrain). Region inferred from archaeological context.
let regionOf name =
    match name with
    | "Altyn Depe" | "Gonur Depe"                                     -> "Central Asia"
    | "Failaka" | "Janabiyah" | "Qala'at Al-Bahrain" | "Saar"          -> "Persian Gulf"
    | "Girsu" | "Kish" | "Nippur" | "Tell Umma" | "Tello" | "Ur"      -> "Mesopotamia"
    | "Luristan" | "Susa" | "Tepe Yahya"                              -> "Iran"
    | "Ra's Al-Junayz" | "Salut"                                      -> "Oman"
    | "Shortughai"                                                    -> "Afghanistan"
    | "Hulas" | "Alamgirpur" | "Bhirrana" | "Chandigarh"
    | "Farmana" | "Hissam-dheri" | "Rakhigarhi" | "Rupar"
    | "Banawali"                                                      -> "Haryana/UP/Punjab India"
    | "Allahdino" | "Bakkar Buthi" | "Chanhujo-daro" | "Gharo Bhiro"
    | "Jhukar" | "Kot-Diji" | "Lakhanjo-daro" | "Lohumjo-daro"
    | "Mohenjo-daro" | "Naru-Waro-dharo" | "Rajanpur" | "Shikarpur"   -> "Sindh"
    | "Bala-kot" | "Hajar" | "Karzakan" | "Miri Qalat"
    | "Nindowari-damb" | "Nausharo" | "Pirak" | "Rahman-deri"
    | "Sibri"                                                         -> "Balochistan"
    | "Amri" | "Baror" | "Daimabad" | "Derawar Ther" | "Desalpur"
    | "Dholavira" | "Gola Dhoro (Bagasra)" | "Guddal A" | "Gumla"
    | "Harappa" | "Kalibangan" | "Kanmer" | "Karanpura" | "Khirsara"
    | "Lothal" | "Pabumath" | "Rappwala Ther" | "Rodji" | "Surkotada"
    | "Tarkhanewala-dera" | "Tigrana" | "Wattoowala" | "Ganweriwala"  -> "Indus core"
    | "Unknown"                                                       -> "Unknown"
    | _                                                               -> "Indus core"

// === DATA: artefact typology (Fuls 2022, §1.1) ================================

let artefactTypes : (string * string option * string) list = [
    // Top-level types
    "TAB",     None,           "Miniature tablets"
    "POT",     None,           "Pot sherds or ceramic vessels"
    "SEAL",    None,           "Intaglio carved seals"
    "TAG",     None,           "Clay objects with seal impressions (sealings)"
    "BNGL",    None,           "Bangles"
    "ROD",     None,           "Cylindrical artefacts"
    "IMPL",    None,           "Implements or tools"
    "BEAD",    None,           "Beads"
    "MDLN",    None,           "Medallions"
    "MISC",    None,           "Other (not in the categories above)"

    // TAB subtypes
    "TAB:B",   Some "TAB",     "Bas-relief tablets"
    "TAB:I",   Some "TAB",     "Incised tablets"
    "TAB:C",   Some "TAB",     "Copper tablets"

    // POT subtypes
    "POT:T",   Some "POT",     "Pottery with text"
    "POT:T:s", Some "POT:T",   "Pottery text — seal impression"
    "POT:T:g", Some "POT:T",   "Pottery text — graffiti"
    "POT:T:p", Some "POT:T",   "Pottery text — painting"
    "POT:D",   Some "POT",     "Pottery with drawings"
    "POT:M",   Some "POT",     "Pottery with potter's marks"

    // SEAL subtypes
    "SEAL:S",  Some "SEAL",    "Square seals"
    "SEAL:R",  Some "SEAL",    "Rectangular seals"
    "SEAL:C",  Some "SEAL",    "Circular seals"
    "SEAL:O",  Some "SEAL",    "Oval seals"
    "SEAL:CY", Some "SEAL",    "Cylindrical seals"
    "SEAL:L",  Some "SEAL",    "Lenticular seals"
    "SEAL:Ot", Some "SEAL",    "Other seal shapes"

    // TAG subtypes
    "TAG:P",   Some "TAG",     "Palm sealing"
    "TAG:C",   Some "TAG",     "Cube sealing"
    "TAG:R",   Some "TAG",     "Pot rim sealing"
    "TAG:L",   Some "TAG",     "Sealing on textile or reed"
    "TAG:W",   Some "TAG",     "Sealing on wooden strip or pole"
    "TAG:B",   Some "TAG",     "Sealing on box"
    "TAG:Ot",  Some "TAG",     "Other sealing types" ]

// === DATA: corpus headline statistics (Tables 1.1, 2.5; §2.1) ================

let corpusStat =
    {| ArtefactCount       = 4660
       TextCount           = 5644
       SignOccurrenceCount = 19831
       LegibleSignCount    = 17957
       DistinctSigns       = 709
       SignIdRangeLow      = 1
       SignIdRangeHigh     = 958
       Note                = "ICIT v2.8 (May 2022). 3657 texts complete with 13672 sign occurrences. 1874 sign occurrences are eroded or unidentifiable (encoded 000). 14 blank spaces encoded 999. Iconography-only ECIT extension: 888 additional artefacts." |}

// === DATA: reading direction statistics (Table 2.5) ==========================

let readingDirections : (string * int * int * int * int) list = [
    // (direction, n_texts, n_lines, n_sign_occurrences, n_legible_signs)
    "right_to_left",        4235, 4328, 17030, 15737
    "left_to_right",         215,  216,   661,   617
    "top_to_bottom",          16,   18,    51,    45
    "boustrophedon",          10,   15,    74,    72
    "single_sign_text_line", 376,  377,   385,   376
    "symmetrical_sign_seq",    8,    8,    18,    18
    "unknown_or_doubtful",   784,  807,  1612,  1092
    "TOTAL",                5644, 5769, 19831, 17957 ]

// === SCHEMA: additive — only CREATE IF NOT EXISTS ============================

let migrations = [
    """
    CREATE TABLE IF NOT EXISTS artefact_type (
        code        TEXT NOT NULL,
        source_code TEXT NOT NULL REFERENCES source(code),
        parent_code TEXT,
        label       TEXT NOT NULL,
        PRIMARY KEY (source_code, code)
    ) STRICT;
    """
    """
    CREATE TABLE IF NOT EXISTS corpus_stat (
        source_code           TEXT PRIMARY KEY REFERENCES source(code),
        n_artefacts           INTEGER NOT NULL,
        n_texts               INTEGER NOT NULL,
        n_sign_occurrences    INTEGER NOT NULL,
        n_legible_signs       INTEGER NOT NULL,
        n_distinct_signs      INTEGER NOT NULL,
        sign_id_range_low     INTEGER,
        sign_id_range_high    INTEGER,
        note                  TEXT
    ) STRICT;
    """
    """
    CREATE TABLE IF NOT EXISTS reading_direction_stat (
        source_code         TEXT NOT NULL REFERENCES source(code),
        direction           TEXT NOT NULL,
        n_texts             INTEGER NOT NULL,
        n_lines             INTEGER NOT NULL,
        n_sign_occurrences  INTEGER NOT NULL,
        n_legible_signs     INTEGER NOT NULL,
        PRIMARY KEY (source_code, direction)
    ) STRICT;
    """
    """
    CREATE TABLE IF NOT EXISTS site_book_anchor (
        source_code TEXT NOT NULL REFERENCES source(code),
        site_id     TEXT NOT NULL REFERENCES site(id),
        page        INTEGER NOT NULL,
        PRIMARY KEY (source_code, site_id)
    ) STRICT;
    """ ]

// === EXEC ====================================================================

let conn = new SqliteConnection($"Data Source={dbPath}")
conn.Open()

let txn = conn.BeginTransaction()

try
    // 1. Schema migrations (idempotent)
    for sql in migrations do
        use cmd = conn.CreateCommand()
        cmd.Transaction <- txn
        cmd.CommandText <- sql
        cmd.ExecuteNonQuery() |> ignore

    // 2. Source row
    let cmdSource = conn.CreateCommand()
    cmdSource.Transaction <- txn
    cmdSource.CommandText <-
        "INSERT INTO source (code, name, year, note) VALUES ($code, $name, $year, $note) " +
        "ON CONFLICT(code) DO UPDATE SET name=excluded.name, year=excluded.year, note=excluded.note"
    cmdSource.Parameters.AddWithValue("$code", sourceRow.Code)  |> ignore
    cmdSource.Parameters.AddWithValue("$name", sourceRow.Name)  |> ignore
    cmdSource.Parameters.AddWithValue("$year", sourceRow.Year)  |> ignore
    cmdSource.Parameters.AddWithValue("$note", sourceRow.Note)  |> ignore
    cmdSource.ExecuteNonQuery() |> ignore

    // 3. Sites + book-page anchors
    let mutable nSites = 0
    for (n, name, page) in sites do
        let siteId =
            "FULS_" +
            (name.ToUpper()
                  .Replace(" ", "_")
                  .Replace("-", "_")
                  .Replace("'", "")
                  .Replace("(", "")
                  .Replace(")", ""))
        let region = regionOf name

        let cmdSite = conn.CreateCommand()
        cmdSite.Transaction <- txn
        cmdSite.CommandText <-
            "INSERT INTO site (id, name, source_code, region, period) " +
            "VALUES ($id, $name, $src, $region, NULL) " +
            "ON CONFLICT(id) DO UPDATE SET name=excluded.name, source_code=excluded.source_code, region=excluded.region"
        cmdSite.Parameters.AddWithValue("$id",     siteId)        |> ignore
        cmdSite.Parameters.AddWithValue("$name",   name)          |> ignore
        cmdSite.Parameters.AddWithValue("$src",    sourceCode)    |> ignore
        cmdSite.Parameters.AddWithValue("$region", region)        |> ignore
        cmdSite.ExecuteNonQuery() |> ignore

        let cmdAnchor = conn.CreateCommand()
        cmdAnchor.Transaction <- txn
        cmdAnchor.CommandText <-
            "INSERT INTO site_book_anchor (source_code, site_id, page) " +
            "VALUES ($src, $sid, $page) " +
            "ON CONFLICT(source_code, site_id) DO UPDATE SET page=excluded.page"
        cmdAnchor.Parameters.AddWithValue("$src",  sourceCode) |> ignore
        cmdAnchor.Parameters.AddWithValue("$sid",  siteId)     |> ignore
        cmdAnchor.Parameters.AddWithValue("$page", page)       |> ignore
        cmdAnchor.ExecuteNonQuery() |> ignore

        nSites <- nSites + 1

    // 4. Artefact typology
    let mutable nTypes = 0
    for (code, parent, label) in artefactTypes do
        let cmd = conn.CreateCommand()
        cmd.Transaction <- txn
        cmd.CommandText <-
            "INSERT INTO artefact_type (source_code, code, parent_code, label) " +
            "VALUES ($src, $code, $parent, $label) " +
            "ON CONFLICT(source_code, code) DO UPDATE SET parent_code=excluded.parent_code, label=excluded.label"
        cmd.Parameters.AddWithValue("$src",    sourceCode)                                      |> ignore
        cmd.Parameters.AddWithValue("$code",   code)                                            |> ignore
        cmd.Parameters.AddWithValue("$parent", parent |> Option.map box |> Option.defaultValue (DBNull.Value :> obj)) |> ignore
        cmd.Parameters.AddWithValue("$label",  label)                                           |> ignore
        cmd.ExecuteNonQuery() |> ignore
        nTypes <- nTypes + 1

    // 5. Corpus headline stats
    let cmdStat = conn.CreateCommand()
    cmdStat.Transaction <- txn
    cmdStat.CommandText <-
        "INSERT INTO corpus_stat (source_code, n_artefacts, n_texts, n_sign_occurrences, n_legible_signs, n_distinct_signs, sign_id_range_low, sign_id_range_high, note) " +
        "VALUES ($src, $a, $t, $so, $ls, $ds, $lo, $hi, $note) " +
        "ON CONFLICT(source_code) DO UPDATE SET " +
        "n_artefacts=excluded.n_artefacts, n_texts=excluded.n_texts, " +
        "n_sign_occurrences=excluded.n_sign_occurrences, n_legible_signs=excluded.n_legible_signs, " +
        "n_distinct_signs=excluded.n_distinct_signs, sign_id_range_low=excluded.sign_id_range_low, " +
        "sign_id_range_high=excluded.sign_id_range_high, note=excluded.note"
    cmdStat.Parameters.AddWithValue("$src",  sourceCode)                       |> ignore
    cmdStat.Parameters.AddWithValue("$a",    corpusStat.ArtefactCount)         |> ignore
    cmdStat.Parameters.AddWithValue("$t",    corpusStat.TextCount)             |> ignore
    cmdStat.Parameters.AddWithValue("$so",   corpusStat.SignOccurrenceCount)   |> ignore
    cmdStat.Parameters.AddWithValue("$ls",   corpusStat.LegibleSignCount)      |> ignore
    cmdStat.Parameters.AddWithValue("$ds",   corpusStat.DistinctSigns)         |> ignore
    cmdStat.Parameters.AddWithValue("$lo",   corpusStat.SignIdRangeLow)        |> ignore
    cmdStat.Parameters.AddWithValue("$hi",   corpusStat.SignIdRangeHigh)       |> ignore
    cmdStat.Parameters.AddWithValue("$note", corpusStat.Note)                  |> ignore
    cmdStat.ExecuteNonQuery() |> ignore

    // 6. Reading direction stats
    let mutable nDirs = 0
    for (dir, t, l, so, ls) in readingDirections do
        let cmd = conn.CreateCommand()
        cmd.Transaction <- txn
        cmd.CommandText <-
            "INSERT INTO reading_direction_stat (source_code, direction, n_texts, n_lines, n_sign_occurrences, n_legible_signs) " +
            "VALUES ($src, $dir, $t, $l, $so, $ls) " +
            "ON CONFLICT(source_code, direction) DO UPDATE SET " +
            "n_texts=excluded.n_texts, n_lines=excluded.n_lines, " +
            "n_sign_occurrences=excluded.n_sign_occurrences, n_legible_signs=excluded.n_legible_signs"
        cmd.Parameters.AddWithValue("$src", sourceCode) |> ignore
        cmd.Parameters.AddWithValue("$dir", dir)        |> ignore
        cmd.Parameters.AddWithValue("$t",   t)          |> ignore
        cmd.Parameters.AddWithValue("$l",   l)          |> ignore
        cmd.Parameters.AddWithValue("$so",  so)         |> ignore
        cmd.Parameters.AddWithValue("$ls",  ls)         |> ignore
        cmd.ExecuteNonQuery() |> ignore
        nDirs <- nDirs + 1

    txn.Commit()

    printfn "OK: ingested Fuls 2022 metadata"
    printfn "  source           : %s" sourceCode
    printfn "  sites            : %d" nSites
    printfn "  artefact_types   : %d" nTypes
    printfn "  corpus_stat rows : 1"
    printfn "  reading_dir rows : %d" nDirs
with
| ex ->
    txn.Rollback()
    eprintfn "FAIL: %s" ex.Message
    eprintfn "%s" ex.StackTrace
    exit 1
