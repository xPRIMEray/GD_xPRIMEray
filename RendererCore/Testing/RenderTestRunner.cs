using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class RenderTestRunner : Node
{
	private enum HarnessState
	{
		Idle = 0,
		Init = 1,
		MatrixStart = 2,
		RunApply = 3,
		RunMeasure = 4
	}

	[ExportGroup("Run Control")]
	[Export] public bool AutoStart = false;
	[Export] public bool AutoQuitOnComplete = false;
	[Export] public bool StartWhenCmdArgPresent = true;
	[Export] public string CmdArgToken = "--render-test";
	[Export(PropertyHint.Range, "1,100000,1")] public int FramesPerRun = 600;
	[Export(PropertyHint.Range, "0,100000,1")] public int WarmupFrames = 60;

	[ExportGroup("Node Paths")]
	[Export] public NodePath GrinFilmCameraPath = "../GrinFilmCamera";
	[Export] public NodePath TargetCameraPath = "../Camera3D";

	private GrinFilmCamera _film;
	private Camera3D _camera;
	private bool _matrixRunning = false;
	private bool _defaultsCaptured = false;
	private bool _cameraTransformCaptured = false;
	private GrinFilmCamera.TestRunDefaults _defaults;
	private Vector3 _cameraOriginalPosition;
	private Basis _cameraOriginalBasis;
	private readonly List<GrinFilmCamera.TestRunConfig> _runs = new List<GrinFilmCamera.TestRunConfig>();
	private readonly List<double> _frameMsSamples = new List<double>(1024);
	private int _runIndex = -1;
	private int _runFrameIndex = 0;
	private double _runFrameMsSum = 0.0;
	private double _runSegsPerPxSum = 0.0;
	private int _runSegsPerPxCount = 0;
	private ulong _runSeriesId = 0;
	private long _matrixStartTimestamp = 0;
	private bool _renderTestMode = false;
	private bool _startupDependencyErrorLogged = false;
	private HarnessState _harnessState = HarnessState.Idle;
	private int _startupDelayFramesRemaining = 0;
	private int _interRunDelayFramesRemaining = 0;

	public override void _Ready()
	{
		GD.Print("[RenderTestRunner] _Ready reached.");

		ProcessPriority = 100;
		bool hasToken = HasCmdArgToken();
		bool shouldStart = AutoStart || (StartWhenCmdArgPresent && hasToken);
		_renderTestMode = IsRenderTestMode() || shouldStart;

		if (_renderTestMode)
		{
			GD.Print($"[RenderTestRunner] TokenDetected={hasToken} shouldStart={shouldStart}");
			if (!shouldStart)
			{
				GD.Print($"[RenderTestRunner] Not starting matrix: StartWhenCmdArgPresent={StartWhenCmdArgPresent} AutoStart={AutoStart} HasToken={hasToken}");
			}
		}

		if (shouldStart)
		{
			CallDeferred(nameof(StartDefaultMatrix));
		}
	}

	public override void _Process(double delta)
	{
		if (!_matrixRunning)
		{
			return;
		}

		if (_film == null || !GodotObject.IsInstanceValid(_film))
		{
			HandleMissingDependenciesAndAbort();
			if (_renderTestMode)
			{
				return;
			}
			FinishMatrix();
			return;
		}

		if (_harnessState == HarnessState.Init)
		{
			_film.UpdateEveryFrame = false;
			string scenePath = GetTree().CurrentScene?.SceneFilePath ?? "null";
			GD.Print($"[RenderTestRunner][INIT] mode={( _renderTestMode ? "render-test" : "normal")} startupDelayFrames={_startupDelayFramesRemaining} scene={scenePath}");
			_harnessState = HarnessState.MatrixStart;
			return;
		}

		if (_harnessState == HarnessState.MatrixStart)
		{
			if (_startupDelayFramesRemaining > 0)
			{
				_startupDelayFramesRemaining--;
				return;
			}

			PrintMatrixStartLogs();
			_runIndex = 0;
			_harnessState = HarnessState.RunApply;
			return;
		}

		if (_harnessState == HarnessState.RunApply)
		{
			if (_interRunDelayFramesRemaining > 0)
			{
				_interRunDelayFramesRemaining--;
				return;
			}

			if (_runIndex < 0 || _runIndex >= _runs.Count)
			{
				FinishMatrix();
				return;
			}

			_film.UpdateEveryFrame = false;
			PrepareRun(_runs[_runIndex]);
			_harnessState = HarnessState.RunMeasure;
			return;
		}

		if (_harnessState != HarnessState.RunMeasure)
		{
			return;
		}

		if (_runIndex < 0 || _runIndex >= _runs.Count)
		{
			FinishMatrix();
			return;
		}

		GrinFilmCamera.TestRunConfig run = _runs[_runIndex];
		ApplyDeterministicCamera(run, _runFrameIndex);

		_film.GetLatestFrameMetricsForTesting(out double frameMs, out bool hasSegsPerPixel, out double segsPerPixel);
		if (_runFrameIndex >= WarmupFrames && double.IsFinite(frameMs) && frameMs >= 0.0)
		{
			_frameMsSamples.Add(frameMs);
			_runFrameMsSum += frameMs;
			if (hasSegsPerPixel && double.IsFinite(segsPerPixel) && segsPerPixel >= 0.0)
			{
				_runSegsPerPxSum += segsPerPixel;
				_runSegsPerPxCount++;
			}
		}

		_runFrameIndex++;
		if (_runFrameIndex >= FramesPerRun)
		{
			_film.UpdateEveryFrame = false;
			FinishRun();
			_runIndex++;
			_interRunDelayFramesRemaining = _renderTestMode ? 1 : 0;
			_harnessState = HarnessState.RunApply;
		}
	}

	public void StartDefaultMatrix()
	{
		if (_matrixRunning || _harnessState != HarnessState.Idle)
		{
			GD.Print("[RenderTest] matrix already running.");
			return;
		}

		_film = GetNodeOrNull<GrinFilmCamera>(GrinFilmCameraPath);
		if (_film == null || !GodotObject.IsInstanceValid(_film))
		{
			HandleMissingDependenciesAndAbort();
			return;
		}

		_camera = GetNodeOrNull<Camera3D>(TargetCameraPath);
		if (_camera == null || !GodotObject.IsInstanceValid(_camera))
		{
			HandleMissingDependenciesAndAbort();
			return;
		}

		if (_renderTestMode && !_film.SharedRbrHasRenderer)
		{
			HandleMissingDependenciesAndAbort();
			return;
		}

		if (_renderTestMode)
		{
			_film.UpdateEveryFrame = false;
			_startupDelayFramesRemaining = 2;
		}
		else
		{
			_startupDelayFramesRemaining = 0;
		}

		_defaults = _film.CaptureTestRunDefaults();
		_defaultsCaptured = true;
		_cameraOriginalPosition = _camera.GlobalPosition;
		_cameraOriginalBasis = _camera.GlobalTransform.Basis;
		_cameraTransformCaptured = true;

		_runs.Clear();
		_runs.AddRange(BuildDefaultRuns());
		if (_runs.Count == 0)
		{
			GD.PrintErr("[RenderTest] run matrix is empty.");
			return;
		}

		_matrixRunning = true;
		_harnessState = HarnessState.Init;
		_interRunDelayFramesRemaining = 0;
		_runIndex = -1;
		_runFrameIndex = 0;
	}

	private void PrintMatrixStartLogs()
	{
		_runSeriesId = (ulong)Time.GetUnixTimeFromSystem();
		_runFrameIndex = 0;
		_matrixStartTimestamp = Stopwatch.GetTimestamp();
		string scenePath = GetTree().CurrentScene?.SceneFilePath ?? "null";
		GD.Print($"[RenderTest][MATRIX START] id={_runSeriesId} runs={_runs.Count} framesPerRun={FramesPerRun} warmup={WarmupFrames}");
		GD.Print($"[MATRIX START] runs={_runs.Count} framesPerRun={FramesPerRun} warmup={WarmupFrames} scene={scenePath}");
	}

	private void PrepareRun(GrinFilmCamera.TestRunConfig run)
	{
		_film.ApplyTestRunConfig(in run);
		bool runWantsUpdateEveryFrame = run.UpdateEveryFrame ?? (_defaultsCaptured ? _defaults.UpdateEveryFrame : true);
		_film.UpdateEveryFrame = runWantsUpdateEveryFrame;
		_runFrameIndex = 0;
		_frameMsSamples.Clear();
		_runFrameMsSum = 0.0;
		_runSegsPerPxSum = 0.0;
		_runSegsPerPxCount = 0;
		GD.Print(
			$"[RenderTest][RUN START] matrix={_runSeriesId} idx={_runIndex + 1}/{_runs.Count} name={Sanitize(run.Name)} " +
			$"frames={FramesPerRun} warmup={WarmupFrames} prune={(run.UseGeometryTLASPruning.HasValue ? (run.UseGeometryTLASPruning.Value ? "on" : "off") : "inherit")}");
		GD.Print(
			$"[RUN START] name={Sanitize(run.Name)} " +
			$"prune={FormatNullableBool(run.UseGeometryTLASPruning)} " +
			$"stride={FormatStride(run)} envScale={FormatNullableFloat(run.Pass2GeomEnvelopeRadiusScale)} " +
			$"aabbExpand={FormatNullableFloat(run.Pass2GeomEnvelopeAabbExpand)} " +
			$"updateEveryFrame={(runWantsUpdateEveryFrame ? "on" : "off")}");
	}

	private void FinishRun()
	{
		GrinFilmCamera.TestRunConfig run = _runs[_runIndex];
		int sampleCount = _frameMsSamples.Count;
		double meanMs = sampleCount > 0 ? (_runFrameMsSum / sampleCount) : 0.0;
		double p95Ms = ComputeP95Ms(_frameMsSamples);
		string meanMsStr = sampleCount > 0 ? meanMs.ToString("0.###") : "na";
		string p95MsStr = sampleCount > 0 ? p95Ms.ToString("0.###") : "na";
		string meanSegStr = _runSegsPerPxCount > 0 ? (_runSegsPerPxSum / _runSegsPerPxCount).ToString("0.###") : "na";

		bool hasRh = _film.TryGetLatestRenderHealthForTesting(out bool trusted, out bool savedPctAvailable, out double savedPct, out string trustReason);
		string trustReasonOut = hasRh ? Sanitize(trustReason) : "no_renderhealth";
		string savedPctStr = (hasRh && trusted && savedPctAvailable && double.IsFinite(savedPct))
			? savedPct.ToString("0.##")
			: "na";

		GD.Print(
			$"[RenderTest][RUN SUMMARY] matrix={_runSeriesId} idx={_runIndex + 1}/{_runs.Count} name={Sanitize(run.Name)} " +
			$"samples={sampleCount} meanMsPerFrame={meanMsStr} p95MsPerFrame={p95MsStr} meanSegsPerPixel={meanSegStr} " +
			$"geomTrusted={(hasRh && trusted ? 1 : 0)} geomTrustReason={trustReasonOut} geomRayTestsSavedPct={savedPctStr}");
		GD.Print(
			$"[RenderTest][RUN END] matrix={_runSeriesId} idx={_runIndex + 1}/{_runs.Count} name={Sanitize(run.Name)} " +
			$"frames={FramesPerRun} warmup={WarmupFrames} samples={sampleCount}");
		GD.Print(
			$"[RUN END] name={Sanitize(run.Name)} frames={FramesPerRun} samples={sampleCount} " +
			$"meanMs={meanMsStr} p95Ms={p95MsStr}");

		if (_defaultsCaptured)
		{
			_film.RestoreTestRunDefaults(in _defaults);
		}
	}

	private void FinishMatrix()
	{
		if (!_matrixRunning)
		{
			return;
		}

		_matrixRunning = false;
		_harnessState = HarnessState.Idle;
		_startupDelayFramesRemaining = 0;
		_interRunDelayFramesRemaining = 0;
		if (_defaultsCaptured && _film != null && GodotObject.IsInstanceValid(_film))
		{
			_film.RestoreTestRunDefaults(in _defaults);
		}
		if (_cameraTransformCaptured && _camera != null && GodotObject.IsInstanceValid(_camera))
		{
			_camera.GlobalTransform = new Transform3D(_cameraOriginalBasis, _cameraOriginalPosition);
		}

		double elapsedMs = ComputeElapsedMs(_matrixStartTimestamp);
		GD.Print($"[RenderTest][MATRIX END] id={_runSeriesId} runs={_runs.Count}");
		GD.Print($"[MATRIX END] totalRuns={_runs.Count} elapsedMs={elapsedMs:0.###}");

		if (_renderTestMode)
		{
			GD.Print("[RenderTestRunner] Quitting (render-test mode).");
			GetTree().Quit(0);
			return;
		}

		if (AutoQuitOnComplete)
		{
			GetTree().Quit(0);
		}
	}

	private List<GrinFilmCamera.TestRunConfig> BuildDefaultRuns()
	{
		Vector3 camPos = _camera.GlobalPosition;
		Vector3 lookAt = camPos + (-_camera.GlobalTransform.Basis.Z) * 3.0f;
		const float orbitRadius = 2.8f;
		const float orbitHeight = 1.2f;
		const float orbitPeriodFrames = 300f;

		return new List<GrinFilmCamera.TestRunConfig>
		{
			new GrinFilmCamera.TestRunConfig
			{
				Name = "baseline_prune_off",
				UpdateEveryFrame = true,
				UseGeometryTLASPruning = false,
				UsePass2CollisionStride = false,
				Pass2SoftGateEnableQuickRayMiss = false,
				CameraMode = GrinFilmCamera.TestCameraMode.Orbit,
				CameraLookAt = lookAt,
				CameraFixedPosition = camPos,
				CameraOrbitRadius = orbitRadius,
				CameraOrbitHeight = orbitHeight,
				CameraOrbitPeriodFrames = orbitPeriodFrames
			},
			new GrinFilmCamera.TestRunConfig
			{
				Name = "prune_on_default",
				UpdateEveryFrame = true,
				UseGeometryTLASPruning = true,
				UsePass2CollisionStride = true,
				Pass2SoftGateEnableQuickRayMiss = true,
				CameraMode = GrinFilmCamera.TestCameraMode.Orbit,
				CameraLookAt = lookAt,
				CameraFixedPosition = camPos,
				CameraOrbitRadius = orbitRadius,
				CameraOrbitHeight = orbitHeight,
				CameraOrbitPeriodFrames = orbitPeriodFrames
			},
			new GrinFilmCamera.TestRunConfig
			{
				Name = "prune_on_tight_env",
				UpdateEveryFrame = true,
				UseGeometryTLASPruning = true,
				Pass2GeomEnvelopeRadiusScale = 1.02f,
				Pass2GeomEnvelopeAabbExpand = 0.01f,
				UsePass2CollisionStride = true,
				Pass2CollisionStrideNear = 1,
				Pass2CollisionStrideFar = 4,
				Pass2CollisionStrideFarStartT = 0.35f,
				MinSegLenForStrideSkip = 0.25f,
				Pass2SoftGateEnableQuickRayMiss = true,
				CameraMode = GrinFilmCamera.TestCameraMode.Orbit,
				CameraLookAt = lookAt,
				CameraFixedPosition = camPos,
				CameraOrbitRadius = orbitRadius,
				CameraOrbitHeight = orbitHeight,
				CameraOrbitPeriodFrames = orbitPeriodFrames
			},
			new GrinFilmCamera.TestRunConfig
			{
				Name = "prune_on_loose_env",
				UpdateEveryFrame = true,
				UseGeometryTLASPruning = true,
				Pass2GeomEnvelopeRadiusScale = 1.10f,
				Pass2GeomEnvelopeAabbExpand = 0.05f,
				UsePass2CollisionStride = true,
				Pass2SoftGateEnableQuickRayMiss = true,
				CameraMode = GrinFilmCamera.TestCameraMode.Orbit,
				CameraLookAt = lookAt,
				CameraFixedPosition = camPos,
				CameraOrbitRadius = orbitRadius,
				CameraOrbitHeight = orbitHeight,
				CameraOrbitPeriodFrames = orbitPeriodFrames
			},
			new GrinFilmCamera.TestRunConfig
			{
				Name = "prune_on_stride_off",
				UpdateEveryFrame = true,
				UseGeometryTLASPruning = true,
				UsePass2CollisionStride = false,
				Pass2SoftGateEnableQuickRayMiss = true,
				CameraMode = GrinFilmCamera.TestCameraMode.Orbit,
				CameraLookAt = lookAt,
				CameraFixedPosition = camPos,
				CameraOrbitRadius = orbitRadius,
				CameraOrbitHeight = orbitHeight,
				CameraOrbitPeriodFrames = orbitPeriodFrames
			}
		};
	}

	private void ApplyDeterministicCamera(in GrinFilmCamera.TestRunConfig run, int frameIndex)
	{
		if (_camera == null || !GodotObject.IsInstanceValid(_camera))
		{
			return;
		}

		if (run.CameraMode == GrinFilmCamera.TestCameraMode.Fixed)
		{
			_camera.GlobalPosition = run.CameraFixedPosition;
			Vector3 toTarget = run.CameraLookAt - run.CameraFixedPosition;
			if (toTarget.LengthSquared() > 1e-8f)
			{
				_camera.LookAt(run.CameraLookAt, Vector3.Up);
			}
			return;
		}

		if (run.CameraMode == GrinFilmCamera.TestCameraMode.Orbit)
		{
			float period = MathF.Max(1.0f, run.CameraOrbitPeriodFrames);
			float phase = (frameIndex % (int)period) / period;
			float angle = phase * Mathf.Tau;
			float radius = MathF.Max(0.1f, run.CameraOrbitRadius);
			Vector3 center = run.CameraLookAt;
			Vector3 pos = new Vector3(
				center.X + MathF.Cos(angle) * radius,
				center.Y + run.CameraOrbitHeight,
				center.Z + MathF.Sin(angle) * radius);
			_camera.GlobalPosition = pos;
			_camera.LookAt(center, Vector3.Up);
		}
	}

	private bool HasCmdArgToken()
	{
		string token = (CmdArgToken ?? string.Empty).Trim();
		if (token.Length == 0)
		{
			return false;
		}

		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (string.IsNullOrWhiteSpace(arg))
			{
				continue;
			}

			if (string.Equals(arg.Trim(), token, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private bool IsRenderTestMode()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (string.IsNullOrWhiteSpace(arg))
			{
				continue;
			}

			string token = arg.Trim();
			if (string.Equals(token, "--render-test", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(token, "--rendertest", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(token, "render-test", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private void HandleMissingDependenciesAndAbort()
	{
		if (_startupDependencyErrorLogged)
		{
			return;
		}

		_startupDependencyErrorLogged = true;
		bool missingFilm = _film == null || !GodotObject.IsInstanceValid(_film);
		bool missingCamera = _camera == null || !GodotObject.IsInstanceValid(_camera);
		bool missingRbrInRenderTest = _renderTestMode && !missingFilm && !_film.SharedRbrHasRenderer;
		string rbrPath = (!missingFilm && _film != null) ? _film.RayBeamRendererPath.ToString() : "unknown";

		if (missingFilm)
		{
			GD.PrintErr($"[RenderTestRunner] ERROR: Missing GrinFilmCamera at GrinFilmCameraPath={GrinFilmCameraPath}");
		}
		if (missingCamera)
		{
			GD.PrintErr($"[RenderTestRunner] ERROR: Missing Camera3D at TargetCameraPath={TargetCameraPath}");
		}
		if (missingRbrInRenderTest)
		{
			GD.PrintErr($"[RenderTestRunner] ERROR: Missing RayBeamRenderer in render-test mode: RayBeamRendererPath={rbrPath} SharedRbrHasRenderer=false");
		}

		if (_renderTestMode)
		{
			GD.Print("[RenderTestRunner] Quitting (render-test mode).");
			GetTree().Quit(1);
		}
	}

	private static string FormatNullableBool(bool? value)
	{
		if (!value.HasValue)
		{
			return "inherit";
		}
		return value.Value ? "on" : "off";
	}

	private static string FormatNullableFloat(float? value)
	{
		if (!value.HasValue)
		{
			return "inherit";
		}
		return value.Value.ToString("0.###");
	}

	private static string FormatStride(in GrinFilmCamera.TestRunConfig run)
	{
		if (!run.UsePass2CollisionStride.HasValue)
		{
			return "inherit";
		}

		if (!run.UsePass2CollisionStride.Value)
		{
			return "off";
		}

		string nearStr = run.Pass2CollisionStrideNear.HasValue ? run.Pass2CollisionStrideNear.Value.ToString() : "inherit";
		string farStr = run.Pass2CollisionStrideFar.HasValue ? run.Pass2CollisionStrideFar.Value.ToString() : "inherit";
		return $"on({nearStr}->{farStr})";
	}

	private static double ComputeElapsedMs(long startTimestamp)
	{
		if (startTimestamp <= 0)
		{
			return 0.0;
		}

		long delta = Stopwatch.GetTimestamp() - startTimestamp;
		if (delta <= 0)
		{
			return 0.0;
		}

		return (delta * 1000.0) / Stopwatch.Frequency;
	}

	private static double ComputeP95Ms(List<double> samples)
	{
		if (samples == null || samples.Count <= 0)
		{
			return 0.0;
		}

		double[] ordered = samples.ToArray();
		Array.Sort(ordered);
		int idx = (int)Math.Ceiling(ordered.Length * 0.95) - 1;
		if (idx < 0) idx = 0;
		if (idx >= ordered.Length) idx = ordered.Length - 1;
		return ordered[idx];
	}

	private static string Sanitize(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return "unnamed";
		}
		return value.Trim().Replace(' ', '_');
	}
}
