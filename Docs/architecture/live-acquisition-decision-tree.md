---
title: Live Acquisition Decision Tree
description: Canonical explanation of how a pixel becomes evidence — LIVE adaptive acquisition vs Formal G SNAPSHOT
---

# Live Acquisition Decision Tree

**Canonical architecture page.** GitHub Pages Home summarizes this tree. Do not maintain a second contradictory version.

xPRIMEray does not ask every pixel the same question forever.

The Observatory is evolving toward an **adaptive acquisition** model: confirmed hits seed where the instrument looks next. Neighbors inherit **priority**, not measured values.

> A hit changes where the instrument looks next, not what its neighbors are measured to be.

> The image grows around evidence.

| Label | Means |
|---|---|
| **LIVE NOW** | In the current host, this path is the live film clock |
| **LANDED FOUNDATION** | Qualified in the current engine base (`717e7230`) |
| **IN DEVELOPMENT** | Work in the tree; **no landing commit yet** |
| **PLANNED** | Architecture, not implemented |
| **FORMAL ONLY** | Sealed SNAPSHOT path; LIVE memory must not influence it |

---

## Two clocks of looking, one clock of proof

| Clock | Owns | Must not |
|---|---|---|
| **Interaction** | WASD / mouse / Godot scene response | wait on full-plate optical work |
| **Live film** | Presentation-oriented optical preview | pretend to be a sealed measurement |
| **Formal G** | Deterministic immutable SNAPSHOT | trust LIVE PixelMemory or Meander |

P2/P3 compute work affects **live film** only.

**Formal G SNAPSHOT** is always a fresh deterministic measurement. It never trusts LIVE preview memory.

---

## Two scales of discovery

| Scale | Space | Question | Status |
|---|---|---|---|
| **Cathedral Probe** | World-space observer ↔ object | Which objects can this observer reach? | **PLANNED** as an object-seeding scheduler. Existing Cathedral Probe overlays and Probe Views are a different, already-landed instrument. |
| **Pixel Meander** | Image-space around confirmed seeds | Where should LIVE look next? | **PLANNED** as a scheduler. **PixelMemory-v1** (hit entity IDs and object-seed/frontier foundation) is the next planned memory extension. |

Conceptual relationship (architecture, not a claim of shipped code):

```text
Observer
  → candidate reachable objects          [Cathedral Probe · PLANNED]
  → first bounded seed hit for Object A  [LIVE contact · LANDED FOUNDATION]
  → first bounded seed hit for Object B
  → each object grows its own image-space frontier  [Pixel Meander · PLANNED]
  → unresolved magenta regions remain available for later search
```

Do not conflate:

| Phrase | Means |
|---|---|
| Same frame. Same transport. Different questions. | **Q** Probe Views over **one** sealed acquisition |
| Same experiment. Different observer. | Third Observer identity — a different measurement context |
| A hit changes where the instrument looks next. | LIVE acquisition **priority**, not copied neighbor samples |

---

## Live Acquisition Decision Tree

<span class="xp-status xp-status--live">LIVE NOW</span> path, with later stages labeled.

```text
Observer + field + experiment
        │
        ▼
LIVE transport context                          LANDED FOUNDATION
        │
        ▼
PixelMemory context check                       SHADOW INSTRUMENTATION · LANDED (ca9d3a51)
        │
        ▼
acquisition policy
  SAFE / BALANCED / MAX / CUSTOM                LANDED FOUNDATION (Compute Envelope)
        │
        ▼
pixel / region request
  row-budget interaction authority              LANDED FOUNDATION
  meander / seed priority                       PLANNED
        │
        ▼
Pass1 transport                                 LANDED FOUNDATION
        │
        ▼
geometry TLAS                                   LANDED FOUNDATION
        │
        ▼
LIVE broadphase  (OverlapOnly)                  LANDED FOUNDATION (717e7230)
        │
        ▼
contact test
        │
        ├─ hit          → confirmed LIVE contact evidence
        └─ no-hit       → reason recorded (miss / prune / budget)
                │
                ▼
        PixelMemory update                      SHADOW MEMORY · LANDED
                │
                ▼
        object / frontier importance            PLANNED (Meander)
                │
                ▼
        next work request
```

<div class="xp-tree" aria-label="Live acquisition decision tree">
  <ol class="xp-tree__list">
    <li><span class="xp-status xp-status--landed">Landed</span> Observer + field + experiment → LIVE transport context</li>
    <li><span class="xp-status xp-status--landed">Shadow instrumentation · landed</span> PixelMemory context check</li>
    <li><span class="xp-status xp-status--landed">Landed</span> Acquisition policy (Compute Envelope)</li>
    <li><span class="xp-status xp-status--landed">Landed</span> Pixel / region request (interaction row budget)</li>
    <li><span class="xp-status xp-status--planned">Planned</span> Meander / seed priority</li>
    <li><span class="xp-status xp-status--landed">Landed</span> Pass1 transport → geometry TLAS → LIVE OverlapOnly broadphase → contact test</li>
    <li><span class="xp-status xp-status--landed">Landed</span> Hit / no-hit reason</li>
    <li><span class="xp-status xp-status--landed">Shadow memory · landed</span> PixelMemory update</li>
    <li><span class="xp-status xp-status--planned">Planned</span> Object / frontier importance → next work request</li>
  </ol>
</div>

---

## Formal G SNAPSHOT — isolated

<div class="xp-formal-box">

**FORMAL ONLY**

```text
G  SNAPSHOT
  fresh deterministic transport
  sealed observer context (ObserverId in ProbeContextKey v2)
  BVH-v0 contact authority
    witness: LinearScan-v0
    secondary: GodotPhysics/DeterministicReplay-v1
  Contact Events / Outcome / Transport Effort   (Q remaps; does not reacquire)
  no PixelMemory influence
  no Meander influence
  Complete = census finished, not truth proven
```

Formal G does not become parallel because LIVE Pass1 has worker ceilings. A stage ceiling is **permission, not obligation**, and it never writes the sealed plate.

</div>

---

## Object-seed / Pixel Meander

<span class="xp-status xp-status--planned">PLANNED</span> as a LIVE scheduler. The picture below is **pedagogy**, not a screenshot of a shipped Meander pass.

A confirmed hit does **not** paint its neighbors. It raises the **priority** of looking next door.

<div class="xp-meander" role="img" aria-label="Two independent object frontiers blooming on a magenta unresolved plate">
  <svg viewBox="0 0 640 220" xmlns="http://www.w3.org/2000/svg">
    <rect width="640" height="220" fill="#2a1848"/>
    <text x="16" y="22" fill="#c4b0e0" font-size="11" font-family="ui-monospace, monospace" letter-spacing="0.12em">MAGENTA / UNRESOLVED PLATE</text>
    <circle cx="210" cy="120" r="54" fill="#5a2f8a" opacity="0.55"/>
    <circle cx="210" cy="120" r="32" fill="#7a48b0" opacity="0.7"/>
    <circle cx="210" cy="120" r="6" fill="#e8d080"/>
    <text x="210" y="78" text-anchor="middle" fill="#e8d080" font-size="11" font-family="ui-monospace, monospace">Object A seed</text>
    <text x="210" y="188" text-anchor="middle" fill="#d8c4f0" font-size="10" font-family="ui-monospace, monospace">frontier A · priority only</text>
    <circle cx="430" cy="128" r="48" fill="#2f6a78" opacity="0.5"/>
    <circle cx="430" cy="128" r="28" fill="#3e8a96" opacity="0.7"/>
    <circle cx="430" cy="128" r="6" fill="#9ee8d0"/>
    <text x="430" y="86" text-anchor="middle" fill="#9ee8d0" font-size="11" font-family="ui-monospace, monospace">Object B seed</text>
    <text x="430" y="196" text-anchor="middle" fill="#d8c4f0" font-size="10" font-family="ui-monospace, monospace">frontier B · independent</text>
  </svg>
  <p class="xp-meander__cap">Two confirmed hits. Two frontiers. They do not share measured samples. They share only the remaining LIVE budget.</p>
</div>

---

## Magenta, deferred, hit, formal background

| Plate reading | Means | Does **not** mean |
|---|---|---|
| **Magenta / unresolved** | Not enough LIVE evidence yet | Empty space, wormhole, or sealed `MaxStepsExhausted` |
| **Deferred low value** | LIVE scheduler spent the current budget elsewhere | The region is proven empty |
| **Hit** | Confirmed LIVE contact evidence | Formal `HitGeometry` / optical closure |
| **Formal background** | Only Formal G may make stronger sealed claims | LIVE miss = sealed background |

See [The Great Magenta Confusion](../portable_observatory/learn/great_magenta_confusion.md): plate color is not an outcome code.

### Deep Field

<span class="xp-status xp-status--planned">PLANNED</span> instrument class.

Ordinary LIVE looking spends budget on high-priority frontiers. **Deep Field** is a later instrument that deliberately spends more acquisition budget on otherwise deferred regions — like choosing a long exposure, not like claiming the sky is a photograph of the cosmos.

Not astrophysical equivalence. Not a Formal G substitute.

---

## Compute Envelope

<span class="xp-status xp-status--landed">LANDED FOUNDATION</span>

CPU percentage is not the goal.

| Owner | Owns |
|---|---|
| Interaction | Latency |
| Measurement | Semantics (Formal G, sealed channels, Q) |
| Compute Envelope | How aggressively LIVE may spend remaining budget on useful unresolved evidence |

SAFE / BALANCED / MAX / CUSTOM control how far LIVE acquisition may expand. The machine may offer more width than a stage can use. **Compute Envelope is permission, not obligation.**

Live Pass1 is stage-capped on the current qualified host/workload (`workers=live:6/stage:6/global:12/host:24`). That is a LIVE film fact. It does not parallelize Formal G.

Pass2 threading and BVH live prefilter remain **deferred**.

---

## Landed foundation vs not yet

**LANDED FOUNDATION** (`717e7230` engine base):

- managed LIVE scheduling (small row quanta)
- interaction row-budget authority
- Compute Envelope (stage-aware ceilings)
- TLAS-backed LIVE geometry pruning
- LIVE OverlapOnly broadphase policy
- useful tested segments / confirmed hits
- Formal BVH-v0 authority isolated from LIVE

**LANDED FOUNDATION:**

- PixelMemory-v0 shadow instrumentation (ca9d3a51)

**NEXT PLANNED MEMORY STATE:**

- PixelMemory-v1: `hitEntityId` + object-seed / frontier foundation

**PLANNED:**

- Pixel Meander frontiers
- Cathedral Probe object-seeding scheduler
- Deep Field instrument
- Third Observer G4/G5 per-observer sealed state / switching
- progressive sparse / reprojected preview
- optical-closure acquisition policy under G

---

## Operator model

```text
E  chooses the experiment
F  reveals the field
G  measures          (Formal SNAPSHOT · fresh · sealed)
Q  interrogates      (remap of one seal · not a new take)
```

Experiment chooses the world.  
Measurement chooses the question.  
Compute chooses how aggressively the machine may answer.
