# xPRIMEray Logo Usage Recommendations

*Audit of available logo and brand assets. Recommendations for placement across documentation, in-game, report templates, and media production.*

---

## Available Logo Assets

All files in `Docs/assets/`.

| Asset | Path | Size | Format | Description |
|---|---|---|---|---|
| **Primary logo (PNG)** | `Docs/assets/xPRIMEray-LOGO.png` | 904 KB | PNG (raster) | Full-color xPRIMEray wordmark/logo. Primary brand asset. |
| **Dark-background logo** | `Docs/assets/xprimeray-logo-dark.png` | 3.1 MB | PNG (raster) | Same logo optimized for dark/black backgrounds. |
| **Vector logo (SVG)** | `Docs/assets/xprimeray-logo.svg` | 4.2 KB | SVG (vector) | Scalable vector. Preferred for any context where size varies. |
| **Icon (SVG)** | `Docs/assets/xprimeray-icon.svg` | 3.4 KB | SVG (vector) | Square icon variant — used as MkDocs site favicon/logo. |
| **Blueprint render** | `Docs/assets/xprimeray-blueprint.png` | 4.7 MB | PNG (raster) | Technical blueprint–style render. Evocative of field topology. |
| **AI concept image** | `Docs/assets/ChatGPT Image Apr 20, 2026, 12_26_28 AM.png` | 591 KB | PNG | AI-generated concept. Atmospheric, not a wordmark. |

The SVG icon is already wired: `mkdocs.yml` `theme.logo` and `theme.favicon` both point to `assets/xprimeray-icon.svg`.

---

## Recommendations by Context

### 1. README header (root `README.md` / `Docs/README.md`)

**Recommended:** `Docs/assets/xprimeray-logo.svg` or `Docs/assets/xPRIMEray-LOGO.png`

Use the SVG for the root README so it scales cleanly on GitHub's light/dark backgrounds. If the root README is dark-themed or the repo header is on a dark page, use `xprimeray-logo-dark.png`. Embed with a centered HTML block:

```html
<p align="center">
  <img src="Docs/assets/xprimeray-logo.svg" alt="xPRIMEray" width="480">
</p>
```

For `Docs/README.md` (MkDocs source), use the SVG at relative path `assets/xprimeray-logo.svg`.

### 2. MkDocs documentation cover / index.md hero

**Recommended:** `Docs/assets/xprimeray-logo.svg`

Already in use as `theme.logo` (site header) and `theme.favicon`. For the `index.md` hero section, add a small centered logo above the H1 using a markdown image tag. Keep it small (200–300 px wide) so it doesn't compete with the Cathedral Probe contact sheet hero image below it.

### 3. In-game splash / debug overlay

**Recommended:** `Docs/assets/xprimeray-icon.svg` (icon variant)

For a debug overlay corner watermark: the icon SVG is small and square — ideal as a corner stamp in `FilmOverlay2D` or in the HUD label set constructed by `GrinFilmCamera`'s `DebugOverlayBus.AddText` calls. Embedding an SVG directly into Godot requires rasterization first; a 64×64 or 128×128 PNG export of the icon would be the practical target.

For a splash screen (if added to the WormholePrototypeRig loading sequence), use `xPRIMEray-LOGO.png` centered on a black background with a short fade.

### 4. Report templates (Python diagnostic outputs)

**Recommended:** `Docs/assets/xPRIMEray-LOGO.png` or `xprimeray-logo-dark.png`

The Python analysis tools currently produce matplotlib figures without a watermark. Adding the logo to diagnostic contact sheets (e.g., `diagnostic_overlay_contact_sheet.png`, `scheduler_stride_plot.png`) would make archived outputs self-identifying when shared outside the repo.

Implementation: in `tools/diagnostic_wireframe_overlay.py` and similar, add a small logo stamp in the bottom-right corner via `plt.figimage(logo_img, xo=..., yo=..., alpha=0.55)` or an axes inset. Use the dark variant for figures with dark backgrounds (heatmaps); use the primary logo for light-background matplotlib plots.

### 5. Milestone gallery and contact sheets

**Recommended:** `Docs/assets/xprimeray-blueprint.png` (background), `xPRIMEray-LOGO.png` (watermark)

The blueprint render is 4.7 MB — too large to embed directly in documentation pages, but appropriate as a background image in a milestone gallery HTML page or a presentation deck title slide. Pair it with the wordmark logo as a centered overlay.

For the curated contact sheets already in `Docs/assets/cathedral_probe/` and `Docs/assets/transport_islands/`, a small corner watermark from the icon SVG would be tasteful — non-intrusive but identifiable.

### 6. Video thumbnails / social media

**Recommended:** `Docs/assets/xprimeray-logo-dark.png` (on dark background) or `xPRIMEray-LOGO.png` (on light)

The AI concept image (`ChatGPT Image Apr 20, 2026, 12_26_28 AM.png`) is well-suited as a video thumbnail background — it reads as "physics + space" at thumbnail scale. Composite the wordmark logo over it at ~40% width, bottom-left anchor.

For YouTube or social post thumbnails: use a 1920×1080 canvas with the concept image as background and the wordmark logo in a corner. The Cathedral Probe six-layer contact sheet (`cathedral_probe_contact_sheet_row_0015.png`) is a strong thumbnail for a technical video — it reads as "diagnostic data" at a glance.

---

## Asset Size Guidance

The 3.1 MB dark logo and 4.7 MB blueprint are too large for direct web embedding. If added to MkDocs pages, resize to ≤ 400 KB before committing. The SVG files and the 904 KB primary logo are acceptable for web use.

| Asset | Web-safe? | Notes |
|---|---|---|
| `xprimeray-logo.svg` | ✅ Yes | Preferred for all web contexts |
| `xprimeray-icon.svg` | ✅ Yes | Already in use as favicon |
| `xPRIMEray-LOGO.png` | ✅ Yes (904 KB) | Acceptable for hero images |
| `xprimeray-logo-dark.png` | ⚠️ Large (3.1 MB) | Resize before web embed |
| `xprimeray-blueprint.png` | ⚠️ Very large (4.7 MB) | Resize before web embed; use for offline/print |
| ChatGPT concept | ✅ Yes (591 KB) | For decorative/thumbnail use |

---

## Quick Placement Checklist

- [ ] Root `README.md` header — add `xprimeray-logo.svg` centered above title
- [ ] `Docs/index.md` — add logo above H1 heading
- [ ] Python diagnostic tools — add `xPRIMEray-LOGO.png` watermark to output contact sheets
- [ ] In-game HUD debug corner — add icon (export icon SVG → 64×64 PNG)
- [ ] MkDocs already covered — icon SVG wired as logo and favicon
- [ ] Milestone gallery page — use blueprint as background if a gallery page is created
- [ ] Archived fixture contact sheets — add icon stamp (optional, non-urgent)
