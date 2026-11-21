#!/usr/bin/env bash
# Fail-closed headless fixture runner (CQ0.5-STAB). One run, one classification line; exit 0 only for PASS.
#
# Usage: GODOT_BIN=<godot> scripts/run_headless_fixture.sh --pass '<stdout regex>' --fail-tag '<TAG>' \
#            [--timeout SEC] [--out DIR] [--name NAME] -- <godot args, e.g. --script res://scripts/test_x.gd>
#
# PASS requires the pass marker on stdout AND exit code 0 AND no "<TAG> FAIL" line AND no SCRIPT ERROR.
# Otherwise the run is classified as one of:
#   pre_verdict_failure  the fixture reported a failure (its own FAIL line or a script error)
#   post_verdict_exit    the pass marker was printed but the process exited non-zero (e.g. a crash at shutdown)
#   no_verdict           the process ended without any verdict (e.g. a native crash during the run)
#   timeout              the process exceeded --timeout and was killed
# The crash signature (first "Fatal error"/CrashHandler/FATAL lines) is reported when present.
set -u
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-godot4-mono}"
pass_re=""; fail_tag=""; timeout_s=600; out=""; name="fixture"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --pass) pass_re="$2"; shift 2;;
    --fail-tag) fail_tag="$2"; shift 2;;
    --timeout) timeout_s="$2"; shift 2;;
    --out) out="$2"; shift 2;;
    --name) name="$2"; shift 2;;
    --) shift; break;;
    *) echo "usage: $0 --pass REGEX --fail-tag TAG [--timeout SEC] [--out DIR] [--name NAME] -- godot-args" >&2; exit 2;;
  esac
done
[[ -n "$pass_re" && -n "$fail_tag" && $# -gt 0 ]] || { echo "missing --pass, --fail-tag or godot arguments" >&2; exit 2; }
# C# build output can be removed externally between builds; a missing GodotSharp.dll fails as unrelated errors.
if [[ ! -f "$ROOT/.godot/mono/temp/bin/Debug/GodotSharp.dll" ]]; then
  echo "$name class=environment_failure reason=GodotSharp.dll_missing (run dotnet build first)"; exit 3
fi
out="${out:-$(mktemp -d)}"; mkdir -p "$out"
start=$(date +%s)
timeout "$timeout_s" "$GODOT_BIN" --headless --path "$ROOT" "$@" > "$out/$name.out" 2> "$out/$name.err"; rc=$?
seconds=$(( $(date +%s) - start ))
pass=$(grep -cE "$pass_re" "$out/$name.out")
fail=$(grep -c "$fail_tag FAIL" "$out/$name.out" "$out/$name.err" | awk -F: '{s+=$2} END {print s+0}')
script_errors=$(grep -c 'SCRIPT ERROR' "$out/$name.err")
signature=$(grep -h -A3 -E '^Fatal error|CrashHandlerException|ERROR: FATAL' "$out/$name.err" | tr -d '\r' | head -4 | sed 's/^ *//' | tr '\n' '|' | cut -c1-200)
if [[ $rc -eq 124 ]]; then class=timeout
elif [[ $pass -ge 1 && $fail -eq 0 && $script_errors -eq 0 && $rc -eq 0 ]]; then class=PASS
elif [[ $fail -gt 0 || $script_errors -gt 0 ]]; then class=pre_verdict_failure
elif [[ $pass -ge 1 ]]; then class=post_verdict_exit
else class=no_verdict; fi
echo "$name class=$class rc=$rc seconds=$seconds pass_markers=$pass fail_lines=$fail script_errors=$script_errors signature=${signature:-none} log=$out/$name.err"
[[ $class == PASS ]]
