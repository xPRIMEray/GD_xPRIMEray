extends SceneTree

# UI-R2B accepted-action automatic film reveal: regression matrix for the
# Suspended Portal Gallery and the Observatory Workbench.
#
# Accepted keyboard G starts and Q / Shift+Q / Ctrl+Q actions reveal a hidden film
# (Explore -> Instrument) once. Refusals, stopping LIVE, Inspect, disabled input
# and direct programmatic calls never emit. Manual H stays authoritative.
# Gallery F10 capture ordering cases need a real framebuffer and run only natively.

const WB := "res://ObservatoryWorkbench.tscn"
const GAL := "res://Experiences/SuspendedPortalGallery.tscn"
const EXPLORE := 0
const INSTRUMENT := 1
const INSPECT := 2
const SEAL_TIMEOUT_FRAMES := 9000

var failures: Array[String] = []
var kind := ""
var root_scene: Node
var chamber: Node
var film: Node
var ctrl: Node
var actions: Array[String] = []
var cases := 0
var acquisitions := 0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	for scene_kind in ["gal", "wb"]:
		await _open(scene_kind)
		await _matrix()
	await _round_trips()
	var native := DisplayServer.get_name() != "headless"
	if native:
		await _open("gal")
		await _gallery_capture_ordering()
	else:
		print("AUTO_REVEAL capture_ordering=SKIPPED_HEADLESS (requires a native framebuffer)")
	if failures.is_empty():
		print("AUTO_REVEAL PASS cases=%d acquisitions=%d capture_ordering=%s server=%s" % [cases, acquisitions, "PASS" if native else "SKIPPED", DisplayServer.get_name()])
		quit(0)
	else:
		for failure in failures:
			push_error("AUTO_REVEAL FAIL: " + failure)
		print("AUTO_REVEAL FAIL count=%d cases=%d" % [failures.size(), cases])
		quit(1)


# ------------------------------------------------------------------ matrix

func _matrix() -> void:
	_check(_level() == EXPLORE and _accepted() == 0 and _reveals() == 0, "fresh scene starts in Explore with zero counts")

	# Direct programmatic calls never emit.
	_mark()
	_check(bool(ctrl.call("ToggleLiveFromInput")) and _mode() == "LIVE", "direct ToggleLiveFromInput starts LIVE")
	await _frames(4)
	_check(bool(ctrl.call("ToggleLiveFromInput")) and _mode() == "OFF", "direct ToggleLiveFromInput stops LIVE")
	ctrl.call("set_mode", 2)
	await _frames(4)
	ctrl.call("set_mode", 0)
	await _frames(2)
	var generation := _generation()
	_check(bool(ctrl.call("EnterFormalSnapshotFromInput")), "direct EnterFormalSnapshotFromInput accepted")
	await _wait_sealed("direct Formal")
	_check(_generation() == generation + 1, "direct Formal advanced generation once")
	_check(bool(ctrl.call("HandleProbeViewInput", false, false)), "direct HandleProbeViewInput cycles the sealed view")
	_check(bool(ctrl.call("HandleProbeViewInput", false, true)), "direct HandleProbeViewInput replacement accepted")
	await _wait_sealed("direct replacement")
	ctrl.call("set_mode", 0)
	await _frames(2)
	_expect_unchanged("direct calls never emit", EXPLORE)

	# G start reveals; G stop never changes the level.
	_mark()
	await _key(KEY_G)
	_expect_reveal("G start from OFF", "live_started")
	_mark()
	await _key(KEY_G)
	_expect_unchanged("G stop in Instrument", INSTRUMENT)
	await _key(KEY_G)
	_mark()
	await _h()
	_check(_level() == EXPLORE, "manual H returns a revealed LIVE film to Explore")
	var row_start := int(film.get("FilmRowCursor"))
	var stayed := true
	for _i in range(90):
		await process_frame
		stayed = stayed and _level() == EXPLORE
	_check(stayed, "manual Explore persists for 90 frames of LIVE progress")
	_check(int(film.get("FilmRowCursor")) != row_start or int(film.get("FilmRowCursor")) > 0, "LIVE progressed while in manual Explore")
	await _key(KEY_Q)
	_check(_disposition() == "formal_from_live_refused_press_g_then_q", "Q in LIVE refused")
	_expect_unchanged("refused Q in LIVE", EXPLORE)
	await _key(KEY_G)
	_check(_mode() == "OFF", "G stopped LIVE")
	_expect_unchanged("G stop in Explore", EXPLORE)

	# Disabled input never emits.
	_mark()
	if kind == "wb":
		await _key(KEY_ESCAPE)
		_check(_level() == INSPECT, "Esc opened Inspect")
		await _key(KEY_G)
		await _key(KEY_Q)
		_check(_mode() == "OFF" and _disposition() == "interactive_input_disabled", "Inspect suspended G and Q")
		_check(_level() == INSPECT, "Inspect level unchanged by refused G/Q")
		chamber.emit_signal("operator_observation_accepted", &"inspect_guard_probe")
		await _frames(2)
		_check(_level() == INSPECT, "relay emission while Inspect is open does not change Inspect")
		await _key(KEY_ESCAPE)
		_check(_level() == EXPLORE, "closing Inspect restores the unchanged closed level")
		_check(_reveals() == int(_marked["reveals"]), "Inspect guard did not promote the closed level")
		await _h()
		await _key(KEY_ESCAPE)
		await _key(KEY_ESCAPE)
		_check(_level() == INSTRUMENT, "Instrument survives an Inspect round trip")
		await _h()
	else:
		ctrl.call("SetInputEnabled", false)
		await _key(KEY_G)
		await _key(KEY_Q)
		_check(_mode() == "OFF" and _disposition() == "interactive_input_disabled", "disabled Gallery input suspended G and Q")
		ctrl.call("SetInputEnabled", true)
	_expect_unchanged("disabled input never emits", EXPLORE)

	# Q Formal request, active refusals, manual Explore through sealing.
	# Headless acquisitions can seal within a few frames, so the refusals are
	# delivered in the acceptance frame: Q (accepted), H (manual Explore), then Q
	# and G while the acquisition is active.
	_mark()
	generation = _generation()
	var refusal_dispositions: Array[String] = []
	_queue(KEY_Q)
	_queue(KEY_H)
	await process_frame
	_check(bool(film.get("CathedralSnapshotLifecycleActive")), "Formal active in its acceptance frame")
	_queue(KEY_Q)
	await process_frame
	refusal_dispositions.append(_disposition())
	_queue(KEY_G)
	await process_frame
	refusal_dispositions.append(_disposition())
	_check(_generation() == generation + 1, "Q Formal advanced generation once")
	_check(refusal_dispositions.size() == 2 and refusal_dispositions[0] == "formal_active_q_ignored" and refusal_dispositions[1] == "formal_active_g_refused", "Q and G during active Formal refused (%s)" % ",".join(refusal_dispositions))
	cases += 1
	_check(actions.size() == int(_marked["actions"]) + 1 and actions[-1] == "formal_requested", "Q Formal request reported formal_requested")
	_check(_reveals() == int(_marked["reveals"]) + 1, "Q Formal request promoted exactly once before manual H")
	_mark()
	_check(_level() == EXPLORE, "manual H in the acceptance frame returned to Explore")
	var explore_through_seal: bool = await _wait_sealed("Q Formal", true)
	_check(explore_through_seal, "manual Explore persists through Formal progress and sealing")
	_expect_unchanged("sealing never emits", EXPLORE)

	# Sealed view cycling and replacement.
	_mark()
	await _key(KEY_Q)
	_expect_reveal("sealed Q cycles view", "probe_view_next")
	await _h()
	_mark()
	await _key(KEY_Q, true)
	_expect_reveal("sealed Shift+Q cycles view", "probe_view_previous")
	_mark()
	await _key(KEY_Q)
	_expect_idempotent("sealed Q in Instrument", "probe_view_next")
	await _h()
	_mark()
	generation = _generation()
	await _key(KEY_Q, false, true)
	_expect_reveal("Ctrl+Q replacement", "formal_replacement_requested")
	await _wait_sealed("first Ctrl+Q")
	_mark()
	await _key(KEY_Q, false, true)
	_expect_idempotent("second Ctrl+Q in Instrument", "formal_replacement_requested")
	await _wait_sealed("second Ctrl+Q")
	_check(_generation() == generation + 2, "two Ctrl+Q replacements advanced generation twice")

	# G from sealed evidence releases it into LIVE and reveals.
	await _h()
	_mark()
	await _key(KEY_G)
	_expect_reveal("G from sealed evidence", "live_started_sealed_evidence_released")
	await _key(KEY_G)
	await _h()

	# Queued Q, Q, G in one frame: one acceptance, one promotion.
	_mark()
	generation = _generation()
	_queue(KEY_Q)
	_queue(KEY_Q)
	_queue(KEY_G)
	await _frames(3)
	_check(_mode() == "SNAPSHOT" and _generation() == generation + 1, "queued Q, Q, G started exactly one acquisition")
	_check(_disposition() == "formal_active_g_refused", "queued G refused during active Formal")
	_check(_accepted() == int(_marked["accepted"]) + 1 and _reveals() == int(_marked["reveals"]) + 1 and _level() == INSTRUMENT, "queued Q, Q, G promoted exactly once")
	cases += 1
	await _wait_sealed("queued Q")
	ctrl.call("set_mode", 0)
	await _frames(2)
	await _h()

	# Active Formal with H toggling every frame produces identical evidence.
	var control: Dictionary = await _acquire_signature(false)
	var toggled: Dictionary = await _acquire_signature(true)
	_check(control == toggled, "H toggling every frame left Formal evidence identical (control=%s toggled=%s)" % [control, toggled])
	print("AUTO_REVEAL %s h_toggle_signature=%s" % [kind, JSON.stringify(control)])
	print("AUTO_REVEAL %s scene=PASS_CANDIDATE actions=%s" % [kind, ",".join(actions)])


func _acquire_signature(toggle_h: bool) -> Dictionary:
	ctrl.call("set_mode", 0)
	await _frames(2)
	if _level() != EXPLORE:
		await _h()
	var generation := _generation()
	var reset_before := int(film.get("FormalWorkingStateResetCount"))
	_queue(KEY_Q)
	var toggles := 0
	var toggles_while_active := 0
	var frames := 0
	while frames < SEAL_TIMEOUT_FRAMES:
		if frames > 0 and bool(film.get("ProbeViewAvailable")) and not bool(film.get("CathedralSnapshotLifecycleActive")):
			break
		if toggle_h:
			if bool(film.get("CathedralSnapshotLifecycleActive")):
				toggles_while_active += 1
			_queue(KEY_H)
			toggles += 1
		await process_frame
		frames += 1
	acquisitions += 1
	await _frames(4)
	_check(bool(film.get("ProbeViewAvailable")), "%s acquisition sealed (toggle=%s)" % [kind, toggle_h])
	if toggle_h:
		_check(toggles_while_active >= 1, "H toggled while the acquisition was active (%d toggles, %d while active, %d frames)" % [toggles, toggles_while_active, frames])
		print("AUTO_REVEAL %s h_toggle frames=%d toggles=%d while_active=%d" % [kind, frames, toggles, toggles_while_active])
	var signature := {
		"generation_delta": _generation() - generation,
		"reset_delta": int(film.get("FormalWorkingStateResetCount")) - reset_before,
		"policy": str(film.get("CathedralFormalPolicySha256")),
		"query": str(film.get("CathedralProbeQueryDatasetSha256")),
		"contact": str(film.get("CathedralSpatialKernelContactCountSha256")),
		"lifecycle": str(film.get("CathedralSnapshotLifecycleStateName")),
		"film": _film_hash(),
	}
	ctrl.call("set_mode", 0)
	await _frames(2)
	if _level() != EXPLORE:
		await _h()
	return signature


# ------------------------------------------------------------------ round trips

func _round_trips() -> void:
	var previous: Array[Node] = []
	for scene_kind in ["gal", "wb", "gal", "wb"]:
		await _open(scene_kind, false)
		for old in previous:
			_check(not is_instance_valid(old), "previous scene's FilmController freed before %s" % scene_kind)
		_check(_level() == EXPLORE and _accepted() == 0 and _reveals() == 0, "%s round trip starts in Explore with zero counts" % scene_kind)
		var controller_connections := (ctrl.get_signal_connection_list("operator_observation_accepted") as Array).size()
		_check(controller_connections == 1, "%s FilmController has exactly one presentation connection (%d)" % [scene_kind, controller_connections])
		if scene_kind == "wb":
			var relay_connections := (chamber.get_signal_connection_list("operator_observation_accepted") as Array).size()
			_check(relay_connections == 1, "Workbench relay has exactly one owner connection (%d)" % relay_connections)
		await _key(KEY_G)
		_check(_level() == INSTRUMENT and _accepted() == 1 and _reveals() == 1, "%s round trip reveals once" % scene_kind)
		await _key(KEY_G)
		previous = [ctrl]


# ------------------------------------------------------------------ Gallery capture ordering (native)

var _emit_state := {}


func _gallery_capture_ordering() -> void:
	root_scene.connect("screenshot_captured", Callable(self, "_on_screenshot_captured"))

	# A: an accepted G during a beauty capture is deferred until the capture has
	# restored and reported its state, then applied.
	_mark()
	_queue(KEY_F10, true)
	_queue(KEY_G)
	var a: Array = await Signal(root_scene, "screenshot_captured")
	await _frames(2)
	_check(bool(a[1]) and bool(a[2]), "A: beauty capture restored its state")
	_check(not bool(_emit_state["full_hud"]) and bool(_emit_state["pending"]), "A: reveal deferred while the capture reported")
	_check(_level() == INSTRUMENT and _reveals() == int(_marked["reveals"]) + 1 and not _pending(), "A: deferred reveal applied after capture")
	cases += 1
	await _key(KEY_G)
	await _h()

	# B control: H alone during a minimal capture.
	_mark()
	_queue(KEY_F10)
	await process_frame
	var b_control_in_progress := _capture_in_progress()
	_queue(KEY_H)
	var b_control: Array = await Signal(root_scene, "screenshot_captured")
	await _frames(2)
	var control_final := _level()
	var control_restored := bool(b_control[2])
	if _level() != EXPLORE:
		await _h()

	# B: G then H during the same capture. Manual H supersedes the deferred reveal.
	_mark()
	_queue(KEY_F10)
	_queue(KEY_G)
	await process_frame
	var b_in_progress := _capture_in_progress() and _pending()
	_queue(KEY_H)
	var b: Array = await Signal(root_scene, "screenshot_captured")
	await _frames(2)
	_check(b_control_in_progress and b_in_progress, "B: H arrived while the capture was in progress (control=%s test=%s)" % [b_control_in_progress, b_in_progress])
	_check(not bool(_emit_state["pending"]), "B: manual H cancelled the deferred reveal")
	_check(_level() == control_final and bool(b[2]) == control_restored, "B: final level and restoration match H alone (level=%d control=%d)" % [_level(), control_final])
	_check(_reveals() == int(_marked["reveals"]), "B: no automatic promotion after manual H")
	cases += 1
	await _key(KEY_G)
	if _level() != EXPLORE:
		await _h()

	# C: H then G during the same capture. The later accepted action reveals after the capture.
	await _h()
	_check(_level() == INSTRUMENT, "C: starts in Instrument")
	_mark()
	_queue(KEY_F10)
	await process_frame
	_queue(KEY_H)
	await process_frame
	var c_in_progress := _capture_in_progress() and _level() == EXPLORE
	_queue(KEY_G)
	await process_frame
	var c_pending := _pending()
	await Signal(root_scene, "screenshot_captured")
	await _frames(2)
	_check(c_in_progress and c_pending, "C: H then G arrived during the capture (h=%s pending=%s)" % [c_in_progress, c_pending])
	_check(not bool(_emit_state["full_hud"]) and bool(_emit_state["pending"]), "C: capture reported the manual Explore level")
	_check(_level() == INSTRUMENT and _reveals() == int(_marked["reveals"]) + 1 and not _pending(), "C: later accepted G revealed after capture")
	cases += 1
	await _key(KEY_G)
	print("AUTO_REVEAL capture_ordering A=beauty_deferred B=H_supersedes C=G_after_H_reveals control_level=%d control_restored=%s" % [control_final, control_restored])


func _on_screenshot_captured(_path: String, _clean: bool, _restored: bool) -> void:
	_emit_state = {"full_hud": bool(root_scene.get("_full_hud")), "pending": _pending()}


# ------------------------------------------------------------------ helpers

var _marked := {}


func _open(scene_kind: String, spy := true) -> void:
	kind = scene_kind
	change_scene_to_file(WB if scene_kind == "wb" else GAL)
	await scene_changed
	await _frames(30)
	root_scene = current_scene
	film = root_scene.find_children("GrinFilmCamera", "", true, false)[0]
	chamber = film.get_parent()
	ctrl = chamber.get_node("FilmController")
	actions.clear()
	if spy:
		ctrl.connect("operator_observation_accepted", Callable(self, "_on_accepted"))


func _on_accepted(action: StringName) -> void:
	actions.append(String(action))


func _level() -> int:
	if kind == "wb":
		return int(root_scene.call("GetPresentationLevelForTesting"))
	return INSTRUMENT if bool(root_scene.get("_full_hud")) else EXPLORE


func _reveals() -> int:
	return int(root_scene.call("GetAutoRevealCountForTesting")) if kind == "wb" else int(root_scene.get("auto_reveal_count"))


func _accepted() -> int:
	return int(ctrl.call("GetOperatorObservationAcceptedCountForTesting"))


func _pending() -> bool:
	return bool(root_scene.get("_pending_reveal"))


func _capture_in_progress() -> bool:
	return bool(root_scene.get("_capture_in_progress"))


func _mode() -> String:
	return str(ctrl.call("GetModeName"))


func _disposition() -> String:
	return str(ctrl.call("GetLastInputDisposition"))


func _generation() -> int:
	return int(film.get("CathedralSnapshotGeneration"))


func _mark() -> void:
	_marked = {"accepted": _accepted(), "reveals": _reveals(), "actions": actions.size()}


func _expect_reveal(label: String, action: String) -> void:
	cases += 1
	_check(_level() == INSTRUMENT, "%s revealed the film" % label)
	_check(_accepted() == int(_marked["accepted"]) + 1, "%s reported exactly one acceptance" % label)
	_check(_reveals() == int(_marked["reveals"]) + 1, "%s promoted exactly once" % label)
	_check(actions.size() == int(_marked["actions"]) + 1 and actions[-1] == action, "%s action was %s" % [label, action])


func _expect_idempotent(label: String, action: String) -> void:
	cases += 1
	_check(_level() == INSTRUMENT, "%s stayed in Instrument" % label)
	_check(_accepted() == int(_marked["accepted"]) + 1, "%s reported exactly one acceptance" % label)
	_check(_reveals() == int(_marked["reveals"]), "%s did not promote again" % label)
	_check(actions.size() > 0 and actions[-1] == action, "%s action was %s" % [label, action])


func _expect_unchanged(label: String, level: int) -> void:
	cases += 1
	_check(_level() == level, "%s kept level %d (got %d)" % [label, level, _level()])
	_check(_accepted() == int(_marked["accepted"]), "%s reported no acceptance" % label)
	_check(_reveals() == int(_marked["reveals"]), "%s did not promote" % label)


func _wait_sealed(label: String, require_explore := false) -> bool:
	var explore_held := true
	for _i in range(SEAL_TIMEOUT_FRAMES):
		if bool(film.get("ProbeViewAvailable")) and not bool(film.get("CathedralSnapshotLifecycleActive")):
			break
		if require_explore and _level() != EXPLORE:
			explore_held = false
		await process_frame
	acquisitions += 1
	_check(bool(film.get("ProbeViewAvailable")), "%s %s sealed" % [kind, label])
	await _frames(3)
	return explore_held


func _h() -> void:
	await _key(KEY_H)


func _key(keycode: Key, shift := false, control := false) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	event.shift_pressed = shift
	event.ctrl_pressed = control
	Input.parse_input_event(event)
	await _frames(3)
	var release := event.duplicate() as InputEventKey
	release.pressed = false
	Input.parse_input_event(release)
	await process_frame


func _queue(keycode: Key, shift := false) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	event.shift_pressed = shift
	Input.parse_input_event(event)
	var release := event.duplicate() as InputEventKey
	release.pressed = false
	Input.parse_input_event(release)


func _film_hash() -> String:
	var image := film.call("CopyFilmImageForTesting") as Image
	if image == null:
		return "none"
	var context := HashingContext.new()
	context.start(HashingContext.HASH_SHA256)
	context.update(image.get_data())
	return context.finish().hex_encode()


func _frames(count: int) -> void:
	for _i in range(count):
		await process_frame


func _check(condition: bool, message: String) -> void:
	if not condition:
		failures.append("%s: %s" % [kind, message])
