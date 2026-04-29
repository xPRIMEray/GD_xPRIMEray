#!/usr/bin/env bash
set -euo pipefail

# High-resolution visual validation path for the domain resolver stress fixture.
# Runs: OFF / telemetry ON / resolver ON at full film resolution (FilmResolutionScale=1.0).
# Default: 320x180 (16x the quick-audit effective 80x45).
# Optional: --medres (640x360) or --hires (1280x720).
# Keeps the low-res quick audit path intact; this script adds a separate visual path.
#
# Usage:
#   scripts/run_domain_audit_visual.sh            # 320x180
#   scripts/run_domain_audit_visual.sh --medres   # 640x360
#   scripts/run_domain_audit_visual.sh --hires    # 1280x720
#
# Output: output/domain_audit_visual/<timestamp>/

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
SCENE="${DOMAIN_AUDIT_VISUAL_SCENE:-res://test-domain-resolver-stress.tscn}"
FIXTURE="${DOMAIN_AUDIT_VISUAL_FIXTURE:-domain_resolver_stress}"
FRAMES="${DOMAIN_AUDIT_VISUAL_FRAMES:-30}"
WARMUP="${DOMAIN_AUDIT_VISUAL_WARMUP:-3}"
OUTPUT_ROOT="${DOMAIN_AUDIT_VISUAL_OUTPUT_ROOT:-$ROOT/output/domain_audit_visual}"
RUN_ROOT="$OUTPUT_ROOT/$(date -u +%Y%m%dT%H%M%SZ)"

FILM_W=320
FILM_H=180
FILM_SCALE=1.0
RES_LABEL="320x180"

for arg in "$@"; do
  case "$arg" in
    --medres)
      FILM_W=640
      FILM_H=360
      FILM_SCALE=1.0
      RES_LABEL="640x360"
      ;;
    --hires)
      FILM_W=1280
      FILM_H=720
      FILM_SCALE=1.0
      RES_LABEL="1280x720"
      ;;
  esac
done

mkdir -p "$RUN_ROOT"

echo "[domain-audit-visual] output=$RUN_ROOT"
echo "[domain-audit-visual] resolution=$RES_LABEL"
echo "[domain-audit-visual] dotnet build"
dotnet build "Physical Light and Camera Units.sln" -c Debug -v minimal | tee "$RUN_ROOT/dotnet_build.log"

run_case() {
  local label="$1"
  local telemetry="$2"
  local resolver="$3"
  local case_dir="$RUN_ROOT/$label"
  mkdir -p "$case_dir"

  echo "[domain-audit-visual] run label=$label telemetry=$telemetry resolver=$resolver res=$RES_LABEL"
  set +e
  "$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
    --render-test \
    --domain-audit-quick \
    "--render-test-fixture=$FIXTURE" \
    --render-test-capture=1 \
    "--render-test-capture-dir=$case_dir" \
    "--render-test-capture-mode=$label" \
    "--render-test-frames=$FRAMES" \
    "--render-test-warmup=$WARMUP" \
    "--render-test-film-width=$FILM_W" \
    "--render-test-film-height=$FILM_H" \
    "--render-test-film-scale=$FILM_SCALE" \
    "--enable-domain-telemetry=$telemetry" \
    "--enable-domain-aware-first-hit-resolver=$resolver" \
    > "$case_dir/run.log" 2>&1
  local exit_code=$?
  set -e
  echo "$exit_code" > "$case_dir/status.txt"
  if [[ "$exit_code" -ne 0 ]] && grep -q "\[RenderTestRunner\]\[ExitCode\] forced=0 reason=harness_success" "$case_dir/run.log"; then
    echo "0" > "$case_dir/effective_status.txt"
    echo "[domain-audit-visual] status label=$label exit=$exit_code effective=0 note=godot_shutdown_abort_after_harness_success"
    return 0
  fi
  echo "$exit_code" > "$case_dir/effective_status.txt"
  echo "[domain-audit-visual] status label=$label exit=$exit_code effective=$exit_code"
  return "$exit_code"
}

overall_status=0
run_case "off"          0 0 || overall_status=1
run_case "telemetry_on" 1 0 || overall_status=1
run_case "resolver_on"  1 1 || overall_status=1

# ── Beauty comparison: OFF vs telemetry ON ────────────────────────────────────

find_beauty_png() {
  local dir="$1"
  find "$dir" -maxdepth 1 -type f -name "*.png" \
    ! -name "*.domain_id.png" \
    ! -name "*.domain_confidence.png" \
    ! -name "*.boundary_confidence.png" \
    ! -name "*.selection_flip.png" \
    ! -name "*.normal_discontinuity.png" \
    | sort | head -n 1
}

off_beauty="$(find_beauty_png "$RUN_ROOT/off")"
tel_beauty="$(find_beauty_png "$RUN_ROOT/telemetry_on")"
res_beauty="$(find_beauty_png "$RUN_ROOT/resolver_on")"

compare_result="missing"
off_hash="missing"
tel_hash="missing"
if [[ -n "$off_beauty" && -n "$tel_beauty" ]]; then
  off_hash="$(sha256sum "$off_beauty" | awk '{print $1}')"
  tel_hash="$(sha256sum "$tel_beauty" | awk '{print $1}')"
  if [[ "$off_hash" == "$tel_hash" ]]; then
    compare_result="identical"
  else
    compare_result="different"
  fi
fi
echo "[domain-audit-visual] beauty_compare_off_vs_telemetry=$compare_result"
echo "[domain-audit-visual] off_beauty_hash=$off_hash"
echo "[domain-audit-visual] tel_beauty_hash=$tel_hash"

# ── Resolver telemetry summary ────────────────────────────────────────────────

resolver_summary="$(find "$RUN_ROOT/resolver_on" -maxdepth 1 -type f -name "*.domain_telemetry_summary.json" | sort | head -n 1)"
if [[ -n "$resolver_summary" ]]; then
  echo "[domain-audit-visual] resolver_summary=$resolver_summary"
  if command -v python3 >/dev/null 2>&1; then
    python3 - "$resolver_summary" <<'PY'
import json, sys
with open(sys.argv[1], "r", encoding="utf-8") as f:
    data = json.load(f)
s = data.get("resolver_change_summary", {})
print("[domain-audit-visual] resolver_changed_pixels=%s" % s.get("changed_pixels", "missing"))
print("[domain-audit-visual] resolver_changed_hit_distance_pixels=%s" % s.get("changed_hit_distance_pixels", "missing"))
print("[domain-audit-visual] resolver_mean_hit_distance_delta=%s" % s.get("mean_hit_distance_delta", "missing"))
PY
  fi
else
  echo "[domain-audit-visual] resolver_summary=missing"
fi

# ── Contact sheet ─────────────────────────────────────────────────────────────

SHEET_PATH="$RUN_ROOT/contact_sheet.png"
sheet_status="skip"

build_contact_sheet_imagemagick() {
  # Collect up to 7 panels: off beauty, tel beauty, resolver beauty, resolver diff,
  # boundary_confidence, normal_discontinuity, selection_flip
  local panels=()

  [[ -n "$off_beauty" ]]  && panels+=("$off_beauty")
  [[ -n "$tel_beauty" ]]  && panels+=("$tel_beauty")
  [[ -n "$res_beauty" ]]  && panels+=("$res_beauty")

  local resolver_diff_path="$RUN_ROOT/resolver_diff.png"
  if [[ -n "$tel_beauty" && -n "$res_beauty" ]]; then
    convert "$tel_beauty" "$res_beauty" \
      -compose Difference -composite \
      -auto-level \
      "$resolver_diff_path" 2>/dev/null && panels+=("$resolver_diff_path")
  fi

  local bconf ndisc sflip
  bconf="$(find "$RUN_ROOT/resolver_on" -maxdepth 1 -name "*.boundary_confidence.png" | sort | head -n 1)"
  ndisc="$(find "$RUN_ROOT/resolver_on" -maxdepth 1 -name "*.normal_discontinuity.png" | sort | head -n 1)"
  sflip="$(find "$RUN_ROOT/resolver_on" -maxdepth 1 -name "*.selection_flip.png" | sort | head -n 1)"
  [[ -n "$bconf" ]] && panels+=("$bconf")
  [[ -n "$ndisc" ]] && panels+=("$ndisc")
  [[ -n "$sflip" ]] && panels+=("$sflip")

  if [[ "${#panels[@]}" -eq 0 ]]; then
    return 1
  fi

  montage "${panels[@]}" -geometry +4+4 -tile "4x2" -background black \
    -label '%f' "$SHEET_PATH" 2>/dev/null
}

build_contact_sheet_python() {
  command -v python3 >/dev/null 2>&1 || return 1
  python3 - "$RUN_ROOT" "$SHEET_PATH" <<'PY'
import sys, os
from pathlib import Path
try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    sys.exit(1)

run_root = Path(sys.argv[1])
sheet_path = sys.argv[2]

def find_beauty(case_dir):
    excl = {'.domain_id.png', '.domain_confidence.png', '.boundary_confidence.png',
            '.selection_flip.png', '.normal_discontinuity.png'}
    for p in sorted(case_dir.glob('*.png')):
        if not any(p.name.endswith(s) for s in excl):
            return p
    return None

def find_map(case_dir, suffix):
    hits = sorted(case_dir.glob(f'*{suffix}'))
    return hits[0] if hits else None

panels = []
labels = []

off_b = find_beauty(run_root / 'off')
tel_b = find_beauty(run_root / 'telemetry_on')
res_b = find_beauty(run_root / 'resolver_on')

if off_b:  panels.append(off_b);  labels.append('OFF beauty')
if tel_b:  panels.append(tel_b);  labels.append('telemetry ON beauty')
if res_b:  panels.append(res_b);  labels.append('resolver ON beauty')

# Diff: telemetry ON vs resolver ON
if tel_b and res_b:
    diff_path = run_root / 'resolver_diff.png'
    t = Image.open(tel_b).convert('RGB')
    r = Image.open(res_b).convert('RGB')
    diff_pixels = [
        (abs(tp[0]-rp[0]), abs(tp[1]-rp[1]), abs(tp[2]-rp[2]))
        for tp, rp in zip(t.getdata(), r.getdata())
    ]
    diff_img = Image.new('RGB', t.size)
    diff_img.putdata(diff_pixels)
    # auto-level: scale to [0,255]
    max_val = max(max(px) for px in diff_pixels) if diff_pixels else 1
    if max_val > 0:
        scale = 255.0 / max_val
        diff_img = diff_img.point(lambda v: min(255, int(v * scale)))
    diff_img.save(diff_path)
    panels.append(diff_path)
    labels.append('resolver diff (scaled)')

bconf = find_map(run_root / 'resolver_on', '.boundary_confidence.png')
ndisc = find_map(run_root / 'resolver_on', '.normal_discontinuity.png')
sflip = find_map(run_root / 'resolver_on', '.selection_flip.png')

if bconf: panels.append(bconf); labels.append('boundary_confidence')
if ndisc: panels.append(ndisc); labels.append('normal_discontinuity')
if sflip: panels.append(sflip); labels.append('selection_flip')

if not panels:
    print('[contact-sheet] no panels found, skipping')
    sys.exit(0)

images = [Image.open(p).convert('RGB') for p in panels]
pw, ph = images[0].size
label_h = 18
cols = min(4, len(images))
rows = (len(images) + cols - 1) // cols
pad = 4
sheet_w = cols * pw + (cols + 1) * pad
sheet_h = rows * (ph + label_h) + (rows + 1) * pad
sheet = Image.new('RGB', (sheet_w, sheet_h), (0, 0, 0))
draw = ImageDraw.Draw(sheet)

try:
    font = ImageFont.load_default()
except Exception:
    font = None

for idx, (img, lbl) in enumerate(zip(images, labels)):
    col = idx % cols
    row = idx // cols
    x = pad + col * (pw + pad)
    y = pad + row * (ph + label_h + pad)
    sheet.paste(img, (x, y))
    draw.text((x + 2, y + ph + 2), lbl, fill=(200, 200, 200), font=font)

sheet.save(sheet_path)
print(f'[contact-sheet] wrote {sheet_path} ({sheet_w}x{sheet_h})')
PY
}

if command -v montage >/dev/null 2>&1; then
  if build_contact_sheet_imagemagick; then
    sheet_status="ok_imagemagick"
    echo "[domain-audit-visual] contact_sheet=$SHEET_PATH (imagemagick)"
  else
    echo "[domain-audit-visual] contact_sheet=imagemagick_failed"
  fi
fi

if [[ "$sheet_status" == "skip" ]]; then
  if build_contact_sheet_python; then
    sheet_status="ok_python"
    echo "[domain-audit-visual] contact_sheet=$SHEET_PATH (python)"
  else
    echo "[domain-audit-visual] contact_sheet=skip (no montage or PIL)"
  fi
fi

# ── Localization check (Python) ───────────────────────────────────────────────

if [[ -n "$tel_beauty" && -n "$res_beauty" ]] && command -v python3 >/dev/null 2>&1; then
  python3 - "$tel_beauty" "$res_beauty" \
    "$(find "$RUN_ROOT/resolver_on" -maxdepth 1 -name '*.boundary_confidence.png' | sort | head -n 1)" \
    "$(find "$RUN_ROOT/resolver_on" -maxdepth 1 -name '*.normal_discontinuity.png' | sort | head -n 1)" \
    "$(find "$RUN_ROOT/resolver_on" -maxdepth 1 -name '*.selection_flip.png' | sort | head -n 1)" \
    <<'PY'
import sys
try:
    from PIL import Image
except ImportError:
    print("[localization] PIL not available, skipping pixel analysis")
    sys.exit(0)

tel_path, res_path, bconf_path, ndisc_path, sflip_path = sys.argv[1:]

if not tel_path or not res_path:
    print("[localization] missing beauty paths")
    sys.exit(0)

tel  = Image.open(tel_path).convert('RGBA')
res  = Image.open(res_path).convert('RGBA')

diff = [(x, y)
        for y in range(tel.height) for x in range(tel.width)
        if tel.getpixel((x, y)) != res.getpixel((x, y))]
print(f"[localization] diff_pixels={len(diff)}")
if not diff:
    print("[localization] no pixel differences between telemetry_on and resolver_on beauty")
    sys.exit(0)

xs = [p[0] for p in diff]; ys = [p[1] for p in diff]
print(f"[localization] diff_bbox=({min(xs)},{min(ys)})-({max(xs)},{max(ys)})")

def channel_mean_max(img_path, points):
    if not img_path:
        return "missing", "missing"
    img = Image.open(img_path).convert('RGBA')
    vals = [img.getpixel((x, y))[0] / 255.0 for (x, y) in points]
    return sum(vals) / len(vals), max(vals)

for label, path in [
    ("boundary", bconf_path),
    ("normal_discontinuity", ndisc_path),
    ("selection_flip", sflip_path),
]:
    mean_v, max_v = channel_mean_max(path, diff)
    print(f"[localization] {label}_mean={mean_v:.6f} max={max_v:.6f}")
PY
fi

# ── Summary ───────────────────────────────────────────────────────────────────

echo "[domain-audit-visual] complete status=$overall_status res=$RES_LABEL output=$RUN_ROOT"
exit "$overall_status"
