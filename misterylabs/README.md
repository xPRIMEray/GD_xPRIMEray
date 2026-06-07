# MisterY Labs — Resonance Spheres + Inspiration Cards (Frontend)

This directory contains the React + TypeScript implementation for the "Resonance Spheres" enhancement to the Observatory / Atlas / Inspirations section.

## What Was Built (per the agent prompt)

- **Data model extension** (`src/types/inspiration.ts` + `src/data/resonance_spheres_data.ts`):
  - `InspirationMedia` and `InspirationNode` with `media[]` supporting image / video / youtube at consistent sizes (hero 16:9, grid).
  - Explicit `xprimeRayAlignment` (the "signal resonance" text).
  - `ResonanceSphere` + `ResonanceSpheresData` with texture + orbiting nodes + geodesic edges.
  - Prioritized high-coherence nodes: Nobel (Thorne, Penrose), Glitch/Digital Circus Trophy Room as the central sphere texture (using existing `overspace_trophy_room_demo.tscn` as base), Interstellar Gargantua.

- **Visual components**:
  - `ResonanceSphere.tsx`: Three.js (preferred) or CSS 3D fallback sphere with the curated texture. Supports highlight pulse, click-to-central-node, subtle observatory rotation.
  - `ResonanceSpheresAtlas.tsx`: Full drop-in experience — central sphere + force-directed constellation (SVG prototype for lines/nodes; swap in d3-force or react-force-graph), rich modal with media gallery + YT embeds + resonance text + tasteful credits.
  - `Atlas.tsx`: Wires the new component prominently.

- **Sphere texture prompt** (`src/data/sphere_texture_prompt.md`): Ready-to-use prompt for generating the "Trophy Room with xPRIMEray GRIN refraction + wormhole edge distortion" equirectangular texture.

- **Data promotion**:
  - New entry in `misterylabs_artifacts/manifest.json`.
  - New curated card `misterylabs_artifacts/cards/resonance-sphere-digital-circus-trophy-room.md`.

## How to Integrate (into existing MisterY Labs React app)

1. Copy `misterylabs/src/` into your frontend.
2. Install `three` (and optionally `@types/three`, d3-force or react-force-graph for production constellation).
3. Import `<ResonanceSpheresAtlas />` (or `<Atlas />`) into your Observatory / Inspirations route.
4. Place generated sphere textures in `public/assets/resonance-spheres/`.
5. Wire the existing `TransportSphereViz.tsx` / `EvidenceGallery.tsx` / `FractalInspirationAtlas.tsx` concepts into this — the data model is deliberately compatible with prior atlas nodes.
6. The central sphere uses the Digital Circus Trophy Room (or any portal screenshot) as texture. On node highlight, rotate the sphere or apply a highlight shader to the corresponding "portal region" on the texture.

## Visual & Tone Notes (from the prompt)

- Dark cinematic observatory (deep blacks, cyan/amber accents).
- Hover/click: nodes pulse, sphere rotates to the linked portal, faint geodesic springs.
- Consistent media sizes enforced in the gallery.
- Credits always tasteful and framed as "Resonance Echo — kinship".
- Mobile-first with desktop constellation richness.

## Next Immediate Steps (after this skeleton)

- Generate the actual sphere texture(s) using the prompt (base renders from the overspace_trophy_room_demo scene + xPRIMEray post-process).
- Add 2–3 more nodes from the existing atlas (Quake, Bell Labs, etc.).
- Replace the SVG constellation prototype with a real force simulation.
- Hook into Supabase or your curation admin flow for easy node addition.
- Feature on the homepage / Observatory landing as the "living canvas" for inspirations.

This makes the Atlas feel alive and deeply tied to xPRIMEray while staying true to the pop-culture / physics kinship that already lives in the observatory_atlas and misterylabs_artifacts.

Run `npm run dev` in your misterylabs frontend after dropping the files in, and the Resonance Spheres experience should light up.
