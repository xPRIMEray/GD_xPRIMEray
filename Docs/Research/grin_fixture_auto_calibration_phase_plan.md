# 🔬 GRIN Fixture Auto-Calibration Framework
## xPRIMEray / GD_xPRIMEray – Engineering Study Architecture

---

# 🧠 Purpose

Establish a **repeatable, measurable, and automatable framework** for:

- Characterizing `GrinFilmCamera` behavior
- Defining valid parameter operating regions
- Enabling Codex-assisted execution
- Progressing toward **auto-calibration of GRIN ray paths**

This document defines the **A / B / C execution model** and integrates it into the broader xPRIMEray architecture.

---

# 🧱 Core Philosophy

We are not just rendering.

We are building a:

> **Controlled optical laboratory inside the renderer**

Where:
- Scenes = fixtures
- Rays = measurable signals
- Parameters = controllable inputs
- Logs = instrumentation
- Codex = execution + analysis agent

---

# 🔺 A / B / C Framework

## A — Safe Execution Harness (Operations Layer)

### Goal
Enable deterministic, bounded, reproducible test execution.

### Requirements
- Fixed entrypoint script(s)
- Controlled scenes only
- Non-destructive commands
- Predictable output directory
- Codex-friendly interface

### Example Entry Script
```
/scripts/run_fixture_baseline.sh
```

### Responsibilities
- Load fixture scene
- Apply parameter set
- Run renderer in bounded mode
- Emit logs to structured location

---

## B — Measurement & Logging Layer (Signal Layer)

### Goal
Convert ray behavior into **quantifiable engineering metrics**

### Required Metrics (Initial Set)
- Hit Success Rate
- Miss / Divergence Rate
- Bend Angle Distribution
- Radial Deviation
- Final Intercept Error
- Path Smoothness (oscillation detection)
- Symmetry Error (if applicable)

### Output Format
```
/output/fixture_runs/{timestamp}/
  ├── params.json
  ├── ray_log.txt
  ├── metrics.json
  └── summary.txt
```

### Key Principle

> If it cannot be measured, it cannot be calibrated.

---

## C — Codex Execution Workflow (Integration Layer)

### Goal
Enable Codex to act as a **controlled engineering operator**

### Standard Interaction Pattern

1. Show command
2. Confirm non-destructive
3. Execute
4. Summarize results

### Example Prompt Pattern
```
Run the GRIN fixture baseline.
Show command first, then execute, then summarize metrics.
```

### Responsibilities
- Execute harness scripts
- Parse output artifacts
- Compare runs
- Suggest parameter adjustments

---

# 🧪 Fixture-Based Characterization

## Definition
A **fixture** is a controlled scene designed to test specific GRIN behaviors.

### Fixture Components
- Known emitter position
- Known target surface or region
- Defined GRIN field
- Expected ray behavior

### Example Fixture Types
- Radial focusing lens
- Symmetric field test
- Wormhole throat proxy
- Flat-to-curved transition field

---

# 📊 Phase Plan

## Phase 1 — Baseline Harness (A)

### Deliverables
- One fixture scene
- One execution script
- Basic logging

### Success Criteria
- Codex can run fixture without errors
- Output artifacts generated consistently

---

## Phase 2 — Metric Instrumentation (B-lite)

### Deliverables
- Core metric extraction
- Structured `metrics.json`

### Success Criteria
- Runs produce comparable numeric outputs
- Basic pass/fail thresholds definable

---

## Phase 3 — Comparative Analysis (B-full)

### Deliverables
- Run-to-run comparison tooling
- Delta reporting

### Success Criteria
- Detect parameter sensitivity
- Identify stable vs unstable regions

---

## Phase 4 — Codex Workflow Integration (C)

### Deliverables
- Codex-compatible script interface
- Standardized prompt templates

### Success Criteria
- Codex can execute + summarize runs reliably

---

## Phase 5 — Parameter Sweep Engine

### Deliverables
- Controlled parameter sweeps
- Multi-run aggregation

### Success Criteria
- Parameter space begins forming stability maps

---

## Phase 6 — Calibration Engine

### Deliverables
- Optimization loop
- Objective function (error minimization)

### Success Criteria
- Identify best-fit parameter sets for fixture

---

## Phase 7 — Auto-Calibration

### Deliverables
- Fully automated loop:
  - run → evaluate → adjust → repeat

### Success Criteria
- System converges to valid GRIN configurations autonomously

---

# 🧠 Architectural Integration

This framework connects to:

- `RayBeamRenderer` → ray path generation
- `GrinFilmCamera` → parameter control surface
- `FieldSource3D` → GRIN field definition
- Future: `ResearchModeConfig`

---

# 🔮 Long-Term Vision

A system where:

- Any fixture can be loaded
- Codex executes a test suite
- Metrics are generated automatically
- Optimal parameters are proposed

Resulting in:

> A self-characterizing optical simulation engine

---

# ⚡ Immediate Next Step

Implement:

1. First fixture scene
2. Single execution script
3. Minimal metrics output

Then validate with Codex execution.

---

# 🧬 Tagline

> We are not tuning parameters manually.
>
> We are teaching the system how to understand itself.

---

🚀 End of Document

