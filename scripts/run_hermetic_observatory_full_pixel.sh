#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
source scripts/use_gpu_runtime.sh

python3 tools/hermetic_observatory_observe.py "$@"
