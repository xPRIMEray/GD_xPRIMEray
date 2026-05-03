#!/usr/bin/env bash
# Focused corner/edge transport accuracy probe for xPRIMEray.

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
OUTPUT_DIR="${DOE_ROOT:-$ROOT/output/corner_transport_probe/$TIMESTAMP}"

FRAMES="${DOE_FRAMES:-5}"
WARMUP="${DOE_WARMUP:-0}"
RES="${DOE_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
STRIDE="${DOE_STRIDE:-4}"
MAX_ROIS="${CORNER_PROBE_MAX_ROIS:-8}"
PATCH_SIZE="${CORNER_PROBE_PATCH_SIZE:-17}"
STEPS="${CORNER_PROBE_STEPS:-0.03,0.025,0.02,0.018,0.016,0.015,0.014,0.013,0.0125,0.011,0.010,0.0075,0.00625,0.003125}"
RADII="${CORNER_PROBE_RADII:-2,4,8,16,32}"
MAX_STEPS="${CORNER_PROBE_MAX_STEPS:-4096}"
MANUAL_ROIS="${CORNER_PROBE_MANUAL_ROIS:-}"

if [[ "${CORNER_PROBE_SMOKE:-0}" == "1" ]]; then
	MAX_ROIS=1
	PATCH_SIZE=9
	STEPS="0.02,0.0125,0.003125"
	RADII="2,4,8"
	MAX_STEPS="${CORNER_PROBE_MAX_STEPS:-2048}"
fi

mkdir -p "$OUTPUT_DIR"

echo "[corner-probe] output=$OUTPUT_DIR"
echo "[corner-probe] rois=$MAX_ROIS patch=$PATCH_SIZE steps=$STEPS radii=$RADII"
dotnet build "$ROOT" || exit 1

set +e
"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
	--render-test \
	--domain-audit-quick \
	"--render-test-fixture=$FIXTURE" \
	--render-test-capture=1 \
	"--render-test-capture-dir=$OUTPUT_DIR" \
	--render-test-capture-mode=corner_transport_probe \
	"--render-test-frames=$FRAMES" \
	"--render-test-warmup=$WARMUP" \
	"--render-test-film-width=$FILM_W" \
	"--render-test-film-height=$FILM_H" \
	--render-test-film-scale=1.0 \
	--render-test-camera-fixed=1 \
	--render-test-step-length=0.015 \
	"--render-test-pixel-stride=$STRIDE" \
	--reference-geodesic-probe=1 \
	"--reference-geodesic-probe-max-anchors=$MAX_ROIS" \
	"--reference-geodesic-probe-max-steps=$MAX_STEPS" \
	--corner-transport-probe=1 \
	"--corner-transport-probe-max-rois=$MAX_ROIS" \
	"--corner-transport-probe-patch-size=$PATCH_SIZE" \
	"--corner-transport-probe-steps=$STEPS" \
	"--corner-transport-probe-radii=$RADII" \
	"--corner-transport-probe-manual-rois=$MANUAL_ROIS" \
	> "$OUTPUT_DIR/run.log" 2>&1
exit_code=$?
set -e

effective="$exit_code"
if [[ "$exit_code" -eq 0 ]] || grep -q "\[RenderTestRunner\]\[ExitCode\] forced=0 reason=harness_success" "$OUTPUT_DIR/run.log" 2>/dev/null; then
	effective=0
fi

echo "$exit_code" > "$OUTPUT_DIR/status.txt"
echo "$effective" > "$OUTPUT_DIR/effective_status.txt"
"$PYTHON" "$ROOT/tools/corner_transport_probe_analyzer.py" "$OUTPUT_DIR" >> "$OUTPUT_DIR/run.log" 2>&1 || effective=1

echo "[corner-probe] exit=$exit_code effective=$effective"
echo "[corner-probe] outputs:"
find "$OUTPUT_DIR" -maxdepth 1 \( -name 'corner_transport_probe.csv' -o -name 'corner_transport_probe.json' -o -name 'corner_*.png' -o -name 'corner_threshold_report.md' \) -print | sort

exit "$effective"
