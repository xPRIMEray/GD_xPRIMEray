# Transport Ownership Graph Extraction Summary

> **Transport Ownership Graph** = nodes are connected components of equivalent renderer transport signatures; edges are observed adjacency/seam relations between those components.

Transport ownership graphs are diagnostic-only renderer validation data. They must not feed rendering, scheduling, hit selection, shading, resolver decisions, traversal, or adaptive precision.

## Concepts

- Topology stability: graph nodes/edges remain structurally consistent across refinement levels.
- Scalar precision stability: path length, hit distance, normal angle, and decision-risk deltas remain within epsilon.
- Seam persistence: a boundary/adjacency relation remains present across step ladder levels.
- Unresolved island closure: sampled unstable pixels become stable by a tested production step.

## Metrics

- graph_node_count: 3
- graph_edge_count: 2
- seam_length_px_total: 48
- high_discontinuity_edge_count: 2
- unstable_subgraph_count: 0
- unresolved_pixel_count: 0
- epsilon_stable_area_percent: 0.0
- precision_floor_histogram: {}
- node_persistence_rate: 
- edge_persistence_rate: 
- merge_count: 0
- split_count: 0
- graph_edit_distance_vs_reference: 
- sampled_topology_sealed_step: 
- full_frame_graph_basis: True
- oracle_sample_only: False
- persistence_basis: unavailable

## Confidence And Warnings

- Missing optional fields: portal_event_count

## Outputs

- `transport_ownership_graph_nodes.csv`
- `transport_ownership_graph_edges.csv`
- `transport_ownership_graph.json`
- `transport_ownership_graph_metrics.json`
- `transport_ownership_graph_persistence.csv`
- `transport_ownership_graph_merges_splits.csv`
- `unstable_subgraphs.csv`
