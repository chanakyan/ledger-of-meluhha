#!/usr/bin/env -S dotnet fsi
// Indus Valley Script — transition matrix + LSSC-style metrics
// Data: logo-syllabic Tamil sentences as Indus script proxy corpus
// Conforms to: spec/fsharp/reference/fsharp_coding_standard.tex
//
// Usage:
//   dotnet fsi indus_lssc.fsx <corpus-csv> <output-db>
//
// Requires GNU tools via brew: coreutils

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open System.IO
open Microsoft.Data.Sqlite

// === ARGUMENT PARSING (S1: no hardcoded paths) ===============================

let args = fsi.CommandLineArgs |> Array.tail

let dataFile, dbPath =
    match args with
    | [| csv; db |] -> csv, db
    | _ ->
        eprintfn "Usage: dotnet fsi indus_lssc.fsx <corpus-csv> <output-db>"
        eprintfn "  e.g. dotnet fsi indus_lssc.fsx corpus.csv output.db"
        exit 1

// === TYPES (T1: records over tuples for 3+ fields) ===========================

type SignStats = {
    Sign: string
    Frequency: int
    UniqueFollowers: int
    Entropy: float
    Class: string }

type InscriptionLssc = {
    Id: string
    SignCount: int
    ClosureCount: int
    OptionCount: int
    Lssc: float
    RawSigns: string }

// === PURE HELPERS (F4: no side effects) ======================================

let skipTokens = Set.ofList ["("; ")"; ","; "."; ":"; ";"; "!"; "?"; "["; "]"]

let isSignToken (t: string) =
    not (Set.contains t skipTokens)
    && t.Length > 0
    && t |> Seq.forall (fun c -> Char.IsDigit c || c = '-')

let median (xs: float list) =
    let sorted = List.sort xs
    let n = sorted.Length
    if n = 0 then nan
    elif n % 2 = 0 then (sorted.[n/2 - 1] + sorted.[n/2]) / 2.0
    else sorted.[n/2]

let log2 x = Math.Log(x) / Math.Log(2.0)

let parseInscription (line: string) =
    let line = line.TrimStart('\uFEFF')
    let idx = line.IndexOf(',')
    if idx < 0 then None
    else
        let iid = line.[..idx-1].Trim()
        let rest = line.[idx+1..].Trim().Trim('"')
        let signs =
            rest.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            |> Array.filter isSignToken
        if signs.Length > 0 then Some (iid, signs)
        else None

let computeEntropy (followerCounts: int seq) =
    let total = followerCounts |> Seq.sum |> float
    followerCounts
    |> Seq.sumBy (fun c ->
        let p = float c / total
        -p * log2 p)

/// Build transition matrix: src -> (dst -> count)
/// P4: mutation hidden behind functional interface
let buildTransitionMatrix (inscriptions: (string * string array) array) =
    let trans = Collections.Generic.Dictionary<string, Collections.Generic.Dictionary<string, int>>()
    for (_, signs) in inscriptions do
        for i in 0 .. signs.Length - 2 do
            let src = signs.[i]
            let dst = signs.[i + 1]
            if not (trans.ContainsKey src) then
                trans.[src] <- Collections.Generic.Dictionary<string, int>()
            let inner = trans.[src]
            if inner.ContainsKey dst then inner.[dst] <- inner.[dst] + 1
            else inner.[dst] <- 1
    // C1: fold to count transitions
    let totalTransitions =
        inscriptions
        |> Array.sumBy (fun (_, signs) -> max 0 (signs.Length - 1))
    trans, totalTransitions

let computeSignStats
    (uniqueSigns: string array)
    (signFreq: Map<string, int>)
    (trans: Collections.Generic.Dictionary<string, Collections.Generic.Dictionary<string, int>>)
    (medianEntropy: float)
    : SignStats array =
    uniqueSigns
    |> Array.map (fun sign ->
        let hasOut, inner = trans.TryGetValue(sign)
        let entropy, followers =
            if not hasOut || inner.Count = 0 then 0.0, 0
            else computeEntropy inner.Values, inner.Count
        { Sign = sign
          Frequency = defaultArg (Map.tryFind sign signFreq) 0
          UniqueFollowers = followers
          Entropy = entropy
          Class = if entropy <= medianEntropy then "closure" else "option" })

let computeInscriptionLssc
    (inscriptions: (string * string array) array)
    (signClass: Map<string, string>)
    : InscriptionLssc array =
    inscriptions
    |> Array.map (fun (iid, signs) ->
        let cCount = signs |> Array.sumBy (fun s -> if signClass.[s] = "closure" then 1 else 0)
        let oCount = signs |> Array.sumBy (fun s -> if signClass.[s] = "option"  then 1 else 0)
        { Id = iid
          SignCount = signs.Length
          ClosureCount = cCount
          OptionCount = oCount
          Lssc = float cCount / float (oCount + 1)
          RawSigns = String.concat " " signs })

let pearsonCorrelation (xs: float array) (ys: float array) =
    let meanX = Array.average xs
    let meanY = Array.average ys
    let cov = Array.map2 (fun x y -> (x - meanX) * (y - meanY)) xs ys |> Array.average
    let stdX = xs |> Array.map (fun x -> (x - meanX) ** 2.0) |> Array.average |> sqrt
    let stdY = ys |> Array.map (fun y -> (y - meanY) ** 2.0) |> Array.average |> sqrt
    if stdX > 0.0 && stdY > 0.0 then cov / (stdX * stdY)
    else nan

// === DB WRITE (C2: imperative loops for terminal side effects) ===============

let schema = """
CREATE TABLE sign_entropy (
    sign              TEXT    PRIMARY KEY,
    frequency         INTEGER,
    unique_followers  INTEGER,
    entropy           REAL,
    class             TEXT CHECK(class IN ('closure','option'))
);
CREATE TABLE inscription_lssc (
    inscription_id  TEXT    PRIMARY KEY,
    sign_count      INTEGER,
    closure_count   INTEGER,
    option_count    INTEGER,
    lssc            REAL,
    raw_signs       TEXT
)"""

let writeResults (dbPath: string) (stats: SignStats array) (lsscRows: InscriptionLssc array) =
    Directory.CreateDirectory(Path.GetDirectoryName dbPath) |> ignore
    if File.Exists dbPath then File.Delete dbPath

    use con = new SqliteConnection(sprintf "Data Source=%s" dbPath)
    con.Open()

    let exec (sql: string) =
        use cmd = new SqliteCommand(sql, con)
        cmd.ExecuteNonQuery() |> ignore

    schema.Split(';', StringSplitOptions.RemoveEmptyEntries)
    |> Array.iter (fun s -> exec (s.Trim()))

    let tx = con.BeginTransaction()

    for s in stats do
        use cmd = new SqliteCommand("INSERT INTO sign_entropy VALUES (@s,@f,@fol,@h,@c)", con)
        cmd.Transaction <- tx
        cmd.Parameters.AddWithValue("@s",   s.Sign) |> ignore
        cmd.Parameters.AddWithValue("@f",   s.Frequency) |> ignore
        cmd.Parameters.AddWithValue("@fol", s.UniqueFollowers) |> ignore
        cmd.Parameters.AddWithValue("@h",   s.Entropy) |> ignore
        cmd.Parameters.AddWithValue("@c",   s.Class) |> ignore
        cmd.ExecuteNonQuery() |> ignore

    for r in lsscRows do
        use cmd = new SqliteCommand("INSERT INTO inscription_lssc VALUES (@id,@sc,@cc,@oc,@l,@r)", con)
        cmd.Transaction <- tx
        cmd.Parameters.AddWithValue("@id",  r.Id) |> ignore
        cmd.Parameters.AddWithValue("@sc",  r.SignCount) |> ignore
        cmd.Parameters.AddWithValue("@cc",  r.ClosureCount) |> ignore
        cmd.Parameters.AddWithValue("@oc",  r.OptionCount) |> ignore
        cmd.Parameters.AddWithValue("@l",   r.Lssc) |> ignore
        cmd.Parameters.AddWithValue("@r",   r.RawSigns) |> ignore
        cmd.ExecuteNonQuery() |> ignore

    tx.Commit()
    con.Close()

// === MAIN (boundary: all side effects here) ==================================

let lines = File.ReadAllLines(dataFile)

let inscriptions =
    lines
    |> Array.skip 1
    |> Array.choose parseInscription

let allSignsFlat = inscriptions |> Array.collect snd
let uniqueSigns = allSignsFlat |> Array.distinct |> Array.sort
let signFreq = allSignsFlat |> Array.countBy id |> Map.ofArray

let trans, totalTransitions = buildTransitionMatrix inscriptions

let entropyVals =
    uniqueSigns
    |> Array.map (fun sign ->
        let hasOut, inner = trans.TryGetValue(sign)
        if not hasOut || inner.Count = 0 then 0.0
        else computeEntropy inner.Values)
    |> Array.toList

let eMedian = median entropyVals

let signStats = computeSignStats uniqueSigns signFreq trans eMedian
let signClass = signStats |> Array.map (fun s -> s.Sign, s.Class) |> Map.ofArray
let lsscRows = computeInscriptionLssc inscriptions signClass

let cCounts = lsscRows |> Array.map (fun r -> float r.ClosureCount)
let oCounts = lsscRows |> Array.map (fun r -> float r.OptionCount)
let pearsonR = pearsonCorrelation cCounts oCounts

writeResults dbPath signStats lsscRows

// === REPORT ==================================================================

let nClosure = signStats |> Array.sumBy (fun s -> if s.Class = "closure" then 1 else 0)
let nOption  = signStats |> Array.sumBy (fun s -> if s.Class = "option"  then 1 else 0)
let lsscVals = lsscRows |> Array.map (fun r -> r.Lssc) |> Array.toList

printfn "INDUS LSSC ANALYSIS"
printfn "  Corpus              : %s" dataFile
printfn "  Inscriptions        : %d" inscriptions.Length
printfn "  Total sign tokens   : %d" allSignsFlat.Length
printfn "  Unique signs        : %d" uniqueSigns.Length
printfn "  Bigram transitions  : %d" totalTransitions
printfn "  Entropy min/mean/med/max: %.4f / %.4f / %.4f / %.4f"
    (List.min entropyVals) (List.average entropyVals) eMedian (List.max entropyVals)
printfn "  Closure signs       : %d" nClosure
printfn "  Option  signs       : %d" nOption
printfn "  LSSC min/mean/med/max: %.4f / %.4f / %.4f / %.4f"
    (List.min lsscVals) (List.average lsscVals) (median lsscVals) (List.max lsscVals)
printfn "  TSF Cond 2 r (raw)  : %.4f" pearsonR
printfn "  DB written to       : %s" dbPath

printfn "\nTop-10 closure signs (lowest entropy):"
signStats
|> Array.filter (fun s -> s.Class = "closure")
|> Array.sortBy (fun s -> s.Entropy)
|> Array.truncate 10
|> Array.iter (fun s ->
    printfn "  %-30s  H=%.4f  freq=%d  followers=%d" s.Sign s.Entropy s.Frequency s.UniqueFollowers)

printfn "\nTop-10 option signs (highest entropy):"
signStats
|> Array.filter (fun s -> s.Class = "option")
|> Array.sortByDescending (fun s -> s.Entropy)
|> Array.truncate 10
|> Array.iter (fun s ->
    printfn "  %-30s  H=%.4f  freq=%d  followers=%d" s.Sign s.Entropy s.Frequency s.UniqueFollowers)
