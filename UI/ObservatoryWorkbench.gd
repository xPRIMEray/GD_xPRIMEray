extends Node

const PRESENTATION_EXPLORE := 0
const PRESENTATION_INSTRUMENT := 1
const PRESENTATION_INSPECT := 2

@onready var _tabs: TabContainer = $UILayer/UIRoot/SideDock/Tabs
@onready var _side_dock: Control = $UILayer/UIRoot/SideDock
@onready var _launch_hint: Control = $UILayer/UIRoot/LaunchHintPanel
@onready var _hint_text: Label = $UILayer/UIRoot/LaunchHintPanel/HintMargin/HintText
@onready var _pixel_life: Control = $UILayer/UIRoot/PixelLifeInspector

var _workbench_open := false
var _pixel_life_open := false
var _input_camera: Node
var _closed_presentation_level := PRESENTATION_EXPLORE
var _auto_reveal_count := 0


func _ready() -> void:
	_tabs.current_tab = 0
	_tabs.set_tab_title(0, "Observatory")
	_tabs.set_tab_title(1, "TestBench")
	_input_camera = _find_input_camera(self)
	if _input_camera != null and _input_camera.has_signal("operator_observation_accepted"):
		_input_camera.connect("operator_observation_accepted", Callable(self, "_on_operator_observation_accepted"))
	get_viewport().size_changed.connect(_apply_workbench_layout)
	_apply_workbench_layout()
	_set_workbench_open(false)


func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if _keyboard_focus_blocks_shortcuts():
			return
		if event.keycode == KEY_I:
			_set_pixel_life_open(not _pixel_life_open)
			get_viewport().set_input_as_handled()
		elif event.keycode == KEY_ESCAPE and _pixel_life_open:
			_set_pixel_life_open(false)
			get_viewport().set_input_as_handled()
		elif event.keycode == KEY_ESCAPE:
			_set_workbench_open(not _workbench_open)
			get_viewport().set_input_as_handled()
		elif event.keycode == KEY_H and not _workbench_open and not _pixel_life_open:
			_closed_presentation_level = PRESENTATION_INSTRUMENT if _closed_presentation_level == PRESENTATION_EXPLORE else PRESENTATION_EXPLORE
			_apply_presentation_level(_closed_presentation_level)
			_update_hint()
			get_viewport().set_input_as_handled()


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		if _pixel_life_open:
			return
		if _workbench_open:
			_set_workbench_open(false)
			get_viewport().set_input_as_handled()
		elif Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
			Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
			_update_hint()


func _set_workbench_open(open: bool) -> void:
	if open and _pixel_life_open:
		_pixel_life_open = false
		_pixel_life.call("SetOpen", false)
	_workbench_open = open
	_side_dock.visible = open
	if _input_camera != null and _input_camera.has_method("SetInputEnabled"):
		_input_camera.call("SetInputEnabled", not open, true)
	_apply_presentation_level(PRESENTATION_INSPECT if open else _closed_presentation_level)
	_launch_hint.visible = not open
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if open else Input.MOUSE_MODE_CAPTURED
	_update_hint()


# INSPECT-R0.2: I toggles the read-only Pixel Life Inspector. Like Inspect (Esc) it frees the mouse and
# suspends camera/player input; it closes the side dock. It reads the sealed acquisition only.
func _set_pixel_life_open(open: bool) -> void:
	if open and _workbench_open:
		_workbench_open = false
		_side_dock.visible = false
	_pixel_life_open = open
	_pixel_life.call("SetOpen", open)
	if _input_camera != null and _input_camera.has_method("SetInputEnabled"):
		_input_camera.call("SetInputEnabled", not open, true)
	_apply_presentation_level(PRESENTATION_INSPECT if open else _closed_presentation_level)
	_launch_hint.visible = not open
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if open else Input.MOUSE_MODE_CAPTURED
	_update_hint()


func IsPixelLifeOpenForTesting() -> bool:
	return _pixel_life_open


# Accepted operator G/Q actions reveal a hidden film by promoting Explore to
# Instrument once. Inspect is never changed, Instrument is left alone, and a later
# manual H back to Explore stays authoritative until the next accepted action.
func _on_operator_observation_accepted(_action: StringName) -> void:
	if _workbench_open or _pixel_life_open or _closed_presentation_level != PRESENTATION_EXPLORE:
		return
	_closed_presentation_level = PRESENTATION_INSTRUMENT
	_auto_reveal_count += 1
	_apply_presentation_level(_closed_presentation_level)
	_update_hint()


func GetAutoRevealCountForTesting() -> int:
	return _auto_reveal_count


func _apply_presentation_level(level: int) -> void:
	if _input_camera != null and _input_camera.has_method("SetPresentationLevel"):
		_input_camera.call("SetPresentationLevel", level)


func _apply_workbench_layout() -> void:
	if not is_node_ready():
		return
	var size := get_viewport().get_visible_rect().size
	var portrait := size.x < 900.0 or size.y > size.x * 1.15
	if portrait:
		_side_dock.anchor_left = 0.0
		_side_dock.anchor_top = 0.48
		_side_dock.anchor_right = 1.0
		_side_dock.anchor_bottom = 1.0
		_side_dock.offset_left = 12.0
		_side_dock.offset_top = 0.0
		_side_dock.offset_right = -12.0
		_side_dock.offset_bottom = -12.0
	else:
		_side_dock.anchor_left = 1.0
		_side_dock.anchor_top = 0.0
		_side_dock.anchor_right = 1.0
		_side_dock.anchor_bottom = 1.0
		_side_dock.offset_left = -540.0
		_side_dock.offset_top = 12.0
		_side_dock.offset_right = -12.0
		_side_dock.offset_bottom = -12.0


func _keyboard_focus_blocks_shortcuts() -> bool:
	var focus := get_viewport().gui_get_focus_owner()
	if focus is LineEdit or focus is TextEdit:
		return true
	if focus != null and focus.get_parent() is SpinBox:
		return true
	for child in get_tree().root.get_children():
		if child is Window and child != get_tree().root and child.visible and child.exclusive:
			return true
	return false


func _update_hint() -> void:
	if _workbench_open:
		_hint_text.text = "Inspect open - Esc or click world to resume"
	elif Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		_hint_text.text = "WASD move - Mouse look - H Explore/Instrument - Esc Inspect - I Pixel Life"
	else:
		_hint_text.text = "Click to explore - H Explore/Instrument - Esc Inspect - I Pixel Life"


func GetPresentationLevelForTesting() -> int:
	return PRESENTATION_INSPECT if _workbench_open or _pixel_life_open else _closed_presentation_level


func IsWorkbenchOpenForTesting() -> bool:
	return _workbench_open


func GetLayoutPlanForTesting(viewport_size: Vector2) -> Dictionary:
	var portrait := viewport_size.x < 900.0 or viewport_size.y > viewport_size.x * 1.15
	var dock := Rect2(
		Vector2(12.0, viewport_size.y * 0.48),
		Vector2(viewport_size.x - 24.0, viewport_size.y * 0.52 - 12.0)
	) if portrait else Rect2(
		Vector2(viewport_size.x - 540.0, 12.0),
		Vector2(528.0, viewport_size.y - 24.0)
	)
	return {
		"dock": dock,
		"status": Rect2(12.0, 12.0, 480.0, 250.0),
		"film": Rect2(12.0, 274.0, 340.0, 340.0),
	}


func _find_input_camera(root: Node) -> Node:
	if root.has_method("SetInputEnabled"):
		return root
	for child in root.get_children():
		var found := _find_input_camera(child)
		if found != null:
			return found
	return null
