extends SceneTree

const EXPECTED_KEYS := [
	"profile", "scale", "pixel_stride", "rows_per_frame", "min_rows_per_frame", "rows_cap",
	"rows_per_step", "budget_ms", "target_ms", "render_max_ms", "render_pixel_cap", "update_every_frame",
	"max_distance", "auto_range", "auto_range_min", "auto_range_max", "steps", "step_length", "min_step",
	"max_step", "collision_stride", "min_collision_stride", "pass1_probe_stride", "pass1_probe_delta",
	"pass1_hit_test", "adaptive_substeps", "band_hit_skip", "broadphase_mode", "broadphase_policy",
	"broadphase_quick_ray", "broadphase_overlap", "effective_broadphase_mode", "effective_broadphase_policy",
	"effective_broadphase_quick_ray", "effective_broadphase_overlap", "stride", "stride_near", "stride_far",
	"stride_far_start", "stride_min_segment", "envelope_radius", "envelope_expand", "tlas_pruning",
	"adaptive_envelope", "adaptive_envelope_mode", "adaptive_envelope_prior", "soft_gate_quick_miss",
	"soft_gate_scoring", "pass2_hit_back_faces", "pass2_hit_from_inside", "render_segment_cap", "recorder_enabled",
]

var scene_root: Node
var film: Node
var renderer: Node
var controller: Node
var inspector: Node
var side_dock: Control
var tabs: TabContainer
var failures: Array[String] = []
var output_root := ""


func _initialize() -> void:
	_parse_args()
	_t1_source_guardrails()
	scene_root = load("res://ObservatoryWorkbench.tscn").instantiate()
	get_root().add_child(scene_root)
	await _settle(8)
	film = scene_root.get_node("PlayableWorld/TransportChamberWorld/GrinFilmCamera")
	renderer = scene_root.get_node("PlayableWorld/TransportChamberWorld/RayBeamRenderer")
	controller = scene_root.get_node("PlayableWorld/TransportChamberWorld/FilmController")
	side_dock = scene_root.get_node("UILayer/UIRoot/SideDock")
	tabs = scene_root.get_node("UILayer/UIRoot/SideDock/Tabs")
	inspector = tabs.get_node("Inspector")
	side_dock.visible = true
	tabs.current_tab = 2
	await _settle(4)

	_t2_exact_field_contract()
	_t3_property_contract()
	await _t4_live_and_profile_transitions()
	await _t5_active_request_ownership()
	await _t6_sealed_q_provenance()
	await _t7_invalidation_and_classification()
	await _t8_refresh_noninterference()
	_t9_configuration_parity()
	await _t10_visibility_and_layout()
	await _t11_optional_viewport_evidence()

	if failures.is_empty():
		print("PROBE_POLICY_INSPECTOR PASS tests=11 fields=52")
		quit(0)
	else:
		for failure in failures:
			push_error("PROBE_POLICY_INSPECTOR FAIL: " + failure)
		print("PROBE_POLICY_INSPECTOR FAIL count=%d" % failures.size())
		quit(1)


func _parse_args() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--output="):
			output_root = arg.trim_prefix("--output=")


func _t1_source_guardrails() -> void:
	var source := FileAccess.get_file_as_string("res://UI/ProbePolicyInspector.gd")
	_assert(not source.is_empty(), "T1 inspector source unavailable")
	for forbidden in [
		"func _process", "IsComputeActive", "_film.set(",
		"_film.call(\"Request", "_film.call(\"Invalidate", "_film.call(\"SetProbe",
		"_controller.call(\"set_mode", "_controller.call(\"Toggle", "_controller.call(\"HandleProbe",
		"func _resolve_broadphase",
	]:
		_assert(source.find(forbidden) < 0, "T1 read-only guardrail contains %s" % forbidden)
	_assert(source.find("Timer") >= 0 and source.find("REFRESH_SECONDS := 0.25") >= 0,
		"T1 bounded refresh timer contract")


func _t2_exact_field_contract() -> void:
	var actual: Array = inspector.call("GetFieldKeysForTesting")
	_assert(actual.size() == 52, "T2 field count expected 52, got %d" % actual.size())
	_assert(actual == EXPECTED_KEYS, "T2 field order or identity differs from qualified signature")
	var unique := {}
	for key in actual:
		unique[key] = true
	_assert(unique.size() == 52, "T2 duplicate configuration keys")


func _t3_property_contract() -> void:
	for property_name in [
		"CathedralRequestedFormalPolicyId", "CathedralActiveFormalPolicySha256",
		"CurrentProbeViewDisplayName", "ProbeProfileStableId", "ProbeProfileDisplayName",
		"RuntimeMacroSelectedMode", "CathedralSnapshotLifecycleActive", "ProbeViewAvailable",
	]:
		_assert(typeof(film.get(property_name)) != TYPE_NIL, "T3 missing camera property %s" % property_name)
	for property_name in ["StepsPerRay", "StepLength", "MinStepLength", "MaxStepLength", "CollisionEveryNSteps", "MinCollisionEveryNSteps"]:
		_assert(typeof(renderer.get(property_name)) != TYPE_NIL, "T3 missing renderer property %s" % property_name)


func _t4_live_and_profile_transitions() -> void:
	inspector.call("ForceRefreshForTesting")
	var state: Dictionary = inspector.call("GetAuthorityStateForTesting")
	_assert(str(state.get("configuration_heading")) == "Effective now · OFF (not LIVE settings)",
		"T4 OFF configuration provenance label")
	_assert((inspector.call("GetObservedLiveBaselineForTesting") as Dictionary).is_empty(),
		"T4 baseline exists before LIVE observation")
	film.call("SetProbeProfileByIndex", 0)
	_assert(bool(controller.call("ToggleLiveFromInput")), "T4 OFF to LIVE rejected")
	await _settle(24)
	inspector.call("ForceRefreshForTesting")
	state = inspector.call("GetAuthorityStateForTesting")
	_assert(str(state.get("configuration_heading")) == "Effective now · LIVE",
		"T4 LIVE configuration provenance label")
	var baseline: Dictionary = inspector.call("GetObservedLiveBaselineForTesting")
	_assert(baseline.size() == 52, "T4 stable LIVE baseline was not captured")
	_assert(str(baseline.get("profile")) == "cathedral", "T4 baseline profile provenance")
	var baseline_copy := JSON.stringify(baseline)
	film.call("SetProbeProfileByIndex", 1)
	await _settle(5)
	inspector.call("ForceRefreshForTesting")
	state = inspector.call("GetAuthorityStateForTesting")
	_assert(str(state.get("mode")) == "LIVE" and str(state.get("selected_profile")) == "motion",
		"T4 LIVE P transition not observed")
	_assert(JSON.stringify(inspector.call("GetObservedLiveBaselineForTesting")) == baseline_copy,
		"T4 historical LIVE baseline was silently overwritten")
	_assert(bool(controller.call("ToggleLiveFromInput")), "T4 LIVE to OFF rejected")
	await _settle(3)


func _t5_active_request_ownership() -> void:
	film.call("SetProbeProfileByIndex", 0)
	_assert(bool(controller.call("EnterFormalSnapshotFromInput")), "T5 Formal request rejected")
	var generation := int(film.get("CathedralSnapshotGeneration"))
	film.call("SetProbeProfileByIndex", 1)
	await _settle(2)
	for _observation in range(60):
		if not str(film.get("CathedralActiveFormalPolicySha256")).is_empty():
			break
		await physics_frame
	inspector.call("ForceRefreshForTesting")
	var state: Dictionary = inspector.call("GetAuthorityStateForTesting")
	_assert(bool(state.get("active")), "T5 active Formal authority not observed")
	_assert(int(film.get("CathedralSnapshotGeneration")) == generation, "T5 inspector or P superseded generation")
	_assert(str(state.get("selected_profile")) == "motion", "T5 selected P profile not observed")
	_assert(str(state.get("requested_profile")) == "cathedral", "T5 request-owned profile not frozen")
	_assert(str(state.get("requested_policy")) == "cathedral.formal.v1", "T5 request-owned policy not observed")
	_assert(not str(state.get("active_policy_sha")).is_empty(), "T5 initialized active Formal policy hash unavailable")
	_assert(str(state.get("applied_profile")) == "cathedral", "T5 last-applied profile truthfulness")
	_assert(str(state.get("pending_profile")) == "motion", "T5 active pending P selection")
	_assert(str(state.get("configuration_heading")) == "Effective now · SNAPSHOT (not LIVE settings)",
		"T5 SNAPSHOT configuration provenance label")
	var active_hash_label := inspector.get_node("Margin/Root/AuthorityGrid/ActiveCard/CardMargin/Card/Body") as Label
	_assert(active_hash_label.mouse_filter == Control.MOUSE_FILTER_STOP, "T5 active hash label mouse access")
	_assert(active_hash_label.tooltip_text == str(state.get("active_policy_sha")), "T5 active full hash tooltip")


func _t6_sealed_q_provenance() -> void:
	await _wait_for_complete()
	inspector.call("ForceRefreshForTesting")
	var state: Dictionary = inspector.call("GetAuthorityStateForTesting")
	_assert(not bool(state.get("active")) and bool(state.get("sealed")), "T6 completion authority classification")
	_assert(str(state.get("requested_profile")).is_empty(), "T6 stale request metadata displayed as active")
	_assert(str(state.get("sealed_profile")) == "cathedral", "T6 sealed request-owned profile")
	_assert(str(state.get("sealed_policy")) == "cathedral.formal.v1", "T6 sealed policy")
	_assert(not str(state.get("sealed_policy_sha")).is_empty(), "T6 sealed policy hash")
	_assert(str(state.get("selected_profile")) == "motion", "T6 selected profile after Formal")
	_assert(str(state.get("applied_profile")) == "cathedral", "T6 last-applied profile after Formal")
	_assert(str(state.get("pending_profile")) == "motion", "T6 pending P selection survives Formal completion")
	var sealed_hash_label := inspector.get_node("Margin/Root/AuthorityGrid/SealedCard/CardMargin/Card/Body") as Label
	_assert(sealed_hash_label.mouse_filter == Control.MOUSE_FILTER_STOP, "T6 sealed hash label mouse access")
	_assert(sealed_hash_label.tooltip_text == str(state.get("sealed_policy_sha")), "T6 sealed full hash tooltip")
	var generation := int(state.get("sealed_generation"))
	var view_before := str(state.get("q_view"))
	_assert(bool(controller.call("HandleProbeViewInput", false, false)), "T6 Q navigation rejected")
	await _settle(2)
	inspector.call("ForceRefreshForTesting")
	state = inspector.call("GetAuthorityStateForTesting")
	_assert(int(state.get("sealed_generation")) == generation, "T6 Q changed sealed generation")
	_assert(str(state.get("q_view")) != view_before, "T6 Q view display did not change")


func _t7_invalidation_and_classification() -> void:
	film.call("InvalidateCathedralProbeSnapshotForContextChange")
	await _settle(2)
	inspector.call("ForceRefreshForTesting")
	var state: Dictionary = inspector.call("GetAuthorityStateForTesting")
	_assert(not bool(state.get("active")) and not bool(state.get("sealed")), "T7 invalidation classification")
	_assert(str(state.get("requested_profile")).is_empty(), "T7 invalidated request metadata presented as active")
	_assert(str(state.get("applied_profile")) == "cathedral" and str(state.get("pending_profile")) == "motion",
		"T7 pending P selection survives invalidation")
	var d2_first := str(inspector.call("ClassifyAuthorityNoticeForTesting", "SNAPSHOT", true, false, 14400, 14400, 1))
	var d2_second := str(inspector.call("ClassifyAuthorityNoticeForTesting", "SNAPSHOT", true, false, 14400, 14400, 2))
	_assert(d2_first.find("not sealed") < 0, "T7 D2 warned before two observations")
	_assert(d2_second == "All pixels processed; not sealed; lifecycle still active.", "T7 D2 conservative classification")
	var d1 := str(inspector.call("ClassifyAuthorityNoticeForTesting", "LIVE", false, true, -1, -1, 0))
	_assert(d1.find("historical sealed evidence remains present") >= 0, "T7 D1 coexistence classification")


func _t8_refresh_noninterference() -> void:
	var before := _production_state_signature()
	var count_before := int(inspector.call("GetRefreshCountForTesting"))
	for _i in range(8):
		inspector.call("ForceRefreshForTesting")
	var after := _production_state_signature()
	_assert(before == after, "T8 inspector refresh changed production state")
	_assert(int(inspector.call("GetRefreshCountForTesting")) == count_before + 8, "T8 refresh accounting")


func _t9_configuration_parity() -> void:
	var actual: Dictionary = inspector.call("GetCurrentConfigurationForTesting")
	var expected := _direct_configuration()
	_assert(actual.size() == 52 and expected.size() == 52, "T9 configuration size")
	for key in EXPECTED_KEYS:
		_assert(actual.get(key) == expected.get(key), "T9 source parity for %s" % key)


func _t10_visibility_and_layout() -> void:
	inspector.call("SetShowAllForTesting", true)
	get_root().size = Vector2i(1280, 720)
	await _settle(3)
	_assert(_controls_within_horizontal_bounds(inspector), "T10 desktop horizontal bounds")
	get_root().size = Vector2i(720, 1080)
	await _settle(3)
	_assert(_controls_within_horizontal_bounds(inspector), "T10 portrait horizontal bounds")
	var visible_count := int(inspector.call("GetRefreshCountForTesting"))
	side_dock.visible = false
	await create_timer(0.6).timeout
	_assert(int(inspector.call("GetRefreshCountForTesting")) == visible_count, "T10 hidden inspector timer remained active")
	side_dock.visible = true
	tabs.current_tab = 2
	await create_timer(0.6).timeout
	_assert(int(inspector.call("GetRefreshCountForTesting")) > visible_count, "T10 visible inspector timer did not resume")


func _t11_optional_viewport_evidence() -> void:
	if output_root.is_empty():
		return
	if DisplayServer.get_name() == "headless":
		print("PROBE_POLICY_INSPECTOR VIEWPORT_EVIDENCE unsupported (headless display)")
		return
	DirAccess.make_dir_recursive_absolute(output_root)
	await process_frame
	await process_frame
	var image := get_root().get_texture().get_image()
	if image == null or image.is_empty():
		print("PROBE_POLICY_INSPECTOR VIEWPORT_EVIDENCE unsupported")
		return
	var path := output_root.path_join("probe_policy_inspector_portrait.png")
	var error := image.save_png(path)
	_assert(error == OK, "T11 viewport evidence save failed")
	if error == OK:
		print("PROBE_POLICY_INSPECTOR VIEWPORT_EVIDENCE " + path)


func _direct_configuration() -> Dictionary:
	return {
		"profile": str(film.get("ProbeProfileStableId")),
		"scale": film.get("FilmResolutionScale"), "pixel_stride": film.get("PixelStride"),
		"rows_per_frame": film.get("RowsPerFrame"), "min_rows_per_frame": film.get("MinRowsPerFrame"),
		"rows_cap": film.get("MaxRowsPerFrameCap"), "rows_per_step": film.get("UpdateEveryFrameMaxRowsPerStep"),
		"budget_ms": film.get("UpdateEveryFrameBudgetMs"), "target_ms": film.get("TargetMsPerFrame"),
		"render_max_ms": film.get("RenderStepMaxMs"), "render_pixel_cap": film.get("RenderStepMaxPixelsPerFrame"),
		"update_every_frame": film.get("UpdateEveryFrame"), "max_distance": film.get("MaxDistance"),
		"auto_range": film.get("AutoRangeDepth"), "auto_range_min": film.get("AutoRangeMin"),
		"auto_range_max": film.get("AutoRangeMax"), "steps": renderer.get("StepsPerRay"),
		"step_length": renderer.get("StepLength"), "min_step": renderer.get("MinStepLength"),
		"max_step": renderer.get("MaxStepLength"), "collision_stride": renderer.get("CollisionEveryNSteps"),
		"min_collision_stride": renderer.get("MinCollisionEveryNSteps"),
		"pass1_probe_stride": film.get("Pass1ProbeEveryNSegments"),
		"pass1_probe_delta": film.get("Pass1ProbeMinTravelDelta"), "pass1_hit_test": film.get("Pass1DoHitTest"),
		"adaptive_substeps": film.get("UseAdaptiveSubsteps"), "band_hit_skip": film.get("UseBandHitSkip"),
		"broadphase_mode": film.get("BroadphaseControlMode"), "broadphase_policy": film.get("BroadphasePolicy"),
		"broadphase_quick_ray": film.get("UseBroadphaseQuickRay"), "broadphase_overlap": film.get("UseBroadphaseOverlap"),
		"effective_broadphase_mode": film.get("EffectiveBroadphaseMode"),
		"effective_broadphase_policy": film.get("EffectiveBroadphasePolicy"),
		"effective_broadphase_quick_ray": film.get("EffectiveBroadphaseQuickRay"),
		"effective_broadphase_overlap": film.get("EffectiveBroadphaseOverlap"),
		"stride": film.get("UsePass2CollisionStride"), "stride_near": film.get("Pass2CollisionStrideNear"),
		"stride_far": film.get("Pass2CollisionStrideFar"), "stride_far_start": film.get("Pass2CollisionStrideFarStartT"),
		"stride_min_segment": film.get("MinSegLenForStrideSkip"),
		"envelope_radius": film.get("Pass2GeomEnvelopeRadiusScale"),
		"envelope_expand": film.get("Pass2GeomEnvelopeAabbExpand"), "tlas_pruning": film.get("UseGeometryTLASPruning"),
		"adaptive_envelope": film.get("AdaptiveTelemetryEnvelopeScalingEnabled"),
		"adaptive_envelope_mode": film.get("AdaptiveEnvelopeControllerMode"),
		"adaptive_envelope_prior": film.get("AdaptiveEnvelopePriorSource"),
		"soft_gate_quick_miss": film.get("Pass2SoftGateEnableQuickRayMiss"),
		"soft_gate_scoring": film.get("Pass2SoftGateScoringEnabled"),
		"pass2_hit_back_faces": film.get("Pass2HitBackFaces"), "pass2_hit_from_inside": film.get("Pass2HitFromInside"),
		"render_segment_cap": film.get("RenderStepMaxSegmentsPerFrame"),
		"recorder_enabled": film.get("RecordFormalTransportLedger"),
	}


func _production_state_signature() -> String:
	return JSON.stringify({
		"mode": str(controller.call("GetModeName")),
		"generation": int(film.get("CathedralSnapshotGeneration")),
		"active": bool(film.get("CathedralSnapshotLifecycleActive")),
		"sealed": bool(film.get("ProbeViewAvailable")),
		"sealed_generation": int(film.get("SealedProbeViewGeneration")),
		"view": int(film.get("CurrentProbeView")),
		"profile": str(film.get("ProbeProfileStableId")),
		"config": _direct_configuration(),
	})


func _controls_within_horizontal_bounds(root: Control) -> bool:
	var root_rect := root.get_global_rect()
	for child in root.find_children("*", "Control", true, false):
		var control := child as Control
		if control == null or not control.is_visible_in_tree():
			continue
		var rect := control.get_global_rect()
		if rect.position.x < root_rect.position.x - 1.0 or rect.end.x > root_rect.end.x + 1.0:
			return false
	return true


func _wait_for_complete() -> void:
	for _i in range(1200):
		await process_frame
		if bool(film.get("ProbeViewAvailable")):
			return
	_fail("Formal acquisition timeout")


func _settle(frames: int) -> void:
	for _i in range(frames):
		await process_frame


func _assert(condition: bool, message: String) -> void:
	if not condition:
		_fail(message)


func _fail(message: String) -> void:
	failures.append(message)
