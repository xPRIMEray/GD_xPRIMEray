extends SceneTree

# TEXTURE-R0A.2 NATIVE-ONLY raster calibration (supervised execution; opens a window). CQ0.2-N1R2 architecture repair.
# Straight rays only (field sources disabled, shared field strength 0). The MaterialCpu film is captured with the
# authored materials (every film pixel with a hit is a candidate); then test-local UNSHADED twins with identical
# fragment maths are swapped in and the independent GPU raster is read back. Each candidate's film hit is projected
# into the raster with camera.unproject_position(hit); the raster pixel shows the surface point p' hit by the straight
# ray through that pixel's centre.
# Physics: physics/3d/run_on_separate_thread=true, so the direct space state is only accessible while the physics
# server is synchronised with the main thread. All queries run synchronously (no await) immediately after
# `await physics_frame`; an unavailable space state is an explicit FAIL, never a sample classification.
# Acceptance per sample (geometric comparability): Exact correspondence; the centre ray hits the film hit's collider at
# p' within 1.5 raster-pixel footprints of the film hit (slant-corrected); all eight neighbour-pixel rays hit the same
# collider (interior, no silhouette); for WorldSpaceChecker, the film hit and p' lie in the same checker cell and both
# are >= 0.02 cells from every cell plane, so both renderers evaluate the same cell. The colour comparison is film
# (candidate MaterialCpu output) versus raster (independent GPU evaluation); neither is derived from the other.
# Pass: >= 200 accepted Exact samples, every channel within 1/255, zero failed comparisons. Every exclusion is counted.
# Usage (operator-attended): godot --path <ws> --script res://scripts/test_material_cpu_raster_calibration.gd -- --resolution=1152x648
# Geometry-only (headless, eligibility evidence only, never PASS):
#   godot --headless --path <ws> --script res://scripts/test_material_cpu_raster_calibration.gd -- --geometry-only

const GAL := "res://Experiences/SuspendedPortalGallery.tscn"
const MATERIAL_CPU := "Material (CPU)"
const TOLERANCE := 1.0 / 255.0 + 1e-6
const MIN_ACCEPTED := 200
const CELL_MARGIN := 0.02
const PROJECTION_PIXELS := 1.5
const UNSHADED_CHECKER := "shader_type spatial;\nrender_mode unshaded;\nuniform vec3 color_a : source_color;\nuniform vec3 color_b : source_color;\nuniform float checker_scale = 8.0;\nvarying vec3 world_position;\nvoid vertex() { world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }\nvoid fragment() {\n\tvec3 cell = floor(world_position * checker_scale);\n\tfloat checker = mod(cell.x + cell.y + cell.z, 2.0);\n\tALBEDO = mix(color_a, color_b, checker);\n}\n"

var failures: Array[String] = []
var film: Node
var ctrl: Node
var rays_cast := 0
var rays_hit := 0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var geometry_only := OS.get_cmdline_user_args().has("--geometry-only")
	if DisplayServer.get_name() == "headless" and not geometry_only:
		print("MATERIAL_CPU_RASTER SKIP native-only test refused under the headless display server")
		quit(2)
		return
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--resolution="):
			var parts := arg.trim_prefix("--resolution=").split("x")
			DisplayServer.window_set_size(Vector2i(int(parts[0]), int(parts[1])))
			if geometry_only:
				root.size = Vector2i(int(parts[0]), int(parts[1]))  # headless has no window; emulate the native viewport
	change_scene_to_file(GAL)
	await scene_changed
	await _frames(30)
	var root_scene := current_scene
	film = root_scene.find_children("GrinFilmCamera", "", true, false)[0]
	ctrl = film.get_parent().get_node("FilmController")
	for source in get_nodes_in_group("field_sources"):
		source.set("Enabled", false)
	film.set("SharedRbrFieldStrength", 0.0)
	_check(is_zero_approx(float(film.get("SharedRbrFieldStrength"))), "straight rays: shared field strength is zero")
	# Deterministic calibration pose (test-local; existing public player pose API). The authored start pose sees only
	# the DiagnosticChamber floor, whose top face is coplanar with a checker cell plane (MaterialSamplingConvention
	# documents that its inward-biased parity differs from the shader there), plus a Flat ceiling, and the LIVE film
	# reaches only nearby geometry. This pose faces the Exact WorldSpaceChecker BackWall (face z=-9.85, scale 0.55:
	# 0.418 cells from the nearest cell plane) square-on at 3.0 m.
	var calibration_camera := Transform3D(Basis.IDENTITY, Vector3(0.0, 1.65, -6.85))
	var player := root_scene.find_child("TransportChamberPlayer", true, false)
	if player == null or not player.has_method("ApplyCameraTransform") or not player.has_method("SetPoseHeld"):
		_abort("calibration pose: TransportChamberPlayer pose API unavailable")
		return
	player.call("SetPoseHeld", true)
	player.call("ApplyCameraTransform", calibration_camera)
	await _frames(10)
	var posed := root.get_viewport().get_camera_3d()
	if posed == null or posed.global_position.distance_to(calibration_camera.origin) > 0.001 or posed.global_transform.basis.z.dot(Vector3.BACK) < 0.99999:
		_abort("calibration pose not applied (camera=%s)" % [posed.global_transform if posed != null else "null"])
		return
	for _i in range(5):
		if str(ctrl.call("GetShadingModeName")) == MATERIAL_CPU:
			break
		ctrl.call("cycle_shading_mode")
	_check(str(ctrl.call("GetShadingModeName")) == MATERIAL_CPU, "shading mode is Material (CPU)")
	ctrl.call("set_mode", 2)
	await _frames(10)
	await _full_passes(2)
	# The raster camera is the film's own viewport camera (GrinFilmCamera resolves GetViewport().GetCamera3D()).
	var camera := root.get_viewport().get_camera_3d()
	if camera == null or film.get_viewport() != root.get_viewport() or film.get_viewport().get_camera_3d() != camera:
		_abort("raster camera is not the film's viewport camera")
		return
	var capture_pose := camera.global_transform
	var capture_fov := camera.fov
	var bindings := {}
	for row in film.call("GetMaterialCpuBindingsForTesting"):
		bindings["%d:%d" % [int(row["collider_id"]), int(row["shape"])]] = row
	var image := film.call("CopyFilmImageForTesting") as Image
	var samples := []
	for y in range(image.get_height()):
		for x in range(image.get_width()):
			var rec: Dictionary = film.call("GetFilmHitRecordForTesting", x, y)
			if bool(rec.get("valid", false)) and bool(rec["had_hit"]):
				samples.append({"rec": rec, "film": image.get_pixel(x, y)})
	ctrl.call("set_mode", 0)
	var raster: Image = null
	var raster_size := Vector2i(root.get_visible_rect().size)
	if not geometry_only:
		_prepare_raster(root_scene)
		await _frames(20)
		await RenderingServer.frame_post_draw
		raster = root.get_viewport().get_texture().get_image()
		raster_size = raster.get_size()
	if not camera.global_transform.is_equal_approx(capture_pose) or not is_equal_approx(camera.fov, capture_fov):
		_abort("camera pose or fov changed between film capture and raster readback")
		return
	# ---- Synchronised physics window: everything below up to the verdict is synchronous (no await). ----
	await physics_frame
	var world := camera.get_world_3d()
	var space: PhysicsDirectSpaceState3D = world.direct_space_state if world != null else null
	if space == null:
		_abort("physics_state_unavailable: direct space state is null inside the physics_frame window")
		return
	var counts := {"candidates": samples.size(), "accepted": 0, "accepted_checker": 0, "accepted_flat": 0, "proxy_excluded": 0,
		"unbound_or_unsupported": 0, "offscreen": 0, "centre_miss": 0, "collider_mismatch": 0, "projection_mismatch": 0,
		"silhouette": 0, "near_boundary": 0, "checker_parity_even": 0, "checker_parity_odd": 0, "failed": 0}
	var first_failure := ""
	var max_error := 0.0
	var sum_error := 0.0
	var tan_half := tan(deg_to_rad(camera.fov) * 0.5)
	for sample in samples:
		var rec: Dictionary = sample["rec"]
		var key := "%d:%d" % [int(rec["collider_id"]), int(rec["shape"])]
		if not bindings.has(key) or not bool(bindings[key]["bound"]):
			counts["unbound_or_unsupported"] += 1
			continue
		var row: Dictionary = bindings[key]
		if str(row["correspondence"]) != "Exact":
			counts["proxy_excluded"] += 1
			continue
		var hp: Vector3 = rec["position"]
		if camera.is_position_behind(hp):
			counts["offscreen"] += 1
			continue
		var screen := camera.unproject_position(hp)
		var px := Vector2i(int(floor(screen.x)), int(floor(screen.y)))
		if px.x < 1 or px.y < 1 or px.x >= raster_size.x - 1 or px.y >= raster_size.y - 1:
			counts["offscreen"] += 1
			continue
		var centre_screen := Vector2(px) + Vector2(0.5, 0.5)
		var centre := _ray(space, camera, centre_screen)
		if centre.is_empty():
			counts["centre_miss"] += 1
			continue
		if int(centre["collider_id"]) != int(rec["collider_id"]):
			counts["collider_mismatch"] += 1
			continue
		var p2: Vector3 = centre["position"]
		var distance := camera.global_position.distance_to(hp)
		var pixel_world := distance * 2.0 * tan_half / float(raster_size.y)
		var view_dir := camera.project_ray_normal(centre_screen)
		var slant := maxf(absf((centre["normal"] as Vector3).dot(view_dir)), 0.1)
		if p2.distance_to(hp) > PROJECTION_PIXELS * pixel_world / slant:
			counts["projection_mismatch"] += 1
			continue
		var clean := true
		for dy in [-1, 0, 1]:
			for dx in [-1, 0, 1]:
				if dx == 0 and dy == 0:
					continue
				var hit := _ray(space, camera, centre_screen + Vector2(dx, dy))
				if hit.is_empty() or int(hit["collider_id"]) != int(rec["collider_id"]):
					clean = false
		if not clean:
			counts["silhouette"] += 1
			continue
		var checker := str(row["kind"]) == "WorldSpaceChecker"
		if checker:
			var s := float(row["scale"])
			var q1 := hp * s
			var q2 := p2 * s
			if q1.floor() != q2.floor() or _boundary_distance(q1) < CELL_MARGIN or _boundary_distance(q2) < CELL_MARGIN:
				counts["near_boundary"] += 1
				continue
		counts["accepted"] += 1
		counts["accepted_checker" if checker else "accepted_flat"] += 1
		if checker:
			# Independent expected parity from the physics point p' (shader convention); never from the film.
			var cell := (p2 * float(row["scale"])).floor()
			counts["checker_parity_odd" if int(posmod(int(cell.x + cell.y + cell.z), 2)) == 1 else "checker_parity_even"] += 1
		if geometry_only:
			continue
		var raster_colour := raster.get_pixel(px.x, px.y)
		var film_colour: Color = sample["film"]
		var error := maxf(absf(raster_colour.r - film_colour.r), maxf(absf(raster_colour.g - film_colour.g), absf(raster_colour.b - film_colour.b)))
		max_error = maxf(max_error, error)
		sum_error += error
		if error > TOLERANCE:
			counts["failed"] += 1
			if first_failure.is_empty():
				first_failure = "%s raster=%s film=%s at %s" % [row["body_path"], raster_colour, film_colour, px]
	# ---- End of synchronised window. ----
	var expected_rays := 0
	for k in ["centre_miss", "collider_mismatch"]:
		expected_rays += int(counts[k])
	expected_rays += int(counts["projection_mismatch"]) + 9 * (int(counts["silhouette"]) + int(counts["near_boundary"]) + int(counts["accepted"]))
	var physics := "physics_context=physics_frame rays_cast=%d rays_hit=%d expected_rays=%d" % [rays_cast, rays_hit, expected_rays]
	if rays_cast != expected_rays:
		_abort("ray accounting mismatch (%s)" % physics)
		return
	if rays_cast > 0 and rays_hit == 0:
		_abort("physics returned no hits for any ray (%s)" % physics)
		return
	var accepted := int(counts["accepted"])
	if geometry_only:
		print("MATERIAL_CPU_RASTER GEOMETRY_ONLY counts=%s %s film=%dx%d raster=%dx%d server=%s (eligibility evidence only; no colour comparison)" % [
			JSON.stringify(counts), physics, image.get_width(), image.get_height(), raster_size.x, raster_size.y, DisplayServer.get_name()])
		quit(0 if failures.is_empty() else 1)
		return
	var mean_error := sum_error / float(accepted) if accepted > 0 else 0.0
	print("MATERIAL_CPU_RASTER counts=%s first_failure=%s" % [JSON.stringify(counts), first_failure])
	print("MATERIAL_CPU_RASTER errors max=%.6f mean=%.6f tolerance=%.6f %s film=%dx%d raster=%dx%d" % [
		max_error, mean_error, TOLERANCE, physics, image.get_width(), image.get_height(), raster_size.x, raster_size.y])
	_check(accepted >= MIN_ACCEPTED, "at least %d accepted samples (%d)" % [MIN_ACCEPTED, accepted])
	_check(int(counts["failed"]) == 0, "every accepted sample within 1/255 per channel (%s)" % first_failure)
	if failures.is_empty():
		print("MATERIAL_CPU_RASTER PASS accepted=%d server=%s" % [accepted, DisplayServer.get_name()])
		quit(0)
	else:
		for failure in failures:
			push_error("MATERIAL_CPU_RASTER FAIL: " + failure)
		print("MATERIAL_CPU_RASTER FAIL count=%d" % failures.size())
		quit(1)


func _abort(reason: String) -> void:
	push_error("MATERIAL_CPU_RASTER FAIL: " + reason)
	print("MATERIAL_CPU_RASTER FAIL count=1 reason=%s" % reason)
	quit(1)


func _prepare_raster(root_scene: Node) -> void:
	# Hide HUD, film, labels and presentation-only meshes; neutral linear environment; unshaded twins.
	for layer in root_scene.find_children("*", "CanvasLayer", true, false):
		layer.visible = false
	for label in root_scene.find_children("*", "Label3D", true, false):
		label.visible = false
	for node in get_nodes_in_group("presentation_only"):
		if node is Node3D:
			node.visible = false
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color.BLACK
	environment.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	environment.tonemap_exposure = 1.0
	environment.tonemap_white = 1.0
	environment.glow_enabled = false
	environment.fog_enabled = false
	environment.ssao_enabled = false
	environment.ssr_enabled = false
	environment.adjustment_enabled = false
	root.get_viewport().get_camera_3d().environment = environment
	root.get_viewport().msaa_3d = Viewport.MSAA_DISABLED
	root.get_viewport().screen_space_aa = Viewport.SCREEN_SPACE_AA_DISABLED
	root.get_viewport().use_taa = false
	var unshaded := Shader.new()
	unshaded.code = UNSHADED_CHECKER
	for mesh_instance in root_scene.find_children("*", "MeshInstance3D", true, false):
		var mi := mesh_instance as MeshInstance3D
		if mi.mesh == null or mi.mesh.get_surface_count() != 1:
			continue
		var material: Material = mi.material_override
		if material == null:
			material = mi.get_surface_override_material(0)
		if material == null:
			material = mi.mesh.surface_get_material(0)
		if material is ShaderMaterial and (material as ShaderMaterial).shader != null and (material as ShaderMaterial).shader.resource_path == "res://Experiences/checkerboard_3d.gdshader":
			var twin := ShaderMaterial.new()
			twin.shader = unshaded
			for parameter in ["color_a", "color_b", "checker_scale"]:
				var value = material.get_shader_parameter(parameter)
				if value != null:
					twin.set_shader_parameter(parameter, value)
			mi.material_override = twin
		elif material is BaseMaterial3D and not (material as BaseMaterial3D).emission_enabled:
			var flat := StandardMaterial3D.new()
			flat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
			flat.albedo_color = (material as BaseMaterial3D).albedo_color
			mi.material_override = flat


# Synchronous; only valid inside the physics_frame window. Every call is counted so the caller can prove execution.
func _ray(space: PhysicsDirectSpaceState3D, camera: Camera3D, screen: Vector2) -> Dictionary:
	var origin := camera.project_ray_origin(screen)
	var query := PhysicsRayQueryParameters3D.create(origin, origin + camera.project_ray_normal(screen) * 500.0)
	query.collide_with_areas = true
	rays_cast += 1
	var hit := space.intersect_ray(query)
	if not hit.is_empty():
		rays_hit += 1
	return hit


func _boundary_distance(q: Vector3) -> float:
	var d := INF
	for c in [q.x, q.y, q.z]:
		var f: float = c - floor(c)
		d = min(d, min(f, 1.0 - f))
	return d


func _full_passes(count: int) -> void:
	film.call("ResetFilmPassManual")
	await process_frame
	var completed := 0
	var last := int(film.get("FilmRowCursor"))
	var progressed := false
	for _i in range(20000):
		await process_frame
		var cursor := int(film.get("FilmRowCursor"))
		if cursor > last:
			progressed = true
		elif cursor < last and progressed:
			completed += 1
			progressed = false
			if completed >= count:
				return
		last = cursor
	_check(false, "LIVE completed %d passes" % count)


func _frames(count: int) -> void:
	for _i in range(count):
		await process_frame


func _check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
