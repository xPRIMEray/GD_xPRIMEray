extends Node3D

signal portal_transport_applied(source_portal: StringName, count: int)
signal screenshot_captured(path: String, clean: bool, state_restored: bool)

const SEMANTIC_SCENE_ID := "suspended-portal-gallery-v0"
const PLAYER_SCENE := preload("res://Transport/TransportChamberPlayer.tscn")
const PORTAL_SCRIPT := preload("res://Wormhole/WormholePortal.cs")
const FIELD_SCRIPT := preload("res://FieldSource3D.cs")
const BOUNDARY_SCRIPT := preload("res://BoundaryLayerVolume.cs")
const RBR_SCRIPT := preload("res://RayBeamRenderer.cs")
const FILM_SCRIPT := preload("res://GrinFilmCamera.cs")
const FILM_CONTROLLER_SCRIPT := preload("res://Transport/FilmController.gd")
const CHECKER_SHADER := preload("res://Experiences/checkerboard_3d.gdshader")

const PORTAL_CENTER := Vector3(0.0, 2.5, -1.0)
const ARRIVAL_CENTER := Vector3(0.0, 2.5, -60.0)
const PORTAL_RADIUS := 1.1
const EXIT_CLEARANCE := 0.4
const FIELD_RADIUS := 3.0
const SCREENSHOT_OUTPUT_ROOT := "res://output/suspended_portal_gallery_v0/captures"

var transport_application_count := 0
var last_transport_source := StringName()
var _cooldown_remaining := 0.0
var _last_source_delta := 0.0
var _last_arrival_delta := 0.0
var _player: CharacterBody3D
var _camera: Camera3D
var _source_portal: Node3D
var _arrival_portal: Node3D
var _source_field: Node3D
var _arrival_field: Node3D
var _film_camera: Node
var _film_controller: Node
var _scene_hud_canvas: CanvasLayer
var _truth_panel: PanelContainer
var _truth_label: Label
var _probe_hud_panel: Control
var _probe_hud_label: Label
var _probe_hud_canvas: CanvasLayer
var _decorative_rings: Node3D
var _diagnostic_labels: Array[Label3D] = []
var _rings_visible := true
var _labels_visible := true
var _full_hud := false
var _hud_visible := true
var _capture_in_progress := false
var _capture_thread: Thread
var last_capture_path := ""
var last_capture_clean := false
var last_capture_state_restored := false
var last_capture_hud_hidden_frame := false


func _ready() -> void:
	set_meta("semantic_scene_id", SEMANTIC_SCENE_ID)
	var world := Node3D.new()
	world.name = "SuspendedPortalGalleryWorld"
	_build_lighting(world)
	_build_main_chamber(world)
	_build_destination_chamber(world)
	_build_diagnostic_zoo(world)
	_build_suspended_support(world)
	_build_portals_fields_and_boundaries(world)
	_build_player_and_instrument(world)
	_build_hud(world)
	add_child(world)

	_player = world.get_node("TransportChamberPlayer") as CharacterBody3D
	_camera = _player.get_node("Head/PlayerCamera") as Camera3D
	_source_portal = world.get_node("PortalSystem/SuspendedPortal") as Node3D
	_arrival_portal = world.get_node("PortalSystem/ArrivalPortal") as Node3D
	_source_field = world.get_node("SuspendedPortalField") as Node3D
	_arrival_field = world.get_node("ArrivalPortalField") as Node3D
	_film_camera = world.get_node("GrinFilmCamera")
	_film_controller = world.get_node("FilmController")
	_scene_hud_canvas = world.get_node("CanvasLayer") as CanvasLayer
	_truth_panel = world.get_node("CanvasLayer/HUDRoot/TruthBoundary") as PanelContainer
	_truth_label = _truth_panel.get_child(0) as Label
	_decorative_rings = world.get_node("DecorativeFieldRings") as Node3D
	_camera.current = true
	_prime_portal_deltas()
	call_deferred("_configure_presentation_hud")


func _process(delta: float) -> void:
	_update_presentation_hud()
	if _source_portal == null or _arrival_portal == null or _camera == null:
		return
	_source_portal.call("UpdatePortalView", _camera)
	_arrival_portal.call("UpdatePortalView", _camera)
	if _cooldown_remaining > 0.0:
		_cooldown_remaining = max(0.0, _cooldown_remaining - delta)
		_prime_portal_deltas()
		return
	_try_cross(_source_portal, true)
	_try_cross(_arrival_portal, false)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_F:
		var next_visible := not bool(_source_field.get("DebugVizInGame"))
		_source_field.set("DebugVizInGame", next_visible)
		_arrival_field.set("DebugVizInGame", next_visible)
		get_viewport().set_input_as_handled()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_R:
		_rings_visible = not _rings_visible
		_decorative_rings.visible = _rings_visible
		get_viewport().set_input_as_handled()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_L:
		_labels_visible = not _labels_visible
		for label in _diagnostic_labels:
			label.visible = _labels_visible
		get_viewport().set_input_as_handled()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_H:
		_full_hud = not _full_hud
		_apply_hud_layout()
		get_viewport().set_input_as_handled()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_F10:
		_capture_current_viewport(bool(event.shift_pressed))
		get_viewport().set_input_as_handled()


func get_player() -> CharacterBody3D:
	return _player


func get_source_portal() -> Node3D:
	return _source_portal


func get_arrival_portal() -> Node3D:
	return _arrival_portal


func get_semantic_scene_id() -> String:
	return SEMANTIC_SCENE_ID


func get_presentation_diagnostics() -> Dictionary:
	return {
		"truth_rect": _truth_panel.get_global_rect() if _truth_panel != null else Rect2(),
		"instrument_rect": _probe_hud_panel.get_global_rect() if _probe_hud_panel != null else Rect2(),
		"film_plate_rect": _scene_hud_canvas.get_node("HUDRoot/FilmPlate").get_global_rect() if _scene_hud_canvas != null else Rect2(),
		"truth_label_rect": _truth_label.get_global_rect() if _truth_label != null else Rect2(),
		"instrument_label_rect": _probe_hud_label.get_global_rect() if _probe_hud_label != null else Rect2(),
		"truth_label_minimum": _truth_label.get_combined_minimum_size() if _truth_label != null else Vector2.ZERO,
		"instrument_label_minimum": _probe_hud_label.get_combined_minimum_size() if _probe_hud_label != null else Vector2.ZERO,
		"truth_text": _truth_label.text if _truth_label != null else "",
		"instrument_text": _probe_hud_label.text if _probe_hud_label != null else "",
		"full_hud": _full_hud,
		"hud_visible": _hud_visible,
		"rings_visible": _rings_visible,
		"labels_visible": _labels_visible,
		"field_visible": bool(_source_field.get("DebugVizInGame")) if _source_field != null else false,
		"capture_in_progress": _capture_in_progress,
		"last_capture_path": last_capture_path,
		"last_capture_clean": last_capture_clean,
		"last_capture_state_restored": last_capture_state_restored,
		"last_capture_hud_hidden_frame": last_capture_hud_hidden_frame,
	}


func _configure_presentation_hud() -> void:
	if _film_camera == null:
		return
	for child in _film_camera.get_children():
		if child is not CanvasLayer:
			continue
		_probe_hud_canvas = child as CanvasLayer
		for hud_child in child.get_children():
			if hud_child is Panel:
				_probe_hud_panel = hud_child as Control
			elif hud_child is Label and String(hud_child.text).contains("Q probe"):
				_probe_hud_label = hud_child as Label
	_apply_hud_layout()
	_update_presentation_hud()


func _apply_hud_layout() -> void:
	if _truth_panel == null or _truth_label == null:
		return
	_truth_panel.custom_minimum_size = Vector2.ZERO
	_truth_label.autowrap_mode = TextServer.AUTOWRAP_OFF
	if _full_hud:
		_truth_panel.size = Vector2(400.0, 285.0)
		_truth_label.add_theme_font_size_override("font_size", 13)
	else:
		_truth_panel.size = Vector2(350.0, 116.0)
		_truth_label.add_theme_font_size_override("font_size", 14)
	if _probe_hud_panel == null or _probe_hud_label == null:
		return
	_probe_hud_label.autowrap_mode = TextServer.AUTOWRAP_OFF
	_probe_hud_label.add_theme_font_size_override("font_size", 13)
	if _full_hud:
		_probe_hud_panel.position = Vector2(420.0, 12.0)
		_probe_hud_panel.size = Vector2(370.0, 300.0)
		_probe_hud_label.position = Vector2(434.0, 24.0)
		_probe_hud_label.size = Vector2(342.0, 274.0)
		_probe_hud_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	else:
		_probe_hud_panel.position = Vector2(386.0, 12.0)
		_probe_hud_panel.size = Vector2(330.0, 150.0)
		_probe_hud_label.position = Vector2(398.0, 22.0)
		_probe_hud_label.size = Vector2(306.0, 128.0)


func _update_presentation_hud() -> void:
	if _truth_label == null or _probe_hud_label == null or _film_camera == null:
		return
	var field_visible := bool(_source_field.get("DebugVizInGame"))
	var profile := String(_film_camera.get("ProbeProfileDisplayName"))
	var generation := int(_film_camera.get("SealedProbeViewGeneration"))
	var probe_modes := ["Outcome", "Contact Events", "Transport Effort"]
	var probe_descriptions := [
		"Terminal semantic outcome",
		"Accepted per-step contacts · repeats count",
		"Numerical step budget used · not time · not field strength",
	]
	var probe_legends := [
		"Unprocessed · HitGeometry · BackgroundResolved · MaxStepsExhausted · StoppedEarlyAbsorbed · NumericalFailure · Invalid",
		"0 · 1 · 2 · 3+",
		"0.0 · 0.5 · 1.0 · N/A",
	]
	var probe_index := clampi(int(_film_camera.get("CurrentProbeView")), 0, probe_modes.size() - 1)
	var probe_status: String = probe_modes[probe_index] if generation > 0 else "unavailable"
	var film_modes := ["OFF", "SNAPSHOT", "LIVE"]
	var film_mode_index := clampi(int(_film_controller.get("_mode")), 0, film_modes.size() - 1)
	var film_mode: String = film_modes[film_mode_index]
	var row_cursor := int(_film_camera.get("FilmRowCursor"))
	if _full_hud:
		_truth_label.text = "THE OPTICAL FUNHOUSE / FULL CONTROLS\nWASD Move | Mouse Look | Shift Sprint\nV Walk/Fly | F Field | R Rings | L Labels\nG Film | P Profile\nQ / Shift+Q Probe next / previous\n[ / ] Film opacity | N Film shading\nH Minimal HUD | F10 Capture\nShift+F10 Beauty (HUD hidden)\nUnavailable here:\nO Optical-only | Esc Workbench\nF1/F2 renderer debug disabled\nE reserved | F6-F9 legacy macros disabled\n\nGodot presentation != transport evidence\nGeometry + contacts + normals authoritative\nMaterials are raster presentation only"
		var field_description := "outer / inner limits · density direction" if field_visible else "hidden (field physics remains active)"
		var probe_detail := "Probe View unavailable\nComplete SNAPSHOT required"
		if generation > 0:
			probe_detail = "%s\nLegend: %s" % [probe_descriptions[probe_index], probe_legends[probe_index]]
		_probe_hud_label.text = "P Probe Profile: %s\nQ Probe View: %s\nF Field Structure: %s\n%s\nR Rings: %s  ·  L Labels: %s\nG Observation: %s  ·  row %d\nSealed generation: %s\n%s" % [
			profile,
			probe_status,
			"ON" if field_visible else "OFF",
			field_description,
			"ON" if _rings_visible else "OFF",
			"ON" if _labels_visible else "OFF",
			film_mode,
			row_cursor,
			str(generation) if generation > 0 else "none",
			probe_detail,
		]
	else:
		_truth_label.text = "THE OPTICAL FUNHOUSE / v0\nV MOVE | F FIELD | G FILM | H FULL\nR RINGS | L LABELS | raster != transport\nGeometry | hits | normals = evidence"
		_probe_hud_label.text = "P  %s  |  Q  %s\nF  FIELD %s  |  G  %s\nLIVE row %d  |  sealed gen %s\nH  full instrumentation" % [
			profile,
			probe_status,
			"ON" if field_visible else "OFF",
			film_mode,
			row_cursor,
			str(generation) if generation > 0 else "—",
		]
		# PanelContainer initially sees the verbose label installed during scene
		# construction. Reassert the compact bounds after replacing that text so
		# the obsolete child minimum cannot keep the old footprint alive.
		_truth_panel.size = Vector2(350.0, 116.0)


func _set_hud_visible(visible_value: bool) -> void:
	_hud_visible = visible_value
	if _scene_hud_canvas != null:
		_scene_hud_canvas.visible = visible_value
	if _probe_hud_canvas != null:
		_probe_hud_canvas.visible = visible_value


func _capture_current_viewport(clean: bool) -> void:
	if _capture_in_progress:
		return
	_capture_in_progress = true
	last_capture_path = ""
	last_capture_clean = clean
	last_capture_state_restored = false
	last_capture_hud_hidden_frame = false
	var previous_full_hud := _full_hud
	var previous_hud_visible := _hud_visible
	var camera_transform := _camera.global_transform
	var portal_count := transport_application_count
	var profile_id := String(_film_camera.get("ProbeProfileStableId"))
	var sealed_generation := int(_film_camera.get("SealedProbeViewGeneration"))
	if clean:
		_set_hud_visible(false)
		await get_tree().process_frame
		last_capture_hud_hidden_frame = not _hud_visible
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	if clean:
		_set_hud_visible(previous_hud_visible)
		_full_hud = previous_full_hud
		_apply_hud_layout()
	var timestamp := Time.get_datetime_string_from_system(false, true).replace(":", "-")
	var mode_name := "beauty" if clean else ("full" if _full_hud else "minimal")
	var file_name := "%s_%s_%dx%d.png" % [timestamp, mode_name, image.get_width(), image.get_height()]
	var output_path := ProjectSettings.globalize_path(SCREENSHOT_OUTPUT_ROOT.path_join(file_name))
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(SCREENSHOT_OUTPUT_ROOT))
	_capture_thread = Thread.new()
	var start_error := _capture_thread.start(_save_screenshot_worker.bind(image, output_path))
	var save_error := start_error
	if start_error == OK:
		while _capture_thread.is_alive():
			await get_tree().process_frame
		save_error = int(_capture_thread.wait_to_finish())
	_capture_thread = null
	last_capture_state_restored = (
		_full_hud == previous_full_hud
		and _hud_visible == previous_hud_visible
		and _camera.global_transform.is_equal_approx(camera_transform)
		and transport_application_count == portal_count
		and String(_film_camera.get("ProbeProfileStableId")) == profile_id
		and int(_film_camera.get("SealedProbeViewGeneration")) == sealed_generation
	)
	if save_error == OK:
		last_capture_path = output_path
		print("[SuspendedPortalCapture] saved=%s clean=%d hudRestored=%d sha256=%s" % [
			output_path,
			1 if clean else 0,
			1 if last_capture_state_restored else 0,
			FileAccess.get_sha256(output_path),
		])
	else:
		push_error("[SuspendedPortalCapture] save failed error=%d path=%s" % [save_error, output_path])
	_capture_in_progress = false
	screenshot_captured.emit(last_capture_path, clean, last_capture_state_restored)


func _save_screenshot_worker(image: Image, output_path: String) -> int:
	return image.save_png(output_path)


func _try_cross(portal: Node3D, source_side: bool) -> void:
	var current_delta := float(portal.call("SignedRadiusDelta", _camera.global_position))
	var previous_delta := _last_source_delta if source_side else _last_arrival_delta
	if source_side:
		_last_source_delta = current_delta
	else:
		_last_arrival_delta = current_delta
	if previous_delta <= 0.0 or current_delta > 0.0:
		return
	var mapped = portal.call("BuildExitTransform", _camera.global_transform)
	if mapped is not Transform3D:
		return
	_player.call("ApplyCameraTransform", mapped)
	transport_application_count += 1
	last_transport_source = portal.name
	portal_transport_applied.emit(last_transport_source, transport_application_count)
	_cooldown_remaining = 0.4
	_prime_portal_deltas()


func _prime_portal_deltas() -> void:
	_last_source_delta = float(_source_portal.call("SignedRadiusDelta", _camera.global_position))
	_last_arrival_delta = float(_arrival_portal.call("SignedRadiusDelta", _camera.global_position))


func _build_lighting(world: Node3D) -> void:
	var environment := WorldEnvironment.new()
	environment.name = "WorldEnvironment"
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color("101526")
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("b8cae8")
	env.ambient_light_energy = 0.48
	environment.environment = env
	world.add_child(environment)

	var key := DirectionalLight3D.new()
	key.name = "KeyLight"
	key.rotation_degrees = Vector3(-52.0, -28.0, 0.0)
	key.light_color = Color("fff0d2")
	key.light_energy = 1.15
	key.shadow_enabled = true
	world.add_child(key)

	_add_omni(world, "PortalFill", Vector3(0.0, 4.8, 1.0), Color("48d8ff"), 8.0, 2.2)
	_add_omni(world, "ZooFill", Vector3(0.0, 4.8, -5.0), Color("ff5ac8"), 11.0, 1.4)


func _build_main_chamber(world: Node3D) -> void:
	var room := Node3D.new()
	room.name = "DiagnosticChamber"
	world.add_child(room)
	_add_box_surface(room, "Floor", Vector3(20.0, 0.25, 20.0), Vector3(0.0, -0.125, 0.0), _checker(Color("11131a"), Color("e8edf2"), 1.0), true)
	_add_box_surface(room, "Ceiling", Vector3(20.0, 0.25, 20.0), Vector3(0.0, 7.125, 0.0), _solid(Color("303548"), 0.85), true)
	_add_box_surface(room, "BackWall", Vector3(20.0, 7.0, 0.3), Vector3(0.0, 3.5, -10.0), _checker(Color("152a67"), Color("70d7ef"), 0.55), true)
	_add_box_surface(room, "FrontWall", Vector3(20.0, 7.0, 0.3), Vector3(0.0, 3.5, 10.0), _checker(Color("172d62"), Color("56bddd"), 0.55), true)
	_add_box_surface(room, "LeftWall", Vector3(0.3, 7.0, 20.0), Vector3(-10.0, 3.5, 0.0), _checker(Color("12376a"), Color("4ce0db"), 0.8), true)
	_add_box_surface(room, "RightWall", Vector3(0.3, 7.0, 20.0), Vector3(10.0, 3.5, 0.0), _checker(Color("12376a"), Color("4ce0db"), 0.8), true)
	_add_calibration_strips(room)


func _build_destination_chamber(world: Node3D) -> void:
	var room := Node3D.new()
	room.name = "DestinationChamber"
	world.add_child(room)
	var center := Vector3(0.0, 0.0, -60.0)
	var neutral := _solid(Color("263047"), 0.82)
	_add_box_surface(room, "Floor", Vector3(16.0, 0.25, 16.0), center + Vector3(0.0, -0.125, 0.0), _checker(Color("101722"), Color("9fc7d9"), 0.8), true)
	_add_box_surface(room, "Ceiling", Vector3(16.0, 0.25, 16.0), center + Vector3(0.0, 7.125, 0.0), neutral, true)
	_add_box_surface(room, "BackWall", Vector3(16.0, 7.0, 0.3), center + Vector3(0.0, 3.5, -8.0), neutral, true)
	_add_box_surface(room, "FrontWall", Vector3(16.0, 7.0, 0.3), center + Vector3(0.0, 3.5, 8.0), neutral, true)
	_add_box_surface(room, "LeftWall", Vector3(0.3, 7.0, 16.0), center + Vector3(-8.0, 3.5, 0.0), neutral, true)
	_add_box_surface(room, "RightWall", Vector3(0.3, 7.0, 16.0), center + Vector3(8.0, 3.5, 0.0), neutral, true)


func _build_diagnostic_zoo(world: Node3D) -> void:
	var zoo := Node3D.new()
	zoo.name = "DiagnosticGeometryZoo"
	world.add_child(zoo)

	var sphere_body := StaticBody3D.new()
	sphere_body.name = "ReferenceSphere"
	sphere_body.position = Vector3(-5.2, 1.4, -5.2)
	sphere_body.add_to_group("fixture_geometry")
	sphere_body.add_to_group("raytrace_geometry")
	var sphere_mesh := MeshInstance3D.new()
	sphere_mesh.name = "PresentationMesh"
	var sphere := SphereMesh.new()
	sphere.radius = 1.35
	sphere.height = 2.7
	sphere.radial_segments = 48
	sphere.rings = 24
	sphere.material = _checker(Color("edfaff"), Color("168bd1"), 1.4)
	sphere_mesh.mesh = sphere
	sphere_body.add_child(sphere_mesh)
	var sphere_collision := CollisionShape3D.new()
	var sphere_shape := SphereShape3D.new()
	sphere_shape.radius = 1.35
	sphere_collision.shape = sphere_shape
	sphere_body.add_child(sphere_collision)
	zoo.add_child(sphere_body)
	_add_object_label(zoo, "SPHERE / LIVE NORMALS", Vector3(-5.2, 3.05, -5.2), Color("65d8ff"))

	_add_box_surface(zoo, "ReferenceCube", Vector3(2.5, 2.5, 2.5), Vector3(5.0, 1.25, -5.0), _checker(Color("e83d39"), Color("ffd536"), 1.25), true)
	_add_object_label(zoo, "CUBE / KERNEL BOX", Vector3(5.0, 2.95, -5.0), Color("ffd536"))

	var pyramid_body := StaticBody3D.new()
	pyramid_body.name = "ReferencePyramid"
	pyramid_body.position = Vector3(-5.0, 0.0, 4.3)
	pyramid_body.add_to_group("fixture_geometry")
	pyramid_body.add_to_group("raytrace_geometry")
	var pyramid_mesh := MeshInstance3D.new()
	pyramid_mesh.name = "PresentationMesh"
	var pyramid := CylinderMesh.new()
	pyramid.top_radius = 0.0
	pyramid.bottom_radius = 1.45
	pyramid.height = 2.8
	pyramid.radial_segments = 4
	pyramid.rings = 1
	pyramid.material = _checker(Color("e932a2"), Color("79f2ff"), 1.2)
	pyramid_mesh.position.y = 1.4
	pyramid_mesh.rotation.y = PI / 4.0
	pyramid_mesh.mesh = pyramid
	pyramid_body.add_child(pyramid_mesh)
	_add_pyramid_face_collision(pyramid_body, Vector3(0.0, 1.15, -0.62), Vector3(-0.68, 0.0, 0.0))
	_add_pyramid_face_collision(pyramid_body, Vector3(0.0, 1.15, 0.62), Vector3(0.68, 0.0, 0.0))
	_add_pyramid_face_collision(pyramid_body, Vector3(-0.62, 1.15, 0.0), Vector3(0.0, 0.0, 0.68))
	_add_pyramid_face_collision(pyramid_body, Vector3(0.62, 1.15, 0.0), Vector3(0.0, 0.0, -0.68))
	zoo.add_child(pyramid_body)
	_add_object_label(zoo, "PYRAMID / 4 KERNEL PLANES", Vector3(-5.0, 3.25, 4.3), Color("ff6bc5"))


func _build_suspended_support(world: Node3D) -> void:
	var support := Node3D.new()
	support.name = "SuspendedPortalSupport"
	world.add_child(support)
	var stone := _solid(Color("787f8f"), 0.88)
	var bronze := _solid(Color("b77a2d"), 0.28, 0.68)
	_add_box_surface(support, "OffsetStonePlinth", Vector3(1.6, 1.5, 1.8), Vector3(3.3, 0.75, -1.0), stone, true)
	var points := [
		Vector3(3.3, 1.5, -1.0),
		Vector3(3.3, 2.8, -1.0),
		Vector3(2.9, 4.0, -1.0),
		Vector3(2.0, 4.75, -1.0),
		Vector3(0.9, 5.05, -1.0),
		Vector3(0.0, 4.55, -1.0),
		Vector3(0.0, 3.75, -1.0),
	]
	for index in points.size() - 1:
		_add_support_segment(support, "BronzeArch%02d" % index, points[index], points[index + 1], 0.16, bronze)


func _build_portals_fields_and_boundaries(world: Node3D) -> void:
	var holder := Node3D.new()
	holder.name = "PortalSystem"
	var source := _make_portal("SuspendedPortal", PORTAL_CENTER, NodePath("../ArrivalPortal"), "suspended_gallery", "diagnostic_arrival")
	var arrival := _make_portal("ArrivalPortal", ARRIVAL_CENTER, NodePath("../SuspendedPortal"), "diagnostic_arrival", "suspended_gallery")
	holder.add_child(source)
	holder.add_child(arrival)
	world.add_child(holder)

	_source_field = _make_field("SuspendedPortalField", PORTAL_CENTER)
	_arrival_field = _make_field("ArrivalPortalField", ARRIVAL_CENTER)
	world.add_child(_source_field)
	world.add_child(_arrival_field)

	var source_boundary := _make_boundary("SuspendedPortalBoundary", PORTAL_CENTER, NodePath("../ArrivalPortalBoundary"))
	var arrival_boundary := _make_boundary("ArrivalPortalBoundary", ARRIVAL_CENTER, NodePath("../SuspendedPortalBoundary"))
	world.add_child(source_boundary)
	world.add_child(arrival_boundary)
	_add_radial_field_rings(world)


func _build_player_and_instrument(world: Node3D) -> void:
	var player := PLAYER_SCENE.instantiate()
	player.name = "TransportChamberPlayer"
	player.position = Vector3(0.0, 0.125, 7.5)
	world.add_child(player)

	var renderer := Node3D.new()
	renderer.name = "RayBeamRenderer"
	renderer.set_script(RBR_SCRIPT)
	renderer.set("CameraPath", NodePath("../TransportChamberPlayer/Head/PlayerCamera"))
	renderer.set("StepsPerRay", 80)
	renderer.set("StepLength", 0.07)
	renderer.set("CollisionMask", 1)
	renderer.set("FieldCenterIsCamera", false)
	renderer.set("FieldCenter", PORTAL_CENTER)
	renderer.set("BendScale", 1.0)
	renderer.set("UpdateEveryFrame", false)
	# The gallery owns its public function-key vocabulary. Keep legacy renderer
	# debug hotkeys unavailable without changing the renderer's global default.
	renderer.set("DebugHotkeysEnabled", false)
	world.add_child(renderer)

	var film := Node.new()
	film.name = "GrinFilmCamera"
	film.set_script(FILM_SCRIPT)
	film.set("Width", 80)
	film.set("Height", 45)
	film.set("FilmResolutionScale", 0.5)
	film.set("RayBeamRendererPath", NodePath("../RayBeamRenderer"))
	film.set("FilmViewPath", NodePath("../CanvasLayer/HUDRoot/FilmPlate/FilmView"))
	film.set("FilmOverlayPath", NodePath("../CanvasLayer/HUDRoot/FilmPlate/FilmOverlay"))
	film.set("FixtureDebugSourceGroup", "fixture_geometry")
	film.set("UpdateEveryFrame", false)
	# F10 belongs to this gallery's screenshot capture. Disable the legacy
	# direct macro keys on this independently owned film instance only.
	film.set("RuntimeMacroHotkeysEnabled", false)
	film.set("UpdateEveryFrameMaxRowsPerStep", 2)
	film.set("MinRowsPerFrame", 1)
	film.set("MaxRowsPerFrameCap", 16)
	film.set("UseThreadedBands", true)
	film.set("ThreadedBandWorkerCount", 6)
	film.set("ThreadedBandRowsPerChunk", 1)
	world.add_child(film)


func _build_hud(world: Node3D) -> void:
	var canvas := CanvasLayer.new()
	canvas.name = "CanvasLayer"
	canvas.layer = 2
	var root := Control.new()
	root.name = "HUDRoot"
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	canvas.add_child(root)

	var info_panel := PanelContainer.new()
	info_panel.name = "TruthBoundary"
	info_panel.position = Vector2(18.0, 18.0)
	info_panel.custom_minimum_size = Vector2(350.0, 0.0)
	var panel_style := StyleBoxFlat.new()
	panel_style.bg_color = Color(0.025, 0.04, 0.075, 0.84)
	panel_style.border_color = Color("4ddcf0")
	panel_style.set_border_width_all(1)
	panel_style.set_corner_radius_all(8)
	panel_style.set_content_margin_all(12.0)
	info_panel.add_theme_stylebox_override("panel", panel_style)
	var label := Label.new()
	label.text = "THE OPTICAL FUNHOUSE\nSUSPENDED PORTAL GALLERY v0\n\nV  WALK / FLY    F  FIELD    G  OBSERVATION\n\nGodot presentation ≠ transport evidence\nGeometry + contacts + normals are authoritative\nMaterials are raster presentation only"
	label.add_theme_color_override("font_color", Color("e7f8ff"))
	label.add_theme_font_size_override("font_size", 15)
	info_panel.add_child(label)
	root.add_child(info_panel)

	var film_plate := PanelContainer.new()
	film_plate.name = "FilmPlate"
	film_plate.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	film_plate.position = Vector2(-348.0, 18.0)
	film_plate.size = Vector2(330.0, 220.0)
	var film_style := StyleBoxFlat.new()
	film_style.bg_color = Color(0.02, 0.025, 0.05, 0.88)
	film_style.border_color = Color("e84fb8")
	film_style.set_border_width_all(2)
	film_style.set_corner_radius_all(8)
	film_plate.add_theme_stylebox_override("panel", film_style)
	var film_view := TextureRect.new()
	film_view.name = "FilmView"
	film_view.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	film_view.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	film_plate.add_child(film_view)
	var overlay := TextureRect.new()
	overlay.name = "FilmOverlay"
	overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	film_plate.add_child(overlay)
	root.add_child(film_plate)
	world.add_child(canvas)

	var controller := Node.new()
	controller.name = "FilmController"
	controller.set_script(FILM_CONTROLLER_SCRIPT)
	controller.set("film_camera_path", NodePath("../GrinFilmCamera"))
	controller.set("film_plate_path", NodePath("../CanvasLayer/HUDRoot/FilmPlate"))
	controller.set("film_view_path", NodePath("../CanvasLayer/HUDRoot/FilmPlate/FilmView"))
	controller.set("film_overlay_path", NodePath("../CanvasLayer/HUDRoot/FilmPlate/FilmOverlay"))
	controller.set("player_path", NodePath("../TransportChamberPlayer"))
	world.add_child(controller)


func _make_portal(name_value: String, position_value: Vector3, linked_path: NodePath, source_id: String, target_id: String) -> Node3D:
	var portal := Node3D.new()
	portal.name = name_value
	portal.position = position_value
	portal.set_script(PORTAL_SCRIPT)
	portal.set("LinkedPortalPath", linked_path)
	portal.set("WormholeId", source_id)
	portal.set("LinkedWormholeId", target_id)
	portal.set("ParentRegionId", source_id)
	portal.set("ChildRegionId", target_id)
	portal.set("GeometryLayerNumber", 1)
	portal.set("PortalLayerNumber", 2)
	portal.set("ThroatRadius", PORTAL_RADIUS)
	portal.set("Radius", PORTAL_RADIUS)
	portal.set("ExitOffset", EXIT_CLEARANCE)
	portal.set("EnablePhaseLockedRemap", false)
	portal.set("PortalSurfacePath", NodePath("PortalSurface"))
	portal.set("PortalViewportPath", NodePath("PortalViewport"))
	portal.set("PortalCameraPath", NodePath("PortalViewport/PortalCamera"))
	portal.set("PortalTextureSize", 384)

	var surface := MeshInstance3D.new()
	surface.name = "PortalSurface"
	surface.layers = 2
	var sphere := SphereMesh.new()
	sphere.radius = PORTAL_RADIUS
	sphere.height = PORTAL_RADIUS * 2.0
	sphere.radial_segments = 48
	sphere.rings = 24
	surface.mesh = sphere
	portal.add_child(surface)
	var viewport := SubViewport.new()
	viewport.name = "PortalViewport"
	viewport.size = Vector2i(384, 384)
	var portal_camera := Camera3D.new()
	portal_camera.name = "PortalCamera"
	portal_camera.near = 0.01
	portal_camera.far = 400.0
	viewport.add_child(portal_camera)
	portal.add_child(viewport)
	return portal


func _make_field(name_value: String, position_value: Vector3) -> Node3D:
	var field := Node3D.new()
	field.name = name_value
	field.position = position_value
	field.set_script(FIELD_SCRIPT)
	field.set("ROuter", FIELD_RADIUS)
	field.set("Amp", 1.0)
	field.set("DebugVizInGame", false)
	return field


func _make_boundary(name_value: String, position_value: Vector3, linked_path: NodePath) -> Node3D:
	var boundary := Node3D.new()
	boundary.name = name_value
	boundary.position = position_value
	boundary.set_script(BOUNDARY_SCRIPT)
	boundary.set("Radius", PORTAL_RADIUS)
	boundary.set("ExecutionMode", 1)
	boundary.set("Behavior", 1)
	boundary.set("LinkedBoundaryPath", linked_path)
	boundary.set("SceneTransformExitOffset", EXIT_CLEARANCE)
	return boundary


func _add_box_surface(parent: Node3D, name_value: String, size_value: Vector3, position_value: Vector3, material: Material, authoritative: bool) -> StaticBody3D:
	var body := StaticBody3D.new()
	body.name = name_value
	body.position = position_value
	if authoritative:
		body.add_to_group("fixture_geometry")
	body.add_to_group("raytrace_geometry")
	var mesh_instance := MeshInstance3D.new()
	mesh_instance.name = "PresentationMesh"
	var mesh := BoxMesh.new()
	mesh.size = size_value
	mesh.material = material
	mesh_instance.mesh = mesh
	body.add_child(mesh_instance)
	var collision := CollisionShape3D.new()
	collision.name = "CollisionShape3D"
	var shape := BoxShape3D.new()
	shape.size = size_value
	collision.shape = shape
	body.add_child(collision)
	parent.add_child(body)
	return body


func _add_support_segment(parent: Node3D, name_value: String, start: Vector3, finish: Vector3, radius: float, material: Material) -> void:
	var delta := finish - start
	var midpoint := (start + finish) * 0.5
	var rotation_basis := Basis(Quaternion(Vector3.UP, delta.normalized()))
	var body := StaticBody3D.new()
	body.name = name_value
	body.transform = Transform3D(rotation_basis, midpoint)
	body.add_to_group("fixture_geometry")
	body.add_to_group("raytrace_geometry")
	var visual := MeshInstance3D.new()
	var cylinder := CylinderMesh.new()
	cylinder.top_radius = radius
	cylinder.bottom_radius = radius
	cylinder.height = delta.length()
	cylinder.radial_segments = 20
	cylinder.material = material
	visual.mesh = cylinder
	body.add_child(visual)
	var collision := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = Vector3(radius * 2.0, delta.length(), radius * 2.0)
	collision.shape = shape
	body.add_child(collision)
	parent.add_child(body)


func _add_pyramid_face_collision(body: StaticBody3D, position_value: Vector3, rotation_value: Vector3) -> void:
	var collision := CollisionShape3D.new()
	collision.position = position_value
	collision.rotation = rotation_value
	var shape := BoxShape3D.new()
	shape.size = Vector3(2.05, 0.10, 1.85)
	collision.shape = shape
	body.add_child(collision)


func _add_radial_field_rings(world: Node3D) -> void:
	var rings := Node3D.new()
	rings.name = "DecorativeFieldRings"
	rings.position = PORTAL_CENTER
	rings.add_to_group("presentation_only")
	var material := _emissive(Color("35d8ff"), 2.5)
	for index in 3:
		var mesh_instance := MeshInstance3D.new()
		mesh_instance.name = "FieldRing%02d" % index
		var torus := TorusMesh.new()
		torus.inner_radius = 1.30 + index * 0.25
		torus.outer_radius = 1.34 + index * 0.25
		torus.rings = 64
		torus.ring_segments = 10
		torus.material = material
		mesh_instance.mesh = torus
		mesh_instance.rotation = Vector3(PI / 2.0, index * 0.35, index * 0.18)
		rings.add_child(mesh_instance)
	world.add_child(rings)


func _add_calibration_strips(room: Node3D) -> void:
	var colors := [Color("f13b4f"), Color("45d8ff"), Color("ffe348"), Color("ea4fc3")]
	for index in colors.size():
		var mesh_instance := MeshInstance3D.new()
		mesh_instance.name = "CalibrationStrip%02d" % index
		mesh_instance.position = Vector3(-7.5 + index * 1.0, 0.08, -9.75)
		var mesh := BoxMesh.new()
		mesh.size = Vector3(0.85, 0.12, 0.35)
		mesh.material = _emissive(colors[index], 0.8)
		mesh_instance.mesh = mesh
		room.add_child(mesh_instance)


func _add_object_label(parent: Node3D, text_value: String, position_value: Vector3, color_value: Color) -> void:
	var label := Label3D.new()
	label.name = text_value.get_slice(" / ", 0).to_pascal_case() + "Label"
	label.text = text_value
	label.position = position_value
	label.font_size = 28
	label.modulate = color_value
	label.outline_size = 8
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	parent.add_child(label)
	_diagnostic_labels.append(label)


func _add_omni(parent: Node3D, name_value: String, position_value: Vector3, color_value: Color, range_value: float, energy_value: float) -> void:
	var light := OmniLight3D.new()
	light.name = name_value
	light.position = position_value
	light.light_color = color_value
	light.omni_range = range_value
	light.light_energy = energy_value
	parent.add_child(light)


func _checker(first: Color, second: Color, scale_value: float) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = CHECKER_SHADER
	material.set_shader_parameter("color_a", Vector3(first.r, first.g, first.b))
	material.set_shader_parameter("color_b", Vector3(second.r, second.g, second.b))
	material.set_shader_parameter("checker_scale", scale_value)
	return material


func _solid(color_value: Color, roughness_value: float, metallic_value := 0.0) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color_value
	material.roughness = roughness_value
	material.metallic = metallic_value
	return material


func _emissive(color_value: Color, energy_value: float) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color_value
	material.emission_enabled = true
	material.emission = color_value
	material.emission_energy_multiplier = energy_value
	material.roughness = 0.35
	return material
