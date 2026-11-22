extends SceneTree

# INSPECT-R0.1 headless test: passive, ordered per-pixel event export (CapturePixelPathObservationBundle).
# Observational only. Modes:
#   --mode=main          recorder ON Formal (scenario A, profile 0): refusal before seal, export x2,
#                        existing bundles captured before/after the export, selection refusals,
#                        refusal while a new acquisition is active.
#   --mode=recorder_off  recorder OFF Formal: export must be refused.
# Args: --output=<dir> [--pixels=i,j,...]

const SCENE_PATH := "res://ObservatoryWorkbench.tscn"
const FORMAL_TIMEOUT_FRAMES := 1200
const ACCEPTANCE := [4681, 11884, 14306, 9359, 4840, 4839, 8232, 4841, 5162, 6796, 8515, 0, 14399]
var mode := "main"
var output_root := ""
var pixels: PackedInt32Array = PackedInt32Array(ACCEPTANCE)
var failures := 0
var film: Node
var film_controller: Node


func _initialize() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--mode="):
			mode = arg.trim_prefix("--mode=")
		elif arg.begins_with("--output="):
			output_root = arg.trim_prefix("--output=")
		elif arg.begins_with("--pixels="):
			pixels = PackedInt32Array()
			for token in arg.trim_prefix("--pixels=").split(","):
				pixels.append(int(token))
	if output_root.is_empty():
		_fail("output root required")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(output_root)
	var root: Node = load(SCENE_PATH).instantiate()
	get_root().add_child(root)
	await _settle(30)
	var chamber := root.get_node("PlayableWorld/TransportChamberWorld")
	film = chamber.get_node("GrinFilmCamera")
	film_controller = chamber.get_node("FilmController")
	chamber.get_node("TransportChamberPlayer").set_physics_process(false)
	film.set("RecordFormalTransportLedger", mode == "main")
	await _settle(5)

	var pre := _export("refusal_before_seal", pixels)
	_expect(pre == "formal_transport_observation_unavailable", "export refused before any sealed acquisition (got '%s')" % pre)
	await _run_formal()

	if mode == "recorder_off":
		var off := _export("refusal_recorder_off", pixels)
		_expect(not off.is_empty(), "export refused for recorder-OFF acquisition (got '%s')" % off)
		_expect(not DirAccess.dir_exists_absolute(output_root.path_join("refusal_recorder_off")), "refusal wrote nothing")
		_finish()
		return

	var query_before := str(film.get("CathedralProbeQueryDatasetSha256"))
	_capture_existing("before")
	# Same run id for both exports: the run id is part of the written identity.
	var a := _export("export_A", pixels, "inspect-r0.1-export")
	_expect(a.is_empty(), "export A succeeded (got '%s')" % a)
	var b := _export("export_B", pixels, "inspect-r0.1-export")
	_expect(b.is_empty(), "export B succeeded (got '%s')" % b)
	_capture_existing("after")
	_expect(str(film.get("CathedralProbeQueryDatasetSha256")) == query_before, "query dataset unchanged by export")
	_expect(str(film.get("CathedralSnapshotLifecycleStateName")) == "complete", "lifecycle still complete after export")
	_expect(bool(film.get("ProbeViewAvailable")), "sealed plate still available after export")
	_expect(_same_file(output_root.path_join("export_A/pixel_paths.json"), output_root.path_join("export_B/pixel_paths.json")), "in-process determinism: pixel_paths.json identical")
	_expect(_same_file(output_root.path_join("export_A/pixel_path_manifest.json"), output_root.path_join("export_B/pixel_path_manifest.json")), "in-process determinism: manifest identical")
	for sub in ["portable_bundle", "transport_disposition_observation", "portal_remap_observation"]:
		var before_dir := output_root.path_join("before").path_join(sub)
		var after_dir := output_root.path_join("after").path_join(sub)
		for f in DirAccess.get_files_at(before_dir):
			if f.ends_with(".bin") or f.ends_with(".png"):
				_expect(_same_file(before_dir.path_join(f), after_dir.path_join(f)), "passive: %s/%s identical before/after export" % [sub, f])

	var cases := {
		"refusal_empty": PackedInt32Array(),
		"refusal_duplicate": PackedInt32Array([4681, 4681]),
		"refusal_negative": PackedInt32Array([-1]),
		"refusal_out_of_range": PackedInt32Array([14400]),
	}
	var expected := {
		"refusal_empty": "pixel_selection_empty",
		"refusal_duplicate": "pixel_selection_duplicate",
		"refusal_negative": "pixel_selection_out_of_range",
		"refusal_out_of_range": "pixel_selection_out_of_range",
	}
	for name in cases.keys():
		var r := _export(name, cases[name])
		_expect(r == expected[name], "%s refused with %s (got '%s')" % [name, expected[name], r])
		_expect(not DirAccess.dir_exists_absolute(output_root.path_join(name)), "%s wrote nothing" % name)

	# A new acquisition invalidates ownership: export must refuse while it is active.
	film_controller.call("set_mode", 0)
	await _settle(5)
	film_controller.call("set_mode", 1)
	await _settle(2)
	var active := _export("refusal_new_acquisition_active", pixels)
	_expect(not active.is_empty(), "export refused while a new acquisition is active (got '%s')" % active)
	_expect(not DirAccess.dir_exists_absolute(output_root.path_join("refusal_new_acquisition_active")), "active-acquisition refusal wrote nothing")
	print("PIXEL_PATH_EXPORT_INFO refusal_new_acquisition_active reason=%s" % active)
	_finish()


func _export(name: String, selection: PackedInt32Array, run_id := "") -> String:
	var reason := str(film.call("CapturePixelPathObservationBundle", output_root.path_join(name),
		run_id if not run_id.is_empty() else "inspect-r0.1-%s" % name, "hello-prt-gallery-v2", SCENE_PATH,
		OS.get_environment("XPRIMERAY_ENGINE_COMMIT"), OS.get_environment("XPRIMERAY_SOURCE_TREE_STATE"),
		OS.get_environment("XPRIMERAY_AUTHORED_DIFF_SHA256"), selection))
	print("PIXEL_PATH_EXPORT_CALL name=%s reason=%s" % [name, reason if not reason.is_empty() else "ok"])
	return reason


func _capture_existing(tag: String) -> void:
	var dir := output_root.path_join(tag)
	var env_commit := OS.get_environment("XPRIMERAY_ENGINE_COMMIT")
	var tree := OS.get_environment("XPRIMERAY_SOURCE_TREE_STATE")
	var diff := OS.get_environment("XPRIMERAY_AUTHORED_DIFF_SHA256")
	var r1 := str(film.call("CapturePortableObservatoryBundle", dir.path_join("portable_bundle"), "inspect-r0.1-" + tag,
		"hello-prt-gallery-v2", SCENE_PATH, "working-tree", "inspect-r0.1", tree, diff))
	var r2 := str(film.call("CaptureTransportDispositionObservationBundle", dir.path_join("transport_disposition_observation"),
		"inspect-r0.1-" + tag, "hello-prt-gallery-v2", SCENE_PATH, env_commit, tree, diff))
	var r3 := str(film.call("CapturePortalRemapObservationBundle", dir.path_join("portal_remap_observation"),
		"inspect-r0.1-" + tag, "hello-prt-gallery-v2", SCENE_PATH, env_commit, tree, diff))
	_expect(r1.is_empty() and r2.is_empty() and r3.is_empty(), "existing bundles captured (%s): '%s' '%s' '%s'" % [tag, r1, r2, r3])


func _run_formal() -> void:
	film_controller.call("set_mode", 1)
	await _settle(2)
	for _i in FORMAL_TIMEOUT_FRAMES:
		await process_frame
		if bool(film.get("ProbeViewAvailable")):
			break
	_expect(bool(film.get("ProbeViewAvailable")), "Formal acquisition completed")
	_expect(str(film.get("CathedralSnapshotLifecycleStateName")) == "complete", "lifecycle complete")
	print("PIXEL_PATH_EXPORT_INFO formal query=%s" % str(film.get("CathedralProbeQueryDatasetSha256")))


func _same_file(a: String, b: String) -> bool:
	if not FileAccess.file_exists(a) or not FileAccess.file_exists(b):
		return false
	return FileAccess.get_file_as_bytes(a) == FileAccess.get_file_as_bytes(b)


func _expect(condition: bool, message: String) -> void:
	if condition:
		print("PIXEL_PATH_EXPORT_OK " + message)
	else:
		failures += 1
		print("PIXEL_PATH_EXPORT_FAIL " + message)


func _fail(message: String) -> void:
	failures += 1
	print("PIXEL_PATH_EXPORT_FAIL " + message)


func _finish() -> void:
	# Complete pending managed finalizers while the engine is alive (same test-only helper as the
	# MaterialCpu fixtures); otherwise GodotSharp may crash at native teardown after the verdict.
	if film != null and is_instance_valid(film) and film.has_method("DrainManagedFinalizersForTesting"):
		print("PIXEL_PATH_EXPORT_INFO shutdown_drain %s" % film.call("DrainManagedFinalizersForTesting"))
		print("PIXEL_PATH_EXPORT_INFO shutdown_drain %s" % film.call("DrainManagedFinalizersForTesting"))
	if failures == 0:
		print("PIXEL_PATH_EXPORT PASS mode=%s pixels=%d" % [mode, pixels.size()])
		quit(0)
	else:
		print("PIXEL_PATH_EXPORT FAILED mode=%s failures=%d" % [mode, failures])
		quit(1)


func _settle(frames: int) -> void:
	for _i in frames:
		await process_frame
