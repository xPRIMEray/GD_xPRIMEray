#!/usr/bin/env python3
"""Compare measured straight/curved transport classification captures.

This tool is presentation-only instrumentation. It reads already-captured
classification images, computes a deterministic disagreement mask, and writes
quiet artifact overlays. It does not run or modify renderer transport.
"""

from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path
from typing import Any

from PIL import Image


SCHEMA_VERSION = 1

# Mirrors GrinFilmCamera fixture transport classification colors, rounded to u8.
CLASS_COLORS: dict[str, tuple[int, int, int]] = {
    "geom_hit": (41, 184, 66),
    "portal_hit": (46, 209, 235),
    "throat_event": (242, 199, 46),
    "throat_entry": (245, 209, 41),
    "throat_exit": (245, 107, 36),
    "throat_shell_transform": (184, 82, 235),
    "throat_inner_absorb": (117, 41, 36),
    "background_hit": (82, 112, 219),
    "escaped_no_hit": (140, 43, 43),
    "budget_exhausted": (242, 46, 46),
}

DEFAULT_METADATA_KEYS = (
    "width",
    "height",
    "fixture",
    "fixture_id",
    "fixture_label",
    "camera_pose_key",
    "traversal_mode",
    "scheduler_mode",
    "render_test_traversal_pass1_pass2",
)

DEFAULT_UNRESOLVED_KINDS = {
    "unknown",
    "unclassified",
    "budget_exhausted",
}

CHANGED_RGBA = (74, 154, 196, 82)
UNRESOLVED_RGBA = (172, 176, 180, 110)
CHANGED_CONTOUR_RGBA = (108, 202, 224, 180)
UNRESOLVED_CONTOUR_RGBA = (190, 190, 190, 170)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--straight", required=True, type=Path, help="Straight transport classification PNG")
    parser.add_argument("--curved", required=True, type=Path, help="Curved GRIN classification PNG")
    parser.add_argument("--out-dir", required=True, type=Path, help="Output folder for delta artifacts")
    parser.add_argument("--straight-metadata", type=Path, help="Optional straight capture metadata JSON")
    parser.add_argument("--curved-metadata", type=Path, help="Optional curved capture metadata JSON")
    parser.add_argument("--straight-coverage", type=Path, help="Optional straight coverage summary JSON")
    parser.add_argument("--curved-coverage", type=Path, help="Optional curved coverage summary JSON")
    parser.add_argument(
        "--metadata-key",
        action="append",
        default=[],
        help="Metadata key to compare. May be repeated. Defaults to common capture keys.",
    )
    parser.add_argument(
        "--require-metadata",
        action="store_true",
        help="Fail if metadata JSON is absent or compared metadata keys do not match.",
    )
    parser.add_argument(
        "--epsilon",
        type=int,
        default=3,
        help="Per-channel color tolerance for classification palette matching.",
    )
    parser.add_argument(
        "--top-transitions",
        type=int,
        default=12,
        help="Number of changed transition buckets to include in the summary.",
    )
    parser.add_argument(
        "--treat-escaped-unresolved",
        action="store_true",
        help="Treat escaped_no_hit as unresolved/missing evidence instead of a measured terminal class.",
    )
    parser.add_argument(
        "--skip-contours",
        action="store_true",
        help="Do not write classification_delta_contours.png.",
    )
    return parser.parse_args()


def load_json(path: Path | None) -> dict[str, Any]:
    if path is None:
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        raise SystemExit(f"Failed to read JSON {path}: {exc}") from exc


def normalize_rgb(pixel: tuple[int, ...]) -> tuple[int, int, int]:
    r, g, b = pixel[:3]
    # Matches GrinFilmCamera.NormalizeFixtureTransportClassificationPixel: black
    # classification pixels are treated as budget exhaustion, not as an invented
    # new class.
    if r <= 0 and g <= 0 and b <= 0:
        return CLASS_COLORS["budget_exhausted"]
    return (r, g, b)


def classify_rgb(rgb: tuple[int, int, int], epsilon: int) -> str:
    best_name = "unknown"
    best_dist = 10**9
    for name, ref in CLASS_COLORS.items():
        dr = abs(rgb[0] - ref[0])
        dg = abs(rgb[1] - ref[1])
        db = abs(rgb[2] - ref[2])
        if dr <= epsilon and dg <= epsilon and db <= epsilon:
            dist = dr + dg + db
            if dist < best_dist:
                best_dist = dist
                best_name = name
    return best_name


def metadata_value(metadata: dict[str, Any], key: str) -> Any:
    current: Any = metadata
    for part in key.split("."):
        if not isinstance(current, dict) or part not in current:
            return None
        current = current[part]
    return current


def compare_metadata(
    straight_meta: dict[str, Any],
    curved_meta: dict[str, Any],
    keys: list[str],
) -> tuple[list[dict[str, Any]], list[str]]:
    compared: list[dict[str, Any]] = []
    warnings: list[str] = []
    for key in keys:
        s_value = metadata_value(straight_meta, key)
        c_value = metadata_value(curved_meta, key)
        if s_value is None and c_value is None:
            continue
        match = s_value == c_value
        compared.append(
            {
                "key": key,
                "straight": s_value,
                "curved": c_value,
                "match": match,
            }
        )
        if not match:
            warnings.append(f"metadata mismatch: {key}")
    return compared, warnings


def is_edge(mask: list[list[bool]], x: int, y: int, width: int, height: int) -> bool:
    if not mask[y][x]:
        return False
    for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        if nx < 0 or ny < 0 or nx >= width or ny >= height:
            return True
        if not mask[ny][nx]:
            return True
    return False


def compare(args: argparse.Namespace) -> dict[str, Any]:
    straight_img = Image.open(args.straight).convert("RGBA")
    curved_img = Image.open(args.curved).convert("RGBA")
    if straight_img.size != curved_img.size:
        raise SystemExit(
            f"Image size mismatch: straight={straight_img.size} curved={curved_img.size}"
        )

    width, height = straight_img.size
    total_pixels = width * height
    unresolved_kinds = set(DEFAULT_UNRESOLVED_KINDS)
    if args.treat_escaped_unresolved:
        unresolved_kinds.add("escaped_no_hit")

    delta = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    contours = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    delta_px = delta.load()
    contours_px = contours.load()
    straight_px = straight_img.load()
    curved_px = curved_img.load()

    changed_mask = [[False] * width for _ in range(height)]
    unresolved_mask = [[False] * width for _ in range(height)]
    transition_counts: Counter[str] = Counter()
    straight_kind_counts: Counter[str] = Counter()
    curved_kind_counts: Counter[str] = Counter()

    unchanged_pixels = 0
    changed_pixels = 0
    unresolved_pixels = 0

    for y in range(height):
        for x in range(width):
            s_rgb = normalize_rgb(straight_px[x, y])
            c_rgb = normalize_rgb(curved_px[x, y])
            s_kind = classify_rgb(s_rgb, args.epsilon)
            c_kind = classify_rgb(c_rgb, args.epsilon)
            straight_kind_counts[s_kind] += 1
            curved_kind_counts[c_kind] += 1
            unresolved = s_kind in unresolved_kinds or c_kind in unresolved_kinds
            changed = s_kind != c_kind

            if unresolved:
                unresolved_pixels += 1
                unresolved_mask[y][x] = True
                delta_px[x, y] = UNRESOLVED_RGBA
            elif changed:
                changed_pixels += 1
                changed_mask[y][x] = True
                transition_counts[f"{s_kind}->{c_kind}"] += 1
                delta_px[x, y] = CHANGED_RGBA
            else:
                unchanged_pixels += 1

    for y in range(height):
        for x in range(width):
            if is_edge(unresolved_mask, x, y, width, height):
                contours_px[x, y] = UNRESOLVED_CONTOUR_RGBA
            elif is_edge(changed_mask, x, y, width, height):
                contours_px[x, y] = CHANGED_CONTOUR_RGBA

    args.out_dir.mkdir(parents=True, exist_ok=True)
    delta_path = args.out_dir / "classification_delta.png"
    contours_path = args.out_dir / "classification_delta_contours.png"
    summary_path = args.out_dir / "classification_delta_summary.json"
    delta.save(delta_path)
    if not args.skip_contours:
        contours.save(contours_path)

    straight_meta = load_json(args.straight_metadata)
    curved_meta = load_json(args.curved_metadata)
    metadata_keys = list(args.metadata_key or DEFAULT_METADATA_KEYS)
    metadata_compared, metadata_warnings = compare_metadata(straight_meta, curved_meta, metadata_keys)
    metadata_present = bool(straight_meta) and bool(curved_meta)
    if args.require_metadata and not metadata_present:
        raise SystemExit("--require-metadata was set, but one or both metadata files are missing/empty")
    if args.require_metadata and metadata_warnings:
        raise SystemExit("; ".join(metadata_warnings))

    summary = {
        "schema_version": SCHEMA_VERSION,
        "tool": "classification_delta_compare.py",
        "inputs": {
            "straight_classification_image": str(args.straight),
            "curved_classification_image": str(args.curved),
            "straight_metadata": str(args.straight_metadata) if args.straight_metadata else None,
            "curved_metadata": str(args.curved_metadata) if args.curved_metadata else None,
            "straight_coverage": str(args.straight_coverage) if args.straight_coverage else None,
            "curved_coverage": str(args.curved_coverage) if args.curved_coverage else None,
        },
        "image": {
            "width": width,
            "height": height,
            "total_pixels": total_pixels,
        },
        "metrics": {
            "unchanged_pixels": unchanged_pixels,
            "changed_pixels": changed_pixels,
            "changed_ratio": changed_pixels / total_pixels if total_pixels else 0.0,
            "unresolved_pixels": unresolved_pixels,
            "unresolved_ratio": unresolved_pixels / total_pixels if total_pixels else 0.0,
            "resolved_compared_pixels": total_pixels - unresolved_pixels,
        },
        "class_counts": {
            "straight": dict(sorted(straight_kind_counts.items())),
            "curved": dict(sorted(curved_kind_counts.items())),
        },
        "top_transition_buckets": [
            {
                "transition": transition,
                "count": count,
                "ratio": count / total_pixels if total_pixels else 0.0,
            }
            for transition, count in transition_counts.most_common(max(0, args.top_transitions))
        ],
        "metadata": {
            "present": metadata_present,
            "compared": metadata_compared,
            "warnings": metadata_warnings,
            "require_metadata": bool(args.require_metadata),
        },
        "coverage": {
            "straight": load_json(args.straight_coverage),
            "curved": load_json(args.curved_coverage),
        },
        "visual_language": {
            "unchanged_pixels": "transparent",
            "changed_pixels": "restrained blue-cyan tint",
            "unresolved_pixels": "neutral gray mark",
            "contours": "single-pixel changed/unresolved mask edges",
            "synthetic_effects": "none",
        },
        "guardrails": [
            "presentation-only instrumentation",
            "no transport semantic changes",
            "no scheduler modifications",
            "no hit selection changes",
            "no resolver decision changes",
            "no oracle behavior changes",
            "no synthetic field energy, glow, particles, or turbulence",
        ],
        "outputs": {
            "classification_delta": str(delta_path),
            "classification_delta_summary": str(summary_path),
            "classification_delta_contours": None if args.skip_contours else str(contours_path),
        },
    }

    summary_path.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return summary


def main() -> int:
    args = parse_args()
    summary = compare(args)
    print(json.dumps(summary["outputs"], indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
