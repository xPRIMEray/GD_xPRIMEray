#!/usr/bin/env bash
# Curved-field validation ladder using passive xPRIMEray diagnostic cockpit tools.
#
# Smoke:
#   CURVED_LADDER_SMOKE=1 bash scripts/run_curved_field_validation_ladder.sh
#
# Full local run:
#   bash scripts/run_curved_field_validation_ladder.sh

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${CURVED_LADDER_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${CURVED_LADDER_ROOT:-$ROOT/output/curved_field_validation_ladder/$TIMESTAMP}"
if [[ "$OUTPUT_DIR" != /* ]]; then
	OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

CURVED_SCENE="${CURVED_LADDER_CURVED_SCENE:-res://test-curved-minimal-backdrop.tscn}"
CURVED_FIXTURE="${CURVED_LADDER_CURVED_FIXTURE:-curved_minimal_backdrop}"
CONTROL_SCENE="${CURVED_LADDER_CONTROL_SCENE:-res://test-domain-resolver-stress.tscn}"
CONTROL_FIXTURE="${CURVED_LADDER_CONTROL_FIXTURE:-domain_resolver_stress}"
FRAMES="${CURVED_LADDER_FRAMES:-90}"
WARMUP="${CURVED_LADDER_WARMUP:-5}"
RES="${CURVED_LADDER_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
TRAVERSAL="${CURVED_LADDER_TRAVERSAL:-row}"
STRIDE="${CURVED_LADDER_STRIDE:-1}"
STEPS_RAW="${CURVED_LADDER_STEPS:-0.02 0.018 0.016 0.015 0.014 0.013 0.0125 0.011 0.010 0.0075 0.00625 0.003125}"
PRIMARY_STEP="${CURVED_LADDER_PRIMARY_STEP:-0.015}"
REFERENCE_STEP="${CURVED_LADDER_REFERENCE_STEP:-0.003125}"
MANUAL_ROIS="${CURVED_LADDER_MANUAL_ROIS:-40,35;280,35;40,145;280,145}"
ORACLE_MAX_PIXELS="${CURVED_LADDER_ORACLE_MAX_PIXELS:-64}"
ORACLE_PATCH_SIZE="${CURVED_LADDER_ORACLE_PATCH_SIZE:-9}"
ORACLE_STEP="${CURVED_LADDER_ORACLE_STEP:-0.0015625}"
ORACLE_TOLERANCE="${CURVED_LADDER_ORACLE_TOLERANCE:-0.0001}"
ORACLE_MAX_STEPS="${CURVED_LADDER_ORACLE_MAX_STEPS:-65536}"
ORACLE_REPLAY_COUNT="${CURVED_LADDER_ORACLE_REPLAY_COUNT:-2}"
SMOKE="${CURVED_LADDER_SMOKE:-0}"

if [[ "$SMOKE" == "1" ]]; then
	FRAMES="${CURVED_LADDER_FRAMES:-5}"
	WARMUP="${CURVED_LADDER_WARMUP:-0}"
	STEPS_RAW="${CURVED_LADDER_STEPS:-0.015 0.00625 0.003125}"
	PRIMARY_STEP="${CURVED_LADDER_PRIMARY_STEP:-0.015}"
	MANUAL_ROIS="${CURVED_LADDER_MANUAL_ROIS:-40,35}"
	ORACLE_MAX_PIXELS="${CURVED_LADDER_ORACLE_MAX_PIXELS:-8}"
	ORACLE_PATCH_SIZE="${CURVED_LADDER_ORACLE_PATCH_SIZE:-3}"
	ORACLE_MAX_STEPS="${CURVED_LADDER_ORACLE_MAX_STEPS:-4096}"
fi

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/curved_field_validation_ladder.log"
exec > >(tee -a "$LOG") 2>&1

echo "[curved-ladder] output=$OUTPUT_DIR"
echo "[curved-ladder] curved=$CURVED_FIXTURE scene=$CURVED_SCENE"
echo "[curved-ladder] control=$CONTROL_FIXTURE scene=$CONTROL_SCENE"
echo "[curved-ladder] frames=$FRAMES warmup=$WARMUP res=$RES traversal=$TRAVERSAL stride=$STRIDE"
echo "[curved-ladder] steps=$STEPS_RAW primary=$PRIMARY_STEP reference=$REFERENCE_STEP"
echo "[curved-ladder] diagnostic-only: outputs never feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision"

step_csv() {
	local first=1
	for step in $STEPS_RAW; do
		if [[ "$first" == "1" ]]; then
			printf "%s" "$step"
			first=0
		else
			printf ",%s" "$step"
		fi
	done
}

STEP_CSV="$(step_csv)"

cat > "$OUTPUT_DIR/run_metadata.json" <<EOF
{
  "study": "curved_field_validation_ladder",
  "timestamp": "$TIMESTAMP",
  "curved_scene": "$CURVED_SCENE",
  "curved_fixture": "$CURVED_FIXTURE",
  "control_scene": "$CONTROL_SCENE",
  "control_fixture": "$CONTROL_FIXTURE",
  "resolution": "$RES",
  "frames": $FRAMES,
  "warmup": $WARMUP,
  "stride": $STRIDE,
  "traversal": "$TRAVERSAL",
  "scheduler_mode": "$TRAVERSAL",
  "primary_step": "$PRIMARY_STEP",
  "reference_step": "$REFERENCE_STEP",
  "step_ladder": "$STEP_CSV",
  "manual_rois": "$MANUAL_ROIS",
  "diagnostic_only": true,
  "guardrail": "Do not describe visible band/support artifacts as caused by curvature unless comparison metrics support that claim."
}
EOF

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

step_token() {
	echo "$1" | tr '.' 'p'
}

discover_image_and_csv() {
	"$PYTHON" - "$1" <<'PY'
from pathlib import Path
import sys
folder = Path(sys.argv[1])
csvs = sorted(folder.glob("*.hit_diagnostics.csv"))
skip = ("layer", "combined_", "diagnostic_", "transport_", "ownership_", "unstable_", "graph_", "merge_", "hit_normal_", "camera_", "epsilon_", "first_stable_", "decision_", "path_", "normal_", "island_", "precision_", "production_", "oracle_", "parent_", "convergence_")
imgs = [p for p in folder.glob("*.png") if not p.name.startswith(skip) and "overlay" not in p.name]
imgs.sort(key=lambda p: p.stat().st_size, reverse=True)
print(str(imgs[0]) if imgs else "")
print(str(csvs[0]) if csvs else "")
PY
}

postprocess_cell() {
	local cell_dir="$1"
	local roi_bbox="${2:-}"
	if [[ ! -f "$cell_dir/effective_status.txt" ]] || [[ "$(tr -d '[:space:]' < "$cell_dir/effective_status.txt")" != "0" ]]; then
		echo "[curved-ladder] skip postprocess failed cell=$cell_dir"
		return 0
	fi
	echo "[curved-ladder] postprocess cell=$cell_dir"
	"$PYTHON" "$ROOT/tools/diagnostic_wireframe_overlay.py" "$cell_dir" --continuity 1 --manual-rois "$MANUAL_ROIS" || true
	if [[ -n "$roi_bbox" ]]; then
		"$PYTHON" "$ROOT/tools/transport_ownership_graph_extractor.py" "$cell_dir" --roi-bbox "$roi_bbox" --reference-step "$REFERENCE_STEP" --out "$cell_dir" --visualize 1 || true
	else
		"$PYTHON" "$ROOT/tools/transport_ownership_graph_extractor.py" "$cell_dir" --reference-step "$REFERENCE_STEP" --out "$cell_dir" --visualize 1 || true
	fi
	"$PYTHON" "$ROOT/tools/transport_ownership_graph_validation.py" "$cell_dir" || true
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
			--projection xz \
			--flip-y 1 \
			--debug-normals 1 \
			--out "$cell_dir" || true
		"$PYTHON" "$ROOT/tools/camera_cross_section_minimap_overlay.py" \
			--image "$image_path" \
			--hit-csv "$hit_csv" \
			--layout quad_panel \
			--panel-size 320x180 \
			--slice-target hit_centroid \
			--minimap 1 \
			--minimap-size 140 \
			--draw-hit-normals 1 \
			--out "$cell_dir/cross_section_quad_panel" || true
		if [[ -f "$cell_dir/cross_section_quad_panel/diagnostic_quad_panel.png" ]]; then
			cp "$cell_dir/cross_section_quad_panel/diagnostic_quad_panel.png" "$cell_dir/diagnostic_quad_panel.png"
		fi
	fi
	"$PYTHON" "$ROOT/tools/graph_plus_hit_normals_report.py" "$cell_dir" --projection xz --flip-y 1 || true
}

write_cell_metadata() {
	local cell_dir="$1"
	local role="$2"
	local phase="$3"
	local fixture="$4"
	local scene="$5"
	local step="$6"
	local exit_code="$7"
	local effective="$8"
	local notes="$9"
	cat > "$cell_dir/metadata.json" <<EOF
{
  "study": "curved_field_validation_ladder",
  "role": "$role",
  "phase": "$phase",
  "fixture": "$fixture",
  "scene": "$scene",
  "step_length": "$step",
  "source_step_length": "$step",
  "reference_step_length": "$REFERENCE_STEP",
  "resolution": "$RES",
  "camera_pose_key": "$fixture:$scene",
  "frames": $FRAMES,
  "warmup": $WARMUP,
  "stride": $STRIDE,
  "traversal": "$TRAVERSAL",
  "scheduler_mode": "$TRAVERSAL",
  "resolver_flags": "domain_telemetry=0;domain_resolver=0;step_convergence=0",
  "step_ladder": "$STEP_CSV",
  "hit_diagnostics_flags": "diagnostic_wireframe_overlay=1;transport=1;risk=1;continuity=1",
  "manual_rois": "$MANUAL_ROIS",
  "cell_dir": "$cell_dir",
  "exit_code": $exit_code,
  "effective_status": $effective,
  "notes": "$notes",
  "diagnostic_only": true
}
EOF
}

run_render_cell() {
	local role="$1"
	local fixture="$2"
	local scene="$3"
	local step="$4"
	local cell_dir="$OUTPUT_DIR/$role/steps/step_$step"
	mkdir -p "$cell_dir"
	echo "[curved-ladder] render role=$role fixture=$fixture step=$step"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$scene" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$fixture" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=${role}_step_$(step_token "$step")" \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		--render-test-film-scale=1.0 \
		--render-test-camera-fixed=1 \
		"--render-test-step-length=$step" \
		"--render-test-pixel-stride=$STRIDE" \
		"--render-test-first-pass-traversal=$TRAVERSAL" \
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
	write_cell_metadata "$cell_dir" "$role" "step_capture" "$fixture" "$scene" "$step" "$exit_code" "$effective" "$notes"
	postprocess_cell "$cell_dir"
}

run_oracle_cell() {
	local role="$1"
	local fixture="$2"
	local scene="$3"
	local cell_dir="$OUTPUT_DIR/$role/oracle"
	mkdir -p "$cell_dir"
	echo "[curved-ladder] oracle role=$role fixture=$fixture"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$scene" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$fixture" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=${role}_reference_transport_oracle" \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		--render-test-film-scale=1.0 \
		--render-test-camera-fixed=1 \
		"--render-test-step-length=$PRIMARY_STEP" \
		"--render-test-pixel-stride=$STRIDE" \
		"--render-test-first-pass-traversal=$TRAVERSAL" \
		--benchmark-deterministic=1 \
		--benchmark-fixed-seed=1337 \
		--reference-transport-oracle=1 \
		"--reference-transport-oracle-manual-rois=$MANUAL_ROIS" \
		"--reference-transport-oracle-max-pixels=$ORACLE_MAX_PIXELS" \
		"--reference-transport-oracle-patch-size=$ORACLE_PATCH_SIZE" \
		"--reference-transport-oracle-production-steps=$STEP_CSV" \
		"--reference-transport-oracle-step-length=$ORACLE_STEP" \
		"--reference-transport-oracle-tolerance=$ORACLE_TOLERANCE" \
		"--reference-transport-oracle-max-steps=$ORACLE_MAX_STEPS" \
		"--reference-transport-oracle-replay-count=$ORACLE_REPLAY_COUNT" \
		--reference-transport-oracle-adaptive-refinement=1 \
		--reference-transport-oracle-family-samples=1 \
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
	write_cell_metadata "$cell_dir" "$role" "oracle" "$fixture" "$scene" "$PRIMARY_STEP" "$exit_code" "$effective" "$notes"
	if [[ "$effective" == "0" ]]; then
		postprocess_cell "$cell_dir"
		"$PYTHON" "$ROOT/tools/reference_transport_oracle_analysis.py" "$cell_dir" || true
		"$PYTHON" "$ROOT/tools/reference_transport_oracle_island_analysis.py" "$cell_dir" --sealed-step 0.00625 || true
	fi
}

echo "[curved-ladder] static checks"
"$PYTHON" -m py_compile \
	"$ROOT/tools/curved_field_validation_ladder_analysis.py" \
	"$ROOT/tools/reference_transport_oracle_analysis.py" \
	"$ROOT/tools/reference_transport_oracle_island_analysis.py" \
	"$ROOT/tools/transport_ownership_graph_extractor.py" \
	"$ROOT/tools/transport_ownership_graph_validation.py" \
	"$ROOT/tools/graph_plus_hit_normals_report.py" \
	"$ROOT/tools/hit_normal_vector_overlay.py" \
	"$ROOT/tools/camera_cross_section_minimap_overlay.py" \
	"$ROOT/tools/diagnostic_wireframe_overlay.py"
if [[ "${CURVED_LADDER_SKIP_BUILD:-0}" != "1" ]]; then
	dotnet build "$ROOT/Physical Light and Camera Units.csproj"
fi
echo "[curved-ladder] checks done"

for role in control curved; do
	if [[ "$role" == "control" ]]; then
		fixture="$CONTROL_FIXTURE"
		scene="$CONTROL_SCENE"
	else
		fixture="$CURVED_FIXTURE"
		scene="$CURVED_SCENE"
	fi
	for step in $STEPS_RAW; do
		run_render_cell "$role" "$fixture" "$scene" "$step"
	done
	echo "[curved-ladder] graph ladder role=$role"
	"$PYTHON" "$ROOT/tools/transport_ownership_graph_extractor.py" "$OUTPUT_DIR/$role/steps" \
		--reference-step "$REFERENCE_STEP" \
		--out "$OUTPUT_DIR/$role/graph_ladder" \
		--visualize 1 || true
	"$PYTHON" "$ROOT/tools/transport_ownership_graph_validation.py" "$OUTPUT_DIR/$role/graph_ladder" || true
done

run_oracle_cell "curved" "$CURVED_FIXTURE" "$CURVED_SCENE"

"$PYTHON" "$ROOT/tools/curved_field_validation_ladder_analysis.py" "$OUTPUT_DIR" --repo-root "$ROOT"

echo "[curved-ladder] complete output=$OUTPUT_DIR"
