using Godot;
using System;
using System.Globalization;
using System.IO;

public partial class AtomicOrbitalGrinRoomController : Node3D
{
	private static readonly NodePath FieldPath = new("AtomicEigenmodeFieldSource3D");
	private static readonly NodePath RendererPath = new("RayBeamRenderer");
	private static readonly NodePath FilmPath = new("../GrinFilmCamera");

	private const string ElectronCountPrefix = "--atomic-electron-count=";
	private const string PresetPrefix = "--atomic-preset=";
	private const string OrbitalRadiusPrefix = "--atomic-orbital-radius=";
	private const string CurvatureStrengthPrefix = "--atomic-curvature-strength=";
	private const string ModulationDepthPrefix = "--atomic-modulation-depth=";
	private const string FieldClockHzPrefix = "--atomic-field-clock-hz=";
	private const string UpdateIntervalPrefix = "--atomic-update-interval-seconds=";
	private const string TimeEnabledPrefix = "--atomic-time-enabled=";
	private const string CaptureFramesPrefix = "--atomic-capture-frames=";
	private const string OutputDirPrefix = "--atomic-output-dir=";
	private const string PhasePrefix = "--atomic-phase=";
	private const string FieldTickPrefix = "--atomic-field-tick-index=";
	private const string RenderCaptureDirPrefix = "--render-test-capture-dir=";

	private AtomicEigenmodeFieldSource3D _field;
	private GrinFilmCamera _film;
	private string _csvPath = string.Empty;
	private double _startTimeSec;
	private int _samplesWritten;
	private int _captureFrames = 1;
	private int _processFrames;

	public override void _Ready()
	{
		_field = GetNodeOrNull<AtomicEigenmodeFieldSource3D>(FieldPath);
		RayBeamRenderer renderer = GetNodeOrNull<RayBeamRenderer>(RendererPath);
		_film = GetNodeOrNull<GrinFilmCamera>(FilmPath);
		_film?.SetHudFixtureName("atomic_orbital_grin_room");
		_startTimeSec = Time.GetTicksMsec() / 1000.0;

		ConfigureField();
		ConfigureRenderer(renderer);
		PrepareTelemetryCsv();

		GD.Print(
			"[AtomicOrbitalGRIN][Contract] fixture=atomic_orbital_grin_room sealed_surfaces=6 " +
			"v1_ladder=A0_A1_A2_A3 physical_truth_claim=0");
		GD.Print($"[AtomicOrbitalGRIN][Field] {(_field != null ? _field.BuildStateToken() : "missing_field")}");
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (_samplesWritten >= _captureFrames)
		{
			return;
		}

		_processFrames++;
		if (WriteTelemetryRowIfReady())
		{
			_samplesWritten++;
		}
	}

	private void ConfigureField()
	{
		if (_field == null)
		{
			return;
		}

		_field.ElectronCount = Mathf.Clamp(ReadInt(ElectronCountPrefix, _field.ElectronCount), 0, 3);
		_field.AtomicPreset = ReadString(PresetPrefix, _field.AtomicPreset);
		_field.OrbitalRadius = Mathf.Max(0.001f, ReadFloat(OrbitalRadiusPrefix, _field.OrbitalRadius));
		_field.CurvatureStrength = Mathf.Max(0f, ReadFloat(CurvatureStrengthPrefix, _field.CurvatureStrength));
		_field.ModulationDepth = Mathf.Clamp(ReadFloat(ModulationDepthPrefix, _field.ModulationDepth), 0f, 1f);
		_field.FieldClockHz = Mathf.Max(0f, ReadFloat(FieldClockHzPrefix, _field.FieldClockHz));
		_field.UpdateIntervalSeconds = Mathf.Max(0.001f, ReadFloat(UpdateIntervalPrefix, _field.UpdateIntervalSeconds));
		_field.TimeEnabled = ReadBool(TimeEnabledPrefix, _field.TimeEnabled);
		_field.FieldTickIndex = Math.Max(0, ReadInt(FieldTickPrefix, _field.FieldTickIndex));
		_field.Phase = ReadFloat(PhasePrefix, ComputePhaseFromTick(_field.FieldTickIndex, _field.FieldClockHz, _field.UpdateIntervalSeconds));
		_field.ProtonCoreEnabled = false;
		_field.Enabled = _field.ElectronCount > 0 && _field.CurvatureStrength > 1e-7f;
		_field.MetricModel = RendererCore.Fields.MetricModel.GRIN;
		_field.TransportModel = RendererCore.Config.TransportModel.GRIN_Optical;
		_field.DebugVizEnabled = false;
		_field.DebugVizInGame = false;
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
		_captureFrames = Math.Max(1, ReadInt(CaptureFramesPrefix, 1));
		string outputDir = ReadString(OutputDirPrefix, string.Empty);
		if (string.IsNullOrWhiteSpace(outputDir))
		{
			outputDir = ReadString(RenderCaptureDirPrefix, Path.Combine("output", "atomic_orbital_grin"));
		}

		outputDir = ResolveOutputPath(outputDir);
		Directory.CreateDirectory(outputDir);
		_csvPath = Path.Combine(outputDir, "atomic_frame_telemetry.csv");
		File.WriteAllText(
			_csvPath,
			"frame_index,render_time,field_tick_index,phase,electron_count,atomic_preset,orbital_radius,curvature_strength,modulation_depth,hit_pixels,miss_pixels,budget_exhausted_pixels,mean_steps_per_pixel,max_steps_per_pixel,mean_density_sampled,max_density_sampled,mean_curvature_magnitude,max_curvature_magnitude\n");
	}

	private bool WriteTelemetryRowIfReady()
	{
		if (string.IsNullOrWhiteSpace(_csvPath) || _field == null)
		{
			return false;
		}

		double renderTime = (Time.GetTicksMsec() / 1000.0) - _startTimeSec;
		long hitPixels = 0;
		long missPixels = 0;
		long budgetExhaustedPixels = 0;
		double meanSteps = 0.0;
		double maxSteps = 0.0;
		double meanCurvature = 0.0;
		double maxCurvature = 0.0;

		bool hasAnyRuntimeStats = false;
		if (_film != null && GodotObject.IsInstanceValid(_film))
		{
			if (_film.TryGetFixtureDebugStatsForTesting(out GrinFilmCamera.FixtureDebugStatsSnapshot stats))
			{
				hasAnyRuntimeStats = true;
				hitPixels = stats.SourceHits + stats.BackgroundHits + stats.UnclassifiedHits + stats.AbsorbedHits;
				missPixels = stats.MissHits;
			}

			if (_film.TryGetTelemetryHeatmapStatsForTesting(GrinFilmCamera.TelemetryHeatmapKind.Pass1Steps, out GrinFilmCamera.TelemetryHeatmapStats stepStats))
			{
				hasAnyRuntimeStats = true;
				meanSteps = stepStats.Mean;
				maxSteps = stepStats.Max;
			}

			if (_film.TryGetTelemetryHeatmapStatsForTesting(GrinFilmCamera.TelemetryHeatmapKind.CurvatureMean, out GrinFilmCamera.TelemetryHeatmapStats curvatureStats))
			{
				hasAnyRuntimeStats = true;
				meanCurvature = curvatureStats.Mean;
				maxCurvature = curvatureStats.Max;
			}

			if (_film.TryGetLatestRenderHealthDiagnosticsForTesting(out GrinFilmCamera.RenderHealthDiagnosticsSnapshot health) &&
				!string.Equals(health.BudgetExitReason, "none", StringComparison.OrdinalIgnoreCase))
			{
				budgetExhaustedPixels = -1;
			}
		}

		if (!hasAnyRuntimeStats && _processFrames < 600)
		{
			return false;
		}

		(double meanDensity, double maxDensity) = SampleDensityStats();
		string row = string.Format(
			CultureInfo.InvariantCulture,
			"{0},{1:0.######},{2},{3:0.######},{4},{5},{6:0.######},{7:0.######},{8:0.######},{9},{10},{11},{12:0.######},{13:0.######},{14:0.######},{15:0.######},{16:0.######},{17:0.######}\n",
			_samplesWritten,
			renderTime,
			_field.FieldTickIndex,
			_field.Phase,
			Mathf.Clamp(_field.ElectronCount, 0, 3),
			SanitizeCsvToken(_field.AtomicPreset),
			_field.OrbitalRadius,
			_field.CurvatureStrength,
			_field.ModulationDepth,
			hitPixels,
			missPixels,
			budgetExhaustedPixels,
			meanSteps,
			maxSteps,
			meanDensity,
			maxDensity,
			meanCurvature,
			maxCurvature);
		File.AppendAllText(_csvPath, row);
		return true;
	}

	private (double mean, double max) SampleDensityStats()
	{
		const int sampleCount = 32;
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

	private static string ResolveOutputPath(string path)
	{
		if (Path.IsPathRooted(path))
		{
			return path;
		}

		return Path.Combine(ProjectSettings.GlobalizePath("res://"), path);
	}

	private static string SanitizeCsvToken(string token)
	{
		return (token ?? string.Empty).Replace(',', '_').Replace('\n', '_').Replace('\r', '_');
	}

	private static float ReadFloat(string prefix, float fallback)
	{
		string raw = ReadString(prefix, string.Empty);
		return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
	}

	private static int ReadInt(string prefix, int fallback)
	{
		string raw = ReadString(prefix, string.Empty);
		return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
	}

	private static bool ReadBool(string prefix, bool fallback)
	{
		string raw = ReadString(prefix, string.Empty).Trim().ToLowerInvariant();
		return raw switch
		{
			"1" or "true" or "yes" or "on" => true,
			"0" or "false" or "no" or "off" => false,
			_ => fallback
		};
	}

	private static string ReadString(string prefix, string fallback)
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (!string.IsNullOrWhiteSpace(arg) && arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				return arg.Substring(prefix.Length).Trim();
			}
		}

		foreach (string arg in OS.GetCmdlineArgs())
		{
			if (!string.IsNullOrWhiteSpace(arg) && arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				return arg.Substring(prefix.Length).Trim();
			}
		}

		return fallback;
	}
}
