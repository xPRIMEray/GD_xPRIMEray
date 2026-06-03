# Hit Normal Vector Overlay Legend

Post-process inspection overlay only. This does not modify renderer behavior and does not replace RGB normal shading.

- Cyan dots: sampled hit pixels.
- Green arrows: drawn screen-projected normal vectors.
- Gray X markers: sampled no-hit pixels, when enabled.
- Yellow dots: hit pixels whose selected projected normal magnitude is below `min_projected_magnitude`.
- Yellow rectangle: ROI bbox, when provided.
- Magenta arrows: before/after projected normal delta when `--compare-csv` is provided.

## Summary

- image: /home/bb/code/godot_xPRIMEray/output/curved_field_validation_ladder/20260509T033059Z/control/steps/step_0.016/domain_resolver_stress__control_step_0p016__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.png
- hit_csv: /home/bb/code/godot_xPRIMEray/output/curved_field_validation_ladder/20260509T033059Z/control/steps/step_0.016/domain_resolver_stress__control_step_0p016__baseline_prune_off__scheduler-baseline__targetms-1000__stride-1__runid-1.hit_diagnostics.csv
- compare_csv: 
- sample_count: 920
- hit_count: 400
- no_hit_count: 520
- sampled_hit_pixels_marked: 400
- compare_sample_count: 0
- normal_field_names_found: ['normal_x', 'normal_y', 'normal_z', 'first_accepted_normal_x', 'first_accepted_normal_y', 'first_accepted_normal_z']
- rows_with_valid_normal_xyz: 57600
- hit_rows_with_valid_normal_xyz: 18613
- sampled_hit_rows_with_valid_normal_xyz: 400
- normal_x_stats: {'min': -0.815914, 'max': 0.815914, 'mean': -0.004925}
- normal_y_stats: {'min': -0.775017, 'max': 0.769698, 'mean': -2.2e-05}
- normal_z_stats: {'min': 0.0, 'max': 1.0, 'mean': 0.322636}
- hit_normal_x_stats: {'min': -0.815914, 'max': 0.815914, 'mean': -0.015242}
- hit_normal_y_stats: {'min': -0.775017, 'max': 0.769698, 'mean': -6.8e-05}
- hit_normal_z_stats: {'min': 0.184707, 'max': 1.0, 'mean': 0.998434}
- projected_xy_magnitude_histogram: {'[0,1e-6)': 38987, '[1e-6,1e-4)': 0, '[1e-4,1e-3)': 0, '[1e-3,1e-2)': 0, '[1e-2,1e-1)': 0, '[1e-1,0.5)': 0, '[0.5,1.0]': 18613, '>1.0': 0}
- hit_projected_xy_magnitude_histogram: {'[0,1e-6)': 0, '[1e-6,1e-4)': 0, '[1e-4,1e-3)': 0, '[1e-3,1e-2)': 0, '[1e-2,1e-1)': 0, '[1e-1,0.5)': 0, '[0.5,1.0]': 18613, '>1.0': 0}
- sampled_projected_xy_magnitude_histogram: {'[0,1e-6)': 0, '[1e-6,1e-4)': 0, '[1e-4,1e-3)': 0, '[1e-3,1e-2)': 0, '[1e-2,1e-1)': 0, '[1e-1,0.5)': 0, '[0.5,1.0]': 400, '>1.0': 0}
- projection_degenerate_normal_count: 0
- camera_facing_or_projection_degenerate_count: 0
- vectors_drawn: 400
- stride: 8
- scale: 12.0
- mode: fixed
- projection: xz
- flip_y: True
- min_projected_magnitude: 1e-06
- roi_bbox: 
- max_compare_normal_angle_delta_deg: 0.0
- mean_compare_normal_angle_delta_deg: 
- post_process_only: True
