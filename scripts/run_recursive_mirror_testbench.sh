#!/usr/bin/env bash
# Recursive Mirror Ghost Portal Testbench - Phase 1 Material System validation
# Focus: crisp geometry edges (perfect mirror reflections) + smooth optical curvature (GRIN + refraction)
# Uses BoundaryLayerVolume with new PerfectMirrorReflection / DielectricRefraction behaviors.

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"

RES="${RES:-1024x576}"   # high-res for canonical benchmark PNG
CAUSAL_THREADS="${CAUSAL_THREADS:-8}"
ENABLE_OVERLAYS="${ENABLE_OVERLAYS:-1}"
ENABLE_HERMETIC_DEBUG="${ENABLE_HERMETIC_DEBUG:-1}"

FRAMES=25
WARMUP=3
UPDATE_BUDGET_MS=10000

if [[ "$RES" == "1024x576" ]]; then
    FILM_W=1024; FILM_H=576
elif [[ "$RES" == "480x270" ]]; then
    FILM_W=480; FILM_H=270
else
    FILM_W=1024; FILM_H=576
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${OUTPUT_DIR:-$ROOT/output/recursive_mirror_ghost_portal/$TIMESTAMP}"
mkdir -p "$OUTPUT_DIR"

LOG="$OUTPUT_DIR/recursive_mirror_testbench.log"
exec > >(tee -a "$LOG") 2>&1

echo "[recursive-mirror] output=$OUTPUT_DIR"
echo "[recursive-mirror] res=${FILM_W}x${FILM_H} causal_threads=$CAUSAL_THREADS"

EXTRA_ARGS=""
[[ "$ENABLE_OVERLAYS" == "1" ]] && EXTRA_ARGS+=" --enable-causal-overlay=1"
[[ "$ENABLE_HERMETIC_DEBUG" == "1" ]] && EXTRA_ARGS+=" --enable-hermetic-debug=1"

# Dedicated scene for the canonical Recursive Mirror Ghost Portal benchmark.
# Two thin parallel PerfectMirrorReflection BoundaryShells + GRIN corridor + ghost markers.
# High steps budget + overlays for deep clean recursion visualization (CausalDoppler, HermeticDebug, CurvatureFingerprint).
"$GODOT_BIN" --headless --path "$ROOT" --scene "res://test-recursive-mirror-ghost-portal.tscn" -- \
    --render-test \
    --domain-audit-quick \
    "--render-test-fixture=recursive_mirror_ghost_portal" \
    --render-test-capture=1 \
    "--render-test-capture-dir=$OUTPUT_DIR" \
    "--render-test-capture-mode=recursive_mirror_ghost_portal" \
    "--render-test-film-width=$FILM_W" \
    "--render-test-film-height=$FILM_H" \
    --render-test-film-scale=1.0 \
    "--render-test-frames=$FRAMES" \
    "--render-test-warmup=$WARMUP" \
    "--render-test-update-budget-ms=$UPDATE_BUDGET_MS" \
    --render-test-camera-fixed=1 \
    --render-test-step-length=0.012 \
    --render-test-steps-per-ray=4000 \
    --render-test-pixel-stride=1 \
    --render-test-first-pass-traversal=row \
    --benchmark-deterministic=1 \
    --benchmark-fixed-seed=4242 \
    --diagnostic-wireframe-overlay=0 \
    "--causal-threads=$CAUSAL_THREADS" \
    --object-seeded-tile-scheduler=1 \
    --causal-turbo=1 \
    --ultra-turbo=1 \
    $EXTRA_ARGS \
    > "$OUTPUT_DIR/run.log" 2>&1

EXIT_CODE=$?
echo "[recursive-mirror] exit_code=$EXIT_CODE"
echo "[recursive-mirror] complete output=$OUTPUT_DIR"
exit $EXIT_CODE
