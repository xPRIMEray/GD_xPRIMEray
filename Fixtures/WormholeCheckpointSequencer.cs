using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Godot;

public partial class WormholeCheckpointSequencer : Node3D
{
	public enum SequenceProfile
	{
		ApprovedBaseline = 0,
		MouthThroatInterpolationProbe = 1,
		ThroatExitInterpolationProbe = 2
	}

	[Export] public string FixtureHudName = "overspace_wormhole_checkpoint_sequence";
	[Export] public SequenceProfile Profile = SequenceProfile.ApprovedBaseline;
	[Export] public int StartupPhysicsFramesDelay = 2;
	[Export] public int SettleFrames = 12;
	[Export] public int CaptureTimeoutFrames = 240;
	[Export] public int MinRenderHealthStep = 1;
	[Export] public int MinProcessedRows = 270;

	private const string RunDirectoryEnv = "WORMHOLE_CHECKPOINT_RUN_DIR";

	private readonly List<CheckpointResult> _results = new();

	private string _runDirectory = string.Empty;
	private Node3D _activeRoom;
	private GrinFilmCamera _activeFilmCamera;
	private CanvasLayer _activeCanvasLayer;

	public override void _Ready()
	{
		_runDirectory = ResolveRunDirectory();
		try
		{
			Directory.CreateDirectory(_runDirectory);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[WormholeCheckpointSequencer][FAIL] reason=create_directory path={_runDirectory} exception={ex.GetType().Name}");
			GetTree().Quit(2);
			return;
		}

		CallDeferred(nameof(BeginSequenceDeferred));
	}

	private async void BeginSequenceDeferred()
	{
		CheckpointSpec[] checkpoints = GetCheckpointsForProfile();
		for (int checkpointIndex = 0; checkpointIndex < checkpoints.Length; checkpointIndex++)
		{
			bool ok = await RunCheckpointAsync(checkpointIndex, checkpoints[checkpointIndex]);
			if (!ok)
			{
				return;
			}
		}

		WriteSummary();
		GetTree().Quit(0);
	}

	private CheckpointSpec[] GetCheckpointsForProfile()
	{
		return Profile switch
		{
			SequenceProfile.MouthThroatInterpolationProbe => BuildMouthThroatInterpolationProbeCheckpoints(),
			SequenceProfile.ThroatExitInterpolationProbe => BuildThroatExitInterpolationProbeCheckpoints(),
			_ => BuildApprovedBaselineCheckpoints()
		};
	}

	private static CheckpointSpec[] BuildApprovedBaselineCheckpoints()
	{
		return new[]
		{
			new CheckpointSpec(
				"mouth",
				"res://Fixtures/fixture_overspace_wormhole_witness_mouth_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.890906f, 0f, 0.454187f),
						new Vector3(0.062862f, 0.990376f, -0.123306f),
						new Vector3(-0.449816f, 0.138405f, 0.882332f)),
					new Vector3(-2.35f, 1.05f, 3.55f)),
				46.0f,
				"Near-mouth static witness showing portal boundary and far-side mapping."),
			new CheckpointSpec(
				"mouth_to_throat_approach",
				"res://Fixtures/fixture_overspace_wormhole_witness_throat_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.907106f, 0f, 0.420902f),
						new Vector3(0.059122f, 0.990086f, -0.127417f),
						new Vector3(-0.416729f, 0.140466f, 0.898113f)),
					new Vector3(-2.085f, 0.985f, 3.465f)),
				46.0f,
				"Conservative approach checkpoint between the mouth and throat witnesses."),
			new CheckpointSpec(
				"throat",
				"res://Fixtures/fixture_overspace_wormhole_witness_throat_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.922063f, 0f, 0.387039f),
						new Vector3(0.055236f, 0.989764f, -0.131592f),
						new Vector3(-0.383077f, 0.142715f, 0.912625f)),
					new Vector3(-1.82f, 0.92f, 3.38f)),
				46.0f,
				"Throat-positive static witness using the validated observer pose."),
			new CheckpointSpec(
				"post_throat_backstep_01",
				"res://Fixtures/fixture_overspace_wormhole_witness_exit_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.931609f, 0f, -0.363462f),
						new Vector3(-0.052292f, 0.989596f, -0.134033f),
						new Vector3(0.35968f, 0.143872f, 0.921917f)),
					new Vector3(22.2f, 0.92f, 3.35f)),
				46.0f,
				"Discovered hard-leg checkpoint reached by a small backstep from the validated post-throat exit approach pose."),
			new CheckpointSpec(
				"post_throat_exit_approach",
				"res://Fixtures/fixture_overspace_wormhole_witness_exit_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.931609f, 0f, -0.363462f),
						new Vector3(-0.052292f, 0.989596f, -0.134033f),
						new Vector3(0.35968f, 0.143872f, 0.921917f)),
					new Vector3(23.4f, 0.92f, 3.35f)),
				46.0f,
				"Conservative post-throat exit-side witness moved slightly back from the validated exit look-back pose."),
			new CheckpointSpec(
				"exit_lookback",
				"res://Fixtures/fixture_overspace_wormhole_witness_exit_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.931609f, 0f, -0.363462f),
						new Vector3(-0.052292f, 0.989596f, -0.134033f),
						new Vector3(0.35968f, 0.143872f, 0.921917f)),
					new Vector3(25.65f, 0.92f, 3.35f)),
				46.0f,
				"Far-side look-back witness confirming the exit-side observer relation.")
		};
	}

	private static CheckpointSpec[] BuildMouthThroatInterpolationProbeCheckpoints()
	{
		return new[]
		{
			new CheckpointSpec(
				"mouth",
				"res://Fixtures/fixture_overspace_wormhole_witness_mouth_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.890906f, 0f, 0.454187f),
						new Vector3(0.062862f, 0.990376f, -0.123306f),
						new Vector3(-0.449816f, 0.138405f, 0.882332f)),
					new Vector3(-2.35f, 1.05f, 3.55f)),
				46.0f,
				"Approved mouth anchor."),
			new CheckpointSpec(
				"mouth_interp_00",
				"res://Fixtures/fixture_overspace_wormhole_witness_throat_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.896306f, 0f, 0.443092f),
						new Vector3(0.061615333f, 0.990279333f, -0.124676333f),
						new Vector3(-0.438787f, 0.139092f, 0.887592333f)),
					new Vector3(-2.261666667f, 1.028333333f, 3.521666667f)),
				46.0f,
				"First sparse interpolated pose between mouth and mouth-to-throat approach."),
			new CheckpointSpec(
				"mouth_interp_01",
				"res://Fixtures/fixture_overspace_wormhole_witness_throat_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.899006f, 0f, 0.4375445f),
						new Vector3(0.060992f, 0.990231f, -0.1253615f),
						new Vector3(-0.4332725f, 0.1394355f, 0.8902225f)),
					new Vector3(-2.2175f, 1.0175f, 3.5075f)),
				46.0f,
				"Sparse interpolated pose between mouth and mouth-to-throat approach."),
			new CheckpointSpec(
				"mouth_to_throat_approach",
				"res://Fixtures/fixture_overspace_wormhole_witness_throat_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.907106f, 0f, 0.420902f),
						new Vector3(0.059122f, 0.990086f, -0.127417f),
						new Vector3(-0.416729f, 0.140466f, 0.898113f)),
					new Vector3(-2.085f, 0.985f, 3.465f)),
				46.0f,
				"Approved approach anchor."),
			new CheckpointSpec(
				"throat_interp_00",
				"res://Fixtures/fixture_overspace_wormhole_witness_throat_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.912091667f, 0f, 0.409614333f),
						new Vector3(0.057826667f, 0.989978667f, -0.128808667f),
						new Vector3(-0.405511667f, 0.141215667f, 0.902950333f)),
					new Vector3(-1.996666667f, 0.963333333f, 3.436666667f)),
				46.0f,
				"First sparse interpolated pose between mouth-to-throat approach and throat."),
			new CheckpointSpec(
				"throat_interp_01",
				"res://Fixtures/fixture_overspace_wormhole_witness_throat_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.9145845f, 0f, 0.4039705f),
						new Vector3(0.057179f, 0.989925f, -0.1295045f),
						new Vector3(-0.399903f, 0.1415905f, 0.905369f)),
					new Vector3(-1.9525f, 0.9525f, 3.4225f)),
				46.0f,
				"Sparse interpolated pose between mouth-to-throat approach and throat."),
			new CheckpointSpec(
				"throat",
				"res://Fixtures/fixture_overspace_wormhole_witness_throat_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.922063f, 0f, 0.387039f),
						new Vector3(0.055236f, 0.989764f, -0.131592f),
						new Vector3(-0.383077f, 0.142715f, 0.912625f)),
					new Vector3(-1.82f, 0.92f, 3.38f)),
				46.0f,
				"Approved throat anchor.")
		};
	}

	private static CheckpointSpec[] BuildThroatExitInterpolationProbeCheckpoints()
	{
		return new[]
		{
			new CheckpointSpec(
				"throat",
				"res://Fixtures/fixture_overspace_wormhole_witness_throat_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.922063f, 0f, 0.387039f),
						new Vector3(0.055236f, 0.989764f, -0.131592f),
						new Vector3(-0.383077f, 0.142715f, 0.912625f)),
					new Vector3(-1.82f, 0.92f, 3.38f)),
				46.0f,
				"Approved throat anchor."),
			new CheckpointSpec(
				"post_throat_backstep_01",
				"res://Fixtures/fixture_overspace_wormhole_witness_exit_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.931609f, 0f, -0.363462f),
						new Vector3(-0.052292f, 0.989596f, -0.134033f),
						new Vector3(0.35968f, 0.143872f, 0.921917f)),
					new Vector3(22.2f, 0.92f, 3.35f)),
				46.0f,
				"Discovered hard-leg checkpoint reached by a small backstep from the validated post-throat exit approach pose."),
			new CheckpointSpec(
				"post_throat_exit_approach",
				"res://Fixtures/fixture_overspace_wormhole_witness_exit_room.tscn",
				new Transform3D(
					new Basis(
						new Vector3(0.931609f, 0f, -0.363462f),
						new Vector3(-0.052292f, 0.989596f, -0.134033f),
						new Vector3(0.35968f, 0.143872f, 0.921917f)),
					new Vector3(23.4f, 0.92f, 3.35f)),
				46.0f,
				"Approved post-throat exit approach anchor."),
		};
	}

	private async System.Threading.Tasks.Task<bool> RunCheckpointAsync(int checkpointIndex, CheckpointSpec checkpoint)
	{
		if (!InstantiateCheckpointScene(checkpoint))
		{
			return false;
		}

		SceneTree tree = GetTree();
		if (tree == null)
		{
			GD.PrintErr("[WormholeCheckpointSequencer][FAIL] reason=missing_tree");
			GetTree().Quit(2);
			return false;
		}

		for (int i = 0; i < Math.Max(0, StartupPhysicsFramesDelay); i++)
		{
			await ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
		}
		await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

		_activeFilmCamera.ResetFixtureDebugStatsForRunStart();
		_activeFilmCamera.UpdateEveryFrame = true;
		GD.Print($"[WormholeCheckpointSequencer][Checkpoint] index={checkpointIndex} name={checkpoint.Name} room={checkpoint.RoomScenePath}");

		int observedFrames = 0;
		int readyFrames = 0;
		while (true)
		{
			await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			observedFrames++;
			if (observedFrames > Math.Max(1, CaptureTimeoutFrames))
			{
				GD.PrintErr($"[WormholeCheckpointSequencer][FAIL] checkpoint={checkpoint.Name} reason=timeout observedFrames={observedFrames}");
				GetTree().Quit(2);
				return false;
			}

			if (!_activeFilmCamera.TryGetFixtureDebugStatsForTesting(out GrinFilmCamera.FixtureDebugStatsSnapshot snapshot) ||
				snapshot.TracedPixels <= 0)
			{
				readyFrames = 0;
				continue;
			}

			int renderHealthStep = 0;
			bool hasRenderHealthStep = _activeFilmCamera.TryGetLatestRenderHealthStepForTesting(out renderHealthStep);
			int processedRows = Math.Max(0, _activeFilmCamera.FilmRowCursor);
			if (_activeFilmCamera.TryGetFixtureWriteDiagnosticsForTesting(out GrinFilmCamera.FixtureWriteDiagnosticsSnapshot writeDiagnostics))
			{
				processedRows = Math.Max(processedRows, writeDiagnostics.RowsCompleted);
			}

			if ((MinRenderHealthStep > 0 && (!hasRenderHealthStep || renderHealthStep < MinRenderHealthStep)) ||
				(MinProcessedRows > 0 && processedRows < MinProcessedRows))
			{
				readyFrames = 0;
				continue;
			}

			readyFrames++;
			if (readyFrames < Math.Max(1, SettleFrames))
			{
				continue;
			}

			if (!CaptureCheckpoint(checkpointIndex, checkpoint, snapshot, renderHealthStep, processedRows))
			{
				return false;
			}
			break;
		}

		await TeardownCheckpointSceneAsync();
		return true;
	}

	private bool InstantiateCheckpointScene(CheckpointSpec checkpoint)
	{
		PackedScene roomScene = GD.Load<PackedScene>(checkpoint.RoomScenePath);
		if (roomScene == null)
		{
			GD.PrintErr($"[WormholeCheckpointSequencer][FAIL] checkpoint={checkpoint.Name} reason=missing_room_scene path={checkpoint.RoomScenePath}");
			GetTree().Quit(2);
			return false;
		}

		_activeRoom = roomScene.Instantiate<Node3D>();
		if (_activeRoom == null)
		{
			GD.PrintErr($"[WormholeCheckpointSequencer][FAIL] checkpoint={checkpoint.Name} reason=instantiate_room");
			GetTree().Quit(2);
			return false;
		}
		AddChild(_activeRoom);

		_activeFilmCamera = BuildFilmCamera();
		AddChild(_activeFilmCamera);

		_activeCanvasLayer = BuildCanvasLayer();
		AddChild(_activeCanvasLayer);

		Camera3D roomCamera = _activeRoom.GetNodeOrNull<Camera3D>("Camera3D");
		if (roomCamera == null || !GodotObject.IsInstanceValid(roomCamera))
		{
			GD.PrintErr($"[WormholeCheckpointSequencer][FAIL] checkpoint={checkpoint.Name} reason=missing_room_camera");
			GetTree().Quit(2);
			return false;
		}
		roomCamera.Current = true;
		roomCamera.Transform = checkpoint.Transform;
		roomCamera.Fov = checkpoint.Fov;
		return true;
	}

	private GrinFilmCamera BuildFilmCamera()
	{
		GrinFilmCamera filmCamera = new()
		{
			Name = "GrinFilmCamera",
			Width = 640,
			Height = 360,
			FilmResolutionScale = 0.75f,
			SkyColor = new Color(0.02f, 0.02f, 0.03f, 1.0f),
			FilmOpacity = 1.0f,
			ShadingMode = (GrinFilmCamera.FilmShadingMode)1,
			FlipNormalToCamera = true,
			FixtureDebugPortalGroup = "fixture_portal",
			FixtureDebugBackgroundGroup = string.Empty,
			FixtureTransportClassificationEnabled = true,
			UpdateEveryFrame = false,
			UpdateEveryFrameBudgetMs = 1000.0f,
			UpdateEveryFrameMaxRowsPerStep = 1024,
			QuitAfterCompletedPass = false,
			RenderStepMaxMs = 1000,
			RenderStepMaxSegmentsPerFrame = 200000000,
			TargetMsPerFrame = 50,
			MaxRowsPerFrameCap = 1024,
			EnableFramePerf = false,
			RenderStepPhaseLog = false,
			RenderStepBandLog = false,
			DebugSnapshotLog = false,
			DebugProbeLog = false,
			DebugGeomPruneAuditEnabled = true,
			AutoRangeMin = 0.01f,
			BroadphasePolicy = (GrinFilmCamera.BroadphasePolicyMode)2,
			RayBeamRendererPath = new NodePath("../FixtureOverspaceHermeticRoom/RayBeamRenderer"),
			FilmViewPath = new NodePath("../CanvasLayer/FilmView"),
			FilmOverlayPath = new NodePath("../CanvasLayer/FilmOverlay2D")
		};
		filmCamera.SetHudFixtureName(FixtureHudName);
		return filmCamera;
	}

	private CanvasLayer BuildCanvasLayer()
	{
		CanvasLayer canvasLayer = new()
		{
			Name = "CanvasLayer",
			Layer = -2
		};

		TextureRect filmView = new()
		{
			Name = "FilmView",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		filmView.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		filmView.SetMeta("_edit_use_anchors_", true);
		canvasLayer.AddChild(filmView);

		FilmOverlay2D overlay = new()
		{
			Name = "FilmOverlay2D",
			Modulate = new Color(1f, 1f, 1f, 0.75f),
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CameraPath = new NodePath("../../FixtureOverspaceHermeticRoom/Camera3D"),
			DrawFilmGradientNormals = false,
			RayWidth = 2.0f,
			FilmGradientScale = 30.0f
		};
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.SetMeta("_edit_use_anchors_", true);
		canvasLayer.AddChild(overlay);

		return canvasLayer;
	}

	private bool CaptureCheckpoint(
		int checkpointIndex,
		CheckpointSpec checkpoint,
		GrinFilmCamera.FixtureDebugStatsSnapshot snapshot,
		int renderHealthStep,
		int processedRows)
	{
		Viewport viewport = GetViewport();
		Image debugImage = viewport?.GetTexture()?.GetImage();
		if (debugImage == null || !_activeFilmCamera.TryCopyFilmImageForTesting(out Image analysisImage) || analysisImage == null)
		{
			GD.PrintErr($"[WormholeCheckpointSequencer][FAIL] checkpoint={checkpoint.Name} reason=missing_image");
			GetTree().Quit(2);
			return false;
		}

		string analysisPath = Path.Combine(_runDirectory, $"{checkpointIndex:00}_{checkpoint.Name}_capture.png");
		string debugPath = Path.Combine(_runDirectory, $"{checkpointIndex:00}_{checkpoint.Name}_debug.png");
		if (analysisImage.SavePng(analysisPath) != Error.Ok || debugImage.SavePng(debugPath) != Error.Ok)
		{
			GD.PrintErr($"[WormholeCheckpointSequencer][FAIL] checkpoint={checkpoint.Name} reason=save_png");
			GetTree().Quit(2);
			return false;
		}

		_activeFilmCamera.TryGetFixtureTransportCoverageForTesting(out GrinFilmCamera.FixtureTransportCoverageSnapshot coverage);
		_activeFilmCamera.TryGetFixtureCausalLedgerForTesting(out GrinFilmCamera.FixtureCausalLedgerSnapshot causal);
		_activeFilmCamera.TryGetFixtureAdaptiveSteppingDiagnosticsForTesting(out GrinFilmCamera.FixtureAdaptiveSteppingDiagnosticsSnapshot adaptive);
		bool runVerified =
			coverage.TotalPixels > 0 &&
			coverage.ClassifiedPixels == coverage.TotalPixels &&
			coverage.HermeticRuleSatisfied &&
			coverage.UnclassifiedPixels == 0 &&
			coverage.BudgetExhaustedPixels == 0 &&
			coverage.EscapedNoHitPixels == 0;

		Camera3D roomCamera = _activeRoom.GetNodeOrNull<Camera3D>("Camera3D");
		_results.Add(new CheckpointResult
		{
			Name = checkpoint.Name,
			Description = checkpoint.Description,
			Fov = roomCamera?.Fov ?? 0.0f,
			Transform = roomCamera?.Transform ?? Transform3D.Identity,
			CapturePath = analysisPath,
			DebugCapturePath = debugPath,
			RenderHealthStep = renderHealthStep,
			ProcessedRows = processedRows,
			TracedPixels = snapshot.TracedPixels,
			ClassifiedPixels = coverage.ClassifiedPixels,
			TotalPixels = coverage.TotalPixels,
			ClassifiedCoverageRatio = coverage.ClassifiedCoverageRatio,
			PortalHitPixels = coverage.PortalHitPixels,
			ThroatEventPixels = coverage.ThroatEventPixels,
			ThroatExitPixels = coverage.ThroatExitPixels,
			BudgetExhaustedPixels = coverage.BudgetExhaustedPixels,
			ThroatClassificationInferredPixels = causal.ThroatClassificationInferredPixels,
			FrontfaceHitPixels = causal.FrontfaceHitPixels,
			BackfaceHitPixels = causal.BackfaceHitPixels,
			BackfaceOnlyPixels = causal.BackfaceOnlyPixels,
			FrontfaceRatio = causal.FrontfaceRatio,
			BoundaryCrossingsTotal = causal.BoundaryCrossingsTotal,
			OpticalPathLengthMean = causal.OpticalPathLengthMean,
			OpticalPathLengthMax = causal.OpticalPathLengthMax,
			AdaptiveDiagnostics = new AdaptiveDiagnosticsResult
			{
				TotalEmittedRaySegCount = adaptive.TotalEmittedRaySegCount,
				AverageSegmentsPerRay = adaptive.AverageSegmentsPerRay,
				MaxSegmentsPerRay = adaptive.MaxSegmentsPerRay,
				AdaptiveSubdivisionCount = adaptive.AdaptiveSubdivisionCount,
				AverageTurnAngle = adaptive.AverageTurnAngle,
				MaxTurnAngle = adaptive.MaxTurnAngle,
				AverageLocalGeometricError = adaptive.AverageLocalGeometricError,
				MaxLocalGeometricError = adaptive.MaxLocalGeometricError,
				SteeringTurns = adaptive.SteeringTurns,
				ParallelRawCount = adaptive.ParallelRawCount,
				SourceHits = adaptive.SourceHits,
				BackgroundHits = adaptive.BackgroundHits,
				TerminatedRayCount = adaptive.TerminatedRayCount,
				FallbackUsedCount = adaptive.FallbackUsedCount,
				Summary = adaptive.Summary
			},
			RunVerified = runVerified
		});

		GD.Print(
			$"[WormholeCheckpointSequencer][Capture] checkpoint={checkpoint.Name} capturePath={analysisPath} debugPath={debugPath} " +
			$"portalHitPixels={coverage.PortalHitPixels} throatEventPixels={coverage.ThroatEventPixels} " +
			$"boundaryCrossingsTotal={causal.BoundaryCrossingsTotal} runVerified={(runVerified ? 1 : 0)}");
		return true;
	}

	private async System.Threading.Tasks.Task TeardownCheckpointSceneAsync()
	{
		if (_activeFilmCamera != null && GodotObject.IsInstanceValid(_activeFilmCamera))
		{
			_activeFilmCamera.UpdateEveryFrame = false;
		}

		_activeCanvasLayer?.QueueFree();
		_activeFilmCamera?.QueueFree();
		_activeRoom?.QueueFree();

		_activeCanvasLayer = null;
		_activeFilmCamera = null;
		_activeRoom = null;

		SceneTree tree = GetTree();
		if (tree == null)
		{
			return;
		}

		for (int i = 0; i < 2; i++)
		{
			await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}
		for (int i = 0; i < 1; i++)
		{
			await ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
		}
	}

	private void WriteSummary()
	{
		string summaryPath = Path.Combine(_runDirectory, "checkpoint_sequence_summary.json");
		var payload = new SequenceSummary
		{
			Fixture = FixtureHudName,
			RunDirectory = _runDirectory,
			CheckpointCount = _results.Count,
			Checkpoints = _results.ToArray()
		};

		JsonSerializerOptions options = new()
		{
			WriteIndented = true
		};
		File.WriteAllText(summaryPath, JsonSerializer.Serialize(payload, options));
		GD.Print($"[WormholeCheckpointSequencer][Summary] path={summaryPath} checkpoints={_results.Count}");
	}

	private static string ResolveRunDirectory()
	{
		string env = System.Environment.GetEnvironmentVariable(RunDirectoryEnv) ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(env))
		{
			return env.Trim();
		}

		return Path.Combine(
			ProjectSettings.GlobalizePath("res://"),
			"output",
			"fixture_runs",
			"fixture_011_wormhole_checkpoint_sequence",
			DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss", CultureInfo.InvariantCulture));
	}

	private readonly record struct CheckpointSpec(string Name, string RoomScenePath, Transform3D Transform, float Fov, string Description);

	private sealed class CheckpointResult
	{
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public float Fov { get; set; }
		public Transform3D Transform { get; set; }
		public string CapturePath { get; set; } = string.Empty;
		public string DebugCapturePath { get; set; } = string.Empty;
		public int RenderHealthStep { get; set; }
		public int ProcessedRows { get; set; }
		public long TracedPixels { get; set; }
		public long ClassifiedPixels { get; set; }
		public long TotalPixels { get; set; }
		public double ClassifiedCoverageRatio { get; set; }
		public long PortalHitPixels { get; set; }
		public long ThroatEventPixels { get; set; }
		public long ThroatExitPixels { get; set; }
		public long BudgetExhaustedPixels { get; set; }
		public long ThroatClassificationInferredPixels { get; set; }
		public long FrontfaceHitPixels { get; set; }
		public long BackfaceHitPixels { get; set; }
		public long BackfaceOnlyPixels { get; set; }
		public double FrontfaceRatio { get; set; }
		public long BoundaryCrossingsTotal { get; set; }
		public double? OpticalPathLengthMean { get; set; }
		public double? OpticalPathLengthMax { get; set; }
		public AdaptiveDiagnosticsResult AdaptiveDiagnostics { get; set; } = new();
		public bool RunVerified { get; set; }
	}

	private sealed class AdaptiveDiagnosticsResult
	{
		public long? TotalEmittedRaySegCount { get; set; }
		public double? AverageSegmentsPerRay { get; set; }
		public long? MaxSegmentsPerRay { get; set; }
		public long? AdaptiveSubdivisionCount { get; set; }
		public double? AverageTurnAngle { get; set; }
		public double? MaxTurnAngle { get; set; }
		public double? AverageLocalGeometricError { get; set; }
		public double? MaxLocalGeometricError { get; set; }
		public long? SteeringTurns { get; set; }
		public long? ParallelRawCount { get; set; }
		public long? SourceHits { get; set; }
		public long? BackgroundHits { get; set; }
		public long? TerminatedRayCount { get; set; }
		public long? FallbackUsedCount { get; set; }
		public string Summary { get; set; } = string.Empty;
	}

	private sealed class SequenceSummary
	{
		public string Fixture { get; set; } = string.Empty;
		public string RunDirectory { get; set; } = string.Empty;
		public int CheckpointCount { get; set; }
		public CheckpointResult[] Checkpoints { get; set; } = Array.Empty<CheckpointResult>();
	}
}
