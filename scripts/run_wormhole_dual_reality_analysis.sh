#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

TIMESTAMP="${1:-$(date +"%Y-%m-%dT%H-%M-%S")}"
RUN_ROOT="$ROOT/output/wormhole_dual_reality_analysis/$TIMESTAMP"
IMAGES_DIR="$RUN_ROOT/images"
LOGS_DIR="$RUN_ROOT/logs"
REPORTS_DIR="$RUN_ROOT/reports"
SUPPORT_DIR="$RUN_ROOT/support"
CASE_TIMEOUT_SECONDS="${WORMHOLE_DUAL_REALITY_TIMEOUT_SECONDS:-190}"

mkdir -p "$IMAGES_DIR" "$LOGS_DIR" "$REPORTS_DIR" "$SUPPORT_DIR"

SCENE_PATH="res://test-wormhole-prototype.tscn"
GODOT_CMD=("./scripts/godot_local.sh" "--path" "." "--scene" "$SCENE_PATH" "--")
ANALYZER="$ROOT/tools/wormhole_dual_reality_analysis.py"

resolve_analysis_python() {
  local candidates=(
    "$ROOT/.venv_image_compare/bin/python"
    "$ROOT/.venv/bin/python"
    "$(command -v python3)"
  )
  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -n "$candidate" && -x "$candidate" ]]; then
      if "$candidate" -c 'import PIL, numpy, skimage' >/dev/null 2>&1; then
        printf '%s\n' "$candidate"
        return 0
      fi
    fi
  done

  printf '%s\n' "$(command -v python3)"
}

ANALYSIS_PYTHON="$(resolve_analysis_python)"

run_case() {
  local case_name="$1"
  local capture_kind="$2"
  shift 2

  local log_path="$LOGS_DIR/${case_name}.log"
  echo "[wormhole_dual_reality_analysis] running ${case_name}"
  local run_status=0
  timeout "${CASE_TIMEOUT_SECONDS}s" "${GODOT_CMD[@]}" "$@" >"$log_path" 2>&1 || run_status=$?
  if [[ "$run_status" -ne 0 && "$run_status" -ne 124 ]]; then
    echo "Case failed before capture: ${case_name} exit=${run_status}" >&2
    return "$run_status"
  fi

  local source_image=""
  case "$capture_kind" in
    film)
      source_image="$ROOT/output/wormhole_test/wormhole_validation_capture.png"
      ;;
    composed)
      source_image="$ROOT/output/wormhole_test/wormhole_validation_composed.png"
      ;;
    *)
      echo "Unknown capture kind: $capture_kind" >&2
      return 1
      ;;
  esac

  if [[ ! -f "$source_image" ]]; then
    echo "Expected capture missing for ${case_name}: $source_image" >&2
    return 1
  fi

  if ! rg -q "capture_saved .*source=" "$log_path"; then
    echo "Expected capture marker missing for ${case_name}: $log_path" >&2
    return 1
  fi

  cp "$source_image" "$IMAGES_DIR/${case_name}.png"

  if [[ -f "$ROOT/output/dual_reality/wormhole_inset_baseline.png" ]]; then
    cp "$ROOT/output/dual_reality/wormhole_inset_baseline.png" \
      "$SUPPORT_DIR/${case_name}__straight_transport_reference.png"
  fi

  if [[ -f "$ROOT/output/wormhole_test/wormhole_portal_sector_report.json" ]]; then
    cp "$ROOT/output/wormhole_test/wormhole_portal_sector_report.json" \
      "$REPORTS_DIR/${case_name}__portal_sector_report.json"
  fi
}

run_case \
  "wormhole_clean_curved" \
  "film" \
  --camera-preset=validation_nearfield

run_case \
  "wormhole_reference_only" \
  "composed" \
  --camera-preset=validation_nearfield \
  --dual-reality=1 \
  --dual-reality-inset=1 \
  --dual-reality-freeze=1

run_case \
  "wormhole_reference_plus_semantic" \
  "composed" \
  --camera-preset=validation_nearfield \
  --dual-reality=1 \
  --dual-reality-inset=1 \
  --dual-reality-wireframe=1 \
  --dual-reality-freeze=1

run_case \
  "wormhole_reference_plus_curvature" \
  "composed" \
  --camera-preset=validation_nearfield \
  --dual-reality=1 \
  --dual-reality-inset=1 \
  --dual-reality-overlay=curvature \
  --dual-reality-curvature-placement=fullscreen \
  --dual-reality-curvature-metric=turnsum \
  --dual-reality-freeze=1

run_case \
  "wormhole_reference_plus_collision" \
  "composed" \
  --camera-preset=validation_nearfield \
  --dual-reality=1 \
  --dual-reality-inset=1 \
  --dual-reality-collision=1 \
  --dual-reality-collision-placement=both \
  --dual-reality-freeze=1

run_case \
  "wormhole_full_stack_curvature" \
  "composed" \
  --camera-preset=validation_nearfield \
  --dual-reality=1 \
  --dual-reality-inset=1 \
  --dual-reality-wireframe=1 \
  --dual-reality-collision=1 \
  --dual-reality-collision-placement=both \
  --dual-reality-overlay=curvature \
  --dual-reality-curvature-placement=fullscreen \
  --dual-reality-curvature-metric=turnsum \
  --dual-reality-freeze=1

"$ANALYSIS_PYTHON" "$ANALYZER" --run-root "$RUN_ROOT"

echo "[wormhole_dual_reality_analysis] run_root=$RUN_ROOT"
