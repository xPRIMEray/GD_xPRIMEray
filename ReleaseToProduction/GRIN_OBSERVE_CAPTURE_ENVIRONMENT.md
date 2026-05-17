# GRIN Observe Capture Environment

Generated for Phase 2 v0.0-pre capture automation.

## Host / WSL

- OS: Ubuntu 24.04.4 LTS (Noble Numbat)
- Kernel: `6.6.87.2-microsoft-standard-WSL2`
- Platform: WSL2
- Project: `/home/bb/code/godot_xPRIMEray`

## Display / Audio Availability

- `DISPLAY=:0`
- `WAYLAND_DISPLAY=wayland-0`
- `PULSE_SERVER=unix:/mnt/wslg/PulseServer`
- WSLg sockets were present:
  - `/tmp/.X11-unix/X0`
  - `/mnt/wslg/runtime-dir/wayland-0`
  - `/mnt/wslg/PulseServer`

Interpretation: WSLg is available and Godot visible playback can run, but capture tools must still be installed and tested independently.

## Tool Availability

System packages initially available:

- `python3`: available
- `pip3`: available
- `obs` / `obs-studio`: missing
- `ffmpeg`: missing
- `xdotool`: missing
- `wmctrl`: missing
- `xvfb-run` / `Xvfb`: missing
- `xwininfo` / `xdpyinfo`: missing
- `gst-launch-1.0`: missing
- `scrot`: missing

APT candidates exist for the missing tools, including `obs-studio`, `ffmpeg`, `xdotool`, `wmctrl`, `xvfb`, `x11-utils`, `gstreamer1.0-tools`, and `scrot`.

Automatic APT install is blocked in this shell because `sudo` requires an interactive password. The setup script detects this and continues without destructive changes.

## Installed Fallback Capture Stack

A project-local capture virtualenv was created at:

- `output/v0.0-pre/video/.capture-venv`

Installed Python packages:

- `pillow`
- `imageio`
- `imageio-ffmpeg`

Bundled ffmpeg provider:

- `output/v0.0-pre/video/.capture-venv/lib/python3.12/site-packages/imageio_ffmpeg/binaries/ffmpeg-linux-x86_64-v7.0.2`

This is sufficient for deterministic fallback MP4 assembly from verified PNG artifacts without system package installation.

## OBS Launch Status

- OBS is not installed in the current WSL environment.
- OBS launch could not be tested.
- OBS window capture is therefore not currently available from this shell.

Recommended OBS operator path:

1. Install OBS in an environment with interactive package privileges or use Windows-host OBS.
2. Run `bash scripts/run_grin_observe_playmode_verify.sh`.
3. Launch Godot with `./scripts/godot_local.sh --path . --scene res://test-straight-basic-visual-offaxis-observe.tscn`.
4. Capture the Godot window at 1920x1080, 30 FPS.
5. Keep full-pixel automation as a pre-tag release gate.

## Godot Window Capture

Godot visible playback works through WSLg, and the play-mode verifier passes when launched through the canonical scenes.

Live Godot window capture from inside WSL is not currently proven because the required capture utilities are missing:

- OBS missing
- system ffmpeg missing
- x11-utils missing
- xdotool/wmctrl missing

## ffmpeg Screen Capture

System `ffmpeg` is not installed, so direct `x11grab` capture from `DISPLAY=:0` was not attempted.

The fallback MP4 path uses the `imageio-ffmpeg` bundled binary to encode a video from generated frames. This verifies video payload generation, not live screen capture.

## Generated Payload

The one-command recording path was tested:

```bash
bash scripts/record_grin_observe_v0_pre_take001.sh
```

Because OBS/system ffmpeg were unavailable, it selected the fallback frame assembly path and produced:

- `output/v0.0-pre/video/xprimeray_grin_observe_v0_pre_take001_fallback.mp4`
- `output/v0.0-pre/video/TAKE001_MANIFEST.md`
- `output/v0.0-pre/video/take001_recording.log`

Capture method:

- `fallback frames via record script`

Resolution/FPS/codec:

- `1920x1080 @ 30 FPS, H.264 MP4`

## Blockers

- Noninteractive sudo prevents installing OBS/system ffmpeg from this shell.
- OBS is not currently available in WSL.
- Live screen/window capture is not proven in WSL.
- Full-pixel render automation remains too slow for this local machine and should be run on faster GPU hardware before tagging release.

## Recommended Path

Smallest robust path now:

```bash
bash scripts/setup_grin_observe_capture_tools.sh
bash scripts/record_grin_observe_v0_pre_take001.sh
```

This refreshes play-mode verification, selects the best available capture path, and always aims to produce a manifest.

For release-quality operator video:

1. Install OBS/system capture tools on a machine with interactive package privileges or use Windows-host OBS.
2. Run the same verifier and launch scripts.
3. Record the Godot window manually or via OBS automation.
4. Run full-pixel automation on faster GPU hardware before tagging:

```bash
bash scripts/run_grin_observe_v0_pre_full_pixel.sh
```

No generated captions or documentation claim exotic physics proof; the payload is framed as coherent transport instrumentation validation.
