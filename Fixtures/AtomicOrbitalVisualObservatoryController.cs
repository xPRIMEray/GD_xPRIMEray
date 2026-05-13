using Godot;
using System;
using System.Globalization;
using System.IO;

public partial class AtomicOrbitalVisualObservatoryController : Node3D
{
	private static readonly NodePath FieldPath = new("AtomicEigenmodeFieldSource3D");
	private static readonly NodePath RendererPath = new("RayBeamRenderer");
	private static readonly NodePath FilmPath = new("../GrinFilmCamera");
	private static readonly NodePath VisualGuidesRootPath = new("VisualGuidesRoot");
	private static readonly NodePath DensityMarkersRootPath = new("DensityMarkersRoot");
	private static readonly NodePath LaserGuidesRootPath = new("LaserGuidesRoot");
	private static readonly NodePath BeamGuidesRootPath = new("BeamGuidesRoot");

	private const string ElectronCountPrefix = "--atomic-electron-count=";
	private const string PresetPrefix = "--atomic-preset=";
	private const string OrbitalRadiusPrefix = "--atomic-orbital-radius=";
	private const string CurvatureStrengthPrefix = "--atomic-curvature-strength=";
	private const string ModulationDepthPrefix = "--atomic-modulation-depth=";
	private const string FieldClockHzPrefix = "--atomic-field-clock-hz=";
	private const string UpdateIntervalPrefix = "--atomic-update-interval-seconds=";
	private const string TimeEnabledPrefix = "--atomic-time-enabled=";
	private const string OutputDirPrefix = "--atomic-output-dir=";
	private const string PhasePrefix = "--atomic-phase=";
	private const string FieldTickPrefix = "--atomic-field-tick-index=";
	private const string RenderCaptureDirPrefix = "--render-test-capture-dir=";
	private const string VisualDensityPrefix = "--atomic-visual-density=";
	private const string VisualGuidesPrefix = "--atomic-visual-guides=";
	private const string VisualLaserPrefix = "--atomic-visual-laser-sheet=";
	private const string VisualBeamsPrefix = "--atomic-visual-beams=";
	private const string VisualAllowExtremePrefix = "--atomic-visual-allow-extreme=";
	private const string VisualOutputDirPrefix = "--atomic-visual-output-dir=";

	private AtomicEigenmodeFieldSource3D _field;
	private GrinFilmCamera _film;
	private string _csvPath = string.Empty;
	private double _startTimeSec;
	private bool _densityEnabled;
	private bool _guidesEnabled;
	private bool _laserEnabled;
	private bool _beamsEnabled;
	private bool _wroteTelemetry;

	public override void _Ready()
	{
		_field = GetNodeOrNull<AtomicEigenmodeFieldSource3D>(FieldPath);
		RayBeamRenderer renderer = GetNodeOrNull<RayBeamRenderer>(RendererPath);
		_film = GetNodeOrNull<GrinFilmCamera>(FilmPath);
		_film?.SetHudFixtureName("atomic_orbital_visual_observatory");
		_startTimeSec = Time.GetTicksMsec() / 1000.0;

		ConfigureField();
		ConfigureVisualRoots();
		ConfigureRenderer(renderer);
		PrepareTelemetryCsv();
		AuditVisualOnlyRoots();

		GD.Print("[AtomicOrbitalVisual][Contract] fixture=atomic_orbital_visual_observatory purpose=interpretation_only closure_validation=0 proof_claim=0");
		GD.Print($"[AtomicOrbitalVisual][Field] {(_field != null ? _field.BuildStateToken() : "missing_field")}");
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (_wroteTelemetry)
		{
			return;
		}

		if (WriteTelemetryRowIfReady())
		{
			_wroteTelemetry = true;
		}
	}

	private void ConfigureField()
	{
		if (_field == null)
		{
			return;
		}

		_field.ElectronCount = Mathf.Clamp(ReadInt(ElectronCountPrefix, 1), 0, 3);
		_field.AtomicPreset = ReadString(PresetPrefix, "hydrogen");
		_field.OrbitalRadius = Mathf.Max(0.001f, ReadFloat(OrbitalRadiusPrefix, 8.0f));
		float requestedStrength = Mathf.Max(0f, ReadFloat(CurvatureStrengthPrefix, 0.05f));
		bool allowExtreme = ReadBool(VisualAllowExtremePrefix, false);
		if (requestedStrength > 0.1f && !allowExtreme)
		{
			GD.PushWarning(
				$"[AtomicOrbitalVisual][Warn] curvatureStrength={requestedStrength.ToString("0.######", CultureInfo.InvariantCulture)} exceeds visual default max 0.1; clamping. Pass --atomic-visual-allow-extreme=1 to opt in.");
			requestedStrength = 0.1f;
		}

		_field.CurvatureStrength = requestedStrength;
		_field.ModulationDepth = Mathf.Clamp(ReadFloat(ModulationDepthPrefix, 0.35f), 0f, 1f);
		_field.FieldClockHz = Mathf.Max(0f, ReadFloat(FieldClockHzPrefix, 0.25f));
		_field.UpdateIntervalSeconds = Mathf.Max(0.001f, ReadFloat(UpdateIntervalPrefix, 1.0f));
		_field.TimeEnabled = ReadBool(TimeEnabledPrefix, false);
		_field.FieldTickIndex = Math.Max(0, ReadInt(FieldTickPrefix, _field.FieldTickIndex));
		_field.Phase = ReadFloat(PhasePrefix, ComputePhaseFromTick(_field.FieldTickIndex, _field.FieldClockHz, _field.UpdateIntervalSeconds));
		_field.ProtonCoreEnabled = false;
		_field.Enabled = _field.ElectronCount > 0 && _field.CurvatureStrength > 1e-7f;
		_field.MetricModel = RendererCore.Fields.MetricModel.GRIN;
		_field.TransportModel = RendererCore.Config.TransportModel.GRIN_Optical;
		_field.DebugVizEnabled = false;
		_field.DebugVizInGame = false;
	}

	private void ConfigureVisualRoots()
	{
		_densityEnabled = ReadBool(VisualDensityPrefix, true);
		_guidesEnabled = ReadBool(VisualGuidesPrefix, true);
		_laserEnabled = ReadBool(VisualLaserPrefix, true);
		_beamsEnabled = ReadBool(VisualBeamsPrefix, true);
		SetRootVisible(VisualGuidesRootPath, _guidesEnabled);
		SetRootVisible(DensityMarkersRootPath, _densityEnabled);
		SetRootVisible(LaserGuidesRootPath, _laserEnabled);
		SetRootVisible(BeamGuidesRootPath, _beamsEnabled);
	}

	private void ConfigureRenderer(RayBeamRenderer renderer)
	{
		if (renderer == null)
		{
			return;
		}

		renderer.UseIntegratedField = true;
		renderer.BendScale = 1.0f;
		renderer.FieldStrength = 1.0f;
		renderer.StopOnHit = true;
		renderer.TerminateTrailOnHit = true;
		renderer.RequireHitToRender = false;
	}

	private void PrepareTelemetryCsv()
	{
		string outputDir = ReadString(VisualOutputDirPrefix, string.Empty);
		if (string.IsNullOrWhiteSpace(outputDir))
		{
			outputDir = ReadString(OutputDirPrefix, string.Empty);
		}
		if (string.IsNullOrWhiteSpace(outputDir))
		{
			outputDir = ReadString(RenderCaptureDirPrefix, Path.Combine("output", "atomic_orbital_visual_observatory"));
		}

		outputDir = ResolveOutputPath(outputDir);
		Directory.CreateDirectory(outputDir);
		_csvPath = Path.Combine(outputDir, "atomic_visual_telemetry.csv");
		File.WriteAllText(
			_csvPath,
			"frame_index,render_time,field_tick_index,phase,electron_count,atomic_preset,orbital_radius,curvature_strength,modulation_depth,visual_density,visual_guides,visual_laser_sheet,visual_beams,hit_pixels,miss_pixels,mean_density_sampled,max_density_sampled\n");
	}

	private bool WriteTelemetryRowIfReady()
	{
		if (string.IsNullOrWhiteSpace(_csvPath) || _field == null)
		{
			return false;
		}

		long hitPixels = 0;
		long missPixels = 0;
		if (_film != null &&
			GodotObject.IsInstanceValid(_film) &&
			_film.TryGetFixtureDebugStatsForTesting(out GrinFilmCamera.FixtureDebugStatsSnapshot stats))
		{
			hitPixels = stats.SourceHits + stats.BackgroundHits + stats.UnclassifiedHits + stats.AbsorbedHits;
			missPixels = stats.MissHits;
		}

		(double meanDensity, double maxDensity) = SampleDensityStats();
		double renderTime = (Time.GetTicksMsec() / 1000.0) - _startTimeSec;
		File.AppendAllText(
			_csvPath,
			string.Format(
				CultureInfo.InvariantCulture,
				"0,{0:0.######},{1},{2:0.######},{3},{4},{5:0.######},{6:0.######},{7:0.######},{8},{9},{10},{11},{12},{13},{14:0.######},{15:0.######}\n",
				renderTime,
				_field.FieldTickIndex,
				_field.Phase,
				Mathf.Clamp(_field.ElectronCount, 0, 3),
				SanitizeCsvToken(_field.AtomicPreset),
				_field.OrbitalRadius,
				_field.CurvatureStrength,
				_field.ModulationDepth,
				_densityEnabled ? 1 : 0,
				_guidesEnabled ? 1 : 0,
				_laserEnabled ? 1 : 0,
				_beamsEnabled ? 1 : 0,
				hitPixels,
				missPixels,
				meanDensity,
				maxDensity));
		return true;
	}

	private void AuditVisualOnlyRoots()
	{
		AuditVisualOnlyRoot(VisualGuidesRootPath);
		AuditVisualOnlyRoot(DensityMarkersRootPath);
		AuditVisualOnlyRoot(LaserGuidesRootPath);
		AuditVisualOnlyRoot(BeamGuidesRootPath);
	}

	private void AuditVisualOnlyRoot(NodePath path)
	{
		Node root = GetNodeOrNull<Node>(path);
		if (root == null)
		{
			return;
		}

		if (!root.IsInGroup("visual_only"))
		{
			GD.PushWarning($"[AtomicOrbitalVisual][Warn] root={root.Name} missing visual_only group");
		}

		foreach (Node node in EnumerateDescendants(root))
		{
			if (node.IsInGroup("raytrace_geometry") || node.IsInGroup("fixture_geometry") || node.IsInGroup("fixture_background") || node.IsInGroup("hermetic_receiver"))
			{
				GD.PushWarning($"[AtomicOrbitalVisual][Warn] visual_only node has raytrace/fixture group path={node.GetPath()}");
			}
			if (node is CollisionObject3D)
			{
				GD.PushWarning($"[AtomicOrbitalVisual][Warn] visual_only node has collision object path={node.GetPath()}");
			}
		}
	}

	private static System.Collections.Generic.IEnumerable<Node> EnumerateDescendants(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			yield return child;
			foreach (Node nested in EnumerateDescendants(child))
			{
				yield return nested;
			}
		}
	}

	private void SetRootVisible(NodePath path, bool visible)
	{
		if (GetNodeOrNull<Node3D>(path) is Node3D node)
		{
			node.Visible = visible;
		}
	}

	private (double mean, double max) SampleDensityStats()
	{
		if (_field == null)
		{
			return (0.0, 0.0);
		}

		const int sampleCount = 48;
		double sum = 0.0;
		double max = 0.0;
		for (int i = 0; i < sampleCount; i++)
		{
			float t = sampleCount <= 1 ? 0f : i / (float)(sampleCount - 1);
			float density = _field.EvaluateDensity(t * _field.SupportRadius);
			sum += density;
			max = Math.Max(max, density);
		}

		return (sum / sampleCount, max);
	}

	private static float ComputePhaseFromTick(int tick, float hz, float interval)
	{
		return Mathf.Tau * Mathf.Max(0f, hz) * Math.Max(0, tick) * Mathf.Max(0.001f, interval);
	}

	private static int ReadInt(string prefix, int fallback)
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
				int.TryParse(arg.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
			{
				return value;
			}
		}

		return fallback;
	}

	private static float ReadFloat(string prefix, float fallback)
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
				float.TryParse(arg.Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
			{
				return value;
			}
		}

		return fallback;
	}

	private static bool ReadBool(string prefix, bool fallback)
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string value = arg.Substring(prefix.Length).Trim();
			if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		return fallback;
	}

	private static string ReadString(string prefix, string fallback)
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				return arg.Substring(prefix.Length).Trim();
			}
		}

		return fallback;
	}

	private static string ResolveOutputPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return Path.Combine(Directory.GetCurrentDirectory(), "output", "atomic_orbital_visual_observatory");
		}

		return Path.IsPathRooted(path) ? path : Path.Combine(Directory.GetCurrentDirectory(), path);
	}

	private static string SanitizeCsvToken(string value)
	{
		return (value ?? string.Empty).Replace(",", "_").Replace("\n", "_").Replace("\r", "_");
	}
}
