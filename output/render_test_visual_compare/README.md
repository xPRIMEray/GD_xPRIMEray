# Render Test Visual Compare

Short name: **Render Test — Visual Comparison Suite**

## Purpose

Side-by-side visual comparison of render test outputs across scheduler and pruning variants. Confirms that scheduler-fast and baseline-only modes produce equivalent final images (or documents known acceptable differences).

## Source / Generation Context

- Scene: `test-curved-minimal.tscn`, `test-curved-minimal-backdrop.tscn`
- Subfolders include: `combined_evidence_2026-03-28`, `corrected_full_packet_2026-03-28`, `corrected_visual_check_2026-03-28`, `curved_minimal`, `curved_minimal_backdrop`

## What the Output Shows

Multiple PNGs per configuration: scheduler-fast vs. visual-check vs. baseline, across pruning variants. Named subfolders from March 2026 capture a specific correction investigation. The comparison identifies whether corrected scheduler behavior produces visually identical output to the reference.

## Key Files

- `*/curved_minimal__scheduler-fast__*.png` — Scheduler-fast renders
- `*/curved_minimal_backdrop__visual-check__*.png` — Visual-check renders
- `combined_evidence_2026-03-28/` — Combined evidence packet
- `corrected_full_packet_2026-03-28/` — Post-correction full packet
- `*scheduler-fast.log` / `*visual-check.log` — Mode logs

## Suggested MisterY Labs Card Summary

Interpretation pending — visual comparison suite confirming render test correctness across scheduler and pruning configurations.

## Status

Archived

## Notes / Next Steps

- March 2026 correction runs are historical. Extract key pass/fail evidence from `corrected_full_packet`.
