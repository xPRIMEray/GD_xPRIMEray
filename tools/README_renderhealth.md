# RenderHealth Parse + Regression

## Trusted semantics

`geomTrusted=1` means the RenderHealth window is stable enough to compare pruning behavior.

A window is trusted when:
- `geomPruneSwitched=0` (no prune mode transition in the window)
- enough same-mode samples exist (`geomHealthModeSamples` meets threshold)
- for prune-on windows, enough pass-2 samples exist (`p2Samp` threshold)

When trusted is false, `geomHealthPartial=1` and trust-sensitive metrics are emitted as `na`.

## Why partial windows happen

Common partial reasons (`geomTrustReason`):
- `mode_switch`: prune toggled during the current window
- `low_mode_samples`: not enough same-mode samples yet after a change/start
- `low_p2samp`: prune-on window has too few pass-2 samples

In those windows, per-pixel ray comparison fields are intentionally gated.

## Scripts

`tools/renderhealth_parse.py`
- Parses a single log and prints mode summaries.
- Optional CSV export of one row per `[RenderHealth]` line.

`tools/renderhealth_regress.py`
- Validates B.3 invariants across one or more logs.
- Exits non-zero on invariant failures.
- Also enforces prune audit mismatch failures unless `--allow-audit-mismatch` is supplied.

## Usage

From repo root:

```powershell
python tools/renderhealth_parse.py "logs/2026-02-13 A TLAS Pruning OnOffOnOffOn"
python tools/renderhealth_parse.py "logs/2026-02-13 B B3 PruningTests" --csv logs/b3_parse.csv
python tools/renderhealth_regress.py "logs/2026-02-13 A TLAS Pruning OnOffOnOffOn" "logs/2026-02-13 B B3 PruningTests"
```

## Recommended test procedure

1. Start render with prune `on`.
2. Toggle prune `off` mid-render.
3. Toggle prune `on` again.
4. Save log output with `[RenderHealth]` lines.
5. Run `renderhealth_parse.py` for summary sanity.
6. Run `renderhealth_regress.py` to enforce invariants before merging changes.
