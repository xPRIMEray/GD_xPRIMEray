# Basic Visual Off-axis Report (2026-03-15)

## Scope

- On-axis references used: GRIN sweep screenshots from March 14, 2026 and Metric sweep screenshots/logs from March 15, 2026.
- Off-axis captures used: straight, GRIN minimal/stronger, Metric minimal/stronger, and Metric stronger gain 10x from March 15, 2026.
- Off-axis change applied: camera X offset only. Field math and transport laws were not modified.

## Parallel Raw

- Metric minimal: on-axis `parallel_raw=5625160` vs off-axis `parallel_raw=4525957` (`-19.54%`).
- Metric stronger: on-axis `parallel_raw=6477137` vs off-axis `parallel_raw=5926532` (`-8.50%`).
- Metric stronger gain10: on-axis `parallel_raw=1469784` vs off-axis `parallel_raw=836859` (`-43.06%`).

## Visibility

- Metric minimal steering turns: on-axis `0` vs off-axis `20714760`.
- Metric stronger steering turns: on-axis `0` vs off-axis `20770979`.
- Metric stronger gain10 steering turns: on-axis `0` vs off-axis `20894259`.
- Whole-frame metric-vs-straight luminance deltas stay close between on-axis and off-axis captures, so the gain is not “more pixels changed overall.”
- The useful change is symmetry breaking: the off-axis comparison sheets, together with the steering-turn logs, show Metric leaving the near-radial zero-turn regime and becoming directionally biased instead of optically centered.
- Stronger gain10 is the clearest off-axis Metric example in this subset because it combines the largest `parallel_raw` reduction with the strongest mean turn (`0.054917`).

## Teaching Baseline Verdict

- `parallel_raw` decreases materially for the minimal case and strongly for the stronger gain10 case. The stronger baseline improves, but only modestly.
- Visible Metric distortion becomes clearer in the off-axis ladder. This is an inference from the nonzero steering-turn diagnostics plus the on-axis/off-axis screenshot sheets, not from a large increase in whole-frame luma difference.
- The off-axis ladder is the better teaching baseline for Metric because straight and GRIN remain readable while Metric no longer collapses into the on-axis zero-turn condition.
- Residual limitation: Metric captures still record zero source/background hits in this subset, so the off-axis ladder improves measurability more than it fully solves visibility.

## Artifacts

- On-axis vs off-axis sheet: `C:\godot\godot_xPRIMEray\screenshots\basic_visual_offaxis\2026-03-15\onaxis_vs_offaxis_sheet.png`
- Off-axis ladder sheet: `C:\godot\godot_xPRIMEray\screenshots\basic_visual_offaxis\2026-03-15\offaxis_ladder_sheet.png`
- Analysis JSON: `C:\godot\godot_xPRIMEray\logs\basic_visual_offaxis\analysis.json`
