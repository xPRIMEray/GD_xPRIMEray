#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd -P)"
VENV="$ROOT/output/v0.0-pre/video/.capture-venv"
PY="$VENV/bin/python"

cd "$ROOT"
mkdir -p output/v0.0-pre/video

if [ ! -x "$PY" ]; then
  echo "[fallback-video] creating capture virtualenv at $VENV"
  python3 -m venv "$VENV"
fi

"$PY" - <<'PY'
import importlib.util
import subprocess
import sys

missing = [pkg for pkg in ("PIL", "imageio_ffmpeg") if importlib.util.find_spec(pkg) is None]
if missing:
    subprocess.check_call([sys.executable, "-m", "pip", "install", "--upgrade", "pip", "pillow", "imageio", "imageio-ffmpeg"])
PY

echo "[fallback-video] refreshing play-mode verification"
bash scripts/run_grin_observe_playmode_verify.sh

echo "[fallback-video] assembling MP4 from verified artifacts"
"$PY" tools/build_grin_observe_demo_from_frames.py "$@"
