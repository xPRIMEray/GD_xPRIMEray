#!/usr/bin/env bash
# Compare render-test first-pass traversal order without changing hit/shading math.
#
# Full run:
#   bash scripts/run_first_pass_traversal_comparison.sh
#
# Smoke:
#   TRAVERSAL_SMOKE=1 bash scripts/run_first_pass_traversal_comparison.sh

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCENE="${TRAVERSAL_SCENE:-res://test-domain-resolver-stress.tscn}"
FIXTURE="${TRAVERSAL_FIXTURE:-domain_resolver_stress}"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${TRAVERSAL_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${TRAVERSAL_ROOT:-$ROOT/output/first_pass_traversal_comparison/$TIMESTAMP}"
if [[ "$OUTPUT_DIR" != /* ]]; then
	OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

FRAMES="${TRAVERSAL_FRAMES:-90}"
WARMUP="${TRAVERSAL_WARMUP:-5}"
RES="${TRAVERSAL_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
FILM_SCALE="${TRAVERSAL_FILM_SCALE:-1.0}"
PIXEL_STRIDE="${TRAVERSAL_PIXEL_STRIDE:-1}"
STEPS_RAW="${TRAVERSAL_STEPS:-0.018 0.015 0.0125 0.00625}"
MODES_RAW="${TRAVERSAL_MODES:-row column tile checkerboard}"
SMOKE="${TRAVERSAL_SMOKE:-0}"
DIAGNOSTIC_WIREFRAME_OVERLAY="${DIAGNOSTIC_WIREFRAME_OVERLAY:-0}"
DIAGNOSTIC_WIREFRAME_CARTESIAN="${DIAGNOSTIC_WIREFRAME_CARTESIAN:-$DIAGNOSTIC_WIREFRAME_OVERLAY}"
DIAGNOSTIC_WIREFRAME_TRANSPORT="${DIAGNOSTIC_WIREFRAME_TRANSPORT:-$DIAGNOSTIC_WIREFRAME_OVERLAY}"
DIAGNOSTIC_WIREFRAME_RISK="${DIAGNOSTIC_WIREFRAME_RISK:-$DIAGNOSTIC_WIREFRAME_OVERLAY}"
DIAGNOSTIC_WIREFRAME_SPACETIME="${DIAGNOSTIC_WIREFRAME_SPACETIME:-0}"
DIAGNOSTIC_WIREFRAME_LABELS="${DIAGNOSTIC_WIREFRAME_LABELS:-$DIAGNOSTIC_WIREFRAME_OVERLAY}"
DIAGNOSTIC_WIREFRAME_MANUAL_ROIS="${DIAGNOSTIC_WIREFRAME_MANUAL_ROIS:-40,35;280,35;40,145;280,145}"

CORNER_ROIS="${CORNER_PROBE_MANUAL_ROIS:-40,35;280,35;40,145;280,145}"
CORNER_STEPS="${CORNER_PROBE_STEPS:-0.02,0.0125,0.003125}"
CORNER_RADII="${CORNER_PROBE_RADII:-2,4,8,16}"
CORNER_PATCH="${CORNER_PROBE_PATCH_SIZE:-9}"
CORNER_MAX_ROIS="${CORNER_PROBE_MAX_ROIS:-4}"
CORNER_MAX_STEPS="${CORNER_PROBE_MAX_STEPS:-2048}"

if [[ "$SMOKE" == "1" ]]; then
	FRAMES="${TRAVERSAL_FRAMES:-5}"
	WARMUP="${TRAVERSAL_WARMUP:-0}"
	STEPS_RAW="${TRAVERSAL_STEPS:-0.015}"
	MODES_RAW="${TRAVERSAL_MODES:-row column}"
	CORNER_RADII="${CORNER_PROBE_RADII:-2,4}"
	CORNER_PATCH="${CORNER_PROBE_PATCH_SIZE:-9}"
	CORNER_MAX_ROIS="${CORNER_PROBE_MAX_ROIS:-1}"
	CORNER_MAX_STEPS="${CORNER_PROBE_MAX_STEPS:-1024}"
fi

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/traversal_comparison.log"
exec > >(tee -a "$LOG") 2>&1

echo "[traversal] output=$OUTPUT_DIR"
echo "[traversal] frames=$FRAMES warmup=$WARMUP res=${FILM_W}x${FILM_H} stride=$PIXEL_STRIDE"
echo "[traversal] steps=$STEPS_RAW modes=$MODES_RAW"
echo "[traversal] corner_rois=$CORNER_ROIS corner_steps=$CORNER_STEPS max_rois=$CORNER_MAX_ROIS"
echo "[traversal] diagnostic_wireframe_overlay=$DIAGNOSTIC_WIREFRAME_OVERLAY rois=$DIAGNOSTIC_WIREFRAME_MANUAL_ROIS"

refresh_summary() {
	"$PYTHON" "$ROOT/tools/first_pass_traversal_analysis.py" "$OUTPUT_DIR" || true
}

cell_dir_for() {
	local step_len="$1"
	local traversal="$2"
	echo "$OUTPUT_DIR/step_${step_len}/${traversal}"
}

write_metadata() {
	local cell_dir="$1"
	local step_len="$2"
	local traversal="$3"
	local exit_code="$4"
	local effective="$5"
	local notes="$6"
	local ts
	ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
	cat > "$cell_dir/metadata.json" <<EOF
{
  "timestamp": "$ts",
  "step_length": "$step_len",
  "traversal": "$traversal",
  "mode": "off",
  "diagnostic_wireframe_overlay": $DIAGNOSTIC_WIREFRAME_OVERLAY,
  "diagnostic_wireframe_manual_rois": "$DIAGNOSTIC_WIREFRAME_MANUAL_ROIS",
  "cell_dir": "$cell_dir",
  "exit_code": $exit_code,
  "effective_status": $effective,
  "notes": "$notes"
}
EOF
}

run_cell() {
	local step_len="$1"
	local traversal="$2"
	local cell_dir
	cell_dir="$(cell_dir_for "$step_len" "$traversal")"
	mkdir -p "$cell_dir"

	if [[ -f "$cell_dir/effective_status.txt" ]] && [[ "$(tr -d '[:space:]' < "$cell_dir/effective_status.txt")" == "0" ]]; then
		echo "[traversal] skip completed step=$step_len traversal=$traversal"
		refresh_summary
		return 0
	fi

	echo "[traversal] run step=$step_len traversal=$traversal"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$FIXTURE" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=traversal_${traversal}" \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		"--render-test-film-scale=$FILM_SCALE" \
		--render-test-camera-fixed=1 \
		"--render-test-step-length=$step_len" \
		"--render-test-pixel-stride=$PIXEL_STRIDE" \
		"--render-test-first-pass-traversal=$traversal" \
		"--diagnostic-wireframe-overlay=$DIAGNOSTIC_WIREFRAME_OVERLAY" \
		"--diagnostic-wireframe-cartesian=$DIAGNOSTIC_WIREFRAME_CARTESIAN" \
		"--diagnostic-wireframe-transport=$DIAGNOSTIC_WIREFRAME_TRANSPORT" \
		"--diagnostic-wireframe-risk=$DIAGNOSTIC_WIREFRAME_RISK" \
		"--diagnostic-wireframe-spacetime=$DIAGNOSTIC_WIREFRAME_SPACETIME" \
		"--diagnostic-wireframe-labels=$DIAGNOSTIC_WIREFRAME_LABELS" \
		"--diagnostic-wireframe-manual-rois=$DIAGNOSTIC_WIREFRAME_MANUAL_ROIS" \
		--enable-domain-telemetry=0 \
		--enable-domain-aware-first-hit-resolver=0 \
		--enable-step-convergence-telemetry=0 \
		--reference-geodesic-probe=1 \
		"--reference-geodesic-probe-max-steps=$CORNER_MAX_STEPS" \
		--corner-transport-probe=1 \
		"--corner-transport-probe-steps=$CORNER_STEPS" \
		"--corner-transport-probe-radii=$CORNER_RADII" \
		"--corner-transport-probe-patch-size=$CORNER_PATCH" \
		"--corner-transport-probe-max-rois=$CORNER_MAX_ROIS" \
		"--corner-transport-probe-manual-rois=$CORNER_ROIS" \
		> "$cell_dir/run.log" 2>&1
	local exit_code=$?
	set -e

	local effective="$exit_code"
	local notes="error_exit_${exit_code}"
	if [[ "$exit_code" -eq 0 ]]; then
		effective=0
		notes="clean_exit"
	elif [[ "$exit_code" -eq 134 ]] || grep -q "\[RenderTestRunner\]\[ExitCode\] forced=0 reason=harness_success" "$cell_dir/run.log" 2>/dev/null; then
		effective=0
		notes="godot_shutdown_abort_after_harness_success"
	fi
	echo "$exit_code" > "$cell_dir/status.txt"
	echo "$effective" > "$cell_dir/effective_status.txt"
	"$PYTHON" "$ROOT/tools/corner_transport_probe_analyzer.py" "$cell_dir" >> "$cell_dir/run.log" 2>&1 || true
	write_metadata "$cell_dir" "$step_len" "$traversal" "$exit_code" "$effective" "$notes"
	if [[ "$DIAGNOSTIC_WIREFRAME_OVERLAY" == "1" ]]; then
		"$PYTHON" "$ROOT/tools/diagnostic_wireframe_overlay.py" "$cell_dir" --manual-rois "$DIAGNOSTIC_WIREFRAME_MANUAL_ROIS" >> "$cell_dir/run.log" 2>&1 || true
	fi
	echo "[traversal] status step=$step_len traversal=$traversal exit=$exit_code effective=$effective notes=$notes"
	refresh_summary
	return 0
}

echo "[traversal] dotnet build"
dotnet build "$ROOT"
echo "[traversal] build done"

read -r -a STEPS <<< "$STEPS_RAW"
read -r -a MODES <<< "$MODES_RAW"

for step_len in "${STEPS[@]}"; do
	for traversal in "${MODES[@]}"; do
		run_cell "$step_len" "$traversal"
	done
done

refresh_summary
echo "[traversal] complete output=$OUTPUT_DIR"
