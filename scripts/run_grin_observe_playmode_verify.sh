#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
source scripts/use_gpu_runtime.sh
python3 tools/grin_observe_playmode_verify.py "$@"
