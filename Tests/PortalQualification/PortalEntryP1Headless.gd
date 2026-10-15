extends Node

const CHAMBER_SCENE := preload("res://Transport/TransportChamberWorld.tscn")
const START_POSITION := Vector3(-2.0, 0.125, 8.5)
const MAX_APPROACH_PHYSICS_FRAMES := 180
const POST_TRANSPORT_PHYSICS_FRAMES := 45
const TRANSPORT_JUMP_DISTANCE := 50.0
const DESTINATION_DELTA_EXPECTED := 0.4
const DESTINATION_DELTA_TOLERANCE := 0.025

var _recording := false
var _player: CharacterBody3D
var _camera: Camera3D
var _gallery_portal: Node3D
var _earth_portal: Node3D
var _pedestal: StaticBody3D
var _previous_process_camera_position := Vector3.ZERO
var _previous_gallery_delta := 0.0
var _minimum_gallery_delta := INF
var _positive_to_nonpositive_crossings := 0
var _transport_applications := 0
var _destination_delta_at_transport := INF
var _pedestal_collision_observed := false


func _ready() -> void:
	process_priority = 100
	process_physics_priority = 100
	var expect_crossing := _argument_value("--expect", "crossing") == "crossing"
	var walk := await _run_case("walk", expect_crossing)
	var fly := await _run_case("fly", expect_crossing)
	var passed := bool(walk.passed) and bool(fly.passed)
	print("PORTAL_P1_SUMMARY expect=%s walk=%s fly=%s" % [
		"crossing" if expect_crossing else "blocked",
		"PASS" if walk.passed else "FAIL",
		"PASS" if fly.passed else "FAIL",
	])
	get_tree().quit(0 if passed else 1)


func _physics_process(_delta: float) -> void:
	if not _recording or not is_instance_valid(_player):
		return

	var camera_position := _camera.global_position
	var gallery_delta := float(_gallery_portal.call("SignedRadiusDelta", camera_position))
	_minimum_gallery_delta = min(_minimum_gallery_delta, gallery_delta)
	if _previous_gallery_delta > 0.0 and gallery_delta <= 0.0:
		_positive_to_nonpositive_crossings += 1
	_previous_gallery_delta = gallery_delta

	for index in _player.get_slide_collision_count():
		var collision := _player.get_slide_collision(index)
		if collision != null and collision.get_collider() == _pedestal:
			_pedestal_collision_observed = true


func _process(_delta: float) -> void:
	if not _recording or not is_instance_valid(_camera):
		return
	var camera_position := _camera.global_position
	if camera_position.distance_to(_previous_process_camera_position) > TRANSPORT_JUMP_DISTANCE:
		_transport_applications += 1
		_destination_delta_at_transport = float(_earth_portal.call("SignedRadiusDelta", camera_position))
	_previous_process_camera_position = camera_position


func _run_case(mode: String, expect_crossing: bool) -> Dictionary:
	var chamber := CHAMBER_SCENE.instantiate()
	add_child(chamber)
	await get_tree().process_frame
	await get_tree().physics_frame

	_player = chamber.get_node("TransportChamberPlayer") as CharacterBody3D
	_camera = _player.get_node("Head/PlayerCamera") as Camera3D
	_gallery_portal = chamber.get_node("OverspaceTrophyRoom/Gallery/EarthOrbPortal") as Node3D
	_earth_portal = chamber.get_node("OverspaceTrophyRoom/Worlds/EarthWorld/EarthArrivalPortal") as Node3D
	_pedestal = chamber.get_node("GalleryCollision/EarthPedestalBase") as StaticBody3D

	_player.call("SetLocomotionModeForTesting", mode)
	_player.global_transform = Transform3D(Basis.IDENTITY, START_POSITION)
	_player.velocity = Vector3.ZERO
	await get_tree().physics_frame

	_previous_process_camera_position = _camera.global_position
	_previous_gallery_delta = float(_gallery_portal.call("SignedRadiusDelta", _previous_process_camera_position))
	_minimum_gallery_delta = _previous_gallery_delta
	_positive_to_nonpositive_crossings = 0
	_transport_applications = 0
	_destination_delta_at_transport = INF
	_pedestal_collision_observed = false
	_recording = true

	Input.action_press("move_forward")
	for _frame in MAX_APPROACH_PHYSICS_FRAMES:
		await get_tree().physics_frame
		await get_tree().process_frame
		if _transport_applications > 0:
			break
	Input.action_release("move_forward")

	if _transport_applications > 0:
		for _frame in POST_TRANSPORT_PHYSICS_FRAMES:
			await get_tree().physics_frame
			await get_tree().process_frame
	_recording = false

	var exceptions := _player.get_collision_exceptions()
	var pedestal_exception_count := 0
	var unrelated_exception_count := 0
	for exception_body in exceptions:
		if exception_body == _pedestal:
			pedestal_exception_count += 1
		else:
			unrelated_exception_count += 1

	var passed: bool
	if expect_crossing:
		passed = (
			_positive_to_nonpositive_crossings == 1
			and _transport_applications == 1
			and abs(_destination_delta_at_transport - DESTINATION_DELTA_EXPECTED) <= DESTINATION_DELTA_TOLERANCE
			and pedestal_exception_count == 1
			and unrelated_exception_count == 0
		)
	else:
		passed = (
			_pedestal_collision_observed
			and _minimum_gallery_delta > 0.0
			and _positive_to_nonpositive_crossings == 0
			and _transport_applications == 0
			and pedestal_exception_count == 0
			and unrelated_exception_count == 0
		)

	print("PORTAL_P1_CASE mode=%s expect=%s pass=%s min_gallery_delta=%.6f crossings=%d transports=%d destination_delta=%.6f pedestal_collision=%s pedestal_exceptions=%d unrelated_exceptions=%d" % [
		mode,
		"crossing" if expect_crossing else "blocked",
		str(passed),
		_minimum_gallery_delta,
		_positive_to_nonpositive_crossings,
		_transport_applications,
		_destination_delta_at_transport,
		str(_pedestal_collision_observed),
		pedestal_exception_count,
		unrelated_exception_count,
	])

	chamber.queue_free()
	await get_tree().process_frame
	return {"passed": passed}


func _argument_value(name: String, fallback: String) -> String:
	var prefix := name + "="
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with(prefix):
			return argument.trim_prefix(prefix).strip_edges().to_lower()
	return fallback
