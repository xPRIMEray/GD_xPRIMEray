import React, { useRef, useEffect } from 'react';
import * as THREE from 'three'; // Assume three.js is installed for real sphere + texture mapping. For pure CSS fallback see below.

// ResonanceSphere.tsx
// Central dynamic canvas for the Resonance Spheres + Inspiration Cards exhibit.
// Textured with a curated inspiration image (e.g. Digital Circus Trophy Room portal screenshot)
// post-processed with xPRIMEray-style curved-ray / GRIN refraction and wormhole-edge distortion.
// Nodes orbit or spring-connect to it. Click on sphere or nodes triggers highlight / modal.
//
// Integration notes:
// - Feed textureUrl from ResonanceSpheresData.spheres[0].textureUrl (the generated "xPRIMEray-distorted" Trophy Room).
// - On node hover/click from parent constellation: rotate sphere to face the linked portal region (use UV mapping or highlight shader).
// - Subtle animation: gentle rotation + pulse on resonance events.
// - Accessibility: ARIA labels, keyboard focus on nodes.
// - Performance: texture lazy-loaded, WebGL context managed.
//
// For non-Three.js fallback (pure web): use CSS 3D + background-image on a .sphere div with perspective + transform for pseudo-sphere, or Canvas 2D sphere projection + image mapping.
// The real beauty comes from feeding an actual xPRIMEray render (from overspace_trophy_room_demo.tscn + post-process shader) as the texture.

interface ResonanceSphereProps {
  textureUrl: string;
  radius?: number;
  onNodeHighlight?: (nodeId: string) => void; // called when parent highlights a linked node
  highlightedNodeId?: string | null;
  className?: string;
}

export const ResonanceSphere: React.FC<ResonanceSphereProps> = ({
  textureUrl,
  radius = 1,
  onNodeHighlight,
  highlightedNodeId,
  className = '',
}) => {
  const mountRef = useRef<HTMLDivElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const sphereRef = useRef<THREE.Mesh | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);

  useEffect(() => {
    if (!mountRef.current) return;

    // Three.js setup (real implementation)
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(75, 1, 0.1, 1000);
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setSize(600, 600); // fixed for hero; make responsive in parent
    mountRef.current.appendChild(renderer.domElement);

    // Lighting for cinematic observatory feel (subtle rim + ambient)
    const ambient = new THREE.AmbientLight(0x111122, 0.6);
    scene.add(ambient);
    const rim = new THREE.DirectionalLight(0x00ffff, 0.8);
    rim.position.set(5, 3, 5);
    scene.add(rim);

    // Sphere geometry + material with the resonance texture
    const geometry = new THREE.SphereGeometry(radius, 64, 64);
    const textureLoader = new THREE.TextureLoader();
    const texture = textureLoader.load(textureUrl, () => {
      // subtle refraction simulation note: in production feed a pre-distorted texture
      // generated from xPRIMEray render (curved_view + GRIN on the portal rings + edge warp)
    });
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;

    const material = new THREE.MeshPhongMaterial({
      map: texture,
      shininess: 15,
      specular: 0x112233,
      transparent: true,
      opacity: 0.95,
    });

    const sphere = new THREE.Mesh(geometry, material);
    scene.add(sphere);
    sphereRef.current = sphere;
    sceneRef.current = scene;
    rendererRef.current = renderer;

    camera.position.z = 2.2;

    // Gentle auto-rotation (cinematic observatory idle)
    let raf: number;
    const animate = () => {
      raf = requestAnimationFrame(animate);
      if (sphere) {
        sphere.rotation.y += 0.0008; // slow, contemplative
        // subtle "pulse" on highlight
        if (highlightedNodeId) {
          sphere.scale.setScalar(1 + Math.sin(Date.now() / 300) * 0.015);
        } else {
          sphere.scale.setScalar(1);
        }
      }
      renderer.render(scene, camera);
    };
    animate();

    // Basic interaction: click sphere to "pulse" or trigger parent callback for central node
    const onClick = () => {
      if (onNodeHighlight && sphereRef.current) {
        // In full impl: raycast or use UV to find which "portal region" was clicked and highlight linked node
        onNodeHighlight('glitch-digital-circus-trophy-room'); // demo: central node
      }
    };
    renderer.domElement.addEventListener('click', onClick);

    // Cleanup
    return () => {
      cancelAnimationFrame(raf);
      renderer.domElement.removeEventListener('click', onClick);
      if (mountRef.current) {
        mountRef.current.removeChild(renderer.domElement);
      }
      renderer.dispose();
    };
  }, [textureUrl, radius, highlightedNodeId, onNodeHighlight]);

  // CSS 3D fallback for environments without WebGL / three (or while loading)
  const cssFallback = (
    <div
      className={`resonance-sphere-fallback ${className}`}
      style={{
        width: 600,
        height: 600,
        borderRadius: '50%',
        background: `url(${textureUrl}) center/cover`,
        boxShadow: '0 0 80px rgba(0,255,255,0.15), inset 0 0 120px rgba(0,0,0,0.6)',
        transform: 'perspective(1200px) rotateY(12deg) rotateX(6deg)',
        transition: 'transform 0.4s ease',
        position: 'relative',
        overflow: 'hidden',
      }}
      onClick={() => onNodeHighlight?.('glitch-digital-circus-trophy-room')}
      aria-label="Resonance Sphere — Trophy Room portal with xPRIMEray curved transport distortion"
    >
      {/* Subtle force lines / geodesic overlay for visual interest */}
      <div style={{ position: 'absolute', inset: 0, background: 'radial-gradient(circle, rgba(0,255,255,0.08) 1px, transparent 1px)', backgroundSize: '12px 12px' }} />
      {highlightedNodeId && (
        <div style={{ position: 'absolute', inset: 0, border: '2px solid #00ffff', borderRadius: '50%', animation: 'pulse 1.5s infinite' }} />
      )}
    </div>
  );

  return (
    <div ref={mountRef} className={`resonance-sphere ${className}`} style={{ width: 600, height: 600 }}>
      {/* The Three.js canvas mounts here. Fallback shown until WebGL ready or for no-three builds. */}
      {/* In production: always prefer the WebGL version for proper sphere mapping + refraction simulation. */}
      {cssFallback}
    </div>
  );
};

export default ResonanceSphere;
