extends SceneTree

func _add_test_body(parent: Node, body_name: String, x: float, color: Color) -> void:
	var body := StaticBody3D.new()
	body.name = body_name
	body.position = Vector3(x, 2.4, 4.0)
	body.collision_layer = 1
	body.collision_mask = 0
	body.add_to_group("fixture_geometry")
	body.add_to_group("raytrace_geometry")
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(2.0, 2.0, 0.5)
	shape.shape = box
	body.add_child(shape)
	var mesh := MeshInstance3D.new()
	var visual_box := BoxMesh.new()
	visual_box.size = box.size
	mesh.mesh = visual_box
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	mesh.material_override = material
	body.add_child(mesh)
	parent.add_child(body)

func _initialize() -> void:
	change_scene_to_file("res://ObservatoryWorkbench.tscn")
	await scene_changed
	await process_frame
	var chamber: Node = current_scene.get_node("PlayableWorld/TransportChamberWorld")
	var film: Node = chamber.get_node("GrinFilmCamera")
	var renderer: Node = chamber.get_node("RayBeamRenderer")
	var controller: Node = chamber.get_node("FilmController")
	_add_test_body(chamber, "LiveSeedObjectA", -2.4, Color(0.1, 0.7, 1.0))
	_add_test_body(chamber, "LiveSeedObjectB", 2.4, Color(1.0, 0.3, 0.1))
	renderer.set("FieldStrength", 0.0)
	renderer.set("BendScale", 0.0)
	film.set("Width", 80)
	film.set("Height", 45)
	film.set("FilmResolutionScale", 1.0)
	film.set("DebugSnapshotLog", true)
	controller.call("set_mode", 2)
	print("MULTI_ENTITY_START scene=%s" % current_scene.name)
	for _i in range(110):
		await process_frame
	controller.call("set_mode", 0)
	for _i in range(8):
		await process_frame
	print("MULTI_ENTITY_PASS")
	quit(0)
