extends SceneTree

# FIELDVIZ-R0 three-state field visualization: F cycles Hidden -> Vectors -> Rays -> Hidden.
# Presentation only. Rays are illustrative local single-source density rays, never per-pixel
# transport. Disabled physical fields are never drawn; unsupported shapes are attributed.

const WB := "res://ObservatoryWorkbench.tscn"
const GAL := "res://Experiences/SuspendedPortalGallery.tscn"
const MANAGED := ["DebugVizInGame", "DebugVizShowDensityVectors", "DebugVizShowDensityRays"]
# Read-only diagnostic text FieldSource3D fills in when it draws; not a physical property.
# DebugDrawInGame is FieldSource3D's legacy alias of the managed DebugVizInGame flag.
const DIAGNOSTIC_ONLY := ["DebugVizSummary", "DebugDrawInGame"]
const FIELD_PRESENTATION_LAYER_MASK := 1 << 19
const SEAL_TIMEOUT_FRAMES := 9000

var failures: Array[String] = []
var kind := ""
var root_scene: Node
var chamber: Node
var film: Node
var ctrl: Node
var card: Control
var cases := 0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	# --only-eligibility runs just the FIELDVIZ-R0.2 reconciliation section (focused before/after evidence).
	var only_eligibility := OS.get_cmdline_user_args().has("--only-eligibility")
	for scene_kind in ["gal", "wb"]:
		await _open(scene_kind)
		await _eligibility_reconciliation()
		await _open(scene_kind)
		await _eligibility_science()
		if only_eligibility:
			continue
		await _open(scene_kind)
		await _three_modes_and_restoration()
		await _authored_flag_restoration()
		await _mixed_state_rule()
		await _lifecycle_off_live_sealed()
		await _active_formal_parity()
		await _r2b_non_interference()
		if scene_kind == "wb":
			await _tab_o_inspect()
		await _open(scene_kind)
		await _disabled_and_unsupported()
	if not only_eligibility:
		await _scene_transitions()
	if failures.is_empty():
		print("FIELD_STRUCTURE_MODES PASS cases=%d server=%s" % [cases, DisplayServer.get_name()])
		quit(0)
	else:
		for failure in failures:
			push_error("FIELD_STRUCTURE_MODES FAIL: " + failure)
		print("FIELD_STRUCTURE_MODES FAIL count=%d cases=%d" % [failures.size(), cases])
		quit(1)


# ------------------------------------------------------------------ R0.2 eligibility reconciliation

# Invariant: a source with Enabled=false is never left drawn in any mode; a source that becomes
# enabled receives the current mode. Changes to Enabled here are test-local configuration.
func _eligibility_reconciliation() -> void:
	var authored := _flags_snapshot()
	var original_enabled := _enabled_snapshot()
	_check(_mode() == "Hidden", "eligibility: starts Hidden")
	for target in ["Vectors", "Rays"]:
		while _mode() != target:
			await _key(KEY_F)
		var a := _eligible_enabled_source()
		var other := _other_enabled_source(a)
		var reconciles := _reconciles()
		# 1. Disable an initially drawn source.
		_check(_is_drawn(a), "%s: %s initially drawn" % [target, a.name])
		a.set("Enabled", false)
		await _frames(2)
		cases += 1
		_check(not _is_drawn(a) and not bool(a.get("DebugVizInGame")), "%s: disabled %s no longer drawn" % [target, a.name])
		_check(_reconciles() == reconciles + 1, "%s: one reconciliation for one transition (%d -> %d)" % [target, reconciles, _reconciles()])
		_check_invariant("%s after disable" % target)
		# 2. Enable a previously disabled source (the authored-disabled one when present).
		var revived: Node = _initially_disabled_source(original_enabled)
		if revived == null:
			revived = a
		revived.set("Enabled", true)
		await _frames(2)
		cases += 1
		_check(_is_drawn(revived) and _flags_match_mode(revived, target), "%s: newly enabled %s receives the current mode" % [target, revived.name])
		_check_invariant("%s after enable" % target)
		# 3. Multiple changes within the same mode, including two sources in one frame.
		for step in range(4):
			a.set("Enabled", step % 2 == 0)
			await _frames(2)
			_check(_is_drawn(a) == bool(a.get("Enabled")), "%s: toggle %d keeps %s drawn==enabled" % [target, step, a.name])
			_check_invariant("%s toggle %d" % [target, step])
		if other != null:
			a.set("Enabled", false)
			other.set("Enabled", false)
			await _frames(2)
			_check(not _is_drawn(a) and not _is_drawn(other), "%s: two sources disabled in one frame are both hidden" % target)
			_check_invariant("%s two-source frame" % target)
			a.set("Enabled", true)
			other.set("Enabled", true)
			await _frames(2)
			_check(_is_drawn(a) and _is_drawn(other), "%s: two sources re-enabled in one frame are both drawn" % target)
		# Same-frame off/on is not a transition: nothing to reconcile, still drawn.
		var before_blip := _reconciles()
		a.set("Enabled", false)
		a.set("Enabled", true)
		await _frames(2)
		_check(_reconciles() == before_blip and _is_drawn(a), "%s: same-frame off/on causes no reconciliation" % target)
		# No continuous work: steady frames perform no reconciliation.
		var steady := _reconciles()
		await _frames(120)
		_check(_reconciles() == steady, "%s: 120 steady frames performed no reconciliation" % target)
		_restore_enabled(original_enabled)
		await _frames(2)
		_check_invariant("%s restored eligibility" % target)
		print("FIELD_STRUCTURE_MODES %s eligibility mode=%s reconciliations=%d %s" % [kind, target, _reconciles(), _counts(_presentation())])
	while _mode() != "Hidden":
		await _key(KEY_F)
	cases += 1
	_check(_flags_snapshot() == authored, "eligibility: Hidden restored every authored flag exactly")
	_check(int(_presentation().get("tracked_sources", 0)) == 0, "eligibility: Hidden tracks no sources")
	if kind == "wb":
		await _experiment_switch_paths()


# The production programmatic path: the field dial switches experiments by flipping Enabled.
func _experiment_switch_paths() -> void:
	var dial := chamber.get_node("FieldDialController")
	var authored := _flags_snapshot()
	for target in ["Vectors", "Rays"]:
		while _mode() != target:
			await _key(KEY_F)
		dial.call("SetDisplayPresetForTesting", "Hermetic")
		await _frames(4)
		cases += 1
		_check_invariant("dial Hermetic in %s" % target)
		_check(_mode() == target and _card_mode() == target, "dial Hermetic keeps %s consistent (got %s)" % [target, _mode()])
		_check(int(_presentation().get("drawn")) == _enabled_eligible_count(), "dial Hermetic draws exactly the enabled sources in %s" % target)
		print("FIELD_STRUCTURE_MODES wb dial_hermetic mode=%s %s" % [target, _counts(_presentation())])
		dial.call("SetDisplayPresetForTesting", "Gallery")
		await _frames(4)
		_check_invariant("dial Gallery in %s" % target)
		_check(_mode() == target, "dial Gallery keeps %s consistent" % target)
	while _mode() != "Hidden":
		await _key(KEY_F)
	_check(_flags_snapshot() == authored, "dial paths: Hidden restored authored flags")


# Reconciliation must not change scientific evidence: the same eligibility changes made with F
# Hidden (no reconciliation) and with F Vectors (reconciliation) must leave identical evidence.
func _eligibility_science() -> void:
	var control := await _eligibility_evidence(false)
	var reconciled := await _eligibility_evidence(true)
	cases += 1
	_check(control == reconciled, "eligibility changes leave LIVE and sealed evidence identical to the Hidden control (control=%s reconciled=%s)" % [control, reconciled])
	print("FIELD_STRUCTURE_MODES %s eligibility_evidence=%s" % [kind, JSON.stringify(reconciled)])


func _eligibility_evidence(with_vectors: bool) -> Dictionary:
	film.call("ResetFieldStructureToHidden")
	ctrl.call("set_mode", 0)
	await _frames(2)
	if with_vectors:
		await _key(KEY_F)
	var a := _eligible_enabled_source()
	var result := {}
	# LIVE: toggle eligibility off and back on during LIVE.
	await _key(KEY_G)
	await _frames(20)
	var live_before := _formal_sig()
	a.set("Enabled", false)
	await _frames(6)
	a.set("Enabled", true)
	await _frames(6)
	var live_after := _formal_sig()
	result["live_mode"] = str(ctrl.call("GetModeName"))
	result["live_generation_delta"] = int(live_after["gen"]) - int(live_before["gen"])
	result["live_reset_delta"] = int(live_after["reset"]) - int(live_before["reset"])
	result["live_replay_delta"] = int(live_after["replay"]) - int(live_before["replay"])
	result["live_policy_same"] = live_after["policy"] == live_before["policy"]
	await _key(KEY_G)
	# Sealed: toggle eligibility off and back on with sealed evidence present.
	ctrl.call("EnterFormalSnapshotFromInput")
	await _wait_sealed()
	var sealed_before := _formal_sig_with_image()
	a.set("Enabled", false)
	await _frames(6)
	a.set("Enabled", true)
	await _frames(6)
	var sealed_after := _formal_sig_with_image()
	result["sealed_after"] = bool(sealed_after["sealed"])
	result["sealed_generation_delta"] = int(sealed_after["sgen"]) - int(sealed_before["sgen"])
	result["sealed_reset_delta"] = int(sealed_after["reset"]) - int(sealed_before["reset"])
	result["sealed_replay_delta"] = int(sealed_after["replay"]) - int(sealed_before["replay"])
	result["sealed_film_same"] = sealed_after["film"] == sealed_before["film"]
	result["sealed_query"] = str(sealed_before["query"])
	result["sealed_contact"] = str(sealed_before["contact"])
	ctrl.call("set_mode", 0)
	film.call("ResetFieldStructureToHidden")
	await _frames(2)
	return result


func _check_invariant(label: String) -> void:
	for source in _eligible_sources():
		if not bool(source.get("Enabled")):
			_check(not _is_drawn(source), "%s: disabled %s is not drawn" % [label, source.name])
	var presentation := _presentation()
	_check(int(presentation.get("disabled_drawn")) == 0, "%s: no disabled source drawn" % label)
	_check(bool(presentation.get("consistent")), "%s: presentation consistent with the mode" % label)


# Drawn by FIELDVIZ: the source's own diagnostic mesh is shown. Ancestor visibility is excluded on
# purpose (HermeticRoomDisplay is hidden by the Gallery experiment independently of F).
func _is_drawn(source: Node) -> bool:
	var visual := source.get_node_or_null("_FieldSourceAcademicViz") as MeshInstance3D
	return bool(source.get("DebugVizInGame")) and visual != null and visual.visible


func _flags_match_mode(source: Node, mode_name: String) -> bool:
	return bool(source.get("DebugVizInGame")) and bool(source.get("DebugVizShowDensityVectors")) == (mode_name == "Vectors") and bool(source.get("DebugVizShowDensityRays")) == (mode_name == "Rays")


func _reconciles() -> int:
	return int(_presentation().get("eligibility_reconciliations", -1))


func _enabled_snapshot() -> Dictionary:
	var values := {}
	for source in _sources():
		values[source.get_instance_id()] = bool(source.get("Enabled"))
	return values


func _restore_enabled(values: Dictionary) -> void:
	for source in _sources():
		if values.has(source.get_instance_id()):
			source.set("Enabled", values[source.get_instance_id()])


func _initially_disabled_source(values: Dictionary) -> Node:
	for source in _eligible_sources():
		if values.has(source.get_instance_id()) and not bool(values[source.get_instance_id()]):
			return source
	return null


func _other_enabled_source(first: Node) -> Node:
	for source in _eligible_sources():
		if source != first and bool(source.get("Enabled")):
			return source
	return null


# ------------------------------------------------------------------ modes

func _three_modes_and_restoration() -> void:
	var authored := _flags_snapshot()
	var physics := _physics_signature()
	_check(_mode() == "Hidden" and _drawn() == 0 and not bool(film.get("CurrentFieldStructureVisible")), "fresh scene starts Hidden with nothing drawn")
	_check(int(_presentation().get("authored_snapshot_count")) == 0, "Hidden holds no authored snapshot")
	await _measure("Hidden")
	var expected_names := ["Vectors", "Rays", "Hidden"]
	for name in expected_names:
		await _key(KEY_F)
		await _frames(6)
		cases += 1
		_check(_mode() == name, "F selected %s (got %s)" % [name, _mode()])
		_check(bool(_presentation().get("consistent")), "%s presentation consistent" % name)
		_check(_card_mode() == name, "card reports F Field: %s" % name)
		_check(_physics_signature() == physics, "%s left every physical field property unchanged%s" % [name, _physics_diff(physics)])
		for source in _sources():
			if not bool(source.get("DebugVizEnabled")):
				continue
			var enabled := bool(source.get("Enabled"))
			var drawn := bool(source.get("DebugVizInGame"))
			var visual := source.get_node_or_null("_FieldSourceAcademicViz") as MeshInstance3D
			if name == "Hidden":
				continue
			_check(drawn == enabled, "%s %s drawn only when its physical field is enabled" % [name, source.name])
			_check(bool(source.get("DebugVizShowDensityVectors")) == (name == "Vectors"), "%s %s vector flag" % [name, source.name])
			_check(bool(source.get("DebugVizShowDensityRays")) == (name == "Rays"), "%s %s ray flag" % [name, source.name])
			if enabled:
				_check(visual != null and visual.is_visible_in_tree() and visual.layers == FIELD_PRESENTATION_LAYER_MASK, "%s %s mesh visible on the presentation layer" % [name, source.name])
			else:
				_check(visual == null or not visual.is_visible_in_tree(), "%s disabled %s is not drawn" % [name, source.name])
		var presentation := _presentation()
		var disabled_count := _disabled_eligible_count()
		if name != "Hidden":
			_check(int(presentation.get("disabled_not_drawn")) == disabled_count and int(presentation.get("disabled_drawn")) == 0, "%s attributes %d disabled field(s) as not drawn" % [name, disabled_count])
			_check(bool(film.get("CurrentFieldStructureVisible")) == (_enabled_eligible_count() > 0), "%s compatibility visibility" % name)
		if name == "Rays":
			_check(int(presentation.get("rays_drawn")) == _enabled_eligible_count() and int(presentation.get("rays_unsupported")) == 0, "Rays drawn on every enabled SphereRadial source")
		if name == "Vectors":
			_check(int(presentation.get("vectors_drawn")) == _enabled_eligible_count(), "Vectors drawn on every enabled source")
		await _check_scene_text(name)
		await _measure(name)
	_check(_flags_snapshot() == authored, "Hidden restored every authored flag exactly")
	_check(_drawn() == 0 and int(_presentation().get("authored_snapshot_count")) == 0, "Hidden draws nothing and releases the snapshot")


func _check_scene_text(name: String) -> void:
	if kind == "gal":
		var full := bool(root_scene.get("_full_hud"))
		if not full:
			await _key(KEY_H)
		var truth := str((root_scene.get("_truth_label") as Label).text)
		var expected := {"Hidden": "hidden (field physics remains active)", "Vectors": "outer / inner limits · density direction", "Rays": "outer / inner limits · illustrative density rays"}
		_check(truth.contains("Field: " + str(expected[name])), "Gallery truth text reports %s" % name)
		_check(_line_fits(root_scene.get("_truth_label") as Label, "Field: "), "Gallery %s truth field line does not wrap" % name)
		_check(str(root_scene.call("get_presentation_diagnostics").get("field_mode")) == name, "Gallery diagnostics field_mode %s" % name)
		if not full:
			await _key(KEY_H)
	else:
		# Workbench: details ride on the card's F line only when they fit; the card never grows.
		card.call("ForceRefreshForTesting")
		var state: Dictionary = card.call("GetDisplayedStateForTesting")
		var details := str(state.get("field_structure_details"))
		var first_line := str(state.get("status_text")).split("
")[0]
		var status := card.get_node("Margin/Content/Status") as Label
		_check(status.get_line_count() == str(state.get("status_text")).split("
").size(), "Workbench card lines do not wrap in %s" % name)
		if name == "Hidden":
			_check(details.is_empty(), "Workbench card has no field details while Hidden")
		else:
			_check(first_line.ends_with("F Field: %s%s" % [name, details]), "Workbench card F line carries the fitted details in %s" % name)
			if _disabled_eligible_count() > 0:
				_check(details.contains("%d disabled not drawn" % _disabled_eligible_count()), "Workbench card attributes disabled field in %s (%s)" % [name, details])
		if name == "Rays":
			_check(details.contains("illustrative"), "Workbench card labels rays illustrative (%s)" % details)
		var observer := chamber.get_node("CanvasLayer/HUDRoot/ObserverModule/ObserverStatusLabel") as Label
		_check(not observer.text.contains("F Vectors") and not observer.text.contains("F Rays"), "observer module text unchanged in %s" % name)


func _authored_flag_restoration() -> void:
	# Simulate non-default authoring on one eligible source; Hidden must restore it exactly.
	var source := _eligible_enabled_source()
	source.set("DebugVizShowDensityVectors", false)
	source.set("DebugVizShowDensityRays", true)
	var authored := _flags_snapshot()
	for _i in range(3):
		await _key(KEY_F)
	cases += 1
	_check(_mode() == "Hidden" and _flags_snapshot() == authored, "non-default authored flags restored after a full cycle")
	await _key(KEY_F)
	await _key(KEY_F)
	ctrl.call("set_mode", 0)
	film.call("ResetFieldStructureToHidden")
	await _frames(3)
	_check(_flags_snapshot() == authored, "ResetFieldStructureToHidden restores authored flags from Rays")
	source.set("DebugVizShowDensityVectors", true)
	source.set("DebugVizShowDensityRays", false)


func _mixed_state_rule() -> void:
	var authored := _flags_snapshot()
	await _key(KEY_F)
	var source := _eligible_enabled_source()
	source.set("DebugVizInGame", false)  # an external writer breaks the Vectors presentation
	await _frames(2)
	cases += 1
	_check(_mode() == "Mixed" and not bool(_presentation().get("consistent")), "an external writer is reported as Mixed")
	_check(_card_mode() == "Mixed", "card reports Mixed truthfully")
	await _key(KEY_F)
	_check(_mode() == "Hidden" and _flags_snapshot() == authored, "F from Mixed normalizes to Hidden with authored flags")


# ------------------------------------------------------------------ lifecycle

func _lifecycle_off_live_sealed() -> void:
	# LIVE: F never restarts or invalidates.
	ctrl.call("set_mode", 0)
	await _key(KEY_G)
	await _frames(20)
	var live_sig := _formal_sig()
	var rows_before := int(film.get("FilmRowCursor"))
	for _i in range(3):
		await _key(KEY_F)
		await _frames(4)
		_check(str(ctrl.call("GetModeName")) == "LIVE", "LIVE survives F (%s)" % _mode())
	cases += 1
	_check(_formal_sig() == live_sig, "LIVE F cycle left generation, replay, reset and policy unchanged")
	_check(int(film.get("FilmRowCursor")) != rows_before or rows_before > 0, "LIVE kept progressing")
	await _key(KEY_G)

	# Sealed: F never changes sealed evidence.
	ctrl.call("EnterFormalSnapshotFromInput")
	await _wait_sealed()
	var sealed := _formal_sig_with_image()
	for name in ["Vectors", "Rays", "Hidden"]:
		await _key(KEY_F)
		await _frames(4)
		_check(_formal_sig_with_image() == sealed, "sealed evidence unchanged in %s" % name)
	cases += 1
	ctrl.call("set_mode", 0)
	await _frames(3)


func _active_formal_parity() -> void:
	var control := await _acquire(false)
	var cycled := await _acquire(true)
	cases += 1
	_check(control == cycled, "F cycling every frame during active Formal left evidence identical (control=%s cycled=%s)" % [control, cycled])
	print("FIELD_STRUCTURE_MODES %s active_formal_signature=%s" % [kind, JSON.stringify(control)])
	film.call("ResetFieldStructureToHidden")
	await _frames(2)


func _acquire(cycle_f: bool) -> Dictionary:
	ctrl.call("set_mode", 0)
	film.call("ResetFieldStructureToHidden")
	await _frames(2)
	var generation := int(film.get("CathedralSnapshotGeneration"))
	var reset := int(film.get("FormalWorkingStateResetCount"))
	ctrl.call("EnterFormalSnapshotFromInput")
	var frames := 0
	var presses_while_active := 0
	while frames < SEAL_TIMEOUT_FRAMES:
		if frames > 0 and bool(film.get("ProbeViewAvailable")) and not bool(film.get("CathedralSnapshotLifecycleActive")):
			break
		if cycle_f:
			if bool(film.get("CathedralSnapshotLifecycleActive")):
				presses_while_active += 1
			_queue(KEY_F)
		await process_frame
		frames += 1
	await _frames(4)
	if cycle_f:
		_check(presses_while_active >= 1, "F pressed while Formal active (%d presses, %d frames)" % [presses_while_active, frames])
	var signature := {
		"generation_delta": int(film.get("CathedralSnapshotGeneration")) - generation,
		"reset_delta": int(film.get("FormalWorkingStateResetCount")) - reset,
		"policy": str(film.get("CathedralFormalPolicySha256")),
		"query": str(film.get("CathedralProbeQueryDatasetSha256")),
		"contact": str(film.get("CathedralSpatialKernelContactCountSha256")),
		"lifecycle": str(film.get("CathedralSnapshotLifecycleStateName")),
		"film": _film_hash(),
	}
	ctrl.call("set_mode", 0)
	await _frames(2)
	return signature


func _r2b_non_interference() -> void:
	# F is not an accepted observation action and never changes the presentation level.
	film.call("ResetFieldStructureToHidden")
	ctrl.call("set_mode", 0)
	await _frames(2)
	while _level() != 0:
		await _key(KEY_H)
	var accepted := int(ctrl.call("GetOperatorObservationAcceptedCountForTesting"))
	for _i in range(3):
		await _key(KEY_F)
		_check(_level() == 0, "F did not reveal or change the level")
	_check(int(ctrl.call("GetOperatorObservationAcceptedCountForTesting")) == accepted, "F reported no accepted observation action")
	await _key(KEY_F)
	await _key(KEY_F)
	await _key(KEY_G)
	cases += 1
	_check(_level() == 1 and _mode() == "Rays", "accepted G still reveals while field Rays stays selected")
	await _key(KEY_G)
	await _key(KEY_H)
	film.call("ResetFieldStructureToHidden")
	await _frames(2)


# ------------------------------------------------------------------ Workbench Tab / O / Inspect

func _tab_o_inspect() -> void:
	var authored := _flags_snapshot()
	# Tab desync regression: Tab hides through the camera; card and sources agree.
	await _key(KEY_F)
	_check(_mode() == "Vectors", "Tab fixture starts in Vectors")
	await _key(KEY_TAB)
	await _frames(3)
	cases += 1
	_check(_mode() == "Hidden" and _card_mode() == "Hidden" and _drawn() == 0, "Tab hides every field and the card agrees (no desync)")
	_check(not bool(film.get("CurrentFieldStructureVisible")), "compatibility visibility agrees after Tab")
	_check(_flags_snapshot() == authored, "Tab restores authored flags")
	await _key(KEY_TAB)
	await _frames(3)
	_check(_mode() == "Hidden", "closing telemetry keeps Hidden")

	# O with each mode: the cull mask and the mode stay independent.
	var camera := (chamber.get_node("TransportChamberPlayer") as Node).call("get_camera") as Camera3D
	var normal_mask := camera.cull_mask
	await _key(KEY_O)
	_check(camera.cull_mask == FIELD_PRESENTATION_LAYER_MASK and _mode() == "Hidden", "O selects the presentation layer without touching F")
	for name in ["Vectors", "Rays", "Hidden"]:
		await _key(KEY_F)
		await _frames(4)
		_check(_mode() == name and camera.cull_mask == FIELD_PRESENTATION_LAYER_MASK, "O + F %s" % name)
		var visible_count := 0
		for source in _sources():
			var visual := source.get_node_or_null("_FieldSourceAcademicViz") as MeshInstance3D
			if visual != null and visual.is_visible_in_tree() and (camera.cull_mask & visual.layers) != 0:
				visible_count += 1
		_check(visible_count == (0 if name == "Hidden" else _enabled_eligible_count()), "O + F %s camera-visible sources %d" % [name, visible_count])
	await _key(KEY_O)
	_check(camera.cull_mask == normal_mask, "O restores the normal mask")
	cases += 1

	# Inspect: F remains available (camera-owned), card reports it, closing keeps it.
	await _key(KEY_ESCAPE)
	_check(int(root_scene.call("GetPresentationLevelForTesting")) == 2, "Esc opened Inspect")
	for name in ["Vectors", "Rays"]:
		await _key(KEY_F)
		_check(_mode() == name and _card_mode() == name, "F %s available and reported in Inspect" % name)
	await _key(KEY_ESCAPE)
	_check(_mode() == "Rays", "closing Inspect keeps the selected F mode")
	await _key(KEY_F)
	_check(_mode() == "Hidden" and _flags_snapshot() == authored, "Inspect round trip restores authored flags on Hidden")
	cases += 1


# ------------------------------------------------------------------ disabled and unsupported

func _disabled_and_unsupported() -> void:
	# Test-local configurations on a freshly loaded scene (presentation attribution only).
	var sources := _eligible_sources()
	var disabled: Node = sources[0]
	var box: Node = sources[1] if sources.size() > 1 else null
	disabled.set("Enabled", false)
	if box != null and box != disabled and bool(box.get("Enabled")):
		box.set("ShapeType", 1)  # BoxVolume: extents and vectors only; no density rays
	else:
		box = null
	await _frames(3)
	var authored := _flags_snapshot()
	await _key(KEY_F)
	var vectors := _presentation()
	await _key(KEY_F)
	var rays := _presentation()
	cases += 1
	_check(not bool(disabled.get("DebugVizInGame")), "disabled physical field is never drawn")
	_check(int(vectors.get("disabled_not_drawn")) == _disabled_eligible_count() and int(rays.get("disabled_not_drawn")) == _disabled_eligible_count(), "disabled fields attributed in Vectors and Rays")
	_check(int(rays.get("disabled_drawn")) == 0, "no disabled field drawn")
	if box != null:
		_check(int(rays.get("rays_unsupported")) == 1, "BoxVolume source attributed rays_unsupported in Rays")
		_check(not _source_report(rays, box.name).get("rays_supported", true), "BoxVolume source reports rays_supported=false")
	if kind == "wb":
		card.call("ForceRefreshForTesting")
		var details := str(card.call("GetDisplayedStateForTesting").get("field_structure_details"))
		# Greedy fit: the card keeps the highest-priority parts that fit; the API always attributes all.
		_check(details.contains("illustrative") and details.contains("disabled not drawn"), "Workbench card keeps illustrative + disabled attribution (%s)" % details)
		print("FIELD_STRUCTURE_MODES wb synthetic_card_details=%s" % details)
		if box != null:
			_check(int(rays.get("rays_unsupported")) == 1, "camera presentation attributes the unsupported shape")
	else:
		await _key(KEY_H)
		var truth := str((root_scene.get("_truth_label") as Label).text)
		_check(truth.contains("disabled not drawn"), "Gallery truth attributes the disabled field")
		if box != null:
			_check(truth.contains("unsupported shape"), "Gallery truth attributes the unsupported shape")
		await _key(KEY_H)
	await _key(KEY_F)
	_check(_mode() == "Hidden" and _flags_snapshot() == authored, "disabled/unsupported configuration restored on Hidden")
	print("FIELD_STRUCTURE_MODES %s disabled_unsupported vectors=%s rays=%s" % [kind, _counts(vectors), _counts(rays)])


# ------------------------------------------------------------------ scene transitions

func _scene_transitions() -> void:
	for scene_kind in ["gal", "wb", "gal", "wb"]:
		await _open(scene_kind)
		cases += 1
		_check(_mode() == "Hidden" and _drawn() == 0 and int(_presentation().get("authored_snapshot_count")) == 0, "%s transition begins Hidden" % scene_kind)
		await _key(KEY_F)
		await _key(KEY_F)
		_check(_mode() == "Rays", "%s reaches Rays before leaving" % scene_kind)


# ------------------------------------------------------------------ measurement

func _measure(name: String) -> void:
	var parts: Array[String] = []
	var total := 0
	for source in _sources():
		var visual := source.get_node_or_null("_FieldSourceAcademicViz") as MeshInstance3D
		if visual == null or visual.mesh == null:
			parts.append("%s:none" % source.name)
			continue
		var vertices := 0
		for surface in range(visual.mesh.get_surface_count()):
			vertices += (visual.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX] as PackedVector3Array).size()
		if visual.is_visible_in_tree():
			total += vertices
		parts.append("%s:vis=%s surf=%d verts=%d" % [source.name, visual.is_visible_in_tree(), visual.mesh.get_surface_count(), vertices])
	var start := Time.get_ticks_usec()
	for _i in range(60):
		await process_frame
	var mean_ms := (Time.get_ticks_usec() - start) / 60000.0
	print("FIELD_STRUCTURE_MODES %s mode=%s drawn_vertices=%d mean_frame_ms=%.3f [%s]" % [kind, name, total, mean_ms, " | ".join(parts)])


# ------------------------------------------------------------------ helpers

func _open(scene_kind: String) -> void:
	kind = scene_kind
	change_scene_to_file(WB if scene_kind == "wb" else GAL)
	await scene_changed
	await _frames(30)
	root_scene = current_scene
	film = root_scene.find_children("GrinFilmCamera", "", true, false)[0]
	chamber = film.get_parent()
	ctrl = chamber.get_node("FilmController")
	card = chamber.get_node("CanvasLayer/HUDRoot/InstrumentStatusCard")


func _sources() -> Array:
	return get_nodes_in_group("field_sources")


func _eligible_sources() -> Array:
	return _sources().filter(func(s): return bool(s.get("DebugVizEnabled")))


func _eligible_enabled_source() -> Node:
	for source in _eligible_sources():
		if bool(source.get("Enabled")):
			return source
	return null


func _enabled_eligible_count() -> int:
	return _eligible_sources().filter(func(s): return bool(s.get("Enabled"))).size()


func _disabled_eligible_count() -> int:
	return _eligible_sources().filter(func(s): return not bool(s.get("Enabled"))).size()


func _drawn() -> int:
	return int(_presentation().get("drawn"))


func _presentation() -> Dictionary:
	return film.call("GetFieldStructurePresentation")


func _source_report(presentation: Dictionary, source_name: String) -> Dictionary:
	for report in presentation.get("sources", []):
		if str(report.get("name")) == str(source_name):
			return report
	return {}


func _counts(presentation: Dictionary) -> String:
	var keys := ["display_name", "drawn", "disabled_not_drawn", "vectors_drawn", "rays_drawn", "vectors_unsupported", "rays_unsupported"]
	var parts: Array[String] = []
	for key in keys:
		parts.append("%s=%s" % [key, presentation.get(key)])
	return ",".join(parts)


func _mode() -> String:
	return str(film.get("CurrentFieldStructureModeName"))


func _card_mode() -> String:
	card.call("ForceRefreshForTesting")
	var state: Dictionary = card.call("GetDisplayedStateForTesting")
	var text := str(state.get("status_text"))
	var mode := str(state.get("field_structure_mode"))
	if not text.contains("F Field: %s" % mode):
		return "card-text-mismatch"
	return mode


func _level() -> int:
	if kind == "wb":
		return int(root_scene.call("GetPresentationLevelForTesting"))
	return 1 if bool(root_scene.get("_full_hud")) else 0


func _flags_snapshot() -> Array:
	var rows := []
	for source in _sources():
		var row := [str(source.get_path()), source.get("DebugVizEnabled"), source.get("Enabled")]
		for property in MANAGED:
			row.append(source.get(property))
		rows.append(row)
	return rows


func _physics_signature() -> Array:
	var rows := []
	for source in _sources():
		# Keyed by name: FieldSource3D toggles inspector usage flags with the ray/vector flags,
		# so positional property lists shift. Every exported (PascalCase) property is compared.
		var values := {"path": str(source.get_path()), "visible": source.visible, "transform": source.global_transform}
		for property in source.get_property_list():
			var name := str(property["name"])
			if name in MANAGED or name in DIAGNOSTIC_ONLY or name.is_empty() or name[0] != name[0].to_upper() or name[0] == name[0].to_lower():
				continue
			values[name] = str(source.get(name))
		rows.append(values)
	return rows


func _physics_diff(before: Array) -> String:
	var after := _physics_signature()
	if after == before:
		return ""
	var diffs: Array[String] = []
	for i in range(mini(before.size(), after.size())):
		var a: Dictionary = before[i]
		var b: Dictionary = after[i]
		for key in a:
			if str(a.get(key)) != str(b.get(key)):
				diffs.append("%s.%s:%s->%s" % [a.get("path"), key, str(a.get(key)), str(b.get(key))])
	return " (diff %s)" % ",".join(diffs.slice(0, 4))


func _formal_sig() -> Dictionary:
	return {"gen": film.get("CathedralSnapshotGeneration"), "sgen": film.get("SealedProbeViewGeneration"), "sealed": film.get("ProbeViewAvailable"), "replay": film.get("CathedralContactReplayInvocationCount"), "reset": film.get("FormalWorkingStateResetCount"), "policy": film.get("CathedralFormalPolicySha256")}


func _formal_sig_with_image() -> Dictionary:
	var signature := _formal_sig()
	signature["query"] = film.get("CathedralProbeQueryDatasetSha256")
	signature["contact"] = film.get("CathedralSpatialKernelContactCountSha256")
	signature["film"] = _film_hash()
	return signature


func _line_fits(label: Label, prefix: String) -> bool:
	var font := label.get_theme_font("font")
	var size := label.get_theme_font_size("font_size")
	for line in label.text.split("\n"):
		if line.begins_with(prefix):
			return font.get_string_size(line, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x <= label.size.x - 0.5
	return true


func _wait_sealed() -> void:
	for _i in range(SEAL_TIMEOUT_FRAMES):
		if bool(film.get("ProbeViewAvailable")) and not bool(film.get("CathedralSnapshotLifecycleActive")):
			break
		await process_frame
	_check(bool(film.get("ProbeViewAvailable")), "acquisition sealed")
	await _frames(3)


func _film_hash() -> String:
	var image := film.call("CopyFilmImageForTesting") as Image
	if image == null:
		return "none"
	var context := HashingContext.new()
	context.start(HashingContext.HASH_SHA256)
	context.update(image.get_data())
	return context.finish().hex_encode()


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


func _queue(keycode: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	Input.parse_input_event(event)
	var release := event.duplicate() as InputEventKey
	release.pressed = false
	Input.parse_input_event(release)


func _frames(count: int) -> void:
	for _i in range(count):
		await process_frame


func _check(condition: bool, message: String) -> void:
	if not condition:
		failures.append("%s: %s" % [kind, message])
