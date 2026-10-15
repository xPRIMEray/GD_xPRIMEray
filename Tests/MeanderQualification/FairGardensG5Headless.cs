using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Targeted G5 LIVE qualification. This harness observes the production
/// scheduler through GetLiveImportanceScheduleForTesting(); it does not
/// reconstruct or substitute the allocator.
/// </summary>
public partial class FairGardensG5Headless : Node
{
	private const int TimeoutFrames = 1800;
	private const uint FixtureLayer = 1u << 19;
	private const int ExpectedWidth = 160;
	private const int ExpectedHeight = 45;

	private enum QualificationCase
	{
		Rotation,
		ExplorationDemand,
		DeferredPass2,
		ContextResetCancellation
	}

	private enum ResetRecoveryPhase
	{
		AwaitingAbandonment,
		ObservingUniformPass,
		AwaitingResumedImportance,
		Done
	}

	private Node _chamber = null!;
	private GrinFilmCamera _film = null!;
	private RayBeamRenderer _renderer = null!;
	private Node _controller = null!;
	private readonly List<StaticBody3D> _bodies = new();
	private readonly HashSet<ulong> _physicsRids = new();
	private readonly HashSet<ulong> _gardenRids = new();
	private readonly List<GrinFilmCamera.LiveImportanceScheduleTestingSnapshot> _rotationCompleted = new();
	private readonly Dictionary<int, string> _assertions = new();
	private uint _lastCompletedScheduleId;
	private uint _lastObservedScheduleId;
	private int _frames;
	private bool _initialized;
	private bool _deferredBudgetArmed;
	private bool _deferredPendingObserved;
	private int _deferredPendingRow = -1;
	private uint _contextResetScheduleId;
	private uint _contextResetGeneration;
	private bool _contextResetRequested;
	private ResetRecoveryPhase _resetRecoveryPhase = ResetRecoveryPhase.AwaitingAbandonment;
	private uint _recoveryContextGeneration;
	private uint _recoveryStartPassId;
	private readonly List<int> _recoveryCompletedRows = new();
	private int _recoveryFirstCompletedRow = -1;
	private bool _recoveryObservedIncompletePass;
	private bool _finished;
	private int _preparedRotationBefore;
	private QualificationCase _case = QualificationCase.Rotation;
	private uint _rotationContextGeneration;

	public override void _Ready()
	{
		PackedScene observatoryScene = GD.Load<PackedScene>("res://ObservatoryWorkbench.tscn");
		Node observatory = observatoryScene.Instantiate();
		AddChild(observatory);
		CallDeferred(MethodName.InitializeFixture);
	}

	private void InitializeFixture()
	{
		_chamber = GetNode("ObservatoryWorkbench/PlayableWorld/TransportChamberWorld");
		_film = _chamber.GetNode<GrinFilmCamera>("GrinFilmCamera");
		_renderer = _chamber.GetNode<RayBeamRenderer>("RayBeamRenderer");
		_controller = _chamber.GetNode("FilmController");

		// Three vertically separated authoritative physics bodies. Keeping them
		// on a dedicated collision layer excludes the Gallery bodies without
		// changing or removing production apparatus.
		AddGarden(_chamber, "G5GardenLow", new Vector3(0f, 0.15f, 5.0f), new Vector3(3.2f, 0.65f, 0.5f));
		AddGarden(_chamber, "G5GardenMid", new Vector3(0f, 1.75f, 5.0f), new Vector3(3.2f, 0.65f, 0.5f));
		AddGarden(_chamber, "G5GardenHigh", new Vector3(0f, 3.35f, 5.0f), new Vector3(3.2f, 0.65f, 0.5f));

		_renderer.CollisionMask = FixtureLayer;
		_renderer.FieldStrength = 0f;
		_renderer.BendScale = 0f;
		_film.SharedRbrCollisionMask = FixtureLayer;
		_film.Width = ExpectedWidth;
		_film.Height = ExpectedHeight;
		_film.FilmResolutionScale = 1f;
		_film.RowsPerFrame = 1;
		_film.MinRowsPerFrame = 1;
		_film.MaxRowsPerFrameCap = 1;
		_film.UpdateEveryFrameMaxRowsPerStep = 1;
		_film.UpdateEveryFrameBudgetMs = 1000f;
		_film.RenderStepMaxMs = 1000;
		_film.UseThreadedBands = false;
		_film.UseImportanceOrdering = true;
		_film.MinExplorationBandFraction = 0.20f;
		_film.MaxFrontierBandFraction = 0.05f; // floor(45 * .05) = two slots
		_film.DebugSnapshotLog = false;

		// Use the same production mode transition as interactive G.
		_controller.Call("set_mode", 2);
		// FilmController applies Preview quality on LIVE entry. Restore only the
		// fixture's resolution/row quantum, then start a clean LIVE context.
		_film.FilmResolutionScale = 1f;
		_film.RowsPerFrame = 1;
		_film.MinRowsPerFrame = 1;
		_film.MaxRowsPerFrameCap = 1;
		_film.UpdateEveryFrameMaxRowsPerStep = 1;
		_film.UpdateEveryFrameBudgetMs = 1000f;
		_film.RenderStepMaxMs = 1000;
		_film.ResetFilmPassManual();
		_initialized = true;

		GD.Print($"[G5][START] film={ExpectedWidth}x{ExpectedHeight} frontierFraction={_film.MaxFrontierBandFraction:0.###} " +
			$"explorationFraction={_film.MinExplorationBandFraction:0.###} physicsRids=[{FormatRids(_physicsRids)}]");
	}

	private void AddGarden(Node parent, string name, Vector3 position, Vector3 size)
	{
		var body = new StaticBody3D
		{
			Name = name,
			Position = position,
			CollisionLayer = FixtureLayer,
			CollisionMask = 0
		};
		body.AddToGroup("fixture_geometry");
		body.AddToGroup("raytrace_geometry");
		var shape = new CollisionShape3D { Shape = new BoxShape3D { Size = size } };
		body.AddChild(shape);
		parent.AddChild(body);
		_bodies.Add(body);
		_physicsRids.Add(body.GetRid().Id);
	}

	private void AddExplorationEnvelope()
	{
		var envelope = new StaticBody3D
		{
			Name = "G5ExplorationBackfaceStrip",
			CollisionLayer = FixtureLayer,
			CollisionMask = 0
		};
		envelope.AddToGroup("fixture_geometry");
		envelope.AddToGroup("raytrace_geometry");
		// Winding gives both triangles a -Z normal, away from the camera at +Z.
		// The overlap broadphase sees the strip, while normal ray resolution with
		// HitBackFaces=false rejects it as a contact.
		var backfaceStrip = new ConcavePolygonShape3D
		{
			Data = new Vector3[]
			{
				new(-6f, 2.30f, 5f), new(-6f, 2.80f, 5f), new(6f, 2.80f, 5f),
				new(-6f, 2.30f, 5f), new(6f, 2.80f, 5f), new(6f, 2.30f, 5f)
			}
		};
		var shape = new CollisionShape3D { Shape = backfaceStrip };
		envelope.AddChild(shape);
		_chamber.AddChild(envelope);
		_physicsRids.Add(envelope.GetRid().Id);

		// Disable TLAS pruning only in this fixture context so real Godot
		// broadphase/physics outcomes reach PixelMemory classification.
		_film.UseGeometryTLASPruning = false;
		_film.ResetFilmPassManual();
		GD.Print($"[G5][CASE B START] stripRid={envelope.GetRid().Id:x16} y=[2.30,2.80] normal=-Z geomTlasPruning=0");
	}

	public override void _Process(double delta)
	{
		if (_finished || !_initialized)
			return;
		_frames++;
		if (_frames > TimeoutFrames)
		{
			Finish(false, $"timeout after {_frames} frames; phase={_case} rotationCompleted={_rotationCompleted.Count} " +
				$"deferredPending={_deferredPendingObserved} resetRequested={_contextResetRequested}");
			return;
		}

		GrinFilmCamera.LiveImportanceScheduleTestingSnapshot snapshot = _film.GetLiveImportanceScheduleForTesting();
		if (_film.TryGetColliderHitActivityForTesting(out var hitActivity))
		{
			int priorCount = _gardenRids.Count;
			foreach (var entry in hitActivity)
				_gardenRids.Add(entry.ColliderId);
			if (_gardenRids.Count != priorCount)
				GD.Print($"[G5][HIT_ENTITIES] [{FormatRids(_gardenRids)}]");
		}

		if (_case == QualificationCase.ContextResetCancellation
			&& _resetRecoveryPhase != ResetRecoveryPhase.AwaitingAbandonment)
		{
			ObserveContextResetRecovery(snapshot);
			return;
		}

		if (snapshot.ScheduleId != 0 && snapshot.ScheduleId != _lastObservedScheduleId)
		{
			_lastObservedScheduleId = snapshot.ScheduleId;
			if (snapshot.State == GrinFilmCamera.LiveImportanceScheduleDiagnosticState.Prepared)
			{
				_preparedRotationBefore = snapshot.RotationOffsetBefore;
				if (snapshot.RotationOffsetAfter != snapshot.RotationOffsetBefore)
				{
					FailAssertion(9, $"rotation changed on preparation {snapshot.RotationOffsetBefore}->{snapshot.RotationOffsetAfter}");
					Finish(false, "rotation advanced on preparation");
					return;
				}

				if (_case == QualificationCase.ContextResetCancellation && !_contextResetRequested)
				{
					_contextResetScheduleId = snapshot.ScheduleId;
					_contextResetGeneration = snapshot.ContextGeneration;
					_contextResetRequested = true;
					_renderer.BendScale = _renderer.BendScale < 0.05f ? 0.1f : 0f;
					GD.Print($"[G5][CONTEXT RESET REQUEST] context={_contextResetGeneration} schedule={_contextResetScheduleId} " +
						$"rotation={snapshot.RotationOffsetBefore} selected={snapshot.ExecutedBands.Count} bendScale={_renderer.BendScale:0.###}");
				}
			}
		}

		if (_case == QualificationCase.DeferredPass2
			&& !_deferredPendingObserved
			&& snapshot.State == GrinFilmCamera.LiveImportanceScheduleDiagnosticState.Prepared
			&& snapshot.ExecutedBands.Any(b => !b.Completed))
		{
			var pending = snapshot.ExecutedBands.Single(b => !b.Completed);
			_deferredPendingObserved = true;
			_deferredPendingRow = pending.RowStart;
			_film.RenderStepMaxMs = 1000;
			_film.UpdateEveryFrameBudgetMs = 1000f;
			GD.Print($"[G5][DEFERRED PENDING] context={snapshot.ContextGeneration} pass={snapshot.LivePassId} " +
				$"schedule={snapshot.ScheduleId} row={pending.RowStart} rotation={snapshot.RotationOffsetBefore}");
		}

		if (snapshot.State == GrinFilmCamera.LiveImportanceScheduleDiagnosticState.Completed
			&& snapshot.ScheduleId != _lastCompletedScheduleId)
		{
			_lastCompletedScheduleId = snapshot.ScheduleId;
			string caseLabel = _case switch
			{
				QualificationCase.Rotation => $"ROTATION_{_rotationCompleted.Count + 1}_COMPLETED",
				QualificationCase.ExplorationDemand => "EXPLORATION_COMPLETED",
				QualificationCase.DeferredPass2 => "DEFERRED_COMPLETED",
				_ => "UNEXPECTED_COMPLETED"
			};
			PrintSnapshot(caseLabel, snapshot);
			bool requireExploration = _case != QualificationCase.Rotation;
			if (!ValidateCompletedSnapshot(snapshot, requireExploration, caseLabel))
			{
				Finish(false, $"completed schedule {snapshot.ScheduleId} violated an assertion");
				return;
			}

			if (_case == QualificationCase.Rotation)
			{
				if (_rotationCompleted.Count == 0)
					_rotationContextGeneration = snapshot.ContextGeneration;
				if (snapshot.ContextGeneration != _rotationContextGeneration)
				{
					Finish(false, $"rotation context changed {_rotationContextGeneration}->{snapshot.ContextGeneration}");
					return;
				}
				_rotationCompleted.Add(snapshot);
				if (_rotationCompleted.Count < 3)
					return;
				if (!ValidateRotation())
				{
					Finish(false, "successive-pass fairness rotation failed");
					return;
				}
				_case = QualificationCase.ExplorationDemand;
				AddExplorationEnvelope();
				return;
			}

			if (_case == QualificationCase.ExplorationDemand)
			{
				if (snapshot.ContextGeneration == _rotationContextGeneration)
				{
					Finish(false, "exploration case did not create a fresh LIVE context");
					return;
				}
				_case = QualificationCase.DeferredPass2;
				_deferredBudgetArmed = true;
				_film.RenderStepMaxMs = 1;
				_film.UpdateEveryFrameBudgetMs = 1000f;
				GD.Print("[G5][DEFERRED START] RenderStepMaxMs=1; waiting for genuine max_ms_after_pass1 pending pass2");
				return;
			}

			if (_case == QualificationCase.DeferredPass2)
			{
				if (!ValidateDeferredPass2Snapshot(snapshot))
				{
					Finish(false, "deferred pass2 lifecycle failed");
					return;
				}
				_case = QualificationCase.ContextResetCancellation;
				return;
			}

			Finish(false, $"schedule {snapshot.ScheduleId} completed after context-reset cancellation was requested");
			return;
		}

		// Fail fast if production completes LIVE passes while the same prepared
		// schedule never records/advances its bands. This is a scheduler execution
		// defect, not a fixture timeout or a reconstructed expectation.
		if (snapshot.State == GrinFilmCamera.LiveImportanceScheduleDiagnosticState.Prepared
			&& _film.TryGetLiveMeanderStateForTesting(out var meander)
			&& meander.LivePassId >= snapshot.LivePassId + 2)
		{
			PrintSnapshot("STALLED_PREPARED", snapshot);
			ValidatePreparedSnapshot(snapshot);
			FailAssertion(5, $"actual selections did not consume prepared order; livePass={meander.LivePassId} preparedPass={snapshot.LivePassId}");
			FailAssertion(6, $"completedBands={meander.ScheduleCompletedBands} totalBands={meander.ScheduleTotalBands}");
			Finish(false, "production importance schedule remained Prepared across completed LIVE passes");
			return;
		}

		if (_contextResetRequested
			&& snapshot.ScheduleId == _contextResetScheduleId
			&& snapshot.State == GrinFilmCamera.LiveImportanceScheduleDiagnosticState.Abandoned)
		{
			PrintSnapshot("CONTEXT_RESET_ABANDONED", snapshot);
			if (!ValidateContextResetSnapshot(snapshot))
			{
				Finish(false, "context-reset cancellation failed");
				return;
			}
			if (!_film.TryGetLiveMeanderStateForTesting(out var resetState))
			{
				FailAssertion(13, "LIVE Meander state unavailable after context reset");
				Finish(false, "post-reset recovery state unavailable");
				return;
			}
			_recoveryContextGeneration = resetState.ContextGeneration;
			_recoveryStartPassId = resetState.LivePassId;
			_recoveryCompletedRows.Clear();
			_recoveryFirstCompletedRow = -1;
			RecordRecoveryRowProgress(resetState.RowCursor);
			_recoveryObservedIncompletePass = resetState.IsFirstPassAfterContextReset
				&& resetState.RowCursor > 0 && resetState.RowCursor < ExpectedHeight;
			_resetRecoveryPhase = ResetRecoveryPhase.ObservingUniformPass;
			GD.Print($"[C2][RESET ACCEPTED] oldContext={_contextResetGeneration} newContext={_recoveryContextGeneration} " +
				$"startPass={_recoveryStartPassId} rowCursor={resetState.RowCursor} rotation=0");
		}
	}

	private void ObserveContextResetRecovery(GrinFilmCamera.LiveImportanceScheduleTestingSnapshot snapshot)
	{
		if (!_film.TryGetLiveMeanderStateForTesting(out var meander))
		{
			FailAssertion(13, "LIVE Meander state unavailable during post-reset recovery");
			Finish(false, "post-reset recovery state unavailable");
			return;
		}
		if (meander.ContextGeneration != _recoveryContextGeneration)
		{
			FailAssertion(13, $"context changed during recovery {_recoveryContextGeneration}->{meander.ContextGeneration}");
			Finish(false, "post-reset recovery context changed unexpectedly");
			return;
		}

		if (_resetRecoveryPhase == ResetRecoveryPhase.ObservingUniformPass)
		{
			if (meander.IsFirstPassAfterContextReset)
			{
				_recoveryObservedIncompletePass |= meander.RowCursor > 0 && meander.RowCursor < ExpectedHeight;
				if (meander.ImportanceFreshThisPass || meander.ScheduleActive || meander.ScheduleTotalBands != 0
					|| snapshot.State == GrinFilmCamera.LiveImportanceScheduleDiagnosticState.Prepared
						&& snapshot.ContextGeneration == _recoveryContextGeneration)
				{
					FailAssertion(13, $"premature importance state row={meander.RowCursor} fresh={meander.ImportanceFreshThisPass} " +
						$"active={meander.ScheduleActive} total={meander.ScheduleTotalBands} diagnostic={snapshot.State}");
					Finish(false, "importance scheduling resumed before recovery pass completion");
					return;
				}
				if (!RecordRecoveryRowProgress(meander.RowCursor))
				{
					Finish(false, "post-reset recovery row traversal was not uniform/sequential");
					return;
				}
				return;
			}

			// The production boundary computes importance, clears the first-pass flag,
			// increments LivePassId, and resets the row cursor as one completed pass.
			if (meander.LivePassId <= _recoveryStartPassId || !meander.ImportanceFreshThisPass)
			{
				FailAssertion(15, $"invalid completion boundary startPass={_recoveryStartPassId} livePass={meander.LivePassId} " +
					$"fresh={meander.ImportanceFreshThisPass} firstAfterReset={meander.IsFirstPassAfterContextReset}");
				Finish(false, "post-reset recovery pass did not commit freshness at completion");
				return;
			}
			if (_recoveryCompletedRows.Count > 0 && _recoveryCompletedRows[^1] == ExpectedHeight - 2)
				_recoveryCompletedRows.Add(ExpectedHeight - 1);
			bool rowsComplete = _recoveryFirstCompletedRow >= 0
				&& _recoveryCompletedRows.SequenceEqual(Enumerable.Range(
					_recoveryFirstCompletedRow,
					ExpectedHeight - _recoveryFirstCompletedRow));
			if (!Check(13, _recoveryObservedIncompletePass,
				$"observedIncomplete={_recoveryObservedIncompletePass} startPass={_recoveryStartPassId}"))
			{
				Finish(false, "first post-reset pass was not observed while incomplete");
				return;
			}
			if (!Check(14, rowsComplete,
				$"uniformRows=[{string.Join(',', _recoveryCompletedRows)}] " +
				$"expected={_recoveryFirstCompletedRow}..{ExpectedHeight - 1}"))
			{
				Finish(false, "post-reset recovery row traversal was incomplete or non-sequential");
				return;
			}
			if (!Check(15, meander.LivePassId == _recoveryStartPassId + 1
				&& !meander.IsFirstPassAfterContextReset && meander.ImportanceFreshThisPass,
				$"pass={_recoveryStartPassId}->{meander.LivePassId} fresh={meander.ImportanceFreshThisPass} " +
				$"firstAfterReset={meander.IsFirstPassAfterContextReset}"))
			{
				Finish(false, "post-reset completion boundary invariant failed");
				return;
			}
			_resetRecoveryPhase = ResetRecoveryPhase.AwaitingResumedImportance;
			GD.Print($"[C2][RECOVERY PASS COMPLETE] context={meander.ContextGeneration} pass={_recoveryStartPassId}->{meander.LivePassId} " +
				$"rows=[{string.Join(',', _recoveryCompletedRows)}] importanceFresh=1");
			return;
		}

		if (_resetRecoveryPhase != ResetRecoveryPhase.AwaitingResumedImportance)
			return;
		if (!meander.ImportanceFreshThisPass)
		{
			FailAssertion(16, $"importance lost freshness while awaiting resumed schedule pass={meander.LivePassId}");
			Finish(false, "importance freshness did not persist into resumed scheduling");
			return;
		}
		if (!meander.ScheduleActive || meander.ScheduleTotalBands <= 0
			|| snapshot.State != GrinFilmCamera.LiveImportanceScheduleDiagnosticState.Prepared
			|| snapshot.ScheduleId == _contextResetScheduleId)
			return;

		var prioritized = snapshot.PreparedBands
			.Where(b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.PrioritizedFrontier)
			.ToArray();
		bool authenticFrontier = meander.FrontierBands > 0 && meander.ActiveGardenCount > 0
			&& snapshot.PreparedBands.Any(b => b.FrontierEligible)
			&& prioritized.Length > 0
			&& prioritized.All(b => b.DominantEntity != 0 && _gardenRids.Contains(b.DominantEntity));
		bool newContextSchedule = snapshot.ContextGeneration == _recoveryContextGeneration
			&& snapshot.LivePassId == meander.LivePassId
			&& snapshot.ScheduleId > _contextResetScheduleId;
		bool resetIsolation = snapshot.RotationOffsetBefore == 0 && snapshot.RotationOffsetAfter == 0
			&& meander.StartingRotationOffset == 0
			&& snapshot.PreparedBands.Count == ExpectedHeight
			&& snapshot.PreparedBands.Select(b => b.RowStart).Distinct().Count() == ExpectedHeight
			&& snapshot.PreparedBands.Select(b => b.RowStart).OrderBy(v => v)
				.SequenceEqual(Enumerable.Range(0, ExpectedHeight));
		bool resumed = Check(16, newContextSchedule && authenticFrontier,
			$"context={snapshot.ContextGeneration} pass={snapshot.LivePassId} schedule={snapshot.ScheduleId} " +
			$"frontierBands={meander.FrontierBands} activeGardens={meander.ActiveGardenCount} " +
			$"owners=[{FormatRidsInOrder(prioritized.Select(b => b.DominantEntity))}]");
		bool isolated = Check(17, resetIsolation,
			$"rotation={snapshot.RotationOffsetBefore}->{snapshot.RotationOffsetAfter} " +
			$"rows={snapshot.PreparedBands.Count} uniqueRows={snapshot.PreparedBands.Select(b => b.RowStart).Distinct().Count()}");
		PrintSnapshot("C2_IMPORTANCE_RESUMED", snapshot);
		_resetRecoveryPhase = ResetRecoveryPhase.Done;
		Finish(resumed && isolated,
			resumed && isolated
				? "rotation, exploration, deferred pass2, context-reset cancellation, and C2 reset recovery passed"
				: "C2 importance-resume or reset-isolation invariant failed");
	}

	private bool RecordRecoveryRowProgress(int rowCursor)
	{
		if (rowCursor <= 0)
			return true;
		int completedRow = rowCursor - 1;
		if (_recoveryCompletedRows.Count > 0 && _recoveryCompletedRows[^1] == completedRow)
			return true;
		if (_recoveryCompletedRows.Count == 0)
		{
			_recoveryFirstCompletedRow = completedRow;
			_recoveryCompletedRows.Add(completedRow);
			return true;
		}
		int expectedRow = _recoveryCompletedRows[^1] + 1;
		if (completedRow != expectedRow)
		{
			FailAssertion(14, $"row traversal expected={expectedRow} observedCompleted={completedRow} rowCursor={rowCursor} " +
				$"rows=[{string.Join(',', _recoveryCompletedRows)}]");
			return false;
		}
		_recoveryCompletedRows.Add(completedRow);
		return true;
	}

	private void ValidatePreparedSnapshot(GrinFilmCamera.LiveImportanceScheduleTestingSnapshot snapshot)
	{
		int[] preparedRows = snapshot.PreparedBands.Select(b => b.RowStart).ToArray();
		Check(1, preparedRows.Length == ExpectedHeight
			&& preparedRows.Distinct().Count() == ExpectedHeight
			&& preparedRows.OrderBy(v => v).SequenceEqual(Enumerable.Range(0, ExpectedHeight)),
			$"prepared unique coverage count={preparedRows.Length} expected={ExpectedHeight}");
		int lastExploration = Array.FindLastIndex(snapshot.PreparedBands.ToArray(), b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.Exploration);
		int firstPrioritized = Array.FindIndex(snapshot.PreparedBands.ToArray(), b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.PrioritizedFrontier);
		Check(2, lastExploration >= 0 && firstPrioritized > lastExploration,
			$"explorationLast={lastExploration} prioritizedFirst={firstPrioritized}");
		var prioritized = snapshot.PreparedBands.Where(b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.PrioritizedFrontier).ToArray();
		Check(3, _gardenRids.Count == 3 && prioritized.Length == 2
			&& prioritized.All(b => b.DominantEntity != 0 && _gardenRids.Contains(b.DominantEntity)),
			$"hitEntities=[{FormatRids(_gardenRids)}] prioritizedOwners=[{string.Join(',', prioritized.Select(b => b.DominantEntity.ToString("x16")))}]");
		Check(4, prioritized.All(b => b.DominantEntity != 0), "RID 0 absent from prioritized gardens");
	}

	private bool ValidateCompletedSnapshot(
		GrinFilmCamera.LiveImportanceScheduleTestingSnapshot snapshot,
		bool requireExploration,
		string caseLabel)
	{
		bool ok = true;
		var preparedRows = snapshot.PreparedBands.Select(b => b.RowStart).ToArray();
		var selectedRows = snapshot.ExecutedBands.Select(b => b.RowStart).ToArray();
		var completedRows = snapshot.ExecutedBands.Where(b => b.Completed).Select(b => b.RowStart).ToArray();
		int bandHeight = preparedRows.Length > 1
			? preparedRows.OrderBy(v => v).Zip(preparedRows.OrderBy(v => v).Skip(1), (a, b) => b - a).Where(v => v > 0).DefaultIfEmpty(1).Min()
			: 1;
		int expectedBands = (ExpectedHeight + bandHeight - 1) / bandHeight;

		ok &= Check(1, preparedRows.Distinct().Count() == preparedRows.Length
			&& preparedRows.Length == expectedBands
			&& preparedRows.OrderBy(v => v).SequenceEqual(Enumerable.Range(0, expectedBands).Select(i => i * bandHeight)),
			$"prepared unique coverage count={preparedRows.Length} expected={expectedBands} bandHeight={bandHeight}");

		int lastExploration = Array.FindLastIndex(snapshot.PreparedBands.ToArray(), b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.Exploration);
		int firstPrioritized = Array.FindIndex(snapshot.PreparedBands.ToArray(), b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.PrioritizedFrontier);
		int explorationEligible = snapshot.PreparedBands.Count(b => b.BandCategory == GrinFilmCamera.LiveImportanceBandDiagnosticCategory.Exploration);
		int explorationSelected = snapshot.PreparedBands.Count(b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.Exploration);
		if (requireExploration)
		{
			ok &= Check(2, explorationEligible > 0 && explorationSelected > 0
				&& lastExploration >= 0 && firstPrioritized > lastExploration,
				$"case={caseLabel} eligible={explorationEligible} selected={explorationSelected} " +
				$"explorationLast={lastExploration} prioritizedFirst={firstPrioritized}");
		}
		else
		{
			bool noDemand = explorationEligible == 0 && explorationSelected == 0;
			GD.Print($"[G5][ASSERT 2][NOT EXERCISED] case={caseLabel} eligible={explorationEligible} selected={explorationSelected}");
			ok &= noDemand;
		}

		var prioritized = snapshot.PreparedBands.Where(b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.PrioritizedFrontier).ToArray();
		bool ownersValid = prioritized.Length == 2
			&& prioritized.All(b => b.FrontierEligible && b.DominantEntity != 0 && _gardenRids.Contains(b.DominantEntity));
		ok &= Check(3, ownersValid, $"prioritizedOwners=[{string.Join(',', prioritized.Select(b => b.DominantEntity.ToString("x16")))}]");
		ok &= Check(4, prioritized.All(b => b.DominantEntity != 0), "RID 0 absent from prioritized gardens");
		ok &= Check(5, selectedRows.SequenceEqual(preparedRows), "selected order equals prepared order");
		ok &= Check(6, snapshot.ExecutedBands.Count == snapshot.PreparedBands.Count
			&& snapshot.ExecutedBands.All(b => b.Completed)
			&& completedRows.Distinct().Count() == completedRows.Length,
			$"executed={snapshot.ExecutedBands.Count} prepared={snapshot.PreparedBands.Count} uniqueCompleted={completedRows.Distinct().Count()}");
		ok &= Check(7, snapshot.ExecutedBands.GroupBy(b => b.PreparedSelectionOrdinal).All(g => g.Count() == 1),
			$"execution records={snapshot.ExecutedBands.Count} deferred={snapshot.ExecutedBands.Count(b => b.DeferredPass2CompletedNormally)}");
		ok &= Check(9, snapshot.RotationOffsetBefore == _preparedRotationBefore,
			$"preparedRotation={_preparedRotationBefore} completedBefore={snapshot.RotationOffsetBefore}");

		var allocation = prioritized.GroupBy(b => b.DominantEntity).ToDictionary(g => g.Key, g => g.Count());
		ok &= Check(11, prioritized.Length == 2 && allocation.Values.Sum() == 2 && allocation.Values.All(v => v == 1),
			$"prioritized={prioritized.Length} allocations=[{string.Join(',', allocation.Select(p => $"{p.Key:x16}:{p.Value}"))}]");
		return ok;
	}

	private bool ValidateRotation()
	{
		ulong[][] eligibleByPass = _rotationCompleted.Select(snapshot => snapshot.PreparedBands
			.Where(b => b.FrontierEligible && b.DominantEntity != 0)
			.Select(b => b.DominantEntity)
			.Distinct()
			.OrderBy(v => v)
			.ToArray()).ToArray();
		bool stableEligibility = eligibleByPass.Length == 3
			&& eligibleByPass.All(eligible => eligible.SequenceEqual(eligibleByPass[0]));
		if (!stableEligibility || eligibleByPass[0].Length <= 2)
		{
			_assertions[8] = "NOT TESTED";
			GD.Print($"[G5][ASSERT 8][NOT TESTED] eligibility changed or insufficient: " +
				string.Join(" | ", eligibleByPass.Select((eligible, index) => $"pass{index + 1}=[{FormatRids(eligible)}]")));
			return true;
		}

		ulong[] sortedGardens = eligibleByPass[0];
		bool offsetsRotate = true;
		bool prefixMatches = true;
		var allocationDetails = new List<string>();
		for (int passIndex = 0; passIndex < _rotationCompleted.Count; passIndex++)
		{
			var snapshot = _rotationCompleted[passIndex];
			ulong[] owners = snapshot.PreparedBands
				.Where(b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.PrioritizedFrontier)
				.Select(b => b.DominantEntity).ToArray();
			ulong[] expected = Enumerable.Range(0, owners.Length)
				.Select(offset => sortedGardens[(snapshot.RotationOffsetBefore + offset) % sortedGardens.Length])
				.ToArray();
			int expectedAfter = (snapshot.RotationOffsetBefore + owners.Length) % sortedGardens.Length;
			prefixMatches &= owners.SequenceEqual(expected);
			offsetsRotate &= snapshot.RotationOffsetAfter == expectedAfter;
			if (passIndex > 0)
				offsetsRotate &= _rotationCompleted[passIndex - 1].RotationOffsetAfter == snapshot.RotationOffsetBefore;
			allocationDetails.Add($"schedule={snapshot.ScheduleId} eligible=[{FormatRids(sortedGardens)}] " +
				$"owners=[{FormatRidsInOrder(owners)}] rotation={snapshot.RotationOffsetBefore}->{snapshot.RotationOffsetAfter}");
		}
		bool allPersistentGardensReached = _rotationCompleted
			.SelectMany(s => s.PreparedBands.Where(b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.PrioritizedFrontier))
			.Select(b => b.DominantEntity).Distinct().OrderBy(v => v).SequenceEqual(sortedGardens);
		return Check(8, offsetsRotate && prefixMatches && allPersistentGardensReached,
			string.Join(" | ", allocationDetails));
	}

	private bool ValidateDeferredPass2Snapshot(GrinFilmCamera.LiveImportanceScheduleTestingSnapshot snapshot)
	{
		bool ok = true;
		var deferred = snapshot.ExecutedBands.Where(b => b.DeferredPass2CompletedNormally).ToArray();
		ok &= Check(7, _deferredBudgetArmed && _deferredPendingObserved
			&& deferred.Length == 1
			&& deferred[0].RowStart == _deferredPendingRow
			&& snapshot.ExecutedBands.GroupBy(b => b.PreparedSelectionOrdinal).All(g => g.Count() == 1),
			$"pendingObserved={_deferredPendingObserved} pendingRow={_deferredPendingRow} " +
			$"deferredRows=[{string.Join(',', deferred.Select(b => b.RowStart))}] records={snapshot.ExecutedBands.Count}");
		ok &= Check(12, deferred.Length == 1
			&& !snapshot.ExecutedBands.Any(b => b.RowStart == _deferredPendingRow
				&& b.CompletionPath == GrinFilmCamera.LiveImportanceBandCompletionPath.Normal),
			$"pendingRow={_deferredPendingRow} terminalPaths=[{string.Join(',', snapshot.ExecutedBands.Where(b => b.RowStart == _deferredPendingRow).Select(b => b.CompletionPath))}]");
		return ok;
	}

	private bool ValidateContextResetSnapshot(GrinFilmCamera.LiveImportanceScheduleTestingSnapshot snapshot)
	{
		bool hasState = _film.TryGetLiveMeanderStateForTesting(out var meander);
		bool uniqueSelections = snapshot.ExecutedBands.GroupBy(b => b.PreparedSelectionOrdinal).All(g => g.Count() == 1);
		bool uniqueCompletions = snapshot.ExecutedBands.Where(b => b.Completed)
			.Select(b => b.PreparedSelectionOrdinal).Distinct().Count()
			== snapshot.ExecutedBands.Count(b => b.Completed);
		bool ok = true;
		ok &= Check(10, snapshot.State == GrinFilmCamera.LiveImportanceScheduleDiagnosticState.Abandoned
			&& snapshot.TerminalReason == "context_reset"
			&& !snapshot.SequentialFallbackActivated
			&& snapshot.RotationOffsetAfter == snapshot.RotationOffsetBefore,
			$"state={snapshot.State} reason={snapshot.TerminalReason} fallback={snapshot.SequentialFallbackActivated} " +
			$"rotation={snapshot.RotationOffsetBefore}->{snapshot.RotationOffsetAfter}");
		ok &= Check(12, snapshot.ScheduleId == _contextResetScheduleId
			&& snapshot.ContextGeneration == _contextResetGeneration
			&& uniqueSelections && uniqueCompletions
			&& hasState
			&& meander.ContextGeneration > _contextResetGeneration
			&& !meander.ScheduleActive
			&& meander.ScheduleTotalBands == 0
			&& !meander.ImportanceFreshThisPass
			&& meander.IsFirstPassAfterContextReset,
			$"oldContext={_contextResetGeneration} newContext={meander.ContextGeneration} " +
			$"scheduleActive={meander.ScheduleActive} totalBands={meander.ScheduleTotalBands} " +
			$"importanceFresh={meander.ImportanceFreshThisPass} firstAfterReset={meander.IsFirstPassAfterContextReset} " +
			$"records={snapshot.ExecutedBands.Count} uniqueSelections={uniqueSelections} uniqueCompletions={uniqueCompletions}");
		return ok;
	}

	private bool Check(int assertion, bool condition, string detail)
	{
		if (!_assertions.ContainsKey(assertion))
			_assertions[assertion] = condition ? "PASS" : "FAIL";
		else if (!condition)
			_assertions[assertion] = "FAIL";
		GD.Print($"[G5][ASSERT {assertion}] {(condition ? "PASS" : "FAIL")} {detail}");
		return condition;
	}

	private void FailAssertion(int assertion, string detail)
	{
		_assertions[assertion] = "FAIL";
		GD.PrintErr($"[G5][ASSERT {assertion}] FAIL {detail}");
	}

	private static void PrintSnapshot(string label, GrinFilmCamera.LiveImportanceScheduleTestingSnapshot snapshot)
	{
		string prepared = string.Join(',', snapshot.PreparedBands.Select(b =>
			$"{b.RowStart}:{b.SelectionPath}:{b.DominantEntity:x16}"));
		string selected = string.Join(',', snapshot.ExecutedBands.Select(b =>
			$"{b.RowStart}:{b.CompletionPath}:{(b.Completed ? 1 : 0)}"));
		var allocation = snapshot.PreparedBands
			.Where(b => b.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.PrioritizedFrontier)
			.GroupBy(b => b.DominantEntity)
			.Select(g => $"{g.Key:x16}:{g.Count()}");
		GD.Print($"[G5][{label}] context={snapshot.ContextGeneration} pass={snapshot.LivePassId} schedule={snapshot.ScheduleId} " +
			$"rotation={snapshot.RotationOffsetBefore}->{snapshot.RotationOffsetAfter} fallback={snapshot.SequentialFallbackActivated} reason={snapshot.TerminalReason} " +
			$"allocations=[{string.Join(',', allocation)}] prepared=[{prepared}] selected=[{selected}]");
	}

	private void Finish(bool pass, string message)
	{
		if (_finished) return;
		_finished = true;
		_controller?.Call("set_mode", 0);
		for (int assertion = 1; assertion <= 17; assertion++)
		{
			if (!_assertions.ContainsKey(assertion))
				_assertions[assertion] = "NOT TESTED";
		}
		string summary = string.Join(' ', Enumerable.Range(1, 17).Select(i => $"{i}={_assertions[i]}"));
		if (pass)
			GD.Print($"[G5][PASS] {message} {summary}");
		else
			GD.PrintErr($"[G5][FAIL] {message} {summary}");
		GetTree().Quit(pass ? 0 : 1);
	}

	private static string FormatRids(IEnumerable<ulong> values) =>
		string.Join(',', values.OrderBy(v => v).Select(v => v.ToString("x16")));

	private static string FormatRidsInOrder(IEnumerable<ulong> values) =>
		string.Join(',', values.Select(v => v.ToString("x16")));
}
