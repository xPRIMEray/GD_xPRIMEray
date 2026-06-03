# Hit Normal Vector Overlay Legend

Post-process inspection overlay only. This does not modify renderer behavior and does not replace RGB normal shading.

- Cyan dots: sampled hit pixels.
- Green arrows: drawn screen-projected normal vectors.
- Gray X markers: sampled no-hit pixels, when enabled.
- Yellow dots: hit pixels whose selected projected normal magnitude is below `min_projected_magnitude`.
- Yellow rectangle: ROI bbox, when provided.
- Magenta arrows: before/after projected normal delta when `--compare-csv` is provided.

## Summary

- image: output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/domain_resolver_stress__reference_transport_oracle_unresolved_island_unresolved_island__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png
- hit_csv: output/reference_transport_oracle_unresolved_island/20260506T035920Z/cells/unresolved_island/domain_resolver_stress__reference_transport_oracle_unresolved_island_unresolved_island__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.hit_diagnostics.csv
- compare_csv: 
- sample_count: 920
- hit_count: 400
- no_hit_count: 520
- sampled_hit_pixels_marked: 400
- compare_sample_count: 0
- normal_field_names_found: ['normal_x', 'normal_y', 'normal_z', 'first_accepted_normal_x', 'first_accepted_normal_y', 'first_accepted_normal_z']
- rows_with_valid_normal_xyz: 57600
- hit_rows_with_valid_normal_xyz: 18539
- sampled_hit_rows_with_valid_normal_xyz: 400
- normal_x_stats: {'min': -0.815721, 'max': 0.815721, 'mean': -0.004925}
- normal_y_stats: {'min': -0.775078, 'max': 0.769705, 'mean': -2.2e-05}
- normal_z_stats: {'min': 0.0, 'max': 1.0, 'mean': 0.321351}
- hit_normal_x_stats: {'min': -0.815721, 'max': 0.815721, 'mean': -0.015303}
- hit_normal_y_stats: {'min': -0.775078, 'max': 0.769705, 'mean': -6.9e-05}
- hit_normal_z_stats: {'min': 0.184763, 'max': 1.0, 'mean': 0.998427}
- projected_xy_magnitude_histogram: {'[0,1e-6)': 55533, '[1e-6,1e-4)': 0, '[1e-4,1e-3)': 0, '[1e-3,1e-2)': 0, '[1e-2,1e-1)': 2, '[1e-1,0.5)': 2044, '[0.5,1.0]': 21, '>1.0': 0}
- hit_projected_xy_magnitude_histogram: {'[0,1e-6)': 16472, '[1e-6,1e-4)': 0, '[1e-4,1e-3)': 0, '[1e-3,1e-2)': 0, '[1e-2,1e-1)': 2, '[1e-1,0.5)': 2044, '[0.5,1.0]': 21, '>1.0': 0}
- sampled_projected_xy_magnitude_histogram: {'[0,1e-6)': 361, '[1e-6,1e-4)': 0, '[1e-4,1e-3)': 0, '[1e-3,1e-2)': 0, '[1e-2,1e-1)': 0, '[1e-1,0.5)': 39, '[0.5,1.0]': 0, '>1.0': 0}
- projection_degenerate_normal_count: 361
- camera_facing_or_projection_degenerate_count: 361
- vectors_drawn: 39
- stride: 8
- scale: 12.0
- mode: fixed
- projection: xy
- flip_y: True
- min_projected_magnitude: 1e-06
- roi_bbox: 
- max_compare_normal_angle_delta_deg: 0.0
- mean_compare_normal_angle_delta_deg: 
- post_process_only: True
