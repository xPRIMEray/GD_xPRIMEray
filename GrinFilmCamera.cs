using Godot;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using XPrimeRay.Perf; // adjust namespace new PerfScope.cs
using RendererCore.Common;
using RendererCore.SceneSnapshot;
using RendererCore.Fields;

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

	[ExportSubgroup("Logging & Diagnostics")]
	/// <summary>Fetches collider names for debug output.</summary>
	// CONTROL FACTOR: Fetch collider names; true adds lookup cost but improves debug readability.
	[Export] public bool NeedColliderNames = false;
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
	[Export(PropertyHint.Range, "1.0,2.0,0.01")] public float Pass2GeomEnvelopeRadiusScale = 1.10f;

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
	private FilmOverlay2D _filmOverlay;
	private float _rangeFar = 5f; // dynamic far distance used for mapping
	private int _depthHistWrite = 0;
	private float[] _depthHistory = Array.Empty<float>();
	private Image _img;
	private ImageTexture _tex;
	private int _filmWidth;
	private int _filmHeight;
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

	// conservative: max segments per ray = StepsPerRay / CollisionEveryNSteps + 2
	private int MaxSegPerRay => (_rbr != null)
		? (Mathf.Max(1, _rbr.StepsPerRay / Mathf.Max(1, _rbr.CollisionEveryNSteps)) + 2)
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
	private const int RenderHealthLogEveryNSteps = 30;
	private const int RenderHealthStallThreshold = 10;
	private const int RenderHealthPass2SampleEveryNSegments = 4096;

	private RenderHealthSample[] _renderHealthSamples = new RenderHealthSample[RenderHealthBufferSize];
	private int _renderHealthWrite = 0;
	private int _renderHealthCount = 0;
	private int _renderHealthStepIndex = 0;
	private int _renderHealthLastLogStep = -1;
	private int _renderHealthStallSteps = 0;
	private int _renderHealthLastRowCursor = -1;
	private string _renderHealthLastExitReason = "";
	private bool _rowStallActive = false;
	private int _renderHealthPass2SampleCounter = 0;

	// band hit ROI history
	private float[] _bandHitRate = Array.Empty<float>();
	private int[] _bandLowHitFrames = Array.Empty<int>();
	private Transform3D _lastCamTransform;
	private bool _hasLastCamTransform;
	private float _lastRangeFar;
	private bool _hasLastRangeFar;

	private FramePerf _framePerf = new FramePerf();
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
	// - geomSegZero increment site: Pass2 TLAS block immediately after QueryAabb(...) when pass==0 && candCount==0.
	// - geomPixNoCand increment site: per-pixel Pass2 epilogue when noCandidatesThisPixel && !hadCandidatesThisPixel.
	// These counters are log-window counters and reset only when a RenderHealth line is printed.
	private long _geomSegZeroCandidatesThisFrame = 0;
	private long _geomPixelNoCandidatesThisFrame = 0;
	private long _geomHitAcceptedThisFrame = 0;
	private long _geomHitRejectedThisFrame = 0;
	private long _geomSegZeroCandidatesLastSample = 0;
	private long _geomPixelNoCandidatesLastSample = 0;
	private long _geomSegmentsQueriedLastSample = 0;
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
	private int _lastSharedSnapshotHash = 0;
	private bool _hasSharedSnapshotHash = false;
	private int _lastSharedSnapshotMirrorHash = 0;
	private bool _hasSharedSnapshotMirrorHash = false;
	private int _lastProcessedPixelsThisBand = 0;
	private bool _hasLastProcessedPixelsThisBand = false;
	private int _broadphaseNoCandidateLogsRemaining = 0;
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
		public bool EnableProfiling;
		public bool VerbosePerfLogs;
		public bool EnableFramePerf;
		public bool FramePerfVerbose;
		public int FramePerfLogEveryNFrames;
		public bool NeedColliderNames;
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
		public long GeomSegZeroCandidates;
		public long GeomPixelNoCandidates;
		public long GeomHitAccepted;
		public long GeomHitRejected;
		public long Pass2SampledSegments;
		public double Pass2RadiusSum;
		public float Pass2RadiusMax;
		public double Pass2EnvDiagSum;
		public long Pass2CandidateCount0;
		public long Pass2CandidateCount1To2;
		public long Pass2CandidateCount3To8;
		public long Pass2CandidateCount9To32;
		public long Pass2CandidateCount33Plus;
		public double AvgStepsPerTracedPixel;
		public string BudgetExitReason;
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

		// Mirror RayBeamRenderer snapshot once after reference resolution.
		{
			RayBeamRenderer.SharedSnapshot snap = _rbr != null ? _rbr.GetSharedSnapshot() : default;
			UpdateSharedSnapshotMirror(in snap, force: true);
			GD.Print($"[RBRRef][Startup] HasRenderer(after mirror)={SharedRbrHasRenderer}");
		}

		GD.Print("✅ GrinFilmCamera ready. Rendering film.");
	}

	public override void _Process(double delta)
	{
		DebugLogConfig.EnableSnapshotLog = DebugSnapshotLog;
		DebugLogConfig.SnapshotLogIntervalSec = Mathf.Max(0.05f, DebugSnapshotIntervalSec);
		DebugLogConfig.EnableProbeLog = DebugProbeLog;
		DebugLogConfig.ProbeLogIntervalSec = Mathf.Max(0.05f, DebugProbeIntervalSec);
		DebugLogConfig.EnableGeomRejectSample = DebugGeomRejectSampleEnabled;

		SyncAndApplyIfDirty("process");
		// Keep broadphase controls in sync each frame so the inspector reflects effective state.
		UpdateBroadphaseEffectiveState();
		// DECISION: only render when UpdateEveryFrame is enabled.
		if (!UpdateEveryFrame) return;
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
				_legacyBackend.RenderFrame(snapshot);
				break;
			case RenderBackends.BackendMode.Compare:
				// TODO: compare mode; for now keep legacy render to avoid breaking output.
				_legacyBackend.RenderFrame(snapshot);
				break;
			default:
				_legacyBackend.RenderFrame(snapshot);
				break;
		}
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
		LogEffectiveConfigIfChanged(in cfg);
		EffectiveBroadphaseSettings broadphaseCfg = cfg.Broadphase;
		EffectiveSoftGateSettings softGateCfg = cfg.SoftGate;
		EffectiveRayMarchSettings rayCfg = cfg.RayMarch;
		EffectiveFilmSettings filmCfg = cfg.Film;
		bool effQuickRay = broadphaseCfg.UseQuickRay;
		bool effOverlap = broadphaseCfg.UseOverlap;

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
		long pass2CandidateCount0 = 0;
		long pass2CandidateCount1To2 = 0;
		long pass2CandidateCount3To8 = 0;
		long pass2CandidateCount9To32 = 0;
		long pass2CandidateCount33Plus = 0;
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
				if (_pendingBandHasPass1)
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
				budgetStopRowEnd = budgetStopRowCursor;
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
				_quickRayZeroCountThisFrame = 0;
				_hybridFallbackCountThisFrame = 0;
				_hybridFallbackHitCountThisFrame = 0;
				_hybridFallbackMissCountThisFrame = 0;
				_hybridNoCandidateCountThisFrame = 0;
				_geomHitAcceptedThisFrame = 0;
				_geomHitRejectedThisFrame = 0;
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

			// DECISION: log toggle snapshots only at frame start.
			if (frameStart)
			{
				MaybePrintToggleSnapshot(in cfg, in rayCfg);
				MaybePrintSoftGateConfigSnapshot(in cfg);
			}

			var space = _cam.GetWorld3D().DirectSpaceState;
			var snap = FrameSnapshotBus.CurrentSnapshot;
			int geomCountForScratch = snap?.Geometry?.Count ?? 0;
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
				rowsPerFrame = Mathf.Clamp(cappedRows, Mathf.Max(1, cfg.MinRowsPerFrame), maxRowsPerFrame);
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

			var basisLocal = basis; // capture for lambda
			Vector3 camPos = _cam.GlobalPosition;

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

				// DECISION: parallelize pass1 over all pixels in the band.
				System.Threading.Tasks.Parallel.For(
					0,
					pixelCount,
					new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = jobs },
					() =>
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
					},
					(pi, _, local) =>
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

						// CROSS-CLASS CONTRACT: RayBeamRenderer builds segments + pass1 hit info.
						int count = _rbr.BuildRaySegmentsCamera_Pass1(
							space,
							ref local.QuickRayParams,
							camPos, dirWorld, bendDir,
							center, beta, gamma,
							fieldSnaps, hasSources,
							farForSim,
							_segBuf, segOffset, maxSeg,
							insightPlane, useInsightPlane, insightEps,
							pass1StopOnHit,
							cfg.Pass1DoHitTest,
							cfg.Pass1ProbeEveryNSegments,
							cfg.Pass1ProbeMinTravelDelta,
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
					},
					local =>
					{
						// DECISION: merge thread-local counters into shared totals.
						if (collectPass1Perf)
						{
							Interlocked.Add(ref pass1PhysQueries, local.PhysQueries);
							Interlocked.Add(ref pass1EarlyStopPixels, local.EarlyStopPixels);
						}
						// DECISION: merge steps when enabled.
						if (collectPass1Steps) Interlocked.Add(ref pass1StepsIntegrated, local.StepsIntegrated);
						// DECISION: merge field evals when frame perf is enabled.
						if (framePerfEnabled) Interlocked.Add(ref pass1FieldEvals, local.FieldEvals);
						// DECISION: merge extra pass1 counters when enabled.
						if (framePerfEnabled)
						{
							Interlocked.Add(ref pass1Raycasts, local.Pass1Raycasts);
							Interlocked.Add(ref pass1ProbeHits, local.Pass1ProbeHits);
							Interlocked.Add(ref pass1FieldGridHits, local.FieldGridHits);
							Interlocked.Add(ref pass1FieldGridMisses, local.FieldGridMisses);
							Interlocked.Add(ref pass1FieldGridFallbacks, local.FieldGridFallbacks);
							Interlocked.Add(ref pass1FieldSourceEvals, local.FieldSourceEvals);
						}
					});

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
			long shadeUsecAccum = 0;
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

			Vector3 camPosPass2 = camPos;
			bool useOverlap = effOverlap;
			bool useQuickRay = effQuickRay;

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

			void RecordRenderHealthPass2Sample(float radius, float envDiag, int candidateCount)
			{
				if (float.IsNaN(radius) || float.IsInfinity(radius)) return;
				if (float.IsNaN(envDiag) || float.IsInfinity(envDiag)) return;

				pass2SampledSegments++;
				pass2RadiusSum += radius;
				if (radius > pass2RadiusMax) pass2RadiusMax = radius;
				pass2EnvDiagSum += envDiag;

				int bucketCount = candidateCount < 0 ? 0 : candidateCount;
				if (bucketCount <= 0) pass2CandidateCount0++;
				else if (bucketCount <= 2) pass2CandidateCount1To2++;
				else if (bucketCount <= 8) pass2CandidateCount3To8++;
				else if (bucketCount <= 32) pass2CandidateCount9To32++;
				else pass2CandidateCount33Plus++;
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

			// DECISION: skip physics if band-level skip is active.
			if (skipBandPhysics)
			{
				ulong shadeStart = 0;
				// DECISION: capture shade timing only when enabled.
				if (shadeTimingEnabled) shadeStart = Time.GetTicksUsec();

				int yAlignedStart = yStart + ((stride - (yStart % stride)) % stride);
				for (int y = yAlignedStart; y < yEnd; y += stride)
				{
					budgetStopRowCursor = y;
					// DECISION: watchdog may trigger budget stop or abort.
					if (CheckRenderStepWatchdog())
					{
						// DECISION: stop loop if budget stop was triggered.
						if (budgetStop) break;
						renderStepAbort = true;
						break;
					}
					int localY = y - yStart;
					for (int x = 0; x < filmW; x += stride)
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
						// DECISION: count filled pixels when stats enabled.
						if (statsEnabled) _perfFrame.FilledPixels += filled;
						// DECISION: count filled pixels for band when frame perf enabled.
						if (framePerfEnabled) bandFilledPixels += filled;
					}
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
					budgetStopRowCursor = y;
					// DECISION: watchdog may trigger budget stop or abort.
					if (CheckRenderStepWatchdog())
					{
						// DECISION: stop loop if budget stop was triggered.
						if (budgetStop) break;
						renderStepAbort = true;
						break;
					}
					int localY = y - yStart;
					for (int x = 0; x < filmW; x += stride)
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
						// DECISION: update traced pixels when stats enabled.
						if (statsEnabled) _perfFrame.TracedPixels++;
						bandTracedPixels++;
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
						bool noCandidatesThisPixel = false;
						bool useHybridBroadphase = broadphaseCfg.Policy == BroadphasePolicyMode.HybridQuickRayThenOverlap;
						var geomTlas = snap?.GeometryTLAS;
						var geomEntities = snap?.Geometry;
						bool useGeomTlas = geomTlas != null && geomEntities != null && geomEntities.Count > 0;

						bool hadHit = false;
						float hitDistance = 0f;
						string hitName = "<none>";
						float bestHit = float.PositiveInfinity;
						float bestHitDistAlongRay = float.PositiveInfinity;
						Vector3 bestHp = Vector3.Zero;
						Vector3 bestHn = Vector3.Up;

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
									&& (_renderHealthPass2SampleCounter++ % RenderHealthPass2SampleEveryNSegments) == 0;
								bool renderHealthSampleRecorded = false;
								float renderHealthSampleRadius = 0f;
								float renderHealthSampleEnvDiag = 0f;
								Aabb3 envelope = default;
								bool envelopeComputed = false;
								if (useGeomTlas || renderHealthSampleThisSeg)
								{
									var segANum = new System.Numerics.Vector3(segA.X, segA.Y, segA.Z);
									var segBNum = new System.Numerics.Vector3(segB.X, segB.Y, segB.Z);
									float geomEnvelopeRadius = seg.RadiusBound * Mathf.Max(1.0f, cfg.Pass2GeomEnvelopeRadiusScale);
									envelope = Aabb3.FromSegment(segANum, segBNum).Expand(geomEnvelopeRadius);
									envelopeComputed = true;
									renderHealthSampleRadius = geomEnvelopeRadius;
								}
								if (renderHealthSampleThisSeg && envelopeComputed)
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
								Span<long> geomCandidateInstanceIds = default;
								int geomCandidateInstanceCount = 0;
								bool geomCandidatesActive = false;
								/////////////////////////////////

								if (useGeomTlas)
								{
									Span<int> geomCandidates = _geomCandidatesScratch;
									int geomCandidateCount = geomTlas.QueryAabb(envelope, geomCandidates);
									if (pass == 0)
									{
										_geomSegmentsQueriedThisFrame++;
										// geomSegZero increments exactly once per queried Pass2 segment.
										if (geomCandidateCount == 0)
										_geomSegZeroCandidatesThisFrame++;
									}
									if (geomCandidateCount > 0)
									{
										geomCandidateInstanceIds = _geomCandidateInstanceIdsScratch;
										var ids = geomEntities.GodotInstanceIds;
										int idsLen = ids.Length;
										int maxFill = Math.Min(geomCandidateCount, geomCandidateInstanceIds.Length);
										for (int gi = 0; gi < maxFill; gi++)
										{
											int geomIndex = geomCandidates[gi];
											if ((uint)geomIndex < (uint)idsLen)
												geomCandidateInstanceIds[geomCandidateInstanceCount++] = ids[geomIndex];
										}
										if (geomCandidateInstanceCount > 1)
										{
											SortLongSpan(geomCandidateInstanceIds, geomCandidateInstanceCount);
											geomCandidateInstanceCount = DedupSortedLong(geomCandidateInstanceIds, geomCandidateInstanceCount);
										}
									}
									if (pass == 0)
									{
										_geomCandidatesSegmentsThisFrame++;
										_geomCandidatesTotalThisFrame += geomCandidateInstanceCount;
									}
									if (renderHealthSampleThisSeg && !renderHealthSampleRecorded && envelopeComputed)
									{
										RecordRenderHealthPass2Sample(renderHealthSampleRadius, renderHealthSampleEnvDiag, geomCandidateInstanceCount);
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
									didHit = RayBeamRenderer.SweepSegmentHit(space, segA, segB, rayCfg.CollisionMask, rayCfg.CollisionRadius, out hp);
									if ((statsEnabled || framePerfEnabled) && !segCounted)
									{
										if (statsEnabled) _perfFrame.SegsTested++;
										if (bandCountersEnabled) bandSegsTested++;
										segCounted = true;
									}
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
										var overlaps = localSpace.IntersectShape(_overlapQuery, broadphaseCfg.MaxResults);
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
										var hit0 = localSpace.IntersectRay(_quickRayParams);
										if (statsEnabled) _perfFrame.IntersectRayCalls++;
										if (bandCountersEnabled) bandPhysicsQueries++;
										if ((statsEnabled || framePerfEnabled) && !segCounted)
										{
											if (statsEnabled) _perfFrame.SegsTested++;
											if (bandCountersEnabled) bandSegsTested++;
											segCounted = true;
										}
										if (hit0.Count == 0)
										{
											qrayCount = 0;
											if (useHybridBroadphase)
											{
												MarkHybridQuickRayMissPending(ax, ay, az, bx, by, bz, false);
											}
											else
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

									int candidateCount = 0;
									int overlapCount = 0;
									int qrayCount = -1;
									bool forceNarrowphaseDueToQuickRay = false;
									var overlapCandidates = _pass2OverlapCandidatesScratch;
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

									if (renderHealthSampleThisSeg && !renderHealthSampleRecorded && envelopeComputed)
									{
										RecordRenderHealthPass2Sample(renderHealthSampleRadius, renderHealthSampleEnvDiag, candidateCount);
										renderHealthSampleRecorded = true;
									}

									if (budgetStop) break;
									if (skipBroadphaseSegment)
									{
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
										FlushHybridQuickRayMissCache();
										continue;
										}
									}

									if (candidateCount > 0 || softGateAllowedNoCandidate)
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
											var hit0 = space.IntersectRay(_quickRayParams);
											if (statsEnabled) _perfFrame.IntersectRayCalls++;
											if (bandCountersEnabled) bandPhysicsQueries++;
											if ((statsEnabled || framePerfEnabled) && !segCounted)
											{
												if (statsEnabled) _perfFrame.SegsTested++;
												if (bandCountersEnabled) bandSegsTested++;
												segCounted = true;
											}
											if (hit0.Count == 0)
											{
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
										didHit = RayBeamRenderer.SubdividedRayHit(
												space, segA, segB,
												rayCfg.CollisionMask,
												sub,
											out hp, out hn, out cid, out cname,
											out int rayQueries,
											includeColliderName: needHitName,
											hitBackFaces: pass2Flags.HitBackFaces,
											hitFromInside: pass2Flags.HitFromInside);
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

									if (didHit && geomCandidatesActive)
									{
										long geomId = unchecked((long)cid);
										if (ContainsSortedLong(geomCandidateInstanceIds, geomCandidateInstanceCount, geomId))
										{
											_geomHitAcceptedThisFrame++;
										}
										else
										{
											_geomHitRejectedThisFrame++;
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

					// geomPixNoCand rule: increment once per pixel only when every Pass2 segment had zero candidates.
					if (noCandidatesThisPixel && !hadCandidatesThisPixel)
						_geomPixelNoCandidatesThisFrame++;

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

						////
						////////////////////////
						ulong shadeStart = 0;
						if (shadeTimingEnabled) shadeStart = Time.GetTicksUsec();
						Color col = cfg.SkyColor;
						bool skipShading = rayCfg.RequireHitToRender && !hadHit;
						if (skipShading)
						{
							if (statsEnabled) _perfFrame.ShadingSkippedPixels++;
						}
						else if (hadHit)
						{
							bandHits++;

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

						int filled = FillPixelBlock(x, y, stride, col, filmW, filmH);
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
						if (softGateAttemptsThisPixel > maxAttemptsAnyPixelThisBand)
							maxAttemptsAnyPixelThisBand = softGateAttemptsThisPixel;
						if (softGateSubdividesThisPixel > maxSubdividesAnyPixelThisBand)
							maxSubdividesAnyPixelThisBand = softGateSubdividesThisPixel;
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
									ColliderId = 0,
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
				}
				}
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
					ResetRowCursor("completed");
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
				_geomSegZeroCandidatesThisFrame,
				_geomPixelNoCandidatesThisFrame,
				_geomHitAcceptedThisFrame,
				_geomHitRejectedThisFrame,
				pass2SampledSegments,
				pass2RadiusSum,
				pass2RadiusMax,
				pass2EnvDiagSum,
				pass2CandidateCount0,
				pass2CandidateCount1To2,
				pass2CandidateCount3To8,
				pass2CandidateCount9To32,
				pass2CandidateCount33Plus,
				avgStepsPerTracedPixel,
				healthExitReason);
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
			Interlocked.Exchange(ref _renderStepActive, 0);
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

	private static bool ContainsSortedLong(Span<long> data, int count, long value)
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
		Span<long> candidateIds,
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

	private void EnsureGeomScratchCapacity(int n)
	{
		if (_geomCandidatesScratch.Length < n)
			_geomCandidatesScratch = new int[n];
		if (_geomCandidateInstanceIdsScratch.Length < n)
			_geomCandidateInstanceIdsScratch = new long[n];
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
		if (stride <= 1)
		{
			if (x >= 0 && x < filmW && y >= 0 && y < filmH)
			{
				_img.SetPixel(x, y, col);
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
				_img.SetPixel(xx, yy, col);
				filled++;
			}
		}
		return filled;
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
			if (_pass2PrevHadHit.Length != targetPixels)
				_pass2PrevHadHit = new byte[targetPixels];
			if (_pass2HadHitLostThisFrame.Length != targetPixels)
				_pass2HadHitLostThisFrame = new byte[targetPixels];
			return false;
		}

		_filmWidth = targetW;
		_filmHeight = targetH;
		_img = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
		_img.Fill(cfg.SkyColor);
		_tex = ImageTexture.CreateFromImage(_img);
		_pass2PrevHadHit = new byte[_filmWidth * _filmHeight];
		_pass2HadHitLostThisFrame = new byte[_filmWidth * _filmHeight];

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
		GD.Print($"[FrameReset] reason={reason} prevRow={prev} frame={_frameIndex}");
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
			Pass2GeomEnvelopeRadiusScale = Mathf.Max(1.0f, Pass2GeomEnvelopeRadiusScale),
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
			EnableProfiling = EnableProfiling,
			VerbosePerfLogs = VerbosePerfLogs,
			EnableFramePerf = EnableFramePerf,
			FramePerfVerbose = FramePerfVerbose,
			FramePerfLogEveryNFrames = FramePerfLogEveryNFrames,
			NeedColliderNames = NeedColliderNames,
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
				MaxSegPerRay = Mathf.Max(1, sharedSnap.StepsPerRay / Mathf.Max(1, sharedSnap.CollisionEveryNSteps)) + 2
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
		hash.Add(cfg.EnableProfiling);
		hash.Add(cfg.VerbosePerfLogs);
		hash.Add(cfg.EnableFramePerf);
		hash.Add(cfg.FramePerfVerbose);
		hash.Add(cfg.FramePerfLogEveryNFrames);
		hash.Add(cfg.NeedColliderNames);
		hash.Add(cfg.UseFieldSourceCache);
		hash.Add(cfg.FieldSourceRefreshIntervalFrames);
		hash.Add(cfg.ShadingMode);
		hash.Add(cfg.FlipNormalToCamera);
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SkyColor.R));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SkyColor.G));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SkyColor.B));
		hash.Add(BitConverter.SingleToInt32Bits(cfg.SkyColor.A));
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
			$"minSeg={cfg.SoftGate.MinSegmentLength:0.###} attempts={cfg.SoftGate.MaxAttemptsPerFrame} sub={cfg.SoftGate.MaxSubdividedCallsPerFrame} " +
			$"stride={cfg.Film.PixelStride} resScale={cfg.Film.ResolutionScale:0.###} rows={cfg.Film.RowsPerFrame} " +
			$"stepLen={cfg.RayMarch.StepLength:0.###} collRad={cfg.RayMarch.CollisionRadius:0.###} mask=0x{cfg.RayMarch.CollisionMask:X8} " +
			$"envRadScale={cfg.Pass2GeomEnvelopeRadiusScale:0.###} maxDist={cfg.Film.MaxDistance:0.###}");
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

	private void LogRenderHealth(in RenderHealthSample latest, bool stalled)
	{
		int window = Math.Min(_renderHealthCount, 10);
		long totalTraced = 0;
		long totalHits = 0;
		long totalQuickRayZero = 0;
		long totalHybridFallback = 0;
		long totalHybridFallbackHits = 0;
		long totalHybridFallbackMisses = 0;
		long totalHybridNoCandidates = 0;
		long totalGeomCandidates = 0;
		long totalGeomCandidateSegments = 0;
		long totalGeomSegmentsQueried = 0;
		long totalGeomSegZeroCandidates = 0;
		long totalGeomPixelNoCandidates = 0;
		long totalGeomHitAccepted = 0;
		long totalGeomHitRejected = 0;
		long totalPass2SampledSegments = 0;
		double totalPass2RadiusSum = 0.0;
		float totalPass2RadiusMax = 0f;
		double totalPass2EnvDiagSum = 0.0;
		long totalPass2CandidateCount0 = 0;
		long totalPass2CandidateCount1To2 = 0;
		long totalPass2CandidateCount3To8 = 0;
		long totalPass2CandidateCount9To32 = 0;
		long totalPass2CandidateCount33Plus = 0;
		string topExit = "none";
		int topExitCount = 0;
		var exitCounts = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);

		for (int i = 0; i < window; i++)
		{
			RenderHealthSample s = GetRenderHealthSampleFromEnd(i);
			totalTraced += s.TracedPixels;
			totalHits += s.Hits;
			totalQuickRayZero += s.QuickRayZeroCount;
			totalHybridFallback += s.HybridFallbackCount;
			totalHybridFallbackHits += s.HybridFallbackHitCount;
			totalHybridFallbackMisses += s.HybridFallbackMissCount;
			totalHybridNoCandidates += s.HybridNoCandidateCount;
			totalGeomCandidates += s.GeomCandidatesTotal;
			totalGeomCandidateSegments += s.GeomCandidatesSegments;
			totalGeomSegmentsQueried += s.GeomSegmentsQueried;
			totalGeomSegZeroCandidates += s.GeomSegZeroCandidates;
			totalGeomPixelNoCandidates += s.GeomPixelNoCandidates;
			totalGeomHitAccepted += s.GeomHitAccepted;
			totalGeomHitRejected += s.GeomHitRejected;
			totalPass2SampledSegments += s.Pass2SampledSegments;
			totalPass2RadiusSum += s.Pass2RadiusSum;
			if (s.Pass2RadiusMax > totalPass2RadiusMax) totalPass2RadiusMax = s.Pass2RadiusMax;
			totalPass2EnvDiagSum += s.Pass2EnvDiagSum;
			totalPass2CandidateCount0 += s.Pass2CandidateCount0;
			totalPass2CandidateCount1To2 += s.Pass2CandidateCount1To2;
			totalPass2CandidateCount3To8 += s.Pass2CandidateCount3To8;
			totalPass2CandidateCount9To32 += s.Pass2CandidateCount9To32;
			totalPass2CandidateCount33Plus += s.Pass2CandidateCount33Plus;
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
		bool geomCounterGuardEnabled = DebugLogConfig.EnableGeomRejectSample || DebugGeomCounterGuardEnabled;
		if (geomCounterGuardEnabled && totalGeomSegZeroCandidates > totalGeomSegmentsQueried)
		{
			GD.PrintErr(
				$"[RenderHealth][WARN] drift=geomSegZero segZero={totalGeomSegZeroCandidates} segQueried={totalGeomSegmentsQueried} " +
				$"p2Samp={totalPass2SampledSegments} step={latest.StepIndex} row={latest.RowCursorAfter} bands={latest.BandsProcessed} window={window}");
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
		GD.Print(
			$"[RenderHealth] step={latest.StepIndex} lastRow={latest.RowCursorAfter} rowsAdv={latest.RowsAdvanced} bands={latest.BandsProcessed} " +
			$"stalledSteps={_renderHealthStallSteps} exit={exitTag} topExit={topExit} hitRate={hitRate:0.###} " +
			$"avgSteps={latest.AvgStepsPerTracedPixel:0.###} qray0={totalQuickRayZero} hybridFallback={totalHybridFallback} " +
			$"hybridFallbackHit={totalHybridFallbackHits} hybridFallbackMiss={totalHybridFallbackMisses} noCandidates={totalHybridNoCandidates} " +
			$"geomCandAvg={geomCandidatesAvg:0.###} geomSegZero={totalGeomSegZeroCandidates} geomPixNoCand={totalGeomPixelNoCandidates} " +
			$"geomHitOk={totalGeomHitAccepted} geomHitReject={totalGeomHitRejected} " +
			$"geomRejectSampleMissing={_geomRejectSampleCidNotInGeometryList} geomRejectSampleInList={_geomRejectSampleCidInGeometryListNotInCandidates} " +
			$"geomRejectSampleCandHit={_geomRejectSampleCandidateContainsCid} geomRejectSampleDominant={geomRejectSampleDominant} " +
			$"p2Samp={totalPass2SampledSegments} radAvg={pass2RadiusAvg:0.###} radMax={totalPass2RadiusMax:0.###} envDiagAvg={pass2EnvDiagAvg:0.###} " +
			$"cand0={totalPass2CandidateCount0} cand1to2={totalPass2CandidateCount1To2} cand3to8={totalPass2CandidateCount3To8} " +
			$"cand9to32={totalPass2CandidateCount9To32} cand33p={totalPass2CandidateCount33Plus}");
	}

	private static long ComputeCounterDelta(long current, ref long lastSample)
	{
		long delta = current - lastSample;
		if (delta < 0) delta = current;
		lastSample = current;
		return delta;
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
		long geomSegZeroCandidates,
		long geomPixelNoCandidates,
		long geomHitAccepted,
		long geomHitRejected,
		long pass2SampledSegments,
		double pass2RadiusSum,
		float pass2RadiusMax,
		double pass2EnvDiagSum,
		long pass2CandidateCount0,
		long pass2CandidateCount1To2,
		long pass2CandidateCount3To8,
		long pass2CandidateCount9To32,
		long pass2CandidateCount33Plus,
		double avgStepsPerTracedPixel,
		string budgetExitReason)
	{
		_renderHealthStepIndex++;
		long geomSegmentsQueriedDelta = ComputeCounterDelta(geomSegmentsQueried, ref _geomSegmentsQueriedLastSample);
		long geomSegZeroCandidatesDelta = ComputeCounterDelta(geomSegZeroCandidates, ref _geomSegZeroCandidatesLastSample);
		long geomPixelNoCandidatesDelta = ComputeCounterDelta(geomPixelNoCandidates, ref _geomPixelNoCandidatesLastSample);
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
			GeomSegZeroCandidates = geomSegZeroCandidatesDelta,
			GeomPixelNoCandidates = geomPixelNoCandidatesDelta,
			GeomHitAccepted = geomHitAccepted,
			GeomHitRejected = geomHitRejected,
			Pass2SampledSegments = pass2SampledSegments,
			Pass2RadiusSum = pass2RadiusSum,
			Pass2RadiusMax = pass2RadiusMax,
			Pass2EnvDiagSum = pass2EnvDiagSum,
			Pass2CandidateCount0 = pass2CandidateCount0,
			Pass2CandidateCount1To2 = pass2CandidateCount1To2,
			Pass2CandidateCount3To8 = pass2CandidateCount3To8,
			Pass2CandidateCount9To32 = pass2CandidateCount9To32,
			Pass2CandidateCount33Plus = pass2CandidateCount33Plus,
			AvgStepsPerTracedPixel = avgStepsPerTracedPixel,
			BudgetExitReason = budgetExitReason ?? string.Empty
		};

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
		if (stalled || cadenceLog)
		{
			if (_renderHealthLastLogStep != _renderHealthStepIndex)
			{
				_renderHealthLastLogStep = _renderHealthStepIndex;
				LogRenderHealth(in sample, stalled);
				_geomSegmentsQueriedThisFrame = 0;
				_geomSegZeroCandidatesThisFrame = 0;
				_geomPixelNoCandidatesThisFrame = 0;
				_geomSegmentsQueriedLastSample = 0;
				_geomSegZeroCandidatesLastSample = 0;
				_geomPixelNoCandidatesLastSample = 0;
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
