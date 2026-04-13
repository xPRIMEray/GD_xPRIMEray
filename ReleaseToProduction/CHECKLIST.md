# Release Validation Checklist

## Scene Lock
- [ ] wormhole scene selected
- [ ] camera preset locked
- [ ] overlays default states defined

## Path Study
- [ ] camera path defined
- [ ] station count fixed
- [ ] station naming deterministic

## Capture Matrix
For each station:
- [ ] clean curved
- [ ] reference reality
- [ ] curvature heatmap
- [ ] semantic OR collision
- [ ] full stack

## Output
- [ ] stored under deterministic path
- [ ] summary.txt generated
- [ ] summary.json generated
- [ ] logs captured

## Validation
- [ ] proto-caustic invariant PASS
- [ ] low-value budget PASS

## Analysis
- [ ] SSIM/MAD computed
- [ ] masked comparison (if available)

## Documentation
- [ ] screenshots embedded into docs
- [ ] research notes updated

## Release Readiness
- [ ] stable demo path exists
- [ ] controls documented
- [ ] known limitations listed