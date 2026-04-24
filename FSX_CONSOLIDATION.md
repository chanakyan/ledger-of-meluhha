# fsx consolidation — agenda

Branch: `claude/consolidate-fsx-scripts-tiDlH`
Approach: scripts live in ONE place (`fsx/`); symlinks from old
locations point to the canonical file.

Full cross-repo audit (including qnx-micro's alloy-fsx inlining) is
in the qnx-micro repo at `FSX_CONSOLIDATION.md` on the same branch.

## In scope (this repo)

| path                          | lines | note                        |
|-------------------------------|------:|-----------------------------|
| `IndusCodebookTypes.fsx`      |   110 | shared via `#load`          |
| `indus_bernoulli.fsx`         |   179 |                             |
| `indus_decoder.fsx`           |   132 | `#load IndusCodebookTypes`  |
| `indus_ingest_corpus.fsx`     |   726 | big — may trigger I1 warn   |
| `indus_lssc.fsx`              |   274 |                             |
| `indus_seed_codebook.fsx`     |   267 |                             |
| `indus_tn_decoder.fsx`        |   274 | `#load IndusCodebookTypes`  |

`hydra/` — self-contained SqlHydra project (`.fsproj` + `.toml`),
leave untouched.

## Target layout

```
fsx/
├── IndusCodebookTypes.fsx
├── indus_bernoulli.fsx
├── indus_decoder.fsx
├── indus_ingest_corpus.fsx
├── indus_lssc.fsx
├── indus_seed_codebook.fsx
└── indus_tn_decoder.fsx
```

Root-level symlinks (one per file) preserve existing callsites.

## Callsites that reference the old paths

- `CMakeLists.txt:200,211,228,239,246` — `${SRC}/indus_*.fsx`. If
  `${SRC}` is `${CMAKE_SOURCE_DIR}`, root symlinks make this work;
  otherwise update the five targets.
- `tools/hooks/audit-fsx.sh:11` — example path `scratch/fsx/*.fsx`
  in the header comment. Optional: update to `fsx/*.fsx`.
- `tools/hooks/pre-commit:28` — matches staged `.fsx` via regex,
  no change needed.
- `*.tex` files — check for literal script paths in prose.

## Name collision

`IndusCodebookTypes.fsx` exists at repo root (110 lines) AND in
`hydra/` (83 lines). `diff` them before symlinking — they may have
diverged. If they have, pick one canonical copy and update the
`#load` in `hydra/` accordingly.

## Commit sequence

1. `mkdir fsx`
2. `git mv <file> fsx/<file>` per file
3. `ln -s fsx/<file> <file>` per preserved callsite  (at repo root)
4. Fix internal `#load` paths in moved files if needed
5. Stage, commit. Pre-commit T0 runs F# coding-standard audit on
   every moved `.fsx`; expect an I1 warning on
   `indus_ingest_corpus.fsx` (726 lines). Warning, not failure.
6. Push

## Gotchas

- `git mv file fsx/file && ln -s fsx/file file` — that order, so the
  `mv` shows as rename, then the symlink lands as a new file.
- macOS: `core.symlinks=true` should already be on.
- No `--no-verify` — the pre-commit audit must pass.
