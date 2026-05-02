#!/usr/bin/env bash
# Cheap smoke for the reference-precision null geodesic probe.

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
SCENE="${DOE_SCENE:-res://test-domain-resolver-stress.tscn}"
FIXTURE="${DOE_FIXTURE:-domain_resolver_stress}"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${DOE_ROOT:-$ROOT/output/reference_geodesic_probe_smoke/$TIMESTAMP}"

mkdir -p "$OUTPUT_DIR"

echo "[reference-geodesic-smoke] output=$OUTPUT_DIR"
dotnet build "$ROOT" || exit 1

set +e
"$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
	--render-test \
	--domain-audit-quick \
	"--render-test-fixture=$FIXTURE" \
	--render-test-capture=1 \
	"--render-test-capture-dir=$OUTPUT_DIR" \
	--render-test-capture-mode=reference_geodesic_probe_smoke \
	--render-test-frames=5 \
	--render-test-warmup=0 \
	--render-test-film-width=320 \
	--render-test-film-height=180 \
	--render-test-film-scale=1.0 \
	--render-test-camera-fixed=1 \
	--render-test-step-length=0.015 \
	--render-test-pixel-stride=4 \
	--reference-geodesic-probe=1 \
	--reference-geodesic-probe-max-anchors=2 \
	--reference-geodesic-probe-max-steps=2048 \
	> "$OUTPUT_DIR/run.log" 2>&1
exit_code=$?
set -e

effective="$exit_code"
if [[ "$exit_code" -eq 0 ]] || grep -q "\[RenderTestRunner\]\[ExitCode\] forced=0 reason=harness_success" "$OUTPUT_DIR/run.log" 2>/dev/null; then
	effective=0
fi

echo "$exit_code" > "$OUTPUT_DIR/status.txt"
echo "$effective" > "$OUTPUT_DIR/effective_status.txt"

echo "[reference-geodesic-smoke] exit=$exit_code effective=$effective"
echo "[reference-geodesic-smoke] diagnostics:"
find "$OUTPUT_DIR" -maxdepth 1 \( -name '*.reference_geodesic_probe.csv' -o -name '*.reference_geodesic_probe.json' \) -print

exit "$effective"
