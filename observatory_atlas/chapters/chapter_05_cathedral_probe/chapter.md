# Chapter 5 — Cathedral Probe

**Act:** III — Understanding  
**Role in arc:** Synthesis. Provides the diagnostic methodology that connects all previous chapters: how to find transport failures from rendered output alone, without the oracle.  
**Status:** Ready — canonical images exist in `Docs/assets/cathedral_probe/` and in `misterylabs_artifacts/`. The runtime per-layer toggle is not yet implemented.

---

## Core Question

*How do you diagnose a renderer that produces wrong pixels without crashing — when you can't afford to run the oracle on every frame?*

---

## What Is the Visitor Investigating?

Chapters 1–4 built up a picture of transport failures: visible disagreement (Chapter 2), silent closure failures (Chapter 3), topological instability zones (Chapter 4). The Cathedral Probe is the diagnostic framework built to detect all three from the rendered output alone — without requiring a separate oracle pass.

The core insight from the Cathedral Probe architecture: **transport instability is not globally smoothable. It is localized, topological, and scheduler-amplified.** Finding it requires a layered observatory — multiple passive instrumentation passes assembled into a composite that makes transport coherence structure legible as a visual space.

This chapter's core image is the six-layer Cathedral Probe composite: a single frame that synthesizes coherence vectors, normal discontinuity, curvature accumulation, domain ownership, transport seams, and step budget allocation into one readable diagnostic.

---

## Observation

**The scheduler resonance finding (the entry to this chapter):**

The DOE Scheduler Resonance experiment (68 cells, stride × step length × scene) revealed that transport banding is not caused by integration precision — it is caused by traversal cadence. Stride=1: 33% band coverage. Stride=4: 0.22% band coverage. The parameter controlling the most visible artifact in the renders is the *scheduling stride*, not the *physics step length*.

This is counterintuitive: making the integration finer (smaller step) makes the band *larger*, not smaller. Finer steps expose more transport boundary structure, which the row-major scheduler then amplifies into wider horizontal bands.

The heatmap (`doe-scheduler-resonance-heatmap.png`) makes the periodic collapse of band coverage visible as a function of row-mod-stride. The solution is scheduler decorrelation: the tile scheduler breaks the row-alignment that enables resonance.

**The six-layer Cathedral Probe composite:**

The canonical composite image (`Docs/assets/cathedral_probe/cathedral_probe_overlay_row_0015.png`) shows the domain-resolver-stress scene at step=0.015, row traversal, all six layers composited:

1. **Beauty render** — the raw integration output
2. **Cartesian wireframe** — geometric boundary structure
3. **Transport ownership map** — per-pixel domain ownership coloring
4. **Risk probe markers** — high-risk transport nodes from the oracle
5. **Spacetime transport diagram** — ray-path topology in scene space
6. **Transport continuity vectors** — 6,619 high-discontinuity vectors (score ≥ 1.0) clustered at ownership boundaries

All six identified shape regions confirm: `boundary_aligns_with_high_vector_density = true`. The continuity vectors are a non-oracle proxy for the oracle's risk regions — they identify the same instability zones as Chapter 4, without requiring a separate oracle run.

**The traversal comparison:**
The four-mode traversal contact sheet shows row, column, tile, and checkerboard traversal at step=0.015. Band coverage: row 20.2%, column 10.8%, tile 10.1%, checkerboard 7.9%. Scheduler decorrelation reduces banding across all non-row modes. Corner instability (precision 0.003125, 468 ownership-change samples) persists unchanged — it is topological, not scheduler-amplified.

**The link to Chapter 4:**
The corner instability in the traversal comparison is the same instability identified in Chapter 4's coherence basin. It persists at 468 ownership-change samples regardless of traversal mode — because it is a topological feature, not a scheduling artifact. The Cathedral Probe identifies it; the oracle confirms it; the coherence basin maps it. Three independent methodologies, same finding.

---

## Artifacts

**In `misterylabs_artifacts/`:**

| File | Role |
|------|------|
| `visuals/doe-scheduler-resonance-heatmap.png` | **Entry point.** The resonance collapse heatmap — stride=4 vs stride=1. |
| `visuals/doe-scheduler-resonance-stride-plot.png` | **Quantitative.** Band coverage vs. stride — the core DOE finding. |
| `datasets/doe-scheduler-resonance.csv` | **Data.** 68-cell sweep results. |
| `cards/doe-scheduler-resonance.md` | **Card text.** |

**In `Docs/assets/cathedral_probe/` (repo-tracked, referenced for this chapter):**

| File | Role |
|------|------|
| `cathedral_probe_overlay_row_0015.png` (16 KB) | **Primary.** Six-layer composite diagnostic overlay. |
| `cathedral_probe_contact_sheet_row_0015.png` (64 KB) | **Contact sheet.** All six layers shown individually. |
| `continuity_vectors_row_0015.png` (8 KB) | **Key layer.** Transport continuity vectors — the non-oracle instability detector. |
| `traversal_contact_sheet_4mode_0015.png` (60 KB) | **Scheduler story.** Four modes showing band-coverage reduction. |
| `scheduler_resonance_stride_heatmap.png` (112 KB) | **Resonance.** Same measurement as the promoted misterylabs artifact. |
| `band_support_by_mode_0015.png` (8 KB) | **Summary.** Band coverage reduction: row → tile → checkerboard. |

*Note: The `Docs/assets/cathedral_probe/` images should be promoted to `misterylabs_artifacts/` in the next curation pass. They are already repo-tracked and sized for web. This is the chapter's primary remaining curation task.*

---

## Sample World

**`cathedral_probe_world`** — `sample_worlds/cathedral_probe_world/`

Scene: `test-domain-resolver-stress.tscn`

The world provides individual layer toggles for each of the six Cathedral Probe components. The visitor can enable them one at a time, in the sequence described in the chapter, building the composite progressively. The stride selector makes the resonance effect live: switching from stride=1 to stride=4 shows the band-coverage collapse in the step-budget-allocation layer before it appears as visible banding in the beauty render.

**Current status:** Design proposal. The six-layer composite exists as a static image. Runtime per-layer toggling requires new implementation in the GrinFilmCamera/FilmOverlay2D pipeline.

---

## Validation Question

*At stride=4, step=0.015, row traversal: what percentage of pixels should fall in the high-curvature band?*

Expected: approximately 0.22–0.45% (from the DOE data across step lengths). At stride=1: 20–33%. The visitor can observe this collapse by toggling the stride selector and watching the step-budget-allocation heatmap develop periodic horizontal stripes.

*Link to Chapter 4:* The corner instability visible in the traversal contact sheet should show 468 ownership-change samples, consistent across all traversal modes. If corner instability varies with traversal mode, it has a scheduling component that was not present in the Chapter 4 oracle probe — worth investigating.

---

## Key Insight

**Transport instability is not globally smoothable. It is localized, topological, and scheduler-amplified. The right response is scheduler decorrelation first, local precision management second, global smoothing never.**

This is the Cathedral Probe's core architectural finding — stated in the abstract, proved through 14 sections of layered evidence, and visible in a single composite image.

---

## Chapter Synthesis

Chapter 5 closes the atlas arc.

- Chapter 1 showed that curved transport is beautiful and real.
- Chapter 2 showed it is measurably different from straight transport.
- Chapter 3 showed that "measurably different" is not the same as "measurably correct."
- Chapter 4 showed that some transport regions cannot be made correct by brute force.
- Chapter 5 showed how to find those regions systematically and fix the scheduler that amplified them.

The visitor who has followed all five chapters now understands xPRIMEray's complete transport validation story: from first principles (bent light) through correctness guarantees (hermetic closure) through failure analysis (coherence basin) through diagnostic methodology (Cathedral Probe).

**What comes after Chapter 5:**

The recursive mirror ghost portal (pending Phase 2 scene build) is the first exhibit that requires everything from all five chapters to interpret: it uses PerfectMirrorReflection and DielectricRefraction at discrete boundaries while simultaneously running GRIN integration between bounces. Its correctness must be validated hermetially, its instability regions must be mapped with the coherence probe, and its failure modes must be diagnosed with the Cathedral Probe methodology.

It is the synthesis artifact — and it will become Chapter 6 when its benchmark image exists.
