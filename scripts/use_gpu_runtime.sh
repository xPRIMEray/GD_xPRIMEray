#!/usr/bin/env bash
# GPU runtime bootstrap — source this to activate D3D12 Gallium hardware acceleration
# in the current shell before launching Godot, OBS, or any OpenGL tool:
#
#   source scripts/use_gpu_runtime.sh          (from project root)
#   source "$(dirname "$0")/use_gpu_runtime.sh" (from another script in scripts/)
#
# GALLIUM_DRIVER=d3d12 routes Mesa to the WSL D3D12 translation layer instead of
# the llvmpipe software rasterizer. Confirmed working on AMD Radeon via WSLg.

export MESA_D3D12_DEFAULT_ADAPTER_NAME="${MESA_D3D12_DEFAULT_ADAPTER_NAME:-AMD}"

export GALLIUM_DRIVER=d3d12
unset LIBGL_ALWAYS_SOFTWARE 2>/dev/null || true

echo "[gpu-runtime] GALLIUM_DRIVER=d3d12"
if command -v glxinfo >/dev/null 2>&1; then
    glxinfo -B 2>/dev/null \
      | grep -E '^\s+(Device:|Accelerated:)' \
      | sed 's/^\s*/[gpu-runtime] /'
fi
