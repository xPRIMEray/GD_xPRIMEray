extends SceneTree

var scene_root: Node
var chamber: Node
var film: Node
var film_controller: Node
var field_dial: Node
var hermetic_display: Node

func _initialize() -> void:
	scene_root = load("res://ObservatoryWorkbench.tscn").instantiate()
	get_root().add_child(scene_root)
	await process_frame
	await process_frame
	await process_frame
	chamber = scene_root.get_node("PlayableWorld/TransportChamberWorld")
	film = chamber.get_node("GrinFilmCamera")
	film_controller = chamber.get_node("FilmController")
	field_dial = chamber.get_node("FieldDialController")
	hermetic_display = chamber.get_node("HermeticRoomDisplay")
	await _settle(30)

	_send_key(KEY_F)
	await _settle(4)
	_assert(field_dial.call("GetExperimentName") == "Gallery", "F preserves Gallery experiment")
	_assert(_all_field_sources_visible(), "F enables all field structures")
	_assert(not bool(hermetic_display.visible), "Gallery exposes Hermetic apparatus")
	_send_key(KEY_H)
	await _settle(4)
	_assert(field_dial.call("GetExperimentName") == "Gallery", "H is inert")
	_assert(not bool(hermetic_display.visible), "H does not reveal Hermetic apparatus")

	_assert(not bool(film.get("ProbeViewAvailable")), "Hermetic starts without sealed authority")

	_send_key(KEY_E)
	await _settle(8)
	_assert(field_dial.call("GetExperimentName") == "Gallery", "E is reserved and inert")
	_assert(not bool(hermetic_display.visible), "E does not reveal Hermetic apparatus")
	_assert(not bool(film.get("ProbeViewAvailable")), "E does not create sealed authority")

	field_dial.call("SetDisplayPresetForTesting", "Hermetic")
	await _settle(8)
	_assert(field_dial.call("GetExperimentName") == "Hermetic", "programmatic Hermetic selection remains available")
	_assert(bool(hermetic_display.visible), "programmatic selection establishes Hermetic apparatus")

	_send_key(KEY_G)
	await _settle(2)
	_assert(film_controller.call("GetModeName") == "SNAPSHOT", "G enters SNAPSHOT")
	await _wait_for_complete()
	_assert(bool(film.get("ProbeViewAvailable")), "formal Snapshot seals authority")
	_assert(int(film.get("SealedProbeViewGeneration")) == 1, "first public Snapshot generation")
	var generation := int(film.get("SealedProbeViewGeneration"))
	var terminal_heartbeat_count := int(film.get("RuntimeHealthHeartbeatCount"))
	await _settle(8)
	_assert(int(film.get("RuntimeHealthHeartbeatCount")) == terminal_heartbeat_count,
		"terminal Snapshot stops runtime-health heartbeat work")
	var measurement_identity := _measurement_identity()
	var observer_transform := _observer_transform()

	_send_key(KEY_F)
	await _settle(4)
	_assert(bool(film.get("ProbeViewAvailable")), "F does not invalidate seal")
	_assert(int(film.get("SealedProbeViewGeneration")) == generation, "F preserves generation")
	_assert(_measurement_identity() == measurement_identity, "F changes measurement identity")
	_assert(_observer_transform() == observer_transform, "F changes observer transform")
	_send_key(KEY_F)
	await _settle(4)

	_send_key(KEY_Q)
	await _settle(2)
	_assert(int(film.get("CurrentProbeView")) == 1, "Q maps Outcome to Contact Events")
	_send_key(KEY_Q)
	await _settle(2)
	_assert(int(film.get("CurrentProbeView")) == 2, "Q maps Contact Events to Effort")
	# Shift+Q is represented as a modified Q event by the routed input path.
	var reverse := InputEventKey.new()
	reverse.keycode = KEY_Q
	reverse.physical_keycode = KEY_Q
	reverse.pressed = true
	reverse.echo = false
	reverse.shift_pressed = true
	Input.parse_input_event(reverse)
	await _settle(2)
	_assert(int(film.get("CurrentProbeView")) == 1, "Shift+Q maps Effort back to Contact Events")
	_assert(int(film.get("SealedProbeViewGeneration")) == generation, "Q preserves generation")
	_assert(_measurement_identity() == measurement_identity, "Q changes measurement identity")
	_assert(int(film.get("RuntimeHealthHeartbeatCount")) == terminal_heartbeat_count,
		"Q does not restart runtime-health heartbeat work")

	_send_key(KEY_E)
	await _settle(6)
	_assert(field_dial.call("GetExperimentName") == "Hermetic", "E remains inert after Complete")
	_assert(bool(film.get("ProbeViewAvailable")), "E preserves sealed authority")
	_assert(int(film.get("SealedProbeViewGeneration")) == generation, "E preserves generation")
	_assert(_measurement_identity() == measurement_identity, "E changes measurement identity")

	field_dial.call("SetDisplayPresetForTesting", "Gallery")
	await _settle(6)
	_assert(not bool(film.get("ProbeViewAvailable")), "programmatic experiment selection invalidates sealed authority")
	_assert(not bool(hermetic_display.visible), "Gallery retains Hermetic apparatus")
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "invalidated", "experiment invalidation state")
	_assert(not bool(chamber.get_node("TransportChamberPlayer").call("IsPoseHeld")), "experiment change releases held pose")

	_send_key(KEY_G)
	await _settle(4)
	_assert(film_controller.call("GetModeName") == "LIVE", "Snapshot to LIVE transition")
	_assert(not bool(film.get("CathedralSnapshotLifecycleActive")), "LIVE does not overlap Snapshot owner")
	_send_key(KEY_G)
	await _settle(2)
	_assert(film_controller.call("GetModeName") == "OFF", "LIVE to OFF transition")
	var off_heartbeat_count := int(film.get("RuntimeHealthHeartbeatCount"))
	await _settle(8)
	_assert(int(film.get("RuntimeHealthHeartbeatCount")) == off_heartbeat_count,
		"OFF remains free of runtime-health heartbeat work")
	_send_key(KEY_G)
	await _settle(2)
	_assert(film_controller.call("GetModeName") == "SNAPSHOT", "second G enters new Snapshot")
	await _wait_for_complete()
	_assert(bool(film.get("ProbeViewAvailable")), "second Snapshot seals authority")
	var second_generation := int(film.get("SealedProbeViewGeneration"))
	_assert(second_generation > generation, "second public Snapshot generation is newer")
	print("PUBLIC CATHEDRAL SNAPSHOT PATH PASS generation=%d" % second_generation)
	quit(0)

func _wait_for_complete() -> void:
	for _i in range(600):
		await process_frame
		if bool(film.get("ProbeViewAvailable")):
			return
	_fail("formal Snapshot timeout")

func _all_field_sources_visible() -> bool:
	for source in scene_root.get_tree().get_nodes_in_group("field_sources"):
		if bool(source.get("Enabled")) and bool(source.get("DebugVizEnabled")) and not bool(source.get("DebugVizInGame")):
			return false
	return true

func _measurement_identity() -> String:
	return "%s|%s|%s|%s|%s|%s" % [
		str(film.get("CathedralSpatialKernelGeometrySnapshotSha256")),
		str(film.get("CathedralSpatialAuthorityContextSha256")),
		str(film.get("CathedralSpatialKernelContextSha256")),
		str(film.get("CathedralSpatialKernelBvhContextSha256")),
		str(film.get("CathedralContactAuthorityToken")),
		str(film.get("SealedProbeViewGeneration"))
	]

func _observer_transform() -> String:
	return str(chamber.get_node("TransportChamberPlayer").global_transform)

func _send_key(code: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	event.echo = false
	Input.parse_input_event(event)

func _settle(frames: int) -> void:
	for _i in range(frames):
		await process_frame

func _assert(condition: bool, message: String) -> void:
	if not condition:
		_fail(message)

func _fail(message: String) -> void:
	push_error("PUBLIC CATHEDRAL SNAPSHOT PATH FAIL: " + message)
	quit(1)
