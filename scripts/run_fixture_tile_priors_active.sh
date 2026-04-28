#!/usr/bin/env bash
# run_fixture_tile_priors_active.sh — Fixture variant: tile_priors_active
#
# Activates the TileMetrics persistent prior system with optional offline band seed.
# Runs against curved_minimal and wormhole scenes; exports tile_metrics_summary.json
# alongside domain_telemetry_summary.json for each capture.
#
# Feature flags enabled:
#   EnableTileMetricsScaffold        = true
#   EnableTileMetricsPersistentPriors = true
#   EnableTileMetricsReorderSimulation = true  (observe-only)
#   EnableTileMetricsBandSeed         = true   (if BAND_SEED_PATH is set)
#   EnableTileMetricsReorderExecution = false  (NOT activated — simulation only)
#
# Usage:
#   ./scripts/run_fixture_tile_priors_active.sh
#
# Environment overrides:
#   FIXTURE_TILE_PRIORS_SCENE        — Godot scene path (default: curved_minimal)
#   FIXTURE_TILE_PRIORS_FRAMES       — frames per run (default: 120)
#   FIXTURE_TILE_PRIORS_WARMUP       — warmup frames (default: 15)
#   FIXTURE_TILE_PRIORS_SUBTILE_W    — TileMetricsSubtileWidth (default: 64)
#   BAND_SEED_PATH                   — absolute path to band_tile_signals.json from a
#                                       prior band_detector.py run (optional)
#   GODOT_EXE                        — override Godot binary path
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

if [[ -f ".env.local" ]]; then
  # shellcheck disable=SC1091
  source .env.local
fi

GODOT_BIN="$ROOT/scripts/godot_local.sh"

emit_build_fingerprint() {
  local dll_path="$ROOT/.godot/mono/temp/bin/Debug/Physical Light and Camera Units.dll"
  [[ -f "$dll_path" ]] || return 0
  local git_short="nogit"
  git -C "$ROOT" rev-parse --short=12 HEAD >/dev/null 2>&1 && \
    git_short="$(git -C "$ROOT" rev-parse --short=12 HEAD)"
  local dll_sha
  dll_sha="$(sha256sum "$dll_path" | awk '{print substr($1,1,16)}')"
  export XPRIMERAY_BUILD_GIT_SHORT="$git_short"
  export XPRIMERAY_BUILD_FINGERPRINT="tile_priors_active_git_${git_short}_sha_${dll_sha}"
  echo "Build fingerprint: $XPRIMERAY_BUILD_FINGERPRINT"
}

VARIANT="tile_priors_active"
SCENE_PATH="${FIXTURE_TILE_PRIORS_SCENE:-res://test-curved-minimal.tscn}"
FRAMES="${FIXTURE_TILE_PRIORS_FRAMES:-120}"
WARMUP="${FIXTURE_TILE_PRIORS_WARMUP:-15}"
SUBTILE_W="${FIXTURE_TILE_PRIORS_SUBTILE_W:-64}"
BAND_SEED="${BAND_SEED_PATH:-}"

TIMESTAMP="$(date +"%Y-%m-%dT%H-%M-%S")"
RUN_DIR="$ROOT/output/fixture_runs/$VARIANT/$TIMESTAMP"
mkdir -p "$RUN_DIR"

CAPTURE_PATH="$RUN_DIR/capture.png"
LOG_PATH="$RUN_DIR/run.log"
CAPTURE_RES_PATH="res://output/fixture_runs/$VARIANT/$TIMESTAMP/capture.png"

echo "=== tile_priors_active fixture ==="
echo "Scene:     $SCENE_PATH"
echo "Frames:    $FRAMES (warmup=$WARMUP)"
echo "SubtileW:  $SUBTILE_W"
echo "BandSeed:  ${BAND_SEED:-<none>}"
echo "RunDir:    $RUN_DIR"

echo ""
echo "Building .NET project..."
dotnet build 'Physical Light and Camera Units.csproj' -c Debug -v minimal
emit_build_fingerprint

# Build Godot CLI arguments.
TILE_ARGS=(
  "--tile-metrics=1"
  "--tile-metrics-subtile-width=$SUBTILE_W"
  "--tile-metrics-simulate-reorder=1"
  "--tile-metrics-persistent-priors=1"
  "--tile-metrics-reorder-execution=0"
)

if [[ -n "$BAND_SEED" ]]; then
  if [[ -f "$BAND_SEED" ]]; then
    TILE_ARGS+=(
      "--tile-metrics-band-seed=1"
      "--tile-metrics-band-seed-path=$BAND_SEED"
    )
    echo "Band seed: $BAND_SEED"
  else
    echo "[tile_priors] WARNING: BAND_SEED_PATH set but file not found: $BAND_SEED" >&2
  fi
fi

export APPDATA="$ROOT/.appdata"
export LOCALAPPDATA="$ROOT/.localappdata"
export USERPROFILE="$ROOT/.userprofile"

CMD=(
  "$GODOT_BIN"
  "--path" "."
  "--scene" "$SCENE_PATH"
  "--"
  "--render-test"
  "--render-test-capture=1"
  "--render-test-capture-dir=$RUN_DIR"
  "--render-test-frames=$FRAMES"
  "--render-test-warmup=$WARMUP"
  "--enable-domain-telemetry=1"
  "--enable-domain-aware-first-hit-resolver=1"
  "--export-telemetry-heatmaps=1"
  "${TILE_ARGS[@]}"
)

echo ""
echo "Launching: ${CMD[*]}"
echo ""

RUN_START="$(date +%s%3N)"
set +e
"${CMD[@]}" >"$LOG_PATH" 2>&1
GODOT_EXIT_CODE=$?
set -e
RUN_END="$(date +%s%3N)"
RUNTIME_MS=$(( RUN_END - RUN_START ))

echo ""
echo "=== tile_priors_active run complete ==="
echo "Exit code: $GODOT_EXIT_CODE"
echo "Runtime:   ${RUNTIME_MS}ms"
echo "Log:       $LOG_PATH"
echo "Capture:   $CAPTURE_PATH"
echo "RunDir:    $RUN_DIR"

if [[ -f "$CAPTURE_PATH" ]]; then
  echo ""
  echo "Artifacts in $RUN_DIR:"
  ls -lh "$RUN_DIR"/*.json "$RUN_DIR"/*.png 2>/dev/null || true
fi

echo ""
echo "To run band_detector on the output:"
echo "  python tools/band_detector.py $CAPTURE_PATH --output $RUN_DIR/band_tile_signals.json"
echo ""
echo "To compare with a baseline:"
echo "  python tools/run_band_comparison.py \\"
echo "    --baseline <baseline_run>/band_tile_signals.json \\"
echo "    --treatment $RUN_DIR/band_tile_signals.json"

exit "$GODOT_EXIT_CODE"
