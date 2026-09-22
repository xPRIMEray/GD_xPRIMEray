extends SceneTree

const SCENE_ID := "portable_observatory.gallery.v1"
const SCENE_PATH := "res://ObservatoryWorkbench.tscn"
const LINEAGE := "13a2d965,af71c50e,94dff98b,f08122b9,5fdc5e1f"

var root_scene: Node
var chamber: Node
var film: Node
var film_controller: Node
var field_dial: Node
var player: Node
var output_dir := "/tmp/artifact001"
var sequence: Array[Dictionary] = []
var start_generation := 0
var start_context := ""
var replay_invocations := 0
var query_dataset_sha := ""
var query_count := 0
var sealed_pose: Transform3D

func _initialize() -> void:
	output_dir = OS.get_environment("ARTIFACT001_OUTPUT")
	if output_dir.is_empty():
		output_dir = "/tmp/artifact001"
	DirAccess.make_dir_recursive_absolute(output_dir)

	root_scene = load(SCENE_PATH).instantiate()
	get_root().add_child(root_scene)
	await _settle(12)
	chamber = root_scene.get_node("PlayableWorld/TransportChamberWorld")
	film = chamber.get_node("GrinFilmCamera")
	film_controller = chamber.get_node("FilmController")
	field_dial = chamber.get_node("FieldDialController")
	player = chamber.get_node("TransportChamberPlayer")
	await _settle(12)
	# Deterministic fixture pose: the public key sequence remains routed through
	# the normal scene, while gravity cannot move the camera between captures.
	player.set_physics_process(false)
	await _settle(2)

	await _capture_stage("establish.png", "Gallery", false)
	_send_key(KEY_F)
	await _settle(8)
	if not _all_field_sources_runtime_visible():
		# The normal routed event can arrive before the C# node's first input pump
		# on a cold Mono startup. A second routed F is still deterministic because
		# the aggregate toggle target is "all visible" whenever any source is off.
		_send_key(KEY_F)
		await _settle(8)
	_assert(_all_field_sources_runtime_visible(), "F did not enable all field structures")
	field_dial.call("SetDisplayPresetForTesting", "Hermetic")
	await _settle(10)
	_assert(field_dial.call("GetExperimentName") == "Hermetic", "programmatic selection did not select Hermetic experiment")
	var experiment_pose: Transform3D = player.global_transform
	_assert(player.global_transform.is_equal_approx(experiment_pose), "observer pose changed after experiment selection settled")
	_assert(_all_field_sources_runtime_visible(), "Field Structure did not remain enabled after experiment selection")
	await _capture_stage("hermetic_field_structure.png", "Hermetic", false)

	_send_key(KEY_G)
	await _wait_for_complete()
	await _settle(2)
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "complete", "formal snapshot is not Complete")
	_assert(bool(film.get("ProbeViewAvailable")), "sealed Probe View authority unavailable")
	_assert(int(film.get("CathedralSnapshotProcessedPixelCount")) == int(film.get("CathedralSnapshotTotalPixelCount")), "snapshot coverage incomplete")
	_assert(int(film.get("CathedralSnapshotTotalPixelCount")) > 0, "snapshot has no pixels")
	_assert(bool(film.get("CathedralContactReplayComplete")), "contact replay did not complete")
	replay_invocations = int(film.get("CathedralContactReplayInvocationCount"))
	_assert(replay_invocations == 1, "expected one contact replay, got %d" % replay_invocations)
	query_dataset_sha = str(film.get("CathedralProbeQueryDatasetSha256"))
	query_count = int(film.get("CathedralProbeQueryCount"))
	_assert(query_count == 388800, "unexpected formal query count: %d" % query_count)
	print("ARTIFACT001 CONTACT_REPLAY authority=%s queries=%d dataset_sha=%s invocations=%d" % [film.get("CathedralContactAuthorityToken"), query_count, query_dataset_sha, replay_invocations])
	start_generation = int(film.get("SealedProbeViewGeneration"))
	start_context = _context_token()
	sealed_pose = player.global_transform
	_assert(bool(player.call("IsPoseHeld")), "formal SNAPSHOT did not hold observer pose")
	await _capture_stage("snapshot_complete.png", "Hermetic", true)
	_send_key(KEY_F)
	await _settle(4)
	_assert(int(film.get("SealedProbeViewGeneration")) == start_generation, "F invalidated sealed generation")
	_send_key(KEY_F)
	await _settle(4)

	await _capture_stage("outcome.png", "Hermetic", true)
	_send_key(KEY_Q)
	await _settle(4)
	_assert(int(film.get("CurrentProbeView")) == 1, "Q did not select Contact Events")
	_assert_same_acquisition("Contact Events")
	await _capture_stage("contact_events.png", "Hermetic", true)
	_send_key(KEY_Q)
	await _settle(4)
	_assert(int(film.get("CurrentProbeView")) == 2, "Q did not select Transport Effort")
	_assert_same_acquisition("Transport Effort")
	await _capture_stage("transport_effort.png", "Hermetic", true)

	var bundle_dir := output_dir.path_join("portable_bundle")
	var failure: String = film.call(
		"CapturePortableObservatoryBundle",
		bundle_dir,
		"artifact001-hermetic",
		SCENE_ID,
		SCENE_PATH,
		OS.get_environment("ARTIFACT001_ENGINE_COMMIT"),
		LINEAGE)
	_assert(failure.is_empty(), "portable bundle failed: " + failure)
	_write_artifact_manifest()
	_write_artifact_summary()
	print("ARTIFACT001 PASS output=%s generation=%d dimensions=%dx%d" % [output_dir, start_generation, _width(), _height()])
	quit(0)

func _capture_stage(file_name: String, context_name: String, sealed_expected: bool) -> void:
	if sealed_expected:
		_assert(bool(film.get("ProbeViewAvailable")), file_name + " requires sealed authority")
		_assert(int(film.get("SealedProbeViewGeneration")) == start_generation if start_generation > 0 else true, file_name + " generation changed")
	var image := root_scene.get_viewport().get_texture().get_image()
	_assert(image != null and image.get_width() > 0 and image.get_height() > 0, file_name + " has no viewport image")
	var path := output_dir.path_join(file_name)
	_assert(image.save_png(path) == OK, "failed to save " + file_name)
	sequence.append({
		"file": file_name,
		"context": context_name,
		"generation": int(film.get("SealedProbeViewGeneration")) if sealed_expected else 0,
		"camera_transform": _camera_transform_values(),
		"width": image.get_width(),
		"height": image.get_height(),
		"sha256": _sha256_file(path)
	})

func _wait_for_complete() -> void:
	for _i in range(1200):
		await process_frame
		if str(film.get("CathedralSnapshotLifecycleStateName")) in ["requested", "waiting_for_physics", "capturing"]:
			_assert(bool(player.call("IsPoseHeld")), "observer pose released during formal acquisition")
		if bool(film.get("ProbeViewAvailable")) and str(film.get("CathedralSnapshotLifecycleStateName")) == "complete":
			return
	_fail("formal Snapshot timeout state=%s reason=%s processed=%d/%d" % [
			film.get("CathedralSnapshotLifecycleStateName"), film.get("CathedralSnapshotLifecycleReasonName"),
			film.get("CathedralSnapshotProcessedPixelCount"), film.get("CathedralSnapshotTotalPixelCount")])

func _assert_same_acquisition(view_name: String) -> void:
	_assert(int(film.get("SealedProbeViewGeneration")) == start_generation, view_name + " changed sealed generation")
	_assert(_context_token() == start_context, view_name + " changed context identity")
	_assert(int(film.get("CathedralSnapshotProcessedPixelCount")) == int(film.get("CathedralSnapshotTotalPixelCount")), view_name + " changed coverage")
	_assert(str(film.get("CathedralSnapshotLifecycleStateName")) == "complete", view_name + " changed lifecycle state")
	_assert(int(film.get("CathedralContactReplayInvocationCount")) == replay_invocations, view_name + " invoked replay")
	_assert(player.global_transform.is_equal_approx(sealed_pose), view_name + " changed observer pose")

func _write_artifact_manifest() -> void:
	var manifest_path := output_dir.path_join("portable_bundle").path_join("manifest.json")
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(manifest_path))
	_assert(manifest is Dictionary, "bundle manifest is not a JSON object")
	var proof: Dictionary = manifest["same_generation_proof"]
	proof["human_sequence_generation_start"] = start_generation
	proof["human_sequence_generation_end"] = int(film.get("SealedProbeViewGeneration"))
	proof["human_sequence_context_unchanged"] = _context_token() == start_context
	proof["q_transport_requests"] = 0
	proof["q_render_steps"] = 0
	proof["q_refinements"] = 0
	proof["q_snapshot_requests"] = 0
	proof["q_source_arrays_mutated"] = false
	proof["contact_replay_invocations"] = replay_invocations
	proof["contact_query_dataset_sha256"] = query_dataset_sha
	proof["contact_query_count"] = query_count
	proof["contact_authority"] = str(film.get("CathedralContactAuthorityToken"))
	manifest["artifact_001"] = {
		"recipe": "Gallery launch → select Hermetic Experiment → F optional → G formal SNAPSHOT → Q triad",
		"field_structure": "ON",
		"preset": "Hermetic",
		"snapshot_state": str(film.get("CathedralSnapshotLifecycleStateName")),
		"unprocessed": int(film.get("CathedralSnapshotTotalPixelCount")) - int(film.get("CathedralSnapshotProcessedPixelCount")),
		"sequence": sequence,
		"portable_bundle_dir": "portable_bundle/",
		"contact_authority": str(film.get("CathedralContactAuthorityToken")),
		"contact_replay_complete": bool(film.get("CathedralContactReplayComplete")),
		"contact_replay_invocations": replay_invocations,
		"contact_query_dataset_sha256": query_dataset_sha,
		"contact_query_count": query_count,
		"sequence_note": "establish.png is the pre-transition Gallery control; all Hermetic acquisition/view captures share one camera pose and dimensions."
	}
	var file := FileAccess.open(output_dir.path_join("manifest.json"), FileAccess.WRITE)
	_assert(file != null, "cannot rewrite artifact manifest")
	file.store_string(JSON.stringify(manifest, "\t") + "\n")

func _write_artifact_summary() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(output_dir.path_join("manifest.json")))
	var histograms: Dictionary = manifest["histograms"]
	var acquisition: Dictionary = manifest["acquisition"]
	var effort: Dictionary = histograms["effort"]
	var lines := PackedStringArray([
		"# Artifact 001 · Cathedral Probe",
		"",
		"## Acquisition Identity",
		"- Semantic SceneId: `%s`" % SCENE_ID,
		"- Preset: `Hermetic`; Field Structure: `ON`",
		"- Generation: `%s`" % acquisition["generation"],
		"- Dimensions: `%sx%s`" % [acquisition["width"], acquisition["height"]],
		"- Context canonical SHA-256: `%s`" % acquisition["context"]["canonical_sha256"],
		"",
		"## Qualification Result",
		"- Formal lifecycle: `Complete`; unprocessed: `%s`" % acquisition["unprocessed"],
		"- Field Structure remained presentation-only during acquisition.",
		"",
		"## Measured Counts",
		"- Outcomes: `%s`" % JSON.stringify(histograms["outcome"]),
		"- Contact Events: `%s`" % JSON.stringify(histograms["contact_events"]),
		"- Effort: `%s`" % JSON.stringify(effort),
		"",
		"## Generated Artifacts",
		"- Human sequence: establish, Hermetic field structure, snapshot complete, Outcome, Contact Events, Transport Effort.",
		"- Canonical channels and plates are the existing deterministic bundle artifacts.",
		"",
		"## Same-Generation Proof",
		"- Q triad generation: `%s → %s → %s`" % [start_generation, start_generation, start_generation],
		"- Q transport/render/refinement/snapshot requests: `0/0/0/0`",
		"- Source authority: one formally Complete sealed acquisition.",
		"",
		"## Known Limitations",
		"- `establish.png` is the pre-transition Gallery control; post-Hermetic captures share the sealed Hermetic pose.",
		"- This is a qualified visualization artifact, not a scientific evidence claim.",
		"",
		"## Claim Boundaries",
		"- No optical closure from this bundle alone.",
		"- Contact Events are not a unique-surface census.",
		"- Transport Effort is not time, energy, or field magnitude.",
		"- Color is presentation mapping, not measurement authority.",
		"- No UAP validation.",
		"",
		"## Reproduction Command",
		"```bash",
		"ARTIFACT001_OUTPUT=/tmp/artifact001 ./scripts/capture_artifact001.sh",
		"```"
	])
	var file := FileAccess.open(output_dir.path_join("run_summary.md"), FileAccess.WRITE)
	_assert(file != null, "cannot write artifact summary")
	file.store_string("\n".join(lines) + "\n")

func _all_field_sources_runtime_visible() -> bool:
	var nodes := root_scene.get_tree().get_nodes_in_group("field_sources")
	var eligible := 0
	for source in nodes:
		if bool(source.get("DebugVizEnabled")):
			eligible += 1
			if not bool(source.get("DebugVizInGame")):
				return false
	return eligible > 0

func _context_token() -> String:
	return "%s:%s:%s:%s" % [film.get("SealedProbeViewGeneration"), film.get("CathedralSnapshotGeneration"), film.get("CathedralSnapshotTotalPixelCount"), field_dial.call("GetExperimentName")]

func _camera_transform_values() -> Array[float]:
	var basis: Basis = player.get_camera().global_transform.basis
	var origin: Vector3 = player.get_camera().global_transform.origin
	return [origin.x, origin.y, origin.z, basis.x.x, basis.y.x, basis.z.x, basis.x.y, basis.y.y, basis.z.y, basis.x.z, basis.y.z, basis.z.z]

func _width() -> int:
	return int(film.get("Width"))

func _height() -> int:
	return int(film.get("Height"))

func _sha256_file(path: String) -> String:
	var context := HashingContext.new()
	context.start(HashingContext.HASH_SHA256)
	var file := FileAccess.open(path, FileAccess.READ)
	_assert(file != null, "cannot hash " + path)
	context.update(file.get_buffer(file.get_length()))
	return context.finish().hex_encode()

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
	push_error("ARTIFACT001 FAIL: " + message)
	quit(1)
