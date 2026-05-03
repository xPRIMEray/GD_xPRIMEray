#!/usr/bin/env bash
# run_doe_overnight.sh - unattended modular DOE runner for xPRIMEray.
#
# Defaults:
#   DOE_MAX_HOURS=8
#   DOE_RES=320x180
#   DOE_FRAMES=90
#   DOE_WARMUP=5
#   DOE_ROOT=output/doe_overnight/<timestamp>
#
# Smoke test:
#   DOE_SMOKE_ONE_CELL=1 DOE_FRAMES=5 DOE_WARMUP=0 bash scripts/run_doe_overnight.sh

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
OUTPUT_DIR="${DOE_RESUME_DIR:-${DOE_ROOT:-$ROOT/output/doe_overnight/$TIMESTAMP}}"
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
DEFAULT_STRIDE="${DOE_STRIDE:-2}"
SMOKE_ONE_CELL="${DOE_SMOKE_ONE_CELL:-0}"
ENABLE_COHERENCE_BASIN="${DOE_ENABLE_COHERENCE_BASIN:-0}"

START_EPOCH="$(date +%s)"
MAX_SECONDS="$(awk -v h="$MAX_HOURS" 'BEGIN { printf "%d", h * 3600 }')"

mkdir -p "$OUTPUT_DIR"
LOG="$OUTPUT_DIR/doe_overnight.log"
exec > >(tee -a "$LOG") 2>&1

echo "[overnight] output=$OUTPUT_DIR"
echo "[overnight] max_hours=$MAX_HOURS frames=$FRAMES warmup=$WARMUP res=${FILM_W}x${FILM_H}"
echo "[overnight] stop_file=$OUTPUT_DIR/STOP"

has_stride_cli=0
if rg -q "render-test-(pixel-)?stride|RenderTest.*StrideCmdArgPrefix" "$ROOT/RendererCore/Testing/RenderTestRunner.cs" 2>/dev/null; then
    has_stride_cli=1
fi
echo "[overnight] stride_cli_available=$has_stride_cli"

echo "[overnight] dotnet build"
dotnet build "$ROOT"
echo "[overnight] build done"

refresh_summary() {
    "$PYTHON" "$ROOT/tools/doe_overnight_analysis.py" "$OUTPUT_DIR" || true
}

elapsed_seconds() {
    local now
    now="$(date +%s)"
    echo $((now - START_EPOCH))
}

should_stop_before_next_cell() {
    if [[ -f "$OUTPUT_DIR/STOP" ]]; then
        echo "[overnight] STOP file detected; stopping before next cell"
        return 0
    fi
    local elapsed
    elapsed="$(elapsed_seconds)"
    if (( elapsed >= MAX_SECONDS )); then
        echo "[overnight] time budget exceeded after current cell: elapsed=${elapsed}s max=${MAX_SECONDS}s"
        return 0
    fi
    return 1
}

cell_dir_for() {
    local subset="$1"
    local step_len="$2"
    local mode="$3"
    local stride="$4"
    local stride_part=""
    if [[ -n "$stride" ]]; then
        stride_part="_stride_${stride}"
    fi
    echo "$OUTPUT_DIR/subset_${subset}/step_${step_len}/${mode}${stride_part}"
}

write_metadata() {
    local cell_dir="$1"
    local subset="$2"
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
  "subset": "$subset",
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
        resolver_on)  echo "1 1 0" ;;
        sconv_on)     echo "1 0 1" ;;
        coherence_basin_on) echo "0 0 0" ;;
        *) echo "[overnight] unknown mode: $mode" >&2; return 1 ;;
    esac
}

run_cell() {
    local subset="$1"
    local step_len="$2"
    local mode="$3"
    local stride="$4"
    local cell_dir
    cell_dir="$(cell_dir_for "$subset" "$step_len" "$mode" "$stride")"
    mkdir -p "$cell_dir"

    if [[ -f "$cell_dir/effective_status.txt" ]] && [[ "$(tr -d '[:space:]' < "$cell_dir/effective_status.txt")" == "0" ]]; then
        echo "[overnight] skip completed subset=$subset sl=$step_len mode=$mode stride=${stride:-na}"
        if [[ ! -f "$cell_dir/metadata.json" ]]; then
            write_metadata "$cell_dir" "$subset" "$step_len" "$mode" "$stride" 0 0 "resumed_completed"
        fi
        refresh_summary
        return 0
    fi

    local flags telemetry resolver step_conv
    flags="$(mode_flags "$mode")" || return 1
    read -r telemetry resolver step_conv <<< "$flags"

    local stride_args=()
    local stride_note="default_stride"
    if [[ -n "$stride" && "$has_stride_cli" == "1" ]]; then
        stride_args=("--render-test-stride=$stride")
        stride_note="stride_$stride"
    elif [[ -n "$stride" ]]; then
        stride_note="stride_cli_unavailable_requested_$stride"
    fi

    echo "[overnight] run subset=$subset sl=$step_len mode=$mode stride=${stride:-na} telemetry=$telemetry resolver=$resolver sconv=$step_conv"
    local coherence_args=()
    if [[ "$mode" == "coherence_basin_on" ]]; then
        coherence_args=(
            "--reference-geodesic-probe=1"
            "--reference-geodesic-probe-max-anchors=${DOE_COHERENCE_MAX_ANCHORS:-2}"
            "--reference-geodesic-probe-max-steps=${DOE_COHERENCE_MAX_STEPS:-2048}"
            "--transport-coherence-basin=1"
            "--transport-coherence-basin-max-centers=${DOE_COHERENCE_MAX_CENTERS:-8}"
            "--transport-coherence-basin-radii=${DOE_COHERENCE_RADII:-4,8,16}"
        )
    fi

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
        "--enable-domain-telemetry=$telemetry" \
        "--enable-domain-aware-first-hit-resolver=$resolver" \
        "--enable-step-convergence-telemetry=$step_conv" \
        "${stride_args[@]}" \
        "${coherence_args[@]}" \
        > "$cell_dir/run.log" 2>&1
    local exit_code=$?
    set -e

    local effective="$exit_code"
    local notes="error"
    if [[ "$exit_code" -eq 0 ]]; then
        effective=0
        notes="clean_exit;$stride_note"
    elif [[ "$exit_code" -eq 134 ]] || grep -q "\[RenderTestRunner\]\[ExitCode\] forced=0 reason=harness_success" "$cell_dir/run.log" 2>/dev/null; then
        effective=0
        notes="godot_shutdown_abort_after_harness_success;$stride_note"
    else
        notes="error_exit_${exit_code};$stride_note"
    fi

    echo "$exit_code" > "$cell_dir/status.txt"
    echo "$effective" > "$cell_dir/effective_status.txt"
    if [[ "$mode" == "coherence_basin_on" ]]; then
        "$PYTHON" "$ROOT/tools/reference_probe_analyzer.py" "$cell_dir" >> "$cell_dir/run.log" 2>&1 || true
    fi
    write_metadata "$cell_dir" "$subset" "$step_len" "$mode" "$stride" "$exit_code" "$effective" "$notes"
    echo "[overnight] status subset=$subset sl=$step_len mode=$mode stride=${stride:-na} exit=$exit_code effective=$effective notes=$notes"
    refresh_summary
    return 0
}

run_subset_a() {
    local steps=("0.04" "0.03" "0.025" "0.02" "0.015" "0.0125" "0.01" "0.0075" "0.00625")
    if [[ "$SMOKE_ONE_CELL" == "1" ]]; then
        steps=("0.025")
    fi
    for sl in "${steps[@]}"; do
        for mode in off telemetry_on; do
            run_cell "A" "$sl" "$mode" ""
            should_stop_before_next_cell && return 0
            [[ "$SMOKE_ONE_CELL" == "1" ]] && return 0
        done
    done
}

run_subset_b() {
    local steps=("0.025" "0.0125" "0.00625")
    for sl in "${steps[@]}"; do
        for mode in off resolver_on; do
            run_cell "B" "$sl" "$mode" ""
            should_stop_before_next_cell && return 0
        done
    done
}

run_subset_c() {
    if [[ "$has_stride_cli" != "1" ]]; then
        echo "[overnight] subset C skipped: render-test stride CLI not found"
        return 0
    fi
    local steps=("0.025" "0.0125" "0.00625")
    local strides=("1" "2" "4")
    for sl in "${steps[@]}"; do
        for stride in "${strides[@]}"; do
            run_cell "C" "$sl" "off" "$stride"
            should_stop_before_next_cell && return 0
        done
    done
}

run_subset_d() {
    local steps=("0.025" "0.0125" "0.00625")
    for sl in "${steps[@]}"; do
        run_cell "D" "$sl" "sconv_on" ""
        should_stop_before_next_cell && return 0
    done
}

run_subset_e() {
    [[ "$ENABLE_COHERENCE_BASIN" == "1" ]] || return 0
    local steps=("0.015" "0.0125" "0.00625")
    for sl in "${steps[@]}"; do
        run_cell "E" "$sl" "coherence_basin_on" ""
        should_stop_before_next_cell && return 0
    done
}

run_subset_a
if ! should_stop_before_next_cell && [[ "$SMOKE_ONE_CELL" != "1" ]]; then run_subset_b; fi
if ! should_stop_before_next_cell && [[ "$SMOKE_ONE_CELL" != "1" ]]; then run_subset_c; fi
if ! should_stop_before_next_cell && [[ "$SMOKE_ONE_CELL" != "1" ]]; then run_subset_d; fi
if ! should_stop_before_next_cell && [[ "$SMOKE_ONE_CELL" != "1" ]]; then run_subset_e; fi

refresh_summary
echo "[overnight] complete output=$OUTPUT_DIR"
echo "[overnight] summaries:"
echo "  $OUTPUT_DIR/DOE_overnight_summary.csv"
echo "  $OUTPUT_DIR/DOE_overnight_summary.json"
echo "  $OUTPUT_DIR/DOE_overnight_summary.md"
