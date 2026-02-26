extends PanelContainer

const KEY_SMART_ENABLED := "xprime/autocal/smart_enabled"
const KEY_SHADOW_EVAL := "xprime/autocal/shadow_eval_enabled"
const KEY_AUTO_APPLY := "xprime/autocal/auto_apply"
const KEY_VERBOSE := "xprime/autocal/verbose"

@onready var _smart_enabled_check: CheckBox = $MarginContainer/VBoxContainer/SmartEnabledCheck
@onready var _shadow_eval_check: CheckBox = $MarginContainer/VBoxContainer/ShadowEvalCheck
@onready var _auto_apply_check: CheckBox = $MarginContainer/VBoxContainer/AutoApplyCheck
@onready var _verbose_check: CheckBox = $MarginContainer/VBoxContainer/VerboseCheck
@onready var _preview_button: Button = $MarginContainer/VBoxContainer/FooterRow/PreviewNowButton
@onready var _status_label: Label = $MarginContainer/VBoxContainer/FooterRow/StatusLabel
@onready var _bridge_node: Node = $AutoCalBridgeNode
var _status_poll_timer: Timer


func _ready() -> void:
	_load_settings_into_ui()

	_smart_enabled_check.toggled.connect(_on_toggle_changed)
	_shadow_eval_check.toggled.connect(_on_toggle_changed)
	_auto_apply_check.toggled.connect(_on_toggle_changed)
	_verbose_check.toggled.connect(_on_toggle_changed)
	_preview_button.pressed.connect(_on_preview_now_pressed)
	_setup_status_poll_timer()

	_refresh_status()
	_apply_settings_live()


func _load_settings_into_ui() -> void:
	_smart_enabled_check.button_pressed = _get_bool_setting(KEY_SMART_ENABLED, false)
	_shadow_eval_check.button_pressed = _get_bool_setting(KEY_SHADOW_EVAL, false)
	_auto_apply_check.button_pressed = _get_bool_setting(KEY_AUTO_APPLY, false)
	_verbose_check.button_pressed = _get_bool_setting(KEY_VERBOSE, false)


func _get_bool_setting(key: String, default_value: bool) -> bool:
	if not ProjectSettings.has_setting(key):
		return default_value

	return bool(ProjectSettings.get_setting(key))


func _on_toggle_changed(_pressed: bool) -> void:
	_save_settings()
	_refresh_status()
	_apply_settings_live()


func _on_preview_now_pressed() -> void:
	_apply_settings_live()
	_refresh_status()


func _save_settings() -> void:
	ProjectSettings.set_setting(KEY_SMART_ENABLED, _smart_enabled_check.button_pressed)
	ProjectSettings.set_setting(KEY_SHADOW_EVAL, _shadow_eval_check.button_pressed)
	ProjectSettings.set_setting(KEY_AUTO_APPLY, _auto_apply_check.button_pressed)
	ProjectSettings.set_setting(KEY_VERBOSE, _verbose_check.button_pressed)
	ProjectSettings.save()


func _refresh_status() -> void:
	if _bridge_node != null and _bridge_node.has_method("GetStatusLine"):
		var status_line := str(_bridge_node.call("GetStatusLine"))
		_status_label.text = status_line if not status_line.is_empty() else ("AutoCal: smart=%s" % ("on" if _smart_enabled_check.button_pressed else "off"))
		return

	_status_label.text = "AutoCal: smart=%s" % ("on" if _smart_enabled_check.button_pressed else "off")


func _setup_status_poll_timer() -> void:
	_status_poll_timer = Timer.new()
	_status_poll_timer.wait_time = 0.5
	_status_poll_timer.one_shot = false
	_status_poll_timer.autostart = true
	_status_poll_timer.timeout.connect(_refresh_status)
	add_child(_status_poll_timer)
	_status_poll_timer.start()


func _apply_settings_live() -> void:
	if _bridge_node != null and _bridge_node.has_method("ApplySettings"):
		_bridge_node.call(
			"ApplySettings",
			_smart_enabled_check.button_pressed,
			_shadow_eval_check.button_pressed,
			_auto_apply_check.button_pressed,
			_verbose_check.button_pressed
		)
