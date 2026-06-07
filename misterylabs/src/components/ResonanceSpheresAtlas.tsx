import React, { useState, useMemo } from 'react';
import { ResonanceSphere } from './ResonanceSphere';
import { resonanceSpheresData, InspirationNode, InspirationMedia } from '../data/resonance_spheres_data';

// ResonanceSpheresAtlas.tsx
// Drop-in replacement / enhancement for FractalInspirationAtlas.tsx
// Features the central Resonance Sphere (textured backdrop) + orbiting inspiration nodes
// in a force-directed constellation ("Unknown Knowns Matrix").
// Click node → rich modal with consistent-size media gallery, YT embeds, xPRIMEray alignment text, credits.
//
// Assumes:
// - Tailwind + shadcn/ui for cards/modal (or replace with your primitives).
// - The sphere component handles texture + subtle xPRIMEray refraction look.
// - Data comes from resonance_spheres_data.ts (extended AtlasNode model with media[]).
//
// Visual style: dark cinematic observatory (deep blacks, cyan/amber accents, geodesic springs).
// Hover nodes: gentle pulse + sphere rotates toward the linked portal region on the texture.
// Mobile: stack sphere + list; zoom/pan on desktop constellation via parent or svg/canvas.

interface ExpandedNodeState {
  node: InspirationNode | null;
  mediaIndex: number;
}

export const ResonanceSpheresAtlas: React.FC = () => {
  const [highlightedNodeId, setHighlightedNodeId] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<ExpandedNodeState>({ node: null, mediaIndex: 0 });

  const { spheres, nodes, edges } = resonanceSpheresData;

  const centralSphere = spheres[0];
  const centralNode = nodes.find(n => n.id === centralSphere.centerNodeId)!;

  // Simple force-directed positions (prototype — replace with d3-force, react-force-graph, or visx in prod)
  const positionedNodes = useMemo(() => {
    return nodes.map((node, i) => {
      // Seed positions around the sphere; in real impl run a simulation with edges as springs
      const angle = (i / nodes.length) * Math.PI * 2 + (node.position?.x || 0) * 0.01;
      const dist = 1.8 + (node.tier === 1 ? 0 : 0.4);
      return {
        ...node,
        x: Math.cos(angle) * dist * 180 + 300,
        y: Math.sin(angle) * dist * 90 + 300,
      };
    });
  }, [nodes]);

  const openNode = (node: InspirationNode) => {
    setExpanded({ node, mediaIndex: 0 });
    setHighlightedNodeId(node.id);
    // In full: animate sphere to highlight the portal region corresponding to this node's texture UV
  };

  const closeModal = () => {
    setExpanded({ node: null, mediaIndex: 0 });
    setHighlightedNodeId(null);
  };

  const currentMedia = expanded.node?.media[expanded.mediaIndex];

  return (
    <div className="resonance-spheres-atlas bg-[#0a0a0f] text-white min-h-screen p-4 md:p-8 font-mono">
      <header className="max-w-5xl mx-auto mb-8">
        <h1 className="text-4xl tracking-[4px] mb-2">RESONANCE SPHERES</h1>
        <p className="text-cyan-400/80 text-sm">A living constellation of portal echoes, curved spacetime kinship, and observer immersion — anchored to xPRIMEray.</p>
        <p className="text-[10px] text-amber-400/60 mt-1">Coherence-maxxed. Nobel first, Glitch (Digital Circus Trophy Room) as central sphere texture.</p>
      </header>

      {/* Central Resonance Sphere + Constellation */}
      <div className="relative max-w-[620px] mx-auto mb-12">
        <ResonanceSphere
          textureUrl={centralSphere.textureUrl}
          highlightedNodeId={highlightedNodeId}
          onNodeHighlight={(id) => {
            const n = nodes.find(nn => nn.id === id);
            if (n) openNode(n);
          }}
          className="mx-auto"
        />

        {/* Orbiting / force-directed nodes as SVG overlay for prototype (clean lines + labels) */}
        <svg
          className="absolute inset-0 pointer-events-none"
          width="600"
          height="600"
          viewBox="0 0 600 600"
        >
          {/* Geodesic / spring lines */}
          {edges.map((edge, idx) => {
            const src = positionedNodes.find(n => n.id === edge.source);
            const tgt = positionedNodes.find(n => n.id === edge.target);
            if (!src || !tgt) return null;
            return (
              <line
                key={idx}
                x1={src.x} y1={src.y} x2={tgt.x} y2={tgt.y}
                stroke="#00ffff" strokeOpacity={0.25} strokeWidth={1}
              />
            );
          })}

          {/* Nodes */}
          {positionedNodes.map((node) => {
            const isCentral = node.id === centralSphere.centerNodeId;
            const isHighlighted = highlightedNodeId === node.id;
            return (
              <g key={node.id} className="cursor-pointer" onClick={() => openNode(node)}>
                <circle
                  cx={node.x} cy={node.y} r={isCentral ? 14 : 9}
                  fill={isHighlighted ? '#00ffff' : (node.tier === 1 ? '#ffcc66' : '#8899aa')}
                  stroke="#112233" strokeWidth={2}
                  className={isHighlighted ? 'animate-pulse' : ''}
                />
                <text
                  x={node.x} y={node.y + (isCentral ? 22 : 18)}
                  textAnchor="middle"
                  fontSize={isCentral ? 10 : 8}
                  fill="#aabbcc"
                  className="select-none"
                >
                  {node.title.split(' — ')[0]}
                </text>
              </g>
            );
          })}
        </svg>

        <div className="text-center text-[10px] text-white/50 mt-2">Click nodes or the sphere. Hover for resonance pulse.</div>
      </div>

      {/* Quick Unknown Knowns Matrix / list for mobile + legend */}
      <div className="max-w-4xl mx-auto grid grid-cols-1 md:grid-cols-2 gap-4 mb-12">
        {positionedNodes.slice(0, 6).map(node => (
          <button
            key={node.id}
            onClick={() => openNode(node)}
            className="text-left p-4 border border-white/10 hover:border-cyan-400/40 bg-black/30 rounded transition-all"
          >
            <div className="text-amber-400 text-xs tracking-widest">{node.category.toUpperCase()} • TIER {node.tier}</div>
            <div className="font-medium text-lg leading-tight mt-1">{node.title}</div>
            <div className="text-white/70 text-sm mt-2 line-clamp-3">{node.xprimeRayAlignment}</div>
            <div className="text-[10px] text-cyan-400/70 mt-3">→ VIEW RESONANCE CARD + MEDIA</div>
          </button>
        ))}
      </div>

      {/* Expanded Node Modal / Card */}
      {expanded.node && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/90 p-4" onClick={closeModal}>
          <div
            className="bg-[#0a0a0f] border border-white/10 max-w-4xl w-full rounded-xl overflow-hidden"
            onClick={e => e.stopPropagation()}
          >
            <div className="p-6 md:p-8">
              <div className="flex justify-between items-start mb-4">
                <div>
                  <div className="uppercase text-xs tracking-[3px] text-amber-400">{expanded.node.category} • TIER {expanded.node.tier}</div>
                  <h2 className="text-3xl mt-1">{expanded.node.title}</h2>
                </div>
                <button onClick={closeModal} className="text-white/50 hover:text-white text-2xl leading-none">×</button>
              </div>

              <p className="text-white/80 mb-6">{expanded.node.xprimeRayAlignment}</p>

              {/* Media Gallery — consistent sizing enforced */}
              <div className="mb-6">
                <div className="flex gap-2 mb-2 text-xs uppercase tracking-widest text-white/50">
                  MEDIA — {expanded.node.media.length} attachments
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {expanded.node.media.map((m, idx) => (
                    <div key={idx} className="border border-white/10 overflow-hidden rounded">
                      {m.type === 'image' && (
                        <img
                          src={m.url}
                          alt={m.alt || m.caption}
                          className="w-full h-auto object-cover"
                          style={{ aspectRatio: m.standardSize === 'hero' ? '16 / 9' : '16 / 9' }} // enforce consistent
                        />
                      )}
                      {m.type === 'youtube' && (
                        <div className="aspect-video bg-black">
                          <iframe
                            src={m.url.replace('watch?v=', 'embed/')}
                            title={m.caption}
                            className="w-full h-full"
                            allowFullScreen
                          />
                        </div>
                      )}
                      {m.caption && <div className="p-2 text-xs text-white/60 border-t border-white/10">{m.caption}</div>}
                      {m.resonanceNote && <div className="p-2 text-[10px] text-cyan-400/80 italic">{m.resonanceNote}</div>}
                    </div>
                  ))}
                </div>
              </div>

              {/* External links + credits (tasteful) */}
              {expanded.node.externalLinks && expanded.node.externalLinks.length > 0 && (
                <div className="text-xs text-white/60 mb-4">
                  {expanded.node.externalLinks.map((l, i) => (
                    <span key={i}>
                      <a href={l.url} target="_blank" rel="noopener" className="underline hover:text-cyan-400">{l.label}</a>
                      {l.credit && <span className="text-white/40"> — {l.credit}</span>}
                      {i < expanded.node.externalLinks!.length - 1 && ' · '}
                    </span>
                  ))}
                </div>
              )}

              <div className="text-[10px] text-white/40">Resonance Echo — kinship with xPRIMEray curved transport, hermetic contracts, and observer-relative structure.</div>
            </div>
          </div>
        </div>
      )}

      <footer className="max-w-5xl mx-auto text-[10px] text-white/30 pt-8 border-t border-white/10">
        Resonance Spheres — a living exhibit. Central texture generated from xPRIMEray renders (e.g. overspace_trophy_room_demo.tscn) with added GRIN refraction and wormhole-edge distortion. All credits to original creators. This is kinship, not appropriation.
      </footer>
    </div>
  );
};

export default ResonanceSpheresAtlas;
