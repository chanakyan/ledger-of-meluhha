#!/usr/bin/env -S dotnet fsi
// Indus Barcode Decoder — reads codebook from SQLite, zero hardcoded config
// Third Buyer Advisory — Rajeshkumar Venugopal, April 2026
// Conforms to: spec/fsharp/reference/fsharp_coding_standard.tex
//
// Usage:
//   dotnet fsi indus_seed_codebook.fsx indus_codebook.db   # first time
//   dotnet fsi indus_decoder.fsx indus_codebook.db
//
// All sign mappings, weights, commodities, routes live in the DB.
// To change the codebook, edit indus_seed_codebook.fsx and re-seed.

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open Microsoft.Data.Sqlite

// === ARGUMENT PARSING (S1: no hardcoded paths) ===============================

let args = fsi.CommandLineArgs |> Array.tail

let dbPath =
    match args with
    | [| db |] -> db
    | _ ->
        eprintfn "Usage: dotnet fsi indus_decoder.fsx <codebook-db>"
        exit 1

// === TYPES (T1: records, T2: DUs) ============================================

type WeightTier = {
    Multiplier: int
    Grams: float
    Series: string
    Application: string }

type Commodity = {
    Code: string
    Label: string
    Description: string
    CorpusNote: string }

type Route = {
    Code: string
    Label: string
    Destination: string }

type MerchantMark = {
    Code: string
    Label: string
    Note: string }

type SignRole = {
    SignId: int
    Role: string
    RefCode: string option
    RefMultiplier: int option }

type PositionalRule = {
    Position: string
    DefaultRole: string }

type CargoTag = {
    Merchant: MerchantMark
    Commodity: Commodity
    WeightTier: WeightTier
    Quantity: int
    Route: Route }

type SealEntry = {
    Label: string
    MerchantCode: string
    Signs: int list }

// === LOAD CODEBOOK FROM DB (F4: pure load, no display) =======================

let loadAll (dbPath: string) =
    use con = new SqliteConnection(sprintf "Data Source=%s;Mode=ReadOnly" dbPath)
    con.Open()

    let query (sql: string) (mapper: SqliteDataReader -> 'T) =
        use cmd = new SqliteCommand(sql, con)
        use rdr = cmd.ExecuteReader()
        [| while rdr.Read() do yield mapper rdr |]

    let weightTiers =
        query "SELECT multiplier, grams, series, application FROM weight_tier ORDER BY multiplier"
            (fun r -> { Multiplier = r.GetInt32 0; Grams = r.GetDouble 1
                        Series = r.GetString 2
                        Application = if r.IsDBNull 3 then "" else r.GetString 3 })

    let commodities =
        query "SELECT code, label, description, corpus_note FROM commodity"
            (fun r -> { Code = r.GetString 0; Label = r.GetString 1
                        Description = if r.IsDBNull 2 then "" else r.GetString 2
                        CorpusNote = if r.IsDBNull 3 then "" else r.GetString 3 })

    let routes =
        query "SELECT code, label, destination FROM route"
            (fun r -> { Code = r.GetString 0; Label = r.GetString 1
                        Destination = if r.IsDBNull 2 then "" else r.GetString 2 })

    let merchantMarks =
        query "SELECT code, label, note FROM merchant_mark"
            (fun r -> { Code = r.GetString 0; Label = r.GetString 1
                        Note = if r.IsDBNull 2 then "" else r.GetString 2 })

    let signRoles =
        query "SELECT sign_id, role, ref_code, ref_multiplier FROM sign_role"
            (fun r -> { SignId = r.GetInt32 0; Role = r.GetString 1
                        RefCode = if r.IsDBNull 2 then None else Some (r.GetString 2)
                        RefMultiplier = if r.IsDBNull 3 then None else Some (r.GetInt32 3) })

    let positionalRules =
        query "SELECT position, default_role FROM positional_rule"
            (fun r -> { Position = r.GetString 0; DefaultRole = r.GetString 1 })

    con.Close()
    weightTiers, commodities, routes, merchantMarks, signRoles, positionalRules

let weightTiers, commodities, routes, merchantMarks, signRoles, positionalRules =
    loadAll dbPath

// === LOOKUP MAPS =============================================================

let commodityByCode = commodities |> Array.map (fun c -> c.Code, c) |> Map.ofArray
let routeByCode     = routes |> Array.map (fun r -> r.Code, r) |> Map.ofArray
let merchantByCode  = merchantMarks |> Array.map (fun m -> m.Code, m) |> Map.ofArray
let weightByMult    = weightTiers |> Array.map (fun w -> w.Multiplier, w) |> Map.ofArray
let signRoleMap     = signRoles |> Array.map (fun s -> s.SignId, s) |> Map.ofArray
let posRuleMap      = positionalRules |> Array.map (fun p -> p.Position, p.DefaultRole) |> Map.ofArray

let unknownCommodity code = { Code = code; Label = "UNKNOWN"; Description = sprintf "sign %s" code; CorpusNote = "" }
let unknownRoute code     = { Code = code; Label = "UNKNOWN"; Destination = sprintf "terminal sign %s" code }
let defaultWeight         = weightByMult |> Map.tryFind 160 |> Option.defaultValue { Multiplier = 160; Grams = 137.0; Series = "decimal"; Application = "" }
let unknownMerchant code  = { Code = code; Label = sprintf "GUILD: %s" code; Note = "" }

// === DECODER (pure) ==========================================================

let resolveSign (signId: int) (position: int) (seqLen: int) : string * string option * int option =
    match Map.tryFind signId signRoleMap with
    | Some sr -> sr.Role, sr.RefCode, sr.RefMultiplier
    | None ->
        let posKey =
            if position = 0 then "first"
            elif position = seqLen - 1 then "last"
            else "other"
        let defRole = Map.tryFind posKey posRuleMap |> Option.defaultValue "structural"
        defRole, Some (sprintf "S-%d" signId), None

let parseSeal (entry: SealEntry) : CargoTag option =
    if List.length entry.Signs < 2 then None
    else
        let resolved =
            entry.Signs
            |> List.mapi (fun i s -> resolveSign s i (List.length entry.Signs))

        let pickRole target =
            resolved |> List.tryPick (fun (role, refCode, refMult) ->
                if role = target then Some (refCode, refMult) else None)

        let commodity =
            match pickRole "commodity" with
            | Some (Some code, _) -> Map.tryFind code commodityByCode |> Option.defaultValue (unknownCommodity code)
            | _ -> unknownCommodity "unresolved"

        let weight =
            match pickRole "weight" with
            | Some (_, Some mult) -> Map.tryFind mult weightByMult |> Option.defaultValue defaultWeight
            | _ -> defaultWeight

        let quantity =
            match pickRole "quantity" with
            | Some (_, Some q) -> q
            | _ -> 1

        let route =
            match pickRole "terminal" with
            | Some (Some code, _) -> Map.tryFind code routeByCode |> Option.defaultValue (unknownRoute code)
            | _ -> unknownRoute "no terminal"

        let merchant =
            Map.tryFind entry.MerchantCode merchantByCode
            |> Option.defaultValue (unknownMerchant entry.MerchantCode)

        Some { Merchant = merchant; Commodity = commodity
               WeightTier = weight; Quantity = quantity; Route = route }

// === TEST DATA ===============================================================

let seals = [
    { Label = "Mohenjo-daro unicorn M-0052";  MerchantCode = "unicorn";  Signs = [342;99;5;59] }
    { Label = "Harappa bull H-0071";           MerchantCode = "bull";     Signs = [218;5;62] }
    { Label = "Lothal dock L-0012";            MerchantCode = "unicorn";  Signs = [301;13;59] }
    { Label = "Kodumanal TN parallel";         MerchantCode = "bull";     Signs = [218;4;63] }
    { Label = "Kalibangan copper K-0150";      MerchantCode = "elephant"; Signs = [184;6;60] }
    { Label = "Mohenjo-daro jar M-0321";       MerchantCode = "unicorn";  Signs = [342;14;59] }
    { Label = "Harappa carnelian H-0009";      MerchantCode = "tiger";    Signs = [176;3;61] }
    { Label = "Harappa gold H-0093";           MerchantCode = "unicorn";  Signs = [89;5;59] }
    { Label = "Thulukarpatti TN iron supply";  MerchantCode = "bull";     Signs = [218;5;63] }
]

// === DISPLAY (boundary: all side effects here) ===============================

let printTag (label: string) (tag: CargoTag) =
    let w = tag.WeightTier.Grams * float tag.Quantity
    printfn ""
    printfn "  +-- %s" label
    printfn "  |  F1  %-20s %s" tag.Merchant.Label tag.Merchant.Note
    printfn "  |  F2  %-15s %s  [%s]" tag.Commodity.Label tag.Commodity.Description tag.Commodity.CorpusNote
    printfn "  |  F3  %s x%-4d = %.2f g  [%s]" tag.WeightTier.Series tag.WeightTier.Multiplier tag.WeightTier.Grams tag.WeightTier.Application
    printfn "  |  F4  Lot x%-3d -> total cargo %.1f g  (%.3f kg)" tag.Quantity w (w / 1000.0)
    printfn "  +-- F5  %-15s -> %s" tag.Route.Label tag.Route.Destination

printfn ""
printfn "  LEDGER OF MELUHHA — INDUS BARCODE DECODER"
printfn "  Third Buyer Advisory LLC  |  April 2026"
printfn "  Codebook: %s (%d signs, %d commodities, %d routes)" dbPath signRoles.Length commodities.Length routes.Length

printfn ""
printfn "  WEIGHT CODEBOOK (base = 0.856g, Hemmy 1931):"
for w in weightTiers do
    printfn "    %s x%-4d = %8.2f g  %s" w.Series w.Multiplier w.Grams w.Application

printfn ""
printfn "== SEAL DECODE =="

seals |> List.iter (fun entry ->
    match parseSeal entry with
    | Some tag -> printTag entry.Label tag
    | None     -> printfn "  [%s] too short — ownership mark only" entry.Label)

printfn ""
printfn "== TAMIL NADU SUPPLY CORRIDOR =="
printfn "  90%% sign overlap = network codebook, not language."
printfn "  TWO ENDS. ONE LEDGER."
