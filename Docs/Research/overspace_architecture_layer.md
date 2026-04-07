# Overspace Architecture Layer

## Purpose

The current wormhole prototype links one scene location to another scene location. That is enough for a first portal mouth, but it is too flat for the longer-term model this project is already pointing toward:

- planetary rabbit-hole layers
- shelf-orb portals into different worlds
- nested solar, planet, and star density regimes
- future scale remap
- future clock or phase-density remap

This note proposes an "overspace" metadata layer that sits above the Godot scene graph and above the current linked-portal prototype. In this model, a portal target is not just a transform. It is an address:

`(WorldNode, DensityLayer, PortalAnchor)`

The scene graph still owns concrete nodes, meshes, physics, and cameras. Overspace adds the semantic graph that says which world a node belongs to, which density layer it inhabits, and what transform contract should apply when a traversal occurs.

## Design goals

- Keep the first implementation additive and metadata-first.
- Avoid a transport rewrite during the architecture pass.
- Support nested worlds and nested density layers independently.
- Let portal destinations resolve by world-layer identity instead of direct scene pairing alone.
- Carry future transform scalars without forcing them into current runtime behavior.

## Conceptual model

### 1. UniverseGraph

`UniverseGraph` is the top-level registry. It owns:

- world nodes
- density-layer definitions per world
- portal anchors bound to scene nodes
- portal links between anchors
- default overspace profile inheritance

It is the authoritative answer to:

- "what world is this scene node in?"
- "which density layer is active here?"
- "what does this portal actually connect to?"

### 2. WorldNode

`WorldNode` is a semantic world container, not necessarily a 1:1 Godot scene.

Examples:

- `SolarSystem`
- `Earth`
- `Sun`
- `ShelfOrbGallery`

A world node may:

- point at a scene resource
- exist only as an organizational parent
- inherit from another world node
- own several density layers

### 3. DensityLayer

`DensityLayer` is the second half of the address. It captures a regime inside a world, not just a render layer or physics layer.

Examples:

- `surface`
- `rabbit_hole_1`
- `corona`
- `core_echo`

Density layers are where future transform drift should live first, because that is where scale, clock rate, phase-density, and field intensity are most likely to diverge.

### 4. PortalAnchor

`PortalAnchor` binds overspace to an actual place in a Godot scene. It answers:

- which scene node hosts the mouth
- which world the mouth belongs to
- which density layer the mouth is embedded in
- which local frame should be treated as entry or exit basis

### 5. PortalLink

`PortalLink` connects one anchor to another anchor. The important shift is that the link is between overspace addresses first and concrete scene nodes second.

That lets the same portal architecture handle:

- Earth surface to Earth rabbit-hole interior
- shelf-orb display scene to Earth
- shelf-orb display scene to Sun
- future Alice-style scale transition
- future density or clock remap

### 6. OverspaceProfile

`OverspaceProfile` is a compact transform bundle. It is deliberately scalar-first for the scaffold pass.

Suggested scalars:

- `density_scalar`
- `phase_scalar`
- `clock_scalar`
- `scale_scalar`
- `field_scalar`

Profiles can live at several levels:

- universe default
- world default
- density-layer override
- portal-anchor override
- portal-link transit override

The effective profile is the multiplicative composition of the chain above.

## Proposed class set

### UniverseGraph

Suggested fields:

- `graph_id`
- `display_name`
- `root_world_id`
- `universe_profile`
- `worlds`
- `anchors`
- `links`

Suggested responsibilities:

- resolve a world by id
- resolve a layer within a world
- resolve an anchor by id
- resolve all links from an anchor
- compute an effective profile for a world-layer address

### WorldNode

Suggested fields:

- `world_id`
- `display_name`
- `parent_world_id`
- `child_world_ids`
- `scene_resource_path`
- `default_layer_id`
- `world_profile`
- `density_layers`
- `anchor_ids`
- `notes`

Suggested meaning:

- semantic world identity
- parent-child nesting across worlds
- per-world transform defaults
- portal ownership map for editor tooling and debug overlays

### DensityLayer

Suggested fields:

- `layer_id`
- `display_name`
- `parent_layer_id`
- `child_layer_ids`
- `layer_profile`
- `anchor_ids`
- `allow_phase_transform`
- `allow_clock_transform`
- `allow_scale_transform`
- `notes`

Suggested meaning:

- a nested density regime inside one world
- optional inheritance from another density layer in the same world
- the main place where future phase and clock behavior can attach cleanly

### PortalAnchor

Suggested fields:

- `anchor_id`
- `display_name`
- `world_id`
- `density_layer_id`
- `scene_node_path`
- `local_frame`
- `influence_radius`
- `anchor_profile`
- `link_ids`
- `notes`

Suggested meaning:

- binds overspace identity to a real node path
- stores the local entry or exit basis
- provides a stable id for editor tooling, debug labels, and runtime lookup

### PortalLink

Suggested fields:

- `link_id`
- `display_name`
- `source_anchor_id`
- `target_anchor_id`
- `bidirectional`
- `link_kind`
- `preserve_velocity_frame`
- `transit_profile`
- `reverse_link_id`
- `notes`

Suggested meaning:

- the actual wormhole or portal relationship
- carries traversal policy without forcing transport implementation yet
- holds future transform overrides for scale, clock, density, or phase changes

### OverspaceProfile

Suggested fields:

- `profile_id`
- `density_scalar`
- `phase_scalar`
- `clock_scalar`
- `scale_scalar`
- `field_scalar`
- `notes`

Suggested meaning:

- scalar-only transform contract for the scaffold pass
- composable across world, layer, anchor, and link scopes
- safe to attach now even if only density is interpreted later

## Addressing rule

The minimum overspace address should be:

`(world_id, density_layer_id)`

The practical runtime address should usually be:

`(world_id, density_layer_id, anchor_id)`

Reason:

- world-layer identity is the semantic destination
- anchor identity is the concrete entry or exit frame within that destination

That keeps the architecture from collapsing back into a raw scene-node lookup.

## Example hierarchy

### World tree

```text
UniverseGraph: PrimeShelf
├─ WorldNode: SolarSystem
│  ├─ DensityLayer: interplanetary
│  ├─ WorldNode: Earth
│  │  ├─ DensityLayer: surface
│  │  └─ DensityLayer: rabbit_hole_1
│  └─ WorldNode: Sun
│     ├─ DensityLayer: corona
│     └─ DensityLayer: core_echo
└─ WorldNode: ShelfOrbGallery
   └─ DensityLayer: display_shell
```

### Suggested profiles

```text
SolarSystem/interplanetary
- density=1.0 phase=1.0 clock=1.0 scale=1.0 field=1.0

Earth/surface
- density=1.0 phase=1.0 clock=1.0 scale=1.0 field=1.0

Earth/rabbit_hole_1
- density=3.5 phase=1.15 clock=0.92 scale=0.35 field=1.4

Sun/corona
- density=2.2 phase=1.05 clock=0.97 scale=1.0 field=2.8

Sun/core_echo
- density=8.0 phase=1.35 clock=0.65 scale=0.6 field=6.5

ShelfOrbGallery/display_shell
- density=1.0 phase=1.0 clock=1.0 scale=0.2 field=1.0
```

### Anchor and link example

```text
PortalAnchor: earth_rabbit_mouth
- world=Earth
- layer=surface
- scene_node_path=res://Scenes/Earth.tscn::RabbitMouth

PortalAnchor: earth_rabbit_inner
- world=Earth
- layer=rabbit_hole_1
- scene_node_path=res://Scenes/EarthRabbitHole.tscn::EntryAnchor

PortalAnchor: sun_orb_target
- world=Sun
- layer=core_echo
- scene_node_path=res://Scenes/SunCore.tscn::OrbEntry

PortalAnchor: shelf_orb_earth
- world=ShelfOrbGallery
- layer=display_shell
- scene_node_path=res://Scenes/ShelfOrbGallery.tscn::EarthOrb

PortalAnchor: shelf_orb_sun
- world=ShelfOrbGallery
- layer=display_shell
- scene_node_path=res://Scenes/ShelfOrbGallery.tscn::SunOrb
```

```text
PortalLink: earth_surface_to_rabbit
- source=earth_rabbit_mouth
- target=earth_rabbit_inner
- transit_profile(scale=0.35, density=3.5)

PortalLink: shelf_orb_to_earth
- source=shelf_orb_earth
- target=earth_rabbit_mouth
- transit_profile(scale=5.0)

PortalLink: shelf_orb_to_sun
- source=shelf_orb_sun
- target=sun_orb_target
- transit_profile(scale=8.0, clock=0.65, field=6.5)
```

This gives one shelf-orb scene two semantically different destinations even if both are rendered by similar linked-mouth mechanics in the current engine.

## Relationship to the current prototype

The current `WormholePortal` already knows how to:

- identify a linked portal
- map transforms to the linked mouth
- build an exit transform
- drive a linked viewport camera

That should remain the concrete mouth implementation for now.

Overspace should be added above it as metadata:

- `WormholePortal` stays the scene mouth
- `PortalAnchor` identifies where that mouth sits in overspace
- `PortalLink` identifies what it means to traverse that mouth
- `UniverseGraph` becomes the lookup table used by debug tooling, validation, and later transport logic

## Smallest implementation step

The smallest step that fits the current engine without a large rewrite is:

1. Add the `Resource` scaffolding for `UniverseGraph`, `WorldNode`, `DensityLayer`, `PortalAnchor`, `PortalLink`, and `OverspaceProfile`.
2. Add optional overspace ids to the current wormhole authoring workflow by letting each portal mouth be represented by a `PortalAnchor` and `PortalLink`.
3. Use the graph only for lookup, labeling, validation output, and future routing decisions.

In practical terms, the first runtime use should be:

- resolve a portal traversal as `source_anchor -> link -> target_anchor`
- expose the resolved `(world_id, density_layer_id)` in logs, overlays, or validation artifacts
- keep the existing teleport and linked-camera behavior unchanged

That gives the engine immediate semantic structure with near-zero transport risk, and it creates the exact attachment point needed for later scale, clock, and density transforms.
