#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
source scripts/use_gpu_runtime.sh

export OFFAXIS_OBSERVE_MIN_PROCESSED_ROWS="${OFFAXIS_OBSERVE_MIN_PROCESSED_ROWS:-270}"
export OFFAXIS_OBSERVE_EXPECTED_FULL_ROWS="${OFFAXIS_OBSERVE_EXPECTED_FULL_ROWS:-270}"
export OFFAXIS_OBSERVE_COMPARE_GRID="${OFFAXIS_OBSERVE_COMPARE_GRID:-1}"
export OFFAXIS_OBSERVE_COMPARE_CROSSHAIR="${OFFAXIS_OBSERVE_COMPARE_CROSSHAIR:-1}"

python3 tools/basic_visual_offaxis_observe.py "$@"
