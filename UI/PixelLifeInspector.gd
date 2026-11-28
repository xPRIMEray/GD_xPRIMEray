extends PanelContainer

# INSPECT-R0.2 Pixel Life Inspector (Phase 1). Read-only symbolic causal timeline of recorded per-pixel
# transport events from a completed, sealed Formal acquisition. Data comes only from
# GrinFilmCamera.GetPixelLifeInspectionJson (whole-film validated against the seal, nothing written).
# Nothing here issues queries, transports, or writes recorder or acquisition state; the refresh timer only
# reads the sealed generation and lifecycle getters already polled by the instrument card.
#
# Lanes per pixel (shared axes across the compared pixels):
#   path length  - DERIVED from recorded chords: queried vs. unqueried intervals (unqueried is hatched and
#                  never implies collision-free space).
#   integration step - RECORDED events in recorder order: question ticks, remap / suppression / egress,
#                  contact (unqualified diagnostic witness) and terminal markers.

const REFRESH_SECONDS := 0.25
const ACCEPTANCE_ROWS: Array[int] = [4839, 4840, 4841]
const ROW_HEIGHT := 60.0
const ROW_HEIGHT_STACKED := 74.0
const LABEL_WIDTH := 168.0
const AXIS_HEIGHT := 18.0
const BOTTOM_AXIS_HEIGHT := 30.0

const C_TEXT := Color(0.94, 0.97, 1.0)
const C_DIM := Color(0.62, 0.69, 0.78)
const C_WARN := Color(1.0, 0.78, 0.42)
const C_QUERIED := Color(0.24, 0.66, 0.38)
const C_UNQUERIED := Color(0.22, 0.24, 0.27)
const C_HATCH := Color(0.42, 0.45, 0.5)
const C_QUESTION := Color(0.6, 0.64, 0.7)
const C_REMAP := Color(0.33, 0.6, 1.0)
const C_SUPPRESS := Color(0.78, 0.42, 0.95)
const C_CROSSING := Color(0.36, 0.86, 0.9)
const C_CONTACT := Color(1.0, 0.33, 0.33)
const C_TERMINAL := Color(1.0, 1.0, 1.0)
const C_FAILED := Color(1.0, 0.58, 0.2)
const C_OTHER := Color(0.85, 0.8, 0.55)
const C_SELECT := Color(1.0, 0.88, 0.3)
const C_FOCUS_BG := Color(0.36, 0.58, 0.72, 0.12)

const REASON_TEXT := {
	"formal_transport_observation_unavailable": "No completed, sealed Formal acquisition. Take one first (G to SNAPSHOT, then Q). Nothing is shown until a seal exists.",
	"recorder_disabled_for_acquisition": "The ordered transport recorder was OFF for this acquisition (GrinFilmCamera.RecordFormalTransportLedger). Per-pixel events were not recorded and are not reconstructed.",
	"formal_acquisition_active": "A Formal acquisition is in progress. The inspector reads only completed, sealed acquisitions.",
	"recorder_unavailable": "The transport recorder is unavailable.",
	"camera_unavailable": "The Formal camera is unavailable.",
}

@export var film_camera_path: NodePath
@export var film_view_path: NodePath

@onready var _title: Label = $Margin/Content/Title
@onready var _identity: Label = $Margin/Content/Identity
@onready var _warnings: Label = $Margin/Content/Warnings
@onready var _locator: Control = $Margin/Content/Top/Locator
@onready var _summary: Label = $Margin/Content/Top/Summary
@onready var _ledger: Control = $Margin/Content/Ledger
@onready var _detail: Label = $Margin/Content/Detail
@onready var _legend: Control = $Margin/Content/Legend
@onready var _timer: Timer = $RefreshTimer

var _film: Node
var _film_view: TextureRect
var _open := false
var _rows: Array[int] = ACCEPTANCE_ROWS.duplicate()
var _focus_row := 1
var _selected_event := -1          # event_ordinal within the focused pixel, -1 = none
var _show_unavailable := false
var _doc: Dictionary = {}
var _pixels: Dictionary = {}       # pixel_index -> pixel dictionary (recorded / derived / unavailable)
var _fetched_key := ""
var _fetch_count := 0
var _last_fetch_ms := 0.0
var _hit_targets: Array = []       # [row, Vector2 position, event_ordinal]
var _film_width := 160
var _film_height := 90


func _ready() -> void:
	_film = get_node_or_null(film_camera_path)
	_film_view = get_node_or_null(film_view_path) as TextureRect
	_timer.wait_time = REFRESH_SECONDS
	_timer.timeout.connect(_on_refresh_timer)
	_ledger.draw.connect(_draw_ledger)
	_locator.draw.connect(_draw_locator)
	_legend.draw.connect(_draw_legend)
	_ledger.gui_input.connect(_on_ledger_input)
	_locator.gui_input.connect(_on_locator_input)
	get_viewport().size_changed.connect(_apply_layout)
	visible = false
	_apply_layout()


func SetOpen(open: bool) -> void:
	_open = open
	visible = open
	if open:
		_timer.start()
		_refresh(false)
	else:
		_timer.stop()


func IsOpen() -> bool:
	return _open


func _on_refresh_timer() -> void:
	_refresh(false)


# Fetches only when the sealed generation, lifecycle or selection changed; otherwise it only re-reads getters.
func _refresh(force: bool) -> void:
	if _film == null:
		_doc = {"status": "unavailable", "reason": "camera_unavailable"}
		_pixels = {}
		_render()
		return
	var key := _state_key()
	if force or key != _fetched_key:
		_fetch(key)
	_render()


func _state_key() -> String:
	var sealed := bool(_film.get("ProbeViewAvailable"))
	var generation := int(_film.get("SealedProbeViewGeneration"))
	var active := bool(_film.get("CathedralSnapshotLifecycleActive"))
	var state := str(_film.get("CathedralSnapshotLifecycleStateName"))
	return "%s|%d|%s|%s|%s" % [sealed, generation, active, state, ",".join(PackedStringArray(_rows.map(func(p): return str(p))))]


func _fetch(key: String) -> void:
	var started := Time.get_ticks_usec()
	var text := str(_film.call("GetPixelLifeInspectionJson", PackedInt32Array(_rows)))
	_last_fetch_ms = float(Time.get_ticks_usec() - started) / 1000.0
	_fetch_count += 1
	_fetched_key = key
	_hit_targets.clear()
	var parsed = JSON.parse_string(text)
	_doc = parsed if parsed is Dictionary else {"status": "unavailable", "reason": "inspection_document_unreadable"}
	_pixels = {}
	if str(_doc.get("status", "")) == "ok":
		var acquisition: Dictionary = _doc.get("acquisition", {})
		_film_width = int(acquisition.get("width", _film_width))
		_film_height = int(acquisition.get("height", _film_height))
		for pixel in _doc.get("pixels", []):
			_pixels[int(pixel.get("pixel_index", -1))] = pixel
	if not _event_exists(_selected_event):
		_selected_event = -1


func _render() -> void:
	var ok := str(_doc.get("status", "")) == "ok"
	_title.text = "PIXEL LIFE · INSPECT · recorded transport events (provisional observation) · I / Esc close"
	if ok:
		var acquisition: Dictionary = _doc.get("acquisition", {})
		var validation: Dictionary = _doc.get("validation", {})
		var lifecycle := str(_film.get("CathedralSnapshotLifecycleStateName"))
		_identity.text = "SEALED %s · seal gen %d · capture gen %d · profile %s\npolicy %s %s · query %s · whole film validated %d px in %.0f ms" % [
			lifecycle.to_upper(), int(_film.get("SealedProbeViewGeneration")), int(acquisition.get("capture_generation", -1)),
			str(_film.get("CathedralSealedProbeProfileDisplayName")),
			str(acquisition.get("formal_policy_id", "?")), _short(acquisition.get("formal_policy_sha256", "")),
			_short(acquisition.get("canonical_query_dataset_sha256", "")),
			int(validation.get("validated_pixels", 0)), _last_fetch_ms]
		_warnings.text = "Not a sealed ledger · source not attested (interactive) · P1 field epoch omits parameters · P2 stepping outside policy hash · P4 process-local epochs\nFilm coordinates are not path coordinates · contacts are unqualified diagnostic witnesses, not terminal hits · unqueried ≠ collision-free · no 3D path is reconstructed"
	else:
		var reason := str(_doc.get("reason", "unknown"))
		_identity.text = "UNAVAILABLE · %s" % reason
		_warnings.text = REASON_TEXT.get(reason, "Validation against the seal refused this inspection (%s). Nothing is displayed or inferred." % reason)
	_summary.text = _summary_text() if ok else ""
	_locator.visible = ok
	_detail.text = _detail_text() if ok else ""
	_ledger.custom_minimum_size.y = (_row_height() * _rows.size() + AXIS_HEIGHT + BOTTOM_AXIS_HEIGHT) if ok else 0.0
	_ledger.queue_redraw()
	_locator.queue_redraw()
	_legend.queue_redraw()


# ---------- text ----------

func _summary_text() -> String:
	var index := _rows[_focus_row]
	var pixel: Dictionary = _pixels.get(index, {})
	if pixel.is_empty():
		return "px %d · not returned by the inspection (unavailable)" % index
	var r: Dictionary = pixel.get("recorded", {})
	var d: Dictionary = pixel.get("derived", {})
	var events: Array = r.get("events", [])
	var lines: Array[String] = []
	lines.append("px %d · film (%d,%d) · film coordinates, not a physical path position" % [index, int(pixel.get("x", -1)), int(pixel.get("y", -1))])
	lines.append("RECORDED  %s %s · steps %d · traveled %s (display) · questions %d · events %d · finalized %s · overflowed %s" % [
		str(r.get("termination", "?")), str(r.get("raw_terminal_flags", [])), int(r.get("steps_integrated", 0)),
		str(d.get("display_traveled", "?")), int(r.get("question_count", 0)), events.size(), str(r.get("finalized", false)), str(r.get("overflowed", false))])
	var first_step := int(r.get("sealed_replay_first_contact_step", -1))
	lines.append("RECORDED (sealed)  BVH contacts %d · first contact step %s (serial replay)" % [
		int(r.get("sealed_bvh_contact_count", 0)), "none" if first_step < 0 else str(first_step)])
	var before := int(d.get("remaps_before_first_contact", -2))
	lines.append("DERIVED  remaps %d · before first contact %s · queried path %s · max unqueried gap %s · contact events %d" % [
		int(d.get("portal_remap_count", 0)), "n/a" if before < 0 else str(before),
		_bits_display(str(d.get("queried_chord_bits", ""))), _bits_display(str(d.get("max_unqueried_gap_bits", ""))), int(d.get("contact_event_count", 0))])
	lines.append("UNAVAILABLE  step positions, field samples, per-question hit details, validated per-question authority · U list")
	return "\n".join(lines)


func _detail_text() -> String:
	if _show_unavailable:
		var items: Array = _doc.get("unavailable", [])
		var text := "UNAVAILABLE (not retained by the recorder; never inferred):"
		for item in items:
			text += "\n· " + str(item)
		return text
	var event := _selected_event_dict()
	if event.is_empty():
		return "Select an event: click a marker, or ←/→ (markers) · Shift+←/→ (every event) · ↑/↓ pixel row · Ctrl+arrows move pixel · U unavailable list · Home reset"
	var kind := str(event.get("kind", "?"))
	var lines: Array[String] = []
	lines.append("RECORDED  event #%d · step %d · segment %d · %s" % [int(event.get("event_ordinal", -1)), int(event.get("integration_step_ordinal", -1)),
		int(event.get("source_segment_ordinal", -1)), kind])
	# The qualification line comes first so it is never clipped on small viewports.
	if kind == "GeometryContactObserved":
		lines.append("DIAGNOSTIC GODOT WITNESS · not a qualified terminal hit · no position or normal recorded · DERIVED join: question #%d (same step and segment)" % int(event.get("derived_contact_question_ordinal", -1)))
	elif kind == "Terminal":
		lines.append("Recorded transport termination · not a qualified surface hit")
	elif kind == "SuppressedMaskTransition" or kind == "BoundarySyntheticEgress":
		lines.append("Boundary bookkeeping recorded at a sample · crossings between samples are unavailable (point-sampled)")
	elif kind == "ProbeQuestionIssued" and event.has("derived_question_ordinal"):
		lines.append("DERIVED  question #%d of this pixel · per-question authority unavailable" % int(event.get("derived_question_ordinal")))
	lines.append("subject %s (identity %s%s) · decision %s · reason %s · observability %s%s" % [
		str(event.get("subject_kind", "?")), str(event.get("identity_availability", "?")),
		"" if event.get("canonical_subject_id") == null else " " + str(event.get("canonical_subject_id")),
		str(event.get("decision", "?")), str(event.get("reason", "?")), str(event.get("observability", [])),
		"" if (event.get("flags", []) as Array).is_empty() else " · flags %s" % str(event.get("flags", []))])
	var payload: Array[String] = []
	for key in ["pre_position_bits", "post_position_bits", "pre_direction_bits", "post_direction_bits", "contact_normal_bits", "path_distance_bits"]:
		if event.has(key):
			payload.append(key.trim_suffix("_bits"))
	lines.append("recorded payload: %s" % ("none (not recorded for this event)" if payload.is_empty() else ", ".join(payload) + " (raw bits; not plotted)"))
	return "\n".join(lines)


# ---------- drawing ----------

func _row_height() -> float:
	return ROW_HEIGHT_STACKED if _stacked() else ROW_HEIGHT


func _stacked() -> bool:
	return _ledger.size.x < 560.0


func _lane_x0() -> float:
	return 6.0 if _stacked() else LABEL_WIDTH


func _shared_axes() -> Vector2:
	var max_steps := 1
	var max_traveled := 0.0001
	for index in _rows:
		var pixel: Dictionary = _pixels.get(index, {})
		if pixel.is_empty():
			continue
		max_steps = maxi(max_steps, int(pixel.get("recorded", {}).get("steps_integrated", 0)))
		max_traveled = maxf(max_traveled, float(str(pixel.get("derived", {}).get("display_traveled", "0"))))
	return Vector2(max_steps, max_traveled)


func _draw_ledger() -> void:
	_hit_targets.clear()
	if str(_doc.get("status", "")) != "ok":
		return
	var font := get_theme_default_font()
	var w := _ledger.size.x
	var x0 := _lane_x0()
	var x1 := w - 8.0
	var axes := _shared_axes()
	var max_steps := axes.x
	var max_traveled := axes.y
	var rh := _row_height()
	# top axis: path length (derived)
	_ledger.draw_string(font, Vector2(x0, 12.0), "path length · DERIVED from recorded chords · 0 – %.3f (shared)" % max_traveled, HORIZONTAL_ALIGNMENT_LEFT, -1, 10, C_DIM)
	var selected_step := -1
	var selected := _selected_event_dict()
	if not selected.is_empty():
		selected_step = int(selected.get("integration_step_ordinal", -1))
	for row in _rows.size():
		var y0 := AXIS_HEIGHT + row * rh
		var index := _rows[row]
		var pixel: Dictionary = _pixels.get(index, {})
		if row == _focus_row:
			_ledger.draw_rect(Rect2(0.0, y0, w, rh - 2.0), C_FOCUS_BG)
		var label_y := y0 + 12.0
		if pixel.is_empty():
			_ledger.draw_string(font, Vector2(6.0, label_y), "px %d · unavailable" % index, HORIZONTAL_ALIGNMENT_LEFT, -1, 11, C_WARN)
			continue
		var r: Dictionary = pixel.get("recorded", {})
		var d: Dictionary = pixel.get("derived", {})
		var head := "px %d (%d,%d)" % [index, int(pixel.get("x", -1)), int(pixel.get("y", -1))]
		var counts := "q %d · remap %d · contact %d" % [int(r.get("question_count", 0)), int(d.get("portal_remap_count", 0)), int(d.get("contact_event_count", 0))]
		var term := "%s · steps %d" % [str(r.get("termination", "?")), int(r.get("steps_integrated", 0))]
		var lane_top := y0
		if _stacked():
			_ledger.draw_string(font, Vector2(6.0, label_y), "%s · %s · %s" % [head, counts, term], HORIZONTAL_ALIGNMENT_LEFT, w - 12.0, 11, C_TEXT)
			lane_top = y0 + 18.0
		else:
			_ledger.draw_string(font, Vector2(6.0, label_y), head, HORIZONTAL_ALIGNMENT_LEFT, LABEL_WIDTH - 10.0, 12, C_TEXT)
			_ledger.draw_string(font, Vector2(6.0, label_y + 15.0), counts, HORIZONTAL_ALIGNMENT_LEFT, LABEL_WIDTH - 10.0, 10, C_DIM)
			_ledger.draw_string(font, Vector2(6.0, label_y + 29.0), term, HORIZONTAL_ALIGNMENT_LEFT, LABEL_WIDTH - 10.0, 10, C_DIM)
		# path lane (derived)
		var py := lane_top + 6.0
		var traveled := float(str(d.get("display_traveled", "0")))
		var span := x1 - x0
		var end_x := x0 + span * clampf(traveled / max_traveled, 0.0, 1.0)
		for interval in d.get("unqueried_path_intervals", []):
			var a := x0 + span * float(str(interval[0])) / max_traveled
			var b := x0 + span * float(str(interval[1])) / max_traveled
			_draw_hatched(Rect2(a, py, maxf(b - a, 1.0), 9.0))
		for interval in d.get("queried_path_intervals", []):
			var a := x0 + span * float(str(interval[0])) / max_traveled
			var b := x0 + span * float(str(interval[1])) / max_traveled
			_ledger.draw_rect(Rect2(a, py, maxf(b - a, 1.0), 9.0), C_QUERIED)
		_ledger.draw_line(Vector2(end_x, py - 2.0), Vector2(end_x, py + 11.0), C_DIM, 1.0)
		# step lane (recorded)
		var base := lane_top + 36.0
		var step_end := x0 + span * float(int(r.get("steps_integrated", 0))) / max_steps
		_ledger.draw_line(Vector2(x0, base), Vector2(step_end, base), Color(C_QUESTION, 0.5), 1.0)
		var stack := {}
		for event in r.get("events", []):
			var kind := str(event.get("kind", ""))
			var step := int(event.get("integration_step_ordinal", 0))
			var x := x0 + span * float(step) / max_steps
			var ordinal := int(event.get("event_ordinal", -1))
			var pos := Vector2(x, base)
			if kind == "ProbeQuestionIssued":
				_ledger.draw_line(Vector2(x, base - 4.0), Vector2(x, base + 4.0), C_QUESTION, 1.0)
			else:
				var k := int(stack.get(step, 0))
				stack[step] = k + 1
				pos = Vector2(x, base - 12.0 + 8.0 * k)
				_draw_marker(kind, pos)
			_hit_targets.append([row, pos, ordinal])
			if row == _focus_row and ordinal == _selected_event:
				_ledger.draw_arc(pos, 7.0, 0.0, TAU, 20, C_SELECT, 1.5)
	# shared step cursor across rows
	if selected_step >= 0:
		var x := x0 + (x1 - x0) * float(selected_step) / max_steps
		var top := AXIS_HEIGHT
		var bottom := AXIS_HEIGHT + rh * _rows.size()
		var yy := top
		while yy < bottom:
			_ledger.draw_line(Vector2(x, yy), Vector2(x, minf(yy + 4.0, bottom)), Color(C_SELECT, 0.6), 1.0)
			yy += 8.0
	# bottom axis: integration step (recorded)
	var ay := AXIS_HEIGHT + rh * _rows.size() + 2.0
	var step := 10
	var s := 0
	while s <= int(max_steps):
		var x := x0 + (x1 - x0) * float(s) / max_steps
		_ledger.draw_line(Vector2(x, ay), Vector2(x, ay + 4.0), C_DIM, 1.0)
		_ledger.draw_string(font, Vector2(x + 2.0, ay + 13.0), str(s), HORIZONTAL_ALIGNMENT_LEFT, -1, 9, C_DIM)
		s += step
	_ledger.draw_string(font, Vector2(x0, ay + 26.0), "integration step · RECORDED · 0 – %d (shared)" % int(max_steps), HORIZONTAL_ALIGNMENT_LEFT, -1, 10, C_DIM)


func _draw_hatched(rect: Rect2) -> void:
	_ledger.draw_rect(rect, C_UNQUERIED)
	var x := rect.position.x - rect.size.y
	while x < rect.end.x:
		var a := Vector2(maxf(x, rect.position.x), rect.end.y - maxf(rect.position.x - x, 0.0))
		var bx := minf(x + rect.size.y, rect.end.x)
		var b := Vector2(bx, rect.end.y - (bx - x))
		if a.x < b.x:
			_ledger.draw_line(a, b, C_HATCH, 1.0)
		x += 4.0


func _draw_marker(kind: String, p: Vector2) -> void:
	match kind:
		"BoundaryRemapApplied":
			_ledger.draw_rect(Rect2(p.x - 1.0, p.y - 4.0, 2.5, 8.0), C_REMAP)
		"SuppressedMaskTransition", "BoundarySyntheticEgress":
			_ledger.draw_rect(Rect2(p.x - 1.0, p.y - 4.0, 2.5, 8.0), C_SUPPRESS)
		"BoundaryCrossingApplied":
			_ledger.draw_rect(Rect2(p.x - 1.0, p.y - 3.0, 2.5, 6.0), C_CROSSING)
		"GeometryContactObserved":
			var pts := PackedVector2Array([p + Vector2(0, -4.5), p + Vector2(4.5, 0), p + Vector2(0, 4.5), p + Vector2(-4.5, 0), p + Vector2(0, -4.5)])
			_ledger.draw_polyline(pts, C_CONTACT, 1.5)
		"Terminal":
			_ledger.draw_rect(Rect2(p.x - 1.0, p.y - 6.0, 3.0, 14.0), C_TERMINAL)
		"ProbeQueryFailed":
			_ledger.draw_line(p + Vector2(-3, -3), p + Vector2(3, 3), C_FAILED, 1.5)
			_ledger.draw_line(p + Vector2(-3, 3), p + Vector2(3, -3), C_FAILED, 1.5)
		_:
			_ledger.draw_circle(p, 2.5, C_OTHER)


func _draw_locator() -> void:
	var rect := Rect2(Vector2.ZERO, _locator.size)
	_locator.draw_rect(rect, Color(0, 0, 0, 0.6))
	var texture: Texture2D = _film_view.texture if _film_view != null else null
	if texture != null:
		_locator.draw_texture_rect(texture, rect, false, Color(1, 1, 1, 0.85))
	else:
		_locator.draw_string(get_theme_default_font(), Vector2(6, 16), "film plate unavailable", HORIZONTAL_ALIGNMENT_LEFT, -1, 10, C_DIM)
	var cw := rect.size.x / float(maxi(_film_width, 1))
	var ch := rect.size.y / float(maxi(_film_height, 1))
	for row in _rows.size():
		var index := _rows[row]
		var cell := Rect2(float(index % _film_width) * cw, float(index / _film_width) * ch, maxf(cw, 2.0), maxf(ch, 2.0))
		_locator.draw_rect(cell.grow(1.5), C_SELECT if row == _focus_row else Color(1, 1, 1, 0.8), false, 1.0)
	_locator.draw_rect(rect, Color(0.36, 0.58, 0.72, 0.5), false, 1.0)
	_locator.draw_string(get_theme_default_font(), Vector2(4, rect.size.y - 4), "film plate (presentation) · click to select", HORIZONTAL_ALIGNMENT_LEFT, rect.size.x - 8, 9, C_DIM)


func _draw_legend() -> void:
	var font := get_theme_default_font()
	var x := 4.0
	var y := 12.0
	var items := [
		["RECORDED", null, ""], ["question", C_QUESTION, "tick"], ["remap", C_REMAP, "bar"], ["suppressed/egress", C_SUPPRESS, "bar"],
		["crossing", C_CROSSING, "bar"], ["contact (unqualified)", C_CONTACT, "diamond"], ["terminal", C_TERMINAL, "bar"],
		["DERIVED", null, ""], ["queried path", C_QUERIED, "box"], ["unqueried (not collision-free)", C_UNQUERIED, "hatch"],
		["UNAVAILABLE", null, ""], ["U list", null, ""],
	]
	for item in items:
		var label := str(item[0])
		var color = item[1]
		var width := font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1, 10).x
		var needed := width + (16.0 if color != null else 4.0) + 10.0
		if x + needed > _legend.size.x:
			x = 4.0
			y += 15.0
		if color != null:
			_legend.draw_rect(Rect2(x, y - 8.0, 10.0, 9.0), color)
			x += 14.0
			_legend.draw_string(font, Vector2(x, y), label, HORIZONTAL_ALIGNMENT_LEFT, -1, 10, C_DIM)
		else:
			_legend.draw_string(font, Vector2(x, y), label, HORIZONTAL_ALIGNMENT_LEFT, -1, 10, C_WARN if label != "U list" else C_DIM)
		x += width + 12.0
	_legend.custom_minimum_size.y = y + 6.0


# ---------- input ----------

func _unhandled_key_input(event: InputEvent) -> void:
	if not _open or not (event is InputEventKey) or not event.pressed:
		return
	var key := event as InputEventKey
	var handled := true
	if key.ctrl_pressed and key.keycode in [KEY_LEFT, KEY_RIGHT, KEY_UP, KEY_DOWN]:
		_move_focused_pixel(key.keycode)
	elif key.keycode == KEY_UP:
		_set_focus_row(_focus_row - 1)
	elif key.keycode == KEY_DOWN:
		_set_focus_row(_focus_row + 1)
	elif key.keycode == KEY_RIGHT:
		_step_event(1, key.shift_pressed)
	elif key.keycode == KEY_LEFT:
		_step_event(-1, key.shift_pressed)
	elif key.keycode == KEY_U and not key.echo:
		_show_unavailable = not _show_unavailable
		_render()
	elif key.keycode == KEY_HOME and not key.echo:
		ResetToAcceptanceRows()
	else:
		handled = false
	if handled:
		get_viewport().set_input_as_handled()


var _ledger_clicks := 0


func _on_ledger_input(event: InputEvent) -> void:
	if not (event is InputEventMouseButton) or not event.pressed or event.button_index != MOUSE_BUTTON_LEFT:
		return
	_ledger_clicks += 1
	var p: Vector2 = event.position
	var best := -1
	var best_d := 9.0
	for target in _hit_targets:
		var d := (target[1] as Vector2).distance_to(p)
		if d < best_d:
			best_d = d
			best = _hit_targets.find(target)
	if best >= 0:
		_focus_row = int(_hit_targets[best][0])
		_selected_event = int(_hit_targets[best][2])
		_show_unavailable = false
	else:
		var row := int(floor((p.y - AXIS_HEIGHT) / _row_height()))
		if row >= 0 and row < _rows.size():
			_set_focus_row(row)
	_render()
	_ledger.accept_event()


func _on_locator_input(event: InputEvent) -> void:
	if not (event is InputEventMouseButton) or not event.pressed or event.button_index != MOUSE_BUTTON_LEFT:
		return
	var p: Vector2 = event.position
	var x := clampi(int(p.x / _locator.size.x * _film_width), 0, _film_width - 1)
	var y := clampi(int(p.y / _locator.size.y * _film_height), 0, _film_height - 1)
	SelectPixel(y * _film_width + x)
	_locator.accept_event()


func _set_focus_row(row: int) -> void:
	_focus_row = clampi(row, 0, _rows.size() - 1)
	if not _event_exists(_selected_event):
		_selected_event = -1
	_render()


func _step_event(direction: int, every_event: bool) -> void:
	var events: Array = _focused_events()
	if events.is_empty():
		return
	var i := -1
	for n in events.size():
		if int(events[n].get("event_ordinal", -1)) == _selected_event:
			i = n
	var n2 := i
	while true:
		n2 += direction
		if n2 < 0 or n2 >= events.size():
			return
		if every_event or str(events[n2].get("kind", "")) != "ProbeQuestionIssued":
			break
	_selected_event = int(events[n2].get("event_ordinal", -1))
	_show_unavailable = false
	_render()


func _move_focused_pixel(keycode: int) -> void:
	var index := _rows[_focus_row]
	var x := index % _film_width
	var y := index / _film_width
	match keycode:
		KEY_LEFT: x -= 1
		KEY_RIGHT: x += 1
		KEY_UP: y -= 1
		KEY_DOWN: y += 1
	x = clampi(x, 0, _film_width - 1)
	y = clampi(y, 0, _film_height - 1)
	SelectPixel(y * _film_width + x)


# Replaces the focused row's pixel (keeps the other comparison rows) and refetches.
func SelectPixel(index: int) -> void:
	if index < 0 or index >= _film_width * _film_height or _rows.has(index):
		if _rows.has(index):
			_focus_row = _rows.find(index)
			_render()
		return
	_rows[_focus_row] = index
	_selected_event = -1
	_refresh(false)


func ResetToAcceptanceRows() -> void:
	_rows = ACCEPTANCE_ROWS.duplicate()
	_focus_row = 1
	_selected_event = -1
	_refresh(false)


# ---------- helpers ----------

func _focused_events() -> Array:
	var pixel: Dictionary = _pixels.get(_rows[_focus_row], {})
	return pixel.get("recorded", {}).get("events", []) if not pixel.is_empty() else []


func _event_exists(ordinal: int) -> bool:
	if ordinal < 0:
		return false
	for event in _focused_events():
		if int(event.get("event_ordinal", -1)) == ordinal:
			return true
	return false


func _selected_event_dict() -> Dictionary:
	if _selected_event < 0:
		return {}
	for event in _focused_events():
		if int(event.get("event_ordinal", -1)) == _selected_event:
			return event
	return {}


func _short(value) -> String:
	var s := str(value)
	return s.substr(0, 8) + "…" if s.length() > 8 else (s if not s.is_empty() else "?")


func _bits_display(hex: String) -> String:
	if hex.length() != 8:
		return "?"
	var bytes := PackedByteArray()
	bytes.resize(4)
	bytes.encode_u32(0, hex.hex_to_int())
	return "%.3f" % bytes.decode_float(0)


func _apply_layout() -> void:
	if not is_node_ready():
		return
	var viewport_size := get_viewport().get_visible_rect().size
	var narrow := viewport_size.x < 900.0 or viewport_size.y > viewport_size.x * 1.15
	if narrow:
		anchor_left = 0.0
		anchor_top = 0.0
		anchor_right = 1.0
		anchor_bottom = 1.0
		offset_left = 12.0
		offset_top = 12.0
		offset_right = -12.0
		offset_bottom = -12.0
		_locator.custom_minimum_size = Vector2(160, 90)
	else:
		# Right of the instrument status card and film plate column (Workbench layout plan: 12..492 px).
		anchor_left = 0.0
		anchor_top = 0.0
		anchor_right = 1.0
		anchor_bottom = 1.0
		offset_left = 504.0
		offset_top = 12.0
		offset_right = -12.0
		offset_bottom = -12.0
		_locator.custom_minimum_size = Vector2(200, 112)
	if _open:
		_render()


# ---------- testing ----------

func GetDisplayedStateForTesting() -> Dictionary:
	return {
		"open": _open, "visible": visible, "rows": _rows.duplicate(), "focus_row": _focus_row,
		"selected_event": _selected_event, "status": str(_doc.get("status", "")), "reason": str(_doc.get("reason", "")),
		"fetch_count": _fetch_count, "last_fetch_ms": _last_fetch_ms, "identity": _identity.text, "warnings": _warnings.text,
		"summary": _summary.text, "detail": _detail.text, "hit_targets": _hit_targets.size(),
		"show_unavailable": _show_unavailable, "panel_rect": get_global_rect(), "ledger_clicks": _ledger_clicks,
	}


func GetInspectionDocumentForTesting() -> Dictionary:
	return _doc.duplicate(true)


func SelectEventForTesting(row: int, ordinal: int) -> void:
	_focus_row = clampi(row, 0, _rows.size() - 1)
	_selected_event = ordinal
	_render()


func ForceRefreshForTesting() -> void:
	_refresh(false)
