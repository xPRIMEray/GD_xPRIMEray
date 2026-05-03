#!/usr/bin/env bash
# Tiny repeatability smoke for row/tile beauty hashes.

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PYTHON="${TILE_COMMIT_TRAVERSAL_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUT="${TILE_COMMIT_REPEAT_ROOT:-$ROOT/output/tile_commit_traversal_repeatability/$TIMESTAMP}"
mkdir -p "$OUT"

run_once() {
	local label="$1"
	TILE_COMMIT_TRAVERSAL_SMOKE=1 \
	TILE_COMMIT_TRAVERSAL_SKIP_CORNER=1 \
	TILE_COMMIT_TRAVERSAL_ROOT="$OUT/$label" \
	bash "$ROOT/scripts/run_tile_commit_traversal_comparison.sh" > "$OUT/$label.log" 2>&1
}

run_once run_a
run_once run_b

"$PYTHON" - <<'PY' "$OUT/run_a/tile_commit_traversal_summary.csv" "$OUT/run_b/tile_commit_traversal_summary.csv" "$OUT/repeatability_summary.json"
import csv, json, sys
from pathlib import Path

def hashes(path):
    rows = list(csv.DictReader(Path(path).open()))
    return {(r["step_length"], r["traversal"]): r["beauty_hash"] for r in rows if r["traversal"] in {"row", "tile"}}

a = hashes(sys.argv[1])
b = hashes(sys.argv[2])
keys = sorted(set(a) | set(b))
mismatches = [{"step_length": k[0], "traversal": k[1], "run_a": a.get(k, ""), "run_b": b.get(k, "")} for k in keys if a.get(k) != b.get(k)]
payload = {"passed": not mismatches, "checked": len(keys), "mismatches": mismatches}
Path(sys.argv[3]).write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n")
print(json.dumps(payload, sort_keys=True))
sys.exit(0 if not mismatches else 1)
PY
