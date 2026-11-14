class_name SceneNavigator
extends RefCounted

const WORKBENCH_ID := "workbench"
const GALLERY_ID := "suspended_portal_gallery"
const WORKBENCH_PATH := "res://ObservatoryWorkbench.tscn"
const GALLERY_PATH := "res://Experiences/SuspendedPortalGallery.tscn"

const _SCENES := {
	WORKBENCH_ID: {
		"display_name": "Observatory Workbench",
		"path": WORKBENCH_PATH,
	},
	GALLERY_ID: {
		"display_name": "Suspended Portal Gallery",
		"path": GALLERY_PATH,
	},
}


static func registry() -> Dictionary:
	return _SCENES.duplicate(true)


static func current_scene_id(tree: SceneTree) -> String:
	if tree == null or tree.current_scene == null:
		return ""
	var current_path := tree.current_scene.scene_file_path
	for scene_id in _SCENES:
		if current_path == str(_SCENES[scene_id]["path"]):
			return str(scene_id)
	return ""


static func validate_target(scene_id: String, resource_path: String = "") -> Dictionary:
	if not _SCENES.has(scene_id):
		return _result(false, false, "unknown_scene_id", scene_id, resource_path,
			"Unknown scene ID: %s" % scene_id)

	var canonical_path := str(_SCENES[scene_id]["path"])
	var path := canonical_path if resource_path.is_empty() else resource_path
	if path != canonical_path:
		return _result(false, false, "path_not_registered", scene_id, path,
			"Scene path is not the registered target for %s" % scene_id)
	if not ResourceLoader.exists(path, "PackedScene"):
		return _result(false, false, "resource_missing", scene_id, path,
			"Scene resource is missing: %s" % path)
	var resource := ResourceLoader.load(path, "PackedScene", ResourceLoader.CACHE_MODE_REUSE)
	if not resource is PackedScene:
		return _result(false, false, "resource_not_packed_scene", scene_id, path,
			"Scene resource is not a PackedScene: %s" % path)
	return {
		"ok": true,
		"changed": false,
		"code": "validated",
		"scene_id": scene_id,
		"path": path,
		"message": "Scene target validated.",
		"packed_scene": resource,
	}


static func navigate(tree: SceneTree, scene_id: String) -> Dictionary:
	if tree == null or tree.current_scene == null:
		return _result(false, false, "current_scene_unavailable", scene_id, "",
			"Current scene is unavailable.")

	var validation := validate_target(scene_id)
	if not bool(validation.get("ok", false)):
		return validation

	var target_path := str(validation["path"])
	if tree.current_scene.scene_file_path == target_path:
		return _result(true, false, "already_current", scene_id, target_path,
			"The requested scene is already current.")

	var controller := _find_current_film_controller(tree.current_scene)
	if controller == null or not controller.has_method("set_mode"):
		return _result(false, false, "film_controller_unavailable", scene_id, target_path,
			"The current instrument has no FilmController authority.")

	# FilmController owns the existing ContextChanged invalidation. This call is
	# deliberately synchronous and occurs before SceneTree queues the old scene
	# for disposal.
	controller.call("set_mode", 0)
	var error := tree.change_scene_to_packed(validation["packed_scene"] as PackedScene)
	if error != OK:
		return _result(false, false, "scene_change_failed", scene_id, target_path,
			"SceneTree rejected the scene change (error %d)." % error)
	return _result(true, true, "scene_change_queued", scene_id, target_path,
		"Opening %s with a fresh instrument." % str(_SCENES[scene_id]["display_name"]))


static func _find_current_film_controller(scene_root: Node) -> Node:
	if scene_root == null:
		return null
	var controllers := scene_root.find_children("FilmController", "Node", true, false)
	if controllers.size() != 1:
		return null
	return controllers[0]


static func _result(ok: bool, changed: bool, code: String, scene_id: String,
		resource_path: String, message: String) -> Dictionary:
	return {
		"ok": ok,
		"changed": changed,
		"code": code,
		"scene_id": scene_id,
		"path": resource_path,
		"message": message,
	}
