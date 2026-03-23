#!/usr/bin/env bash
set -euo pipefail

# Fixture 004 is the first multi-attractor characterization harness:
# it preserves the Fixture 002/003 single centered source row and hardened
# reporting path while replacing the single field center with two controlled
# horizontal attractors to study transport under competing local basins.

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

resolve_ledger_python() {
  if [[ -n "${FIXTURE_004_LEDGER_PYTHON:-}" && -x "${FIXTURE_004_LEDGER_PYTHON}" ]]; then
    printf '%s\n' "${FIXTURE_004_LEDGER_PYTHON}"
    return 0
  fi

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

resolve_runtime_root() {
  if [[ -n "${FIXTURE_004_RUNTIME_ROOT:-}" ]]; then
    printf '%s\n' "${FIXTURE_004_RUNTIME_ROOT}"
    return 0
  fi

  local win_mirror="/mnt/c/godot/$(basename "$ROOT")"
  if [[ -d "$win_mirror" ]]; then
    printf '%s\n' "$win_mirror"
    return 0
  fi

  printf '%s\n' "$ROOT"
}

resolve_dotnet_exe() {
  if [[ -n "${FIXTURE_004_DOTNET_EXE:-}" ]]; then
    printf '%s\n' "${FIXTURE_004_DOTNET_EXE}"
    return 0
  fi

  local dotnet_path
  dotnet_path="$(cmd.exe /c where dotnet 2>/dev/null | tr -d '\r' | head -n 1)"
  if [[ -n "$dotnet_path" ]]; then
    printf '%s\n' "$dotnet_path"
    return 0
  fi

  echo "No Windows dotnet executable found for Fixture 004 runtime rebuild." >&2
  return 1
}

sync_runtime_root() {
  local runtime_root="$1"
  if [[ "$runtime_root" == "$ROOT" ]]; then
    return 0
  fi

  rsync -a --prune-empty-dirs \
    --include='*/' \
    --include='*.cs' \
    --include='*.csproj' \
    --include='*.sln' \
    --include='*.godot' \
    --include='*.tscn' \
    --include='*.gd' \
    --include='*.gdshader' \
    --include='*.json' \
    --include='*.cfg' \
    --include='*.uid' \
    --exclude='*' \
    "$ROOT/" "$runtime_root/"
}

cleanup_stale_fixture_processes() {
  local runtime_root_win="$1"
  local scene_token="res://test-grin-basic-visual-linear-dual-attractor-minimal.tscn"
  powershell.exe -NoProfile -Command "
    \$runtimeRoot = '$runtime_root_win';
    \$sceneToken = '$scene_token';
    \$procs = Get-CimInstance Win32_Process | Where-Object {
      \$_.Name -like 'Godot*' -and
      \$_.CommandLine -and
      \$_.CommandLine.Contains(\$sceneToken) -and
      \$_.CommandLine.Contains(\$runtimeRoot)
    };
    foreach (\$proc in \$procs) {
      try { Stop-Process -Id \$proc.ProcessId -Force -ErrorAction Stop } catch {}
    }
    Write-Output ('fixture_process_cleanup count=' + @(\$procs).Count);
  " | tr -d '\r'
}

build_runtime_assembly() {
  local runtime_root="$1"
  local dotnet_exe="$2"
  local csproj_win
  csproj_win="$(wslpath -w "$runtime_root/Physical Light and Camera Units.csproj")"
  echo "Runtime project root: $runtime_root"
  echo "Runtime assembly target: $runtime_root/.godot/mono/temp/bin/Debug/Physical Light and Camera Units.dll"
  powershell.exe -NoProfile -Command "\$output = & '$dotnet_exe' build '$csproj_win' -c Debug --no-incremental --nologo --verbosity minimal | Out-String -Width 240; Write-Output \$output; exit \$LASTEXITCODE" | tr -d '\r'
}

emit_runtime_build_fingerprint() {
  local runtime_root="$1"
  local dll_path="$runtime_root/.godot/mono/temp/bin/Debug/Physical Light and Camera Units.dll"
  if [[ ! -f "$dll_path" ]]; then
    echo "runtime assembly missing after build: $dll_path" >&2
    return 1
  fi

  local git_short="nogit"
  if git -C "$ROOT" rev-parse --short=12 HEAD >/dev/null 2>&1; then
    git_short="$(git -C "$ROOT" rev-parse --short=12 HEAD)"
  fi

  local dll_write_utc
  dll_write_utc="$(powershell.exe -NoProfile -Command "(Get-Item '$(wslpath -w "$dll_path")').LastWriteTimeUtc.ToString('yyyyMMddTHHmmssZ')" | tr -d '\r')"
  local dll_sha
  dll_sha="$(sha256sum "$dll_path" | awk '{print substr($1,1,16)}')"
  export XPRIMERAY_BUILD_GIT_SHORT="$git_short"
  export XPRIMERAY_BUILD_FINGERPRINT="fixture004_runtime_fingerprint_v1_git_${git_short}_utc_${dll_write_utc}_sha_${dll_sha}"
  echo "Runtime fingerprint: $XPRIMERAY_BUILD_FINGERPRINT"
}

GODOT_BIN="$(resolve_godot_exe)"
LEDGER_PYTHON_BIN="$(resolve_ledger_python)"
RUNTIME_ROOT="$(resolve_runtime_root)"
RUNTIME_ROOT_WIN="$(wslpath -w "$RUNTIME_ROOT")"
DOTNET_EXE="$(resolve_dotnet_exe)"
export GODOT_EXE="$GODOT_BIN"

FIXTURE_ID="fixture_004"
SCENE_PATH="res://test-grin-basic-visual-linear-dual-attractor-minimal.tscn"
LAUNCHER_TOKEN="run_fixture_004"
SETTLE_FRAMES="${FIXTURE_004_SETTLE_FRAMES:-12}"
MIN_RH_STEP="${FIXTURE_004_MIN_RH_STEP:-20}"
MIN_PROCESSED_ROWS="${FIXTURE_004_MIN_PROCESSED_ROWS:-64}"
CAPTURE_FILM_OPACITY="${FIXTURE_004_CAPTURE_FILM_OPACITY:-1.0}"
COMPARE_GRID="${FIXTURE_004_COMPARE_GRID:-1}"
COMPARE_CROSSHAIR="${FIXTURE_004_COMPARE_CROSSHAIR:-1}"
VISUAL_MODE="${FIXTURE_004_VISUAL_MODE:-diagnostic_flat}"
ANALYSIS_CAPTURE_MODE="${FIXTURE_004_ANALYSIS_CAPTURE_MODE:-categorical_final}"
SOURCE_HIGHLIGHT="${FIXTURE_004_SOURCE_HIGHLIGHT:-1}"
REQUESTED_TRANSPORT_MODEL="${FIXTURE_004_TRANSPORT_MODEL:-}"
REQUESTED_STEP_LENGTH="${FIXTURE_004_STEP_LENGTH:-0.040}"
REQUESTED_MIN_STEP_LENGTH="${FIXTURE_004_MIN_STEP_LENGTH:-}"
REQUESTED_STEPS_PER_RAY="${FIXTURE_004_STEPS_PER_RAY:-}"
REQUESTED_TURN_THRESHOLD="${FIXTURE_004_TURN_THRESHOLD:-2.4}"
REQUESTED_ERROR_TOLERANCE="${FIXTURE_004_ERROR_TOLERANCE:-0.010}"
BASELINE_CAPTURE="${FIXTURE_004_BASELINE_CAPTURE:-}"

TIMESTAMP="$(date +"%Y-%m-%dT%H-%M-%S")"
RUN_DIR="$ROOT/output/fixture_runs/$FIXTURE_ID/$TIMESTAMP"
mkdir -p "$RUN_DIR"

sync_runtime_root "$RUNTIME_ROOT"
cleanup_stale_fixture_processes "$RUNTIME_ROOT_WIN"
build_runtime_assembly "$RUNTIME_ROOT" "$DOTNET_EXE"
emit_runtime_build_fingerprint "$RUNTIME_ROOT"

CAPTURE_PATH="$RUN_DIR/capture.png"
CAPTURE_PATH_WIN="$(wslpath -w "$CAPTURE_PATH")"
ANALYSIS_CAPTURE_ALIAS_PATH="$RUN_DIR/analysis_capture.png"
DEBUG_CAPTURE_PATH="$RUN_DIR/debug_capture.png"
DEBUG_CAPTURE_PATH_WIN="$(wslpath -w "$DEBUG_CAPTURE_PATH")"
LOG_PATH="$RUN_DIR/run.log"

EXTRA_RENDER_ARGS=()
REPORT_ARGS=()
LEDGER_ARGS=()

if [[ -n "$REQUESTED_TRANSPORT_MODEL" ]]; then
  EXTRA_RENDER_ARGS+=("--transport-model=$REQUESTED_TRANSPORT_MODEL")
  REPORT_ARGS+=(--requested-transport-model "$REQUESTED_TRANSPORT_MODEL")
fi

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

if [[ -n "$BASELINE_CAPTURE" ]]; then
  LEDGER_ARGS+=(--baseline-path "$BASELINE_CAPTURE")
fi

CMD=(
  "$GODOT_BIN"
  "--path" "$RUNTIME_ROOT_WIN"
  "--scene" "$SCENE_PATH"
  "--"
  "--grin-basic-capture=$CAPTURE_PATH_WIN"
  "--grin-basic-debug-capture=$DEBUG_CAPTURE_PATH_WIN"
  "--grin-basic-settle-frames=$SETTLE_FRAMES"
  "--grin-basic-min-rh-step=$MIN_RH_STEP"
  "--grin-basic-min-processed-rows=$MIN_PROCESSED_ROWS"
  "--grin-basic-capture-film-opacity=$CAPTURE_FILM_OPACITY"
  "--grin-basic-compare-grid=$COMPARE_GRID"
  "--grin-basic-compare-crosshair=$COMPARE_CROSSHAIR"
  "--grin-basic-visual-mode=$VISUAL_MODE"
  "--grin-basic-analysis-capture-mode=$ANALYSIS_CAPTURE_MODE"
  "--grin-basic-source-highlight=$SOURCE_HIGHLIGHT"
  "--grin-basic-exit-after-capture=1"
  "--grin-basic-build-fingerprint=$XPRIMERAY_BUILD_FINGERPRINT"
  "--grin-basic-build-git-short=$XPRIMERAY_BUILD_GIT_SHORT"
  "${EXTRA_RENDER_ARGS[@]}"
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

if [[ -f "$CAPTURE_PATH" ]]; then
  cp -f "$CAPTURE_PATH" "$ANALYSIS_CAPTURE_ALIAS_PATH"
fi

"$LEDGER_PYTHON_BIN" "$ROOT/tools/fixture_004_report.py" \
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
  --radial-bin-count "${FIXTURE_004_RADIAL_BIN_COUNT:-8}" \
  "${REPORT_ARGS[@]}"

"$LEDGER_PYTHON_BIN" "$ROOT/tools/characterization_ledger/ledger_writer.py" \
  --summary-json "$RUN_DIR/summary.json" \
  --metrics-json "$RUN_DIR/metrics.json" \
  --params-json "$RUN_DIR/params.json" \
  --capture-path "$CAPTURE_PATH" \
  --fixture-id "$FIXTURE_ID" \
  --timestamp "$TIMESTAMP" \
  "${LEDGER_ARGS[@]}"

SUMMARY_PATH="$RUN_DIR/summary.txt"
printf 'Summary:\n'
cat "$SUMMARY_PATH"

if [[ "$GODOT_EXIT_CODE" -ne 0 ]]; then
  exit "$GODOT_EXIT_CODE"
fi
