---
title: arXiv Preprints
description: xPRIMEray arXiv-facing manuscript submissions
---

# arXiv Preprints

This directory contains LaTeX source for manuscripts targeting arXiv submission.

---

## Perceptual Curvature Threshold Hypothesis

**File:** [`Perceptual Curvature Threshold Hypothesis.tex`](Perceptual Curvature Threshold Hypothesis.tex)

**Status:** Complete — publication ready

### Abstract

This paper presents the perceptual curvature threshold hypothesis for derivative-aware step scaling in curved ray transport systems. The central claim is that below a scene-specific curvature threshold κₚ, adaptive step scaling adjustments become imperceptible to the observer, making deadband filtering both safe and efficient.

The paper covers:

- **Derivative-Aware Step Scaling** — adaptive step sizing based on local curvature gradient magnitude
- **Deadband Filtering** — suppressing micro-adjustments below the perceptual threshold
- **Experimental Validation** — fixture-based comparison across `curved_minimal` and `curved_minimal_backdrop` baselines
- **Nyquist-Like Interpretation** — curvature sampling analogy to sampling theory
- **Connection to Geodesic Deviation** — GR-grounded interpretation of the threshold condition

### Key Result

The hypothesis is formalized as:

$$\kappa < \kappa_p \implies \text{step adjustment is perceptually negligible}$$

where κₚ is empirically determined per scene configuration through the fixture harness.

### Experimental Basis

Validated against outputs from `exp1_derivative_step_v0` through `v4`:

- step sizes swept from 0.025 to 0.055
- adjustment reduction rates and output fidelity compared across variants
- runtime measured against baseline

### Compile

```bash
cd Docs/Arxiv
pdflatex "Perceptual Curvature Threshold Hypothesis.tex"
```

---

*LaTeX source uses standard packages: `amsmath`, `amssymb`, `booktabs`, `geometry`. No external figures required.*
