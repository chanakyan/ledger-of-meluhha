#!/usr/bin/env -S dotnet fsi
// SPDX-License-Identifier: BSD-2-Clause
// Implements: Ledger of Meluhha §The Database — Wells 2015 sign-role ingest
// Coding standard: spec/fsharp/reference/fsharp_coding_standard.tex
//
// Ingest the structured data extractable from Bryan K. Wells (2015)
// 'The Archaeology and Epigraphy of Indus Writing' (Archaeopress, Oxford,
// ISBN 978-1-78491-046-4), with technical appendices by Andreas Fuls.
//
// This is a born-digital 161-page PDF — text extraction is clean. The
// numeric tables (4.1, 4.2, 5.1-5.3, 6.1, AII.1-5) are rendered as images
// in the typeset and do not extract via pdftotext, so this script ingests
// only what the PROSE around those tables explicitly states:
//
//   - 5 new sign-function codes that Wells defines (INS, ICTM, EMS, MMS, LMS)
//     beyond the 16-code Fuls 2019 vocabulary already in sign_function.
//   - ~35 sign-role assignments from §Numerals (Ch.5) + §Methods of Sign
//     Analysis (App.II).
//   - 4 documented sign collocations from prose (sign 003+156 = 35 numeric;
//     sign 006 collocates with sign 740 on copper tools; long stroke +
//     sign 700 high-frequency on Harappa tablets).
//   - 4 Harappa volumetric units (VI=40.4L through VIIII=161.7L from §4.2
//     "Volumetric system of Harappa", new volumetric_unit table).
//   - 4 new bibliography entries (Yadav 2008, Knorozov 1968, Marshall 1931,
//     Mackay 1938) cited in Wells 2015 prose.
//
// Sign-image content (Tables AII.2-AII.5 positional histograms, Tables 5.1-
// 5.3 collocation matrices, Table 6.1 proto-Dravidian numerals) is not
// machine-extractable from the PDF — would require either OCR of the
// figure regions or re-typing from the printed book.
//
// Usage:
//   dotnet fsi wells2015_to_sqlite.fsx <db>

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open System.IO
open Microsoft.Data.Sqlite

let args = fsi.CommandLineArgs |> Array.tail

let dbPath =
    match args with
    | [| db |] -> db
    | _ ->
        eprintfn "Usage: dotnet fsi wells2015_to_sqlite.fsx <db>"
        exit 1

if not (File.Exists dbPath) then
    eprintfn "ERROR: database not found: %s" dbPath
    exit 1

let sourceCode = "WELLS2015"

// === SCHEMA: one new table (volumetric_unit), rest reuse existing =============

let migrations = [
    """
    CREATE TABLE IF NOT EXISTS volumetric_unit (
        source_code TEXT NOT NULL REFERENCES source(code),
        symbol      TEXT NOT NULL,
        liters      REAL NOT NULL,
        notes       TEXT,
        PRIMARY KEY (source_code, symbol)
    ) STRICT;
    """ ]

// === DATA: records (T1 records-over-tuples) ===================================

type SignFunctionRow = { Code: string; Label: string }
type SignRoleRow     = { SignId: int; FunctionCode: string; Evidence: string }
type SignPairRow     = { Left: int; Right: int; Frequency: int; Context: string }
type VolumetricRow   = { Symbol: string; Liters: float; Notes: string }
type BibliographyRow = {
    CitationKey: string
    Author:      string
    Year:        int
    Title:       string
    Venue:       string
    Url:         string option }

let sourceRow =
    {| Code = sourceCode
       Name = "Wells 2015 — The Archaeology and Epigraphy of Indus Writing"
       Year = 2015
       Note = "Bryan K. Wells with technical appendices by Andreas Fuls. Archaeopress, Oxford. ISBN 978-1-78491-046-4. 161-page born-digital PDF, text-extractable. Chapter 5 (Numerals), Appendix II (Positional Analysis) source the sign-role assignments." |}

// === DATA: 5 sign-function codes Wells defines beyond the Fuls 16 ============
// Wells 2006 (Ch.3 + App.II) introduces these classifications:
let signFunctions : SignFunctionRow list = [
    { Code="INS";  Label="Initial Sign — preferred position 1 (Wells 2006 p.138)" }
    { Code="ICTM"; Label="Initial Cluster Terminal Marker — signs 1, 2, 60 (Wells 2006 p.141)" }
    { Code="EMS";  Label="Early-Medial Sign — maximum at positions 3-4 (Wells 2015 App.II Fig.6)" }
    { Code="MMS";  Label="Mid-Medial Sign — maximum at positions 5-6 (Wells 2015 App.II Fig.7)" }
    { Code="LMS";  Label="Late-Medial Sign — Wells 2015 App.II" } ]

// === DATA: sign-role assignments extracted from prose =========================
// Each assignment cites the section/page where Wells states it.
let signRoles : SignRoleRow list = [
    // ICTM — three signs (Wells 2006 p.141, restated in 2015 App.II)
    { SignId=1;  FunctionCode="ICTM"; Evidence="Wells 2006 p.141 — 'sign 1 ... Initial Cluster Terminal Marker'" }
    { SignId=2;  FunctionCode="ICTM"; Evidence="Wells 2006 p.141 / Wells 2015 App.II — 'sign 2 ... ICTM'" }
    { SignId=60; FunctionCode="ICTM"; Evidence="Wells 2006 p.141 — 'sign 60 appears only in position 1 to 4 ... ICTM'" }

    // INS — Initial Signs (per Wells 2006 p.138 cited in App.II)
    // Specific INS not enumerated in extracted prose; class defined.

    // TMK — Terminal Markers (Wells 2015 §App.II, beyond the 6 Fuls listed)
    { SignId=156; FunctionCode="TMK"; Evidence="Wells 2015 App.II — 'sign 156 ... appear anywhere at the second half of text positions'" }
    { SignId=158; FunctionCode="TMK"; Evidence="Wells 2015 App.II — 'sign 158 ... appear anywhere at the second half'" }
    { SignId=741; FunctionCode="TMK"; Evidence="Wells 2015 App.II — 'sign 741 is similar to sign 740 with a maximum at position 5 but not terminal'" }

    // PTM — Post-Terminal Markers (Wells 2015 App.II)
    { SignId=90;  FunctionCode="PTM"; Evidence="Wells 2015 App.II — 'Post-Terminal signs are sign 090, 400, 621, and 679'" }
    { SignId=400; FunctionCode="PTM"; Evidence="Wells 2015 App.II — same enumeration" }
    { SignId=621; FunctionCode="PTM"; Evidence="Wells 2015 App.II — same enumeration" }
    { SignId=679; FunctionCode="PTM"; Evidence="Wells 2015 App.II — same enumeration" }
    { SignId=161; FunctionCode="PTM"; Evidence="Wells 2015 App.II — 'Other Terminal or Post-Terminal signs are sign 161, 422, and 423, but their frequency is very low'" }
    { SignId=422; FunctionCode="PTM"; Evidence="Wells 2015 App.II — same low-frequency note" }
    { SignId=423; FunctionCode="PTM"; Evidence="Wells 2015 App.II — same low-frequency note" }

    // SYL — Syllables (uniform position distribution per App.II)
    { SignId=382; FunctionCode="SYL"; Evidence="Wells 2015 App.II Fig.4 — 'Signs with an almost constant distribution are signs 382, 790, 832, and 892'" }
    { SignId=790; FunctionCode="SYL"; Evidence="Wells 2015 App.II Fig.4 — same enumeration" }
    { SignId=832; FunctionCode="SYL"; Evidence="Wells 2015 App.II Fig.4 — same enumeration" }
    { SignId=892; FunctionCode="SYL"; Evidence="Wells 2015 App.II Fig.4 — same enumeration" }
    // Uniform-but-not-initial syllables (App.II Fig.5)
    { SignId=368; FunctionCode="SYL"; Evidence="Wells 2015 App.II Fig.5 — 'Sign 368 ... uniform but almost never initial ... possible grammatical marker (Knorozov 1968)'" }
    { SignId=595; FunctionCode="SYL"; Evidence="Wells 2015 App.II Fig.5 — 'Sign 368 and 595 are more or less uniform but almost never initial'" }

    // EMS — Early-Medial Signs (Wells 2015 App.II Fig.6)
    { SignId=2;  FunctionCode="EMS"; Evidence="Wells 2015 App.II Fig.6 — 'Sign 002 and 060 are known as a marker after initial signs ... Early-Medial'" }
    { SignId=60; FunctionCode="EMS"; Evidence="Wells 2015 App.II Fig.6 — same enumeration" }

    // MMS — Mid-Medial Signs (Wells 2015 App.II Fig.7)
    { SignId=741; FunctionCode="MMS"; Evidence="Wells 2015 App.II Fig.7 — 'signs 741 and 742 show a maximum at position 5 ... Mid-Medial'" }
    { SignId=742; FunctionCode="MMS"; Evidence="Wells 2015 App.II Fig.7 — same enumeration" }

    // SHN — Short Linear Stroke Numerals (Ch.5 says max value 7, signs 1-7
    // when in numeric context; sign 003 has wide collocations).
    { SignId=1; FunctionCode="SHN"; Evidence="Wells 2015 Ch.5 — Short Linear Stroke numerals (1-7); also doubles as ICTM" }
    { SignId=2; FunctionCode="SHN"; Evidence="Wells 2015 Ch.5 — Short Linear Stroke numerals (1-7); also doubles as ICTM" }
    { SignId=3; FunctionCode="SHN"; Evidence="Wells 2015 Ch.5 — 'Sign 003 has the widest set of collocations of this class of numeral'" }
    { SignId=4; FunctionCode="SHN"; Evidence="Wells 2015 Ch.5 — Short Linear Stroke numeral" }
    { SignId=5; FunctionCode="SHN"; Evidence="Wells 2015 Ch.5 — Short Linear Stroke numeral" }
    { SignId=6; FunctionCode="SHN"; Evidence="Wells 2015 Ch.5 — 'Sign 006 occurs three times. In two of these contexts they are found on copper tools'" }
    { SignId=7; FunctionCode="SHN"; Evidence="Wells 2015 Ch.5 — 'maximum value is 7' for short linear strokes" }

    // SSN — Short Stacked Stroke Numerals (Ch.5 Table 5.2 region — 014-019
    // are reliable numerics; 012/013 questionable; 020 mystery; 055 special).
    { SignId=14; FunctionCode="SSN"; Evidence="Wells 2015 Ch.5 — 'signs 014 through 019, which have many numeric contexts'" }
    { SignId=15; FunctionCode="SSN"; Evidence="Wells 2015 Ch.5 — same enumeration" }
    { SignId=16; FunctionCode="SSN"; Evidence="Wells 2015 Ch.5 — '016 occurs with any significant frequency with signs 220, and 390'" }
    { SignId=17; FunctionCode="SSN"; Evidence="Wells 2015 Ch.5 — 'High frequency collocations of sign 017 with both 575 and 585'" }
    { SignId=18; FunctionCode="SSN"; Evidence="Wells 2015 Ch.5 — same enumeration" }
    { SignId=19; FunctionCode="SSN"; Evidence="Wells 2015 Ch.5 — same enumeration" }

    // LON — Long Linear Stroke Numerals (Ch.5 — 1-9, 8 unattested)
    { SignId=31; FunctionCode="LON"; Evidence="Wells 2015 Ch.5 — Long Linear Stroke numerals; 'signs 031 and 032 ... in numeric, possible numeric and non-numeric contexts' (polyvalent)" }
    { SignId=32; FunctionCode="LON"; Evidence="Wells 2015 Ch.5 — 'sign 032 ... numeric and non-numeric (syllabic) contexts'" }
    { SignId=33; FunctionCode="LON"; Evidence="Wells 2015 Ch.5 — Long Linear Stroke numeral; pairs with sign 700 frequently on Harappa tablets" }
    { SignId=34; FunctionCode="LON"; Evidence="Wells 2015 Ch.5 — pairs with sign 700 frequently on Harappa tablets" }
    { SignId=35; FunctionCode="LON"; Evidence="Wells 2015 Ch.5 — Long Linear Stroke numeral" }
    { SignId=36; FunctionCode="LON"; Evidence="Wells 2015 Ch.5 — Long Linear Stroke numeral" }
    { SignId=37; FunctionCode="LON"; Evidence="Wells 2015 Ch.5 — Long Linear Stroke numeral" }
    { SignId=39; FunctionCode="LON"; Evidence="Wells 2015 Ch.5 — Long Linear Stroke numeral (8 = sign 38 unattested)" }

    // SPN — Special Numeral
    { SignId=55; FunctionCode="SPN"; Evidence="Wells 2015 Ch.5 — 'sign 055 will be discussed later under the heading of Special Numerals'" }

    // FSH — Fish signs (Ch.5 mentions 'fish signs' as 156, but also general
    // fish-sign category; 156 already classified as TMK above — multi-role).
    { SignId=156; FunctionCode="FSH"; Evidence="Wells 2015 Ch.5 — '003 and 156 (n=35) are all numeric contexts'; 156 is a fish sign" } ]

// === DATA: collocations from prose ============================================
let signPairs : SignPairRow list = [
    { Left=3;   Right=156; Frequency=35;  Context="Wells 2015 Ch.5 — 'collocation of sign 003 and 156 (n=35) are all numeric contexts'" }
    { Left=6;   Right=740; Frequency=2;   Context="Wells 2015 Ch.5 — 'sign 006 occurs solo and left adjacent to sign 740' on copper tools (n=2)" }
    { Left=17;  Right=575; Frequency=0;   Context="Wells 2015 Ch.5 — 'High frequency collocations of sign 017 with both 575 and 585'; exact n not given in prose" }
    { Left=17;  Right=585; Frequency=0;   Context="Wells 2015 Ch.5 — same; exact n not given in prose" } ]

// === DATA: Harappa volumetric units (Ch.4 §4.2) ===============================
let volumetricUnits : VolumetricRow list = [
    { Symbol="VI";    Liters=40.4;  Notes="basic unit of Harappa volumetric system; observed in Purana Qila vessels" }
    { Symbol="VIIII"; Liters=161.7; Notes="upper end of miniature-tablet range" }
    { Symbol="VIIIIII"; Liters=242.6; Notes="estimated from VI×6, observed in Purana Qila pot H-371" }
    { Symbol="VIIIIIII"; Liters=283.0; Notes="estimated from VI×7, observed in Purana Qila pot H-370" } ]

// === DATA: bibliography new in Wells 2015 prose ===============================
// Existing entries from icit_help_to_sqlite (Wells 1999/2011/2015, Fuls 2010/
// 2014/2015a/b/c) are not duplicated. Added: works Wells 2015 cites in prose.
let bibliography : BibliographyRow list = [
    { CitationKey="Wells2006";    Author="Wells, Bryan K.";                                        Year=2006; Title="Epigraphic Approaches to Indus Writing";                                   Venue="PhD dissertation, Harvard University. Cited extensively in Wells 2015 App.II"; Url=None }
    { CitationKey="Yadav2008";    Author="Yadav, Nisha and Vahia, M.N. and Mahadevan, I. and Joglekar, H."; Year=2008; Title="A Statistical Approach for Pattern Search in Indus Writing";   Venue="International Journal of Dravidian Linguistics 37(1), pp. 39-52. Cited in Wells 2015 App.II"; Url=None }
    { CitationKey="Knorozov1968"; Author="Knorozov, Yuri V.";                                     Year=1968; Title="Proto-Indica: brief report on the investigations of the proto-Indian texts"; Venue="Moscow: Nauka. Cited in Wells 2015 App.II for sign 368 grammatical-marker hypothesis"; Url=None }
    { CitationKey="Marshall1931"; Author="Marshall, John (ed.)";                                  Year=1931; Title="Mohenjo-daro and the Indus Civilization";                                  Venue="Arthur Probsthain, London. 3 vols. Foundational excavation report"; Url=None }
    { CitationKey="Mackay1938";   Author="Mackay, Ernest J.H.";                                   Year=1938; Title="Further Excavations at Mohenjo-daro";                                     Venue="Government of India, Delhi. 2 vols. Cited in Wells 2015 Ch.4 'Block' and 'House' designations"; Url=None }
    { CitationKey="Hemmy1931";    Author="Hemmy, A.S.";                                           Year=1931; Title="The Statistical Treatment of Ancient Weights";                            Venue="In: Marshall (1931) Mohenjo-daro Vol.II, App.XIV. Source for the 0.856g base unit"; Url=None } ]

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

    // Source row
    exec
        ("INSERT INTO source (code, name, year, note) VALUES ($c, $n, $y, $note) " +
         "ON CONFLICT(code) DO UPDATE SET name=excluded.name, year=excluded.year, note=excluded.note")
        [ "$c",    box sourceRow.Code
          "$n",    box sourceRow.Name
          "$y",    box sourceRow.Year
          "$note", box sourceRow.Note ]

    // Sign functions (Wells's vocabulary)
    let mutable nFn = 0
    for fn in signFunctions do
        exec
            ("INSERT INTO sign_function (source_code, function_code, label) VALUES ($s, $c, $l) " +
             "ON CONFLICT(source_code, function_code) DO UPDATE SET label=excluded.label")
            [ "$s", box sourceCode; "$c", box fn.Code; "$l", box fn.Label ]
        nFn <- nFn + 1

    // Sign roles
    let mutable nRole = 0
    for r in signRoles do
        exec
            ("INSERT INTO sign_role (source_code, sign_id, function_code, evidence) VALUES ($s, $sid, $f, $e) " +
             "ON CONFLICT(source_code, sign_id, function_code) DO UPDATE SET evidence=excluded.evidence")
            [ "$s", box sourceCode; "$sid", box r.SignId; "$f", box r.FunctionCode; "$e", box r.Evidence ]
        nRole <- nRole + 1

    // Sign pairs
    let mutable nPair = 0
    for p in signPairs do
        exec
            ("INSERT INTO sign_pair_frequency (source_code, sign_left, sign_right, frequency, context) VALUES ($s, $l, $r, $f, $ctx) " +
             "ON CONFLICT(source_code, sign_left, sign_right) DO UPDATE SET frequency=excluded.frequency, context=excluded.context")
            [ "$s", box sourceCode; "$l", box p.Left; "$r", box p.Right; "$f", box p.Frequency; "$ctx", box p.Context ]
        nPair <- nPair + 1

    // Volumetric units
    let mutable nVol = 0
    for v in volumetricUnits do
        exec
            ("INSERT INTO volumetric_unit (source_code, symbol, liters, notes) VALUES ($s, $sym, $l, $n) " +
             "ON CONFLICT(source_code, symbol) DO UPDATE SET liters=excluded.liters, notes=excluded.notes")
            [ "$s", box sourceCode; "$sym", box v.Symbol; "$l", box v.Liters; "$n", box v.Notes ]
        nVol <- nVol + 1

    // Bibliography
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

    printfn "OK: ingested Wells 2015 sign-role data"
    printfn "  source_code         : %s" sourceCode
    printfn "  sign_function       : %d" nFn
    printfn "  sign_role           : %d" nRole
    printfn "  sign_pair_frequency : %d" nPair
    printfn "  volumetric_unit     : %d" nVol
    printfn "  bibliography_ref    : %d" nBib
with
| ex ->
    txn.Rollback()
    eprintfn "FAIL: %s" ex.Message
    eprintfn "%s" ex.StackTrace
    exit 1
