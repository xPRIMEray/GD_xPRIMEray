#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd -P)"
VIDEO_DIR="$ROOT/output/v0.0-pre/video"
LOG="$VIDEO_DIR/take001_recording.log"
LIVE_CAPTURE_SECONDS="${LIVE_CAPTURE_SECONDS:-45}"
FPS="${FPS:-30}"
DISPLAY_SIZE="${DISPLAY_SIZE:-1920x1080}"
OUTPUT_MP4="$VIDEO_DIR/xprimeray_grin_observe_v0_pre_take001.mp4"

cd "$ROOT"
source scripts/use_gpu_runtime.sh
mkdir -p "$VIDEO_DIR"

log() {
  printf '[take001-record] %s\n' "$*" | tee -a "$LOG"
}

have() {
  command -v "$1" >/dev/null 2>&1
}

write_operator_manifest() {
  local method="$1"
  local status="$2"
  local video_path="$3"
  local limitation="$4"
  {
    printf '# TAKE001 Manifest\n\n'
    printf -- '- Timestamp UTC: `%s`\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    printf -- '- Status: `%s`\n' "$status"
    printf -- '- Capture method used: `%s`\n' "$method"
    printf -- '- Video artifact path: `%s`\n' "$video_path"
    printf -- '- Resolution/FPS/codec: `%s @ %s FPS, H.264 MP4 target`\n' "$DISPLAY_SIZE" "$FPS"
    printf -- '- Verification report path: `output/v0.0-pre/GRIN_OBSERVE_PLAYMODE_VERIFY.md`\n'
    printf '\n## Screenshot Artifacts\n\n'
    printf -- '- `output/v0.0-pre/straight_control_verify.png`\n'
    printf -- '- `output/v0.0-pre/curved_grin_verify.png`\n'
    printf -- '- `output/v0.0-pre/curved_grin_final_smoke.png`\n'
    printf '\n## Known Limitations\n\n'
    printf -- '- %s\n' "$limitation"
    printf -- '- Full-pixel release automation remains a pre-tag gate on faster GPU hardware.\n'
    printf -- '- Captions/docs intentionally avoid unsupported physics claims.\n'
  } > "$VIDEO_DIR/TAKE001_MANIFEST.md"
}

main() {
  : > "$LOG"
  log "refreshing play-mode verification"
  bash scripts/run_grin_observe_playmode_verify.sh | tee -a "$LOG"

  if have obs; then
    log "OBS detected; launching Godot for semi-automated operator capture"
    log "Start OBS recording to MKV/MP4, capture the Godot window, then press Enter here when finished."
    ./scripts/godot_local.sh --path . --scene res://test-straight-basic-visual-offaxis-observe.tscn &
    local godot_pid=$!
    read -r -p "[take001-record] Press Enter after OBS recording is complete..." _
    if kill -0 "$godot_pid" >/dev/null 2>&1; then
      kill "$godot_pid" || true
    fi
    log "If OBS produced a video, place it under output/v0.0-pre/video/"
    write_operator_manifest "OBS operator capture" "OPERATOR_ACTION_REQUIRED" "output/v0.0-pre/video/<operator-recording>.mp4" "OBS capture is operator-guided from WSL; manifest awaits final video path."
    exit 0
  fi

  if have ffmpeg && [ -n "${DISPLAY:-}" ]; then
    log "ffmpeg detected; attempting x11grab live capture from DISPLAY=${DISPLAY}"
    ./scripts/godot_local.sh --path . --scene res://test-straight-basic-visual-offaxis-observe.tscn &
    local godot_pid=$!
    sleep 4
    set +e
    ffmpeg -y \
      -video_size "$DISPLAY_SIZE" \
      -framerate "$FPS" \
      -f x11grab \
      -i "${DISPLAY}.0" \
      -t "$LIVE_CAPTURE_SECONDS" \
      -c:v libx264 \
      -pix_fmt yuv420p \
      "$OUTPUT_MP4" 2>&1 | tee -a "$LOG"
    local ffmpeg_status=${PIPESTATUS[0]}
    set -e
    if kill -0 "$godot_pid" >/dev/null 2>&1; then
      kill "$godot_pid" || true
    fi
    if [ "$ffmpeg_status" -eq 0 ] && [ -s "$OUTPUT_MP4" ]; then
      log "live capture wrote $OUTPUT_MP4"
      write_operator_manifest "ffmpeg x11grab" "PASS" "output/v0.0-pre/video/$(basename "$OUTPUT_MP4")" "Live screen capture was produced from WSL DISPLAY."
      exit 0
    fi
    log "ffmpeg live capture failed; falling back to verified still assembly"
  else
    log "OBS/ffmpeg live capture unavailable; falling back to verified still assembly"
  fi

  bash scripts/build_grin_observe_demo_from_frames.sh --method "fallback frames via record script"
}

main "$@"
