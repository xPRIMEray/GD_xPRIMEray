# Sphere Texture Prompt — Digital Circus Trophy Room (xPRIMEray Resonance)

**Base Image (reference):** High-fidelity screenshot or render of the Trophy Room interior from The Amazing Digital Circus (Glitch Productions). Focus on the central ringmaster portal architecture, the floating trophies/frames, the dramatic lighting, and the sense of recursive depth / trapped observers.

**xPRIMEray-style Post-Processing Instructions (for Midjourney / Stable Diffusion / Flux / your renderer):**

"Ultra-detailed cinematic render of the Digital Circus Trophy Room mapped onto a perfect sphere for use as a 3D texture / environment map. Subtle but unmistakable xPRIMEray curved-ray / GRIN refraction distortion on all portal rings and floating frames — light bends exactly as in a Gordon effective metric null geodesic. Wormhole-edge chromatic aberration and soft gravitational lensing at the boundaries of the main portal. The ringmaster's central 'throat' shows asymmetric compression and background starfield / void distortion identical to a traversable wormhole mouth. Deep blacks with cyan and warm amber rim lighting. No text, no UI. Photorealistic material response on the trophies and architecture but with the characteristic 'curved transport' softening and color fringing that only appears when rays are actually integrated through a refractive field instead of post-processed. 8k resolution, seamless equirectangular projection suitable for sphere texturing in Three.js or Godot. Cinematic observatory mood, high dynamic range, subtle god rays leaking through the distorted portals."

**Negative prompt ideas:** straight lines, no distortion, clean perspective, flat shading, text, logos, UI elements, cartoonish, low contrast.

**Usage in Resonance Sphere:**
- The resulting texture becomes the `textureUrl` for the central-transport-sphere.
- When a linked node (e.g. Nobel black hole lensing or another portal) is highlighted, the sphere can rotate or a shader can highlight the corresponding UV region on the Trophy Room texture.
- This creates the "the portal screenshot is the living backdrop" effect described in the task.

Generate 2-3 variants:
1. Subtle GRIN (gentle bend, good for hero).
2. Stronger wormhole distortion (more dramatic for close-up modal).
3. Night / deep-space version with more void leakage.

Once generated, place in misterylabs/public/assets/resonance-spheres/ and reference in resonance_spheres_data.ts.

This prompt is deliberately written to produce an image that feels like it was rendered *inside* an xPRIMEray scene rather than a generic portal art.
