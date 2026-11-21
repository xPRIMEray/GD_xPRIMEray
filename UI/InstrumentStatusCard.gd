extends PanelContainer

const REFRESH_SECONDS := 0.25
const LEVEL_EXPLORE := 0
const LEVEL_INSTRUMENT := 1
const LEVEL_INSPECT := 2
const FORMAL_ATTENTION_STATES := ["incomplete", "invalidated", "failed"]
# Presentation-only attribution for the MaterialCpu shading mode. The authoritative mode name
# ("Material (CPU)") and shading_display are unchanged; the label is appended to the N line.
const MATERIAL_CPU_MODE := "Material (CPU)"
const MATERIAL_CPU_ATTRIBUTION := "Experimental · Display approximation"
const MATERIAL_CPU_ATTRIBUTION_MINIMAL := "Experimental"
const MATERIAL_CPU_EXPLANATION := "CPU material sampling validated. Raster color parity pending."

@export var scene_label := "WORKBENCH"
@export var film_camera_path: NodePath
@export var film_controller_path: NodePath
@export var film_plate_path: NodePath
@export var player_path: NodePath

@onready var _title: Label = $Margin/Content/Title
@onready var _status: Label = $Margin/Content/Status
@onready var _secondary_controls: Label = $Margin/Content/SecondaryControls
@onready var _timer: Timer = $RefreshTimer

var _film: Node
var _controller: Node
var _film_plate: Control
var _player: Node
var _presentation_level := LEVEL_EXPLORE
var _last_state: Dictionary = {}
var _refresh_count := 0


func _ready() -> void:
	_film = get_node_or_null(film_camera_path)
	_controller = get_node_or_null(film_controller_path)
	_film_plate = get_node_or_null(film_plate_path) as Control
	_player = get_node_or_null(player_path)
	_timer.wait_time = REFRESH_SECONDS
	_timer.timeout.connect(_refresh)
	visibility_changed.connect(_update_timer_state)
	_update_timer_state()
	_refresh()


func SetPresentationLevel(level: int) -> void:
	_presentation_level = clampi(level, LEVEL_EXPLORE, LEVEL_INSPECT)
	_refresh()


func _update_timer_state() -> void:
	if not is_node_ready():
		return
	if is_visible_in_tree():
		if _timer.is_stopped():
			_timer.start()
	else:
		_timer.stop()


func _refresh() -> void:
	_refresh_count += 1
	if _film == null or _controller == null:
		_set_text(_title, "INSTRUMENT · UNAVAILABLE")
		_set_text(_status, "Formal camera or controller unavailable.")
		_secondary_controls.visible = false
		_last_state = {"available": false}
		return

	var mode := str(_controller.call("GetModeName"))
	var selected_profile := str(_film.get("ProbeProfileDisplayName"))
	var selected_profile_id := str(_film.get("ProbeProfileStableId"))
	var sealed := bool(_film.get("ProbeViewAvailable"))
	var sealed_profile := str(_film.get("CathedralSealedProbeProfileDisplayName")) if sealed else ""
	var sealed_profile_id := str(_film.get("CathedralSealedProbeProfileStableId")) if sealed else ""
	var sealed_generation := int(_film.get("SealedProbeViewGeneration")) if sealed else 0
	var q_view := str(_film.get("CurrentProbeViewDisplayName")) if sealed else "Unavailable"
	var q_description := str(_film.get("CurrentProbeViewDescription")) if sealed else ""
	var q_legend := str(_film.get("CurrentProbeViewLegend")) if sealed else ""
	var field_structure_visible := bool(_film.get("CurrentFieldStructureVisible"))
	var field_presentation: Dictionary = _film.call("GetFieldStructurePresentation") if _film.has_method("GetFieldStructurePresentation") else {}
	var field_mode := str(field_presentation.get("display_name", "Vectors" if field_structure_visible else "Hidden"))
	var active := bool(_film.get("CathedralSnapshotLifecycleActive"))
	var lifecycle := str(_film.get("CathedralSnapshotLifecycleStateName"))
	var lifecycle_reason := str(_film.get("CathedralSnapshotLifecycleReasonName"))
	var processed := int(_film.get("CathedralSnapshotProcessedPixelCount")) if active else -1
	var total := int(_film.get("CathedralSnapshotTotalPixelCount")) if active else -1
	var disposition := str(_controller.call("GetLastInputDisposition"))
	var film_width := int(_film.get("FilmWidthPixels"))
	var film_height := int(_film.get("FilmHeightRows"))
	var opacity_percent := int(_controller.call("GetOpacityPercent"))
	var shading := str(_controller.call("GetShadingModeName"))
	var shading_display := _shading_display_name(shading)
	var pose_held := _player != null and _player.has_method("IsPoseHeld") and bool(_player.call("IsPoseHeld"))
	var input_enabled := not _controller.has_method("IsInteractiveInputEnabled") or bool(_controller.call("IsInteractiveInputEnabled"))
	var q_readiness := _q_formal_readiness(mode, active, lifecycle, input_enabled)

	var formal_attention := _formal_attention(mode, active, lifecycle, sealed)
	_set_text(_title, "%s · %s" % [scene_label, _level_name(_presentation_level)])
	var profile_line := "P Selected %s" % selected_profile
	if sealed:
		profile_line += " · Sealed %s" % sealed_profile
	profile_line += " · F Field: %s" % field_mode
	var field_details := _fit_field_details(profile_line, field_mode, field_presentation)
	profile_line += field_details
	var q_line := "Q Sealed: %s" % q_view if sealed else "Q Formal: %s" % q_readiness
	if sealed:
		q_line += " · Seal %d" % sealed_generation
	var shading_line := "N Shading: %s" % shading_display
	if mode == "SNAPSHOT" and sealed:
		shading_line = "N LIVE shading: %s · Q image unchanged" % shading_display
	var material_attribution := _fit_material_attribution(shading_line, shading)
	shading_line += material_attribution
	var lines: Array[String] = [
		profile_line,
		"G Film: %s · %s" % [mode, q_line],
		shading_line,
	]
	if shading == MATERIAL_CPU_MODE and _presentation_level != LEVEL_EXPLORE:
		lines.append(MATERIAL_CPU_EXPLANATION)
	if _presentation_level != LEVEL_EXPLORE:
		var opacity_text := "—" if mode == "OFF" else "%d%%" % opacity_percent
		lines.append("Film: %d×%d · Opacity: %s · Pose held: %s" % [film_width, film_height, opacity_text, "YES" if pose_held else "NO"])
	if sealed:
		if _presentation_level != LEVEL_EXPLORE:
			lines.append("Q: %s" % q_description)
			lines.append("Legend: %s" % q_legend)
	if active:
		lines.append("Formal: %s / %s · %d / %d" % [lifecycle, lifecycle_reason, processed, total])
	elif formal_attention:
		lines.append("Formal last: %s / %s" % [lifecycle, lifecycle_reason])
	elif _presentation_level != LEVEL_EXPLORE:
		lines.append("Formal: %s / %s" % [lifecycle, lifecycle_reason])
	if not disposition.is_empty() and disposition != "none":
		lines.append("Last action: %s" % disposition)
	var plate_hidden := (
		_presentation_level == LEVEL_EXPLORE
		and _film_plate != null
		and _film_plate.visible
		and not _film_plate.is_visible_in_tree()
	)
	if plate_hidden:
		lines.append("Plate hidden · H to view")
	var status_text := "\n".join(lines)
	_set_text(_status, status_text)
	_secondary_controls.visible = _presentation_level != LEVEL_EXPLORE and scene_label == "WORKBENCH"
	_set_text(_secondary_controls, _secondary_control_reference(input_enabled))

	_last_state = {
		"available": true, "level": _presentation_level, "mode": mode,
		"selected_profile": selected_profile, "selected_profile_id": selected_profile_id,
		"sealed": sealed, "sealed_profile": sealed_profile, "sealed_profile_id": sealed_profile_id,
		"sealed_generation": sealed_generation, "q_view": q_view,
		"q_description": q_description, "q_legend": q_legend,
		"field_structure_visible": field_structure_visible, "active": active,
		"field_structure_mode": field_mode, "field_structure_details": field_details,
		"field_structure_presentation": field_presentation,
		"lifecycle": lifecycle, "lifecycle_reason": lifecycle_reason,
		"processed": processed, "total": total, "disposition": disposition,
		"formal_attention": formal_attention, "terminal_relevant": formal_attention,
		"plate_hidden": plate_hidden, "opacity_percent": opacity_percent,
		"shading": shading, "shading_display": shading_display, "material_attribution": material_attribution,
		"input_enabled": input_enabled, "q_readiness": "" if sealed else q_readiness,
		"secondary_controls_visible": _secondary_controls.visible,
		"secondary_controls": _secondary_controls.text,
		"pose_held": pose_held, "scene_label": scene_label,
		"film_width": film_width, "film_height": film_height, "status_text": status_text,
	}


# FIELDVIZ-R0: Rays are illustrative local single-source density rays, never transport;
# disabled physical fields and unsupported shapes are attributed. Details are appended to
# the existing F line only when they fit its measured width, so the card never grows.
func _fit_field_details(line: String, field_mode: String, presentation: Dictionary) -> String:
	if field_mode == "Hidden":
		return ""
	# Priority order; each part is kept only while the whole line still fits.
	var candidates: Array[String] = []
	if field_mode == "Rays":
		candidates.append("illustrative")
	var disabled := int(presentation.get("disabled_not_drawn", 0))
	if disabled > 0:
		candidates.append("%d disabled not drawn" % disabled)
	var unsupported := int(presentation.get("rays_unsupported", 0)) + int(presentation.get("vectors_unsupported", 0))
	if unsupported > 0:
		candidates.append("%d unsupported shape" % unsupported)
	if candidates.is_empty():
		return ""
	var full := " (%s)" % " · ".join(candidates)
	if unsupported == 0 or _fits_status_line(line + full):
		var kept: Array[String] = []
		for part in candidates:
			var trial := kept.duplicate()
			trial.append(part)
			if _fits_status_line(line + " (%s)" % " · ".join(trial)):
				kept = trial
		return "" if kept.is_empty() else " (%s)" % " · ".join(kept)
	# FIELDVIZ-R0.2.1: a nonzero unsupported count is never silently omitted. Under constrained width it
	# is placed first, in the longest explicit form that fits; the remaining parts follow only while the
	# line still fits. If even the shortest explicit form cannot fit, it is still shown.
	var groups: Array = [["%d unsupported shape" % unsupported, "%d unsupported" % unsupported]]
	if field_mode == "Rays":
		groups.append(["illustrative"])
	if disabled > 0:
		groups.append(["%d disabled not drawn" % disabled, "%d disabled" % disabled])
	var compact: Array[String] = []
	for index in range(groups.size()):
		var placed := false
		for variant in groups[index]:
			var trial := compact.duplicate()
			trial.append(variant)
			if _fits_status_line(line + " (%s)" % " · ".join(trial)):
				compact = trial
				placed = true
				break
		if not placed and index == 0:
			compact.append(groups[0][-1])
	return " (%s)" % " · ".join(compact)


# The experimental label is never dropped: the full attribution when it fits on the N line, otherwise
# the minimal "Experimental" (which may wrap on very narrow cards rather than disappear).
func _fit_material_attribution(line: String, shading: String) -> String:
	if shading != MATERIAL_CPU_MODE:
		return ""
	var full := " · %s" % MATERIAL_CPU_ATTRIBUTION
	if _fits_status_line(line + full):
		return full
	return " · %s" % MATERIAL_CPU_ATTRIBUTION_MINIMAL


func _fits_status_line(line: String) -> bool:
	var width := _status.size.x
	if width <= 0.0:
		return false
	var font := _status.get_theme_font("font")
	var size := _status.get_theme_font_size("font_size")
	return font.get_string_size(line, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x <= width


func _level_name(level: int) -> String:
	match level:
		LEVEL_INSTRUMENT:
			return "INSTRUMENT"
		LEVEL_INSPECT:
			return "INSPECT"
	return "EXPLORE"


func _formal_attention(mode: String, active: bool, state: String, sealed: bool) -> bool:
	if active or mode != "SNAPSHOT":
		return false
	return state in FORMAL_ATTENTION_STATES or (state == "complete" and not sealed)


func _shading_display_name(authoritative_name: String) -> String:
	if authoritative_name == "Geometric Normal Debug":
		return "RGB Normals"
	return authoritative_name


func _q_formal_readiness(mode: String, active: bool, lifecycle: String, input_enabled: bool) -> String:
	# Mirrors FilmController.HandleProbeViewInput refusal order: disabled input,
	# then LIVE, then the authoritative SNAPSHOT lifecycle.
	if active:
		return lifecycle
	if not input_enabled:
		return "suspended"
	if mode == "LIVE":
		return "G OFF first"
	if mode == "SNAPSHOT":
		return lifecycle
	return "ready"


func _secondary_control_reference(input_enabled := true) -> String:
	if not input_enabled:
		# Workbench Inspect disables FilmController and player input. P and F are
		# camera-owned and remain active; the remaining film keys do not act.
		return "Inspect · still active: P Profile · F Field\nSuspended until Esc: G · Q · N · [ / ] · H · V · WASD · Mouse"
	return"[ / ] Opacity · H Level · Shift+Q Previous · Ctrl+Q Reacquire\nV Walk/Fly · WASD Move · Mouse Look · Shift Sprint"


func _set_text(label: Label, value: String) -> void:
	if label.text != value:
		label.text = value


func GetDisplayedStateForTesting() -> Dictionary:
	return _last_state.duplicate(true)


func GetRefreshCountForTesting() -> int:
	return _refresh_count


func ForceRefreshForTesting() -> void:
	_refresh()


func IsFormalAttentionForTesting(mode: String, active: bool, state: String, sealed: bool) -> bool:
	return _formal_attention(mode, active, state, sealed)
