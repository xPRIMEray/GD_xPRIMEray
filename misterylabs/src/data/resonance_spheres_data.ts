// Sample / curated data for Resonance Spheres + Inspiration Cards
// Coherence-maxxed: strong, specific alignment to xPRIMEray (portals as wormhole analogs,
// curved spacetime viz, rendering pipelines that deliver "real" depth, observer immersion,
// spatial storytelling, traversal mechanics).
// Nobel / Glitch first, then others pulled from existing atlas (Quake, Interstellar, Bell Labs, etc.).

import { ResonanceSpheresData } from '../types/inspiration';

export const resonanceSpheresData: ResonanceSpheresData = {
  spheres: [
    {
      id: "central-transport-sphere",
      centerNodeId: "glitch-digital-circus-trophy-room",
      textureUrl: "/assets/resonance-spheres/digital-circus-trophy-room-xprime-distorted.png", // generated with xPRIMEray-style post-processing: subtle GRIN refraction on the ringmaster portals + wormhole-edge distortion on the Trophy Room geometry. Use overspace_trophy_room_demo.tscn renders as base.
      radius: 1.0,
      orbitingNodeIds: ["nobel-kip-thorne", "nobel-roger-penrose", "glitch-digital-circus-trophy-room", "interstellar-gargantua", "quake-portal-tech"],
      description: "Central dynamic canvas. Trophy Room portal screenshot (from The Amazing Digital Circus) mapped onto the sphere with xPRIMEray curved-ray / GRIN refraction and wormhole boundary distortion. Nodes orbit and connect via geodesic springs. Hover/click pulses the sphere to the linked portal region."
    }
  ],
  nodes: [
    {
      id: "glitch-digital-circus-trophy-room",
      title: "The Amazing Digital Circus — Trophy Room Portal",
      category: "pop-culture",
      tier: 1,
      position: { x: 0, y: 0, z: 0 },
      tags: ["portal", "ringmaster", "spatial-storytelling", "rendering-pipeline", "observer-immersion"],
      summary: "The Trophy Room as a liminal nexus of portals, games, and trapped observers. The ringmaster's domain is a perfect pop-culture analog for a wormhole mouth + recursive transport space.",
      xprimeRayAlignment: "Strong resonance: The Trophy Room functions as a 'wormhole mouth' where characters are pulled into recursive, rule-bending spaces. xPRIMEray's recursive mirror ghost portal and overspace fixtures are direct technical cousins — both require stable high-bounce reflection + curved transport without banding or energy loss. The rendering pipeline in the show delivers 'real' depth and parallax that straight-ray approximations would break. Perfect backdrop sphere texture candidate (use overspace_trophy_room_demo.tscn renders with added GRIN refraction on the rings and wormhole-edge distortion on the architecture).",
      media: [
        {
          type: "image",
          url: "https://example.com/trophy-room-hero-1200x675.png", // placeholder; in real: promoted from overspace_trophy_room_demo renders + xPRIMEray post-process
          caption: "Trophy Room hero — mapped as central sphere texture with xPRIMEray-style GRIN refraction on portals and wormhole boundary distortion.",
          alt: "The Amazing Digital Circus Trophy Room interior with ringmaster portals, rendered with curved transport distortion",
          standardSize: "hero",
          resonanceNote: "Ringmaster portals = wormhole mouths; recursive rooms = infinite mirror recursion; the 'game' rules = hermetic fixture contracts."
        },
        {
          type: "image",
          url: "https://example.com/trophy-room-grid-400x225.png",
          caption: "Thumbnail grid variant",
          standardSize: "grid"
        },
        {
          type: "youtube",
          url: "https://www.youtube.com/watch?v=example-digital-circus-trophy", // real episode clip or trailer
          caption: "Trophy Room sequence — observe the portal transitions and spatial recursion",
          thumbnail: "https://example.com/trophy-thumb.jpg"
        }
      ],
      externalLinks: [
        { label: "Glitch Productions — The Amazing Digital Circus", url: "https://www.youtube.com/@GLITCH", credit: "Glitch Productions" },
        { label: "Overspace Trophy Room Demo (xPRIMEray scene)", url: "overspace_trophy_room_demo.tscn" }
      ],
      resonanceSphereTexture: "central-transport-sphere",
      zenoXeno: "xeno"
    },
    {
      id: "nobel-kip-thorne",
      title: "Kip Thorne — Black Hole Visualization & Gravitational Lensing",
      category: "physics",
      tier: 1,
      position: { x: 120, y: -80 },
      parentIds: ["glitch-digital-circus-trophy-room"],
      tags: ["curved-spacetime", "gravitational-lensing", "black-hole", "visualization", "nobel"],
      summary: "Nobel laureate physicist whose work on wormholes (Morris–Thorne metric) and black hole lensing (Interstellar) directly parallels xPRIMEray's Gordon effective metric and GRIN null-geodesic integration.",
      xprimeRayAlignment: "Core technical kinship: Thorne's Morris-Thorne traversable wormhole requires exotic matter to keep the throat open — xPRIMEray's wormhole fixtures and overspace topology explicitly test throat stability under curved transport. His gravitational lensing work for Interstellar (Gargantua) is the gold-standard cinematic curved-ray benchmark. xPRIMEray's 'Dual Reality' and 'Observer Disagreement' chapters are the scientific instrument version of what Thorne did for film: show the difference between naive straight transport and real geodesic curvature, with measurable, falsifiable outputs.",
      media: [
        {
          type: "image",
          url: "https://example.com/thorne-lensing-hero.png",
          caption: "Gravitational lensing around a spinning black hole — the visual that made curved null geodesics mainstream",
          standardSize: "hero",
          resonanceNote: "Direct ancestor of xPRIMEray's curved_view.gdshader and the dual-reality comparison pipeline."
        },
        {
          type: "youtube",
          url: "https://www.youtube.com/watch?v=example-kip-thorne-interstellar",
          caption: "Thorne on the science of Interstellar lensing and wormholes"
        }
      ],
      externalLinks: [
        { label: "Kip Thorne Nobel Prize", url: "https://www.nobelprize.org/prizes/physics/2017/thorne/facts/" },
        { label: "The Science of Interstellar (book)", url: "https://en.wikipedia.org/wiki/The_Science_of_Interstellar" }
      ],
      zenoXeno: "zeno"
    },
    {
      id: "nobel-roger-penrose",
      title: "Roger Penrose — Trapped Surfaces, Conformal Diagrams & Singularities",
      category: "physics",
      tier: 1,
      position: { x: -140, y: 60 },
      parentIds: ["nobel-kip-thorne"],
      tags: ["trapped-surfaces", "penrose-diagram", "causal-structure", "singularity", "nobel"],
      summary: "Penrose diagrams and trapped surface theorems are the mathematical language for the causal structure that xPRIMEray's hermetic closure and boundary event ledger make observable in rendered pixels.",
      xprimeRayAlignment: "The 'hermetic fixture contract' (100% pixel classification, zero unresolved exits) is a computational embodiment of Penrose's trapped surface and causal boundary ideas. When rays in xPRIMEray hit a 'trapped' region (high-curvature GRIN shell or wormhole throat), the closure diagnostics and ownership graphs reveal the same topological features Penrose diagrams abstract. The 'unresolved island' in the transport oracle is a pixel-level Penrose singularity made visible.",
      media: [
        {
          type: "image",
          url: "https://example.com/penrose-diagram-hero.png",
          caption: "Classic Penrose diagram of a black hole — causal structure made drawable",
          standardSize: "hero"
        }
      ],
      externalLinks: [
        { label: "Roger Penrose Nobel Prize", url: "https://www.nobelprize.org/prizes/physics/2020/penrose/facts/" },
        { label: "Penrose diagrams (Wikipedia)", url: "https://en.wikipedia.org/wiki/Penrose_diagram" }
      ],
      zenoXeno: "zeno"
    },
    {
      id: "interstellar-gargantua",
      title: "Interstellar — Gargantua Black Hole (Double Negative / Thorne)",
      category: "pop-culture",
      tier: 2,
      position: { x: 80, y: 140 },
      parentIds: ["nobel-kip-thorne"],
      tags: ["black-hole", "gravitational-lensing", "cinematic-rendering", "curved-spacetime"],
      summary: "The most famous cinematic rendering of a real (Kerr) black hole. The accretion disk and photon ring were computed with the same null-geodesic techniques xPRIMEray uses for GRIN fields.",
      xprimeRayAlignment: "xPRIMEray's 'Dual Reality' chapter and the wormhole DR story are the scientific instrument version of what the Interstellar VFX team did for film. The same question: 'What does curved transport actually look like?' The same answer: integrate the geodesics, don't fake it with distortion maps. The Gargantua render is the pop-culture proof that audiences can handle (and are moved by) true curved-ray imagery when it's done right.",
      media: [
        {
          type: "image",
          url: "https://example.com/gargantua-hero.png",
          caption: "Gargantua as rendered for Interstellar — the photon ring and lensing that made curved transport Hollywood-mainstream",
          standardSize: "hero"
        }
      ],
      externalLinks: [
        { label: "How Interstellar's black hole was rendered (Double Negative paper)", url: "https://arxiv.org/abs/1502.03808" }
      ],
      zenoXeno: "zeno"
    }
    // TODO: Add Quake (portal tech / level traversal as early "wormhole" game mechanic), Bell Labs (visualization history), etc. from existing atlas.
  ],
  edges: [
    { source: "glitch-digital-circus-trophy-room", target: "nobel-kip-thorne", type: "resonance" },
    { source: "nobel-kip-thorne", target: "interstellar-gargantua", type: "parent" },
    { source: "nobel-kip-thorne", target: "nobel-roger-penrose", type: "portal-echo" },
    { source: "glitch-digital-circus-trophy-room", target: "interstellar-gargantua", type: "observer-kinship" }
  ]
};

export default resonanceSpheresData;
