#!/usr/bin/env -S dotnet fsi
// Ingest Indus corpus data into normalized SQLite database.
// Schema verified by Alloy: spec/indus_corpus.als (9/9 UNSAT)
// Conforms to: spec/fsharp/reference/fsharp_coding_standard.tex
//
// Usage:
//   dotnet fsi indus_ingest_corpus.fsx <indus-valley-dir> <output-db>
//   dotnet fsi indus_ingest_corpus.fsx ../indus-valley indus_corpus.db
//
// Sources ingested:
//   1. Tamil logogram CSVs (lemmas, morphemes, clitics, letters, syllables)
//   2. Tamil logo-syllabic sentences (the proxy corpus)
//   3. Rajan-Sivanantham 42 base signs (hardcoded from PDF table p.4)
//   4. Mahadevan sign references (from dendrogram + histogram files)

#r "nuget: Microsoft.Data.Sqlite, 8.0.13"

open System
open System.IO
open Microsoft.Data.Sqlite

// === ARGUMENT PARSING (S1) ===================================================

let args = fsi.CommandLineArgs |> Array.tail

let dataDir, dbPath =
    match args with
    | [| dir; db |] -> dir, db
    | _ ->
        eprintfn "Usage: dotnet fsi indus_ingest_corpus.fsx <indus-valley-dir> <output-db>"
        exit 1

// === TYPES (T1: records over tuples) =========================================

type Source = { Code: string; Name: string; Year: int; Note: string }

type Site = { Id: string; Name: string; SourceCode: string; Region: string; Period: string }

type BaseSign = {
    SourceCode: string
    SignId: string
    Label: string
    Variants: int
    Composites: int
    Total: int }

type SignForm = {
    SourceCode: string
    FormId: string
    ParentSignId: string
    FormType: string
    Label: string }

type Logogram = {
    SignId: string
    SourceCode: string
    Label: string
    PartOfSpeech: string
    Category: string }

type Inscription = {
    Id: string
    SourceCode: string
    SignSequence: string
    SignCount: int }

// === SCHEMA (S4) =============================================================

let schema = """
CREATE TABLE source (
    code    TEXT PRIMARY KEY,
    name    TEXT NOT NULL,
    year    INTEGER,
    note    TEXT
) STRICT;

CREATE TABLE site (
    id          TEXT PRIMARY KEY,
    name        TEXT NOT NULL,
    source_code TEXT NOT NULL REFERENCES source(code),
    region      TEXT,
    period      TEXT
) STRICT;

CREATE TABLE base_sign (
    source_code TEXT    NOT NULL REFERENCES source(code),
    sign_id     TEXT    NOT NULL,
    label       TEXT,
    variants    INTEGER DEFAULT 0,
    composites  INTEGER DEFAULT 0,
    total       INTEGER DEFAULT 0,
    PRIMARY KEY (source_code, sign_id)
) STRICT;

CREATE TABLE sign_form (
    source_code     TEXT NOT NULL,
    form_id         TEXT NOT NULL,
    parent_sign_id  TEXT NOT NULL,
    form_type       TEXT NOT NULL CHECK(form_type IN ('variant','composite')),
    label           TEXT,
    PRIMARY KEY (source_code, form_id),
    FOREIGN KEY (source_code, parent_sign_id) REFERENCES base_sign(source_code, sign_id)
) STRICT;

CREATE TABLE sign_occurrence (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    artefact_id TEXT    NOT NULL,
    source_code TEXT    NOT NULL,
    sign_id     TEXT    NOT NULL,
    position    INTEGER NOT NULL,
    UNIQUE(artefact_id, position)
) STRICT;

CREATE TABLE inscription (
    id            TEXT PRIMARY KEY,
    source_code   TEXT NOT NULL REFERENCES source(code),
    sign_sequence TEXT NOT NULL,
    sign_count    INTEGER NOT NULL
) STRICT;

CREATE TABLE morphological_parallel (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    sign_a_src  TEXT NOT NULL,
    sign_a_id   TEXT NOT NULL,
    sign_b_src  TEXT NOT NULL,
    sign_b_id   TEXT NOT NULL,
    confidence  TEXT CHECK(confidence IN ('exact','near','possible')),
    note        TEXT,
    CHECK(sign_a_src != sign_b_src)
) STRICT;

CREATE TABLE sign_concordance (
    parpola_id    TEXT PRIMARY KEY,
    description   TEXT,
    mahadevan_ids TEXT,
    wells_ids     TEXT
) STRICT;

CREATE TABLE cisi_inscription (
    id          TEXT PRIMARY KEY,
    description TEXT,
    signs       TEXT NOT NULL,
    sign_count  INTEGER NOT NULL,
    source_code TEXT NOT NULL DEFAULT 'CISI' REFERENCES source(code)
) STRICT;

CREATE TABLE treebank_token (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    sentence_id   INTEGER NOT NULL,
    position      INTEGER NOT NULL,
    form          TEXT    NOT NULL,
    lemma         TEXT    NOT NULL,
    pos           TEXT    NOT NULL,
    pos_detail    TEXT,
    features      TEXT,
    head          INTEGER,
    dep_rel       TEXT,
    UNIQUE(sentence_id, position)
) STRICT;

CREATE TABLE tamil_sentence (
    id              INTEGER PRIMARY KEY,
    original_tamil  TEXT    NOT NULL,
    logosyllabic    TEXT,
    sign_count      INTEGER
) STRICT
"""

// === HELPERS (F4: pure) ======================================================

let parseCsv (path: string) =
    if not (File.Exists path) then
        eprintfn "  SKIP: %s (not found)" path
        [||]
    else
        let lines = File.ReadAllLines(path)
        let lines = lines |> Array.map (fun l -> l.TrimStart('\uFEFF'))
        if lines.Length < 2 then [||]
        else lines |> Array.skip 1

let splitCsvLine (line: string) =
    // Simple CSV split — handles quoted fields for this dataset
    let mutable inQuote = false
    let mutable fields = ResizeArray<string>()
    let mutable current = System.Text.StringBuilder()
    for c in line do
        match c with
        | '"' -> inQuote <- not inQuote
        | ',' when not inQuote ->
            fields.Add(current.ToString().Trim())
            current.Clear() |> ignore
        | _ -> current.Append(c) |> ignore
    fields.Add(current.ToString().Trim())
    fields.ToArray()

// === RAJAN-SIVANANTHAM 42 BASE SIGNS (from PDF table p.4) ====================
// Transcribed directly from the published table. This is the source of truth.

let rajanSigns = [|
    ("1",  "1.0 saltire/X-cross",       39, 124, 164)
    ("2",  "2.0 X-cross",               37, 179, 217)
    ("3",  "3.0 triple stroke",          27, 105, 133)
    ("4",  "4.0 chevron/arrowhead",      19,  66, 102)  // total from table is 102 not 85
    ("5",  "5.0 zigzag",                 16,  35,  52)
    ("6",  "6.0 circle variants",         9,  40,  50)
    ("7",  "7.0 trident",                 4,  32,  37)
    ("8",  "8.0 diamond",                 5,   7,  13)
    ("9",  "9.0 wheel/asterisk",          6,  42,  49)
    ("10", "10.0 arrow-right",            4,   5,  10)
    ("11", "11.0 Y-branch",              12,  59,  72)
    ("12", "12.0 cup/U-down",             3,   3,   7)
    ("13", "13.0 arrow-up",              35,  66, 102)
    ("14", "14.0 triangle",              15,  16,  32)
    ("15", "15.0 U-form",                 9,  83,  93)
    ("16", "16.0 double-U",               8,  58,  67)
    ("17", "17.0 arch/gate",             16,  41,  58)
    ("18", "18.0 rectangle",             10,   5,  16)
    ("19", "19.0 comb",                   6,  28,  35)
    ("20", "20.0 isolated/dot-circle",    6,   0,   7)
    ("21", "21.0 pi-form",               11,  38,  50)
    ("22", "22.0 double-bar",            16,  35,  52)
    ("23", "23.0 E-form",                 4,  38,  43)
    ("24", "24.0 psi/trident-base",       9,  20,  30)
    ("25", "25.0 plus/cross",             8,  21,  30)
    ("26", "26.0 X-variant",              5,  10,  16)
    ("27", "27.0 double-triangle",        6,   2,   9)
    ("28", "28.0 box-grid",              12,  26,  39)
    ("29", "29.0 wave/multiple",         24,  22,  47)
    ("30", "30.0 wave/triskel",          19,  70,  90)
    ("31", "31.0 flag",                  14,  15,  30)
    ("32", "32.0 angle/hook",            10,  20,  31)
    ("33", "33.0 dot-line",              17,  15,  33)
    ("34", "34.0 triple-wave",            6,  59,  66)
    ("35", "35.0 double-line",           18,  18,  37)
    ("36", "36.0 single stroke",         23,  51,  75)
    ("37", "37.0 tree/branch",           27,  40,  68)
    ("38", "38.0 diamond-dot",           15,  21,  37)
    ("39", "39.0 curve",                 18,  22,  41)
    ("40", "40.0 dot/minimal",            1,   3,   5)
    ("41", "41.0 A-frame",               7,  20,  28)
    ("42", "42.0 spiral",                4,   8,  13)
|]

// === SOURCES =================================================================

let sources = [|
    { Code = "RS2025";   Name = "Rajan-Sivanantham Indus Signs and Graffiti Marks of TN"
      Year = 2025;       Note = "42 base signs, 544 variants, 1521 composites, 140 sites" }
    { Code = "RS2026";   Name = "Rajan-Sivanantham Inscribed Potsherds of TN Vol 1"
      Year = 2026;       Note = "740pp, 15184 potsherds, 1500+ Tamili inscribed, 42 sites" }
    { Code = "TAMIL_TB"; Name = "Tamil Treebank v0.1 — logo-syllabic conversion"
      Year = 2011;       Note = "Ramasamy & Abokrtsk, converted to logogram IDs by Kee2u project" }
    { Code = "MAHADEVAN"; Name = "Mahadevan Concordance / EBUDS / ICIT"
      Year = 1977;       Note = "~417 signs, ~4000 objects, ~4791 texts" }
    { Code = "CISI";     Name = "CISI corpus via mayig/indus-valley-script-corpus"
      Year = 1987;       Note = "Joshi & Parpola, digitized by mayig. 179 Mohenjo-daro inscriptions, Parpola sign IDs" }
|]

// === TN SITES (key sites from RS publications) ===============================

let tnSites = [|
    ("TN_KEELADI",       "Keeladi",          "RS2026", "Sivaganga", "Iron Age / Early Historic")
    ("TN_KODUMANAL",     "Kodumanal",        "RS2025", "Erode",     "Iron Age")
    ("TN_ADICHANALLUR",  "Adichanallur",     "RS2026", "Thoothukudi","Iron Age")
    ("TN_THULUKARPATTI", "Thulukarpatti",    "RS2025", "Thoothukudi","Iron Age")
    ("TN_ARIKAMEDU",     "Arikamedu",        "RS2026", "Puducherry", "Early Historic")
    ("TN_URAIYUR",       "Uraiyur",          "RS2026", "Trichy",    "Early Historic")
    ("TN_KORKAI",        "Korkai",           "RS2026", "Thoothukudi","Iron Age / Early Historic")
    ("TN_SIVAGALAI",     "Sivagalai",        "RS2026", "Thoothukudi","Iron Age")
    ("TN_MANGADU",       "Mangadu",          "RS2026", "Kanchipuram","Iron Age")
    ("TN_KILNAMANDI",    "Kilnamandi",       "RS2025", "Dharmapuri", "Iron Age")
    ("TN_PORUNTHAL",     "Porunthal",        "RS2026", "Dindigul",  "Iron Age")
    ("TN_MAYILADUMPARAI","Mayiladumparai",   "RS2026", "Krishnagiri","Iron Age")
    ("IVC_MOHENJODARO",  "Mohenjo-daro",     "MAHADEVAN","Sindh",    "Mature Harappan")
    ("IVC_HARAPPA",      "Harappa",          "MAHADEVAN","Punjab",   "Mature Harappan")
    ("IVC_LOTHAL",       "Lothal",           "MAHADEVAN","Gujarat",  "Mature Harappan")
    ("IVC_KALIBANGAN",   "Kalibangan",       "MAHADEVAN","Rajasthan","Mature Harappan")
    ("IVC_DHOLAVIRA",    "Dholavira",        "MAHADEVAN","Gujarat",  "Mature Harappan")
    ("IVC_ROJDI",        "Rojdi",            "MAHADEVAN","Gujarat",  "Late Harappan")
|]

// === DB WRITE ================================================================

if File.Exists dbPath then File.Delete dbPath
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath dbPath)) |> ignore

use con = new SqliteConnection(sprintf "Data Source=%s" dbPath)
con.Open()

let exec (sql: string) =
    use cmd = new SqliteCommand(sql, con)
    cmd.ExecuteNonQuery() |> ignore

schema.Split(';', StringSplitOptions.RemoveEmptyEntries)
|> Array.iter (fun s ->
    let trimmed = s.Trim()
    if trimmed.Length > 0 then exec trimmed)

let tx = con.BeginTransaction()

// --- sources ---
for s in sources do
    use cmd = new SqliteCommand("INSERT INTO source VALUES (@c,@n,@y,@t)", con, tx)
    cmd.Parameters.AddWithValue("@c", s.Code) |> ignore
    cmd.Parameters.AddWithValue("@n", s.Name) |> ignore
    cmd.Parameters.AddWithValue("@y", s.Year) |> ignore
    cmd.Parameters.AddWithValue("@t", s.Note) |> ignore
    cmd.ExecuteNonQuery() |> ignore
printfn "  source: %d rows" sources.Length

// --- sites ---
for (id, name, src, region, period) in tnSites do
    use cmd = new SqliteCommand("INSERT INTO site VALUES (@i,@n,@s,@r,@p)", con, tx)
    cmd.Parameters.AddWithValue("@i", id) |> ignore
    cmd.Parameters.AddWithValue("@n", name) |> ignore
    cmd.Parameters.AddWithValue("@s", src) |> ignore
    cmd.Parameters.AddWithValue("@r", region) |> ignore
    cmd.Parameters.AddWithValue("@p", period) |> ignore
    cmd.ExecuteNonQuery() |> ignore
printfn "  site: %d rows" tnSites.Length

// --- Rajan-Sivanantham base signs ---
for (signId, label, variants, composites, total) in rajanSigns do
    use cmd = new SqliteCommand("INSERT INTO base_sign VALUES (@s,@i,@l,@v,@c,@t)", con, tx)
    cmd.Parameters.AddWithValue("@s", "RS2025") |> ignore
    cmd.Parameters.AddWithValue("@i", signId) |> ignore
    cmd.Parameters.AddWithValue("@l", label) |> ignore
    cmd.Parameters.AddWithValue("@v", variants) |> ignore
    cmd.Parameters.AddWithValue("@c", composites) |> ignore
    cmd.Parameters.AddWithValue("@t", total) |> ignore
    cmd.ExecuteNonQuery() |> ignore
printfn "  base_sign (RS2025): %d rows" rajanSigns.Length

// --- Tamil logogram base signs from CSVs ---
let logoDir = Path.Combine(dataDir, "Preprocessing/Converted_Tamil/LogoSyllabic")

let ingestLogograms (file: string) (category: string) =
    let rows = parseCsv (Path.Combine(logoDir, file))
    rows
    |> Array.fold (fun acc line ->
        let fields = splitCsvLine line
        // 4-col: index, label, type, id (lemmas, morphemes, clitics)
        // 3-col: index, label, id (letters, syllables)
        let labelAndId =
            if fields.Length >= 4 then Some (fields.[1], fields.[3], fields.[2])
            elif fields.Length >= 3 then Some (fields.[1], fields.[2], "")
            else None
        match labelAndId with
        | Some (label, signId, pos) ->
            use cmd = new SqliteCommand(
                "INSERT OR IGNORE INTO base_sign VALUES (@s,@i,@l,0,0,0)", con, tx)
            cmd.Parameters.AddWithValue("@s", "TAMIL_TB") |> ignore
            cmd.Parameters.AddWithValue("@i", signId) |> ignore
            cmd.Parameters.AddWithValue("@l",
                if pos <> "" then sprintf "%s [%s] %s" label pos category
                else sprintf "%s %s" label category) |> ignore
            cmd.ExecuteNonQuery() |> ignore
            acc + 1
        | None -> acc) 0

let c1 = ingestLogograms "lemmas_labelled.csv" "lemma"
let c2 = ingestLogograms "morphemes_labelled.csv" "morpheme"
let c3 = ingestLogograms "clitics_and_postpositions_labelled.csv" "clitic"
let c4 = ingestLogograms "Letters_labelled.csv" "letter"
let c5 = ingestLogograms "CV_syllables_labelled.csv" "CV-syllable"
let c6 = ingestLogograms "VC_syllables_labelled.csv" "VC-syllable"
printfn "  base_sign (TAMIL_TB): %d lemmas, %d morphemes, %d clitics, %d letters, %d CV, %d VC"
    c1 c2 c3 c4 c5 c6

// --- Tamil logo-syllabic inscriptions ---
let sentFile = Path.Combine(logoDir, "logo_syllabic_tamil_sentences.csv")
let sentRows = parseCsv sentFile
let mutable inscCount = 0
for line in sentRows do
    let fields = splitCsvLine line
    if fields.Length >= 2 then
        let iid = fields.[0]
        let seq = fields.[1]
        let signs =
            seq.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            |> Array.filter (fun t ->
                t.Length > 0 && t <> "(" && t <> ")" && t <> "," && t <> "." && t <> ":" && t <> ";")
        use cmd = new SqliteCommand(
            "INSERT OR IGNORE INTO inscription VALUES (@i,@s,@q,@c)", con, tx)
        cmd.Parameters.AddWithValue("@i", sprintf "TAMIL_%s" iid) |> ignore
        cmd.Parameters.AddWithValue("@s", "TAMIL_TB") |> ignore
        cmd.Parameters.AddWithValue("@q", seq) |> ignore
        cmd.Parameters.AddWithValue("@c", signs.Length) |> ignore
        cmd.ExecuteNonQuery() |> ignore

        // sign occurrences
        for pos in 0 .. signs.Length - 1 do
            use ocmd = new SqliteCommand(
                "INSERT INTO sign_occurrence (artefact_id, source_code, sign_id, position) VALUES (@a,@s,@i,@p)",
                con, tx)
            ocmd.Parameters.AddWithValue("@a", sprintf "TAMIL_%s" iid) |> ignore
            ocmd.Parameters.AddWithValue("@s", "TAMIL_TB") |> ignore
            ocmd.Parameters.AddWithValue("@i", signs.[pos]) |> ignore
            ocmd.Parameters.AddWithValue("@p", pos) |> ignore
            ocmd.ExecuteNonQuery() |> ignore

        inscCount <- inscCount + 1
printfn "  inscription (TAMIL_TB): %d" inscCount

// --- Mahadevan signs from histogram file names ---
let histDir = Path.Combine(dataDir, "Statistical_Analysis/Histograms2")
if Directory.Exists histDir then
    let signFiles =
        Directory.GetFiles(histDir, "*.png")
        |> Array.map Path.GetFileNameWithoutExtension
        |> Array.distinct
        |> Array.sort
    for signId in signFiles do
        use cmd = new SqliteCommand(
            "INSERT OR IGNORE INTO base_sign VALUES (@s,@i,@l,0,0,0)", con, tx)
        cmd.Parameters.AddWithValue("@s", "MAHADEVAN") |> ignore
        cmd.Parameters.AddWithValue("@i", signId) |> ignore
        cmd.Parameters.AddWithValue("@l", sprintf "Mahadevan sign %s" signId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
    printfn "  base_sign (MAHADEVAN): %d from histogram files" signFiles.Length
else
    printfn "  base_sign (MAHADEVAN): SKIP (no Histograms2 dir)"

// --- CISI data from ~/data/indus_valley.db ---
let ivDbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "data/indus_valley.db")

if File.Exists ivDbPath then
    use ivCon = new SqliteConnection(sprintf "Data Source=%s;Mode=ReadOnly" ivDbPath)
    ivCon.Open()

    // concordance
    let mutable concCount = 0
    use concCmd = new SqliteCommand(
        "SELECT parpola_id, description, mahadevan_ids, wells_ids FROM sign_concordance", ivCon)
    use concRdr = concCmd.ExecuteReader()
    while concRdr.Read() do
        use ins = new SqliteCommand(
            "INSERT OR IGNORE INTO sign_concordance VALUES (@p,@d,@m,@w)", con, tx)
        ins.Parameters.AddWithValue("@p", concRdr.GetString 0) |> ignore
        ins.Parameters.AddWithValue("@d", if concRdr.IsDBNull 1 then box System.DBNull.Value else box (concRdr.GetString 1)) |> ignore
        ins.Parameters.AddWithValue("@m", if concRdr.IsDBNull 2 then box System.DBNull.Value else box (concRdr.GetString 2)) |> ignore
        ins.Parameters.AddWithValue("@w", if concRdr.IsDBNull 3 then box System.DBNull.Value else box (concRdr.GetString 3)) |> ignore
        ins.ExecuteNonQuery() |> ignore
        concCount <- concCount + 1
    concRdr.Close()
    printfn "  sign_concordance: %d rows (from %s)" concCount ivDbPath

    // CISI inscriptions
    let mutable cisiCount = 0
    use cisiCmd = new SqliteCommand(
        "SELECT inscription_id, description, signs, sign_count FROM cisi_inscriptions", ivCon)
    use cisiRdr = cisiCmd.ExecuteReader()
    while cisiRdr.Read() do
        use ins = new SqliteCommand(
            "INSERT OR IGNORE INTO cisi_inscription VALUES (@i,@d,@s,@c,'CISI')", con, tx)
        ins.Parameters.AddWithValue("@i", cisiRdr.GetString 0) |> ignore
        ins.Parameters.AddWithValue("@d", if cisiRdr.IsDBNull 1 then box System.DBNull.Value else box (cisiRdr.GetString 1)) |> ignore
        ins.Parameters.AddWithValue("@s", cisiRdr.GetString 2) |> ignore
        ins.Parameters.AddWithValue("@c", cisiRdr.GetInt32 3) |> ignore
        ins.ExecuteNonQuery() |> ignore
        cisiCount <- cisiCount + 1
    cisiRdr.Close()
    printfn "  cisi_inscription: %d rows (from %s)" cisiCount ivDbPath

    ivCon.Close()
else
    printfn "  CISI: SKIP (%s not found)" ivDbPath

// --- Tamil Treebank CoNLL ---
let conllPath = Path.Combine(dataDir, "../data/tamil-treebank/TamilTB.v0.1.utf8.conll")
let conllPathAlt = Path.Combine(
    Path.GetDirectoryName(Path.GetFullPath dbPath), "data/tamil-treebank/TamilTB.v0.1.utf8.conll")
let conllFile =
    if File.Exists conllPath then Some conllPath
    elif File.Exists conllPathAlt then Some conllPathAlt
    else None

match conllFile with
| Some cf ->
    let lines = File.ReadAllLines(cf)
    let mutable sentId = 0
    let mutable tokenCount = 0
    for line in lines do
        if line.Trim() = "" then
            sentId <- sentId + 1
        elif not (line.StartsWith("#")) then
            let cols = line.Split('\t')
            if cols.Length >= 8 then
                use cmd = new SqliteCommand(
                    "INSERT INTO treebank_token (sentence_id,position,form,lemma,pos,pos_detail,features,head,dep_rel)
                     VALUES (@s,@p,@f,@l,@pos,@pd,@ft,@h,@dr)", con, tx)
                cmd.Parameters.AddWithValue("@s", sentId) |> ignore
                cmd.Parameters.AddWithValue("@p", cols.[0]) |> ignore
                cmd.Parameters.AddWithValue("@f", cols.[1]) |> ignore
                cmd.Parameters.AddWithValue("@l", cols.[2]) |> ignore
                cmd.Parameters.AddWithValue("@pos", cols.[3]) |> ignore
                cmd.Parameters.AddWithValue("@pd", cols.[4]) |> ignore
                cmd.Parameters.AddWithValue("@ft", if cols.Length > 5 then cols.[5] else "") |> ignore
                cmd.Parameters.AddWithValue("@h", if cols.Length > 6 then (box (cols.[6])) else box System.DBNull.Value) |> ignore
                cmd.Parameters.AddWithValue("@dr", if cols.Length > 7 then cols.[7] else "") |> ignore
                cmd.ExecuteNonQuery() |> ignore
                tokenCount <- tokenCount + 1
    printfn "  treebank_token: %d tokens, %d sentences (from %s)" tokenCount sentId cf
| None ->
    printfn "  treebank_token: SKIP (CoNLL file not found)"

// --- Original Tamil sentences + logosyllabic alignment ---
let origPath = Path.Combine(dataDir, "Preprocessing/Converted_Tamil/LogoSyllabic/Original_Tamil_Sentences.csv")
let logoPath = Path.Combine(dataDir, "Preprocessing/Converted_Tamil/LogoSyllabic/logo_syllabic_tamil_sentences.csv")
if File.Exists origPath && File.Exists logoPath then
    let origLines = parseCsv origPath
    let logoLines = parseCsv logoPath
    let mutable sentCount = 0
    for i in 0 .. min origLines.Length logoLines.Length - 1 do
        let origFields = splitCsvLine origLines.[i]
        let logoFields = splitCsvLine logoLines.[i]
        if origFields.Length >= 2 && logoFields.Length >= 2 then
            let tamil = origFields.[1]
            let logo = logoFields.[1]
            let signCount =
                logo.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                |> Array.filter (fun t -> t.Length > 0 && t <> "(" && t <> ")" && t <> "," && t <> "." && t <> ":" && t <> ";")
                |> Array.length
            use cmd = new SqliteCommand(
                "INSERT OR IGNORE INTO tamil_sentence VALUES (@i,@t,@l,@c)", con, tx)
            cmd.Parameters.AddWithValue("@i", i) |> ignore
            cmd.Parameters.AddWithValue("@t", tamil) |> ignore
            cmd.Parameters.AddWithValue("@l", logo) |> ignore
            cmd.Parameters.AddWithValue("@c", signCount) |> ignore
            cmd.ExecuteNonQuery() |> ignore
            sentCount <- sentCount + 1
    printfn "  tamil_sentence: %d rows (original + logosyllabic aligned)" sentCount
else
    printfn "  tamil_sentence: SKIP (Original_Tamil_Sentences.csv not found)"

tx.Commit()
con.Close()

// === VERIFY ==================================================================

use con2 = new SqliteConnection(sprintf "Data Source=%s;Mode=ReadOnly" dbPath)
con2.Open()

let count (table: string) =
    use cmd = new SqliteCommand(sprintf "SELECT COUNT(*) FROM %s" table, con2)
    cmd.ExecuteScalar() :?> int64

printfn ""
printfn "INDUS CORPUS DB: %s" dbPath
printfn "  source           : %d" (count "source")
printfn "  site             : %d" (count "site")
printfn "  base_sign        : %d" (count "base_sign")
printfn "  sign_form        : %d" (count "sign_form")
printfn "  sign_occurrence  : %d" (count "sign_occurrence")
printfn "  inscription      : %d" (count "inscription")
printfn "  morphological_parallel: %d" (count "morphological_parallel")
printfn "  sign_concordance : %d" (count "sign_concordance")
printfn "  cisi_inscription : %d" (count "cisi_inscription")
printfn "  treebank_token   : %d" (count "treebank_token")
printfn "  tamil_sentence   : %d" (count "tamil_sentence")

// sign breakdown by source
printfn ""
printfn "  Signs by source:"
let srcCmd = new SqliteCommand(
    "SELECT source_code, COUNT(*) FROM base_sign GROUP BY source_code ORDER BY source_code", con2)
let rdr = srcCmd.ExecuteReader()
while rdr.Read() do
    printfn "    %-12s : %d" (rdr.GetString 0) (rdr.GetInt64 1)
rdr.Close()

con2.Close()
