// Resonance Spheres + Inspiration Cards data model
// Extends the existing AtlasNode concept for pop-culture / mythic / tech alignments
// that have strong, specific "signal resonance" with xPRIMEray (portals, curved transport,
// observer immersion, spatial storytelling, rendering pipelines).

export interface InspirationMedia {
  type: 'image' | 'video' | 'youtube';
  url: string;                    // direct or embed URL
  caption?: string;
  alt?: string;
  thumbnail?: string;             // for grid thumbs
  standardSize?: 'hero' | 'grid'; // 1200x675 hero or 400x225 grid
  resonanceNote?: string;         // why this media resonates with xPRIMEray (e.g. "Trophy Room ringmaster portals as wormhole mouth analog; rendering delivers 'real' depth via curved transport")
}

export interface InspirationNode {
  id: string;                     // e.g. "glitch-digital-circus-trophy-room"
  title: string;
  category: 'pop-culture' | 'physics' | 'tech-history' | 'mythic' | 'game';
  tier: 1 | 2 | 3;                // coherence / priority (Nobel/Glitch first)
  position?: { x: number; y: number; z?: number }; // for constellation / orbit layout
  parentIds?: string[];           // resonances / kinship (e.g. links to wormhole chapters)
  tags: string[];                 // zeno/xeno, portal, traversal, observer, etc.
  summary: string;                // short hook for card
  xprimeRayAlignment: string;     // "signal resonance" text: specific alignment to curved transport, hermetic, observer disagreement, etc.
  media: InspirationMedia[];      // 1+ images (hero + grid), YT, etc. Consistent sizes enforced in UI.
  externalLinks?: Array<{ label: string; url: string; credit?: string }>; // tasteful credits
  resonanceSphereTexture?: string; // reference to the sphere-mapped image (e.g. trophy room with xPRIMEray GRIN refraction distortion)
  // For force-directed / Unknown Knowns Matrix
  zenoXeno?: 'zeno' | 'xeno' | 'both'; // alignment to known/unknown in transport
}

export interface ResonanceSphere {
  id: string;                     // "central-transport-sphere" or "digital-circus-trophy"
  centerNodeId: string;           // the inspiration node that textures this sphere
  textureUrl: string;             // the curated screenshot (Trophy Room etc.) post-processed with xPRIMEray-style curved-ray / GRIN refraction / wormhole edge distortion
  radius: number;
  orbitingNodeIds: string[];      // nodes that orbit or connect via geodesic springs
  description: string;            // "Dynamic canvas: portal screenshots become the living backdrop for the constellation"
}

// The main data for the FractalInspirationAtlas / Resonance Spheres view
export interface ResonanceSpheresData {
  spheres: ResonanceSphere[];
  nodes: InspirationNode[];
  // Connections for force-directed / geodesic lines
  edges: Array<{ source: string; target: string; type: 'resonance' | 'parent' | 'portal-echo' | 'observer-kinship' }>;
}
