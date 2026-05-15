extends PanelContainer

@onready var _recipe_options: OptionButton = $Margin/Root/Top/RecipeColumn/RecipeOptions
@onready var _quality_options: OptionButton = $Margin/Root/Top/ControlColumn/QualityOptions
@onready var _overlay_options: OptionButton = $Margin/Root/Top/ControlColumn/OverlayOptions
@onready var _placement_options: OptionButton = $Margin/Root/Top/ControlColumn/PlacementOptions
@onready var _guardrail_label: Label = $Margin/Root/Body/InfoColumn/GuardrailText
@onready var _status_label: Label = $Margin/Root/Header/StatusLabel
@onready var _command_text: TextEdit = $Margin/Root/Body/InfoColumn/CommandText
@onready var _log_text: TextEdit = $Margin/Root/Body/LogColumn/LogText
@onready var _folder_label: Label = $Margin/Root/Body/ArtifactColumn/FolderLabel
@onready var _report_label: Label = $Margin/Root/Body/ArtifactColumn/ReportLabel
@onready var _contact_label: Label = $Margin/Root/Body/ArtifactColumn/ContactLabel
@onready var _preview_label: Label = $Margin/Root/Body/ArtifactColumn/PreviewLabel
@onready var _preview_texture: TextureRect = $Margin/Root/Body/ArtifactColumn/PreviewTexture
@onready var _dry_run_button: Button = $Margin/Root/Actions/DryRunButton
@onready var _run_button: Button = $Margin/Root/Actions/RunButton
@onready var _atomic_quick_button: Button = $Margin/Root/Actions/AtomicQuickButton
@onready var _stop_button: Button = $Margin/Root/Actions/StopButton
@onready var _copy_button: Button = $Margin/Root/Actions/CopyButton
@onready var _open_folder_button: Button = $Margin/Root/Actions/OpenFolderButton
@onready var _open_report_button: Button = $Margin/Root/Actions/OpenReportButton
@onready var _open_contact_button: Button = $Margin/Root/Actions/OpenContactButton
@onready var _controller: Node = $TestBenchController

var _recipes: Array = []
var _active_manifest: Dictionary = {}
var _poll_timer: Timer
var _preview_path := ""


func _ready() -> void:
	_load_recipes()
	_wire_buttons()
	_setup_poll_timer()
	_refresh_recipe_dependent_options()
	_update_status("Ready.")


func _load_recipes() -> void:
	_recipes.clear()
	_recipe_options.clear()
	var parsed = JSON.parse_string(str(_controller.call("GetRecipeCatalogJson")))
	if typeof(parsed) != TYPE_ARRAY:
		_update_status("Recipe registry unavailable.")
		return

	_recipes = parsed
	for recipe in _recipes:
		_recipe_options.add_item(str(recipe.get("title", recipe.get("id", "recipe"))))


func _wire_buttons() -> void:
	_recipe_options.item_selected.connect(func(_idx: int) -> void: _refresh_recipe_dependent_options())
	_dry_run_button.pressed.connect(_on_dry_run_pressed)
	_run_button.pressed.connect(_on_run_pressed)
	_atomic_quick_button.pressed.connect(_on_atomic_quick_pressed)
	_stop_button.pressed.connect(_on_stop_pressed)
	_copy_button.pressed.connect(_on_copy_pressed)
	_open_folder_button.pressed.connect(_on_open_folder_pressed)
	_open_report_button.pressed.connect(_on_open_report_pressed)
	_open_contact_button.pressed.connect(_on_open_contact_pressed)


func _setup_poll_timer() -> void:
	_poll_timer = Timer.new()
	_poll_timer.wait_time = 0.35
	_poll_timer.one_shot = false
	_poll_timer.timeout.connect(_poll_controller)
	add_child(_poll_timer)
	_poll_timer.start()


func _selected_recipe() -> Dictionary:
	if _recipes.is_empty():
		return {}
	var idx = clamp(_recipe_options.selected, 0, _recipes.size() - 1)
	return _recipes[idx]


func _selected_text(options: OptionButton) -> String:
	if options.item_count <= 0:
		return ""
	return options.get_item_text(max(0, options.selected))


func _refresh_recipe_dependent_options() -> void:
	var recipe := _selected_recipe()
	_overlay_options.clear()
	_placement_options.clear()
	_quality_options.clear()
	for quality in recipe.get("qualityPresets", ["smoke", "review", "full"]):
		_quality_options.add_item(str(quality))
	var default_quality := str(recipe.get("defaultQuality", ""))
	for idx in range(_quality_options.item_count):
		if _quality_options.get_item_text(idx) == default_quality:
			_quality_options.select(idx)
			break
	for mode in recipe.get("allowedOverlayModes", ["none"]):
		_overlay_options.add_item(str(mode))
	for placement in recipe.get("allowedPlacements", ["none"]):
		_placement_options.add_item(str(placement))
	_guardrail_label.text = str(recipe.get("guardrailText", ""))
	_update_status("Selected: %s" % str(recipe.get("title", "recipe")))


func _on_dry_run_pressed() -> void:
	_invoke_plan("DryRunJson")


func _on_run_pressed() -> void:
	_invoke_plan("RunJson")


func _on_atomic_quick_pressed() -> void:
	if not _select_recipe_by_id("atomic_visual_observatory"):
		_update_status("Atomic Visual recipe is not available.")
		return
	_select_option_by_text(_quality_options, "quick_review")
	_select_option_by_text(_overlay_options, "density_contours")
	_select_option_by_text(_placement_options, "contact_sheet_only")
	_invoke_plan("RunJson")


func _invoke_plan(method_name: String) -> void:
	var recipe := _selected_recipe()
	if recipe.is_empty():
		_update_status("No recipe selected.")
		return

	var result = JSON.parse_string(str(_controller.call(
		method_name,
		str(recipe.get("id", "")),
		_selected_text(_quality_options),
		_selected_text(_overlay_options),
		_selected_text(_placement_options)
	)))
	if typeof(result) != TYPE_DICTIONARY:
		_update_status("Controller returned an unreadable response.")
		return

	_update_status(str(result.get("message", "")))
	if result.has("manifest") and typeof(result["manifest"]) == TYPE_DICTIONARY:
		_active_manifest = result["manifest"]
		_render_manifest(_active_manifest)


func _on_stop_pressed() -> void:
	_update_status(str(_controller.call("StopRun")))


func _on_copy_pressed() -> void:
	_update_status(str(_controller.call("CopyExactCommand")))


func _on_open_folder_pressed() -> void:
	_update_status(str(_controller.call("OpenLatestFolder")))


func _on_open_report_pressed() -> void:
	_update_status(str(_controller.call("OpenReport")))


func _on_open_contact_pressed() -> void:
	_update_status(str(_controller.call("OpenContactSheet")))


func _poll_controller() -> void:
	if _controller == null:
		return
	_log_text.text = str(_controller.call("DrainLog"))
	_log_text.scroll_vertical = _log_text.get_line_count()
	var status = JSON.parse_string(str(_controller.call("GetStatusJson")))
	if typeof(status) == TYPE_DICTIONARY:
		_run_button.disabled = bool(status.get("running", false))
		_atomic_quick_button.disabled = bool(status.get("running", false))
		_stop_button.disabled = not bool(status.get("running", false))
		if status.has("manifest") and typeof(status["manifest"]) == TYPE_DICTIONARY:
			_active_manifest = status["manifest"]
			_render_manifest(_active_manifest)


func _render_manifest(manifest: Dictionary) -> void:
	_command_text.text = str(manifest.get("exact_command", ""))
	var artifacts: Dictionary = manifest.get("artifacts", {})
	_folder_label.text = "Folder: %s" % str(artifacts.get("folder", manifest.get("planned_output_folder", "")))
	_report_label.text = "Report: %s" % str(artifacts.get("report", ""))
	_contact_label.text = "Contact sheet: %s" % str(artifacts.get("contact_sheet", ""))
	_preview_label.text = "Preview: %s" % str(artifacts.get("preview", ""))
	var preview_path := str(artifacts.get("contact_sheet", ""))
	if preview_path.is_empty():
		preview_path = str(artifacts.get("preview", ""))
	_update_preview_texture(preview_path)
	_apply_status_color(manifest)
	var summary := str(manifest.get("cockpit_summary", ""))
	if not summary.is_empty():
		_status_label.text = summary


func _update_status(text: String) -> void:
	_status_label.text = text
	_status_label.remove_theme_color_override("font_color")


func _select_recipe_by_id(recipe_id: String) -> bool:
	for idx in range(_recipes.size()):
		if str(_recipes[idx].get("id", "")) == recipe_id:
			_recipe_options.select(idx)
			_refresh_recipe_dependent_options()
			return true
	return false


func _select_option_by_text(options: OptionButton, text: String) -> bool:
	for idx in range(options.item_count):
		if options.get_item_text(idx) == text:
			options.select(idx)
			return true
	return false


func _apply_status_color(manifest: Dictionary) -> void:
	var status := str(manifest.get("status", ""))
	var exit_code := int(manifest.get("exit_code", -999))
	if status == "completed" and exit_code == 0:
		_status_label.add_theme_color_override("font_color", Color(0.45, 0.95, 0.58))
	elif status == "incomplete" or status == "blocked_full_requires_smoke":
		_status_label.add_theme_color_override("font_color", Color(1.0, 0.82, 0.35))
	elif status == "completed_with_errors":
		_status_label.add_theme_color_override("font_color", Color(1.0, 0.38, 0.32))
	else:
		_status_label.remove_theme_color_override("font_color")


func _update_preview_texture(path: String) -> void:
	if path.is_empty():
		_preview_path = ""
		_preview_texture.texture = null
		return
	if path == _preview_path:
		return
	_preview_path = path
	if not FileAccess.file_exists(path):
		_preview_texture.texture = null
		return
	var image := Image.new()
	var err := image.load(path)
	if err != OK:
		_preview_texture.texture = null
		return
	_preview_texture.texture = ImageTexture.create_from_image(image)
