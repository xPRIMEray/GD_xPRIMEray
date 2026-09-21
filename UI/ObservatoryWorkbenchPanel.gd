extends PanelContainer

const RECIPE_ID := "observer_instrumentation_diagnostics"
const FIXTURE_IDS    := ["oi_001", "oi_006", "oi_012"]
const FIXTURE_LABELS := ["OI-001", "OI-006", "OI-012"]
const FIXTURE_NAMES  := ["Equator UV Band", "Checker Diagnostic", "Texture Sample"]

const FIXTURE_CLAIMS := [
	"UV coordinates at the equatorial band land in the expected V-range for all surface hits.",
	"Light and dark checker classifications route consistently with no unresolved diagnostic pixels.",
	"Texture observations match the configured CPU source with zero measured channel difference.",
]
const FIXTURE_BOUNDARIES := [
	"Does not verify UV at poles, seam wrap, or non-equatorial regions.",
	"Verifies probe routing and checker logic; does not verify rendered pixel color accuracy.",
	"Validates the diagnostic read-back path, not the renderer's filtering or mip selection.",
]
# Reserved for future content; EvidenceCard hides the section when the string is empty.
const FIXTURE_WHY := ["", "", ""]

const FIXTURE_PNGS := [
	"",
	"artifacts/oi_007_checker_diagnostic_upscaled.png",
	"artifacts/oi_012_texture_sample_diagnostic_upscaled.png",
]
const FIXTURE_PNGS_FALLBACK := [
	"",
	"artifacts/oi_006_checker_diagnostic.png",
	"artifacts/oi_012_texture_sample_diagnostic.png",
]

# [display_label, metric_key] pairs per fixture.
# metric_key matches keys in summary.json["fixtures"][fid]["metrics"] (primary)
# and in the fixture log PASS/FAIL line (fallback). Same table drives both paths.
const FIXTURE_METRIC_DEFS := [
	# OI-001
	[["Surface hits", "surface"], ["UV in band", "vBand"], ["UV mean", "vMean"],
	 ["UV min", "vMin"], ["UV max", "vMax"], ["Unresolved", "unresolvedAny"]],
	# OI-006
	[["Surface hits", "surface"], ["Checker hits", "checker"], ["Light cells", "light"],
	 ["Dark cells", "dark"], ["Unresolved probe", "unresolvedProbe"], ["UV mismatch", "uvMismatch"]],
	# OI-012
	[["Samples", "samples"], ["Mismatches", "mismatches"], ["Max Δ channel", "maxChannelDelta"],
	 ["Mean Δ channel", "meanChannelDelta"], ["Written pixels", "writtenPixels"], ["Unique pixels", "uniquePixels"]],
]

@onready var _controller: Node = $TestBenchController
@onready var _status_chip: Label = $Margin/Root/Header/StatusChip
@onready var _run_button: Button = $Margin/Root/Actions/RunButton
@onready var _stop_button: Button = $Margin/Root/Actions/StopButton
@onready var _open_bundle_button: Button = $Margin/Root/Actions/OpenBundleButton
@onready var _open_report_button: Button = $Margin/Root/Actions/OpenReportButton
@onready var _evidence_card = $Margin/Root/EvidenceScroll/EvidenceCard
@onready var _optical_only_check: CheckButton = $Margin/Root/PresentationControls/OpticalOnlyCheck
@onready var _demo_mode_check: CheckButton = $Margin/Root/PresentationControls/DemoModeCheck
@onready var _presentation_status: Label = $Margin/Root/PresentationStatus

@onready var _fixture_chips: Array = [
	$Margin/Root/Experiments/Oi001Row/Oi001Chip,
	$Margin/Root/Experiments/Oi006Row/Oi006Chip,
	$Margin/Root/Experiments/Oi012Row/Oi012Chip,
]
@onready var _fixture_btns: Array = [
	$Margin/Root/Experiments/Oi001Row/Oi001Btn,
	$Margin/Root/Experiments/Oi006Row/Oi006Btn,
	$Margin/Root/Experiments/Oi012Row/Oi012Btn,
]

var _bundle_dir := ""
var _verdicts := ["-", "-", "-"]
var _selected := 0
var _is_running := false
var _poll_timer: Timer
var _summary := {}  # Cached after _finalize(); used by _get_fixture_metrics().
var _presentation_host: Node


func _ready() -> void:
	var search_root := get_tree().current_scene
	if search_root == null:
		search_root = get_tree().root
	_presentation_host = _find_presentation_host(search_root)
	_setup_poll_timer()
	_run_button.pressed.connect(_on_run_pressed)
	_stop_button.pressed.connect(_on_stop_pressed)
	_open_bundle_button.pressed.connect(_on_open_bundle_pressed)
	_open_report_button.pressed.connect(_on_open_report_pressed)
	_optical_only_check.toggled.connect(_on_optical_only_toggled)
	_demo_mode_check.toggled.connect(_on_demo_mode_toggled)
	for idx in FIXTURE_IDS.size():
		var btn: Button = _fixture_btns[idx]
		btn.pressed.connect(_on_fixture_selected.bind(idx))
	_set_state_idle()
	_fixture_btns[0].button_pressed = true
	_on_fixture_selected(0)
	_sync_presentation_controls()


func _find_presentation_host(root: Node) -> Node:
	if root == null:
		return null
	if root.has_method("SetOpticalOnlyPresentation"):
		return root
	for child in root.get_children():
		var found := _find_presentation_host(child)
		if found != null:
			return found
	return null


func _on_optical_only_toggled(enabled: bool) -> void:
	if _presentation_host != null:
		_presentation_host.call("SetOpticalOnlyPresentation", enabled)
	_update_presentation_status(enabled)


func _on_demo_mode_toggled(enabled: bool) -> void:
	if _presentation_host != null:
		_presentation_host.call("SetDemoPresentationMode", enabled)
	_optical_only_check.set_pressed_no_signal(enabled)
	_optical_only_check.disabled = enabled or _presentation_host == null
	_update_presentation_status(enabled)


func _sync_presentation_controls() -> void:
	var enabled := false
	if _presentation_host != null:
		enabled = bool(_presentation_host.call("IsOpticalOnlyPresentation"))
	_optical_only_check.disabled = _presentation_host == null
	_demo_mode_check.disabled = _presentation_host == null
	_optical_only_check.set_pressed_no_signal(enabled)
	_update_presentation_status(enabled)


func _update_presentation_status(optical_only: bool) -> void:
	_presentation_status.text = (
		"Observer hides conventional meshes; experiment remains authoritative"
		if optical_only
		else "Normal Godot observer view"
	)


func _setup_poll_timer() -> void:
	_poll_timer = Timer.new()
	_poll_timer.wait_time = 0.35
	_poll_timer.one_shot = false
	_poll_timer.timeout.connect(_poll)
	add_child(_poll_timer)
	_poll_timer.start()


func _on_run_pressed() -> void:
	_bundle_dir = ""
	_verdicts = ["-", "-", "-"]
	_summary = {}
	var result = JSON.parse_string(str(_controller.call(
		"RunJson", RECIPE_ID, "smoke", "none", "none"
	)))
	if typeof(result) != TYPE_DICTIONARY:
		_status_chip.text = "Error: bad response"
		return
	var manifest: Dictionary = result.get("manifest", {})
	_bundle_dir = str(manifest.get("planned_output_folder", ""))
	_set_state_running()


func _on_stop_pressed() -> void:
	_controller.call("StopRun")


func _on_open_bundle_pressed() -> void:
	_controller.call("OpenLatestFolder")


func _on_open_report_pressed() -> void:
	_controller.call("OpenReport")


func _poll() -> void:
	if _controller == null:
		return
	var log_chunk := str(_controller.call("DrainLog"))
	if not log_chunk.is_empty() and _is_running:
		_evidence_card.append_log(log_chunk)
		_apply_live_chips(log_chunk)

	var status = JSON.parse_string(str(_controller.call("GetStatusJson")))
	if typeof(status) != TYPE_DICTIONARY:
		return
	var running: bool = bool(status.get("running", false))
	if not running and _is_running:
		_finalize()


func _apply_live_chips(log_chunk: String) -> void:
	for idx in FIXTURE_IDS.size():
		var label: String = FIXTURE_LABELS[idx]
		if log_chunk.contains("[%s] PASS" % label):
			_verdicts[idx] = "PASS"
		elif log_chunk.contains("[%s] FAIL" % label):
			_verdicts[idx] = "FAIL"
	_refresh_chips()


func _finalize() -> void:
	_is_running = false

	if _bundle_dir.is_empty():
		_set_state_error("No bundle dir.")
		return

	var summary_path := _bundle_dir + "/summary.json"
	if not FileAccess.file_exists(summary_path):
		_set_state_error("summary.json missing.")
		return

	var f := FileAccess.open(summary_path, FileAccess.READ)
	if f == null:
		_set_state_error("Cannot open summary.json.")
		return
	var parsed = JSON.parse_string(f.get_as_text())
	f = null
	if typeof(parsed) != TYPE_DICTIONARY:
		_set_state_error("summary.json unreadable.")
		return

	_summary = parsed

	var fixtures: Dictionary = _summary.get("fixtures", {})
	for idx in FIXTURE_IDS.size():
		var fid: String = FIXTURE_IDS[idx]
		_verdicts[idx] = str(fixtures.get(fid, {}).get("verdict", "-"))

	var overall := str(_summary.get("overall_verdict", "-"))
	if overall == "PASS":
		_status_chip.text = "PASS"
		_status_chip.add_theme_color_override("font_color", Color(0.45, 0.95, 0.58))
	else:
		_status_chip.text = overall
		_status_chip.add_theme_color_override("font_color", Color(1.0, 0.38, 0.32))

	_run_button.disabled = false
	_stop_button.disabled = true
	_open_bundle_button.disabled = false
	_open_report_button.disabled = false
	_refresh_chips()
	_evidence_card.populate(_build_card_data(_selected))


func _on_fixture_selected(idx: int) -> void:
	_selected = idx
	if _is_running:
		# During a run: show claim/boundary for the new selection without clearing
		# the streaming log. Metrics remain as pending placeholders.
		_evidence_card.populate({
			"title": "%s — %s" % [FIXTURE_LABELS[idx], FIXTURE_NAMES[idx]],
			"verdict": "...",
			"claim": FIXTURE_CLAIMS[idx],
			"boundary": FIXTURE_BOUNDARIES[idx],
			"why_it_matters": FIXTURE_WHY[idx],
			"metrics": [],
			"legacy_metrics": false,
			"artifact_path": "",
			"log_text": "",
		})
	else:
		_evidence_card.populate(_build_card_data(idx))


# Assemble the full data dict for EvidenceCard.populate().
func _build_card_data(idx: int) -> Dictionary:
	var metric_result := _get_fixture_metrics(idx)

	var artifact_path := ""
	if not _bundle_dir.is_empty() and not FIXTURE_PNGS[idx].is_empty():
		var primary: String = _bundle_dir + "/" + FIXTURE_PNGS[idx]
		var fallback: String = _bundle_dir + "/" + FIXTURE_PNGS_FALLBACK[idx]
		if FileAccess.file_exists(primary):
			artifact_path = primary
		elif FileAccess.file_exists(fallback):
			artifact_path = fallback

	var log_text := ""
	if not _bundle_dir.is_empty():
		var log_path: String = _bundle_dir + "/logs/" + FIXTURE_IDS[idx] + ".log"
		if FileAccess.file_exists(log_path):
			var lf := FileAccess.open(log_path, FileAccess.READ)
			if lf != null:
				log_text = lf.get_as_text()
				lf = null

	return {
		"title": "%s — %s" % [FIXTURE_LABELS[idx], FIXTURE_NAMES[idx]],
		"verdict": _verdicts[idx],
		"claim": FIXTURE_CLAIMS[idx],
		"boundary": FIXTURE_BOUNDARIES[idx],
		"why_it_matters": FIXTURE_WHY[idx],
		"metrics": metric_result["rows"],
		"legacy_metrics": metric_result["legacy"],
		"artifact_path": artifact_path,
		"log_text": log_text,
	}


# Returns {rows: Array[{key, value}], legacy: bool}.
#
# Primary path: summary.json["fixtures"][fid]["metrics"] — a Dictionary of
# metric_key → value strings. Expected when bundles include structured metrics.
#
# Fallback path: parse the fixture log, merging all [OI-NNN]-prefixed lines.
# Sets legacy=true so the card can display the "legacy" indicator.
func _get_fixture_metrics(idx: int) -> Dictionary:
	var fid: String = FIXTURE_IDS[idx]
	var verdict: String = _verdicts[idx]

	if _bundle_dir.is_empty() or verdict == "-" or verdict == "...":
		return {"rows": [], "legacy": false}

	# Primary: structured metrics block in summary.json
	var fixture_entry: Dictionary = _summary.get("fixtures", {}).get(fid, {})
	if fixture_entry.has("metrics") and typeof(fixture_entry["metrics"]) == TYPE_DICTIONARY:
		return {
			"rows": _apply_metric_defs(idx, fixture_entry["metrics"]),
			"legacy": false,
		}

	# Fallback: parse the fixture log
	var log_path: String = _bundle_dir + "/logs/" + fid + ".log"
	if not FileAccess.file_exists(log_path):
		return {"rows": [], "legacy": false}
	var lf := FileAccess.open(log_path, FileAccess.READ)
	if lf == null:
		return {"rows": [], "legacy": false}
	var raw := _parse_fixture_metrics(lf.get_as_text(), FIXTURE_LABELS[idx])
	lf = null
	return {
		"rows": _apply_metric_defs(idx, raw),
		"legacy": true,
	}


# Apply FIXTURE_METRIC_DEFS ordering to a raw key→value dict, producing
# the [{key: display_label, value}] array that EvidenceCard.populate() expects.
func _apply_metric_defs(idx: int, raw: Dictionary) -> Array:
	var rows := []
	for row_def in FIXTURE_METRIC_DEFS[idx]:
		var display: String = row_def[0]
		var key: String = row_def[1]
		rows.append({"key": display, "value": raw.get(key, "—")})
	return rows


func _parse_kv(line: String) -> Dictionary:
	var result := {}
	for part in line.split(" "):
		if "=" in part:
			var kv := part.split("=", true, 1)
			if kv.size() == 2:
				result[kv[0]] = kv[1]
	return result


func _parse_fixture_metrics(log_text: String, fixture_label: String) -> Dictionary:
	# Merge key=value fields from ALL [OI-NNN]-prefixed lines.
	# Later lines overwrite earlier ones — the acceptance (PASS/FAIL) line wins.
	var prefix := "[%s]" % fixture_label
	var result := {}
	for line in log_text.split("\n"):
		if line.begins_with(prefix):
			result.merge(_parse_kv(line), true)
	return result


func _refresh_chips() -> void:
	for idx in FIXTURE_IDS.size():
		var chip: Label = _fixture_chips[idx]
		chip.text = _verdicts[idx]
		chip.remove_theme_color_override("font_color")
		match _verdicts[idx]:
			"PASS":
				chip.add_theme_color_override("font_color", Color(0.45, 0.95, 0.58))
			"FAIL":
				chip.add_theme_color_override("font_color", Color(1.0, 0.38, 0.32))
			"...":
				chip.add_theme_color_override("font_color", Color(1.0, 0.82, 0.35))


func _set_state_idle() -> void:
	_is_running = false
	_status_chip.text = "Ready"
	_status_chip.remove_theme_color_override("font_color")
	_run_button.disabled = false
	_stop_button.disabled = true
	_open_bundle_button.disabled = _bundle_dir.is_empty()
	_open_report_button.disabled = _bundle_dir.is_empty()


func _set_state_running() -> void:
	_is_running = true
	_status_chip.text = "Running..."
	_status_chip.remove_theme_color_override("font_color")
	_run_button.disabled = true
	_stop_button.disabled = false
	_open_bundle_button.disabled = true
	_open_report_button.disabled = true
	_verdicts = ["...", "...", "..."]
	_refresh_chips()
	_evidence_card.reset_for_run()
	_on_fixture_selected(_selected)


func _set_state_error(msg: String) -> void:
	_status_chip.text = "Error"
	_status_chip.add_theme_color_override("font_color", Color(1.0, 0.38, 0.32))
	_run_button.disabled = false
	_stop_button.disabled = true
	_evidence_card.show_error(msg)
