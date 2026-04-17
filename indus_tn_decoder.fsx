#!/usr/bin/env -S dotnet fsi
// Tamil Nadu Potsherd Decoder — cross-corpus resolution using REAL data
// Reads morphological parallels, CISI inscriptions, and sign concordance
// from indus_corpus.db. Resolves TN graffiti through the IVC codebook.
// Conforms to: spec/fsharp/reference/fsharp_coding_standard.tex
//
// Usage:
//   dotnet fsi indus_tn_decoder.fsx <corpus-db> <codebook-db>
//   dotnet fsi indus_tn_decoder.fsx indus_corpus.db indus_codebook.db
//
// The decode chain:
//   TN potsherd sign
//     -> morphological_parallel (cross to IVC artefact)
//     -> cisi_inscription (real sign sequence, Parpola IDs)
//     -> sign_concordance (Parpola -> Mahadevan)
//     -> sign_role in codebook (assign cargo-tag field)
//     -> commodity / weight_tier / route (look up content)
//
// ZERO hardcoded sign sequences. Everything from the DB.

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open Microsoft.Data.Sqlite

// === ARGUMENT PARSING (S1) ===================================================

let args = fsi.CommandLineArgs |> Array.tail

let corpusDb, codebookDb =
    match args with
    | [| c; k |] -> c, k
    | _ ->
        eprintfn "Usage: dotnet fsi indus_tn_decoder.fsx <corpus-db> <codebook-db>"
        exit 1

// === TYPES (T1) ==============================================================

type Parallel = {
    TnArtefactId: string
    IvcArtefactId: string
    IvcSite: string
    TnSite: string
    Confidence: string
    Note: string }

type CisiInscription = {
    Id: string
    Description: string
    Signs: string array
    SignCount: int }

type WeightInfo = { Series: string; Mult: int; Grams: float; App: string }

type DecodedField =
    | CommodityField of label: string * desc: string
    | WeightField of WeightInfo
    | QuantityField of int
    | TerminalField of label: string * dest: string
    | StructuralField
    | Unmapped of string

// === LOAD FROM DB (F4: pure loads) ===========================================

let query (con: SqliteConnection) (sql: string) (mapper: SqliteDataReader -> 'T) =
    use cmd = new SqliteCommand(sql, con)
    use rdr = cmd.ExecuteReader()
    [| while rdr.Read() do yield mapper rdr |]

let loadParallels (con: SqliteConnection) =
    query con
        "SELECT sign_a_id, sign_b_id, confidence, note FROM morphological_parallel ORDER BY sign_b_id"
        (fun r ->
            let ivcId = r.GetString 0
            let tnId = r.GetString 1
            { TnArtefactId = tnId
              IvcArtefactId = ivcId
              IvcSite =
                if ivcId.Contains("Mohenj") then "Mohenjo-daro"
                elif ivcId.Contains("Harappa") then "Harappa"
                elif ivcId.Contains("Kalibangan") then "Kalibangan"
                elif ivcId.Contains("Rojdi") then "Rojdi"
                else "Unknown"
              TnSite =
                if tnId.Contains("Kodumanal") then "Kodumanal"
                elif tnId.Contains("Thulukarpatti") then "Thulukarpatti"
                elif tnId.Contains("Keeladi") || tnId.Contains("Keedali") then "Keeladi"
                elif tnId.Contains("Karur") then "Karur"
                elif tnId.Contains("Kilnamandi") then "Kilnamandi"
                elif tnId.Contains("Teriruveli") then "Teriruveli"
                elif tnId.Contains("Vallanadu") then "Vallanadu Agaram"
                else "Unknown"
              Confidence = r.GetString 2
              Note = if r.IsDBNull 3 then "" else r.GetString 3 })

let loadCisiInscriptions (con: SqliteConnection) =
    query con
        "SELECT id, description, signs, sign_count FROM cisi_inscription"
        (fun r ->
            let signsJson = r.GetString 2
            let signs =
                signsJson.Trim('[', ']').Split(',')
                |> Array.map (fun s -> s.Trim().Trim('"'))
                |> Array.filter (fun s -> s.Length > 0)
            { Id = r.GetString 0
              Description = if r.IsDBNull 1 then "" else r.GetString 1
              Signs = signs
              SignCount = r.GetInt32 3 })
    |> Array.map (fun ci -> ci.Id, ci) |> Map.ofArray

let loadConcordance (con: SqliteConnection) =
    query con
        "SELECT parpola_id, mahadevan_ids FROM sign_concordance WHERE mahadevan_ids IS NOT NULL"
        (fun r ->
            let mJson = r.GetString 1
            let mIds =
                mJson.Trim('[', ']').Split(',')
                |> Array.map (fun s -> s.Trim().Trim('"'))
                |> Array.filter (fun s -> s.Length > 0)
            r.GetString 0, mIds)
    |> Map.ofArray

let loadSignRoles (con: SqliteConnection) =
    query con "SELECT sign_id, role, ref_code, ref_multiplier FROM sign_role"
        (fun r ->
            r.GetInt32 0,
            (r.GetString 1,
             (if r.IsDBNull 2 then None else Some (r.GetString 2)),
             (if r.IsDBNull 3 then None else Some (r.GetInt32 3))))
    |> Map.ofArray

let loadCommodities (con: SqliteConnection) =
    query con "SELECT code, label, description FROM commodity"
        (fun r -> r.GetString 0, (r.GetString 1, if r.IsDBNull 2 then "" else r.GetString 2))
    |> Map.ofArray

let loadWeightTiers (con: SqliteConnection) =
    query con "SELECT multiplier, grams, series, application FROM weight_tier"
        (fun r -> r.GetInt32 0, (r.GetString 2, r.GetInt32 0, r.GetDouble 1, if r.IsDBNull 3 then "" else r.GetString 3))
    |> Map.ofArray

let loadRoutes (con: SqliteConnection) =
    query con "SELECT code, label, destination FROM route"
        (fun r -> r.GetString 0, (r.GetString 1, if r.IsDBNull 2 then "" else r.GetString 2))
    |> Map.ofArray

// === DECODE (pure) ===========================================================

let mahadevanToInt (mid: string) =
    let digits = mid.TrimStart('M', '0')
    match Int32.TryParse(if digits = "" then "0" else digits) with
    | true, n -> Some n
    | _ -> None

let decodeSign roles commodities weights routes (mahadevanId: string) =
    match mahadevanToInt mahadevanId with
    | None -> Unmapped (sprintf "%s: not a valid Mahadevan number" mahadevanId)
    | Some signNum ->
        match Map.tryFind signNum roles with
        | Some ("commodity", Some code, _) ->
            match Map.tryFind code commodities with
            | Some (label, desc) -> CommodityField (label, desc)
            | None -> Unmapped (sprintf "M%03d: commodity %s not in codebook" signNum code)
        | Some ("weight", _, Some m) ->
            match Map.tryFind m weights with
            | Some (s, mm, g, a) -> WeightField { Series = s; Mult = mm; Grams = g; App = a }
            | None -> Unmapped (sprintf "M%03d: weight x%d not in codebook" signNum m)
        | Some ("quantity", _, Some q) -> QuantityField q
        | Some ("terminal", Some code, _) ->
            match Map.tryFind code routes with
            | Some (label, dest) -> TerminalField (label, dest)
            | None -> Unmapped (sprintf "M%03d: route %s not in codebook" signNum code)
        | Some ("structural", _, _) -> StructuralField
        | Some _ -> Unmapped (sprintf "M%03d: unhandled role" signNum)
        | None -> Unmapped (sprintf "M%03d: not in codebook" signNum)

let describeField = function
    | CommodityField (l, d) -> sprintf "F2 COMMODITY  %-15s %s" l d
    | WeightField w -> sprintf "F3 WEIGHT     %s x%-4d = %.2f g  %s" w.Series w.Mult w.Grams w.App
    | QuantityField q -> sprintf "F4 QUANTITY   x%d" q
    | TerminalField (l, d) -> sprintf "F5 ROUTE      %-15s -> %s" l d
    | StructuralField -> "   STRUCTURAL (field delimiter)"
    | Unmapped reason -> sprintf "   UNMAPPED   %s" reason

// === ARTEFACT ID -> CISI ID ==================================================

let artefactToCisi (artefactId: string) =
    let cleaned = artefactId.Replace("Mohenjadaro", "Mohenjodaro")
    let parts = cleaned.Split('_')
    if parts.Length >= 2 then
        let numStr = parts.[1].TrimStart('0')
        let suffix = if parts.Length >= 3 then parts.[2] else "A"
        let prefix =
            if cleaned.Contains("Mohenj") then "M"
            elif cleaned.Contains("Harappa") then "H"
            elif cleaned.Contains("Kalibangan") then "K"
            elif cleaned.Contains("Rojdi") then "R"
            else "?"
        sprintf "%s-%s%s" prefix numStr suffix
    // Handle "Harappa-1482A" style IDs
    elif artefactId.Contains("-") then
        let dashParts = artefactId.Split('-')
        if dashParts.Length >= 2 then
            let prefix =
                if dashParts.[0].Contains("Harappa") then "H"
                elif dashParts.[0].Contains("Kalibangan") then "K"
                elif dashParts.[0].Contains("Rojdi") then "R"
                else "?"
            let numSuffix = dashParts.[1]
            // split trailing letter from number: "1482A" -> "1482", "A"
            let numPart = numSuffix |> Seq.takeWhile Char.IsDigit |> System.String.Concat
            let letterPart = numSuffix |> Seq.skipWhile Char.IsDigit |> System.String.Concat
            let letterPart = if letterPart = "" then "A" else letterPart
            sprintf "%s-%s%s" prefix numPart letterPart
        else artefactId
    else artefactId

// === MAIN ====================================================================

let corpusCon = new SqliteConnection(sprintf "Data Source=%s;Mode=ReadOnly" corpusDb)
corpusCon.Open()
let codebookCon = new SqliteConnection(sprintf "Data Source=%s;Mode=ReadOnly" codebookDb)
codebookCon.Open()

let parallels = loadParallels corpusCon
let cisiMap = loadCisiInscriptions corpusCon
let concordance = loadConcordance corpusCon
let roles = loadSignRoles codebookCon
let commodities = loadCommodities codebookCon
let weights = loadWeightTiers codebookCon
let routes = loadRoutes codebookCon

printfn ""
printfn "  TAMIL NADU POTSHERD DECODER (REAL DATA)"
printfn "  Corpus: %s (%d parallels, %d CISI inscriptions, %d concordance)"
    corpusDb parallels.Length cisiMap.Count concordance.Count
printfn "  Codebook: %s" codebookDb
printfn ""

let bySite =
    parallels
    |> Array.groupBy (fun p -> p.TnSite)
    |> Array.sortByDescending (fun (_, ps) -> ps.Length)

let mutable totalDecoded = 0
let mutable totalPending = 0

for (site, siteParallels) in bySite do
    printfn "== %s (%d parallels) ==" site siteParallels.Length
    printfn ""

    for p in siteParallels do
        let cisiId = artefactToCisi p.IvcArtefactId
        printfn "  TN:   %s" p.TnArtefactId
        printfn "  IVC:  %s (%s)  [%s]" p.IvcArtefactId p.IvcSite p.Confidence
        printfn "  CISI: %s" cisiId

        match Map.tryFind cisiId cisiMap with
        | Some ci ->
            totalDecoded <- totalDecoded + 1
            printfn "  Signs: %s (%d)" (ci.Signs |> String.concat " ") ci.SignCount
            printfn "  Decode:"

            ci.Signs |> Array.iter (fun pSign ->
                match Map.tryFind pSign concordance with
                | Some mIds when mIds.Length > 0 ->
                    let field = decodeSign roles commodities weights routes mIds.[0]
                    printfn "    %s -> %s -> %s" pSign mIds.[0] (describeField field)
                | _ ->
                    printfn "    %s -> (no Mahadevan mapping)" pSign)

            let fields =
                ci.Signs |> Array.choose (fun pSign ->
                    concordance
                    |> Map.tryFind pSign
                    |> Option.bind (fun mIds ->
                        if mIds.Length > 0 then Some (decodeSign roles commodities weights routes mIds.[0])
                        else None))

            let comms = fields |> Array.choose (function CommodityField (l,_) -> Some l | _ -> None)
            let rtes = fields |> Array.choose (function TerminalField (l,d) -> Some (sprintf "%s->%s" l d) | _ -> None)
            let wts = fields |> Array.choose (function WeightField w -> Some (sprintf "%.1fg(%sx%d)" w.Grams w.Series w.Mult) | _ -> None)

            printfn "  => TAG: %s | %s | %s  [REAL DATA]"
                (if comms.Length = 0 then "?" else String.concat "/" comms)
                (if wts.Length = 0 then "?" else String.concat "/" wts)
                (if rtes.Length = 0 then "domestic" else String.concat "/" rtes)
            printfn ""

        | None ->
            totalPending <- totalPending + 1
            printfn "  (CISI %s not in corpus — needs Harappa/Kalibangan/Rojdi data)" cisiId
            printfn ""

    printfn ""

printfn "== SUMMARY =="
printfn "  Total parallels        : %d" parallels.Length
printfn "  Decoded (real CISI)    : %d" totalDecoded
printfn "  Pending (no CISI data) : %d" totalPending
printfn "  TN sites               : %d" bySite.Length

corpusCon.Close()
codebookCon.Close()
