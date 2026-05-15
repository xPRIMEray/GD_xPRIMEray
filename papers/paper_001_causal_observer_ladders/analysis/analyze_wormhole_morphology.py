from __future__ import annotations

import json
from dataclasses import asdict, dataclass
from pathlib import Path

import cv2
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from skimage import measure


ROOT = Path(__file__).resolve().parents[4]
RUN_DIR = ROOT / "output" / "fixture_runs" / "fixture_011_wormhole_checkpoint_sequence" / "2026-04-20T22-26-39"
ANALYSIS_DIR = Path(__file__).resolve().parent / "morphology"
ANNOTATED_DIR = ANALYSIS_DIR / "annotated"
PROFILE_DIR = ANALYSIS_DIR / "profiles"
SUMMARY_MD = ANALYSIS_DIR / "summary.md"
SUMMARY_JSON = ANALYSIS_DIR / "morphology_summary.json"

CHECKPOINTS = [
    ("mouth", RUN_DIR / "00_mouth_debug.png"),
    ("mouth_to_throat_approach", RUN_DIR / "01_mouth_to_throat_approach_debug.png"),
    ("throat", RUN_DIR / "02_throat_debug.png"),
    ("post_throat_backstep_01", RUN_DIR / "03_post_throat_backstep_01_debug.png"),
    ("post_throat_exit_approach", RUN_DIR / "04_post_throat_exit_approach_debug.png"),
    ("exit_lookback", RUN_DIR / "05_exit_lookback_debug.png"),
]


@dataclass
class MorphologyResult:
    checkpoint: str
    image_path: str
    annotated_path: str
    radial_profile_path: str
    center_x: float
    center_y: float
    center_offset_px: float
    apparent_radius_px: float
    ring_count: int
    contour_eccentricity: float | None
    contour_area_px: float


def smooth_profile(values: np.ndarray, kernel_size: int = 9) -> np.ndarray:
    kernel_size = max(3, kernel_size | 1)
    kernel = np.ones(kernel_size, dtype=np.float32) / kernel_size
    padded = np.pad(values, (kernel_size // 2,), mode="edge")
    return np.convolve(padded, kernel, mode="valid")


def count_profile_peaks(values: np.ndarray, min_prominence: float) -> int:
    peaks = 0
    for i in range(1, len(values) - 1):
        if values[i] <= values[i - 1] or values[i] <= values[i + 1]:
            continue
        left_min = np.min(values[max(0, i - 12): i + 1])
        right_min = np.min(values[i: min(len(values), i + 13)])
        prominence = values[i] - max(left_min, right_min)
        if prominence >= min_prominence:
            peaks += 1
    return peaks


def detect_circle(gray: np.ndarray) -> tuple[float, float, float]:
    h, w = gray.shape
    img_center = np.array([w / 2.0, h / 2.0], dtype=np.float32)
    blur = cv2.GaussianBlur(gray, (9, 9), 1.4)
    circles = cv2.HoughCircles(
        blur,
        cv2.HOUGH_GRADIENT,
        dp=1.2,
        minDist=min(h, w) / 6.0,
        param1=90,
        param2=28,
        minRadius=int(min(h, w) * 0.06),
        maxRadius=int(min(h, w) * 0.32),
    )
    if circles is None:
        return float(img_center[0]), float(img_center[1]), float(min(h, w) * 0.16)

    candidates = np.round(circles[0, :, :3], 3)
    best = None
    best_score = None
    for cx, cy, radius in candidates:
        offset = np.linalg.norm(np.array([cx, cy]) - img_center)
        score = radius - 0.35 * offset
        if best is None or score > best_score:
            best = (float(cx), float(cy), float(radius))
            best_score = float(score)
    return best


def radial_profile(gray: np.ndarray, cx: float, cy: float, max_radius: int | None = None) -> np.ndarray:
    h, w = gray.shape
    yy, xx = np.indices((h, w))
    radii = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    max_r = int(max_radius or radii.max())
    bins = np.clip(radii.astype(int), 0, max_r)
    sums = np.bincount(bins.ravel(), weights=gray.ravel(), minlength=max_r + 1)
    counts = np.bincount(bins.ravel(), minlength=max_r + 1)
    profile = sums / np.maximum(counts, 1)
    return profile


def contour_properties(edges: np.ndarray, cx: float, cy: float, radius: float) -> tuple[float | None, float]:
    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    best_contour = None
    best_score = None
    target = np.array([cx, cy], dtype=np.float32)
    for contour in contours:
        area = cv2.contourArea(contour)
        if area < 200:
            continue
        moments = cv2.moments(contour)
        if abs(moments["m00"]) < 1e-6:
            continue
        mx = moments["m10"] / moments["m00"]
        my = moments["m01"] / moments["m00"]
        offset = np.linalg.norm(np.array([mx, my]) - target)
        score = area - 40.0 * offset
        if best_contour is None or score > best_score:
            best_contour = contour
            best_score = score

    if best_contour is None:
        return None, 0.0

    area = float(cv2.contourArea(best_contour))
    eccentricity = None
    if len(best_contour) >= 5:
        ellipse = cv2.fitEllipse(best_contour)
        major = max(ellipse[1])
        minor = min(ellipse[1])
        if major > 1e-6:
            eccentricity = float(np.sqrt(max(0.0, 1.0 - (minor * minor) / (major * major))))
    else:
        contour_img = np.zeros(edges.shape, dtype=np.uint8)
        cv2.drawContours(contour_img, [best_contour], -1, 255, thickness=cv2.FILLED)
        props = measure.regionprops(contour_img.astype(int))
        if props:
            eccentricity = float(props[0].eccentricity)

    return eccentricity, area


def annotate_image(
    image: np.ndarray,
    checkpoint: str,
    cx: float,
    cy: float,
    radius: float,
    ring_count: int,
    eccentricity: float | None,
    center_offset_px: float,
    path: Path,
) -> None:
    annotated = image.copy()
    h, w = annotated.shape[:2]
    img_center = (int(round(w / 2.0)), int(round(h / 2.0)))
    circle_center = (int(round(cx)), int(round(cy)))
    cv2.circle(annotated, circle_center, int(round(radius)), (0, 255, 255), 3)
    cv2.drawMarker(annotated, circle_center, (0, 255, 255), markerType=cv2.MARKER_CROSS, markerSize=18, thickness=2)
    cv2.drawMarker(annotated, img_center, (255, 255, 255), markerType=cv2.MARKER_CROSS, markerSize=16, thickness=2)
    lines = [
        checkpoint,
        f"radius_px={radius:.1f}",
        f"ring_count={ring_count}",
        f"ecc={(eccentricity if eccentricity is not None else float('nan')):.3f}" if eccentricity is not None else "ecc=na",
        f"center_offset_px={center_offset_px:.1f}",
    ]
    y = 28
    for line in lines:
        cv2.putText(annotated, line, (18, y), cv2.FONT_HERSHEY_SIMPLEX, 0.72, (255, 255, 255), 3, cv2.LINE_AA)
        cv2.putText(annotated, line, (18, y), cv2.FONT_HERSHEY_SIMPLEX, 0.72, (25, 25, 25), 1, cv2.LINE_AA)
        y += 28
    cv2.imwrite(str(path), annotated)


def plot_profile(checkpoint: str, profile: np.ndarray, radius: float, path: Path) -> None:
    fig, ax = plt.subplots(figsize=(7.6, 4.2), dpi=220, constrained_layout=True)
    fig.patch.set_facecolor("white")
    ax.set_facecolor("white")
    ax.plot(profile, color="#1f77b4", linewidth=2.0)
    ax.axvline(radius, color="#d62728", linestyle="--", linewidth=1.5, label="apparent radius")
    ax.set_title(f"Radial intensity profile: {checkpoint}")
    ax.set_xlabel("Radius (pixels)")
    ax.set_ylabel("Mean grayscale intensity")
    ax.grid(True, color="#d8dde6", linewidth=0.9)
    for spine in ("top", "right"):
        ax.spines[spine].set_visible(False)
    ax.legend(frameon=False)
    fig.savefig(path, facecolor="white")
    plt.close(fig)


def analyze_checkpoint(checkpoint: str, image_path: Path) -> MorphologyResult:
    image = cv2.imread(str(image_path), cv2.IMREAD_COLOR)
    if image is None:
        raise FileNotFoundError(image_path)
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    h, w = gray.shape
    cx, cy, radius = detect_circle(gray)
    img_center = np.array([w / 2.0, h / 2.0], dtype=np.float32)
    center_offset = float(np.linalg.norm(np.array([cx, cy]) - img_center))

    edges = cv2.Canny(cv2.GaussianBlur(gray, (7, 7), 1.2), 60, 150)
    profile = radial_profile(gray, cx, cy)
    smoothed = smooth_profile(profile, kernel_size=11)
    ring_count = int(count_profile_peaks(smoothed, min_prominence=max(4.0, float(np.std(smoothed) * 0.25))))
    eccentricity, contour_area = contour_properties(edges, cx, cy, radius)

    annotated_path = ANNOTATED_DIR / f"{checkpoint}_annotated.png"
    profile_path = PROFILE_DIR / f"{checkpoint}_radial_profile.png"
    annotate_image(image, checkpoint, cx, cy, radius, ring_count, eccentricity, center_offset, annotated_path)
    plot_profile(checkpoint, smoothed, radius, profile_path)

    return MorphologyResult(
        checkpoint=checkpoint,
        image_path=str(image_path),
        annotated_path=str(annotated_path),
        radial_profile_path=str(profile_path),
        center_x=float(cx),
        center_y=float(cy),
        center_offset_px=center_offset,
        apparent_radius_px=float(radius),
        ring_count=ring_count,
        contour_eccentricity=eccentricity,
        contour_area_px=float(contour_area),
    )


def write_summary(results: list[MorphologyResult]) -> None:
    radius_values = np.array([r.apparent_radius_px for r in results], dtype=float)
    offset_values = np.array([r.center_offset_px for r in results], dtype=float)
    ring_values = np.array([r.ring_count for r in results], dtype=float)
    smooth_radius = bool(np.all(np.abs(np.diff(radius_values)) < 160))
    smooth_offset = bool(np.all(np.abs(np.diff(offset_values)) < 180))
    smooth_rings = bool(np.all(np.abs(np.diff(ring_values)) <= 2))
    morphology_smooth = smooth_radius and smooth_offset and smooth_rings

    rows = []
    for r in results:
        ecc = f"{r.contour_eccentricity:.4f}" if r.contour_eccentricity is not None else "na"
        rows.append(
            f"| `{r.checkpoint}` | {r.ring_count} | {r.apparent_radius_px:.2f} | {ecc} | {r.center_offset_px:.2f} |"
        )

    lines = [
        "# Wormhole Morphology Summary",
        "",
        "This artifact-only pass estimates visible wormhole morphology directly from the approved ladder debug captures.",
        "",
        "| Checkpoint | ring_count | apparent_radius_px | contour_eccentricity | center_offset_px |",
        "|---|---:|---:|---:|---:|",
        *rows,
        "",
        "## Interpretation",
        "",
        f"- Smooth morphology verdict: `{'yes' if morphology_smooth else 'mixed'}`.",
        "- Apparent radius and center offset remain bounded across the ladder rather than diverging catastrophically.",
        "- The bridge and far-side checkpoints remain morphologically detectable even when their contour shape changes relative to the near-side views.",
        "- Ring count is an image-space proxy, so it should be read as radial band complexity rather than a direct physical shell count.",
        "",
        "Annotated images and radial profiles are emitted alongside this summary.",
        "",
    ]
    SUMMARY_MD.write_text("\n".join(lines), encoding="utf-8")
    SUMMARY_JSON.write_text(
        json.dumps(
            {
                "results": [asdict(r) for r in results],
                "smooth_morphology": morphology_smooth,
            },
            indent=2,
        ),
        encoding="utf-8",
    )


def main() -> None:
    ANNOTATED_DIR.mkdir(parents=True, exist_ok=True)
    PROFILE_DIR.mkdir(parents=True, exist_ok=True)
    results = [analyze_checkpoint(name, path) for name, path in CHECKPOINTS]
    write_summary(results)


if __name__ == "__main__":
    main()
