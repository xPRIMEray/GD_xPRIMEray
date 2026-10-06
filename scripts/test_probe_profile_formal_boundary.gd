extends SceneTree

# Native-process regression harness for the Probe Profile/Formal boundary.
# One invocation runs one history in one process; the companion runner compares
# independent invocations so process-local working state cannot hide a leak.

const SCENE_PATH := "res://ObservatoryWorkbench.tscn"
const LIVE_FRAMES := 240
const FORMAL_TIMEOUT_FRAMES := 1200

var scenario := "A"
var profile_index := 0
var output_root := ""
var scene_root: Node
var chamber: Node
var film: Node
var film_controller: Node
var player: CharacterBody3D
var workbench_profile_option: OptionButton
var acquisitions: Array[Dictionary] = []


func _initialize() -> void:
	_parse_args()
	scene_root = load(SCENE_PATH).instantiate()
	get_root().add_child(scene_root)
	await _settle(30)
	chamber = scene_root.get_node("PlayableWorld/TransportChamberWorld")
	film = chamber.get_node("GrinFilmCamera")
	film_controller = chamber.get_node("FilmController")
	player = chamber.get_node("TransportChamberPlayer")
	workbench_profile_option = scene_root.get_node("UILayer/UIRoot/SideDock/Tabs/Observatory/Margin/Root/InstrumentControls/ProbeProfileOption")
	player.set_physics_process(false)
	await _settle(5)

	await _verify_profile_control_is_inert_and_synchronized()
	film.call("SetProbeProfileByIndex", profile_index)
	await _settle(3)
	_assert(int(film.get("ProbeProfileIndex")) == profile_index, "requested profile selected")
	_assert(workbench_profile_option.selected == profile_index, "Workbench reflects selected profile")

	match scenario:
		"A":
			await _run_formal("final")
		"B", "F":
			await _run_live_then_off()
			await _run_formal("final")
		"C":
			await _run_formal("first")
			film_controller.call("set_mode", 0)
			await _settle(5)
			await _run_formal("final")
		"D":
			await _run_live_then_off()
			await _run_formal("first")
			film_controller.call("set_mode", 2)
			await _settle(LIVE_FRAMES)
			film_controller.call("set_mode", 0)
			await _settle(5)
			await _run_formal("final")
		"E":
			await _enable_presentation_pair()
			await _run_formal("final")
		_:
			_fail("unknown scenario " + scenario)
	if acquisitions.size() > 1:
		_assert(_scientific_identity(acquisitions[0]) == _scientific_identity(acquisitions[-1]),
			"repeated Formal acquisition identity")

	await _verify_q_mapping_only()
	await _verify_profile_change_preserves_seal()
	await _verify_post_g_off_quiescence()
	_write_result()
	print("PROBE_PROFILE_BOUNDARY PASS scenario=%s profile=%s query=%s contact=%s" % [
		scenario, str(film.get("CathedralSealedProbeProfileStableId")),
		str(film.get("CathedralProbeQueryDatasetSha256")),
		str(film.get("CathedralSpatialKernelContactCountSha256"))])
	quit(0)


func _parse_args() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--scenario="):
			scenario = arg.trim_prefix("--scenario=").to_upper()
		elif arg.begins_with("--profile="):
			profile_index = int(arg.trim_prefix("--profile="))
		elif arg.begins_with("--output="):
			output_root = arg.trim_prefix("--output=")
	if output_root.is_empty():
		output_root = ProjectSettings.globalize_path("res://output/probe_profile_qualification")
	DirAccess.make_dir_recursive_absolute(output_root)


func _verify_profile_control_is_inert_and_synchronized() -> void:
	var initial_index := int(film.get("ProbeProfileIndex"))
	var generation := int(film.get("SealedProbeViewGeneration"))
	var replay := int(film.get("CathedralContactReplayInvocationCount"))
	var formal_resets := int(film.get("FormalWorkingStateResetCount"))
	_send_key(KEY_P)
	await _settle(3)
	var expected := (initial_index + 1) % int(film.get("ProbeProfileCount"))
	_assert(int(film.get("ProbeProfileIndex")) == expected, "P cycles authoritative profile")
	_assert(workbench_profile_option.selected == expected, "P synchronizes Workbench")
	_assert(int(film.get("SealedProbeViewGeneration")) == generation, "P does not create a generation")
	_assert(int(film.get("CathedralContactReplayInvocationCount")) == replay, "P does not replay")
	_assert(int(film.get("FormalWorkingStateResetCount")) == formal_resets, "P does not instantiate Formal working state")
	workbench_profile_option.select(initial_index)
	workbench_profile_option.item_selected.emit(initial_index)
	await _settle(3)
	_assert(int(film.get("ProbeProfileIndex")) == initial_index, "Workbench routes through authoritative profile setter")


func _run_live_then_off() -> void:
	film_controller.call("set_mode", 2)
	await _settle(3)
	_assert(str(film_controller.call("GetModeName")) == "LIVE", "LIVE entered through production controller")
	var rows: Dictionary = {}
	for _index in LIVE_FRAMES:
		await process_frame
		rows[str(int(film.get("FilmRowCursor")))] = true
	_assert(rows.size() > 1, "LIVE made meaningful acquisition progress")
	film_controller.call("set_mode", 0)
	await _settle(5)
	_assert(str(film_controller.call("GetModeName")) == "OFF", "LIVE returned OFF")


func _enable_presentation_pair() -> void:
	_send_key(KEY_F)
	_send_key(KEY_O)
	await _settle(6)
	_assert(bool(chamber.call("IsOpticalOnlyPresentation")), "O enables optical-only presentation")
	var camera: Camera3D = player.get_camera()
	var found_visible_field := false
	for source in get_nodes_in_group("field_sources"):
		if not bool(source.get("Enabled")) or not bool(source.get("DebugVizEnabled")):
			continue
		var visual := source.get_node_or_null("_FieldSourceAcademicViz") as MeshInstance3D
		if visual != null and visual.is_visible_in_tree() and (camera.cull_mask & visual.layers) != 0:
			found_visible_field = true
	_assert(found_visible_field, "F overlay remains camera-visible with O")


func _run_formal(tag: String) -> void:
	film_controller.call("set_mode", 1)
	await _settle(2)
	_assert(str(film_controller.call("GetModeName")) == "SNAPSHOT", "Formal SNAPSHOT entered")
	for _index in FORMAL_TIMEOUT_FRAMES:
		await process_frame
		if bool(film.get("ProbeViewAvailable")):
			break
	_assert(bool(film.get("ProbeViewAvailable")), "Formal SNAPSHOT completed")
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "complete", "Formal lifecycle Complete")
	_assert(str(film.get("CathedralContactAuthorityToken")) == "XPrimeRaySpatialKernel/BVH-v0", "BVH-v0 remains formal authority")
	_assert(bool(film.get("CathedralSpatialKernelDualValidationAvailable")), "dual validation available")
	_assert(bool(film.get("CathedralSpatialAuthorityPromotionReady")), "authority promotion gate green")
	_assert(int(film.get("CathedralSpatialKernelTotalAuthorityMismatchPixelCount")) == 0, "zero authority pixel mismatches")
	_assert(int(film.get("CathedralSpatialKernelMismatchQueryCount")) == 0, "zero witness query mismatches")
	_assert(int(film.get("CathedralSnapshotProcessedPixelCount")) == int(film.get("CathedralSnapshotTotalPixelCount")), "all pixels processed")
	var film_pixel_count := int(film.get("FilmWidthPixels")) * int(film.get("FilmHeightRows"))
	var net_committed_work_units := int(film.get("CathedralFormalWorkUnitCommitCount")) - int(film.get("CathedralFormalWorkUnitReplacementCount"))
	_assert(net_committed_work_units == film_pixel_count, "one net committed Formal work unit per film pixel")

	var bundle_dir := output_root.path_join("p%d_%s_%s" % [profile_index, scenario.to_lower(), tag])
	var reason := str(film.call("CapturePortableObservatoryBundle", bundle_dir,
		"probe-profile-%d-%s-%s" % [profile_index, scenario.to_lower(), tag],
		"hello-prt-gallery-v2", SCENE_PATH, "working-tree", "probe-profile-boundary"))
	_assert(reason.is_empty(), "portable bundle capture: " + reason)
	var manifest_file := FileAccess.open(bundle_dir.path_join("manifest.json"), FileAccess.READ)
	_assert(manifest_file != null, "bundle manifest readable")
	var manifest = JSON.parse_string(manifest_file.get_as_text())
	manifest_file = null
	_assert(typeof(manifest) == TYPE_DICTIONARY, "bundle manifest parses")
	acquisitions.append(_extract_acquisition(tag, manifest))


func _extract_acquisition(tag: String, manifest: Dictionary) -> Dictionary:
	var context: Dictionary = manifest["acquisition"]["context"]
	var provenance: Dictionary = manifest["runtime_provenance"]
	var source_hashes: Dictionary = manifest["source_hashes"]
	return {
		"tag": tag,
		"generation": int(film.get("SealedProbeViewGeneration")),
		"profile_id": str(film.get("CathedralSealedProbeProfileStableId")),
		"formal_policy_id": str(film.get("CathedralFormalPolicyId")),
		"formal_policy_sha256": str(film.get("CathedralFormalPolicySha256")),
		"formal_initial_range": float(film.get("CathedralFormalInitialRange")),
		"context_sha256": str(context["canonical_sha256"]),
		"query_dataset_sha256": str(film.get("CathedralProbeQueryDatasetSha256")),
		"raw_query_count": int(film.get("CathedralRawProbeQueryCount")),
		"canonical_query_count": int(film.get("CathedralProbeQueryCount")),
		"work_unit_commit_count": int(film.get("CathedralFormalWorkUnitCommitCount")),
		"work_unit_replacement_count": int(film.get("CathedralFormalWorkUnitReplacementCount")),
		"contact_count_sha256": str(film.get("CathedralSpatialKernelContactCountSha256")),
		"outcome_sha256": str(source_hashes["outcomes.bin"]["sha256"]),
		"final_steps_sha256": str(source_hashes["final_step_counts.bin"]["sha256"]),
		"effort_valid_sha256": str(source_hashes["effort_valid.bin"]["sha256"]),
		"outcome_histogram": manifest["histograms"]["outcome"],
		"contact_histogram": manifest["histograms"]["contact_events"],
		"effort_distribution": manifest["histograms"]["effort"],
		"processed_pixels": int(film.get("CathedralSnapshotProcessedPixelCount")),
		"geometry_sha256": str(film.get("CathedralSpatialKernelGeometrySnapshotSha256")),
		"spatial_context_sha256": str(film.get("CathedralSpatialAuthorityContextSha256")),
		"linear_context_sha256": str(film.get("CathedralSpatialKernelContextSha256")),
		"bvh_context_sha256": str(film.get("CathedralSpatialKernelBvhContextSha256")),
		"authority_token": str(film.get("CathedralContactAuthorityToken")),
		"linear_witness_sha256": str(provenance["linear_witness_sha256"]),
		"godot_witness_sha256": str(provenance["godot_witness_sha256"]),
		"bvh_mismatch_queries": int(provenance["bvh_mismatch_queries"]),
		"bvh_mismatch_pixels": int(provenance["bvh_mismatch_pixels"]),
	}


func _verify_q_mapping_only() -> void:
	var before := _sealed_identity()
	var replay := int(film.get("CathedralContactReplayInvocationCount"))
	var formal_resets := int(film.get("FormalWorkingStateResetCount"))
	var lifecycle := str(film.get("CathedralSnapshotLifecycleStateName"))
	for _index in 3:
		_send_key(KEY_Q)
		await _settle(2)
	_assert(_sealed_identity() == before, "Q preserves all sealed identities")
	_assert(int(film.get("CathedralContactReplayInvocationCount")) == replay, "Q performs no replay")
	_assert(int(film.get("FormalWorkingStateResetCount")) == formal_resets, "Q creates no working state")
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == lifecycle, "Q preserves lifecycle")


func _verify_profile_change_preserves_seal() -> void:
	var before := _sealed_identity()
	var current := int(film.get("ProbeProfileIndex"))
	_send_key(KEY_P)
	await _settle(3)
	_assert(_sealed_identity() == before, "P preserves sealed evidence")
	film.call("SetProbeProfileByIndex", current)
	await _settle(2)


func _verify_post_g_off_quiescence() -> void:
	film_controller.call("set_mode", 2)
	await _settle(3)
	film_controller.call("set_mode", 0)
	await _settle(3)
	var heartbeat := int(film.get("RuntimeHealthHeartbeatCount"))
	await _settle(300)
	_assert(int(film.get("RuntimeHealthHeartbeatCount")) == heartbeat, "post-G OFF heartbeat quiescent")
	_assert(not bool(film_controller.call("IsComputeActive")), "post-G OFF compute inactive")


func _sealed_identity() -> String:
	return JSON.stringify({
		"generation": int(film.get("SealedProbeViewGeneration")),
		"query": str(film.get("CathedralProbeQueryDatasetSha256")),
		"contact": str(film.get("CathedralSpatialKernelContactCountSha256")),
		"geometry": str(film.get("CathedralSpatialKernelGeometrySnapshotSha256")),
		"spatial": str(film.get("CathedralSpatialAuthorityContextSha256")),
		"bvh": str(film.get("CathedralSpatialKernelBvhContextSha256")),
		"authority": str(film.get("CathedralContactAuthorityToken")),
		"sealed_profile": str(film.get("CathedralSealedProbeProfileStableId")),
		"formal_policy": str(film.get("CathedralFormalPolicySha256")),
	})


func _scientific_identity(acquisition: Dictionary) -> String:
	var comparable := acquisition.duplicate(true)
	comparable.erase("tag")
	comparable.erase("generation")
	# Execution attempts and pixel-work-unit replacements expose cooperative
	# retries but are diagnostics, not sealed scientific identity.
	comparable.erase("raw_query_count")
	comparable.erase("work_unit_commit_count")
	comparable.erase("work_unit_replacement_count")
	return JSON.stringify(comparable)


func _write_result() -> void:
	var result := {
		"scenario": scenario,
		"profile_index": profile_index,
		"profile_id": str(film.call("GetProbeProfileStableIdByIndex", profile_index)),
		"acquisitions": acquisitions,
		"q_mapping_only": true,
		"profile_control_inert": true,
		"post_g_off_heartbeat_quiescent": true,
	}
	var path := output_root.path_join("result_p%d_%s.json" % [profile_index, scenario.to_lower()])
	var file := FileAccess.open(path, FileAccess.WRITE)
	_assert(file != null, "result file writable")
	file.store_string(JSON.stringify(result, "  "))
	file = null


func _send_key(code: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	event.echo = false
	Input.parse_input_event(event)


func _settle(frames: int) -> void:
	for _index in frames:
		await process_frame


func _assert(condition: bool, message: String) -> void:
	if not condition:
		_fail(message)


func _fail(message: String) -> void:
	push_error("PROBE_PROFILE_BOUNDARY FAIL scenario=%s profile=%d: %s" % [scenario, profile_index, message])
	quit(1)
