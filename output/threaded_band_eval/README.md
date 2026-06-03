# Threaded Band Eval

Short name: **Threaded Band Evaluation — 2 vs. 4 Threads**

## Purpose

Measures the performance and correctness of multi-threaded band evaluation: compares single-threaded baseline, 2-thread, and 4-thread configurations. Identifies whether threading introduces output divergence or race conditions in the band evaluator.

## Source / Generation Context

- Scene: `test-curved-minimal-backdrop.tscn`
- Single run folder (flat, no timestamp subdirs)

## What the Output Shows

Profile logs per configuration: `pass2_candidate_eval_baseline.log`, `pass2_candidate_eval_threaded2.log`, `pass2_local_accum_baseline.log`, `pass2_local_accum_threaded2_profile.log`, `pass2_local_accum_threaded4_profile.log`. Timing breakdowns isolate candidate evaluation vs. local accumulation cost per thread count.

## Key Files

- `pass2_candidate_eval_baseline.log` — Baseline (1-thread) candidate eval
- `pass2_candidate_eval_threaded2.log` — 2-thread candidate eval
- `pass2_local_accum_threaded2_profile.log` — 2-thread accumulation profile
- `pass2_local_accum_threaded4_profile.log` — 4-thread accumulation profile
- `curved_minimal_backdrop_threaded2.log` / `threaded4.log` — Full render logs

## Suggested MisterY Labs Card Summary

Interpretation pending — threading performance comparison for the pass-2 band evaluator at 1, 2, and 4 threads.

## Status

Test output

## Notes / Next Steps

- Extract speedup ratios from profile logs.
- Confirm no pixel divergence between thread counts (correctness gate for threading).
