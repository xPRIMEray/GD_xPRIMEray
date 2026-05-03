#!/usr/bin/env bash
# Cheap smoke for passive Transport Coherence Basin diagnostics.

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${DOE_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi
SCENE="${DOE_SCENE:-res://test-domain-resolver-stress.tscn}"
FIXTURE="${DOE_FIXTURE:-domain_resolver_stress}"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${DOE_ROOT:-$ROOT/output/transport_coherence_basin_smoke/$TIMESTAMP}"

mkdir -p "$OUTPUT_DIR"

echo "[coherence-basin-smoke] output=$OUTPUT_DIR"
dotnet build "$ROOT" || exit 1

set +e
"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
	--render-test \
	--domain-audit-quick \
	"--render-test-fixture=$FIXTURE" \
	--render-test-capture=1 \
	"--render-test-capture-dir=$OUTPUT_DIR" \
	--render-test-capture-mode=transport_coherence_basin_smoke \
	--render-test-frames=5 \
	--render-test-warmup=0 \
	--render-test-film-width=320 \
	--render-test-film-height=180 \
	--render-test-film-scale=1.0 \
	--render-test-camera-fixed=1 \
	--render-test-step-length=0.015 \
	--render-test-pixel-stride=4 \
	--reference-geodesic-probe=1 \
	--reference-geodesic-probe-max-anchors="${DOE_COHERENCE_MAX_ANCHORS:-2}" \
	--reference-geodesic-probe-max-steps="${DOE_COHERENCE_MAX_STEPS:-2048}" \
	--transport-coherence-basin=1 \
	--transport-coherence-basin-max-centers="${DOE_COHERENCE_MAX_CENTERS:-8}" \
	--transport-coherence-basin-radii="${DOE_COHERENCE_RADII:-4,8,16}" \
	> "$OUTPUT_DIR/run.log" 2>&1
exit_code=$?
set -e

effective="$exit_code"
if [[ "$exit_code" -eq 0 ]] || grep -q "\[RenderTestRunner\]\[ExitCode\] forced=0 reason=harness_success" "$OUTPUT_DIR/run.log" 2>/dev/null; then
	effective=0
fi

echo "$exit_code" > "$OUTPUT_DIR/status.txt"
echo "$effective" > "$OUTPUT_DIR/effective_status.txt"
"$PYTHON" "$ROOT/tools/reference_probe_analyzer.py" "$OUTPUT_DIR" >> "$OUTPUT_DIR/run.log" 2>&1 || effective=1

echo "[coherence-basin-smoke] exit=$exit_code effective=$effective"
echo "[coherence-basin-smoke] diagnostics:"
find "$OUTPUT_DIR" -maxdepth 1 \( -name '*.reference_geodesic_probe.csv' -o -name 'transport_coherence_basins.csv' -o -name 'unstable_seams.csv' -o -name 'scene_transport_memory.json' \) -print

exit "$effective"
