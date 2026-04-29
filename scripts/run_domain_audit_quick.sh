#!/usr/bin/env bash
set -euo pipefail

# Runs the minimal domain-aware rendering audit matrix:
#   1. domain features OFF
#   2. telemetry ON, resolver OFF
#   3. telemetry ON, resolver ON
#
# Outputs are written under output/domain_audit_quick/ unless
# DOMAIN_AUDIT_QUICK_OUTPUT_ROOT is set.

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
SCENE="${DOMAIN_AUDIT_QUICK_SCENE:-res://test-curved-minimal.tscn}"
FIXTURE="${DOMAIN_AUDIT_QUICK_FIXTURE:-curved_minimal}"
FRAMES="${DOMAIN_AUDIT_QUICK_FRAMES:-30}"
WARMUP="${DOMAIN_AUDIT_QUICK_WARMUP:-3}"
OUTPUT_ROOT="${DOMAIN_AUDIT_QUICK_OUTPUT_ROOT:-$ROOT/output/domain_audit_quick}"
RUN_ROOT="$OUTPUT_ROOT/$(date -u +%Y%m%dT%H%M%SZ)"

mkdir -p "$RUN_ROOT"

echo "[domain-audit-quick] output=$RUN_ROOT"
echo "[domain-audit-quick] dotnet build"
dotnet build "Physical Light and Camera Units.sln" -c Debug -v minimal | tee "$RUN_ROOT/dotnet_build.log"

run_case() {
  local label="$1"
  local telemetry="$2"
  local resolver="$3"
  local case_dir="$RUN_ROOT/$label"
  mkdir -p "$case_dir"

  echo "[domain-audit-quick] run label=$label telemetry=$telemetry resolver=$resolver frames=$FRAMES warmup=$WARMUP"
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
    "--enable-domain-telemetry=$telemetry" \
    "--enable-domain-aware-first-hit-resolver=$resolver" \
    > "$case_dir/run.log" 2>&1
  local status=$?
  set -e
  echo "$status" > "$case_dir/status.txt"
  if [[ "$status" -ne 0 ]] && grep -q "\[RenderTestRunner\]\[ExitCode\] forced=0 reason=harness_success" "$case_dir/run.log"; then
    echo "0" > "$case_dir/effective_status.txt"
    echo "[domain-audit-quick] status label=$label exit=$status effective=0 note=godot_shutdown_abort_after_harness_success"
    return 0
  fi
  echo "$status" > "$case_dir/effective_status.txt"
  echo "[domain-audit-quick] status label=$label exit=$status effective=$status"
  return "$status"
}

status=0
run_case "off" 0 0 || status=1
run_case "telemetry_on" 1 0 || status=1
run_case "resolver_on" 1 1 || status=1

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
telemetry_beauty="$(find_beauty_png "$RUN_ROOT/telemetry_on")"
compare_result="missing"
if [[ -n "$off_beauty" && -n "$telemetry_beauty" ]]; then
  if cmp -s "$off_beauty" "$telemetry_beauty"; then
    compare_result="identical"
  else
    compare_result="different"
  fi
fi
echo "[domain-audit-quick] beauty_compare_off_vs_telemetry=$compare_result"

resolver_summary="$(find "$RUN_ROOT/resolver_on" -maxdepth 1 -type f -name "*.domain_telemetry_summary.json" | sort | head -n 1)"
if [[ -n "$resolver_summary" ]]; then
  echo "[domain-audit-quick] resolver_summary=$resolver_summary"
  if command -v python3 >/dev/null 2>&1; then
    python3 - "$resolver_summary" <<'PY'
import json
import sys
path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)
summary = data.get("resolver_change_summary", {})
print("[domain-audit-quick] resolver_changed_pixels=%s" % summary.get("changed_pixels", "missing"))
print("[domain-audit-quick] resolver_changed_hit_distance_pixels=%s" % summary.get("changed_hit_distance_pixels", "missing"))
print("[domain-audit-quick] resolver_mean_hit_distance_delta=%s" % summary.get("mean_hit_distance_delta", "missing"))
PY
  fi
else
  echo "[domain-audit-quick] resolver_summary=missing"
fi

echo "[domain-audit-quick] complete status=$status"
exit "$status"
