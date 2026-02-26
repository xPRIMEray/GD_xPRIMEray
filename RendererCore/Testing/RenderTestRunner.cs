using Godot;
using RendererCore.Calibration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

public partial class RenderTestRunner : Node
{
	public enum RenderTestFixture
	{
		Default = 0,
		Straight = 1,
		CurvedMinimal = 2
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
	[Export] public bool EnableSceneAutoCalibration = false;
	[Export] public bool ApplyAutoCalibratedPreset = false;
	[Export] public bool AutoCalVerboseLogs = false;
	[Export] public bool EnableShadowCalibrationEvaluation = false;
	[Export] public bool ShadowEvalVerboseLogs = false;
	[Export] public bool EnableLifecycleStressPass = false;
	[Export] public string CmdArgToken = "--render-test";
	[Export] public RenderTestFixture Fixture = RenderTestFixture.Default;
	// Defaults aligned with test_bbNew.tscn.
	[Export(PropertyHint.Range, "1,100000,1")] public int FramesPerRun = 30;
	[Export(PropertyHint.Range, "0,100000,1")] public int WarmupFrames = 3;
	[Export(PropertyHint.Range, "1,1000,1")] public int LifecycleStressCycles = 20;
	[Export(PropertyHint.Range, "1,10000,1")] public int LifecycleStressFramesPerRun = 60;
	[Export(PropertyHint.Range, "0,10000,1")] public int LifecycleStressWarmupFrames = 3;

	[ExportGroup("Node Paths")]
	[Export] public NodePath GrinFilmCameraPath = "../GrinFilmCamera";
	[Export] public NodePath TargetCameraPath = "../Camera3D";

	private const int RenderTestMinGeomPixProcessedPerWindow = 1024;
	private const long RenderTestMinGeomRayTestsTotalPerWindow = 4096L;
	private const float RenderTestTrustRollingWindowSec = 10.0f;
	private const float RenderTestBbNewResolutionScale = 0.25f;
	private const string RenderTestDefaultScenePath = "res://test.tscn";
	private const string RenderTestStraightScenePath = "res://test-straight.tscn";
	private const string RenderTestCurvedMinimalScenePath = "res://test-curved-minimal.tscn";
	private const string RenderTestStraightArgToken = "--render-test-straight";
	private const string RenderTestStraightSceneHint = "straight";
	private const string RenderTestFixtureArgPrefix = "--render-test-fixture=";
	private const string AutoCalCmdArgPrefix = "--autocal=";
	private const string ShadowEvalCmdArgPrefix = "--shadow-eval=";
	private const string ShadowPruneOffTargetMsCmdArgPrefix = "--shadow-pruneoff-target-ms=";
	private const string AutoCalVerboseCmdArgPrefix = "--autocal-verbose=";
	private const string AutoCalApplyCmdArgPrefix = "--autocal-apply=";
	private const string LifecycleStressCmdArgPrefix = "--lifecycle-stress=";
	private const string LifecycleStressCyclesCmdArgPrefix = "--lifecycle-stress-cycles=";
	private const string LifecycleStressFramesCmdArgPrefix = "--lifecycle-stress-frames=";
	private const string LifecycleStressWarmupCmdArgPrefix = "--lifecycle-stress-warmup=";
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
	private bool _shadowEvalPendingForCurrentMatrixRun = false;
	private bool _shadowEvalActiveRun = false;
	private bool _shadowEvalDefaultsCaptured = false;
	private GrinFilmCamera.TestRunDefaults _shadowEvalDefaults;
	private readonly Stopwatch _activeRunHarnessStopwatch = new Stopwatch();
	private GrinFilmCamera.TestRunConfig _activeRunConfig;
	private bool _activeRunConfigReady = false;
	private ulong _runExecSequence = 0;
	private bool _hasPendingShadowEvalBaselineResult = false;
	private ShadowEvalRunMetrics _pendingShadowEvalBaselineResult;
	private bool _autoCalPresetReady = false;
	private CalibratedPreset _autoCalPreset;
	private int? _shadowPruneOffTargetMsOverride;
	private bool _autoCalProbeSignatureHash64Known = false;
	private ulong _autoCalProbeSignatureHash64 = 0UL;
	private bool _autoCalCanonicalSignatureHash64Known = false;
	private ulong _autoCalCanonicalSignatureHash64 = 0UL;
	private bool _autoCalAcceptedApplyPending = false;
	private ulong _autoCalAcceptedApplyPendingCanonicalSignatureHash64 = 0UL;
	private ulong _autoCalAcceptedApplyPendingPresetHash64 = 0UL;
	private string _autoCalAcceptedApplyPendingReason = string.Empty;
	private bool _autoCalAcceptedApplyActiveRun = false;
	private bool _autoCalAcceptedApplyActiveRunCanonicalSignatureHash64Known = false;
	private ulong _autoCalAcceptedApplyActiveRunCanonicalSignatureHash64 = 0UL;
	private bool _autoCalAcceptedApplyActiveRunPresetHash64Known = false;
	private ulong _autoCalAcceptedApplyActiveRunPresetHash64 = 0UL;
	private readonly AutoCalRuntimeState _lastAutoCalState = new AutoCalRuntimeState();
	private bool _autoCalDecisionLogDedupKeyValid = false;
	private ulong _autoCalDecisionLogDedupBaselineRunId = 0UL;
	private ulong _autoCalDecisionLogDedupCanonicalSignatureHash64 = 0UL;
	private ulong _autoCalDecisionLogDedupPresetHash64 = 0UL;
	private ShadowEvalMatrixDecisionAggregate _shadowEvalMatrixDecisionAggregate;
	private bool _lifecycleStressMidCycleRendererNullObserved = false;

	private sealed class LifecycleStressSessionState
	{
		public bool Active;
		public int TargetCycles;
		public int FramesPerRun;
		public int WarmupFrames;
		public int CompletedCycles;
		public int FailedCycles;
		public int TotalFailures;
		public int TotalFailuresAtCycleStart;
		public readonly List<string> FailureDetails = new List<string>();
	}

	private static readonly LifecycleStressSessionState s_lifecycleStress = new LifecycleStressSessionState();

	private struct ShadowEvalRunMetrics
	{
		public ulong RunId;
		public bool TrustKnown;
		public bool Trusted;
		public string TrustReason;
		public int TrustFlipEst;
		public bool GeomPixProcessedKnown;
		public long GeomPixProcessed;
		public bool GeomRayTestsTotalKnown;
		public long GeomRayTestsTotal;
		public bool MeanMsKnown;
		public double MeanMs;
		public bool ElapsedMsKnown;
		public double ElapsedMs;
		public bool PruneEnabled;
		public int TargetMsPerFrame;
	}

	private struct ShadowEvalMatrixDecisionAggregate
	{
		public bool enabled;
		public int shadow_pair_count;
		public bool any_pair_reject;
		public bool any_pair_defer;
		public bool max_overhead_pct_est_known;
		public double max_overhead_pct_est;
		public ulong last_baseline_run_id;
		public ulong canonical_signature_hash64;
		public ulong preset_hash64;
	}

	public override void _Ready()
	{
		GD.Print("[RenderTestRunner] _Ready reached.");

		ProcessPriority = 100;
		bool hasToken = HasCmdArgToken();
		bool shouldStart = AutoStart || (StartWhenCmdArgPresent && hasToken);
		_renderTestMode = IsRenderTestMode() || shouldStart;
		_requestedFixture = GetRequestedFixture();
		ApplyStartupCliFlagOverrides();
		ConfigureLifecycleStressSessionFromFlags();

		if (_renderTestMode)
		{
			GD.Print(
				$"[RenderTestRunner][CLIFlags] autocal={(EnableSceneAutoCalibration ? 1 : 0)} " +
				$"shadow_eval={(EnableShadowCalibrationEvaluation ? 1 : 0)} " +
				$"autocal_verbose={(AutoCalVerboseLogs ? 1 : 0)} " +
				$"autocal_apply={(ApplyAutoCalibratedPreset ? 1 : 0)} " +
				$"lifecycle_stress={(IsLifecycleStressActive() ? 1 : 0)}");
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

		GrinFilmCamera.TestRunConfig run = _activeRunConfigReady ? _activeRunConfig : _runs[_runIndex];
		if (_renderTestMode)
		{
			RestoreCapturedCameraTransformIfNeeded();
		}
		else
		{
			ApplyDeterministicCamera(run, _runFrameIndex);
		}
		if (IsLifecycleStressActive())
		{
			ValidateLifecycleStressMidCycleRendererReference();
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
			if (_shadowEvalPendingForCurrentMatrixRun)
			{
				_shadowEvalPendingForCurrentMatrixRun = false;
				_shadowEvalActiveRun = true;
			}
			else
			{
				_shadowEvalActiveRun = false;
				_runIndex++;
			}
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
		if (IsLifecycleStressActive())
		{
			FramesPerRun = s_lifecycleStress.FramesPerRun;
			WarmupFrames = s_lifecycleStress.WarmupFrames;
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
		if (IsLifecycleStressActive() && _runs.Count > 1)
		{
			GrinFilmCamera.TestRunConfig baselineRun = _runs[0];
			_runs.Clear();
			_runs.Add(baselineRun);
		}
		if (_runs.Count == 0)
		{
			GD.PrintErr("[RenderTest] run matrix is empty.");
			return;
		}

		_matrixRunning = true;
		if (IsLifecycleStressActive())
		{
			s_lifecycleStress.TotalFailuresAtCycleStart = s_lifecycleStress.TotalFailures;
			GD.Print(
				$"[RenderTestRunner][LifecycleStress][CYCLE START] cycle={s_lifecycleStress.CompletedCycles + 1}/{s_lifecycleStress.TargetCycles} " +
				$"fixture={_requestedFixture} frames={FramesPerRun} warmup={WarmupFrames}");
		}
		_harnessState = HarnessState.Init;
		_interRunDelayFramesRemaining = 0;
		_runIndex = -1;
		_runFrameIndex = 0;
		_shadowEvalPendingForCurrentMatrixRun = false;
		_shadowEvalActiveRun = false;
		_shadowEvalDefaultsCaptured = false;
		_activeRunConfigReady = false;
		_hasPendingShadowEvalBaselineResult = false;
		ResetShadowEvalMatrixDecisionAggregate();
		_autoCalAcceptedApplyActiveRun = false;
		ClearPendingAcceptedAutoCalApply();
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

	private void ValidateLifecycleStressMidCycleRendererReference()
	{
		if (!IsLifecycleStressActive() || _harnessState != HarnessState.RunMeasure || _runIndex < 0 || _runIndex >= _runs.Count)
		{
			return;
		}

		if (_runFrameIndex <= 0)
		{
			return;
		}

		bool ok = TryGetSharedRayBeamRenderer(out RayBeamRenderer rbr) && GodotObject.IsInstanceValid(rbr);
		if (ok)
		{
			return;
		}

		if (_lifecycleStressMidCycleRendererNullObserved)
		{
			return;
		}

		_lifecycleStressMidCycleRendererNullObserved = true;
		RecordLifecycleStressFailure(
			$"cycle={s_lifecycleStress.CompletedCycles + 1}/{s_lifecycleStress.TargetCycles} " +
			$"run={Sanitize((_activeRunConfigReady ? _activeRunConfig.Name : _runs[_runIndex].Name))} " +
			$"frame={_runFrameIndex}/{FramesPerRun} assertion=renderer_ref_non_null_mid_cycle");
	}

	private void ValidateLifecycleStressRunAssertions(in GrinFilmCamera.TestRunConfig run, ulong runExecId)
	{
		int cycleNumber = s_lifecycleStress.CompletedCycles + 1;
		if (_lifecycleStressMidCycleRendererNullObserved)
		{
			GD.PrintErr(
				$"[RenderTestRunner][LifecycleStress][FAIL] cycle={cycleNumber}/{s_lifecycleStress.TargetCycles} " +
				$"run={Sanitize(run.Name)} run_id={runExecId} assertion=renderer_ref_non_null_mid_cycle");
		}

		if (!TryGetSharedRayBeamRenderer(out RayBeamRenderer rbr) || !GodotObject.IsInstanceValid(rbr))
		{
			RecordLifecycleStressFailure(
				$"cycle={cycleNumber}/{s_lifecycleStress.TargetCycles} run={Sanitize(run.Name)} run_id={runExecId} " +
				$"assertion=renderer_ref_available_at_run_end");
			return;
		}

		RayBeamRenderer.LifecycleStressDebugSnapshot snap = rbr.GetLifecycleStressDebugSnapshot();
		if (snap.QueuedOutOfTreeCount != 0)
		{
			RecordLifecycleStressFailure(
				$"cycle={cycleNumber}/{s_lifecycleStress.TargetCycles} run={Sanitize(run.Name)} run_id={runExecId} " +
				$"assertion=no_rebuild_queued_out_of_tree observed={snap.QueuedOutOfTreeCount}");
		}
		if (snap.DuplicateDeferredTokenExecCount != 0)
		{
			RecordLifecycleStressFailure(
				$"cycle={cycleNumber}/{s_lifecycleStress.TargetCycles} run={Sanitize(run.Name)} run_id={runExecId} " +
				$"assertion=no_duplicate_rebuild_tokens observed={snap.DuplicateDeferredTokenExecCount}");
		}

		GD.Print(
			$"[RenderTestRunner][LifecycleStress][RUN] cycle={cycleNumber}/{s_lifecycleStress.TargetCycles} " +
			$"run={Sanitize(run.Name)} run_id={runExecId} " +
			$"queued_out_of_tree={snap.QueuedOutOfTreeCount} dup_tokens={snap.DuplicateDeferredTokenExecCount} " +
			$"deferred_execs={snap.DeferredTokenExecCount} last_req_id={snap.LatestRequestId}");
	}

	private void RecordLifecycleStressFailure(string detail)
	{
		s_lifecycleStress.TotalFailures++;
		s_lifecycleStress.FailureDetails.Add(detail);
		GD.PrintErr($"[RenderTestRunner][LifecycleStress][FAIL] {detail}");
	}

	private void PrintMatrixStartLogs()
	{
		_runSeriesId = (ulong)Time.GetUnixTimeFromSystem();
		_runFrameIndex = 0;
		_matrixStartTimestamp = Stopwatch.GetTimestamp();
		MaybeRunSceneAutoCalibration();
		string scenePath = GetTree().CurrentScene?.SceneFilePath ?? "null";
		GD.Print($"[RenderTest][MATRIX START] id={_runSeriesId} runs={_runs.Count} framesPerRun={FramesPerRun} warmup={WarmupFrames}");
		GD.Print($"[MATRIX START] runs={_runs.Count} framesPerRun={FramesPerRun} warmup={WarmupFrames} scene={scenePath}");
	}

	private void PrepareRun(GrinFilmCamera.TestRunConfig run)
	{
		_shadowEvalDefaultsCaptured = false;
		_autoCalAcceptedApplyActiveRun = false;
		_autoCalAcceptedApplyActiveRunCanonicalSignatureHash64Known = false;
		_autoCalAcceptedApplyActiveRunCanonicalSignatureHash64 = 0UL;
		_autoCalAcceptedApplyActiveRunPresetHash64Known = false;
		_autoCalAcceptedApplyActiveRunPresetHash64 = 0UL;
		_activeRunConfig = run;
		_activeRunConfigReady = true;
		if (ShouldRunShadowCalibrationEvaluation() && _shadowEvalActiveRun)
		{
			_shadowEvalDefaults = _film.CaptureTestRunDefaults();
			_shadowEvalDefaultsCaptured = true;
			_activeRunConfig = BuildShadowEvalRunConfig(in run);
			if (ShadowEvalVerboseLogs)
			{
				GD.Print(
					$"[RenderTestRunner][AutoCalShadowEval] prepare phase=shadow base_name={Sanitize(run.Name)} " +
					$"shadow_name={Sanitize(_activeRunConfig.Name)} preset_ready={(_autoCalPresetReady ? 1 : 0)} " +
					$"preset_hash64=0x{_autoCalPreset.hash64:x16} shadow_target_ms_per_frame={_activeRunConfig.TargetMsPerFrame}");
			}
		}
		else if (ShouldRunShadowCalibrationEvaluation() && ShadowEvalVerboseLogs)
		{
			GD.Print($"[RenderTestRunner][AutoCalShadowEval] prepare phase=baseline name={Sanitize(run.Name)}");
		}

		MaybeApplyAcceptedAutoCalPresetToNextRun();
		EmitAutoCalAppliedRunStartMarkerIfNeeded();

		_activeRunHarnessStopwatch.Restart();
		_film.ResetRenderHealthWindowForRunStart();
		_film.ApplyTestRunConfig(in _activeRunConfig);
		if (_renderTestMode && IsStraightRun(_activeRunConfig.Name))
		{
			ApplyStraightRunOverrides(in _activeRunConfig);
		}
		bool runWantsUpdateEveryFrame = _activeRunConfig.UpdateEveryFrame ?? (_defaultsCaptured ? _defaults.UpdateEveryFrame : true);
		_film.UpdateEveryFrame = runWantsUpdateEveryFrame;
		_renderTestLiveRunName = Sanitize(_activeRunConfig.Name);
		_renderTestNextLiveLogTimestamp = 0;
		_renderTestHasSeenRenderHealthSnapshot = false;
		_runFrameIndex = 0;
		_frameMsSamples.Clear();
		_runFrameMsSum = 0.0;
		_runSegsPerPxSum = 0.0;
		_runSegsPerPxCount = 0;
		_lifecycleStressMidCycleRendererNullObserved = false;
		if (IsLifecycleStressActive() && TryGetSharedRayBeamRenderer(out RayBeamRenderer lifecycleRbr) && GodotObject.IsInstanceValid(lifecycleRbr))
		{
			lifecycleRbr.ResetLifecycleStressDebugCounters();
		}
		GD.Print(
			$"[RenderTest][RUN START] matrix={_runSeriesId} idx={_runIndex + 1}/{_runs.Count} name={Sanitize(_activeRunConfig.Name)} " +
			$"frames={FramesPerRun} warmup={WarmupFrames} prune={(_activeRunConfig.UseGeometryTLASPruning.HasValue ? (_activeRunConfig.UseGeometryTLASPruning.Value ? "on" : "off") : "inherit")}");
		GD.Print(
			$"[RUN START] name={Sanitize(_activeRunConfig.Name)} " +
			$"prune={FormatNullableBool(_activeRunConfig.UseGeometryTLASPruning)} " +
			$"stride={FormatStride(_activeRunConfig)} envScale={FormatNullableFloat(_activeRunConfig.Pass2GeomEnvelopeRadiusScale)} " +
			$"aabbExpand={FormatNullableFloat(_activeRunConfig.Pass2GeomEnvelopeAabbExpand)} " +
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
			if (IsStraightRun(_activeRunConfig.Name))
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

	private void MaybeRunSceneAutoCalibration()
	{
		ResetShadowEvalMatrixDecisionAggregate();
		_autoCalPresetReady = false;
		_autoCalPreset = default;
		_autoCalProbeSignatureHash64Known = false;
		_autoCalProbeSignatureHash64 = 0UL;
		_autoCalCanonicalSignatureHash64Known = false;
		_autoCalCanonicalSignatureHash64 = 0UL;
		ClearPendingAcceptedAutoCalApply();

		if (!EnableSceneAutoCalibration)
		{
			return;
		}

		ProbeBudget probeBudget = new ProbeBudget();
		Node probeRoot = GetTree()?.CurrentScene;
		if (probeRoot == null || !GodotObject.IsInstanceValid(probeRoot))
		{
			probeRoot = this;
		}

		(SceneProbeReport sig, ProbeTelemetry telemetry) = SceneAutoCalibrator.ProbeScene(probeRoot, probeBudget);
		Debug.Assert(telemetry.visited_nodes >= 0, "Scene probe visited_nodes must be non-negative.");
		Debug.Assert(telemetry.inspected_meshes >= 0, "Scene probe inspected_meshes must be non-negative.");
		Debug.Assert(telemetry.inspected_meshes <= probeBudget.max_meshes, "Scene probe exceeded mesh budget cap.");

		SceneProbeArchetype archetype = SceneAutoCalibrator.ClassifyArchetype(sig);
		if (sig != null)
		{
			sig.archetype = archetype;
		}

		bool? baselinePruneEnabled = (_film != null && GodotObject.IsInstanceValid(_film))
			? _film.UseGeometryTLASPruning
			: (bool?)null;
		CalibratedPreset preset = SceneAutoCalibrator.BuildPreset(sig, archetype, baselinePruneEnabled);
		_autoCalPreset = preset;
		_autoCalPresetReady = true;
		_shadowEvalMatrixDecisionAggregate.enabled = preset.enable_tlas_prune.HasValue;
		_shadowEvalMatrixDecisionAggregate.canonical_signature_hash64 = 0UL;
		_shadowEvalMatrixDecisionAggregate.preset_hash64 = preset.hash64;
		ulong probeReportHash64 = sig?.ComputeHash64() ?? 0UL;
		SceneSignature canonicalSignature = BuildCanonicalSceneSignatureFromProbeReport(sig, probeRoot);
		ulong canonicalSceneSignatureHash64 = canonicalSignature.ComputeHash64();
		_autoCalProbeSignatureHash64Known = sig != null;
		_autoCalProbeSignatureHash64 = probeReportHash64;
		_autoCalCanonicalSignatureHash64Known = true;
		_autoCalCanonicalSignatureHash64 = canonicalSceneSignatureHash64;
		_shadowEvalMatrixDecisionAggregate.canonical_signature_hash64 = canonicalSceneSignatureHash64;
		string presetNotes = FormatAutoCalNotesForLog(preset.notes, 120);
		string sigExit = sig != null ? sig.probe_early_exit_reason.ToString() : "na";
		GD.Print(
			$"[RenderTestRunner][AutoCal] signature nodes={sig?.scene_node_count ?? -1} meshes={sig?.scene_mesh_count ?? -1} " +
			$"surfacesEst={sig?.scene_surface_count_estimate ?? -1} fields={sig?.scene_field_source_count ?? -1} " +
			$"grin={sig?.scene_grin_volume_count ?? -1} curvature={sig?.scene_curvature_field_count ?? -1} " +
			$"childrenSkipped={sig?.scene_children_skipped ?? -1} exit={sigExit} " +
			$"archetype={archetype}");
		GD.Print(
			$"[RenderTestRunner][AutoCal] telemetry ms={telemetry.elapsed_msec:0.###} visited={telemetry.visited_nodes} " +
			$"inspectedMeshes={telemetry.inspected_meshes} earlyExit={telemetry.early_exit_reason}");
		GD.Print(
			$"[RenderTestRunner][AutoCalMeta] " +
			$"signature_kind=probe_report " +
			$"scene_signature_hash64=0x{probeReportHash64:x16} " +
			$"canonical_signature_hash64=0x{canonicalSceneSignatureHash64:x16} " +
			$"canonical_signature_kind=calibration_v1 " +
			$"scene_archetype={archetype} " +
			$"probe_elapsed_msec={telemetry.elapsed_msec:0.###} " +
			$"probe_exit_reason={telemetry.early_exit_reason} " +
			$"preset_hash64=0x{preset.hash64:x16} " +
			$"preset_is_noop={(preset.IsNoOp ? 1 : 0)} " +
			$"preset_notes={presetNotes}");
		_lastAutoCalState.scene_archetype = archetype.ToString();
		_lastAutoCalState.canonical_signature_hash64 = $"0x{canonicalSceneSignatureHash64:x16}";
		_lastAutoCalState.preset_hash64 = $"0x{preset.hash64:x16}";
		if (AutoCalVerboseLogs)
		{
			GD.Print($"[RenderTestRunner][AutoCal] preset {preset}");
		}
	}

	private static SceneSignature BuildCanonicalSceneSignatureFromProbeReport(SceneProbeReport probeReport, Node probeRoot)
	{
		SceneSignature signature = new SceneSignature();
		Node currentScene = probeRoot?.GetTree()?.CurrentScene;

		signature.scene_path = currentScene != null && GodotObject.IsInstanceValid(currentScene)
			? (currentScene.SceneFilePath ?? string.Empty)
			: string.Empty;

		try
		{
			Godot.Collections.Dictionary versionInfo = Engine.GetVersionInfo();
			if (versionInfo != null && versionInfo.ContainsKey("string"))
			{
				signature.engine_version = versionInfo["string"].ToString();
			}
		}
		catch
		{
			// Keep engine_version empty on API/lookup mismatch; this is log-only metadata.
		}

		signature.node_count = probeReport?.scene_node_count ?? 0;
		signature.mesh_instance_count = probeReport?.scene_mesh_count ?? 0;
		signature.field_source_count = probeReport?.scene_field_source_count ?? 0;
		signature.light_count = 0;
		signature.camera_count = 0;

		int triMin = 0;
		int triMax = 0;
		if (probeReport != null && probeReport.scene_triangle_count_known && probeReport.scene_triangle_count_estimate >= 0)
		{
			long triEstimate = probeReport.scene_triangle_count_estimate;
			if (triEstimate > int.MaxValue)
			{
				triEstimate = int.MaxValue;
			}
			triMin = (int)triEstimate;
			triMax = (int)triEstimate;
		}
		signature.tri_estimate_min = triMin;
		signature.tri_estimate_max = triMax;

		signature.bounds_hint = null;
		signature.hash64 = signature.ComputeHash64();
		return signature;
	}

	private void ApplyPreset(CalibratedPreset preset)
	{
		GD.Print($"[RenderTestRunner][AutoCal][ApplyPresetStub] Would apply preset: {preset}");
	}

	private bool ShouldRunShadowCalibrationEvaluation()
	{
		return EnableSceneAutoCalibration && EnableShadowCalibrationEvaluation;
	}

	private GrinFilmCamera.TestRunConfig BuildShadowEvalRunConfig(in GrinFilmCamera.TestRunConfig baselineRun)
	{
		GrinFilmCamera.TestRunConfig shadow = baselineRun;
		shadow.Name = $"{Sanitize(baselineRun.Name)}_shadow";
		if (_autoCalPresetReady)
		{
			ApplyAutoCalPresetDeltaToRunConfig(in _autoCalPreset, ref shadow);
			ApplyWeakSignalFieldHeavyAdaptiveTargetForShadowBaseline(in _autoCalPreset, in baselineRun, ref shadow);
			ApplyShadowPruneOffTargetMsOverrideForExperiment(in baselineRun, ref shadow);
			// Keep changes minimal/no-op in this step. Preset fields not directly represented by TestRunConfig
			// can be mapped later without changing the harness flow.
		}
		return shadow;
	}

	private void ApplyShadowPruneOffTargetMsOverrideForExperiment(in GrinFilmCamera.TestRunConfig baselineRun, ref GrinFilmCamera.TestRunConfig shadowRun)
	{
		if (!_shadowPruneOffTargetMsOverride.HasValue)
		{
			return;
		}

		if (!string.Equals(baselineRun.Name?.Trim(), "baseline_prune_off", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		if (!ResolveRunPruneEnabled(in baselineRun))
		{
			shadowRun.TargetMsPerFrame = Math.Max(1, _shadowPruneOffTargetMsOverride.Value);
		}
	}

	private static void ApplyAutoCalPresetDeltaToRunConfig(in CalibratedPreset preset, ref GrinFilmCamera.TestRunConfig run)
	{
		if (preset.enable_tlas_prune.HasValue)
		{
			run.UseGeometryTLASPruning = preset.enable_tlas_prune.Value;
		}
		if (preset.target_ms_per_frame.HasValue)
		{
			run.TargetMsPerFrame = preset.target_ms_per_frame.Value;
		}
	}

	private void ApplyWeakSignalFieldHeavyAdaptiveTargetForShadowBaseline(in CalibratedPreset preset, in GrinFilmCamera.TestRunConfig baselineRun, ref GrinFilmCamera.TestRunConfig shadowRun)
	{
		if (preset.enable_tlas_prune.HasValue)
		{
			return;
		}

		if (!preset.target_ms_per_frame.HasValue)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(preset.notes) ||
			preset.notes.IndexOf("fieldheavy:weak_signal", StringComparison.OrdinalIgnoreCase) < 0 ||
			preset.notes.IndexOf("adaptive_target_by_baseline_prune", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return;
		}

		bool baselinePruneEnabled = ResolveRunPruneEnabled(in baselineRun);
		shadowRun.TargetMsPerFrame = baselinePruneEnabled ? 10 : 20;
	}

	private void ResetShadowEvalMatrixDecisionAggregate()
	{
		_shadowEvalMatrixDecisionAggregate = default;
	}

	private bool ShouldAggregateShadowEvalDecisionAcrossMatrix()
	{
		return _shadowEvalMatrixDecisionAggregate.enabled && ShouldRunShadowCalibrationEvaluation();
	}

	private void RecordShadowEvalMatrixPairDecision(ulong baselineRunId, in CalibrationDecisionRecord pairRecord)
	{
		if (!ShouldAggregateShadowEvalDecisionAcrossMatrix())
		{
			return;
		}

		_shadowEvalMatrixDecisionAggregate.shadow_pair_count++;
		_shadowEvalMatrixDecisionAggregate.last_baseline_run_id = baselineRunId;
		_shadowEvalMatrixDecisionAggregate.canonical_signature_hash64 = pairRecord.canonical_signature_hash64;
		_shadowEvalMatrixDecisionAggregate.preset_hash64 = pairRecord.preset_hash64;
		_shadowEvalMatrixDecisionAggregate.any_pair_reject |= pairRecord.decision == CalibrationDecision.Reject;
		_shadowEvalMatrixDecisionAggregate.any_pair_defer |= pairRecord.decision == CalibrationDecision.Defer;
		if (pairRecord.overhead_pct_est.HasValue && double.IsFinite(pairRecord.overhead_pct_est.Value))
		{
			double overhead = pairRecord.overhead_pct_est.Value;
			if (!_shadowEvalMatrixDecisionAggregate.max_overhead_pct_est_known ||
				overhead > _shadowEvalMatrixDecisionAggregate.max_overhead_pct_est)
			{
				_shadowEvalMatrixDecisionAggregate.max_overhead_pct_est = overhead;
			}
			_shadowEvalMatrixDecisionAggregate.max_overhead_pct_est_known = true;
		}
	}

	private CalibrationDecisionRecord BuildShadowEvalMatrixFinalDecision()
	{
		CalibrationShadowEvalMatrixInput matrixInput = new CalibrationShadowEvalMatrixInput
		{
			shadow_pair_count = _shadowEvalMatrixDecisionAggregate.shadow_pair_count,
			any_pair_reject = _shadowEvalMatrixDecisionAggregate.any_pair_reject,
			any_pair_defer = _shadowEvalMatrixDecisionAggregate.any_pair_defer,
			max_overhead_pct_est_known = _shadowEvalMatrixDecisionAggregate.max_overhead_pct_est_known,
			max_overhead_pct_est = _shadowEvalMatrixDecisionAggregate.max_overhead_pct_est,
			canonical_signature_hash64 = _shadowEvalMatrixDecisionAggregate.canonical_signature_hash64,
			preset_hash64 = _shadowEvalMatrixDecisionAggregate.preset_hash64
		};
		return CalibrationAcceptancePolicy.DecideFromShadowEvalMatrix(in matrixInput);
	}

	private CalibrationDecisionRecord CoercePairDecisionForMatrixAggregation(in CalibrationDecisionRecord pairRecord)
	{
		if (!ShouldAggregateShadowEvalDecisionAcrossMatrix())
		{
			return pairRecord;
		}

		if (pairRecord.decision != CalibrationDecision.Accept)
		{
			return pairRecord;
		}

		CalibrationDecisionRecord pending = pairRecord;
		pending.decision = CalibrationDecision.Defer;
		pending.reason = "matrix_pending";
		pending.verdict = "matrix";
		return pending;
	}

	private void EmitShadowEvalMatrixFinalDecisionIfNeeded()
	{
		if (!ShouldAggregateShadowEvalDecisionAcrossMatrix())
		{
			return;
		}

		if (_shadowEvalMatrixDecisionAggregate.shadow_pair_count <= 0)
		{
			return;
		}

		CalibrationDecisionRecord finalRecord = BuildShadowEvalMatrixFinalDecision();
		EmitShadowEvalDecisionLog(_shadowEvalMatrixDecisionAggregate.last_baseline_run_id, finalRecord);
	}

	private void EmitShadowEvalComparisonLog(ShadowEvalRunMetrics baseline, ShadowEvalRunMetrics shadow)
	{
		string baselineTrust = baseline.TrustKnown ? (baseline.Trusted ? "1" : "0") : "na";
		string shadowTrust = shadow.TrustKnown ? (shadow.Trusted ? "1" : "0") : "na";
		string baselineElapsedMs = baseline.ElapsedMsKnown && double.IsFinite(baseline.ElapsedMs)
			? baseline.ElapsedMs.ToString("0.###")
			: "na";
		string shadowElapsedMs = shadow.ElapsedMsKnown && double.IsFinite(shadow.ElapsedMs)
			? shadow.ElapsedMs.ToString("0.###")
			: "na";
		string overheadPctEst = "na";
		double overheadPct = 0.0;
		bool overheadKnown = baseline.ElapsedMsKnown
			&& shadow.ElapsedMsKnown
			&& baseline.ElapsedMs >= 0.0
			&& shadow.ElapsedMs >= 0.0
			&& double.IsFinite(baseline.ElapsedMs)
			&& double.IsFinite(shadow.ElapsedMs);
		if (overheadKnown)
		{
			double baselineDenomMs = Math.Max(1.0, baseline.ElapsedMs);
			overheadPct = ((shadow.ElapsedMs - baseline.ElapsedMs) / baselineDenomMs) * 100.0;
			if (double.IsFinite(overheadPct))
			{
				overheadPctEst = overheadPct.ToString("0.##");
			}
			else
			{
				overheadKnown = false;
			}
		}

		string verdict = ComputeShadowEvalVerdict(baseline, shadow, overheadKnown, overheadPct);
		string dominantReason = ComputeShadowEvalDominantReasonToken(baseline, shadow, overheadKnown, overheadPct);
		string dominantReasonDetail = ComputeShadowEvalDominantReasonDetailToken(dominantReason, baseline, shadow, overheadKnown, overheadPct);
		string shadowTargetMs = shadow.TargetMsPerFrame > 0 ? shadow.TargetMsPerFrame.ToString() : "na";
		string probeSigToken = _autoCalProbeSignatureHash64Known
			? $"scene_signature_hash64=0x{_autoCalProbeSignatureHash64:x16} "
			: string.Empty;
		string canonicalSigToken = _autoCalCanonicalSignatureHash64Known
			? $"canonical_signature_hash64=0x{_autoCalCanonicalSignatureHash64:x16} "
			: string.Empty;
		GD.Print(
			$"[RenderTestRunner][AutoCalShadowEval] baseline_run_id={baseline.RunId} shadow_run_id={shadow.RunId} " +
			$"baseline_trust={baselineTrust} shadow_trust={shadowTrust} " +
			$"trust_flip_baseline={FormatTrustFlipForLog(baseline)} trust_flip_shadow={FormatTrustFlipForLog(shadow)} " +
			$"dominant_reason={dominantReason} dominant_reason_detail={dominantReasonDetail} shadow_target_ms_per_frame={shadowTargetMs} " +
			$"baseline_prune={(baseline.PruneEnabled ? "on" : "off")} shadow_prune={(shadow.PruneEnabled ? "on" : "off")} " +
			$"{probeSigToken}{canonicalSigToken}" +
			$"baseline_elapsed_msec={baselineElapsedMs} shadow_elapsed_msec={shadowElapsedMs} " +
			$"overhead_pct_est={overheadPctEst} verdict={verdict}");

		CalibrationShadowEvalInput decisionInput = new CalibrationShadowEvalInput
		{
			overhead_pct_est_known = overheadKnown && double.IsFinite(overheadPct),
			overhead_pct_est = overheadPct,
			shadow_trust_known = shadow.TrustKnown,
			shadow_trust = shadow.Trusted,
			verdict = verdict,
			canonical_signature_hash64 = _autoCalCanonicalSignatureHash64Known ? _autoCalCanonicalSignatureHash64 : 0UL,
			preset_hash64 = _autoCalPresetReady ? _autoCalPreset.hash64 : 0UL
		};
		CalibrationDecisionRecord pairRecord = CalibrationAcceptancePolicy.DecideFromShadowEval(in decisionInput);
		RecordShadowEvalMatrixPairDecision(baseline.RunId, in pairRecord);
		CalibrationDecisionRecord emittedRecord = CoercePairDecisionForMatrixAggregation(in pairRecord);
		EmitShadowEvalDecisionLog(baseline.RunId, emittedRecord, allowAcceptActions: !ShouldAggregateShadowEvalDecisionAcrossMatrix());
	}

	private void EmitShadowEvalSkippedLog(ShadowEvalRunMetrics baseline, string skipReason)
	{
		string baselineTrust = baseline.TrustKnown ? (baseline.Trusted ? "1" : "0") : "na";
		string baselineElapsedMs = baseline.ElapsedMsKnown && double.IsFinite(baseline.ElapsedMs)
			? baseline.ElapsedMs.ToString("0.###")
			: "na";
		string shadowTrust = baselineTrust;
		string shadowElapsedMs = baselineElapsedMs != "na" ? baselineElapsedMs : "0";
		string dominantReason = "unknown";
		string dominantReasonDetail = "na";
		string probeSigToken = _autoCalProbeSignatureHash64Known
			? $"scene_signature_hash64=0x{_autoCalProbeSignatureHash64:x16} "
			: string.Empty;
		string canonicalSigToken = _autoCalCanonicalSignatureHash64Known
			? $"canonical_signature_hash64=0x{_autoCalCanonicalSignatureHash64:x16} "
			: string.Empty;
		GD.Print(
			$"[RenderTestRunner][AutoCalShadowEval] baseline_run_id={baseline.RunId} shadow_run_id=na " +
			$"baseline_trust={baselineTrust} shadow_trust={shadowTrust} " +
			$"trust_flip_baseline={FormatTrustFlipForLog(baseline)} trust_flip_shadow=na " +
			$"dominant_reason={dominantReason} dominant_reason_detail={dominantReasonDetail} shadow_target_ms_per_frame=na " +
			$"baseline_prune={(baseline.PruneEnabled ? "on" : "off")} shadow_prune={(baseline.PruneEnabled ? "on" : "off")} " +
			$"{probeSigToken}{canonicalSigToken}" +
			$"baseline_elapsed_msec={baselineElapsedMs} shadow_elapsed_msec={shadowElapsedMs} " +
			$"overhead_pct_est=0.0 verdict=pass skip_reason={Sanitize(skipReason)}");

		CalibrationShadowEvalInput decisionInput = new CalibrationShadowEvalInput
		{
			overhead_pct_est_known = false,
			overhead_pct_est = 0.0,
			shadow_trust_known = false,
			shadow_trust = false,
			verdict = "defer",
			canonical_signature_hash64 = _autoCalCanonicalSignatureHash64Known ? _autoCalCanonicalSignatureHash64 : 0UL,
			preset_hash64 = _autoCalPresetReady ? _autoCalPreset.hash64 : 0UL
		};
		CalibrationDecisionRecord pairRecord = CalibrationAcceptancePolicy.DecideFromShadowEval(in decisionInput);
		RecordShadowEvalMatrixPairDecision(baseline.RunId, in pairRecord);
		CalibrationDecisionRecord emittedRecord = CoercePairDecisionForMatrixAggregation(in pairRecord);
		EmitShadowEvalDecisionLog(baseline.RunId, emittedRecord, allowAcceptActions: !ShouldAggregateShadowEvalDecisionAcrossMatrix());
	}

	private void EmitShadowEvalDecisionLog(ulong baselineRunId, CalibrationDecisionRecord record, bool allowAcceptActions = true)
	{
		if (_autoCalDecisionLogDedupKeyValid &&
			_autoCalDecisionLogDedupBaselineRunId == baselineRunId &&
			_autoCalDecisionLogDedupCanonicalSignatureHash64 == record.canonical_signature_hash64 &&
			_autoCalDecisionLogDedupPresetHash64 == record.preset_hash64)
		{
			return;
		}

		_autoCalDecisionLogDedupKeyValid = true;
		_autoCalDecisionLogDedupBaselineRunId = baselineRunId;
		_autoCalDecisionLogDedupCanonicalSignatureHash64 = record.canonical_signature_hash64;
		_autoCalDecisionLogDedupPresetHash64 = record.preset_hash64;

		string decision = FormatCalibrationDecision(record.decision);
		string reason = string.IsNullOrWhiteSpace(record.reason) ? "na" : Sanitize(record.reason);
		string overheadPctEst = record.overhead_pct_est.HasValue && double.IsFinite(record.overhead_pct_est.Value)
			? record.overhead_pct_est.Value.ToString("0.##")
			: "na";
		string shadowTrust = !record.shadow_trust.HasValue
			? "na"
			: (record.shadow_trust.Value ? "1" : "0");
		string verdict = string.IsNullOrWhiteSpace(record.verdict) ? "na" : Sanitize(record.verdict);
		string dominantReason = ComputeDecisionDominantReasonToken(in record);
		GD.Print(
			$"[RenderTestRunner][AutoCalDecision] " +
			$"canonical_signature_hash64=0x{record.canonical_signature_hash64:x16} " +
			$"preset_hash64=0x{record.preset_hash64:x16} " +
			$"decision={decision} reason={reason} dominant_reason={dominantReason} " +
			$"overhead_pct_est={overheadPctEst} shadow_trust={shadowTrust} verdict={verdict}");
		_lastAutoCalState.canonical_signature_hash64 = $"0x{record.canonical_signature_hash64:x16}";
		_lastAutoCalState.preset_hash64 = $"0x{record.preset_hash64:x16}";
		_lastAutoCalState.decision = decision;
		_lastAutoCalState.decision_reason = reason;
		_lastAutoCalState.overhead_pct_est = overheadPctEst;
		_lastAutoCalState.shadow_trust = shadowTrust;
		_lastAutoCalState.verdict = verdict;

		if (!allowAcceptActions)
		{
			return;
		}

		if (record.decision == CalibrationDecision.Accept)
		{
			EmitAutoCalApplyPreviewLog(record, reason);
			QueueAcceptedAutoCalApplyForNextRun(in record, reason);
		}
		else
		{
			ClearPendingAcceptedAutoCalApply();
		}
	}

	private void EmitAutoCalApplyPreviewLog(in CalibrationDecisionRecord record, string reason)
	{
		string previewChanges = BuildAutoCalApplyPreviewChangesToken();
		GD.Print(
			$"[RenderTestRunner][AutoCalApplyPreview] " +
			$"canonical_signature_hash64=0x{record.canonical_signature_hash64:x16} " +
			$"preset_hash64=0x{record.preset_hash64:x16} " +
			$"decision=accept reason={reason} " +
			$"preview_changes={previewChanges}");
		_lastAutoCalState.canonical_signature_hash64 = $"0x{record.canonical_signature_hash64:x16}";
		_lastAutoCalState.preset_hash64 = $"0x{record.preset_hash64:x16}";
		_lastAutoCalState.decision = "accept";
		_lastAutoCalState.decision_reason = string.IsNullOrWhiteSpace(reason) ? "accept" : reason;
		_lastAutoCalState.preview_changes = previewChanges;
	}

	private string BuildAutoCalApplyPreviewChangesToken()
	{
		if (_autoCalPresetReady && _autoCalPreset.IsNoOp)
		{
			return "noop";
		}

		if (_autoCalPresetReady)
		{
			System.Collections.Generic.List<string> parts = null;
			if (_autoCalPreset.enable_tlas_prune.HasValue)
			{
				parts ??= new System.Collections.Generic.List<string>(2);
				parts.Add($"prune_enabled:{(_autoCalPreset.enable_tlas_prune.Value ? "true" : "false")}");
			}
			if (_autoCalPreset.target_ms_per_frame.HasValue)
			{
				parts ??= new System.Collections.Generic.List<string>(2);
				parts.Add($"target_ms_per_frame:{_autoCalPreset.target_ms_per_frame.Value}");
			}
			if (parts != null && parts.Count > 0)
			{
				return string.Join(",", parts);
			}
		}

		return "unimplemented_fields";
	}

	private void QueueAcceptedAutoCalApplyForNextRun(in CalibrationDecisionRecord record, string reason)
	{
		if (!ApplyAutoCalibratedPreset)
		{
			EmitAutoCalApplyLog(record.canonical_signature_hash64, record.preset_hash64, "next_run", "skipped", "flag_off");
			ClearPendingAcceptedAutoCalApply();
			return;
		}

		if (!_autoCalPresetReady)
		{
			EmitAutoCalApplyLog(record.canonical_signature_hash64, record.preset_hash64, "next_run", "skipped", "preset_not_ready");
			ClearPendingAcceptedAutoCalApply();
			return;
		}

		if (_autoCalPreset.IsNoOp)
		{
			EmitAutoCalApplyLog(record.canonical_signature_hash64, record.preset_hash64, "next_run", "skipped", "noop_preset");
			ClearPendingAcceptedAutoCalApply();
			return;
		}

		if (_autoCalPreset.hash64 != record.preset_hash64)
		{
			EmitAutoCalApplyLog(record.canonical_signature_hash64, record.preset_hash64, "next_run", "skipped", "preset_hash_mismatch");
			ClearPendingAcceptedAutoCalApply();
			return;
		}

		if (_autoCalCanonicalSignatureHash64Known && _autoCalCanonicalSignatureHash64 != record.canonical_signature_hash64)
		{
			EmitAutoCalApplyLog(record.canonical_signature_hash64, record.preset_hash64, "next_run", "skipped", "canonical_hash_mismatch");
			ClearPendingAcceptedAutoCalApply();
			return;
		}

		_autoCalAcceptedApplyPending = true;
		_autoCalAcceptedApplyPendingCanonicalSignatureHash64 = record.canonical_signature_hash64;
		_autoCalAcceptedApplyPendingPresetHash64 = record.preset_hash64;
		_autoCalAcceptedApplyPendingReason = string.IsNullOrWhiteSpace(reason) ? "accept" : Sanitize(reason);
	}

	private void MaybeApplyAcceptedAutoCalPresetToNextRun()
	{
		if (!_autoCalAcceptedApplyPending)
		{
			return;
		}

		// Apply only to the next baseline run after an accepted shadow-eval decision.
		if (_shadowEvalActiveRun)
		{
			return;
		}

		ulong canonicalHash = _autoCalAcceptedApplyPendingCanonicalSignatureHash64;
		ulong presetHash = _autoCalAcceptedApplyPendingPresetHash64;
		string reason = string.IsNullOrWhiteSpace(_autoCalAcceptedApplyPendingReason)
			? "accept"
			: Sanitize(_autoCalAcceptedApplyPendingReason);

		try
		{
			if (!_autoCalPresetReady)
			{
				EmitAutoCalApplyLog(canonicalHash, presetHash, "next_run", "skipped", "preset_not_ready");
				return;
			}

			if (_autoCalPreset.IsNoOp)
			{
				EmitAutoCalApplyLog(canonicalHash, presetHash, "next_run", "skipped", "noop_preset");
				return;
			}

			if (_autoCalPreset.hash64 != presetHash)
			{
				EmitAutoCalApplyLog(canonicalHash, presetHash, "next_run", "skipped", "preset_hash_mismatch");
				return;
			}

			if (_autoCalCanonicalSignatureHash64Known && _autoCalCanonicalSignatureHash64 != canonicalHash)
			{
				EmitAutoCalApplyLog(canonicalHash, presetHash, "next_run", "skipped", "canonical_hash_mismatch");
				return;
			}

			ApplyAutoCalPresetDeltaToRunConfig(in _autoCalPreset, ref _activeRunConfig);
			_autoCalAcceptedApplyActiveRun = true;
			_autoCalAcceptedApplyActiveRunCanonicalSignatureHash64Known = canonicalHash != 0UL;
			_autoCalAcceptedApplyActiveRunCanonicalSignatureHash64 = canonicalHash;
			_autoCalAcceptedApplyActiveRunPresetHash64Known = presetHash != 0UL;
			_autoCalAcceptedApplyActiveRunPresetHash64 = presetHash;
			EmitAutoCalApplyLog(canonicalHash, presetHash, "next_run", "applied", reason);
		}
		finally
		{
			ClearPendingAcceptedAutoCalApply();
		}
	}

	private void EmitAutoCalApplyLog(ulong canonicalSignatureHash64, ulong presetHash64, string applyScope, string status, string reason)
	{
		string reasonToken = string.IsNullOrWhiteSpace(reason) ? "na" : Sanitize(reason);
		GD.Print(
			$"[RenderTestRunner][AutoCalApply] " +
			$"canonical_signature_hash64=0x{canonicalSignatureHash64:x16} " +
			$"preset_hash64=0x{presetHash64:x16} " +
			$"apply_scope={Sanitize(applyScope)} status={Sanitize(status)} reason={reasonToken}");
		_lastAutoCalState.canonical_signature_hash64 = $"0x{canonicalSignatureHash64:x16}";
		_lastAutoCalState.preset_hash64 = $"0x{presetHash64:x16}";
		_lastAutoCalState.apply_scope = Sanitize(applyScope);
		_lastAutoCalState.apply_status = Sanitize(status);
		_lastAutoCalState.apply_reason = reasonToken;
	}

	private void EmitAutoCalAppliedRunStartMarkerIfNeeded()
	{
		if (!_autoCalAcceptedApplyActiveRun)
		{
			return;
		}

		ulong runId = _runExecSequence + 1UL;
		string canonicalSigToken = _autoCalAcceptedApplyActiveRunCanonicalSignatureHash64Known
			? $" canonical_signature_hash64=0x{_autoCalAcceptedApplyActiveRunCanonicalSignatureHash64:x16}"
			: string.Empty;
		string presetHashToken = _autoCalAcceptedApplyActiveRunPresetHash64Known
			? $" preset_hash64=0x{_autoCalAcceptedApplyActiveRunPresetHash64:x16}"
			: string.Empty;
		GD.Print(
			$"[RenderTestRunner][AutoCalAppliedRun] run_id={runId} applied=1{canonicalSigToken}{presetHashToken} apply_scope=next_run");
		if (_autoCalAcceptedApplyActiveRunCanonicalSignatureHash64Known)
		{
			_lastAutoCalState.canonical_signature_hash64 = $"0x{_autoCalAcceptedApplyActiveRunCanonicalSignatureHash64:x16}";
		}
		if (_autoCalAcceptedApplyActiveRunPresetHash64Known)
		{
			_lastAutoCalState.preset_hash64 = $"0x{_autoCalAcceptedApplyActiveRunPresetHash64:x16}";
		}
		_lastAutoCalState.apply_scope = "next_run";
		_lastAutoCalState.apply_status = "applied";
	}

	private void ClearPendingAcceptedAutoCalApply()
	{
		_autoCalAcceptedApplyPending = false;
		_autoCalAcceptedApplyPendingCanonicalSignatureHash64 = 0UL;
		_autoCalAcceptedApplyPendingPresetHash64 = 0UL;
		_autoCalAcceptedApplyPendingReason = string.Empty;
	}

	private static string FormatTrustFlipForLog(ShadowEvalRunMetrics metrics)
	{
		return "0";
	}

	public string GetAutoCalStatusLine()
	{
		List<string> parts = new List<string>(10)
		{
			$"smart={(EnableSceneAutoCalibration ? "on" : "off")}"
		};

		if (!string.IsNullOrWhiteSpace(_lastAutoCalState.scene_archetype))
		{
			parts.Add($"archetype={FormatAutoCalStatusValue(_lastAutoCalState.scene_archetype)}");
		}
		if (!string.IsNullOrWhiteSpace(_lastAutoCalState.decision))
		{
			parts.Add($"decision={FormatAutoCalStatusValue(_lastAutoCalState.decision)}");
		}
		if (!string.IsNullOrWhiteSpace(_lastAutoCalState.decision_reason))
		{
			parts.Add($"reason={FormatAutoCalStatusValue(_lastAutoCalState.decision_reason)}");
		}
		if (!string.IsNullOrWhiteSpace(_lastAutoCalState.preview_changes))
		{
			parts.Add($"preview={FormatAutoCalStatusValue(_lastAutoCalState.preview_changes)}");
		}
		if (!string.IsNullOrWhiteSpace(_lastAutoCalState.apply_status))
		{
			string applyToken = _lastAutoCalState.apply_status;
			if (!string.IsNullOrWhiteSpace(_lastAutoCalState.apply_scope))
			{
				applyToken += $"/{_lastAutoCalState.apply_scope}";
			}
			parts.Add($"apply={FormatAutoCalStatusValue(applyToken)}");
		}
		if (!string.IsNullOrWhiteSpace(_lastAutoCalState.overhead_pct_est) &&
			!string.Equals(_lastAutoCalState.overhead_pct_est, "na", StringComparison.OrdinalIgnoreCase))
		{
			parts.Add($"overhead={FormatAutoCalStatusValue(_lastAutoCalState.overhead_pct_est)}");
		}
		if (!string.IsNullOrWhiteSpace(_lastAutoCalState.verdict) &&
			!string.Equals(_lastAutoCalState.verdict, "na", StringComparison.OrdinalIgnoreCase))
		{
			parts.Add($"verdict={FormatAutoCalStatusValue(_lastAutoCalState.verdict)}");
		}
		if (!string.IsNullOrWhiteSpace(_lastAutoCalState.canonical_signature_hash64))
		{
			parts.Add($"sig={FormatAutoCalStatusValue(_lastAutoCalState.canonical_signature_hash64)}");
		}
		if (!string.IsNullOrWhiteSpace(_lastAutoCalState.preset_hash64))
		{
			parts.Add($"preset={FormatAutoCalStatusValue(_lastAutoCalState.preset_hash64)}");
		}

		return TruncateAutoCalStatusLine($"AutoCal: {string.Join(" | ", parts)}", 320);
	}

	private static string FormatCalibrationDecision(CalibrationDecision decision)
	{
		switch (decision)
		{
			case CalibrationDecision.Accept:
				return "accept";
			case CalibrationDecision.Reject:
				return "reject";
			default:
				return "defer";
		}
	}

	private static string ComputeShadowEvalVerdict(ShadowEvalRunMetrics baseline, ShadowEvalRunMetrics shadow, bool overheadKnown, double overheadPct)
	{
		if (!baseline.TrustKnown || !shadow.TrustKnown || !overheadKnown || !double.IsFinite(overheadPct))
		{
			return "defer";
		}

		if (!shadow.Trusted || overheadPct > 3.0)
		{
			return "fail";
		}

		if (baseline.Trusted && shadow.Trusted && overheadPct <= 3.0)
		{
			return "pass";
		}

		return "defer";
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
		try
		{
			GrinFilmCamera.TestRunConfig run = _activeRunConfigReady ? _activeRunConfig : _runs[_runIndex];
			_runExecSequence++;
			ulong runExecId = _runExecSequence;
			_activeRunHarnessStopwatch.Stop();
			double runElapsedMs = _activeRunHarnessStopwatch.Elapsed.TotalMilliseconds;
			bool runElapsedKnown = double.IsFinite(runElapsedMs) && runElapsedMs >= 0.0;
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
				$"run_id={runExecId} frames={FramesPerRun} warmup={WarmupFrames} samples={sampleCount}");
			GD.Print(
				$"[RUN END] name={Sanitize(run.Name)} run_id={runExecId} frames={FramesPerRun} samples={sampleCount} " +
				$"meanMs={meanMsStr} p95Ms={p95MsStr}");

			bool hasRhLiveSnapshot = TryGetLatestRenderHealthLiveSnapshot(out RenderHealthLiveSnapshot finalRhSnap);
			ShadowEvalRunMetrics metrics = new ShadowEvalRunMetrics
			{
				RunId = runExecId,
				TrustKnown = hasRh,
				Trusted = hasRh && trusted,
				TrustReason = hasRh ? (trustReason ?? string.Empty) : string.Empty,
				TrustFlipEst = (hasRh && trusted) ? 1 : 0,
				GeomPixProcessedKnown = hasRhLiveSnapshot,
				GeomPixProcessed = hasRhLiveSnapshot ? finalRhSnap.GeomPixProcessed : 0L,
				GeomRayTestsTotalKnown = hasRhLiveSnapshot,
				GeomRayTestsTotal = hasRhLiveSnapshot ? finalRhSnap.GeomRayTestsTotal : 0L,
				MeanMsKnown = sampleCount > 0 && double.IsFinite(meanMs) && meanMs >= 0.0,
				MeanMs = meanMs,
				ElapsedMsKnown = runElapsedKnown,
				ElapsedMs = runElapsedMs,
				PruneEnabled = ResolveRunPruneEnabled(in run),
				TargetMsPerFrame = ResolveRunTargetMsPerFrame(in run)
			};

			if (IsLifecycleStressActive())
			{
				ValidateLifecycleStressRunAssertions(in run, runExecId);
			}

			if (ShouldRunShadowCalibrationEvaluation())
			{
				if (_shadowEvalActiveRun)
				{
					if (_hasPendingShadowEvalBaselineResult)
					{
						EmitShadowEvalComparisonLog(_pendingShadowEvalBaselineResult, metrics);
					}
					else
					{
						GD.PrintErr("[RenderTestRunner][AutoCalShadowEval] missing baseline metrics for shadow comparison.");
					}
					_hasPendingShadowEvalBaselineResult = false;
				}
				else
				{
					_pendingShadowEvalBaselineResult = metrics;
					_hasPendingShadowEvalBaselineResult = true;
					if (_autoCalPresetReady && _autoCalPreset.IsNoOp)
					{
						_shadowEvalPendingForCurrentMatrixRun = false;
						_hasPendingShadowEvalBaselineResult = false;
						EmitShadowEvalSkippedLog(metrics, "noop_preset");
					}
					else
					{
						_shadowEvalPendingForCurrentMatrixRun = true;
					}
				}
			}
		}
		finally
		{
			_activeRunHarnessStopwatch.Reset();
			if (_shadowEvalDefaultsCaptured)
			{
				if (_film != null && GodotObject.IsInstanceValid(_film))
				{
					_film.RestoreTestRunDefaults(in _shadowEvalDefaults);
				}
				_shadowEvalDefaultsCaptured = false;
			}
			_shadowEvalDefaults = default;
			if (_defaultsCaptured && _film != null && GodotObject.IsInstanceValid(_film))
			{
				_film.RestoreTestRunDefaults(in _defaults);
			}
			_activeRunConfig = default;
			_activeRunConfigReady = false;
			_autoCalAcceptedApplyActiveRun = false;
		}
	}

	private bool ResolveRunPruneEnabled(in GrinFilmCamera.TestRunConfig run)
	{
		if (run.UseGeometryTLASPruning.HasValue)
		{
			return run.UseGeometryTLASPruning.Value;
		}

		if (_shadowEvalDefaultsCaptured)
		{
			return _shadowEvalDefaults.UseGeometryTLASPruning;
		}

		if (_defaultsCaptured)
		{
			return _defaults.UseGeometryTLASPruning;
		}

		if (_film != null && GodotObject.IsInstanceValid(_film))
		{
			return _film.UseGeometryTLASPruning;
		}

		return false;
	}

	private int ResolveRunTargetMsPerFrame(in GrinFilmCamera.TestRunConfig run)
	{
		if (run.TargetMsPerFrame.HasValue)
		{
			return Math.Max(1, run.TargetMsPerFrame.Value);
		}

		if (_shadowEvalDefaultsCaptured)
		{
			return Math.Max(1, _shadowEvalDefaults.TargetMsPerFrame);
		}

		if (_defaultsCaptured)
		{
			return Math.Max(1, _defaults.TargetMsPerFrame);
		}

		if (_film != null && GodotObject.IsInstanceValid(_film))
		{
			return Math.Max(1, _film.TargetMsPerFrame);
		}

		return 0;
	}

	private static string ComputeShadowEvalDominantReasonToken(ShadowEvalRunMetrics baseline, ShadowEvalRunMetrics shadow, bool overheadKnown, double overheadPct)
	{
		if (shadow.TrustKnown && !shadow.Trusted)
		{
			return MapTrustReasonToDominantReasonToken(shadow.TrustReason);
		}

		if (baseline.TrustKnown && !baseline.Trusted)
		{
			return MapTrustReasonToDominantReasonToken(baseline.TrustReason);
		}

		if (overheadKnown && double.IsFinite(overheadPct) && overheadPct > 3.0)
		{
			return "overhead";
		}

		return "unknown";
	}

	private string ComputeShadowEvalDominantReasonDetailToken(string dominantReason, ShadowEvalRunMetrics baseline, ShadowEvalRunMetrics shadow, bool overheadKnown, double overheadPct)
	{
		if (string.Equals(dominantReason, "low_geom_pix", StringComparison.OrdinalIgnoreCase))
		{
			ShadowEvalRunMetrics source = shadow;
			bool shadowMatches =
				shadow.TrustKnown &&
				!shadow.Trusted &&
				!string.IsNullOrWhiteSpace(shadow.TrustReason) &&
				shadow.TrustReason.IndexOf("low_geom_pix", StringComparison.OrdinalIgnoreCase) >= 0;
			if (!shadowMatches)
			{
				source = baseline;
			}

			if (!source.GeomPixProcessedKnown)
			{
				return $"geomPix_na_min{_renderTestMinGeomPixForTrust}";
			}

			long deficit = Math.Max(0L, (long)_renderTestMinGeomPixForTrust - source.GeomPixProcessed);
			return $"geomPix{source.GeomPixProcessed}_min{_renderTestMinGeomPixForTrust}_def{deficit}";
		}

		if (string.Equals(dominantReason, "low_raytests", StringComparison.OrdinalIgnoreCase))
		{
			ShadowEvalRunMetrics source = shadow;
			bool shadowMatches =
				shadow.TrustKnown &&
				!shadow.Trusted &&
				!string.IsNullOrWhiteSpace(shadow.TrustReason) &&
				shadow.TrustReason.IndexOf("low_raytests", StringComparison.OrdinalIgnoreCase) >= 0;
			if (!shadowMatches)
			{
				source = baseline;
			}

			if (!source.GeomRayTestsTotalKnown)
			{
				return $"geomRays_na_min{_renderTestMinGeomRayTestsForTrust}";
			}

			long deficit = Math.Max(0L, (long)_renderTestMinGeomRayTestsForTrust - source.GeomRayTestsTotal);
			return $"geomRays{source.GeomRayTestsTotal}_min{_renderTestMinGeomRayTestsForTrust}_def{deficit}";
		}

		if (string.Equals(dominantReason, "overhead", StringComparison.OrdinalIgnoreCase) &&
			overheadKnown &&
			double.IsFinite(overheadPct))
		{
			return $"overheadPct{overheadPct:0.##}";
		}

		return "na";
	}

	private static string ComputeDecisionDominantReasonToken(in CalibrationDecisionRecord record)
	{
		string reason = string.IsNullOrWhiteSpace(record.reason) ? string.Empty : record.reason.Trim();
		if (string.Equals(reason, "matrix_pending", StringComparison.OrdinalIgnoreCase))
		{
			return "matrix_pending";
		}

		if (reason.IndexOf("overhead", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "overhead";
		}

		return "unknown";
	}

	private static string MapTrustReasonToDominantReasonToken(string trustReason)
	{
		if (string.IsNullOrWhiteSpace(trustReason))
		{
			return "unknown";
		}

		string reason = trustReason.Trim();
		if (reason.IndexOf("low_raytests", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "low_raytests";
		}

		if (reason.IndexOf("low_geom_pix", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "low_geom_pix";
		}

		return "unknown";
	}

	private void FinishMatrix()
	{
		if (!_matrixRunning)
		{
			return;
		}

		EmitShadowEvalMatrixFinalDecisionIfNeeded();

		_matrixRunning = false;
		_harnessState = HarnessState.Idle;
		_startupDelayFramesRemaining = 0;
		_interRunDelayFramesRemaining = 0;
		_shadowEvalPendingForCurrentMatrixRun = false;
		_shadowEvalActiveRun = false;
		_shadowEvalDefaultsCaptured = false;
		_shadowEvalDefaults = default;
		_activeRunConfigReady = false;
		_activeRunConfig = default;
		_hasPendingShadowEvalBaselineResult = false;
		_autoCalPresetReady = false;
		_autoCalPreset = default;
		_autoCalProbeSignatureHash64Known = false;
		_autoCalProbeSignatureHash64 = 0UL;
		_autoCalCanonicalSignatureHash64Known = false;
		_autoCalCanonicalSignatureHash64 = 0UL;
		_autoCalAcceptedApplyActiveRun = false;
		_autoCalDecisionLogDedupKeyValid = false;
		_autoCalDecisionLogDedupBaselineRunId = 0UL;
		_autoCalDecisionLogDedupCanonicalSignatureHash64 = 0UL;
		_autoCalDecisionLogDedupPresetHash64 = 0UL;
		ResetShadowEvalMatrixDecisionAggregate();
		ClearPendingAcceptedAutoCalApply();
		_activeRunHarnessStopwatch.Reset();
		_runExecSequence = 0;
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

		if (IsLifecycleStressActive())
		{
			bool cycleFailed = s_lifecycleStress.TotalFailures > s_lifecycleStress.TotalFailuresAtCycleStart;
			s_lifecycleStress.CompletedCycles++;
			if (cycleFailed)
			{
				s_lifecycleStress.FailedCycles++;
			}

			GD.Print(
				$"[RenderTestRunner][LifecycleStress][CYCLE END] cycle={s_lifecycleStress.CompletedCycles}/{s_lifecycleStress.TargetCycles} " +
				$"failed={(cycleFailed ? 1 : 0)} total_failures={s_lifecycleStress.TotalFailures}");

			if (s_lifecycleStress.CompletedCycles < s_lifecycleStress.TargetCycles)
			{
				string scenePath = GetScenePathForFixture(_requestedFixture);
				GD.Print(
					$"[RenderTestRunner][LifecycleStress][RELOAD] next_cycle={s_lifecycleStress.CompletedCycles + 1}/{s_lifecycleStress.TargetCycles} " +
					$"scene={scenePath}");
				StopFilmRenderingForExitIfNeeded();
				CallDeferred(nameof(SwitchToFixtureSceneDeferred), scenePath);
				return;
			}

			GD.Print(
				$"[RenderTestRunner][LifecycleStress][SUMMARY] cycles={s_lifecycleStress.CompletedCycles} " +
				$"failed_cycles={s_lifecycleStress.FailedCycles} total_failures={s_lifecycleStress.TotalFailures}");
			for (int i = 0; i < s_lifecycleStress.FailureDetails.Count; i++)
			{
				GD.PrintErr($"[RenderTestRunner][LifecycleStress][SUMMARY][FAIL {i + 1}] {s_lifecycleStress.FailureDetails[i]}");
			}

			int exitCode = s_lifecycleStress.TotalFailures == 0 ? 0 : 1;
			s_lifecycleStress.Active = false;
			GD.Print($"[RenderTestRunner] Lifecycle stress complete. Requesting deferred quit code={exitCode}");
			StopFilmRenderingForExitIfNeeded();
			CallDeferred(nameof(QuitDeferred), exitCode);
			return;
		}

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

	private void ApplyStartupCliFlagOverrides()
	{
		if (TryGetBoolCmdArgValue(AutoCalCmdArgPrefix, out bool autoCalEnabled))
		{
			EnableSceneAutoCalibration = autoCalEnabled;
		}
		if (TryGetBoolCmdArgValue(ShadowEvalCmdArgPrefix, out bool shadowEvalEnabled))
		{
			EnableShadowCalibrationEvaluation = shadowEvalEnabled;
		}
		if (TryGetBoolCmdArgValue(AutoCalVerboseCmdArgPrefix, out bool autoCalVerbose))
		{
			AutoCalVerboseLogs = autoCalVerbose;
			ShadowEvalVerboseLogs = autoCalVerbose;
		}
		if (TryGetBoolCmdArgValue(AutoCalApplyCmdArgPrefix, out bool autoCalApply))
		{
			ApplyAutoCalibratedPreset = autoCalApply;
		}
		if (TryGetIntCmdArgValue(ShadowPruneOffTargetMsCmdArgPrefix, out int shadowPruneOffTargetMs))
		{
			_shadowPruneOffTargetMsOverride = Math.Max(1, shadowPruneOffTargetMs);
		}
	}

	private void ConfigureLifecycleStressSessionFromFlags()
	{
		bool cliOverride = TryGetBoolCmdArgValue(LifecycleStressCmdArgPrefix, out bool cliEnable);
		bool enable = cliOverride ? cliEnable : EnableLifecycleStressPass;
		if (!enable)
		{
			return;
		}

		s_lifecycleStress.Active = true;
		s_lifecycleStress.TargetCycles = Math.Max(1, LifecycleStressCycles);
		s_lifecycleStress.FramesPerRun = Math.Max(1, LifecycleStressFramesPerRun);
		s_lifecycleStress.WarmupFrames = Math.Max(0, LifecycleStressWarmupFrames);

		if (TryGetIntCmdArgValue(LifecycleStressCyclesCmdArgPrefix, out int cliCycles))
		{
			s_lifecycleStress.TargetCycles = Math.Max(1, cliCycles);
		}
		if (TryGetIntCmdArgValue(LifecycleStressFramesCmdArgPrefix, out int cliFrames))
		{
			s_lifecycleStress.FramesPerRun = Math.Max(1, cliFrames);
		}
		if (TryGetIntCmdArgValue(LifecycleStressWarmupCmdArgPrefix, out int cliWarmup))
		{
			s_lifecycleStress.WarmupFrames = Math.Max(0, cliWarmup);
		}
	}

	private static bool IsLifecycleStressActive()
	{
		return s_lifecycleStress.Active;
	}

	private static bool TryGetIntCmdArgValue(string argPrefix, out int value)
	{
		value = 0;
		if (string.IsNullOrWhiteSpace(argPrefix))
		{
			return false;
		}

		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (string.IsNullOrWhiteSpace(arg))
			{
				continue;
			}

			string trimmed = arg.Trim();
			if (!trimmed.StartsWith(argPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string rawValue = trimmed.Substring(argPrefix.Length).Trim();
			if (int.TryParse(rawValue, out int parsed))
			{
				value = parsed;
				return true;
			}
		}

		return false;
	}

	private static bool TryGetBoolCmdArgValue(string argPrefix, out bool value)
	{
		value = false;
		if (string.IsNullOrWhiteSpace(argPrefix))
		{
			return false;
		}

		string[] args = OS.GetCmdlineUserArgs();
		string exactToken = argPrefix.EndsWith("=", StringComparison.Ordinal)
			? argPrefix.Substring(0, argPrefix.Length - 1)
			: argPrefix;

		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i];
			if (string.IsNullOrWhiteSpace(arg))
			{
				continue;
			}

			string trimmed = arg.Trim();
			if (!trimmed.StartsWith(argPrefix, StringComparison.OrdinalIgnoreCase))
			{
				if (!string.IsNullOrWhiteSpace(exactToken)
					&& string.Equals(trimmed, exactToken, StringComparison.OrdinalIgnoreCase)
					&& (i + 1) < args.Length)
				{
					string nextRaw = args[i + 1];
					if (string.IsNullOrWhiteSpace(nextRaw))
					{
						continue;
					}

					string nextValue = nextRaw.Trim();
					if (string.Equals(nextValue, "1", StringComparison.Ordinal))
					{
						value = true;
						return true;
					}
					if (string.Equals(nextValue, "0", StringComparison.Ordinal))
					{
						value = false;
						return true;
					}
				}
				continue;
			}

			string rawValue = trimmed.Substring(argPrefix.Length).Trim();
			if (string.Equals(rawValue, "1", StringComparison.Ordinal))
			{
				value = true;
				return true;
			}
			if (string.Equals(rawValue, "0", StringComparison.Ordinal))
			{
				value = false;
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
			if (string.Equals(value, "curved_minimal", StringComparison.OrdinalIgnoreCase))
			{
				fixture = RenderTestFixture.CurvedMinimal;
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
		return fixture switch
		{
			RenderTestFixture.Straight => RenderTestStraightScenePath,
			RenderTestFixture.CurvedMinimal => RenderTestCurvedMinimalScenePath,
			_ => RenderTestDefaultScenePath
		};
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

	private static string FormatAutoCalNotesForLog(string value, int maxChars)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return "-";
		}

		string trimmed = value.Trim();
		if (maxChars > 0 && trimmed.Length > maxChars)
		{
			trimmed = trimmed.Substring(0, maxChars);
		}

		return Sanitize(trimmed);
	}

	private static string FormatAutoCalStatusValue(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return "na";
		}

		string sanitized = value.Trim()
			.Replace('\r', ' ')
			.Replace('\n', ' ')
			.Replace('\t', ' ');
		while (sanitized.Contains("  ", StringComparison.Ordinal))
		{
			sanitized = sanitized.Replace("  ", " ", StringComparison.Ordinal);
		}

		return sanitized;
	}

	private static string TruncateAutoCalStatusLine(string value, int maxChars)
	{
		if (string.IsNullOrEmpty(value) || maxChars <= 0 || value.Length <= maxChars)
		{
			return value;
		}

		if (maxChars <= 3)
		{
			return value.Substring(0, maxChars);
		}

		return value.Substring(0, maxChars - 3) + "...";
	}

}
