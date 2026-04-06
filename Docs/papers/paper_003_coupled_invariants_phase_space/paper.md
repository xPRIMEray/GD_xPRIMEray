---
title: Coupled Invariants and Stability Phase Space in Geometry-Aware Wormhole Transport
authors: <fill>
date: 2026-04-06
invariant: coupled_system
status: draft
related_fixtures: wormhole_prototype
---

# Paper 003: Coupled Invariants and Stability Phase Space in Geometry-Aware Wormhole Transport

## Abstract

We study the wormhole renderer in `GD_xPRIMEray` not through a single invariant, but through the coupled action of two already-validated constraints: the proto-caustic annulus and the low-value sector budget. The central result is that these contracts define a bounded operational phase space rather than a single acceptable point. Within that space, a stable region exists in which the annular optical structure is preserved while low-yield expenditure remains constrained; outside it, the system either wastes computation or suppresses structure too aggressively. Conventional rendering validation rarely describes behavior in such terms, because correctness and efficiency are usually tested independently rather than as interacting constraints on one transport process. The present note shows that deterministic wormhole transport admits a system-level description in which stable behavior is selected by the simultaneous satisfaction of coupled invariants.

## 1. Motivation

Papers 001 and 002 each established something necessary. The first showed that the wormhole harness contains a destination-side annulus whose density, continuity, and radial separation must be preserved if the transport is to remain geometrically faithful. The second showed that there also exists a low-value outer-ring family whose query share can be bounded without harming the preserved annulus, at least within a modest operating range. Neither result, taken alone, is sufficient to describe the actual behavior of the system.

Real rendering systems do not operate under isolated conditions. They operate under multiple constraints whose interaction matters. A positive invariant can be satisfied while computation is still squandered elsewhere. A negative invariant can be satisfied while optical structure weakens. It is therefore no longer enough to ask whether one invariant passes. One must ask what kinds of behavior arise when both are imposed simultaneously.

This shift changes the conceptual frame. The problem is not merely one of tuning a parameter until an image appears acceptable. It is the problem of identifying the region of operational space in which the coupled constraints jointly select a stable rendering regime. Outside that region, one expects one of several kinds of failure: under-constrained waste, over-suppression of meaningful structure, or broader instability in which both geometric fidelity and computational discipline are lost.

The present paper therefore moves from single-invariant thinking to system-level behavior. It treats the wormhole renderer as a constrained optical-computational system whose acceptable states form a bounded phase space. The question is no longer simply what must exist or what must be limited, but how the renderer behaves when both demands are enforced together.

<!--
Perspective Alignment Notes
- Penrose: geometry should define the allowable states of the system, not merely decorate a computational procedure.
- Bandyopadhyay: stability should be read as coherent persistence across repeated runs, not as a lucky local success.
- Orch OR: observer-facing structure emerges from constrained selection among possible histories, but only where the constraints are measurable and falsifiable.
-->

## 2. Coupled System Definition

The coupled system is defined by two pre-existing invariants:

- `I1`: proto-caustic invariant
- `I2`: low-value sector budget

The first is the positive constraint introduced in Paper 001. It requires that the destination-side annulus at:

- `layer = 1`
- `radial_bin = 3`

continue to satisfy threshold conditions on:

- hit density
- hit continuity ratio
- positive-overlap continuity ratio
- radial gradient

The second is the negative constraint introduced in Paper 002. It requires that the designated low-value outer-ring family:

- `layer = 0`
- `radial_bin = 3`

remain below an allowed query-share bound derived from deterministic baseline measurement:

- `baseline_query_share = 0.4011`
- `max_query_share_scale = 0.9`
- `maximum_allowed_query_share = 0.361`

The coupled system condition is therefore simple to state:

- `I1` must pass
- `I2` must pass

Yet the consequences are richer than that brief statement suggests. The invariants are not independent tests applied to unrelated regions. They constrain a single wormhole transport process viewed from two complementary directions. `I1` protects a high-value annular concentration. `I2` prevents recurrent low-yield sectors from reclaiming too large a share of pass-2 expenditure. The acceptable state of the renderer is thus not a scalar optimum, but a bounded region in which preservation and suppression coexist without contradiction.

<!--
Perspective Alignment Notes
- Penrose: the coupled system should read as a geometry-defined admissible set.
- Bandyopadhyay: the system condition is meaningful because both invariants persist across repeated deterministic realizations.
- Orch OR: consistent histories are those that satisfy both preservation and bounded suppression, not those that merely satisfy one.
-->

## 3. Experimental Method

The experimental method reuses the deterministic wormhole harness established in the previous notes. Camera transform and input are fixed. The scene remains static. GRIN transport, remap logic, broadphase policy, and hit acceptance rules are unchanged. What varies is the operating point of the low-value throttle and the resulting behavior of the coupled invariant system.

The most informative practical sweep presently available is along throttle strength. In particular, the harness has already yielded a meaningful sequence across:

- `period = 1`
- `period = 2`
- `period = 3`

for the low-value family:

- `layer = 0`
- `radial_bin = 3`
- `theta bins = {13,14,15,0}`

The interpretation of these settings is straightforward. `period = 1` corresponds to the unthrottled or baseline case for that region. `period = 2` introduces the currently retained deterministic suppression. `period = 3` applies a stronger suppression that has already been observed to degrade the overall operating point.

For each run, the coupled system is evaluated by recording:

- proto-caustic invariant pass/fail
- low-value sector budget pass/fail
- key optical metrics
  - hit density
  - hit continuity
  - positive-overlap continuity
  - radial gradient
- key performance metrics
  - `pass2.query`
  - `pass2.physics`
  - `geom_hits`
  - `final_write_px`

The present note therefore treats the harness as a phase-space probe. Each operating point is mapped not only by its timing and hit counts, but by whether it falls into a stable or unstable region of the coupled invariant system.

<!--
Perspective Alignment Notes
- Keep the language concrete and implementation-facing.
- The phase-space language must remain tied to actual sweepable harness parameters.
- Let the method read as a disciplined reuse of existing deterministic infrastructure, not as an abstract analogy.
-->

## 4. Results: Phase Space

The coupled system admits a useful phase-space description. At the present level of resolution, four logical regions may be defined:

- `stable`: `I1` passes and `I2` passes
- `over-suppressed`: `I2` passes and `I1` fails
- `under-constrained`: `I1` passes and `I2` fails
- `unstable`: both `I1` and `I2` fail

The current measured wormhole harness already identifies a nontrivial subset of this space.

### Figure A

![Figure A — Main Render](../../../output/wormhole_test/figures/figure_A_main_render.png)

Figure A remains the observer-facing reference image. It shows what the coupled system must continue to support: not merely any image, but one whose preserved annulus remains visible in the downstream structure of the render.

### Figure B

![Figure B — Composed Render with Research Inset](../../../output/wormhole_test/figures/figure_B_composed_overlay.png)

Figure B makes the system state explicit through the overlay and contract indicators. The viewer is no longer seeing a free-running render, but a point in a constrained operating space.

### Figure C

![Figure C — Ring Density](../../../output/wormhole_test/figures/figure_C_ring_density.png)

Figure C is where the coupled description becomes legible. It shows the annular structure that `I1` protects and, by implication, the portal-local geometry relative to which `I2` limits low-value expenditure. It therefore functions as the geometric backbone of the phase-space interpretation.

### Figure D

![Figure D — Metrics Table](../../../output/wormhole_test/figures/figure_D_metrics_table.png)

Figure D compactly presents the current coupled operating point by placing both contracts and the active throttle profile in the same frame as the performance metrics. This turns the quartet into a system-level report: image, explanation, structure, and coupled state.

If one maps the currently observed throttle settings textually, the phase-space picture is already informative:

| Operating Point | `I1` Proto-Caustic | `I2` Low-Value Budget | Observed State |
|---|---|---|---|
| `period = 1` | pass | fail or weaker control of low-value share | under-constrained |
| `period = 2` | pass | pass | stable |
| `period = 3` | formally pass but weakened metrics and hit/write drift | pass | boundary toward over-suppression / rejected operating point |

This table is not yet a dense 2D numerical map, but it is already a genuine phase-space description. It identifies a stable region, a too-weak region, and a too-strong boundary.

<!--
Perspective Alignment Notes
- Penrose-style emphasis: the renderer occupies allowable or disallowed geometric-computational states.
- The figures should still feel like coordinated projections of one system.
- Keep the mapping concrete even when the phase-space language becomes more abstract.
-->

## 5. Key Observation

The key observation is that the retained operating point at `period = 2` lies in a stable region, whereas the stronger setting at `period = 3` does not.

At `period = 2`:

- the proto-caustic annulus remains preserved
- the low-value sector budget passes
- `pass2.query` and `pass2.physics` improve relative to the weaker operating point
- `geom_hits` and `final_write_px` remain stable

At `period = 3`:

- the low-value budget still passes
- the annulus metrics weaken
- `pass2.query` worsens
- `pass2.physics` worsens
- `geom_hits` and `final_write_px` drift downward

The significance of this comparison is not merely practical. It shows that the coupled system has a bounded stability region rather than a monotone trade-off. Too little suppression leaves the low-value family insufficiently constrained. Too much suppression injures the very structure the positive invariant was meant to preserve. Correct behavior is therefore not obtained by turning one knob until cost becomes small. It is selected by remaining within a constrained region in which the two invariants remain jointly compatible.

## 6. Discussion

The coupled invariant system behaves like a constrained dynamical system in the minimal sense relevant here. The renderer is not evolving in continuous physical time in the manner of a classical phase-flow model, but its admissible operating states are nonetheless shaped by interacting constraints that define a bounded region of stability. This is already enough to justify the phase-space language.

What matters most is that the invariants are not independent. The positive invariant is not simply a correctness ornament, and the negative invariant is not simply an efficiency add-on. Each changes the interpretation of the other. The annulus tells us where optical structure concentrates. The low-value budget tells us where recurrent expenditure must remain bounded. Together they define an allowable state rather than two separate checkboxes.

This also reframes optimization. A parameter change is not good merely because one metric improves. It is good only if it moves the system within, or deeper into, the stable coupled region. Likewise, a change is not acceptable merely because one contract still passes formally. If it drifts toward a boundary where structure weakens and hits decline, the phase-space view reveals that the system is leaving the stable region even before outright failure occurs.

There is a restrained interpretive consequence here. The stable region resembles a coherent attractor in the sense that repeated deterministic runs return to the same bounded operating point when the constraints and harness are held fixed. One should not overstate that analogy. Yet it is a useful one: the system is not wandering arbitrarily through parameter space. It is being selected into a constrained, reproducible regime by the interaction of geometric preservation and bounded suppression.

<!--
Perspective Alignment Notes
- Penrose: geometric constraints define the allowable states of the system.
- Bandyopadhyay: the stable region resembles a coherent temporal attractor across repeated realizations.
- Orch OR: one may speak of consistent histories only insofar as the coupled constraints select them reproducibly and measurably.
-->

## 7. Conclusion

We defined the wormhole renderer as a coupled invariant system in which the proto-caustic annulus and the low-value sector budget must both be satisfied.  
We showed that the current retained operating point lies in a stable bounded region, while stronger suppression moves the system toward structural degradation rather than deeper improvement.  
This matters because geometry-aware wormhole rendering can now be described not as parameter tuning, but as selection within a constrained operational phase space.

## Appendix A

Current sweep table from observed deterministic harness points:

| Throttle Region | Theta Bins | Period | `I1` Proto-Caustic | `I2` Budget | Notes |
|---|---|---:|---|---|---|
| `layer=0`, `radial_bin=3` | `{13,14,15,0}` | `1` | pass | weaker or not yet bounded | under-constrained reference |
| `layer=0`, `radial_bin=3` | `{13,14,15,0}` | `2` | pass | pass | current stable operating point |
| `layer=0`, `radial_bin=3` | `{13,14,15,0}` | `3` | degraded boundary behavior | pass | rejected due to weaker annulus metrics and hit/write drift |

Optional future phase-space expansion:

- add a second sweep axis from low-value budget scaling
- record explicit region labels in a dedicated phase-space artifact
- introduce Figure E as a 2D stability map once enough deterministic sweep points exist

<!--
Perspective Alignment Notes
- Appendix stays operational and comparative.
- No metaphysical language should enter the sweep table.
-->
