#!/usr/bin/env bash
# Hermetic hit-closure ladder for xPRIMEray sealed-room validation.
#
# Smoke:
#   HERMETIC_CLOSURE_SMOKE=1 bash scripts/run_hermetic_hit_closure_ladder.sh

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${HERMETIC_CLOSURE_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${HERMETIC_CLOSURE_ROOT:-$ROOT/output/hermetic_hit_closure/$TIMESTAMP}"
if [[ "$OUTPUT_DIR" != /* ]]; then
	OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

SCENE="${HERMETIC_CLOSURE_SCENE:-res://test-hermetic-curved-room.tscn}"
FIXTURE="${HERMETIC_CLOSURE_FIXTURE:-hermetic_curved_room}"
RES="${HERMETIC_CLOSURE_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
FILM_SCALE="${HERMETIC_CLOSURE_FILM_SCALE:-1.0}"
FRAMES="${HERMETIC_CLOSURE_FRAMES:-90}"
WARMUP="${HERMETIC_CLOSURE_WARMUP:-5}"
STEPS_RAW="${HERMETIC_CLOSURE_STEPS:-0.02 0.015 0.0125 0.00625}"
BUDGETS_RAW="${HERMETIC_CLOSURE_BUDGETS:-128 700 1400}"
CURVATURES_RAW="${HERMETIC_CLOSURE_CURVATURES:-0 0.75}"
TRAVERSALS_RAW="${HERMETIC_CLOSURE_TRAVERSALS:-row reverse_row}"
STRIDE="${HERMETIC_CLOSURE_STRIDE:-1}"
MAX_HOURS="${HERMETIC_CLOSURE_MAX_HOURS:-12}"
SMOKE="${HERMETIC_CLOSURE_SMOKE:-0}"
RESOLVER_PHASE="${HERMETIC_RESOLVER_PHASE:-0}"
ADAPTIVE_CLOSURE_PROBE="${HERMETIC_ADAPTIVE_CLOSURE_PROBE:-1}"

if [[ "$SMOKE" == "1" ]]; then
	FRAMES="${HERMETIC_CLOSURE_FRAMES:-5}"
	WARMUP="${HERMETIC_CLOSURE_WARMUP:-0}"
	FILM_SCALE="${HERMETIC_CLOSURE_FILM_SCALE:-0.0625}"
	STEPS_RAW="${HERMETIC_CLOSURE_STEPS:-0.015}"
	BUDGETS_RAW="${HERMETIC_CLOSURE_BUDGETS:-32 700}"
	CURVATURES_RAW="${HERMETIC_CLOSURE_CURVATURES:-0}"
	TRAVERSALS_RAW="${HERMETIC_CLOSURE_TRAVERSALS:-row}"
fi

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/hermetic_hit_closure.log"
exec > >(tee -a "$LOG") 2>&1

START_EPOCH="$(date +%s)"
MAX_SECONDS="$((MAX_HOURS * 3600))"

echo "[hermetic-closure] output=$OUTPUT_DIR"
echo "[hermetic-closure] scene=$SCENE fixture=$FIXTURE"
echo "[hermetic-closure] frames=$FRAMES warmup=$WARMUP res=${FILM_W}x${FILM_H} stride=$STRIDE"
echo "[hermetic-closure] steps=$STEPS_RAW budgets=$BUDGETS_RAW curvature=$CURVATURES_RAW traversal=$TRAVERSALS_RAW"
echo "[hermetic-closure] contract=every pixel should hit a sealed-room receiver unless budget/integration/topology/valid-exception prevents closure"
echo "[hermetic-closure] guardrail=scene-contract closure only; no physical truth claim; diagnostics never feed rendering"
echo "[hermetic-closure] adaptive_closure_probe=$ADAPTIVE_CLOSURE_PROBE prototype_postprocess_only=1"

sanitize_float() {
	echo "$1" | sed 's/-/m/g; s/\./p/g'
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

refresh_summary() {
	"$PYTHON" "$ROOT/tools/hermetic_hit_closure_analysis.py" "$OUTPUT_DIR" || true
	if [[ "$ADAPTIVE_CLOSURE_PROBE" == "1" ]]; then
		"$PYTHON" "$ROOT/tools/adaptive_closure_probe_analysis.py" "$OUTPUT_DIR" || true
	fi
}

write_metadata() {
	local cell_dir="$1"
	local step="$2"
	local budget="$3"
	local curvature="$4"
	local traversal="$5"
	local exit_code="$6"
	local effective="$7"
	local notes="$8"
	local ts
	ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
	cat > "$cell_dir/metadata.json" <<EOF
{
  "timestamp": "$ts",
  "study": "hermetic_hit_closure_ladder",
  "fixture": "$FIXTURE",
  "scene": "$SCENE",
  "step_length": "$step",
  "steps_per_ray": "$budget",
  "curvature_strength": "$curvature",
  "traversal": "$traversal",
  "resolution": "${FILM_W}x${FILM_H}",
  "film_width": $FILM_W,
  "film_height": $FILM_H,
  "film_scale": $FILM_SCALE,
  "stride": $STRIDE,
  "frames": $FRAMES,
  "warmup": $WARMUP,
  "resolver_phase": $RESOLVER_PHASE,
  "adaptive_closure_probe": $ADAPTIVE_CLOSURE_PROBE,
  "closure_contract": "all pixels expected to hit sealed-room receiver; no valid exceptions in v1",
  "closure_truth_scope": "scene-contract closure, not physical correctness",
  "diagnostic_only": true,
  "exit_code": $exit_code,
  "effective_status": $effective,
  "notes": "$notes"
}
EOF
}

run_cell() {
	local step="$1"
	local budget="$2"
	local curvature="$3"
	local traversal="$4"
	local curv_token
	curv_token="$(sanitize_float "$curvature")"
	local cell_dir="$OUTPUT_DIR/cells/step_${step}/budget_${budget}/curvature_${curv_token}/${traversal}"
	mkdir -p "$cell_dir"
	if [[ -f "$cell_dir/effective_status.txt" ]] && [[ "$(tr -d '[:space:]' < "$cell_dir/effective_status.txt")" == "0" ]]; then
		echo "[hermetic-closure] skip completed step=$step budget=$budget curvature=$curvature traversal=$traversal"
		refresh_summary
		return 0
	fi

	echo "[hermetic-closure] cell step=$step budget=$budget curvature=$curvature traversal=$traversal"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$FIXTURE" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=hermetic_step_${step}_budget_${budget}_curv_${curv_token}_${traversal}" \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		"--render-test-film-scale=$FILM_SCALE" \
		--render-test-camera-fixed=1 \
		"--render-test-step-length=$step" \
		"--render-test-steps-per-ray=$budget" \
		"--hermetic-curvature-strength=$curvature" \
		"--render-test-pixel-stride=$STRIDE" \
		"--render-test-first-pass-traversal=$traversal" \
		--benchmark-deterministic=1 \
		--benchmark-fixed-seed=1337 \
		--diagnostic-wireframe-overlay=1 \
		--diagnostic-wireframe-cartesian=1 \
		--diagnostic-wireframe-transport=1 \
		--diagnostic-wireframe-risk=1 \
		--diagnostic-wireframe-spacetime=0 \
		--diagnostic-wireframe-continuity=1 \
		--diagnostic-wireframe-labels=1 \
		--diagnostic-wireframe-manual-rois=40,35\;280,35\;40,145\;280,145 \
		--enable-domain-telemetry=0 \
		"--enable-domain-aware-first-hit-resolver=$RESOLVER_PHASE" \
		--enable-step-convergence-telemetry=0 \
		> "$cell_dir/run.log" 2>&1
	local exit_code=$?
	set -e
	read -r effective notes <<< "$(effective_from_log "$exit_code" "$cell_dir/run.log")"
	echo "$exit_code" > "$cell_dir/status.txt"
	echo "$effective" > "$cell_dir/effective_status.txt"
	write_metadata "$cell_dir" "$step" "$budget" "$curvature" "$traversal" "$exit_code" "$effective" "$notes"

	"$PYTHON" "$ROOT/tools/diagnostic_wireframe_overlay.py" "$cell_dir" --continuity 1 >> "$cell_dir/run.log" 2>&1 || true
	"$PYTHON" "$ROOT/tools/transport_ownership_graph_extractor.py" "$cell_dir" --visualize 1 --out "$cell_dir" >> "$cell_dir/run.log" 2>&1 || true
	local image
	image="$(find "$cell_dir" -maxdepth 1 -name '*.png' ! -name 'layer*' ! -name 'combined*' ! -name 'diagnostic*' ! -name 'transport_*' ! -name 'budget_*' ! -name 'ownership_*' | head -n 1 || true)"
	local hit_csv
	hit_csv="$(find "$cell_dir" -maxdepth 1 -name '*.hit_diagnostics.csv' | head -n 1 || true)"
	if [[ -n "$image" && -n "$hit_csv" ]]; then
		"$PYTHON" "$ROOT/tools/hit_normal_vector_overlay.py" --image "$image" --hit-csv "$hit_csv" --stride 8 --scale 12 --out "$cell_dir" >> "$cell_dir/run.log" 2>&1 || true
		cp -f "$cell_dir/hit_normal_vector_overlay.png" "$cell_dir/full_frame_hit_normals.png" 2>/dev/null || true
	fi
	"$PYTHON" "$ROOT/tools/graph_plus_hit_normals_report.py" "$cell_dir" --stride 8 --scale 12 >> "$cell_dir/run.log" 2>&1 || true

	echo "[hermetic-closure] cell status step=$step budget=$budget curvature=$curvature traversal=$traversal exit=$exit_code effective=$effective notes=$notes"
	refresh_summary
}

echo "[hermetic-closure] static checks"
dotnet build "$ROOT/Physical Light and Camera Units.csproj"
"$PYTHON" -m py_compile "$ROOT/tools/hermetic_hit_closure_analysis.py" "$ROOT/tools/diagnostic_wireframe_overlay.py" "$ROOT/tools/transport_ownership_graph_extractor.py"

read -r -a STEPS <<< "$STEPS_RAW"
read -r -a BUDGETS <<< "$BUDGETS_RAW"
read -r -a CURVATURES <<< "$CURVATURES_RAW"
read -r -a TRAVERSALS <<< "$TRAVERSALS_RAW"

for curvature in "${CURVATURES[@]}"; do
	for traversal in "${TRAVERSALS[@]}"; do
		for step in "${STEPS[@]}"; do
			for budget in "${BUDGETS[@]}"; do
				run_cell "$step" "$budget" "$curvature" "$traversal"
				if [[ -f "$OUTPUT_DIR/STOP" ]]; then
					echo "[hermetic-closure] STOP file detected; stopping after current cell"
					refresh_summary
					exit 0
				fi
				now="$(date +%s)"
				if (( now - START_EPOCH >= MAX_SECONDS )); then
					echo "[hermetic-closure] max runtime exceeded; stopping after current cell"
					refresh_summary
					exit 0
				fi
			done
		done
	done
done

refresh_summary
echo "[hermetic-closure] done output=$OUTPUT_DIR"
