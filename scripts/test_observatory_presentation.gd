extends SceneTree

const FIELD_PRESENTATION_LAYER_MASK := 1 << 19

var scene_root: Node
var chamber: Node
var film: Node
var film_controller: Node
var player: CharacterBody3D
var player_camera: Camera3D


func _initialize() -> void:
	scene_root = load("res://ObservatoryWorkbench.tscn").instantiate()
	get_root().add_child(scene_root)
	await _settle(30)
	chamber = scene_root.get_node("PlayableWorld/TransportChamberWorld")
	film = chamber.get_node("GrinFilmCamera")
	film_controller = chamber.get_node("FilmController")
	player = chamber.get_node("TransportChamberPlayer")
	player_camera = player.get_camera()

	var normal_cull_mask := player_camera.cull_mask
	var observer_transform := player.global_transform
	var camera_transform := player_camera.global_transform
	var active_state_before := _active_experiment_state()
	var film_overlay: Node = chamber.get_node("CanvasLayer/HUDRoot/FilmModule/FilmPlate/FilmOverlay2D")
	var telemetry: Control = chamber.get_node("CanvasLayer/HUDRoot/TelemetryModule")
	_assert(not _field_visuals_camera_visible(), "F starts with field overlays camera-hidden")
	_send_key(KEY_F)
	await _settle(12)
	_assert(_field_visuals_camera_visible(), "normal F ON makes field overlays camera-visible")
	_send_key(KEY_F)
	await _settle(4)
	_assert(not _field_visuals_camera_visible(), "normal F OFF makes field overlays camera-hidden")
	_send_key(KEY_F)
	await _settle(4)
	_assert(_field_visuals_camera_visible(), "normal repeated F ON restores camera-visible overlays")

	chamber.call("SetOpticalOnlyPresentation", true)
	await _settle(2)
	_assert(_field_visuals_camera_visible(), "optical-only preserves F ON camera visibility")
	_send_key(KEY_F)
	await _settle(2)
	_assert(not _field_visuals_camera_visible(), "optical-only F OFF makes field overlays camera-hidden")
	_send_key(KEY_F)
	await _settle(4)
	_assert(_field_visuals_camera_visible(), "optical-only F ON makes field overlays camera-visible")
	chamber.call("SetOpticalOnlyPresentation", false)
	await _settle(2)
	_assert(_field_visuals_camera_visible(), "normal restore preserves F ON camera visibility")

	_send_key(KEY_TAB)
	await _settle(2)
	_assert(telemetry.visible, "advanced telemetry opens before demo mode")
	_send_key(KEY_F)
	await _settle(4)
	_assert(_field_visuals_camera_visible(), "F ON before demo makes field overlays camera-visible")
	chamber.call("SetDemoPresentationMode", true)
	_assert(player_camera.cull_mask == FIELD_PRESENTATION_LAYER_MASK, "demo mode selects optical-only observer")
	_assert(_field_visuals_camera_visible(), "demo mode preserves F ON camera visibility")
	_send_key(KEY_F)
	await _settle(2)
	_assert(not _field_visuals_camera_visible(), "demo F OFF makes field overlays camera-hidden")
	_send_key(KEY_F)
	await _settle(4)
	_assert(_field_visuals_camera_visible(), "demo F ON makes field overlays camera-visible")
	_assert(film_overlay.get("SuppressDebugOverlayBusPresentation") == true, "demo mode suppresses development annotations")
	_assert(not telemetry.visible, "demo mode hides advanced telemetry")
	chamber.call("SetDemoPresentationMode", false)
	_assert(player_camera.cull_mask == normal_cull_mask, "demo mode restores normal observer")
	_assert(_field_visuals_camera_visible(), "demo restore preserves F ON camera visibility")
	_assert(film_overlay.get("SuppressDebugOverlayBusPresentation") == false, "demo mode restores diagnostic annotations")
	_assert(telemetry.visible, "demo mode restores prior advanced telemetry state")
	_send_key(KEY_TAB)
	await _settle(2)
	_assert(_active_experiment_state() == active_state_before, "demo mode preserves active experiment state")

	_send_key(KEY_F)
	await _settle(4)
	_assert(_all_field_sources_visible(), "F enables FieldSource3D overlays")
	_assert(_all_field_visuals_use_presentation_layer(), "field overlays use presentation layer")
	if OS.get_cmdline_user_args().has("--optical-first"):
		chamber.call("SetOpticalOnlyPresentation", true)

	_send_key(KEY_G)
	await _wait_for_complete()
	var visible_identity := _formal_identity()
	var visible_result := _formal_result()
	print("OBSERVATORY PRESENTATION FIRST_RESULT %s" % visible_result)
	var generation := int(film.get("SealedProbeViewGeneration"))
	_assert(_formal_validation_green(), "visible formal authority validation")

	chamber.call("SetOpticalOnlyPresentation", true)
	await _settle(4)
	_assert(bool(chamber.call("IsOpticalOnlyPresentation")), "optical-only state")
	_assert(player_camera.cull_mask == FIELD_PRESENTATION_LAYER_MASK, "optical-only camera mask")
	_assert(_formal_result() == visible_result, "presentation toggle preserves sealed result")
	_assert(int(film.get("SealedProbeViewGeneration")) == generation, "presentation toggle preserves generation")
	_assert(_active_experiment_state() == active_state_before, "presentation toggle preserves active experiment state")
	_assert(_all_field_sources_visible(), "F overlays remain enabled in optical-only mode")

	var q_replay_invocations := int(film.get("CathedralContactReplayInvocationCount"))
	var q_lifecycle_state := str(film.get("CathedralSnapshotLifecycleStateName"))
	_send_key(KEY_Q)
	await _settle(2)
	_assert(int(film.get("SealedProbeViewGeneration")) == generation, "Q remains mapping-only")
	_assert(_formal_result() == visible_result, "Q preserves formal result")
	_assert(int(film.get("CathedralContactReplayInvocationCount")) == q_replay_invocations, "Q does not replay contacts")
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == q_lifecycle_state, "Q does not restart acquisition")

	_send_key(KEY_G)
	await _settle(4)
	_assert(film_controller.call("GetModeName") == "LIVE", "G enters LIVE")
	_send_key(KEY_G)
	await _settle(12)
	_assert(film_controller.call("GetModeName") == "OFF", "G returns OFF")
	var off_heartbeat_count := int(film.get("RuntimeHealthHeartbeatCount"))
	await _settle(12)
	_assert(int(film.get("RuntimeHealthHeartbeatCount")) == off_heartbeat_count, "post-G OFF is quiescent")

	player.global_transform = observer_transform
	player.velocity = Vector3.ZERO
	player_camera.global_transform = camera_transform
	await _settle(2)
	_send_key(KEY_G)
	await _wait_for_complete()
	_assert(_formal_validation_green(), "optical-only formal authority validation")
	_assert(_formal_identity() == visible_identity, "visible and optical-only formal identity match")

	chamber.call("SetOpticalOnlyPresentation", false)
	await _settle(4)
	_assert(player_camera.cull_mask == normal_cull_mask, "normal camera mask restored exactly")
	_assert(_active_experiment_state() == active_state_before, "normal restore preserves active experiment state")
	print("OBSERVATORY PRESENTATION PASS identity=%s" % visible_identity)
	quit(0)


func _formal_identity() -> String:
	return "%s|%s|%s|%s|%s" % [
		str(film.get("CathedralSpatialKernelGeometrySnapshotSha256")),
		str(film.get("CathedralSpatialAuthorityContextSha256")),
		str(film.get("CathedralSpatialKernelContextSha256")),
		str(film.get("CathedralSpatialKernelBvhContextSha256")),
		str(film.get("CathedralContactAuthorityToken")),
	]


func _formal_result() -> String:
	return "%s|%s|%s" % [
		_formal_identity(),
		str(film.get("CathedralProbeQueryDatasetSha256")),
		str(film.get("CathedralSpatialKernelContactCountSha256")),
	]


func _formal_validation_green() -> bool:
	return (
		str(film.get("CathedralContactAuthorityToken")) == "XPrimeRaySpatialKernel/BVH-v0"
		and film.get("CathedralSpatialKernelDualValidationAvailable") == true
		and film.get("CathedralSpatialAuthorityPromotionReady") == true
		and int(film.get("CathedralSpatialKernelTotalAuthorityMismatchPixelCount")) == 0
		and int(film.get("CathedralSpatialKernelMismatchQueryCount")) == 0
	)


func _active_experiment_state() -> String:
	var rows: Array[String] = []
	_collect_active_state(scene_root, rows)
	rows.sort()
	return "\n".join(rows)


func _collect_active_state(node: Node, rows: Array[String]) -> void:
	if node is CollisionObject3D:
		rows.append("collision|%s|%s|%s|%s" % [
			node.get_path(), node.collision_layer, node.collision_mask, node.process_mode])
	elif node is CollisionShape3D:
		rows.append("shape|%s|%s|%s" % [node.get_path(), node.disabled, node.process_mode])
	elif node.is_in_group("field_sources"):
		rows.append("field|%s|%s|%s|%s|%s" % [
			node.get_path(), node.get("Enabled"), node.get("Amp"), node.get("RInner"), node.get("ROuter")])
	elif node is VisualInstance3D and not node.name.begins_with("_FieldSourceAcademicViz"):
		rows.append("visual|%s|%s|%s" % [node.get_path(), node.visible, node.process_mode])
	for child in node.get_children():
		_collect_active_state(child, rows)


func _all_field_sources_visible() -> bool:
	for source in scene_root.get_tree().get_nodes_in_group("field_sources"):
		if bool(source.get("Enabled")) and bool(source.get("DebugVizEnabled")) and not bool(source.get("DebugVizInGame")):
			return false
	return true


func _all_field_visuals_use_presentation_layer() -> bool:
	var found := false
	for source in scene_root.get_tree().get_nodes_in_group("field_sources"):
		var visual := source.get_node_or_null("_FieldSourceAcademicViz") as MeshInstance3D
		if visual == null or not visual.visible:
			continue
		found = true
		if visual.layers != FIELD_PRESENTATION_LAYER_MASK:
			return false
	return found


func _field_visuals_camera_visible() -> bool:
	var active_camera := scene_root.get_viewport().get_camera_3d()
	if active_camera != player_camera or not player_camera.current:
		return false
	var found_eligible := false
	for source in scene_root.get_tree().get_nodes_in_group("field_sources"):
		if not bool(source.get("Enabled")) or not bool(source.get("DebugVizEnabled")):
			continue
		found_eligible = true
		var visual := source.get_node_or_null("_FieldSourceAcademicViz") as MeshInstance3D
		if not bool(source.get("DebugVizInGame")):
			if visual != null and visual.is_visible_in_tree():
				return true
			continue
		if visual == null or visual.mesh == null or visual.mesh.get_surface_count() == 0 or not visual.is_visible_in_tree():
			return false
		if (player_camera.cull_mask & visual.layers) == 0:
			return false
	return found_eligible and _all_field_sources_visible()


func _wait_for_complete() -> void:
	for _i in range(900):
		await process_frame
		if bool(film.get("ProbeViewAvailable")):
			return
	_fail("formal Snapshot timeout")


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
	push_error("OBSERVATORY PRESENTATION FAIL: " + message)
	quit(1)
