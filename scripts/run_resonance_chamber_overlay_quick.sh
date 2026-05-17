#!/usr/bin/env bash
set -u

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT" || exit 1

PYTHON_BIN="${PYTHON_BIN:-python3}"
if ! command -v "$PYTHON_BIN" >/dev/null 2>&1; then
  if command -v python >/dev/null 2>&1; then
    PYTHON_BIN="python"
  else
    echo "[resonance_quick] No Python interpreter found (tried python3, python)." >&2
    exit 1
  fi
fi

latest_dir="$(find output -type d -path '*wormhole*' 2>/dev/null | while IFS= read -r dir; do
  if [ -d "$dir/panels" ] || find "$dir" -maxdepth 1 -type f -name '*wormhole*_summary.json' | grep -q .; then
    printf '%s\t%s\n' "$(stat -c '%Y' "$dir" 2>/dev/null || echo 0)" "$dir"
  fi
done | sort -n | tail -1 | cut -f2-)"

if [ -z "${latest_dir:-}" ]; then
  echo "[resonance_quick] No relevant wormhole output directory found under output/." >&2
  exit 1
fi

echo "[resonance_quick] input=$latest_dir"
"$PYTHON_BIN" tools/resonance_chamber_overlay.py --input "$latest_dir"
status=$?
if [ "$status" -ne 0 ]; then
  echo "[resonance_quick] overlay generation failed with status $status" >&2
  exit "$status"
fi

echo "[resonance_quick] generated:"
for artifact in \
  resonance_chamber_overlay.png \
  resonance_chamber_overlay_annotated.png \
  resonance_chamber_summary.md \
  resonance_chamber_metrics.csv
do
  if [ -f "$latest_dir/$artifact" ]; then
    echo "  $latest_dir/$artifact"
  else
    echo "  missing: $latest_dir/$artifact"
  fi
done
