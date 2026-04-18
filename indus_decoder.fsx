#!/usr/bin/env -S dotnet fsi
// Indus Barcode Decoder — codebook as frozen typed records, zero DB dependency
// Third Buyer Advisory — Rajeshkumar Venugopal, April 2026
// Conforms to: spec/fsharp/reference/fsharp_coding_standard.tex
//
// Usage:
//   dotnet fsi indus_decoder.fsx
//
// The codebook is frozen in IndusCodebookTypes.fsx (generated once by SqlHydra).
// To change the codebook, edit indus_seed_codebook.fsx, re-seed, re-generate.

#load "IndusCodebookTypes.fsx"
open IndusCodebookTypes

// === TYPES ===================================================================

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

// === DECODER (pure) ==========================================================

let unknownCommodity code = { Code = code; Label = "UNKNOWN"; Description = sprintf "sign %s" code; CorpusNote = "" }
let unknownRoute code     = { Code = code; Label = "UNKNOWN"; Destination = sprintf "terminal sign %s" code }
let defaultWeight         = weightByMult |> Map.tryFind 160 |> Option.defaultValue { Multiplier = 160; Grams = 137.0; Series = "decimal"; Application = "" }
let unknownMerchant code  = { Code = code; Label = sprintf "GUILD: %s" code; Note = "" }

let resolveSign (signId: int) (position: int) (seqLen: int) : string * string option * int option =
    match Map.tryFind signId signRoleBySign with
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

// === DISPLAY =================================================================

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
printfn "  Codebook: %d signs, %d commodities, %d routes (frozen)" signRoles.Length commodities.Length routes.Length

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
