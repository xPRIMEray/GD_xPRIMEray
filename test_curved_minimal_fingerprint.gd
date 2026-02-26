extends Node3D

const FIELD_PATH := NodePath("FixtureCurvedMinimal/FieldSource3D")

const FIXED_STRENGTH := 1.15
const FIXED_SOFTENING := 0.1
const FIXED_OUTER_RADIUS := 4.5
const FIXED_OVERRIDE_GAMMA := true
const FIXED_GAMMA := 2.0
const FIXED_DEBUG_DRAW_BOUNDS := false
const FIXED_DEBUG_DRAW_IN_GAME := false


func _ready() -> void:
	var field = get_node_or_null(FIELD_PATH)
	if field == null:
		push_warning("CurvedMinimal fingerprint: missing FieldSource3D at %s" % FIELD_PATH)
		return

	field.set("Strength", FIXED_STRENGTH)
	field.set("Softening", FIXED_SOFTENING)
	field.set("OuterRadius", FIXED_OUTER_RADIUS)
	field.set("OverrideGamma", FIXED_OVERRIDE_GAMMA)
	field.set("Gamma", FIXED_GAMMA)
	field.set("DebugDrawBounds", FIXED_DEBUG_DRAW_BOUNDS)
	field.set("DebugDrawInGame", FIXED_DEBUG_DRAW_IN_GAME)

	print(
		"CURVED_MINIMAL_FP path=%s Strength=%.3f Softening=%.3f OuterRadius=%.3f OverrideGamma=%s Gamma=%.3f DebugDrawBounds=%s DebugDrawInGame=%s"
		% [
			str(field.get_path()),
			FIXED_STRENGTH,
			FIXED_SOFTENING,
			FIXED_OUTER_RADIUS,
			str(FIXED_OVERRIDE_GAMMA).to_lower(),
			FIXED_GAMMA,
			str(FIXED_DEBUG_DRAW_BOUNDS).to_lower(),
			str(FIXED_DEBUG_DRAW_IN_GAME).to_lower(),
		]
	)
