extends SceneTree

const SCENE_PATH := "res://ObservatoryWorkbench.tscn"
const TIMEOUT_FRAMES := 1200


func _initialize() -> void:
	await _qualify_step_convergence_ownership()
	await _qualify_boundary_mutation_rejection()
	quit(0)


func _qualify_step_convergence_ownership() -> void:
	var first := await _capture_convergence_snapshot(true)
	var second := await _capture_convergence_snapshot(false)

	_assert(first.query_sha == second.query_sha, "equivalent convergence runs preserve query identity")
	_assert(first.contact_sha == second.contact_sha, "equivalent convergence runs preserve contact identity")
	_assert(not str(first.telemetry_sha).is_empty(), "renderer-cache rebuild run produces convergence diagnostics")
	_assert(not str(second.telemetry_sha).is_empty(), "control run produces convergence diagnostics")
	_assert(first.frozen_sha == second.frozen_sha, "equivalent convergence runs consume one frozen execution identity")
	print("FROZEN_BOUNDARY_STEP_CONVERGENCE PASS first_frozen_resolves=%d second_frozen_resolves=%d live_resolves=0 execution=%s rebuild_telemetry=%s control_telemetry=%s" % [
		first.frozen_resolves,
		second.frozen_resolves,
		first.frozen_sha,
		first.telemetry_sha,
		second.telemetry_sha,
	])


func _capture_convergence_snapshot(rebuild_cache: bool) -> Dictionary:
	var scene_root: Node = load(SCENE_PATH).instantiate()
	var chamber: Node = scene_root.get_node("PlayableWorld/TransportChamberWorld")
	var film: Node = chamber.get_node("GrinFilmCamera")
	var controller: Node = chamber.get_node("FilmController")
	var player: CharacterBody3D = chamber.get_node("TransportChamberPlayer")
	var renderer: Node = chamber.get_node("RayBeamRenderer")
	film.set("Width", 32)
	film.set("Height", 18)
	film.set("EnableDomainTelemetry", true)
	film.set("EnableStepConvergenceTelemetry", true)
	film.set("EnableDomainAwareFirstHitResolver", false)
	get_root().add_child(scene_root)
	await _settle(30)
	player.set_physics_process(false)
	var result := await _run_convergence_snapshot(film, controller, renderer, rebuild_cache)
	controller.call("set_mode", 0)
	await _settle(2)
	scene_root.queue_free()
	await _settle(2)
	return result


func _run_convergence_snapshot(film: Node, controller: Node, renderer: Node, rebuild_cache: bool) -> Dictionary:
	renderer.call("BeginBoundaryExecutionOwnershipDiagnosticsForTesting")
	controller.call("set_mode", 1)
	for _index in 60:
		await process_frame
		if str(film.get("CathedralSnapshotLifecycleStateName")) == "capturing":
			break
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "capturing", "Formal convergence capture started")
	if rebuild_cache:
		renderer.call("RebuildBoundaryCacheForTesting")
	await _wait_for_terminal(film)
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "complete", "Formal convergence capture completed")
	_assert(bool(film.get("ProbeViewAvailable")), "completed convergence acquisition exposes sealed evidence")
	var work_units := int(film.get("CathedralFormalWorkUnitCommitCount")) - int(film.get("CathedralFormalWorkUnitReplacementCount"))
	var frozen_resolves := int(renderer.call("GetBoundaryExecutionFrozenFormalResolveCountForTesting"))
	var live_resolves := int(renderer.call("GetBoundaryExecutionLiveCacheResolveCountForTesting"))
	var missing_rejects := int(renderer.call("GetBoundaryExecutionMissingFrozenFormalRejectCountForTesting"))
	var identity_mismatches := int(renderer.call("GetBoundaryExecutionFrozenFormalIdentityMismatchCountForTesting"))
	var frozen_sha := str(renderer.call("GetBoundaryExecutionFrozenFormalSha256ForTesting"))
	renderer.call("EndBoundaryExecutionOwnershipDiagnosticsForTesting")
	_assert(frozen_resolves > work_units, "nested convergence transport reached the actual frozen resolver")
	_assert(live_resolves == 0, "Formal convergence never resolves the LIVE boundary cache")
	_assert(missing_rejects == 0, "valid Formal convergence owns a frozen snapshot")
	_assert(identity_mismatches == 0, "all Formal convergence resolutions consume one execution identity")
	_assert(not frozen_sha.is_empty(), "actual frozen execution identity is observed")
	return {
		"query_sha": str(film.get("CathedralProbeQueryDatasetSha256")),
		"contact_sha": str(film.get("CathedralSpatialKernelContactCountSha256")),
		"telemetry_sha": str(film.call("GetStepConvergenceTelemetrySha256ForTesting")),
		"frozen_sha": frozen_sha,
		"frozen_resolves": frozen_resolves,
	}


func _qualify_boundary_mutation_rejection() -> void:
	var scene_root: Node = load(SCENE_PATH).instantiate()
	get_root().add_child(scene_root)
	await _settle(30)
	var chamber: Node = scene_root.get_node("PlayableWorld/TransportChamberWorld")
	var film: Node = chamber.get_node("GrinFilmCamera")
	var controller: Node = chamber.get_node("FilmController")
	var player: CharacterBody3D = chamber.get_node("TransportChamberPlayer")
	var boundary: Node = chamber.get_node("GalleryBoundary")
	player.set_physics_process(false)

	controller.call("set_mode", 1)
	for _index in 60:
		await process_frame
		if str(film.get("CathedralSnapshotLifecycleStateName")) == "capturing":
			break
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "capturing", "Formal capture started")
	var original_ior := float(boundary.get("IOR"))
	boundary.set("IOR", original_ior + 0.125)

	for _index in TIMEOUT_FRAMES:
		await process_frame
		var state := str(film.get("CathedralSnapshotLifecycleStateName"))
		if state == "failed" or state == "complete":
			break
	boundary.set("IOR", original_ior)

	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "failed", "mutated boundary authority cannot seal")
	_assert(str(film.get("CathedralSnapshotLifecycleReasonName")) == "boundary_epoch_changed", "boundary execution hash invalidation reason")
	_assert(not bool(film.get("ProbeViewAvailable")), "invalidated acquisition exposes no sealed Q evidence")
	print("FROZEN_BOUNDARY_MUTATION PASS state=failed reason=boundary_epoch_changed parameter=IOR")
	controller.call("set_mode", 0)
	await _settle(2)
	scene_root.queue_free()
	await _settle(2)


func _wait_for_terminal(film: Node) -> void:
	for _index in TIMEOUT_FRAMES:
		await process_frame
		var state := str(film.get("CathedralSnapshotLifecycleStateName"))
		if state == "failed" or state == "complete":
			return
	_assert(false, "Formal acquisition reached terminal state within timeout")


func _settle(frames: int) -> void:
	for _index in frames:
		await process_frame


func _assert(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("FROZEN_BOUNDARY_MUTATION FAIL: " + message)
	quit(1)
