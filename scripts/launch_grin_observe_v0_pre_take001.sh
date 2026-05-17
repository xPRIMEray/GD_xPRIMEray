#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
source scripts/use_gpu_runtime.sh

echo "[take001] refreshing play-mode smoke verification"
bash scripts/run_grin_observe_playmode_verify.sh

echo "[take001] launch OBS separately, start MKV recording, then use this Godot window"
exec ./scripts/godot_local.sh --path . --scene res://test-straight-basic-visual-offaxis-observe.tscn
