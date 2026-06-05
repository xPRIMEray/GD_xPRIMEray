#!/usr/bin/env bash
# Hermetic curvature FPS benchmark.
#
# Smoke:
#   CURVATURE_FPS_FRAMES=10 CURVATURE_FPS_WARMUP=2 CURVATURE_FPS_FILM_SCALE=0.125 bash scripts/run_curvature_fps_benchmark.sh

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${CURVATURE_FPS_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${CURVATURE_FPS_ROOT:-$ROOT/output/curvature_fps_benchmark/$TIMESTAMP}"
if [[ "$OUTPUT_DIR" != /* ]]; then
	OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

SCENE="${CURVATURE_FPS_SCENE:-res://test-hermetic-curved-room.tscn}"
FIXTURE="${CURVATURE_FPS_FIXTURE:-hermetic_curved_room}"
RES="${CURVATURE_FPS_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
FILM_SCALE="${CURVATURE_FPS_FILM_SCALE:-1.0}"
FRAMES="${CURVATURE_FPS_FRAMES:-90}"
WARMUP="${CURVATURE_FPS_WARMUP:-5}"
STEP="${CURVATURE_FPS_STEP:-0.015}"
BUDGET="${CURVATURE_FPS_STEPS_PER_RAY:-700}"
STRIDE="${CURVATURE_FPS_STRIDE:-1}"
TRAVERSAL="${CURVATURE_FPS_TRAVERSAL:-row}"
MANUAL_ROIS="${CURVATURE_FPS_MANUAL_ROIS:-40,35;280,35;40,145;280,145}"
SWEEP="${CURVATURE_FPS_SWEEP:-0:0.0 25:0.2875 50:0.575 75:0.8625 100:1.15}"

mkdir -p "$OUTPUT_DIR" "$ROOT/reports"
LOG="$OUTPUT_DIR/curvature_fps_benchmark.log"
exec > >(tee -a "$LOG") 2>&1

echo "[curvature-fps] output=$OUTPUT_DIR"
echo "[curvature-fps] scene=$SCENE fixture=$FIXTURE"
echo "[curvature-fps] frames=$FRAMES warmup=$WARMUP res=${FILM_W}x${FILM_H} scale=$FILM_SCALE stride=$STRIDE"
echo "[curvature-fps] step=$STEP steps_per_ray=$BUDGET traversal=$TRAVERSAL"
echo "[curvature-fps] sweep=$SWEEP"
echo "[curvature-fps] contract=every evaluated pixel should hit a sealed-room receiver"
echo "[curvature-fps] guardrail=scene-contract closure only; diagnostics never feed rendering"

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
	local percent="$2"
	local amplitude="$3"
	local exit_code="$4"
	local effective="$5"
	local notes="$6"
	local ts
	ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
	cat > "$cell_dir/metadata.json" <<EOF
{
  "timestamp": "$ts",
  "study": "curvature_fps_benchmark",
  "fixture": "$FIXTURE",
  "scene": "$SCENE",
  "curvature_percent": $percent,
  "curvature_strength": "$amplitude",
  "field_amplitude": "$amplitude",
  "step_length": "$STEP",
  "steps_per_ray": "$BUDGET",
  "traversal": "$TRAVERSAL",
  "resolution": "${FILM_W}x${FILM_H}",
  "film_width": $FILM_W,
  "film_height": $FILM_H,
  "film_scale": $FILM_SCALE,
  "stride": $STRIDE,
  "frames": $FRAMES,
  "warmup": $WARMUP,
  "closure_contract": "all pixels expected to hit sealed-room receiver; no valid exceptions in v1",
  "closure_truth_scope": "scene-contract closure, not physical correctness",
  "curved_minimal_reference": "100pct amplitude 1.15 follows CurvedMinimalFingerprint canonical fixture amplitude",
  "diagnostic_only": true,
  "exit_code": $exit_code,
  "effective_status": $effective,
  "notes": "$notes"
}
EOF
}

discover_image_and_csv() {
	"$PYTHON" - "$1" <<'PY'
from pathlib import Path
import sys
folder = Path(sys.argv[1])
csvs = sorted(folder.glob("*.hit_diagnostics.csv"))
skip = ("layer", "combined_", "diagnostic_", "transport_", "ownership_", "unstable_", "graph_", "merge_", "hit_normal_", "budget_")
imgs = [p for p in folder.glob("*.png") if not p.name.startswith(skip) and "overlay" not in p.name]
imgs.sort(key=lambda p: p.stat().st_size, reverse=True)
print(str(imgs[0]) if imgs else "")
print(str(csvs[0]) if csvs else "")
PY
}

postprocess_cell() {
	local cell_dir="$1"
	echo "[curvature-fps] postprocess cell=$cell_dir"
	"$PYTHON" "$ROOT/tools/diagnostic_wireframe_overlay.py" "$cell_dir" --continuity 1 >> "$cell_dir/run.log" 2>&1 || true
	"$PYTHON" "$ROOT/tools/transport_ownership_graph_extractor.py" "$cell_dir" --visualize 1 --out "$cell_dir" >> "$cell_dir/run.log" 2>&1 || true
	local image_path hit_csv
	mapfile -t found < <(discover_image_and_csv "$cell_dir")
	image_path="${found[0]:-}"
	hit_csv="${found[1]:-}"
	if [[ -n "$image_path" && -n "$hit_csv" ]]; then
		"$PYTHON" "$ROOT/tools/hit_normal_vector_overlay.py" \
			--image "$image_path" \
			--hit-csv "$hit_csv" \
			--stride 8 \
			--scale 12 \
			--out "$cell_dir" >> "$cell_dir/run.log" 2>&1 || true
		cp -f "$cell_dir/hit_normal_vector_overlay.png" "$cell_dir/full_frame_hit_normals.png" 2>/dev/null || true
	fi
	"$PYTHON" "$ROOT/tools/graph_plus_hit_normals_report.py" "$cell_dir" --stride 8 --scale 12 >> "$cell_dir/run.log" 2>&1 || true
}

run_cell() {
	local percent="$1"
	local amplitude="$2"
	local cell_dir="$OUTPUT_DIR/cells/curvature_$(printf '%03d' "$percent")/row"
	mkdir -p "$cell_dir"
	echo "[curvature-fps] cell curvature=${percent}% amplitude=$amplitude"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$FIXTURE" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=curvature_fps_${percent}" \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		"--render-test-film-scale=$FILM_SCALE" \
		--render-test-camera-fixed=1 \
		"--render-test-step-length=$STEP" \
		"--render-test-steps-per-ray=$BUDGET" \
		"--render-test-pixel-stride=$STRIDE" \
		"--render-test-first-pass-traversal=$TRAVERSAL" \
		"--hermetic-curvature-strength=$amplitude" \
		--curvature-fps-benchmark=1 \
		"--curvature-fps-percent=$percent" \
		"--curvature-fps-amplitude=$amplitude" \
		--benchmark-deterministic=1 \
		--benchmark-fixed-seed=1337 \
		--diagnostic-wireframe-overlay=1 \
		--diagnostic-wireframe-cartesian=1 \
		--diagnostic-wireframe-transport=1 \
		--diagnostic-wireframe-risk=1 \
		--diagnostic-wireframe-spacetime=0 \
		--diagnostic-wireframe-continuity=1 \
		--diagnostic-wireframe-labels=1 \
		"--diagnostic-wireframe-manual-rois=$MANUAL_ROIS" \
		--enable-domain-telemetry=0 \
		--enable-domain-aware-first-hit-resolver=0 \
		--enable-step-convergence-telemetry=0 \
		> "$cell_dir/run.log" 2>&1
	local exit_code=$?
	set -e
	read -r effective notes <<< "$(effective_from_log "$exit_code" "$cell_dir/run.log")"
	echo "$exit_code" > "$cell_dir/status.txt"
	echo "$effective" > "$cell_dir/effective_status.txt"
	write_metadata "$cell_dir" "$percent" "$amplitude" "$exit_code" "$effective" "$notes"
	postprocess_cell "$cell_dir"
	echo "[curvature-fps] cell status curvature=${percent}% exit=$exit_code effective=$effective notes=$notes"
}

echo "[curvature-fps] static checks"
dotnet build "$ROOT/Physical Light and Camera Units.csproj" || exit 1
"$PYTHON" -m py_compile \
	"$ROOT/tools/curvature_fps_benchmark_report.py" \
	"$ROOT/tools/hermetic_hit_closure_analysis.py" \
	"$ROOT/tools/diagnostic_wireframe_overlay.py" \
	"$ROOT/tools/transport_ownership_graph_extractor.py" \
	"$ROOT/tools/graph_plus_hit_normals_report.py" \
	"$ROOT/tools/hit_normal_vector_overlay.py" || exit 1
echo "[curvature-fps] checks done"

for item in $SWEEP; do
	percent="${item%%:*}"
	amplitude="${item#*:}"
	run_cell "$percent" "$amplitude"
done

"$PYTHON" "$ROOT/tools/hermetic_hit_closure_analysis.py" "$OUTPUT_DIR" || true
"$PYTHON" "$ROOT/tools/curvature_fps_benchmark_report.py" "$OUTPUT_DIR" --repo-root "$ROOT"

echo "[curvature-fps] complete output=$OUTPUT_DIR"
