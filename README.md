# The Ledger of Meluhha

**Indus Valley Script as Metrological Accounting Code**

Working paper + reproducibility artifact.

[![License: BSD-2-Clause (human)](https://img.shields.io/badge/license-BSD--2--Clause-blue.svg)](LICENSE-AI.tex)
[![AI use: prohibited](https://img.shields.io/badge/AI%20use-prohibited-red.svg)](ai.txt)
[![F#](https://img.shields.io/badge/F%23-dotnet%20fsi-378BBA.svg)](https://fsharp.org)
[![Alloy 6](https://img.shields.io/badge/Alloy%206-16%20UNSAT-success.svg)](spec/indus_corpus.als)

---

## What

The Indus Valley script (c. 2600–1900 BCE) is a metrological cargo-tag system, not a phonetic language. Five fields per inscription: merchant mark, commodity class, weight tier, quantity multiplier, trade route.

Two Mohenjo-daro seals (M-52A, M-148A) decoded end-to-end from real CISI sign-sequence data through the Parpola–Mahadevan concordance into a metrological codebook. Both produce jar goods as commodity — independently the most frequent sign in the Mahadevan corpus (~10% of occurrences). M-52A produces a Mesopotamia route marker, consistent with its findspot at the primary IVC export hub.

Mass decode across 179 inscriptions: 65% commodity assignment, 21% route assignment.

Independent research. No peer-review track. Verification protocol is in §For Journalists of the paper: any quantitative claim is reproducible from the SQLite database in under ten minutes using `sqlite3` and no credentials.

## Run it

Prereqs: .NET SDK 8+, `sqlite3` on PATH. No other dependencies.

```bash
git clone https://github.com/chanakyan/ledger-of-meluhha
cd ledger-of-meluhha

# The frozen-types decoder — no DB needed at runtime
dotnet fsi indus_decoder.fsx

# The cross-corpus decoder against the SQLite corpus
dotnet fsi indus_tn_decoder.fsx indus_corpus.db

# The independence argument: analytic + Monte Carlo 10^7 + Fisher + Bayes
dotnet fsi indus_bernoulli.fsx
```

Query the data directly without F#:

```bash
sqlite3 indus_codebook.db "SELECT * FROM sign_role WHERE role='commodity'"
sqlite3 indus_corpus.db   "SELECT * FROM morphological_parallel"
sqlite3 indus_lssc.db     "SELECT class, COUNT(*), ROUND(AVG(entropy),4)
                           FROM sign_entropy GROUP BY class"
```

## The F# architecture

**Frozen typed codebook.** `IndusCodebookTypes.fsx` is a once-generated SqlHydra v4 product: typed F# records for 28 sign roles, 8 commodities, 5 routes, 11 weight tiers, 5 merchant marks. The decoder `#load`s the file and runs with zero database dependency at runtime. The DB is regenerated only when the codebook schema changes.

**Read path / write path separation.** Raw ADO.NET SQL lives only in the `*_to_sqlite.fsx` ingest scripts (the write path). All decoder code uses SqlHydra-generated typed queries from `hydra/`. The split is enforced by `tools/hooks/audit-fsx.sh` (pre-commit and pre-push).

**Alloy 6 verification before SQL.** `spec/indus_corpus.als` ships 16 assertions against the schema. All UNSAT at scope 6. SQL was written only after the schema was machine-verified consistent.

**Bernoulli independence.** `indus_bernoulli.fsx` runs the joint-coincidence argument: analytic, Monte Carlo (10^7 trials), Fisher's combined probability test, Bayesian update, and sensitivity analysis. Joint probability that the five decode convergences are coincidental under generous null bounds: 3.1 × 10^-5.

**Single-file dashboard.** `ledger-of-meluhha.html` (also `src/ledger.ts` for the TypeScript source) renders the trade network, decodes seals live, filters routes by commodity. Drop `indus_corpus.db` onto the page; no server, no build step.

## Repo layout

```
ledger_of_meluhha.tex          The paper. LuaLaTeX. 22 pages.
README.tex                     Long-form README (this file is the short version).
description.txt                One-screen summary.

IndusCodebookTypes.fsx         Frozen typed codebook records (read path).
indus_decoder.fsx              Cargo-tag decoder. No args, no DB.
indus_tn_decoder.fsx           Tamil Nadu cross-corpus decoder.
indus_ingest_corpus.fsx        Builds indus_corpus.db from sources (write path).
indus_lssc.fsx                 Transition entropy, LSSC scores.
indus_bernoulli.fsx            Independence argument.
indus_seed_codebook.fsx        Seeds indus_codebook.db (write path).

hydra/                         SqlHydra v4 config + generated .fs types.
spec/indus_corpus.als          Alloy 6 spec — 16 assertions, all UNSAT @ scope 6.
spec/fsharp/, spec/latex/, ... AI instruction contracts (coding standards).

indus_codebook.db              Codebook (frozen).
indus_corpus.db                Normalised corpus, 2,373 signs across 5 sources.
indus_lssc.db                  Latent-structural-state-contraction data.

data/morphological-parallels/  Tamil Nadu ↔ IVC parallels from Rajan & Sivanantham.
data/tamil-treebank/           Tamil Treebank v0.1 (control corpus).
data/source-papers/            Reference paper TeX sources.

ledger-of-meluhha.html         Interactive dashboard (single HTML file).
src/ledger.ts                  TypeScript source for the dashboard.

tools/hooks/                   pre-commit + audit script enforcing read/write split.
CMakeLists.txt                 Build orchestration with loud dependency checks.
```

## Compile the paper

Local (LuaLaTeX required):

```bash
lualatex ledger_of_meluhha.tex
lualatex ledger_of_meluhha.tex   # for cross-refs
```

Overleaf:

1. Upload `ledger_of_meluhha.tex` and `citations.lua`.
2. Menu → Compiler → **LuaLaTeX**.
3. Recompile.

## Citation

```
Venugopal, R. (2026). The Ledger of Meluhha: Indus Valley
Script as Metrological Accounting Code. Working Paper.
Third Buyer Advisory LLC.
```

DOI: see Zenodo deposit.

## License

**Human use:** BSD-2-Clause. Permissive, no notification required, attribution requested.

**AI use:** prohibited. No artificial intelligence system, agent, crawler, or pipeline may access, clone, copy, train on, fine-tune, RAG-index, or run inference against this content — commercial or non-commercial — without a separate written license from Rajeshkumar Venugopal / Third Buyer Advisory. See `ai.txt`, `robots.txt`, and `LICENSE-AI.tex`. Violation is breach of contract, copyright infringement, and unauthorised access under 18 U.S.C. § 1030 (CFAA).

Third-party data: Rajan and Sivanantham (2025, 2026) — fair academic use. Tamil Treebank v0.1 — CC BY-NC-SA 3.0 (Ramasamy & Žabokrtský 2011).

## Contact

Rajeshkumar Venugopal — vrajeshkumar@gmail.com — Third Buyer Advisory LLC, Waterford, Michigan.
