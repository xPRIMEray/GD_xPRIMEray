#!/usr/bin/env python3
import argparse
import json
import math
import re
import struct
from pathlib import Path

from PIL import Image


FAIL_RE = re.compile(r"\[GrinBasicVisual\]\[Capture\]\[FAIL\].*")
GUARD_PROGRESS_RE = re.compile(r"reason=guard_progress\b")
FORCED_ADVANCE_RE = re.compile(r"forcedAdvance=1\b")


def scalar(token: str):
    token = token.strip()
    if not token or token.lower() == "na":
        return None
    try:
        if any(ch in token for ch in ".eE"):
            return float(token)
        return int(token)
    except ValueError:
        return token


def first_non_empty(*values):
    for value in values:
        if value is None:
            continue
        if isinstance(value, str) and value == "":
            continue
        if isinstance(value, (list, tuple, dict, set)) and len(value) == 0:
            continue
        return value
    return None


def parse_kv(line: str, prefix: str):
    idx = line.find(prefix)
    if idx < 0:
        return None

    result = {}
    for token in line[idx + len(prefix):].strip().split():
        if "=" not in token:
            continue
        key, value = token.split("=", 1)
        result[key] = scalar(value)
    return result


def parse_log(text: str) -> dict:
    parsed = {
        "capture": None,
        "captureArtifacts": None,
        "overlayDiag": None,
        "whiteStreakDiag": None,
        "writeDiag": None,
        "bottomRegionDiag": None,
        "rows": None,
        "visual": None,
        "runtimeBuild": None,
        "launchAudit": None,
        "renderer": None,
        "captureFailure": None,
        "guardProgress": len(GUARD_PROGRESS_RE.findall(text)),
        "forcedAdvance": len(FORCED_ADVANCE_RE.findall(text)),
    }
    for line in text.splitlines():
        capture = parse_kv(line, "[GrinBasicVisual][Capture]")
        if capture:
            parsed["capture"] = capture

        capture_artifacts = parse_kv(line, "[GrinBasicVisual][CaptureArtifacts]")
        if capture_artifacts:
            parsed["captureArtifacts"] = capture_artifacts

        overlay_diag = parse_kv(line, "[GrinBasicVisual][OverlayDiag]")
        if overlay_diag:
            parsed["overlayDiag"] = overlay_diag

        white_streak_diag = parse_kv(line, "[GrinBasicVisual][WhiteStreakDiag]")
        if white_streak_diag:
            parsed["whiteStreakDiag"] = white_streak_diag

        write_diag = parse_kv(line, "[GrinBasicVisual][WriteDiag]")
        if write_diag:
            parsed["writeDiag"] = write_diag

        bottom_region_diag = parse_kv(line, "[GrinBasicVisual][BottomRegionDiag]")
        if bottom_region_diag:
            parsed["bottomRegionDiag"] = bottom_region_diag

        launch = parse_kv(line, "[LaunchAudit]")
        if launch:
            parsed["launchAudit"] = launch

        renderer = parse_kv(line, "[GrinBasicVisual][Renderer]")
        if renderer:
            parsed["renderer"] = renderer

        rows = parse_kv(line, "[GrinBasicVisual][Rows]")
        if rows:
            parsed["rows"] = rows

        visual = parse_kv(line, "[GrinBasicVisual][Visual]")
        if visual:
            parsed["visual"] = visual

        runtime_build = parse_kv(line, "[RuntimeBuild]")
        if runtime_build:
            parsed["runtimeBuild"] = runtime_build

        fail = FAIL_RE.search(line)
        if fail:
            parsed["captureFailure"] = fail.group(0).strip()

    return parsed


def detect_image_size(path: Path) -> dict:
    if not path.exists() or path.stat().st_size <= 0:
        return {"width": None, "height": None}
    try:
        with path.open("rb") as handle:
            header = handle.read(24)
        if len(header) >= 24 and header[:8] == b"\x89PNG\r\n\x1a\n" and header[12:16] == b"IHDR":
            width, height = struct.unpack(">II", header[16:24])
            return {"width": width, "height": height}
    except OSError:
        return {"width": None, "height": None}
    return {"width": None, "height": None}


def build_params(args: argparse.Namespace, parsed: dict) -> dict:
    launch = parsed.get("launchAudit") or {}
    return {
        "timestamp": args.timestamp,
        "fixture_id": args.fixture_id,
        "scene": args.scene,
        "launcher": args.launcher,
        "resolved_fixture_identity": launch.get("actual_fixture"),
        "run_dir": str(args.run_dir),
        "capture_path": str(args.capture_path),
        "log_path": str(args.log_path),
        "requested_transport_model": args.requested_transport_model,
        "requested_step_length": args.requested_step_length,
        "requested_min_step_length": args.requested_min_step_length,
        "requested_steps_per_ray": args.requested_steps_per_ray,
        "requested_turn_threshold": args.requested_turn_threshold,
        "requested_error_tolerance": args.requested_error_tolerance,
        "thresholds": {
            "settle_frames": args.settle_frames,
            "min_render_health_step": args.min_rh_step,
            "min_processed_rows": args.min_processed_rows,
            "capture_film_opacity": args.capture_film_opacity,
            "compare_grid": args.compare_grid,
            "compare_crosshair": args.compare_crosshair,
            "radial_bin_count": args.radial_bin_count,
        },
    }


def build_metrics(args: argparse.Namespace, parsed: dict) -> dict:
    capture = parsed.get("capture") or {}
    rows = parsed.get("rows") or {}
    capture_artifacts = parsed.get("captureArtifacts") or {}
    overlay_diag = parsed.get("overlayDiag") or {}
    white_streak_diag = parsed.get("whiteStreakDiag") or {}
    write_diag = parsed.get("writeDiag") or {}
    bottom_region_diag = parsed.get("bottomRegionDiag") or {}
    visual = parsed.get("visual") or {}
    runtime_build = parsed.get("runtimeBuild") or {}
    launch = parsed.get("launchAudit") or {}
    renderer = parsed.get("renderer") or {}
    image = detect_image_size(args.capture_path)
    effective_turn_threshold = renderer.get("turnThreshold")
    if effective_turn_threshold is None:
        effective_turn_threshold = args.requested_turn_threshold
    effective_error_tolerance = renderer.get("errorTolerance")
    if effective_error_tolerance is None:
        effective_error_tolerance = args.requested_error_tolerance
    traced_pixels = capture.get("tracedPixels")
    source_hits = capture.get("sourceHits")
    miss_hits = capture.get("missHits")
    ready_frames = capture.get("readyFrames")
    render_health_step = capture.get("rhStep")
    processed_rows = capture.get("processedRows")
    capture_exists = args.capture_path.exists() and args.capture_path.stat().st_size > 0

    hit_success_rate = None
    miss_rate = None
    if isinstance(traced_pixels, (int, float)) and traced_pixels > 0:
        if isinstance(source_hits, (int, float)):
            hit_success_rate = source_hits / traced_pixels
        if isinstance(miss_hits, (int, float)):
            miss_rate = miss_hits / traced_pixels

    capture_failure = parsed.get("captureFailure")
    status = "ok"
    failure_reason = None

    if args.godot_exit_code != 0:
        status = "failed"
        failure_reason = f"godot_exit_{args.godot_exit_code}"
    elif capture_failure:
        status = "failed"
        failure_reason = capture_failure
    elif not capture_exists:
        status = "failed"
        failure_reason = "missing_capture_png"
    elif not capture:
        status = "failed"
        failure_reason = "missing_capture_log"
    elif launch.get("status") != "ok":
        status = "failed"
        failure_reason = "launch_audit_not_ok"

    return {
        "fixture_id": args.fixture_id,
        "scene": args.scene,
        "launcher": args.launcher,
        "status": status,
        "failure_reason": failure_reason,
        "transport_model_used": renderer.get("transport"),
        "launch_audit_status": launch.get("status"),
        "capture_succeeded": status == "ok",
        "runtime_seconds": args.runtime_seconds,
        "traced_pixels": traced_pixels,
        "source_hits": source_hits,
        "background_hits": capture.get("backgroundHits"),
        "absorbed_hits": capture.get("absorbedHits"),
        "miss_hits": miss_hits,
        "ready_frames": ready_frames,
        "render_health_step": render_health_step,
        "processed_rows": processed_rows,
        "total_rows_considered": rows.get("totalRowsConsidered"),
        "total_rows_processed": rows.get("totalRowsProcessed"),
        "total_rows_skipped": rows.get("totalRowsSkipped"),
        "processed_row_start": rows.get("processedRowStart"),
        "processed_row_end": rows.get("processedRowEnd"),
        "zero_hit_rows": rows.get("zeroHitRows"),
        "row_participation_summary": rows.get("summary"),
        "processed_row_ranges": rows.get("processedRowRanges"),
        "skipped_row_ranges": rows.get("skippedRowRanges"),
        "zero_hit_row_ranges": rows.get("zeroHitRowRanges"),
        "runtime_fingerprint": runtime_build.get("buildFingerprint"),
        "runtime_source_fingerprint": runtime_build.get("sourceFingerprint"),
        "runtime_git_short": runtime_build.get("gitShort"),
        "runtime_assembly_path": runtime_build.get("assemblyPath"),
        "runtime_assembly_write_utc": runtime_build.get("assemblyWriteUtc"),
        "runtime_module_version_id": runtime_build.get("moduleVersionId"),
        "guard_progress": parsed.get("guardProgress"),
        "forced_advance": parsed.get("forcedAdvance"),
        "effective_steps_per_ray": renderer.get("stepsPerRay"),
        "effective_step_length": renderer.get("stepLength"),
        "effective_min_step_length": renderer.get("minStepLength"),
        "effective_max_step_length": renderer.get("maxStepLength"),
        "effective_turn_threshold": effective_turn_threshold,
        "effective_error_tolerance": effective_error_tolerance,
        "image_width": image.get("width"),
        "image_height": image.get("height"),
        "hit_success_rate": hit_success_rate,
        "miss_rate": miss_rate,
        "visual_mode": visual.get("mode"),
        "visual_shading_mode": visual.get("shadingMode"),
        "visual_baseline_shading_mode": visual.get("baselineShadingMode"),
        "visual_source_highlight": visual.get("sourceHighlight"),
        "visual_diagnostic_flat": visual.get("authority"),
        "baseline_used_normal_shading": visual.get("normalShadingInBaseline"),
        "analysis_capture_path": capture_artifacts.get("analysisPath"),
        "debug_capture_path": capture_artifacts.get("debugPath"),
        "analysis_capture_mode": first_non_empty(
            write_diag.get("analysisCaptureMode"),
            capture_artifacts.get("analysisCaptureMode"),
        ),
        "analysis_capture_written": capture_artifacts.get("analysisCaptureWritten"),
        "debug_capture_written": capture_artifacts.get("debugCaptureWritten"),
        "categorical_final_written": capture_artifacts.get("categoricalFinalWritten"),
        "overlay_enabled_for_analysis_capture": capture_artifacts.get("overlayEnabledForAnalysisCapture"),
        "analysis_capture_width": capture_artifacts.get("analysisWidth"),
        "analysis_capture_height": capture_artifacts.get("analysisHeight"),
        "debug_capture_width": capture_artifacts.get("debugWidth"),
        "debug_capture_height": capture_artifacts.get("debugHeight"),
        "viewport_width": capture_artifacts.get("viewportWidth"),
        "viewport_height": capture_artifacts.get("viewportHeight"),
        "film_width": capture_artifacts.get("filmWidth"),
        "film_height": capture_artifacts.get("filmHeight"),
        "film_rows_rendered": capture_artifacts.get("filmRowsRendered"),
        "film_view_rect": capture_artifacts.get("filmViewRect"),
        "capture_crop": capture_artifacts.get("captureCrop"),
        "capture_crop_bounds": capture_artifacts.get("captureCropBounds"),
        "rendered_image_bounds": capture_artifacts.get("renderedImageBounds"),
        "unrendered_image_bounds": capture_artifacts.get("unrenderedImageBounds"),
        "ray_renderer_debug_mode": overlay_diag.get("rayRendererDebugMode"),
        "ray_renderer_debug_overlay_owned_by_film": overlay_diag.get("rayRendererDebugOverlayOwnedByFilm"),
        "ray_renderer_debug_max_rays": overlay_diag.get("rayRendererDebugMaxRays"),
        "film_overlay_draw_rays": overlay_diag.get("filmOverlayDrawRays"),
        "film_overlay_draw_hit_normals": overlay_diag.get("filmOverlayDrawHitNormals"),
        "film_overlay_draw_film_gradient_normals": overlay_diag.get("filmOverlayDrawFilmGradientNormals"),
        "comparison_grid_enabled": overlay_diag.get("comparisonGrid"),
        "comparison_crosshair_enabled": overlay_diag.get("comparisonCrosshair"),
        "overlay_ray_count": overlay_diag.get("overlayRayCount"),
        "overlay_point_count": overlay_diag.get("overlayPointCount"),
        "overlay_bus_items": overlay_diag.get("overlayBusItems"),
        "overlay_bus_lines": overlay_diag.get("overlayBusLines"),
        "overlay_bus_texts": overlay_diag.get("overlayBusTexts"),
        "film_debug_ray_count": overlay_diag.get("filmDebugRayCount"),
        "film_debug_point_count": overlay_diag.get("filmDebugPointCount"),
        "film_debug_ray_cap": overlay_diag.get("filmDebugRayCap"),
        "analysis_bright_row_count": white_streak_diag.get("analysisBrightRowCount"),
        "analysis_longest_bright_run": white_streak_diag.get("analysisLongestRun"),
        "analysis_rendered_bright_row_count": white_streak_diag.get("analysisRenderedBrightRowCount"),
        "analysis_rendered_bright_group_count": white_streak_diag.get("analysisRenderedBrightGroupCount"),
        "analysis_rendered_bright_first_row": white_streak_diag.get("analysisRenderedBrightFirstRow"),
        "analysis_rendered_bright_last_row": white_streak_diag.get("analysisRenderedBrightLastRow"),
        "analysis_rendered_longest_bright_run": white_streak_diag.get("analysisRenderedLongestRun"),
        "analysis_unrendered_bright_row_count": white_streak_diag.get("analysisUnrenderedBrightRowCount"),
        "analysis_unrendered_bright_group_count": white_streak_diag.get("analysisUnrenderedBrightGroupCount"),
        "debug_bright_row_count": white_streak_diag.get("debugBrightRowCount"),
        "debug_longest_bright_run": white_streak_diag.get("debugLongestRun"),
        "render_health_step_for_streaks": white_streak_diag.get("renderHealthStep"),
        "render_health_traced_pixels": white_streak_diag.get("renderHealthTracedPixels"),
        "render_health_geom_segments_queried": white_streak_diag.get("renderHealthGeomSegmentsQueried"),
        "render_health_geom_ray_tests_total": white_streak_diag.get("renderHealthGeomRayTestsTotal"),
        "render_health_pass2_sampled_segments": white_streak_diag.get("renderHealthPass2SampledSegments"),
        "render_health_avg_steps_per_traced_pixel": white_streak_diag.get("renderHealthAvgStepsPerTracedPixel"),
        "render_health_exit_reason": white_streak_diag.get("renderHealthExitReason"),
        "final_hit_only_analysis": write_diag.get("finalHitOnlyAnalysis"),
        "rows_started": write_diag.get("rowsStarted"),
        "rows_completed": write_diag.get("rowsCompleted"),
        "rows_partially_written": write_diag.get("rowsPartiallyWritten"),
        "rows_early_terminated": write_diag.get("rowsEarlyTerminated"),
        "final_hit_pixel_count": write_diag.get("finalHitPixelCount"),
        "traversal_write_pixel_count": write_diag.get("traversalWritePixelCount"),
        "white_streak_likely_source": white_streak_diag.get("likelySource"),
        "analysis_bottom_band_present": bottom_region_diag.get("analysisBottomBandPresent"),
        "analysis_bottom_band_start": bottom_region_diag.get("analysisBandStart"),
        "analysis_bottom_band_height": bottom_region_diag.get("analysisBandHeight"),
        "analysis_rendered_rows": bottom_region_diag.get("analysisRenderedRows"),
        "analysis_unrendered_rows": bottom_region_diag.get("analysisUnrenderedRows"),
        "analysis_band_matches_unrendered_rows": bottom_region_diag.get("analysisBandMatchesUnrenderedRows"),
        "analysis_band_matches_sky_color": bottom_region_diag.get("analysisBandMatchesSkyColor"),
        "debug_bottom_band_present": bottom_region_diag.get("debugBottomBandPresent"),
        "debug_bottom_band_start": bottom_region_diag.get("debugBandStart"),
        "debug_bottom_band_height": bottom_region_diag.get("debugBandHeight"),
        "debug_expected_bottom_band_start": bottom_region_diag.get("debugExpectedBandStart"),
        "debug_expected_bottom_band_height": bottom_region_diag.get("debugExpectedBandHeight"),
        "debug_bottom_band_start_delta": bottom_region_diag.get("debugBandStartDelta"),
        "debug_bottom_band_height_delta": bottom_region_diag.get("debugBandHeightDelta"),
        "bottom_region_likely_cause": bottom_region_diag.get("likelyCause"),
        "launch_audit": {
            "requested_launcher": launch.get("requested_launcher"),
            "actual_scene": launch.get("actual_scene"),
            "actual_fixture": launch.get("actual_fixture"),
            "expected_scene": launch.get("expected_scene"),
            "expected_fixture": launch.get("expected_fixture"),
            "scene_match": launch.get("scene_match"),
            "fixture_match": launch.get("fixture_match"),
            "status": launch.get("status"),
        },
    }


def values_match_with_tolerance(requested_value, effective_value) -> bool:
    if not isinstance(requested_value, (int, float)) or not isinstance(effective_value, (int, float)):
        return False
    if not math.isfinite(requested_value) or not math.isfinite(effective_value):
        return False
    tolerance = max(1e-6, abs(float(requested_value)) * 1e-4)
    return math.isclose(float(requested_value), float(effective_value), rel_tol=1e-4, abs_tol=tolerance)


def build_verification(metrics: dict, params: dict) -> dict:
    runtime_fingerprint_present = bool(metrics.get("runtime_fingerprint"))
    assembly_timestamp_present = bool(metrics.get("runtime_assembly_write_utc"))
    effective_step_matches_requested = values_match_with_tolerance(
        params.get("requested_step_length"),
        metrics.get("effective_step_length"),
    )
    row_diagnostics_present = any(
        metrics.get(field) is not None
        for field in (
            "total_rows_considered",
            "total_rows_processed",
            "total_rows_skipped",
            "processed_row_start",
            "processed_row_end",
            "zero_hit_rows",
            "row_participation_summary",
        )
    )
    processed_rows = metrics.get("processed_rows")
    traced_pixels = metrics.get("traced_pixels")
    scheduler_clean = (
        metrics.get("status") == "ok"
        and metrics.get("capture_succeeded") is True
        and metrics.get("launch_audit_status") == "ok"
        and metrics.get("guard_progress") == 0
        and metrics.get("forced_advance") == 0
        and isinstance(processed_rows, (int, float))
        and processed_rows >= 164
        and isinstance(traced_pixels, (int, float))
        and traced_pixels > 0
    )
    run_verified = all(
        (
            runtime_fingerprint_present,
            assembly_timestamp_present,
            effective_step_matches_requested,
            row_diagnostics_present,
            scheduler_clean,
        )
    )
    return {
        "runtime_fingerprint_present": runtime_fingerprint_present,
        "assembly_timestamp_present": assembly_timestamp_present,
        "effective_step_matches_requested": effective_step_matches_requested,
        "row_diagnostics_present": row_diagnostics_present,
        "scheduler_clean": scheduler_clean,
        "run_verified": run_verified,
    }


def parse_row_ranges(raw_value) -> list[tuple[int, int]]:
    text = normalize_token(raw_value)
    if text == "" or text == "-":
        return []
    ranges: list[tuple[int, int]] = []
    for chunk in text.split(","):
        token = chunk.strip()
        if token == "":
            continue
        if "-" in token:
            start_text, end_text = token.split("-", 1)
            start = parse_int_token(start_text)
            end = parse_int_token(end_text)
            if start is None or end is None:
                continue
            if end < start:
                start, end = end, start
            ranges.append((start, end))
            continue
        value = parse_int_token(token)
        if value is not None:
            ranges.append((value, value))
    return ranges


def normalize_token(value) -> str:
    if value is None:
        return ""
    return str(value).strip()


def parse_int_token(value) -> int | None:
    try:
        return int(str(value).strip())
    except (TypeError, ValueError):
        return None


def build_row_coverage_artifact(metrics: dict) -> dict:
    total_rows = parse_int_token(metrics.get("total_rows_considered"))
    if total_rows is None or total_rows <= 0:
        return {
            "total_rows_considered": metrics.get("total_rows_considered"),
            "total_rows_processed": metrics.get("total_rows_processed"),
            "zero_hit_rows": metrics.get("zero_hit_rows"),
            "coverage_visual": "",
            "coverage_legend": "P=processed_with_hits Z=processed_zero_hits S=skipped .=unseen",
        }

    processed = [False] * total_rows
    skipped = [False] * total_rows
    zero_hit = [False] * total_rows

    for start, end in parse_row_ranges(metrics.get("processed_row_ranges")):
        for index in range(max(0, start), min(total_rows - 1, end) + 1):
            processed[index] = True
    for start, end in parse_row_ranges(metrics.get("skipped_row_ranges")):
        for index in range(max(0, start), min(total_rows - 1, end) + 1):
            skipped[index] = True
    for start, end in parse_row_ranges(metrics.get("zero_hit_row_ranges")):
        for index in range(max(0, start), min(total_rows - 1, end) + 1):
            zero_hit[index] = True

    chars: list[str] = []
    for index in range(total_rows):
        if zero_hit[index]:
            chars.append("Z")
        elif processed[index]:
            chars.append("P")
        elif skipped[index]:
            chars.append("S")
        else:
            chars.append(".")

    return {
        "total_rows_considered": metrics.get("total_rows_considered"),
        "total_rows_processed": metrics.get("total_rows_processed"),
        "zero_hit_rows": metrics.get("zero_hit_rows"),
        "coverage_visual": "".join(chars),
        "coverage_legend": "P=processed_with_hits Z=processed_zero_hits S=skipped .=unseen",
    }


def repo_root_path() -> Path:
    return Path(__file__).resolve().parent.parent


IDENTITY_BASIS = (
    (1.0, 0.0, 0.0),
    (0.0, 1.0, 0.0),
    (0.0, 0.0, 1.0),
)
IDENTITY_TRANSFORM = {
    "basis": IDENTITY_BASIS,
    "origin": (0.0, 0.0, 0.0),
}
SCENE_PARSE_CACHE: dict[Path, dict] = {}
SCENE_PROP_KEYS = {
    "transform",
    "current",
    "fov",
    "FieldPath",
    "script",
}
FLOAT_TOKEN_RE = re.compile(r"[-+]?(?:\d+\.\d+|\d+\.?|\.\d+)(?:[eE][-+]?\d+)?")


def resolve_res_path(path_token: str, base_path: Path | None = None) -> Path | None:
    if not path_token:
        return None
    if path_token.startswith("res://"):
        return repo_root_path() / path_token[len("res://"):]
    if base_path is None:
        return None
    return (base_path.parent / path_token).resolve()


def parse_scene_value(key: str, raw_value: str):
    value = raw_value.strip()
    if key == "transform" and value.startswith("Transform3D("):
        payload = value[len("Transform3D("):-1] if value.endswith(")") else value[len("Transform3D("):]
        numbers = []
        for token in payload.split(","):
            text = token.strip()
            if text == "":
                continue
            try:
                numbers.append(float(text))
            except ValueError:
                numbers = []
                break
        if len(numbers) == 12:
            return {
                "basis": (
                    (numbers[0], numbers[1], numbers[2]),
                    (numbers[3], numbers[4], numbers[5]),
                    (numbers[6], numbers[7], numbers[8]),
                ),
                "origin": (numbers[9], numbers[10], numbers[11]),
            }
    if value.startswith('NodePath("') and value.endswith('")'):
        return value[len('NodePath("'):-2]
    if key == "script" and value.startswith('ExtResource("') and value.endswith('")'):
        return {
            "ext_resource_id": value[len('ExtResource("'):-2],
        }
    if value in {"true", "false"}:
        return value == "true"
    number = scalar(value)
    if isinstance(number, (int, float)):
        return number
    if value.startswith('"') and value.endswith('"'):
        return value[1:-1]
    return value


def parse_node_header(line: str) -> dict | None:
    if not line.startswith("[node "):
        return None
    name_match = re.search(r'name="([^"]+)"', line)
    if not name_match:
        return None
    type_match = re.search(r'type="([^"]+)"', line)
    parent_match = re.search(r'parent="([^"]*)"', line)
    instance_match = re.search(r'instance=ExtResource\("([^"]+)"\)', line)
    name = name_match.group(1)
    parent_token = parent_match.group(1) if parent_match else None
    if parent_token is None:
        rel_path = ""
        parent_rel_path = None
    elif parent_token == ".":
        rel_path = name
        parent_rel_path = ""
    else:
        rel_path = f"{parent_token}/{name}"
        parent_rel_path = parent_token
    return {
        "name": name,
        "type": type_match.group(1) if type_match else None,
        "parent_rel_path": parent_rel_path,
        "rel_path": rel_path,
        "instance_ref": instance_match.group(1) if instance_match else None,
    }


def parse_scene_definition(scene_path: Path) -> dict:
    scene_path = scene_path.resolve()
    cached = SCENE_PARSE_CACHE.get(scene_path)
    if cached is not None:
        return cached

    text = scene_path.read_text(encoding="utf-8", errors="replace")
    ext_resources: dict[str, str] = {}
    nodes: dict[str, dict] = {}
    current_node: dict | None = None

    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("[ext_resource "):
            path_match = re.search(r'path="([^"]+)"', stripped)
            id_match = re.search(r'id="([^"]+)"', stripped)
            if path_match and id_match:
                ext_resources[id_match.group(1)] = path_match.group(1)
            continue

        node_header = parse_node_header(stripped)
        if node_header is not None:
            current_node = {
                "name": node_header["name"],
                "type": node_header["type"],
                "parent_rel_path": node_header["parent_rel_path"],
                "instance_ref": node_header["instance_ref"],
                "properties": {},
            }
            nodes[node_header["rel_path"]] = current_node
            continue

        if current_node is None or " = " not in stripped:
            continue

        key, raw_value = stripped.split(" = ", 1)
        key = key.strip()
        if key not in SCENE_PROP_KEYS:
            continue
        current_node["properties"][key] = parse_scene_value(key, raw_value)

    children: dict[str | None, list[str]] = {}
    for rel_path, node in nodes.items():
        children.setdefault(node["parent_rel_path"], []).append(rel_path)

    parsed = {
        "scene_path": scene_path,
        "ext_resources": ext_resources,
        "nodes": nodes,
        "children": children,
    }
    SCENE_PARSE_CACHE[scene_path] = parsed
    return parsed


def join_rel_path(prefix: str, suffix: str) -> str:
    if prefix == "":
        return suffix
    if suffix == "":
        return prefix
    return f"{prefix}/{suffix}"


def mat3_mul(a: tuple[tuple[float, float, float], ...], b: tuple[tuple[float, float, float], ...]) -> tuple[tuple[float, float, float], ...]:
    rows = []
    for row in range(3):
        rows.append(
            tuple(
                sum(a[row][k] * b[k][col] for k in range(3))
                for col in range(3)
            )
        )
    return tuple(rows)


def mat3_vec_mul(matrix: tuple[tuple[float, float, float], ...], vector: tuple[float, float, float]) -> tuple[float, float, float]:
    return tuple(sum(matrix[row][col] * vector[col] for col in range(3)) for row in range(3))


def vec3_add(a: tuple[float, float, float], b: tuple[float, float, float]) -> tuple[float, float, float]:
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def vec3_sub(a: tuple[float, float, float], b: tuple[float, float, float]) -> tuple[float, float, float]:
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def compose_transforms(parent_transform: dict, local_transform: dict) -> dict:
    parent_basis = parent_transform["basis"]
    local_basis = local_transform["basis"]
    parent_origin = parent_transform["origin"]
    local_origin = local_transform["origin"]
    return {
        "basis": mat3_mul(parent_basis, local_basis),
        "origin": vec3_add(mat3_vec_mul(parent_basis, local_origin), parent_origin),
    }


def invert_mat3(matrix: tuple[tuple[float, float, float], ...]) -> tuple[tuple[float, float, float], ...] | None:
    a, b, c = matrix[0]
    d, e, f = matrix[1]
    g, h, i = matrix[2]
    det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g)
    if math.isclose(det, 0.0, abs_tol=1e-9):
        return None
    inv_det = 1.0 / det
    return (
        ((e * i - f * h) * inv_det, (c * h - b * i) * inv_det, (b * f - c * e) * inv_det),
        ((f * g - d * i) * inv_det, (a * i - c * g) * inv_det, (c * d - a * f) * inv_det),
        ((d * h - e * g) * inv_det, (b * g - a * h) * inv_det, (a * e - b * d) * inv_det),
    )


def transform_point_to_local(transform: dict, world_point: tuple[float, float, float]) -> tuple[float, float, float] | None:
    inverse_basis = invert_mat3(transform["basis"])
    if inverse_basis is None:
        return None
    return mat3_vec_mul(inverse_basis, vec3_sub(world_point, transform["origin"]))


def flatten_scene_tree(scene_path: Path) -> dict[str, dict]:
    flat_nodes: dict[str, dict] = {}

    def resolve_script_path(parsed_scene: dict, node: dict) -> str:
        script_value = (node.get("properties") or {}).get("script")
        if isinstance(script_value, dict):
            ext_resource_id = normalize_token(script_value.get("ext_resource_id"))
            if ext_resource_id != "":
                return normalize_token(parsed_scene["ext_resources"].get(ext_resource_id))
        return normalize_token(script_value)

    def mount_scene(parsed_scene: dict, mount_prefix: str, parent_transform: dict) -> None:
        root_node = parsed_scene["nodes"].get("")
        if root_node is None:
            return
        root_transform = root_node["properties"].get("transform") or IDENTITY_TRANSFORM
        root_global = compose_transforms(parent_transform, root_transform)
        flat_nodes[mount_prefix] = {
            "type": root_node.get("type"),
            "properties": dict(root_node.get("properties") or {}),
            "script_path": resolve_script_path(parsed_scene, root_node),
            "global_transform": root_global,
            "scene_path": str(parsed_scene["scene_path"]),
        }
        for child_rel_path in parsed_scene["children"].get("", []):
            mount_node(parsed_scene, child_rel_path, mount_prefix, root_global)

    def mount_node(parsed_scene: dict, rel_path: str, mount_prefix: str, parent_transform: dict) -> None:
        node = parsed_scene["nodes"][rel_path]
        local_transform = node["properties"].get("transform") or IDENTITY_TRANSFORM
        node_global = compose_transforms(parent_transform, local_transform)
        mounted_rel_path = join_rel_path(mount_prefix, rel_path)
        instance_ref = node.get("instance_ref")
        if instance_ref:
            resource_token = parsed_scene["ext_resources"].get(instance_ref)
            instance_path = resolve_res_path(resource_token or "", parsed_scene["scene_path"])
            if instance_path is not None and instance_path.exists():
                mount_scene(parse_scene_definition(instance_path), mounted_rel_path, node_global)
            return

        flat_nodes[mounted_rel_path] = {
            "type": node.get("type"),
            "properties": dict(node.get("properties") or {}),
            "script_path": resolve_script_path(parsed_scene, node),
            "global_transform": node_global,
            "scene_path": str(parsed_scene["scene_path"]),
        }
        for child_rel_path in parsed_scene["children"].get(rel_path, []):
            mount_node(parsed_scene, child_rel_path, mount_prefix, node_global)

    mount_scene(parse_scene_definition(scene_path), "", IDENTITY_TRANSFORM)
    return flat_nodes


def project_world_point_to_image(
    world_point: tuple[float, float, float],
    camera_transform: dict,
    fov_degrees: float,
    image_width: int,
    image_height: int,
) -> dict | None:
    if image_width <= 0 or image_height <= 0:
        return None
    if not isinstance(fov_degrees, (int, float)) or not math.isfinite(float(fov_degrees)) or float(fov_degrees) <= 0.0:
        return None
    camera_point = transform_point_to_local(camera_transform, world_point)
    if camera_point is None:
        return None
    x_cam, y_cam, z_cam = camera_point
    if z_cam >= -1e-6:
        return None
    cx = (image_width - 1) / 2.0
    cy = (image_height - 1) / 2.0
    focal_length = cy / math.tan(math.radians(float(fov_degrees)) / 2.0)
    if not math.isfinite(focal_length):
        return None
    return {
        "x": cx + focal_length * (x_cam / -z_cam),
        "y": cy - focal_length * (y_cam / -z_cam),
        "camera_space_point": {
            "x": round(x_cam, 6),
            "y": round(y_cam, 6),
            "z": round(z_cam, 6),
        },
    }


def resolve_scene_camera(flat_nodes: dict[str, dict]) -> tuple[str | None, dict | None]:
    camera_path = None
    camera_node = None
    for rel_path, node in flat_nodes.items():
        if node.get("type") == "Camera3D" and (node.get("properties") or {}).get("current") is True:
            camera_path = rel_path
            camera_node = node
            break
    if camera_node is None:
        for rel_path, node in flat_nodes.items():
            if node.get("type") == "Camera3D":
                camera_path = rel_path
                camera_node = node
                break
    return camera_path, camera_node


def path_is_within(root_path: str, candidate_path: str) -> bool:
    normalized_root = normalize_token(root_path)
    normalized_candidate = normalize_token(candidate_path)
    if normalized_root == "":
        return normalized_candidate != ""
    return normalized_candidate == normalized_root or normalized_candidate.startswith(normalized_root + "/")


def is_field_source_node(node: dict) -> bool:
    script_path = normalize_token(node.get("script_path"))
    return script_path.endswith("FieldSource3D.cs")


def resolve_fixture_field_analysis(args: argparse.Namespace, metrics: dict) -> dict:
    scene_path = resolve_res_path(args.scene)
    if scene_path is None or not scene_path.exists():
        return {
            "resolved": False,
            "reason": f"scene_not_found:{args.scene}",
        }

    flat_nodes = flatten_scene_tree(scene_path)
    root_node = flat_nodes.get("") or {}
    root_properties = root_node.get("properties") or {}
    primary_field_path = normalize_token(root_properties.get("FieldPath"))
    if primary_field_path == "":
        return {
            "resolved": False,
            "reason": "field_path_missing",
            "scene_path": str(scene_path),
        }

    primary_field_node = flat_nodes.get(primary_field_path)
    if primary_field_node is None:
        return {
            "resolved": False,
            "reason": f"field_node_not_found:{primary_field_path}",
            "scene_path": str(scene_path),
        }

    fixture_root_path = primary_field_path.rsplit("/", 1)[0] if "/" in primary_field_path else ""
    camera_path, camera_node = resolve_scene_camera(flat_nodes)
    if camera_node is None:
        return {
            "resolved": False,
            "reason": "camera_not_found",
            "scene_path": str(scene_path),
            "field_path": primary_field_path,
        }

    image_width = parse_int_token(metrics.get("image_width"))
    image_height = parse_int_token(metrics.get("image_height"))
    fov = (camera_node.get("properties") or {}).get("fov")
    camera_transform = (flat_nodes.get(camera_path) or {}).get("global_transform") or IDENTITY_TRANSFORM
    attractors: list[dict] = []
    projection_failures: list[dict] = []

    for rel_path, node in flat_nodes.items():
        if not path_is_within(fixture_root_path, rel_path):
            continue
        if not is_field_source_node(node):
            continue

        field_transform = node.get("global_transform") or IDENTITY_TRANSFORM
        world_origin = field_transform["origin"]
        projection = project_world_point_to_image(world_origin, camera_transform, fov or 75.0, image_width or 0, image_height or 0)
        if projection is None:
            projection_failures.append(
                {
                    "field_path": rel_path,
                    "field_world_origin": {
                        "x": round(world_origin[0], 6),
                        "y": round(world_origin[1], 6),
                        "z": round(world_origin[2], 6),
                    },
                }
            )
            continue

        attractors.append(
            {
                "field_path": rel_path,
                "field_world_origin": {
                    "x": round(world_origin[0], 6),
                    "y": round(world_origin[1], 6),
                    "z": round(world_origin[2], 6),
                },
                "field_center_pixel": {
                    "x": round(projection["x"], 4),
                    "y": round(projection["y"], 4),
                },
                "camera_space_point": projection.get("camera_space_point") or {},
                "is_primary_field": rel_path == primary_field_path,
            }
        )

    if not attractors:
        return {
            "resolved": False,
            "reason": "field_projection_failed",
            "scene_path": str(scene_path),
            "field_path": primary_field_path,
            "fixture_root_path": fixture_root_path,
            "camera_path": camera_path,
            "projection_failures": projection_failures,
        }

    attractors.sort(key=lambda entry: (not entry.get("is_primary_field", False), entry.get("field_path") or ""))
    return {
        "resolved": True,
        "scene_path": str(scene_path),
        "field_path": primary_field_path,
        "fixture_root_path": fixture_root_path,
        "camera_path": camera_path,
        "field_world_origin": attractors[0].get("field_world_origin") or {},
        "field_center_pixel": attractors[0].get("field_center_pixel") or {},
        "camera_fov_degrees": fov,
        "camera_space_point": attractors[0].get("camera_space_point") or {},
        "projection_basis": "scene_camera_perspective",
        "attractor_count": len(attractors),
        "attractors": attractors,
        "projection_failures": projection_failures,
    }


def resolve_field_analysis_origin(args: argparse.Namespace, metrics: dict) -> dict:
    fixture_analysis = resolve_fixture_field_analysis(args, metrics)
    if not fixture_analysis.get("resolved"):
        return fixture_analysis
    return {
        "resolved": True,
        "scene_path": fixture_analysis.get("scene_path"),
        "field_path": fixture_analysis.get("field_path"),
        "camera_path": fixture_analysis.get("camera_path"),
        "field_world_origin": fixture_analysis.get("field_world_origin") or {},
        "field_center_pixel": fixture_analysis.get("field_center_pixel") or {},
        "camera_fov_degrees": fixture_analysis.get("camera_fov_degrees"),
        "camera_space_point": fixture_analysis.get("camera_space_point") or {},
        "projection_basis": fixture_analysis.get("projection_basis"),
    }


def clamp_byte(value: float) -> int:
    return max(0, min(255, int(round(value * 255.0))))


FINAL_HIT_RGBA = (
    clamp_byte(1.0),
    clamp_byte(0.82),
    clamp_byte(0.18),
    clamp_byte(1.0),
)
RENDERED_NO_HIT_RGBA = (
    clamp_byte(0.07),
    clamp_byte(0.09),
    clamp_byte(0.18),
    clamp_byte(1.0),
)
UNRENDERED_RGBA = (0, 0, 0, 255)


def rgba_matches(pixel: tuple[int, int, int, int], expected: tuple[int, int, int, int], tolerance: int = 2) -> bool:
    return all(abs(int(actual) - int(target)) <= tolerance for actual, target in zip(pixel, expected))


def classify_categorical_pixel(pixel: tuple[int, int, int, int]) -> str:
    if rgba_matches(pixel, FINAL_HIT_RGBA):
        return "final_hit"
    if rgba_matches(pixel, RENDERED_NO_HIT_RGBA):
        return "rendered_no_hit"
    if rgba_matches(pixel, UNRENDERED_RGBA):
        return "unrendered"
    return "other"


COUNT_KEYS = (
    "pixel_count",
    "final_hit_pixel_count",
    "rendered_no_hit_pixel_count",
    "unrendered_pixel_count",
    "other_pixel_count",
)


def build_disabled_spatial_artifact(capture_mode, reason: str) -> dict:
    return {
        "enabled": False,
        "reason": reason,
        "analysis_capture_mode": capture_mode,
    }


def build_count_bucket() -> dict:
    return {
        "pixel_count": 0,
        "final_hit_pixel_count": 0,
        "rendered_no_hit_pixel_count": 0,
        "unrendered_pixel_count": 0,
        "other_pixel_count": 0,
    }


def accumulate_bucket_counts(target: dict, source: dict) -> None:
    for key in COUNT_KEYS:
        target[key] += source.get(key, 0)


def add_category_to_bucket(bucket: dict, category: str) -> None:
    bucket["pixel_count"] += 1
    if category == "final_hit":
        bucket["final_hit_pixel_count"] += 1
    elif category == "rendered_no_hit":
        bucket["rendered_no_hit_pixel_count"] += 1
    elif category == "unrendered":
        bucket["unrendered_pixel_count"] += 1
    else:
        bucket["other_pixel_count"] += 1


def finalize_radial_bucket(bucket: dict) -> None:
    rendered_pixels = bucket["final_hit_pixel_count"] + bucket["rendered_no_hit_pixel_count"]
    bucket["rendered_pixel_count"] = rendered_pixels
    bucket["hit_fraction_within_rendered_area"] = (
        round(bucket["final_hit_pixel_count"] / rendered_pixels, 6) if rendered_pixels > 0 else None
    )
    bucket["rendered_coverage_fraction"] = (
        round(rendered_pixels / bucket["pixel_count"], 6) if bucket["pixel_count"] > 0 else None
    )


def finalize_sector_bucket(bucket: dict) -> None:
    rendered_pixels = bucket["final_hit_pixel_count"] + bucket["rendered_no_hit_pixel_count"]
    bucket["rendered_pixel_count"] = rendered_pixels
    bucket["hit_frac_rendered"] = round(bucket["final_hit_pixel_count"] / rendered_pixels, 6) if rendered_pixels > 0 else None
    bucket["rendered_coverage"] = round(rendered_pixels / bucket["pixel_count"], 6) if bucket["pixel_count"] > 0 else None
    bucket["hit_fraction_within_rendered_area"] = bucket["hit_frac_rendered"]
    bucket["rendered_coverage_fraction"] = bucket["rendered_coverage"]


NEAREST_ATTRACTOR_AMBIGUOUS_MARGIN_FRACTION = 0.15
NEAREST_ATTRACTOR_DOMINANT_SHARE_THRESHOLD = 0.65
NEAREST_ATTRACTOR_SPLIT_SHARE_THRESHOLD = 0.35
NEAREST_ATTRACTOR_CLEAN_PARTITION_THRESHOLD = 0.70


def build_profile_artifacts(
    args: argparse.Namespace,
    metrics: dict,
    origin_x: float,
    origin_y: float,
    profile_label: str,
    origin_key: str,
    origin_label: str,
    sector_axis_label: str,
    sector_left_rule: str,
    sector_right_rule: str,
    extra_metadata: dict | None = None,
) -> tuple[dict, dict]:
    capture_mode = metrics.get("analysis_capture_mode")
    if capture_mode != "categorical_final":
        disabled = build_disabled_spatial_artifact(capture_mode, f"analysis_capture_mode_not_supported:{capture_mode}")
        return disabled, disabled.copy()

    if args.radial_bin_count <= 0:
        disabled = build_disabled_spatial_artifact(capture_mode, f"invalid_radial_bin_count:{args.radial_bin_count}")
        return disabled, disabled.copy()

    with Image.open(args.capture_path) as image:
        rgba = image.convert("RGBA")
        width, height = rgba.size
        pixels = rgba.load()

        corner_points = (
            (0.0, 0.0),
            (0.0, height - 1.0),
            (width - 1.0, 0.0),
            (width - 1.0, height - 1.0),
        )
        max_radius = max(math.hypot(corner_x - origin_x, corner_y - origin_y) for corner_x, corner_y in corner_points)
        if max_radius <= 0.0:
            max_radius = 1.0

        bins: list[dict] = []
        sector_bins: list[dict] = []
        for index in range(args.radial_bin_count):
            radius_start = (index / args.radial_bin_count) * max_radius
            radius_end = ((index + 1) / args.radial_bin_count) * max_radius
            bins.append(
                {
                    "bin_index": index,
                    "radius_start_px": round(radius_start, 4),
                    "radius_end_px": round(radius_end, 4),
                    **build_count_bucket(),
                }
            )
            sector_bins.append(
                {
                    "bin_index": index,
                    "radius_start_px": round(radius_start, 4),
                    "radius_end_px": round(radius_end, 4),
                    "sectors": {
                        "left": build_count_bucket(),
                        "right": build_count_bucket(),
                    },
                }
            )

        for y in range(height):
            for x in range(width):
                radius = math.hypot(x - origin_x, y - origin_y)
                normalized = min(radius / max_radius, 0.999999999)
                bin_index = min(int(normalized * args.radial_bin_count), args.radial_bin_count - 1)
                category = classify_categorical_pixel(tuple(int(channel) for channel in pixels[x, y]))
                add_category_to_bucket(bins[bin_index], category)
                sector_name = "left" if x <= origin_x else "right"
                add_category_to_bucket(sector_bins[bin_index]["sectors"][sector_name], category)

        totals = build_count_bucket()
        for bucket in bins:
            finalize_radial_bucket(bucket)
            accumulate_bucket_counts(totals, bucket)
        finalize_radial_bucket(totals)

        sector_totals = {
            "left": build_count_bucket(),
            "right": build_count_bucket(),
        }
        sector_combined_totals = build_count_bucket()
        for bucket in sector_bins:
            combined = build_count_bucket()
            for sector_name in ("left", "right"):
                sector_bucket = bucket["sectors"][sector_name]
                finalize_sector_bucket(sector_bucket)
                accumulate_bucket_counts(combined, sector_bucket)
                accumulate_bucket_counts(sector_totals[sector_name], sector_bucket)
            finalize_sector_bucket(combined)
            bucket["combined"] = combined
            left_bucket = bucket["sectors"]["left"]
            right_bucket = bucket["sectors"]["right"]
            bucket["left_minus_right"] = {
                "final_hit_pixel_count": left_bucket["final_hit_pixel_count"] - right_bucket["final_hit_pixel_count"],
                "rendered_pixel_count": left_bucket["rendered_pixel_count"] - right_bucket["rendered_pixel_count"],
                "hit_frac_rendered": (
                    round(left_bucket["hit_frac_rendered"] - right_bucket["hit_frac_rendered"], 6)
                    if left_bucket["hit_frac_rendered"] is not None and right_bucket["hit_frac_rendered"] is not None
                    else None
                ),
                "rendered_coverage": (
                    round(left_bucket["rendered_coverage"] - right_bucket["rendered_coverage"], 6)
                    if left_bucket["rendered_coverage"] is not None and right_bucket["rendered_coverage"] is not None
                    else None
                ),
            }
            accumulate_bucket_counts(sector_combined_totals, combined)

        for sector_name in ("left", "right"):
            finalize_sector_bucket(sector_totals[sector_name])
        finalize_sector_bucket(sector_combined_totals)

        base_metadata = {
            "enabled": True,
            "analysis_capture_mode": capture_mode,
            "capture_path": str(args.capture_path),
            "bin_count": args.radial_bin_count,
            "profile_label": profile_label,
            origin_key: {
                "x": round(origin_x, 4),
                "y": round(origin_y, 4),
            },
            "origin_label": origin_label,
            "max_radius_px": round(max_radius, 4),
            "categorical_palette": {
                "final_hit_rgba": list(FINAL_HIT_RGBA),
                "rendered_no_hit_rgba": list(RENDERED_NO_HIT_RGBA),
                "unrendered_rgba": list(UNRENDERED_RGBA),
            },
        }
        if extra_metadata:
            base_metadata.update(extra_metadata)

        radial_profile = dict(base_metadata)
        radial_profile["bins"] = bins
        radial_profile["totals"] = totals

        radial_sector_profile = dict(base_metadata)
        radial_sector_profile["sector_definition"] = {
            "axis": sector_axis_label,
            "left_rule": sector_left_rule,
            "right_rule": sector_right_rule,
        }
        radial_sector_profile["bins"] = sector_bins
        radial_sector_profile["totals"] = {
            "sectors": sector_totals,
            "combined": sector_combined_totals,
        }
        return radial_profile, radial_sector_profile


def build_spatial_profiles(args: argparse.Namespace, metrics: dict) -> tuple[dict, dict]:
    with Image.open(args.capture_path) as image:
        width, height = image.size
    center_x = (width - 1) / 2.0
    center_y = (height - 1) / 2.0
    return build_profile_artifacts(
        args,
        metrics,
        center_x,
        center_y,
        profile_label="image_center",
        origin_key="center_pixel",
        origin_label="Center Pixel",
        sector_axis_label="image_center_x",
        sector_left_rule="x <= center_x",
        sector_right_rule="x > center_x",
    )


def build_field_spatial_profiles(args: argparse.Namespace, metrics: dict) -> tuple[dict, dict]:
    capture_mode = metrics.get("analysis_capture_mode")
    field_origin = resolve_field_analysis_origin(args, metrics)
    if not field_origin.get("resolved"):
        disabled = build_disabled_spatial_artifact(
            capture_mode,
            field_origin.get("reason") or "field_origin_unresolved",
        )
        disabled["field_analysis_origin"] = field_origin
        return disabled, disabled.copy()

    field_center = field_origin["field_center_pixel"]
    extra_metadata = {
        "field_analysis_origin": field_origin,
        "field_world_origin": field_origin.get("field_world_origin"),
    }
    return build_profile_artifacts(
        args,
        metrics,
        float(field_center["x"]),
        float(field_center["y"]),
        profile_label="field_relative",
        origin_key="field_center_pixel",
        origin_label="Field Center Pixel",
        sector_axis_label="field_center_x",
        sector_left_rule="x <= field_center_x",
        sector_right_rule="x > field_center_x",
        extra_metadata=extra_metadata,
    )


def classify_nearest_attractor_behavior(partition_summary: dict) -> str:
    dominant_share = partition_summary.get("dominant_final_hit_share")
    second_share = partition_summary.get("second_final_hit_share")
    clean_fraction = partition_summary.get("clean_partition_fraction")
    if dominant_share is None:
        return "blended"
    if dominant_share >= NEAREST_ATTRACTOR_DOMINANT_SHARE_THRESHOLD:
        return "dominant"
    if (
        second_share is not None
        and second_share >= NEAREST_ATTRACTOR_SPLIT_SHARE_THRESHOLD
        and clean_fraction is not None
        and clean_fraction >= NEAREST_ATTRACTOR_CLEAN_PARTITION_THRESHOLD
    ):
        return "split"
    return "blended"


def build_nearest_attractor_profile(args: argparse.Namespace, metrics: dict) -> dict:
    capture_mode = metrics.get("analysis_capture_mode")
    if capture_mode != "categorical_final":
        return build_disabled_spatial_artifact(capture_mode, f"analysis_capture_mode_not_supported:{capture_mode}")
    if args.radial_bin_count <= 0:
        return build_disabled_spatial_artifact(capture_mode, f"invalid_radial_bin_count:{args.radial_bin_count}")

    fixture_analysis = resolve_fixture_field_analysis(args, metrics)
    if not fixture_analysis.get("resolved"):
        disabled = build_disabled_spatial_artifact(
            capture_mode,
            fixture_analysis.get("reason") or "fixture_field_analysis_unresolved",
        )
        disabled["fixture_field_analysis"] = fixture_analysis
        return disabled

    attractor_origins = fixture_analysis.get("attractors") or []
    if len(attractor_origins) == 0:
        disabled = build_disabled_spatial_artifact(capture_mode, "attractor_count_zero")
        disabled["fixture_field_analysis"] = fixture_analysis
        return disabled

    with Image.open(args.capture_path) as image:
        rgba = image.convert("RGBA")
        width, height = rgba.size
        pixels = rgba.load()

        attractors: list[dict] = []
        for index, origin in enumerate(attractor_origins):
            center = origin.get("field_center_pixel") or {}
            center_x = float(center.get("x", 0.0))
            center_y = float(center.get("y", 0.0))
            corner_points = (
                (0.0, 0.0),
                (0.0, height - 1.0),
                (width - 1.0, 0.0),
                (width - 1.0, height - 1.0),
            )
            max_radius = max(math.hypot(corner_x - center_x, corner_y - center_y) for corner_x, corner_y in corner_points)
            if max_radius <= 0.0:
                max_radius = 1.0

            bins: list[dict] = []
            sector_bins: list[dict] = []
            for bin_index in range(args.radial_bin_count):
                radius_start = (bin_index / args.radial_bin_count) * max_radius
                radius_end = ((bin_index + 1) / args.radial_bin_count) * max_radius
                bins.append(
                    {
                        "bin_index": bin_index,
                        "radius_start_px": round(radius_start, 4),
                        "radius_end_px": round(radius_end, 4),
                        **build_count_bucket(),
                    }
                )
                sector_bins.append(
                    {
                        "bin_index": bin_index,
                        "radius_start_px": round(radius_start, 4),
                        "radius_end_px": round(radius_end, 4),
                        "sectors": {
                            "left": build_count_bucket(),
                            "right": build_count_bucket(),
                        },
                    }
                )

            attractors.append(
                {
                    "attractor_index": index,
                    "field_path": origin.get("field_path"),
                    "field_world_origin": origin.get("field_world_origin") or {},
                    "field_center_pixel": {
                        "x": round(center_x, 4),
                        "y": round(center_y, 4),
                    },
                    "camera_space_point": origin.get("camera_space_point") or {},
                    "is_primary_field": bool(origin.get("is_primary_field")),
                    "max_radius_px": round(max_radius, 4),
                    "bins": bins,
                    "sector_bins": sector_bins,
                }
            )

        total_final_hit_pixels = 0
        ambiguous_final_hit_pixels = 0
        cleanly_partitioned_final_hit_pixels = 0

        for y in range(height):
            for x in range(width):
                distances: list[tuple[float, int]] = []
                for attractor in attractors:
                    center = attractor["field_center_pixel"]
                    distance = math.hypot(x - float(center["x"]), y - float(center["y"]))
                    distances.append((distance, int(attractor["attractor_index"])))
                distances.sort(key=lambda item: item[0])
                nearest_distance, nearest_index = distances[0]
                second_distance = distances[1][0] if len(distances) > 1 else nearest_distance
                attractor = attractors[nearest_index]
                normalized = min(nearest_distance / max(float(attractor["max_radius_px"]), 1e-9), 0.999999999)
                bin_index = min(int(normalized * args.radial_bin_count), args.radial_bin_count - 1)
                category = classify_categorical_pixel(tuple(int(channel) for channel in pixels[x, y]))
                add_category_to_bucket(attractor["bins"][bin_index], category)
                sector_name = "left" if x <= float(attractor["field_center_pixel"]["x"]) else "right"
                add_category_to_bucket(attractor["sector_bins"][bin_index]["sectors"][sector_name], category)

                if category == "final_hit":
                    total_final_hit_pixels += 1
                    normalized_margin = 1.0 if second_distance <= 1e-9 else max(0.0, second_distance - nearest_distance) / second_distance
                    if normalized_margin < NEAREST_ATTRACTOR_AMBIGUOUS_MARGIN_FRACTION:
                        ambiguous_final_hit_pixels += 1
                    else:
                        cleanly_partitioned_final_hit_pixels += 1

        for attractor in attractors:
            totals = build_count_bucket()
            for bucket in attractor["bins"]:
                finalize_radial_bucket(bucket)
                accumulate_bucket_counts(totals, bucket)
            finalize_radial_bucket(totals)
            attractor["totals"] = totals

            sector_totals = {
                "left": build_count_bucket(),
                "right": build_count_bucket(),
            }
            sector_combined_totals = build_count_bucket()
            for bucket in attractor["sector_bins"]:
                combined = build_count_bucket()
                for sector_name in ("left", "right"):
                    sector_bucket = bucket["sectors"][sector_name]
                    finalize_sector_bucket(sector_bucket)
                    accumulate_bucket_counts(combined, sector_bucket)
                    accumulate_bucket_counts(sector_totals[sector_name], sector_bucket)
                finalize_sector_bucket(combined)
                bucket["combined"] = combined
                left_bucket = bucket["sectors"]["left"]
                right_bucket = bucket["sectors"]["right"]
                bucket["left_minus_right"] = {
                    "final_hit_pixel_count": left_bucket["final_hit_pixel_count"] - right_bucket["final_hit_pixel_count"],
                    "rendered_pixel_count": left_bucket["rendered_pixel_count"] - right_bucket["rendered_pixel_count"],
                    "hit_frac_rendered": (
                        round(left_bucket["hit_frac_rendered"] - right_bucket["hit_frac_rendered"], 6)
                        if left_bucket["hit_frac_rendered"] is not None and right_bucket["hit_frac_rendered"] is not None
                        else None
                    ),
                    "rendered_coverage": (
                        round(left_bucket["rendered_coverage"] - right_bucket["rendered_coverage"], 6)
                        if left_bucket["rendered_coverage"] is not None and right_bucket["rendered_coverage"] is not None
                        else None
                    ),
                }
                accumulate_bucket_counts(sector_combined_totals, combined)

            for sector_name in ("left", "right"):
                finalize_sector_bucket(sector_totals[sector_name])
            finalize_sector_bucket(sector_combined_totals)
            attractor["sector_definition"] = {
                "axis": "nearest_attractor_center_x",
                "left_rule": "x <= attractor_center_x",
                "right_rule": "x > attractor_center_x",
            }
            attractor["sector_totals"] = {
                "sectors": sector_totals,
                "combined": sector_combined_totals,
            }
            attractor["assigned_pixel_count"] = totals.get("pixel_count")
            attractor["final_hit_share"] = (
                round(totals.get("final_hit_pixel_count", 0) / total_final_hit_pixels, 6)
                if total_final_hit_pixels > 0
                else None
            )
            attractor["rendered_share"] = (
                round(totals.get("rendered_pixel_count", 0) / width / height, 6)
                if width > 0 and height > 0
                else None
            )
            attractor["sector_bins"] = attractor["sector_bins"]

        sorted_attractors = sorted(
            attractors,
            key=lambda item: (
                -(item.get("totals") or {}).get("final_hit_pixel_count", 0),
                item.get("field_path") or "",
            ),
        )
        dominant = sorted_attractors[0] if sorted_attractors else {}
        second = sorted_attractors[1] if len(sorted_attractors) > 1 else {}
        clean_partition_fraction = (
            round(cleanly_partitioned_final_hit_pixels / total_final_hit_pixels, 6)
            if total_final_hit_pixels > 0
            else None
        )
        partition_summary = {
            "total_final_hit_pixels": total_final_hit_pixels,
            "ambiguous_final_hit_pixels": ambiguous_final_hit_pixels,
            "cleanly_partitioned_final_hit_pixels": cleanly_partitioned_final_hit_pixels,
            "clean_partition_fraction": clean_partition_fraction,
            "dominant_attractor_index": dominant.get("attractor_index"),
            "dominant_attractor_path": dominant.get("field_path"),
            "dominant_final_hit_count": (dominant.get("totals") or {}).get("final_hit_pixel_count"),
            "dominant_final_hit_share": dominant.get("final_hit_share"),
            "second_final_hit_share": second.get("final_hit_share"),
            "partition_clean": (
                clean_partition_fraction is not None
                and clean_partition_fraction >= NEAREST_ATTRACTOR_CLEAN_PARTITION_THRESHOLD
            ),
        }
        partition_summary["behavior"] = classify_nearest_attractor_behavior(partition_summary)

        return {
            "enabled": True,
            "analysis_capture_mode": capture_mode,
            "capture_path": str(args.capture_path),
            "bin_count": args.radial_bin_count,
            "profile_label": "nearest_attractor_relative",
            "assignment_basis": "nearest_projected_field_center",
            "origin_label": "Nearest Attractor Center Pixel",
            "fixture_field_analysis": fixture_analysis,
            "attractor_count": len(attractors),
            "attractors": attractors,
            "partition_summary": partition_summary,
            "categorical_palette": {
                "final_hit_rgba": list(FINAL_HIT_RGBA),
                "rendered_no_hit_rgba": list(RENDERED_NO_HIT_RGBA),
                "unrendered_rgba": list(UNRENDERED_RGBA),
            },
        }


def build_radial_profile(args: argparse.Namespace, metrics: dict) -> dict:
    radial_profile, _ = build_spatial_profiles(args, metrics)
    return radial_profile


def build_radial_sector_profile(args: argparse.Namespace, metrics: dict) -> dict:
    _, radial_sector_profile = build_spatial_profiles(args, metrics)
    return radial_sector_profile


def get_profile_origin(profile: dict) -> tuple[str, dict]:
    if "field_center_pixel" in profile:
        return profile.get("origin_label") or "Field Center Pixel", profile.get("field_center_pixel") or {}
    return profile.get("origin_label") or "Center Pixel", profile.get("center_pixel") or {}


def build_profile_text(profile: dict, title: str) -> str:
    if not profile.get("enabled"):
        reason = profile.get("reason") or "disabled"
        return f"{title}: unavailable ({reason})\n"

    origin_label, origin = get_profile_origin(profile)

    lines = [
        title,
        f"Capture Path: {profile.get('capture_path')}",
        f"Analysis Capture Mode: {profile.get('analysis_capture_mode')}",
        f"Bin Count: {profile.get('bin_count')}",
        f"{origin_label}: ({origin.get('x')}, {origin.get('y')})",
        f"Max Radius Px: {profile.get('max_radius_px')}",
        "Legend: bin radius_px pixels final_hit rendered_no_hit unrendered hit_frac_rendered rendered_coverage other",
    ]
    field_analysis_origin = profile.get("field_analysis_origin") or {}
    if field_analysis_origin.get("resolved"):
        field_world_origin = field_analysis_origin.get("field_world_origin") or {}
        lines.append(
            "Field World Origin: ({x}, {y}, {z})".format(
                x=field_world_origin.get("x"),
                y=field_world_origin.get("y"),
                z=field_world_origin.get("z"),
            )
        )
    for bucket in profile.get("bins", []):
        hit_fraction = bucket.get("hit_fraction_within_rendered_area")
        coverage_fraction = bucket.get("rendered_coverage_fraction")
        lines.append(
            "bin{idx} {start:7.3f}-{end:7.3f} px={pixels:5d} hit={hit:5d} nohit={nohit:5d} "
            "unrendered={unrendered:5d} hitFrac={hitfrac} renderedCov={coverage} other={other:5d}".format(
                idx=bucket["bin_index"],
                start=bucket["radius_start_px"],
                end=bucket["radius_end_px"],
                pixels=bucket["pixel_count"],
                hit=bucket["final_hit_pixel_count"],
                nohit=bucket["rendered_no_hit_pixel_count"],
                unrendered=bucket["unrendered_pixel_count"],
                hitfrac="na" if hit_fraction is None else f"{hit_fraction:.4f}",
                coverage="na" if coverage_fraction is None else f"{coverage_fraction:.4f}",
                other=bucket["other_pixel_count"],
            )
        )
    totals = profile.get("totals") or {}
    lines.extend(
        [
            "Totals",
            f"Pixels: {totals.get('pixel_count')}",
            f"Final Hit Pixels: {totals.get('final_hit_pixel_count')}",
            f"Rendered No-Hit Pixels: {totals.get('rendered_no_hit_pixel_count')}",
            f"Unrendered Pixels: {totals.get('unrendered_pixel_count')}",
            f"Overall Hit Fraction Within Rendered Area: {totals.get('hit_fraction_within_rendered_area')}",
            f"Overall Rendered Coverage Fraction: {totals.get('rendered_coverage_fraction')}",
        ]
    )
    return "\n".join(lines) + "\n"


def build_radial_profile_text(profile: dict) -> str:
    return build_profile_text(profile, "Radial Profile")


def build_field_radial_profile_text(profile: dict) -> str:
    return build_profile_text(profile, "Field Radial Profile")


def format_sector_text_value(value) -> str:
    return "na" if value is None else f"{value:.4f}"


def build_sector_profile_text(profile: dict, title: str) -> str:
    if not profile.get("enabled"):
        reason = profile.get("reason") or "disabled"
        return f"{title}: unavailable ({reason})\n"

    origin_label, origin = get_profile_origin(profile)
    sector_definition = profile.get("sector_definition") or {}

    lines = [
        title,
        f"Capture Path: {profile.get('capture_path')}",
        f"Analysis Capture Mode: {profile.get('analysis_capture_mode')}",
        f"Bin Count: {profile.get('bin_count')}",
        f"{origin_label}: ({origin.get('x')}, {origin.get('y')})",
        f"Max Radius Px: {profile.get('max_radius_px')}",
        "Sector Definition: left=({left_rule}) right=({right_rule})".format(
            left_rule=sector_definition.get("left_rule"),
            right_rule=sector_definition.get("right_rule"),
        ),
        "Legend: bin radius_px | left/right px hit nohit unrendered hit_frac_rendered rendered_coverage other",
    ]
    field_analysis_origin = profile.get("field_analysis_origin") or {}
    if field_analysis_origin.get("resolved"):
        field_world_origin = field_analysis_origin.get("field_world_origin") or {}
        lines.append(
            "Field World Origin: ({x}, {y}, {z})".format(
                x=field_world_origin.get("x"),
                y=field_world_origin.get("y"),
                z=field_world_origin.get("z"),
            )
        )
    for bucket in profile.get("bins", []):
        left_bucket = (bucket.get("sectors") or {}).get("left") or {}
        right_bucket = (bucket.get("sectors") or {}).get("right") or {}
        lines.append(
            "bin{idx} {start:7.3f}-{end:7.3f} "
            "L(px={lpx:5d} hit={lhit:5d} nohit={lnohit:5d} unrendered={lunrendered:5d} "
            "hitFrac={lhitfrac} renderedCov={lcoverage} other={lother:5d}) "
            "R(px={rpx:5d} hit={rhit:5d} nohit={rnohit:5d} unrendered={runrendered:5d} "
            "hitFrac={rhitfrac} renderedCov={rcoverage} other={rother:5d})".format(
                idx=bucket["bin_index"],
                start=bucket["radius_start_px"],
                end=bucket["radius_end_px"],
                lpx=left_bucket.get("pixel_count", 0),
                lhit=left_bucket.get("final_hit_pixel_count", 0),
                lnohit=left_bucket.get("rendered_no_hit_pixel_count", 0),
                lunrendered=left_bucket.get("unrendered_pixel_count", 0),
                lhitfrac=format_sector_text_value(left_bucket.get("hit_frac_rendered")),
                lcoverage=format_sector_text_value(left_bucket.get("rendered_coverage")),
                lother=left_bucket.get("other_pixel_count", 0),
                rpx=right_bucket.get("pixel_count", 0),
                rhit=right_bucket.get("final_hit_pixel_count", 0),
                rnohit=right_bucket.get("rendered_no_hit_pixel_count", 0),
                runrendered=right_bucket.get("unrendered_pixel_count", 0),
                rhitfrac=format_sector_text_value(right_bucket.get("hit_frac_rendered")),
                rcoverage=format_sector_text_value(right_bucket.get("rendered_coverage")),
                rother=right_bucket.get("other_pixel_count", 0),
            )
        )
    totals = profile.get("totals") or {}
    total_sectors = totals.get("sectors") or {}
    combined = totals.get("combined") or {}
    for sector_name in ("left", "right"):
        sector_totals = total_sectors.get(sector_name) or {}
        lines.append(
            (
                f"Totals {sector_name}: px={sector_totals.get('pixel_count')} "
                f"hit={sector_totals.get('final_hit_pixel_count')} "
                f"nohit={sector_totals.get('rendered_no_hit_pixel_count')} "
                f"unrendered={sector_totals.get('unrendered_pixel_count')} "
                f"hitFrac={format_sector_text_value(sector_totals.get('hit_frac_rendered'))} "
                f"renderedCov={format_sector_text_value(sector_totals.get('rendered_coverage'))} "
                f"other={sector_totals.get('other_pixel_count')}"
            )
        )
    lines.append(
        (
            f"Totals combined: px={combined.get('pixel_count')} "
            f"hit={combined.get('final_hit_pixel_count')} "
            f"nohit={combined.get('rendered_no_hit_pixel_count')} "
            f"unrendered={combined.get('unrendered_pixel_count')} "
            f"hitFrac={format_sector_text_value(combined.get('hit_frac_rendered'))} "
            f"renderedCov={format_sector_text_value(combined.get('rendered_coverage'))} "
            f"other={combined.get('other_pixel_count')}"
        )
    )
    return "\n".join(lines) + "\n"


def build_radial_sector_profile_text(profile: dict) -> str:
    return build_sector_profile_text(profile, "Radial Sector Profile")


def build_field_radial_sector_profile_text(profile: dict) -> str:
    return build_sector_profile_text(profile, "Field Radial Sector Profile")


def build_nearest_attractor_profile_text(profile: dict) -> str:
    if not profile.get("enabled"):
        reason = profile.get("reason") or "disabled"
        return f"Nearest Attractor Profile: unavailable ({reason})\n"

    partition_summary = profile.get("partition_summary") or {}
    lines = [
        "Nearest Attractor Profile",
        f"Capture Path: {profile.get('capture_path')}",
        f"Analysis Capture Mode: {profile.get('analysis_capture_mode')}",
        f"Assignment Basis: {profile.get('assignment_basis')}",
        f"Attractor Count: {profile.get('attractor_count')}",
        f"Behavior: {partition_summary.get('behavior')}",
        f"Partition Clean: {partition_summary.get('partition_clean')}",
        f"Clean Partition Fraction: {partition_summary.get('clean_partition_fraction')}",
        f"Dominant Attractor: {partition_summary.get('dominant_attractor_path')}",
        f"Dominant Final Hit Share: {partition_summary.get('dominant_final_hit_share')}",
        f"Second Final Hit Share: {partition_summary.get('second_final_hit_share')}",
        f"Ambiguous Final Hit Pixels: {partition_summary.get('ambiguous_final_hit_pixels')}",
        f"Total Final Hit Pixels: {partition_summary.get('total_final_hit_pixels')}",
    ]

    for attractor in profile.get("attractors", []):
        totals = attractor.get("totals") or {}
        sector_totals = (attractor.get("sector_totals") or {}).get("sectors") or {}
        left_sector = sector_totals.get("left") or {}
        right_sector = sector_totals.get("right") or {}
        center = attractor.get("field_center_pixel") or {}
        world = attractor.get("field_world_origin") or {}
        lines.extend(
            [
                "",
                "Attractor {index}".format(index=attractor.get("attractor_index")),
                f"Field Path: {attractor.get('field_path')}",
                f"Primary Field: {attractor.get('is_primary_field')}",
                f"Field Center Pixel: ({center.get('x')}, {center.get('y')})",
                f"Field World Origin: ({world.get('x')}, {world.get('y')}, {world.get('z')})",
                f"Assigned Pixels: {attractor.get('assigned_pixel_count')}",
                f"Final Hit Share: {attractor.get('final_hit_share')}",
                f"Totals: px={totals.get('pixel_count')} hit={totals.get('final_hit_pixel_count')} nohit={totals.get('rendered_no_hit_pixel_count')} unrendered={totals.get('unrendered_pixel_count')} hitFrac={totals.get('hit_fraction_within_rendered_area')} renderedCov={totals.get('rendered_coverage_fraction')} other={totals.get('other_pixel_count')}",
                f"Sector Left: px={left_sector.get('pixel_count')} hit={left_sector.get('final_hit_pixel_count')} nohit={left_sector.get('rendered_no_hit_pixel_count')} unrendered={left_sector.get('unrendered_pixel_count')} hitFrac={left_sector.get('hit_frac_rendered')} renderedCov={left_sector.get('rendered_coverage')} other={left_sector.get('other_pixel_count')}",
                f"Sector Right: px={right_sector.get('pixel_count')} hit={right_sector.get('final_hit_pixel_count')} nohit={right_sector.get('rendered_no_hit_pixel_count')} unrendered={right_sector.get('unrendered_pixel_count')} hitFrac={right_sector.get('hit_frac_rendered')} renderedCov={right_sector.get('rendered_coverage')} other={right_sector.get('other_pixel_count')}",
                "Legend: bin radius_px pixels final_hit rendered_no_hit unrendered hit_frac_rendered rendered_coverage other",
            ]
        )
        for bucket in attractor.get("bins", []):
            hit_fraction = bucket.get("hit_fraction_within_rendered_area")
            coverage_fraction = bucket.get("rendered_coverage_fraction")
            lines.append(
                "bin{idx} {start:7.3f}-{end:7.3f} px={pixels:5d} hit={hit:5d} nohit={nohit:5d} "
                "unrendered={unrendered:5d} hitFrac={hitfrac} renderedCov={coverage} other={other:5d}".format(
                    idx=bucket["bin_index"],
                    start=bucket["radius_start_px"],
                    end=bucket["radius_end_px"],
                    pixels=bucket["pixel_count"],
                    hit=bucket["final_hit_pixel_count"],
                    nohit=bucket["rendered_no_hit_pixel_count"],
                    unrendered=bucket["unrendered_pixel_count"],
                    hitfrac="na" if hit_fraction is None else f"{hit_fraction:.4f}",
                    coverage="na" if coverage_fraction is None else f"{coverage_fraction:.4f}",
                    other=bucket["other_pixel_count"],
                )
            )
    return "\n".join(lines) + "\n"


def build_summary(metrics: dict, args: argparse.Namespace) -> str:
    verification = metrics.get("verification") or {}
    row_coverage = metrics.get("row_coverage") or {}
    radial_profile = metrics.get("radial_profile") or {}
    radial_sector_profile = metrics.get("radial_sector_profile") or {}
    field_radial_profile = metrics.get("field_radial_profile") or {}
    field_radial_sector_profile = metrics.get("field_radial_sector_profile") or {}
    nearest_attractor_profile = metrics.get("nearest_attractor_profile") or {}
    radial_sector_totals = (radial_sector_profile.get("totals") or {}).get("sectors") or {}
    radial_sector_left = radial_sector_totals.get("left") or {}
    radial_sector_right = radial_sector_totals.get("right") or {}
    field_sector_totals = (field_radial_sector_profile.get("totals") or {}).get("sectors") or {}
    field_sector_left = field_sector_totals.get("left") or {}
    field_sector_right = field_sector_totals.get("right") or {}
    nearest_partition = nearest_attractor_profile.get("partition_summary") or {}
    field_origin = field_radial_profile.get("field_analysis_origin") or {}
    field_center_pixel = field_origin.get("field_center_pixel") or {}
    field_world_origin = field_origin.get("field_world_origin") or {}
    lines = [
        f"Fixture: {metrics['fixture_id']}",
        f"Timestamp: {args.timestamp}",
        f"Scene: {metrics['scene']}",
        f"Status: {metrics['status']}",
        f"Runtime Seconds: {metrics['runtime_seconds']}",
        f"Traced Pixels: {metrics['traced_pixels']}",
        f"Source Hits: {metrics['source_hits']}",
        f"Miss Hits: {metrics['miss_hits']}",
        f"Capture Succeeded: {str(metrics['capture_succeeded']).lower()}",
        f"Launch Audit Status: {metrics['launch_audit_status']}",
        f"Guard Progress Exits: {metrics['guard_progress']}",
        f"Forced Advance Events: {metrics['forced_advance']}",
        f"Render Health Step: {metrics['render_health_step']}",
        f"Processed Rows: {metrics['processed_rows']}",
        f"Total Rows Considered: {metrics['total_rows_considered']}",
        f"Total Rows Processed: {metrics['total_rows_processed']}",
        f"Total Rows Skipped: {metrics['total_rows_skipped']}",
        f"Processed Row Window: {metrics['processed_row_start']}..{metrics['processed_row_end']}",
        f"Zero-Hit Rows: {metrics['zero_hit_rows']}",
        f"Row Participation Summary: {metrics['row_participation_summary']}",
        f"Row Coverage: {row_coverage.get('coverage_visual', '')}",
        f"Visual Mode: {metrics.get('visual_mode')}",
        f"Visual Shading Mode: {metrics.get('visual_shading_mode')}",
        f"Baseline Shading Mode: {metrics.get('visual_baseline_shading_mode')}",
        f"Analysis Capture Mode: {metrics.get('analysis_capture_mode')}",
        f"Analysis Capture Written: {metrics.get('analysis_capture_written')}",
        f"Debug Capture Written: {metrics.get('debug_capture_written')}",
        f"Categorical Final Written: {metrics.get('categorical_final_written')}",
        f"Overlay Enabled For Analysis Capture: {metrics.get('overlay_enabled_for_analysis_capture')}",
        f"White Streak Likely Source: {metrics.get('white_streak_likely_source')}",
        f"Rows Started: {metrics.get('rows_started')}",
        f"Rows Completed: {metrics.get('rows_completed')}",
        f"Rows Partially Written: {metrics.get('rows_partially_written')}",
        f"Rows Early Terminated: {metrics.get('rows_early_terminated')}",
        f"Final Hit Pixel Count: {metrics.get('final_hit_pixel_count')}",
        f"Traversal Write Pixel Count: {metrics.get('traversal_write_pixel_count')}",
        f"Rendered Bright Rows: {metrics.get('analysis_rendered_bright_row_count')}",
        f"RenderHealth Geom Segments Queried: {metrics.get('render_health_geom_segments_queried')}",
        f"Bottom Region Likely Cause: {metrics.get('bottom_region_likely_cause')}",
        f"Analysis Unrendered Rows: {metrics.get('analysis_unrendered_rows')}",
        f"Analysis Band Matches Unrendered Rows: {metrics.get('analysis_band_matches_unrendered_rows')}",
        f"Radial Bin Count: {radial_profile.get('bin_count')}",
        f"Radial Overall Hit Fraction: {(radial_profile.get('totals') or {}).get('hit_fraction_within_rendered_area')}",
        f"Radial Overall Rendered Coverage: {(radial_profile.get('totals') or {}).get('rendered_coverage_fraction')}",
        f"Radial Profile Path: {metrics.get('radial_profile_path')}",
        f"Radial Sector Left Hit Fraction: {radial_sector_left.get('hit_frac_rendered')}",
        f"Radial Sector Right Hit Fraction: {radial_sector_right.get('hit_frac_rendered')}",
        f"Radial Sector Left Rendered Coverage: {radial_sector_left.get('rendered_coverage')}",
        f"Radial Sector Right Rendered Coverage: {radial_sector_right.get('rendered_coverage')}",
        f"Radial Sector Profile Path: {metrics.get('radial_sector_profile_path')}",
        f"Field Center Pixel: ({field_center_pixel.get('x')}, {field_center_pixel.get('y')})",
        f"Field World Origin: ({field_world_origin.get('x')}, {field_world_origin.get('y')}, {field_world_origin.get('z')})",
        f"Field Radial Overall Hit Fraction: {(field_radial_profile.get('totals') or {}).get('hit_fraction_within_rendered_area')}",
        f"Field Radial Overall Rendered Coverage: {(field_radial_profile.get('totals') or {}).get('rendered_coverage_fraction')}",
        f"Field Radial Profile Path: {metrics.get('field_radial_profile_path')}",
        f"Field Sector Left Hit Fraction: {field_sector_left.get('hit_frac_rendered')}",
        f"Field Sector Right Hit Fraction: {field_sector_right.get('hit_frac_rendered')}",
        f"Field Sector Left Rendered Coverage: {field_sector_left.get('rendered_coverage')}",
        f"Field Sector Right Rendered Coverage: {field_sector_right.get('rendered_coverage')}",
        f"Field Radial Sector Profile Path: {metrics.get('field_radial_sector_profile_path')}",
        f"Nearest Attractor Count: {nearest_attractor_profile.get('attractor_count')}",
        f"Nearest Attractor Partition Clean: {nearest_partition.get('partition_clean')}",
        f"Nearest Attractor Clean Fraction: {nearest_partition.get('clean_partition_fraction')}",
        f"Nearest Attractor Dominant Path: {nearest_partition.get('dominant_attractor_path')}",
        f"Nearest Attractor Dominant Share: {nearest_partition.get('dominant_final_hit_share')}",
        f"Nearest Attractor Second Share: {nearest_partition.get('second_final_hit_share')}",
        f"Nearest Attractor Behavior: {nearest_partition.get('behavior')}",
        f"Nearest Attractor Profile Path: {metrics.get('nearest_attractor_profile_path')}",
        f"Runtime Fingerprint: {metrics['runtime_fingerprint']}",
        f"Assembly Timestamp Present: {str(verification.get('assembly_timestamp_present', False)).lower()}",
        f"Requested Step Match: {str(verification.get('effective_step_matches_requested', False)).lower()}",
        f"Row Diagnostics Present: {str(verification.get('row_diagnostics_present', False)).lower()}",
        f"Scheduler Clean: {str(verification.get('scheduler_clean', False)).lower()}",
        f"Run Verified: {str(verification.get('run_verified', False)).lower()}",
        f"Turn Threshold: {metrics['effective_turn_threshold']}",
        f"Output Path: {args.run_dir}",
    ]
    if metrics.get("failure_reason"):
        lines.append(f"Failure Reason: {metrics['failure_reason']}")
    return "\n".join(lines) + "\n"


def build_parser(description: str = "Build fixture characterization run artifacts from a raw Godot log.") -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=description)
    parser.add_argument("--fixture-id", required=True)
    parser.add_argument("--timestamp", required=True)
    parser.add_argument("--scene", required=True)
    parser.add_argument("--launcher", required=True)
    parser.add_argument("--run-dir", type=Path, required=True)
    parser.add_argument("--log-path", type=Path, required=True)
    parser.add_argument("--capture-path", type=Path, required=True)
    parser.add_argument("--runtime-seconds", type=float, required=True)
    parser.add_argument("--godot-exit-code", type=int, required=True)
    parser.add_argument("--settle-frames", type=int, required=True)
    parser.add_argument("--min-rh-step", type=int, required=True)
    parser.add_argument("--min-processed-rows", type=int, required=True)
    parser.add_argument("--capture-film-opacity", required=True)
    parser.add_argument("--compare-grid", required=True)
    parser.add_argument("--compare-crosshair", required=True)
    parser.add_argument("--radial-bin-count", type=int, default=8)
    parser.add_argument("--requested-transport-model", default=None)
    parser.add_argument("--requested-step-length", type=float, default=None)
    parser.add_argument("--requested-min-step-length", type=float, default=None)
    parser.add_argument("--requested-steps-per-ray", type=int, default=None)
    parser.add_argument("--requested-turn-threshold", type=float, default=None)
    parser.add_argument("--requested-error-tolerance", type=float, default=None)
    return parser


def run_report(args: argparse.Namespace) -> int:
    args.run_dir.mkdir(parents=True, exist_ok=True)
    log_text = args.log_path.read_text(encoding="utf-8", errors="replace") if args.log_path.exists() else ""
    parsed = parse_log(log_text)

    params = build_params(args, parsed)
    metrics = build_metrics(args, parsed)
    metrics["verification"] = build_verification(metrics, params)
    metrics["row_coverage"] = build_row_coverage_artifact(metrics)
    radial_profile, radial_sector_profile = build_spatial_profiles(args, metrics)
    field_radial_profile, field_radial_sector_profile = build_field_spatial_profiles(args, metrics)
    radial_profile_path = args.run_dir / "radial_profile.json"
    radial_profile_text_path = args.run_dir / "radial_profile.txt"
    radial_profile_path.write_text(json.dumps(radial_profile, indent=2) + "\n", encoding="utf-8")
    radial_profile_text_path.write_text(build_radial_profile_text(radial_profile), encoding="utf-8")
    metrics["radial_profile"] = radial_profile
    metrics["radial_profile_path"] = str(radial_profile_path)
    metrics["radial_profile_text_path"] = str(radial_profile_text_path)
    radial_sector_profile_path = args.run_dir / "radial_sector_profile.json"
    radial_sector_profile_text_path = args.run_dir / "radial_sector_profile.txt"
    radial_sector_profile_path.write_text(json.dumps(radial_sector_profile, indent=2) + "\n", encoding="utf-8")
    radial_sector_profile_text_path.write_text(
        build_radial_sector_profile_text(radial_sector_profile),
        encoding="utf-8",
    )
    metrics["radial_sector_profile"] = radial_sector_profile
    metrics["radial_sector_profile_path"] = str(radial_sector_profile_path)
    metrics["radial_sector_profile_text_path"] = str(radial_sector_profile_text_path)
    field_radial_profile_path = args.run_dir / "field_radial_profile.json"
    field_radial_profile_text_path = args.run_dir / "field_radial_profile.txt"
    field_radial_profile_path.write_text(json.dumps(field_radial_profile, indent=2) + "\n", encoding="utf-8")
    field_radial_profile_text_path.write_text(build_field_radial_profile_text(field_radial_profile), encoding="utf-8")
    metrics["field_radial_profile"] = field_radial_profile
    metrics["field_radial_profile_path"] = str(field_radial_profile_path)
    metrics["field_radial_profile_text_path"] = str(field_radial_profile_text_path)
    field_radial_sector_profile_path = args.run_dir / "field_radial_sector_profile.json"
    field_radial_sector_profile_text_path = args.run_dir / "field_radial_sector_profile.txt"
    field_radial_sector_profile_path.write_text(
        json.dumps(field_radial_sector_profile, indent=2) + "\n",
        encoding="utf-8",
    )
    field_radial_sector_profile_text_path.write_text(
        build_field_radial_sector_profile_text(field_radial_sector_profile),
        encoding="utf-8",
    )
    metrics["field_radial_sector_profile"] = field_radial_sector_profile
    metrics["field_radial_sector_profile_path"] = str(field_radial_sector_profile_path)
    metrics["field_radial_sector_profile_text_path"] = str(field_radial_sector_profile_text_path)
    nearest_attractor_profile = build_nearest_attractor_profile(args, metrics)
    nearest_attractor_profile_path = args.run_dir / "nearest_attractor_profile.json"
    nearest_attractor_profile_text_path = args.run_dir / "nearest_attractor_profile.txt"
    nearest_attractor_profile_path.write_text(
        json.dumps(nearest_attractor_profile, indent=2) + "\n",
        encoding="utf-8",
    )
    nearest_attractor_profile_text_path.write_text(
        build_nearest_attractor_profile_text(nearest_attractor_profile),
        encoding="utf-8",
    )
    metrics["nearest_attractor_profile"] = nearest_attractor_profile
    metrics["nearest_attractor_profile_path"] = str(nearest_attractor_profile_path)
    metrics["nearest_attractor_profile_text_path"] = str(nearest_attractor_profile_text_path)
    summary = build_summary(metrics, args)
    summary_json = {
        "timestamp": args.timestamp,
        "fixture_id": args.fixture_id,
        "scene": args.scene,
        "launcher": args.launcher,
        "run_dir": str(args.run_dir),
        "capture_path": str(args.capture_path),
        "log_path": str(args.log_path),
        "runtime_seconds": args.runtime_seconds,
        "params": params,
        "metrics": metrics,
        "capture": parsed.get("capture") or {},
        "captureArtifacts": parsed.get("captureArtifacts") or {},
        "overlayDiag": parsed.get("overlayDiag") or {},
        "whiteStreakDiag": parsed.get("whiteStreakDiag") or {},
        "writeDiag": parsed.get("writeDiag") or {},
        "bottomRegionDiag": parsed.get("bottomRegionDiag") or {},
        "rowParticipation": parsed.get("rows") or {},
        "runtimeBuild": parsed.get("runtimeBuild") or {},
        "launchAudit": parsed.get("launchAudit") or {},
        "renderer": parsed.get("renderer") or {},
        "visual": parsed.get("visual") or {},
        "scheduler": {
            "guard_progress": parsed.get("guardProgress"),
            "forcedAdvance": parsed.get("forcedAdvance"),
        },
        "verification": metrics.get("verification") or {},
        "image": {
            "width": metrics.get("image_width"),
            "height": metrics.get("image_height"),
        },
        "visual_tag": metrics.get("visual_mode") or "",
        "decision_tag": "",
    }

    (args.run_dir / "params.json").write_text(json.dumps(params, indent=2) + "\n", encoding="utf-8")
    (args.run_dir / "metrics.json").write_text(json.dumps(metrics, indent=2) + "\n", encoding="utf-8")
    (args.run_dir / "summary.json").write_text(json.dumps(summary_json, indent=2) + "\n", encoding="utf-8")
    (args.run_dir / "summary.txt").write_text(summary, encoding="utf-8")
    row_coverage = metrics.get("row_coverage") or {}
    row_coverage_text = "\n".join(
        [
            f"Total Rows Considered: {row_coverage.get('total_rows_considered')}",
            f"Total Rows Processed: {row_coverage.get('total_rows_processed')}",
            f"Zero-Hit Rows: {row_coverage.get('zero_hit_rows')}",
            f"Coverage: {row_coverage.get('coverage_visual', '')}",
            f"Legend: {row_coverage.get('coverage_legend', '')}",
            "",
        ]
    )
    (args.run_dir / "row_coverage.json").write_text(json.dumps(row_coverage, indent=2) + "\n", encoding="utf-8")
    (args.run_dir / "row_coverage.txt").write_text(row_coverage_text, encoding="utf-8")
    return 0


def main(description: str = "Build fixture characterization run artifacts from a raw Godot log.") -> int:
    parser = build_parser(description)
    args = parser.parse_args()
    return run_report(args)


if __name__ == "__main__":
    raise SystemExit(main())
