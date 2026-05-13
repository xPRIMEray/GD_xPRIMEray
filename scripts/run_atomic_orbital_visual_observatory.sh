#!/usr/bin/env bash
# Visual-only atomic orbital GRIN observatory. Interpretation only; not closure validation.

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${ATOMIC_ORBITAL_VISUAL_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${ATOMIC_ORBITAL_VISUAL_ROOT:-$ROOT/output/atomic_orbital_visual_observatory/$TIMESTAMP}"
if [[ "$OUTPUT_DIR" != /* ]]; then
	OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

SCENE="${ATOMIC_ORBITAL_VISUAL_SCENE:-res://test-atomic-orbital-visual-observatory.tscn}"
FIXTURE="${ATOMIC_ORBITAL_VISUAL_FIXTURE:-atomic_orbital_visual_observatory}"
RES="${ATOMIC_ORBITAL_VISUAL_RES:-640x360}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
FRAMES="${ATOMIC_ORBITAL_VISUAL_FRAMES:-120}"
WARMUP="${ATOMIC_ORBITAL_VISUAL_WARMUP:-3}"
STEP="${ATOMIC_ORBITAL_VISUAL_STEP:-0.0125}"
BUDGET="${ATOMIC_ORBITAL_VISUAL_BUDGET:-900}"
UPDATE_BUDGET_MS="${ATOMIC_ORBITAL_VISUAL_UPDATE_BUDGET_MS:-12000}"
STRIDE="${ATOMIC_ORBITAL_VISUAL_STRIDE:-2}"
SMOKE="${ATOMIC_ORBITAL_VISUAL_SMOKE:-0}"

if [[ "$SMOKE" == "1" ]]; then
	RES="${ATOMIC_ORBITAL_VISUAL_RES:-80x45}"
	FILM_W="${RES%x*}"
	FILM_H="${RES#*x}"
	FRAMES="${ATOMIC_ORBITAL_VISUAL_FRAMES:-30}"
	WARMUP="${ATOMIC_ORBITAL_VISUAL_WARMUP:-0}"
	UPDATE_BUDGET_MS="${ATOMIC_ORBITAL_VISUAL_UPDATE_BUDGET_MS:-2000}"
fi

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/atomic_orbital_visual_observatory.log"
exec > >(tee -a "$LOG") 2>&1

echo "[atomic-visual] output=$OUTPUT_DIR"
echo "[atomic-visual] scene=$SCENE fixture=$FIXTURE res=${FILM_W}x${FILM_H} frames=$FRAMES warmup=$WARMUP step=$STEP budget=$BUDGET update_budget_ms=$UPDATE_BUDGET_MS"
echo "[atomic-visual] purpose=interpretation_only closure_validation=0 pass_fail_gates=none"

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

write_metadata() {
	local cell_dir="$1"
	local cell="$2"
	local shading="$3"
	local electrons="$4"
	local radius="$5"
	local strength="$6"
	local modulation="$7"
	local time_enabled="$8"
	local tick="$9"
	local phase="${10}"
	local exit_code="${11}"
	local effective="${12}"
	local notes="${13}"
	cat > "$cell_dir/metadata.json" <<EOF
{
  "study": "atomic_orbital_visual_observatory",
  "purpose": "Interpretation only; not closure validation.",
  "cell": "$cell",
  "film_shading": "$shading",
  "fixture": "$FIXTURE",
  "scene": "$SCENE",
  "electron_count": $electrons,
  "atomic_preset": "hydrogen",
  "orbital_radius": $radius,
  "curvature_strength": $strength,
  "modulation_depth": $modulation,
  "time_enabled": $time_enabled,
  "field_tick_index": $tick,
  "phase": $phase,
  "visual_density": 1,
  "visual_guides": 1,
  "visual_laser_sheet": 1,
  "visual_beams": 1,
  "visual_only_exclusion": "VisualGuidesRoot, DensityMarkersRoot, LaserGuidesRoot, and BeamGuidesRoot are in group visual_only and excluded by SnapshotBuilder.",
  "resolution": "${FILM_W}x${FILM_H}",
  "frames": $FRAMES,
  "warmup": $WARMUP,
  "step_length": "$STEP",
  "steps_per_ray": $BUDGET,
  "update_budget_ms": $UPDATE_BUDGET_MS,
  "exit_code": $exit_code,
  "effective_status": $effective,
  "notes": "$notes"
}
EOF
}

run_cell() {
	local cell="$1"
	local shading="$2"
	local electrons="$3"
	local radius="$4"
	local strength="$5"
	local modulation="$6"
	local time_enabled="$7"
	local tick="$8"
	local phase="$9"
	local cell_dir="$OUTPUT_DIR/cells/$shading/$cell"
	mkdir -p "$cell_dir"
	echo "[atomic-visual] cell=$cell shading=$shading electrons=$electrons radius=$radius strength=$strength modulation=$modulation time=$time_enabled tick=$tick phase=$phase"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$FIXTURE" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=${shading}_${cell}" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		--render-test-film-scale=1.0 \
		"--render-test-film-shading=$shading" \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		"--render-test-update-budget-ms=$UPDATE_BUDGET_MS" \
		--render-test-camera-fixed=1 \
		"--render-test-step-length=$STEP" \
		"--render-test-steps-per-ray=$BUDGET" \
		"--render-test-pixel-stride=$STRIDE" \
		--render-test-first-pass-traversal=row \
		--benchmark-deterministic=1 \
		--benchmark-fixed-seed=1337 \
		--diagnostic-wireframe-overlay=0 \
		--enable-domain-telemetry=0 \
		--enable-domain-aware-first-hit-resolver=0 \
		--enable-step-convergence-telemetry=0 \
		"--atomic-electron-count=$electrons" \
		--atomic-preset=hydrogen \
		"--atomic-orbital-radius=$radius" \
		"--atomic-curvature-strength=$strength" \
		"--atomic-modulation-depth=$modulation" \
		--atomic-field-clock-hz=0.25 \
		--atomic-update-interval-seconds=1.0 \
		"--atomic-time-enabled=$time_enabled" \
		"--atomic-field-tick-index=$tick" \
		"--atomic-phase=$phase" \
		--atomic-visual-density=1 \
		--atomic-visual-guides=1 \
		--atomic-visual-laser-sheet=1 \
		--atomic-visual-beams=1 \
		--atomic-visual-allow-extreme=0 \
		"--atomic-visual-output-dir=$cell_dir" \
		> "$cell_dir/run.log" 2>&1
	local exit_code=$?
	set -e
	read -r effective notes <<< "$(effective_from_log "$exit_code" "$cell_dir/run.log")"
	echo "$exit_code" > "$cell_dir/status.txt"
	echo "$effective" > "$cell_dir/effective_status.txt"
	write_metadata "$cell_dir" "$cell" "$shading" "$electrons" "$radius" "$strength" "$modulation" "$time_enabled" "$tick" "$phase" "$exit_code" "$effective" "$notes"
	echo "[atomic-visual] cell_status cell=$cell shading=$shading exit=$exit_code effective=$effective notes=$notes"
}

echo "[atomic-visual] static checks"
dotnet build "$ROOT/Physical Light and Camera Units.csproj"
"$PYTHON" -m py_compile "$ROOT/tools/atomic_orbital_visual_diff.py"

for shading in normal_rgb depth_heatmap; do
	run_cell "V0_baseline_no_field" "$shading" 0 8.0 0 0 0 0 0
	run_cell "V1_static_hydrogen" "$shading" 1 8.0 0.05 0 0 0 0
	run_cell "V2_exaggerated_hydrogen" "$shading" 1 9.0 0.1 0 0 0 0
	run_cell "V3_tick0" "$shading" 1 8.0 0.065 0.35 1 0 -1.570796
	run_cell "V4_tick1" "$shading" 1 8.0 0.065 0.35 1 1 1.570796
done

"$PYTHON" "$ROOT/tools/atomic_orbital_visual_diff.py" "$OUTPUT_DIR" || true

echo "[atomic-visual] complete output=$OUTPUT_DIR"
