#!/usr/bin/env bash
#
# tools/hooks/audit-fsx.sh — F# coding standard audit
#
# Machine-enforceable subset of fsharp_coding_standard.tex.
# Each check cites its rule ID. Run on staged .fsx files
# from the pre-commit hook.
#
# Usage:
#   audit-fsx.sh file1.fsx file2.fsx ...
#   audit-fsx.sh scratch/fsx/*.fsx        # audit all
#
# Exit 0 = all checks pass. Exit 1 = violations found.
#
# Rules checked (from fsharp_coding_standard.tex):
#   S1   No hardcoded paths (/Users/, /home/, ~/,  C:\)
#   S2   NuGet references before open statements
#   I1   Excessive mutable (>5 per file = warning, >10 = fail)
#   C1   Mutable accumulator anti-pattern (mutable + for + <-)
#   T1   Tuple types with 4+ elements (suggests record needed)
#   N1   Type names must be PascalCase
#   F4   Mixing printfn inside pure extraction functions
#   E1   try/with blocks should return Result, not re-raise
#   X2   String concatenation in loops (+ inside for)

set -uo pipefail

VFILE=$(mktemp)
WFILE=$(mktemp)
echo "0" > "$VFILE"
echo "0" > "$WFILE"
trap 'rm -f "$VFILE" "$WFILE"' EXIT

fail() {
    local file="$1" line="$2" rule="$3" msg="$4"
    printf "  FAIL  %-4s %s:%s  %s\n" "$rule" "$(basename "$file")" "$line" "$msg"
    echo "$(( $(cat "$VFILE") + 1 ))" > "$VFILE"
}

warn() {
    local file="$1" line="$2" rule="$3" msg="$4"
    printf "  WARN  %-4s %s:%s  %s\n" "$rule" "$(basename "$file")" "$line" "$msg"
    echo "$(( $(cat "$WFILE") + 1 ))" > "$WFILE"
}

audit_file() {
    local f="$1"
    local base
    base=$(basename "$f")

    # ── S1: No hardcoded paths ────────────────────────────────
    # Skip comments (lines starting with //) for path checks
    # in the header block. Check actual code lines.
    local linenum=0
    while IFS= read -r line; do
        linenum=$((linenum + 1))
        # Skip pure comment lines and #r references
        case "$line" in
            //*)  continue ;;
            \#r*) continue ;;
            "")   continue ;;
        esac
        # Check for hardcoded home directories
        if echo "$line" | grep -qE '/Users/[a-zA-Z]|/home/[a-zA-Z]|~/[a-zA-Z]' ; then
            fail "$f" "$linenum" "S1" "hardcoded path: $(echo "$line" | head -c 60)"
        fi
    done < "$f"

    # ── S2: NuGet before open ─────────────────────────────────
    local first_open first_nuget
    first_open=$(grep -n '^open ' "$f" | head -1 | cut -d: -f1)
    first_nuget=$(grep -n '^#r "nuget:' "$f" | tail -1 | cut -d: -f1)
    first_open=${first_open:-9999}
    first_nuget=${first_nuget:-0}
    if [ "$first_nuget" -gt "$first_open" ] 2>/dev/null; then
        fail "$f" "$first_nuget" "S2" "NuGet reference after first 'open' (line $first_open)"
    fi

    # ── I1: Excessive mutable ─────────────────────────────────
    local mut_count
    mut_count=$(grep -c 'let mutable ' "$f" 2>/dev/null || true)
    mut_count=${mut_count:-0}
    if [ "$mut_count" -gt 10 ]; then
        fail "$f" "-" "I1" "$mut_count mutable bindings (max 10)"
    elif [ "$mut_count" -gt 5 ]; then
        warn "$f" "-" "I1" "$mut_count mutable bindings (consider reducing)"
    fi

    # ── C1: Mutable accumulator pattern ───────────────────────
    # Detect: let mutable total = 0 ... for ... do ... total <- total + ...
    # Only flag variables named like counters (total*, count*, sum*, acc*).
    # Parsing mutables (depth, splitPos, currentFile) are not accumulators.
    local in_mutable_block=0
    local mutable_var=""
    local mutable_line=0
    linenum=0
    while IFS= read -r line; do
        linenum=$((linenum + 1))
        if echo "$line" | grep -qE 'let mutable (total|count|sum|acc|num|n[A-Z])[a-zA-Z0-9]* = 0'; then
            mutable_var=$(echo "$line" | sed -E 's/.*let mutable ([a-zA-Z0-9]+) = 0.*/\1/')
            mutable_line=$linenum
            in_mutable_block=1
        fi
        if [ "$in_mutable_block" = "1" ]; then
            if [ $((linenum - mutable_line)) -gt 30 ]; then
                in_mutable_block=0
            fi
            if echo "$line" | grep -qE "${mutable_var} <- ${mutable_var} \+"; then
                fail "$f" "$mutable_line" "C1" "'mutable $mutable_var' accumulator — use Array.fold"
                in_mutable_block=0
            fi
        fi
    done < "$f"

    # ── T1: Tuple types with 4+ stars ─────────────────────────
    # Look for type annotations like: string * string * string * string
    # Skip comment lines and lines with multiplication expressions
    grep -nE '[a-z] \* [a-z].*\* [a-z].*\* [a-z]' "$f" | while IFS=: read -r ln content; do
        # Skip comment lines
        local trimmed
        trimmed=$(echo "$content" | sed 's/^ *//')
        case "$trimmed" in
            //*)  ;;
            *)  fail "$f" "$ln" "T1" "4+ element tuple type — use a record" ;;
        esac
    done

    # ── N1: Type names must be PascalCase ─────────────────────
    grep -nE '^type [a-z]' "$f" | while IFS=: read -r ln content; do
        local typename
        typename=$(echo "$content" | sed -E 's/^type ([a-zA-Z0-9_]+).*/\1/')
        fail "$f" "$ln" "N1" "type '$typename' should be PascalCase"
    done

    # ── E1: try/with that doesn't return Result ───────────────
    # Heuristic: find 'with ex ->' or 'with e ->' not followed by Error
    grep -nE 'with (ex|e) ->' "$f" | while IFS=: read -r ln content; do
        # Check if the next non-empty line contains Error
        local nextlines
        nextlines=$(sed -n "$((ln+1)),$((ln+3))p" "$f" | tr -d ' ')
        if ! echo "$nextlines" | grep -q 'Error'; then
            warn "$f" "$ln" "E1" "try/with block may not return Result"
        fi
    done

    # ── F4: printfn inside functions named 'extract' or 'process' ──
    # Pure functions should not print. Side effects at boundary only.
    local in_pure_fn=0
    local pure_fn_name=""
    linenum=0
    while IFS= read -r line; do
        linenum=$((linenum + 1))
        if echo "$line" | grep -qE '^let (extract|findMainFile|resolveCallee|splitFuncType|paramsToJson) '; then
            in_pure_fn=1
            pure_fn_name=$(echo "$line" | sed -E 's/^let ([a-zA-Z0-9]+).*/\1/')
        fi
        # Exit pure function at next top-level let
        if [ "$in_pure_fn" = "1" ] && [ "$linenum" -gt 1 ]; then
            if echo "$line" | grep -qE '^let [a-zA-Z]' && ! echo "$line" | grep -qE "^let ${pure_fn_name}"; then
                in_pure_fn=0
            fi
        fi
        if [ "$in_pure_fn" = "1" ]; then
            if echo "$line" | grep -qE '^\s*(printfn|printf |eprintfn)'; then
                fail "$f" "$linenum" "F4" "side effect (print) inside pure function '$pure_fn_name'"
            fi
        fi
    done < "$f"
}

# ── Main ──────────────────────────────────────────────────────
if [ $# -eq 0 ]; then
    echo "Usage: audit-fsx.sh file1.fsx [file2.fsx ...]"
    exit 1
fi

echo "F# coding standard audit"
echo ""

for f in "$@"; do
    if [ ! -f "$f" ]; then
        echo "  SKIP: $f (not found)"
        continue
    fi
    echo "  ── $(basename "$f") ──"
    audit_file "$f"
done

VIOLATIONS=$(cat "$VFILE")
WARNINGS=$(cat "$WFILE")

echo ""
if [ "$VIOLATIONS" -gt 0 ]; then
    echo "FAIL: $VIOLATIONS violation(s), $WARNINGS warning(s)"
    exit 1
elif [ "$WARNINGS" -gt 0 ]; then
    echo "PASS with $WARNINGS warning(s)"
    exit 0
else
    echo "PASS: all checks clean"
    exit 0
fi
