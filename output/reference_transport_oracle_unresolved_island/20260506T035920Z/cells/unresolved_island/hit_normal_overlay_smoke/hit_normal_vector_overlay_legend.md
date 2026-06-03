# Hit Normal Vector Overlay Legend

Post-process inspection overlay only. This does not modify renderer behavior and does not replace RGB normal shading.

- Green arrows: screen-projected `(normal_x, normal_y)` at sampled hit pixels.
- Gray X markers: sampled no-hit pixels, when enabled.
- Yellow dots: hit pixels whose projected x/y normal length is near zero.
- Yellow rectangle: ROI bbox, when provided.
- Magenta arrows: before/after projected normal delta when `--compare-csv` is provided.

## Summary

- image: output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/domain_resolver_stress__reference_transport_oracle_unresolved_island_unresolved_island__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png
- hit_csv: output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/domain_resolver_stress__reference_transport_oracle_unresolved_island_unresolved_island__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.hit_diagnostics.csv
- compare_csv: 
- sample_count: 920
- hit_count: 400
- no_hit_count: 520
- compare_sample_count: 0
- zero_projected_normal_count: 361
- stride: 8
- scale: 12.0
- mode: fixed
- roi_bbox: 
- max_compare_normal_angle_delta_deg: 0.0
- mean_compare_normal_angle_delta_deg: 
- post_process_only: True
