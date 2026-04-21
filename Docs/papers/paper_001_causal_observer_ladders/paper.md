# Causal Observer Ladders for Wormhole Ray Transport  
### Fresh-Instance Validation, Regime Structure, and Bridge Anomalies

---

## Abstract

We present a fixture-based method for validating observer-dependent ray transport through wormhole-like topological structures. Using a fresh-instance observer ladder with per-checkpoint causal verification, we identify distinct transport regimes spanning near-side, throat, bridge, and far-side observer states. 

We demonstrate that world-space interpolation fails across the topological transition, while a mixed strategy of interpolation and discovered checkpoints yields a coherent traversal path. Derived metrics including optical path length (OPL), interaction density, and transport cost reveal a previously uncharacterized bridge regime in which interaction density collapses while per-crossing cost peaks. These results suggest that observer position defines transport regime and that naive coordinate interpolation is insufficient across non-trivial topology.

---

## 1. Introduction

Simulating ray transport through curved or topologically non-trivial spaces presents challenges beyond standard geometric rendering. In particular, observer-dependent effects may induce discontinuities that are not captured by naive interpolation in world-space coordinates.

This work introduces a **causal observer ladder** methodology: a sequence of validated observer states, each computed under fresh-instance conditions, ensuring that transport metrics reflect true causal structure rather than accumulated simulation artifacts.

We apply this method to a wormhole-like system and show that traversal naturally separates into multiple regimes with distinct transport characteristics.

---

## 2. Method

### 2.1 Fresh-Instance Observer Ladder

Each checkpoint is evaluated using a fresh instance of the renderer, ensuring:
- full classified pixel coverage
- zero budget exhaustion
- zero inferred classification artifacts

This guarantees that differences between checkpoints arise from transport structure rather than simulation state.

### 2.2 Ladder Construction

The observer ladder consists of six validated checkpoints:

- `mouth`
- `mouth_to_throat_approach`
- `throat`
- `post_throat_backstep_01`
- `post_throat_exit_approach`
- `exit_lookback`

Near-side checkpoints were densified via interpolation, while hard-leg checkpoints were discovered via guided search.

### 2.3 Derived Metrics

For each checkpoint, we compute:

- Optical Path Length (OPL mean, max)
- Portal-hit density
- Throat-event density
- Crossings per pixel
- Segments per crossing
- Average segments per ray

---

## 3. Results

All checkpoints satisfy full classified coverage, zero budget exhaustion, and zero inferred throat classification, ensuring that reported differences arise from transport structure rather than sampling artifacts.

### 3.1 Near-Side Regime

From `mouth` to `throat`, interaction density increases while transport cost decreases:

- portal-hit density: 0.1465 → 0.1750  
- throat-event density: 0.0969 → 0.1139  
- crossings per pixel: 0.6495 → 0.7479  
- OPL mean: 9.9599 → 9.5078  
- segments per crossing: 153.26 → 128.17  

This indicates a smooth, interpolation-friendly regime.

### 3.2 Throat as Transition Hinge

The throat behaves as a **transition hinge rather than a discontinuity**, extending the near-side trend instead of breaking it.

### 3.3 Bridge Regime

The checkpoint `post_throat_backstep_01` exhibits a distinct transport state:

- portal-hit density: 0.0964  
- throat-event density: 0.0555  
- crossings per pixel: 0.2098  
- segments per crossing: 366.03 (maximum)  
- OPL mean: 7.5908 (minimum)  

This indicates a **sparse, high-cost transport regime** where interactions are rare but expensive.

### 3.4 Far-Side Re-Densification

The far-side regime re-densifies:

- throat-event density peaks at 0.2111  
- crossings per pixel peaks at 1.6544  
- segments per crossing drops to 50.31 (minimum)  

At `exit_lookback`, portal density reaches its maximum (0.2557), and OPL max reaches 16.3070.

---

## 4. Key Findings

- Interpolation validity is regime-dependent  
- The throat is not the primary discontinuity  
- The bridge is the dominant transport anomaly  
- OPL and interaction density decouple sharply at the bridge  

---

## 5. Discussion

These results suggest that observer traversal through wormhole-like topology cannot be treated as a continuous world-space path. Instead, traversal must be constructed as a sequence of causally valid states.

The existence of a bridge regime indicates that transport efficiency and interaction density may decouple in transitional regions, revealing structure not visible through naive sampling.

---

## 6. Conclusion

We introduce a validated observer ladder framework for wormhole ray transport and demonstrate that traversal naturally decomposes into distinct regimes. This provides a foundation for future work in curved-ray rendering, topological optics, and causal transport analysis.

---

## Figures (Proposed)

- Ladder diagram (observer positions)
- Interaction density vs checkpoint
- Segments-per-crossing spike at bridge
- OPL vs interaction density phase plot

---

## Data Source

All results derived from:
output/fixture_runs/fixture_011_wormhole_checkpoint_sequence/2026-04-20T22-26-39/


---