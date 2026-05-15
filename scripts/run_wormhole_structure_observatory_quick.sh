#!/usr/bin/env bash
set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

RUN_LABEL="${1:-$(date -u +%Y%m%dT%H%M%SZ)}"
OUTPUT_ROOT="${WORMHOLE_STRUCTURE_ROOT:-$ROOT/output/wormhole_structure_observatory}"
RUN_ROOT="$OUTPUT_ROOT/$RUN_LABEL"
PANELS_DIR="$RUN_ROOT/panels"
LOGS_DIR="$RUN_ROOT/logs"
QUALITY="${WORMHOLE_STRUCTURE_QUALITY:-quick_review}"
PANEL_TIMEOUT_SECONDS="${WORMHOLE_STRUCTURE_PANEL_TIMEOUT_SECONDS:-95}"
EXTRA_PANELS="${WORMHOLE_STRUCTURE_EXTRA_PANELS:-0}"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
SCENE_PATH="${WORMHOLE_STRUCTURE_SCENE:-res://test-wormhole-prototype.tscn}"
PYTHON="${WORMHOLE_STRUCTURE_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
	PYTHON="python3"
fi

mkdir -p "$PANELS_DIR" "$LOGS_DIR"
OBS_LOG="$RUN_ROOT/wormhole_structure_observatory.log"
exec > >(tee -a "$OBS_LOG") 2>&1

echo "[wormhole-structure] output=$RUN_ROOT"
echo "[wormhole-structure] quality=$QUALITY panel_timeout_seconds=$PANEL_TIMEOUT_SECONDS extra_panels=$EXTRA_PANELS"
echo "[wormhole-structure] purpose=visual_observatory_only transport_logic=unchanged validation_verdicts=unchanged"

write_panel_status() {
	local panel_id="$1"
	local title="$2"
	local status="$3"
	local exit_code="$4"
	local duration_seconds="$5"
	local timed_out="$6"
	local image_path="$7"
	local log_path="$8"
	local notes="$9"
	"$PYTHON" - "$PANELS_DIR/$panel_id/panel_status.json" "$panel_id" "$title" "$status" "$exit_code" "$duration_seconds" "$timed_out" "$image_path" "$log_path" "$notes" <<'PY'
import json
import sys
from pathlib import Path

out, panel_id, title, status, exit_code, duration, timed_out, image_path, log_path, notes = sys.argv[1:]
payload = {
    "panel_id": panel_id,
    "title": title,
    "status": status,
    "exit_code": int(exit_code),
    "duration_seconds": float(duration),
    "timed_out": timed_out == "1",
    "image": image_path,
    "log": log_path,
    "notes": notes,
}
Path(out).write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

run_panel() {
	local panel_id="$1"
	local title="$2"
	local image_kind="$3"
	shift 3

	local panel_dir="$PANELS_DIR/$panel_id"
	local log_path="$LOGS_DIR/$panel_id.log"
	mkdir -p "$panel_dir"

	local film_path="$panel_dir/${panel_id}_film.png"
	local composed_path="$panel_dir/${panel_id}_composed.png"
	local dual_path="$panel_dir/${panel_id}_dual_reality.png"
	local film_res_path="res://output/wormhole_structure_observatory/$RUN_LABEL/panels/$panel_id/${panel_id}_film.png"
	local composed_res_path="res://output/wormhole_structure_observatory/$RUN_LABEL/panels/$panel_id/${panel_id}_composed.png"
	local dual_res_path="res://output/wormhole_structure_observatory/$RUN_LABEL/panels/$panel_id/${panel_id}_dual_reality.png"
	local expected_path="$film_path"
	local capture_marker="source=film_buffer"
	if [[ "$image_kind" == "composed" ]]; then
		expected_path="$composed_path"
		capture_marker="source=viewport_composite"
	elif [[ "$image_kind" == "domain" ]]; then
		expected_path="${film_path%.png}.domain_id.png"
		capture_marker="source=film_buffer"
	fi

	echo "[wormhole-structure] panel=$panel_id title=\"$title\""
	local start_epoch end_epoch duration
	start_epoch="$(date +%s)"
	local run_status=0
	timeout "${PANEL_TIMEOUT_SECONDS}s" "$GODOT_BIN" --path "$ROOT" --scene "$SCENE_PATH" -- \
		--camera-preset=validation_nearfield \
		--wormhole-validation-delay=8 \
		--wormhole-validation-max-delay=16 \
		--wormhole-exit-after-capture=1 \
		"--wormhole-validation-capture-path=$film_res_path" \
		"--wormhole-validation-composite-path=$composed_res_path" \
		"--wormhole-dual-reality-capture-path=$dual_res_path" \
		"$@" > "$log_path" 2>&1 || run_status=$?
	end_epoch="$(date +%s)"
	duration=$((end_epoch - start_epoch))

	local status="ok"
	local timed_out="0"
	local notes="capture_found"
	if [[ "$run_status" -eq 124 ]]; then
		status="incomplete"
		timed_out="1"
		notes="panel_timeout"
	elif [[ ! -f "$expected_path" ]]; then
		status="incomplete"
		notes="missing_capture"
	elif ! rg -q "capture_saved .*${capture_marker}" "$log_path"; then
		status="incomplete"
		notes="missing_capture_marker"
	elif [[ "$run_status" -ne 0 ]]; then
		notes="capture_found_nonzero_exit_${run_status}"
	fi

	write_panel_status "$panel_id" "$title" "$status" "$run_status" "$duration" "$timed_out" "$expected_path" "$log_path" "$notes"
	echo "[wormhole-structure] panel_status panel=$panel_id status=$status exit=$run_status duration=${duration}s notes=$notes"
}

if [[ "$QUALITY" != "quick_review" ]]; then
	echo "[wormhole-structure] quality_not_available quality=$QUALITY note=review_full_reserved_for_manual_storytelling"
	"$PYTHON" "$ROOT/tools/wormhole_structure_observatory_report.py" "$RUN_ROOT" --quality "$QUALITY" --quality-unavailable
	exit 2
fi

run_panel "clean_curved" "Clean curved render" "film" \
	--wormhole-film-shading=normal_rgb

run_panel "straight_vs_curved" "Straight reference vs curved transport" "composed" \
	--wormhole-film-shading=normal_rgb \
	--dual-reality=1 \
	--dual-reality-inset=1 \
	--dual-reality-freeze=1

run_panel "depth_heatmap" "Depth heatmap" "film" \
	--wormhole-film-shading=depth_heatmap

if [[ "$EXTRA_PANELS" == "1" ]]; then
	run_panel "step_budget_heatmap" "Step budget heatmap" "composed" \
		--wormhole-film-shading=normal_rgb \
		--dual-reality=1 \
		--dual-reality-overlay=curvature \
		--dual-reality-curvature-placement=fullscreen \
		--dual-reality-curvature-metric=steps \
		--dual-reality-freeze=1

	run_panel "domain_diagnostics" "Domain diagnostics" "domain" \
		--wormhole-film-shading=normal_rgb \
		--enable-domain-telemetry=1

	run_panel "structure_minimap" "Structure minimap" "composed" \
		--wormhole-film-shading=normal_rgb \
		--dual-reality=1 \
		--dual-reality-inset=1 \
		--dual-reality-wireframe=1 \
		--dual-reality-collision=1 \
		--dual-reality-collision-placement=both \
		--dual-reality-overlay=curvature \
		--dual-reality-curvature-placement=fullscreen \
		--dual-reality-curvature-metric=turnsum \
		--dual-reality-freeze=1
fi

"$PYTHON" "$ROOT/tools/wormhole_structure_observatory_report.py" "$RUN_ROOT" --quality "$QUALITY"

echo "[wormhole-structure] complete output=$RUN_ROOT"
