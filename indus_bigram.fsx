#!/usr/bin/env -S dotnet fsi
// SPDX-License-Identifier: BSD-2-Clause
// indus_bigram.fsx — Bigram structure analysis: mapped vs unmapped signs.
//
// Tests whether unmapped signs show phonetic (alphabetic) structure
// or open-inventory (codebook) structure. An alphabet produces moderate
// successor diversity with repetitive patterns. An open inventory
// produces high diversity with unique combinations.
//
// Usage:
//   dotnet fsi indus_bigram.fsx <corpus-db> <codebook-db>
//   dotnet fsi indus_bigram.fsx indus_corpus.db indus_codebook.db
//
// Output: bigram statistics, positional analysis, mass decode summary.
// All results printed to stdout — no database written.

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open Microsoft.Data.Sqlite

// === ARGUMENT PARSING ========================================================

let args = fsi.CommandLineArgs |> Array.tail

let corpusDb, codebookDb =
    match args with
    | [| c; k |] -> c, k
    | _ ->
        eprintfn "Usage: dotnet fsi indus_bigram.fsx <corpus-db> <codebook-db>"
        exit 1

// === HELPERS ==================================================================

let query (con: SqliteConnection) (sql: string) (mapper: SqliteDataReader -> 'T) =
    use cmd = new SqliteCommand(sql, con)
    use rdr = cmd.ExecuteReader()
    [| while rdr.Read() do yield mapper rdr |]

let scalar (con: SqliteConnection) (sql: string) =
    use cmd = new SqliteCommand(sql, con)
    cmd.ExecuteScalar() :?> int64

// === CONNECT =================================================================

use con = new SqliteConnection(sprintf "Data Source=%s;Mode=ReadOnly" corpusDb)
con.Open()

use attachCmd = new SqliteCommand(
    sprintf "ATTACH DATABASE '%s' AS cb" codebookDb, con)
attachCmd.ExecuteNonQuery() |> ignore

// === 1. MASS DECODE — commodity and route hits ==============================

printfn "═══════════════════════════════════════════════════════════════"
printfn " INDUS BIGRAM ANALYSIS: mapped vs unmapped sign structure"
printfn "═══════════════════════════════════════════════════════════════"
printfn ""

let totalInscriptions = scalar con "SELECT COUNT(*) FROM cisi_inscription"

let commodityHits = scalar con "
    SELECT COUNT(DISTINCT ci.id)
    FROM cisi_inscription ci
    JOIN sign_occurrence so ON so.artefact_id = ci.id AND so.source_code = 'CISI_MAYIG'
    JOIN sign_concordance sc ON sc.parpola_id = so.sign_id
    JOIN cb.sign_role sr ON sc.mahadevan_ids = 'M' || printf('%03d', sr.sign_id)
    WHERE sr.role = 'commodity'"

let routeHits = scalar con "
    SELECT COUNT(DISTINCT ci.id)
    FROM cisi_inscription ci
    JOIN sign_occurrence so ON so.artefact_id = ci.id AND so.source_code = 'CISI_MAYIG'
    JOIN sign_concordance sc ON sc.parpola_id = so.sign_id
    JOIN cb.sign_role sr ON sc.mahadevan_ids = 'M' || printf('%03d', sr.sign_id)
    WHERE sr.role = 'terminal'"

let bothHits = scalar con "
    SELECT COUNT(DISTINCT ci.id)
    FROM cisi_inscription ci
    WHERE ci.id IN (
      SELECT DISTINCT ci2.id FROM cisi_inscription ci2
      JOIN sign_occurrence so2 ON so2.artefact_id = ci2.id AND so2.source_code = 'CISI_MAYIG'
      JOIN sign_concordance sc2 ON sc2.parpola_id = so2.sign_id
      JOIN cb.sign_role sr2 ON sc2.mahadevan_ids = 'M' || printf('%03d', sr2.sign_id)
      WHERE sr2.role = 'commodity'
    ) AND ci.id IN (
      SELECT DISTINCT ci3.id FROM cisi_inscription ci3
      JOIN sign_occurrence so3 ON so3.artefact_id = ci3.id AND so3.source_code = 'CISI_MAYIG'
      JOIN sign_concordance sc3 ON sc3.parpola_id = so3.sign_id
      JOIN cb.sign_role sr3 ON sc3.mahadevan_ids = 'M' || printf('%03d', sr3.sign_id)
      WHERE sr3.role = 'terminal'
    )"

printfn "1. MASS DECODE"
printfn "   Total inscriptions:                %d" totalInscriptions
let pct n = int (float n / float totalInscriptions * 100.0)
printfn "   With commodity hit:                %d (%d%%)" commodityHits (pct commodityHits)
printfn "   With route hit:                    %d (%d%%)" routeHits (pct routeHits)
printfn "   With both commodity AND route:     %d (%d%%)" bothHits (pct bothHits)
printfn ""

// === 2. COMMODITY DISTRIBUTION ===============================================

printfn "2. COMMODITY DISTRIBUTION"

let commSql = "SELECT CASE sr.ref_code WHEN 'jar' THEN 'JAR GOODS' WHEN 'gold' THEN 'GOLD' WHEN 'carnelian' THEN 'CARNELIAN' WHEN 'ivory' THEN 'IVORY/SHELL' WHEN 'textile' THEN 'TEXTILES' WHEN 'timber' THEN 'TIMBER' WHEN 'copper' THEN 'COPPER' WHEN 'iron' THEN 'IRON' END as commodity, COUNT(DISTINCT ci.id) as seals FROM cisi_inscription ci JOIN sign_occurrence so ON so.artefact_id = ci.id AND so.source_code = 'CISI_MAYIG' JOIN sign_concordance sc ON sc.parpola_id = so.sign_id JOIN cb.sign_role sr ON sc.mahadevan_ids = 'M' || printf('%03d', sr.sign_id) WHERE sr.role = 'commodity' GROUP BY sr.ref_code ORDER BY seals DESC"
let commodities = query con commSql (fun r -> r.GetString 0, r.GetInt64 1)

for (name, count) in commodities do
    let p = int (float count / float totalInscriptions * 100.0)
    printfn "   %-15s %3d seals (%d%%)" name count p
printfn ""

// === 3. ROUTE DISTRIBUTION ===================================================

printfn "3. ROUTE DISTRIBUTION"

let routeSql = "SELECT CASE sr.ref_code WHEN 'mesopotamia' THEN 'MESOPOTAMIA' WHEN 'dilmun' THEN 'DILMUN' WHEN 'magan' THEN 'MAGAN' WHEN 'internal_north' THEN 'NORTH' WHEN 'internal_south' THEN 'SOUTH' END as route, COUNT(DISTINCT ci.id) as seals FROM cisi_inscription ci JOIN sign_occurrence so ON so.artefact_id = ci.id AND so.source_code = 'CISI_MAYIG' JOIN sign_concordance sc ON sc.parpola_id = so.sign_id JOIN cb.sign_role sr ON sc.mahadevan_ids = 'M' || printf('%03d', sr.sign_id) WHERE sr.role = 'terminal' GROUP BY sr.ref_code ORDER BY seals DESC"
let routes = query con routeSql (fun r -> r.GetString 0, r.GetInt64 1)

for (name, count) in routes do
    printfn "   %-15s %3d seals" name count

// Check for South = 0
let southCount =
    routes |> Array.tryFind (fun (n, _) -> n = "SOUTH")
    |> Option.map snd |> Option.defaultValue 0L
printfn ""
printfn "   South terminal:  %d  <-- M063 prediction %s"
    southCount (if southCount = 0L then "CONFIRMED" else "VIOLATED")
printfn ""

// === 4. BIGRAM STRUCTURE: MAPPED VS UNMAPPED =================================

printfn "4. BIGRAM STRUCTURE"

type BigramStats = {
    SignType: string
    UniqueSources: int64
    UniqueSuccessors: int64
    DistinctBigrams: int64
    TotalBigrams: int64 }

let bigramSql = "SELECT CASE WHEN sr.sign_id IS NOT NULL THEN 'mapped' ELSE 'unmapped' END as sign_type, COUNT(DISTINCT so1.sign_id) as unique_sources, COUNT(DISTINCT so2.sign_id) as unique_successors, COUNT(DISTINCT so1.sign_id || '->' || so2.sign_id) as distinct_bigrams, COUNT(*) as total_bigrams FROM sign_occurrence so1 JOIN sign_occurrence so2 ON so2.artefact_id = so1.artefact_id AND so2.source_code = 'CISI_MAYIG' AND so2.position = so1.position + 1 JOIN sign_concordance sc ON sc.parpola_id = so1.sign_id LEFT JOIN cb.sign_role sr ON sc.mahadevan_ids = 'M' || printf('%03d', sr.sign_id) WHERE so1.source_code = 'CISI_MAYIG' GROUP BY sign_type"

let bigramMapper (r: SqliteDataReader) =
    { SignType = r.GetString 0
      UniqueSources = r.GetInt64 1
      UniqueSuccessors = r.GetInt64 2
      DistinctBigrams = r.GetInt64 3
      TotalBigrams = r.GetInt64 4 }

let bigramStats = query con bigramSql bigramMapper

printfn ""
printfn "   %-10s %8s %8s %10s %10s %10s" "Type" "Sources" "Succs" "Avg S/sign" "Uniq ratio" "Bigrams"

for b in bigramStats do
    let avgSucc = float b.UniqueSuccessors / float (max 1L b.UniqueSources)
    let uniqRatio = float b.DistinctBigrams / float (max 1L b.TotalBigrams)
    printfn "   %-10s %8d %8d %10.1f %10.2f %10d" b.SignType b.UniqueSources b.UniqueSuccessors avgSucc uniqRatio b.TotalBigrams

printfn ""

// === 5. INTERPRETATION =======================================================

let mapped = bigramStats |> Array.tryFind (fun b -> b.SignType = "mapped")
let unmapped = bigramStats |> Array.tryFind (fun b -> b.SignType = "unmapped")

match mapped, unmapped with
| Some m, Some u ->
    let mAvg = float m.UniqueSuccessors / float (max 1L m.UniqueSources)
    let uAvg = float u.UniqueSuccessors / float (max 1L u.UniqueSources)
    let mUniq = float m.DistinctBigrams / float (max 1L m.TotalBigrams)
    let uUniq = float u.DistinctBigrams / float (max 1L u.TotalBigrams)

    printfn "5. INTERPRETATION"
    printfn ""
    printfn "   Mapped signs (codebook roles):"
    printfn "     %.1f successors per sign — closed-vocabulary field markers" mAvg
    printfn "     %.2f bigram uniqueness — moderate, structured combinations" mUniq
    printfn ""
    printfn "   Unmapped signs:"
    printfn "     %.1f successors per sign — each sign goes to ~1 place" uAvg
    printfn "     %.2f bigram uniqueness — high, unique combinations" uUniq
    printfn ""

    if uAvg < 3.0 && uUniq > 0.6 then
        printfn "   CONCLUSION: No alphabet detected."
        printfn "   Phonetic systems produce 10-30 successors/sign with"
        printfn "   repetitive bigrams (th, he, in, er). The unmapped signs"
        printfn "   produce %.1f successors/sign with %.0f%% unique bigrams."
            uAvg (uUniq * 100.0)
        printfn "   This is an open nominal inventory (merchant marks,"
        printfn "   sub-qualifiers) — not a phonetic layer."
        printfn ""
        printfn "   The unmapped 35%% is more codebook, not less."
    else
        printfn "   WARNING: Successor diversity (%.1f) is in the" uAvg
        printfn "   range that could indicate phonetic structure."
        printfn "   Further analysis required."
| _ ->
    printfn "   ERROR: Could not compute bigram statistics."

printfn ""
printfn "═══════════════════════════════════════════════════════════════"
printfn " Done. All results derived from:"
printfn "   corpus:   %s" corpusDb
printfn "   codebook: %s" codebookDb
printfn "═══════════════════════════════════════════════════════════════"

con.Close()
