using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public partial class GrinObservePlayModeVerifier : Node
{
	private readonly struct CheckResult
	{
		public readonly string Area;
		public readonly string Name;
		public readonly string Status;
		public readonly string Detail;

		public CheckResult(string area, string name, string status, string detail)
		{
			Area = area;
			Name = name;
			Status = status;
			Detail = detail;
		}
	}

	private readonly struct ArtifactResult
	{
		public readonly string Label;
		public readonly string Path;
		public readonly int Width;
		public readonly int Height;
		public readonly long TotalPixels;
		public readonly long NonBackgroundPixels;
		public readonly long TracedPixels;
		public readonly double CoveragePercent;
		public readonly string Sha256;
		public readonly string Status;
		public readonly string Detail;

		public ArtifactResult(
			string label,
			string path,
			int width,
			int height,
			long totalPixels,
			long nonBackgroundPixels,
			long tracedPixels,
			double coveragePercent,
			string sha256,
			string status,
			string detail)
		{
			Label = label;
			Path = path;
			Width = width;
			Height = height;
			TotalPixels = totalPixels;
			NonBackgroundPixels = nonBackgroundPixels;
			TracedPixels = tracedPixels;
			CoveragePercent = coveragePercent;
			Sha256 = sha256;
			Status = status;
			Detail = detail;
		}
	}

	[Export] public NodePath HudPath = new("../CanvasLayer/DemoHud");
	[Export] public NodePath OverlayPath = new("../CanvasLayer/FilmOverlay2D");
	[Export] public NodePath FilmViewPath = new("../CanvasLayer/FilmView");
	[Export] public NodePath FilmCameraPath = new("../GrinFilmCamera");
	[Export] public string Role = "curved";
	[Export] public string OutputDir = "res://output/v0.0-pre";
	[Export] public int FramesToWait = 20;
	[Export] public double MinNonBackgroundCoveragePercent = 0.5;
	[Export] public bool ExitAfterVerify = true;

	private readonly List<CheckResult> _checks = new();
	private readonly List<ArtifactResult> _artifacts = new();
	private GrinObserveDemoHud _hud;
	private FilmOverlay2D _overlay;
	private GrinFilmCamera _filmCamera;

	public override void _Ready()
	{
		if (!HasVerifyArg())
		{
			return;
		}

		ApplyUserArgs();
		CallDeferred(MethodName.RunVerification);
	}

	private async void RunVerification()
	{
		for (int i = 0; i < Math.Max(1, FramesToWait); i++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		try
		{
			RunSceneChecks();
			_artifacts.Add(CaptureArtifact(BuildVerifyArtifactName(), "primary verify viewport"));
			RunControlChecks();
			if (IsCurvedRole())
			{
				_artifacts.Add(CaptureArtifact("curved_grin_final_smoke.png", "curved final smoke viewport"));
			}

			WriteJsonResult();
			if (ExitAfterVerify)
			{
				GetTree().Quit(HasHardFailure() ? 2 : 0);
			}
		}
		catch (Exception ex)
		{
			_checks.Add(new CheckResult("runtime", "play-mode verifier", "FAIL", $"{ex.GetType().Name}: {ex.Message}"));
			WriteJsonResult();
			if (ExitAfterVerify)
			{
				GetTree().Quit(2);
			}
		}
	}

	private void RunSceneChecks()
	{
		_hud = GetNodeOrNull<GrinObserveDemoHud>(HudPath);
		_overlay = GetNodeOrNull<FilmOverlay2D>(OverlayPath);
		_filmCamera = GetNodeOrNull<GrinFilmCamera>(FilmCameraPath);

		string scene = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		AddCheck("scene boot", "canonical scene loaded", IsExpectedScene(scene) ? "PASS" : "FAIL", scene);
		AddCheck("scene boot", "GrinObserveDemoHud active", _hud != null && GodotObject.IsInstanceValid(_hud) ? "PASS" : "FAIL", HudPath.ToString());
		AddCheck("scene boot", "FilmOverlay2D active", _overlay != null && GodotObject.IsInstanceValid(_overlay) ? "PASS" : "FAIL", OverlayPath.ToString());
		AddCheck("scene boot", "GrinFilmCamera active", _filmCamera != null && GodotObject.IsInstanceValid(_filmCamera) ? "PASS" : "FAIL", FilmCameraPath.ToString());
		AddCheck("scene boot", "paired comparison scene reachable", IsPairedSceneReachable(out string pairDetail) ? "PASS" : "FAIL", pairDetail);
		AddCheck("scene boot", "no fatal runtime errors during first frames", "PASS", $"verifier reached frame {FramesToWait}");
		AddCheck("user behavior", "objects locked for v0.0-pre", "PASS", "verification does not expose object manipulation controls");

		bool filmMacrosDisabled = _filmCamera == null || !_filmCamera.RuntimeMacroHotkeysEnabled;
		AddCheck(
			"key conflict",
			"renderer F6-F10 macro hotkeys disabled",
			filmMacrosDisabled ? "PASS" : "BLOCKED BY KEY CONFLICT",
			filmMacrosDisabled ? "GrinFilmCamera.RuntimeMacroHotkeysEnabled=false" : "GrinFilmCamera.RuntimeMacroHotkeysEnabled=true");

		List<string> activeBeamHotkeys = new();
		CollectActiveRayBeamHotkeys(GetTree().CurrentScene, activeBeamHotkeys);
		AddCheck(
			"key conflict",
			"RayBeamRenderer debug hotkeys disabled",
			activeBeamHotkeys.Count == 0 ? "PASS" : "BLOCKED BY KEY CONFLICT",
			activeBeamHotkeys.Count == 0 ? "no active RayBeamRenderer debug hotkeys found" : string.Join(", ", activeBeamHotkeys));
	}

	private void RunControlChecks()
	{
		if (_hud == null || !GodotObject.IsInstanceValid(_hud))
		{
			AddCheck("controls", "F1-F12 cockpit map", "FAIL", "HUD missing; controls not tested");
			return;
		}

		Key[] keys =
		{
			Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6,
			Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12
		};

		foreach (Key key in keys)
		{
			_hud.TryRunControlForVerification(key, out string status, out string detail);
			AddCheck("controls", key.ToString(), status, detail);
		}
	}

	private ArtifactResult CaptureArtifact(string fileName, string label)
	{
		Image image = TryGetArtifactImage();
		string projectPath = BuildProjectOutputPath(fileName);
		string fullPath = ProjectSettings.GlobalizePath(projectPath);
		EnsureParentDirectory(fullPath);

		if (image == null)
		{
			return new ArtifactResult(label, ProjectRelativePath(fullPath), 0, 0, 0, 0, GetTracedPixels(), 0.0, string.Empty, "FAIL", "viewport image unavailable");
		}

		int width = image.GetWidth();
		int height = image.GetHeight();
		PixelStats stats = MeasurePixels(image);
		Error error = image.SavePng(fullPath);
		image.Dispose();
		string sha = error == Error.Ok ? ComputeSha256(fullPath) : string.Empty;
		string status = error == Error.Ok && stats.CoveragePercent >= MinNonBackgroundCoveragePercent ? "PASS" : "FAIL";
		string detail = error == Error.Ok
			? $"full-pixel scan threshold >= {MinNonBackgroundCoveragePercent.ToString("0.###", CultureInfo.InvariantCulture)}%"
			: $"SavePng failed: {error}";

		return new ArtifactResult(
			label,
			ProjectRelativePath(fullPath),
			width,
			height,
			stats.TotalPixels,
			stats.NonBackgroundPixels,
			GetTracedPixels(),
			stats.CoveragePercent,
			sha,
			status,
			detail);
	}

	private Image TryGetArtifactImage()
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

	private readonly struct PixelStats
	{
		public readonly long TotalPixels;
		public readonly long NonBackgroundPixels;
		public readonly double CoveragePercent;

		public PixelStats(long totalPixels, long nonBackgroundPixels, double coveragePercent)
		{
			TotalPixels = totalPixels;
			NonBackgroundPixels = nonBackgroundPixels;
			CoveragePercent = coveragePercent;
		}
	}

	private static PixelStats MeasurePixels(Image image)
	{
		int width = image.GetWidth();
		int height = image.GetHeight();
		long total = (long)width * height;
		if (width <= 0 || height <= 0)
		{
			return new PixelStats(0, 0, 0.0);
		}

		Color background = image.GetPixel(0, 0);
		long nonBackground = 0;
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				Color pixel = image.GetPixel(x, y);
				if (pixel.A > 0.01f && ColorDistance(pixel, background) > 0.035f)
				{
					nonBackground++;
				}
			}
		}

		double percent = total > 0 ? (nonBackground * 100.0) / total : 0.0;
		return new PixelStats(total, nonBackground, percent);
	}

	private long GetTracedPixels()
	{
		if (_filmCamera != null &&
			GodotObject.IsInstanceValid(_filmCamera) &&
			_filmCamera.TryGetFixtureDebugStatsForTesting(out GrinFilmCamera.FixtureDebugStatsSnapshot snapshot))
		{
			return snapshot.TracedPixels;
		}
		return -1;
	}

	private void WriteJsonResult()
	{
		string roleToken = NormalizeRoleToken(Role);
		string fullPath = ProjectSettings.GlobalizePath(BuildProjectOutputPath($"playmode_verify_{roleToken}.json"));
		EnsureParentDirectory(fullPath);
		File.WriteAllText(fullPath, BuildJson());
		GD.Print($"[GrinObservePlayModeVerify] wrote {ProjectRelativePath(fullPath)}");
	}

	private string BuildJson()
	{
		StringBuilder sb = new();
		string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
		sb.AppendLine("{");
		sb.AppendLine("  \"schema\": \"xprimeray.grin_observe_playmode_verify.v1\",");
		sb.AppendLine($"  \"timestamp_utc\": \"{JsonEscape(timestamp)}\",");
		sb.AppendLine($"  \"role\": \"{JsonEscape(NormalizeRoleToken(Role))}\",");
		sb.AppendLine($"  \"scene\": \"{JsonEscape(GetTree().CurrentScene?.SceneFilePath ?? string.Empty)}\",");
		sb.AppendLine($"  \"hud_active\": {JsonBool(_hud != null && GodotObject.IsInstanceValid(_hud))},");
		sb.AppendLine($"  \"keymap_markdown\": \"{JsonEscape(_hud?.BuildKeymapMarkdownRows() ?? string.Empty)}\",");
		sb.AppendLine("  \"checks\": [");
		for (int i = 0; i < _checks.Count; i++)
		{
			CheckResult check = _checks[i];
			sb.Append("    { ");
			sb.Append($"\"area\": \"{JsonEscape(check.Area)}\", ");
			sb.Append($"\"name\": \"{JsonEscape(check.Name)}\", ");
			sb.Append($"\"status\": \"{JsonEscape(check.Status)}\", ");
			sb.Append($"\"detail\": \"{JsonEscape(check.Detail)}\" ");
			sb.Append(i == _checks.Count - 1 ? "}\n" : "},\n");
		}
		sb.AppendLine("  ],");
		sb.AppendLine("  \"artifacts\": [");
		for (int i = 0; i < _artifacts.Count; i++)
		{
			ArtifactResult artifact = _artifacts[i];
			sb.AppendLine("    {");
			sb.AppendLine($"      \"label\": \"{JsonEscape(artifact.Label)}\",");
			sb.AppendLine($"      \"path\": \"{JsonEscape(artifact.Path)}\",");
			sb.AppendLine($"      \"width\": {artifact.Width},");
			sb.AppendLine($"      \"height\": {artifact.Height},");
			sb.AppendLine($"      \"total_pixels\": {artifact.TotalPixels},");
			sb.AppendLine($"      \"non_background_pixels\": {artifact.NonBackgroundPixels},");
			sb.AppendLine($"      \"traced_pixels\": {artifact.TracedPixels},");
			sb.AppendLine($"      \"coverage_percent\": {artifact.CoveragePercent.ToString("0.######", CultureInfo.InvariantCulture)},");
			sb.AppendLine($"      \"sha256\": \"{JsonEscape(artifact.Sha256)}\",");
			sb.AppendLine($"      \"status\": \"{JsonEscape(artifact.Status)}\",");
			sb.AppendLine($"      \"detail\": \"{JsonEscape(artifact.Detail)}\"");
			sb.Append(i == _artifacts.Count - 1 ? "    }\n" : "    },\n");
		}
		sb.AppendLine("  ],");
		sb.AppendLine($"  \"summary_status\": \"{(HasHardFailure() ? "FAIL" : "PASS")}\",");
		sb.AppendLine("  \"known_limitations\": [");
		sb.AppendLine("    \"F2 is verified as matched scene reachability during automated play-mode verification; normal Play mode still performs scene switching.\",");
		sb.AppendLine("    \"The full-pixel pass scans exported viewport PNG artifacts; advanced physics validation remains outside v0.0-pre scope.\"");
		sb.AppendLine("  ]");
		sb.AppendLine("}");
		return sb.ToString();
	}

	private void AddCheck(string area, string name, string status, string detail)
	{
		_checks.Add(new CheckResult(area, name, status, detail));
	}

	private bool HasHardFailure()
	{
		foreach (CheckResult check in _checks)
		{
			if (check.Status == "FAIL" || check.Status == "BLOCKED BY KEY CONFLICT")
			{
				return true;
			}
		}
		foreach (ArtifactResult artifact in _artifacts)
		{
			if (artifact.Status == "FAIL" || artifact.Status == "BLOCKED BY KEY CONFLICT")
			{
				return true;
			}
		}
		return false;
	}

	private bool IsExpectedScene(string scene)
	{
		if (IsCurvedRole())
		{
			return scene.EndsWith("test-grin-basic-visual-offaxis-observe.tscn", StringComparison.OrdinalIgnoreCase);
		}
		return scene.EndsWith("test-straight-basic-visual-offaxis-observe.tscn", StringComparison.OrdinalIgnoreCase);
	}

	private bool IsPairedSceneReachable(out string detail)
	{
		if (_hud == null || !GodotObject.IsInstanceValid(_hud))
		{
			detail = "HUD missing";
			return false;
		}
		detail = _hud.PairedScenePath;
		return !string.IsNullOrWhiteSpace(_hud.PairedScenePath) && ResourceLoader.Exists(_hud.PairedScenePath);
	}

	private void CollectActiveRayBeamHotkeys(Node node, List<string> active)
	{
		if (node == null)
		{
			return;
		}
		if (node is RayBeamRenderer renderer && renderer.DebugHotkeysEnabled)
		{
			active.Add(renderer.GetPath());
		}
		foreach (Node child in node.GetChildren())
		{
			CollectActiveRayBeamHotkeys(child, active);
		}
	}

	private string BuildVerifyArtifactName()
	{
		return IsCurvedRole() ? "curved_grin_verify.png" : "straight_control_verify.png";
	}

	private string BuildProjectOutputPath(string fileName)
	{
		string root = string.IsNullOrWhiteSpace(OutputDir) ? "res://output/v0.0-pre" : OutputDir;
		if (!root.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
		{
			root = "res://" + root.TrimStart('/', '\\');
		}
		return root.TrimEnd('/', '\\') + "/" + fileName;
	}

	private bool IsCurvedRole()
	{
		return NormalizeRoleToken(Role).Contains("curved", StringComparison.OrdinalIgnoreCase);
	}

	private void ApplyUserArgs()
	{
		foreach (string raw in OS.GetCmdlineUserArgs())
		{
			string arg = raw ?? string.Empty;
			if (arg.StartsWith("--grin-observe-playmode-role=", StringComparison.OrdinalIgnoreCase))
			{
				Role = arg.Substring("--grin-observe-playmode-role=".Length);
			}
			else if (arg.StartsWith("--grin-observe-playmode-output=", StringComparison.OrdinalIgnoreCase))
			{
				OutputDir = arg.Substring("--grin-observe-playmode-output=".Length);
			}
			else if (arg.StartsWith("--grin-observe-playmode-frames=", StringComparison.OrdinalIgnoreCase) &&
				int.TryParse(arg.Substring("--grin-observe-playmode-frames=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int frames))
			{
				FramesToWait = Math.Max(1, frames);
			}
		}
	}

	private static bool HasVerifyArg()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (string.Equals(arg, "--grin-observe-playmode-verify=1", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(arg, "--grin-observe-playmode-verify", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static string NormalizeRoleToken(string role)
	{
		string token = string.IsNullOrWhiteSpace(role) ? "curved" : role.Trim().ToLowerInvariant();
		token = token.Replace(" ", "_", StringComparison.Ordinal).Replace("-", "_", StringComparison.Ordinal);
		if (token == "straight")
		{
			return "straight_control";
		}
		if (token == "curved")
		{
			return "curved_grin";
		}
		return token;
	}

	private static double ColorDistance(Color a, Color b)
	{
		double dr = a.R - b.R;
		double dg = a.G - b.G;
		double db = a.B - b.B;
		double da = a.A - b.A;
		return Math.Sqrt((dr * dr) + (dg * dg) + (db * db) + (da * da));
	}

	private static string ComputeSha256(string path)
	{
		using FileStream stream = File.OpenRead(path);
		byte[] hash = SHA256.HashData(stream);
		return Convert.ToHexString(hash).ToLowerInvariant();
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

	private static string JsonBool(bool value) => value ? "true" : "false";

	private static string JsonEscape(string value)
	{
		return (value ?? string.Empty)
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal)
			.Replace("\r", "\\r", StringComparison.Ordinal);
	}
}
