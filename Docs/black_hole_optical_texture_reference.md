# Black Hole Optical Texture Reference

This reference complements [Architecture Overview](architecture_overview.md) and [Metric Transport Next-Gen Roadmap](metric_transport_nextgen_roadmap.md) by defining the image-level black-hole phenomena the renderer should eventually reproduce.

## Purpose

This document defines the expected optical characteristics of black holes as they should appear in physically accurate ray transport models.

These characteristics collectively form what we refer to as gravitational optical texture — the visual fingerprint produced by curved spacetime acting on bundles of light rays.

The purpose of this reference is to:

define the visual phenomena expected from General Relativity

provide validation targets for the renderer

clarify what approximations (GRIN / Gordon metric) preserve vs what full metric geodesic transport preserves

provide terminology for interpreting rendering results

## Definition: Gravitational Optical Texture

Gravitational optical texture refers to the visual pattern produced when curved spacetime shapes bundles of light rays (null congruences) near a compact object such as a black hole.

This includes:

shadow geometry

photon rings

lensing distortion

caustics

higher-order images

asymmetry due to spin

In practice, gravitational texture is what allows a camera to visually reveal the geometry of spacetime.

The Event Horizon Telescope image of M87* is an example of gravitational texture observed in nature.

## Major Optical Characteristics

### 1. Shadow / Critical Curve

The black hole shadow is the region of the image plane where rays fall into the event horizon.

The boundary of this region is the critical curve.

Properties:

defines the silhouette of the black hole

depends strongly on the spacetime metric

relatively insensitive to emission physics

Visual characteristics:

nearly circular for Schwarzschild

asymmetric for Kerr

extremely sharp transition between captured and escaping rays

Importance:

The shadow diameter is one of the most robust observational signatures of General Relativity.

### 2. Photon Ring

The photon ring arises from rays that pass near unstable photon orbits and escape toward the camera.

Properties:

corresponds to rays that orbit the black hole before escaping

forms a thin ring around the shadow

encodes strong-field spacetime geometry

Visual characteristics:

narrow bright ring near the shadow edge

extremely sensitive to the metric

contains nested higher-order sub-rings

Importance:

The photon ring is one of the strongest indicators of true geodesic transport.

### 3. Higher-Order Images

Some photons loop around the black hole multiple times before escaping.

Each additional orbit produces a higher-order image.

Properties:

infinite sequence in theory

exponentially decreasing brightness

Visual characteristics:

nested thin rings

repeated distorted background features

Importance:

Higher-order images strongly depend on accurate geodesic integration.

### 4. Deflection Field

The deflection field describes how incoming directions map to outgoing camera directions.

Properties:

varies strongly with impact parameter

diverges near the photon sphere

Visual characteristics:

background objects shift position

arcs and distortions appear near the shadow

Importance:

The deflection field defines the large-scale lensing behavior of the black hole.

### 5. Impact Parameter Response

Ray behavior depends strongly on the impact parameter (closest approach distance).

Three regimes exist:

capture
skimming / orbiting
escape

Visual characteristics:

defines shadow size

determines ring location

controls image distortion

Importance:

Accurate impact-parameter response requires correct geodesic transport.

### 6. Caustics

Caustics occur where ray bundles fold and concentrate light.

Properties:

appear where mapping between sky and image becomes singular

amplify brightness

Visual characteristics:

bright arcs

thin lensing features

image duplication

Importance:

Caustics represent the folding of null congruences and are an important part of gravitational texture.

### 7. Null Congruence Behavior

Light propagates in bundles, not isolated rays.

These bundles experience:

focusing

shear

twist

These effects are governed by the Raychaudhuri equation in General Relativity.

Visual characteristics:

magnification patterns

stretched or compressed images

sheared background objects

Importance:

These bundle effects produce the subtle structure of gravitational lensing.

### 8. Capture Topology

Rays near a black hole have three possible outcomes:

captured by the horizon
orbiting near the photon sphere
escaping toward infinity

Visual characteristics:

defines shadow occupancy

determines silhouette edge

governs transition between dark and lensed regions

### 9. Frame Dragging (Kerr)

For rotating black holes, spacetime itself is dragged around the hole.

This produces:

asymmetric shadow

skewed photon ring

different behavior for prograde vs retrograde rays

Visual characteristics:

lopsided ring brightness

displaced shadow center

asymmetric lensing

Importance:

Frame dragging is a defining feature of Kerr spacetime.

## GRIN vs Metric Transport

The renderer currently supports GRIN-style optical bending and is evolving toward full metric transport.

### GRIN / Optical Approximation

GRIN transport can reproduce:

smooth bending

general lensing distortion

qualitative arcs and rings

However it typically cannot preserve:

exact shadow boundary

correct photon ring structure

higher-order images

true caustic structure

Kerr asymmetry

### Full Metric Geodesic Transport

Metric transport preserves the true null geodesic structure of spacetime.

This enables accurate reproduction of:

shadow geometry

photon rings

higher-order images

caustics

spin asymmetry

correct deflection laws

Metric transport is therefore required to reproduce the full gravitational optical texture of black holes.

## Relationship to Renderer Architecture

In the renderer architecture:

Scene geometry
    ↓
FieldSource3D
    ↓
Transport model
    ↓
Ray propagation
    ↓
Hit testing
    ↓
Film accumulation

Two transport models are currently relevant:

GRIN_Optical
Metric_NullGeodesic

GRIN provides a useful baseline for validating renderer behavior.

Metric transport will eventually implement persistent geodesic integration using spacetime connection coefficients (Christoffel symbols).

## Validation Strategy

Visual validation should compare three cases:

Straight rays (no field)

GRIN approximation

Metric geodesic transport

Differences between cases reveal where optical approximations diverge from General Relativity.

Particular attention should be paid to:

shadow edge

photon ring structure

higher-order images

caustics

asymmetry

These features collectively define the gravitational texture of the rendered scene.

## Summary

Gravitational optical texture is the image-level signature of curved spacetime acting on bundles of light rays.

Accurate rendering of this texture requires geodesic transport rather than simple optical bending.

Understanding and validating these features provides a clear path toward physically meaningful black-hole visualization.
