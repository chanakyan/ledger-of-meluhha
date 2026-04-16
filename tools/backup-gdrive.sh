#!/usr/bin/env bash
#
# tools/backup-gdrive.sh — ledger-of-meluhha
#
# Tars source files (no .git, no build artifacts, no generated DBs)
# and drops a single tarball into Google Drive. Overwrites every run.
# Designed for launchd daily schedule.
#
# What's backed up: everything git tracks (the 16 source files).
# What's excluded: .git/, build/, *.db, *.pdf, *.aux, *.log, etc.
#
# Manual run:
#   tools/backup-gdrive.sh

set -euo pipefail

REPO="$HOME/ledger-of-meluhha"
GDRIVE="$HOME/Library/CloudStorage/GoogleDrive-vrajeshkumar@gmail.com/My Drive/Data/Indus"
TARBALL="ledger-of-meluhha-backup.tar.gz"

if [ ! -d "$REPO/.git" ]; then
    echo "ERROR: $REPO is not a git repo" >&2
    exit 1
fi

if [ ! -d "$GDRIVE" ]; then
    echo "ERROR: Google Drive not mounted at $GDRIVE" >&2
    exit 1
fi

cd "$REPO"

# Tar only git-tracked files — cleanest way to exclude all generated artifacts
git ls-files -z | gtar --null -T - -czf "/tmp/$TARBALL"

mv -f "/tmp/$TARBALL" "$GDRIVE/$TARBALL"

echo "$(gdate -Iseconds)  backed up $(git ls-files | wc -l | tr -d ' ') files to $GDRIVE/$TARBALL"
