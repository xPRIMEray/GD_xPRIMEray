# MisterY Labs Artifacts

This folder is the **curated export layer** for xPRIMEray research outputs.

## Two-Layer Architecture

```
output/                        ← Active lab bench (runtime target)
  atomic_orbital_visual_observatory/
  wormhole_DR_Story/
  recursive_mirror_ghost_portal/
  ...README.md in every folder

misterylabs_artifacts/         ← Curated exhibit layer (this folder)
  manifest.json                ← Index of all promoted artifacts
  visuals/                     ← Website-ready images (<2 MB each)
  cards/                       ← Markdown card copy per artifact
  datasets/                    ← Small tabular summaries (CSV, JSON)
  validation/                  ← Validation reports and evidence
```

## Rules

### `output/` — Lab Bench
- The canonical target for all test harnesses, scripts, and Godot runs.
- **Never move or rename output folders** — scripts reference them by path.
- 4.2 GB+, ~19,000 files; only metadata (README.md, *.md, *.json, *.csv, *.txt) is tracked by Git.
- Raw renders (*.png), logs (*.log), and raw dumps are ignored by Git.
- Large raw files (oracle segment CSVs up to 720 MB, parent-path JSONL up to 96 MB) are explicitly blocked by `.gitignore`.
- Attach large raw files to GitHub Releases or store them in external object storage.

### `misterylabs_artifacts/` — Curated Exhibit
- Everything here is **intentionally selected** and safe to copy into the MisterY Labs website repo.
- Files here must be small enough for Git (target: <2 MB per file, <20 MB total folder).
- No raw render dumps, no oracle CSVs, no log files.
- Reference source outputs by path in `manifest.json` — don't duplicate unnecessarily.

## Promoting an Artifact

1. Find the source folder under `output/` (check its `README.md` for status).
2. If status is `Visual reference` or `Validation candidate`, it's ready to promote.
3. Run `scripts/export_to_misterylabs.sh <output_folder_name>` to copy the README and any small selected files.
4. Or copy manually — see the manual workflow below.
5. Update `manifest.json` to set `"in_misterylabs": true` for that artifact.

### Manual Promotion Workflow

```bash
# Copy a specific artifact's README + selected small files
SRC=output/wormhole_DR_Story
DEST=misterylabs_artifacts/cards

cp $SRC/README.md $DEST/wormhole_DR_Story.md

# If a curated PNG exists under 2MB, copy to visuals/
cp $SRC/latest/wormhole_dual_reality_storytelling_contact_sheet.png \
   misterylabs_artifacts/visuals/

# Update manifest.json to mark it promoted
```

## What Lives Where

| Type | Source | Destination |
|------|--------|-------------|
| Artifact README | `output/<folder>/README.md` | `cards/<folder>.md` |
| Website-ready PNG (<2MB) | `output/<folder>/<timestamped>/<image>.png` | `visuals/<slug>.png` |
| Small summary CSV | `output/<folder>/<timestamped>/<name>.csv` | `datasets/<slug>.csv` |
| Validation report | `output/<folder>/<timestamped>/<name>.md` | `validation/<slug>.md` |
| Giant oracle CSVs | `output/reference_transport_oracle_*/` | GitHub Release attachment |
| Raw renders (all .png) | `output/**/*.png` | Not in Git at all |

## Attaching Large Files to Releases

For files too large for Git (oracle segment dumps, full render sets):

```bash
# Tag and upload via GitHub CLI
gh release create v0.1-data --title "xPRIMEray Dataset v0.1"
gh release upload v0.1-data \
  output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/*_oracle_segments.csv
```
