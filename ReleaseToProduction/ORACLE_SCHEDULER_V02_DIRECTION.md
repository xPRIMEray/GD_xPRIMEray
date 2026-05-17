# Oracle Scheduler — v0.2+ Direction Note

**Status: Not scheduled. Document only. Do not implement in v0.0-pre or v0.1.**

---

## Framing

The current transport loop allocates step budget globally and traverses rows sequentially.
This is sufficient for hermetic calibration and baseline visual validation.

The v0.2+ Oracle Scheduler direction is to make traversal allocation spatially adaptive —
analogous to FEA auto-meshing, which refines the mesh where gradients, discontinuities,
or uncertainty are locally high, rather than applying uniform resolution everywhere.

---

## Adaptive traversal scheduling targets

The scheduler should eventually support scheduling across any of these spatial primitives:

| Primitive | Use case |
|---|---|
| Horizontal bands | Current default; baseline fallback |
| Vertical bands | Rotated anisotropy, vertical seams |
| Tiles | General spatial adaptivity; the core unit for most refinement |
| Voxel / field cells | Field-space adaptivity; refine where field gradient is high |
| Transport ownership regions | Refine near domain boundaries / resolver handoff seams |
| High-curvature regions | Detect tightly bent ray paths; increase step density |
| Unresolved island neighborhoods | Detect isolated unresolved patches; replay oracle paths inside them |

---

## Proposed architecture (v0.2+)

1. **Coarse full-frame pass** — low resolution traversal over all tiles; establishes baseline hit map
2. **Risk detection** — identify tiles with: high color gradient, geometry discontinuities, seam adjacency, budget-exhausted pixels, or unresolved island membership
3. **Refinement scheduling** — allocate additional step budget to risk tiles; replay traversal at higher density
4. **Oracle path replay** — inside unresolved islands, replay cached oracle paths with modified entry conditions
5. **Spatial budget accounting** — step budget allocated per-tile or per-region, not globally

---

## Relationship to current work

The `traversalRowsCompleted` metric added in v0.0-pre (to fix hermetic validation accounting)
is a precursor: it separates traversal completion from presentation state.
A spatial scheduler will need per-region completion tracking, not just a global row cursor.

The hermetic calibration chamber (missHits==0 gate) remains the correctness baseline.
Oracle scheduling must not break hermetic closure — if a refinement pass introduces escapes,
that is a regression, not an improvement.

---

## Known limitations to address before scheduling

- `_rowCursor` is a single global cursor; spatial scheduling needs per-region cursor state
- Step budget is a single global scalar; needs spatial distribution
- `_fixtureRowsCompleted` bitmask is row-granular; tile scheduling needs 2D completion tracking
- High-curvature region detection requires per-ray bend-rate logging not yet exposed

---

## References

- Current traversal: `GrinFilmCamera.cs` — `_rowCursor`, `_fixtureRowsCompleted`, `ResetRowCursor`
- Hermetic validation baseline: `tools/hermetic_observatory_observe.py`, `check_hermetic()`
- Coverage log: `[GrinBasicVisual][Coverage]` — `hermeticRuleSatisfied`, `escapedNoHitPixels`, `budgetExhaustedPixels`
