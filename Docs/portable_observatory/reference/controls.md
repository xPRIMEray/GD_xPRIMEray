---
po_doc_type: reference
title: Controls
status: partial
engine_commit: "5ce15c13"
generated: false
claim_boundary: "Key bindings document host defaults; internal controls are not part of the Demo v1 public vocabulary."
---

# Controls

Public Demo v1 controls for the Godot Transport Lens host.

## Compact strip

```text
V walk/fly | F field structure | O optical-only world
G observation / film | Q probe view | Esc Workbench
```

E is **reserved / inert** for Demo v1. H remains **retired / inert**.

## Primary keys

| Key | Public name | What it does | Boundary |
|-----|-------------|--------------|----------|
| **V** | Walk / Fly | Toggles observer locomotion | Free-roam camera is not the Transport Lens |
| **F** | Field Structure | Toggles authored field overlays | Presentation only |
| **O** | Optical-only World | Hides conventional world meshes from the observer camera | Presentation only; experiment geometry remains authoritative |
| **G** | Observation / Film | Cycles Formal SNAPSHOT, LIVE, then OFF | SNAPSHOT remains deterministic and authority-qualified |
| **Q** | Probe View | Remaps a sealed Complete plate (Outcome / Contact Events / Transport Effort) | Same frame and transport; never a new acquisition |
| **Esc** | Workbench | Opens configuration and releases the mouse | Does not change experiment state |
| **E** | Reserved | No public Demo v1 action | Programmatic experiment selection remains available |
| **H** | Retired | No action | Must not be repurposed |

Internal diagnostic and tuning controls remain available to development workflows, but are not part of the Demo v1 public vocabulary.

## Operator order

1. Use **F** and **O** to choose presentation without changing the experiment.
2. Use **G** to run SNAPSHOT until Complete.
3. Use **Q** to inspect the sealed Outcome / Contact Events / Transport Effort views.
4. Use **Esc** for Workbench configuration.

**Note on G:** G is the formal SNAPSHOT commit. LIVE film is a separate preview clock.

## See also

- [Public Vocabulary](public_vocabulary.md)
- [Running the Observatory](../development/running_the_observatory.md)
- [Tuning the Region Probe](../experiments/tuning_the_region_probe.md)
