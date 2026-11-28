extends SceneTree

# INSPECT-R0.2 Pixel Life Inspector fixture (Phase 1). Read-only viewer over the INSPECT-R0.1 model.
# Modes:
#   --mode=main          recorder ON Formal (canonical Workbench pose): I-key toggle, three-pixel comparison
#                        4839/4840/4841, content equality with the published export, interaction, and
#                        passivity (sealed identity, query counts, existing bundles, export, film image).
#   --mode=recorder_off  recorder OFF Formal: inspector must show the explicit refusal, nothing fabricated.
# Args: --output=<dir> [--screenshots]   (screenshots require a windowed, non-headless run)

const SCENE_PATH := "res://ObservatoryWorkbench.tscn"
const FORMAL_TIMEOUT_FRAMES := 1200
const TRIO := [4839, 4840, 4841]
const SEALED_GETTERS := [
	"ProbeViewAvailable", "SealedProbeViewGeneration", "CathedralSnapshotGeneration", "CathedralSnapshotLifecycleStateName",
	"CathedralSnapshotLifecycleReasonName", "CathedralFormalPolicyId", "CathedralFormalPolicySha256", "CathedralProbeQueryCount",
	"CathedralRawProbeQueryCount", "CathedralProbeQueryDatasetSha256", "CathedralFormalWorkUnitCommitCount",
	"CathedralFormalWorkUnitReplacementCount", "FormalWorkingStateResetCount", "CathedralContactReplayComplete",
	"CathedralContactReplayInvocationCount", "CathedralContactAuthorityToken", "CathedralSpatialKernelGeometrySnapshotSha256",
	"CathedralSpatialKernelContactCountSha256", "CathedralSpatialKernelAuthorityToken", "CathedralSpatialAuthorityContextSha256",
	"CathedralFrozenFieldSourceContentSha256", "SealedFrameFieldSourceContentSha256", "CathedralSealedProbeProfileStableId",
	"RecordFormalTransportLedger", "CurrentProbeViewDisplayName", "ProbeProfileStableId",
]
var mode := "main"
var output_root := ""
var screenshots := false
var failures := 0
var root_node: Node
var film: Node
var film_controller: Node
var inspector: Control
var workbench: Node
var shot_index := 0


func _initialize() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--mode="):
			mode = arg.trim_prefix("--mode=")
		elif arg.begins_with("--output="):
			output_root = arg.trim_prefix("--output=")
		elif arg == "--screenshots":
			screenshots = DisplayServer.get_name() != "headless"
	if output_root.is_empty():
		_fail("output root required")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(output_root)
	root_node = load(SCENE_PATH).instantiate()
	get_root().add_child(root_node)
	await _settle(30)
	workbench = root_node
	var chamber := root_node.get_node("PlayableWorld/TransportChamberWorld")
	film = chamber.get_node("GrinFilmCamera")
	film_controller = chamber.get_node("FilmController")
	chamber.get_node("TransportChamberPlayer").set_physics_process(false)
	film.set("RecordFormalTransportLedger", mode == "main")
	await _settle(5)

	# Before any seal: explicit unavailability, nothing fabricated.
	_expect(not bool(workbench.call("IsPixelLifeOpenForTesting")), "inspector starts closed")
	_expect(not bool(workbench.call("IsPixelLifeInstantiatedForTesting")) and root_node.get_node_or_null("UILayer/UIRoot/PixelLifeInspector") == null,
		"inspector is not instantiated until first opened (no scene-load allocation)")
	await _press(KEY_I)
	inspector = root_node.get_node_or_null("UILayer/UIRoot/PixelLifeInspector")
	_expect(inspector != null, "first I instantiates the inspector under the Workbench UI root")
	var st: Dictionary = inspector.call("GetDisplayedStateForTesting")
	_expect(bool(workbench.call("IsPixelLifeOpenForTesting")) and bool(st.get("visible")), "I key opens the inspector")
	_expect(str(st.get("status")) == "unavailable" and str(st.get("reason")) == "formal_transport_observation_unavailable",
		"before seal: explicit unavailability (got %s/%s)" % [st.get("status"), st.get("reason")])
	_expect(str(st.get("summary")).is_empty() and int(st.get("hit_targets")) == 0, "before seal: no pixel data drawn")
	_expect(Input.mouse_mode == Input.MOUSE_MODE_VISIBLE, "inspector frees the mouse")
	await _shot("before_seal_unavailable")
	await _press(KEY_I)
	_expect(not bool(workbench.call("IsPixelLifeOpenForTesting")) and not inspector.visible, "I key closes the inspector")

	await _run_formal()

	if mode == "recorder_off":
		await _press(KEY_I)
		st = inspector.call("GetDisplayedStateForTesting")
		_expect(str(st.get("reason")) == "recorder_disabled_for_acquisition", "recorder OFF: explicit refusal (got %s)" % st.get("reason"))
		_expect(str(st.get("warnings")).contains("OFF"), "recorder OFF explained in plain language")
		_expect(int(st.get("hit_targets")) == 0 and str(st.get("summary")).is_empty(), "recorder OFF: nothing drawn or inferred")
		var doc: Dictionary = inspector.call("GetInspectionDocumentForTesting")
		_expect(not doc.has("pixels"), "recorder OFF: no pixel model returned")
		await _shot("recorder_off_refusal")
		await _press(KEY_I)
		_finish()
		return

	# ---- before snapshot ----
	var before := _sealed_snapshot()
	_capture_existing("before")
	var e1 := _export("export_before")
	_expect(e1.is_empty(), "reference export before inspection (got '%s')" % e1)
	var film_png_before := _save_film_image("film_before.png")

	# ---- open on the sealed acquisition ----
	var fetches_before_open := int(inspector.call("GetDisplayedStateForTesting").get("fetch_count"))
	await _press(KEY_I)
	st = inspector.call("GetDisplayedStateForTesting")
	_expect(str(st.get("status")) == "ok", "sealed + recorder ON: inspection ok (got %s %s)" % [st.get("status"), st.get("reason")])
	_expect(st.get("rows") == TRIO, "default comparison rows 4839/4840/4841 (got %s)" % str(st.get("rows")))
	_expect(int(st.get("fetch_count")) == fetches_before_open + 1, "one fetch on open")
	print("PIXEL_LIFE_INFO fetch_ms=%.1f" % float(st.get("last_fetch_ms")))
	var doc: Dictionary = inspector.call("GetInspectionDocumentForTesting")
	_check_against_export(doc, "export_before")
	_expect(str(st.get("summary")).contains("px 4840 · film (40,30)"), "focused pixel and film coordinates shown")
	_expect(str(st.get("summary")).contains("film coordinates, not a physical path position"), "film-vs-path coordinate boundary shown")
	_expect(str(st.get("identity")).contains("SEALED COMPLETE") and str(st.get("identity")).contains("validated 14400 px"), "sealed status and whole-film validation shown")
	var warnings := str(st.get("warnings"))
	for token in ["Not a sealed ledger", "source not attested", "P1", "P2", "P4", "unqualified", "unqueried ≠ collision-free", "no 3D path"]:
		_expect(warnings.contains(token), "provenance/boundary warning visible: %s" % token)
	_expect(int(st.get("hit_targets")) > 0, "ledger markers drawn")
	await _shot("trio_default")

	# ---- interaction ----
	await _press(KEY_RIGHT)
	st = inspector.call("GetDisplayedStateForTesting")
	var first_marker := int(st.get("selected_event"))
	_expect(first_marker >= 0 and str(st.get("detail")).begins_with("RECORDED  event #%d" % first_marker), "Right selects the first non-question event")
	_expect(not str(st.get("detail")).contains("ProbeQuestionIssued"), "marker stepping skips question events")
	await _press(KEY_RIGHT, true)
	st = inspector.call("GetDisplayedStateForTesting")
	_expect(int(st.get("selected_event")) == first_marker + 1, "Shift+Right steps every event in recorder order")
	# contact in 4839 (row 0): find its ordinal from the model
	var contact := _first_event(doc, 4839, "GeometryContactObserved")
	_expect(contact >= 0, "4839 has a recorded contact event")
	inspector.call("SelectEventForTesting", 0, contact)
	st = inspector.call("GetDisplayedStateForTesting")
	_expect(str(st.get("detail")).contains("DIAGNOSTIC GODOT WITNESS") and str(st.get("detail")).contains("not a qualified terminal hit"), "contact shown as unqualified diagnostic witness")
	_expect(str(st.get("detail")).contains("no position or normal recorded"), "no fabricated contact geometry")
	_expect(str(st.get("summary")).contains("px 4839"), "selecting a marker focuses its pixel")
	await _shot("contact_selected_4839")
	var terminal := _first_event(doc, 4841, "Terminal")
	inspector.call("SelectEventForTesting", 2, terminal)
	st = inspector.call("GetDisplayedStateForTesting")
	_expect(str(st.get("detail")).contains("not a qualified surface hit"), "terminal marker not presented as a hit")
	await _press(KEY_U)
	st = inspector.call("GetDisplayedStateForTesting")
	_expect(bool(st.get("show_unavailable")) and str(st.get("detail")).begins_with("UNAVAILABLE"), "U lists unavailable channels")
	await _shot("unavailable_list")
	await _press(KEY_U)
	# keyboard row focus and pixel moves
	await _press(KEY_UP)
	await _press(KEY_UP)
	st = inspector.call("GetDisplayedStateForTesting")
	_expect(int(st.get("focus_row")) == 0, "Up moves focus to the first row")
	var fetches := int(st.get("fetch_count"))
	await _press(KEY_DOWN)
	await _press(KEY_DOWN)
	await _press(KEY_RIGHT, false, true)  # Ctrl+Right on 4841 -> 4842 replaces row 2
	st = inspector.call("GetDisplayedStateForTesting")
	_expect(st.get("rows") == [4839, 4840, 4842], "Ctrl+Right moves the focused pixel by one film pixel (got %s)" % str(st.get("rows")))
	_expect(int(st.get("fetch_count")) == fetches + 1, "pixel change refetches once")
	_expect(str(st.get("summary")).contains("px 4842 · film (42,30)"), "moved pixel summary")
	await _press(KEY_HOME)
	st = inspector.call("GetDisplayedStateForTesting")
	_expect(st.get("rows") == TRIO, "Home restores the acceptance trio")
	# mouse: click a ledger marker of row 1
	var clicked := await _click_first_marker(1)
	_expect(clicked, "mouse click on a ledger marker selects it")
	# timer refreshes read getters only: no refetch while nothing changes
	fetches = int(inspector.call("GetDisplayedStateForTesting").get("fetch_count"))
	await create_timer(1.6).timeout
	_expect(int(inspector.call("GetDisplayedStateForTesting").get("fetch_count")) == fetches, "periodic refresh does not refetch when nothing changed")
	# Esc closes the inspector without opening the Workbench dock
	await _press(KEY_ESCAPE)
	_expect(not bool(workbench.call("IsPixelLifeOpenForTesting")) and not bool(workbench.call("IsWorkbenchOpenForTesting")), "Esc closes the inspector only")
	# Workbench dock open, then I: dock closes, inspector opens
	await _press(KEY_ESCAPE)
	_expect(bool(workbench.call("IsWorkbenchOpenForTesting")), "Esc opens the Workbench dock")
	await _press(KEY_I)
	_expect(bool(workbench.call("IsPixelLifeOpenForTesting")) and not bool(workbench.call("IsWorkbenchOpenForTesting")), "I from the dock switches to the inspector")
	await _press(KEY_I)
	# repeated toggling
	for i in 12:
		await _press(KEY_I)
		await _settle(3)
	_expect(not bool(workbench.call("IsPixelLifeOpenForTesting")), "even number of toggles leaves it closed")
	if screenshots:
		await _press(KEY_I)
		for size in [Vector2i(1600, 900), Vector2i(1280, 720), Vector2i(860, 760)]:
			DisplayServer.window_set_size(size)
			get_root().size = size
			await _settle(10)
			inspector.call("SelectEventForTesting", 0, contact)
			await _settle(4)
			await _shot("trio_%dx%d" % [size.x, size.y])
		DisplayServer.window_set_size(Vector2i(1280, 720))
		get_root().size = Vector2i(1280, 720)
		await _press(KEY_I)
	await _settle(5)

	# ---- after snapshot: passivity ----
	var after := _sealed_snapshot()
	for key in before.keys():
		_expect(str(before[key]) == str(after[key]), "passive: %s unchanged (%s)" % [key, str(after[key])])
	_capture_existing("after")
	for sub in ["portable_bundle", "transport_disposition_observation", "portal_remap_observation"]:
		var before_dir := output_root.path_join("before").path_join(sub)
		var after_dir := output_root.path_join("after").path_join(sub)
		var n := 0
		for f in DirAccess.get_files_at(before_dir):
			if f.ends_with(".bin") or f.ends_with(".png"):
				n += 1
				_expect(_same_file(before_dir.path_join(f), after_dir.path_join(f)), "passive: %s/%s identical" % [sub, f])
		_expect(n > 0, "passivity compared %d files in %s" % [n, sub])
	var e2 := _export("export_after")
	_expect(e2.is_empty(), "reference export after inspection (got '%s')" % e2)
	_expect(_same_file(output_root.path_join("export_before/pixel_paths.json"), output_root.path_join("export_after/pixel_paths.json")), "passive: pixel_paths.json identical before/after inspection")
	_expect(_same_file(output_root.path_join("export_before/pixel_path_manifest.json"), output_root.path_join("export_after/pixel_path_manifest.json")), "passive: export manifest identical before/after inspection")
	var film_png_after := _save_film_image("film_after.png")
	_expect(not film_png_before.is_empty() and film_png_before == film_png_after, "passive: film plate image identical (%s)" % film_png_after.substr(0, 12))
	print("PIXEL_LIFE_INFO DETERMINISM query=%s policy=%s generation=%s contacts=%s" % [after["CathedralProbeQueryDatasetSha256"],
		after["CathedralFormalPolicySha256"], after["SealedProbeViewGeneration"], after["CathedralSpatialKernelContactCountSha256"]])

	# ---- a new acquisition while open: explicit, nothing stale shown ----
	await _press(KEY_I)
	film_controller.call("set_mode", 0)
	await _settle(5)
	film_controller.call("set_mode", 1)
	await _settle(2)
	inspector.call("ForceRefreshForTesting")
	st = inspector.call("GetDisplayedStateForTesting")
	_expect(str(st.get("status")) != "ok" and int(st.get("hit_targets")) == 0, "new acquisition: stale events are not shown (reason %s)" % st.get("reason"))
	print("PIXEL_LIFE_INFO new_acquisition_reason=%s" % st.get("reason"))
	await _press(KEY_I)
	_finish()


func _check_against_export(doc: Dictionary, export_name: String) -> void:
	var f := FileAccess.open(output_root.path_join(export_name).path_join("pixel_paths.json"), FileAccess.READ)
	var m := FileAccess.open(output_root.path_join(export_name).path_join("pixel_path_manifest.json"), FileAccess.READ)
	_expect(f != null and m != null, "reference export readable")
	if f == null or m == null:
		return
	var exported: Dictionary = JSON.parse_string(f.get_as_text())
	var manifest: Dictionary = JSON.parse_string(m.get_as_text())
	var by_index := {}
	for p in exported.get("pixels", []):
		by_index[int(p.get("pixel_index"))] = p
	var pixels: Array = doc.get("pixels", [])
	_expect(pixels.size() == 3, "three pixels returned")
	for p in pixels:
		var index := int(p.get("pixel_index"))
		_expect(by_index.has(index), "pixel %d present in export" % index)
		if by_index.has(index):
			_expect(JSON.stringify(p, "", true) == JSON.stringify(by_index[index], "", true), "pixel %d: inspector model == published export pixel (all recorded/derived/unavailable fields)" % index)
			var r: Dictionary = p.get("recorded", {})
			var d: Dictionary = p.get("derived", {})
			print("PIXEL_LIFE_INFO pixel=%d xy=(%d,%d) q=%d events=%d remap=%d contact=%d sealed_contacts=%d first_contact_step=%d steps=%d traveled=%s term=%s" % [
				index, int(p.get("x")), int(p.get("y")), int(r.get("question_count")), (r.get("events", []) as Array).size(),
				int(d.get("portal_remap_count")), int(d.get("contact_event_count")), int(r.get("sealed_bvh_contact_count")),
				int(r.get("sealed_replay_first_contact_step")), int(r.get("steps_integrated")), str(d.get("display_traveled")), str(r.get("termination"))])
	var acq: Dictionary = doc.get("acquisition", {})
	var macq: Dictionary = manifest.get("acquisition", {})
	for key in ["formal_policy_sha256", "canonical_query_dataset_sha256", "geometry_snapshot_sha256", "capture_generation", "width", "height"]:
		_expect(str(acq.get(key)) == str(macq.get(key)), "acquisition identity %s equals export manifest" % key)
	_expect(int(doc.get("validation", {}).get("validated_pixels", 0)) == int(manifest.get("validation", {}).get("validated_pixels", -1)),
		"inspection validated the same number of pixels as the export (whole film)")
	_expect(doc.get("validation", {}).get("full_film_digest_sha256") == null, "inspection claims no full-film digest")
	_expect(str(doc.get("source_attestation", "")).begins_with("not attested"), "inspection does not claim source attestation")


func _first_event(doc: Dictionary, index: int, kind: String) -> int:
	for p in doc.get("pixels", []):
		if int(p.get("pixel_index")) == index:
			for e in p.get("recorded", {}).get("events", []):
				if str(e.get("kind")) == kind:
					return int(e.get("event_ordinal"))
	return -1


func _click_first_marker(row: int) -> bool:
	var ledger: Control = inspector.get_node("Margin/Content/Ledger")
	var targets: Array = inspector.get("_hit_targets")
	for t in targets:
		if int(t[0]) == row and int(inspector.get("_selected_event")) != int(t[2]):
			var global := ledger.get_global_transform_with_canvas() * (t[1] as Vector2)
			var down := InputEventMouseButton.new()
			down.button_index = MOUSE_BUTTON_LEFT
			down.pressed = true
			down.position = global
			down.global_position = global
			var clicks_before := int(inspector.call("GetDisplayedStateForTesting").get("ledger_clicks"))
			get_root().push_input(down, true)
			var up := down.duplicate()
			up.pressed = false
			get_root().push_input(up, true)
			await _settle(3)
			var st: Dictionary = inspector.call("GetDisplayedStateForTesting")
			print("PIXEL_LIFE_INFO click pos=%s ledger=%s clicks=%d->%d selected=%d target=%d" % [global, ledger.get_global_rect(), clicks_before,
				int(st.get("ledger_clicks")), int(st.get("selected_event")), int(t[2])])
			return int(st.get("selected_event")) == int(t[2]) and int(st.get("focus_row")) == row
	return false


func _sealed_snapshot() -> Dictionary:
	var s := {}
	for key in SEALED_GETTERS:
		s[key] = film.get(key)
	s["controller_mode"] = film_controller.call("GetModeName")
	s["controller_opacity"] = film_controller.call("GetOpacityPercent")
	return s


func _save_film_image(name: String) -> String:
	var view: TextureRect = root_node.get_node("PlayableWorld/TransportChamberWorld/CanvasLayer/HUDRoot/FilmModule/FilmPlate/FilmView")
	if view.texture == null:
		return ""
	var image := view.texture.get_image()
	if image == null:
		return ""
	image.save_png(output_root.path_join(name))
	return image.get_data().hex_encode().sha256_text()


func _export(name: String) -> String:
	var reason := str(film.call("CapturePixelPathObservationBundle", output_root.path_join(name), "inspect-r0.2-reference",
		"hello-prt-gallery-v2", SCENE_PATH, OS.get_environment("XPRIMERAY_ENGINE_COMMIT"), OS.get_environment("XPRIMERAY_SOURCE_TREE_STATE"),
		OS.get_environment("XPRIMERAY_AUTHORED_DIFF_SHA256"), PackedInt32Array(TRIO)))
	print("PIXEL_LIFE_EXPORT name=%s reason=%s" % [name, reason if not reason.is_empty() else "ok"])
	return reason


func _capture_existing(tag: String) -> void:
	var dir := output_root.path_join(tag)
	var commit := OS.get_environment("XPRIMERAY_ENGINE_COMMIT")
	var tree := OS.get_environment("XPRIMERAY_SOURCE_TREE_STATE")
	var diff := OS.get_environment("XPRIMERAY_AUTHORED_DIFF_SHA256")
	var r1 := str(film.call("CapturePortableObservatoryBundle", dir.path_join("portable_bundle"), "inspect-r0.2-" + tag,
		"hello-prt-gallery-v2", SCENE_PATH, "working-tree", "inspect-r0.2", tree, diff))
	var r2 := str(film.call("CaptureTransportDispositionObservationBundle", dir.path_join("transport_disposition_observation"),
		"inspect-r0.2-" + tag, "hello-prt-gallery-v2", SCENE_PATH, commit, tree, diff))
	var r3 := str(film.call("CapturePortalRemapObservationBundle", dir.path_join("portal_remap_observation"),
		"inspect-r0.2-" + tag, "hello-prt-gallery-v2", SCENE_PATH, commit, tree, diff))
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


func _press(keycode: int, shift := false, ctrl := false) -> void:
	var down := InputEventKey.new()
	down.keycode = keycode
	down.physical_keycode = keycode
	down.pressed = true
	down.shift_pressed = shift
	down.ctrl_pressed = ctrl
	Input.parse_input_event(down)
	var up := down.duplicate()
	up.pressed = false
	Input.parse_input_event(up)
	await _settle(3)


func _shot(name: String) -> void:
	if not screenshots:
		return
	await _settle(6)
	await RenderingServer.frame_post_draw
	shot_index += 1
	var image := get_root().get_texture().get_image()
	var path := output_root.path_join("shot_%02d_%s.png" % [shot_index, name])
	image.save_png(path)
	print("PIXEL_LIFE_SHOT %s %dx%d" % [path, image.get_width(), image.get_height()])


func _same_file(a: String, b: String) -> bool:
	if not FileAccess.file_exists(a) or not FileAccess.file_exists(b):
		return false
	return FileAccess.get_file_as_bytes(a) == FileAccess.get_file_as_bytes(b)


func _expect(condition: bool, message: String) -> void:
	if condition:
		print("PIXEL_LIFE_OK " + message)
	else:
		failures += 1
		print("PIXEL_LIFE_FAIL " + message)


func _fail(message: String) -> void:
	failures += 1
	print("PIXEL_LIFE_FAIL " + message)


func _finish() -> void:
	if film != null and is_instance_valid(film) and film.has_method("DrainManagedFinalizersForTesting"):
		print("PIXEL_LIFE_INFO shutdown_drain %s" % film.call("DrainManagedFinalizersForTesting"))
		print("PIXEL_LIFE_INFO shutdown_drain %s" % film.call("DrainManagedFinalizersForTesting"))
	if failures == 0:
		print("PIXEL_LIFE PASS mode=%s" % mode)
		quit(0)
	else:
		print("PIXEL_LIFE FAILED mode=%s failures=%d" % [mode, failures])
		quit(1)


func _settle(frames: int) -> void:
	for _i in frames:
		await process_frame
