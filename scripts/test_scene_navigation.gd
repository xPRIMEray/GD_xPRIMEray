extends SceneTree

const NAVIGATOR := preload("res://UI/SceneNavigator.gd")

var failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_registry_and_validation()
	await _change_to(NAVIGATOR.WORKBENCH_PATH)
	var workbench := current_scene
	_assert(workbench != null, "Workbench did not become current")
	if workbench == null:
		_finish()
		return

	var controller := _controller(workbench)
	var film := _film(workbench)
	_test_scenes_tab(workbench)
	_test_invalid_and_noop_navigation(controller)

	# A real request must be invalidated synchronously before the old camera is
	# queued for disposal.
	_assert(bool(controller.call("EnterFormalSnapshotFromInput")), "Formal request did not start")
	_assert(bool(film.get("CathedralSnapshotLifecycleActive")), "Formal lifecycle not active before navigation")
	var old_film_ref: WeakRef = weakref(film)
	var result: Dictionary = NAVIGATOR.navigate(self, NAVIGATOR.GALLERY_ID)
	_assert(bool(result.get("ok", false)) and bool(result.get("changed", false)),
		"Workbench to Gallery navigation was not queued")
	_assert(not bool(film.get("CathedralSnapshotLifecycleActive")),
		"Formal lifecycle remained active after OFF transition")
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "invalidated",
		"Formal lifecycle did not become invalidated before disposal")
	_assert(str(film.get("CathedralSnapshotLifecycleReasonName")) == "context_changed",
		"Formal lifecycle did not record ContextChanged before disposal")
	await scene_changed
	await _settle(5)
	_assert(old_film_ref.get_ref() == null, "Old Workbench camera was not freed")

	var gallery := current_scene
	_assert(gallery != null and gallery.scene_file_path == NAVIGATOR.GALLERY_PATH,
		"Gallery did not become current")
	_test_fresh_instrument(gallery, "Gallery")
	_test_gallery_geometry_ownership(gallery)
	await _test_embedded_gallery_escape_is_inert(gallery)

	# Exercise the gallery's actual Esc handler while it owns current_scene.
	var escape := InputEventKey.new()
	escape.keycode = KEY_ESCAPE
	escape.pressed = true
	escape.echo = false
	Input.parse_input_event(escape)
	for _frame in range(20):
		await process_frame
		if current_scene != null and current_scene.scene_file_path == NAVIGATOR.WORKBENCH_PATH:
			break
	await _settle(5)
	_assert(current_scene.scene_file_path == NAVIGATOR.WORKBENCH_PATH,
		"Gallery Esc did not return to Workbench")
	_test_fresh_instrument(current_scene, "Workbench after Esc")
	_test_inspector_rebind(current_scene)

	# Complete three total Workbench/Gallery round trips and prove that each
	# destination owns exactly one fresh camera/controller pair.
	for round_trip in range(2, 4):
		await _navigate_and_wait(NAVIGATOR.GALLERY_ID)
		_test_fresh_instrument(current_scene, "Gallery round %d" % round_trip)
		await _navigate_and_wait(NAVIGATOR.WORKBENCH_ID)
		_test_fresh_instrument(current_scene, "Workbench round %d" % round_trip)

	_finish()


func _test_registry_and_validation() -> void:
	var registry := NAVIGATOR.registry()
	_assert(registry.size() == 2, "Registry must contain exactly two scenes")
	_assert(registry.has(NAVIGATOR.WORKBENCH_ID), "Workbench registry entry missing")
	_assert(registry.has(NAVIGATOR.GALLERY_ID), "Gallery registry entry missing")
	for scene_id in registry:
		var validation := NAVIGATOR.validate_target(str(scene_id))
		_assert(bool(validation.get("ok", false)), "Registered scene failed validation: %s" % scene_id)
		_assert(validation.get("packed_scene") is PackedScene,
			"Registered scene did not load as PackedScene: %s" % scene_id)
	var invalid_id := NAVIGATOR.validate_target("not_registered")
	_assert(not bool(invalid_id.get("ok", true)) and str(invalid_id.get("code")) == "unknown_scene_id",
		"Invalid scene ID did not fail closed")
	var invalid_path := NAVIGATOR.validate_target(NAVIGATOR.WORKBENCH_ID, "res://missing_scene.tscn")
	_assert(not bool(invalid_path.get("ok", true)) and str(invalid_path.get("code")) == "path_not_registered",
		"Invalid scene path did not fail closed")


func _test_scenes_tab(workbench: Node) -> void:
	var tabs := workbench.get_node("UILayer/UIRoot/SideDock/Tabs") as TabContainer
	_assert(tabs.get_tab_count() == 4, "Workbench must expose exactly four primary tabs")
	_assert(tabs.get_tab_title(0) == "Observatory", "Observatory tab moved from index 0")
	_assert(tabs.get_child(1).name == "TestBenchScroll", "TestBench tab moved from index 1")
	_assert(tabs.get_tab_title(2) == "Inspector", "Inspector tab moved from index 2")
	_assert(tabs.get_tab_title(3) == "Scenes", "Scenes tab is not at index 3")
	var scenes := tabs.get_node("Scenes")
	_assert(scenes.get_node("Margin/Content/Warning").text.find("fresh instrument") >= 0,
		"Scenes panel lacks the fresh-instrument warning")


func _test_invalid_and_noop_navigation(controller: Node) -> void:
	controller.call("set_mode", 2)
	var before_mode := str(controller.call("GetModeName"))
	var invalid := NAVIGATOR.navigate(self, "not_registered")
	_assert(not bool(invalid.get("ok", true)), "Invalid navigation unexpectedly succeeded")
	_assert(str(controller.call("GetModeName")) == before_mode,
		"Invalid navigation mutated the instrument")
	var noop := NAVIGATOR.navigate(self, NAVIGATOR.WORKBENCH_ID)
	_assert(bool(noop.get("ok", false)) and not bool(noop.get("changed", true)) and
		str(noop.get("code")) == "already_current", "Same-scene navigation was not a no-op success")
	_assert(str(controller.call("GetModeName")) == before_mode,
		"Same-scene no-op reset the instrument")
	controller.call("set_mode", 0)


func _test_fresh_instrument(scene: Node, label: String) -> void:
	var controllers := scene.find_children("FilmController", "Node", true, false)
	var films := scene.find_children("GrinFilmCamera", "Node", true, false)
	_assert(controllers.size() == 1, "%s does not own exactly one FilmController" % label)
	_assert(films.size() == 1, "%s does not own exactly one GrinFilmCamera" % label)
	if controllers.size() != 1 or films.size() != 1:
		return
	var controller: Node = controllers[0]
	var film: Node = films[0]
	_assert(str(controller.call("GetModeName")) == "OFF", "%s did not start G OFF" % label)
	_assert(str(film.get("ProbeProfileStableId")) == "cathedral", "%s did not start P Cathedral" % label)
	_assert(not bool(film.get("ProbeViewAvailable")), "%s retained sealed evidence" % label)
	_assert(int(film.get("CathedralSnapshotGeneration")) == 0, "%s generation was not zero" % label)
	_assert(not bool(film.get("CathedralSnapshotLifecycleActive")), "%s lifecycle started active" % label)


func _test_gallery_geometry_ownership(gallery: Node) -> void:
	var fixture_nodes := get_nodes_in_group("fixture_geometry")
	_assert(not fixture_nodes.is_empty(), "Gallery fixture geometry is missing")
	for fixture in fixture_nodes:
		if gallery.is_ancestor_of(fixture):
			continue
		_assert(false, "Gallery fixture geometry escaped the gallery root: %s" % fixture.get_path())


func _test_embedded_gallery_escape_is_inert(actual_gallery: Node) -> void:
	var embedded: Node = (load(NAVIGATOR.GALLERY_PATH) as PackedScene).instantiate()
	embedded.name = "EmbeddedGalleryFixture"
	actual_gallery.add_child(embedded)
	await _settle(3)
	var before := current_scene
	var escape := InputEventKey.new()
	escape.keycode = KEY_ESCAPE
	escape.pressed = true
	embedded.call("_unhandled_input", escape)
	await _settle(2)
	_assert(current_scene == before, "Embedded gallery Esc changed the current scene")
	embedded.queue_free()
	await _settle(2)


func _test_inspector_rebind(workbench: Node) -> void:
	var inspector := workbench.get_node("UILayer/UIRoot/SideDock/Tabs/Inspector")
	var film := _film(workbench)
	_assert(inspector.get("_film") == film, "Inspector did not bind to the fresh Workbench camera")


func _navigate_and_wait(scene_id: String) -> void:
	var prior_film_ref: WeakRef = weakref(_film(current_scene))
	var result: Dictionary = NAVIGATOR.navigate(self, scene_id)
	_assert(bool(result.get("ok", false)) and bool(result.get("changed", false)),
		"Navigation failed for %s: %s" % [scene_id, result.get("code", "missing")])
	if bool(result.get("changed", false)):
		await scene_changed
		await _settle(4)
		_assert(prior_film_ref.get_ref() == null,
			"Prior camera survived navigation to %s" % scene_id)


func _change_to(path: String) -> void:
	var packed := load(path) as PackedScene
	_assert(packed != null, "Unable to load initial scene: %s" % path)
	if packed == null:
		return
	var error := change_scene_to_packed(packed)
	_assert(error == OK, "Unable to queue initial scene: %s" % path)
	if error == OK:
		await scene_changed
		await _settle(5)


func _controller(scene: Node) -> Node:
	var nodes := scene.find_children("FilmController", "Node", true, false)
	return nodes[0] if nodes.size() == 1 else null


func _film(scene: Node) -> Node:
	var nodes := scene.find_children("GrinFilmCamera", "Node", true, false)
	return nodes[0] if nodes.size() == 1 else null


func _settle(frames: int) -> void:
	for _index in range(frames):
		await process_frame


func _assert(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)


func _finish() -> void:
	if failures.is_empty():
		print("SCENE_NAVIGATION PASS scenarios=10 round_trips=3")
		quit(0)
		return
	for failure in failures:
		push_error("SCENE_NAVIGATION FAIL: %s" % failure)
	print("SCENE_NAVIGATION FAIL count=%d" % failures.size())
	quit(1)
