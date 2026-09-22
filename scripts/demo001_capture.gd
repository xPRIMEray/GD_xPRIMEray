extends SceneTree

const DEMO_NAME := "xPRIMEray Demo 001"
const DEMO_VERSION := "1.0"
const CHOREOGRAPHY_VERSION := "demo001.instrument-film.v1"
const SCENE_PATH := "res://ObservatoryWorkbench.tscn"
const FIELD_PRESENTATION_LAYER_MASK := 1 << 19

const WORLD_HOLD_FRAMES := 90
const FIELD_HOLD_FRAMES := 120
const OPTICAL_HOLD_FRAMES := 120
const LIVE_HOLD_FRAMES := 360
const EVIDENCE_HOLD_FRAMES := 120
const RETURN_HOLD_FRAMES := 120
const CONDITION_TIMEOUT_FRAMES := 1200

var scene_root: Node
var chamber: Node
var film: Node
var film_controller: Node
var field_dial: Node
var player: CharacterBody3D
var player_camera: Camera3D

var output_dir := ""
var frames_dir := ""
var movie_filename := "demo001.avi"
var fixed_fps := 30
var capture_width := 1280
var capture_height := 720
var capture_representative_frames := true
var choreography_frame := 0
var stages: Array[Dictionary] = []
var representative_frames: Array[Dictionary] = []
var production_transitions: Array[String] = []
var live_row_cursors: Dictionary = {}
var first_formal_result: Dictionary = {}
var sealed_result: Dictionary = {}
var first_formal_validation_green := false
var sealed_formal_validation_green := false
var q_mapping_verified := false
var post_g_heartbeat_stable := false


func _initialize() -> void:
	output_dir = OS.get_environment("DEMO001_OUTPUT_DIR")
	if output_dir.is_empty():
		output_dir = ProjectSettings.globalize_path("res://output/demo001")
	frames_dir = output_dir.path_join("frames")
	movie_filename = OS.get_environment("DEMO001_MOVIE_FILENAME")
	if movie_filename.is_empty():
		movie_filename = "demo001.avi"
	fixed_fps = maxi(1, int(OS.get_environment("DEMO001_FIXED_FPS")))
	if OS.get_environment("DEMO001_FIXED_FPS").is_empty():
		fixed_fps = 30
	if not OS.get_environment("DEMO001_CAPTURE_WIDTH").is_empty():
		capture_width = maxi(1, int(OS.get_environment("DEMO001_CAPTURE_WIDTH")))
	if not OS.get_environment("DEMO001_CAPTURE_HEIGHT").is_empty():
		capture_height = maxi(1, int(OS.get_environment("DEMO001_CAPTURE_HEIGHT")))
	capture_representative_frames = OS.get_environment("DEMO001_SKIP_SCREENSHOTS") != "1"
	DirAccess.make_dir_recursive_absolute(frames_dir)

	scene_root = load(SCENE_PATH).instantiate()
	get_root().add_child(scene_root)
	await _hold_frames(30)
	chamber = scene_root.get_node("PlayableWorld/TransportChamberWorld")
	film = chamber.get_node("GrinFilmCamera")
	film_controller = chamber.get_node("FilmController")
	field_dial = chamber.get_node("FieldDialController")
	player = chamber.get_node("TransportChamberPlayer")
	player_camera = player.get_camera()
	player.set_physics_process(false)
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	await _hold_frames(5)

	await _assert_public_retired_keys()
	await _stage_world()
	await _stage_field()
	await _stage_optical_only()
	await _stage_live_discovery()
	await _stage_sealed_observation()
	await _stage_probe_views()
	await _stage_return()
	_write_manifest()
	print("DEMO001 PASS frames=%d duration=%.3f output=%s" % [
		choreography_frame, float(choreography_frame) / float(fixed_fps), output_dir])
	quit(0)


func _stage_world() -> void:
	_begin_stage("establish_world")
	_assert(not bool(chamber.call("IsOpticalOnlyPresentation")), "world begins in normal presentation")
	_assert(not _field_structure_enabled(), "world begins with Field Structure OFF")
	await _hold_frames(WORLD_HOLD_FRAMES)
	await _capture_representative("01_world.png", "conventional_world")
	_end_stage()


func _stage_field() -> void:
	_begin_stage("reveal_field")
	_send_key(KEY_F)
	production_transitions.append("F:OFF->ON")
	await _wait_until(Callable(self, "_field_visuals_camera_visible"), "Field Structure camera-visible")
	await _hold_frames(FIELD_HOLD_FRAMES)
	await _capture_representative("02_field.png", "field_structure_on")
	_end_stage()


func _stage_optical_only() -> void:
	_begin_stage("optical_isolation")
	_send_key(KEY_O)
	production_transitions.append("O:NORMAL->OPTICAL_ONLY")
	await _wait_until(Callable(self, "_optical_only_ready"), "Optical-only active")
	chamber.call("SetDemoPresentationMode", true)
	production_transitions.append("Demo:OFF->ON")
	await _hold_frames(OPTICAL_HOLD_FRAMES)
	_assert(_field_visuals_camera_visible(), "Field Structure remains camera-visible in Optical-only")
	await _capture_representative("03_optical_only.png", "field_plus_optical_only")
	_end_stage()


func _stage_live_discovery() -> void:
	_begin_stage("live_optical_discovery")
	_send_key(KEY_G)
	production_transitions.append("G:OFF->SNAPSHOT")
	await _wait_for_formal_complete()
	first_formal_result = _formal_result()
	first_formal_validation_green = _formal_validation_green()
	_assert(first_formal_validation_green, "pre-LIVE formal validation")

	_send_key(KEY_G)
	production_transitions.append("G:SNAPSHOT->LIVE")
	await _wait_for_mode("LIVE")
	var heartbeat_start := int(film.get("RuntimeHealthHeartbeatCount"))
	for index in range(LIVE_HOLD_FRAMES):
		await _next_frame()
		live_row_cursors[str(int(film.get("FilmRowCursor")))] = true
		if index == int(LIVE_HOLD_FRAMES / 2):
			await _capture_representative("04_live.png", "genuine_live_acquisition")
	_assert(live_row_cursors.size() > 1 or int(film.get("RuntimeHealthHeartbeatCount")) > heartbeat_start,
		"LIVE acquisition did not show production progress")
	_end_stage()


func _stage_sealed_observation() -> void:
	_begin_stage("sealed_observation")
	_send_key(KEY_G)
	production_transitions.append("G:LIVE->OFF")
	await _wait_for_mode("OFF")
	_send_key(KEY_G)
	production_transitions.append("G:OFF->SNAPSHOT")
	await _wait_for_formal_complete()
	sealed_formal_validation_green = _formal_validation_green()
	_assert(sealed_formal_validation_green, "sealed formal validation")
	sealed_result = _formal_result()
	await _hold_frames(EVIDENCE_HOLD_FRAMES)
	await _capture_representative("05_outcome.png", "sealed_outcome")
	_end_stage()


func _stage_probe_views() -> void:
	_begin_stage("sealed_probe_view_triad")
	var generation := int(film.get("SealedProbeViewGeneration"))
	var result_before := _formal_result()
	var replay_before := int(film.get("CathedralContactReplayInvocationCount"))
	var lifecycle_before := str(film.get("CathedralSnapshotLifecycleStateName"))

	_send_key(KEY_Q)
	production_transitions.append("Q:OUTCOME->CONTACT_EVENTS")
	await _wait_for_probe_view(1)
	await _hold_frames(EVIDENCE_HOLD_FRAMES)
	await _capture_representative("06_contacts.png", "sealed_contact_events")

	_send_key(KEY_Q)
	production_transitions.append("Q:CONTACT_EVENTS->TRANSPORT_EFFORT")
	await _wait_for_probe_view(2)
	await _hold_frames(EVIDENCE_HOLD_FRAMES)
	await _capture_representative("07_effort.png", "sealed_transport_effort")

	q_mapping_verified = (
		int(film.get("SealedProbeViewGeneration")) == generation
		and _formal_result() == result_before
		and int(film.get("CathedralContactReplayInvocationCount")) == replay_before
		and str(film.get("CathedralSnapshotLifecycleStateName")) == lifecycle_before
	)
	_assert(q_mapping_verified, "Q must remain mapping-only")
	_end_stage()


func _stage_return() -> void:
	_begin_stage("return_to_conventional_world")
	_send_key(KEY_G)
	production_transitions.append("G:SNAPSHOT->LIVE")
	await _wait_for_mode("LIVE")
	_send_key(KEY_G)
	production_transitions.append("G:LIVE->OFF")
	await _wait_for_mode("OFF")
	chamber.call("SetDemoPresentationMode", false)
	production_transitions.append("Demo:ON->OFF")
	_send_key(KEY_O)
	production_transitions.append("O:OPTICAL_ONLY->NORMAL")
	await _wait_until(Callable(self, "_normal_presentation_ready"), "normal presentation restored")
	_send_key(KEY_F)
	production_transitions.append("F:ON->OFF")
	await _wait_until(Callable(self, "_field_structure_disabled"), "Field Structure OFF")
	var heartbeat := int(film.get("RuntimeHealthHeartbeatCount"))
	await _hold_frames(RETURN_HOLD_FRAMES)
	post_g_heartbeat_stable = int(film.get("RuntimeHealthHeartbeatCount")) == heartbeat
	_assert(post_g_heartbeat_stable, "post-G OFF heartbeat remains quiescent")
	await _capture_representative("08_return.png", "restored_conventional_world")
	_end_stage()


func _assert_public_retired_keys() -> void:
	var experiment := str(field_dial.call("GetExperimentName"))
	var generation := int(film.get("SealedProbeViewGeneration"))
	_send_key(KEY_E)
	_send_key(KEY_H)
	await _hold_frames(3)
	_assert(str(field_dial.call("GetExperimentName")) == experiment, "E is reserved/inert")
	_assert(int(film.get("SealedProbeViewGeneration")) == generation, "E/H preserve generation")
	production_transitions.append("E:INERT")
	production_transitions.append("H:INERT")


func _wait_for_formal_complete() -> void:
	for _i in range(CONDITION_TIMEOUT_FRAMES):
		await _next_frame()
		if (
			str(film.get("CathedralSnapshotLifecycleStateName")) == "complete"
			and bool(film.get("ProbeViewAvailable"))
			and int(film.get("CathedralSnapshotProcessedPixelCount")) == int(film.get("CathedralSnapshotTotalPixelCount"))
		):
			return
	_fail("formal SNAPSHOT did not reach Complete")


func _wait_for_mode(expected: String) -> void:
	for _i in range(CONDITION_TIMEOUT_FRAMES):
		await _next_frame()
		if str(film_controller.call("GetModeName")) == expected:
			return
	_fail("film mode did not reach " + expected)


func _wait_for_probe_view(expected: int) -> void:
	for _i in range(60):
		await _next_frame()
		if int(film.get("CurrentProbeView")) == expected:
			return
	_fail("Probe View did not reach %d" % expected)


func _wait_until(predicate: Callable, description: String) -> void:
	for _i in range(120):
		await _next_frame()
		if bool(predicate.call()):
			return
	_fail(description + " timeout")


func _hold_frames(count: int) -> void:
	for _i in range(count):
		await _next_frame()


func _next_frame() -> void:
	await process_frame
	choreography_frame += 1


func _begin_stage(stage_id: String) -> void:
	stages.append({"id": stage_id, "start_frame": choreography_frame})


func _end_stage() -> void:
	var index := stages.size() - 1
	stages[index]["end_frame"] = choreography_frame
	stages[index]["duration_frames"] = int(stages[index]["end_frame"]) - int(stages[index]["start_frame"])


func _capture_representative(file_name: String, stage_id: String) -> void:
	await _next_frame()
	if not capture_representative_frames:
		return
	var image := scene_root.get_viewport().get_texture().get_image()
	_assert(image != null and image.get_width() > 0 and image.get_height() > 0, file_name + " has no viewport image")
	_assert(image.get_width() == capture_width and image.get_height() == capture_height,
		file_name + " does not match requested capture resolution")
	var path := frames_dir.path_join(file_name)
	_assert(image.save_png(path) == OK, "failed to save " + file_name)
	representative_frames.append({
		"file": "frames/" + file_name,
		"stage": stage_id,
		"frame": choreography_frame,
		"width": image.get_width(),
		"height": image.get_height(),
		"sha256": FileAccess.get_sha256(path),
	})


func _field_structure_enabled() -> bool:
	for source in scene_root.get_tree().get_nodes_in_group("field_sources"):
		if bool(source.get("Enabled")) and bool(source.get("DebugVizEnabled")) and not bool(source.get("DebugVizInGame")):
			return false
	return true


func _field_structure_disabled() -> bool:
	for source in scene_root.get_tree().get_nodes_in_group("field_sources"):
		if bool(source.get("DebugVizEnabled")) and bool(source.get("DebugVizInGame")):
			return false
	return true


func _field_visuals_camera_visible() -> bool:
	if scene_root.get_viewport().get_camera_3d() != player_camera or not player_camera.current:
		return false
	var found := false
	for source in scene_root.get_tree().get_nodes_in_group("field_sources"):
		if not bool(source.get("Enabled")) or not bool(source.get("DebugVizEnabled")):
			continue
		found = true
		var visual := source.get_node_or_null("_FieldSourceAcademicViz") as MeshInstance3D
		if visual == null or visual.mesh == null or visual.mesh.get_surface_count() == 0:
			return false
		if not visual.is_visible_in_tree() or (player_camera.cull_mask & visual.layers) == 0:
			return false
	return found and _field_structure_enabled()


func _optical_only_ready() -> bool:
	return bool(chamber.call("IsOpticalOnlyPresentation")) and player_camera.cull_mask == FIELD_PRESENTATION_LAYER_MASK


func _normal_presentation_ready() -> bool:
	return not bool(chamber.call("IsOpticalOnlyPresentation")) and player_camera.cull_mask != FIELD_PRESENTATION_LAYER_MASK


func _formal_validation_green() -> bool:
	return (
		str(film.get("CathedralContactAuthorityToken")) == "XPrimeRaySpatialKernel/BVH-v0"
		and bool(film.get("CathedralSpatialKernelDualValidationAvailable"))
		and bool(film.get("CathedralSpatialAuthorityPromotionReady"))
		and int(film.get("CathedralSpatialKernelTotalAuthorityMismatchPixelCount")) == 0
		and int(film.get("CathedralSpatialKernelMismatchQueryCount")) == 0
	)


func _formal_result() -> Dictionary:
	return {
		"generation": int(film.get("SealedProbeViewGeneration")),
		"authority_token": str(film.get("CathedralContactAuthorityToken")),
		"geometry_sha256": str(film.get("CathedralSpatialKernelGeometrySnapshotSha256")),
		"spatial_context_sha256": str(film.get("CathedralSpatialAuthorityContextSha256")),
		"bvh_context_sha256": str(film.get("CathedralSpatialKernelBvhContextSha256")),
		"query_dataset_sha256": str(film.get("CathedralProbeQueryDatasetSha256")),
		"contact_count_sha256": str(film.get("CathedralSpatialKernelContactCountSha256")),
		"contact_replay_invocations": int(film.get("CathedralContactReplayInvocationCount")),
		"authority_mismatch_pixels": int(film.get("CathedralSpatialKernelTotalAuthorityMismatchPixelCount")),
		"authority_mismatch_queries": int(film.get("CathedralSpatialKernelMismatchQueryCount")),
	}


func _write_manifest() -> void:
	var manifest := {
		"provenance_class": "DEMONSTRATION",
		"not_measurement_authority": true,
		"demo": {"name": DEMO_NAME, "version": DEMO_VERSION, "choreography_version": CHOREOGRAPHY_VERSION},
		"created_utc": Time.get_datetime_string_from_system(true),
		"git_commit": OS.get_environment("DEMO001_GIT_COMMIT"),
		"godot_version": Engine.get_version_info(),
		"capture": {
			"mode": "Godot MovieWriter --write-movie",
			"fixed_fps": fixed_fps,
			"resolution": {"width": capture_width, "height": capture_height},
			"total_frames": choreography_frame,
			"total_duration_seconds": float(choreography_frame) / float(fixed_fps),
			"choreography_frames": choreography_frame,
			"choreography_duration_seconds": float(choreography_frame) / float(fixed_fps),
			"movie_filename": movie_filename,
		},
		"scene": SCENE_PATH,
		"observer_pose_locked_by_harness": true,
		"production_transitions": production_transitions,
		"stages": stages,
		"representative_frames": representative_frames,
		"live_progress": {
			"production_mode_observed": "LIVE",
			"distinct_row_cursors": live_row_cursors.keys(),
			"fixed_delta_seconds": 1.0 / float(fixed_fps),
		},
		"formal_acquisition_before_live": first_formal_result,
		"sealed_observation_for_q": sealed_result,
		"qualification": {
			"formal_validation_green": first_formal_validation_green and sealed_formal_validation_green,
			"q_mapping_only_verified": q_mapping_verified,
			"post_g_off_heartbeat_stable": post_g_heartbeat_stable,
			"e_reserved_inert": true,
			"h_retired_inert": true,
		},
		"output": {
			"movie": movie_filename,
			"frames_directory": "frames/",
		},
	}
	var manifest_path := output_dir.path_join("demo001_manifest.json")
	var file := FileAccess.open(manifest_path, FileAccess.WRITE)
	_assert(file != null, "cannot write demonstration manifest")
	file.store_string(JSON.stringify(manifest, "  "))
	file = null


func _send_key(code: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	event.echo = false
	Input.parse_input_event(event)


func _assert(condition: bool, message: String) -> void:
	if not condition:
		_fail(message)


func _fail(message: String) -> void:
	push_error("DEMO001 FAIL: " + message)
	quit(1)
