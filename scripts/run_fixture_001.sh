#!/usr/bin/env bash
set -euo pipefail

# Fixture 001 is a non-destructive baseline harness:
# it launches a fixed scene, captures one rendered artifact, and writes logs/metrics.

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

if [ -f ".env.local" ]; then
  # shellcheck disable=SC1091
  source .env.local
fi

resolve_godot_exe() {
  if [[ -n "${GODOT_EXE:-}" ]]; then
    if [[ -x "$GODOT_EXE" ]]; then
      printf '%s\n' "$GODOT_EXE"
      return 0
    fi
    echo "GODOT_EXE is set but not executable: $GODOT_EXE" >&2
    return 1
  fi

  if command -v godot4 >/dev/null 2>&1; then
    command -v godot4
    return 0
  fi

  if command -v godot >/dev/null 2>&1; then
    command -v godot
    return 0
  fi

  echo "No Godot executable found. Set GODOT_EXE or install godot4/godot on PATH." >&2
  return 1
}

GODOT_BIN="$(resolve_godot_exe)"
export GODOT_EXE="$GODOT_BIN"

FIXTURE_ID="fixture_001"
SCENE_PATH="res://test-grin-basic-visual-minimal.tscn"
LAUNCHER_TOKEN="run_fixture_001"
SETTLE_FRAMES="${FIXTURE_001_SETTLE_FRAMES:-12}"
MIN_RH_STEP="${FIXTURE_001_MIN_RH_STEP:-20}"
MIN_PROCESSED_ROWS="${FIXTURE_001_MIN_PROCESSED_ROWS:-64}"
CAPTURE_FILM_OPACITY="${FIXTURE_001_CAPTURE_FILM_OPACITY:-1.0}"
COMPARE_GRID="${FIXTURE_001_COMPARE_GRID:-1}"
COMPARE_CROSSHAIR="${FIXTURE_001_COMPARE_CROSSHAIR:-1}"

TIMESTAMP="$(date +"%Y-%m-%dT%H-%M-%S")"
RUN_DIR="$ROOT/output/fixture_runs/$FIXTURE_ID/$TIMESTAMP"
mkdir -p "$RUN_DIR"

CAPTURE_PATH="$RUN_DIR/capture.png"
LOG_PATH="$RUN_DIR/run.log"

CMD=(
  "$GODOT_BIN"
  "--path" "."
  "--scene" "$SCENE_PATH"
  "--"
  "--grin-basic-capture=$CAPTURE_PATH"
  "--grin-basic-settle-frames=$SETTLE_FRAMES"
  "--grin-basic-min-rh-step=$MIN_RH_STEP"
  "--grin-basic-min-processed-rows=$MIN_PROCESSED_ROWS"
  "--grin-basic-capture-film-opacity=$CAPTURE_FILM_OPACITY"
  "--grin-basic-compare-grid=$COMPARE_GRID"
  "--grin-basic-compare-crosshair=$COMPARE_CROSSHAIR"
  "--grin-basic-exit-after-capture=1"
)

printf 'Command:\n'
printf '  %q' "${CMD[@]}"
printf '\n'

export XPRIMERAY_REQUESTED_LAUNCHER="$LAUNCHER_TOKEN"

START_TS="$(python3 -c 'import time; print(time.perf_counter())')"
set +e
"${CMD[@]}" 2>&1 | tee "$LOG_PATH"
GODOT_EXIT_CODE=${PIPESTATUS[0]}
set -e
END_TS="$(python3 -c 'import time; print(time.perf_counter())')"
RUNTIME_SECONDS="$(python3 -c "start=float('$START_TS'); end=float('$END_TS'); print(f'{end-start:.3f}')")"

python3 "$ROOT/tools/fixture_001_report.py" \
  --fixture-id "$FIXTURE_ID" \
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
  --compare-crosshair "$COMPARE_CROSSHAIR"

SUMMARY_PATH="$RUN_DIR/summary.txt"
printf 'Summary:\n'
cat "$SUMMARY_PATH"

if [[ "$GODOT_EXIT_CODE" -ne 0 ]]; then
  exit "$GODOT_EXIT_CODE"
fi

