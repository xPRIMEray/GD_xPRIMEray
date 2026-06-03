# Transport Ownership Graph Validation Report

Post-process validation only. This report does not modify renderer behavior.

- Overall status: **pass**
- Folder: `output/transport_ownership_graph_precision_sweep/20260504T043955Z`

## Summary Metrics

- graph_node_count: 1
- graph_edge_count: 0
- node_area_total: 289
- hit_diagnostic_labeled_pixels: 289
- unresolved_pixel_count: 0
- persistence_row_count: 26
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
| `node_area_matches_labeled_pixels` | pass | Node area total matches labeled hit-diagnostic pixels. |
| `unstable_subgraphs_match_unresolved_metric` | pass | unstable_subgraphs.csv agrees with unresolved_pixel_count. |
| `persistence_basis_allows_rows` | pass | Persistence rows exist with a non-unavailable basis. |
| `sampled_topology_sealed_step_scope` | pass | No sampled sealed step reported. |
| `lineage_png_for_persistence` | pass | graph_persistence_lineage.png exists for multi-step persistence. |
| `confidence_flags_mutually_consistent` | pass | Confidence flags are mutually consistent. |
