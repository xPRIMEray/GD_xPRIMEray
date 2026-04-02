using Godot;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using XPrimeRay.Perf; // adjust namespace new PerfScope.cs
using RendererCore.Common;
using RendererCore.SceneSnapshot;
using RendererCore.Fields;
using RendererCore.Config;

public partial class GrinFilmCamera : Node
{
	// ===== Interaction Map =====
	// Provides to RayBeamRenderer:
	// - Debug overlay data via UpdateDebugOverlayFromFilm(...) call (points/offsets/counts/hits)
	// Consumes from RayBeamRenderer:
	// - Ray integration and collision settings (StepsPerRay, CollisionEveryNSteps, etc.)
	// - Segment builders and hit payload structures (RaySeg, HitPayload, BuildRaySegmentsCamera_Pass1, GetDebugRayBundle)
	// Transfer points:
	// - _rbr acquired from RayBeamRendererPath in _Ready/_Process
	// - UpdateDebugOverlayFromFilm(...) called during render pass when DebugOverlayOwnedByFilm is true

	// ===== Inputs / Controls =====

	public enum RenderQualityMode
	{
		Debug,
		FastPreview,
		Balanced,
		Quality,
		Barebones
	}

	public enum PresetMode
	{
		Walk = 0,
		Preview = 1,
		Cinematic = 2
	}

	public enum PerformancePresetMode
	{
		None = 0,
		FastPreview = 1,
		Quality = 2
	}

	public enum TestCameraMode
	{
		None = 0,
		Fixed = 1,
		Orbit = 2
	}

	public enum SmartScaleGoalMode
	{
		MaxHits = 0
	}

	public enum SmartScaleProbeBudgetMode
	{
		RenderStepCalls = 0,
		RowsAdvanced = 1
	}

	public enum RuntimeMacroMode
	{
		Accurate = 0,
		Tight08 = 1,
		CheapMotion = 2,
		SettleRefine = 3
	}

	public struct TestRunConfig
	{
		public string Name;
		public bool? UpdateEveryFrame;
		public bool? UseGeometryTLASPruning;
		public float? Pass2GeomEnvelopeRadiusScale;
		public float? Pass2GeomEnvelopeAabbExpand;
		public bool? AdaptiveTelemetryEnvelopeScalingEnabled;
		public string AdaptiveEnvelopeControllerMode;
		public string AdaptiveEnvelopePriorSource;
		public float? AdaptiveEnvelopeHotThresholdPercentile;
		public float? AdaptiveEnvelopeWarmThresholdPercentile;
		public float? AdaptiveEnvelopeRelaxedThresholdPercentile;
		public float? AdaptiveEnvelopeTightScale;
		public float? AdaptiveEnvelopeWarmScale;
		public float? AdaptiveEnvelopeNeutralScale;
		public float? AdaptiveEnvelopeRelaxedScale;
		public bool? UsePass2CollisionStride;
		public int? Pass2CollisionStrideNear;
		public int? Pass2CollisionStrideFar;
		public float? Pass2CollisionStrideFarStartT;
		public float? MinSegLenForStrideSkip;
		public bool? Pass2SoftGateEnableQuickRayMiss;
		public bool? Pass2SoftGateScoringEnabled;
		public int? TargetMsPerFrame;
		public int? SharedRbrStepsPerRay;
		public float? SharedRbrMinStepLength;
		public float? SharedRbrMaxStepLength;
		public float? SharedRbrStepAdaptGain;
		public TestCameraMode CameraMode;
		public Vector3 CameraFixedPosition;
		public Vector3 CameraLookAt;
		public float CameraOrbitRadius;
		public float CameraOrbitHeight;
		public float CameraOrbitPeriodFrames;
	}

	public struct TestRunDefaults
	{
		public bool UpdateEveryFrame;
		public bool UseGeometryTLASPruning;
		public bool UseThreadedBands;
		public int ThreadedBandWorkerCount;
		public int ThreadedBandRowsPerChunk;
		public bool UseThreadedPass2CandidateEval;
		public int ThreadedPass2CandidateWorkers;
		public int ThreadedPass2CandidateRowsPerChunk;
		public bool UseThreadedPass2QueryResolve;
		public int ThreadedPass2QueryWorkers;
		public int ThreadedPass2QueryRowsPerChunk;
		public bool UseThreadedPass2LocalAccumulation;
		public int ThreadedPass2WorkerCount;
		public int ThreadedPass2RowsPerChunk;
		public float Pass2GeomEnvelopeRadiusScale;
		public float Pass2GeomEnvelopeAabbExpand;
		public bool AdaptiveTelemetryEnvelopeScalingEnabled;
		public string AdaptiveEnvelopeControllerMode;
		public string AdaptiveEnvelopePriorSource;
		public string AdaptiveEnvelopeThresholdStatistic;
		public float AdaptiveEnvelopeHotThresholdPercentile;
		public float AdaptiveEnvelopeWarmThresholdPercentile;
		public float AdaptiveEnvelopeRelaxedThresholdPercentile;
		public float AdaptiveEnvelopeTightScale;
		public float AdaptiveEnvelopeWarmScale;
		public float AdaptiveEnvelopeNeutralScale;
		public float AdaptiveEnvelopeRelaxedScale;
		public bool UsePass2CollisionStride;
		public int Pass2CollisionStrideNear;
		public int Pass2CollisionStrideFar;
		public float Pass2CollisionStrideFarStartT;
		public float MinSegLenForStrideSkip;
		public bool Pass2SoftGateEnableQuickRayMiss;
		public bool Pass2SoftGateScoringEnabled;
		public int TargetMsPerFrame;
		public int SharedRbrStepsPerRay;
		public float SharedRbrMinStepLength;
		public float SharedRbrMaxStepLength;
		public float SharedRbrStepAdaptGain;
	}

	public readonly struct FixtureDebugStatsSnapshot
	{
		public readonly long SourceHits;
		public readonly long BackgroundHits;
		public readonly long UnclassifiedHits;
		public readonly long AbsorbedHits;
		public readonly long MissHits;
		public readonly long TracedPixels;

		public FixtureDebugStatsSnapshot(
			long sourceHits,
			long backgroundHits,
			long unclassifiedHits,
			long absorbedHits,
			long missHits,
			long tracedPixels)
		{
			SourceHits = sourceHits;
			BackgroundHits = backgroundHits;
			UnclassifiedHits = unclassifiedHits;
			AbsorbedHits = absorbedHits;
			MissHits = missHits;
			TracedPixels = tracedPixels;
		}
	}

	public readonly struct FixtureRowParticipationSnapshot
	{
		public readonly int TotalRowsConsidered;
		public readonly int TotalRowsProcessed;
		public readonly int TotalRowsSkipped;
		public readonly int ProcessedRowStart;
		public readonly int ProcessedRowEnd;
		public readonly int ZeroHitRows;
		public readonly string ProcessedRowRanges;
		public readonly string SkippedRowRanges;
		public readonly string ZeroHitRowRanges;
		public readonly string Summary;

		public FixtureRowParticipationSnapshot(
			int totalRowsConsidered,
			int totalRowsProcessed,
			int totalRowsSkipped,
			int processedRowStart,
			int processedRowEnd,
			int zeroHitRows,
			string processedRowRanges,
			string skippedRowRanges,
			string zeroHitRowRanges,
			string summary)
		{
			TotalRowsConsidered = totalRowsConsidered;
			TotalRowsProcessed = totalRowsProcessed;
			TotalRowsSkipped = totalRowsSkipped;
			ProcessedRowStart = processedRowStart;
			ProcessedRowEnd = processedRowEnd;
			ZeroHitRows = zeroHitRows;
			ProcessedRowRanges = processedRowRanges ?? "";
			SkippedRowRanges = skippedRowRanges ?? "";
			ZeroHitRowRanges = zeroHitRowRanges ?? "";
			Summary = summary ?? "";
		}
	}

	public readonly struct FilmCaptureDiagnosticsSnapshot
	{
		public readonly int FilmWidth;
		public readonly int FilmHeight;
		public readonly int RowCursor;
		public readonly int DebugRayCount;
		public readonly int DebugPointCount;
		public readonly int DebugMaxFilmRays;
		public readonly long FrameRaysTraced;
		public readonly long FrameSegmentsIntegrated;
		public readonly long FrameSegmentsTested;
		public readonly long FramePhysicsQueries;
		public readonly Color SkyColor;

		public FilmCaptureDiagnosticsSnapshot(
			int filmWidth,
			int filmHeight,
			int rowCursor,
			int debugRayCount,
			int debugPointCount,
			int debugMaxFilmRays,
			long frameRaysTraced,
			long frameSegmentsIntegrated,
			long frameSegmentsTested,
			long framePhysicsQueries,
			Color skyColor)
		{
			FilmWidth = filmWidth;
			FilmHeight = filmHeight;
			RowCursor = rowCursor;
			DebugRayCount = debugRayCount;
			DebugPointCount = debugPointCount;
			DebugMaxFilmRays = debugMaxFilmRays;
			FrameRaysTraced = frameRaysTraced;
			FrameSegmentsIntegrated = frameSegmentsIntegrated;
			FrameSegmentsTested = frameSegmentsTested;
			FramePhysicsQueries = framePhysicsQueries;
			SkyColor = skyColor;
		}
	}

	public enum TelemetryHeatmapKind
	{
		Work = 0,
		Candidates = 1,
		Query = 2,
		Resolve = 3,
		Pass1Steps = 4,
		CurvatureMax = 5,
		CurvatureMean = 6,
		DkMax = 7,
		D2kMax = 8,
		WorkMinusCurvature = 9,
		QueryMinusCurvature = 10,
		Efficiency = 11
	}

	public readonly struct AdaptiveEnvelopeScaleStats
	{
		public readonly float Min;
		public readonly float Mean;
		public readonly float Max;

		public AdaptiveEnvelopeScaleStats(float min, float mean, float max)
		{
			Min = min;
			Mean = mean;
			Max = max;
		}
	}

	public readonly struct AdaptiveEnvelopeDebugStats
	{
		public readonly float Min;
		public readonly float Mean;
		public readonly float Max;
		public readonly int SampleCount;
		public readonly int TightCount;
		public readonly int WarmCount;
		public readonly int NeutralCount;
		public readonly int RelaxedCount;
		public readonly int HistBin0;
		public readonly int HistBin1;
		public readonly int HistBin2;
		public readonly int HistBin3;
		public readonly int HistBin4;
		public readonly float QueryMinusCurvatureP50;
		public readonly float QueryMinusCurvatureP90;
		public readonly string PriorSource;
		public readonly string PriorFallbackBehavior;
		public readonly bool PriorSnapshotAvailable;
		public readonly int PriorFallbackCount;
		public readonly int PriorSnapshotUnavailableFallbackCount;
		public readonly int PriorInsufficientDataFallbackCount;

		public AdaptiveEnvelopeDebugStats(
			float min,
			float mean,
			float max,
			int sampleCount,
			int tightCount,
			int warmCount,
			int neutralCount,
			int relaxedCount,
			int histBin0,
			int histBin1,
			int histBin2,
			int histBin3,
			int histBin4,
			float queryMinusCurvatureP50,
			float queryMinusCurvatureP90,
			string priorSource,
			string priorFallbackBehavior,
			bool priorSnapshotAvailable,
			int priorFallbackCount,
			int priorSnapshotUnavailableFallbackCount,
			int priorInsufficientDataFallbackCount)
		{
			Min = min;
			Mean = mean;
			Max = max;
			SampleCount = sampleCount;
			TightCount = tightCount;
			WarmCount = warmCount;
			NeutralCount = neutralCount;
			RelaxedCount = relaxedCount;
			HistBin0 = histBin0;
			HistBin1 = histBin1;
			HistBin2 = histBin2;
			HistBin3 = histBin3;
			HistBin4 = histBin4;
			QueryMinusCurvatureP50 = queryMinusCurvatureP50;
			QueryMinusCurvatureP90 = queryMinusCurvatureP90;
			PriorSource = priorSource ?? string.Empty;
			PriorFallbackBehavior = priorFallbackBehavior ?? string.Empty;
			PriorSnapshotAvailable = priorSnapshotAvailable;
			PriorFallbackCount = priorFallbackCount;
			PriorSnapshotUnavailableFallbackCount = priorSnapshotUnavailableFallbackCount;
			PriorInsufficientDataFallbackCount = priorInsufficientDataFallbackCount;
		}
	}

	public readonly struct TelemetryHeatmapStats
	{
		public readonly string Key;
		public readonly float Min;
		public readonly float Max;
		public readonly float Mean;
		public readonly float P10;
		public readonly float P50;
		public readonly float P90;
		public readonly float ClampMax;
		public readonly string Mode;

		public TelemetryHeatmapStats(
			string key,
			float min,
			float max,
			float mean,
			float p10,
			float p50,
			float p90,
			float clampMax,
			string mode)
		{
			Key = key ?? string.Empty;
			Min = min;
			Max = max;
			Mean = mean;
			P10 = p10;
			P50 = p50;
			P90 = p90;
			ClampMax = clampMax;
			Mode = mode ?? "basic";
		}
	}

	public readonly struct RenderHealthDiagnosticsSnapshot
	{
		public readonly int StepIndex;
		public readonly int RowCursorAfter;
		public readonly int RowsAdvanced;
		public readonly int BandsProcessed;
		public readonly long TracedPixels;
		public readonly long GeomSegmentsQueried;
		public readonly long GeomRayTestsTotal;
		public readonly long Pass2SampledSegments;
		public readonly double AvgStepsPerTracedPixel;
		public readonly string BudgetExitReason;
		public readonly bool GeomPruneRequested;
		public readonly bool UseGeometryTLASPruning;

		public RenderHealthDiagnosticsSnapshot(
			int stepIndex,
			int rowCursorAfter,
			int rowsAdvanced,
			int bandsProcessed,
			long tracedPixels,
			long geomSegmentsQueried,
			long geomRayTestsTotal,
			long pass2SampledSegments,
			double avgStepsPerTracedPixel,
			string budgetExitReason,
			bool geomPruneRequested,
			bool useGeometryTLASPruning)
		{
			StepIndex = stepIndex;
			RowCursorAfter = rowCursorAfter;
			RowsAdvanced = rowsAdvanced;
			BandsProcessed = bandsProcessed;
			TracedPixels = tracedPixels;
			GeomSegmentsQueried = geomSegmentsQueried;
			GeomRayTestsTotal = geomRayTestsTotal;
			Pass2SampledSegments = pass2SampledSegments;
			AvgStepsPerTracedPixel = avgStepsPerTracedPixel;
			BudgetExitReason = budgetExitReason ?? string.Empty;
			GeomPruneRequested = geomPruneRequested;
			UseGeometryTLASPruning = useGeometryTLASPruning;
		}
	}

	public readonly struct FixtureWriteDiagnosticsSnapshot
	{
		public readonly int RowsStarted;
		public readonly int RowsCompleted;
		public readonly int RowsPartiallyWritten;
		public readonly int RowsEarlyTerminated;
		public readonly long FinalHitPixelCount;
		public readonly long TraversalWritePixelCount;

		public FixtureWriteDiagnosticsSnapshot(
			int rowsStarted,
			int rowsCompleted,
			int rowsPartiallyWritten,
			int rowsEarlyTerminated,
			long finalHitPixelCount,
			long traversalWritePixelCount)
		{
			RowsStarted = rowsStarted;
			RowsCompleted = rowsCompleted;
			RowsPartiallyWritten = rowsPartiallyWritten;
			RowsEarlyTerminated = rowsEarlyTerminated;
			FinalHitPixelCount = finalHitPixelCount;
			TraversalWritePixelCount = traversalWritePixelCount;
		}
	}

	private static readonly Color FixtureCategoricalFinalHitColor = new(1.0f, 0.82f, 0.18f, 1.0f);
	private static readonly Color FixtureCategoricalRenderedNoHitColor = new(0.07f, 0.09f, 0.18f, 1.0f);

	[ExportGroup("Presets")]

	[ExportSubgroup("Scene Preset")]
	// This section affects algorithm toggles and behavior; it does not touch quality budgets.
	/// <summary>Preset selection for tuning.</summary>
	// CONTROL FACTOR: Performance preset; higher quality increases cost.
	[Export] public PresetMode Preset = PresetMode.Preview;
	/// <summary>Apply the preset automatically in _Ready.</summary>
	// CONTROL FACTOR: Auto-apply preset on startup; true overrides manual tweaks.
	[Export] public bool ApplyPresetOnReady = false;
	/// <summary>Force reapply presets next frame (debug escape hatch).</summary>
	// CONTROL FACTOR: Forces a one-shot preset reapply; auto-clears after use.
	[Export] public bool ForceReapplyPresetsNextFrame = false;

	[ExportSubgroup("Quality Mode")]
	// This section affects quality/perf budgets; it does not change algorithm toggles.
	/// <summary>Quality preset controlling key render budgets/strides.</summary>
	// CONTROL FACTOR: Quality mode preset; overrides key budgets/stride values.
	[Export] public RenderQualityMode QualityMode = RenderQualityMode.Balanced;
	/// <summary>Legacy ordering toggle (kept for compatibility; order is now deterministic).</summary>
	// CONTROL FACTOR: Deprecated ordering switch; presets are now disentangled.
	[Export] public bool UseQualityModePresets = true;

	[ExportSubgroup("Performance Preset")]
	// This section affects algorithm toggles for performance; it does not touch quality budgets.
	/// <summary>Performance preset selection for algorithmic speed tweaks.</summary>
	// CONTROL FACTOR: Performance preset; higher quality increases cost.
	[Export] public PerformancePresetMode PerformancePreset = PerformancePresetMode.None;

	[ExportGroup("Rendering")]

	[ExportSubgroup("Film Output")]
	// This section affects output resolution and sampling density.
	/// <summary>Base film width in pixels before scaling.</summary>
	// CONTROL FACTOR: Base width (pixels); higher increases resolution and cost.
	[Export] public int Width = 160;
	/// <summary>Base film height in pixels before scaling.</summary>
	// CONTROL FACTOR: Base height (pixels); higher increases resolution and cost.
	[Export] public int Height = 90;
	/// <summary>Scales film resolution (0.25 to 1.0).</summary>
	// CONTROL FACTOR: Resolution scale (0.25..1.0); lower reduces cost at the expense of detail.
	[Export(PropertyHint.Range, "0.01,1.0,0.01")] public float FilmResolutionScale = 1.0f;
	/// <summary>Traces every Nth pixel and fills stride-sized blocks.</summary>
	// CONTROL FACTOR: Pixel stride; higher skips pixels and fills blocks for speed (lower fidelity).
	[Export(PropertyHint.Range, "1,8,1")] public int PixelStride = 1;
	/// <summary>Number of film rows rendered per frame.</summary>
	// CONTROL FACTOR: Rows per frame; higher = faster convergence but more per-frame cost.
	[Export] public int RowsPerFrame = 8;

	[ExportSubgroup("Appearance")]
	// This section affects film appearance only (not correctness).
	/// <summary>Background color for no-hit pixels.</summary>
	// CONTROL FACTOR: Background color for miss pixels.
	[Export] public Color SkyColor = new Color(0, 0, 0, 1);
	/// <summary>Opacity applied to the film TextureRect.</summary>
	// CONTROL FACTOR: UI opacity for film display; higher = more opaque.
	[Export] public float FilmOpacity = 0.7f;
	public enum FilmShadingMode
	{
		DepthHeatmap = 0,   // your current behavior
		NormalRGB = 1,      // (N*0.5 + 0.5)
		NdotV = 2,          // grayscale: saturate(dot(N, V))
		TwoSidedNdotV = 3,  // grayscale: saturate(abs(dot(N, V)))
	}

	/// <summary>Film shading mode (depth, normal RGB, NdotV).</summary>
	// CONTROL FACTOR: Shading mode selection; changes how hits map to film color.
	[Export] public FilmShadingMode ShadingMode = FilmShadingMode.DepthHeatmap;
	// Note: overlay normals are world-space collision normals (physics mesh).
	// Film distortion is a visualization artifact and does not change collider geometry.
	// For film-surface normals, use a screen-space gradient (see FilmOverlay2D) or a ray-space curvature normal; physics will not provide it.
	/// <summary>Flips hit normals to face the camera for shading.</summary>
	// CONTROL FACTOR: When true, normals are flipped toward camera; affects NdotV shading.
	[Export] public bool FlipNormalToCamera = true;

	[ExportSubgroup("Telemetry Artifacts")]
	/// <summary>Exports optional film-space telemetry heatmaps for fixture/debug runs.</summary>
	[Export] public bool ExportTelemetryHeatmaps = false;
	/// <summary>Optional output directory override for telemetry heatmaps. Empty defers to the harness capture path.</summary>
	[Export] public string TelemetryHeatmapOutputDir = "";
	/// <summary>Normalization mode for telemetry heatmaps. "basic" uses percentile clamping.</summary>
	[Export] public string TelemetryHeatmapMode = "basic";
	/// <summary>Enables telemetry-driven adaptive envelope scaling for pass-2 candidate gathering.</summary>
	[Export] public bool AdaptiveTelemetryEnvelopeScalingEnabled = false;
	/// <summary>Selects the adaptive controller layout. "three_state" preserves the current reference behavior; "four_state_warm" enables a hot/warm/neutral/relaxed split.</summary>
	[Export] public string AdaptiveEnvelopeControllerMode = "three_state";
	/// <summary>Selects whether adaptive envelope priors come from the same in-progress pass or the previous completed frame/pass snapshot.</summary>
	[Export] public string AdaptiveEnvelopePriorSource = "same_pass";
	/// <summary>Statistic used to trigger adaptive envelope thresholds: mean, p90, or max.</summary>
	[Export] public string AdaptiveEnvelopeThresholdStatistic = "mean";
	/// <summary>Global percentile threshold above which the hot regime activates in four-state mode.</summary>
	[Export(PropertyHint.Range, "50,100,1")] public float AdaptiveEnvelopeHotThresholdPercentile = 95f;
	/// <summary>Global percentile threshold above which the warm regime activates in four-state mode.</summary>
	[Export(PropertyHint.Range, "0,100,1")] public float AdaptiveEnvelopeWarmThresholdPercentile = 80f;
	/// <summary>Global percentile threshold below which the relaxed regime activates in four-state mode.</summary>
	[Export(PropertyHint.Range, "0,100,1")] public float AdaptiveEnvelopeRelaxedThresholdPercentile = 50f;
	/// <summary>Low threshold on the normalized telemetry mismatch signal.</summary>
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float AdaptiveTelemetryEnvelopeLowThreshold = 0.35f;
	/// <summary>High threshold on the normalized telemetry mismatch signal.</summary>
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float AdaptiveTelemetryEnvelopeHighThreshold = 0.65f;
	/// <summary>Envelope radius scale used in the tight adaptive regime.</summary>
	[Export(PropertyHint.Range, "0.1,2.0,0.01")] public float AdaptiveEnvelopeTightScale = 0.70f;
	/// <summary>Envelope radius scale used in the warm adaptive regime.</summary>
	[Export(PropertyHint.Range, "0.1,2.0,0.01")] public float AdaptiveEnvelopeWarmScale = 0.85f;
	/// <summary>Envelope radius scale used in the neutral adaptive regime.</summary>
	[Export(PropertyHint.Range, "0.1,2.0,0.01")] public float AdaptiveEnvelopeNeutralScale = 1.00f;
	/// <summary>Envelope radius scale used in the relaxed adaptive regime.</summary>
	[Export(PropertyHint.Range, "0.1,2.0,0.01")] public float AdaptiveEnvelopeRelaxedScale = 1.05f;

	[ExportSubgroup("Research Mode")]
	// Optional per-camera overrides for research behavior. Inert unless override flags enable fields.
	// Inspector-facing top-level toggle to guarantee visibility even when nested struct expansion is limited.
	[Export]
	public bool ResearchEnabledInspector
	{
		get => ResearchOverrides.Enabled;
		set => ResearchOverrides.Enabled = value;
	}

	public ResearchModeOverrides ResearchOverrides = new ResearchModeOverrides
	{
		Enabled = false
	};

	/// <summary>
	/// Scanline-truth read-only aliases.
	/// These provide canonical film iteration semantics without renaming internal fields.
	/// Intended for telemetry, SmartScale, and harness inspection.
	/// </summary>
	// Public scanline-truth aliases for diagnostics/harnesses. Internal field names stay intact.
	public int FilmRowCursor => _rowCursor;
	public int FilmHeightRows => _filmHeight;
	public int FilmWidthPixels => _filmWidth;
	public int BandHeightRowsResolved => _bandHeightRowsResolved > 0
		? _bandHeightRowsResolved
		: Math.Max(1, _adaptiveRowsPerFrame > 0 ? _adaptiveRowsPerFrame : RowsPerFrame);


	[ExportGroup("Budgets & Watchdogs")]

	[ExportSubgroup("Update Every Frame")]
	// This section affects per-frame workload caps (performance only).
	/// <summary>Runs RenderStep every frame when enabled.</summary>
	// CONTROL FACTOR: Master toggle for per-frame RenderStep; false requires manual stepping.
	[Export] public bool UpdateEveryFrame = true;
	// Backend routing (default Legacy).
	[Export] public RenderBackends.BackendMode BackendMode = RenderBackends.BackendMode.Legacy;
	/// <summary>When UpdateEveryFrame is true, clamp per-call RenderStep budget to this value (ms). <=0 disables the clamp.</summary>
	// CONTROL FACTOR: Per-call RenderStep time budget (ms); lower reduces work per frame.
	[Export] public float UpdateEveryFrameBudgetMs = 16f;
	/// <summary>When UpdateEveryFrame is true, hard-cap RenderStep band height (rows) per call.</summary>
	// CONTROL FACTOR: Per-call row cap when updating every frame; lower spreads work across frames.
	[Export] public int UpdateEveryFrameMaxRowsPerStep = 2;
	/// <summary>When true, stop the scene after the first fully completed film pass.</summary>
	[Export] public bool QuitAfterCompletedPass = false;

	[ExportSubgroup("RenderStep Caps")]
	// This section prevents runaway costs (watchdogs/limits).
	/// <summary>Hard time budget for RenderStep (ms). Exceeding this disables UpdateEveryFrame.</summary>
	// CONTROL FACTOR: Hard ceiling (ms); exceeding disables UpdateEveryFrame to prevent stalls.
	[Export] public int RenderStepMaxMs = 50;
	/// <summary>Hard cap on RenderStep pixel workload per frame. 0 disables.</summary>
	// CONTROL FACTOR: Hard pixel cap per frame; lower reduces CPU cost.
	[Export] public int RenderStepMaxPixelsPerFrame = 2000000;
	/// <summary>Hard cap on RenderStep segments per frame. 0 disables.</summary>
	// CONTROL FACTOR: Hard segment cap per frame; lower reduces collision workload.
	[Export] public int RenderStepMaxSegmentsPerFrame = 20000000;
	/// <summary>Consecutive steps with processed pixels but no row advance before forcing advance.</summary>
	// CONTROL FACTOR: No-row-progress watchdog repeat limit; lower forces row advance sooner.
	[Export] public int RenderStepNoRowProgressRepeatLimit = 6;

	[ExportSubgroup("Adaptive Rows")]
	// This section affects adaptive row sizing (performance only).
	/// <summary>Target CPU time budget per RenderStep (ms). Set <=0 to disable adaptive rows.</summary>
	// CONTROL FACTOR: Target budget (ms) for adaptive rows; lower reduces work.
	[Export] public int TargetMsPerFrame = 16;
	/// <summary>Minimum rows per frame when adaptive rows are enabled.</summary>
	// CONTROL FACTOR: Minimum rows per frame under adaptive mode; higher keeps throughput up.
	[Export] public int MinRowsPerFrame = 4;
	/// <summary>Maximum rows per frame when adaptive rows are enabled.</summary>
	// CONTROL FACTOR: Maximum rows per frame under adaptive mode; higher allows bigger bursts.
	[Export] public int MaxRowsPerFrameCap = 256;

	[ExportGroup("SmartScale")]
	[Export] public bool SmartScaleEnabled = false;
	[Export] public bool SmartScaleRunOnReady = false;
	[Export] public SmartScaleGoalMode SmartScaleGoal = SmartScaleGoalMode.MaxHits;
	[Export] public SmartScaleProbeBudgetMode SmartScaleBudgetMode = SmartScaleProbeBudgetMode.RowsAdvanced;
	[Export(PropertyHint.Range, "1,1000000,1")] public int SmartScaleBudgetN = 512;
	private bool _smartScaleRunOnceLatch = false;
	[Export]
	public bool SmartScaleRunOnce
	{
		get => _smartScaleRunOnceLatch;
		set
		{
			_smartScaleRunOnceLatch = false;
			if (!value) return;
			_smartScaleRunRequested = true;
			GD.Print("[SmartScale][Inspector] run-once requested.");
		}
	}

	[ExportGroup("Runtime Macro Modes")]
	/// <summary>Enables interactive runtime macro mode switching for live experimentation.</summary>
	[Export] public bool RuntimeMacroModeSwitchingEnabled = true;
	/// <summary>Applies the selected runtime macro mode on startup. Off by default so existing scene/test defaults stay unchanged.</summary>
	[Export] public bool RuntimeMacroApplyOnReady = false;
	/// <summary>Shows runtime macro mode state in the on-screen HUD when active in interactive mode.</summary>
	[Export] public bool RuntimeMacroShowHudStatus = true;
	/// <summary>Allows keyboard hotkeys for runtime macro mode switching.</summary>
	[Export] public bool RuntimeMacroHotkeysEnabled = true;
	/// <summary>Selected runtime macro mode for hotkeys and optional startup apply.</summary>
	[Export] public RuntimeMacroMode RuntimeMacroSelectedMode = RuntimeMacroMode.Tight08;

	[ExportGroup("Profiling")]
	[ExportSubgroup("Runtime Stats")]
	// This section affects profiling counters and sampling only.
	/// <summary>Enables perf stats collection.</summary>
	// CONTROL FACTOR: Enables perf stats; true adds some overhead.
	[Export] public bool EnableProfiling = true;
	/// <summary>Prints verbose perf logs per band.</summary>
	// CONTROL FACTOR: Verbose perf logging; higher log volume.
	[Export] public bool VerbosePerfLogs = false;
	/// <summary>Enables FramePerf stage timing and counters.</summary>
	// CONTROL FACTOR: Enables frame performance tracking.
	[Export] public bool EnableFramePerf = true;
	/// <summary>Prints FramePerf every frame when enabled.</summary>
	// CONTROL FACTOR: Verbose per-frame perf logging.
	[Export] public bool FramePerfVerbose = false;
	/// <summary>Frames between FramePerf logs when not verbose.</summary>
	// CONTROL FACTOR: Log cadence in frames when not verbose.
	[Export] public int FramePerfLogEveryNFrames = 30;

	[ExportSubgroup("Threading")]
	/// <summary>Experimental bounded multithreading pass for pass1 transport using scheduler-aligned row chunks.</summary>
	[Export] public bool UseThreadedBands = false;
	/// <summary>Worker count for the threaded pass1 row-chunk mode. Set to 1 for deterministic single-worker comparison.</summary>
	[Export(PropertyHint.Range, "1,16,1")] public int ThreadedBandWorkerCount = 2;
	/// <summary>Rows per pass1 work chunk when threaded bands are enabled.</summary>
	[Export(PropertyHint.Range, "1,256,1")] public int ThreadedBandRowsPerChunk = 4;
	/// <summary>Experimental pass2 candidate-eval prepass: threaded TLAS candidate evaluation, serialized Godot queries and final commit.</summary>
	[Export] public bool UseThreadedPass2CandidateEval = false;
	/// <summary>Worker count for the pass2 candidate-eval prepass.</summary>
	[Export(PropertyHint.Range, "1,16,1")] public int ThreadedPass2CandidateWorkers = 2;
	/// <summary>Rows per pass2 candidate-eval chunk.</summary>
	[Export(PropertyHint.Range, "1,256,1")] public int ThreadedPass2CandidateRowsPerChunk = 4;
	/// <summary>Experimental pass2 query/resolve ownership prototype: worker-owned row chunks run local overlap + hit queries, serialized final commit.</summary>
	[Export] public bool UseThreadedPass2QueryResolve = false;
	/// <summary>Worker count for the pass2 query/resolve ownership prototype.</summary>
	[Export(PropertyHint.Range, "1,16,1")] public int ThreadedPass2QueryWorkers = 2;
	/// <summary>Rows per pass2 query/resolve worker chunk.</summary>
	[Export(PropertyHint.Range, "1,256,1")] public int ThreadedPass2QueryRowsPerChunk = 4;
	/// <summary>Experimental pass2 local-accumulation scaffold: serial physics gather, worker-local shading payloads, serialized commit.</summary>
	[Export] public bool UseThreadedPass2LocalAccumulation = false;
	/// <summary>Worker count for the pass2 local-accumulation scaffold.</summary>
	[Export(PropertyHint.Range, "1,16,1")] public int ThreadedPass2WorkerCount = 2;
	/// <summary>Rows per pass2 local-accumulation chunk.</summary>
	[Export(PropertyHint.Range, "1,256,1")] public int ThreadedPass2RowsPerChunk = 4;

	[ExportSubgroup("Logging & Diagnostics")]
	/// <summary>Fetches collider names for debug output.</summary>
	// CONTROL FACTOR: Fetch collider names; true adds lookup cost but improves debug readability.
	[Export] public bool NeedColliderNames = false;
	/// <summary>Fixture-scoped debug coloring for hit classes (source/background/absorbed).</summary>
	// CONTROL FACTOR: Off by default; enable only for fixture/test visualization.
	[Export] public bool FixtureDebugHitColoringEnabled = false;
	/// <summary>Group name used to classify source hits for fixture debug coloring.</summary>
	// CONTROL FACTOR: Nodes in this group are treated as source hits.
	[Export] public string FixtureDebugSourceGroup = "fixture_source";
	/// <summary>Optional group name used to classify fixture detector/background hits explicitly.</summary>
	// CONTROL FACTOR: When populated, non-source hits outside this group are reported as unclassified.
	[Export] public string FixtureDebugBackgroundGroup = "fixture_background";
	/// <summary>Color for fixture source hits when fixture debug coloring is enabled.</summary>
	[Export] public Color FixtureDebugSourceHitColor = new Color(1f, 1f, 0.9f, 1f);
	/// <summary>Color for fixture detector/background hits when fixture debug coloring is enabled.</summary>
	[Export] public Color FixtureDebugBackgroundHitColor = new Color(0.16f, 0.24f, 0.40f, 1f);
	/// <summary>Color for absorbed rays when fixture debug coloring is enabled.</summary>
	[Export] public Color FixtureDebugAbsorbedColor = new Color(0f, 0f, 0f, 1f);
	/// <summary>Color for miss pixels when fixture debug coloring is enabled.</summary>
	[Export] public Color FixtureDebugMissColor = new Color(0.06f, 0.07f, 0.10f, 1f);
	/// <summary>When enabled, fixture debug hit colors remain the final authority for this render mode.</summary>
	[Export] public bool FixtureDebugColorAuthorityEnabled = false;
	/// <summary>When enabled, source hits use the explicit source highlight color.</summary>
	[Export] public bool FixtureDebugSourceHighlightEnabled = true;
	/// <summary>Logs sampled fixture hit classifications and final film colors.</summary>
	[Export] public bool FixtureDebugTraceEnabled = false;
	/// <summary>Modulo used for deterministic fixture debug trace sampling.</summary>
	[Export(PropertyHint.Range, "1,997,1")] public int FixtureDebugTraceSampleModulo = 43;
	/// <summary>Maximum fixture debug trace logs emitted per RenderStep call.</summary>
	[Export(PropertyHint.Range, "0,256,1")] public int FixtureDebugTraceMaxLogsPerStep = 12;
	/// <summary>Caches field source snapshots for faster updates.</summary>
	// CONTROL FACTOR: Cache field sources; true reduces per-frame scanning but may lag changes.
	[Export] public bool UseFieldSourceCache = false;
	/// <summary>How often to refresh cached field sources.</summary>
	// CONTROL FACTOR: Refresh interval in frames; higher = less overhead but more staleness.
	[Export] public int FieldSourceRefreshIntervalFrames = 30;
	/// <summary>Enables RenderStep Phase Logging.</summary>
	// CONTROL FACTOR: Enables Phase by Phase updates in console log.
	[Export] public bool RenderStepPhaseLog = true;
	/// <summary>Enables RenderStep Band by Band Logging.</summary>
	// CONTROL FACTOR: Enables Band by Band Logging each RenderStep in console log.
	[Export] public bool RenderStepBandLog = true;

	[ExportGroup("Debug Logs")]
	[Export] public bool DebugSnapshotLog = true;
	[Export(PropertyHint.Range, "0.05,10.0,0.05")] public float DebugSnapshotIntervalSec = 1.0f;
	[Export] public bool DebugProbeLog = true;
	[Export(PropertyHint.Range, "0.05,10.0,0.05")] public float DebugProbeIntervalSec = 1.0f;
	[Export] public bool DebugGeomRejectSampleEnabled = false;
	[Export(PropertyHint.Range, "1,10000,1")] public int DebugGeomRejectSampleEveryN = 200;
	[Export] public bool DebugGeomCounterGuardEnabled = false;
	[Export(PropertyHint.Range, "1,100000,1")] public int GeomHitRejectWarnThresholdPerWindow = 128;
	[Export] public bool DebugGeomPruneAuditEnabled = false;
	[Export(PropertyHint.Range, "0,4096,1")] public int DebugGeomPruneAuditSamplesPerHealthWindow = 256;
	[Export(PropertyHint.Range, "1,1024,1")] public int DebugGeomPruneAuditMaxExtraRayTestsPerSample = 128;
	[Export] public bool DebugGeomPruneAuditOnlyWhenCandidateZero = true;
	[Export(PropertyHint.Range, "0,64,1")] public int DebugGeomPruneAuditMaxMismatchLogsPerWindow = 3;
	/// <summary>Experimental instrumentation scaffold for future tile-aware scheduling; logs fixed horizontal subtiles within each band.</summary>
	[Export] public bool EnableTileMetricsScaffold = false;
	/// <summary>Fixed horizontal subtile width used by tile metrics when the scaffold is enabled.</summary>
	[Export(PropertyHint.Range, "1,4096,1")] public int TileMetricsSubtileWidth = 64;
	/// <summary>Max per-tile metric logs emitted per frame when the tile scaffold is enabled.</summary>
	[Export(PropertyHint.Range, "0,256,1")] public int TileMetricsMaxLogsPerFrame = 16;
	/// <summary>Observe-only simulated reorder over subtiles; computes priority order but does not change execution.</summary>
	[Export] public bool EnableTileMetricsReorderSimulation = false;
	/// <summary>Official experimental scheduler mode: reorder-only subtile execution within each band, with no work reduction.</summary>
	[Export] public bool EnableTileMetricsReorderExecution = false;
	/// <summary>Cautious additive memory for reorder-only scheduling; blends decayed per-subtile priors into the existing ranking path.</summary>
	[Export] public bool EnableTileMetricsPersistentPriors = false;


	[ExportGroup("Ray March")]

	[ExportSubgroup("Range & Auto Depth")]
	// This section affects ray range and depth auto-scaling (correctness + performance).
	/// <summary>Max ray distance when auto-range is disabled.</summary>
	// CONTROL FACTOR: Max ray distance (world units) when AutoRangeDepth is off.
	[Export] public float MaxDistance = 50f;
	/// <summary>Auto-adjusts depth range based on recent hits.</summary>
	// CONTROL FACTOR: Enables auto-range; true adapts far distance to recent hits.
	[Export] public bool AutoRangeDepth = true;
	/// <summary>Minimum allowed auto-range far distance.</summary>
	// CONTROL FACTOR: Minimum far distance (world units) under auto-range.
	[Export] public float AutoRangeMin = 0.25f;
	/// <summary>Maximum allowed auto-range far distance.</summary>
	// CONTROL FACTOR: Maximum far distance (world units) under auto-range.
	[Export] public float AutoRangeMax = 200f;
	/// <summary>Lerp factor for auto-range updates.</summary>
	// CONTROL FACTOR: Smoothing factor; higher reacts faster to changes.
	[Export] public float AutoRangeSmoothing = 0.15f;
	/// <summary>Safety multiplier for robust far estimate.</summary>
	// CONTROL FACTOR: Safety multiplier; higher increases far distance buffer.
	[Export] public float AutoRangeSafety = 1.15f;
	/// <summary>Frames tracked for robust far estimate.</summary>
	// CONTROL FACTOR: Depth history window size (frames); larger smooths more.
	[Export] public int DepthHistoryFrames = 30;

	[ExportSubgroup("Field Grid")]
	// This section affects pass-1 sampling strategy (performance/correctness tradeoff).
	/// <summary>Uses a cached 3D vector field grid for pass-1 sampling.</summary>
	// CONTROL FACTOR: Enables field grid; true trades memory for speed.
	[Export] public bool UseFieldGrid = false;
	/// <summary>Cell size for field grid sampling.</summary>
	// CONTROL FACTOR: Grid cell size (world units); smaller = more accurate but more memory.
	[Export] public float FieldGridCellSize = 0.25f;
	/// <summary>Rebuild the field grid every N frames.</summary>
	// CONTROL FACTOR: Grid rebuild cadence; higher = less overhead but more staleness.
	[Export] public int FieldGridRebuildEveryNFrames = 8;
	/// <summary>Padding added to far distance for grid bounds.</summary>
	// CONTROL FACTOR: Extra padding (world units) for grid bounds; higher covers more space at cost of memory.
	[Export] public float FieldGridBoundsPadding = 5f;

	[ExportSubgroup("Curvature Grid")]
	// This section affects curvature bound lookup (performance/correctness tradeoff).
	/// <summary>Cell size for curvature bound grid.</summary>
	// CONTROL FACTOR: Grid cell size (world units); smaller = more accurate but more memory.
	[Export] public float CurvatureGridCellSize = 1.0f;
	/// <summary>Curvature grid X dimension (cells).</summary>
	// CONTROL FACTOR: Grid dimension; higher covers more space at cost of memory.
	[Export] public int CurvatureGridDimX = 32;
	/// <summary>Curvature grid Y dimension (cells).</summary>
	// CONTROL FACTOR: Grid dimension; higher covers more space at cost of memory.
	[Export] public int CurvatureGridDimY = 16;
	/// <summary>Curvature grid Z dimension (cells).</summary>
	// CONTROL FACTOR: Grid dimension; higher covers more space at cost of memory.
	[Export] public int CurvatureGridDimZ = 32;

	[ExportSubgroup("Sampling & Probes")]
	// This section affects ray marching behavior and sampling correctness.
	/// <summary>Reads Beta/Gamma from the active Camera3D.</summary>
	// CONTROL FACTOR: When true, uses camera Beta/Gamma; false uses film defaults.
	[Export] public bool UseCameraPropsBetaGamma = true;
	/// <summary>Skips collision checks for tiny segments.</summary>
	// CONTROL FACTOR: Segment length threshold (world units) below which collisions are skipped.
	[Export] public float TinySegmentSkipLen = 0.0f;
	/// <summary>Early-out distance for nearest-hit search.</summary>
	// CONTROL FACTOR: Early-out epsilon (world units); higher exits sooner, possibly missing closer hits.
	[Export] public float EarlyOutDistanceEps = 0.0f;
	/// <summary>Refines collision checks by subdividing segments.</summary>
	// CONTROL FACTOR: Enables adaptive substeps; true increases accuracy at cost.
	[Export] public bool UseAdaptiveSubsteps = false;
	/// <summary>Skips physics for low-hit bands.</summary>
	// CONTROL FACTOR: Enables band-level hit skip; true reduces cost when bands rarely hit.
	[Export] public bool UseBandHitSkip = false;
	/// <summary>Hit rate threshold to enable skipping.</summary>
	// CONTROL FACTOR: Hit-rate threshold; lower = more likely to skip.
	[Export] public float BandSkipHitThreshold = 0.001f;
	/// <summary>Frames below threshold before skipping.</summary>
	// CONTROL FACTOR: Frames below threshold before skipping; higher reduces flapping.
	[Export] public int BandSkipFrames = 3;
	/// <summary>Position delta that invalidates band skip history.</summary>
	// CONTROL FACTOR: Position delta (world units) that resets skip history.
	[Export] public float BandSkipInvalidatePosDelta = 0.05f;
	/// <summary>Basis delta that invalidates band skip history.</summary>
	// CONTROL FACTOR: Basis delta (radians-ish) that resets skip history.
	[Export] public float BandSkipInvalidateBasisDelta = 0.02f;
	/// <summary>Range delta that invalidates band skip history.</summary>
	// CONTROL FACTOR: Range delta (world units) that resets skip history.
	[Export] public float BandSkipInvalidateRangeDelta = 0.25f;
	/// <summary>Enables pass-1 hit tests.</summary>
	// CONTROL FACTOR: Enables pass-1 hit probes; true increases accuracy but adds work.
	[Export] public bool Pass1DoHitTest = true;
	/// <summary>Runs a pass-1 probe every N steps (0 disables; independent of segment emission cadence).</summary>
	// CONTROL FACTOR: Probe cadence in steps; higher = fewer probes.
	[Export] public int Pass1ProbeEveryNSegments = 4;
	/// <summary>Minimum travel distance between pass-1 probes (<=0 disables).</summary>
	// CONTROL FACTOR: Probe travel distance (world units); higher = fewer probes.
	[Export] public float Pass1ProbeMinTravelDelta = 0.25f;


	[ExportGroup("Physics / Collision")]
	[ExportSubgroup("Broadphase / Mode")]
	// Broadphase precedence: Mode selects the single source of truth.
	// Off: disables quick-ray + overlap.
	// Manual: uses manual toggles below.
	// Policy: uses BroadphasePolicy dropdown.
	// Auto: uses heuristic (see Render Health) to choose policy.
	// This section affects collision policy switches (behavior).
	/// <summary>Broadphase mode (single source of truth).</summary>
	// CONTROL FACTOR: Mode that decides where broadphase settings come from.
	[Export] public BroadphaseMode BroadphaseControlMode = BroadphaseMode.Policy;

	[ExportSubgroup("Broadphase / Policy Settings")]
	// This section affects collision policy switches (behavior).
	/// <summary>Broadphase Policy (used when Mode = Policy or Auto).</summary>
	// CONTROL FACTOR: Broadphase policy selection.
	[Export] public BroadphasePolicyMode BroadphasePolicy = BroadphasePolicyMode.None;

	[ExportSubgroup("Broadphase / Legacy (Read-Only)")]
	/// <summary>Legacy: UseBroadphasePolicy (deprecated).</summary>
	// CONTROL FACTOR: Deprecated; mirrored from BroadphaseMode for backwards compatibility.
	[Export] [Obsolete("Deprecated: use BroadphaseMode.")]
	public bool UseBroadphasePolicy = false;

	[ExportSubgroup("Broadphase / Manual Overrides")]
	// Manual toggles are only authoritative when Mode = Manual; otherwise they reflect effective state.
	// This section affects collision culling (performance only).
	/// <summary>Quick Ray (effective; read-only unless Mode = Manual). Only used when BroadphaseMode=Manual.</summary>
	// CONTROL FACTOR: Enables quick-ray broadphase; true reduces work by early rejection.
	[Export] public bool UseBroadphaseQuickRay = false;
	/// <summary>Overlap (effective; read-only unless Mode = Manual). Only used when BroadphaseMode=Manual.</summary>
	// CONTROL FACTOR: Enables overlap broadphase; true adds extra culling based on radius.
	[Export] public bool UseBroadphaseOverlap = false;

	[ExportSubgroup("Broadphase / Policy Settings")]
	/// <summary>Extra radius for overlap broadphase.</summary>
	// CONTROL FACTOR: Overlap margin (world units); higher catches more but costs more.
	[Export] public float BroadphaseMargin = 0.03f;
	/// <summary>Max overlap results to consider.</summary>
	// CONTROL FACTOR: Cap on overlap results; higher may increase cost.
	[Export] public int BroadphaseMaxResults = 8;

	[ExportSubgroup("Broadphase / Auto Heuristics")]
	/// <summary>Render-health window size used by Auto broadphase policy.</summary>
	// CONTROL FACTOR: Window size for auto policy decisions; higher smooths more.
	[Export] public int AutoBroadphaseWindow = 6;
	/// <summary>Cooldown steps after switching Auto policy.</summary>
	// CONTROL FACTOR: Cooldown duration; higher reduces flip-flopping.
	[Export] public int AutoBroadphaseCooldownSteps = 30;
	/// <summary>Minimum traced pixels required to consider auto policy flip.</summary>
	// CONTROL FACTOR: Low-trace guard; higher ignores low-signal frames.
	[Export] public int AutoBroadphaseMinTracedPixels = 5000;
	/// <summary>Low hit-rate threshold for auto policy flip.</summary>
	// CONTROL FACTOR: Lower values make flips rarer.
	[Export] public float AutoBroadphaseLowHitRate = 0.0025f;
	/// <summary>Hit-rate variance threshold for auto policy flip.</summary>
	// CONTROL FACTOR: Higher values make flips rarer.
	[Export] public float AutoBroadphaseVarianceThreshold = 0.0004f;

	[ExportSubgroup("Broadphase / Effective State")]
	/// <summary>Effective broadphase mode (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror of resolved mode.
	[Export] public BroadphaseMode EffectiveBroadphaseMode = BroadphaseMode.Manual;
	/// <summary>Effective broadphase policy (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror of resolved policy.
	[Export] public BroadphasePolicyMode EffectiveBroadphasePolicy = BroadphasePolicyMode.None;
	/// <summary>Effective Quick Ray toggle (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror of resolved Quick Ray.
	[Export] public bool EffectiveBroadphaseQuickRay = false;
	/// <summary>Effective Overlap toggle (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror of resolved Overlap.
	[Export] public bool EffectiveBroadphaseOverlap = false;
	/// <summary>Effective broadphase reason tag (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror of resolved source tag.
	[Export] public string EffectiveBroadphaseReason = "";

	[ExportSubgroup("Stride")]
	// This section affects collision sampling density (performance/correctness tradeoff).
	/// <summary>Skips some pass-2 collision checks based on distance.</summary>
	// CONTROL FACTOR: Enables distance-based collision stride in pass 2.
	[Export] public bool UsePass2CollisionStride = false;
	/// <summary>Stride near the camera for pass-2 collision checks.</summary>
	// CONTROL FACTOR: Collision stride near camera; higher skips more checks close-up.
	[Export(PropertyHint.Range, "1,8,1")] public int Pass2CollisionStrideNear = 1;
	/// <summary>Stride at far distances for pass-2 collision checks.</summary>
	// CONTROL FACTOR: Collision stride far away; higher skips more checks in distance.
	[Export(PropertyHint.Range, "1,32,1")] public int Pass2CollisionStrideFar = 4;
	/// <summary>Start t (0..1) where far stride begins in pass 2.</summary>
	// CONTROL FACTOR: Transition point (0..1 of ray length) to far stride.
	[Export(PropertyHint.Range, "0,1,0.01")] public float Pass2CollisionStrideFarStartT = 0.35f;
	/// <summary>If >0, segments shorter than this length always run pass-2 collision tests.</summary>
	// CONTROL FACTOR: Minimum segment length (world units) for stride skipping; lower = more checks.
	[Export(PropertyHint.Range, "0,1,0.001")] public float MinSegLenForStrideSkip = 0f;
	/// <summary>Multiplier applied to pass-2 geometry envelope radius before TLAS query.</summary>
	// CONTROL FACTOR: Higher values make candidate gathering more conservative (fewer false rejects, more candidates).
	[Export(PropertyHint.Range, "0.1,2.0,0.01")] public float Pass2GeomEnvelopeRadiusScale = 1.10f;
	/// <summary>Additional axis-aligned expansion applied to pass-2 geometry envelope after radius expansion.</summary>
	// CONTROL FACTOR: Higher values increase TLAS query conservativeness independently of segment radius.
	[Export(PropertyHint.Range, "0.0,2.0,0.01")] public float Pass2GeomEnvelopeAabbExpand = 0.0f;
	/// <summary>Enables GeometryTLAS candidate pruning in pass-2 narrowphase.</summary>
	// CONTROL FACTOR: Disable for A/B comparisons; when off, pass-2 skips TLAS candidate filtering.
	[Export] public bool UseGeometryTLASPruning = true;

	[ExportSubgroup("Hit Flags")]
	// This section affects collision hit rules and logging.
	/// <summary>Ray query option: include back-facing triangles in pass-2 checks.</summary>
	// CONTROL FACTOR: Include backfaces in pass-2 raycasts; true increases hits but can add noise.
	[Export] public bool Pass2HitBackFaces = false;
	/// <summary>Ray query option: detect hits when starting inside colliders.</summary>
	// CONTROL FACTOR: Allow hits from inside; true detects interior starts.
	[Export] public bool Pass2HitFromInside = true;
	/// <summary>Forces a representative subdivided test when quick-ray misses all candidate segments.</summary>
	// CONTROL FACTOR: Forces subdivided test on instability; increases accuracy at cost.
	[Export] public bool Pass2ForceOnInstability = false;
	/// <summary>Only forces instability tests when the pixel hit in the previous frame.</summary>
	// CONTROL FACTOR: Limit forced instability tests to previously hit pixels.
	[Export] public bool Pass2ForceIfPrevHitLost = false;
	/// <summary>Logs quick-ray misses that later subdivide and hit (per frame).</summary>
	// CONTROL FACTOR: Log sample count for quick-ray misses; higher logs more diagnostics.
	[Export] public int Pass2LogQuickRayMissSamples = 0;
	public enum BroadphaseMode
	{
		Off = 0,
		Manual = 1,
		Policy = 2,
		Auto = 3
	}

	public enum BroadphasePolicyMode
	{
		None = 0,
		QuickRayOnly = 1,
		OverlapOnly = 2,
		Both = 3,
		HybridQuickRayThenOverlap = 4
	}

	/// <summary>Uses a quick probe, then subdivides if needed.</summary>
	// CONTROL FACTOR: Enables quick probe then subdivide; true favors early-outs.
	[Export] public bool UseSingleProbeThenSubdivide = false;
	/// <summary>If true, keeps scanning segments for the nearest hit.</summary>
	// CONTROL FACTOR: Nearest-hit search; true prioritizes closest hit over first hit.
	[Export] public bool NearestHitOnly = false;

#region Pass2 SoftGate
	[ExportGroup("Soft Gate")]
	[ExportSubgroup("Core")]
	// This section affects core SoftGate behavior (correctness/performance tradeoff).
	/// <summary>Allows occasional subdivide attempts on quick-ray misses (Pass2).</summary>
	// CONTROL FACTOR: Enables soft-gated subdivide probes on quick-ray misses; true increases accuracy at some cost.
	[Export] public bool Pass2SoftGateEnableQuickRayMiss = false;
	/// <summary>Disable SoftGate for the rest of the frame when overload is detected.</summary>
	// CONTROL FACTOR: When true, SoftGate shuts off mid-frame under overload; prevents long stalls but may reduce hits.
	[Export] public bool DisableSoftGateOnOverload = true;

	[ExportSubgroup("Budgets")]
	// This section affects SoftGate workload caps (performance only).
	/// <summary>Max soft-gate attempts per pixel (Pass2). 0 disables.</summary>
	// CONTROL FACTOR: Per-pixel SoftGate attempt cap; higher increases accuracy but can cost CPU.
	[Export(PropertyHint.Range, "0,8,1")] public int Pass2SoftGateMaxAttemptsPerPixel = 2;
	/// <summary>Max soft-gate attempts per frame (Pass2). 0 disables; raise only when profiling.</summary>
	// CONTROL FACTOR: Per-frame SoftGate attempt cap; higher allows more probes but risks frame time spikes.
	[Export(PropertyHint.Range, "0,100000,1")] public int Pass2SoftGateMaxAttemptsPerFrame = 5000;
	/// <summary>Auto-scaled max soft-gate attempts per frame lower bound when using RayBeam settings.</summary>
	// CONTROL FACTOR: Lower bound for auto-scaled per-frame attempts; higher raises baseline workload.
	[Export(PropertyHint.Range, "0,100000,1")] public int Pass2SoftGateMaxAttemptsPerFrameMin = 20;
	/// <summary>Auto-scaled max soft-gate attempts per frame upper bound when using RayBeam settings.</summary>
	// CONTROL FACTOR: Upper bound for auto-scaled per-frame attempts; higher allows more probes under heavy rays.
	[Export(PropertyHint.Range, "0,100000,1")] public int Pass2SoftGateMaxAttemptsPerFrameMax = 5000;
	/// <summary>Max soft-gated subdivided calls per frame (Pass2). 0 disables; higher values can stall frames.</summary>
	// CONTROL FACTOR: Per-frame cap on subdivided collision tests; higher increases accuracy but can stall.
	[Export(PropertyHint.Range, "0,200000,1")] public int Pass2SoftGateMaxSubdividedCallsPerFrame = 10000;
	/// <summary>Auto-scaled max soft-gated subdivided calls per frame lower bound when using RayBeam settings.</summary>
	// CONTROL FACTOR: Lower bound for auto-scaled subdivide calls; higher raises baseline work.
	[Export(PropertyHint.Range, "0,200000,1")] public int Pass2SoftGateMaxSubdividedCallsPerFrameMin = 50;
	/// <summary>Auto-scaled max soft-gated subdivided calls per frame upper bound when using RayBeam settings.</summary>
	// CONTROL FACTOR: Upper bound for auto-scaled subdivide calls; higher allows more heavy probes.
	[Export(PropertyHint.Range, "0,200000,1")] public int Pass2SoftGateMaxSubdividedCallsPerFrameMax = 10000;
	/// <summary>Watchdog timeout (ms) for a single soft-gated subdivide (Pass2). 0 disables.</summary>
	// CONTROL FACTOR: Watchdog time (ms) per subdivide; lower aborts quicker, higher allows deeper work.
	[Export(PropertyHint.Range, "0,5000,0.1")] public float Pass2SoftGateWatchdogMs = 50f;
	/// <summary>Max watchdog logs per frame when Pass2SoftGateDebugEnabled is enabled.</summary>
	// CONTROL FACTOR: Cap on watchdog log spam per frame.
	[Export(PropertyHint.Range, "0,32,1")] public int Pass2SoftGateWatchdogLogLimitPerFrame = 4;

	[ExportSubgroup("Scoring")]
	// This section affects SoftGate scoring behavior (correctness/performance tradeoff).
	/// <summary>Legacy cadence gate for soft-gated subdivides (Pass2). Unused.</summary>
	[Obsolete("Legacy soft-gate cadence (unused). Use Pass2SoftGateScoreThreshold + scoring model instead.")]
	public int Pass2SoftGateLegacyEveryNSegments = 8;
	/// <summary>Legacy length gate for soft-gated subdivides (Pass2). Unused.</summary>
	[Obsolete("Legacy soft-gate min segment length (unused). Use Pass2SoftGateMinSegmentLength instead.")]
	public float Pass2SoftGateLegacyMinSegmentLength = 0f;

	/// <summary>Enable scoring-based soft-gate (Pass2).</summary>
	// CONTROL FACTOR: Enables score-based SoftGate selection; true increases selectivity vs brute-force.
	[Export] public bool Pass2SoftGateScoringEnabled = true;
	/// <summary>Maximum scoring soft-gate attempts allowed per frame (Pass2).</summary>
	// CONTROL FACTOR: Per-frame budget for score-based attempts; higher allows more probes.
	[Export] public int Pass2SoftGateScoreBudgetPerFrame = 32;
	/// <summary>Minimum segment length eligible for scoring soft-gate (Pass2).</summary>
	// CONTROL FACTOR: Minimum segment length (world units) to score; higher skips short segments.
	[Export] public float Pass2SoftGateMinSegmentLength = 0.2f;
	/// <summary>Score threshold required to trigger scoring soft-gate (Pass2). Adjust only with debug summaries.</summary>
	// CONTROL FACTOR: Score threshold; higher means fewer probes.
	[Export] public float Pass2SoftGateScoreThreshold = 1.0f;
	/// <summary>Weight for turn-angle contribution (scaled by 0..180 deg).</summary>
	// CONTROL FACTOR: Weight of turn-angle in score; higher favors curved segments.
	[Export] public float Pass2SoftGateScoreTurnAngleWeight = 1.0f;
	/// <summary>Extra score added when a previous-frame hit was lost.</summary>
	// CONTROL FACTOR: Bonus when previous hit lost; higher makes re-probe more aggressive.
	[Export] public float Pass2SoftGateScorePrevHitLostBonus = 0.75f;
	/// <summary>Random chance to probe even when score is below threshold.</summary>
	// CONTROL FACTOR: Random probe chance; higher adds more exploratory probes.
	[Export] public float Pass2SoftGateRandomProbeChance = 0.01f;

	[ExportSubgroup("Debug")]
	// This section affects SoftGate debugging only.
	/// <summary>Enables soft-gate debug counters and logging (Pass2).</summary>
	// CONTROL FACTOR: Enables SoftGate debug counters/logs; true adds overhead and logs.
	[Export] public bool Pass2SoftGateDebugEnabled = true;
	/// <summary>SoftGate debug verbosity (0=off, 1=frame, 2=band, 3=sampled segments).</summary>
	// CONTROL FACTOR: Debug verbosity level; higher emits more detailed logs.
	[Export(PropertyHint.Range, "0,3,1")] public int Pass2SoftGateDebugVerbosity = 1;
	/// <summary>Prints a compact debug summary per frame (Pass2).</summary>
	// CONTROL FACTOR: Enables per-frame summary printouts.
	[Export] public bool Pass2SoftGateDebugSummaryPerFrame = false;
	/// <summary>Max debug summary logs per frame when enabled.</summary>
	// CONTROL FACTOR: Cap on per-frame summary logs.
	[Export(PropertyHint.Range, "0,8,1")] public int Pass2SoftGateDebugSummaryLogLimitPerFrame = 1;
	// Toggle for extended rolling performance suffix on the on-screen RenderHealth HUD.
	[Export] public bool DebugRenderHealthRollingOverlay = true;
	[Export(PropertyHint.Range, "0.5,2.0,0.1")] public float RenderHealthRollingWindowSec = 1.0f;
	[Export(PropertyHint.Range, "0.6,1.0,0.05")] public float HudOverlayFontScale = 1.0f;
#endregion


	[ExportGroup("Debug Visualization")]

	[ExportSubgroup("Overlay Rays")]
	// This section affects debug overlays only (performance only).
	/// <summary>Debug ray sampling density for overlay.</summary>
	// CONTROL FACTOR: Debug ray stride; higher samples fewer rays.
	[Export] public int DebugEveryNPixels = 8;
	/// <summary>Cap on debug rays per band.</summary>
	// CONTROL FACTOR: Debug ray cap per band; limits overlay workload.
	[Export] public int DebugMaxFilmRays = 2048;

	[ExportSubgroup("Deprecated (No Effect)")]
	// This section is legacy and has no effect.
	/// <summary>Legacy pass-2 insight plane toggle (no effect).</summary>
	// CONTROL FACTOR: Deprecated; has no effect.
	[Obsolete("Deprecated: no effect in current film pass.")]
	public bool UseInsightPlanePass2 = true;
	/// <summary>Legacy insight plane slab thickness (no effect).</summary>
	// CONTROL FACTOR: Deprecated; has no effect.
	[Obsolete("Deprecated: no effect in current film pass.")]
	public float InsightPlaneEps = 0.10f;
	/// <summary>Placeholder for future normal smoothing (unused).</summary>
	// CONTROL FACTOR: Deprecated; has no effect.
	[Obsolete("Deprecated: reserved for future normal smoothing.")]
	public bool UseSmoothNormals = false;

	[ExportGroup("Shared With RayBeamRenderer")]

	[ExportSubgroup("References")]
	// This section references RayBeamRenderer and reflects shared settings (read from RayBeamRenderer at runtime).
	// SHARED FROM RAYBEAMRENDERER: StepsPerRay, CollisionEveryNSteps, collision mask, and field integration settings.
	// TOGGLES PULLED FROM RAYBEAMRENDERER: RequireHitToRender, StopOnHit, TerminateTrailOnHit, DebugOverlayOwnedByFilm.
	/// <summary>NodePath to the RayBeamRenderer used for film segment generation.</summary>
	// CONTROL FACTOR: RayBeamRendererPath selects the ray integrator; wrong path breaks film ray generation.
	[Export] public NodePath RayBeamRendererPath;
	/// <summary>Optional TextureRect used to display the film texture.</summary>
	// CONTROL FACTOR: Optional UI target for film texture; when null, film still renders but no direct display.
	[Export] public NodePath FilmViewPath;
	/// <summary>Optional FilmOverlay2D for debug ray overlay.</summary>
	// CONTROL FACTOR: Optional overlay node for debug ray visualization.
	[Export] public NodePath FilmOverlayPath;

	[ExportSubgroup("SoftGate Scaling (RayBeamRenderer)")]
	// This section overrides SoftGate thresholds using RayBeamRenderer step sizing.
	/// <summary>Use RayBeamRenderer step sizing to scale Pass2 SoftGate thresholds.</summary>
	// CONTROL FACTOR: When enabled, RayBeamRenderer step size overrides manual SoftGate scaling.
	[Export] public bool Pass2SoftGateUseRayBeamSettings = true;
	/// <summary>Minimum segment length in steps when using RayBeam settings (leave default unless you are tuning SoftGate).</summary>
	// CONTROL FACTOR: Minimum segment length (in steps) eligible for SoftGate when using RayBeam scaling; higher reduces probes.
	[Export] public float Pass2SoftGateMinSegLenSteps = 2.0f;

	[ExportGroup("Shared From RayBeamRenderer")]

	[ExportSubgroup("Status")]
	/// <summary>Shows whether a RayBeamRenderer snapshot is currently available.</summary>
	// CONTROL FACTOR: Read-only status mirror.
	[Export] public bool SharedRbrHasRenderer = false;

	[ExportSubgroup("Ray March")]
	/// <summary>Ray march steps per ray (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public int SharedRbrStepsPerRay = 0;
	/// <summary>Collision cadence (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public int SharedRbrCollisionEveryNSteps = 1;
	/// <summary>Step length (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrStepLength = 0.0f;
	/// <summary>Minimum step length (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrMinStepLength = 0.0f;
	/// <summary>Maximum step length (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrMaxStepLength = 0.0f;
	/// <summary>Step adapt gain (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrStepAdaptGain = 0.0f;
	/// <summary>Integrated field toggle (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public bool SharedRbrUseIntegratedField = false;
	/// <summary>Bend scale (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrBendScale = 0.0f;
	/// <summary>Field strength (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrFieldStrength = 0.0f;
	/// <summary>Field center (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public Vector3 SharedRbrFieldCenter = Vector3.Zero;
	/// <summary>Field center follows camera (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public bool SharedRbrFieldCenterIsCamera = true;

	[ExportSubgroup("Collision")]
	/// <summary>Collision mask (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public uint SharedRbrCollisionMask = 0x0000FFFF;
	/// <summary>Collision radius (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrCollisionRadius = 0.0f;
	/// <summary>Sphere sweep collision (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public bool SharedRbrUseSphereSweepCollision = false;
	/// <summary>Insight plane filter (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public bool SharedRbrUseInsightPlaneFilter = false;
	/// <summary>Collision subdivide threshold (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrCollisionRaySubdivideThreshold = 0.0f;
	/// <summary>Max collision substeps (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public int SharedRbrMaxCollisionSubsteps = 0;
	/// <summary>Require hit to render (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public bool SharedRbrRequireHitToRender = false;
	/// <summary>Stop on hit (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public bool SharedRbrStopOnHit = false;
	/// <summary>Terminate trail on hit (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public bool SharedRbrTerminateTrailOnHit = false;
	/// <summary>Screen-space collision cadence (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public bool SharedRbrUseScreenSpaceCollisionCadence = false;
	/// <summary>Collision max error pixels (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrCollisionMaxErrorPixels = 0.0f;
	/// <summary>Min depth for error (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrMinDepthForError = 0.0f;
	/// <summary>Min collision cadence (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public int SharedRbrMinCollisionEveryNSteps = 0;

	[ExportSubgroup("Debug Visualization")]
	/// <summary>Debug draw mode (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public RayBeamRenderer.DebugDrawMode SharedRbrDebugMode = RayBeamRenderer.DebugDrawMode.Off;
	/// <summary>Debug normal length (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public float SharedRbrDebugNormalLen = 0.0f;
	/// <summary>Debug overlay owned by film (mirrored).</summary>
	// CONTROL FACTOR: Read-only mirror from RayBeamRenderer.
	[Export] public bool SharedRbrDebugOverlayOwnedByFilm = false;


	// ===== Cached State =====
	private const float HudOverlayFontScaleMin = 0.6f;
	private const float HudOverlayFontScaleMax = 1.0f;
	private FilmOverlay2D _filmOverlay;
	private float _rangeFar = 5f; // dynamic far distance used for mapping
	private int _depthHistWrite = 0;
	private float[] _depthHistory = Array.Empty<float>();
	private Image _img;
	private ImageTexture _tex;
	private int _filmWidth;
	private int _filmHeight;
	private float[] _telemetryPass1AcceptedSteps = Array.Empty<float>();
	private float[] _telemetryCandidateCount = Array.Empty<float>();
	private float[] _telemetryQueryCount = Array.Empty<float>();
	private float[] _telemetryResolveCount = Array.Empty<float>();
	private float[] _telemetryCurvatureMax = Array.Empty<float>();
	private float[] _telemetryCurvatureMean = Array.Empty<float>();
	private float[] _telemetryDkMax = Array.Empty<float>();
	private float[] _telemetryD2kMax = Array.Empty<float>();
	private float[] _adaptiveEnvelopeMismatchPrior = Array.Empty<float>();
	private byte[] _adaptiveEnvelopeActiveMask = Array.Empty<byte>();
	private float[] _adaptiveEnvelopePreviousMismatchPrior = Array.Empty<float>();
	private byte[] _adaptiveEnvelopePreviousActiveMask = Array.Empty<byte>();
	private float _adaptiveEnvelopeScaleMinThisRun = 1.0f;
	private float _adaptiveEnvelopeScaleMaxThisRun = 1.0f;
	private double _adaptiveEnvelopeScaleSumThisRun = 0.0;
	private long _adaptiveEnvelopeScaleCountThisRun = 0;
	private int _adaptiveEnvelopeTightCountThisRun = 0;
	private int _adaptiveEnvelopeWarmCountThisRun = 0;
	private int _adaptiveEnvelopeNeutralCountThisRun = 0;
	private int _adaptiveEnvelopeRelaxedCountThisRun = 0;
	private int[] _adaptiveEnvelopeScaleHistogram = new int[5];
	private float _adaptiveEnvelopeGlobalQueryMinusCurvatureP50 = 0.0f;
	private float _adaptiveEnvelopeGlobalQueryMinusCurvatureP90 = 0.0f;
	private float _adaptiveEnvelopeGlobalRelaxedThreshold = 0.0f;
	private float _adaptiveEnvelopeGlobalWarmThreshold = 0.0f;
	private float _adaptiveEnvelopeGlobalHotThreshold = 0.0f;
	private float _adaptiveEnvelopePreviousGlobalQueryMinusCurvatureP50 = 0.0f;
	private float _adaptiveEnvelopePreviousGlobalQueryMinusCurvatureP90 = 0.0f;
	private float _adaptiveEnvelopePreviousGlobalRelaxedThreshold = 0.0f;
	private float _adaptiveEnvelopePreviousGlobalWarmThreshold = 0.0f;
	private float _adaptiveEnvelopePreviousGlobalHotThreshold = 0.0f;
	private bool _adaptiveEnvelopePreviousSnapshotAvailable = false;
	private int _adaptiveEnvelopePriorFallbackCountThisRun = 0;
	private int _adaptiveEnvelopePriorSnapshotUnavailableFallbackCountThisRun = 0;
	private int _adaptiveEnvelopePriorInsufficientDataFallbackCountThisRun = 0;
	private string _adaptiveEnvelopePriorFallbackBehaviorThisRun = "none";
	private bool _adaptiveEnvelopePriorSnapshotUnavailableLoggedThisRun = false;
	private bool _adaptiveEnvelopePriorInsufficientDataLoggedThisRun = false;
	private TextureRect _filmView;   // if user supplies FilmViewPath
	private TextureRect _overlayRect; // auto-created fallback
	private int _rowCursor = 0;
	private int _pendingBandRowStart = -1;
	private int _pendingBandRowCount = 0;
	private bool _pendingBandHasPass1 = false;
	private bool _softGateDisabledForPass = false;
	private int _lastFilmSettingsHash = 0;
	private bool _hasFilmSettingsHash = false;
	private ulong _lastCameraInstanceId = 0;
	private bool _hasLastCameraInstanceId = false;
	private Camera3D _cam;
	private bool _physicsRunsOnSeparateThread = false;
	private PhysicsDirectSpaceState3D _cachedRenderSpaceState;
	private ulong _cachedRenderSpaceWorldId = 0;
	private bool _hasCachedRenderSpaceWorldId = false;
	private bool _renderSpaceUnavailableWarned = false;
	private int _renderSpaceRayQueryWarned = 0;
	// CROSS-CLASS CONTRACT: _rbr supplies ray integration, segment builders, and hit payloads.
	// ASSUMPTION: _rbr settings are synchronized with film expectations (step lengths, collision cadence).
	// EFFECT: mismatched settings skew pass-1/2 collision accuracy and debug overlays.
	private RayBeamRenderer _rbr;
	private RenderBackends.LegacyBackend _legacyBackend;
	private RenderBackends.CoreBackend _coreBackend;
	private RayBeamRenderer.RaySeg[] _segBuf;
	private int[] _segCountPerPixel;
	private bool[] _pass1HitFound = Array.Empty<bool>();
	private bool[] _pass1StoppedEarly = Array.Empty<bool>();
	private int[] _pass1HitSegIndex = Array.Empty<int>();
	private float[] _pass1HitDist = Array.Empty<float>();
	private Vector3[] _pass1HitPos = Array.Empty<Vector3>();
	private Vector3[] _pass1HitNormal = Array.Empty<Vector3>();
	private ulong[] _pass1HitColliderId = Array.Empty<ulong>();
	private byte[] _pass2PrevHadHit = Array.Empty<byte>();
	private byte[] _pass2HadHitLostThisFrame = Array.Empty<byte>();
	private PhysicsRayQueryParameters3D _quickRayParams;
	private PhysicsShapeQueryParameters3D _overlapQuery;
	private SphereShape3D _overlapSphere;
	private readonly System.Collections.Generic.List<Godot.Collections.Dictionary> _pass2OverlapCandidatesScratch = new System.Collections.Generic.List<Godot.Collections.Dictionary>(64);
private readonly System.Collections.Generic.HashSet<ulong> _fixtureDebugSourceIds = new System.Collections.Generic.HashSet<ulong>();
private readonly System.Collections.Generic.HashSet<ulong> _fixtureDebugBackgroundIds = new System.Collections.Generic.HashSet<ulong>();
private bool _fixtureDebugHasExplicitBackgroundGroup = false;
	private readonly PerfStats _perfStats = new PerfStats(60);
	private PerfFrameReport _perfFrame;

	// field source cache
	private int _frameIndex = 0;
	private ulong _frameId = 0;
	private double _busLogTimerSec = 0.0;
	private double _snapshotLogTimerSec = 0.0;
	private bool _warnedNotProcessing = false;
	private bool _warnedNoCameraForGrid = false;
	private int _fieldSourceLastRefreshFrame = -100000;
	private Node[] _fieldSourceNodes = Array.Empty<Node>();
	private Transform3D[] _fieldSourceXforms = Array.Empty<Transform3D>();
	private ulong[] _fieldSourceIds = Array.Empty<ulong>();
	private int _fieldSourceCount = 0;
	private RayBeamRenderer.FieldSourceSnap[] _fieldSourceSnaps = Array.Empty<RayBeamRenderer.FieldSourceSnap>();
	private FieldGrid3D _fieldGrid;

	// conservative: renderer reports the current worst-case segment budget per ray.
	private int MaxSegPerRay => (_rbr != null)
		? _rbr.EstimateMaxSegmentsPerRay()
		: 64;

	// Debug overlay buffers (reused, no GC)
	private Vector3[] _dbgPts = Array.Empty<Vector3>(); // concatenated polyline points
	private int[] _dbgOff = Array.Empty<int>();         // offsets per ray
	private int[] _dbgCnt = Array.Empty<int>();         // counts per ray
	private RayBeamRenderer.HitPayload[] _dbgHits = Array.Empty<RayBeamRenderer.HitPayload>();
	private int _dbgRayCount = 0;
	private int _dbgPtWrite = 0;

	private const int Pass2QuickRayCacheSize = 512;
	private const float Pass2QuickRayCacheQuantize = 10f;
	private const int BroadphaseHybridFallbackLogLimitPerFrame = 4;
	private const int BroadphaseHybridFallbackHitLogLimitPerFrame = 4;
	private const int BroadphaseHybridGateLogLimitPerFrame = 4;
	private const int BroadphaseNoCandidateLogLimitPerFrame = 4;
	private const int GeomEligibleAccountingLogLimitPerFrame = 1;

	private struct Pass2HitFlags
	{
		public bool HitBackFaces;
		public bool HitFromInside;
	}

	private struct BroadphaseCandidateResult
	{
		public int Count;
		public bool DidQuickRay;
		public bool DidOverlap;
		public bool NoCandidates;
	}

	private struct SegmentContext
	{
		public PhysicsDirectSpaceState3D Space;
		public Vector3 A;
		public Vector3 B;
		public bool UseQuickRay;
		public bool UseOverlap;
		public bool BypassQuickRay;
		public bool QuickRayExecuted;
		public int QuickRayCount;
		public bool QuickRayHit;
		public bool QuickRayMiss;
		public bool OverlapExecuted;
		public int OverlapCount;
	}

	private struct OverlapResult
	{
		public int Count;
		public System.Collections.Generic.List<Godot.Collections.Dictionary> Candidates;
	}

	private struct Pass2QuickRayCacheEntry
	{
		public int Ax;
		public int Ay;
		public int Az;
		public int Bx;
		public int By;
		public int Bz;
		public int Flags;
		public float HitDistAlongRay;
		public bool DidHit;
	}

	private Pass2QuickRayCacheEntry[] _pass2QuickRayCache = Array.Empty<Pass2QuickRayCacheEntry>();
	private int _pass2QuickRayCacheCount = 0;
	private int _pass2QuickRayCacheWrite = 0;

	private struct ToggleSnapshot
	{
		public bool UseAdaptiveSubsteps;
		public bool UseSingleProbeThenSubdivide;
		public bool UseBandHitSkip;
		public bool RequireHitToRender;
		public bool StopOnHit;
		public bool TerminateTrailOnHit;
		public bool UpdateEveryFrame;
	}

	private ToggleSnapshot _lastToggleSnapshot;
	private bool _hasToggleSnapshot;
	private bool _lastBroadphaseEffectiveQuickRay = false;
	private bool _lastBroadphaseEffectiveOverlap = false;
	private BroadphaseMode _lastBroadphaseEffectiveMode = BroadphaseMode.Manual;
	private BroadphasePolicyMode _lastBroadphaseEffectivePolicy = BroadphasePolicyMode.None;
	private bool _hasLastBroadphaseEffective = false;
	private bool _isBroadphaseSyncing = false;
	private bool _hasBroadphaseSyncSnapshot = false;
	private BroadphaseMode _lastBroadphaseMode = BroadphaseMode.Manual;
	private BroadphasePolicyMode _lastBroadphasePolicy = BroadphasePolicyMode.None;
	private bool _lastUseBroadphaseQuickRay = false;
	private bool _lastUseBroadphaseOverlap = false;
	private bool _broadphaseCurvedWarned = false;
	private BroadphasePolicyMode _autoBroadphasePolicy = BroadphasePolicyMode.QuickRayOnly;
	private int _autoBroadphaseCooldownRemaining = 0;
	private int _autoBroadphaseLastFlipStep = -1;

	private const int RenderHealthBufferSize = 60;
	private const int TileMetricsBufferSize = 128;
	private const int RenderHealthLogEveryNSteps = 30;
	private const int RenderHealthStallThreshold = 10;
	private const int RenderHealthPass2SampleEveryNSegments = 4096;
	private const int RenderHealthMinSamplesForTrust = 64;
	private const int RenderHealthMinModeSamplesForTrust = 8;
	// Renderer throughput smoothing for overlay metrics (EMA over RenderHealth emissions).
	private const double OverlayRenderThroughputEmaAlpha = 0.20;
	private const int OverlayRollingCapacity = 256;
	private const uint PruneAuditDeterministicMask = 31u; // 1/32 deterministic sampling gate.
	private const double TileMetricsPersistentPriorDecay = 0.92;
	private const double TileMetricsPersistentPriorBlendWeight = 0.6;
	private const double TileMetricsPersistentPriorWeakCurrentBoost = 0.2;
	private const int TileMetricsPersistentPriorNeighborBandRadius = 2;

	private RenderHealthSample[] _renderHealthSamples = new RenderHealthSample[RenderHealthBufferSize];
	private TileMetricSample[] _tileMetricSamples = new TileMetricSample[TileMetricsBufferSize];
	private int _renderHealthWrite = 0;
	private int _renderHealthCount = 0;
	private int _tileMetricWrite = 0;
	private int _tileMetricCount = 0;
	private int _tileMetricsLogFrame = -1;
	private int _tileMetricsLogsRemainingThisFrame = 0;
	private int _renderHealthStepIndex = 0;
	private int _renderHealthLastLogStep = -1;
	private int _renderHealthStallSteps = 0;
	private int _renderHealthLastRowCursor = -1;
	private string _renderHealthLastExitReason = "";
	private bool _rowStallActive = false;
	private int _renderHealthPass2SampleCounter = 0;
	private TileMetricAccumulator[] _tileMetricCurrentSubtiles = Array.Empty<TileMetricAccumulator>();
	private int _tileMetricCurrentBandIndex = 0;
	private int _tileMetricCurrentBandY = 0;
	private int _tileMetricCurrentBandHeight = 0;
	private int _tileMetricCurrentSubtileCount = 0;
	private int _tileMetricCurrentSubtileWidth = 1;
	private long _tileMetricSimBandsWithHitsThisFrame = 0;
	private long _tileMetricSimTotalHitsThisFrame = 0;
	private long _tileMetricSimSegmentsTracedThisFrame = 0;
	private long _tileMetricSimHitsTop1ThisFrame = 0;
	private long _tileMetricSimHitsTop2ThisFrame = 0;
	private long _tileMetricSimHitsTop3ThisFrame = 0;
	private long _tileMetricActualHitsTop1ThisFrame = 0;
	private long _tileMetricActualHitsTop2ThisFrame = 0;
	private long _tileMetricActualHitsTop3ThisFrame = 0;
	private long _tileMetricSimPrimaryHitsTop1ThisFrame = 0;
	private long _tileMetricSimBackdropHitsTop1ThisFrame = 0;
	private long _tileMetricSimCombinedHitsTop1ThisFrame = 0;
	private long _tileMetricActualPrimaryHitsTop1ThisFrame = 0;
	private long _tileMetricActualBackdropHitsTop1ThisFrame = 0;
	private long _tileMetricActualCombinedHitsTop1ThisFrame = 0;
	private long _tileMetricActualFirstHitOrdinalSumThisFrame = 0;
	private long _tileMetricActualHit50OrdinalSumThisFrame = 0;
	private long _tileMetricActualFirstHitOrdinalCountThisFrame = 0;
	private long _tileMetricActualHit50OrdinalCountThisFrame = 0;
	private readonly System.Collections.Generic.Dictionary<long, TileMetricAccumulator[]> _tileMetricBandHistory = new System.Collections.Generic.Dictionary<long, TileMetricAccumulator[]>();
	private readonly System.Collections.Generic.Dictionary<string, TileMetricPersistentPrior> _tileMetricPersistentPriors = new System.Collections.Generic.Dictionary<string, TileMetricPersistentPrior>(StringComparer.Ordinal);
	private int[] _tileMetricCurrentExecutionOrder = Array.Empty<int>();
	private string _tileMetricCurrentExecutionSource = "baseline";
	private double _tileMetricCurrentExecutionPriorWeight = 0.0;
	private long _tileMetricExecBandsWithHitsThisFrame = 0;
	private long _tileMetricExecTotalHitsThisFrame = 0;
	private long _tileMetricExecSegmentsTracedThisFrame = 0;
	private long _tileMetricExecLegacyHitsTop1ThisFrame = 0;
	private long _tileMetricExecLegacyHitsTop2ThisFrame = 0;
	private long _tileMetricExecLegacyHitsTop3ThisFrame = 0;
	private long _tileMetricExecOrderedHitsTop1ThisFrame = 0;
	private long _tileMetricExecOrderedHitsTop2ThisFrame = 0;
	private long _tileMetricExecOrderedHitsTop3ThisFrame = 0;
	private long _tileMetricExecLegacyPrimaryHitsTop1ThisFrame = 0;
	private long _tileMetricExecLegacyBackdropHitsTop1ThisFrame = 0;
	private long _tileMetricExecLegacyCombinedHitsTop1ThisFrame = 0;
	private long _tileMetricExecOrderedPrimaryHitsTop1ThisFrame = 0;
	private long _tileMetricExecOrderedBackdropHitsTop1ThisFrame = 0;
	private long _tileMetricExecOrderedCombinedHitsTop1ThisFrame = 0;
	private long _tileMetricExecFirstHitOrdinalSumThisFrame = 0;
	private long _tileMetricExecHit50OrdinalSumThisFrame = 0;
	private long _tileMetricExecFirstHitOrdinalCountThisFrame = 0;
	private long _tileMetricExecHit50OrdinalCountThisFrame = 0;
	private long _tileMetricExecSeedBandsWithHitsThisFrame = 0;
	private long _tileMetricExecRankedBandsWithHitsThisFrame = 0;
	private long _tileMetricExecPriorBandsWithHitsThisFrame = 0;
	private long _tileMetricExecPriorOnlyBandsWithHitsThisFrame = 0;
	private long _tileMetricExecCurrentDominantBandsWithHitsThisFrame = 0;
	private long _tileMetricExecPriorContribBandsWithHitsThisFrame = 0;
	private long _tileMetricExecSeedHitsThisFrame = 0;
	private long _tileMetricExecRankedHitsThisFrame = 0;
	private long _tileMetricExecPriorOnlyHitsThisFrame = 0;
	private long _tileMetricExecSeedSegmentsTracedThisFrame = 0;
	private long _tileMetricExecRankedSegmentsTracedThisFrame = 0;
	private long _tileMetricExecPriorOnlySegmentsTracedThisFrame = 0;
	private long _tileMetricExecTop1OrderChangedBandsWithHitsThisFrame = 0;
	private long _tileMetricExecTop1HitImprovedBandsWithHitsThisFrame = 0;
	private bool ExperimentalSubtileSchedulerModeEnabled => EnableTileMetricsScaffold && EnableTileMetricsReorderExecution;
	private bool ExperimentalPersistentSubtileSchedulerModeEnabled => ExperimentalSubtileSchedulerModeEnabled && EnableTileMetricsPersistentPriors;
	private int _geomPruneAuditSamplesTakenThisWindow = 0;
	private double _lastFrameRenderMs = 0.0;
	private readonly StringBuilder _overlayHudSb = new StringBuilder(256);
	private string _hudFixtureName = string.Empty;
	private string _hudTransportModel = string.Empty;
	private string _hudProfileToken = string.Empty;
	private string _hudSourcePatternMode = string.Empty;
	private string _hudRenderTestMode = string.Empty;
	private string _hudRenderLoopStatus = string.Empty;
	private int _hudRenderProbeRayCount = -1;
	private string _hudMetricSteeringLaw = string.Empty;
	private bool _hudMetricGainOverrideActive = false;
	private float _hudMetricGainOverride = 1.0f;
	private string _lastLoggedHudMetadata = string.Empty;
	private string _lastLoggedHudRuntimeSummary = string.Empty;
	private long _lastRenderHealthEmissionTimestamp = 0;
	private bool _hasLastRenderHealthEmissionTimestamp = false;
	private double _rowsPerSecEma = 0.0;
	private double _msPerRowEma = 0.0;
	private bool _hasRenderThroughputEma = false;
	private bool _renderThroughputWindowTrusted = false;
	private readonly OverlayRollingWindow _overlayRolling = new OverlayRollingWindow(OverlayRollingCapacity);
	private readonly OverlayRollingWindow _presentRolling = new OverlayRollingWindow(OverlayRollingCapacity);
	private bool _testHasRenderHealthSnapshot = false;
	private bool _testLastGeomTrusted = false;
	private bool _testLastGeomSavedPctAvailable = false;
	private double _testLastGeomSavedPct = 0.0;
	private bool _testLastGeomPerPxOnAvailable = false;
	private double _testLastGeomPerPxOn = 0.0;
	private bool _testLastGeomPerPxOffAvailable = false;
	private double _testLastGeomPerPxOff = 0.0;
	private long _testLastGeomPixProcessedRaw = 0;
	private long _testLastGeomRayTestsTotalRaw = 0;
	private long _testLastGeomPixNoCandRaw = 0;
	private long _testLastP2SampRaw = 0;
	private bool _testLastGeomSegZeroRatePctAvailable = false;
	private double _testLastGeomSegZeroRatePct = 0.0;
	private string _testLastTopExit = "na";
	private string _testLastGeomTrustReason = "na";
	private bool _testLastTrustGeomPixMet = false;
	private bool _testLastTrustRayTestsMet = false;
	private bool _testLastTrustP2Met = false;
	private bool _renderHealthTestTrustEnforcementEnabled = false;
	private int _renderHealthTestMinGeomPixProcessedPerWindow = 0;
	private long _renderHealthTestMinGeomRayTestsTotalPerWindow = 0;
	private int _renderHealthTestPass2SampleEveryNSegmentsOverride = 0;
	private int _renderHealthTestMinPass2SamplesForTrustOverride = 0;
	private long _fixtureDebugSourceHitsThisRun = 0;
	private long _fixtureDebugBackgroundHitsThisRun = 0;
	private long _fixtureDebugUnclassifiedHitsThisRun = 0;
	private long _fixtureDebugAbsorbedHitsThisRun = 0;
	private long _fixtureDebugMissHitsThisRun = 0;
	private long _fixtureFinalHitPixelCountThisRun = 0;
	private long _fixtureTraversalWritePixelCountThisRun = 0;
	private byte[] _fixtureRowsConsidered = Array.Empty<byte>();
	private byte[] _fixtureRowsProcessed = Array.Empty<byte>();
	private byte[] _fixtureRowsSkipped = Array.Empty<byte>();
	private byte[] _fixtureRowsZeroHit = Array.Empty<byte>();
	private byte[] _fixtureRowsStarted = Array.Empty<byte>();
	private byte[] _fixtureRowsCompleted = Array.Empty<byte>();
	private byte[] _fixtureRowsPartiallyWritten = Array.Empty<byte>();
	private byte[] _fixtureRowsEarlyTerminated = Array.Empty<byte>();
	private Image _fixtureFinalHitOnlyImg;
	private Image _fixtureCategoricalFinalImg;

	// band hit ROI history
	private float[] _bandHitRate = Array.Empty<float>();
	private int[] _bandLowHitFrames = Array.Empty<int>();
	private Transform3D _lastCamTransform;
	private bool _hasLastCamTransform;
	private float _lastRangeFar;
	private bool _hasLastRangeFar;

	private FramePerf _framePerf = new FramePerf();
	private uint[] _presentTouchedEpoch = Array.Empty<uint>();
	private uint[] _refreshTouchedEpoch = Array.Empty<uint>();
	private uint _presentTouchedEpochId = 1;
	private uint _refreshTouchedEpochId = 1;
	private int _presentTouchedPixels = 0;
	private int _refreshTouchedPixels = 0;
	private int _presentsSinceRefreshReset = 0;
	private long _refreshCycleStartTimestamp = 0;
	private bool _hasRefreshCycleStartTimestamp = false;
	private int _lastPresentedPixelsUpdated = 0;
	private double _lastPresentedCoverageRatio = 0.0;
	private int _lastFramesToFullRefresh = 0;
	private double _lastTimeToFullRefreshMs = 0.0;
	private double _lastEffectiveFullRefreshFps = 0.0;
	private bool _lastFullRefreshMeasured = false;
	private bool _threadReadinessAuditLogged = false;
	private bool _threadedBandsDeterminismWarned = false;
	private bool _deterministicBenchmarkModeRequested = false;
	private uint _deterministicBenchmarkSeed = 1;
	private bool _deterministicBenchmarkModeLogged = false;
	private double _lastTestedSegsPerPixel = 0.0;
	private long _lastPhysQ = 0;
	private bool _hasPerfDeltaBaseline = false;
	private int _adaptiveRowsPerFrame = 0;
	private const int SoftGateSampleEveryNSegments = 4096;
	private int _softGateWatchdogLogsRemaining = 0;
	private int _softGateSummaryLogsRemaining = 0;
	private int _broadphaseHybridFallbackLogsRemaining = 0;
	private int _broadphaseHybridFallbackHitLogsRemaining = 0;
	private int _broadphaseHybridGateLogsRemaining = 0;
	private int _softGateSampleCounter = 0;
	private long _softGateAttemptsUsedThisFrame = 0;
	private long _softGateSubdividedCallsUsedThisFrame = 0;
	private int _quickRayZeroCountThisFrame = 0;
	private int _hybridFallbackCountThisFrame = 0;
	private int _hybridFallbackHitCountThisFrame = 0;
	private int _hybridFallbackMissCountThisFrame = 0;
	private long _geomCandidatesTotalThisFrame = 0;
	private long _geomCandidatesSegmentsThisFrame = 0;
	private long _geomSegmentsQueriedThisFrame = 0;
	// RenderHealth counter accounting map:
	// - geomSegQueried increment site: Pass2 TLAS block immediately after QueryAabb(...) when pass==0.
	// - geomSegWithCandidates increment site: Pass2 TLAS block immediately after QueryAabb(...) when pass==0 && candCount>0.
	// - geomSegZero increment site: Pass2 TLAS block immediately after QueryAabb(...) when pass==0 && candCount==0.
	// - geomPixProcessed increment site: first Pass2 geometry query attempt per pixel (TLAS/overlap/ray/sweep),
	//   regardless of prune mode.
	//   Prior behavior only incremented in prune-ON pixel entry, which kept prune-OFF windows at 0 and forced
	//   trust-gated OFF per-pixel metrics (geomRayTestsPerPxOff) to remain NA.
	// - geomPixHadAnyCandidates increment site: per-pixel Pass2 epilogue when TLAS pruning is active and any pass-0 TLAS query returned candCount>0.
	// - geomPixNoCand increment site: per-pixel Pass2 epilogue when TLAS pruning is active and no pass-0 TLAS query returned candCount>0.
	// - geomRayTestsTotal can be incremented by eligible accounting when a candidate-bearing segment exits early with no ray query.
	// These counters are log-window counters and reset only when a RenderHealth line is printed.
	private long _geomSegWithCandidatesThisFrame = 0;
	private long _geomSegZeroCandidatesThisFrame = 0;
	private long _geomPixelProcessedThisFrame = 0;
	private long _geomPixelHadAnyCandidatesThisFrame = 0;
	private long _geomPixelNoCandidatesThisFrame = 0;
	private long _geomHitAcceptedThisFrame = 0;
	private long _geomHitRejectedThisFrame = 0;
	private long _geomRayTestsTotalThisFrame = 0;
	private long _geomRayTestsAcceptedThisFrame = 0;
	private long _geomRayTestsRejectedThisFrame = 0;
	private long _geomHitAcceptedLastSample = 0;
	private long _geomHitRejectedLastSample = 0;
	private long _geomSegWithCandidatesLastSample = 0;
	private long _geomSegZeroCandidatesLastSample = 0;
	private long _geomPixelProcessedLastSample = 0;
	private long _geomPixelHadAnyCandidatesLastSample = 0;
	private long _geomPixelNoCandidatesLastSample = 0;
	private long _geomSegmentsQueriedLastSample = 0;
	private long _geomRayTestsTotalLastSample = 0;
	private long _geomRayTestsAcceptedLastSample = 0;
	private long _geomRayTestsRejectedLastSample = 0;
	private long _geomRayTestsPruningOnTotal = 0;
	private long _geomRayTestsPruningOnTracedPixels = 0;
	private long _geomRayTestsPruningOffTotal = 0;
	private long _geomRayTestsPruningOffTracedPixels = 0;
	private bool _hasRenderHealthGeomPruneMode = false;
	private bool _lastRenderHealthGeomPruneMode = false;
	private int _geomPruneSwitchedThisWindow = 0;
	// Baseline used only for "saved%" comparisons while pruning is ON.
	// It is learned only while pruning is OFF and shown while pruning is ON.
	// It is reset when entering OFF mode to relearn a mode-pure baseline,
	// and becomes ready only after stable OFF windows (no switch + enough mode samples).
	private double _geomRayTestsOffPerPixelBaseline = 0.0;
	private bool _geomRayTestsOffPerPixelBaselineReady = false;
	private long _geomPruneAuditSamplesThisFrame = 0;
	private long _geomPruneAuditFalseNegThisFrame = 0;
	private long _geomPruneAuditFalsePosThisFrame = 0;
	private long _geomPruneAuditCandidateZeroButBaselineHitThisFrame = 0;
	private long _geomPruneAuditSamplesLastSample = 0;
	private long _geomPruneAuditFalseNegLastSample = 0;
	private long _geomPruneAuditFalsePosLastSample = 0;
	private long _geomPruneAuditCandidateZeroButBaselineHitLastSample = 0;
	private int _geomPruneAuditMismatchLogsThisWindow = 0;
	private long _geomRejectSampleCidNotInGeometryList = 0;
	private long _geomRejectSampleCidInGeometryListNotInCandidates = 0;
	private long _geomRejectSampleCandidateContainsCid = 0;
	private int[] _geomCandidatesScratch = Array.Empty<int>();
	private long[] _geomCandidateInstanceIdsScratch = Array.Empty<long>();
	private SoftGateDebugCounters _softGateFrame;
	private SoftGateDebugCounters _softGateBand;
	private SoftGateConfigSnapshot _lastSoftGateCfgSnapshot;
	private bool _hasSoftGateCfgSnapshot = false;
	private int _p2SoftGateUsedThisFrame = 0;
	private int _lastEffectiveConfigHash = 0;
	private bool _hasEffectiveConfigHash = false;
	private int _lastResearchSummaryHash = 0;
	private bool _hasResearchSummaryHash = false;
	private bool _researchWasEnabledLastFrame = false;
	private int _researchClampedStepsPerRayCount = 0;
	private int _researchDeterministicFramesCount = 0;
	private int _lastSharedSnapshotHash = 0;
	private bool _hasSharedSnapshotHash = false;
	private int _lastSharedSnapshotMirrorHash = 0;
	private bool _hasSharedSnapshotMirrorHash = false;
	private int _lastProcessedPixelsThisBand = 0;
	private bool _hasLastProcessedPixelsThisBand = false;
	private int _broadphaseNoCandidateLogsRemaining = 0;
	private int _geomEligibleAccountingLogsRemaining = 0;
	private int _hybridNoCandidateCountThisFrame = 0;
	private bool _rbrRefLoggedPathEmpty = false;
	private bool _rbrRefLoggedResolvedOk = false;
	private bool _rbrRefLoggedResolveFailed = false;
	private bool _rbrRefLoggedWrongType = false;
	private bool _rbrRefAutoResolveAttempted = false;
	private bool _rbrRefLoggedAutoResolved = false;
	private bool _rbrRefLoggedAutoResolveFailed = false;
	private NodePath _lastRbrResolvePath;
	private bool _hasLastRbrResolvePath = false;
	private int _softGateFrameId = -1;
	private int _softGateParamLogRemaining = 2;
	private int _budgetYieldLogFrameId = -1;
	private int _renderStepYieldLogFrameId = -1;
	private int _renderStepYieldLogsThisFrame = 0;
	private int _renderStepForceAdvanceWarnFrameId = -1;
	private int _renderStepForceAdvanceWarnsThisFrame = 0;
	private int _budgetExitFrameId = -1;
	private readonly System.Collections.Generic.HashSet<string> _budgetExitReasonsThisFrame = new();
	private RenderQualityMode _lastQualityMode = (RenderQualityMode)(-1);
	private PresetMode _lastPreset = (PresetMode)(-1);
	private PerformancePresetMode _lastPerformancePreset = (PerformancePresetMode)(-1);
	private bool _presetSceneDirty = false;
	private bool _presetPerfDirty = false;
	private bool _presetQualityDirty = false;
	private string _presetDirtyReason = "";
	private bool _isApplyingPresets = false;
	private RandomNumberGenerator _rng = new RandomNumberGenerator();
	private volatile int _renderStepActive = 0;
	private bool _renderStepReentryWarned = false;
	private bool _renderStepMissingRbrCameraWarned = false;
	private bool _rowsRangeWarningIssued = false;
	private int _bandHeightRowsResolved = 0;
	private int _stuckBandStartRow = -1;
	private int _stuckBandEndRow = -1;
	private int _stuckBandRepeats = 0;
	private int _noRowProgressRepeats = 0;
	private int _bandNoHitStallStartRow = -1;
	private int _bandNoHitStallEndRow = -1;
	private int _bandNoHitStallRepeats = 0;
	private int _noCandidateBandStallSteps = 0;
	private int _noCandidateBandLastRowCursor = -1;
	private int _noHitBandStallSteps = 0;
	private int _noHitBandLastRowCursor = -1;
	private bool _lastBandCommitted = true;
	private int _lastRenderStepRowCursor = -1;
	private int _lastRenderStepBandStart = -1;
	private int _lastRenderStepBandEnd = -1;
	private int _bandIncompleteFrameId = -1;
	private int _bandIncompleteRowStart = -1;
	private int _bandIncompleteRowEnd = -1;
	private bool _suppressStuckBandRepeatOnce = false;
	private bool _pendingRowCursorReset = false;
	private string _pendingRowCursorResetReason = "";
	private const int StuckBandWatchdogMaxRepeats = 10;
	private const int BandNoHitStallMaxRepeats = 3;
	private const int SmartScaleWarmupSamples = 3;
	private bool _smartScaleRunRequested = false;
	private bool _smartScaleRunInProgress = false;
	private bool _smartScalePendingSafeBoundaryAbort = false;
	private bool _smartScaleRunOnReadyDeferred = false;
	private bool _smartScaleWarnedFixtureCurvature = false;
	private bool _smartScaleEnableEdgeArmed = false;
	private int _smartScaleActiveProbeIndex = -1;
	private int _smartScaleProbeLastStepObserved = -1;
	private int _smartScaleProbeRenderStepCalls = 0;
	private int _smartScaleProbeRowsAdvancedTotal = 0;
	private int _smartScaleProbeBudgetStopCount = 0;
	private long _smartScaleProbeGeomPixProcessedRaw = 0;
	private long _smartScaleProbeGeomRayTestsTotalRaw = 0;
	private readonly System.Collections.Generic.List<SmartScaleProbeResult> _smartScaleProbeResults = new System.Collections.Generic.List<SmartScaleProbeResult>(8);
	private SmartScaleSavedConfig _smartScaleSavedConfig;
	private bool _smartScaleSavedConfigValid = false;
	private SmartScaleProbeConfig[] _smartScaleProbePlan = Array.Empty<SmartScaleProbeConfig>();
	private TestRunDefaults _runtimeMacroBaselineDefaults;
	private bool _runtimeMacroBaselineCaptured = false;
	private bool _runtimeMacroModeActive = false;
	private bool _runtimeMacroAvailabilityWarned = false;
	private bool _runtimeMacroHasMotionSample = false;
	private Vector3 _runtimeMacroLastCameraPosition = Vector3.Zero;
	private Basis _runtimeMacroLastCameraBasis = Basis.Identity;
	private bool _runtimeMacroCameraMoving = false;
	private float _runtimeMacroLastMotionDistance = 0.0f;
	private float _runtimeMacroLastMotionAngleDeg = 0.0f;
	private string _runtimeMacroLastAppliedSummary = string.Empty;
	private const float RuntimeMacroMotionDistanceEps = 0.003f;
	private const float RuntimeMacroMotionAngleDegEps = 0.2f;
	private const Key RuntimeMacroCycleKey = Key.F6;
	private const Key RuntimeMacroAccurateKey = Key.F7;
	private const Key RuntimeMacroTight08Key = Key.F8;
	private const Key RuntimeMacroCheapMotionKey = Key.F9;
	private const Key RuntimeMacroSettleRefineKey = Key.F10;

	private struct SmartScaleSavedConfig
	{
		public bool UpdateEveryFrame;
		public int PixelStride;
		public bool UsePass2CollisionStride;
		public int TargetMsPerFrame;
		public float UpdateEveryFrameBudgetMs;
		public int UpdateEveryFrameMaxRowsPerStep;
		public int RowsPerFrame;
		public int MaxRowsPerFrameCap;
		public int RenderStepMaxMs;
		public int RenderStepMaxPixelsPerFrame;
		public int RenderStepMaxSegmentsPerFrame;
		public BroadphaseMode BroadphaseControlMode;
		public BroadphasePolicyMode BroadphasePolicy;
	}

	private struct SmartScaleProbeConfig
	{
		public string ProbeId;
		public string Summary;
		public int? PixelStride;
		public bool? UsePass2CollisionStride;
		public int? TargetMsPerFrame;
		public float? UpdateEveryFrameBudgetMs;
		public int? UpdateEveryFrameMaxRowsPerStep;
		public int? RowsPerFrame;
		public int? MaxRowsPerFrameCap;
		public int? RenderStepMaxMs;
		public int? RenderStepMaxPixelsPerFrame;
		public int? RenderStepMaxSegmentsPerFrame;
		public BroadphaseMode? BroadphaseControlMode;
		public BroadphasePolicyMode? BroadphasePolicy;
	}

	private struct SmartScaleProbeResult
	{
		public SmartScaleProbeConfig Probe;
		public bool Trusted;
		public bool TrustKnown;
		public string TrustReason;
		public int RenderStepCalls;
		public int RowsAdvancedTotal;
		public int BudgetStopCount;
		public long GeomPixProcessedRaw;
		public long GeomRayTestsTotalRaw;
	}



	private struct SoftGateDebugCounters
	{
		public int FrameIndex;
		public long TracedPixels;
		public long FilledPixels;
		public long EffectivePixels;
		public long SegsTotal;
		public long SegsTested;
		public long Pass2Hits;
		public long QRayCalls;
		public long QRayHit;
		public long QRayMiss;
		public bool SoftGateEnabled;
		public float SoftGateMinSegLen;
		public float SoftGateScoreThreshold;
		public float SoftGateTurnAngleWeight;
		public float SoftGatePrevHitLostBonus;
		public float SoftGateRandomProbeChance;
		public int SoftGateMaxAttemptsPerFrameV2;
		public long SoftGateConsidered;
		public long SoftGateSkipped;
		public long SoftGateForced;
		public long SoftGateAttempts;
		public long SoftGateHits;
		public long SoftGateHitChangedResult;
		public long SoftGateNewPixelFilled;
		public long SoftGateBudgetExceeded;
		public long SoftGateAttemptsUsed;
		public long SoftGateSubdividedCallsUsed;
		public int Pass2SoftGateMaxAttemptsPerPixel;
		public int Pass2SoftGateMaxAttemptsPerFrame;
		public int Pass2SoftGateMaxSubdividedCallsPerFrame;
		public double SoftGateMetricMin;
		public double SoftGateMetricMax;
		public double SoftGateMetricSum;
		public long SoftGateMetricCount;
		public long SkipSegLenTooShort;
		public long SkipScoreTooLow;
		public long SkipRandomNotSelected;
		public long SkipBudgetAttemptCap;
		public long SkipBudgetSubdivideCap;
		public long SkipGuard;
		public long SkipOther;
	}

	private struct SoftGateConfigSnapshot
	{
		public bool Pass2SoftGateEnableQuickRayMiss;
		public bool Pass2SoftGateScoringEnabled;
		public float Pass2SoftGateMinSegmentLength;
		public float Pass2SoftGateScoreThreshold;
		public float Pass2SoftGateScoreTurnAngleWeight;
		public float Pass2SoftGateScorePrevHitLostBonus;
		public float Pass2SoftGateRandomProbeChance;
		public int Pass2SoftGateScoreBudgetPerFrame;
		public int Pass2SoftGateMaxAttemptsPerPixel;
		public int Pass2SoftGateMaxAttemptsPerFrame;
		public int Pass2SoftGateMaxSubdividedCallsPerFrame;
		public bool UpdateEveryFrame;
	}

	private struct EffectiveBroadphaseSettings
	{
		public bool UseQuickRay;
		public bool UseOverlap;
		public BroadphaseMode Mode;
		public string ModeName;
		public string Reason;
		public BroadphasePolicyMode Policy;
		public float Margin;
		public int MaxResults;
	}

	private struct EffectiveSoftGateSettings
	{
		public bool EnableQuickRayMiss;
		public bool ScoringEnabled;
		public bool DisableOnOverload;
		public bool UseRayBeamSettings;
		public bool UseRayBeamSettingsActive;
		public float EffectiveStepLength;
		public float MinSegLenSteps;
		public float MinSegmentLength;
		public float ScoreThreshold;
		public float ScoreTurnAngleWeight;
		public float ScorePrevHitLostBonus;
		public float RandomProbeChance;
		public int ScoreBudgetPerFrame;
		public int MaxAttemptsPerPixel;
		public int MaxAttemptsPerFrame;
		public int MaxSubdividedCallsPerFrame;
		public float WatchdogMs;
		public int WatchdogLogLimitPerFrame;
		public bool DebugEnabled;
		public int DebugVerbosity;
		public bool DebugSummaryPerFrame;
		public int DebugSummaryLogLimitPerFrame;
	}

	private struct EffectiveRayMarchSettings
	{
		public bool HasRenderer;
		public int StepsPerRay;
		public int CollisionEveryNSteps;
		public float StepLength;
		public float MinStepLength;
		public float MaxStepLength;
		public float StepAdaptGain;
		public bool UseIntegratedField;
		public float BendScale;
		public float FieldStrength;
		public Vector3 FieldCenter;
		public bool FieldCenterIsCamera;
		public uint CollisionMask;
		public float CollisionRadius;
		public bool UseSphereSweepCollision;
		public bool UseInsightPlaneFilter;
		public float CollisionRaySubdivideThreshold;
		public int MaxCollisionSubsteps;
		public bool RequireHitToRender;
		public bool StopOnHit;
		public bool TerminateTrailOnHit;
		public bool UseScreenSpaceCollisionCadence;
		public float CollisionMaxErrorPixels;
		public float MinDepthForError;
		public int MinCollisionEveryNSteps;
		public RayBeamRenderer.DebugDrawMode DebugMode;
		public float DebugNormalLen;
		public bool DebugOverlayOwnedByFilm;
		public int MaxSegPerRay;
	}

	private struct EffectiveFilmSettings
	{
		public int BaseWidth;
		public int BaseHeight;
		public float ResolutionScale;
		public int PixelStride;
		public int RowsPerFrame;
		public float MaxDistance;
		public float Opacity;
	}

		private struct EffectiveConfig
		{
		public EffectiveBroadphaseSettings Broadphase;
		public EffectiveSoftGateSettings SoftGate;
		public EffectiveRayMarchSettings RayMarch;
		public ResearchModeConfig Research;
		public RayBeamRenderer.SharedSnapshot SharedRaySnapshot;
		public EffectiveFilmSettings Film;
		public bool UpdateEveryFrame;
		public float UpdateEveryFrameBudgetMs;
		public int UpdateEveryFrameMaxRowsPerStep;
		public int RenderStepMaxMs;
		public int RenderStepMaxPixelsPerFrame;
		public int RenderStepMaxSegmentsPerFrame;
		public int RenderStepNoRowProgressRepeatLimit;
		public int TargetMsPerFrame;
		public int MinRowsPerFrame;
		public int MaxRowsPerFrameCap;
		public bool AutoRangeDepth;
		public float AutoRangeMin;
		public float AutoRangeMax;
		public float AutoRangeSmoothing;
		public float AutoRangeSafety;
		public int DepthHistoryFrames;
		public bool UseFieldGrid;
		public float FieldGridCellSize;
		public int FieldGridRebuildEveryNFrames;
		public float FieldGridBoundsPadding;
		public bool UseCameraPropsBetaGamma;
		public float TinySegmentSkipLen;
		public float EarlyOutDistanceEps;
		public bool UseAdaptiveSubsteps;
		public bool UseBandHitSkip;
		public float BandSkipHitThreshold;
		public int BandSkipFrames;
		public float BandSkipInvalidatePosDelta;
		public float BandSkipInvalidateBasisDelta;
		public float BandSkipInvalidateRangeDelta;
		public bool Pass1DoHitTest;
		public int Pass1ProbeEveryNSegments;
		public float Pass1ProbeMinTravelDelta;
		public bool UsePass2CollisionStride;
		public int Pass2CollisionStrideNear;
		public int Pass2CollisionStrideFar;
		public float Pass2CollisionStrideFarStartT;
		public float MinSegLenForStrideSkip;
		public float Pass2GeomEnvelopeRadiusScale;
		public float Pass2GeomEnvelopeAabbExpand;
		public bool AdaptiveTelemetryEnvelopeScalingEnabled;
		public string AdaptiveEnvelopeControllerMode;
		public string AdaptiveEnvelopePriorSource;
		public string AdaptiveEnvelopeThresholdStatistic;
		public float AdaptiveTelemetryEnvelopeLowThreshold;
		public float AdaptiveTelemetryEnvelopeHighThreshold;
		public float AdaptiveEnvelopeHotThresholdPercentile;
		public float AdaptiveEnvelopeWarmThresholdPercentile;
		public float AdaptiveEnvelopeRelaxedThresholdPercentile;
		public float AdaptiveEnvelopeTightScale;
		public float AdaptiveEnvelopeWarmScale;
		public float AdaptiveEnvelopeNeutralScale;
		public float AdaptiveEnvelopeRelaxedScale;
		public bool UseGeometryTLASPruning;
		public bool Pass2HitBackFaces;
		public bool Pass2HitFromInside;
		public bool Pass2ForceOnInstability;
		public bool Pass2ForceIfPrevHitLost;
		public int Pass2LogQuickRayMissSamples;
		public bool UseSingleProbeThenSubdivide;
		public bool NearestHitOnly;
		public bool UseInsightPlanePass2;
		public bool RenderStepPhaseLog;
		public bool RenderStepBandLog;
		public int DebugEveryNPixels;
		public int DebugMaxFilmRays;
		public bool DebugGeomPruneAuditEnabled;
		public int DebugGeomPruneAuditSamplesPerHealthWindow;
		public int DebugGeomPruneAuditMaxExtraRayTestsPerSample;
		public bool DebugGeomPruneAuditOnlyWhenCandidateZero;
		public int DebugGeomPruneAuditMaxMismatchLogsPerWindow;
		public bool EnableProfiling;
		public bool VerbosePerfLogs;
			public bool EnableFramePerf;
			public bool FramePerfVerbose;
			public int FramePerfLogEveryNFrames;
			public bool UseThreadedBands;
			public int ThreadedBandWorkerCount;
			public int ThreadedBandRowsPerChunk;
			public bool UseThreadedPass2CandidateEval;
			public int ThreadedPass2CandidateWorkers;
			public int ThreadedPass2CandidateRowsPerChunk;
			public bool UseThreadedPass2QueryResolve;
			public int ThreadedPass2QueryWorkers;
			public int ThreadedPass2QueryRowsPerChunk;
			public bool UseThreadedPass2LocalAccumulation;
			public int ThreadedPass2WorkerCount;
			public int ThreadedPass2RowsPerChunk;
			public bool NeedColliderNames;
		public bool FixtureDebugHitColoringEnabled;
		public string FixtureDebugSourceGroup;
		public string FixtureDebugBackgroundGroup;
		public Color FixtureDebugSourceHitColor;
		public Color FixtureDebugBackgroundHitColor;
		public Color FixtureDebugAbsorbedColor;
		public Color FixtureDebugMissColor;
		public bool FixtureDebugColorAuthorityEnabled;
		public bool FixtureDebugSourceHighlightEnabled;
		public bool FixtureDebugTraceEnabled;
		public int FixtureDebugTraceSampleModulo;
		public int FixtureDebugTraceMaxLogsPerStep;
		public bool UseFieldSourceCache;
		public int FieldSourceRefreshIntervalFrames;
		public FilmShadingMode ShadingMode;
		public bool FlipNormalToCamera;
		public Color SkyColor;
	}

	private struct RenderHealthSample
	{
		public int StepIndex;
		public int RowCursorBefore;
		public int RowCursorAfter;
		public int RowsAdvanced;
		public int BandsProcessed;
		public long TracedPixels;
		public int Hits;
		public int QuickRayZeroCount;
		public int HybridFallbackCount;
		public int HybridFallbackHitCount;
		public int HybridFallbackMissCount;
		public int HybridNoCandidateCount;
		public long GeomCandidatesTotal;
		public long GeomCandidatesSegments;
		public long GeomSegmentsQueried;
		public long GeomSegWithCandidates;
		public long GeomSegZeroCandidates;
		public long GeomPixelProcessed;
		public long GeomPixelHadAnyCandidates;
		public long GeomPixelNoCandidates;
		public long GeomHitAccepted;
		public long GeomHitRejected;
		public long GeomRayTestsTotal;
		public long GeomRayTestsAccepted;
		public long GeomRayTestsRejected;
		public long Pass2SampledSegments;
		public double Pass2RadiusSum;
		public float Pass2RadiusMax;
		public double Pass2EnvDiagSum;
		public float Pass2EnvDiagMax;
		public double Pass2EnvelopeInflationSum;
		public float Pass2EnvelopeInflationMax;
		public long Pass2CandidateCount0;
		public long Pass2CandidateCount1To2;
		public long Pass2CandidateCount3To8;
		public long Pass2CandidateCount9To32;
		public long Pass2CandidateCount33Plus;
		public long PruneAuditSamples;
		public long PruneAuditFalseNeg;
		public long PruneAuditFalsePos;
		public long PruneAuditCandidateZeroButBaselineHit;
		public double AvgStepsPerTracedPixel;
		public string BudgetExitReason;
		public bool GeomPruneRequested;
		public bool UseGeometryTLASPruning;
		public bool PruneAuditEnabled;
		public int PresentPixelsUpdated;
		public double PresentCoverageRatio;
		public int FramesToFullRefresh;
		public double TimeToFullRefreshMs;
		public double EffectiveFullRefreshFps;
		public bool FullRefreshMeasured;
		public double StepWallMs;
	}

	private readonly struct TileDescriptor
	{
		public readonly int StepIndex;
		public readonly int BandIndex;
		public readonly int SubtileIndex;
		public readonly int X;
		public readonly int Y;
		public readonly int Width;
		public readonly int Height;
		public readonly string StableId;

		public TileDescriptor(int stepIndex, int bandIndex, int subtileIndex, int x, int y, int width, int height, string stableId)
		{
			StepIndex = stepIndex;
			BandIndex = bandIndex;
			SubtileIndex = subtileIndex;
			X = x;
			Y = y;
			Width = width;
			Height = height;
			StableId = stableId ?? string.Empty;
		}
	}

	private readonly struct TileMetricSample
	{
		public readonly TileDescriptor Descriptor;
		public readonly long RaysTraced;
		public readonly long Hits;
		public readonly long SourceHits;
		public readonly long BackgroundHits;
		public readonly long UnclassifiedHits;
		public readonly long CandidateReferences;
		public readonly long CandidateSegments;
		public readonly long NoCandidatePixels;
		public readonly long CandidatePixels;
		public readonly long GeomPixelsProcessed;
		public readonly long GeomRayTests;
		public readonly string ExitReason;

		public TileMetricSample(
			in TileDescriptor descriptor,
			long raysTraced,
			long hits,
			long sourceHits,
			long backgroundHits,
			long unclassifiedHits,
			long candidateReferences,
			long candidateSegments,
			long noCandidatePixels,
			long candidatePixels,
			long geomPixelsProcessed,
			long geomRayTests,
			string exitReason)
		{
			Descriptor = descriptor;
			RaysTraced = raysTraced;
			Hits = hits;
			SourceHits = sourceHits;
			BackgroundHits = backgroundHits;
			UnclassifiedHits = unclassifiedHits;
			CandidateReferences = candidateReferences;
			CandidateSegments = candidateSegments;
			NoCandidatePixels = noCandidatePixels;
			CandidatePixels = candidatePixels;
			GeomPixelsProcessed = geomPixelsProcessed;
			GeomRayTests = geomRayTests;
			ExitReason = exitReason ?? string.Empty;
		}
	}

	private struct TileMetricAccumulator
	{
		public long RaysTraced;
		public long Hits;
		public long SourceHits;
		public long BackgroundHits;
		public long UnclassifiedHits;
		public long CandidateReferences;
		public long CandidateSegments;
		public long NoCandidatePixels;
		public long CandidatePixels;
		public long GeomPixelsProcessed;
		public long GeomRayTests;
	}

	private struct TileMetricPersistentPrior
	{
		public double RaysTraced;
		public double Hits;
		public double NoCandidatePixels;
		public double GeomPixelsProcessed;

		public void DecayInPlace(double decay)
		{
			double clamped = Math.Clamp(decay, 0.0, 1.0);
			RaysTraced *= clamped;
			Hits *= clamped;
			NoCandidatePixels *= clamped;
			GeomPixelsProcessed *= clamped;
		}

		public void Add(in TileMetricAccumulator subtile)
		{
			RaysTraced += Math.Max(0L, subtile.RaysTraced);
			Hits += Math.Max(0L, subtile.Hits);
			NoCandidatePixels += Math.Max(0L, subtile.NoCandidatePixels);
			GeomPixelsProcessed += Math.Max(0L, subtile.GeomPixelsProcessed);
		}

		public void AddWeighted(in TileMetricPersistentPrior prior, double weight)
		{
			double clamped = Math.Max(0.0, weight);
			if (clamped <= 0.0)
				return;
			RaysTraced += Math.Max(0.0, prior.RaysTraced) * clamped;
			Hits += Math.Max(0.0, prior.Hits) * clamped;
			NoCandidatePixels += Math.Max(0.0, prior.NoCandidatePixels) * clamped;
			GeomPixelsProcessed += Math.Max(0.0, prior.GeomPixelsProcessed) * clamped;
		}
	}

	private readonly struct TilePriorityRankInputs
	{
		public readonly int SubtileIndex;
		public readonly int X;
		public readonly int Width;
		public readonly TilePriorityCandidate Current;
		public readonly bool HasCurrent;
		public readonly TilePriorityCandidate Prior;
		public readonly bool HasPrior;
		public readonly double PriorBlendWeight;

		public TilePriorityRankInputs(
			int subtileIndex,
			int x,
			int width,
			in TilePriorityCandidate current,
			bool hasCurrent,
			in TilePriorityCandidate prior,
			bool hasPrior,
			double priorBlendWeight)
		{
			SubtileIndex = subtileIndex;
			X = x;
			Width = width;
			Current = current;
			HasCurrent = hasCurrent;
			Prior = prior;
			HasPrior = hasPrior;
			PriorBlendWeight = priorBlendWeight;
		}
	}

	private struct OverlayRollingSnapshot
	{
		public int Steps;
		public double StepMsTotal;
		public long RowsAdvanced;
		public long Hits;
		public long RayTests;
		public int RayTestsSamples;
		public double ElapsedSec;
	}

private sealed class OverlayRollingWindow
	{
		private readonly long[] _timestamps;
		private readonly double[] _stepMs;
		private readonly int[] _rowsAdvanced;
		private readonly int[] _hits;
		private readonly long[] _rayTests;
		private readonly byte[] _hasRayTests;
		private int _head;
		private int _count;
		private int _steps;
		private int _rayTestsSamples;
		private double _sumStepMs;
		private long _sumRowsAdvanced;
		private long _sumHits;
		private long _sumRayTests;
		private long _windowTicks;

		public OverlayRollingWindow(int capacity)
		{
			int cap = Math.Max(8, capacity);
			_timestamps = new long[cap];
			_stepMs = new double[cap];
			_rowsAdvanced = new int[cap];
			_hits = new int[cap];
			_rayTests = new long[cap];
			_hasRayTests = new byte[cap];
			_windowTicks = (long)(Stopwatch.Frequency * 1.0);
		}

		public void SetWindowSeconds(double seconds)
		{
			double clamped = Math.Clamp(seconds, 0.5, 2.0);
			long ticks = (long)(Stopwatch.Frequency * clamped);
			_windowTicks = Math.Max(1L, ticks);
		}

		public void Reset()
		{
			_head = 0;
			_count = 0;
			_steps = 0;
			_rayTestsSamples = 0;
			_sumStepMs = 0.0;
			_sumRowsAdvanced = 0;
			_sumHits = 0;
			_sumRayTests = 0;
		}

		public void AddSample(long nowTicks, double stepWallMs, int rowsAdvanced, int hits, bool hasRayTests, long rayTests)
		{
			Trim(nowTicks);
			if (_count == _timestamps.Length)
				RemoveHead();

			int idx = (_head + _count) % _timestamps.Length;
			_timestamps[idx] = nowTicks;
			_stepMs[idx] = stepWallMs;
			_rowsAdvanced[idx] = rowsAdvanced;
			_hits[idx] = hits;
			_rayTests[idx] = rayTests;
			_hasRayTests[idx] = hasRayTests ? (byte)1 : (byte)0;
			_count++;
			_steps++;
			_sumStepMs += stepWallMs;
			_sumRowsAdvanced += rowsAdvanced;
			_sumHits += hits;
			if (hasRayTests)
			{
				_sumRayTests += rayTests;
				_rayTestsSamples++;
			}
			Trim(nowTicks);
		}

		public OverlayRollingSnapshot Snapshot(long nowTicks)
		{
			Trim(nowTicks);
			double elapsedSec = 0.0;
			if (_count > 0)
			{
				long oldestTs = _timestamps[_head];
				long elapsedTicks = nowTicks - oldestTs;
				if (elapsedTicks > 0)
					elapsedSec = elapsedTicks / (double)Stopwatch.Frequency;
			}

			return new OverlayRollingSnapshot
			{
				Steps = _steps,
				StepMsTotal = _sumStepMs,
				RowsAdvanced = _sumRowsAdvanced,
				Hits = _sumHits,
				RayTests = _sumRayTests,
				RayTestsSamples = _rayTestsSamples,
				ElapsedSec = elapsedSec
			};
		}

		private void Trim(long nowTicks)
		{
			while (_count > 0)
			{
				long oldestTs = _timestamps[_head];
				long age = nowTicks - oldestTs;
				if (age <= _windowTicks)
					break;
				RemoveHead();
			}
		}

		private void RemoveHead()
		{
			if (_count <= 0)
				return;

			_sumStepMs -= _stepMs[_head];
			_sumRowsAdvanced -= _rowsAdvanced[_head];
			_sumHits -= _hits[_head];
			_steps--;
			if (_hasRayTests[_head] != 0)
			{
				_sumRayTests -= _rayTests[_head];
				_rayTestsSamples--;
			}

			_head = (_head + 1) % _timestamps.Length;
			_count--;
			if (_count == 0)
			{
				_head = 0;
			}
		}
	}

	private enum SoftGateDecisionReason
	{
		Allow = 0,
		Disabled,
		SegLenTooShort,
		NanMetric,
		ScoreTooLow,
		RandomNotSelected,
		BudgetAttemptCap,
		BudgetSubdivideCap,
		Guard,
		Other
	}

	private sealed class Pass1ThreadLocal
	{
		public PhysicsRayQueryParameters3D QuickRayParams;
		public long PhysQueries;
		public long EarlyStopPixels;
		public long StepsIntegrated;
		public long FieldEvals;
		public long Pass1Raycasts;
		public long Pass1ProbeHits;
		public long FieldGridHits;
		public long FieldGridMisses;
		public long FieldGridFallbacks;
		public long FieldSourceEvals;
	}

	private struct Pass2ResolvedSample
	{
		public int X;
		public int Y;
		public int Stride;
		public int GlobalPi;
		public int SubtileIndex;
		public int SegCount;
		public int SegOffset;
		public bool HadHit;
		public bool AbsorbedByInnerRadius;
		public bool NeedHitName;
		public bool PrevHadHit;
		public bool PrevHadHitForSoftGate;
		public bool TestedAnyInPass0ThisPixel;
		public bool SoftGateHitThisPixel;
		public float CandidateCount;
		public float QueryCount;
		public float ResolveCount;
		public float HitDistance;
		public Vector3 BestHp;
		public Vector3 BestHn;
		public ulong BestCid;
		public string HitName;
	}

	private struct Pass2ShadedSample
	{
		public Color Color;
		public string FixtureHitKind;
		public Color FixtureChosenDebugColor;
		public bool FixtureDebugColorChosen;
		public bool SkipShading;
	}

	private sealed class Pass2ChunkAccumulator
	{
		public int Hits;
		public int SourceHits;
		public int BackgroundHits;
		public int UnclassifiedHits;
		public int AbsorbedHits;
		public int MissHits;
		public int ShadingSkippedPixels;
	}

	private sealed class Pass2QueryResolveChunk
	{
		public int StartRow;
		public int EndRowExclusive;
		public System.Collections.Generic.List<Pass2ResolvedSample> Samples = new();
		public long TracedPixels;
		public long SegsIntegrated;
		public long SegsTested;
		public long PhysicsQueries;
		public long IntersectShapeCalls;
		public long SubdividedRayCalls;
		public long SubdividedRayQueries;
		public long SubdividedRaySubsteps;
		public long Pass2StrideSum;
		public long Pass2StrideCount;
		public long QueryUsec;
		public long ResolveUsec;
		public long PhysUsec;
		public ThreadedPass2GeomAccumulator Geom;
	}

	private struct ThreadedPass2GeomAccumulator
	{
		public long GeomSegmentsQueried;
		public long GeomSegWithCandidates;
		public long GeomSegZeroCandidates;
		public long GeomPixelProcessed;
		public long GeomPixelHadAnyCandidates;
		public long GeomPixelNoCandidates;
		public long GeomHitAccepted;
		public long GeomHitRejected;
		public long GeomRayTestsTotal;
		public long Pass2SampledSegments;
		public double Pass2RadiusSum;
		public float Pass2RadiusMax;
		public double Pass2EnvDiagSum;
		public float Pass2EnvDiagMax;
		public double Pass2EnvelopeInflationSum;
		public float Pass2EnvelopeInflationMax;
		public long Pass2CandidateCount0;
		public long Pass2CandidateCount1To2;
		public long Pass2CandidateCount3To8;
		public long Pass2CandidateCount9To32;
		public long Pass2CandidateCount33Plus;
	}

	private struct Pass2CandidateEvalRecord
	{
		public int CandidateStart;
		public int CandidateCount;
		public float EnvelopeRadius;
		public float EnvelopeDiag;
		public float EnvelopeInflation;
	}

	private sealed class Pass2CandidateEvalChunk
	{
		public int StartRow;
		public int EndRowExclusive;
		public int FirstRecordIndex;
		public int RecordCount;
		public long[] CandidateIds = Array.Empty<long>();
		public int CandidateSegments;
		public int CandidateSegmentsWithHits;
		public int CandidateSegmentsZero;
		public long CandidateReferences;
	}

	private bool _dbgOnce = false;
	private void EarlyOut(string why, bool enableProfiling)
	{
		//GD.PrintErr($"⛔ RenderStep early-out: {why} rowCursor={_rowCursor} cam={_cam?.GetPath()} rbr={_rbr?.GetPath()}");
		if (enableProfiling) GD.Print($"[EarlyOut] {why} rowCursor={_rowCursor} cam={_cam?.GetPath()} rbr={_rbr?.GetPath()}");

	}

	private bool TryAutoResolveRayBeamRenderer(out RayBeamRenderer rbr, out NodePath generatedPath)
	{
		rbr = null;
		generatedPath = default;

		if (_rbrRefAutoResolveAttempted)
			return false;
		_rbrRefAutoResolveAttempted = true;

		Node parent = GetParent();
		if (parent == null)
			return false;

		Node byName = parent.FindChild("RayBeamRenderer", recursive: true, owned: true);
		if (byName is RayBeamRenderer byNameRbr)
		{
			rbr = byNameRbr;
			generatedPath = GetPathTo(byNameRbr);
			return true;
		}

		var stack = new System.Collections.Generic.Stack<Node>();
		stack.Push(parent);
		while (stack.Count > 0)
		{
			Node current = stack.Pop();
			foreach (Node child in current.GetChildren())
			{
				if (child is RayBeamRenderer byTypeRbr)
				{
					rbr = byTypeRbr;
					generatedPath = GetPathTo(byTypeRbr);
					return true;
				}
				if (child.GetChildCount() > 0)
					stack.Push(child);
			}
		}

		return false;
	}

	private void ResolveRayBeamRendererReference()
	{
		if (_rbr != null && !IsInstanceValid(_rbr))
		{
			_rbr = null;
		}

		bool pathChanged = !_hasLastRbrResolvePath || RayBeamRendererPath != _lastRbrResolvePath;
		if (pathChanged)
		{
			_lastRbrResolvePath = RayBeamRendererPath;
			_hasLastRbrResolvePath = true;
			_rbrRefAutoResolveAttempted = false;
			_rbrRefLoggedAutoResolved = false;
			_rbrRefLoggedAutoResolveFailed = false;
		}

		if (!pathChanged && _rbr != null)
			return;

		if (RayBeamRendererPath.IsEmpty)
		{
			if (!_rbrRefLoggedPathEmpty)
			{
				_rbrRefLoggedPathEmpty = true;
				GD.Print("[RBRRef] path empty");
			}
			if (TryAutoResolveRayBeamRenderer(out RayBeamRenderer autoRbr, out NodePath autoPath))
			{
				_rbr = autoRbr;
				if (!_rbrRefLoggedAutoResolved)
				{
					_rbrRefLoggedAutoResolved = true;
					GD.Print($"[RBRRef] auto-resolved name={autoRbr.Name} path={autoPath}");
				}
				return;
			}
			if (!_rbrRefLoggedAutoResolveFailed)
			{
				_rbrRefLoggedAutoResolveFailed = true;
				GD.Print("[RBRRef] auto-resolve failed");
			}
			_rbr = null;
			return;
		}

		Node node = GetNodeOrNull(RayBeamRendererPath);
		if (node == null)
		{
			if (!_rbrRefLoggedResolveFailed)
			{
				_rbrRefLoggedResolveFailed = true;
				GD.Print($"[RBRRef] resolve failed path={RayBeamRendererPath}");
			}
			if (TryAutoResolveRayBeamRenderer(out RayBeamRenderer autoRbr, out NodePath autoPath))
			{
				_rbr = autoRbr;
				if (!_rbrRefLoggedAutoResolved)
				{
					_rbrRefLoggedAutoResolved = true;
					GD.Print($"[RBRRef] auto-resolved name={autoRbr.Name} path={autoPath}");
				}
				return;
			}
			if (!_rbrRefLoggedAutoResolveFailed)
			{
				_rbrRefLoggedAutoResolveFailed = true;
				GD.Print("[RBRRef] auto-resolve failed");
			}
			_rbr = null;
			return;
		}

		if (node is RayBeamRenderer rbr)
		{
			_rbr = rbr;
			if (!_rbrRefLoggedResolvedOk)
			{
				_rbrRefLoggedResolvedOk = true;
				GD.Print($"[RBRRef] resolved ok name={rbr.Name}");
			}
			return;
		}

		if (!_rbrRefLoggedWrongType)
		{
			_rbrRefLoggedWrongType = true;
			GD.Print("[RBRRef] wrong type at path");
		}
		if (TryAutoResolveRayBeamRenderer(out RayBeamRenderer autoRbrWrongType, out NodePath autoPathWrongType))
		{
			_rbr = autoRbrWrongType;
			if (!_rbrRefLoggedAutoResolved)
			{
				_rbrRefLoggedAutoResolved = true;
				GD.Print($"[RBRRef] auto-resolved name={autoRbrWrongType.Name} path={autoPathWrongType}");
			}
			return;
		}
		if (!_rbrRefLoggedAutoResolveFailed)
		{
			_rbrRefLoggedAutoResolveFailed = true;
			GD.Print("[RBRRef] auto-resolve failed");
		}
		_rbr = null;
	}

	// ===== Core Update Loop =====
	public override void _Ready()
	{
		GD.Print("✅ GrinFilmCamera READY: ", GetPath());

		_cam = GetViewport().GetCamera3D();
		// DECISION: abort if no active camera.
		if (_cam == null)
		{
			GD.PushError("GrinFilmCamera: No active Camera3D found in viewport.");
			return;
		}
		_physicsRunsOnSeparateThread = ProjectSettings.GetSetting("physics/3d/run_on_separate_thread").AsBool();
		SetPhysicsProcess(true);
		_lastCameraInstanceId = _cam.GetInstanceId();
		_hasLastCameraInstanceId = true;

		GD.Print($"[RBRRef][Startup] configured path={RayBeamRendererPath}");
		ResolveRayBeamRendererReference();
		GD.Print($"[RBRRef][Startup] resolved={_rbr != null}");
		GD.Print("RayBeamRenderer found? ", _rbr != null);
		// DECISION: warn if RayBeamRenderer is missing, but continue so mirrors can update.
		if (_rbr == null)
		{
			GD.PushError("GrinFilmCamera: RayBeamRendererPath missing or invalid.");
		}

			_rng.Randomize();
			ApplyTileMetricsCmdArgs();
			ApplyThreadingCmdArgs();
			ApplyDeterministicBenchmarkCmdArgs();
	
			// DECISION: optionally apply presets at startup via the single orchestration path.
		if (!ApplyPresetOnReady)
		{
			_lastPreset = Preset;
			_lastQualityMode = QualityMode;
			_lastPerformancePreset = PerformancePreset;
			_presetSceneDirty = false;
			_presetPerfDirty = false;
			_presetQualityDirty = false;
			_presetDirtyReason = "";
		}
		SyncAndApplyIfDirty("ready", force: ApplyPresetOnReady);
		SetProcessUnhandledInput(true);
		CaptureRuntimeMacroBaselineDefaults();
		if (RuntimeMacroApplyOnReady)
		{
			TryApplyRuntimeMacroMode(RuntimeMacroSelectedMode, "ready_apply");
		}
		else if (RuntimeMacroSwitchingAllowed() && RuntimeMacroHotkeysEnabled)
		{
			GD.Print(
				$"[RuntimeMacroMode] hotkeys cycle={RuntimeMacroCycleKey} accurate={RuntimeMacroAccurateKey} tight08={RuntimeMacroTight08Key} cheapMotion={RuntimeMacroCheapMotionKey} settleRefine={RuntimeMacroSettleRefineKey} " +
				$"startupMode={GetRuntimeMacroModeLabel(RuntimeMacroSelectedMode)} applyOnReady={(RuntimeMacroApplyOnReady ? 1 : 0)}");
		}

		if (!IsProcessing() && !_warnedNotProcessing)
		{
			_warnedNotProcessing = true;
			GD.PrintErr("GrinFilmCamera: Node is not processing; FrameSnapshotBus will not update.");
		}
		if (!UpdateEveryFrame && !_warnedNotProcessing)
		{
			_warnedNotProcessing = true;
			GD.PrintErr("GrinFilmCamera: UpdateEveryFrame is false; FrameSnapshotBus will not update.");
		}

    	// ⛔ Freeze beam rebuilds while film camera is active
		// CROSS-CLASS CONTRACT: Freeze RayBeamRenderer rebuilds while film camera is active.
		// ASSUMPTION: film pass owns ray stability; external rebuilds would desync buffers.
		if (_rbr != null)
			_rbr.AllowRebuild = false;

		// DECISION: RenderStep reads only the resolved effective config (no direct exported-field reads).
		ResolveEffectiveConfig(out EffectiveConfig cfg);
		LogEffectiveConfigIfChanged(in cfg);
		_rangeFar = cfg.Film.MaxDistance;
		_filmView = GetNodeOrNull<TextureRect>(FilmViewPath);
		GD.Print("FilmView found? ", _filmView != null);

		// EFFECT: allocate film image/texture buffers as needed.
		EnsureFilmImageSize(in cfg);

		// DECISION: if FilmViewPath is set, use it; otherwise build overlay.
		if (_filmView != null)
		{
			_filmView.Texture = _tex;
		}
		else
		{
		// DECISION: otherwise auto-create an overlay for display.
		var layer = new CanvasLayer();
		AddChild(layer);

			_overlayRect = new TextureRect();
			_overlayRect.Texture = _tex;

			// Godot 4 settings
			_overlayRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			_overlayRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;

			_overlayRect.AnchorLeft = 0;
			_overlayRect.AnchorTop = 0;
			_overlayRect.AnchorRight = 1;
			_overlayRect.AnchorBottom = 1;
			_overlayRect.OffsetLeft = 0;
			_overlayRect.OffsetTop = 0;
			_overlayRect.OffsetRight = 0;
			_overlayRect.OffsetBottom = 0;

		// EFFECT: nearest filtering keeps pixelated look for low-res film.
		_overlayRect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			layer.AddChild(_overlayRect);

			GD.Print("GrinFilmCamera: No FilmViewPath set, created overlay TextureRect.");
		}
		UpdateFilmOpacity();

		_filmOverlay = GetNodeOrNull<FilmOverlay2D>(FilmOverlayPath);
		ApplyHudOverlayVisualSettings();

		// Mirror RayBeamRenderer snapshot once after reference resolution.
		{
			RayBeamRenderer.SharedSnapshot snap = _rbr != null ? _rbr.GetSharedSnapshot() : default;
			UpdateSharedSnapshotMirror(in snap, force: true);
			GD.Print($"[RBRRef][Startup] HasRenderer(after mirror)={SharedRbrHasRenderer}");
		}

		GD.Print("✅ GrinFilmCamera ready. Rendering film.");
			_smartScaleEnableEdgeArmed = SmartScaleEnabled;
			_smartScaleRunOnReadyDeferred = SmartScaleRunOnReady;
			CallDeferred(nameof(EmitFixtureCurvatureDisabledWarning));
		}

	private static bool IsUsablePhysicsSpaceState(PhysicsDirectSpaceState3D space)
	{
		return space != null && GodotObject.IsInstanceValid(space);
	}

	private readonly struct TilePriorityCandidate
	{
		public readonly int SubtileIndex;
		public readonly int X;
		public readonly int Width;
		public readonly long Hits;
		public readonly long SourceHits;
		public readonly long BackgroundHits;
		public readonly long UnclassifiedHits;
		public readonly long Rays;
		public readonly long NoCandidatePixels;
		public readonly long GeomPixelsProcessed;
		public readonly double HitYield;
		public readonly double NoCandidateRatio;

		public TilePriorityCandidate(int subtileIndex, int x, int width, in TileMetricAccumulator subtile)
		{
			SubtileIndex = subtileIndex;
			X = x;
			Width = width;
			Hits = subtile.Hits;
			SourceHits = subtile.SourceHits;
			BackgroundHits = subtile.BackgroundHits;
			UnclassifiedHits = subtile.UnclassifiedHits;
			Rays = subtile.RaysTraced;
			NoCandidatePixels = subtile.NoCandidatePixels;
			GeomPixelsProcessed = subtile.GeomPixelsProcessed;
			HitYield = Rays > 0 ? (double)Hits / Rays : 0.0;
			NoCandidateRatio = GeomPixelsProcessed > 0 ? (double)NoCandidatePixels / GeomPixelsProcessed : 1.0;
		}
	}

	private void ClearCachedRenderSpaceState()
	{
		_cachedRenderSpaceState = null;
		_cachedRenderSpaceWorldId = 0;
		_hasCachedRenderSpaceWorldId = false;
	}

	private void RefreshCachedRenderSpaceState()
	{
		Camera3D cam = _cam;
		if (cam == null || !GodotObject.IsInstanceValid(cam))
		{
			cam = GetViewport()?.GetCamera3D();
			if (cam != null && GodotObject.IsInstanceValid(cam))
				_cam = cam;
		}

		if (cam == null || !GodotObject.IsInstanceValid(cam))
		{
			ClearCachedRenderSpaceState();
			return;
		}

		World3D world = cam.GetWorld3D();
		if (world == null || !GodotObject.IsInstanceValid(world))
		{
			ClearCachedRenderSpaceState();
			return;
		}

		_cachedRenderSpaceWorldId = world.GetInstanceId();
		_hasCachedRenderSpaceWorldId = true;
		try
		{
			_cachedRenderSpaceState = world.DirectSpaceState;
			if (!IsUsablePhysicsSpaceState(_cachedRenderSpaceState))
				ClearCachedRenderSpaceState();
		}
		catch (NullReferenceException)
		{
			ClearCachedRenderSpaceState();
		}
		catch (ObjectDisposedException)
		{
			ClearCachedRenderSpaceState();
		}
	}

	private bool TryResolveRenderSpaceState(out PhysicsDirectSpaceState3D space, out string source)
	{
		space = null;
		source = "unknown";

		Camera3D cam = _cam;
		if (cam == null || !GodotObject.IsInstanceValid(cam))
		{
			cam = GetViewport()?.GetCamera3D();
			if (cam != null && GodotObject.IsInstanceValid(cam))
				_cam = cam;
		}

		if (cam == null || !GodotObject.IsInstanceValid(cam))
		{
			source = "no_camera";
			return false;
		}

		World3D world = cam.GetWorld3D();
		if (world == null || !GodotObject.IsInstanceValid(world))
		{
			source = "no_world";
			return false;
		}

		ulong worldId = world.GetInstanceId();
		if (_hasCachedRenderSpaceWorldId
			&& _cachedRenderSpaceWorldId == worldId
			&& IsUsablePhysicsSpaceState(_cachedRenderSpaceState))
		{
			space = _cachedRenderSpaceState;
			source = "physics_cache";
			return true;
		}

		if (_physicsRunsOnSeparateThread)
		{
			if (!_hasCachedRenderSpaceWorldId)
			{
				source = "physics_cache_missing";
				return false;
			}
			if (_cachedRenderSpaceWorldId != worldId)
			{
				source = "physics_cache_world_mismatch";
				return false;
			}
			if (!IsUsablePhysicsSpaceState(_cachedRenderSpaceState))
			{
				source = "physics_cache_empty";
				return false;
			}

			space = _cachedRenderSpaceState;
			source = "physics_cache";
			return true;
		}

		try
		{
			space = world.DirectSpaceState;
		}
		catch (NullReferenceException)
		{
			source = "direct_exception_nullref";
			return false;
		}
		catch (ObjectDisposedException)
		{
			source = "direct_exception_disposed";
			return false;
		}
		if (!IsUsablePhysicsSpaceState(space))
		{
			source = "direct_unavailable";
			return false;
		}

		source = "direct";
		return true;
	}

	private void LogRenderSpaceUnavailableOnce(string source, bool pass1ProbeRequested, string reason)
	{
		if (_renderSpaceUnavailableWarned)
			return;

		_renderSpaceUnavailableWarned = true;
		GD.PushWarning(
			$"[RenderSpace][WARN] scene={ResolveHudSceneName()} fixture={ResolveHudFixtureName()} " +
			$"mode={ResolveHudModePath()} separateThread={(_physicsRunsOnSeparateThread ? 1 : 0)} " +
			$"source={source} pass1ProbeRequested={(pass1ProbeRequested ? 1 : 0)} reason={reason}");
	}

	private static string FormatRenderDiagToken(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
	}

	private bool TryIntersectRenderSpaceRaySafe(
		PhysicsDirectSpaceState3D space,
		PhysicsRayQueryParameters3D query,
		bool pass1ProbeEnabled,
		string queryKind,
		string renderSpaceSource,
		string sceneName,
		string fixtureName,
		string modeToken,
		ref int warnedState,
		out Godot.Collections.Dictionary hit)
	{
		hit = new Godot.Collections.Dictionary();
		bool spaceUsable = IsUsablePhysicsSpaceState(space);
		bool queryValid = query != null;
		string sceneToken = FormatRenderDiagToken(sceneName);
		string fixtureToken = FormatRenderDiagToken(fixtureName);
		string modeValue = FormatRenderDiagToken(modeToken);
		string sourceToken = FormatRenderDiagToken(renderSpaceSource);
		string queryToken = FormatRenderDiagToken(queryKind);

		if (!spaceUsable || !queryValid)
		{
			if (Interlocked.Exchange(ref warnedState, 1) == 0)
			{
				GD.PushWarning(
					$"[RenderSpace][RayGuard] scene={sceneToken} fixture={fixtureToken} mode={modeValue} " +
					$"queryKind={queryToken} source={sourceToken} separateThread={(_physicsRunsOnSeparateThread ? 1 : 0)} " +
					$"spaceNull={(space == null ? 1 : 0)} spaceUsable={(spaceUsable ? 1 : 0)} " +
					$"quickRayValid={(queryValid ? 1 : 0)} pass1ProbeEnabled={(pass1ProbeEnabled ? 1 : 0)} " +
					$"exception=none reason=skip_query_without_crash");
			}
			return false;
		}

		try
		{
			hit = space.IntersectRay(query);
			return true;
		}
		catch (NullReferenceException ex)
		{
			if (Interlocked.Exchange(ref warnedState, 1) == 0)
			{
				GD.PushWarning(
					$"[RenderSpace][RayGuard] scene={sceneToken} fixture={fixtureToken} mode={modeValue} " +
					$"queryKind={queryToken} source={sourceToken} separateThread={(_physicsRunsOnSeparateThread ? 1 : 0)} " +
					$"spaceNull={(space == null ? 1 : 0)} spaceUsable={(spaceUsable ? 1 : 0)} " +
					$"quickRayValid={(queryValid ? 1 : 0)} pass1ProbeEnabled={(pass1ProbeEnabled ? 1 : 0)} " +
					$"exception={ex.GetType().Name} reason=skip_query_without_crash");
			}
			return false;
		}
		catch (ObjectDisposedException ex)
		{
			if (Interlocked.Exchange(ref warnedState, 1) == 0)
			{
				GD.PushWarning(
					$"[RenderSpace][RayGuard] scene={sceneToken} fixture={fixtureToken} mode={modeValue} " +
					$"queryKind={queryToken} source={sourceToken} separateThread={(_physicsRunsOnSeparateThread ? 1 : 0)} " +
					$"spaceNull={(space == null ? 1 : 0)} spaceUsable={(spaceUsable ? 1 : 0)} " +
					$"quickRayValid={(queryValid ? 1 : 0)} pass1ProbeEnabled={(pass1ProbeEnabled ? 1 : 0)} " +
					$"exception={ex.GetType().Name} reason=skip_query_without_crash");
			}
			return false;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		RefreshCachedRenderSpaceState();
	}

		public override void _Process(double delta)
	{
		DebugLogConfig.EnableSnapshotLog = DebugSnapshotLog;
		DebugLogConfig.SnapshotLogIntervalSec = Mathf.Max(0.05f, DebugSnapshotIntervalSec);
		DebugLogConfig.EnableProbeLog = DebugProbeLog;
		DebugLogConfig.ProbeLogIntervalSec = Mathf.Max(0.05f, DebugProbeIntervalSec);
		DebugLogConfig.EnableGeomRejectSample = DebugGeomRejectSampleEnabled;
		UpdateRuntimeMacroMotionState();

			SyncAndApplyIfDirty("process");
			// Keep broadphase controls in sync each frame so the inspector reflects effective state.
			UpdateBroadphaseEffectiveState();
			PumpSmartScaleController();
			if (!UpdateEveryFrame && ShouldEmitMetadataOnlyOverlay())
			{
				EmitRenderMetricsOverlay();
			}
			// DECISION: only render when UpdateEveryFrame is enabled.
			if (!UpdateEveryFrame) return;
		if (_physicsRunsOnSeparateThread && !TryResolveRenderSpaceState(out _, out string renderSpaceSource))
		{
			LogRenderSpaceUnavailableOnce(renderSpaceSource, Pass1DoHitTest, "render_frame_skipped_until_physics_space_ready");
			return;
		}
		_renderSpaceUnavailableWarned = false;
		RenderFrameBackend(delta);
	}

	private void RenderFrameBackend(double delta)
	{
		var snapshot = GodotAdapter.SnapshotBuilder.BuildFromGodotScene(GetTree().CurrentScene);
		var cam = _cam;
		if (cam == null || !IsInstanceValid(cam))
		{
			cam = GetViewport()?.GetCamera3D();
			if (cam != null && IsInstanceValid(cam))
			{
				_cam = cam;
			}
		}
		CurvatureBoundGrid grid = null;
		if (cam != null && IsInstanceValid(cam))
		{
			var camPos = cam.GlobalPosition;
			var camPosNum = new System.Numerics.Vector3(camPos.X, camPos.Y, camPos.Z);
			grid = CurvatureBoundGrid.BuildAroundCamera(
				camPosNum,
				cellSize: CurvatureGridCellSize,
				dimX: CurvatureGridDimX,
				dimY: CurvatureGridDimY,
				dimZ: CurvatureGridDimZ,
				snapshot);
		}
		else if (!_warnedNoCameraForGrid)
		{
			_warnedNoCameraForGrid = true;
			GD.PrintErr("GrinFilmCamera: No valid Camera3D for CurvatureGrid build.");
		}

		snapshot = new SceneSnapshot
		{
			Instances = snapshot.Instances,
			Fields = snapshot.Fields,
			FieldParams = snapshot.FieldParams,
			FieldTLAS = snapshot.FieldTLAS,
			Geometry = snapshot.Geometry,
			GeometryTLAS = snapshot.GeometryTLAS,
			CurvatureGrid = grid
		};

		_frameId++;
		FrameSnapshotBus.Set(snapshot, _frameId);
		ThrottleBusLog(delta, snapshot);
		ThrottleSnapshotSummary(delta, snapshot);

		_legacyBackend ??= new RenderBackends.LegacyBackend(this);
		_coreBackend ??= new RenderBackends.CoreBackend();

		switch (BackendMode)
		{
			case RenderBackends.BackendMode.Core:
				_coreBackend.RenderFrame(snapshot);
				RenderLegacyBackendTimed(snapshot);
				break;
			case RenderBackends.BackendMode.Compare:
				// TODO: compare mode; for now keep legacy render to avoid breaking output.
				RenderLegacyBackendTimed(snapshot);
				break;
			default:
				RenderLegacyBackendTimed(snapshot);
				break;
		}
	}

	private void RenderLegacyBackendTimed(SceneSnapshot snapshot)
	{
		long t0 = Stopwatch.GetTimestamp();
		_legacyBackend.RenderFrame(snapshot);
		long t1 = Stopwatch.GetTimestamp();

		_lastFrameRenderMs = (t1 - t0) * 1000.0 / Stopwatch.Frequency;
		EmitRenderMetricsOverlay();
	}

	private void PumpSmartScaleController()
	{
		if (_smartScaleRunOnReadyDeferred)
		{
			_smartScaleRunOnReadyDeferred = false;
			_smartScaleRunRequested = true;
			GD.Print("[SmartScale][Inspector] run-on-ready requested.");
		}

		if (SmartScaleEnabled && !_smartScaleEnableEdgeArmed)
		{
			_smartScaleRunRequested = true;
			GD.Print("[SmartScale][Inspector] enabled in inspector; scheduling SmartScale run.");
		}
		_smartScaleEnableEdgeArmed = SmartScaleEnabled;

		if (_smartScaleRunInProgress)
		{
			ObserveSmartScaleProbeProgress();
			return;
		}

		if (!_smartScaleRunRequested)
		{
			return;
		}

		if (!IsSmartScaleSafeBoundary())
		{
			RequestSmartScaleSafeBoundaryAbortIfNeeded();
			return;
		}

		BeginSmartScaleRun();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!RuntimeMacroHotkeysEnabled || e is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
			return;

		RuntimeMacroMode requestedMode;
		bool applyRequested = true;
		switch (keyEvent.Keycode)
		{
			case RuntimeMacroCycleKey:
				requestedMode = GetNextRuntimeMacroMode(RuntimeMacroSelectedMode);
				break;
			case RuntimeMacroAccurateKey:
				requestedMode = RuntimeMacroMode.Accurate;
				break;
			case RuntimeMacroTight08Key:
				requestedMode = RuntimeMacroMode.Tight08;
				break;
			case RuntimeMacroCheapMotionKey:
				requestedMode = RuntimeMacroMode.CheapMotion;
				break;
			case RuntimeMacroSettleRefineKey:
				requestedMode = RuntimeMacroMode.SettleRefine;
				break;
			default:
				applyRequested = false;
				requestedMode = RuntimeMacroSelectedMode;
				break;
		}

		if (!applyRequested)
			return;

		RuntimeMacroSelectedMode = requestedMode;
		if (TryApplyRuntimeMacroMode(requestedMode, keyEvent.Keycode == RuntimeMacroCycleKey ? "hotkey_cycle" : "hotkey_direct"))
		{
			GetViewport()?.SetInputAsHandled();
		}
	}

	private bool IsSmartScaleSafeBoundary()
	{
		if (Interlocked.CompareExchange(ref _renderStepActive, 0, 0) != 0)
		{
			return false;
		}
		if (_pendingBandHasPass1)
		{
			return false;
		}
		return _rowCursor == 0;
	}

	private void RequestSmartScaleSafeBoundaryAbortIfNeeded()
	{
		if (_smartScalePendingSafeBoundaryAbort)
		{
			return;
		}
		if (Interlocked.CompareExchange(ref _renderStepActive, 0, 0) != 0)
		{
			GD.Print("[SmartScale][Inspector] waiting for active RenderStep to finish before run.");
			return;
		}

		if (_rowCursor != 0 || _pendingBandHasPass1)
		{
			GD.Print(
				$"[SmartScale][Inspector] aborting current band for safe run boundary row={_rowCursor} pending_pass2={(_pendingBandHasPass1 ? 1 : 0)}.");
			ResetFilmPassManual();
		}

		_smartScalePendingSafeBoundaryAbort = true;
	}

	private void BeginSmartScaleRun()
	{
		_smartScalePendingSafeBoundaryAbort = false;
		_smartScaleRunRequested = false;

		if (SmartScaleGoal != SmartScaleGoalMode.MaxHits)
		{
			GD.PushWarning($"[SmartScale][Inspector] Unsupported goal={SmartScaleGoal}; only max_hits is supported.");
			return;
		}

		_smartScaleSavedConfig = CaptureSmartScaleSavedConfig();
		_smartScaleSavedConfigValid = true;
		_smartScaleProbeResults.Clear();
		_smartScaleProbePlan = BuildSmartScaleProbePlan();
		_smartScaleActiveProbeIndex = -1;
		_smartScaleRunInProgress = true;

		GD.Print(
			$"[SmartScale][Inspector] begin goal={GetSmartScaleGoalToken()} budget_mode={GetSmartScaleBudgetModeToken()} budget_n={GetSmartScaleBudgetNResolved()} probes={_smartScaleProbePlan.Length}");

		if (_smartScaleProbePlan.Length == 0)
		{
			FinalizeSmartScaleRun();
			return;
		}

		StartSmartScaleProbe(0);
	}

	private void StartSmartScaleProbe(int probeIndex)
	{
		if (probeIndex < 0 || probeIndex >= _smartScaleProbePlan.Length)
		{
			FinalizeSmartScaleRun();
			return;
		}

		_smartScaleActiveProbeIndex = probeIndex;
		_smartScaleProbeRenderStepCalls = 0;
		_smartScaleProbeRowsAdvancedTotal = 0;
		_smartScaleProbeBudgetStopCount = 0;
		_smartScaleProbeGeomPixProcessedRaw = 0;
		_smartScaleProbeGeomRayTestsTotalRaw = 0;
		_smartScaleProbeLastStepObserved = _renderHealthStepIndex;

		ResetRenderHealthWindowForRunStart();
		ResetFilmPassManual();
		UpdateEveryFrame = true;

		SmartScaleProbeConfig probe = _smartScaleProbePlan[probeIndex];
		ApplySmartScaleProbeOverrides(in probe);
		GD.Print(
			$"[SmartScale][ProbeStart] probe={probe.ProbeId} summary={probe.Summary} budget_mode={GetSmartScaleBudgetModeToken()} budget_n={GetSmartScaleBudgetNResolved()}");
	}

	private void ObserveSmartScaleProbeProgress()
	{
		if (_smartScaleActiveProbeIndex < 0 || _smartScaleActiveProbeIndex >= _smartScaleProbePlan.Length)
		{
			return;
		}
		if (_renderHealthCount <= 0)
		{
			return;
		}

		RenderHealthSample latest = GetRenderHealthSampleFromEnd(0);
		if (latest.StepIndex <= _smartScaleProbeLastStepObserved)
		{
			return;
		}

		int stepGap = latest.StepIndex - _smartScaleProbeLastStepObserved;
		int consumeCount = Math.Min(Math.Max(1, stepGap), _renderHealthCount);
		for (int offset = consumeCount - 1; offset >= 0; offset--)
		{
			RenderHealthSample sample = GetRenderHealthSampleFromEnd(offset);
			if (sample.StepIndex <= _smartScaleProbeLastStepObserved)
			{
				continue;
			}

			_smartScaleProbeLastStepObserved = sample.StepIndex;
			_smartScaleProbeRenderStepCalls++;
			_smartScaleProbeRowsAdvancedTotal += Math.Max(0, sample.RowsAdvanced);
			_smartScaleProbeGeomPixProcessedRaw += Math.Max(0L, sample.GeomPixelProcessed);
			_smartScaleProbeGeomRayTestsTotalRaw += Math.Max(0L, sample.GeomRayTestsTotal);
			if (!string.IsNullOrWhiteSpace(sample.BudgetExitReason) &&
				!string.Equals(sample.BudgetExitReason, "none", StringComparison.OrdinalIgnoreCase))
			{
				_smartScaleProbeBudgetStopCount++;
			}
		}

		if (_smartScaleProbeRenderStepCalls < SmartScaleWarmupSamples)
		{
			return;
		}
		if (!HasReachedSmartScaleProbeBudget())
		{
			return;
		}

		CompleteSmartScaleProbe();
	}

	private bool HasReachedSmartScaleProbeBudget()
	{
		int budgetN = GetSmartScaleBudgetNResolved();
		if (SmartScaleBudgetMode == SmartScaleProbeBudgetMode.RowsAdvanced)
		{
			return _smartScaleProbeRowsAdvancedTotal >= budgetN;
		}
		return _smartScaleProbeRenderStepCalls >= budgetN;
	}

	private int GetSmartScaleBudgetNResolved()
	{
		return Math.Max(1, SmartScaleBudgetN);
	}

	private string GetSmartScaleGoalToken()
	{
		return SmartScaleGoal == SmartScaleGoalMode.MaxHits ? "max_hits" : "unknown";
	}

	private string GetSmartScaleBudgetModeToken()
	{
		return SmartScaleBudgetMode == SmartScaleProbeBudgetMode.RowsAdvanced
			? "rows_advanced"
			: "renderstep_calls";
	}

	private void CompleteSmartScaleProbe()
	{
		SmartScaleProbeConfig probe = _smartScaleProbePlan[_smartScaleActiveProbeIndex];
		bool hasTrust = TryGetLatestRenderHealthForTesting(out bool trusted, out _, out _, out string trustReason);
		var result = new SmartScaleProbeResult
		{
			Probe = probe,
			Trusted = trusted,
			TrustKnown = hasTrust,
			TrustReason = hasTrust ? trustReason : "na",
			RenderStepCalls = _smartScaleProbeRenderStepCalls,
			RowsAdvancedTotal = _smartScaleProbeRowsAdvancedTotal,
			BudgetStopCount = _smartScaleProbeBudgetStopCount,
			GeomPixProcessedRaw = _smartScaleProbeGeomPixProcessedRaw,
			GeomRayTestsTotalRaw = _smartScaleProbeGeomRayTestsTotalRaw
		};

		_smartScaleProbeResults.Add(result);
		GD.Print(
			$"[SmartScale][ProbeResult] probe={probe.ProbeId} trust={(result.TrustKnown ? (result.Trusted ? 1 : 0) : -1)} trust_reason={result.TrustReason} " +
			$"geomPixProcessedRaw={result.GeomPixProcessedRaw} geomRayTestsTotalRaw={result.GeomRayTestsTotalRaw} " +
			$"budget_mode={GetSmartScaleBudgetModeToken()} budget_n={GetSmartScaleBudgetNResolved()} renderstep_calls={result.RenderStepCalls} rows_advanced_total={result.RowsAdvancedTotal}");

		// Preserve harness behavior: baseline trusted in max_hits can early stop.
		if (_smartScaleActiveProbeIndex == 0 && result.TrustKnown && result.Trusted)
		{
			GD.Print("[SmartScale][Decision] early_stop=1 reason=baseline_trusted");
			FinalizeSmartScaleRun();
			return;
		}

		int nextProbe = _smartScaleActiveProbeIndex + 1;
		if (nextProbe >= _smartScaleProbePlan.Length)
		{
			FinalizeSmartScaleRun();
			return;
		}

		StartSmartScaleProbe(nextProbe);
	}

	private static int GetSmartScaleTrustRank(in SmartScaleProbeResult probe)
	{
		if (!probe.TrustKnown) return 0;
		return probe.Trusted ? 2 : 1;
	}

	private static bool IsSmartScaleProbeBetter(in SmartScaleProbeResult candidate, in SmartScaleProbeResult incumbent)
	{
		int cTrust = GetSmartScaleTrustRank(in candidate);
		int iTrust = GetSmartScaleTrustRank(in incumbent);
		if (cTrust != iTrust) return cTrust > iTrust;

		if (candidate.GeomPixProcessedRaw != incumbent.GeomPixProcessedRaw)
		{
			return candidate.GeomPixProcessedRaw > incumbent.GeomPixProcessedRaw;
		}
		if (candidate.BudgetStopCount != incumbent.BudgetStopCount)
		{
			return candidate.BudgetStopCount < incumbent.BudgetStopCount;
		}
		return candidate.RenderStepCalls < incumbent.RenderStepCalls;
	}

	private bool TrySelectBestSmartScaleProbe(out SmartScaleProbeResult best)
	{
		best = default;
		if (_smartScaleProbeResults.Count <= 0)
		{
			return false;
		}

		best = _smartScaleProbeResults[0];
		for (int i = 1; i < _smartScaleProbeResults.Count; i++)
		{
			SmartScaleProbeResult candidate = _smartScaleProbeResults[i];
			if (IsSmartScaleProbeBetter(in candidate, in best))
			{
				best = candidate;
			}
		}
		return true;
	}

	private void FinalizeSmartScaleRun()
	{
		bool hasBest = TrySelectBestSmartScaleProbe(out SmartScaleProbeResult best);

		if (_smartScaleSavedConfigValid)
		{
			RestoreSmartScaleSavedConfig(in _smartScaleSavedConfig);
		}

		if (hasBest)
		{
			ApplySmartScaleProbeOverrides(in best.Probe);
			GD.Print(
				$"[SmartScale][Summary] probes={_smartScaleProbeResults.Count} best={best.Probe.ProbeId} best_trust={(best.TrustKnown ? (best.Trusted ? 1 : 0) : -1)} " +
				$"best_geomPix={best.GeomPixProcessedRaw} budgetStops={best.BudgetStopCount}");
		}
		else
		{
			GD.PushWarning("[SmartScale][Inspector] no valid probe result; restored original settings.");
		}

		_smartScaleRunInProgress = false;
		_smartScaleActiveProbeIndex = -1;
		_smartScalePendingSafeBoundaryAbort = false;
		_smartScaleRunRequested = false;
		_smartScaleSavedConfigValid = false;
	}

	private SmartScaleSavedConfig CaptureSmartScaleSavedConfig()
	{
		return new SmartScaleSavedConfig
		{
			UpdateEveryFrame = UpdateEveryFrame,
			PixelStride = PixelStride,
			UsePass2CollisionStride = UsePass2CollisionStride,
			TargetMsPerFrame = TargetMsPerFrame,
			UpdateEveryFrameBudgetMs = UpdateEveryFrameBudgetMs,
			UpdateEveryFrameMaxRowsPerStep = UpdateEveryFrameMaxRowsPerStep,
			RowsPerFrame = RowsPerFrame,
			MaxRowsPerFrameCap = MaxRowsPerFrameCap,
			RenderStepMaxMs = RenderStepMaxMs,
			RenderStepMaxPixelsPerFrame = RenderStepMaxPixelsPerFrame,
			RenderStepMaxSegmentsPerFrame = RenderStepMaxSegmentsPerFrame,
			BroadphaseControlMode = BroadphaseControlMode,
			BroadphasePolicy = BroadphasePolicy
		};
	}

	private void RestoreSmartScaleSavedConfig(in SmartScaleSavedConfig cfg)
	{
		UpdateEveryFrame = cfg.UpdateEveryFrame;
		PixelStride = cfg.PixelStride;
		UsePass2CollisionStride = cfg.UsePass2CollisionStride;
		TargetMsPerFrame = cfg.TargetMsPerFrame;
		UpdateEveryFrameBudgetMs = cfg.UpdateEveryFrameBudgetMs;
		UpdateEveryFrameMaxRowsPerStep = cfg.UpdateEveryFrameMaxRowsPerStep;
		RowsPerFrame = cfg.RowsPerFrame;
		MaxRowsPerFrameCap = cfg.MaxRowsPerFrameCap;
		RenderStepMaxMs = cfg.RenderStepMaxMs;
		RenderStepMaxPixelsPerFrame = cfg.RenderStepMaxPixelsPerFrame;
		RenderStepMaxSegmentsPerFrame = cfg.RenderStepMaxSegmentsPerFrame;
		BroadphaseControlMode = cfg.BroadphaseControlMode;
		BroadphasePolicy = cfg.BroadphasePolicy;
	}

	private SmartScaleProbeConfig[] BuildSmartScaleProbePlan()
	{
		int fullRows = Math.Max(
			1,
			_filmHeight > 0 ? _filmHeight : Math.Max(1, (int)Math.Round(Height * Math.Max(0.01f, FilmResolutionScale))));
		int raisedPixelCap = Math.Max(4_000_000, Math.Max(0, RenderStepMaxPixelsPerFrame));
		int raisedSegCap = Math.Max(100_000_000, Math.Max(0, RenderStepMaxSegmentsPerFrame));

		return new[]
		{
			new SmartScaleProbeConfig
			{
				ProbeId = "step0_baseline",
				Summary = "baseline",
				BroadphaseControlMode = BroadphaseMode.Policy,
				BroadphasePolicy = BroadphasePolicyMode.OverlapOnly
			},
			new SmartScaleProbeConfig
			{
				ProbeId = "step1_pixel_unlock",
				Summary = "pixel_throughput_unlock",
				PixelStride = 1,
				UsePass2CollisionStride = false,
				UpdateEveryFrameMaxRowsPerStep = fullRows,
				RowsPerFrame = fullRows,
				MaxRowsPerFrameCap = fullRows,
				RenderStepMaxPixelsPerFrame = raisedPixelCap,
				RenderStepMaxSegmentsPerFrame = raisedSegCap,
				BroadphaseControlMode = BroadphaseMode.Policy,
				BroadphasePolicy = BroadphasePolicyMode.OverlapOnly
			},
			new SmartScaleProbeConfig
			{
				ProbeId = "step2_time_expand_20ms",
				Summary = "time_expansion_20ms",
				PixelStride = 1,
				UsePass2CollisionStride = false,
				TargetMsPerFrame = 20,
				UpdateEveryFrameBudgetMs = 1000f,
				RenderStepMaxMs = 1000,
				UpdateEveryFrameMaxRowsPerStep = fullRows,
				RowsPerFrame = fullRows,
				MaxRowsPerFrameCap = fullRows,
				RenderStepMaxPixelsPerFrame = raisedPixelCap,
				RenderStepMaxSegmentsPerFrame = raisedSegCap,
				BroadphaseControlMode = BroadphaseMode.Policy,
				BroadphasePolicy = BroadphasePolicyMode.OverlapOnly
			},
			new SmartScaleProbeConfig
			{
				ProbeId = "stepX_broadphase_relax",
				Summary = "broadphase_relax",
				PixelStride = 1,
				UsePass2CollisionStride = false,
				TargetMsPerFrame = 20,
				UpdateEveryFrameBudgetMs = 1000f,
				RenderStepMaxMs = 1000,
				UpdateEveryFrameMaxRowsPerStep = fullRows,
				RowsPerFrame = fullRows,
				MaxRowsPerFrameCap = fullRows,
				RenderStepMaxPixelsPerFrame = raisedPixelCap,
				RenderStepMaxSegmentsPerFrame = raisedSegCap,
				BroadphaseControlMode = BroadphaseMode.Policy,
				BroadphasePolicy = BroadphasePolicyMode.Both
			},
			new SmartScaleProbeConfig
			{
				ProbeId = "step3_aggressive_30ms",
				Summary = "aggressive_throughput_30ms",
				PixelStride = 1,
				UsePass2CollisionStride = false,
				TargetMsPerFrame = 30,
				UpdateEveryFrameBudgetMs = 1000f,
				RenderStepMaxMs = 1000,
				UpdateEveryFrameMaxRowsPerStep = fullRows,
				RowsPerFrame = fullRows,
				MaxRowsPerFrameCap = fullRows,
				RenderStepMaxPixelsPerFrame = raisedPixelCap,
				RenderStepMaxSegmentsPerFrame = raisedSegCap,
				BroadphaseControlMode = BroadphaseMode.Policy,
				BroadphasePolicy = BroadphasePolicyMode.OverlapOnly
			}
		};
	}

	private void ApplySmartScaleProbeOverrides(in SmartScaleProbeConfig probe)
	{
		if (probe.PixelStride.HasValue) PixelStride = Mathf.Clamp(probe.PixelStride.Value, 1, 8);
		if (probe.UsePass2CollisionStride.HasValue) UsePass2CollisionStride = probe.UsePass2CollisionStride.Value;
		if (probe.TargetMsPerFrame.HasValue) TargetMsPerFrame = Math.Max(1, probe.TargetMsPerFrame.Value);
		if (probe.UpdateEveryFrameBudgetMs.HasValue) UpdateEveryFrameBudgetMs = Math.Max(1f, probe.UpdateEveryFrameBudgetMs.Value);
		if (probe.UpdateEveryFrameMaxRowsPerStep.HasValue) UpdateEveryFrameMaxRowsPerStep = Math.Max(1, probe.UpdateEveryFrameMaxRowsPerStep.Value);
		if (probe.RowsPerFrame.HasValue) RowsPerFrame = Math.Max(1, probe.RowsPerFrame.Value);
		if (probe.MaxRowsPerFrameCap.HasValue) MaxRowsPerFrameCap = Math.Max(1, probe.MaxRowsPerFrameCap.Value);
		if (probe.RenderStepMaxMs.HasValue) RenderStepMaxMs = Math.Max(1, probe.RenderStepMaxMs.Value);
		if (probe.RenderStepMaxPixelsPerFrame.HasValue) RenderStepMaxPixelsPerFrame = Math.Max(0, probe.RenderStepMaxPixelsPerFrame.Value);
		if (probe.RenderStepMaxSegmentsPerFrame.HasValue) RenderStepMaxSegmentsPerFrame = Math.Max(0, probe.RenderStepMaxSegmentsPerFrame.Value);
		if (probe.BroadphaseControlMode.HasValue) BroadphaseControlMode = probe.BroadphaseControlMode.Value;
		if (probe.BroadphasePolicy.HasValue) BroadphasePolicy = probe.BroadphasePolicy.Value;
	}

	private static float ResolveEffectiveFieldBeta(float globalBeta, bool overrideEnabled, float betaScale)
	{
		if (!overrideEnabled)
		{
			return globalBeta;
		}
		if (Math.Abs(globalBeta) <= 1e-6f)
		{
			return betaScale;
		}
		return globalBeta * betaScale;
	}

	private void EmitFixtureCurvatureDisabledWarning()
	{
		if (_smartScaleWarnedFixtureCurvature)
		{
			return;
		}
		_smartScaleWarnedFixtureCurvature = true;

		var tree = GetTree();
		if (tree == null)
		{
			return;
		}
		var nodes = tree.GetNodesInGroup("field_sources");
		if (nodes == null || nodes.Count == 0)
		{
			return;
		}

		float globalBeta = 0f;
		if (UseCameraPropsBetaGamma && _cam != null && IsInstanceValid(_cam))
		{
			globalBeta = ReadFloat(_cam, "Beta", 0f);
		}

		bool anyContributingSource = false;
		int sourceCount = 0;
		for (int i = 0; i < nodes.Count; i++)
		{
			if (nodes[i] is not FieldSource3D fs) continue;
			sourceCount++;
			if (!fs.Enabled) continue;
			FieldSource3D.ResolvedFieldParams resolved = fs.ResolveEffectiveParams(out _);
			float effBeta = Math.Abs(globalBeta) > 1e-6f ? (globalBeta * resolved.amp) : resolved.amp;
			float effAmp = effBeta;
			if (Math.Abs(effAmp) > 1e-6f)
			{
				anyContributingSource = true;
				break;
			}
		}

		if (sourceCount > 0 && !anyContributingSource)
		{
			GD.PushWarning("[FixtureWarn] Field curvature disabled (effective beta==0 across all FieldSource3D).");
		}
	}

	public TestRunDefaults CaptureTestRunDefaults()
	{
		ResolveRayBeamRendererReference();
		return new TestRunDefaults
		{
				UpdateEveryFrame = UpdateEveryFrame,
				UseGeometryTLASPruning = UseGeometryTLASPruning,
				UseThreadedBands = UseThreadedBands,
				ThreadedBandWorkerCount = ThreadedBandWorkerCount,
				ThreadedBandRowsPerChunk = ThreadedBandRowsPerChunk,
				UseThreadedPass2CandidateEval = UseThreadedPass2CandidateEval,
				ThreadedPass2CandidateWorkers = ThreadedPass2CandidateWorkers,
				ThreadedPass2CandidateRowsPerChunk = ThreadedPass2CandidateRowsPerChunk,
				UseThreadedPass2QueryResolve = UseThreadedPass2QueryResolve,
				ThreadedPass2QueryWorkers = ThreadedPass2QueryWorkers,
				ThreadedPass2QueryRowsPerChunk = ThreadedPass2QueryRowsPerChunk,
				UseThreadedPass2LocalAccumulation = UseThreadedPass2LocalAccumulation,
				ThreadedPass2WorkerCount = ThreadedPass2WorkerCount,
				ThreadedPass2RowsPerChunk = ThreadedPass2RowsPerChunk,
			Pass2GeomEnvelopeRadiusScale = Pass2GeomEnvelopeRadiusScale,
			Pass2GeomEnvelopeAabbExpand = Pass2GeomEnvelopeAabbExpand,
			AdaptiveTelemetryEnvelopeScalingEnabled = AdaptiveTelemetryEnvelopeScalingEnabled,
			AdaptiveEnvelopeControllerMode = AdaptiveEnvelopeControllerMode,
			AdaptiveEnvelopePriorSource = AdaptiveEnvelopePriorSource,
			AdaptiveEnvelopeThresholdStatistic = AdaptiveEnvelopeThresholdStatistic,
			AdaptiveEnvelopeHotThresholdPercentile = AdaptiveEnvelopeHotThresholdPercentile,
			AdaptiveEnvelopeWarmThresholdPercentile = AdaptiveEnvelopeWarmThresholdPercentile,
			AdaptiveEnvelopeRelaxedThresholdPercentile = AdaptiveEnvelopeRelaxedThresholdPercentile,
			AdaptiveEnvelopeTightScale = AdaptiveEnvelopeTightScale,
			AdaptiveEnvelopeWarmScale = AdaptiveEnvelopeWarmScale,
			AdaptiveEnvelopeNeutralScale = AdaptiveEnvelopeNeutralScale,
			AdaptiveEnvelopeRelaxedScale = AdaptiveEnvelopeRelaxedScale,
				UsePass2CollisionStride = UsePass2CollisionStride,
			Pass2CollisionStrideNear = Pass2CollisionStrideNear,
			Pass2CollisionStrideFar = Pass2CollisionStrideFar,
			Pass2CollisionStrideFarStartT = Pass2CollisionStrideFarStartT,
			MinSegLenForStrideSkip = MinSegLenForStrideSkip,
			Pass2SoftGateEnableQuickRayMiss = Pass2SoftGateEnableQuickRayMiss,
			Pass2SoftGateScoringEnabled = Pass2SoftGateScoringEnabled,
			TargetMsPerFrame = TargetMsPerFrame,
			SharedRbrStepsPerRay = _rbr != null ? _rbr.StepsPerRay : 0,
			SharedRbrMinStepLength = _rbr != null ? _rbr.MinStepLength : 0f,
			SharedRbrMaxStepLength = _rbr != null ? _rbr.MaxStepLength : 0f,
			SharedRbrStepAdaptGain = _rbr != null ? _rbr.StepAdaptGain : 0f
		};
	}

	private void CaptureRuntimeMacroBaselineDefaults()
	{
		_runtimeMacroBaselineDefaults = CaptureTestRunDefaults();
		_runtimeMacroBaselineCaptured = true;
	}

	private bool RuntimeMacroSwitchingAllowed()
	{
		if (!RuntimeMacroModeSwitchingEnabled)
			return false;
		if (_deterministicBenchmarkModeRequested)
			return false;

		foreach (string arg in GetHudCmdArgs())
		{
			string trimmed = NormalizeHudValue(arg);
			if (string.Equals(trimmed, "--render-test", StringComparison.OrdinalIgnoreCase) ||
				trimmed.StartsWith("--render-test-", StringComparison.OrdinalIgnoreCase) ||
				trimmed.StartsWith("--render-test-fixture=", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		return true;
	}

	private bool ShouldShowRuntimeMacroHudStatus()
	{
		return RuntimeMacroShowHudStatus && (RuntimeMacroSwitchingAllowed() || _runtimeMacroModeActive);
	}

	private static RuntimeMacroMode GetNextRuntimeMacroMode(RuntimeMacroMode mode)
	{
		return mode switch
		{
			RuntimeMacroMode.Accurate => RuntimeMacroMode.Tight08,
			RuntimeMacroMode.Tight08 => RuntimeMacroMode.CheapMotion,
			RuntimeMacroMode.CheapMotion => RuntimeMacroMode.SettleRefine,
			_ => RuntimeMacroMode.Accurate
		};
	}

	private static string GetRuntimeMacroModeLabel(RuntimeMacroMode mode)
	{
		return mode switch
		{
			RuntimeMacroMode.Accurate => "Accurate",
			RuntimeMacroMode.Tight08 => "Tight08",
			RuntimeMacroMode.CheapMotion => "CheapMotion",
			RuntimeMacroMode.SettleRefine => "SettleRefine",
			_ => mode.ToString()
		};
	}

	private TestRunConfig BuildRuntimeMacroModeConfig(RuntimeMacroMode mode)
	{
		return mode switch
		{
			RuntimeMacroMode.Tight08 => new TestRunConfig
			{
				Name = "runtime_macro_tight08",
				UseGeometryTLASPruning = true,
				Pass2GeomEnvelopeRadiusScale = 0.80f,
				Pass2GeomEnvelopeAabbExpand = 0.00f,
				AdaptiveTelemetryEnvelopeScalingEnabled = false,
				UsePass2CollisionStride = false,
				Pass2SoftGateEnableQuickRayMiss = false,
				Pass2SoftGateScoringEnabled = false
			},
			RuntimeMacroMode.CheapMotion => new TestRunConfig
			{
				Name = "runtime_macro_cheap_motion",
				UseGeometryTLASPruning = true,
				Pass2GeomEnvelopeRadiusScale = 1.02f,
				Pass2GeomEnvelopeAabbExpand = 0.01f,
				AdaptiveTelemetryEnvelopeScalingEnabled = false,
				UsePass2CollisionStride = true,
				Pass2CollisionStrideNear = 1,
				Pass2CollisionStrideFar = 4,
				Pass2CollisionStrideFarStartT = 0.35f,
				MinSegLenForStrideSkip = 0.25f,
				Pass2SoftGateEnableQuickRayMiss = false,
				Pass2SoftGateScoringEnabled = false
			},
			RuntimeMacroMode.SettleRefine => new TestRunConfig
			{
				Name = "runtime_macro_settle_refine",
				UseGeometryTLASPruning = true,
				Pass2GeomEnvelopeRadiusScale = 1.00f,
				Pass2GeomEnvelopeAabbExpand = 0.00f,
				AdaptiveTelemetryEnvelopeScalingEnabled = true,
				AdaptiveEnvelopeControllerMode = "four_state_warm",
				AdaptiveEnvelopePriorSource = "previous_pass",
				AdaptiveEnvelopeHotThresholdPercentile = 95f,
				AdaptiveEnvelopeWarmThresholdPercentile = 80f,
				AdaptiveEnvelopeRelaxedThresholdPercentile = 50f,
				AdaptiveEnvelopeTightScale = 0.70f,
				AdaptiveEnvelopeWarmScale = 0.85f,
				AdaptiveEnvelopeNeutralScale = 1.00f,
				AdaptiveEnvelopeRelaxedScale = 1.05f,
				UsePass2CollisionStride = false,
				Pass2SoftGateEnableQuickRayMiss = true,
				Pass2SoftGateScoringEnabled = true
			},
			_ => new TestRunConfig
			{
				Name = "runtime_macro_accurate"
			}
		};
	}

	private string BuildRuntimeMacroAppliedSummary(RuntimeMacroMode mode)
	{
		ResolveRayBeamRendererReference();
		string motionToken = _runtimeMacroCameraMoving ? "moving" : "still";
		return
			$"mode={GetRuntimeMacroModeLabel(mode)} " +
			$"prune={(UseGeometryTLASPruning ? 1 : 0)} envScale={Pass2GeomEnvelopeRadiusScale:0.##} envAabb={Pass2GeomEnvelopeAabbExpand:0.##} " +
			$"adaptiveEnv={(AdaptiveTelemetryEnvelopeScalingEnabled ? 1 : 0)} adaptiveMode={AdaptiveEnvelopeControllerMode} adaptivePrior={AdaptiveEnvelopePriorSource} " +
			$"stride={(UsePass2CollisionStride ? 1 : 0)} strideNear={Pass2CollisionStrideNear} strideFar={Pass2CollisionStrideFar} strideT={Pass2CollisionStrideFarStartT:0.##} minSeg={MinSegLenForStrideSkip:0.###} " +
			$"softQuickMiss={(Pass2SoftGateEnableQuickRayMiss ? 1 : 0)} softScore={(Pass2SoftGateScoringEnabled ? 1 : 0)} " +
			$"stepsPerRay={(_rbr != null ? _rbr.StepsPerRay : 0)} minStep={(_rbr != null ? _rbr.MinStepLength : 0f):0.####} maxStep={(_rbr != null ? _rbr.MaxStepLength : 0f):0.###} " +
			$"camMotion={motionToken}";
	}

	private bool TryApplyRuntimeMacroMode(RuntimeMacroMode mode, string reason)
	{
		if (!RuntimeMacroSwitchingAllowed())
		{
			if (!_runtimeMacroAvailabilityWarned)
			{
				_runtimeMacroAvailabilityWarned = true;
				GD.Print("[RuntimeMacroMode] switching_locked=1 reason=benchmark_or_render_test");
			}
			return false;
		}

		if (!_runtimeMacroBaselineCaptured)
			CaptureRuntimeMacroBaselineDefaults();

		if (mode == RuntimeMacroMode.Accurate)
		{
			RestoreTestRunDefaults(in _runtimeMacroBaselineDefaults);
		}
		else
		{
			RestoreTestRunDefaults(in _runtimeMacroBaselineDefaults);
			TestRunConfig config = BuildRuntimeMacroModeConfig(mode);
			ApplyTestRunConfig(in config);
		}

		SanitizeAndClampSettings();
		UpdateBroadphaseEffectiveState();
		_runtimeMacroModeActive = true;
		RuntimeMacroSelectedMode = mode;
		_runtimeMacroLastAppliedSummary = BuildRuntimeMacroAppliedSummary(mode);
		ResetRenderHealthWindowForRunStart();
		ResetRowCursor($"runtime_macro_{GetRuntimeMacroModeLabel(mode).ToLowerInvariant()}");
		GD.Print($"[RuntimeMacroMode] reason={reason} {_runtimeMacroLastAppliedSummary}");
		return true;
	}

	private void UpdateRuntimeMacroMotionState()
	{
		Camera3D cam = _cam;
		if (cam == null || !GodotObject.IsInstanceValid(cam))
		{
			cam = GetViewport()?.GetCamera3D();
			if (cam != null && GodotObject.IsInstanceValid(cam))
				_cam = cam;
		}

		if (cam == null || !GodotObject.IsInstanceValid(cam))
			return;

		Vector3 currentPos = cam.GlobalPosition;
		Basis currentBasis = cam.GlobalTransform.Basis;
		if (!_runtimeMacroHasMotionSample)
		{
			_runtimeMacroHasMotionSample = true;
			_runtimeMacroLastCameraPosition = currentPos;
			_runtimeMacroLastCameraBasis = currentBasis;
			_runtimeMacroCameraMoving = false;
			_runtimeMacroLastMotionDistance = 0.0f;
			_runtimeMacroLastMotionAngleDeg = 0.0f;
			return;
		}

		_runtimeMacroLastMotionDistance = currentPos.DistanceTo(_runtimeMacroLastCameraPosition);
		float dot = Mathf.Clamp(currentBasis.Z.Normalized().Dot(_runtimeMacroLastCameraBasis.Z.Normalized()), -1.0f, 1.0f);
		_runtimeMacroLastMotionAngleDeg = Mathf.RadToDeg(Mathf.Acos(dot));
		_runtimeMacroCameraMoving = _runtimeMacroLastMotionDistance > RuntimeMacroMotionDistanceEps
			|| _runtimeMacroLastMotionAngleDeg > RuntimeMacroMotionAngleDegEps;
		_runtimeMacroLastCameraPosition = currentPos;
		_runtimeMacroLastCameraBasis = currentBasis;
	}

	public void ApplyTestRunConfig(in TestRunConfig run)
	{
		ResolveRayBeamRendererReference();
		UpdateEveryFrame = run.UpdateEveryFrame ?? true;
		if (run.UseGeometryTLASPruning.HasValue) UseGeometryTLASPruning = run.UseGeometryTLASPruning.Value;
		if (run.Pass2GeomEnvelopeRadiusScale.HasValue) Pass2GeomEnvelopeRadiusScale = Mathf.Max(0.1f, run.Pass2GeomEnvelopeRadiusScale.Value);
		if (run.Pass2GeomEnvelopeAabbExpand.HasValue) Pass2GeomEnvelopeAabbExpand = Mathf.Max(0.0f, run.Pass2GeomEnvelopeAabbExpand.Value);
		if (run.AdaptiveTelemetryEnvelopeScalingEnabled.HasValue) AdaptiveTelemetryEnvelopeScalingEnabled = run.AdaptiveTelemetryEnvelopeScalingEnabled.Value;
		if (!string.IsNullOrWhiteSpace(run.AdaptiveEnvelopeControllerMode)) AdaptiveEnvelopeControllerMode = run.AdaptiveEnvelopeControllerMode;
		if (!string.IsNullOrWhiteSpace(run.AdaptiveEnvelopePriorSource)) AdaptiveEnvelopePriorSource = run.AdaptiveEnvelopePriorSource;
		if (run.AdaptiveEnvelopeHotThresholdPercentile.HasValue) AdaptiveEnvelopeHotThresholdPercentile = Mathf.Clamp(run.AdaptiveEnvelopeHotThresholdPercentile.Value, 0f, 100f);
		if (run.AdaptiveEnvelopeWarmThresholdPercentile.HasValue) AdaptiveEnvelopeWarmThresholdPercentile = Mathf.Clamp(run.AdaptiveEnvelopeWarmThresholdPercentile.Value, 0f, 100f);
		if (run.AdaptiveEnvelopeRelaxedThresholdPercentile.HasValue) AdaptiveEnvelopeRelaxedThresholdPercentile = Mathf.Clamp(run.AdaptiveEnvelopeRelaxedThresholdPercentile.Value, 0f, 100f);
		if (run.AdaptiveEnvelopeTightScale.HasValue) AdaptiveEnvelopeTightScale = Mathf.Clamp(run.AdaptiveEnvelopeTightScale.Value, 0.1f, 2.0f);
		if (run.AdaptiveEnvelopeWarmScale.HasValue) AdaptiveEnvelopeWarmScale = Mathf.Clamp(run.AdaptiveEnvelopeWarmScale.Value, 0.1f, 2.0f);
		if (run.AdaptiveEnvelopeNeutralScale.HasValue) AdaptiveEnvelopeNeutralScale = Mathf.Clamp(run.AdaptiveEnvelopeNeutralScale.Value, 0.1f, 2.0f);
		if (run.AdaptiveEnvelopeRelaxedScale.HasValue) AdaptiveEnvelopeRelaxedScale = Mathf.Clamp(run.AdaptiveEnvelopeRelaxedScale.Value, 0.1f, 2.0f);
		if (run.UsePass2CollisionStride.HasValue) UsePass2CollisionStride = run.UsePass2CollisionStride.Value;
		if (run.Pass2CollisionStrideNear.HasValue) Pass2CollisionStrideNear = Math.Max(1, run.Pass2CollisionStrideNear.Value);
		if (run.Pass2CollisionStrideFar.HasValue) Pass2CollisionStrideFar = Math.Max(1, run.Pass2CollisionStrideFar.Value);
		if (run.Pass2CollisionStrideFarStartT.HasValue) Pass2CollisionStrideFarStartT = Mathf.Clamp(run.Pass2CollisionStrideFarStartT.Value, 0.0f, 1.0f);
		if (run.MinSegLenForStrideSkip.HasValue) MinSegLenForStrideSkip = Mathf.Max(0.0f, run.MinSegLenForStrideSkip.Value);
		if (run.Pass2SoftGateEnableQuickRayMiss.HasValue) Pass2SoftGateEnableQuickRayMiss = run.Pass2SoftGateEnableQuickRayMiss.Value;
		if (run.Pass2SoftGateScoringEnabled.HasValue) Pass2SoftGateScoringEnabled = run.Pass2SoftGateScoringEnabled.Value;
		if (run.TargetMsPerFrame.HasValue) TargetMsPerFrame = Math.Max(1, run.TargetMsPerFrame.Value);
		if (_rbr != null)
		{
			if (run.SharedRbrStepsPerRay.HasValue) _rbr.StepsPerRay = Math.Max(1, run.SharedRbrStepsPerRay.Value);
			if (run.SharedRbrMinStepLength.HasValue) _rbr.MinStepLength = Mathf.Max(0.0001f, run.SharedRbrMinStepLength.Value);
			if (run.SharedRbrMaxStepLength.HasValue) _rbr.MaxStepLength = Mathf.Max(0.0001f, run.SharedRbrMaxStepLength.Value);
			if (run.SharedRbrStepAdaptGain.HasValue) _rbr.StepAdaptGain = Mathf.Max(0.0f, run.SharedRbrStepAdaptGain.Value);
		}
	}

	public void RestoreTestRunDefaults(in TestRunDefaults defaults)
	{
		ResolveRayBeamRendererReference();
			UpdateEveryFrame = defaults.UpdateEveryFrame;
			UseGeometryTLASPruning = defaults.UseGeometryTLASPruning;
			UseThreadedBands = defaults.UseThreadedBands;
			ThreadedBandWorkerCount = defaults.ThreadedBandWorkerCount;
			ThreadedBandRowsPerChunk = defaults.ThreadedBandRowsPerChunk;
			UseThreadedPass2CandidateEval = defaults.UseThreadedPass2CandidateEval;
			ThreadedPass2CandidateWorkers = defaults.ThreadedPass2CandidateWorkers;
			ThreadedPass2CandidateRowsPerChunk = defaults.ThreadedPass2CandidateRowsPerChunk;
			UseThreadedPass2QueryResolve = defaults.UseThreadedPass2QueryResolve;
			ThreadedPass2QueryWorkers = defaults.ThreadedPass2QueryWorkers;
			ThreadedPass2QueryRowsPerChunk = defaults.ThreadedPass2QueryRowsPerChunk;
			UseThreadedPass2LocalAccumulation = defaults.UseThreadedPass2LocalAccumulation;
			ThreadedPass2WorkerCount = defaults.ThreadedPass2WorkerCount;
			ThreadedPass2RowsPerChunk = defaults.ThreadedPass2RowsPerChunk;
			Pass2GeomEnvelopeRadiusScale = defaults.Pass2GeomEnvelopeRadiusScale;
		Pass2GeomEnvelopeAabbExpand = defaults.Pass2GeomEnvelopeAabbExpand;
		AdaptiveTelemetryEnvelopeScalingEnabled = defaults.AdaptiveTelemetryEnvelopeScalingEnabled;
		AdaptiveEnvelopeControllerMode = defaults.AdaptiveEnvelopeControllerMode;
		AdaptiveEnvelopePriorSource = defaults.AdaptiveEnvelopePriorSource;
		AdaptiveEnvelopeThresholdStatistic = defaults.AdaptiveEnvelopeThresholdStatistic;
		AdaptiveEnvelopeHotThresholdPercentile = defaults.AdaptiveEnvelopeHotThresholdPercentile;
		AdaptiveEnvelopeWarmThresholdPercentile = defaults.AdaptiveEnvelopeWarmThresholdPercentile;
		AdaptiveEnvelopeRelaxedThresholdPercentile = defaults.AdaptiveEnvelopeRelaxedThresholdPercentile;
		AdaptiveEnvelopeTightScale = defaults.AdaptiveEnvelopeTightScale;
		AdaptiveEnvelopeWarmScale = defaults.AdaptiveEnvelopeWarmScale;
		AdaptiveEnvelopeNeutralScale = defaults.AdaptiveEnvelopeNeutralScale;
		AdaptiveEnvelopeRelaxedScale = defaults.AdaptiveEnvelopeRelaxedScale;
		UsePass2CollisionStride = defaults.UsePass2CollisionStride;
		Pass2CollisionStrideNear = defaults.Pass2CollisionStrideNear;
		Pass2CollisionStrideFar = defaults.Pass2CollisionStrideFar;
		Pass2CollisionStrideFarStartT = defaults.Pass2CollisionStrideFarStartT;
		MinSegLenForStrideSkip = defaults.MinSegLenForStrideSkip;
		Pass2SoftGateEnableQuickRayMiss = defaults.Pass2SoftGateEnableQuickRayMiss;
		Pass2SoftGateScoringEnabled = defaults.Pass2SoftGateScoringEnabled;
		TargetMsPerFrame = defaults.TargetMsPerFrame;
		if (_rbr != null)
		{
			_rbr.StepsPerRay = Math.Max(1, defaults.SharedRbrStepsPerRay);
			_rbr.MinStepLength = Mathf.Max(0.0001f, defaults.SharedRbrMinStepLength);
			_rbr.MaxStepLength = Mathf.Max(0.0001f, defaults.SharedRbrMaxStepLength);
			_rbr.StepAdaptGain = Mathf.Max(0.0f, defaults.SharedRbrStepAdaptGain);
		}
	}

	public void GetLatestFrameMetricsForTesting(out double frameMs, out bool hasSegsPerPixel, out double segsPerPixel)
	{
		frameMs = _lastFrameRenderMs;
		hasSegsPerPixel = _framePerf.RaysTraced > 0;
		segsPerPixel = hasSegsPerPixel
			? (double)_framePerf.SegmentsTested / Math.Max(1L, _framePerf.RaysTraced)
			: 0.0;
	}

	public bool TryGetLatestRenderHealthForTesting(out bool trusted, out bool savedPctAvailable, out double savedPct, out string trustReason)
	{
		trusted = _testLastGeomTrusted;
		savedPctAvailable = _testLastGeomSavedPctAvailable;
		savedPct = _testLastGeomSavedPct;
		trustReason = _testLastGeomTrustReason;
		return _testHasRenderHealthSnapshot;
	}

	public bool TryGetLatestRenderHealthStepForTesting(out int stepIndex)
	{
		stepIndex = _renderHealthStepIndex;
		return _testHasRenderHealthSnapshot;
	}

	public bool RuntimeMacroModeActiveDebug => _runtimeMacroModeActive;
	public bool RuntimeMacroCameraMovingDebug => _runtimeMacroCameraMoving;
	public float RuntimeMacroCameraMotionDistanceDebug => _runtimeMacroLastMotionDistance;
	public float RuntimeMacroCameraMotionAngleDegDebug => _runtimeMacroLastMotionAngleDeg;

	public bool TryGetLatestRenderHealthDiagnosticsForTesting(out RenderHealthDiagnosticsSnapshot snapshot)
	{
		snapshot = default;
		if (_renderHealthCount <= 0)
		{
			return false;
		}

		RenderHealthSample latest = GetRenderHealthSampleFromEnd(0);
		snapshot = new RenderHealthDiagnosticsSnapshot(
			latest.StepIndex,
			latest.RowCursorAfter,
			latest.RowsAdvanced,
			latest.BandsProcessed,
			latest.TracedPixels,
			latest.GeomSegmentsQueried,
			latest.GeomRayTestsTotal,
			latest.Pass2SampledSegments,
			latest.AvgStepsPerTracedPixel,
			string.IsNullOrWhiteSpace(latest.BudgetExitReason) ? "none" : latest.BudgetExitReason,
			latest.GeomPruneRequested,
			latest.UseGeometryTLASPruning);
		return true;
	}

	public void ResetFixtureDebugStatsForRunStart()
	{
		_fixtureDebugSourceHitsThisRun = 0;
		_fixtureDebugBackgroundHitsThisRun = 0;
		_fixtureDebugUnclassifiedHitsThisRun = 0;
		_fixtureDebugAbsorbedHitsThisRun = 0;
		_fixtureDebugMissHitsThisRun = 0;
		ResetTelemetryHeatmapsForRunStart();
		ResetFixtureRowParticipationForRunStart();
		ResetFixtureWriteDiagnosticsForRunStart();
	}

	public bool TryGetFixtureDebugStatsForTesting(out FixtureDebugStatsSnapshot snapshot)
	{
		long tracedPixels = _fixtureDebugSourceHitsThisRun +
			_fixtureDebugBackgroundHitsThisRun +
			_fixtureDebugUnclassifiedHitsThisRun +
			_fixtureDebugAbsorbedHitsThisRun +
			_fixtureDebugMissHitsThisRun;
		snapshot = new FixtureDebugStatsSnapshot(
			_fixtureDebugSourceHitsThisRun,
			_fixtureDebugBackgroundHitsThisRun,
			_fixtureDebugUnclassifiedHitsThisRun,
			_fixtureDebugAbsorbedHitsThisRun,
			_fixtureDebugMissHitsThisRun,
			tracedPixels);
		return tracedPixels > 0;
	}

	public bool TryGetFixtureRowParticipationForTesting(out FixtureRowParticipationSnapshot snapshot)
	{
		int totalRowsConsidered = CountMarkedRows(_fixtureRowsConsidered);
		int totalRowsProcessed = CountMarkedRows(_fixtureRowsProcessed);
		int totalRowsSkipped = CountMarkedRows(_fixtureRowsSkipped);
		int zeroHitRows = CountMarkedRows(_fixtureRowsZeroHit);
		int processedRowStart = FindFirstMarkedRow(_fixtureRowsProcessed);
		int processedRowEnd = FindLastMarkedRow(_fixtureRowsProcessed);
		string processedRanges = BuildMarkedRowRanges(_fixtureRowsProcessed);
		string skippedRanges = BuildMarkedRowRanges(_fixtureRowsSkipped);
		string zeroHitRanges = BuildMarkedRowRanges(_fixtureRowsZeroHit);
		string summary =
			$"proc={FormatCompactRowRanges(processedRanges)}|" +
			$"skip={FormatCompactRowRanges(skippedRanges)}|" +
			$"zero={FormatCompactRowRanges(zeroHitRanges)}";
		snapshot = new FixtureRowParticipationSnapshot(
			totalRowsConsidered,
			totalRowsProcessed,
			totalRowsSkipped,
			processedRowStart,
			processedRowEnd,
			zeroHitRows,
			processedRanges,
			skippedRanges,
			zeroHitRanges,
			summary);
		return totalRowsConsidered > 0 || totalRowsProcessed > 0 || totalRowsSkipped > 0 || zeroHitRows > 0;
	}

	public bool TryGetFixtureWriteDiagnosticsForTesting(out FixtureWriteDiagnosticsSnapshot snapshot)
	{
		snapshot = new FixtureWriteDiagnosticsSnapshot(
			CountMarkedRows(_fixtureRowsStarted),
			CountMarkedRows(_fixtureRowsCompleted),
			CountMarkedRows(_fixtureRowsPartiallyWritten),
			CountMarkedRows(_fixtureRowsEarlyTerminated),
			_fixtureFinalHitPixelCountThisRun,
			_fixtureTraversalWritePixelCountThisRun);
		return snapshot.RowsStarted > 0 ||
			snapshot.RowsCompleted > 0 ||
			snapshot.RowsPartiallyWritten > 0 ||
			snapshot.RowsEarlyTerminated > 0 ||
			snapshot.FinalHitPixelCount > 0 ||
			snapshot.TraversalWritePixelCount > 0;
	}

	public void ResetTelemetryHeatmapsForRunStart()
	{
		ClearTelemetryHeatmapArrays();
		Array.Clear(_adaptiveEnvelopeMismatchPrior, 0, _adaptiveEnvelopeMismatchPrior.Length);
		Array.Clear(_adaptiveEnvelopeActiveMask, 0, _adaptiveEnvelopeActiveMask.Length);
		Array.Clear(_adaptiveEnvelopePreviousMismatchPrior, 0, _adaptiveEnvelopePreviousMismatchPrior.Length);
		Array.Clear(_adaptiveEnvelopePreviousActiveMask, 0, _adaptiveEnvelopePreviousActiveMask.Length);
		_adaptiveEnvelopeScaleMinThisRun = 1.0f;
		_adaptiveEnvelopeScaleMaxThisRun = 1.0f;
		_adaptiveEnvelopeScaleSumThisRun = 0.0;
		_adaptiveEnvelopeScaleCountThisRun = 0;
		_adaptiveEnvelopeTightCountThisRun = 0;
		_adaptiveEnvelopeWarmCountThisRun = 0;
		_adaptiveEnvelopeNeutralCountThisRun = 0;
		_adaptiveEnvelopeRelaxedCountThisRun = 0;
		Array.Clear(_adaptiveEnvelopeScaleHistogram, 0, _adaptiveEnvelopeScaleHistogram.Length);
		_adaptiveEnvelopeGlobalQueryMinusCurvatureP50 = 0.0f;
		_adaptiveEnvelopeGlobalQueryMinusCurvatureP90 = 0.0f;
		_adaptiveEnvelopeGlobalRelaxedThreshold = 0.0f;
		_adaptiveEnvelopeGlobalWarmThreshold = 0.0f;
		_adaptiveEnvelopeGlobalHotThreshold = 0.0f;
		_adaptiveEnvelopePreviousGlobalQueryMinusCurvatureP50 = 0.0f;
		_adaptiveEnvelopePreviousGlobalQueryMinusCurvatureP90 = 0.0f;
		_adaptiveEnvelopePreviousGlobalRelaxedThreshold = 0.0f;
		_adaptiveEnvelopePreviousGlobalWarmThreshold = 0.0f;
		_adaptiveEnvelopePreviousGlobalHotThreshold = 0.0f;
		_adaptiveEnvelopePreviousSnapshotAvailable = false;
		_adaptiveEnvelopePriorFallbackCountThisRun = 0;
		_adaptiveEnvelopePriorSnapshotUnavailableFallbackCountThisRun = 0;
		_adaptiveEnvelopePriorInsufficientDataFallbackCountThisRun = 0;
		_adaptiveEnvelopePriorFallbackBehaviorThisRun = "none";
		_adaptiveEnvelopePriorSnapshotUnavailableLoggedThisRun = false;
		_adaptiveEnvelopePriorInsufficientDataLoggedThisRun = false;
	}

	public bool TryCopyTelemetryHeatmapImageForTesting(TelemetryHeatmapKind kind, out Image image, out TelemetryHeatmapStats stats)
	{
		image = null;
		stats = default;
		if (_filmWidth <= 0 || _filmHeight <= 0)
		{
			return false;
		}

		if (!TryGetTelemetrySource(kind, out float[] source, out string key))
		{
			return false;
		}

		float[] values = ResolveTelemetryValues(kind, source);
		if (values == null || values.Length != _filmWidth * _filmHeight)
		{
			return false;
		}

		TelemetryHeatmapStats computedStats = ComputeTelemetryHeatmapStats(values, key, ResolveTelemetryHeatmapModeToken());
		Image heatmap = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
		bool signedMap = IsSignedTelemetryHeatmapKind(kind);
		float clampMax = Math.Max(0f, computedStats.ClampMax);
		float denom = clampMax > 0f
			? clampMax
			: (signedMap ? Math.Max(Math.Abs(computedStats.Min), Math.Abs(computedStats.Max)) : Math.Max(0f, computedStats.Max));
		for (int y = 0; y < _filmHeight; y++)
		{
			int rowBase = y * _filmWidth;
			for (int x = 0; x < _filmWidth; x++)
			{
				float raw = values[rowBase + x];
				float normalized = signedMap
					? (denom > 0f ? Mathf.Clamp(0.5f + (0.5f * (raw / denom)), 0f, 1f) : 0.5f)
					: (denom > 0f ? Mathf.Clamp(raw / denom, 0f, 1f) : 0f);
				heatmap.SetPixel(x, y, EvaluateTelemetryHeatColor(normalized));
			}
		}

		image = heatmap;
		stats = computedStats;
		return true;
	}

	public bool TryGetTelemetryHeatmapStatsForTesting(TelemetryHeatmapKind kind, out TelemetryHeatmapStats stats)
	{
		stats = default;
		if (_filmWidth <= 0 || _filmHeight <= 0)
		{
			return false;
		}

		if (!TryGetTelemetrySource(kind, out float[] source, out string key))
		{
			return false;
		}

		float[] values = ResolveTelemetryValues(kind, source);
		if (values == null || values.Length != _filmWidth * _filmHeight)
		{
			return false;
		}

		stats = ComputeTelemetryHeatmapStats(values, key, ResolveTelemetryHeatmapModeToken());
		return true;
	}

	public bool TryGetTelemetryCorrelationStatsForTesting(out float workVsCurvatureMean, out float queryVsCurvatureMean)
	{
		workVsCurvatureMean = 0f;
		queryVsCurvatureMean = 0f;
		if (!TelemetryHeatmapsEnabledForCurrentRun())
		{
			return false;
		}

		float[] work = BuildDerivedWorkScoreArray();
		if (work.Length != _telemetryCurvatureMean.Length || _telemetryQueryCount.Length != _telemetryCurvatureMean.Length)
		{
			return false;
		}

		workVsCurvatureMean = ComputePearsonCorrelation(work, _telemetryCurvatureMean);
		queryVsCurvatureMean = ComputePearsonCorrelation(_telemetryQueryCount, _telemetryCurvatureMean);
		return true;
	}

	public bool TryGetAdaptiveEnvelopeScaleStatsForTesting(out AdaptiveEnvelopeScaleStats stats)
	{
		stats = default;
		if (_adaptiveEnvelopeScaleCountThisRun <= 0)
		{
			return false;
		}

		stats = new AdaptiveEnvelopeScaleStats(
			_adaptiveEnvelopeScaleMinThisRun,
			(float)(_adaptiveEnvelopeScaleSumThisRun / Math.Max(1L, _adaptiveEnvelopeScaleCountThisRun)),
			_adaptiveEnvelopeScaleMaxThisRun);
		return true;
	}

	public bool TryGetAdaptiveEnvelopeDebugStatsForTesting(out AdaptiveEnvelopeDebugStats stats)
	{
		stats = default;
		if (_adaptiveEnvelopeScaleCountThisRun <= 0)
		{
			return false;
		}

		stats = new AdaptiveEnvelopeDebugStats(
			_adaptiveEnvelopeScaleMinThisRun,
			(float)(_adaptiveEnvelopeScaleSumThisRun / Math.Max(1L, _adaptiveEnvelopeScaleCountThisRun)),
			_adaptiveEnvelopeScaleMaxThisRun,
			(int)Math.Min(int.MaxValue, _adaptiveEnvelopeScaleCountThisRun),
			_adaptiveEnvelopeTightCountThisRun,
			_adaptiveEnvelopeWarmCountThisRun,
			_adaptiveEnvelopeNeutralCountThisRun,
			_adaptiveEnvelopeRelaxedCountThisRun,
			_adaptiveEnvelopeScaleHistogram.Length > 0 ? _adaptiveEnvelopeScaleHistogram[0] : 0,
			_adaptiveEnvelopeScaleHistogram.Length > 1 ? _adaptiveEnvelopeScaleHistogram[1] : 0,
			_adaptiveEnvelopeScaleHistogram.Length > 2 ? _adaptiveEnvelopeScaleHistogram[2] : 0,
			_adaptiveEnvelopeScaleHistogram.Length > 3 ? _adaptiveEnvelopeScaleHistogram[3] : 0,
			_adaptiveEnvelopeScaleHistogram.Length > 4 ? _adaptiveEnvelopeScaleHistogram[4] : 0,
			_adaptiveEnvelopeGlobalQueryMinusCurvatureP50,
			_adaptiveEnvelopeGlobalQueryMinusCurvatureP90,
			ResolveAdaptiveEnvelopePriorSourceToken(),
			_adaptiveEnvelopePriorFallbackBehaviorThisRun,
			_adaptiveEnvelopePreviousSnapshotAvailable,
			_adaptiveEnvelopePriorFallbackCountThisRun,
			_adaptiveEnvelopePriorSnapshotUnavailableFallbackCountThisRun,
			_adaptiveEnvelopePriorInsufficientDataFallbackCountThisRun);
		return true;
	}

	public bool TryCopyFilmImageForTesting(out Image image)
	{
		image = null;
		if (_img == null || _filmWidth <= 0 || _filmHeight <= 0)
		{
			return false;
		}

		Image copy = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
		for (int y = 0; y < _filmHeight; y++)
		{
			for (int x = 0; x < _filmWidth; x++)
			{
				copy.SetPixel(x, y, _img.GetPixel(x, y));
			}
		}

		image = copy;
		return true;
	}

	public bool TryCopyFinalHitOnlyFilmImageForTesting(out Image image)
	{
		image = null;
		if (_fixtureFinalHitOnlyImg == null || _filmWidth <= 0 || _filmHeight <= 0)
		{
			return false;
		}

		Image copy = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
		for (int y = 0; y < _filmHeight; y++)
		{
			for (int x = 0; x < _filmWidth; x++)
			{
				copy.SetPixel(x, y, _fixtureFinalHitOnlyImg.GetPixel(x, y));
			}
		}

		image = copy;
		return true;
	}

	public bool TryCopyCategoricalFinalFilmImageForTesting(out Image image)
	{
		image = null;
		if (_fixtureCategoricalFinalImg == null || _filmWidth <= 0 || _filmHeight <= 0)
		{
			return false;
		}

		Image copy = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
		for (int y = 0; y < _filmHeight; y++)
		{
			for (int x = 0; x < _filmWidth; x++)
			{
				Color pixel = _fixtureCategoricalFinalImg.GetPixel(x, y);
				// Reserve black for truly unrendered rows; normalize any in-range gaps to the rendered no-hit state.
				if (y < Math.Min(_rowCursor, _filmHeight) && IsCategoricalVoidPixel(pixel))
				{
					pixel = FixtureCategoricalRenderedNoHitColor;
				}
				copy.SetPixel(x, y, pixel);
			}
		}

		image = copy;
		return true;
	}

	public bool TryGetFilmCaptureDiagnosticsForTesting(out FilmCaptureDiagnosticsSnapshot snapshot)
	{
		snapshot = new FilmCaptureDiagnosticsSnapshot(
			_filmWidth,
			_filmHeight,
			_rowCursor,
			_dbgRayCount,
			_dbgPtWrite,
			DebugMaxFilmRays,
			_framePerf.RaysTraced,
			_framePerf.SegmentsIntegrated,
			_framePerf.SegmentsTested,
			_framePerf.PhysicsQueries,
			SkyColor);
		return _filmWidth > 0 && _filmHeight > 0;
	}

	private void EnsureTelemetryHeatmapCapacity(int pixelCount)
	{
		int safeCount = Math.Max(0, pixelCount);
		if (_telemetryPass1AcceptedSteps.Length != safeCount)
			_telemetryPass1AcceptedSteps = new float[safeCount];
		if (_telemetryCandidateCount.Length != safeCount)
			_telemetryCandidateCount = new float[safeCount];
		if (_telemetryQueryCount.Length != safeCount)
			_telemetryQueryCount = new float[safeCount];
		if (_telemetryResolveCount.Length != safeCount)
			_telemetryResolveCount = new float[safeCount];
		if (_telemetryCurvatureMax.Length != safeCount)
			_telemetryCurvatureMax = new float[safeCount];
		if (_telemetryCurvatureMean.Length != safeCount)
			_telemetryCurvatureMean = new float[safeCount];
		if (_telemetryDkMax.Length != safeCount)
			_telemetryDkMax = new float[safeCount];
		if (_telemetryD2kMax.Length != safeCount)
			_telemetryD2kMax = new float[safeCount];
		if (_adaptiveEnvelopeMismatchPrior.Length != safeCount)
			_adaptiveEnvelopeMismatchPrior = new float[safeCount];
		if (_adaptiveEnvelopeActiveMask.Length != safeCount)
			_adaptiveEnvelopeActiveMask = new byte[safeCount];
		if (_adaptiveEnvelopePreviousMismatchPrior.Length != safeCount)
			_adaptiveEnvelopePreviousMismatchPrior = new float[safeCount];
		if (_adaptiveEnvelopePreviousActiveMask.Length != safeCount)
			_adaptiveEnvelopePreviousActiveMask = new byte[safeCount];
	}

	private void ClearTelemetryHeatmapArrays()
	{
		Array.Clear(_telemetryPass1AcceptedSteps, 0, _telemetryPass1AcceptedSteps.Length);
		Array.Clear(_telemetryCandidateCount, 0, _telemetryCandidateCount.Length);
		Array.Clear(_telemetryQueryCount, 0, _telemetryQueryCount.Length);
		Array.Clear(_telemetryResolveCount, 0, _telemetryResolveCount.Length);
		Array.Clear(_telemetryCurvatureMax, 0, _telemetryCurvatureMax.Length);
		Array.Clear(_telemetryCurvatureMean, 0, _telemetryCurvatureMean.Length);
		Array.Clear(_telemetryDkMax, 0, _telemetryDkMax.Length);
		Array.Clear(_telemetryD2kMax, 0, _telemetryD2kMax.Length);
		Array.Clear(_adaptiveEnvelopeMismatchPrior, 0, _adaptiveEnvelopeMismatchPrior.Length);
	}

	private bool AdaptiveTelemetryEnvelopeScalingEnabledForCurrentRun()
	{
		return AdaptiveTelemetryEnvelopeScalingEnabled
			&& TelemetryHeatmapsEnabledForCurrentRun()
			&& _adaptiveEnvelopeMismatchPrior.Length == _filmWidth * _filmHeight;
	}

	private string ResolveAdaptiveEnvelopeThresholdStatisticToken()
	{
		string token = string.IsNullOrWhiteSpace(AdaptiveEnvelopeThresholdStatistic)
			? "mean"
			: AdaptiveEnvelopeThresholdStatistic.Trim().ToLowerInvariant();
		return token switch
		{
			"mean" => "mean",
			"p90" => "p90",
			"max" => "max",
			_ => "mean"
		};
	}

	private string ResolveAdaptiveEnvelopeControllerModeToken()
	{
		string token = string.IsNullOrWhiteSpace(AdaptiveEnvelopeControllerMode)
			? "three_state"
			: AdaptiveEnvelopeControllerMode.Trim().ToLowerInvariant();
		return token switch
		{
			"four_state_warm" => "four_state_warm",
			"four_state" => "four_state_warm",
			_ => "three_state"
		};
	}

	private string ResolveAdaptiveEnvelopePriorSourceToken()
	{
		string token = string.IsNullOrWhiteSpace(AdaptiveEnvelopePriorSource)
			? "same_pass"
			: AdaptiveEnvelopePriorSource.Trim().ToLowerInvariant();
		return token switch
		{
			"previous_pass" => "previous_pass",
			_ => "same_pass"
		};
	}

	private static float NormalizeAdaptiveEnvelopePercentileSetting(float percentile)
	{
		return Mathf.Clamp(percentile, 0f, 100f) / 100f;
	}

	private bool TryPopulateAdaptiveEnvelopeTelemetrySnapshot(
		float[] mismatchTarget,
		byte[] activeMaskTarget,
		out float globalP50,
		out float globalP90,
		out float globalRelaxedThreshold,
		out float globalWarmThreshold,
		out float globalHotThreshold)
	{
		globalP50 = 0.0f;
		globalP90 = 0.0f;
		globalRelaxedThreshold = 0.0f;
		globalWarmThreshold = 0.0f;
		globalHotThreshold = 0.0f;
		if (!AdaptiveTelemetryEnvelopeScalingEnabledForCurrentRun())
		{
			return false;
		}

		if (mismatchTarget == null || mismatchTarget.Length != _filmWidth * _filmHeight ||
			activeMaskTarget == null || activeMaskTarget.Length != _filmWidth * _filmHeight)
		{
			return false;
		}

		float[] queryMinusCurvature = BuildDerivedNormalizedDifferenceArray(_telemetryQueryCount, _telemetryCurvatureMean);
		if (queryMinusCurvature.Length != _filmWidth * _filmHeight)
		{
			return false;
		}

		float[] active = new float[queryMinusCurvature.Length];
		int activeCount = 0;
		for (int i = 0; i < queryMinusCurvature.Length; i++)
		{
			float mismatch = queryMinusCurvature[i];
			mismatchTarget[i] = mismatch;
			bool isActive = _telemetryPass1AcceptedSteps[i] > 0f || _telemetryCurvatureMean[i] > 0f || _telemetryQueryCount[i] > 0f;
			activeMaskTarget[i] = isActive ? (byte)1 : (byte)0;
			if (!isActive)
			{
				continue;
			}

			active[activeCount++] = mismatch;
		}

		if (activeCount <= 0)
		{
			return false;
		}

		Array.Sort(active, 0, activeCount);
		globalP50 = SampleSortedPercentile(active, activeCount, 0.50f);
		globalP90 = SampleSortedPercentile(active, activeCount, 0.90f);
		globalRelaxedThreshold = SampleSortedPercentile(active, activeCount, NormalizeAdaptiveEnvelopePercentileSetting(AdaptiveEnvelopeRelaxedThresholdPercentile));
		globalWarmThreshold = SampleSortedPercentile(active, activeCount, NormalizeAdaptiveEnvelopePercentileSetting(AdaptiveEnvelopeWarmThresholdPercentile));
		globalHotThreshold = SampleSortedPercentile(active, activeCount, NormalizeAdaptiveEnvelopePercentileSetting(AdaptiveEnvelopeHotThresholdPercentile));
		return true;
	}

	private void RefreshAdaptiveEnvelopeTelemetryPriors()
	{
		if (!TryPopulateAdaptiveEnvelopeTelemetrySnapshot(
			_adaptiveEnvelopeMismatchPrior,
			_adaptiveEnvelopeActiveMask,
			out _adaptiveEnvelopeGlobalQueryMinusCurvatureP50,
			out _adaptiveEnvelopeGlobalQueryMinusCurvatureP90,
			out _adaptiveEnvelopeGlobalRelaxedThreshold,
			out _adaptiveEnvelopeGlobalWarmThreshold,
			out _adaptiveEnvelopeGlobalHotThreshold))
		{
			_adaptiveEnvelopeGlobalQueryMinusCurvatureP50 = 0.0f;
			_adaptiveEnvelopeGlobalQueryMinusCurvatureP90 = 0.0f;
			_adaptiveEnvelopeGlobalRelaxedThreshold = 0.0f;
			_adaptiveEnvelopeGlobalWarmThreshold = 0.0f;
			_adaptiveEnvelopeGlobalHotThreshold = 0.0f;
		}
	}

	private void CaptureAdaptiveEnvelopePreviousTelemetrySnapshot()
	{
		if (TryPopulateAdaptiveEnvelopeTelemetrySnapshot(
			_adaptiveEnvelopePreviousMismatchPrior,
			_adaptiveEnvelopePreviousActiveMask,
			out _adaptiveEnvelopePreviousGlobalQueryMinusCurvatureP50,
			out _adaptiveEnvelopePreviousGlobalQueryMinusCurvatureP90,
			out _adaptiveEnvelopePreviousGlobalRelaxedThreshold,
			out _adaptiveEnvelopePreviousGlobalWarmThreshold,
			out _adaptiveEnvelopePreviousGlobalHotThreshold))
		{
			_adaptiveEnvelopePreviousSnapshotAvailable = true;
			return;
		}

		_adaptiveEnvelopePreviousSnapshotAvailable = false;
		_adaptiveEnvelopePreviousGlobalQueryMinusCurvatureP50 = 0.0f;
		_adaptiveEnvelopePreviousGlobalQueryMinusCurvatureP90 = 0.0f;
		_adaptiveEnvelopePreviousGlobalRelaxedThreshold = 0.0f;
		_adaptiveEnvelopePreviousGlobalWarmThreshold = 0.0f;
		_adaptiveEnvelopePreviousGlobalHotThreshold = 0.0f;
	}

	private void RecordAdaptiveEnvelopePriorFallback(string reason)
	{
		_adaptiveEnvelopePriorFallbackBehaviorThisRun = "neutral";
		_adaptiveEnvelopePriorFallbackCountThisRun++;
		if (string.Equals(reason, "snapshot_unavailable", StringComparison.Ordinal))
		{
			_adaptiveEnvelopePriorSnapshotUnavailableFallbackCountThisRun++;
			if (!_adaptiveEnvelopePriorSnapshotUnavailableLoggedThisRun)
			{
				_adaptiveEnvelopePriorSnapshotUnavailableLoggedThisRun = true;
				GD.Print("[AdaptiveEnvelope] prior_source=previous_pass fallback=neutral reason=snapshot_unavailable");
			}
			return;
		}

		if (string.Equals(reason, "insufficient_prior_samples", StringComparison.Ordinal))
		{
			_adaptiveEnvelopePriorInsufficientDataFallbackCountThisRun++;
			if (!_adaptiveEnvelopePriorInsufficientDataLoggedThisRun)
			{
				_adaptiveEnvelopePriorInsufficientDataLoggedThisRun = true;
				GD.Print("[AdaptiveEnvelope] prior_source=previous_pass fallback=neutral reason=insufficient_prior_samples");
			}
		}
	}

	private enum AdaptiveEnvelopeRegime
	{
		Hot = 0,
		Warm = 1,
		Neutral = 2,
		Relaxed = 3
	}

	private float ComputeAdaptiveEnvelopeScaleForRect(int xStart, int xEndExclusive, int yStart, int yEndExclusive, float baseScale)
	{
		float baseClamped = Mathf.Clamp(baseScale, 0.65f, 1.1f);
		if (!AdaptiveTelemetryEnvelopeScalingEnabledForCurrentRun())
		{
			return baseClamped;
		}

		int x0 = Math.Max(0, xStart);
		int y0 = Math.Max(0, yStart);
		int x1 = Math.Min(_filmWidth, xEndExclusive);
		int y1 = Math.Min(_filmHeight, yEndExclusive);
		if (x0 >= x1 || y0 >= y1)
		{
			return baseClamped;
		}

		string priorSourceToken = ResolveAdaptiveEnvelopePriorSourceToken();
		float[] mismatchPrior = _adaptiveEnvelopeMismatchPrior;
		byte[] activeMask = _adaptiveEnvelopeActiveMask;
		float globalP50 = _adaptiveEnvelopeGlobalQueryMinusCurvatureP50;
		float globalP90 = _adaptiveEnvelopeGlobalQueryMinusCurvatureP90;
		float globalRelaxedThreshold = _adaptiveEnvelopeGlobalRelaxedThreshold;
		float globalWarmThreshold = _adaptiveEnvelopeGlobalWarmThreshold;
		float globalHotThreshold = _adaptiveEnvelopeGlobalHotThreshold;
		if (priorSourceToken == "previous_pass")
		{
			if (!_adaptiveEnvelopePreviousSnapshotAvailable)
			{
				RecordAdaptiveEnvelopePriorFallback("snapshot_unavailable");
				return 1.0f;
			}

			mismatchPrior = _adaptiveEnvelopePreviousMismatchPrior;
			activeMask = _adaptiveEnvelopePreviousActiveMask;
			globalP50 = _adaptiveEnvelopePreviousGlobalQueryMinusCurvatureP50;
			globalP90 = _adaptiveEnvelopePreviousGlobalQueryMinusCurvatureP90;
			globalRelaxedThreshold = _adaptiveEnvelopePreviousGlobalRelaxedThreshold;
			globalWarmThreshold = _adaptiveEnvelopePreviousGlobalWarmThreshold;
			globalHotThreshold = _adaptiveEnvelopePreviousGlobalHotThreshold;
		}

		double mismatchSum = 0.0;
		float mismatchMax = float.NegativeInfinity;
		float[] localMismatch = new float[Math.Max(1, (x1 - x0) * (y1 - y0))];
		int activeCount = 0;
		for (int y = y0; y < y1; y++)
		{
			int rowBase = y * _filmWidth;
			for (int x = x0; x < x1; x++)
			{
				int pi = rowBase + x;
				if (activeMask[pi] == 0)
				{
					continue;
				}

				float mismatchValue = mismatchPrior[pi];
				mismatchSum += mismatchValue;
				if (mismatchValue > mismatchMax)
				{
					mismatchMax = mismatchValue;
				}
				localMismatch[activeCount] = mismatchValue;
				activeCount++;
			}
		}

		if (activeCount < 4)
		{
			if (priorSourceToken == "previous_pass")
			{
				RecordAdaptiveEnvelopePriorFallback("insufficient_prior_samples");
				return 1.0f;
			}

			return baseClamped;
		}

		string statisticToken = ResolveAdaptiveEnvelopeThresholdStatisticToken();
		string controllerModeToken = ResolveAdaptiveEnvelopeControllerModeToken();
		float mismatch = statisticToken switch
		{
			"p90" => ComputeActivePercentile(localMismatch, activeCount, 0.90f),
			"max" => mismatchMax,
			_ => (float)(mismatchSum / activeCount)
		};
		AdaptiveEnvelopeRegime regime = AdaptiveEnvelopeRegime.Neutral;
		float localScale = Mathf.Clamp(AdaptiveEnvelopeNeutralScale, 0.1f, 2.0f);
		if (controllerModeToken == "four_state_warm")
		{
			float relaxedThreshold = globalRelaxedThreshold;
			float warmThreshold = Math.Max(globalRelaxedThreshold, globalWarmThreshold);
			float hotThreshold = Math.Max(warmThreshold, globalHotThreshold);
			if (mismatch > hotThreshold)
			{
				regime = AdaptiveEnvelopeRegime.Hot;
				localScale = Mathf.Clamp(AdaptiveEnvelopeTightScale, 0.1f, 2.0f);
			}
			else if (mismatch > warmThreshold)
			{
				regime = AdaptiveEnvelopeRegime.Warm;
				localScale = Mathf.Clamp(AdaptiveEnvelopeWarmScale, 0.1f, 2.0f);
			}
			else if (mismatch < relaxedThreshold)
			{
				regime = AdaptiveEnvelopeRegime.Relaxed;
				localScale = Mathf.Clamp(AdaptiveEnvelopeRelaxedScale, 0.1f, 2.0f);
			}
		}
		else if (mismatch > globalP90)
		{
			regime = AdaptiveEnvelopeRegime.Hot;
			localScale = Mathf.Clamp(AdaptiveEnvelopeTightScale, 0.1f, 2.0f);
		}
		else if (mismatch < globalP50)
		{
			regime = AdaptiveEnvelopeRegime.Relaxed;
			localScale = Mathf.Clamp(AdaptiveEnvelopeRelaxedScale, 0.1f, 2.0f);
		}

		float clampedScale = Mathf.Clamp(localScale, 0.65f, 1.1f);
		RecordAdaptiveEnvelopeScaleSample(clampedScale, regime);
		return clampedScale;
	}

	private float[] BuildAdaptiveEnvelopeScaleBySubtileForBand(int yStart, int bandHeight, float baseScale)
	{
		float[] scales = new float[Math.Max(0, _tileMetricCurrentSubtileCount)];
		if (scales.Length == 0)
		{
			return scales;
		}

		float defaultScale = Mathf.Clamp(baseScale, 0.65f, 1.1f);
		for (int i = 0; i < scales.Length; i++)
		{
			scales[i] = defaultScale;
		}

		if (!AdaptiveTelemetryEnvelopeScalingEnabledForCurrentRun())
		{
			return scales;
		}

		float passProgress = _filmHeight > 0 ? Mathf.Clamp((float)yStart / _filmHeight, 0f, 1f) : 0f;
		if (passProgress < 0.25f)
		{
			return scales;
		}

		RefreshAdaptiveEnvelopeTelemetryPriors();
		int yEnd = Math.Min(_filmHeight, yStart + Math.Max(1, bandHeight));
		for (int subtileIndex = 0; subtileIndex < scales.Length; subtileIndex++)
		{
			int subtileXStart = subtileIndex * _tileMetricCurrentSubtileWidth;
			int subtileXEnd = Math.Max(0, Math.Min(_filmWidth, subtileXStart + _tileMetricCurrentSubtileWidth));
			scales[subtileIndex] = ComputeAdaptiveEnvelopeScaleForRect(subtileXStart, subtileXEnd, yStart, yEnd, baseScale);
		}

		return scales;
	}

	private void RecordAdaptiveEnvelopeScaleSample(float scale, AdaptiveEnvelopeRegime regime)
	{
		float clamped = Mathf.Clamp(scale, 0.65f, 1.1f);
		if (_adaptiveEnvelopeScaleCountThisRun <= 0)
		{
			_adaptiveEnvelopeScaleMinThisRun = clamped;
			_adaptiveEnvelopeScaleMaxThisRun = clamped;
			_adaptiveEnvelopeScaleSumThisRun = clamped;
			_adaptiveEnvelopeScaleCountThisRun = 1;
		}
		else
		{
			_adaptiveEnvelopeScaleMinThisRun = Math.Min(_adaptiveEnvelopeScaleMinThisRun, clamped);
			_adaptiveEnvelopeScaleMaxThisRun = Math.Max(_adaptiveEnvelopeScaleMaxThisRun, clamped);
			_adaptiveEnvelopeScaleSumThisRun += clamped;
			_adaptiveEnvelopeScaleCountThisRun++;
		}

		switch (regime)
		{
			case AdaptiveEnvelopeRegime.Hot:
				_adaptiveEnvelopeTightCountThisRun++;
				break;
			case AdaptiveEnvelopeRegime.Warm:
				_adaptiveEnvelopeWarmCountThisRun++;
				break;
			case AdaptiveEnvelopeRegime.Relaxed:
				_adaptiveEnvelopeRelaxedCountThisRun++;
				break;
			default:
				_adaptiveEnvelopeNeutralCountThisRun++;
				break;
		}

		int histIndex;
		if (clamped < 0.75f) histIndex = 0;
		else if (clamped < 0.85f) histIndex = 1;
		else if (clamped < 0.95f) histIndex = 2;
		else if (clamped < 1.025f) histIndex = 3;
		else histIndex = 4;
		if ((uint)histIndex < (uint)_adaptiveEnvelopeScaleHistogram.Length)
		{
			_adaptiveEnvelopeScaleHistogram[histIndex]++;
		}
	}

	private static float SampleSortedPercentile(float[] sorted, int count, float percentile)
	{
		if (sorted == null || count <= 0)
		{
			return 0f;
		}

		float clampedPercentile = Mathf.Clamp(percentile, 0f, 1f);
		int index = Math.Clamp((int)MathF.Round((count - 1) * clampedPercentile), 0, count - 1);
		return sorted[index];
	}

	private static float ComputeActivePercentile(float[] values, int count, float percentile)
	{
		if (values == null || count <= 0)
		{
			return 0f;
		}

		float[] copy = new float[count];
		Array.Copy(values, copy, count);
		Array.Sort(copy, 0, count);
		return SampleSortedPercentile(copy, count, percentile);
	}

	private void AccumulateTelemetryBlock(float[] target, int x, int y, int stride, float value)
	{
		if (target == null || target.Length == 0 || value <= 0f || _filmWidth <= 0 || _filmHeight <= 0)
		{
			return;
		}

		int safeStride = Math.Max(1, stride);
		int yMax = Math.Min(_filmHeight, y + safeStride);
		int xMax = Math.Min(_filmWidth, x + safeStride);
		for (int yy = Math.Max(0, y); yy < yMax; yy++)
		{
			int rowBase = yy * _filmWidth;
			for (int xx = Math.Max(0, x); xx < xMax; xx++)
			{
				target[rowBase + xx] += value;
			}
		}
	}

	private bool TelemetryHeatmapsEnabledForCurrentRun()
	{
		return ExportTelemetryHeatmaps
			&& _filmWidth > 0
			&& _filmHeight > 0
			&& _telemetryPass1AcceptedSteps.Length == _filmWidth * _filmHeight
			&& _telemetryCandidateCount.Length == _filmWidth * _filmHeight
			&& _telemetryQueryCount.Length == _filmWidth * _filmHeight
			&& _telemetryResolveCount.Length == _filmWidth * _filmHeight
			&& _telemetryCurvatureMax.Length == _filmWidth * _filmHeight
			&& _telemetryCurvatureMean.Length == _filmWidth * _filmHeight
			&& _telemetryDkMax.Length == _filmWidth * _filmHeight
			&& _telemetryD2kMax.Length == _filmWidth * _filmHeight;
	}

	private bool TryGetTelemetrySource(TelemetryHeatmapKind kind, out float[] source, out string key)
	{
		source = null;
		key = string.Empty;
		switch (kind)
		{
			case TelemetryHeatmapKind.Work:
				key = "work";
				return TelemetryHeatmapsEnabledForCurrentRun();
			case TelemetryHeatmapKind.Candidates:
				source = _telemetryCandidateCount;
				key = "candidates";
				break;
			case TelemetryHeatmapKind.Query:
				source = _telemetryQueryCount;
				key = "query";
				break;
			case TelemetryHeatmapKind.Resolve:
				source = _telemetryResolveCount;
				key = "resolve";
				break;
			case TelemetryHeatmapKind.Pass1Steps:
				source = _telemetryPass1AcceptedSteps;
				key = "pass1_steps";
				break;
			case TelemetryHeatmapKind.CurvatureMax:
				source = _telemetryCurvatureMax;
				key = "curvature_max";
				break;
			case TelemetryHeatmapKind.CurvatureMean:
				source = _telemetryCurvatureMean;
				key = "curvature_mean";
				break;
			case TelemetryHeatmapKind.DkMax:
				source = _telemetryDkMax;
				key = "dk_max";
				break;
			case TelemetryHeatmapKind.D2kMax:
				source = _telemetryD2kMax;
				key = "d2k_max";
				break;
			case TelemetryHeatmapKind.WorkMinusCurvature:
				key = "work_minus_curvature";
				return TelemetryHeatmapsEnabledForCurrentRun();
			case TelemetryHeatmapKind.QueryMinusCurvature:
				key = "query_minus_curvature";
				return TelemetryHeatmapsEnabledForCurrentRun();
			case TelemetryHeatmapKind.Efficiency:
				key = "efficiency";
				return TelemetryHeatmapsEnabledForCurrentRun();
			default:
				return false;
		}

		return TelemetryHeatmapsEnabledForCurrentRun() && source != null;
	}

	private float[] BuildDerivedWorkScoreArray()
	{
		if (!TelemetryHeatmapsEnabledForCurrentRun())
		{
			return Array.Empty<float>();
		}

		int pixelCount = _filmWidth * _filmHeight;
		float[] work = new float[pixelCount];
		for (int i = 0; i < pixelCount; i++)
		{
			work[i] =
				_telemetryPass1AcceptedSteps[i] +
				_telemetryCandidateCount[i] +
				_telemetryQueryCount[i] +
				_telemetryResolveCount[i];
		}

		return work;
	}

	private float[] BuildDerivedNormalizedDifferenceArray(float[] lhs, float[] rhs)
	{
		if (!TelemetryHeatmapsEnabledForCurrentRun())
		{
			return Array.Empty<float>();
		}
		if (lhs == null || rhs == null || lhs.Length != rhs.Length || lhs.Length != (_filmWidth * _filmHeight))
		{
			return Array.Empty<float>();
		}

		TelemetryHeatmapStats lhsStats = ComputeTelemetryHeatmapStats(lhs, "lhs", ResolveTelemetryHeatmapModeToken());
		TelemetryHeatmapStats rhsStats = ComputeTelemetryHeatmapStats(rhs, "rhs", ResolveTelemetryHeatmapModeToken());
		float lhsDenom = lhsStats.ClampMax > 0f ? lhsStats.ClampMax : Math.Max(0f, lhsStats.Max);
		float rhsDenom = rhsStats.ClampMax > 0f ? rhsStats.ClampMax : Math.Max(0f, rhsStats.Max);
		float[] diff = new float[lhs.Length];
		for (int i = 0; i < lhs.Length; i++)
		{
			float lhsNorm = lhsDenom > 0f ? Mathf.Clamp(lhs[i] / lhsDenom, 0f, 1f) : 0f;
			float rhsNorm = rhsDenom > 0f ? Mathf.Clamp(rhs[i] / rhsDenom, 0f, 1f) : 0f;
			diff[i] = lhsNorm - rhsNorm;
		}

		return diff;
	}

	private float[] BuildDerivedEfficiencyArray(float epsilon)
	{
		if (!TelemetryHeatmapsEnabledForCurrentRun())
		{
			return Array.Empty<float>();
		}

		float safeEpsilon = Mathf.Max(1e-6f, epsilon);
		float[] work = BuildDerivedWorkScoreArray();
		if (work.Length != _telemetryCurvatureMean.Length)
		{
			return Array.Empty<float>();
		}

		float[] efficiency = new float[work.Length];
		for (int i = 0; i < work.Length; i++)
		{
			efficiency[i] = work[i] / (_telemetryCurvatureMean[i] + safeEpsilon);
		}

		return efficiency;
	}

	private bool IsSignedTelemetryHeatmapKind(TelemetryHeatmapKind kind)
	{
		return kind == TelemetryHeatmapKind.WorkMinusCurvature || kind == TelemetryHeatmapKind.QueryMinusCurvature;
	}

	private float[] ResolveTelemetryValues(TelemetryHeatmapKind kind, float[] source)
	{
		switch (kind)
		{
			case TelemetryHeatmapKind.Work:
				return BuildDerivedWorkScoreArray();
			case TelemetryHeatmapKind.WorkMinusCurvature:
				return BuildDerivedNormalizedDifferenceArray(BuildDerivedWorkScoreArray(), _telemetryCurvatureMean);
			case TelemetryHeatmapKind.QueryMinusCurvature:
				return BuildDerivedNormalizedDifferenceArray(_telemetryQueryCount, _telemetryCurvatureMean);
			case TelemetryHeatmapKind.Efficiency:
				return BuildDerivedEfficiencyArray(1e-3f);
			default:
				return source ?? Array.Empty<float>();
		}
	}

	private string ResolveTelemetryHeatmapModeToken()
	{
		string mode = string.IsNullOrWhiteSpace(TelemetryHeatmapMode)
			? "basic"
			: TelemetryHeatmapMode.Trim().ToLowerInvariant();
		return string.IsNullOrWhiteSpace(mode) ? "basic" : mode;
	}

	private static TelemetryHeatmapStats ComputeTelemetryHeatmapStats(float[] values, string key, string mode)
	{
		if (values == null || values.Length == 0)
		{
			return new TelemetryHeatmapStats(key, 0f, 0f, 0f, 0f, 0f, 0f, 0f, mode);
		}

		float min = float.PositiveInfinity;
		float max = float.NegativeInfinity;
		double sum = 0.0;
		float[] sorted = new float[values.Length];
		for (int i = 0; i < values.Length; i++)
		{
			float value = values[i];
			if (float.IsNaN(value) || float.IsInfinity(value))
			{
				value = 0f;
			}
			sorted[i] = value;
			if (value < min) min = value;
			if (value > max) max = value;
			sum += value;
		}

		Array.Sort(sorted);
		float p10 = SamplePercentile(sorted, 0.10f);
		float p50 = SamplePercentile(sorted, 0.50f);
		float p90 = SamplePercentile(sorted, 0.90f);
		float p99 = SamplePercentile(sorted, 0.99f);
		bool hasNegative = min < 0f;
		float clampMax;
		if (string.Equals(mode, "basic", StringComparison.OrdinalIgnoreCase))
		{
			if (hasNegative)
			{
				float p01 = SamplePercentile(sorted, 0.01f);
				clampMax = Math.Max(Math.Abs(p01), Math.Abs(p99));
				clampMax = Math.Max(clampMax, Math.Max(Math.Abs(p10), Math.Abs(p90)));
			}
			else
			{
				clampMax = Math.Max(p90, p99);
			}
		}
		else
		{
			clampMax = hasNegative ? Math.Max(Math.Abs(min), Math.Abs(max)) : max;
		}
		if (clampMax <= 0f)
		{
			clampMax = hasNegative ? Math.Max(Math.Abs(min), Math.Abs(max)) : max;
		}

		return new TelemetryHeatmapStats(
			key,
			float.IsFinite(min) ? min : 0f,
			float.IsFinite(max) ? max : 0f,
			(float)(sum / values.Length),
			p10,
			p50,
			p90,
			float.IsFinite(clampMax) ? clampMax : 0f,
			mode);
	}

	private static float SamplePercentile(float[] sorted, float percentile)
	{
		if (sorted == null || sorted.Length == 0)
		{
			return 0f;
		}

		float clamped = Mathf.Clamp(percentile, 0f, 1f);
		int index = Mathf.Clamp(Mathf.RoundToInt((sorted.Length - 1) * clamped), 0, sorted.Length - 1);
		float value = sorted[index];
		return float.IsFinite(value) ? value : 0f;
	}

	private static Color EvaluateTelemetryHeatColor(float t)
	{
		float clamped = Mathf.Clamp(t, 0f, 1f);
		if (clamped <= 0.33f)
		{
			float local = clamped / 0.33f;
			return new Color(0f, local, 1f, 1f);
		}
		if (clamped <= 0.66f)
		{
			float local = (clamped - 0.33f) / 0.33f;
			return new Color(local, 1f, 1f - local, 1f);
		}

		float tail = (clamped - 0.66f) / 0.34f;
		return new Color(1f, 1f - tail, 0f, 1f);
	}

	private static float ComputePearsonCorrelation(float[] lhs, float[] rhs)
	{
		if (lhs == null || rhs == null || lhs.Length == 0 || lhs.Length != rhs.Length)
		{
			return 0f;
		}

		double lhsSum = 0.0;
		double rhsSum = 0.0;
		int count = lhs.Length;
		for (int i = 0; i < count; i++)
		{
			double l = SanitizeTelemetryValue(lhs[i]);
			double r = SanitizeTelemetryValue(rhs[i]);
			lhsSum += l;
			rhsSum += r;
		}

		double lhsMean = lhsSum / count;
		double rhsMean = rhsSum / count;
		double covariance = 0.0;
		double lhsVar = 0.0;
		double rhsVar = 0.0;
		for (int i = 0; i < count; i++)
		{
			double l = SanitizeTelemetryValue(lhs[i]) - lhsMean;
			double r = SanitizeTelemetryValue(rhs[i]) - rhsMean;
			covariance += l * r;
			lhsVar += l * l;
			rhsVar += r * r;
		}

		double denom = Math.Sqrt(lhsVar * rhsVar);
		if (!double.IsFinite(denom) || denom <= 1e-12)
		{
			return 0f;
		}

		double corr = covariance / denom;
		if (!double.IsFinite(corr))
		{
			return 0f;
		}

		return (float)Mathf.Clamp((float)corr, -1f, 1f);
	}

	private static double SanitizeTelemetryValue(float value)
	{
		return (float.IsNaN(value) || float.IsInfinity(value)) ? 0.0 : value;
	}

	private void ResetFixtureRowParticipationForRunStart()
	{
		if (_fixtureRowsConsidered.Length > 0) Array.Clear(_fixtureRowsConsidered, 0, _fixtureRowsConsidered.Length);
		if (_fixtureRowsProcessed.Length > 0) Array.Clear(_fixtureRowsProcessed, 0, _fixtureRowsProcessed.Length);
		if (_fixtureRowsSkipped.Length > 0) Array.Clear(_fixtureRowsSkipped, 0, _fixtureRowsSkipped.Length);
		if (_fixtureRowsZeroHit.Length > 0) Array.Clear(_fixtureRowsZeroHit, 0, _fixtureRowsZeroHit.Length);
	}

	private void ResetFixtureWriteDiagnosticsForRunStart()
	{
		_fixtureFinalHitPixelCountThisRun = 0;
		_fixtureTraversalWritePixelCountThisRun = 0;
		if (_fixtureRowsStarted.Length > 0) Array.Clear(_fixtureRowsStarted, 0, _fixtureRowsStarted.Length);
		if (_fixtureRowsCompleted.Length > 0) Array.Clear(_fixtureRowsCompleted, 0, _fixtureRowsCompleted.Length);
		if (_fixtureRowsPartiallyWritten.Length > 0) Array.Clear(_fixtureRowsPartiallyWritten, 0, _fixtureRowsPartiallyWritten.Length);
		if (_fixtureRowsEarlyTerminated.Length > 0) Array.Clear(_fixtureRowsEarlyTerminated, 0, _fixtureRowsEarlyTerminated.Length);
		if (_fixtureFinalHitOnlyImg != null && _filmWidth > 0 && _filmHeight > 0)
		{
			_fixtureFinalHitOnlyImg.Fill(Colors.Black);
		}
		if (_fixtureCategoricalFinalImg != null && _filmWidth > 0 && _filmHeight > 0)
		{
			_fixtureCategoricalFinalImg.Fill(Colors.Black);
		}
	}

	private void EnsureFixtureRowParticipationCapacity(int filmHeight)
	{
		int resolvedHeight = Math.Max(0, filmHeight);
		if (_fixtureRowsConsidered.Length != resolvedHeight)
		{
			_fixtureRowsConsidered = new byte[resolvedHeight];
			_fixtureRowsProcessed = new byte[resolvedHeight];
			_fixtureRowsSkipped = new byte[resolvedHeight];
			_fixtureRowsZeroHit = new byte[resolvedHeight];
			_fixtureRowsStarted = new byte[resolvedHeight];
			_fixtureRowsCompleted = new byte[resolvedHeight];
			_fixtureRowsPartiallyWritten = new byte[resolvedHeight];
			_fixtureRowsEarlyTerminated = new byte[resolvedHeight];
		}
	}

	private void RecordFixtureRowParticipationBand(
		int bandStart,
		int bandEnd,
		bool markProcessed,
		bool markSkipped,
		bool markZeroHit)
	{
		int filmHeight = Math.Max(0, _filmHeight);
		EnsureFixtureRowParticipationCapacity(filmHeight);
		if (filmHeight <= 0)
		{
			return;
		}

		int start = Mathf.Clamp(bandStart, 0, filmHeight);
		int end = Mathf.Clamp(bandEnd, 0, filmHeight);
		if (end <= start)
		{
			return;
		}

		MarkFixtureRowRange(_fixtureRowsConsidered, start, end);
		if (markProcessed)
		{
			MarkFixtureRowRange(_fixtureRowsProcessed, start, end);
		}
		if (markSkipped)
		{
			MarkFixtureRowRange(_fixtureRowsSkipped, start, end);
		}
		if (markZeroHit)
		{
			MarkFixtureRowRange(_fixtureRowsZeroHit, start, end);
		}
	}

	private static void MarkFixtureRowRange(byte[] rows, int start, int end)
	{
		if (rows.Length == 0)
		{
			return;
		}

		int clampedStart = Math.Max(0, Math.Min(start, rows.Length));
		int clampedEnd = Math.Max(clampedStart, Math.Min(end, rows.Length));
		for (int index = clampedStart; index < clampedEnd; index++)
		{
			rows[index] = 1;
		}
	}

	private void RecordFixtureRowWriteStart(int row)
	{
		int filmHeight = Math.Max(0, _filmHeight);
		EnsureFixtureRowParticipationCapacity(filmHeight);
		if (row < 0 || row >= _fixtureRowsStarted.Length)
		{
			return;
		}

		_fixtureRowsStarted[row] = 1;
	}

	private void RecordFixtureRowWriteOutcome(int row, bool rowHadWrites, bool rowCompleted)
	{
		if (row < 0 || row >= _fixtureRowsStarted.Length)
		{
			return;
		}

		if (rowCompleted)
		{
			_fixtureRowsCompleted[row] = 1;
			return;
		}

		_fixtureRowsEarlyTerminated[row] = 1;
		if (rowHadWrites)
		{
			_fixtureRowsPartiallyWritten[row] = 1;
		}
	}

	private static int CountMarkedRows(byte[] rows)
	{
		int count = 0;
		for (int index = 0; index < rows.Length; index++)
		{
			if (rows[index] != 0)
			{
				count++;
			}
		}
		return count;
	}

	private static bool IsCategoricalVoidPixel(Color color)
	{
		return color.R <= 0.001f &&
			color.G <= 0.001f &&
			color.B <= 0.001f &&
			color.A >= 0.999f;
	}

	private static int FindFirstMarkedRow(byte[] rows)
	{
		for (int index = 0; index < rows.Length; index++)
		{
			if (rows[index] != 0)
			{
				return index;
			}
		}
		return -1;
	}

	private static int FindLastMarkedRow(byte[] rows)
	{
		for (int index = rows.Length - 1; index >= 0; index--)
		{
			if (rows[index] != 0)
			{
				return index;
			}
		}
		return -1;
	}

	private static string BuildMarkedRowRanges(byte[] rows)
	{
		if (rows.Length == 0)
		{
			return "";
		}

		System.Text.StringBuilder builder = new System.Text.StringBuilder();
		int rangeStart = -1;
		for (int index = 0; index <= rows.Length; index++)
		{
			bool marked = index < rows.Length && rows[index] != 0;
			if (marked)
			{
				if (rangeStart < 0)
				{
					rangeStart = index;
				}
				continue;
			}

			if (rangeStart < 0)
			{
				continue;
			}

			int rangeEnd = index - 1;
			if (builder.Length > 0)
			{
				builder.Append(",");
			}
			builder.Append(rangeStart.ToString(CultureInfo.InvariantCulture));
			if (rangeEnd > rangeStart)
			{
				builder.Append("-").Append(rangeEnd.ToString(CultureInfo.InvariantCulture));
			}
			rangeStart = -1;
		}

		return builder.ToString();
	}

	private static string FormatCompactRowRanges(string ranges)
	{
		return string.IsNullOrWhiteSpace(ranges) ? "-" : ranges;
	}

	public void SetFilmOpacityForTesting(float opacity)
	{
		FilmOpacity = Mathf.Clamp(opacity, 0.0f, 1.0f);
		UpdateFilmOpacity();
	}

	public void ResetRenderHealthOverlayRollingForRunStart()
	{
		ResetRenderHealthOverlayWindowState();
	}

	public void ConfigureRenderHealthTrustEnforcementForTesting(
		bool enabled,
		int minGeomPixProcessedPerWindow,
		long minGeomRayTestsTotalPerWindow,
		int pass2SampleEveryNSegmentsOverride = 0,
		int minPass2SamplesForTrustOverride = 0)
	{
		_renderHealthTestTrustEnforcementEnabled = enabled;
		_renderHealthTestMinGeomPixProcessedPerWindow = enabled ? Math.Max(0, minGeomPixProcessedPerWindow) : 0;
		_renderHealthTestMinGeomRayTestsTotalPerWindow = enabled ? Math.Max(0L, minGeomRayTestsTotalPerWindow) : 0L;
		_renderHealthTestPass2SampleEveryNSegmentsOverride = enabled ? Math.Max(0, pass2SampleEveryNSegmentsOverride) : 0;
		_renderHealthTestMinPass2SamplesForTrustOverride = enabled ? Math.Max(0, minPass2SamplesForTrustOverride) : 0;
	}

	public void ResetRenderHealthWindowForRunStart()
	{
		ResetRenderHealthOverlayWindowState();
		_renderHealthWrite = 0;
		_renderHealthCount = 0;
		_renderHealthStallSteps = 0;
		_renderHealthLastRowCursor = -1;
		_renderHealthLastExitReason = "";
		_renderHealthPass2SampleCounter = 0;
		_geomPruneSwitchedThisWindow = 0;
		_hasRenderHealthGeomPruneMode = false;
		_lastRenderHealthGeomPruneMode = false;
		_geomSegmentsQueriedThisFrame = 0;
		_geomSegWithCandidatesThisFrame = 0;
		_geomSegZeroCandidatesThisFrame = 0;
		_geomPixelProcessedThisFrame = 0;
		_geomPixelHadAnyCandidatesThisFrame = 0;
		_geomPixelNoCandidatesThisFrame = 0;
		_geomHitAcceptedThisFrame = 0;
		_geomHitRejectedThisFrame = 0;
		_geomRayTestsTotalThisFrame = 0;
		_geomRayTestsAcceptedThisFrame = 0;
		_geomRayTestsRejectedThisFrame = 0;
		_geomHitAcceptedLastSample = 0;
		_geomHitRejectedLastSample = 0;
		_geomSegmentsQueriedLastSample = 0;
		_geomSegWithCandidatesLastSample = 0;
		_geomSegZeroCandidatesLastSample = 0;
		_geomPixelProcessedLastSample = 0;
		_geomPixelHadAnyCandidatesLastSample = 0;
		_geomPixelNoCandidatesLastSample = 0;
		_geomRayTestsTotalLastSample = 0;
		_geomRayTestsAcceptedLastSample = 0;
		_geomRayTestsRejectedLastSample = 0;
		_geomPruneAuditSamplesThisFrame = 0;
		_geomPruneAuditFalseNegThisFrame = 0;
		_geomPruneAuditFalsePosThisFrame = 0;
		_geomPruneAuditCandidateZeroButBaselineHitThisFrame = 0;
		_geomPruneAuditSamplesLastSample = 0;
		_geomPruneAuditFalseNegLastSample = 0;
		_geomPruneAuditFalsePosLastSample = 0;
		_geomPruneAuditCandidateZeroButBaselineHitLastSample = 0;
		_geomPruneAuditMismatchLogsThisWindow = 0;
		_geomPruneAuditSamplesTakenThisWindow = 0;
		_geomRejectSampleCidNotInGeometryList = 0;
		_geomRejectSampleCidInGeometryListNotInCandidates = 0;
		_geomRejectSampleCandidateContainsCid = 0;
		_testHasRenderHealthSnapshot = false;
		_testLastGeomTrusted = false;
		_testLastGeomSavedPctAvailable = false;
		_testLastGeomSavedPct = 0.0;
		_testLastGeomTrustReason = "na";
		_testLastTrustGeomPixMet = false;
		_testLastTrustRayTestsMet = false;
		_testLastTrustP2Met = false;
	}

	private void ResetRenderHealthOverlayWindowState()
	{
		_overlayRolling.Reset();
		_lastRenderHealthEmissionTimestamp = 0;
		_hasLastRenderHealthEmissionTimestamp = false;
		_rowsPerSecEma = 0.0;
		_msPerRowEma = 0.0;
		_hasRenderThroughputEma = false;
		_renderThroughputWindowTrusted = false;
	}

	private void UpdateRenderThroughputMetricsFromRenderHealth(long rowsAdvancedInWindow, bool windowTrusted)
	{
		long now = Stopwatch.GetTimestamp();
		if (!_hasLastRenderHealthEmissionTimestamp)
		{
			_lastRenderHealthEmissionTimestamp = now;
			_hasLastRenderHealthEmissionTimestamp = true;
			_renderThroughputWindowTrusted = false;
			return;
		}

		long deltaTicks = now - _lastRenderHealthEmissionTimestamp;
		_lastRenderHealthEmissionTimestamp = now;
		_renderThroughputWindowTrusted = false;
		if (deltaTicks <= 0)
			return;

		double elapsedSec = deltaTicks / (double)Stopwatch.Frequency;
		if (!double.IsFinite(elapsedSec) || elapsedSec <= 0.0 || !windowTrusted || rowsAdvancedInWindow <= 0)
			return;

		double rowsPerSec = rowsAdvancedInWindow / elapsedSec;
		double msPerRow = (elapsedSec * 1000.0) / rowsAdvancedInWindow;
		if (!double.IsFinite(rowsPerSec) || !double.IsFinite(msPerRow) || rowsPerSec <= 0.0 || msPerRow <= 0.0)
			return;

		if (!_hasRenderThroughputEma)
		{
			_rowsPerSecEma = rowsPerSec;
			_msPerRowEma = msPerRow;
			_hasRenderThroughputEma = true;
		}
		else
		{
			_rowsPerSecEma += (rowsPerSec - _rowsPerSecEma) * OverlayRenderThroughputEmaAlpha;
			_msPerRowEma += (msPerRow - _msPerRowEma) * OverlayRenderThroughputEmaAlpha;
		}

		_renderThroughputWindowTrusted = true;
	}

	private void EmitRenderMetricsOverlay()
	{
		if (_filmOverlay == null || !GodotObject.IsInstanceValid(_filmOverlay))
			return;

		_overlayRolling.SetWindowSeconds(RenderHealthRollingWindowSec);
		_presentRolling.SetWindowSeconds(RenderHealthRollingWindowSec);
		long now = Stopwatch.GetTimestamp();
		OverlayRollingSnapshot rolling = _overlayRolling.Snapshot(now);
		OverlayRollingSnapshot presentRolling = _presentRolling.Snapshot(now);
		double elapsedSec = rolling.ElapsedSec;
		if ((!double.IsFinite(elapsedSec) || elapsedSec <= 0.0) && rolling.StepMsTotal > 0.0)
			elapsedSec = rolling.StepMsTotal / 1000.0;
		bool hasElapsed = elapsedSec > 0.0 && double.IsFinite(elapsedSec);
		bool hasSteps = rolling.Steps > 0;
		bool hasRows = rolling.RowsAdvanced > 0 && hasElapsed;
		double rollingRowsPerSec = hasRows ? (rolling.RowsAdvanced / elapsedSec) : 0.0;
		double rollingMsPerRow = hasRows ? (rolling.StepMsTotal / rolling.RowsAdvanced) : 0.0;
		double rollingMsPerStep = hasSteps ? (rolling.StepMsTotal / rolling.Steps) : 0.0;
		double rollingStepsPerSec = hasSteps && hasElapsed ? (rolling.Steps / elapsedSec) : 0.0;
		double rollingHitsPerSec = hasSteps && hasElapsed ? (rolling.Hits / elapsedSec) : 0.0;
		double rollingRayTestsPerSec = hasElapsed && rolling.RayTestsSamples > 0 ? (rolling.RayTests / elapsedSec) : 0.0;
		double presentElapsedSec = presentRolling.ElapsedSec;
		bool hasPresentElapsed = presentElapsedSec > 0.0 && double.IsFinite(presentElapsedSec);
		double rollingPresentsPerSec = hasPresentElapsed && presentRolling.Steps > 0 ? (presentRolling.Steps / presentElapsedSec) : 0.0;

		_overlayHudSb.Clear();
		var lines = new System.Collections.Generic.List<string>(6);
		string hudMetadata = BuildHudMetadataLine();
		if (!string.IsNullOrWhiteSpace(hudMetadata))
		{
			MaybeLogHudMetadata(hudMetadata);
			AddHudMetadataOverlayLines(lines);
		}
		MaybeLogHudRuntimeSummary();
		RenderHealthSample latest = default;
		bool hasLatest = _renderHealthCount > 0;
		if (hasLatest)
			latest = GetRenderHealthSampleFromEnd(0);
		bool showProbeOnlyStatus = !UpdateEveryFrame && !hasLatest && HasHudProbeStatus();
		if (showProbeOnlyStatus)
		{
			AddHudProbeStatusLines(lines);
			Vector2 probeOverlayBasePos = new Vector2(16f, 24f);
			DebugOverlayBus.AddText(probeOverlayBasePos, string.Join("\n", lines), Colors.White);
			return;
		}

		double engineFps = Engine.GetFramesPerSecond();
		string FmtInt(bool has, int value) => has ? value.ToString() : "na";
		string FmtLong(bool has, long value) => has ? value.ToString() : "na";
		string FmtBool01(bool has, bool value) => has ? (value ? "1" : "0") : "na";
		string FmtDouble(bool has, double value, string fmt)
			=> (has && double.IsFinite(value)) ? value.ToString(fmt) : "na";

		string rowsPerSecText = (hasRows && double.IsFinite(rollingRowsPerSec) && rollingRowsPerSec > 0.0)
			? rollingRowsPerSec.ToString("0.0")
			: "na";
		string msPerRowText = (hasRows && double.IsFinite(rollingMsPerRow) && rollingMsPerRow > 0.0)
			? rollingMsPerRow.ToString("0.0")
			: "na";
		string msPerStepText = (hasSteps && double.IsFinite(rollingMsPerStep) && rollingMsPerStep >= 0.0)
			? rollingMsPerStep.ToString("0.0")
			: "na";
		string stepsPerSecText = (hasSteps && hasElapsed && double.IsFinite(rollingStepsPerSec) && rollingStepsPerSec >= 0.0)
			? rollingStepsPerSec.ToString("0.0")
			: "na";
		string hitsPerSecText = (hasSteps && hasElapsed && double.IsFinite(rollingHitsPerSec) && rollingHitsPerSec >= 0.0)
			? rollingHitsPerSec.ToString("0.0")
			: "na";
		string rayTestsPerSecText = (hasElapsed && rolling.RayTestsSamples > 0 && double.IsFinite(rollingRayTestsPerSec) && rollingRayTestsPerSec >= 0.0)
			? rollingRayTestsPerSec.ToString("0.0")
			: "na";
		string presentsPerSecText = (hasPresentElapsed && presentRolling.Steps > 0 && double.IsFinite(rollingPresentsPerSec) && rollingPresentsPerSec >= 0.0)
			? rollingPresentsPerSec.ToString("0.0")
			: "na";
		string etaText = "na";
		double etaFullFilmSec = 0.0;
		if (_filmHeight > 0 && hasRows && rollingRowsPerSec > 0.0)
		{
			etaFullFilmSec = _filmHeight / rollingRowsPerSec;
			if (double.IsFinite(etaFullFilmSec) && etaFullFilmSec >= 0.0)
				etaText = etaFullFilmSec.ToString("0.0");
		}
		bool latestHasHitRate = hasLatest && latest.TracedPixels > 0;
		double latestHitRate = latestHasHitRate ? (double)latest.Hits / latest.TracedPixels : 0.0;

		_overlayHudSb.Append("RH step=").Append(FmtInt(hasLatest, latest.StepIndex))
			.Append(" row=").Append(FmtInt(hasLatest, latest.RowCursorAfter))
			.Append(" adv=").Append(FmtInt(hasLatest, latest.RowsAdvanced))
			.Append(" bands=").Append(FmtInt(hasLatest, latest.BandsProcessed))
			.Append(" hitRate=").Append(FmtDouble(latestHasHitRate, latestHitRate, "0.000"))
			.Append(" fps=").Append(FmtDouble(double.IsFinite(engineFps) && engineFps >= 0.0, engineFps, "0.0"))
			.Append(" present/s=").Append(presentsPerSecText)
			.Append(" tickMs=").Append(FmtDouble(double.IsFinite(_lastFrameRenderMs) && _lastFrameRenderMs >= 0.0, _lastFrameRenderMs, "0.0"))
			.Append(" eta=").Append(etaText);
		lines.Add(_overlayHudSb.ToString());

		_overlayHudSb.Clear();
		_overlayHudSb.Append("roll ms/step=").Append(msPerStepText)
			.Append(" steps/s=").Append(stepsPerSecText)
			.Append(" rows/s=").Append(rowsPerSecText)
			.Append(" ms/row=").Append(msPerRowText)
			.Append(" hits/s=").Append(hitsPerSecText)
			.Append(" rayTests/s=").Append(rayTestsPerSecText);
		lines.Add(_overlayHudSb.ToString());

		_overlayHudSb.Clear();
		_overlayHudSb.Append("refresh px=").Append(_lastPresentedPixelsUpdated)
			.Append(" cover=").Append(FmtDouble(_filmWidth > 0 && _filmHeight > 0, _lastPresentedCoverageRatio, "0.000"))
			.Append(" fullFrames=").Append(_lastFullRefreshMeasured && _lastFramesToFullRefresh > 0 ? _lastFramesToFullRefresh.ToString() : "na")
			.Append(" fullMs=").Append(FmtDouble(_lastFullRefreshMeasured, _lastTimeToFullRefreshMs, "0.0"))
			.Append(" fullFps=").Append(FmtDouble(_lastFullRefreshMeasured, _lastEffectiveFullRefreshFps, "0.00"));
		lines.Add(_overlayHudSb.ToString());

		_overlayHudSb.Clear();
		_overlayHudSb.Append("prune=").Append(hasLatest ? (latest.UseGeometryTLASPruning ? "on" : "off") : "na")
			.Append(" trusted=").Append(FmtBool01(_testHasRenderHealthSnapshot, _testLastGeomTrusted))
			.Append(" reason=").Append(_testHasRenderHealthSnapshot ? _testLastGeomTrustReason : "na")
			.Append(" perPxOff=").Append(FmtDouble(_testHasRenderHealthSnapshot && _testLastGeomPerPxOffAvailable, _testLastGeomPerPxOff, "0.000"))
			.Append(" perPxOn=").Append(FmtDouble(_testHasRenderHealthSnapshot && _testLastGeomPerPxOnAvailable, _testLastGeomPerPxOn, "0.000"))
			.Append(" saved%=").Append(FmtDouble(_testHasRenderHealthSnapshot && _testLastGeomSavedPctAvailable, _testLastGeomSavedPct, "0.00"));
		lines.Add(_overlayHudSb.ToString());

		if (_renderHealthTestTrustEnforcementEnabled || DebugRenderHealthRollingOverlay)
		{
			int minP2 = GetRenderHealthMinP2SamplesForTrustEffective();
			int p2Every = RenderHealthPass2SampleEveryNSegments;
			if (_renderHealthTestTrustEnforcementEnabled && _renderHealthTestPass2SampleEveryNSegmentsOverride > 0)
				p2Every = _renderHealthTestPass2SampleEveryNSegmentsOverride;
			p2Every = Math.Max(1, p2Every);
			_overlayHudSb.Clear();
			_overlayHudSb.Append("gate metPix=").Append(FmtBool01(_testHasRenderHealthSnapshot, _testLastTrustGeomPixMet))
				.Append(" metRay=").Append(FmtBool01(_testHasRenderHealthSnapshot, _testLastTrustRayTestsMet))
				.Append(" metP2=").Append(FmtBool01(_testHasRenderHealthSnapshot, _testLastTrustP2Met))
				.Append(" minPix=").Append(_renderHealthTestMinGeomPixProcessedPerWindow)
				.Append(" minRay=").Append(_renderHealthTestMinGeomRayTestsTotalPerWindow)
				.Append(" minP2=").Append(minP2)
				.Append(" p2Every=").Append(p2Every);
			lines.Add(_overlayHudSb.ToString());
		}

		if (_renderHealthTestTrustEnforcementEnabled)
		{
			_overlayHudSb.Clear();
			_overlayHudSb.Append("raw pix=").Append(FmtLong(_testHasRenderHealthSnapshot, _testLastGeomPixProcessedRaw))
				.Append(" rays=").Append(FmtLong(_testHasRenderHealthSnapshot, _testLastGeomRayTestsTotalRaw))
				.Append(" p2=").Append(FmtLong(_testHasRenderHealthSnapshot, _testLastP2SampRaw))
				.Append(" noCand=").Append(FmtLong(_testHasRenderHealthSnapshot, _testLastGeomPixNoCandRaw))
				.Append(" seg0%=").Append(FmtDouble(_testHasRenderHealthSnapshot && _testLastGeomSegZeroRatePctAvailable, _testLastGeomSegZeroRatePct, "0.00"));
			lines.Add(_overlayHudSb.ToString());
		}

		bool showDiag = DebugRenderHealthRollingOverlay
			|| !_testHasRenderHealthSnapshot
			|| !string.Equals(_testLastGeomTrustReason, "ok", StringComparison.Ordinal);
		if (showDiag)
		{
			string exitText = hasLatest
				? (string.IsNullOrEmpty(latest.BudgetExitReason) ? "none" : latest.BudgetExitReason)
				: "na";
			_overlayHudSb.Clear();
			_overlayHudSb.Append("diag qray0=").Append(FmtInt(hasLatest, latest.QuickRayZeroCount))
				.Append(" hybridFB=").Append(FmtInt(hasLatest, latest.HybridFallbackCount))
				.Append(" exit=").Append(exitText)
				.Append(" topExit=").Append(_testHasRenderHealthSnapshot ? _testLastTopExit : "na")
				.Append(" stalled=").Append(FmtInt(hasLatest, _renderHealthStallSteps));
			lines.Add(_overlayHudSb.ToString());
		}

		if (lines.Count > 7)
			lines.RemoveRange(7, lines.Count - 7);

		Vector2 overlayBasePos = new Vector2(16f, 24f);
		DebugOverlayBus.AddText(overlayBasePos, string.Join("\n", lines), Colors.White);
	}

	public void SetHudFixtureName(string fixtureName)
	{
		_hudFixtureName = NormalizeHudValue(fixtureName);
	}

	public void SetHudTransportModel(string transportModel)
	{
		_hudTransportModel = NormalizeHudValue(transportModel);
	}

	public void SetHudProfileToken(string profileToken)
	{
		_hudProfileToken = NormalizeHudValue(profileToken);
	}

	public void SetHudSourcePatternMode(string sourcePatternMode)
	{
		_hudSourcePatternMode = NormalizeHudValue(sourcePatternMode);
	}

	public void SetHudRenderTestMode(string renderTestMode)
	{
		_hudRenderTestMode = NormalizeHudValue(renderTestMode).ToUpperInvariant();
	}

	public void SetHudRenderLoopStatus(string renderLoopStatus)
	{
		_hudRenderLoopStatus = NormalizeHudValue(renderLoopStatus).ToUpperInvariant();
	}

	public void SetHudRenderProbeRayCount(int rayCount)
	{
		_hudRenderProbeRayCount = rayCount >= 0 ? rayCount : -1;
	}

	public void SetHudMetricSteeringLaw(string metricSteeringLaw)
	{
		_hudMetricSteeringLaw = NormalizeHudValue(metricSteeringLaw);
	}

	public void SetHudMetricGainOverride(float metricGainOverride, bool active)
	{
		_hudMetricGainOverride = float.IsFinite(metricGainOverride) ? metricGainOverride : 1.0f;
		_hudMetricGainOverrideActive = active && float.IsFinite(metricGainOverride);
	}

	private string BuildHudMetadataLine()
	{
		_overlayHudSb.Clear();
		AppendHudToken(_overlayHudSb, "fixture", ResolveHudFixtureName());
		AppendHudToken(_overlayHudSb, "transport", ResolveHudTransportModel());
		AppendHudToken(_overlayHudSb, "profile", ResolveHudProfileToken());

		if (TryResolveHudMetricGainOverride(out float metricGainOverride))
		{
			AppendHudToken(_overlayHudSb, "metricGain", metricGainOverride.ToString("0.0##"));
		}
		string metricSteeringLaw = ResolveHudMetricSteeringLaw();
		if (!string.IsNullOrWhiteSpace(metricSteeringLaw))
		{
			AppendHudToken(_overlayHudSb, "metricLaw", metricSteeringLaw);
		}

		AppendHudToken(_overlayHudSb, "sourcePattern", ResolveHudSourcePatternMode());
		AppendHudToken(_overlayHudSb, "MODE", ResolveHudModePath());
		if (ShouldShowRuntimeMacroHudStatus())
		{
			AppendHudToken(_overlayHudSb, "macro", ResolveRuntimeMacroHudToken());
		}
		AppendHudToken(_overlayHudSb, "FILM_ACCUM", ResolveHudFilmAccumulationStatus());
		return _overlayHudSb.ToString();
	}

	private void AddHudMetadataOverlayLines(System.Collections.Generic.List<string> lines)
	{
		if (lines == null)
			return;

		_overlayHudSb.Clear();
		AppendHudToken(_overlayHudSb, "fixture", ResolveHudFixtureName());
		AppendHudToken(_overlayHudSb, "transport", ResolveHudTransportModel());
		AppendHudToken(_overlayHudSb, "mode", ResolveHudModePath());
		if (_overlayHudSb.Length > 0)
			lines.Add(_overlayHudSb.ToString());

		_overlayHudSb.Clear();
		AppendHudToken(_overlayHudSb, "profile", ResolveHudProfileToken());
		if (TryResolveHudMetricGainOverride(out float metricGainOverride))
		{
			AppendHudToken(_overlayHudSb, "metricGain", metricGainOverride.ToString("0.0##"));
		}
		string metricSteeringLaw = ResolveHudMetricSteeringLaw();
		if (!string.IsNullOrWhiteSpace(metricSteeringLaw))
		{
			AppendHudToken(_overlayHudSb, "metricLaw", metricSteeringLaw);
		}
		AppendHudToken(_overlayHudSb, "sourcePattern", ResolveHudSourcePatternMode());
		if (ShouldShowRuntimeMacroHudStatus())
		{
			AppendHudToken(_overlayHudSb, "macro", ResolveRuntimeMacroHudToken());
			AppendHudToken(_overlayHudSb, "camMotion", _runtimeMacroCameraMoving ? "moving" : "still");
		}
		AppendHudToken(_overlayHudSb, "filmAccum", ResolveHudFilmAccumulationStatus());
		if (_overlayHudSb.Length > 0)
			lines.Add(_overlayHudSb.ToString());
	}

	private void MaybeLogHudMetadata(string metadata)
	{
		if (string.IsNullOrWhiteSpace(metadata) ||
			string.Equals(metadata, _lastLoggedHudMetadata, StringComparison.Ordinal))
		{
			return;
		}

		_lastLoggedHudMetadata = metadata;
		GD.Print($"[HUDMeta] {metadata}");
	}

	private void MaybeLogHudRuntimeSummary()
	{
		string summary = BuildHudRuntimeSummaryLine();
		if (string.IsNullOrWhiteSpace(summary) ||
			string.Equals(summary, _lastLoggedHudRuntimeSummary, StringComparison.Ordinal))
		{
			return;
		}

		_lastLoggedHudRuntimeSummary = summary;
		GD.Print($"[RuntimeMode] {summary}");
	}

	private string ResolveHudFixtureName()
	{
		if (!string.IsNullOrWhiteSpace(_hudFixtureName))
			return _hudFixtureName;

		string scenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		if (scenePath.IndexOf("blackhole", StringComparison.OrdinalIgnoreCase) >= 0)
			return "blackhole_minimal";
		if (scenePath.IndexOf("einstein", StringComparison.OrdinalIgnoreCase) >= 0)
			return "einstein_ring_minimal";
		if (scenePath.IndexOf("curved-minimal", StringComparison.OrdinalIgnoreCase) >= 0)
			return "curved_minimal";
		if (scenePath.IndexOf("straight", StringComparison.OrdinalIgnoreCase) >= 0)
			return "straight";
		return string.Empty;
	}

	private string ResolveHudTransportModel()
	{
		if (!string.IsNullOrWhiteSpace(_hudTransportModel))
			return _hudTransportModel;

		foreach (string arg in GetHudCmdArgs())
		{
			if (TryGetHudArgValue(arg, "--blackhole-transport-model=", out string value) ||
				TryGetHudArgValue(arg, "--einstein-transport-model=", out value) ||
				TryGetHudArgValue(arg, "--transport-model=", out value))
			{
				return NormalizeTransportHudValue(value);
			}
		}

		string scenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		if (scenePath.IndexOf("-metric", StringComparison.OrdinalIgnoreCase) >= 0)
			return TransportModel.Metric_NullGeodesic.ToString();
		if (scenePath.IndexOf("-grin", StringComparison.OrdinalIgnoreCase) >= 0)
			return TransportModel.GRIN_Optical.ToString();

		return string.Empty;
	}

	private string ResolveHudProfileToken()
	{
		if (!string.IsNullOrWhiteSpace(_hudProfileToken))
			return _hudProfileToken;

		foreach (string arg in GetHudCmdArgs())
		{
			if (TryGetHudArgValue(arg, "--render-test-profile=", out string value))
				return NormalizeHudValue(value);
		}

		return string.Empty;
	}

	private string ResolveHudSourcePatternMode()
	{
		return _hudSourcePatternMode;
	}

	private bool ShouldEmitMetadataOnlyOverlay()
	{
		return HasHudProbeStatus() || !string.IsNullOrWhiteSpace(ResolveHudModePath());
	}

	private bool HasHudProbeStatus()
	{
		return !string.IsNullOrWhiteSpace(_hudRenderTestMode)
			|| !string.IsNullOrWhiteSpace(_hudRenderLoopStatus)
			|| _hudRenderProbeRayCount >= 0;
	}

	private void AddHudProbeStatusLines(System.Collections.Generic.List<string> lines)
	{
		if (lines == null)
			return;

		if (_hudRenderProbeRayCount >= 0)
			lines.Add($"RAYS={_hudRenderProbeRayCount}");
	}

	private string ResolveHudModePath()
	{
		if (!string.IsNullOrWhiteSpace(_hudRenderTestMode))
			return _hudRenderTestMode;

		foreach (string arg in GetHudCmdArgs())
		{
			string trimmed = NormalizeHudValue(arg);
			if (string.Equals(trimmed, "--render-test", StringComparison.OrdinalIgnoreCase) ||
				trimmed.StartsWith("--render-test-", StringComparison.OrdinalIgnoreCase) ||
				trimmed.StartsWith("--render-test-fixture=", StringComparison.OrdinalIgnoreCase))
			{
				return "RENDER_TEST_MATRIX";
			}
		}

		return UpdateEveryFrame ? "FULL_RENDER" : "FIXTURE_PROBE";
	}

	private string ResolveHudFilmAccumulationStatus()
	{
		if (!string.IsNullOrWhiteSpace(_hudRenderLoopStatus))
		{
			string explicitStatus = _hudRenderLoopStatus.ToUpperInvariant();
			if (explicitStatus is "ENABLED" or "ON" or "TRUE" or "1")
				return "ON";
			if (explicitStatus is "DISABLED" or "OFF" or "FALSE" or "0")
				return "OFF";
			return explicitStatus;
		}

		foreach (string arg in GetHudCmdArgs())
		{
			string trimmed = NormalizeHudValue(arg);
			if (string.Equals(trimmed, "--render-test", StringComparison.OrdinalIgnoreCase) ||
				trimmed.StartsWith("--render-test-", StringComparison.OrdinalIgnoreCase) ||
				trimmed.StartsWith("--render-test-fixture=", StringComparison.OrdinalIgnoreCase))
			{
				return "ON";
			}
		}

		return UpdateEveryFrame ? "ON" : "OFF";
	}

	private string BuildHudRuntimeSummaryLine()
	{
		_overlayHudSb.Clear();
		AppendHudToken(_overlayHudSb, "scene", ResolveHudSceneName());
		AppendHudToken(_overlayHudSb, "fixture", ResolveHudFixtureName());
		AppendHudToken(_overlayHudSb, "transport", ResolveHudTransportModel());
		AppendHudToken(_overlayHudSb, "mode", ResolveHudModePath());
		if (ShouldShowRuntimeMacroHudStatus())
		{
			AppendHudToken(_overlayHudSb, "macro", ResolveRuntimeMacroHudToken());
			AppendHudToken(_overlayHudSb, "camMotion", _runtimeMacroCameraMoving ? "moving" : "still");
		}
		AppendHudToken(_overlayHudSb, "filmAccum", ResolveHudFilmAccumulationStatus());
		return _overlayHudSb.ToString();
	}

	private string ResolveRuntimeMacroHudToken()
	{
		if (!_runtimeMacroModeActive)
			return $"inactive({GetRuntimeMacroModeLabel(RuntimeMacroSelectedMode)})";
		return GetRuntimeMacroModeLabel(RuntimeMacroSelectedMode);
	}

	private string ResolveHudSceneName()
	{
		string scenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(scenePath))
			return NormalizeHudValue(Path.GetFileNameWithoutExtension(scenePath));

		return NormalizeHudValue(GetTree().CurrentScene?.Name ?? string.Empty);
	}

	private string ResolveHudMetricSteeringLaw()
	{
		if (!string.IsNullOrWhiteSpace(_hudMetricSteeringLaw))
		{
			return _hudMetricSteeringLaw;
		}

		string transportModel = ResolveHudTransportModel();
		if (!string.Equals(transportModel, TransportModel.Metric_NullGeodesic.ToString(), StringComparison.Ordinal))
		{
			return string.Empty;
		}

		foreach (string arg in GetHudCmdArgs())
		{
			if (TryGetHudArgValue(arg, "--metric-steering-law=", out string value) ||
				TryGetHudArgValue(arg, "--metric-law=", out value))
			{
				return NormalizeMetricSteeringLawHudValue(value);
			}
		}

		return RayBeamRenderer.GetMetricSteeringLawToken(
			RayBeamRenderer.ResolveMetricSteeringLawOverride(MetricSteeringLaw.MetricLaw_CurrentEnvelope));
	}

	private bool TryResolveHudMetricGainOverride(out float metricGainOverride)
	{
		metricGainOverride = 1.0f;
		if (_hudMetricGainOverrideActive)
		{
			metricGainOverride = _hudMetricGainOverride;
			return true;
		}

		string transportModel = ResolveHudTransportModel();
		if (!string.Equals(transportModel, TransportModel.Metric_NullGeodesic.ToString(), StringComparison.Ordinal))
			return false;

		float parsedOverride = RayBeamRenderer.ResolveMetricComparisonScalarOverride();
		if (!float.IsFinite(parsedOverride) || Mathf.IsEqualApprox(parsedOverride, 1.0f))
			return false;

		metricGainOverride = parsedOverride;
		return true;
	}

	private void ApplyHudOverlayVisualSettings()
	{
		if (_filmOverlay == null || !GodotObject.IsInstanceValid(_filmOverlay))
			return;

		_filmOverlay.HudFontScale = ResolveHudOverlayFontScale();
		_filmOverlay.QueueRedraw();
	}

	private float ResolveHudOverlayFontScale()
	{
		float resolved = Mathf.Clamp(HudOverlayFontScale, HudOverlayFontScaleMin, HudOverlayFontScaleMax);
		foreach (string arg in GetHudCmdArgs())
		{
			if (!(TryGetHudArgValue(arg, "--hud-font-scale=", out string value) ||
				TryGetHudArgValue(arg, "--hud-scale=", out value)))
			{
				continue;
			}

			if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ||
				float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
			{
				resolved = parsed;
			}
		}

		return Mathf.Clamp(resolved, HudOverlayFontScaleMin, HudOverlayFontScaleMax);
	}

	private static void AppendHudToken(StringBuilder sb, string key, string value)
	{
		if (sb == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
			return;

		if (sb.Length > 0)
			sb.Append(' ');

		sb.Append(key).Append('=').Append(value);
	}

	private static string NormalizeHudValue(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
	}

	private static string NormalizeTransportHudValue(string value)
	{
		string normalized = NormalizeHudValue(value);
		if (string.IsNullOrWhiteSpace(normalized))
			return string.Empty;

		if (string.Equals(normalized, "grin", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(normalized, "optical", StringComparison.OrdinalIgnoreCase))
		{
			return TransportModel.GRIN_Optical.ToString();
		}
		if (string.Equals(normalized, "metric", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(normalized, "nullgeodesic", StringComparison.OrdinalIgnoreCase))
		{
			return TransportModel.Metric_NullGeodesic.ToString();
		}

		return normalized;
	}

	private static string NormalizeMetricSteeringLawHudValue(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		string normalized = value.Replace("-", string.Empty)
			.Replace("_", string.Empty)
			.Trim()
			.ToLowerInvariant();
		return normalized switch
		{
			"impact" or "impactparameter" or "impactparameterapprox" or "metriclawimpactparameterapprox"
				=> RayBeamRenderer.GetMetricSteeringLawToken(MetricSteeringLaw.MetricLaw_ImpactParameterApprox),
			"current" or "currentenvelope" or "metriclawcurrentenvelope" or "baseline"
				=> RayBeamRenderer.GetMetricSteeringLawToken(MetricSteeringLaw.MetricLaw_CurrentEnvelope),
			_ => NormalizeHudValue(value)
		};
	}

	private static bool TryGetHudArgValue(string arg, string prefix, out string value)
	{
		value = string.Empty;
		if (string.IsNullOrWhiteSpace(arg) || string.IsNullOrWhiteSpace(prefix))
			return false;

		string trimmed = arg.Trim();
		if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			return false;

		value = NormalizeHudValue(trimmed.Substring(prefix.Length));
		return !string.IsNullOrWhiteSpace(value);
	}

	private static string[] GetHudCmdArgs()
	{
		string[] userArgs = OS.GetCmdlineUserArgs();
		string[] args = OS.GetCmdlineArgs();
		if ((userArgs == null || userArgs.Length == 0) && (args == null || args.Length == 0))
			return Array.Empty<string>();
		if (userArgs == null || userArgs.Length == 0)
			return args ?? Array.Empty<string>();
		if (args == null || args.Length == 0)
			return userArgs;

		var merged = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
		var ordered = new System.Collections.Generic.List<string>(userArgs.Length + args.Length);
		for (int i = 0; i < userArgs.Length; i++)
		{
			string token = NormalizeHudValue(userArgs[i]);
			if (!string.IsNullOrWhiteSpace(token) && merged.Add(token))
				ordered.Add(token);
		}
		for (int i = 0; i < args.Length; i++)
		{
			string token = NormalizeHudValue(args[i]);
			if (!string.IsNullOrWhiteSpace(token) && merged.Add(token))
				ordered.Add(token);
		}

		return ordered.ToArray();
	}

	private void ApplyTileMetricsCmdArgs()
	{
		foreach (string arg in GetHudCmdArgs())
		{
			if (TryGetHudArgValue(arg, "--tile-metrics=", out string enabledValue))
			{
				string normalized = enabledValue.Trim().ToLowerInvariant();
				EnableTileMetricsScaffold = normalized is "1" or "true" or "on" or "yes";
				continue;
			}

			if (TryGetHudArgValue(arg, "--tile-metrics-subtile-width=", out string subtileWidthValue)
				&& int.TryParse(subtileWidthValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedWidth))
			{
				TileMetricsSubtileWidth = Math.Max(1, parsedWidth);
			}

			if (TryGetHudArgValue(arg, "--tile-metrics-max-logs=", out string maxLogsValue)
				&& int.TryParse(maxLogsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedMaxLogs))
			{
				TileMetricsMaxLogsPerFrame = Math.Max(0, parsedMaxLogs);
			}

			if (TryGetHudArgValue(arg, "--tile-metrics-simulate-reorder=", out string simulateValue))
			{
				string normalized = simulateValue.Trim().ToLowerInvariant();
				EnableTileMetricsReorderSimulation = normalized is "1" or "true" or "on" or "yes";
				continue;
			}

			if (TryGetHudArgValue(arg, "--tile-metrics-reorder-execution=", out string executeValue))
			{
				string normalized = executeValue.Trim().ToLowerInvariant();
				EnableTileMetricsReorderExecution = normalized is "1" or "true" or "on" or "yes";
				continue;
			}

			if (TryGetHudArgValue(arg, "--tile-metrics-persistent-priors=", out string priorsValue))
			{
				string normalized = priorsValue.Trim().ToLowerInvariant();
				EnableTileMetricsPersistentPriors = normalized is "1" or "true" or "on" or "yes";
				continue;
			}

			if (TryGetHudArgValue(arg, "--experimental-subtile-scheduler=", out string schedulerValue))
			{
				string normalized = schedulerValue.Trim().ToLowerInvariant();
				EnableTileMetricsReorderExecution = normalized is "1" or "true" or "on" or "yes";
			}
		}
	}

	private void ApplyThreadingCmdArgs()
	{
		foreach (string arg in GetHudCmdArgs())
		{
			if (TryGetHudArgValue(arg, "--threaded-bands=", out string enabledValue))
			{
				string normalized = enabledValue.Trim().ToLowerInvariant();
				UseThreadedBands = normalized is "1" or "true" or "on" or "yes";
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-band-workers=", out string workersValue)
				&& int.TryParse(workersValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedWorkers))
			{
				ThreadedBandWorkerCount = Math.Clamp(parsedWorkers, 1, 16);
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-band-rows=", out string rowsValue)
				&& int.TryParse(rowsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedRows))
			{
				ThreadedBandRowsPerChunk = Math.Max(1, parsedRows);
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-pass2-local-accumulation=", out string pass2EnabledValue))
			{
				string normalized = pass2EnabledValue.Trim().ToLowerInvariant();
				UseThreadedPass2LocalAccumulation = normalized is "1" or "true" or "on" or "yes";
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-pass2-candidate-eval=", out string pass2CandidateEvalValue))
			{
				string normalized = pass2CandidateEvalValue.Trim().ToLowerInvariant();
				UseThreadedPass2CandidateEval = normalized is "1" or "true" or "on" or "yes";
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-pass2-candidate-workers=", out string pass2CandidateWorkersValue)
				&& int.TryParse(pass2CandidateWorkersValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPass2CandidateWorkers))
			{
				ThreadedPass2CandidateWorkers = Math.Clamp(parsedPass2CandidateWorkers, 1, 16);
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-pass2-candidate-rows=", out string pass2CandidateRowsValue)
				&& int.TryParse(pass2CandidateRowsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPass2CandidateRows))
			{
				ThreadedPass2CandidateRowsPerChunk = Math.Max(1, parsedPass2CandidateRows);
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-pass2-query-resolve=", out string pass2QueryResolveValue))
			{
				string normalized = pass2QueryResolveValue.Trim().ToLowerInvariant();
				UseThreadedPass2QueryResolve = normalized is "1" or "true" or "on" or "yes";
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-pass2-query-workers=", out string pass2QueryWorkersValue)
				&& int.TryParse(pass2QueryWorkersValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPass2QueryWorkers))
			{
				ThreadedPass2QueryWorkers = Math.Clamp(parsedPass2QueryWorkers, 1, 16);
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-pass2-query-rows=", out string pass2QueryRowsValue)
				&& int.TryParse(pass2QueryRowsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPass2QueryRows))
			{
				ThreadedPass2QueryRowsPerChunk = Math.Max(1, parsedPass2QueryRows);
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-pass2-workers=", out string pass2WorkersValue)
				&& int.TryParse(pass2WorkersValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPass2Workers))
			{
				ThreadedPass2WorkerCount = Math.Clamp(parsedPass2Workers, 1, 16);
				continue;
			}

			if (TryGetHudArgValue(arg, "--threaded-pass2-rows=", out string pass2RowsValue)
				&& int.TryParse(pass2RowsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPass2Rows))
			{
				ThreadedPass2RowsPerChunk = Math.Max(1, parsedPass2Rows);
			}
		}
	}

	private void ApplyDeterministicBenchmarkCmdArgs()
	{
		bool enableBenchmarkLock = false;
		uint benchmarkSeed = 1;

		foreach (string arg in GetHudCmdArgs())
		{
			if (TryGetHudArgValue(arg, "--benchmark-lock=", out string lockValue)
				|| TryGetHudArgValue(arg, "--benchmark-deterministic=", out lockValue))
			{
				string normalized = lockValue.Trim().ToLowerInvariant();
				enableBenchmarkLock = normalized is "1" or "true" or "on" or "yes";
				continue;
			}

			if (TryGetHudArgValue(arg, "--benchmark-seed=", out string seedValue)
				|| TryGetHudArgValue(arg, "--benchmark-fixed-seed=", out seedValue))
			{
				if (uint.TryParse(seedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedSeed))
				{
					benchmarkSeed = Math.Max(1u, parsedSeed);
				}
			}
		}

		if (!enableBenchmarkLock)
			return;

		_deterministicBenchmarkModeRequested = true;
		_deterministicBenchmarkSeed = benchmarkSeed;
		ApplyDeterministicBenchmarkLockIn(benchmarkSeed);
	}

	private void ApplyDeterministicBenchmarkLockIn(uint seed)
	{
		ResearchOverrides.Enabled = true;
		ResearchOverrides.Override_ResearchEnabled = true;
		ResearchOverrides.ResearchEnabled = true;
		ResearchOverrides.Override_DeterministicMode = true;
		ResearchOverrides.DeterministicMode = true;
		ResearchOverrides.Override_FixedSeed = true;
		ResearchOverrides.FixedSeed = Math.Max(1u, seed);

		UseThreadedBands = true;
		ThreadedBandWorkerCount = 1;
		ThreadedBandRowsPerChunk = 4;
		EnableTileMetricsReorderSimulation = false;
		EnableTileMetricsReorderExecution = false;
		EnableTileMetricsPersistentPriors = false;
	}

	private void ThrottleBusLog(double delta, SceneSnapshot snapshot)
	{
		_busLogTimerSec += Math.Max(0.0, delta);
		if (_busLogTimerSec < 1.0)
		{
			return;
		}

		_busLogTimerSec -= 1.0;
		var fieldsCount = snapshot.Fields?.Count ?? 0;
		var gridOk = snapshot.CurvatureGrid != null ? "OK" : "NULL";
		GD.Print($"[BUS SET] frameId={_frameId} grid={gridOk} fields={fieldsCount}");
	}

	private void ThrottleSnapshotSummary(double delta, SceneSnapshot snapshot)
	{
		_snapshotLogTimerSec += Math.Max(0.0, delta);
		if (_snapshotLogTimerSec < 1.0)
		{
			return;
		}

		_snapshotLogTimerSec -= 1.0;
		GD.Print(snapshot.DebugSummary());
	}

	public void RenderStep()
	{
		// DECISION: guard against re-entrant RenderStep calls.
		if (Interlocked.CompareExchange(ref _renderStepActive, 1, 0) != 0)
		{
			// DECISION: log re-entry warning once.
			if (!_renderStepReentryWarned)
			{
				_renderStepReentryWarned = true;
				GD.PrintErr($"[RenderStep][Guard] re-entry blocked. frame={_frameIndex} row={_rowCursor} cam={_cam?.GetPath()} rbr={_rbr?.GetPath()}");
			}
			// EFFECT: disable UpdateEveryFrame to avoid repeated contention.
			UpdateEveryFrame = false;
			return;
		}

		ResolveEffectiveConfig(out EffectiveConfig cfg);
		long renderStepStartTimestamp = Stopwatch.GetTimestamp();
		bool researchAppliedRayMarchClamp = false;
		RayBeamRenderer.SharedSnapshot researchRestoreSnapshot = cfg.SharedRaySnapshot;
		bool skipBandPhysicsForDiagnostics = false;
		bool beganBoundaryValidationRun = false;
		try
		{
			if (cfg.Research.ResearchEnabled)
			{
				if (cfg.RayMarch.HasRenderer)
				{
					int clampedStepsPerRay = Mathf.Min(cfg.RayMarch.StepsPerRay, Mathf.Max(1, cfg.Research.MaxStepsPerRay));
					if (clampedStepsPerRay != cfg.RayMarch.StepsPerRay)
					{
						_researchClampedStepsPerRayCount++;
					}
					cfg.RayMarch.StepsPerRay = Mathf.Max(1, clampedStepsPerRay);

					float clampedMinStepLength = Mathf.Max(cfg.RayMarch.MinStepLength, Mathf.Max(0f, cfg.Research.DtMin));
					float clampedMaxStepLength = Mathf.Min(cfg.RayMarch.MaxStepLength, Mathf.Max(clampedMinStepLength, cfg.Research.DtMax));
					float minStepLength = Mathf.Min(clampedMinStepLength, clampedMaxStepLength);
					float maxStepLength = Mathf.Max(clampedMinStepLength, clampedMaxStepLength);
					cfg.RayMarch.MinStepLength = minStepLength;
					cfg.RayMarch.MaxStepLength = maxStepLength;
					cfg.RayMarch.StepLength = Mathf.Clamp(cfg.RayMarch.StepLength, minStepLength, maxStepLength);
					cfg.RayMarch.MaxSegPerRay = _rbr != null
						? _rbr.EstimateMaxSegmentsPerRay()
						: (Mathf.Max(1, cfg.RayMarch.StepsPerRay / Mathf.Max(1, cfg.RayMarch.CollisionEveryNSteps)) + 2);

					if (_rbr != null && cfg.SharedRaySnapshot.HasRenderer)
					{
						_rbr.StepsPerRay = cfg.RayMarch.StepsPerRay;
						_rbr.MinStepLength = cfg.RayMarch.MinStepLength;
						_rbr.MaxStepLength = cfg.RayMarch.MaxStepLength;
						_rbr.StepLength = cfg.RayMarch.StepLength;
						researchAppliedRayMarchClamp = true;
					}

					if (cfg.SoftGate.UseRayBeamSettings)
					{
						bool used = false;
						float stepLength = cfg.RayMarch.StepLength;
						float softGateMinStepLength = cfg.RayMarch.MinStepLength;
						float softGateMaxStepLength = cfg.RayMarch.MaxStepLength;
						float stepAdaptGain = cfg.RayMarch.StepAdaptGain;
						bool stepsFinite = float.IsFinite(stepLength)
							&& float.IsFinite(softGateMinStepLength)
							&& float.IsFinite(softGateMaxStepLength)
							&& float.IsFinite(stepAdaptGain);
						if (stepsFinite)
						{
							float minStep = Mathf.Min(softGateMinStepLength, softGateMaxStepLength);
							float maxStep = Mathf.Max(softGateMinStepLength, softGateMaxStepLength);
							float effStepLen = Mathf.Clamp(stepLength, minStep, maxStep);
							cfg.SoftGate.EffectiveStepLength = effStepLen;
							cfg.SoftGate.MinSegmentLength = cfg.SoftGate.MinSegLenSteps * effStepLen;

							float stepScale = effStepLen > 0f
								? Mathf.Clamp(1f / effStepLen, 0.25f, 4f)
								: 1f;
							float strideScale = Mathf.Clamp(1f / Mathf.Max(1, cfg.Film.PixelStride), 0.125f, 1f);
							int derivedMaxAttemptsPerFrame = Mathf.Clamp(
								Mathf.RoundToInt(Pass2SoftGateMaxAttemptsPerFrame * stepScale * strideScale),
								Pass2SoftGateMaxAttemptsPerFrameMin,
								Pass2SoftGateMaxAttemptsPerFrameMax);
							int derivedMaxSubdividedCallsPerFrame = Mathf.Clamp(
								Mathf.RoundToInt(Pass2SoftGateMaxSubdividedCallsPerFrame * stepScale * strideScale),
								Pass2SoftGateMaxSubdividedCallsPerFrameMin,
								Pass2SoftGateMaxSubdividedCallsPerFrameMax);

							cfg.SoftGate.MaxAttemptsPerFrame = derivedMaxAttemptsPerFrame;
							cfg.SoftGate.MaxSubdividedCallsPerFrame = derivedMaxSubdividedCallsPerFrame;
							used = true;
						}
						cfg.SoftGate.UseRayBeamSettingsActive = used;
					}
				}

				if (cfg.Research.DeterministicMode)
				{
					_researchDeterministicFramesCount++;
					cfg.SoftGate.RandomProbeChance = 0f;
					_rng.Seed = (ulong)cfg.Research.FixedSeed + (ulong)(uint)_frameIndex;
				}

				if (cfg.Research.ValidationEnabled)
				{
					// TODO: Run the research validation harness at a deterministic boundary.
				}
			}

			LogEffectiveConfigIfChanged(in cfg);
			LogResearchConfigIfChanged(in cfg);
			EmitDeterministicBenchmarkModeOnce(in cfg);
			EffectiveBroadphaseSettings broadphaseCfg = cfg.Broadphase;
			EffectiveSoftGateSettings softGateCfg = cfg.SoftGate;
			EffectiveRayMarchSettings rayCfg = cfg.RayMarch;
			EffectiveFilmSettings filmCfg = cfg.Film;
			bool effQuickRay = broadphaseCfg.UseQuickRay;
			bool effOverlap = broadphaseCfg.UseOverlap;
			int fixtureDebugTraceLogsRemainingThisStep = cfg.FixtureDebugTraceEnabled
				? Math.Max(0, cfg.FixtureDebugTraceMaxLogsPerStep)
				: 0;
			// Deterministic per-kind quotas ensure rare classes (e.g. source/absorbed) are still sampled.
			int fixtureTraceSourceRemaining = cfg.FixtureDebugTraceEnabled ? 3 : 0;
			int fixtureTraceBackgroundRemaining = cfg.FixtureDebugTraceEnabled ? 3 : 0;
			int fixtureTraceAbsorbedRemaining = cfg.FixtureDebugTraceEnabled ? 3 : 0;
			int fixtureTraceMissRemaining = cfg.FixtureDebugTraceEnabled ? 3 : 0;

		// DECISION: record starting row for forward-progress guard.
		int startRow = _rowCursor;
		int rowCursorStart = _rowCursor;
		int rowCursorEnd = _rowCursor;
		int processedPixelsThisStep = 0;
		int renderHealthRowCursorBefore = _rowCursor;
		bool rowCursorResetThisStep = false;
		int budgetFrameId = (int)Engine.GetFramesDrawn();
		if (_budgetExitFrameId != budgetFrameId)
		{
			_budgetExitFrameId = budgetFrameId;
			_budgetExitReasonsThisFrame.Clear();
		}

		// EFFECT: start timing for watchdog/budget checks.
		Stopwatch renderStepWatch = Stopwatch.StartNew();
		bool renderStepAbort = false;
		bool renderStepAbortLogged = false;
		string renderStepAbortReason = "";
		bool renderStepStopLogged = false;
		bool bandCommittedThisStep = false;
		bool bandAttemptedThisStep = false;
		bool budgetStop = false;
		bool budgetStopLogged = false;
		string budgetStopReason = "";
		int budgetStopRowStart = _rowCursor;
		int budgetStopRowCursor = _rowCursor;
		int budgetStopRowEnd = _rowCursor;
		bool softGateDisabledThisFrame = false;
		string softGateDisableReason = "";
		bool softGateDisableLogged = false;
		bool statsEnabled = false;
		bool framePerfEnabled = false;
		bool frameStart = false;
		PerfScope frameScope = default;
		PerfScope schedulerScope = default;
		bool schedulerScopeActive = false;
		ulong schedulerStartUsec = 0;
		ulong pass1StartUsec = 0;
		ulong pass1EndUsec = 0;
		ulong pass2StartUsec = 0;
		ulong pass2EndUsec = 0;
		bool pass1SkippedThisStep = false;
		bool pendingPass2 = false;
		int rowsPerFrame = 1;
		int yStart = _rowCursor;
		int yEnd = _rowCursor;
		int bandH = 0;
		int bandHits = 0;
		int bandTracedPixels = 0;
		int processedPixelsThisBand = 0;
		int bandNoCandidatePixels = 0;
		string renderPhase = "enter";
		bool pass1CompletedThisStep = false;
		bool pass2CompletedThisStep = false;
		bool bandCompletedThisStep = false;
		bool bandSummaryLoggedThisBand = false;
		int bandStartRowCursor = _rowCursor;
		long pass1StepsIntegrated = 0;
		int filmW = _filmWidth;
		int pass2SoftGateMaxAttemptsPerFrameEffective = 0;
		int pass2SoftGateMaxSubdividedCallsPerFrameEffective = 0;
		long pass2SampledSegments = 0;
		double pass2RadiusSum = 0.0;
		float pass2RadiusMax = 0f;
		double pass2EnvDiagSum = 0.0;
		float pass2EnvDiagMax = 0f;
		double pass2EnvelopeInflationSum = 0.0;
		float pass2EnvelopeInflationMax = 0f;
		long pass2CandidateCount0 = 0;
		long pass2CandidateCount1To2 = 0;
		long pass2CandidateCount3To8 = 0;
		long pass2CandidateCount9To32 = 0;
		long pass2CandidateCount33Plus = 0;
		bool useGeomTlasPruningForStep = false;
		bool geomPruneRequestedForStep = false;
		bool geomHealthPartialForStep = true;
		int renderHealthPass2SampleEveryForStep = Math.Max(1, RenderHealthPass2SampleEveryNSegments);
		_geomCandidatesTotalThisFrame = 0;
		_geomCandidatesSegmentsThisFrame = 0;

		void LogBudgetExitOnce(string reason, int rowCursor)
		{
			if (string.IsNullOrEmpty(reason)) return;
			if (_budgetExitReasonsThisFrame.Contains(reason)) return;
			_budgetExitReasonsThisFrame.Add(reason);
			long elapsedMs = renderStepWatch.ElapsedMilliseconds;
			int rowsDoneThisStep = rowCursor >= startRow ? rowCursor - startRow : 0;
			int pixelCountLocal = bandH > 0 && filmW > 0 ? bandH * filmW : 0;
			int pixelCap = cfg.RenderStepMaxPixelsPerFrame > 0 ? cfg.RenderStepMaxPixelsPerFrame : 0;
			int attemptsCap = pass2SoftGateMaxAttemptsPerFrameEffective;
			int subdivCap = pass2SoftGateMaxSubdividedCallsPerFrameEffective;
			string bandContext = reason == "guard_no_candidates_band"
				? $" band=[{yStart},{yEnd}) repeats={_noCandidateBandStallSteps}"
				: "";
			GD.Print(
				$"[BudgetExit] frame={_frameIndex} row={rowCursor} reason={reason} elapsedMs={elapsedMs} " +
				$"rowsDoneThisStep={rowsDoneThisStep} hitsThisBand={bandHits} " +
				$"attempts={_softGateAttemptsUsedThisFrame}/{attemptsCap} " +
				$"subdiv={_softGateSubdividedCallsUsedThisFrame}/{subdivCap} " +
				$"px={pixelCountLocal}/{pixelCap}{bandContext}");
		}

		try
		{
			ulong t0 = Time.GetTicksUsec();
			// DECISION: enable stats when profiling or verbose logs are on.
			statsEnabled = cfg.EnableProfiling || cfg.VerbosePerfLogs;
			// DECISION: enable frame perf when configured.
			framePerfEnabled = cfg.EnableFramePerf;
			// DECISION: enable frame perf scope only when enabled.
			if (framePerfEnabled) frameScope = new PerfScope(_framePerf, PerfStage.FrameTotal);

		// Soft-gate debug toggles
		/////////////////////////////
		// DECISION: enable debug tiers based on verbosity level.
		bool softGateDebugEnabled = softGateCfg.DebugEnabled && softGateCfg.DebugVerbosity > 0;
		bool softGateBandEnabled = softGateDebugEnabled && softGateCfg.DebugVerbosity >= 2;
		bool softGateSegEnabled = softGateDebugEnabled && softGateCfg.DebugVerbosity >= 3;
		/////////////////////////////

			// DECISION: resize film buffers if resolution settings changed.
			bool resizedFilm = EnsureFilmImageSize(in cfg);
			int settingsHash = ComputeFilmSettingsHash(in cfg);
			// DECISION: reset row cursor when film settings change.
			if (_hasFilmSettingsHash && settingsHash != _lastFilmSettingsHash)
			{
				// DECISION: defer settings resets mid-band so we don't keep restarting the same rows.
				if (_rowCursor != 0 && !_pendingRowCursorReset)
				{
					_pendingRowCursorReset = true;
					_pendingRowCursorResetReason = "settings_dirty";
					GD.Print($"[RenderStep][DeferReset] reason=settings_dirty row={_rowCursor} -> defer until band advance");
				}
				else
				{
					ResetRowCursor("settings_dirty");
					rowCursorResetThisStep = true;
				}
			}
			_lastFilmSettingsHash = settingsHash;
			_hasFilmSettingsHash = true;

			Camera3D activeCam = GetViewport().GetCamera3D();
			// DECISION: sync active camera changes.
			if (activeCam != null)
			{
				ulong camId = activeCam.GetInstanceId();
				// DECISION: reset when camera instance changes.
				if (_cam != activeCam || (!_hasLastCameraInstanceId || camId != _lastCameraInstanceId))
				{
					_cam = activeCam;
					_lastCameraInstanceId = camId;
					_hasLastCameraInstanceId = true;
					ResetRowCursor("camera_dirty");
					rowCursorResetThisStep = true;
				}
			}
			// DECISION: wrap when we finished all rows.
			if (_rowCursor >= _filmHeight)
			{
				ResetRowCursor("completed");
				rowCursorResetThisStep = true;
			}
			if (rowCursorResetThisStep)
			{
				startRow = _rowCursor;
				bandStartRowCursor = _rowCursor;
				rowCursorStart = _rowCursor;
			}

			// DECISION: this is the start of a frame when row cursor wraps to 0.
			frameStart = _rowCursor == 0;
			filmW = _filmWidth;
			int filmH = _filmHeight;
			// CONTROL FACTOR: PixelStride reduces sampling density.
			int stride = filmCfg.PixelStride;
			long tracedPixels = (long)filmW * filmH / Math.Max(1, stride * stride);

			float pass2SoftGateMinSegmentLengthEffective = softGateCfg.MinSegmentLength;
			pass2SoftGateMaxAttemptsPerFrameEffective = softGateCfg.MaxAttemptsPerFrame;
			pass2SoftGateMaxSubdividedCallsPerFrameEffective = softGateCfg.MaxSubdividedCallsPerFrame;
			float pass2SoftGateEffStepLen = softGateCfg.EffectiveStepLength;
			bool pass2SoftGateUseRayBeamSettingsActive = softGateCfg.UseRayBeamSettingsActive;

			// CONTROL FACTOR: effective time budget for RenderStep.
			float effectiveMaxMs = cfg.RenderStepMaxMs;
			// DECISION: clamp effective budget when UpdateEveryFrame budget is configured.
			if (cfg.UpdateEveryFrame && cfg.UpdateEveryFrameBudgetMs > 0f)
			{
				// DECISION: choose the tighter of RenderStepMaxMs and UpdateEveryFrameBudgetMs.
				float baseMax = cfg.RenderStepMaxMs > 0 ? cfg.RenderStepMaxMs : cfg.UpdateEveryFrameBudgetMs;
				effectiveMaxMs = Mathf.Min(baseMax, cfg.UpdateEveryFrameBudgetMs);
			}
			// DECISION: soft gate active only when enabled and not disabled for this pass.
			bool softGateEnabledNow = softGateCfg.EnableQuickRayMiss && softGateCfg.ScoringEnabled && !_softGateDisabledForPass;
			// DECISION: clear pending band if its bounds are invalid.
			if (_pendingBandHasPass1 && (_pendingBandRowStart < 0 || _pendingBandRowCount <= 0))
			{
				_pendingBandRowStart = -1;
				_pendingBandRowCount = 0;
				_pendingBandHasPass1 = false;
			}
			pendingPass2 = _pendingBandHasPass1;
			bandH = 0;
			long bandSegsTested = 0;
			long bandPhysicsQueries = 0;
			int maxAttemptsAnyPixelThisBand = 0;
			int maxSubdividesAnyPixelThisBand = 0;

			void LogRenderPhase(string phase)
			{
				renderPhase = phase;
				GD.Print(
					$"[RenderStep] phase={phase} frame={_frameIndex} row={_rowCursor} " +
					$"attempts={_softGateAttemptsUsedThisFrame}/{pass2SoftGateMaxAttemptsPerFrameEffective} " +
					$"sub={_softGateSubdividedCallsUsedThisFrame}/{pass2SoftGateMaxSubdividedCallsPerFrameEffective} " +
					$"pxCap={softGateCfg.MaxAttemptsPerPixel} scoreCap={softGateCfg.ScoreBudgetPerFrame} " +
					$"ms={renderStepWatch.ElapsedMilliseconds}");
			}

			void LogRenderStopOnce(string reason)
			{
				// DECISION: emit a single definitive stop line for any budget/timeout stop.
				if (renderStepStopLogged) return;
				renderStepStopLogged = true;
				GD.PrintErr(
					$"[RenderStep][STOP] reason={reason} phase={renderPhase} y=[{yStart},{yEnd}) rowCursor={_rowCursor} " +
					$"elapsedMs={renderStepWatch.ElapsedMilliseconds} " +
					$"attempts={_softGateAttemptsUsedThisFrame}/{pass2SoftGateMaxAttemptsPerFrameEffective} " +
					$"sub={_softGateSubdividedCallsUsedThisFrame}/{pass2SoftGateMaxSubdividedCallsPerFrameEffective} " +
					$"hits={bandHits}");
			}

			void FinalizeBandAndAdvance(string reason, int bandStart, int bandEnd, int hitsInBand, string extraStats)
			{
				int rowCursorBefore = _rowCursor;
				int bandRows = Math.Max(0, bandEnd - bandStart);
				int advanceRows = Math.Max(1, bandRows);
				bool preservePendingPass2 = string.Equals(reason, "max_ms_after_pass1", StringComparison.Ordinal);
				int filmHLocal = _filmHeight;
				int nextRow = bandStart + advanceRows;
				if (filmHLocal > 0)
				{
					nextRow = Mathf.Clamp(nextRow, 0, filmHLocal);
					if (nextRow >= filmHLocal)
						nextRow = 0;
				}
				long attemptsUsed = _softGateAttemptsUsedThisFrame;
				long subdivUsed = _softGateSubdividedCallsUsedThisFrame;
				string extraSuffix = string.IsNullOrEmpty(extraStats) ? "" : $" {extraStats}";
				GD.Print(
					$"[RenderStep][Finalize] reason={reason} phase={renderPhase} y=[{bandStart},{bandEnd}) " +
					$"rowCursor={rowCursorBefore}->{nextRow} elapsedMs={renderStepWatch.ElapsedMilliseconds} " +
					$"attempts={attemptsUsed}/{pass2SoftGateMaxAttemptsPerFrameEffective} " +
					$"sub={subdivUsed}/{pass2SoftGateMaxSubdividedCallsPerFrameEffective} " +
					$"hits={hitsInBand}{extraSuffix}");
				
				if (cfg.RenderStepBandLog) LogBandSummaryOnce(MapBandSummaryReason(reason));

				_rowCursor = nextRow;
				bandCommittedThisStep = true;
				ResetNoHitStall();
				if (_pendingBandHasPass1 && !preservePendingPass2)
				{
					_pendingBandRowStart = -1;
					_pendingBandRowCount = 0;
					_pendingBandHasPass1 = false;
				}
				_bandIncompleteFrameId = -1;
				_bandIncompleteRowStart = -1;
				_bandIncompleteRowEnd = -1;
				// Reset per-band soft-gate counters to avoid carrying stalls forward.
				_softGateAttemptsUsedThisFrame = 0;
				_softGateSubdividedCallsUsedThisFrame = 0;
				_p2SoftGateUsedThisFrame = 0;
				maxAttemptsAnyPixelThisBand = 0;
				maxSubdividesAnyPixelThisBand = 0;
			}

			void MarkBandIncompleteThisFrame(string reason, int bandStart, int bandEnd)
			{
				_ = reason;
				int frameId = (int)Engine.GetFramesDrawn();
				_bandIncompleteFrameId = frameId;
				_bandIncompleteRowStart = bandStart;
				_bandIncompleteRowEnd = bandEnd;
				_suppressStuckBandRepeatOnce = true;
				_stuckBandRepeats = 0;
			}

			void ForceAdvanceRowCursorOnStop(string reason, int desiredEndRow)
			{
				// DECISION: always advance on stop so "no hits" or budget exits can't stall the same band.
				_ = reason;
				int filmHLocal = _filmHeight;
				int advanceTarget = desiredEndRow;
				if (advanceTarget <= yStart)
					advanceTarget = yStart + 1;
				advanceTarget = Mathf.Clamp(advanceTarget, 0, filmHLocal);
				if (filmHLocal > 0 && advanceTarget >= filmHLocal)
					advanceTarget = 0;
				_rowCursor = advanceTarget;
				bandCommittedThisStep = true;
				// DECISION: drop pending pass2 when stopping early to avoid re-entering the same band forever.
				if (_pendingBandHasPass1)
				{
					_pendingBandRowStart = -1;
					_pendingBandRowCount = 0;
					_pendingBandHasPass1 = false;
				}
			}

			void ApplyDeferredRowCursorResetIfNeeded(int bandStart, int bandEnd)
			{
				// DECISION: apply deferred reset only after the band advances to avoid restarting the same rows.
				if (!_pendingRowCursorReset || !bandCommittedThisStep) return;
				string reason = _pendingRowCursorResetReason;
				_pendingRowCursorReset = false;
				_pendingRowCursorResetReason = "";
				GD.Print($"[RenderStep][DeferReset] apply reason={reason} after band y=[{bandStart},{bandEnd})");
				ResetRowCursor(reason);
			}

			string GetMaxMsStopReason()
			{
				// DECISION: distinguish which time budget is active for stop logs.
				if (cfg.UpdateEveryFrame && cfg.UpdateEveryFrameBudgetMs > 0f && (cfg.RenderStepMaxMs <= 0 || cfg.UpdateEveryFrameBudgetMs <= cfg.RenderStepMaxMs))
					return "update_every_frame_budget";
				return "renderstep_max_ms";
			}

			void TriggerBudgetStop(string reason)
			{
				// DECISION: only budget-stop when UpdateEveryFrame is active.
				if (!cfg.UpdateEveryFrame) return;
				// DECISION: budget stop is one-shot.
				if (budgetStop) return;
				budgetStop = true;
				budgetStopReason = reason;
				if (cfg.Research.ResearchEnabled && cfg.Research.DeterministicMode && yEnd > yStart)
				{
					budgetStopRowEnd = yEnd;
				}
				else
				{
					budgetStopRowEnd = budgetStopRowCursor;
				}
				LogBudgetExitOnce(reason, budgetStopRowCursor);
				LogRenderStopOnce(reason);
			}

			void LogBudgetStopOnce()
			{
				// DECISION: log once per budget stop occurrence.
				if (!budgetStop || budgetStopLogged) return;
				budgetStopLogged = true;
				int frameId = (int)Engine.GetFramesDrawn();
				// DECISION: avoid duplicate logs in same frame.
				if (_budgetYieldLogFrameId == frameId) return;
				_budgetYieldLogFrameId = frameId;
				ulong nowUsec = Time.GetTicksUsec();
				double p1Ms = pass1EndUsec > pass1StartUsec
					? (pass1EndUsec - pass1StartUsec) / 1000.0
					: (pass1StartUsec > 0 ? (nowUsec - pass1StartUsec) / 1000.0 : 0.0);
				ulong pass2EndUsecNow = pass2EndUsec > pass2StartUsec ? pass2EndUsec : nowUsec;
				double p2Ms = pass2StartUsec > 0
					? (pass2EndUsecNow - pass2StartUsec) / 1000.0
					: 0.0;
				int rowEnd = Mathf.Clamp(budgetStopRowEnd, 0, _filmHeight);
				int rowsDone = Mathf.Max(0, rowEnd - budgetStopRowStart);
				GD.Print(
					$"[RenderStep][Yield] reason={budgetStopReason} frame={_frameIndex} rowCursor={rowEnd} rowsDone={rowsDone} " +
					$"pendingPass2={(pendingPass2 ? 1 : 0)} bandH={bandH} pass1RerunAvoided={(pass1SkippedThisStep ? 1 : 0)} " +
					$"ms={renderStepWatch.ElapsedMilliseconds} p1ms={p1Ms:0.00} p2ms={p2Ms:0.00} " +
					$"p2SegTestedStep={bandSegsTested} softGate{{attemptUsed={_softGateAttemptsUsedThisFrame} subdivUsed={_softGateSubdividedCallsUsedThisFrame}}}");
			}

			string MapBandSummaryReason(string reason)
			{
				if (string.IsNullOrEmpty(reason)) return "normal";
				if (reason == "zero_hit_advance" || reason == "zero-hit-advance") return "zero-hit-advance";
				if (reason.Contains("guard") || reason.Contains("watchdog")) return "guard";
				if (reason.Contains("budget") || reason.Contains("max_ms") || reason.Contains("target_ms")) return "budget";
				if (reason.StartsWith("max_") || reason.StartsWith("softgate_") || reason.Contains("max_segments") || reason.Contains("max_pixels")) return "cap";
				return "normal";
			}

			void LogBandSummaryOnce(string reasonDone)
			{
				if (bandSummaryLoggedThisBand) return;
				bandSummaryLoggedThisBand = true;
				double avgStepsPerTracedPixel = bandTracedPixels > 0
					? (double)pass1StepsIntegrated / bandTracedPixels
					: 0.0;
				GD.Print(
					$"[BandSummary] frame={_frameIndex} y=[{yStart},{yEnd}) " +
					$"hits={bandHits} tracedPx={bandTracedPixels} noCandPx={bandNoCandidatePixels} avgSteps={avgStepsPerTracedPixel:0.00} reasonDone={reasonDone}");
			}

			void ResetNoHitStall()
			{
				_bandNoHitStallRepeats = 0;
				_bandNoHitStallStartRow = -1;
				_bandNoHitStallEndRow = -1;
			}

			bool TrackNoHitStall()
			{
				if (processedPixelsThisBand > 0 || _rowCursor != bandStartRowCursor)
				{
					ResetNoHitStall();
					return false;
				}
				if (_bandNoHitStallStartRow == yStart && _bandNoHitStallEndRow == yEnd)
					_bandNoHitStallRepeats++;
				else
				{
					_bandNoHitStallStartRow = yStart;
					_bandNoHitStallEndRow = yEnd;
					_bandNoHitStallRepeats = 1;
				}
				return _bandNoHitStallRepeats > BandNoHitStallMaxRepeats;
			}

			bool ForceAdvanceOnNoHit(string reason, string reasonDone, bool forceNow)
			{
				bool shouldForce = forceNow || TrackNoHitStall();
				if (!shouldForce) return false;
				LogBudgetExitOnce(reason, _rowCursor);
				ForceAdvanceRowCursorOnStop("zero_hit_advance", yEnd);
				ResetNoHitStall();
				if (cfg.RenderStepBandLog) LogBandSummaryOnce(reasonDone);
				return true;
			}

			void LogYieldAbortReason(string reason, int endRow, bool forcedAdvance, int hitsInBand)
			{
				int frameId = (int)Engine.GetFramesDrawn();
				if (_renderStepYieldLogFrameId != frameId)
				{
					_renderStepYieldLogFrameId = frameId;
					_renderStepYieldLogsThisFrame = 0;
				}
				if (_renderStepYieldLogsThisFrame >= 2) return;
				_renderStepYieldLogsThisFrame++;
				GD.Print(
					$"[RenderStep][YieldAbort] reason={reason} startRow={startRow} endRow={endRow} " +
					$"forcedAdvance={(forcedAdvance ? 1 : 0)} elapsedMs={renderStepWatch.ElapsedMilliseconds} " +
					$"budgetStop={(budgetStop ? 1 : 0)} hitsInBand={hitsInBand}");
			}

			void LogForcedAdvanceWarning(string reason, int endRow)
			{
				int frameId = (int)Engine.GetFramesDrawn();
				if (_renderStepForceAdvanceWarnFrameId != frameId)
				{
					_renderStepForceAdvanceWarnFrameId = frameId;
					_renderStepForceAdvanceWarnsThisFrame = 0;
				}
				if (_renderStepForceAdvanceWarnsThisFrame >= 1) return;
				_renderStepForceAdvanceWarnsThisFrame++;
				GD.PrintErr(
					$"[RenderStep][WARN] progress-guard forced advance reason={reason} startRow={startRow} endRow={endRow} " +
					$"bandH={bandH} rowsPerFrame={rowsPerFrame} ms={renderStepWatch.ElapsedMilliseconds}");
			}

			int ComputeAdvanceRows()
			{
				int advance = bandH > 0 ? bandH : rowsPerFrame;
				if (advance <= 0) advance = Math.Max(1, filmCfg.RowsPerFrame);
				return Math.Max(1, advance);
			}

			void EnsureForwardProgress(string reason, bool fatal, int desiredEndRow, int hitsInBand, bool logAlways)
			{
				if (fatal)
				{
					if (logAlways)
						LogYieldAbortReason(reason, Mathf.Clamp(desiredEndRow, 0, _filmHeight), false, hitsInBand);
					return;
				}

				int filmHLocal = _filmHeight;
				int endRow = Mathf.Clamp(desiredEndRow, 0, filmHLocal);
				bool forced = false;
				if (startRow < filmHLocal)
				{
					if (endRow <= startRow)
					{
						endRow = Math.Min(filmHLocal, startRow + ComputeAdvanceRows());
						forced = true;
					}
				}
				else
				{
					endRow = filmHLocal;
				}

				_rowCursor = endRow;
				if (forced)
				{
					// DECISION: clear pending pass2 when forced to advance; prevents reprocessing the same band.
					if (_pendingBandHasPass1)
					{
						_pendingBandRowStart = -1;
						_pendingBandRowCount = 0;
						_pendingBandHasPass1 = false;
					}
					LogBudgetExitOnce("guard_progress", endRow);
					LogForcedAdvanceWarning(reason, endRow);
					if (cfg.RenderStepBandLog) LogBandSummaryOnce("guard");
				}

				if (logAlways || forced)
					LogYieldAbortReason(reason, endRow, forced, hitsInBand);
			}

			bool CheckRenderStepWatchdog()
			{
				// DECISION: watchdog disabled when effectiveMaxMs <= 0.
				if (effectiveMaxMs <= 0) return false;
				// DECISION: continue when still under budget.
				if (renderStepWatch.ElapsedMilliseconds <= effectiveMaxMs) return false;
				// DECISION: if UpdateEveryFrame, yield instead of abort.
				if (cfg.UpdateEveryFrame)
				{
					TriggerBudgetStop(GetMaxMsStopReason());
					return true;
				}
				// DECISION: first time over budget, mark abort and possibly disable soft gate.
				if (!renderStepAbort)
				{
					renderStepAbort = true;
					// DECISION: optionally disable SoftGate on overload to reduce work.
					if (softGateCfg.DisableOnOverload && softGateEnabledNow)
						DisableSoftGateThisFrame("renderstep_watchdog");
				}
				return true;
			}

			void AbortRenderStep(string reason)
			{
				// DECISION: abort is one-shot; skip if already logged.
				if (renderStepAbortLogged) return;
				renderStepAbortLogged = true;
				renderStepAbortReason = reason;
				if (reason == "watchdog")
					LogBudgetExitOnce("renderstep_max_ms", _rowCursor);
				// EFFECT: disable UpdateEveryFrame on abort.
				UpdateEveryFrame = false;
				cfg.UpdateEveryFrame = false;
				// DECISION: log soft gate disable reason once.
				if (softGateDisabledThisFrame && !softGateDisableLogged)
				{
					softGateDisableLogged = true;
					string sgReason = string.IsNullOrEmpty(softGateDisableReason) ? "overload" : softGateDisableReason;
					GD.PrintErr(
						$"[SoftGate][Disable] reason={sgReason} frame={_frameIndex} row={_rowCursor} " +
						$"attempts={_softGateAttemptsUsedThisFrame}/{pass2SoftGateMaxAttemptsPerFrameEffective} " +
						$"sub={_softGateSubdividedCallsUsedThisFrame}/{pass2SoftGateMaxSubdividedCallsPerFrameEffective} " +
						$"ms={renderStepWatch.ElapsedMilliseconds}");
				}
				// DECISION: budget aborts include watchdog or budget-based soft-gate disables.
				bool budgetAbort = reason == "watchdog"
					|| (softGateDisabledThisFrame && softGateDisableReason.StartsWith("budget", StringComparison.Ordinal));
				// DECISION: emit budget diagnostics only for budget-related aborts.
				if (budgetAbort)
				{
					long qRayCalls = softGateDebugEnabled ? _softGateFrame.QRayCalls : 0;
					long qRayHit = softGateDebugEnabled ? _softGateFrame.QRayHit : 0;
					long qRayMiss = softGateDebugEnabled ? _softGateFrame.QRayMiss : 0;
					int subCalls = statsEnabled ? _perfFrame.SubdividedRayCalls : 0;
					int subSteps = statsEnabled ? _perfFrame.SubdividedRaySubsteps : 0;
					GD.PrintErr(
						$"[RenderStep][Budget] reason={reason} frame={_frameIndex} row={_rowCursor} bandH={bandH} stride={stride} " +
						$"elapsedMs={renderStepWatch.ElapsedMilliseconds} maxMs={effectiveMaxMs:0.###} " +
						$"attempts={_softGateAttemptsUsedThisFrame}/{pass2SoftGateMaxAttemptsPerFrameEffective} " +
						$"sub={_softGateSubdividedCallsUsedThisFrame}/{pass2SoftGateMaxSubdividedCallsPerFrameEffective} " +
						$"maxPxAttempts={maxAttemptsAnyPixelThisBand} maxPxSub={maxSubdividesAnyPixelThisBand} " +
						$"tracedPx={bandTracedPixels} segsTested={bandSegsTested} qRay={qRayCalls}/{qRayHit}/{qRayMiss} " +
						$"physQ={bandPhysicsQueries} subCalls={subCalls} subSteps={subSteps}");
				}
				GD.PrintErr(
					$"[RenderStep][Abort] reason={reason} frame={_frameIndex} row={_rowCursor} " +
					$"ms={renderStepWatch.ElapsedMilliseconds}");
			}

			void DisableSoftGateThisFrame(string reason)
			{
				// DECISION: only disable once per frame.
				if (softGateDisabledThisFrame) return;
				softGateDisabledThisFrame = true;
				softGateDisableReason = reason;
			}

			// DECISION: initialize per-frame counters at frame start.
			if (frameStart)
			{
				_frameIndex++;
				// DECISION: reset perf frame only when stats enabled.
				if (statsEnabled)
				{
					_perfFrame.Reset();
					_perfFrame.RequireHitToRender = rayCfg.RequireHitToRender;
					_perfFrame.EffectiveStride = stride;
					_perfFrame.EffectiveWidth = filmW;
					_perfFrame.EffectiveHeight = filmH;
					_perfFrame.EffectiveRenderPixels = (int)tracedPixels;
				}else{}
				// DECISION: reset frame perf only when enabled.
				if (framePerfEnabled)
				{
					_framePerf.Reset();
					_framePerf.FrameIndex = _frameIndex;
				}else{}
				
				// Soft-gate frame counters
				/////////////////////////////
				// DECISION: reset soft-gate frame counters when debug is enabled.
				if (softGateDebugEnabled)
				{
					long effPx = tracedPixels;
					ResetSoftGateCounters(
						ref _softGateFrame,
						_frameIndex,
						effPx,
						softGateCfg.EnableQuickRayMiss,
						softGateCfg.ScoringEnabled,
						pass2SoftGateMinSegmentLengthEffective,
						softGateCfg.ScoreThreshold,
						softGateCfg.ScoreTurnAngleWeight,
						softGateCfg.ScorePrevHitLostBonus,
						softGateCfg.RandomProbeChance,
						softGateCfg.ScoreBudgetPerFrame,
						softGateCfg.MaxAttemptsPerPixel,
						pass2SoftGateMaxAttemptsPerFrameEffective,
						pass2SoftGateMaxSubdividedCallsPerFrameEffective);
					_softGateSampleCounter = 0;
				}else{}
				_softGateAttemptsUsedThisFrame = 0;
				_softGateSubdividedCallsUsedThisFrame = 0;
				_softGateWatchdogLogsRemaining = Mathf.Max(0, softGateCfg.WatchdogLogLimitPerFrame);
				_softGateSummaryLogsRemaining = Mathf.Max(0, softGateCfg.DebugSummaryLogLimitPerFrame);
				_broadphaseHybridFallbackLogsRemaining = BroadphaseHybridFallbackLogLimitPerFrame;
				_broadphaseHybridFallbackHitLogsRemaining = BroadphaseHybridFallbackHitLogLimitPerFrame;
				_broadphaseHybridGateLogsRemaining = BroadphaseHybridGateLogLimitPerFrame;
				_broadphaseNoCandidateLogsRemaining = BroadphaseNoCandidateLogLimitPerFrame;
				_geomEligibleAccountingLogsRemaining = GeomEligibleAccountingLogLimitPerFrame;
				_quickRayZeroCountThisFrame = 0;
				_hybridFallbackCountThisFrame = 0;
				_hybridFallbackHitCountThisFrame = 0;
				_hybridFallbackMissCountThisFrame = 0;
				_hybridNoCandidateCountThisFrame = 0;
				/////////////////////////////
			}

			if (cfg.RenderStepPhaseLog)	LogRenderPhase("enter");

			// DECISION: mark film resize in perf stats when enabled.
			if (statsEnabled && resizedFilm)
			{
				_perfFrame.ResizedFilm = true;
			}

			// DECISION: abort if RayBeamRenderer is missing.
			if (_rbr == null)
			{
				AbortRenderStep("No RayBeamRenderer assigned");
				EnsureForwardProgress("no_rbr", true, _rowCursor, bandHits, true);
				return;
			} else{}

			// DECISION: abort if camera is missing.
			if (_cam == null) {
				AbortRenderStep("No active Camera3D in viewport");
				EnsureForwardProgress("no_camera", true, _rowCursor, bandHits, true);
				return;
			} else{}

			// DECISION: validate cached RayBeamRenderer camera on main thread before worker pass.
			Camera3D rbrCam = _rbr.GetCamera();
			if (rbrCam == null)
			{
				if (!_renderStepMissingRbrCameraWarned)
				{
					_renderStepMissingRbrCameraWarned = true;
					GD.PushError("[RenderStep] RayBeamRenderer cached camera is null. Ensure RayBeamRenderer.CameraPath resolves in _Ready.");
				}
				AbortRenderStep("RayBeamRenderer camera cache is null");
				EnsureForwardProgress("no_rbr_camera", true, _rowCursor, bandHits, true);
				return;
			}
			_renderStepMissingRbrCameraWarned = false;

			if (_rowCursor == 0 && !beganBoundaryValidationRun)
			{
				_rbr.BeginBoundaryValidationRun();
				beganBoundaryValidationRun = true;
			}

			// DECISION: log toggle snapshots only at frame start.
			if (frameStart)
			{
				MaybePrintToggleSnapshot(in cfg, in rayCfg);
				MaybePrintSoftGateConfigSnapshot(in cfg);
			}

			if (statsEnabled) schedulerStartUsec = Time.GetTicksUsec();
			if (framePerfEnabled)
			{
				schedulerScope = new PerfScope(_framePerf, PerfStage.SchedulerOrchestration);
				schedulerScopeActive = true;
			}

			bool pass1ProbeRequestedForStep = cfg.Pass1DoHitTest;
			if (!TryResolveRenderSpaceState(out PhysicsDirectSpaceState3D space, out string renderSpaceSource))
			{
				LogRenderSpaceUnavailableOnce(renderSpaceSource, pass1ProbeRequestedForStep, "render_step_space_unavailable");
				AbortRenderStep("Physics space unavailable for render step");
				return;
			}
			_renderSpaceUnavailableWarned = false;
			string renderSceneName = ResolveHudSceneName();
			string renderFixtureName = ResolveHudFixtureName();
			string renderModeToken = ResolveHudModePath();
			var snap = FrameSnapshotBus.CurrentSnapshot;
			var geomTlasForStep = snap?.GeometryTLAS;
			var geomEntitiesForStep = snap?.Geometry;
			geomPruneRequestedForStep = cfg.UseGeometryTLASPruning;
			bool effectivePruneActive = geomPruneRequestedForStep
				&& IsGeometryTLASUsable(geomTlasForStep, geomEntitiesForStep);
			useGeomTlasPruningForStep = effectivePruneActive;
			bool geomPruneSwitchingThisStep = _hasRenderHealthGeomPruneMode
				&& _lastRenderHealthGeomPruneMode != useGeomTlasPruningForStep;
			int modeWindowSamplesForStep = CountRenderHealthModeSamplesInWindow(useGeomTlasPruningForStep) + 1;
			renderHealthPass2SampleEveryForStep = GetRenderHealthPass2SampleEveryForStep(useGeomTlasPruningForStep);
			geomHealthPartialForStep = _geomPruneSwitchedThisWindow == 1
				|| geomPruneSwitchingThisStep
				|| modeWindowSamplesForStep < RenderHealthMinModeSamplesForTrust;
			int geomCountForScratch = geomEntitiesForStep?.Count ?? 0;
			EnsureGeomScratchCapacity(Math.Max(256, geomCountForScratch));

			var fieldSnaps = GetFieldSourceSnaps(in cfg, _frameIndex, out bool hasSources, out bool cacheRefreshed);
			// DECISION: track cache hits/misses for field sources when caching is enabled.
			if (framePerfEnabled && frameStart && cfg.UseFieldSourceCache)
			{
				// DECISION: count cache misses vs hits.
				if (cacheRefreshed) _framePerf.CacheMisses++;
				else _framePerf.CacheHits++;
			}

			// DECISION: throttle verbose field source logs to once per frame.
			if (cfg.VerbosePerfLogs && (_rowCursor % filmH) == 0)
				GD.Print($"fieldSnaps={fieldSnaps.Length} hasSources={hasSources}");
			if (cfg.FixtureDebugHitColoringEnabled || cfg.FixtureDebugTraceEnabled || _renderHealthTestTrustEnforcementEnabled)
				RefreshFixtureDebugSourceIds(cfg.FixtureDebugSourceGroup);


			float beta = 0f;
			float gamma = 2f;
			// DECISION: optionally pull Beta/Gamma from active camera.
			if (cfg.UseCameraPropsBetaGamma)
			{
				beta = ReadFloat(_cam, "Beta", 0f);
				gamma = ReadFloat(_cam, "Gamma", 2f);
			}
			MaybeWarnBroadphaseQuickRayCurved(beta, gamma, effQuickRay, cfg.UseCameraPropsBetaGamma);
			if (framePerfEnabled)
				_framePerf.PowFastPath = (gamma == -2f || gamma == -1f || gamma == 0f || gamma == 1f || gamma == 2f);

			// CROSS-CLASS CONTRACT: RayBeamRenderer decides field center policy.
			Vector3 center = rayCfg.FieldCenterIsCamera ? _cam.GlobalPosition : rayCfg.FieldCenter;
			var basis = _cam.GlobalTransform.Basis;

			float fovRad = Mathf.DegToRad(_cam.Fov);
			float tanHalf = Mathf.Tan(fovRad * 0.5f);
			float aspect = (float)filmW / Mathf.Max(1f, filmH);

			int maxSeg = rayCfg.MaxSegPerRay;
			yStart = _rowCursor;
			int baseRowsPerFrame = Mathf.Clamp(filmCfg.RowsPerFrame, Mathf.Max(1, cfg.MinRowsPerFrame), filmH);
			int maxRowsPerFrame = Mathf.Clamp(cfg.MaxRowsPerFrameCap, Mathf.Max(1, cfg.MinRowsPerFrame), filmH);
			// DECISION: disable adaptive rows when target ms <= 0 or no prior adaptive state.
			if (cfg.TargetMsPerFrame <= 0 || _adaptiveRowsPerFrame <= 0)
				_adaptiveRowsPerFrame = baseRowsPerFrame;
			rowsPerFrame = Mathf.Clamp(_adaptiveRowsPerFrame, Mathf.Max(1, cfg.MinRowsPerFrame), maxRowsPerFrame);
			// DECISION: keep adaptive state in sync.
			if (rowsPerFrame != _adaptiveRowsPerFrame)
				_adaptiveRowsPerFrame = rowsPerFrame;
			// DECISION: tighten row caps when UpdateEveryFrame is active.
			if (cfg.UpdateEveryFrame)
			{
				int updateEveryFrameMaxRows = Math.Max(1, cfg.UpdateEveryFrameMaxRowsPerStep);
				maxRowsPerFrame = Math.Min(maxRowsPerFrame, updateEveryFrameMaxRows);
				// DECISION: apply pixel/segment caps to row budget when configured.
				int maxRowsByPixel = cfg.RenderStepMaxPixelsPerFrame > 0
					? Math.Max(1, cfg.RenderStepMaxPixelsPerFrame / Math.Max(1, filmW))
					: int.MaxValue;
				int maxRowsBySeg = cfg.RenderStepMaxSegmentsPerFrame > 0
					? Math.Max(1, cfg.RenderStepMaxSegmentsPerFrame / Math.Max(1, filmW * maxSeg))
					: int.MaxValue;
				int cappedRows = Math.Min(rowsPerFrame, Math.Min(maxRowsByPixel, maxRowsBySeg));
				int minRows = Mathf.Max(1, cfg.MinRowsPerFrame);
				int maxRows = maxRowsPerFrame;
				if (maxRows < minRows)
				{
					if (!_rowsRangeWarningIssued)
					{
						GD.PushWarning($"[RenderStep] Invalid rows-per-frame range: minRows={minRows} > maxRows={maxRows}. Forcing maxRows=minRows.");
						_rowsRangeWarningIssued = true;
					}
					maxRows = minRows;
				}
				rowsPerFrame = Mathf.Clamp(cappedRows, minRows, maxRows);
				// DECISION: keep adaptive state in sync.
				if (rowsPerFrame != _adaptiveRowsPerFrame)
					_adaptiveRowsPerFrame = rowsPerFrame;
			}
			// NOTE: yEnd is tracked for forward-progress guard/logs.
			// DECISION: if pass2 is pending, re-use the cached band.
			if (pendingPass2)
			{
				yStart = _pendingBandRowStart;
				yEnd = Mathf.Min(filmH, yStart + _pendingBandRowCount);
				bandH = yEnd - yStart;
				rowsPerFrame = Math.Max(1, bandH);
				pass1SkippedThisStep = true;
			}
			else
			{
				yEnd = Mathf.Min(filmH, _rowCursor + rowsPerFrame);
				bandH = yEnd - yStart;
			}
			_bandHeightRowsResolved = Math.Max(1, rowsPerFrame);
			budgetStopRowStart = yStart;
			budgetStopRowCursor = yStart;
			budgetStopRowEnd = yStart;
			int renderFrameId = (int)Engine.GetFramesDrawn();
			if (_bandIncompleteFrameId != renderFrameId)
			{
				_bandIncompleteFrameId = -1;
				_bandIncompleteRowStart = -1;
				_bandIncompleteRowEnd = -1;
			}
			if (_bandIncompleteFrameId == renderFrameId
				&& _rowCursor == _bandIncompleteRowStart
				&& yStart == _bandIncompleteRowStart
				&& yEnd == _bandIncompleteRowEnd)
			{
				// DECISION: avoid re-entering an incomplete band within the same frame.
				_suppressStuckBandRepeatOnce = true;
				LogBudgetExitOnce("guard_incomplete_band", _rowCursor);
				if (cfg.RenderStepBandLog) LogBandSummaryOnce("guard");
				return;
			}

			// DECISION: detect repeated starts on the same band without a prior commit and force advance.
			if (bandH > 0)
			{
				bool noRowProgressSinceLast = _lastRenderStepRowCursor >= 0 && _rowCursor == _lastRenderStepRowCursor;
				bool noPixelProgressSinceLast = !_hasLastProcessedPixelsThisBand || _lastProcessedPixelsThisBand == 0;
				bool noProgressSinceLast = noRowProgressSinceLast && noPixelProgressSinceLast;
				bool sameBandAsLast = _lastRenderStepBandStart == yStart
					&& _lastRenderStepBandEnd == yEnd
					&& _lastRenderStepRowCursor == _rowCursor;
				bool countRepeat = sameBandAsLast && noProgressSinceLast && !_suppressStuckBandRepeatOnce && !_lastBandCommitted;
				if (countRepeat)
					_stuckBandRepeats++;
				else
					_stuckBandRepeats = 0;
				_suppressStuckBandRepeatOnce = false;
				_stuckBandStartRow = yStart;
				_stuckBandEndRow = yEnd;
				if (_stuckBandRepeats > StuckBandWatchdogMaxRepeats)
				{
					GD.PrintErr($"[RenderStep][WATCHDOG] stuckBand y=[{yStart},{yEnd}) repeats={_stuckBandRepeats} -> forceAdvance");
					LogBudgetExitOnce("guard_stuck_band", _rowCursor);
					ForceAdvanceRowCursorOnStop("watchdog_stuck_band", yEnd);
					if (cfg.RenderStepBandLog) LogBandSummaryOnce("guard");
					ResetNoHitStall();
					ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);
					return;
				}
			}

			int pixelCount = bandH * filmW;
			// DECISION: enforce max pixels per frame when configured.
			if (cfg.RenderStepMaxPixelsPerFrame > 0 && pixelCount > cfg.RenderStepMaxPixelsPerFrame)
			{
				// DECISION: yield when UpdateEveryFrame; abort otherwise.
				if (cfg.UpdateEveryFrame)
					TriggerBudgetStop("max_pixels");
				else
				{
					AbortRenderStep($"max-pixels {pixelCount}>{cfg.RenderStepMaxPixelsPerFrame}");
					LogBudgetExitOnce("max_pixels", _rowCursor);
					LogRenderStopOnce("max_pixels");
					FinalizeBandAndAdvance("max_pixels", yStart, yEnd, bandHits, $"px={pixelCount}");
					ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);
					return;
				}
			}
			// DECISION: if budget stop triggered, log and bail.
			if (budgetStop)
			{
				LogBudgetStopOnce();
				if (budgetStopReason == "softgate_attempt_cap")
				{
					FinalizeBandAndAdvance("softgate_attempt_cap", yStart, yEnd, bandHits, "");
				}
				else
				{
					FinalizeBandAndAdvance(budgetStopReason, yStart, yEnd, bandHits, "");
				}
				ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);
				return;
			}

			EnsureDepthHistory(cfg.DepthHistoryFrames);
			float frameMaxHit = 0f; // track deepest hit this RenderStep band

			bandHits = 0;
			// DECISION: choose far distance based on auto-range.
			float farForSim = cfg.AutoRangeDepth ? _rangeFar : cfg.Film.MaxDistance;

			// Soft-gate band counters
			/////////////////////////////
			// DECISION: reset soft-gate band counters when enabled.
			if (softGateBandEnabled)
			{
				ResetSoftGateCounters(
					ref _softGateBand,
					_frameIndex,
					0,
					softGateCfg.EnableQuickRayMiss,
					softGateCfg.ScoringEnabled,
					pass2SoftGateMinSegmentLengthEffective,
					softGateCfg.ScoreThreshold,
					softGateCfg.ScoreTurnAngleWeight,
					softGateCfg.ScorePrevHitLostBonus,
					softGateCfg.RandomProbeChance,
					softGateCfg.ScoreBudgetPerFrame,
					softGateCfg.MaxAttemptsPerPixel,
					pass2SoftGateMaxAttemptsPerFrameEffective,
					pass2SoftGateMaxSubdividedCallsPerFrameEffective);
			}else{}
			/////////////////////////////

			FieldGrid3D fieldGridForPass1 = null;
			CurvatureBoundGrid curvatureGridForPass1 = null;
			// DECISION: use field grid only when enabled, integrated field is on, and sources exist.
			if (cfg.UseFieldGrid && rayCfg.UseIntegratedField && hasSources)
			{
				int rebuildN = Mathf.Max(1, cfg.FieldGridRebuildEveryNFrames);
				bool shouldRebuild = cacheRefreshed || _fieldGrid == null || (_frameIndex % rebuildN) == 0;
				// DECISION: rebuild grid on schedule or when missing.
				if (shouldRebuild)
				{
					float cellSize = Mathf.Max(0.001f, cfg.FieldGridCellSize);
					float radius = Mathf.Max(0.01f, farForSim + cfg.FieldGridBoundsPadding);
					Vector3 half = new Vector3(radius, radius, radius);
					Vector3 origin = _cam.GlobalPosition - half;
					Aabb bounds = new Aabb(origin, half * 2f);
					_fieldGrid ??= new FieldGrid3D();
					_fieldGrid.BuildFromSources(fieldSnaps, beta, gamma, rayCfg.BendScale, rayCfg.FieldStrength, bounds, cellSize);
				}
				fieldGridForPass1 = _fieldGrid;
			}
			curvatureGridForPass1 = snap?.CurvatureGrid;
			bool skipBandPhysics = false;
			int bandIndex = 0;
			// DECISION: band-level skip when enabled and history supports it.
			if (cfg.UseBandHitSkip)
			{
				EnsureBandHitHistory(filmH, rowsPerFrame);
				bandIndex = yStart / rowsPerFrame;

				// DECISION: invalidate history when camera/range changed.
				if (CheckAndUpdateBandInvalidation(_cam.GlobalTransform, farForSim, cfg.BandSkipInvalidatePosDelta, cfg.BandSkipInvalidateBasisDelta, cfg.BandSkipInvalidateRangeDelta))
					ResetBandHitHistory();

				// DECISION: skip physics when hit rate is low for enough frames.
				if (bandIndex >= 0 && bandIndex < _bandHitRate.Length && cfg.BandSkipFrames > 0)
				{
					// DECISION: only skip when hit rate is below threshold for long enough.
					if (_bandLowHitFrames[bandIndex] >= cfg.BandSkipFrames && _bandHitRate[bandIndex] < cfg.BandSkipHitThreshold)
						skipBandPhysics = true;
				}
			}
			if (skipBandPhysics && cfg.FixtureDebugHitColoringEnabled && cfg.FixtureDebugColorAuthorityEnabled)
				skipBandPhysics = false;
			skipBandPhysicsForDiagnostics = skipBandPhysics;

			// allocate / reuse buffers
			int segTotal = pixelCount * maxSeg;
			// DECISION: enforce max segments per frame when configured.
			if (cfg.RenderStepMaxSegmentsPerFrame > 0 && segTotal > cfg.RenderStepMaxSegmentsPerFrame)
			{
				// DECISION: yield when UpdateEveryFrame; abort otherwise.
				if (cfg.UpdateEveryFrame)
					TriggerBudgetStop("max_segments");
				else
				{
					AbortRenderStep($"max-segs {segTotal}>{cfg.RenderStepMaxSegmentsPerFrame}");
					LogBudgetExitOnce("max_segments", _rowCursor);
					LogRenderStopOnce("max_segments");
					FinalizeBandAndAdvance("max_segments", yStart, yEnd, bandHits, $"segs={segTotal}");
					ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);
					return;
				}
			}
			// DECISION: if budget stop triggered, log and bail.
			if (budgetStop)
			{
				LogBudgetStopOnce();
				if (budgetStopReason == "softgate_attempt_cap")
				{
					FinalizeBandAndAdvance("softgate_attempt_cap", yStart, yEnd, bandHits, "");
				}
				else
				{
					FinalizeBandAndAdvance(budgetStopReason, yStart, yEnd, bandHits, "");
				}
				ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);
				return;
			}
			// EFFECT: allocate segment buffers for this band.
			_segBuf ??= new RayBeamRenderer.RaySeg[segTotal];
			// DECISION: grow segment buffer when capacity is insufficient.
			if (_segBuf.Length < segTotal) _segBuf = new RayBeamRenderer.RaySeg[segTotal];

			// EFFECT: allocate per-pixel segment count and hit buffers.
			_segCountPerPixel ??= new int[pixelCount];
			// DECISION: grow per-pixel segment counts buffer when needed.
			if (_segCountPerPixel.Length < pixelCount) _segCountPerPixel = new int[pixelCount];
			// DECISION: grow pass1 hit-found buffer when needed.
			if (_pass1HitFound.Length < pixelCount) _pass1HitFound = new bool[pixelCount];
			// DECISION: grow pass1 stopped-early buffer when needed.
			if (_pass1StoppedEarly.Length < pixelCount) _pass1StoppedEarly = new bool[pixelCount];
			// DECISION: grow pass1 hit index buffer when needed.
			if (_pass1HitSegIndex.Length < pixelCount) _pass1HitSegIndex = new int[pixelCount];
			// DECISION: grow pass1 hit distance buffer when needed.
			if (_pass1HitDist.Length < pixelCount) _pass1HitDist = new float[pixelCount];
			// DECISION: grow pass1 hit position buffer when needed.
			if (_pass1HitPos.Length < pixelCount) _pass1HitPos = new Vector3[pixelCount];
			// DECISION: grow pass1 hit normal buffer when needed.
			if (_pass1HitNormal.Length < pixelCount) _pass1HitNormal = new Vector3[pixelCount];
			// DECISION: grow pass1 hit collider id buffer when needed.
			if (_pass1HitColliderId.Length < pixelCount) _pass1HitColliderId = new ulong[pixelCount];

			///  Debug code block drop
			_dbgRayCount = 0;
			_dbgPtWrite = 0;
			// DECISION: only build debug overlay if enabled.
			bool wantDbg = (rayCfg.HasRenderer
				&& rayCfg.DebugMode != RayBeamRenderer.DebugDrawMode.Off
				&& rayCfg.DebugOverlayOwnedByFilm);
			// Rough upper bounds for this band (for capacity planning)
			// We’ll only sample 1 out of DebugEveryNPixels pixels.
			// DECISION: allocate debug buffers only when needed.
			if (wantDbg)
			{
				int pxStride = Math.Max(1, cfg.DebugEveryNPixels);
				int sampledW = (filmW + pxStride - 1) / pxStride;
				int sampledH = (bandH + pxStride - 1) / pxStride;
				int sampledPixels = sampledW * sampledH;
				sampledPixels = Math.Min(sampledPixels, cfg.DebugMaxFilmRays);

				// Each sampled pixel stores up to segCount+1 points; we’ll cap segments too
				int maxPtsPerRay = maxSeg + 1;
				EnsureFilmDebugCapacity(sampledPixels, sampledPixels * maxPtsPerRay);
			}

			// snapshot plane filter state (value types -> thread friendly)
			Plane insightPlane = default;
			bool useInsightPlane = false;
			float insightEps = rayCfg.CollisionRadius;

			// DECISION: legacy pass2 insight plane toggle (currently unused).
			if (cfg.UseInsightPlanePass2 && rayCfg.UseInsightPlaneFilter)
			{
				// easiest v0: rebuild plane here from a NodePath you expose, OR if _rbr has the plane cached, add a getter.
				// For now (if you don't have a getter), just leave this false until we wire it.
				// useInsightPlane = true; insightPlane = ...;
			}

			// DECISION: film pass currently disables insight plane unless wired.
			if (rayCfg.UseInsightPlaneFilter)
			{
				// RayBeamRenderer already computed plane in rebuild, but for film we can just disable
				// OR if you want it: add a public getter in RayBeamRenderer for current plane/flag.
				// For now: keep it off in film threading unless you wire it.
				useInsightPlane = false;
			}

			// ---- PASS 1 (workers): build segments for each pixel ----
			//int jobs = Mathf.Clamp(OS.GetProcessorCount(), 2, 16);
			// CONTROL FACTOR: worker count for Parallel.For; lower reduces contention.
			int jobs = Mathf.Clamp(OS.GetProcessorCount() / 2, 2, 8);
			EmitThreadReadinessAuditOnce(in cfg, rowsPerFrame, bandH, filmW, filmH, jobs);

			var basisLocal = basis; // capture for lambda
			Camera3D pass1Cam = rbrCam;
			Vector3 pass1CamPos = pass1Cam != null ? pass1Cam.GlobalPosition : Vector3.Zero;
			float pass1PxPerRad = 0f;
			if (rayCfg.UseScreenSpaceCollisionCadence && pass1Cam != null)
			{
				float pass1FovY = Mathf.DegToRad(pass1Cam.Fov);
				var pass1Vp = pass1Cam.GetViewport();
				float pass1VpHeight = pass1Vp != null ? pass1Vp.GetVisibleRect().Size.Y : 720f;
				pass1VpHeight = Mathf.Max(1f, pass1VpHeight);
				float pass1FocalPx = (pass1VpHeight * 0.5f) / Mathf.Max(1e-6f, Mathf.Tan(pass1FovY * 0.5f));
				pass1PxPerRad = pass1FocalPx;
			}

			if (framePerfEnabled && schedulerScopeActive)
			{
				schedulerScope.Dispose();
				schedulerScopeActive = false;
			}
			if (statsEnabled && schedulerStartUsec > 0)
				_perfFrame.AddSchedulerUsec(Time.GetTicksUsec() - schedulerStartUsec);

			ulong a0 = Time.GetTicksUsec(); // before Parallel.For
			ulong a1 = a0;
			// CROSS-CLASS CONTRACT: pass1StopOnHit inherits ray stopping rules from RayBeamRenderer.
			bool pass1StopOnHit = rayCfg.StopOnHit || rayCfg.TerminateTrailOnHit || rayCfg.RequireHitToRender;
			long pass1PhysQueries = 0;
			long pass1EarlyStopPixels = 0;
			pass1StepsIntegrated = 0;
			long pass1FieldEvals = 0;
			long pass1Raycasts = 0;
			long pass1ProbeHits = 0;
			long pass1FieldGridHits = 0;
			long pass1FieldGridMisses = 0;
			long pass1FieldGridFallbacks = 0;
			long pass1FieldSourceEvals = 0;
			// DECISION: skip pass1 when we are resuming a pending pass2 band.
			if (!pendingPass2)
			{
				pass1StartUsec = a0;
				
				if (cfg.RenderStepPhaseLog)	LogRenderPhase("pass1-start");

				PerfScope pass1Scope = default;
				// DECISION: enable pass1 perf scope when frame perf is enabled.
				if (framePerfEnabled) pass1Scope = new PerfScope(_framePerf, PerfStage.Pass1_Integrate);

				bool collectPass1Perf = framePerfEnabled;
				bool collectPass1Steps = framePerfEnabled || cfg.VerbosePerfLogs;
				bool pass1SpaceAvailable = IsUsablePhysicsSpaceState(space);
				int pass1ProbeGuardLogState = 0;

				Pass1ThreadLocal CreatePass1ThreadLocal()
				{
					return new Pass1ThreadLocal
					{
						QuickRayParams = new PhysicsRayQueryParameters3D
						{
							CollisionMask = rayCfg.CollisionMask,
							CollideWithBodies = true,
							CollideWithAreas = true,
							HitFromInside = cfg.Pass2HitFromInside,
							HitBackFaces = cfg.Pass2HitBackFaces
						}
					};
				}

				Pass1ThreadLocal ProcessPass1Pixel(int pi, Pass1ThreadLocal local)
				{
					int localY = pi / filmW;   // 0..bandH-1
					int x = pi - localY * filmW;
					int y = yStart + localY;
					// DECISION: skip pixels not aligned to stride (block fill later).
					if ((x % stride) != 0 || (y % stride) != 0)
					{
						_segCountPerPixel[pi] = 0;
						_pass1HitFound[pi] = false;
						_pass1StoppedEarly[pi] = false;
						_pass1HitSegIndex[pi] = -1;
						_pass1HitDist[pi] = float.PositiveInfinity;
						_pass1HitPos[pi] = Vector3.Zero;
						_pass1HitNormal[pi] = Vector3.Up;
						_pass1HitColliderId[pi] = 0;
						return local;
					}

					float v = ((y + 0.5f) / filmH) * 2f - 1f;
					v = -v;
					float u = ((x + 0.5f) / filmW) * 2f - 1f;

					Vector3 dirCam = new Vector3(
						u * tanHalf * aspect,
						v * tanHalf,
						-1f
					).Normalized();

					// EFFECT: transform camera ray to world space.
					Vector3 dirWorld = (basisLocal * dirCam).Normalized();
					Vector3 bendDir = basisLocal.X;

					int segOffset = pi * maxSeg;
					if (local.QuickRayParams == null)
					{
						local.QuickRayParams = new PhysicsRayQueryParameters3D
						{
							CollisionMask = rayCfg.CollisionMask,
							CollideWithBodies = true,
							CollideWithAreas = true,
							HitFromInside = cfg.Pass2HitFromInside,
							HitBackFaces = cfg.Pass2HitBackFaces
						};
					}
					bool quickRayParamsValid = local.QuickRayParams != null;
					bool pass1ProbeEnabledForRay = cfg.Pass1DoHitTest && pass1SpaceAvailable && quickRayParamsValid;
					if (cfg.Pass1DoHitTest && !pass1ProbeEnabledForRay && Interlocked.Exchange(ref pass1ProbeGuardLogState, 1) == 0)
					{
						GD.PushWarning(
							$"[Pass1Probe][Guard] scene={renderSceneName} fixture={renderFixtureName} mode={renderModeToken} " +
							$"spaceNull={(pass1SpaceAvailable ? 0 : 1)} quickRayValid={(quickRayParamsValid ? 1 : 0)} " +
							$"pass1ProbeRequested=1 pass1ProbeEnabled=0 source={renderSpaceSource}");
					}

					// CROSS-CLASS CONTRACT: RayBeamRenderer builds segments + pass1 hit info.
						int count = _rbr.BuildRaySegmentsCamera_Pass1(
						space,
						ref local.QuickRayParams,
						pass1Cam, pass1PxPerRad, pass1CamPos,
						pass1CamPos, dirWorld, bendDir,
						center, beta, gamma,
						fieldSnaps, hasSources,
						farForSim,
						_segBuf, segOffset, maxSeg,
						insightPlane, useInsightPlane, insightEps,
						pass1StopOnHit,
						pass1ProbeEnabledForRay,
						cfg.Pass1ProbeEveryNSegments,
						cfg.Pass1ProbeMinTravelDelta,
						renderSpaceSource,
						renderSceneName,
						renderFixtureName,
						renderModeToken,
						out RayBeamRenderer.Pass1HitInfo hitInfo,
						out bool stoppedEarly,
						out int hitSegIndex,
						out int stepsIntegrated,
						out int fieldEvals,
						out int pass1RaycastsLocal,
						out int pass1ProbeHitsLocal,
							out int fieldGridHitsLocal,
							out int fieldGridMissesLocal,
							out int fieldGridFallbacksLocal,
							out int fieldSourceEvalsLocal,
							out float telemetryCurvatureMax,
							out float telemetryCurvatureMean,
							out float telemetryDkMax,
							out float telemetryD2kMax,
							curvatureGridForPass1,
							fieldGridForPass1
						);

					// DECISION: accumulate perf counters only when enabled.
					if (collectPass1Perf)
					{
						local.PhysQueries += pass1RaycastsLocal;
						// DECISION: count early-stop pixels only when stopped early.
						if (stoppedEarly) local.EarlyStopPixels++;
					}
					// DECISION: accumulate steps when enabled.
						if (collectPass1Steps) local.StepsIntegrated += stepsIntegrated;
						if (TelemetryHeatmapsEnabledForCurrentRun())
						{
							if (stepsIntegrated > 0)
								AccumulateTelemetryBlock(_telemetryPass1AcceptedSteps, x, y, stride, stepsIntegrated);
							if (telemetryCurvatureMax > 0f)
								AccumulateTelemetryBlock(_telemetryCurvatureMax, x, y, stride, telemetryCurvatureMax);
							if (telemetryCurvatureMean > 0f)
								AccumulateTelemetryBlock(_telemetryCurvatureMean, x, y, stride, telemetryCurvatureMean);
							if (telemetryDkMax > 0f)
								AccumulateTelemetryBlock(_telemetryDkMax, x, y, stride, telemetryDkMax);
							if (telemetryD2kMax > 0f)
								AccumulateTelemetryBlock(_telemetryD2kMax, x, y, stride, telemetryD2kMax);
						}
					// DECISION: accumulate field evals when frame perf is enabled.
					if (framePerfEnabled) local.FieldEvals += fieldEvals;
					// DECISION: accumulate extra pass1 counters when enabled.
					if (framePerfEnabled)
					{
						local.Pass1Raycasts += pass1RaycastsLocal;
						local.Pass1ProbeHits += pass1ProbeHitsLocal;
						local.FieldGridHits += fieldGridHitsLocal;
						local.FieldGridMisses += fieldGridMissesLocal;
						local.FieldGridFallbacks += fieldGridFallbacksLocal;
						local.FieldSourceEvals += fieldSourceEvalsLocal;
					}

					_segCountPerPixel[pi] = count;
					_pass1HitFound[pi] = hitInfo.Found;
					_pass1StoppedEarly[pi] = stoppedEarly;
					_pass1HitSegIndex[pi] = hitSegIndex;
					_pass1HitDist[pi] = hitInfo.Distance;
					_pass1HitPos[pi] = hitInfo.Position;
					_pass1HitNormal[pi] = hitInfo.Normal;
					_pass1HitColliderId[pi] = hitInfo.ColliderId;
					return local;
				}

				void MergePass1ThreadLocal(in Pass1ThreadLocal local)
				{
					if (local == null)
						return;
					if (collectPass1Perf)
					{
						pass1PhysQueries += local.PhysQueries;
						pass1EarlyStopPixels += local.EarlyStopPixels;
					}
					if (collectPass1Steps) pass1StepsIntegrated += local.StepsIntegrated;
					if (framePerfEnabled) pass1FieldEvals += local.FieldEvals;
					if (framePerfEnabled)
					{
						pass1Raycasts += local.Pass1Raycasts;
						pass1ProbeHits += local.Pass1ProbeHits;
						pass1FieldGridHits += local.FieldGridHits;
						pass1FieldGridMisses += local.FieldGridMisses;
						pass1FieldGridFallbacks += local.FieldGridFallbacks;
						pass1FieldSourceEvals += local.FieldSourceEvals;
					}
				}

				bool useThreadedPass1Bands = cfg.UseThreadedBands;
				int threadedBandWorkerCount = Math.Max(1, cfg.ThreadedBandWorkerCount);
				int threadedBandRowsPerChunk = Math.Max(1, cfg.ThreadedBandRowsPerChunk);
				if (cfg.Research.DeterministicMode && useThreadedPass1Bands && threadedBandWorkerCount > 1 && !_threadedBandsDeterminismWarned)
				{
					_threadedBandsDeterminismWarned = true;
					GD.PushWarning("[ThreadedBands] Deterministic mode with workerCount>1 preserves stable chunk assignment and merge order, but exact execution interleaving still differs. Use workerCount=1 for the single-thread deterministic anchor.");
				}

				if (useThreadedPass1Bands)
				{
					int chunkCount = Math.Max(1, (bandH + threadedBandRowsPerChunk - 1) / threadedBandRowsPerChunk);
					Pass1ThreadLocal[] chunkResults = new Pass1ThreadLocal[chunkCount];
					if (threadedBandWorkerCount <= 1 || chunkCount <= 1)
					{
						for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
						{
							int rowStartLocal = chunkIndex * threadedBandRowsPerChunk;
							int rowEndLocal = Math.Min(bandH, rowStartLocal + threadedBandRowsPerChunk);
							int piStart = rowStartLocal * filmW;
							int piEnd = rowEndLocal * filmW;
							Pass1ThreadLocal local = CreatePass1ThreadLocal();
							for (int pi = piStart; pi < piEnd; pi++)
								local = ProcessPass1Pixel(pi, local);
							chunkResults[chunkIndex] = local;
						}
					}
					else
					{
						var pass1ChunkOptions = new System.Threading.Tasks.ParallelOptions
						{
							MaxDegreeOfParallelism = threadedBandWorkerCount
						};
						System.Threading.Tasks.Parallel.For(
							0,
							chunkCount,
							pass1ChunkOptions,
							chunkIndex =>
							{
								int rowStartLocal = chunkIndex * threadedBandRowsPerChunk;
								int rowEndLocal = Math.Min(bandH, rowStartLocal + threadedBandRowsPerChunk);
								int piStart = rowStartLocal * filmW;
								int piEnd = rowEndLocal * filmW;
								Pass1ThreadLocal local = CreatePass1ThreadLocal();
								for (int pi = piStart; pi < piEnd; pi++)
									local = ProcessPass1Pixel(pi, local);
								chunkResults[chunkIndex] = local;
							});
					}

					for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
						MergePass1ThreadLocal(in chunkResults[chunkIndex]);
				}
				else
				{
					// DECISION: preserve the legacy default path unless threaded bands are explicitly enabled.
					System.Threading.Tasks.Parallel.For(
						0,
						pixelCount,
						new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = jobs },
						() => CreatePass1ThreadLocal(),
						(pi, _, local) => ProcessPass1Pixel(pi, local),
						local => MergePass1ThreadLocal(in local));
				}

				// DECISION: dispose pass1 perf scope when enabled.
				if (framePerfEnabled) pass1Scope.Dispose();
				if (cfg.RenderStepPhaseLog)	LogRenderPhase("pass1-end");

				a1 = Time.GetTicksUsec(); // after wait
				pass1EndUsec = a1;

				// DECISION: if we exceeded budget after pass1, defer pass2 to next frame.
				if (cfg.UpdateEveryFrame && effectiveMaxMs > 0f && renderStepWatch.ElapsedMilliseconds > effectiveMaxMs)
				{
					_pendingBandRowStart = yStart;
					_pendingBandRowCount = bandH;
					_pendingBandHasPass1 = true;
					GD.Print($"[RenderStep][Yield] reason=max_ms_after_pass1 frame={_frameIndex} rowStart={yStart} bandH={bandH} committed=0 pendingPass2=1 ms={renderStepWatch.ElapsedMilliseconds}");
					LogRenderStopOnce("max_ms_after_pass1");
					FinalizeBandAndAdvance("max_ms_after_pass1", yStart, yEnd, bandHits, "pendingPass2=1");
					ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);
					return;
				}

				// DECISION: abort/yield when watchdog triggers.
				if (CheckRenderStepWatchdog())
				{
					// DECISION: if watchdog triggered without a budget stop, abort the render step.
					if (!budgetStop)
					{
						AbortRenderStep("watchdog");
						string maxMsReason = GetMaxMsStopReason();
						LogRenderStopOnce(maxMsReason);
						LogBudgetExitOnce(maxMsReason, _rowCursor);
						ForceAdvanceRowCursorOnStop(maxMsReason, yEnd);
						if (cfg.RenderStepBandLog) LogBandSummaryOnce("guard");
						ResetNoHitStall();
						ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);
						return;
					}
				}

				// DECISION: update perf stats when enabled.
				if (statsEnabled)
				{
					_perfFrame.AddPass1Usec(a1 - a0);
					_perfFrame.Pixels += pixelCount;
				}
				// DECISION: update frame perf counters when enabled.
				if (framePerfEnabled)
				{
					_framePerf.PhysicsQueries += pass1PhysQueries;
					_framePerf.EarlyStopOnHitPixels += pass1EarlyStopPixels;
					_framePerf.StepsIntegrated += pass1StepsIntegrated;
					_framePerf.FieldEvals += pass1FieldEvals;
					_framePerf.Pass1Raycasts += pass1Raycasts;
					_framePerf.Pass1ProbeHits += pass1ProbeHits;
					_framePerf.FieldGridHits += pass1FieldGridHits;
					_framePerf.FieldGridMisses += pass1FieldGridMisses;
					_framePerf.FieldGridFallbacks += pass1FieldGridFallbacks;
					_framePerf.FieldSourceEvals += pass1FieldSourceEvals;
				}
				pass1CompletedThisStep = true;
			}
			else
			{
				// DECISION: when pending pass2, skip pass1 timing.
				pass1StartUsec = 0;
				pass1EndUsec = 0;
				a1 = Time.GetTicksUsec();
			}

			// ---- PASS 2 (main thread): collisions + shading ----
			// EFFECT: mark pass2 start time for budgets and logs.
			pass2StartUsec = a1;
			if (cfg.RenderStepPhaseLog) LogRenderPhase("pass2-start");
			bandAttemptedThisStep = true;
			bandHits = 0;
			bandTracedPixels = 0;
			processedPixelsThisBand = 0;
			bool bandHadCandidates = false;
			_tileMetricCurrentBandIndex = bandIndex;
			_tileMetricCurrentBandY = yStart;
			_tileMetricCurrentBandHeight = Math.Max(0, yEnd - yStart);
			_tileMetricCurrentSubtileWidth = Math.Max(1, TileMetricsSubtileWidth);
			_tileMetricCurrentSubtileCount = Math.Max(1, (filmW + _tileMetricCurrentSubtileWidth - 1) / _tileMetricCurrentSubtileWidth);
			EnsureTileMetricSubtileCapacity(_tileMetricCurrentSubtileCount);
			PrepareExperimentalSubtileSchedulerOrderForCurrentBand();
			float[] adaptiveEnvelopeScaleBySubtile = BuildAdaptiveEnvelopeScaleBySubtileForBand(
				yStart,
				bandH,
				Mathf.Max(0.1f, cfg.Pass2GeomEnvelopeRadiusScale));
			long shadeUsecAccum = 0;
			long pass2EnvelopeUsecAccum = 0;
			long pass2CandidateEvalUsecAccum = 0;
			long pass2QueryUsecAccum = 0;
			long pass2HitResolveUsecAccum = 0;
			long pass2SoftGateUsecAccum = 0;
			long bandSegsIntegrated = 0;
			bandSegsTested = 0;
			bandPhysicsQueries = 0;
			// DECISION: band counters active when any perf tracking is enabled.
			bool bandCountersEnabled = statsEnabled || framePerfEnabled;
			int bandFilledPixels = 0;
			// Pass-2 stride counters track expensive subdivided tests, not whole segments.
			long subRaysSkippedByPass2Stride = 0;
			long subRaysForcedByPass2Stride = 0;
			long pass2StrideSum = 0;
			long pass2StrideCount = 0;
			long bandFarEarlyOuts = 0;

			// Soft-gate pass-2 counters
			long p2SoftGateAttempts = 0;
			long p2SoftGateHits = 0;
			long softGateTriggered = 0;
			long softGateAttempted = 0;
			long softGateHitChangedResult = 0;
			long softGateNewPixelFilled = 0;
			long softGateCandidateNull = 0;
			long softGateLoopGuardTripped = 0;
			long softGateBudgetExceeded = 0;
			long softGateAttemptsUsed = 0;
			long softGateSubdividedCallsUsed = 0;
			long pixelDeltaChanged = 0;
			long pixelDeltaNewFilled = 0;
			int softGateFrameId = (int)Engine.GetFramesDrawn();
			if (cfg.RenderStepPhaseLog) LogRenderPhase("softgate-loop");

			Pass2HitFlags pass2Flags = new Pass2HitFlags
			{
				HitBackFaces = cfg.Pass2HitBackFaces,
				HitFromInside = cfg.Pass2HitFromInside
			};
			// DECISION: encode pass2 flags into a small int for cache keys.
			int pass2FlagsKey = (pass2Flags.HitBackFaces ? 1 : 0) | (pass2Flags.HitFromInside ? 2 : 0);
			int pass2QuickRayMissLogRemaining = cfg.Pass2LogQuickRayMissSamples;

			Vector3 camPosPass2 = _cam.GlobalPosition;
			bool useOverlap = effOverlap;
			bool useQuickRay = effQuickRay;
			bool telemetryHeatmapsEnabled = TelemetryHeatmapsEnabledForCurrentRun();

			// DECISION: configure overlap broadphase only when enabled.
			if (useOverlap)
			{
				_overlapSphere ??= new SphereShape3D();
				_overlapQuery ??= new PhysicsShapeQueryParameters3D();
				_overlapSphere.Radius = rayCfg.CollisionRadius + broadphaseCfg.Margin;
				_overlapQuery.Shape = _overlapSphere;
				_overlapQuery.CollisionMask = rayCfg.CollisionMask;
				_overlapQuery.CollideWithBodies = true;
				_overlapQuery.CollideWithAreas = true;
			}

			// DECISION: configure quick-ray params when quick probing is used.
			if (useQuickRay || cfg.UseSingleProbeThenSubdivide)
			{
				_quickRayParams ??= new PhysicsRayQueryParameters3D();
				_quickRayParams.CollisionMask = rayCfg.CollisionMask;
				_quickRayParams.CollideWithBodies = true;
				_quickRayParams.CollideWithAreas = true;
				_quickRayParams.HitFromInside = pass2Flags.HitFromInside;
				_quickRayParams.HitBackFaces = pass2Flags.HitBackFaces;
			}

			// DECISION: reset quick-ray cache when quick probes are active.
			if (useQuickRay || cfg.UseSingleProbeThenSubdivide)
			{
				EnsurePass2QuickRayCache();
				ResetPass2QuickRayCache();
			}

				PerfScope pass2Scope = default;
				// DECISION: enable pass2 perf scope when frame perf is enabled.
				if (framePerfEnabled) pass2Scope = new PerfScope(_framePerf, PerfStage.Pass2_Subdivide);
				bool shadeTimingEnabled = statsEnabled || framePerfEnabled;
				bool requestedThreadedPass2QueryResolve = cfg.UseThreadedPass2QueryResolve;
				bool useThreadedPass2QueryResolve = requestedThreadedPass2QueryResolve
					&& !skipBandPhysics
					&& useGeomTlasPruningForStep
					&& !softGateCfg.EnableQuickRayMiss
					&& !cfg.UseSingleProbeThenSubdivide;
				if (requestedThreadedPass2QueryResolve && !useThreadedPass2QueryResolve)
				{
					GD.Print(
						$"[ThreadedPass2QueryResolve] enabled=0 reason=requires_geom_prune_and_softgate_off_and_singleprobe_off " +
						$"skipBandPhysics={(skipBandPhysics ? 1 : 0)} geomPrune={(useGeomTlasPruningForStep ? 1 : 0)} " +
						$"softgate={(softGateCfg.EnableQuickRayMiss ? 1 : 0)} singleProbe={(cfg.UseSingleProbeThenSubdivide ? 1 : 0)}");
				}
				int threadedPass2QueryWorkers = Math.Max(1, cfg.ThreadedPass2QueryWorkers);
				int threadedPass2QueryRowsPerChunk = Math.Max(1, cfg.ThreadedPass2QueryRowsPerChunk);
				bool useThreadedPass2CandidateEval = (cfg.UseThreadedPass2CandidateEval || useThreadedPass2QueryResolve)
					&& !skipBandPhysics
					&& useGeomTlasPruningForStep;
				int threadedPass2CandidateWorkers = Math.Max(1, cfg.ThreadedPass2CandidateWorkers);
				int threadedPass2CandidateRowsPerChunk = Math.Max(1, cfg.ThreadedPass2CandidateRowsPerChunk);
				bool useThreadedPass2LocalAccumulation = cfg.UseThreadedPass2LocalAccumulation
					&& !skipBandPhysics
					&& !useThreadedPass2QueryResolve;
				int threadedPass2WorkerCount = Math.Max(1, cfg.ThreadedPass2WorkerCount);
				int threadedPass2RowsPerChunk = Math.Max(1, cfg.ThreadedPass2RowsPerChunk);
				bool useThreadedPass2ResolvedSampleCommit = useThreadedPass2LocalAccumulation || useThreadedPass2QueryResolve;
				int[] pass2CandidateRecordStarts = Array.Empty<int>();
				int[] pass2CandidateVisitedSegStarts = Array.Empty<int>();
				int[] pass2CandidateVisitedSegCounts = Array.Empty<int>();
				Pass2CandidateEvalRecord[] pass2CandidateEvalRecords = Array.Empty<Pass2CandidateEvalRecord>();
				long[] pass2CandidateEvalIds = Array.Empty<long>();
				var pass2ResolvedSamples = useThreadedPass2ResolvedSampleCommit
					? new System.Collections.Generic.List<Pass2ResolvedSample>(Math.Max(16, bandH * Math.Max(1, filmW / Math.Max(1, stride))))
					: null;
				var pass2ChunkSampleStarts = useThreadedPass2ResolvedSampleCommit
					? new System.Collections.Generic.List<int>(Math.Max(2, (bandH + Math.Max(1, useThreadedPass2QueryResolve ? threadedPass2QueryRowsPerChunk : threadedPass2RowsPerChunk) - 1) / Math.Max(1, useThreadedPass2QueryResolve ? threadedPass2QueryRowsPerChunk : threadedPass2RowsPerChunk) + 1))
					: null;

			void RecordRenderHealthPass2Sample(float radius, float envDiag, float envelopeInflation, int candidateCount)
			{
				if (float.IsNaN(radius) || float.IsInfinity(radius)) return;
				if (float.IsNaN(envDiag) || float.IsInfinity(envDiag)) return;
				if (float.IsNaN(envelopeInflation) || float.IsInfinity(envelopeInflation)) return;

				pass2SampledSegments++;
				pass2RadiusSum += radius;
				if (radius > pass2RadiusMax) pass2RadiusMax = radius;
				pass2EnvDiagSum += envDiag;
				if (envDiag > pass2EnvDiagMax) pass2EnvDiagMax = envDiag;
				pass2EnvelopeInflationSum += envelopeInflation;
				if (envelopeInflation > pass2EnvelopeInflationMax) pass2EnvelopeInflationMax = envelopeInflation;

				if (candidateCount >= 0)
				{
					if (candidateCount <= 0) pass2CandidateCount0++;
					else if (candidateCount <= 2) pass2CandidateCount1To2++;
					else if (candidateCount <= 8) pass2CandidateCount3To8++;
					else if (candidateCount <= 32) pass2CandidateCount9To32++;
					else pass2CandidateCount33Plus++;
				}
			}

			void CountQuickRayResult(bool hit)
			{
				// DECISION: only count quick-ray stats when debug is enabled.
				if (!softGateDebugEnabled) return;
				_softGateFrame.QRayCalls++;
				// DECISION: increment hit vs miss counters.
				if (hit) _softGateFrame.QRayHit++;
				else _softGateFrame.QRayMiss++;
				// DECISION: also update band counters when enabled.
				if (softGateBandEnabled)
				{
					_softGateBand.QRayCalls++;
					// DECISION: increment hit vs miss counters for band.
					if (hit) _softGateBand.QRayHit++;
					else _softGateBand.QRayMiss++;
				}
			}

			void SoftGateRecordMetric(float metric)
			{
				// DECISION: only record metrics when debug is enabled.
				if (!softGateDebugEnabled) return;
				_softGateFrame.SoftGateMetricCount++;
				_softGateFrame.SoftGateMetricSum += metric;
				// DECISION: update min/max metric for frame.
				if (metric < _softGateFrame.SoftGateMetricMin) _softGateFrame.SoftGateMetricMin = metric;
				if (metric > _softGateFrame.SoftGateMetricMax) _softGateFrame.SoftGateMetricMax = metric;
				// DECISION: also update band metrics when enabled.
				if (softGateBandEnabled)
				{
					_softGateBand.SoftGateMetricCount++;
					_softGateBand.SoftGateMetricSum += metric;
					// DECISION: update min/max metric for band.
					if (metric < _softGateBand.SoftGateMetricMin) _softGateBand.SoftGateMetricMin = metric;
					if (metric > _softGateBand.SoftGateMetricMax) _softGateBand.SoftGateMetricMax = metric;
				}
			}

			void SoftGateRecordSkip(SoftGateDecisionReason reason)
			{
				// DECISION: only record skips when debug is enabled.
				if (!softGateDebugEnabled) return;
				_softGateFrame.SoftGateSkipped++;
				// DECISION: also update band skip counters when enabled.
				if (softGateBandEnabled) _softGateBand.SoftGateSkipped++;
				// DECISION: bucket skip reason into counters.
				switch (reason)
				{
					case SoftGateDecisionReason.Disabled:
						_softGateFrame.SkipOther++;
						// DECISION: update band counters when enabled.
						if (softGateBandEnabled) _softGateBand.SkipOther++;
						break;
					case SoftGateDecisionReason.SegLenTooShort:
						_softGateFrame.SkipSegLenTooShort++;
						// DECISION: update band counters when enabled.
						if (softGateBandEnabled) _softGateBand.SkipSegLenTooShort++;
						break;
					case SoftGateDecisionReason.ScoreTooLow:
						_softGateFrame.SkipScoreTooLow++;
						// DECISION: update band counters when enabled.
						if (softGateBandEnabled) _softGateBand.SkipScoreTooLow++;
						break;
					case SoftGateDecisionReason.RandomNotSelected:
						_softGateFrame.SkipRandomNotSelected++;
						// DECISION: update band counters when enabled.
						if (softGateBandEnabled) _softGateBand.SkipRandomNotSelected++;
						break;
					case SoftGateDecisionReason.BudgetAttemptCap:
						_softGateFrame.SkipBudgetAttemptCap++;
						// DECISION: update band counters when enabled.
						if (softGateBandEnabled) _softGateBand.SkipBudgetAttemptCap++;
						break;
					case SoftGateDecisionReason.BudgetSubdivideCap:
						_softGateFrame.SkipBudgetSubdivideCap++;
						// DECISION: update band counters when enabled.
						if (softGateBandEnabled) _softGateBand.SkipBudgetSubdivideCap++;
						break;
					case SoftGateDecisionReason.Guard:
						_softGateFrame.SkipGuard++;
						// DECISION: update band counters when enabled.
						if (softGateBandEnabled) _softGateBand.SkipGuard++;
						break;
					case SoftGateDecisionReason.NanMetric:
					case SoftGateDecisionReason.Other:
					default:
						_softGateFrame.SkipOther++;
						// DECISION: update band counters when enabled.
						if (softGateBandEnabled) _softGateBand.SkipOther++;
						break;
				}
			}

			bool TryHandleQuickRayMissWithSoftGate(
				int frameId,
				int segIndex,
				float segmentLength,
				Vector3 prevSegDir,
				Vector3 currSegDir,
				bool prevHadHit,
				bool prevHitLost,
				bool countSubdividedSkip,
				bool singleProbeSkipCounter,
				ref SoftGateDecisionReason reason,
				ref float score,
				ref float turnAngleDeg,
				ref float turnAngleScore,
				ref float prevHitLostScore,
				ref bool randomProbe,
				ref bool segLenOk,
				ref bool sampleThisSeg,
				ref bool attemptSubdivide,
				ref int attemptsThisPixel,
				ref long attemptsUsed,
				ref long subdividedCallsUsed,
				ref long softGateAttemptsTotal,
				ref long budgetExceeded)
			{
				ulong softGateTimingStart = 0;
				if (statsEnabled) softGateTimingStart = Time.GetTicksUsec();
				try
				{
					// DECISION: sample this segment only when segment-level debug is enabled.
					if (softGateSegEnabled)
						sampleThisSeg = (_softGateSampleCounter++ % SoftGateSampleEveryNSegments) == 0;

				score = 0f;
				turnAngleDeg = 0f;
				turnAngleScore = 0f;
				prevHitLostScore = 0f;
				randomProbe = false;
				// DECISION: segment length is ok if min length is disabled or segment is long enough.
				segLenOk = pass2SoftGateMinSegmentLengthEffective <= 0f || segmentLength >= pass2SoftGateMinSegmentLengthEffective;

				// DECISION: per-pixel attempt budget gate.
				if (softGateCfg.MaxAttemptsPerPixel > 0 && attemptsThisPixel >= softGateCfg.MaxAttemptsPerPixel)
				{
					budgetExceeded++;
					reason = SoftGateDecisionReason.BudgetAttemptCap;
					SoftGateRecordSkip(reason);
					DisableSoftGateThisFrame("budget_pixel");
					LogSoftGateSample(
						segIndex,
						segmentLength,
						score,
						turnAngleDeg,
						turnAngleScore,
						prevHitLostScore,
						randomProbe,
						segLenOk,
						false,
						false,
						false,
						reason,
						sampleThisSeg);
					return false;
				}

				// DECISION: per-frame attempt budget gate.
				if (pass2SoftGateMaxAttemptsPerFrameEffective > 0 && _softGateAttemptsUsedThisFrame >= pass2SoftGateMaxAttemptsPerFrameEffective)
				{
					budgetExceeded++;
					reason = SoftGateDecisionReason.BudgetAttemptCap;
					SoftGateRecordSkip(reason);
					DisableSoftGateThisFrame("budget_attempt");
					LogBudgetExitOnce("softgate_attempt_cap", _rowCursor);
					// DECISION: yield when updating every frame.
					if (cfg.UpdateEveryFrame) TriggerBudgetStop("softgate_attempt_cap");
					LogSoftGateSample(
						segIndex,
						segmentLength,
						score,
						turnAngleDeg,
						turnAngleScore,
						prevHitLostScore,
						randomProbe,
						segLenOk,
						false,
						false,
						false,
						reason,
						sampleThisSeg);
					return false;
				}

				// DECISION: per-frame subdivide budget gate.
				if (pass2SoftGateMaxSubdividedCallsPerFrameEffective > 0 && _softGateSubdividedCallsUsedThisFrame >= pass2SoftGateMaxSubdividedCallsPerFrameEffective)
				{
					budgetExceeded++;
					reason = SoftGateDecisionReason.BudgetSubdivideCap;
					SoftGateRecordSkip(reason);
					DisableSoftGateThisFrame("budget_subdivide");
					LogBudgetExitOnce("softgate_subdivide_cap", _rowCursor);
					// DECISION: yield when updating every frame.
					if (cfg.UpdateEveryFrame) TriggerBudgetStop("softgate_subdivide_cap");
					LogSoftGateSample(
						segIndex,
						segmentLength,
						score,
						turnAngleDeg,
						turnAngleScore,
						prevHitLostScore,
						randomProbe,
						segLenOk,
						false,
						false,
						false,
						reason,
						sampleThisSeg);
					return false;
				}

				bool allowSoftGate = ShouldSoftGate(
					frameId,
					segIndex,
					segmentLength,
					prevSegDir,
					currSegDir,
					prevHadHit,
					prevHitLost,
					out reason,
					out score,
					out turnAngleDeg,
					out turnAngleScore,
					out prevHitLostScore,
					out randomProbe,
					out segLenOk);

				// DECISION: if SoftGate disallows this segment, skip subdivide.
				if (!allowSoftGate)
				{
					// DECISION: optionally count subdivide skips.
					if (countSubdividedSkip) _perfFrame.SubdividedRaySkipped++;
					// DECISION: update frame perf skip counters when enabled.
					if (framePerfEnabled)
					{
						// DECISION: categorize skip reason based on probe mode.
						if (singleProbeSkipCounter) _framePerf.Pass2Skip_SingleProbeMiss++;
						else _framePerf.Pass2Skip_QuickRayMiss++;
					}
					LogSoftGateSample(
						segIndex,
						segmentLength,
						score,
						turnAngleDeg,
						turnAngleScore,
						prevHitLostScore,
						randomProbe,
						segLenOk,
						false,
						false,
						false,
						reason,
						sampleThisSeg);
					return false;
				}

				bool attemptBudgetOk = (softGateCfg.MaxAttemptsPerPixel > 0 && attemptsThisPixel < softGateCfg.MaxAttemptsPerPixel)
					&& (pass2SoftGateMaxAttemptsPerFrameEffective > 0 && _softGateAttemptsUsedThisFrame < pass2SoftGateMaxAttemptsPerFrameEffective);
				bool subdivideBudgetOk = pass2SoftGateMaxSubdividedCallsPerFrameEffective > 0
					&& _softGateSubdividedCallsUsedThisFrame < pass2SoftGateMaxSubdividedCallsPerFrameEffective;
				// DECISION: abort if either attempt or subdivide budget is exhausted.
				if (!attemptBudgetOk || !subdivideBudgetOk)
				{
					budgetExceeded++;
					reason = attemptBudgetOk ? SoftGateDecisionReason.BudgetSubdivideCap : SoftGateDecisionReason.BudgetAttemptCap;
					SoftGateRecordSkip(reason);
					DisableSoftGateThisFrame(attemptBudgetOk ? "budget_subdivide" : "budget_attempt");
					string softGateBudgetReason = attemptBudgetOk ? "softgate_subdivide_cap" : "softgate_attempt_cap";
					LogBudgetExitOnce(softGateBudgetReason, _rowCursor);
					// DECISION: yield when updating every frame.
					if (cfg.UpdateEveryFrame) TriggerBudgetStop(softGateBudgetReason);
					LogSoftGateSample(
						segIndex,
						segmentLength,
						score,
						turnAngleDeg,
						turnAngleScore,
						prevHitLostScore,
						randomProbe,
						segLenOk,
						false,
						false,
						false,
						reason,
						sampleThisSeg);
					return false;
				}

				attemptSubdivide = true;
				attemptsThisPixel++;
				attemptsUsed++;
				subdividedCallsUsed++;
					_softGateAttemptsUsedThisFrame++;
					_softGateSubdividedCallsUsedThisFrame++;
					softGateAttemptsTotal++;
					return true;
				}
				finally
				{
					if (statsEnabled && softGateTimingStart > 0)
						pass2SoftGateUsecAccum += (long)(Time.GetTicksUsec() - softGateTimingStart);
				}
			}

			bool ShouldSoftGate(
				int frameId,
				int segIndex,
				float segmentLength,
				Vector3 prevSegDir,
				Vector3 currSegDir,
				bool prevHadHit,
				bool prevHitLost,
				out SoftGateDecisionReason reason,
				out float score,
				out float turnAngleDeg,
				out float turnAngleScore,
				out float prevHitLostScore,
				out bool randomProbe,
				out bool segLenOk)
			{
				float minSegLen = pass2SoftGateMinSegmentLengthEffective;
				score = 0f;
				turnAngleDeg = 0f;
				turnAngleScore = 0f;
				prevHitLostScore = 0f;
				randomProbe = false;
				segLenOk = false;
				reason = SoftGateDecisionReason.Allow;

				// SoftGate v2: allow only on QuickRay misses with instability evidence and within the per-frame budget.
				// DECISION: reset per-frame soft-gate counters when frame changes.
				if (frameId != _softGateFrameId)
				{
					_softGateFrameId = frameId;
					_p2SoftGateUsedThisFrame = 0;
				}

				// DECISION: track considered count when debug is enabled.
				if (softGateDebugEnabled)
				{
					_softGateFrame.SoftGateConsidered++;
					if (softGateBandEnabled) _softGateBand.SoftGateConsidered++;
				}

				// DECISION: guard when soft gate is disabled for this frame/pass.
				if (softGateDisabledThisFrame || _softGateDisabledForPass)
				{
					reason = SoftGateDecisionReason.Guard;
					SoftGateRecordSkip(reason);
					return false;
				}

				// DECISION: soft gate requires both quick-ray-miss and scoring to be enabled.
				if (!softGateCfg.EnableQuickRayMiss || !softGateCfg.ScoringEnabled)
				{
					reason = SoftGateDecisionReason.Disabled;
					SoftGateRecordSkip(reason);
					return false;
				}

				// DECISION: emit parameter logs only when debug enabled and budget remains.
				if (softGateCfg.DebugEnabled && _softGateParamLogRemaining > 0)
				{
					// DECISION: log includes RayBeam scaling when active.
					if (pass2SoftGateUseRayBeamSettingsActive)
					{
						GD.Print($"[SoftGate][Cfg] segIndex={segIndex} minSegLen={minSegLen:0.###} minSegSteps={softGateCfg.MinSegLenSteps:0.###} effStepLen={pass2SoftGateEffStepLen:0.###} scoreThr={softGateCfg.ScoreThreshold:0.###} turnW={softGateCfg.ScoreTurnAngleWeight:0.###} prevLost={softGateCfg.ScorePrevHitLostBonus:0.###} rand={softGateCfg.RandomProbeChance:0.###}");
					}
					else
					{
						GD.Print($"[SoftGate][Cfg] segIndex={segIndex} minSegLen={minSegLen:0.###} scoreThr={softGateCfg.ScoreThreshold:0.###} turnW={softGateCfg.ScoreTurnAngleWeight:0.###} prevLost={softGateCfg.ScorePrevHitLostBonus:0.###} rand={softGateCfg.RandomProbeChance:0.###}");
					}
					_softGateParamLogRemaining--;
				}

				bool metricsFinite = float.IsFinite(segmentLength)
					&& float.IsFinite(minSegLen)
					&& float.IsFinite(softGateCfg.ScoreThreshold)
					&& float.IsFinite(softGateCfg.ScoreTurnAngleWeight)
					&& float.IsFinite(softGateCfg.ScorePrevHitLostBonus)
					&& float.IsFinite(softGateCfg.RandomProbeChance)
					&& float.IsFinite(prevSegDir.X) && float.IsFinite(prevSegDir.Y) && float.IsFinite(prevSegDir.Z)
					&& float.IsFinite(currSegDir.X) && float.IsFinite(currSegDir.Y) && float.IsFinite(currSegDir.Z);
				// DECISION: skip when any metric is non-finite.
				if (!metricsFinite)
				{
					reason = SoftGateDecisionReason.NanMetric;
					SoftGateRecordSkip(reason);
					return false;
				}

				// Min segment length: avoids spending budget on tiny segments that rarely change the result.
				segLenOk = minSegLen <= 0f || segmentLength >= minSegLen;
				// DECISION: skip when segment is too short.
				if (!segLenOk)
				{
					reason = SoftGateDecisionReason.SegLenTooShort;
					SoftGateRecordSkip(reason);
					return false;
				}

				// Turn-angle score: captures local curvature/instability in the segment chain.
				bool haveDirs = prevSegDir.LengthSquared() > 1e-6f && currSegDir.LengthSquared() > 1e-6f;
				// DECISION: compute turn-angle score only when directions are valid and weight > 0.
				if (haveDirs && softGateCfg.ScoreTurnAngleWeight > 0f)
				{
					float dot = Mathf.Clamp(prevSegDir.Dot(currSegDir), -1f, 1f);
					turnAngleDeg = Mathf.RadToDeg(Mathf.Acos(dot));
					turnAngleScore = (turnAngleDeg / 180f) * softGateCfg.ScoreTurnAngleWeight;
					score += turnAngleScore;
				}
				// Prev-hit-lost bonus: encourages probing when last frame hit disappeared.
				// DECISION: add bonus when previous hit was lost.
				if (prevHadHit && prevHitLost)
				{
					prevHitLostScore = softGateCfg.ScorePrevHitLostBonus;
					score += prevHitLostScore;
				}

				// Random probe: avoids missing thin/rare occluders when score stays low.
				randomProbe = softGateCfg.RandomProbeChance > 0f && _rng.Randf() < softGateCfg.RandomProbeChance;
				bool scoreHit = score >= softGateCfg.ScoreThreshold || randomProbe;

				// DECISION: record metric only when debug enabled.
				if (softGateDebugEnabled) SoftGateRecordMetric(score);

				// Score threshold: only trigger when instability evidence is strong enough.
				// DECISION: skip when score below threshold and no random probe.
				if (!scoreHit)
				{
					bool randEnabled = softGateCfg.RandomProbeChance > 0f;
					reason = randEnabled ? SoftGateDecisionReason.RandomNotSelected : SoftGateDecisionReason.ScoreTooLow;
					SoftGateRecordSkip(reason);
					return false;
				}

				// DECISION: enforce per-frame score budget.
				if (softGateCfg.ScoreBudgetPerFrame > 0 && _p2SoftGateUsedThisFrame >= softGateCfg.ScoreBudgetPerFrame)
				{
					reason = SoftGateDecisionReason.BudgetAttemptCap;
					SoftGateRecordSkip(reason);
					return false;
				}

				// DECISION: update forced counters when debug enabled.
				if (softGateDebugEnabled)
				{
					_softGateFrame.SoftGateForced++;
					// DECISION: also update band forced counters when enabled.
					if (softGateBandEnabled) _softGateBand.SoftGateForced++;
				}

				_p2SoftGateUsedThisFrame++;

				softGateTriggered++;
				return true;
			}

			void LogSoftGateSample(int segIndex, float segmentLength, float score, float turnAngleDeg, float turnAngleScore, float prevHitLostScore, bool randomProbe, bool segLenOk, bool forced, bool attempted, bool hit, SoftGateDecisionReason reason, bool sampleThisSeg)
			{
				// DECISION: only log sampled segments.
				if (!sampleThisSeg) return;

				string reasonText;
				// DECISION: map reason enum to text.
				switch (reason)
				{
					case SoftGateDecisionReason.Disabled:
						reasonText = "disabled";
						break;
					case SoftGateDecisionReason.NanMetric:
						reasonText = "nan";
						break;
					case SoftGateDecisionReason.SegLenTooShort:
						reasonText = "seglen_short";
						break;
					case SoftGateDecisionReason.ScoreTooLow:
						reasonText = "score_low";
						break;
					case SoftGateDecisionReason.RandomNotSelected:
						reasonText = "rand_miss";
						break;
					case SoftGateDecisionReason.BudgetAttemptCap:
						reasonText = "budget_attempt";
						break;
					case SoftGateDecisionReason.BudgetSubdivideCap:
						reasonText = "budget_sub";
						break;
					case SoftGateDecisionReason.Guard:
						reasonText = "guard";
						break;
					case SoftGateDecisionReason.Allow:
						reasonText = "allow";
						break;
					default:
						reasonText = "other";
						break;
				}

				// DECISION: append status flags for segment length, score, and random probe.
				reasonText += $" seglen={(segLenOk ? "ok" : "short")} score={(score >= softGateCfg.ScoreThreshold ? "ok" : "low")} rand={(randomProbe ? 1 : 0)}";

				GD.Print(
					$"SG seg={segIndex} len={segmentLength:0.###} score={score:0.###} angleDeg={turnAngleDeg:0.###} angleScore={turnAngleScore:0.###} prevLostScore={prevHitLostScore:0.###} forced={(forced ? 1 : 0)} attempt={(attempted ? 1 : 0)} hit={(hit ? 1 : 0)} reason={reasonText}");
			}

			int pixelsVisitedThisBand = 0;
			int bandPixelCountGuard = 0;
			int pixelLoopGuardSlack = Math.Max(4, stride * 2);
			if (bandH > 0 && filmW > 0 && stride > 0)
			{
				int rowsGuard = (bandH + stride - 1) / stride;
				int colsGuard = (filmW + stride - 1) / stride;
				bandPixelCountGuard = rowsGuard * colsGuard;
			}
			bool pixelLoopGuardTripped = false;
				void CheckPixelLoopGuard(int x, int y)
				{
					if (pixelLoopGuardTripped) return;
				pixelsVisitedThisBand++;
				if (bandPixelCountGuard <= 0) return;
				if (pixelsVisitedThisBand <= bandPixelCountGuard + pixelLoopGuardSlack) return;
				pixelLoopGuardTripped = true;
				GD.Print($"[WATCHDOG] pixelLoopGuard tripped at row={y} band=[{yStart},{yEnd}) policy={broadphaseCfg.Policy} x={x} stride={stride}");
					TriggerBudgetStop("guard_pixel_loop");
					ForceAdvanceRowCursorOnStop("guard_pixel_loop", yEnd);
				}

				void PrepareThreadedPass2CandidateEval()
				{
					if (!useThreadedPass2CandidateEval)
						return;

					int pixelCountForBand = bandH * filmW;
					if (pixelCountForBand <= 0 || geomTlasForStep == null || geomEntitiesForStep == null)
						return;

					pass2CandidateRecordStarts = new int[pixelCountForBand];
					pass2CandidateVisitedSegStarts = new int[pixelCountForBand];
					pass2CandidateVisitedSegCounts = new int[pixelCountForBand];

					var chunkDescriptors = new System.Collections.Generic.List<Pass2CandidateEvalChunk>(Math.Max(1, (bandH + threadedPass2CandidateRowsPerChunk - 1) / threadedPass2CandidateRowsPerChunk));
					int totalRecordCount = 0;
					int yAlignedStart = yStart + ((stride - (yStart % stride)) % stride);
					int sampledRowOrdinal = 0;
					int chunkStartRow = -1;
					int chunkFirstRecordIndex = 0;

					void FlushChunkDescriptor(int endRowExclusive)
					{
						if (chunkStartRow < 0)
							return;
						chunkDescriptors.Add(new Pass2CandidateEvalChunk
						{
							StartRow = chunkStartRow,
							EndRowExclusive = endRowExclusive,
							FirstRecordIndex = chunkFirstRecordIndex,
							RecordCount = Math.Max(0, totalRecordCount - chunkFirstRecordIndex)
						});
						chunkStartRow = -1;
					}

					for (int y = yAlignedStart; y < yEnd; y += stride, sampledRowOrdinal++)
					{
						if (sampledRowOrdinal % threadedPass2CandidateRowsPerChunk == 0)
						{
							FlushChunkDescriptor(y);
							chunkStartRow = y;
							chunkFirstRecordIndex = totalRecordCount;
						}

						int localY = y - yStart;
						for (int execIndex = 0; execIndex < _tileMetricCurrentSubtileCount; execIndex++)
						{
							int subtileIndex = _tileMetricCurrentExecutionOrder[execIndex];
							int subtileXStart = subtileIndex * _tileMetricCurrentSubtileWidth;
							int subtileXEnd = Math.Max(0, Math.Min(filmW, subtileXStart + _tileMetricCurrentSubtileWidth));
							if (subtileXStart >= subtileXEnd)
								continue;
							int xAlignedStart = subtileXStart + ((stride - (subtileXStart % stride)) % stride);
							for (int x = xAlignedStart; x < subtileXEnd; x += stride)
							{
								int pi = localY * filmW + x;
								int segCount = _segCountPerPixel[pi];
								int visitedSegStart = 0;
								int visitedSegCount = 0;
								if (segCount > 0)
								{
									visitedSegStart = 0;
									int visitedSegEnd = segCount - 1;
									if (_pass1StoppedEarly[pi] && _pass1HitSegIndex[pi] >= 0)
									{
										visitedSegStart = Math.Max(0, _pass1HitSegIndex[pi] - 1);
										visitedSegEnd = Math.Min(segCount - 1, _pass1HitSegIndex[pi] + 1);
									}
									visitedSegCount = Math.Max(0, visitedSegEnd - visitedSegStart + 1);
								}

								pass2CandidateRecordStarts[pi] = totalRecordCount;
								pass2CandidateVisitedSegStarts[pi] = visitedSegStart;
								pass2CandidateVisitedSegCounts[pi] = visitedSegCount;
								totalRecordCount += visitedSegCount;
							}
						}
					}
					FlushChunkDescriptor(yEnd);

					if (totalRecordCount <= 0 || chunkDescriptors.Count == 0)
					{
						useThreadedPass2CandidateEval = false;
						pass2CandidateRecordStarts = Array.Empty<int>();
						pass2CandidateVisitedSegStarts = Array.Empty<int>();
						pass2CandidateVisitedSegCounts = Array.Empty<int>();
						return;
					}

					pass2CandidateEvalRecords = new Pass2CandidateEvalRecord[totalRecordCount];

					void EvaluateCandidateChunk(int chunkIndex)
					{
						Pass2CandidateEvalChunk chunk = chunkDescriptors[chunkIndex];
						var localCandidateIds = new System.Collections.Generic.List<long>(Math.Max(8, chunk.RecordCount * 2));
						int[] localGeomCandidates = new int[Math.Max(1, _geomCandidatesScratch.Length)];
						long[] localCandidateIdsScratch = new long[Math.Max(1, _geomCandidateInstanceIdsScratch.Length)];

						for (int y = chunk.StartRow; y < chunk.EndRowExclusive; y += stride)
						{
							int localY = y - yStart;
							for (int execIndex = 0; execIndex < _tileMetricCurrentSubtileCount; execIndex++)
							{
								int subtileIndex = _tileMetricCurrentExecutionOrder[execIndex];
								int subtileXStart = subtileIndex * _tileMetricCurrentSubtileWidth;
								int subtileXEnd = Math.Max(0, Math.Min(filmW, subtileXStart + _tileMetricCurrentSubtileWidth));
								if (subtileXStart >= subtileXEnd)
									continue;
								int xAlignedStart = subtileXStart + ((stride - (subtileXStart % stride)) % stride);
								for (int x = xAlignedStart; x < subtileXEnd; x += stride)
								{
									int pi = localY * filmW + x;
									int segCount = _segCountPerPixel[pi];
									int segOffset = pi * maxSeg;
									int visitedSegStart = pass2CandidateVisitedSegStarts[pi];
									int visitedSegCount = pass2CandidateVisitedSegCounts[pi];
									int recordBase = pass2CandidateRecordStarts[pi];
									for (int localSegIndex = 0; localSegIndex < visitedSegCount; localSegIndex++)
									{
										int si = visitedSegStart + localSegIndex;
										if (si < 0 || si >= segCount)
											continue;
										ref readonly var seg = ref _segBuf[segOffset + si];
										Vector3 segA = seg.A;
										Vector3 segB = seg.B;
										ulong envelopeStartUsec = 0;
										if (statsEnabled) envelopeStartUsec = Time.GetTicksUsec();
										var segANum = new System.Numerics.Vector3(segA.X, segA.Y, segA.Z);
										var segBNum = new System.Numerics.Vector3(segB.X, segB.Y, segB.Z);
										float baseRadiusBound = Mathf.Max(0f, seg.RadiusBound);
										float localEnvelopeScale = (uint)subtileIndex < (uint)adaptiveEnvelopeScaleBySubtile.Length
											? adaptiveEnvelopeScaleBySubtile[subtileIndex]
											: Mathf.Max(0.1f, cfg.Pass2GeomEnvelopeRadiusScale);
										float geomEnvelopeRadius = baseRadiusBound * Mathf.Max(0.1f, localEnvelopeScale);
										float geomEnvelopeAabbExpand = Mathf.Max(0.0f, cfg.Pass2GeomEnvelopeAabbExpand);
										Aabb3 envelope = Aabb3.FromSegment(segANum, segBNum).Expand(geomEnvelopeRadius);
										if (geomEnvelopeAabbExpand > 0f)
											envelope = envelope.Expand(geomEnvelopeAabbExpand);
										float envDiag = envelope.Extents.Length();
										float envInflation = Math.Max(0f, (geomEnvelopeRadius - baseRadiusBound) + geomEnvelopeAabbExpand);
										if (statsEnabled && envelopeStartUsec > 0)
											System.Threading.Interlocked.Add(ref pass2EnvelopeUsecAccum, (long)(Time.GetTicksUsec() - envelopeStartUsec));

										ulong candidateStartUsec = 0;
										if (statsEnabled) candidateStartUsec = Time.GetTicksUsec();
										int geomCandidateCount = geomTlasForStep.QueryAabb(envelope, localGeomCandidates);
										if (statsEnabled)
										{
											ulong candidateUsec = Time.GetTicksUsec() - candidateStartUsec;
											System.Threading.Interlocked.Add(ref pass2CandidateEvalUsecAccum, (long)candidateUsec);
										}

										int geomCandidateInstanceCount = 0;
										if (geomCandidateCount > 0)
										{
											var ids = geomEntitiesForStep.GodotInstanceIds;
											int idsLen = ids.Length;
											int maxFill = Math.Min(geomCandidateCount, localCandidateIdsScratch.Length);
											for (int gi = 0; gi < maxFill; gi++)
											{
												int geomIndex = localGeomCandidates[gi];
												if ((uint)geomIndex < (uint)idsLen)
													localCandidateIdsScratch[geomCandidateInstanceCount++] = ids[geomIndex];
											}
											if (geomCandidateInstanceCount > 1)
											{
												SortLongSpan(localCandidateIdsScratch, geomCandidateInstanceCount);
												geomCandidateInstanceCount = DedupSortedLong(localCandidateIdsScratch, geomCandidateInstanceCount);
											}
										}

										int recordIndex = recordBase + localSegIndex;
										pass2CandidateEvalRecords[recordIndex] = new Pass2CandidateEvalRecord
										{
											CandidateStart = localCandidateIds.Count,
											CandidateCount = geomCandidateInstanceCount,
											EnvelopeRadius = geomEnvelopeRadius,
											EnvelopeDiag = envDiag,
											EnvelopeInflation = envInflation
										};

										chunk.CandidateSegments++;
										chunk.CandidateReferences += geomCandidateInstanceCount;
										if (geomCandidateInstanceCount <= 0)
											chunk.CandidateSegmentsZero++;
										else
											chunk.CandidateSegmentsWithHits++;

										for (int ci = 0; ci < geomCandidateInstanceCount; ci++)
											localCandidateIds.Add(localCandidateIdsScratch[ci]);
									}
								}
							}
						}

						chunk.CandidateIds = localCandidateIds.Count > 0 ? localCandidateIds.ToArray() : Array.Empty<long>();
						chunkDescriptors[chunkIndex] = chunk;
					}

					if (threadedPass2CandidateWorkers <= 1 || chunkDescriptors.Count <= 1)
					{
						for (int chunkIndex = 0; chunkIndex < chunkDescriptors.Count; chunkIndex++)
							EvaluateCandidateChunk(chunkIndex);
					}
					else
					{
						var candidateOptions = new System.Threading.Tasks.ParallelOptions
						{
							MaxDegreeOfParallelism = threadedPass2CandidateWorkers
						};
						System.Threading.Tasks.Parallel.For(0, chunkDescriptors.Count, candidateOptions, EvaluateCandidateChunk);
					}
					int totalCandidateIds = 0;
					for (int chunkIndex = 0; chunkIndex < chunkDescriptors.Count; chunkIndex++)
						totalCandidateIds += chunkDescriptors[chunkIndex].CandidateIds.Length;
					pass2CandidateEvalIds = totalCandidateIds > 0 ? new long[totalCandidateIds] : Array.Empty<long>();

					int globalCandidateOffset = 0;
					for (int chunkIndex = 0; chunkIndex < chunkDescriptors.Count; chunkIndex++)
					{
						Pass2CandidateEvalChunk chunk = chunkDescriptors[chunkIndex];
						long[] chunkIds = chunk.CandidateIds;
						for (int recordOffset = 0; recordOffset < chunk.RecordCount; recordOffset++)
						{
							int recordIndex = chunk.FirstRecordIndex + recordOffset;
							if ((uint)recordIndex >= (uint)pass2CandidateEvalRecords.Length)
								continue;
							if (pass2CandidateEvalRecords[recordIndex].CandidateCount > 0)
								pass2CandidateEvalRecords[recordIndex].CandidateStart += globalCandidateOffset;
						}
						if (chunkIds.Length > 0)
						{
							Array.Copy(chunkIds, 0, pass2CandidateEvalIds, globalCandidateOffset, chunkIds.Length);
							globalCandidateOffset += chunkIds.Length;
						}
						}
					}

					bool RunThreadedPass2QueryResolvePrototype()
					{
						if (!useThreadedPass2QueryResolve || pass2ResolvedSamples == null || pass2ChunkSampleStarts == null)
							return false;

						int yAlignedStart = yStart + ((stride - (yStart % stride)) % stride);
						var chunkDescriptors = new System.Collections.Generic.List<Pass2QueryResolveChunk>(Math.Max(1, (bandH + threadedPass2QueryRowsPerChunk - 1) / threadedPass2QueryRowsPerChunk));
						Pass2QueryResolveChunk? activeChunk = null;
						int sampledRowOrdinal = 0;
						for (int y = yAlignedStart; y < yEnd; y += stride, sampledRowOrdinal++)
						{
							if ((sampledRowOrdinal % threadedPass2QueryRowsPerChunk) == 0)
							{
								activeChunk = new Pass2QueryResolveChunk
								{
									StartRow = y,
									EndRowExclusive = Math.Min(yEnd, y + threadedPass2QueryRowsPerChunk * stride)
								};
								chunkDescriptors.Add(activeChunk);
							}
						}

						if (chunkDescriptors.Count == 0)
							return false;

						for (int y = yAlignedStart; y < yEnd; y += stride)
							RecordFixtureRowWriteStart(y);

						void ResolveChunk(int chunkIndex)
						{
							Pass2QueryResolveChunk chunk = chunkDescriptors[chunkIndex];
							var localSamples = new System.Collections.Generic.List<Pass2ResolvedSample>(Math.Max(16, (chunk.EndRowExclusive - chunk.StartRow) * Math.Max(1, filmW / Math.Max(1, stride))));
							var localOverlapCandidates = new System.Collections.Generic.List<Godot.Collections.Dictionary>(Math.Max(4, broadphaseCfg.MaxResults));
							var localOverlapQuery = new PhysicsShapeQueryParameters3D
							{
								Shape = _overlapQuery.Shape,
								CollisionMask = _overlapQuery.CollisionMask,
								CollideWithBodies = _overlapQuery.CollideWithBodies,
								CollideWithAreas = _overlapQuery.CollideWithAreas,
								Margin = _overlapQuery.Margin,
								Transform = _overlapQuery.Transform
							};
							void RecordLocalPass2Sample(float radius, float envDiag, float envelopeInflation, int candidateCount)
							{
								if (float.IsNaN(radius) || float.IsInfinity(radius))
									return;
								if (float.IsNaN(envDiag) || float.IsInfinity(envDiag))
									return;
								if (float.IsNaN(envelopeInflation) || float.IsInfinity(envelopeInflation))
									return;

								chunk.Geom.Pass2SampledSegments++;
								chunk.Geom.Pass2RadiusSum += radius;
								if (radius > chunk.Geom.Pass2RadiusMax) chunk.Geom.Pass2RadiusMax = radius;
								chunk.Geom.Pass2EnvDiagSum += envDiag;
								if (envDiag > chunk.Geom.Pass2EnvDiagMax) chunk.Geom.Pass2EnvDiagMax = envDiag;
								chunk.Geom.Pass2EnvelopeInflationSum += envelopeInflation;
								if (envelopeInflation > chunk.Geom.Pass2EnvelopeInflationMax) chunk.Geom.Pass2EnvelopeInflationMax = envelopeInflation;

								if (candidateCount <= 0) chunk.Geom.Pass2CandidateCount0++;
								else if (candidateCount <= 2) chunk.Geom.Pass2CandidateCount1To2++;
								else if (candidateCount <= 8) chunk.Geom.Pass2CandidateCount3To8++;
								else if (candidateCount <= 32) chunk.Geom.Pass2CandidateCount9To32++;
								else chunk.Geom.Pass2CandidateCount33Plus++;
							}
							ulong chunkPhysStart = statsEnabled ? Time.GetTicksUsec() : 0;

							for (int y = chunk.StartRow; y < chunk.EndRowExclusive; y += stride)
							{
								int localY = y - yStart;
								for (int execIndex = 0; execIndex < _tileMetricCurrentSubtileCount; execIndex++)
								{
									int subtileIndex = _tileMetricCurrentExecutionOrder[execIndex];
									int subtileXStart = subtileIndex * _tileMetricCurrentSubtileWidth;
									int subtileXEnd = Math.Max(0, Math.Min(filmW, subtileXStart + _tileMetricCurrentSubtileWidth));
									if (subtileXStart >= subtileXEnd)
										continue;
									int xAlignedStart = subtileXStart + ((stride - (subtileXStart % stride)) % stride);
									for (int x = xAlignedStart; x < subtileXEnd; x += stride)
									{
										int pi = localY * filmW + x;
										int globalPi = y * filmW + x;
										int segCount = _segCountPerPixel[pi];
										int segOffset = pi * maxSeg;
										bool pass1StoppedEarly = _pass1StoppedEarly[pi];
										int pass1HitSegIndex = _pass1HitSegIndex[pi];
										int segStart = 0;
										int segEnd = segCount - 1;
										if (pass1StoppedEarly && pass1HitSegIndex >= 0)
										{
											segStart = Math.Max(0, pass1HitSegIndex - 1);
											segEnd = Math.Min(segCount - 1, pass1HitSegIndex + 1);
										}

										bool prevHadHit = cfg.Pass2ForceOnInstability
											&& _pass2PrevHadHit.Length > globalPi
											&& _pass2PrevHadHit[globalPi] != 0;
										bool prevHadHitForSoftGate = _pass2PrevHadHit.Length > globalPi
											&& _pass2PrevHadHit[globalPi] != 0;
										bool testedAnyInPass0ThisPixel = false;
										bool skippedAnyByStrideThisPixel = false;
										bool geomPixelProcessedThisPixel = false;
										bool geomPixelHadAnyCandidatesThisPixel = false;
										float telemetryCandidateCountThisPixel = 0f;
										float telemetryQueryCountThisPixel = 0f;
										float telemetryResolveCountThisPixel = 0f;
										bool hadHit = false;
										float hitDistance = 0f;
										float bestHit = float.PositiveInfinity;
										Vector3 bestHp = Vector3.Zero;
										Vector3 bestHn = Vector3.Up;
										ulong bestCid = 0;
										string hitName = "<none>";
										bool needHitName = cfg.NeedColliderNames;
										bool absorbedByInnerRadius = false;
										bool segmentsMonotonic = true;
										if (segCount > 1)
										{
											float prevTraveledB = float.NegativeInfinity;
											for (int si = 0; si < segCount; si++)
											{
												float traveledB = _segBuf[segOffset + si].TraveledB;
												if (traveledB < prevTraveledB - 1e-6f)
												{
													segmentsMonotonic = false;
													break;
												}
												prevTraveledB = traveledB;
											}
										}
										bool allowFarEarlyOut = cfg.NearestHitOnly && segmentsMonotonic;
										float farEarlyOutEps = Mathf.Max(0f, cfg.EarlyOutDistanceEps);
										int lastSi = Math.Max(0, segCount - 1);
										int candidateRecordBaseForPixel = (uint)pi < (uint)pass2CandidateRecordStarts.Length
											? pass2CandidateRecordStarts[pi]
											: -1;
										int candidateVisitedSegStartForPixel = (uint)pi < (uint)pass2CandidateVisitedSegStarts.Length
											? pass2CandidateVisitedSegStarts[pi]
											: 0;

										chunk.TracedPixels++;
										chunk.SegsIntegrated += segCount;

										for (int si = segStart; si <= segEnd; si++)
										{
										if (allowFarEarlyOut && hadHit)
										{
											ref readonly var farSeg = ref _segBuf[segOffset + si];
											if (farSeg.TraveledB > bestHit + farEarlyOutEps)
												break;
										}

											ref readonly var seg = ref _segBuf[segOffset + si];
											Vector3 segA = seg.A;
											Vector3 segB = seg.B;
											float segLen = (segB - segA).Length();
											int pass2Stride = pass1StoppedEarly ? 1 : ComputePass2CollisionStride(seg.TraveledB, farForSim, in cfg);
											if (cfg.MinSegLenForStrideSkip > 0f && segLen < cfg.MinSegLenForStrideSkip)
											{
												chunk.Pass2StrideSum += 1;
												chunk.Pass2StrideCount++;
											}

											int candidateRecordIndex = candidateRecordBaseForPixel >= 0
												? candidateRecordBaseForPixel + (si - candidateVisitedSegStartForPixel)
												: -1;
											int geomCandidateCount = (uint)candidateRecordIndex < (uint)pass2CandidateEvalRecords.Length
												? pass2CandidateEvalRecords[candidateRecordIndex].CandidateCount
												: 0;
											if (geomCandidateCount > 0)
												telemetryCandidateCountThisPixel += geomCandidateCount;
											chunk.Geom.GeomSegmentsQueried++;
											if (geomCandidateCount <= 0)
												chunk.Geom.GeomSegZeroCandidates++;
											else
												chunk.Geom.GeomSegWithCandidates++;
											if ((uint)candidateRecordIndex < (uint)pass2CandidateEvalRecords.Length)
											{
												Pass2CandidateEvalRecord candidateRecord = pass2CandidateEvalRecords[candidateRecordIndex];
												RecordLocalPass2Sample(
													candidateRecord.EnvelopeRadius,
													candidateRecord.EnvelopeDiag,
													candidateRecord.EnvelopeInflation,
													geomCandidateCount);
											}
											if (geomCandidateCount <= 0)
												continue;
											geomPixelHadAnyCandidatesThisPixel = true;

											if (!pass1StoppedEarly && cfg.UsePass2CollisionStride && segCount > 1)
											{
												bool forceTest = si == 0 || si == lastSi
													|| (cfg.MinSegLenForStrideSkip > 0f && segLen < cfg.MinSegLenForStrideSkip);
												if (!forceTest && (si % Math.Max(1, pass2Stride)) != 0)
												{
													skippedAnyByStrideThisPixel = true;
													continue;
												}
											}

											testedAnyInPass0ThisPixel = true;
											chunk.Pass2StrideSum += pass2Stride;
											chunk.Pass2StrideCount++;

											Vector3 mid = (segA + segB) * 0.5f;
											localOverlapQuery.Transform = new Transform3D(Basis.Identity, mid);
											ulong overlapStartUsec = statsEnabled ? Time.GetTicksUsec() : 0;
											var overlaps = space.IntersectShape(localOverlapQuery, broadphaseCfg.MaxResults);
											if (statsEnabled && overlapStartUsec > 0)
												chunk.QueryUsec += (long)(Time.GetTicksUsec() - overlapStartUsec);
											telemetryQueryCountThisPixel += 1f;
											if (!geomPixelProcessedThisPixel)
											{
												chunk.Geom.GeomPixelProcessed++;
												geomPixelProcessedThisPixel = true;
											}
											chunk.IntersectShapeCalls++;
											chunk.PhysicsQueries++;
											chunk.SegsTested++;
											localOverlapCandidates.Clear();
											for (int oi = 0; oi < overlaps.Count; oi++)
												localOverlapCandidates.Add((Godot.Collections.Dictionary)overlaps[oi]);
											if (localOverlapCandidates.Count == 0)
												continue;

									if (rayCfg.UseSphereSweepCollision)
									{
												ulong queryStartUsec = statsEnabled ? Time.GetTicksUsec() : 0;
												bool didHitSweep = RayBeamRenderer.SweepSegmentHit(space, segA, segB, rayCfg.CollisionMask, rayCfg.CollisionRadius, out Vector3 hpSweep);
												if (statsEnabled && queryStartUsec > 0)
													chunk.QueryUsec += (long)(Time.GetTicksUsec() - queryStartUsec);
												telemetryQueryCountThisPixel += 1f;
												chunk.PhysicsQueries++;
												chunk.Geom.GeomRayTestsTotal++;
												if (!didHitSweep)
													continue;
												float hitDistAlongRay = seg.TraveledB - segLen + (hpSweep - segA).Length();
												telemetryResolveCountThisPixel += 1f;
												ulong resolveStartUsec = statsEnabled ? Time.GetTicksUsec() : 0;
												if (hitDistAlongRay < bestHit)
												{
													bestHit = hitDistAlongRay;
													hadHit = true;
													hitDistance = hitDistAlongRay;
													bestHp = hpSweep;
													bestHn = Vector3.Up;
													bestCid = 0;
												}
												if (statsEnabled && resolveStartUsec > 0)
													chunk.ResolveUsec += (long)(Time.GetTicksUsec() - resolveStartUsec);
												if (cfg.NearestHitOnly)
													break;
												continue;
											}

											int sub = 1;
											if (segLen > rayCfg.CollisionRaySubdivideThreshold)
												sub = Mathf.CeilToInt(segLen / rayCfg.CollisionRaySubdivideThreshold);
											sub = Mathf.Clamp(sub, 1, rayCfg.MaxCollisionSubsteps);
											if (cfg.UseAdaptiveSubsteps)
											{
												float far = cfg.AutoRangeDepth ? _rangeFar : cfg.Film.MaxDistance;
												float t = Mathf.Clamp(seg.TraveledB / Mathf.Max(0.001f, far), 0f, 1f);
												float minSub = Mathf.Max(1f, sub * 0.25f);
												float scaled = Mathf.Lerp(sub, minSub, t);
												sub = Mathf.Clamp(Mathf.RoundToInt(scaled), 1, rayCfg.MaxCollisionSubsteps);
											}

											ulong queryStart = statsEnabled ? Time.GetTicksUsec() : 0;
											bool didHit = RayBeamRenderer.SubdividedRayHit(
												space,
												segA,
												segB,
												rayCfg.CollisionMask,
												sub,
												out Vector3 hp,
												out Vector3 hn,
												out ulong cid,
												out string cname,
												out int rayQueries,
												includeColliderName: needHitName,
												hitBackFaces: pass2Flags.HitBackFaces,
												hitFromInside: pass2Flags.HitFromInside,
												diagnosticSceneName: renderSceneName,
												diagnosticFixtureName: renderFixtureName,
												diagnosticModeToken: renderModeToken,
												diagnosticQueryKind: "pass2_threaded_query_resolve");
												if (statsEnabled && queryStart > 0)
													chunk.QueryUsec += (long)(Time.GetTicksUsec() - queryStart);
												telemetryQueryCountThisPixel += Math.Max(1, rayQueries);
												chunk.SubdividedRayCalls++;
												chunk.SubdividedRayQueries += rayQueries;
												chunk.SubdividedRaySubsteps += sub;
												chunk.PhysicsQueries += rayQueries;
												chunk.Geom.GeomRayTestsTotal += rayQueries;
											if (!didHit)
												continue;

											float resolvedHitDistance = seg.TraveledB - segLen + (hp - segA).Length();
											telemetryResolveCountThisPixel += 1f;
											ulong resolveStart = statsEnabled ? Time.GetTicksUsec() : 0;
											if (resolvedHitDistance < bestHit)
											{
												bestHit = resolvedHitDistance;
												hadHit = true;
												hitDistance = resolvedHitDistance;
												bestHp = hp;
												bestHn = hn;
												bestCid = cid;
												if (needHitName)
													hitName = cname;
											}
											if (statsEnabled && resolveStart > 0)
												chunk.ResolveUsec += (long)(Time.GetTicksUsec() - resolveStart);
											if (cfg.NearestHitOnly)
												break;
										}

										if (!hadHit && cfg.FixtureDebugHitColoringEnabled && segCount > 0)
										{
											Vector3 terminalPoint = _segBuf[segOffset + (segCount - 1)].B;
											absorbedByInnerRadius = IsInsideAbsorbingSource(terminalPoint, fieldSnaps);
										}
										if (geomPixelHadAnyCandidatesThisPixel) chunk.Geom.GeomPixelHadAnyCandidates++;
										else chunk.Geom.GeomPixelNoCandidates++;
										if (hadHit) chunk.Geom.GeomHitAccepted++;

										localSamples.Add(new Pass2ResolvedSample
										{
											X = x,
											Y = y,
											Stride = stride,
											GlobalPi = globalPi,
											SubtileIndex = subtileIndex,
											SegCount = segCount,
											SegOffset = segOffset,
											HadHit = hadHit,
											AbsorbedByInnerRadius = absorbedByInnerRadius,
											NeedHitName = needHitName,
											PrevHadHit = prevHadHit,
											PrevHadHitForSoftGate = prevHadHitForSoftGate,
											TestedAnyInPass0ThisPixel = testedAnyInPass0ThisPixel,
											SoftGateHitThisPixel = false,
											CandidateCount = telemetryCandidateCountThisPixel,
											QueryCount = telemetryQueryCountThisPixel,
											ResolveCount = telemetryResolveCountThisPixel,
											HitDistance = hitDistance,
											BestHp = bestHp,
											BestHn = bestHn,
											BestCid = bestCid,
											HitName = needHitName ? hitName : string.Empty
										});
									}
								}
							}

							chunk.Samples = localSamples;
							if (statsEnabled && chunkPhysStart > 0)
								chunk.PhysUsec = (long)(Time.GetTicksUsec() - chunkPhysStart);
							chunkDescriptors[chunkIndex] = chunk;
						}

						if (threadedPass2QueryWorkers <= 1 || chunkDescriptors.Count <= 1)
						{
							for (int chunkIndex = 0; chunkIndex < chunkDescriptors.Count; chunkIndex++)
								ResolveChunk(chunkIndex);
						}
						else
						{
							var pass2QueryOptions = new System.Threading.Tasks.ParallelOptions
							{
								MaxDegreeOfParallelism = threadedPass2QueryWorkers
							};
							System.Threading.Tasks.Parallel.For(0, chunkDescriptors.Count, pass2QueryOptions, ResolveChunk);
						}

						pass2ChunkSampleStarts.Clear();
							for (int chunkIndex = 0; chunkIndex < chunkDescriptors.Count; chunkIndex++)
							{
								Pass2QueryResolveChunk chunk = chunkDescriptors[chunkIndex];
								pass2ChunkSampleStarts.Add(pass2ResolvedSamples.Count);
							if (chunk.Samples.Count > 0)
								pass2ResolvedSamples.AddRange(chunk.Samples);
							bandTracedPixels += (int)chunk.TracedPixels;
							processedPixelsThisBand += (int)chunk.TracedPixels;
							processedPixelsThisStep += (int)chunk.TracedPixels;
							bandSegsIntegrated += chunk.SegsIntegrated;
							bandSegsTested += chunk.SegsTested;
							bandPhysicsQueries += chunk.PhysicsQueries;
							pass2QueryUsecAccum += chunk.QueryUsec;
							pass2HitResolveUsecAccum += chunk.ResolveUsec;
								if (statsEnabled)
								{
									_perfFrame.TracedPixels += (int)chunk.TracedPixels;
								_perfFrame.Segs += (int)chunk.SegsIntegrated;
								_perfFrame.SegsTested += (int)chunk.SegsTested;
								_perfFrame.IntersectShapeCalls += (int)chunk.IntersectShapeCalls;
								_perfFrame.SubdividedRayCalls += (int)chunk.SubdividedRayCalls;
								_perfFrame.SubdividedRayQueries += (int)chunk.SubdividedRayQueries;
								_perfFrame.SubdividedRaySubsteps += (int)chunk.SubdividedRaySubsteps;
								_perfFrame.Pass2StrideSum += chunk.Pass2StrideSum;
								_perfFrame.Pass2StrideCount += chunk.Pass2StrideCount;
									if (chunk.PhysUsec > 0)
										_perfFrame.AddPass2PhysUsec((ulong)chunk.PhysUsec);
								}
								_geomSegmentsQueriedThisFrame += chunk.Geom.GeomSegmentsQueried;
								_geomSegWithCandidatesThisFrame += chunk.Geom.GeomSegWithCandidates;
								_geomSegZeroCandidatesThisFrame += chunk.Geom.GeomSegZeroCandidates;
								_geomPixelProcessedThisFrame += chunk.Geom.GeomPixelProcessed;
								_geomPixelHadAnyCandidatesThisFrame += chunk.Geom.GeomPixelHadAnyCandidates;
								_geomPixelNoCandidatesThisFrame += chunk.Geom.GeomPixelNoCandidates;
								_geomHitAcceptedThisFrame += chunk.Geom.GeomHitAccepted;
								_geomHitRejectedThisFrame += chunk.Geom.GeomHitRejected;
								_geomRayTestsTotalThisFrame += chunk.Geom.GeomRayTestsTotal;
								pass2SampledSegments += chunk.Geom.Pass2SampledSegments;
								pass2RadiusSum += chunk.Geom.Pass2RadiusSum;
								if (chunk.Geom.Pass2RadiusMax > pass2RadiusMax) pass2RadiusMax = chunk.Geom.Pass2RadiusMax;
								pass2EnvDiagSum += chunk.Geom.Pass2EnvDiagSum;
								if (chunk.Geom.Pass2EnvDiagMax > pass2EnvDiagMax) pass2EnvDiagMax = chunk.Geom.Pass2EnvDiagMax;
								pass2EnvelopeInflationSum += chunk.Geom.Pass2EnvelopeInflationSum;
								if (chunk.Geom.Pass2EnvelopeInflationMax > pass2EnvelopeInflationMax) pass2EnvelopeInflationMax = chunk.Geom.Pass2EnvelopeInflationMax;
								pass2CandidateCount0 += chunk.Geom.Pass2CandidateCount0;
								pass2CandidateCount1To2 += chunk.Geom.Pass2CandidateCount1To2;
								pass2CandidateCount3To8 += chunk.Geom.Pass2CandidateCount3To8;
								pass2CandidateCount9To32 += chunk.Geom.Pass2CandidateCount9To32;
								pass2CandidateCount33Plus += chunk.Geom.Pass2CandidateCount33Plus;
								for (int y = chunk.StartRow; y < chunk.EndRowExclusive; y += stride)
									RecordFixtureRowWriteOutcome(y, rowHadWrites: true, rowCompleted: true);
							}

						RunThreadedPass2LocalAccumulationCommit();
						return true;
					}

					void RunThreadedPass2LocalAccumulationCommit()
					{
					if (!useThreadedPass2ResolvedSampleCommit || pass2ResolvedSamples == null || pass2ResolvedSamples.Count == 0)
						return;

					if (pass2ChunkSampleStarts == null || pass2ChunkSampleStarts.Count == 0)
						pass2ChunkSampleStarts = new System.Collections.Generic.List<int> { 0 };
					pass2ChunkSampleStarts.Add(pass2ResolvedSamples.Count);

					int chunkCount = Math.Max(0, pass2ChunkSampleStarts.Count - 1);
					if (chunkCount <= 0)
						return;

					Pass2ShadedSample[] shadedSamples = new Pass2ShadedSample[pass2ResolvedSamples.Count];
					Pass2ChunkAccumulator[] chunkAccums = new Pass2ChunkAccumulator[chunkCount];
					ulong shadeStartUsec = 0;
					if (shadeTimingEnabled)
						shadeStartUsec = Time.GetTicksUsec();

					void ShadeChunk(int chunkIndex)
					{
						int startIndex = pass2ChunkSampleStarts[chunkIndex];
						int endIndex = pass2ChunkSampleStarts[chunkIndex + 1];
						var accum = new Pass2ChunkAccumulator();

						for (int sampleIndex = startIndex; sampleIndex < endIndex; sampleIndex++)
						{
							Pass2ResolvedSample sample = pass2ResolvedSamples[sampleIndex];
							Color col = cfg.SkyColor;
							string fixtureHitKind = "miss";
							Color fixtureChosenDebugColor = cfg.SkyColor;
							bool fixtureDebugColorChosen = false;
							bool skipShading = rayCfg.RequireHitToRender && !sample.HadHit;

							if (skipShading)
							{
								accum.ShadingSkippedPixels++;
							}
							else if (sample.HadHit)
							{
								accum.Hits++;
								switch (cfg.ShadingMode)
								{
									default:
									case FilmShadingMode.DepthHeatmap:
									{
										float far = cfg.AutoRangeDepth ? _rangeFar : cfg.Film.MaxDistance;
										float d = Mathf.Clamp(sample.HitDistance / Mathf.Max(0.001f, far), 0f, 1f);
										col = Color.FromHsv(0.66f * (1f - d), 1f, 1f);
										break;
									}
									case FilmShadingMode.NormalRGB:
									{
										Vector3 n = sample.BestHn;
										if (cfg.FlipNormalToCamera)
										{
											Vector3 v = (camPosPass2 - sample.BestHp).Normalized();
											if (n.Dot(v) < 0f) n = -n;
										}
										col = ShadeNormalRGB(n);
										break;
									}
									case FilmShadingMode.NdotV:
									{
										Vector3 v = camPosPass2 - sample.BestHp;
										Vector3 n = sample.BestHn;
										col = ShadeNdotV(n, v, out _);
										if (cfg.FlipNormalToCamera)
										{
											Vector3 vn = (camPosPass2 - sample.BestHp).Normalized();
											if (n.Dot(vn) < 0f)
											{
												n = -n;
												col = ShadeNdotV(n, v, out _);
											}
										}
										break;
									}
									case FilmShadingMode.TwoSidedNdotV:
									{
										Vector3 v = (camPosPass2 - sample.BestHp).Normalized();
										Vector3 n = sample.BestHn.Normalized();
										float ndv = n.Dot(v);
										col = ShadeNdotVAbs(ndv);
										break;
									}
								}
							}

							fixtureHitKind = ClassifyFixtureHitKind(sample.HadHit, sample.AbsorbedByInnerRadius, sample.BestCid);
							if (cfg.FixtureDebugHitColoringEnabled)
							{
								if (fixtureHitKind == "source")
								{
									if (cfg.FixtureDebugSourceHighlightEnabled)
									{
										col = cfg.FixtureDebugSourceHitColor;
										fixtureChosenDebugColor = col;
										fixtureDebugColorChosen = true;
									}
									else if (cfg.FixtureDebugColorAuthorityEnabled)
									{
										col = cfg.FixtureDebugBackgroundHitColor;
										fixtureChosenDebugColor = col;
										fixtureDebugColorChosen = true;
									}
								}
								else if (fixtureHitKind == "background")
								{
									if (cfg.FixtureDebugColorAuthorityEnabled)
									{
										col = cfg.FixtureDebugBackgroundHitColor;
										fixtureChosenDebugColor = col;
										fixtureDebugColorChosen = true;
									}
								}
								else if (fixtureHitKind == "absorbed")
								{
									col = cfg.FixtureDebugAbsorbedColor;
									fixtureChosenDebugColor = col;
									fixtureDebugColorChosen = true;
								}
								else
								{
									col = cfg.FixtureDebugMissColor;
									fixtureChosenDebugColor = col;
									fixtureDebugColorChosen = true;
								}
							}

							switch (fixtureHitKind)
							{
								case "source":
									accum.SourceHits++;
									break;
								case "background":
									accum.BackgroundHits++;
									break;
								case "unclassified":
									accum.UnclassifiedHits++;
									break;
								case "absorbed":
									accum.AbsorbedHits++;
									break;
								default:
									accum.MissHits++;
									break;
							}

							shadedSamples[sampleIndex] = new Pass2ShadedSample
							{
								Color = col,
								FixtureHitKind = fixtureHitKind,
								FixtureChosenDebugColor = fixtureChosenDebugColor,
								FixtureDebugColorChosen = fixtureDebugColorChosen,
								SkipShading = skipShading
							};
						}

						chunkAccums[chunkIndex] = accum;
					}

						int pass2ShadeWorkers = useThreadedPass2QueryResolve ? threadedPass2QueryWorkers : threadedPass2WorkerCount;
						if (pass2ShadeWorkers <= 1 || chunkCount <= 1)
						{
							for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
								ShadeChunk(chunkIndex);
					}
					else
					{
							var pass2ChunkOptions = new System.Threading.Tasks.ParallelOptions
							{
								MaxDegreeOfParallelism = pass2ShadeWorkers
							};
						System.Threading.Tasks.Parallel.For(0, chunkCount, pass2ChunkOptions, ShadeChunk);
					}

					if (shadeTimingEnabled && shadeStartUsec > 0)
					{
						ulong shadeUsec = Time.GetTicksUsec() - shadeStartUsec;
						if (statsEnabled) _perfFrame.AddPass2ShadeUsec(shadeUsec);
						shadeUsecAccum += (long)shadeUsec;
					}

					ulong commitStart = 0;
					if (statsEnabled) commitStart = Time.GetTicksUsec();

					for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
					{
						Pass2ChunkAccumulator accum = chunkAccums[chunkIndex] ?? new Pass2ChunkAccumulator();
						if (statsEnabled)
							_perfFrame.ShadingSkippedPixels += accum.ShadingSkippedPixels;

						int startIndex = pass2ChunkSampleStarts[chunkIndex];
						int endIndex = pass2ChunkSampleStarts[chunkIndex + 1];
						for (int sampleIndex = startIndex; sampleIndex < endIndex; sampleIndex++)
						{
							Pass2ResolvedSample sample = pass2ResolvedSamples[sampleIndex];
							Pass2ShadedSample shaded = shadedSamples[sampleIndex];

							if (sample.HadHit)
							{
								bandHits++;
								_tileMetricCurrentSubtiles[sample.SubtileIndex].Hits++;
								if (sample.HitDistance > frameMaxHit) frameMaxHit = sample.HitDistance;
							}

							switch (shaded.FixtureHitKind)
							{
								case "source":
									_fixtureDebugSourceHitsThisRun++;
									break;
								case "background":
									_fixtureDebugBackgroundHitsThisRun++;
									break;
								case "unclassified":
									_fixtureDebugUnclassifiedHitsThisRun++;
									break;
								case "absorbed":
									_fixtureDebugAbsorbedHitsThisRun++;
									break;
								default:
									_fixtureDebugMissHitsThisRun++;
									break;
							}

							if (sample.HadHit)
							{
								switch (shaded.FixtureHitKind)
								{
									case "source":
										_tileMetricCurrentSubtiles[sample.SubtileIndex].SourceHits++;
										break;
									case "background":
										_tileMetricCurrentSubtiles[sample.SubtileIndex].BackgroundHits++;
										break;
									case "unclassified":
										_tileMetricCurrentSubtiles[sample.SubtileIndex].UnclassifiedHits++;
										break;
								}
							}

							int filled = FillPixelBlock(sample.X, sample.Y, sample.Stride, shaded.Color, filmW, filmH);
							if (telemetryHeatmapsEnabled)
							{
								AccumulateTelemetryBlock(_telemetryCandidateCount, sample.X, sample.Y, sample.Stride, sample.CandidateCount);
								AccumulateTelemetryBlock(_telemetryQueryCount, sample.X, sample.Y, sample.Stride, sample.QueryCount);
								AccumulateTelemetryBlock(_telemetryResolveCount, sample.X, sample.Y, sample.Stride, sample.ResolveCount);
							}
							if (statsEnabled) _perfFrame.FilledPixels += filled;
							if (framePerfEnabled) bandFilledPixels += filled;
							if (sample.HadHit)
							{
								_fixtureFinalHitPixelCountThisRun += filled;
								FillPixelBlock(_fixtureFinalHitOnlyImg, sample.X, sample.Y, sample.Stride, shaded.Color, filmW, filmH);
							}
							Color categoricalColor = sample.HadHit
								? FixtureCategoricalFinalHitColor
								: FixtureCategoricalRenderedNoHitColor;
							FillPixelBlock(_fixtureCategoricalFinalImg, sample.X, sample.Y, sample.Stride, categoricalColor, filmW, filmH);

							bool fixtureTraceByKind = false;
							if (cfg.FixtureDebugTraceEnabled)
							{
								switch (shaded.FixtureHitKind)
								{
									case "source":
										if (fixtureTraceSourceRemaining > 0)
										{
											fixtureTraceSourceRemaining--;
											fixtureTraceByKind = true;
										}
										break;
									case "background":
										if (fixtureTraceBackgroundRemaining > 0)
										{
											fixtureTraceBackgroundRemaining--;
											fixtureTraceByKind = true;
										}
										break;
									case "absorbed":
										if (fixtureTraceAbsorbedRemaining > 0)
										{
											fixtureTraceAbsorbedRemaining--;
											fixtureTraceByKind = true;
										}
										break;
									default:
										if (fixtureTraceMissRemaining > 0)
										{
											fixtureTraceMissRemaining--;
											fixtureTraceByKind = true;
										}
										break;
								}
							}
							bool fixtureTraceByModulo = cfg.FixtureDebugTraceEnabled
								&& fixtureDebugTraceLogsRemainingThisStep > 0
								&& ShouldTraceFixtureDebugSample(sample.X, sample.Y, cfg.FixtureDebugTraceSampleModulo);
							if (cfg.FixtureDebugTraceEnabled && (fixtureTraceByKind || fixtureTraceByModulo))
							{
								Color finalWrittenColor = _img.GetPixel(sample.X, sample.Y);
								GD.Print(
									$"[FixtureDebugTrace] frame={_frameIndex} row={sample.Y} x={sample.X} kind={shaded.FixtureHitKind} hadHit={(sample.HadHit ? 1 : 0)} " +
									$"cid={sample.BestCid} debugChosen={FormatColorCompact(shaded.FixtureChosenDebugColor)} " +
									$"finalWritten={FormatColorCompact(finalWrittenColor)} auth={(cfg.FixtureDebugColorAuthorityEnabled ? 1 : 0)} " +
									$"chosen={(shaded.FixtureDebugColorChosen ? 1 : 0)}");
								if (fixtureDebugTraceLogsRemainingThisStep > 0)
									fixtureDebugTraceLogsRemainingThisStep--;
							}

							if (_pass2PrevHadHit.Length > sample.GlobalPi)
							{
								bool prevHit = sample.PrevHadHit;
								bool nowHit = sample.HadHit;
								if (prevHit != nowHit) pixelDeltaChanged++;
								if (!prevHit && nowHit) pixelDeltaNewFilled++;
								if (!prevHit && nowHit && sample.SoftGateHitThisPixel) softGateNewPixelFilled++;
								_pass2PrevHadHit[sample.GlobalPi] = sample.HadHit ? (byte)1 : (byte)0;
							}
							if (_pass2HadHitLostThisFrame.Length > sample.GlobalPi)
								_pass2HadHitLostThisFrame[sample.GlobalPi] = (sample.PrevHadHitForSoftGate && !sample.HadHit && sample.TestedAnyInPass0ThisPixel) ? (byte)1 : (byte)0;

							if (wantDbg)
							{
								ulong dbgStart = 0;
								if (statsEnabled) dbgStart = Time.GetTicksUsec();
								int pxStride = Math.Max(1, cfg.DebugEveryNPixels);
								if ((sample.X % pxStride) == 0 && (sample.Y % pxStride) == 0 && _dbgRayCount < cfg.DebugMaxFilmRays)
								{
									int rayIndex = _dbgRayCount++;
									_dbgOff[rayIndex] = _dbgPtWrite;
									int w0 = _dbgPtWrite;
									if (sample.SegCount > 0)
									{
										_dbgPts[_dbgPtWrite++] = _segBuf[sample.SegOffset + 0].A;
										int writeSegs = Math.Min(sample.SegCount, maxSeg);
										for (int si2 = 0; si2 < writeSegs; si2++)
											_dbgPts[_dbgPtWrite++] = _segBuf[sample.SegOffset + si2].B;
									}
									else
									{
										_dbgPts[_dbgPtWrite++] = _cam.GlobalPosition;
										_dbgPts[_dbgPtWrite++] = _cam.GlobalPosition + (-_cam.GlobalTransform.Basis.Z) * 0.25f;
									}
									_dbgCnt[rayIndex] = _dbgPtWrite - w0;
									_dbgHits[rayIndex] = new RayBeamRenderer.HitPayload
									{
										Valid = sample.HadHit,
										Position = sample.BestHp,
										Normal = sample.BestHn,
										Distance = sample.HitDistance,
										ColliderId = sample.BestCid,
										ColliderName = sample.NeedHitName ? sample.HitName : "<none>",
										Albedo = Colors.White
									};
								}
								if (statsEnabled)
								{
									ulong dbgEnd = Time.GetTicksUsec();
									_perfFrame.AddOverlayBuildUsec(dbgEnd - dbgStart);
								}
							}
						}
					}

					if (statsEnabled)
						_perfFrame.AddPass2CommitUsec(Time.GetTicksUsec() - commitStart);
				}

					if (useThreadedPass2CandidateEval)
						PrepareThreadedPass2CandidateEval();

					if (RunThreadedPass2QueryResolvePrototype())
					{
						pass2CompletedThisStep = !budgetStop && !renderStepAbort;
					}

					// DECISION: skip physics if band-level skip is active.
					else if (skipBandPhysics)
				{
				ulong shadeStart = 0;
				// DECISION: capture shade timing only when enabled.
				if (shadeTimingEnabled) shadeStart = Time.GetTicksUsec();

					int yAlignedStart = yStart + ((stride - (yStart % stride)) % stride);
					for (int y = yAlignedStart; y < yEnd; y += stride)
					{
						if (useThreadedPass2LocalAccumulation)
						{
							int sampledRowIndex = (y - yAlignedStart) / Math.Max(1, stride);
							if (sampledRowIndex % threadedPass2RowsPerChunk == 0)
								pass2ChunkSampleStarts!.Add(pass2ResolvedSamples!.Count);
						}
						budgetStopRowCursor = y;
						RecordFixtureRowWriteStart(y);
					bool rowHadWritesThisPass = false;
					bool rowCompletedThisPass = false;
					// DECISION: watchdog may trigger budget stop or abort.
					if (CheckRenderStepWatchdog())
					{
						// DECISION: stop loop if budget stop was triggered.
						if (budgetStop) break;
						renderStepAbort = true;
						break;
					}
					int localY = y - yStart;
					for (int execIndex = 0; execIndex < _tileMetricCurrentSubtileCount; execIndex++)
					{
						int subtileIndex = _tileMetricCurrentExecutionOrder[execIndex];
						int subtileXStart = subtileIndex * _tileMetricCurrentSubtileWidth;
						int subtileXEnd = Math.Max(0, Math.Min(filmW, subtileXStart + _tileMetricCurrentSubtileWidth));
						if (subtileXStart >= subtileXEnd)
							continue;
						int xAlignedStart = subtileXStart + ((stride - (subtileXStart % stride)) % stride);
						for (int x = xAlignedStart; x < subtileXEnd; x += stride)
						{
						// DECISION: stop inner loop when budget stop is active.
						if (budgetStop) break;
						// DECISION: periodic watchdog check within row.
						if ((x & 31) == 0 && CheckRenderStepWatchdog())
						{
							// DECISION: stop inner loop if budget stop was triggered.
							if (budgetStop) break;
							renderStepAbort = true;
							break;
						}
						CheckPixelLoopGuard(x, y);
						if (pixelLoopGuardTripped)
						{
							renderStepAbort = true;
							break;
						}
						// DECISION: update perf stats when enabled.
						if (statsEnabled)
						{
							int pi = localY * filmW + x;
							_perfFrame.Segs += _segCountPerPixel[pi];
							// DECISION: count shading skipped pixels when RequireHitToRender is active.
							if (rayCfg.RequireHitToRender) _perfFrame.ShadingSkippedPixels++;
							_perfFrame.TracedPixels++;
						}
						// DECISION: update band counters when enabled.
						if (bandCountersEnabled)
						{
							int pi = localY * filmW + x;
							bandSegsIntegrated += _segCountPerPixel[pi];
						}
						bandTracedPixels++;
						processedPixelsThisBand++;
						processedPixelsThisStep++;
						int filled = FillPixelBlock(x, y, stride, cfg.SkyColor, filmW, filmH);
						rowHadWritesThisPass |= filled > 0;
						// DECISION: count filled pixels when stats enabled.
						if (statsEnabled) _perfFrame.FilledPixels += filled;
						// DECISION: count filled pixels for band when frame perf enabled.
						if (framePerfEnabled) bandFilledPixels += filled;
					}
						if (budgetStop || renderStepAbort)
							break;
					}
					rowCompletedThisPass = !renderStepAbort && !budgetStop;
					RecordFixtureRowWriteOutcome(y, rowHadWritesThisPass, rowCompletedThisPass);
					// DECISION: stop when abort or budget stop is active.
					if (renderStepAbort || budgetStop) break;
				}

				// DECISION: accumulate shade timing when enabled.
				if (shadeTimingEnabled)
				{
					ulong shadeUsec = Time.GetTicksUsec() - shadeStart;
					if (statsEnabled) _perfFrame.AddPass2ShadeUsec(shadeUsec);
					shadeUsecAccum += (long)shadeUsec;
				}
			}
			else
			{
				int yAlignedStart = yStart + ((stride - (yStart % stride)) % stride);
				for (int y = yAlignedStart; y < yEnd; y += stride)
				{
					if (useThreadedPass2LocalAccumulation)
					{
						int sampledRowIndex = (y - yAlignedStart) / Math.Max(1, stride);
						if (sampledRowIndex % threadedPass2RowsPerChunk == 0)
							pass2ChunkSampleStarts!.Add(pass2ResolvedSamples!.Count);
					}
					budgetStopRowCursor = y;
					RecordFixtureRowWriteStart(y);
					bool rowHadWritesThisPass = false;
					bool rowCompletedThisPass = false;
					// DECISION: watchdog may trigger budget stop or abort.
					if (CheckRenderStepWatchdog())
					{
						// DECISION: stop loop if budget stop was triggered.
						if (budgetStop) break;
						renderStepAbort = true;
						break;
					}
					int localY = y - yStart;
					for (int execIndex = 0; execIndex < _tileMetricCurrentSubtileCount; execIndex++)
					{
						int subtileIndex = _tileMetricCurrentExecutionOrder[execIndex];
						int subtileXStart = subtileIndex * _tileMetricCurrentSubtileWidth;
						int subtileXEnd = Math.Max(0, Math.Min(filmW, subtileXStart + _tileMetricCurrentSubtileWidth));
						if (subtileXStart >= subtileXEnd)
							continue;
						int xAlignedStart = subtileXStart + ((stride - (subtileXStart % stride)) % stride);
						for (int x = xAlignedStart; x < subtileXEnd; x += stride)
						{
						// DECISION: stop inner loop when budget stop is active.
						if (budgetStop) break;
						// DECISION: periodic watchdog check within row.
						if ((x & 31) == 0 && CheckRenderStepWatchdog())
						{
							// DECISION: stop inner loop if budget stop was triggered.
							if (budgetStop) break;
							renderStepAbort = true;
							break;
						}
						CheckPixelLoopGuard(x, y);
						if (pixelLoopGuardTripped)
						{
							renderStepAbort = true;
							break;
						}
						int pi = localY * filmW + x;
						int globalPi = y * filmW + x;
						int subtileIndexThisPixel = subtileIndex;
						// DECISION: update traced pixels when stats enabled.
						if (statsEnabled) _perfFrame.TracedPixels++;
						bandTracedPixels++;
						_tileMetricCurrentSubtiles[subtileIndexThisPixel].RaysTraced++;
						processedPixelsThisBand++;
						processedPixelsThisStep++;

						// DECISION: previous-hit flag for instability probes.
						bool prevHadHit = cfg.Pass2ForceOnInstability
							&& _pass2PrevHadHit.Length > globalPi
							&& _pass2PrevHadHit[globalPi] != 0;
						// DECISION: previous-hit flag for soft gate scoring.
						bool prevHadHitForSoftGate = _pass2PrevHadHit.Length > globalPi
							&& _pass2PrevHadHit[globalPi] != 0;
						// DECISION: reset "hit lost this frame" flag when in bounds.
						if (_pass2HadHitLostThisFrame.Length > globalPi)
							_pass2HadHitLostThisFrame[globalPi] = 0;
						bool quickRayTestedThisPixel = false;
						bool quickRayHitThisPixel = false;
							bool forceInstabilityThisPixel = false;
							bool forcePrevHitLostThisPixel = false;
							int forceRepSegIndex = -1;
							bool softGateHitThisPixel = false;
							bool softGateWatchdogTrippedThisPixel = false;
						bool hadCandidatesThisPixel = false;
						bool geomPixelHadAnyCandidatesThisPixel = false;
						bool noCandidatesThisPixel = false;
						float telemetryCandidateCountThisPixel = 0f;
						float telemetryQueryCountThisPixel = 0f;
						float telemetryResolveCountThisPixel = 0f;
						long geomRayTestsAtPixelStart = _geomRayTestsTotalThisFrame;
						bool useHybridBroadphase = broadphaseCfg.Policy == BroadphasePolicyMode.HybridQuickRayThenOverlap;
						var geomTlas = geomTlasForStep;
						var geomEntities = geomEntitiesForStep;
						bool useGeomTlasPruning = useGeomTlasPruningForStep;
						bool geomPixelWorkCountedThisPixel = false;
						void MarkGeomPixelProcessedForWork()
						{
							// Keep geomPixProcessed mode-agnostic: count the first actual geometry query attempt per pixel.
							// This avoids prune-OFF windows staying at zero solely because prune-ON entry gating was used.
							if (geomPixelWorkCountedThisPixel)
								return;
							_geomPixelProcessedThisFrame++;
							_tileMetricCurrentSubtiles[subtileIndexThisPixel].GeomPixelsProcessed++;
							geomPixelWorkCountedThisPixel = true;
						}

						bool hadHit = false;
						float hitDistance = 0f;
						string hitName = "<none>";
						float bestHit = float.PositiveInfinity;
						float bestHitDistAlongRay = float.PositiveInfinity;
						Vector3 bestHp = Vector3.Zero;
						Vector3 bestHn = Vector3.Up;
						ulong bestCid = 0;
						bool absorbedByInnerRadius = false;

						int segCount = _segCountPerPixel[pi];
						int segOffset = pi * maxSeg;
						bool pass1StoppedEarly = _pass1StoppedEarly[pi];
						int pass1HitSegIndex = _pass1HitSegIndex[pi];
						int segStart = 0;
						int segEnd = segCount - 1;
						// DECISION: narrow segment scan around pass1 hit if pass1 stopped early.
						if (pass1StoppedEarly && pass1HitSegIndex >= 0)
						{
							segStart = Math.Max(0, pass1HitSegIndex - 1);
							segEnd = Math.Min(segCount - 1, pass1HitSegIndex + 1);
						}
						int candidateRecordBaseForPixel = useThreadedPass2CandidateEval && (uint)pi < (uint)pass2CandidateRecordStarts.Length
							? pass2CandidateRecordStarts[pi]
							: -1;
						int candidateVisitedSegStartForPixel = useThreadedPass2CandidateEval && (uint)pi < (uint)pass2CandidateVisitedSegStarts.Length
							? pass2CandidateVisitedSegStarts[pi]
							: 0;

						// DECISION: update segment counts when stats enabled.
						if (statsEnabled) _perfFrame.Segs += segCount;
						// DECISION: update band segment counts when enabled.
						if (bandCountersEnabled) bandSegsIntegrated += segCount;

						bool isCenterSample = (x == filmW / 2 && y == (yStart + (bandH / 2)));
						// DECISION: log center sample only when verbose perf logs enabled.
						bool logCenterSample = cfg.VerbosePerfLogs && isCenterSample;
						bool needHitName = cfg.NeedColliderNames || logCenterSample;
						bool testedAnyInPass0ThisPixel = false;
						bool skippedAnyByStrideThisPixel = false;
						int softGateAttemptsThisPixel = 0;
						int softGateSubdividesThisPixel = 0;
						bool segmentsMonotonic = true;
						if (segCount > 1)
						{
							float prevTraveledB = float.NegativeInfinity;
							for (int si = 0; si < segCount; si++)
							{
								float traveledB = _segBuf[segOffset + si].TraveledB;
								if (traveledB < prevTraveledB - 1e-6f)
								{
									segmentsMonotonic = false;
									break;
								}
								prevTraveledB = traveledB;
							}
						}
						bool allowFarEarlyOut = cfg.NearestHitOnly && segmentsMonotonic;
						float farEarlyOutEps = Mathf.Max(0f, cfg.EarlyOutDistanceEps);
						bool earlyOutFarThisPixel = false;

						ulong physStart = 0;
						if (statsEnabled) physStart = Time.GetTicksUsec();

						int lastSi = Math.Max(0, segCount - 1);
						for (int pass = 0; pass < 2; pass++)
						{
							bool forceStride1 = pass1StoppedEarly || pass == 1;
							bool allowInstabilityPass = pass == 1 && forceInstabilityThisPixel;
							if (pass1StoppedEarly && pass == 1 && !forceInstabilityThisPixel)
								break;
							if (forceStride1 && !pass1StoppedEarly)
							{
								if (hadHit)
									break;
								if (!allowInstabilityPass)
								{
									if (!cfg.UsePass2CollisionStride || !skippedAnyByStrideThisPixel || testedAnyInPass0ThisPixel)
										break;
									if (statsEnabled) _perfFrame.Pass2ForceStride1Pixels++;
								}
							}

							Vector3 lastSegDir = Vector3.Zero;
							for (int si = segStart; si <= segEnd; si++)
							{
								var seg = _segBuf[segOffset + si];
								Vector3 segA = seg.A;
								Vector3 segB = seg.B;
								Vector3 segDelta = segB - segA;
								float segLen = segDelta.Length();
								Vector3 prevSegDir = lastSegDir;
								Vector3 currSegDir = segDelta.Normalized();
								lastSegDir = currSegDir;
								int pass2Stride = forceStride1 ? 1 : ComputePass2CollisionStride(seg.TraveledB, farForSim, in cfg);

								if (segLen <= 1e-6f) continue;
								if (cfg.TinySegmentSkipLen > 0f && segLen < cfg.TinySegmentSkipLen) continue;
								if (allowFarEarlyOut && bestHitDistAlongRay < float.PositiveInfinity)
								{
									float segStartDist = seg.TraveledB - segLen;
									if (segStartDist > bestHitDistAlongRay + farEarlyOutEps)
									{
										earlyOutFarThisPixel = true;
										bandFarEarlyOuts++;
										if (framePerfEnabled) _framePerf.Pass2Skip_BestHitDist++;
										EarlyOut("far early-out", cfg.EnableProfiling);
										break;
									}
								}

								bool renderHealthSampleThisSeg = pass == 0
									&& (_renderHealthPass2SampleCounter++ % renderHealthPass2SampleEveryForStep) == 0;
								bool renderHealthSampleRecorded = false;
								float renderHealthSampleRadius = 0f;
								float renderHealthSampleEnvDiag = 0f;
								float renderHealthSampleEnvelopeInflation = 0f;
								Aabb3 envelope = default;
								bool envelopeComputed = false;
								int candidateRecordIndex = useThreadedPass2CandidateEval && candidateRecordBaseForPixel >= 0
									? candidateRecordBaseForPixel + (si - candidateVisitedSegStartForPixel)
									: -1;
								if (useThreadedPass2CandidateEval
									&& (uint)candidateRecordIndex < (uint)pass2CandidateEvalRecords.Length)
								{
									Pass2CandidateEvalRecord candidateRecord = pass2CandidateEvalRecords[candidateRecordIndex];
									envelopeComputed = true;
									renderHealthSampleRadius = candidateRecord.EnvelopeRadius;
									renderHealthSampleEnvDiag = candidateRecord.EnvelopeDiag;
									renderHealthSampleEnvelopeInflation = candidateRecord.EnvelopeInflation;
								}
								else if (useGeomTlasPruning || renderHealthSampleThisSeg)
								{
									ulong envelopeStartUsec = 0;
									if (statsEnabled) envelopeStartUsec = Time.GetTicksUsec();
									var segANum = new System.Numerics.Vector3(segA.X, segA.Y, segA.Z);
									var segBNum = new System.Numerics.Vector3(segB.X, segB.Y, segB.Z);
									float baseRadiusBound = Mathf.Max(0f, seg.RadiusBound);
									float localEnvelopeScale = (uint)subtileIndexThisPixel < (uint)adaptiveEnvelopeScaleBySubtile.Length
										? adaptiveEnvelopeScaleBySubtile[subtileIndexThisPixel]
										: Mathf.Max(0.1f, cfg.Pass2GeomEnvelopeRadiusScale);
									float geomEnvelopeRadius = baseRadiusBound * Mathf.Max(0.1f, localEnvelopeScale);
									float geomEnvelopeAabbExpand = Mathf.Max(0.0f, cfg.Pass2GeomEnvelopeAabbExpand);
									envelope = Aabb3.FromSegment(segANum, segBNum).Expand(geomEnvelopeRadius);
									if (geomEnvelopeAabbExpand > 0f)
										envelope = envelope.Expand(geomEnvelopeAabbExpand);
									envelopeComputed = true;
									renderHealthSampleRadius = geomEnvelopeRadius;
									renderHealthSampleEnvelopeInflation = Math.Max(0f, (geomEnvelopeRadius - baseRadiusBound) + geomEnvelopeAabbExpand);
									if (statsEnabled && envelopeStartUsec > 0)
										pass2EnvelopeUsecAccum += (long)(Time.GetTicksUsec() - envelopeStartUsec);
								}
								if (renderHealthSampleThisSeg && envelopeComputed && !useThreadedPass2CandidateEval)
								{
									renderHealthSampleEnvDiag = envelope.Extents.Length();
								}

								/////////////////////////////////
								/// Per-segment vars with softGate
								bool segCounted = false;
								ulong cid = 0;
								string cname = "<none>";
								Vector3 hp = Vector3.Zero;
								Vector3 hn = Vector3.Up; // hit normal (world-space collider)
								bool didHit = false;
								bool softGateAttempt = false;
								bool softGateAttemptedRay = false;
								bool softGateHit = false;
								bool softGateSampleThisSeg = false;
								float softGateScore = 0f;
								float softGateTurnAngleDeg = 0f;
								float softGateTurnAngleScore = 0f;
								float softGatePrevHitLostScore = 0f;
								bool softGateRandomProbe = false;
								bool softGateSegLenOk = false;
								SoftGateDecisionReason softGateDecisionReason = SoftGateDecisionReason.Other;
								bool quickRayMissCachedForSeg = false;
								bool hybridFallbackActive = false;
								bool hybridQuickRayMissPendingCache = false;
								bool hybridQuickRayMissAlreadyCached = false;
								int hybridQuickRayMissAx = 0;
								int hybridQuickRayMissAy = 0;
								int hybridQuickRayMissAz = 0;
								int hybridQuickRayMissBx = 0;
								int hybridQuickRayMissBy = 0;
								int hybridQuickRayMissBz = 0;
								int hybridQuickRayMissFlags = 0;
								long geomRayTestsAtSegStart = _geomRayTestsTotalThisFrame;
								int geomTlasCandidateCount = -1;
								long[] geomCandidateInstanceIdsArray = _geomCandidateInstanceIdsScratch;
								ReadOnlySpan<long> geomCandidateInstanceIds = default;
								int geomCandidateInstanceCount = 0;
								bool geomCandidatesActive = false;
								bool pruneAuditSampleThisSeg = false;
								bool pruneAuditBaselineComputed = false;
								bool pruneAuditBaselineHit = false;
								ulong pruneAuditBaselineCid = 0;
								bool pruneAuditBaselineCidInGeometryList = false;
								bool pruneAuditBaselineCidInCandidates = false;
								bool pruneAuditContainmentComputed = false;
								bool pruneAuditFalseNegCounted = false;
								bool pruneAuditFalsePosCounted = false;
								bool pruneAuditOnlyWhenCandidateZero = cfg.DebugGeomPruneAuditOnlyWhenCandidateZero;
								int pruneAuditSampleBudget = Math.Max(0, cfg.DebugGeomPruneAuditSamplesPerHealthWindow);
								int pruneAuditMaxExtraRayTestsPerSample = Math.Max(1, cfg.DebugGeomPruneAuditMaxExtraRayTestsPerSample);
								int pruneAuditLogLimit = Math.Max(0, cfg.DebugGeomPruneAuditMaxMismatchLogsPerWindow);

								void AccountEligibleRayWorkEarlyOut(string reason, int broadphaseCandidateCount)
								{
									if (!useGeomTlasPruning)
										return;
									if (_geomRayTestsTotalThisFrame != geomRayTestsAtSegStart)
										return;
									bool hasCandidates = geomTlasCandidateCount > 0
										|| geomCandidateInstanceCount > 0
										|| broadphaseCandidateCount > 0;
									if (!hasCandidates)
										return;
									_geomRayTestsTotalThisFrame++;
									if (cfg.VerbosePerfLogs && _geomEligibleAccountingLogsRemaining > 0)
									{
										_geomEligibleAccountingLogsRemaining--;
										GD.Print(
											$"[RenderHealth][EligibleRayWork] reason={reason} row={y} x={x} seg={si} " +
											$"geomTlasCand={geomTlasCandidateCount} broadphaseCand={broadphaseCandidateCount}");
									}
								}

								bool TrySelectPruneAuditSample(int candidateCountForAudit)
								{
									if (pruneAuditSampleThisSeg)
										return true;
									if (!useGeomTlasPruning || !cfg.DebugGeomPruneAuditEnabled || pass != 0)
										return false;
									if (geomHealthPartialForStep)
										return false;
									if (pruneAuditSampleBudget <= 0 || _geomPruneAuditSamplesTakenThisWindow >= pruneAuditSampleBudget)
										return false;
									bool candidateCountIsZero = candidateCountForAudit == 0;
									if (pruneAuditOnlyWhenCandidateZero && !candidateCountIsZero)
										return false;

									uint sampleHash = HashPruneAuditSample(_frameIndex, _renderHealthStepIndex, globalPi, si, pass);
									if ((sampleHash & PruneAuditDeterministicMask) != 0u)
										return false;

									pruneAuditSampleThisSeg = true;
									_geomPruneAuditSamplesTakenThisWindow++;
									return true;
								}

								bool EnsurePruneAuditBaselineHit()
								{
									if (!pruneAuditSampleThisSeg)
										return false;
									if (!pruneAuditBaselineComputed)
									{
										_geomPruneAuditSamplesThisFrame++;
										if (rayCfg.UseSphereSweepCollision)
										{
											MarkGeomPixelProcessedForWork();
											pruneAuditBaselineHit = RayBeamRenderer.SweepSegmentHit(
												space,
												segA,
												segB,
												rayCfg.CollisionMask,
												rayCfg.CollisionRadius,
												out _);
											_geomRayTestsTotalThisFrame++;
											pruneAuditBaselineCid = 0;
										}
										else
										{
											int sub = 1;
											if (segLen > rayCfg.CollisionRaySubdivideThreshold)
												sub = Mathf.CeilToInt(segLen / rayCfg.CollisionRaySubdivideThreshold);
											int auditSubMax = Math.Max(1, Math.Min(rayCfg.MaxCollisionSubsteps, pruneAuditMaxExtraRayTestsPerSample));
											sub = Mathf.Clamp(sub, 1, auditSubMax);

											if (cfg.UseAdaptiveSubsteps)
											{
												float far = cfg.AutoRangeDepth ? _rangeFar : cfg.Film.MaxDistance;
												float t = Mathf.Clamp(seg.TraveledB / Mathf.Max(0.001f, far), 0f, 1f);
												float minSub = Mathf.Max(1f, sub * 0.25f);
												float scaled = Mathf.Lerp(sub, minSub, t);
												sub = Mathf.Clamp(Mathf.RoundToInt(scaled), 1, auditSubMax);
											}

											MarkGeomPixelProcessedForWork();
											pruneAuditBaselineHit = RayBeamRenderer.SubdividedRayHit(
												space,
												segA,
												segB,
												rayCfg.CollisionMask,
												sub,
												out _,
												out _,
												out pruneAuditBaselineCid,
												out _,
												out int pruneAuditRayQueries,
												includeColliderName: false,
												hitBackFaces: pass2Flags.HitBackFaces,
												hitFromInside: pass2Flags.HitFromInside,
												diagnosticSceneName: renderSceneName,
												diagnosticFixtureName: renderFixtureName,
												diagnosticModeToken: renderModeToken,
												diagnosticQueryKind: "pass2_prune_audit_subdivided_ray");
											if (pruneAuditRayQueries > 0)
												_geomRayTestsTotalThisFrame += pruneAuditRayQueries;
										}
										pruneAuditBaselineComputed = true;
									}
									return true;
								}

								void ComputePruneAuditContainment(int candidateCountForAudit)
								{
									if (pruneAuditContainmentComputed)
										return;
									if (!EnsurePruneAuditBaselineHit())
										return;

									pruneAuditContainmentComputed = true;
									pruneAuditBaselineCidInGeometryList = false;
									pruneAuditBaselineCidInCandidates = false;

									// ID-space contract:
									// - SubdividedRayHit/Sweep return physics "collider_id".
									// - SnapshotBuilder stores GeometryEntitySOA.GodotInstanceIds via node.GetInstanceId().
									// We check both geometry list + TLAS candidates to avoid assuming mismatched ID spaces.
									if (!pruneAuditBaselineHit || pruneAuditBaselineCid == 0UL)
										return;

									long baselineCidLong = unchecked((long)pruneAuditBaselineCid);
									var geomIds = geomEntities?.GodotInstanceIds;
									if (geomIds != null)
									{
										for (int gi = 0; gi < geomIds.Length; gi++)
										{
											if (geomIds[gi] == baselineCidLong)
											{
												pruneAuditBaselineCidInGeometryList = true;
												break;
											}
										}
									}

									if (candidateCountForAudit > 0)
										pruneAuditBaselineCidInCandidates = ContainsSortedLong(geomCandidateInstanceIdsArray, candidateCountForAudit, baselineCidLong);
								}

								void LogPruneAuditMismatch(string kind, int candidateCountForAudit, bool pruningHit)
								{
									if (!pruneAuditSampleThisSeg || pruneAuditLogLimit <= 0)
										return;
									if (_geomPruneAuditMismatchLogsThisWindow >= pruneAuditLogLimit)
										return;

									_geomPruneAuditMismatchLogsThisWindow++;
									bool candidateCountIsZero = candidateCountForAudit == 0;
									bool candidateHasBaselineCid = pruneAuditBaselineCidInGeometryList || pruneAuditBaselineCidInCandidates;
									long baselineCid = pruneAuditBaselineHit ? unchecked((long)pruneAuditBaselineCid) : 0L;
									float envDiagForLog = envelopeComputed ? envelope.Extents.Length() : renderHealthSampleEnvDiag;
									bool envMismatchSuspect = pruneAuditBaselineHit
										&& pruneAuditBaselineCidInGeometryList
										&& !pruneAuditBaselineCidInCandidates;

									var sb = new StringBuilder(256);
									sb.Append("[PruneAudit][").Append(kind).Append("]")
										.Append(" pass=").Append(pass)
										.Append(" row=").Append(y)
										.Append(" x=").Append(x)
										.Append(" seg=").Append(si)
										.Append(" candCount=").Append(candidateCountForAudit)
										.Append(" cand0=").Append(candidateCountIsZero ? 1 : 0)
										.Append(" baseHit=").Append(pruneAuditBaselineHit ? 1 : 0)
										.Append(" baseCid=").Append(baselineCid)
										.Append(" cidInGeomList=").Append(pruneAuditBaselineCidInGeometryList ? 1 : 0)
										.Append(" cidInCandidates=").Append(pruneAuditBaselineCidInCandidates ? 1 : 0)
										.Append(" candidateHasCid=").Append(candidateHasBaselineCid ? 1 : 0)
										.Append(" pruneHit=").Append(pruningHit ? 1 : 0)
										.Append(" envDiag=").Append(envDiagForLog.ToString("0.###"))
										.Append(" envRad=").Append(renderHealthSampleRadius.ToString("0.###"))
										.Append(" envRadScale=").Append(cfg.Pass2GeomEnvelopeRadiusScale.ToString("0.###"))
										.Append(" envMismatchSuspect=").Append(envMismatchSuspect ? 1 : 0);

									int previewCount = Math.Min(6, Math.Max(0, candidateCountForAudit));
									if (previewCount > 0)
									{
										sb.Append(" candPreview=[");
										for (int ci = 0; ci < previewCount; ci++)
										{
											if (ci > 0) sb.Append(",");
											sb.Append(geomCandidateInstanceIdsArray[ci]);
										}
										sb.Append("]");
									}

									GD.PrintErr(sb.ToString());
								}

								void EvaluatePruneAuditCandidateCoverage(int candidateCountForAudit)
								{
									if (!TrySelectPruneAuditSample(candidateCountForAudit))
										return;
									if (!EnsurePruneAuditBaselineHit())
										return;

									ComputePruneAuditContainment(candidateCountForAudit);
									bool candidateCountIsZero = candidateCountForAudit == 0;
									bool baselineCidKnown = pruneAuditBaselineHit && pruneAuditBaselineCid != 0UL;
									bool candidateHasBaselineCid = pruneAuditBaselineCidInGeometryList || pruneAuditBaselineCidInCandidates;
									bool pruningWouldRejectBaseline = pruneAuditBaselineHit
										&& (candidateCountIsZero || (baselineCidKnown && !candidateHasBaselineCid));
									if (pruningWouldRejectBaseline)
									{
										_geomPruneAuditFalseNegThisFrame++;
										pruneAuditFalseNegCounted = true;
										if (candidateCountIsZero)
											_geomPruneAuditCandidateZeroButBaselineHitThisFrame++;

										string kind = candidateCountIsZero
											? "FN_CAND0_HIT"
											: !baselineCidKnown
												? "FN_BASECID_UNKNOWN"
												: pruneAuditBaselineCidInGeometryList
												? "FN_CID_NOT_IN_CAND"
												: "FN_CID_NOT_IN_GEOM";
										LogPruneAuditMismatch(kind, candidateCountForAudit, pruningHit: false);
									}
									else if (!pruneAuditBaselineHit && candidateHasBaselineCid)
									{
										_geomPruneAuditFalsePosThisFrame++;
										pruneAuditFalsePosCounted = true;
										LogPruneAuditMismatch("FP_BASEMISS_CANDHAS", candidateCountForAudit, pruningHit: false);
									}
								}

								void FinalizePruneAuditResult(bool pruningHit, int candidateCountForAudit)
								{
									if (!EnsurePruneAuditBaselineHit())
										return;

									ComputePruneAuditContainment(candidateCountForAudit);
									if (pruningHit && !pruneAuditBaselineHit && !pruneAuditFalsePosCounted)
									{
										_geomPruneAuditFalsePosThisFrame++;
										pruneAuditFalsePosCounted = true;
										LogPruneAuditMismatch("FP_PRUNE_HIT_BASEMISS", candidateCountForAudit, pruningHit);
										return;
									}

									// Diagnostic-only: prune path missed baseline hit despite containing CID in candidate set.
									bool candidateHasBaselineCid = pruneAuditBaselineCidInGeometryList || pruneAuditBaselineCidInCandidates;
									if (!pruningHit && pruneAuditBaselineHit && candidateHasBaselineCid && !pruneAuditFalseNegCounted)
										LogPruneAuditMismatch("FN_PRUNE_MISS_CANDHAS", candidateCountForAudit, pruningHit);
								}
								/////////////////////////////////

								if (useGeomTlasPruning)
								{
									if (useThreadedPass2CandidateEval
										&& (uint)candidateRecordIndex < (uint)pass2CandidateEvalRecords.Length)
									{
										MarkGeomPixelProcessedForWork();
										Pass2CandidateEvalRecord candidateRecord = pass2CandidateEvalRecords[candidateRecordIndex];
										geomTlasCandidateCount = candidateRecord.CandidateCount;
										geomCandidateInstanceCount = candidateRecord.CandidateCount;
										if (geomCandidateInstanceCount > 0 && (uint)candidateRecord.CandidateStart < (uint)pass2CandidateEvalIds.Length)
											geomCandidateInstanceIds = pass2CandidateEvalIds.AsSpan(candidateRecord.CandidateStart, Math.Min(geomCandidateInstanceCount, pass2CandidateEvalIds.Length - candidateRecord.CandidateStart));
									}
									else
									{
										ulong candidateStartUsec = 0;
										if (statsEnabled) candidateStartUsec = Time.GetTicksUsec();
										Span<int> geomCandidates = _geomCandidatesScratch;
										MarkGeomPixelProcessedForWork();
										int geomCandidateCount = geomTlas.QueryAabb(envelope, geomCandidates);
										geomTlasCandidateCount = geomCandidateCount;
										if (geomCandidateCount > 0)
										{
											Span<long> geomCandidateScratch = geomCandidateInstanceIdsArray;
											var ids = geomEntities.GodotInstanceIds;
											int idsLen = ids.Length;
											int maxFill = Math.Min(geomCandidateCount, geomCandidateScratch.Length);
											for (int gi = 0; gi < maxFill; gi++)
											{
												int geomIndex = geomCandidates[gi];
												if ((uint)geomIndex < (uint)idsLen)
													geomCandidateScratch[geomCandidateInstanceCount++] = ids[geomIndex];
											}
											if (geomCandidateInstanceCount > 1)
											{
												SortLongSpan(geomCandidateScratch, geomCandidateInstanceCount);
												geomCandidateInstanceCount = DedupSortedLong(geomCandidateScratch, geomCandidateInstanceCount);
											}
											geomCandidateInstanceIds = geomCandidateScratch.Slice(0, geomCandidateInstanceCount);
										}
										if (statsEnabled && candidateStartUsec > 0)
											pass2CandidateEvalUsecAccum += (long)(Time.GetTicksUsec() - candidateStartUsec);
									}
									if (pass == 0)
									{
										_geomCandidatesSegmentsThisFrame++;
										_geomCandidatesTotalThisFrame += geomCandidateInstanceCount;
										_tileMetricCurrentSubtiles[subtileIndexThisPixel].CandidateSegments++;
										_tileMetricCurrentSubtiles[subtileIndexThisPixel].CandidateReferences += geomCandidateInstanceCount;
										_geomSegmentsQueriedThisFrame++;
										if (geomCandidateInstanceCount == 0)
										{
											_geomSegZeroCandidatesThisFrame++;
										}
										else
										{
											_geomSegWithCandidatesThisFrame++;
											geomPixelHadAnyCandidatesThisPixel = true;
										}
									}
									EvaluatePruneAuditCandidateCoverage(geomCandidateInstanceCount);
									if (renderHealthSampleThisSeg && !renderHealthSampleRecorded && envelopeComputed)
									{
										RecordRenderHealthPass2Sample(renderHealthSampleRadius, renderHealthSampleEnvDiag, renderHealthSampleEnvelopeInflation, geomCandidateInstanceCount);
										renderHealthSampleRecorded = true;
									}
									geomCandidatesActive = geomCandidateInstanceCount > 0;
									if (!geomCandidatesActive)
									{
										noCandidatesThisPixel = true;
										continue;
									}
								}

								if (rayCfg.UseSphereSweepCollision)
								{
									if (!forceStride1)
									{
										testedAnyInPass0ThisPixel = true;
										pass2StrideSum += pass2Stride;
										pass2StrideCount++;
									}
									ulong queryStartUsec = 0;
									if (statsEnabled) queryStartUsec = Time.GetTicksUsec();
										MarkGeomPixelProcessedForWork();
										didHit = RayBeamRenderer.SweepSegmentHit(space, segA, segB, rayCfg.CollisionMask, rayCfg.CollisionRadius, out hp);
										if (statsEnabled && queryStartUsec > 0)
											pass2QueryUsecAccum += (long)(Time.GetTicksUsec() - queryStartUsec);
										if (telemetryHeatmapsEnabled)
											telemetryQueryCountThisPixel += 1f;
										if (didHit && telemetryHeatmapsEnabled)
											telemetryResolveCountThisPixel += 1f;
										_geomRayTestsTotalThisFrame++;
									if ((statsEnabled || framePerfEnabled) && !segCounted)
									{
										if (statsEnabled) _perfFrame.SegsTested++;
										if (bandCountersEnabled) bandSegsTested++;
										segCounted = true;
									}
									FinalizePruneAuditResult(didHit, geomCandidateInstanceCount);
									// cname stays "<none>" for sphere sweep (unless you add a separate lookup)
								}						
								else
								{
									// Decision A
									if (useInsightPlane)
									{
										//if (!SegmentCrossesPlane(segA, segB, insightPlane, insightEps))
										if (!RayBeamRenderer.SegmentCrossesPlane(segA, segB, insightPlane, insightEps))
										{
											if (framePerfEnabled) _framePerf.Pass2Skip_InsightPlane++;
											continue;
										}
									}


									// Decision B/C
									// ---- PASS2 broadphase candidates ----
									bool pendingQuickRayMissSoftGate = false;
									bool prevHitLostForSoftGate = false;
									bool softGateAllowedNoCandidate = false;
									bool bypassQuickRayForRepresentative = allowInstabilityPass && si == forceRepSegIndex;
									bool skipBroadphaseSegment = false;

									void RunOverlapQuery(
										PhysicsDirectSpaceState3D localSpace,
										Vector3 p0,
										Vector3 p1,
										System.Collections.Generic.List<Godot.Collections.Dictionary> reuse,
										out int overlapCount)
									{
										Vector3 mid = (p0 + p1) * 0.5f;

										_overlapQuery.Transform = new Transform3D(Basis.Identity, mid);
										ulong queryStartUsec = 0;
										if (statsEnabled) queryStartUsec = Time.GetTicksUsec();
										MarkGeomPixelProcessedForWork();
										var overlaps = localSpace.IntersectShape(_overlapQuery, broadphaseCfg.MaxResults);
										if (statsEnabled && queryStartUsec > 0)
											pass2QueryUsecAccum += (long)(Time.GetTicksUsec() - queryStartUsec);
										if (telemetryHeatmapsEnabled)
											telemetryQueryCountThisPixel += 1f;
										if (statsEnabled) _perfFrame.IntersectShapeCalls++;
										if (bandCountersEnabled) bandPhysicsQueries++;
										if ((statsEnabled || framePerfEnabled) && !segCounted)
										{
											if (statsEnabled) _perfFrame.SegsTested++;
											if (bandCountersEnabled) bandSegsTested++;
											segCounted = true;
										}
										overlapCount = overlaps.Count;
										reuse.Clear();
										for (int oi = 0; oi < overlapCount; oi++)
										{
											var o = (Godot.Collections.Dictionary)overlaps[oi];
											reuse.Add(o);
										}
										if (framePerfEnabled)
										{
											if (overlapCount == 0)
											{
												_framePerf.Pass2OverlapMisses++;
												_framePerf.Pass2Skip_OverlapEmpty++;
											}
											else
											{
												_framePerf.Pass2OverlapHits++;
											}
										}
									}

									void MarkHybridQuickRayMissPending(int ax, int ay, int az, int bx, int by, int bz, bool alreadyCached)
									{
										hybridQuickRayMissPendingCache = true;
										hybridQuickRayMissAlreadyCached = alreadyCached;
										hybridQuickRayMissAx = ax;
										hybridQuickRayMissAy = ay;
										hybridQuickRayMissAz = az;
										hybridQuickRayMissBx = bx;
										hybridQuickRayMissBy = by;
										hybridQuickRayMissBz = bz;
										hybridQuickRayMissFlags = pass2FlagsKey;
									}

									void FlushHybridQuickRayMissCache()
									{
										if (!hybridQuickRayMissPendingCache)
											return;
										if (!hybridQuickRayMissAlreadyCached)
											AddPass2QuickRayCache(hybridQuickRayMissAx, hybridQuickRayMissAy, hybridQuickRayMissAz, hybridQuickRayMissBx, hybridQuickRayMissBy, hybridQuickRayMissBz, hybridQuickRayMissFlags, false, 0f);
										hybridQuickRayMissPendingCache = false;
										hybridQuickRayMissAlreadyCached = false;
									}

									void FinalizeHybridQuickRayMissCache(bool hit, float hitDistAlongRay)
									{
										if (!hybridQuickRayMissPendingCache)
											return;

										if (hit)
										{
											bool updated = TryUpdatePass2QuickRayCacheEntry(hybridQuickRayMissAx, hybridQuickRayMissAy, hybridQuickRayMissAz, hybridQuickRayMissBx, hybridQuickRayMissBy, hybridQuickRayMissBz, hybridQuickRayMissFlags, true, hitDistAlongRay);
											if (!updated && hybridQuickRayMissAlreadyCached)
											{
												AddPass2QuickRayCache(hybridQuickRayMissAx, hybridQuickRayMissAy, hybridQuickRayMissAz, hybridQuickRayMissBx, hybridQuickRayMissBy, hybridQuickRayMissBz, hybridQuickRayMissFlags, true, hitDistAlongRay);
											}
										}
										else
										{
											if (!hybridQuickRayMissAlreadyCached)
												AddPass2QuickRayCache(hybridQuickRayMissAx, hybridQuickRayMissAy, hybridQuickRayMissAz, hybridQuickRayMissBx, hybridQuickRayMissBy, hybridQuickRayMissBz, hybridQuickRayMissFlags, false, 0f);
										}

										hybridQuickRayMissPendingCache = false;
										hybridQuickRayMissAlreadyCached = false;
									}

									int RunQuickRayQuery(
										PhysicsDirectSpaceState3D localSpace,
										Vector3 p0,
										Vector3 p1,
										out int qrayCount)
									{
										qrayCount = -1;

										int ax = QuantizePass2QuickRay(p0.X);
										int ay = QuantizePass2QuickRay(p0.Y);
										int az = QuantizePass2QuickRay(p0.Z);
										int bx = QuantizePass2QuickRay(p1.X);
										int by = QuantizePass2QuickRay(p1.Y);
										int bz = QuantizePass2QuickRay(p1.Z);

										if (TryGetPass2QuickRayCache(ax, ay, az, bx, by, bz, pass2FlagsKey, out bool cachedHit, out float cachedDist))
										{
											qrayCount = cachedHit ? 1 : 0;
											if (pass == 0)
											{
												quickRayTestedThisPixel = true;
												if (cachedHit) quickRayHitThisPixel = true;
											}
											if (framePerfEnabled) _framePerf.CacheHits++;
											if (framePerfEnabled)
											{
												if (cachedHit) _framePerf.Pass2QuickRayHits++;
												else _framePerf.Pass2QuickRayMisses++;
											}
											CountQuickRayResult(cachedHit);
											if (!cachedHit)
											{
												if (useHybridBroadphase)
													MarkHybridQuickRayMissPending(ax, ay, az, bx, by, bz, true);
												_quickRayZeroCountThisFrame++;
												if (prevHadHitForSoftGate && si > segStart && _pass2HadHitLostThisFrame.Length > globalPi)
													_pass2HadHitLostThisFrame[globalPi] = 1;
												// SoftGate v2 uses per-pixel hit history; wire these to real history buffers if you track them elsewhere.
												prevHitLostForSoftGate = _pass2HadHitLostThisFrame.Length > globalPi && _pass2HadHitLostThisFrame[globalPi] != 0;
												if (!useHybridBroadphase)
												{
													if (!TryHandleQuickRayMissWithSoftGate(
														softGateFrameId,
														si,
														segLen,
														prevSegDir,
														currSegDir,
														prevHadHitForSoftGate,
														prevHitLostForSoftGate,
														cfg.UseSingleProbeThenSubdivide,
														false,
														ref softGateDecisionReason,
														ref softGateScore,
														ref softGateTurnAngleDeg,
														ref softGateTurnAngleScore,
														ref softGatePrevHitLostScore,
														ref softGateRandomProbe,
														ref softGateSegLenOk,
														ref softGateSampleThisSeg,
														ref softGateAttempt,
														ref softGateAttemptsThisPixel,
														ref softGateAttemptsUsed,
														ref softGateSubdividedCallsUsed,
														ref p2SoftGateAttempts,
														ref softGateBudgetExceeded))
													{
														if (budgetStop) return qrayCount;
														skipBroadphaseSegment = true;
														return qrayCount;
													}
													softGateAllowedNoCandidate = true;
												}
												else
												{
													pendingQuickRayMissSoftGate = true;
												}
											}
											if (cachedDist < bestHitDistAlongRay)
												bestHitDistAlongRay = cachedDist;
											return qrayCount;
										}

										if (pass == 0) quickRayTestedThisPixel = true;
										if (framePerfEnabled) _framePerf.CacheMisses++;
										_quickRayParams.From = p0;
										_quickRayParams.To = p1;
										MarkGeomPixelProcessedForWork();
										bool quickRaySucceeded = TryIntersectRenderSpaceRaySafe(
											localSpace,
											_quickRayParams,
											cfg.Pass1DoHitTest,
											"pass2_quickray_local",
											renderSpaceSource,
											renderSceneName,
											renderFixtureName,
											renderModeToken,
											ref _renderSpaceRayQueryWarned,
											out var hit0);
										if (quickRaySucceeded && telemetryHeatmapsEnabled)
											telemetryQueryCountThisPixel += 1f;
										if (quickRaySucceeded)
										{
											_geomRayTestsTotalThisFrame++;
											if (statsEnabled) _perfFrame.IntersectRayCalls++;
											if (bandCountersEnabled) bandPhysicsQueries++;
										}
										if (quickRaySucceeded && (statsEnabled || framePerfEnabled) && !segCounted)
										{
											if (statsEnabled) _perfFrame.SegsTested++;
											if (bandCountersEnabled) bandSegsTested++;
											segCounted = true;
										}
										if (hit0.Count == 0)
										{
											qrayCount = 0;
											if (quickRaySucceeded && useHybridBroadphase)
											{
												MarkHybridQuickRayMissPending(ax, ay, az, bx, by, bz, false);
											}
											else if (quickRaySucceeded)
											{
												AddPass2QuickRayCache(ax, ay, az, bx, by, bz, pass2FlagsKey, false, 0f);
											}
											if (framePerfEnabled) _framePerf.Pass2QuickRayMisses++;
											CountQuickRayResult(false);
											_quickRayZeroCountThisFrame++;
											if (prevHadHitForSoftGate && si > segStart && _pass2HadHitLostThisFrame.Length > globalPi)
												_pass2HadHitLostThisFrame[globalPi] = 1;
											prevHitLostForSoftGate = _pass2HadHitLostThisFrame.Length > globalPi && _pass2HadHitLostThisFrame[globalPi] != 0;
											if (!useHybridBroadphase)
											{
												if (!TryHandleQuickRayMissWithSoftGate(
													softGateFrameId,
													si,
													segLen,
													prevSegDir,
													currSegDir,
													prevHadHitForSoftGate,
													prevHitLostForSoftGate,
													cfg.UseSingleProbeThenSubdivide,
													false,
													ref softGateDecisionReason,
													ref softGateScore,
													ref softGateTurnAngleDeg,
													ref softGateTurnAngleScore,
													ref softGatePrevHitLostScore,
													ref softGateRandomProbe,
													ref softGateSegLenOk,
													ref softGateSampleThisSeg,
													ref softGateAttempt,
													ref softGateAttemptsThisPixel,
													ref softGateAttemptsUsed,
													ref softGateSubdividedCallsUsed,
													ref p2SoftGateAttempts,
													ref softGateBudgetExceeded))
												{
													if (budgetStop) return qrayCount;
													skipBroadphaseSegment = true;
													return qrayCount;
												}
												softGateAllowedNoCandidate = true;
											}
											else
											{
												pendingQuickRayMissSoftGate = true;
											}
										}
										else
										{
											qrayCount = 1;
											CountQuickRayResult(true);
										}
										if (pass == 0) quickRayHitThisPixel = true;
										if (framePerfEnabled) _framePerf.Pass2QuickRayHits++;
										Vector3 hitPos = (Vector3)hit0["position"];
										float d = seg.TraveledB - segLen + (hitPos - p0).Length();
										if (telemetryHeatmapsEnabled)
											telemetryResolveCountThisPixel += 1f;
										AddPass2QuickRayCache(ax, ay, az, bx, by, bz, pass2FlagsKey, true, d);
										if (d < bestHitDistAlongRay)
											bestHitDistAlongRay = d;
										return qrayCount;
									}

									bool TryHybridBroadphase(
										ref SegmentContext seg,
										out OverlapResult overlaps,
										out bool usedOverlapFallback)
									{
										// Hybrid broadphase: QuickRay hit is sufficient for candidates; only run overlap on qray miss.
										overlaps = default;
										overlaps.Candidates = _pass2OverlapCandidatesScratch;
										overlaps.Count = 0;
										usedOverlapFallback = false;

										seg.QuickRayExecuted = false;
										seg.QuickRayCount = -1;
										seg.QuickRayHit = false;
										seg.QuickRayMiss = false;
										seg.OverlapExecuted = false;
										seg.OverlapCount = 0;

										bool canQuickRay = seg.UseQuickRay && !seg.BypassQuickRay;
										if (canQuickRay)
										{
											seg.QuickRayExecuted = true;
											int qrayCount;
											RunQuickRayQuery(seg.Space, seg.A, seg.B, out qrayCount);
											seg.QuickRayCount = qrayCount;
											if (qrayCount > 0)
											{
												seg.QuickRayHit = true;
											}
											else
											{
												seg.QuickRayMiss = true;
											}
										}

										if (seg.UseOverlap && (!seg.QuickRayExecuted || seg.QuickRayCount == 0))
										{
											usedOverlapFallback = seg.QuickRayMiss;
											seg.OverlapExecuted = true;
											RunOverlapQuery(seg.Space, seg.A, seg.B, overlaps.Candidates, out int overlapCount);
											seg.OverlapCount = overlapCount;
											overlaps.Count = overlapCount;
										}

										return (seg.QuickRayCount > 0) || (seg.OverlapCount > 0);
									}

									int candidateCount = -1;
									int overlapCount = 0;
									int qrayCount = -1;
									bool forceNarrowphaseDueToQuickRay = false;
									var overlapCandidates = _pass2OverlapCandidatesScratch;
									if (useGeomTlasPruning)
									{
										candidateCount = 0;
										if (useHybridBroadphase)
										{
											SegmentContext segCtx = new SegmentContext
											{
												Space = space,
												A = segA,
												B = segB,
												UseQuickRay = useQuickRay,
												UseOverlap = useOverlap,
												BypassQuickRay = false
											};
											bool usedOverlapFallback;
											OverlapResult overlapResult;
											bool hasCandidates = TryHybridBroadphase(ref segCtx, out overlapResult, out usedOverlapFallback);
											overlapCandidates = overlapResult.Candidates;
											overlapCount = segCtx.OverlapCount;
											qrayCount = segCtx.QuickRayCount;
											if (hasCandidates)
											{
												if (overlapCount > 0)
												{
													candidateCount = overlapCount;
												}
												else if (qrayCount > 0)
												{
													candidateCount = qrayCount;
													forceNarrowphaseDueToQuickRay = true;
													if (_broadphaseHybridGateLogsRemaining > 0)
													{
														GD.Print($"[HybridGate] qray>0 but overlap=0 -> forcing narrowphase row={y} x={x} seg={si} qray={qrayCount}");
														_broadphaseHybridGateLogsRemaining--;
													}
												}
												if (usedOverlapFallback && overlapCount > 0)
												{
													_hybridFallbackCountThisFrame++;
													hybridFallbackActive = true;
													if (_broadphaseHybridFallbackLogsRemaining > 0)
													{
														GD.Print($"[HybridFallback] qray=0 -> overlap candidates={overlapCount} row={_rowCursor} x={x}");
														_broadphaseHybridFallbackLogsRemaining--;
													}
													pendingQuickRayMissSoftGate = false;
												}
											}
											else if (usedOverlapFallback && qrayCount == 0)
											{
												noCandidatesThisPixel = true;
											}
										}
										else
										{
											if (useOverlap)
											{
												RunOverlapQuery(space, segA, segB, overlapCandidates, out overlapCount);
												if (overlapCount > 0)
													candidateCount = overlapCount;
											}
											if (candidateCount == 0 && useQuickRay && !bypassQuickRayForRepresentative)
											{
												int qrayCountLocal;
												RunQuickRayQuery(space, segA, segB, out qrayCountLocal);
												if (qrayCountLocal > 0)
													candidateCount = qrayCountLocal;
											}
										}
									}
									if (telemetryHeatmapsEnabled && candidateCount > 0)
										telemetryCandidateCountThisPixel += candidateCount;

									if (renderHealthSampleThisSeg && !renderHealthSampleRecorded && envelopeComputed)
									{
										// TLAS pruning off restores pre-TLAS full narrowphase, so candidate histograms are intentionally NA.
										int renderHealthCandidateCount = useGeomTlasPruning ? candidateCount : -1;
										RecordRenderHealthPass2Sample(renderHealthSampleRadius, renderHealthSampleEnvDiag, renderHealthSampleEnvelopeInflation, renderHealthCandidateCount);
										renderHealthSampleRecorded = true;
									}

									if (budgetStop) break;
									if (useGeomTlasPruning)
									{
										if (skipBroadphaseSegment)
										{
											FinalizePruneAuditResult(false, geomCandidateInstanceCount);
											AccountEligibleRayWorkEarlyOut("skip_broadphase_segment", candidateCount);
											FlushHybridQuickRayMissCache();
											continue;
										}

										if (pendingQuickRayMissSoftGate)
										{
											if (!TryHandleQuickRayMissWithSoftGate(
												softGateFrameId,
												si,
												segLen,
												prevSegDir,
												currSegDir,
												prevHadHitForSoftGate,
												prevHitLostForSoftGate,
												cfg.UseSingleProbeThenSubdivide,
												false,
												ref softGateDecisionReason,
												ref softGateScore,
												ref softGateTurnAngleDeg,
												ref softGateTurnAngleScore,
												ref softGatePrevHitLostScore,
												ref softGateRandomProbe,
												ref softGateSegLenOk,
												ref softGateSampleThisSeg,
												ref softGateAttempt,
												ref softGateAttemptsThisPixel,
												ref softGateAttemptsUsed,
												ref softGateSubdividedCallsUsed,
												ref p2SoftGateAttempts,
												ref softGateBudgetExceeded))
											{
												if (budgetStop) break;
												FinalizePruneAuditResult(false, geomCandidateInstanceCount);
												AccountEligibleRayWorkEarlyOut("quickray_softgate_reject", candidateCount);
												FlushHybridQuickRayMissCache();
												continue;
											}
											softGateAllowedNoCandidate = true;
										}

										if (candidateCount == 0 && !softGateAllowedNoCandidate)
										{
											if (forceNarrowphaseDueToQuickRay)
											{
												softGateAllowedNoCandidate = true;
											}
											else
											{
												FinalizePruneAuditResult(false, geomCandidateInstanceCount);
												AccountEligibleRayWorkEarlyOut("broadphase_no_candidate", candidateCount);
												FlushHybridQuickRayMissCache();
												continue;
											}
										}

										if (candidateCount > 0 || softGateAllowedNoCandidate)
										{
											bandHadCandidates = true;
											hadCandidatesThisPixel = true;
										}
									}
									else
									{
										bandHadCandidates = true;
										hadCandidatesThisPixel = true;
									}

									if (cfg.UseSingleProbeThenSubdivide && !useQuickRay && !bypassQuickRayForRepresentative)
									{
										int ax = QuantizePass2QuickRay(segA.X);
										int ay = QuantizePass2QuickRay(segA.Y);
										int az = QuantizePass2QuickRay(segA.Z);
										int bx = QuantizePass2QuickRay(segB.X);
										int by = QuantizePass2QuickRay(segB.Y);
										int bz = QuantizePass2QuickRay(segB.Z);

										if (TryGetPass2QuickRayCache(ax, ay, az, bx, by, bz, pass2FlagsKey, out bool cachedHit, out float cachedDist))
										{
											if (pass == 0)
											{
												quickRayTestedThisPixel = true;
												if (cachedHit) quickRayHitThisPixel = true;
											}
											if (framePerfEnabled) _framePerf.CacheHits++;
											if (framePerfEnabled)
											{
												if (cachedHit) _framePerf.Pass2QuickRayHits++;
												else _framePerf.Pass2QuickRayMisses++;
											}
											CountQuickRayResult(cachedHit);
											if (!cachedHit)
											{
												_quickRayZeroCountThisFrame++;
												if (prevHadHitForSoftGate && si > segStart && _pass2HadHitLostThisFrame.Length > globalPi)
													_pass2HadHitLostThisFrame[globalPi] = 1;
												prevHitLostForSoftGate = _pass2HadHitLostThisFrame.Length > globalPi && _pass2HadHitLostThisFrame[globalPi] != 0;
												if (!TryHandleQuickRayMissWithSoftGate(
													softGateFrameId,
													si,
													segLen,
													prevSegDir,
													currSegDir,
													prevHadHitForSoftGate,
													prevHitLostForSoftGate,
													true,
													true,
													ref softGateDecisionReason,
													ref softGateScore,
													ref softGateTurnAngleDeg,
													ref softGateTurnAngleScore,
													ref softGatePrevHitLostScore,
													ref softGateRandomProbe,
													ref softGateSegLenOk,
													ref softGateSampleThisSeg,
													ref softGateAttempt,
													ref softGateAttemptsThisPixel,
													ref softGateAttemptsUsed,
													ref softGateSubdividedCallsUsed,
													ref p2SoftGateAttempts,
													ref softGateBudgetExceeded))
												{
													if (budgetStop) break;
													AccountEligibleRayWorkEarlyOut("singleprobe_cached_quickray_reject", candidateCount);
													continue;
												}
											}
											if (cachedDist < bestHitDistAlongRay)
												bestHitDistAlongRay = cachedDist;
										}
										else
										{
											if (pass == 0) quickRayTestedThisPixel = true;
											if (framePerfEnabled) _framePerf.CacheMisses++;
											_quickRayParams.From = segA;
											_quickRayParams.To = segB;
											ulong queryStartUsec = 0;
											if (statsEnabled) queryStartUsec = Time.GetTicksUsec();
											MarkGeomPixelProcessedForWork();
											bool quickRaySucceeded = TryIntersectRenderSpaceRaySafe(
												space,
												_quickRayParams,
												cfg.Pass1DoHitTest,
												"pass2_quickray_segment",
												renderSpaceSource,
												renderSceneName,
												renderFixtureName,
												renderModeToken,
												ref _renderSpaceRayQueryWarned,
												out var hit0);
											if (statsEnabled && queryStartUsec > 0)
												pass2QueryUsecAccum += (long)(Time.GetTicksUsec() - queryStartUsec);
											if (quickRaySucceeded && telemetryHeatmapsEnabled)
												telemetryQueryCountThisPixel += 1f;
											if (quickRaySucceeded)
											{
												_geomRayTestsTotalThisFrame++;
												if (statsEnabled) _perfFrame.IntersectRayCalls++;
												if (bandCountersEnabled) bandPhysicsQueries++;
											}
											if (quickRaySucceeded && (statsEnabled || framePerfEnabled) && !segCounted)
											{
												if (statsEnabled) _perfFrame.SegsTested++;
												if (bandCountersEnabled) bandSegsTested++;
												segCounted = true;
											}
											if (hit0.Count == 0)
											{
												if (quickRaySucceeded)
													AddPass2QuickRayCache(ax, ay, az, bx, by, bz, pass2FlagsKey, false, 0f);
												if (framePerfEnabled) _framePerf.Pass2QuickRayMisses++;
												CountQuickRayResult(false);
												_quickRayZeroCountThisFrame++;
												if (prevHadHitForSoftGate && si > segStart && _pass2HadHitLostThisFrame.Length > globalPi)
													_pass2HadHitLostThisFrame[globalPi] = 1;
												prevHitLostForSoftGate = _pass2HadHitLostThisFrame.Length > globalPi && _pass2HadHitLostThisFrame[globalPi] != 0;
												if (!TryHandleQuickRayMissWithSoftGate(
													softGateFrameId,
													si,
													segLen,
													prevSegDir,
													currSegDir,
													prevHadHitForSoftGate,
													prevHitLostForSoftGate,
													true,
													true,
													ref softGateDecisionReason,
													ref softGateScore,
													ref softGateTurnAngleDeg,
													ref softGateTurnAngleScore,
													ref softGatePrevHitLostScore,
													ref softGateRandomProbe,
													ref softGateSegLenOk,
													ref softGateSampleThisSeg,
													ref softGateAttempt,
													ref softGateAttemptsThisPixel,
													ref softGateAttemptsUsed,
													ref softGateSubdividedCallsUsed,
													ref p2SoftGateAttempts,
													ref softGateBudgetExceeded))
												{
													if (budgetStop) break;
													AccountEligibleRayWorkEarlyOut("singleprobe_quickray_reject", candidateCount);
													continue;
												}
											}
											else
											{
												CountQuickRayResult(true);
											}
											if (pass == 0) quickRayHitThisPixel = true;
											if (framePerfEnabled) _framePerf.Pass2QuickRayHits++;
											Vector3 hitPos = (Vector3)hit0["position"];
											float d = seg.TraveledB - segLen + (hitPos - segA).Length();
											if (telemetryHeatmapsEnabled)
												telemetryResolveCountThisPixel += 1f;
											AddPass2QuickRayCache(ax, ay, az, bx, by, bz, pass2FlagsKey, true, d);
											if (d < bestHitDistAlongRay)
												bestHitDistAlongRay = d;
										}
									}

									if (!forceStride1 && pass2Stride > 1)
									{
										bool forceTest = si == 0 || si == lastSi
											|| (cfg.MinSegLenForStrideSkip > 0f && segLen < cfg.MinSegLenForStrideSkip);
										if (forceTest)
											subRaysForcedByPass2Stride++;
										else if ((si % pass2Stride) != 0)
										{
											subRaysSkippedByPass2Stride++;
											skippedAnyByStrideThisPixel = true;
											_perfFrame.SubRaySkippedByStride++;
											if (framePerfEnabled) _framePerf.Pass2Skip_Stride++;
											LogSoftGateSample(
												si,
												segLen,
												softGateScore,
												softGateTurnAngleDeg,
												softGateTurnAngleScore,
												softGatePrevHitLostScore,
												softGateRandomProbe,
												softGateSegLenOk,
												softGateDecisionReason == SoftGateDecisionReason.Allow,
												false,
												false,
												softGateDecisionReason,
												softGateSampleThisSeg);
											AccountEligibleRayWorkEarlyOut("stride_skip", candidateCount);
											continue;
										}
									}
									if (!forceStride1)
									{
										testedAnyInPass0ThisPixel = true;
										pass2StrideSum += pass2Stride;
										pass2StrideCount++;
									}

									if (pass == 1 && pass2QuickRayMissLogRemaining > 0 && (useQuickRay || cfg.UseSingleProbeThenSubdivide))
									{
										int ax = QuantizePass2QuickRay(segA.X);
										int ay = QuantizePass2QuickRay(segA.Y);
										int az = QuantizePass2QuickRay(segA.Z);
										int bx = QuantizePass2QuickRay(segB.X);
										int by = QuantizePass2QuickRay(segB.Y);
										int bz = QuantizePass2QuickRay(segB.Z);
										if (TryGetPass2QuickRayCache(ax, ay, az, bx, by, bz, pass2FlagsKey, out bool cachedHit, out _))
											quickRayMissCachedForSeg = !cachedHit;
									}

									bool TrySubdividedRayNarrowphase(out float hitDistAlongRay)
									{
										hitDistAlongRay = 0f;
										didHit = false;
										// ---- accurate subdivided ray ----
										if (softGateAttempt)
										{
											if (softGateDebugEnabled)
											{
												_softGateFrame.SoftGateAttempts++;
												if (softGateBandEnabled) _softGateBand.SoftGateAttempts++;
											}
											softGateAttemptedRay = true;
											softGateAttempted++;
										}
										ulong softGateStart = 0;
										if (softGateAttemptedRay && softGateCfg.WatchdogMs > 0f)
											softGateStart = Time.GetTicksUsec();
										int sub = 1;
										if (segLen > rayCfg.CollisionRaySubdivideThreshold)
											sub = Mathf.CeilToInt(segLen / rayCfg.CollisionRaySubdivideThreshold);
										sub = Mathf.Clamp(sub, 1, rayCfg.MaxCollisionSubsteps);

										if (cfg.UseAdaptiveSubsteps)
										{
											float far = cfg.AutoRangeDepth ? _rangeFar : cfg.Film.MaxDistance;
											float t = Mathf.Clamp(seg.TraveledB / Mathf.Max(0.001f, far), 0f, 1f);
											float minSub = Mathf.Max(1f, sub * 0.25f);
											float scaled = Mathf.Lerp(sub, minSub, t);
											sub = Mathf.Clamp(Mathf.RoundToInt(scaled), 1, rayCfg.MaxCollisionSubsteps);
										}

										if (softGateAttemptedRay)
										{
											softGateSubdividesThisPixel++;
											if (CheckRenderStepWatchdog())
											{
												if (budgetStop) return false;
												renderStepAbort = true;
												softGateWatchdogTrippedThisPixel = true;
												return false;
											}
										}
										ulong queryStartUsec = 0;
										if (statsEnabled) queryStartUsec = Time.GetTicksUsec();
										MarkGeomPixelProcessedForWork();
										didHit = RayBeamRenderer.SubdividedRayHit(
												space, segA, segB,
												rayCfg.CollisionMask,
												sub,
											out hp, out hn, out cid, out cname,
											out int rayQueries,
											includeColliderName: needHitName,
											hitBackFaces: pass2Flags.HitBackFaces,
											hitFromInside: pass2Flags.HitFromInside,
											diagnosticSceneName: renderSceneName,
												diagnosticFixtureName: renderFixtureName,
												diagnosticModeToken: renderModeToken,
												diagnosticQueryKind: "pass2_subdivided_ray");
									if (statsEnabled && queryStartUsec > 0)
										pass2QueryUsecAccum += (long)(Time.GetTicksUsec() - queryStartUsec);
									if (telemetryHeatmapsEnabled)
										telemetryQueryCountThisPixel += Math.Max(1, rayQueries);
									if (rayQueries > 0)
										_geomRayTestsTotalThisFrame += rayQueries;
									if (statsEnabled)
									{
										_perfFrame.SubdividedRayCalls++;
										_perfFrame.SubdividedRayQueries += rayQueries;
										_perfFrame.SubdividedRaySubsteps += sub;
									}
									if (bandCountersEnabled) bandPhysicsQueries += rayQueries;
									if ((statsEnabled || framePerfEnabled) && !segCounted)
									{
										if (statsEnabled) _perfFrame.SegsTested++;
										if (bandCountersEnabled) bandSegsTested++;
										segCounted = true;
									}
									
									if (didHit && quickRayMissCachedForSeg && pass2QuickRayMissLogRemaining > 0)
									{
										Vector3 rayDir = segLen > 0f ? (segB - segA) / segLen : Vector3.Zero;
										GD.Print($"Pass2 QuickRay miss->subdivide hit: from={segA} to={segB} dir={rayDir} segLen={segLen} flags(HitFromInside={pass2Flags.HitFromInside}, HitBackFaces={pass2Flags.HitBackFaces}) colliderRid={cid}");
										pass2QuickRayMissLogRemaining--;
									}
									if (!didHit && prevHadHitForSoftGate && si > segStart && _pass2HadHitLostThisFrame.Length > globalPi)
									{
										_pass2HadHitLostThisFrame[globalPi] = 1;
									}
									if (didHit && softGateAttemptedRay)
									{
										p2SoftGateHits++;
										softGateHitChangedResult++;
										if (softGateDebugEnabled)
										{
											_softGateFrame.SoftGateHits++;
											if (softGateBandEnabled) _softGateBand.SoftGateHits++;
										}
										softGateHit = true;
										softGateHitThisPixel = true;
									}
									if (softGateAttemptedRay && softGateCfg.WatchdogMs > 0f)
									{
										double elapsedMs = (Time.GetTicksUsec() - softGateStart) / 1000.0;
										if (elapsedMs > softGateCfg.WatchdogMs)
										{
											softGateLoopGuardTripped++;
											softGateWatchdogTrippedThisPixel = true;
											SoftGateRecordSkip(SoftGateDecisionReason.Guard);
											LogBudgetExitOnce("guard_softgate_watchdog", y);
											if (softGateCfg.DisableOnOverload)
											{
												_softGateDisabledForPass = true;
												DisableSoftGateThisFrame("softgate_watchdog");
											}
											if (softGateCfg.DebugEnabled && _softGateWatchdogLogsRemaining > 0)
											{
												_softGateWatchdogLogsRemaining--;
												GD.PrintErr($"[SoftGate][Watchdog] segIndex={si} elapsed={elapsedMs:0.00}ms sub={sub} segLen={segLen:0.###} guard=1");
											}
										}
									}

									if (didHit && needHitName)
										hitName = cname;
									LogSoftGateSample(
											si,
											segLen,
											softGateScore,
											softGateTurnAngleDeg,
											softGateTurnAngleScore,
											softGatePrevHitLostScore,
											softGateRandomProbe,
											softGateSegLenOk,
											softGateDecisionReason == SoftGateDecisionReason.Allow,
											softGateAttemptedRay,
											softGateHit,
											softGateDecisionReason,
											softGateSampleThisSeg);
										if (didHit)
											hitDistAlongRay = seg.TraveledB - segLen + (hp - segA).Length();
										if (didHit && telemetryHeatmapsEnabled)
											telemetryResolveCountThisPixel += 1f;
										return didHit;
									}

									bool TryHybridFallbackNarrowphase(out float hitDistAlongRay)
									{
										bool hit = TrySubdividedRayNarrowphase(out hitDistAlongRay);
										if (!softGateWatchdogTrippedThisPixel && !budgetStop)
										{
											if (hit)
											{
												_hybridFallbackHitCountThisFrame++;
												if (_broadphaseHybridFallbackHitLogsRemaining > 0)
												{
													GD.Print($"[HybridFallbackHit] qrayMiss -> subdividedHit dist={hitDistAlongRay:0.###} row={y} x={x}");
													_broadphaseHybridFallbackHitLogsRemaining--;
												}
											}
											else
											{
												_hybridFallbackMissCountThisFrame++;
											}
										}
										return hit;
									}

									float narrowphaseHitDistAlongRay = 0f;
									if (hybridFallbackActive)
										didHit = TryHybridFallbackNarrowphase(out narrowphaseHitDistAlongRay);
									else
										didHit = TrySubdividedRayNarrowphase(out narrowphaseHitDistAlongRay);

									ulong hitResolveStartUsec = 0;
									if (statsEnabled) hitResolveStartUsec = Time.GetTicksUsec();
									if (didHit && useGeomTlasPruning)
									{
										long geomId = unchecked((long)cid);
										if (ContainsSortedLong(geomCandidateInstanceIds, geomCandidateInstanceCount, geomId))
										{
											_geomHitAcceptedThisFrame++;
											_geomRayTestsAcceptedThisFrame++;
										}
										else
										{
											_geomHitRejectedThisFrame++;
											_geomRayTestsRejectedThisFrame++;
											if (DebugGeomRejectSampleEnabled
												&& DebugGeomRejectSampleEveryN > 0
												&& (_geomHitRejectedThisFrame % DebugGeomRejectSampleEveryN) == 0)
											{
												GeomRejectSampleCause rejectCause = LogGeomRejectSample(
													cid,
													cname,
													envelope,
													geomCandidateInstanceIds,
													geomCandidateInstanceCount,
													geomEntities);
												switch (rejectCause)
												{
													case GeomRejectSampleCause.CidNotInGeometryList:
														_geomRejectSampleCidNotInGeometryList++;
														break;
													case GeomRejectSampleCause.CidInGeometryListNotInCandidates:
														_geomRejectSampleCidInGeometryListNotInCandidates++;
														break;
													case GeomRejectSampleCause.CandidateContainsCid:
														_geomRejectSampleCandidateContainsCid++;
														break;
												}
											}
											didHit = false;
											cid = 0;
											cname = "<none>";
										}
									}
									else if (didHit)
									{
										_geomHitAcceptedThisFrame++;
										_geomRayTestsAcceptedThisFrame++;
									}
									FinalizePruneAuditResult(didHit, geomCandidateInstanceCount);
									if (statsEnabled && hitResolveStartUsec > 0)
										pass2HitResolveUsecAccum += (long)(Time.GetTicksUsec() - hitResolveStartUsec);

									FinalizeHybridQuickRayMissCache(didHit, narrowphaseHitDistAlongRay);
									if (budgetStop) break;

								////////////
								if (softGateWatchdogTrippedThisPixel)
									break;
								if (didHit)
								{
									float d = seg.TraveledB - segLen + (hp - segA).Length();
									if (d < bestHitDistAlongRay)
										bestHitDistAlongRay = d;

									if (d < bestHit)
									{
										bestHit = d;
										hitDistance = d;
										hadHit = true;
										if (needHitName) hitName = cname;
										bestHp = hp;      // ADD
										bestHn = hn;      // ADD
										bestCid = cid;
									}

									// If you only want the nearest hit, keep scanning segments
									if (cfg.NearestHitOnly)
									{
										if (cfg.EarlyOutDistanceEps > 0f && bestHit <= cfg.EarlyOutDistanceEps){
											EarlyOut("near early-out", cfg.EnableProfiling);
											break;
										}
										continue;
									}
									
									// Otherwise, first hit wins
									break;
								}
								//////////////////
							}
							if (budgetStop) break;
							if (pass == 0)
							{
								bool quickRayAllMiss = quickRayTestedThisPixel && !quickRayHitThisPixel;
								if (!hadHit && cfg.Pass2ForceOnInstability && quickRayAllMiss)
								{
									bool allowForce = !cfg.Pass2ForceIfPrevHitLost || prevHadHit;
									if (allowForce && segCount > 0 && segStart <= segEnd)
									{
										forceInstabilityThisPixel = true;
										forcePrevHitLostThisPixel = cfg.Pass2ForceIfPrevHitLost && prevHadHit;
										forceRepSegIndex = segStart + ((segEnd - segStart) / 2);
										if (statsEnabled)
										{
											_perfFrame.Pass2ForceInstabilityPixels++;
											if (forcePrevHitLostThisPixel)
												_perfFrame.Pass2ForcePrevHitLostPixels++;
										}
									}
								}
							}
							if (earlyOutFarThisPixel){
								EarlyOut("far early-out", cfg.EnableProfiling);
								break;
							}else{}

							if (softGateWatchdogTrippedThisPixel)
								break;

						if (hadHit)
							break;
					}
					if (budgetStop) break;

					// TLAS-pruning-only per-pixel candidate counters are committed once, after Pass2 finishes for the pixel.
					if (useGeomTlasPruningForStep)
					{
						// Accounting-only fallback: candidate-bearing pixel did pass2 candidate work but exited before any ray query.
						if (geomPixelHadAnyCandidatesThisPixel && _geomRayTestsTotalThisFrame == geomRayTestsAtPixelStart)
						{
							_geomRayTestsTotalThisFrame++;
							if (cfg.VerbosePerfLogs && _geomEligibleAccountingLogsRemaining > 0)
							{
								_geomEligibleAccountingLogsRemaining--;
								GD.Print($"[RenderHealth][EligibleRayWork] reason=pixel_candidate_no_query row={y} x={x} seg=-1 geomTlasCand=-1 broadphaseCand=-1");
							}
						}
						if (geomPixelHadAnyCandidatesThisPixel)
						{
							_geomPixelHadAnyCandidatesThisFrame++;
							_tileMetricCurrentSubtiles[subtileIndexThisPixel].CandidatePixels++;
						}
						else
						{
							_geomPixelNoCandidatesThisFrame++;
							_tileMetricCurrentSubtiles[subtileIndexThisPixel].NoCandidatePixels++;
						}
					}

					long geomRayTestsDeltaThisPixel = _geomRayTestsTotalThisFrame - geomRayTestsAtPixelStart;
					if (geomRayTestsDeltaThisPixel > 0)
						_tileMetricCurrentSubtiles[subtileIndexThisPixel].GeomRayTests += geomRayTestsDeltaThisPixel;

					if (useHybridBroadphase && noCandidatesThisPixel && !hadCandidatesThisPixel)
					{
						bandNoCandidatePixels++;
						_hybridNoCandidateCountThisFrame++;
						if (_broadphaseNoCandidateLogsRemaining > 0)
						{
							GD.Print($"[NoCandidates] policy=Hybrid row={_rowCursor} x={x} qray=0 overlap=0");
							_broadphaseNoCandidateLogsRemaining--;
						}
					}

					if (statsEnabled)
					{
						ulong physEnd = Time.GetTicksUsec();
						_perfFrame.AddPass2PhysUsec(physEnd - physStart);
					}
							if (!hadHit && cfg.FixtureDebugHitColoringEnabled && segCount > 0)
							{
								Vector3 terminalPoint = _segBuf[segOffset + (segCount - 1)].B;
								absorbedByInnerRadius = IsInsideAbsorbingSource(terminalPoint, fieldSnaps);
							}
							if (softGateAttemptsThisPixel > maxAttemptsAnyPixelThisBand)
								maxAttemptsAnyPixelThisBand = softGateAttemptsThisPixel;
							if (softGateSubdividesThisPixel > maxSubdividesAnyPixelThisBand)
								maxSubdividesAnyPixelThisBand = softGateSubdividesThisPixel;

							if (useThreadedPass2LocalAccumulation)
							{
								pass2ResolvedSamples!.Add(new Pass2ResolvedSample
								{
									X = x,
									Y = y,
									Stride = stride,
									GlobalPi = globalPi,
									SubtileIndex = subtileIndexThisPixel,
									SegCount = segCount,
									SegOffset = segOffset,
									HadHit = hadHit,
									AbsorbedByInnerRadius = absorbedByInnerRadius,
									NeedHitName = needHitName,
									PrevHadHit = prevHadHit,
									PrevHadHitForSoftGate = prevHadHitForSoftGate,
									TestedAnyInPass0ThisPixel = testedAnyInPass0ThisPixel,
									SoftGateHitThisPixel = softGateHitThisPixel,
									CandidateCount = telemetryCandidateCountThisPixel,
									QueryCount = telemetryQueryCountThisPixel,
									ResolveCount = telemetryResolveCountThisPixel,
									HitDistance = hitDistance,
									BestHp = bestHp,
									BestHn = bestHn,
									BestCid = bestCid,
									HitName = needHitName ? hitName : string.Empty
								});
								rowHadWritesThisPass = true;
								continue;
							}

							////
							////////////////////////
							ulong shadeStart = 0;
						if (shadeTimingEnabled) shadeStart = Time.GetTicksUsec();
						Color col = cfg.SkyColor;
						string fixtureHitKind = "miss";
						Color fixtureChosenDebugColor = cfg.SkyColor;
						bool fixtureDebugColorChosen = false;
						bool skipShading = rayCfg.RequireHitToRender && !hadHit;
						if (skipShading)
						{
							if (statsEnabled) _perfFrame.ShadingSkippedPixels++;
						}
						else if (hadHit)
						{
							bandHits++;
							_tileMetricCurrentSubtiles[subtileIndexThisPixel].Hits++;

							// track farthest hit seen
							if (hitDistance > frameMaxHit) frameMaxHit = hitDistance;

							// bestHn is a world-space collision normal; film distortion does not change collider geometry.
							switch (cfg.ShadingMode)
							{
								default:
								case FilmShadingMode.DepthHeatmap:
								{
									float far = cfg.AutoRangeDepth ? _rangeFar : cfg.Film.MaxDistance;
									float d = Mathf.Clamp(hitDistance / Mathf.Max(0.001f, far), 0f, 1f);
									col = Color.FromHsv(0.66f * (1f - d), 1f, 1f);
									break;
								}

								case FilmShadingMode.NormalRGB:
								{
									// hn is the physics collision normal for the nearest hit.
									Vector3 n = bestHn;
									if (cfg.FlipNormalToCamera)
									{
										Vector3 v = (camPosPass2 - bestHp).Normalized();
										if (n.Dot(v) < 0f) n = -n;
									}
									col = ShadeNormalRGB(n);
									break;
								}

								case FilmShadingMode.NdotV:
								{
									Vector3 v = camPosPass2 - bestHp;
									Vector3 n = bestHn;
									float rawDot;
									col = ShadeNdotV(n, v, out rawDot);
									if (statsEnabled && rawDot < 0f) _perfFrame.BackfaceNdotVHits++;
									if (cfg.FlipNormalToCamera && rawDot < 0f)
									{
										n = -n;
										col = ShadeNdotV(n, v, out _);
									}
									break;
								}

								case FilmShadingMode.TwoSidedNdotV:
								{
									Vector3 v = (camPosPass2 - bestHp).Normalized();
									Vector3 n = bestHn.Normalized();
									float ndv = n.Dot(v);
									col = ShadeNdotVAbs(ndv);
									break;
								}

							}

							if (logCenterSample)
								GD.Print($"Film hit: dist={hitDistance:0.000} name={hitName} mode={cfg.ShadingMode}");
						}
						fixtureHitKind = ClassifyFixtureHitKind(hadHit, absorbedByInnerRadius, bestCid);

						if (cfg.FixtureDebugHitColoringEnabled)
						{
							if (fixtureHitKind == "source")
							{
								if (cfg.FixtureDebugSourceHighlightEnabled)
								{
									col = cfg.FixtureDebugSourceHitColor;
									fixtureChosenDebugColor = col;
									fixtureDebugColorChosen = true;
								}
								else if (cfg.FixtureDebugColorAuthorityEnabled)
								{
									col = cfg.FixtureDebugBackgroundHitColor;
									fixtureChosenDebugColor = col;
									fixtureDebugColorChosen = true;
								}
							}
							else if (fixtureHitKind == "background")
							{
								if (cfg.FixtureDebugColorAuthorityEnabled)
								{
									col = cfg.FixtureDebugBackgroundHitColor;
									fixtureChosenDebugColor = col;
									fixtureDebugColorChosen = true;
								}
							}
							else if (fixtureHitKind == "absorbed")
							{
								col = cfg.FixtureDebugAbsorbedColor;
								fixtureChosenDebugColor = col;
								fixtureDebugColorChosen = true;
							}
							else
							{
								col = cfg.FixtureDebugMissColor;
								fixtureChosenDebugColor = col;
								fixtureDebugColorChosen = true;
							}
						}

						switch (fixtureHitKind)
						{
							case "source":
								_fixtureDebugSourceHitsThisRun++;
								break;
							case "background":
								_fixtureDebugBackgroundHitsThisRun++;
								break;
							case "unclassified":
								_fixtureDebugUnclassifiedHitsThisRun++;
								break;
							case "absorbed":
								_fixtureDebugAbsorbedHitsThisRun++;
								break;
							default:
								_fixtureDebugMissHitsThisRun++;
								break;
						}

						if (hadHit)
						{
							switch (fixtureHitKind)
							{
								case "source":
									_tileMetricCurrentSubtiles[subtileIndexThisPixel].SourceHits++;
									break;
								case "background":
									_tileMetricCurrentSubtiles[subtileIndexThisPixel].BackgroundHits++;
									break;
								case "unclassified":
									_tileMetricCurrentSubtiles[subtileIndexThisPixel].UnclassifiedHits++;
									break;
							}
						}

						int filled = FillPixelBlock(x, y, stride, col, filmW, filmH);
						if (telemetryHeatmapsEnabled)
						{
							AccumulateTelemetryBlock(_telemetryCandidateCount, x, y, stride, telemetryCandidateCountThisPixel);
							AccumulateTelemetryBlock(_telemetryQueryCount, x, y, stride, telemetryQueryCountThisPixel);
							AccumulateTelemetryBlock(_telemetryResolveCount, x, y, stride, telemetryResolveCountThisPixel);
						}
						rowHadWritesThisPass |= filled > 0;
						if (hadHit)
						{
							_fixtureFinalHitPixelCountThisRun += filled;
							FillPixelBlock(_fixtureFinalHitOnlyImg, x, y, stride, col, filmW, filmH);
						}
						Color categoricalColor = hadHit
							? FixtureCategoricalFinalHitColor
							: FixtureCategoricalRenderedNoHitColor;
						FillPixelBlock(_fixtureCategoricalFinalImg, x, y, stride, categoricalColor, filmW, filmH);
						bool fixtureTraceByKind = false;
						if (cfg.FixtureDebugTraceEnabled)
						{
							switch (fixtureHitKind)
							{
								case "source":
									if (fixtureTraceSourceRemaining > 0)
									{
										fixtureTraceSourceRemaining--;
										fixtureTraceByKind = true;
									}
									break;
								case "background":
									if (fixtureTraceBackgroundRemaining > 0)
									{
										fixtureTraceBackgroundRemaining--;
										fixtureTraceByKind = true;
									}
									break;
								case "absorbed":
									if (fixtureTraceAbsorbedRemaining > 0)
									{
										fixtureTraceAbsorbedRemaining--;
										fixtureTraceByKind = true;
									}
									break;
								default:
									if (fixtureTraceMissRemaining > 0)
									{
										fixtureTraceMissRemaining--;
										fixtureTraceByKind = true;
									}
									break;
							}
						}
						bool fixtureTraceByModulo = cfg.FixtureDebugTraceEnabled
							&& fixtureDebugTraceLogsRemainingThisStep > 0
							&& ShouldTraceFixtureDebugSample(x, y, cfg.FixtureDebugTraceSampleModulo);
						if (cfg.FixtureDebugTraceEnabled
							&& (fixtureTraceByKind || fixtureTraceByModulo))
						{
							Color finalWrittenColor = _img.GetPixel(x, y);
							GD.Print(
								$"[FixtureDebugTrace] frame={_frameIndex} row={y} x={x} kind={fixtureHitKind} hadHit={(hadHit ? 1 : 0)} " +
								$"cid={bestCid} debugChosen={FormatColorCompact(fixtureChosenDebugColor)} " +
								$"finalWritten={FormatColorCompact(finalWrittenColor)} auth={(cfg.FixtureDebugColorAuthorityEnabled ? 1 : 0)} " +
								$"chosen={(fixtureDebugColorChosen ? 1 : 0)}");
							if (fixtureDebugTraceLogsRemainingThisStep > 0)
								fixtureDebugTraceLogsRemainingThisStep--;
						}
						if (statsEnabled) _perfFrame.FilledPixels += filled;
						if (framePerfEnabled) bandFilledPixels += filled;
						if (shadeTimingEnabled)
						{
							ulong shadeEnd = Time.GetTicksUsec();
							ulong shadeUsec = shadeEnd - shadeStart;
							if (statsEnabled) _perfFrame.AddPass2ShadeUsec(shadeUsec);
							shadeUsecAccum += (long)shadeUsec;
						}
						if (_pass2PrevHadHit.Length > globalPi)
						{
							bool prevHit = prevHadHit;
							bool nowHit = hadHit;
							if (prevHit != nowHit) pixelDeltaChanged++;
							if (!prevHit && nowHit) pixelDeltaNewFilled++;
							if (!prevHit && nowHit && softGateHitThisPixel) softGateNewPixelFilled++;
							_pass2PrevHadHit[globalPi] = hadHit ? (byte)1 : (byte)0;
						}
							if (_pass2HadHitLostThisFrame.Length > globalPi)
								_pass2HadHitLostThisFrame[globalPi] = (prevHadHitForSoftGate && !hadHit && testedAnyInPass0ThisPixel) ? (byte)1 : (byte)0;
							////////////////////////////
						/// 

						////////////////////////
						/// Debug Block Addition
						///////////
						if (wantDbg)
						{
							ulong dbgStart = 0;
							if (statsEnabled) dbgStart = Time.GetTicksUsec();
							int pxStride = Math.Max(1, cfg.DebugEveryNPixels);

							// Sample a sparse grid (keeps overlay readable + fast)
							if ((x % pxStride) == 0 && (y % pxStride) == 0 && _dbgRayCount < cfg.DebugMaxFilmRays)
							{
								int rayIndex = _dbgRayCount++;

								_dbgOff[rayIndex] = _dbgPtWrite;

								// Build polyline points from the segments we already have
								// We want: p0, p1, p2, ... so: seg0.A, seg0.B, seg1.B, ...
								int w0 = _dbgPtWrite;

								if (segCount > 0)
								{
									// first point
									_dbgPts[_dbgPtWrite++] = _segBuf[segOffset + 0].A;

									// subsequent points
									int writeSegs = Math.Min(segCount, maxSeg);
									for (int si2 = 0; si2 < writeSegs; si2++)
									{
										_dbgPts[_dbgPtWrite++] = _segBuf[segOffset + si2].B;
									}
								}
								else
								{
									// no segments: still place a tiny stub so we can see "empty" rays if desired
									_dbgPts[_dbgPtWrite++] = _cam.GlobalPosition;
									_dbgPts[_dbgPtWrite++] = _cam.GlobalPosition + (-_cam.GlobalTransform.Basis.Z) * 0.25f;
								}

								_dbgCnt[rayIndex] = _dbgPtWrite - w0;

								// Hit payload for this pixel ray
								_dbgHits[rayIndex] = new RayBeamRenderer.HitPayload
								{
									Valid = hadHit,
									Position = bestHp,
									Normal = bestHn,
									Distance = hitDistance,
									ColliderId = bestCid,
									ColliderName = needHitName ? hitName : "<none>",
									Albedo = Colors.White
								};
								if (cfg.VerbosePerfLogs && _dbgHits[rayIndex].Valid != hadHit)
								{
									GD.Print($"Debug hit validity mismatch at rayIndex={rayIndex}");
								}
							}
							if (statsEnabled)
							{
								ulong dbgEnd = Time.GetTicksUsec();
								_perfFrame.AddOverlayBuildUsec(dbgEnd - dbgStart);
							}
						}
						///////////
						////////////////////////

						}
						if (budgetStop || renderStepAbort)
							break;
					}
					rowCompletedThisPass = !renderStepAbort && !budgetStop;
					RecordFixtureRowWriteOutcome(y, rowHadWritesThisPass, rowCompletedThisPass);
				}
				}
			}
			if (useThreadedPass2LocalAccumulation && !skipBandPhysics && !renderStepAbort)
			{
				RunThreadedPass2LocalAccumulationCommit();
			}
			if (framePerfEnabled) pass2Scope.Dispose();
			pass2CompletedThisStep = !budgetStop && !renderStepAbort;
			if (renderStepAbort && !budgetStop)
			{
				AbortRenderStep("watchdog");
				string maxMsReason = GetMaxMsStopReason();
				LogRenderStopOnce(maxMsReason);
				LogBudgetExitOnce(maxMsReason, _rowCursor);
				ForceAdvanceRowCursorOnStop(maxMsReason, yEnd);
				if (cfg.RenderStepBandLog) LogBandSummaryOnce("guard");
				ResetNoHitStall();
				ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);
				return;
			}

			if (softGateDisabledThisFrame && !softGateDisableLogged)
			{
				softGateDisableLogged = true;
				string reason = string.IsNullOrEmpty(softGateDisableReason) ? "overload" : softGateDisableReason;
				GD.PrintErr(
					$"[SoftGate][Disable] reason={reason} frame={_frameIndex} row={_rowCursor} " +
					$"attempts={_softGateAttemptsUsedThisFrame}/{pass2SoftGateMaxAttemptsPerFrameEffective} " +
					$"sub={_softGateSubdividedCallsUsedThisFrame}/{pass2SoftGateMaxSubdividedCallsPerFrameEffective} " +
					$"ms={renderStepWatch.ElapsedMilliseconds}");
			}

			ulong b1 = Time.GetTicksUsec(); // after PASS 2
			pass2EndUsec = b1;
			if (statsEnabled)
			{
				if (pass2EnvelopeUsecAccum > 0) _perfFrame.AddPass2EnvelopeUsec((ulong)pass2EnvelopeUsecAccum);
				if (pass2CandidateEvalUsecAccum > 0) _perfFrame.AddPass2CandidateEvalUsec((ulong)pass2CandidateEvalUsecAccum);
				if (pass2QueryUsecAccum > 0) _perfFrame.AddPass2QueryUsec((ulong)pass2QueryUsecAccum);
				if (pass2HitResolveUsecAccum > 0) _perfFrame.AddPass2HitResolveUsec((ulong)pass2HitResolveUsecAccum);
				if (pass2SoftGateUsecAccum > 0) _perfFrame.AddPass2SoftGateUsec((ulong)pass2SoftGateUsecAccum);
			}
			if (TargetMsPerFrame > 0)
			{
				double elapsedMs = (b1 - a0) / 1000.0;
				if (elapsedMs > 0.01)
				{
					double ratio = (double)TargetMsPerFrame / elapsedMs;
					int currentRows = _adaptiveRowsPerFrame > 0 ? _adaptiveRowsPerFrame : rowsPerFrame;
					int adjusted = Mathf.RoundToInt((float)(currentRows * ratio));
					adjusted = Mathf.Clamp(adjusted, Mathf.Max(1, MinRowsPerFrame), maxRowsPerFrame);
					_adaptiveRowsPerFrame = adjusted;
				}
			}
			if (statsEnabled)
			{
				_perfFrame.Hits += bandHits;
				_perfFrame.BandSegsIntegrated = bandSegsIntegrated;
				_perfFrame.BandSegsTested = bandSegsTested;
				_perfFrame.BandPhysicsQueries = bandPhysicsQueries;
				_perfFrame.Pass2SoftGateAttempts += p2SoftGateAttempts;
				_perfFrame.Pass2SoftGateHits += p2SoftGateHits;
				_perfFrame.SoftGateTriggered += softGateTriggered;
				_perfFrame.SoftGateAttempted += softGateAttempted;
				_perfFrame.SoftGateHitChangedResult += softGateHitChangedResult;
				_perfFrame.SoftGateNewPixelFilled += softGateNewPixelFilled;
				_perfFrame.SoftGateCandidateNull += softGateCandidateNull;
				_perfFrame.SoftGateLoopGuardTripped += softGateLoopGuardTripped;
				_perfFrame.SoftGateBudgetExceeded += softGateBudgetExceeded;
				_perfFrame.PixelDeltaChanged += pixelDeltaChanged;
				_perfFrame.PixelDeltaNewFilled += pixelDeltaNewFilled;
			}
			if (framePerfEnabled)
			{
				_framePerf.RaysTraced += bandTracedPixels;
				_framePerf.PixelsUpdated += bandFilledPixels;
				_framePerf.SegmentsIntegrated += bandSegsIntegrated;
				_framePerf.SegmentsTested += bandSegsTested;
				_framePerf.PhysicsQueries += bandPhysicsQueries;
				_framePerf.Hits += bandHits;
				_framePerf.EarlyOutFar += bandFarEarlyOuts;
				_framePerf.Pass2SoftGateAttempts += p2SoftGateAttempts;
				_framePerf.Pass2SoftGateHits += p2SoftGateHits;
				_framePerf.SoftGateTriggered += softGateTriggered;
				_framePerf.SoftGateAttempted += softGateAttempted;
				_framePerf.SoftGateHitChangedResult += softGateHitChangedResult;
				_framePerf.SoftGateNewPixelFilled += softGateNewPixelFilled;
				_framePerf.SoftGateCandidateNull += softGateCandidateNull;
				_framePerf.SoftGateLoopGuardTripped += softGateLoopGuardTripped;
				_framePerf.SoftGateBudgetExceeded += softGateBudgetExceeded;
				_framePerf.PixelDeltaChanged += pixelDeltaChanged;
				_framePerf.PixelDeltaNewFilled += pixelDeltaNewFilled;
			}
			if (softGateDebugEnabled)
			{
				_softGateFrame.TracedPixels += bandTracedPixels;
				_softGateFrame.FilledPixels += bandFilledPixels;
				_softGateFrame.SegsTotal += bandSegsIntegrated;
				_softGateFrame.SegsTested += bandSegsTested;
				_softGateFrame.Pass2Hits += bandHits;
				_softGateFrame.SoftGateHitChangedResult += softGateHitChangedResult;
				_softGateFrame.SoftGateNewPixelFilled += softGateNewPixelFilled;
				_softGateFrame.SoftGateBudgetExceeded += softGateBudgetExceeded;
				_softGateFrame.SoftGateAttemptsUsed += softGateAttemptsUsed;
				_softGateFrame.SoftGateSubdividedCallsUsed += softGateSubdividedCallsUsed;
			}
			if (softGateBandEnabled)
			{
				_softGateBand.TracedPixels = bandTracedPixels;
				_softGateBand.FilledPixels = bandFilledPixels;
				_softGateBand.SegsTotal = bandSegsIntegrated;
				_softGateBand.SegsTested = bandSegsTested;
				_softGateBand.Pass2Hits = bandHits;
				_softGateBand.SoftGateHitChangedResult = softGateHitChangedResult;
				_softGateBand.SoftGateNewPixelFilled = softGateNewPixelFilled;
				_softGateBand.SoftGateBudgetExceeded = softGateBudgetExceeded;
				_softGateBand.SoftGateAttemptsUsed = softGateAttemptsUsed;
				_softGateBand.SoftGateSubdividedCallsUsed = softGateSubdividedCallsUsed;
				GD.Print(BuildSoftGateBandSummary(yStart, yEnd, _softGateBand));
			}
			if (cfg.UseBandHitSkip && bandIndex >= 0 && bandIndex < _bandHitRate.Length)
			{
				float hitRate = bandTracedPixels > 0 ? (float)bandHits / bandTracedPixels : 0f;
				_bandHitRate[bandIndex] = hitRate;
				if (hitRate < cfg.BandSkipHitThreshold)
					_bandLowHitFrames[bandIndex]++;
				else
					_bandLowHitFrames[bandIndex] = 0;
			}

			// ---- Debug overlay draw ONCE per band ----
			ApplyHudOverlayVisualSettings();
			if (wantDbg && _filmOverlay != null)
			{
				ulong dbgOverlayStart = 0;
				if (statsEnabled) dbgOverlayStart = Time.GetTicksUsec();

				if (cfg.VerbosePerfLogs)
					ValidateDebugOverlayData(cfg.DebugMaxFilmRays);

				_filmOverlay.SetData(
					_cam,
					_dbgPts.AsSpan(0, _dbgPtWrite),
					_dbgOff.AsSpan(0, _dbgRayCount),
					_dbgCnt.AsSpan(0, _dbgRayCount),
					_dbgHits.AsSpan(0, _dbgRayCount),
					rayCfg.DebugNormalLen,
					_img,
					filmW,
					filmH,
					cfg.DebugEveryNPixels
				);

				if (statsEnabled)
				{
					ulong dbgOverlayEnd = Time.GetTicksUsec();
					_perfFrame.AddOverlayEnqueueUsec(dbgOverlayEnd - dbgOverlayStart);
				}
			}
			else if (_filmOverlay != null && rayCfg.HasRenderer && rayCfg.DebugOverlayOwnedByFilm)
			{
				_filmOverlay.ClearOverlay();
			}
			if (!wantDbg && _filmOverlay != null && _filmOverlay.DrawFilmGradientNormals)
			{
				_filmOverlay.SetFilmImage(_img, filmW, filmH, cfg.DebugEveryNPixels);
			}


			if (cfg.AutoRangeDepth && frameMaxHit > 0.0001f)
			{
				// write one sample per RenderStep call (band-based)
				_depthHistory[_depthHistWrite] = frameMaxHit;
				_depthHistWrite = (_depthHistWrite + 1) % _depthHistory.Length;

				// robust far plane estimate + safety multiplier
				float robust = RobustFarEstimate_Fallback(); // use fallback for reliability
				float targetFar = robust * cfg.AutoRangeSafety;

				// clamp
				targetFar = Mathf.Clamp(targetFar, cfg.AutoRangeMin, cfg.AutoRangeMax);

				// smooth
				_rangeFar = Mathf.Lerp(_rangeFar, targetFar, cfg.AutoRangeSmoothing);
			}
			if (cfg.VerbosePerfLogs && _rowCursor == 0 && cfg.AutoRangeDepth)
				GD.Print($"AutoRange Far={_rangeFar:0.###}  (MaxDistance export={cfg.Film.MaxDistance:0.###})");


			if (cfg.VerbosePerfLogs)
			{
				double avgStepsPerTracedPixel = bandTracedPixels > 0
					? (double)pass1StepsIntegrated / bandTracedPixels
					: 0.0;
				GD.Print($"Film band y=[{yStart},{yEnd}) hits={bandHits} avgStepsPerTracedPx={avgStepsPerTracedPixel:0.00}");
			}

			ulong updateStart = 0;
			if (statsEnabled) updateStart = Time.GetTicksUsec();
			PerfScope uploadScope = default;
			if (framePerfEnabled) uploadScope = new PerfScope(_framePerf, PerfStage.UploadTexture);
			_tex.Update(_img);
			if (framePerfEnabled) uploadScope.Dispose();
			if (statsEnabled) _perfFrame.AddFilmUpdateUsec(Time.GetTicksUsec() - updateStart);
			FinalizePresentedFrameRefreshMetrics();
			if (statsEnabled)
			{
				_perfFrame.PresentPixelsUpdated = _lastPresentedPixelsUpdated;
				_perfFrame.PresentCoverageRatio = _lastPresentedCoverageRatio;
				_perfFrame.FramesToFullRefresh = _lastFramesToFullRefresh;
				_perfFrame.TimeToFullRefreshMs = _lastTimeToFullRefreshMs;
				_perfFrame.EffectiveFullRefreshFps = _lastEffectiveFullRefreshFps;
				_perfFrame.FullRefreshMeasured = _lastFullRefreshMeasured;
			}

			if (budgetStop) LogBudgetStopOnce();
			if (budgetStop)
			{
				bandCompletedThisStep = pass2CompletedThisStep && (pass1CompletedThisStep || pass1SkippedThisStep);
				bool isTimeBudget = budgetStopReason == "update_every_frame_budget"
					|| budgetStopReason == "renderstep_max_ms"
					|| budgetStopReason == "render_step_max_ms"
					|| budgetStopReason == "target_ms_per_frame";
				if (budgetStopReason == "softgate_attempt_cap")
				{
					FinalizeBandAndAdvance("softgate_attempt_cap", yStart, yEnd, bandHits, "");
				}
				else if (isTimeBudget && !bandCompletedThisStep)
				{
					LogRenderStopOnce(budgetStopReason);
					if (!ForceAdvanceOnNoHit(budgetStopReason, "zero-hit-advance", true))
					{
						if (cfg.RenderStepBandLog) LogBandSummaryOnce("budget");
						MarkBandIncompleteThisFrame(budgetStopReason, yStart, yEnd);
					}
					return;
				}
				else
				{
					FinalizeBandAndAdvance(budgetStopReason, yStart, yEnd, bandHits, "");
				}
			}
			else
			{
				int nextRowCursor = yEnd;
				_rowCursor = Mathf.Clamp(nextRowCursor, 0, filmH);
				bool bandAdvanced = yEnd != yStart;
				if (pendingPass2)
				{
					_pendingBandRowStart = -1;
					_pendingBandRowCount = 0;
					_pendingBandHasPass1 = false;
				}
				if (_rowCursor < filmH)
					EnsureForwardProgress("end", false, _rowCursor, bandHits, false);
				if (_rowCursor >= filmH)
				{
					_rbr.EmitBoundaryValidationSummary($"film={filmW}x{filmH}");
					ResetRowCursor("completed");
					if (QuitAfterCompletedPass)
					{
						UpdateEveryFrame = false;
						cfg.UpdateEveryFrame = false;
						CallDeferred(nameof(QuitTreeDeferred));
					}
				}
				bandCommittedThisStep = bandAdvanced;
				if (bandAdvanced)
					if (cfg.RenderStepBandLog) LogBandSummaryOnce(bandHits == 0 ? "zero-hit-advance" : "normal");
				else
					ForceAdvanceOnNoHit("guard_no_progress", "zero-hit-advance", false);
				if (bandAdvanced) ResetNoHitStall();
			}
			ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);

			ulong t1 = Time.GetTicksUsec();
			if (cfg.VerbosePerfLogs)
			{
				GD.Print($"RenderStep {(t1 - t0)/1000.0:0.00} ms  rows={bandH}  jobs={jobs}  hits={bandHits}");
				GD.Print($"pass1={(a1-a0)/1000.0:0.00}ms  pass2={(b1-a1)/1000.0:0.00}ms  total={(b1-a0)/1000.0:0.00}ms");
			}
			
			if (statsEnabled)
			{
				_perfFrame.SegsSkippedByPass2Stride += subRaysSkippedByPass2Stride;
				_perfFrame.SegsForcedTestByPass2Stride += subRaysForcedByPass2Stride;
				_perfFrame.Pass2StrideSum += pass2StrideSum;
				_perfFrame.Pass2StrideCount += pass2StrideCount;
			}
			if (framePerfEnabled && shadeUsecAccum > 0)
			{
				long shadeTicks = (long)(shadeUsecAccum * (double)Stopwatch.Frequency / 1_000_000.0);
				_framePerf.AddTicks(PerfStage.Shade, shadeTicks);
			}
			if (statsEnabled && _rowCursor == 0)
			{
				_perfFrame.ShadingSkippedNoHits = _perfFrame.RequireHitToRender
					&& _perfFrame.Hits == 0
					&& _perfFrame.TracedPixels > 0
					&& _perfFrame.ShadingSkippedPixels >= _perfFrame.TracedPixels;
				_perfStats.FinalizeAndPrint(ref _perfFrame, cfg.VerbosePerfLogs);
			}
			if (framePerfEnabled && _rowCursor == 0)
			{
				int logEvery = Mathf.Max(1, cfg.FramePerfLogEveryNFrames);
				bool shouldLogFramePerf = cfg.FramePerfVerbose || (_frameIndex % logEvery) == 0;
				if (shouldLogFramePerf)
				{
					GD.Print("FramePerf: " + _framePerf.ToOneLineSummary());
					double testedPerPixel = _framePerf.RaysTraced > 0
						? (double)_framePerf.SegmentsTested / _framePerf.RaysTraced
						: 0.0;
					long physQ = _framePerf.PhysicsQueries;
					if (_hasPerfDeltaBaseline)
					{
						string testedDelta = (testedPerPixel - _lastTestedSegsPerPixel).ToString("+0.###;-0.###;+0.###");
						string physQDelta = (physQ - _lastPhysQ).ToString("+0;-0;0");
						GD.Print($"FramePerf delta: tested/px={testedPerPixel:0.###} (d{testedDelta}) physQ={physQ} (d{physQDelta})");
					}
					else
					{
						GD.Print($"FramePerf delta: tested/px={testedPerPixel:0.###} physQ={physQ} (baseline)");
						_hasPerfDeltaBaseline = true;
					}
					_lastTestedSegsPerPixel = testedPerPixel;
					_lastPhysQ = physQ;
				}
			}
			if (_rowCursor == 0)
				LogExperimentalSubtileSchedulerFrameSummaryIfNeeded();
			if (_rowCursor == 0)
				LogTilePrioritySimulationFrameSummaryIfNeeded();
			if (cfg.RenderStepPhaseLog) LogRenderPhase("end");
			if (softGateDebugEnabled && _rowCursor == 0)
			{
				string extraContext =
					"px[traced=" + _softGateFrame.TracedPixels +
					" filled=" + _softGateFrame.FilledPixels +
					" eff=" + _softGateFrame.EffectivePixels +
					"] segs[total=" + _softGateFrame.SegsTotal +
					" tested=" + _softGateFrame.SegsTested +
					"] pass2Hits=" + _softGateFrame.Pass2Hits;
				GD.Print(BuildSoftGateFrameSummary(_softGateFrame, extraContext));
				if (softGateCfg.DebugSummaryPerFrame && _softGateSummaryLogsRemaining > 0)
				{
					GD.Print(BuildSoftGateDebugSummary(_softGateFrame));
					_softGateSummaryLogsRemaining--;
				}

				bool haveAutoRangeFar = cfg.AutoRangeDepth && float.IsFinite(_rangeFar) && _rangeFar > 0f;
				bool haveAvgSegPerPixel = _softGateFrame.TracedPixels > 0 && _softGateFrame.SegsTotal > 0;
				if (haveAutoRangeFar && haveAvgSegPerPixel)
				{
					double avgSegPerPixel = (double)_softGateFrame.SegsTotal / Math.Max(1.0, _softGateFrame.TracedPixels);
					double estimateAvgSegLen = _rangeFar / Math.Max(1.0, avgSegPerPixel);
					if (_softGateFrame.SoftGateMinSegLen > 1.5f * estimateAvgSegLen)
					{
						GD.Print($"[SoftGate][WARN] minSegLen={_softGateFrame.SoftGateMinSegLen:0.###} > 1.5x estAvgSegLen={estimateAvgSegLen:0.###}; consider minSegLen~{estimateAvgSegLen:0.###}.");
					}
				}

				if (_softGateFrame.SoftGateForced > 0 && _softGateFrame.SoftGateAttempts == 0)
				{
					GetTopSoftGateSkipReasons(_softGateFrame, out string top1, out long top1Count, out string top2, out long top2Count);
					GD.Print("[SoftGate][WARN] forced>0 but attempts=0: topSkips="
						+ top1 + "(" + top1Count + "), "
						+ top2 + "(" + top2Count + ") "
						+ "budget{px=" + _softGateFrame.Pass2SoftGateMaxAttemptsPerPixel
						+ " frame=" + _softGateFrame.Pass2SoftGateMaxAttemptsPerFrame
						+ " sub=" + _softGateFrame.Pass2SoftGateMaxSubdividedCallsPerFrame
						+ " score=" + _softGateFrame.SoftGateMaxAttemptsPerFrameV2
						+ "}");
				}
				if (pass2SoftGateUseRayBeamSettingsActive
					&& _softGateFrame.SoftGateEnabled
					&& _softGateFrame.SoftGateAttempts == 0)
				{
					GetTopSoftGateSkipReasons(_softGateFrame, out string top1, out long top1Count, out _, out _);
					if (top1 == "segLenTooShort" && top1Count > 0 && _softGateSummaryLogsRemaining > 0)
					{
						_softGateSummaryLogsRemaining--;
						GD.Print($"[SoftGate][WARN] seglen skips dominate with attempts=0 while using RayBeam settings; consider lowering Pass2SoftGateMinSegLenSteps (cur={softGateCfg.MinSegLenSteps:0.###}).");
					}
				}
				if (_softGateFrame.SoftGateEnabled && _softGateFrame.SoftGateConsidered > 0 && _softGateFrame.SoftGateAttempts == 0)
				{
					GD.Print("[SoftGate][WARN] enabled but no attempts: check gating (minSegLen/score/random) summary above.");
				}
			}

			bool noCandidateBand = processedPixelsThisBand > 0 && !bandHadCandidates;
			bool noRowAdvanceThisStep = renderHealthRowCursorBefore == _rowCursor;
			if (noCandidateBand && noRowAdvanceThisStep)
			{
				if (_noCandidateBandLastRowCursor == _rowCursor)
					_noCandidateBandStallSteps++;
				else
				{
					_noCandidateBandLastRowCursor = _rowCursor;
					_noCandidateBandStallSteps = 1;
				}
			}
			else
			{
				_noCandidateBandStallSteps = 0;
				_noCandidateBandLastRowCursor = _rowCursor;
			}

			if (noCandidateBand && noRowAdvanceThisStep && _noCandidateBandStallSteps >= RenderHealthStallThreshold)
			{
				LogBudgetExitOnce("guard_no_candidates_band", _rowCursor);
				ForceAdvanceRowCursorOnStop("guard_no_candidates_band", yEnd);
				_noCandidateBandStallSteps = 0;
				_noCandidateBandLastRowCursor = _rowCursor;
			}

			bool noHitBand = processedPixelsThisBand > 0 && bandHits == 0 && bandHadCandidates;
			if (noHitBand && noRowAdvanceThisStep)
			{
				if (_noHitBandLastRowCursor == _rowCursor)
					_noHitBandStallSteps++;
				else
				{
					_noHitBandLastRowCursor = _rowCursor;
					_noHitBandStallSteps = 1;
				}
			}
			else
			{
				_noHitBandStallSteps = 0;
				_noHitBandLastRowCursor = _rowCursor;
			}

			if (noHitBand && noRowAdvanceThisStep && _noHitBandStallSteps >= RenderHealthStallThreshold)
			{
				GD.PrintErr($"[WATCHDOG] no-hit band y=[{yStart},{yEnd}) repeats={_noHitBandStallSteps} -> forceAdvance");
				LogBudgetExitOnce("guard_no_hit_band", _rowCursor);
				ForceAdvanceRowCursorOnStop("guard_no_hit_band", yEnd);
				_noHitBandStallSteps = 0;
				_noHitBandLastRowCursor = _rowCursor;
			}
		}
		finally
		{
			if (framePerfEnabled) frameScope.Dispose();
			if (bandAttemptedThisStep && bandH > 0)
			{
				bool bandWasSkipped = skipBandPhysicsForDiagnostics && processedPixelsThisBand > 0;
				bool bandWasProcessed = processedPixelsThisBand > 0 && !bandWasSkipped;
				bool bandWasZeroHit = bandWasProcessed && bandHits == 0;
				RecordFixtureRowParticipationBand(yStart, yEnd, bandWasProcessed, bandWasSkipped, bandWasZeroHit);
			}
			rowCursorEnd = _rowCursor;
			if (processedPixelsThisStep > 0 && rowCursorEnd == rowCursorStart)
			{
				_noRowProgressRepeats++;
			}
			else
			{
				_noRowProgressRepeats = 0;
			}
			if (processedPixelsThisStep > 0
				&& rowCursorEnd == rowCursorStart
				&& _noRowProgressRepeats >= Math.Max(1, cfg.RenderStepNoRowProgressRepeatLimit))
			{
				int filmHLocal = _filmHeight;
				int advanceRows = bandH > 0 ? bandH : Math.Max(1, rowsPerFrame);
				int forcedRow = filmHLocal > 0
					? Math.Min(rowCursorEnd + advanceRows, filmHLocal)
					: rowCursorEnd + advanceRows;
				GD.PrintErr($"[RenderStep][WATCHDOG] noRowProgress processedPixels={processedPixelsThisStep} repeats={_noRowProgressRepeats} -> forceAdvance");
				LogBudgetExitOnce("guard_no_row_progress", _rowCursor);
				_rowCursor = forcedRow;
				_noRowProgressRepeats = 0;
				rowCursorEnd = _rowCursor;
			}
			double avgStepsPerTracedPixel = bandTracedPixels > 0
				? (double)pass1StepsIntegrated / bandTracedPixels
				: 0.0;
			long renderStepEndTimestamp = Stopwatch.GetTimestamp();
			double renderStepWallMs = (renderStepEndTimestamp - renderStepStartTimestamp) * 1000.0 / Stopwatch.Frequency;
			string healthExitReason = budgetStop
				? budgetStopReason
				: (renderStepAbortLogged ? renderStepAbortReason : "");
			RecordRenderHealthSample(
				renderHealthRowCursorBefore,
				_rowCursor,
				bandAttemptedThisStep ? 1 : 0,
				bandTracedPixels,
				bandHits,
				_quickRayZeroCountThisFrame,
				_hybridFallbackCountThisFrame,
				_hybridFallbackHitCountThisFrame,
				_hybridFallbackMissCountThisFrame,
				_hybridNoCandidateCountThisFrame,
				_geomCandidatesTotalThisFrame,
				_geomCandidatesSegmentsThisFrame,
				_geomSegmentsQueriedThisFrame,
				_geomSegWithCandidatesThisFrame,
				_geomSegZeroCandidatesThisFrame,
				_geomPixelProcessedThisFrame,
				_geomPixelHadAnyCandidatesThisFrame,
				_geomPixelNoCandidatesThisFrame,
				_geomHitAcceptedThisFrame,
				_geomHitRejectedThisFrame,
				_geomRayTestsTotalThisFrame,
				_geomRayTestsAcceptedThisFrame,
				_geomRayTestsRejectedThisFrame,
				pass2SampledSegments,
				pass2RadiusSum,
				pass2RadiusMax,
				pass2EnvDiagSum,
				pass2EnvDiagMax,
				pass2EnvelopeInflationSum,
				pass2EnvelopeInflationMax,
				pass2CandidateCount0,
				pass2CandidateCount1To2,
				pass2CandidateCount3To8,
				pass2CandidateCount9To32,
				pass2CandidateCount33Plus,
				_geomPruneAuditSamplesThisFrame,
				_geomPruneAuditFalseNegThisFrame,
				_geomPruneAuditFalsePosThisFrame,
				_geomPruneAuditCandidateZeroButBaselineHitThisFrame,
				avgStepsPerTracedPixel,
				renderStepWallMs,
				healthExitReason,
				useGeomTlasPruningForStep,
				geomPruneRequestedForStep,
				cfg.DebugGeomPruneAuditEnabled);
			LogExperimentalSubtileSchedulerForCurrentBand(_renderHealthStepIndex, healthExitReason);
			SimulateTilePriorityOrderForCurrentBand(_renderHealthStepIndex, healthExitReason);
			for (int subtileIndex = 0; subtileIndex < _tileMetricCurrentSubtileCount; subtileIndex++)
			{
				int subtileX = subtileIndex * _tileMetricCurrentSubtileWidth;
				int subtileWidth = Math.Max(0, Math.Min(_tileMetricCurrentSubtileWidth, filmW - subtileX));
				if (subtileWidth <= 0)
					continue;
				string stableId = BuildTileMetricsStableId(
					_tileMetricCurrentBandIndex,
					subtileIndex,
					subtileX,
					_tileMetricCurrentBandY,
					subtileWidth,
					_tileMetricCurrentBandHeight);
				var tileDescriptor = new TileDescriptor(
					_renderHealthStepIndex,
					_tileMetricCurrentBandIndex,
					subtileIndex,
					subtileX,
					_tileMetricCurrentBandY,
					subtileWidth,
					_tileMetricCurrentBandHeight,
					stableId);
				TileMetricAccumulator subtile = _tileMetricCurrentSubtiles[subtileIndex];
					RecordTileMetricSample(
						in tileDescriptor,
						subtile.RaysTraced,
						subtile.Hits,
						subtile.SourceHits,
						subtile.BackgroundHits,
						subtile.UnclassifiedHits,
						subtile.CandidateReferences,
						subtile.CandidateSegments,
					subtile.NoCandidatePixels,
					subtile.CandidatePixels,
					subtile.GeomPixelsProcessed,
					subtile.GeomRayTests,
					healthExitReason);
			}
			SaveTileMetricBandHistoryForCurrentBand();
			bool stalledNow = _renderHealthStallSteps >= RenderHealthStallThreshold;
			if (stalledNow && !_rowStallActive)
			{
				_rowStallActive = true;
				LogBudgetExitOnce("row_stall", _rowCursor);
			}
			else if (!stalledNow && _rowStallActive)
			{
				_rowStallActive = false;
				LogBudgetExitOnce("row_progress", _rowCursor);
			}
			_lastBandCommitted = bandCommittedThisStep;
			_lastRenderStepRowCursor = _rowCursor;
			_lastRenderStepBandStart = yStart;
			_lastRenderStepBandEnd = yEnd;
			_lastProcessedPixelsThisBand = processedPixelsThisBand;
			_hasLastProcessedPixelsThisBand = true;
			CaptureAdaptiveEnvelopePreviousTelemetrySnapshot();
			Interlocked.Exchange(ref _renderStepActive, 0);
		}
		}
		finally
		{
			if (researchAppliedRayMarchClamp && _rbr != null && researchRestoreSnapshot.HasRenderer)
			{
				_rbr.StepsPerRay = researchRestoreSnapshot.StepsPerRay;
				_rbr.MinStepLength = researchRestoreSnapshot.MinStepLength;
				_rbr.MaxStepLength = researchRestoreSnapshot.MaxStepLength;
				_rbr.StepLength = researchRestoreSnapshot.StepLength;
			}
		}
	}

	private static float ReadFloat(Node obj, StringName prop, float fallback)
	{
		if (obj == null) return fallback;
		Variant v = obj.Get(prop);
		return v.VariantType switch
		{
			Variant.Type.Float => (float)v,
			Variant.Type.Int => (int)v,
			_ => fallback
		};
	}

	private static void SortLongSpan(Span<long> data, int count)
	{
		if (count <= 1) return;
		for (int i = 1; i < count; i++)
		{
			long key = data[i];
			int j = i - 1;
			while (j >= 0 && data[j] > key)
			{
				data[j + 1] = data[j];
				j--;
			}
			data[j + 1] = key;
		}
	}

	private static bool ContainsSortedLong(ReadOnlySpan<long> data, int count, long value)
	{
		int lo = 0;
		int hi = count - 1;
		while (lo <= hi)
		{
			int mid = lo + ((hi - lo) >> 1);
			long m = data[mid];
			if (m == value) return true;
			if (m < value) lo = mid + 1;
			else hi = mid - 1;
		}
		return false;
	}

	private static int DedupSortedLong(Span<long> data, int count)
	{
		if (count <= 1) return count;
		int w = 1;
		long prev = data[0];
		for (int r = 1; r < count; r++)
		{
			long v = data[r];
			if (v != prev)
			{
				data[w++] = v;
				prev = v;
			}
		}
		return w;
	}

	private enum GeomRejectSampleCause
	{
		CidNotInGeometryList = 0,
		CidInGeometryListNotInCandidates = 1,
		CandidateContainsCid = 2
	}

	private GeomRejectSampleCause LogGeomRejectSample(
		ulong cid,
		string cname,
		in Aabb3 envelope,
		ReadOnlySpan<long> candidateIds,
		int candidateCount,
		GeometryEntitySOA geomEntities)
	{
		var envMin = envelope.Min;
		var envMax = envelope.Max;
		float envDiag = envelope.Extents.Length();
		long cidLong = unchecked((long)cid);
		bool candidateHasCid = false;
		int candidateScanCount = Math.Min(candidateCount, candidateIds.Length);
		for (int i = 0; i < candidateScanCount; i++)
		{
			if (candidateIds[i] == cidLong)
			{
				candidateHasCid = true;
				break;
			}
		}

		int foundGeomIndex = -1;
		if (geomEntities != null)
		{
			var ids = geomEntities.GodotInstanceIds;
			for (int i = 0; i < ids.Length; i++)
			{
				if (ids[i] == cidLong)
				{
					foundGeomIndex = i;
					break;
				}
			}
		}

		GeomRejectSampleCause cause;
		if (foundGeomIndex < 0) cause = GeomRejectSampleCause.CidNotInGeometryList;
		else if (candidateHasCid) cause = GeomRejectSampleCause.CandidateContainsCid;
		else cause = GeomRejectSampleCause.CidInGeometryListNotInCandidates;

		var sb = new StringBuilder(256);
		sb.Append("[GeomRejectSample] cid=").Append(cid)
			.Append(" cname=").Append(cname ?? "<null>")
			.Append(" cause=")
			.Append(cause == GeomRejectSampleCause.CidNotInGeometryList
				? "CID_NOT_IN_GEOMETRY_LIST"
				: cause == GeomRejectSampleCause.CandidateContainsCid
					? "CID_IN_CANDIDATES_UNEXPECTED"
					: "CID_IN_GEOMETRY_LIST_NOT_IN_CANDIDATES")
			.Append(" envDiag=").Append(envDiag.ToString("0.###"))
			.Append(" envMin=(").Append(envMin.X.ToString("0.###")).Append(",").Append(envMin.Y.ToString("0.###")).Append(",").Append(envMin.Z.ToString("0.###")).Append(")")
			.Append(" envMax=(").Append(envMax.X.ToString("0.###")).Append(",").Append(envMax.Y.ToString("0.###")).Append(",").Append(envMax.Z.ToString("0.###")).Append(")")
			.Append(" candCount=").Append(candidateCount)
			.Append(" candHasCid=").Append(candidateHasCid ? "1" : "0")
			.Append(" cidInGeometryList=").Append(foundGeomIndex >= 0 ? "1" : "0");

		if (foundGeomIndex >= 0 && geomEntities != null && (uint)foundGeomIndex < (uint)geomEntities.WorldBounds.Length)
		{
			var geomBounds = geomEntities.WorldBounds[foundGeomIndex];
			var geomMin = geomBounds.Min;
			var geomMax = geomBounds.Max;
			sb.Append(" geomIndex=").Append(foundGeomIndex)
				.Append(" geomAabbMin=(").Append(geomMin.X.ToString("0.###")).Append(",").Append(geomMin.Y.ToString("0.###")).Append(",").Append(geomMin.Z.ToString("0.###")).Append(")")
				.Append(" geomAabbMax=(").Append(geomMax.X.ToString("0.###")).Append(",").Append(geomMax.Y.ToString("0.###")).Append(",").Append(geomMax.Z.ToString("0.###")).Append(")");
		}

		int previewCount = Math.Min(8, candidateCount);
		sb.Append(" candIds=[");
		for (int i = 0; i < previewCount; i++)
		{
			if (i > 0) sb.Append(",");
			sb.Append(candidateIds[i]);
		}
		sb.Append("]");
		if (candidateCount > previewCount)
		{
			ulong hash = HashLongSpanFNV(candidateIds, candidateCount);
			sb.Append(" candHash=0x").Append(hash.ToString("X"));
		}

		GD.Print(sb.ToString());
		return cause;
	}

	private static ulong HashLongSpanFNV(ReadOnlySpan<long> data, int count)
	{
		ulong hash = 1469598103934665603UL;
		int n = Math.Min(count, data.Length);
		for (int i = 0; i < n; i++)
		{
			hash ^= unchecked((ulong)data[i]);
			hash *= 1099511628211UL;
		}
		return hash;
	}

	private static uint HashPruneAuditSample(int frameIndex, int stepIndex, int pixelIndex, int segmentIndex, int pass)
	{
		unchecked
		{
			uint h = 2166136261u;
			h = (h ^ (uint)frameIndex) * 16777619u;
			h = (h ^ (uint)stepIndex) * 16777619u;
			h = (h ^ (uint)pixelIndex) * 16777619u;
			h = (h ^ (uint)segmentIndex) * 16777619u;
			h = (h ^ (uint)pass) * 16777619u;
			// Final avalanche to improve distribution on low-entropy inputs.
			h ^= h >> 16;
			h *= 2246822519u;
			h ^= h >> 13;
			h *= 3266489917u;
			h ^= h >> 16;
			return h;
		}
	}

	private RayBeamRenderer.FieldSourceSnap[] GetFieldSourceSnaps(in EffectiveConfig cfg, int frameIndex, out bool hasSources, out bool cacheRefreshed)
	{
		cacheRefreshed = false;
		if (!cfg.UseFieldSourceCache)
		{
			var fieldSources = GetTree().GetNodesInGroup("field_sources");
			var snaps = _rbr.SnapshotFieldSources(fieldSources);
			hasSources = snaps.Length > 0;
			return snaps;
		}

		bool needsRefresh = false;
		int refreshInterval = Mathf.Max(1, cfg.FieldSourceRefreshIntervalFrames);
		if (frameIndex - _fieldSourceLastRefreshFrame >= refreshInterval)
			needsRefresh = true;

		if (!needsRefresh && _fieldSourceCount > 0)
		{
			for (int i = 0; i < _fieldSourceCount; i++)
			{
				Node node = _fieldSourceNodes[i];
				if (node == null || !IsInstanceValid(node))
				{
					needsRefresh = true;
					break;
				}

				ulong id = node.GetInstanceId();
				if (_fieldSourceIds[i] != id)
				{
					needsRefresh = true;
					break;
				}

				if (node is Node3D n3)
				{
					Transform3D t = n3.GlobalTransform;
					if (!TransformEqualApprox(t, _fieldSourceXforms[i]))
					{
						needsRefresh = true;
						break;
					}
				}
			}
		}

		if (needsRefresh)
		{
			RefreshFieldSourceCache(frameIndex);
			cacheRefreshed = true;
		}

		hasSources = _fieldSourceSnaps.Length > 0;
		return _fieldSourceSnaps;
	}

	private void RefreshFieldSourceCache(int frameIndex)
	{
		var fieldSources = GetTree().GetNodesInGroup("field_sources");
		_fieldSourceSnaps = _rbr.SnapshotFieldSources(fieldSources);

		EnsureFieldSourceCacheCapacity(_fieldSourceSnaps.Length);
		_fieldSourceCount = 0;

		foreach (var node in fieldSources)
		{
			if (node is not FieldSource3D fs) continue;
			if (_fieldSourceCount >= _fieldSourceNodes.Length) break;

			_fieldSourceNodes[_fieldSourceCount] = fs;
			_fieldSourceXforms[_fieldSourceCount] = fs.GlobalTransform;
			_fieldSourceIds[_fieldSourceCount] = fs.GetInstanceId();
			_fieldSourceCount++;
		}

		_fieldSourceLastRefreshFrame = frameIndex;
	}

	private void EnsureFieldSourceCacheCapacity(int count)
	{
		if (_fieldSourceNodes.Length < count) Array.Resize(ref _fieldSourceNodes, count);
		if (_fieldSourceXforms.Length < count) Array.Resize(ref _fieldSourceXforms, count);
		if (_fieldSourceIds.Length < count) Array.Resize(ref _fieldSourceIds, count);
	}

	private void RefreshFixtureDebugSourceIds(string sourceGroup)
	{
		_fixtureDebugSourceIds.Clear();
		_fixtureDebugBackgroundIds.Clear();
		_fixtureDebugHasExplicitBackgroundGroup = false;
		SceneTree tree = GetTree();
		if (tree == null)
		{
			return;
		}

		string group = string.IsNullOrWhiteSpace(sourceGroup) ? "fixture_source" : sourceGroup;
		var nodes = tree.GetNodesInGroup(group);
		foreach (var node in nodes)
		{
			if (node is Node sourceNode)
			{
				_fixtureDebugSourceIds.Add(sourceNode.GetInstanceId());
			}
		}

		string backgroundGroup = string.IsNullOrWhiteSpace(FixtureDebugBackgroundGroup)
			? string.Empty
			: FixtureDebugBackgroundGroup.Trim();
		if (backgroundGroup.Length == 0)
		{
			return;
		}

		var backgroundNodes = tree.GetNodesInGroup(backgroundGroup);
		foreach (var node in backgroundNodes)
		{
			if (node is Node backgroundNode)
			{
				_fixtureDebugBackgroundIds.Add(backgroundNode.GetInstanceId());
			}
		}

		_fixtureDebugHasExplicitBackgroundGroup = _fixtureDebugBackgroundIds.Count > 0;
	}

	private string ClassifyFixtureHitKind(bool hadHit, bool absorbedByInnerRadius, ulong colliderId)
	{
		if (!hadHit)
		{
			return absorbedByInnerRadius ? "absorbed" : "miss";
		}

		if (_fixtureDebugSourceIds.Contains(colliderId))
		{
			return "source";
		}

		if (_fixtureDebugHasExplicitBackgroundGroup)
		{
			return _fixtureDebugBackgroundIds.Contains(colliderId) ? "background" : "unclassified";
		}

		return "background";
	}

	private static string FormatTileClassShare(long hits, long totalHits)
	{
		return totalHits > 0
			? ((double)hits / totalHits).ToString("0.###", CultureInfo.InvariantCulture)
			: "na";
	}

	private static bool ShouldTraceFixtureDebugSample(int x, int y, int modulo)
	{
		int m = Math.Max(1, modulo);
		int hx = (x * 73856093) ^ (y * 19349663);
		if (hx < 0)
			hx = -hx;
		return (hx % m) == 0;
	}

	private static string FormatColorCompact(Color c)
	{
		return $"({c.R:0.###},{c.G:0.###},{c.B:0.###},{c.A:0.###})";
	}

	private static bool IsInsideAbsorbingSource(Vector3 point, RayBeamRenderer.FieldSourceSnap[] sources)
	{
		if (sources == null || sources.Length == 0)
		{
			return false;
		}

		for (int i = 0; i < sources.Length; i++)
		{
			ref readonly RayBeamRenderer.FieldSourceSnap source = ref sources[i];
			if (!source.Enabled)
			{
				continue;
			}

			if ((source.ModeFlags & FieldMath.ModeFlagAbsorbInsideInnerRadius) == 0u)
			{
				continue;
			}

			float inner = source.RInner;
			if (inner <= 0f)
			{
				continue;
			}

			if (point.DistanceSquaredTo(source.Center) < (inner * inner))
			{
				return true;
			}
		}

		return false;
	}

	private void EnsureGeomScratchCapacity(int n)
	{
		if (_geomCandidatesScratch.Length < n)
			_geomCandidatesScratch = new int[n];
		if (_geomCandidateInstanceIdsScratch.Length < n)
			_geomCandidateInstanceIdsScratch = new long[n];
	}

	private static bool IsGeometryTLASUsable(RendererCore.Geometry.GeometryTLAS geomTlas, GeometryEntitySOA geomEntities)
	{
		if (geomTlas == null || geomEntities == null)
			return false;
		if (geomEntities.Count <= 0)
			return false;
		if (geomEntities.WorldBounds == null || geomEntities.WorldBounds.Length <= 0)
			return false;
		if (geomTlas.Nodes == null || geomTlas.Nodes.Length == 0 || geomTlas.RootIndex < 0)
			return false;
		if (geomTlas.LeafGeometryIds == null || geomTlas.LeafGeometryIds.Length == 0)
			return false;
		return true;
	}

	private static bool TransformEqualApprox(Transform3D a, Transform3D b)
	{
		return a.Basis.IsEqualApprox(b.Basis) && a.Origin.IsEqualApprox(b.Origin);
	}

	private void EnsureBandHitHistory(int filmHeight, int rowsPerFrame)
	{
		int bandCount = (filmHeight + rowsPerFrame - 1) / rowsPerFrame;
		if (_bandHitRate.Length != bandCount)
		{
			_bandHitRate = new float[bandCount];
			_bandLowHitFrames = new int[bandCount];
		}
	}

	private void ResetBandHitHistory()
	{
		if (_bandHitRate.Length > 0) Array.Clear(_bandHitRate, 0, _bandHitRate.Length);
		if (_bandLowHitFrames.Length > 0) Array.Clear(_bandLowHitFrames, 0, _bandLowHitFrames.Length);
	}

	private bool CheckAndUpdateBandInvalidation(Transform3D current, float rangeFar, float posDeltaThreshold, float basisDeltaThreshold, float rangeDeltaThreshold)
	{
		bool invalidate = false;
		if (_hasLastCamTransform)
		{
			float posDelta = (current.Origin - _lastCamTransform.Origin).Length();
			float basisDelta = MaxBasisDelta(current.Basis, _lastCamTransform.Basis);
			if (posDelta > posDeltaThreshold || basisDelta > basisDeltaThreshold)
				invalidate = true;
		}

		if (_hasLastRangeFar && rangeDeltaThreshold > 0f)
		{
			if (Mathf.Abs(rangeFar - _lastRangeFar) > rangeDeltaThreshold)
				invalidate = true;
		}

		_lastCamTransform = current;
		_hasLastCamTransform = true;
		_lastRangeFar = rangeFar;
		_hasLastRangeFar = true;

		return invalidate;
	}

	private static float MaxBasisDelta(Basis a, Basis b)
	{
		float dx = (a.X - b.X).Length();
		float dy = (a.Y - b.Y).Length();
		float dz = (a.Z - b.Z).Length();
		return Mathf.Max(dx, Mathf.Max(dy, dz));
	}

	private void EnsureDepthHistory(int depthHistoryFrames)
	{
		if (_depthHistory.Length != depthHistoryFrames)
		{
			_depthHistory = new float[Mathf.Max(4, depthHistoryFrames)];
			for (int i = 0; i < _depthHistory.Length; i++) _depthHistory[i] = 0f;
			_depthHistWrite = 0;
		}
	}

	// robust estimate: take the 80th percentile of frame-max values
	private float RobustFarEstimate_Fallback()
	{
		var list = new System.Collections.Generic.List<float>(_depthHistory.Length);
		for (int i = 0; i < _depthHistory.Length; i++)
		{
			float d = _depthHistory[i];
			if (d > 0.0001f && float.IsFinite(d)) list.Add(d);
		}

		if (list.Count == 0) return _rangeFar;

		list.Sort();

		int idx = (int)Mathf.Floor((list.Count - 1) * 0.80f);
		idx = Mathf.Clamp(idx, 0, list.Count - 1);

		return list[idx];
	}

	// ✅ ADD inside GrinFilmCamera class (helpers)
	private static Color ShadeNormalRGB(Vector3 n)
	{
		n = n.Normalized();
		return new Color(n.X * 0.5f + 0.5f, n.Y * 0.5f + 0.5f, n.Z * 0.5f + 0.5f, 1f);
	}

	private static Color ShadeNdotV(Vector3 n, Vector3 v, out float rawDot)
	{
		n = n.Normalized();
		v = v.Normalized();
		rawDot = n.Dot(v);
		float ndv = Mathf.Clamp(rawDot, 0f, 1f);
		return new Color(ndv, ndv, ndv, 1f);
	}

	private static Color ShadeNdotVAbs(float ndv)
	{
		ndv = Mathf.Clamp(Mathf.Abs(ndv), 0f, 1f);
		return new Color(ndv, ndv, ndv, 1f);
	}

	private int ComputePass2CollisionStride(float traveledB, float far, in EffectiveConfig cfg)
	{
		if (!cfg.UsePass2CollisionStride) return 1;
		int nearS = Mathf.Clamp(cfg.Pass2CollisionStrideNear, 1, 32);
		int farS = Mathf.Clamp(cfg.Pass2CollisionStrideFar, 1, 32);
		if (farS <= nearS) return nearS;

		float t = traveledB / Mathf.Max(0.001f, far);
		float startT = Mathf.Clamp(cfg.Pass2CollisionStrideFarStartT, 0f, 1f);
		if (t <= startT) return nearS;

		float a = (t - startT) / Mathf.Max(1e-6f, (1f - startT));
		int s = Mathf.RoundToInt(Mathf.Lerp(nearS, farS, a));
		return Mathf.Clamp(s, 1, farS);
	}

	private void EnsurePass2QuickRayCache()
	{
		if (_pass2QuickRayCache.Length != Pass2QuickRayCacheSize)
			_pass2QuickRayCache = new Pass2QuickRayCacheEntry[Pass2QuickRayCacheSize];
	}

	private void ResetPass2QuickRayCache()
	{
		_pass2QuickRayCacheCount = 0;
		_pass2QuickRayCacheWrite = 0;
	}

	private static int QuantizePass2QuickRay(float v)
	{
		return Mathf.FloorToInt(v * Pass2QuickRayCacheQuantize);
	}

	private bool TryGetPass2QuickRayCache(int ax, int ay, int az, int bx, int by, int bz, int flagsKey, out bool didHit, out float hitDistAlongRay)
	{
		int count = _pass2QuickRayCacheCount;
		if (count == 0)
		{
			didHit = false;
			hitDistAlongRay = 0f;
			return false;
		}

		int scan = count < _pass2QuickRayCache.Length ? count : _pass2QuickRayCache.Length;
		for (int i = 0; i < scan; i++)
		{
			ref Pass2QuickRayCacheEntry e = ref _pass2QuickRayCache[i];
			if (e.Ax == ax && e.Ay == ay && e.Az == az && e.Bx == bx && e.By == by && e.Bz == bz && e.Flags == flagsKey)
			{
				didHit = e.DidHit;
				hitDistAlongRay = e.HitDistAlongRay;
				return true;
			}
		}

		didHit = false;
		hitDistAlongRay = 0f;
		return false;
	}

	private bool TryUpdatePass2QuickRayCacheEntry(int ax, int ay, int az, int bx, int by, int bz, int flagsKey, bool didHit, float hitDistAlongRay)
	{
		int count = _pass2QuickRayCacheCount;
		if (count == 0)
			return false;

		int scan = count < _pass2QuickRayCache.Length ? count : _pass2QuickRayCache.Length;
		for (int i = 0; i < scan; i++)
		{
			ref Pass2QuickRayCacheEntry e = ref _pass2QuickRayCache[i];
			if (e.Ax == ax && e.Ay == ay && e.Az == az && e.Bx == bx && e.By == by && e.Bz == bz && e.Flags == flagsKey)
			{
				e.DidHit = didHit;
				e.HitDistAlongRay = hitDistAlongRay;
				return true;
			}
		}

		return false;
	}

	private void AddPass2QuickRayCache(int ax, int ay, int az, int bx, int by, int bz, int flagsKey, bool didHit, float hitDistAlongRay)
	{
		int idx = _pass2QuickRayCacheWrite;
		_pass2QuickRayCache[idx] = new Pass2QuickRayCacheEntry
		{
			Ax = ax,
			Ay = ay,
			Az = az,
			Bx = bx,
			By = by,
			Bz = bz,
			Flags = flagsKey,
			DidHit = didHit,
			HitDistAlongRay = hitDistAlongRay
		};
		_pass2QuickRayCacheWrite = (idx + 1) % _pass2QuickRayCache.Length;
		if (_pass2QuickRayCacheCount < _pass2QuickRayCache.Length)
			_pass2QuickRayCacheCount++;
	}

	private int FillPixelBlock(int x, int y, int stride, Color col, int filmW, int filmH)
	{
		return FillPixelBlock(_img, x, y, stride, col, filmW, filmH, TrackFilmPixelTouch);
	}

	private static int FillPixelBlock(Image image, int x, int y, int stride, Color col, int filmW, int filmH, Action<int> onPixelWritten = null)
	{
		if (image == null)
		{
			return 0;
		}

		if (stride <= 1)
		{
			if (x >= 0 && x < filmW && y >= 0 && y < filmH)
			{
				image.SetPixel(x, y, col);
				onPixelWritten?.Invoke((y * filmW) + x);
				return 1;
			}
			return 0;
		}

		int filled = 0;
		int yMax = Math.Min(filmH, y + stride);
		int xMax = Math.Min(filmW, x + stride);
		for (int yy = y; yy < yMax; yy++)
		{
			for (int xx = x; xx < xMax; xx++)
			{
				image.SetPixel(xx, yy, col);
				onPixelWritten?.Invoke((yy * filmW) + xx);
				filled++;
			}
		}
		return filled;
	}

	private void TrackFilmPixelTouch(int pixelIndex)
	{
		if (pixelIndex < 0)
			return;
		if ((uint)pixelIndex >= (uint)_presentTouchedEpoch.Length || (uint)pixelIndex >= (uint)_refreshTouchedEpoch.Length)
			return;

		if (_presentTouchedEpoch[pixelIndex] != _presentTouchedEpochId)
		{
			_presentTouchedEpoch[pixelIndex] = _presentTouchedEpochId;
			_presentTouchedPixels++;
		}

		if (_refreshTouchedEpoch[pixelIndex] != _refreshTouchedEpochId)
		{
			_refreshTouchedEpoch[pixelIndex] = _refreshTouchedEpochId;
			_refreshTouchedPixels++;
		}
	}

	private void ResetPresentedFrameTracking()
	{
		_presentTouchedPixels = 0;
		if (_presentTouchedEpochId == uint.MaxValue)
		{
			Array.Clear(_presentTouchedEpoch, 0, _presentTouchedEpoch.Length);
			_presentTouchedEpochId = 1;
		}
		else
		{
			_presentTouchedEpochId++;
		}
	}

	private void ResetFullRefreshCycleTracking(long nowTicks, bool clearLastMeasurement)
	{
		_refreshTouchedPixels = 0;
		_presentsSinceRefreshReset = 0;
		_refreshCycleStartTimestamp = nowTicks;
		_hasRefreshCycleStartTimestamp = true;
		if (_refreshTouchedEpochId == uint.MaxValue)
		{
			Array.Clear(_refreshTouchedEpoch, 0, _refreshTouchedEpoch.Length);
			_refreshTouchedEpochId = 1;
		}
		else
		{
			_refreshTouchedEpochId++;
		}

		if (clearLastMeasurement)
		{
			_lastFramesToFullRefresh = 0;
			_lastTimeToFullRefreshMs = 0.0;
			_lastEffectiveFullRefreshFps = 0.0;
			_lastFullRefreshMeasured = false;
		}
	}

	private void ResetRefreshAuditTracking()
	{
		long nowTicks = Stopwatch.GetTimestamp();
		_lastPresentedPixelsUpdated = 0;
		_lastPresentedCoverageRatio = 0.0;
		ResetPresentedFrameTracking();
		ResetFullRefreshCycleTracking(nowTicks, clearLastMeasurement: true);
		_presentRolling.Reset();
	}

	private void FinalizePresentedFrameRefreshMetrics()
	{
		int totalPixels = _filmWidth > 0 && _filmHeight > 0 ? _filmWidth * _filmHeight : 0;
		long nowTicks = Stopwatch.GetTimestamp();
		_lastPresentedPixelsUpdated = _presentTouchedPixels;
		_lastPresentedCoverageRatio = totalPixels > 0
			? Math.Clamp(_presentTouchedPixels / (double)totalPixels, 0.0, 1.0)
			: 0.0;

		if (!_hasRefreshCycleStartTimestamp)
		{
			_refreshCycleStartTimestamp = nowTicks;
			_hasRefreshCycleStartTimestamp = true;
		}

		_presentsSinceRefreshReset++;

		if (totalPixels > 0 && _refreshTouchedPixels >= totalPixels)
		{
			double fullRefreshMs = (nowTicks - _refreshCycleStartTimestamp) * 1000.0 / Stopwatch.Frequency;
			_lastFramesToFullRefresh = _presentsSinceRefreshReset;
			_lastTimeToFullRefreshMs = fullRefreshMs;
			_lastEffectiveFullRefreshFps = fullRefreshMs > 0.0 ? 1000.0 / fullRefreshMs : 0.0;
			_lastFullRefreshMeasured = double.IsFinite(fullRefreshMs) && fullRefreshMs > 0.0;
			ResetFullRefreshCycleTracking(nowTicks, clearLastMeasurement: false);
		}

		_presentRolling.AddSample(
			nowTicks,
			0.0,
			Math.Max(0, _lastPresentedPixelsUpdated),
			_lastPresentedCoverageRatio >= 0.999999 ? 1 : 0,
			false,
			0L);

		ResetPresentedFrameTracking();
	}

	private void EmitThreadReadinessAuditOnce(in EffectiveConfig cfg, int rowsPerFrame, int bandHeight, int filmW, int filmH, int jobs)
	{
		if (_threadReadinessAuditLogged)
			return;

		_threadReadinessAuditLogged = true;
		string workShape = $"band_rows y=[{_rowCursor},{Math.Min(filmH, _rowCursor + Math.Max(1, bandHeight))}) width={filmW} stride={cfg.Film.PixelStride}";
		string deterministicAnchor = cfg.Research.DeterministicMode
			? $"on seedBase={cfg.Research.FixedSeed}"
			: "off hook=Research.DeterministicMode+FixedSeed";
		GD.Print($"[ThreadAudit] unitOfWork={workShape} rowsPerStep={rowsPerFrame} jobs={jobs} pendingPass2Reuse={( _pendingBandHasPass1 ? 1 : 0)} subtileReorder={(ExperimentalSubtileSchedulerModeEnabled ? 1 : 0)}");
		GD.Print("[ThreadAudit] sharedWrites=film_image,texture_upload,row_cursor,pending_band,frame_counters,renderhealth_window,overlay_buffers,tile_metric_priors,band_hit_history,fixture_capture_images");
		GD.Print("[ThreadAudit] localAccumulationReadiness=pass1_segment_build:easy pass2_counters:easy film_writes:medium overlay/debug:medium adaptive/band-history/priors:caution");
		GD.Print("[ThreadAudit][Pass2] localizable=per_pixel_hit_search,broadphase_candidate_lists,segment_shading_scratch,row_or_region_counters,region_hit_masks,region_color_buffers");
		GD.Print("[ThreadAudit][Pass2] serialized=film_image_and_fixture_images,pass2_hit_history,quickray_cache,softgate_frame_state,geom_renderhealth_counters,debug_overlay_buffers,row_cursor_budget_stop,texture_upload");
		GD.Print("[ThreadAudit][Pass2] highRisk=adaptive_rows,tile_reorder_execution,persistent_priors,capture_snapshot_bookkeeping,global_rng_shared_softgate_paths");
		GD.Print("[ThreadAudit][Pass2] firstSlice=disjoint_row_region_pass2_with_local_color_and_hit_buffers_then_serial_commit_to_film_histories_overlay");
		GD.Print($"[ThreadAudit] deterministic={deterministicAnchor}");
	}

	private void EmitDeterministicBenchmarkModeOnce(in EffectiveConfig cfg)
	{
		if (_deterministicBenchmarkModeLogged || !_deterministicBenchmarkModeRequested)
			return;

		_deterministicBenchmarkModeLogged = true;
		GD.Print(
			$"[BenchmarkMode] deterministic_lock=1 researchEnabled={(cfg.Research.ResearchEnabled ? 1 : 0)} " +
			$"deterministic={(cfg.Research.DeterministicMode ? 1 : 0)} fixedSeed={cfg.Research.FixedSeed} " +
			$"threadedBands={(cfg.UseThreadedBands ? 1 : 0)} bandWorkers={cfg.ThreadedBandWorkerCount} bandChunkRows={cfg.ThreadedBandRowsPerChunk} " +
			$"tileReorder={(EnableTileMetricsReorderExecution ? 1 : 0)} priors={(EnableTileMetricsPersistentPriors ? 1 : 0)}");
		GD.Print("[BenchmarkMode] cli=--benchmark-lock=1 --benchmark-seed=<n> contract=stable_chunk_order+stable_merge_order+single_worker_pass1_anchor");
	}

	private void UpdateFilmViewTexture()
	{
		if (_filmView != null && GodotObject.IsInstanceValid(_filmView))
			_filmView.Texture = _tex;
		else
			_filmView = null;

		if (_overlayRect != null && GodotObject.IsInstanceValid(_overlayRect))
			_overlayRect.Texture = _tex;
		else
			_overlayRect = null;
	}

	private bool EnsureFilmImageSize(in EffectiveConfig cfg)
	{
		float scale = cfg.Film.ResolutionScale;
		int targetW = Mathf.Max(8, Mathf.RoundToInt(cfg.Film.BaseWidth * scale));
		int targetH = Mathf.Max(8, Mathf.RoundToInt(cfg.Film.BaseHeight * scale));
		int targetPixels = targetW * targetH;
		if (_img != null && _filmWidth == targetW && _filmHeight == targetH)
		{
			EnsureTelemetryHeatmapCapacity(targetPixels);
			if (_pass2PrevHadHit.Length != targetPixels)
				_pass2PrevHadHit = new byte[targetPixels];
				if (_pass2HadHitLostThisFrame.Length != targetPixels)
					_pass2HadHitLostThisFrame = new byte[targetPixels];
				if (_presentTouchedEpoch.Length != targetPixels)
					_presentTouchedEpoch = new uint[targetPixels];
				if (_refreshTouchedEpoch.Length != targetPixels)
					_refreshTouchedEpoch = new uint[targetPixels];
				if (_fixtureFinalHitOnlyImg == null)
				{
					_fixtureFinalHitOnlyImg = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
					_fixtureFinalHitOnlyImg.Fill(Colors.Black);
				}
				if (_fixtureCategoricalFinalImg == null)
				{
					_fixtureCategoricalFinalImg = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
					_fixtureCategoricalFinalImg.Fill(Colors.Black);
				}
				return false;
			}

		_filmWidth = targetW;
		_filmHeight = targetH;
		ResetRenderHealthOverlayWindowState();
		_img = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
		_img.Fill(cfg.SkyColor);
		EnsureTelemetryHeatmapCapacity(_filmWidth * _filmHeight);
		ClearTelemetryHeatmapArrays();
		_fixtureFinalHitOnlyImg = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
		_fixtureFinalHitOnlyImg.Fill(Colors.Black);
		_fixtureCategoricalFinalImg = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
		_fixtureCategoricalFinalImg.Fill(Colors.Black);
		_tex = ImageTexture.CreateFromImage(_img);
		_pass2PrevHadHit = new byte[_filmWidth * _filmHeight];
		_pass2HadHitLostThisFrame = new byte[_filmWidth * _filmHeight];
		_presentTouchedEpoch = new uint[_filmWidth * _filmHeight];
		_refreshTouchedEpoch = new uint[_filmWidth * _filmHeight];
		ResetRefreshAuditTracking();

		UpdateFilmViewTexture();

		return true;
	}

	private int ComputeFilmSettingsHash(in EffectiveConfig cfg)
	{
		unchecked
		{
			int hash = 17;
			hash = hash * 31 + cfg.Film.BaseWidth;
			hash = hash * 31 + cfg.Film.BaseHeight;
			hash = hash * 31 + cfg.Film.PixelStride;
			hash = hash * 31 + cfg.Film.ResolutionScale.GetHashCode();
			return hash;
		}
	}

	private void ResetRowCursor(string reason)
	{
		_softGateDisabledForPass = false;
		_pendingBandRowStart = -1;
		_pendingBandRowCount = 0;
		_pendingBandHasPass1 = false;
		_pendingRowCursorReset = false;
		_pendingRowCursorResetReason = "";
		_stuckBandStartRow = -1;
		_stuckBandEndRow = -1;
		_stuckBandRepeats = 0;
		_lastBandCommitted = true;
		_lastRenderStepRowCursor = -1;
		_lastRenderStepBandStart = -1;
		_lastRenderStepBandEnd = -1;
		_bandIncompleteFrameId = -1;
		_bandIncompleteRowStart = -1;
		_bandIncompleteRowEnd = -1;
		_suppressStuckBandRepeatOnce = false;
		if (_rowCursor == 0) return;
		int prev = _rowCursor;
		_rowCursor = 0;
		ResetRefreshAuditTracking();
		GD.Print($"[FrameReset] reason={reason} prevRow={prev} frame={_frameIndex}");
	}

	private void QuitTreeDeferred()
	{
		GetTree()?.Quit();
	}

	private void EnsureFilmDebugCapacity(int rays, int pts)
	{
		if (_dbgOff.Length < rays) Array.Resize(ref _dbgOff, rays);
		if (_dbgCnt.Length < rays) Array.Resize(ref _dbgCnt, rays);
		if (_dbgHits.Length < rays) Array.Resize(ref _dbgHits, rays);
		if (_dbgPts.Length < pts) Array.Resize(ref _dbgPts, pts);
	}

	private void ValidateDebugOverlayData(int debugMaxFilmRays)
	{
		if (_dbgRayCount > debugMaxFilmRays)
			GD.Print($"Debug overlay rayCount exceeded cap: {_dbgRayCount} > {debugMaxFilmRays}");

		if (_dbgRayCount > _dbgOff.Length || _dbgRayCount > _dbgCnt.Length || _dbgRayCount > _dbgHits.Length)
			GD.Print("Debug overlay ray arrays are smaller than rayCount.");

		if (_dbgPtWrite > _dbgPts.Length)
			GD.Print($"Debug overlay point write exceeded capacity: {_dbgPtWrite} > {_dbgPts.Length}");

		if (_dbgRayCount > 0 && _dbgPtWrite == 0)
			GD.Print("Debug overlay has rays but zero points.");

		int maxPt = _dbgPtWrite;
		for (int i = 0; i < _dbgRayCount; i++)
		{
			int start = _dbgOff[i];
			int count = _dbgCnt[i];
			if (start < 0 || count < 0 || start + count > maxPt)
			{
				GD.Print($"Debug overlay bounds error at ray {i}: start={start} count={count} maxPt={maxPt}");
				break;
			}
		}
	}

	private void MaybePrintToggleSnapshot(in EffectiveConfig cfg, in EffectiveRayMarchSettings rayCfg)
	{
		if (!rayCfg.HasRenderer) return;

		ToggleSnapshot cur = new ToggleSnapshot
		{
			UseAdaptiveSubsteps = cfg.UseAdaptiveSubsteps,
			UseSingleProbeThenSubdivide = cfg.UseSingleProbeThenSubdivide,
			UseBandHitSkip = cfg.UseBandHitSkip,
			RequireHitToRender = rayCfg.RequireHitToRender,
			StopOnHit = rayCfg.StopOnHit,
			TerminateTrailOnHit = rayCfg.TerminateTrailOnHit,
			UpdateEveryFrame = cfg.UpdateEveryFrame
		};

		if (_hasToggleSnapshot && ToggleSnapshotEquals(in cur, in _lastToggleSnapshot)) return;

		_lastToggleSnapshot = cur;
		_hasToggleSnapshot = true;

		GD.Print(
			"Toggles: AdaptiveSubsteps=" + (cur.UseAdaptiveSubsteps ? "1" : "0") +
			" SingleProbeSubdivide=" + (cur.UseSingleProbeThenSubdivide ? "1" : "0") +
			" BandHitSkip=" + (cur.UseBandHitSkip ? "1" : "0") +
			" RequireHit=" + (cur.RequireHitToRender ? "1" : "0") +
			" StopOnHit=" + (cur.StopOnHit ? "1" : "0") +
			" TerminateTrailOnHit=" + (cur.TerminateTrailOnHit ? "1" : "0") +
			" UpdateEveryFrame=" + (cur.UpdateEveryFrame ? "1" : "0"));
	}

	private static bool ToggleSnapshotEquals(in ToggleSnapshot a, in ToggleSnapshot b)
	{
		return a.UseAdaptiveSubsteps == b.UseAdaptiveSubsteps
			&& a.UseSingleProbeThenSubdivide == b.UseSingleProbeThenSubdivide
			&& a.UseBandHitSkip == b.UseBandHitSkip
			&& a.RequireHitToRender == b.RequireHitToRender
			&& a.StopOnHit == b.StopOnHit
			&& a.TerminateTrailOnHit == b.TerminateTrailOnHit
			&& a.UpdateEveryFrame == b.UpdateEveryFrame;
	}

	private static void ResetSoftGateCounters(
		ref SoftGateDebugCounters c,
		int frameIndex,
		long effectivePixels,
		bool enabled,
		bool v2Enabled,
		float minSegLen,
		float scoreThreshold,
		float turnAngleWeight,
		float prevHitLostBonus,
		float randomProbeChance,
		int maxAttemptsPerFrameV2,
		int maxAttemptsPerPixel,
		int maxAttemptsPerFrame,
		int maxSubdividedCallsPerFrame)
	{
		c = new SoftGateDebugCounters
		{
			FrameIndex = frameIndex,
			EffectivePixels = effectivePixels,
			SoftGateEnabled = enabled && v2Enabled,
			SoftGateMinSegLen = minSegLen,
			SoftGateScoreThreshold = scoreThreshold,
			SoftGateTurnAngleWeight = turnAngleWeight,
			SoftGatePrevHitLostBonus = prevHitLostBonus,
			SoftGateRandomProbeChance = randomProbeChance,
			SoftGateMaxAttemptsPerFrameV2 = maxAttemptsPerFrameV2,
			Pass2SoftGateMaxAttemptsPerPixel = maxAttemptsPerPixel,
			Pass2SoftGateMaxAttemptsPerFrame = maxAttemptsPerFrame,
			Pass2SoftGateMaxSubdividedCallsPerFrame = maxSubdividedCallsPerFrame,
			SoftGateMetricMin = double.PositiveInfinity,
			SoftGateMetricMax = double.NegativeInfinity
		};
	}

	private static void GetTopSoftGateSkipReasons(
		in SoftGateDebugCounters c,
		out string firstReason,
		out long firstCount,
		out string secondReason,
		out long secondCount)
	{
		// Use locals (NOT out params) inside Consider()
		string fr = "none";
		string sr = "none";
		long fc = 0;
		long sc = 0;

		void Consider(string name, long count)
		{
			if (count <= 0) return;

			if (count > fc)
			{
				sc = fc;
				sr = fr;
				fc = count;
				fr = name;
				return;
			}

			if (count > sc)
			{
				sc = count;
				sr = name;
			}
		}

		Consider("segLenTooShort", c.SkipSegLenTooShort);
		Consider("scoreTooLow", c.SkipScoreTooLow);
		Consider("randomNotSelected", c.SkipRandomNotSelected);
		Consider("budgetAttemptCap", c.SkipBudgetAttemptCap);
		Consider("budgetSubdivideCap", c.SkipBudgetSubdivideCap);
		Consider("guard", c.SkipGuard);
		Consider("other", c.SkipOther);

		// Assign outs once at the end
		firstReason = fr;
		firstCount  = fc;
		secondReason = sr;
		secondCount  = sc;
	}

	private static string BuildSoftGateFrameSummary(in SoftGateDebugCounters c, string extraContext)
	{
		double metricAvg = c.SoftGateMetricCount > 0 ? c.SoftGateMetricSum / c.SoftGateMetricCount : 0.0;
		double metricMin = c.SoftGateMetricCount > 0 ? c.SoftGateMetricMin : 0.0;
		double metricMax = c.SoftGateMetricCount > 0 ? c.SoftGateMetricMax : 0.0;

		StringBuilder sb = new StringBuilder(256);
		sb.Append("[SoftGate] frame=").Append(c.FrameIndex)
			.Append(" enabled=").Append(c.SoftGateEnabled ? "1" : "0")
			.Append(" minSeg=").Append(c.SoftGateMinSegLen.ToString("0.###"))
			.Append(" scoreThr=").Append(c.SoftGateScoreThreshold.ToString("0.###"))
			.Append(" turnW=").Append(c.SoftGateTurnAngleWeight.ToString("0.###"))
			.Append(" prevLost=").Append(c.SoftGatePrevHitLostBonus.ToString("0.###"))
			.Append(" rand=").Append(c.SoftGateRandomProbeChance.ToString("0.###"))
			.Append(" scoreBudget=").Append(c.SoftGateMaxAttemptsPerFrameV2)
			.Append(" considered=").Append(c.SoftGateConsidered)
			.Append(" forced=").Append(c.SoftGateForced)
			.Append(" attempts=").Append(c.SoftGateAttempts)
			.Append(" hits=").Append(c.SoftGateHits)
			.Append(" hitChange=").Append(c.SoftGateHitChangedResult)
			.Append(" newPx=").Append(c.SoftGateNewPixelFilled)
			.Append(" budget{px=").Append(c.Pass2SoftGateMaxAttemptsPerPixel)
			.Append(" frame=").Append(c.Pass2SoftGateMaxAttemptsPerFrame)
			.Append(" sub=").Append(c.Pass2SoftGateMaxSubdividedCallsPerFrame)
			.Append(" used=").Append(c.SoftGateAttemptsUsed)
			.Append(" subUsed=").Append(c.SoftGateSubdividedCallsUsed)
			.Append(" exceeded=").Append(c.SoftGateBudgetExceeded)
			.Append("}")
			.Append(" skipped=").Append(c.SoftGateSkipped)
			.Append(" {seglen=").Append(c.SkipSegLenTooShort)
			.Append(" scoreLow=").Append(c.SkipScoreTooLow)
			.Append(" randMiss=").Append(c.SkipRandomNotSelected)
			.Append(" budAttempt=").Append(c.SkipBudgetAttemptCap)
			.Append(" budSub=").Append(c.SkipBudgetSubdivideCap)
			.Append(" guard=").Append(c.SkipGuard)
			.Append(" other=").Append(c.SkipOther)
			.Append("} ")
			.Append("metric[min=").Append(metricMin.ToString("0.###"))
			.Append(" max=").Append(metricMax.ToString("0.###"))
			.Append(" avg=").Append(metricAvg.ToString("0.###"))
			.Append("] ")
			.Append("qray[call=").Append(c.QRayCalls)
			.Append(" hit=").Append(c.QRayHit)
			.Append(" miss=").Append(c.QRayMiss)
			.Append("]");

		if (c.SoftGateMetricCount > 0 && c.SoftGateScoreThreshold > metricMax)
		{
			sb.Append(" ScoreThr=").Append(c.SoftGateScoreThreshold.ToString("0.###"))
				.Append(" > maxObserved=").Append(metricMax.ToString("0.###"))
				.Append(" -> will rarely/never attempt.");
		}

		if (!string.IsNullOrEmpty(extraContext))
		{
			sb.Append(" ").Append(extraContext);
		}

		return sb.ToString();
	}

	private static string BuildSoftGateDebugSummary(in SoftGateDebugCounters c)
	{
		StringBuilder sb = new StringBuilder(180);
		sb.Append("[SoftGate][Dbg] attempts=").Append(c.SoftGateAttempts)
			.Append(" hits=").Append(c.SoftGateHits)
			.Append(" skipped{seglen=").Append(c.SkipSegLenTooShort)
			.Append(" scoreLow=").Append(c.SkipScoreTooLow)
			.Append(" randMiss=").Append(c.SkipRandomNotSelected)
			.Append(" budAttempt=").Append(c.SkipBudgetAttemptCap)
			.Append(" budSub=").Append(c.SkipBudgetSubdivideCap)
			.Append(" guard=").Append(c.SkipGuard)
			.Append(" other=").Append(c.SkipOther)
			.Append("}");
		return sb.ToString();
	}

	private static string BuildSoftGateBandSummary(int yStart, int yEnd, in SoftGateDebugCounters c)
	{
		StringBuilder sb = new StringBuilder(160);
		sb.Append("[Band] y=[").Append(yStart).Append(",").Append(yEnd).Append(")")
			.Append(" hits=").Append(c.Pass2Hits)
			.Append(" segs=").Append(c.SegsTotal)
			.Append(" tested=").Append(c.SegsTested)
			.Append(" qRayHit=").Append(c.QRayHit)
			.Append(" qRayMiss=").Append(c.QRayMiss)
			.Append(" SG{considered=").Append(c.SoftGateConsidered)
			.Append(" skipped=").Append(c.SoftGateSkipped)
			.Append(" forced=").Append(c.SoftGateForced)
			.Append(" attempts=").Append(c.SoftGateAttempts)
			.Append(" hits=").Append(c.SoftGateHits)
			.Append("}");
		return sb.ToString();
	}

	private void MaybePrintSoftGateConfigSnapshot(in EffectiveConfig cfg)
	{
		EffectiveSoftGateSettings softGateCfg = cfg.SoftGate;
		SoftGateConfigSnapshot cur = new SoftGateConfigSnapshot
		{
			Pass2SoftGateEnableQuickRayMiss = softGateCfg.EnableQuickRayMiss,
			Pass2SoftGateScoringEnabled = softGateCfg.ScoringEnabled,
			Pass2SoftGateMinSegmentLength = softGateCfg.MinSegmentLength,
			Pass2SoftGateScoreThreshold = softGateCfg.ScoreThreshold,
			Pass2SoftGateScoreTurnAngleWeight = softGateCfg.ScoreTurnAngleWeight,
			Pass2SoftGateScorePrevHitLostBonus = softGateCfg.ScorePrevHitLostBonus,
			Pass2SoftGateRandomProbeChance = softGateCfg.RandomProbeChance,
			Pass2SoftGateScoreBudgetPerFrame = softGateCfg.ScoreBudgetPerFrame,
			Pass2SoftGateMaxAttemptsPerPixel = softGateCfg.MaxAttemptsPerPixel,
			Pass2SoftGateMaxAttemptsPerFrame = softGateCfg.MaxAttemptsPerFrame,
			Pass2SoftGateMaxSubdividedCallsPerFrame = softGateCfg.MaxSubdividedCallsPerFrame,
			UpdateEveryFrame = cfg.UpdateEveryFrame
		};

		if (_hasSoftGateCfgSnapshot && SoftGateConfigSnapshotEquals(in cur, in _lastSoftGateCfgSnapshot)) return;

		_lastSoftGateCfgSnapshot = cur;
		_hasSoftGateCfgSnapshot = true;

		GD.Print(
			"[Cfg] Pass2SoftGateEnableQuickRayMiss=" + (cur.Pass2SoftGateEnableQuickRayMiss ? "1" : "0") +
			" Pass2SoftGateScoringEnabled=" + (cur.Pass2SoftGateScoringEnabled ? "1" : "0") +
			" Pass2SoftGateMinSegmentLength=" + cur.Pass2SoftGateMinSegmentLength.ToString("0.###") +
			" Pass2SoftGateScoreThreshold=" + cur.Pass2SoftGateScoreThreshold.ToString("0.###") +
			" Pass2SoftGateScoreTurnAngleWeight=" + cur.Pass2SoftGateScoreTurnAngleWeight.ToString("0.###") +
			" Pass2SoftGateScorePrevHitLostBonus=" + cur.Pass2SoftGateScorePrevHitLostBonus.ToString("0.###") +
			" Pass2SoftGateRandomProbeChance=" + cur.Pass2SoftGateRandomProbeChance.ToString("0.###") +
			" Pass2SoftGateScoreBudgetPerFrame=" + cur.Pass2SoftGateScoreBudgetPerFrame +
			" Pass2SoftGateMaxAttemptsPerPixel=" + cur.Pass2SoftGateMaxAttemptsPerPixel +
			" Pass2SoftGateMaxAttemptsPerFrame=" + cur.Pass2SoftGateMaxAttemptsPerFrame +
			" Pass2SoftGateMaxSubdividedCallsPerFrame=" + cur.Pass2SoftGateMaxSubdividedCallsPerFrame +
			" UpdateEveryFrame=" + (cur.UpdateEveryFrame ? "1" : "0"));
	}

	private static bool SoftGateConfigSnapshotEquals(in SoftGateConfigSnapshot a, in SoftGateConfigSnapshot b)
	{
		return a.Pass2SoftGateEnableQuickRayMiss == b.Pass2SoftGateEnableQuickRayMiss
			&& a.Pass2SoftGateScoringEnabled == b.Pass2SoftGateScoringEnabled
			&& Math.Abs(a.Pass2SoftGateMinSegmentLength - b.Pass2SoftGateMinSegmentLength) < 1e-6f
			&& Math.Abs(a.Pass2SoftGateScoreThreshold - b.Pass2SoftGateScoreThreshold) < 1e-6f
			&& Math.Abs(a.Pass2SoftGateScoreTurnAngleWeight - b.Pass2SoftGateScoreTurnAngleWeight) < 1e-6f
			&& Math.Abs(a.Pass2SoftGateScorePrevHitLostBonus - b.Pass2SoftGateScorePrevHitLostBonus) < 1e-6f
			&& Math.Abs(a.Pass2SoftGateRandomProbeChance - b.Pass2SoftGateRandomProbeChance) < 1e-6f
			&& a.Pass2SoftGateScoreBudgetPerFrame == b.Pass2SoftGateScoreBudgetPerFrame
			&& a.Pass2SoftGateMaxAttemptsPerPixel == b.Pass2SoftGateMaxAttemptsPerPixel
			&& a.Pass2SoftGateMaxAttemptsPerFrame == b.Pass2SoftGateMaxAttemptsPerFrame
			&& a.Pass2SoftGateMaxSubdividedCallsPerFrame == b.Pass2SoftGateMaxSubdividedCallsPerFrame
			&& a.UpdateEveryFrame == b.UpdateEveryFrame;
	}


	public void ApplyPerfPresetFastPreview()
	{
		PerformancePreset = PerformancePresetMode.FastPreview;
		MarkPresetDirty(scene: false, perf: true, quality: false, reason: "PerfPresetFastPreview");
	}

	public void ResetFilmPassManual()
	{
		ResetRowCursor("manual");
	}

	public void ApplyPreset(PresetMode mode)
	{
		Preset = mode;
		MarkPresetDirty(scene: true, perf: false, quality: false, reason: "ApplyPreset");
	}

	public void ApplyPerfPresetQuality()
	{
		PerformancePreset = PerformancePresetMode.Quality;
		MarkPresetDirty(scene: false, perf: true, quality: false, reason: "PerfPresetQuality");
	}

	private void SyncAndApplyIfDirty(string reason, bool force = false)
	{
		// PRECEDENCE: quality -> perf -> user overrides -> sanitize.
		// RATIONALE: quality and perf presets establish baselines; user overrides (scene preset + manual toggles)
		// can then intentionally supersede them before final clamping.
		if (_isApplyingPresets) return;

		bool forceApply = force || ForceReapplyPresetsNextFrame;
		bool presetChanged = _lastPreset != Preset;
		bool qualityChanged = _lastQualityMode != QualityMode;
		bool perfChanged = _lastPerformancePreset != PerformancePreset;
		if (presetChanged) _presetSceneDirty = true;
		if (qualityChanged) _presetQualityDirty = true;
		if (perfChanged) _presetPerfDirty = true;
		if (!forceApply && !_presetSceneDirty && !_presetQualityDirty && !_presetPerfDirty) return;

		_isApplyingPresets = true;
		bool sceneApplied = false;
		bool perfApplied = false;
		bool qualityApplied = false;
		bool userApplied = false;

		try
		{
			if (forceApply || _presetQualityDirty)
			{
				ApplyQualityModePresetCore(QualityMode);
				qualityApplied = true;
			}

			if (forceApply || _presetPerfDirty)
			{
				ApplyPerfPresetCore(PerformancePreset);
				perfApplied = true;
			}

			if (forceApply || _presetSceneDirty)
			{
				ApplyScenePresetCore(Preset);
				sceneApplied = true;
			}

			ApplyUserOverridesCore();
			userApplied = true;

			SanitizeAndClampSettings();

			_lastPreset = Preset;
			_lastQualityMode = QualityMode;
			_lastPerformancePreset = PerformancePreset;
			_presetSceneDirty = false;
			_presetPerfDirty = false;
			_presetQualityDirty = false;
			string applyReason = !string.IsNullOrEmpty(_presetDirtyReason) ? _presetDirtyReason : reason;
			_presetDirtyReason = "";

			GD.Print(
				$"[PresetApply] reason={applyReason} quality={(qualityApplied ? 1 : 0)} perf={(perfApplied ? 1 : 0)} user={(userApplied ? 1 : 0)} scene={(sceneApplied ? 1 : 0)} " +
				$"preset={Preset} perfPreset={PerformancePreset} quality={QualityMode} " +
				$"resScale={FilmResolutionScale:0.###} stride={PixelStride} rows={RowsPerFrame} " +
				$"broadphaseMode={BroadphaseControlMode} broadphasePolicy={BroadphasePolicy}");
		}
		finally
		{
			_isApplyingPresets = false;
			if (ForceReapplyPresetsNextFrame)
			{
				ForceReapplyPresetsNextFrame = false;
			}
		}
	}

	private void ApplyUserOverridesCore()
	{
		// Intentionally left as a hook for future manual overrides without altering preset math.
	}

	private void ApplyScenePresetCore(PresetMode mode)
	{
		switch (mode)
		{
			case PresetMode.Walk:
				DebugEveryNPixels = 16;
				DebugMaxFilmRays = 512;
				BroadphaseControlMode = BroadphaseMode.Policy;
				BroadphasePolicy = BroadphasePolicyMode.QuickRayOnly;
				UseBroadphaseQuickRay = true;
				UseBroadphaseOverlap = false;
				break;
			case PresetMode.Cinematic:
				DebugEveryNPixels = 4;
				DebugMaxFilmRays = 4096;
				BroadphaseControlMode = BroadphaseMode.Policy;
				BroadphasePolicy = BroadphasePolicyMode.Both;
				UseBroadphaseQuickRay = true;
				UseBroadphaseOverlap = true;
				break;
			default:
			case PresetMode.Preview:
				DebugEveryNPixels = 8;
				DebugMaxFilmRays = 2048;
				BroadphaseControlMode = BroadphaseMode.Policy;
				BroadphasePolicy = BroadphasePolicyMode.QuickRayOnly;
				UseBroadphaseQuickRay = true;
				UseBroadphaseOverlap = false;
				break;
		}
	}

	private void ApplyPerfPresetCore(PerformancePresetMode mode)
	{
		switch (mode)
		{
			case PerformancePresetMode.FastPreview:
				UseFieldSourceCache = true;
				BroadphaseControlMode = BroadphaseMode.Policy;
				BroadphasePolicy = BroadphasePolicyMode.QuickRayOnly;
				TinySegmentSkipLen = 0.005f;
				EarlyOutDistanceEps = 0.01f;
				NeedColliderNames = false;
				break;
			case PerformancePresetMode.Quality:
				UseFieldSourceCache = false;
				BroadphaseControlMode = BroadphaseMode.Manual;
				TinySegmentSkipLen = 0.0f;
				EarlyOutDistanceEps = 0.0f;
				NeedColliderNames = false;
				break;
			case PerformancePresetMode.None:
			default:
				break;
		}
	}

	private void SanitizeAndClampSettings()
	{
		PixelStride = Mathf.Clamp(PixelStride, 1, 8);
		RowsPerFrame = Math.Max(1, RowsPerFrame);
		TargetMsPerFrame = Math.Max(1, TargetMsPerFrame);
		UpdateEveryFrameBudgetMs = Mathf.Max(1f, UpdateEveryFrameBudgetMs);
		RenderStepMaxMs = Math.Max(1, RenderStepMaxMs);
	}

	private void MarkPresetDirty(bool scene, bool perf, bool quality, string reason)
	{
		if (scene) _presetSceneDirty = true;
		if (perf) _presetPerfDirty = true;
		if (quality) _presetQualityDirty = true;
		if (!string.IsNullOrEmpty(reason))
		{
			_presetDirtyReason = reason;
		}
	}

	private void ResolveEffectiveConfig(out EffectiveConfig cfg)
	{
		ResolveRayBeamRendererReference();
		// EFFECTIVE CONFIG CONTRACT:
		// - Snapshots RayBeamRenderer shared values first.
		// - Resolves broadphase mode (Manual/Policy/Auto/Off).
		// - Emits only effective booleans for RenderStep to consume.
		var broadphaseResolved = UpdateBroadphaseEffectiveState();

		cfg = new EffectiveConfig
		{
			Broadphase = new EffectiveBroadphaseSettings
			{
				UseQuickRay = broadphaseResolved.effQuickRay,
				UseOverlap = broadphaseResolved.effOverlap,
				Mode = broadphaseResolved.effMode,
				ModeName = broadphaseResolved.effMode.ToString(),
				Reason = broadphaseResolved.sourceTag,
				Policy = broadphaseResolved.effPolicy,
				Margin = BroadphaseMargin,
				MaxResults = BroadphaseMaxResults
			},
			Film = new EffectiveFilmSettings
			{
				BaseWidth = Width,
				BaseHeight = Height,
				ResolutionScale = Mathf.Clamp(FilmResolutionScale, 0.01f, 1.0f),
				PixelStride = Mathf.Clamp(PixelStride, 1, 8),
				RowsPerFrame = Math.Max(1, RowsPerFrame),
				MaxDistance = MaxDistance,
				Opacity = FilmOpacity
			},
			UpdateEveryFrame = UpdateEveryFrame,
			UpdateEveryFrameBudgetMs = UpdateEveryFrameBudgetMs,
			UpdateEveryFrameMaxRowsPerStep = UpdateEveryFrameMaxRowsPerStep,
			RenderStepMaxMs = RenderStepMaxMs,
			RenderStepMaxPixelsPerFrame = RenderStepMaxPixelsPerFrame,
			RenderStepMaxSegmentsPerFrame = RenderStepMaxSegmentsPerFrame,
			RenderStepNoRowProgressRepeatLimit = RenderStepNoRowProgressRepeatLimit,
			TargetMsPerFrame = TargetMsPerFrame,
			MinRowsPerFrame = MinRowsPerFrame,
			MaxRowsPerFrameCap = MaxRowsPerFrameCap,
			AutoRangeDepth = AutoRangeDepth,
			AutoRangeMin = AutoRangeMin,
			AutoRangeMax = AutoRangeMax,
			AutoRangeSmoothing = AutoRangeSmoothing,
			AutoRangeSafety = AutoRangeSafety,
			DepthHistoryFrames = DepthHistoryFrames,
			UseFieldGrid = UseFieldGrid,
			FieldGridCellSize = FieldGridCellSize,
			FieldGridRebuildEveryNFrames = FieldGridRebuildEveryNFrames,
			FieldGridBoundsPadding = FieldGridBoundsPadding,
			UseCameraPropsBetaGamma = UseCameraPropsBetaGamma,
			TinySegmentSkipLen = TinySegmentSkipLen,
			EarlyOutDistanceEps = EarlyOutDistanceEps,
			UseAdaptiveSubsteps = UseAdaptiveSubsteps,
			UseBandHitSkip = UseBandHitSkip,
			BandSkipHitThreshold = BandSkipHitThreshold,
			BandSkipFrames = BandSkipFrames,
			BandSkipInvalidatePosDelta = BandSkipInvalidatePosDelta,
			BandSkipInvalidateBasisDelta = BandSkipInvalidateBasisDelta,
			BandSkipInvalidateRangeDelta = BandSkipInvalidateRangeDelta,
			Pass1DoHitTest = Pass1DoHitTest,
			Pass1ProbeEveryNSegments = Pass1ProbeEveryNSegments,
			Pass1ProbeMinTravelDelta = Pass1ProbeMinTravelDelta,
			UsePass2CollisionStride = UsePass2CollisionStride,
			Pass2CollisionStrideNear = Pass2CollisionStrideNear,
			Pass2CollisionStrideFar = Pass2CollisionStrideFar,
			Pass2CollisionStrideFarStartT = Pass2CollisionStrideFarStartT,
			MinSegLenForStrideSkip = MinSegLenForStrideSkip,
			Pass2GeomEnvelopeRadiusScale = Mathf.Max(0.1f, Pass2GeomEnvelopeRadiusScale),
			Pass2GeomEnvelopeAabbExpand = Mathf.Max(0.0f, Pass2GeomEnvelopeAabbExpand),
			AdaptiveTelemetryEnvelopeScalingEnabled = AdaptiveTelemetryEnvelopeScalingEnabled,
			AdaptiveEnvelopeControllerMode = ResolveAdaptiveEnvelopeControllerModeToken(),
			AdaptiveEnvelopePriorSource = ResolveAdaptiveEnvelopePriorSourceToken(),
			AdaptiveEnvelopeThresholdStatistic = ResolveAdaptiveEnvelopeThresholdStatisticToken(),
			AdaptiveTelemetryEnvelopeLowThreshold = Mathf.Clamp(AdaptiveTelemetryEnvelopeLowThreshold, 0.0f, 1.0f),
			AdaptiveTelemetryEnvelopeHighThreshold = Mathf.Clamp(AdaptiveTelemetryEnvelopeHighThreshold, 0.0f, 1.0f),
			AdaptiveEnvelopeHotThresholdPercentile = Mathf.Clamp(AdaptiveEnvelopeHotThresholdPercentile, 0f, 100f),
			AdaptiveEnvelopeWarmThresholdPercentile = Mathf.Clamp(AdaptiveEnvelopeWarmThresholdPercentile, 0f, 100f),
			AdaptiveEnvelopeRelaxedThresholdPercentile = Mathf.Clamp(AdaptiveEnvelopeRelaxedThresholdPercentile, 0f, 100f),
			AdaptiveEnvelopeTightScale = Mathf.Clamp(AdaptiveEnvelopeTightScale, 0.1f, 2.0f),
			AdaptiveEnvelopeWarmScale = Mathf.Clamp(AdaptiveEnvelopeWarmScale, 0.1f, 2.0f),
			AdaptiveEnvelopeNeutralScale = Mathf.Clamp(AdaptiveEnvelopeNeutralScale, 0.1f, 2.0f),
			AdaptiveEnvelopeRelaxedScale = Mathf.Clamp(AdaptiveEnvelopeRelaxedScale, 0.1f, 2.0f),
			UseGeometryTLASPruning = UseGeometryTLASPruning,
			Pass2HitBackFaces = Pass2HitBackFaces,
			Pass2HitFromInside = Pass2HitFromInside,
			Pass2ForceOnInstability = Pass2ForceOnInstability,
			Pass2ForceIfPrevHitLost = Pass2ForceIfPrevHitLost,
			Pass2LogQuickRayMissSamples = Pass2LogQuickRayMissSamples,
			UseSingleProbeThenSubdivide = UseSingleProbeThenSubdivide,
			NearestHitOnly = NearestHitOnly,
			UseInsightPlanePass2 = UseInsightPlanePass2,
			RenderStepPhaseLog = RenderStepPhaseLog,
			RenderStepBandLog = RenderStepBandLog,
			DebugEveryNPixels = DebugEveryNPixels,
			DebugMaxFilmRays = DebugMaxFilmRays,
			DebugGeomPruneAuditEnabled = DebugGeomPruneAuditEnabled,
			DebugGeomPruneAuditSamplesPerHealthWindow = Math.Max(0, DebugGeomPruneAuditSamplesPerHealthWindow),
			DebugGeomPruneAuditMaxExtraRayTestsPerSample = Math.Max(1, DebugGeomPruneAuditMaxExtraRayTestsPerSample),
			DebugGeomPruneAuditOnlyWhenCandidateZero = DebugGeomPruneAuditOnlyWhenCandidateZero,
			DebugGeomPruneAuditMaxMismatchLogsPerWindow = DebugGeomPruneAuditMaxMismatchLogsPerWindow,
				EnableProfiling = EnableProfiling,
				VerbosePerfLogs = VerbosePerfLogs,
				EnableFramePerf = EnableFramePerf,
				FramePerfVerbose = FramePerfVerbose,
				FramePerfLogEveryNFrames = FramePerfLogEveryNFrames,
				UseThreadedBands = UseThreadedBands,
				ThreadedBandWorkerCount = Math.Clamp(ThreadedBandWorkerCount, 1, 16),
				ThreadedBandRowsPerChunk = Math.Max(1, ThreadedBandRowsPerChunk),
				UseThreadedPass2CandidateEval = UseThreadedPass2CandidateEval,
				ThreadedPass2CandidateWorkers = Math.Clamp(ThreadedPass2CandidateWorkers, 1, 16),
				ThreadedPass2CandidateRowsPerChunk = Math.Max(1, ThreadedPass2CandidateRowsPerChunk),
				UseThreadedPass2QueryResolve = UseThreadedPass2QueryResolve,
				ThreadedPass2QueryWorkers = Math.Clamp(ThreadedPass2QueryWorkers, 1, 16),
				ThreadedPass2QueryRowsPerChunk = Math.Max(1, ThreadedPass2QueryRowsPerChunk),
				UseThreadedPass2LocalAccumulation = UseThreadedPass2LocalAccumulation,
				ThreadedPass2WorkerCount = Math.Clamp(ThreadedPass2WorkerCount, 1, 16),
				ThreadedPass2RowsPerChunk = Math.Max(1, ThreadedPass2RowsPerChunk),
				NeedColliderNames = NeedColliderNames,
			FixtureDebugHitColoringEnabled = FixtureDebugHitColoringEnabled,
			FixtureDebugSourceGroup = string.IsNullOrWhiteSpace(FixtureDebugSourceGroup) ? "fixture_source" : FixtureDebugSourceGroup.Trim(),
			FixtureDebugBackgroundGroup = string.IsNullOrWhiteSpace(FixtureDebugBackgroundGroup) ? "fixture_background" : FixtureDebugBackgroundGroup.Trim(),
			FixtureDebugSourceHitColor = FixtureDebugSourceHitColor,
			FixtureDebugBackgroundHitColor = FixtureDebugBackgroundHitColor,
			FixtureDebugAbsorbedColor = FixtureDebugAbsorbedColor,
			FixtureDebugMissColor = FixtureDebugMissColor,
			FixtureDebugColorAuthorityEnabled = FixtureDebugColorAuthorityEnabled,
			FixtureDebugSourceHighlightEnabled = FixtureDebugSourceHighlightEnabled,
			FixtureDebugTraceEnabled = FixtureDebugTraceEnabled,
			FixtureDebugTraceSampleModulo = Math.Max(1, FixtureDebugTraceSampleModulo),
			FixtureDebugTraceMaxLogsPerStep = Math.Max(0, FixtureDebugTraceMaxLogsPerStep),
			UseFieldSourceCache = UseFieldSourceCache,
			FieldSourceRefreshIntervalFrames = FieldSourceRefreshIntervalFrames,
			ShadingMode = ShadingMode,
			FlipNormalToCamera = FlipNormalToCamera,
			SkyColor = SkyColor
		};

		cfg.SoftGate = new EffectiveSoftGateSettings
		{
			EnableQuickRayMiss = Pass2SoftGateEnableQuickRayMiss,
			ScoringEnabled = Pass2SoftGateScoringEnabled,
			DisableOnOverload = DisableSoftGateOnOverload,
			UseRayBeamSettings = Pass2SoftGateUseRayBeamSettings,
			UseRayBeamSettingsActive = false,
			EffectiveStepLength = 0f,
			MinSegLenSteps = Pass2SoftGateMinSegLenSteps,
			MinSegmentLength = Pass2SoftGateMinSegmentLength,
			ScoreThreshold = Pass2SoftGateScoreThreshold,
			ScoreTurnAngleWeight = Pass2SoftGateScoreTurnAngleWeight,
			ScorePrevHitLostBonus = Pass2SoftGateScorePrevHitLostBonus,
			RandomProbeChance = Pass2SoftGateRandomProbeChance,
			ScoreBudgetPerFrame = Pass2SoftGateScoreBudgetPerFrame,
			MaxAttemptsPerPixel = Pass2SoftGateMaxAttemptsPerPixel,
			MaxAttemptsPerFrame = Pass2SoftGateMaxAttemptsPerFrame,
			MaxSubdividedCallsPerFrame = Pass2SoftGateMaxSubdividedCallsPerFrame,
			WatchdogMs = Pass2SoftGateWatchdogMs,
			WatchdogLogLimitPerFrame = Pass2SoftGateWatchdogLogLimitPerFrame,
			DebugEnabled = Pass2SoftGateDebugEnabled,
			DebugVerbosity = Pass2SoftGateDebugVerbosity,
			DebugSummaryPerFrame = Pass2SoftGateDebugSummaryPerFrame,
			DebugSummaryLogLimitPerFrame = Pass2SoftGateDebugSummaryLogLimitPerFrame
		};

		var rbr = _rbr;
		RayBeamRenderer.SharedSnapshot sharedSnap = rbr != null ? rbr.GetSharedSnapshot() : default;
		bool hasRenderer = sharedSnap.HasRenderer;

		cfg.SharedRaySnapshot = sharedSnap;
		LogSharedSnapshotIfChanged(in sharedSnap);
		UpdateSharedSnapshotMirror(in sharedSnap, force: false);

		if (hasRenderer)
		{
			cfg.RayMarch = new EffectiveRayMarchSettings
			{
				HasRenderer = true,
				StepsPerRay = sharedSnap.StepsPerRay,
				CollisionEveryNSteps = sharedSnap.CollisionEveryNSteps,
				StepLength = sharedSnap.StepLength,
				MinStepLength = sharedSnap.MinStepLength,
				MaxStepLength = sharedSnap.MaxStepLength,
				StepAdaptGain = sharedSnap.StepAdaptGain,
				UseIntegratedField = sharedSnap.UseIntegratedField,
				BendScale = sharedSnap.BendScale,
				FieldStrength = sharedSnap.FieldStrength,
				FieldCenter = sharedSnap.FieldCenter,
				FieldCenterIsCamera = sharedSnap.FieldCenterIsCamera,
				CollisionMask = sharedSnap.CollisionMask,
				CollisionRadius = sharedSnap.CollisionRadius,
				UseSphereSweepCollision = sharedSnap.UseSphereSweepCollision,
				UseInsightPlaneFilter = sharedSnap.UseInsightPlaneFilter,
				CollisionRaySubdivideThreshold = sharedSnap.CollisionRaySubdivideThreshold,
				MaxCollisionSubsteps = sharedSnap.MaxCollisionSubsteps,
				RequireHitToRender = sharedSnap.RequireHitToRender,
				StopOnHit = sharedSnap.StopOnHit,
				TerminateTrailOnHit = sharedSnap.TerminateTrailOnHit,
				UseScreenSpaceCollisionCadence = sharedSnap.UseScreenSpaceCollisionCadence,
				CollisionMaxErrorPixels = sharedSnap.CollisionMaxErrorPixels,
				MinDepthForError = sharedSnap.MinDepthForError,
				MinCollisionEveryNSteps = sharedSnap.MinCollisionEveryNSteps,
				DebugMode = sharedSnap.DebugMode,
				DebugNormalLen = sharedSnap.DebugNormalLen,
				DebugOverlayOwnedByFilm = sharedSnap.DebugOverlayOwnedByFilm,
				MaxSegPerRay = _rbr != null
					? _rbr.EstimateMaxSegmentsPerRay()
					: (Mathf.Max(1, sharedSnap.StepsPerRay / Mathf.Max(1, sharedSnap.CollisionEveryNSteps)) + 2)
			};
		}
		else
		{
			cfg.RayMarch = new EffectiveRayMarchSettings
			{
				HasRenderer = false,
				DebugMode = RayBeamRenderer.DebugDrawMode.Off,
				MaxSegPerRay = 64
			};
		}

		// Apply explicit RayBeamRenderer-derived scaling last.
		if (cfg.SoftGate.UseRayBeamSettings)
		{
			bool used = false;
			if (cfg.RayMarch.HasRenderer)
			{
				float stepLength = cfg.RayMarch.StepLength;
				float minStepLength = cfg.RayMarch.MinStepLength;
				float maxStepLength = cfg.RayMarch.MaxStepLength;
				float stepAdaptGain = cfg.RayMarch.StepAdaptGain;
				bool stepsFinite = float.IsFinite(stepLength)
					&& float.IsFinite(minStepLength)
					&& float.IsFinite(maxStepLength)
					&& float.IsFinite(stepAdaptGain);
				if (stepsFinite)
				{
					float minStep = Mathf.Min(minStepLength, maxStepLength);
					float maxStep = Mathf.Max(minStepLength, maxStepLength);
					float effStepLen = Mathf.Clamp(stepLength, minStep, maxStep);
					cfg.SoftGate.EffectiveStepLength = effStepLen;
					cfg.SoftGate.MinSegmentLength = cfg.SoftGate.MinSegLenSteps * effStepLen;

					float stepScale = effStepLen > 0f
						? Mathf.Clamp(1f / effStepLen, 0.25f, 4f)
						: 1f;
					float strideScale = Mathf.Clamp(1f / Mathf.Max(1, cfg.Film.PixelStride), 0.125f, 1f);
					int derivedMaxAttemptsPerFrame = Mathf.Clamp(
						Mathf.RoundToInt(Pass2SoftGateMaxAttemptsPerFrame * stepScale * strideScale),
						Pass2SoftGateMaxAttemptsPerFrameMin,
						Pass2SoftGateMaxAttemptsPerFrameMax);
					int derivedMaxSubdividedCallsPerFrame = Mathf.Clamp(
						Mathf.RoundToInt(Pass2SoftGateMaxSubdividedCallsPerFrame * stepScale * strideScale),
						Pass2SoftGateMaxSubdividedCallsPerFrameMin,
						Pass2SoftGateMaxSubdividedCallsPerFrameMax);

					cfg.SoftGate.MaxAttemptsPerFrame = derivedMaxAttemptsPerFrame;
					cfg.SoftGate.MaxSubdividedCallsPerFrame = derivedMaxSubdividedCallsPerFrame;
					used = true;
				}
			}
			cfg.SoftGate.UseRayBeamSettingsActive = used;
		}

		cfg.Research = ResearchModeConfig.DefaultsPreview();
		cfg.Research = ResearchModeMerge.Apply(in cfg.Research, in ResearchOverrides);
	}

	private static int ComputeEffectiveConfigHash(in EffectiveConfig cfg)
	{
		var hash = new HashCode();
		hash.Add(cfg.Broadphase.UseQuickRay);
		hash.Add(cfg.Broadphase.UseOverlap);
		hash.Add(cfg.Broadphase.Mode);
		hash.Add(cfg.Broadphase.Reason ?? string.Empty);
		hash.Add(cfg.Broadphase.Policy);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Broadphase.Margin));
		hash.Add(cfg.Broadphase.MaxResults);
		hash.Add(cfg.SoftGate.EnableQuickRayMiss);
		hash.Add(cfg.SoftGate.ScoringEnabled);
		hash.Add(cfg.SoftGate.DisableOnOverload);
		hash.Add(cfg.SoftGate.UseRayBeamSettings);
		hash.Add(cfg.SoftGate.UseRayBeamSettingsActive);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SoftGate.EffectiveStepLength));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SoftGate.MinSegLenSteps));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SoftGate.MinSegmentLength));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SoftGate.ScoreThreshold));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SoftGate.ScoreTurnAngleWeight));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SoftGate.ScorePrevHitLostBonus));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SoftGate.RandomProbeChance));
		hash.Add(cfg.SoftGate.ScoreBudgetPerFrame);
		hash.Add(cfg.SoftGate.MaxAttemptsPerPixel);
		hash.Add(cfg.SoftGate.MaxAttemptsPerFrame);
		hash.Add(cfg.SoftGate.MaxSubdividedCallsPerFrame);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SoftGate.WatchdogMs));
		hash.Add(cfg.SoftGate.WatchdogLogLimitPerFrame);
		hash.Add(cfg.SoftGate.DebugEnabled);
		hash.Add(cfg.SoftGate.DebugVerbosity);
		hash.Add(cfg.SoftGate.DebugSummaryPerFrame);
		hash.Add(cfg.SoftGate.DebugSummaryLogLimitPerFrame);
		hash.Add(cfg.RayMarch.HasRenderer);
		hash.Add(cfg.RayMarch.StepsPerRay);
		hash.Add(cfg.RayMarch.CollisionEveryNSteps);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.StepLength));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.MinStepLength));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.MaxStepLength));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.StepAdaptGain));
		hash.Add(cfg.RayMarch.UseIntegratedField);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.BendScale));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.FieldStrength));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.FieldCenter.X));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.FieldCenter.Y));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.FieldCenter.Z));
		hash.Add(cfg.RayMarch.FieldCenterIsCamera);
		hash.Add(cfg.RayMarch.CollisionMask);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.CollisionRadius));
		hash.Add(cfg.RayMarch.UseSphereSweepCollision);
		hash.Add(cfg.RayMarch.UseInsightPlaneFilter);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.CollisionRaySubdivideThreshold));
		hash.Add(cfg.RayMarch.MaxCollisionSubsteps);
		hash.Add(cfg.RayMarch.RequireHitToRender);
		hash.Add(cfg.RayMarch.StopOnHit);
		hash.Add(cfg.RayMarch.TerminateTrailOnHit);
		hash.Add(cfg.RayMarch.UseScreenSpaceCollisionCadence);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.CollisionMaxErrorPixels));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.MinDepthForError));
		hash.Add(cfg.RayMarch.MinCollisionEveryNSteps);
		hash.Add(cfg.RayMarch.DebugMode);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RayMarch.DebugNormalLen));
		hash.Add(cfg.RayMarch.DebugOverlayOwnedByFilm);
		hash.Add(cfg.RayMarch.MaxSegPerRay);
		hash.Add(cfg.Film.BaseWidth);
		hash.Add(cfg.Film.BaseHeight);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Film.ResolutionScale));
		hash.Add(cfg.Film.PixelStride);
		hash.Add(cfg.Film.RowsPerFrame);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Film.MaxDistance));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Film.Opacity));
		hash.Add(cfg.UpdateEveryFrame);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.UpdateEveryFrameBudgetMs));
		hash.Add(cfg.UpdateEveryFrameMaxRowsPerStep);
		hash.Add(cfg.RenderStepMaxMs);
		hash.Add(cfg.RenderStepMaxPixelsPerFrame);
		hash.Add(cfg.RenderStepMaxSegmentsPerFrame);
		hash.Add(cfg.RenderStepNoRowProgressRepeatLimit);
		hash.Add(cfg.TargetMsPerFrame);
		hash.Add(cfg.MinRowsPerFrame);
		hash.Add(cfg.MaxRowsPerFrameCap);
		hash.Add(cfg.AutoRangeDepth);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AutoRangeMin));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AutoRangeMax));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AutoRangeSmoothing));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AutoRangeSafety));
		hash.Add(cfg.DepthHistoryFrames);
		hash.Add(cfg.UseFieldGrid);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FieldGridCellSize));
		hash.Add(cfg.FieldGridRebuildEveryNFrames);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FieldGridBoundsPadding));
		hash.Add(cfg.UseCameraPropsBetaGamma);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.TinySegmentSkipLen));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.EarlyOutDistanceEps));
		hash.Add(cfg.UseAdaptiveSubsteps);
		hash.Add(cfg.UseBandHitSkip);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.BandSkipHitThreshold));
		hash.Add(cfg.BandSkipFrames);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.BandSkipInvalidatePosDelta));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.BandSkipInvalidateBasisDelta));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.BandSkipInvalidateRangeDelta));
		hash.Add(cfg.Pass1DoHitTest);
		hash.Add(cfg.Pass1ProbeEveryNSegments);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Pass1ProbeMinTravelDelta));
		hash.Add(cfg.UsePass2CollisionStride);
		hash.Add(cfg.Pass2CollisionStrideNear);
		hash.Add(cfg.Pass2CollisionStrideFar);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Pass2CollisionStrideFarStartT));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.MinSegLenForStrideSkip));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Pass2GeomEnvelopeRadiusScale));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Pass2GeomEnvelopeAabbExpand));
		hash.Add(cfg.AdaptiveTelemetryEnvelopeScalingEnabled);
		hash.Add(cfg.AdaptiveEnvelopeControllerMode ?? string.Empty);
		hash.Add(cfg.AdaptiveEnvelopePriorSource ?? string.Empty);
		hash.Add(cfg.AdaptiveEnvelopeThresholdStatistic ?? string.Empty);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AdaptiveTelemetryEnvelopeLowThreshold));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AdaptiveTelemetryEnvelopeHighThreshold));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AdaptiveEnvelopeHotThresholdPercentile));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AdaptiveEnvelopeWarmThresholdPercentile));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AdaptiveEnvelopeRelaxedThresholdPercentile));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AdaptiveEnvelopeTightScale));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AdaptiveEnvelopeWarmScale));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AdaptiveEnvelopeNeutralScale));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AdaptiveEnvelopeRelaxedScale));
		hash.Add(cfg.UseGeometryTLASPruning);
		hash.Add(cfg.Pass2HitBackFaces);
		hash.Add(cfg.Pass2HitFromInside);
		hash.Add(cfg.Pass2ForceOnInstability);
		hash.Add(cfg.Pass2ForceIfPrevHitLost);
		hash.Add(cfg.Pass2LogQuickRayMissSamples);
		hash.Add(cfg.UseSingleProbeThenSubdivide);
		hash.Add(cfg.NearestHitOnly);
		hash.Add(cfg.UseInsightPlanePass2);
		hash.Add(cfg.RenderStepPhaseLog);
		hash.Add(cfg.RenderStepBandLog);
		hash.Add(cfg.DebugEveryNPixels);
		hash.Add(cfg.DebugMaxFilmRays);
		hash.Add(cfg.DebugGeomPruneAuditEnabled);
		hash.Add(cfg.DebugGeomPruneAuditSamplesPerHealthWindow);
		hash.Add(cfg.DebugGeomPruneAuditMaxExtraRayTestsPerSample);
		hash.Add(cfg.DebugGeomPruneAuditOnlyWhenCandidateZero);
		hash.Add(cfg.DebugGeomPruneAuditMaxMismatchLogsPerWindow);
		hash.Add(cfg.EnableProfiling);
		hash.Add(cfg.VerbosePerfLogs);
		hash.Add(cfg.EnableFramePerf);
		hash.Add(cfg.FramePerfVerbose);
		hash.Add(cfg.FramePerfLogEveryNFrames);
		hash.Add(cfg.UseThreadedBands);
		hash.Add(cfg.ThreadedBandWorkerCount);
		hash.Add(cfg.ThreadedBandRowsPerChunk);
		hash.Add(cfg.UseThreadedPass2CandidateEval);
		hash.Add(cfg.ThreadedPass2CandidateWorkers);
		hash.Add(cfg.ThreadedPass2CandidateRowsPerChunk);
		hash.Add(cfg.UseThreadedPass2QueryResolve);
		hash.Add(cfg.ThreadedPass2QueryWorkers);
		hash.Add(cfg.ThreadedPass2QueryRowsPerChunk);
		hash.Add(cfg.UseThreadedPass2LocalAccumulation);
		hash.Add(cfg.ThreadedPass2WorkerCount);
		hash.Add(cfg.ThreadedPass2RowsPerChunk);
		hash.Add(cfg.NeedColliderNames);
		hash.Add(cfg.FixtureDebugHitColoringEnabled);
		hash.Add(cfg.FixtureDebugSourceGroup ?? string.Empty);
		hash.Add(cfg.FixtureDebugBackgroundGroup ?? string.Empty);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugSourceHitColor.R));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugSourceHitColor.G));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugSourceHitColor.B));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugSourceHitColor.A));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugBackgroundHitColor.R));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugBackgroundHitColor.G));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugBackgroundHitColor.B));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugBackgroundHitColor.A));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugAbsorbedColor.R));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugAbsorbedColor.G));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugAbsorbedColor.B));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugAbsorbedColor.A));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugMissColor.R));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugMissColor.G));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugMissColor.B));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.FixtureDebugMissColor.A));
		hash.Add(cfg.FixtureDebugColorAuthorityEnabled);
		hash.Add(cfg.FixtureDebugSourceHighlightEnabled);
		hash.Add(cfg.FixtureDebugTraceEnabled);
		hash.Add(cfg.FixtureDebugTraceSampleModulo);
		hash.Add(cfg.FixtureDebugTraceMaxLogsPerStep);
		hash.Add(cfg.UseFieldSourceCache);
		hash.Add(cfg.FieldSourceRefreshIntervalFrames);
		hash.Add(cfg.ShadingMode);
		hash.Add(cfg.FlipNormalToCamera);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SkyColor.R));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SkyColor.G));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SkyColor.B));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SkyColor.A));
		hash.Add(cfg.Research.ResearchEnabled);
		hash.Add(cfg.Research.ResearchTier);
		hash.Add(cfg.Research.TransportModel);
		hash.Add(cfg.Research.IntegratorKind);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Research.AbsPosTol));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Research.RelTol));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Research.DtMin));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Research.DtMax));
		hash.Add(cfg.Research.MaxStepsPerRay);
		hash.Add(cfg.Research.TrackNullConstraint);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.Research.NullConstraintTol));
		hash.Add(cfg.Research.EnableConstraintProjection);
		hash.Add(cfg.Research.MaxConstraintProjectionIters);
		hash.Add(cfg.Research.DeterministicMode);
		hash.Add(cfg.Research.FixedSeed);
		hash.Add(cfg.Research.MaxWorkPerFrameMs);
		hash.Add(cfg.Research.ValidationEnabled);
		hash.Add(cfg.Research.ValidationSuite);
		hash.Add(cfg.Research.ValidationReportPath ?? string.Empty);
		return hash.ToHashCode();
	}

	private void LogEffectiveConfigIfChanged(in EffectiveConfig cfg)
	{
		int hash = ComputeEffectiveConfigHash(in cfg);
		if (_hasEffectiveConfigHash && hash == _lastEffectiveConfigHash) return;
		_lastEffectiveConfigHash = hash;
		_hasEffectiveConfigHash = true;

		string broadphaseTag = string.IsNullOrEmpty(cfg.Broadphase.Reason) ? "resolved" : cfg.Broadphase.Reason;
			GD.Print(
				$"[EffectiveCfg] broadphase={cfg.Broadphase.ModeName}({broadphaseTag}) policy={cfg.Broadphase.Policy} quick={(cfg.Broadphase.UseQuickRay ? 1 : 0)} overlap={(cfg.Broadphase.UseOverlap ? 1 : 0)} " +
				$"softgate={(cfg.SoftGate.EnableQuickRayMiss ? 1 : 0)} score={(cfg.SoftGate.ScoringEnabled ? 1 : 0)} " +
				$"threadedBands={(cfg.UseThreadedBands ? 1 : 0)} bandWorkers={cfg.ThreadedBandWorkerCount} bandChunkRows={cfg.ThreadedBandRowsPerChunk} " +
				$"threadedP2Cand={(cfg.UseThreadedPass2CandidateEval ? 1 : 0)} p2CandWorkers={cfg.ThreadedPass2CandidateWorkers} p2CandRows={cfg.ThreadedPass2CandidateRowsPerChunk} " +
				$"threadedP2QR={(cfg.UseThreadedPass2QueryResolve ? 1 : 0)} p2QRWorkers={cfg.ThreadedPass2QueryWorkers} p2QRRows={cfg.ThreadedPass2QueryRowsPerChunk} " +
				$"threadedP2={(cfg.UseThreadedPass2LocalAccumulation ? 1 : 0)} p2Workers={cfg.ThreadedPass2WorkerCount} p2ChunkRows={cfg.ThreadedPass2RowsPerChunk} " +
				$"minSeg={cfg.SoftGate.MinSegmentLength:0.###} attempts={cfg.SoftGate.MaxAttemptsPerFrame} sub={cfg.SoftGate.MaxSubdividedCallsPerFrame} " +
			$"stride={cfg.Film.PixelStride} resScale={cfg.Film.ResolutionScale:0.###} rows={cfg.Film.RowsPerFrame} " +
			$"stepLen={cfg.RayMarch.StepLength:0.###} collRad={cfg.RayMarch.CollisionRadius:0.###} mask=0x{cfg.RayMarch.CollisionMask:X8} " +
			$"envRadScale={cfg.Pass2GeomEnvelopeRadiusScale:0.###} envAabbExpand={cfg.Pass2GeomEnvelopeAabbExpand:0.###} " +
			$"adaptiveEnv={(cfg.AdaptiveTelemetryEnvelopeScalingEnabled ? 1 : 0)} adaptiveMode={cfg.AdaptiveEnvelopeControllerMode} adaptivePrior={cfg.AdaptiveEnvelopePriorSource} adaptiveStat={cfg.AdaptiveEnvelopeThresholdStatistic} adaptiveLow={cfg.AdaptiveTelemetryEnvelopeLowThreshold:0.##} adaptiveHigh={cfg.AdaptiveTelemetryEnvelopeHighThreshold:0.##} adaptivePct={cfg.AdaptiveEnvelopeHotThresholdPercentile:0.#}/{cfg.AdaptiveEnvelopeWarmThresholdPercentile:0.#}/{cfg.AdaptiveEnvelopeRelaxedThresholdPercentile:0.#} adaptiveScales={cfg.AdaptiveEnvelopeTightScale:0.##}/{cfg.AdaptiveEnvelopeWarmScale:0.##}/{cfg.AdaptiveEnvelopeNeutralScale:0.##}/{cfg.AdaptiveEnvelopeRelaxedScale:0.##} " +
			$"geomPrune={(cfg.UseGeometryTLASPruning ? 1 : 0)} maxDist={cfg.Film.MaxDistance:0.###}");
	}

	private static int ComputeResearchConfigHash(in ResearchModeConfig cfg)
	{
		var hash = new HashCode();
		hash.Add(cfg.ResearchEnabled);
		hash.Add(cfg.ResearchTier);
		hash.Add(cfg.TransportModel);
		hash.Add(cfg.IntegratorKind);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.AbsPosTol));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.RelTol));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.DtMin));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.DtMax));
		hash.Add(cfg.MaxStepsPerRay);
		hash.Add(cfg.TrackNullConstraint);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.NullConstraintTol));
		hash.Add(cfg.EnableConstraintProjection);
		hash.Add(cfg.MaxConstraintProjectionIters);
		hash.Add(cfg.DeterministicMode);
		hash.Add(cfg.FixedSeed);
		hash.Add(cfg.MaxWorkPerFrameMs);
		hash.Add(cfg.ValidationEnabled);
		hash.Add(cfg.ValidationSuite);
		hash.Add(cfg.ValidationReportPath ?? string.Empty);
		return hash.ToHashCode();
	}

	private void LogResearchConfigIfChanged(in EffectiveConfig cfg)
	{
		if (!cfg.Research.ResearchEnabled)
		{
			_researchWasEnabledLastFrame = false;
			return;
		}

		int hash = ComputeResearchConfigHash(in cfg.Research);
		bool firstEnabled = !_researchWasEnabledLastFrame;
		bool changed = !_hasResearchSummaryHash || hash != _lastResearchSummaryHash;
		if (!firstEnabled && !changed)
		{
			return;
		}

		_lastResearchSummaryHash = hash;
		_hasResearchSummaryHash = true;
		_researchWasEnabledLastFrame = true;

		GD.Print(
			$"[ResearchMode] tier={cfg.Research.ResearchTier} transport={cfg.Research.TransportModel} integrator={cfg.Research.IntegratorKind} " +
			$"absTol={cfg.Research.AbsPosTol:0.###e+0} dt=[{cfg.Research.DtMin:0.###e+0},{cfg.Research.DtMax:0.###e+0}] deterministic={(cfg.Research.DeterministicMode ? 1 : 0)}");

		if (cfg.Research.ValidationEnabled)
		{
			GD.Print($"[ResearchMode][Validation] TODO: validation suite '{cfg.Research.ValidationSuite}' would run here.");
		}
	}

	private static int ComputeSharedSnapshotHash(in RayBeamRenderer.SharedSnapshot snap)
	{
		var hash = new HashCode();
		hash.Add(snap.HasRenderer);
		hash.Add(snap.StepsPerRay);
		hash.Add(snap.CollisionEveryNSteps);
		hash.Add(BitConverter.SingleToInt32Bits(snap.StepLength));
		hash.Add(BitConverter.SingleToInt32Bits(snap.MinStepLength));
		hash.Add(BitConverter.SingleToInt32Bits(snap.MaxStepLength));
		hash.Add(BitConverter.SingleToInt32Bits(snap.StepAdaptGain));
		hash.Add(snap.UseIntegratedField);
		hash.Add(BitConverter.SingleToInt32Bits(snap.BendScale));
		hash.Add(BitConverter.SingleToInt32Bits(snap.FieldStrength));
		hash.Add(BitConverter.SingleToInt32Bits(snap.FieldCenter.X));
		hash.Add(BitConverter.SingleToInt32Bits(snap.FieldCenter.Y));
		hash.Add(BitConverter.SingleToInt32Bits(snap.FieldCenter.Z));
		hash.Add(snap.FieldCenterIsCamera);
		hash.Add(snap.CollisionMask);
		hash.Add(BitConverter.SingleToInt32Bits(snap.CollisionRadius));
		hash.Add(snap.UseSphereSweepCollision);
		hash.Add(snap.UseInsightPlaneFilter);
		hash.Add(BitConverter.SingleToInt32Bits(snap.CollisionRaySubdivideThreshold));
		hash.Add(snap.MaxCollisionSubsteps);
		hash.Add(snap.RequireHitToRender);
		hash.Add(snap.StopOnHit);
		hash.Add(snap.TerminateTrailOnHit);
		hash.Add(snap.UseScreenSpaceCollisionCadence);
		hash.Add(BitConverter.SingleToInt32Bits(snap.CollisionMaxErrorPixels));
		hash.Add(BitConverter.SingleToInt32Bits(snap.MinDepthForError));
		hash.Add(snap.MinCollisionEveryNSteps);
		hash.Add(snap.DebugMode);
		hash.Add(BitConverter.SingleToInt32Bits(snap.DebugNormalLen));
		hash.Add(snap.DebugOverlayOwnedByFilm);
		return hash.ToHashCode();
	}

	private void LogSharedSnapshotIfChanged(in RayBeamRenderer.SharedSnapshot snap)
	{
		int hash = ComputeSharedSnapshotHash(in snap);
		if (_hasSharedSnapshotHash && hash == _lastSharedSnapshotHash) return;
		_lastSharedSnapshotHash = hash;
		_hasSharedSnapshotHash = true;

		if (!snap.HasRenderer)
		{
			GD.Print("[SharedSnap] renderer=missing");
			return;
		}

		GD.Print(
			$"[SharedSnap] steps={snap.StepsPerRay} stepLen={snap.StepLength:0.###} minStep={snap.MinStepLength:0.###} maxStep={snap.MaxStepLength:0.###} " +
			$"collEvery={snap.CollisionEveryNSteps} collRad={snap.CollisionRadius:0.###} mask=0x{snap.CollisionMask:X8} debug={snap.DebugMode}");
	}

	private void UpdateSharedSnapshotMirror(in RayBeamRenderer.SharedSnapshot snap, bool force)
	{
		int hash = ComputeSharedSnapshotHash(in snap);
		if (!force && _hasSharedSnapshotMirrorHash && hash == _lastSharedSnapshotMirrorHash) return;
		_lastSharedSnapshotMirrorHash = hash;
		_hasSharedSnapshotMirrorHash = true;

		SharedRbrHasRenderer = snap.HasRenderer;
		if (!snap.HasRenderer)
			return;

		SharedRbrStepsPerRay = snap.StepsPerRay;
		SharedRbrCollisionEveryNSteps = snap.CollisionEveryNSteps;
		SharedRbrStepLength = snap.StepLength;
		SharedRbrMinStepLength = snap.MinStepLength;
		SharedRbrMaxStepLength = snap.MaxStepLength;
		SharedRbrStepAdaptGain = snap.StepAdaptGain;
		SharedRbrUseIntegratedField = snap.UseIntegratedField;
		SharedRbrBendScale = snap.BendScale;
		SharedRbrFieldStrength = snap.FieldStrength;
		SharedRbrFieldCenter = snap.FieldCenter;
		SharedRbrFieldCenterIsCamera = snap.FieldCenterIsCamera;
		SharedRbrCollisionMask = snap.CollisionMask;
		SharedRbrCollisionRadius = snap.CollisionRadius;
		SharedRbrUseSphereSweepCollision = snap.UseSphereSweepCollision;
		SharedRbrUseInsightPlaneFilter = snap.UseInsightPlaneFilter;
		SharedRbrCollisionRaySubdivideThreshold = snap.CollisionRaySubdivideThreshold;
		SharedRbrMaxCollisionSubsteps = snap.MaxCollisionSubsteps;
		SharedRbrRequireHitToRender = snap.RequireHitToRender;
		SharedRbrStopOnHit = snap.StopOnHit;
		SharedRbrTerminateTrailOnHit = snap.TerminateTrailOnHit;
		SharedRbrUseScreenSpaceCollisionCadence = snap.UseScreenSpaceCollisionCadence;
		SharedRbrCollisionMaxErrorPixels = snap.CollisionMaxErrorPixels;
		SharedRbrMinDepthForError = snap.MinDepthForError;
		SharedRbrMinCollisionEveryNSteps = snap.MinCollisionEveryNSteps;
		SharedRbrDebugMode = snap.DebugMode;
		SharedRbrDebugNormalLen = snap.DebugNormalLen;
		SharedRbrDebugOverlayOwnedByFilm = snap.DebugOverlayOwnedByFilm;
	}

	private RenderHealthSample GetRenderHealthSampleFromEnd(int offset)
	{
		int idx = _renderHealthWrite - 1 - offset;
		if (idx < 0) idx += RenderHealthBufferSize;
		return _renderHealthSamples[idx];
	}

	private int CountRenderHealthModeSamplesInWindow(bool useGeometryTLASPruningMode, int maxWindow = 10)
	{
		int window = Math.Min(_renderHealthCount, maxWindow);
		int count = 0;
		for (int i = 0; i < window; i++)
		{
			RenderHealthSample s = GetRenderHealthSampleFromEnd(i);
			if (s.UseGeometryTLASPruning != useGeometryTLASPruningMode) break;
			count++;
		}
		return count;
	}

	private int GetRenderHealthMinP2SamplesForTrustEffective()
	{
		if (_renderHealthTestTrustEnforcementEnabled && _renderHealthTestMinPass2SamplesForTrustOverride > 0)
			return Math.Max(1, _renderHealthTestMinPass2SamplesForTrustOverride);
		return RenderHealthMinSamplesForTrust;
	}

	private int GetRenderHealthPass2SampleEveryForStep(bool useGeometryTLASPruningMode)
	{
		int sampleEvery = RenderHealthPass2SampleEveryNSegments;
		if (_renderHealthTestTrustEnforcementEnabled && _renderHealthTestPass2SampleEveryNSegmentsOverride > 0)
			sampleEvery = _renderHealthTestPass2SampleEveryNSegmentsOverride;
		sampleEvery = Math.Max(1, sampleEvery);
		if (!_renderHealthTestTrustEnforcementEnabled)
			return sampleEvery;

		int minP2 = GetRenderHealthMinP2SamplesForTrustEffective();
		int window = Math.Min(_renderHealthCount, 10);
		long modeP2Samples = 0;
		for (int i = 0; i < window; i++)
		{
			RenderHealthSample s = GetRenderHealthSampleFromEnd(i);
			if (s.UseGeometryTLASPruning != useGeometryTLASPruningMode)
				break;
			modeP2Samples += Math.Max(0L, s.Pass2SampledSegments);
		}

		if (modeP2Samples < minP2)
			sampleEvery = Math.Min(sampleEvery, 8);
		return sampleEvery;
	}

	/*
	Acceptance checklist for RenderHealth logs:
	(a) steady OFF: geomPruneRequested=0/1, geomPruneEffective=off, geometry totals/per-px valid only after stable OFF window; candidate/audit metrics are NA.
	(b) steady ON: geomPruneRequested=1, geomPruneEffective=on, candidate hist/seg-pixel counters and audit metrics valid only in trusted windows.
	(c) OFF->ON switch window: geomPruneSwitched=1, geomHealthPartial=1, geometry totals/per-px and saved% are NA.
	(d) ON->OFF switch window: geomPruneSwitched=1, geomHealthPartial=1, geometry totals/per-px and baseline/saved% are NA; OFF baseline relearns after stable OFF windows.
	*/
	private void LogRenderHealth(in RenderHealthSample latest, bool stalled)
	{
		int window = Math.Min(_renderHealthCount, 10);
		int modeWindowSamplesUsed = 0;
		long totalTraced = 0;
		long totalHits = 0;
		long totalRowsAdvanced = 0;
		long totalQuickRayZero = 0;
		long totalHybridFallback = 0;
		long totalHybridFallbackHits = 0;
		long totalHybridFallbackMisses = 0;
		long totalHybridNoCandidates = 0;
		long totalGeomCandidates = 0;
		long totalGeomCandidateSegments = 0;
		long totalGeomSegmentsQueried = 0;
		long totalGeomSegWithCandidates = 0;
		long totalGeomSegZeroCandidates = 0;
		long totalGeomPixelProcessed = 0;
		long totalGeomPixelHadAnyCandidates = 0;
		long totalGeomPixelNoCandidates = 0;
		long totalGeomHitAccepted = 0;
		long totalGeomHitRejected = 0;
		long totalGeomRayTestsTotal = 0;
		long totalGeomRayTestsAccepted = 0;
		long totalGeomRayTestsRejected = 0;
		long totalTracedForGeomMode = 0;
		long totalPass2SampledSegments = 0;
		double totalPass2RadiusSum = 0.0;
		float totalPass2RadiusMax = 0f;
		double totalPass2EnvDiagSum = 0.0;
		float totalPass2EnvDiagMax = 0f;
		double totalPass2EnvelopeInflationSum = 0.0;
		float totalPass2EnvelopeInflationMax = 0f;
		long totalPass2CandidateCount0 = 0;
		long totalPass2CandidateCount1To2 = 0;
		long totalPass2CandidateCount3To8 = 0;
		long totalPass2CandidateCount9To32 = 0;
		long totalPass2CandidateCount33Plus = 0;
		long totalPruneAuditSamples = 0;
		long totalPruneAuditFalseNeg = 0;
		long totalPruneAuditFalsePos = 0;
		long totalPruneAuditCandidateZeroButBaselineHit = 0;
		string topExit = "none";
		int topExitCount = 0;
		var exitCounts = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);

		for (int i = 0; i < window; i++)
		{
			RenderHealthSample s = GetRenderHealthSampleFromEnd(i);
			totalTraced += s.TracedPixels;
			totalHits += s.Hits;
			totalRowsAdvanced += s.RowsAdvanced;
			totalQuickRayZero += s.QuickRayZeroCount;
			totalHybridFallback += s.HybridFallbackCount;
			totalHybridFallbackHits += s.HybridFallbackHitCount;
			totalHybridFallbackMisses += s.HybridFallbackMissCount;
			totalHybridNoCandidates += s.HybridNoCandidateCount;
			// Mode switch -> reset mode-window counters to avoid mixed-window stats; required by regress invariants.
			// Only aggregate contiguous newest samples from the current prune mode.
			if (s.UseGeometryTLASPruning != latest.UseGeometryTLASPruning)
			{
				break;
			}
			// Keep geom aggregates mode-pure: never mix ON/OFF samples in one RenderHealth line.
			modeWindowSamplesUsed++;
			totalTracedForGeomMode += s.TracedPixels;
			totalGeomCandidates += s.GeomCandidatesTotal;
			totalGeomCandidateSegments += s.GeomCandidatesSegments;
			totalGeomSegmentsQueried += s.GeomSegmentsQueried;
			totalGeomSegWithCandidates += s.GeomSegWithCandidates;
			totalGeomSegZeroCandidates += s.GeomSegZeroCandidates;
			totalGeomPixelProcessed += s.GeomPixelProcessed;
			totalGeomPixelHadAnyCandidates += s.GeomPixelHadAnyCandidates;
			totalGeomPixelNoCandidates += s.GeomPixelNoCandidates;
			totalGeomHitAccepted += s.GeomHitAccepted;
			totalGeomHitRejected += s.GeomHitRejected;
			totalGeomRayTestsTotal += s.GeomRayTestsTotal;
			totalGeomRayTestsAccepted += s.GeomRayTestsAccepted;
			totalGeomRayTestsRejected += s.GeomRayTestsRejected;
			totalPass2SampledSegments += s.Pass2SampledSegments;
			totalPass2RadiusSum += s.Pass2RadiusSum;
			if (s.Pass2RadiusMax > totalPass2RadiusMax) totalPass2RadiusMax = s.Pass2RadiusMax;
			totalPass2EnvDiagSum += s.Pass2EnvDiagSum;
			if (s.Pass2EnvDiagMax > totalPass2EnvDiagMax) totalPass2EnvDiagMax = s.Pass2EnvDiagMax;
			totalPass2EnvelopeInflationSum += s.Pass2EnvelopeInflationSum;
			if (s.Pass2EnvelopeInflationMax > totalPass2EnvelopeInflationMax) totalPass2EnvelopeInflationMax = s.Pass2EnvelopeInflationMax;
			totalPass2CandidateCount0 += s.Pass2CandidateCount0;
			totalPass2CandidateCount1To2 += s.Pass2CandidateCount1To2;
			totalPass2CandidateCount3To8 += s.Pass2CandidateCount3To8;
			totalPass2CandidateCount9To32 += s.Pass2CandidateCount9To32;
			totalPass2CandidateCount33Plus += s.Pass2CandidateCount33Plus;
			totalPruneAuditSamples += s.PruneAuditSamples;
			totalPruneAuditFalseNeg += s.PruneAuditFalseNeg;
			totalPruneAuditFalsePos += s.PruneAuditFalsePos;
			totalPruneAuditCandidateZeroButBaselineHit += s.PruneAuditCandidateZeroButBaselineHit;
			if (!string.IsNullOrEmpty(s.BudgetExitReason))
			{
				exitCounts.TryGetValue(s.BudgetExitReason, out int count);
				exitCounts[s.BudgetExitReason] = count + 1;
			}
		}

		foreach (var kv in exitCounts)
		{
			if (kv.Value > topExitCount)
			{
				topExit = kv.Key;
				topExitCount = kv.Value;
			}
		}

		float hitRate = totalTraced > 0 ? (float)totalHits / totalTraced : 0f;
		double geomCandidatesAvg = totalGeomCandidateSegments > 0
			? (double)totalGeomCandidates / totalGeomCandidateSegments
			: 0.0;
		double pass2RadiusAvg = totalPass2SampledSegments > 0
			? totalPass2RadiusSum / totalPass2SampledSegments
			: 0.0;
		double pass2EnvDiagAvg = totalPass2SampledSegments > 0
			? totalPass2EnvDiagSum / totalPass2SampledSegments
			: 0.0;
		double pass2EnvelopeInflationAvg = totalPass2SampledSegments > 0
			? totalPass2EnvelopeInflationSum / totalPass2SampledSegments
			: 0.0;
		long totalGeomRayTestsEligible = latest.UseGeometryTLASPruning
			? totalGeomPixelHadAnyCandidates
			: 0L;
		long totalGeomRayTestsForTrust = Math.Max(totalGeomRayTestsTotal, totalGeomRayTestsEligible);
		string geomPruneMode = latest.UseGeometryTLASPruning ? "on" : "off";
		string geomPruneEffective = geomPruneMode;
		int geomPruneRequestedBit = latest.GeomPruneRequested ? 1 : 0;
		bool modeHasEnoughSamples = modeWindowSamplesUsed >= RenderHealthMinModeSamplesForTrust;
		int minP2 = GetRenderHealthMinP2SamplesForTrustEffective();
		int trustCfgSampleEvery = RenderHealthPass2SampleEveryNSegments;
		if (_renderHealthTestTrustEnforcementEnabled && _renderHealthTestPass2SampleEveryNSegmentsOverride > 0)
			trustCfgSampleEvery = _renderHealthTestPass2SampleEveryNSegmentsOverride;
		trustCfgSampleEvery = Math.Max(1, trustCfgSampleEvery);
		bool pruneOnHasEnoughP2Samples = totalPass2SampledSegments >= minP2;
		bool hasGeomSamples = totalPass2SampledSegments > 0
			|| totalGeomPixelProcessed > 0
			|| totalGeomSegmentsQueried > 0;
		bool testTrustGeomPixMet = !_renderHealthTestTrustEnforcementEnabled
			|| totalGeomPixelProcessed >= _renderHealthTestMinGeomPixProcessedPerWindow;
		bool testTrustGeomRayTestsMet = !_renderHealthTestTrustEnforcementEnabled
			|| totalGeomRayTestsForTrust >= _renderHealthTestMinGeomRayTestsTotalPerWindow;
		bool testTrustGeomWorkMet = testTrustGeomPixMet && testTrustGeomRayTestsMet;
		bool geomWindowPartial = _geomPruneSwitchedThisWindow == 1
			|| !modeHasEnoughSamples
			|| !hasGeomSamples
			|| !testTrustGeomWorkMet
			|| (latest.UseGeometryTLASPruning && !pruneOnHasEnoughP2Samples);
		bool geomWindowTrusted = !geomWindowPartial;
		bool geomModeHasPixelDenominator = totalGeomPixelProcessed > 0;
		double geomRayTestsPerPixelOn = (latest.UseGeometryTLASPruning && geomModeHasPixelDenominator)
			? (double)totalGeomRayTestsForTrust / totalGeomPixelProcessed
			: double.NaN;
		double geomRayTestsPerPixelOffCurrent = (!latest.UseGeometryTLASPruning && geomModeHasPixelDenominator)
			? (double)totalGeomRayTestsForTrust / totalGeomPixelProcessed
			: double.NaN;
		bool geomRayTestsPerPixelOnNumeric = latest.UseGeometryTLASPruning
			&& geomModeHasPixelDenominator
			&& double.IsFinite(geomRayTestsPerPixelOn);
		bool geomRayTestsPerPixelOffCurrentNumeric = !latest.UseGeometryTLASPruning
			&& geomModeHasPixelDenominator
			&& double.IsFinite(geomRayTestsPerPixelOffCurrent);
		bool geomModeMetricNumeric = latest.UseGeometryTLASPruning
			? geomRayTestsPerPixelOnNumeric
			: geomRayTestsPerPixelOffCurrentNumeric;
		// Trusted requires samples present and numeric per-mode metric; avoids trusted-with-na failures in regress.
		bool geomMetricsTrusted = geomWindowTrusted && geomModeMetricNumeric;
		bool showPruneOnMetrics = latest.UseGeometryTLASPruning
			&& geomMetricsTrusted;
		string geomWindowTrustReason;
		if (_geomPruneSwitchedThisWindow == 1)
		{
			geomWindowTrustReason = "mode_switch";
		}
		else if (!modeHasEnoughSamples)
		{
			geomWindowTrustReason = "low_mode_samples";
		}
		else if (!hasGeomSamples)
		{
			geomWindowTrustReason = "no_geom_samples";
		}
		else if (!testTrustGeomPixMet)
		{
			geomWindowTrustReason = "low_geom_pix";
		}
		else if (!testTrustGeomRayTestsMet)
		{
			geomWindowTrustReason = "low_raytests";
		}
		else if (latest.UseGeometryTLASPruning && !pruneOnHasEnoughP2Samples)
		{
			geomWindowTrustReason = "low_p2samp";
		}
		else if (!geomModeMetricNumeric)
		{
			geomWindowTrustReason = latest.UseGeometryTLASPruning ? "missing_on_metric" : "missing_off_metric";
		}
		else if (geomMetricsTrusted)
		{
			geomWindowTrustReason = "ok";
		}
		else
		{
			geomWindowTrustReason = "low_mode_samples";
		}
		int geomHealthPartial = geomMetricsTrusted ? 0 : 1;
		string geomCandAvgStr = showPruneOnMetrics
			? geomCandidatesAvg.ToString("0.###")
			: "na";
		string geomSegQueriedStr = showPruneOnMetrics ? totalGeomSegmentsQueried.ToString() : "na";
		string geomSegWithCandidatesStr = showPruneOnMetrics ? totalGeomSegWithCandidates.ToString() : "na";
		string geomSegZeroStr = showPruneOnMetrics ? totalGeomSegZeroCandidates.ToString() : "na";
		string geomPixProcessedStr = showPruneOnMetrics ? totalGeomPixelProcessed.ToString() : "na";
		string geomPixHadAnyCandidatesStr = showPruneOnMetrics ? totalGeomPixelHadAnyCandidates.ToString() : "na";
		string geomPixNoCandStr = showPruneOnMetrics ? totalGeomPixelNoCandidates.ToString() : "na";
		double geomSegZeroRatePct = 100.0 * totalGeomSegZeroCandidates / Math.Max(1L, totalGeomSegmentsQueried);
		double geomPixNoCandRatePct = 100.0 * totalGeomPixelNoCandidates / Math.Max(1L, totalGeomPixelProcessed);
		string geomSegZeroRatePctStr = showPruneOnMetrics ? geomSegZeroRatePct.ToString("0.00") : "na";
		string geomPixNoCandRatePctStr = showPruneOnMetrics ? geomPixNoCandRatePct.ToString("0.00") : "na";
		string cand0Str = showPruneOnMetrics ? totalPass2CandidateCount0.ToString() : "na";
		string cand1to2Str = showPruneOnMetrics ? totalPass2CandidateCount1To2.ToString() : "na";
		string cand3to8Str = showPruneOnMetrics ? totalPass2CandidateCount3To8.ToString() : "na";
		string cand9to32Str = showPruneOnMetrics ? totalPass2CandidateCount9To32.ToString() : "na";
		string cand33PlusStr = showPruneOnMetrics ? totalPass2CandidateCount33Plus.ToString() : "na";
		bool pruneAuditActive = showPruneOnMetrics && latest.PruneAuditEnabled;
		string geomPruneAuditSampStr = pruneAuditActive ? totalPruneAuditSamples.ToString() : "na";
		string geomPruneAuditFalseNegStr = pruneAuditActive ? totalPruneAuditFalseNeg.ToString() : "na";
		string geomPruneAuditFalsePosStr = pruneAuditActive ? totalPruneAuditFalsePos.ToString() : "na";
		string geomPruneAuditCand0HitStr = pruneAuditActive ? totalPruneAuditCandidateZeroButBaselineHit.ToString() : "na";
		// FalseNegRate is defined as FalseNeg / Samp over the current RenderHealth window.
		string geomPruneAuditFalseNegRateStr = pruneAuditActive
			? ((double)totalPruneAuditFalseNeg / Math.Max(1L, totalPruneAuditSamples)).ToString("0.###")
			: "na";
		string geomHitOkStr = geomMetricsTrusted ? totalGeomHitAccepted.ToString() : "na";
		string geomHitRejectStr = geomMetricsTrusted ? totalGeomHitRejected.ToString() : "na";
		// Geometry totals/per-px are trust-gated to avoid misleading zeros in mode-switch windows.
		string geomRayTestsTotalStr = geomMetricsTrusted ? totalGeomRayTestsForTrust.ToString() : "na";
		string geomRayTestsAcceptedStr = geomMetricsTrusted ? totalGeomRayTestsAccepted.ToString() : "na";
		string geomRayTestsRejectedStr = geomMetricsTrusted ? totalGeomRayTestsRejected.ToString() : "na";
		double geomRayTestsPerPixelOffBaseline = _geomRayTestsOffPerPixelBaselineReady
			? _geomRayTestsOffPerPixelBaseline
			: -1.0;
		string geomRayTestsSavedPct = "na";
		bool geomSavedPctAvailableForTest = false;
		double geomSavedPctForTest = 0.0;
		if (latest.UseGeometryTLASPruning
			&& geomMetricsTrusted
			&& geomRayTestsPerPixelOn >= 0.0
			&& geomRayTestsPerPixelOffBaseline > 0.0
			&& _geomRayTestsOffPerPixelBaselineReady)
		{
			double savedPct = 100.0 * (1.0 - (geomRayTestsPerPixelOn / geomRayTestsPerPixelOffBaseline));
			// Clamp to [0,100]: this metric is defined as "saved work", so negative regressions are reported as 0.
			savedPct = Math.Clamp(savedPct, 0.0, 100.0);
			geomRayTestsSavedPct = savedPct.ToString("0.##");
			geomSavedPctAvailableForTest = true;
			geomSavedPctForTest = savedPct;
		}
		// OFF-per-px display semantics:
		// - prune OFF + trusted => current OFF per-px
		// - prune ON + trusted + baseline ready => learned OFF baseline
		// - otherwise => na
		double geomRayTestsPerPixelOffDisplay = latest.UseGeometryTLASPruning
			? geomRayTestsPerPixelOffBaseline
			: geomRayTestsPerPixelOffCurrent;
		bool geomRayTestsPerPixelOffDisplayNumeric = latest.UseGeometryTLASPruning
			? (double.IsFinite(geomRayTestsPerPixelOffDisplay) && geomRayTestsPerPixelOffDisplay >= 0.0)
			: geomRayTestsPerPixelOffCurrentNumeric;
		string geomRayTestsPerPxOnStr = (geomMetricsTrusted && geomRayTestsPerPixelOnNumeric) ? geomRayTestsPerPixelOn.ToString("0.###") : "na";
		string geomRayTestsPerPxOffStr = (geomMetricsTrusted && geomRayTestsPerPixelOffDisplayNumeric) ? geomRayTestsPerPixelOffDisplay.ToString("0.###") : "na";
		bool geomCounterGuardEnabled = DebugLogConfig.EnableGeomRejectSample || DebugGeomCounterGuardEnabled;
		bool geomSegZeroDrift = totalGeomSegZeroCandidates > totalGeomSegmentsQueried;
		bool geomSegWithCandidatesDrift = totalGeomSegWithCandidates > totalGeomSegmentsQueried;
		bool geomPixNoCandDrift = totalGeomPixelNoCandidates > totalGeomPixelProcessed;
		if (geomCounterGuardEnabled && showPruneOnMetrics && (geomSegZeroDrift || geomSegWithCandidatesDrift || geomPixNoCandDrift))
		{
			GD.PrintErr(
				$"[RenderHealth][Warn] geomCounterSanity segZero={totalGeomSegZeroCandidates} segWithCandidates={totalGeomSegWithCandidates} segQueried={totalGeomSegmentsQueried} " +
				$"pixNoCand={totalGeomPixelNoCandidates} pixProcessed={totalGeomPixelProcessed} drift(segZero={ (geomSegZeroDrift ? 1 : 0) },segWithCandidates={ (geomSegWithCandidatesDrift ? 1 : 0) },pixNoCand={ (geomPixNoCandDrift ? 1 : 0) }) " +
				$"step={latest.StepIndex} rows={latest.RowCursorBefore}->{latest.RowCursorAfter} bands={latest.BandsProcessed} prune={geomPruneMode} window={window}");
		}
		int geomRejectWarnThreshold = Math.Max(1, GeomHitRejectWarnThresholdPerWindow);
		if (geomMetricsTrusted && totalGeomHitRejected >= geomRejectWarnThreshold)
		{
			GD.PrintErr(
				$"[RenderHealth][Warn] geomHitRejectSpike reject={totalGeomHitRejected} threshold={geomRejectWarnThreshold} " +
				$"mode={geomPruneMode} window={window} step={latest.StepIndex} row={latest.RowCursorAfter}");
		}
		string geomRejectSampleDominant = "none";
		if (_geomRejectSampleCidNotInGeometryList >= _geomRejectSampleCidInGeometryListNotInCandidates
			&& _geomRejectSampleCidNotInGeometryList >= _geomRejectSampleCandidateContainsCid
			&& _geomRejectSampleCidNotInGeometryList > 0)
		{
			geomRejectSampleDominant = "CID_NOT_IN_GEOMETRY_LIST";
		}
		else if (_geomRejectSampleCidInGeometryListNotInCandidates >= _geomRejectSampleCandidateContainsCid
			&& _geomRejectSampleCidInGeometryListNotInCandidates > 0)
		{
			geomRejectSampleDominant = "CID_IN_GEOMETRY_LIST_NOT_IN_CANDIDATES";
		}
		else if (_geomRejectSampleCandidateContainsCid > 0)
		{
			geomRejectSampleDominant = "CID_IN_CANDIDATES_UNEXPECTED";
		}
		string exitTag = string.IsNullOrEmpty(latest.BudgetExitReason) ? "none" : latest.BudgetExitReason;
		UpdateRenderThroughputMetricsFromRenderHealth(totalRowsAdvanced, geomMetricsTrusted);
		_testHasRenderHealthSnapshot = true;
		_testLastGeomTrusted = geomMetricsTrusted;
		_testLastGeomSavedPctAvailable = geomSavedPctAvailableForTest;
		_testLastGeomSavedPct = geomSavedPctForTest;
		_testLastGeomPerPxOnAvailable = geomMetricsTrusted && geomRayTestsPerPixelOnNumeric;
		_testLastGeomPerPxOn = _testLastGeomPerPxOnAvailable ? geomRayTestsPerPixelOn : 0.0;
		_testLastGeomPerPxOffAvailable = geomMetricsTrusted && geomRayTestsPerPixelOffDisplayNumeric;
		_testLastGeomPerPxOff = _testLastGeomPerPxOffAvailable ? geomRayTestsPerPixelOffDisplay : 0.0;
		_testLastGeomPixProcessedRaw = totalGeomPixelProcessed;
		_testLastGeomRayTestsTotalRaw = totalGeomRayTestsForTrust;
		_testLastGeomPixNoCandRaw = totalGeomPixelNoCandidates;
		_testLastP2SampRaw = totalPass2SampledSegments;
		_testLastGeomSegZeroRatePctAvailable = totalGeomSegmentsQueried > 0;
		_testLastGeomSegZeroRatePct = _testLastGeomSegZeroRatePctAvailable ? geomSegZeroRatePct : 0.0;
		_testLastTopExit = string.IsNullOrEmpty(topExit) ? "none" : topExit;
		_testLastGeomTrustReason = geomWindowTrustReason;
		_testLastTrustGeomPixMet = testTrustGeomPixMet;
		_testLastTrustRayTestsMet = testTrustGeomRayTestsMet;
		_testLastTrustP2Met = totalPass2SampledSegments >= minP2;
		string presentCoverageStr = (_filmWidth > 0 && _filmHeight > 0) ? latest.PresentCoverageRatio.ToString("0.000") : "na";
		string fullRefreshFramesStr = latest.FullRefreshMeasured && latest.FramesToFullRefresh > 0 ? latest.FramesToFullRefresh.ToString() : "na";
		string fullRefreshMsStr = latest.FullRefreshMeasured && latest.TimeToFullRefreshMs > 0.0 ? latest.TimeToFullRefreshMs.ToString("0.00") : "na";
		string fullRefreshFpsStr = latest.FullRefreshMeasured && latest.EffectiveFullRefreshFps > 0.0 ? latest.EffectiveFullRefreshFps.ToString("0.00") : "na";
		GD.Print(
			$"[RenderHealth] step={latest.StepIndex} lastRow={latest.RowCursorAfter} rowsAdv={latest.RowsAdvanced} bands={latest.BandsProcessed} " +
			$"stalledSteps={_renderHealthStallSteps} exit={exitTag} topExit={topExit} hitRate={hitRate:0.###} " +
			$"presentPx={latest.PresentPixelsUpdated} presentCover={presentCoverageStr} fullRefreshFrames={fullRefreshFramesStr} fullRefreshMs={fullRefreshMsStr} fullRefreshFps={fullRefreshFpsStr} " +
			$"avgSteps={latest.AvgStepsPerTracedPixel:0.###} qray0={totalQuickRayZero} hybridFallback={totalHybridFallback} " +
			$"hybridFallbackHit={totalHybridFallbackHits} hybridFallbackMiss={totalHybridFallbackMisses} noCandidates={totalHybridNoCandidates} " +
			$"geomCandAvg={geomCandAvgStr} geomSegQueried={geomSegQueriedStr} geomSegWithCandidates={geomSegWithCandidatesStr} geomSegZero={geomSegZeroStr} geomSegZeroRatePct={geomSegZeroRatePctStr} " +
			$"geomPixProcessed={geomPixProcessedStr} geomPixHadAnyCandidates={geomPixHadAnyCandidatesStr} geomPixNoCand={geomPixNoCandStr} geomPixNoCandRatePct={geomPixNoCandRatePctStr} " +
			$"geomHitOk={geomHitOkStr} geomHitReject={geomHitRejectStr} " +
			$"geomPrune={geomPruneMode} geomPruneRequested={geomPruneRequestedBit} geomPruneEffective={geomPruneEffective} geomRayTestsTotal={geomRayTestsTotalStr} geomRayTestsAccepted={geomRayTestsAcceptedStr} geomRayTestsRejected={geomRayTestsRejectedStr} " +
			$"geomRayTestsPerPxOn={geomRayTestsPerPxOnStr} geomRayTestsPerPxOff={geomRayTestsPerPxOffStr} geomRayTestsSavedPct={geomRayTestsSavedPct} geomPruneSwitched={_geomPruneSwitchedThisWindow} geomTrusted={(geomMetricsTrusted ? 1 : 0)} geomHealthPartial={geomHealthPartial} geomHealthModeSamples={modeWindowSamplesUsed} geomTrustReason={geomWindowTrustReason} " +
			$"geomRejectSampleMissing={_geomRejectSampleCidNotInGeometryList} geomRejectSampleInList={_geomRejectSampleCidInGeometryListNotInCandidates} " +
			$"geomRejectSampleCandHit={_geomRejectSampleCandidateContainsCid} geomRejectSampleDominant={geomRejectSampleDominant} " +
			$"p2Samp={totalPass2SampledSegments} radAvg={pass2RadiusAvg:0.###} radMax={totalPass2RadiusMax:0.###} envDiagAvg={pass2EnvDiagAvg:0.###} envDiagMax={totalPass2EnvDiagMax:0.###} envInflAvg={pass2EnvelopeInflationAvg:0.###} envInflMax={totalPass2EnvelopeInflationMax:0.###} " +
			$"cand0={cand0Str} cand1to2={cand1to2Str} cand3to8={cand3to8Str} " +
			$"cand9to32={cand9to32Str} cand33p={cand33PlusStr} " +
			$"geomPruneAuditSamp={geomPruneAuditSampStr} geomPruneAuditFalseNeg={geomPruneAuditFalseNegStr} geomPruneAuditFalsePos={geomPruneAuditFalsePosStr} " +
			$"geomPruneAuditCand0Hit={geomPruneAuditCand0HitStr} geomPruneAuditFalseNegRate={geomPruneAuditFalseNegRateStr}");
		if (_rbr != null && GodotObject.IsInstanceValid(_rbr) && _rbr.DerivativeAwareLogMetrics)
		{
			RayBeamRenderer.DerivativeAwareSteppingDiagnosticsSnapshot derivativeDiag = _rbr.GetDerivativeAwareSteppingDiagnosticsSnapshot();
			if (derivativeDiag.Enabled)
			{
				GD.Print(
					$"[RenderHealth][DerivativeStep] step={latest.StepIndex} samples={derivativeDiag.SampleCount} candidateAdjust={derivativeDiag.CandidateAdjustmentCount} " +
					$"hysteresisActive={derivativeDiag.HysteresisEngagedSampleCount} hysteresisEnter={derivativeDiag.HysteresisEnterCount} hysteresisExit={derivativeDiag.HysteresisExitCount} " +
					$"activeSpans={derivativeDiag.ActiveSpanCount} meanActiveSpan={derivativeDiag.MeanActiveSpanLength:0.###} maxActiveSpan={derivativeDiag.MaxActiveSpanLength} " +
					$"singleSampleSpans={derivativeDiag.SingleSampleSpanCount} multiSampleSpans={derivativeDiag.MultiSampleSpanCount} " +
					$"appliedAdjust={derivativeDiag.EngagedStepCount} loggedApplied={derivativeDiag.LoggedAppliedAdjustmentCount} " +
					$"kMean={derivativeDiag.MeanK:0.######} kMin={derivativeDiag.MinK:0.######} kMax={derivativeDiag.MaxK:0.######} " +
					$"absDkMean={derivativeDiag.MeanAbsDk:0.######} absDkMin={derivativeDiag.MinAbsDk:0.######} absDkMax={derivativeDiag.MaxAbsDk:0.######} " +
					$"absD2kMean={derivativeDiag.MeanAbsD2k:0.######} absD2kMin={derivativeDiag.MinAbsD2k:0.######} absD2kMax={derivativeDiag.MaxAbsD2k:0.######} " +
					$"difficultyMean={derivativeDiag.MeanDifficulty:0.######} difficultyMin={derivativeDiag.MinDifficulty:0.######} difficultyMax={derivativeDiag.MaxDifficulty:0.######} " +
					$"stepBeforeMean={derivativeDiag.MeanStepBefore:0.######} stepAfterMean={derivativeDiag.MeanStepAfter:0.######} " +
					$"candidateScaleUp={derivativeDiag.CandidateScaleUpCount} candidateScaleDown={derivativeDiag.CandidateScaleDownCount} " +
					$"scaleUp={derivativeDiag.ScaleUpCount} scaleDown={derivativeDiag.ScaleDownCount} metricRetries={derivativeDiag.MetricSubdivisionRetryCount}");
			}
		}
		if (_renderHealthTestTrustEnforcementEnabled)
		{
			GD.Print(
				$"[RenderHealth][GeomCoverage] step={latest.StepIndex} geomPrune={geomPruneMode} " +
				$"geomPixProcessedRaw={totalGeomPixelProcessed} geomRayTestsTotalRaw={totalGeomRayTestsForTrust} " +
				$"geomSegQueriedRaw={totalGeomSegmentsQueried} " +
				$"pass2PixelsRaw={totalGeomPixelProcessed} geomPixHadAnyCandidatesRaw={totalGeomPixelHadAnyCandidates} " +
				$"geomPixNoCandRaw={totalGeomPixelNoCandidates} geomRayTestsEligibleRaw={totalGeomRayTestsEligible} p2SampRaw={totalPass2SampledSegments}");
		}
		if (_renderHealthTestTrustEnforcementEnabled)
		{
			GD.Print(
				$"[RenderHealth][TrustGateDebug] geomPixProcessedRaw={totalGeomPixelProcessed} geomRayTestsTotalRaw={totalGeomRayTestsForTrust} geomRayTestsEligibleRaw={totalGeomRayTestsEligible} " +
				$"trustGeomPixMet={(testTrustGeomPixMet ? 1 : 0)} trustRayTestsMet={(testTrustGeomRayTestsMet ? 1 : 0)} " +
				$"minGeomPix={_renderHealthTestMinGeomPixProcessedPerWindow} minRayTests={_renderHealthTestMinGeomRayTestsTotalPerWindow}");
			GD.Print(
				$"[RenderHealth][TrustCfg] p2SampleEveryNSeg={trustCfgSampleEvery} minP2SamplesForTrust={minP2}");
		}
	}

	private static long ComputeCounterDelta(long current, ref long lastSample)
	{
		long delta = current - lastSample;
		if (delta < 0) delta = current;
		lastSample = current;
		return delta;
	}

	private void EnsureTileMetricSubtileCapacity(int subtileCount)
	{
		if (_tileMetricCurrentSubtiles.Length < subtileCount)
			_tileMetricCurrentSubtiles = new TileMetricAccumulator[subtileCount];
		for (int i = 0; i < subtileCount; i++)
			_tileMetricCurrentSubtiles[i] = default;
	}

	private static string BuildTileMetricsStableId(int bandIndex, int subtileIndex, int x, int y, int width, int height)
	{
		return BuildTileMetricsSpatialStableId(x, y, width, height);
	}

	private static string BuildTileMetricsSpatialStableId(int x, int y, int width, int height)
	{
		return $"slice_y{y}_h{height}_x{x}_w{width}";
	}

	private static long BuildTileMetricBandHistoryKey(int y, int height)
	{
		return ((long)y << 32) ^ (uint)height;
	}

	private void EnsureTileMetricExecutionOrderCapacity(int subtileCount)
	{
		if (_tileMetricCurrentExecutionOrder.Length < subtileCount)
			_tileMetricCurrentExecutionOrder = new int[subtileCount];
		for (int i = 0; i < subtileCount; i++)
			_tileMetricCurrentExecutionOrder[i] = i;
	}

	private static TilePriorityCandidate BuildTilePriorityCandidateFromPrior(int subtileIndex, int x, int width, in TileMetricPersistentPrior prior)
	{
		TileMetricAccumulator synthetic = default;
		synthetic.RaysTraced = (long)Math.Round(Math.Max(0.0, prior.RaysTraced), MidpointRounding.AwayFromZero);
		synthetic.Hits = (long)Math.Round(Math.Max(0.0, prior.Hits), MidpointRounding.AwayFromZero);
		synthetic.NoCandidatePixels = (long)Math.Round(Math.Max(0.0, prior.NoCandidatePixels), MidpointRounding.AwayFromZero);
		synthetic.GeomPixelsProcessed = (long)Math.Round(Math.Max(0.0, prior.GeomPixelsProcessed), MidpointRounding.AwayFromZero);
		return new TilePriorityCandidate(subtileIndex, x, width, synthetic);
	}

	private static double ResolveTilePriorityPriorBlendWeight(bool hasCurrent, in TilePriorityCandidate current, in TilePriorityCandidate prior, bool usedNeighborSeed)
	{
		if (prior.Rays <= 0 || prior.Hits < 0)
			return 0.0;
		double confidence = Math.Clamp(prior.Rays / 48.0, 0.0, 1.0);
		double cautiousWeight = TileMetricsPersistentPriorBlendWeight * confidence;
		if (!hasCurrent)
			return Math.Clamp(Math.Max(cautiousWeight, usedNeighborSeed ? 0.8 : 1.0), 0.0, 1.0);

		bool weakCurrent = current.Rays <= 32 || current.Hits <= 0 || current.NoCandidateRatio >= 0.95;
		bool strongerPrior = prior.HitYield > current.HitYield || prior.Hits > current.Hits;
		if (weakCurrent && strongerPrior)
			cautiousWeight = Math.Min(1.0, cautiousWeight + TileMetricsPersistentPriorWeakCurrentBoost);
		if (usedNeighborSeed)
			cautiousWeight = Math.Min(1.0, cautiousWeight + 0.1);
		return Math.Clamp(cautiousWeight, 0.0, 1.0);
	}

	private bool TryBuildNearbyPersistentPrior(int subtileIndex, int subtileX, int subtileWidth, out TilePriorityCandidate priorCandidate, out double priorBlendWeight)
	{
		priorCandidate = default;
		priorBlendWeight = 0.0;
		if (!ExperimentalPersistentSubtileSchedulerModeEnabled || _tileMetricCurrentBandHeight <= 0)
			return false;

		TileMetricPersistentPrior aggregate = default;
		bool exactMatch = false;
		double totalWeight = 0.0;
		for (int deltaBand = -TileMetricsPersistentPriorNeighborBandRadius; deltaBand <= TileMetricsPersistentPriorNeighborBandRadius; deltaBand++)
		{
			int priorY = _tileMetricCurrentBandY + (deltaBand * _tileMetricCurrentBandHeight);
			if (priorY < 0 || priorY >= _filmHeight)
				continue;
			string spatialKey = BuildTileMetricsSpatialStableId(subtileX, priorY, subtileWidth, _tileMetricCurrentBandHeight);
			if (!_tileMetricPersistentPriors.TryGetValue(spatialKey, out TileMetricPersistentPrior prior) || prior.RaysTraced <= 0.0)
				continue;
			double weight = deltaBand == 0 ? 1.0 : (1.0 / (1.0 + Math.Abs(deltaBand)));
			aggregate.AddWeighted(prior, weight);
			totalWeight += weight;
			if (deltaBand == 0)
				exactMatch = true;
		}

		if (totalWeight <= 0.0 || aggregate.RaysTraced <= 0.0)
			return false;

		priorCandidate = BuildTilePriorityCandidateFromPrior(subtileIndex, subtileX, subtileWidth, aggregate);
		priorBlendWeight = ResolveTilePriorityPriorBlendWeight(
			hasCurrent: false,
			current: default,
			prior: priorCandidate,
			usedNeighborSeed: !exactMatch);
		return priorBlendWeight > 0.0 || priorCandidate.Rays > 0;
	}

	private static int CompareTilePriorityCandidates(in TilePriorityRankInputs a, in TilePriorityRankInputs b)
	{
		double aCurrentWeight = a.HasCurrent ? 1.0 : 0.0;
		double bCurrentWeight = b.HasCurrent ? 1.0 : 0.0;
		double aPriorWeight = a.HasPrior ? a.PriorBlendWeight : 0.0;
		double bPriorWeight = b.HasPrior ? b.PriorBlendWeight : 0.0;
		double aYield = (aCurrentWeight * a.Current.HitYield) + (aPriorWeight * a.Prior.HitYield);
		double bYield = (bCurrentWeight * b.Current.HitYield) + (bPriorWeight * b.Prior.HitYield);
		int cmp = bYield.CompareTo(aYield);
		if (cmp != 0) return cmp;

		double aHits = (aCurrentWeight * a.Current.Hits) + (aPriorWeight * a.Prior.Hits);
		double bHits = (bCurrentWeight * b.Current.Hits) + (bPriorWeight * b.Prior.Hits);
		cmp = bHits.CompareTo(aHits);
		if (cmp != 0) return cmp;

		double aNoCand = a.HasCurrent
			? ((1.0 - aPriorWeight) * a.Current.NoCandidateRatio) + (aPriorWeight * a.Prior.NoCandidateRatio)
			: (a.HasPrior ? a.Prior.NoCandidateRatio : 1.0);
		double bNoCand = b.HasCurrent
			? ((1.0 - bPriorWeight) * b.Current.NoCandidateRatio) + (bPriorWeight * b.Prior.NoCandidateRatio)
			: (b.HasPrior ? b.Prior.NoCandidateRatio : 1.0);
		cmp = aNoCand.CompareTo(bNoCand);
		if (cmp != 0) return cmp;
		return a.SubtileIndex.CompareTo(b.SubtileIndex);
	}

	private bool TryBuildPersistentPriorCandidate(int subtileIndex, int subtileX, int subtileWidth, bool hasCurrent, in TilePriorityCandidate currentCandidate, out TilePriorityCandidate priorCandidate, out double priorBlendWeight)
	{
		priorCandidate = default;
		priorBlendWeight = 0.0;
		if (!ExperimentalPersistentSubtileSchedulerModeEnabled)
			return false;

		bool usedNeighborSeed = false;
		string spatialKey = BuildTileMetricsSpatialStableId(subtileX, _tileMetricCurrentBandY, subtileWidth, _tileMetricCurrentBandHeight);
		if (_tileMetricPersistentPriors.TryGetValue(spatialKey, out TileMetricPersistentPrior prior) && prior.RaysTraced > 0.0)
		{
			priorCandidate = BuildTilePriorityCandidateFromPrior(subtileIndex, subtileX, subtileWidth, prior);
		}
		else if (TryBuildNearbyPersistentPrior(subtileIndex, subtileX, subtileWidth, out priorCandidate, out priorBlendWeight))
		{
			usedNeighborSeed = true;
		}
		else
		{
			return false;
		}

		if (!usedNeighborSeed)
			priorBlendWeight = ResolveTilePriorityPriorBlendWeight(hasCurrent, currentCandidate, priorCandidate, usedNeighborSeed: false);
		else if (hasCurrent)
			priorBlendWeight = ResolveTilePriorityPriorBlendWeight(hasCurrent, currentCandidate, priorCandidate, usedNeighborSeed: true);
		return priorBlendWeight > 0.0 || priorCandidate.Rays > 0;
	}

	private void PrepareExperimentalSubtileSchedulerOrderForCurrentBand()
	{
		EnsureTileMetricExecutionOrderCapacity(_tileMetricCurrentSubtileCount);
		_tileMetricCurrentExecutionSource = "baseline";
		_tileMetricCurrentExecutionPriorWeight = 0.0;

		if (!ExperimentalSubtileSchedulerModeEnabled || _tileMetricCurrentSubtileCount <= 1)
			return;
		long historyKey = BuildTileMetricBandHistoryKey(_tileMetricCurrentBandY, _tileMetricCurrentBandHeight);
		bool hasHistory = _tileMetricBandHistory.TryGetValue(historyKey, out TileMetricAccumulator[] history)
			&& history != null
			&& history.Length >= _tileMetricCurrentSubtileCount;

		var prioritized = new System.Collections.Generic.List<TilePriorityRankInputs>(_tileMetricCurrentSubtileCount);
		for (int subtileIndex = 0; subtileIndex < _tileMetricCurrentSubtileCount; subtileIndex++)
		{
			int subtileX = subtileIndex * _tileMetricCurrentSubtileWidth;
			int subtileWidth = Math.Max(0, Math.Min(_tileMetricCurrentSubtileWidth, _filmWidth - subtileX));
			if (subtileWidth <= 0)
				continue;

			TilePriorityCandidate currentCandidate = default;
			bool hasCurrent = false;
			if (hasHistory)
			{
				currentCandidate = new TilePriorityCandidate(subtileIndex, subtileX, subtileWidth, history[subtileIndex]);
				hasCurrent = true;
			}

			TilePriorityCandidate priorCandidate = default;
			double priorBlendWeight = 0.0;
			bool hasPrior = TryBuildPersistentPriorCandidate(subtileIndex, subtileX, subtileWidth, hasCurrent, currentCandidate, out priorCandidate, out priorBlendWeight);
			if (!hasCurrent && !hasPrior)
				continue;

			prioritized.Add(new TilePriorityRankInputs(
				subtileIndex,
				subtileX,
				subtileWidth,
				currentCandidate,
				hasCurrent,
				priorCandidate,
				hasPrior,
				priorBlendWeight));
		}

		if (prioritized.Count == 0)
			return;

		prioritized.Sort((a, b) => CompareTilePriorityCandidates(a, b));

		for (int i = 0; i < prioritized.Count; i++)
			_tileMetricCurrentExecutionOrder[i] = prioritized[i].SubtileIndex;

		bool hasAnyCurrent = false;
		bool hasAnyPrior = false;
		bool priorContributed = false;
		for (int i = 0; i < prioritized.Count; i++)
		{
			if (prioritized[i].HasCurrent)
				hasAnyCurrent = true;
			if (prioritized[i].HasPrior)
			{
				hasAnyPrior = true;
				if (prioritized[i].PriorBlendWeight > 0.0)
					priorContributed = true;
			}
		}

		if (hasAnyCurrent && priorContributed)
			_tileMetricCurrentExecutionSource = "history_plus_prior";
		else if (hasAnyCurrent)
			_tileMetricCurrentExecutionSource = "history";
		else if (hasAnyPrior)
			_tileMetricCurrentExecutionSource = "persistent_prior";
		if (prioritized.Count > 0)
			_tileMetricCurrentExecutionPriorWeight = Math.Clamp(prioritized[0].PriorBlendWeight, 0.0, 1.0);
	}

	private void SaveTileMetricBandHistoryForCurrentBand()
	{
		if (!EnableTileMetricsScaffold || _tileMetricCurrentSubtileCount <= 0)
			return;

		var snapshot = new TileMetricAccumulator[_tileMetricCurrentSubtileCount];
		Array.Copy(_tileMetricCurrentSubtiles, snapshot, _tileMetricCurrentSubtileCount);
		long historyKey = BuildTileMetricBandHistoryKey(_tileMetricCurrentBandY, _tileMetricCurrentBandHeight);
		_tileMetricBandHistory[historyKey] = snapshot;

		if (!ExperimentalPersistentSubtileSchedulerModeEnabled)
			return;

		for (int subtileIndex = 0; subtileIndex < _tileMetricCurrentSubtileCount; subtileIndex++)
		{
			int subtileX = subtileIndex * _tileMetricCurrentSubtileWidth;
			int subtileWidth = Math.Max(0, Math.Min(_tileMetricCurrentSubtileWidth, _filmWidth - subtileX));
			if (subtileWidth <= 0)
				continue;

			string spatialKey = BuildTileMetricsSpatialStableId(subtileX, _tileMetricCurrentBandY, subtileWidth, _tileMetricCurrentBandHeight);
			_tileMetricPersistentPriors.TryGetValue(spatialKey, out TileMetricPersistentPrior prior);
			prior.DecayInPlace(TileMetricsPersistentPriorDecay);
			prior.Add(snapshot[subtileIndex]);
			_tileMetricPersistentPriors[spatialKey] = prior;
		}
	}

	private static string BuildTilePriorityOrderText(System.Collections.Generic.IReadOnlyList<TilePriorityCandidate> items)
	{
		if (items == null || items.Count == 0)
			return "none";
		var sb = new StringBuilder(128);
		for (int i = 0; i < items.Count; i++)
		{
			if (i > 0) sb.Append(',');
			TilePriorityCandidate item = items[i];
			sb.Append(item.SubtileIndex)
				.Append('@')
				.Append(item.X)
				.Append(':')
				.Append(item.HitYield.ToString("0.###", CultureInfo.InvariantCulture))
				.Append('/')
				.Append(item.Hits)
				.Append('/')
				.Append(item.NoCandidateRatio.ToString("0.###", CultureInfo.InvariantCulture));
		}
		return sb.ToString();
	}

	private static int ComputeTileHitCaptureOrdinal(System.Collections.Generic.IReadOnlyList<TilePriorityCandidate> items, long totalHits, double thresholdShare)
	{
		if (items == null || items.Count == 0 || totalHits <= 0)
			return 0;

		long cumulativeHits = 0;
		double thresholdHits = totalHits * thresholdShare;
		for (int i = 0; i < items.Count; i++)
		{
			cumulativeHits += items[i].Hits;
			if (cumulativeHits > 0 && cumulativeHits >= thresholdHits)
				return i + 1;
		}

		return items.Count;
	}

	private void LogExperimentalSubtileSchedulerForCurrentBand(int stepIndex, string exitReason)
	{
		if (!ExperimentalSubtileSchedulerModeEnabled || _tileMetricCurrentSubtileCount <= 0)
			return;

		var legacy = new System.Collections.Generic.List<TilePriorityCandidate>(_tileMetricCurrentSubtileCount);
		var executed = new System.Collections.Generic.List<TilePriorityCandidate>(_tileMetricCurrentSubtileCount);
		TilePriorityCandidate[] bySubtileIndex = new TilePriorityCandidate[_tileMetricCurrentSubtileCount];
		long totalBandHits = 0;
		long totalBandSegmentsTraced = 0;

		for (int subtileIndex = 0; subtileIndex < _tileMetricCurrentSubtileCount; subtileIndex++)
		{
			int subtileX = subtileIndex * _tileMetricCurrentSubtileWidth;
			int subtileWidth = Math.Max(0, Math.Min(_tileMetricCurrentSubtileWidth, _filmWidth - subtileX));
			if (subtileWidth <= 0)
				continue;
			TilePriorityCandidate candidate = new TilePriorityCandidate(subtileIndex, subtileX, subtileWidth, _tileMetricCurrentSubtiles[subtileIndex]);
			legacy.Add(candidate);
			bySubtileIndex[subtileIndex] = candidate;
			totalBandHits += candidate.Hits;
			totalBandSegmentsTraced += Math.Max(0L, _tileMetricCurrentSubtiles[subtileIndex].CandidateSegments);
		}

		if (legacy.Count == 0 || totalBandHits <= 0)
			return;

		for (int execIndex = 0; execIndex < legacy.Count; execIndex++)
		{
			int subtileIndex = _tileMetricCurrentExecutionOrder[execIndex];
			if ((uint)subtileIndex >= (uint)bySubtileIndex.Length)
				continue;
			executed.Add(bySubtileIndex[subtileIndex]);
		}

		long legacyTop1 = legacy.Count > 0 ? legacy[0].Hits : 0;
		long legacyTop2 = legacyTop1 + (legacy.Count > 1 ? legacy[1].Hits : 0);
		long legacyTop3 = legacyTop2 + (legacy.Count > 2 ? legacy[2].Hits : 0);
		long legacyPrimaryTop1 = legacy.Count > 0 ? legacy[0].SourceHits : 0;
		long legacyBackdropTop1 = legacy.Count > 0 ? legacy[0].BackgroundHits : 0;
		long legacyCombinedTop1 = legacyPrimaryTop1 + legacyBackdropTop1;
		long execTop1 = executed.Count > 0 ? executed[0].Hits : 0;
		long execTop2 = execTop1 + (executed.Count > 1 ? executed[1].Hits : 0);
		long execTop3 = execTop2 + (executed.Count > 2 ? executed[2].Hits : 0);
		long execPrimaryTop1 = executed.Count > 0 ? executed[0].SourceHits : 0;
		long execBackdropTop1 = executed.Count > 0 ? executed[0].BackgroundHits : 0;
		long execCombinedTop1 = execPrimaryTop1 + execBackdropTop1;
		int firstHitOrdinal = ComputeTileHitCaptureOrdinal(executed, totalBandHits, 1.0 / Math.Max(1L, totalBandHits));
		int hit50Ordinal = ComputeTileHitCaptureOrdinal(executed, totalBandHits, 0.5);

		_tileMetricExecBandsWithHitsThisFrame++;
		_tileMetricExecTotalHitsThisFrame += totalBandHits;
		_tileMetricExecSegmentsTracedThisFrame += totalBandSegmentsTraced;
		_tileMetricExecLegacyHitsTop1ThisFrame += legacyTop1;
		_tileMetricExecLegacyHitsTop2ThisFrame += legacyTop2;
		_tileMetricExecLegacyHitsTop3ThisFrame += legacyTop3;
		_tileMetricExecOrderedHitsTop1ThisFrame += execTop1;
		_tileMetricExecOrderedHitsTop2ThisFrame += execTop2;
		_tileMetricExecOrderedHitsTop3ThisFrame += execTop3;
		_tileMetricExecLegacyPrimaryHitsTop1ThisFrame += legacyPrimaryTop1;
		_tileMetricExecLegacyBackdropHitsTop1ThisFrame += legacyBackdropTop1;
		_tileMetricExecLegacyCombinedHitsTop1ThisFrame += legacyCombinedTop1;
		_tileMetricExecOrderedPrimaryHitsTop1ThisFrame += execPrimaryTop1;
		_tileMetricExecOrderedBackdropHitsTop1ThisFrame += execBackdropTop1;
		_tileMetricExecOrderedCombinedHitsTop1ThisFrame += execCombinedTop1;
		bool rankedBand = !string.Equals(_tileMetricCurrentExecutionSource, "baseline", StringComparison.Ordinal);
		bool priorOnlyBand = string.Equals(_tileMetricCurrentExecutionSource, "persistent_prior", StringComparison.Ordinal);
		bool priorContribBand = string.Equals(_tileMetricCurrentExecutionSource, "history_plus_prior", StringComparison.Ordinal)
			|| priorOnlyBand;
		bool currentDominantBand = string.Equals(_tileMetricCurrentExecutionSource, "history", StringComparison.Ordinal)
			|| string.Equals(_tileMetricCurrentExecutionSource, "history_plus_prior", StringComparison.Ordinal);
		if (rankedBand)
			_tileMetricExecRankedBandsWithHitsThisFrame++;
		else
			_tileMetricExecSeedBandsWithHitsThisFrame++;
		if (rankedBand)
		{
			_tileMetricExecRankedHitsThisFrame += totalBandHits;
			_tileMetricExecRankedSegmentsTracedThisFrame += totalBandSegmentsTraced;
		}
		else
		{
			_tileMetricExecSeedHitsThisFrame += totalBandHits;
			_tileMetricExecSeedSegmentsTracedThisFrame += totalBandSegmentsTraced;
		}
		if (priorContribBand)
			_tileMetricExecPriorBandsWithHitsThisFrame++;
		if (priorOnlyBand)
		{
			_tileMetricExecPriorOnlyBandsWithHitsThisFrame++;
			_tileMetricExecPriorOnlyHitsThisFrame += totalBandHits;
			_tileMetricExecPriorOnlySegmentsTracedThisFrame += totalBandSegmentsTraced;
		}
		if (currentDominantBand)
			_tileMetricExecCurrentDominantBandsWithHitsThisFrame++;
		if (string.Equals(_tileMetricCurrentExecutionSource, "history_plus_prior", StringComparison.Ordinal))
			_tileMetricExecPriorContribBandsWithHitsThisFrame++;
		if (legacy.Count > 0 && executed.Count > 0 && legacy[0].SubtileIndex != executed[0].SubtileIndex)
			_tileMetricExecTop1OrderChangedBandsWithHitsThisFrame++;
		if (execTop1 > legacyTop1)
			_tileMetricExecTop1HitImprovedBandsWithHitsThisFrame++;
		if (firstHitOrdinal > 0)
		{
			_tileMetricExecFirstHitOrdinalSumThisFrame += firstHitOrdinal;
			_tileMetricExecFirstHitOrdinalCountThisFrame++;
		}
		if (hit50Ordinal > 0)
		{
			_tileMetricExecHit50OrdinalSumThisFrame += hit50Ordinal;
			_tileMetricExecHit50OrdinalCountThisFrame++;
		}

		string legacyOrder = BuildTilePriorityOrderText(legacy);
		string executionOrder = BuildTilePriorityOrderText(executed);
		string legacyTop1Share = ((double)legacyTop1 / totalBandHits).ToString("0.###", CultureInfo.InvariantCulture);
		string legacyTop2Share = ((double)legacyTop2 / totalBandHits).ToString("0.###", CultureInfo.InvariantCulture);
		string execTop1Share = ((double)execTop1 / totalBandHits).ToString("0.###", CultureInfo.InvariantCulture);
		string execTop2Share = ((double)execTop2 / totalBandHits).ToString("0.###", CultureInfo.InvariantCulture);
		string schedulerPhase = priorOnlyBand
			? "cold_start_prior_ranked"
			: (rankedBand ? "warm_start_ranked" : "cold_start_seed");
		double currentEvidenceWeight = string.Equals(_tileMetricCurrentExecutionSource, "history_plus_prior", StringComparison.Ordinal)
			? Math.Max(0.0, 1.0 - _tileMetricCurrentExecutionPriorWeight)
			: (currentDominantBand ? 1.0 : 0.0);
		double priorEvidenceWeight = string.Equals(_tileMetricCurrentExecutionSource, "history_plus_prior", StringComparison.Ordinal)
			? Math.Clamp(_tileMetricCurrentExecutionPriorWeight, 0.0, 1.0)
			: (priorOnlyBand ? 1.0 : 0.0);
		double totalEvidenceWeight = currentEvidenceWeight + priorEvidenceWeight;
		string currentEvidenceShare = totalEvidenceWeight > 0.0
			? (currentEvidenceWeight / totalEvidenceWeight).ToString("0.###", CultureInfo.InvariantCulture)
			: "0";
		string priorEvidenceShare = totalEvidenceWeight > 0.0
			? (priorEvidenceWeight / totalEvidenceWeight).ToString("0.###", CultureInfo.InvariantCulture)
			: "0";
		string hitsPerSegmentTraced = totalBandSegmentsTraced > 0
			? ((double)totalBandHits / totalBandSegmentsTraced).ToString("0.###", CultureInfo.InvariantCulture)
			: "na";
		GD.Print(
			$"[TileMetrics][ExecOrder] step={stepIndex} band={_tileMetricCurrentBandIndex} y={_tileMetricCurrentBandY} h={_tileMetricCurrentBandHeight} " +
			$"mode={(ExperimentalPersistentSubtileSchedulerModeEnabled ? "experimental_reorder_only_persistent_priors" : "experimental_reorder_only")} phase={schedulerPhase} rankActive={(rankedBand ? 1 : 0)} " +
			$"legacy={legacyOrder} executed={executionOrder} totalHits={totalBandHits} " +
			$"segmentsTraced={totalBandSegmentsTraced} hitsPerSegmentTraced={hitsPerSegmentTraced} " +
			$"legacyTop1Share={legacyTop1Share} legacyTop2Share={legacyTop2Share} " +
			$"execTop1Share={execTop1Share} execTop2Share={execTop2Share} " +
			$"legacyPrimaryTop1Share={FormatTileClassShare(legacyPrimaryTop1, totalBandHits)} legacyBackdropTop1Share={FormatTileClassShare(legacyBackdropTop1, totalBandHits)} legacyCombinedTop1Share={FormatTileClassShare(legacyCombinedTop1, totalBandHits)} " +
			$"execPrimaryTop1Share={FormatTileClassShare(execPrimaryTop1, totalBandHits)} execBackdropTop1Share={FormatTileClassShare(execBackdropTop1, totalBandHits)} execCombinedTop1Share={FormatTileClassShare(execCombinedTop1, totalBandHits)} " +
			$"currentEvidenceShare={currentEvidenceShare} priorEvidenceShare={priorEvidenceShare} priorContributed={(priorContribBand ? 1 : 0)} coldStartReduced={(priorOnlyBand ? 1 : 0)} " +
			$"firstHitOrdinal={firstHitOrdinal} hit50Ordinal={hit50Ordinal} source={_tileMetricCurrentExecutionSource} " +
			$"exit={(string.IsNullOrEmpty(exitReason) ? "none" : exitReason)}");
	}

	private void LogExperimentalSubtileSchedulerFrameSummaryIfNeeded()
	{
		if (!ExperimentalSubtileSchedulerModeEnabled)
			return;
		if (_tileMetricExecBandsWithHitsThisFrame <= 0 || _tileMetricExecTotalHitsThisFrame <= 0)
			return;

		double invHits = 1.0 / _tileMetricExecTotalHitsThisFrame;
		string avgFirstHitOrdinal = _tileMetricExecFirstHitOrdinalCountThisFrame > 0
			? ((double)_tileMetricExecFirstHitOrdinalSumThisFrame / _tileMetricExecFirstHitOrdinalCountThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "na";
		string avgHit50Ordinal = _tileMetricExecHit50OrdinalCountThisFrame > 0
			? ((double)_tileMetricExecHit50OrdinalSumThisFrame / _tileMetricExecHit50OrdinalCountThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "na";
		bool rankedFrame = _tileMetricExecRankedBandsWithHitsThisFrame > 0;
		bool priorOnlyFrame = _tileMetricExecPriorOnlyBandsWithHitsThisFrame > 0 && _tileMetricExecCurrentDominantBandsWithHitsThisFrame == 0;
		string framePhase = priorOnlyFrame
			? "cold_start_prior_ranked"
			: (rankedFrame
				? (_tileMetricExecSeedBandsWithHitsThisFrame > 0 ? "mixed_warm_start" : "warm_start_ranked")
				: "cold_start_seed_only");
		string hitsPerSegmentTraced = _tileMetricExecSegmentsTracedThisFrame > 0
			? ((double)_tileMetricExecTotalHitsThisFrame / _tileMetricExecSegmentsTracedThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "na";
		string seedHitShare = _tileMetricExecTotalHitsThisFrame > 0
			? ((double)_tileMetricExecSeedHitsThisFrame / _tileMetricExecTotalHitsThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "0";
		string rankedHitShare = _tileMetricExecTotalHitsThisFrame > 0
			? ((double)_tileMetricExecRankedHitsThisFrame / _tileMetricExecTotalHitsThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "0";
		string priorOnlyHitShare = _tileMetricExecTotalHitsThisFrame > 0
			? ((double)_tileMetricExecPriorOnlyHitsThisFrame / _tileMetricExecTotalHitsThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "0";
		string seedSegmentShare = _tileMetricExecSegmentsTracedThisFrame > 0
			? ((double)_tileMetricExecSeedSegmentsTracedThisFrame / _tileMetricExecSegmentsTracedThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "0";
		string rankedSegmentShare = _tileMetricExecSegmentsTracedThisFrame > 0
			? ((double)_tileMetricExecRankedSegmentsTracedThisFrame / _tileMetricExecSegmentsTracedThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "0";
		string priorOnlySegmentShare = _tileMetricExecSegmentsTracedThisFrame > 0
			? ((double)_tileMetricExecPriorOnlySegmentsTracedThisFrame / _tileMetricExecSegmentsTracedThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "0";
		GD.Print(
			$"[TileMetrics][ExecSummary] mode={(ExperimentalPersistentSubtileSchedulerModeEnabled ? "experimental_reorder_only_persistent_priors" : "experimental_reorder_only")} framePhase={framePhase} rankActive={(rankedFrame ? 1 : 0)} " +
			$"bandsWithHits={_tileMetricExecBandsWithHitsThisFrame} seedBandsWithHits={_tileMetricExecSeedBandsWithHitsThisFrame} rankedBandsWithHits={_tileMetricExecRankedBandsWithHitsThisFrame} priorBandsWithHits={_tileMetricExecPriorBandsWithHitsThisFrame} priorOnlyBandsWithHits={_tileMetricExecPriorOnlyBandsWithHitsThisFrame} priorContribBandsWithHits={_tileMetricExecPriorContribBandsWithHitsThisFrame} totalHits={_tileMetricExecTotalHitsThisFrame} " +
			$"segmentsTraced={_tileMetricExecSegmentsTracedThisFrame} hitsPerSegmentTraced={hitsPerSegmentTraced} " +
			$"seedHitShare={seedHitShare} rankedHitShare={rankedHitShare} priorOnlyHitShare={priorOnlyHitShare} " +
			$"seedSegmentShare={seedSegmentShare} rankedSegmentShare={rankedSegmentShare} priorOnlySegmentShare={priorOnlySegmentShare} " +
			$"top1OrderChangedBandsWithHits={_tileMetricExecTop1OrderChangedBandsWithHitsThisFrame} top1HitImprovedBandsWithHits={_tileMetricExecTop1HitImprovedBandsWithHitsThisFrame} " +
			$"legacyTop1Share={_tileMetricExecLegacyHitsTop1ThisFrame * invHits:0.###} legacyTop2Share={_tileMetricExecLegacyHitsTop2ThisFrame * invHits:0.###} legacyTop3Share={_tileMetricExecLegacyHitsTop3ThisFrame * invHits:0.###} " +
			$"execTop1Share={_tileMetricExecOrderedHitsTop1ThisFrame * invHits:0.###} execTop2Share={_tileMetricExecOrderedHitsTop2ThisFrame * invHits:0.###} execTop3Share={_tileMetricExecOrderedHitsTop3ThisFrame * invHits:0.###} " +
			$"legacyPrimaryTop1Share={_tileMetricExecLegacyPrimaryHitsTop1ThisFrame * invHits:0.###} legacyBackdropTop1Share={_tileMetricExecLegacyBackdropHitsTop1ThisFrame * invHits:0.###} legacyCombinedTop1Share={_tileMetricExecLegacyCombinedHitsTop1ThisFrame * invHits:0.###} " +
			$"execPrimaryTop1Share={_tileMetricExecOrderedPrimaryHitsTop1ThisFrame * invHits:0.###} execBackdropTop1Share={_tileMetricExecOrderedBackdropHitsTop1ThisFrame * invHits:0.###} execCombinedTop1Share={_tileMetricExecOrderedCombinedHitsTop1ThisFrame * invHits:0.###} " +
			$"avgFirstHitOrdinal={avgFirstHitOrdinal} avgHit50Ordinal={avgHit50Ordinal}");

		_tileMetricExecBandsWithHitsThisFrame = 0;
		_tileMetricExecTotalHitsThisFrame = 0;
		_tileMetricExecSegmentsTracedThisFrame = 0;
		_tileMetricExecLegacyHitsTop1ThisFrame = 0;
		_tileMetricExecLegacyHitsTop2ThisFrame = 0;
		_tileMetricExecLegacyHitsTop3ThisFrame = 0;
		_tileMetricExecOrderedHitsTop1ThisFrame = 0;
		_tileMetricExecOrderedHitsTop2ThisFrame = 0;
		_tileMetricExecOrderedHitsTop3ThisFrame = 0;
		_tileMetricExecLegacyPrimaryHitsTop1ThisFrame = 0;
		_tileMetricExecLegacyBackdropHitsTop1ThisFrame = 0;
		_tileMetricExecLegacyCombinedHitsTop1ThisFrame = 0;
		_tileMetricExecOrderedPrimaryHitsTop1ThisFrame = 0;
		_tileMetricExecOrderedBackdropHitsTop1ThisFrame = 0;
		_tileMetricExecOrderedCombinedHitsTop1ThisFrame = 0;
		_tileMetricExecFirstHitOrdinalSumThisFrame = 0;
		_tileMetricExecHit50OrdinalSumThisFrame = 0;
		_tileMetricExecFirstHitOrdinalCountThisFrame = 0;
		_tileMetricExecHit50OrdinalCountThisFrame = 0;
		_tileMetricExecSeedBandsWithHitsThisFrame = 0;
		_tileMetricExecRankedBandsWithHitsThisFrame = 0;
		_tileMetricExecPriorBandsWithHitsThisFrame = 0;
		_tileMetricExecPriorOnlyBandsWithHitsThisFrame = 0;
		_tileMetricExecCurrentDominantBandsWithHitsThisFrame = 0;
		_tileMetricExecPriorContribBandsWithHitsThisFrame = 0;
		_tileMetricExecSeedHitsThisFrame = 0;
		_tileMetricExecRankedHitsThisFrame = 0;
		_tileMetricExecPriorOnlyHitsThisFrame = 0;
		_tileMetricExecSeedSegmentsTracedThisFrame = 0;
		_tileMetricExecRankedSegmentsTracedThisFrame = 0;
		_tileMetricExecPriorOnlySegmentsTracedThisFrame = 0;
		_tileMetricExecTop1OrderChangedBandsWithHitsThisFrame = 0;
		_tileMetricExecTop1HitImprovedBandsWithHitsThisFrame = 0;
	}

	private void SimulateTilePriorityOrderForCurrentBand(int stepIndex, string exitReason)
	{
		if (!EnableTileMetricsScaffold || !EnableTileMetricsReorderSimulation || _tileMetricCurrentSubtileCount <= 0)
			return;

		var actual = new System.Collections.Generic.List<TilePriorityCandidate>(_tileMetricCurrentSubtileCount);
		var simulated = new System.Collections.Generic.List<TilePriorityCandidate>(_tileMetricCurrentSubtileCount);
		long totalBandHits = 0;
		long totalBandSegmentsTraced = 0;
		for (int subtileIndex = 0; subtileIndex < _tileMetricCurrentSubtileCount; subtileIndex++)
		{
			int subtileX = subtileIndex * _tileMetricCurrentSubtileWidth;
			int subtileWidth = Math.Max(0, Math.Min(_tileMetricCurrentSubtileWidth, _filmWidth - subtileX));
			if (subtileWidth <= 0)
				continue;
			TilePriorityCandidate candidate = new TilePriorityCandidate(subtileIndex, subtileX, subtileWidth, _tileMetricCurrentSubtiles[subtileIndex]);
			actual.Add(candidate);
			simulated.Add(candidate);
			totalBandHits += candidate.Hits;
			totalBandSegmentsTraced += Math.Max(0L, _tileMetricCurrentSubtiles[subtileIndex].CandidateSegments);
		}

		if (actual.Count == 0)
			return;

		// Observe-only priority contract for the first scheduler experiment:
		// prefer higher yield, then more hits, then fewer no-candidate pixels.
		simulated.Sort((a, b) =>
		{
			int cmp = b.HitYield.CompareTo(a.HitYield);
			if (cmp != 0) return cmp;
			cmp = b.Hits.CompareTo(a.Hits);
			if (cmp != 0) return cmp;
			cmp = a.NoCandidateRatio.CompareTo(b.NoCandidateRatio);
			if (cmp != 0) return cmp;
			return a.SubtileIndex.CompareTo(b.SubtileIndex);
		});

		long actualTop1 = actual.Count > 0 ? actual[0].Hits : 0;
		long actualTop2 = actualTop1 + (actual.Count > 1 ? actual[1].Hits : 0);
		long actualTop3 = actualTop2 + (actual.Count > 2 ? actual[2].Hits : 0);
		long actualPrimaryTop1 = actual.Count > 0 ? actual[0].SourceHits : 0;
		long actualBackdropTop1 = actual.Count > 0 ? actual[0].BackgroundHits : 0;
		long actualCombinedTop1 = actualPrimaryTop1 + actualBackdropTop1;
		long simTop1 = simulated.Count > 0 ? simulated[0].Hits : 0;
		long simTop2 = simTop1 + (simulated.Count > 1 ? simulated[1].Hits : 0);
		long simTop3 = simTop2 + (simulated.Count > 2 ? simulated[2].Hits : 0);
		long simPrimaryTop1 = simulated.Count > 0 ? simulated[0].SourceHits : 0;
		long simBackdropTop1 = simulated.Count > 0 ? simulated[0].BackgroundHits : 0;
		long simCombinedTop1 = simPrimaryTop1 + simBackdropTop1;
		int actualFirstHitOrdinal = ComputeTileHitCaptureOrdinal(actual, totalBandHits, 1.0 / Math.Max(1L, totalBandHits));
		int actualHit50Ordinal = ComputeTileHitCaptureOrdinal(actual, totalBandHits, 0.5);

		if (totalBandHits > 0)
		{
			_tileMetricSimBandsWithHitsThisFrame++;
			_tileMetricSimTotalHitsThisFrame += totalBandHits;
			_tileMetricSimSegmentsTracedThisFrame += totalBandSegmentsTraced;
			_tileMetricActualHitsTop1ThisFrame += actualTop1;
			_tileMetricActualHitsTop2ThisFrame += actualTop2;
			_tileMetricActualHitsTop3ThisFrame += actualTop3;
			_tileMetricSimHitsTop1ThisFrame += simTop1;
			_tileMetricSimHitsTop2ThisFrame += simTop2;
			_tileMetricSimHitsTop3ThisFrame += simTop3;
			_tileMetricActualPrimaryHitsTop1ThisFrame += actualPrimaryTop1;
			_tileMetricActualBackdropHitsTop1ThisFrame += actualBackdropTop1;
			_tileMetricActualCombinedHitsTop1ThisFrame += actualCombinedTop1;
			_tileMetricSimPrimaryHitsTop1ThisFrame += simPrimaryTop1;
			_tileMetricSimBackdropHitsTop1ThisFrame += simBackdropTop1;
			_tileMetricSimCombinedHitsTop1ThisFrame += simCombinedTop1;
			if (actualFirstHitOrdinal > 0)
			{
				_tileMetricActualFirstHitOrdinalSumThisFrame += actualFirstHitOrdinal;
				_tileMetricActualFirstHitOrdinalCountThisFrame++;
			}
			if (actualHit50Ordinal > 0)
			{
				_tileMetricActualHit50OrdinalSumThisFrame += actualHit50Ordinal;
				_tileMetricActualHit50OrdinalCountThisFrame++;
			}
		}

		string actualOrder = BuildTilePriorityOrderText(actual);
		string simulatedOrder = BuildTilePriorityOrderText(simulated);
		string actualTop1Share = totalBandHits > 0 ? ((double)actualTop1 / totalBandHits).ToString("0.###", CultureInfo.InvariantCulture) : "na";
		string actualTop2Share = totalBandHits > 0 ? ((double)actualTop2 / totalBandHits).ToString("0.###", CultureInfo.InvariantCulture) : "na";
		string simulatedTop1Share = totalBandHits > 0 ? ((double)simTop1 / totalBandHits).ToString("0.###", CultureInfo.InvariantCulture) : "na";
		string simulatedTop2Share = totalBandHits > 0 ? ((double)simTop2 / totalBandHits).ToString("0.###", CultureInfo.InvariantCulture) : "na";
		string hitsPerSegmentTraced = totalBandSegmentsTraced > 0
			? ((double)totalBandHits / totalBandSegmentsTraced).ToString("0.###", CultureInfo.InvariantCulture)
			: "na";
		GD.Print(
			$"[TileMetrics][SimOrder] step={stepIndex} band={_tileMetricCurrentBandIndex} y={_tileMetricCurrentBandY} h={_tileMetricCurrentBandHeight} " +
			$"actual={actualOrder} simulated={simulatedOrder} totalHits={totalBandHits} " +
			$"segmentsTraced={totalBandSegmentsTraced} hitsPerSegmentTraced={hitsPerSegmentTraced} " +
			$"actualTop1Share={actualTop1Share} actualTop2Share={actualTop2Share} " +
			$"simTop1Share={simulatedTop1Share} simTop2Share={simulatedTop2Share} " +
			$"actualPrimaryTop1Share={FormatTileClassShare(actualPrimaryTop1, totalBandHits)} actualBackdropTop1Share={FormatTileClassShare(actualBackdropTop1, totalBandHits)} actualCombinedTop1Share={FormatTileClassShare(actualCombinedTop1, totalBandHits)} " +
			$"simPrimaryTop1Share={FormatTileClassShare(simPrimaryTop1, totalBandHits)} simBackdropTop1Share={FormatTileClassShare(simBackdropTop1, totalBandHits)} simCombinedTop1Share={FormatTileClassShare(simCombinedTop1, totalBandHits)} " +
			$"actualFirstHitOrdinal={actualFirstHitOrdinal} actualHit50Ordinal={actualHit50Ordinal} " +
			$"exit={(string.IsNullOrEmpty(exitReason) ? "none" : exitReason)}");
	}

	private void LogTilePrioritySimulationFrameSummaryIfNeeded()
	{
		if (!EnableTileMetricsScaffold || !EnableTileMetricsReorderSimulation)
			return;
		if (_tileMetricSimBandsWithHitsThisFrame <= 0 || _tileMetricSimTotalHitsThisFrame <= 0)
			return;

		double invHits = 1.0 / _tileMetricSimTotalHitsThisFrame;
		string hitsPerSegmentTraced = _tileMetricSimSegmentsTracedThisFrame > 0
			? ((double)_tileMetricSimTotalHitsThisFrame / _tileMetricSimSegmentsTracedThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "na";
		string avgFirstHitOrdinal = _tileMetricActualFirstHitOrdinalCountThisFrame > 0
			? ((double)_tileMetricActualFirstHitOrdinalSumThisFrame / _tileMetricActualFirstHitOrdinalCountThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "na";
		string avgHit50Ordinal = _tileMetricActualHit50OrdinalCountThisFrame > 0
			? ((double)_tileMetricActualHit50OrdinalSumThisFrame / _tileMetricActualHit50OrdinalCountThisFrame).ToString("0.###", CultureInfo.InvariantCulture)
			: "na";
		GD.Print(
			$"[TileMetrics][SimSummary] bandsWithHits={_tileMetricSimBandsWithHitsThisFrame} totalHits={_tileMetricSimTotalHitsThisFrame} segmentsTraced={_tileMetricSimSegmentsTracedThisFrame} hitsPerSegmentTraced={hitsPerSegmentTraced} " +
			$"actualTop1Share={_tileMetricActualHitsTop1ThisFrame * invHits:0.###} actualTop2Share={_tileMetricActualHitsTop2ThisFrame * invHits:0.###} actualTop3Share={_tileMetricActualHitsTop3ThisFrame * invHits:0.###} " +
			$"simTop1Share={_tileMetricSimHitsTop1ThisFrame * invHits:0.###} simTop2Share={_tileMetricSimHitsTop2ThisFrame * invHits:0.###} simTop3Share={_tileMetricSimHitsTop3ThisFrame * invHits:0.###} " +
			$"actualPrimaryTop1Share={_tileMetricActualPrimaryHitsTop1ThisFrame * invHits:0.###} actualBackdropTop1Share={_tileMetricActualBackdropHitsTop1ThisFrame * invHits:0.###} actualCombinedTop1Share={_tileMetricActualCombinedHitsTop1ThisFrame * invHits:0.###} " +
			$"simPrimaryTop1Share={_tileMetricSimPrimaryHitsTop1ThisFrame * invHits:0.###} simBackdropTop1Share={_tileMetricSimBackdropHitsTop1ThisFrame * invHits:0.###} simCombinedTop1Share={_tileMetricSimCombinedHitsTop1ThisFrame * invHits:0.###} " +
			$"actualAvgFirstHitOrdinal={avgFirstHitOrdinal} actualAvgHit50Ordinal={avgHit50Ordinal}");

		_tileMetricSimBandsWithHitsThisFrame = 0;
		_tileMetricSimTotalHitsThisFrame = 0;
		_tileMetricSimSegmentsTracedThisFrame = 0;
		_tileMetricSimHitsTop1ThisFrame = 0;
		_tileMetricSimHitsTop2ThisFrame = 0;
		_tileMetricSimHitsTop3ThisFrame = 0;
		_tileMetricActualHitsTop1ThisFrame = 0;
		_tileMetricActualHitsTop2ThisFrame = 0;
		_tileMetricActualHitsTop3ThisFrame = 0;
		_tileMetricSimPrimaryHitsTop1ThisFrame = 0;
		_tileMetricSimBackdropHitsTop1ThisFrame = 0;
		_tileMetricSimCombinedHitsTop1ThisFrame = 0;
		_tileMetricActualPrimaryHitsTop1ThisFrame = 0;
		_tileMetricActualBackdropHitsTop1ThisFrame = 0;
		_tileMetricActualCombinedHitsTop1ThisFrame = 0;
		_tileMetricActualFirstHitOrdinalSumThisFrame = 0;
		_tileMetricActualHit50OrdinalSumThisFrame = 0;
		_tileMetricActualFirstHitOrdinalCountThisFrame = 0;
		_tileMetricActualHit50OrdinalCountThisFrame = 0;
	}

	private void RecordTileMetricSample(
		in TileDescriptor descriptor,
		long raysTraced,
		long hits,
		long sourceHits,
		long backgroundHits,
		long unclassifiedHits,
		long candidateReferences,
		long candidateSegments,
		long noCandidatePixels,
		long candidatePixels,
		long geomPixelsProcessed,
		long geomRayTests,
		string exitReason)
	{
		if (!EnableTileMetricsScaffold)
			return;

		var sample = new TileMetricSample(
			in descriptor,
			raysTraced,
			hits,
			sourceHits,
			backgroundHits,
			unclassifiedHits,
			candidateReferences,
			candidateSegments,
			noCandidatePixels,
			candidatePixels,
			geomPixelsProcessed,
			geomRayTests,
			exitReason);

		_tileMetricSamples[_tileMetricWrite] = sample;
		_tileMetricWrite = (_tileMetricWrite + 1) % TileMetricsBufferSize;
		if (_tileMetricCount < TileMetricsBufferSize)
			_tileMetricCount++;

		if (_tileMetricsLogFrame != _frameIndex)
		{
			_tileMetricsLogFrame = _frameIndex;
			_tileMetricsLogsRemainingThisFrame = Math.Max(0, TileMetricsMaxLogsPerFrame);
		}

		if (_tileMetricsLogsRemainingThisFrame <= 0)
			return;
		_tileMetricsLogsRemainingThisFrame--;

		string raysPerHit = hits > 0 ? ((double)raysTraced / hits).ToString("0.###") : "na";
		string candRefsPerHit = hits > 0 ? ((double)candidateReferences / hits).ToString("0.###") : "na";
		string noCandidateRatio = geomPixelsProcessed > 0 ? ((double)noCandidatePixels / geomPixelsProcessed).ToString("0.###") : "na";
		string hitYield = raysTraced > 0 ? ((double)hits / raysTraced).ToString("0.###") : "0";
		GD.Print(
			$"[TileMetrics] step={descriptor.StepIndex} band={descriptor.BandIndex} subtile={descriptor.SubtileIndex} id={descriptor.StableId} " +
			$"x={descriptor.X} y={descriptor.Y} w={descriptor.Width} h={descriptor.Height} rays={raysTraced} hits={hits} sourceHits={sourceHits} backgroundHits={backgroundHits} unclassifiedHits={unclassifiedHits} candRefs={candidateReferences} candSegs={candidateSegments} " +
			$"candPx={candidatePixels} noCandPx={noCandidatePixels} geomPx={geomPixelsProcessed} geomRayTests={geomRayTests} " +
			$"raysPerHit={raysPerHit} candChecksPerHit={candRefsPerHit} noCandRatio={noCandidateRatio} hitYield={hitYield} " +
			$"exit={(string.IsNullOrEmpty(sample.ExitReason) ? "none" : sample.ExitReason)}");
	}

	private void RecordRenderHealthSample(
		int rowCursorBefore,
		int rowCursorAfter,
		int bandsProcessed,
		long tracedPixels,
		int hits,
		int quickRayZeroCount,
		int hybridFallbackCount,
		int hybridFallbackHitCount,
		int hybridFallbackMissCount,
		int hybridNoCandidateCount,
		long geomCandidatesTotal,
		long geomCandidatesSegments,
		long geomSegmentsQueried,
		long geomSegWithCandidates,
		long geomSegZeroCandidates,
		long geomPixelProcessed,
		long geomPixelHadAnyCandidates,
		long geomPixelNoCandidates,
		long geomHitAccepted,
		long geomHitRejected,
		long geomRayTestsTotal,
		long geomRayTestsAccepted,
		long geomRayTestsRejected,
		long pass2SampledSegments,
		double pass2RadiusSum,
		float pass2RadiusMax,
		double pass2EnvDiagSum,
		float pass2EnvDiagMax,
		double pass2EnvelopeInflationSum,
		float pass2EnvelopeInflationMax,
		long pass2CandidateCount0,
		long pass2CandidateCount1To2,
		long pass2CandidateCount3To8,
		long pass2CandidateCount9To32,
		long pass2CandidateCount33Plus,
		long pruneAuditSamples,
		long pruneAuditFalseNeg,
		long pruneAuditFalsePos,
		long pruneAuditCandidateZeroButBaselineHit,
		double avgStepsPerTracedPixel,
		double stepWallMs,
		string budgetExitReason,
		bool useGeometryTLASPruning,
		bool geomPruneRequested,
		bool pruneAuditEnabled)
	{
		bool geomPruneSwitched = _hasRenderHealthGeomPruneMode
			&& _lastRenderHealthGeomPruneMode != useGeometryTLASPruning;
		if (geomPruneSwitched)
		{
			ResetRenderHealthOverlayWindowState();
			_geomPruneSwitchedThisWindow = 1;
			// Mode switch -> reset mode-window counters to avoid mixed-window stats; required by regress invariants.
			// Rebase cumulative counters at mode transition so deltas stay mode-pure.
			_geomHitAcceptedLastSample = geomHitAccepted;
			_geomHitRejectedLastSample = geomHitRejected;
			_geomSegmentsQueriedLastSample = geomSegmentsQueried;
			_geomSegWithCandidatesLastSample = geomSegWithCandidates;
			_geomSegZeroCandidatesLastSample = geomSegZeroCandidates;
			_geomPixelProcessedLastSample = geomPixelProcessed;
			_geomPixelHadAnyCandidatesLastSample = geomPixelHadAnyCandidates;
			_geomPixelNoCandidatesLastSample = geomPixelNoCandidates;
			_geomRayTestsTotalLastSample = geomRayTestsTotal;
			_geomRayTestsAcceptedLastSample = geomRayTestsAccepted;
			_geomRayTestsRejectedLastSample = geomRayTestsRejected;
			_geomPruneAuditSamplesLastSample = pruneAuditSamples;
			_geomPruneAuditFalseNegLastSample = pruneAuditFalseNeg;
			_geomPruneAuditFalsePosLastSample = pruneAuditFalsePos;
			_geomPruneAuditCandidateZeroButBaselineHitLastSample = pruneAuditCandidateZeroButBaselineHit;
			_geomPruneAuditMismatchLogsThisWindow = 0;
			_geomPruneAuditSamplesTakenThisWindow = 0;

			if (useGeometryTLASPruning)
			{
				// ON mode starts a fresh "current ON" run, while OFF baseline remains available for saved%.
				_geomRayTestsPruningOnTotal = 0;
				_geomRayTestsPruningOnTracedPixels = 0;
			}
			else
			{
				// OFF mode relearns baseline from scratch to avoid mixed-mode drift.
				_geomRayTestsPruningOffTotal = 0;
				_geomRayTestsPruningOffTracedPixels = 0;
				_geomRayTestsOffPerPixelBaseline = 0.0;
				_geomRayTestsOffPerPixelBaselineReady = false;
			}
		}
		_lastRenderHealthGeomPruneMode = useGeometryTLASPruning;
		_hasRenderHealthGeomPruneMode = true;
		int modeWindowSamplesUsed = CountRenderHealthModeSamplesInWindow(useGeometryTLASPruning) + 1;
		bool geomWindowPartial = _geomPruneSwitchedThisWindow == 1
			|| modeWindowSamplesUsed < RenderHealthMinModeSamplesForTrust;

		_renderHealthStepIndex++;
		long geomSegmentsQueriedDelta = ComputeCounterDelta(geomSegmentsQueried, ref _geomSegmentsQueriedLastSample);
		long geomSegWithCandidatesDelta = ComputeCounterDelta(geomSegWithCandidates, ref _geomSegWithCandidatesLastSample);
		long geomSegZeroCandidatesDelta = ComputeCounterDelta(geomSegZeroCandidates, ref _geomSegZeroCandidatesLastSample);
		long geomPixelProcessedDelta = ComputeCounterDelta(geomPixelProcessed, ref _geomPixelProcessedLastSample);
		long geomPixelHadAnyCandidatesDelta = ComputeCounterDelta(geomPixelHadAnyCandidates, ref _geomPixelHadAnyCandidatesLastSample);
		long geomPixelNoCandidatesDelta = ComputeCounterDelta(geomPixelNoCandidates, ref _geomPixelNoCandidatesLastSample);
		long geomHitAcceptedDelta = ComputeCounterDelta(geomHitAccepted, ref _geomHitAcceptedLastSample);
		long geomHitRejectedDelta = ComputeCounterDelta(geomHitRejected, ref _geomHitRejectedLastSample);
		long geomRayTestsTotalDelta = ComputeCounterDelta(geomRayTestsTotal, ref _geomRayTestsTotalLastSample);
		long geomRayTestsAcceptedDelta = ComputeCounterDelta(geomRayTestsAccepted, ref _geomRayTestsAcceptedLastSample);
		long geomRayTestsRejectedDelta = ComputeCounterDelta(geomRayTestsRejected, ref _geomRayTestsRejectedLastSample);
		long pruneAuditSamplesDelta = ComputeCounterDelta(pruneAuditSamples, ref _geomPruneAuditSamplesLastSample);
		long pruneAuditFalseNegDelta = ComputeCounterDelta(pruneAuditFalseNeg, ref _geomPruneAuditFalseNegLastSample);
		long pruneAuditFalsePosDelta = ComputeCounterDelta(pruneAuditFalsePos, ref _geomPruneAuditFalsePosLastSample);
		long pruneAuditCandidateZeroButBaselineHitDelta = ComputeCounterDelta(pruneAuditCandidateZeroButBaselineHit, ref _geomPruneAuditCandidateZeroButBaselineHitLastSample);
		int rowsAdvanced = 0;
		int filmHLocal = _filmHeight;
		if (filmHLocal > 0)
		{
			rowsAdvanced = rowCursorAfter >= rowCursorBefore
				? rowCursorAfter - rowCursorBefore
				: (filmHLocal - rowCursorBefore) + rowCursorAfter;
		}

		var sample = new RenderHealthSample
		{
			StepIndex = _renderHealthStepIndex,
			RowCursorBefore = rowCursorBefore,
			RowCursorAfter = rowCursorAfter,
			RowsAdvanced = rowsAdvanced,
			BandsProcessed = bandsProcessed,
			TracedPixels = tracedPixels,
			Hits = hits,
			QuickRayZeroCount = quickRayZeroCount,
			HybridFallbackCount = hybridFallbackCount,
			HybridFallbackHitCount = hybridFallbackHitCount,
			HybridFallbackMissCount = hybridFallbackMissCount,
			HybridNoCandidateCount = hybridNoCandidateCount,
			GeomCandidatesTotal = geomCandidatesTotal,
			GeomCandidatesSegments = geomCandidatesSegments,
			GeomSegmentsQueried = geomSegmentsQueriedDelta,
			GeomSegWithCandidates = geomSegWithCandidatesDelta,
			GeomSegZeroCandidates = geomSegZeroCandidatesDelta,
			GeomPixelProcessed = geomPixelProcessedDelta,
			GeomPixelHadAnyCandidates = geomPixelHadAnyCandidatesDelta,
			GeomPixelNoCandidates = geomPixelNoCandidatesDelta,
			GeomHitAccepted = geomHitAcceptedDelta,
			GeomHitRejected = geomHitRejectedDelta,
			GeomRayTestsTotal = geomRayTestsTotalDelta,
			GeomRayTestsAccepted = geomRayTestsAcceptedDelta,
			GeomRayTestsRejected = geomRayTestsRejectedDelta,
			Pass2SampledSegments = pass2SampledSegments,
			Pass2RadiusSum = pass2RadiusSum,
			Pass2RadiusMax = pass2RadiusMax,
			Pass2EnvDiagSum = pass2EnvDiagSum,
			Pass2EnvDiagMax = pass2EnvDiagMax,
			Pass2EnvelopeInflationSum = pass2EnvelopeInflationSum,
			Pass2EnvelopeInflationMax = pass2EnvelopeInflationMax,
			Pass2CandidateCount0 = pass2CandidateCount0,
			Pass2CandidateCount1To2 = pass2CandidateCount1To2,
			Pass2CandidateCount3To8 = pass2CandidateCount3To8,
			Pass2CandidateCount9To32 = pass2CandidateCount9To32,
			Pass2CandidateCount33Plus = pass2CandidateCount33Plus,
			PruneAuditSamples = pruneAuditSamplesDelta,
			PruneAuditFalseNeg = pruneAuditFalseNegDelta,
			PruneAuditFalsePos = pruneAuditFalsePosDelta,
			PruneAuditCandidateZeroButBaselineHit = pruneAuditCandidateZeroButBaselineHitDelta,
			AvgStepsPerTracedPixel = avgStepsPerTracedPixel,
			BudgetExitReason = budgetExitReason ?? string.Empty,
			GeomPruneRequested = geomPruneRequested,
			UseGeometryTLASPruning = useGeometryTLASPruning,
			PruneAuditEnabled = pruneAuditEnabled,
			PresentPixelsUpdated = _lastPresentedPixelsUpdated,
			PresentCoverageRatio = _lastPresentedCoverageRatio,
			FramesToFullRefresh = _lastFramesToFullRefresh,
			TimeToFullRefreshMs = _lastTimeToFullRefreshMs,
			EffectiveFullRefreshFps = _lastEffectiveFullRefreshFps,
			FullRefreshMeasured = _lastFullRefreshMeasured,
			StepWallMs = stepWallMs
		};

		if (double.IsFinite(stepWallMs) && stepWallMs >= 0.0)
		{
			bool hasRayTests = sample.GeomRayTestsTotal > 0;
			long nowTicks = Stopwatch.GetTimestamp();
			_overlayRolling.AddSample(
				nowTicks,
				stepWallMs,
				Math.Max(0, sample.RowsAdvanced),
				Math.Max(0, sample.Hits),
				hasRayTests,
				Math.Max(0L, sample.GeomRayTestsTotal));
		}

		if (sample.UseGeometryTLASPruning)
		{
			_geomRayTestsPruningOnTotal += sample.GeomRayTestsTotal;
			_geomRayTestsPruningOnTracedPixels += sample.TracedPixels;
		}
		else
		{
			if (!geomWindowPartial)
			{
				_geomRayTestsPruningOffTotal += sample.GeomRayTestsTotal;
				_geomRayTestsPruningOffTracedPixels += sample.TracedPixels;
				if (_geomRayTestsPruningOffTracedPixels > 0)
				{
					// OFF-baseline is learned only from stable OFF windows.
					_geomRayTestsOffPerPixelBaseline = (double)_geomRayTestsPruningOffTotal / _geomRayTestsPruningOffTracedPixels;
					_geomRayTestsOffPerPixelBaselineReady = _geomRayTestsOffPerPixelBaseline > 0.0;
				}
			}
		}

		_renderHealthSamples[_renderHealthWrite] = sample;
		_renderHealthWrite = (_renderHealthWrite + 1) % RenderHealthBufferSize;
		if (_renderHealthCount < RenderHealthBufferSize) _renderHealthCount++;

		if (_autoBroadphaseCooldownRemaining > 0) _autoBroadphaseCooldownRemaining--;

		if (rowCursorAfter == _renderHealthLastRowCursor
			&& !string.IsNullOrEmpty(sample.BudgetExitReason)
			&& sample.BudgetExitReason == _renderHealthLastExitReason)
		{
			_renderHealthStallSteps++;
		}
		else
		{
			_renderHealthStallSteps = 0;
		}

		_renderHealthLastRowCursor = rowCursorAfter;
		_renderHealthLastExitReason = sample.BudgetExitReason;

		bool stalled = _renderHealthStallSteps >= RenderHealthStallThreshold;
		bool cadenceLog = (_renderHealthStepIndex % RenderHealthLogEveryNSteps) == 0;
		bool forceModeSwitchLog = geomPruneSwitched;
		if (stalled || cadenceLog || forceModeSwitchLog)
		{
			if (_renderHealthLastLogStep != _renderHealthStepIndex)
			{
				_renderHealthLastLogStep = _renderHealthStepIndex;
				LogRenderHealth(in sample, stalled);
				_geomSegmentsQueriedThisFrame = 0;
				_geomSegWithCandidatesThisFrame = 0;
				_geomSegZeroCandidatesThisFrame = 0;
				_geomPixelProcessedThisFrame = 0;
				_geomPixelHadAnyCandidatesThisFrame = 0;
				_geomPixelNoCandidatesThisFrame = 0;
				_geomSegmentsQueriedLastSample = 0;
				_geomSegWithCandidatesLastSample = 0;
				_geomSegZeroCandidatesLastSample = 0;
				_geomPixelProcessedLastSample = 0;
				_geomPixelHadAnyCandidatesLastSample = 0;
				_geomPixelNoCandidatesLastSample = 0;
				_geomRayTestsTotalLastSample = 0;
				_geomRayTestsAcceptedLastSample = 0;
				_geomRayTestsRejectedLastSample = 0;
				_geomPruneAuditSamplesThisFrame = 0;
				_geomPruneAuditFalseNegThisFrame = 0;
				_geomPruneAuditFalsePosThisFrame = 0;
				_geomPruneAuditCandidateZeroButBaselineHitThisFrame = 0;
				_geomPruneAuditSamplesLastSample = 0;
				_geomPruneAuditFalseNegLastSample = 0;
				_geomPruneAuditFalsePosLastSample = 0;
				_geomPruneAuditCandidateZeroButBaselineHitLastSample = 0;
				_geomPruneAuditMismatchLogsThisWindow = 0;
				_geomPruneAuditSamplesTakenThisWindow = 0;
				_geomPruneSwitchedThisWindow = 0;
			}
		}
	}

	private bool TryComputeAutoBroadphaseSignal(out string reason)
	{
		reason = "";
		int window = Math.Min(_renderHealthCount, AutoBroadphaseWindow);
		if (window <= 0) return false;

		double sum = 0.0;
		double sumSq = 0.0;
		int count = 0;
		float minHitRate = 1.0f;
		long tracedTotal = 0;

		for (int i = 0; i < window; i++)
		{
			RenderHealthSample s = GetRenderHealthSampleFromEnd(i);
			if (s.TracedPixels < AutoBroadphaseMinTracedPixels) continue;
			float hitRate = s.TracedPixels > 0 ? (float)s.Hits / s.TracedPixels : 0f;
			sum += hitRate;
			sumSq += hitRate * hitRate;
			count++;
			tracedTotal += s.TracedPixels;
			if (hitRate < minHitRate) minHitRate = hitRate;
		}

		if (count <= 0) return false;
		double mean = sum / count;
		double variance = Math.Max(0.0, (sumSq / count) - (mean * mean));

		if (minHitRate < AutoBroadphaseLowHitRate && tracedTotal >= AutoBroadphaseMinTracedPixels)
		{
			reason = $"low_hit_rate<{AutoBroadphaseLowHitRate:0.####}";
			return true;
		}

		if (variance > AutoBroadphaseVarianceThreshold)
		{
			reason = $"hit_var>{AutoBroadphaseVarianceThreshold:0.####}";
			return true;
		}

		return false;
	}

	private void LogAutoBroadphaseFlip(string reason, int cooldown)
	{
		GD.Print($"[AutoBroadphase] policy={_autoBroadphasePolicy} reason={reason} cooldown={cooldown}");
	}

	private BroadphasePolicyMode ResolveAutoBroadphasePolicy(out string reason)
	{
		if (_autoBroadphaseCooldownRemaining > 0)
		{
			reason = $"auto_cooldown:{_autoBroadphaseCooldownRemaining}";
			return BroadphasePolicyMode.OverlapOnly;
		}

		if (TryComputeAutoBroadphaseSignal(out string autoReason))
		{
			BroadphasePolicyMode nextPolicy = BroadphasePolicyMode.OverlapOnly;
			_autoBroadphaseCooldownRemaining = AutoBroadphaseCooldownSteps;
			if (_autoBroadphasePolicy != nextPolicy)
			{
				_autoBroadphasePolicy = nextPolicy;
				_autoBroadphaseLastFlipStep = _renderHealthStepIndex;
				LogAutoBroadphaseFlip(autoReason, _autoBroadphaseCooldownRemaining);
			}
			reason = autoReason;
			return nextPolicy;
		}

		BroadphasePolicyMode defaultPolicy = BroadphasePolicyMode.QuickRayOnly;
		if (_autoBroadphasePolicy != defaultPolicy)
		{
			_autoBroadphasePolicy = defaultPolicy;
			_autoBroadphaseLastFlipStep = _renderHealthStepIndex;
			LogAutoBroadphaseFlip("cooldown_end", 0);
		}
		reason = "auto_default";
		return defaultPolicy;
	}

	private static (bool quickRay, bool overlap) GetBroadphaseTogglesFromPolicy(BroadphasePolicyMode policy)
	{
		switch (policy)
		{
			case BroadphasePolicyMode.None:
				return (false, false);
			case BroadphasePolicyMode.QuickRayOnly:
				return (true, false);
			case BroadphasePolicyMode.OverlapOnly:
				return (false, true);
			case BroadphasePolicyMode.Both:
			case BroadphasePolicyMode.HybridQuickRayThenOverlap:
			default:
				return (true, true);
		}
	}

	private static BroadphasePolicyMode GetBroadphasePolicyFromToggles(bool quickRay, bool overlap)
	{
		if (quickRay && overlap) return BroadphasePolicyMode.Both;
		if (quickRay) return BroadphasePolicyMode.QuickRayOnly;
		if (overlap) return BroadphasePolicyMode.OverlapOnly;
		return BroadphasePolicyMode.None;
	}

	private void SyncBroadphaseControlsIfNeeded()
	{
		if (_isBroadphaseSyncing) return;

		_isBroadphaseSyncing = true;
		try
		{
			// Legacy field mirrors new mode.
			UseBroadphasePolicy = BroadphaseControlMode == BroadphaseMode.Policy;

			if (BroadphaseControlMode == BroadphaseMode.Manual)
			{
				BroadphasePolicyMode desiredPolicy = GetBroadphasePolicyFromToggles(UseBroadphaseQuickRay, UseBroadphaseOverlap);
				if (BroadphasePolicy != desiredPolicy)
				{
					BroadphasePolicy = desiredPolicy;
				}
			}
			else if (BroadphaseControlMode == BroadphaseMode.Off)
			{
				if (UseBroadphaseQuickRay || UseBroadphaseOverlap)
				{
					UseBroadphaseQuickRay = false;
					UseBroadphaseOverlap = false;
				}
				if (BroadphasePolicy != BroadphasePolicyMode.None)
				{
					BroadphasePolicy = BroadphasePolicyMode.None;
				}
			}
		}
		finally
		{
			_lastBroadphaseMode = BroadphaseControlMode;
			_lastBroadphasePolicy = BroadphasePolicy;
			_lastUseBroadphaseQuickRay = UseBroadphaseQuickRay;
			_lastUseBroadphaseOverlap = UseBroadphaseOverlap;
			_hasBroadphaseSyncSnapshot = true;
			_isBroadphaseSyncing = false;
		}
	}

	private void ResolveEffectiveBroadphase(
		out bool effQuickRay,
		out bool effOverlap,
		out BroadphasePolicyMode effPolicy,
		out BroadphaseMode effMode,
		out string source)
	{
		effMode = BroadphaseControlMode;
		switch (BroadphaseControlMode)
		{
			case BroadphaseMode.Off:
				effQuickRay = false;
				effOverlap = false;
				effPolicy = BroadphasePolicyMode.None;
				source = "off";
				break;
			case BroadphaseMode.Manual:
				effQuickRay = UseBroadphaseQuickRay;
				effOverlap = UseBroadphaseOverlap;
				effPolicy = GetBroadphasePolicyFromToggles(effQuickRay, effOverlap);
				source = "manual";
				break;
			case BroadphaseMode.Auto:
			{
				effPolicy = ResolveAutoBroadphasePolicy(out string autoReason);
				var (policyQuickRay, policyOverlap) = GetBroadphaseTogglesFromPolicy(effPolicy);
				effQuickRay = policyQuickRay;
				effOverlap = policyOverlap;
				source = autoReason;
				break;
			}
			case BroadphaseMode.Policy:
			default:
				effPolicy = BroadphasePolicy;
				var (policyQuick, policyOverlapPolicy) = GetBroadphaseTogglesFromPolicy(effPolicy);
				effQuickRay = policyQuick;
				effOverlap = policyOverlapPolicy;
				source = "policy";
				break;
		}

		if (BroadphaseControlMode != BroadphaseMode.Manual)
		{
			if (UseBroadphaseQuickRay != effQuickRay || UseBroadphaseOverlap != effOverlap)
			{
				UseBroadphaseQuickRay = effQuickRay;
				UseBroadphaseOverlap = effOverlap;
			}
			if (BroadphasePolicy != effPolicy)
			{
				BroadphasePolicy = effPolicy;
			}
		}
	}

	private (bool effQuickRay, bool effOverlap, BroadphasePolicyMode effPolicy, BroadphaseMode effMode, string sourceTag) UpdateBroadphaseEffectiveState()
	{
		SyncBroadphaseControlsIfNeeded();
		ResolveEffectiveBroadphase(out bool effQuickRay, out bool effOverlap, out BroadphasePolicyMode effPolicy, out BroadphaseMode effMode, out string source);
		LogBroadphaseEffectiveIfChanged(effQuickRay, effOverlap, effPolicy, effMode, source);
		return (effQuickRay, effOverlap, effPolicy, effMode, source);
	}

	private void LogBroadphaseEffectiveIfChanged(bool effQuickRay, bool effOverlap, BroadphasePolicyMode effPolicy, BroadphaseMode effMode, string sourceTag)
	{
		if (_hasLastBroadphaseEffective
			&& _lastBroadphaseEffectiveQuickRay == effQuickRay
			&& _lastBroadphaseEffectiveOverlap == effOverlap
			&& _lastBroadphaseEffectiveMode == effMode
			&& _lastBroadphaseEffectivePolicy == effPolicy)
		{
			return;
		}

		_lastBroadphaseEffectiveQuickRay = effQuickRay;
		_lastBroadphaseEffectiveOverlap = effOverlap;
		_lastBroadphaseEffectiveMode = effMode;
		_lastBroadphaseEffectivePolicy = effPolicy;
		_hasLastBroadphaseEffective = true;
		EffectiveBroadphaseMode = effMode;
		EffectiveBroadphasePolicy = effPolicy;
		EffectiveBroadphaseQuickRay = effQuickRay;
		EffectiveBroadphaseOverlap = effOverlap;
		string reasonTag = string.IsNullOrEmpty(sourceTag) ? "resolved" : sourceTag;
		EffectiveBroadphaseReason = reasonTag;

		GD.Print(
			$"[BroadphaseEffective] mode={effMode} policy={effPolicy} " +
			$"quick={effQuickRay} overlap={effOverlap} reason={reasonTag}");
	}

	private void MaybeWarnBroadphaseQuickRayCurved(float beta, float gamma, bool effQuickRay, bool useCameraPropsBetaGamma)
	{
		bool curvedInputs = useCameraPropsBetaGamma
			|| Math.Abs(beta) > 1e-6f
			|| Math.Abs(gamma) > 1e-6f;

		if (effQuickRay && curvedInputs)
		{
			if (_broadphaseCurvedWarned) return;
			_broadphaseCurvedWarned = true;
			GD.Print("[Warn] Broadphase QuickRay may miss hits under curved marching; consider OverlapOnly/Both or disable broadphase.");
			return;
		}

		_broadphaseCurvedWarned = false;
	}

	public void ApplyQualityModePreset(RenderQualityMode mode)
	{
		QualityMode = mode;
		MarkPresetDirty(scene: false, perf: false, quality: true, reason: "QualityModePreset");
	}

	private void ApplyQualityModePresetCore(RenderQualityMode mode)
	{
		switch (mode)
		{
			case RenderQualityMode.Debug:
				FilmResolutionScale = 0.25f;
				PixelStride = 4;
				RowsPerFrame = 2;
				TargetMsPerFrame = 8;
				MaxRowsPerFrameCap = 48;
				UpdateEveryFrameBudgetMs = 8f;
				RenderStepMaxMs = 20;
				Pass2SoftGateScoreBudgetPerFrame = 8;
				Pass2SoftGateMaxAttemptsPerPixel = 1;
				Pass2SoftGateMaxAttemptsPerFrame = 200;
				Pass2SoftGateMaxSubdividedCallsPerFrame = 400;
				break;
			case RenderQualityMode.FastPreview:
				FilmResolutionScale = 0.5f;
				PixelStride = 2;
				RowsPerFrame = 6;
				TargetMsPerFrame = 16;
				MaxRowsPerFrameCap = 128;
				UpdateEveryFrameBudgetMs = 16f;
				RenderStepMaxMs = 40;
				Pass2SoftGateScoreBudgetPerFrame = 16;
				Pass2SoftGateMaxAttemptsPerPixel = 2;
				Pass2SoftGateMaxAttemptsPerFrame = 1000;
				Pass2SoftGateMaxSubdividedCallsPerFrame = 2000;
				break;
			case RenderQualityMode.Balanced:
				FilmResolutionScale = 0.75f;
				PixelStride = 1;
				RowsPerFrame = 8;
				TargetMsPerFrame = 20;
				MaxRowsPerFrameCap = 256;
				UpdateEveryFrameBudgetMs = 20f;
				RenderStepMaxMs = 60;
				Pass2SoftGateScoreBudgetPerFrame = 32;
				Pass2SoftGateMaxAttemptsPerPixel = 3;
				Pass2SoftGateMaxAttemptsPerFrame = 3000;
				Pass2SoftGateMaxSubdividedCallsPerFrame = 6000;
				break;
			case RenderQualityMode.Quality:
				FilmResolutionScale = 1.0f;
				PixelStride = 1;
				RowsPerFrame = 16;
				TargetMsPerFrame = 33;
				MaxRowsPerFrameCap = 512;
				UpdateEveryFrameBudgetMs = 33f;
				RenderStepMaxMs = 100;
				Pass2SoftGateScoreBudgetPerFrame = 64;
				Pass2SoftGateMaxAttemptsPerPixel = 4;
				Pass2SoftGateMaxAttemptsPerFrame = 6000;
				Pass2SoftGateMaxSubdividedCallsPerFrame = 12000;
				break;
			case RenderQualityMode.Barebones:
				FilmResolutionScale = 0.25f;
				PixelStride = 2;
				RowsPerFrame = 8;
				TargetMsPerFrame = 500;
				MaxRowsPerFrameCap = 16;
				UpdateEveryFrameBudgetMs = 1000f;
				RenderStepMaxMs = 10000;
				Pass2SoftGateScoreBudgetPerFrame = 256;
				Pass2SoftGateMaxAttemptsPerPixel = 4;
				Pass2SoftGateMaxAttemptsPerFrame = 300;
				Pass2SoftGateMaxSubdividedCallsPerFrame = 500;
				break;
		}
	}

	void UpdateFilmOpacity()
	{
		var target = _filmView ?? _overlayRect;
		if (target == null) return;
		target.Modulate = new Color(1, 1, 1, FilmOpacity);
	}

}
