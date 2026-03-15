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
	private const string CaptureArgPrefix = "--grin-basic-capture=";
	private const string SettleFramesArgPrefix = "--grin-basic-settle-frames=";
	private const string MinRenderHealthStepArgPrefix = "--grin-basic-min-rh-step=";
	private const string MinProcessedRowsArgPrefix = "--grin-basic-min-processed-rows=";
	private const string CaptureFilmOpacityArgPrefix = "--grin-basic-capture-film-opacity=";
	private const string ExitAfterCaptureArgPrefix = "--grin-basic-exit-after-capture=";

	[Export] public NodePath FilmCameraPath = new("GrinFilmCamera");
	[Export] public NodePath FieldPath = new("FixtureGrinBasicVisual/FieldSource3D");
	[Export] public string FixtureHudName = "grin_basic_visual";
	[Export] public string SourcePatternMode = "dot_grid";
	[Export] public float DefaultROuterOverride = -1.0f;
	[Export] public float DefaultAmpOverride = -1.0f;
	[Export] public float DefaultGammaOverride = -1.0f;
	[Export(PropertyHint.Range, "0,8,1")] public int StartupPhysicsFramesDelay = 2;
	[Export(PropertyHint.Range, "1,240,1")] public int DefaultCaptureSettleFrames = 12;
	[Export(PropertyHint.Range, "1,2048,1")] public int DefaultCaptureTimeoutFrames = 240;
	[Export(PropertyHint.Range, "0,240,1")] public int DefaultCaptureMinRenderHealthStep = 0;
	[Export(PropertyHint.Range, "0,4096,1")] public int DefaultCaptureMinProcessedRows = 0;
	[Export(PropertyHint.Range, "-1,1,0.01")] public float DefaultCaptureFilmOpacityOverride = 1.0f;

	private GrinFilmCamera _filmCamera;
	private FieldSource3D _field;
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
	private int _pendingQuitCode;
	private float _captureFilmOpacityOverride = -1.0f;
	private string _capturePath = string.Empty;

	public override void _Ready()
	{
		_filmCamera = GetNodeOrNull<GrinFilmCamera>(FilmCameraPath);
		_field = GetNodeOrNull<FieldSource3D>(FieldPath);
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

		if (_field != null)
		{
			ApplyFieldOverrides(_field, options);
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

		int renderHealthStep = 0;
		bool hasRenderHealthStep = _filmCamera.TryGetLatestRenderHealthStepForTesting(out renderHealthStep);
		int processedRows = Math.Max(0, _filmCamera.FilmRowCursor);
		if ((_captureMinRenderHealthStep > 0 && (!hasRenderHealthStep || renderHealthStep < _captureMinRenderHealthStep)) ||
			(_captureMinProcessedRows > 0 && processedRows < _captureMinProcessedRows))
		{
			_captureReadyFrames = 0;
			return;
		}

		_captureReadyFrames++;
		if (_captureReadyFrames < _captureSettleFrames)
		{
			return;
		}

		CaptureViewportAndQuit(snapshot.TracedPixels, hasRenderHealthStep ? renderHealthStep : -1, processedRows);
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

	private void CaptureViewportAndQuit(long tracedPixels, int renderHealthStep, int processedRows)
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

		_captureComplete = true;
		SetProcess(false);
		GD.Print(
			$"[GrinBasicVisual][Capture] fixture={FixtureHudName} path={capturePath} " +
			$"tracedPixels={tracedPixels} readyFrames={_captureReadyFrames} " +
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
		GD.PrintErr(message);
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
		public int? SettleFrames;
		public int? MinRenderHealthStep;
		public int? MinProcessedRows;
		public float? CaptureFilmOpacity;
		public string CapturePath;
		public bool ExitAfterCapture;
	}
}
