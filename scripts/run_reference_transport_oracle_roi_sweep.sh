#!/usr/bin/env bash
# ReferenceTransportOracle ROI sweep for xPRIMEray.
#
# Full local run:
#   ORACLE_MAX_HOURS=12 bash scripts/run_reference_transport_oracle_roi_sweep.sh
#
# Smoke:
#   ORACLE_SMOKE=1 bash scripts/run_reference_transport_oracle_roi_sweep.sh

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCENE="${ORACLE_SCENE:-res://test-domain-resolver-stress.tscn}"
FIXTURE="${ORACLE_FIXTURE:-domain_resolver_stress}"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${ORACLE_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${ORACLE_ROOT:-$ROOT/output/reference_transport_oracle_roi_sweep/$TIMESTAMP}"
if [[ "$OUTPUT_DIR" != /* ]]; then
	OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

MAX_HOURS="${ORACLE_MAX_HOURS:-12}"
MAX_SECONDS="$("$PYTHON" - <<PY
print(int(float("$MAX_HOURS") * 3600))
PY
)"
START_EPOCH="$(date +%s)"

FRAMES="${ORACLE_FRAMES:-90}"
WARMUP="${ORACLE_WARMUP:-5}"
RES="${ORACLE_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
TRAVERSALS_RAW="${ORACLE_TRAVERSALS:-row}"
STRIDES_RAW="${ORACLE_STRIDES:-1}"
PRODUCTION_STEPS="${ORACLE_PRODUCTION_STEPS:-0.02,0.015,0.0125,0.00625,0.003125}"
ORACLE_STEP="${ORACLE_STEP_LENGTH:-0.0015625}"
ORACLE_TOLERANCE="${ORACLE_TOLERANCE:-0.0001}"
ORACLE_MAX_STEPS="${ORACLE_MAX_STEPS:-65536}"
ORACLE_REPLAY_COUNT="${ORACLE_REPLAY_COUNT:-2}"
ORACLE_ADAPTIVE_REFINEMENT="${ORACLE_ADAPTIVE_REFINEMENT:-1}"
ORACLE_FAMILY_SAMPLES="${ORACLE_FAMILY_SAMPLES:-1}"
ORACLE_MAX_PIXELS="${ORACLE_MAX_PIXELS:-64}"
ORACLE_PATCH_SIZE="${ORACLE_PATCH_SIZE:-9}"
ORACLE_MANUAL_ROIS="${ORACLE_MANUAL_ROIS:-40,35;280,35;40,145;280,145}"
ORACLE_LATEST_TILE_COMMIT_DIR="${ORACLE_LATEST_TILE_COMMIT_DIR:-auto}"
SMOKE="${ORACLE_SMOKE:-0}"

if [[ "$SMOKE" == "1" ]]; then
	FRAMES=5
	WARMUP=0
	TRAVERSALS_RAW="row"
	STRIDES_RAW="1"
	PRODUCTION_STEPS="0.015"
	ORACLE_MAX_PIXELS=8
	ORACLE_PATCH_SIZE=3
	ORACLE_MAX_STEPS=4096
	ORACLE_MANUAL_ROIS="40,35"
	ORACLE_LATEST_TILE_COMMIT_DIR=""
fi

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/reference_transport_oracle.log"
exec > >(tee -a "$LOG") 2>&1

echo "[reference-transport-oracle] output=$OUTPUT_DIR"
echo "[reference-transport-oracle] frames=$FRAMES warmup=$WARMUP res=${FILM_W}x${FILM_H} max_hours=$MAX_HOURS"
echo "[reference-transport-oracle] production_steps=$PRODUCTION_STEPS oracle_step=$ORACLE_STEP tolerance=$ORACLE_TOLERANCE max_steps=$ORACLE_MAX_STEPS"

resolve_latest_tile_commit() {
	if [[ "$ORACLE_LATEST_TILE_COMMIT_DIR" != "auto" ]]; then
		echo "$ORACLE_LATEST_TILE_COMMIT_DIR"
		return 0
	fi
	ls -td "$ROOT"/output/tile_commit_traversal_comparison/* 2>/dev/null | grep -v '\.zip$' | head -1 || true
}

build_rois() {
	local latest
	latest="$(resolve_latest_tile_commit)"
	"$PYTHON" - "$ORACLE_MANUAL_ROIS" "$latest" <<'PY'
import csv, sys
from pathlib import Path
manual, latest = sys.argv[1], sys.argv[2]
pts = []
def add(x, y):
    try:
        p = (int(round(float(x))), int(round(float(y))))
    except Exception:
        return
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
        if len(pts) >= 12:
            break
    if len(pts) < 12:
        for path in sorted(root.glob('**/transport_continuity_vectors.csv')):
            try:
                with path.open(newline='') as handle:
                    rows = sorted(csv.DictReader(handle), key=lambda r: float(r.get('total_transport_discontinuity_score') or 0), reverse=True)
                for row in rows[:64]:
                    add(row.get('x', 0), row.get('y', 0))
                    if len(pts) >= 12:
                        break
            except Exception:
                pass
            if len(pts) >= 12:
                break
print(';'.join(f'{x},{y}' for x, y in pts))
PY
}

SWEEP_ROIS="$(build_rois)"
if [[ -z "$SWEEP_ROIS" ]]; then
	SWEEP_ROIS="$ORACLE_MANUAL_ROIS"
fi
echo "[reference-transport-oracle] rois=$SWEEP_ROIS"

time_budget_exceeded() {
	local now elapsed
	now="$(date +%s)"
	elapsed=$((now - START_EPOCH))
	[[ "$elapsed" -ge "$MAX_SECONDS" ]]
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
	"$PYTHON" - "$OUTPUT_DIR" <<'PY'
import csv, json, hashlib, sys
from pathlib import Path
root = Path(sys.argv[1])
rows = []
for meta_path in sorted(root.glob('cells/*/metadata.json')):
    meta = json.loads(meta_path.read_text())
    cell = Path(meta.get('cell_dir', meta_path.parent))
    packet = next(iter(sorted(cell.glob('*.reference_transport_oracle.json'))), None)
    report = cell / 'reference_transport_oracle_report.md'
    packet_json = json.loads(packet.read_text()) if packet and packet.exists() else {}
    comparisons = next(iter(sorted(cell.glob('*.reference_transport_oracle_comparisons.csv'))), None)
    stable = unresolved = multi = snap = 0
    repeat_fail = 0
    if comparisons and comparisons.exists():
        with comparisons.open(newline='') as handle:
            for row in csv.DictReader(handle):
                cls = row.get('epsilon_stability_class', '')
                stable += cls == 'stable'
                unresolved += cls == 'unresolved'
                multi += cls == 'multi_solution'
                snap += cls == 'threshold_snap'
                repeat_fail += row.get('oracle_repeat_match') not in {'1', 'true', 'True'}
    rows.append({
        'timestamp': meta.get('timestamp', ''),
        'traversal': meta.get('traversal', ''),
        'stride': meta.get('stride', ''),
        'effective_status': meta.get('effective_status', ''),
        'cell_dir': str(cell),
        'sample_count': packet_json.get('sample_count', ''),
        'comparison_count': packet_json.get('comparison_count', ''),
        'stable_count': stable,
        'threshold_snap_count': snap,
        'unresolved_count': unresolved,
        'multi_solution_count': multi,
        'oracle_repeat_fail_count': repeat_fail,
        'runtime_ms': packet_json.get('runtime_ms', ''),
        'report': str(report) if report.exists() else '',
        'notes': meta.get('notes', ''),
    })
cols = ['timestamp','traversal','stride','effective_status','cell_dir','sample_count','comparison_count','stable_count','threshold_snap_count','unresolved_count','multi_solution_count','oracle_repeat_fail_count','runtime_ms','report','notes']
with (root / 'reference_transport_oracle_summary.csv').open('w', newline='') as handle:
    writer = csv.DictWriter(handle, fieldnames=cols)
    writer.writeheader()
    for row in rows:
        writer.writerow(row)
(root / 'reference_transport_oracle_summary.json').write_text(json.dumps(rows, indent=2, sort_keys=True) + '\n')
lines = ['# ReferenceTransportOracle ROI Sweep Summary', '', f'- Cells: {len(rows)}']
for row in rows:
    lines.append(f"- traversal={row['traversal']} stride={row['stride']} status={row['effective_status']} stable={row['stable_count']} snap={row['threshold_snap_count']} unresolved={row['unresolved_count']} multi={row['multi_solution_count']}")
(root / 'reference_transport_oracle_summary.md').write_text('\n'.join(lines) + '\n')
PY
}

run_cell() {
	local traversal="$1"
	local stride="$2"
	local cell_dir="$OUTPUT_DIR/cells/${traversal}_stride_${stride}"
	mkdir -p "$cell_dir"
	if [[ -f "$cell_dir/effective_status.txt" ]] && [[ "$(tr -d '[:space:]' < "$cell_dir/effective_status.txt")" == "0" ]]; then
		echo "[reference-transport-oracle] skip completed traversal=$traversal stride=$stride"
		refresh_summary
		return 0
	fi

	echo "[reference-transport-oracle] run traversal=$traversal stride=$stride"
	set +e
	"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
		--render-test \
		--domain-audit-quick \
		"--render-test-fixture=$FIXTURE" \
		--render-test-capture=1 \
		"--render-test-capture-dir=$cell_dir" \
		"--render-test-capture-mode=reference_transport_oracle_${traversal}_stride_${stride}" \
		"--render-test-frames=$FRAMES" \
		"--render-test-warmup=$WARMUP" \
		"--render-test-film-width=$FILM_W" \
		"--render-test-film-height=$FILM_H" \
		--render-test-film-scale=1.0 \
		--render-test-camera-fixed=1 \
		--render-test-step-length=0.015 \
		"--render-test-pixel-stride=$stride" \
		"--render-test-first-pass-traversal=$traversal" \
		--benchmark-deterministic=1 \
		--benchmark-fixed-seed=1337 \
		--reference-transport-oracle=1 \
		"--reference-transport-oracle-manual-rois=$SWEEP_ROIS" \
		"--reference-transport-oracle-max-pixels=$ORACLE_MAX_PIXELS" \
		"--reference-transport-oracle-patch-size=$ORACLE_PATCH_SIZE" \
		"--reference-transport-oracle-production-steps=$PRODUCTION_STEPS" \
		"--reference-transport-oracle-step-length=$ORACLE_STEP" \
		"--reference-transport-oracle-tolerance=$ORACLE_TOLERANCE" \
		"--reference-transport-oracle-max-steps=$ORACLE_MAX_STEPS" \
		"--reference-transport-oracle-replay-count=$ORACLE_REPLAY_COUNT" \
		"--reference-transport-oracle-adaptive-refinement=$ORACLE_ADAPTIVE_REFINEMENT" \
		"--reference-transport-oracle-family-samples=$ORACLE_FAMILY_SAMPLES" \
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
  "study": "reference_transport_oracle_roi_sweep",
  "fixture": "$FIXTURE",
  "traversal": "$traversal",
  "stride": $stride,
  "resolution": "${FILM_W}x${FILM_H}",
  "production_steps": "$PRODUCTION_STEPS",
  "oracle_step_length": "$ORACLE_STEP",
  "oracle_tolerance": "$ORACLE_TOLERANCE",
  "roi_sources": "$SWEEP_ROIS",
  "diagnostic_only": true,
  "guardrail": "ReferenceTransportOracle outputs must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.",
  "cell_dir": "$cell_dir",
  "exit_code": $exit_code,
  "effective_status": $effective,
  "notes": "$notes"
}
EOF
	if [[ "$effective" == "0" ]]; then
		"$PYTHON" "$ROOT/tools/reference_transport_oracle_analysis.py" "$cell_dir" >> "$cell_dir/run.log" 2>&1 || effective=1
	fi
	refresh_summary
	echo "[reference-transport-oracle] status traversal=$traversal stride=$stride exit=$exit_code effective=$effective notes=$notes"
}

echo "[reference-transport-oracle] static checks"
"$PYTHON" -m py_compile "$ROOT/tools/reference_transport_oracle_analysis.py" "$ROOT/tools/diagnostic_wireframe_overlay.py"
dotnet build "$ROOT/Physical Light and Camera Units.csproj"
echo "[reference-transport-oracle] checks done"

read -r -a TRAVERSALS <<< "$TRAVERSALS_RAW"
read -r -a STRIDES <<< "$STRIDES_RAW"
for traversal in "${TRAVERSALS[@]}"; do
	for stride in "${STRIDES[@]}"; do
		run_cell "$traversal" "$stride"
		if [[ -f "$OUTPUT_DIR/STOP" ]]; then
			echo "[reference-transport-oracle] STOP file found; exiting after completed cell"
			refresh_summary
			exit 0
		fi
		if time_budget_exceeded; then
			echo "[reference-transport-oracle] max runtime exceeded; exiting after completed cell"
			refresh_summary
			exit 0
		fi
	done
done

refresh_summary
echo "[reference-transport-oracle] complete output=$OUTPUT_DIR"
