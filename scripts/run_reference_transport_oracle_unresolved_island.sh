#!/usr/bin/env bash
# Focused ReferenceTransportOracle refinement for the 20260505 unresolved island.
#
# Full local run:
#   bash scripts/run_reference_transport_oracle_unresolved_island.sh
#
# Smoke:
#   ORACLE_ISLAND_SMOKE=1 bash scripts/run_reference_transport_oracle_unresolved_island.sh
#
# Extra-fine follow-up for unresolved-at-0.003125 pixels:
#   ORACLE_ISLAND_EXTRA_FINE=1 bash scripts/run_reference_transport_oracle_unresolved_island.sh

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCENE="${ORACLE_ISLAND_SCENE:-res://test-domain-resolver-stress.tscn}"
FIXTURE="${ORACLE_ISLAND_FIXTURE:-domain_resolver_stress}"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${ORACLE_ISLAND_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${ORACLE_ISLAND_ROOT:-$ROOT/output/reference_transport_oracle_unresolved_island/$TIMESTAMP}"
if [[ "$OUTPUT_DIR" != /* ]]; then
	OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

FRAMES="${ORACLE_ISLAND_FRAMES:-90}"
WARMUP="${ORACLE_ISLAND_WARMUP:-5}"
RES="${ORACLE_ISLAND_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
CENTER="${ORACLE_ISLAND_CENTER:-40,34}"
TARGET_BBOX="${ORACLE_ISLAND_TARGET_BBOX:-36,31,44,37}"
LOCAL_PATCH_BBOX="${ORACLE_ISLAND_LOCAL_PATCH_BBOX:-32,27,48,43}"
PATCH_SIZE="${ORACLE_ISLAND_PATCH_SIZE:-17}"
if [[ "${ORACLE_ISLAND_DEEP:-0}" == "1" ]]; then
	PATCH_SIZE="${ORACLE_ISLAND_PATCH_SIZE:-33}"
fi
PRODUCTION_STEPS="${ORACLE_ISLAND_PRODUCTION_STEPS:-0.02,0.018,0.016,0.015,0.014,0.013,0.0125,0.011,0.010,0.0075,0.00625,0.003125}"
ORACLE_STEP="${ORACLE_ISLAND_ORACLE_STEP:-0.0015625}"
if [[ "${ORACLE_ISLAND_EXTRA_FINE:-0}" == "1" ]]; then
	ORACLE_STEP="${ORACLE_ISLAND_ORACLE_STEP:-0.00078125}"
fi
ORACLE_TOLERANCE="${ORACLE_ISLAND_TOLERANCE:-0.0001}"
ORACLE_MAX_STEPS="${ORACLE_ISLAND_MAX_STEPS:-65536}"
ORACLE_REPLAY_COUNT="${ORACLE_ISLAND_REPLAY_COUNT:-2}"
TRAVERSAL="${ORACLE_ISLAND_TRAVERSAL:-row}"
STRIDE="${ORACLE_ISLAND_STRIDE:-1}"
SMOKE="${ORACLE_ISLAND_SMOKE:-0}"

if [[ "$SMOKE" == "1" ]]; then
	FRAMES="${ORACLE_ISLAND_FRAMES:-5}"
	WARMUP="${ORACLE_ISLAND_WARMUP:-0}"
	PATCH_SIZE="${ORACLE_ISLAND_PATCH_SIZE:-5}"
	PRODUCTION_STEPS="${ORACLE_ISLAND_PRODUCTION_STEPS:-0.015,0.00625,0.003125}"
	ORACLE_STEP="${ORACLE_ISLAND_ORACLE_STEP:-0.0015625}"
	ORACLE_MAX_STEPS="${ORACLE_ISLAND_MAX_STEPS:-4096}"
fi

MAX_PIXELS=$((PATCH_SIZE * PATCH_SIZE))

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/unresolved_island.log"
exec > >(tee -a "$LOG") 2>&1

echo "[oracle-island] output=$OUTPUT_DIR"
echo "[oracle-island] center=$CENTER target_bbox=$TARGET_BBOX local_patch_bbox=$LOCAL_PATCH_BBOX patch=$PATCH_SIZE max_pixels=$MAX_PIXELS"
echo "[oracle-island] frames=$FRAMES warmup=$WARMUP res=${FILM_W}x${FILM_H} traversal=$TRAVERSAL stride=$STRIDE"
echo "[oracle-island] production_steps=$PRODUCTION_STEPS oracle_step=$ORACLE_STEP tolerance=$ORACLE_TOLERANCE max_steps=$ORACLE_MAX_STEPS"
echo "[oracle-island] diagnostic-only: oracle outputs never feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision"

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

run_oracle_cell() {
	local cell_dir="$1"
	local rois="$2"
	local patch_size="$3"
	local max_pixels="$4"
	local production_steps="$5"
	local oracle_step="$6"
	local mode_name="$7"
	mkdir -p "$cell_dir"
	echo "[oracle-island] run cell=$mode_name rois=$rois patch=$patch_size max_pixels=$max_pixels oracle_step=$oracle_step"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$FIXTURE" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=reference_transport_oracle_unresolved_island_${mode_name}" \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		--render-test-film-scale=1.0 \
		--render-test-camera-fixed=1 \
		--render-test-step-length=0.015 \
		"--render-test-pixel-stride=$STRIDE" \
		"--render-test-first-pass-traversal=$TRAVERSAL" \
		--benchmark-deterministic=1 \
		--benchmark-fixed-seed=1337 \
		--reference-transport-oracle=1 \
		"--reference-transport-oracle-manual-rois=$rois" \
		"--reference-transport-oracle-max-pixels=$max_pixels" \
		"--reference-transport-oracle-patch-size=$patch_size" \
		"--reference-transport-oracle-production-steps=$production_steps" \
		"--reference-transport-oracle-step-length=$oracle_step" \
		"--reference-transport-oracle-tolerance=$ORACLE_TOLERANCE" \
		"--reference-transport-oracle-max-steps=$ORACLE_MAX_STEPS" \
		"--reference-transport-oracle-replay-count=$ORACLE_REPLAY_COUNT" \
		--reference-transport-oracle-adaptive-refinement=1 \
		--reference-transport-oracle-family-samples=1 \
		--diagnostic-wireframe-overlay=1 \
		--diagnostic-wireframe-cartesian=0 \
		--diagnostic-wireframe-transport=1 \
		--diagnostic-wireframe-risk=1 \
		--diagnostic-wireframe-spacetime=0 \
		--diagnostic-wireframe-continuity=1 \
		--diagnostic-wireframe-labels=1 \
		"--diagnostic-wireframe-manual-rois=$CENTER" \
		--enable-domain-telemetry=0 \
		--enable-domain-aware-first-hit-resolver=0 \
		--enable-step-convergence-telemetry=0 \
		> "$cell_dir/run.log" 2>&1
	local exit_code=$?
	set -e
	read -r effective notes <<< "$(effective_from_log "$exit_code" "$cell_dir/run.log")"
	echo "$exit_code" > "$cell_dir/status.txt"
	echo "$effective" > "$cell_dir/effective_status.txt"
	local ts
	ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
	cat > "$cell_dir/metadata.json" <<EOF
{
  "timestamp": "$ts",
  "study": "reference_transport_oracle_unresolved_island",
  "fixture": "$FIXTURE",
  "mode": "$mode_name",
  "center": "$CENTER",
  "target_bbox": "$TARGET_BBOX",
  "local_patch_bbox": "$LOCAL_PATCH_BBOX",
  "patch_size": $patch_size,
  "max_pixels": $max_pixels,
  "production_steps": "$production_steps",
  "oracle_step_length": "$oracle_step",
  "oracle_tolerance": "$ORACLE_TOLERANCE",
  "traversal": "$TRAVERSAL",
  "stride": $STRIDE,
  "resolution": "${FILM_W}x${FILM_H}",
  "diagnostic_only": true,
  "guardrail": "ReferenceTransportOracle outputs must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.",
  "cell_dir": "$cell_dir",
  "exit_code": $exit_code,
  "effective_status": $effective,
  "notes": "$notes"
}
EOF
	if [[ "$effective" == "0" ]]; then
		"$PYTHON" "$ROOT/tools/diagnostic_wireframe_overlay.py" "$cell_dir" --continuity 1 --manual-rois "$CENTER" >> "$cell_dir/run.log" 2>&1 || true
		"$PYTHON" "$ROOT/tools/reference_transport_oracle_analysis.py" "$cell_dir" >> "$cell_dir/run.log" 2>&1 || effective=1
		"$PYTHON" "$ROOT/tools/reference_transport_oracle_island_analysis.py" "$cell_dir" \
			--patch-bbox "$LOCAL_PATCH_BBOX" \
			--target-bbox "$TARGET_BBOX" \
			--sealed-step 0.00625 >> "$cell_dir/run.log" 2>&1 || effective=1
	fi
	echo "$effective" > "$cell_dir/effective_status.txt"
	echo "[oracle-island] status cell=$mode_name exit=$exit_code effective=$effective notes=$notes"
}

refresh_root_summary() {
	"$PYTHON" - "$OUTPUT_DIR" <<'PY'
import csv, json, sys
from pathlib import Path
root = Path(sys.argv[1])
rows = []
for summary_path in sorted(root.glob('cells/*/unresolved_island_summary.json')):
    data = json.loads(summary_path.read_text())
    meta_path = summary_path.parent / 'metadata.json'
    meta = json.loads(meta_path.read_text()) if meta_path.exists() else {}
    rows.append({
        'cell': summary_path.parent.name,
        'effective_status': meta.get('effective_status', ''),
        'sample_count': data.get('sample_count', ''),
        'comparison_count': data.get('comparison_count', ''),
        'sealed_at_0.00625': data.get('sealed_at_0.00625', ''),
        'stable_at_0.00625_count': data.get('stable_at_0.00625_count', ''),
        'unresolved_at_0.003125_count': data.get('unresolved_at_0.003125_count', ''),
        'oracle_replay_failure_count': data.get('oracle_replay_failure_count', ''),
        'local_continuity_vector_count': data.get('local_continuity_vector_count', ''),
        'local_transport_shape_region_count': data.get('local_transport_shape_region_count', ''),
        'oracle_step_length': meta.get('oracle_step_length', ''),
        'cell_dir': str(summary_path.parent),
    })
cols = ['cell','effective_status','sample_count','comparison_count','sealed_at_0.00625','stable_at_0.00625_count','unresolved_at_0.003125_count','oracle_replay_failure_count','local_continuity_vector_count','local_transport_shape_region_count','oracle_step_length','cell_dir']
with (root / 'unresolved_island_root_summary.csv').open('w', newline='') as handle:
    writer = csv.DictWriter(handle, fieldnames=cols)
    writer.writeheader()
    for row in rows:
        writer.writerow(row)
(root / 'unresolved_island_root_summary.json').write_text(json.dumps(rows, indent=2, sort_keys=True) + '\n')
lines = ['# ReferenceTransportOracle Unresolved-Island Run', '']
for row in rows:
    lines.append(f"- {row['cell']}: status={row['effective_status']} samples={row['sample_count']} sealed_at_0.00625={row['sealed_at_0.00625']} unresolved_at_0.003125={row['unresolved_at_0.003125_count']} replay_failures={row['oracle_replay_failure_count']}")
(root / 'unresolved_island_root_summary.md').write_text('\n'.join(lines) + '\n')
PY
}

publish_main_outputs() {
	local src="$MAIN_CELL"
	for name in \
		unresolved_island_summary.csv \
		unresolved_island_summary.md \
		unresolved_island_summary.json \
		first_stable_step_map.png \
		decision_risk_gradient.png \
		path_length_delta_map.png \
		normal_angle_delta_map.png \
		ownership_transition_map.png \
		island_convergence_ladder.png \
		local_continuity_vectors.csv \
		local_transport_shape_regions.csv; do
		if [[ -f "$src/$name" ]]; then
			cp "$src/$name" "$OUTPUT_DIR/$name"
		fi
	done
}

echo "[oracle-island] static checks"
"$PYTHON" -m py_compile "$ROOT/tools/reference_transport_oracle_analysis.py" "$ROOT/tools/reference_transport_oracle_island_analysis.py" "$ROOT/tools/diagnostic_wireframe_overlay.py"
dotnet build "$ROOT/Physical Light and Camera Units.csproj"
echo "[oracle-island] checks done"

MAIN_CELL="$OUTPUT_DIR/cells/unresolved_island"
run_oracle_cell "$MAIN_CELL" "$CENTER" "$PATCH_SIZE" "$MAX_PIXELS" "$PRODUCTION_STEPS" "$ORACLE_STEP" "unresolved_island"
refresh_root_summary
publish_main_outputs

if [[ "${ORACLE_ISLAND_EXTRA_FINE:-0}" == "1" ]]; then
	PIXELS_CSV="$MAIN_CELL/extra_fine_required_pixels.csv"
	if [[ -s "$PIXELS_CSV" ]]; then
		UNRESOLVED_ROIS="$("$PYTHON" - "$PIXELS_CSV" <<'PY'
import csv, sys
from pathlib import Path
path = Path(sys.argv[1])
pts = []
with path.open(newline='') as handle:
    for row in csv.DictReader(handle):
        x, y = row.get('x'), row.get('y')
        if x not in ('', None) and y not in ('', None):
            pts.append(f"{int(float(x))},{int(float(y))}")
print(';'.join(pts))
PY
)"
		if [[ -n "$UNRESOLVED_ROIS" ]]; then
			count="$("$PYTHON" - <<PY
print(len("$UNRESOLVED_ROIS".split(';')))
PY
)"
			EXTRA_CELL="$OUTPUT_DIR/cells/extra_fine_unresolved_only"
			run_oracle_cell "$EXTRA_CELL" "$UNRESOLVED_ROIS" 1 "$count" "0.003125" "0.00078125" "extra_fine_unresolved_only"
			refresh_root_summary
			publish_main_outputs
		fi
	fi
fi

echo "[oracle-island] complete output=$OUTPUT_DIR"
