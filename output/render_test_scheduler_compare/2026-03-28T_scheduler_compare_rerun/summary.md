# Render Test Scheduler Compare (2026-03-28T_scheduler_compare_rerun)

Run root: `/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun`
Images: `/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun/images`
Logs: `/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun/logs`

## Cases

- curved_minimal baseline: godot_exit=0 regress_exit=0 captures=0 log=`/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun/logs/curved_minimal__baseline.log`
  regress=`PASS: /home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun/logs/curved_minimal__baseline.log windows=50, trusted=10, partial=40
ON trusted mean per-px-on=na
OFF trusted mean per-px-off=178.077
ON saved% mean (baseline-ready)=na
Audit totals: falseNeg=0 falsePos=0

summary: pass=1 fail=0

DOE summary:
A) TrustReason histogram (global)
  low_raytests: 40
  ok: 10

A) TrustReason histogram (per run)
  baseline_prune_off: ok=10
  prune_on_default: low_raytests=10
  prune_on_tight_env: low_raytests=10
  prune_on_loose_env: low_raytests=10
  prune_on_stride_off: low_raytests=10

B) Trust flip timeline
  baseline_prune_off: firstTrustedStep=30 trusted_windows=10/10 (100.0%)
  prune_on_default: firstTrustedStep=none trusted_windows=0/10 (0.0%)
  prune_on_tight_env: firstTrustedStep=none trusted_windows=0/10 (0.0%)
  prune_on_loose_env: firstTrustedStep=none trusted_windows=0/10 (0.0%)
  prune_on_stride_off: firstTrustedStep=none trusted_windows=0/10 (0.0%)

C) low_raytests diagnostics: count=40
  run=prune_on_default step=330 geomRayTestsTotalRaw=608.0 minRayTests=4096.0 geomPixProcessedRaw=1160.0 minGeomPix=1024.0 candPix/noCand=43/1117 rayTestsPerGeomPix=0.524 candRate=0.037 noCandRate=0.963
  run=prune_on_default step=360 geomRayTestsTotalRaw=608.0 minRayTests=4096.0 geomPixProcessedRaw=1120.0 minGeomPix=1024.0 candPix/noCand=43/1077 rayTestsPerGeomPix=0.543 candRate=0.038 noCandRate=0.962
  run=prune_on_default step=390 geomRayTestsTotalRaw=767.0 minRayTests=4096.0 geomPixProcessedRaw=1160.0 minGeomPix=1024.0 candPix/noCand=54/1106 rayTestsPerGeomPix=0.661 candRate=0.047 noCandRate=0.953
  run=prune_on_default step=420 geomRayTestsTotalRaw=1071.0 minRayTests=4096.0 geomPixProcessedRaw=1160.0 minGeomPix=1024.0 candPix/noCand=75/1085 rayTestsPerGeomPix=0.923 candRate=0.065 noCandRate=0.935
  run=prune_on_default step=450 geomRayTestsTotalRaw=613.0 minRayTests=4096.0 geomPixProcessedRaw=1160.0 minGeomPix=1024.0 candPix/noCand=43/1117 rayTestsPerGeomPix=0.528 candRate=0.037 noCandRate=0.963

D) Top 10 worst trust failures by deficit
  run=prune_on_tight_env step=690 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_tight_env step=720 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_tight_env step=810 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_tight_env step=840 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_stride_off step=1290 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_stride_off step=1320 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_stride_off step=1410 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_stride_off step=1440 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_default step=330 raytestsDeficit=3488.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_default step=360 raytestsDeficit=3488.000 geomPixDeficit=0.000 p2Deficit=na

RUN REPORT
  run=baseline_prune_off frames=300 warmup=30 samples=270 windows=10 trusted=10 partial=0
    firstTrustedStep=30 lastTrustedStep=300
    topReasons=ok=10
    perPxOff(mean,p95)=178.077,212.757 perPxOn(mean,p95)=na,na savedPctMean=na
  run=prune_on_default frames=300 warmup=30 samples=270 windows=10 trusted=0 partial=10
    firstTrustedStep=na lastTrustedStep=na
    topReasons=low_raytests=10
    perPxOff(mean,p95)=na,na perPxOn(mean,p95)=na,na savedPctMean=na
    low_raytests examples:
      step=330 geomRayTestsTotalRaw=608 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=360 geomRayTestsTotalRaw=608 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1120 minGeomPix=1024
      step=390 geomRayTestsTotalRaw=767 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
  run=prune_on_tight_env frames=300 warmup=30 samples=270 windows=10 trusted=0 partial=10
    firstTrustedStep=na lastTrustedStep=na
    topReasons=low_raytests=10
    perPxOff(mean,p95)=na,na perPxOn(mean,p95)=na,na savedPctMean=na
    low_raytests examples:
      step=630 geomRayTestsTotalRaw=733 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=660 geomRayTestsTotalRaw=1000 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=690 geomRayTestsTotalRaw=576 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
  run=prune_on_loose_env frames=300 warmup=30 samples=270 windows=10 trusted=0 partial=10
    firstTrustedStep=na lastTrustedStep=na
    topReasons=low_raytests=10
    perPxOff(mean,p95)=na,na perPxOn(mean,p95)=na,na savedPctMean=na
    low_raytests examples:
      step=930 geomRayTestsTotalRaw=652 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=960 geomRayTestsTotalRaw=652 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1120 minGeomPix=1024
      step=990 geomRayTestsTotalRaw=832 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
  run=prune_on_stride_off frames=300 warmup=30 samples=270 windows=10 trusted=0 partial=10
    firstTrustedStep=na lastTrustedStep=na
    topReasons=low_raytests=10
    perPxOff(mean,p95)=na,na perPxOn(mean,p95)=na,na savedPctMean=na
    low_raytests examples:
      step=1230 geomRayTestsTotalRaw=733 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=1260 geomRayTestsTotalRaw=1000 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=1290 geomRayTestsTotalRaw=576 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024

REASON HISTOGRAM
  reasons:
    low_raytests: 40
    ok: 10
  trusted: trusted=1=10 trusted=0=40
  prune: prune=on=40 prune=off=10
  trustGateDebug metPix=1 metRay=0: 40

=== AutoCal Shadow Eval Summary (C1.1 g.3) ===
Coverage
  total_runs=5
  runs_with_shadow_eval=0
  coverage_pct=0.0%
Verdicts
  pass_count=0
  fail_count=0
  defer_count=0
  missing_count=0
Overhead (overhead_pct_est, numeric only)
  count=0
  mean=na
  median=na
  p90=na
  p95=na
  max=na
Top offenders (overhead_pct_est desc)
  none
Trust sanity
  baseline_trust_eq_1_and_shadow_trust_ne_1=0
  fail_count_inferred_due_to_shadow_trust=0
  shadow_trust_counts=none

AutoCal decision binding diagnostic
  missing_decision_count=0

CSV written: /home/bb/code/godot_xPRIMEray/tools/renderhealth_summary.csv
JSON written: /home/bb/code/godot_xPRIMEray/tools/renderhealth_summary.json`
- curved_minimal reorder-only: godot_exit=0 regress_exit=0 captures=0 log=`/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun/logs/curved_minimal__reorder-only.log`
  regress=`PASS: /home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun/logs/curved_minimal__reorder-only.log windows=50, trusted=10, partial=40
ON trusted mean per-px-on=na
OFF trusted mean per-px-off=178.077
ON saved% mean (baseline-ready)=na
Audit totals: falseNeg=0 falsePos=0

summary: pass=1 fail=0

DOE summary:
A) TrustReason histogram (global)
  low_raytests: 40
  ok: 10

A) TrustReason histogram (per run)
  baseline_prune_off: ok=10
  prune_on_default: low_raytests=10
  prune_on_tight_env: low_raytests=10
  prune_on_loose_env: low_raytests=10
  prune_on_stride_off: low_raytests=10

B) Trust flip timeline
  baseline_prune_off: firstTrustedStep=30 trusted_windows=10/10 (100.0%)
  prune_on_default: firstTrustedStep=none trusted_windows=0/10 (0.0%)
  prune_on_tight_env: firstTrustedStep=none trusted_windows=0/10 (0.0%)
  prune_on_loose_env: firstTrustedStep=none trusted_windows=0/10 (0.0%)
  prune_on_stride_off: firstTrustedStep=none trusted_windows=0/10 (0.0%)

C) low_raytests diagnostics: count=40
  run=prune_on_default step=330 geomRayTestsTotalRaw=608.0 minRayTests=4096.0 geomPixProcessedRaw=1160.0 minGeomPix=1024.0 candPix/noCand=43/1117 rayTestsPerGeomPix=0.524 candRate=0.037 noCandRate=0.963
  run=prune_on_default step=360 geomRayTestsTotalRaw=608.0 minRayTests=4096.0 geomPixProcessedRaw=1120.0 minGeomPix=1024.0 candPix/noCand=43/1077 rayTestsPerGeomPix=0.543 candRate=0.038 noCandRate=0.962
  run=prune_on_default step=390 geomRayTestsTotalRaw=767.0 minRayTests=4096.0 geomPixProcessedRaw=1160.0 minGeomPix=1024.0 candPix/noCand=54/1106 rayTestsPerGeomPix=0.661 candRate=0.047 noCandRate=0.953
  run=prune_on_default step=420 geomRayTestsTotalRaw=1071.0 minRayTests=4096.0 geomPixProcessedRaw=1160.0 minGeomPix=1024.0 candPix/noCand=75/1085 rayTestsPerGeomPix=0.923 candRate=0.065 noCandRate=0.935
  run=prune_on_default step=450 geomRayTestsTotalRaw=613.0 minRayTests=4096.0 geomPixProcessedRaw=1160.0 minGeomPix=1024.0 candPix/noCand=43/1117 rayTestsPerGeomPix=0.528 candRate=0.037 noCandRate=0.963

D) Top 10 worst trust failures by deficit
  run=prune_on_tight_env step=690 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_tight_env step=720 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_tight_env step=810 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_tight_env step=840 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_stride_off step=1290 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_stride_off step=1320 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_stride_off step=1410 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_stride_off step=1440 raytestsDeficit=3520.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_default step=330 raytestsDeficit=3488.000 geomPixDeficit=0.000 p2Deficit=na
  run=prune_on_default step=360 raytestsDeficit=3488.000 geomPixDeficit=0.000 p2Deficit=na

RUN REPORT
  run=baseline_prune_off frames=300 warmup=30 samples=270 windows=10 trusted=10 partial=0
    firstTrustedStep=30 lastTrustedStep=300
    topReasons=ok=10
    perPxOff(mean,p95)=178.077,212.757 perPxOn(mean,p95)=na,na savedPctMean=na
  run=prune_on_default frames=300 warmup=30 samples=270 windows=10 trusted=0 partial=10
    firstTrustedStep=na lastTrustedStep=na
    topReasons=low_raytests=10
    perPxOff(mean,p95)=na,na perPxOn(mean,p95)=na,na savedPctMean=na
    low_raytests examples:
      step=330 geomRayTestsTotalRaw=608 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=360 geomRayTestsTotalRaw=608 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1120 minGeomPix=1024
      step=390 geomRayTestsTotalRaw=767 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
  run=prune_on_tight_env frames=300 warmup=30 samples=270 windows=10 trusted=0 partial=10
    firstTrustedStep=na lastTrustedStep=na
    topReasons=low_raytests=10
    perPxOff(mean,p95)=na,na perPxOn(mean,p95)=na,na savedPctMean=na
    low_raytests examples:
      step=630 geomRayTestsTotalRaw=733 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=660 geomRayTestsTotalRaw=1000 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=690 geomRayTestsTotalRaw=576 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
  run=prune_on_loose_env frames=300 warmup=30 samples=270 windows=10 trusted=0 partial=10
    firstTrustedStep=na lastTrustedStep=na
    topReasons=low_raytests=10
    perPxOff(mean,p95)=na,na perPxOn(mean,p95)=na,na savedPctMean=na
    low_raytests examples:
      step=930 geomRayTestsTotalRaw=652 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=960 geomRayTestsTotalRaw=652 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1120 minGeomPix=1024
      step=990 geomRayTestsTotalRaw=832 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
  run=prune_on_stride_off frames=300 warmup=30 samples=270 windows=10 trusted=0 partial=10
    firstTrustedStep=na lastTrustedStep=na
    topReasons=low_raytests=10
    perPxOff(mean,p95)=na,na perPxOn(mean,p95)=na,na savedPctMean=na
    low_raytests examples:
      step=1230 geomRayTestsTotalRaw=733 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=1260 geomRayTestsTotalRaw=1000 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024
      step=1290 geomRayTestsTotalRaw=576 minRayTests=4096 trustRayTestsMet=0 geomPixProcessedRaw=1160 minGeomPix=1024

REASON HISTOGRAM
  reasons:
    low_raytests: 40
    ok: 10
  trusted: trusted=1=10 trusted=0=40
  prune: prune=on=40 prune=off=10
  trustGateDebug metPix=1 metRay=0: 40

=== AutoCal Shadow Eval Summary (C1.1 g.3) ===
Coverage
  total_runs=5
  runs_with_shadow_eval=0
  coverage_pct=0.0%
Verdicts
  pass_count=0
  fail_count=0
  defer_count=0
  missing_count=0
Overhead (overhead_pct_est, numeric only)
  count=0
  mean=na
  median=na
  p90=na
  p95=na
  max=na
Top offenders (overhead_pct_est desc)
  none
Trust sanity
  baseline_trust_eq_1_and_shadow_trust_ne_1=0
  fail_count_inferred_due_to_shadow_trust=0
  shadow_trust_counts=none

AutoCal decision binding diagnostic
  missing_decision_count=0

CSV written: /home/bb/code/godot_xPRIMEray/tools/renderhealth_summary.csv
JSON written: /home/bb/code/godot_xPRIMEray/tools/renderhealth_summary.json`
- curved_minimal_backdrop baseline: godot_exit=2 regress_exit=0 captures=0 log=`/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun/logs/curved_minimal_backdrop__baseline.log`
  regress=`PASS: /home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun/logs/curved_minimal_backdrop__baseline.log windows=1, trusted=1, partial=0
ON trusted mean per-px-on=35.969
OFF trusted mean per-px-off=na
ON saved% mean (baseline-ready)=na
Audit totals: falseNeg=0 falsePos=0

summary: pass=1 fail=0

DOE summary:
A) TrustReason histogram (global)
  ok: 1

A) TrustReason histogram (per run)
  __unscoped__:curved_minimal_backdrop__baseline.log: ok=1

B) Trust flip timeline
  __unscoped__:curved_minimal_backdrop__baseline.log: firstTrustedStep=30 trusted_windows=1/1 (100.0%)

C) low_raytests diagnostics: count=0

D) Top 10 worst trust failures by deficit
  none

RUN REPORT
  run=__unscoped__:curved_minimal_backdrop__baseline.log frames=na warmup=na samples=na windows=1 trusted=1 partial=0
    firstTrustedStep=30 lastTrustedStep=30
    topReasons=ok=1
    perPxOff(mean,p95)=na,na perPxOn(mean,p95)=35.969,35.969 savedPctMean=na

REASON HISTOGRAM
  reasons:
    ok: 1
  trusted: trusted=1=1 trusted=0=0
  prune: prune=on=1 prune=off=0
  trustGateDebug metPix=1 metRay=0: 0

=== AutoCal Shadow Eval Summary (C1.1 g.3) ===
Coverage
  total_runs=1
  runs_with_shadow_eval=0
  coverage_pct=0.0%
Verdicts
  pass_count=0
  fail_count=0
  defer_count=0
  missing_count=0
Overhead (overhead_pct_est, numeric only)
  count=0
  mean=na
  median=na
  p90=na
  p95=na
  max=na
Top offenders (overhead_pct_est desc)
  none
Trust sanity
  baseline_trust_eq_1_and_shadow_trust_ne_1=0
  fail_count_inferred_due_to_shadow_trust=0
  shadow_trust_counts=none

AutoCal decision binding diagnostic
  missing_decision_count=0

CSV written: /home/bb/code/godot_xPRIMEray/tools/renderhealth_summary.csv
JSON written: /home/bb/code/godot_xPRIMEray/tools/renderhealth_summary.json`
- curved_minimal_backdrop reorder-only: godot_exit=5 regress_exit=0 captures=0 log=`/home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun/logs/curved_minimal_backdrop__reorder-only.log`
  regress=`PASS: /home/bb/code/godot_xPRIMEray/output/render_test_scheduler_compare/2026-03-28T_scheduler_compare_rerun/logs/curved_minimal_backdrop__reorder-only.log windows=1, trusted=1, partial=0
ON trusted mean per-px-on=34.699
OFF trusted mean per-px-off=na
ON saved% mean (baseline-ready)=na
Audit totals: falseNeg=0 falsePos=0

summary: pass=1 fail=0

DOE summary:
A) TrustReason histogram (global)
  ok: 1

A) TrustReason histogram (per run)
  __unscoped__:curved_minimal_backdrop__reorder-only.log: ok=1

B) Trust flip timeline
  __unscoped__:curved_minimal_backdrop__reorder-only.log: firstTrustedStep=30 trusted_windows=1/1 (100.0%)

C) low_raytests diagnostics: count=0

D) Top 10 worst trust failures by deficit
  none

RUN REPORT
  run=__unscoped__:curved_minimal_backdrop__reorder-only.log frames=na warmup=na samples=na windows=1 trusted=1 partial=0
    firstTrustedStep=30 lastTrustedStep=30
    topReasons=ok=1
    perPxOff(mean,p95)=na,na perPxOn(mean,p95)=34.699,34.699 savedPctMean=na

REASON HISTOGRAM
  reasons:
    ok: 1
  trusted: trusted=1=1 trusted=0=0
  prune: prune=on=1 prune=off=0
  trustGateDebug metPix=1 metRay=0: 0

=== AutoCal Shadow Eval Summary (C1.1 g.3) ===
Coverage
  total_runs=1
  runs_with_shadow_eval=0
  coverage_pct=0.0%
Verdicts
  pass_count=0
  fail_count=0
  defer_count=0
  missing_count=0
Overhead (overhead_pct_est, numeric only)
  count=0
  mean=na
  median=na
  p90=na
  p95=na
  max=na
Top offenders (overhead_pct_est desc)
  none
Trust sanity
  baseline_trust_eq_1_and_shadow_trust_ne_1=0
  fail_count_inferred_due_to_shadow_trust=0
  shadow_trust_counts=none

AutoCal decision binding diagnostic
  missing_decision_count=0

CSV written: /home/bb/code/godot_xPRIMEray/tools/renderhealth_summary.csv
JSON written: /home/bb/code/godot_xPRIMEray/tools/renderhealth_summary.json`

## Comparisons

- curved_minimal: missing capture
- curved_minimal_backdrop: missing capture
