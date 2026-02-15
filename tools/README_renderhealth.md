# RenderHealth Parse + Regression

## Trusted semantics

`geomTrusted=1` means the RenderHealth window is stable enough to compare pruning behavior.

A window is trusted when:
- `geomPruneSwitched=0` (no prune mode transition in the window)
- enough same-mode samples exist (`geomHealthModeSamples` meets threshold)
- for prune-on windows, enough pass-2 samples exist (`p2Samp` threshold)

When trusted is false, `geomHealthPartial=1` and trust-sensitive metrics are emitted as `na`.

Trust detection in tools:
- Prefer explicit `geomTrusted` when present.
- If `geomTrusted` is missing, infer trust from `geomHealthPartial` (`1` means untrusted).

## Why partial windows happen

Common partial reasons (`geomTrustReason`):
- `mode_switch`: prune toggled during the current window
- `low_mode_samples`: not enough same-mode samples yet after a change/start
- `low_p2samp`: prune-on window has too few pass-2 samples

In those windows, per-pixel ray comparison fields and prune-on-only fields are intentionally gated.

## `na` vs `0`

- `na` means metric is intentionally unavailable/gated for that mode/window and must not be interpreted numerically.
- `0` means metric is valid and measured, and its numeric value happened to be zero.
- Example: `geomPrune=off` trusted windows must show candidate/prune-on-only fields as `na`, not `0`.

## Scripts

`tools/renderhealth_parse.py`
- Parses a single log and prints mode summaries.
- Optional CSV export of one row per `[RenderHealth]` line.
- Importable module API: `parse_renderhealth_line()`, `load_entries()`, `summarize_entries()`.
- Loader is robust to single-line/no-newline log artifacts by scanning `[RenderHealth]` segments from stream content.

`tools/renderhealth_regress.py`
- Validates B.3 invariants across one or more logs.
- Exits non-zero on first invariant failure with line index and key window fields in the failure message.
- Also enforces prune audit mismatch failures unless `--allow-audit-mismatch` is supplied.

## Usage

From repo root:

```powershell
python tools/renderhealth_parse.py "logs/2026-02-13 A TLAS Pruning OnOffOnOffOn"
python tools/renderhealth_parse.py "logs/2026-02-13 B B3 PruningTests" --csv logs/b3_parse.csv
python tools/renderhealth_regress.py "logs/2026-02-13 A TLAS Pruning OnOffOnOffOn" "logs/2026-02-13 B B3 PruningTests" "logs/2026-02-14 A b.3 validations"
```

## Recommended test procedure

1. Start with prune `off` and warm up a few windows.
2. Hold prune `off` until trusted OFF windows appear (`geomTrusted=1`) so OFF baseline is learned.
3. Toggle prune `on`.
4. Wait for switch/partial windows to clear, then collect trusted ON windows.
5. Optional stress: toggle ON/OFF repeatedly and verify every switch window is partial/untrusted with gated `na` fields.
6. Save log output with `[RenderHealth]` lines.
7. Run `renderhealth_parse.py` for summary sanity.
8. Run `renderhealth_regress.py` to enforce invariants before merging changes.
