#!/usr/bin/env bash
#
# tools/hooks/install.sh — ledger-of-meluhha
#
# Run once after cloning:
#   tools/hooks/install.sh

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
HOOKS_SRC="${REPO_ROOT}/tools/hooks"
HOOKS_DST="${REPO_ROOT}/.git/hooks"

if [ ! -d "${HOOKS_DST}" ]; then
    echo "install.sh: ${HOOKS_DST} does not exist (not a git repo?)" >&2
    exit 1
fi

linked=0
for src in "${HOOKS_SRC}"/*; do
    name="$(basename "${src}")"
    [ "${name}" = "install.sh" ] && continue
    [ ! -f "${src}" ] && continue

    ln -sf "../../tools/hooks/${name}" "${HOOKS_DST}/${name}"
    chmod +x "${src}"
    echo "  ${name}  -> tools/hooks/${name}"
    linked=$((linked + 1))
done

echo "install.sh: linked ${linked} hook(s) into .git/hooks/"
