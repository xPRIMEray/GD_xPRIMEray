extends SceneTree

# TEXTURE-R0A.2 headless qualification: MaterialCpu binding inventories, classification, diagnostics and the
# snapshot lifecycle. The expectations below are derived independently in GDScript from the live scene
# (presentation mesh rule, material precedence, authored parameters), not from the C# adapter.
# Usage: godot --headless --path <ws> --script res://scripts/test_material_cpu_bindings.gd

const WB := "res://ObservatoryWorkbench.tscn"
const GAL := "res://Experiences/SuspendedPortalGallery.tscn"
const CHECKER_PATH := "res://Experiences/checkerboard_3d.gdshader"
const MATERIAL_CPU := "Material (CPU)"
const SCAN_WAIT_FRAME_LIMIT := 600
const SCAN_WAIT_MS_LIMIT := 30000

var failures: Array[String] = []
var kind := ""
var cases := 0
var root_scene: Node
var chamber: Node
var film: Node
var ctrl: Node


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _open("gal")
	await _select_material_cpu_live()
	await _gallery_inventory()
	await _independent_inventory_check()
	await _diagnostic_fixtures()
	await _invalidation_material_swap()
	await _invalidation_live_reset_and_mode_leave()
	await _manifest_stability_across_reload()
	await _open("wb")
	await _select_material_cpu_live()
	await _workbench_inventory()
	await _independent_inventory_check()
	await _dial_switch_is_not_a_binding_change()
	await _scene_transitions()
	if failures.is_empty():
		print("MATERIAL_CPU_BINDINGS PASS cases=%d server=%s" % [cases, DisplayServer.get_name()])
		quit(0)
	else:
		for failure in failures:
			push_error("MATERIAL_CPU_BINDINGS FAIL: " + failure)
		print("MATERIAL_CPU_BINDINGS FAIL count=%d cases=%d first=%s" % [failures.size(), cases, failures[0]])
		quit(1)


# ------------------------------------------------------------------ inventories

func _gallery_inventory() -> void:
	var rows := _rows()
	var state := _state()
	print("MATERIAL_CPU_BINDINGS gal entries=%d bound=%d unsupported=%d descriptors=%d manifest=%s provenance=%s scan_ms=%.3f" % [
		int(state["entries"]), int(state["bound"]), int(state["unsupported_entries"]), int(state["descriptors"]),
		state["manifest_sha256"], state["table_provenance_sha256"], float(state["last_scan_ms"])])
	_print_manifest()
	cases += 1
	_check(rows.size() == _collision_shape_count(), "every collision shape is inventoried (%d rows, %d shapes)" % [rows.size(), _collision_shape_count()])
	_expect_checker("ReferenceCube", "Exact", Color("e83d39"), Color("ffd536"), 1.25, 1)
	_expect_checker("ReferenceSphere", "Proxy", Color("edfaff"), Color("168bd1"), 1.4, 1)
	_expect_checker("ReferencePyramid", "Proxy", Color("e932a2"), Color("79f2ff"), 1.2, 4)
	var plinth := _rows_for("OffsetStonePlinth")
	cases += 1
	_check(plinth.size() == 1 and bool(plinth[0]["bound"]) and str(plinth[0]["kind"]) == "Flat" and str(plinth[0]["correspondence"]) == "Exact"
		and _rgb_equal(plinth[0]["flat"], Color("787f8f")), "OffsetStonePlinth is Flat 787f8f Exact (%s)" % [plinth])
	for row in rows:
		_check(not str(row["body_path"]).contains("@"), "body path is deterministic (no auto name): %s" % row["body_path"])
		if str(row["body_path"]).get_file().begins_with("BronzeArch"):
			_check(str(row["correspondence"]) == "Proxy", "cylinder support behind a box collider is Proxy: %s" % row["body_path"])


func _workbench_inventory() -> void:
	var rows := _rows()
	var state := _state()
	print("MATERIAL_CPU_BINDINGS wb entries=%d bound=%d unsupported=%d descriptors=%d manifest=%s scan_ms=%.3f" % [
		int(state["entries"]), int(state["bound"]), int(state["unsupported_entries"]), int(state["descriptors"]),
		state["manifest_sha256"], float(state["last_scan_ms"])])
	_print_manifest()
	cases += 1
	_check(rows.size() == _collision_shape_count(), "every collision shape is inventoried (%d rows, %d shapes)" % [rows.size(), _collision_shape_count()])
	var gallery_collision := 0
	var hermetic := 0
	for row in rows:
		var path := str(row["body_path"])
		_check(not path.contains("@"), "body path is deterministic: %s" % path)
		if path.contains("GalleryCollision/"):
			gallery_collision += 1
			_check(not bool(row["bound"]) and str(row["reason"]) == "NoPresentationMesh", "%s: collision-only gallery body is NoPresentationMesh (%s)" % [path, row["reason"]])
		if path.contains("HermeticRoomDisplay/"):
			hermetic += 1
			_check(not bool(row["bound"]) and str(row["reason"]) == "EmissionEnabled", "%s: emissive Hermetic wall is EmissionEnabled (%s)" % [path, row["reason"]])
	cases += 1
	_check(gallery_collision == 7 and hermetic == 6, "Workbench gallery collision (7) and Hermetic (6) bodies attributed (%d, %d)" % [gallery_collision, hermetic])


func _expect_checker(body_name: String, correspondence: String, a: Color, b: Color, scale_value: float, shapes: int) -> void:
	var rows := _rows_for(body_name)
	cases += 1
	_check(rows.size() == shapes, "%s has %d shape rows (%d)" % [body_name, shapes, rows.size()])
	for row in rows:
		_check(bool(row["bound"]) and str(row["kind"]) == "WorldSpaceChecker", "%s bound as checker (%s)" % [body_name, row])
		_check(str(row["correspondence"]) == correspondence, "%s correspondence %s (%s)" % [body_name, correspondence, row.get("correspondence")])
		if bool(row["bound"]):
			_check(_rgb_equal(row["color_a"], a) and _rgb_equal(row["color_b"], b) and is_equal_approx(float(row["scale"]), scale_value), "%s checker parameters (%s)" % [body_name, row])


# Independent GDScript re-derivation of each row's outcome from the scene.
func _independent_inventory_check() -> void:
	var by_body := {}
	for row in _rows():
		by_body[str(row["body_path"])] = row
	var checked := 0
	for body in root_scene.find_children("*", "CollisionObject3D", true, false):
		var path := str(root_scene.get_path_to(body))
		if not by_body.has(path):
			_check(body.get_shape_owners().is_empty(), "body without shapes may be absent; %s missing" % path)
			continue
		var row: Dictionary = by_body[path]
		_check(int(row["collider_id"]) == body.get_instance_id(), "%s keyed by its instance id" % path)
		var expected := _derive(body)
		var ok := bool(row["bound"]) == bool(expected["bound"])
		if ok and bool(expected["bound"]):
			ok = str(row["kind"]) == str(expected["kind"])
			if ok and str(expected["kind"]) == "Flat":
				ok = _rgb_equal(row["flat"], expected["flat"])
			elif ok:
				ok = _rgb_equal(row["color_a"], expected["a"]) and _rgb_equal(row["color_b"], expected["b"]) and is_equal_approx(float(row["scale"]), float(expected["scale"]))
		elif ok:
			ok = str(row["reason"]) == str(expected["reason"])
		_check(ok, "%s: adapter %s, independent derivation %s" % [path, row, expected])
		checked += 1
	cases += 1
	print("MATERIAL_CPU_BINDINGS %s independent_derivation bodies=%d" % [kind, checked])


func _derive(body: CollisionObject3D) -> Dictionary:
	var meshes: Array = []
	for child in body.get_children():
		if child is MeshInstance3D:
			meshes.append(child)
	if body.has_meta("xpr_presentation_mesh") and body.get_node_or_null(NodePath(str(body.get_meta("xpr_presentation_mesh")))) is MeshInstance3D:
		meshes = [body.get_node(NodePath(str(body.get_meta("xpr_presentation_mesh"))))]
	if meshes.size() == 0:
		return {"bound": false, "reason": "NoPresentationMesh"}
	if meshes.size() > 1:
		return {"bound": false, "reason": "AmbiguousPresentationMesh"}
	var mi: MeshInstance3D = meshes[0]
	if mi.mesh == null:
		return {"bound": false, "reason": "NoPresentationMesh"}
	if mi.mesh.get_surface_count() != 1:
		return {"bound": false, "reason": "MultiSurfaceMesh"}
	if mi.material_overlay != null:
		return {"bound": false, "reason": "ShaderNotRecognized"}
	var material: Material = mi.material_override
	if material == null:
		material = mi.get_surface_override_material(0)
	if material == null:
		material = mi.mesh.surface_get_material(0)
	if material == null or material.next_pass != null:
		return {"bound": false, "reason": "ShaderNotRecognized"}
	if material is ShaderMaterial:
		var shader: Shader = (material as ShaderMaterial).shader
		if shader == null or shader.resource_path != CHECKER_PATH:
			return {"bound": false, "reason": "ShaderNotRecognized"}
		if shader.code.sha256_text() != "f9f8e05b67fdf8594888b979d8ae3a35f5ca80fdde52d7c3d91b17e24a4c46e8":
			return {"bound": false, "reason": "ShaderSourceHashMismatch"}
		var a = material.get_shader_parameter("color_a")
		var b = material.get_shader_parameter("color_b")
		var s = material.get_shader_parameter("checker_scale")
		if a == null: a = Vector3(0.04, 0.05, 0.08)
		if b == null: b = Vector3(0.9, 0.92, 0.95)
		if s == null: s = 8.0
		if not (a is Vector3 or a is Color) or not (b is Vector3 or b is Color) or not (s is float or s is int):
			return {"bound": false, "reason": "ParameterMissingOrWrongType"}
		if not is_finite(float(s)):
			return {"bound": false, "reason": "NonFiniteParameter"}
		return {"bound": true, "kind": "WorldSpaceChecker", "a": _as_color(a), "b": _as_color(b), "scale": float(s)}
	if material is BaseMaterial3D:
		var m := material as BaseMaterial3D
		if m.emission_enabled:
			return {"bound": false, "reason": "EmissionEnabled"}
		if m.albedo_texture != null or (m.detail_enabled and m.detail_albedo != null):
			return {"bound": false, "reason": "TextureBound"}
		if m.vertex_color_use_as_albedo:
			return {"bound": false, "reason": "VertexColorAlbedo"}
		if m.transparency != BaseMaterial3D.TRANSPARENCY_DISABLED or m.albedo_color.a < 1.0:
			return {"bound": false, "reason": "TransparencyEnabled"}
		return {"bound": true, "kind": "Flat", "flat": m.albedo_color}
	return {"bound": false, "reason": "ShaderNotRecognized"}


# ------------------------------------------------------------------ diagnostics (test-local fixtures, far from view)

func _diagnostic_fixtures() -> void:
	var holder := Node3D.new()
	holder.name = "MaterialCpuDiagnosticFixtures"
	holder.position = Vector3(0, -500, 0)
	root_scene.add_child(holder)
	var generation_before := int(_state()["generation"])
	var flat := StandardMaterial3D.new()
	flat.albedo_color = Color(0.2, 0.4, 0.6)
	_fixture(holder, "NoMesh", [], null)
	_fixture(holder, "TwoMeshes", [_box_mesh(flat), _box_mesh(flat)], null)
	var two_surfaces := ArrayMesh.new()
	for _i in range(2):
		var arrays := []
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX] = PackedVector3Array([Vector3.ZERO, Vector3.RIGHT, Vector3.UP])
		two_surfaces.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var multi := MeshInstance3D.new()
	multi.mesh = two_surfaces
	_fixture(holder, "MultiSurface", [multi], null)
	var other_shader := ShaderMaterial.new()
	other_shader.shader = Shader.new()
	other_shader.shader.code = "shader_type spatial;\nvoid fragment() { ALBEDO = vec3(1.0); }\n"
	_fixture(holder, "OtherShader", [_box_mesh(other_shader)], null)
	var checker := load(CHECKER_PATH)
	var wrong_type := ShaderMaterial.new()
	wrong_type.shader = checker
	wrong_type.set_shader_parameter("color_a", "not a colour")
	_fixture(holder, "WrongParamType", [_box_mesh(wrong_type)], null)
	var nonfinite := ShaderMaterial.new()
	nonfinite.shader = checker
	nonfinite.set_shader_parameter("checker_scale", INF)
	_fixture(holder, "NonFiniteScale", [_box_mesh(nonfinite)], null)
	var defaults := ShaderMaterial.new()
	defaults.shader = checker
	_fixture(holder, "CheckerDefaults", [_box_mesh(defaults)], null)
	var emissive := StandardMaterial3D.new()
	emissive.emission_enabled = true
	_fixture(holder, "Emissive", [_box_mesh(emissive)], null)
	var textured := StandardMaterial3D.new()
	textured.albedo_texture = ImageTexture.create_from_image(Image.create(2, 2, false, Image.FORMAT_RGBA8))
	_fixture(holder, "Textured", [_box_mesh(textured)], null)
	var vertex_colour := StandardMaterial3D.new()
	vertex_colour.vertex_color_use_as_albedo = true
	_fixture(holder, "VertexColour", [_box_mesh(vertex_colour)], null)
	var transparent := StandardMaterial3D.new()
	transparent.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_fixture(holder, "Transparent", [_box_mesh(transparent)], null)
	var concave := ConcavePolygonShape3D.new()
	concave.set_faces(PackedVector3Array([Vector3.ZERO, Vector3.RIGHT, Vector3.UP]))
	_fixture(holder, "Concave", [_box_mesh(flat)], concave)
	var overlay_mesh := _box_mesh(flat)
	overlay_mesh.material_overlay = flat
	_fixture(holder, "Overlay", [overlay_mesh], null)
	var next_pass := StandardMaterial3D.new()
	next_pass.albedo_color = Color(0.3, 0.3, 0.3)
	next_pass.next_pass = flat
	_fixture(holder, "NextPass", [_box_mesh(next_pass)], null)
	var surface_override := _box_mesh(emissive)
	surface_override.set_surface_override_material(0, flat)
	_fixture(holder, "SurfaceOverrideBeatsMesh", [surface_override], null)
	var material_override := _box_mesh(emissive)
	material_override.set_surface_override_material(0, emissive)
	material_override.material_override = flat
	_fixture(holder, "OverrideBeatsSurfaceOverride", [material_override], null)
	var meta_body := _fixture(holder, "MetaMesh", [], null)
	var detached := Node3D.new()
	detached.name = "Visual"
	meta_body.add_child(detached)
	var meta_mesh := _box_mesh(flat)
	meta_mesh.name = "Deep"
	detached.add_child(meta_mesh)
	meta_body.set_meta("xpr_presentation_mesh", NodePath("Visual/Deep"))
	var bad_meta := _fixture(holder, "BadMetaFallsBack", [_box_mesh(flat)], null)
	bad_meta.set_meta("xpr_presentation_mesh", NodePath("Missing"))
	await _await_scans(2, "diagnostic fixtures added")
	var expected := {
		"NoMesh": ["NoPresentationMesh", "no_mesh_child"],
		"TwoMeshes": ["AmbiguousPresentationMesh", "mesh_children=2"],
		"MultiSurface": ["MultiSurfaceMesh", "surfaces=2"],
		"OtherShader": ["ShaderNotRecognized", "shader="],
		"WrongParamType": ["ParameterMissingOrWrongType", "param_type:color_a"],
		"NonFiniteScale": ["NonFiniteParameter", "nonfinite:checker_scale"],
		"Emissive": ["EmissionEnabled", ""],
		"Textured": ["TextureBound", ""],
		"VertexColour": ["VertexColorAlbedo", ""],
		"Transparent": ["TransparencyEnabled", ""],
		"Concave": ["ConcaveFaceIndexAmbiguous", "concave_shape"],
		"Overlay": ["ShaderNotRecognized", "material_overlay"],
		"NextPass": ["ShaderNotRecognized", "next_pass"],
	}
	var state := _state()
	cases += 1
	_check(int(state["generation"]) == generation_before + 1 and str(state["last_rebuild_reason"]) == "state_changed", "adding fixtures rebuilt exactly once (gen %d -> %s, %s)" % [generation_before, state["generation"], state["last_rebuild_reason"]])
	for name in expected.keys():
		var rows := _rows_for("MaterialCpuDiagnosticFixtures/" + name)
		_check(rows.size() == 1, "%s inventoried once" % name)
		if rows.size() == 1:
			var row: Dictionary = rows[0]
			_check(not bool(row["bound"]) and str(row["reason"]) == expected[name][0] and _has(str(row["detail"]), expected[name][1]),
				"%s -> %s/%s (got bound=%s %s/%s)" % [name, expected[name][0], expected[name][1], row["bound"], row["reason"], row["detail"]])
		cases += 1
	for pair in [["CheckerDefaults", "WorldSpaceChecker"], ["SurfaceOverrideBeatsMesh", "Flat"], ["OverrideBeatsSurfaceOverride", "Flat"], ["MetaMesh", "Flat"], ["BadMetaFallsBack", "Flat"]]:
		var rows := _rows_for("MaterialCpuDiagnosticFixtures/" + pair[0])
		_check(rows.size() == 1 and bool(rows[0]["bound"]) and str(rows[0]["kind"]) == pair[1], "%s binds as %s (%s)" % [pair[0], pair[1], rows])
		cases += 1
	var defaults_row: Array = _rows_for("MaterialCpuDiagnosticFixtures/CheckerDefaults")
	if defaults_row.size() == 1:
		_check(_rgb_equal(defaults_row[0]["color_a"], Color(0.04, 0.05, 0.08)) and is_equal_approx(float(defaults_row[0]["scale"]), 8.0), "unset checker parameters use the pinned shader defaults")
	var meta_row: Array = _rows_for("MaterialCpuDiagnosticFixtures/MetaMesh")
	var bad_meta_row: Array = _rows_for("MaterialCpuDiagnosticFixtures/BadMetaFallsBack")
	_check(meta_row.size() == 1 and str(meta_row[0]["mesh"]) == "meta:Visual/Deep" and str(meta_row[0]["correspondence"]) == "Proxy", "valid metadata resolves the mesh (Proxy: not a direct child) (%s)" % [meta_row])
	_check(bad_meta_row.size() == 1 and str(bad_meta_row[0]["mesh"]).begins_with("child:") and str(bad_meta_row[0]["detail"]).contains("metadata_invalid"), "invalid metadata falls back to the child rule and is recorded (%s)" % [bad_meta_row])
	# Checker shader edit: every checker binding becomes ShaderSourceHashMismatch; restoring restores provenance.
	var manifest_before := str(state["manifest_sha256"])
	var provenance_before := str(state["table_provenance_sha256"])
	var shader: Shader = checker
	var original_code := shader.code
	shader.code = original_code + "\n// edited\n"
	await _await_scans(2, "checker shader edited")
	var edited := 0
	for row in _rows():
		if str(row["reason"]) == "ShaderSourceHashMismatch":
			edited += 1
	_check(edited >= 6, "an edited checker shader is reported as ShaderSourceHashMismatch on every checker body (%d)" % edited)
	shader.code = original_code
	await _await_scans(2, "checker shader restored")
	_check(str(_state()["manifest_sha256"]) == manifest_before and str(_state()["table_provenance_sha256"]) == provenance_before, "restoring the shader restores the manifest and table provenance")
	cases += 2
	holder.queue_free()
	await _await_scans(2, "diagnostic fixtures freed")


func _fixture(parent: Node3D, name: String, meshes: Array, shape: Shape3D) -> StaticBody3D:
	var body := StaticBody3D.new()
	body.name = name
	body.position = Vector3(parent.get_child_count() * 3.0, 0, 0)
	for mesh in meshes:
		body.add_child(mesh)
	var collision := CollisionShape3D.new()
	if shape == null:
		var box := BoxShape3D.new()
		box.size = Vector3.ONE
		shape = box
	collision.shape = shape
	body.add_child(collision)
	parent.add_child(body)
	return body


func _box_mesh(material: Material) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = Vector3.ONE
	box.material = material
	mi.mesh = box
	return mi


# ------------------------------------------------------------------ lifecycle

func _invalidation_material_swap() -> void:
	var before := _state()
	var sphere_mesh := root_scene.find_child("ReferenceSphere", true, false).get_node("PresentationMesh") as MeshInstance3D
	var original_override := sphere_mesh.material_override
	var red := StandardMaterial3D.new()
	red.albedo_color = Color(0.9, 0.1, 0.1)
	sphere_mesh.material_override = red
	await _await_scans(2, "material_override swapped")
	var swapped := _state()
	var rows := _rows_for("ReferenceSphere")
	cases += 1
	_check(int(swapped["generation"]) == int(before["generation"]) + 1 and str(swapped["last_rebuild_reason"]) == "state_changed", "material_override swap rebuilds once (%s -> %s)" % [before["generation"], swapped["generation"]])
	_check(rows.size() == 1 and str(rows[0]["kind"]) == "Flat" and str(rows[0]["source"]) == "material_override", "swapped sphere binds the override (%s)" % [rows])
	await _await_scans(3, "steady state")
	_check(int(_state()["generation"]) == int(swapped["generation"]), "steady state does not rebuild")
	sphere_mesh.material_override = original_override
	await _await_scans(2, "material_override restored")
	var restored := _state()
	_check(int(restored["generation"]) == int(swapped["generation"]) + 1, "restoring the override rebuilds once")
	_check(str(restored["manifest_sha256"]) == str(before["manifest_sha256"]) and str(restored["table_provenance_sha256"]) == str(before["table_provenance_sha256"]), "restored binding state restores manifest and provenance")
	cases += 1


func _invalidation_live_reset_and_mode_leave() -> void:
	var before := _state()
	film.call("ResetFilmPassManual")
	await _await_scans(2, "LIVE reset")
	var reset := _state()
	cases += 1
	_check(int(reset["generation"]) == int(before["generation"]) + 1 and str(reset["last_rebuild_reason"]) == "live_reset", "LIVE reset rebuilds (%s)" % reset["last_rebuild_reason"])
	_check(str(reset["manifest_sha256"]) == str(before["manifest_sha256"]), "LIVE reset keeps an unchanged manifest")
	var revalidations := int(reset["revalidations"])
	await _await_scans(3, "LIVE steady revalidation")
	_check(int(_state()["revalidations"]) > revalidations and int(_state()["generation"]) == int(reset["generation"]), "revalidation continues each step without rebuilding")
	ctrl.call("cycle_shading_mode")
	await _frames(3)
	var left := _state()
	cases += 1
	_check(not bool(left["snapshot_held"]) and str(left["last_release_reason"]) == "mode_left", "leaving MaterialCpu releases the snapshot (%s)" % [left])
	var revalidations_after_leave := int(left["revalidations"])
	await _frames_and_physics_ticks(10, 4)
	_check(int(_state()["revalidations"]) == revalidations_after_leave, "no scanning outside MaterialCpu")
	await _select_material_cpu_live()
	_check(bool(_state()["snapshot_held"]) and str(_state()["last_rebuild_reason"]) == "enter", "re-entering MaterialCpu rebuilds")
	# OFF stops RenderStep; the snapshot is retained but never sampled until LIVE resumes.
	ctrl.call("set_mode", 0)
	var stats_before: Dictionary = _state()["pass_stats"]
	await _frames_and_physics_ticks(10, 4)
	_check(_state()["pass_stats"] == stats_before, "film OFF performs no sampling")
	cases += 1
	await _select_material_cpu_live()


func _manifest_stability_across_reload() -> void:
	var first := _state()
	await _open("gal")
	await _select_material_cpu_live()
	var second := _state()
	cases += 1
	_check(str(first["manifest_sha256"]) == str(second["manifest_sha256"]), "path-keyed manifest is identical after a scene reload (%s vs %s)" % [first["manifest_sha256"], second["manifest_sha256"]])
	_check(str(first["table_provenance_sha256"]) != str(second["table_provenance_sha256"]), "table provenance changes with runtime instance ids (expected)")
	print("MATERIAL_CPU_BINDINGS gal reload manifest=%s provenance %s -> %s" % [second["manifest_sha256"], first["table_provenance_sha256"], second["table_provenance_sha256"]])


func _dial_switch_is_not_a_binding_change() -> void:
	var dial := chamber.get_node("FieldDialController")
	var before := _state()
	# Every frame across the deferred layer toggles: the binding manifest is unchanged and any rebuild is a
	# LIVE acquisition reset (never a state change), so no frame can carry a false binding.
	var reasons := {}
	for preset in ["Hermetic", "Gallery"]:
		dial.call("SetDisplayPresetForTesting", preset)
		for _i in range(8):
			await process_frame
			var now := _state()
			_check(str(now["manifest_sha256"]) == str(before["manifest_sha256"]), "%s frame %d: manifest unchanged" % [preset, _i])
			if int(now["generation"]) != int(before["generation"]):
				reasons[str(now["last_rebuild_reason"])] = true
	cases += 1
	_check(not reasons.has("state_changed"), "dial switches never rebuild for a binding change (reasons %s)" % [reasons.keys()])
	print("MATERIAL_CPU_BINDINGS wb dial generation %s -> %s reasons=%s" % [before["generation"], _state()["generation"], reasons.keys()])


func _scene_transitions() -> void:
	var exits := []
	for scene_kind in ["gal", "wb", "gal"]:
		var old_film := film
		old_film.tree_exited.connect(func() -> void: exits.append(old_film.call("GetMaterialCpuStateForTesting")), CONNECT_ONE_SHOT)
		await _open(scene_kind)
		cases += 1
		_check(not bool(_state()["snapshot_held"]) and int(_state()["revalidations"]) == 0, "%s starts without a material snapshot" % scene_kind)
		_check(str(ctrl.call("GetShadingModeName")) != MATERIAL_CPU, "%s starts with its authored N, not MaterialCpu" % scene_kind)
		await _select_material_cpu_live()
		_check(bool(_state()["snapshot_held"]), "%s builds on entering MaterialCpu" % scene_kind)
	for exit_state in exits:
		_check(not bool(exit_state["snapshot_held"]) and str(exit_state["last_release_reason"]) == "exit_tree", "leaving a scene released its snapshot (%s)" % [exit_state])
	_check(exits.size() == 3, "three scene exits observed")


# ------------------------------------------------------------------ helpers

func _select_material_cpu_live() -> void:
	for _i in range(5):
		if str(ctrl.call("GetShadingModeName")) == MATERIAL_CPU:
			break
		ctrl.call("cycle_shading_mode")
	_check(str(ctrl.call("GetShadingModeName")) == MATERIAL_CPU, "N reaches Material (CPU)")
	if str(ctrl.call("GetModeName")) != "LIVE":
		ctrl.call("set_mode", 2)
	await _await_scans(2, "enter LIVE MaterialCpu")
	_check(bool(_state()["snapshot_held"]), "LIVE MaterialCpu holds a snapshot")


func _collision_shape_count() -> int:
	var count := 0
	for body in root_scene.find_children("*", "CollisionObject3D", true, false):
		for owner_id in body.get_shape_owners():
			count += body.shape_owner_get_shape_count(owner_id)
	return count


func _rows() -> Array:
	return film.call("GetMaterialCpuBindingsForTesting")


func _rows_for(suffix: String) -> Array:
	var found := []
	for row in _rows():
		if str(row["body_path"]).ends_with(suffix):
			found.append(row)
	return found


func _state() -> Dictionary:
	return film.call("GetMaterialCpuStateForTesting")


func _print_manifest() -> void:
	for line in str(film.call("GetMaterialCpuManifestForTesting")).split("\n"):
		if not line.is_empty():
			print("MATERIAL_CPU_MANIFEST %s %s" % [kind, line])


func _has(text: String, part: String) -> bool:
	return part.is_empty() or text.contains(part)


func _as_color(value) -> Color:
	if value is Color:
		return value
	return Color(value.x, value.y, value.z)


func _rgb_equal(value, expected: Color) -> bool:
	var c := _as_color(value)
	return is_equal_approx(c.r, expected.r) and is_equal_approx(c.g, expected.g) and is_equal_approx(c.b, expected.b)


func _open(scene_kind: String) -> void:
	kind = scene_kind
	change_scene_to_file(WB if scene_kind == "wb" else GAL)
	await scene_changed
	await _frames(30)
	root_scene = current_scene
	film = root_scene.find_children("GrinFilmCamera", "", true, false)[0]
	chamber = film.get_parent()
	ctrl = chamber.get_node("FilmController")


func _frames(count: int) -> void:
	for _i in range(count):
		await process_frame


# Synchronization contract: the film revalidates (and, on a change, rebuilds) the MaterialCpu binding snapshot at the
# start of each LIVE RenderStep. With separate-thread physics, _Process only queues that step and the physics pump
# executes it, so a fixed number of process frames can contain zero revalidations. Wait instead for `count`
# revalidations that started after the caller's scene change (all on the main thread, so each one observes it).
# Bounded: a timeout is recorded as a fixture failure with the film state, never skipped.
func _await_scans(count: int, label: String) -> void:
	var start := int(_state()["revalidations"])
	var ticks_start := Engine.get_physics_frames()
	var started_ms := Time.get_ticks_msec()
	var waited := 0
	while int(_state()["revalidations"]) < start + count:
		if waited >= SCAN_WAIT_FRAME_LIMIT or Time.get_ticks_msec() - started_ms > SCAN_WAIT_MS_LIMIT:
			var state := _state()
			_check(false, "%s: timed out waiting for %d MaterialCpu revalidation(s) (revalidations %d -> %d, frames %d, physics ticks %d, %d ms, generation %s, snapshot_held %s, rebuild_requested %s, film %s, shading %s)" % [
				label, count, start, int(state["revalidations"]), waited, Engine.get_physics_frames() - ticks_start,
				Time.get_ticks_msec() - started_ms, state["generation"], state["snapshot_held"], state["rebuild_requested"],
				ctrl.call("GetModeName"), ctrl.call("GetShadingModeName")])
			return
		await process_frame
		waited += 1


# For assertions that something does NOT happen: guarantee the physics pump had the opportunity to run a step.
func _frames_and_physics_ticks(frame_count: int, tick_count: int) -> void:
	var ticks_start := Engine.get_physics_frames()
	await _frames(frame_count)
	var waited := 0
	while Engine.get_physics_frames() - ticks_start < tick_count and waited < SCAN_WAIT_FRAME_LIMIT:
		await process_frame
		waited += 1
	_check(Engine.get_physics_frames() - ticks_start >= tick_count, "physics pump advanced %d of %d ticks while waiting" % [Engine.get_physics_frames() - ticks_start, tick_count])


func _check(condition: bool, message: String) -> void:
	if not condition:
		failures.append("%s: %s" % [kind, message])
