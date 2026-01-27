using Godot;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using XPrimeRay.Perf; // adjust namespace new PerfScope.cs

public partial class GrinFilmCamera : Node
{

	[ExportCategory("Film Camera")]
#region Pass2 SoftGate
	[ExportGroup("Physics / Collision / Pass2 SoftGate")]
	[ExportSubgroup("Core")]
	/// <summary>Allows occasional subdivide attempts on quick-ray misses (Pass2).</summary>
	[Export] public bool Pass2SoftGateEnableQuickRayMiss = false;
	/// <summary>Use RayBeamRenderer step sizing to scale Pass2 SoftGate thresholds.</summary>
	[Export] public bool Pass2SoftGateUseRayBeamSettings = true;
	/// <summary>Disable SoftGate for the rest of the frame when overload is detected.</summary>
	[Export] public bool DisableSoftGateOnOverload = true;
	/// <summary>Minimum segment length in steps when using RayBeam settings (leave default unless you are tuning SoftGate).</summary>
	[Export] public float Pass2SoftGateMinSegLenSteps = 2.0f;

	[ExportSubgroup("Budget")]
	/// <summary>Max soft-gate attempts per pixel (Pass2). 0 disables.</summary>
	[Export(PropertyHint.Range, "0,8,1")] public int Pass2SoftGateMaxAttemptsPerPixel = 2;
	/// <summary>Max soft-gate attempts per frame (Pass2). 0 disables; raise only when profiling.</summary>
	[Export(PropertyHint.Range, "0,100000,1")] public int Pass2SoftGateMaxAttemptsPerFrame = 5000;
	/// <summary>Auto-scaled max soft-gate attempts per frame lower bound when using RayBeam settings.</summary>
	[Export(PropertyHint.Range, "0,100000,1")] public int Pass2SoftGateMaxAttemptsPerFrameMin = 20;
	/// <summary>Auto-scaled max soft-gate attempts per frame upper bound when using RayBeam settings.</summary>
	[Export(PropertyHint.Range, "0,100000,1")] public int Pass2SoftGateMaxAttemptsPerFrameMax = 5000;
	/// <summary>Max soft-gated subdivided calls per frame (Pass2). 0 disables; higher values can stall frames.</summary>
	[Export(PropertyHint.Range, "0,200000,1")] public int Pass2SoftGateMaxSubdividedCallsPerFrame = 10000;
	/// <summary>Auto-scaled max soft-gated subdivided calls per frame lower bound when using RayBeam settings.</summary>
	[Export(PropertyHint.Range, "0,200000,1")] public int Pass2SoftGateMaxSubdividedCallsPerFrameMin = 50;
	/// <summary>Auto-scaled max soft-gated subdivided calls per frame upper bound when using RayBeam settings.</summary>
	[Export(PropertyHint.Range, "0,200000,1")] public int Pass2SoftGateMaxSubdividedCallsPerFrameMax = 10000;
	/// <summary>Watchdog timeout (ms) for a single soft-gated subdivide (Pass2). 0 disables.</summary>
	[Export(PropertyHint.Range, "0,50,0.1")] public float Pass2SoftGateWatchdogMs = 5f;
	/// <summary>Max watchdog logs per frame when Pass2SoftGateDebugEnabled is enabled.</summary>
	[Export(PropertyHint.Range, "0,32,1")] public int Pass2SoftGateWatchdogLogLimitPerFrame = 4;

	[ExportSubgroup("Scoring")]
	/// <summary>Legacy cadence gate for soft-gated subdivides (Pass2). Unused.</summary>
	[Obsolete("Legacy soft-gate cadence (unused). Use Pass2SoftGateScoreThreshold + scoring model instead.")]
	public int Pass2SoftGateLegacyEveryNSegments = 8;
	/// <summary>Legacy length gate for soft-gated subdivides (Pass2). Unused.</summary>
	[Obsolete("Legacy soft-gate min segment length (unused). Use Pass2SoftGateMinSegmentLength instead.")]
	public float Pass2SoftGateLegacyMinSegmentLength = 0f;

	/// <summary>Enable scoring-based soft-gate (Pass2).</summary>
	[Export] public bool Pass2SoftGateScoringEnabled = true;
	/// <summary>Maximum scoring soft-gate attempts allowed per frame (Pass2).</summary>
	[Export] public int Pass2SoftGateScoreBudgetPerFrame = 32;
	/// <summary>Minimum segment length eligible for scoring soft-gate (Pass2).</summary>
	[Export] public float Pass2SoftGateMinSegmentLength = 0.2f;
	/// <summary>Score threshold required to trigger scoring soft-gate (Pass2). Adjust only with debug summaries.</summary>
	[Export] public float Pass2SoftGateScoreThreshold = 1.0f;
	/// <summary>Weight for turn-angle contribution (scaled by 0..180 deg).</summary>
	[Export] public float Pass2SoftGateScoreTurnAngleWeight = 1.0f;
	/// <summary>Extra score added when a previous-frame hit was lost.</summary>
	[Export] public float Pass2SoftGateScorePrevHitLostBonus = 0.75f;
	/// <summary>Random chance to probe even when score is below threshold.</summary>
	[Export] public float Pass2SoftGateRandomProbeChance = 0.01f;

	[ExportSubgroup("Debug")]
	/// <summary>Enables soft-gate debug counters and logging (Pass2).</summary>
	[Export] public bool Pass2SoftGateDebugEnabled = true;
	/// <summary>SoftGate debug verbosity (0=off, 1=frame, 2=band, 3=sampled segments).</summary>
	[Export(PropertyHint.Range, "0,3,1")] public int Pass2SoftGateDebugVerbosity = 1;
	/// <summary>Prints a compact debug summary per frame (Pass2).</summary>
	[Export] public bool Pass2SoftGateDebugSummaryPerFrame = false;
	/// <summary>Max debug summary logs per frame when enabled.</summary>
	[Export(PropertyHint.Range, "0,8,1")] public int Pass2SoftGateDebugSummaryLogLimitPerFrame = 1;
#endregion

	[ExportGroup("References")]
	/// <summary>NodePath to the RayBeamRenderer used for film segment generation.</summary>
	[Export] public NodePath RayBeamRendererPath;
	/// <summary>Optional TextureRect used to display the film texture.</summary>
	[Export] public NodePath FilmViewPath;
	/// <summary>Optional FilmOverlay2D for debug ray overlay.</summary>
	[Export] public NodePath FilmOverlayPath;

	[ExportGroup("General")]
	/// <summary>Runs RenderStep every frame when enabled.</summary>
	[Export] public bool UpdateEveryFrame = true;
	/// <summary>Hard time budget for RenderStep (ms). Exceeding this disables UpdateEveryFrame.</summary>
	[Export] public int RenderStepMaxMs = 50;
	/// <summary>Hard cap on RenderStep pixel workload per frame. 0 disables.</summary>
	[Export] public int RenderStepMaxPixelsPerFrame = 2000000;
	/// <summary>Hard cap on RenderStep segments per frame. 0 disables.</summary>
	[Export] public int RenderStepMaxSegmentsPerFrame = 20000000;

	[ExportCategory("Film Camera")]
	[ExportGroup("Rendering / Film Output")]
	/// <summary>Base film width in pixels before scaling.</summary>
	[Export] public int Width = 160;
	/// <summary>Base film height in pixels before scaling.</summary>
	[Export] public int Height = 90;
	/// <summary>Scales film resolution (0.25 to 1.0).</summary>
	[Export(PropertyHint.Range, "0.25,1.0,0.01")] public float FilmResolutionScale = 1.0f;
	/// <summary>Traces every Nth pixel and fills stride-sized blocks.</summary>
	[Export(PropertyHint.Range, "1,8,1")] public int PixelStride = 1;

	public enum PresetMode
	{
		Walk = 0,
		Preview = 1,
		Cinematic = 2
	}

	[ExportGroup("Performance / Profiling")]
	/// <summary>Preset selection for tuning.</summary>
	[Export] public PresetMode Preset = PresetMode.Preview;
	/// <summary>Apply the preset automatically in _Ready.</summary>
	[Export] public bool ApplyPresetOnReady = false;
	/// <summary>Enables perf stats collection.</summary>
	[Export] public bool EnableProfiling = true;
	/// <summary>Prints verbose perf logs per band.</summary>
	[Export] public bool VerbosePerfLogs = false;
	/// <summary>Enables FramePerf stage timing and counters.</summary>
	[Export] public bool EnableFramePerf = true;
	/// <summary>Prints FramePerf every frame when enabled.</summary>
	[Export] public bool FramePerfVerbose = false;
	/// <summary>Frames between FramePerf logs when not verbose.</summary>
	[Export] public int FramePerfLogEveryNFrames = 30;
	/// <summary>Fetches collider names for debug output.</summary>
	[Export] public bool NeedColliderNames = false;
	/// <summary>Caches field source snapshots for faster updates.</summary>
	[Export] public bool UseFieldSourceCache = false;
	/// <summary>How often to refresh cached field sources.</summary>
	[Export] public int FieldSourceRefreshIntervalFrames = 30;

	[ExportGroup("Field Grid")]
	/// <summary>Uses a cached 3D vector field grid for pass-1 sampling.</summary>
	[Export] public bool UseFieldGrid = false;
	/// <summary>Cell size for field grid sampling.</summary>
	[Export] public float FieldGridCellSize = 0.25f;
	/// <summary>Rebuild the field grid every N frames.</summary>
	[Export] public int FieldGridRebuildEveryNFrames = 8;
	/// <summary>Padding added to far distance for grid bounds.</summary>
	[Export] public float FieldGridBoundsPadding = 5f;

	[ExportGroup("Rendering / Film Output")]
	/// <summary>Number of film rows rendered per frame.</summary>
	[Export] public int RowsPerFrame = 8;
	/// <summary>Target CPU time budget per RenderStep (ms). Set <=0 to disable adaptive rows.</summary>
	[Export] public int TargetMsPerFrame = 16;
	/// <summary>Minimum rows per frame when adaptive rows are enabled.</summary>
	[Export] public int MinRowsPerFrame = 4;
	/// <summary>Maximum rows per frame when adaptive rows are enabled.</summary>
	[Export] public int MaxRowsPerFrameCap = 256;
	/// <summary>Max ray distance when auto-range is disabled.</summary>
	[Export] public float MaxDistance = 50f;
	/// <summary>Background color for no-hit pixels.</summary>
	[Export] public Color SkyColor = new Color(0, 0, 0, 1);
	/// <summary>Opacity applied to the film TextureRect.</summary>
	[Export] public float FilmOpacity = 0.7f;

	[ExportGroup("Ray March / Sampling")]
	/// <summary>Reads Beta/Gamma from the active Camera3D.</summary>
	[Export] public bool UseCameraPropsBetaGamma = true;
	/// <summary>Skips collision checks for tiny segments.</summary>
	[Export] public float TinySegmentSkipLen = 0.0f;
	/// <summary>Early-out distance for nearest-hit search.</summary>
	[Export] public float EarlyOutDistanceEps = 0.0f;
	/// <summary>Refines collision checks by subdividing segments.</summary>
	[Export] public bool UseAdaptiveSubsteps = false;
	/// <summary>Skips physics for low-hit bands.</summary>
	[Export] public bool UseBandHitSkip = false;
	/// <summary>Hit rate threshold to enable skipping.</summary>
	[Export] public float BandSkipHitThreshold = 0.001f;
	/// <summary>Frames below threshold before skipping.</summary>
	[Export] public int BandSkipFrames = 3;
	/// <summary>Position delta that invalidates band skip history.</summary>
	[Export] public float BandSkipInvalidatePosDelta = 0.05f;
	/// <summary>Basis delta that invalidates band skip history.</summary>
	[Export] public float BandSkipInvalidateBasisDelta = 0.02f;
	/// <summary>Range delta that invalidates band skip history.</summary>
	[Export] public float BandSkipInvalidateRangeDelta = 0.25f;
	/// <summary>Enables pass-1 hit tests.</summary>
	[Export] public bool Pass1DoHitTest = true;
	/// <summary>Runs a pass-1 probe every N steps (0 disables; independent of segment emission cadence).</summary>
	[Export] public int Pass1ProbeEveryNSegments = 4;
	/// <summary>Minimum travel distance between pass-1 probes (<=0 disables).</summary>
	[Export] public float Pass1ProbeMinTravelDelta = 0.25f;

	[ExportGroup("Auto Range / Depth")]
	/// <summary>Auto-adjusts depth range based on recent hits.</summary>
	[Export] public bool AutoRangeDepth = true;
	/// <summary>Minimum allowed auto-range far distance.</summary>
	[Export] public float AutoRangeMin = 0.25f;
	/// <summary>Maximum allowed auto-range far distance.</summary>
	[Export] public float AutoRangeMax = 200f;
	/// <summary>Lerp factor for auto-range updates.</summary>
	[Export] public float AutoRangeSmoothing = 0.15f;
	/// <summary>Safety multiplier for robust far estimate.</summary>
	[Export] public float AutoRangeSafety = 1.15f;
	/// <summary>Frames tracked for robust far estimate.</summary>
	[Export] public int DepthHistoryFrames = 30;

	[ExportGroup("Physics / Collision")]
	/// <summary>Enables a quick raycast broadphase test.</summary>
	[Export] public bool UseBroadphaseQuickRay = true;
	/// <summary>Enables a sphere overlap broadphase test.</summary>
	[Export] public bool UseBroadphaseOverlap = false;
	/// <summary>Extra radius for overlap broadphase.</summary>
	[Export] public float BroadphaseMargin = 0.03f;
	/// <summary>Max overlap results to consider.</summary>
	[Export] public int BroadphaseMaxResults = 8;
	/// <summary>Skips some pass-2 collision checks based on distance.</summary>
	[Export] public bool UsePass2CollisionStride = false;
	/// <summary>Stride near the camera for pass-2 collision checks.</summary>
	[Export(PropertyHint.Range, "1,8,1")] public int Pass2CollisionStrideNear = 1;
	/// <summary>Stride at far distances for pass-2 collision checks.</summary>
	[Export(PropertyHint.Range, "1,32,1")] public int Pass2CollisionStrideFar = 4;
	/// <summary>Start t (0..1) where far stride begins in pass 2.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float Pass2CollisionStrideFarStartT = 0.35f;
	/// <summary>If >0, segments shorter than this length always run pass-2 collision tests.</summary>
	[Export(PropertyHint.Range, "0,1,0.001")] public float MinSegLenForStrideSkip = 0f;
	/// <summary>Ray query option: include back-facing triangles in pass-2 checks.</summary>
	[Export] public bool Pass2HitBackFaces = false;
	/// <summary>Ray query option: detect hits when starting inside colliders.</summary>
	[Export] public bool Pass2HitFromInside = true;
	/// <summary>Forces a representative subdivided test when quick-ray misses all candidate segments.</summary>
	[Export] public bool Pass2ForceOnInstability = false;
	/// <summary>Only forces instability tests when the pixel hit in the previous frame.</summary>
	[Export] public bool Pass2ForceIfPrevHitLost = false;
	/// <summary>Logs quick-ray misses that later subdivide and hit (per frame).</summary>
	[Export] public int Pass2LogQuickRayMissSamples = 0;
	public enum BroadphaseMode
	{
		None = 0,
		QuickRayOnly = 1,
		OverlapOnly = 2,
		Both = 3
	}

	/// <summary>Overrides broadphase toggles using BroadphasePolicy.</summary>
	[Export] public bool UseBroadphasePolicy = false;
	/// <summary>Broadphase policy when UseBroadphasePolicy is true.</summary>
	[Export] public BroadphaseMode BroadphasePolicy = BroadphaseMode.QuickRayOnly;
	/// <summary>Uses a quick probe, then subdivides if needed.</summary>
	[Export] public bool UseSingleProbeThenSubdivide = false;
	/// <summary>If true, keeps scanning segments for the nearest hit.</summary>
	[Export] public bool NearestHitOnly = true;

	public enum FilmShadingMode
	{
		DepthHeatmap = 0,   // your current behavior
		NormalRGB = 1,      // (N*0.5 + 0.5)
		NdotV = 2,          // grayscale: saturate(dot(N, V))
		TwoSidedNdotV = 3,  // grayscale: saturate(abs(dot(N, V)))
	}

	[ExportGroup("Rendering / Film Output")]
	/// <summary>Film shading mode (depth, normal RGB, NdotV).</summary>
	[Export] public FilmShadingMode ShadingMode = FilmShadingMode.DepthHeatmap;
	// Note: overlay normals are world-space collision normals (physics mesh).
	// Film distortion is a visualization artifact and does not change collider geometry.
	// For film-surface normals, use a screen-space gradient (see FilmOverlay2D) or a ray-space curvature normal; physics will not provide it.
	/// <summary>Flips hit normals to face the camera for shading.</summary>
	[Export] public bool FlipNormalToCamera = true;

	[ExportGroup("Debug Visualization")]
	/// <summary>Debug ray sampling density for overlay.</summary>
	[Export] public int DebugEveryNPixels = 8;
	/// <summary>Cap on debug rays per band.</summary>
	[Export] public int DebugMaxFilmRays = 2048;
	[ExportGroup("Deprecated (No Effect)")]
	/// <summary>Legacy pass-2 insight plane toggle (no effect).</summary>
	[Obsolete("Deprecated: no effect in current film pass.")]
	public bool UseInsightPlanePass2 = true;
	/// <summary>Legacy insight plane slab thickness (no effect).</summary>
	[Obsolete("Deprecated: no effect in current film pass.")]
	public float InsightPlaneEps = 0.10f;
	/// <summary>Placeholder for future normal smoothing (unused).</summary>
	[Obsolete("Deprecated: reserved for future normal smoothing.")]
	public bool UseSmoothNormals = false;


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
	private Camera3D _cam;
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
	private RandomNumberGenerator _rng = new RandomNumberGenerator();
	private volatile int _renderStepActive = 0;
	private bool _renderStepReentryWarned = false;



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
		GD.PrintErr($"⛔ RenderStep early-out: {why} rowCursor={_rowCursor} cam={_cam?.GetPath()} rbr={_rbr?.GetPath()}");
	}



	public override void _Ready()
	{
		GD.Print("✅ GrinFilmCamera READY: ", GetPath());

		_cam = GetViewport().GetCamera3D();
		if (_cam == null)
		{
			GD.PushError("GrinFilmCamera: No active Camera3D found in viewport.");
			return;
		}

		_rbr = GetNodeOrNull<RayBeamRenderer>(RayBeamRendererPath);
		GD.Print("RayBeamRenderer found? ", _rbr != null);
		if (_rbr == null)
		{
			GD.PushError("GrinFilmCamera: RayBeamRendererPath missing or invalid.");
			return;
		}

    	_rng.Randomize();

		if (ApplyPresetOnReady)
		{
			ApplyPreset(Preset);
		}

    	// ⛔ Freeze beam rebuilds while film camera is active
		_rbr.AllowRebuild = false;

		_filmView = GetNodeOrNull<TextureRect>(FilmViewPath);
		GD.Print("FilmView found? ", _filmView != null);

		// Create image + texture
		EnsureFilmImageSize();

		// If FilmViewPath is set, use it.
		if (_filmView != null)
		{
			_filmView.Texture = _tex;
		}
		else
		{
			// Otherwise auto-create an overlay.
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
		if (!UpdateEveryFrame) return;
		RenderStep();
	}

	public void RenderStep()
	{
		if (Interlocked.CompareExchange(ref _renderStepActive, 1, 0) != 0)
		{
			if (!_renderStepReentryWarned)
			{
				_renderStepReentryWarned = true;
				GD.PrintErr($"[RenderStep][Guard] re-entry blocked. frame={_frameIndex} row={_rowCursor} cam={_cam?.GetPath()} rbr={_rbr?.GetPath()}");
			}
			UpdateEveryFrame = false;
			return;
		}

		Stopwatch renderStepWatch = Stopwatch.StartNew();
		bool renderStepAbort = false;
		bool renderStepAbortLogged = false;
		bool softGateDisabledThisFrame = false;
		string softGateDisableReason = "";
		bool softGateDisableLogged = false;
		bool statsEnabled = false;
		bool framePerfEnabled = false;
		bool frameStart = false;
		PerfScope frameScope = default;

		try
		{
			ulong t0 = Time.GetTicksUsec();
			statsEnabled = EnableProfiling || VerbosePerfLogs;
			framePerfEnabled = EnableFramePerf;
			frameStart = _rowCursor == 0;
			if (framePerfEnabled) frameScope = new PerfScope(_framePerf, PerfStage.FrameTotal);

		// Soft-gate debug toggles
		/////////////////////////////
		bool softGateDebugEnabled = Pass2SoftGateDebugEnabled && Pass2SoftGateDebugVerbosity > 0;
		bool softGateBandEnabled = softGateDebugEnabled && Pass2SoftGateDebugVerbosity >= 2;
		bool softGateSegEnabled = softGateDebugEnabled && Pass2SoftGateDebugVerbosity >= 3;
		/////////////////////////////

			bool resizedFilm = EnsureFilmImageSize();
			int filmW = _filmWidth;
			int filmH = _filmHeight;
			int stride = Mathf.Clamp(PixelStride, 1, 8);
			long tracedPixels = (long)filmW * filmH / Math.Max(1, stride * stride);

			float pass2SoftGateMinSegmentLengthEffective = Pass2SoftGateMinSegmentLength;
			int pass2SoftGateMaxAttemptsPerFrameEffective = Pass2SoftGateMaxAttemptsPerFrame;
			int pass2SoftGateMaxSubdividedCallsPerFrameEffective = Pass2SoftGateMaxSubdividedCallsPerFrame;
			float pass2SoftGateEffStepLen = 0f;
			bool pass2SoftGateUseRayBeamSettingsActive = false;

			if (Pass2SoftGateUseRayBeamSettings)
			{
				if (_rbr != null)
				{
					float stepLength = _rbr.StepLength;
					float minStepLength = _rbr.MinStepLength;
					float maxStepLength = _rbr.MaxStepLength;
					float stepAdaptGain = _rbr.StepAdaptGain;
					bool stepsFinite = float.IsFinite(stepLength)
						&& float.IsFinite(minStepLength)
						&& float.IsFinite(maxStepLength)
						&& float.IsFinite(stepAdaptGain);
					if (stepsFinite)
					{
						float minStep = Mathf.Min(minStepLength, maxStepLength);
						float maxStep = Mathf.Max(minStepLength, maxStepLength);
						pass2SoftGateEffStepLen = Mathf.Clamp(stepLength, minStep, maxStep);
						pass2SoftGateMinSegmentLengthEffective = Pass2SoftGateMinSegLenSteps * pass2SoftGateEffStepLen;

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

				if (!pass2SoftGateUseRayBeamSettingsActive)
				{
					// Silent fallback to manual settings when RayBeam parameters are unavailable.
				}
			}

			void LogRenderPhase(string phase)
			{
				GD.Print(
					$"[RenderStep] phase={phase} frame={_frameIndex} row={_rowCursor} " +
					$"attempts={_softGateAttemptsUsedThisFrame}/{pass2SoftGateMaxAttemptsPerFrameEffective} " +
					$"sub={_softGateSubdividedCallsUsedThisFrame}/{pass2SoftGateMaxSubdividedCallsPerFrameEffective} " +
					$"pxCap={Pass2SoftGateMaxAttemptsPerPixel} scoreCap={Pass2SoftGateScoreBudgetPerFrame} " +
					$"ms={renderStepWatch.ElapsedMilliseconds}");
			}

			bool CheckRenderStepWatchdog()
			{
				if (RenderStepMaxMs <= 0) return false;
				if (renderStepWatch.ElapsedMilliseconds <= RenderStepMaxMs) return false;
				if (!renderStepAbort)
				{
					renderStepAbort = true;
					if (DisableSoftGateOnOverload)
						DisableSoftGateThisFrame("renderstep_watchdog");
				}
				return true;
			}

			void AbortRenderStep(string reason)
			{
				if (renderStepAbortLogged) return;
				renderStepAbortLogged = true;
				UpdateEveryFrame = false;
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
				GD.PrintErr(
					$"[RenderStep][Abort] reason={reason} frame={_frameIndex} row={_rowCursor} " +
					$"ms={renderStepWatch.ElapsedMilliseconds}");
			}

			void DisableSoftGateThisFrame(string reason)
			{
				if (softGateDisabledThisFrame) return;
				softGateDisabledThisFrame = true;
				softGateDisableReason = reason;
			}

			if (frameStart)
			{
				_frameIndex++;
				if (statsEnabled)
				{
					_perfFrame.Reset();
					_perfFrame.RequireHitToRender = _rbr != null && _rbr.RequireHitToRender;
					_perfFrame.EffectiveStride = stride;
					_perfFrame.EffectiveWidth = filmW;
					_perfFrame.EffectiveHeight = filmH;
					_perfFrame.EffectiveRenderPixels = (int)tracedPixels;
				}else{}
				if (framePerfEnabled)
				{
					_framePerf.Reset();
					_framePerf.FrameIndex = _frameIndex;
				}else{}
				
				// Soft-gate frame counters
				/////////////////////////////
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
			if (statsEnabled && resizedFilm)
			{
				_perfFrame.ResizedFilm = true;
			}

			if (_rbr == null)
			{
				AbortRenderStep("No RayBeamRenderer assigned");
				return;
			} else{}

			if (_cam == null) {
				AbortRenderStep("No active Camera3D in viewport");
				return;
			} else{}

			if (frameStart)
			{
				MaybePrintToggleSnapshot();
				MaybePrintSoftGateConfigSnapshot();
			}

			var space = _cam.GetWorld3D().DirectSpaceState;

			var fieldSnaps = GetFieldSourceSnaps(_frameIndex, out bool hasSources, out bool cacheRefreshed);
			if (framePerfEnabled && frameStart && UseFieldSourceCache)
			{
				if (cacheRefreshed) _framePerf.CacheMisses++;
				else _framePerf.CacheHits++;
			}

			if (VerbosePerfLogs && (_rowCursor % filmH) == 0)
				GD.Print($"fieldSnaps={fieldSnaps.Length} hasSources={hasSources}");


			float beta = 0f;
			float gamma = 2f;
			if (UseCameraPropsBetaGamma)
			{
				beta = ReadFloat(_cam, "Beta", 0f);
				gamma = ReadFloat(_cam, "Gamma", 2f);
			}

			Vector3 center = _rbr.FieldCenterIsCamera ? _cam.GlobalPosition : _rbr.FieldCenter;
			var basis = _cam.GlobalTransform.Basis;

			float fovRad = Mathf.DegToRad(_cam.Fov);
			float tanHalf = Mathf.Tan(fovRad * 0.5f);
			float aspect = (float)filmW / Mathf.Max(1f, filmH);

			int yStart = _rowCursor;
			int baseRowsPerFrame = Mathf.Clamp(RowsPerFrame, Mathf.Max(1, MinRowsPerFrame), filmH);
			int maxRowsPerFrame = Mathf.Clamp(MaxRowsPerFrameCap, Mathf.Max(1, MinRowsPerFrame), filmH);
			if (TargetMsPerFrame <= 0 || _adaptiveRowsPerFrame <= 0)
				_adaptiveRowsPerFrame = baseRowsPerFrame;
			int rowsPerFrame = Mathf.Clamp(_adaptiveRowsPerFrame, Mathf.Max(1, MinRowsPerFrame), maxRowsPerFrame);
			if (rowsPerFrame != _adaptiveRowsPerFrame)
				_adaptiveRowsPerFrame = rowsPerFrame;
			int yEnd = Mathf.Min(filmH, _rowCursor + rowsPerFrame);
			int bandH = yEnd - yStart;
			int pixelCount = bandH * filmW;
			if (RenderStepMaxPixelsPerFrame > 0 && pixelCount > RenderStepMaxPixelsPerFrame)
			{
				AbortRenderStep($"max-pixels {pixelCount}>{RenderStepMaxPixelsPerFrame}");
				return;
			}

			EnsureDepthHistory();
			float frameMaxHit = 0f; // track deepest hit this RenderStep band

			int bandHits = 0;
			int maxSeg = MaxSegPerRay;
			float farForSim = AutoRangeDepth ? _rangeFar : MaxDistance;

			// Soft-gate band counters
			/////////////////////////////
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
			if (UseFieldGrid && _rbr.UseIntegratedField && hasSources)
			{
				int rebuildN = Mathf.Max(1, FieldGridRebuildEveryNFrames);
				bool shouldRebuild = cacheRefreshed || _fieldGrid == null || (_frameIndex % rebuildN) == 0;
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
			if (UseBandHitSkip)
			{
				EnsureBandHitHistory(filmH, rowsPerFrame);
				bandIndex = yStart / rowsPerFrame;

				if (CheckAndUpdateBandInvalidation(_cam.GlobalTransform, farForSim))
					ResetBandHitHistory();

				if (bandIndex >= 0 && bandIndex < _bandHitRate.Length && BandSkipFrames > 0)
				{
					if (_bandLowHitFrames[bandIndex] >= BandSkipFrames && _bandHitRate[bandIndex] < BandSkipHitThreshold)
						skipBandPhysics = true;
				}
			}

			// allocate / reuse buffers
			int segTotal = pixelCount * maxSeg;
			if (RenderStepMaxSegmentsPerFrame > 0 && segTotal > RenderStepMaxSegmentsPerFrame)
			{
				AbortRenderStep($"max-segs {segTotal}>{RenderStepMaxSegmentsPerFrame}");
				return;
			}
			_segBuf ??= new RayBeamRenderer.RaySeg[segTotal];
			if (_segBuf.Length < segTotal) _segBuf = new RayBeamRenderer.RaySeg[segTotal];

			_segCountPerPixel ??= new int[pixelCount];
			if (_segCountPerPixel.Length < pixelCount) _segCountPerPixel = new int[pixelCount];
			if (_pass1HitFound.Length < pixelCount) _pass1HitFound = new bool[pixelCount];
			if (_pass1StoppedEarly.Length < pixelCount) _pass1StoppedEarly = new bool[pixelCount];
			if (_pass1HitSegIndex.Length < pixelCount) _pass1HitSegIndex = new int[pixelCount];
			if (_pass1HitDist.Length < pixelCount) _pass1HitDist = new float[pixelCount];
			if (_pass1HitPos.Length < pixelCount) _pass1HitPos = new Vector3[pixelCount];
			if (_pass1HitNormal.Length < pixelCount) _pass1HitNormal = new Vector3[pixelCount];
			if (_pass1HitColliderId.Length < pixelCount) _pass1HitColliderId = new ulong[pixelCount];

			///  Debug code block drop
			_dbgRayCount = 0;
			_dbgPtWrite = 0;
			// Only build debug overlay if enabled
			bool wantDbg = (_rbr != null
				&& _rbr.DebugMode != RayBeamRenderer.DebugDrawMode.Off
				&& _rbr.DebugOverlayOwnedByFilm);
			// Rough upper bounds for this band (for capacity planning)
			// We’ll only sample 1 out of DebugEveryNPixels pixels.
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

			if (UseInsightPlanePass2 && _rbr.UseInsightPlaneFilter)
			{
				// easiest v0: rebuild plane here from a NodePath you expose, OR if _rbr has the plane cached, add a getter.
				// For now (if you don't have a getter), just leave this false until we wire it.
				// useInsightPlane = true; insightPlane = ...;
			}

			if (_rbr.UseInsightPlaneFilter)
			{
				// RayBeamRenderer already computed plane in rebuild, but for film we can just disable
				// OR if you want it: add a public getter in RayBeamRenderer for current plane/flag.
				// For now: keep it off in film threading unless you wire it.
				useInsightPlane = false;
			}

			// ---- PASS 1 (workers): build segments for each pixel ----
			//int jobs = Mathf.Clamp(OS.GetProcessorCount(), 2, 16);
			int jobs = Mathf.Clamp(OS.GetProcessorCount() / 2, 2, 8);

			var basisLocal = basis; // capture for lambda
			Vector3 camPos = _cam.GlobalPosition;

			ulong a0 = Time.GetTicksUsec(); // before Parallel.For
			LogRenderPhase("pass1-start");

			PerfScope pass1Scope = default;
			if (framePerfEnabled) pass1Scope = new PerfScope(_framePerf, PerfStage.Pass1_Integrate);

			bool pass1StopOnHit = _rbr.StopOnHit || _rbr.TerminateTrailOnHit || _rbr.RequireHitToRender;
			long pass1PhysQueries = 0;
			long pass1EarlyStopPixels = 0;
			long pass1StepsIntegrated = 0;
			long pass1FieldEvals = 0;
			long pass1Raycasts = 0;
			long pass1ProbeHits = 0;
			long pass1FieldGridHits = 0;
			long pass1FieldGridMisses = 0;
			bool collectPass1Perf = framePerfEnabled;
			bool collectPass1Steps = framePerfEnabled || VerbosePerfLogs;

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

					Vector3 dirWorld = (basisLocal * dirCam).Normalized();
					Vector3 bendDir = basisLocal.X;

					int segOffset = pi * maxSeg;

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

					if (collectPass1Perf)
					{
						local.PhysQueries += pass1RaycastsLocal;
						if (stoppedEarly) local.EarlyStopPixels++;
					}
					if (collectPass1Steps) local.StepsIntegrated += stepsIntegrated;
					if (framePerfEnabled) local.FieldEvals += fieldEvals;
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
					if (collectPass1Perf)
					{
						Interlocked.Add(ref pass1PhysQueries, local.PhysQueries);
						Interlocked.Add(ref pass1EarlyStopPixels, local.EarlyStopPixels);
					}
					if (collectPass1Steps) Interlocked.Add(ref pass1StepsIntegrated, local.StepsIntegrated);
					if (framePerfEnabled) Interlocked.Add(ref pass1FieldEvals, local.FieldEvals);
					if (framePerfEnabled)
					{
						Interlocked.Add(ref pass1Raycasts, local.Pass1Raycasts);
						Interlocked.Add(ref pass1ProbeHits, local.Pass1ProbeHits);
						Interlocked.Add(ref pass1FieldGridHits, local.FieldGridHits);
						Interlocked.Add(ref pass1FieldGridMisses, local.FieldGridMisses);
					}
				});

			if (framePerfEnabled) pass1Scope.Dispose();
			LogRenderPhase("pass1-end");
			if (CheckRenderStepWatchdog())
			{
				AbortRenderStep("watchdog");
				return;
			}

			ulong a1 = Time.GetTicksUsec(); // after wait

			if (statsEnabled)
			{
				_perfFrame.AddPass1Usec(a1 - a0);
				_perfFrame.Pixels += pixelCount;
			}
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

			// ---- PASS 2 (main thread): collisions + shading ----
			LogRenderPhase("pass2-start");
			bandHits = 0;
			int bandTracedPixels = 0;
			long shadeUsecAccum = 0;
			long bandSegsIntegrated = 0;
			long bandSegsTested = 0;
			long bandPhysicsQueries = 0;
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
			int pass2FlagsKey = (pass2Flags.HitBackFaces ? 1 : 0) | (pass2Flags.HitFromInside ? 2 : 0);
			int pass2QuickRayMissLogRemaining = Pass2LogQuickRayMissSamples;

			Vector3 camPosPass2 = camPos;
			bool useOverlap = UseBroadphaseOverlap;
			bool useQuickRay = UseBroadphaseQuickRay;
			if (UseBroadphasePolicy)
			{
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

			if (useQuickRay || UseSingleProbeThenSubdivide)
			{
				_quickRayParams ??= new PhysicsRayQueryParameters3D();
				_quickRayParams.CollisionMask = _rbr.CollisionMask;
				_quickRayParams.CollideWithBodies = true;
				_quickRayParams.CollideWithAreas = true;
				_quickRayParams.HitFromInside = pass2Flags.HitFromInside;
				_quickRayParams.HitBackFaces = pass2Flags.HitBackFaces;
			}

			if (useQuickRay || UseSingleProbeThenSubdivide)
			{
				EnsurePass2QuickRayCache();
				ResetPass2QuickRayCache();
			}

			PerfScope pass2Scope = default;
			if (framePerfEnabled) pass2Scope = new PerfScope(_framePerf, PerfStage.Pass2_Subdivide);
			bool shadeTimingEnabled = statsEnabled || framePerfEnabled;

			void CountQuickRayResult(bool hit)
			{
				if (!softGateDebugEnabled) return;
				_softGateFrame.QRayCalls++;
				if (hit) _softGateFrame.QRayHit++;
				else _softGateFrame.QRayMiss++;
				if (softGateBandEnabled)
				{
					_softGateBand.QRayCalls++;
					if (hit) _softGateBand.QRayHit++;
					else _softGateBand.QRayMiss++;
				}
			}

			void SoftGateRecordMetric(float metric)
			{
				if (!softGateDebugEnabled) return;
				_softGateFrame.SoftGateMetricCount++;
				_softGateFrame.SoftGateMetricSum += metric;
				if (metric < _softGateFrame.SoftGateMetricMin) _softGateFrame.SoftGateMetricMin = metric;
				if (metric > _softGateFrame.SoftGateMetricMax) _softGateFrame.SoftGateMetricMax = metric;
				if (softGateBandEnabled)
				{
					_softGateBand.SoftGateMetricCount++;
					_softGateBand.SoftGateMetricSum += metric;
					if (metric < _softGateBand.SoftGateMetricMin) _softGateBand.SoftGateMetricMin = metric;
					if (metric > _softGateBand.SoftGateMetricMax) _softGateBand.SoftGateMetricMax = metric;
				}
			}

			void SoftGateRecordSkip(SoftGateDecisionReason reason)
			{
				if (!softGateDebugEnabled) return;
				_softGateFrame.SoftGateSkipped++;
				if (softGateBandEnabled) _softGateBand.SoftGateSkipped++;
				switch (reason)
				{
					case SoftGateDecisionReason.Disabled:
						_softGateFrame.SkipOther++;
						if (softGateBandEnabled) _softGateBand.SkipOther++;
						break;
					case SoftGateDecisionReason.SegLenTooShort:
						_softGateFrame.SkipSegLenTooShort++;
						if (softGateBandEnabled) _softGateBand.SkipSegLenTooShort++;
						break;
					case SoftGateDecisionReason.ScoreTooLow:
						_softGateFrame.SkipScoreTooLow++;
						if (softGateBandEnabled) _softGateBand.SkipScoreTooLow++;
						break;
					case SoftGateDecisionReason.RandomNotSelected:
						_softGateFrame.SkipRandomNotSelected++;
						if (softGateBandEnabled) _softGateBand.SkipRandomNotSelected++;
						break;
					case SoftGateDecisionReason.BudgetAttemptCap:
						_softGateFrame.SkipBudgetAttemptCap++;
						if (softGateBandEnabled) _softGateBand.SkipBudgetAttemptCap++;
						break;
					case SoftGateDecisionReason.BudgetSubdivideCap:
						_softGateFrame.SkipBudgetSubdivideCap++;
						if (softGateBandEnabled) _softGateBand.SkipBudgetSubdivideCap++;
						break;
					case SoftGateDecisionReason.Guard:
						_softGateFrame.SkipGuard++;
						if (softGateBandEnabled) _softGateBand.SkipGuard++;
						break;
					case SoftGateDecisionReason.NanMetric:
					case SoftGateDecisionReason.Other:
					default:
						_softGateFrame.SkipOther++;
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
				if (softGateSegEnabled)
					sampleThisSeg = (_softGateSampleCounter++ % SoftGateSampleEveryNSegments) == 0;

				score = 0f;
				turnAngleDeg = 0f;
				turnAngleScore = 0f;
				prevHitLostScore = 0f;
				randomProbe = false;
				segLenOk = pass2SoftGateMinSegmentLengthEffective <= 0f || segmentLength >= pass2SoftGateMinSegmentLengthEffective;

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

				if (pass2SoftGateMaxAttemptsPerFrameEffective > 0 && _softGateAttemptsUsedThisFrame >= pass2SoftGateMaxAttemptsPerFrameEffective)
				{
					budgetExceeded++;
					reason = SoftGateDecisionReason.BudgetAttemptCap;
					SoftGateRecordSkip(reason);
					DisableSoftGateThisFrame("budget_attempt");
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

				if (pass2SoftGateMaxSubdividedCallsPerFrameEffective > 0 && _softGateSubdividedCallsUsedThisFrame >= pass2SoftGateMaxSubdividedCallsPerFrameEffective)
				{
					budgetExceeded++;
					reason = SoftGateDecisionReason.BudgetSubdivideCap;
					SoftGateRecordSkip(reason);
					DisableSoftGateThisFrame("budget_subdivide");
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

				if (!allowSoftGate)
				{
					if (countSubdividedSkip) _perfFrame.SubdividedRaySkipped++;
					if (framePerfEnabled)
					{
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
				if (!attemptBudgetOk || !subdivideBudgetOk)
				{
					budgetExceeded++;
					reason = attemptBudgetOk ? SoftGateDecisionReason.BudgetSubdivideCap : SoftGateDecisionReason.BudgetAttemptCap;
					SoftGateRecordSkip(reason);
					DisableSoftGateThisFrame(attemptBudgetOk ? "budget_subdivide" : "budget_attempt");
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
				if (frameId != _softGateFrameId)
				{
					_softGateFrameId = frameId;
					_p2SoftGateUsedThisFrame = 0;
				}

				if (softGateDebugEnabled)
				{
					_softGateFrame.SoftGateConsidered++;
					if (softGateBandEnabled) _softGateBand.SoftGateConsidered++;
				}

				if (softGateDisabledThisFrame)
				{
					reason = SoftGateDecisionReason.Guard;
					SoftGateRecordSkip(reason);
					return false;
				}

				if (!Pass2SoftGateEnableQuickRayMiss || !Pass2SoftGateScoringEnabled)
				{
					reason = SoftGateDecisionReason.Disabled;
					SoftGateRecordSkip(reason);
					return false;
				}

				if (Pass2SoftGateDebugEnabled && _softGateParamLogRemaining > 0)
				{
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
				if (!metricsFinite)
				{
					reason = SoftGateDecisionReason.NanMetric;
					SoftGateRecordSkip(reason);
					return false;
				}

				// Min segment length: avoids spending budget on tiny segments that rarely change the result.
				segLenOk = minSegLen <= 0f || segmentLength >= minSegLen;
				if (!segLenOk)
				{
					reason = SoftGateDecisionReason.SegLenTooShort;
					SoftGateRecordSkip(reason);
					return false;
				}

				// Turn-angle score: captures local curvature/instability in the segment chain.
				bool haveDirs = prevSegDir.LengthSquared() > 1e-6f && currSegDir.LengthSquared() > 1e-6f;
				if (haveDirs && Pass2SoftGateScoreTurnAngleWeight > 0f)
				{
					float dot = Mathf.Clamp(prevSegDir.Dot(currSegDir), -1f, 1f);
					turnAngleDeg = Mathf.RadToDeg(Mathf.Acos(dot));
					turnAngleScore = (turnAngleDeg / 180f) * Pass2SoftGateScoreTurnAngleWeight;
					score += turnAngleScore;
				}
				// Prev-hit-lost bonus: encourages probing when last frame hit disappeared.
				if (prevHadHit && prevHitLost)
				{
					prevHitLostScore = Pass2SoftGateScorePrevHitLostBonus;
					score += prevHitLostScore;
				}

				// Random probe: avoids missing thin/rare occluders when score stays low.
				randomProbe = Pass2SoftGateRandomProbeChance > 0f && _rng.Randf() < Pass2SoftGateRandomProbeChance;
				bool scoreHit = score >= Pass2SoftGateScoreThreshold || randomProbe;

				if (softGateDebugEnabled) SoftGateRecordMetric(score);

				// Score threshold: only trigger when instability evidence is strong enough.
				if (!scoreHit)
				{
					bool randEnabled = Pass2SoftGateRandomProbeChance > 0f;
					reason = randEnabled ? SoftGateDecisionReason.RandomNotSelected : SoftGateDecisionReason.ScoreTooLow;
					SoftGateRecordSkip(reason);
					return false;
				}

				if (Pass2SoftGateScoreBudgetPerFrame > 0 && _p2SoftGateUsedThisFrame >= Pass2SoftGateScoreBudgetPerFrame)
				{
					reason = SoftGateDecisionReason.BudgetAttemptCap;
					SoftGateRecordSkip(reason);
					return false;
				}

				if (softGateDebugEnabled)
				{
					_softGateFrame.SoftGateForced++;
					if (softGateBandEnabled) _softGateBand.SoftGateForced++;
				}

				_p2SoftGateUsedThisFrame++;

				softGateTriggered++;
				return true;
			}

			void LogSoftGateSample(int segIndex, float segmentLength, float score, float turnAngleDeg, float turnAngleScore, float prevHitLostScore, bool randomProbe, bool segLenOk, bool forced, bool attempted, bool hit, SoftGateDecisionReason reason, bool sampleThisSeg)
			{
				if (!sampleThisSeg) return;

				string reasonText;
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

				reasonText += $" seglen={(segLenOk ? "ok" : "short")} score={(score >= Pass2SoftGateScoreThreshold ? "ok" : "low")} rand={(randomProbe ? 1 : 0)}";

				GD.Print(
					$"SG seg={segIndex} len={segmentLength:0.###} score={score:0.###} angleDeg={turnAngleDeg:0.###} angleScore={turnAngleScore:0.###} prevLostScore={prevHitLostScore:0.###} forced={(forced ? 1 : 0)} attempt={(attempted ? 1 : 0)} hit={(hit ? 1 : 0)} reason={reasonText}");
			}

			if (skipBandPhysics)
			{
				ulong shadeStart = 0;
				if (shadeTimingEnabled) shadeStart = Time.GetTicksUsec();

				int yAlignedStart = yStart + ((stride - (yStart % stride)) % stride);
				for (int y = yAlignedStart; y < yEnd; y += stride)
				{
					if (CheckRenderStepWatchdog())
					{
						renderStepAbort = true;
						break;
					}
					int localY = y - yStart;
					for (int x = 0; x < filmW; x += stride)
					{
						if ((x & 31) == 0 && CheckRenderStepWatchdog())
						{
							renderStepAbort = true;
							break;
						}
						if (statsEnabled)
						{
							int pi = localY * filmW + x;
							_perfFrame.Segs += _segCountPerPixel[pi];
							if (_rbr != null && _rbr.RequireHitToRender) _perfFrame.ShadingSkippedPixels++;
							_perfFrame.TracedPixels++;
						}
						if (bandCountersEnabled)
						{
							int pi = localY * filmW + x;
							bandSegsIntegrated += _segCountPerPixel[pi];
						}
						bandTracedPixels++;
						int filled = FillPixelBlock(x, y, stride, SkyColor, filmW, filmH);
						if (statsEnabled) _perfFrame.FilledPixels += filled;
						if (framePerfEnabled) bandFilledPixels += filled;
					}
					if (renderStepAbort) break;
				}

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
					if (CheckRenderStepWatchdog())
					{
						renderStepAbort = true;
						break;
					}
					int localY = y - yStart;
					for (int x = 0; x < filmW; x += stride)
					{
						if ((x & 31) == 0 && CheckRenderStepWatchdog())
						{
							renderStepAbort = true;
							break;
						}
						int pi = localY * filmW + x;
						int globalPi = y * filmW + x;
						if (statsEnabled) _perfFrame.TracedPixels++;
						bandTracedPixels++;

						bool prevHadHit = Pass2ForceOnInstability
							&& _pass2PrevHadHit.Length > globalPi
							&& _pass2PrevHadHit[globalPi] != 0;
						bool prevHadHitForSoftGate = _pass2PrevHadHit.Length > globalPi
							&& _pass2PrevHadHit[globalPi] != 0;
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
						if (pass1StoppedEarly && pass1HitSegIndex >= 0)
						{
							segStart = Math.Max(0, pass1HitSegIndex - 1);
							segEnd = Math.Min(segCount - 1, pass1HitSegIndex + 1);
						}

						if (statsEnabled) _perfFrame.Segs += segCount;
						if (bandCountersEnabled) bandSegsIntegrated += segCount;

						bool isCenterSample = (x == filmW / 2 && y == (yStart + (bandH / 2)));
						bool logCenterSample = VerbosePerfLogs && isCenterSample;
						bool needHitName = NeedColliderNames || logCenterSample;
						bool testedAnyInPass0ThisPixel = false;
						bool skippedAnyByStrideThisPixel = false;
						int softGateAttemptsThisPixel = 0;
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
													continue;
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
													continue;
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
													continue;
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
													continue;
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
											if (DisableSoftGateOnOverload)
												DisableSoftGateThisFrame("softgate_watchdog");
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
								EarlyOut("near early-out");
								break;
							}else{}

							if (softGateWatchdogTrippedThisPixel)
								break;

							if (hadHit)
								break;
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
			if (renderStepAbort)
			{
				AbortRenderStep("watchdog");
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

			_rowCursor = yEnd;
			if (_rowCursor >= filmH) _rowCursor = 0;

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
				if (_softGateFrame.SoftGateEnabled && _softGateFrame.SoftGateConsidered > 0 && _softGateFrame.SoftGateAttempts == 0)
				{
					GD.Print("[SoftGate][WARN] enabled but no attempts: check gating (minSegLen/score/random) summary above.");
				}
			}
		}
		finally
		{
			if (framePerfEnabled) frameScope.Dispose();
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
		float scale = Mathf.Clamp(FilmResolutionScale, 0.25f, 1.0f);
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

		if (_rowCursor >= _filmHeight) _rowCursor = 0;
		return true;
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
	}

	public void ApplyPerfPresetQuality()
	{
		UseFieldSourceCache = false;
		UseBroadphasePolicy = false;
		TinySegmentSkipLen = 0.0f;
		EarlyOutDistanceEps = 0.0f;
		NeedColliderNames = false;
	}

	void UpdateFilmOpacity()
	{
		var target = _filmView ?? _overlayRect;
		if (target == null) return;
		target.Modulate = new Color(1, 1, 1, FilmOpacity);
	}

}
