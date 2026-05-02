#!/usr/bin/env -S dotnet fsi
// SPDX-License-Identifier: BSD-2-Clause
// Post-process the raw cisi_page / cisi_artefact rows produced by
// cisi_ocr_to_sqlite.fsx. Two passes:
//   1. Normalize site OCR typos (MOHENIJO-DARO → MOHENJO-DARO, HARAPPAS →
//      HARAPPA, HULASI → HULAS, "GRAFFITI KALIBANGAN"/"SEALS HARAPPA" →
//      strip leading category words).
//   2. Drop cisi_artefact rows whose page_num maps to a cisi_page with
//      site IS NULL or site NOT IN canonical-Indus-site list. These are
//      phantom captures from the introduction and indices where number
//      sentences like "Mohenjo-daro: 540 seals" matched the M-NNN regex.
//
// Idempotent. Safe to re-run.
//
// Usage:
//   dotnet fsi cisi_ocr_cleanup_to_sqlite.fsx <db>

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open System.IO
open Microsoft.Data.Sqlite

let args = fsi.CommandLineArgs |> Array.tail

let dbPath =
    match args with
    | [| db |] -> db
    | _ ->
        eprintfn "Usage: dotnet fsi cisi_ocr_cleanup_to_sqlite.fsx <db>"
        exit 1

if not (File.Exists dbPath) then
    eprintfn "ERROR: database not found: %s" dbPath
    exit 1

let sourceCode = "CISI_JP1987"

// Canonical Indus site names appearing as page-section headers in
// Joshi-Parpola Vol.1.
let canonicalSites = Set.ofList [
    "MOHENJO-DARO"; "HARAPPA"; "LOTHAL"; "KALIBANGAN"; "CHANHUJO-DARO";
    "BANAWALI"; "DAIMABAD"; "SURKOTADA"; "PRABHAS PATAN"; "KHIRSARA";
    "HULAS"; "ALAMGIRPUR"; "KOT-DIJI"; "AMRI"; "BARA"; "BHAGATRAV";
    "BHIRRANA"; "DESALPUR"; "DHOLAVIRA"; "FARMANA"; "KANMER"; "NAUSHARO";
    "NINDOWARI"; "PIRAK"; "RAKHIGARHI"; "RUPAR"; "TARKHANEWALA-DERA";
    "RANGPUR" ]

// Site name normalizations from observed OCR errors. Empty replacement
// means strip the row (set site to NULL).
let siteRewrites : (string * string) list = [
    "MOHENIJO-DARO",       "MOHENJO-DARO"
    "MOHENJOO-DARO",       "MOHENJO-DARO"
    "MOHENJODARO",         "MOHENJO-DARO"
    "MOHENIJODARO",        "MOHENJO-DARO"
    "HARAPPAS",            "HARAPPA"
    "HARAPPAY",            "HARAPPA"
    "HARAPPAI",            "HARAPPA"
    "HARAPPAS SEALS",      "HARAPPA"
    "HARAPPAY SEALS",      "HARAPPA"
    "HARAPPAI SEALS",      "HARAPPA"
    "HULASI",              "HULAS"
    "GRAFFITI KALIBANGAN", "KALIBANGAN"
    "GRAFFITI HARAPPA",    "HARAPPA"
    "GRAFFITI AMRI",       "AMRI"
    "GRAFFITI MOHENJO-DARO","MOHENJO-DARO"
    "SEALS KALIBANGAN",    "KALIBANGAN"
    "SEALS HARAPPA",       "HARAPPA"
    "SEALS PRABHAS PATAN", "PRABHAS PATAN"
    "SEALS KHIRSARA",      "KHIRSARA"
    "SEALS MOHENJO-DARO",  "MOHENJO-DARO"
    "TABLETS HARAPPA",     "HARAPPA"
    "TABLETS MOHENJO-DARO","MOHENJO-DARO"
    "TABLETS",             ""
    "TABLE",               ""
    "DATA",                "" ]

// Known non-content site values — drop artefact rows from these pages
// (front matter, indices, plates without inscription captions).
let nonContentSites = Set.ofList [
    "INTRODUCTION"; "PREFACE"; "CONTENTS"; "COLOUR PHOTOGRAPHS";
    "SUOMALAISEN TIEDEAKATEMIAN TOIMITUKSIA"; "ADDENDA";
    "PREFACE VII"; "INTRODUCTION XXI"; "INTRODUCTION XIII";
    "INTRODUCTION XVH"; "INTRODUCTION XXXI" ]

let conn = new SqliteConnection($"Data Source={dbPath}")
conn.Open()
let txn = conn.BeginTransaction()

let exec (sql: string) (kvs: (string * obj) list) : int =
    use cmd = conn.CreateCommand()
    cmd.Transaction <- txn
    cmd.CommandText <- sql
    for (k, v) in kvs do cmd.Parameters.AddWithValue(k, v) |> ignore
    cmd.ExecuteNonQuery()

try
    // Pass 1: site normalization
    let mutable nNormalized = 0
    for (bad, good) in siteRewrites do
        if good = "" then
            let n = exec
                        "UPDATE cisi_page SET site=NULL WHERE source_code=$src AND site=$bad"
                        [ "$src", box sourceCode; "$bad", box bad ]
            nNormalized <- nNormalized + n
        else
            let n = exec
                        "UPDATE cisi_page SET site=$good WHERE source_code=$src AND site=$bad"
                        [ "$src", box sourceCode; "$bad", box bad; "$good", box good ]
            nNormalized <- nNormalized + n

    // Pass 2: phantom rejection. A cisi_artefact row is a phantom if its
    // page's site is NULL or not in canonical list. Add an `is_phantom`
    // column rather than DELETE — keeps audit trail; queries can filter.
    // Add column if missing (migration step).
    try
        exec "ALTER TABLE cisi_artefact ADD COLUMN is_phantom INTEGER NOT NULL DEFAULT 0" [] |> ignore
    with _ -> ()  // column already exists

    let canonicalSql =
        canonicalSites
        |> Seq.map (fun s -> "'" + s.Replace("'", "''") + "'")
        |> String.concat ","

    let sqlMarkPhantom =
        sprintf """
        UPDATE cisi_artefact
        SET is_phantom = CASE
            WHEN page_num IN (
                SELECT page_num FROM cisi_page
                WHERE source_code = $src
                  AND (site IS NULL OR site NOT IN (%s))
            ) THEN 1
            ELSE 0
        END
        WHERE source_code = $src
        """ canonicalSql
    let nMarked = exec sqlMarkPhantom [ "$src", box sourceCode ]

    txn.Commit()

    // Report
    let report (sql: string) (label: string) =
        use cmd = conn.CreateCommand()
        cmd.CommandText <- sql
        cmd.Parameters.AddWithValue("$src", box sourceCode) |> ignore
        let v = cmd.ExecuteScalar() :?> int64
        printfn "  %-40s : %d" label v

    printfn "OK: cisi_ocr_cleanup"
    printfn "  site rows normalized                     : %d" nNormalized
    printfn "  cisi_artefact rows touched by phantom-mark: %d" nMarked
    printfn ""
    printfn "post-cleanup state:"
    report
        "SELECT COUNT(*) FROM cisi_page WHERE source_code = $src AND site IS NOT NULL"
        "pages with site"
    let canonicalInClause =
        canonicalSites
        |> Seq.map (fun s -> "'" + s + "'")
        |> String.concat ","
    let canonicalPagesSql =
        sprintf "SELECT COUNT(*) FROM cisi_page WHERE source_code = $src AND site IN (%s)" canonicalInClause
    report canonicalPagesSql "pages with canonical site"
    report
        "SELECT COUNT(*) FROM cisi_artefact WHERE source_code = $src AND is_phantom = 0"
        "non-phantom cisi_artefact rows"
    report
        "SELECT COUNT(DISTINCT artefact_id) FROM cisi_artefact WHERE source_code = $src AND is_phantom = 0"
        "non-phantom unique artefact_ids"
    report
        "SELECT COUNT(*) FROM cisi_artefact WHERE source_code = $src AND is_phantom = 1"
        "phantom rows (kept for audit)"

with
| ex ->
    txn.Rollback()
    eprintfn "FAIL: %s" ex.Message
    eprintfn "%s" ex.StackTrace
    exit 1
