using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

public partial class RenderTestRunner : Node
{
	public enum RenderTestFixture
	{
		Default = 0,
		Straight = 1
	}

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
	[Export] public RenderTestFixture Fixture = RenderTestFixture.Default;
	// Defaults aligned with test_bbNew.tscn.
	[Export(PropertyHint.Range, "1,100000,1")] public int FramesPerRun = 30;
	[Export(PropertyHint.Range, "0,100000,1")] public int WarmupFrames = 3;

	[ExportGroup("Node Paths")]
	[Export] public NodePath GrinFilmCameraPath = "../GrinFilmCamera";
	[Export] public NodePath TargetCameraPath = "../Camera3D";

	private const int RenderTestMinGeomPixProcessedPerWindow = 1024;
	private const long RenderTestMinGeomRayTestsTotalPerWindow = 4096L;
	private const float RenderTestTrustRollingWindowSec = 10.0f;
	private const float RenderTestBbNewResolutionScale = 0.25f;
	private const string RenderTestDefaultScenePath = "res://test.tscn";
	private const string RenderTestStraightScenePath = "res://test-straight.tscn";
	private const string RenderTestStraightArgToken = "--render-test-straight";
	private const string RenderTestStraightSceneHint = "straight";
	private const string RenderTestFixtureArgPrefix = "--render-test-fixture=";
	private const int RenderTestMinFramesPerRun = 90;
	private const int RenderTestStatsFullFrameEveryNSteps = 8;
	private const int RenderTestTrustPass2SampleEveryNSegments = 8;
	private const int RenderTestTrustMinP2Samples = 8;
	private static readonly long RenderTestLiveLogIntervalTicks = Stopwatch.Frequency;

	private GrinFilmCamera _film;
	private Camera3D _camera;
	private bool _matrixRunning = false;
	private bool _defaultsCaptured = false;
	private bool _cameraTransformCaptured = false;
	private GrinFilmCamera.TestRunDefaults _defaults;
	private Transform3D _cameraOriginalTransform;
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
	private bool _baselineApplied = false;
	private HarnessState _harnessState = HarnessState.Idle;
	private int _startupDelayFramesRemaining = 0;
	private int _interRunDelayFramesRemaining = 0;
	private bool _renderTestResolutionScaleCaptured = false;
	private float _renderTestOriginalResolutionScale = 1.0f;
	private bool _renderTestFramesPerRunCaptured = false;
	private int _renderTestOriginalFramesPerRun = 0;
	private long _renderTestNextLiveLogTimestamp = 0;
	private string _renderTestLiveRunName = "unnamed";
	private bool _renderTestHasSeenRenderHealthSnapshot = false;
	private bool _rhLiveReflectionReady = false;
	private bool _rhLiveReflectionFailed = false;
	private FieldInfo _rhCountField;
	private FieldInfo _rhWriteField;
	private FieldInfo _rhSamplesField;
	private Type _rhSampleType;
	private FieldInfo _rhSampleStepField;
	private FieldInfo _rhSampleRowField;
	private FieldInfo _rhSampleRowsAdvField;
	private FieldInfo _rhSampleBandsField;
	private FieldInfo _rhSampleGeomPixField;
	private FieldInfo _rhSampleGeomRayTestsField;
	private FieldInfo _rhSampleHitsField;
	private FieldInfo _rhSampleTracedField;
	private FieldInfo _rhGeomOnTotalField;
	private FieldInfo _rhGeomOnTracedField;
	private FieldInfo _rhGeomOffTotalField;
	private FieldInfo _rhGeomOffTracedField;
	private FieldInfo _rhGeomOffBaselineField;
	private FieldInfo _rhGeomOffBaselineReadyField;
	private int _renderTestMinGeomPixForTrust = RenderTestMinGeomPixProcessedPerWindow;
	private long _renderTestMinGeomRayTestsForTrust = RenderTestMinGeomRayTestsTotalPerWindow;
	private int _renderTestStatsScaledFilmW = 0;
	private int _renderTestStatsScaledFilmH = 0;
	private int _renderTestStatsRowsPerStep = 0;
	private bool _renderTestTrustStraightMode = false;
	private int _renderTestTrustPass2Every = RenderTestTrustPass2SampleEveryNSegments;
	private int _renderTestTrustMinP2 = RenderTestTrustMinP2Samples;
	private bool _straightFixtureSceneActive = false;
	private RenderTestFixture _requestedFixture = RenderTestFixture.Default;

	public override void _Ready()
	{
		GD.Print("[RenderTestRunner] _Ready reached.");

		ProcessPriority = 100;
		bool hasToken = HasCmdArgToken();
		bool shouldStart = AutoStart || (StartWhenCmdArgPresent && hasToken);
		_renderTestMode = IsRenderTestMode() || shouldStart;
		_requestedFixture = GetRequestedFixture();

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
			if (_renderTestMode)
			{
				string desiredScenePath = GetScenePathForFixture(_requestedFixture);
				if (!IsCurrentScenePath(desiredScenePath))
				{
					GD.Print($"[RenderTestRunner] Switching to fixture scene: fixture={_requestedFixture} path={desiredScenePath}");
					CallDeferred(nameof(SwitchToFixtureSceneDeferred), desiredScenePath);
					return;
				}
			}
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
			RestoreCapturedCameraTransformIfNeeded();
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
		if (_renderTestMode)
		{
			RestoreCapturedCameraTransformIfNeeded();
		}
		else
		{
			ApplyDeterministicCamera(run, _runFrameIndex);
		}

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
		MaybeEmitRenderTestLive(run);

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

		if (_renderTestMode && !_baselineApplied && !ApplyRenderTestBaseline())
		{
			StopFilmRenderingForExitIfNeeded();
			CallDeferred(nameof(QuitDeferred), 1);
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
		_cameraOriginalTransform = _camera.GlobalTransform;
		_cameraTransformCaptured = true;
		if (_renderTestMode)
		{
			GD.Print("[RenderTestRunner] Captured camera transform for deterministic tests.");
		}

		_runs.Clear();
		_straightFixtureSceneActive = IsStraightFixtureSceneActive();
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

	private bool ApplyRenderTestBaseline()
	{
		if (!_renderTestMode || _film == null || !GodotObject.IsInstanceValid(_film))
		{
			return true;
		}

		if (!TryGetSharedRayBeamRenderer(out RayBeamRenderer rbr))
		{
			GD.PrintErr("[RenderTestRunner] ERROR: Failed to apply bbNew baseline profile (render-test mode): shared RayBeamRenderer reference unavailable.");
			return false;
		}

		// GrinFilmCamera bbNew baseline.
		_film.Preset = GrinFilmCamera.PresetMode.Walk;
		_film.ApplyPresetOnReady = true;
		_film.QualityMode = GrinFilmCamera.RenderQualityMode.Barebones;
		_film.Width = 320;
		_film.Height = 180;
		if (!_renderTestResolutionScaleCaptured)
		{
			_renderTestOriginalResolutionScale = _film.FilmResolutionScale;
			_renderTestResolutionScaleCaptured = true;
		}
		_film.FilmResolutionScale = MathF.Max(RenderTestBbNewResolutionScale, 0.01f);
		float effectiveScale = MathF.Max(_film.FilmResolutionScale, 0.01f);
		_film.RenderHealthRollingWindowSec = RenderTestTrustRollingWindowSec;
		int scaledFilmW = Mathf.Max(8, Mathf.CeilToInt(_film.Width * effectiveScale));
		int scaledFilmH = Mathf.Max(8, Mathf.CeilToInt(_film.Height * effectiveScale));
		int statsRowsPerStep = ComputeStatsFriendlyRowsPerStep(scaledFilmH);
		long expectedScaledPixels = Math.Max(1L, (long)scaledFilmW * scaledFilmH);
		_renderTestMinGeomPixForTrust = Math.Max(RenderTestMinGeomPixProcessedPerWindow, (int)(expectedScaledPixels / 4L));
		_renderTestTrustStraightMode = IsStraightFixtureModeForTrust();
		long minRayTestsDefault = Math.Max(RenderTestMinGeomRayTestsTotalPerWindow, (long)_renderTestMinGeomPixForTrust);
		long minRayTestsStraight = Math.Max(1024L, (long)_renderTestMinGeomPixForTrust * 2L);
		_renderTestMinGeomRayTestsForTrust = _renderTestTrustStraightMode ? minRayTestsStraight : minRayTestsDefault;
		_renderTestStatsScaledFilmW = scaledFilmW;
		_renderTestStatsScaledFilmH = scaledFilmH;
		_renderTestStatsRowsPerStep = statsRowsPerStep;
		if (!_renderTestFramesPerRunCaptured)
		{
			_renderTestOriginalFramesPerRun = FramesPerRun;
			_renderTestFramesPerRunCaptured = true;
		}
		FramesPerRun = Math.Max(FramesPerRun, RenderTestMinFramesPerRun);
		_film.SkyColor = new Color(0.15517181f, 0.13225737f, 0.33741817f, 1.0f);
		_film.FilmOpacity = 0.8f;
		_film.ShadingMode = GrinFilmCamera.FilmShadingMode.NormalRGB;
		_film.FlipNormalToCamera = false;
		_film.UpdateEveryFrame = false;
		_film.UpdateEveryFrameBudgetMs = 2000f;
		_film.UpdateEveryFrameMaxRowsPerStep = statsRowsPerStep;
		_film.RowsPerFrame = statsRowsPerStep;
		_film.MinRowsPerFrame = Math.Min(_film.MinRowsPerFrame, statsRowsPerStep);
		_film.MaxRowsPerFrameCap = statsRowsPerStep;
		_film.RenderStepMaxMs = 20000;
		_film.RenderStepMaxSegmentsPerFrame = 50000000;
		_film.TargetMsPerFrame = 1000;
		_film.EnableProfiling = false;
		_film.EnableFramePerf = false;
		_film.RenderStepPhaseLog = false;
		_film.RenderStepBandLog = false;
		_film.DebugSnapshotLog = false;
		_film.DebugProbeLog = false;
		_film.DebugGeomPruneAuditOnlyWhenCandidateZero = false;
		_film.AutoRangeMin = 0.01f;
		_film.Pass1DoHitTest = false;
		_film.BroadphaseControlMode = GrinFilmCamera.BroadphaseMode.Policy;
		_film.BroadphasePolicy = GrinFilmCamera.BroadphasePolicyMode.OverlapOnly;
		_film.BroadphaseMargin = 0.1f;
		_film.BroadphaseMaxResults = 16;
		_film.UsePass2CollisionStride = false;
		_film.MinSegLenForStrideSkip = 0.0f;
		_film.Pass2GeomEnvelopeRadiusScale = 1.02f;
		_film.Pass2GeomEnvelopeAabbExpand = 0.01f;
		_film.Pass2HitBackFaces = true;
		_film.Pass2ForceIfPrevHitLost = true;
		_film.Pass2SoftGateEnableQuickRayMiss = true;
		_film.DisableSoftGateOnOverload = false;
		_film.Pass2SoftGateWatchdogMs = 5000f;
		_film.Pass2SoftGateScoringEnabled = false;
		_film.Pass2SoftGateDebugEnabled = false;

		// RayBeamRenderer bbNew baseline.
		rbr.StepsPerRay = 600;
		rbr.StepLength = 0.05f;
		rbr.MinStepLength = 0.001f;
		rbr.MaxStepLength = 0.3f;
		rbr.LowCurvaturePerpAccel = 0.3f;
		rbr.BendScale = 1.0f;
		rbr.FieldCenter = new Vector3(0f, 1.5f, 0f);
		rbr.FieldCenterIsCamera = false;
		rbr.DebugMode = RayBeamRenderer.DebugDrawMode.Off;
		rbr.DebugNormalLen = 0.295f;
		rbr.Alpha = 1.0f;
		rbr.UpdateEveryFrame = false;
		_renderTestTrustPass2Every = RenderTestTrustPass2SampleEveryNSegments;
		_renderTestTrustMinP2 = RenderTestTrustMinP2Samples;
		_film.ConfigureRenderHealthTrustEnforcementForTesting(
			enabled: true,
			minGeomPixProcessedPerWindow: _renderTestMinGeomPixForTrust,
			minGeomRayTestsTotalPerWindow: _renderTestMinGeomRayTestsForTrust,
			pass2SampleEveryNSegmentsOverride: _renderTestTrustPass2Every,
			minPass2SamplesForTrustOverride: _renderTestTrustMinP2);
		GD.Print(
			$"[RenderTestRunner] RenderHealth overrides: p2Every={_renderTestTrustPass2Every} minP2={_renderTestTrustMinP2}");
		const bool trustEnforcementEnabled = true;
		GD.Print(
			$"[RenderTestRunner] Render-test baseline: scaledFilmPx={expectedScaledPixels} rowsPerStep={statsRowsPerStep} " +
			$"minGeomPix={_renderTestMinGeomPixForTrust} minRayTests={_renderTestMinGeomRayTestsForTrust} " +
			$"trustEnforcement={(trustEnforcementEnabled ? "on" : "off")}");
		GD.Print(
			$"[RenderTestRunner] Trust target (render-test): scaledFilm={scaledFilmW}x{scaledFilmH} " +
			$"scaledPixels={expectedScaledPixels} minGeomPix={_renderTestMinGeomPixForTrust} minRayTests={_renderTestMinGeomRayTestsForTrust}");

		_baselineApplied = true;
		GD.Print("[RenderTestRunner] Applied bbNew baseline profile (render-test mode).");
		return true;
	}

	private bool TryGetSharedRayBeamRenderer(out RayBeamRenderer rbr)
	{
		rbr = null;
		if (_film == null || !GodotObject.IsInstanceValid(_film) || !_film.SharedRbrHasRenderer)
		{
			return false;
		}

		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		PropertyInfo sharedProp = typeof(GrinFilmCamera).GetProperty("SharedRbr", flags);
		if (sharedProp != null && sharedProp.GetValue(_film) is RayBeamRenderer sharedRef && GodotObject.IsInstanceValid(sharedRef))
		{
			rbr = sharedRef;
			return true;
		}

		FieldInfo cachedField = typeof(GrinFilmCamera).GetField("_rbr", flags);
		if (cachedField != null && cachedField.GetValue(_film) is RayBeamRenderer cachedRef && GodotObject.IsInstanceValid(cachedRef))
		{
			rbr = cachedRef;
			return true;
		}

		return false;
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
		_film.ResetRenderHealthWindowForRunStart();
		_film.ApplyTestRunConfig(in run);
		if (_renderTestMode && IsStraightRun(run.Name))
		{
			ApplyStraightRunOverrides(in run);
		}
		bool runWantsUpdateEveryFrame = run.UpdateEveryFrame ?? (_defaultsCaptured ? _defaults.UpdateEveryFrame : true);
		_film.UpdateEveryFrame = runWantsUpdateEveryFrame;
		_renderTestLiveRunName = Sanitize(run.Name);
		_renderTestNextLiveLogTimestamp = 0;
		_renderTestHasSeenRenderHealthSnapshot = false;
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
		if (_renderTestMode)
		{
			GD.Print(
				$"[RenderTestRunner] Trust enforcement (mode={(_renderTestTrustStraightMode ? "straight" : "default")}): " +
				$"scaledFilm={_renderTestStatsScaledFilmW}x{_renderTestStatsScaledFilmH} " +
				$"minGeomPix={_renderTestMinGeomPixForTrust} minRayTests={_renderTestMinGeomRayTestsForTrust} " +
				$"minP2={_renderTestTrustMinP2} p2Every={_renderTestTrustPass2Every} " +
				$"rollingSec={_film.RenderHealthRollingWindowSec:0.###}");
			GD.Print(
				$"[RenderHealth][StatsMode] scaledFilm={_renderTestStatsScaledFilmW}x{_renderTestStatsScaledFilmH} " +
				$"rowsPerStep={_renderTestStatsRowsPerStep} fullFrameEveryNSteps<={RenderTestStatsFullFrameEveryNSteps}");
			if (IsStraightRun(run.Name))
			{
				string camPos = _camera != null && GodotObject.IsInstanceValid(_camera)
					? _camera.GlobalPosition.ToString()
					: "na";
				Vector3 target = (_camera != null && GodotObject.IsInstanceValid(_camera))
					? (_camera.GlobalPosition + (-_camera.GlobalTransform.Basis.Z) * 5.0f)
					: Vector3.Zero;
				float bendScale = 0.0f;
				if (TryGetSharedRayBeamRenderer(out RayBeamRenderer straightRbr) && GodotObject.IsInstanceValid(straightRbr))
				{
					bendScale = straightRbr.BendScale;
				}
				GD.Print($"[RenderTestRunner] Straight fixture: camera={camPos} target={target} bendScale={bendScale:0.###}");
			}
		}
	}

	private static int ComputeStatsFriendlyRowsPerStep(int scaledFilmH)
	{
		int h = Math.Max(1, scaledFilmH);
		int n = Math.Max(1, RenderTestStatsFullFrameEveryNSteps);
		int rows = (h + n - 1) / n;
		if (h > 1 && rows >= h)
		{
			rows = h - 1;
		}
		return Math.Max(1, rows);
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
			_film.ConfigureRenderHealthTrustEnforcementForTesting(enabled: false, minGeomPixProcessedPerWindow: 0, minGeomRayTestsTotalPerWindow: 0);
			if (_renderTestResolutionScaleCaptured)
			{
				_film.FilmResolutionScale = _renderTestOriginalResolutionScale;
			}
			if (_renderTestFramesPerRunCaptured)
			{
				FramesPerRun = _renderTestOriginalFramesPerRun;
			}
		}
		if (_cameraTransformCaptured && _camera != null && GodotObject.IsInstanceValid(_camera))
		{
			_camera.GlobalTransform = _cameraOriginalTransform;
		}

		double elapsedMs = ComputeElapsedMs(_matrixStartTimestamp);
		GD.Print($"[RenderTest][MATRIX END] id={_runSeriesId} runs={_runs.Count}");
		GD.Print($"[MATRIX END] totalRuns={_runs.Count} elapsedMs={elapsedMs:0.###}");

		if (_renderTestMode)
		{
			GD.Print("[RenderTestRunner] Quitting (render-test mode).");
			StopFilmRenderingForExitIfNeeded();
			GD.Print("[RenderTestRunner] Requesting deferred quit code=0");
			CallDeferred(nameof(QuitDeferred), 0);
			return;
		}

		if (AutoQuitOnComplete)
		{
			GD.Print("[RenderTestRunner] Requesting deferred quit code=0");
			CallDeferred(nameof(QuitDeferred), 0);
		}
	}

	private List<GrinFilmCamera.TestRunConfig> BuildDefaultRuns()
	{
		Vector3 camPos = _camera.GlobalPosition;
		Vector3 lookAt = camPos + (-_camera.GlobalTransform.Basis.Z) * 3.0f;
		const float orbitRadius = 2.8f;
		const float orbitHeight = 1.2f;
		const float orbitPeriodFrames = 300f;

		List<GrinFilmCamera.TestRunConfig> runs = new List<GrinFilmCamera.TestRunConfig>
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

		if (_straightFixtureSceneActive)
		{
			Vector3 straightCamPos = new Vector3(0f, 0f, 5f);
			Vector3 straightLookAt = Vector3.Zero;
			runs.Add(new GrinFilmCamera.TestRunConfig
			{
				Name = "straight_prune_off",
				UpdateEveryFrame = true,
				UseGeometryTLASPruning = false,
				UsePass2CollisionStride = false,
				Pass2SoftGateEnableQuickRayMiss = false,
				CameraMode = GrinFilmCamera.TestCameraMode.Fixed,
				CameraLookAt = straightLookAt,
				CameraFixedPosition = straightCamPos,
				CameraOrbitRadius = 0f,
				CameraOrbitHeight = 0f,
				CameraOrbitPeriodFrames = 1f
			});
			runs.Add(new GrinFilmCamera.TestRunConfig
			{
				Name = "straight_prune_on",
				UpdateEveryFrame = true,
				UseGeometryTLASPruning = true,
				UsePass2CollisionStride = true,
				Pass2SoftGateEnableQuickRayMiss = true,
				CameraMode = GrinFilmCamera.TestCameraMode.Fixed,
				CameraLookAt = straightLookAt,
				CameraFixedPosition = straightCamPos,
				CameraOrbitRadius = 0f,
				CameraOrbitHeight = 0f,
				CameraOrbitPeriodFrames = 1f
			});
		}

		return runs;
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

	private bool IsStraightFixtureSceneActive()
	{
		return IsStraightFixtureModeForTrust();
	}

	private bool IsStraightFixtureModeForTrust()
	{
		if (HasCmdArg(RenderTestStraightArgToken))
		{
			return true;
		}
		if (_requestedFixture == RenderTestFixture.Straight)
		{
			return true;
		}

		string currentPath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		if (PathLooksStraightFixture(currentPath))
		{
			return true;
		}

		string expectedPath = GetScenePathForFixture(_requestedFixture);
		return PathLooksStraightFixture(expectedPath);
	}

	private static bool PathLooksStraightFixture(string scenePath)
	{
		if (string.IsNullOrWhiteSpace(scenePath))
		{
			return false;
		}

		return scenePath.IndexOf(RenderTestStraightSceneHint, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static bool HasCmdArg(string expectedToken)
	{
		if (string.IsNullOrWhiteSpace(expectedToken))
		{
			return false;
		}

		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (string.IsNullOrWhiteSpace(arg))
			{
				continue;
			}

			if (string.Equals(arg.Trim(), expectedToken, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private RenderTestFixture GetRequestedFixture()
	{
		if (TryGetFixtureFromCmdArgs(out RenderTestFixture fromArgs))
		{
			return fromArgs;
		}

		return Fixture;
	}

	private bool TryGetFixtureFromCmdArgs(out RenderTestFixture fixture)
	{
		fixture = RenderTestFixture.Default;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (string.IsNullOrWhiteSpace(arg))
			{
				continue;
			}

			string trimmed = arg.Trim();
			if (!trimmed.StartsWith(RenderTestFixtureArgPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string value = trimmed.Substring(RenderTestFixtureArgPrefix.Length).Trim();
			if (string.Equals(value, "straight", StringComparison.OrdinalIgnoreCase))
			{
				fixture = RenderTestFixture.Straight;
				return true;
			}
			if (string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
			{
				fixture = RenderTestFixture.Default;
				return true;
			}
		}

		return false;
	}

	private static string GetScenePathForFixture(RenderTestFixture fixture)
	{
		return fixture == RenderTestFixture.Straight
			? RenderTestStraightScenePath
			: RenderTestDefaultScenePath;
	}

	private bool IsCurrentScenePath(string scenePath)
	{
		string current = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		return string.Equals(current, scenePath, StringComparison.OrdinalIgnoreCase);
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

	private void SwitchToFixtureSceneDeferred(string scenePath)
	{
		Error err = GetTree().ChangeSceneToFile(scenePath);
		if (err == Error.Ok)
		{
			return;
		}

		GD.PrintErr($"[RenderTestRunner] ERROR: Failed to switch to fixture scene: path={scenePath} err={(int)err}");
		if (_renderTestMode)
		{
			StopFilmRenderingForExitIfNeeded();
			CallDeferred(nameof(QuitDeferred), 1);
		}
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
			StopFilmRenderingForExitIfNeeded();
			GD.Print("[RenderTestRunner] Requesting deferred quit code=1");
			CallDeferred(nameof(QuitDeferred), 1);
		}
	}

	private void StopFilmRenderingForExitIfNeeded()
	{
		if (_film == null || !GodotObject.IsInstanceValid(_film))
		{
			return;
		}

		_film.UpdateEveryFrame = false;
		_film.SetProcess(false);
	}

	private void QuitDeferred(int code)
	{
		GetTree().Quit(code);
	}

	private void RestoreCapturedCameraTransformIfNeeded()
	{
		if (!_renderTestMode || !_cameraTransformCaptured || _camera == null || !GodotObject.IsInstanceValid(_camera))
		{
			return;
		}

		_camera.GlobalTransform = _cameraOriginalTransform;
	}

	private void MaybeEmitRenderTestLive(in GrinFilmCamera.TestRunConfig run)
	{
		if (!_renderTestMode || _film == null || !GodotObject.IsInstanceValid(_film))
		{
			return;
		}

		bool hasRenderHealth = _film.TryGetLatestRenderHealthForTesting(out bool geomTrustedRaw, out _, out _, out string geomTrustReasonRaw);
		if (hasRenderHealth && !_renderTestHasSeenRenderHealthSnapshot)
		{
			_renderTestHasSeenRenderHealthSnapshot = true;
			int readyStep = -1;
			if (TryGetLatestRenderHealthLiveSnapshot(out RenderHealthLiveSnapshot readySnap))
			{
				readyStep = readySnap.Step;
			}
			GD.Print($"[RenderTestRunner] RenderHealth snapshot ready at step={(readyStep >= 0 ? readyStep.ToString() : "na")} frame={_runFrameIndex + 1}");
		}

		long now = Stopwatch.GetTimestamp();
		if (_renderTestNextLiveLogTimestamp > 0 && now < _renderTestNextLiveLogTimestamp)
		{
			return;
		}
		_renderTestNextLiveLogTimestamp = now + RenderTestLiveLogIntervalTicks;

		bool geomTrusted = hasRenderHealth && geomTrustedRaw;
		int geomHealthPartial = geomTrusted ? 0 : 1;
		string trustReasonOut = hasRenderHealth ? Sanitize(geomTrustReasonRaw) : "warming_up";

		string lastRow = "na";
		string rowsAdv = "na";
		string bands = "na";
		string geomPixProcessed = "na";
		string geomRayTestsTotal = "na";
		string hitRate = "na";
		if (TryGetLatestRenderHealthLiveSnapshot(out RenderHealthLiveSnapshot snap))
		{
			lastRow = snap.LastRow.ToString();
			rowsAdv = snap.RowsAdv.ToString();
			bands = snap.Bands.ToString();
			geomPixProcessed = snap.GeomPixProcessed.ToString();
			geomRayTestsTotal = snap.GeomRayTestsTotal.ToString();
			hitRate = snap.Traced > 0
				? ((double)snap.Hits / snap.Traced).ToString("0.###")
				: "na";
		}

		GD.Print(
			$"[RenderTestLive] name={_renderTestLiveRunName} frame={_runFrameIndex + 1}/{FramesPerRun} " +
			$"lastRow={lastRow} rowsAdv={rowsAdv} bands={bands} " +
			$"geomPixProcessed={geomPixProcessed} geomRayTestsTotal={geomRayTestsTotal} " +
			$"geomTrusted={(geomTrusted ? 1 : 0)} geomHealthPartial={geomHealthPartial} geomTrustReason={trustReasonOut} hitRate={hitRate}");
		if (IsStraightRun(run.Name))
		{
			string perPxOff = "na";
			string perPxOn = "na";
			TryGetRenderHealthPerPixelMetrics(out perPxOff, out perPxOn);
			string stepStr = snap.Step >= 0 ? snap.Step.ToString() : "na";
			GD.Print($"[RenderHealth][StraightDebug] step={stepStr} hitRate={hitRate} perPxOff={perPxOff} perPxOn={perPxOn}");
		}
	}

	private bool TryGetLatestRenderHealthLiveSnapshot(out RenderHealthLiveSnapshot snap)
	{
		snap = default;
		if (_film == null || !GodotObject.IsInstanceValid(_film))
		{
			return false;
		}

		if (!EnsureRenderHealthLiveReflection())
		{
			return false;
		}

		if (!(_rhCountField.GetValue(_film) is int count) || count <= 0)
		{
			return false;
		}
		if (!(_rhWriteField.GetValue(_film) is int write))
		{
			return false;
		}
		if (!(_rhSamplesField.GetValue(_film) is Array samples) || samples.Length <= 0)
		{
			return false;
		}

		int idx = write - 1;
		if (idx < 0)
		{
			idx += samples.Length;
		}

		object boxedSample = samples.GetValue(idx);
		if (boxedSample == null)
		{
			return false;
		}

		snap = new RenderHealthLiveSnapshot
		{
			Step = ReadIntField(_rhSampleStepField, boxedSample),
			LastRow = ReadIntField(_rhSampleRowField, boxedSample),
			RowsAdv = ReadIntField(_rhSampleRowsAdvField, boxedSample),
			Bands = ReadIntField(_rhSampleBandsField, boxedSample),
			GeomPixProcessed = ReadLongField(_rhSampleGeomPixField, boxedSample),
			GeomRayTestsTotal = ReadLongField(_rhSampleGeomRayTestsField, boxedSample),
			Hits = ReadIntField(_rhSampleHitsField, boxedSample),
			Traced = ReadLongField(_rhSampleTracedField, boxedSample)
		};
		return true;
	}

	private bool EnsureRenderHealthLiveReflection()
	{
		if (_rhLiveReflectionReady)
		{
			return true;
		}
		if (_rhLiveReflectionFailed)
		{
			return false;
		}

		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		Type filmType = typeof(GrinFilmCamera);
		_rhCountField = filmType.GetField("_renderHealthCount", flags);
		_rhWriteField = filmType.GetField("_renderHealthWrite", flags);
		_rhSamplesField = filmType.GetField("_renderHealthSamples", flags);

		_rhSampleType = filmType.GetNestedType("RenderHealthSample", BindingFlags.NonPublic);
		if (_rhSampleType != null)
		{
			_rhSampleStepField = _rhSampleType.GetField("StepIndex", BindingFlags.Instance | BindingFlags.Public);
			_rhSampleRowField = _rhSampleType.GetField("RowCursorAfter", BindingFlags.Instance | BindingFlags.Public);
			_rhSampleRowsAdvField = _rhSampleType.GetField("RowsAdvanced", BindingFlags.Instance | BindingFlags.Public);
			_rhSampleBandsField = _rhSampleType.GetField("BandsProcessed", BindingFlags.Instance | BindingFlags.Public);
			_rhSampleGeomPixField = _rhSampleType.GetField("GeomPixelProcessed", BindingFlags.Instance | BindingFlags.Public);
		_rhSampleGeomRayTestsField = _rhSampleType.GetField("GeomRayTestsTotal", BindingFlags.Instance | BindingFlags.Public);
		_rhSampleHitsField = _rhSampleType.GetField("Hits", BindingFlags.Instance | BindingFlags.Public);
		_rhSampleTracedField = _rhSampleType.GetField("TracedPixels", BindingFlags.Instance | BindingFlags.Public);
	}
	_rhGeomOnTotalField = filmType.GetField("_geomRayTestsPruningOnTotal", flags);
	_rhGeomOnTracedField = filmType.GetField("_geomRayTestsPruningOnTracedPixels", flags);
	_rhGeomOffTotalField = filmType.GetField("_geomRayTestsPruningOffTotal", flags);
	_rhGeomOffTracedField = filmType.GetField("_geomRayTestsPruningOffTracedPixels", flags);
	_rhGeomOffBaselineField = filmType.GetField("_geomRayTestsOffPerPixelBaseline", flags);
	_rhGeomOffBaselineReadyField = filmType.GetField("_geomRayTestsOffPerPixelBaselineReady", flags);

		_rhLiveReflectionReady =
			_rhCountField != null
			&& _rhWriteField != null
			&& _rhSamplesField != null
			&& _rhSampleStepField != null
			&& _rhSampleRowField != null
			&& _rhSampleRowsAdvField != null
			&& _rhSampleBandsField != null
			&& _rhSampleGeomPixField != null
			&& _rhSampleGeomRayTestsField != null
			&& _rhSampleHitsField != null
			&& _rhSampleTracedField != null;

		_rhLiveReflectionFailed = !_rhLiveReflectionReady;
		return _rhLiveReflectionReady;
	}

	private static int ReadIntField(FieldInfo field, object target)
	{
		if (field == null)
		{
			return 0;
		}
		object value = field.GetValue(target);
		return value is int i ? i : 0;
	}

	private static long ReadLongField(FieldInfo field, object target)
	{
		if (field == null)
		{
			return 0L;
		}
		object value = field.GetValue(target);
		if (value is long l) return l;
		if (value is int i) return i;
		return 0L;
	}

	private static double ReadDoubleField(FieldInfo field, object target)
	{
		if (field == null)
		{
			return 0.0;
		}
		object value = field.GetValue(target);
		if (value is double d) return d;
		if (value is float f) return f;
		return 0.0;
	}

	private bool TryGetRenderHealthPerPixelMetrics(out string perPxOff, out string perPxOn)
	{
		perPxOff = "na";
		perPxOn = "na";
		if (_film == null || !GodotObject.IsInstanceValid(_film) || !EnsureRenderHealthLiveReflection())
		{
			return false;
		}

		long onTotal = ReadLongField(_rhGeomOnTotalField, _film);
		long onTraced = ReadLongField(_rhGeomOnTracedField, _film);
		long offTotal = ReadLongField(_rhGeomOffTotalField, _film);
		long offTraced = ReadLongField(_rhGeomOffTracedField, _film);

		bool hasAny = false;
		if (onTraced > 0)
		{
			double perPx = onTotal / (double)onTraced;
			if (double.IsFinite(perPx) && perPx >= 0.0)
			{
				perPxOn = perPx.ToString("0.###");
				hasAny = true;
			}
		}
		if (offTraced > 0)
		{
			double perPx = offTotal / (double)offTraced;
			if (double.IsFinite(perPx) && perPx >= 0.0)
			{
				perPxOff = perPx.ToString("0.###");
				hasAny = true;
			}
		}
		else if (_rhGeomOffBaselineReadyField != null
			&& _rhGeomOffBaselineField != null
			&& _rhGeomOffBaselineReadyField.GetValue(_film) is bool ready
			&& ready)
		{
			double baseline = ReadDoubleField(_rhGeomOffBaselineField, _film);
			if (double.IsFinite(baseline) && baseline >= 0.0)
			{
				perPxOff = baseline.ToString("0.###");
				hasAny = true;
			}
		}

		return hasAny;
	}

	private void ApplyStraightRunOverrides(in GrinFilmCamera.TestRunConfig run)
	{
		if (!TryGetSharedRayBeamRenderer(out RayBeamRenderer rbr) || !GodotObject.IsInstanceValid(rbr))
		{
			return;
		}

		rbr.BendScale = 0.0f;
		rbr.FieldCenterIsCamera = true;
		rbr.UpdateEveryFrame = false;
		_film.BroadphaseControlMode = GrinFilmCamera.BroadphaseMode.Policy;
		_film.BroadphasePolicy = (run.UseGeometryTLASPruning ?? false)
			? GrinFilmCamera.BroadphasePolicyMode.QuickRayOnly
			: GrinFilmCamera.BroadphasePolicyMode.OverlapOnly;
	}

	private static bool IsStraightRun(string runName)
	{
		if (string.IsNullOrWhiteSpace(runName))
		{
			return false;
		}
		return runName.Trim().StartsWith("straight_", StringComparison.OrdinalIgnoreCase);
	}

	private struct RenderHealthLiveSnapshot
	{
		public int Step;
		public int LastRow;
		public int RowsAdv;
		public int Bands;
		public long GeomPixProcessed;
		public long GeomRayTestsTotal;
		public int Hits;
		public long Traced;
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
