extends PanelContainer

const REFRESH_SECONDS := 0.25
const LEVEL_EXPLORE := 0
const LEVEL_INSTRUMENT := 1
const LEVEL_INSPECT := 2
const FORMAL_ATTENTION_STATES := ["incomplete", "invalidated", "failed"]

@export var scene_label := "WORKBENCH"
@export var film_camera_path: NodePath
@export var film_controller_path: NodePath
@export var film_plate_path: NodePath
@export var player_path: NodePath

@onready var _title: Label = $Margin/Content/Title
@onready var _status: Label = $Margin/Content/Status
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
	var pose_held := _player != null and _player.has_method("IsPoseHeld") and bool(_player.call("IsPoseHeld"))

	var formal_attention := _formal_attention(mode, active, lifecycle, sealed)
	_set_text(_title, "%s · %s" % [scene_label, _level_name(_presentation_level)])
	var profile_line := "P Selected %s" % selected_profile
	if sealed:
		profile_line += " · Sealed %s" % sealed_profile
	var q_line := "Q %s" % q_view
	if sealed:
		q_line += " · Seal %d" % sealed_generation
	var lines: Array[String] = [
		profile_line,
		"G %s · %s · F %s" % [mode, q_line, "ON" if field_structure_visible else "OFF"],
	]
	if _presentation_level != LEVEL_EXPLORE:
		var opacity_text := "—" if mode == "OFF" else "%d%%" % opacity_percent
		lines.append("Film: %d×%d · Opacity: %s · Pose held: %s" % [film_width, film_height, opacity_text, "YES" if pose_held else "NO"])
		lines.append("Shading: %s" % shading)
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

	_last_state = {
		"available": true, "level": _presentation_level, "mode": mode,
		"selected_profile": selected_profile, "selected_profile_id": selected_profile_id,
		"sealed": sealed, "sealed_profile": sealed_profile, "sealed_profile_id": sealed_profile_id,
		"sealed_generation": sealed_generation, "q_view": q_view,
		"q_description": q_description, "q_legend": q_legend,
		"field_structure_visible": field_structure_visible, "active": active,
		"lifecycle": lifecycle, "lifecycle_reason": lifecycle_reason,
		"processed": processed, "total": total, "disposition": disposition,
		"formal_attention": formal_attention, "terminal_relevant": formal_attention,
		"plate_hidden": plate_hidden, "opacity_percent": opacity_percent,
		"shading": shading, "pose_held": pose_held, "scene_label": scene_label,
		"film_width": film_width, "film_height": film_height, "status_text": status_text,
	}


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
