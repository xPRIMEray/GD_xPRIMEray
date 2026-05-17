#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd -P)"

log() {
  printf '[capture-setup] %s\n' "$*"
}

have() {
  command -v "$1" >/dev/null 2>&1
}

run_install() {
  if [ "$#" -eq 0 ]; then
    return 0
  fi

  if have apt-get; then
    log "package manager: apt-get"
    local sudo_cmd=()
    if [ "$(id -u)" -ne 0 ]; then
      if have sudo; then
        if sudo -n true 2>/dev/null; then
          sudo_cmd=(sudo)
        else
          log "sudo is present but requires an interactive password; cannot install apt packages automatically"
          return 1
        fi
      else
        log "sudo is unavailable; cannot install packages automatically"
        return 1
      fi
    fi
    log "updating package index"
    "${sudo_cmd[@]}" apt-get update
    log "installing: $*"
    "${sudo_cmd[@]}" apt-get install -y --no-install-recommends "$@"
    return 0
  fi

  if have dnf; then
    log "package manager: dnf"
    local sudo_cmd=()
    if [ "$(id -u)" -ne 0 ]; then
      if have sudo; then
        if sudo -n true 2>/dev/null; then
          sudo_cmd=(sudo)
        else
          log "sudo is present but requires an interactive password; cannot install dnf packages automatically"
          return 1
        fi
      else
        log "sudo is unavailable; cannot install packages automatically"
        return 1
      fi
    fi
    "${sudo_cmd[@]}" dnf install -y "$@"
    return 0
  fi

  if have pacman; then
    log "package manager: pacman"
    local sudo_cmd=()
    if [ "$(id -u)" -ne 0 ]; then
      if have sudo; then
        if sudo -n true 2>/dev/null; then
          sudo_cmd=(sudo)
        else
          log "sudo is present but requires an interactive password; cannot install pacman packages automatically"
          return 1
        fi
      else
        log "sudo is unavailable; cannot install packages automatically"
        return 1
      fi
    fi
    "${sudo_cmd[@]}" pacman -Sy --needed --noconfirm "$@"
    return 0
  fi

  log "no supported package manager found"
  return 1
}

tool_version() {
  local tool="$1"
  shift || true
  if ! have "$tool"; then
    printf '%-24s missing\n' "$tool"
    return 0
  fi
  printf '%-24s ' "$tool"
  "$tool" "$@" 2>&1 | head -n 1 || true
}

main() {
  cd "$ROOT"
  mkdir -p output/v0.0-pre/video

  log "project: $ROOT"
  log "kernel: $(uname -r)"
  log "display: DISPLAY=${DISPLAY:-unset} WAYLAND_DISPLAY=${WAYLAND_DISPLAY:-unset} PULSE_SERVER=${PULSE_SERVER:-unset}"

  local packages=()
  for tool_pkg in \
    "ffmpeg:ffmpeg" \
    "obs:obs-studio" \
    "xdotool:xdotool" \
    "wmctrl:wmctrl" \
    "xvfb-run:xvfb" \
    "xwininfo:x11-utils" \
    "xdpyinfo:x11-utils" \
    "gst-launch-1.0:gstreamer1.0-tools" \
    "scrot:scrot" \
    "python3:python3" \
    "pip3:python3-pip"
  do
    tool="${tool_pkg%%:*}"
    pkg="${tool_pkg#*:}"
    if ! have "$tool"; then
      packages+=("$pkg")
    fi
  done

  if [ "${#packages[@]}" -gt 0 ]; then
    log "missing packages detected: ${packages[*]}"
    run_install "${packages[@]}" || log "automatic install did not complete; continue with available tools"
  else
    log "all baseline capture packages already available"
  fi

  log "tool versions"
  tool_version ffmpeg -version
  tool_version obs --version
  tool_version xdotool --version
  tool_version wmctrl -V
  tool_version xvfb-run --help
  tool_version xwininfo -version
  tool_version xdpyinfo -version
  tool_version gst-launch-1.0 --version
  tool_version scrot --version
  tool_version python3 --version
  tool_version pip3 --version

  if have obs; then
    log "OBS is installed. Test launch without recording: obs --help"
  else
    log "OBS is still unavailable. Use fallback frame assembly via scripts/build_grin_observe_demo_from_frames.sh"
  fi

  if have ffmpeg; then
    log "ffmpeg is installed. Fallback MP4 assembly is available."
  else
    log "system ffmpeg is unavailable. Preparing user-local Python fallback venv."
    local venv="$ROOT/output/v0.0-pre/video/.capture-venv"
    if [ ! -x "$venv/bin/python" ]; then
      python3 -m venv "$venv"
    fi
    "$venv/bin/python" - <<'PY'
import importlib.util
import subprocess
import sys

missing = [pkg for pkg in ("PIL", "imageio_ffmpeg") if importlib.util.find_spec(pkg) is None]
if missing:
    subprocess.check_call([sys.executable, "-m", "pip", "install", "--upgrade", "pip", "pillow", "imageio", "imageio-ffmpeg"])
import imageio_ffmpeg
print("[capture-setup] fallback ffmpeg:", imageio_ffmpeg.get_ffmpeg_exe())
PY
  fi

  log "next steps:"
  log "1. Run: bash scripts/run_grin_observe_playmode_verify.sh"
  log "2. Run: bash scripts/record_grin_observe_v0_pre_take001.sh"
  log "3. If live capture is blocked, run: bash scripts/build_grin_observe_demo_from_frames.sh"
}

main "$@"
