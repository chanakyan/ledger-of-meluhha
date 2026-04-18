#!/usr/bin/env -S dotnet fsi
// Tamil Nadu Potsherd Decoder — cross-corpus resolution using REAL data
// Codebook from frozen typed records; corpus from SQLite at runtime.
// Conforms to: spec/fsharp/reference/fsharp_coding_standard.tex
//
// Usage:
//   dotnet fsi indus_tn_decoder.fsx <corpus-db>
//   dotnet fsi indus_tn_decoder.fsx indus_corpus.db
//
// The decode chain:
//   TN potsherd sign
//     -> morphological_parallel (cross to IVC artefact)
//     -> cisi_inscription (real sign sequence, Parpola IDs)
//     -> sign_concordance (Parpola -> Mahadevan)
//     -> signRoleBySign (frozen codebook — Parpola keyed)
//     -> commodity / weight / route (frozen lookups)
//
// ZERO hardcoded sign sequences. Corpus from the DB, codebook frozen.

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"
#load "IndusCodebookTypes.fsx"

open System
open Microsoft.Data.Sqlite
open IndusCodebookTypes

// === ARGUMENT PARSING (S1) ===================================================

let args = fsi.CommandLineArgs |> Array.tail

let corpusDb =
    match args with
    | [| c |] -> c
    | _ ->
        eprintfn "Usage: dotnet fsi indus_tn_decoder.fsx <corpus-db>"
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

type DecodedField =
    | CommodityField of label: string * desc: string
    | WeightField of WeightTier
    | QuantityField of int
    | TerminalField of label: string * dest: string
    | StructuralField
    | Unmapped of string

// === CORPUS DB QUERIES =======================================================

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

// === DECODE (pure — uses frozen codebook) ====================================

let mahadevanToInt (mid: string) =
    let digits = mid.TrimStart('M', '0')
    match Int32.TryParse(if digits = "" then "0" else digits) with
    | true, n -> Some n
    | _ -> None

let decodeSign (mahadevanId: string) =
    match mahadevanToInt mahadevanId with
    | None -> Unmapped (sprintf "%s: not a valid Mahadevan number" mahadevanId)
    | Some signNum ->
        match Map.tryFind signNum signRoleBySign with
        | Some sr ->
            match sr.Role with
            | "commodity" ->
                match sr.RefCode |> Option.bind (fun c -> Map.tryFind c commodityByCode) with
                | Some c -> CommodityField (c.Label, c.Description)
                | None -> Unmapped (sprintf "M%03d: commodity %s not in codebook" signNum (sr.RefCode |> Option.defaultValue "?"))
            | "weight" ->
                match sr.RefMultiplier |> Option.bind (fun m -> Map.tryFind m weightByMult) with
                | Some w -> WeightField w
                | None -> Unmapped (sprintf "M%03d: weight not in codebook" signNum)
            | "quantity" ->
                QuantityField (sr.RefMultiplier |> Option.defaultValue 1)
            | "terminal" ->
                match sr.RefCode |> Option.bind (fun c -> Map.tryFind c routeByCode) with
                | Some r -> TerminalField (r.Label, r.Destination)
                | None -> Unmapped (sprintf "M%03d: route %s not in codebook" signNum (sr.RefCode |> Option.defaultValue "?"))
            | "structural" -> StructuralField
            | _ -> Unmapped (sprintf "M%03d: unhandled role %s" signNum sr.Role)
        | None -> Unmapped (sprintf "M%03d: not in codebook" signNum)

let describeField = function
    | CommodityField (l, d) -> sprintf "F2 COMMODITY  %-15s %s" l d
    | WeightField w -> sprintf "F3 WEIGHT     %s x%-4d = %.2f g  %s" w.Series w.Multiplier w.Grams w.Application
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
    elif artefactId.Contains("-") then
        let dashParts = artefactId.Split('-')
        if dashParts.Length >= 2 then
            let prefix =
                if dashParts.[0].Contains("Harappa") then "H"
                elif dashParts.[0].Contains("Kalibangan") then "K"
                elif dashParts.[0].Contains("Rojdi") then "R"
                else "?"
            let numSuffix = dashParts.[1]
            let numPart = numSuffix |> Seq.takeWhile Char.IsDigit |> System.String.Concat
            let letterPart = numSuffix |> Seq.skipWhile Char.IsDigit |> System.String.Concat
            let letterPart = if letterPart = "" then "A" else letterPart
            sprintf "%s-%s%s" prefix numPart letterPart
        else artefactId
    else artefactId

// === MAIN ====================================================================

let corpusCon = new SqliteConnection(sprintf "Data Source=%s;Mode=ReadOnly" corpusDb)
corpusCon.Open()

let parallels = loadParallels corpusCon
let cisiMap = loadCisiInscriptions corpusCon
let concordance = loadConcordance corpusCon

printfn ""
printfn "  TAMIL NADU POTSHERD DECODER (REAL DATA)"
printfn "  Corpus: %s (%d parallels, %d CISI inscriptions, %d concordance)"
    corpusDb parallels.Length cisiMap.Count concordance.Count
printfn "  Codebook: frozen (%d signs, %d commodities, %d routes)"
    signRoles.Length commodities.Length routes.Length
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
                    let field = decodeSign mIds.[0]
                    printfn "    %s -> %s -> %s" pSign mIds.[0] (describeField field)
                | _ ->
                    printfn "    %s -> (no Mahadevan mapping)" pSign)

            let fields =
                ci.Signs |> Array.choose (fun pSign ->
                    concordance
                    |> Map.tryFind pSign
                    |> Option.bind (fun mIds ->
                        if mIds.Length > 0 then Some (decodeSign mIds.[0])
                        else None))

            let comms = fields |> Array.choose (function CommodityField (l,_) -> Some l | _ -> None)
            let rtes = fields |> Array.choose (function TerminalField (l,d) -> Some (sprintf "%s->%s" l d) | _ -> None)
            let wts = fields |> Array.choose (function WeightField w -> Some (sprintf "%.1fg(%sx%d)" w.Grams w.Series w.Multiplier) | _ -> None)

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
