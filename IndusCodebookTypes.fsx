// Indus codebook — frozen typed records, generated once from indus_codebook.db
// by SqlHydra v4.0.0-beta.3. No runtime DB dependency. #load from any .fsx.
// To regenerate: cd hydra && sqlhydra sqlite

type WeightTier =
    { Multiplier: int; Grams: float; Series: string; Application: string }

type Commodity =
    { Code: string; Label: string; Description: string; CorpusNote: string }

type Route =
    { Code: string; Label: string; Destination: string }

type MerchantMark =
    { Code: string; Label: string; Note: string }

type SignRole =
    { SignId: int; Role: string; RefCode: string option; RefMultiplier: int option }

type PositionalRule =
    { Position: string; DefaultRole: string }

// === FROZEN DATA (from indus_codebook.db, seeded by indus_seed_codebook.fsx) =

let weightTiers = [|
    { Multiplier=1;    Grams=0.856;   Series="binary";  Application="Gold dust, gem weights" }
    { Multiplier=2;    Grams=1.712;   Series="binary";  Application="" }
    { Multiplier=4;    Grams=3.424;   Series="binary";  Application="Silver, carnelian" }
    { Multiplier=8;    Grams=6.848;   Series="binary";  Application="" }
    { Multiplier=16;   Grams=13.696;  Series="binary";  Application="Copper ingot reference unit" }
    { Multiplier=32;   Grams=27.392;  Series="binary";  Application="" }
    { Multiplier=64;   Grams=54.784;  Series="binary";  Application="" }
    { Multiplier=160;  Grams=137.0;   Series="decimal"; Application="Cotton bale tier 1" }
    { Multiplier=200;  Grams=171.2;   Series="decimal"; Application="" }
    { Multiplier=500;  Grams=685.0;   Series="decimal"; Application="Sesame oil jar class" }
    { Multiplier=1000; Grams=1370.0;  Series="decimal"; Application="Bulk grain" }
|]

let commodities = [|
    { Code="jar";       Label="JAR GOODS";     Description="sesame oil/grain/resin"; CorpusNote="S-342 ~10% corpus" }
    { Code="iron";      Label="IRON GOODS";    Description="Tamil Nadu supply corridor"; CorpusNote="fish sign" }
    { Code="carnelian"; Label="CARNELIAN";     Description="agate/carnelian beads"; CorpusNote="Gujarat source" }
    { Code="copper";    Label="COPPER-BRONZE"; Description="ingots/tools"; CorpusNote="Khetri mines" }
    { Code="textile";   Label="TEXTILES";      Description="cotton bales"; CorpusNote="Indus bulk export" }
    { Code="timber";    Label="TIMBER";        Description="structural logs"; CorpusNote="Akkadian records" }
    { Code="ivory";     Label="IVORY/SHELL";   Description="ornaments"; CorpusNote="Akkadian records" }
    { Code="gold";      Label="GOLD";          Description="jewellery"; CorpusNote="south India source" }
|]

let routes = [|
    { Code="mesopotamia";    Label="MESOPOTAMIA"; Destination="Ur / Kish / Tell Asmar" }
    { Code="dilmun";         Label="DILMUN";      Destination="Bahrain entrepot" }
    { Code="magan";          Label="MAGAN";       Destination="Oman copper return" }
    { Code="internal_north"; Label="NORTH";       Destination="Harappa / Mohenjo-daro" }
    { Code="internal_south"; Label="SOUTH";       Destination="Tamil Nadu supply" }
|]

let merchantMarks = [|
    { Code="unicorn";    Label="UNICORN GUILD";    Note="dominant trading house, most common motif" }
    { Code="bull";       Label="BULL GUILD";       Note="" }
    { Code="elephant";   Label="ELEPHANT GUILD";   Note="" }
    { Code="tiger";      Label="TIGER GUILD";      Note="" }
    { Code="rhinoceros"; Label="RHINOCEROS GUILD"; Note="" }
|]

let signRoles = [|
    { SignId=342; Role="commodity"; RefCode=Some "jar";       RefMultiplier=None }
    { SignId=218; Role="commodity"; RefCode=Some "iron";      RefMultiplier=None }
    { SignId=176; Role="commodity"; RefCode=Some "carnelian"; RefMultiplier=None }
    { SignId=184; Role="commodity"; RefCode=Some "copper";    RefMultiplier=None }
    { SignId=301; Role="commodity"; RefCode=Some "textile";   RefMultiplier=None }
    { SignId=200; Role="commodity"; RefCode=Some "timber";    RefMultiplier=None }
    { SignId=211; Role="commodity"; RefCode=Some "ivory";     RefMultiplier=None }
    { SignId=89;  Role="commodity"; RefCode=Some "gold";      RefMultiplier=None }
    { SignId=1;   Role="weight"; RefCode=None; RefMultiplier=Some 1 }
    { SignId=2;   Role="weight"; RefCode=None; RefMultiplier=Some 2 }
    { SignId=3;   Role="weight"; RefCode=None; RefMultiplier=Some 4 }
    { SignId=4;   Role="weight"; RefCode=None; RefMultiplier=Some 8 }
    { SignId=5;   Role="weight"; RefCode=None; RefMultiplier=Some 16 }
    { SignId=6;   Role="weight"; RefCode=None; RefMultiplier=Some 32 }
    { SignId=7;   Role="weight"; RefCode=None; RefMultiplier=Some 64 }
    { SignId=10;  Role="quantity"; RefCode=None; RefMultiplier=Some 1 }
    { SignId=11;  Role="quantity"; RefCode=None; RefMultiplier=Some 2 }
    { SignId=12;  Role="quantity"; RefCode=None; RefMultiplier=Some 5 }
    { SignId=13;  Role="quantity"; RefCode=None; RefMultiplier=Some 10 }
    { SignId=14;  Role="quantity"; RefCode=None; RefMultiplier=Some 20 }
    { SignId=15;  Role="quantity"; RefCode=None; RefMultiplier=Some 50 }
    { SignId=59;  Role="terminal"; RefCode=Some "mesopotamia";    RefMultiplier=None }
    { SignId=60;  Role="terminal"; RefCode=Some "dilmun";         RefMultiplier=None }
    { SignId=61;  Role="terminal"; RefCode=Some "magan";          RefMultiplier=None }
    { SignId=62;  Role="terminal"; RefCode=Some "internal_north"; RefMultiplier=None }
    { SignId=63;  Role="terminal"; RefCode=Some "internal_south"; RefMultiplier=None }
    { SignId=99;  Role="structural"; RefCode=None; RefMultiplier=None }
    { SignId=267; Role="structural"; RefCode=None; RefMultiplier=None }
|]

let positionalRules = [|
    { Position="first"; DefaultRole="commodity" }
    { Position="last";  DefaultRole="terminal" }
    { Position="other"; DefaultRole="structural" }
|]

// === LOOKUP MAPS (prebuilt) ==================================================

let commodityByCode = commodities |> Array.map (fun c -> c.Code, c) |> Map.ofArray
let routeByCode     = routes |> Array.map (fun r -> r.Code, r) |> Map.ofArray
let merchantByCode  = merchantMarks |> Array.map (fun m -> m.Code, m) |> Map.ofArray
let weightByMult    = weightTiers |> Array.map (fun w -> w.Multiplier, w) |> Map.ofArray
let signRoleBySign  = signRoles |> Array.map (fun s -> s.SignId, s) |> Map.ofArray
let posRuleMap      = positionalRules |> Array.map (fun p -> p.Position, p.DefaultRole) |> Map.ofArray
