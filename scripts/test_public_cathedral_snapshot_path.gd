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
	var disabled_generation := int(film.get("CathedralSnapshotGeneration"))
	film_controller.call("SetInputEnabled", false)
	_send_key(KEY_Q)
	_send_key(KEY_G)
	await _settle(2)
	_assert(film_controller.call("GetModeName") == "OFF", "disabled interactive input accepted Q or G")
	_assert(int(film.get("CathedralSnapshotGeneration")) == disabled_generation,
		"disabled interactive Q created a Formal request")
	_assert(str(film_controller.call("GetLastInputDisposition")) == "interactive_input_disabled",
		"disabled interactive input did not report its authority boundary")
	_assert(film.has_method("RequestCathedralProbeSnapshot"),
		"programmatic Formal acquisition API unavailable while interactive input is disabled")
	film_controller.call("SetInputEnabled", true)

	_send_key(KEY_E)
	await _settle(8)
	_assert(field_dial.call("GetExperimentName") == "Gallery", "E is reserved and inert")
	_assert(not bool(hermetic_display.visible), "E does not reveal Hermetic apparatus")
	_assert(not bool(film.get("ProbeViewAvailable")), "E does not create sealed authority")

	field_dial.call("SetDisplayPresetForTesting", "Hermetic")
	await _settle(8)
	_assert(field_dial.call("GetExperimentName") == "Hermetic", "programmatic Hermetic selection remains available")
	_assert(bool(hermetic_display.visible), "programmatic selection establishes Hermetic apparatus")
	await _verify_live_profile_ordering()

	var generation_before_request := int(film.get("CathedralSnapshotGeneration"))
	var profile_before_request := str(film.get("ProbeProfileStableId"))
	# Keep Q/P/Q/G ordered ahead of the very fast headless renderer so the
	# repeated Q and G are genuinely dispatched while Formal owns acquisition.
	_queue_key(KEY_Q)
	_queue_key(KEY_P)
	_queue_key(KEY_Q)
	_queue_key(KEY_G)
	await process_frame
	var first_request_generation := int(film.get("CathedralSnapshotGeneration"))
	var requested_profile := str(film.get("CathedralRequestedProbeProfileStableId"))
	_assert(film_controller.call("GetModeName") == "SNAPSHOT", "queued Q enters SNAPSHOT and queued G cannot abandon it")
	_assert(first_request_generation == generation_before_request + 1,
		"repeated queued Q superseded request")
	_assert(requested_profile == profile_before_request,
		"P after request relabeled active acquisition")
	await _wait_for_complete()
	_assert(bool(film.get("ProbeViewAvailable")), "formal Snapshot seals authority")
	_assert(int(film.get("SealedProbeViewGeneration")) == 1, "first public Snapshot generation")
	_assert(str(film.get("CathedralSealedProbeProfileStableId")) == requested_profile,
		"sealed profile does not match request-bound profile")
	_send_key(KEY_P)
	await _settle(2)
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

	var replacement := InputEventKey.new()
	replacement.keycode = KEY_Q
	replacement.physical_keycode = KEY_Q
	replacement.pressed = true
	replacement.echo = false
	replacement.ctrl_pressed = true
	Input.parse_input_event(replacement)
	await _settle(2)
	_assert(not bool(film.get("ProbeViewAvailable")), "Ctrl+Q did not withdraw completed seal")
	_assert(int(film.get("CathedralSnapshotGeneration")) == generation + 1, "Ctrl+Q did not create one replacement generation")
	await _wait_for_complete()
	generation = int(film.get("SealedProbeViewGeneration"))
	measurement_identity = _measurement_identity()
	terminal_heartbeat_count = int(film.get("RuntimeHealthHeartbeatCount"))

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
	_send_key(KEY_Q)
	await _settle(2)
	_assert(film_controller.call("GetModeName") == "SNAPSHOT", "Q enters second Snapshot from OFF")
	await _wait_for_complete()
	_assert(bool(film.get("ProbeViewAvailable")), "second Snapshot seals authority")
	var second_generation := int(film.get("SealedProbeViewGeneration"))
	_assert(second_generation > generation, "second public Snapshot generation is newer")
	print("PUBLIC CATHEDRAL SNAPSHOT PATH PASS generation=%d" % second_generation)
	quit(0)


func _verify_live_profile_ordering() -> void:
	film.call("SetProbeProfileByIndex", 0)
	_send_key(KEY_P)
	await _settle(2)
	_send_key(KEY_G)
	await _settle(2)
	var p_then_g := _live_effective_signature()
	_send_key(KEY_G)
	await _settle(2)
	film.call("SetProbeProfileByIndex", 0)
	_send_key(KEY_G)
	await _settle(2)
	_send_key(KEY_P)
	await _settle(2)
	var g_then_p := _live_effective_signature()
	_assert(p_then_g == g_then_p, "P->G and G->P LIVE configurations differ")
	_send_key(KEY_G)
	await _settle(2)
	film.call("SetProbeProfileByIndex", 0)


func _live_effective_signature() -> String:
	return JSON.stringify({
		"profile": str(film.get("ProbeProfileStableId")),
		"scale": float(film.get("FilmResolutionScale")),
		"pixel_stride": int(film.get("PixelStride")),
		"rows_per_frame": int(film.get("RowsPerFrame")),
		"rows_cap": int(film.get("MaxRowsPerFrameCap")),
		"rows_per_step": int(film.get("UpdateEveryFrameMaxRowsPerStep")),
		"budget_ms": float(film.get("UpdateEveryFrameBudgetMs")),
		"target_ms": int(film.get("TargetMsPerFrame")),
		"render_max_ms": int(film.get("RenderStepMaxMs")),
		"render_pixel_cap": int(film.get("RenderStepMaxPixelsPerFrame")),
		"update_every_frame": bool(film.get("UpdateEveryFrame")),
		"max_distance": float(film.get("MaxDistance")),
		"auto_range": bool(film.get("AutoRangeDepth")),
		"auto_range_min": float(film.get("AutoRangeMin")),
		"auto_range_max": float(film.get("AutoRangeMax")),
		"steps": int(film.get("SharedRbrStepsPerRay")),
		"step_length": float(film.get("SharedRbrStepLength")),
		"min_step": float(film.get("SharedRbrMinStepLength")),
		"max_step": float(film.get("SharedRbrMaxStepLength")),
		"collision_stride": int(film.get("SharedRbrCollisionEveryNSteps")),
		"min_collision_stride": int(film.get("SharedRbrMinCollisionEveryNSteps")),
		"pass1_probe_stride": int(film.get("Pass1ProbeEveryNSegments")),
		"pass1_probe_delta": float(film.get("Pass1ProbeMinTravelDelta")),
		"pass1_hit_test": bool(film.get("Pass1DoHitTest")),
		"adaptive_substeps": bool(film.get("UseAdaptiveSubsteps")),
		"band_hit_skip": bool(film.get("UseBandHitSkip")),
		"broadphase_mode": int(film.get("BroadphaseControlMode")),
		"broadphase_policy": int(film.get("BroadphasePolicy")),
		"broadphase_quick_ray": bool(film.get("UseBroadphaseQuickRay")),
		"broadphase_overlap": bool(film.get("UseBroadphaseOverlap")),
		"effective_broadphase_mode": int(film.get("EffectiveBroadphaseMode")),
		"effective_broadphase_policy": int(film.get("EffectiveBroadphasePolicy")),
		"effective_broadphase_quick_ray": bool(film.get("EffectiveBroadphaseQuickRay")),
		"effective_broadphase_overlap": bool(film.get("EffectiveBroadphaseOverlap")),
		"stride": bool(film.get("UsePass2CollisionStride")),
		"stride_near": int(film.get("Pass2CollisionStrideNear")),
		"stride_far": int(film.get("Pass2CollisionStrideFar")),
		"stride_far_start": float(film.get("Pass2CollisionStrideFarStartT")),
		"stride_min_segment": float(film.get("MinSegLenForStrideSkip")),
		"envelope_radius": float(film.get("Pass2GeomEnvelopeRadiusScale")),
		"envelope_expand": float(film.get("Pass2GeomEnvelopeAabbExpand")),
		"tlas_pruning": bool(film.get("UseGeometryTLASPruning")),
		"adaptive_envelope": bool(film.get("AdaptiveTelemetryEnvelopeScalingEnabled")),
		"adaptive_envelope_mode": str(film.get("AdaptiveEnvelopeControllerMode")),
		"adaptive_envelope_prior": str(film.get("AdaptiveEnvelopePriorSource")),
		"soft_gate_quick_miss": bool(film.get("Pass2SoftGateEnableQuickRayMiss")),
		"soft_gate_scoring": bool(film.get("Pass2SoftGateScoringEnabled")),
		"pass2_hit_back_faces": bool(film.get("Pass2HitBackFaces")),
		"pass2_hit_from_inside": bool(film.get("Pass2HitFromInside")),
		"render_segment_cap": int(film.get("RenderStepMaxSegmentsPerFrame")),
		"recorder_enabled": bool(film.get("RecordFormalTransportLedger")),
	})

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


func _queue_key(code: Key) -> void:
	var press := InputEventKey.new()
	press.keycode = code
	press.physical_keycode = code
	press.pressed = true
	press.echo = false
	Input.parse_input_event(press)
	var release := InputEventKey.new()
	release.keycode = code
	release.physical_keycode = code
	release.pressed = false
	release.echo = false
	Input.parse_input_event(release)


func _settle(frames: int) -> void:
	for _i in range(frames):
		await process_frame

func _assert(condition: bool, message: String) -> void:
	if not condition:
		_fail(message)

func _fail(message: String) -> void:
	push_error("PUBLIC CATHEDRAL SNAPSHOT PATH FAIL: " + message)
	quit(1)
