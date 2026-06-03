# Observatory Atlas

The Observatory Atlas is the visitor guide for xPRIMEray's public research space.

It is three things at once: a museum map, a scientific expedition logbook, and an interactive curriculum. Its job is to transform a collection of experiments, renders, and diagnostic outputs into a coherent story a new visitor can follow from start to finish.

---

## The Visitor Journey

```
┌─────────────────────────────────────────────────────────────────────┐
│                   ACT I — SEEING                                    │
│                                                                     │
│  Chapter 1: Dual Reality          Chapter 2: Observer Disagreement  │
│  ─────────────────────────        ────────────────────────────────  │
│  What does a wormhole look        How different is "curved" from    │
│  like from the inside?            "straight" — measured, not just   │
│  (Perception)                     described? (Measurement)          │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   ACT II — TRUSTING                                 │
│                                                                     │
│  Chapter 3: Hermetic Closure                                        │
│  ─────────────────────────────────────────────────────────────────  │
│  When does a render look correct but have zero correctly            │
│  classified pixels? (Validation)                                    │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   ACT III — UNDERSTANDING                           │
│                                                                     │
│  Chapter 4: Coherence Basin       Chapter 5: Cathedral Probe        │
│  ──────────────────────────       ─────────────────────────────     │
│  Where does the transport         How do you find and fix a         │
│  field itself refuse to           transport failure that looks      │
│  converge? (Instability map)      like a correct render?            │
│                                   (Diagnostic methodology)          │
└─────────────────────────────────────────────────────────────────────┘
```

## Chapter Index

| # | Title | Core Question | Tier | Status |
|---|-------|--------------|------|--------|
| 1 | [Dual Reality](chapters/chapter_01_dual_reality/) | What does a wormhole look like, and how do we know the bending is real? | Entry | Ready |
| 2 | [Observer Disagreement](chapters/chapter_02_observer_disagreement/) | How much does curved transport change what you see vs. straight? | Core | Ready |
| 3 | [Hermetic Closure](chapters/chapter_03_hermetic_closure/) | When is a render silently wrong, and how do you detect it? | Core | Ready |
| 4 | [Coherence Basin](chapters/chapter_04_coherence_basin/) | Where in the scene does transport refuse to converge, and why? | Advanced | Needs one render |
| 5 | [Cathedral Probe](chapters/chapter_05_cathedral_probe/) | How do you diagnose transport failures without a crash? | Advanced | Ready |

## Minimum Viable Observatory (20 minutes)

If a visitor has only 20 minutes:

1. **Chapter 1** (8 min) — Start with the wormhole. Establish the visual.
2. **Chapter 2** (7 min) — Show the disagreement. Make "curved vs. straight" measurable.
3. **Chapter 3** (5 min) — Reveal the cliff. Show that "plausible" is not "correct."

This arc — *wonder → measurement → validation* — communicates the essence of xPRIMEray without any rendering background. See [full MVO report](#minimum-viable-observatory) below.

## Atlas Files

```
observatory_atlas/
  README.md                    ← you are here
  atlas_manifest.json          ← dependency graph: artifacts → worlds → chapters
  chapters/
    chapter_01_dual_reality/   chapter.md
    chapter_02_observer_disagreement/   chapter.md
    chapter_03_hermetic_closure/        chapter.md
    chapter_04_coherence_basin/         chapter.md
    chapter_05_cathedral_probe/         chapter.md
```

## Relationship to Other Layers

```
output/                    ← raw experiment outputs (4.2 GB, lab bench)
misterylabs_artifacts/     ← curated export layer (2.7 MB, website-ready)
sample_worlds/             ← interactive world designs (design proposals)
observatory_atlas/         ← visitor journey and curriculum (this folder)
```

The atlas consumes artifacts from `misterylabs_artifacts/` and connects them to world designs from `sample_worlds/`. It does not generate new outputs — it curates and sequences what already exists.

---

## Minimum Viable Observatory

*The smallest collection of artifacts, worlds, and chapters that communicates the essence of xPRIMEray to a new visitor in 20 minutes.*

### The Three-Chapter Core

**Chapter 1 — Dual Reality (8 minutes)**
Open with beauty. Show the wormhole. Toggle the Reference Reality inset. The visitor sees two renders of the same scene — one curved, one straight. No equations. The gap between them is the claim.

- Artifact: `visuals/wormhole-dual-reality-story.png`
- World: `dual_reality_view_world` (when built)
- Entry requirement: none

**Chapter 2 — Observer Disagreement (7 minutes)**
Make the gap measurable. Switch to the observer disagreement world. The HUD shows: 30,839 pixels changed. The delta map shows blue (curved bends rays away from geometry) dominating cyan (toward) at a 9:1 ratio. The visitor now has a number for what they just saw.

- Artifact: `visuals/observer-disagreement-contact-sheet.png`
- World: `observer_disagreement_world` (when built)
- Entry requirement: Chapter 1 (needs "two transport modes" concept)

**Chapter 3 — Hermetic Closure (5 minutes)**
Reveal the cost of getting it wrong. Show the hermetic world at budget=32. The image looks reasonable. The HUD shows 0% closure. The visitor realizes they've been looking at unresolved noise. Toggle to budget=700: 100% closure, same budget, correct render. The cliff is visceral.

- Artifact: `visuals/hermetic-hit-closure-storyboard.png`
- World: `hermetic_closure_world` (when built)
- Entry requirement: any familiarity with the concept of "renderer correctness"

### What the MVO Communicates

| Claim | Where the visitor sees it |
|-------|--------------------------|
| Curved transport produces different images than straight | Chapter 1: the dual-reality toggle |
| The difference is measurable, not just visual | Chapter 2: 23.8% pixel disagreement |
| A plausible render can be completely wrong | Chapter 3: 0% closure at budget=32 |
| The error is silent — the HUD is required to detect it | Chapter 3: the Validation HUD |
| xPRIMEray was built to catch exactly this | Chapter 3: the recovery heatmap shows it can |

### What the MVO Does NOT Cover

- Where in the field instability lives (Chapter 4 — Coherence Basin)
- How to diagnose a transport failure systematically (Chapter 5 — Cathedral Probe)
- The atomic orbital GRIN rendering (separate exhibit, not in the core arc)
- The overspace milestone (visual reference, better as a site hero than a chapter)
- The recursive mirror ghost portal (pending Phase 2 scene build)

A visitor who completes the MVO knows what xPRIMEray is, why it matters, and what "correct rendering" means in the curved-transport context. Chapters 4 and 5 deepen the picture for researchers and contributors who want to understand the diagnostic methodology.
