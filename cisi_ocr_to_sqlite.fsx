#!/usr/bin/env -S dotnet fsi
// SPDX-License-Identifier: BSD-2-Clause
// OCR Joshi & Parpola 1987 'Corpus of Indus Seals and Inscriptions, Vol. 1:
// Collections in India' (Helsinki, Suomalainen Tiedeakatemia, ASI MASI No. 86,
// AASF Series B No. 239, 862-page scanned PDF, ~/Downloads/Corpus of Indus
// Inscriptions.pdf) into indus_corpus.db.
//
// The PDF is fully image-based (Adobe Acrobat 8.0 Image Conversion Plug-in,
// 2011). Tesseract extracts the typeset metadata cleanly — page headers
// ("44 MOHENJO-DARO 134-138 SEALS 'unicorn' IV") and per-photo captions
// ("M-134 A", "M-134 a", "M-135 a bis"). Indus sign glyphs themselves are
// photographs of stone — Tesseract cannot read them. Sign-content extraction
// is a separate later phase (image segmentation + transcription).
//
// Pipeline (this script does only Phase 1, metadata OCR):
//   pdftoppm 300dpi → JPEG per page
//   tesseract --psm 6 → text per page
//   regex parse → cisi_page rows + cisi_artefact rows
//   UPSERT into db
//
// Usage:
//   dotnet fsi cisi_ocr_to_sqlite.fsx <pdf-path> <output-db> [first-page] [last-page]
//   dotnet fsi cisi_ocr_to_sqlite.fsx ~/Downloads/CISI.pdf indus_corpus.db 1 100
//
// Defaults: first-page=1, last-page=last-page-of-PDF.

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open System.IO
open System.Diagnostics
open System.Text.RegularExpressions
open Microsoft.Data.Sqlite

// === ARG PARSING =============================================================

let args = fsi.CommandLineArgs |> Array.tail

let pdfPath, dbPath, firstPage, lastPageOpt =
    match args with
    | [| pdf; db |]              -> pdf, db, 1, None
    | [| pdf; db; f |]           -> pdf, db, int f, None
    | [| pdf; db; f; l |]        -> pdf, db, int f, Some (int l)
    | _ ->
        eprintfn "Usage: dotnet fsi cisi_ocr_to_sqlite.fsx <pdf> <db> [first] [last]"
        exit 1

if not (File.Exists pdfPath) then
    eprintfn "ERROR: PDF not found: %s" pdfPath
    exit 1

if not (File.Exists dbPath) then
    eprintfn "ERROR: database not found: %s" dbPath
    exit 1

let sourceCode = "CISI_JP1987"

// === SCHEMA (additive) =======================================================

let migrations = [
    """
    CREATE TABLE IF NOT EXISTS cisi_page (
        source_code TEXT NOT NULL REFERENCES source(code),
        page_num    INTEGER NOT NULL,
        book_page   TEXT,
        site        TEXT,
        id_range    TEXT,
        raw_header  TEXT,
        PRIMARY KEY (source_code, page_num)
    ) STRICT;
    """
    """
    CREATE TABLE IF NOT EXISTS cisi_artefact (
        source_code  TEXT NOT NULL REFERENCES source(code),
        artefact_id  TEXT NOT NULL,
        side         TEXT NOT NULL,
        page_num     INTEGER NOT NULL,
        raw_caption  TEXT,
        PRIMARY KEY (source_code, artefact_id, side)
    ) STRICT;
    """ ]

// === SOURCE row ==============================================================

let sourceRow =
    {| Code = sourceCode
       Name = "Joshi & Parpola 1987 — Corpus of Indus Seals and Inscriptions Vol. 1: Collections in India"
       Year = 1987
       Note = "Helsinki: Suomalainen Tiedeakatemia. AASF Ser. B Vol. 239. ASI MASI No. 86. ISBN n/a. Photographic corpus, 862 pages, ingested via tesseract OCR of scanned PDF." |}

// === RENDER PHASE ============================================================
// pdftoppm produces /tmp/cisi-NNN.jpg

let workDir = Path.Combine(Path.GetTempPath(), "cisi_ocr_work")
Directory.CreateDirectory workDir |> ignore

let runProc (exe: string) (args: string seq) =
    let psi = ProcessStartInfo(exe)
    for a in args do psi.ArgumentList.Add a
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    let p = Process.Start psi
    let stdout = p.StandardOutput.ReadToEnd()
    let stderr = p.StandardError.ReadToEnd()
    p.WaitForExit()
    p.ExitCode, stdout, stderr

// Determine last page if not given.
let lastPage =
    match lastPageOpt with
    | Some n -> n
    | None ->
        let _, out, _ = runProc "pdfinfo" [pdfPath]
        let m = Regex.Match(out, @"Pages:\s+(\d+)")
        if m.Success then int m.Groups.[1].Value else 0

if lastPage <= 0 then
    eprintfn "ERROR: could not determine last page"
    exit 1

printfn "OCR pipeline: pages %d..%d of %s" firstPage lastPage pdfPath
printfn "  workdir: %s" workDir
printfn "  db:      %s" dbPath

// Render only pages we don't already have.
let pageImagePath p = Path.Combine(workDir, sprintf "p-%04d.jpg" p)

let pagesToRender =
    [ for p in firstPage .. lastPage do
        if not (File.Exists (pageImagePath p)) then yield p ]

if not (List.isEmpty pagesToRender) then
    printfn "  rendering %d pages..." (List.length pagesToRender)
    // pdftoppm renders a contiguous range; we'll do one big run for the whole range.
    let prefix = Path.Combine(workDir, "p")
    let code, _, err =
        runProc "pdftoppm"
            [ "-r"; "300"
              "-f"; string firstPage
              "-l"; string lastPage
              "-jpeg"
              pdfPath
              prefix ]
    if code <> 0 then
        eprintfn "pdftoppm failed: %s" err
        exit 1
    // pdftoppm output is p-0001.jpg style with at least 4-digit zero padding
    // when total pages > 999 — match by glob below.

// pdftoppm may produce 3-, 4-, or wider zero-padding depending on total pages.
let actualPageImage (p: int) : string option =
    [ "p-%03d.jpg"; "p-%04d.jpg"; "p-%05d.jpg" ]
    |> List.map (fun fmt -> Path.Combine(workDir, sprintf (Printf.StringFormat<int->string>(fmt)) p))
    |> List.tryFind File.Exists

// === OCR PHASE ===============================================================

let ocrPage (p: int) : string =
    let imgOpt = actualPageImage p
    match imgOpt with
    | None -> ""
    | Some imgPath ->
        let txtBase = Path.Combine(workDir, sprintf "p-%04d" p)
        let txtPath = txtBase + ".txt"
        if not (File.Exists txtPath) then
            let code, _, err = runProc "tesseract" [imgPath; txtBase; "-l"; "eng"; "--psm"; "6"]
            if code <> 0 then
                eprintfn "tesseract failed on page %d: %s" p err
        if File.Exists txtPath then File.ReadAllText txtPath else ""

// === PARSE PHASE =============================================================

// Page header forms seen in CISI Vol 1:
//   "44 MOHENJO-DARO 134-138 SEALS [icon] 'unicorn' IV"
//   "XXVI INTRODUCTION"
//   "150 LOTHAL 1-12 SEALS"
// Strategy: first whitespace-delimited token is the book page number (arabic
// or roman); next ALL-CAPS run is the site; if a numeric range "NNN-NNN"
// follows, capture it; the rest goes into raw_header.

let romanRe = Regex(@"^[IVXLCDM]+$")

type PageHeader = {
    BookPage : string option
    Site     : string option
    IdRange  : string option
    Raw      : string }

let parsePageHeader (text: string) : PageHeader =
    let firstNonEmpty =
        text.Split('\n')
        |> Array.map (fun l -> l.Trim())
        |> Array.tryFind (fun l -> l.Length > 0)
        |> Option.defaultValue ""
    let tokens = firstNonEmpty.Split([|' ';'\t'|], StringSplitOptions.RemoveEmptyEntries)
    let bookPage =
        if tokens.Length > 0 then
            let t0 = tokens.[0]
            if Regex.IsMatch(t0, @"^\d+$") || romanRe.IsMatch t0 then Some t0 else None
        else None
    let siteIdx = if Option.isSome bookPage then 1 else 0
    let isAllCaps (s: string) = s.Length >= 3 && Regex.IsMatch(s, @"^[A-Z][A-Z\-]+$")
    let site =
        if siteIdx < tokens.Length && isAllCaps tokens.[siteIdx] then
            // Glue together consecutive ALL-CAPS tokens (e.g., "MOHENJO-DARO" is one token,
            // but "TEPE YAHYA" or "RA'S AL-JUNAYZ" might be multiple).
            let rec collect i acc =
                if i < tokens.Length && (isAllCaps tokens.[i] || tokens.[i] = "AL") then
                    collect (i+1) (tokens.[i] :: acc)
                else acc
            let parts = collect siteIdx [] |> List.rev
            Some (String.concat " " parts)
        else None
    let idRange =
        let m = Regex.Match(firstNonEmpty, @"\b(\d+(?:[a-z])?-\d+(?:[a-z])?)\b")
        if m.Success then Some m.Value else None
    { BookPage = bookPage; Site = site; IdRange = idRange; Raw = firstNonEmpty }

// CISI artefact-caption forms:
//   "M-134 A"      Mohenjo-daro, primary side
//   "M-134 a"      Mohenjo-daro, impression / reverse
//   "M-135 a bis"  variant of side a
//   "H-001 A"      Harappa
//   "L-001 A"      Lothal
//   "K-001 A"      Kalibangan
//   "C-001 A"      Chanhujo-daro
//   "B-001 A"      Banawali
// Tesseract artefacts: occasional "M-136'8" instead of "M-136 a" — apostrophe
// noise. Treat "[ID-]NNN['/`][digit/letter]" forms as suspect and emit them
// with side="?".

// Allowed catalogue prefixes (Joshi-Parpola 1987 covers Indian collections;
// expand as we see them in headers).
let prefixes = "MHLKCBND" // Mohenjo-daro, Harappa, Lothal, Kalibangan, Chanhujo, Banawali, Nageshwar, Dholavira

let captionRe =
    Regex(sprintf @"\b([%s])-(\d{1,4})\s*([A-Za-z](?:\s*bis)?)?\b" prefixes)

type CaptionHit = {
    ArtefactId : string  // e.g., "M-134"
    Side       : string  // "A", "a", "a bis", or "?"
    Raw        : string }

let parseCaptions (text: string) : CaptionHit list =
    [ for m in captionRe.Matches text ->
        let pfx = m.Groups.[1].Value
        let num = int m.Groups.[2].Value
        let side =
            let s = m.Groups.[3].Value.Trim()
            if s = "" then "?" else s
        { ArtefactId = sprintf "%s-%d" pfx num
          Side       = side
          Raw        = m.Value } ]
    |> List.distinct

// === DB PHASE ================================================================

let conn = new SqliteConnection($"Data Source={dbPath}")
conn.Open()
let txn = conn.BeginTransaction()

try
    for sql in migrations do
        use cmd = conn.CreateCommand()
        cmd.Transaction <- txn
        cmd.CommandText <- sql
        cmd.ExecuteNonQuery() |> ignore

    // Source row UPSERT
    let cs = conn.CreateCommand()
    cs.Transaction <- txn
    cs.CommandText <-
        "INSERT INTO source (code, name, year, note) VALUES ($code, $name, $year, $note) " +
        "ON CONFLICT(code) DO UPDATE SET name=excluded.name, year=excluded.year, note=excluded.note"
    cs.Parameters.AddWithValue("$code", sourceRow.Code) |> ignore
    cs.Parameters.AddWithValue("$name", sourceRow.Name) |> ignore
    cs.Parameters.AddWithValue("$year", sourceRow.Year) |> ignore
    cs.Parameters.AddWithValue("$note", sourceRow.Note) |> ignore
    cs.ExecuteNonQuery() |> ignore

    let mutable nPages = 0
    let mutable nArtefacts = 0
    let mutable nUnknownSide = 0
    let mutable nNoSite = 0

    for p in firstPage .. lastPage do
        let text = ocrPage p
        if text.Length > 0 then
            let hdr = parsePageHeader text
            let caps = parseCaptions text

            // page row
            let cmdPage = conn.CreateCommand()
            cmdPage.Transaction <- txn
            cmdPage.CommandText <-
                "INSERT INTO cisi_page (source_code, page_num, book_page, site, id_range, raw_header) " +
                "VALUES ($src, $p, $bp, $site, $rng, $raw) " +
                "ON CONFLICT(source_code, page_num) DO UPDATE SET " +
                "book_page=excluded.book_page, site=excluded.site, id_range=excluded.id_range, raw_header=excluded.raw_header"
            cmdPage.Parameters.AddWithValue("$src",  sourceCode)                                                                |> ignore
            cmdPage.Parameters.AddWithValue("$p",    p)                                                                          |> ignore
            cmdPage.Parameters.AddWithValue("$bp",   (hdr.BookPage |> Option.map box |> Option.defaultValue (DBNull.Value :> obj)))   |> ignore
            cmdPage.Parameters.AddWithValue("$site", (hdr.Site     |> Option.map box |> Option.defaultValue (DBNull.Value :> obj)))   |> ignore
            cmdPage.Parameters.AddWithValue("$rng",  (hdr.IdRange  |> Option.map box |> Option.defaultValue (DBNull.Value :> obj)))   |> ignore
            cmdPage.Parameters.AddWithValue("$raw",  hdr.Raw)                                                                    |> ignore
            cmdPage.ExecuteNonQuery() |> ignore
            nPages <- nPages + 1
            if hdr.Site.IsNone then nNoSite <- nNoSite + 1

            // artefact rows
            for c in caps do
                let cmdA = conn.CreateCommand()
                cmdA.Transaction <- txn
                cmdA.CommandText <-
                    "INSERT INTO cisi_artefact (source_code, artefact_id, side, page_num, raw_caption) " +
                    "VALUES ($src, $aid, $side, $p, $raw) " +
                    "ON CONFLICT(source_code, artefact_id, side) DO UPDATE SET page_num=excluded.page_num, raw_caption=excluded.raw_caption"
                cmdA.Parameters.AddWithValue("$src",  sourceCode)   |> ignore
                cmdA.Parameters.AddWithValue("$aid",  c.ArtefactId) |> ignore
                cmdA.Parameters.AddWithValue("$side", c.Side)       |> ignore
                cmdA.Parameters.AddWithValue("$p",    p)            |> ignore
                cmdA.Parameters.AddWithValue("$raw",  c.Raw)        |> ignore
                cmdA.ExecuteNonQuery() |> ignore
                nArtefacts <- nArtefacts + 1
                if c.Side = "?" then nUnknownSide <- nUnknownSide + 1

        if p % 25 = 0 then
            printfn "  page %d: pages=%d artefacts=%d unknown_side=%d no_site=%d" p nPages nArtefacts nUnknownSide nNoSite

    txn.Commit()

    printfn ""
    printfn "DONE: pages %d..%d" firstPage lastPage
    printfn "  cisi_page    rows : %d" nPages
    printfn "  cisi_artefact rows: %d" nArtefacts
    printfn "  unknown side      : %d (artefacts where caption matched ID but side was lost)" nUnknownSide
    printfn "  pages w/o site    : %d (text-only pages — intro, indices, blank)" nNoSite
with
| ex ->
    txn.Rollback()
    eprintfn "FAIL: %s" ex.Message
    eprintfn "%s" ex.StackTrace
    exit 1
