using Godot;
using System;
using System.Collections.Generic;

public partial class RayBeamRenderer : Node3D
{
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

	// ===== Cached State =====
	private MultiMeshInstance3D _mmi;
	private MultiMesh _mm;
	private StandardMaterial3D _mat;

	private float _lastBeta = float.NaN;
	private float _lastGamma = float.NaN;

	private bool _rebuildInProgress = false;
	private bool _rebuildQueued = false;

	// --- Change detection cache ---
	private Vector3 _lastCamPos = new Vector3(float.NaN, float.NaN, float.NaN);
	private float _lastCamFocal = float.NaN;
	private int _lastFieldSourceCount = -1;

	private Plane _insightPlane;
	private bool _hasInsightPlane = false;

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

	public struct HitPayload {
		public bool Valid;
		public Vector3 Position;
		public ulong ColliderId;
		public string ColliderName;
		public float Distance;     // path length to hit
		public Vector3 Normal;     // (optional for v0)
		public Color Albedo;       // (optional; can be constant for v0)
	}

	public struct RaySeg
	{
		public Vector3 A;
		public Vector3 B;
		public float TraveledB; // path length at end of segment (at B)
	}

	public delegate bool SegmentCallback(in RaySeg seg, int segIndex);

	public struct Pass1HitInfo
	{
		public bool Found;
		public float Distance;
		public Vector3 Position;
		public Vector3 Normal;
		public ulong ColliderId;
	}

	public struct FieldSourceSnap
	{
		public bool Enabled;
		public bool Attract;

		public Vector3 Center;

		public float Softening;
		public float MinRadius;
		public float MaxRadius;

		public bool OverrideGamma;
		public float Gamma;

		public bool OverrideBetaScale;
		public float BetaScale;

		public float Strength;

		public int Profile; // FieldSource3D.ProfileType cast to int (0..3)
		public float Sigma;

		public float InnerRadius;
		public float OuterRadius;
		public float EdgeSoftness;
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
	public override async void _Ready()
	{
		// DECISION: guard against double-init if _Ready runs again (scene reloads, etc.)
		// EFFECT: skip all setup when multimesh is already valid and in the tree.
		if (_mmi != null && IsInstanceValid(_mmi) && _mmi.IsInsideTree())
			return;

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
			_dbgMeshInstance.Owner = GetTree().EditedSceneRoot;
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

		// 3) Await frame (lets scene settle)
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

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

		_rebuildQueued = true;
		CallDeferred(nameof(DoRebuildDeferred));
	}

	private async void DoRebuildDeferred()
	{
		_rebuildQueued = false;
		// DECISION: wait for physics frame so collision queries are safe.
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Rebuild();
	}

	private Camera3D GetCamera()
	{
		// DECISION: use override path if set; otherwise use viewport camera.
		if (CameraPath != null && !CameraPath.IsEmpty)
			return GetNodeOrNull<Camera3D>(CameraPath);

		return GetViewport()?.GetCamera3D();
	}

	private void Rebuild()
	{
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

			var fieldSources = GetTree().GetNodesInGroup("field_sources");
			GD.Print("RayBeamRenderer: field sources in group = ", fieldSources.Count);
			bool hasSources = fieldSources.Count > 0;

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
						fieldSources,
						hasSources,
						CollisionMask,
						out hit
					);

					// DECISION: count hit rays for reporting/stats.
					if (meta.HadHit) hitCount++;

					// DECISION: print hit details only when debug enabled for this ray.
					if (debugThisRay && meta.HadHit)
						GD.Print($"[DBG] HIT collider='{hit.ColliderName}' id={hit.ColliderId} pos={hit.Position}");

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
		Vector3 aSum = Vector3.Zero;

		// DECISION: accumulate acceleration from each field source node.
		foreach (var n in sources)
		{
			// DECISION: only process FieldSource3D nodes.
			if (n is not FieldSource3D fs) continue;
			// DECISION: skip disabled field sources.
			if (!fs.Enabled) continue;

			Vector3 center = fs.GlobalPosition;
			Vector3 rvec = p - center;

			float rRaw = rvec.Length();
			float soft = Mathf.Max(0.00001f, fs.Softening);
			float r = Mathf.Sqrt(rRaw * rRaw + soft * soft);

			// DECISION: honor min/max radius gates.
			if (fs.MinRadius > 0.0f && r < fs.MinRadius) continue;
			// DECISION: skip when beyond max radius.
			if (fs.MaxRadius > 0.0f && r > fs.MaxRadius) continue;

			Vector3 dir = (-rvec / r);
			// DECISION: repel instead of attract when Attract is false.
			if (!fs.Attract) dir = -dir;

			// DECISION: apply per-source gamma override when enabled.
			float gamma = fs.OverrideGamma ? fs.Gamma : globalGamma;
			// DECISION: apply per-source beta scale override when enabled.
			float betaScale = fs.OverrideBetaScale ? fs.BetaScale : 1.0f;

			float amp = globalBeta * betaScale * BendScale * FieldStrength * fs.Strength;
			float mag = 0.0f;

			// DECISION: select field profile model.
			switch (fs.Profile)
			{
				case FieldSource3D.ProfileType.Power: // DECISION: Power profile
					mag = amp * Mathf.Pow(r, gamma);
					break;

				case FieldSource3D.ProfileType.InversePower: // DECISION: InversePower profile
					mag = amp / Mathf.Pow(r, Mathf.Max(0.0001f, gamma));
					break;

				case FieldSource3D.ProfileType.Gaussian: // DECISION: Gaussian profile
					{
						float sigma = Mathf.Max(0.0001f, fs.Sigma);
						float x = r / sigma;
						mag = amp * Mathf.Exp(-x * x);
					}
					break;

				case FieldSource3D.ProfileType.Shell: // DECISION: Shell profile
					{
						float inner = Mathf.Max(0.0f, fs.InnerRadius);
						float outer = Mathf.Max(inner + 0.0001f, fs.OuterRadius);
						float edge = Mathf.Max(0.0001f, fs.EdgeSoftness);

						float wIn = SmoothStep(inner - edge, inner + edge, r);
						float wOut = 1.0f - SmoothStep(outer - edge, outer + edge, r);
						float w = Mathf.Clamp(wIn * wOut, 0.0f, 1.0f);

						mag = amp * w * Mathf.Pow(r, gamma);
					}
					break;
			}

			aSum += dir * mag;
		}

		return aSum;
	}

	private static float SmoothStep(float a, float b, float x)
	{
		// EFFECT: cubic smooth step in [a,b].
		float t = Mathf.Clamp((x - a) / (b - a), 0.0f, 1.0f);
		return t * t * (3.0f - 2.0f * t);
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

			var rq = PhysicsRayQueryParameters3D.Create(prev, cur, mask);
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

	// ✅ ADD: normal-aware overload with optional collider name + query count
	public static bool SubdividedRayHit(PhysicsDirectSpaceState3D space,
		Vector3 a, Vector3 b, uint mask, int maxSubsteps,
		out Vector3 hitPos, out Vector3 hitNormal,
		out ulong colliderId, out string colliderName,
		out int rayQueryCount,
		bool includeColliderName,
		bool hitBackFaces = false,
		bool hitFromInside = false)
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

			var rq = PhysicsRayQueryParameters3D.Create(prev, cur, mask);
			rq.CollideWithBodies = true;
			rq.CollideWithAreas = true;
			rq.HitBackFaces = hitBackFaces;
			rq.HitFromInside = hitFromInside;

			rayQueryCount++;
			var hit = space.IntersectRay(rq);
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
							Godot.Collections.Array<Node> fieldSources,
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

		// DECISION: integrate along the ray for up to StepsPerRay steps.
		for (int s = 0; s <= StepsPerRay; s++)
		{
			Vector3 a = Vector3.Zero;
			Vector3 next = p;

			// DECISION: integrated vs analytic field path.
			if (UseIntegratedField)
			{
				// DECISION: use field sources if any; else use global radial field.
				if (hasSources)
					a = ComputeAccelerationAtPoint(p, fieldSources, beta, gamma);
				else
				{
					Vector3 rvec = p - center;
					float rr = Mathf.Max(0.001f, rvec.Length());
					a = (-rvec / rr) * (beta * Mathf.Pow(rr, gamma) * BendScale * FieldStrength);
				}

				float aLen = a.Length();

				// DECISION: sanitize non-finite acceleration.
				if (!float.IsFinite(aLen))
				{
					a = Vector3.Zero;
					aLen = 0.0f;
				}
				// DECISION: clamp extreme acceleration to avoid instability.
				else if (aLen > 50.0f)
				{
					a = a * (50.0f / aLen);
					aLen = 50.0f;
				}

				// CONTROL FACTOR: StepLength/StepAdaptGain control adaptive step size.
				float step = Mathf.Clamp(StepLength / (1.0f + aLen * StepAdaptGain), minStep, maxStep);
				v = SafeNormalized(v + a * step, v);
				next = p + v * step;

				float remaining = e.MaxDistance - traveled;
				// DECISION: clamp step to remaining distance to avoid overshoot.
				if (step > remaining) step = remaining; // DECISION: clamp step to remaining distance.

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
				float bend = beta * Mathf.Pow(t, gamma) * BendScale;
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
		_sampleWriteHead = sampleStart + sampleCount;

		// EFFECT: render count may be truncated by hit trail termination.
		int renderCount = Mathf.Min(sampleCount, Mathf.Max(0, trailStopCount));

		// EFFECT: emit hit payload (Valid=false when no hit).
		hitOut = new HitPayload
		{
			Valid = hadHit,
			Position = hitPos,
			ColliderId = hitColliderId,
			ColliderName = hitColliderName,
			// NEW (important)
			Distance = hitDist,              // ← this is the key one
			Normal = Vector3.Zero,             // v0 placeholder
			Albedo = Colors.White              // v0 placeholder
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
		Godot.Collections.Array<Node> fieldSources,
		bool hasSources, uint collisionMask,
		float maxDistance, out HitPayload hitOut)
	{
		bool hadHit = false;
		Vector3 hitPos = Vector3.Zero;
		ulong colliderId = 0;
		string colliderName = "<none>";
		float traveled = 0.0f;
		float hitDistance = 0f;

		Vector3 p = origin;
		Vector3 v = dir;

		// CONTROL FACTOR: CollisionEveryNSteps cadence (film version always checks on cadence).
		int ce = Mathf.Max(1, CollisionEveryNSteps);

		float minStep = Mathf.Min(MinStepLength, MaxStepLength);
		float maxStep = Mathf.Max(MinStepLength, MaxStepLength);
		minStep = Mathf.Max(0.0001f, minStep);

		// DECISION: integrate along the ray for up to StepsPerRay steps.
		for (int s = 0; s <= StepsPerRay; s++)
		{
			Vector3 next = p;

			// DECISION: integrated vs analytic field path.
			if (UseIntegratedField)
			{
				Vector3 a = Vector3.Zero;

				// DECISION: use field sources if any; else use global radial field.
				if (hasSources)
					a = ComputeAccelerationAtPoint(p, fieldSources, beta, gamma);
				else
				{
					Vector3 rvec = p - center;
					float rr = Mathf.Max(0.001f, rvec.Length());
					a = (-rvec / rr) * (beta * Mathf.Pow(rr, gamma) * BendScale * FieldStrength);
				}

				float aLen = a.Length();
				// DECISION: sanitize non-finite/overlarge acceleration.
				if (!float.IsFinite(aLen)) { a = Vector3.Zero; aLen = 0f; }
				else if (aLen > 50f) { a = a * (50f / aLen); aLen = 50f; } // DECISION: clamp extreme acceleration.

				float step = Mathf.Clamp(StepLength / (1.0f + aLen * StepAdaptGain), minStep, maxStep);

				v = SafeNormalized(v + a * step, v);
				next = p + v * step;

				//float segLenStep = (next - p).Length();
				//traveled += segLenStep;
				traveled += step;

				// DECISION: stop integrating when max distance exceeded.
				if (traveled > maxDistance) break;
			}
			else
			{
				// EFFECT: analytic bend path (no integration), step by StepLength.
				float t = s * StepLength;
				float bend = beta * Mathf.Pow(t, gamma) * BendScale;
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
			Distance = hitDistance,
			Normal = Vector3.Zero,
			Albedo = Colors.White
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
		float fieldStrength = FieldStrength;
		float stepLength = StepLength;
		float stepAdaptGain = StepAdaptGain;
		/////////////////////////
		/// 


		// DECISION: integrate along the ray for up to StepsPerRay steps.
		for (int s = 0; s <= StepsPerRay; s++)
		{
			Vector3 next = p;

			// DECISION: integrated vs analytic field path.
			if (UseIntegratedField)
			{
				// Early-out if we are already at/over max distance
				float remaining = maxDistance - traveled;
				// DECISION: stop when max distance reached.
				if (remaining <= 0f) break;

				Vector3 a;

				// DECISION: use field sources if any; else use global radial field.
				if (hasSources)
					a = ComputeAccelerationAtPointSnap(p, fieldSnaps, beta, gamma, bendScale, fieldStrength);
				else
				{
					Vector3 rvec = p - center;
					float rr = Mathf.Max(0.001f, rvec.Length());
					a = (-rvec / rr) * (beta * Mathf.Pow(rr, gamma) * bendScale * fieldStrength);
				}

				////////////
				////////////////
				///////////////////////////////////
				float aLen = a.Length();
				// DECISION: sanitize non-finite/overlarge acceleration.
				if (!float.IsFinite(aLen)) { a = Vector3.Zero; aLen = 0f; }
				else if (aLen > 50f) { a *= (50f / aLen); aLen = 50f; } // DECISION: clamp extreme acceleration.

				// Compute step FIRST
				float step = stepLength / (1.0f + aLen * stepAdaptGain);
				step = Mathf.Clamp(step, minStep, maxStep);

				// CONTROL FACTOR: LowCurvatureStepBoost/LowCurvaturePerpAccel adjust step size.
				// DECISION: boost step size on low curvature.
				if (LowCurvatureStepBoost > 1.0f)
				{
					Vector3 aPerp = a - v * a.Dot(v);
					float aPerpLen = aPerp.Length();
					// DECISION: treat low perpendicular acceleration as low curvature.
					if (aPerpLen < LowCurvaturePerpAccel)
						step = Mathf.Min(step * LowCurvatureStepBoost, maxStep);
				}

				// DECISION: clamp to remaining distance so we don't overshoot maxDistance.
				if (step > remaining) step = remaining; // DECISION: clamp step to remaining distance.

				//////////////////////////////
				/// ////////////
				/// /////////
				// DECISION: update segment cadence using screen-space error when enabled.
				if (UseScreenSpaceCollisionCadence && cam != null)
				{
					float aPerp = PerpAccelLen(a, v);   // v from previous iteration; ok
					float depth = (p - camPos).Length();
					ce = ComputeCeFromScreenError(ceBase, step, aPerp, depth, pxPerRad, CollisionMaxErrorPixels);
				}
				////////
				/// ///////////////
				/// //////////////////////////
				else
				{
					ce = ceBase;
				}

				v = SafeNormalized(v + a * step, v);
				next = p + v * step;

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

				float bend = beta * Mathf.Pow(t, gamma) * bendScale;
				next = origin + dir * t + bendDir * bend;
				traveled = t;
			}

			// DECISION: emit segments only at adaptive cadence.
			stepsSinceEmit++;
			// DECISION: emit when cadence threshold reached (skip s=0).
			if (s > 0 && stepsSinceEmit >= ce)
			{
				stepsSinceEmit = 0;
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
					TraveledB = traveled
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

		return written;
	}

	// Pass-1 variant to avoid per-pixel delegate allocations in Parallel.For.
	public int BuildRaySegmentsCamera_Pass1(
		PhysicsDirectSpaceState3D space,
		ref PhysicsRayQueryParameters3D quickRayParams,
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
		out Pass1HitInfo hitInfo,
		out bool stoppedEarly,
		out int hitSegIndex,
		out int stepsIntegrated,
		out int fieldEvals,
		out int pass1Raycasts,
		out int pass1ProbeHits,
		out int fieldGridHits,
		out int fieldGridMisses,
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
		stepsIntegrated = 0;
		fieldEvals = 0;
		pass1Raycasts = 0;
		pass1ProbeHits = 0;
		fieldGridHits = 0;
		fieldGridMisses = 0;

		hitInfo = new Pass1HitInfo
		{
			Found = false,
			Distance = float.PositiveInfinity,
			Position = Vector3.Zero,
			Normal = Vector3.Up,
			ColliderId = 0
		};
		stoppedEarly = false;
		hitSegIndex = -1;

		// Precompute for non-integrated mode
		float bendScale = BendScale;
		float fieldStrength = FieldStrength;
		float stepLength = StepLength;
		float stepAdaptGain = StepAdaptGain;
		// CONTROL FACTOR: pass1ProbeEveryNSegments controls cadence-based probing.
		int probeEvery = pass1ProbeEveryNSegments;
		bool useProbeEvery = probeEvery > 0;
		// CONTROL FACTOR: pass1ProbeMinTravelDelta controls travel-based probing.
		float probeMinTravelDelta = pass1ProbeMinTravelDelta;
		bool useProbeMinTravel = probeMinTravelDelta > 0f;
		// DECISION: when cadence probing is off, set countdown to max to avoid triggering.
		int probeCountdown = useProbeEvery ? probeEvery : int.MaxValue;
		float traveledSinceProbe = 0f;

		// DECISION: integrate along the ray for up to StepsPerRay steps.
		for (int s = 0; s <= StepsPerRay; s++)
		{
			stepsIntegrated++;
			float prevTraveled = traveled;
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
				if (fieldGrid != null && fieldGrid.TrySample(p, out a))
				{
					fieldGridHits++;
				}
				// DECISION: grid exists but no sample available.
				else if (fieldGrid != null)
				{
					fieldGridMisses++;
				}
				// DECISION: fall back to field sources if any.
				else if (hasSources)
					a = ComputeAccelerationAtPointSnap(p, fieldSnaps, beta, gamma, bendScale, fieldStrength);
				else
				{
					Vector3 rvec = p - center;
					float rr = Mathf.Max(0.001f, rvec.Length());
					a = (-rvec / rr) * (beta * Mathf.Pow(rr, gamma) * bendScale * fieldStrength);
				}
				fieldEvals++;

				float aLen = a.Length();
				// DECISION: sanitize non-finite/overlarge acceleration.
				if (!float.IsFinite(aLen)) { a = Vector3.Zero; aLen = 0f; }
				else if (aLen > 50f) { a *= (50f / aLen); aLen = 50f; } // DECISION: clamp extreme acceleration.

				// Compute step FIRST
				float step = stepLength / (1.0f + aLen * stepAdaptGain);
				step = Mathf.Clamp(step, minStep, maxStep);

				// CONTROL FACTOR: LowCurvatureStepBoost/LowCurvaturePerpAccel adjust step size.
				// DECISION: boost step size on low curvature.
				if (LowCurvatureStepBoost > 1.0f)
				{
					Vector3 aPerp = a - v * a.Dot(v);
					float aPerpLen = aPerp.Length();
					// DECISION: treat low perpendicular acceleration as low curvature.
					if (aPerpLen < LowCurvaturePerpAccel)
						step = Mathf.Min(step * LowCurvatureStepBoost, maxStep);
				}

				// DECISION: clamp to remaining distance so we don't overshoot maxDistance.
				if (step > remaining) step = remaining; // DECISION: clamp step to remaining distance.

				// DECISION: update segment cadence using screen-space error when enabled.
				if (UseScreenSpaceCollisionCadence && cam != null)
				{
					float aPerp = PerpAccelLen(a, v);   // v from previous iteration; ok
					float depth = (p - camPos).Length();
					ce = ComputeCeFromScreenError(ceBase, step, aPerp, depth, pxPerRad, CollisionMaxErrorPixels);
				}
				else
				{
					ce = ceBase;
				}

				v = SafeNormalized(v + a * step, v);
				next = p + v * step;

				// traveled increment is ~step (v is normalized)
				traveled += step;
			}
			else
			{
				// EFFECT: analytic bend path (no integration), step by StepLength.
				float t = s * stepLength;
				// DECISION: stop when max distance exceeded.
				if (t > maxDistance) break;

				float bend = beta * Mathf.Pow(t, gamma) * bendScale;
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
			stepsSinceEmit++;
			// DECISION: emit when cadence threshold reached (skip s=0).
			if (s > 0 && stepsSinceEmit >= ce)
			{
				stepsSinceEmit = 0;
				// DECISION: optionally filter segments by insight plane slab.
				if (useInsightPlane && !SegmentCrossesPlane(p, next, insightPlane, insightEps))
				{
					p = next;
					continue;
				}

				// DECISION: guard output capacity.
				if (written >= outCapacity) break;

				int segIndex = written;
				RaySeg seg = new RaySeg
				{
					A = p,
					B = next,
					TraveledB = traveled
				};
				outSegs[outOffset + written] = seg;
				written++;

				bool probeByTravel = useProbeMinTravel && (traveledSinceProbe >= probeMinTravelDelta);
				bool probeByCountdown = useProbeEvery && (probeCountdown <= 0);
				// DECISION: only probe when pass1DoHitTest enabled and a probe gate is satisfied.
				bool doProbe = pass1DoHitTest && (probeByTravel || probeByCountdown);

				// DECISION: only probe when allowed by gates and budget.
				if (doProbe)
				{
					traveledSinceProbe = 0f;
					// DECISION: reset countdown when using cadence-based probing.
					if (useProbeEvery)
						probeCountdown = probeEvery;
					pass1Raycasts++;
					quickRayParams.From = seg.A;
					quickRayParams.To = seg.B;
					var hit0 = space.IntersectRay(quickRayParams);
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

		return written;
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

			list.Add(new FieldSourceSnap
			{
				Enabled = fs.Enabled,
				Attract = fs.Attract,
				Center = fs.GlobalPosition,

				Softening = fs.Softening,
				MinRadius = fs.MinRadius,
				MaxRadius = fs.MaxRadius,

				OverrideGamma = fs.OverrideGamma,
				Gamma = fs.Gamma,

				OverrideBetaScale = fs.OverrideBetaScale,
				BetaScale = fs.BetaScale,

				Strength = fs.Strength,

				Profile = (int)fs.Profile,
				Sigma = fs.Sigma,

				InnerRadius = fs.InnerRadius,
				OuterRadius = fs.OuterRadius,
				EdgeSoftness = fs.EdgeSoftness
			});
		}

		return list.ToArray();
	}

	public static Vector3 ComputeAccelerationAtPointSnap(
		Vector3 p,
		FieldSourceSnap[] sources,
		float globalBeta, float globalGamma,
		float bendScale, float fieldStrength)
	{
		Vector3 aSum = Vector3.Zero;

		// DECISION: accumulate acceleration from each snapped field source.
		for (int i = 0; i < sources.Length; i++)
		{
			ref readonly var fs = ref sources[i];
			// DECISION: skip disabled field sources.
			if (!fs.Enabled) continue;

			Vector3 rvec = p - fs.Center;

			float rRaw = rvec.Length();
			float soft = Mathf.Max(0.00001f, fs.Softening);
			float r = Mathf.Sqrt(rRaw * rRaw + soft * soft);

			// DECISION: honor min/max radius gates.
			if (fs.MinRadius > 0.0f && r < fs.MinRadius) continue;
			// DECISION: skip when beyond max radius.
			if (fs.MaxRadius > 0.0f && r > fs.MaxRadius) continue;

			Vector3 dir = (-rvec / r);
			// DECISION: repel instead of attract when Attract is false.
			if (!fs.Attract) dir = -dir;

			// DECISION: apply per-source gamma override when enabled.
			float gamma = fs.OverrideGamma ? fs.Gamma : globalGamma;
			// DECISION: apply per-source beta scale override when enabled.
			float betaScale = fs.OverrideBetaScale ? fs.BetaScale : 1.0f;

			float amp = globalBeta * betaScale * bendScale * fieldStrength * fs.Strength;
			float mag = 0.0f;

			// DECISION: select field profile model.
			switch (fs.Profile)
			{
				case 0: // DECISION: Power profile
					mag = amp * Mathf.Pow(r, gamma);
					break;

				case 1: // DECISION: InversePower profile
					mag = amp / Mathf.Pow(r, Mathf.Max(0.0001f, gamma));
					break;

				case 2: // DECISION: Gaussian profile
				{
					float sigma = Mathf.Max(0.0001f, fs.Sigma);
					float x = r / sigma;
					mag = amp * Mathf.Exp(-x * x);
				}
				break;

				case 3: // DECISION: Shell profile
				{
					float inner = Mathf.Max(0.0f, fs.InnerRadius);
					float outer = Mathf.Max(inner + 0.0001f, fs.OuterRadius);
					float edge = Mathf.Max(0.0001f, fs.EdgeSoftness);

					float wIn = SmoothStep(inner - edge, inner + edge, r);
					float wOut = 1.0f - SmoothStep(outer - edge, outer + edge, r);
					float w = Mathf.Clamp(wIn * wOut, 0.0f, 1.0f);

					mag = amp * w * Mathf.Pow(r, gamma);
				}
				break;
			}

			aSum += dir * mag;
		}

		return aSum;
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
