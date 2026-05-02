#!/usr/bin/env bash
# run_doe_scheduler_resonance.sh - daytime low-credit DOE for row/stride resonance.
#
# Full run:
#   DOE_MAX_HOURS=8 bash scripts/run_doe_scheduler_resonance.sh
#
# Smoke test:
#   DOE_SMOKE_ONE_CELL=1 DOE_FRAMES=5 DOE_WARMUP=0 bash scripts/run_doe_scheduler_resonance.sh

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCENE="${DOE_SCENE:-res://test-domain-resolver-stress.tscn}"
FIXTURE="${DOE_FIXTURE:-domain_resolver_stress}"
GODOT_BIN="${GODOT_BIN:-$ROOT/scripts/godot_local.sh}"
PYTHON="${DOE_PYTHON:-$ROOT/.venv/bin/python3}"
if [[ ! -x "$PYTHON" ]]; then
    PYTHON="python3"
fi

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUTPUT_DIR="${DOE_RESUME_DIR:-${DOE_ROOT:-$ROOT/output/doe_scheduler_resonance/$TIMESTAMP}}"
if [[ "$OUTPUT_DIR" != /* ]]; then
    OUTPUT_DIR="$ROOT/$OUTPUT_DIR"
fi

MAX_HOURS="${DOE_MAX_HOURS:-8}"
FRAMES="${DOE_FRAMES:-90}"
WARMUP="${DOE_WARMUP:-5}"
RES="${DOE_RES:-320x180}"
FILM_W="${RES%x*}"
FILM_H="${RES#*x}"
FILM_SCALE="${DOE_FILM_SCALE:-1.0}"
SMOKE_ONE_CELL="${DOE_SMOKE_ONE_CELL:-0}"

START_EPOCH="$(date +%s)"
MAX_SECONDS="$(awk -v h="$MAX_HOURS" 'BEGIN { printf "%d", h * 3600 }')"

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/scheduler_doe.log"
exec > >(tee -a "$LOG") 2>&1

echo "[scheduler-doe] output=$OUTPUT_DIR"
echo "[scheduler-doe] max_hours=$MAX_HOURS frames=$FRAMES warmup=$WARMUP res=${FILM_W}x${FILM_H}"
echo "[scheduler-doe] stop_file=$OUTPUT_DIR/STOP"

refresh_summary() {
    "$PYTHON" "$ROOT/tools/doe_scheduler_resonance_analysis.py" "$OUTPUT_DIR" || true
}

elapsed_seconds() {
    local now
    now="$(date +%s)"
    echo $((now - START_EPOCH))
}

should_stop_before_next_cell() {
    if [[ -f "$OUTPUT_DIR/STOP" ]]; then
        echo "[scheduler-doe] STOP file detected; stopping before next cell"
        return 0
    fi
    local elapsed
    elapsed="$(elapsed_seconds)"
    if (( elapsed >= MAX_SECONDS )); then
        echo "[scheduler-doe] time budget exceeded after current cell: elapsed=${elapsed}s max=${MAX_SECONDS}s"
        return 0
    fi
    return 1
}

cell_dir_for() {
    local phase="$1"
    local step_len="$2"
    local mode="$3"
    local stride="$4"
    echo "$OUTPUT_DIR/phase_${phase}/step_${step_len}/${mode}_stride_${stride}"
}

write_metadata() {
    local cell_dir="$1"
    local phase="$2"
    local step_len="$3"
    local mode="$4"
    local stride="$5"
    local exit_code="$6"
    local effective="$7"
    local notes="$8"
    local ts
    ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    cat > "$cell_dir/metadata.json" <<EOF
{
  "timestamp": "$ts",
  "phase": "$phase",
  "step_length": "$step_len",
  "mode": "$mode",
  "stride": "$stride",
  "cell_dir": "$cell_dir",
  "exit_code": $exit_code,
  "effective_status": $effective,
  "notes": "$notes"
}
EOF
}

mode_flags() {
    local mode="$1"
    case "$mode" in
        off)          echo "0 0 0" ;;
        telemetry_on) echo "1 0 0" ;;
        *) echo "[scheduler-doe] unknown mode: $mode" >&2; return 1 ;;
    esac
}

run_cell() {
    local phase="$1"
    local step_len="$2"
    local mode="$3"
    local stride="$4"
    local cell_dir
    cell_dir="$(cell_dir_for "$phase" "$step_len" "$mode" "$stride")"
    mkdir -p "$cell_dir"

    if [[ -f "$cell_dir/effective_status.txt" ]] && [[ "$(tr -d '[:space:]' < "$cell_dir/effective_status.txt")" == "0" ]]; then
        echo "[scheduler-doe] skip completed phase=$phase sl=$step_len mode=$mode stride=$stride"
        if [[ ! -f "$cell_dir/metadata.json" ]]; then
            write_metadata "$cell_dir" "$phase" "$step_len" "$mode" "$stride" 0 0 "resumed_completed"
        fi
        refresh_summary
        return 0
    fi

    local flags telemetry resolver step_conv
    flags="$(mode_flags "$mode")" || return 1
    read -r telemetry resolver step_conv <<< "$flags"

    echo "[scheduler-doe] run phase=$phase sl=$step_len mode=$mode stride=$stride"
    set +e
    "$GODOT_BIN" --headless --path "$ROOT" --scene "$SCENE" -- \
        --render-test \
        --domain-audit-quick \
        "--render-test-fixture=$FIXTURE" \
        --render-test-capture=1 \
        "--render-test-capture-dir=$cell_dir" \
        "--render-test-capture-mode=$mode" \
        "--render-test-frames=$FRAMES" \
        "--render-test-warmup=$WARMUP" \
        "--render-test-film-width=$FILM_W" \
        "--render-test-film-height=$FILM_H" \
        "--render-test-film-scale=$FILM_SCALE" \
        --render-test-camera-fixed=1 \
        "--render-test-step-length=$step_len" \
        "--render-test-pixel-stride=$stride" \
        "--enable-domain-telemetry=$telemetry" \
        "--enable-domain-aware-first-hit-resolver=$resolver" \
        "--enable-step-convergence-telemetry=$step_conv" \
        > "$cell_dir/run.log" 2>&1
    local exit_code=$?
    set -e

    local effective="$exit_code"
    local notes="error"
    if [[ "$exit_code" -eq 0 ]]; then
        effective=0
        notes="clean_exit"
    elif [[ "$exit_code" -eq 134 ]] || grep -q "\[RenderTestRunner\]\[ExitCode\] forced=0 reason=harness_success" "$cell_dir/run.log" 2>/dev/null; then
        effective=0
        notes="godot_shutdown_abort_after_harness_success"
    else
        notes="error_exit_${exit_code}"
    fi

    echo "$exit_code" > "$cell_dir/status.txt"
    echo "$effective" > "$cell_dir/effective_status.txt"
    write_metadata "$cell_dir" "$phase" "$step_len" "$mode" "$stride" "$exit_code" "$effective" "$notes"
    echo "[scheduler-doe] status phase=$phase sl=$step_len mode=$mode stride=$stride exit=$exit_code effective=$effective notes=$notes"
    refresh_summary
    return 0
}

echo "[scheduler-doe] dotnet build"
dotnet build "$ROOT"
echo "[scheduler-doe] build done"

STEP_LENGTHS=("0.018" "0.016" "0.015" "0.014" "0.013" "0.0125" "0.011" "0.010" "0.0075" "0.00625")
STRIDES=("1" "2" "4" "8")
TELEMETRY_STEPS=("0.025" "0.015" "0.0125" "0.00625")

if [[ "$SMOKE_ONE_CELL" == "1" ]]; then
    STEP_LENGTHS=("0.015")
    STRIDES=("2")
fi

for sl in "${STEP_LENGTHS[@]}"; do
    for stride in "${STRIDES[@]}"; do
        run_cell "A_off_stride" "$sl" "off" "$stride"
        should_stop_before_next_cell && { refresh_summary; echo "[scheduler-doe] complete output=$OUTPUT_DIR"; exit 0; }
        [[ "$SMOKE_ONE_CELL" == "1" ]] && { refresh_summary; echo "[scheduler-doe] complete output=$OUTPUT_DIR"; exit 0; }
    done
done

for sl in "${TELEMETRY_STEPS[@]}"; do
    for stride in "${STRIDES[@]}"; do
        run_cell "B_telemetry_if_time" "$sl" "telemetry_on" "$stride"
        should_stop_before_next_cell && { refresh_summary; echo "[scheduler-doe] complete output=$OUTPUT_DIR"; exit 0; }
    done
done

refresh_summary
echo "[scheduler-doe] complete output=$OUTPUT_DIR"
echo "[scheduler-doe] summaries:"
echo "  $OUTPUT_DIR/scheduler_DOE_summary.csv"
echo "  $OUTPUT_DIR/scheduler_DOE_summary.json"
echo "  $OUTPUT_DIR/scheduler_DOE_summary.md"
