using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Godot;

public partial class WormholeCheckpointSequencer : Node3D
{
	[Export] public string FixtureHudName = "overspace_wormhole_checkpoint_sequence";
	[Export] public int StartupPhysicsFramesDelay = 2;
	[Export] public int SettleFrames = 12;
	[Export] public int CaptureTimeoutFrames = 240;
	[Export] public int MinRenderHealthStep = 1;
	[Export] public int MinProcessedRows = 270;

	private const string RunDirectoryEnv = "WORMHOLE_CHECKPOINT_RUN_DIR";

	private readonly CheckpointSpec[] _checkpoints =
	{
		new(
			"mouth",
			"res://Fixtures/fixture_overspace_wormhole_witness_mouth_room.tscn",
			"Near-mouth static witness showing portal boundary and far-side mapping."),
		new(
			"throat",
			"res://Fixtures/fixture_overspace_wormhole_witness_throat_room.tscn",
			"Throat-positive static witness using the validated observer pose."),
		new(
			"exit_lookback",
			"res://Fixtures/fixture_overspace_wormhole_witness_exit_room.tscn",
			"Far-side look-back witness confirming the exit-side observer relation.")
	};

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
		for (int checkpointIndex = 0; checkpointIndex < _checkpoints.Length; checkpointIndex++)
		{
			bool ok = await RunCheckpointAsync(checkpointIndex, _checkpoints[checkpointIndex]);
			if (!ok)
			{
				return;
			}
		}

		WriteSummary();
		GetTree().Quit(0);
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

	private readonly record struct CheckpointSpec(string Name, string RoomScenePath, string Description);

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
		public bool RunVerified { get; set; }
	}

	private sealed class SequenceSummary
	{
		public string Fixture { get; set; } = string.Empty;
		public string RunDirectory { get; set; } = string.Empty;
		public int CheckpointCount { get; set; }
		public CheckpointResult[] Checkpoints { get; set; } = Array.Empty<CheckpointResult>();
	}
}
