#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

if [[ -f ".env.local" ]]; then
  # shellcheck disable=SC1091
  source .env.local
fi

resolve_report_python() {
  local candidates=(
    "$ROOT/.venv/bin/python"
    "$ROOT/.venv_image_compare/bin/python"
    "$(command -v python3)"
  )
  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -n "$candidate" && -x "$candidate" ]]; then
      if "$candidate" -c 'from PIL import Image' >/dev/null 2>&1; then
        printf '%s\n' "$candidate"
        return 0
      fi
    fi
  done

  printf '%s\n' "$(command -v python3)"
}

emit_runtime_build_fingerprint() {
  local dll_path="$ROOT/.godot/mono/temp/bin/Debug/Physical Light and Camera Units.dll"
  if [[ ! -f "$dll_path" ]]; then
    return 0
  fi

  local git_short="nogit"
  if git -C "$ROOT" rev-parse --short=12 HEAD >/dev/null 2>&1; then
    git_short="$(git -C "$ROOT" rev-parse --short=12 HEAD)"
  fi

  local dll_write_utc
  dll_write_utc="$(python3 - <<'PY' "$dll_path"
from datetime import datetime, timezone
from pathlib import Path
import sys

path = Path(sys.argv[1])
dt = datetime.fromtimestamp(path.stat().st_mtime, tz=timezone.utc)
print(dt.strftime("%Y%m%dT%H%M%SZ"))
PY
)"
  local dll_sha
  dll_sha="$(sha256sum "$dll_path" | awk '{print substr($1,1,16)}')"
  export XPRIMERAY_BUILD_GIT_SHORT="$git_short"
  export XPRIMERAY_BUILD_FINGERPRINT="fixture005_runtime_fingerprint_v1_git_${git_short}_utc_${dll_write_utc}_sha_${dll_sha}"
  echo "Runtime fingerprint: $XPRIMERAY_BUILD_FINGERPRINT"
}

REPORT_PYTHON_BIN="$(resolve_report_python)"
FIXTURE_ID="fixture_009_wormhole_witness_throat"
SCENE_PATH="res://test-overspace-wormhole-witness-throat-fixture.tscn"
LAUNCHER_TOKEN="run_fixture_009_wormhole_witness_throat"
SETTLE_FRAMES="${FIXTURE_009_THROAT_SETTLE_FRAMES:-12}"
MIN_RH_STEP="${FIXTURE_009_THROAT_MIN_RH_STEP:-1}"
MIN_PROCESSED_ROWS="${FIXTURE_009_THROAT_MIN_PROCESSED_ROWS:-270}"
CAPTURE_FILM_OPACITY="${FIXTURE_009_THROAT_CAPTURE_FILM_OPACITY:-1.0}"
COMPARE_GRID="${FIXTURE_009_THROAT_COMPARE_GRID:-0}"
COMPARE_CROSSHAIR="${FIXTURE_009_THROAT_COMPARE_CROSSHAIR:-0}"
VISUAL_MODE="${FIXTURE_009_THROAT_VISUAL_MODE:-geometry_context}"
ANALYSIS_CAPTURE_MODE="${FIXTURE_009_THROAT_ANALYSIS_CAPTURE_MODE:-resolved_film}"
SOURCE_HIGHLIGHT="${FIXTURE_009_THROAT_SOURCE_HIGHLIGHT:-0}"
REQUESTED_STEP_LENGTH="${FIXTURE_009_THROAT_STEP_LENGTH:-0.05}"
REQUESTED_MIN_STEP_LENGTH="${FIXTURE_009_THROAT_MIN_STEP_LENGTH:-0.02}"
REQUESTED_STEPS_PER_RAY="${FIXTURE_009_THROAT_STEPS_PER_RAY:-640}"
REQUESTED_TURN_THRESHOLD="${FIXTURE_009_THROAT_TURN_THRESHOLD:-2.4}"
REQUESTED_ERROR_TOLERANCE="${FIXTURE_009_THROAT_ERROR_TOLERANCE:-0.010}"

TIMESTAMP="$(date +"%Y-%m-%dT%H-%M-%S")"
RUN_DIR="$ROOT/output/fixture_runs/$FIXTURE_ID/$TIMESTAMP"
mkdir -p "$RUN_DIR"

echo "Building .NET project..."
dotnet build 'Physical Light and Camera Units.csproj' -c Debug -v minimal
emit_runtime_build_fingerprint

CAPTURE_PATH="$RUN_DIR/capture.png"
DEBUG_CAPTURE_PATH="$RUN_DIR/debug_capture.png"
LOG_PATH="$RUN_DIR/run.log"

CAPTURE_RES_PATH="res://output/fixture_runs/$FIXTURE_ID/$TIMESTAMP/capture.png"
DEBUG_CAPTURE_RES_PATH="res://output/fixture_runs/$FIXTURE_ID/$TIMESTAMP/debug_capture.png"

EXTRA_RENDER_ARGS=()
REPORT_ARGS=()

if [[ -n "$REQUESTED_STEP_LENGTH" ]]; then
  EXTRA_RENDER_ARGS+=("--grin-basic-step-length=$REQUESTED_STEP_LENGTH")
  REPORT_ARGS+=(--requested-step-length "$REQUESTED_STEP_LENGTH")
fi

if [[ -n "$REQUESTED_MIN_STEP_LENGTH" ]]; then
  EXTRA_RENDER_ARGS+=("--grin-basic-min-step-length=$REQUESTED_MIN_STEP_LENGTH")
  REPORT_ARGS+=(--requested-min-step-length "$REQUESTED_MIN_STEP_LENGTH")
fi

if [[ -n "$REQUESTED_STEPS_PER_RAY" ]]; then
  EXTRA_RENDER_ARGS+=("--grin-basic-steps-per-ray=$REQUESTED_STEPS_PER_RAY")
  REPORT_ARGS+=(--requested-steps-per-ray "$REQUESTED_STEPS_PER_RAY")
fi

if [[ -n "$REQUESTED_TURN_THRESHOLD" ]]; then
  EXTRA_RENDER_ARGS+=("--grin-basic-turn-threshold=$REQUESTED_TURN_THRESHOLD")
  REPORT_ARGS+=(--requested-turn-threshold "$REQUESTED_TURN_THRESHOLD")
fi

if [[ -n "$REQUESTED_ERROR_TOLERANCE" ]]; then
  EXTRA_RENDER_ARGS+=("--grin-basic-error-tolerance=$REQUESTED_ERROR_TOLERANCE")
  REPORT_ARGS+=(--requested-error-tolerance "$REQUESTED_ERROR_TOLERANCE")
fi

CMD=(
  "bash"
  "$ROOT/scripts/godot_local.sh"
  "--path" "."
  "--scene" "$SCENE_PATH"
  "--"
  "--grin-basic-capture=$CAPTURE_RES_PATH"
  "--grin-basic-debug-capture=$DEBUG_CAPTURE_RES_PATH"
  "--grin-basic-analysis-capture-mode=$ANALYSIS_CAPTURE_MODE"
  "--grin-basic-settle-frames=$SETTLE_FRAMES"
  "--grin-basic-min-rh-step=$MIN_RH_STEP"
  "--grin-basic-min-processed-rows=$MIN_PROCESSED_ROWS"
  "--grin-basic-capture-film-opacity=$CAPTURE_FILM_OPACITY"
  "--grin-basic-compare-grid=$COMPARE_GRID"
  "--grin-basic-compare-crosshair=$COMPARE_CROSSHAIR"
  "--grin-basic-visual-mode=$VISUAL_MODE"
  "--grin-basic-source-highlight=$SOURCE_HIGHLIGHT"
  "--grin-basic-exit-after-capture=1"
)
CMD+=("${EXTRA_RENDER_ARGS[@]}")

export APPDATA="$ROOT/.appdata"
export LOCALAPPDATA="$ROOT/.localappdata"
export USERPROFILE="$ROOT/.userprofile"
export GRIN_BASIC_CAPTURE_PATH="$CAPTURE_RES_PATH"
export GRIN_BASIC_DEBUG_CAPTURE_PATH="$DEBUG_CAPTURE_RES_PATH"
export GRIN_BASIC_ANALYSIS_CAPTURE_MODE="$ANALYSIS_CAPTURE_MODE"
export GRIN_BASIC_SETTLE_FRAMES="$SETTLE_FRAMES"
export GRIN_BASIC_MIN_RH_STEP="$MIN_RH_STEP"
export GRIN_BASIC_MIN_PROCESSED_ROWS="$MIN_PROCESSED_ROWS"
export GRIN_BASIC_CAPTURE_FILM_OPACITY="$CAPTURE_FILM_OPACITY"
export GRIN_BASIC_COMPARE_GRID="$COMPARE_GRID"
export GRIN_BASIC_COMPARE_CROSSHAIR="$COMPARE_CROSSHAIR"
export GRIN_BASIC_VISUAL_MODE="$VISUAL_MODE"
export GRIN_BASIC_SOURCE_HIGHLIGHT="$SOURCE_HIGHLIGHT"
export GRIN_BASIC_EXIT_AFTER_CAPTURE="1"

RUN_START="$(python3 - <<'PY'
import time
print(time.time())
PY
)"

set +e
"${CMD[@]}" >"$LOG_PATH" 2>&1
GODOT_EXIT_CODE=$?
set -e

RUN_END="$(python3 - <<'PY'
import time
print(time.time())
PY
)"

RUNTIME_SECONDS="$(python3 - <<'PY' "$RUN_START" "$RUN_END"
import sys
start = float(sys.argv[1])
end = float(sys.argv[2])
print(f"{max(0.0, end - start):0.6f}")
PY
)"

"$REPORT_PYTHON_BIN" "$ROOT/tools/fixture_005_report.py" \
  --fixture-id "$FIXTURE_ID" \
  --timestamp "$TIMESTAMP" \
  --scene "$SCENE_PATH" \
  --launcher "$LAUNCHER_TOKEN" \
  --run-dir "$RUN_DIR" \
  --log-path "$LOG_PATH" \
  --capture-path "$CAPTURE_PATH" \
  --runtime-seconds "$RUNTIME_SECONDS" \
  --godot-exit-code "$GODOT_EXIT_CODE" \
  --settle-frames "$SETTLE_FRAMES" \
  --min-rh-step "$MIN_RH_STEP" \
  --min-processed-rows "$MIN_PROCESSED_ROWS" \
  --capture-film-opacity "$CAPTURE_FILM_OPACITY" \
  --compare-grid "$COMPARE_GRID" \
  --compare-crosshair "$COMPARE_CROSSHAIR" \
  "${REPORT_ARGS[@]}"

echo "Fixture run saved to: $RUN_DIR"
exit "$GODOT_EXIT_CODE"
