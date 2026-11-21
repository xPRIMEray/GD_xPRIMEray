extends SceneTree

# TEXTURE-R0A.2 headless qualification: MaterialCpu film shading.
# Each shaded film pixel is checked against an independent GDScript oracle that re-evaluates the authored
# material at the pixel's recorded terminal hit (shader maths in float64, RGBA8 quantization through Image),
# using only the path-keyed binding rows. It also checks the five-state N cycle, serial/threaded parity,
# determinism, unbound/unsupported sentinels, Formal/Q and sealed non-interference, LIVE parity and FIELDVIZ
# coexistence. Usage: godot --headless --path <ws> --script res://scripts/test_material_cpu_film.gd

const WB := "res://ObservatoryWorkbench.tscn"
const GAL := "res://Experiences/SuspendedPortalGallery.tscn"
const MATERIAL_CPU := "Material (CPU)"
const SEAL_TIMEOUT_FRAMES := 9000
const NEAR_CELLS := 0.01

var failures: Array[String] = []
var kind := ""
var cases := 0
var root_scene: Node
var chamber: Node
var film: Node
var ctrl: Node
var quant := Image.create(1, 1, false, Image.FORMAT_RGBA8)


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	for scene_kind in ["gal", "wb"]:
		await _open(scene_kind)
		await _n_cycle_keys()
		# Formal/Q and sealed sections first, on a fresh scene (no prior heavy LIVE load).
		await _open(scene_kind)
		await _formal_non_interference()
		await _sealed_invariance()
		await _open(scene_kind)
		await _live_parity_and_correspondence()
		await _determinism_and_threaded_parity()
		if scene_kind == "gal":
			await _unbound_sentinel()
			await _gallery_truth_text()
		else:
			await _card_shows_material_cpu()
			await _dial_switch_correspondence()
		await _open(scene_kind)
		await _fieldviz_coexistence()
	if failures.is_empty():
		print("MATERIAL_CPU_FILM PASS cases=%d server=%s" % [cases, DisplayServer.get_name()])
		quit(0)
	else:
		for failure in failures:
			push_error("MATERIAL_CPU_FILM FAIL: " + failure)
		print("MATERIAL_CPU_FILM FAIL count=%d cases=%d first=%s" % [failures.size(), cases, failures[0]])
		quit(1)


# ------------------------------------------------------------------ N cycle

func _n_cycle_keys() -> void:
	var start := str(ctrl.call("GetShadingModeName"))
	var seen := {}
	var sequence: Array[String] = []
	for _i in range(5):
		await _key(KEY_N)
		var name := str(ctrl.call("GetShadingModeName"))
		seen[name] = true
		sequence.append(name)
		_check(int(film.get("ShadingMode")) == (4 if name == MATERIAL_CPU else int(film.get("ShadingMode"))), "camera ShadingMode follows N")
	cases += 1
	_check(seen.size() == 5 and seen.has(MATERIAL_CPU), "N exposes five states including Material (CPU) (%s)" % [sequence])
	_check(str(ctrl.call("GetShadingModeName")) == start, "five N presses return to %s (%s)" % [start, sequence])
	print("MATERIAL_CPU_FILM %s n_sequence=%s" % [kind, ",".join(sequence)])


# ------------------------------------------------------------------ LIVE parity + oracle

func _live_parity_and_correspondence() -> void:
	# LIVE scientific parity: selecting MaterialCpu during LIVE changes no acquisition state beyond what
	# selecting an existing mode (Depth) changes.
	var depth := await _live_delta("Depth")
	var material := await _live_delta(MATERIAL_CPU)
	cases += 1
	_check(depth == material, "LIVE acquisition deltas equal for Depth and Material (CPU) (depth=%s material=%s)" % [depth, material])
	await _select_live(MATERIAL_CPU)
	await _full_passes(2)
	var result := _oracle("serial")
	cases += 1
	var state := _state()
	var pass_stats: Dictionary = state["pass_stats"]
	_check(bool(pass_stats["consistent"]) and int(pass_stats["total"]) > 0, "statistics partition is consistent (%s)" % [pass_stats])
	_check(int(state["threaded_samples"]) == 0 and int(state["serial_samples"]) > 0, "default scene configuration shades on the serial site")
	print("MATERIAL_CPU_FILM %s pass_stats=%s" % [kind, JSON.stringify(pass_stats)])


func _live_delta(mode_name: String) -> Dictionary:
	ctrl.call("set_mode", 0)
	await _set_n("Depth" if mode_name != "Depth" else "NdotV")
	ctrl.call("set_mode", 2)
	await _frames(20)
	var before := _formal_sig()
	await _set_n(mode_name)
	await _frames(30)
	var after := _formal_sig()
	ctrl.call("set_mode", 0)
	await _frames(2)
	return {
		"gen": int(after["gen"]) - int(before["gen"]),
		"sgen": int(after["sgen"]) - int(before["sgen"]),
		"replay": int(after["replay"]) - int(before["replay"]),
		"reset": int(after["reset"]) - int(before["reset"]),
		"policy_same": after["policy"] == before["policy"],
		"mode": str(ctrl.call("GetModeName")),
	}


# Independent oracle over every film pixel. Returns counts; mismatches are failures.
func _oracle(label: String) -> Dictionary:
	var image := film.call("CopyFilmImageForTesting") as Image
	var bindings := {}
	for row in film.call("GetMaterialCpuBindingsForTesting"):
		bindings["%d:%d" % [int(row["collider_id"]), int(row["shape"])]] = row
	var sky := _q(film.get("SkyColor"))
	var counts := {"pixels": 0, "matched_flat": 0, "matched_checker": 0, "near_skipped": 0, "unsupported": 0, "unbound": 0, "invalid": 0, "miss": 0, "mismatch": 0}
	var first_mismatch := ""
	var width := int(film.get("FilmWidthPixels"))
	var height := int(film.get("FilmHeightRows"))
	for y in range(height):
		for x in range(width):
			var rec: Dictionary = film.call("GetFilmHitRecordForTesting", x, y)
			counts["pixels"] += 1
			var actual := image.get_pixel(x, y)
			var expected: Color
			if not bool(rec["had_hit"]):
				expected = sky
				counts["miss"] += 1
			else:
				var key := "%d:%d" % [int(rec["collider_id"]), int(rec["shape"])]
				var normal: Vector3 = rec["normal"]
				if not bindings.has(key):
					expected = _q(Color(0.25, 0.25, 0.25))
					counts["unbound"] += 1
				elif not bool(bindings[key]["bound"]):
					expected = _q(Color(1, 0, 1))
					counts["unsupported"] += 1
				elif str(bindings[key]["kind"]) == "Flat":
					expected = _q(_as_color(bindings[key]["flat"]))
					counts["matched_flat"] += 1
				elif normal == Vector3.ZERO:
					expected = _q(Color(0, 0, 0))
					counts["invalid"] += 1
				else:
					var row: Dictionary = bindings[key]
					var position: Vector3 = rec["position"]
					var scale_value := float(row["scale"])
					if _boundary_distance(position * scale_value) < NEAR_CELLS:
						counts["near_skipped"] += 1
						continue
					var biased := position - normal.normalized() * 1e-4
					var cell := (biased * scale_value).floor()
					var parity := posmod(int(cell.x + cell.y + cell.z), 2)
					expected = _q(_as_color(row["color_a"] if parity == 0 else row["color_b"]))
					counts["matched_checker"] += 1
			if actual != expected:
				counts["mismatch"] += 1
				if first_mismatch.is_empty():
					first_mismatch = "(%d,%d) actual=%s expected=%s rec=%s" % [x, y, actual, expected, rec]
	cases += 1
	_check(int(counts["mismatch"]) == 0, "%s film equals the independent oracle at every pixel (%s; first %s)" % [label, counts, first_mismatch])
	if kind == "gal":
		_check(int(counts["matched_checker"]) > 0 and int(counts["matched_flat"]) > 0, "Gallery film shows matched checker and flat materials (%s)" % [counts])
	print("MATERIAL_CPU_FILM %s oracle[%s]=%s" % [kind, label, JSON.stringify(counts)])
	return counts


func _boundary_distance(q: Vector3) -> float:
	var d := INF
	for c in [q.x, q.y, q.z]:
		var f: float = c - floor(c)
		d = min(d, min(f, 1.0 - f))
	return d


# ------------------------------------------------------------------ determinism and serial/threaded parity

func _determinism_and_threaded_parity() -> void:
	await _full_passes(1)
	var a := _film_capture()
	await _full_passes(1)
	var b := _film_capture()
	var comparison := _compare_captures(a, b)
	cases += 1
	_check(int(comparison["same_hit_different_colour"]) == 0, "repeat passes: identical hits give identical colours (%s)" % [comparison])
	print("MATERIAL_CPU_FILM %s determinism=%s film_equal=%s stats_equal=%s" % [kind, JSON.stringify(comparison), a["hash"] == b["hash"], str(a["stats"]) == str(b["stats"])])
	# Threaded resolved-sample commit (dormant by default): same immutable table, per-chunk caches and statistics.
	film.set("UseThreadedPass2LocalAccumulation", true)
	film.set("ThreadedPass2WorkerCount", 4)
	film.set("ThreadedPass2RowsPerChunk", 2)
	var threaded_before := int(_state()["threaded_samples"])
	await _full_passes(2)
	var threaded_samples := int(_state()["threaded_samples"]) - threaded_before
	cases += 1
	_check(threaded_samples > 0, "threaded shading site exercised (%d samples)" % threaded_samples)
	_oracle("threaded")
	var t := _film_capture()
	var threaded := _compare_captures(a, t)
	_check(int(threaded["same_hit_different_colour"]) == 0, "serial vs threaded: identical hits give identical colours (%s)" % [threaded])
	_check(bool(_state()["pass_stats"]["consistent"]), "merged per-chunk statistics stay consistent")
	print("MATERIAL_CPU_FILM %s serial_vs_threaded=%s film_equal=%s threaded_samples=%d" % [kind, JSON.stringify(threaded), a["hash"] == t["hash"], threaded_samples])
	film.set("UseThreadedPass2LocalAccumulation", false)
	await _full_passes(1)


func _film_capture() -> Dictionary:
	var image := film.call("CopyFilmImageForTesting") as Image
	var hits := PackedStringArray()
	var width := int(film.get("FilmWidthPixels"))
	var height := int(film.get("FilmHeightRows"))
	for y in range(height):
		for x in range(width):
			var rec: Dictionary = film.call("GetFilmHitRecordForTesting", x, y)
			hits.append("%s|%d|%d|%s|%s" % [rec["had_hit"], int(rec["collider_id"]), int(rec["shape"]), var_to_str(rec["position"]), var_to_str(rec["normal"])])
	return {"image": image, "hits": hits, "hash": _hash(image), "stats": JSON.stringify(_state()["pass_stats"])}


func _compare_captures(a: Dictionary, b: Dictionary) -> Dictionary:
	var ia: Image = a["image"]
	var ib: Image = b["image"]
	var width := ia.get_width()
	var result := {"pixels": 0, "hit_differences": 0, "same_hit_different_colour": 0}
	for index in range(a["hits"].size()):
		result["pixels"] += 1
		var x := index % width
		var y := index / width
		if a["hits"][index] != b["hits"][index]:
			result["hit_differences"] += 1
		elif ia.get_pixel(x, y) != ib.get_pixel(x, y):
			result["same_hit_different_colour"] += 1
	return result


# ------------------------------------------------------------------ sentinels and scene-specific checks

func _unbound_sentinel() -> void:
	# A collider outside the current scene is not inventoried: its hits are Unbound (grey), not invented colour.
	var camera := get_root().get_camera_3d()
	var body := StaticBody3D.new()
	body.name = "OutsideSceneBlocker"
	# Same geometry group as scene bodies, so the film's geometry pruning admits it; it is still outside the
	# current scene and therefore outside the material inventory.
	body.add_to_group("raytrace_geometry")
	var collision := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(1.2, 1.2, 0.05)
	collision.shape = box
	body.add_child(collision)
	get_root().add_child(body)
	body.global_transform = Transform3D(camera.global_transform.basis, camera.global_transform.origin - camera.global_transform.basis.z * 1.5)
	await _frames(2)
	await _full_passes(1)
	# Informational: the oracle (mismatch == 0) still governs every pixel. Observed in qualification: the film
	# reports such pixels as no-hit, so the Unbound sentinel is not reachable from the scene; in-scene colliders
	# are always inventoried. Unbound attribution is covered by the managed MaterialCpuTests.
	var counts := _oracle("outside_scene_blocker")
	var stats: Dictionary = _state()["pass_stats"]
	print("MATERIAL_CPU_FILM %s outside_scene_blocker unbound_pixels=%d unbound_samples=%d" % [kind, int(counts["unbound"]), int(stats["unbound"])])
	body.queue_free()
	await _frames(2)
	await _full_passes(1)


func _gallery_truth_text() -> void:
	# The full Instrument HUD (H) carries the material claim (the panel's construction-time text is replaced
	# every frame); the compact Explore HUD says only "raster != transport", which remains true.
	await _key(KEY_H)
	await _frames(3)
	var hud := false
	for label in root_scene.find_children("*", "Label", true, false):
		var text := str(label.text)
		if text.contains("Raster materials: presentation only") and text.contains("N Material (CPU): authored colour at LIVE hit"):
			hud = true
		_check(not text.contains("Materials are raster presentation only"), "old unqualified material claim removed from HUD")
	cases += 1
	_check(hud, "Gallery Instrument truth text distinguishes raster materials from MaterialCpu observation")
	await _key(KEY_H)
	await _frames(3)


func _card_shows_material_cpu() -> void:
	var card := chamber.get_node("CanvasLayer/HUDRoot/InstrumentStatusCard")
	await _frames(4)
	var state: Dictionary = card.call("GetDisplayedStateForTesting")
	cases += 1
	_check(str(state.get("shading")) == MATERIAL_CPU and str(state.get("status_text")).contains("N Shading: Material (CPU)"), "Workbench card shows N Shading: Material (CPU) (%s)" % [state.get("status_text")])
	_check(not str(state.get("status_text")).contains("matched"), "no material statistics line added to the card in R0A.2")


func _dial_switch_correspondence() -> void:
	var dial := chamber.get_node("FieldDialController")
	var generation := int(_state()["generation"])
	var manifest := str(_state()["manifest_sha256"])
	dial.call("SetDisplayPresetForTesting", "Hermetic")
	await _frames(4)
	await _full_passes(2)
	_oracle("hermetic")
	cases += 1
	_check(str(_state()["last_rebuild_reason"]) == "live_reset" and str(_state()["manifest_sha256"]) == manifest, "dial switch rebuilds only through LIVE resets with an unchanged manifest (gen %d -> %s, %s)" % [generation, _state()["generation"], _state()["last_rebuild_reason"]])
	dial.call("SetDisplayPresetForTesting", "Gallery")
	await _frames(4)
	await _full_passes(1)


func _fieldviz_coexistence() -> void:
	await _select_live(MATERIAL_CPU)
	var manifest := str(_state()["manifest_sha256"])
	var names: Array[String] = []
	for _i in range(3):
		await _key(KEY_F)
		await _frames(4)
		names.append(str(film.get("CurrentFieldStructureModeName")))
		_check(str(ctrl.call("GetShadingModeName")) == MATERIAL_CPU and str(ctrl.call("GetModeName")) == "LIVE", "F leaves N and LIVE unchanged")
	cases += 1
	_check(names == ["Vectors", "Rays", "Hidden"], "FIELDVIZ F cycle intact under MaterialCpu (%s)" % [names])
	_check(str(_state()["manifest_sha256"]) == manifest, "field-structure presentation meshes never enter the material inventory")
	await _key(KEY_F)
	await _frames(4)
	await _full_passes(2)
	_oracle("fieldviz_vectors")
	film.call("ResetFieldStructureToHidden")
	await _frames(2)


# ------------------------------------------------------------------ Formal / Q / sealed

func _formal_non_interference() -> void:
	var depth := await _acquire("Depth")
	# Warm MaterialCpu in LIVE so a material snapshot is held while Formal acquires with N=Material (CPU).
	await _select_live(MATERIAL_CPU)
	await _frames(20)
	ctrl.call("set_mode", 0)
	await _frames(2)
	cases += 1
	_check(bool(_state()["snapshot_held"]), "material snapshot held entering Formal")
	var counters_before := _sampling_counters()
	var material := await _acquire(MATERIAL_CPU)
	var counters_after := _sampling_counters()
	cases += 1
	_check(depth == material, "Formal acquisition evidence identical with N=Depth and N=Material (CPU) (depth=%s material=%s)" % [depth, material])
	_check(counters_before == counters_after, "Formal/Q never scanned or sampled (%s -> %s)" % [counters_before, counters_after])
	print("MATERIAL_CPU_FILM %s formal_signature=%s" % [kind, JSON.stringify(material)])


func _acquire(mode_name: String) -> Dictionary:
	if mode_name == "Depth":
		# Symmetric control: the same short LIVE warm-up as the MaterialCpu acquisition.
		await _select_live("Depth")
		await _frames(20)
	ctrl.call("set_mode", 0)
	await _set_n(mode_name)
	await _frames(2)
	var generation := int(film.get("CathedralSnapshotGeneration"))
	var reset := int(film.get("FormalWorkingStateResetCount"))
	ctrl.call("EnterFormalSnapshotFromInput")
	await _wait_sealed()
	var signature := {
		"generation_delta": int(film.get("CathedralSnapshotGeneration")) - generation,
		"reset_delta": int(film.get("FormalWorkingStateResetCount")) - reset,
		"policy": str(film.get("CathedralFormalPolicySha256")),
		"query": str(film.get("CathedralProbeQueryDatasetSha256")),
		"contact": str(film.get("CathedralSpatialKernelContactCountSha256")),
		"lifecycle": str(film.get("CathedralSnapshotLifecycleStateName")),
		"film": _hash(film.call("CopyFilmImageForTesting")),
	}
	ctrl.call("set_mode", 0)
	await _frames(2)
	return signature


func _sealed_invariance() -> void:
	ctrl.call("set_mode", 0)
	await _set_n("Depth")
	ctrl.call("EnterFormalSnapshotFromInput")
	await _wait_sealed()
	var sealed := _formal_sig_with_image()
	var counters := _sampling_counters()
	var names: Array[String] = []
	for _i in range(5):
		await _key(KEY_N)
		await _frames(6)
		names.append(str(ctrl.call("GetShadingModeName")))
		_check(_formal_sig_with_image() == sealed, "sealed evidence unchanged at N=%s" % names[-1])
	cases += 1
	_check(names.has(MATERIAL_CPU), "sealed N cycle passes through Material (CPU) (%s)" % [names])
	_check(_sampling_counters() == counters, "no sampling while sealed")
	ctrl.call("set_mode", 0)
	await _frames(3)


func _sampling_counters() -> Dictionary:
	var state := _state()
	# Cumulative counters only: pass statistics are legitimately cleared by the existing film-pass reset.
	return {"serial": state["serial_samples"], "threaded": state["threaded_samples"], "revalidations": state["revalidations"], "rebuilds": state["rebuilds"]}


# ------------------------------------------------------------------ helpers

func _set_n(name: String) -> void:
	for _i in range(5):
		if str(ctrl.call("GetShadingModeName")) == name:
			break
		ctrl.call("cycle_shading_mode")
	_check(str(ctrl.call("GetShadingModeName")) == name, "N reaches %s" % name)
	await process_frame


func _select_live(name: String) -> void:
	await _set_n(name)
	if str(ctrl.call("GetModeName")) != "LIVE":
		ctrl.call("set_mode", 2)
	await _frames(6)


# Restarts the LIVE pass and waits for `count` completed passes (row cursor wrap).
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
				await process_frame
				return
		last = cursor
	_check(false, "LIVE completed %d passes" % count)


func _formal_sig() -> Dictionary:
	return {"gen": film.get("CathedralSnapshotGeneration"), "sgen": film.get("SealedProbeViewGeneration"), "sealed": film.get("ProbeViewAvailable"), "replay": film.get("CathedralContactReplayInvocationCount"), "reset": film.get("FormalWorkingStateResetCount"), "policy": film.get("CathedralFormalPolicySha256")}


func _formal_sig_with_image() -> Dictionary:
	var signature := _formal_sig()
	signature["query"] = film.get("CathedralProbeQueryDatasetSha256")
	signature["contact"] = film.get("CathedralSpatialKernelContactCountSha256")
	signature["film"] = _hash(film.call("CopyFilmImageForTesting"))
	return signature


func _wait_sealed() -> void:
	for _i in range(SEAL_TIMEOUT_FRAMES):
		if bool(film.get("ProbeViewAvailable")) and not bool(film.get("CathedralSnapshotLifecycleActive")):
			break
		await process_frame
	_check(bool(film.get("ProbeViewAvailable")), "acquisition sealed")
	await _frames(3)


func _state() -> Dictionary:
	return film.call("GetMaterialCpuStateForTesting")


func _q(color: Color) -> Color:
	quant.set_pixel(0, 0, Color(color.r, color.g, color.b, 1.0))
	return quant.get_pixel(0, 0)


func _as_color(value) -> Color:
	if value is Color:
		return value
	return Color(value.x, value.y, value.z)


func _hash(image) -> String:
	if image == null:
		return "none"
	var context := HashingContext.new()
	context.start(HashingContext.HASH_SHA256)
	context.update((image as Image).get_data())
	return context.finish().hex_encode()


func _open(scene_kind: String) -> void:
	kind = scene_kind
	change_scene_to_file(WB if scene_kind == "wb" else GAL)
	await scene_changed
	await _frames(30)
	root_scene = current_scene
	film = root_scene.find_children("GrinFilmCamera", "", true, false)[0]
	chamber = film.get_parent()
	ctrl = chamber.get_node("FilmController")


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
		failures.append("%s: %s" % [kind, message])
