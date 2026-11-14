extends PanelContainer

@onready var _current_scene: Label = %CurrentScene
@onready var _status: Label = %NavigationStatus
@onready var _workbench_button: Button = %WorkbenchButton
@onready var _gallery_button: Button = %GalleryButton


func _ready() -> void:
	_workbench_button.pressed.connect(_navigate.bind(SceneNavigator.WORKBENCH_ID))
	_gallery_button.pressed.connect(_navigate.bind(SceneNavigator.GALLERY_ID))
	_refresh_current_scene()


func _navigate(scene_id: String) -> void:
	var result := SceneNavigator.navigate(get_tree(), scene_id)
	_status.text = str(result.get("message", "Navigation failed."))
	if not bool(result.get("changed", false)):
		_refresh_current_scene()


func _refresh_current_scene() -> void:
	var scene_id := SceneNavigator.current_scene_id(get_tree())
	var scenes := SceneNavigator.registry()
	if scenes.has(scene_id):
		_current_scene.text = "Current scene: %s" % str(scenes[scene_id]["display_name"])
	else:
		_current_scene.text = "Current scene: Unregistered"
	_workbench_button.disabled = scene_id == SceneNavigator.WORKBENCH_ID
	_gallery_button.disabled = scene_id == SceneNavigator.GALLERY_ID
