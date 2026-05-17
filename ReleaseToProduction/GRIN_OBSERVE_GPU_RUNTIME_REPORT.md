# GRIN Observe GPU Runtime Report

**Date:** 2026-05-17  
**Environment:** WSL2 / Ubuntu 24.04 / AMD Radeon (integrated)  
**Status:** D3D12 hardware acceleration confirmed active

---

## Root Cause — llvmpipe Default

WSL2's Mesa installation defaults to the `llvmpipe` software rasterizer even when a
GPU is physically present and accessible via the D3D12 translation layer (DXVK/WSLg bridge).
Without an explicit driver selection, `glxinfo -B` reports:

```
Device: llvmpipe (LLVM 20.1.2, 256 bits)
Accelerated: no
```

This affects Godot, OBS, and any other OpenGL-dependent tool launched from WSL.
The llvmpipe path is correct for headless or no-GPU environments but produces
significantly slower render throughput in GPU-capable WSL2 setups.

---

## Breakthrough

The following single-variable override routes Mesa to the D3D12 Gallium backend,
using the hardware GPU through the Windows D3D12 API:

```bash
GALLIUM_DRIVER=d3d12 glxinfo -B
```

Expected output (confirmed):

```
Device: D3D12 (AMD Radeon(TM) Graphics)
Accelerated: yes
```

No driver installation, recompilation, or system-level change is required.
The D3D12 backend is already present in the Mesa installation; it only needs
to be selected.

---

## Runtime Strategy

All observatory entry-point scripts now activate D3D12 at the top of their execution
before spawning Godot, OBS, or Python capture tools:

**Central bootstrap:** `scripts/use_gpu_runtime.sh`  
Source this in any script or interactive shell to activate GPU acceleration:

```bash
source scripts/use_gpu_runtime.sh
```

Output on activation:
```
[gpu-runtime] GALLIUM_DRIVER=d3d12
[gpu-runtime] Device: D3D12 (AMD Radeon(TM) Graphics)
[gpu-runtime] Accelerated: yes
```

**Scripts updated to source the bootstrap:**

| Script | Purpose |
| --- | --- |
| `scripts/godot_local.sh` | Default GPU env for all direct Godot invocations |
| `scripts/run_grin_observe_playmode_verify.sh` | Play-mode verifier → Python → Godot chain |
| `scripts/run_grin_observe_v0_pre_full_pixel.sh` | Full-pixel release gate |
| `scripts/launch_grin_observe_v0_pre_take001.sh` | Operator Take001 launch |
| `scripts/record_grin_observe_v0_pre_take001.sh` | Automated capture pipeline |

**godot_local.sh** uses a default-only pattern: `GALLIUM_DRIVER="${GALLIUM_DRIVER:-d3d12}"`.
Any calling script that sets its own `GALLIUM_DRIVER` before invoking `godot_local.sh`
takes priority. This is fully reversible.

---

## Verification

Check current GPU state at any time:

```bash
bash scripts/check_gpu_runtime.sh
```

This reports:
- Current environment variables
- Active renderer (inheriting shell env)
- D3D12-forced renderer (always checked with `GALLIUM_DRIVER=d3d12`)
- Vulkan summary if `vulkaninfo` is available

---

## OBS with GPU Acceleration

```bash
bash scripts/launch_obs_gpu.sh
```

This sources `use_gpu_runtime.sh` then `exec obs`. OBS is confirmed present at
`/usr/bin/obs` but has not yet been tested under GPU-accelerated rendering.
Interactive OBS launch requires a desktop session; automated capture still relies
on the fallback frame-assembly pipeline until OBS can be tested.

---

## Performance Notes

Full-pixel render automation runtime under D3D12 vs llvmpipe has not been measured
at time of writing. The release gate (`run_grin_observe_v0_pre_full_pixel.sh`) is
now configured to use D3D12; any improvement in render throughput will be observable
in output log timestamps.

No claims are made about specific performance improvement factors. Acceleration is
confirmed present; throughput measurement is deferred to the next full-pixel run.

---

## Limitations

- **Vulkan:** D3D12 Gallium is confirmed. Vulkan via `vulkaninfo` has not been tested
  and cannot be assumed to work without separate verification.
- **OBS GPU mode:** OBS installation is confirmed; GPU-accelerated OBS recording is
  not yet verified in this WSL environment.
- **LIBGL_ALWAYS_SOFTWARE:** Unsetting this variable is included in `use_gpu_runtime.sh`
  as a safety measure. If this variable is intentionally set in a calling environment,
  unsetting it in the bootstrap may change rendering behavior — review before use in
  non-observatory contexts.
- **Driver override is env-only:** `GALLIUM_DRIVER=d3d12` affects only processes that
  inherit the variable. Processes spawned without inheriting this env (e.g., launched
  from a desktop launcher or a fresh shell) still use the system default.
