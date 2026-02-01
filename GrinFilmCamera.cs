using Godot;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using XPrimeRay.Perf; // adjust namespace new PerfScope.cs

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

	[ExportCategory("Film Camera")]
#region Pass2 SoftGate
	[ExportGroup("Physics / Collision / Pass2 SoftGate")]
	[ExportSubgroup("Core")]
	/// <summary>Allows occasional subdivide attempts on quick-ray misses (Pass2).</summary>
	// CONTROL FACTOR: Enables soft-gated subdivide probes on quick-ray misses; true increases accuracy at some cost.
	[Export] public bool Pass2SoftGateEnableQuickRayMiss = false;
	/// <summary>Use RayBeamRenderer step sizing to scale Pass2 SoftGate thresholds.</summary>
	// CONTROL FACTOR: When true, thresholds scale with RayBeamRenderer step sizes (keeps behavior consistent across step settings).
	[Export] public bool Pass2SoftGateUseRayBeamSettings = true;
	/// <summary>Disable SoftGate for the rest of the frame when overload is detected.</summary>
	// CONTROL FACTOR: When true, SoftGate shuts off mid-frame under overload; prevents long stalls but may reduce hits.
	[Export] public bool DisableSoftGateOnOverload = true;
	/// <summary>Minimum segment length in steps when using RayBeam settings (leave default unless you are tuning SoftGate).</summary>
	// CONTROL FACTOR: Minimum segment length (in steps) eligible for SoftGate when using RayBeam scaling; higher reduces probes.
	[Export] public float Pass2SoftGateMinSegLenSteps = 2.0f;

	[ExportSubgroup("Budget")]
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

	[ExportGroup("References")]
	/// <summary>NodePath to the RayBeamRenderer used for film segment generation.</summary>
	// CONTROL FACTOR: RayBeamRendererPath selects the ray integrator; wrong path breaks film ray generation.
	[Export] public NodePath RayBeamRendererPath;
	/// <summary>Optional TextureRect used to display the film texture.</summary>
	// CONTROL FACTOR: Optional UI target for film texture; when null, film still renders but no direct display.
	[Export] public NodePath FilmViewPath;
	/// <summary>Optional FilmOverlay2D for debug ray overlay.</summary>
	// CONTROL FACTOR: Optional overlay node for debug ray visualization.
	[Export] public NodePath FilmOverlayPath;

[ExportGroup("General")]
/// <summary>Runs RenderStep every frame when enabled.</summary>
// CONTROL FACTOR: Master toggle for per-frame RenderStep; false requires manual stepping.
[Export] public bool UpdateEveryFrame = true;
/// <summary>When UpdateEveryFrame is true, clamp per-call RenderStep budget to this value (ms). <=0 disables the clamp.</summary>
// CONTROL FACTOR: Per-call RenderStep time budget (ms); lower reduces work per frame.
[Export] public float UpdateEveryFrameBudgetMs = 16f;
/// <summary>When UpdateEveryFrame is true, hard-cap RenderStep band height (rows) per call.</summary>
// CONTROL FACTOR: Per-call row cap when updating every frame; lower spreads work across frames.
[Export] public int UpdateEveryFrameMaxRowsPerStep = 2;
/// <summary>Hard time budget for RenderStep (ms). Exceeding this disables UpdateEveryFrame.</summary>
// CONTROL FACTOR: Hard ceiling (ms); exceeding disables UpdateEveryFrame to prevent stalls.
[Export] public int RenderStepMaxMs = 50;
/// <summary>Hard cap on RenderStep pixel workload per frame. 0 disables.</summary>
// CONTROL FACTOR: Hard pixel cap per frame; lower reduces CPU cost.
[Export] public int RenderStepMaxPixelsPerFrame = 2000000;
/// <summary>Hard cap on RenderStep segments per frame. 0 disables.</summary>
// CONTROL FACTOR: Hard segment cap per frame; lower reduces collision workload.
[Export] public int RenderStepMaxSegmentsPerFrame = 20000000;

	[ExportCategory("Film Camera")]
	[ExportGroup("Rendering / Film Output")]
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

	[ExportGroup("Performance / Profiling")]
	/// <summary>Quality preset controlling key render budgets/strides.</summary>
	// CONTROL FACTOR: Quality mode preset; overrides key budgets/stride values.
	[Export] public RenderQualityMode QualityMode = RenderQualityMode.Balanced;
	/// <summary>Preset selection for tuning.</summary>
	// CONTROL FACTOR: Performance preset; higher quality increases cost.
	[Export] public PresetMode Preset = PresetMode.Preview;
	/// <summary>Apply the preset automatically in _Ready.</summary>
	// CONTROL FACTOR: Auto-apply preset on startup; true overrides manual tweaks.
	[Export] public bool ApplyPresetOnReady = false;
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
	/// <summary>Fetches collider names for debug output.</summary>
	// CONTROL FACTOR: Fetch collider names; true adds lookup cost but improves debug readability.
	[Export] public bool NeedColliderNames = false;
	/// <summary>Caches field source snapshots for faster updates.</summary>
	// CONTROL FACTOR: Cache field sources; true reduces per-frame scanning but may lag changes.
	[Export] public bool UseFieldSourceCache = false;
	/// <summary>How often to refresh cached field sources.</summary>
	// CONTROL FACTOR: Refresh interval in frames; higher = less overhead but more staleness.
	[Export] public int FieldSourceRefreshIntervalFrames = 30;

	[ExportGroup("Field Grid")]
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

	[ExportGroup("Rendering / Film Output")]
	/// <summary>Number of film rows rendered per frame.</summary>
	// CONTROL FACTOR: Rows per frame; higher = faster convergence but more per-frame cost.
	[Export] public int RowsPerFrame = 8;
	/// <summary>Target CPU time budget per RenderStep (ms). Set <=0 to disable adaptive rows.</summary>
	// CONTROL FACTOR: Target budget (ms) for adaptive rows; lower reduces work.
	[Export] public int TargetMsPerFrame = 16;
	/// <summary>Minimum rows per frame when adaptive rows are enabled.</summary>
	// CONTROL FACTOR: Minimum rows per frame under adaptive mode; higher keeps throughput up.
	[Export] public int MinRowsPerFrame = 4;
	/// <summary>Maximum rows per frame when adaptive rows are enabled.</summary>
	// CONTROL FACTOR: Maximum rows per frame under adaptive mode; higher allows bigger bursts.
	[Export] public int MaxRowsPerFrameCap = 256;
	/// <summary>Max ray distance when auto-range is disabled.</summary>
	// CONTROL FACTOR: Max ray distance (world units) when AutoRangeDepth is off.
	[Export] public float MaxDistance = 50f;
	/// <summary>Background color for no-hit pixels.</summary>
	// CONTROL FACTOR: Background color for miss pixels.
	[Export] public Color SkyColor = new Color(0, 0, 0, 1);
	/// <summary>Opacity applied to the film TextureRect.</summary>
	// CONTROL FACTOR: UI opacity for film display; higher = more opaque.
	[Export] public float FilmOpacity = 0.7f;

	[ExportGroup("Ray March / Sampling")]
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

	[ExportGroup("Auto Range / Depth")]
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

	[ExportGroup("Physics / Collision")]
	/// <summary>Enables a quick raycast broadphase test.</summary>
	// CONTROL FACTOR: Enables quick-ray broadphase; true reduces work by early rejection.
	[Export] public bool UseBroadphaseQuickRay = true;
	/// <summary>Enables a sphere overlap broadphase test.</summary>
	// CONTROL FACTOR: Enables overlap broadphase; true adds extra culling based on radius.
	[Export] public bool UseBroadphaseOverlap = false;
	/// <summary>Extra radius for overlap broadphase.</summary>
	// CONTROL FACTOR: Overlap margin (world units); higher catches more but costs more.
	[Export] public float BroadphaseMargin = 0.03f;
	/// <summary>Max overlap results to consider.</summary>
	// CONTROL FACTOR: Cap on overlap results; higher may increase cost.
	[Export] public int BroadphaseMaxResults = 8;
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
		None = 0,
		QuickRayOnly = 1,
		OverlapOnly = 2,
		Both = 3
	}

	/// <summary>Overrides broadphase toggles using BroadphasePolicy.</summary>
	// CONTROL FACTOR: When true, BroadphasePolicy overrides individual toggles.
	[Export] public bool UseBroadphasePolicy = false;
	/// <summary>Broadphase policy when UseBroadphasePolicy is true.</summary>
	// CONTROL FACTOR: Broadphase mode policy selection.
	[Export] public BroadphaseMode BroadphasePolicy = BroadphaseMode.QuickRayOnly;
	/// <summary>Uses a quick probe, then subdivides if needed.</summary>
	// CONTROL FACTOR: Enables quick probe then subdivide; true favors early-outs.
	[Export] public bool UseSingleProbeThenSubdivide = false;
	/// <summary>If true, keeps scanning segments for the nearest hit.</summary>
	// CONTROL FACTOR: Nearest-hit search; true prioritizes closest hit over first hit.
	[Export] public bool NearestHitOnly = false;

	public enum FilmShadingMode
	{
		DepthHeatmap = 0,   // your current behavior
		NormalRGB = 1,      // (N*0.5 + 0.5)
		NdotV = 2,          // grayscale: saturate(dot(N, V))
		TwoSidedNdotV = 3,  // grayscale: saturate(abs(dot(N, V)))
	}

	[ExportGroup("Rendering / Film Output")]
	/// <summary>Film shading mode (depth, normal RGB, NdotV).</summary>
	// CONTROL FACTOR: Shading mode selection; changes how hits map to film color.
	[Export] public FilmShadingMode ShadingMode = FilmShadingMode.DepthHeatmap;
	// Note: overlay normals are world-space collision normals (physics mesh).
	// Film distortion is a visualization artifact and does not change collider geometry.
	// For film-surface normals, use a screen-space gradient (see FilmOverlay2D) or a ray-space curvature normal; physics will not provide it.
	/// <summary>Flips hit normals to face the camera for shading.</summary>
	// CONTROL FACTOR: When true, normals are flipped toward camera; affects NdotV shading.
	[Export] public bool FlipNormalToCamera = true;

	[ExportGroup("Debug Visualization")]
	/// <summary>Debug ray sampling density for overlay.</summary>
	// CONTROL FACTOR: Debug ray stride; higher samples fewer rays.
	[Export] public int DebugEveryNPixels = 8;
	/// <summary>Cap on debug rays per band.</summary>
	// CONTROL FACTOR: Debug ray cap per band; limits overlay workload.
	[Export] public int DebugMaxFilmRays = 2048;
	[ExportGroup("Deprecated (No Effect)")]
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
	private readonly PerfStats _perfStats = new PerfStats(60);
	private PerfFrameReport _perfFrame;

	// field source cache
	private int _frameIndex = 0;
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

	private struct Pass2HitFlags
	{
		public bool HitBackFaces;
		public bool HitFromInside;
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
	private int _softGateSampleCounter = 0;
	private long _softGateAttemptsUsedThisFrame = 0;
	private long _softGateSubdividedCallsUsedThisFrame = 0;
	private SoftGateDebugCounters _softGateFrame;
	private SoftGateDebugCounters _softGateBand;
	private SoftGateConfigSnapshot _lastSoftGateCfgSnapshot;
	private bool _hasSoftGateCfgSnapshot = false;
	private int _p2SoftGateUsedThisFrame = 0;
	private int _softGateFrameId = -1;
	private int _softGateParamLogRemaining = 2;
	private int _budgetYieldLogFrameId = -1;
	private int _renderStepYieldLogFrameId = -1;
	private int _renderStepYieldLogsThisFrame = 0;
	private int _renderStepForceAdvanceWarnFrameId = -1;
	private int _renderStepForceAdvanceWarnsThisFrame = 0;
	private int _budgetExitFrameId = -1;
	private bool _budgetExitLoggedThisFrame = false;
	private RenderQualityMode _appliedQualityMode = (RenderQualityMode)(-1);
	private RandomNumberGenerator _rng = new RandomNumberGenerator();
	private volatile int _renderStepActive = 0;
	private bool _renderStepReentryWarned = false;
	private int _stuckBandStartRow = -1;
	private int _stuckBandEndRow = -1;
	private int _stuckBandRepeats = 0;
	private int _bandNoHitStallStartRow = -1;
	private int _bandNoHitStallEndRow = -1;
	private int _bandNoHitStallRepeats = 0;
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
	}

	private bool _dbgOnce = false;
	private void EarlyOut(string why)
	{
		//GD.PrintErr($"⛔ RenderStep early-out: {why} rowCursor={_rowCursor} cam={_cam?.GetPath()} rbr={_rbr?.GetPath()}");
		if (EnableProfiling) GD.Print($"[EarlyOut] {why} rowCursor={_rowCursor} cam={_cam?.GetPath()} rbr={_rbr?.GetPath()}");

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

		_rbr = GetNodeOrNull<RayBeamRenderer>(RayBeamRendererPath);
		GD.Print("RayBeamRenderer found? ", _rbr != null);
		// DECISION: abort if RayBeamRenderer is missing.
		if (_rbr == null)
		{
			GD.PushError("GrinFilmCamera: RayBeamRendererPath missing or invalid.");
			return;
		}

    	_rng.Randomize();

		// DECISION: optionally apply preset at startup.
		if (ApplyPresetOnReady)
		{
			ApplyPreset(Preset);
		}
		ApplyQualityModePresetIfNeeded("ready");

    	// ⛔ Freeze beam rebuilds while film camera is active
		// CROSS-CLASS CONTRACT: Freeze RayBeamRenderer rebuilds while film camera is active.
		// ASSUMPTION: film pass owns ray stability; external rebuilds would desync buffers.
		_rbr.AllowRebuild = false;

		_rangeFar = MaxDistance;
		_filmView = GetNodeOrNull<TextureRect>(FilmViewPath);
		GD.Print("FilmView found? ", _filmView != null);

		// EFFECT: allocate film image/texture buffers as needed.
		EnsureFilmImageSize();

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

		GD.Print("✅ GrinFilmCamera ready. Rendering film.");
	}

	public override void _Process(double delta)
	{
		ApplyQualityModePresetIfNeeded("process");
		// DECISION: only render when UpdateEveryFrame is enabled.
		if (!UpdateEveryFrame) return;
		RenderStep();
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

		// DECISION: record starting row for forward-progress guard.
		int startRow = _rowCursor;
		bool rowCursorResetThisStep = false;
		int budgetFrameId = (int)Engine.GetFramesDrawn();
		if (_budgetExitFrameId != budgetFrameId)
		{
			_budgetExitFrameId = budgetFrameId;
			_budgetExitLoggedThisFrame = false;
		}

		// EFFECT: start timing for watchdog/budget checks.
		Stopwatch renderStepWatch = Stopwatch.StartNew();
		bool renderStepAbort = false;
		bool renderStepAbortLogged = false;
		bool renderStepStopLogged = false;
		bool bandCommittedThisStep = false;
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
		string renderPhase = "enter";
		bool pass1CompletedThisStep = false;
		bool pass2CompletedThisStep = false;
		bool bandCompletedThisStep = false;
		bool bandSummaryLoggedThisBand = false;
		int bandStartRowCursor = _rowCursor;
		long pass1StepsIntegrated = 0;

		try
		{
			ulong t0 = Time.GetTicksUsec();
			// DECISION: enable stats when profiling or verbose logs are on.
			statsEnabled = EnableProfiling || VerbosePerfLogs;
			// DECISION: enable frame perf when configured.
			framePerfEnabled = EnableFramePerf;
			// DECISION: enable frame perf scope only when enabled.
			if (framePerfEnabled) frameScope = new PerfScope(_framePerf, PerfStage.FrameTotal);

		// Soft-gate debug toggles
		/////////////////////////////
		// DECISION: enable debug tiers based on verbosity level.
		bool softGateDebugEnabled = Pass2SoftGateDebugEnabled && Pass2SoftGateDebugVerbosity > 0;
		bool softGateBandEnabled = softGateDebugEnabled && Pass2SoftGateDebugVerbosity >= 2;
		bool softGateSegEnabled = softGateDebugEnabled && Pass2SoftGateDebugVerbosity >= 3;
		/////////////////////////////

			// DECISION: resize film buffers if resolution settings changed.
			bool resizedFilm = EnsureFilmImageSize();
			int settingsHash = ComputeFilmSettingsHash();
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
			}

			// DECISION: this is the start of a frame when row cursor wraps to 0.
			frameStart = _rowCursor == 0;
			int filmW = _filmWidth;
			int filmH = _filmHeight;
			// CONTROL FACTOR: PixelStride reduces sampling density.
			int stride = Mathf.Clamp(PixelStride, 1, 8);
			long tracedPixels = (long)filmW * filmH / Math.Max(1, stride * stride);

			// CONTROL FACTOR: effective SoftGate thresholds (may be scaled by RayBeam settings).
			float pass2SoftGateMinSegmentLengthEffective = Pass2SoftGateMinSegmentLength;
			int pass2SoftGateMaxAttemptsPerFrameEffective = Pass2SoftGateMaxAttemptsPerFrame;
			int pass2SoftGateMaxSubdividedCallsPerFrameEffective = Pass2SoftGateMaxSubdividedCallsPerFrame;
			float pass2SoftGateEffStepLen = 0f;
			bool pass2SoftGateUseRayBeamSettingsActive = false;

			// DECISION: optionally scale SoftGate thresholds based on RayBeamRenderer settings.
			if (Pass2SoftGateUseRayBeamSettings)
			{
				// DECISION: require RayBeamRenderer to source step settings.
				if (_rbr != null)
				{
					// CROSS-CLASS CONTRACT: use RayBeamRenderer step settings to scale SoftGate thresholds.
					float stepLength = _rbr.StepLength;
					float minStepLength = _rbr.MinStepLength;
					float maxStepLength = _rbr.MaxStepLength;
					float stepAdaptGain = _rbr.StepAdaptGain;
					bool stepsFinite = float.IsFinite(stepLength)
						&& float.IsFinite(minStepLength)
						&& float.IsFinite(maxStepLength)
						&& float.IsFinite(stepAdaptGain);
					// DECISION: only use step settings when all values are finite.
					if (stepsFinite)
					{
						float minStep = Mathf.Min(minStepLength, maxStepLength);
						float maxStep = Mathf.Max(minStepLength, maxStepLength);
						// EFFECT: compute effective step length in world units.
						pass2SoftGateEffStepLen = Mathf.Clamp(stepLength, minStep, maxStep);
						// CONTROL FACTOR: scale min segment length by effective step length.
						pass2SoftGateMinSegmentLengthEffective = Pass2SoftGateMinSegLenSteps * pass2SoftGateEffStepLen;

						// DECISION: derive step scale from effective step length.
						float stepScale = pass2SoftGateEffStepLen > 0f
							? Mathf.Clamp(1f / pass2SoftGateEffStepLen, 0.25f, 4f)
							: 1f;
						float strideScale = Mathf.Clamp(1f / Mathf.Max(1, stride), 0.125f, 1f);
						int derivedMaxAttemptsPerFrame = Mathf.Clamp(
							Mathf.RoundToInt(Pass2SoftGateMaxAttemptsPerFrame * stepScale * strideScale),
							Pass2SoftGateMaxAttemptsPerFrameMin,
							Pass2SoftGateMaxAttemptsPerFrameMax);
						int derivedMaxSubdividedCallsPerFrame = Mathf.Clamp(
							Mathf.RoundToInt(Pass2SoftGateMaxSubdividedCallsPerFrame * stepScale * strideScale),
							Pass2SoftGateMaxSubdividedCallsPerFrameMin,
							Pass2SoftGateMaxSubdividedCallsPerFrameMax);

						pass2SoftGateMaxAttemptsPerFrameEffective = derivedMaxAttemptsPerFrame;
						pass2SoftGateMaxSubdividedCallsPerFrameEffective = derivedMaxSubdividedCallsPerFrame;
						pass2SoftGateUseRayBeamSettingsActive = true;
					}
				}

				// DECISION: if RayBeam settings not used, fall back to manual settings.
				if (!pass2SoftGateUseRayBeamSettingsActive)
				{
					// Silent fallback to manual settings when RayBeam parameters are unavailable.
				}
			}

			// CONTROL FACTOR: effective time budget for RenderStep.
			float effectiveMaxMs = RenderStepMaxMs;
			// DECISION: clamp effective budget when UpdateEveryFrame budget is configured.
			if (UpdateEveryFrame && UpdateEveryFrameBudgetMs > 0f)
			{
				// DECISION: choose the tighter of RenderStepMaxMs and UpdateEveryFrameBudgetMs.
				float baseMax = RenderStepMaxMs > 0 ? RenderStepMaxMs : UpdateEveryFrameBudgetMs;
				effectiveMaxMs = Mathf.Min(baseMax, UpdateEveryFrameBudgetMs);
			}
			// DECISION: soft gate active only when enabled and not disabled for this pass.
			bool softGateEnabledNow = Pass2SoftGateEnableQuickRayMiss && Pass2SoftGateScoringEnabled && !_softGateDisabledForPass;
			// DECISION: clear pending band if its bounds are invalid.
			if (_pendingBandHasPass1 && (_pendingBandRowStart < 0 || _pendingBandRowCount <= 0))
			{
				_pendingBandRowStart = -1;
				_pendingBandRowCount = 0;
				_pendingBandHasPass1 = false;
			}
			pendingPass2 = _pendingBandHasPass1;
			bandH = 0;
			int bandTracedPixels = 0;
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
					$"pxCap={Pass2SoftGateMaxAttemptsPerPixel} scoreCap={Pass2SoftGateScoreBudgetPerFrame} " +
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
				LogBandSummaryOnce(MapBandSummaryReason(reason));

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
				if (UpdateEveryFrame && UpdateEveryFrameBudgetMs > 0f && (RenderStepMaxMs <= 0 || UpdateEveryFrameBudgetMs <= RenderStepMaxMs))
					return "update_every_frame_budget";
				return "renderstep_max_ms";
			}

			void TriggerBudgetStop(string reason)
			{
				// DECISION: only budget-stop when UpdateEveryFrame is active.
				if (!UpdateEveryFrame) return;
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

			void LogBudgetExitOnce(string reason, int rowCursor)
			{
				if (_budgetExitLoggedThisFrame) return;
				_budgetExitLoggedThisFrame = true;
				long elapsedMs = renderStepWatch.ElapsedMilliseconds;
				int rowsDoneThisStep = rowCursor >= startRow ? rowCursor - startRow : 0;
				int pixelCountLocal = bandH > 0 && filmW > 0 ? bandH * filmW : 0;
				int pixelCap = RenderStepMaxPixelsPerFrame > 0 ? RenderStepMaxPixelsPerFrame : 0;
				int attemptsCap = pass2SoftGateMaxAttemptsPerFrameEffective;
				int subdivCap = pass2SoftGateMaxSubdividedCallsPerFrameEffective;
				GD.Print(
					$"[BudgetExit] frame={_frameIndex} row={rowCursor} reason={reason} elapsedMs={elapsedMs} " +
					$"rowsDoneThisStep={rowsDoneThisStep} hitsThisBand={bandHits} " +
					$"attempts={_softGateAttemptsUsedThisFrame}/{attemptsCap} " +
					$"subdiv={_softGateSubdividedCallsUsedThisFrame}/{subdivCap} " +
					$"px={pixelCountLocal}/{pixelCap}");
			}

			string MapBandSummaryReason(string reason)
			{
				if (string.IsNullOrEmpty(reason)) return "normal";
				if (reason == "zero_hit_advance" || reason == "zero-hit-advance") return "zero-hit-advance";
				if (reason.Contains("guard") || reason.Contains("watchdog")) return "guard";
				if (reason.Contains("budget") || reason.Contains("max_ms") || reason.Contains("target_ms")) return "budget";
				if (reason.StartsWith("max_") || reason.StartsWith("sg_") || reason.Contains("max_segments") || reason.Contains("max_pixels")) return "cap";
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
					$"hits={bandHits} tracedPx={bandTracedPixels} avgSteps={avgStepsPerTracedPixel:0.00} reasonDone={reasonDone}");
			}

			void ResetNoHitStall()
			{
				_bandNoHitStallRepeats = 0;
				_bandNoHitStallStartRow = -1;
				_bandNoHitStallEndRow = -1;
			}

			bool TrackNoHitStall()
			{
				if (bandHits > 0 || _rowCursor != bandStartRowCursor)
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
				LogBandSummaryOnce(reasonDone);
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
				if (advance <= 0) advance = Math.Max(1, RowsPerFrame);
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
					LogBandSummaryOnce("guard");
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
				if (UpdateEveryFrame)
				{
					TriggerBudgetStop(GetMaxMsStopReason());
					return true;
				}
				// DECISION: first time over budget, mark abort and possibly disable soft gate.
				if (!renderStepAbort)
				{
					renderStepAbort = true;
					// DECISION: optionally disable SoftGate on overload to reduce work.
					if (DisableSoftGateOnOverload && softGateEnabledNow)
						DisableSoftGateThisFrame("renderstep_watchdog");
				}
				return true;
			}

			void AbortRenderStep(string reason)
			{
				// DECISION: abort is one-shot; skip if already logged.
				if (renderStepAbortLogged) return;
				renderStepAbortLogged = true;
				if (reason == "watchdog")
					LogBudgetExitOnce("renderstep_max_ms", _rowCursor);
				// EFFECT: disable UpdateEveryFrame on abort.
				UpdateEveryFrame = false;
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
					_perfFrame.RequireHitToRender = _rbr != null && _rbr.RequireHitToRender;
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
						Pass2SoftGateEnableQuickRayMiss,
						Pass2SoftGateScoringEnabled,
						pass2SoftGateMinSegmentLengthEffective,
						Pass2SoftGateScoreThreshold,
						Pass2SoftGateScoreTurnAngleWeight,
						Pass2SoftGateScorePrevHitLostBonus,
						Pass2SoftGateRandomProbeChance,
						Pass2SoftGateScoreBudgetPerFrame,
						Pass2SoftGateMaxAttemptsPerPixel,
						pass2SoftGateMaxAttemptsPerFrameEffective,
						pass2SoftGateMaxSubdividedCallsPerFrameEffective);
					_softGateSampleCounter = 0;
				}else{}
				_softGateAttemptsUsedThisFrame = 0;
				_softGateSubdividedCallsUsedThisFrame = 0;
				_softGateWatchdogLogsRemaining = Mathf.Max(0, Pass2SoftGateWatchdogLogLimitPerFrame);
				_softGateSummaryLogsRemaining = Mathf.Max(0, Pass2SoftGateDebugSummaryLogLimitPerFrame);
				/////////////////////////////
			}
			LogRenderPhase("enter");
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
				MaybePrintToggleSnapshot();
				MaybePrintSoftGateConfigSnapshot();
			}

			var space = _cam.GetWorld3D().DirectSpaceState;

			var fieldSnaps = GetFieldSourceSnaps(_frameIndex, out bool hasSources, out bool cacheRefreshed);
			// DECISION: track cache hits/misses for field sources when caching is enabled.
			if (framePerfEnabled && frameStart && UseFieldSourceCache)
			{
				// DECISION: count cache misses vs hits.
				if (cacheRefreshed) _framePerf.CacheMisses++;
				else _framePerf.CacheHits++;
			}

			// DECISION: throttle verbose field source logs to once per frame.
			if (VerbosePerfLogs && (_rowCursor % filmH) == 0)
				GD.Print($"fieldSnaps={fieldSnaps.Length} hasSources={hasSources}");


			float beta = 0f;
			float gamma = 2f;
			// DECISION: optionally pull Beta/Gamma from active camera.
			if (UseCameraPropsBetaGamma)
			{
				beta = ReadFloat(_cam, "Beta", 0f);
				gamma = ReadFloat(_cam, "Gamma", 2f);
			}

			// CROSS-CLASS CONTRACT: RayBeamRenderer decides field center policy.
			Vector3 center = _rbr.FieldCenterIsCamera ? _cam.GlobalPosition : _rbr.FieldCenter;
			var basis = _cam.GlobalTransform.Basis;

			float fovRad = Mathf.DegToRad(_cam.Fov);
			float tanHalf = Mathf.Tan(fovRad * 0.5f);
			float aspect = (float)filmW / Mathf.Max(1f, filmH);

			int maxSeg = MaxSegPerRay;
			yStart = _rowCursor;
			int baseRowsPerFrame = Mathf.Clamp(RowsPerFrame, Mathf.Max(1, MinRowsPerFrame), filmH);
			int maxRowsPerFrame = Mathf.Clamp(MaxRowsPerFrameCap, Mathf.Max(1, MinRowsPerFrame), filmH);
			// DECISION: disable adaptive rows when target ms <= 0 or no prior adaptive state.
			if (TargetMsPerFrame <= 0 || _adaptiveRowsPerFrame <= 0)
				_adaptiveRowsPerFrame = baseRowsPerFrame;
			rowsPerFrame = Mathf.Clamp(_adaptiveRowsPerFrame, Mathf.Max(1, MinRowsPerFrame), maxRowsPerFrame);
			// DECISION: keep adaptive state in sync.
			if (rowsPerFrame != _adaptiveRowsPerFrame)
				_adaptiveRowsPerFrame = rowsPerFrame;
			// DECISION: tighten row caps when UpdateEveryFrame is active.
			if (UpdateEveryFrame)
			{
				int updateEveryFrameMaxRows = Math.Max(1, UpdateEveryFrameMaxRowsPerStep);
				maxRowsPerFrame = Math.Min(maxRowsPerFrame, updateEveryFrameMaxRows);
				// DECISION: apply pixel/segment caps to row budget when configured.
				int maxRowsByPixel = RenderStepMaxPixelsPerFrame > 0
					? Math.Max(1, RenderStepMaxPixelsPerFrame / Math.Max(1, filmW))
					: int.MaxValue;
				int maxRowsBySeg = RenderStepMaxSegmentsPerFrame > 0
					? Math.Max(1, RenderStepMaxSegmentsPerFrame / Math.Max(1, filmW * maxSeg))
					: int.MaxValue;
				int cappedRows = Math.Min(rowsPerFrame, Math.Min(maxRowsByPixel, maxRowsBySeg));
				rowsPerFrame = Mathf.Clamp(cappedRows, Mathf.Max(1, MinRowsPerFrame), maxRowsPerFrame);
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
				LogBandSummaryOnce("guard");
				return;
			}

			// DECISION: detect repeated starts on the same band without a prior commit and force advance.
			if (bandH > 0)
			{
				bool noProgressSinceLast = _lastRenderStepRowCursor >= 0 && _rowCursor == _lastRenderStepRowCursor;
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
					LogBandSummaryOnce("guard");
					ResetNoHitStall();
					ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);
					return;
				}
			}

			int pixelCount = bandH * filmW;
			// DECISION: enforce max pixels per frame when configured.
			if (RenderStepMaxPixelsPerFrame > 0 && pixelCount > RenderStepMaxPixelsPerFrame)
			{
				// DECISION: yield when UpdateEveryFrame; abort otherwise.
				if (UpdateEveryFrame)
					TriggerBudgetStop("max_pixels");
				else
				{
					AbortRenderStep($"max-pixels {pixelCount}>{RenderStepMaxPixelsPerFrame}");
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
				if (budgetStopReason == "sg_attempts")
				{
					FinalizeBandAndAdvance("sg_attempts", yStart, yEnd, bandHits, "");
				}
				else
				{
					FinalizeBandAndAdvance(budgetStopReason, yStart, yEnd, bandHits, "");
				}
				ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);
				return;
			}

			EnsureDepthHistory();
			float frameMaxHit = 0f; // track deepest hit this RenderStep band

			bandHits = 0;
			// DECISION: choose far distance based on auto-range.
			float farForSim = AutoRangeDepth ? _rangeFar : MaxDistance;

			// Soft-gate band counters
			/////////////////////////////
			// DECISION: reset soft-gate band counters when enabled.
			if (softGateBandEnabled)
			{
				ResetSoftGateCounters(
					ref _softGateBand,
					_frameIndex,
					0,
					Pass2SoftGateEnableQuickRayMiss,
					Pass2SoftGateScoringEnabled,
					pass2SoftGateMinSegmentLengthEffective,
					Pass2SoftGateScoreThreshold,
					Pass2SoftGateScoreTurnAngleWeight,
					Pass2SoftGateScorePrevHitLostBonus,
					Pass2SoftGateRandomProbeChance,
					Pass2SoftGateScoreBudgetPerFrame,
					Pass2SoftGateMaxAttemptsPerPixel,
					pass2SoftGateMaxAttemptsPerFrameEffective,
					pass2SoftGateMaxSubdividedCallsPerFrameEffective);
			}else{}
			/////////////////////////////

			FieldGrid3D fieldGridForPass1 = null;
			// DECISION: use field grid only when enabled, integrated field is on, and sources exist.
			if (UseFieldGrid && _rbr.UseIntegratedField && hasSources)
			{
				int rebuildN = Mathf.Max(1, FieldGridRebuildEveryNFrames);
				bool shouldRebuild = cacheRefreshed || _fieldGrid == null || (_frameIndex % rebuildN) == 0;
				// DECISION: rebuild grid on schedule or when missing.
				if (shouldRebuild)
				{
					float cellSize = Mathf.Max(0.001f, FieldGridCellSize);
					float radius = Mathf.Max(0.01f, farForSim + FieldGridBoundsPadding);
					Vector3 half = new Vector3(radius, radius, radius);
					Vector3 origin = _cam.GlobalPosition - half;
					Aabb bounds = new Aabb(origin, half * 2f);
					_fieldGrid ??= new FieldGrid3D();
					_fieldGrid.BuildFromSources(fieldSnaps, beta, gamma, _rbr.BendScale, _rbr.FieldStrength, bounds, cellSize);
				}
				fieldGridForPass1 = _fieldGrid;
			}
			bool skipBandPhysics = false;
			int bandIndex = 0;
			// DECISION: band-level skip when enabled and history supports it.
			if (UseBandHitSkip)
			{
				EnsureBandHitHistory(filmH, rowsPerFrame);
				bandIndex = yStart / rowsPerFrame;

				// DECISION: invalidate history when camera/range changed.
				if (CheckAndUpdateBandInvalidation(_cam.GlobalTransform, farForSim))
					ResetBandHitHistory();

				// DECISION: skip physics when hit rate is low for enough frames.
				if (bandIndex >= 0 && bandIndex < _bandHitRate.Length && BandSkipFrames > 0)
				{
					// DECISION: only skip when hit rate is below threshold for long enough.
					if (_bandLowHitFrames[bandIndex] >= BandSkipFrames && _bandHitRate[bandIndex] < BandSkipHitThreshold)
						skipBandPhysics = true;
				}
			}

			// allocate / reuse buffers
			int segTotal = pixelCount * maxSeg;
			// DECISION: enforce max segments per frame when configured.
			if (RenderStepMaxSegmentsPerFrame > 0 && segTotal > RenderStepMaxSegmentsPerFrame)
			{
				// DECISION: yield when UpdateEveryFrame; abort otherwise.
				if (UpdateEveryFrame)
					TriggerBudgetStop("max_segments");
				else
				{
					AbortRenderStep($"max-segs {segTotal}>{RenderStepMaxSegmentsPerFrame}");
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
				if (budgetStopReason == "sg_attempts")
				{
					FinalizeBandAndAdvance("sg_attempts", yStart, yEnd, bandHits, "");
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
			bool wantDbg = (_rbr != null
				&& _rbr.DebugMode != RayBeamRenderer.DebugDrawMode.Off
				&& _rbr.DebugOverlayOwnedByFilm);
			// Rough upper bounds for this band (for capacity planning)
			// We’ll only sample 1 out of DebugEveryNPixels pixels.
			// DECISION: allocate debug buffers only when needed.
			if (wantDbg)
			{
				int pxStride = Math.Max(1, DebugEveryNPixels);
				int sampledW = (filmW + pxStride - 1) / pxStride;
				int sampledH = (bandH + pxStride - 1) / pxStride;
				int sampledPixels = sampledW * sampledH;
				sampledPixels = Math.Min(sampledPixels, DebugMaxFilmRays);

				// Each sampled pixel stores up to segCount+1 points; we’ll cap segments too
				int maxPtsPerRay = maxSeg + 1;
				EnsureFilmDebugCapacity(sampledPixels, sampledPixels * maxPtsPerRay);
			}

			// snapshot plane filter state (value types -> thread friendly)
			Plane insightPlane = default;
			bool useInsightPlane = false;
			float insightEps = _rbr.CollisionRadius;

			// DECISION: legacy pass2 insight plane toggle (currently unused).
			if (UseInsightPlanePass2 && _rbr.UseInsightPlaneFilter)
			{
				// easiest v0: rebuild plane here from a NodePath you expose, OR if _rbr has the plane cached, add a getter.
				// For now (if you don't have a getter), just leave this false until we wire it.
				// useInsightPlane = true; insightPlane = ...;
			}

			// DECISION: film pass currently disables insight plane unless wired.
			if (_rbr.UseInsightPlaneFilter)
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
			bool pass1StopOnHit = _rbr.StopOnHit || _rbr.TerminateTrailOnHit || _rbr.RequireHitToRender;
			long pass1PhysQueries = 0;
			long pass1EarlyStopPixels = 0;
			pass1StepsIntegrated = 0;
			long pass1FieldEvals = 0;
			long pass1Raycasts = 0;
			long pass1ProbeHits = 0;
			long pass1FieldGridHits = 0;
			long pass1FieldGridMisses = 0;
			// DECISION: skip pass1 when we are resuming a pending pass2 band.
			if (!pendingPass2)
			{
				pass1StartUsec = a0;
				LogRenderPhase("pass1-start");

				PerfScope pass1Scope = default;
				// DECISION: enable pass1 perf scope when frame perf is enabled.
				if (framePerfEnabled) pass1Scope = new PerfScope(_framePerf, PerfStage.Pass1_Integrate);

				bool collectPass1Perf = framePerfEnabled;
				bool collectPass1Steps = framePerfEnabled || VerbosePerfLogs;

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
								CollisionMask = _rbr.CollisionMask,
								CollideWithBodies = true,
								CollideWithAreas = true,
								HitFromInside = Pass2HitFromInside,
								HitBackFaces = Pass2HitBackFaces
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
							Pass1DoHitTest,
							Pass1ProbeEveryNSegments,
							Pass1ProbeMinTravelDelta,
							out RayBeamRenderer.Pass1HitInfo hitInfo,
							out bool stoppedEarly,
							out int hitSegIndex,
							out int stepsIntegrated,
							out int fieldEvals,
							out int pass1RaycastsLocal,
							out int pass1ProbeHitsLocal,
							out int fieldGridHitsLocal,
							out int fieldGridMissesLocal,
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
						}
					});

				// DECISION: dispose pass1 perf scope when enabled.
				if (framePerfEnabled) pass1Scope.Dispose();
				LogRenderPhase("pass1-end");

				a1 = Time.GetTicksUsec(); // after wait
				pass1EndUsec = a1;

				// DECISION: if we exceeded budget after pass1, defer pass2 to next frame.
				if (UpdateEveryFrame && effectiveMaxMs > 0f && renderStepWatch.ElapsedMilliseconds > effectiveMaxMs)
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
						LogBandSummaryOnce("guard");
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
			LogRenderPhase("pass2-start");
			bandHits = 0;
			bandTracedPixels = 0;
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
			LogRenderPhase("softgate-loop");

			Pass2HitFlags pass2Flags = new Pass2HitFlags
			{
				HitBackFaces = Pass2HitBackFaces,
				HitFromInside = Pass2HitFromInside
			};
			// DECISION: encode pass2 flags into a small int for cache keys.
			int pass2FlagsKey = (pass2Flags.HitBackFaces ? 1 : 0) | (pass2Flags.HitFromInside ? 2 : 0);
			int pass2QuickRayMissLogRemaining = Pass2LogQuickRayMissSamples;

			Vector3 camPosPass2 = camPos;
			bool useOverlap = UseBroadphaseOverlap;
			bool useQuickRay = UseBroadphaseQuickRay;
			// DECISION: optionally override broadphase toggles via policy.
			if (UseBroadphasePolicy)
			{
				// DECISION: select broadphase strategy based on policy.
				switch (BroadphasePolicy)
				{
					case BroadphaseMode.None:
						useOverlap = false;
						useQuickRay = false;
						break;
					case BroadphaseMode.QuickRayOnly:
						useOverlap = false;
						useQuickRay = true;
						break;
					case BroadphaseMode.OverlapOnly:
						useOverlap = true;
						useQuickRay = false;
						break;
					case BroadphaseMode.Both:
						useOverlap = true;
						useQuickRay = true;
						break;
				}
			}

			// DECISION: configure overlap broadphase only when enabled.
			if (useOverlap)
			{
				_overlapSphere ??= new SphereShape3D();
				_overlapQuery ??= new PhysicsShapeQueryParameters3D();
				_overlapSphere.Radius = _rbr.CollisionRadius + BroadphaseMargin;
				_overlapQuery.Shape = _overlapSphere;
				_overlapQuery.CollisionMask = _rbr.CollisionMask;
				_overlapQuery.CollideWithBodies = true;
				_overlapQuery.CollideWithAreas = true;
			}

			// DECISION: configure quick-ray params when quick probing is used.
			if (useQuickRay || UseSingleProbeThenSubdivide)
			{
				_quickRayParams ??= new PhysicsRayQueryParameters3D();
				_quickRayParams.CollisionMask = _rbr.CollisionMask;
				_quickRayParams.CollideWithBodies = true;
				_quickRayParams.CollideWithAreas = true;
				_quickRayParams.HitFromInside = pass2Flags.HitFromInside;
				_quickRayParams.HitBackFaces = pass2Flags.HitBackFaces;
			}

			// DECISION: reset quick-ray cache when quick probes are active.
			if (useQuickRay || UseSingleProbeThenSubdivide)
			{
				EnsurePass2QuickRayCache();
				ResetPass2QuickRayCache();
			}

			PerfScope pass2Scope = default;
			// DECISION: enable pass2 perf scope when frame perf is enabled.
			if (framePerfEnabled) pass2Scope = new PerfScope(_framePerf, PerfStage.Pass2_Subdivide);
			bool shadeTimingEnabled = statsEnabled || framePerfEnabled;

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
				if (Pass2SoftGateMaxAttemptsPerPixel > 0 && attemptsThisPixel >= Pass2SoftGateMaxAttemptsPerPixel)
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
					// DECISION: yield when updating every frame.
					if (UpdateEveryFrame) TriggerBudgetStop("sg_attempts");
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
					// DECISION: yield when updating every frame.
					if (UpdateEveryFrame) TriggerBudgetStop("sg_subdivide");
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

				bool attemptBudgetOk = (Pass2SoftGateMaxAttemptsPerPixel > 0 && attemptsThisPixel < Pass2SoftGateMaxAttemptsPerPixel)
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
					// DECISION: yield when updating every frame.
					if (UpdateEveryFrame) TriggerBudgetStop(attemptBudgetOk ? "sg_subdivide" : "sg_attempts");
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
				if (!Pass2SoftGateEnableQuickRayMiss || !Pass2SoftGateScoringEnabled)
				{
					reason = SoftGateDecisionReason.Disabled;
					SoftGateRecordSkip(reason);
					return false;
				}

				// DECISION: emit parameter logs only when debug enabled and budget remains.
				if (Pass2SoftGateDebugEnabled && _softGateParamLogRemaining > 0)
				{
					// DECISION: log includes RayBeam scaling when active.
					if (pass2SoftGateUseRayBeamSettingsActive)
					{
						GD.Print($"[SoftGate][Cfg] segIndex={segIndex} minSegLen={minSegLen:0.###} minSegSteps={Pass2SoftGateMinSegLenSteps:0.###} effStepLen={pass2SoftGateEffStepLen:0.###} scoreThr={Pass2SoftGateScoreThreshold:0.###} turnW={Pass2SoftGateScoreTurnAngleWeight:0.###} prevLost={Pass2SoftGateScorePrevHitLostBonus:0.###} rand={Pass2SoftGateRandomProbeChance:0.###}");
					}
					else
					{
						GD.Print($"[SoftGate][Cfg] segIndex={segIndex} minSegLen={minSegLen:0.###} scoreThr={Pass2SoftGateScoreThreshold:0.###} turnW={Pass2SoftGateScoreTurnAngleWeight:0.###} prevLost={Pass2SoftGateScorePrevHitLostBonus:0.###} rand={Pass2SoftGateRandomProbeChance:0.###}");
					}
					_softGateParamLogRemaining--;
				}

				bool metricsFinite = float.IsFinite(segmentLength)
					&& float.IsFinite(minSegLen)
					&& float.IsFinite(Pass2SoftGateScoreThreshold)
					&& float.IsFinite(Pass2SoftGateScoreTurnAngleWeight)
					&& float.IsFinite(Pass2SoftGateScorePrevHitLostBonus)
					&& float.IsFinite(Pass2SoftGateRandomProbeChance)
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
				if (haveDirs && Pass2SoftGateScoreTurnAngleWeight > 0f)
				{
					float dot = Mathf.Clamp(prevSegDir.Dot(currSegDir), -1f, 1f);
					turnAngleDeg = Mathf.RadToDeg(Mathf.Acos(dot));
					turnAngleScore = (turnAngleDeg / 180f) * Pass2SoftGateScoreTurnAngleWeight;
					score += turnAngleScore;
				}
				// Prev-hit-lost bonus: encourages probing when last frame hit disappeared.
				// DECISION: add bonus when previous hit was lost.
				if (prevHadHit && prevHitLost)
				{
					prevHitLostScore = Pass2SoftGateScorePrevHitLostBonus;
					score += prevHitLostScore;
				}

				// Random probe: avoids missing thin/rare occluders when score stays low.
				randomProbe = Pass2SoftGateRandomProbeChance > 0f && _rng.Randf() < Pass2SoftGateRandomProbeChance;
				bool scoreHit = score >= Pass2SoftGateScoreThreshold || randomProbe;

				// DECISION: record metric only when debug enabled.
				if (softGateDebugEnabled) SoftGateRecordMetric(score);

				// Score threshold: only trigger when instability evidence is strong enough.
				// DECISION: skip when score below threshold and no random probe.
				if (!scoreHit)
				{
					bool randEnabled = Pass2SoftGateRandomProbeChance > 0f;
					reason = randEnabled ? SoftGateDecisionReason.RandomNotSelected : SoftGateDecisionReason.ScoreTooLow;
					SoftGateRecordSkip(reason);
					return false;
				}

				// DECISION: enforce per-frame score budget.
				if (Pass2SoftGateScoreBudgetPerFrame > 0 && _p2SoftGateUsedThisFrame >= Pass2SoftGateScoreBudgetPerFrame)
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
				reasonText += $" seglen={(segLenOk ? "ok" : "short")} score={(score >= Pass2SoftGateScoreThreshold ? "ok" : "low")} rand={(randomProbe ? 1 : 0)}";

				GD.Print(
					$"SG seg={segIndex} len={segmentLength:0.###} score={score:0.###} angleDeg={turnAngleDeg:0.###} angleScore={turnAngleScore:0.###} prevLostScore={prevHitLostScore:0.###} forced={(forced ? 1 : 0)} attempt={(attempted ? 1 : 0)} hit={(hit ? 1 : 0)} reason={reasonText}");
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
						// DECISION: update perf stats when enabled.
						if (statsEnabled)
						{
							int pi = localY * filmW + x;
							_perfFrame.Segs += _segCountPerPixel[pi];
							// DECISION: count shading skipped pixels when RequireHitToRender is active.
							if (_rbr != null && _rbr.RequireHitToRender) _perfFrame.ShadingSkippedPixels++;
							_perfFrame.TracedPixels++;
						}
						// DECISION: update band counters when enabled.
						if (bandCountersEnabled)
						{
							int pi = localY * filmW + x;
							bandSegsIntegrated += _segCountPerPixel[pi];
						}
						bandTracedPixels++;
						int filled = FillPixelBlock(x, y, stride, SkyColor, filmW, filmH);
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
						int pi = localY * filmW + x;
						int globalPi = y * filmW + x;
						// DECISION: update traced pixels when stats enabled.
						if (statsEnabled) _perfFrame.TracedPixels++;
						bandTracedPixels++;

						// DECISION: previous-hit flag for instability probes.
						bool prevHadHit = Pass2ForceOnInstability
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
						bool logCenterSample = VerbosePerfLogs && isCenterSample;
						bool needHitName = NeedColliderNames || logCenterSample;
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
						bool allowFarEarlyOut = NearestHitOnly && segmentsMonotonic;
						float farEarlyOutEps = Mathf.Max(0f, EarlyOutDistanceEps);
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
									if (!UsePass2CollisionStride || !skippedAnyByStrideThisPixel || testedAnyInPass0ThisPixel)
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
								int pass2Stride = forceStride1 ? 1 : ComputePass2CollisionStride(seg.TraveledB, farForSim);

								if (segLen <= 1e-6f) continue;
								if (TinySegmentSkipLen > 0f && segLen < TinySegmentSkipLen) continue;
								if (allowFarEarlyOut && bestHitDistAlongRay < float.PositiveInfinity)
								{
									float segStartDist = seg.TraveledB - segLen;
									if (segStartDist > bestHitDistAlongRay + farEarlyOutEps)
									{
										earlyOutFarThisPixel = true;
										bandFarEarlyOuts++;
										if (framePerfEnabled) _framePerf.Pass2Skip_BestHitDist++;
										EarlyOut("far early-out");
										break;
									}
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
								/////////////////////////////////
								
								if (_rbr.UseSphereSweepCollision)
								{
									if (!forceStride1)
									{
										testedAnyInPass0ThisPixel = true;
										pass2StrideSum += pass2Stride;
										pass2StrideCount++;
									}
									didHit = RayBeamRenderer.SweepSegmentHit(space, segA, segB, _rbr.CollisionMask, _rbr.CollisionRadius, out hp);
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


									// Decision B
									// ---- PASS2 cheap reject 2: optional overlap ----
									if (useOverlap)
									{
										Vector3 mid = (segA + segB) * 0.5f;

										_overlapQuery.Transform = new Transform3D(Basis.Identity, mid);
										var overlaps = space.IntersectShape(_overlapQuery, BroadphaseMaxResults);
										if (statsEnabled) _perfFrame.IntersectShapeCalls++;
										if (bandCountersEnabled) bandPhysicsQueries++;
										if ((statsEnabled || framePerfEnabled) && !segCounted)
										{
											if (statsEnabled) _perfFrame.SegsTested++;
											if (bandCountersEnabled) bandSegsTested++;
											segCounted = true;
										}
										if (overlaps.Count == 0)
										{
											if (framePerfEnabled)
											{
												_framePerf.Pass2OverlapMisses++;
												_framePerf.Pass2Skip_OverlapEmpty++;
											}
											continue;
										}
										if (framePerfEnabled) _framePerf.Pass2OverlapHits++;
									}

									// Decision C
									// ---- PASS2 cheap reject 1: quick ray probe ----
									bool bypassQuickRayForRepresentative = allowInstabilityPass && si == forceRepSegIndex;
									if (useQuickRay && !bypassQuickRayForRepresentative)
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
												if (prevHadHitForSoftGate && si > segStart && _pass2HadHitLostThisFrame.Length > globalPi)
													_pass2HadHitLostThisFrame[globalPi] = 1;
												// SoftGate v2 uses per-pixel hit history; wire these to real history buffers if you track them elsewhere.
												bool prevHitLostForSoftGate = _pass2HadHitLostThisFrame.Length > globalPi && _pass2HadHitLostThisFrame[globalPi] != 0;
												if (!TryHandleQuickRayMissWithSoftGate(
													softGateFrameId,
													si,
													segLen,
													prevSegDir,
													currSegDir,
													prevHadHitForSoftGate,
													prevHitLostForSoftGate,
													UseSingleProbeThenSubdivide,
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
												if (prevHadHitForSoftGate && si > segStart && _pass2HadHitLostThisFrame.Length > globalPi)
													_pass2HadHitLostThisFrame[globalPi] = 1;
												bool prevHitLostForSoftGate = _pass2HadHitLostThisFrame.Length > globalPi && _pass2HadHitLostThisFrame[globalPi] != 0;
												if (!TryHandleQuickRayMissWithSoftGate(
													softGateFrameId,
													si,
													segLen,
													prevSegDir,
													currSegDir,
													prevHadHitForSoftGate,
													prevHitLostForSoftGate,
													UseSingleProbeThenSubdivide,
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

									if (UseSingleProbeThenSubdivide && !useQuickRay && !bypassQuickRayForRepresentative)
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
												if (prevHadHitForSoftGate && si > segStart && _pass2HadHitLostThisFrame.Length > globalPi)
													_pass2HadHitLostThisFrame[globalPi] = 1;
												bool prevHitLostForSoftGate = _pass2HadHitLostThisFrame.Length > globalPi && _pass2HadHitLostThisFrame[globalPi] != 0;
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
												if (prevHadHitForSoftGate && si > segStart && _pass2HadHitLostThisFrame.Length > globalPi)
													_pass2HadHitLostThisFrame[globalPi] = 1;
												bool prevHitLostForSoftGate = _pass2HadHitLostThisFrame.Length > globalPi && _pass2HadHitLostThisFrame[globalPi] != 0;
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
											|| (MinSegLenForStrideSkip > 0f && segLen < MinSegLenForStrideSkip);
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

									if (pass == 1 && pass2QuickRayMissLogRemaining > 0 && (useQuickRay || UseSingleProbeThenSubdivide))
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
										if (softGateAttemptedRay && Pass2SoftGateWatchdogMs > 0f)
											softGateStart = Time.GetTicksUsec();
										int sub = 1;
										if (segLen > _rbr.CollisionRaySubdivideThreshold)
											sub = Mathf.CeilToInt(segLen / _rbr.CollisionRaySubdivideThreshold);
										sub = Mathf.Clamp(sub, 1, _rbr.MaxCollisionSubsteps);

										if (UseAdaptiveSubsteps)
										{
											float far = AutoRangeDepth ? _rangeFar : MaxDistance;
											float t = Mathf.Clamp(seg.TraveledB / Mathf.Max(0.001f, far), 0f, 1f);
											float minSub = Mathf.Max(1f, sub * 0.25f);
											float scaled = Mathf.Lerp(sub, minSub, t);
											sub = Mathf.Clamp(Mathf.RoundToInt(scaled), 1, _rbr.MaxCollisionSubsteps);
										}

										if (softGateAttemptedRay)
										{
											softGateSubdividesThisPixel++;
											if (CheckRenderStepWatchdog())
											{
												if (budgetStop) break;
												renderStepAbort = true;
												softGateWatchdogTrippedThisPixel = true;
												break;
											}
										}
										didHit = RayBeamRenderer.SubdividedRayHit(
												space, segA, segB,
												_rbr.CollisionMask,
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
									if (softGateAttemptedRay && Pass2SoftGateWatchdogMs > 0f)
									{
										double elapsedMs = (Time.GetTicksUsec() - softGateStart) / 1000.0;
										if (elapsedMs > Pass2SoftGateWatchdogMs)
										{
											softGateLoopGuardTripped++;
											softGateWatchdogTrippedThisPixel = true;
											SoftGateRecordSkip(SoftGateDecisionReason.Guard);
											LogBudgetExitOnce("guard_softgate_watchdog", y);
											if (DisableSoftGateOnOverload)
											{
												_softGateDisabledForPass = true;
												DisableSoftGateThisFrame("softgate_watchdog");
											}
											if (Pass2SoftGateDebugEnabled && _softGateWatchdogLogsRemaining > 0)
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
										if (softGateWatchdogTrippedThisPixel)
											break;
									}

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
									if (NearestHitOnly)
									{
										if (EarlyOutDistanceEps > 0f && bestHit <= EarlyOutDistanceEps){
											EarlyOut("near early-out");
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
								if (!hadHit && Pass2ForceOnInstability && quickRayAllMiss)
								{
									bool allowForce = !Pass2ForceIfPrevHitLost || prevHadHit;
									if (allowForce && segCount > 0 && segStart <= segEnd)
									{
										forceInstabilityThisPixel = true;
										forcePrevHitLostThisPixel = Pass2ForceIfPrevHitLost && prevHadHit;
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
								EarlyOut("far early-out");
								break;
							}else{}

							if (softGateWatchdogTrippedThisPixel)
								break;

						if (hadHit)
							break;
					}
					if (budgetStop) break;

					if (statsEnabled)
					{
							ulong physEnd = Time.GetTicksUsec();
							_perfFrame.AddPass2PhysUsec(physEnd - physStart);
						}

						////
						////////////////////////
						ulong shadeStart = 0;
						if (shadeTimingEnabled) shadeStart = Time.GetTicksUsec();
						Color col = SkyColor;
						bool skipShading = _rbr != null && _rbr.RequireHitToRender && !hadHit;
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
							switch (ShadingMode)
							{
								default:
								case FilmShadingMode.DepthHeatmap:
								{
									float far = AutoRangeDepth ? _rangeFar : MaxDistance;
									float d = Mathf.Clamp(hitDistance / Mathf.Max(0.001f, far), 0f, 1f);
									col = Color.FromHsv(0.66f * (1f - d), 1f, 1f);
									break;
								}

								case FilmShadingMode.NormalRGB:
								{
									// hn is the physics collision normal for the nearest hit.
									Vector3 n = bestHn;
									if (FlipNormalToCamera)
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
									if (FlipNormalToCamera && rawDot < 0f)
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
								GD.Print($"Film hit: dist={hitDistance:0.000} name={hitName} mode={ShadingMode}");
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
							int pxStride = Math.Max(1, DebugEveryNPixels);

							// Sample a sparse grid (keeps overlay readable + fast)
							if ((x % pxStride) == 0 && (y % pxStride) == 0 && _dbgRayCount < DebugMaxFilmRays)
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
								if (VerbosePerfLogs && _dbgHits[rayIndex].Valid != hadHit)
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
			if (framePerfEnabled) pass2Scope.Dispose();
			pass2CompletedThisStep = !budgetStop && !renderStepAbort;
			if (renderStepAbort && !budgetStop)
			{
				AbortRenderStep("watchdog");
				string maxMsReason = GetMaxMsStopReason();
				LogRenderStopOnce(maxMsReason);
				LogBudgetExitOnce(maxMsReason, _rowCursor);
				ForceAdvanceRowCursorOnStop(maxMsReason, yEnd);
				LogBandSummaryOnce("guard");
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
			if (UseBandHitSkip && bandIndex >= 0 && bandIndex < _bandHitRate.Length)
			{
				float hitRate = bandTracedPixels > 0 ? (float)bandHits / bandTracedPixels : 0f;
				_bandHitRate[bandIndex] = hitRate;
				if (hitRate < BandSkipHitThreshold)
					_bandLowHitFrames[bandIndex]++;
				else
					_bandLowHitFrames[bandIndex] = 0;
			}

			// ---- Debug overlay draw ONCE per band ----
			if (wantDbg && _filmOverlay != null)
			{
				ulong dbgOverlayStart = 0;
				if (statsEnabled) dbgOverlayStart = Time.GetTicksUsec();

				if (VerbosePerfLogs)
					ValidateDebugOverlayData();

				_filmOverlay.SetData(
					_cam,
					_dbgPts.AsSpan(0, _dbgPtWrite),
					_dbgOff.AsSpan(0, _dbgRayCount),
					_dbgCnt.AsSpan(0, _dbgRayCount),
					_dbgHits.AsSpan(0, _dbgRayCount),
					_rbr.DebugNormalLen,
					_img,
					filmW,
					filmH,
					DebugEveryNPixels
				);

				if (statsEnabled)
				{
					ulong dbgOverlayEnd = Time.GetTicksUsec();
					_perfFrame.AddOverlayEnqueueUsec(dbgOverlayEnd - dbgOverlayStart);
				}
			}
			else if (_filmOverlay != null && _rbr != null && _rbr.DebugOverlayOwnedByFilm)
			{
				_filmOverlay.ClearOverlay();
			}
			if (!wantDbg && _filmOverlay != null && _filmOverlay.DrawFilmGradientNormals)
			{
				_filmOverlay.SetFilmImage(_img, filmW, filmH, DebugEveryNPixels);
			}


			if (AutoRangeDepth && frameMaxHit > 0.0001f)
			{
				// write one sample per RenderStep call (band-based)
				_depthHistory[_depthHistWrite] = frameMaxHit;
				_depthHistWrite = (_depthHistWrite + 1) % _depthHistory.Length;

				// robust far plane estimate + safety multiplier
				float robust = RobustFarEstimate_Fallback(); // use fallback for reliability
				float targetFar = robust * AutoRangeSafety;

				// clamp
				targetFar = Mathf.Clamp(targetFar, AutoRangeMin, AutoRangeMax);

				// smooth
				_rangeFar = Mathf.Lerp(_rangeFar, targetFar, AutoRangeSmoothing);
			}
			if (VerbosePerfLogs && _rowCursor == 0 && AutoRangeDepth)
				GD.Print($"AutoRange Far={_rangeFar:0.###}  (MaxDistance export={MaxDistance:0.###})");


			if (VerbosePerfLogs)
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
				if (budgetStopReason == "sg_attempts")
				{
					FinalizeBandAndAdvance("sg_attempts", yStart, yEnd, bandHits, "");
				}
				else if (isTimeBudget && !bandCompletedThisStep)
				{
					LogRenderStopOnce(budgetStopReason);
					if (!ForceAdvanceOnNoHit(budgetStopReason, "zero-hit-advance", true))
					{
						LogBandSummaryOnce("budget");
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
					LogBandSummaryOnce(bandHits == 0 ? "zero-hit-advance" : "normal");
				else
					ForceAdvanceOnNoHit("guard_no_progress", "zero-hit-advance", false);
				if (bandAdvanced) ResetNoHitStall();
			}
			ApplyDeferredRowCursorResetIfNeeded(yStart, yEnd);

			ulong t1 = Time.GetTicksUsec();
			if (VerbosePerfLogs)
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
				_perfStats.FinalizeAndPrint(ref _perfFrame, VerbosePerfLogs);
			}
			if (framePerfEnabled && _rowCursor == 0)
			{
				int logEvery = Mathf.Max(1, FramePerfLogEveryNFrames);
				bool shouldLogFramePerf = FramePerfVerbose || (_frameIndex % logEvery) == 0;
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
			LogRenderPhase("end");
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
				if (Pass2SoftGateDebugSummaryPerFrame && _softGateSummaryLogsRemaining > 0)
				{
					GD.Print(BuildSoftGateDebugSummary(_softGateFrame));
					_softGateSummaryLogsRemaining--;
				}

				bool haveAutoRangeFar = AutoRangeDepth && float.IsFinite(_rangeFar) && _rangeFar > 0f;
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
						GD.Print($"[SoftGate][WARN] seglen skips dominate with attempts=0 while using RayBeam settings; consider lowering Pass2SoftGateMinSegLenSteps (cur={Pass2SoftGateMinSegLenSteps:0.###}).");
					}
				}
				if (_softGateFrame.SoftGateEnabled && _softGateFrame.SoftGateConsidered > 0 && _softGateFrame.SoftGateAttempts == 0)
				{
					GD.Print("[SoftGate][WARN] enabled but no attempts: check gating (minSegLen/score/random) summary above.");
				}
			}
		}
		finally
		{
			if (framePerfEnabled) frameScope.Dispose();
			_lastBandCommitted = bandCommittedThisStep;
			_lastRenderStepRowCursor = _rowCursor;
			_lastRenderStepBandStart = yStart;
			_lastRenderStepBandEnd = yEnd;
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

	private RayBeamRenderer.FieldSourceSnap[] GetFieldSourceSnaps(int frameIndex, out bool hasSources, out bool cacheRefreshed)
	{
		cacheRefreshed = false;
		if (!UseFieldSourceCache)
		{
			var fieldSources = GetTree().GetNodesInGroup("field_sources");
			var snaps = _rbr.SnapshotFieldSources(fieldSources);
			hasSources = snaps.Length > 0;
			return snaps;
		}

		bool needsRefresh = false;
		int refreshInterval = Mathf.Max(1, FieldSourceRefreshIntervalFrames);
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

	private bool CheckAndUpdateBandInvalidation(Transform3D current, float rangeFar)
	{
		bool invalidate = false;
		if (_hasLastCamTransform)
		{
			float posDelta = (current.Origin - _lastCamTransform.Origin).Length();
			float basisDelta = MaxBasisDelta(current.Basis, _lastCamTransform.Basis);
			if (posDelta > BandSkipInvalidatePosDelta || basisDelta > BandSkipInvalidateBasisDelta)
				invalidate = true;
		}

		if (_hasLastRangeFar && BandSkipInvalidateRangeDelta > 0f)
		{
			if (Mathf.Abs(rangeFar - _lastRangeFar) > BandSkipInvalidateRangeDelta)
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

	private void EnsureDepthHistory()
	{
		if (_depthHistory.Length != DepthHistoryFrames)
		{
			_depthHistory = new float[Mathf.Max(4, DepthHistoryFrames)];
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

	private int ComputePass2CollisionStride(float traveledB, float far)
	{
		if (!UsePass2CollisionStride) return 1;
		int nearS = Mathf.Clamp(Pass2CollisionStrideNear, 1, 32);
		int farS = Mathf.Clamp(Pass2CollisionStrideFar, 1, 32);
		if (farS <= nearS) return nearS;

		float t = traveledB / Mathf.Max(0.001f, far);
		float startT = Mathf.Clamp(Pass2CollisionStrideFarStartT, 0f, 1f);
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

	private bool EnsureFilmImageSize()
	{
		float scale = Mathf.Clamp(FilmResolutionScale, 0.01f, 1.0f);
		int targetW = Mathf.Max(8, Mathf.RoundToInt(Width * scale));
		int targetH = Mathf.Max(8, Mathf.RoundToInt(Height * scale));
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
		_img.Fill(SkyColor);
		_tex = ImageTexture.CreateFromImage(_img);
		_pass2PrevHadHit = new byte[_filmWidth * _filmHeight];
		_pass2HadHitLostThisFrame = new byte[_filmWidth * _filmHeight];

		UpdateFilmViewTexture();

		return true;
	}

	private int ComputeFilmSettingsHash()
	{
		unchecked
		{
			int hash = 17;
			hash = hash * 31 + Width;
			hash = hash * 31 + Height;
			hash = hash * 31 + PixelStride;
			hash = hash * 31 + FilmResolutionScale.GetHashCode();
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

	private void ValidateDebugOverlayData()
	{
		if (_dbgRayCount > DebugMaxFilmRays)
			GD.Print($"Debug overlay rayCount exceeded cap: {_dbgRayCount} > {DebugMaxFilmRays}");

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

	private void MaybePrintToggleSnapshot()
	{
		if (_rbr == null) return;

		ToggleSnapshot cur = new ToggleSnapshot
		{
			UseAdaptiveSubsteps = UseAdaptiveSubsteps,
			UseSingleProbeThenSubdivide = UseSingleProbeThenSubdivide,
			UseBandHitSkip = UseBandHitSkip,
			RequireHitToRender = _rbr.RequireHitToRender,
			StopOnHit = _rbr.StopOnHit,
			TerminateTrailOnHit = _rbr.TerminateTrailOnHit,
			UpdateEveryFrame = UpdateEveryFrame
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

	private void MaybePrintSoftGateConfigSnapshot()
	{
		SoftGateConfigSnapshot cur = new SoftGateConfigSnapshot
		{
			Pass2SoftGateEnableQuickRayMiss = Pass2SoftGateEnableQuickRayMiss,
			Pass2SoftGateScoringEnabled = Pass2SoftGateScoringEnabled,
			Pass2SoftGateMinSegmentLength = Pass2SoftGateMinSegmentLength,
			Pass2SoftGateScoreThreshold = Pass2SoftGateScoreThreshold,
			Pass2SoftGateScoreTurnAngleWeight = Pass2SoftGateScoreTurnAngleWeight,
			Pass2SoftGateScorePrevHitLostBonus = Pass2SoftGateScorePrevHitLostBonus,
			Pass2SoftGateRandomProbeChance = Pass2SoftGateRandomProbeChance,
			Pass2SoftGateScoreBudgetPerFrame = Pass2SoftGateScoreBudgetPerFrame,
			Pass2SoftGateMaxAttemptsPerPixel = Pass2SoftGateMaxAttemptsPerPixel,
			Pass2SoftGateMaxAttemptsPerFrame = Pass2SoftGateMaxAttemptsPerFrame,
			Pass2SoftGateMaxSubdividedCallsPerFrame = Pass2SoftGateMaxSubdividedCallsPerFrame,
			UpdateEveryFrame = UpdateEveryFrame
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
		UseFieldSourceCache = true;
		UseBroadphasePolicy = true;
		BroadphasePolicy = BroadphaseMode.QuickRayOnly;
		TinySegmentSkipLen = 0.005f;
		EarlyOutDistanceEps = 0.01f;
		NeedColliderNames = false;
		ApplyQualityModePresetIfNeeded("preset");
	}

	public void ResetFilmPassManual()
	{
		ResetRowCursor("manual");
	}

	public void ApplyPreset(PresetMode mode)
	{
		switch (mode)
		{
			case PresetMode.Walk:
				FilmResolutionScale = 0.5f;
				PixelStride = 2;
				RowsPerFrame = 16;
				DebugEveryNPixels = 16;
				DebugMaxFilmRays = 512;
				UseBroadphasePolicy = true;
				BroadphasePolicy = BroadphaseMode.QuickRayOnly;
				UseBroadphaseQuickRay = true;
				UseBroadphaseOverlap = false;
				break;
			case PresetMode.Cinematic:
				FilmResolutionScale = 1.0f;
				PixelStride = 1;
				RowsPerFrame = 4;
				DebugEveryNPixels = 4;
				DebugMaxFilmRays = 4096;
				UseBroadphasePolicy = true;
				BroadphasePolicy = BroadphaseMode.Both;
				UseBroadphaseQuickRay = true;
				UseBroadphaseOverlap = true;
				break;
			default:
			case PresetMode.Preview:
				FilmResolutionScale = 1.0f;
				PixelStride = 1;
				RowsPerFrame = 8;
				DebugEveryNPixels = 8;
				DebugMaxFilmRays = 2048;
				UseBroadphasePolicy = true;
				BroadphasePolicy = BroadphaseMode.QuickRayOnly;
				UseBroadphaseQuickRay = true;
				UseBroadphaseOverlap = false;
				break;
		}
		ApplyQualityModePresetIfNeeded("preset");
	}

	public void ApplyPerfPresetQuality()
	{
		UseFieldSourceCache = false;
		UseBroadphasePolicy = false;
		TinySegmentSkipLen = 0.0f;
		EarlyOutDistanceEps = 0.0f;
		NeedColliderNames = false;
		ApplyQualityModePresetIfNeeded("preset");
	}
	public void ApplyQualityModePresetIfNeeded(string reason)
	{
		bool forceApply = reason == "ready" || reason == "preset";
		bool modeChanged = _appliedQualityMode != QualityMode;
		if (!modeChanged && !forceApply) return;

		switch (QualityMode)
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

		if (modeChanged)
		{
			GD.Print(
				$"[QualityMode] applied mode={QualityMode} reason={reason} settings: " +
				$"filmScale={FilmResolutionScale:0.###} pixelStride={PixelStride} rowsPerFrame={RowsPerFrame} " +
				$"targetMs={TargetMsPerFrame} maxRowsCap={MaxRowsPerFrameCap} " +
				$"UpdateEveryFrameBudgetMs={UpdateEveryFrameBudgetMs:0.###} RenderStepMaxMs={RenderStepMaxMs} " +
				$"softgate{{score={Pass2SoftGateScoreBudgetPerFrame} px={Pass2SoftGateMaxAttemptsPerPixel} " +
				$"frame={Pass2SoftGateMaxAttemptsPerFrame} sub={Pass2SoftGateMaxSubdividedCallsPerFrame}}}");
		}
		_appliedQualityMode = QualityMode;
	}

	void UpdateFilmOpacity()
	{
		var target = _filmView ?? _overlayRect;
		if (target == null) return;
		target.Modulate = new Color(1, 1, 1, FilmOpacity);
	}

}
