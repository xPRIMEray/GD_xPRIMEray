#!/usr/bin/env bash
# Deterministic render-test traversal comparison for pass1 acquisition + pass2 commit order.

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCENE="${TILE_COMMIT_TRAVERSAL_SCENE:-res://test-domain-resolver-stress.tscn}"
FIXTURE="${TILE_COMMIT_TRAVERSAL_FIXTURE:-domain_resolver_stress}"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${TILE_COMMIT_TRAVERSAL_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${TILE_COMMIT_TRAVERSAL_ROOT:-$ROOT/output/tile_commit_traversal_comparison/$TIMESTAMP}"
if [[ "$OUTPUT_DIR" != /* ]]; then
	OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

FRAMES="${TILE_COMMIT_TRAVERSAL_FRAMES:-90}"
WARMUP="${TILE_COMMIT_TRAVERSAL_WARMUP:-5}"
RES="${TILE_COMMIT_TRAVERSAL_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
FILM_SCALE="${TILE_COMMIT_TRAVERSAL_FILM_SCALE:-1.0}"
STRIDE="${TILE_COMMIT_TRAVERSAL_STRIDE:-1}"
STEPS_RAW="${TILE_COMMIT_TRAVERSAL_STEPS:-0.015 0.0125}"
MODES_RAW="${TILE_COMMIT_TRAVERSAL_MODES:-row column tile checkerboard}"
SMOKE="${TILE_COMMIT_TRAVERSAL_SMOKE:-0}"
SKIP_CORNER="${TILE_COMMIT_TRAVERSAL_SKIP_CORNER:-0}"
DIAGNOSTIC_WIREFRAME_OVERLAY="${DIAGNOSTIC_WIREFRAME_OVERLAY:-0}"
DIAGNOSTIC_WIREFRAME_CARTESIAN="${DIAGNOSTIC_WIREFRAME_CARTESIAN:-$DIAGNOSTIC_WIREFRAME_OVERLAY}"
DIAGNOSTIC_WIREFRAME_TRANSPORT="${DIAGNOSTIC_WIREFRAME_TRANSPORT:-$DIAGNOSTIC_WIREFRAME_OVERLAY}"
DIAGNOSTIC_WIREFRAME_RISK="${DIAGNOSTIC_WIREFRAME_RISK:-$DIAGNOSTIC_WIREFRAME_OVERLAY}"
DIAGNOSTIC_WIREFRAME_SPACETIME="${DIAGNOSTIC_WIREFRAME_SPACETIME:-0}"
DIAGNOSTIC_WIREFRAME_LABELS="${DIAGNOSTIC_WIREFRAME_LABELS:-$DIAGNOSTIC_WIREFRAME_OVERLAY}"
DIAGNOSTIC_WIREFRAME_MANUAL_ROIS="${DIAGNOSTIC_WIREFRAME_MANUAL_ROIS:-40,35;280,35;40,145;280,145}"

CORNER_ROIS="${CORNER_PROBE_MANUAL_ROIS:-40,35;280,35;40,145;280,145}"
CORNER_STEPS="${CORNER_PROBE_STEPS:-0.02,0.0125,0.003125}"
CORNER_RADII="${CORNER_PROBE_RADII:-2,4}"
CORNER_PATCH="${CORNER_PROBE_PATCH_SIZE:-9}"
CORNER_MAX_ROIS="${CORNER_PROBE_MAX_ROIS:-4}"
CORNER_MAX_STEPS="${CORNER_PROBE_MAX_STEPS:-1024}"

if [[ "$SMOKE" == "1" ]]; then
	FRAMES="${TILE_COMMIT_TRAVERSAL_FRAMES:-5}"
	WARMUP="${TILE_COMMIT_TRAVERSAL_WARMUP:-0}"
	STEPS_RAW="${TILE_COMMIT_TRAVERSAL_STEPS:-0.015}"
	MODES_RAW="${TILE_COMMIT_TRAVERSAL_MODES:-row tile}"
	CORNER_MAX_ROIS="${CORNER_PROBE_MAX_ROIS:-1}"
fi

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/tile_commit_traversal.log"
exec > >(tee -a "$LOG") 2>&1

echo "[tile-commit] output=$OUTPUT_DIR"
echo "[tile-commit] flag=--render-test-first-pass-traversal controls pass1_acquisition+pass2_commit in render-test mode"
echo "[tile-commit] frames=$FRAMES warmup=$WARMUP res=${FILM_W}x${FILM_H} stride=$STRIDE steps=$STEPS_RAW modes=$MODES_RAW"
echo "[tile-commit] diagnostic_wireframe_overlay=$DIAGNOSTIC_WIREFRAME_OVERLAY rois=$DIAGNOSTIC_WIREFRAME_MANUAL_ROIS"

refresh_summary() {
	"$PYTHON" "$ROOT/tools/tile_commit_traversal_analysis.py" "$OUTPUT_DIR" || true
}

beauty_dir_for() {
	echo "$OUTPUT_DIR/beauty/step_$1/$2"
}

corner_dir_for() {
	echo "$OUTPUT_DIR/corner_probe_after_beauty/step_$1/$2"
}

write_metadata() {
	local cell_dir="$1"
	local step="$2"
	local mode="$3"
	local phase="$4"
	local exit_code="$5"
	local effective="$6"
	local notes="$7"
	local ts
	ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
	cat > "$cell_dir/metadata.json" <<EOF
{
  "timestamp": "$ts",
  "phase": "$phase",
  "step_length": "$step",
  "traversal": "$mode",
  "resolution": "${FILM_W}x${FILM_H}",
  "film_width": $FILM_W,
  "film_height": $FILM_H,
  "stride": $STRIDE,
  "traversal_flag_scope": "render-test pass1 acquisition + pass2 commit/write order",
  "diagnostic_wireframe_overlay": $DIAGNOSTIC_WIREFRAME_OVERLAY,
  "diagnostic_wireframe_manual_rois": "$DIAGNOSTIC_WIREFRAME_MANUAL_ROIS",
  "cell_dir": "$cell_dir",
  "exit_code": $exit_code,
  "effective_status": $effective,
  "notes": "$notes"
}
EOF
}

effective_from_log() {
	local exit_code="$1"
	local log_path="$2"
	if [[ "$exit_code" -eq 0 ]]; then
		echo "0 clean_exit"
	elif [[ "$exit_code" -eq 134 ]] || grep -q "\[RenderTestRunner\]\[ExitCode\] forced=0 reason=harness_success" "$log_path" 2>/dev/null; then
		echo "0 godot_shutdown_abort_after_harness_success"
	else
		echo "$exit_code error_exit_${exit_code}"
	fi
}

run_beauty_cell() {
	local step="$1"
	local mode="$2"
	local cell_dir
	cell_dir="$(beauty_dir_for "$step" "$mode")"
	mkdir -p "$cell_dir"
	if [[ -f "$cell_dir/effective_status.txt" ]] && [[ "$(tr -d '[:space:]' < "$cell_dir/effective_status.txt")" == "0" ]]; then
		echo "[tile-commit] skip completed beauty step=$step mode=$mode"
		refresh_summary
		return 0
	fi
	echo "[tile-commit] beauty step=$step mode=$mode"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$FIXTURE" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=tilecommit_${mode}" \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		"--render-test-film-scale=$FILM_SCALE" \
		--render-test-camera-fixed=1 \
		"--render-test-step-length=$step" \
		"--render-test-pixel-stride=$STRIDE" \
		"--render-test-first-pass-traversal=$mode" \
		--benchmark-deterministic=1 \
		--benchmark-fixed-seed=1337 \
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
		> "$cell_dir/run.log" 2>&1
	local exit_code=$?
	set -e
	read -r effective notes <<< "$(effective_from_log "$exit_code" "$cell_dir/run.log")"
	echo "$exit_code" > "$cell_dir/status.txt"
	echo "$effective" > "$cell_dir/effective_status.txt"
	write_metadata "$cell_dir" "$step" "$mode" "beauty" "$exit_code" "$effective" "$notes"
	if [[ "$DIAGNOSTIC_WIREFRAME_OVERLAY" == "1" ]]; then
		"$PYTHON" "$ROOT/tools/diagnostic_wireframe_overlay.py" "$cell_dir" --manual-rois "$DIAGNOSTIC_WIREFRAME_MANUAL_ROIS" >> "$cell_dir/run.log" 2>&1 || true
	fi
	echo "[tile-commit] beauty status step=$step mode=$mode exit=$exit_code effective=$effective notes=$notes"
	refresh_summary
}

run_corner_cell() {
	local step="$1"
	local mode="$2"
	local cell_dir
	cell_dir="$(corner_dir_for "$step" "$mode")"
	mkdir -p "$cell_dir"
	echo "[tile-commit] corner step=$step mode=$mode"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$FIXTURE" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=tilecommit_corner_${mode}" \
		"--render-test-frames=5" \
		--render-test-warmup=0 \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		"--render-test-film-scale=$FILM_SCALE" \
		--render-test-camera-fixed=1 \
		"--render-test-step-length=$step" \
		"--render-test-pixel-stride=$STRIDE" \
		"--render-test-first-pass-traversal=$mode" \
		--benchmark-deterministic=1 \
		--benchmark-fixed-seed=1337 \
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
	read -r effective notes <<< "$(effective_from_log "$exit_code" "$cell_dir/run.log")"
	echo "$exit_code" > "$cell_dir/status.txt"
	echo "$effective" > "$cell_dir/effective_status.txt"
	"$PYTHON" "$ROOT/tools/corner_transport_probe_analyzer.py" "$cell_dir" >> "$cell_dir/run.log" 2>&1 || true
	write_metadata "$cell_dir" "$step" "$mode" "corner_probe_after_beauty" "$exit_code" "$effective" "$notes"
	if [[ "$DIAGNOSTIC_WIREFRAME_OVERLAY" == "1" ]]; then
		"$PYTHON" "$ROOT/tools/diagnostic_wireframe_overlay.py" "$cell_dir" --manual-rois "$DIAGNOSTIC_WIREFRAME_MANUAL_ROIS" >> "$cell_dir/run.log" 2>&1 || true
	fi
}

echo "[tile-commit] dotnet build"
dotnet build "$ROOT"
echo "[tile-commit] build done"

read -r -a STEPS <<< "$STEPS_RAW"
read -r -a MODES <<< "$MODES_RAW"

for step in "${STEPS[@]}"; do
	for mode in "${MODES[@]}"; do
		run_beauty_cell "$step" "$mode"
	done
done

if [[ "$SKIP_CORNER" != "1" ]]; then
	for step in "${STEPS[@]}"; do
		for mode in "${MODES[@]}"; do
			run_corner_cell "$step" "$mode"
		done
	done
fi

refresh_summary
echo "[tile-commit] complete output=$OUTPUT_DIR"
