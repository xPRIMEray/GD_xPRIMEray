extends SceneTree

const SCENE_PATH := "res://Experiences/SuspendedPortalGallery.tscn"
const SEMANTIC_SCENE_ID := "suspended-portal-gallery-v0"
const SCENE := preload(SCENE_PATH)
const FORMAL_TIMEOUT_FRAMES := 1200

var output_root := "res://output/suspended_portal_gallery_v0/p14_q_acquisition"
var bundle_root := ""
var divergence_path := ""

var gallery: Node3D
var world: Node3D
var player: CharacterBody3D
var camera: Camera3D
var film: Node
var film_controller: Node
var film_view: TextureRect
var failures: Array[String] = []


func _parse_args() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--output="):
			output_root = arg.trim_prefix("--output=")


func _initialize() -> void:
	_parse_args()
	bundle_root = output_root.path_join("portable_bundle")
	divergence_path = output_root.path_join("query_divergence_attribution.json")
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output_root))
	gallery = SCENE.instantiate()
	get_root().add_child(gallery)
	await _settle(12)
	world = gallery.get_node("SuspendedPortalGalleryWorld")
	player = gallery.call("get_player") as CharacterBody3D
	camera = player.get_node("Head/PlayerCamera") as Camera3D
	film = world.get_node("GrinFilmCamera")
	film_controller = world.get_node("FilmController")
	film_view = world.get_node("CanvasLayer/HUDRoot/FilmPlate/FilmView") as TextureRect
	film.set("CaptureSpatialQueryDivergenceDiagnostics", true)
	player.call("SetInputEnabled", false, true)
	player.set_physics_process(false)
	await _settle(3)

	var no_snapshot_generation := int(film.get("SealedProbeViewGeneration"))
	var no_snapshot_reset_count := int(film.get("FormalWorkingStateResetCount"))
	var no_snapshot_replay_count := int(film.get("CathedralContactReplayInvocationCount"))
	var no_snapshot_view := int(film.get("CurrentProbeView"))
	_assert(not bool(film.get("ProbeViewAvailable")), "fresh gallery unexpectedly has sealed Probe View data")
	await _dispatch_key(KEY_Q)
	_assert(int(film.get("CurrentProbeView")) == (no_snapshot_view + 1) % 3, "Q unavailable-state selection did not advance")
	await _dispatch_key(KEY_Q, true)
	_assert(int(film.get("CurrentProbeView")) == no_snapshot_view, "Shift+Q unavailable-state selection did not reverse")
	_assert(not bool(film.get("ProbeViewAvailable")), "unavailable Q created sealed evidence")
	_assert(int(film.get("SealedProbeViewGeneration")) == no_snapshot_generation, "unavailable Q changed generation")
	_assert(int(film.get("FormalWorkingStateResetCount")) == no_snapshot_reset_count, "unavailable Q created Formal working state")
	_assert(int(film.get("CathedralContactReplayInvocationCount")) == no_snapshot_replay_count, "unavailable Q invoked replay")

	film_controller.call("set_mode", 1)
	await _settle(2)
	_assert(String(film_controller.call("GetModeName")) == "SNAPSHOT", "production FilmController did not enter SNAPSHOT")
	var formal_complete := await _wait_for_formal_complete()
	if not formal_complete:
		_write_blocked_checkpoint()
		_finish()
		return
	_assert(bool(film.get("ProbeViewAvailable")), "gallery Formal acquisition did not seal Probe View data")
	_assert(String(film.get("CathedralSnapshotLifecycleStateName")) == "complete", "gallery Formal lifecycle is not Complete")
	_assert(int(film.get("CathedralSnapshotProcessedPixelCount")) == int(film.get("CathedralSnapshotTotalPixelCount")), "gallery Formal acquisition has unprocessed pixels")
	_assert(bool(film.get("CathedralSpatialAuthorityPromotionReady")), "gallery Formal authority promotion gate is not ready")
	_assert(String(film.get("CathedralContactAuthorityToken")) == "XPrimeRaySpatialKernel/BVH-v0", "gallery Formal authority is not BVH-v0")
	_assert(int(film.get("CathedralSpatialKernelTotalAuthorityMismatchPixelCount")) == 0, "gallery authority pixel mismatches")
	_assert(int(film.get("CathedralSpatialKernelMismatchQueryCount")) == 0, "gallery witness query mismatches")
	_assert(int(film.get("CathedralSpatialKernelGodotQueryComparisonCount")) == int(film.get("CathedralProbeQueryCount")), "gallery Godot query comparison coverage")
	var film_pixels := int(film.get("FilmWidthPixels")) * int(film.get("FilmHeightRows"))
	var net_work_units := int(film.get("CathedralFormalWorkUnitCommitCount")) - int(film.get("CathedralFormalWorkUnitReplacementCount"))
	_assert(net_work_units == film_pixels, "gallery seal is not one net work unit per pixel")

	var export_reason := String(film.call(
		"CapturePortableObservatoryBundle",
		ProjectSettings.globalize_path(bundle_root),
		"suspended-portal-gallery-p14",
		SEMANTIC_SCENE_ID,
		SCENE_PATH,
		"4c93bcaa684417f93a8423627ce795a1b7a9fb51",
		"p13-suspended-gallery -> p14-cathedral-formal-q",
		_source_tree_state(),
		_authored_diff_sha256()))
	_assert(export_reason.is_empty(), "portable gallery bundle export failed: " + export_reason)
	var portable_manifest_path := bundle_root.path_join("manifest.json")
	var portable_manifest = JSON.parse_string(FileAccess.get_file_as_string(portable_manifest_path))
	_assert(portable_manifest is Dictionary, "portable gallery manifest did not parse")
	if not portable_manifest is Dictionary:
		_finish()
		return
	var manifest: Dictionary = portable_manifest
	_assert(String(manifest["scene"]["semantic_scene_id"]) == SEMANTIC_SCENE_ID, "portable bundle semantic scene identity")
	_assert(String(manifest["scene"]["godot_scene_path"]) == SCENE_PATH, "portable bundle scene path")
	_assert(String(manifest["acquisition"]["state"]) == "Complete", "portable bundle acquisition status")
	_assert(String(manifest["runtime_provenance"]["source_tree_state"]) == _source_tree_state(), "portable bundle source-tree state")
	_assert(String(manifest["runtime_provenance"]["authored_diff_sha256"]) == _authored_diff_sha256(), "portable bundle authored-diff fingerprint")
	_assert(int(manifest["acquisition"]["generation"]) == int(film.get("SealedProbeViewGeneration")), "portable bundle sealed generation")
	_assert(int(manifest["acquisition"]["unprocessed"]) == 0, "portable bundle unprocessed count")
	_assert(bool(manifest["same_generation_proof"]["source_hashes_equal_before_after_mapping"]), "portable mapping changed raw sources")
	_assert(int(manifest["same_generation_proof"]["generation_at_export_start"]) == int(manifest["same_generation_proof"]["generation_at_export_end"]), "portable export crossed generations")
	_write_scientific_checkpoint(manifest)

	var sealed_identity_before := _sealed_identity()
	var replay_before := int(film.get("CathedralContactReplayInvocationCount"))
	var reset_before := int(film.get("FormalWorkingStateResetCount"))
	var generation_before := int(film.get("SealedProbeViewGeneration"))
	film.call("SetProbeView", 0)
	var plate_comparisons := {}
	plate_comparisons["outcome"] = await _capture_plate_stages("outcome", "outcome_plate_rgba", manifest)
	var displayed_hashes := { "outcome": _available_readback_hash(plate_comparisons["outcome"]) }

	await _dispatch_key(KEY_Q)
	_assert(int(film.get("CurrentProbeView")) == 1, "Q did not select Contact Events")
	plate_comparisons["contact_events"] = await _capture_plate_stages("contact_events", "contact_events_plate_rgba", manifest)
	displayed_hashes["contact_events"] = _available_readback_hash(plate_comparisons["contact_events"])

	await _dispatch_key(KEY_Q)
	_assert(int(film.get("CurrentProbeView")) == 2, "second Q did not select Transport Effort")
	plate_comparisons["transport_effort"] = await _capture_plate_stages("transport_effort", "transport_effort_plate_rgba", manifest)
	displayed_hashes["transport_effort"] = _available_readback_hash(plate_comparisons["transport_effort"])

	await _dispatch_key(KEY_Q, true)
	_assert(int(film.get("CurrentProbeView")) == 1, "Shift+Q did not reverse to Contact Events")
	var contact_reverse: Dictionary = await _capture_plate_stages("contact_events", "contact_events_plate_rgba", manifest)
	plate_comparisons["contact_events_reverse"] = contact_reverse
	_assert(String(contact_reverse["cpu"]["sha256"]) == String(plate_comparisons["contact_events"]["cpu"]["sha256"]), "Shift+Q remapped different Contact Events CPU bytes")
	_assert(_available_readback_hash(contact_reverse) == String(displayed_hashes["contact_events"]), "Shift+Q remapped different Contact Events available readback bytes")
	_assert(_sealed_identity() == sealed_identity_before, "Q cycling changed sealed scientific identity")
	_assert(int(film.get("SealedProbeViewGeneration")) == generation_before, "Q cycling changed sealed generation")
	_assert(int(film.get("CathedralContactReplayInvocationCount")) == replay_before, "Q cycling invoked contact replay")
	_assert(int(film.get("FormalWorkingStateResetCount")) == reset_before, "Q cycling reset Formal working state")
	_assert(String(film.get("CathedralSnapshotLifecycleStateName")) == "complete", "Q cycling changed Formal lifecycle")
	_write_plate_comparison_report(plate_comparisons, sealed_identity_before, replay_before, reset_before, generation_before)
	if not bool(plate_comparisons["outcome"]["gpu"]["supported"]):
		print("SUSPENDED_GALLERY_P14_8 CPU_PARITY=PASS GPU_READBACK=UNSUPPORTED reason=headless_display_server_has_no_frame_post_draw")
		_finish()
		return
	if not failures.is_empty():
		# The existing headless plate/capture issue is downstream of the sealed
		# scientific checkpoint. Do not enter viewport capture after it fails.
		_finish()
		return

	var quick_capture: Array = await _capture_selected_q_view()
	_assert(int(film.get("CurrentProbeView")) == 1, "F10 changed selected Q view")
	_assert(_sealed_identity() == sealed_identity_before, "F10 changed sealed scientific identity")
	_assert(int(film.get("SealedProbeViewGeneration")) == generation_before, "F10 changed sealed generation")

	var source_hashes: Dictionary = manifest["source_hashes"]
	var provenance: Dictionary = manifest["runtime_provenance"]
	var package := {
		"schema": "xprimeray.suspended-portal-gallery-q-package.v1",
		"scene": {
			"instantiated_scene": SCENE_PATH,
			"semantic_scene_id": SEMANTIC_SCENE_ID,
			"instantiated_root": String(gallery.get_path()),
			"film_camera": String(film.get_path()),
			"observer_camera": String(camera.get_path()),
			"observer_camera_transform": _transform_array(camera.global_transform),
		},
		"acquisition": {
			"status": String(film.get("CathedralSnapshotLifecycleStateName")),
			"reason": String(film.get("CathedralSnapshotLifecycleReasonName")),
			"sealed_generation": generation_before,
			"context_matching": true,
			"probe_profile": String(film.get("CathedralSealedProbeProfileStableId")),
			"formal_policy_id": String(film.get("CathedralFormalPolicyId")),
			"formal_policy_sha256": String(film.get("CathedralFormalPolicySha256")),
			"dimensions": [int(film.get("FilmWidthPixels")), int(film.get("FilmHeightRows"))],
			"processed_work_units": int(film.get("CathedralSnapshotProcessedPixelCount")),
			"raw_queries": int(film.get("CathedralRawProbeQueryCount")),
			"committed_queries": int(film.get("CathedralProbeQueryCount")),
			"work_unit_commits": int(film.get("CathedralFormalWorkUnitCommitCount")),
			"work_unit_replacements": int(film.get("CathedralFormalWorkUnitReplacementCount")),
		},
		"identities": {
			"formal_policy_sha256": String(film.get("CathedralFormalPolicySha256")),
			"query_dataset_sha256": String(film.get("CathedralProbeQueryDatasetSha256")),
			"contact_count_sha256": String(film.get("CathedralSpatialKernelContactCountSha256")),
			"outcome_sha256": String(source_hashes["outcomes.bin"]["sha256"]),
			"transport_effort_sha256": String(source_hashes["final_step_counts.bin"]["sha256"]),
			"geometry_sha256": String(film.get("CathedralSpatialKernelGeometrySnapshotSha256")),
			"spatial_context_sha256": String(film.get("CathedralSpatialAuthorityContextSha256")),
			"bvh_context_sha256": String(film.get("CathedralSpatialKernelBvhContextSha256")),
		},
		"authority": {
			"formal": String(film.get("CathedralContactAuthorityToken")),
			"linear_witness_sha256": String(provenance["linear_witness_sha256"]),
			"godot_witness_sha256": String(provenance["godot_witness_sha256"]),
			"authority_mismatch_pixels": int(film.get("CathedralSpatialKernelTotalAuthorityMismatchPixelCount")),
			"witness_mismatch_queries": int(film.get("CathedralSpatialKernelMismatchQueryCount")),
			"bvh_mismatch_queries": int(provenance["bvh_mismatch_queries"]),
			"bvh_mismatch_pixels": int(provenance["bvh_mismatch_pixels"]),
		},
		"q_mapping": {
			"forward_sequence": ["Outcome", "Contact Events", "Transport Effort"],
			"reverse_result": "Contact Events",
			"displayed_plate_rgba_sha256": displayed_hashes,
			"sealed_identity_unchanged": _sealed_identity() == sealed_identity_before,
			"generation_unchanged": int(film.get("SealedProbeViewGeneration")) == generation_before,
			"replay_invocations_during_q": int(film.get("CathedralContactReplayInvocationCount")) - replay_before,
			"formal_resets_during_q": int(film.get("FormalWorkingStateResetCount")) - reset_before,
		},
		"quick_capture": {
			"selected_q_view": "Contact Events",
			"path": String(quick_capture[0]),
			"sha256": FileAccess.get_sha256(String(quick_capture[0])),
			"clean_mode": bool(quick_capture[1]),
			"state_restored": bool(quick_capture[2]),
		},
		"portable_bundle": {
			"path": ProjectSettings.globalize_path(bundle_root),
			"artifacts": _bundle_artifact_hashes(),
			"raw_arrays_and_plates_same_generation": true,
		},
		"p13_preserved": {
			"triptych_manifest": "res://output/suspended_portal_gallery_v0/p13_16x8_1280x720/sphere_mapping_triptych/sphere_mapping_manifest.json",
			"present": FileAccess.file_exists("res://output/suspended_portal_gallery_v0/p13_16x8_1280x720/sphere_mapping_triptych/sphere_mapping_manifest.json"),
		},
	}
	var package_path := output_root.path_join("qualification_manifest.json")
	var package_file := FileAccess.open(package_path, FileAccess.WRITE)
	_assert(package_file != null, "qualification manifest writable")
	if package_file != null:
		package_file.store_string(JSON.stringify(package, "\t"))
		package_file.close()
	print("SUSPENDED_GALLERY_P14 identities policy=%s query=%s contact=%s outcome=%s effort=%s geometry=%s spatial=%s bvh=%s" % [
		package["identities"]["formal_policy_sha256"], package["identities"]["query_dataset_sha256"],
		package["identities"]["contact_count_sha256"], package["identities"]["outcome_sha256"],
		package["identities"]["transport_effort_sha256"], package["identities"]["geometry_sha256"],
		package["identities"]["spatial_context_sha256"], package["identities"]["bvh_context_sha256"]])
	print("SUSPENDED_GALLERY_P14 counts dimensions=%dx%d work_units=%d raw=%d committed=%d commits=%d replacements=%d generation=%d" % [
		int(film.get("FilmWidthPixels")), int(film.get("FilmHeightRows")), int(film.get("CathedralSnapshotProcessedPixelCount")),
		int(film.get("CathedralRawProbeQueryCount")), int(film.get("CathedralProbeQueryCount")),
		int(film.get("CathedralFormalWorkUnitCommitCount")), int(film.get("CathedralFormalWorkUnitReplacementCount")), generation_before])
	print("SUSPENDED_GALLERY_P14 Q forward=PASS reverse=PASS displayed_equals_export=PASS f10=PASS manifest=%s" % ProjectSettings.globalize_path(package_path))
	_finish()


func _write_scientific_checkpoint(manifest: Dictionary) -> void:
	var source_hashes: Dictionary = manifest["source_hashes"]
	var checkpoint := {
		"schema": "xprimeray.suspended-gallery-scientific-checkpoint.v1",
		"scene": SCENE_PATH,
		"semantic_scene_id": SEMANTIC_SCENE_ID,
		"status": String(film.get("CathedralSnapshotLifecycleStateName")),
		"sealed_generation": int(film.get("SealedProbeViewGeneration")),
		"dimensions": [int(film.get("FilmWidthPixels")), int(film.get("FilmHeightRows"))],
		"processed_work_units": int(film.get("CathedralSnapshotProcessedPixelCount")),
		"raw_queries": int(film.get("CathedralRawProbeQueryCount")),
		"committed_queries": int(film.get("CathedralProbeQueryCount")),
		"work_unit_commits": int(film.get("CathedralFormalWorkUnitCommitCount")),
		"work_unit_replacements": int(film.get("CathedralFormalWorkUnitReplacementCount")),
		"formal_policy_sha256": String(film.get("CathedralFormalPolicySha256")),
		"query_dataset_sha256": String(film.get("CathedralProbeQueryDatasetSha256")),
		"contact_count_sha256": String(film.get("CathedralSpatialKernelContactCountSha256")),
		"outcome_sha256": String(source_hashes["outcomes.bin"]["sha256"]),
		"transport_effort_sha256": String(source_hashes["final_step_counts.bin"]["sha256"]),
		"geometry_sha256": String(film.get("CathedralSpatialKernelGeometrySnapshotSha256")),
		"spatial_context_sha256": String(film.get("CathedralSpatialAuthorityContextSha256")),
		"bvh_context_sha256": String(film.get("CathedralSpatialKernelBvhContextSha256")),
		"intersection_policy": String(film.get("CathedralSpatialKernelIntersectionPolicyVersion")),
		"primitive_count": int(film.get("CathedralSpatialKernelPrimitiveCount")),
		"authority": String(film.get("CathedralContactAuthorityToken")),
		"authority_mismatch_pixels": int(film.get("CathedralSpatialKernelTotalAuthorityMismatchPixelCount")),
		"godot_query_comparisons": int(film.get("CathedralSpatialKernelGodotQueryComparisonCount")),
		"godot_linear_mismatch_queries": int(film.get("CathedralSpatialKernelMismatchQueryCount")),
	}
	var path := output_root.path_join("scientific_checkpoint.json")
	var file := FileAccess.open(path, FileAccess.WRITE)
	_assert(file != null, "scientific checkpoint writable")
	if file != null:
		file.store_string(JSON.stringify(checkpoint, "\t"))
		file.close()


func _wait_for_formal_complete() -> bool:
	for _frame in FORMAL_TIMEOUT_FRAMES:
		await process_frame
		if bool(film.get("ProbeViewAvailable")) and String(film.get("CathedralSnapshotLifecycleStateName")) == "complete":
			return true
		if bool(film.get("CathedralProbeSnapshotIsTerminal")) and String(film.get("CathedralSnapshotLifecycleStateName")) != "complete":
			break
	_assert(false, "gallery Formal timeout/terminal failure state=%s reason=%s processed=%d/%d" % [
		film.get("CathedralSnapshotLifecycleStateName"), film.get("CathedralSnapshotLifecycleReasonName"),
		film.get("CathedralSnapshotProcessedPixelCount"), film.get("CathedralSnapshotTotalPixelCount")])
	return false


func _write_blocked_checkpoint() -> void:
	var divergence_json := String(film.get("CathedralSpatialKernelQueryDivergenceJson"))
	var divergence_data = JSON.parse_string(divergence_json)
	_assert(divergence_data is Dictionary, "query divergence attribution JSON parsed")
	var divergence_file := FileAccess.open(divergence_path, FileAccess.WRITE)
	_assert(divergence_file != null, "query divergence attribution file writable")
	if divergence_file != null:
		divergence_file.store_string(divergence_json)
		divergence_file.close()
	var divergence_count := -1
	if divergence_data is Dictionary:
		divergence_count = int(divergence_data.get("divergence_count", -1))
	_assert(divergence_count == int(film.get("CathedralSpatialKernelMismatchQueryCount")), "query divergence record count matches validator")
	var checkpoint := {
		"schema": "xprimeray.suspended-portal-gallery-q-blocked.v1",
		"status": "HOLD",
		"scene": {
			"path": SCENE_PATH,
			"semantic_scene_id": SEMANTIC_SCENE_ID,
			"film_camera": String(film.get_path()),
			"observer_camera": String(camera.get_path()),
			"observer_camera_transform": _transform_array(camera.global_transform),
		},
		"formal_attempt": {
			"lifecycle_state": String(film.get("CathedralSnapshotLifecycleStateName")),
			"lifecycle_reason": String(film.get("CathedralSnapshotLifecycleReasonName")),
			"processed_pixels": int(film.get("CathedralSnapshotProcessedPixelCount")),
			"total_pixels": int(film.get("CathedralSnapshotTotalPixelCount")),
			"raw_queries": int(film.get("CathedralRawProbeQueryCount")),
			"committed_queries": int(film.get("CathedralProbeQueryCount")),
			"work_unit_commits": int(film.get("CathedralFormalWorkUnitCommitCount")),
			"work_unit_replacements": int(film.get("CathedralFormalWorkUnitReplacementCount")),
			"formal_policy_id": String(film.get("CathedralFormalPolicyId")),
			"formal_policy_sha256": String(film.get("CathedralFormalPolicySha256")),
			"geometry_sha256": String(film.get("CathedralSpatialKernelGeometrySnapshotSha256")),
			"spatial_context_sha256": String(film.get("CathedralSpatialAuthorityContextSha256")),
			"linear_context_sha256": String(film.get("CathedralSpatialKernelContextSha256")),
			"bvh_context_sha256": String(film.get("CathedralSpatialKernelBvhContextSha256")),
			"query_dataset_sha256": String(film.get("CathedralProbeQueryDatasetSha256")),
			"linear_contact_sha256": String(film.get("CathedralSpatialKernelContactCountSha256")),
		},
		"authority_gate": {
			"promotion_ready": bool(film.get("CathedralSpatialAuthorityPromotionReady")),
			"sealed_probe_view_available": bool(film.get("ProbeViewAvailable")),
			"total_mismatch_pixels": int(film.get("CathedralSpatialKernelTotalAuthorityMismatchPixelCount")),
			"contact_count_mismatch_pixels": int(film.get("CathedralSpatialKernelContactCountMismatchPixelCount")),
			"surface_semantic_mismatch_pixels": int(film.get("CathedralSpatialKernelSurfaceSemanticMismatchPixelCount")),
			"mismatch_queries": int(film.get("CathedralSpatialKernelMismatchQueryCount")),
			"conclusion": "Formal seal and portable export correctly refused because Godot replay and frozen spatial geometry are not authority-equivalent.",
		},
		"query_divergence_attribution": {
			"path": divergence_path,
			"sha256": FileAccess.get_sha256(divergence_path),
			"records": divergence_count,
			"ordinary_log_records": 0,
		},
		"suspected_scene_seam": {
			"node": "/root/SuspendedPortalGallery/SuspendedPortalGalleryWorld/DiagnosticGeometryZoo/ReferenceSphere",
			"godot_collision_active": true,
			"spatial_kernel_excluded_group": true,
			"note": "Confirm with a separately authorized geometry-ownership investigation; P14 does not alter collision or Formal membership.",
		},
	}
	var path := output_root.path_join("blocked_checkpoint.json")
	var file := FileAccess.open(path, FileAccess.WRITE)
	_assert(file != null, "blocked checkpoint writable")
	if file != null:
		file.store_string(JSON.stringify(checkpoint, "\t"))
		file.close()
	print("SUSPENDED_GALLERY_P14 HOLD state=%s reason=%s mismatches=%d queries=%d attribution=%s checkpoint=%s" % [
		checkpoint["formal_attempt"]["lifecycle_state"], checkpoint["formal_attempt"]["lifecycle_reason"],
		checkpoint["authority_gate"]["total_mismatch_pixels"], checkpoint["authority_gate"]["mismatch_queries"],
		ProjectSettings.globalize_path(divergence_path),
		ProjectSettings.globalize_path(path)])


func _capture_selected_q_view() -> Array:
	var event := InputEventKey.new()
	event.keycode = KEY_F10
	event.physical_keycode = KEY_F10
	event.pressed = true
	Input.parse_input_event(event)
	var result: Array = await gallery.screenshot_captured
	event.pressed = false
	Input.parse_input_event(event)
	_assert(not String(result[0]).is_empty(), "F10 selected Q capture path")
	_assert(FileAccess.file_exists(String(result[0])), "F10 selected Q capture exists")
	_assert(not bool(result[1]), "plain F10 unexpectedly used beauty mode")
	_assert(bool(result[2]), "F10 selected Q capture did not restore state")
	return result


func _capture_plate_stages(stem: String, manifest_key: String, manifest: Dictionary) -> Dictionary:
	var canonical_path := bundle_root.path_join(stem + ".png")
	var canonical := Image.load_from_file(ProjectSettings.globalize_path(canonical_path))
	_assert(canonical != null, stem + " canonical PNG loads")
	if canonical == null:
		return {}
	var canonical_original_format := int(canonical.get_format())
	if canonical.get_format() != Image.FORMAT_RGBA8:
		canonical.convert(Image.FORMAT_RGBA8)
	var expected := canonical.get_data()
	var expected_hash := _sha256_bytes(expected)
	var manifest_hash := String(manifest["mapped_hashes"]["plate_rgba"][manifest_key]["sha256"])
	_assert(expected_hash == manifest_hash, stem + " decoded canonical PNG differs from exporter RGBA hash")
	_assert(canonical.get_width() == 80 and canonical.get_height() == 45, stem + " canonical dimensions are not 80x45")
	_assert(_all_alpha_opaque(expected), stem + " canonical alpha is not 255")

	var flipped := canonical.duplicate()
	flipped.flip_y()
	var flipped_bytes: PackedByteArray = flipped.get_data()
	var orientation_asymmetric := _sha256_bytes(flipped_bytes) != expected_hash
	_assert(orientation_asymmetric, stem + " canonical plate is not vertically asymmetric")

	var cpu_image: Image = film.call("CopyFilmImageForTesting") as Image
	_assert(cpu_image != null, stem + " CPU film image is available")
	var cpu_original_format := -1
	var cpu_bytes := PackedByteArray()
	if cpu_image != null:
		cpu_original_format = int(cpu_image.get_format())
		if cpu_image.get_format() != Image.FORMAT_RGBA8:
			cpu_image.convert(Image.FORMAT_RGBA8)
		cpu_bytes = cpu_image.get_data()
	_assert(cpu_image != null and cpu_image.get_width() == 80 and cpu_image.get_height() == 45, stem + " CPU dimensions are not 80x45")
	var cpu_comparison := _compare_rgba(expected, cpu_bytes, flipped_bytes, 80)
	_assert(bool(cpu_comparison["equal"]), stem + " exporter RGBA differs from CPU film image")

	var gpu_supported := DisplayServer.get_name() != "headless"
	var gpu_image: Image = null
	var gpu_original_format := -1
	var gpu_bytes := PackedByteArray()
	if gpu_supported:
		await RenderingServer.frame_post_draw
		_assert(film_view != null and film_view.texture != null, stem + " FilmView texture is available")
		gpu_image = film_view.texture.get_image() if film_view != null and film_view.texture != null else null
		_assert(gpu_image != null, stem + " synchronized texture image is available")
	if gpu_supported and gpu_image != null:
		gpu_original_format = int(gpu_image.get_format())
		if gpu_image.get_format() != Image.FORMAT_RGBA8:
			gpu_image.convert(Image.FORMAT_RGBA8)
		gpu_bytes = gpu_image.get_data()
	if gpu_supported:
		_assert(gpu_image != null and gpu_image.get_width() == 80 and gpu_image.get_height() == 45, stem + " GPU dimensions are not 80x45")
	var gpu_comparison := _compare_rgba(expected, gpu_bytes, flipped_bytes, 80)
	if gpu_supported:
		_assert(bool(gpu_comparison["equal"]), stem + " synchronized texture differs from exporter RGBA")
	var texture_instance := int(film_view.texture.get_instance_id()) if film_view != null and film_view.texture != null else 0
	var texture_rid := int(film_view.texture.get_rid().get_id()) if film_view != null and film_view.texture != null else 0
	_assert(texture_instance == int(film.get("FilmTextureInstanceIdForTesting")), stem + " FilmView does not reference the film texture")

	return {
		"mode": stem,
		"orientation_asymmetric": orientation_asymmetric,
		"exporter": {
			"width": canonical.get_width(), "height": canonical.get_height(),
			"original_format": canonical_original_format, "rgba8_format": int(canonical.get_format()),
			"bytes": expected.size(), "sha256": expected_hash, "alpha_255": _all_alpha_opaque(expected),
			"manifest_sha256": manifest_hash,
		},
		"cpu": {
			"width": cpu_image.get_width() if cpu_image != null else 0, "height": cpu_image.get_height() if cpu_image != null else 0,
			"original_format": cpu_original_format, "rgba8_format": int(cpu_image.get_format()) if cpu_image != null else -1,
			"bytes": cpu_bytes.size(), "sha256": _sha256_bytes(cpu_bytes),
			"comparison": cpu_comparison,
		},
		"gpu": {
			"supported": gpu_supported,
			"unsupported_reason": "headless_display_server_has_no_frame_post_draw" if not gpu_supported else "",
			"width": gpu_image.get_width() if gpu_image != null else 0, "height": gpu_image.get_height() if gpu_image != null else 0,
			"original_format": gpu_original_format, "rgba8_format": int(gpu_image.get_format()) if gpu_image != null else -1,
			"bytes": gpu_bytes.size(), "sha256": _sha256_bytes(gpu_bytes) if not gpu_bytes.is_empty() else "",
			"comparison": gpu_comparison, "synchronization": "RenderingServer.frame_post_draw",
			"texture_instance_id": texture_instance, "texture_rid_id": texture_rid,
		},
	}


func _available_readback_hash(comparison: Dictionary) -> String:
	if bool(comparison["gpu"]["supported"]):
		return String(comparison["gpu"]["sha256"])
	return String(comparison["cpu"]["sha256"])


func _compare_rgba(expected: PackedByteArray, actual: PackedByteArray, flipped_expected: PackedByteArray, width: int) -> Dictionary:
	var first_byte := -1
	var limit := mini(expected.size(), actual.size())
	for index in limit:
		if expected[index] != actual[index]:
			first_byte = index
			break
	if first_byte < 0 and expected.size() != actual.size():
		first_byte = limit
	var pixel := first_byte / 4 if first_byte >= 0 else -1
	var base := pixel * 4 if pixel >= 0 else -1
	return {
		"equal": expected == actual,
		"vertical_flip_equal": flipped_expected == actual,
		"first_differing_byte": first_byte,
		"first_differing_pixel": pixel,
		"x": pixel % width if pixel >= 0 else -1,
		"y": pixel / width if pixel >= 0 else -1,
		"expected_rgba": _rgba_at(expected, base),
		"actual_rgba": _rgba_at(actual, base),
	}


func _rgba_at(bytes: PackedByteArray, base: int) -> Array:
	if base < 0 or base + 3 >= bytes.size():
		return []
	return [bytes[base], bytes[base + 1], bytes[base + 2], bytes[base + 3]]


func _all_alpha_opaque(bytes: PackedByteArray) -> bool:
	for index in range(3, bytes.size(), 4):
		if bytes[index] != 255:
			return false
	return true


func _sha256_bytes(bytes: PackedByteArray) -> String:
	var hasher := HashingContext.new()
	_assert(hasher.start(HashingContext.HASH_SHA256) == OK, "RGBA hashing starts")
	hasher.update(bytes)
	return hasher.finish().hex_encode()


func _write_plate_comparison_report(comparisons: Dictionary, identity_before: String, replay_before: int, reset_before: int, generation_before: int) -> void:
	var report := {
		"schema": "xprimeray.plate-readback-attribution.v1",
		"scene": SCENE_PATH,
		"semantic_scene_id": SEMANTIC_SCENE_ID,
		"comparisons": comparisons,
		"sealed_state": {
			"identity_unchanged": _sealed_identity() == identity_before,
			"generation_before": generation_before,
			"generation_after": int(film.get("SealedProbeViewGeneration")),
			"replay_delta": int(film.get("CathedralContactReplayInvocationCount")) - replay_before,
			"reset_delta": int(film.get("FormalWorkingStateResetCount")) - reset_before,
			"lifecycle": String(film.get("CathedralSnapshotLifecycleStateName")),
		},
		"failures": failures,
	}
	var path := output_root.path_join("plate_readback_attribution.json")
	var file := FileAccess.open(path, FileAccess.WRITE)
	_assert(file != null, "plate comparison report writable")
	if file != null:
		file.store_string(JSON.stringify(report, "\t") + "\n")


func _sealed_identity() -> String:
	return JSON.stringify({
		"generation": int(film.get("SealedProbeViewGeneration")),
		"query": String(film.get("CathedralProbeQueryDatasetSha256")),
		"contact": String(film.get("CathedralSpatialKernelContactCountSha256")),
		"geometry": String(film.get("CathedralSpatialKernelGeometrySnapshotSha256")),
		"spatial": String(film.get("CathedralSpatialAuthorityContextSha256")),
		"bvh": String(film.get("CathedralSpatialKernelBvhContextSha256")),
		"policy": String(film.get("CathedralFormalPolicySha256")),
		"profile": String(film.get("CathedralSealedProbeProfileStableId")),
		"authority": String(film.get("CathedralContactAuthorityToken")),
	})


func _source_tree_state() -> String:
	var value := OS.get_environment("XPRIMERAY_SOURCE_TREE_STATE")
	_assert(value == "clean" or value == "dirty", "explicit XPRIMERAY_SOURCE_TREE_STATE is required")
	return value


func _authored_diff_sha256() -> String:
	var value := OS.get_environment("XPRIMERAY_AUTHORED_DIFF_SHA256")
	if _source_tree_state() == "dirty":
		_assert(value.length() == 64, "dirty source tree requires XPRIMERAY_AUTHORED_DIFF_SHA256")
	return value


func _bundle_artifact_hashes() -> Dictionary:
	var hashes := {}
	var directory := DirAccess.open(bundle_root)
	_assert(directory != null, "portable bundle directory readable")
	if directory == null:
		return hashes
	var names := directory.get_files()
	names.sort()
	for name in names:
		var path := bundle_root.path_join(name)
		hashes[name] = {
			"bytes": FileAccess.get_file_as_bytes(path).size(),
			"sha256": FileAccess.get_sha256(path),
		}
	return hashes


func _transform_array(value: Transform3D) -> Array:
	return [
		value.origin.x, value.origin.y, value.origin.z,
		value.basis.x.x, value.basis.y.x, value.basis.z.x,
		value.basis.x.y, value.basis.y.y, value.basis.z.y,
		value.basis.x.z, value.basis.y.z, value.basis.z.z,
	]


func _dispatch_key(code: Key, shift_pressed := false) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	event.echo = false
	event.shift_pressed = shift_pressed
	Input.parse_input_event(event)
	await _settle(3)
	event.pressed = false
	Input.parse_input_event(event)
	await process_frame


func _settle(frames: int) -> void:
	for _frame in frames:
		await process_frame


func _assert(condition: bool, message: String) -> void:
	if condition:
		return
	failures.append(message)
	push_error("SUSPENDED_GALLERY_P14 FAIL: " + message)


func _finish() -> void:
	if failures.is_empty():
		print("SUSPENDED_GALLERY_P14 PASS output=%s" % ProjectSettings.globalize_path(output_root))
		quit(0)
	else:
		print("SUSPENDED_GALLERY_P14 FAIL count=%d output=%s" % [failures.size(), ProjectSettings.globalize_path(output_root)])
		for failure in failures:
			print("SUSPENDED_GALLERY_P14 FAILURE " + failure)
		quit(1)
