extends SceneTree

const SCENE := preload("res://Experiences/SuspendedPortalGallery.tscn")
const OUTPUT_BASE := "res://output/suspended_portal_gallery_v0"
const SPHERE_MAPPING_DIAGNOSTIC_SHADER := preload("res://Experiences/sphere_checker_mapping_diagnostic.gdshader")
const SPHERE_COLOR_A := Color("edfaff")
const SPHERE_COLOR_B := Color("168bd1")
const SPHERE_WORLD_AND_OBJECT_SCALE := 1.4
const SPHERE_UV_CELLS := Vector2(16.0, 8.0)

var gallery: Node3D
var world: Node3D
var player: CharacterBody3D
var player_camera: Camera3D
var capture_camera: Camera3D
var output_root := ""
var capture_failed := false


func _initialize() -> void:
	gallery = SCENE.instantiate()
	get_root().add_child(gallery)
	await _settle(8)
	var initial_image := get_root().get_viewport().get_texture().get_image()
	var viewport_size := Vector2(initial_image.get_width(), initial_image.get_height())
	output_root = OUTPUT_BASE.path_join("p13_16x8_%dx%d" % [int(viewport_size.x), int(viewport_size.y)])
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output_root))
	world = gallery.get_node("SuspendedPortalGalleryWorld")
	player = gallery.call("get_player") as CharacterBody3D
	player_camera = player.get_node("Head/PlayerCamera") as Camera3D
	capture_camera = Camera3D.new()
	capture_camera.name = "DocumentationCamera"
	capture_camera.fov = 58.0
	capture_camera.near = 0.05
	capture_camera.far = 200.0
	world.add_child(capture_camera)
	player_camera.current = false
	capture_camera.current = true

	await _capture("01_front_architecture.png", Vector3(0.0, 3.2, 7.8), Vector3(0.0, 2.7, -1.0), Vector3.UP)
	await _capture("02_side_support.png", Vector3(7.8, 3.4, 0.8), Vector3(0.8, 2.8, -1.0), Vector3.UP)
	await _capture("03_top_approach_envelope.png", Vector3(0.0, 6.5, 5.2), Vector3(0.0, 1.4, -1.0), Vector3.UP)
	await _capture("04_player_height_approach.png", Vector3(-0.8, 1.78, 5.8), Vector3(0.0, 2.5, -1.0), Vector3.UP)
	await _capture("05_diagnostic_primitives.png", Vector3(0.0, 4.2, 8.0), Vector3(0.0, 1.5, -2.0), Vector3.UP)
	await _capture_sphere_mapping_triptych()

	capture_camera.current = false
	player_camera.current = true
	player.global_transform = Transform3D(Basis.IDENTITY, Vector3(0.0, 0.125, 7.5))
	var film_controller := world.get_node("FilmController")
	var film := world.get_node("GrinFilmCamera")
	await _qualify_input_contract(film_controller, film)
	await _dispatch_input_key(KEY_H)
	await _settle(2)
	var inspect_controls: Dictionary = gallery.call("get_presentation_diagnostics")
	var expanded_truth_rect: Rect2 = inspect_controls["truth_rect"]
	var expanded_instrument_rect: Rect2 = inspect_controls["instrument_rect"]
	var film_plate_rect: Rect2 = inspect_controls["film_plate_rect"]
	print("SUSPENDED_PORTAL_FULL_HUD_RECTS truth=%s instrument=%s film=%s truthLabel=%s instrumentLabel=%s" % [expanded_truth_rect, expanded_instrument_rect, film_plate_rect, inspect_controls["truth_label_rect"], inspect_controls["instrument_label_rect"]])
	_assert(bool(inspect_controls["full_hud"]), "H did not open full instrumentation")
	_assert(expanded_truth_rect.size.y > 116.0, "expanded truth HUD did not grow")
	_assert(expanded_instrument_rect.size.y > 150.0, "expanded instrument HUD did not grow")
	_assert(expanded_truth_rect.end.x < expanded_instrument_rect.position.x, "full HUD panels overlap")
	_assert(expanded_instrument_rect.end.x < film_plate_rect.position.x, "full instrument HUD overlaps film plate")
	_assert(_rect_contains(expanded_truth_rect, inspect_controls["truth_label_rect"]), "full control text clips truth panel")
	_assert(_rect_contains(expanded_instrument_rect, inspect_controls["instrument_label_rect"]), "full instrument text clips panel")
	_assert(_full_hud_contract_present(String(inspect_controls["truth_text"])), "full HUD control contract incomplete")
	await _capture_current("07_full_instrumentation.png")
	await _dispatch_input_key(KEY_H)
	await _settle(1)
	await _dispatch_input_key(KEY_F)
	var separated_controls: Dictionary = gallery.call("get_presentation_diagnostics")
	_assert(bool(separated_controls["field_visible"]), "F did not enable field vectors")
	await _dispatch_input_key(KEY_R)
	separated_controls = gallery.call("get_presentation_diagnostics")
	_assert(not bool(separated_controls["rings_visible"]) and bool(separated_controls["field_visible"]), "R was not independent from field vectors")
	await _dispatch_input_key(KEY_R)
	await _dispatch_input_key(KEY_L)
	separated_controls = gallery.call("get_presentation_diagnostics")
	_assert(not bool(separated_controls["labels_visible"]) and bool(separated_controls["field_visible"]), "L was not independent from field vectors")
	await _dispatch_input_key(KEY_L)
	print("SUSPENDED_PORTAL_PRESENTATION_CONTROLS hud=H field=F rings=R labels=L result=PASS")
	film_controller.call("set_mode", 2)
	await _wait_for_film_progress(film, 600)
	var row_cursor := int(film.get("FilmRowCursor"))
	_assert(row_cursor > 0, "GrinFilmCamera LIVE did not advance")
	await _capture_current("06_grin_observation.png")
	var camera_before_hotkeys := player_camera.global_transform
	var portal_count_before_hotkeys := int(gallery.transport_application_count)
	var macro_before_hotkeys := int(film.get("RuntimeMacroSelectedMode"))
	var hud_capture: Array = await _capture_via_hotkey(false)
	var beauty_capture: Array = await _capture_via_hotkey(true)
	var capture_diagnostics: Dictionary = gallery.call("get_presentation_diagnostics")
	_assert(not bool(capture_diagnostics["full_hud"]), "capture did not restore Minimal HUD mode")
	_assert(bool(capture_diagnostics["hud_visible"]), "beauty capture did not restore HUD visibility")
	_assert(bool(capture_diagnostics["last_capture_hud_hidden_frame"]), "beauty capture did not wait for hidden HUD frame")
	_assert(bool(capture_diagnostics["last_capture_state_restored"]), "beauty capture state restoration")
	_assert(player_camera.global_transform.is_equal_approx(camera_before_hotkeys), "capture changed camera transform")
	_assert(int(gallery.transport_application_count) == portal_count_before_hotkeys, "capture changed portal transport count")
	_assert(int(film.get("RuntimeMacroSelectedMode")) == macro_before_hotkeys, "F10 invoked legacy macro mode")
	_assert(int(film_controller.get("_mode")) == 2, "capture changed LIVE film mode")
	print("SUSPENDED_PORTAL_HOTKEY_CAPTURE hud=%s beauty=%s restored=1 macro_unchanged=1" % [hud_capture[0], beauty_capture[0]])
	film_controller.call("set_mode", 0)

	var diagnostics: Dictionary = gallery.call("get_presentation_diagnostics")
	var truth_rect: Rect2 = diagnostics["truth_rect"]
	var instrument_rect: Rect2 = diagnostics["instrument_rect"]
	var truth_ratio := truth_rect.size.x * truth_rect.size.y / (377.0 * 221.0)
	var instrument_ratio := instrument_rect.size.x * instrument_rect.size.y / (390.0 * 248.0)
	_assert(truth_ratio >= 0.40 and truth_ratio <= 0.50, "truth HUD area target")
	_assert(instrument_ratio >= 0.45 and instrument_ratio <= 0.60, "instrument HUD area target")
	_assert(truth_rect.end.x < instrument_rect.position.x, "compact HUD panels overlap")
	_assert(_rect_contains(truth_rect, diagnostics["truth_label_rect"]), "minimal truth text clips panel")
	_assert(_rect_contains(instrument_rect, diagnostics["instrument_label_rect"]), "minimal instrument text clips panel")
	_assert(String(diagnostics["truth_text"]).contains("H FULL"), "Minimal HUD does not expose H")
	print("SUSPENDED_PORTAL_HUD_METRICS resolution=%dx%d truth_before=377x221 truth_after=%dx%d truth_area_ratio=%.4f instrument_before=390x248 instrument_after=%dx%d instrument_area_ratio=%.4f" % [
		int(viewport_size.x), int(viewport_size.y),
		int(truth_rect.size.x), int(truth_rect.size.y), truth_ratio,
		int(instrument_rect.size.x), int(instrument_rect.size.y), instrument_ratio,
	])
	if capture_failed:
		print("SUSPENDED_PORTAL_CAPTURE FAIL output=%s film_row_cursor=%d" % [output_root, row_cursor])
		quit(1)
	else:
		print("SUSPENDED_PORTAL_CAPTURE PASS output=%s film_row_cursor=%d" % [output_root, row_cursor])
		quit(0)


func _capture(file_name: String, origin: Vector3, target: Vector3, up: Vector3) -> void:
	capture_camera.global_position = origin
	capture_camera.look_at(target, up)
	await _settle(5)
	await _capture_current(file_name)


func _capture_current(file_name: String) -> void:
	await process_frame
	var image := get_root().get_viewport().get_texture().get_image()
	_assert(image != null and image.get_width() > 0 and image.get_height() > 0, file_name + " viewport image")
	var output_path := ProjectSettings.globalize_path(output_root.path_join(file_name))
	_assert(image.save_png(output_path) == OK, "save " + file_name)
	print("SUSPENDED_PORTAL_CAPTURE_FRAME file=%s size=%dx%d sha256=%s" % [
		output_path,
		image.get_width(),
		image.get_height(),
		FileAccess.get_sha256(output_path),
	])


func _capture_sphere_mapping_triptych() -> void:
	var sphere_body := world.get_node("DiagnosticGeometryZoo/ReferenceSphere") as StaticBody3D
	var sphere_mesh := sphere_body.get_node("PresentationMesh") as MeshInstance3D
	var sphere_collision: CollisionShape3D = null
	for child in sphere_body.get_children():
		if child is CollisionShape3D:
			sphere_collision = child as CollisionShape3D
			break
	var sphere_resource := sphere_mesh.mesh as SphereMesh
	_assert(sphere_body != null and sphere_mesh != null and sphere_collision != null and sphere_resource != null, "sphere mapping fixture nodes")
	if sphere_body == null or sphere_mesh == null or sphere_collision == null or sphere_resource == null:
		return

	var original_mesh_material: Material = sphere_resource.material
	var original_override: Material = sphere_mesh.material_override
	var original_camera_transform := capture_camera.global_transform
	var original_camera_fov := capture_camera.fov
	var presentation_before: Dictionary = gallery.call("get_presentation_diagnostics")
	var hud_was_visible := bool(presentation_before["hud_visible"])
	var scientific_state_before := _sphere_mapping_scientific_state(sphere_body, sphere_collision)
	var triptych_root := output_root.path_join("sphere_mapping_triptych")
	_assert(DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(triptych_root)) == OK, "create sphere mapping output directory")

	# Establish one camera/light/presentation state before the first image. Between
	# the three captures, only ReferenceSphere.material_override is changed.
	gallery.call("_set_hud_visible", false)
	capture_camera.fov = 48.0
	capture_camera.global_position = Vector3(-5.2, 1.65, 0.2)
	capture_camera.look_at(sphere_body.global_position, Vector3.UP)
	await _settle(5)
	var comparison_camera := capture_camera.global_transform
	var comparison_sphere_transform := sphere_body.global_transform
	var variants: Array[Dictionary] = []

	# Untouched reference: the existing mesh material remains installed and no
	# override is introduced for this frame.
	_assert(sphere_resource.material == original_mesh_material and sphere_mesh.material_override == original_override, "world-space reference material untouched")
	variants.append(await _capture_mapping_frame(triptych_root, "01_world_space_reference.png", "World-space reference"))

	var uv_material := _make_sphere_mapping_material(0)
	sphere_mesh.material_override = uv_material
	await _settle(3)
	_assert(capture_camera.global_transform.is_equal_approx(comparison_camera), "UV capture camera changed")
	_assert(sphere_body.global_transform.is_equal_approx(comparison_sphere_transform), "UV capture sphere transform changed")
	variants.append(await _capture_mapping_frame(triptych_root, "02_uv_space_diagnostic.png", "UV-space diagnostic"))

	var object_material := _make_sphere_mapping_material(1)
	sphere_mesh.material_override = object_material
	await _settle(3)
	_assert(capture_camera.global_transform.is_equal_approx(comparison_camera), "object-space capture camera changed")
	_assert(sphere_body.global_transform.is_equal_approx(comparison_sphere_transform), "object-space capture sphere transform changed")
	variants.append(await _capture_mapping_frame(triptych_root, "03_centered_object_space_diagnostic.png", "Centered object-space diagnostic"))

	# Restoration is deliberately unconditional with respect to image-save
	# results: failed captures set capture_failed but never bypass this cleanup.
	sphere_mesh.material_override = original_override
	capture_camera.global_transform = original_camera_transform
	capture_camera.fov = original_camera_fov
	gallery.call("_set_hud_visible", hud_was_visible)
	await _settle(3)

	var scientific_state_after := _sphere_mapping_scientific_state(sphere_body, sphere_collision)
	var mesh_material_restored := sphere_resource.material == original_mesh_material
	var override_restored := sphere_mesh.material_override == original_override
	_assert(mesh_material_restored, "original sphere mesh material reference restored")
	_assert(override_restored, "original sphere material override restored")
	_assert(scientific_state_after == scientific_state_before, "sphere mapping capture changed scientific or traversal state")
	_assert(capture_camera.global_transform.is_equal_approx(original_camera_transform) and is_equal_approx(capture_camera.fov, original_camera_fov), "documentation camera restoration")
	_assert(bool(gallery.call("get_presentation_diagnostics")["hud_visible"]) == hud_was_visible, "HUD visibility restoration after sphere mapping capture")

	var nominal_cartesian_cell := 1.0 / SPHERE_WORLD_AND_OBJECT_SCALE
	var nominal_equatorial_uv_cell := TAU * sphere_resource.radius / SPHERE_UV_CELLS.x
	var nominal_meridional_uv_cell := PI * sphere_resource.radius / SPHERE_UV_CELLS.y
	var manifest := {
		"schema": "xprimeray.sphere-mapping-triptych.v1",
		"semantic_scene_id": String(gallery.call("get_semantic_scene_id")),
		"sphere": {
			"node": String(sphere_body.get_path()),
			"radius": sphere_resource.radius,
			"radial_segments": sphere_resource.radial_segments,
			"rings": sphere_resource.rings,
			"collider_shape": sphere_collision.shape.get_class(),
		},
		"capture": {
			"resolution": [get_root().get_viewport().size.x, get_root().get_viewport().size.y],
			"camera_origin": _vector3_array(comparison_camera.origin),
			"camera_basis": _basis_array(comparison_camera.basis),
			"camera_fov_degrees": 48.0,
			"same_camera_light_transform": true,
		},
		"mapping": {
			"colors_srgb_hex": [SPHERE_COLOR_A.to_html(false), SPHERE_COLOR_B.to_html(false)],
			"world_space": "existing absolute-world Cartesian lattice; untouched reference material",
			"uv_space": "16 x 8 parameter-space cells; chosen empirically for about four visible bands across each frontal axis in this fixed view; seam/poles and latitude area compression are expected",
			"centered_object_space": "mesh-local Cartesian lattice with the central cell centered on the mesh origin",
			"world_and_object_scale": SPHERE_WORLD_AND_OBJECT_SCALE,
			"uv_cells": [SPHERE_UV_CELLS.x, SPHERE_UV_CELLS.y],
			"nominal_cartesian_cell_size": nominal_cartesian_cell,
			"nominal_uv_equator_arc_per_cell": nominal_equatorial_uv_cell,
			"nominal_uv_meridian_arc_per_cell": nominal_meridional_uv_cell,
			"qualification": "Comparable nominal density only; no mapping is claimed equal-area or globally distortion-free/equal-angular.",
		},
		"variants": variants,
		"restoration": {
			"original_mesh_material_reference": mesh_material_restored,
			"original_material_override_reference": override_restored,
			"scientific_and_traversal_state_unchanged": scientific_state_after == scientific_state_before,
		},
	}
	var manifest_path := triptych_root.path_join("sphere_mapping_manifest.json")
	var manifest_file := FileAccess.open(ProjectSettings.globalize_path(manifest_path), FileAccess.WRITE)
	_assert(manifest_file != null, "open sphere mapping manifest")
	if manifest_file != null:
		manifest_file.store_string(JSON.stringify(manifest, "\t"))
		manifest_file.close()
	print("SPHERE_MAPPING_TRIPTYCH variants=%d material_restored=%d state_unchanged=%d manifest=%s" % [
		variants.size(),
		int(mesh_material_restored and override_restored),
		int(scientific_state_after == scientific_state_before),
		ProjectSettings.globalize_path(manifest_path),
	])


func _make_sphere_mapping_material(mapping_mode: int) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = SPHERE_MAPPING_DIAGNOSTIC_SHADER
	material.set_shader_parameter("mapping_mode", mapping_mode)
	material.set_shader_parameter("color_a", Vector3(SPHERE_COLOR_A.r, SPHERE_COLOR_A.g, SPHERE_COLOR_A.b))
	material.set_shader_parameter("color_b", Vector3(SPHERE_COLOR_B.r, SPHERE_COLOR_B.g, SPHERE_COLOR_B.b))
	material.set_shader_parameter("uv_cells", SPHERE_UV_CELLS)
	material.set_shader_parameter("object_checker_scale", SPHERE_WORLD_AND_OBJECT_SCALE)
	return material


func _capture_mapping_frame(directory: String, file_name: String, label: String) -> Dictionary:
	await process_frame
	var image := get_root().get_viewport().get_texture().get_image()
	_assert(image != null and image.get_width() > 0 and image.get_height() > 0, file_name + " viewport image")
	var output_path := ProjectSettings.globalize_path(directory.path_join(file_name))
	var save_result := image.save_png(output_path) if image != null else ERR_CANT_CREATE
	_assert(save_result == OK, "save " + file_name)
	var sha256 := FileAccess.get_sha256(output_path) if FileAccess.file_exists(output_path) else ""
	print("SPHERE_MAPPING_FRAME label=%s file=%s size=%dx%d sha256=%s" % [
		label,
		output_path,
		image.get_width() if image != null else 0,
		image.get_height() if image != null else 0,
		sha256,
	])
	return {
		"label": label,
		"file": file_name,
		"sha256": sha256,
	}


func _sphere_mapping_scientific_state(sphere_body: StaticBody3D, sphere_collision: CollisionShape3D) -> Array:
	var film := world.get_node("GrinFilmCamera")
	var film_controller := world.get_node("FilmController")
	var source_field := world.get_node("SuspendedPortalField") as Node3D
	var arrival_field := world.get_node("ArrivalPortalField") as Node3D
	var source_boundary := world.get_node("SuspendedPortalBoundary") as Node3D
	var arrival_boundary := world.get_node("ArrivalPortalBoundary") as Node3D
	return [
		sphere_body.global_transform,
		sphere_collision.global_transform,
		sphere_collision.shape.get_instance_id(),
		gallery.get_source_portal().global_transform,
		gallery.get_arrival_portal().global_transform,
		source_field.global_transform,
		arrival_field.global_transform,
		float(source_field.get("ROuter")),
		float(source_field.get("Amp")),
		float(arrival_field.get("ROuter")),
		float(arrival_field.get("Amp")),
		source_boundary.global_transform,
		arrival_boundary.global_transform,
		player.global_transform,
		player.velocity,
		int(gallery.transport_application_count),
		String(film.get("ProbeProfileStableId")),
		int(film.get("SealedProbeViewGeneration")),
		int(film.get("CurrentProbeView")),
		int(film_controller.get("_mode")),
	]


func _vector3_array(value: Vector3) -> Array:
	return [value.x, value.y, value.z]


func _basis_array(value: Basis) -> Array:
	return [_vector3_array(value.x), _vector3_array(value.y), _vector3_array(value.z)]


func _settle(frames: int) -> void:
	for _frame in frames:
		await process_frame


func _wait_for_film_progress(film: Node, maximum_physics_frames: int) -> void:
	for _frame in maximum_physics_frames:
		if int(film.get("FilmRowCursor")) > 0:
			return
		await physics_frame


func _press_node_key(node: Node, keycode: Key, shift_pressed := false) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	event.shift_pressed = shift_pressed
	node.call("_unhandled_input", event)


func _qualify_input_contract(film_controller: Node, film: Node) -> void:
	_assert(_action_has_key("move_forward", KEY_W), "W is not bound to move_forward")
	_assert(_action_has_key("move_backward", KEY_S), "S is not bound to move_backward")
	_assert(_action_has_key("move_left", KEY_A), "A is not bound to move_left")
	_assert(_action_has_key("move_right", KEY_D), "D is not bound to move_right")
	_assert(_action_has_key("move_sprint", KEY_SHIFT), "Shift is not bound to move_sprint")

	var player_transform := player.global_transform
	Input.action_press("move_forward")
	await physics_frame
	await physics_frame
	Input.action_release("move_forward")
	var walk_distance := player.global_position.distance_to(player_transform.origin)
	player.global_transform = player_transform
	player.velocity = Vector3.ZERO
	Input.action_press("move_forward")
	Input.action_press("move_sprint")
	await physics_frame
	await physics_frame
	Input.action_release("move_forward")
	Input.action_release("move_sprint")
	var sprint_distance := player.global_position.distance_to(player_transform.origin)
	_assert(walk_distance > 0.001, "W did not move the production player")
	_assert(sprint_distance > walk_distance * 1.5, "Shift did not apply sprint")
	player.global_transform = player_transform
	player.velocity = Vector3.ZERO

	var player_basis := player.transform.basis
	var head := player.get_node("Head") as Node3D
	var head_rotation := head.rotation
	var mouse_event := InputEventMouseMotion.new()
	mouse_event.relative = Vector2(12.0, -8.0)
	player.call("_unhandled_input", mouse_event)
	_assert(not player.transform.basis.is_equal_approx(player_basis) and not head.rotation.is_equal_approx(head_rotation), "mouse look did not change yaw and pitch")
	player.transform.basis = player_basis
	head.rotation = head_rotation

	var locomotion_before := int(player.get("_loco_mode"))
	_press_node_key(player, KEY_V)
	_assert(int(player.get("_loco_mode")) != locomotion_before, "V did not toggle Walk/Fly")
	_press_node_key(player, KEY_V)
	_assert(int(player.get("_loco_mode")) == locomotion_before, "V did not restore Walk/Fly")

	var field_before := bool(gallery.get("_source_field").get("DebugVizInGame"))
	await _dispatch_input_key(KEY_F)
	_assert(bool(gallery.get("_source_field").get("DebugVizInGame")) != field_before, "F did not toggle field structure")
	await _dispatch_input_key(KEY_F)
	_assert(bool(gallery.get("_source_field").get("DebugVizInGame")) == field_before, "F did not restore field structure")

	var opacity_before := int(film_controller.call("GetOpacityPercent"))
	_press_node_key(film_controller, KEY_BRACKETLEFT)
	_assert(int(film_controller.call("GetOpacityPercent")) < opacity_before, "[ did not lower film opacity")
	_press_node_key(film_controller, KEY_BRACKETRIGHT)
	_assert(int(film_controller.call("GetOpacityPercent")) == opacity_before, "] did not restore film opacity")
	var shading_before := String(film_controller.call("GetShadingModeName"))
	for _index in 4:
		_press_node_key(film_controller, KEY_N)
	_assert(String(film_controller.call("GetShadingModeName")) == shading_before, "N shading cycle did not return to origin")

	var profile_before := String(film.get("ProbeProfileStableId"))
	await _dispatch_input_key(KEY_P)
	_assert(String(film.get("ProbeProfileStableId")) != profile_before, "P did not cycle Probe Profile")
	await _dispatch_input_key(KEY_P)
	_assert(String(film.get("ProbeProfileStableId")) == profile_before, "P did not restore Probe Profile")
	var view_before := int(film.get("CurrentProbeView"))
	var generation_before := int(film.get("SealedProbeViewGeneration"))
	await _dispatch_input_key(KEY_Q)
	_assert(int(film.get("CurrentProbeView")) == (view_before + 1) % 3, "Q did not select next Probe View")
	await _dispatch_input_key(KEY_Q, true)
	_assert(int(film.get("CurrentProbeView")) == view_before, "Shift+Q did not select previous Probe View")
	_assert(int(film.get("SealedProbeViewGeneration")) == generation_before, "Q changed sealed generation")

	_assert(int(film_controller.get("_mode")) == 0, "G qualification did not start OFF")
	_press_node_key(film_controller, KEY_G)
	_assert(int(film_controller.get("_mode")) == 1, "G did not enter SNAPSHOT")
	_press_node_key(film_controller, KEY_G)
	_assert(int(film_controller.get("_mode")) == 2, "G did not enter LIVE")
	_press_node_key(film_controller, KEY_G)
	_assert(int(film_controller.get("_mode")) == 0, "G did not return OFF")

	var macro_before := int(film.get("RuntimeMacroSelectedMode"))
	for legacy_key in [KEY_F6, KEY_F7, KEY_F8, KEY_F9]:
		await _dispatch_input_key(legacy_key)
	_assert(int(film.get("RuntimeMacroSelectedMode")) == macro_before, "disabled F6-F9 legacy macro changed state")
	var renderer := world.get_node("RayBeamRenderer")
	var debug_mode_before := int(renderer.get("DebugMode"))
	var hit_filter_before := bool(renderer.get("DebugDrawOnlyHits"))
	_assert(not bool(renderer.get("DebugHotkeysEnabled")), "F1/F2 renderer debug hotkeys remain active")
	await _dispatch_input_key(KEY_F1)
	await _dispatch_input_key(KEY_F2)
	_assert(int(renderer.get("DebugMode")) == debug_mode_before and bool(renderer.get("DebugDrawOnlyHits")) == hit_filter_before, "disabled F1/F2 renderer debug changed state")
	var unavailable_before := _control_state(film_controller, film)
	for unavailable_key in [KEY_O, KEY_E, KEY_ESCAPE]:
		await _dispatch_input_key(unavailable_key)
	_assert(_control_state(film_controller, film) == unavailable_before, "unavailable O/E/Esc changed gallery state")
	print("SUSPENDED_PORTAL_INPUT_CONTRACT movement=PASS mouse=PASS VFGPNQ=PASS brackets=PASS legacy_macros=DISABLED")


func _action_has_key(action: StringName, expected_key: Key) -> bool:
	for event in InputMap.action_get_events(action):
		if event is InputEventKey and (event.keycode == expected_key or event.physical_keycode == expected_key):
			return true
	return false


func _full_hud_contract_present(text: String) -> bool:
	for token in ["WASD", "Mouse Look", "Shift Sprint", "V Walk/Fly", "F Field", "R Rings", "L Labels", "G Film", "P Profile", "Shift+Q", "[ / ]", "N Film shading", "H Minimal HUD", "F10 Capture", "Shift+F10 Beauty", "O Optical-only", "Esc Workbench", "F1/F2 renderer debug disabled", "E reserved", "F6-F9 legacy macros disabled"]:
		if not text.contains(token):
			push_error("SUSPENDED_PORTAL_HUD_CONTRACT missing token: " + token)
			return false
	return true


func _rect_contains(outer: Rect2, inner: Rect2) -> bool:
	return inner.position.x >= outer.position.x - 0.5 and inner.position.y >= outer.position.y - 0.5 and inner.end.x <= outer.end.x + 0.5 and inner.end.y <= outer.end.y + 0.5


func _capture_via_hotkey(clean: bool) -> Array:
	var event := InputEventKey.new()
	event.keycode = KEY_F10
	event.physical_keycode = KEY_F10
	event.pressed = true
	event.shift_pressed = clean
	Input.parse_input_event(event)
	var result: Array = await gallery.screenshot_captured
	event.pressed = false
	Input.parse_input_event(event)
	_assert(not String(result[0]).is_empty(), "F10 capture path")
	_assert(bool(result[1]) == clean, "F10 clean mode")
	_assert(bool(result[2]), "F10 state restoration")
	_assert(FileAccess.file_exists(String(result[0])), "F10 output exists")
	return result


func _dispatch_input_key(keycode: Key, shift_pressed := false) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	event.shift_pressed = shift_pressed
	Input.parse_input_event(event)
	await process_frame
	await process_frame
	event.pressed = false
	Input.parse_input_event(event)
	await process_frame


func _control_state(film_controller: Node, film: Node) -> Array:
	return [
		int(player.get("_loco_mode")),
		bool(gallery.get("_source_field").get("DebugVizInGame")),
		bool(gallery.get("_rings_visible")),
		bool(gallery.get("_labels_visible")),
		bool(gallery.get("_full_hud")),
		int(film_controller.get("_mode")),
		int(film_controller.call("GetOpacityPercent")),
		String(film_controller.call("GetShadingModeName")),
		String(film.get("ProbeProfileStableId")),
		int(film.get("CurrentProbeView")),
		int(film.get("RuntimeMacroSelectedMode")),
	]


func _assert(condition: bool, message: String) -> void:
	if condition:
		return
	capture_failed = true
	push_error("SUSPENDED_PORTAL_CAPTURE FAIL: " + message)
