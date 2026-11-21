extends SceneTree

# FIELDVIZ-R0.2.1 headless test: Workbench card attribution under constrained width.
# A nonzero unsupported-shape count must stay visible on the card's F line at every width, while the card's
# other readouts are unchanged. Configurations are test-local (presentation attribution only).
# Usage: godot --headless --path <ws> --script res://scripts/test_workbench_field_attribution.gd

const WB := "res://ObservatoryWorkbench.tscn"
const AUTHORED_CARD_WIDTH := 328.0
const MARGINS := 24.0
const WIDTHS := [600.0, 328.0, 300.0, 280.0, 260.0, 240.0, 220.0]

var failures: Array[String] = []
var cases := 0
var film: Node
var ctrl: Node
var card: Control
var status: Label


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	change_scene_to_file(WB)
	await scene_changed
	await _frames(30)
	film = current_scene.find_children("GrinFilmCamera", "", true, false)[0]
	ctrl = film.get_parent().get_node("FilmController")
	card = film.get_parent().get_node("CanvasLayer/HUDRoot/InstrumentStatusCard")
	status = card.get_node("Margin/Content/Status") as Label
	var sources := []
	for source in get_nodes_in_group("field_sources"):
		if bool(source.get("DebugVizEnabled")):
			sources.append(source)
	var disabled: Node = sources[0]
	var box: Node = null
	for source in sources:
		if source != disabled and bool(source.get("Enabled")):
			box = source
			break
	disabled.set("Enabled", false)
	box.set("ShapeType", 1)  # BoxVolume: extents and vectors only; no density rays
	await _frames(3)
	# Zero-unsupported control in Vectors: unchanged R0.2 behaviour.
	await _f_to("Vectors")
	await _check_widths("Vectors")
	await _f_to("Rays")
	var presentation: Dictionary = film.call("GetFieldStructurePresentation")
	cases += 1
	_check(int(presentation.get("rays_unsupported", 0)) == 1, "Rays attributes one unsupported shape (%s)" % [presentation])
	await _check_widths("Rays")
	# A longer F line (sealed profile prefix) is a real constraint: seal, then re-check Rays.
	ctrl.call("EnterFormalSnapshotFromInput")
	for _i in range(9000):
		if bool(film.get("ProbeViewAvailable")) and not bool(film.get("CathedralSnapshotLifecycleActive")):
			break
		await process_frame
	cases += 1
	_check(bool(film.get("ProbeViewAvailable")), "sealed for the long-prefix case")
	await _check_widths("Rays sealed")
	_restore_width(AUTHORED_CARD_WIDTH)
	await _frames(2)
	if failures.is_empty():
		print("WORKBENCH_FIELD_ATTRIBUTION PASS cases=%d server=%s" % [cases, DisplayServer.get_name()])
		quit(0)
	else:
		for failure in failures:
			push_error("WORKBENCH_FIELD_ATTRIBUTION FAIL: " + failure)
		print("WORKBENCH_FIELD_ATTRIBUTION FAIL count=%d cases=%d first=%s" % [failures.size(), cases, failures[0]])
		quit(1)


func _check_widths(label: String) -> void:
	var presentation: Dictionary = film.call("GetFieldStructurePresentation")
	var unsupported := int(presentation.get("rays_unsupported", 0)) + int(presentation.get("vectors_unsupported", 0))
	for width in WIDTHS:
		_restore_width(width)
		await _frames(2)
		card.call("ForceRefreshForTesting")
		await process_frame
		var state: Dictionary = card.call("GetDisplayedStateForTesting")
		var details := str(state.get("field_structure_details"))
		var text := str(state.get("status_text"))
		var first_line := text.split("\n")[0]
		var text_lines := text.split("\n").size()
		var prefix := first_line.trim_suffix(details)
		var minimal := " (%d unsupported)" % unsupported
		var minimal_fits := _fits(prefix + minimal)
		var wraps := status.get_line_count() != text_lines
		print("WORKBENCH_FIELD_ATTRIBUTION %s width=%.0f status=%.1f unsupported=%d details=%s label_lines=%d text_lines=%d minimal_fits=%s" % [
			label, width, status.size.x, unsupported, details, status.get_line_count(), text_lines, minimal_fits])
		cases += 1
		_check(first_line.ends_with(details), "%s @%.0f: details ride on the F line" % [label, width])
		if unsupported > 0:
			_check(details.contains("%d unsupported" % unsupported), "%s @%.0f: nonzero unsupported count is visible (%s)" % [label, width, details])
			if minimal_fits:
				_check(not wraps, "%s @%.0f: attribution fits without wrapping" % [label, width])
		else:
			_check(not details.contains("unsupported"), "%s @%.0f: no unsupported text when the count is zero" % [label, width])
			if _fits(prefix):
				_check(not wraps, "%s @%.0f: zero-unsupported line never wraps (greedy fit unchanged)" % [label, width])
		if width >= 600.0 and unsupported > 0 and label == "Rays":
			_check(details == " (illustrative · 1 disabled not drawn · 1 unsupported shape)", "unconstrained: unchanged full R0.2 text and order (%s)" % details)
		# Other scientific readouts unchanged by the attribution logic.
		# Sealed SNAPSHOT shows "N LIVE shading: … · Q image unchanged" in place of "N Shading:".
		_check(text.contains("G Film:") and (text.contains("N Shading:") or text.contains("N LIVE shading:")), "%s @%.0f: other readouts present" % [label, width])


func _restore_width(width: float) -> void:
	status.custom_minimum_size.x = width - MARGINS
	(card.get_node("Margin/Content/SecondaryControls") as Control).custom_minimum_size.x = width - MARGINS
	card.custom_minimum_size.x = width
	card.size = Vector2(width, card.size.y)


func _fits(line: String) -> bool:
	var font := status.get_theme_font("font")
	var size := status.get_theme_font_size("font_size")
	return font.get_string_size(line, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x <= status.size.x


func _f_to(mode_name: String) -> void:
	for _i in range(4):
		if str(film.get("CurrentFieldStructureModeName")) == mode_name:
			return
		await _key(KEY_F)
	_check(str(film.get("CurrentFieldStructureModeName")) == mode_name, "F reaches %s" % mode_name)


func _key(keycode: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	Input.parse_input_event(event)
	await _frames(3)
	var release := event.duplicate() as InputEventKey
	release.pressed = false
	Input.parse_input_event(release)
	await process_frame


func _frames(count: int) -> void:
	for _i in range(count):
		await process_frame


func _check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
