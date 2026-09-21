extends Node3D

@onready var _demo = $OverspaceTrophyRoom
@onready var _player = $TransportChamberPlayer
@onready var _legacy_camera: Camera3D = $OverspaceTrophyRoom/PlayerCamera
@onready var _gallery_portal: Node3D = $OverspaceTrophyRoom/Gallery/EarthOrbPortal
@onready var _earth_portal: Node3D = $OverspaceTrophyRoom/Worlds/EarthWorld/EarthArrivalPortal
@onready var _film_camera: Node = $GrinFilmCamera
@onready var _ray_renderer: Node = $RayBeamRenderer
@onready var _film_controller: Node = $FilmController
@onready var _film_overlay: Node = $CanvasLayer/HUDRoot/FilmModule/FilmPlate/FilmOverlay2D
@onready var _field_dial: Node = $FieldDialController
@onready var _summary_label: Label = $OverspaceTrophyRoom/CanvasLayer/DemoSummary
@onready var _overspace_debug_overlay: Control = $OverspaceTrophyRoom/CanvasLayer/OverspaceDebugOverlay
@onready var _gallery_field: Node = $GalleryFieldSource
@onready var _earth_field: Node = $EarthFieldSource
@onready var _hud_root: Control = $CanvasLayer/HUDRoot
@onready var _telemetry_module: Control = $CanvasLayer/HUDRoot/TelemetryModule
@onready var _telemetry_backdrop: Control = $CanvasLayer/HUDRoot/TelemetryBackdrop
@onready var _telemetry_label: Label = $CanvasLayer/HUDRoot/TelemetryModule/TelemetryLabel
@onready var _portal_label: Label = $CanvasLayer/HUDRoot/PortalModule/PortalStatusLabel
@onready var _locomotion_label: Label = $CanvasLayer/HUDRoot/ObserverModule/ObserverStatusLabel

var _cooldown_remaining := 0.0
var _last_gallery_delta := 0.0
var _last_earth_delta := 0.0
var _advanced_telemetry_visible := false
var _optical_only_presentation := false
var _normal_observer_cull_mask: int = 0
var _demo_presentation_enabled := false
var _demo_restore_advanced_telemetry := false

const FIELD_PRESENTATION_LAYER_MASK := 1 << 19


func _ready() -> void:
	if _legacy_camera != null:
		_legacy_camera.current = false
		if _legacy_camera.has_method("SetInputEnabled"):
			_legacy_camera.call("SetInputEnabled", false, false)
	_configure_chamber_portals()

	var player_camera: Camera3D = _player.get_camera()
	_normal_observer_cull_mask = player_camera.cull_mask
	if _demo.has_method("OverrideViewerCamera"):
		_demo.call("OverrideViewerCamera", player_camera)
	if _demo.has_method("SetExternalTraversalOwnerActive"):
		_demo.call("SetExternalTraversalOwnerActive", true)
	player_camera.current = true
	if _player.has_signal("loco_mode_changed"):
		_player.loco_mode_changed.connect(_on_locomotion_mode_changed)
	if _field_dial != null and _field_dial.has_signal("field_strength_changed"):
		_field_dial.connect("field_strength_changed", Callable(self, "_on_field_strength_changed"))
	_on_locomotion_mode_changed(_player.GetLocomotionModeName())
	SetHudVisibleForGameplay(true)
	_set_advanced_telemetry_visible(false)
	_set_chamber_summary()
	_prime_portal_deltas()
	_update_portal_status()


func _exit_tree() -> void:
	if _film_controller != null:
		_film_controller.call("set_mode", 0)
	if _film_camera != null:
		_film_camera.set("UpdateEveryFrame", false)
		_film_camera.set_process(false)
	if _ray_renderer != null:
		_ray_renderer.set("UpdateEveryFrame", false)
		_ray_renderer.set_process(false)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_TAB:
			_set_advanced_telemetry_visible(not _advanced_telemetry_visible)
			get_viewport().set_input_as_handled()


func _process(delta: float) -> void:
	if _cooldown_remaining > 0.0:
		_cooldown_remaining = max(0.0, _cooldown_remaining - delta)
		_prime_portal_deltas()
		return

	var camera: Camera3D = _player.get_camera()
	_try_cross(_gallery_portal, camera.global_position, true)
	_try_cross(_earth_portal, camera.global_position, false)
	_update_portal_status()
	if _advanced_telemetry_visible:
		_update_telemetry_label()


func _try_cross(portal: Node3D, camera_position: Vector3, gallery_side: bool) -> void:
	if portal == null or not portal.has_method("SignedRadiusDelta"):
		return
	var current_delta := float(portal.call("SignedRadiusDelta", camera_position))
	var previous_delta := _last_gallery_delta if gallery_side else _last_earth_delta
	if gallery_side:
		_last_gallery_delta = current_delta
	else:
		_last_earth_delta = current_delta

	if previous_delta > 0.0 and current_delta <= 0.0 and portal.has_method("BuildExitTransform"):
		var mapped = portal.call("BuildExitTransform", _player.get_camera().global_transform)
		if mapped is Transform3D:
			_player.ApplyCameraTransform(mapped)
			if _film_controller != null and _film_controller.has_method("NotifyCameraTransformJump"):
				_film_controller.call("NotifyCameraTransformJump")
			_cooldown_remaining = 0.4
			_prime_portal_deltas()
			_update_portal_status()


func _prime_portal_deltas() -> void:
	var camera: Camera3D = _player.get_camera()
	if _gallery_portal != null and _gallery_portal.has_method("SignedRadiusDelta"):
		_last_gallery_delta = float(_gallery_portal.call("SignedRadiusDelta", camera.global_position))
	if _earth_portal != null and _earth_portal.has_method("SignedRadiusDelta"):
		_last_earth_delta = float(_earth_portal.call("SignedRadiusDelta", camera.global_position))


func _configure_chamber_portals() -> void:
	for portal in [_gallery_portal, _earth_portal]:
		if portal == null:
			continue
		portal.set("EnablePhaseLockedRemap", false)


func _set_chamber_summary() -> void:
	if _summary_label == null:
		return
	_summary_label.text = ""
	_summary_label.visible = false


func _set_advanced_telemetry_visible(visible: bool) -> void:
	_advanced_telemetry_visible = visible
	if _telemetry_module != null:
		_telemetry_module.visible = visible
	if _telemetry_backdrop != null:
		_telemetry_backdrop.visible = visible
	if _overspace_debug_overlay != null:
		_overspace_debug_overlay.visible = false
	for field in [_gallery_field, _earth_field]:
		if field != null:
			field.set("DebugVizInGame", false)
	_update_telemetry_label()


func SetHudVisibleForGameplay(visible: bool) -> void:
	if _hud_root != null:
		_hud_root.visible = visible


func SetOpticalOnlyPresentation(enabled: bool) -> void:
	var camera: Camera3D = _player.get_camera()
	if camera == null:
		return
	_optical_only_presentation = enabled
	camera.cull_mask = FIELD_PRESENTATION_LAYER_MASK if enabled else _normal_observer_cull_mask


func IsOpticalOnlyPresentation() -> bool:
	return _optical_only_presentation


func SetDemoPresentationMode(enabled: bool) -> void:
	if enabled == _demo_presentation_enabled:
		return
	_demo_presentation_enabled = enabled
	if enabled:
		_demo_restore_advanced_telemetry = _advanced_telemetry_visible
		_advanced_telemetry_visible = false
		_telemetry_module.visible = false
		_telemetry_backdrop.visible = false
	else:
		_advanced_telemetry_visible = _demo_restore_advanced_telemetry
		_telemetry_module.visible = _advanced_telemetry_visible
		_telemetry_backdrop.visible = _advanced_telemetry_visible
	_update_telemetry_label()
	if _film_overlay != null:
		_film_overlay.set("SuppressDebugOverlayBusPresentation", enabled)
	SetOpticalOnlyPresentation(enabled)


func SetInputEnabled(enabled: bool, release_mouse := true) -> void:
	if _player != null and _player.has_method("SetInputEnabled"):
		_player.call("SetInputEnabled", enabled, release_mouse)
	if _field_dial != null and _field_dial.has_method("SetInputEnabled"):
		_field_dial.call("SetInputEnabled", enabled)
	SetHudVisibleForGameplay(enabled)


func _on_locomotion_mode_changed(mode_name: String) -> void:
	if _locomotion_label == null:
		return
	_locomotion_label.text = "%s | WASD + mouse | Shift sprint | V fly\nE experiment | F field structure\nG SNAPSHOT | Q probe views | N shading | [ ] opacity | , . field\nTab telemetry | Esc Observatory" % mode_name


func _on_field_strength_changed(_value: float) -> void:
	if _advanced_telemetry_visible:
		_update_telemetry_label()


func _update_portal_status() -> void:
	if _portal_label == null:
		return
	var zone := _current_zone_name()
	var target := "Earth" if zone == "Gallery" else "Gallery"
	var delta := _last_gallery_delta if zone == "Gallery" else _last_earth_delta
	_portal_label.text = "Zone: %s | Portal: %s | delta: %+0.3f" % [
		zone,
		target,
		delta,
	]


func _update_telemetry_label() -> void:
	if _telemetry_label == null:
		return
	var zone := _current_zone_name()
	var gallery_delta := _last_gallery_delta
	var earth_delta := _last_earth_delta
	var film_mode := "unknown"
	var quality := "unknown"
	var shading := "unknown"
	var compute := "off"
	var field_value := 1.0
	var field_state := "FULL"
	var experiment := "Gallery"
	var reference_amp := 0.0
	var bend_scale := 0.0
	var compute_profile := "unknown"
	var compute_workers := "unknown"
	var compute_watchdog := "unknown"
	var compute_pressure := "unknown"
	if _film_controller != null:
		if _film_controller.has_method("GetModeName"):
			film_mode = str(_film_controller.call("GetModeName"))
		if _film_controller.has_method("GetQualityName"):
			quality = str(_film_controller.call("GetQualityName"))
		if _film_controller.has_method("GetShadingModeName"):
			shading = str(_film_controller.call("GetShadingModeName"))
		if _film_controller.has_method("IsComputeActive"):
			compute = "active" if bool(_film_controller.call("IsComputeActive")) else "idle"
	if _field_dial != null:
		if _field_dial.has_method("GetFieldStrength"):
			field_value = float(_field_dial.call("GetFieldStrength"))
		if _field_dial.has_method("GetFieldStateName"):
			field_state = str(_field_dial.call("GetFieldStateName"))
		if _field_dial.has_method("GetExperimentName"):
			experiment = str(_field_dial.call("GetExperimentName"))
		if _field_dial.has_method("GetReferenceAmp"):
			reference_amp = float(_field_dial.call("GetReferenceAmp"))
		if _field_dial.has_method("GetBendScale"):
			bend_scale = float(_field_dial.call("GetBendScale"))
	var rows := "--"
	var scale := "--"
	if _film_camera != null:
		rows = str(_film_camera.get("MaxRowsPerFrameCap"))
		scale = "%0.2f" % float(_film_camera.get("FilmResolutionScale"))
		if _film_camera.get("ComputeResourceProfile") != null:
			compute_profile = str(_film_camera.get("ComputeResourceProfile"))
		if _film_camera.get("ComputeEffectiveWorkers") != null:
			compute_workers = str(_film_camera.get("ComputeEffectiveWorkers"))
		if _film_camera.get("ComputeWatchdogStatus") != null:
			compute_watchdog = str(_film_camera.get("ComputeWatchdogStatus"))
		if _film_camera.get("ComputeResourcePressure") != null:
			compute_pressure = str(_film_camera.get("ComputeResourcePressure"))
	_telemetry_label.text = "Telemetry\nZone: %s | Experiment: %s\nGallery delta: %+0.3f\nEarth delta: %+0.3f\nField: %0.2f / %s\nReference Amp: %0.2f | BendScale: %0.2f\nFilm: %s / %s / %s\nRows cap: %s | scale: %s | compute: %s\nCompute: %s · workers actual/ceiling/host %s · watchdog %s · pressure %s\nMapped-vector graphic: deferred" % [
		zone,
		experiment,
		gallery_delta,
		earth_delta,
		field_value,
		field_state,
		reference_amp,
		bend_scale,
		film_mode,
		quality,
		shading,
		rows,
		scale,
		compute,
		compute_profile,
		compute_workers,
		compute_watchdog,
		compute_pressure,
	]


func _current_zone_name() -> String:
	var camera: Camera3D = _player.get_camera()
	if camera == null or _gallery_portal == null or _earth_portal == null:
		return "Gallery"
	var gallery_distance := camera.global_position.distance_to(_gallery_portal.global_position)
	var earth_distance := camera.global_position.distance_to(_earth_portal.global_position)
	return "Earth" if earth_distance < gallery_distance else "Gallery"
