# Hit Normal Vector Overlay Legend

Post-process inspection overlay only. This does not modify renderer behavior and does not replace RGB normal shading.

- Cyan dots: sampled hit pixels.
- Green arrows: drawn screen-projected normal vectors.
- Gray X markers: sampled no-hit pixels, when enabled.
- Yellow dots: hit pixels whose selected projected normal magnitude is below `min_projected_magnitude`.
- Yellow rectangle: ROI bbox, when provided.
- Magenta arrows: before/after projected normal delta when `--compare-csv` is provided.

## Summary

- image: /home/bb/code/godot_xPRIMEray/output/curvature_fps_benchmark/20260606T014236Z/cells/curvature_025/row/hermetic_curved_room__curvature_fps_25__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png
- hit_csv: /home/bb/code/godot_xPRIMEray/output/curvature_fps_benchmark/20260606T014236Z/cells/curvature_025/row/hermetic_curved_room__curvature_fps_25__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.hit_diagnostics.csv
- compare_csv: 
- sample_count: 15
- hit_count: 15
- no_hit_count: 0
- sampled_hit_pixels_marked: 15
- compare_sample_count: 0
- normal_field_names_found: ['normal_x', 'normal_y', 'normal_z', 'first_accepted_normal_x', 'first_accepted_normal_y', 'first_accepted_normal_z']
- rows_with_valid_normal_xyz: 880
- hit_rows_with_valid_normal_xyz: 880
- sampled_hit_rows_with_valid_normal_xyz: 15
- normal_x_stats: {'min': -1.0, 'max': 1.0, 'mean': 0.0}
- normal_y_stats: {'min': 0.0, 'max': 0.0, 'mean': 0.0}
- normal_z_stats: {'min': 0.0, 'max': 1.0, 'mean': 0.629545}
- hit_normal_x_stats: {'min': -1.0, 'max': 1.0, 'mean': 0.0}
- hit_normal_y_stats: {'min': 0.0, 'max': 0.0, 'mean': 0.0}
- hit_normal_z_stats: {'min': 0.0, 'max': 1.0, 'mean': 0.629545}
- projected_xy_magnitude_histogram: {'[0,1e-6)': 554, '[1e-6,1e-4)': 0, '[1e-4,1e-3)': 0, '[1e-3,1e-2)': 0, '[1e-2,1e-1)': 0, '[1e-1,0.5)': 0, '[0.5,1.0]': 326, '>1.0': 0}
- hit_projected_xy_magnitude_histogram: {'[0,1e-6)': 554, '[1e-6,1e-4)': 0, '[1e-4,1e-3)': 0, '[1e-3,1e-2)': 0, '[1e-2,1e-1)': 0, '[1e-1,0.5)': 0, '[0.5,1.0]': 326, '>1.0': 0}
- sampled_projected_xy_magnitude_histogram: {'[0,1e-6)': 11, '[1e-6,1e-4)': 0, '[1e-4,1e-3)': 0, '[1e-3,1e-2)': 0, '[1e-2,1e-1)': 0, '[1e-1,0.5)': 0, '[0.5,1.0]': 4, '>1.0': 0}
- projection_degenerate_normal_count: 11
- camera_facing_or_projection_degenerate_count: 11
- vectors_drawn: 4
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
