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
var max_card_height := 0.0
var min_film_clearance := INF
var layout_samples: Array[String] = []


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
	_test_formal_attention_table()
	await _test_authoritative_information()
	await _test_accepted_action_reveal()
	await _test_sealed_and_live_plate()
	await _test_demo_inspect_telemetry()
	await _test_terminal_discoverability()
	await _test_boundary_failure_discoverability()
	await _test_refresh_noninterference()
	await _test_layouts()
	_test_navigation_contract()

	if failures.is_empty():
		print("WORKBENCH_PRESENTATION_LEVELS PASS levels=3 refresh_fields=52 max_card_height=%.1f min_film_clearance=%.1f" % [max_card_height, min_film_clearance])
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
	_assert(source.find("IsComputeActive") < 0, "status card calls timer-advancing compute accessor")
	_assert(source.find("GetQualityName") < 0, "status card uses quality name as dimension authority")


func _test_formal_attention_table() -> void:
	var states := ["inactive", "requested", "waiting_for_physics", "capturing", "complete", "incomplete", "invalidated", "failed"]
	var modes := ["OFF", "LIVE", "SNAPSHOT"]
	var cases := 0
	for mode in modes:
		for active in [false, true]:
			for state in states:
				for sealed in [false, true]:
					var expected: bool = (
						mode == "SNAPSHOT"
						and not active
						and (state in ["incomplete", "invalidated", "failed"] or (state == "complete" and not sealed))
					)
					var actual := bool(status_card.call("IsFormalAttentionForTesting", mode, active, state, sealed))
					_assert(actual == expected, "Formal attention table mismatch mode=%s active=%s state=%s sealed=%s" % [mode, active, state, sealed])
					cases += 1
	_assert(cases == 96, "Formal attention classifier table is incomplete")


func _test_levels_and_input() -> void:
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == EXPLORE, "initial level is not Explore")
	_assert(not side_dock.visible and status_card.visible and not film_module.visible, "Explore visibility contract")
	status_card.call("ForceRefreshForTesting")
	_assert(not bool(status_card.call("GetDisplayedStateForTesting").get("secondary_controls_visible")), "Explore exposed secondary controls")
	_assert(not _camera_probe_hud_visible(), "Explore duplicated camera Probe HUD")
	_send_key(KEY_H)
	await _settle(3)
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSTRUMENT, "H did not enter Instrument")
	_assert(status_card.visible and film_module.visible and not side_dock.visible, "Instrument visibility contract")
	status_card.call("ForceRefreshForTesting")
	_assert(bool(status_card.call("GetDisplayedStateForTesting").get("secondary_controls_visible")), "Instrument omitted secondary controls")
	_send_key(KEY_ESCAPE)
	await _settle(3)
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSPECT, "Esc did not enter Inspect")
	_assert(side_dock.visible and film_module.visible, "Inspect must show SideDock and film")
	_assert(not _camera_probe_hud_visible(), "Inspect camera Probe HUD overlaps presentation")
	await _test_inspect_control_semantics()
	_send_key(KEY_ESCAPE)
	await _settle(3)
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSTRUMENT, "closing Inspect did not restore Instrument")
	status_card.call("ForceRefreshForTesting")
	var restored_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(bool(restored_state.get("input_enabled")) and str(restored_state.get("q_readiness")) == "ready", "closing Inspect did not restore Q readiness")
	_assert(str(restored_state.get("secondary_controls")).contains("[ / ] Opacity · H Level") and not str(restored_state.get("secondary_controls")).contains("Suspended"), "closing Inspect left the suspended-controls reference")

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


func _test_inspect_control_semantics() -> void:
	# UI-R2A.1: Inspect must not advertise disabled controls as available, and
	# the controls it names as active or suspended must behave accordingly.
	status_card.call("ForceRefreshForTesting")
	var state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	var status_text := str(state.get("status_text"))
	var secondary := str(state.get("secondary_controls"))
	_assert(not bool(state.get("input_enabled")), "Inspect did not suspend film input")
	_assert(bool(state.get("secondary_controls_visible")), "Inspect omitted the control-availability reference")
	for token in ["P Selected", "G Film:", "Q Formal:", "N Shading:", "F Field:"]:
		_assert(status_text.contains(token), "Inspect dropped primary indicator %s" % token)
	_assert(str(state.get("q_readiness")) == "suspended" and status_text.contains("Q Formal: suspended"), "Inspect presented Q Formal as ready")
	_assert(not status_text.contains("Q Formal: ready"), "Inspect retained Q Formal ready")
	_assert(secondary.contains("still active: P Profile · F Field"), "Inspect did not identify the controls that remain active")
	_assert(secondary.contains("Suspended until Esc:"), "Inspect did not identify suspended controls")
	for token in ["G", "Q", "N", "[ / ]", "H", "V", "WASD", "Mouse"]:
		_assert(secondary.get_slice("Suspended until Esc:", 1).contains(token), "Inspect suspended list missing %s" % token)
	for stale in ["[ / ] Opacity", "H Level", "Shift+Q Previous", "Ctrl+Q Reacquire", "V Walk/Fly", "WASD Move", "Mouse Look", "Shift Sprint"]:
		_assert(not secondary.contains(stale), "Inspect still advertises suspended control %s" % stale)
	var mode_before := str(controller.call("GetModeName"))
	var shading_before := int(film.get("ShadingMode"))
	var opacity_before := int(controller.call("GetOpacityPercent"))
	var generation_before := int(film.get("CathedralSnapshotGeneration"))
	var accepted_before := int(controller.call("GetOperatorObservationAcceptedCountForTesting"))
	var reveals_before := int(workbench.call("GetAutoRevealCountForTesting"))
	for code in [KEY_G, KEY_N, KEY_BRACKETLEFT, KEY_Q, KEY_H]:
		_send_key(code)
		await _settle(3)
	_assert(int(controller.call("GetOperatorObservationAcceptedCountForTesting")) == accepted_before, "suspended G/Q reported acceptance in Inspect")
	_assert(int(workbench.call("GetAutoRevealCountForTesting")) == reveals_before, "Inspect changed the closed presentation level")
	_assert(str(controller.call("GetModeName")) == mode_before, "suspended G acted in Inspect")
	_assert(int(film.get("ShadingMode")) == shading_before, "suspended N acted in Inspect")
	_assert(int(controller.call("GetOpacityPercent")) == opacity_before, "suspended [ acted in Inspect")
	_assert(int(film.get("CathedralSnapshotGeneration")) == generation_before, "suspended Q acted in Inspect")
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSPECT, "suspended H acted in Inspect")
	var profile_before := str(film.get("ProbeProfileStableId"))
	_send_key(KEY_P)
	await _settle(3)
	_assert(str(film.get("ProbeProfileStableId")) != profile_before, "P is advertised as active in Inspect but did not act")
	for _restore in range(4):
		if str(film.get("ProbeProfileStableId")) == profile_before:
			break
		_send_key(KEY_P)
		await _settle(3)
	_assert(str(film.get("ProbeProfileStableId")) == profile_before, "Inspect P profile was not restored")
	var field_before := bool(film.get("CurrentFieldStructureVisible"))
	_send_key(KEY_F)
	await _settle(3)
	_assert(bool(film.get("CurrentFieldStructureVisible")) != field_before, "F is advertised as active in Inspect but did not act")
	# FIELDVIZ-R0: F cycles Hidden -> Vectors -> Rays -> Hidden; two more presses restore Hidden.
	_send_key(KEY_F)
	await _settle(3)
	_assert(str(film.get("CurrentFieldStructureModeName")) == "Rays", "Inspect second F did not select Rays")
	_send_key(KEY_F)
	await _settle(3)
	_assert(bool(film.get("CurrentFieldStructureVisible")) == field_before, "Inspect F field visibility was not restored")


func _test_authoritative_information() -> void:
	status_card.call("ForceRefreshForTesting")
	var state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(str(state.get("mode")) == str(controller.call("GetModeName")), "G mode source mismatch")
	_assert(str(state.get("selected_profile_id")) == str(film.get("ProbeProfileStableId")), "selected P source mismatch")
	_assert(int(state.get("film_width")) == int(film.get("FilmWidthPixels")), "film width is not authoritative")
	_assert(int(state.get("film_height")) == int(film.get("FilmHeightRows")), "film height is not authoritative")
	_assert(str(state.get("disposition")) == str(controller.call("GetLastInputDisposition")), "input disposition mismatch")
	_assert(bool(state.get("field_structure_visible")) == bool(film.get("CurrentFieldStructureVisible")), "field visibility source mismatch")
	_assert(str(state.get("scene_label")) == "WORKBENCH", "scene-neutral title binding mismatch")
	_assert(str(state.get("shading")) == str(controller.call("GetShadingModeName")), "shading source mismatch")
	_assert(int(state.get("opacity_percent")) == int(controller.call("GetOpacityPercent")), "opacity source mismatch")
	var player := chamber.get_node("TransportChamberPlayer")
	_assert(bool(state.get("pose_held")) == bool(player.call("IsPoseHeld")), "pose-held source mismatch")
	var status_text := str(state.get("status_text"))
	_assert(status_text.contains("Film: %d×%d" % [int(film.get("FilmWidthPixels")), int(film.get("FilmHeightRows"))]), "actual film dimensions absent")
	_assert(status_text.contains("Opacity: —"), "OFF opacity is not shown as inactive")
	_assert(status_text.contains("N Shading: %s" % _display_shading(str(controller.call("GetShadingModeName")))), "authoritative N shading absent")
	_assert(status_text.contains("P Selected") and status_text.contains("G Film:") and status_text.contains("Q Formal:") and status_text.contains("F Field:"), "primary P/G/Q/N/F hierarchy incomplete")
	_assert(status_text.contains("Pose held: %s" % ("YES" if bool(player.call("IsPoseHeld")) else "NO")), "authoritative pose state absent")
	_assert(str(state.get("mode")) == "OFF" and str(state.get("q_readiness")) == "ready" and status_text.contains("Q Formal: ready"), "OFF did not present Q Formal as ready")
	var secondary := str(state.get("secondary_controls"))
	for token in ["[ / ] Opacity", "H Level", "Shift+Q Previous", "Ctrl+Q Reacquire", "V Walk/Fly", "WASD Move", "Mouse Look", "Shift Sprint"]:
		_assert(secondary.contains(token), "shared secondary reference missing %s" % token)
	var locomotion_text := str(chamber.get_node("CanvasLayer/HUDRoot/ObserverModule/ObserverStatusLabel").text)
	for token in ["Esc Inspect", "Tab Telemetry", "O Optical-only", "Field strength", "0 Straight", "1 Full", "F1/F2 Renderer diagnostics"]:
		_assert(locomotion_text.contains(token), "Workbench reference missing %s" % token)
	_sample_card_layout("off_instrument")
	var field_before := bool(state.get("field_structure_visible"))
	_send_key(KEY_F)
	await _settle(4)

	var initial_shading := str(controller.call("GetShadingModeName"))
	var observed_shading := {}
	for shading_index in range(5):
		status_card.call("ForceRefreshForTesting")
		state = status_card.call("GetDisplayedStateForTesting")
		var raw_shading := str(controller.call("GetShadingModeName"))
		observed_shading[raw_shading] = true
		_assert(str(state.get("shading")) == raw_shading, "raw shading authority mismatch")
		_assert(str(state.get("shading_display")) == _display_shading(raw_shading), "human-readable shading mismatch")
		_assert(str(state.get("status_text")).contains("N Shading: %s" % _display_shading(raw_shading)), "N shading value not visible")
		_sample_card_layout("off_shading_%d" % shading_index)
		_send_key(KEY_N)
		await _settle(2)
	_assert(observed_shading.size() == 5, "N did not expose all five shading values")
	_assert(str(controller.call("GetShadingModeName")) == initial_shading, "N cycle did not return to its initial value")

	_send_key(KEY_G)
	await _settle(4)
	_assert(str(controller.call("GetModeName")) == "LIVE", "G did not enter LIVE for opacity presentation checks")
	status_card.call("ForceRefreshForTesting")
	var live_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(str(live_state.get("q_readiness")) == "G OFF first" and str(live_state.get("status_text")).contains("Q Formal: G OFF first"), "LIVE presented Q Formal as ready")
	_assert(not str(live_state.get("status_text")).contains("Q Formal: ready"), "LIVE retained Q Formal ready")
	_send_key(KEY_Q)
	await _settle(3)
	_assert(str(controller.call("GetModeName")) == "LIVE" and str(controller.call("GetLastInputDisposition")) == "formal_from_live_refused_press_g_then_q", "Q in LIVE was not refused as the card states")
	var initial_opacity := int(controller.call("GetOpacityPercent"))
	for _step in range(12):
		_send_key(KEY_BRACKETLEFT)
	await _settle(2)
	status_card.call("ForceRefreshForTesting")
	_assert(int(controller.call("GetOpacityPercent")) == 0, "opacity lower extreme unavailable")
	_assert(str(status_card.call("GetDisplayedStateForTesting").get("status_text")).contains("Opacity: 0%"), "zero opacity not presented")
	_sample_card_layout("live_opacity_0")
	for _step in range(12):
		_send_key(KEY_BRACKETRIGHT)
	await _settle(2)
	status_card.call("ForceRefreshForTesting")
	_assert(int(controller.call("GetOpacityPercent")) == 100, "opacity upper extreme unavailable")
	_assert(str(status_card.call("GetDisplayedStateForTesting").get("status_text")).contains("Opacity: 100%"), "full opacity not presented")
	_sample_card_layout("live_opacity_100")
	for _step in range(int((100 - initial_opacity) / 10)):
		_send_key(KEY_BRACKETLEFT)
	await _settle(2)
	_assert(int(controller.call("GetOpacityPercent")) == initial_opacity, "opacity was not restored after extreme checks")
	_send_key(KEY_G)
	await _settle(4)
	_assert(str(controller.call("GetModeName")) == "OFF", "G did not restore OFF after opacity presentation checks")
	status_card.call("ForceRefreshForTesting")
	state = status_card.call("GetDisplayedStateForTesting")
	_assert(bool(state.get("field_structure_visible")) == bool(film.get("CurrentFieldStructureVisible")), "updated field visibility mismatch")
	_assert(bool(state.get("field_structure_visible")) != field_before, "F did not update field visibility")
	_send_key(KEY_F)
	await _settle(4)
	_send_key(KEY_F)
	await _settle(4)
	_assert(str(film.get("CurrentFieldStructureModeName")) == "Hidden", "F cycle did not return to Hidden")


func _test_accepted_action_reveal() -> void:
	# UI-R2B: accepted G starts reveal a hidden film once; Instrument is idempotent;
	# refusals and stopping LIVE never change the level; manual H stays authoritative.
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSTRUMENT and str(controller.call("GetModeName")) == "OFF", "reveal fixture did not start OFF in Instrument")
	var accepted_before := int(controller.call("GetOperatorObservationAcceptedCountForTesting"))
	var reveals_before := int(workbench.call("GetAutoRevealCountForTesting"))
	_send_key(KEY_G)
	await _settle(4)
	_assert(str(controller.call("GetModeName")) == "LIVE", "reveal fixture G did not start LIVE")
	_assert(int(controller.call("GetOperatorObservationAcceptedCountForTesting")) == accepted_before + 1, "accepted G in Instrument did not report once")
	_assert(int(workbench.call("GetAutoRevealCountForTesting")) == reveals_before, "accepted G promoted an already visible film")
	_send_key(KEY_G)
	await _settle(4)
	_send_key(KEY_H)
	await _settle(3)
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == EXPLORE and not film_module.visible, "H did not return to Explore before reveal")
	_send_key(KEY_G)
	await _settle(4)
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSTRUMENT and film_module.visible, "accepted G did not reveal the hidden film")
	_assert(int(workbench.call("GetAutoRevealCountForTesting")) == reveals_before + 1, "accepted G did not promote exactly once")
	_send_key(KEY_H)
	await _settle(3)
	var accepted_before_refusal := int(controller.call("GetOperatorObservationAcceptedCountForTesting"))
	_send_key(KEY_Q)
	await _settle(3)
	_assert(str(controller.call("GetLastInputDisposition")) == "formal_from_live_refused_press_g_then_q", "reveal fixture Q in LIVE was not refused")
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == EXPLORE, "refused Q revealed the film")
	_send_key(KEY_G)
	await _settle(4)
	_assert(str(controller.call("GetModeName")) == "OFF", "reveal fixture G did not stop LIVE")
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == EXPLORE, "stopping LIVE changed the presentation level")
	_assert(int(controller.call("GetOperatorObservationAcceptedCountForTesting")) == accepted_before_refusal, "refused Q or LIVE stop reported acceptance")
	_send_key(KEY_H)
	await _settle(3)
	_assert(int(workbench.call("GetPresentationLevelForTesting")) == INSTRUMENT, "reveal fixture did not restore Instrument")


func _test_sealed_and_live_plate() -> void:
	film.call("SetProbeProfileByIndex", 0)
	_assert(bool(controller.call("EnterFormalSnapshotFromInput")), "Formal request rejected")
	film.call("SetProbeProfileByIndex", 1)
	await _settle(2)
	status_card.call("ForceRefreshForTesting")
	var active_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(bool(active_state.get("active")), "active Formal status absent")
	_assert(bool(active_state.get("pose_held")) == bool(chamber.get_node("TransportChamberPlayer").call("IsPoseHeld")), "active Formal pose authority mismatch")
	_assert(not film_plate.visible, "film plate visible before seal")
	_sample_card_layout("active_formal")
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
	_assert(str(sealed_state.get("status_text")).contains("N LIVE shading:") and str(sealed_state.get("status_text")).contains("Q image unchanged"), "sealed SNAPSHOT does not distinguish LIVE shading from Q image")
	_assert(film_plate.visible and film_module.visible, "sealed plate not visible in Instrument")
	_sample_card_layout("sealed_instrument")
	_send_key(KEY_H)
	await _settle(2)
	status_card.call("ForceRefreshForTesting")
	var explore_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(bool(explore_state.get("plate_hidden")), "Explore hidden-plate affordance missing")
	_assert(film_plate.visible and not film_plate.is_visible_in_tree(), "hidden-plate affordance lacks actual visibility evidence")
	_assert(str(explore_state.get("status_text")).contains("Plate hidden"), "Explore hidden-plate message missing")
	_assert(_film_image_sha256() == sealed_cpu_hash, "Explore transition changed sealed CPU film")
	_send_key(KEY_H)
	await _settle(2)

	_send_key(KEY_ESCAPE)
	await _settle(3)
	_assert(side_dock.visible and film_module.visible and film_plate.visible, "sealed plate not visible in Inspect")
	_assert(_film_image_sha256() == sealed_cpu_hash, "Inspect transition changed sealed CPU film")
	_sample_card_layout("sealed_inspect_outcome")
	var q_views: Array[String] = [str(film.get("CurrentProbeViewDisplayName"))]
	for q_index in range(3):
		_send_key(KEY_ESCAPE)
		await _settle(2)
		_assert(bool(controller.call("HandleProbeViewInput", false, false)), "production Q cycle rejected")
		await _settle(3)
		_send_key(KEY_ESCAPE)
		await _settle(2)
		status_card.call("ForceRefreshForTesting")
		var q_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
		_assert(str(q_state.get("q_description")) == str(film.get("CurrentProbeViewDescription")), "cycled Q description mismatch")
		_assert(str(q_state.get("q_legend")) == str(film.get("CurrentProbeViewLegend")), "cycled Q legend mismatch")
		q_views.append(str(q_state.get("q_view")))
		_sample_card_layout("sealed_inspect_q%d" % q_index)
	_assert(q_views[0] == q_views[3], "three Q cycles did not return to initial view")
	_assert(q_views[0] != q_views[1] and q_views[1] != q_views[2] and q_views[0] != q_views[2], "all three Q views were not reached")
	_assert(_film_image_sha256() == sealed_cpu_hash, "full Q cycle changed initial sealed CPU film")
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
	_assert(bool(status_card.call("IsFormalAttentionForTesting", "SNAPSHOT", false, "failed", false)), "failed lifecycle classification")
	_assert(not bool(status_card.call("IsFormalAttentionForTesting", "LIVE", false, "invalidated", false)), "LIVE shows misleading Formal attention")
	_assert(not bool(status_card.call("IsFormalAttentionForTesting", "OFF", false, "invalidated", false)), "OFF shows misleading Formal attention")
	_sample_card_layout("invalidated_explore")


func _test_boundary_failure_discoverability() -> void:
	_assert(bool(controller.call("EnterFormalSnapshotFromInput")), "boundary mutation fixture request rejected")
	for _index in range(120):
		await process_frame
		if str(film.get("CathedralSnapshotLifecycleStateName")) == "capturing":
			break
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "capturing", "boundary mutation fixture did not capture")
	var boundary := chamber.get_node("GalleryBoundary")
	var original_ior := float(boundary.get("IOR"))
	boundary.set("IOR", original_ior + 0.125)
	for _index in range(1200):
		await process_frame
		if str(film.get("CathedralSnapshotLifecycleStateName")) in ["failed", "complete"]:
			break
	boundary.set("IOR", original_ior)
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "failed", "boundary mutation did not reach real failed state")
	_assert(str(film.get("CathedralSnapshotLifecycleReasonName")) == "boundary_epoch_changed", "boundary mutation failure reason mismatch")
	_assert(not bool(film.get("ProbeViewAvailable")), "failed boundary acquisition exposed sealed evidence")
	status_card.call("ForceRefreshForTesting")
	var failed_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(bool(failed_state.get("formal_attention")), "real failed acquisition is not discoverable")
	_assert(str(failed_state.get("status_text")).contains("Formal last: failed / boundary_epoch_changed"), "failed reason absent from card")
	_sample_card_layout("failed_explore")
	_assert(bool(controller.call("ToggleLiveFromInput")), "failed SNAPSHOT to LIVE release rejected")
	await _settle(3)
	status_card.call("ForceRefreshForTesting")
	var live_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(str(live_state.get("mode")) == "LIVE" and not bool(live_state.get("formal_attention")), "LIVE release shows stale Formal attention")
	_assert(bool(controller.call("ToggleLiveFromInput")), "LIVE to OFF release rejected")
	await _settle(3)
	status_card.call("ForceRefreshForTesting")
	var off_state: Dictionary = status_card.call("GetDisplayedStateForTesting")
	_assert(str(off_state.get("mode")) == "OFF" and not bool(off_state.get("formal_attention")), "OFF release shows stale Formal attention")


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
	var physical_window := DisplayServer.window_get_size()
	print("WORKBENCH_PRESENTATION_LAYOUT physical=%s logical=%s dock=%s status=%s film=%s" % [
		physical_window, actual_viewport, side_dock.get_global_rect(), status_card.get_global_rect(), film_module.get_global_rect()])
	_assert(actual_viewport.encloses(side_dock.get_global_rect()), "actual SideDock clipped")
	_assert(actual_viewport.encloses(status_card.get_global_rect()), "actual status card clipped")
	_assert(actual_viewport.encloses(film_module.get_global_rect()), "actual film module clipped")
	_assert(not side_dock.get_global_rect().intersects(film_module.get_global_rect()), "actual SideDock overlaps film")
	_assert(not side_dock.get_global_rect().intersects(status_card.get_global_rect()), "actual SideDock overlaps status")
	_assert(not status_card.get_global_rect().intersects(film_module.get_global_rect()), "actual status overlaps film")
	_assert(max_card_height <= 250.0, "status card exceeded qualified 250px envelope: %.1f" % max_card_height)
	_assert(min_film_clearance >= 12.0, "status-to-film clearance below 12px: %.1f" % min_film_clearance)
	print("WORKBENCH_PRESENTATION_SAMPLES max_card_height=%.1f min_film_clearance=%.1f samples=%s" % [max_card_height, min_film_clearance, ",".join(layout_samples)])
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


func _sample_card_layout(label: String) -> void:
	var card_rect := status_card.get_global_rect()
	var film_rect := film_module.get_global_rect()
	max_card_height = maxf(max_card_height, card_rect.size.y)
	var horizontal_overlap := card_rect.position.x < film_rect.end.x and film_rect.position.x < card_rect.end.x
	if horizontal_overlap:
		min_film_clearance = minf(min_film_clearance, film_rect.position.y - card_rect.end.y)
	layout_samples.append("%s:%.1f" % [label, card_rect.size.y])
	var capture_dir := OS.get_environment("XPRIMERAY_LAYOUT_CAPTURE_DIR")
	if not capture_dir.is_empty():
		DirAccess.make_dir_recursive_absolute(capture_dir)
		var physical := DisplayServer.window_get_size()
		var image := get_root().get_texture().get_image()
		var path := capture_dir.path_join("%dx%d_%s.png" % [physical.x, physical.y, label])
		_assert(not image.is_empty() and image.save_png(path) == OK, "layout capture failed: %s" % path)


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


func _display_shading(authoritative_name: String) -> String:
	return "RGB Normals" if authoritative_name == "Geometric Normal Debug" else authoritative_name


func _assert(condition: bool, message: String) -> void:
	if not condition:
		_fail(message)


func _fail(message: String) -> void:
	failures.append(message)
