import React from 'react';
import { ResonanceSpheresAtlas } from './ResonanceSpheresAtlas';

// Atlas.tsx (or the main Observatory / Inspirations page)
// Prominently features the new Resonance Spheres experience as the flagship interactive.
// This is the drop-in enhancement to the previous FractalInspirationAtlas concept.

export const Atlas: React.FC = () => {
  return (
    <div className="min-h-screen bg-[#050507] text-white">
      <div className="border-b border-white/10 py-4 px-8 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-full bg-gradient-to-br from-cyan-400 to-amber-400" />
          <div>
            <div className="font-semibold tracking-[2px]">MISTERY LABS</div>
            <div className="text-[10px] text-white/50 -mt-1">xPRIMEray Observatory</div>
          </div>
        </div>
        <div className="text-xs text-white/40">RESONANCE SPHERES — v0.1</div>
      </div>

      <ResonanceSpheresAtlas />

      {/* Legacy / supporting sections can live below or in tabs */}
      <div className="max-w-5xl mx-auto px-8 py-12 text-white/60 text-sm border-t border-white/10 mt-12">
        <p>This is the primary interactive for the Inspirations / Resonance section. It replaces or augments the previous force-directed FractalInspirationAtlas with a sphere-anchored, media-rich, xPRIMEray-coherent experience.</p>
        <p className="mt-2">All data lives in <code>src/data/resonance_spheres_data.ts</code>. Add new high-coherence nodes there (Nobel, Glitch, existing atlas entries). Generate sphere textures using the prompt in the same folder.</p>
      </div>
    </div>
  );
};

export default Atlas;
