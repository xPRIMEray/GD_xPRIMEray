extends SceneTree

# Native-process regression harness for the Probe Profile/Formal boundary.
# One invocation runs one history in one process; the companion runner compares
# independent invocations so process-local working state cannot hide a leak.

const SCENE_PATH := "res://ObservatoryWorkbench.tscn"
const LIVE_FRAMES := 240
const DEFAULT_FORMAL_TIMEOUT_FRAMES := 1200
const G5_ACTIVE_SCHEDULE_TIMEOUT_FRAMES := 1200
const SCHEDULE_PROBE_SCRIPT := preload("res://Tests/MeanderQualification/LiveImportanceScheduleProbe.cs")
const LOCKED_POLICY_SHA := "317abb82b4d3df9b42476e54f2f9cfab839eb1d161b80a2db795b7e7ce4596da"
const LOCKED_QUERY_SHA := "954b29ad2cac80cf1dedbb83cb693c9a0d5960f70c2971209cb2c50b4ef116d7"
const LOCKED_CONTACT_SHA := "a23b3e1b15205fbfe4edbd86a0ab0bb621fa5d65aaeb3613becfcb0b4ec8c73a"
const LOCKED_OUTCOME_SHA := "2a7a3b5f6cb14cc09aaabd1d8ce102d8cf9fddc2c36e1d3d675b9542cc51cfad"
const LOCKED_EFFORT_SHA := "4c4fd8b791a27d233cbf18e576fd69d1da2c8ac58a5b3f86e2eb73c772d7ad86"
const LOCKED_GEOMETRY_SHA := "7f9d193006179d0d359a575ac52dec6822857efc6a78df8621c40f1172c5c32b"
const LOCKED_SPATIAL_SHA := "249ae1121f465dd05a562cbd2c9c82d0e7e4b4c5cfe7b69a845471d4eaaca894"
const LOCKED_BVH_SHA := "e41f33da677db9ff03546aff5f85b8b6d5e43ee390eaf94cea665b7da5a6ba7f"

var scenario := "A"
var profile_index := 0
var output_root := ""
var formal_timeout_frames := DEFAULT_FORMAL_TIMEOUT_FRAMES
var record_formal_transport_ledger := false
var formal_transport_ledger_summary: Dictionary = {}
var formal_transport_ledger_acquisitions: Array[Dictionary] = []
var scene_root: Node
var chamber: Node
var film: Node
var film_controller: Node
var player: CharacterBody3D
var workbench_profile_option: OptionButton
var acquisitions: Array[Dictionary] = []
var schedule_probe: RefCounted
var guarded_live_schedule: Dictionary = {}
var guarded_live_band_height := 0


func _initialize() -> void:
	_parse_args()
	_verify_completed_recorder_negative_fixtures()
	scene_root = load(SCENE_PATH).instantiate()
	get_root().add_child(scene_root)
	await _settle(30)
	chamber = scene_root.get_node("PlayableWorld/TransportChamberWorld")
	film = chamber.get_node("GrinFilmCamera")
	film_controller = chamber.get_node("FilmController")
	player = chamber.get_node("TransportChamberPlayer")
	workbench_profile_option = scene_root.get_node("UILayer/UIRoot/SideDock/Tabs/Observatory/Margin/Root/InstrumentControls/ProbeProfileOption")
	schedule_probe = SCHEDULE_PROBE_SCRIPT.new()
	_assert(schedule_probe != null, "LIVE importance schedule probe available")
	player.set_physics_process(false)
	film.set("RecordFormalTransportLedger", record_formal_transport_ledger)
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
		"G5":
			await _prepare_active_g5_live_schedule_then_off()
			await _run_formal("final", true)
			_assert_locked_canonical(acquisitions[-1])
		"G5C":
			await _prepare_completed_g5_live_schedule_then_off()
			await _run_formal("final", true)
			_assert_locked_canonical(acquisitions[-1])
		_:
			_fail("unknown scenario " + scenario)
	if acquisitions.size() > 1:
		_assert(_scientific_identity(acquisitions[0]) == _scientific_identity(acquisitions[-1]),
			"repeated Formal acquisition identity")
	_verify_formal_transport_recorder()

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
		elif arg.begins_with("--formal-timeout-frames="):
			formal_timeout_frames = maxi(1, int(arg.trim_prefix("--formal-timeout-frames=")))
		elif arg.begins_with("--profile="):
			profile_index = int(arg.trim_prefix("--profile="))
		elif arg.begins_with("--output="):
			output_root = arg.trim_prefix("--output=")
		elif arg == "--record-formal-transport-ledger":
			record_formal_transport_ledger = true
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


func _capture_live_schedule() -> Dictionary:
	return schedule_probe.call("Capture", film) as Dictionary


func _same_live_schedule_position(expected: Dictionary, actual: Dictionary) -> bool:
	return int(actual.get("context_generation", -1)) == int(expected.get("context_generation", -2)) \
		and int(actual.get("schedule_id", -1)) == int(expected.get("schedule_id", -2)) \
		and int(actual.get("executed_count", -1)) == int(expected.get("executed_count", -2)) \
		and int(actual.get("completed_count", -1)) == int(expected.get("completed_count", -2)) \
		and int(actual.get("rotation_before", -1)) == int(expected.get("rotation_before", -2)) \
		and int(actual.get("rotation_after", -1)) == int(expected.get("rotation_after", -2)) \
		and bool(actual.get("schedule_active", true)) == bool(expected.get("schedule_active", false)) \
		and int(actual.get("schedule_total_bands", -1)) == int(expected.get("schedule_total_bands", -2)) \
		and int(actual.get("schedule_cursor", -1)) == int(expected.get("schedule_cursor", -2))


func _prepare_active_g5_live_schedule_then_off() -> void:
	film.set("UseImportanceOrdering", true)
	film_controller.call("set_mode", 2)
	await _settle(3)
	_assert(str(film_controller.call("GetModeName")) == "LIVE", "G5 isolation entered LIVE")
	var active: Dictionary = {}
	for _index in G5_ACTIVE_SCHEDULE_TIMEOUT_FRAMES:
		await process_frame
		var candidate := _capture_live_schedule()
		if str(candidate.get("state", "")) == "Prepared" \
				and int(candidate.get("frontier_eligible_count", 0)) > 0 \
				and int(candidate.get("prioritized_garden_count", 0)) > 0 \
				and int(candidate.get("executed_count", 0)) > 0 \
				and int(candidate.get("executed_count", 0)) < int(candidate.get("prepared_count", 0)):
			active = candidate
			break
	_assert(not active.is_empty(), "G5 LIVE reached an active genuine-frontier schedule")
	guarded_live_schedule = active.duplicate(true)
	guarded_live_band_height = int(active.get("resolved_band_height", 0))
	film_controller.call("set_mode", 0)
	await _settle(3)
	_assert(str(film_controller.call("GetModeName")) == "OFF", "active G5 LIVE schedule transitioned OFF")
	var after_off := _capture_live_schedule()
	_assert(str(after_off.get("state", "")) == "Prepared", "OFF preserves active LIVE schedule for isolation test")
	_assert(_same_live_schedule_position(guarded_live_schedule, after_off), "OFF does not consume LIVE schedule")
	print("G5_FORMAL_ISOLATION ACTIVE context=%d pass=%d schedule=%d executed=%d/%d rotation=%d->%d band_height=%d frontier=%d gardens=%d" % [
		int(active["context_generation"]), int(active["live_pass_id"]), int(active["schedule_id"]),
		int(active["executed_count"]), int(active["prepared_count"]), int(active["rotation_before"]),
		int(active["rotation_after"]), guarded_live_band_height, int(active["frontier_eligible_count"]),
		int(active["prioritized_garden_count"])])


func _prepare_completed_g5_live_schedule_then_off() -> void:
	film.set("UseImportanceOrdering", true)
	film_controller.call("set_mode", 2)
	await _settle(3)
	_assert(str(film_controller.call("GetModeName")) == "LIVE", "completed G5 isolation entered LIVE")
	var completed: Dictionary = {}
	for _index in G5_ACTIVE_SCHEDULE_TIMEOUT_FRAMES:
		await process_frame
		var candidate := _capture_live_schedule()
		if str(candidate.get("state", "")) == "Completed" \
				and not bool(candidate.get("schedule_active", true)) \
				and int(candidate.get("schedule_total_bands", 0)) > 0 \
				and int(candidate.get("schedule_cursor", -1)) == int(candidate.get("schedule_total_bands", -2)) \
				and int(candidate.get("frontier_eligible_count", 0)) > 0 \
				and int(candidate.get("prioritized_garden_count", 0)) > 0 \
				and int(candidate.get("executed_count", 0)) == int(candidate.get("prepared_count", -1)) \
				and int(candidate.get("completed_count", 0)) == int(candidate.get("prepared_count", -1)):
			completed = candidate
			break
	_assert(not completed.is_empty(), "G5 LIVE reached a genuinely completed frontier schedule")
	guarded_live_schedule = completed.duplicate(true)
	guarded_live_band_height = int(completed.get("resolved_band_height", 0))
	film_controller.call("set_mode", 0)
	await _settle(3)
	_assert(str(film_controller.call("GetModeName")) == "OFF", "completed G5 LIVE schedule transitioned OFF")
	var after_off := _capture_live_schedule()
	_assert(str(after_off.get("state", "")) == "Completed", "OFF preserves completed LIVE schedule for isolation test")
	_assert(not bool(after_off.get("schedule_active", true)), "completed LIVE schedule remains inactive before Formal")
	_assert(int(after_off.get("schedule_total_bands", 0)) > 0, "completed LIVE schedule retains terminal band count")
	_assert(_same_live_schedule_position(guarded_live_schedule, after_off), "OFF does not alter completed LIVE schedule")
	print("G5_FORMAL_ISOLATION COMPLETED_PRECONDITION context=%d pass=%d schedule=%d active=%s total=%d cursor=%d executed=%d completed=%d rotation=%d->%d band_height=%d frontier=%d gardens=%d" % [
		int(completed["context_generation"]), int(completed["live_pass_id"]), int(completed["schedule_id"]),
		str(completed["schedule_active"]), int(completed["schedule_total_bands"]), int(completed["schedule_cursor"]),
		int(completed["executed_count"]), int(completed["completed_count"]), int(completed["rotation_before"]),
		int(completed["rotation_after"]), guarded_live_band_height, int(completed["frontier_eligible_count"]),
		int(completed["prioritized_garden_count"])])


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


func _run_formal(tag: String, guard_live_schedule := false) -> void:
	film_controller.call("set_mode", 1)
	await _settle(2)
	_assert(str(film_controller.call("GetModeName")) == "SNAPSHOT", "Formal SNAPSHOT entered")
	if guard_live_schedule:
		var during_start := _capture_live_schedule()
		_assert(_same_live_schedule_position(guarded_live_schedule, during_start), "Formal start does not advance LIVE schedule")
		_assert(int(film.get("BandHeightRowsResolved")) == int(film.get("RowsPerFrame")), "Formal uses canonical row-band height")
		_assert(int(film.get("BandHeightRowsResolved")) != guarded_live_band_height, "Formal does not inherit LIVE schedule band height")
	for _index in formal_timeout_frames:
		await process_frame
		if guard_live_schedule:
			_assert(_same_live_schedule_position(guarded_live_schedule, _capture_live_schedule()), "Formal does not mutate LIVE schedule")
		if bool(film.get("ProbeViewAvailable")):
			break
	_assert(bool(film.get("ProbeViewAvailable")), "Formal SNAPSHOT completed")
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "complete", "Formal lifecycle Complete")
	_assert(str(film.get("CathedralContactAuthorityToken")) == "XPrimeRaySpatialKernel/BVH-v0", "BVH-v0 remains formal authority")
	_assert(bool(film.get("CathedralSpatialKernelDualValidationAvailable")), "dual validation available")
	_assert(bool(film.get("CathedralSpatialAuthorityPromotionReady")), "authority promotion gate green")
	_assert(int(film.get("CathedralSpatialKernelTotalAuthorityMismatchPixelCount")) == 0, "zero authority pixel mismatches")
	_assert(int(film.get("CathedralSpatialKernelMismatchQueryCount")) == 0, "zero witness query mismatches")
	_assert(int(film.get("CathedralSpatialKernelGodotQueryComparisonCount")) == int(film.get("CathedralProbeQueryCount")), "default Formal compares every Godot query")
	_assert(int(film.get("CathedralSnapshotProcessedPixelCount")) == int(film.get("CathedralSnapshotTotalPixelCount")), "all pixels processed")
	var film_pixel_count := int(film.get("FilmWidthPixels")) * int(film.get("FilmHeightRows"))
	var net_committed_work_units := int(film.get("CathedralFormalWorkUnitCommitCount")) - int(film.get("CathedralFormalWorkUnitReplacementCount"))
	_assert(net_committed_work_units == film_pixel_count, "one net committed Formal work unit per film pixel")

	var bundle_dir := output_root.path_join("p%d_%s_%s" % [profile_index, scenario.to_lower(), tag])
	var reason := str(film.call("CapturePortableObservatoryBundle", bundle_dir,
		"probe-profile-%d-%s-%s" % [profile_index, scenario.to_lower(), tag],
		"hello-prt-gallery-v2", SCENE_PATH, "working-tree", "probe-profile-boundary",
		OS.get_environment("XPRIMERAY_SOURCE_TREE_STATE"), OS.get_environment("XPRIMERAY_AUTHORED_DIFF_SHA256")))
	_assert(reason.is_empty(), "portable bundle capture: " + reason)
	if record_formal_transport_ledger:
		var engine_commit := OS.get_environment("XPRIMERAY_ENGINE_COMMIT")
		_assert(not engine_commit.is_empty(), "portal-remap bundle engine commit available")
		var portal_bundle_dir := output_root.path_join("portal_remap_observation_p%d_%s_%s" % [
			profile_index, scenario.to_lower(), tag])
		var portal_reason := str(film.call("CapturePortalRemapObservationBundle", portal_bundle_dir,
			"portal-remap-observation-%d-%s-%s" % [profile_index, scenario.to_lower(), tag],
			"hello-prt-gallery-v2", SCENE_PATH, engine_commit,
			OS.get_environment("XPRIMERAY_SOURCE_TREE_STATE"), OS.get_environment("XPRIMERAY_AUTHORED_DIFF_SHA256")))
		_assert(portal_reason.is_empty(), "provisional portal-remap bundle capture: " + portal_reason)
		print("PORTAL_REMAP_OBSERVATION_BUNDLE path=%s" % portal_bundle_dir)
	else:
		var off_probe_dir := output_root.path_join("portal_remap_observation_disabled_probe")
		var off_probe_reason := str(film.call("CapturePortalRemapObservationBundle", off_probe_dir,
			"portal-remap-observation-disabled-probe", "hello-prt-gallery-v2", SCENE_PATH,
			OS.get_environment("XPRIMERAY_ENGINE_COMMIT"),
			OS.get_environment("XPRIMERAY_SOURCE_TREE_STATE"), OS.get_environment("XPRIMERAY_AUTHORED_DIFF_SHA256")))
		_assert(off_probe_reason == "formal_transport_observation_unavailable", "recorder OFF has no sealed observation snapshot")
		_assert(not DirAccess.dir_exists_absolute(off_probe_dir), "recorder OFF writes no portal observation artifact")
	var manifest_file := FileAccess.open(bundle_dir.path_join("manifest.json"), FileAccess.READ)
	_assert(manifest_file != null, "bundle manifest readable")
	var manifest = JSON.parse_string(manifest_file.get_as_text())
	manifest_file = null
	_assert(typeof(manifest) == TYPE_DICTIONARY, "bundle manifest parses")
	var acquisition := _extract_acquisition(tag, manifest)
	acquisitions.append(acquisition)
	_capture_formal_transport_recorder_acquisition(tag, acquisition, bundle_dir)
	if guard_live_schedule:
		var after_formal := _capture_live_schedule()
		_assert(_same_live_schedule_position(guarded_live_schedule, after_formal), "Formal completion preserves LIVE cursor and rotation")
		print("G5_FORMAL_ISOLATION PASS schedule=%d executed=%d rotation=%d->%d formal_band_height=%d" % [
			int(after_formal["schedule_id"]), int(after_formal["executed_count"]),
			int(after_formal["rotation_before"]), int(after_formal["rotation_after"]),
			int(film.get("BandHeightRowsResolved"))])


func _assert_locked_canonical(acquisition: Dictionary) -> void:
	_assert(str(acquisition["formal_policy_sha256"]) == LOCKED_POLICY_SHA, "locked Formal policy SHA")
	_assert(str(acquisition["query_dataset_sha256"]) == LOCKED_QUERY_SHA, "locked Query SHA")
	_assert(str(acquisition["contact_count_sha256"]) == LOCKED_CONTACT_SHA, "locked Contact SHA")
	_assert(str(acquisition["outcome_sha256"]) == LOCKED_OUTCOME_SHA, "locked Outcome SHA")
	_assert(str(acquisition["final_steps_sha256"]) == LOCKED_EFFORT_SHA, "locked Effort SHA")
	_assert(str(acquisition["geometry_sha256"]) == LOCKED_GEOMETRY_SHA, "locked Geometry SHA")
	_assert(str(acquisition["spatial_context_sha256"]) == LOCKED_SPATIAL_SHA, "locked Spatial context SHA")
	_assert(str(acquisition["bvh_context_sha256"]) == LOCKED_BVH_SHA, "locked BVH context SHA")
	_assert(int(acquisition["processed_pixels"]) == 14400, "locked classified work units")
	_assert(int(acquisition["canonical_query_count"]) == 521819, "locked committed query count")
	_assert(int(acquisition["work_unit_commit_count"]) == 14400, "locked Formal work-unit commits")
	_assert(int(acquisition["work_unit_replacement_count"]) == 0, "no Formal work-unit replacements")
	_assert(int(acquisition["bvh_mismatch_queries"]) == 0, "zero BVH query mismatches")
	_assert(int(acquisition["bvh_mismatch_pixels"]) == 0, "zero BVH pixel mismatches")


func _capture_formal_transport_recorder_acquisition(tag: String, acquisition: Dictionary, bundle_dir: String) -> void:
	var summary := (film.call("GetFormalTransportLedgerSummaryForTesting") as Dictionary).duplicate(true)
	var pixel_count := int(summary.get("pixel_count", 0))
	var finalized_count := int(summary.get("finalized_count", 0))
	var unfinalized_current := maxi(0, pixel_count - finalized_count)
	var recorder_enabled := bool(summary.get("recorder_enabled_for_capture", false))
	var recorder_status := "disabled"
	if recorder_enabled:
		recorder_status = "provisional_complete" if unfinalized_current == 0 else "provisional_incomplete"
	summary["acquisition_tag"] = tag
	summary["acquisition_generation"] = int(acquisition["generation"])
	summary["unfinalized_current_pixel_records"] = unfinalized_current
	summary["recorder_status"] = recorder_status
	summary["bundle_path"] = bundle_dir
	summary["result_identity"] = {
		"formal_policy_sha256": acquisition["formal_policy_sha256"],
		"query_dataset_sha256": acquisition["query_dataset_sha256"],
		"contact_count_sha256": acquisition["contact_count_sha256"],
		"outcome_sha256": acquisition["outcome_sha256"],
		"final_steps_sha256": acquisition["final_steps_sha256"],
		"geometry_sha256": acquisition["geometry_sha256"],
		"spatial_context_sha256": acquisition["spatial_context_sha256"],
		"bvh_context_sha256": acquisition["bvh_context_sha256"],
	}

	_assert(bool(summary.get("available", false)), "%s recorder summary available" % tag)
	_assert(int(summary.get("capture_generation", -1)) == int(acquisition["generation"]),
		"%s recorder generation matches acquisition" % tag)
	_assert(int(summary.get("work_unit_commits", -1)) == int(acquisition["work_unit_commit_count"]),
		"%s recorder work-unit commits match acquisition" % tag)
	_assert(int(summary.get("work_unit_replacements", -1)) == int(acquisition["work_unit_replacement_count"]),
		"%s recorder replacements match acquisition" % tag)
	if recorder_enabled:
		var completion_conditions := _recorder_completion_conditions(summary)
		_assert(bool(completion_conditions["unfinalized_zero"]),
			"%s has no unfinalized current pixel records" % tag)
		_assert(bool(completion_conditions["status_complete"]),
			"%s recorder status is provisional_complete" % tag)
		_assert(bool(completion_conditions["non_sealable"]),
			"%s provisional recorder remains non-sealable" % tag)
		_assert(bool(completion_conditions["finalizations_match"]),
			"%s every eligible committed attempt finalized" % tag)

	formal_transport_ledger_acquisitions.append(summary.duplicate(true))
	var artifact_path := output_root.path_join("formal_transport_ledger_p%d_%s_%s.json" % [
		profile_index, scenario.to_lower(), tag])
	var artifact := FileAccess.open(artifact_path, FileAccess.WRITE)
	_assert(artifact != null, "%s recorder evidence artifact writable" % tag)
	artifact.store_string(JSON.stringify(summary, "  "))
	artifact = null
	print("FORMAL_TRANSPORT_LEDGER_ACQUISITION tag=%s %s" % [tag, JSON.stringify(summary)])


func _recorder_completion_conditions(summary: Dictionary) -> Dictionary:
	return {
		"unfinalized_zero": int(summary.get("unfinalized_current_pixel_records", -1)) == 0,
		"status_complete": str(summary.get("recorder_status", "")) == "provisional_complete",
		"non_sealable": not bool(summary.get("can_seal", true)),
		"finalizations_match": int(summary.get("accepted_finalizations", -1)) == int(summary.get("finalization_eligible_commits", -2)),
	}


func _recorder_completion_conditions_pass(summary: Dictionary) -> bool:
	var conditions := _recorder_completion_conditions(summary)
	return bool(conditions["unfinalized_zero"]) \
		and bool(conditions["status_complete"]) \
		and bool(conditions["non_sealable"]) \
		and bool(conditions["finalizations_match"])


func _verify_completed_recorder_negative_fixtures() -> void:
	var valid_later := {
		"unfinalized_current_pixel_records": 0,
		"recorder_status": "provisional_complete",
		"can_seal": false,
		"accepted_finalizations": 14400,
		"finalization_eligible_commits": 14400,
	}
	_assert(_recorder_completion_conditions_pass(valid_later), "recorder completion validation accepts valid later acquisition")
	var invalid_earlier_cases: Array[Dictionary] = []
	var unfinalized := valid_later.duplicate(true)
	unfinalized["unfinalized_current_pixel_records"] = 1
	invalid_earlier_cases.append(unfinalized)
	var incomplete_status := valid_later.duplicate(true)
	incomplete_status["recorder_status"] = "provisional_incomplete"
	invalid_earlier_cases.append(incomplete_status)
	var mismatched_finalizations := valid_later.duplicate(true)
	mismatched_finalizations["accepted_finalizations"] = 14399
	invalid_earlier_cases.append(mismatched_finalizations)
	var sealable := valid_later.duplicate(true)
	sealable["can_seal"] = true
	invalid_earlier_cases.append(sealable)
	for invalid_earlier in invalid_earlier_cases:
		_assert(not _recorder_completion_conditions_pass(invalid_earlier),
			"invalid earlier recorder acquisition rejected")
		_assert(not (_recorder_completion_conditions_pass(invalid_earlier) \
			and _recorder_completion_conditions_pass(valid_later)),
			"valid later acquisition cannot hide invalid earlier recorder evidence")
	print("FORMAL_TRANSPORT_RECORDER_NEGATIVE_FIXTURES PASS cases=%d" % invalid_earlier_cases.size())


func _verify_formal_transport_recorder() -> void:
	formal_transport_ledger_summary = film.call("GetFormalTransportLedgerSummaryForTesting") as Dictionary
	print("FORMAL_TRANSPORT_LEDGER %s" % JSON.stringify(formal_transport_ledger_summary))
	_assert(bool(formal_transport_ledger_summary.get("available", false)), "Formal transport recorder summary available")
	_assert(not bool(formal_transport_ledger_summary.get("can_seal", true)), "provisional recorder cannot seal")
	if not record_formal_transport_ledger:
		_assert(not bool(formal_transport_ledger_summary.get("recorder_enabled_for_capture", true)), "recorder OFF remains disabled")
		_assert(int(formal_transport_ledger_summary.get("attempt_allocations", -1)) == 0, "recorder OFF performs no attempt allocation")
		_assert(int(formal_transport_ledger_summary.get("recorded_events", -1)) == 0, "recorder OFF creates no events")
		_assert(int(formal_transport_ledger_summary.get("pixel_count", -1)) == 0, "recorder OFF creates no pixel slots")
		return

	var film_pixel_count := int(film.get("FilmWidthPixels")) * int(film.get("FilmHeightRows"))
	_assert(bool(formal_transport_ledger_summary.get("recorder_enabled_for_capture", false)), "recorder ON retained for completed capture")
	_assert(int(formal_transport_ledger_summary.get("pixel_count", -1)) == film_pixel_count, "one authoritative provisional pixel record")
	_assert(int(formal_transport_ledger_summary.get("finalized_count", -1)) == film_pixel_count, "same-attempt classifications finalized")
	_assert(int(formal_transport_ledger_summary.get("accepted_finalizations", -1)) == int(formal_transport_ledger_summary.get("finalization_eligible_commits", -2)),
		"every eligible committed attempt finalized")
	_assert(int(formal_transport_ledger_summary.get("invalid_event_ordinals", -1)) == 0, "captured event ordinals contiguous")
	_assert(int(formal_transport_ledger_summary.get("invalid_provisional_status", -1)) == 0, "all records remain explicitly provisional")
	_assert(int(formal_transport_ledger_summary.get("pending_question_count", -1)) == int(film.get("CathedralProbeQueryCount")), "every committed question remains pending")
	var event_counts: Dictionary = formal_transport_ledger_summary.get("event_counts", {}) as Dictionary
	_assert(int(event_counts.get("ProbeQuestionIssued", -1)) == int(film.get("CathedralProbeQueryCount")), "question events match canonical dataset")
	_assert(int(event_counts.get("Terminal", -1)) == film_pixel_count, "one supported terminal event per canonical pixel")
	_assert(int(formal_transport_ledger_summary.get("found_count", 0)) > 0, "geometry/background hits represented")
	_assert(int(formal_transport_ledger_summary.get("no_hit_count", 0)) > 0, "no-hit paths represented without fabricated contacts")

	var generation := int(formal_transport_ledger_summary.get("capture_generation", 0))
	_assert(not bool(film.call("TryFinalizeFormalTransportAttemptForTesting", generation, 0, 2147483647, false, 0)), "stale commit-version ticket rejected")
	var other_capture_generation := generation - 1 if generation > 1 else generation + 1
	_assert(not bool(film.call("TryFinalizeFormalTransportAttemptForTesting", other_capture_generation, 0, 1, false, 0)), "cross-capture ticket rejected")
	formal_transport_ledger_summary = film.call("GetFormalTransportLedgerSummaryForTesting") as Dictionary
	_assert(int(formal_transport_ledger_summary.get("stale_finalization_rejects", -1)) == 2, "stale ticket rejection diagnostics")


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
		"formal_transport_ledger": formal_transport_ledger_summary,
		"formal_transport_ledgers_by_acquisition": formal_transport_ledger_acquisitions,
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
