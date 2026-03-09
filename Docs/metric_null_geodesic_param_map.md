# Metric_NullGeodesic Parameter Map

`[MetricParamMap] Amp=direct BetaScale=direct Gamma=indirect A/B/C=indirect Sigma=indirect RInner=indirect ROuter=direct CurveType=indirect`

This map is for the current runtime path in [RayBeamRenderer.cs](/c:/godot/godot_xPRIMEray/RayBeamRenderer.cs). It describes what changes the active `TransportModel=Metric_NullGeodesic` branch numerically today, not what a future physical metric model might want.

## Current mapping

| Parameter | Classification | Current effect in metric mode |
| --- | --- | --- |
| `Amp` | Direct metric influence | First enabled source only. Feeds `abs(Amp)` into the weak-field scalar proxy. Also still affects GRIN fallback/floor. |
| `CanonicalBetaScale` / beta override | Direct metric influence | First enabled source only. Resolved as `betaScaleEff = abs(override ? BetaScale : globalBeta)`. Also still affects GRIN fallback/floor. |
| `CanonicalGamma` | Indirect influence | No direct metric turn-law input. Only matters through GRIN acceleration magnitude/fallback when `CurveType=Power`. |
| `CurveA` / `CurveB` / `CurveC` | Indirect influence | No direct metric turn-law input. Only matter through GRIN acceleration magnitude/fallback when `CurveType=Polynomial`. |
| `Sigma` | Indirect influence | No direct metric turn-law input. Only matters through GRIN acceleration magnitude/fallback when `CurveType=Exponential`. |
| `RInner` | Indirect influence | No direct metric envelope use. Only affects GRIN `u`/profile evaluation and GRIN fallback/floor. |
| `ROuter` | Direct metric influence | First enabled source only. Used as the metric stub's `characteristicRadius` in the lensing envelope. Also affects GRIN `u`/profile evaluation and fallback/floor. |
| `CurveType` | Indirect influence | The metric stub does not branch on curve type directly. Curve choice only changes the GRIN profile, GRIN magnitude floor, or GRIN fallback result. |

## Important runtime caveats

- The metric stub is not purely metric yet. It always computes GRIN acceleration first, uses `max(mappedWeakFieldScalar, |grinAccel|)` as the metric scalar, and falls back to GRIN when the metric direction delta is zero or non-finite.
- Direct metric mapping uses the first enabled `FieldSourceSnap` only. Extra enabled sources still affect GRIN acceleration, but not the direct metric scalar proxy or direct metric radius envelope.
- If `GrinFilmCamera.UseFieldGrid=true` and the sample point is inside the grid, the step uses cached GRIN acceleration from `FieldGrid3D` and bypasses `StepTransport_MetricStub` for that step.

## What is effectively unused vs indirect

None of the listed canonical parameters are completely dead numerically. The non-direct controls are better described as indirect than unused, because they can still change metric-mode results through the GRIN floor/fallback path. They are not currently direct or especially physical metric knobs.

## Is "field too intense" meaningful in metric mode?

Only in a limited scaffold sense. Large `Amp` / beta / `BendScale` / `FieldStrength` can make the current weak-field proxy or GRIN floor large enough to hit the stub's turn clamp, or make GRIN fallback dominate. That is a heuristic saturation story, not a physically meaningful "metric too intense" diagnosis.

## Real tuning knobs right now

- Source-side: `Amp`
- Source-side: `CanonicalBetaScale` (or disabling override so camera/global beta drives the scalar)
- Source-side: `ROuter`

Renderer-side multipliers `BendScale` and `FieldStrength` still scale the whole scaffold strongly, but they are not canonical `FieldSource3D` parameters.
