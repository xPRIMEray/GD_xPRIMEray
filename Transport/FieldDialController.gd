class_name FieldDialController
extends Node

signal field_strength_changed(value: float)

const FIELD_MIN := 0.0
const FIELD_MAX := 1.0
const FIELD_STEP := 0.05
const FIELD_DIAL_REPEAT_S := 0.08

const EXPERIMENT_GALLERY := "Gallery"
const EXPERIMENT_HERMETIC := "Hermetic"

@export var ray_renderer_path: NodePath
@export var film_controller_path: NodePath
@export var player_path: NodePath
@export var overspace_root_path: NodePath
@export var gallery_collision_path: NodePath
@export var hermetic_display_path: NodePath
@export var field_bar_path: NodePath
@export var field_value_label_path: NodePath
@export var gallery_field_paths: Array[NodePath] = []
@export var hermetic_field_path: NodePath

var _field_strength := 1.0
var _input_enabled := true
var _held_direction := 0
var _repeat_timer := 0.0
var _experiment := EXPERIMENT_GALLERY
var _gallery_camera_transform := Transform3D(Basis.IDENTITY, Vector3(0.0, 1.8, 10.0))
var _hermetic_camera_transform := Transform3D(Basis.IDENTITY, Vector3(2.0, 1.6, -1.0))

@onready var _ray_renderer: Node = get_node_or_null(ray_renderer_path)
@onready var _film_controller: Node = get_node_or_null(film_controller_path)
@onready var _player: Node = get_node_or_null(player_path)
@onready var _overspace_root: Node3D = get_node_or_null(overspace_root_path)
@onready var _gallery_collision: Node3D = get_node_or_null(gallery_collision_path)
@onready var _hermetic_display: Node3D = get_node_or_null(hermetic_display_path)
@onready var _field_bar: Range = get_node_or_null(field_bar_path)
@onready var _field_value_label: Label = get_node_or_null(field_value_label_path)
@onready var _hermetic_field: Node = get_node_or_null(hermetic_field_path)


func _ready() -> void:
	_apply_field_strength(false)
	_set_experiment(EXPERIMENT_GALLERY, false)
	_update_display()


func _unhandled_input(event: InputEvent) -> void:
	if not _input_enabled:
		return
	if event is InputEventKey and not event.echo:
		if event.pressed:
			match event.keycode:
				KEY_COMMA:
					_begin_held_step(-1)
					get_viewport().set_input_as_handled()
				KEY_PERIOD:
					_begin_held_step(1)
					get_viewport().set_input_as_handled()
				KEY_0:
					_set_field_strength(0.0)
					get_viewport().set_input_as_handled()
				KEY_1:
					_set_field_strength(1.0)
					get_viewport().set_input_as_handled()
		elif (event.keycode == KEY_COMMA and _held_direction < 0) or (event.keycode == KEY_PERIOD and _held_direction > 0):
			_held_direction = 0
			_repeat_timer = 0.0


func _process(delta: float) -> void:
	if not _input_enabled or _held_direction == 0:
		return
	_repeat_timer -= delta
	if _repeat_timer > 0.0:
		return
	_step_field(_held_direction)
	_repeat_timer = FIELD_DIAL_REPEAT_S


func SetInputEnabled(enabled: bool) -> void:
	_input_enabled = enabled
	if not enabled:
		_held_direction = 0
		_repeat_timer = 0.0


func ToggleDisplayPreset() -> void:
	ToggleExperiment()


func ToggleExperiment() -> void:
	var next_experiment := EXPERIMENT_HERMETIC if _experiment == EXPERIMENT_GALLERY else EXPERIMENT_GALLERY
	_set_experiment(next_experiment, true)


func SetDisplayPresetForTesting(preset: String) -> void:
	_set_experiment(EXPERIMENT_HERMETIC if preset.to_lower() == "hermetic" else EXPERIMENT_GALLERY, true)


func SetFieldStrengthForTesting(value: float) -> void:
	_set_field_strength(value)


func GetFieldStrength() -> float:
	return _field_strength


func GetFieldStateName() -> String:
	if is_equal_approx(_field_strength, 0.0):
		return "STRAIGHT"
	if is_equal_approx(_field_strength, 1.0):
		return "FULL"
	return "SCALED"


func GetDisplayPresetName() -> String:
	return _experiment


func GetExperimentName() -> String:
	return _experiment


func GetReferenceAmp() -> float:
	var field := _get_active_reference_field()
	if field == null:
		return 0.0
	return float(field.get("Amp"))


func GetBendScale() -> float:
	if _ray_renderer == null:
		return 0.0
	return float(_ray_renderer.get("BendScale"))


func _begin_held_step(direction: int) -> void:
	_held_direction = direction
	_repeat_timer = FIELD_DIAL_REPEAT_S
	_step_field(direction)


func _step_field(direction: int) -> void:
	_set_field_strength(_field_strength + float(direction) * FIELD_STEP)


func _set_field_strength(value: float) -> void:
	var next_value: float = clamp(snappedf(value, FIELD_STEP), FIELD_MIN, FIELD_MAX)
	if is_equal_approx(next_value, _field_strength):
		_update_display()
		return
	_field_strength = next_value
	_apply_field_strength(true)
	_update_display()


func _apply_field_strength(notify: bool) -> void:
	if _ray_renderer != null:
		_ray_renderer.set("FieldStrength", _field_strength)
	if notify:
		field_strength_changed.emit(_field_strength)
		if _film_controller != null and _film_controller.has_method("NotifyFieldStrengthChanged"):
			_film_controller.call("NotifyFieldStrengthChanged")


func _set_experiment(experiment: String, invalidate_film: bool) -> void:
	_experiment = experiment
	var hermetic_active := _experiment == EXPERIMENT_HERMETIC
	if _gallery_collision != null:
		_gallery_collision.visible = not hermetic_active
		_set_collision_enabled(_gallery_collision, not hermetic_active)
	if _overspace_root != null:
		# The Gallery apparatus is likewise experiment-owned, not presentation-owned.
		_overspace_root.visible = not hermetic_active
	for path in gallery_field_paths:
		var field := get_node_or_null(path)
		if field != null:
			field.set("Enabled", not hermetic_active)
	if _hermetic_display != null:
		# HermeticRoomDisplay is experiment-owned apparatus. H never controls
		# this eligibility; it follows E exactly.
		_hermetic_display.visible = hermetic_active
		_set_collision_enabled(_hermetic_display, hermetic_active)
	if _hermetic_field != null:
		_hermetic_field.set("Enabled", hermetic_active)
	if _player != null and _player.has_method("ApplyCameraTransform"):
		_player.call("ApplyCameraTransform", _hermetic_camera_transform if hermetic_active else _gallery_camera_transform)
	if invalidate_film and _film_controller != null and _film_controller.has_method("NotifyCameraTransformJump"):
		_film_controller.call("NotifyCameraTransformJump")
	_update_display()


func _set_collision_enabled(root: Node, enabled: bool) -> void:
	if root is CollisionObject3D:
		root.set_deferred("process_mode", Node.PROCESS_MODE_INHERIT if enabled else Node.PROCESS_MODE_DISABLED)
		root.set_deferred("collision_layer", 1 if enabled else 0)
		root.set_deferred("collision_mask", 1 if enabled else 0)
	for child in root.get_children():
		_set_collision_enabled(child, enabled)


func _update_display() -> void:
	if _field_bar != null:
		_field_bar.value = _field_strength
	if _field_value_label == null:
		return
	var state := GetFieldStateName()
	if state == "STRAIGHT":
		_field_value_label.text = "Field: 0.00 · STRAIGHT"
	elif state == "FULL":
		_field_value_label.text = "Field: 1.00 · FULL"
	else:
		_field_value_label.text = "Field: %0.2f" % _field_strength


func _get_active_reference_field() -> Node:
	if _experiment == EXPERIMENT_HERMETIC:
		return _hermetic_field
	for path in gallery_field_paths:
		var field := get_node_or_null(path)
		if field != null:
			return field
	return null
