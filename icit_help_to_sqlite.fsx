#!/usr/bin/env -S dotnet fsi
// SPDX-License-Identifier: BSD-2-Clause
// Implements: Ledger of Meluhha §The data path — ICIT help-PDF salvage
// Coding standard: spec/fsharp/reference/fsharp_coding_standard.tex
//
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

// Records per coding standard T1 (records over tuples).
type SignFunctionRow = { Code: string; Label: string }
type ReadingDirRow   = { Code: string; Label: string }
type IcitInscription = {
    IcitId:         int
    Site:           string
    ArtefactType:   string
    TextClass:      string option
    ExcavationIdno: string
    TextCode:       string }
type SignRoleRow = { SignId: int; FunctionCode: string; Evidence: string }
type SignPairRow = { Left: int; Right: int; Frequency: int }

let sourceRow =
    {| Code = sourceCode
       Name = "Fuls 2019 — Documentation of the Online Indus Writing Database (ICIT help PDF)"
       Year = 2019
       Note = "21-page PDF, ModDate 2019-09-04, hosted at https://www.user.tu-berlin.de/fuls/Homepage/indus/help_onlinedatabase.pdf. Only freely-accessible source for Wells-numbered ICIT content as of 2026-05-02 (caddy.igg.tu-berlin.de is unreachable; epigraphica.de subscription requires functioning links the user has not received)." |}

// Page 2-3: 16 sign-function codes
let signFunctions : SignFunctionRow list = [
    { Code="ITM"; Label="Initial Cluster Terminal Marker" }
    { Code="LOG"; Label="Logogram" }
    { Code="LON"; Label="Long Numeral" }
    { Code="FSH"; Label="Fish sign" }
    { Code="NUM"; Label="Numeral" }
    { Code="PTM"; Label="Post Terminal Marker" }
    { Code="S17"; Label="Set 17" }
    { Code="S28"; Label="Set 28" }
    { Code="S30"; Label="Set 30" }
    { Code="S36"; Label="Set 36" }
    { Code="SHN"; Label="Short Numeral" }
    { Code="SSN"; Label="Short Stacked Numeral" }
    { Code="SPN"; Label="Special Numeral" }
    { Code="SYL"; Label="Syllable" }
    { Code="TMK"; Label="Terminal Marker" }
    { Code="XXX"; Label="Test function" } ]

// Page 6-7: 4 reading-direction codes
let readingDirCodes : ReadingDirRow list = [
    { Code="R/L"; Label="Right to left (default; 87% of texts)" }
    { Code="L/R"; Label="Left to right" }
    { Code="T/B"; Label="Top to bottom" }
    { Code="BUS"; Label="Boustrophedon" } ]

// Page 7-8: text encoding rules — captured in the inscription text_code field
// directly. Page 8 example: 8 ICIT inscriptions, all Mohenjo-daro:
let icitInscriptions : IcitInscription list = [
    { IcitId=1202; Site="Mohenjo-daro"; ArtefactType="TAB:B";  TextClass=None;        ExcavationIdno="E232 CXVI:6"; TextCode="+520-033-705-803-853+" }
    { IcitId=1254; Site="Mohenjo-daro"; ArtefactType="SEAL:S"; TextClass=None;        ExcavationIdno="DK1104";      TextCode="+740-760-033-705-803-002-920-317+" }
    { IcitId=815;  Site="Mohenjo-daro"; ArtefactType="SEAL:S"; TextClass=None;        ExcavationIdno="SD2445/105";  TextCode="+520-220-033-705-803-055-002-820+" }
    { IcitId=814;  Site="Mohenjo-daro"; ArtefactType="SEAL:S"; TextClass=None;        ExcavationIdno="VS1779/104";  TextCode="+740-773-033-705-233-803-002-861+" }
    { IcitId=1470; Site="Mohenjo-daro"; ArtefactType="SEAL:S"; TextClass=None;        ExcavationIdno="DK-i 1066";   TextCode="+520-070-033-705-233-235-803+" }
    { IcitId=404;  Site="Mohenjo-daro"; ArtefactType="SEAL:S"; TextClass=None;        ExcavationIdno="DK6357/446";  TextCode="+740-585-017-033-705-233-798-803-002-861-603+" }
    { IcitId=856;  Site="Mohenjo-daro"; ArtefactType="SEAL:S"; TextClass=None;        ExcavationIdno="DK221/147";   TextCode="+520-033-705-236-803+" }
    { IcitId=385;  Site="Mohenjo-daro"; ArtefactType="SEAL:S"; TextClass=None;        ExcavationIdno="DK5821/426";  TextCode="+740-176-033-705-240-235-803+" }
    // Page 20: 2 more from the Set 17 search example
    { IcitId=1296; Site="Harappa";      ArtefactType="SEAL:S"; TextClass=Some "LP";   ExcavationIdno="11453 619";   TextCode="+740-585-017-240-002-305-032-904+" }
    { IcitId=3119; Site="Mohenjo-daro"; ArtefactType="SEAL:S"; TextClass=Some "LP";   ExcavationIdno="DK12124/174"; TextCode="+740-798-231-002-298-460-032+" } ]

// Helpers for compact role definitions.
let private numEvidence = "page 19 matrix column header"
let private numRow id = { SignId=id; FunctionCode="NUM"; Evidence=numEvidence }

// Sign roles aggregated across the help PDF.
let signRoles : SignRoleRow list = [
    // TMK — Terminal Markers (page 4 caption "terminal marker sign 760"; page 7
    // structure analysis "TMK 520 or 740"; page 18 matrix lists 90/226/400/520/
    // 527/740 explicitly as TMK signs).
    { SignId=90;  FunctionCode="TMK"; Evidence="page 18 matrix (TMK row)" }
    { SignId=226; FunctionCode="TMK"; Evidence="page 18 matrix (TMK row)" }
    { SignId=400; FunctionCode="TMK"; Evidence="page 18 matrix (TMK row)" }
    { SignId=520; FunctionCode="TMK"; Evidence="page 7 example + page 18 matrix" }
    { SignId=527; FunctionCode="TMK"; Evidence="page 18 matrix (TMK row)" }
    { SignId=740; FunctionCode="TMK"; Evidence="page 7 example + page 18 matrix" }
    { SignId=760; FunctionCode="TMK"; Evidence="page 4 caption — 'terminal marker sign 760'" }

    // ITM — Initial Cluster Terminal Markers
    { SignId=2;  FunctionCode="ITM"; Evidence="page 1 figure caption — 'Initial Cluster Terminal Marker sign 2'" }
    { SignId=60; FunctionCode="ITM"; Evidence="page 15 structure analysis — 'ITM 060'" }

    // NUM — Numerals (page 19 column headers list these as NUMs in the
    // right-sign axis of the 700×NUM matrix). Sign 2 is multi-role (also ITM).
    numRow 1;  numRow 2;  numRow 3;  numRow 4;  numRow 5;  numRow 6;  numRow 7
    numRow 13; numRow 14; numRow 15; numRow 16; numRow 17; numRow 18; numRow 19; numRow 20
    numRow 31; numRow 32; numRow 33; numRow 34; numRow 35; numRow 36; numRow 37; numRow 39
    numRow 55; numRow 65; numRow 415; numRow 416; numRow 705; numRow 900; numRow 909

    // INITIAL — sign 820 noted as "typical initial sign" page 4
    { SignId=820; FunctionCode="INITIAL"; Evidence="page 4 caption — 'typical initial sign 820'" } ]

// Page 18 matrix: TMK × TMK adjacency frequencies (left → right). Non-zero only.
let tmkPairs : SignPairRow list = [
    { Left=90;  Right=400; Frequency=1 }
    { Left=90;  Right=520; Frequency=1 }
    { Left=90;  Right=740; Frequency=115 }
    { Left=400; Right=90;  Frequency=12 }
    { Left=400; Right=226; Frequency=3 }
    { Left=400; Right=520; Frequency=15 }
    { Left=400; Right=527; Frequency=3 }
    { Left=400; Right=740; Frequency=216 }
    { Left=520; Right=400; Frequency=8 }
    { Left=740; Right=90;  Frequency=1 }
    { Left=740; Right=400; Frequency=14 }
    { Left=740; Right=740; Frequency=1 } ]

// Page 19 matrix: sign 700 (left) × NUM signs (right). Non-zero entries only.
let sign700NumPairs : SignPairRow list = [
    { Left=700; Right=3;  Frequency=2 }
    { Left=700; Right=6;  Frequency=8 }
    { Left=700; Right=14; Frequency=1 }
    { Left=700; Right=31; Frequency=3 }
    { Left=700; Right=32; Frequency=89 }
    { Left=700; Right=33; Frequency=136 }
    { Left=700; Right=34; Frequency=105 }
    { Left=700; Right=36; Frequency=2 } ]

type SignClusterRow = {
    Signs:     string
    Frequency: int
    Contexts:  string
    Evidence:  string }

// Page 16: documented sign clusters
let signClusters : SignClusterRow list = [
    { Signs="400-740-176"; Frequency=42; Contexts="TAB:B + TAB:I from Harappa";                                Evidence="page 16 — sign cluster analysis introductory example" }
    { Signs="740-176";     Frequency=4;  Contexts="POT:T:g across 401 texts (ICIT IDs 702, 719, 1954, 2165)";  Evidence="page 16 — 2-sign cluster minimum-4 search result" }
    { Signs="001-480";     Frequency=4;  Contexts="ICIT IDs 1872, 1873, 2152, 2161";                           Evidence="page 16 — 2-sign cluster minimum-4 search result" }
    { Signs="002-861";     Frequency=5;  Contexts="ICIT IDs 18, 323, 2142, 2165, 4122";                        Evidence="page 16 — 2-sign cluster minimum-4 search result" } ]

type BibliographyRow = {
    CitationKey: string
    Author:      string
    Year:        int
    Title:       string
    Venue:       string
    Url:         string option }

// Page 21: References
let bibliography : BibliographyRow list = [
    { CitationKey="Wells1999"; Author="Wells, Bryan K."; Year=1999; Title="An Introduction to Indus Writing";                                              Venue="MA Thesis, Early Site Research Foundation, Independence"; Url=None }
    { CitationKey="Wells2011"; Author="Wells, Bryan K."; Year=2011; Title="Epigraphic Approaches To Indus Writing";                                       Venue="Oxbow books, Oakville and Oxford"; Url=None }
    { CitationKey="Wells2015"; Author="Wells, Bryan K."; Year=2015; Title="The Archaeology and Epigraphy of Indus Writing";                               Venue="Archaeopress, Oxford"; Url=None }
    { CitationKey="Fuls2010";  Author="Fuls, Andreas";   Year=2010; Title="Entwicklung einer geographisch-epigraphischen Datenbank der Indusschrift";     Venue="In: Sven Weisbrich, Robert Kaden (Ed.): Entwicklerforum Geoinformationstechnik 2010. Shaker Verlag, Aachen, pp. 29-45"; Url=None }
    { CitationKey="Fuls2014";  Author="Fuls, Andreas";   Year=2014; Title="Positional Analysis of Indus Signs";                                           Venue="Epigrafika, Vol. 7 (1), pp. 253-275"; Url=None }
    { CitationKey="Fuls2015a"; Author="Fuls, Andreas";   Year=2015; Title="Appendix I: Automated Segmentation of Indus Texts";                            Venue="In: Bryan K. Wells, The Archaeology and Epigraphy of Indus Writing. Archaeopress, Oxford, pp. 100-118"; Url=None }
    { CitationKey="Fuls2015b"; Author="Fuls, Andreas";   Year=2015; Title="Appendix II: Positional Analysis of Indus Signs";                              Venue="In: Bryan K. Wells, The Archaeology and Epigraphy of Indus Writing. Archaeopress, Oxford, pp. 119-133"; Url=None }
    { CitationKey="Fuls2015c"; Author="Fuls, Andreas";   Year=2015; Title="Appendix III: Classifying Undeciphered Writing Systems";                       Venue="In: Bryan K. Wells, The Archaeology and Epigraphy of Indus Writing. Archaeopress, Oxford, pp. 134-140"; Url=None } ]

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
    for fn in signFunctions do
        exec
            ("INSERT INTO sign_function (source_code, function_code, label) VALUES ($s, $c, $l) " +
             "ON CONFLICT(source_code, function_code) DO UPDATE SET label=excluded.label")
            [ "$s", box sourceCode; "$c", box fn.Code; "$l", box fn.Label ]
        nFn <- nFn + 1

    let mutable nDir = 0
    for d in readingDirCodes do
        exec
            ("INSERT INTO reading_direction_code (source_code, code, label) VALUES ($s, $c, $l) " +
             "ON CONFLICT(source_code, code) DO UPDATE SET label=excluded.label")
            [ "$s", box sourceCode; "$c", box d.Code; "$l", box d.Label ]
        nDir <- nDir + 1

    let mutable nIns = 0
    for i in icitInscriptions do
        exec
            ("INSERT INTO icit_inscription (icit_id, source_code, site, artefact_type, text_class, excavation_idno, text_code) " +
             "VALUES ($id, $s, $site, $at, $cl, $idno, $tc) " +
             "ON CONFLICT(source_code, icit_id) DO UPDATE SET site=excluded.site, artefact_type=excluded.artefact_type, text_class=excluded.text_class, excavation_idno=excluded.excavation_idno, text_code=excluded.text_code")
            [ "$id",   box i.IcitId
              "$s",    box sourceCode
              "$site", box i.Site
              "$at",   box i.ArtefactType
              "$cl",   nullable i.TextClass
              "$idno", box i.ExcavationIdno
              "$tc",   box i.TextCode ]
        nIns <- nIns + 1

    let mutable nRole = 0
    for r in signRoles do
        exec
            ("INSERT INTO sign_role (source_code, sign_id, function_code, evidence) VALUES ($s, $sid, $f, $e) " +
             "ON CONFLICT(source_code, sign_id, function_code) DO UPDATE SET evidence=excluded.evidence")
            [ "$s", box sourceCode; "$sid", box r.SignId; "$f", box r.FunctionCode; "$e", box r.Evidence ]
        nRole <- nRole + 1

    let writePair (ctxLabel: string) (p: SignPairRow) =
        exec
            ("INSERT INTO sign_pair_frequency (source_code, sign_left, sign_right, frequency, context) VALUES ($s, $l, $r, $f, $ctx) " +
             "ON CONFLICT(source_code, sign_left, sign_right) DO UPDATE SET frequency=excluded.frequency, context=excluded.context")
            [ "$s", box sourceCode; "$l", box p.Left; "$r", box p.Right; "$f", box p.Frequency; "$ctx", box ctxLabel ]

    let mutable nPair = 0
    for p in tmkPairs do
        writePair "TMK-TMK adjacency, page 18 matrix" p
        nPair <- nPair + 1
    for p in sign700NumPairs do
        writePair "sign 700 → NUM right-adjacency, page 19 matrix" p
        nPair <- nPair + 1

    let mutable nClu = 0
    for c in signClusters do
        exec
            ("INSERT INTO sign_cluster (source_code, signs, frequency, contexts, evidence) VALUES ($s, $sg, $f, $cx, $e) " +
             "ON CONFLICT(source_code, signs) DO UPDATE SET frequency=excluded.frequency, contexts=excluded.contexts, evidence=excluded.evidence")
            [ "$s", box sourceCode; "$sg", box c.Signs; "$f", box c.Frequency; "$cx", box c.Contexts; "$e", box c.Evidence ]
        nClu <- nClu + 1

    let mutable nBib = 0
    for b in bibliography do
        exec
            ("INSERT INTO bibliography_reference (citation_key, source_code, author, year, title, venue, url) VALUES ($k, $s, $a, $y, $t, $v, $u) " +
             "ON CONFLICT(source_code, citation_key) DO UPDATE SET author=excluded.author, year=excluded.year, title=excluded.title, venue=excluded.venue, url=excluded.url")
            [ "$k", box b.CitationKey
              "$s", box sourceCode
              "$a", box b.Author
              "$y", box b.Year
              "$t", box b.Title
              "$v", box b.Venue
              "$u", nullable b.Url ]
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
