using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using RendererCore.Config;
using RendererCore.Fields;

public partial class RayBeamRenderer : Node3D
{
	// PR: Academic canonical radial model: u-clamped shell with profile f(u).
	// ===== Interaction Map =====
	// Provides to GrinFilmCamera:
	// - DebugRayBundle via GetDebugRayBundle() (ray polylines + hit payloads for overlay)
	// - UpdateDebugOverlayFromFilm(...) entry point to draw film-driven overlays
	// Consumes from GrinFilmCamera:
	// - None directly; GrinFilmCamera reads this node's ray buffers and toggles
	// Transfer points:
	// - GetDebugRayBundle() called by GrinFilmCamera during film pass
	// - UpdateDebugOverlayFromFilm(...) called by GrinFilmCamera on main thread

	// ===== Inputs / Controls =====
	[ExportCategory("Ray Beam Renderer")]
	[ExportGroup("References")]
	/// <summary>Optional camera override; uses viewport camera when empty.</summary>
	// CONTROL FACTOR: Optional camera override path; when set, all ray generation uses this camera's transform/props.
	[Export] public NodePath CameraPath;

	[ExportGroup("Shared With GrinFilmCamera")]
	[ExportSubgroup("Ray March")]
	// Consumed by GrinFilmCamera.ResolveEffectiveConfig().
	/// <summary>Number of integration steps per ray.</summary>
	// CONTROL FACTOR: Step count (>=1); higher = smoother curves + more cost.
	[Export] public int StepsPerRay = 64;
	/// <summary>Base step length for integration.</summary>
	// CONTROL FACTOR: Base distance per step in world units; higher = faster but less accurate.
	[Export] public float StepLength = 0.25f;
	/// <summary>Clamp minimum step length.</summary>
	// CONTROL FACTOR: Lower bound on adaptive step size; prevents tiny steps (>= ~0.0001).
	[Export] public float MinStepLength = 0.05f;
	/// <summary>Clamp maximum step length.</summary>
	// CONTROL FACTOR: Upper bound on adaptive step size; prevents too-coarse integration.
	[Export] public float MaxStepLength = 0.5f;
	/// <summary>Adaptation strength for step sizing.</summary>
	// CONTROL FACTOR: How strongly acceleration shortens steps; higher = more adaptation.
	[Export] public float StepAdaptGain = 0.05f;
	/// <summary>Perpendicular acceleration threshold to consider path low curvature.</summary>
	// CONTROL FACTOR: Low-curvature threshold; below this we boost step size.
	[Export] public float LowCurvaturePerpAccel = 0.05f;
	/// <summary>Multiplier for step size when curvature is low.</summary>
	// CONTROL FACTOR: Step size multiplier when curvature is low (>1 increases speed, reduces detail).
	[Export] public float LowCurvatureStepBoost = 2.0f;
	/// <summary>Safety factor for curvature-derived radius bounds.</summary>
	// CONTROL FACTOR: Scales curvature-based radius bounds; higher = more conservative.
	[Export] public float RadiusSafety = 3.0f;
	/// <summary>Minimum radius bound for curved segments.</summary>
	// CONTROL FACTOR: Lower bound on curvature-based radius bounds.
	[Export] public float RadiusMin = 0.01f;

	[ExportSubgroup("Field Sources")]
	// Consumed by GrinFilmCamera.ResolveEffectiveConfig().
	/// <summary>Integrates field acceleration instead of closed form.</summary>
	// CONTROL FACTOR: True = integrate acceleration per step; false = analytic bend formula.
	[Export] public bool UseIntegratedField = true;
	/// <summary>Base bend strength.</summary>
	// CONTROL FACTOR: Global bend amplitude (unitless); higher = stronger curvature.
	[Export] public float BendScale = 0.12f;
	/// <summary>Extra multiplier for field strength.</summary>
	// CONTROL FACTOR: Global field strength multiplier; higher = stronger attraction/repulsion.
	[Export] public float FieldStrength = 1.0f;
	/// <summary>Selects the experimental metric steering surrogate used by Metric_NullGeodesic.</summary>
	// CONTROL FACTOR: Keeps the current envelope law as baseline and allows impact-parameter-focused steering for comparisons.
	[Export] public MetricSteeringLaw MetricSteeringLawMode = MetricSteeringLaw.MetricLaw_CurrentEnvelope;
	/// <summary>Turn-angle threshold in degrees before metric steps are subdivided.</summary>
	// CONTROL FACTOR: Smaller values increase metric segment density in tighter bends.
	[Export] public float MetricAdaptiveTurnThresholdDegrees = 4.0f;
	/// <summary>Local geometric error tolerance for metric step acceptance.</summary>
	// CONTROL FACTOR: Smaller values force finer metric segment emission.
	[Export] public float MetricAdaptiveErrorTolerance = 0.01f;
	/// <summary>Extra curvature gain used by metric step-size reduction.</summary>
	// CONTROL FACTOR: Higher values shrink metric steps more aggressively in high curvature.
	[Export] public float MetricAdaptiveCurvatureGain = 2.0f;
	/// <summary>Maximum metric step-halving retries applied before accepting a minimum step.</summary>
	// CONTROL FACTOR: Higher values allow denser metric segment chains at increased cost.
	[Export] public int MetricAdaptiveMaxSubdivisions = 2;
	/// <summary>Enables bounded derivative-aware adaptive step scaling on top of the existing controller.</summary>
	// CONTROL FACTOR: Default off for experiment safety; when enabled, a short curvature-history trend can nudge step length up/down.
	[Export] public bool UseDerivativeAwareStepping = false;
	/// <summary>Strength of derivative-aware step scaling around the existing adaptive baseline.</summary>
	// CONTROL FACTOR: Higher values increase predictive influence but remain bounded by the max up/down clamps.
	[Export] public float DerivativeAwareStepScaleStrength = 0.15f;
	/// <summary>History warmup length for derivative-aware stepping.</summary>
	// CONTROL FACTOR: Larger values delay full predictive influence to reduce startup chatter.
	[Export] public int DerivativeAwareHistoryLength = 4;
	/// <summary>Includes second-derivative trend information when computing predictive difficulty.</summary>
	// CONTROL FACTOR: True = earlier warning of curvature ramps; false = first-derivative only.
	[Export] public bool DerivativeAwareUseSecondDerivative = true;
	/// <summary>EMA smoothing factor used for curvature proxy and predictive difficulty.</summary>
	// CONTROL FACTOR: Lower values smooth more aggressively; higher values react faster.
	[Export] public float DerivativeAwareSmoothingAlpha = 0.5f;
	/// <summary>Upper bound on derivative-aware step expansion relative to the baseline adaptive step.</summary>
	// CONTROL FACTOR: Caps predictive scale-up so the experiment cannot overrun smooth regions.
	[Export] public float DerivativeAwareMaxStepScaleUp = 1.15f;
	/// <summary>Lower bound on derivative-aware step shrink relative to the baseline adaptive step.</summary>
	// CONTROL FACTOR: Caps predictive scale-down so the experiment cannot over-tighten transition zones.
	[Export] public float DerivativeAwareMaxStepScaleDown = 0.85f;
	/// <summary>Ignore derivative-aware scale changes whose distance from unity is below this threshold.</summary>
	// CONTROL FACTOR: Small deadband suppresses low-value near-unity adjustments without disabling the predictor.
	[Export] public float DerivativeAwareStepDeadband = 0.01f;
	/// <summary>Optional minimum predictive difficulty before derivative-aware scaling is allowed to apply.</summary>
	// CONTROL FACTOR: Zero disables the gate; positive values suppress low-signal predictive nudges.
	[Export] public float DerivativeAwareDifficultyThreshold = 0.0f;
	/// <summary>Optional minimum applied scale delta before the adjustment is included in logs.</summary>
	// CONTROL FACTOR: Zero logs every applied adjustment; higher values focus diagnostics on larger changes.
	[Export] public float DerivativeAwareMinLoggedScaleDelta = 0.0f;
	/// <summary>Uses a small engage/release threshold gap to reduce deadband-edge flapping.</summary>
	// CONTROL FACTOR: True = keep the controller latched until the smaller release threshold is crossed.
	[Export] public bool DerivativeAwareUseHysteresis = true;
	/// <summary>Absolute scale delta required to enter derivative-aware active state.</summary>
	// CONTROL FACTOR: Higher values make entry more selective.
	[Export] public float DerivativeAwareHysteresisEngageDelta = 0.012f;
	/// <summary>Absolute scale delta below which derivative-aware active state is released.</summary>
	// CONTROL FACTOR: Lower than engage to prevent rapid threshold-edge re-entry.
	[Export] public float DerivativeAwareHysteresisReleaseDelta = 0.008f;
	/// <summary>Keeps derivative-aware active state alive for a tiny minimum accepted-step span after entry.</summary>
	// CONTROL FACTOR: True = convert one-sample threshold hits into short, stable active windows.
	[Export] public bool DerivativeAwareUseMinimumActiveSpan = true;
	/// <summary>Minimum accepted active samples before hysteresis release is allowed.</summary>
	// CONTROL FACTOR: Higher values hold predictive action longer after engagement.
	[Export] public int DerivativeAwareMinimumActiveAcceptedSteps = 2;
	/// <summary>Emits derivative-aware stepping diagnostics in transport/render-test logs when enabled.</summary>
	// CONTROL FACTOR: True = lightweight cumulative metrics for baseline-vs-experiment comparison.
	[Export] public bool DerivativeAwareLogMetrics = true;
	/// <summary>World center for field when not using camera.</summary>
	// CONTROL FACTOR: Field center (world space) when not camera-driven.
	[Export] public Vector3 FieldCenter = Vector3.Zero;
	/// <summary>Uses camera position as field center.</summary>
	// CONTROL FACTOR: True = field center follows camera; false = uses FieldCenter.
	[Export] public bool FieldCenterIsCamera = true;

	[ExportSubgroup("Collision")]
	// Consumed by GrinFilmCamera.ResolveEffectiveConfig().
	/// <summary>Stops simulation on first hit.</summary>
	// CONTROL FACTOR: True = stop simulation at first hit; false = keep integrating.
	[Export] public bool StopOnHit = false;
	/// <summary>Collision mask for ray tests.</summary>
	// CONTROL FACTOR: Physics layer mask for collision tests.
	[Export] public uint CollisionMask = 0x0000FFFF;
	/// <summary>Collision test cadence in steps.</summary>
	// CONTROL FACTOR: Collision check cadence (steps); higher = fewer tests.
	[Export] public int CollisionEveryNSteps = 1;
	/// <summary>Sphere radius for collision.</summary>
	// CONTROL FACTOR: Sweep radius for sphere tests (world units).
	[Export] public float CollisionRadius = 0.03f;
	/// <summary>Uses IntersectShape sphere sweep.</summary>
	// CONTROL FACTOR: True = sphere sweep; false = raycast.
	[Export] public bool UseSphereSweepCollision = false;
	/// <summary>Rejects segments outside a plane slab.</summary>
	// CONTROL FACTOR: True = filter segments by insight plane slab.
	[Export] public bool UseInsightPlaneFilter = false;
	/// <summary>NodePath to plane source.</summary>
	// CONTROL FACTOR: NodePath providing insight plane; used when UseInsightPlaneFilter is true.
	[Export] public NodePath InsightPlaneNode;
	/// <summary>Segment length that triggers subdivision.</summary>
	// CONTROL FACTOR: Subdivide long segments for collision accuracy (world units).
	[Export] public float CollisionRaySubdivideThreshold = 0.25f;
	/// <summary>Max sub-rays per segment.</summary>
	// CONTROL FACTOR: Cap on subdivision count per segment.
	[Export] public int MaxCollisionSubsteps = 16;
	/// <summary>Only render rays that hit.</summary>
	// CONTROL FACTOR: True = skip drawing rays without hits (useful for debug clarity).
	[Export] public bool RequireHitToRender = false;
	/// <summary>Keeps collision checks even if StopOnHit is false.</summary>
	// CONTROL FACTOR: True = keep collision tests when StopOnHit is false.
	[Export] public bool CheckCollisionsEvenIfNotStopping = false;
	/// <summary>Adjusts collision cadence to limit screen error.</summary>
	// CONTROL FACTOR: Adaptive collision cadence based on screen error (camera-dependent).
	[Export] public bool UseScreenSpaceCollisionCadence = true;
	/// <summary>Target sagitta error in pixels.</summary>
	// CONTROL FACTOR: Screen error budget (pixels) for adaptive collision cadence.
	[Export] public float CollisionMaxErrorPixels = 0.75f;
	/// <summary>Min depth for screen error calculations.</summary>
	// CONTROL FACTOR: Minimum depth (world units) used for screen error computations.
	[Export] public float MinDepthForError = 0.10f;
	/// <summary>Lower bound on adaptive collision cadence.</summary>
	// CONTROL FACTOR: Minimum allowed collision cadence steps.
	[Export] public int MinCollisionEveryNSteps = 1;

	[ExportSubgroup("Debug Visualization (Shared)")]
	// Consumed by GrinFilmCamera.ResolveEffectiveConfig().
	/// <summary>Debug overlay mode (off/rays/normals).</summary>
	// CONTROL FACTOR: Debug overlay selection.
	[Export] public DebugDrawMode DebugMode = DebugDrawMode.RaysAndNormals;
	/// <summary>Length of debug hit normals.</summary>
	// CONTROL FACTOR: Length of drawn normals (world units).
	[Export] public float DebugNormalLen = 0.25f;
	/// <summary>Film camera drives overlay drawing when true.</summary>
	// CONTROL FACTOR: If true, overlay is driven by film pass via UpdateDebugOverlayFromFilm.
	[Export] public bool DebugOverlayOwnedByFilm = true;

	[ExportGroup("Ray Beam Rendering")]
	[ExportSubgroup("Samples")]
	/// <summary>Billboard size for each sample.</summary>
	// CONTROL FACTOR: Billboard quad size (world units); larger = thicker beams.
	[Export] public float QuadSize = 0.04f;
	/// <summary>Base alpha for ray samples.</summary>
	// CONTROL FACTOR: Base alpha for sample color; higher = brighter accumulation.
	[Export] public float Alpha = 0.50f;
	/// <summary>Samples every N steps for drawing.</summary>
	// CONTROL FACTOR: Render cadence; higher = fewer visible samples (faster, more sparse).
	[Export] public int RenderEveryNSteps = 1;
	/// <summary>Colors rays based on field magnitude.</summary>
	// CONTROL FACTOR: True = encode field magnitude in color; false = use emitter color only.
	[Export] public bool ColorByField = true;
	/// <summary>Strength of field-based color ramp.</summary>
	// CONTROL FACTOR: Field-to-color gain; higher = stronger color shift on high acceleration.
	[Export] public float FieldColorGain = 0.15f;
	/// <summary>Color for maximum field heat.</summary>
	// CONTROL FACTOR: Color at high field magnitude; used when ColorByField is true.
	[Export] public Color HotColor = new Color(0.2f, 1.0f, 1.0f, 1.0f);

	[ExportSubgroup("Hit Markers")]
	/// <summary>Stops drawing samples after the first hit.</summary>
	// CONTROL FACTOR: When true, render trail ends at hit (simulation may continue).
	[Export] public bool TerminateTrailOnHit = true;
	/// <summary>Draws a marker at hit position.</summary>
	// CONTROL FACTOR: Toggle hit marker billboard.
	[Export] public bool DrawHitMarker = true;
	/// <summary>Color of the hit marker.</summary>
	// CONTROL FACTOR: Hit marker color (RGB/A).
	[Export] public Color HitMarkerColor = new Color(1, 0, 0, 1);

	// =======================
	// Debug Controls (RayBeamRenderer)
	// =======================
	public enum DebugDrawMode
	{
		Off = 0,
		RaysOnly = 1,
		RaysAndNormals = 2
	}

	[ExportGroup("Debug Visualization")]
	[ExportSubgroup("Live Rebuild")]
	/// <summary>Rebuilds ray debug visualization every frame (RayBeamRenderer only; not film rendering).</summary>
	// CONTROL FACTOR: Master toggle for ray debug rebuilds; when false, rays stay static until manually rebuilt.
	[Export] public bool UpdateEveryFrame = true;
	/// <summary>Alias for UpdateEveryFrame to distinguish from GrinFilmCamera.UpdateEveryFrame.</summary>
	[Export] public bool UpdateRayDebugEveryFrame
	{
		get => UpdateEveryFrame;
		set => UpdateEveryFrame = value;
	}
	/// <summary>Allows Rebuild when UpdateEveryFrame is enabled.</summary>
	// CONTROL FACTOR: Secondary gate for rebuilds; use to freeze updates without disabling UpdateEveryFrame logic.
	[Export] public bool AllowRebuild = true;

	[ExportSubgroup("Logging")]
	/// <summary>Enables per-ray debug logs.</summary>
	// CONTROL FACTOR: Master debug logging toggle.
	[Export] public bool DebugRender = false;
	/// <summary>Log every N rays during rebuild.</summary>
	// CONTROL FACTOR: Debug print cadence (rays).
	[Export] public int DebugEveryNRays = 25;
	/// <summary>Logs billboard rejects (bounds or NaN).</summary>
	// CONTROL FACTOR: Debug logging for billboard rejection.
	[Export] public bool DebugSetBillboardRejects = false;
	/// <summary>Max billboard reject logs per ray.</summary>
	// CONTROL FACTOR: Cap on reject logs per ray.
	[Export] public int DebugMaxRejectPrints = 10;
	/// <summary>Cap on debug overlay rays.</summary>
	// CONTROL FACTOR: Max rays rendered in debug overlay.
	[Export] public int DebugMaxRays = 256;
	/// <summary>Cap on segments per debug ray.</summary>
	// CONTROL FACTOR: Max segments drawn per debug ray.
	[Export] public int DebugMaxSegmentsPerRay = 64;
	/// <summary>Draw only rays that hit.</summary>
	// CONTROL FACTOR: Filter debug overlay to hit rays only.
	[Export] public bool DebugDrawOnlyHits = false;

	public readonly struct SharedSnapshot
	{
		public readonly bool HasRenderer;
		public readonly int StepsPerRay;
		public readonly int CollisionEveryNSteps;
		public readonly float StepLength;
		public readonly float MinStepLength;
		public readonly float MaxStepLength;
		public readonly float StepAdaptGain;
		public readonly bool UseIntegratedField;
		public readonly float BendScale;
		public readonly float FieldStrength;
		public readonly Vector3 FieldCenter;
		public readonly bool FieldCenterIsCamera;
		public readonly uint CollisionMask;
		public readonly float CollisionRadius;
		public readonly bool UseSphereSweepCollision;
		public readonly bool UseInsightPlaneFilter;
		public readonly float CollisionRaySubdivideThreshold;
		public readonly int MaxCollisionSubsteps;
		public readonly bool RequireHitToRender;
		public readonly bool StopOnHit;
		public readonly bool TerminateTrailOnHit;
		public readonly bool UseScreenSpaceCollisionCadence;
		public readonly float CollisionMaxErrorPixels;
		public readonly float MinDepthForError;
		public readonly int MinCollisionEveryNSteps;
		public readonly DebugDrawMode DebugMode;
		public readonly float DebugNormalLen;
		public readonly bool DebugOverlayOwnedByFilm;

		public SharedSnapshot(
			bool hasRenderer,
			int stepsPerRay,
			int collisionEveryNSteps,
			float stepLength,
			float minStepLength,
			float maxStepLength,
			float stepAdaptGain,
			bool useIntegratedField,
			float bendScale,
			float fieldStrength,
			Vector3 fieldCenter,
			bool fieldCenterIsCamera,
			uint collisionMask,
			float collisionRadius,
			bool useSphereSweepCollision,
			bool useInsightPlaneFilter,
			float collisionRaySubdivideThreshold,
			int maxCollisionSubsteps,
			bool requireHitToRender,
			bool stopOnHit,
			bool terminateTrailOnHit,
			bool useScreenSpaceCollisionCadence,
			float collisionMaxErrorPixels,
			float minDepthForError,
			int minCollisionEveryNSteps,
			DebugDrawMode debugMode,
			float debugNormalLen,
			bool debugOverlayOwnedByFilm)
		{
			HasRenderer = hasRenderer;
			StepsPerRay = stepsPerRay;
			CollisionEveryNSteps = collisionEveryNSteps;
			StepLength = stepLength;
			MinStepLength = minStepLength;
			MaxStepLength = maxStepLength;
			StepAdaptGain = stepAdaptGain;
			UseIntegratedField = useIntegratedField;
			BendScale = bendScale;
			FieldStrength = fieldStrength;
			FieldCenter = fieldCenter;
			FieldCenterIsCamera = fieldCenterIsCamera;
			CollisionMask = collisionMask;
			CollisionRadius = collisionRadius;
			UseSphereSweepCollision = useSphereSweepCollision;
			UseInsightPlaneFilter = useInsightPlaneFilter;
			CollisionRaySubdivideThreshold = collisionRaySubdivideThreshold;
			MaxCollisionSubsteps = maxCollisionSubsteps;
			RequireHitToRender = requireHitToRender;
			StopOnHit = stopOnHit;
			TerminateTrailOnHit = terminateTrailOnHit;
			UseScreenSpaceCollisionCadence = useScreenSpaceCollisionCadence;
			CollisionMaxErrorPixels = collisionMaxErrorPixels;
			MinDepthForError = minDepthForError;
			MinCollisionEveryNSteps = minCollisionEveryNSteps;
			DebugMode = debugMode;
			DebugNormalLen = debugNormalLen;
			DebugOverlayOwnedByFilm = debugOverlayOwnedByFilm;
		}
	}

	public SharedSnapshot GetSharedSnapshot()
	{
		// Consumed by GrinFilmCamera.ResolveEffectiveConfig().
		return new SharedSnapshot(
			true,
			StepsPerRay,
			CollisionEveryNSteps,
			StepLength,
			MinStepLength,
			MaxStepLength,
			StepAdaptGain,
			UseIntegratedField,
			BendScale,
			FieldStrength,
			FieldCenter,
			FieldCenterIsCamera,
			CollisionMask,
			CollisionRadius,
			UseSphereSweepCollision,
			UseInsightPlaneFilter,
			CollisionRaySubdivideThreshold,
			MaxCollisionSubsteps,
			RequireHitToRender,
			StopOnHit,
			TerminateTrailOnHit,
			UseScreenSpaceCollisionCadence,
			CollisionMaxErrorPixels,
			MinDepthForError,
			MinCollisionEveryNSteps,
			DebugMode,
			DebugNormalLen,
			DebugOverlayOwnedByFilm);
	}

	public int EstimateMaxSegmentsPerRay()
	{
		int cadenceSegments = Mathf.Max(1, StepsPerRay / Mathf.Max(1, CollisionEveryNSteps)) + 2;
		if (!UseIntegratedField)
		{
			return cadenceSegments;
		}

		int metricSegments = Mathf.Max(1, StepsPerRay * GetMetricAdaptiveStepMultiplier()) + 2;
		return Mathf.Max(cadenceSegments, metricSegments);
	}

	// ===== Cached State =====
	private MultiMeshInstance3D _mmi;
	private MultiMesh _mm;
	private StandardMaterial3D _mat;
	private Camera3D _cachedCamera;
	private bool _cameraResolved;

	private float _lastBeta = float.NaN;
	private float _lastGamma = float.NaN;

	private bool _rebuildInProgress = false;
	private bool _rebuildQueued = false;
	private long _rebuildRequestId = 0;
	private bool _lifecycleInTree = false;
	private readonly HashSet<long> _lifecycleDebugExecutedDeferredTokens = new();
	private int _lifecycleDebugQueuedOutOfTreeCount = 0;
	private int _lifecycleDebugDuplicateDeferredTokenExecCount = 0;
	private int _lifecycleDebugDeferredTokenExecCount = 0;

	// --- Change detection cache ---
	private Vector3 _lastCamPos = new Vector3(float.NaN, float.NaN, float.NaN);
	private float _lastCamFocal = float.NaN;
	private int _lastFieldSourceCount = -1;
	private TransportModel _lastLoggedTransportModel = TransportModel.GRIN_Optical;
	private bool _hasLoggedTransportModel = false;
	private bool _loggedMetricStubFallback = false;
	private bool _loggedMetricEquivalentFallback = false;
	private bool _loggedMetricWeakFieldMapping = false;
	private bool _loggedMetricScalarIngredients = false;
	private bool _loggedMetricSteeringLaw = false;
	private bool _loggedHybridStubFallback = false;
	private static int _pass1ProbeUnavailableWarned = 0;
	private static int _pass1ProbeQueryFailureWarned = 0;
	private static int _subdividedRayQueryFailureWarned = 0;
	private static bool _metricComparisonScalarOverrideResolved = false;
	private static float _metricComparisonScalarOverride = 1.0f;
	private static bool _metricSteeringLawOverrideResolved = false;
	private static MetricSteeringLaw _metricSteeringLawOverride = MetricSteeringLaw.MetricLaw_CurrentEnvelope;
	private static bool _derivativeAwareStepOverrideResolved = false;
	private static bool? _derivativeAwareStepOverride = null;
	private static bool _derivativeAwareMinimumActiveAcceptedStepsOverrideResolved = false;
	private static int? _derivativeAwareMinimumActiveAcceptedStepsOverride = null;
	private const int TransportSteeringBucketCount = 6;
	private const float DerivativeAwareDkWeight = 0.5f;
	private const float DerivativeAwareD2kWeight = 0.25f;
	private int _metricDeltaZeroCount = 0;
	private int _metricDeltaNonzeroCount = 0;
	private int _metricFallbackCount = 0;
	private int _metricContributionAppliedCount = 0;
	private int _metricGridBypassStepCount = 0;
	private int _metricScalarDominatedStepCount = 0;
	private int _metricParallelRawCount = 0;
	private int _metricParallelPreferredCount = 0;
	private int _metricParallelRecoveredCount = 0;
	private int _metricParallelFallbackCount = 0;
	private int _metricZeroReasonParallelCount = 0;
	private int _metricZeroReasonPerpEpsilonCount = 0;
	private int _metricZeroReasonRadiusLowCount = 0;
	private int _metricZeroReasonRadiusHighCount = 0;
	private int _metricZeroReasonNonFiniteCount = 0;
	private int _metricZeroReasonCoincidentCount = 0;
	private int _metricZeroReasonStepGuardCount = 0;
	private int _metricZeroReasonZeroTurnCount = 0;
	private int _metricDiagnosticEvalCount = 0;
	private int _metricDiagnosticSampleLogsEmitted = 0;
	private int _metricParallelDiagnosticSampleLogsEmitted = 0;
	private double _transportSteeringTurnSum = 0.0;
	private float _transportSteeringMaxTurn = 0f;
	private int _transportSteeringTurnCount = 0;
	private readonly double[] _transportSteeringRadialTurnSums = new double[TransportSteeringBucketCount];
	private readonly int[] _transportSteeringRadialCounts = new int[TransportSteeringBucketCount];
	private double _derivativeAwareKSum = 0.0;
	private float _derivativeAwareKMin = float.PositiveInfinity;
	private float _derivativeAwareKMax = 0f;
	private double _derivativeAwareAbsDkSum = 0.0;
	private float _derivativeAwareAbsDkMin = float.PositiveInfinity;
	private float _derivativeAwareAbsDkMax = 0f;
	private double _derivativeAwareAbsD2kSum = 0.0;
	private float _derivativeAwareAbsD2kMin = float.PositiveInfinity;
	private float _derivativeAwareAbsD2kMax = 0f;
	private double _derivativeAwareDifficultySum = 0.0;
	private float _derivativeAwareDifficultyMin = float.PositiveInfinity;
	private float _derivativeAwareDifficultyMax = 0f;
	private double _derivativeAwareStepBeforeSum = 0.0;
	private double _derivativeAwareStepAfterSum = 0.0;
	private int _derivativeAwareSampleCount = 0;
	private int _derivativeAwareCandidateAdjustmentCount = 0;
	private int _derivativeAwareHysteresisEngagedSampleCount = 0;
	private int _derivativeAwareEngagedStepCount = 0;
	private int _derivativeAwareLoggedAppliedAdjustmentCount = 0;
	private int _derivativeAwareCandidateScaleUpCount = 0;
	private int _derivativeAwareCandidateScaleDownCount = 0;
	private int _derivativeAwareScaleUpCount = 0;
	private int _derivativeAwareScaleDownCount = 0;
	private int _derivativeAwareHysteresisEnterCount = 0;
	private int _derivativeAwareHysteresisExitCount = 0;
	private int _derivativeAwareActiveSpanCount = 0;
	private double _derivativeAwareActiveSpanLengthSum = 0.0;
	private int _derivativeAwareActiveSpanMaxLength = 0;
	private int _derivativeAwareSingleSampleSpanCount = 0;
	private int _derivativeAwareMultiSampleSpanCount = 0;
	private int _derivativeAwareMetricSubdivisionRetryCount = 0;

	private Plane _insightPlane;
	private bool _hasInsightPlane = false;

	// Boundary layer volume snapshots; rebuilt each frame on the main thread.
	// Read-only during parallel ray integration.
	private BoundaryLayerSnap[] _boundaryLayerSnaps = Array.Empty<BoundaryLayerSnap>();
	private bool _hasBoundaryLayers = false;
	private int[] _boundaryDebugEntryCounts = Array.Empty<int>();
	private int[] _boundaryDebugExitCounts = Array.Empty<int>();
	private int _boundaryDebugImpulseCount = 0;
	private int _boundaryDebugSceneTransformCount = 0;
	private bool _boundaryDebugRunActive = false;
	private bool _boundaryDebugHasEvents = false;

	private int _dbgRejectPrints = 0;

	// Shared sample buffers (no per-ray allocation)
	private Vector3[] _samplePos = Array.Empty<Vector3>();
	private Color[] _sampleCol = Array.Empty<Color>();

	// Per-ray metadata
	private RayMeta[] _rayMeta = Array.Empty<RayMeta>();
	private HitPayload[] _hitPayload = Array.Empty<HitPayload>();

	private int _sampleWriteHead;
	private int _rayWriteHead;

	// =======================
	// Debug Render Objects
	// =======================
	private MeshInstance3D _dbgMeshInstance;
	private ImmediateMesh _dbgImmediate;
	private StandardMaterial3D _dbgMaterial;

	// Reuse buffers to avoid GC churn
	private readonly System.Collections.Generic.List<Vector3> _dbgLinePoints = new();
	private readonly System.Collections.Generic.List<Color> _dbgLineColors = new();

	// Debug bundle backing arrays (derived from _rayMeta, no extra point storage)
	private int[] _dbgRayOffsets = Array.Empty<int>();
	private int[] _dbgRayCounts  = Array.Empty<int>();


	public struct RayMeta {
		public int SampleStart;
		public int SampleCount;
		public int RenderCount;
		public bool HadHit;
		public int HitPayloadIndex; // -1 if none
	}

	public enum RayTerminationReason
	{
		None = 0,
		Hit = 1,
		AbsorbedInsideInnerRadius = 2
	}

	public struct HitPayload {
		public bool Valid;
		public Vector3 Position;
		public ulong ColliderId;
		public string ColliderName;
		public float Distance;     // path length to hit
		public Vector3 Normal;     // (optional for v0)
		public Color Albedo;       // (optional; can be constant for v0)
		public int Absorbed;       // 1 when terminated by inner-radius absorption.
		public RayTerminationReason TerminationReason;
	}

	public struct RaySeg
	{
		public Vector3 A;
		public Vector3 B;
		public float TraveledB; // path length at end of segment (at B)
		public float RadiusBound; // conservative curve deviation bound for this segment
		public int BoundaryRemapCount; // number of scene-transform remaps experienced before this segment end
		public int EventCount; // cumulative boundary event count at segment end
		public int BoundaryCrossings; // cumulative entry+exit count at segment end
		public int TransformCount; // cumulative scene-transform count at segment end
		public int EntryCount; // cumulative entry count at segment end
		public int ExitCount; // cumulative exit count at segment end
		public int LastCrossingLayer; // latest boundary layer index, or -1 when unset
		public byte LastCrossingKind; // see LedgerCrossingKind
		public bool AmbiguousOrdering; // true when multiple crossing events occurred in one dispatch step
	}

	public delegate bool SegmentCallback(in RaySeg seg, int segIndex);

	public struct Pass1HitInfo
	{
		public bool Found;
		public float Distance;
		public Vector3 Position;
		public Vector3 Normal;
		public ulong ColliderId;
		public int PrimitiveOrShapeId;
	}

	public readonly struct LedgerContinuationSummary
	{
		public readonly float Traveled;
		public readonly int BoundaryRemapCount;
		public readonly int EventCount;
		public readonly int BoundaryCrossings;
		public readonly int TransformCount;
		public readonly int EntryCount;
		public readonly int ExitCount;
		public readonly int LastCrossingLayer;
		public readonly byte LastCrossingKind;
		public readonly bool AmbiguousOrdering;
		public readonly int SegmentsIntegrated;
		public readonly bool ReachedExit;
		public readonly bool TransformChanged;

		public LedgerContinuationSummary(
			float traveled,
			int boundaryRemapCount,
			int eventCount,
			int boundaryCrossings,
			int transformCount,
			int entryCount,
			int exitCount,
			int lastCrossingLayer,
			byte lastCrossingKind,
			bool ambiguousOrdering,
			int segmentsIntegrated,
			bool reachedExit,
			bool transformChanged)
		{
			Traveled = traveled;
			BoundaryRemapCount = boundaryRemapCount;
			EventCount = eventCount;
			BoundaryCrossings = boundaryCrossings;
			TransformCount = transformCount;
			EntryCount = entryCount;
			ExitCount = exitCount;
			LastCrossingLayer = lastCrossingLayer;
			LastCrossingKind = lastCrossingKind;
			AmbiguousOrdering = ambiguousOrdering;
			SegmentsIntegrated = segmentsIntegrated;
			ReachedExit = reachedExit;
			TransformChanged = transformChanged;
		}
	}

	public struct FieldSourceSnap
	{
		public bool Enabled;
		public Vector3 Center;
		public TransportModel TransportModel;
		public uint ModeFlags;
		public FieldShapeType ShapeType;
		public FieldCurveType CurveType;
		public float RInner;
		public float ROuter;
		public float Amp;
		public float CurveA;
		public float CurveB;
		public float CurveC;
		public float Sigma;
		public bool OverrideBetaScale;
		public float BetaScale;
		public float EdgeSoftness;
		public Curve CustomCurve;
		// BoxVolume support: half-extents in local space + inverse world orientation.
		// Zero/identity for SphereRadial; populated from FieldSource3D.BoxExtents at snap time.
		public Vector3 HalfExtents;
		public Basis BoxInvBasis;
	}

	// ===== Boundary Layer Volume Snapshot =====
	// Lightweight runtime mirror of BoundaryLayerVolume; used in the hot loop without
	// touching the scene tree. Parallel to FieldSourceSnap but a separate domain.
	public struct BoundaryLayerSnap
	{
		public bool Enabled;
		public ulong NodeInstanceId;
		public Vector3 Center;
		public Transform3D WorldFromLocal;
		public Transform3D LocalFromWorld;
		public BoundaryLayerVolume.BoundaryShapeType ShapeType;
		public float Radius;
		public Vector3 HalfExtents;   // Box only: local-space half-extents
		public Basis InvBasis;        // Box only: inverse of world orientation
		public BoundaryLayerVolume.BoundaryExecutionMode ExecutionMode;
		public BoundaryLayerVolume.BoundaryCrossingPolicy CrossingPolicy;
		public BoundaryLayerVolume.BoundaryBehavior Behavior;
		// DirectionBias params (already in world space at snapshot time):
		public Vector3 BiasDirection;
		public float BiasStrength;
		// SceneTransform params:
		public bool HasLinkedTransform;
		public ulong LinkedNodeInstanceId;
		public int LinkedLayerIndex;
		public Transform3D LinkedWorldFromLocal;
		public Vector3 LinkedCenter;
		public float LinkedRadius;
		public float SceneTransformExitOffset;
		// Debug fields (captured at snapshot time; not used in hot path unless DebugLogCrossings is set):
		public string NodeName;
		public bool DebugLogCrossings;
	}

	public enum LedgerCrossingKind : byte
	{
		None = 0,
		Entry = 1,
		Exit = 2,
	}

	private struct BoundaryInteractionLedgerDelta
	{
		public int EventCount;
		public int BoundaryCrossings;
		public int TransformCount;
		public int EntryCount;
		public int ExitCount;
		public int LastCrossingLayer;
		public LedgerCrossingKind LastCrossingKind;
		public bool AmbiguousOrdering;

		public void RecordEvent(int layerIndex, LedgerCrossingKind kind, bool causedTransform)
		{
			if (EventCount > 0)
			{
				AmbiguousOrdering = true;
			}

			EventCount++;
			BoundaryCrossings++;
			LastCrossingLayer = layerIndex;
			LastCrossingKind = kind;
			if (causedTransform)
			{
				TransformCount++;
			}

			if (kind == LedgerCrossingKind.Entry)
			{
				EntryCount++;
			}
			else if (kind == LedgerCrossingKind.Exit)
			{
				ExitCount++;
			}
		}
	}

	public void BeginBoundaryValidationRun()
	{
		if (_boundaryLayerSnaps == null || _boundaryLayerSnaps.Length == 0)
		{
			_boundaryDebugEntryCounts = Array.Empty<int>();
			_boundaryDebugExitCounts = Array.Empty<int>();
			_boundaryDebugImpulseCount = 0;
			_boundaryDebugHasEvents = false;
			_boundaryDebugRunActive = false;
			return;
		}

		if (_boundaryDebugEntryCounts.Length != _boundaryLayerSnaps.Length)
			_boundaryDebugEntryCounts = new int[_boundaryLayerSnaps.Length];
		else
			Array.Clear(_boundaryDebugEntryCounts, 0, _boundaryDebugEntryCounts.Length);

		if (_boundaryDebugExitCounts.Length != _boundaryLayerSnaps.Length)
			_boundaryDebugExitCounts = new int[_boundaryLayerSnaps.Length];
		else
			Array.Clear(_boundaryDebugExitCounts, 0, _boundaryDebugExitCounts.Length);

		_boundaryDebugImpulseCount = 0;
		_boundaryDebugSceneTransformCount = 0;
		_boundaryDebugHasEvents = false;
		_boundaryDebugRunActive = true;
	}

	public void EmitBoundaryValidationSummary(string label = "")
	{
		if (!_boundaryDebugRunActive) return;

		int totalEntries = 0;
		int totalExits = 0;
		for (int i = 0; i < _boundaryDebugEntryCounts.Length; i++)
			totalEntries += _boundaryDebugEntryCounts[i];
		for (int i = 0; i < _boundaryDebugExitCounts.Length; i++)
			totalExits += _boundaryDebugExitCounts[i];

		string suffix = string.IsNullOrWhiteSpace(label) ? "" : $" {label}";
		GD.Print($"[BLV][Summary]{suffix} entries={totalEntries} exits={totalExits} impulses={_boundaryDebugImpulseCount} remaps={_boundaryDebugSceneTransformCount} layers={_boundaryLayerSnaps.Length}");
		for (int i = 0; i < _boundaryLayerSnaps.Length; i++)
		{
			if (_boundaryDebugEntryCounts[i] == 0 && _boundaryDebugExitCounts[i] == 0)
				continue;
			string nodeName = string.IsNullOrEmpty(_boundaryLayerSnaps[i].NodeName) ? $"layer_{i}" : _boundaryLayerSnaps[i].NodeName;
			GD.Print($"[BLV][Layer] layer={i} name='{nodeName}' entry={_boundaryDebugEntryCounts[i]} exit={_boundaryDebugExitCounts[i]}");
		}

		if (!_boundaryDebugHasEvents)
			GD.Print($"[BLV][Summary]{suffix} no crossing events recorded.");

		_boundaryDebugRunActive = false;
	}

	private void RecordBoundaryValidationEvent(int layerIndex, bool isEntry, bool isExit)
	{
		if (!_boundaryDebugRunActive) return;
		if ((uint)layerIndex >= (uint)_boundaryLayerSnaps.Length) return;
		if (isEntry) _boundaryDebugEntryCounts[layerIndex]++;
		if (isExit) _boundaryDebugExitCounts[layerIndex]++;
		if (isEntry || isExit)
		{
			_boundaryDebugImpulseCount++;
			_boundaryDebugHasEvents = true;
		}
	}

	private void RecordBoundarySceneTransformValidation()
	{
		if (!_boundaryDebugRunActive) return;
		_boundaryDebugSceneTransformCount++;
	}

	public BoundaryLayerSnap[] GetBoundaryLayerSnapsForTesting()
	{
		return _boundaryLayerSnaps ?? Array.Empty<BoundaryLayerSnap>();
	}

	public readonly struct DebugRayBundle
	{
		public readonly Vector3[] Points;     // concatenated polyline points (world)
		public readonly int[] Offsets;        // per-ray start index into Points
		public readonly int[] Counts;         // per-ray point count
		public readonly HitPayload[] Hits;    // per-ray hit payloads
		public readonly int RayCount;         // how many rays are valid this frame

		public DebugRayBundle(Vector3[] points, int[] offsets, int[] counts, HitPayload[] hits, int rayCount)
		{
			Points = points;
			Offsets = offsets;
			Counts = counts;
			Hits = hits;
			RayCount = rayCount;
		}
	}

	///
	/////////////////////////
	/////////////////////////
	// ===== Core Update Loop =====
	public override void _EnterTree()
	{
		_lifecycleInTree = true;
	}

	public override void _ExitTree()
	{
		_lifecycleInTree = false;
	}

	public override async void _Ready()
	{
		// DECISION: guard against double-init if _Ready runs again (scene reloads, etc.)
		// EFFECT: skip all setup when multimesh is already valid and in the tree.
		if (_mmi != null && IsInstanceValid(_mmi) && _mmi.IsInsideTree())
			return;

		// Resolve camera binding once on main thread; worker paths must not touch SceneTree.
		if (CameraPath != null && !CameraPath.IsEmpty)
			_cachedCamera = GetNodeOrNull<Camera3D>(CameraPath);
		else
			_cachedCamera = GetViewport()?.GetCamera3D();
		_cameraResolved = _cachedCamera != null;
		if (!_cameraResolved)
		{
			string cameraBinding = (CameraPath != null && !CameraPath.IsEmpty) ? CameraPath.ToString() : "<viewport active camera>";
			GD.PushError($"[RayBeamRenderer] Camera binding missing in _Ready. CameraPath={cameraBinding} node={GetPath()}");
		}

		// ===== Output / Debug =====
		// 1) MultiMesh Instance (billboards for ray samples)
		_mm = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			UseCustomData = false
		};

		_mmi = new MultiMeshInstance3D { Multimesh = _mm };

		// EFFECT: each sample is rendered as a quad billboard.
		var quad = new QuadMesh { Size = new Vector2(1, 1) };
		_mm.Mesh = quad;

		_mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = new Color(1, 1, 1, 1),
			EmissionEnabled = true,
			Emission = new Color(1, 1, 1, 1),
			EmissionEnergyMultiplier = 2.0f
		};

		_mmi.MaterialOverride = _mat;
		AddChild(_mmi);

		// 2) Debug mesh setup (attach ONCE — pick ONE parent)
		// DECISION: create debug mesh if missing; otherwise reuse existing.
		if (_dbgMeshInstance == null || !IsInstanceValid(_dbgMeshInstance))
		{
			_dbgImmediate = new ImmediateMesh();
			_dbgMaterial = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				VertexColorUseAsAlbedo = true,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				NoDepthTest = true
			};

			_dbgMeshInstance = new MeshInstance3D
			{
				Mesh = _dbgImmediate,
				MaterialOverride = _dbgMaterial,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				GlobalTransform = Transform3D.Identity,
				Visible = true,
				Layers = 1,
				TopLevel = false
			};

			// DECISION: keep debug mesh under this node for consistent transforms.
			AddChild(_dbgMeshInstance);

			// If you want it visible in the editor tree when running,
			// set Owner to the current edited scene root (not Owner).
			var ownerTree = GetTree();
			if (ownerTree != null)
				_dbgMeshInstance.Owner = ownerTree.EditedSceneRoot;
		}
		else
		{
			// DECISION: if it exists but is parented elsewhere, re-home it safely.
			var p = _dbgMeshInstance.GetParent();
			// DECISION: only re-parent when the parent is not this node.
			if (p != this)
			{
				p?.RemoveChild(_dbgMeshInstance);
				AddChild(_dbgMeshInstance);
			}
		}

		GD.Print($"[DBG] dbgMesh inTree={_dbgMeshInstance.IsInsideTree()} parent={_dbgMeshInstance.GetParent()?.Name} world={_dbgMeshInstance.GlobalTransform.Origin}");

		var startupTree = GetTree();
		if (!_lifecycleInTree || !IsInsideTree() || startupTree == null)
			return;

		// 3) Await frame (lets scene settle)
		await ToSignal(startupTree, SceneTree.SignalName.ProcessFrame);
		if (!_lifecycleInTree || !IsInsideTree() || GetTree() == null)
			return;

		// 4) Rebuild
		Rebuild();
	}

	public override void _Process(double delta)
	{
		// DECISION: skip dynamic rebuilds unless both toggles allow it.
		if (!UpdateEveryFrame) return;
		// DECISION: secondary gate for rebuilds when UpdateEveryFrame is enabled.
		if (!AllowRebuild) return;

		var cam = GetCamera();
		// DECISION: no camera means no rebuild.
		if (cam == null) return;
		GD.Print($"[DBG] camWorld={cam.GetWorld3D()?.GetRid()} dbgWorld={_dbgMeshInstance.GetWorld3D()?.GetRid()}");

		float beta = ReadFloat(cam, "Beta", 0f);
		float gamma = ReadFloat(cam, "Gamma", 2f);
		float focal = ReadFloat(cam, "FocalLength", 0f);

		var fieldSources = GetTree().GetNodesInGroup("field_sources");
		int fieldCount = fieldSources.Count;

		bool changed = false;

		// DECISION: camera Beta/Gamma changed (field curve parameters).
		if (!Mathf.IsEqualApprox(beta, _lastBeta) || !Mathf.IsEqualApprox(gamma, _lastGamma))
			changed = true;

		// DECISION: camera moved beyond epsilon; rays need rebuild.
		if (!IsFinite(_lastCamPos) || cam.GlobalPosition.DistanceTo(_lastCamPos) > 0.001f)
			changed = true;

		// DECISION: focal length changed; affects ray setup.
		if (float.IsNaN(_lastCamFocal) || !Mathf.IsEqualApprox(focal, _lastCamFocal))
			changed = true;

		// DECISION: field source count changed; field sampling changes.
		if (fieldCount != _lastFieldSourceCount)
			changed = true;

		// DECISION: if nothing changed, skip rebuild.
		if (!changed)
			return;

		_lastCamPos = cam.GlobalPosition;
		_lastCamFocal = focal;
		_lastFieldSourceCount = fieldCount;
		_lastBeta = beta;
		_lastGamma = gamma;

		RequestRebuild();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		// DECISION: only handle non-echo key presses.
		if (e is InputEventKey k && k.Pressed && !k.Echo)
		{
			// DECISION: F1 toggles overlay mode.
			if (k.Keycode == Key.F1)
			{
				// DECISION: toggle debug overlay mode on F1.
				// DECISION: if currently off, enable RaysAndNormals; otherwise turn off.
				DebugMode = DebugMode == DebugDrawMode.Off ? DebugDrawMode.RaysAndNormals : DebugDrawMode.Off;
				GD.Print($"[RayBeamRenderer] DebugMode = {DebugMode}");
			}
			// DECISION: F2 toggles hit-only mode.
			if (k.Keycode == Key.F2)
			{
				// DECISION: toggle hit-only debug overlay on F2.
				DebugDrawOnlyHits = !DebugDrawOnlyHits;
				GD.Print($"[RayBeamRenderer] DebugDrawOnlyHits = {DebugDrawOnlyHits}");
			}
		}
	}

	public readonly struct LifecycleStressDebugSnapshot
	{
		public readonly int QueuedOutOfTreeCount;
		public readonly int DuplicateDeferredTokenExecCount;
		public readonly int DeferredTokenExecCount;
		public readonly long LatestRequestId;
		public readonly bool LifecycleInTree;
		public readonly bool InsideTree;

		public LifecycleStressDebugSnapshot(
			int queuedOutOfTreeCount,
			int duplicateDeferredTokenExecCount,
			int deferredTokenExecCount,
			long latestRequestId,
			bool lifecycleInTree,
			bool insideTree)
		{
			QueuedOutOfTreeCount = queuedOutOfTreeCount;
			DuplicateDeferredTokenExecCount = duplicateDeferredTokenExecCount;
			DeferredTokenExecCount = deferredTokenExecCount;
			LatestRequestId = latestRequestId;
			LifecycleInTree = lifecycleInTree;
			InsideTree = insideTree;
		}
	}

	public LifecycleStressDebugSnapshot GetLifecycleStressDebugSnapshot()
	{
		return new LifecycleStressDebugSnapshot(
			_lifecycleDebugQueuedOutOfTreeCount,
			_lifecycleDebugDuplicateDeferredTokenExecCount,
			_lifecycleDebugDeferredTokenExecCount,
			_rebuildRequestId,
			_lifecycleInTree,
			IsInsideTree());
	}

	public void ResetLifecycleStressDebugCounters()
	{
		_lifecycleDebugQueuedOutOfTreeCount = 0;
		_lifecycleDebugDuplicateDeferredTokenExecCount = 0;
		_lifecycleDebugDeferredTokenExecCount = 0;
		_lifecycleDebugExecutedDeferredTokens.Clear();
	}

	public readonly struct MetricTransportDiagnosticsSnapshot
	{
		public readonly string MetricSteeringLawToken;
		public readonly int MetricDirectSteps;
		public readonly int GridBypassSteps;
		public readonly int GrinFallbackSteps;
		public readonly int GrinScalarDominatedSteps;
		public readonly int MetricDeltaZeroCount;
		public readonly int MetricDeltaNonzeroCount;
		public readonly int MetricFallbackCount;
		public readonly float MetricContributionRatio;
		public readonly int SteeringTurnCount;
		public readonly float MeanTurn;
		public readonly float MaxTurn;
		public readonly int ParallelRawCount;
		public readonly string RadialTurnSummary;
		public readonly string ZeroReasonSummary;

		public MetricTransportDiagnosticsSnapshot(
			string metricSteeringLawToken,
			int metricDirectSteps,
			int gridBypassSteps,
			int grinFallbackSteps,
			int grinScalarDominatedSteps,
			int metricDeltaZeroCount,
			int metricDeltaNonzeroCount,
			int metricFallbackCount,
			float metricContributionRatio,
			int steeringTurnCount,
			float meanTurn,
			float maxTurn,
			int parallelRawCount,
			string radialTurnSummary,
			string zeroReasonSummary)
		{
			MetricSteeringLawToken = string.IsNullOrWhiteSpace(metricSteeringLawToken)
				? GetMetricSteeringLawToken(MetricSteeringLaw.MetricLaw_CurrentEnvelope)
				: metricSteeringLawToken;
			MetricDirectSteps = metricDirectSteps;
			GridBypassSteps = gridBypassSteps;
			GrinFallbackSteps = grinFallbackSteps;
			GrinScalarDominatedSteps = grinScalarDominatedSteps;
			MetricDeltaZeroCount = metricDeltaZeroCount;
			MetricDeltaNonzeroCount = metricDeltaNonzeroCount;
			MetricFallbackCount = metricFallbackCount;
			MetricContributionRatio = metricContributionRatio;
			SteeringTurnCount = steeringTurnCount;
			MeanTurn = meanTurn;
			MaxTurn = maxTurn;
			ParallelRawCount = parallelRawCount;
			RadialTurnSummary = radialTurnSummary ?? "none";
			ZeroReasonSummary = zeroReasonSummary ?? "none";
		}
	}

	private struct DerivativeAwareStepState
	{
		public bool HasSample;
		public int SampleCount;
		public bool HysteresisEngaged;
		public int ActiveAcceptedStepCount;
		public float SmoothedK;
		public float PrevDk;
		public float SmoothedDifficulty;
	}

	private struct Pass1TelemetryDerivativeState
	{
		public bool HasSample;
		public int SampleCount;
		public float SmoothedK;
		public float PrevDk;
		public float CurvatureSum;
		public float CurvatureMax;
		public float DkMax;
		public float D2kMax;
		public float TurnSum;
		public float TurnMax;
	}

	public readonly struct DerivativeAwareSteppingDiagnosticsSnapshot
	{
		public readonly bool Enabled;
		public readonly int SampleCount;
		public readonly int CandidateAdjustmentCount;
		public readonly int HysteresisEngagedSampleCount;
		public readonly int EngagedStepCount;
		public readonly int LoggedAppliedAdjustmentCount;
		public readonly int CandidateScaleUpCount;
		public readonly int CandidateScaleDownCount;
		public readonly int ScaleUpCount;
		public readonly int ScaleDownCount;
		public readonly int HysteresisEnterCount;
		public readonly int HysteresisExitCount;
		public readonly int ActiveSpanCount;
		public readonly float MeanActiveSpanLength;
		public readonly int MaxActiveSpanLength;
		public readonly int SingleSampleSpanCount;
		public readonly int MultiSampleSpanCount;
		public readonly int MetricSubdivisionRetryCount;
		public readonly float MeanK;
		public readonly float MinK;
		public readonly float MaxK;
		public readonly float MeanAbsDk;
		public readonly float MinAbsDk;
		public readonly float MaxAbsDk;
		public readonly float MeanAbsD2k;
		public readonly float MinAbsD2k;
		public readonly float MaxAbsD2k;
		public readonly float MeanDifficulty;
		public readonly float MinDifficulty;
		public readonly float MaxDifficulty;
		public readonly float MeanStepBefore;
		public readonly float MeanStepAfter;

		public DerivativeAwareSteppingDiagnosticsSnapshot(
			bool enabled,
			int sampleCount,
			int candidateAdjustmentCount,
			int hysteresisEngagedSampleCount,
			int engagedStepCount,
			int loggedAppliedAdjustmentCount,
			int candidateScaleUpCount,
			int candidateScaleDownCount,
			int scaleUpCount,
			int scaleDownCount,
			int hysteresisEnterCount,
			int hysteresisExitCount,
			int activeSpanCount,
			float meanActiveSpanLength,
			int maxActiveSpanLength,
			int singleSampleSpanCount,
			int multiSampleSpanCount,
			int metricSubdivisionRetryCount,
			float meanK,
			float minK,
			float maxK,
			float meanAbsDk,
			float minAbsDk,
			float maxAbsDk,
			float meanAbsD2k,
			float minAbsD2k,
			float maxAbsD2k,
			float meanDifficulty,
			float minDifficulty,
			float maxDifficulty,
			float meanStepBefore,
			float meanStepAfter)
		{
			Enabled = enabled;
			SampleCount = sampleCount;
			CandidateAdjustmentCount = candidateAdjustmentCount;
			HysteresisEngagedSampleCount = hysteresisEngagedSampleCount;
			EngagedStepCount = engagedStepCount;
			LoggedAppliedAdjustmentCount = loggedAppliedAdjustmentCount;
			CandidateScaleUpCount = candidateScaleUpCount;
			CandidateScaleDownCount = candidateScaleDownCount;
			ScaleUpCount = scaleUpCount;
			ScaleDownCount = scaleDownCount;
			HysteresisEnterCount = hysteresisEnterCount;
			HysteresisExitCount = hysteresisExitCount;
			ActiveSpanCount = activeSpanCount;
			MeanActiveSpanLength = meanActiveSpanLength;
			MaxActiveSpanLength = maxActiveSpanLength;
			SingleSampleSpanCount = singleSampleSpanCount;
			MultiSampleSpanCount = multiSampleSpanCount;
			MetricSubdivisionRetryCount = metricSubdivisionRetryCount;
			MeanK = meanK;
			MinK = minK;
			MaxK = maxK;
			MeanAbsDk = meanAbsDk;
			MinAbsDk = minAbsDk;
			MaxAbsDk = maxAbsDk;
			MeanAbsD2k = meanAbsD2k;
			MinAbsD2k = minAbsD2k;
			MaxAbsD2k = maxAbsD2k;
			MeanDifficulty = meanDifficulty;
			MinDifficulty = minDifficulty;
			MaxDifficulty = maxDifficulty;
			MeanStepBefore = meanStepBefore;
			MeanStepAfter = meanStepAfter;
		}
	}

	public MetricTransportDiagnosticsSnapshot GetMetricTransportDiagnosticsSnapshot()
	{
		int totalResolved = _metricContributionAppliedCount + _metricFallbackCount;
		float contributionRatio = totalResolved > 0
			? _metricContributionAppliedCount / (float)totalResolved
			: 0f;
		float meanTurn = _transportSteeringTurnCount > 0
			? (float)(_transportSteeringTurnSum / _transportSteeringTurnCount)
			: 0f;
		return new MetricTransportDiagnosticsSnapshot(
			GetMetricSteeringLawToken(GetEffectiveMetricSteeringLaw()),
			_metricContributionAppliedCount,
			_metricGridBypassStepCount,
			_metricFallbackCount,
			_metricScalarDominatedStepCount,
			_metricDeltaZeroCount,
			_metricDeltaNonzeroCount,
			_metricFallbackCount,
			contributionRatio,
			_transportSteeringTurnCount,
			meanTurn,
			_transportSteeringMaxTurn,
			_metricParallelRawCount,
			BuildTransportSteeringRadialSummary(),
			BuildMetricZeroReasonSummary());
	}

	public DerivativeAwareSteppingDiagnosticsSnapshot GetDerivativeAwareSteppingDiagnosticsSnapshot()
	{
		int samples = _derivativeAwareSampleCount;
		float meanK = samples > 0 ? (float)(_derivativeAwareKSum / samples) : 0f;
		float meanAbsDk = samples > 0 ? (float)(_derivativeAwareAbsDkSum / samples) : 0f;
		float meanAbsD2k = samples > 0 ? (float)(_derivativeAwareAbsD2kSum / samples) : 0f;
		float meanDifficulty = samples > 0 ? (float)(_derivativeAwareDifficultySum / samples) : 0f;
		float meanStepBefore = samples > 0 ? (float)(_derivativeAwareStepBeforeSum / samples) : 0f;
		float meanStepAfter = samples > 0 ? (float)(_derivativeAwareStepAfterSum / samples) : 0f;
		float meanActiveSpanLength = _derivativeAwareActiveSpanCount > 0
			? (float)(_derivativeAwareActiveSpanLengthSum / _derivativeAwareActiveSpanCount)
			: 0f;
		return new DerivativeAwareSteppingDiagnosticsSnapshot(
			IsDerivativeAwareSteppingEnabled(),
			samples,
			_derivativeAwareCandidateAdjustmentCount,
			_derivativeAwareHysteresisEngagedSampleCount,
			_derivativeAwareEngagedStepCount,
			_derivativeAwareLoggedAppliedAdjustmentCount,
			_derivativeAwareCandidateScaleUpCount,
			_derivativeAwareCandidateScaleDownCount,
			_derivativeAwareScaleUpCount,
			_derivativeAwareScaleDownCount,
			_derivativeAwareHysteresisEnterCount,
			_derivativeAwareHysteresisExitCount,
			_derivativeAwareActiveSpanCount,
			meanActiveSpanLength,
			_derivativeAwareActiveSpanMaxLength,
			_derivativeAwareSingleSampleSpanCount,
			_derivativeAwareMultiSampleSpanCount,
			_derivativeAwareMetricSubdivisionRetryCount,
			meanK,
			samples > 0 ? _derivativeAwareKMin : 0f,
			samples > 0 ? _derivativeAwareKMax : 0f,
			meanAbsDk,
			samples > 0 ? _derivativeAwareAbsDkMin : 0f,
			samples > 0 ? _derivativeAwareAbsDkMax : 0f,
			meanAbsD2k,
			samples > 0 ? _derivativeAwareAbsD2kMin : 0f,
			samples > 0 ? _derivativeAwareAbsD2kMax : 0f,
			meanDifficulty,
			samples > 0 ? _derivativeAwareDifficultyMin : 0f,
			samples > 0 ? _derivativeAwareDifficultyMax : 0f,
			meanStepBefore,
			meanStepAfter);
	}

	public void ResetMetricTransportDiagnostics()
	{
		_loggedMetricSteeringLaw = false;
		_metricDeltaZeroCount = 0;
		_metricDeltaNonzeroCount = 0;
		_metricFallbackCount = 0;
		_metricContributionAppliedCount = 0;
		_metricGridBypassStepCount = 0;
		_metricScalarDominatedStepCount = 0;
		_metricParallelRawCount = 0;
		_metricParallelPreferredCount = 0;
		_metricParallelRecoveredCount = 0;
		_metricParallelFallbackCount = 0;
		_metricZeroReasonParallelCount = 0;
		_metricZeroReasonPerpEpsilonCount = 0;
		_metricZeroReasonRadiusLowCount = 0;
		_metricZeroReasonRadiusHighCount = 0;
		_metricZeroReasonNonFiniteCount = 0;
		_metricZeroReasonCoincidentCount = 0;
		_metricZeroReasonStepGuardCount = 0;
		_metricZeroReasonZeroTurnCount = 0;
		_metricDiagnosticEvalCount = 0;
		_metricDiagnosticSampleLogsEmitted = 0;
		_metricParallelDiagnosticSampleLogsEmitted = 0;
		_transportSteeringTurnSum = 0.0;
		_transportSteeringMaxTurn = 0f;
		_transportSteeringTurnCount = 0;
		Array.Clear(_transportSteeringRadialTurnSums, 0, _transportSteeringRadialTurnSums.Length);
		Array.Clear(_transportSteeringRadialCounts, 0, _transportSteeringRadialCounts.Length);
		ResetDerivativeAwareSteppingDiagnostics();
	}

	public void ResetDerivativeAwareSteppingDiagnostics()
	{
		_derivativeAwareKSum = 0.0;
		_derivativeAwareKMin = float.PositiveInfinity;
		_derivativeAwareKMax = 0f;
		_derivativeAwareAbsDkSum = 0.0;
		_derivativeAwareAbsDkMin = float.PositiveInfinity;
		_derivativeAwareAbsDkMax = 0f;
		_derivativeAwareAbsD2kSum = 0.0;
		_derivativeAwareAbsD2kMin = float.PositiveInfinity;
		_derivativeAwareAbsD2kMax = 0f;
		_derivativeAwareDifficultySum = 0.0;
		_derivativeAwareDifficultyMin = float.PositiveInfinity;
		_derivativeAwareDifficultyMax = 0f;
		_derivativeAwareStepBeforeSum = 0.0;
		_derivativeAwareStepAfterSum = 0.0;
		_derivativeAwareSampleCount = 0;
		_derivativeAwareCandidateAdjustmentCount = 0;
		_derivativeAwareHysteresisEngagedSampleCount = 0;
		_derivativeAwareEngagedStepCount = 0;
		_derivativeAwareLoggedAppliedAdjustmentCount = 0;
		_derivativeAwareCandidateScaleUpCount = 0;
		_derivativeAwareCandidateScaleDownCount = 0;
		_derivativeAwareScaleUpCount = 0;
		_derivativeAwareScaleDownCount = 0;
		_derivativeAwareHysteresisEnterCount = 0;
		_derivativeAwareHysteresisExitCount = 0;
		_derivativeAwareActiveSpanCount = 0;
		_derivativeAwareActiveSpanLengthSum = 0.0;
		_derivativeAwareActiveSpanMaxLength = 0;
		_derivativeAwareSingleSampleSpanCount = 0;
		_derivativeAwareMultiSampleSpanCount = 0;
		_derivativeAwareMetricSubdivisionRetryCount = 0;
	}

	private void RecordDerivativeAwareActiveSpan(int spanLength)
	{
		if (spanLength <= 0)
		{
			return;
		}

		_derivativeAwareActiveSpanCount++;
		_derivativeAwareActiveSpanLengthSum += spanLength;
		_derivativeAwareActiveSpanMaxLength = Mathf.Max(_derivativeAwareActiveSpanMaxLength, spanLength);
		if (spanLength <= 1)
		{
			_derivativeAwareSingleSampleSpanCount++;
		}
		else
		{
			_derivativeAwareMultiSampleSpanCount++;
		}
	}

	private void FinalizeDerivativeAwareStepState(ref DerivativeAwareStepState state)
	{
		if (state.HysteresisEngaged && state.ActiveAcceptedStepCount > 0)
		{
			RecordDerivativeAwareActiveSpan(state.ActiveAcceptedStepCount);
		}

		state.HysteresisEngaged = false;
		state.ActiveAcceptedStepCount = 0;
	}

	public Vector3 ComputeActiveTransportAccelerationForDiagnostics(
		Vector3 p,
		Vector3 v,
		Vector3 preferredBendDir,
		Vector3 center,
		float beta,
		float gamma,
		FieldSourceSnap[] fieldSources,
		bool hasSources)
	{
		TransportModel active = hasSources ? ResolveActiveTransportModel(fieldSources) : TransportModel.GRIN_Optical;
		return ComputeTransportAccelerationForActiveModel(active, p, v, preferredBendDir, center, beta, gamma, fieldSources, hasSources);
	}

	public bool TryStepActiveTransportForDiagnostics(
		Vector3 p,
		Vector3 v,
		Vector3 center,
		float beta,
		float gamma,
		FieldSourceSnap[] fieldSources,
		bool hasSources,
		float maxDistance,
		float traveled,
		float minStep,
		float maxStep,
		FieldGrid3D fieldGrid,
		out Vector3 next,
		out Vector3 vNext,
		out float step)
	{
		DerivativeAwareStepState derivativeState = default;
		bool stepped = TryStepIntegratedTransport(
			p,
			v,
			center,
			beta,
			gamma,
			fieldSources,
			hasSources,
			maxDistance,
			traveled,
			minStep,
			maxStep,
			fieldGrid,
			applyLowCurvatureBoost: false,
			ref derivativeState,
			out next,
			out vNext,
			out _,
			out step);
		FinalizeDerivativeAwareStepState(ref derivativeState);
		return stepped;
	}

	private string BuildMetricZeroReasonSummary()
	{
		var parts = new List<string>(10);
		if (_metricParallelRawCount > 0) parts.Add($"parallel_raw={_metricParallelRawCount}");
		if (_metricParallelPreferredCount > 0) parts.Add($"parallel_preferred={_metricParallelPreferredCount}");
		if (_metricParallelRecoveredCount > 0) parts.Add($"parallel_recovered={_metricParallelRecoveredCount}");
		if (_metricParallelFallbackCount > 0) parts.Add($"parallel_fallback={_metricParallelFallbackCount}");
		if (_metricZeroReasonPerpEpsilonCount > 0) parts.Add($"perp_eps={_metricZeroReasonPerpEpsilonCount}");
		if (_metricZeroReasonRadiusLowCount > 0) parts.Add($"r_lo={_metricZeroReasonRadiusLowCount}");
		if (_metricZeroReasonRadiusHighCount > 0) parts.Add($"r_hi={_metricZeroReasonRadiusHighCount}");
		if (_metricZeroReasonNonFiniteCount > 0) parts.Add($"nonfinite={_metricZeroReasonNonFiniteCount}");
		if (_metricZeroReasonCoincidentCount > 0) parts.Add($"coincident={_metricZeroReasonCoincidentCount}");
		if (_metricZeroReasonStepGuardCount > 0) parts.Add($"step={_metricZeroReasonStepGuardCount}");
		if (_metricZeroReasonZeroTurnCount > 0) parts.Add($"dtheta0={_metricZeroReasonZeroTurnCount}");
		return parts.Count > 0 ? string.Join(",", parts) : "none";
	}

	private string BuildTransportSteeringRadialSummary()
	{
		var parts = new string[TransportSteeringBucketCount];
		for (int i = 0; i < TransportSteeringBucketCount; i++)
		{
			int count = _transportSteeringRadialCounts[i];
			float meanTurn = count > 0
				? (float)(_transportSteeringRadialTurnSums[i] / count)
				: 0f;
			parts[i] = $"{GetTransportSteeringBucketLabel(i)}:{meanTurn:0.######}@{count}";
		}

		return string.Join(",", parts);
	}

	private void RequestRebuild()
	{
		// DECISION: if a rebuild is already running, just queue another.
		if (_rebuildInProgress)
		{
			_rebuildQueued = true;
			return;
		}
		// DECISION: avoid double-queuing.
		if (_rebuildQueued) return;

		QueueDeferredRebuild();
	}

	private void QueueDeferredRebuild()
	{
		if (!_lifecycleInTree || !IsInsideTree() || GetTree() == null)
		{
			_lifecycleDebugQueuedOutOfTreeCount++;
		}
		_rebuildQueued = true;
		long requestId = unchecked(++_rebuildRequestId);
		CallDeferred(nameof(DoRebuildDeferred), requestId);
	}

	private async void DoRebuildDeferred(long requestId)
	{
		bool handedOffToRebuild = false;
		try
		{
			if (!GodotObject.IsInstanceValid(this))
			{
				if (DebugRender)
					GD.Print("[RayBeamRenderer] DoRebuildDeferred abandon reason=freed");
				return;
			}

			if (requestId != _rebuildRequestId)
				return;

			if (DebugRender)
				GD.Print($"[RayBeamRenderer] DoRebuildDeferred enter id={requestId}");

			if (IsQueuedForDeletion())
			{
				if (DebugRender)
					GD.Print("[RayBeamRenderer] DoRebuildDeferred abandon reason=queued_for_delete");
				return;
			}

			if (!_lifecycleInTree)
			{
				if (DebugRender)
					GD.Print("[RayBeamRenderer] DoRebuildDeferred abandon reason=detached");
				return;
			}

			if (!IsInsideTree())
			{
				if (DebugRender)
					GD.Print("[RayBeamRenderer] DoRebuildDeferred abandon reason=detached");
				return;
			}
			var tree = GetTree();
			if (tree == null)
			{
				if (DebugRender)
					GD.Print("[RayBeamRenderer] DoRebuildDeferred abandon reason=detached");
				return;
			}

			// DECISION: ProcessFrame is safer than PhysicsFrame during scene switches / teardown.
			await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

			if (!GodotObject.IsInstanceValid(this))
			{
				if (DebugRender)
					GD.Print("[RayBeamRenderer] DoRebuildDeferred abandon reason=freed");
				return;
			}

			if (requestId != _rebuildRequestId)
				return;

			if (IsQueuedForDeletion())
			{
				if (DebugRender)
					GD.Print("[RayBeamRenderer] DoRebuildDeferred abandon reason=queued_for_delete");
				return;
			}

			if (!_lifecycleInTree)
			{
				if (DebugRender)
					GD.Print("[RayBeamRenderer] DoRebuildDeferred abandon reason=detached");
				return;
			}

			if (!IsInsideTree() || GetTree() == null)
			{
				if (DebugRender)
					GD.Print("[RayBeamRenderer] DoRebuildDeferred abandon reason=detached");
				return;
			}

			// Clear only at handoff to Rebuild(); requests arriving during Rebuild() can re-set this flag.
			if (!_lifecycleDebugExecutedDeferredTokens.Add(requestId))
			{
				_lifecycleDebugDuplicateDeferredTokenExecCount++;
			}
			_lifecycleDebugDeferredTokenExecCount++;
			_rebuildQueued = false;
			handedOffToRebuild = true;
			Rebuild();
		}
		finally
		{
			if (!handedOffToRebuild && GodotObject.IsInstanceValid(this) && requestId == _rebuildRequestId)
				_rebuildQueued = false;
		}
	}

	public Camera3D GetCamera()
	{
		// Return only the cached camera; never touch SceneTree here.
		return _cachedCamera;
	}

	private void Rebuild()
	{
		// DECISION: Rebuild can be requested during startup / scene transitions before this node is attached.
		if (!_lifecycleInTree || !IsInsideTree() || GetTree() == null)
		{
			if (!_rebuildQueued)
			{
				if (DebugRender)
					GD.Print("[RayBeamRenderer] rebuild deferred: not in tree");
				QueueDeferredRebuild();
			}
			return;
		}

		GD.Print("Rebuild ENTER");
		GD.Print($"Rebuild on node: {GetPath()}  TerminateTrailOnHit={TerminateTrailOnHit} StopOnHit={StopOnHit} RequireHitToRender={RequireHitToRender}");
		GD.Print($"READY node: {GetPath()}  Script={GetScript()}  TerminateTrailOnHit={TerminateTrailOnHit}");

		// DECISION: prevent concurrent rebuilds.
		if (_rebuildInProgress) return;
		_rebuildInProgress = true;

		try
		{
			var cam = GetCamera();
			// DECISION: abort rebuild if no camera.
			if (cam == null) return;

			RefreshInsightPlane();

			// DECISION: choose field center (camera-driven vs fixed).
			// EFFECT: changes origin for field acceleration computations.
			// DECISION: choose camera center vs fixed field center.
			Vector3 center = FieldCenterIsCamera ? cam.GlobalPosition : FieldCenter;

			float beta = ReadFloat(cam, "Beta", 0f);
			float gamma = ReadFloat(cam, "Gamma", 2f);

			_lastBeta = beta;
			_lastGamma = gamma;
			ResetMetricTransportDiagnostics();

			var fieldSources = GetTree().GetNodesInGroup("field_sources");
			GD.Print("RayBeamRenderer: field sources in group = ", fieldSources.Count);
			FieldSourceSnap[] fieldSourceSnaps = SnapshotFieldSources(fieldSources);
			bool hasSources = fieldSourceSnaps.Length > 0;
			TransportModel rebuildTransportModel = hasSources ? ResolveActiveTransportModel(fieldSourceSnaps) : TransportModel.GRIN_Optical;

			// Snapshot boundary layer volumes for this frame (read-only during integration).
			var blvNodes = GetTree().GetNodesInGroup("boundary_layer_volumes");
			_boundaryLayerSnaps = SnapshotBoundaryLayers(blvNodes);
			_hasBoundaryLayers = _boundaryLayerSnaps.Length > 0;
			GD.Print("RayBeamRenderer: boundary layers in group = ", blvNodes.Count, " snapped = ", _boundaryLayerSnaps.Length, " integrated = ", UseIntegratedField);

			var emitters = GetTree().GetNodesInGroup("ray_emitters");
			int emitterCount = emitters.Count;
			// DECISION: no emitters => no instances.
			if (emitterCount == 0)
			{
				_mm.InstanceCount = 0;
				return;
			}
			GD.Print("RayBeamRenderer: emitters in group = ", emitters.Count);

			int total = 0;
			int capacity = total;
			_mm.InstanceCount = capacity;
			var emitterList = new List<RayEmitter3D>(emitterCount);

			// DECISION: iterate all emitter nodes to build emitter list and capacity.
			foreach (var node in emitters)
			{
				// DECISION: only RayEmitter3D nodes contribute.
				if (node is RayEmitter3D e)
				{
					emitterList.Add(e);
					total += Math.Max(1, e.Rays) * (StepsPerRay + 1);
				}
			}

			///
			////////////////////
			int raysTotal = 0;
			// DECISION: count total rays across all emitters.
			foreach (var e in emitterList)
				raysTotal += Math.Max(1, e.Rays);

			int maxSamplesPerRay = ComputeMaxSamplesPerRay();
			int maxTotalSamples = raysTotal * maxSamplesPerRay;

			EnsureCapacity(raysTotal, maxTotalSamples);
			_sampleWriteHead = 0;
			_rayWriteHead = 0;
			//////////
			///

			_mm.InstanceCount = total;
			_mm.VisibleInstanceCount = total; // default: show all until we decide otherwise
			GD.Print($"RayBeamRenderer: total instances target = {total}");

			Vector3 camRight = cam.GlobalTransform.Basis.X.Normalized();
			Vector3 camUp = cam.GlobalTransform.Basis.Y.Normalized();
			Vector3 camForward = (-cam.GlobalTransform.Basis.Z).Normalized();

			int idx = 0;
			var rng = new Random(12345);

			PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;

			float minStep = Mathf.Min(MinStepLength, MaxStepLength);
			float maxStep = Mathf.Max(MinStepLength, MaxStepLength);
			minStep = Mathf.Max(0.0001f, minStep);

			int hitCount = 0;
			bool capacityExhausted = false;

			// DECISION: iterate each emitter to simulate its rays.
			foreach (var e in emitterList)
			{
				// DECISION: stop emitting if we exceeded instance capacity.
				if (capacityExhausted) break;

				Color baseC = e.RayColor;
				float maxDist = e.MaxDistance;

				int rays = Math.Max(1, e.Rays);
				int rayOrdinal = 0;
				float spreadRad = Mathf.DegToRad(e.SpreadDegrees);

				Vector3 origin = e.GlobalTransform.Origin;

				// DECISION: iterate each ray emitted by this emitter.
				for (int r = 0; r < rays; r++)
				{
					rayOrdinal++;

					// DECISION: reset debug reject counter per emitter's first ray.
					if (rayOrdinal == 1) _dbgRejectPrints = 0;

					bool debugThisRay = DebugRender && (rayOrdinal % Mathf.Max(1, DebugEveryNRays) == 0);

					// DECISION: emit per-ray debug logs only on debug cadence.
					if (debugThisRay)
						GD.Print($"[DBG] Ray#{rayOrdinal} start RequireHitToRender={RequireHitToRender} StopOnHit={StopOnHit}");

					Vector3 localDir;
					// DECISION: choose deterministic fan pattern or random cone sample.
					if (e.UseFan)
					{
						float yawTotal = Mathf.DegToRad(e.FanYawDegrees);
						float pitch = Mathf.DegToRad(e.FanPitchDegrees);

						// DECISION: normalize index to [0..1], handle single-ray case.
						float u = (rays == 1) ? 0.0f : (float)r / (rays - 1);
						float yaw = Mathf.Lerp(-yawTotal * 0.5f, yawTotal * 0.5f, u);

						localDir = new Vector3(0, 0, -1);
						localDir = localDir.Rotated(Vector3.Up, yaw);
						localDir = localDir.Rotated(Vector3.Right, pitch);
					}
					else
					{
						localDir = RandomInCone(rng, spreadRad);
					}

					Vector3 dir = (e.GlobalTransform.Basis * localDir).Normalized();

					float dx = dir.Dot(camRight);
					float dy = dir.Dot(camUp);
					Vector2 d2 = new Vector2(dx, -dy);
					// DECISION: normalize direction unless degenerate; fall back to +X.
					Vector2 d2n = d2.Length() > 1e-6f ? d2 / d2.Length() : Vector2.Right;
					Vector3 bendDir = (camRight * d2n.X + camUp * -d2n.Y).Normalized();

					// EFFECT: simulate into shared buffers, then render from arrays.
					HitPayload hit;
					RayMeta meta = SimulateRay(
						space,
						e,
						origin,
						dir,
						bendDir,
						center,
						beta,
						gamma,
						fieldSourceSnaps,
						hasSources,
						CollisionMask,
						out hit
					);

					// DECISION: count hit rays for reporting/stats.
					if (meta.HadHit) hitCount++;

					// DECISION: print hit details only when debug enabled for this ray.
					if (debugThisRay && meta.HadHit)
						GD.Print($"[DBG] HIT collider='{hit.ColliderName}' id={hit.ColliderId} pos={hit.Position}");
					if (debugThisRay && hit.Absorbed == 1)
						GD.Print($"[DBG] ABSORB reason={hit.TerminationReason} distance={hit.Distance:0.###}");

					// DECISION: print per-ray meta only when debug enabled for this ray.
					if (debugThisRay)
						GD.Print($"[DBG] Ray#{rayOrdinal} meta.HadHit={meta.HadHit} meta.RenderCount={meta.RenderCount}");

					_rayMeta[_rayWriteHead] = meta;
					_hitPayload[_rayWriteHead] = hit;

					// DECISION: optionally require a hit to render a ray.
					// DECISION: optionally require a hit to render a ray.
					if (!RequireHitToRender || meta.HadHit)
					{
						// DECISION: print sample counts only when debug enabled for this ray.
						if (debugThisRay)
							GD.Print($"Ray#{rayOrdinal} hadHit={meta.HadHit} samples={meta.SampleCount} renderCount={meta.RenderCount}");

						// DECISION: emit each renderable sample for this ray.
						for (int i = 0; i < meta.RenderCount; i++)
						{
							// DECISION: prevent overflow of instance buffer.
							if (idx >= _mm.InstanceCount)
							{
								capacityExhausted = true;
								break;
							}

							int si = meta.SampleStart + i;
							SetBillboardInstance(idx++, capacity, _samplePos[si], camRight, camUp, camForward, _sampleCol[si]);
						}

						// DECISION: draw hit marker only when enabled and capacity allows.
						if (DrawHitMarker && meta.HadHit && idx < _mm.InstanceCount)
						{
							SetBillboardInstance(idx++, capacity, hit.Position, camRight, camUp, camForward, HitMarkerColor);
						}
					}

					_rayWriteHead++;
				}

			}

			// ✅ Always trim to what we actually wrote (prevents stale transforms/colors)
			//_mm.InstanceCount = idx;
			//_mm.VisibleInstanceCount = idx; 
			// EFFECT: trim visible instances to what we actually wrote (prevents stale transforms/colors).
			_mm.VisibleInstanceCount = Mathf.Max(0, idx); // ✅ show only the instances we wrote

			// DECISION: optional debug summary per rebuild.
			if (DebugRender)
			{
				GD.Print($"[DBG] Rebuild summary: totalTarget={total} idxWritten={idx} InstanceCount={_mm.InstanceCount} VisibleCount={_mm.VisibleInstanceCount} hits={hitCount}");
			}
			if (rebuildTransportModel == TransportModel.Metric_NullGeodesic)
			{
				MetricTransportDiagnosticsSnapshot metricDiagnostics = GetMetricTransportDiagnosticsSnapshot();
				GD.Print(
					$"[Transport][MetricDiagSummary] metricLaw={metricDiagnostics.MetricSteeringLawToken} metricDeltaZeroCount={metricDiagnostics.MetricDeltaZeroCount} " +
					$"metricDeltaNonzeroCount={metricDiagnostics.MetricDeltaNonzeroCount} metricFallbackCount={metricDiagnostics.MetricFallbackCount} " +
					$"metricContributionRatio={metricDiagnostics.MetricContributionRatio:0.######} zeroReasons={metricDiagnostics.ZeroReasonSummary}");
			}
			if (IsDerivativeAwareSteppingEnabled() && DerivativeAwareLogMetrics)
			{
				DerivativeAwareSteppingDiagnosticsSnapshot derivativeDiagnostics = GetDerivativeAwareSteppingDiagnosticsSnapshot();
				GD.Print(
					$"[Transport][DerivativeStepSummary] samples={derivativeDiagnostics.SampleCount} candidateAdjust={derivativeDiagnostics.CandidateAdjustmentCount} " +
					$"hysteresisActive={derivativeDiagnostics.HysteresisEngagedSampleCount} hysteresisEnter={derivativeDiagnostics.HysteresisEnterCount} hysteresisExit={derivativeDiagnostics.HysteresisExitCount} " +
					$"activeSpans={derivativeDiagnostics.ActiveSpanCount} meanActiveSpan={derivativeDiagnostics.MeanActiveSpanLength:0.###} maxActiveSpan={derivativeDiagnostics.MaxActiveSpanLength} " +
					$"singleSampleSpans={derivativeDiagnostics.SingleSampleSpanCount} multiSampleSpans={derivativeDiagnostics.MultiSampleSpanCount} " +
					$"appliedAdjust={derivativeDiagnostics.EngagedStepCount} loggedApplied={derivativeDiagnostics.LoggedAppliedAdjustmentCount} " +
					$"kMean={derivativeDiagnostics.MeanK:0.######} kMin={derivativeDiagnostics.MinK:0.######} kMax={derivativeDiagnostics.MaxK:0.######} " +
					$"absDkMean={derivativeDiagnostics.MeanAbsDk:0.######} absDkMax={derivativeDiagnostics.MaxAbsDk:0.######} " +
					$"absD2kMean={derivativeDiagnostics.MeanAbsD2k:0.######} absD2kMax={derivativeDiagnostics.MaxAbsD2k:0.######} " +
					$"difficultyMean={derivativeDiagnostics.MeanDifficulty:0.######} difficultyMax={derivativeDiagnostics.MaxDifficulty:0.######} " +
					$"stepBeforeMean={derivativeDiagnostics.MeanStepBefore:0.######} stepAfterMean={derivativeDiagnostics.MeanStepAfter:0.######} " +
					$"candidateScaleUp={derivativeDiagnostics.CandidateScaleUpCount} candidateScaleDown={derivativeDiagnostics.CandidateScaleDownCount} " +
					$"scaleUp={derivativeDiagnostics.ScaleUpCount} scaleDown={derivativeDiagnostics.ScaleDownCount} " +
					$"metricRetries={derivativeDiagnostics.MetricSubdivisionRetryCount}");
			}

			// =======================
			// Debug overlay draw (ImmediateMesh)
			// =======================
			// DECISION: only draw local debug overlay when film pass is not the owner.
			if (!DebugOverlayOwnedByFilm)
			{
				UpdateDebugOverlay(cam, _rayWriteHead);
			}

		}
		finally
		{
			_rebuildInProgress = false;
		}

		GD.Print("Rebuild EXIT");
	}

	//private void SetBillboardInstance(int index, Vector3 pos, Vector3 camRight, Vector3 camUp, Vector3 camForward, Color c)
	private void SetBillboardInstance(int index, int capacity, Vector3 pos, Vector3 camRight, Vector3 camUp, Vector3 camForward, Color c)
	{
		// DECISION: cannot set transforms without a MultiMesh.
		if (_mm == null) return;

		// DECISION: reject out-of-range instance indices.
		if (index < 0 || index >= _mm.InstanceCount)
		{
			// DEBUG VISIBILITY: limited reject logging to avoid spam.
			if (DebugRender && DebugSetBillboardRejects && _dbgRejectPrints < DebugMaxRejectPrints)
			{
				_dbgRejectPrints++;
				GD.Print($"[DBG] SetBillboard SKIP: index {index} out of range (InstanceCount={_mm.InstanceCount})");
			}
			return;
		}

		// DECISION: reject NaN/Inf positions to avoid corrupt transforms.
		if (!IsFinite(pos))
		{
			// DEBUG VISIBILITY: limited reject logging to avoid spam.
			if (DebugRender && DebugSetBillboardRejects && _dbgRejectPrints < DebugMaxRejectPrints)
			{
				_dbgRejectPrints++;
				GD.Print($"[DBG] SetBillboard SKIP: pos non-finite: {pos}");
			}
			return;
		}

		// CONTROL FACTOR: QuadSize controls billboard scale (world units).
		float s = QuadSize;

		var basis = new Basis(
			camRight * s,
			camUp * s,
			camForward * s
		);

		// DECISION: reject non-finite basis vectors to avoid invalid transforms.
		if (!IsFinite(basis.X) || !IsFinite(basis.Y) || !IsFinite(basis.Z))
		{
			// DEBUG VISIBILITY: limited reject logging to avoid spam.
			if (DebugRender && DebugSetBillboardRejects && _dbgRejectPrints < DebugMaxRejectPrints)
			{
				_dbgRejectPrints++;
				GD.Print($"[DBG] SetBillboard SKIP: basis non-finite X={basis.X} Y={basis.Y} Z={basis.Z}");
			}
			return;
		}

		var xform = new Transform3D(basis, pos);
		_mm.SetInstanceTransform(index, xform);
		_mm.SetInstanceColor(index, c);
	}

	private Vector3 ComputeAccelerationAtPoint(Vector3 p, Godot.Collections.Array<Node> sources, float globalBeta, float globalGamma)
	{
		_ = globalGamma;
		Vector3 aSum = Vector3.Zero;

		// DECISION: accumulate acceleration from each field source node.
		foreach (var n in sources)
		{
			// DECISION: only process FieldSource3D nodes.
			if (n is not FieldSource3D fs) continue;
			FieldSourceSnap snap = BuildFieldSourceSnap(fs);
			// DECISION: skip disabled field sources.
			if (!snap.Enabled) continue;

			FieldMath.EvalResult eval = FieldMath.EvalFieldAccel(
				p,
				snap.Center,
				snap.CurveType,
				snap.RInner,
				snap.ROuter,
				snap.Amp,
				snap.CurveA,
				snap.Sigma,
				snap.CurveA,
				snap.CurveB,
				snap.CurveC,
				snap.CustomCurve,
				snap.ModeFlags,
				globalBeta,
				snap.OverrideBetaScale,
				snap.BetaScale,
				snap.EdgeSoftness);

			aSum += eval.Acceleration * BendScale * FieldStrength;
		}

		return aSum;
	}

	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
	private static float FastPow(float r, float gamma)
	{
		// DECISION: fast-path common integer gamma values to avoid Mathf.Pow overhead.
		if (gamma == -2f) return 1f / (r * r);
		if (gamma == -1f) return 1f / r;
		if (gamma ==  0f) return 1f;
		if (gamma ==  1f) return r;
		if (gamma ==  2f) return r * r;
		return Mathf.Pow(r, gamma);
	}

	private static float ReadFloat(Node obj, StringName prop, float fallback)
	{
		// DECISION: missing object yields fallback.
		if (obj == null) return fallback;

		Variant v = obj.Get(prop);
		// DECISION: accept float/int; otherwise fallback.
		return v.VariantType switch
		{
			Variant.Type.Float => (float)v,
			Variant.Type.Int => (int)v,
			_ => fallback
		};
	}

	private static Vector3 RandomInCone(Random rng, float coneAngleRad)
	{
		double u = rng.NextDouble();
		double v = rng.NextDouble();

		float cosTheta = Mathf.Lerp(1.0f, Mathf.Cos(coneAngleRad), (float)u);
		float sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTheta * cosTheta));
		float phi = Mathf.Tau * (float)v;

		float x = sinTheta * Mathf.Cos(phi);
		float y = sinTheta * Mathf.Sin(phi);
		float z = -cosTheta;

		return new Vector3(x, y, z).Normalized();
	}

	public static bool SegmentCrossesPlane(Vector3 p, Vector3 q, Plane plane, float eps = 0.001f)
	{
		float dp = plane.DistanceTo(p);
		float dq = plane.DistanceTo(q);

		float slab = eps * 10.0f;
		// DECISION: treat near-plane endpoints as crossing.
		if (Mathf.Abs(dp) <= slab || Mathf.Abs(dq) <= slab)
			return true;

		// DECISION: crossing occurs when endpoints are on opposite sides.
		return (dp > 0f) != (dq > 0f);
	}

	private void RefreshInsightPlane()
	{
		_hasInsightPlane = false;
		// DECISION: no node path means no insight plane.
		if (InsightPlaneNode == null || InsightPlaneNode.IsEmpty) return;

		var n = GetNodeOrNull<Node3D>(InsightPlaneNode);
		// DECISION: invalid node path means no insight plane.
		if (n == null) return;

		Vector3 normal = n.GlobalTransform.Basis.Y.Normalized();
		Vector3 point = n.GlobalPosition;

		_insightPlane = new Plane(normal, point);
		_hasInsightPlane = true;
	}

	public static bool SweepSegmentHit(PhysicsDirectSpaceState3D space,
							Vector3 a, Vector3 b, uint mask, float radius,
							out Vector3 hitPos)
	{
		hitPos = Vector3.Zero;

		Vector3 motion = b - a;
		float len = motion.Length();
		// DECISION: skip degenerate or non-finite motion.
		if (!float.IsFinite(len) || len <= 1e-6f) return false;

		var sphere = new SphereShape3D { Radius = Mathf.Max(0.0005f, radius) };

		var q = new PhysicsShapeQueryParameters3D
		{
			Shape = sphere,
			Transform = new Transform3D(Basis.Identity, a),
			Motion = motion,
			CollisionMask = mask,
			Margin = 0.0f,
			CollideWithBodies = true,
			CollideWithAreas = true
		};

		float[] res = space.CastMotion(q);
		// DECISION: require valid CastMotion result.
		if (res == null || res.Length < 2) return false;

		float unsafeFrac = res[1];
		// DECISION: reject non-finite collision fraction.
		if (!float.IsFinite(unsafeFrac)) return false;

		// DECISION: unsafe fraction < 1 means collision occurs along motion.
		if (unsafeFrac < 1.0f)
		{
			hitPos = a + motion * Mathf.Clamp(unsafeFrac, 0.0f, 1.0f);
			return true;
		}

		return false;
	}

	private static bool IsUsablePhysicsSpaceState(PhysicsDirectSpaceState3D space)
	{
		return space != null && GodotObject.IsInstanceValid(space);
	}

	private static string FormatRayDiagToken(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
	}

	private static bool TryIntersectRayWithGuard(
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
		string sceneToken = FormatRayDiagToken(sceneName);
		string fixtureToken = FormatRayDiagToken(fixtureName);
		string modeValue = FormatRayDiagToken(modeToken);
		string queryToken = FormatRayDiagToken(queryKind);
		string sourceToken = FormatRayDiagToken(renderSpaceSource);

		if (!spaceUsable || !queryValid)
		{
			if (System.Threading.Interlocked.Exchange(ref warnedState, 1) == 0)
			{
				GD.PushWarning(
					$"[RenderSpace][RayGuard] scene={sceneToken} fixture={fixtureToken} mode={modeValue} " +
					$"queryKind={queryToken} source={sourceToken} spaceNull={(space == null ? 1 : 0)} " +
					$"spaceUsable={(spaceUsable ? 1 : 0)} quickRayValid={(queryValid ? 1 : 0)} " +
					$"pass1ProbeEnabled={(pass1ProbeEnabled ? 1 : 0)} exception=none reason=skip_query_without_crash");
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
			if (System.Threading.Interlocked.Exchange(ref warnedState, 1) == 0)
			{
				GD.PushWarning(
					$"[RenderSpace][RayGuard] scene={sceneToken} fixture={fixtureToken} mode={modeValue} " +
					$"queryKind={queryToken} source={sourceToken} spaceNull={(space == null ? 1 : 0)} " +
					$"spaceUsable={(spaceUsable ? 1 : 0)} quickRayValid={(queryValid ? 1 : 0)} " +
					$"pass1ProbeEnabled={(pass1ProbeEnabled ? 1 : 0)} exception={ex.GetType().Name} " +
					$"reason=skip_query_without_crash");
			}
			return false;
		}
		catch (ObjectDisposedException ex)
		{
			if (System.Threading.Interlocked.Exchange(ref warnedState, 1) == 0)
			{
				GD.PushWarning(
					$"[RenderSpace][RayGuard] scene={sceneToken} fixture={fixtureToken} mode={modeValue} " +
					$"queryKind={queryToken} source={sourceToken} spaceNull={(space == null ? 1 : 0)} " +
					$"spaceUsable={(spaceUsable ? 1 : 0)} quickRayValid={(queryValid ? 1 : 0)} " +
					$"pass1ProbeEnabled={(pass1ProbeEnabled ? 1 : 0)} exception={ex.GetType().Name} " +
					$"reason=skip_query_without_crash");
			}
			return false;
		}
	}

	// Baseline method
	public static bool SubdividedRayHit(PhysicsDirectSpaceState3D space,
										Vector3 a, Vector3 b, uint mask,
										int maxSubsteps, out Vector3 hitPos)
	{
		ulong cid;
		string cname;
		return SubdividedRayHit(space, a, b, mask, maxSubsteps, out hitPos, out cid, out cname);
	}
	
	/// 
	/// Method Overload 1
	public static bool SubdividedRayHit(PhysicsDirectSpaceState3D space,
							Vector3 a, Vector3 b, uint mask, int maxSubsteps,
							out Vector3 hitPos, out ulong colliderId,
							out string colliderName)
	{
		hitPos = Vector3.Zero;
		colliderId = 0;
		colliderName = "<none>";

		Vector3 d = b - a;
		float len = d.Length();
		// DECISION: skip degenerate segments.
		if (len <= 1e-6f) return false;

		int steps = Mathf.Clamp(maxSubsteps, 1, 64);
		Vector3 prev = a;

		// DECISION: walk along the segment in substeps for raycasting.
		for (int i = 1; i <= steps; i++)
		{
			float t = (float)i / steps;
			Vector3 cur = a + d * t;

			using var rq = PhysicsRayQueryParameters3D.Create(prev, cur, mask);
			rq.CollideWithBodies = true;
			rq.CollideWithAreas = true;
			rq.HitFromInside = false;	// formerly true

			var hit = space.IntersectRay(rq);
			// DECISION: first hit wins.
			if (hit.Count > 0)
			{
				hitPos = (Vector3)hit["position"];
				colliderId = (ulong)hit["collider_id"];
				var colliderObj = hit["collider"].AsGodotObject();
				// DECISION: fallback collider name when object is null.
				colliderName = colliderObj != null ? colliderObj.ToString() : "<null>";
				return true;
			}

			prev = cur;
		}

		return false;
	}

	/// Method Overload 2
	// ✅ ADD: normal-aware overload (no extra work; Godot gives "normal" already)
	public static bool SubdividedRayHit(PhysicsDirectSpaceState3D space,
		Vector3 a, Vector3 b, uint mask, int maxSubsteps,
		out Vector3 hitPos, out Vector3 hitNormal,
		out ulong colliderId, out string colliderName)
	{
		return SubdividedRayHit(
			space, a, b, mask, maxSubsteps,
			out hitPos, out hitNormal,
			out colliderId, out colliderName,
			out _,
			includeColliderName: true
		);
	}

	public static bool SubdividedRayHit(PhysicsDirectSpaceState3D space,
		Vector3 a, Vector3 b, uint mask, int maxSubsteps,
		out Vector3 hitPos, out Vector3 hitNormal,
		out ulong colliderId, out int primitiveOrShapeId, out string colliderName)
	{
		return SubdividedRayHit(
			space, a, b, mask, maxSubsteps,
			out hitPos, out hitNormal,
			out colliderId, out primitiveOrShapeId, out colliderName,
			out _,
			includeColliderName: true
		);
	}

	// ✅ ADD: normal-aware overload with optional collider name + query count
	public static bool SubdividedRayHit(PhysicsDirectSpaceState3D space,
		Vector3 a, Vector3 b, uint mask, int maxSubsteps,
		out Vector3 hitPos, out Vector3 hitNormal,
		out ulong colliderId, out string colliderName,
		out int rayQueryCount,
		bool includeColliderName,
		bool hitBackFaces = false,
		bool hitFromInside = false,
		string diagnosticSceneName = "",
		string diagnosticFixtureName = "",
		string diagnosticModeToken = "",
		string diagnosticQueryKind = "subdivided_ray")
	{
		hitPos = Vector3.Zero;
		hitNormal = Vector3.Up;
		colliderId = 0;
		colliderName = "<none>";
		rayQueryCount = 0;

		Vector3 d = b - a;
		float len = d.Length();
		// DECISION: skip degenerate segments.
		if (len <= 1e-6f) return false;

		int steps = Mathf.Clamp(maxSubsteps, 1, 64);
		Vector3 prev = a;

		// DECISION: walk along the segment in substeps for raycasting.
		for (int i = 1; i <= steps; i++)
		{
			float t = (float)i / steps;
			Vector3 cur = a + d * t;

			using var rq = PhysicsRayQueryParameters3D.Create(prev, cur, mask);
			rq.CollideWithBodies = true;
			rq.CollideWithAreas = true;
			rq.HitBackFaces = hitBackFaces;
			rq.HitFromInside = hitFromInside;

			rayQueryCount++;
			if (!TryIntersectRayWithGuard(
				space,
				rq,
				pass1ProbeEnabled: false,
				queryKind: diagnosticQueryKind,
				renderSpaceSource: "render_space",
				sceneName: diagnosticSceneName,
				fixtureName: diagnosticFixtureName,
				modeToken: diagnosticModeToken,
				ref _subdividedRayQueryFailureWarned,
				out var hit))
			{
				return false;
			}
			// DECISION: first hit wins.
			if (hit.Count > 0)
			{
				hitPos = (Vector3)hit["position"];
				// DECISION: only use normal if provided.
				if (hit.TryGetValue("normal", out var nObj))
					hitNormal = ((Vector3)nObj).Normalized();

				colliderId = (ulong)hit["collider_id"];
				// DECISION: optionally resolve collider name (extra cost).
				if (includeColliderName)
				{
					var colliderObj = hit["collider"].AsGodotObject();
					// DECISION: fallback collider name when object is null.
					colliderName = colliderObj != null ? colliderObj.ToString() : "<null>";
				}
				return true;
			}

			prev = cur;
		}

		return false;
	}

	public static bool SubdividedRayHit(PhysicsDirectSpaceState3D space,
		Vector3 a, Vector3 b, uint mask, int maxSubsteps,
		out Vector3 hitPos, out Vector3 hitNormal,
		out ulong colliderId, out int primitiveOrShapeId, out string colliderName,
		out int rayQueryCount,
		bool includeColliderName,
		bool hitBackFaces = false,
		bool hitFromInside = false,
		string diagnosticSceneName = "",
		string diagnosticFixtureName = "",
		string diagnosticModeToken = "",
		string diagnosticQueryKind = "subdivided_ray")
	{
		hitPos = Vector3.Zero;
		hitNormal = Vector3.Up;
		colliderId = 0;
		primitiveOrShapeId = -1;
		colliderName = "<none>";
		rayQueryCount = 0;

		Vector3 d = b - a;
		float len = d.Length();
		if (len <= 1e-6f) return false;

		int steps = Mathf.Clamp(maxSubsteps, 1, 64);
		Vector3 prev = a;

		for (int i = 1; i <= steps; i++)
		{
			float t = (float)i / steps;
			Vector3 cur = a + d * t;

			using var rq = PhysicsRayQueryParameters3D.Create(prev, cur, mask);
			rq.CollideWithBodies = true;
			rq.CollideWithAreas = true;
			rq.HitBackFaces = hitBackFaces;
			rq.HitFromInside = hitFromInside;

			rayQueryCount++;
			if (!TryIntersectRayWithGuard(
				space,
				rq,
				pass1ProbeEnabled: false,
				queryKind: diagnosticQueryKind,
				renderSpaceSource: "render_space",
				sceneName: diagnosticSceneName,
				fixtureName: diagnosticFixtureName,
				modeToken: diagnosticModeToken,
				ref _subdividedRayQueryFailureWarned,
				out var hit))
			{
				return false;
			}

			if (hit.Count > 0)
			{
				hitPos = (Vector3)hit["position"];
				if (hit.TryGetValue("normal", out var nObj))
					hitNormal = ((Vector3)nObj).Normalized();

				colliderId = (ulong)hit["collider_id"];
				primitiveOrShapeId = ExtractPrimitiveOrShapeId(hit);
				if (includeColliderName)
				{
					var colliderObj = hit["collider"].AsGodotObject();
					colliderName = colliderObj != null ? colliderObj.ToString() : "<null>";
				}
				return true;
			}

			prev = cur;
		}

		return false;
	}

	public static int ExtractPrimitiveOrShapeId(Godot.Collections.Dictionary hit)
	{
		if (TryReadHitInt(hit, "face_index", out int faceIndex) && faceIndex >= 0)
			return faceIndex;

		if (TryReadHitInt(hit, "shape", out int shapeIndex))
			return shapeIndex;

		return -1;
	}

	private static bool TryReadHitInt(Godot.Collections.Dictionary hit, string key, out int value)
	{
		value = -1;
		if (hit == null || !hit.TryGetValue(key, out var raw))
			return false;

		try
		{
			Variant variant = (Variant)raw;
			value = variant.VariantType switch
			{
				Variant.Type.Int => checked((int)(long)variant),
				Variant.Type.Float => checked((int)MathF.Round((float)variant)),
				_ => Convert.ToInt32(variant.ToString(), CultureInfo.InvariantCulture)
			};
			return true;
		}
		catch
		{
			value = -1;
			return false;
		}
	}


	private static Vector3 SafeNormalized(Vector3 v, Vector3 fallback)
	{
		float len = v.Length();
		// DECISION: return fallback when vector is degenerate or non-finite.
		if (!float.IsFinite(len) || len < 1e-8f) return fallback;
		return v / len;
	}

	private static bool IsFinite(Vector3 v)
	{
		return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
	}

	// ===== Ray Construction / Integration =====
	private void EnsureCapacity(int raysTotal, int samplesTotal)
	{
		// EFFECT: resize shared buffers to avoid per-ray allocations.
		// DECISION: grow ray meta buffer if needed.
		if (_rayMeta.Length < raysTotal) Array.Resize(ref _rayMeta, raysTotal);
		// DECISION: grow hit payload buffer if needed (1 payload per ray for now).
		if (_hitPayload.Length < raysTotal) Array.Resize(ref _hitPayload, raysTotal); // 1 payload per ray for now

		// DECISION: grow sample position buffer if needed.
		if (_samplePos.Length < samplesTotal) Array.Resize(ref _samplePos, samplesTotal);
		// DECISION: grow sample color buffer if needed.
		if (_sampleCol.Length < samplesTotal) Array.Resize(ref _sampleCol, samplesTotal);

		// DECISION: grow debug offsets buffer if needed.
		if (_dbgRayOffsets.Length < raysTotal) Array.Resize(ref _dbgRayOffsets, raysTotal);
		// DECISION: grow debug counts buffer if needed.
		if (_dbgRayCounts.Length  < raysTotal) Array.Resize(ref _dbgRayCounts,  raysTotal);
	}

	// Initial version testing
	private RayMeta SimulateRay(PhysicsDirectSpaceState3D space, RayEmitter3D e,
							Vector3 origin,	Vector3 dir, Vector3 bendDir,
							Vector3 center, float beta, float gamma,
							FieldSourceSnap[] fieldSources,
							bool hasSources, uint collisionMask,
							out HitPayload hitOut)
	{
		// CROSS-CLASS CONTRACT: RayEmitter3D supplies origin/dir/spread/MaxDistance and color/intensity.
		// ASSUMPTION: origin/dir are in world space; dir is normalized.
		// EFFECT: wrong space or normalization will distort ray path and collision results.

		bool rayHit = false;
		bool hadHit = false;
		Vector3 hitPos = Vector3.Zero;
		ulong hitColliderId = 0;
		string hitColliderName = "<none>";
		int trailStopCount = int.MaxValue;
		bool absorbedByInnerRadius = false;
		RayTerminationReason terminationReason = RayTerminationReason.None;

		int sampleStart = _sampleWriteHead;
		int sampleCount = 0;

		Vector3 p = origin;
		Vector3 v = dir;
		float traveled = 0.0f;

		// CONTROL FACTOR: RenderEveryNSteps controls sample write cadence for rendering.
		int every = Mathf.Max(1, RenderEveryNSteps);

		//////////////  SCREEN-SPACE COLLISION CADENCE (SIMULATE)  //////////////
		// CONTROL FACTOR: CollisionEveryNSteps is the base collision cadence.
		int ceBase = Mathf.Max(1, CollisionEveryNSteps);
		int ceCurrent = ceBase;              // ✅ must persist across loop
		int stepsSinceCollision = 0;

		var cam = GetCamera();
		// DECISION: compute pixels-per-radian only when cadence is enabled and camera exists.
		float pxPerRad = (UseScreenSpaceCollisionCadence && cam != null) ? GetPixelsPerRadian(cam) : 0f;
		// DECISION: fall back to origin if camera is missing.
		Vector3 camPos = (cam != null) ? cam.GlobalPosition : Vector3.Zero;
		/////////////////////////////////////////////////////////////////////////

		// CONTROL FACTOR: MinStepLength/MaxStepLength clamp adaptive step size.
		float minStep = Mathf.Min(MinStepLength, MaxStepLength);
		float maxStep = Mathf.Max(MinStepLength, MaxStepLength);
		minStep = Mathf.Max(0.0001f, minStep);

		float hitDist = 0.0f;
		DerivativeAwareStepState derivativeStepState = default;

		// Per-ray inside state for CrossingEvent boundary layer detection (≤32 layers via bitmask).
		// Initialized from the ray origin so that rays starting inside a volume do NOT fire an entry event.
		uint blvInsideMask = (_hasBoundaryLayers && UseIntegratedField)
			? ComputeInsideMask(p, _boundaryLayerSnaps)
			: 0u;
		int boundaryRemapCount = 0;
		int ledgerEventCount = 0;
		int ledgerBoundaryCrossings = 0;
		int ledgerTransformCount = 0;
		int ledgerEntryCount = 0;
		int ledgerExitCount = 0;
		int ledgerLastCrossingLayer = -1;
		LedgerCrossingKind ledgerLastCrossingKind = LedgerCrossingKind.None;
		bool ledgerAmbiguousOrdering = false;

		// DECISION: integrate along the ray for up to StepsPerRay steps.
		for (int s = 0; s <= StepsPerRay; s++)
		{
			if (UseIntegratedField && hasSources && TryGetAbsorbingSourceAtPoint(p, fieldSources, out _))
			{
				absorbedByInnerRadius = true;
				terminationReason = RayTerminationReason.AbsorbedInsideInnerRadius;
				break;
			}

			// Boundary layer: continuous effects and crossing-event detection.
			if (_hasBoundaryLayers && UseIntegratedField)
			{
				BoundaryInteractionLedgerDelta boundaryDelta = ApplyBoundaryLayerInteractions(ref p, ref v, _boundaryLayerSnaps, ref blvInsideMask);
				boundaryRemapCount += boundaryDelta.TransformCount;
				ledgerEventCount += boundaryDelta.EventCount;
				ledgerBoundaryCrossings += boundaryDelta.BoundaryCrossings;
				ledgerTransformCount += boundaryDelta.TransformCount;
				ledgerEntryCount += boundaryDelta.EntryCount;
				ledgerExitCount += boundaryDelta.ExitCount;
				if (boundaryDelta.LastCrossingKind != LedgerCrossingKind.None)
				{
					ledgerLastCrossingKind = boundaryDelta.LastCrossingKind;
					ledgerLastCrossingLayer = boundaryDelta.LastCrossingLayer;
				}
				ledgerAmbiguousOrdering |= boundaryDelta.AmbiguousOrdering;
			}

			Vector3 a = Vector3.Zero;
			Vector3 next = p;

			// DECISION: integrated vs analytic field path.
			if (UseIntegratedField)
			{
				// Transport interpretation branch lives here; FieldMath remains canonical baseline.
				if (!TryStepIntegratedTransport(
					p,
					v,
					center,
					beta,
					gamma,
					fieldSources,
					hasSources,
					e.MaxDistance,
					traveled,
					minStep,
					maxStep,
					fieldGrid: null,
					applyLowCurvatureBoost: false,
					ref derivativeStepState,
					out next,
					out v,
					out a,
					out float step))
				{
					break;
				}

				///////////////////////////
				///////////////////////////
				// Update collision cadence for this step (screen-space error model)
				// EFFECT: recompute collision cadence for this step.
				ceCurrent = ceBase;

				// DECISION: adapt collision cadence using screen-space error model when enabled.
				if (UseScreenSpaceCollisionCadence && cam != null)
				{
					float aPerp = PerpAccelLen(a, v); // v normalized after SafeNormalized
					float depth = (p - camPos).Length();
					ceCurrent = ComputeCeFromScreenError(ceBase, step, aPerp, depth, pxPerRad, CollisionMaxErrorPixels);
				}
				///////////////////////////
				///////////////////////////

				traveled += step;
				// DECISION: stop integrating when max distance exceeded.
				if (traveled > e.MaxDistance)
					break;
			}
			else
			{
				// EFFECT: analytic bend path (no integration), step by StepLength.
				float t = s * StepLength;
				float bend = beta * FastPow(t, gamma) * BendScale;
				next = origin + dir * t + bendDir * bend;
			}

			// DECISION: avoid divide-by-zero when StepsPerRay is zero or negative.
			float step01 = (StepsPerRay <= 0) ? 0f : (float)s / StepsPerRay;
			float fade = 1.0f - step01;
			fade *= fade;
			float alpha = Alpha * e.Intensity * fade;

			Color c = e.RayColor;
			// DECISION: optionally color by field magnitude.
			if (ColorByField)
			{
				float heat = Mathf.Clamp(a.Length() * FieldColorGain, 0f, 1f);
				c = c.Lerp(HotColor, heat);
			}
			c.A = Mathf.Clamp(alpha, 0.0f, 1.0f);

			// Store samples (array-backed)
			// DECISION: write samples only at render cadence.
			if ((s % every) == 0)
			{
				// DECISION: optionally terminate trail after first hit.
				if (!TerminateTrailOnHit || sampleCount < trailStopCount)
				{
					int wi = sampleStart + sampleCount;
					// DECISION: guard buffer bounds.
					if (wi < _samplePos.Length)
					{
						_samplePos[wi] = p;
						_sampleCol[wi] = c;
						sampleCount++;
					}
					else
					{
						// Out of capacity (shouldn't happen if EnsureCapacity is correct)
						break;
					}
				}
			}

			// Collision check
			stepsSinceCollision++;
			// DECISION: run collision checks only when enabled and cadence reached.
			if ((StopOnHit || CheckCollisionsEvenIfNotStopping) && s > 0 && stepsSinceCollision >= ceCurrent)
			{
				stepsSinceCollision = 0;
				Vector3 segA = p;
				Vector3 segB = next;
				float segLen = (segB - segA).Length();

				bool allowCollision = true;

				// DECISION: optionally filter segments by insight plane slab.
				if (UseInsightPlaneFilter && _hasInsightPlane)
				{
					// DECISION: skip collision checks when segment does not cross insight plane slab.
					if (!SegmentCrossesPlane(segA, segB, _insightPlane, CollisionRadius))
						allowCollision = false;
				}

				// DECISION: only test collision if allowed by filters.
				if (allowCollision)
				{
					bool didHit = false;
					Vector3 hp = Vector3.Zero;
					ulong cid = 0;
					string cname = "<none>";

					// DECISION: skip degenerate segments.
					if (segLen > 1e-6f)
					{
						// DECISION: choose sweep vs subdivided raycast.
						if (UseSphereSweepCollision)
							didHit = SweepSegmentHit(space, segA, segB, collisionMask, CollisionRadius, out hp);
						else
						{
							int sub = 1;
							// DECISION: subdivide when segment is long.
							if (segLen > CollisionRaySubdivideThreshold)
								sub = Mathf.CeilToInt(segLen / CollisionRaySubdivideThreshold);
							sub = Mathf.Clamp(sub, 1, MaxCollisionSubsteps);

							//didHit = SubdividedRayHit(space, segA, segB, collisionMask, sub, out hp, out cid, out cname);
							Vector3 hn;
							didHit = SubdividedRayHit(space, segA, segB, collisionMask, sub, out hp, out hn, out cid, out cname);
							// EFFECT: stash collider identity for hit payload.
							if (didHit)
							{
								hitColliderId = cid;
								hitColliderName = cname;
								// store normal:
								// (stash hn somewhere for hitOut later)
							}
						}
					}

					// DECISION: record first hit only.
					if (didHit && !rayHit)
					{
						rayHit = true;
						hadHit = true;
						hitPos = hp;
						if (terminationReason == RayTerminationReason.None)
						{
							terminationReason = RayTerminationReason.Hit;
						}

						hitColliderId = cid;
						hitColliderName = cname;
						
						// EFFECT: estimate path distance to hit (segment-local interpolation).
						hitDist = traveled - segLen + (hitPos - segA).Length();

						// DECISION: when trail termination is enabled, stop writing samples past the hit.
						if (TerminateTrailOnHit)
							trailStopCount = sampleCount;

						// DECISION: stop simulation at first hit when enabled.
						if (StopOnHit)
							break;
					}
				}
			}
			p = next;
		}

		// Update write head
		FinalizeDerivativeAwareStepState(ref derivativeStepState);
		_sampleWriteHead = sampleStart + sampleCount;

		// EFFECT: render count may be truncated by hit trail termination.
		int renderCount = Mathf.Min(sampleCount, Mathf.Max(0, trailStopCount));

		// EFFECT: emit hit payload (Valid=false when no hit).
		FinalizeDerivativeAwareStepState(ref derivativeStepState);
		hitOut = new HitPayload
		{
			Valid = hadHit,
			Position = hitPos,
			ColliderId = hitColliderId,
			ColliderName = hitColliderName,
			Distance = absorbedByInnerRadius ? traveled : hitDist,
			Normal = Vector3.Zero,             // v0 placeholder
			Albedo = Colors.White,             // v0 placeholder
			Absorbed = absorbedByInnerRadius ? 1 : 0,
			TerminationReason = terminationReason
		};

		return new RayMeta
		{
			SampleStart = sampleStart,
			SampleCount = sampleCount,
			RenderCount = renderCount,
			HadHit = hadHit,
			// DECISION: only set payload index when a hit exists.
			HitPayloadIndex = hadHit ? _rayWriteHead : -1
		};
	}

	// Camera Film Version (hit-only, no sample buffer writes)
	public RayMeta SimulateRayCamera(
		PhysicsDirectSpaceState3D space,
		Vector3 origin, Vector3 dir, Vector3 bendDir,
		Vector3 center, float beta, float gamma,
		FieldSourceSnap[] fieldSources,
		bool hasSources, uint collisionMask,
		float maxDistance, out HitPayload hitOut)
	{
		bool hadHit = false;
		Vector3 hitPos = Vector3.Zero;
		ulong colliderId = 0;
		string colliderName = "<none>";
		bool absorbedByInnerRadius = false;
		RayTerminationReason terminationReason = RayTerminationReason.None;
		float traveled = 0.0f;
		float hitDistance = 0f;

		Vector3 p = origin;
		Vector3 v = dir;

		// CONTROL FACTOR: CollisionEveryNSteps cadence (film version always checks on cadence).
		int ce = Mathf.Max(1, CollisionEveryNSteps);

		float minStep = Mathf.Min(MinStepLength, MaxStepLength);
		float maxStep = Mathf.Max(MinStepLength, MaxStepLength);
		minStep = Mathf.Max(0.0001f, minStep);
		DerivativeAwareStepState derivativeStepState = default;

		// Per-ray inside state for CrossingEvent boundary layer detection (≤32 layers via bitmask).
		// Initialized from the ray origin so that rays starting inside a volume do NOT fire an entry event.
		uint blvInsideMask = (_hasBoundaryLayers && UseIntegratedField)
			? ComputeInsideMask(p, _boundaryLayerSnaps)
			: 0u;
		int boundaryRemapCount = 0;
		int ledgerEventCount = 0;
		int ledgerBoundaryCrossings = 0;
		int ledgerTransformCount = 0;
		int ledgerEntryCount = 0;
		int ledgerExitCount = 0;
		int ledgerLastCrossingLayer = -1;
		LedgerCrossingKind ledgerLastCrossingKind = LedgerCrossingKind.None;
		bool ledgerAmbiguousOrdering = false;

		// DECISION: integrate along the ray for up to StepsPerRay steps.
		for (int s = 0; s <= StepsPerRay; s++)
		{
			if (UseIntegratedField && hasSources && TryGetAbsorbingSourceAtPoint(p, fieldSources, out _))
			{
				absorbedByInnerRadius = true;
				terminationReason = RayTerminationReason.AbsorbedInsideInnerRadius;
				break;
			}

			// Boundary layer: continuous effects and crossing-event detection.
			if (_hasBoundaryLayers && UseIntegratedField)
			{
				BoundaryInteractionLedgerDelta boundaryDelta = ApplyBoundaryLayerInteractions(ref p, ref v, _boundaryLayerSnaps, ref blvInsideMask);
				boundaryRemapCount += boundaryDelta.TransformCount;
				ledgerEventCount += boundaryDelta.EventCount;
				ledgerBoundaryCrossings += boundaryDelta.BoundaryCrossings;
				ledgerTransformCount += boundaryDelta.TransformCount;
				ledgerEntryCount += boundaryDelta.EntryCount;
				ledgerExitCount += boundaryDelta.ExitCount;
				if (boundaryDelta.LastCrossingKind != LedgerCrossingKind.None)
				{
					ledgerLastCrossingKind = boundaryDelta.LastCrossingKind;
					ledgerLastCrossingLayer = boundaryDelta.LastCrossingLayer;
				}
				ledgerAmbiguousOrdering |= boundaryDelta.AmbiguousOrdering;
			}

			Vector3 next = p;

			// DECISION: integrated vs analytic field path.
			if (UseIntegratedField)
			{
				if (!TryStepIntegratedTransport(
					p,
					v,
					center,
					beta,
					gamma,
					fieldSources,
					hasSources,
					maxDistance,
					traveled,
					minStep,
					maxStep,
					fieldGrid: null,
					applyLowCurvatureBoost: false,
					ref derivativeStepState,
					out next,
					out v,
					out _,
					out float step))
				{
					break;
				}

				traveled += step;

				// DECISION: stop integrating when max distance exceeded.
				if (traveled > maxDistance) break;
			}
			else
			{
				// EFFECT: analytic bend path (no integration), step by StepLength.
				float t = s * StepLength;
				float bend = beta * FastPow(t, gamma) * BendScale;
				next = origin + dir * t + bendDir * bend;

				traveled = t;
				// DECISION: stop when max distance exceeded.
				if (traveled > maxDistance) break;
			}

			// DECISION: collision every N steps (ALWAYS for film).
			if (s > 0 && (s % ce) == 0)
			{
				Vector3 segA = p;
				Vector3 segB = next;
				float segLen = (segB - segA).Length();

				bool allowCollision = true;
				// DECISION: optionally filter segments by insight plane slab.
				if (UseInsightPlaneFilter && _hasInsightPlane)
				{
					// DECISION: skip collision checks when segment does not cross insight plane slab.
					if (!SegmentCrossesPlane(segA, segB, _insightPlane, CollisionRadius))
						allowCollision = false;
				}

				// DECISION: only test collisions for valid, allowed segments.
				if (allowCollision && segLen > 1e-6f)
				{
					Vector3 hp;
					ulong cid;
					string cname;

					bool didHit = false;

					// DECISION: choose sweep vs subdivided raycast.
					if (UseSphereSweepCollision)
					{
						didHit = SweepSegmentHit(space, segA, segB, collisionMask, CollisionRadius, out hp);
						// sphere sweep doesn't return collider id/name (ok for v0)
					}
					else
					{
						int sub = 1;
						// DECISION: subdivide when segment is long.
						if (segLen > CollisionRaySubdivideThreshold)
							sub = Mathf.CeilToInt(segLen / CollisionRaySubdivideThreshold);
						sub = Mathf.Clamp(sub, 1, MaxCollisionSubsteps);

						didHit = SubdividedRayHit(space, segA, segB, collisionMask, sub, out hp, out cid, out cname);
						// EFFECT: stash collider identity for hit payload.
						if (didHit)
						{
							colliderId = cid;
							colliderName = cname;
						}
					}

					// DECISION: first hit stops film ray (hit-only).
					if (didHit)
					{
						hadHit = true;
						hitPos = hp;
						terminationReason = RayTerminationReason.Hit;

						// EFFECT: more accurate path-length to hit within the segment.
						hitDistance = traveled - segLen + (hitPos - segA).Length();
						break; // film wants first hit
					}
				}
			}

			p = next;
		}

		// EFFECT: emit hit payload (Valid=false when no hit).
		hitOut = new HitPayload
		{
			Valid = hadHit,
			Position = hitPos,
			ColliderId = colliderId,
			ColliderName = colliderName,
			Distance = absorbedByInnerRadius ? traveled : hitDistance,
			Normal = Vector3.Zero,
			Albedo = Colors.White,
			Absorbed = absorbedByInnerRadius ? 1 : 0,
			TerminationReason = terminationReason
		};

		return new RayMeta
		{
			SampleStart = 0,
			SampleCount = 0,
			RenderCount = 0,
			HadHit = hadHit,
			HitPayloadIndex = -1
		};
	}


	private int ComputeMaxSamplesPerRay()
	{
		// CONTROL FACTOR: RenderEveryNSteps reduces sample count per ray.
		int every = Mathf.Max(1, RenderEveryNSteps);
		// steps include s=0..StepsPerRay inclusive → StepsPerRay+1 "step indices"
		int samples = (StepsPerRay / every) + 2; // conservative pad
		// DECISION: add room for hit marker if enabled.
		if (DrawHitMarker) samples += 1;
		// DECISION: enforce minimum buffer size.
		return Mathf.Max(4, samples);
	}

	// Build segments at collision cadence (ce). No physics calls here.
	public int BuildRaySegmentsCamera(
		Vector3 origin, Vector3 dir, Vector3 bendDir,
		Vector3 center, float beta, float gamma,
		FieldSourceSnap[] fieldSnaps, bool hasSources,
		float maxDistance,
		RaySeg[] outSegs, int outOffset, int outCapacity,
		Plane insightPlane, bool useInsightPlane, float insightEps,
		SegmentCallback onSegment = null)
	{
		/// 
		/////////////////////////
		Vector3 p = origin;
		Vector3 v = dir; // assumed normalized

		float traveled = 0f;

		// CONTROL FACTOR: CollisionEveryNSteps is the base segment cadence.
		int ceBase = Mathf.Max(1, CollisionEveryNSteps);
		int ce = ceBase;
		int stepsSinceEmit = 0;

		// camera data for screen-space cadence
		Camera3D cam = GetCamera();
		// DECISION: compute pixels-per-radian only when cadence is enabled and camera exists.
		float pxPerRad = (UseScreenSpaceCollisionCadence && cam != null) ? GetPixelsPerRadian(cam) : 0f;
		// DECISION: fall back to origin if camera is missing.
		Vector3 camPos = (cam != null) ? cam.GlobalPosition : Vector3.Zero;

		// CONTROL FACTOR: MinStepLength/MaxStepLength clamp adaptive step size.
		float minStep = Mathf.Max(0.0001f, Mathf.Min(MinStepLength, MaxStepLength));
		float maxStep = Mathf.Max(MinStepLength, MaxStepLength);

		int written = 0;

		// Precompute for non-integrated mode
		float bendScale = BendScale;
		float stepLength = StepLength;
		TransportModel activeTransport = hasSources ? ResolveActiveTransportModel(fieldSnaps) : TransportModel.GRIN_Optical;
		bool emitEveryMetricStep = UseIntegratedField && activeTransport == TransportModel.Metric_NullGeodesic;
		int maxIntegrationSteps = emitEveryMetricStep
			? Mathf.Max(1, StepsPerRay * GetMetricAdaptiveStepMultiplier())
			: StepsPerRay;
		float metricTurnThreshold = GetMetricAdaptiveTurnThresholdRadians();
		float metricErrorTolerance = Mathf.Max(1e-5f, MetricAdaptiveErrorTolerance);
		int metricMaxSubdivisions = Mathf.Max(0, MetricAdaptiveMaxSubdivisions);
		DerivativeAwareStepState derivativeStepState = default;
		BoundaryLayerSnap[] boundaryLayers = _boundaryLayerSnaps;
		bool hasBoundaryLayers = _hasBoundaryLayers;
		uint blvInsideMask = (hasBoundaryLayers && UseIntegratedField)
			? ComputeInsideMask(p, boundaryLayers)
			: 0u;
		int boundaryRemapCount = 0;
		int ledgerEventCount = 0;
		int ledgerBoundaryCrossings = 0;
		int ledgerTransformCount = 0;
		int ledgerEntryCount = 0;
		int ledgerExitCount = 0;
		int ledgerLastCrossingLayer = -1;
		LedgerCrossingKind ledgerLastCrossingKind = LedgerCrossingKind.None;
		bool ledgerAmbiguousOrdering = false;


		// DECISION: integrate along the ray for up to StepsPerRay steps.
		for (int s = 0; s <= maxIntegrationSteps; s++)
		{
			if (UseIntegratedField && hasSources && TryGetAbsorbingSourceAtPoint(p, fieldSnaps, out _))
			{
				break;
			}

			if (hasBoundaryLayers && UseIntegratedField)
			{
				BoundaryInteractionLedgerDelta boundaryDelta = ApplyBoundaryLayerInteractions(ref p, ref v, boundaryLayers, ref blvInsideMask);
				boundaryRemapCount += boundaryDelta.TransformCount;
				ledgerEventCount += boundaryDelta.EventCount;
				ledgerBoundaryCrossings += boundaryDelta.BoundaryCrossings;
				ledgerTransformCount += boundaryDelta.TransformCount;
				ledgerEntryCount += boundaryDelta.EntryCount;
				ledgerExitCount += boundaryDelta.ExitCount;
				if (boundaryDelta.LastCrossingKind != LedgerCrossingKind.None)
				{
					ledgerLastCrossingKind = boundaryDelta.LastCrossingKind;
					ledgerLastCrossingLayer = boundaryDelta.LastCrossingLayer;
				}
				ledgerAmbiguousOrdering |= boundaryDelta.AmbiguousOrdering;
			}

			Vector3 next = p;

			// DECISION: integrated vs analytic field path.
			if (UseIntegratedField)
			{
				float remaining = maxDistance - traveled;
				if (remaining <= 0f)
				{
					break;
				}

				Vector3 a = ComputeTransportAccelerationForActiveModel(activeTransport, p, v, bendDir, center, beta, gamma, fieldSnaps, hasSources);
				float aLen = a.Length();
				if (!float.IsFinite(aLen))
				{
					a = Vector3.Zero;
					aLen = 0f;
				}
				else if (aLen > 50f)
				{
					a *= 50f / aLen;
					aLen = 50f;
				}

				float step = ComputeAdaptiveIntegratedStepLength(
					v,
					a,
					minStep,
					maxStep,
					applyLowCurvatureBoost: true,
					applyMetricCurvatureGain: emitEveryMetricStep,
					ref derivativeStepState);

				if (step > remaining)
				{
					step = remaining;
				}

				if (emitEveryMetricStep)
				{
					ce = 1;
				}
				else if (UseScreenSpaceCollisionCadence && cam != null)
				{
					float aPerp = PerpAccelLen(a, v);
					float depth = (p - camPos).Length();
					ce = ComputeCeFromScreenError(ceBase, step, aPerp, depth, pxPerRad, CollisionMaxErrorPixels);
				}
				else
				{
					ce = ceBase;
				}

				Vector3 vBeforeStep = v;
				if (emitEveryMetricStep)
				{
					float acceptedStep = step;
					Vector3 acceptedNext = p;
					Vector3 acceptedDir = v;
					Vector3 acceptedAccel = a;

					for (int subdiv = 0; subdiv <= metricMaxSubdivisions; subdiv++)
					{
						float trialStep = acceptedStep;
						Vector3 coarseDir = SafeNormalized(v + (acceptedAccel * trialStep), v);
						Vector3 coarseNext = p + (coarseDir * trialStep);

						float halfStep = 0.5f * trialStep;
						Vector3 halfDir = SafeNormalized(v + (acceptedAccel * halfStep), v);
						Vector3 halfPos = p + (halfDir * halfStep);
						Vector3 halfAccel = ComputeTransportAccelerationForActiveModel(activeTransport, halfPos, halfDir, bendDir, center, beta, gamma, fieldSnaps, hasSources);
						float halfAccelLen = halfAccel.Length();
						if (!float.IsFinite(halfAccelLen))
						{
							halfAccel = Vector3.Zero;
						}
						else if (halfAccelLen > 50f)
						{
							halfAccel *= 50f / halfAccelLen;
						}

						Vector3 refinedDir = SafeNormalized(halfDir + (halfAccel * halfStep), halfDir);
						Vector3 refinedNext = halfPos + (refinedDir * halfStep);
						float turnAngle = ComputeDirectionTurnAngle(vBeforeStep, refinedDir);
						float errorEstimate = EstimateMetricStepError(coarseNext, refinedNext, coarseDir, refinedDir, trialStep);
						bool acceptStep = (turnAngle <= metricTurnThreshold && errorEstimate <= metricErrorTolerance) || (trialStep <= (minStep + 1e-6f)) || subdiv >= metricMaxSubdivisions;
						if (acceptStep)
						{
							step = trialStep;
							next = refinedNext;
							v = refinedDir;
							a = (acceptedAccel + halfAccel) * 0.5f;
							break;
						}

						RecordDerivativeAwareMetricSubdivisionRetry();
						acceptedStep = Mathf.Max(minStep, trialStep * 0.5f);
					}
				}
				else
				{
					v = SafeNormalized(v + a * step, v);
					next = p + v * step;
				}

				RecordTransportSteeringStep(p, vBeforeStep, v, center, fieldSnaps, hasSources);

				//////////////////////////////
				/// ////////////
				/// /////////
				// DECISION: update segment cadence using screen-space error when enabled.
				// traveled increment is ~step (v is normalized)
				traveled += step;
				///////////////////////////////////
				/////////////////
				///////////
			}
			else
			{
				// EFFECT: analytic bend path (no integration), step by StepLength.
				float t = s * stepLength;
				// DECISION: stop when max distance exceeded.
				if (t > maxDistance) break;

				float bend = beta * FastPow(t, gamma) * bendScale;
				next = origin + dir * t + bendDir * bend;
				traveled = t;
			}

			// DECISION: emit segments only at adaptive cadence.
			bool shouldEmit;
			if (emitEveryMetricStep)
			{
				shouldEmit = (next - p).LengthSquared() > 1e-12f;
			}
			else
			{
				stepsSinceEmit++;
				shouldEmit = s > 0 && stepsSinceEmit >= ce;
			}

			// DECISION: emit when cadence threshold reached (skip s=0) or on each accepted metric step.
			if (shouldEmit)
			{
				if (!emitEveryMetricStep)
				{
					stepsSinceEmit = 0;
				}

				// DECISION: optionally filter segments by insight plane slab.
				if (useInsightPlane && !SegmentCrossesPlane(p, next, insightPlane, insightEps))
				{
					p = next;
					continue;
				}

				// DECISION: guard output capacity.
				if (written >= outCapacity) break;

				RaySeg seg = new RaySeg
				{
					A = p,
					B = next,
					TraveledB = traveled,
					RadiusBound = RadiusMin,
					BoundaryRemapCount = boundaryRemapCount,
					EventCount = ledgerEventCount,
					BoundaryCrossings = ledgerBoundaryCrossings,
					TransformCount = ledgerTransformCount,
					EntryCount = ledgerEntryCount,
					ExitCount = ledgerExitCount,
					LastCrossingLayer = ledgerLastCrossingLayer,
					LastCrossingKind = (byte)ledgerLastCrossingKind,
					AmbiguousOrdering = ledgerAmbiguousOrdering
				};
				outSegs[outOffset + written] = seg;
				// DECISION: allow optional callback to terminate segment emission early.
				bool stop = onSegment != null && onSegment(seg, written);
				written++;
				// DECISION: stop building segments when callback requests it.
				if (stop) break;
			}

			p = next;
		}

		FinalizeDerivativeAwareStepState(ref derivativeStepState);
		return written;
	}

	// Pass-1 variant to avoid per-pixel delegate allocations in Parallel.For.
	public int BuildRaySegmentsCamera_Pass1(
		PhysicsDirectSpaceState3D space,
		ref PhysicsRayQueryParameters3D quickRayParams,
		Camera3D cam,
		float camPixelsPerRadian,
		Vector3 camPosSnapshot,
		Vector3 origin, Vector3 dir, Vector3 bendDir,
		Vector3 center, float beta, float gamma,
		FieldSourceSnap[] fieldSnaps, bool hasSources,
		float maxDistance,
		RaySeg[] outSegs, int outOffset, int outCapacity,
		Plane insightPlane, bool useInsightPlane, float insightEps,
		bool stopOnHit,
		bool pass1DoHitTest,
		int pass1ProbeEveryNSegments,
		float pass1ProbeMinTravelDelta,
		string renderSpaceSource,
		string diagnosticSceneName,
		string diagnosticFixtureName,
		string diagnosticModeToken,
		out Pass1HitInfo hitInfo,
		out bool stoppedEarly,
		out bool maxStepsReached,
		out int hitSegIndex,
		out int stepsIntegrated,
		out int fieldEvals,
		out int pass1Raycasts,
		out int pass1ProbeHits,
		out int fieldGridHits,
		out int fieldGridMisses,
		out int fieldGridFallbacks,
		out int fieldSourceEvals,
		out float curvatureMax,
		out float curvatureMean,
		out float dkMax,
		out float d2kMax,
		out float turnSum,
		out float turnMax,
		CurvatureBoundGrid curvatureGrid,
		FieldGrid3D fieldGrid = null)
	{
		// CROSS-CLASS CONTRACT: GrinFilmCamera calls this to build segments + optional pass-1 hit probes.
		// ASSUMPTION: origin/dir/bendDir are in world space; dir normalized.
		// EFFECT: incorrect inputs break pass-1 hit results and downstream film shading.

		Vector3 p = origin;
		Vector3 v = dir; // assumed normalized

		float traveled = 0f;

		// CONTROL FACTOR: CollisionEveryNSteps is the base segment cadence.
		int ceBase = Mathf.Max(1, CollisionEveryNSteps);
		int ce = ceBase;
		int stepsSinceEmit = 0;

		// camera snapshots are captured on the main thread by the caller.
		bool hasCam = cam != null;
		// DECISION: use caller-provided snapshot only when cadence is enabled and camera exists.
		float pxPerRad = (UseScreenSpaceCollisionCadence && hasCam) ? camPixelsPerRadian : 0f;
		// DECISION: fall back to origin if camera is missing.
		Vector3 camPos = hasCam ? camPosSnapshot : Vector3.Zero;

		// CONTROL FACTOR: MinStepLength/MaxStepLength clamp adaptive step size.
		float minStep = Mathf.Max(0.0001f, Mathf.Min(MinStepLength, MaxStepLength));
		float maxStep = Mathf.Max(MinStepLength, MaxStepLength);

		int written = 0;
		stepsIntegrated = 0;
		fieldEvals = 0;
		pass1Raycasts = 0;
		pass1ProbeHits = 0;
		fieldGridHits = 0;
		fieldGridMisses = 0;
		fieldGridFallbacks = 0;
		fieldSourceEvals = 0;
		curvatureMax = 0f;
		curvatureMean = 0f;
		dkMax = 0f;
		d2kMax = 0f;
		turnSum = 0f;
		turnMax = 0f;

		hitInfo = new Pass1HitInfo
		{
			Found = false,
			Distance = float.PositiveInfinity,
			Position = Vector3.Zero,
			Normal = Vector3.Up,
			ColliderId = 0,
			PrimitiveOrShapeId = -1
		};
		stoppedEarly = false;
		maxStepsReached = false;
		hitSegIndex = -1;

		// Precompute for non-integrated mode
		float bendScale = BendScale;
		float stepLength = StepLength;
		float stepAdaptGain = StepAdaptGain;
		float radiusSafety = RadiusSafety;
		float radiusMin = RadiusMin;
		bool useCurvatureGrid = curvatureGrid != null;
		// Cache boundary layer state for this ray (read-only; written on main thread before Parallel.For).
		BoundaryLayerSnap[] boundaryLayers = _boundaryLayerSnaps;
		bool hasBoundaryLayers = _hasBoundaryLayers;
		// CONTROL FACTOR: pass1ProbeEveryNSegments controls cadence-based probing.
		int probeEvery = pass1ProbeEveryNSegments;
		bool useProbeEvery = probeEvery > 0;
		// CONTROL FACTOR: pass1ProbeMinTravelDelta controls travel-based probing.
		float probeMinTravelDelta = pass1ProbeMinTravelDelta;
		bool useProbeMinTravel = probeMinTravelDelta > 0f;
		// DECISION: when cadence probing is off, set countdown to max to avoid triggering.
		int probeCountdown = useProbeEvery ? probeEvery : int.MaxValue;
		float traveledSinceProbe = 0f;
		TransportModel activeTransport = hasSources ? ResolveActiveTransportModel(fieldSnaps) : TransportModel.GRIN_Optical;
		bool emitEveryMetricStep = UseIntegratedField && activeTransport == TransportModel.Metric_NullGeodesic;
		int maxIntegrationSteps = emitEveryMetricStep
			? Mathf.Max(1, StepsPerRay * GetMetricAdaptiveStepMultiplier())
			: StepsPerRay;
		float metricTurnThreshold = GetMetricAdaptiveTurnThresholdRadians();
		float metricErrorTolerance = Mathf.Max(1e-5f, MetricAdaptiveErrorTolerance);
		int metricMaxSubdivisions = Mathf.Max(0, MetricAdaptiveMaxSubdivisions);
			DerivativeAwareStepState derivativeStepState = default;
			Pass1TelemetryDerivativeState telemetryDerivativeState = default;

		// Per-ray inside state for CrossingEvent boundary layer detection (≤32 layers via bitmask).
		// Initialized from the ray origin so that rays starting inside a volume do NOT fire an entry event.
		uint blvInsideMask = (hasBoundaryLayers && UseIntegratedField)
			? ComputeInsideMask(p, boundaryLayers)
			: 0u;
		int boundaryRemapCount = 0;
		int ledgerEventCount = 0;
		int ledgerBoundaryCrossings = 0;
		int ledgerTransformCount = 0;
		int ledgerEntryCount = 0;
		int ledgerExitCount = 0;
		int ledgerLastCrossingLayer = -1;
		LedgerCrossingKind ledgerLastCrossingKind = LedgerCrossingKind.None;
		bool ledgerAmbiguousOrdering = false;

		// DECISION: integrate along the ray for up to StepsPerRay steps.
		for (int s = 0; s <= maxIntegrationSteps; s++)
		{
			stepsIntegrated++;
			float prevTraveled = traveled;

			if (UseIntegratedField && hasSources && TryGetAbsorbingSourceAtPoint(p, fieldSnaps, out _))
			{
				stoppedEarly = true;
				break;
			}

			// Boundary layer: continuous effects and crossing-event detection.
			if (hasBoundaryLayers && UseIntegratedField)
			{
				BoundaryInteractionLedgerDelta boundaryDelta = ApplyBoundaryLayerInteractions(ref p, ref v, boundaryLayers, ref blvInsideMask);
				boundaryRemapCount += boundaryDelta.TransformCount;
				ledgerEventCount += boundaryDelta.EventCount;
				ledgerBoundaryCrossings += boundaryDelta.BoundaryCrossings;
				ledgerTransformCount += boundaryDelta.TransformCount;
				ledgerEntryCount += boundaryDelta.EntryCount;
				ledgerExitCount += boundaryDelta.ExitCount;
				if (boundaryDelta.LastCrossingKind != LedgerCrossingKind.None)
				{
					ledgerLastCrossingKind = boundaryDelta.LastCrossingKind;
					ledgerLastCrossingLayer = boundaryDelta.LastCrossingLayer;
				}
				ledgerAmbiguousOrdering |= boundaryDelta.AmbiguousOrdering;
			}

			Vector3 next = p;

			// DECISION: integrated vs analytic field path.
			if (UseIntegratedField)
			{
				// Early-out if we are already at/over max distance
				float remaining = maxDistance - traveled;
				// DECISION: stop when max distance reached.
				if (remaining <= 0f) break;

				Vector3 a;
				a = Vector3.Zero;

				// DECISION: prefer field grid sampling when available.
				// NOTE: Metric_NullGeodesic still borrows GRIN here because FieldGrid3D caches GRIN acceleration;
				// an in-bounds grid hit bypasses StepTransport_MetricStub for this step.
				if (fieldGrid != null && fieldGrid.TrySample(p, out a))
				{
					fieldGridHits++;
					if (activeTransport == TransportModel.Metric_NullGeodesic)
					{
						_metricGridBypassStepCount++;
					}
				}
				// DECISION: grid exists but point is outside bounds — fall through to source eval.
				else if (fieldGrid != null)
				{
					fieldGridMisses++;
					if (hasSources)
					{
						a = ComputeTransportAccelerationForActiveModel(activeTransport, p, v, bendDir, center, beta, gamma, fieldSnaps, hasSources);
						fieldGridFallbacks++;
						fieldSourceEvals++;
					}
				}
				// DECISION: fall back to field sources if any.
				else if (hasSources)
				{
					a = ComputeTransportAccelerationForActiveModel(activeTransport, p, v, bendDir, center, beta, gamma, fieldSnaps, hasSources);
					fieldSourceEvals++;
				}
				else
				{
					a = StepTransport_GRIN(p, center, beta, gamma, fieldSnaps, hasSources);
					fieldSourceEvals++;
				}
					fieldEvals++;

					RecordPass1TelemetryDerivativeSample(a, v, ref telemetryDerivativeState);

					float aLen = a.Length();
				// DECISION: sanitize non-finite/overlarge acceleration.
				if (!float.IsFinite(aLen)) { a = Vector3.Zero; aLen = 0f; }
				else if (aLen > 50f) { a *= (50f / aLen); aLen = 50f; } // DECISION: clamp extreme acceleration.

				float step = ComputeAdaptiveIntegratedStepLength(
					v,
					a,
					minStep,
					maxStep,
					applyLowCurvatureBoost: true,
					applyMetricCurvatureGain: emitEveryMetricStep,
					ref derivativeStepState);

				// DECISION: clamp to remaining distance so we don't overshoot maxDistance.
				if (step > remaining) step = remaining; // DECISION: clamp step to remaining distance.

				// DECISION: update segment cadence using screen-space error when enabled.
				if (emitEveryMetricStep)
				{
					ce = 1;
				}
				else if (UseScreenSpaceCollisionCadence && cam != null)
				{
					float aPerp = PerpAccelLen(a, v);   // v from previous iteration; ok
					float depth = (p - camPos).Length();
					ce = ComputeCeFromScreenError(ceBase, step, aPerp, depth, pxPerRad, CollisionMaxErrorPixels);
				}
				else
				{
					ce = ceBase;
				}

				Vector3 vBeforeStep = v;
				if (emitEveryMetricStep)
				{
					float acceptedStep = step;
					Vector3 acceptedNext = p;
					Vector3 acceptedDir = v;
					Vector3 acceptedAccel = a;
					int extraMetricFieldEvals = 0;

					for (int subdiv = 0; subdiv <= metricMaxSubdivisions; subdiv++)
					{
						float trialStep = acceptedStep;
						Vector3 coarseDir = SafeNormalized(v + (acceptedAccel * trialStep), v);
						Vector3 coarseNext = p + (coarseDir * trialStep);

						float halfStep = 0.5f * trialStep;
						Vector3 halfDir = SafeNormalized(v + (acceptedAccel * halfStep), v);
						Vector3 halfPos = p + (halfDir * halfStep);

						Vector3 halfAccel = Vector3.Zero;
						if (fieldGrid != null && fieldGrid.TrySample(halfPos, out halfAccel))
						{
						}
						else if (hasSources)
						{
							halfAccel = ComputeTransportAccelerationForActiveModel(activeTransport, halfPos, halfDir, bendDir, center, beta, gamma, fieldSnaps, hasSources);
						}
						else
						{
							halfAccel = StepTransport_GRIN(halfPos, center, beta, gamma, fieldSnaps, hasSources);
						}

						float halfAccelLen = halfAccel.Length();
						if (!float.IsFinite(halfAccelLen))
						{
							halfAccel = Vector3.Zero;
						}
						else if (halfAccelLen > 50f)
						{
							halfAccel *= 50f / halfAccelLen;
						}

						extraMetricFieldEvals++;

						Vector3 refinedDir = SafeNormalized(halfDir + (halfAccel * halfStep), halfDir);
						Vector3 refinedNext = halfPos + (refinedDir * halfStep);
						float turnAngle = ComputeDirectionTurnAngle(vBeforeStep, refinedDir);
						float errorEstimate = EstimateMetricStepError(coarseNext, refinedNext, coarseDir, refinedDir, trialStep);
						bool acceptStep = (turnAngle <= metricTurnThreshold && errorEstimate <= metricErrorTolerance) || (trialStep <= (minStep + 1e-6f)) || subdiv >= metricMaxSubdivisions;
						if (acceptStep)
						{
							acceptedStep = trialStep;
							acceptedNext = refinedNext;
							acceptedDir = refinedDir;
							acceptedAccel = (acceptedAccel + halfAccel) * 0.5f;
							break;
						}

						RecordDerivativeAwareMetricSubdivisionRetry();
						acceptedStep = Mathf.Max(minStep, trialStep * 0.5f);
					}

					fieldEvals += extraMetricFieldEvals;
					step = acceptedStep;
					next = acceptedNext;
					v = acceptedDir;
					a = acceptedAccel;
				}
				else
				{
					v = SafeNormalized(v + a * step, v);
					next = p + v * step;
				}

				RecordTransportSteeringStep(p, vBeforeStep, v, center, fieldSnaps, hasSources);
				RecordPass1TelemetryTurnSample(vBeforeStep, v, ref telemetryDerivativeState);

				// traveled increment is ~step (v is normalized)
				traveled += step;
			}
			else
			{
				// EFFECT: analytic bend path (no integration), step by StepLength.
				float t = s * stepLength;
				// DECISION: stop when max distance exceeded.
				if (t > maxDistance) break;

				float bend = beta * FastPow(t, gamma) * bendScale;
				next = origin + dir * t + bendDir * bend;
				traveled = t;
			}

			float traveledDelta = traveled - prevTraveled;
			// DECISION: track only positive travel deltas for probe distance.
			if (traveledDelta > 0f)
				traveledSinceProbe += traveledDelta;

			// DECISION: countdown-based probe gate.
			if (useProbeEvery && probeCountdown > int.MinValue)
				probeCountdown--;

			// DECISION: emit segments only at adaptive cadence.
			bool shouldEmit;
			if (emitEveryMetricStep)
			{
				shouldEmit = (next - p).LengthSquared() > 1e-12f;
			}
			else
			{
				stepsSinceEmit++;
				shouldEmit = s > 0 && stepsSinceEmit >= ce;
			}

			// DECISION: emit when cadence threshold reached (skip s=0) or on each accepted metric step.
			if (shouldEmit)
			{
				if (!emitEveryMetricStep)
				{
					stepsSinceEmit = 0;
				}

				// DECISION: optionally filter segments by insight plane slab.
				if (useInsightPlane && !SegmentCrossesPlane(p, next, insightPlane, insightEps))
				{
					p = next;
					continue;
				}

				// DECISION: guard output capacity.
				if (written >= outCapacity) break;

				int segIndex = written;
				float radiusBound = radiusMin;
				if (useCurvatureGrid)
				{
					var p0 = new System.Numerics.Vector3(p.X, p.Y, p.Z);
					var p1 = new System.Numerics.Vector3(next.X, next.Y, next.Z);
					float k0 = curvatureGrid.LookupKmax(p0);
					float k1 = curvatureGrid.LookupKmax(p1);
					float k = (k0 > k1) ? k0 : k1;
					float dt = (next - p).Length();
					float radius = radiusSafety * 0.5f * k * (dt * dt);
					if (radius < radiusMin) radius = radiusMin;
					radiusBound = radius;
				}
				RaySeg seg = new RaySeg
				{
					A = p,
					B = next,
					TraveledB = traveled,
					RadiusBound = radiusBound,
					BoundaryRemapCount = boundaryRemapCount,
					EventCount = ledgerEventCount,
					BoundaryCrossings = ledgerBoundaryCrossings,
					TransformCount = ledgerTransformCount,
					EntryCount = ledgerEntryCount,
					ExitCount = ledgerExitCount,
					LastCrossingLayer = ledgerLastCrossingLayer,
					LastCrossingKind = (byte)ledgerLastCrossingKind,
					AmbiguousOrdering = ledgerAmbiguousOrdering
				};
				// TODO(metric-pass1): When Metric_NullGeodesic emits persistent MetricRayState
				// steps, mirror this RaySeg emission through RendererCore.Transport.
				// MetricSegmentCompatibility.BuildEmission(...) for pass-1 quick probes and
				// pass-2 narrowphase sidecar metadata without changing RaySeg[] consumers.
				outSegs[outOffset + written] = seg;
				written++;

				bool probeByTravel = useProbeMinTravel && (traveledSinceProbe >= probeMinTravelDelta);
				bool probeByCountdown = useProbeEvery && (probeCountdown <= 0);
				// DECISION: only probe when pass1DoHitTest enabled and a probe gate is satisfied.
				bool doProbe = pass1DoHitTest && (probeByTravel || probeByCountdown);
				bool probeSpaceValid = space != null && GodotObject.IsInstanceValid(space);
				bool probeQueryValid = quickRayParams != null;

				// DECISION: only probe when allowed by gates and budget.
				if (doProbe)
				{
					if (!probeSpaceValid || !probeQueryValid)
					{
						if (System.Threading.Interlocked.Exchange(ref _pass1ProbeUnavailableWarned, 1) == 0)
						{
							GD.PushWarning(
								$"[Pass1Probe][RendererGuard] scene={FormatRayDiagToken(diagnosticSceneName)} " +
								$"fixture={FormatRayDiagToken(diagnosticFixtureName)} mode={FormatRayDiagToken(diagnosticModeToken)} " +
								$"source={FormatRayDiagToken(renderSpaceSource)} spaceNull={(probeSpaceValid ? 0 : 1)} " +
								$"quickRayValid={(probeQueryValid ? 1 : 0)} pass1ProbeEnabled={(pass1DoHitTest ? 1 : 0)} " +
								$"reason=skip_probe_without_crash");
						}
						doProbe = false;
					}
				}

				if (doProbe)
				{
					traveledSinceProbe = 0f;
					// DECISION: reset countdown when using cadence-based probing.
					if (useProbeEvery)
						probeCountdown = probeEvery;
					pass1Raycasts++;
					quickRayParams.From = seg.A;
					quickRayParams.To = seg.B;
					if (!TryIntersectRayWithGuard(
						space,
						quickRayParams,
						pass1DoHitTest,
						"pass1_probe",
						renderSpaceSource,
						diagnosticSceneName,
						diagnosticFixtureName,
						diagnosticModeToken,
						ref _pass1ProbeQueryFailureWarned,
						out var hit0))
					{
						continue;
					}
					// DECISION: process hit results only when raycast hits something.
					if (hit0.Count > 0)
					{
						pass1ProbeHits++;
						Vector3 hp = (Vector3)hit0["position"];
						Vector3 hn = (Vector3)hit0["normal"];
						float segLen = (seg.B - seg.A).Length();
						float d = seg.TraveledB - segLen + (hp - seg.A).Length();
						// DECISION: keep nearest hit encountered so far.
						if (!hitInfo.Found || d < hitInfo.Distance)
						{
							hitInfo.Found = true;
							hitInfo.Distance = d;
							hitInfo.Position = hp;
							hitInfo.Normal = hn;
							// DECISION: collider_id may be absent; only read when present.
							if (hit0.ContainsKey("collider_id"))
								hitInfo.ColliderId = (ulong)(long)hit0["collider_id"];
							hitInfo.PrimitiveOrShapeId = ExtractPrimitiveOrShapeId(hit0);
						}
						// DECISION: remember first segment index that hit.
						if (hitSegIndex < 0)
							hitSegIndex = segIndex;

						// DECISION: optionally stop building segments on first hit.
						if (stopOnHit)
						{
							stoppedEarly = true;
							break;
						}
					}
				}
			}

			p = next;
		}

			maxStepsReached = !stoppedEarly && stepsIntegrated >= maxIntegrationSteps + 1 && traveled < maxDistance - 1e-4f;
			FinalizeDerivativeAwareStepState(ref derivativeStepState);
			curvatureMax = telemetryDerivativeState.CurvatureMax;
			curvatureMean = telemetryDerivativeState.SampleCount > 0
				? (telemetryDerivativeState.CurvatureSum / telemetryDerivativeState.SampleCount)
				: 0f;
			dkMax = telemetryDerivativeState.DkMax;
			d2kMax = telemetryDerivativeState.D2kMax;
			turnSum = telemetryDerivativeState.TurnSum;
			turnMax = telemetryDerivativeState.TurnMax;
			return written;
		}

	public LedgerContinuationSummary ContinueLedgerAfterPass1Stop(
		Vector3 startPos,
		Vector3 startDir,
		Vector3 bendDir,
		Vector3 center,
		float beta,
		float gamma,
		FieldSourceSnap[] fieldSnaps,
		bool hasSources,
		float maxDistance,
		int maxExtraSegments,
		float startTraveled,
		int startBoundaryRemapCount,
		int startEventCount,
		int startBoundaryCrossings,
		int startTransformCount,
		int startEntryCount,
		int startExitCount,
		int startLastCrossingLayer,
		byte startLastCrossingKind,
		bool startAmbiguousOrdering)
	{
		const int tier1MaxSegments = 4;
		const int tier2AdditionalSegments = 3;
		int maxSafeLimit = Math.Max(1, maxExtraSegments);
		int tier1Limit = Math.Min(maxSafeLimit, tier1MaxSegments);
		int tier2Limit = Math.Min(maxSafeLimit, tier1Limit + tier2AdditionalSegments);

		if (maxSafeLimit <= 0 || maxDistance <= startTraveled)
		{
			return new LedgerContinuationSummary(
				startTraveled,
				startBoundaryRemapCount,
				startEventCount,
				startBoundaryCrossings,
				startTransformCount,
				startEntryCount,
				startExitCount,
				startLastCrossingLayer,
				startLastCrossingKind,
				startAmbiguousOrdering,
				0,
				false,
				false);
		}

		Vector3 p = startPos;
		Vector3 v = startDir.LengthSquared() > 1e-12f ? startDir.Normalized() : Vector3.Forward;
		float traveled = startTraveled;
		int boundaryRemapCount = startBoundaryRemapCount;
		int ledgerEventCount = startEventCount;
		int ledgerBoundaryCrossings = startBoundaryCrossings;
		int ledgerTransformCount = startTransformCount;
		int ledgerEntryCount = startEntryCount;
		int ledgerExitCount = startExitCount;
		int ledgerLastCrossingLayer = startLastCrossingLayer;
		LedgerCrossingKind ledgerLastCrossingKind = (LedgerCrossingKind)startLastCrossingKind;
		bool ledgerAmbiguousOrdering = startAmbiguousOrdering;
		int integratedSegments = 0;
		bool transformChanged = false;
		int unchangedTransformSteps = 0;

		BoundaryLayerSnap[] boundaryLayers = _boundaryLayerSnaps;
		bool hasBoundaryLayers = _hasBoundaryLayers;
		uint blvInsideMask = (hasBoundaryLayers && UseIntegratedField)
			? ComputeInsideMask(p, boundaryLayers)
			: 0u;

		float minStep = Mathf.Max(0.0001f, Mathf.Min(MinStepLength, MaxStepLength));
		float maxStep = Mathf.Max(MinStepLength, MaxStepLength);
		TransportModel activeTransport = hasSources ? ResolveActiveTransportModel(fieldSnaps) : TransportModel.GRIN_Optical;
		bool emitEveryMetricStep = UseIntegratedField && activeTransport == TransportModel.Metric_NullGeodesic;
		DerivativeAwareStepState derivativeStepState = default;

		while (integratedSegments < maxSafeLimit && traveled < maxDistance)
		{
			if (UseIntegratedField && hasSources && TryGetAbsorbingSourceAtPoint(p, fieldSnaps, out _))
				break;

			if (hasBoundaryLayers && UseIntegratedField)
			{
				int transformCountBefore = ledgerTransformCount;
				BoundaryInteractionLedgerDelta boundaryDelta = ApplyBoundaryLayerInteractions(ref p, ref v, boundaryLayers, ref blvInsideMask);
				boundaryRemapCount += boundaryDelta.TransformCount;
				ledgerEventCount += boundaryDelta.EventCount;
				ledgerBoundaryCrossings += boundaryDelta.BoundaryCrossings;
				ledgerTransformCount += boundaryDelta.TransformCount;
				ledgerEntryCount += boundaryDelta.EntryCount;
				ledgerExitCount += boundaryDelta.ExitCount;
				if (boundaryDelta.LastCrossingKind != LedgerCrossingKind.None)
				{
					ledgerLastCrossingKind = boundaryDelta.LastCrossingKind;
					ledgerLastCrossingLayer = boundaryDelta.LastCrossingLayer;
				}
				ledgerAmbiguousOrdering |= boundaryDelta.AmbiguousOrdering;
				if (ledgerExitCount > startExitCount)
					break;
				if (ledgerTransformCount != transformCountBefore)
				{
					transformChanged = true;
					unchangedTransformSteps = 0;
				}
				else
				{
					unchangedTransformSteps++;
				}
			}

			float remaining = maxDistance - traveled;
			if (remaining <= 0f)
				break;

			Vector3 next = p;
			if (UseIntegratedField)
			{
				Vector3 a = Vector3.Zero;
				if (hasSources)
				{
					a = ComputeTransportAccelerationForActiveModel(activeTransport, p, v, bendDir, center, beta, gamma, fieldSnaps, hasSources);
				}
				else
				{
					a = StepTransport_GRIN(p, center, beta, gamma, fieldSnaps, hasSources);
				}

				float aLen = a.Length();
				if (!float.IsFinite(aLen))
				{
					a = Vector3.Zero;
				}
				else if (aLen > 50f)
				{
					a *= 50f / aLen;
				}

				float step = ComputeAdaptiveIntegratedStepLength(
					v,
					a,
					minStep,
					maxStep,
					applyLowCurvatureBoost: true,
					applyMetricCurvatureGain: emitEveryMetricStep,
					ref derivativeStepState);
				if (step > remaining)
					step = remaining;

				if (emitEveryMetricStep)
				{
					v = SafeNormalized(v + a * step, v);
					next = p + v * step;
				}
				else
				{
					v = SafeNormalized(v + a * step, v);
					next = p + v * step;
				}
				traveled += step;
			}
			else
			{
				float step = Mathf.Min(Mathf.Max(0.0001f, StepLength), remaining);
				next = p + v * step;
				traveled += step;
			}

			if ((next - p).LengthSquared() <= 1e-12f)
				break;

			p = next;
			integratedSegments++;

			bool unresolved = ledgerExitCount <= startExitCount;
			if (!unresolved)
				break;

			if (integratedSegments >= tier1Limit)
			{
				bool allowTier2 = integratedSegments < tier2Limit && (transformChanged || unchangedTransformSteps <= 1);
				if (!allowTier2)
					break;
			}
		}

		FinalizeDerivativeAwareStepState(ref derivativeStepState);
		return new LedgerContinuationSummary(
			traveled,
			boundaryRemapCount,
			ledgerEventCount,
			ledgerBoundaryCrossings,
			ledgerTransformCount,
			ledgerEntryCount,
			ledgerExitCount,
			ledgerLastCrossingLayer,
			(byte)ledgerLastCrossingKind,
			ledgerAmbiguousOrdering,
			integratedSegments,
			ledgerExitCount > startExitCount,
			transformChanged);
	}

	public FieldSourceSnap[] SnapshotFieldSources(Godot.Collections.Array<Node> nodes)
	{
		// DECISION: no nodes means empty snapshot array.
		if (nodes == null || nodes.Count == 0) return Array.Empty<FieldSourceSnap>();

		var list = new List<FieldSourceSnap>(nodes.Count);

		// DECISION: snapshot only FieldSource3D nodes into lightweight structs.
		foreach (var n in nodes)
		{
			// DECISION: only snapshot FieldSource3D nodes.
			if (n is not FieldSource3D fs) continue;
			list.Add(BuildFieldSourceSnap(fs));
		}

		FieldSourceSnap[] snaps = list.ToArray();
		MaybeLogActiveTransport(snaps);
		return snaps;
	}

	public static FieldSourceSnap BuildFieldSourceSnap(FieldSource3D fs)
	{
		FieldSource3D.ResolvedFieldParams resolved = fs.ResolveEffectiveParams(out _);
		float inner = Mathf.Max(0f, resolved.rInner);
		float outer = Mathf.Max(0f, resolved.rOuter);
		if (outer > 0f && outer < inner)
		{
			outer = inner;
		}

		bool isBox = resolved.shapeType == FieldShapeType.BoxVolume;
		return new FieldSourceSnap
		{
			Enabled = resolved.enabled,
			Center = fs.GlobalPosition,
			TransportModel = fs.TransportModel,
			ModeFlags = resolved.modeFlags,
			ShapeType = resolved.shapeType,
			CurveType = resolved.curveType,
			RInner = inner,
			ROuter = outer,
			Amp = resolved.amp,
			CurveA = resolved.a,
			CurveB = resolved.b,
			CurveC = resolved.c,
			Sigma = Mathf.Max(0f, resolved.sigma),
			OverrideBetaScale = resolved.overrideBetaScale,
			BetaScale = resolved.betaScale,
			EdgeSoftness = Mathf.Clamp(resolved.edgeSoftness, 0f, 1f),
			CustomCurve = resolved.customCurve,
			// BoxVolume support region: capture extents and inverse world orientation so
			// ContainsPointInBox() can transform the sample point into the node's local frame.
			HalfExtents = isBox ? fs.BoxExtents : Vector3.Zero,
			BoxInvBasis = isBox ? fs.GlobalTransform.Basis.Inverse() : default
		};
	}

	// ===== Boundary Layer Volume Snapshot + Evaluation =====

	private BoundaryLayerSnap[] SnapshotBoundaryLayers(Godot.Collections.Array<Node> nodes)
	{
		if (nodes == null || nodes.Count == 0) return Array.Empty<BoundaryLayerSnap>();
		var list = new List<BoundaryLayerSnap>(nodes.Count);
		foreach (Node node in nodes)
		{
			if (node is BoundaryLayerVolume blv && blv.Enabled)
				list.Add(BuildBoundaryLayerSnap(blv));
		}
		var layerIndexByInstanceId = new Dictionary<ulong, int>(list.Count);
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].NodeInstanceId != 0)
				layerIndexByInstanceId[list[i].NodeInstanceId] = i;
		}
		for (int i = 0; i < list.Count; i++)
		{
			BoundaryLayerSnap snap = list[i];
			snap.LinkedLayerIndex = -1;
			if (snap.LinkedNodeInstanceId != 0 &&
				layerIndexByInstanceId.TryGetValue(snap.LinkedNodeInstanceId, out int linkedLayerIndex))
			{
				snap.LinkedLayerIndex = linkedLayerIndex;
			}
			list[i] = snap;
		}
		// Guard: crossing detection uses a uint bitmask; only the first 32 layers can be tracked.
		// Continuous mode is unaffected (ApplyBoundaryLayerBias iterates all layers).
		// Warn once if any CrossingEvent layer falls beyond the bitmask window.
		if (list.Count > 32)
		{
			for (int i = 32; i < list.Count; i++)
			{
				if (list[i].ExecutionMode == BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent)
				{
					GD.PushWarning(
						$"[BoundaryLayerVolume] {list.Count} enabled layers registered. " +
						$"CrossingEvent detection uses a uint bitmask capped at 32. " +
						$"Layer '{list[i].NodeName}' (index {i}) and any CrossingEvent layers beyond " +
						$"index 31 will not fire crossing events. Continuous mode is unaffected. " +
						$"Reduce layer count or switch excess layers to Continuous.");
					break; // one warning per snapshot is enough
				}
			}
		}
		return list.ToArray();
	}

	private static BoundaryLayerSnap BuildBoundaryLayerSnap(BoundaryLayerVolume blv)
	{
		bool isBox = blv.ShapeType == BoundaryLayerVolume.BoundaryShapeType.Box;
		Transform3D worldFromLocal = blv.GlobalTransform;
		Transform3D localFromWorld = worldFromLocal.AffineInverse();
		Node3D linkedNode = null;
		if (!blv.LinkedBoundaryPath.IsEmpty)
			linkedNode = blv.GetNodeOrNull<Node3D>(blv.LinkedBoundaryPath);
		if (blv.Behavior == BoundaryLayerVolume.BoundaryBehavior.SceneTransform && linkedNode == null)
			GD.PushWarning($"{blv.Name}: SceneTransform behavior requires a valid LinkedBoundaryPath.");
		float linkedRadius = blv.Radius;
		if (linkedNode is BoundaryLayerVolume linkedBoundary)
			linkedRadius = Mathf.Max(0f, linkedBoundary.Radius);
		// Transform bias direction from local to world space and re-normalize.
		Vector3 biasWorld = worldFromLocal.Basis * blv.BiasDirection;
		float bLen = biasWorld.Length();
		if (bLen > FieldMath.Epsilon) biasWorld /= bLen;
		return new BoundaryLayerSnap
		{
			Enabled           = blv.Enabled,
			NodeInstanceId    = blv.GetInstanceId(),
			Center            = blv.GlobalPosition,
			WorldFromLocal    = worldFromLocal,
			LocalFromWorld    = localFromWorld,
			ShapeType         = blv.ShapeType,
			Radius            = Mathf.Max(0f, blv.Radius),
			HalfExtents       = isBox ? blv.BoxExtents : Vector3.Zero,
			InvBasis          = isBox ? worldFromLocal.Basis.Inverse() : default,
			ExecutionMode     = blv.ExecutionMode,
			CrossingPolicy    = blv.CrossingPolicy,
			Behavior          = blv.Behavior,
			BiasDirection     = biasWorld,
			BiasStrength      = Mathf.Clamp(blv.BiasStrength, 0f, 1f),
			HasLinkedTransform = linkedNode != null,
			LinkedNodeInstanceId = linkedNode?.GetInstanceId() ?? 0,
			LinkedLayerIndex  = -1,
			LinkedWorldFromLocal = linkedNode?.GlobalTransform ?? default,
			LinkedCenter = linkedNode?.GlobalPosition ?? Vector3.Zero,
			LinkedRadius = linkedRadius,
			SceneTransformExitOffset = Mathf.Max(0f, blv.SceneTransformExitOffset),
			NodeName          = blv.Name,
			DebugLogCrossings = blv.DebugLogCrossings
		};
	}

	private static readonly Transform3D BoundarySceneFlip = new Transform3D(
		Basis.FromEuler(new Vector3(0f, Mathf.Pi, 0f)),
		Vector3.Zero);

	// Returns true when world-space point p is inside the boundary layer's support region.
	private static bool IsInsideBoundaryLayer(Vector3 p, in BoundaryLayerSnap layer)
	{
		switch (layer.ShapeType)
		{
			case BoundaryLayerVolume.BoundaryShapeType.Sphere:
				return p.DistanceSquaredTo(layer.Center) < (layer.Radius * layer.Radius);
			case BoundaryLayerVolume.BoundaryShapeType.Box:
			{
				Vector3 local = layer.InvBasis * (p - layer.Center);
				return Mathf.Abs(local.X) <= layer.HalfExtents.X
					&& Mathf.Abs(local.Y) <= layer.HalfExtents.Y
					&& Mathf.Abs(local.Z) <= layer.HalfExtents.Z;
			}
			default:
				return false;
		}
	}

	// Applies all Continuous-mode boundary layer behaviors at position p and returns the
	// (possibly re-normalized) ray direction. Returns v unchanged when no Continuous layer
	// contains p. Each qualifying layer is evaluated independently; effects accumulate.
	// CrossingEvent-mode layers are skipped here — they are dispatched by ApplyBoundaryLayerCrossings.
	// TODO: add additional Continuous cases here as new BoundaryBehavior values are introduced.
	private static Vector3 ApplyBoundaryLayerBias(Vector3 p, Vector3 v, BoundaryLayerSnap[] layers)
	{
		for (int i = 0; i < layers.Length; i++)
		{
			ref readonly BoundaryLayerSnap layer = ref layers[i];
			if (!layer.Enabled) continue;
			// Skip CrossingEvent layers — dispatched via ApplyBoundaryLayerCrossings.
			if (layer.ExecutionMode != BoundaryLayerVolume.BoundaryExecutionMode.Continuous) continue;
			if (!IsInsideBoundaryLayer(p, layer)) continue;
			switch (layer.Behavior)
			{
				case BoundaryLayerVolume.BoundaryBehavior.DirectionBias:
					v = SafeNormalized(v + layer.BiasDirection * layer.BiasStrength, v);
					break;
			}
		}
		return v;
	}

	// Returns a bitmask where bit i is set iff p is inside layers[i].
	// Hard cap at 32: layers at index >= 32 are excluded from crossing detection.
	// Continuous mode (ApplyBoundaryLayerBias) iterates all layers and is unaffected by this cap.
	// SnapshotBoundaryLayers emits a warning when any CrossingEvent layer falls beyond index 31.
	private static uint ComputeInsideMask(Vector3 p, BoundaryLayerSnap[] layers)
	{
		uint mask = 0u;
		int count = Mathf.Min(layers.Length, 32);
		for (int i = 0; i < count; i++)
		{
			if (layers[i].Enabled && IsInsideBoundaryLayer(p, layers[i]))
				mask |= (1u << i);
		}
		return mask;
	}

	// Dispatches CrossingEvent behaviors for layers that were crossed in this step.
	//
	// Semantics:
	//   - Entry (entryBits): bit set when layer transitions 0→1 (outside→inside).
	//   - Exit  (exitBits):  bit set when layer transitions 1→0 (inside→outside).
	//   - CrossingPolicy selects which directions dispatch: EntryOnly (default), ExitOnly, EntryAndExit.
	//     EntryOnly preserves legacy entry-only behavior exactly.
	//   - Start-inside: the initial blvInsideMask is seeded from the ray origin, so rays that begin
	//     inside a volume do NOT synthesize a false entry event on the first step.
	//   - Ordering: layers are processed in ascending snap-array index order. When multiple
	//     layers trigger in the same step, effects accumulate in that order.
	//   - Only layers with ExecutionMode == CrossingEvent are processed here.
	//     Continuous layers are handled exclusively by ApplyBoundaryLayerBias.
	//   - DirectionBias uses identical logic for entry and exit (stateless nudge; no inverse needed).
	//     If a step produces both entry and exit bits for the same layer (sub-step-width volume),
	//     EntryAndExit will apply the bias twice. This is a degenerate scene-setup case.
	private void ApplyBoundaryLayerCrossings(
		ref Vector3 p, ref Vector3 v,
		BoundaryLayerSnap[] layers,
		uint entryBits,
		uint exitBits,
		ref BoundaryInteractionLedgerDelta ledgerDelta)
	{
		for (int i = 0; i < layers.Length && i < 32; i++)
		{
			uint bit = 1u << i;
			ref readonly BoundaryLayerSnap layer = ref layers[i];
			if (!layer.Enabled) continue;
			if (layer.ExecutionMode != BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent) continue;

			bool isEntry = (entryBits & bit) != 0u;
			bool isExit  = (exitBits  & bit) != 0u;
			bool doEntry = isEntry && layer.CrossingPolicy != BoundaryLayerVolume.BoundaryCrossingPolicy.ExitOnly;
			bool doExit  = isExit  && layer.CrossingPolicy != BoundaryLayerVolume.BoundaryCrossingPolicy.EntryOnly;
			if (!doEntry && !doExit) continue;

			if (doEntry)
			{
				bool causedTransform = false;
				switch (layer.Behavior)
				{
					case BoundaryLayerVolume.BoundaryBehavior.DirectionBias:
						v = SafeNormalized(v + layer.BiasDirection * layer.BiasStrength, v);
						break;
					case BoundaryLayerVolume.BoundaryBehavior.SceneTransform:
						ApplyBoundarySceneTransform(ref p, ref v, in layer);
						RecordBoundarySceneTransformValidation();
						causedTransform = true;
						break;
				}
				RecordBoundaryValidationEvent(i, isEntry: true, isExit: false);
				ledgerDelta.RecordEvent(i, LedgerCrossingKind.Entry, causedTransform);
				if (causedTransform)
				{
					RecordBoundaryTransformPairedEgress(in layer, ref ledgerDelta);
				}
				if (layer.DebugLogCrossings && !_boundaryDebugRunActive)
					GD.Print($"[BLV] entry event: layer={i} name='{layer.NodeName}' behavior={layer.Behavior} pos=({p.X:0.##},{p.Y:0.##},{p.Z:0.##})");
			}
			if (doExit)
			{
				bool causedTransform = false;
				switch (layer.Behavior)
				{
					case BoundaryLayerVolume.BoundaryBehavior.DirectionBias:
						v = SafeNormalized(v + layer.BiasDirection * layer.BiasStrength, v);
						break;
					case BoundaryLayerVolume.BoundaryBehavior.SceneTransform:
						ApplyBoundarySceneTransform(ref p, ref v, in layer);
						RecordBoundarySceneTransformValidation();
						causedTransform = true;
						break;
				}
				RecordBoundaryValidationEvent(i, isEntry: false, isExit: true);
				ledgerDelta.RecordEvent(i, LedgerCrossingKind.Exit, causedTransform);
				if (layer.DebugLogCrossings && !_boundaryDebugRunActive)
					GD.Print($"[BLV] exit event: layer={i} name='{layer.NodeName}' behavior={layer.Behavior} pos=({p.X:0.##},{p.Y:0.##},{p.Z:0.##})");
			}
		}
	}

	private void RecordBoundaryTransformPairedEgress(in BoundaryLayerSnap layer, ref BoundaryInteractionLedgerDelta ledgerDelta)
	{
		// SceneTransform is a topological remap: the ray enters the source shell and resumes
		// just outside the linked shell. There is no sampled geometric exit crossing at the
		// destination, so we record an explicit paired egress event here for the causal ledger.
		int linkedLayerIndex = layer.LinkedLayerIndex;
		if (linkedLayerIndex < 0)
			return;

		RecordBoundaryValidationEvent(linkedLayerIndex, isEntry: false, isExit: true);
		ledgerDelta.EventCount++;
		ledgerDelta.BoundaryCrossings++;
		ledgerDelta.ExitCount++;
		ledgerDelta.LastCrossingLayer = linkedLayerIndex;
		ledgerDelta.LastCrossingKind = LedgerCrossingKind.Exit;
	}

	private static void ApplyBoundarySceneTransform(ref Vector3 p, ref Vector3 v, in BoundaryLayerSnap layer)
	{
		if (!layer.HasLinkedTransform)
			return;

		Transform3D map = layer.LinkedWorldFromLocal * BoundarySceneFlip * layer.LocalFromWorld;
		p = map * p;

		Basis directionMap = layer.LinkedWorldFromLocal.Basis * BoundarySceneFlip.Basis * layer.LocalFromWorld.Basis;
		v = SafeNormalized(directionMap * v, v);

		Vector3 radial = p - layer.LinkedCenter;
		if (radial.LengthSquared() < 1e-6f)
			radial = -layer.LinkedWorldFromLocal.Basis.Z;

		p = layer.LinkedCenter + radial.Normalized() * (layer.LinkedRadius + layer.SceneTransformExitOffset);
	}

	private BoundaryInteractionLedgerDelta ApplyBoundaryLayerInteractions(ref Vector3 p, ref Vector3 v, BoundaryLayerSnap[] layers, ref uint insideMask)
	{
		BoundaryInteractionLedgerDelta ledgerDelta = default;
		ledgerDelta.LastCrossingLayer = -1;
		if (layers == null || layers.Length == 0)
		{
			insideMask = 0u;
			return ledgerDelta;
		}

		uint newMask = ComputeInsideMask(p, layers);
		uint crossings = newMask ^ insideMask;
		int sceneTransformEvents = 0;
		v = ApplyBoundaryLayerBias(p, v, layers);
		if (crossings != 0u)
		{
			ApplyBoundaryLayerCrossings(ref p, ref v, layers, crossings & newMask, crossings & insideMask, ref ledgerDelta);
			insideMask = ComputeInsideMask(p, layers);
			return ledgerDelta;
		}

		insideMask = newMask;
		return ledgerDelta;
	}

	public static TransportModel ResolveActiveTransportModel(FieldSourceSnap[] sources)
	{
		if (sources == null || sources.Length == 0)
		{
			return TransportModel.GRIN_Optical;
		}

		for (int i = 0; i < sources.Length; i++)
		{
			if (!sources[i].Enabled)
			{
				continue;
			}

			return sources[i].TransportModel;
		}

		return TransportModel.GRIN_Optical;
	}

	private void MaybeLogActiveTransport(FieldSourceSnap[] sources)
	{
		TransportModel active = ResolveActiveTransportModel(sources);
		if (_hasLoggedTransportModel && _lastLoggedTransportModel == active)
		{
			return;
		}

		_lastLoggedTransportModel = active;
		_hasLoggedTransportModel = true;
		GD.Print($"[Transport] active={active}");
	}

	private static bool TryGetAbsorbingSourceAtPoint(
		Vector3 p,
		FieldSourceSnap[] sources,
		out int sourceIndex)
	{
		sourceIndex = -1;
		if (sources == null || sources.Length == 0)
		{
			return false;
		}

		for (int i = 0; i < sources.Length; i++)
		{
			ref readonly FieldSourceSnap source = ref sources[i];
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

			if (p.DistanceSquaredTo(source.Center) < (inner * inner))
			{
				sourceIndex = i;
				return true;
			}
		}

		return false;
	}

	// Returns true when world-space point p is inside the oriented box defined by
	// (center, halfExtents in local space, invBasis = inverse of node world orientation).
	// Used to enforce BoxVolume support regions before evaluating field profiles.
	private static bool ContainsPointInBox(Vector3 p, Vector3 center, Vector3 halfExtents, Basis invBasis)
	{
		Vector3 local = invBasis * (p - center);
		return Mathf.Abs(local.X) <= halfExtents.X
			&& Mathf.Abs(local.Y) <= halfExtents.Y
			&& Mathf.Abs(local.Z) <= halfExtents.Z;
	}

	// FieldMath remains the canonical field baseline evaluator.
	// Transport interpretation is selected in integrator step helpers.
	public static Vector3 ComputeAccelerationAtPointSnap(
		Vector3 p,
		FieldSourceSnap[] sources,
		float globalBeta, float globalGamma,
		float bendScale, float fieldStrength)
	{
		_ = globalGamma;
		Vector3 aSum = Vector3.Zero;

		// DECISION: accumulate acceleration from each snapped field source.
		for (int i = 0; i < sources.Length; i++)
		{
			ref readonly var fs = ref sources[i];
			// DECISION: skip disabled field sources.
			if (!fs.Enabled) continue;

			// Support region enforcement: BoxVolume fields only contribute inside their box.
			// SphereRadial outer-radius enforcement is handled inside EvalFieldAccel.
			if (fs.ShapeType == FieldShapeType.BoxVolume && !ContainsPointInBox(p, fs.Center, fs.HalfExtents, fs.BoxInvBasis))
				continue;

			FieldMath.EvalResult eval = FieldMath.EvalFieldAccel(
				p,
				fs.Center,
				fs.CurveType,
				fs.RInner,
				fs.ROuter,
				fs.Amp,
				fs.CurveA,
				fs.Sigma,
				fs.CurveA,
				fs.CurveB,
				fs.CurveC,
				fs.CustomCurve,
				fs.ModeFlags,
				globalBeta,
				fs.OverrideBetaScale,
				fs.BetaScale,
				fs.EdgeSoftness);

			aSum += eval.Acceleration * bendScale * fieldStrength;
		}

		return aSum;
	}

	private readonly struct MetricTransportStepContext
	{
		public readonly Vector3 Position;
		public readonly Vector3 SourceCenter;
		public readonly float RadialDistance;
		public readonly float CharacteristicRadius;
		public readonly Vector3 Direction;
		public readonly Vector3 PreferredBendDir;
		public readonly float StepSize;
		public readonly float WeakFieldScalar;

		public MetricTransportStepContext(
			Vector3 position,
			Vector3 sourceCenter,
			float radialDistance,
			float characteristicRadius,
			Vector3 direction,
			Vector3 preferredBendDir,
			float stepSize,
			float weakFieldScalar)
		{
			Position = position;
			SourceCenter = sourceCenter;
			RadialDistance = radialDistance;
			CharacteristicRadius = characteristicRadius;
			Direction = direction;
			PreferredBendDir = preferredBendDir;
			StepSize = stepSize;
			WeakFieldScalar = weakFieldScalar;
		}
	}

	private enum MetricPerpendicularRecoveryBasis
	{
		None = 0,
		Preferred = 1,
		Axis = 2
	}

	private enum MetricDeltaZeroReason
	{
		None = 0,
		StepGuard = 1,
		NonFiniteOrNormalizationGuard = 2,
		SourceCoincident = 3,
		RadialParallel = 4,
		BendPerpBelowEpsilon = 5,
		ZeroTurn = 6
	}

	private readonly struct MetricDirectionDeltaEvaluation
	{
		public readonly Vector3 Delta;
		public readonly MetricDeltaZeroReason ZeroReason;
		public readonly float Radius;
		public readonly float BendPerpMagnitude;
		public readonly float DTheta;
		public readonly bool EncounteredRawParallel;
		public readonly bool UsedParallelRecovery;
		public readonly bool UsedPreferredBendRecovery;
		public readonly MetricPerpendicularRecoveryBasis RecoveryBasis;
		public readonly bool RadiusBelowDiagnosticThreshold;
		public readonly bool RadiusAboveDiagnosticThreshold;

		public MetricDirectionDeltaEvaluation(
			Vector3 delta,
			MetricDeltaZeroReason zeroReason,
			float radius,
			float bendPerpMagnitude,
			float dTheta,
			bool encounteredRawParallel,
			bool usedParallelRecovery,
			bool usedPreferredBendRecovery,
			MetricPerpendicularRecoveryBasis recoveryBasis,
			bool radiusBelowDiagnosticThreshold,
			bool radiusAboveDiagnosticThreshold)
		{
			Delta = delta;
			ZeroReason = zeroReason;
			Radius = radius;
			BendPerpMagnitude = bendPerpMagnitude;
			DTheta = dTheta;
			EncounteredRawParallel = encounteredRawParallel;
			UsedParallelRecovery = usedParallelRecovery;
			UsedPreferredBendRecovery = usedPreferredBendRecovery;
			RecoveryBasis = recoveryBasis;
			RadiusBelowDiagnosticThreshold = radiusBelowDiagnosticThreshold;
			RadiusAboveDiagnosticThreshold = radiusAboveDiagnosticThreshold;
		}

		public bool IsZero => Delta.LengthSquared() <= 0f;
	}

	private readonly struct MetricWeakFieldScalarInputs
	{
		public readonly bool HasEnabledSource;
		public readonly int SourceIndex;
		public readonly float Amp;
		public readonly float BetaScaleEff;
		public readonly float BendScaleEff;
		public readonly float FieldStrengthEff;
		public readonly float Scalar;
		public readonly float ROuter;
		public readonly FieldCurveType CurveType;
		public readonly bool OverrideBetaScale;

		public MetricWeakFieldScalarInputs(
			bool hasEnabledSource,
			int sourceIndex,
			float amp,
			float betaScaleEff,
			float bendScaleEff,
			float fieldStrengthEff,
			float scalar,
			float rOuter,
			FieldCurveType curveType,
			bool overrideBetaScale)
		{
			HasEnabledSource = hasEnabledSource;
			SourceIndex = sourceIndex;
			Amp = amp;
			BetaScaleEff = betaScaleEff;
			BendScaleEff = bendScaleEff;
			FieldStrengthEff = fieldStrengthEff;
			Scalar = scalar;
			ROuter = rOuter;
			CurveType = curveType;
			OverrideBetaScale = overrideBetaScale;
		}
	}

	// Metric_NullGeodesic is still a weak-field scaffold.
	// Direct source-side mapping is intentionally narrow:
	// - |Amp| and resolved betaScaleEff from the first enabled source feed the metric scalar proxy.
	// - first enabled source ROuter feeds the metric envelope radius in StepTransport_MetricStub.
	// Curve/profile controls (CurveType/Gamma/A/B/C/Sigma/RInner/CustomCurve/EdgeSoftness) do not
	// directly parameterize the metric turn law. They only leak in indirectly because the metric stub
	// also computes GRIN acceleration, uses |grinAccel| as a floor on weakFieldScalar, and falls back
	// to GRIN when the metric direction delta collapses to zero/non-finite.
	private static MetricWeakFieldScalarInputs ResolveMetricWeakFieldScalarInputs(
		FieldSourceSnap[] sources,
		float globalBeta,
		float bendScale,
		float fieldStrength)
	{
		float betaEff = Mathf.Abs(globalBeta);
		float amp = 0f;
		float rOuter = 0f;
		FieldCurveType curveType = FieldCurveType.Linear;
		bool overrideBetaScale = false;
		int sourceIndex = -1;
		if (sources != null)
		{
			for (int i = 0; i < sources.Length; i++)
			{
				ref readonly FieldSourceSnap source = ref sources[i];
				if (!source.Enabled)
				{
					continue;
				}

				sourceIndex = i;
				amp = Mathf.Abs(source.Amp);
				betaEff = Mathf.Abs(source.OverrideBetaScale ? source.BetaScale : globalBeta);
				rOuter = Mathf.Max(0f, source.ROuter);
				curveType = source.CurveType;
				overrideBetaScale = source.OverrideBetaScale;
				break;
			}
		}

		// Fallback preserves non-zero coupling when no sources are active.
		if (sourceIndex < 0)
		{
			amp = 1f;
		}

		float bendScaleEff = Mathf.Abs(bendScale);
		float fieldStrengthEff = Mathf.Abs(fieldStrength);
		float scalar = Mathf.Max(0f, amp * betaEff * bendScaleEff * fieldStrengthEff);
		return new MetricWeakFieldScalarInputs(
			sourceIndex >= 0,
			sourceIndex,
			amp,
			betaEff,
			bendScaleEff,
			fieldStrengthEff,
			scalar,
			rOuter,
			curveType,
			overrideBetaScale);
	}

	// Research scalar mapping for Metric_NullGeodesic scaffold:
	// weakField ~= |Amp| * betaScaleEff * BendScale * FieldStrength.
	// This is still GRIN-scaffolded source tuning, not a metric-only scalar path.
	// This keeps metric tuning tied to existing fixture/source parameters without modifying FieldMath.
	public static float ComputeMetricWeakFieldScalarProxy(
		FieldSourceSnap[] sources,
		float globalBeta,
		float bendScale,
		float fieldStrength)
	{
		return ResolveMetricWeakFieldScalarInputs(sources, globalBeta, bendScale, fieldStrength).Scalar;
	}

	public static float ComputeMetricWeakFieldScalarForActiveModel(
		FieldSourceSnap[] sources,
		float globalBeta,
		float bendScale,
		float fieldStrength,
		TransportModel activeTransportModel)
	{
		float scalar = ComputeMetricWeakFieldScalarProxy(sources, globalBeta, bendScale, fieldStrength);
		if (activeTransportModel != TransportModel.Metric_NullGeodesic)
		{
			return scalar;
		}

		return scalar * ResolveMetricComparisonScalarOverride();
	}

	public MetricSteeringLaw GetEffectiveMetricSteeringLaw()
	{
		return ResolveMetricSteeringLawOverride(MetricSteeringLawMode);
	}

	public static string GetMetricSteeringLawToken(MetricSteeringLaw law)
	{
		return law switch
		{
			MetricSteeringLaw.MetricLaw_ImpactParameterApprox => "MetricLaw_ImpactParameterApprox",
			_ => "MetricLaw_CurrentEnvelope"
		};
	}

	public static MetricSteeringLaw ResolveMetricSteeringLawOverride(
		MetricSteeringLaw fallbackLaw = MetricSteeringLaw.MetricLaw_CurrentEnvelope)
	{
		if (_metricSteeringLawOverrideResolved)
		{
			return _metricSteeringLawOverride;
		}

		_metricSteeringLawOverrideResolved = true;
		_metricSteeringLawOverride = fallbackLaw;
		if (!HasRenderTestFlagForMetricComparisonOverride())
		{
			return _metricSteeringLawOverride;
		}

		string[] args = GetCmdArgsForMetricComparisonOverride();
		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (TryParseMetricSteeringLawArg(arg, "--metric-steering-law=", out MetricSteeringLaw parsedLaw) ||
				TryParseMetricSteeringLawArg(arg, "--metric-law=", out parsedLaw))
			{
				_metricSteeringLawOverride = parsedLaw;
				break;
			}
		}

		return _metricSteeringLawOverride;
	}

	public static float ResolveMetricComparisonScalarOverride()
	{
		if (_metricComparisonScalarOverrideResolved)
		{
			return _metricComparisonScalarOverride;
		}

		_metricComparisonScalarOverrideResolved = true;
		_metricComparisonScalarOverride = 1.0f;
		if (!HasRenderTestFlagForMetricComparisonOverride())
		{
			return _metricComparisonScalarOverride;
		}

		string[] args = GetCmdArgsForMetricComparisonOverride();
		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (TryParseMetricComparisonOverrideArg(arg, "--metric-scalar-multiplier=", out float scalarOverride) ||
				TryParseMetricComparisonOverrideArg(arg, "--metric-gain=", out scalarOverride))
			{
				_metricComparisonScalarOverride = scalarOverride;
				break;
			}
		}

		return _metricComparisonScalarOverride;
	}

	private static string GetMetricDeltaZeroReasonToken(MetricDeltaZeroReason reason)
	{
		return reason switch
		{
			MetricDeltaZeroReason.StepGuard => "step",
			MetricDeltaZeroReason.NonFiniteOrNormalizationGuard => "nonfinite",
			MetricDeltaZeroReason.SourceCoincident => "coincident",
			MetricDeltaZeroReason.RadialParallel => "parallel_fallback",
			MetricDeltaZeroReason.BendPerpBelowEpsilon => "perp_eps",
			MetricDeltaZeroReason.ZeroTurn => "dtheta0",
			_ => "none"
		};
	}

	private static string GetMetricDiagnosticReasonToken(in MetricDirectionDeltaEvaluation evaluation)
	{
		if (evaluation.EncounteredRawParallel)
		{
			if (evaluation.UsedPreferredBendRecovery)
			{
				return "parallel_preferred";
			}
			if (evaluation.UsedParallelRecovery)
			{
				return "parallel_recovered";
			}
			if (evaluation.ZeroReason == MetricDeltaZeroReason.RadialParallel)
			{
				return "parallel_fallback";
			}
			return "parallel_raw";
		}

		return evaluation.IsZero ? GetMetricDeltaZeroReasonToken(evaluation.ZeroReason) : "none";
	}

	private static bool ShouldLogMetricDiagnosticSamples()
	{
		return HasRenderTestFlagForMetricComparisonOverride();
	}

	private void RecordMetricDirectionDeltaEvaluation(in MetricDirectionDeltaEvaluation evaluation)
	{
		_metricDiagnosticEvalCount++;
		if (evaluation.EncounteredRawParallel)
		{
			if (evaluation.UsedPreferredBendRecovery)
			{
				_metricParallelPreferredCount++;
			}
			else if (evaluation.UsedParallelRecovery)
			{
				_metricParallelRecoveredCount++;
			}
			else
			{
				_metricParallelRawCount++;
			}
		}
		if (evaluation.IsZero)
		{
			_metricDeltaZeroCount++;
			switch (evaluation.ZeroReason)
			{
				case MetricDeltaZeroReason.StepGuard:
					_metricZeroReasonStepGuardCount++;
					break;
				case MetricDeltaZeroReason.NonFiniteOrNormalizationGuard:
					_metricZeroReasonNonFiniteCount++;
					break;
				case MetricDeltaZeroReason.SourceCoincident:
					_metricZeroReasonCoincidentCount++;
					break;
				case MetricDeltaZeroReason.RadialParallel:
					_metricParallelFallbackCount++;
					_metricZeroReasonParallelCount++;
					break;
				case MetricDeltaZeroReason.BendPerpBelowEpsilon:
					_metricZeroReasonPerpEpsilonCount++;
					break;
				case MetricDeltaZeroReason.ZeroTurn:
					_metricZeroReasonZeroTurnCount++;
					break;
			}

			if (evaluation.RadiusBelowDiagnosticThreshold)
			{
				_metricZeroReasonRadiusLowCount++;
			}
			if (evaluation.RadiusAboveDiagnosticThreshold)
			{
				_metricZeroReasonRadiusHighCount++;
			}
			return;
		}

		_metricDeltaNonzeroCount++;
	}

	private void MaybeLogMetricDiagnosticSample(in MetricTransportStepContext context, in MetricDirectionDeltaEvaluation evaluation)
	{
		if (!ShouldLogMetricDiagnosticSamples())
		{
			return;
		}

		bool isParallelSample = evaluation.EncounteredRawParallel;
		if (isParallelSample)
		{
			if (_metricParallelDiagnosticSampleLogsEmitted >= 6)
			{
				return;
			}

			_metricParallelDiagnosticSampleLogsEmitted++;
		}
		else
		{
			if (_metricDiagnosticSampleLogsEmitted >= 6)
			{
				return;
			}

			_metricDiagnosticSampleLogsEmitted++;
		}

		string status = evaluation.IsZero ? "zero" : "nonzero";
		string reason = GetMetricDiagnosticReasonToken(in evaluation);
		string radiusFlag = evaluation.RadiusBelowDiagnosticThreshold
			? "r_lo"
			: (evaluation.RadiusAboveDiagnosticThreshold ? "r_hi" : "r_ok");
		Vector3 rayDir = SafeNormalized(context.Direction, Vector3.Forward);
		Vector3 radialDir = SafeNormalized(context.SourceCenter - context.Position, Vector3.Zero);
		float radialDot = radialDir == Vector3.Zero
			? 0f
			: Mathf.Clamp(rayDir.Dot(radialDir), -1f, 1f);
		string zeroReason = evaluation.IsZero ? GetMetricDeltaZeroReasonToken(evaluation.ZeroReason) : "none";
		string recoveryBasis = evaluation.RecoveryBasis switch
		{
			MetricPerpendicularRecoveryBasis.Preferred => "preferred",
			MetricPerpendicularRecoveryBasis.Axis => "axis",
			_ => "raw"
		};
		GD.Print(
			$"[Transport][MetricDiagSample] eval={_metricDiagnosticEvalCount} status={status} reason={reason} radiusFlag={radiusFlag} " +
			$"zeroReason={zeroReason} recoveryBasis={recoveryBasis} " +
			$"rayDir=({rayDir.X:0.######},{rayDir.Y:0.######},{rayDir.Z:0.######}) " +
			$"radialDir=({radialDir.X:0.######},{radialDir.Y:0.######},{radialDir.Z:0.######}) " +
			$"dot={radialDot:0.######} r={evaluation.Radius:0.######} bendPerp={evaluation.BendPerpMagnitude:0.######} dTheta={evaluation.DTheta:0.######}");
	}

	private void RecordTransportSteeringStep(
		Vector3 p,
		Vector3 vBefore,
		Vector3 vAfter,
		Vector3 center,
		FieldSourceSnap[] fieldSources,
		bool hasSources)
	{
		Vector3 dirBefore = SafeNormalized(vBefore, Vector3.Forward);
		Vector3 dirAfter = SafeNormalized(vAfter, dirBefore);
		if (!IsFinite(dirBefore) || !IsFinite(dirAfter))
		{
			return;
		}

		float dot = Mathf.Clamp(dirBefore.Dot(dirAfter), -1f, 1f);
		float crossLen = dirBefore.Cross(dirAfter).Length();
		float turn = Mathf.Atan2(crossLen, dot);
		if (!float.IsFinite(turn))
		{
			return;
		}

		ResolveTransportSteeringRadialFrame(center, fieldSources, hasSources, out Vector3 sourceCenter, out float radialScale);
		float radiusRatio = p.DistanceTo(sourceCenter) / radialScale;
		int bucketIndex = GetTransportSteeringBucketIndex(radiusRatio);

		_transportSteeringTurnCount++;
		_transportSteeringTurnSum += turn;
		_transportSteeringMaxTurn = Mathf.Max(_transportSteeringMaxTurn, turn);
		_transportSteeringRadialTurnSums[bucketIndex] += turn;
		_transportSteeringRadialCounts[bucketIndex]++;
	}

	private static void ResolveTransportSteeringRadialFrame(
		Vector3 fallbackCenter,
		FieldSourceSnap[] fieldSources,
		bool hasSources,
		out Vector3 sourceCenter,
		out float radialScale)
	{
		if (hasSources && fieldSources != null && fieldSources.Length > 0)
		{
			sourceCenter = fieldSources[0].Center;
			radialScale = Mathf.Max(0.001f, fieldSources[0].ROuter);
			return;
		}

		sourceCenter = fallbackCenter;
		radialScale = 1f;
	}

	private static int GetTransportSteeringBucketIndex(float radiusRatio)
	{
		if (!float.IsFinite(radiusRatio) || radiusRatio < 0f)
		{
			return TransportSteeringBucketCount - 1;
		}
		if (radiusRatio < 0.5f) return 0;
		if (radiusRatio < 1f) return 1;
		if (radiusRatio < 2f) return 2;
		if (radiusRatio < 4f) return 3;
		if (radiusRatio < 8f) return 4;
		return 5;
	}

	private static string GetTransportSteeringBucketLabel(int bucketIndex)
	{
		return bucketIndex switch
		{
			0 => "r<0.5R",
			1 => "0.5R-1R",
			2 => "1R-2R",
			3 => "2R-4R",
			4 => "4R-8R",
			_ => "r>=8R"
		};
	}

	private static bool TryParseMetricComparisonOverrideArg(string arg, string prefix, out float scalarOverride)
	{
		scalarOverride = 1.0f;
		if (string.IsNullOrWhiteSpace(arg) ||
			!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string token = arg.Substring(prefix.Length).Trim();
		if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
		{
			return false;
		}

		if (!float.IsFinite(parsed) || parsed <= 0f)
		{
			return false;
		}

		scalarOverride = parsed;
		return true;
	}

	private static bool TryParseMetricSteeringLawArg(string arg, string prefix, out MetricSteeringLaw law)
	{
		law = MetricSteeringLaw.MetricLaw_CurrentEnvelope;
		if (string.IsNullOrWhiteSpace(arg) ||
			!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string token = arg.Substring(prefix.Length).Trim();
		if (string.IsNullOrWhiteSpace(token))
		{
			return false;
		}

		string normalized = token.Replace("-", string.Empty)
			.Replace("_", string.Empty)
			.Trim()
			.ToLowerInvariant();
		switch (normalized)
		{
			case "current":
			case "currentenvelope":
			case "metriclawcurrentenvelope":
			case "baseline":
				law = MetricSteeringLaw.MetricLaw_CurrentEnvelope;
				return true;
			case "impact":
			case "impactparameter":
			case "impactparameterapprox":
			case "metriclawimpactparameterapprox":
				law = MetricSteeringLaw.MetricLaw_ImpactParameterApprox;
				return true;
		}

		return Enum.TryParse(token, true, out law);
	}

	private static bool TryParseBoolOverrideArg(string arg, string prefix, out bool value)
	{
		value = false;
		if (string.IsNullOrWhiteSpace(arg) ||
			!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string token = arg.Substring(prefix.Length).Trim();
		if (string.IsNullOrWhiteSpace(token))
		{
			return false;
		}

		switch (token.ToLowerInvariant())
		{
			case "1":
			case "true":
			case "on":
			case "yes":
				value = true;
				return true;
			case "0":
			case "false":
			case "off":
			case "no":
				value = false;
				return true;
			default:
				return false;
		}
	}

	private bool IsDerivativeAwareSteppingEnabled()
	{
		if (_derivativeAwareStepOverrideResolved)
		{
			return _derivativeAwareStepOverride ?? UseDerivativeAwareStepping;
		}

		_derivativeAwareStepOverrideResolved = true;
		_derivativeAwareStepOverride = null;
		string[] args = GetCmdArgsForMetricComparisonOverride();
		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (TryParseBoolOverrideArg(arg, "--derivative-aware-step=", out bool enabled) ||
				TryParseBoolOverrideArg(arg, "--exp1-derivative-step=", out enabled))
			{
				_derivativeAwareStepOverride = enabled;
			}
		}

		return _derivativeAwareStepOverride ?? UseDerivativeAwareStepping;
	}

	private int ResolveDerivativeAwareMinimumActiveAcceptedSteps()
	{
		if (_derivativeAwareMinimumActiveAcceptedStepsOverrideResolved)
		{
			return _derivativeAwareMinimumActiveAcceptedStepsOverride ?? DerivativeAwareMinimumActiveAcceptedSteps;
		}

		_derivativeAwareMinimumActiveAcceptedStepsOverrideResolved = true;
		_derivativeAwareMinimumActiveAcceptedStepsOverride = null;
		string[] args = GetCmdArgsForMetricComparisonOverride();
		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (TryParsePositiveIntOverrideArg(arg, "--derivative-aware-min-active-steps=", out int steps) ||
				TryParsePositiveIntOverrideArg(arg, "--exp1-derivative-min-active-steps=", out steps))
			{
				_derivativeAwareMinimumActiveAcceptedStepsOverride = steps;
			}
		}

		return _derivativeAwareMinimumActiveAcceptedStepsOverride ?? DerivativeAwareMinimumActiveAcceptedSteps;
	}

	private static bool HasRenderTestFlagForMetricComparisonOverride()
	{
		string[] args = GetCmdArgsForMetricComparisonOverride();
		for (int i = 0; i < args.Length; i++)
		{
			string token = (args[i] ?? string.Empty).Trim();
			if (string.Equals(token, "--render-test", StringComparison.OrdinalIgnoreCase) ||
				token.StartsWith("--render-test-", StringComparison.OrdinalIgnoreCase) ||
				token.StartsWith("--render-test-fixture=", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private static string[] GetCmdArgsForMetricComparisonOverride()
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

		HashSet<string> merged = new(StringComparer.Ordinal);
		List<string> ordered = new(userArgs.Length + args.Length);
		for (int i = 0; i < userArgs.Length; i++)
		{
			string raw = userArgs[i];
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
		for (int i = 0; i < args.Length; i++)
		{
			string raw = args[i];
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

	private static bool TryParsePositiveIntOverrideArg(string arg, string prefix, out int value)
	{
		value = 0;
		if (string.IsNullOrWhiteSpace(arg) ||
			!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string token = arg.Substring(prefix.Length).Trim();
		if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
		{
			return false;
		}

		if (parsed <= 0)
		{
			return false;
		}

		value = parsed;
		return true;
	}

	private int GetMetricAdaptiveStepMultiplier()
	{
		int subdivisions = Mathf.Clamp(MetricAdaptiveMaxSubdivisions, 0, 5);
		return 1 << subdivisions;
	}

	private float GetMetricAdaptiveTurnThresholdRadians()
	{
		return Mathf.DegToRad(Mathf.Clamp(MetricAdaptiveTurnThresholdDegrees, 0.1f, 45.0f));
	}

	private static float ComputeDirectionTurnAngle(Vector3 from, Vector3 to)
	{
		Vector3 dirBefore = SafeNormalized(from, Vector3.Forward);
		Vector3 dirAfter = SafeNormalized(to, dirBefore);
		if (!IsFinite(dirBefore) || !IsFinite(dirAfter))
		{
			return 0f;
		}

		float dot = Mathf.Clamp(dirBefore.Dot(dirAfter), -1f, 1f);
		float crossLen = dirBefore.Cross(dirAfter).Length();
		float turn = Mathf.Atan2(crossLen, dot);
		return float.IsFinite(turn) ? turn : 0f;
	}

	private static float EstimateMetricStepError(
		Vector3 coarsePos,
		Vector3 refinedPos,
		Vector3 coarseDir,
		Vector3 refinedDir,
		float step)
	{
		float positionalError = (refinedPos - coarsePos).Length();
		float directionalError = step * ComputeDirectionTurnAngle(coarseDir, refinedDir);
		float estimate = positionalError + directionalError;
		return float.IsFinite(estimate) && estimate > 0f ? estimate : 0f;
	}

	private float ComputeAdaptiveIntegratedStepLength(
		Vector3 v,
		Vector3 acceleration,
		float minStep,
		float maxStep,
		bool applyLowCurvatureBoost,
		bool applyMetricCurvatureGain,
		ref DerivativeAwareStepState derivativeState)
	{
		float aLen = acceleration.Length();
		float step = StepLength / (1.0f + aLen * StepAdaptGain);
		step = Mathf.Clamp(step, minStep, maxStep);
		if (applyLowCurvatureBoost && LowCurvatureStepBoost > 1.0f)
		{
			Vector3 aPerp = acceleration - v * acceleration.Dot(v);
			float aPerpLen = aPerp.Length();
			if (aPerpLen < LowCurvaturePerpAccel)
			{
				step = Mathf.Min(step * LowCurvatureStepBoost, maxStep);
			}
		}

		if (applyMetricCurvatureGain)
		{
			float metricCurvatureGain = Mathf.Max(0f, MetricAdaptiveCurvatureGain);
			if (metricCurvatureGain > 0f)
			{
				float aPerpLen = PerpAccelLen(acceleration, v);
				step /= 1.0f + (aPerpLen * StepAdaptGain * metricCurvatureGain);
				step = Mathf.Clamp(step, minStep, maxStep);
			}
		}

		return ApplyDerivativeAwareStepModifier(step, minStep, maxStep, acceleration, v, ref derivativeState);
	}

	private float ApplyDerivativeAwareStepModifier(
		float baselineStep,
		float minStep,
		float maxStep,
		Vector3 acceleration,
		Vector3 v,
		ref DerivativeAwareStepState state)
	{
		if (!IsDerivativeAwareSteppingEnabled())
		{
			return baselineStep;
		}

		float alpha = Mathf.Clamp(DerivativeAwareSmoothingAlpha, 0.01f, 1.0f);
		float maxScaleUp = Mathf.Max(1.0f, DerivativeAwareMaxStepScaleUp);
		float maxScaleDown = Mathf.Clamp(DerivativeAwareMaxStepScaleDown, 0.01f, 1.0f);
		float strength = Mathf.Clamp(DerivativeAwareStepScaleStrength, 0f, 1.0f);
		int historyLength = Mathf.Max(2, DerivativeAwareHistoryLength);
		float kRaw = PerpAccelLen(acceleration, v);
		if (!float.IsFinite(kRaw) || kRaw < 0f)
		{
			kRaw = 0f;
		}

		float prevSmoothedK = state.SmoothedK;
		float prevDk = state.PrevDk;
		float smoothedK = state.HasSample
			? (prevSmoothedK + ((kRaw - prevSmoothedK) * alpha))
			: kRaw;
		float dk = state.HasSample ? (smoothedK - prevSmoothedK) : 0f;
		float d2k = (state.HasSample && DerivativeAwareUseSecondDerivative) ? (dk - prevDk) : 0f;
		float absDk = Mathf.Abs(dk);
		float absD2k = Mathf.Abs(d2k);
		float difficultyRaw = smoothedK + (DerivativeAwareDkWeight * absDk);
		if (DerivativeAwareUseSecondDerivative)
		{
			difficultyRaw += DerivativeAwareD2kWeight * absD2k;
		}

		float smoothedDifficulty = state.HasSample
			? (state.SmoothedDifficulty + ((difficultyRaw - state.SmoothedDifficulty) * alpha))
			: difficultyRaw;
		float signedPredictiveTrend = (DerivativeAwareDkWeight * dk)
			+ (DerivativeAwareUseSecondDerivative ? (DerivativeAwareD2kWeight * d2k) : 0f);
		float normalizedTrend = signedPredictiveTrend / Mathf.Max(1e-4f, 1.0f + smoothedDifficulty);
		float rawScale = 1.0f - (strength * normalizedTrend);
		float clampedScale = Mathf.Clamp(rawScale, maxScaleDown, maxScaleUp);
		float warmup = Mathf.Clamp((state.SampleCount + 1) / (float)Mathf.Max(1, historyLength - 1), 0f, 1f);
		float candidateScale = Mathf.Lerp(1.0f, clampedScale, warmup);
		float difficultyThreshold = Mathf.Max(0f, DerivativeAwareDifficultyThreshold);
		float deadband = Mathf.Max(0f, DerivativeAwareStepDeadband);
		bool passesDifficultyGate = smoothedDifficulty >= difficultyThreshold;
		float candidateScaleDelta = candidateScale - 1.0f;
		bool candidateAdjusted = passesDifficultyGate && !Mathf.IsZeroApprox(candidateScaleDelta);
		float scaleDeltaAbs = Mathf.Abs(candidateScaleDelta);
		bool hysteresisEngaged = state.HysteresisEngaged;
		bool hysteresisEntered = false;
		bool hysteresisExited = false;
		int activeSpanCount = state.ActiveAcceptedStepCount;
		int minimumActiveAcceptedSteps = Mathf.Max(1, ResolveDerivativeAwareMinimumActiveAcceptedSteps());
		if (DerivativeAwareUseHysteresis)
		{
			float engageDelta = Mathf.Max(0f, DerivativeAwareHysteresisEngageDelta);
			float releaseDelta = Mathf.Clamp(DerivativeAwareHysteresisReleaseDelta, 0f, engageDelta);
			if (!passesDifficultyGate)
			{
				bool holdActive = DerivativeAwareUseMinimumActiveSpan && hysteresisEngaged && activeSpanCount < minimumActiveAcceptedSteps;
				if (hysteresisEngaged && !holdActive)
				{
					hysteresisExited = true;
				}
				hysteresisEngaged = holdActive;
			}
			else if (!hysteresisEngaged)
			{
				if (scaleDeltaAbs >= engageDelta)
				{
					hysteresisEngaged = true;
					hysteresisEntered = true;
				}
			}
			else if (scaleDeltaAbs < releaseDelta)
			{
				bool holdActive = DerivativeAwareUseMinimumActiveSpan && activeSpanCount < minimumActiveAcceptedSteps;
				if (!holdActive)
				{
					hysteresisEngaged = false;
					hysteresisExited = true;
				}
			}
		}
		else
		{
			hysteresisEngaged = passesDifficultyGate && (scaleDeltaAbs >= deadband);
		}

		if (hysteresisEngaged)
		{
			activeSpanCount = hysteresisEntered ? 1 : (activeSpanCount + 1);
		}

		if (hysteresisExited)
		{
			RecordDerivativeAwareActiveSpan(activeSpanCount);
			activeSpanCount = 0;
		}

		float finalScale = candidateScale;
		if (!passesDifficultyGate || !hysteresisEngaged || scaleDeltaAbs < deadband)
		{
			finalScale = 1.0f;
		}
		float appliedScaleDelta = finalScale - 1.0f;
		bool appliedAdjusted = !Mathf.IsZeroApprox(appliedScaleDelta);
		float adjustedStep = Mathf.Clamp(baselineStep * finalScale, minStep, maxStep);

		RecordDerivativeAwareStepSample(
			smoothedK,
			absDk,
			absD2k,
			smoothedDifficulty,
			baselineStep,
			adjustedStep,
			candidateScale,
			finalScale,
			candidateAdjusted,
			appliedAdjusted,
			hysteresisEngaged,
			hysteresisEntered,
			hysteresisExited);

		state.HasSample = true;
		state.SampleCount++;
		state.HysteresisEngaged = hysteresisEngaged;
		state.ActiveAcceptedStepCount = activeSpanCount;
		state.SmoothedK = smoothedK;
		state.PrevDk = dk;
		state.SmoothedDifficulty = smoothedDifficulty;
		return adjustedStep;
	}

	private void RecordDerivativeAwareStepSample(
		float k,
		float absDk,
		float absD2k,
		float difficulty,
		float stepBefore,
		float stepAfter,
		float candidateScale,
		float finalScale,
		bool candidateAdjusted,
		bool appliedAdjusted,
		bool hysteresisEngaged,
		bool hysteresisEntered,
		bool hysteresisExited)
	{
		_derivativeAwareSampleCount++;
		_derivativeAwareKSum += k;
		_derivativeAwareKMin = Mathf.Min(_derivativeAwareKMin, k);
		_derivativeAwareKMax = Mathf.Max(_derivativeAwareKMax, k);
		_derivativeAwareAbsDkSum += absDk;
		_derivativeAwareAbsDkMin = Mathf.Min(_derivativeAwareAbsDkMin, absDk);
		_derivativeAwareAbsDkMax = Mathf.Max(_derivativeAwareAbsDkMax, absDk);
		_derivativeAwareAbsD2kSum += absD2k;
		_derivativeAwareAbsD2kMin = Mathf.Min(_derivativeAwareAbsD2kMin, absD2k);
		_derivativeAwareAbsD2kMax = Mathf.Max(_derivativeAwareAbsD2kMax, absD2k);
		_derivativeAwareDifficultySum += difficulty;
		_derivativeAwareDifficultyMin = Mathf.Min(_derivativeAwareDifficultyMin, difficulty);
		_derivativeAwareDifficultyMax = Mathf.Max(_derivativeAwareDifficultyMax, difficulty);
		_derivativeAwareStepBeforeSum += stepBefore;
		_derivativeAwareStepAfterSum += stepAfter;
		if (candidateAdjusted)
		{
			_derivativeAwareCandidateAdjustmentCount++;
			if (candidateScale > 1.0001f)
			{
				_derivativeAwareCandidateScaleUpCount++;
			}
			else if (candidateScale < 0.9999f)
			{
				_derivativeAwareCandidateScaleDownCount++;
			}
		}
		if (hysteresisEngaged)
		{
			_derivativeAwareHysteresisEngagedSampleCount++;
		}
		if (hysteresisEntered)
		{
			_derivativeAwareHysteresisEnterCount++;
		}
		if (hysteresisExited)
		{
			_derivativeAwareHysteresisExitCount++;
		}
		if (appliedAdjusted)
		{
			_derivativeAwareEngagedStepCount++;
			if (Mathf.Abs(finalScale - 1.0f) >= Mathf.Max(0f, DerivativeAwareMinLoggedScaleDelta))
			{
				_derivativeAwareLoggedAppliedAdjustmentCount++;
			}
		}
		if (appliedAdjusted && finalScale > 1.0001f)
		{
			_derivativeAwareScaleUpCount++;
		}
		else if (appliedAdjusted && finalScale < 0.9999f)
		{
			_derivativeAwareScaleDownCount++;
		}
	}

	private void RecordPass1TelemetryDerivativeSample(
		Vector3 acceleration,
		Vector3 v,
		ref Pass1TelemetryDerivativeState state)
	{
		float kRaw = PerpAccelLen(acceleration, v);
		if (!float.IsFinite(kRaw) || kRaw < 0f)
		{
			kRaw = 0f;
		}

		float alpha = Mathf.Clamp(DerivativeAwareSmoothingAlpha, 0.01f, 1.0f);
		float prevSmoothedK = state.SmoothedK;
		float smoothedK = state.HasSample
			? (prevSmoothedK + ((kRaw - prevSmoothedK) * alpha))
			: kRaw;
		float dk = state.HasSample ? (smoothedK - prevSmoothedK) : 0f;
		float d2k = state.HasSample ? (dk - state.PrevDk) : 0f;

		state.HasSample = true;
		state.SampleCount++;
		state.SmoothedK = smoothedK;
		state.PrevDk = dk;
		state.CurvatureSum += kRaw;
		state.CurvatureMax = Mathf.Max(state.CurvatureMax, kRaw);
		state.DkMax = Mathf.Max(state.DkMax, Mathf.Abs(dk));
		state.D2kMax = Mathf.Max(state.D2kMax, Mathf.Abs(d2k));
	}

	private static void RecordPass1TelemetryTurnSample(
		Vector3 vBefore,
		Vector3 vAfter,
		ref Pass1TelemetryDerivativeState state)
	{
		float turn = ComputeDirectionTurnAngle(vBefore, vAfter);
		if (!float.IsFinite(turn) || turn <= 0f)
		{
			return;
		}

		state.TurnSum += turn;
		state.TurnMax = Mathf.Max(state.TurnMax, turn);
	}

	private void RecordDerivativeAwareMetricSubdivisionRetry()
	{
		if (IsDerivativeAwareSteppingEnabled())
		{
			_derivativeAwareMetricSubdivisionRetryCount++;
		}
	}

	private bool TryStepIntegratedTransport(
		Vector3 p,
		Vector3 v,
		Vector3 center,
		float beta,
		float gamma,
		FieldSourceSnap[] fieldSources,
		bool hasSources,
		float maxDistance,
		float traveled,
		float minStep,
		float maxStep,
		FieldGrid3D fieldGrid,
		bool applyLowCurvatureBoost,
		ref DerivativeAwareStepState derivativeState,
		out Vector3 next,
		out Vector3 vNext,
		out Vector3 acceleration,
		out float step)
	{
		next = p;
		vNext = v;
		acceleration = Vector3.Zero;
		step = 0f;

		float remaining = maxDistance - traveled;
		if (remaining <= 0f)
		{
			return false;
		}

		TransportModel active = hasSources ? ResolveActiveTransportModel(fieldSources) : TransportModel.GRIN_Optical;
		if (fieldGrid != null && active == TransportModel.Metric_NullGeodesic && fieldGrid.TrySample(p, out acceleration))
		{
			_metricGridBypassStepCount++;
		}
		else
		{
			acceleration = ComputeTransportAccelerationForActiveModel(active, p, v, Vector3.Zero, center, beta, gamma, fieldSources, hasSources);
		}

		float aLen = acceleration.Length();
		if (!float.IsFinite(aLen))
		{
			acceleration = Vector3.Zero;
			aLen = 0f;
		}
		else if (aLen > 50f)
		{
			acceleration *= (50f / aLen);
			aLen = 50f;
		}

		step = ComputeAdaptiveIntegratedStepLength(
			v,
			acceleration,
			minStep,
			maxStep,
			applyLowCurvatureBoost,
			applyMetricCurvatureGain: false,
			ref derivativeState);

		if (step > remaining)
		{
			step = remaining;
		}

		Vector3 vBeforeStep = v;
		vNext = SafeNormalized(v + acceleration * step, v);
		RecordTransportSteeringStep(p, vBeforeStep, vNext, center, fieldSources, hasSources);
		next = p + vNext * step;
		return true;
	}

	private Vector3 ComputeTransportAccelerationForActiveModel(
		TransportModel active,
		Vector3 p,
		Vector3 v,
		Vector3 preferredBendDir,
		Vector3 center,
		float beta,
		float gamma,
		FieldSourceSnap[] fieldSources,
		bool hasSources)
	{
		return active switch
		{
			TransportModel.Metric_NullGeodesic => StepTransport_MetricStub(p, v, preferredBendDir, center, beta, gamma, fieldSources, hasSources),
			TransportModel.Hybrid_Research => StepTransport_HybridStub(p, v, center, beta, gamma, fieldSources, hasSources),
			_ => StepTransport_GRIN(p, center, beta, gamma, fieldSources, hasSources)
		};
	}

	// GRIN_Optical is the currently validated transport mode.
	private Vector3 StepTransport_GRIN(
		Vector3 p,
		Vector3 center,
		float beta,
		float gamma,
		FieldSourceSnap[] fieldSources,
		bool hasSources)
	{
		if (hasSources)
		{
			return ComputeAccelerationAtPointSnap(p, fieldSources, beta, gamma, BendScale, FieldStrength);
		}

		Vector3 rvec = p - center;
		float rr = Mathf.Max(0.001f, rvec.Length());
		return (-rvec / rr) * (beta * FastPow(rr, gamma) * BendScale * FieldStrength);
	}

	// Metric_NullGeodesic is scaffolded; safe fallback remains GRIN until metric update is implemented.
	private Vector3 StepTransport_MetricStub(
		Vector3 p,
		Vector3 v,
		Vector3 preferredBendDir,
		Vector3 center,
		float beta,
		float gamma,
		FieldSourceSnap[] fieldSources,
		bool hasSources)
	{
		Vector3 grinAccel = StepTransport_GRIN(p, center, beta, gamma, fieldSources, hasSources);
		MetricSteeringLaw metricLaw = GetEffectiveMetricSteeringLaw();
		Vector3 sourceCenter = hasSources ? fieldSources[0].Center : center;
		float radialDistance = p.DistanceTo(sourceCenter);
		MetricWeakFieldScalarInputs metricInputs = ResolveMetricWeakFieldScalarInputs(
			fieldSources,
			beta,
			BendScale,
			FieldStrength);
		float characteristicRadius = hasSources
			? Mathf.Max(0.25f, metricInputs.ROuter)
			: Mathf.Max(0.25f, radialDistance);
		float metricScalarOverride = ResolveMetricComparisonScalarOverride();
		float mappedWeakFieldScalar = metricInputs.Scalar * metricScalarOverride;
		float grinAccelMagnitude = grinAccel.Length();
		if (grinAccelMagnitude > mappedWeakFieldScalar)
		{
			_metricScalarDominatedStepCount++;
		}
		float weakFieldScalar = Mathf.Max(mappedWeakFieldScalar, grinAccelMagnitude);
		var metricContext = new MetricTransportStepContext(
			p,
			sourceCenter,
			radialDistance,
			characteristicRadius,
			v,
			preferredBendDir,
			StepLength,
			weakFieldScalar);
		MetricDirectionDeltaEvaluation metricEval = EvaluateMetricDirectionDeltaStub(in metricContext, metricLaw);
		RecordMetricDirectionDeltaEvaluation(in metricEval);
		MaybeLogMetricDiagnosticSample(in metricContext, in metricEval);
		if (!_loggedMetricSteeringLaw)
		{
			_loggedMetricSteeringLaw = true;
			GD.Print(
				$"[Transport][MetricLaw] active={GetMetricSteeringLawToken(metricLaw)} transport={TransportModel.Metric_NullGeodesic}");
		}
		if (!_loggedMetricWeakFieldMapping)
		{
			_loggedMetricWeakFieldMapping = true;
			GD.Print(
				$"[Transport] Metric_NullGeodesic weak-field scaffold active. " +
				$"effectiveMetricScalar={weakFieldScalar:0.######} " +
				$"(mapped={mappedWeakFieldScalar:0.######}, grinAccelMag={grinAccelMagnitude:0.######}, metricScalarOverride={metricScalarOverride:0.###}, " +
				$"metricLaw={GetMetricSteeringLawToken(metricLaw)}, formula=weakFieldScalar*step*boundedLensingWeight).");
		}
		if (!_loggedMetricScalarIngredients)
		{
			_loggedMetricScalarIngredients = true;
			string sourceToken = metricInputs.HasEnabledSource ? metricInputs.SourceIndex.ToString(CultureInfo.InvariantCulture) : "none";
			string betaMode = metricInputs.OverrideBetaScale ? "override" : "global";
			GD.Print(
				$"[Transport][MetricScalarMap] sourceIndex={sourceToken} amp={metricInputs.Amp:0.######} " +
				$"betaScaleEff={metricInputs.BetaScaleEff:0.######} betaMode={betaMode} " +
				$"bendScaleEff={metricInputs.BendScaleEff:0.######} fieldStrengthEff={metricInputs.FieldStrengthEff:0.######} " +
				$"rOuter={metricInputs.ROuter:0.######} mappedWeakField={mappedWeakFieldScalar:0.######} " +
				$"grinAccelMag={grinAccelMagnitude:0.######} effectiveMetricScalar={weakFieldScalar:0.######} " +
				$"curveIndirect={metricInputs.CurveType} note=gamma/a/b/c/sigma/rInner/curve-shape only via grinFloorOrFallback");
		}

		if (metricEval.IsZero)
		{
			_metricFallbackCount++;
			if (!_loggedMetricStubFallback)
			{
				_loggedMetricStubFallback = true;
				string radiusFlag = metricEval.RadiusBelowDiagnosticThreshold
					? "r_lo"
					: (metricEval.RadiusAboveDiagnosticThreshold ? "r_hi" : "r_ok");
				GD.Print(
					$"[Transport] Metric_NullGeodesic produced zero direction delta; using GRIN acceleration fallback. " +
					$"reason={GetMetricDeltaZeroReasonToken(metricEval.ZeroReason)} radiusFlag={radiusFlag} " +
					$"r={metricEval.Radius:0.######} bendPerp={metricEval.BendPerpMagnitude:0.######} dTheta={metricEval.DTheta:0.######}");
			}

			return grinAccel;
		}

		Vector3 dir = SafeNormalized(v, Vector3.Forward);
		Vector3 dirNext = SafeNormalized(dir + metricEval.Delta, dir);
		Vector3 metricEquivalentAccel = (dirNext - dir) / metricContext.StepSize;
		if (!IsFinite(metricEquivalentAccel))
		{
			_metricFallbackCount++;
			if (!_loggedMetricEquivalentFallback)
			{
				_loggedMetricEquivalentFallback = true;
				GD.Print("[Transport] Metric_NullGeodesic produced non-finite equivalent acceleration; using GRIN fallback.");
			}
			return grinAccel;
		}

		_metricContributionAppliedCount++;
		return metricEquivalentAccel;
	}

	private Vector3 StepTransport_HybridStub(
		Vector3 p,
		Vector3 v,
		Vector3 center,
		float beta,
		float gamma,
		FieldSourceSnap[] fieldSources,
		bool hasSources)
	{
		_ = v;
		if (!_loggedHybridStubFallback)
		{
			_loggedHybridStubFallback = true;
			GD.Print("[Transport] Hybrid_Research stub active; falling back to GRIN_Optical transport.");
		}

		return StepTransport_GRIN(p, center, beta, gamma, fieldSources, hasSources);
	}

	// Hook contract for next phase metric transport (weak-field / null-geodesic update).
	private static MetricDirectionDeltaEvaluation EvaluateMetricDirectionDeltaStub(
		in MetricTransportStepContext context,
		MetricSteeringLaw metricLaw)
	{
		// Research scaffold only:
		// First-order weak-field, Schwarzschild-inspired radial bending surrogate.
		// This is NOT a tensor/Christoffel geodesic solver; it only applies a small turn
		// toward the source center in the plane perpendicular to the current direction.
		const float radiusLowDiagnosticThreshold = 0.01f;
		const float radiusHighDiagnosticThreshold = 16f;
		if (!IsFinite(context.Position) ||
			!IsFinite(context.SourceCenter) ||
			!IsFinite(context.Direction) ||
			!float.IsFinite(context.StepSize) ||
			!float.IsFinite(context.WeakFieldScalar) ||
			context.Direction.LengthSquared() <= 1e-12f)
		{
			float invalidRadius = float.IsFinite(context.RadialDistance) ? Mathf.Abs(context.RadialDistance) : 0f;
			bool radiusLow = invalidRadius <= radiusLowDiagnosticThreshold;
			bool radiusHigh = invalidRadius >= radiusHighDiagnosticThreshold;
			return new MetricDirectionDeltaEvaluation(
				Vector3.Zero,
				MetricDeltaZeroReason.NonFiniteOrNormalizationGuard,
				invalidRadius,
				0f,
				0f,
				false,
				false,
				false,
				MetricPerpendicularRecoveryBasis.None,
				radiusLow,
				radiusHigh);
		}

		float rawRadius = Mathf.Abs(context.RadialDistance);
		bool radiusBelowThreshold = rawRadius <= radiusLowDiagnosticThreshold;
		bool radiusAboveThreshold = rawRadius >= radiusHighDiagnosticThreshold;
		if (context.StepSize <= 1e-6f)
		{
			return new MetricDirectionDeltaEvaluation(
				Vector3.Zero,
				MetricDeltaZeroReason.StepGuard,
				rawRadius,
				0f,
				0f,
				false,
				false,
				false,
				MetricPerpendicularRecoveryBasis.None,
				radiusBelowThreshold,
				radiusAboveThreshold);
		}

		Vector3 dir = SafeNormalized(context.Direction, Vector3.Forward);
		Vector3 toCenter = context.SourceCenter - context.Position;
		float toCenterLenSq = toCenter.LengthSquared();
		if (toCenterLenSq <= 1e-12f)
		{
			return new MetricDirectionDeltaEvaluation(
				Vector3.Zero,
				MetricDeltaZeroReason.SourceCoincident,
				rawRadius,
				0f,
				0f,
				false,
				false,
				false,
				MetricPerpendicularRecoveryBasis.None,
				radiusBelowThreshold,
				radiusAboveThreshold);
		}

		Vector3 radialDir = toCenter / Mathf.Sqrt(toCenterLenSq);
		Vector3 bendDirPerp = radialDir - dir * radialDir.Dot(dir);
		float bendPerpLenSq = bendDirPerp.LengthSquared();
		float bendPerpMagnitude = bendPerpLenSq > 0f ? Mathf.Sqrt(bendPerpLenSq) : 0f;
		const float parallelAlignmentThreshold = 0.999999f;
		const float parallelRecoveryPerpThreshold = 1e-5f;
		const float parallelRecoveryPerpThresholdSq = parallelRecoveryPerpThreshold * parallelRecoveryPerpThreshold;
		float radialAlignment = Mathf.Abs(radialDir.Dot(dir));
		bool encounteredRawParallel = radialAlignment >= parallelAlignmentThreshold;
		bool usedParallelRecovery = false;
		bool usedPreferredBendRecovery = false;
		MetricPerpendicularRecoveryBasis recoveryBasis = MetricPerpendicularRecoveryBasis.None;
		if (encounteredRawParallel && bendPerpLenSq <= parallelRecoveryPerpThresholdSq)
		{
			if (TryBuildStableMetricPerpendicular(
				dir,
				bendDirPerp,
				context.PreferredBendDir,
				out Vector3 recoveredPerp,
				out recoveryBasis))
			{
				bendDirPerp = recoveredPerp;
				bendPerpMagnitude = recoveredPerp.Length();
				usedParallelRecovery = true;
				usedPreferredBendRecovery = recoveryBasis == MetricPerpendicularRecoveryBasis.Preferred;
			}
			else
			{
				return new MetricDirectionDeltaEvaluation(
					Vector3.Zero,
					MetricDeltaZeroReason.RadialParallel,
					rawRadius,
					bendPerpMagnitude,
					0f,
					true,
					false,
					false,
					recoveryBasis,
					radiusBelowThreshold,
					radiusAboveThreshold);
			}
		}
		else if (bendPerpLenSq <= 1e-12f)
		{
			return new MetricDirectionDeltaEvaluation(
				Vector3.Zero,
				MetricDeltaZeroReason.BendPerpBelowEpsilon,
				rawRadius,
				bendPerpMagnitude,
				0f,
				encounteredRawParallel,
				false,
				false,
				recoveryBasis,
				radiusBelowThreshold,
				radiusAboveThreshold);
		}
		else
		{
			bendDirPerp /= bendPerpMagnitude;
		}

		float r = Mathf.Max(1e-3f, rawRadius);
		float weak = Mathf.Max(0f, context.WeakFieldScalar);
		float characteristicRadius = Mathf.Max(0.25f, context.CharacteristicRadius);
		float transverseDistance = Mathf.Max(0f, rawRadius * bendPerpMagnitude);
		float radiusNorm = r / characteristicRadius;
		float impactParameterNorm = transverseDistance / characteristicRadius;
		float dTheta = metricLaw switch
		{
			MetricSteeringLaw.MetricLaw_ImpactParameterApprox => EvaluateMetricImpactParameterApproxTurn(
				weak,
				context.StepSize,
				radiusNorm,
				impactParameterNorm),
			_ => EvaluateMetricCurrentEnvelopeTurn(
				weak,
				context.StepSize,
				radiusNorm,
				impactParameterNorm)
		};
		float clampedDTheta = Mathf.Clamp(dTheta, 0f, 0.08f);
		if (!float.IsFinite(clampedDTheta) || clampedDTheta <= 0f)
		{
			return new MetricDirectionDeltaEvaluation(
				Vector3.Zero,
				MetricDeltaZeroReason.ZeroTurn,
				rawRadius,
				bendPerpMagnitude,
				float.IsFinite(clampedDTheta) ? clampedDTheta : 0f,
				encounteredRawParallel,
				usedParallelRecovery,
				usedPreferredBendRecovery,
				recoveryBasis,
				radiusBelowThreshold,
				radiusAboveThreshold);
		}

		return new MetricDirectionDeltaEvaluation(
			bendDirPerp * clampedDTheta,
			MetricDeltaZeroReason.None,
			rawRadius,
			bendPerpMagnitude,
			clampedDTheta,
			encounteredRawParallel,
			usedParallelRecovery,
			usedPreferredBendRecovery,
			recoveryBasis,
			radiusBelowThreshold,
			radiusAboveThreshold);
	}

	private static float EvaluateMetricCurrentEnvelopeTurn(
		float weak,
		float stepSize,
		float radiusNorm,
		float impactParameterNorm)
	{
		const float impactSoftening = 0.35f;
		const float coreRise = 0.5f;
		const float outerTailGain = 0.6f;
		const float metricGain = 8f;
		float impactWeight = impactParameterNorm / (impactParameterNorm + impactSoftening);
		float coreSuppression = 1f - Mathf.Exp(-(radiusNorm * radiusNorm) / (coreRise * coreRise));
		float outerEnvelope = 1f / (1f + radiusNorm * radiusNorm * outerTailGain);
		float lensingWeight = impactWeight * coreSuppression * outerEnvelope;
		return weak * stepSize * metricGain * lensingWeight;
	}

	private static float EvaluateMetricImpactParameterApproxTurn(
		float weak,
		float stepSize,
		float radiusNorm,
		float impactParameterNorm)
	{
		const float impactPeak = 0.95f;
		const float impactWidth = 0.55f;
		const float impactCenterSoftening = 0.22f;
		const float impactOuterTail = 0.18f;
		const float radiusEnvelopeGain = 0.18f;
		const float metricGain = 12f;
		float centeredImpact = (impactParameterNorm - impactPeak) / impactWidth;
		float bandFocus = 1f / (1f + centeredImpact * centeredImpact * centeredImpact * centeredImpact);
		float centerSuppression = 1f - Mathf.Exp(-(impactParameterNorm * impactParameterNorm) / (impactCenterSoftening * impactCenterSoftening));
		float impactTail = 1f / (1f + impactParameterNorm * impactParameterNorm * impactOuterTail);
		float radiusEnvelope = 1f / (1f + radiusNorm * radiusNorm * radiusEnvelopeGain);
		float lensingWeight = bandFocus * centerSuppression * impactTail * radiusEnvelope;
		return weak * stepSize * metricGain * lensingWeight;
	}

	private static bool TryBuildStableMetricPerpendicular(
		Vector3 dir,
		Vector3 preferredPerp,
		Vector3 preferredBendDir,
		out Vector3 recoveredPerp,
		out MetricPerpendicularRecoveryBasis recoveryBasis)
	{
		recoveryBasis = MetricPerpendicularRecoveryBasis.None;
		if (IsFinite(preferredBendDir) &&
			preferredBendDir.LengthSquared() > 1e-12f &&
			TryProjectMetricAxisPerpendicular(dir, preferredBendDir, out recoveredPerp))
		{
			if (preferredPerp.LengthSquared() > 1e-20f && recoveredPerp.Dot(preferredPerp) < 0f)
			{
				recoveredPerp = -recoveredPerp;
			}

			recoveryBasis = MetricPerpendicularRecoveryBasis.Preferred;
			return true;
		}

		Vector3 axis = SelectLeastAlignedMetricAxis(dir);
		if (!TryProjectMetricAxisPerpendicular(dir, axis, out recoveredPerp))
		{
			Vector3 fallbackAxisA = axis == Vector3.Right ? Vector3.Up : Vector3.Right;
			Vector3 fallbackAxisB = axis == Vector3.Forward ? Vector3.Up : Vector3.Forward;
			if (!TryProjectMetricAxisPerpendicular(dir, fallbackAxisA, out recoveredPerp) &&
				!TryProjectMetricAxisPerpendicular(dir, fallbackAxisB, out recoveredPerp))
			{
				recoveredPerp = Vector3.Zero;
				return false;
			}
		}

		if (preferredPerp.LengthSquared() > 1e-20f && recoveredPerp.Dot(preferredPerp) < 0f)
		{
			recoveredPerp = -recoveredPerp;
		}

		recoveryBasis = MetricPerpendicularRecoveryBasis.Axis;
		return IsFinite(recoveredPerp);
	}

	private static bool TryProjectMetricAxisPerpendicular(Vector3 dir, Vector3 axis, out Vector3 recoveredPerp)
	{
		recoveredPerp = axis - dir * axis.Dot(dir);
		float recoveredLenSq = recoveredPerp.LengthSquared();
		if (recoveredLenSq <= 1e-12f)
		{
			recoveredPerp = Vector3.Zero;
			return false;
		}

		recoveredPerp /= Mathf.Sqrt(recoveredLenSq);
		return IsFinite(recoveredPerp);
	}

	private static Vector3 SelectLeastAlignedMetricAxis(Vector3 dir)
	{
		float ax = Mathf.Abs(dir.X);
		float ay = Mathf.Abs(dir.Y);
		float az = Mathf.Abs(dir.Z);
		if (ax <= ay && ax <= az)
		{
			return Vector3.Right;
		}
		if (ay <= az)
		{
			return Vector3.Up;
		}
		return Vector3.Forward;
	}

	private float GetPixelsPerRadian(Camera3D cam)
	{
		// DECISION: no camera yields fallback scale.
		if (cam == null) return 1000f;

		// vertical FOV in radians (Godot 4 uses Degrees for Fov)
		float fovY = Mathf.DegToRad(cam.Fov);

		// viewport height in pixels
		var vp = cam.GetViewport();
		// DECISION: fallback to 720px when viewport is missing.
		float h = vp != null ? vp.GetVisibleRect().Size.Y : 720f;
		h = Mathf.Max(1f, h);

		// px per radian for vertical axis:
		// tan(theta/2) = (h/2)/f  =>  f = (h/2)/tan(theta/2)
		// angle small: pixels ~ angle * f
		float f = (h * 0.5f) / Mathf.Max(1e-6f, Mathf.Tan(fovY * 0.5f));
		return f;
	}

	private static float PerpAccelLen(Vector3 a, Vector3 vNorm)
	{
		// a_perp = a - v*(a·v)
		Vector3 aPar = vNorm * a.Dot(vNorm);
		Vector3 aPerp = a - aPar;
		return aPerp.Length();
	}

	private int ComputeCeFromScreenError(
		int ceBase,
		float stepLen,
		float aPerpLen,
		float depth,
		float pxPerRad,
		float maxErrPx)
	{
		ceBase = Mathf.Max(1, ceBase);
		// DECISION: near-zero perpendicular acceleration means no extra curvature → base cadence.
		if (aPerpLen <= 1e-8f) return ceBase;

		// CONTROL FACTOR: MinDepthForError clamps depth to avoid huge cadence values.
		depth = Mathf.Max(MinDepthForError, depth);
		pxPerRad = Mathf.Max(1e-3f, pxPerRad);
		maxErrPx = Mathf.Max(1e-3f, maxErrPx);

		// LsegMax = sqrt( 2*maxErrPx*depth / (aPerpLen*pxPerRad) )
		float LsegMax = Mathf.Sqrt((2f * maxErrPx * depth) / (aPerpLen * pxPerRad));

		int ce = (int)Mathf.Floor(LsegMax / Mathf.Max(1e-6f, stepLen));
		// DECISION: clamp cadence between MinCollisionEveryNSteps and base cadence.
		ce = Mathf.Clamp(ce, MinCollisionEveryNSteps, ceBase);
		return ce;
	}

	// ===== Output / Debug =====
	// Debug Overlay Builder (uses _rayMeta + _samplePos + _hitPayload)
	private void UpdateDebugOverlay(Camera3D cam, int raysWritten)
	{
		GD.Print($"[DBG] Overlay call: mode={DebugMode} raysWritten={raysWritten} dbgMeshNull={_dbgMeshInstance==null}");

		// --- CAN'T-MISS sanity line (1m in front of camera) ---
		var camXform = cam.GlobalTransform;
		Vector3 p0 = camXform.Origin + (-camXform.Basis.Z) * 0.5f; // forward in Godot is -Z
		Vector3 p1 = camXform.Origin + (-camXform.Basis.Z) * 2.5f;
		DbgAddLine(p0, p1, new Color(1, 0, 0, 1));

		// DECISION: throttle debug log to every 60 frames.
		if (Engine.GetFramesDrawn() % 60 == 0)
			GD.Print($"[DBG] UpdateDebugOverlay called. mode={DebugMode}");


		// DECISION: cannot draw overlay without debug mesh buffers.
		if (_dbgMeshInstance == null || _dbgImmediate == null) return;

		// DECISION: if debug is off, hide and clear.
		if (DebugMode == DebugDrawMode.Off)
		{
			_dbgMeshInstance.Visible = false;
			_dbgImmediate.ClearSurfaces();
			return;
		}

		_dbgMeshInstance.Visible = true;
		DbgClearLines();
		DbgAddLine(Vector3.Zero, Vector3.Forward * 5f, Colors.Red);
		DbgFlushLines();
		//return;

		int drawn = 0;
		// CONTROL FACTOR: DebugMaxRays caps overlay workload.
		int rayLimit = Mathf.Min(DebugMaxRays, raysWritten);

		// DECISION: iterate rays up to DebugMaxRays cap.
		for (int r = 0; r < rayLimit; r++)
		{
			ref RayMeta meta = ref _rayMeta[r];

			// DECISION: optionally draw only hit rays.
			if (DebugDrawOnlyHits && !meta.HadHit)
				continue;

			int count = Mathf.Max(0, meta.RenderCount); // RenderCount already respects TerminateTrailOnHit
			// DECISION: need at least two points to draw a segment.
			if (count < 2)
				continue;

			// CONTROL FACTOR: DebugMaxSegmentsPerRay caps per-ray segments.
			int segMax = Mathf.Min(DebugMaxSegmentsPerRay, count - 1);

			int start = meta.SampleStart;

			// --- Draw ray polyline from samples ---
			// DECISION: draw polyline segments up to DebugMaxSegmentsPerRay cap.
			for (int i = 0; i < segMax; i++)
			{
				Vector3 a = _samplePos[start + i];
				Vector3 b = _samplePos[start + i + 1];

				// Fade alpha along the ray for readability
				// DECISION: avoid divide-by-zero when only one segment.
				float t = segMax <= 1 ? 1f : (float)i / (segMax - 1);
				Color c = new Color(1f, 1f, 1f, 0.25f + 0.75f * t);

				DbgAddLine(a, b, c);
			}

			// --- Draw hit normal as RGB (abs normal) ---
			// DECISION: draw hit normals only in RaysAndNormals mode and when hit exists.
			if (DebugMode == DebugDrawMode.RaysAndNormals && meta.HadHit)
			{
				HitPayload hp = _hitPayload[r];
				// DECISION: only draw if payload is valid.
				if (hp.Valid)
				{
					Vector3 p = hp.Position;
					Vector3 n = hp.Normal;

					// DECISION: skip near-zero normals.
					if (n.LengthSquared() > 1e-10f)
					{
						n = n.Normalized();
						Color nc = new Color(Mathf.Abs(n.X), Mathf.Abs(n.Y), Mathf.Abs(n.Z), 1f);
						DbgAddLine(p, p + n * DebugNormalLen, nc);
					}
				}
			}

			drawn++;
		}

		DbgFlushLines();

		// Optional: quick sanity print
		// GD.Print($"[DBG] Overlay rays drawn={drawn} raysWritten={raysWritten} mode={DebugMode}");
	}

	private void DbgClearLines()
	{
		_dbgLinePoints.Clear();
		_dbgLineColors.Clear();
	}

	private void DbgAddLine(Vector3 a, Vector3 b, Color c)
	{
		_dbgLinePoints.Add(a);
		_dbgLinePoints.Add(b);
		_dbgLineColors.Add(c);
		_dbgLineColors.Add(c);
	}

	private void DbgFlushLines()
	{
		_dbgImmediate.ClearSurfaces();
		// DECISION: need at least 2 points to draw a line.
		if (_dbgLinePoints.Count < 2) return;

		_dbgImmediate.SurfaceBegin(Mesh.PrimitiveType.Lines, _dbgMaterial);

		// DECISION: emit all queued debug line segments.
		for (int i = 0; i < _dbgLinePoints.Count; i++)
		{
			_dbgImmediate.SurfaceSetColor(_dbgLineColors[i]);
			_dbgImmediate.SurfaceAddVertex(_dbgLinePoints[i]);
		}

		_dbgImmediate.SurfaceEnd();
	}

	// ============================================================================
	// Film-driven debug overlay (call this from GrinFilmCamera pass2 / main thread)
	// ============================================================================

	public void UpdateDebugOverlayFromFilm(
		Camera3D cam,
		ReadOnlySpan<Vector3> rayPointsWorld,   // polyline points: p0,p1,p2... per ray (concatenated)
		ReadOnlySpan<int> rayOffsets,           // start index per ray into rayPointsWorld (length = rayCount)
		ReadOnlySpan<int> rayCounts,            // point count per ray (length = rayCount)
		ReadOnlySpan<HitPayload> hits,          // length = rayCount (or 0 if none)
		int everyNRays = 1,                     // stride for performance
		float normalLen = 0.15f                 // how long to draw hit normals
	)
	{
		// CROSS-CLASS CONTRACT: GrinFilmCamera supplies rayPointsWorld/offsets/counts built in world space.
		// ASSUMPTION: rayPointsWorld are contiguous polylines, offsets/counts index into same array.
		// EFFECT: incorrect offsets/counts will draw garbage lines or hit normals.

		//GD.Print($"[DBG] Entry to UpdateDebugOverlayFromFile");

		// DECISION: fast bail-outs to avoid any work when debug is disabled or inputs are invalid.
		if (DebugMode == DebugDrawMode.Off)
		{
			// DECISION: hide debug mesh when debug is off.
			if (_dbgMeshInstance != null) _dbgMeshInstance.Visible = false;
			return;
		}

		// DECISION: must have debug mesh buffers to draw anything.
		if (_dbgImmediate == null || _dbgMeshInstance == null)
		{
			GD.Print($"[DBG] _dbgImmediate or _dbgMeshInstance is null");
			return;
		}

		//GD.Print($"[DBG] Inside UpdateDebugOverlayFromFile. cam = {cam}");
		// DECISION: no camera means no camera axes or consistent draw frame.
		if (cam == null)
		{
			GD.Print($"[DBG] Cam is Null");
			return;
		}

		_dbgMeshInstance.Visible = true;

		// Optional: keep overlay "always visible"
		// (NoDepthTest already handles depth; these help order if needed)
		// DECISION: if material supports render priority, push it to front for visibility.
		if (_dbgMaterial is BaseMaterial3D bm)
			bm.RenderPriority = 127;

		//GD.Print($"[DBG] ClearSurfaces/SurfaceBegin calls");
		// ---- Build ImmediateMesh lines ----
		_dbgImmediate.ClearSurfaces();
		_dbgImmediate.SurfaceBegin(Mesh.PrimitiveType.Lines, _dbgMaterial);
		
		//GD.Print($"[DBG] AddCamAxes call");
		// 1) Camera axes (super helpful for grounding)
		AddCamAxes(cam, 0.35f);

		// 2) Rays + hit normals
		int rayCount = rayOffsets.Length;
		int stride = Math.Max(1, everyNRays);
		GD.Print($"[DBG] rayCount = {rayCount}");
		// DECISION: iterate rays at the requested stride for performance.
		for (int r = 0; r < rayCount; r += stride)
		{
			int start = rayOffsets[r];
			int count = rayCounts[r];
			//GD.Print($"[DBG] ray r={r} / start = {start} / count = {count}");

			// DECISION: skip invalid or too-short polylines.
			if (start < 0 || count < 2) continue;
			// DECISION: skip if indices would exceed buffer bounds.
			if (start + count > rayPointsWorld.Length) continue;
			// Need at least 2 points to draw a polyline
			// DECISION: need at least 2 points to draw a polyline.
			if (count < 2)
				continue;

			// Pull hit (if provided)
			bool hadHit = false;
			Vector3 hitPos = default;
			Vector3 hitNrm = default;

			// DECISION: only read hit payloads if provided.
			if (r < hits.Length)
			{
				hadHit = HitIsValid(hits[r]);
				//GD.Print($"[DBG] ray r={r} / hadHit= {hadHit}");

				// DECISION: only read hit position/normal when hit is valid.
				if (hadHit)
				{
					hitPos = hits[r].Position;
					hitNrm = hits[r].Normal;
					//GD.Print($"[DBG] ray r={r} / hitPos= {hitPos} / hitNrm= {hitNrm}");
				}
			}

			// DECISION: optionally draw only hit rays.
			if (DebugDrawOnlyHits && !hadHit)
				continue;

			// Ray color
			// - green for non-hit rays, yellow for hit rays (easy to read)
			// DECISION: color rays differently based on hit state.
			Color rayC = hadHit ? new Color(1f, 1f, 0f, 1f) : new Color(0f, 1f, 0f, 1f);

			// Draw segments
			// DECISION: draw each segment between consecutive points.
			for (int i = 0; i < count - 1; i++)
			{
				Vector3 a = rayPointsWorld[start + i];
				Vector3 b = rayPointsWorld[start + i + 1];
				//GD.Print($"[DBG] Addline a={a} / b= {b} / rayC= {rayC}");
				AddLine(a, b, rayC);
			}

			//GD.Print($"[DBG] hitNrm.LengthSquared={hitNrm.LengthSquared()}");
			// Draw hit normal (red)
			// DECISION: draw hit normals only in RaysAndNormals mode and when normal is valid.
			if (DebugMode == DebugDrawMode.RaysAndNormals && hadHit && hitNrm.LengthSquared() > 1e-10f)
			{
				Vector3 n0 = hitPos;
				Vector3 n1 = hitPos + hitNrm.Normalized() * normalLen;
				//GD.Print($"[DBG] Addline n0={n0} / n1={n1}");
				AddLine(n0, n1, new Color(1f, 0f, 0f, 1f));
			}
		}

		_dbgImmediate.SurfaceEnd();
	}

	// ---------------------------------------------------------------------------
	// Helpers (ImmediateMesh line + camera axes)
	// ---------------------------------------------------------------------------

	private void AddLine(Vector3 a, Vector3 b, Color c)
	{
		_dbgImmediate.SurfaceSetColor(c);
		_dbgImmediate.SurfaceAddVertex(a);
		_dbgImmediate.SurfaceSetColor(c);
		_dbgImmediate.SurfaceAddVertex(b);
	}

	private void AddCamAxes(Camera3D cam, float len)
	{
		// Godot 4: Basis.X = right, Basis.Y = up, -Basis.Z = forward
		Transform3D t = cam.GlobalTransform;
		Vector3 o = t.Origin;
		Vector3 right = t.Basis.X.Normalized();
		Vector3 up = t.Basis.Y.Normalized();
		Vector3 fwd = (-t.Basis.Z).Normalized();

		// X=red, Y=green, Z=blue-ish (forward)
		AddLine(o, o + right * len, new Color(1f, 0.2f, 0.2f, 1f));
		AddLine(o, o + up * len, new Color(0.2f, 1f, 0.2f, 1f));
		AddLine(o, o + fwd * len, new Color(0.35f, 0.6f, 1f, 1f));
	}

	private static bool HitIsValid(in HitPayload h)
	{
		return h.Valid;
	}


	/// Returns arrays that are valid until next render step/rebuild.
	/// Intended for GrinFilmCamera to drive debug overlay.
	public DebugRayBundle GetDebugRayBundle()
	{
		// CROSS-CLASS CONTRACT: GrinFilmCamera consumes these shared arrays directly.
		// ASSUMPTION: caller will read within RayCount and will not persist across rebuilds.
		// EFFECT: stale references after rebuild will point to reused buffers.

		// Build offsets/counts from _rayMeta (no allocations if arrays already sized)
		int rayCount = _rayWriteHead;

		// Clamp to capacity to be extra safe
		rayCount = Mathf.Clamp(rayCount, 0, _rayMeta.Length);

		// DECISION: map per-ray metadata into offsets/counts.
		for (int r = 0; r < rayCount; r++)
		{
			_dbgRayOffsets[r] = _rayMeta[r].SampleStart;
			_dbgRayCounts[r]  = Mathf.Max(0, _rayMeta[r].RenderCount); // respects TerminateTrailOnHit
		}

		// Points are the shared sample buffer
		return new DebugRayBundle(
			_samplePos,
			_dbgRayOffsets,
			_dbgRayCounts,
			_hitPayload,
			rayCount
		);
	}



}
