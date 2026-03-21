using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public partial class GrinBasicVisualController : Node3D
{
	private const string ROuterArgPrefix = "--grin-basic-r-outer=";
	private const string AmpArgPrefix = "--grin-basic-amp=";
	private const string GammaArgPrefix = "--grin-basic-gamma=";
	private const string StepScaleArgPrefix = "--grin-basic-step-scale=";
	private const string StepLengthArgPrefix = "--grin-basic-step-length=";
	private const string MinStepLengthArgPrefix = "--grin-basic-min-step-length=";
	private const string MaxStepLengthArgPrefix = "--grin-basic-max-step-length=";
	private const string StepsPerRayArgPrefix = "--grin-basic-steps-per-ray=";
	private const string TurnThresholdArgPrefix = "--grin-basic-turn-threshold=";
	private const string MetricGainArgPrefix = "--grin-basic-metric-gain=";
	private const string BendScaleArgPrefix = "--grin-basic-bend-scale=";
	private const string FieldStrengthArgPrefix = "--grin-basic-field-strength=";
	private const string CaptureArgPrefix = "--grin-basic-capture=";
	private const string SettleFramesArgPrefix = "--grin-basic-settle-frames=";
	private const string MinRenderHealthStepArgPrefix = "--grin-basic-min-rh-step=";
	private const string MinProcessedRowsArgPrefix = "--grin-basic-min-processed-rows=";
	private const string CaptureFilmOpacityArgPrefix = "--grin-basic-capture-film-opacity=";
	private const string ExitAfterCaptureArgPrefix = "--grin-basic-exit-after-capture=";
	private const string CompareGridArgPrefix = "--grin-basic-compare-grid=";
	private const string CompareCrosshairArgPrefix = "--grin-basic-compare-crosshair=";

	[Export] public NodePath FilmCameraPath = new("GrinFilmCamera");
	[Export] public NodePath FieldPath = new("FixtureGrinBasicVisual/FieldSource3D");
	[Export] public NodePath OverlayPath = new("CanvasLayer/FilmOverlay2D");
	[Export] public string FixtureHudName = "grin_basic_visual";
	[Export] public string SourcePatternMode = "dot_grid";
	[Export] public float DefaultROuterOverride = -1.0f;
	[Export] public float DefaultAmpOverride = -1.0f;
	[Export] public float DefaultGammaOverride = -1.0f;
	[Export] public bool ComparisonGridEnabled = true;
	[Export] public bool ComparisonCrosshairEnabled = true;
	[Export(PropertyHint.Range, "0,8,1")] public int StartupPhysicsFramesDelay = 2;
	[Export(PropertyHint.Range, "1,240,1")] public int DefaultCaptureSettleFrames = 12;
	[Export(PropertyHint.Range, "1,2048,1")] public int DefaultCaptureTimeoutFrames = 240;
	[Export(PropertyHint.Range, "0,240,1")] public int DefaultCaptureMinRenderHealthStep = 0;
	[Export(PropertyHint.Range, "0,4096,1")] public int DefaultCaptureMinProcessedRows = 0;
	[Export(PropertyHint.Range, "-1,1,0.01")] public float DefaultCaptureFilmOpacityOverride = 1.0f;

	private GrinFilmCamera _filmCamera;
	private FieldSource3D _field;
	private FilmOverlay2D _filmOverlay;
	private RayBeamRenderer _rayBeamRenderer;
	private bool _intendedFullRender;
	private bool _captureRequested;
	private bool _exitAfterCapture;
	private bool _captureComplete;
	private bool _startupComplete;
	private bool _quitRequested;
	private int _captureSettleFrames = 12;
	private int _captureTimeoutFrames = 240;
	private int _captureReadyFrames;
	private int _captureObservedFrames;
	private int _captureMinRenderHealthStep;
	private int _captureMinProcessedRows;
	private int _captureMaxObservedRenderHealthStep = -1;
	private int _captureMaxObservedProcessedRows;
	private bool _captureObservedAnyTracedPixels;
	private int _pendingQuitCode;
	private float _captureFilmOpacityOverride = -1.0f;
	private string _capturePath = string.Empty;
	private RendererConfigSnapshot _rendererConfig;

	public override void _Ready()
	{
		_filmCamera = GetNodeOrNull<GrinFilmCamera>(FilmCameraPath);
		_field = GetNodeOrNull<FieldSource3D>(FieldPath);
		_filmOverlay = GetNodeOrNull<FilmOverlay2D>(OverlayPath);
		_rayBeamRenderer = ResolveRayBeamRenderer();
		_intendedFullRender = _filmCamera != null && _filmCamera.UpdateEveryFrame;
		ConfigureCalibrationViewport();
		TagDotNodesAsFixtureSource();

		CmdlineOptions options = ParseCmdlineOptions(GetCmdArgsForParsing());
		_captureRequested = !string.IsNullOrWhiteSpace(options.CapturePath);
		_exitAfterCapture = options.ExitAfterCapture;
		_capturePath = options.CapturePath ?? string.Empty;
		_captureSettleFrames = Math.Max(1, options.SettleFrames ?? DefaultCaptureSettleFrames);
		_captureTimeoutFrames = Math.Max(_captureSettleFrames, DefaultCaptureTimeoutFrames);
		_captureMinRenderHealthStep = Math.Max(0, options.MinRenderHealthStep ?? DefaultCaptureMinRenderHealthStep);
		_captureMinProcessedRows = Math.Max(0, options.MinProcessedRows ?? DefaultCaptureMinProcessedRows);
		_captureFilmOpacityOverride = options.CaptureFilmOpacity ?? DefaultCaptureFilmOpacityOverride;
		ApplyComparisonOverlayOptions(options);

		if (_field != null)
		{
			ApplyFieldOverrides(_field, options);
		}
		if (_rayBeamRenderer != null)
		{
			_rendererConfig = ApplyRendererOverrides(_rayBeamRenderer, options);
		}

		string modeToken = _intendedFullRender ? "FULL_RENDER" : "FIXTURE_PROBE";
		string actualScenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		string expectedScenePath = LauncherAudit.GetCanonicalScenePathForFixtureToken(FixtureHudName);
		bool enforceLaunchMatch = LauncherAudit.GetRequestedLauncher().Length > 0;

		if (!LauncherAudit.LogAndValidateStartup(
			expectedScenePath,
			FixtureHudName,
			actualScenePath,
			modeToken,
			enforceLaunchMatch))
		{
			if (enforceLaunchMatch)
			{
				CallDeferred(nameof(QuitLauncherMismatchDeferred));
			}
			return;
		}

		if (_filmCamera != null)
		{
			_filmCamera.SetHudFixtureName(FixtureHudName);
			_filmCamera.SetHudSourcePatternMode(SourcePatternMode);
			if (_field != null)
			{
				_filmCamera.SetHudTransportModel(_field.TransportModel.ToString());
			}
			_filmCamera.SetHudMetricGainOverride(_rendererConfig.MetricGainMultiplier, _rendererConfig.MetricGainActive);
		}

		if (_field == null)
		{
			GD.PushWarning($"[GrinBasicVisual] missing field path={FieldPath}");
			return;
		}

		FieldSource3D.ResolvedFieldParams resolved = _field.ResolveEffectiveParams(out string resolveReason);
		string betaMode = resolved.overrideBetaScale ? "override" : "global";
		GD.Print(
			$"[GrinBasicVisual] fixture={FixtureHudName} enabled={(_field.Enabled ? 1 : 0)} resolve={resolveReason} " +
			$"transport={_field.TransportModel} curve={resolved.curveType} rInner={resolved.rInner:0.###} " +
			$"rOuter={resolved.rOuter:0.###} amp={resolved.amp:0.###} gamma={resolved.a:0.###} " +
			$"betaMode={betaMode} betaScale={resolved.betaScale:0.###}");
		if (_rayBeamRenderer != null && GodotObject.IsInstanceValid(_rayBeamRenderer))
		{
			GD.Print(
				$"[GrinBasicVisual][Renderer] fixture={FixtureHudName} transport={_field.TransportModel} " +
				$"useFieldGrid={(_filmCamera != null && _filmCamera.UseFieldGrid ? 1 : 0)} " +
				$"researchEnabled={(_filmCamera != null && _filmCamera.ResearchEnabledInspector ? 1 : 0)} " +
				$"stepsPerRay={_rendererConfig.StepsPerRay} stepLength={_rendererConfig.StepLength:0.######} " +
				$"minStepLength={_rendererConfig.MinStepLength:0.######} maxStepLength={_rendererConfig.MaxStepLength:0.######} " +
				$"turnThreshold={_rendererConfig.TurnThresholdDegrees:0.######} " +
				$"bendScale={_rendererConfig.BendScale:0.######} fieldStrength={_rendererConfig.FieldStrength:0.######} " +
				$"metricGain={_rendererConfig.MetricGainMultiplier:0.######} metricGainActive={(_rendererConfig.MetricGainActive ? 1 : 0)}");
		}

		if (_captureRequested)
		{
			if (_filmCamera != null && GodotObject.IsInstanceValid(_filmCamera) && HasCaptureOpacityOverride(_captureFilmOpacityOverride))
			{
				_filmCamera.SetFilmOpacityForTesting(_captureFilmOpacityOverride);
			}
			GD.Print(
				$"[GrinBasicVisual][CaptureConfig] fixture={FixtureHudName} path={ResolveCapturePath(_capturePath)} " +
				$"settleFrames={_captureSettleFrames} timeoutFrames={_captureTimeoutFrames} " +
				$"minRhStep={_captureMinRenderHealthStep} minRows={_captureMinProcessedRows} " +
				$"filmOpacity={(HasCaptureOpacityOverride(_captureFilmOpacityOverride) ? _captureFilmOpacityOverride.ToString("0.##", CultureInfo.InvariantCulture) : "unchanged")} " +
				$"compareGrid={((_filmOverlay != null && _filmOverlay.ShowComparisonGrid) ? 1 : 0)} " +
				$"compareCrosshair={((_filmOverlay != null && _filmOverlay.ShowComparisonCrosshair) ? 1 : 0)} " +
				$"exitAfterCapture={(_exitAfterCapture ? 1 : 0)}");
			SetProcess(true);
		}

		if (_filmCamera != null && _intendedFullRender)
		{
			_filmCamera.UpdateEveryFrame = false;
			GD.Print(
				$"[GrinBasicVisual][Startup] fixture={FixtureHudName} delaying_full_render=1 " +
				$"physicsFrames={Math.Max(0, StartupPhysicsFramesDelay)}");
			CallDeferred(nameof(BeginStartupSequenceDeferred));
		}
		else
		{
			_startupComplete = true;
		}
	}

	public override void _Process(double delta)
	{
		if (!_captureRequested || _captureComplete)
		{
			return;
		}

		_captureObservedFrames++;
		if (_captureObservedFrames > _captureTimeoutFrames)
		{
			FailCaptureAndQuit(
				$"[GrinBasicVisual][Capture][FAIL] fixture={FixtureHudName} reason=timeout " +
				$"observedFrames={_captureObservedFrames} timeoutFrames={_captureTimeoutFrames}");
			return;
		}

		if (!_startupComplete)
		{
			return;
		}

		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera))
		{
			FailCaptureAndQuit($"[GrinBasicVisual][Capture][FAIL] fixture={FixtureHudName} reason=missing_film_camera");
			return;
		}

		if (!_filmCamera.TryGetFixtureDebugStatsForTesting(out GrinFilmCamera.FixtureDebugStatsSnapshot snapshot) ||
			snapshot.TracedPixels <= 0)
		{
			_captureReadyFrames = 0;
			return;
		}
		_captureObservedAnyTracedPixels = true;

		int renderHealthStep = 0;
		bool hasRenderHealthStep = _filmCamera.TryGetLatestRenderHealthStepForTesting(out renderHealthStep);
		int processedRows = Math.Max(0, _filmCamera.FilmRowCursor);
		if (hasRenderHealthStep)
		{
			_captureMaxObservedRenderHealthStep = Math.Max(_captureMaxObservedRenderHealthStep, renderHealthStep);
		}
		_captureMaxObservedProcessedRows = Math.Max(_captureMaxObservedProcessedRows, processedRows);

		int gatedRenderHealthStep = hasRenderHealthStep ? _captureMaxObservedRenderHealthStep : -1;
		int gatedProcessedRows = _captureMaxObservedProcessedRows;
		if ((_captureMinRenderHealthStep > 0 && gatedRenderHealthStep < _captureMinRenderHealthStep) ||
			(_captureMinProcessedRows > 0 && gatedProcessedRows < _captureMinProcessedRows))
		{
			_captureReadyFrames = 0;
			return;
		}

		_captureReadyFrames++;
		if (_captureReadyFrames < _captureSettleFrames)
		{
			return;
		}

		CaptureViewportAndQuit(snapshot, gatedRenderHealthStep, gatedProcessedRows);
	}

	private async void BeginStartupSequenceDeferred()
	{
		SceneTree tree = GetTree();
		if (tree == null)
		{
			_startupComplete = true;
			return;
		}

		for (int i = 0; i < Math.Max(0, StartupPhysicsFramesDelay); i++)
		{
			await ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
		}
		await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera))
		{
			_startupComplete = true;
			return;
		}

		_filmCamera.ResetFixtureDebugStatsForRunStart();
		_filmCamera.UpdateEveryFrame = _intendedFullRender;
		_startupComplete = true;
		GD.Print(
			$"[GrinBasicVisual][Startup] fixture={FixtureHudName} full_render_enabled={(_intendedFullRender ? 1 : 0)} " +
			$"physicsFrames={Math.Max(0, StartupPhysicsFramesDelay)}");
	}

	private void ConfigureCalibrationViewport()
	{
		ConfigureCalibrationTextureRect(GetNodeOrNull<TextureRect>("CanvasLayer/FilmView"));
		ConfigureCalibrationTextureRect(GetNodeOrNull<TextureRect>("CanvasLayer/FilmOverlay2D"));
	}

	private static void ConfigureCalibrationTextureRect(TextureRect rect)
	{
		if (rect == null || !GodotObject.IsInstanceValid(rect))
		{
			return;
		}

		rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		rect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
	}

	private void TagDotNodesAsFixtureSource()
	{
		Node fixtureRoot = _field?.GetParent();
		if (fixtureRoot == null || !GodotObject.IsInstanceValid(fixtureRoot))
		{
			return;
		}

		int taggedDots = 0;
		foreach (Node child in fixtureRoot.GetChildren())
		{
			string name = child?.Name.ToString() ?? string.Empty;
			if (!name.StartsWith("dot_", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			child.AddToGroup("fixture_source");
			taggedDots++;
		}

		GD.Print(
			$"[GrinBasicVisual][SourceGroup] fixture={FixtureHudName} group=fixture_source taggedDots={taggedDots}");
	}

	private void ApplyFieldOverrides(FieldSource3D field, CmdlineOptions options)
	{
		if (HasOverrideValue(DefaultROuterOverride))
		{
			field.ROuter = Mathf.Max(0f, DefaultROuterOverride);
		}
		if (HasOverrideValue(DefaultAmpOverride))
		{
			field.Amp = DefaultAmpOverride;
		}
		if (HasOverrideValue(DefaultGammaOverride))
		{
			field.CanonicalGamma = DefaultGammaOverride;
		}

		if (options.ROuter.HasValue)
		{
			field.ROuter = Mathf.Max(0f, options.ROuter.Value);
		}
		if (options.Amp.HasValue)
		{
			field.Amp = options.Amp.Value;
		}
		if (options.Gamma.HasValue)
		{
			field.CanonicalGamma = options.Gamma.Value;
		}
	}

	private RendererConfigSnapshot ApplyRendererOverrides(RayBeamRenderer rayBeamRenderer, CmdlineOptions options)
	{
		float baselineStepLength = Mathf.Max(0.0001f, rayBeamRenderer.StepLength);
		float baselineMinStepLength = Mathf.Max(0.0001f, rayBeamRenderer.MinStepLength > 0f ? rayBeamRenderer.MinStepLength : baselineStepLength);
		float baselineMaxStepLength = Mathf.Max(baselineMinStepLength, rayBeamRenderer.MaxStepLength > 0f ? rayBeamRenderer.MaxStepLength : baselineStepLength);
		int baselineStepsPerRay = Mathf.Max(1, rayBeamRenderer.StepsPerRay);
		float baselineTurnThreshold = Mathf.Clamp(rayBeamRenderer.MetricAdaptiveTurnThresholdDegrees, 0.1f, 45.0f);
		float baselineBendScale = float.IsFinite(rayBeamRenderer.BendScale) ? rayBeamRenderer.BendScale : 1.0f;
		float baselineFieldStrength = float.IsFinite(rayBeamRenderer.FieldStrength) ? rayBeamRenderer.FieldStrength : 1.0f;
		float stepScale = options.StepScale.HasValue && float.IsFinite(options.StepScale.Value) && options.StepScale.Value > 0.0f
			? options.StepScale.Value
			: 1.0f;
		float metricGain = options.MetricGain.HasValue && float.IsFinite(options.MetricGain.Value) && options.MetricGain.Value > 0.0f
			? options.MetricGain.Value
			: 1.0f;

		float minStepLength = options.MinStepLength.HasValue
			? Mathf.Max(0.0001f, options.MinStepLength.Value)
			: baselineMinStepLength * stepScale;
		float maxStepLength = options.MaxStepLength.HasValue
			? Mathf.Max(0.0001f, options.MaxStepLength.Value)
			: baselineMaxStepLength * stepScale;
		float stepLength = options.StepLength.HasValue
			? Mathf.Max(0.0001f, options.StepLength.Value)
			: baselineStepLength * stepScale;
		int stepsPerRay = options.StepsPerRay.HasValue
			? Mathf.Max(1, options.StepsPerRay.Value)
			: baselineStepsPerRay;
		float turnThresholdDegrees = options.TurnThresholdDegrees.HasValue
			? Mathf.Clamp(options.TurnThresholdDegrees.Value, 0.1f, 45.0f)
			: baselineTurnThreshold;
		float bendScale = options.BendScale.HasValue
			? options.BendScale.Value
			: baselineBendScale * metricGain;
		float fieldStrength = options.FieldStrength.HasValue
			? options.FieldStrength.Value
			: baselineFieldStrength;

		float resolvedMinStepLength = Mathf.Min(minStepLength, maxStepLength);
		float resolvedMaxStepLength = Mathf.Max(minStepLength, maxStepLength);
		rayBeamRenderer.MinStepLength = resolvedMinStepLength;
		rayBeamRenderer.MaxStepLength = resolvedMaxStepLength;
		rayBeamRenderer.StepLength = Mathf.Clamp(stepLength, resolvedMinStepLength, resolvedMaxStepLength);
		rayBeamRenderer.StepsPerRay = stepsPerRay;
		rayBeamRenderer.MetricAdaptiveTurnThresholdDegrees = turnThresholdDegrees;
		rayBeamRenderer.BendScale = bendScale;
		rayBeamRenderer.FieldStrength = fieldStrength;

		float metricGainMultiplier = 1.0f;
		bool metricGainActive = false;
		if (Mathf.Abs(baselineBendScale) > 1e-6f)
		{
			metricGainMultiplier = rayBeamRenderer.BendScale / baselineBendScale;
			metricGainActive = Mathf.Abs(metricGainMultiplier - 1.0f) > 1e-6f;
		}
		else if (options.MetricGain.HasValue)
		{
			metricGainMultiplier = metricGain;
			metricGainActive = true;
		}

		return new RendererConfigSnapshot
		{
			StepsPerRay = rayBeamRenderer.StepsPerRay,
			StepLength = rayBeamRenderer.StepLength,
			MinStepLength = rayBeamRenderer.MinStepLength,
			MaxStepLength = rayBeamRenderer.MaxStepLength,
			TurnThresholdDegrees = rayBeamRenderer.MetricAdaptiveTurnThresholdDegrees,
			BendScale = rayBeamRenderer.BendScale,
			FieldStrength = rayBeamRenderer.FieldStrength,
			MetricGainMultiplier = metricGainMultiplier,
			MetricGainActive = metricGainActive
		};
	}

	private void CaptureViewportAndQuit(GrinFilmCamera.FixtureDebugStatsSnapshot snapshot, int renderHealthStep, int processedRows)
	{
		Viewport viewport = GetViewport();
		Image image = viewport?.GetTexture()?.GetImage();
		if (image == null)
		{
			FailCaptureAndQuit($"[GrinBasicVisual][Capture][FAIL] fixture={FixtureHudName} reason=missing_viewport_image");
			return;
		}

		string capturePath = ResolveCapturePath(_capturePath);
		try
		{
			string directory = Path.GetDirectoryName(capturePath);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}
		}
		catch (Exception ex)
		{
			FailCaptureAndQuit(
				$"[GrinBasicVisual][Capture][FAIL] fixture={FixtureHudName} reason=create_directory " +
				$"path={capturePath} exception={ex.GetType().Name}");
			return;
		}

		Error saveError = image.SavePng(capturePath);
		if (saveError != Error.Ok)
		{
			FailCaptureAndQuit(
				$"[GrinBasicVisual][Capture][FAIL] fixture={FixtureHudName} reason=save_png " +
				$"path={capturePath} error={saveError}");
			return;
		}

		if (_field != null &&
			_field.TransportModel == RendererCore.Config.TransportModel.Metric_NullGeodesic &&
			_rayBeamRenderer != null &&
			GodotObject.IsInstanceValid(_rayBeamRenderer))
		{
			RayBeamRenderer.MetricTransportDiagnosticsSnapshot metricDiag = _rayBeamRenderer.GetMetricTransportDiagnosticsSnapshot();
			GD.Print(
				$"[GrinBasicVisual][MetricDiag] fixture={FixtureHudName} law={metricDiag.MetricSteeringLawToken} " +
				$"directSteps={metricDiag.MetricDirectSteps} gridBypassSteps={metricDiag.GridBypassSteps} " +
				$"fallbackSteps={metricDiag.GrinFallbackSteps} scalarDominatedSteps={metricDiag.GrinScalarDominatedSteps} " +
				$"deltaZero={metricDiag.MetricDeltaZeroCount} deltaNonzero={metricDiag.MetricDeltaNonzeroCount} " +
				$"metricFallbackCount={metricDiag.MetricFallbackCount} contributionRatio={metricDiag.MetricContributionRatio:0.######} " +
				$"steeringTurns={metricDiag.SteeringTurnCount} meanTurn={metricDiag.MeanTurn:0.######} maxTurn={metricDiag.MaxTurn:0.######} " +
				$"zeroReasons={metricDiag.ZeroReasonSummary} radialSummary={metricDiag.RadialTurnSummary}");
		}

		_captureComplete = true;
		SetProcess(false);
		GD.Print(
			$"[GrinBasicVisual][Capture] fixture={FixtureHudName} path={capturePath} " +
			$"tracedPixels={snapshot.TracedPixels} sourceHits={snapshot.SourceHits} backgroundHits={snapshot.BackgroundHits} " +
			$"absorbedHits={snapshot.AbsorbedHits} missHits={snapshot.MissHits} readyFrames={_captureReadyFrames} " +
			$"rhStep={(renderHealthStep >= 0 ? renderHealthStep.ToString(CultureInfo.InvariantCulture) : "na")} " +
			$"processedRows={processedRows.ToString(CultureInfo.InvariantCulture)}");
		if (_exitAfterCapture)
		{
			RequestQuit(0);
		}
	}

	private void FailCaptureAndQuit(string message)
	{
		_captureComplete = true;
		SetProcess(false);
		GD.PrintErr(
			$"{message} maxRhStep={(_captureMaxObservedRenderHealthStep >= 0 ? _captureMaxObservedRenderHealthStep.ToString(CultureInfo.InvariantCulture) : "na")} " +
			$"maxProcessedRows={_captureMaxObservedProcessedRows.ToString(CultureInfo.InvariantCulture)} " +
			$"observedTracedPixels={(_captureObservedAnyTracedPixels ? 1 : 0)}");
		RequestQuit(2);
	}

	private static bool HasOverrideValue(float value)
	{
		return float.IsFinite(value) && value >= 0.0f;
	}

	private static bool HasCaptureOpacityOverride(float value)
	{
		return float.IsFinite(value) && value >= 0.0f;
	}

	private RayBeamRenderer ResolveRayBeamRenderer()
	{
		Node fixtureRoot = _field?.GetParent();
		if (fixtureRoot != null && GodotObject.IsInstanceValid(fixtureRoot))
		{
			RayBeamRenderer fixtureRenderer = fixtureRoot.GetNodeOrNull<RayBeamRenderer>("RayBeamRenderer");
			if (fixtureRenderer != null)
			{
				return fixtureRenderer;
			}
		}

		return GetNodeOrNull<RayBeamRenderer>("RayBeamRenderer");
	}

	private static bool TryParseFloatArgValue(string arg, string prefix, out float value)
	{
		value = 0f;
		if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string raw = arg.Substring(prefix.Length).Trim();
		return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	}

	private static bool TryParseIntArgValue(string arg, string prefix, out int value)
	{
		value = 0;
		if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string raw = arg.Substring(prefix.Length).Trim();
		return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
	}

	private static bool TryParseBoolArgValue(string arg, string prefix, out bool value)
	{
		value = false;
		if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string raw = arg.Substring(prefix.Length).Trim();
		if (string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}

		if (string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase))
		{
			value = true;
			return true;
		}

		if (string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase))
		{
			value = false;
			return true;
		}

		return false;
	}

	private void ApplyComparisonOverlayOptions(CmdlineOptions options)
	{
		if (_filmOverlay == null || !GodotObject.IsInstanceValid(_filmOverlay))
			return;

		_filmOverlay.ShowComparisonGrid = options.CompareGridEnabled ?? ComparisonGridEnabled;
		_filmOverlay.ShowComparisonCrosshair = options.CompareCrosshairEnabled ?? ComparisonCrosshairEnabled;
		_filmOverlay.QueueRedraw();
	}

	private static CmdlineOptions ParseCmdlineOptions(string[] args)
	{
		CmdlineOptions options = default;
		if (args == null)
		{
			return options;
		}

		foreach (string arg in args)
		{
			if (TryParseFloatArgValue(arg, ROuterArgPrefix, out float rOuter))
			{
				options.ROuter = rOuter;
				continue;
			}
			if (TryParseFloatArgValue(arg, AmpArgPrefix, out float amp))
			{
				options.Amp = amp;
				continue;
			}
			if (TryParseFloatArgValue(arg, GammaArgPrefix, out float gamma))
			{
				options.Gamma = gamma;
				continue;
			}
			if (TryParseFloatArgValue(arg, StepScaleArgPrefix, out float stepScale))
			{
				options.StepScale = stepScale;
				continue;
			}
			if (TryParseFloatArgValue(arg, StepLengthArgPrefix, out float stepLength))
			{
				options.StepLength = stepLength;
				continue;
			}
			if (TryParseFloatArgValue(arg, MinStepLengthArgPrefix, out float minStepLength))
			{
				options.MinStepLength = minStepLength;
				continue;
			}
			if (TryParseFloatArgValue(arg, MaxStepLengthArgPrefix, out float maxStepLength))
			{
				options.MaxStepLength = maxStepLength;
				continue;
			}
			if (TryParseIntArgValue(arg, StepsPerRayArgPrefix, out int stepsPerRay))
			{
				options.StepsPerRay = stepsPerRay;
				continue;
			}
			if (TryParseFloatArgValue(arg, TurnThresholdArgPrefix, out float turnThresholdDegrees))
			{
				options.TurnThresholdDegrees = turnThresholdDegrees;
				continue;
			}
			if (TryParseFloatArgValue(arg, MetricGainArgPrefix, out float metricGain))
			{
				options.MetricGain = metricGain;
				continue;
			}
			if (TryParseFloatArgValue(arg, BendScaleArgPrefix, out float bendScale))
			{
				options.BendScale = bendScale;
				continue;
			}
			if (TryParseFloatArgValue(arg, FieldStrengthArgPrefix, out float fieldStrength))
			{
				options.FieldStrength = fieldStrength;
				continue;
			}
			if (TryParseIntArgValue(arg, SettleFramesArgPrefix, out int settleFrames))
			{
				options.SettleFrames = settleFrames;
				continue;
			}
			if (TryParseIntArgValue(arg, MinRenderHealthStepArgPrefix, out int minRenderHealthStep))
			{
				options.MinRenderHealthStep = minRenderHealthStep;
				continue;
			}
			if (TryParseIntArgValue(arg, MinProcessedRowsArgPrefix, out int minProcessedRows))
			{
				options.MinProcessedRows = minProcessedRows;
				continue;
			}
			if (TryParseFloatArgValue(arg, CaptureFilmOpacityArgPrefix, out float captureFilmOpacity))
			{
				options.CaptureFilmOpacity = captureFilmOpacity;
				continue;
			}
			if (TryParseIntArgValue(arg, ExitAfterCaptureArgPrefix, out int exitAfterCapture))
			{
				options.ExitAfterCapture = exitAfterCapture > 0;
				continue;
			}
			if (TryParseBoolArgValue(arg, CompareGridArgPrefix, out bool compareGrid))
			{
				options.CompareGridEnabled = compareGrid;
				continue;
			}
			if (TryParseBoolArgValue(arg, CompareCrosshairArgPrefix, out bool compareCrosshair))
			{
				options.CompareCrosshairEnabled = compareCrosshair;
				continue;
			}
			if (arg.StartsWith(CaptureArgPrefix, StringComparison.OrdinalIgnoreCase))
			{
				options.CapturePath = arg.Substring(CaptureArgPrefix.Length).Trim();
			}
		}

		return options;
	}

	private static string[] GetCmdArgsForParsing()
	{
		string[] userArgs = OS.GetCmdlineUserArgs();
		string[] args = OS.GetCmdlineArgs();
		if ((userArgs == null || userArgs.Length == 0) && (args == null || args.Length == 0))
		{
			return Array.Empty<string>();
		}
		if (userArgs == null || userArgs.Length == 0)
		{
			return args ?? Array.Empty<string>();
		}
		if (args == null || args.Length == 0)
		{
			return userArgs;
		}

		HashSet<string> merged = new HashSet<string>(StringComparer.Ordinal);
		List<string> ordered = new List<string>(userArgs.Length + args.Length);
		foreach (string raw in userArgs)
		{
			if (string.IsNullOrWhiteSpace(raw))
			{
				continue;
			}

			string token = raw.Trim();
			if (merged.Add(token))
			{
				ordered.Add(token);
			}
		}
		foreach (string raw in args)
		{
			if (string.IsNullOrWhiteSpace(raw))
			{
				continue;
			}

			string token = raw.Trim();
			if (merged.Add(token))
			{
				ordered.Add(token);
			}
		}

		return ordered.ToArray();
	}

	private static string ResolveCapturePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}

		if (Path.IsPathRooted(path))
		{
			return Path.GetFullPath(path);
		}

		string projectRoot = ProjectSettings.GlobalizePath("res://");
		return Path.GetFullPath(Path.Combine(projectRoot, path));
	}

	private void RequestQuit(int exitCode)
	{
		if (_quitRequested)
		{
			return;
		}

		_quitRequested = true;
		_pendingQuitCode = exitCode;
		if (_filmCamera != null && GodotObject.IsInstanceValid(_filmCamera))
		{
			_filmCamera.UpdateEveryFrame = false;
		}
		CallDeferred(nameof(QuitSequenceDeferred));
	}

	private async void QuitSequenceDeferred()
	{
		SceneTree tree = GetTree();
		if (tree == null)
		{
			return;
		}

		for (int i = 0; i < 6; i++)
		{
			await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}
		for (int i = 0; i < 2; i++)
		{
			await ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
		}
		tree.Quit(_pendingQuitCode);
	}

	private void QuitLauncherMismatchDeferred()
	{
		GD.PrintErr("[GrinBasicVisual][FAIL] Requesting quit code=1 due to launcher/scene mismatch.");
		GetTree()?.Quit(1);
	}

	private struct CmdlineOptions
	{
		public float? ROuter;
		public float? Amp;
		public float? Gamma;
		public float? StepScale;
		public float? StepLength;
		public float? MinStepLength;
		public float? MaxStepLength;
		public int? StepsPerRay;
		public float? TurnThresholdDegrees;
		public float? MetricGain;
		public float? BendScale;
		public float? FieldStrength;
		public int? SettleFrames;
		public int? MinRenderHealthStep;
		public int? MinProcessedRows;
		public float? CaptureFilmOpacity;
		public bool? CompareGridEnabled;
		public bool? CompareCrosshairEnabled;
		public string CapturePath;
		public bool ExitAfterCapture;
	}

	private struct RendererConfigSnapshot
	{
		public int StepsPerRay;
		public float StepLength;
		public float MinStepLength;
		public float MaxStepLength;
		public float TurnThresholdDegrees;
		public float BendScale;
		public float FieldStrength;
		public float MetricGainMultiplier;
		public bool MetricGainActive;
	}
}
