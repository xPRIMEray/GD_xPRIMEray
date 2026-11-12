extends PanelContainer

const REFRESH_SECONDS := 0.25
const COMPLETE_UNSEALED_NOTICE := "All pixels processed; not sealed; lifecycle still active."

# key, display label, runtime group, source property, source kind
const FIELD_DEFS := [
	["profile", "Selected profile", "Authority", "ProbeProfileStableId", "film"],
	["scale", "Film resolution scale", "Film and scheduling", "FilmResolutionScale", "film"],
	["pixel_stride", "Pixel stride", "Film and scheduling", "PixelStride", "film"],
	["rows_per_frame", "Rows per frame", "Film and scheduling", "RowsPerFrame", "film"],
	["min_rows_per_frame", "Minimum rows per frame", "Film and scheduling", "MinRowsPerFrame", "film"],
	["rows_cap", "Rows cap", "Film and scheduling", "MaxRowsPerFrameCap", "film"],
	["rows_per_step", "Rows per render step", "Film and scheduling", "UpdateEveryFrameMaxRowsPerStep", "film"],
	["budget_ms", "Frame budget (ms)", "Film and scheduling", "UpdateEveryFrameBudgetMs", "film"],
	["target_ms", "Target frame time (ms)", "Film and scheduling", "TargetMsPerFrame", "film"],
	["render_max_ms", "Render-step maximum (ms)", "Film and scheduling", "RenderStepMaxMs", "film"],
	["render_pixel_cap", "Render pixel cap", "Film and scheduling", "RenderStepMaxPixelsPerFrame", "film"],
	["update_every_frame", "Continuous update", "Film and scheduling", "UpdateEveryFrame", "film"],
	["max_distance", "Maximum path distance", "Range", "MaxDistance", "film"],
	["auto_range", "Automatic range", "Range", "AutoRangeDepth", "film"],
	["auto_range_min", "Automatic range minimum", "Range", "AutoRangeMin", "film"],
	["auto_range_max", "Automatic range maximum", "Range", "AutoRangeMax", "film"],
	["steps", "Steps per ray", "Integration and queries", "StepsPerRay", "renderer"],
	["step_length", "Integration step length", "Integration and queries", "StepLength", "renderer"],
	["min_step", "Minimum adaptive step", "Integration and queries", "MinStepLength", "renderer"],
	["max_step", "Maximum adaptive step", "Integration and queries", "MaxStepLength", "renderer"],
	["collision_stride", "Collision cadence", "Integration and queries", "CollisionEveryNSteps", "renderer"],
	["min_collision_stride", "Minimum collision cadence", "Integration and queries", "MinCollisionEveryNSteps", "renderer"],
	["pass1_probe_stride", "Pass-1 probe cadence", "Integration and queries", "Pass1ProbeEveryNSegments", "film"],
	["pass1_probe_delta", "Pass-1 minimum probe travel", "Integration and queries", "Pass1ProbeMinTravelDelta", "film"],
	["pass1_hit_test", "Pass-1 hit testing", "Integration and queries", "Pass1DoHitTest", "film"],
	["adaptive_substeps", "Adaptive substeps", "Integration and queries", "UseAdaptiveSubsteps", "film"],
	["band_hit_skip", "Band hit skip", "Integration and queries", "UseBandHitSkip", "film"],
	["broadphase_mode", "Broadphase control mode", "Broadphase", "BroadphaseControlMode", "film"],
	["broadphase_policy", "Broadphase policy", "Broadphase", "BroadphasePolicy", "film"],
	["broadphase_quick_ray", "Broadphase quick ray", "Broadphase", "UseBroadphaseQuickRay", "film"],
	["broadphase_overlap", "Broadphase overlap", "Broadphase", "UseBroadphaseOverlap", "film"],
	["effective_broadphase_mode", "Resolved broadphase mode", "Broadphase", "EffectiveBroadphaseMode", "film"],
	["effective_broadphase_policy", "Resolved broadphase policy", "Broadphase", "EffectiveBroadphasePolicy", "film"],
	["effective_broadphase_quick_ray", "Resolved quick ray", "Broadphase", "EffectiveBroadphaseQuickRay", "film"],
	["effective_broadphase_overlap", "Resolved overlap", "Broadphase", "EffectiveBroadphaseOverlap", "film"],
	["stride", "Pass-2 collision stride", "Pass 2", "UsePass2CollisionStride", "film"],
	["stride_near", "Near collision stride", "Pass 2", "Pass2CollisionStrideNear", "film"],
	["stride_far", "Far collision stride", "Pass 2", "Pass2CollisionStrideFar", "film"],
	["stride_far_start", "Far-stride threshold", "Pass 2", "Pass2CollisionStrideFarStartT", "film"],
	["stride_min_segment", "Stride minimum segment", "Pass 2", "MinSegLenForStrideSkip", "film"],
	["envelope_radius", "Geometry envelope radius scale", "Pass 2", "Pass2GeomEnvelopeRadiusScale", "film"],
	["envelope_expand", "Geometry envelope expansion", "Pass 2", "Pass2GeomEnvelopeAabbExpand", "film"],
	["tlas_pruning", "Geometry TLAS pruning", "Pass 2", "UseGeometryTLASPruning", "film"],
	["adaptive_envelope", "Adaptive envelope telemetry", "Pass 2", "AdaptiveTelemetryEnvelopeScalingEnabled", "film"],
	["adaptive_envelope_mode", "Adaptive envelope controller", "Pass 2", "AdaptiveEnvelopeControllerMode", "film"],
	["adaptive_envelope_prior", "Adaptive envelope prior", "Pass 2", "AdaptiveEnvelopePriorSource", "film"],
	["soft_gate_quick_miss", "Soft-gate quick miss", "Pass 2", "Pass2SoftGateEnableQuickRayMiss", "film"],
	["soft_gate_scoring", "Soft-gate scoring", "Pass 2", "Pass2SoftGateScoringEnabled", "film"],
	["pass2_hit_back_faces", "Pass-2 back faces", "Pass 2", "Pass2HitBackFaces", "film"],
	["pass2_hit_from_inside", "Pass-2 hit from inside", "Pass 2", "Pass2HitFromInside", "film"],
	["render_segment_cap", "Render segment cap", "Capacity", "RenderStepMaxSegmentsPerFrame", "film"],
	["recorder_enabled", "Formal recorder enabled", "Capacity", "RecordFormalTransportLedger", "film"],
]

@onready var _selected_text: Label = $Margin/Root/AuthorityGrid/SelectedCard/CardMargin/Card/Body
@onready var _applied_text: Label = $Margin/Root/AuthorityGrid/AppliedCard/CardMargin/Card/Body
@onready var _active_text: Label = $Margin/Root/AuthorityGrid/ActiveCard/CardMargin/Card/Body
@onready var _sealed_text: Label = $Margin/Root/AuthorityGrid/SealedCard/CardMargin/Card/Body
@onready var _authority_notice: Label = $Margin/Root/AuthorityNotice
@onready var _current_config_label: Label = $Margin/Root/CurrentConfigLabel
@onready var _baseline_label: Label = $Margin/Root/ConfigHeader/BaselineLabel
@onready var _show_all: CheckButton = $Margin/Root/ConfigHeader/ShowAll
@onready var _config_text: Label = $Margin/Root/ConfigScroll/ConfigText
@onready var _timer: Timer = $RefreshTimer

var _film: Node
var _renderer: Node
var _controller: Node
var _observed_live_baseline: Dictionary = {}
var _baseline_profile := ""
var _baseline_mode := ""
var _live_candidate: Dictionary = {}
var _live_candidate_signature := ""
var _live_observation_streak := 0
var _complete_unsealed_streak := 0
var _refresh_count := 0
var _last_authority_state: Dictionary = {}


func _ready() -> void:
	var root := get_tree().current_scene
	if root == null:
		root = get_tree().root
	_film = _find_node_with_method(root, "SetProbeProfileByIndex")
	if _film != null:
		_renderer = _film.get_parent().get_node_or_null("RayBeamRenderer")
		_controller = _film.get_parent().get_node_or_null("FilmController")
		var signal_name := &"probe_profile_changed" if _film.has_signal("probe_profile_changed") else &"ProbeProfileChanged"
		if _film.has_signal(signal_name):
			_film.connect(signal_name, Callable(self, "_on_probe_profile_changed"))
	_timer.wait_time = REFRESH_SECONDS
	_timer.timeout.connect(_refresh)
	visibility_changed.connect(_update_timer_state)
	_show_all.toggled.connect(_on_show_all_toggled)
	_update_timer_state()
	call_deferred("_refresh")


func _update_timer_state() -> void:
	if not is_node_ready():
		return
	if is_visible_in_tree():
		if _timer.is_stopped():
			_timer.start()
	else:
		_timer.stop()


func _on_probe_profile_changed(_stable_id: String, _display_name: String, _index: int) -> void:
	call_deferred("_refresh")


func _on_show_all_toggled(_enabled: bool) -> void:
	_refresh_configuration_text()


func _refresh() -> void:
	_refresh_count += 1
	if _film == null:
		_set_text(_authority_notice, "Probe camera unavailable.")
		return
	var mode := _controller_mode()
	var active := bool(_film.get("CathedralSnapshotLifecycleActive"))
	var sealed := bool(_film.get("ProbeViewAvailable"))
	var selected_profile := str(_film.get("ProbeProfileStableId"))
	var selected_display := str(_film.get("ProbeProfileDisplayName"))
	var requested_profile := str(_film.get("CathedralRequestedProbeProfileStableId")) if active else ""
	var requested_policy := str(_film.get("CathedralRequestedFormalPolicyId")) if active else ""
	var active_policy_sha := str(_film.get("CathedralActiveFormalPolicySha256")) if active else ""

	_set_text(_selected_text, "%s\n%s" % [selected_display, selected_profile])
	var runtime_macro_mode := int(_film.get("RuntimeMacroSelectedMode"))
	var applied_profile := _runtime_macro_profile_id(runtime_macro_mode)
	var applied_label := _runtime_macro_label(runtime_macro_mode)
	var pending := mode != "LIVE" and selected_profile != applied_profile
	var applied_scope := "Currently effective in LIVE" if mode == "LIVE" else "Historical last-applied LIVE settings"
	var applied_body := "Last-applied profile: %s\nExecution policy: %s\n%s\nCurrently effective mode: %s" % [
		applied_profile, applied_label, applied_scope, mode]
	if pending:
		applied_body += "\nPending P selection: %s" % selected_profile
	_set_text(_applied_text, applied_body)

	var processed := -1
	var total := -1
	if active:
		processed = int(_film.get("CathedralSnapshotProcessedPixelCount"))
		total = int(_film.get("CathedralSnapshotTotalPixelCount"))
	var lifecycle_state := str(_film.get("CathedralSnapshotLifecycleStateName"))
	var lifecycle_reason := str(_film.get("CathedralSnapshotLifecycleReasonName"))
	var active_body := "Inactive\nLast lifecycle: %s / %s" % [lifecycle_state, lifecycle_reason]
	if active:
		active_body = "Generation %d · %s / %s\nProgress %d / %d\nRequest profile: %s\nFormal policy: %s\nPolicy SHA: %s" % [
			int(_film.get("CathedralSnapshotGeneration")), lifecycle_state, lifecycle_reason,
			processed, total, requested_profile, requested_policy, _short_hash(active_policy_sha)]
	_set_text(_active_text, active_body, active_policy_sha)

	var sealed_body := "No sealed evidence"
	var sealed_sha := ""
	if sealed:
		sealed_sha = str(_film.get("CathedralFormalPolicySha256"))
		sealed_body = "Generation %d · profile %s\nFormal policy: %s\nPolicy SHA: %s\nQ view: %s" % [
			int(_film.get("SealedProbeViewGeneration")),
			str(_film.get("CathedralSealedProbeProfileStableId")),
			str(_film.get("CathedralFormalPolicyId")),
			_short_hash(sealed_sha),
			str(_film.get("CurrentProbeViewDisplayName"))]
		if mode == "LIVE":
			sealed_body += "\nHistorical sealed evidence; LIVE is active."
	_set_text(_sealed_text, sealed_body, sealed_sha)

	if active and not sealed and total > 0 and processed == total:
		_complete_unsealed_streak += 1
	else:
		_complete_unsealed_streak = 0
	var notice := _classify_authority_notice(mode, active, sealed, processed, total, _complete_unsealed_streak)
	_set_text(_authority_notice, notice)

	var current_config := _read_configuration()
	_observe_live_baseline(mode, selected_profile, current_config)
	var config_heading := _configuration_heading(mode)
	_set_text(_current_config_label, config_heading)
	_refresh_configuration_text(current_config)
	_last_authority_state = {
		"mode": mode, "active": active, "sealed": sealed,
		"selected_profile": selected_profile, "applied_profile": applied_profile,
		"pending_profile": selected_profile if pending else "",
		"requested_profile": requested_profile,
		"requested_policy": requested_policy, "active_policy_sha": active_policy_sha,
		"lifecycle_state": lifecycle_state, "lifecycle_reason": lifecycle_reason,
		"processed": processed, "total": total, "notice": notice,
		"configuration_heading": config_heading,
		"sealed_generation": int(_film.get("SealedProbeViewGeneration")),
		"sealed_profile": str(_film.get("CathedralSealedProbeProfileStableId")) if sealed else "",
		"sealed_policy": str(_film.get("CathedralFormalPolicyId")) if sealed else "",
		"sealed_policy_sha": sealed_sha,
		"q_view": str(_film.get("CurrentProbeViewDisplayName")) if sealed else "",
	}


func _observe_live_baseline(mode: String, profile: String, config: Dictionary) -> void:
	if mode != "LIVE" or not _observed_live_baseline.is_empty():
		_live_observation_streak = 0
		_live_candidate.clear()
		_live_candidate_signature = ""
		return
	var signature := JSON.stringify(config)
	if signature == _live_candidate_signature:
		_live_observation_streak += 1
	else:
		_live_candidate = config.duplicate(true)
		_live_candidate_signature = signature
		_live_observation_streak = 1
	if _live_observation_streak >= 2:
		_observed_live_baseline = _live_candidate.duplicate(true)
		_baseline_profile = profile
		_baseline_mode = mode


func _refresh_configuration_text(current: Dictionary = {}) -> void:
	if _film == null:
		return
	if current.is_empty():
		current = _read_configuration()
	if _observed_live_baseline.is_empty():
		_set_text(_baseline_label, "Observed LIVE baseline: Not observed")
	else:
		_set_text(_baseline_label, "Observed LIVE baseline · profile %s · captured in %s · historical reference" % [_baseline_profile, _baseline_mode])
	var lines: Array[String] = []
	var current_group := ""
	for definition in FIELD_DEFS:
		var key: String = definition[0]
		var changed: bool = _observed_live_baseline.is_empty() or current.get(key) != _observed_live_baseline.get(key)
		if not _show_all.button_pressed and not changed:
			continue
		var group: String = definition[2]
		if group != current_group:
			current_group = group
			lines.append("\n%s" % group)
		var suffix := ""
		if not _observed_live_baseline.is_empty() and changed:
			suffix = "  (baseline %s)" % _format_value(_observed_live_baseline.get(key))
		lines.append("%s: %s%s" % [definition[1], _format_value(current.get(key)), suffix])
	if lines.is_empty():
		lines.append("No changes from observed LIVE baseline.")
	_set_text(_config_text, "\n".join(lines).strip_edges())


func _read_configuration() -> Dictionary:
	var result := {}
	for definition in FIELD_DEFS:
		var key: String = definition[0]
		var property_name: String = definition[3]
		var source_kind: String = definition[4]
		match source_kind:
			"renderer":
				result[key] = _renderer.get(property_name) if _renderer != null else null
			_:
				result[key] = _film.get(property_name)
	return result


func _controller_mode() -> String:
	return str(_controller.call("GetModeName")) if _controller != null else "UNKNOWN"


func _configuration_heading(mode: String) -> String:
	if mode == "LIVE":
		return "Effective now · LIVE"
	return "Effective now · %s (not LIVE settings)" % mode


func _runtime_macro_profile_id(mode: int) -> String:
	return "motion" if mode == 2 else "cathedral"


func _runtime_macro_label(mode: int) -> String:
	match mode:
		0: return "Cathedral / Accurate"
		1: return "Tight08"
		2: return "Motion / CheapMotion"
		3: return "SettleRefine"
	return "Unknown (%d)" % mode


func _short_hash(value: String) -> String:
	if value.is_empty():
		return "Unavailable"
	return value.substr(0, min(12, value.length())) + "…"


func _format_value(value: Variant) -> String:
	if value is float:
		return String.num(float(value), 9)
	if value is bool:
		return "On" if value else "Off"
	return str(value)


func _set_text(label: Label, value: String, full_tooltip := "") -> void:
	if label.text != value:
		label.text = value
	if label.tooltip_text != full_tooltip:
		label.tooltip_text = full_tooltip


func _classify_authority_notice(mode: String, active: bool, sealed: bool, processed: int, total: int, streak: int) -> String:
	if active and not sealed and total > 0 and processed == total and streak >= 2:
		return COMPLETE_UNSEALED_NOTICE
	if mode == "LIVE" and sealed:
		return "LIVE is active; historical sealed evidence remains present."
	return "Read-only observation. No acquisition or rendering control is performed here."


func _find_node_with_method(root: Node, method_name: String) -> Node:
	if root == null:
		return null
	if root.has_method(method_name):
		return root
	for child in root.get_children():
		var found := _find_node_with_method(child, method_name)
		if found != null:
			return found
	return null


# Read-only fixture accessors. They return copies and never invoke control APIs.
func GetFieldKeysForTesting() -> Array[String]:
	var keys: Array[String] = []
	for definition in FIELD_DEFS:
		keys.append(definition[0])
	return keys


func GetCurrentConfigurationForTesting() -> Dictionary:
	return _read_configuration().duplicate(true)


func GetObservedLiveBaselineForTesting() -> Dictionary:
	return _observed_live_baseline.duplicate(true)


func GetAuthorityStateForTesting() -> Dictionary:
	return _last_authority_state.duplicate(true)


func GetRefreshCountForTesting() -> int:
	return _refresh_count


func ForceRefreshForTesting() -> void:
	_refresh()


func SetShowAllForTesting(enabled: bool) -> void:
	_show_all.button_pressed = enabled
	_refresh_configuration_text()


func ClassifyAuthorityNoticeForTesting(mode: String, active: bool, sealed: bool, processed: int, total: int, streak: int) -> String:
	return _classify_authority_notice(mode, active, sealed, processed, total, streak)
