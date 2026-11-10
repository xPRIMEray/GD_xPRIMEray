---
title: Current Research Trajectory — 2026-11-01
description: Qualified baseline, provisional pixel-ledger observation, and the path toward a 3D trajectory instrument
---

# Current Research Trajectory — 2026-11-01

## Where the instrument stands

Published baseline:

`2c703a1974dde3d9e8fed64cb1d3ae48235b0c08`

The Canonical Workbench Formal acquisition produced a complete 160×90
measurement:

| Measurement | Result |
|---|---:|
| Film samples | 14,400 |
| Validated Formal queries | 521,819 |
| Godot/kernel mismatches | 0 |
| BVH mismatches | 0 |
| Recorded portal remaps | 14,057 events across 3,709 pixels |
| Pixels with recorded contact activity | 6,266 |
| Qualified terminal `HitGeometry` outcomes | 0 |

The 6,266 contacted pixels are **not qualified terminal hits**. They record
contact activity during transport. The sealed Outcome plate classified all
14,400 pixels as `MaxStepsExhausted`, so this acquisition contains zero
qualified terminal geometry-hit outcomes.

## Recorded portal remaps — provisional

![Provisional 160×90 portal-remap observation plate](assets/observatory/current_research/portal_remap_count_q3.png)

The plate counts `BoundaryRemapApplied` events per pixel. It records 14,057
remaps distributed across 3,709 pixels in 14,400 complete current attempts.

This is a deterministic observation of the passive recorder, but it is not a
sealed scientific ledger and does not extend the sealed Q deck.

It does not yet prove:

- a qualified terminal surface hit;
- optical closure;
- a complete continuous ray trajectory;
- independent authority for every recorded event;
- reflection, refraction, or multi-bounce transport beyond implemented
  production behavior.

The image is presentation mapping. Exact event counts remain recorder data.

## Authority boundary

The existing Formal acquisition, spatial witnesses, Outcome, Contact Events
and Transport Effort identities remain qualified.

The transport ledger and portal-remap plate remain provisional:

- recorder evidence is opt-in;
- question dispositions remain pending;
- the ledger reports `CanSeal=false`;
- no sealed scientific-ledger hash is claimed;
- contact observations must not be promoted into terminal-hit claims.

## Roadmap

1. **Disposition** — Measure path reach, query coverage, first-contact activity,
   termination and completeness without modifying transport or claiming new
   scientific authority.
2. **Pixel Inspector** — expose one pixel’s ordered events, completeness and
   authority state.
3. **3D Trajectory** — visualize recorded positions and segments only after
   their sampling and authority limits are explicit.
4. **Qualified Termination** — distinguish intermediate contact from a
   scientifically qualified terminal surface result.
5. **Adaptive Coverage** — use qualified outcomes to search unresolved and
   alternate path families without copying evidence between pixels.

The immediate goal is not a prettier ray trace. It is an instrument that can
show exactly what happened to one film sample, what remains unknown, and which
claims are qualified.
