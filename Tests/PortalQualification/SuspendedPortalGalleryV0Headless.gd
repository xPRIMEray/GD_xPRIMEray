extends Node

const GALLERY_SCENE := preload("res://Experiences/SuspendedPortalGallery.tscn")
const MAX_APPROACH_FRAMES := 180
const POST_TRANSPORT_FRAMES := 45
const EXIT_DELTA := 0.4
const EXIT_TOLERANCE := 0.025

var _recording := false
var _player: CharacterBody3D
var _camera: Camera3D
var _source_portal: Node3D
var _arrival_portal: Node3D
var _minimum_source_delta := INF
var _previous_source_delta := 0.0
var _crossings := 0
var _obstruction_names: Array[String] = []
var _destination_delta_at_transport := INF
var _assertions_passed := true


func _ready() -> void:
	process_physics_priority = 100
	var cases := [
		{"name": "front_walk", "mode": "walk", "position": Vector3(0.0, 0.125, 3.2), "yaw": 0.0},
		{"name": "back_walk", "mode": "walk", "position": Vector3(0.0, 0.125, -5.2), "yaw": PI},
		{"name": "left_fly", "mode": "fly", "position": Vector3(-4.2, 0.125, -1.0), "yaw": -PI / 2.0},
	]
	var all_passed := true
	for case in cases:
		var result := await _run_case(case)
		all_passed = all_passed and bool(result.passed)
	print("SUSPENDED_PORTAL_V0_SUMMARY cases=%d result=%s" % [cases.size(), "PASS" if all_passed else "FAIL"])
	get_tree().quit(0 if all_passed else 1)


func _physics_process(_delta: float) -> void:
	if not _recording or not is_instance_valid(_player):
		return
	var current_delta := float(_source_portal.call("SignedRadiusDelta", _camera.global_position))
	_minimum_source_delta = min(_minimum_source_delta, current_delta)
	if _previous_source_delta > 0.0 and current_delta <= 0.0:
		_crossings += 1
	_previous_source_delta = current_delta
	if _crossings == 0:
		for index in _player.get_slide_collision_count():
			var collision := _player.get_slide_collision(index)
			var collider := collision.get_collider() if collision != null else null
			if collider != null and str(collider.name) != "Floor":
				var collider_name := str(collider.get_path())
				if not _obstruction_names.has(collider_name):
					_obstruction_names.append(collider_name)


func _run_case(case: Dictionary) -> Dictionary:
	var gallery := GALLERY_SCENE.instantiate()
	add_child(gallery)
	await get_tree().process_frame
	await get_tree().physics_frame
	await get_tree().process_frame

	_player = gallery.call("get_player") as CharacterBody3D
	_camera = _player.get_node("Head/PlayerCamera") as Camera3D
	_source_portal = gallery.call("get_source_portal") as Node3D
	_arrival_portal = gallery.call("get_arrival_portal") as Node3D
	gallery.portal_transport_applied.connect(_on_transport_applied)

	var world := gallery.get_node("SuspendedPortalGalleryWorld")
	var required_surfaces := ["Floor", "Ceiling", "BackWall", "FrontWall", "LeftWall", "RightWall"]
	for surface_name in required_surfaces:
		_assert(world.get_node_or_null("DiagnosticChamber/" + surface_name) != null, "closed chamber surface " + surface_name)
	_assert(str(gallery.call("get_semantic_scene_id")) == "suspended-portal-gallery-v0", "semantic scene id")
	_assert(world.get_node("DiagnosticGeometryZoo/ReferenceCube").is_in_group("fixture_geometry"), "cube kernel membership")
	_assert(world.get_node("DiagnosticGeometryZoo/ReferencePyramid").is_in_group("fixture_geometry"), "pyramid kernel membership")
	_assert(world.get_node("DiagnosticGeometryZoo/ReferenceSphere").is_in_group("fixture_geometry"), "sphere kernel membership")
	_assert(not world.get_node("DiagnosticGeometryZoo/ReferenceSphere").is_in_group("spatial_kernel_excluded"), "sphere is not kernel-excluded")

	_player.call("SetLocomotionModeForTesting", str(case.mode))
	_player.global_transform = Transform3D(Basis.from_euler(Vector3(0.0, float(case.yaw), 0.0)), case.position)
	_player.velocity = Vector3.ZERO
	await get_tree().physics_frame
	await get_tree().process_frame

	_previous_source_delta = float(_source_portal.call("SignedRadiusDelta", _camera.global_position))
	_minimum_source_delta = _previous_source_delta
	_crossings = 0
	_obstruction_names.clear()
	_destination_delta_at_transport = INF
	_recording = true
	Input.action_press("move_forward")
	for _frame in MAX_APPROACH_FRAMES:
		await get_tree().physics_frame
		await get_tree().process_frame
		if int(gallery.transport_application_count) > 0:
			break
	Input.action_release("move_forward")

	for _frame in POST_TRANSPORT_FRAMES:
		await get_tree().physics_frame
		await get_tree().process_frame
	_recording = false

	var transport_count := int(gallery.transport_application_count)
	var passed: bool = (
		_crossings == 1
		and transport_count == 1
		and abs(_destination_delta_at_transport - EXIT_DELTA) <= EXIT_TOLERANCE
		and _obstruction_names.is_empty()
		and _assertions_passed
	)
	print("SUSPENDED_PORTAL_V0_CASE name=%s mode=%s pass=%s min_delta=%.6f crossings=%d transports=%d destination_delta=%.6f obstructions=%s" % [
		case.name,
		case.mode,
		str(passed),
		_minimum_source_delta,
		_crossings,
		transport_count,
		_destination_delta_at_transport,
		str(_obstruction_names),
	])

	gallery.queue_free()
	await get_tree().process_frame
	return {"passed": passed}


func _on_transport_applied(_source_name: StringName, _count: int) -> void:
	_destination_delta_at_transport = float(_arrival_portal.call("SignedRadiusDelta", _camera.global_position))


func _assert(condition: bool, message: String) -> void:
	if not condition:
		_assertions_passed = false
		push_error("SUSPENDED_PORTAL_V0 ASSERT: " + message)
