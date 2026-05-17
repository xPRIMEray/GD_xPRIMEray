#!/usr/bin/env bash
set -euo pipefail
# GPU runtime verification helper.
# Run standalone to audit renderer, environment, and Vulkan state:
#   bash scripts/check_gpu_runtime.sh

echo "=== xPRIMEray GPU Runtime Check ==="
echo ""

echo "--- Environment ---"
echo "GALLIUM_DRIVER=${GALLIUM_DRIVER:-<unset>}"
echo "LIBGL_ALWAYS_SOFTWARE=${LIBGL_ALWAYS_SOFTWARE:-<unset>}"
echo "DISPLAY=${DISPLAY:-<unset>}"
echo ""

if ! command -v glxinfo >/dev/null 2>&1; then
    echo "glxinfo not found — install mesa-utils to enable renderer checks:"
    echo "  sudo apt-get install mesa-utils"
    echo ""
else
    echo "--- Current renderer (inherits shell env) ---"
    glxinfo -B 2>/dev/null \
      | grep -E '^\s+(Device:|Accelerated:|Video memory:)' \
      | sed 's/^\s*//'
    echo ""

    echo "--- D3D12 renderer (GALLIUM_DRIVER=d3d12 forced) ---"
    GALLIUM_DRIVER=d3d12 glxinfo -B 2>/dev/null \
      | grep -E '^\s+(Device:|Accelerated:|Video memory:)' \
      | sed 's/^\s*//'
    echo ""
fi

if command -v vulkaninfo >/dev/null 2>&1; then
    echo "--- Vulkan summary ---"
    vulkaninfo --summary 2>/dev/null | head -15 || echo "(vulkaninfo query failed)"
    echo ""
else
    echo "--- Vulkan: vulkaninfo not available ---"
    echo ""
fi

echo "=== End GPU Runtime Check ==="
