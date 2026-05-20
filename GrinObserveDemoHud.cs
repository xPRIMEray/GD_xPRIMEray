using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public partial class GrinObserveDemoHud : Control
{
	[Export] public NodePath OverlayPath = new("../FilmOverlay2D");
	[Export] public NodePath FilmViewPath = new("../FilmView");
	[Export] public NodePath FieldPath = new("../../FixtureGrinBasicVisual/FieldSource3D");
	[Export] public NodePath CameraPath = new("../../FixtureGrinBasicVisual/Camera3D");
	[Export] public string DemoVersion = "xPRIMEray v0.0-pre";
	[Export] public string ModeLabel = "Curved GRIN Transport";
	[Export] public string FixtureLabel = "GRIN Basic Visual Off-Axis Observe";
	[Export] public string PairedScenePath = "res://test-straight-basic-visual-offaxis-observe.tscn";
	[Export] public string ComparisonLabel = "F2 switches matched scene: Straight Control vs Curved Transport";
	[Export] public string DiagnosticsHint = "--grin-basic-capture=output/v0.0-pre/grin_observe.png --grin-basic-exit-after-capture=1";
	[Export] public bool ShowHotkeyHints = true;
	[Export] public bool EnableHotkeys = true;
	[Export] public string EvidenceOutputDir = "res://output/v0.0-pre";

	private FilmOverlay2D _overlay;
	private FieldSource3D _field;
	private Camera3D _camera;
	private Font _font;
	private Transform3D _canonicalCameraTransform;
	private bool _hasCanonicalCameraTransform;
	private bool _showHelp = true;
	private bool _comparisonView;
	private bool _cameraFrozen;
	private bool _cleanPresentation;
	private bool _lastScreenshotOk;
	private bool _lastDiagnosticsOk;
	private string _lastEvidencePath = string.Empty;
	private string _observatoryModeName = string.Empty;

	public bool HelpVisible => _showHelp;
	public bool ComparisonViewEnabled => _comparisonView;
	public bool CameraFrozen => _cameraFrozen;
	public bool CleanPresentationEnabled => _cleanPresentation;
	public bool LastScreenshotOk => _lastScreenshotOk;
	public bool LastDiagnosticsOk => _lastDiagnosticsOk;
	public string LastEvidencePath => _lastEvidencePath;

	/// <summary>
	/// Set by ObservatoryModeController to annotate the active mode in the HUD.
	/// Empty string (default) leaves the HUD unchanged — v0.0-pre behavior is preserved.
	/// </summary>
	public string ObservatoryModeName
	{
		get => _observatoryModeName;
		set => _observatoryModeName = value ?? string.Empty;
	}

	public override void _Ready()
	{
		if (IsCommandLineCaptureRun())
		{
			EnableHotkeys = false;
		}
		_overlay = GetNodeOrNull<FilmOverlay2D>(OverlayPath);
		_field = GetNodeOrNull<FieldSource3D>(FieldPath);
		_camera = GetNodeOrNull<Camera3D>(CameraPath);
		if (_camera != null && GodotObject.IsInstanceValid(_camera))
		{
			_canonicalCameraTransform = _camera.GlobalTransform;
			_hasCanonicalCameraTransform = true;
		}
		_font = GetThemeDefaultFont();
		MouseFilter = MouseFilterEnum.Ignore;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (_cameraFrozen && _camera != null && GodotObject.IsInstanceValid(_camera) && _hasCanonicalCameraTransform)
		{
			_camera.GlobalTransform = _canonicalCameraTransform;
		}
		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!EnableHotkeys || _overlay == null || !GodotObject.IsInstanceValid(_overlay))
		{
			return;
		}

		if (inputEvent is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
		{
			return;
		}

		if (TryRunControl(keyEvent.Keycode, allowSceneSwitch: true, out _, out _))
		{
			MarkInputHandled();
		}
	}

	public bool TryRunControlForVerification(Key key, out string status, out string detail)
	{
		return TryRunControl(key, allowSceneSwitch: false, out status, out detail);
	}

	public FilmOverlay2D.OverlayRenderSnapshot GetOverlaySnapshotForVerification()
	{
		return _overlay != null && GodotObject.IsInstanceValid(_overlay)
			? _overlay.GetOverlayRenderSnapshot()
			: default;
	}

	public string BuildKeymapMarkdownRows()
	{
		return "| Key | v0.0-pre control |\n" +
			"| --- | --- |\n" +
			"| F1 | Help / Control overlay |\n" +
			"| F2 | Toggle Straight Control vs Curved Transport by matched scene switch |\n" +
			"| F3 | Toggle Film Rays |\n" +
			"| F4 | Toggle Hit Normals |\n" +
			"| F5 | Toggle Grid / Reference Crosshair |\n" +
			"| F6 | Toggle Difference / Comparison View |\n" +
			"| F7 | Freeze / Unfreeze Camera |\n" +
			"| F8 | Reset Camera to Canonical Pose |\n" +
			"| F9 | Capture Screenshot / Still Packet |\n" +
			"| F10 | Export Diagnostics |\n" +
			"| F11 | Toggle Clean Presentation Mode |\n" +
			"| F12 | Toggle Crosshair / Minimal Reticle |";
	}

	public override void _Draw()
	{
		if (_font == null)
		{
			_font = GetThemeDefaultFont();
		}
		if (_font == null)
		{
			return;
		}

		Vector2 size = GetRect().Size;
		float scale = Mathf.Clamp(Mathf.Min(size.X / 1280f, size.Y / 720f), 0.85f, 1.25f);
		int titleSize = Mathf.RoundToInt(18f * scale);
		int bodySize = Mathf.RoundToInt(13f * scale);
		float pad = 14f * scale;
		float line = 18f * scale;

		if (_cleanPresentation)
		{
			DrawCleanModePill(new Vector2(pad, pad), titleSize, bodySize);
			return;
		}

		string overlayState = BuildOverlayState();
		string fieldState = BuildFieldState();
		string cockpitState = BuildCockpitState();
		string hotkeys = ShowHotkeyHints
			? "F1 help  F2 mode  F3 rays  F4 normals  F5 grid  F6 compare  F7 freeze  F8 reset  F9 still  F10 diagnostics  F11 clean  F12 reticle"
			: string.Empty;

		string modeLineText = string.IsNullOrEmpty(_observatoryModeName)
			? ModeLabel
			: $"{ModeLabel}  ·  {_observatoryModeName}";
		string[] topLines =
		{
			DemoVersion,
			modeLineText,
			FixtureLabel,
			overlayState,
			fieldState,
			cockpitState
		};
		DrawPanel(new Vector2(pad, pad), topLines, titleSize, bodySize, line, highlightMode: true);

		List<string> bottomLines = new()
		{
			ComparisonLabel,
			$"Diagnostics/export: {DiagnosticsHint}"
		};
		if (!string.IsNullOrWhiteSpace(_lastEvidencePath))
		{
			bottomLines.Add($"Last evidence: {_lastEvidencePath}");
		}
		if (_showHelp && !string.IsNullOrWhiteSpace(hotkeys))
		{
			bottomLines.Add(hotkeys);
		}
		float bottomHeight = (bottomLines.Count * line) + (pad * 1.6f);
		DrawPanel(new Vector2(pad, Mathf.Max(pad, size.Y - bottomHeight - pad)), bottomLines.ToArray(), titleSize, bodySize, line, highlightMode: false);
	}

	private void DrawPanel(Vector2 origin, string[] lines, int titleSize, int bodySize, float lineHeight, bool highlightMode)
	{
		float padX = 13f;
		float padY = 10f;
		float maxWidth = 0f;
		for (int i = 0; i < lines.Length; i++)
		{
			int fontSize = i == 0 && highlightMode ? titleSize : bodySize;
			maxWidth = Math.Max(maxWidth, _font.GetStringSize(lines[i] ?? string.Empty, HorizontalAlignment.Left, -1, fontSize).X);
		}

		float width = Mathf.Min(GetRect().Size.X - (origin.X * 2f), maxWidth + (padX * 2f));
		float height = (lines.Length * lineHeight) + (padY * 1.7f);
		Rect2 rect = new Rect2(origin, new Vector2(width, height));
		DrawRect(rect, new Color(0.025f, 0.035f, 0.05f, 0.78f), filled: true);
		DrawRect(rect, highlightMode ? new Color(0.35f, 0.82f, 1.0f, 0.52f) : new Color(1f, 1f, 1f, 0.22f), filled: false, width: 1.5f);

		for (int i = 0; i < lines.Length; i++)
		{
			int fontSize = i == 0 && highlightMode ? titleSize : bodySize;
			Color color = i == 1 && highlightMode ? new Color(1f, 0.9f, 0.42f, 1f) : new Color(0.92f, 0.97f, 1f, 0.95f);
			Vector2 pos = origin + new Vector2(padX, padY + ((i + 1) * lineHeight) - 4f);
			DrawString(_font, pos, lines[i] ?? string.Empty, HorizontalAlignment.Left, width - (padX * 2f), fontSize, color);
		}
	}

	private string BuildOverlayState()
	{
		if (_overlay == null || !GodotObject.IsInstanceValid(_overlay))
		{
			return "FilmOverlay2D: unavailable";
		}

		FilmOverlay2D.OverlayRenderSnapshot snapshot = _overlay.GetOverlayRenderSnapshot();
		string traversal = snapshot.TraversalOverlayEnabled || snapshot.TraversalMinimapEnabled
			? $" traversal={OnOff(snapshot.TraversalOverlayEnabled)} mini={OnOff(snapshot.TraversalMinimapEnabled)} mode={snapshot.TraversalMode} rows={snapshot.TraversalRowsCompleted}"
			: string.Empty;
		return $"FilmOverlay2D: rays={OnOff(snapshot.DrawRaysEnabled)} normals={OnOff(snapshot.DrawHitNormalsEnabled)} grid={OnOff(snapshot.ComparisonGridEnabled)} reticle={OnOff(snapshot.ComparisonCrosshairEnabled)} rays_sampled={snapshot.RayCount}{traversal}";
	}

	private string BuildFieldState()
	{
		if (_field == null || !GodotObject.IsInstanceValid(_field))
		{
			return "Field: unavailable";
		}

		FieldSource3D.ResolvedFieldParams resolved = _field.ResolveEffectiveParams(out _);
		return $"Field: transport={_field.TransportModel} rOuter={resolved.rOuter:0.##} amp={resolved.amp:0.##} gamma={resolved.a:0.##}";
	}

	private string BuildCockpitState()
	{
		return $"Cockpit: comparison={OnOff(_comparisonView)} cameraFrozen={OnOff(_cameraFrozen)} clean={OnOff(_cleanPresentation)} still={OkFail(_lastScreenshotOk)} diagnostics={OkFail(_lastDiagnosticsOk)}";
	}

	private void DrawCleanModePill(Vector2 origin, int titleSize, int bodySize)
	{
		string[] lines = { DemoVersion, ModeLabel, "F11 restores cockpit" };
		DrawPanel(origin, lines, titleSize, bodySize, 18f, highlightMode: true);
	}

	private void SwitchToPairedScene()
	{
		if (string.IsNullOrWhiteSpace(PairedScenePath))
		{
			_lastEvidencePath = "F2 unavailable: paired scene path is empty";
			return;
		}

		Error error = GetTree().ChangeSceneToFile(PairedScenePath);
		if (error != Error.Ok)
		{
			_lastEvidencePath = $"F2 scene switch failed: {error}";
		}
	}

	private bool TryRunControl(Key key, bool allowSceneSwitch, out string status, out string detail)
	{
		status = "PASS";
		detail = string.Empty;
		if (_overlay == null || !GodotObject.IsInstanceValid(_overlay))
		{
			status = "FAIL";
			detail = "FilmOverlay2D is unavailable; cockpit controls cannot be verified.";
			return false;
		}

		switch (key)
		{
			case Key.F1:
				_showHelp = !_showHelp;
				detail = $"help overlay visible={OnOff(_showHelp)}";
				return true;
			case Key.F2:
				if (string.IsNullOrWhiteSpace(PairedScenePath))
				{
					status = "FAIL";
					detail = "paired scene path is empty";
					return false;
				}
				if (!ResourceLoader.Exists(PairedScenePath))
				{
					status = "FAIL";
					detail = $"paired scene is not reachable: {PairedScenePath}";
					return false;
				}
				detail = allowSceneSwitch
					? $"switching to paired scene: {PairedScenePath}"
					: $"paired scene reachable: {PairedScenePath}; verification does not switch scenes";
				if (allowSceneSwitch)
				{
					SwitchToPairedScene();
				}
				return true;
			case Key.F3:
				_overlay.DrawRays = !_overlay.DrawRays;
				_overlay.QueueRedraw();
				detail = $"film rays={OnOff(_overlay.DrawRays)}";
				return true;
			case Key.F4:
				_overlay.DrawHitNormals = !_overlay.DrawHitNormals;
				_overlay.QueueRedraw();
				detail = $"hit normals={OnOff(_overlay.DrawHitNormals)}";
				return true;
			case Key.F5:
				_overlay.ShowComparisonGrid = !_overlay.ShowComparisonGrid;
				_overlay.ShowComparisonCrosshair = _overlay.ShowComparisonGrid;
				_overlay.QueueRedraw();
				detail = $"grid={OnOff(_overlay.ShowComparisonGrid)} reticle={OnOff(_overlay.ShowComparisonCrosshair)}";
				return true;
			case Key.F6:
				_comparisonView = !_comparisonView;
				_overlay.ShowComparisonGrid = _comparisonView;
				_overlay.ShowComparisonCrosshair = _comparisonView;
				_overlay.QueueRedraw();
				detail = $"comparison view={OnOff(_comparisonView)}";
				return true;
			case Key.F7:
				ToggleCameraFreeze();
				detail = $"camera frozen={OnOff(_cameraFrozen)}";
				return true;
			case Key.F8:
				ResetCameraToCanonicalPose();
				if (!_hasCanonicalCameraTransform)
				{
					status = "FAIL";
					detail = "canonical camera pose is unavailable";
					return false;
				}
				detail = "camera reset to canonical pose";
				return true;
			case Key.F9:
				CaptureScreenshotPacket();
				status = _lastScreenshotOk ? "PASS" : "FAIL";
				detail = _lastEvidencePath;
				return _lastScreenshotOk;
			case Key.F10:
				ExportDiagnosticsPacket();
				status = _lastDiagnosticsOk ? "PASS" : "FAIL";
				detail = _lastEvidencePath;
				return _lastDiagnosticsOk;
			case Key.F11:
				_cleanPresentation = !_cleanPresentation;
				detail = $"clean presentation={OnOff(_cleanPresentation)}";
				return true;
			case Key.F12:
				_overlay.ShowComparisonCrosshair = !_overlay.ShowComparisonCrosshair;
				_overlay.QueueRedraw();
				detail = $"minimal reticle={OnOff(_overlay.ShowComparisonCrosshair)}";
				return true;
			default:
				status = "NOT IMPLEMENTED";
				detail = $"no v0.0-pre cockpit control is assigned to {key}";
				return false;
		}
	}

	private void MarkInputHandled()
	{
		GetViewport()?.SetInputAsHandled();
	}

	private static bool IsCommandLineCaptureRun()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if ((arg ?? string.Empty).StartsWith("--grin-basic-capture=", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private void ToggleCameraFreeze()
	{
		_cameraFrozen = !_cameraFrozen;
		if (_camera != null && GodotObject.IsInstanceValid(_camera))
		{
			_canonicalCameraTransform = _camera.GlobalTransform;
			_hasCanonicalCameraTransform = true;
		}
	}

	private void ResetCameraToCanonicalPose()
	{
		if (_camera == null || !GodotObject.IsInstanceValid(_camera) || !_hasCanonicalCameraTransform)
		{
			_lastEvidencePath = "F8 reset unavailable: no canonical camera pose";
			return;
		}

		_camera.GlobalTransform = _canonicalCameraTransform;
	}

	public void CaptureScreenshotPacket()
	{
		_lastScreenshotOk = false;
		Image image = TryGetEvidenceImage();
		if (image == null)
		{
			_lastEvidencePath = "F9 screenshot failed: missing viewport or film image";
			return;
		}

		string path = BuildEvidencePath("still", "png");
		EnsureParentDirectory(path);
		Error error = image.SavePng(path);
		image.Dispose();
		_lastScreenshotOk = error == Error.Ok;
		_lastEvidencePath = _lastScreenshotOk ? ProjectRelativePath(path) : $"F9 screenshot failed: {error}";
	}

	private Image TryGetEvidenceImage()
	{
		TextureRect filmView = GetNodeOrNull<TextureRect>(FilmViewPath);
		Image filmImage = filmView?.Texture?.GetImage();
		if (filmImage != null)
		{
			return filmImage;
		}

		Viewport viewport = GetViewport();
		return viewport?.GetTexture()?.GetImage();
	}

	public void ExportDiagnosticsPacket()
	{
		_lastDiagnosticsOk = false;
		string path = BuildEvidencePath("diagnostics", "json");
		EnsureParentDirectory(path);
		string json = BuildDiagnosticsJson();
		try
		{
			File.WriteAllText(path, json);
			_lastDiagnosticsOk = true;
			_lastEvidencePath = ProjectRelativePath(path);
		}
		catch (Exception ex)
		{
			_lastEvidencePath = $"F10 diagnostics failed: {ex.GetType().Name}";
		}
	}

	private string BuildDiagnosticsJson()
	{
		string escapedMode = JsonEscape(ModeLabel);
		string escapedScene = JsonEscape(GetTree().CurrentScene?.SceneFilePath ?? string.Empty);
		string escapedOverlay = JsonEscape(BuildOverlayState());
		string escapedField = JsonEscape(BuildFieldState());
		string escapedCockpit = JsonEscape(BuildCockpitState());
		string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
		return "{\n" +
			$"  \"schema\": \"xprimeray.grin_observe_demo_diagnostics.v1\",\n" +
			$"  \"timestamp_utc\": \"{timestamp}\",\n" +
			$"  \"version\": \"{JsonEscape(DemoVersion)}\",\n" +
			$"  \"mode\": \"{escapedMode}\",\n" +
			$"  \"scene\": \"{escapedScene}\",\n" +
			$"  \"fixture\": \"{JsonEscape(FixtureLabel)}\",\n" +
			$"  \"overlay_state\": \"{escapedOverlay}\",\n" +
			$"  \"field_state\": \"{escapedField}\",\n" +
			$"  \"cockpit_state\": \"{escapedCockpit}\",\n" +
			$"  \"object_state_locked\": true\n" +
			"}\n";
	}

	private string BuildEvidencePath(string kind, string extension)
	{
		string modeToken = ModeLabel.ToLowerInvariant()
			.Replace(" ", "_", StringComparison.Ordinal)
			.Replace("/", "_", StringComparison.Ordinal);
		string fileName = $"grin_observe_{modeToken}_{kind}.{extension}";
		string root = string.IsNullOrWhiteSpace(EvidenceOutputDir) ? "res://output/v0.0-pre" : EvidenceOutputDir;
		return ProjectSettings.GlobalizePath(root.TrimEnd('/', '\\') + "/" + fileName);
	}

	private static void EnsureParentDirectory(string path)
	{
		string directory = Path.GetDirectoryName(path) ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}
	}

	private static string ProjectRelativePath(string path)
	{
		string projectRoot = ProjectSettings.GlobalizePath("res://").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		if (path.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
		{
			return path.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
		}
		return path.Replace('\\', '/');
	}

	private static string OnOff(bool value) => value ? "on" : "off";

	private static string OkFail(bool value) => value ? "ok" : "pending";

	private static string JsonEscape(string value)
	{
		return (value ?? string.Empty)
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal)
			.Replace("\r", "\\r", StringComparison.Ordinal);
	}
}
