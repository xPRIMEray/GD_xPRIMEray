extends SceneTree

const GALLERY_SCENE := preload("res://Experiences/SuspendedPortalGallery.tscn")
const FORMAL_TIMEOUT_FRAMES := 1200
const EXPECTED_VIEW_HASHES := {
	0: "3b12cfb0b7582f532a801f99a9b340bbc1239514c811bd8ec2974ea04f3086d2",
	1: "4be6e2e286b3ddd047a559e2b0aa41f90b4d0bff173ae6844ba7160f29fdd3c2",
	2: "2d4852190ec55d88f11b673a4d561ec0c9d11991099ca43edf3ff61ed6879b2b",
}

var gallery: Node
var world: Node
var film: Node
var controller: Node
var player: Node
var card: Control
var presentation: Control
var plate: Control
var view: TextureRect
var failures: Array[String] = []
var max_card_height := 0.0
var min_clearance := INF


func _initialize() -> void:
	await _new_gallery()
	_test_hierarchy_and_authority()
	await _test_levels_off_and_live()
	await _test_formal_and_sealed_views()
	await _test_context_invalidation()
	await _test_boundary_failure()
	await _test_escape_round_trip()
	_finish()


func _new_gallery() -> void:
	if gallery != null and is_instance_valid(gallery):
		gallery.queue_free()
		await process_frame
	gallery = GALLERY_SCENE.instantiate()
	get_root().add_child(gallery)
	await _settle(12)
	world = gallery.get_node("SuspendedPortalGalleryWorld")
	film = world.get_node("GrinFilmCamera")
	controller = world.get_node("FilmController")
	player = world.get_node("TransportChamberPlayer")
	card = world.get_node("CanvasLayer/HUDRoot/InstrumentStatusCard")
	presentation = world.get_node("CanvasLayer/HUDRoot/FilmPresentation")
	plate = presentation.get_node("FilmPlate")
	view = plate.get_node("FilmView")
	controller.call("SetInputEnabled", true)


func _test_hierarchy_and_authority() -> void:
	_assert(card.get("scene_label") == "GALLERY", "shared card scene label")
	_assert(NodePath(card.get("film_camera_path")) == NodePath("../../../GrinFilmCamera"), "shared card camera binding")
	_assert(NodePath(card.get("film_controller_path")) == NodePath("../../../FilmController"), "shared card controller binding")
	_assert(NodePath(card.get("film_plate_path")) == NodePath("../FilmPresentation/FilmPlate"), "shared card plate binding")
	_assert(NodePath(card.get("player_path")) == NodePath("../../../TransportChamberPlayer"), "shared card player binding")
	_assert(NodePath(film.get("FilmViewPath")) == NodePath("../CanvasLayer/HUDRoot/FilmPresentation/FilmPlate/FilmView"), "camera FilmView relocation")
	_assert(NodePath(film.get("FilmOverlayPath")) == NodePath("../CanvasLayer/HUDRoot/FilmPresentation/FilmPlate/FilmOverlay"), "camera overlay relocation")
	_assert(NodePath(controller.get("film_plate_path")) == NodePath("../CanvasLayer/HUDRoot/FilmPresentation/FilmPlate"), "controller plate relocation")
	_assert(NodePath(controller.get("film_view_path")) == NodePath("../CanvasLayer/HUDRoot/FilmPresentation/FilmPlate/FilmView"), "controller view relocation")
	_assert(NodePath(controller.get("film_overlay_path")) == NodePath("../CanvasLayer/HUDRoot/FilmPresentation/FilmPlate/FilmOverlay"), "controller overlay relocation")
	_assert(presentation.mouse_filter == Control.MOUSE_FILTER_IGNORE, "presentation wrapper intercepts input")
	_assert(_legacy_probe_hud_count() == 1 and not _legacy_probe_hud_visible(), "legacy layer-12 Probe HUD remains visible")
	var source := FileAccess.get_file_as_string("res://Experiences/SuspendedPortalGalleryController.gd")
	for duplicate in ["Terminal semantic outcome", "Accepted per-step contacts", "Numerical step budget used", "probe_modes", "probe_legends"]:
		_assert(source.find(duplicate) < 0, "duplicated Q vocabulary remains: " + duplicate)
	_assert(source.find("event.keycode == KEY_F:") < 0, "dead Gallery F input handler remains")


func _test_levels_off_and_live() -> void:
	_assert(not bool(gallery.get("_full_hud")), "fresh Gallery is not Explore")
	_assert(not presentation.visible, "Explore presentation wrapper visible")
	_assert(int(card.call("GetDisplayedStateForTesting").get("level")) == 0, "Explore card level")
	await _sample_layout("off_explore", 0, "OFF", "inactive", false)
	await _dispatch_key(KEY_H)
	_assert(bool(gallery.get("_full_hud")) and presentation.visible, "H did not enter Instrument")
	_assert(int(card.call("GetDisplayedStateForTesting").get("level")) == 1, "Instrument card level")
	card.call("ForceRefreshForTesting")
	var card_state: Dictionary = card.call("GetDisplayedStateForTesting")
	_assert(not bool(card_state.get("secondary_controls_visible")), "Gallery duplicated its secondary truth-panel reference in the shared card")
	var truth_text := String((world.get_node("CanvasLayer/HUDRoot/TruthBoundary").get_child(0) as Label).text)
	for token in ["P Profile", "G Film", "Q Formal/View", "N LIVE Shading", "F Field", "R Rings", "L Labels", "Esc Workbench", "F10 Capture", "Shift+F10 Beauty", "Disabled: O Optical-only", "F1/F2 renderer debug", "F6-F9 legacy macros"]:
		_assert(truth_text.contains(token), "Gallery operator reference missing %s" % token)
	await _settle(2)
	_assert_reference_lines_unwrapped("off_instrument")
	_assert(String(card_state.get("q_readiness")) == "ready" and String(card_state.get("status_text")).contains("Q Formal: ready"), "OFF did not present Q Formal as ready")
	await _sample_layout("off_instrument", 1, "OFF", "inactive", false)
	await _dispatch_key(KEY_H)
	await _dispatch_key(KEY_G)
	_assert(String(controller.call("GetModeName")) == "LIVE", "G did not enter LIVE")
	var live_config := _direct_configuration()
	for _index in range(20):
		await _dispatch_key(KEY_H)
		card.call("ForceRefreshForTesting")
	_assert(_direct_configuration() == live_config, "presentation changes altered 52-field LIVE configuration")
	_assert(not _legacy_probe_hud_visible(), "presentation changes resurrected legacy Probe HUD")
	if not bool(gallery.get("_full_hud")):
		await _dispatch_key(KEY_H)
	await _sample_layout("live_instrument", 1, "LIVE", "inactive", true)
	_assert_reference_lines_unwrapped("live_instrument")
	card.call("ForceRefreshForTesting")
	var live_state: Dictionary = card.call("GetDisplayedStateForTesting")
	_assert(String(live_state.get("q_readiness")) == "G OFF first" and String(live_state.get("status_text")).contains("Q Formal: G OFF first"), "LIVE presented Q Formal as ready")
	_assert(not String(live_state.get("status_text")).contains("Q Formal: ready"), "LIVE retained Q Formal ready")
	var generation_before_live_q := int(film.get("CathedralSnapshotGeneration"))
	await _dispatch_key(KEY_Q)
	_assert(String(controller.call("GetModeName")) == "LIVE" and String(controller.call("GetLastInputDisposition")) == "formal_from_live_refused_press_g_then_q", "Q in LIVE was not refused as the card states")
	_assert(int(film.get("CathedralSnapshotGeneration")) == generation_before_live_q, "refused LIVE Q changed Formal generation")
	await _dispatch_key(KEY_G)
	_assert(String(controller.call("GetModeName")) == "OFF", "G did not return LIVE to OFF")


func _test_formal_and_sealed_views() -> void:
	if bool(gallery.get("_full_hud")):
		await _dispatch_key(KEY_H)
	_assert(bool(controller.call("EnterFormalSnapshotFromInput")), "Formal request rejected")
	_assert(bool(player.call("IsPoseHeld")), "active Formal pose not held")
	for _frame in range(120):
		await process_frame
		if String(film.get("CathedralSnapshotLifecycleStateName")) == "capturing":
			break
	_assert(String(film.get("CathedralSnapshotLifecycleStateName")) == "capturing", "Formal acquisition did not initialize")
	var authority_before := _authority_signature()
	for _frame in range(FORMAL_TIMEOUT_FRAMES):
		if bool(film.get("ProbeViewAvailable")):
			break
		await _toggle_h_one_frame()
	_assert(bool(film.get("ProbeViewAvailable")), "Formal acquisition did not seal")
	_assert(bool(controller.get("_formal_snapshot_terminal_handled")), "terminal handler did not latch")
	_assert(plate.visible and view.texture != null, "sealed first view was not painted")
	var authority_after := _authority_signature()
	_assert(_scientific_authority_equal_except_terminal(authority_before, authority_after), "presentation toggles changed active acquisition authority before=%s after=%s" % [JSON.stringify(authority_before), JSON.stringify(authority_after)])
	if bool(gallery.get("_full_hud")):
		await _dispatch_key(KEY_H)
	card.call("ForceRefreshForTesting")
	_assert(plate.visible and not plate.is_visible_in_tree(), "Explore did not suppress effective sealed plate visibility")
	_assert(bool(card.call("GetDisplayedStateForTesting").get("plate_hidden")), "shared recovery hint absent")
	_assert(_film_hash() == EXPECTED_VIEW_HASHES[0], "Outcome CPU bytes differ from retained baseline")
	await _dispatch_key(KEY_H)
	_assert(plate.is_visible_in_tree(), "H did not reveal existing sealed image")
	await _sample_layout("sealed_outcome", 1, "SNAPSHOT", "complete", true)
	var hashes := {}
	for expected_view in [0, 1, 2]:
		_assert(int(film.get("CurrentProbeView")) == expected_view, "unexpected forward Q order")
		card.call("ForceRefreshForTesting")
		var state: Dictionary = card.call("GetDisplayedStateForTesting")
		_assert(String(state.get("q_view")) == String(film.get("CurrentProbeViewDisplayName")), "authoritative Q name mismatch")
		_assert(String(state.get("q_description")) == String(film.get("CurrentProbeViewDescription")), "authoritative Q description mismatch")
		_assert(String(state.get("q_legend")) == String(film.get("CurrentProbeViewLegend")), "authoritative Q legend mismatch")
		_assert(String(state.get("status_text")).contains("N LIVE shading:") and String(state.get("status_text")).contains("Q image unchanged"), "Gallery sealed SNAPSHOT does not distinguish LIVE shading from Q image")
		hashes[expected_view] = _film_hash()
		_assert(hashes[expected_view] == EXPECTED_VIEW_HASHES[expected_view], "sealed Q CPU hash mismatch view=%d" % expected_view)
		await _sample_layout("sealed_q%d_instrument" % expected_view, 1, "SNAPSHOT", "complete", true)
		await _dispatch_key(KEY_H)
		_assert(_film_hash() == hashes[expected_view], "presentation level changed sealed CPU bytes view=%d" % expected_view)
		await _sample_layout("sealed_q%d_explore" % expected_view, 0, "SNAPSHOT", "complete", false)
		await _dispatch_key(KEY_H)
		if expected_view < 2:
			await _dispatch_key(KEY_Q)
	await _dispatch_key(KEY_Q, true)
	_assert(int(film.get("CurrentProbeView")) == 1, "reverse Q did not select Contact Events")
	_assert(_film_hash() == EXPECTED_VIEW_HASHES[1], "reverse Contact Events CPU bytes differ")
	await _dispatch_key(KEY_P)
	card.call("ForceRefreshForTesting")
	var profile_state: Dictionary = card.call("GetDisplayedStateForTesting")
	_assert(String(profile_state.get("selected_profile_id")) == "motion", "P did not select Motion")
	_assert(String(profile_state.get("sealed_profile_id")) == "cathedral", "sealed Cathedral provenance was relabeled")
	await _sample_layout("motion_selected_cathedral_sealed", 1, "SNAPSHOT", "complete", true)
	var state_before := _complete_state_signature()
	var refresh_before := int(card.call("GetRefreshCountForTesting"))
	for _refresh in range(20):
		card.call("ForceRefreshForTesting")
	_assert(int(card.call("GetRefreshCountForTesting")) == refresh_before + 20, "twenty shared-card refreshes not observed")
	_assert(_complete_state_signature() == state_before, "shared-card refresh changed authority")
	_assert(not _legacy_probe_hud_visible(), "sealed presentation resurrected legacy Probe HUD")
	if DisplayServer.get_name() != "headless":
		var level_before := bool(gallery.get("_full_hud"))
		gallery.call("_capture_current_viewport", true)
		for _wait in range(600):
			await process_frame
			if not bool(gallery.get("_capture_in_progress")):
				break
		_assert(bool(gallery.get("last_capture_state_restored")), "beauty capture did not restore state")
		_assert(bool(gallery.get("_full_hud")) == level_before, "beauty capture changed presentation level")
		_assert(not _legacy_probe_hud_visible(), "beauty capture resurrected legacy Probe HUD")


func _test_context_invalidation() -> void:
	await _new_gallery()
	_assert(bool(controller.call("EnterFormalSnapshotFromInput")), "context invalidation request rejected")
	await process_frame
	film.call("InvalidateCathedralProbeSnapshotForContextChange")
	await _settle(3)
	card.call("ForceRefreshForTesting")
	var state: Dictionary = card.call("GetDisplayedStateForTesting")
	_assert(String(film.get("CathedralSnapshotLifecycleStateName")) == "invalidated", "context change did not invalidate")
	_assert(String(film.get("CathedralSnapshotLifecycleReasonName")).to_lower() == "context_changed", "context invalidation reason")
	_assert(bool(state.get("formal_attention")), "shared card did not expose invalidation")
	_assert(not bool(film.get("ProbeViewAvailable")), "invalidation exposed a seal")
	await _sample_layout("invalidated", 0, "SNAPSHOT", "invalidated", false)


func _test_boundary_failure() -> void:
	await _new_gallery()
	_assert(bool(controller.call("EnterFormalSnapshotFromInput")), "boundary failure request rejected")
	for _frame in range(120):
		await process_frame
		if String(film.get("CathedralSnapshotLifecycleStateName")) == "capturing":
			break
	var boundary := world.get_node("SuspendedPortalBoundary")
	var original_ior := float(boundary.get("IOR"))
	boundary.set("IOR", original_ior + 0.125)
	for _frame in range(FORMAL_TIMEOUT_FRAMES):
		await process_frame
		if String(film.get("CathedralSnapshotLifecycleStateName")) in ["failed", "complete"]:
			break
	boundary.set("IOR", original_ior)
	card.call("ForceRefreshForTesting")
	var state: Dictionary = card.call("GetDisplayedStateForTesting")
	_assert(String(film.get("CathedralSnapshotLifecycleStateName")) == "failed", "boundary mutation did not fail")
	_assert(String(film.get("CathedralSnapshotLifecycleReasonName")) == "boundary_epoch_changed", "boundary failure reason")
	_assert(not bool(film.get("ProbeViewAvailable")), "boundary failure exposed a seal")
	_assert(bool(state.get("formal_attention")), "shared card did not expose boundary failure")
	await _sample_layout("boundary_failed", 0, "SNAPSHOT", "failed", false)
	_assert(bool(controller.call("ToggleLiveFromInput")), "voluntary failed-SNAPSHOT release rejected")
	await _settle(3)
	card.call("ForceRefreshForTesting")
	_assert(not bool(card.call("GetDisplayedStateForTesting").get("formal_attention")), "voluntary G release left stale Formal warning")


func _test_escape_round_trip() -> void:
	await _new_gallery()
	current_scene = gallery
	_assert(bool(controller.call("EnterFormalSnapshotFromInput")), "round-trip active request rejected")
	await process_frame
	await _dispatch_key(KEY_ESCAPE)
	for _frame in range(120):
		await process_frame
		if current_scene != null and current_scene.scene_file_path == "res://ObservatoryWorkbench.tscn":
			break
	_assert(current_scene != null and current_scene.scene_file_path == "res://ObservatoryWorkbench.tscn", "Esc did not return to Workbench")
	if current_scene != null:
		var camera_count := 0
		for node in current_scene.find_children("*", "Node", true, false):
			if node.name == "GrinFilmCamera":
				camera_count += 1
		_assert(camera_count == 1, "Workbench round trip did not create exactly one fresh camera")


func _sample_layout(label: String, expected_level: int, expected_mode: String, expected_lifecycle: String, expected_plate_visible: bool) -> void:
	await process_frame
	await process_frame
	card.call("ForceRefreshForTesting")
	var state: Dictionary = card.call("GetDisplayedStateForTesting")
	_assert(int(state.get("level")) == expected_level, "%s presentation level attribution" % label)
	_assert(String(controller.call("GetModeName")) == expected_mode, "%s mode attribution" % label)
	_assert(String(film.get("CathedralSnapshotLifecycleStateName")) == expected_lifecycle, "%s lifecycle attribution" % label)
	_assert(plate.is_visible_in_tree() == expected_plate_visible, "%s effective plate visibility attribution" % label)
	var truth := world.get_node("CanvasLayer/HUDRoot/TruthBoundary") as Control
	var card_rect := card.get_global_rect()
	var truth_rect := truth.get_global_rect()
	var plate_rect := plate.get_global_rect()
	var card_ratio := card_rect.size.x * card_rect.size.y / (390.0 * 248.0)
	var truth_ratio := truth_rect.size.x * truth_rect.size.y / (377.0 * 221.0)
	max_card_height = maxf(max_card_height, card_rect.size.y)
	min_clearance = minf(min_clearance, plate_rect.position.x - card_rect.end.x)
	_assert(not truth_rect.intersects(card_rect), "%s truth/card overlap" % label)
	_assert(not card_rect.intersects(plate_rect), "%s card/film overlap" % label)
	if expected_level == 0:
		_assert(card_ratio >= 0.45 and card_ratio <= 0.60, "%s Explore card ratio %.4f" % [label, card_ratio])
		_assert(truth_ratio >= 0.40 and truth_ratio <= 0.50, "%s Explore truth ratio %.4f" % [label, truth_ratio])
	var capture_dir := OS.get_environment("XPRIMERAY_LAYOUT_CAPTURE_DIR")
	if not capture_dir.is_empty() and DisplayServer.get_name() != "headless":
		DirAccess.make_dir_recursive_absolute(capture_dir)
		var image := get_root().get_texture().get_image()
		var physical := DisplayServer.window_get_size()
		_assert(image.save_png(capture_dir.path_join("%dx%d_%s.png" % [physical.x, physical.y, label])) == OK, "layout capture failed")


func _assert_reference_lines_unwrapped(label: String) -> void:
	# UI-R2A.1: no Instrument reference line may wrap, so a key can never be
	# separated from its action (formerly "... | H" / "Explore").
	var truth_label := world.get_node("CanvasLayer/HUDRoot/TruthBoundary").get_child(0) as Label
	var logical_lines := truth_label.text.split("\n")
	var font := truth_label.get_theme_font("font")
	var font_size := truth_label.get_theme_font_size("font_size")
	var available := truth_label.size.x
	var style := truth_label.get_theme_stylebox("normal")
	if style != null:
		available -= style.get_minimum_size().x
	for line in logical_lines:
		var width := font.get_string_size(line, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
		_assert(width <= available, "%s reference line wraps (%.1f > %.1f): %s" % [label, width, available, line])
	_assert(truth_label.get_line_count() == logical_lines.size(), "%s reference visual lines %d != logical lines %d" % [label, truth_label.get_line_count(), logical_lines.size()])
	var h_explore_lines := 0
	for line in logical_lines:
		if line.contains("H Explore"):
			h_explore_lines += 1
	_assert(h_explore_lines == 1, "%s H Explore is not a single unbroken reference entry" % label)


func _legacy_probe_hud_count() -> int:
	var count := 0
	for child in film.get_children():
		if child is CanvasLayer and int(child.layer) == 12:
			count += 1
	return count


func _legacy_probe_hud_visible() -> bool:
	for child in film.get_children():
		if child is CanvasLayer and int(child.layer) == 12:
			return child.visible
	return false


func _film_hash() -> String:
	var image := film.call("CopyFilmImageForTesting") as Image
	if image == null or image.is_empty():
		return ""
	var context := HashingContext.new()
	context.start(HashingContext.HASH_SHA256)
	context.update(image.get_data())
	return context.finish().hex_encode()


func _authority_signature() -> Dictionary:
	return {
		"generation": int(film.get("CathedralSnapshotGeneration")),
		"requested_policy_id": String(film.get("CathedralRequestedFormalPolicyId")),
		"policy_id": String(film.get("CathedralFormalPolicyId")),
		"policy_sha": String(film.get("CathedralFormalPolicySha256")),
		"geometry": String(film.get("CathedralSpatialKernelGeometrySnapshotSha256")),
		"spatial": String(film.get("CathedralSpatialAuthorityContextSha256")),
		"bvh": String(film.get("CathedralSpatialKernelBvhContextSha256")),
	}


func _scientific_authority_equal_except_terminal(before: Dictionary, after: Dictionary) -> bool:
	return (
		before["generation"] == after["generation"]
		and before["requested_policy_id"] == "cathedral.formal.v1"
		and after["policy_id"] == before["requested_policy_id"]
	)


func _complete_state_signature() -> String:
	return JSON.stringify({
		"mode": String(controller.call("GetModeName")),
		"profile": String(film.get("ProbeProfileStableId")),
		"macro": int(film.get("RuntimeMacroSelectedMode")),
		"view": int(film.get("CurrentProbeView")),
		"generation": int(film.get("CathedralSnapshotGeneration")),
		"lifecycle": String(film.get("CathedralSnapshotLifecycleStateName")),
		"reason": String(film.get("CathedralSnapshotLifecycleReasonName")),
		"sealed": bool(film.get("ProbeViewAvailable")),
		"sealed_generation": int(film.get("SealedProbeViewGeneration")),
		"policy": String(film.get("CathedralFormalPolicySha256")),
		"configuration": _direct_configuration(),
	})


func _direct_configuration() -> Dictionary:
	var renderer := world.get_node("RayBeamRenderer")
	return {
		"profile": String(film.get("ProbeProfileStableId")), "scale": film.get("FilmResolutionScale"), "pixel_stride": film.get("PixelStride"),
		"rows_per_frame": film.get("RowsPerFrame"), "min_rows_per_frame": film.get("MinRowsPerFrame"), "rows_cap": film.get("MaxRowsPerFrameCap"),
		"rows_per_step": film.get("UpdateEveryFrameMaxRowsPerStep"), "budget_ms": film.get("UpdateEveryFrameBudgetMs"), "target_ms": film.get("TargetMsPerFrame"),
		"render_max_ms": film.get("RenderStepMaxMs"), "render_pixel_cap": film.get("RenderStepMaxPixelsPerFrame"), "update_every_frame": film.get("UpdateEveryFrame"),
		"max_distance": film.get("MaxDistance"), "auto_range": film.get("AutoRangeDepth"), "auto_range_min": film.get("AutoRangeMin"), "auto_range_max": film.get("AutoRangeMax"),
		"steps": renderer.get("StepsPerRay"), "step_length": renderer.get("StepLength"), "min_step": renderer.get("MinStepLength"), "max_step": renderer.get("MaxStepLength"),
		"collision_stride": renderer.get("CollisionEveryNSteps"), "min_collision_stride": renderer.get("MinCollisionEveryNSteps"),
		"pass1_probe_stride": film.get("Pass1ProbeEveryNSegments"), "pass1_probe_delta": film.get("Pass1ProbeMinTravelDelta"), "pass1_hit_test": film.get("Pass1DoHitTest"),
		"adaptive_substeps": film.get("UseAdaptiveSubsteps"), "band_hit_skip": film.get("UseBandHitSkip"), "broadphase_mode": film.get("BroadphaseControlMode"),
		"broadphase_policy": film.get("BroadphasePolicy"), "broadphase_quick_ray": film.get("UseBroadphaseQuickRay"), "broadphase_overlap": film.get("UseBroadphaseOverlap"),
		"effective_broadphase_mode": film.get("EffectiveBroadphaseMode"), "effective_broadphase_policy": film.get("EffectiveBroadphasePolicy"),
		"effective_broadphase_quick_ray": film.get("EffectiveBroadphaseQuickRay"), "effective_broadphase_overlap": film.get("EffectiveBroadphaseOverlap"),
		"stride": film.get("UsePass2CollisionStride"), "stride_near": film.get("Pass2CollisionStrideNear"), "stride_far": film.get("Pass2CollisionStrideFar"),
		"stride_far_start": film.get("Pass2CollisionStrideFarStartT"), "stride_min_segment": film.get("MinSegLenForStrideSkip"),
		"envelope_radius": film.get("Pass2GeomEnvelopeRadiusScale"), "envelope_expand": film.get("Pass2GeomEnvelopeAabbExpand"), "tlas_pruning": film.get("UseGeometryTLASPruning"),
		"adaptive_envelope": film.get("AdaptiveTelemetryEnvelopeScalingEnabled"), "adaptive_envelope_mode": film.get("AdaptiveEnvelopeControllerMode"),
		"adaptive_envelope_prior": film.get("AdaptiveEnvelopePriorSource"), "soft_gate_quick_miss": film.get("Pass2SoftGateEnableQuickRayMiss"),
		"soft_gate_scoring": film.get("Pass2SoftGateScoringEnabled"), "pass2_hit_back_faces": film.get("Pass2HitBackFaces"),
		"pass2_hit_from_inside": film.get("Pass2HitFromInside"), "render_segment_cap": film.get("RenderStepMaxSegmentsPerFrame"),
		"recorder_enabled": film.get("RecordFormalTransportLedger"),
	}


func _dispatch_key(keycode: Key, shift_pressed := false) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	event.echo = false
	event.shift_pressed = shift_pressed
	Input.parse_input_event(event)
	await _settle(3)
	event.pressed = false
	Input.parse_input_event(event)
	await process_frame


func _toggle_h_one_frame() -> void:
	var event := InputEventKey.new()
	event.keycode = KEY_H
	event.physical_keycode = KEY_H
	event.pressed = true
	event.echo = false
	Input.parse_input_event(event)
	await process_frame
	event.pressed = false
	Input.parse_input_event(event)


func _settle(frames: int) -> void:
	for _frame in range(frames):
		await process_frame


func _assert(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)


func _finish() -> void:
	if failures.is_empty():
		print("GALLERY_PRESENTATION_LEVELS PASS fields=52 physical=%s logical=%s max_card_height=%.1f min_film_clearance=%.1f" % [DisplayServer.window_get_size(), get_root().get_visible_rect().size, max_card_height, min_clearance])
		quit(0)
		return
	for failure in failures:
		push_error("GALLERY_PRESENTATION_LEVELS FAIL: " + failure)
	print("GALLERY_PRESENTATION_LEVELS FAIL count=%d" % failures.size())
	quit(1)
