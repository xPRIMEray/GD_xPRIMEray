extends SceneTree

const GALLERY_SCENE := preload("res://Experiences/SuspendedPortalGallery.tscn")
const FORMAL_TIMEOUT_FRAMES := 1200

var gallery: Node
var world: Node
var film: Node
var controller: Node
var player: Node
var film_plate: Control
var film_view: TextureRect
var probe_hud_label: Label
var status_label: Label
var failures: Array[String] = []


func _initialize() -> void:
	gallery = GALLERY_SCENE.instantiate()
	get_root().add_child(gallery)
	await _settle(12)
	world = gallery.get_node("SuspendedPortalGalleryWorld")
	film = world.get_node("GrinFilmCamera")
	controller = world.get_node("FilmController")
	player = world.get_node("TransportChamberPlayer")
	film_plate = world.get_node("CanvasLayer/HUDRoot/FilmPlate")
	film_view = world.get_node("CanvasLayer/HUDRoot/FilmPlate/FilmView")
	probe_hud_label = gallery.get("_probe_hud_label") as Label
	status_label = world.get_node("CanvasLayer/HUDRoot/FilmStatusLabel") as Label

	_verify_status_sink_contract()
	_verify_hud_has_no_live_row_outside_live("OFF")
	_assert(not film_plate.visible, "A fresh Gallery plate must be hidden")
	_assert(not bool(film.get("ProbeViewAvailable")), "Fresh Gallery unexpectedly has a seal")

	_assert(bool(controller.call("EnterFormalSnapshotFromInput")),
		"FilmController rejected the normal Formal Q request")
	_assert(String(controller.call("GetModeName")) == "SNAPSHOT", "Q did not enter SNAPSHOT")
	_assert(bool(film.get("CathedralSnapshotLifecycleActive")), "Formal lifecycle did not become active")
	_assert(bool(player.call("IsPoseHeld")), "Active Formal acquisition did not hold the player pose")
	_assert(not film_plate.visible, "Plate became visible before Formal sealing")
	_verify_hud_has_no_live_row_outside_live("SNAPSHOT")
	await process_frame

	var completed := await _wait_for_seal()
	if not completed:
		_finish()
		return
	await _settle(3)

	_assert(String(film.get("CathedralSnapshotLifecycleStateName")) == "complete",
		"Sealed lifecycle is not complete")
	_assert(bool(film.get("ProbeViewAvailable")), "Successful Formal completion did not expose a seal")
	_assert(film_plate.visible, "Successful seal did not automatically reveal the plate")
	_assert(bool(controller.get("_formal_snapshot_terminal_handled")),
		"FilmController did not latch its terminal-handled transition")
	_assert(int(film.get("CurrentProbeView")) == 0, "Production completion did not paint the first Q view")
	_assert(film_view.texture != null, "Visible sealed plate has no texture")
	_assert(status_label.text.find("COMPLETE") >= 0, "Hidden status sink did not receive terminal status")
	_assert(bool(player.call("IsPoseHeld")), "Completed SNAPSHOT unexpectedly released the held pose")
	_verify_hud_has_no_live_row_outside_live("SNAPSHOT complete")

	var sealed_state := _observation_state()
	var status_text_before := status_label.text
	for _refresh in range(20):
		gallery.call("_update_presentation_hud")
	_assert(_observation_state() == sealed_state,
		"Twenty presentation refreshes changed acquisition or effective policy state")
	_assert(status_label.text == status_text_before,
		"Gallery presentation refresh wrote the FilmController status label")
	_assert(controller.get("_status_label") == status_label,
		"FilmController status reference became stale")

	var first_view := int(film.get("CurrentProbeView"))
	await _dispatch_key(KEY_Q)
	_assert(int(film.get("CurrentProbeView")) == (first_view + 1) % 3,
		"Q did not advance to the expected sealed view")
	_assert(film_plate.visible, "Q cycling hid the sealed plate")
	_assert(film_view.texture != null, "Q cycling removed the sealed texture")

	await _dispatch_key(KEY_G)
	_assert(String(controller.call("GetModeName")) == "LIVE", "G did not enter LIVE after completion")
	_assert(film_plate.visible, "LIVE preview did not preserve plate visibility")
	_assert(bool(film.get("UpdateEveryFrame")), "LIVE preview did not resume frame updates")
	_assert(not bool(player.call("IsPoseHeld")), "LIVE entry did not release the held player pose")
	gallery.call("_update_presentation_hud")
	_assert(_hud_has_numeric_live_row(), "LIVE HUD does not report the current LIVE row")

	print("GALLERY_SEALED_PLATE OBSERVATION generation=%d sealed_generation=%d queries=%d policy=%s query_sha=%s contact_sha=%s" % [
		int(sealed_state["generation"]), int(sealed_state["sealed_generation"]),
		int(film.get("CathedralProbeQueryCount")), String(sealed_state["policy_sha"]),
		String(film.get("CathedralProbeQueryDatasetSha256")),
		String(film.get("CathedralSpatialKernelContactCountSha256")),
	])
	_finish()


func _verify_status_sink_contract() -> void:
	var labels := world.find_children("FilmStatusLabel", "Label", true, false)
	_assert(labels.size() == 1, "Gallery must contain exactly one FilmStatusLabel")
	_assert(status_label != null and not status_label.visible, "FilmStatusLabel must be hidden")
	_assert(controller.get("_status_label") == status_label,
		"FilmController did not resolve the dedicated status label")
	_assert(NodePath(controller.get("status_label_path")) == NodePath("../CanvasLayer/HUDRoot/FilmStatusLabel"),
		"FilmController status path is not the dedicated Gallery label")
	var source := FileAccess.get_file_as_string("res://Experiences/SuspendedPortalGalleryController.gd")
	_assert(source.find("film_status.text") < 0,
		"Gallery controller introduced a second FilmStatusLabel text writer")


func _observation_state() -> Dictionary:
	return {
		"mode": String(controller.call("GetModeName")),
		"profile": int(film.get("ProbeProfileIndex")),
		"macro": int(film.get("RuntimeMacroSelectedMode")),
		"view": int(film.get("CurrentProbeView")),
		"generation": int(film.get("CathedralSnapshotGeneration")),
		"state": String(film.get("CathedralSnapshotLifecycleStateName")),
		"reason": String(film.get("CathedralSnapshotLifecycleReasonName")),
		"sealed": bool(film.get("ProbeViewAvailable")),
		"sealed_generation": int(film.get("SealedProbeViewGeneration")),
		"policy_id": String(film.get("CathedralFormalPolicyId")),
		"policy_sha": String(film.get("CathedralFormalPolicySha256")),
		"broadphase_mode": int(film.get("EffectiveBroadphaseMode")),
		"broadphase_policy": int(film.get("EffectiveBroadphasePolicy")),
		"broadphase_quick_ray": bool(film.get("EffectiveBroadphaseQuickRay")),
		"broadphase_overlap": bool(film.get("EffectiveBroadphaseOverlap")),
		"broadphase_reason": String(film.get("EffectiveBroadphaseReason")),
	}


func _wait_for_seal() -> bool:
	for _frame in range(FORMAL_TIMEOUT_FRAMES):
		await process_frame
		if bool(film.get("ProbeViewAvailable")) and String(film.get("CathedralSnapshotLifecycleStateName")) == "complete":
			return true
		if bool(film.get("CathedralProbeSnapshotIsTerminal")) and String(film.get("CathedralSnapshotLifecycleStateName")) != "complete":
			break
	_assert(false, "Formal did not seal: state=%s reason=%s processed=%d/%d" % [
		String(film.get("CathedralSnapshotLifecycleStateName")),
		String(film.get("CathedralSnapshotLifecycleReasonName")),
		int(film.get("CathedralSnapshotProcessedPixelCount")),
		int(film.get("CathedralSnapshotTotalPixelCount")),
	])
	return false


func _verify_hud_has_no_live_row_outside_live(context: String) -> void:
	gallery.call("_update_presentation_hud")
	_assert(not _hud_has_numeric_live_row(), "%s HUD displays a stale LIVE row" % context)


func _hud_has_numeric_live_row() -> bool:
	var regex := RegEx.new()
	regex.compile("LIVE row [0-9]+")
	return regex.search(probe_hud_label.text) != null


func _dispatch_key(keycode: Key) -> void:
	_press_key(keycode)
	await _settle(3)
	_release_key(keycode)
	await process_frame


func _press_key(keycode: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	event.echo = false
	Input.parse_input_event(event)


func _release_key(keycode: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = false
	Input.parse_input_event(event)


func _settle(frames: int) -> void:
	for _frame in range(frames):
		await process_frame


func _assert(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)


func _finish() -> void:
	if failures.is_empty():
		print("GALLERY_SEALED_PLATE PASS")
		quit(0)
		return
	for failure in failures:
		push_error("GALLERY_SEALED_PLATE FAIL: %s" % failure)
	print("GALLERY_SEALED_PLATE FAIL count=%d" % failures.size())
	quit(1)
