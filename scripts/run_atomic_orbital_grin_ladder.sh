#!/usr/bin/env bash
# V1 atomic orbital GRIN ladder. Repeated deterministic short runs; no image sequence plumbing.

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${ATOMIC_ORBITAL_GRIN_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${ATOMIC_ORBITAL_GRIN_ROOT:-$ROOT/output/atomic_orbital_grin_ladder/$TIMESTAMP}"
if [[ "$OUTPUT_DIR" != /* ]]; then
	OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

SCENE="${ATOMIC_ORBITAL_GRIN_SCENE:-res://test-atomic-orbital-grin-room.tscn}"
FIXTURE="${ATOMIC_ORBITAL_GRIN_FIXTURE:-atomic_orbital_grin_room}"
RES="${ATOMIC_ORBITAL_GRIN_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
FRAMES="${ATOMIC_ORBITAL_GRIN_FRAMES:-12}"
WARMUP="${ATOMIC_ORBITAL_GRIN_WARMUP:-1}"
STEP="${ATOMIC_ORBITAL_GRIN_STEP:-0.015}"
BUDGET="${ATOMIC_ORBITAL_GRIN_BUDGET:-700}"
STRIDE="${ATOMIC_ORBITAL_GRIN_STRIDE:-1}"
SMOKE="${ATOMIC_ORBITAL_GRIN_SMOKE:-0}"

if [[ "$SMOKE" == "1" ]]; then
	RES="${ATOMIC_ORBITAL_GRIN_RES:-40x23}"
	FILM_W="${RES%x*}"
	FILM_H="${RES#*x}"
	FRAMES="${ATOMIC_ORBITAL_GRIN_FRAMES:-30}"
	WARMUP="${ATOMIC_ORBITAL_GRIN_WARMUP:-0}"
fi

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/atomic_orbital_grin_ladder.log"
exec > >(tee -a "$LOG") 2>&1

echo "[atomic-ladder] output=$OUTPUT_DIR"
echo "[atomic-ladder] scene=$SCENE fixture=$FIXTURE res=${FILM_W}x${FILM_H} frames=$FRAMES warmup=$WARMUP step=$STEP budget=$BUDGET"
echo "[atomic-ladder] v1_cells=A0,A1,A2,A3 strict_gates=A0-A2"

effective_from_log() {
	local exit_code="$1"
	local log_path="$2"
	if [[ "$exit_code" -eq 0 ]]; then
		echo "0 clean_exit"
	elif [[ "$exit_code" -eq 134 ]] || grep -q "\[RenderTestRunner\]\[ExitCode\] forced=0 reason=harness_success" "$log_path" 2>/dev/null; then
		echo "0 godot_shutdown_abort_after_harness_success"
	elif grep -q "\[RenderTestRunner\]\[Capture\]" "$log_path" 2>/dev/null && grep -q "handle_crash: Program crashed with signal 11" "$log_path" 2>/dev/null; then
		echo "0 godot_shutdown_crash_after_capture"
	else
		echo "$exit_code error_exit_${exit_code}"
	fi
}

write_metadata() {
	local cell_dir="$1"
	local cell="$2"
	local electrons="$3"
	local strength="$4"
	local modulation="$5"
	local time_enabled="$6"
	local tick="$7"
	local phase="$8"
	local exit_code="$9"
	local effective="${10}"
	local notes="${11}"
	cat > "$cell_dir/metadata.json" <<EOF
{
  "study": "atomic_orbital_grin_v1",
  "cell": "$cell",
  "fixture": "$FIXTURE",
  "scene": "$SCENE",
  "electron_count": $electrons,
  "atomic_preset": "hydrogen",
  "orbital_radius": 3.5,
  "curvature_strength": $strength,
  "modulation_depth": $modulation,
  "time_enabled": $time_enabled,
  "field_tick_index": $tick,
  "phase": $phase,
  "proton_core_enabled": false,
  "resolution": "${FILM_W}x${FILM_H}",
  "frames": $FRAMES,
  "warmup": $WARMUP,
  "step_length": "$STEP",
  "steps_per_ray": $BUDGET,
  "pass_fail_gates": "A0-A2 closure_rate>=0.999 miss_pixels==0 budget_exhausted_pixels==0; A3 report classified differences",
  "rollback_guardrail": "hermetic_curved_room unchanged when atomic fixture is not selected",
  "exit_code": $exit_code,
  "effective_status": $effective,
  "notes": "$notes"
}
EOF
}

run_cell() {
	local cell="$1"
	local electrons="$2"
	local strength="$3"
	local modulation="$4"
	local time_enabled="$5"
	local tick="$6"
	local phase="$7"
	local cell_dir="$OUTPUT_DIR/cells/$cell"
	mkdir -p "$cell_dir"
	echo "[atomic-ladder] cell=$cell electrons=$electrons strength=$strength modulation=$modulation time=$time_enabled tick=$tick phase=$phase"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$FIXTURE" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=$cell" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		--render-test-film-scale=1.0 \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		--render-test-camera-fixed=1 \
		"--render-test-step-length=$STEP" \
		"--render-test-steps-per-ray=$BUDGET" \
		"--render-test-pixel-stride=$STRIDE" \
		--render-test-first-pass-traversal=row \
		--benchmark-deterministic=1 \
		--benchmark-fixed-seed=1337 \
		--diagnostic-wireframe-overlay=1 \
		--diagnostic-wireframe-cartesian=1 \
		--diagnostic-wireframe-transport=1 \
		--diagnostic-wireframe-risk=1 \
		--diagnostic-wireframe-continuity=1 \
		--enable-domain-telemetry=0 \
		--enable-domain-aware-first-hit-resolver=0 \
		--enable-step-convergence-telemetry=0 \
		"--atomic-electron-count=$electrons" \
		--atomic-preset=hydrogen \
		--atomic-orbital-radius=3.5 \
		"--atomic-curvature-strength=$strength" \
		"--atomic-modulation-depth=$modulation" \
		--atomic-field-clock-hz=0.25 \
		--atomic-update-interval-seconds=1.0 \
		"--atomic-time-enabled=$time_enabled" \
		--atomic-capture-frames=4 \
		"--atomic-field-tick-index=$tick" \
		"--atomic-phase=$phase" \
		"--atomic-output-dir=$cell_dir" \
		> "$cell_dir/run.log" 2>&1
	local exit_code=$?
	set -e
	read -r effective notes <<< "$(effective_from_log "$exit_code" "$cell_dir/run.log")"
	echo "$exit_code" > "$cell_dir/status.txt"
	echo "$effective" > "$cell_dir/effective_status.txt"
	write_metadata "$cell_dir" "$cell" "$electrons" "$strength" "$modulation" "$time_enabled" "$tick" "$phase" "$exit_code" "$effective" "$notes"
	"$PYTHON" "$ROOT/tools/diagnostic_wireframe_overlay.py" "$cell_dir" --continuity 1 >> "$cell_dir/run.log" 2>&1 || true
	"$PYTHON" "$ROOT/tools/graph_plus_hit_normals_report.py" "$cell_dir" --stride 8 --scale 12 >> "$cell_dir/run.log" 2>&1 || true
}

echo "[atomic-ladder] static checks"
dotnet build "$ROOT/Physical Light and Camera Units.csproj"
"$PYTHON" -m py_compile "$ROOT/tools/atomic_orbital_grin_ladder_analysis.py"

run_cell "A0_straight_baseline" 0 0 0 0 0 0
run_cell "A1_no_cloud_reference" 0 0.002 0 0 0 0
run_cell "A2_static_hydrogen" 1 0.002 0 0 0 0
run_cell "A3_clocked_hydrogen_tick0" 1 0.002 0.10 1 0 0
run_cell "A3_clocked_hydrogen_tick1" 1 0.002 0.10 1 1 1.570796

"$PYTHON" "$ROOT/tools/atomic_orbital_grin_ladder_analysis.py" "$OUTPUT_DIR" || true

echo "[atomic-ladder] complete output=$OUTPUT_DIR"
