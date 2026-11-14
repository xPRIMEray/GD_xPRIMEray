extends SceneTree

const EXPLORE := 0
const INSTRUMENT := 1
const INSPECT := 2

var scene_root: Node
var workbench: Node
var chamber: Node
var film: Node
var controller: Node
var status_card: Control
var film_module: Control
var film_plate: Control
var telemetry_module: Control
var telemetry_backdrop: Control
var side_dock: Control
var tabs: TabContainer
var inspector: Node
var failures: Array[String] = []


func _initialize() -> void:
	_source_guardrails()
	scene_root = load("res://ObservatoryWorkbench.tscn").instantiate()
	get_root().add_child(scene_root)
	await _settle(12)
	workbench = scene_root
	chamber = scene_root.get_node("PlayableWorld/TransportChamberWorld")
	film = chamber.get_node("GrinFilmCamera")
	controller = chamber.get_node("FilmController")
	status_card = chamber.get_node("CanvasLayer/HUDRoot/InstrumentStatusCard")
	film_module = chamber.get_node("CanvasLayer/HUDRoot/FilmModule")
	film_plate = film_module.get_node("FilmPlate")
	telemetry_module = chamber.get_node("CanvasLayer/HUDRoot/TelemetryModule")
	telemetry_backdrop = chamber.get_node("CanvasLayer/HUDRoot/TelemetryBackdrop")
	side_dock = scene_root.get_node("UILayer/UIRoot/SideDock")
	tabs = side_dock.get_node("Tabs")
	inspector = tabs.get_node("Inspector")

	await _test_levels_and_input()
	await _test_authoritative_information()
	await _test_sealed_and_live_plate()
	await _test_demo_inspect_telemetry()
	await _test_terminal_discoverability()
	await _test_refresh_noninterference()
	await _test_layouts()
	_test_navigation_contract()

	if failures.is_empty():
		print("WORKBENCH_PRESENTATION_LEVELS PASS levels=3 refresh_fields=52")
		quit(0)
	else:
		for failure in failures:
			push_error("WORKBENCH_PRESENTATION_LEVELS FAIL: " + failure)
		print("WORKBENCH_PRESENTATION_LEVELS FAIL count=%d" % failures.size())
		quit(1)


func _source_guardrails() -> void:
	var source := FileAccess.get_file_as_string("res://UI/InstrumentStatusCard.gd")
	_assert(not source.is_empty(), "status-card source unavailable")
	for forbidden in ["SetProbeView", "SetMode", "set_mode", "RequestCathedral", "InvalidateCathedral", ".set("]:
		_assert(source.find(forbidden) < 0, "read-only card contains %s" % forbidden)
	for mapper_vocabulary in ["HitGeometry", "MaxStepsExhausted", "StoppedEarlyAbsorbed", "NumericalFailure"]:
		_assert(source.find(mapper_vocabulary) < 0, "status card duplicates legend vocabulary %s" % mapper_vocabulary)
	_assert(source.find("func _process") < 0, "status card introduced a process loop")
	_assert(source.find("REFRESH_SECONDS := 0.25") >= 0, "status card refresh cadence")


func _test_levels_and_input() -> void:
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == EXPLORE, "initial level is not Explore")
	_assert(not side_dock.visible and status_card.visible and not film_module.visible, "Explore visibility contract")
	_assert(not _camera_probe_hud_visible(), "Explore duplicated camera Probe HUD")
	_send_key(KEY_H)
	await _settle(3)
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSTRUMENT, "H did not enter Instrument")
	_assert(status_card.visible and film_module.visible and not side_dock.visible, "Instrument visibility contract")
	_send_key(KEY_ESCAPE)
	await _settle(3)
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSPECT, "Esc did not enter Inspect")
	_assert(side_dock.visible and film_module.visible, "Inspect must show SideDock and film")
	_assert(not _camera_probe_hud_visible(), "Inspect camera Probe HUD overlaps presentation")
	_send_key(KEY_ESCAPE)
	await _settle(3)
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSTRUMENT, "closing Inspect did not restore Instrument")

	var input := LineEdit.new()
	scene_root.add_child(input)
	input.grab_focus()
	await _settle(2)
	_send_key(KEY_H)
	await _settle(2)
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSTRUMENT, "H hijacked focused input")
	input.release_focus()
	input.queue_free()
	await _settle(2)


func _test_authoritative_information() -> void:
	status_card.call("ForceRefreshForTesting")
	var state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(str(state.get("mode")) == str(controller.call("GetModeName")), "G mode source mismatch")
	_assert(str(state.get("selected_profile_id")) == str(film.get("ProbeProfileStableId")), "selected P source mismatch")
	_assert(int(state.get("film_width")) == int(film.get("FilmWidthPixels")), "film width is not authoritative")
	_assert(int(state.get("film_height")) == int(film.get("FilmHeightRows")), "film height is not authoritative")
	_assert(str(state.get("disposition")) == str(controller.call("GetLastInputDisposition")), "input disposition mismatch")
	_assert(bool(state.get("field_structure_visible")) == bool(film.get("CurrentFieldStructureVisible")), "field visibility source mismatch")
	var field_before := bool(state.get("field_structure_visible"))
	_send_key(KEY_F)
	await _settle(4)
	status_card.call("ForceRefreshForTesting")
	state = status_card.call("GetDisplayedStateForTesting")
	_assert(bool(state.get("field_structure_visible")) == bool(film.get("CurrentFieldStructureVisible")), "updated field visibility mismatch")
	_assert(bool(state.get("field_structure_visible")) != field_before, "F did not update field visibility")
	_send_key(KEY_F)
	await _settle(4)


func _test_sealed_and_live_plate() -> void:
	film.call("SetProbeProfileByIndex", 0)
	_assert(bool(controller.call("EnterFormalSnapshotFromInput")), "Formal request rejected")
	film.call("SetProbeProfileByIndex", 1)
	await _settle(2)
	status_card.call("ForceRefreshForTesting")
	var active_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(bool(active_state.get("active")), "active Formal status absent")
	_assert(not film_plate.visible, "film plate visible before seal")
	var active_generation := int(film.get("CathedralSnapshotGeneration"))
	var request_profile := str(film.get("CathedralRequestedProbeProfileStableId"))
	_send_key(KEY_H)
	await _settle(2)
	_send_key(KEY_H)
	await _settle(2)
	_send_key(KEY_ESCAPE)
	await _settle(2)
	_send_key(KEY_ESCAPE)
	await _settle(2)
	_assert(int(film.get("CathedralSnapshotGeneration")) == active_generation, "presentation changes superseded active Formal generation")
	_assert(str(film.get("CathedralRequestedProbeProfileStableId")) == request_profile, "presentation changes relabeled request authority")
	await _wait_for_complete()
	var sealed_cpu_hash := _film_image_sha256()
	status_card.call("ForceRefreshForTesting")
	var sealed_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(bool(sealed_state.get("sealed")), "sealed evidence absent")
	_assert(str(sealed_state.get("selected_profile_id")) == "motion", "current P selection missing")
	_assert(str(sealed_state.get("sealed_profile_id")) == "cathedral", "sealed P inferred from current selection")
	_assert(str(sealed_state.get("q_view")) == str(film.get("CurrentProbeViewDisplayName")), "Q display getter mismatch")
	_assert(str(sealed_state.get("q_description")) == str(film.get("CurrentProbeViewDescription")), "Q description getter mismatch")
	_assert(str(sealed_state.get("q_legend")) == str(film.get("CurrentProbeViewLegend")), "Q legend getter mismatch")
	_assert(film_plate.visible and film_module.visible, "sealed plate not visible in Instrument")
	_send_key(KEY_H)
	await _settle(2)
	status_card.call("ForceRefreshForTesting")
	var explore_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(bool(explore_state.get("plate_hidden")), "Explore hidden-plate affordance missing")
	_assert(str(explore_state.get("status_text")).contains("Plate hidden"), "Explore hidden-plate message missing")
	_assert(_film_image_sha256() == sealed_cpu_hash, "Explore transition changed sealed CPU film")
	_send_key(KEY_H)
	await _settle(2)

	_send_key(KEY_ESCAPE)
	await _settle(3)
	_assert(side_dock.visible and film_module.visible and film_plate.visible, "sealed plate not visible in Inspect")
	_assert(_film_image_sha256() == sealed_cpu_hash, "Inspect transition changed sealed CPU film")
	_send_key(KEY_ESCAPE)
	await _settle(2)
	_assert(bool(controller.call("ToggleLiveFromInput")), "LIVE entry rejected")
	await _settle(4)
	_assert(str(controller.call("GetModeName")) == "LIVE", "LIVE mode unavailable")
	_assert(film_module.visible and film_plate.visible, "LIVE preview unavailable in Instrument")


func _test_demo_inspect_telemetry() -> void:
	_send_key(KEY_TAB)
	await _settle(2)
	_assert(telemetry_module.visible and telemetry_backdrop.visible, "advanced telemetry did not open in Instrument")
	_send_key(KEY_ESCAPE)
	await _settle(2)
	_assert(not telemetry_module.visible and not telemetry_backdrop.visible, "Inspect did not suppress telemetry")
	chamber.call("SetDemoPresentationMode", true)
	await _settle(2)
	_assert(not telemetry_module.visible and not telemetry_backdrop.visible, "demo enable restored telemetry over Inspect")
	chamber.call("SetDemoPresentationMode", false)
	await _settle(2)
	_assert(not telemetry_module.visible and not telemetry_backdrop.visible, "demo disable restored telemetry over Inspect")
	_send_key(KEY_ESCAPE)
	await _settle(2)
	_assert(telemetry_module.visible and telemetry_backdrop.visible, "closing Inspect did not restore permitted telemetry")
	_send_key(KEY_TAB)
	await _settle(2)


func _test_terminal_discoverability() -> void:
	_assert(bool(controller.call("ToggleLiveFromInput")), "LIVE to OFF rejected before invalidation fixture")
	await _settle(2)
	_assert(bool(controller.call("EnterFormalSnapshotFromInput")), "invalidation fixture request rejected")
	await _settle(2)
	controller.call("NotifyCameraTransformJump")
	await _settle(3)
	if int(workbench.call("GetPresentationLevelForTesting")) != EXPLORE:
		_send_key(KEY_H)
		await _settle(2)
	status_card.call("ForceRefreshForTesting")
	var state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(not bool(state.get("active")) and bool(state.get("terminal_relevant")), "real invalidation not classified as terminal-relevant")
	_assert(str(state.get("status_text")).contains("Formal last:"), "invalidated state undiscoverable in Explore")
	_assert(str(state.get("status_text")).contains("Last action:"), "historical disposition is not labeled as last action")
	_assert(bool(status_card.call("IsTerminalStateRelevantForTesting", "Failed", "QueryFailure")), "failed lifecycle classification")


func _test_refresh_noninterference() -> void:
	var before := _production_state_signature()
	var refresh_before := int(status_card.call("GetRefreshCountForTesting"))
	for _i in range(20):
		status_card.call("ForceRefreshForTesting")
	var after := _production_state_signature()
	_assert(before == after, "twenty status refreshes mutated production state")
	_assert(int(status_card.call("GetRefreshCountForTesting")) == refresh_before + 20, "refresh accounting")


func _test_layouts() -> void:
	if not bool(workbench.call("IsWorkbenchOpenForTesting")):
		_send_key(KEY_ESCAPE)
		await _settle(3)
	var actual_viewport := get_root().get_visible_rect()
	print("WORKBENCH_PRESENTATION_LAYOUT actual=%s dock=%s status=%s film=%s" % [
		actual_viewport, side_dock.get_global_rect(), status_card.get_global_rect(), film_module.get_global_rect()])
	_assert(actual_viewport.encloses(side_dock.get_global_rect()), "actual SideDock clipped")
	_assert(actual_viewport.encloses(status_card.get_global_rect()), "actual status card clipped")
	_assert(actual_viewport.encloses(film_module.get_global_rect()), "actual film module clipped")
	_assert(not side_dock.get_global_rect().intersects(film_module.get_global_rect()), "actual SideDock overlaps film")
	_assert(not side_dock.get_global_rect().intersects(status_card.get_global_rect()), "actual SideDock overlaps status")
	_assert(not status_card.get_global_rect().intersects(film_module.get_global_rect()), "actual status overlaps film")
	for size in [Vector2i(1152, 648), Vector2i(1920, 1080), Vector2i(720, 1280)]:
		var plan: Dictionary = workbench.call("GetLayoutPlanForTesting", Vector2(size))
		var viewport_rect := Rect2(Vector2.ZERO, Vector2(size))
		var dock: Rect2 = plan.get("dock")
		var card: Rect2 = plan.get("status")
		var plate: Rect2 = plan.get("film")
		print("WORKBENCH_PRESENTATION_PLAN size=%s dock=%s status=%s film=%s" % [size, dock, card, plate])
		_assert(viewport_rect.encloses(dock), "planned SideDock clipped at %s" % size)
		_assert(viewport_rect.encloses(card), "planned status card clipped at %s" % size)
		_assert(viewport_rect.encloses(plate), "planned film module clipped at %s" % size)
		_assert(not dock.intersects(plate), "planned SideDock overlaps film at %s" % size)
		_assert(not dock.intersects(card), "planned SideDock overlaps status at %s" % size)
		_assert(not card.intersects(plate), "planned status overlaps film at %s" % size)
	_assert(not _camera_probe_hud_visible(), "Probe HUD visible during layout checks")
	_send_key(KEY_ESCAPE)
	await _settle(3)


func _test_navigation_contract() -> void:
	_assert(tabs.get_tab_count() == 4, "scene navigation tab count changed")
	_assert(tabs.get_child(2).name == "Inspector", "Inspector tab index changed")
	_assert(tabs.get_child(3).name == "Scenes", "Scenes tab index changed")


func _production_state_signature() -> String:
	return JSON.stringify({
		"mode": str(controller.call("GetModeName")),
		"profile": str(film.get("ProbeProfileStableId")),
		"macro": int(film.get("RuntimeMacroSelectedMode")),
		"q_view": int(film.get("CurrentProbeView")),
		"generation": int(film.get("CathedralSnapshotGeneration")),
		"active": bool(film.get("CathedralSnapshotLifecycleActive")),
		"state": str(film.get("CathedralSnapshotLifecycleStateName")),
		"reason": str(film.get("CathedralSnapshotLifecycleReasonName")),
		"sealed": bool(film.get("ProbeViewAvailable")),
		"sealed_generation": int(film.get("SealedProbeViewGeneration")),
		"policy": str(film.get("CathedralFormalPolicySha256")),
		"broadphase_mode": int(film.get("EffectiveBroadphaseMode")),
		"broadphase_policy": int(film.get("EffectiveBroadphasePolicy")),
		"broadphase_quick": bool(film.get("EffectiveBroadphaseQuickRay")),
		"broadphase_overlap": bool(film.get("EffectiveBroadphaseOverlap")),
		"configuration": inspector.call("GetCurrentConfigurationForTesting"),
	})


func _camera_probe_hud_visible() -> bool:
	for child in film.get_children():
		if child is CanvasLayer and int(child.layer) == 12:
			return child.visible
	return false


func _film_image_sha256() -> String:
	var image := film.call("CopyFilmImageForTesting") as Image
	if image == null or image.is_empty():
		_fail("CPU film image unavailable")
		return ""
	var context := HashingContext.new()
	context.start(HashingContext.HASH_SHA256)
	context.update(image.get_data())
	return context.finish().hex_encode()


func _wait_for_complete() -> void:
	for _i in range(1200):
		await process_frame
		if bool(film.get("ProbeViewAvailable")):
			return
	_fail("Formal acquisition timeout")


func _send_key(code: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	event.echo = false
	Input.parse_input_event(event)


func _settle(frames: int) -> void:
	for _i in range(frames):
		await process_frame


func _assert(condition: bool, message: String) -> void:
	if not condition:
		_fail(message)


func _fail(message: String) -> void:
	failures.append(message)
