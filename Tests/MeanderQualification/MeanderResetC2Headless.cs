using Godot;

/// <summary>
/// C-2: Meander context-reset lifecycle qualification.
///
/// Proves the reset state machine invariant:
///   reset occurs
///   → first full pass after reset runs with importance scheduling suppressed
///   → pass completes all bands
///   → importance becomes fresh
///   → next pass resumes importance ordering
///
/// Scene requirements:
///   - GrinFilmCamera child named "GrinFilmCamera" with UpdateEveryFrame=true,
///     UseImportanceOrdering=true, and at least one visible collider body so
///     frontierBands > 0 after pass 1.
///   - RayBeamRenderer child named "RayBeamRenderer" with BendScale=0.
///   - Camera3D active in the viewport.
///
/// The harness waits by PASS COUNT, not wall-clock time. At 80×45 with
/// rowsPerFrame=1, one pass takes 45 frames (~1.5 s at 30 Hz).
/// TimeoutFrames is set to cover 6 full passes.
///
/// Expected log evidence after a successful run:
///   [PixelMemory][LIVE][Reset] oldGeneration=1 newGeneration=2 ...
///     importanceFreshAfter=0 scheduleTotalBandsAfter=0 ...
///   [PixelMemory][LIVE][PassEnd] livePassId=1 ...
///     importanceFreshBeforeCompute=0 importanceFreshAfterCompute=1
///     policyUsed=uniform passComplete=true isFirstPassAfterContextReset=1
///   [PixelMemory][LIVE][PassEnd] livePassId=2 ...
///     importanceFreshBeforeCompute=1 policyUsed=importance passComplete=true
///     isFirstPassAfterContextReset=0
/// </summary>
public partial class MeanderResetC2Headless : Node
{
	private const int TimeoutFrames = 450; // ~10 s at 30 Hz; covers 6 passes

	private GrinFilmCamera _film;
	private RayBeamRenderer _rbr;
	private int _frames;
	private bool _finished;

	// --- phase tracking ---
	private enum Phase
	{
		WaitStableImportance,   // wait for first importance-fresh pass
		TriggerReset,           // change BendScale to force context hash change
		AssertReset,            // verify generation incremented and flags cleared
		WaitFirstPassEnd,       // wait for isFirstPassAfterContextReset → false
		AssertImportanceResumes,// verify importance ordering is active again
		Done
	}

	private Phase _phase = Phase.WaitStableImportance;
	private uint _baselinePassId;
	private uint _baselineGeneration;
	private uint _resetGeneration;
	private uint _firstPassEndId;

	// Snapshot captured at the moment of the Reset event
	private GrinFilmCamera.LiveMeanderStateSnapshot _resetSnapshot;
	// Snapshot captured when first pass after reset completes
	private GrinFilmCamera.LiveMeanderStateSnapshot _firstPassEndSnapshot;

	public override void _Ready()
	{
		// The qualification node is attached to the chamber alongside the
		// production camera/renderer in the Observatory scene.
		_film = GetParent()?.GetNodeOrNull<GrinFilmCamera>("GrinFilmCamera");
		_rbr = GetParent()?.GetNodeOrNull<RayBeamRenderer>("RayBeamRenderer");

		if (_film == null)
		{
			Finish(false, "missing GrinFilmCamera child");
			return;
		}
		if (_rbr == null)
		{
			Finish(false, "missing RayBeamRenderer child — needed to trigger BendScale context reset");
			return;
		}
		if (!_film.UseImportanceOrdering)
		{
			Finish(false, "UseImportanceOrdering=false; set it to true in the scene before running C-2");
			return;
		}
	}

	public override void _Process(double delta)
	{
		if (_finished) return;
		_frames++;
		if (_frames >= TimeoutFrames && _phase != Phase.Done)
		{
			Finish(false, $"timeout at frame {_frames} in phase {_phase}");
			return;
		}

		if (!_film.TryGetLiveMeanderStateForTesting(out var snap))
			return;

		switch (_phase)
		{
			case Phase.WaitStableImportance:
				// Wait until at least one full pass has completed with importance fresh.
				if (snap.ImportanceFreshThisPass && snap.ContextInitialized
					&& snap.ContextGeneration >= 1 && !snap.IsFirstPassAfterContextReset
					&& snap.ScheduleTotalBands > 0
					&& (snap.FrontierBands > 0 || snap.ExplorationBands > 0))
				{
					_baselinePassId = snap.LivePassId;
					_baselineGeneration = snap.ContextGeneration;
					GD.Print($"[C2] Phase 0 complete: stable importance pass. " +
						$"livePassId={_baselinePassId} contextGeneration={_baselineGeneration} " +
						$"frontierBands={snap.FrontierBands} explorationBands={snap.ExplorationBands}");
					if (snap.FrontierBands == 0)
					{
						Finish(false, $"C-1 regression: frontierBands=0 after stable pass (scene may lack visible geometry)");
						return;
					}
					_phase = Phase.TriggerReset;
					// Trigger reset immediately on this frame.
					goto case Phase.TriggerReset;
				}
				break;

			case Phase.TriggerReset:
				// Change BendScale to force a context hash change.
				_rbr.BendScale = _rbr.BendScale < 0.05f ? 0.1f : 0.0f;
				GD.Print($"[C2] Phase 1: BendScale set to {_rbr.BendScale:0.###} to trigger context reset.");
				_phase = Phase.AssertReset;
				break;

			case Phase.AssertReset:
				// Wait for contextGeneration to increment, confirming the reset fired.
				if (snap.ContextGeneration != _baselineGeneration)
				{
					_resetGeneration = snap.ContextGeneration;
					_resetSnapshot = snap;
					GD.Print($"[C2] Phase 2: Reset detected. " +
						$"newGeneration={snap.ContextGeneration} isFirstPassAfterContextReset={snap.IsFirstPassAfterContextReset} " +
						$"scheduleTotalBands={snap.ScheduleTotalBands} importanceFresh={snap.ImportanceFreshThisPass}");

					// Assert reset invariants.
					if (!snap.IsFirstPassAfterContextReset)
					{
						Finish(false, $"FAIL: isFirstPassAfterContextReset=false immediately after reset (generation={snap.ContextGeneration})");
						return;
					}
					if (snap.ScheduleActive)
					{
						Finish(false, $"FAIL: scheduleActive=true immediately after reset");
						return;
					}
					if (snap.ScheduleTotalBands != 0)
					{
						Finish(false, $"FAIL: scheduleTotalBandsAfter={snap.ScheduleTotalBands} (expected 0 after reset)");
						return;
					}
					if (snap.ImportanceFreshThisPass)
					{
						Finish(false, $"FAIL: importanceFreshThisPass=true immediately after reset (must be false)");
						return;
					}
					_phase = Phase.WaitFirstPassEnd;
				}
				break;

			case Phase.WaitFirstPassEnd:
				// Wait for livePassId to increment (first full pass after reset completes).
				if (snap.ContextGeneration == _resetGeneration
					&& !snap.IsFirstPassAfterContextReset
					&& snap.LivePassId > _baselinePassId)
				{
					_firstPassEndId = snap.LivePassId;
					_firstPassEndSnapshot = snap;
					GD.Print($"[C2] Phase 3: First post-reset pass ended. " +
						$"livePassId={snap.LivePassId} importanceFresh={snap.ImportanceFreshThisPass} " +
						$"isFirstPassAfterContextReset={snap.IsFirstPassAfterContextReset}");

					if (!snap.ImportanceFreshThisPass)
					{
						Finish(false, $"FAIL: importanceFreshThisPass=false after first post-reset pass completed (expected true)");
						return;
					}
					_phase = Phase.AssertImportanceResumes;
					goto case Phase.AssertImportanceResumes;
				}
				// Verify invariant holds during the first pass.
				if (snap.ContextGeneration == _resetGeneration
					&& snap.IsFirstPassAfterContextReset
					&& snap.ImportanceFreshThisPass)
				{
					Finish(false, $"FAIL: importanceFreshThisPass=true during first post-reset pass (must be false until pass completes)");
					return;
				}
				break;

			case Phase.AssertImportanceResumes:
				// Importance should be fresh and the next pass should use importance ordering.
				if (!snap.ImportanceFreshThisPass)
				{
					Finish(false, $"FAIL: importanceFreshThisPass=false when checking importance resume (livePassId={snap.LivePassId})");
					return;
				}
				if (snap.ScheduleTotalBands <= 0 || (snap.FrontierBands == 0 && snap.ExplorationBands == 0))
					break;
				GD.Print($"[C2] Phase 4: Importance ordering confirmed resumed. " +
					$"livePassId={snap.LivePassId} contextGeneration={snap.ContextGeneration} " +
					$"frontierBands={snap.FrontierBands} explorationBands={snap.ExplorationBands}");
				if (snap.FrontierBands == 0)
				{
					Finish(false, $"FAIL: C-1 regression — frontierBands=0 after importance resume (contextGeneration={snap.ContextGeneration})");
					return;
				}
				_phase = Phase.Done;
				Finish(true,
					$"PASS: Reset state machine proven. " +
					$"generation {_baselineGeneration}→{_resetGeneration}, " +
					$"first post-reset pass used uniform policy and completed with importanceFreshAfterCompute=true, " +
					$"importance ordering resumed in generation {snap.ContextGeneration}. " +
					$"frontierBands={snap.FrontierBands} explorationBands={snap.ExplorationBands}.");
				break;
		}
	}

	private void Finish(bool pass, string message)
	{
		_finished = true;
		string prefix = pass ? "[C2][PASS]" : "[C2][FAIL]";
		if (pass)
			GD.Print($"{prefix} {message}");
		else
			GD.PrintErr($"{prefix} {message}");
	}
}
