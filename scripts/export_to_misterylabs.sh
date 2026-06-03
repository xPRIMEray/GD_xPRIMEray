#!/usr/bin/env bash
# export_to_misterylabs.sh — Promote a curated artifact from output/ into misterylabs_artifacts/
#
# Usage:
#   ./scripts/export_to_misterylabs.sh <output_folder_name> [--png <image_file>] [--csv <csv_file>] [--validate <md_file>]
#
# Examples:
#   # Copy the card README only
#   ./scripts/export_to_misterylabs.sh wormhole_DR_Story
#
#   # Copy the card README + a specific PNG
#   ./scripts/export_to_misterylabs.sh wormhole_DR_Story --png latest/wormhole_dual_reality_storytelling_contact_sheet.png
#
#   # Copy card + dataset CSV + validation report
#   ./scripts/export_to_misterylabs.sh doe_overnight \
#     --csv 20260502T060652Z/DOE_overnight_summary.csv \
#     --validate 20260502T060652Z/DOE_overnight_summary.md
#
# Rules enforced:
#   - PNGs must be under 2 MB (configurable via MAX_PNG_BYTES).
#   - CSVs must be under 500 KB.
#   - No *.log, *.jsonl, *_oracle_segments.csv, *_parent_paths.*, *_hit_diagnostics.* allowed.
#   - Source folder must exist under output/ and have a README.md.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_ROOT="$ROOT/output"
ARTIFACT_ROOT="$ROOT/misterylabs_artifacts"
MAX_PNG_BYTES=$((2 * 1024 * 1024))   # 2 MB
MAX_CSV_BYTES=$((500 * 1024))         # 500 KB

red()    { printf '\033[0;31m%s\033[0m\n' "$*"; }
green()  { printf '\033[0;32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[0;33m%s\033[0m\n' "$*"; }

usage() {
    echo "Usage: $0 <output_folder_name> [--png <rel_path>] [--csv <rel_path>] [--validate <rel_path>]"
    echo ""
    echo "  <output_folder_name>   Folder name under output/ (e.g. wormhole_DR_Story)"
    echo "  --png <rel_path>       Path relative to the output folder to copy to visuals/"
    echo "  --csv <rel_path>       Path relative to the output folder to copy to datasets/"
    echo "  --validate <rel_path>  Path relative to the output folder to copy to validation/"
    echo ""
    echo "The card README (output/<folder>/README.md) is always copied to cards/."
    exit 1
}

[[ $# -lt 1 ]] && usage

FOLDER_NAME="$1"; shift
SOURCE_DIR="$OUTPUT_ROOT/$FOLDER_NAME"

if [[ ! -d "$SOURCE_DIR" ]]; then
    red "ERROR: output/$FOLDER_NAME does not exist."
    exit 1
fi

if [[ ! -f "$SOURCE_DIR/README.md" ]]; then
    red "ERROR: output/$FOLDER_NAME/README.md not found. Run the artifact README system first."
    exit 1
fi

SLUG="${FOLDER_NAME//_/-}"
PNG_FILE=""
CSV_FILE=""
VALIDATE_FILE=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --png)      PNG_FILE="$2"; shift 2 ;;
        --csv)      CSV_FILE="$2"; shift 2 ;;
        --validate) VALIDATE_FILE="$2"; shift 2 ;;
        *) red "Unknown argument: $1"; usage ;;
    esac
done

# ─── Guard: block known large/dangerous patterns ───────────────────────────
check_blocked() {
    local file="$1"
    local basename
    basename="$(basename "$file")"
    if [[ "$basename" == *_oracle_segments.csv || \
          "$basename" == *_parent_paths.* || \
          "$basename" == *_hit_diagnostics.csv || \
          "$basename" == *_escape_vectors.csv || \
          "$basename" == *.jsonl || \
          "$basename" == *.log || \
          "$basename" == *.so || \
          "$basename" == *.pyc || \
          "$basename" == *.exe ]]; then
        red "BLOCKED: $basename matches a known large/binary pattern and cannot be promoted."
        red "  Attach it to a GitHub Release instead: gh release upload <tag> \"$file\""
        exit 1
    fi
}

# ─── Copy card README ───────────────────────────────────────────────────────
CARD_DEST="$ARTIFACT_ROOT/cards/$SLUG.md"
cp "$SOURCE_DIR/README.md" "$CARD_DEST"
green "Card:     cards/$SLUG.md"

# ─── Copy PNG if requested ──────────────────────────────────────────────────
if [[ -n "$PNG_FILE" ]]; then
    SRC="$SOURCE_DIR/$PNG_FILE"
    if [[ ! -f "$SRC" ]]; then
        red "ERROR: PNG not found: output/$FOLDER_NAME/$PNG_FILE"
        exit 1
    fi
    check_blocked "$SRC"
    FSIZE=$(stat -c%s "$SRC" 2>/dev/null || stat -f%z "$SRC")
    if [[ "$FSIZE" -gt "$MAX_PNG_BYTES" ]]; then
        red "ERROR: PNG is $(( FSIZE / 1024 ))KB — exceeds 2MB limit."
        red "  Resize with: convert \"$SRC\" -resize 1920x1080\\> \"$ARTIFACT_ROOT/visuals/$SLUG.png\""
        exit 1
    fi
    DEST="$ARTIFACT_ROOT/visuals/$SLUG.png"
    cp "$SRC" "$DEST"
    green "Visual:   visuals/$SLUG.png  ($(( FSIZE / 1024 ))KB)"
fi

# ─── Copy CSV dataset if requested ─────────────────────────────────────────
if [[ -n "$CSV_FILE" ]]; then
    SRC="$SOURCE_DIR/$CSV_FILE"
    if [[ ! -f "$SRC" ]]; then
        red "ERROR: CSV not found: output/$FOLDER_NAME/$CSV_FILE"
        exit 1
    fi
    check_blocked "$SRC"
    FSIZE=$(stat -c%s "$SRC" 2>/dev/null || stat -f%z "$SRC")
    if [[ "$FSIZE" -gt "$MAX_CSV_BYTES" ]]; then
        red "ERROR: CSV is $(( FSIZE / 1024 ))KB — exceeds 500KB limit."
        red "  Consider subsetting to key rows and re-running."
        exit 1
    fi
    DEST="$ARTIFACT_ROOT/datasets/$SLUG.csv"
    cp "$SRC" "$DEST"
    green "Dataset:  datasets/$SLUG.csv  ($(( FSIZE / 1024 ))KB)"
fi

# ─── Copy validation report if requested ───────────────────────────────────
if [[ -n "$VALIDATE_FILE" ]]; then
    SRC="$SOURCE_DIR/$VALIDATE_FILE"
    if [[ ! -f "$SRC" ]]; then
        red "ERROR: File not found: output/$FOLDER_NAME/$VALIDATE_FILE"
        exit 1
    fi
    check_blocked "$SRC"
    DEST="$ARTIFACT_ROOT/validation/$SLUG.md"
    cp "$SRC" "$DEST"
    FSIZE=$(stat -c%s "$SRC" 2>/dev/null || stat -f%z "$SRC")
    green "Validate: validation/$SLUG.md  ($(( FSIZE / 1024 ))KB)"
fi

echo ""
yellow "Next: Update misterylabs_artifacts/manifest.json — set \"in_misterylabs\": true for \"$SLUG\"."
yellow "Then commit: git add misterylabs_artifacts/ && git commit -m 'promote $FOLDER_NAME artifact'"
