All overspace validation fixtures must be hermetically enclosed and produce 100% classified pixel outcomes under GrinFilmCamera transport.

Hermetic Fixture Rule

"A scene promoted to an overspace validation fixture should be hermetically enclosed and should produce 100% classified pixel outcomes under GrinFilmCamera transport, within the configured transport budget."

This is a validation-harness rule for enclosed overspace scenes. It is not a claim about universal physics truth.

🧱 Canon: Hermetic Coverage Rule
Core idea

Any scene promoted to a validation fixture must be hermetically enclosed such that every pixel produces a deterministic outcome under GrinFilmCamera.

Not “looks filled.”
Not “seems fine.”
👉 Every pixel classified.

🧠 Slight refinement (important)

Instead of:

every pixel should have a hit

Make it:

every pixel must return a classified transport result

Why?

Because in a real renderer, valid outcomes include:

geom_hit ✅ (wall, object)
portal_hit 🌀 (wormhole surface)
throat_event 🌉 (boundary layer crossing)
background_hit 🌫️ (if allowed)
escaped_no_hit 🚀 (should be rare in hermetic scenes)
budget_exhausted ⛔ (this is a failure signal)

So your rule becomes stronger and more future-proof.

🔒 Hermetic Fixture Definition

A scene qualifies as a Hermetic Overspace Fixture if:

1. Fully enclosed geometry
No open sky leaks
Camera cannot “see infinity”
All rays must intersect something within budget
2. Sufficient transport budget
Step count high enough
Distance limit high enough
No premature termination
3. Deterministic classification
Every pixel resolves to a known state
No silent nulls
No untracked misses
🧪 Why this is 🔥 powerful
Ibn al-Haytham lens 🧪

You’ve created a closed optical experiment:

no uncontrolled variables
every ray accounted for
Wenzel Jakob lens 🎯

You’ve defined a coverage invariant:

if pixels fail → renderer issue
not scene ambiguity
Your lens 🌀

You now have:

a truth chamber for curved-ray physics

🚀 Immediate next milestone
Phase A.2 — Hermetic Coverage Pass
Goal

Take your existing wormhole room and:

seal it completely
run it through GrinFilmCamera
ensure 100% pixel classification
🔧 Minimal implementation steps
Create Hermetic Room Fixture
Cube room (no gaps)
Wormhole orb inside
Camera fully enclosed
Force GrinFilmCamera path
bypass Godot raster view for validation
use renderer output only
Increase transport limits
step count ↑
max distance ↑
Add classification buffer
per-pixel result enum
stored alongside color
Add coverage metric
% classified pixels
% budget exhausted
% escapes
📊 Success condition

You want:

coverage = 100%
budget_exhausted ≈ 0%
escaped_no_hit ≈ 0%

If not:
👉 you’ve found a renderer limitation, not a scene issue

🧩 Optional (but powerful)

Add a coverage heatmap overlay:

green = valid hit
yellow = portal/throat
red = budget exhausted
black = unclassified (should be zero)

Now your engine literally tells you where it’s blind
