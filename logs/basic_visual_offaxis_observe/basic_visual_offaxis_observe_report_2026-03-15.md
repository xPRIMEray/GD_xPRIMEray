# Basic Visual Off-axis Observe Report (2026-03-15)

## Capture Fix

- Root blocker: `LaunchAudit` treated the new `*-observe.tscn` harnesses as launcher/scene mismatches, so `GrinBasicVisualController._Ready()` returned before `[GrinBasicVisual][CaptureConfig]` and the observe runs never armed capture.
- Secondary gate fix retained: capture readiness now latches best-observed render-health step and processed-row values instead of relying on the transient current `FilmRowCursor`.

## Metric Observability

- Metric cases with nonzero source/background hits at capture: `0` / `3`.
- Metric cases judged easier to read than the old off-axis ladder: `0` / `3`.
- Most likely geometry change driving the improvement: `no single change isolated; the combined observe geometry did most of the work`.

## Case Readout

- `Metric minimal`: sourceHits `0` -> `0`, backgroundHits `0` -> `0`, parallel_raw `4525957` -> `4505430`, meanTurn `0.031524` -> `0.03151`.
- `Metric stronger`: sourceHits `0` -> `0`, backgroundHits `0` -> `0`, parallel_raw `5926532` -> `5929494`, meanTurn `0.029881` -> `0.029705`.
- `Metric stronger gain10`: sourceHits `0` -> `0`, backgroundHits `0` -> `0`, parallel_raw `836859` -> `837066`, meanTurn `0.054917` -> `0.05442`.

## Artifacts

- Observe contact sheet: `C:\godot\godot_xPRIMEray\screenshots\basic_visual_offaxis_observe\2026-03-15\observe_contact_sheet.png`
- Old off-axis vs observe sheet: `C:\godot\godot_xPRIMEray\screenshots\basic_visual_offaxis_observe\2026-03-15\old_vs_observe_comparison_sheet.png`
- Summary JSON: `C:\godot\godot_xPRIMEray\logs\basic_visual_offaxis_observe\summary.json`
