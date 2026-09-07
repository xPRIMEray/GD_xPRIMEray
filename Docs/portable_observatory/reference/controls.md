---
po_doc_type: reference
title: Controls
status: partial
engine_commit: "5ce15c13"
generated: false
claim_boundary: "Key bindings document host defaults; some keys may be partially wired. Planned binds are labeled."
---

# Controls

Public control legend for the Godot Transport Lens host. Internal names only where useful.

---

## Compact strip

```text
E experiment · F field structure · G SNAPSHOT · Q probe views
N display · , . 0 1 field · Tab inspector · Esc evidence
P probe · J/K region · V walk/fly
```

H (Hermetic presentation) is **retired / inert**. Do not teach H as a scene loader.

---

## Primary keys

| Key | Public name | What it does | Boundary |
|-----|-------------|--------------|----------|
| **E** | Experiment | Loads authored experiment context (geometry, field, optional viewpoint) | Invalidates any open seal. Not a shader. Not H. |
| **F** | Field Structure | Authored overlay visibility | Presentation only |
| **G** | SNAPSHOT | Commits Formal G — freeze context, acquire, seal | Fresh deterministic measurement. Never trusts LIVE PixelMemory. LIVE is a different clock. |
| **Q** | Probe View | Remaps a sealed Complete plate (Outcome / Contact Events / Transport Effort) | Same frame, same transport, different questions. Not a new acquisition. |
| **N** | Display Mode | Cycles film shading (Depth / NormalRGB / NdotV / …) | Presentation only |
| **Tab** | Inspector | Opens Observation Inspector | Numbers/context; not OI PASS |
| **0** / **1** | Field ends | Field strength → STRAIGHT (0) or FULL (1) | Policy scale—not “nature max” |
| **,** / **.** | Field fine | Step field strength down / up | Field Dial control—not evaluated field map |
| **P** | Probe deeper | Region Refinement on selection | More Transport Effort—not automatic truth |
| **J** / **K** | Prev / next region | Cycle Unresolved Regions | When wired |
| **R** | Reset probe effort | Clear refinement memory for context | When wired |
| **V** | Walk / Fly | Locomotion | Free-roam camera ≠ Transport Lens |
| **[** / **]** | Plate opacity | Presentation blend | Not measurement |
| **Esc** | Evidence Console | Workbench / release mouse | Recipe-bound evidence |

### Proposed / partial binds

| Key | Public name | Note |
|-----|-------------|------|
| **L** or **T** | Plate / Transport Lens | Proposed mnemonic replacements for **G**; until the input map is updated, **G** is the active binding |
| **O** | Orientation display | Proposed dedicated orientation cycle; until bound, use **N** |
| **D** | Depth display | Proposed shortcut; until bound, use **N** |

**Note on G:** G is Formal SNAPSHOT commit. LIVE film is a separate preview clock. Do not remap G for elegance.

---

## Operator order (safe)

1. **E** Experiment → pose + field → **G** SNAPSHOT until Complete.
2. **Tab** histogram / validity.
3. **N** only after you know outcomes (display does not change classes).
4. **P** only on outcome-defined Unresolved Regions.
5. **Esc** for fixture recipes—not free-roam PASS.

---

## See also

- [Public Vocabulary](public_vocabulary.md)
- [Running the Observatory](../development/running_the_observatory.md)
- [Tuning the Region Probe](../experiments/tuning_the_region_probe.md)
