#!/usr/bin/env -S dotnet fsi
// SPDX-License-Identifier: BSD-2-Clause
// Ingest CISI corpus JSON from the mayig digitisation into indus_corpus.db.
//
// Source: mayig/indus-valley-script-corpus (forked to chanakyan/)
//   corpus/   — 179 Mohenjo-daro inscription JSONs (M-1A through M-184A)
//   features/ — 396 Parpola sign definitions with concordance mappings
//
// This script reads the JSON structure, not the repo name. If the upstream
// changes hosting, only the path argument changes. The data shape is stable.
//
// Usage:
//   dotnet fsi indus_ingest_cisi.fsx <corpus-dir> <output-db>
//   dotnet fsi indus_ingest_cisi.fsx ~/indus-valley-script-corpus indus_corpus.db
//
// Tables populated:
//   source            — CISI_MAYIG row
//   base_sign         — 396 Parpola signs with descriptions
//   sign_concordance  — Parpola -> Mahadevan/Wells mapping
//   cisi_inscription  — 179 inscription records with sign sequences
//   sign_occurrence   — per-position sign occurrences for each inscription

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open System.IO
open System.Text.Json
open Microsoft.Data.Sqlite

// === ARGUMENT PARSING ========================================================

let args = fsi.CommandLineArgs |> Array.tail

let corpusDir, dbPath =
    match args with
    | [| dir; db |] -> dir, db
    | _ ->
        eprintfn "Usage: dotnet fsi indus_ingest_cisi.fsx <corpus-dir> <output-db>"
        exit 1

let corpusJsonDir = Path.Combine(corpusDir, "corpus")
let featuresDir = Path.Combine(corpusDir, "features")

if not (Directory.Exists corpusJsonDir) then
    eprintfn "ERROR: corpus dir not found: %s" corpusJsonDir
    exit 1

if not (Directory.Exists featuresDir) then
    eprintfn "ERROR: features dir not found: %s" featuresDir
    exit 1

// === TYPES ===================================================================

type Grapheme = { Id: string; Features: int array }

type Inscription = {
    Id: string
    Description: string
    Graphemes: Grapheme array }

type SignDef = {
    Id: string
    Description: string
    ParpolaGraphemes: string array
    WellsGraphemes: string array
    MahadevanGraphemes: string array }

// === JSON PARSING (pure) =====================================================

let parseInscriptionFile (path: string) : Inscription array =
    let json = File.ReadAllText(path)
    let doc = JsonDocument.Parse(json)
    doc.RootElement.EnumerateArray()
    |> Seq.map (fun el ->
        let graphemes =
            el.GetProperty("graphemes").EnumerateArray()
            |> Seq.map (fun g ->
                let features =
                    g.GetProperty("features").EnumerateArray()
                    |> Seq.map (fun f -> f.GetInt32())
                    |> Seq.toArray
                { Id = g.GetProperty("id").GetString()
                  Features = features })
            |> Seq.toArray
        { Id = el.GetProperty("id").GetString()
          Description = el.GetProperty("description").GetString()
          Graphemes = graphemes })
    |> Seq.toArray

let parseSignDef (path: string) : SignDef option =
    let json = File.ReadAllText(path)
    let doc = JsonDocument.Parse(json)
    let root = doc.RootElement
    let id = root.GetProperty("id").GetString()
    // Skip default_features — not a real sign
    if id = "default_features" then None
    else
        let getStringArray (prop: string) =
            if root.TryGetProperty(prop) |> fst then
                root.GetProperty(prop).EnumerateArray()
                |> Seq.map (fun e -> e.GetString())
                |> Seq.toArray
            else [||]
        Some {
            Id = id
            Description = root.GetProperty("description").GetString()
            ParpolaGraphemes = getStringArray "parpola_graphemes"
            WellsGraphemes = getStringArray "wells_graphemes"
            MahadevanGraphemes = getStringArray "mahadevan_graphemes" }

// === READ ALL DATA (pure) ====================================================

printfn "Reading CISI corpus from %s ..." corpusDir

let allInscriptions =
    Directory.GetFiles(corpusJsonDir, "*.json", SearchOption.AllDirectories)
    |> Array.collect parseInscriptionFile
    |> Array.sortBy (fun i -> i.Id)

printfn "  Parsed %d inscriptions from %d JSON files"
    allInscriptions.Length
    (Directory.GetFiles(corpusJsonDir, "*.json", SearchOption.AllDirectories).Length)

let allSignDefs =
    Directory.GetFiles(featuresDir, "P*.json")
    |> Array.choose parseSignDef
    |> Array.sortBy (fun s -> s.Id)

printfn "  Parsed %d sign definitions from features/"
    allSignDefs.Length

// === STATISTICS (pure) =======================================================

let totalGraphemes = allInscriptions |> Array.sumBy (fun i -> i.Graphemes.Length)
let uniqueSigns = allInscriptions |> Array.collect (fun i -> i.Graphemes |> Array.map (fun g -> g.Id)) |> Array.distinct
let mapped = allSignDefs |> Array.filter (fun s -> s.MahadevanGraphemes.Length > 0)

printfn "  Total grapheme occurrences: %d" totalGraphemes
printfn "  Unique Parpola signs used:  %d" uniqueSigns.Length
printfn "  Signs with Mahadevan mapping: %d / %d" mapped.Length allSignDefs.Length

// === DB WRITE (boundary) =====================================================

if not (File.Exists dbPath) then
    eprintfn "ERROR: database not found: %s (run indus_ingest_corpus.fsx first)" dbPath
    exit 1

use con = new SqliteConnection(sprintf "Data Source=%s" dbPath)
con.Open()

let tx = con.BeginTransaction()

let exec (sql: string) =
    use cmd = new SqliteCommand(sql, con, tx)
    cmd.ExecuteNonQuery() |> ignore

// --- source row ---
exec "DELETE FROM source WHERE code = 'CISI_MAYIG'"

let insertSource () =
    use cmd = new SqliteCommand(
        "INSERT OR REPLACE INTO source VALUES (@c,@n,@y,@t)", con, tx)
    cmd.Parameters.AddWithValue("@c", "CISI_MAYIG") |> ignore
    cmd.Parameters.AddWithValue("@n",
        "CISI corpus digitisation (mayig/indus-valley-script-corpus)") |> ignore
    cmd.Parameters.AddWithValue("@y", 2024) |> ignore
    cmd.Parameters.AddWithValue("@t",
        sprintf "%d inscriptions, %d sign defs, %d with Mahadevan mapping. Mohenjo-daro M-series."
            allInscriptions.Length allSignDefs.Length mapped.Length) |> ignore
    cmd.ExecuteNonQuery() |> ignore

insertSource ()
printfn "  source: CISI_MAYIG inserted"

// --- sign definitions -> base_sign ---
let mutable signCount = 0
for s in allSignDefs do
    use cmd = new SqliteCommand(
        "INSERT OR IGNORE INTO base_sign VALUES (@s,@i,@l,0,0,0)", con, tx)
    cmd.Parameters.AddWithValue("@s", "CISI_MAYIG") |> ignore
    cmd.Parameters.AddWithValue("@i", s.Id) |> ignore
    cmd.Parameters.AddWithValue("@l", s.Description) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    signCount <- signCount + 1
printfn "  base_sign (CISI_MAYIG): %d rows" signCount

// --- concordance -> sign_concordance ---
let mutable concCount = 0
for s in allSignDefs do
    let mahadevan =
        if s.MahadevanGraphemes.Length > 0 then
            String.Join(",", s.MahadevanGraphemes)
        else null
    let wells =
        if s.WellsGraphemes.Length > 0 then
            String.Join(",", s.WellsGraphemes)
        else null
    use cmd = new SqliteCommand(
        "INSERT OR REPLACE INTO sign_concordance VALUES (@p,@d,@m,@w)", con, tx)
    cmd.Parameters.AddWithValue("@p", s.Id) |> ignore
    cmd.Parameters.AddWithValue("@d",
        if isNull s.Description then box DBNull.Value
        else box s.Description) |> ignore
    cmd.Parameters.AddWithValue("@m",
        if isNull mahadevan then box DBNull.Value
        else box mahadevan) |> ignore
    cmd.Parameters.AddWithValue("@w",
        if isNull wells then box DBNull.Value
        else box wells) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    concCount <- concCount + 1
printfn "  sign_concordance: %d rows" concCount

// --- inscriptions -> cisi_inscription ---
let mutable inscCount = 0
for ins in allInscriptions do
    let signSeq = ins.Graphemes |> Array.map (fun g -> g.Id) |> String.concat ","
    use cmd = new SqliteCommand(
        "INSERT OR REPLACE INTO cisi_inscription VALUES (@i,@d,@s,@c,'CISI_MAYIG')", con, tx)
    cmd.Parameters.AddWithValue("@i", ins.Id) |> ignore
    cmd.Parameters.AddWithValue("@d", ins.Description) |> ignore
    cmd.Parameters.AddWithValue("@s", signSeq) |> ignore
    cmd.Parameters.AddWithValue("@c", ins.Graphemes.Length) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    inscCount <- inscCount + 1
printfn "  cisi_inscription: %d rows" inscCount

// --- per-position occurrences -> sign_occurrence ---
let mutable occCount = 0
for ins in allInscriptions do
    ins.Graphemes |> Array.iteri (fun pos g ->
        use cmd = new SqliteCommand(
            "INSERT OR IGNORE INTO sign_occurrence (artefact_id, source_code, sign_id, position)
             VALUES (@a,@s,@i,@p)", con, tx)
        cmd.Parameters.AddWithValue("@a", ins.Id) |> ignore
        cmd.Parameters.AddWithValue("@s", "CISI_MAYIG") |> ignore
        cmd.Parameters.AddWithValue("@i", g.Id) |> ignore
        cmd.Parameters.AddWithValue("@p", pos) |> ignore
        cmd.ExecuteNonQuery() |> ignore
        occCount <- occCount + 1)
printfn "  sign_occurrence: %d rows" occCount

tx.Commit()

// === VERIFY ==================================================================

let count (table: string) (where: string) =
    use cmd = new SqliteCommand(
        sprintf "SELECT COUNT(*) FROM %s WHERE %s" table where, con)
    cmd.ExecuteScalar() :?> int64

printfn ""
printfn "CISI INGEST COMPLETE: %s" dbPath
printfn "  base_sign (CISI_MAYIG)     : %d" (count "base_sign" "source_code='CISI_MAYIG'")
printfn "  sign_concordance           : %d" (count "sign_concordance" "1=1")
printfn "  cisi_inscription (CISI_MAYIG): %d" (count "cisi_inscription" "source_code='CISI_MAYIG'")
printfn "  sign_occurrence (CISI_MAYIG): %d" (count "sign_occurrence" "source_code='CISI_MAYIG'")

// --- spot check: M-52A (the Mesopotamia seal from the paper) ---
printfn ""
printfn "Spot check — M-52A:"
use spot = new SqliteCommand(
    "SELECT signs, sign_count FROM cisi_inscription WHERE id='M-52A'", con)
use rdr = spot.ExecuteReader()
if rdr.Read() then
    printfn "  signs: %s" (rdr.GetString 0)
    printfn "  count: %d" (rdr.GetInt32 1)
    // Check concordance for P324 -> M342 (jar goods)
    use conc = new SqliteCommand(
        "SELECT mahadevan_ids FROM sign_concordance WHERE parpola_id='P324'", con)
    let mah = conc.ExecuteScalar()
    if not (isNull mah) then
        printfn "  P324 -> %s (expected: M342 = jar goods)" (string mah)
    // Check P050 -> M059 (fish = Mesopotamia route)
    use conc2 = new SqliteCommand(
        "SELECT mahadevan_ids FROM sign_concordance WHERE parpola_id='P050'", con)
    let mah2 = conc2.ExecuteScalar()
    if not (isNull mah2) then
        printfn "  P050 -> %s (expected: M059 = Mesopotamia route)" (string mah2)
else
    printfn "  WARNING: M-52A not found in cisi_inscription"

con.Close()
printfn ""
printfn "Done."
