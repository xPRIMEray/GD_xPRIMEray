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
FIXTURE_ID="fixture_011_wormhole_checkpoint_sequence"
SCENE_PATH="res://test-overspace-wormhole-checkpoint-sequence-fixture.tscn"
LAUNCHER_TOKEN="run_fixture_011_wormhole_checkpoint_sequence"

TIMESTAMP="$(date +"%Y-%m-%dT%H-%M-%S")"
RUN_DIR="$ROOT/output/fixture_runs/$FIXTURE_ID/$TIMESTAMP"
mkdir -p "$RUN_DIR"

echo "Building .NET project..."
dotnet build 'Physical Light and Camera Units.csproj' -c Debug -v minimal
emit_runtime_build_fingerprint

LOG_PATH="$RUN_DIR/run.log"

CMD=(
  "bash"
  "$ROOT/scripts/godot_local.sh"
  "--path" "."
  "--scene" "$SCENE_PATH"
)

export APPDATA="$ROOT/.appdata"
export LOCALAPPDATA="$ROOT/.localappdata"
export USERPROFILE="$ROOT/.userprofile"
export WORMHOLE_CHECKPOINT_RUN_DIR="$RUN_DIR"

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

echo "Fixture run saved to: $RUN_DIR"
exit "$GODOT_EXIT_CODE"
