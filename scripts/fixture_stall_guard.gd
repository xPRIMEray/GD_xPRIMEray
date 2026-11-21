extends RefCounted
## Fail-closed stall guard for single-coroutine SceneTree fixtures (CQ0.5-STAB, B1).
##
## A GDScript runtime error aborts the fixture coroutine without reaching quit(), which leaves the process
## (and any MovieWriter recording) running until an external timeout. The fixture reports progress from its
## frame-wait helpers; when no progress is reported for `stall_frames` consecutive process frames, the guard
## reports a `<TAG> FAIL:` line and quits with exit code 1. A long synchronous call blocks frames, so it can
## never be mistaken for a stall. Keep a reference to the guard for the fixture's lifetime.

var _tree: SceneTree
var _tag: String
var _stall_frames: int
var _since_progress := 0
var _stage := "start"
var _stopped := false


func _init(tree: SceneTree, tag: String, stall_frames: int) -> void:
	_tree = tree
	_tag = tag
	_stall_frames = maxi(1, stall_frames)
	_tree.process_frame.connect(_on_process_frame)


func progress(stage: String = "") -> void:
	_since_progress = 0
	if not stage.is_empty():
		_stage = stage


## Call once the fixture has reached its own terminal quit() (pass or fail).
func stop() -> void:
	_stopped = true


func _on_process_frame() -> void:
	if _stopped:
		return
	_since_progress += 1
	if _since_progress < _stall_frames:
		return
	_stopped = true
	push_error("%s FAIL: fixture stalled for %d frames after stage '%s' (script error or unbounded wait); exiting fail-closed" % [_tag, _stall_frames, _stage])
	_tree.quit(1)
