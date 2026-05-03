#!/usr/bin/env bash
# Repeatability check for passive Transport Coherence Basin diagnostics.

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
RUN_ROOT="${DOE_ROOT:-$ROOT/output/transport_coherence_basin_repeatability/$TIMESTAMP}"
mkdir -p "$RUN_ROOT"

run_one() {
	local name="$1"
	DOE_ROOT="$RUN_ROOT/$name" bash "$ROOT/scripts/run_transport_coherence_basin_smoke.sh"
}

run_one run_a || exit 1
run_one run_b || exit 1

python3 - "$RUN_ROOT/run_a/scene_transport_memory.json" "$RUN_ROOT/run_b/scene_transport_memory.json" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

a = Path(sys.argv[1])
b = Path(sys.argv[2])
if not a.exists() or not b.exists():
    raise SystemExit("scene_transport_memory.json missing from one repeatability run")

def load(path):
    data = json.loads(path.read_text())
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    return data, digest

ad, ah = load(a)
bd, bh = load(b)
checks = [
    ("basin_count", ad.get("basin_count"), bd.get("basin_count")),
    ("unstable_seam_count", ad.get("unstable_seam_count"), bd.get("unstable_seam_count")),
    ("scene_transport_memory_sha256", ah, bh),
]
failed = False
for name, av, bv in checks:
    print(f"[coherence-repeatability] {name}: {av} vs {bv}")
    if av != bv:
        failed = True
if failed:
    raise SystemExit(1)
PY

echo "[coherence-repeatability] pass output=$RUN_ROOT"
