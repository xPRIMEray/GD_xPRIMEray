#!/usr/bin/env bash
# Overnight Transport Ownership Boundary Graph Precision Sweep.
#
# Full local run:
#   GRAPH_SWEEP_MAX_HOURS=12 bash scripts/run_transport_ownership_graph_precision_sweep.sh
#
# Smoke:
#   GRAPH_SWEEP_SMOKE=1 bash scripts/run_transport_ownership_graph_precision_sweep.sh

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCENE="${GRAPH_SWEEP_SCENE:-res://test-domain-resolver-stress.tscn}"
FIXTURE="${GRAPH_SWEEP_FIXTURE:-domain_resolver_stress}"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${GRAPH_SWEEP_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${GRAPH_SWEEP_ROOT:-$ROOT/output/transport_ownership_graph_precision_sweep/$TIMESTAMP}"
if [[ "$OUTPUT_DIR" != /* ]]; then
	OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

MAX_HOURS="${GRAPH_SWEEP_MAX_HOURS:-12}"
MAX_SECONDS="$("$PYTHON" - <<PY
print(int(float("$MAX_HOURS") * 3600))
PY
)"
START_EPOCH="$(date +%s)"

FRAMES="${GRAPH_SWEEP_FRAMES:-90}"
WARMUP="${GRAPH_SWEEP_WARMUP:-5}"
RES="${GRAPH_SWEEP_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
FILM_SCALE="${GRAPH_SWEEP_FILM_SCALE:-1.0}"
DEEP="${GRAPH_SWEEP_DEEP:-0}"
FORCE_ALL="${GRAPH_SWEEP_FORCE_ALL:-0}"
OVERLAY="${GRAPH_SWEEP_OVERLAY:-1}"
CONTINUITY="${GRAPH_SWEEP_CONTINUITY:-1}"
LATEST_TILE_COMMIT_DIR="${GRAPH_SWEEP_LATEST_TILE_COMMIT_DIR:-auto}"
SMOKE="${GRAPH_SWEEP_SMOKE:-0}"
REFERENCE_STEP="${GRAPH_SWEEP_REFERENCE_STEP:-0.003125}"
STEPS_RAW="${GRAPH_SWEEP_STEPS:-0.02 0.016 0.015 0.014 0.013 0.0125 0.011 0.010 0.0075 0.00625 0.003125}"
TRAVERSALS_RAW="${GRAPH_SWEEP_TRAVERSALS:-row checkerboard}"
STRIDES_RAW="${GRAPH_SWEEP_STRIDES:-1 4}"
MANUAL_ROIS="${GRAPH_SWEEP_MANUAL_ROIS:-40,35;280,35;40,145;280,145}"
PATCH_SIZE="${GRAPH_SWEEP_PATCH_SIZE:-17}"
if [[ "$DEEP" == "1" ]]; then
	PATCH_SIZE="${GRAPH_SWEEP_PATCH_SIZE:-33}"
fi
RADII="${GRAPH_SWEEP_RADII:-2,4,8,16,32}"
MAX_ROIS="${GRAPH_SWEEP_MAX_ROIS:-12}"
if [[ "$SMOKE" == "1" ]]; then
	FRAMES="${GRAPH_SWEEP_FRAMES:-5}"
	WARMUP="${GRAPH_SWEEP_WARMUP:-0}"
	STEPS_RAW="${GRAPH_SWEEP_STEPS:-0.015 0.003125}"
	TRAVERSALS_RAW="${GRAPH_SWEEP_TRAVERSALS:-row}"
	STRIDES_RAW="${GRAPH_SWEEP_STRIDES:-1}"
	MAX_ROIS="${GRAPH_SWEEP_MAX_ROIS:-1}"
	PATCH_SIZE="${GRAPH_SWEEP_PATCH_SIZE:-17}"
fi

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/graph_sweep.log"
exec > >(tee -a "$LOG") 2>&1

echo "[graph-sweep] output=$OUTPUT_DIR"
echo "[graph-sweep] frames=$FRAMES warmup=$WARMUP res=${FILM_W}x${FILM_H} max_hours=$MAX_HOURS"
echo "[graph-sweep] steps=$STEPS_RAW reference=$REFERENCE_STEP traversals=$TRAVERSALS_RAW strides=$STRIDES_RAW"
echo "[graph-sweep] overlay=$OVERLAY continuity=$CONTINUITY deep=$DEEP patch=$PATCH_SIZE radii=$RADII"

resolve_latest_tile_commit() {
	if [[ "$LATEST_TILE_COMMIT_DIR" != "auto" ]]; then
		echo "$LATEST_TILE_COMMIT_DIR"
		return 0
	fi
	ls -td "$ROOT"/output/tile_commit_traversal_comparison/* 2>/dev/null | grep -v '\.zip$' | head -1 || true
}

build_rois() {
	local latest
	latest="$(resolve_latest_tile_commit)"
	"$PYTHON" - "$MANUAL_ROIS" "$latest" "$MAX_ROIS" <<'PY'
import csv, sys
from pathlib import Path
manual, latest, max_rois = sys.argv[1], sys.argv[2], int(sys.argv[3])
pts = []
def add(x, y):
    p = (int(round(float(x))), int(round(float(y))))
    if p not in pts:
        pts.append(p)
for item in manual.split(';'):
    if ',' in item:
        x, y = item.split(',', 1)
        add(x, y)
root = Path(latest) if latest else None
if root and root.exists():
    for path in sorted(root.glob('**/transport_shape_regions.csv')):
        try:
            with path.open(newline='') as handle:
                for row in csv.DictReader(handle):
                    if str(row.get('boundary_aligns_with_high_vector_density', '')).lower() == 'true':
                        add(row.get('centroid_x', 0), row.get('centroid_y', 0))
        except Exception:
            pass
        if len(pts) >= max_rois:
            break
    if len(pts) < max_rois:
        for path in sorted(root.glob('**/transport_continuity_vectors.csv')):
            try:
                with path.open(newline='') as handle:
                    rows = sorted(csv.DictReader(handle), key=lambda r: float(r.get('total_transport_discontinuity_score') or 0), reverse=True)
                for row in rows[:64]:
                    add(row.get('x', 0), row.get('y', 0))
                    if len(pts) >= max_rois:
                        break
            except Exception:
                pass
            if len(pts) >= max_rois:
                break
pts = pts[:max_rois]
print(';'.join(f'{x},{y}' for x, y in pts))
PY
}

SWEEP_ROIS="$(build_rois)"
if [[ -z "$SWEEP_ROIS" ]]; then
	SWEEP_ROIS="$MANUAL_ROIS"
fi
echo "[graph-sweep] rois=$SWEEP_ROIS"

refresh_summary() {
	"$PYTHON" "$ROOT/tools/transport_ownership_graph_analysis.py" "$OUTPUT_DIR" || true
}

cell_dir_for() {
	echo "$OUTPUT_DIR/cells/step_$1/${2}_stride_$3"
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

write_metadata() {
	local cell_dir="$1"
	local step="$2"
	local traversal="$3"
	local stride="$4"
	local exit_code="$5"
	local effective="$6"
	local notes="$7"
	local plateaued="$8"
	local ts
	ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
	cat > "$cell_dir/metadata.json" <<EOF
{
  "timestamp": "$ts",
  "study": "transport_ownership_graph_precision_sweep",
  "phase": "graph_precision_sweep",
  "fixture": "$FIXTURE",
  "step_length": "$step",
  "reference_step_length": "$REFERENCE_STEP",
  "traversal": "$traversal",
  "stride": $stride,
  "roi_label": "full_frame_control_plus_roi_seeds",
  "roi_sources": "$SWEEP_ROIS",
  "patch_size": $PATCH_SIZE,
  "radial_rings": "$RADII",
  "resolution": "${FILM_W}x${FILM_H}",
  "diagnostic_only": true,
  "graph_guardrail": "ownership graph outputs must not feed render scheduling, hit selection, shading, resolver decisions, or adaptive precision",
  "cell_dir": "$cell_dir",
  "exit_code": $exit_code,
  "effective_status": $effective,
  "plateaued": $plateaued,
  "notes": "$notes"
}
EOF
}

time_budget_exceeded() {
	local now elapsed
	now="$(date +%s)"
	elapsed=$((now - START_EPOCH))
	[[ "$elapsed" -ge "$MAX_SECONDS" ]]
}

declare -A RECENT_SIGNATURES
is_plateau_skip() {
	local key="$1"
	local step="$2"
	if [[ "$FORCE_ALL" == "1" || "$step" == "$REFERENCE_STEP" ]]; then
		return 1
	fi
	local hist="${RECENT_SIGNATURES[$key]:-}"
	IFS='|' read -r a b c _ <<< "$hist"
	[[ -n "$a" && "$a" == "$b" && "$b" == "$c" ]]
}

update_plateau_signature() {
	local key="$1"
	local cell_dir="$2"
	local summary="$cell_dir/ownership_graph_summary.json"
	if [[ ! -f "$summary" ]]; then
		return 0
	fi
	local sig
	sig="$("$PYTHON" - "$summary" <<'PY'
import json, sys
j=json.load(open(sys.argv[1]))
print(f"{j.get('graph_node_count')}:{j.get('graph_edge_count')}:{j.get('seam_length_px_total')}:{j.get('high_discontinuity_edge_count')}")
PY
)"
	local hist="${RECENT_SIGNATURES[$key]:-}"
	RECENT_SIGNATURES[$key]="$sig|$hist"
}

run_cell() {
	local step="$1"
	local traversal="$2"
	local stride="$3"
	local cell_dir key
	cell_dir="$(cell_dir_for "$step" "$traversal" "$stride")"
	key="${traversal}_stride_${stride}"
	mkdir -p "$cell_dir"

	if [[ -f "$cell_dir/effective_status.txt" ]] && [[ "$(tr -d '[:space:]' < "$cell_dir/effective_status.txt")" == "0" ]]; then
		echo "[graph-sweep] skip completed step=$step traversal=$traversal stride=$stride"
		refresh_summary
		return 0
	fi
	if is_plateau_skip "$key" "$step"; then
		echo "[graph-sweep] plateau skip step=$step traversal=$traversal stride=$stride"
		echo "0" > "$cell_dir/status.txt"
		echo "0" > "$cell_dir/effective_status.txt"
		write_metadata "$cell_dir" "$step" "$traversal" "$stride" "0" "0" "plateau_skip_three_consecutive_graph_signatures" "1"
		refresh_summary
		return 0
	fi

	echo "[graph-sweep] run step=$step traversal=$traversal stride=$stride"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$FIXTURE" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=graph_${traversal}_stride_${stride}" \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		"--render-test-film-scale=$FILM_SCALE" \
		--render-test-camera-fixed=1 \
		"--render-test-step-length=$step" \
		"--render-test-pixel-stride=$stride" \
		"--render-test-first-pass-traversal=$traversal" \
		--benchmark-deterministic=1 \
		--benchmark-fixed-seed=1337 \
		"--diagnostic-wireframe-overlay=$OVERLAY" \
		"--diagnostic-wireframe-cartesian=$OVERLAY" \
		"--diagnostic-wireframe-transport=$OVERLAY" \
		"--diagnostic-wireframe-risk=$OVERLAY" \
		"--diagnostic-wireframe-spacetime=0" \
		"--diagnostic-wireframe-continuity=$CONTINUITY" \
		"--diagnostic-wireframe-labels=$OVERLAY" \
		"--diagnostic-wireframe-manual-rois=$SWEEP_ROIS" \
		--enable-domain-telemetry=0 \
		--enable-domain-aware-first-hit-resolver=0 \
		--enable-step-convergence-telemetry=0 \
		> "$cell_dir/run.log" 2>&1
	local exit_code=$?
	set -e
	read -r effective notes <<< "$(effective_from_log "$exit_code" "$cell_dir/run.log")"
	echo "$exit_code" > "$cell_dir/status.txt"
	echo "$effective" > "$cell_dir/effective_status.txt"
	write_metadata "$cell_dir" "$step" "$traversal" "$stride" "$exit_code" "$effective" "$notes" "0"
	if [[ "$OVERLAY" == "1" ]]; then
		"$PYTHON" "$ROOT/tools/diagnostic_wireframe_overlay.py" "$cell_dir" --manual-rois "$SWEEP_ROIS" --continuity "$CONTINUITY" >> "$cell_dir/run.log" 2>&1 || true
	fi
	refresh_summary
	update_plateau_signature "$key" "$cell_dir"
	echo "[graph-sweep] status step=$step traversal=$traversal stride=$stride exit=$exit_code effective=$effective notes=$notes"
}

echo "[graph-sweep] static checks"
"$PYTHON" -m py_compile "$ROOT/tools/transport_ownership_graph_analysis.py" "$ROOT/tools/diagnostic_wireframe_overlay.py"
dotnet build "$ROOT"
echo "[graph-sweep] checks done"

read -r -a STEPS <<< "$STEPS_RAW"
read -r -a TRAVERSALS <<< "$TRAVERSALS_RAW"
read -r -a STRIDES <<< "$STRIDES_RAW"

for step in "${STEPS[@]}"; do
	for traversal in "${TRAVERSALS[@]}"; do
		for stride in "${STRIDES[@]}"; do
			run_cell "$step" "$traversal" "$stride"
			if [[ -f "$OUTPUT_DIR/STOP" ]]; then
				echo "[graph-sweep] STOP file found; exiting after completed cell"
				refresh_summary
				exit 0
			fi
			if time_budget_exceeded; then
				echo "[graph-sweep] max runtime exceeded; exiting after completed cell"
				refresh_summary
				exit 0
			fi
		done
	done
done

refresh_summary
echo "[graph-sweep] complete output=$OUTPUT_DIR"
