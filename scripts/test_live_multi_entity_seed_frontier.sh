#!/usr/bin/env bash
set -euo pipefail

log_file="$(mktemp)"
trap 'rm -f "$log_file"' EXIT

timeout 120s godot4-mono --path . --headless \
  --script res://scripts/test_live_multi_entity_seed_frontier.gd >"$log_file" 2>&1

line="$(rg '\[PixelMemory\]\[LIVE\].*uniqueLiveEntities=' "$log_file" | tail -1)"
test -n "$line"

python3 - "$line" <<'PY'
import re
import sys

line = sys.argv[1]
def value(name):
    match = re.search(r"(?:^| )" + re.escape(name) + r"=([0-9]+)", line)
    if not match:
        raise SystemExit(f"missing {name}")
    return int(match.group(1))

assert value("uniqueLiveEntities") >= 2
assert value("seededPx") > value("largestEntitySeedPx")
assert value("frontierCandidatePx") > 0
PY

echo "LIVE MULTI-ENTITY SEED FRONTIER PASS"
echo "$line"
