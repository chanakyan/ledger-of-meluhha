#!/usr/bin/env -S dotnet fsi
// SPDX-License-Identifier: BSD-2-Clause
// Implements: Ledger of Meluhha §The Database — Fuls 2022 metadata scaffold
// Coding standard: spec/fsharp/reference/fsharp_coding_standard.tex
//
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

type FulsSite = { Number: int; Name: string; BookPage: int }

let sites : FulsSite list = [
    { Number=1;  Name="Alamgirpur";          BookPage=28 }
    { Number=2;  Name="Allahdino";           BookPage=28 }
    { Number=3;  Name="Altyn Depe";          BookPage=29 }
    { Number=4;  Name="Amri";                BookPage=29 }
    { Number=5;  Name="Bakkar Buthi";        BookPage=30 }
    { Number=6;  Name="Bala-kot";            BookPage=30 }
    { Number=7;  Name="Banawali";            BookPage=31 }
    { Number=8;  Name="Baror";               BookPage=33 }
    { Number=9;  Name="Bhirrana";            BookPage=33 }
    { Number=10; Name="Chandigarh";          BookPage=34 }
    { Number=11; Name="Chanhujo-daro";       BookPage=34 }
    { Number=12; Name="Daimabad";            BookPage=41 }
    { Number=13; Name="Derawar Ther";        BookPage=41 }
    { Number=14; Name="Desalpur";            BookPage=41 }
    { Number=15; Name="Dholavira";           BookPage=42 }
    { Number=16; Name="Failaka";             BookPage=58 }
    { Number=17; Name="Farmana";             BookPage=59 }
    { Number=18; Name="Ganweriwala";         BookPage=59 }
    { Number=19; Name="Gharo Bhiro";         BookPage=60 }
    { Number=20; Name="Girsu";               BookPage=60 }
    { Number=21; Name="Gola Dhoro (Bagasra)"; BookPage=60 }
    { Number=22; Name="Gonur Depe";          BookPage=61 }
    { Number=23; Name="Guddal A";            BookPage=61 }
    { Number=24; Name="Gumla";               BookPage=61 }
    { Number=25; Name="Hajar";               BookPage=62 }
    { Number=26; Name="Harappa";             BookPage=62 }
    { Number=27; Name="Hissam-dheri";        BookPage=256 }
    { Number=28; Name="Hulas";               BookPage=257 }
    { Number=29; Name="Janabiyah";           BookPage=257 }
    { Number=30; Name="Jhukar";              BookPage=257 }
    { Number=31; Name="Kalibangan";          BookPage=258 }
    { Number=32; Name="Kanmer";              BookPage=273 }
    { Number=33; Name="Karanpura";           BookPage=274 }
    { Number=34; Name="Karzakan";            BookPage=274 }
    { Number=35; Name="Khirsara";            BookPage=274 }
    { Number=36; Name="Kish";                BookPage=275 }
    { Number=37; Name="Kot-Diji";            BookPage=275 }
    { Number=38; Name="Lakhanjo-daro";       BookPage=276 }
    { Number=39; Name="Lohumjo-daro";        BookPage=278 }
    { Number=40; Name="Lothal";              BookPage=278 }
    { Number=41; Name="Luristan";            BookPage=293 }
    { Number=42; Name="Miri Qalat";          BookPage=293 }
    { Number=43; Name="Mohenjo-daro";        BookPage=294 }
    { Number=44; Name="Naru-Waro-dharo";     BookPage=436 }
    { Number=45; Name="Nausharo";            BookPage=436 }
    { Number=46; Name="Nindowari-damb";      BookPage=439 }
    { Number=47; Name="Nippur";              BookPage=440 }
    { Number=48; Name="Nuhato";              BookPage=440 }
    { Number=49; Name="Pabumath";            BookPage=441 }
    { Number=50; Name="Pirak";               BookPage=441 }
    { Number=51; Name="Qala'at Al-Bahrain";  BookPage=441 }
    { Number=52; Name="Ra's Al-Junayz";      BookPage=441 }
    { Number=53; Name="Rahman-deri";         BookPage=442 }
    { Number=54; Name="Rajanpur";            BookPage=442 }
    { Number=55; Name="Rakhigarhi";          BookPage=443 }
    { Number=56; Name="Rappwala Ther";       BookPage=443 }
    { Number=57; Name="Rodji";               BookPage=444 }
    { Number=58; Name="Rupar";               BookPage=444 }
    { Number=59; Name="Saar";                BookPage=445 }
    { Number=60; Name="Salut";               BookPage=445 }
    { Number=61; Name="Shikarpur";           BookPage=445 }
    { Number=62; Name="Shortughai";          BookPage=446 }
    { Number=63; Name="Sibri";               BookPage=446 }
    { Number=64; Name="Surkotada";           BookPage=446 }
    { Number=65; Name="Susa";                BookPage=447 }
    { Number=66; Name="Tarkhanewala-dera";   BookPage=447 }
    { Number=67; Name="Tell Umma";           BookPage=448 }
    { Number=68; Name="Tello";               BookPage=448 }
    { Number=69; Name="Tepe Yahya";          BookPage=448 }
    { Number=70; Name="Tigrana";             BookPage=449 }
    { Number=71; Name="Unknown";             BookPage=449 }
    { Number=72; Name="Ur";                  BookPage=451 }
    { Number=73; Name="Wattoowala";          BookPage=451 } ]

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

type ArtefactType = { Code: string; ParentCode: string option; Label: string }

let artefactTypes : ArtefactType list = [
    // Top-level types
    { Code="TAB";     ParentCode=None;          Label="Miniature tablets" }
    { Code="POT";     ParentCode=None;          Label="Pot sherds or ceramic vessels" }
    { Code="SEAL";    ParentCode=None;          Label="Intaglio carved seals" }
    { Code="TAG";     ParentCode=None;          Label="Clay objects with seal impressions (sealings)" }
    { Code="BNGL";    ParentCode=None;          Label="Bangles" }
    { Code="ROD";     ParentCode=None;          Label="Cylindrical artefacts" }
    { Code="IMPL";    ParentCode=None;          Label="Implements or tools" }
    { Code="BEAD";    ParentCode=None;          Label="Beads" }
    { Code="MDLN";    ParentCode=None;          Label="Medallions" }
    { Code="MISC";    ParentCode=None;          Label="Other (not in the categories above)" }

    // TAB subtypes
    { Code="TAB:B";   ParentCode=Some "TAB";    Label="Bas-relief tablets" }
    { Code="TAB:I";   ParentCode=Some "TAB";    Label="Incised tablets" }
    { Code="TAB:C";   ParentCode=Some "TAB";    Label="Copper tablets" }

    // POT subtypes
    { Code="POT:T";   ParentCode=Some "POT";    Label="Pottery with text" }
    { Code="POT:T:s"; ParentCode=Some "POT:T";  Label="Pottery text — seal impression" }
    { Code="POT:T:g"; ParentCode=Some "POT:T";  Label="Pottery text — graffiti" }
    { Code="POT:T:p"; ParentCode=Some "POT:T";  Label="Pottery text — painting" }
    { Code="POT:D";   ParentCode=Some "POT";    Label="Pottery with drawings" }
    { Code="POT:M";   ParentCode=Some "POT";    Label="Pottery with potter's marks" }

    // SEAL subtypes
    { Code="SEAL:S";  ParentCode=Some "SEAL";   Label="Square seals" }
    { Code="SEAL:R";  ParentCode=Some "SEAL";   Label="Rectangular seals" }
    { Code="SEAL:C";  ParentCode=Some "SEAL";   Label="Circular seals" }
    { Code="SEAL:O";  ParentCode=Some "SEAL";   Label="Oval seals" }
    { Code="SEAL:CY"; ParentCode=Some "SEAL";   Label="Cylindrical seals" }
    { Code="SEAL:L";  ParentCode=Some "SEAL";   Label="Lenticular seals" }
    { Code="SEAL:Ot"; ParentCode=Some "SEAL";   Label="Other seal shapes" }

    // TAG subtypes
    { Code="TAG:P";   ParentCode=Some "TAG";    Label="Palm sealing" }
    { Code="TAG:C";   ParentCode=Some "TAG";    Label="Cube sealing" }
    { Code="TAG:R";   ParentCode=Some "TAG";    Label="Pot rim sealing" }
    { Code="TAG:L";   ParentCode=Some "TAG";    Label="Sealing on textile or reed" }
    { Code="TAG:W";   ParentCode=Some "TAG";    Label="Sealing on wooden strip or pole" }
    { Code="TAG:B";   ParentCode=Some "TAG";    Label="Sealing on box" }
    { Code="TAG:Ot";  ParentCode=Some "TAG";    Label="Other sealing types" } ]

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

type ReadingDirStat = {
    Direction:    string
    Texts:        int
    Lines:        int
    SignOcc:      int
    LegibleSigns: int }

let readingDirections : ReadingDirStat list = [
    { Direction="right_to_left";        Texts=4235; Lines=4328; SignOcc=17030; LegibleSigns=15737 }
    { Direction="left_to_right";        Texts=215;  Lines=216;  SignOcc=661;   LegibleSigns=617 }
    { Direction="top_to_bottom";        Texts=16;   Lines=18;   SignOcc=51;    LegibleSigns=45 }
    { Direction="boustrophedon";        Texts=10;   Lines=15;   SignOcc=74;    LegibleSigns=72 }
    { Direction="single_sign_text_line"; Texts=376; Lines=377;  SignOcc=385;   LegibleSigns=376 }
    { Direction="symmetrical_sign_seq"; Texts=8;    Lines=8;    SignOcc=18;    LegibleSigns=18 }
    { Direction="unknown_or_doubtful";  Texts=784;  Lines=807;  SignOcc=1612;  LegibleSigns=1092 }
    { Direction="TOTAL";                Texts=5644; Lines=5769; SignOcc=19831; LegibleSigns=17957 } ]

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
    for s in sites do
        let siteId =
            "FULS_" +
            (s.Name.ToUpper()
                   .Replace(" ", "_")
                   .Replace("-", "_")
                   .Replace("'", "")
                   .Replace("(", "")
                   .Replace(")", ""))
        let region = regionOf s.Name

        let cmdSite = conn.CreateCommand()
        cmdSite.Transaction <- txn
        cmdSite.CommandText <-
            "INSERT INTO site (id, name, source_code, region, period) " +
            "VALUES ($id, $name, $src, $region, NULL) " +
            "ON CONFLICT(id) DO UPDATE SET name=excluded.name, source_code=excluded.source_code, region=excluded.region"
        cmdSite.Parameters.AddWithValue("$id",     siteId)        |> ignore
        cmdSite.Parameters.AddWithValue("$name",   s.Name)        |> ignore
        cmdSite.Parameters.AddWithValue("$src",    sourceCode)    |> ignore
        cmdSite.Parameters.AddWithValue("$region", region)        |> ignore
        cmdSite.ExecuteNonQuery() |> ignore

        let cmdAnchor = conn.CreateCommand()
        cmdAnchor.Transaction <- txn
        cmdAnchor.CommandText <-
            "INSERT INTO site_book_anchor (source_code, site_id, page) " +
            "VALUES ($src, $sid, $page) " +
            "ON CONFLICT(source_code, site_id) DO UPDATE SET page=excluded.page"
        cmdAnchor.Parameters.AddWithValue("$src",  sourceCode)  |> ignore
        cmdAnchor.Parameters.AddWithValue("$sid",  siteId)      |> ignore
        cmdAnchor.Parameters.AddWithValue("$page", s.BookPage)  |> ignore
        cmdAnchor.ExecuteNonQuery() |> ignore

        nSites <- nSites + 1

    // 4. Artefact typology
    let mutable nTypes = 0
    for t in artefactTypes do
        let cmd = conn.CreateCommand()
        cmd.Transaction <- txn
        cmd.CommandText <-
            "INSERT INTO artefact_type (source_code, code, parent_code, label) " +
            "VALUES ($src, $code, $parent, $label) " +
            "ON CONFLICT(source_code, code) DO UPDATE SET parent_code=excluded.parent_code, label=excluded.label"
        cmd.Parameters.AddWithValue("$src",    sourceCode)                                              |> ignore
        cmd.Parameters.AddWithValue("$code",   t.Code)                                                  |> ignore
        cmd.Parameters.AddWithValue("$parent", t.ParentCode |> Option.map box |> Option.defaultValue (DBNull.Value :> obj)) |> ignore
        cmd.Parameters.AddWithValue("$label",  t.Label)                                                 |> ignore
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
    for r in readingDirections do
        let cmd = conn.CreateCommand()
        cmd.Transaction <- txn
        cmd.CommandText <-
            "INSERT INTO reading_direction_stat (source_code, direction, n_texts, n_lines, n_sign_occurrences, n_legible_signs) " +
            "VALUES ($src, $dir, $t, $l, $so, $ls) " +
            "ON CONFLICT(source_code, direction) DO UPDATE SET " +
            "n_texts=excluded.n_texts, n_lines=excluded.n_lines, " +
            "n_sign_occurrences=excluded.n_sign_occurrences, n_legible_signs=excluded.n_legible_signs"
        cmd.Parameters.AddWithValue("$src", sourceCode)     |> ignore
        cmd.Parameters.AddWithValue("$dir", r.Direction)    |> ignore
        cmd.Parameters.AddWithValue("$t",   r.Texts)        |> ignore
        cmd.Parameters.AddWithValue("$l",   r.Lines)        |> ignore
        cmd.Parameters.AddWithValue("$so",  r.SignOcc)      |> ignore
        cmd.Parameters.AddWithValue("$ls",  r.LegibleSigns) |> ignore
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
