# Overspace First Milestone Path Ladder

This run captures one starting frame plus five evenly spaced auto-path samples so we can tune camera speed, offset, and orb approach composition.

## Cases

### 01_path_start — Path start

- Progress target: 0.0%
- What it shows: Initial camera pose before the auto-path begins moving toward the gallery orb.
- What it communicates: Baseline framing for path-tuning. This is the exact starting pose that the auto-path logic inherits.
- Image: `images/01_path_start.png`
- Log: `logs/01_path_start.log`

### 02_path_20 — 20% path progress

- Progress target: 20.0%
- What it shows: Auto-path sample at the first evenly spaced progress stop toward the gallery orb.
- What it communicates: Early approach framing. This helps tune acceleration, centering, and early overlay legibility.
- Image: `images/02_path_20.png`
- Log: `logs/02_path_20.log`

### 03_path_40 — 40% path progress

- Progress target: 40.0%
- What it shows: Auto-path sample at forty percent of the planned approach distance.
- What it communicates: Mid-path stability check. This reveals whether the orb alignment converges cleanly before the close approach.
- Image: `images/03_path_40.png`
- Log: `logs/03_path_40.log`

### 04_path_60 — 60% path progress

- Progress target: 60.0%
- What it shows: Auto-path sample at sixty percent of the planned approach distance.
- What it communicates: Late-mid path check. This is where motion and portal framing should begin to feel intentional rather than exploratory.
- Image: `images/04_path_60.png`
- Log: `logs/04_path_60.log`

### 05_path_80 — 80% path progress

- Progress target: 80.0%
- What it shows: Auto-path sample near the close approach to the gallery orb.
- What it communicates: Pre-entry composition check. This shows whether the path gets us near the orb without losing diagnostic clarity.
- Image: `images/05_path_80.png`
- Log: `logs/05_path_80.log`

### 06_path_100 — 100% path progress

- Progress target: 100.0%
- What it shows: Final pre-entry approach sample at the end of the planned auto-path.
- What it communicates: Terminal path framing. This is the best pre-traversal frame for deciding whether speed, offset, and aim need adjustment.
- Image: `images/06_path_100.png`
- Log: `logs/06_path_100.log`
