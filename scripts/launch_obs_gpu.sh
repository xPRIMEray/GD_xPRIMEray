#!/usr/bin/env bash
set -euo pipefail
# Launch OBS with D3D12 Gallium hardware acceleration active.
# Usage: bash scripts/launch_obs_gpu.sh [obs-args...]

ROOT="$(cd "$(dirname "$0")/.." && pwd -P)"
cd "$ROOT"

source scripts/use_gpu_runtime.sh

if ! command -v obs >/dev/null 2>&1; then
    echo "[obs-gpu] ERROR: obs not found in PATH" >&2
    echo "[obs-gpu] Install OBS: sudo apt-get install obs-studio" >&2
    exit 1
fi

echo "[obs-gpu] launching OBS"
exec obs "$@"
