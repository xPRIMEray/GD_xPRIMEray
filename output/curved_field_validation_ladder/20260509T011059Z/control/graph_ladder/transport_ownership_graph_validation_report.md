# Transport Ownership Graph Validation Report

Post-process validation only. This report does not modify renderer behavior.

- Overall status: **warn**
- Folder: `/home/bb/code/godot_xPRIMEray/output/curved_field_validation_ladder/20260509T011059Z/control/graph_ladder`

## Summary Metrics

- graph_node_count: 1
- graph_edge_count: 0
- node_area_total: 57600
- hit_diagnostic_labeled_pixels: None
- unresolved_pixel_count: 0
- persistence_row_count: 8
- full_frame_graph_basis: True
- oracle_sample_only: False
- persistence_basis: overlap
- sampled_topology_sealed_step: 
- lineage_png_exists: True

## Checks

| Check | Status | Message |
|---|---|---|
| `metrics_exists` | pass | Metrics JSON exists. |
| `nodes_csv_exists` | pass | Nodes CSV exists. |
| `edges_csv_exists` | pass | Edges CSV exists. |
| `node_ids_unique` | pass | Node IDs are unique. |
| `node_count_matches_metrics` | pass | Node row count matches graph_node_count. |
| `edge_count_matches_metrics` | pass | Edge row count matches graph_edge_count. |
| `edges_reference_valid_nodes` | pass | Every edge references valid node IDs. |
| `edges_not_self_edges` | pass | No self-edges found. |
| `node_areas_positive` | pass | All node areas are positive or no nodes are present. |
| `node_area_matches_labeled_pixels` | warn | Full-frame graph basis set, but hit diagnostics were not available to verify pixel area. |
| `unstable_subgraphs_match_unresolved_metric` | pass | unstable_subgraphs.csv agrees with unresolved_pixel_count. |
| `persistence_basis_allows_rows` | pass | Persistence rows exist with a non-unavailable basis. |
| `sampled_topology_sealed_step_scope` | pass | No sampled sealed step reported. |
| `lineage_png_for_persistence` | pass | graph_persistence_lineage.png exists for multi-step persistence. |
| `confidence_flags_mutually_consistent` | pass | Confidence flags are mutually consistent. |
