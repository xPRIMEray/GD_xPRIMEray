#!/usr/bin/env bash
# Causal Observatory Testbench - for ultra-turbo + hermetic + cathedral probe diagnostics.
# Adapted from atomic orbital harness for focused banding / fingerprint experiments.

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"

RES="${RES:-480x270}"
CAUSAL_THREADS="${CAUSAL_THREADS:-20}"
ULTRA_TURBO="${ULTRA_TURBO:-0}"
ENABLE_OVERLAYS="${ENABLE_OVERLAYS:-1}"
ENABLE_HERMETIC_DEBUG="${ENABLE_HERMETIC_DEBUG:-1}"
ENABLE_CURVATURE_FINGERPRINT="${ENABLE_CURVATURE_FINGERPRINT:-1}"

FRAMES=40
WARMUP=3
UPDATE_BUDGET_MS=6000

if [[ "$RES" == "480x270" ]]; then
    FILM_W=480
    FILM_H=270
elif [[ "$RES" == "320x180" ]]; then
    FILM_W=320
    FILM_H=180
else
    FILM_W=480
    FILM_H=270
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${OUTPUT_DIR:-$ROOT/output/causal_observatory_testbench/$TIMESTAMP}"
mkdir -p "$OUTPUT_DIR"

LOG="$OUTPUT_DIR/causal_observatory_testbench.log"
exec > >(tee -a "$LOG") 2>&1

echo "[causal-testbench] output=$OUTPUT_DIR"
echo "[causal-testbench] res=${FILM_W}x${FILM_H} causal_threads=$CAUSAL_THREADS ultra_turbo=$ULTRA_TURBO"
echo "[causal-testbench] overlays=$ENABLE_OVERLAYS hermetic_debug=$ENABLE_HERMETIC_DEBUG curvature_fingerprint=$ENABLE_CURVATURE_FINGERPRINT"

EXTRA_ARGS=""
[[ "$ULTRA_TURBO" == "1" ]] && EXTRA_ARGS+=" --ultra-turbo=1"
[[ "$ENABLE_OVERLAYS" == "1" ]] && EXTRA_ARGS+=" --enable-causal-overlay=1"
[[ "$ENABLE_HERMETIC_DEBUG" == "1" ]] && EXTRA_ARGS+=" --enable-hermetic-debug=1"
[[ "$ENABLE_CURVATURE_FINGERPRINT" == "1" ]] && EXTRA_ARGS+=" --enable-curvature-fingerprint=1 --curvature-fingerprint-overlay=1"

"$GODOT_BIN" --headless --path "$ROOT" --scene "res://test-atomic-orbital-visual-observatory.tscn" -- \
    --render-test \
    --domain-audit-quick \
    "--render-test-fixture=atomic_orbital_visual_observatory" \
    --render-test-capture=1 \
    "--render-test-capture-dir=$OUTPUT_DIR" \
    "--render-test-capture-mode=causal_ultra_turbo" \
    "--render-test-film-width=$FILM_W" \
    "--render-test-film-height=$FILM_H" \
    --render-test-film-scale=1.0 \
    "--render-test-frames=$FRAMES" \
    "--render-test-warmup=$WARMUP" \
    "--render-test-update-budget-ms=$UPDATE_BUDGET_MS" \
    --render-test-camera-fixed=1 \
    --render-test-step-length=0.0125 \
    --render-test-steps-per-ray=1200 \
    --render-test-pixel-stride=1 \
    --render-test-first-pass-traversal=row \
    --benchmark-deterministic=1 \
    --benchmark-fixed-seed=1337 \
    --diagnostic-wireframe-overlay=0 \
    --enable-domain-telemetry=0 \
    "--causal-threads=$CAUSAL_THREADS" \
    --object-seeded-tile-scheduler=1 \
    --causal-turbo=1 \
    $EXTRA_ARGS \
    > "$OUTPUT_DIR/run.log" 2>&1

EXIT_CODE=$?

echo "[causal-testbench] exit_code=$EXIT_CODE"
echo "[causal-testbench] complete output=$OUTPUT_DIR"
exit $EXIT_CODE
