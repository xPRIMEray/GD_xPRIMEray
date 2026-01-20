// ✅ Plug-n-play RayBeamRenderer.cs patch
// What this does:
// 1) Terminates the *draw trail* at first hit (even if you keep simulating)
// 2) Prevents double-stamping (no more overbright/saturation from stamping prePos twice)
// 3) Keeps your existing StopOnHit behavior intact
//
// Drop-in replace your script with this full file, or copy the marked blocks into yours.

using Godot;
using System;
using System.Collections.Generic;

public partial class RayBeamRenderer : Node3D
{
	[ExportCategory("Ray Beam Renderer")]
	[ExportGroup("References")]
	/// <summary>Optional camera override; uses viewport camera when empty.</summary>
	[Export] public NodePath CameraPath;

	[ExportGroup("Performance / Profiling")]
	/// <summary>Rebuilds rays when camera or field sources change.</summary>
	[Export] public bool UpdateEveryFrame = true;
	/// <summary>Allows Rebuild when UpdateEveryFrame is enabled.</summary>
	[Export] public bool AllowRebuild = true;

	[ExportGroup("Ray March / Sampling")]
	/// <summary>Number of integration steps per ray.</summary>
	[Export] public int StepsPerRay = 64;
	/// <summary>Base step length for integration.</summary>
	[Export] public float StepLength = 0.25f;
	/// <summary>Clamp minimum step length.</summary>
	[Export] public float MinStepLength = 0.05f;
	/// <summary>Clamp maximum step length.</summary>
	[Export] public float MaxStepLength = 0.5f;
	/// <summary>Adaptation strength for step sizing.</summary>
	[Export] public float StepAdaptGain = 0.05f;
	/// <summary>Integrates field acceleration instead of closed form.</summary>
	[Export] public bool UseIntegratedField = true;
	/// <summary>Base bend strength.</summary>
	[Export] public float BendScale = 0.12f;
	/// <summary>Extra multiplier for field strength.</summary>
	[Export] public float FieldStrength = 1.0f;
	/// <summary>World center for field when not using camera.</summary>
	[Export] public Vector3 FieldCenter = Vector3.Zero;
	/// <summary>Uses camera position as field center.</summary>
	[Export] public bool FieldCenterIsCamera = true;

	[ExportGroup("Rendering / Film Output")]
	/// <summary>Billboard size for each sample.</summary>
	[Export] public float QuadSize = 0.04f;
	/// <summary>Base alpha for ray samples.</summary>
	[Export] public float Alpha = 0.50f;
	/// <summary>Samples every N steps for drawing.</summary>
	[Export] public int RenderEveryNSteps = 1;
	/// <summary>Colors rays based on field magnitude.</summary>
	[Export] public bool ColorByField = true;
	/// <summary>Strength of field-based color ramp.</summary>
	[Export] public float FieldColorGain = 0.15f;
	/// <summary>Color for maximum field heat.</summary>
	[Export] public Color HotColor = new Color(0.2f, 1.0f, 1.0f, 1.0f);
	/// <summary>Stops drawing samples after the first hit.</summary>
	[Export] public bool TerminateTrailOnHit = true;
	/// <summary>Draws a marker at hit position.</summary>
	[Export] public bool DrawHitMarker = true;
	/// <summary>Color of the hit marker.</summary>
	[Export] public Color HitMarkerColor = new Color(1, 0, 0, 1);

	[ExportGroup("Physics / Collision")]
	/// <summary>Stops simulation on first hit.</summary>
	[Export] public bool StopOnHit = false;
	/// <summary>Collision mask for ray tests.</summary>
	[Export] public uint CollisionMask = 0xFFFFFFFF;
	/// <summary>Collision test cadence in steps.</summary>
	[Export] public int CollisionEveryNSteps = 1;
	/// <summary>Sphere radius for collision.</summary>
	[Export] public float CollisionRadius = 0.03f;
	/// <summary>Uses IntersectShape sphere sweep.</summary>
	[Export] public bool UseSphereSweepCollision = false;
	/// <summary>Rejects segments outside a plane slab.</summary>
	[Export] public bool UseInsightPlaneFilter = false;
	/// <summary>NodePath to plane source.</summary>
	[Export] public NodePath InsightPlaneNode;
	/// <summary>Segment length that triggers subdivision.</summary>
	[Export] public float CollisionRaySubdivideThreshold = 0.25f;
	/// <summary>Max sub-rays per segment.</summary>
	[Export] public int MaxCollisionSubsteps = 16;
	/// <summary>Only render rays that hit.</summary>
	[Export] public bool RequireHitToRender = false;
	/// <summary>Keeps collision checks even if StopOnHit is false.</summary>
	[Export] public bool CheckCollisionsEvenIfNotStopping = false;
	/// <summary>Adjusts collision cadence to limit screen error.</summary>
	[Export] public bool UseScreenSpaceCollisionCadence = true;
	/// <summary>Target sagitta error in pixels.</summary>
	[Export] public float CollisionMaxErrorPixels = 0.75f;
	/// <summary>Min depth for screen error calculations.</summary>
	[Export] public float MinDepthForError = 0.10f;
	/// <summary>Lower bound on adaptive collision cadence.</summary>
	[Export] public int MinCollisionEveryNSteps = 1;

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
	/// <summary>Enables per-ray debug logs.</summary>
	[Export] public bool DebugRender = false;
	/// <summary>Log every N rays during rebuild.</summary>
	[Export] public int DebugEveryNRays = 25;
	/// <summary>Logs billboard rejects (bounds or NaN).</summary>
	[Export] public bool DebugSetBillboardRejects = false;
	/// <summary>Max billboard reject logs per ray.</summary>
	[Export] public int DebugMaxRejectPrints = 10;
	/// <summary>Debug overlay mode (off/rays/normals).</summary>
	[Export] public DebugDrawMode DebugMode = DebugDrawMode.RaysAndNormals;
	/// <summary>Cap on debug overlay rays.</summary>
	[Export] public int DebugMaxRays = 256;
	/// <summary>Cap on segments per debug ray.</summary>
	[Export] public int DebugMaxSegmentsPerRay = 64;
	/// <summary>Length of debug hit normals.</summary>
	[Export] public float DebugNormalLen = 0.25f;
	/// <summary>Draw only rays that hit.</summary>
	[Export] public bool DebugDrawOnlyHits = false;
	/// <summary>Film camera drives overlay drawing when true.</summary>
	[Export] public bool DebugOverlayOwnedByFilm = true;

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
	public override async void _Ready()
	{
		// 1. Multimesh Instance
		_mm = new MultiMesh();
		_mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		_mm.UseColors = true;
		_mm.UseCustomData = false;
		_mmi = new MultiMeshInstance3D
		{
			Multimesh = _mm
		};
		// Simple quad for each sample
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


		// 2. Debug mesh setup
		_dbgImmediate = new ImmediateMesh();
		_dbgMaterial = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			NoDepthTest = true
		};
		_dbgMeshInstance = new MeshInstance3D();
		_dbgMeshInstance.Mesh = _dbgImmediate;
		_dbgMeshInstance.MaterialOverride = _dbgMaterial;
		_dbgMeshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		_dbgMeshInstance.GlobalTransform = Transform3D.Identity;
		_dbgMeshInstance.Visible = true;
		_dbgMeshInstance.Layers = 1; // default 3D layer
		_dbgMeshInstance.TopLevel = false;

		AddChild(_dbgMeshInstance);
		GetTree().CurrentScene.AddChild(_dbgMeshInstance);
		_dbgMeshInstance.Owner = Owner; // if you ever want it visible in editor ownership


		GD.Print($"[DBG] dbgMesh inTree={_dbgMeshInstance.IsInsideTree()} parent={_dbgMeshInstance.GetParent()?.Name} world={_dbgMeshInstance.GlobalTransform.Origin}");

		// 3. Await Frame
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		// 4. Rebuild
		Rebuild();
	}

	public override void _Process(double delta)
	{
		if (!UpdateEveryFrame) return;
		if (!AllowRebuild) return;

		var cam = GetCamera();
		if (cam == null) return;
		GD.Print($"[DBG] camWorld={cam.GetWorld3D()?.GetRid()} dbgWorld={_dbgMeshInstance.GetWorld3D()?.GetRid()}");

		float beta = ReadFloat(cam, "Beta", 0f);
		float gamma = ReadFloat(cam, "Gamma", 2f);
		float focal = ReadFloat(cam, "FocalLength", 0f);

		var fieldSources = GetTree().GetNodesInGroup("field_sources");
		int fieldCount = fieldSources.Count;

		bool changed = false;

		if (!Mathf.IsEqualApprox(beta, _lastBeta) || !Mathf.IsEqualApprox(gamma, _lastGamma))
			changed = true;

		if (!IsFinite(_lastCamPos) || cam.GlobalPosition.DistanceTo(_lastCamPos) > 0.001f)
			changed = true;

		if (float.IsNaN(_lastCamFocal) || !Mathf.IsEqualApprox(focal, _lastCamFocal))
			changed = true;

		if (fieldCount != _lastFieldSourceCount)
			changed = true;

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
		if (e is InputEventKey k && k.Pressed && !k.Echo)
		{
			if (k.Keycode == Key.F1)
			{
				DebugMode = DebugMode == DebugDrawMode.Off ? DebugDrawMode.RaysAndNormals : DebugDrawMode.Off;
				GD.Print($"[RayBeamRenderer] DebugMode = {DebugMode}");
			}
			if (k.Keycode == Key.F2)
			{
				DebugDrawOnlyHits = !DebugDrawOnlyHits;
				GD.Print($"[RayBeamRenderer] DebugDrawOnlyHits = {DebugDrawOnlyHits}");
			}
		}
	}

	private void RequestRebuild()
	{
		if (_rebuildInProgress)
		{
			_rebuildQueued = true;
			return;
		}
		if (_rebuildQueued) return;

		_rebuildQueued = true;
		CallDeferred(nameof(DoRebuildDeferred));
	}

	private async void DoRebuildDeferred()
	{
		_rebuildQueued = false;
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Rebuild();
	}

	private Camera3D GetCamera()
	{
		if (CameraPath != null && !CameraPath.IsEmpty)
			return GetNodeOrNull<Camera3D>(CameraPath);

		return GetViewport()?.GetCamera3D();
	}

	private void Rebuild()
	{
		GD.Print("Rebuild ENTER");
		GD.Print($"Rebuild on node: {GetPath()}  TerminateTrailOnHit={TerminateTrailOnHit} StopOnHit={StopOnHit} RequireHitToRender={RequireHitToRender}");
		GD.Print($"READY node: {GetPath()}  Script={GetScript()}  TerminateTrailOnHit={TerminateTrailOnHit}");

		if (_rebuildInProgress) return;
		_rebuildInProgress = true;

		try
		{
			var cam = GetCamera();
			if (cam == null) return;

			RefreshInsightPlane();

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

			foreach (var node in emitters)
			{
				if (node is RayEmitter3D e)
				{
					emitterList.Add(e);
					total += Math.Max(1, e.Rays) * (StepsPerRay + 1);
				}
			}

			///
			////////////////////
			int raysTotal = 0;
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

			foreach (var e in emitterList)
			{
				if (capacityExhausted) break;

				Color baseC = e.RayColor;
				float maxDist = e.MaxDistance;

				int rays = Math.Max(1, e.Rays);
				int rayOrdinal = 0;
				float spreadRad = Mathf.DegToRad(e.SpreadDegrees);

				Vector3 origin = e.GlobalTransform.Origin;

				for (int r = 0; r < rays; r++)
				{
					rayOrdinal++;

					if (rayOrdinal == 1) _dbgRejectPrints = 0;

					bool debugThisRay = DebugRender && (rayOrdinal % Mathf.Max(1, DebugEveryNRays) == 0);

					if (debugThisRay)
						GD.Print($"[DBG] Ray#{rayOrdinal} start RequireHitToRender={RequireHitToRender} StopOnHit={StopOnHit}");

					Vector3 localDir;
					if (e.UseFan)
					{
						float yawTotal = Mathf.DegToRad(e.FanYawDegrees);
						float pitch = Mathf.DegToRad(e.FanPitchDegrees);

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
					Vector2 d2n = d2.Length() > 1e-6f ? d2 / d2.Length() : Vector2.Right;
					Vector3 bendDir = (camRight * d2n.X + camUp * -d2n.Y).Normalized();

					// --- NEW PATH: simulate into shared buffers, then render from arrays ---
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

					if (meta.HadHit) hitCount++;

					if (debugThisRay && meta.HadHit)
						GD.Print($"[DBG] HIT collider='{hit.ColliderName}' id={hit.ColliderId} pos={hit.Position}");

					if (debugThisRay)
						GD.Print($"[DBG] Ray#{rayOrdinal} meta.HadHit={meta.HadHit} meta.RenderCount={meta.RenderCount}");

					_rayMeta[_rayWriteHead] = meta;
					_hitPayload[_rayWriteHead] = hit;

					if (!RequireHitToRender || meta.HadHit)
					{
						if (debugThisRay)
							GD.Print($"Ray#{rayOrdinal} hadHit={meta.HadHit} samples={meta.SampleCount} renderCount={meta.RenderCount}");

						for (int i = 0; i < meta.RenderCount; i++)
						{
							if (idx >= _mm.InstanceCount)
							{
								capacityExhausted = true;
								break;
							}

							int si = meta.SampleStart + i;
							SetBillboardInstance(idx++, capacity, _samplePos[si], camRight, camUp, camForward, _sampleCol[si]);
						}

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
			_mm.VisibleInstanceCount = Mathf.Max(0, idx); // ✅ show only the instances we wrote

			if (DebugRender)
			{
				GD.Print($"[DBG] Rebuild summary: totalTarget={total} idxWritten={idx} InstanceCount={_mm.InstanceCount} VisibleCount={_mm.VisibleInstanceCount} hits={hitCount}");
			}

			// =======================
			// Debug overlay draw (ImmediateMesh)
			// =======================
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
		if (_mm == null) return;

		if (index < 0 || index >= _mm.InstanceCount)
		{
			if (DebugRender && DebugSetBillboardRejects && _dbgRejectPrints < DebugMaxRejectPrints)
			{
				_dbgRejectPrints++;
				GD.Print($"[DBG] SetBillboard SKIP: index {index} out of range (InstanceCount={_mm.InstanceCount})");
			}
			return;
		}

		if (!IsFinite(pos))
		{
			if (DebugRender && DebugSetBillboardRejects && _dbgRejectPrints < DebugMaxRejectPrints)
			{
				_dbgRejectPrints++;
				GD.Print($"[DBG] SetBillboard SKIP: pos non-finite: {pos}");
			}
			return;
		}

		float s = QuadSize;

		var basis = new Basis(
			camRight * s,
			camUp * s,
			camForward * s
		);

		if (!IsFinite(basis.X) || !IsFinite(basis.Y) || !IsFinite(basis.Z))
		{
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

		foreach (var n in sources)
		{
			if (n is not FieldSource3D fs) continue;
			if (!fs.Enabled) continue;

			Vector3 center = fs.GlobalPosition;
			Vector3 rvec = p - center;

			float rRaw = rvec.Length();
			float soft = Mathf.Max(0.00001f, fs.Softening);
			float r = Mathf.Sqrt(rRaw * rRaw + soft * soft);

			if (fs.MinRadius > 0.0f && r < fs.MinRadius) continue;
			if (fs.MaxRadius > 0.0f && r > fs.MaxRadius) continue;

			Vector3 dir = (-rvec / r);
			if (!fs.Attract) dir = -dir;

			float gamma = fs.OverrideGamma ? fs.Gamma : globalGamma;
			float betaScale = fs.OverrideBetaScale ? fs.BetaScale : 1.0f;

			float amp = globalBeta * betaScale * BendScale * FieldStrength * fs.Strength;
			float mag = 0.0f;

			switch (fs.Profile)
			{
				case FieldSource3D.ProfileType.Power:
					mag = amp * Mathf.Pow(r, gamma);
					break;

				case FieldSource3D.ProfileType.InversePower:
					mag = amp / Mathf.Pow(r, Mathf.Max(0.0001f, gamma));
					break;

				case FieldSource3D.ProfileType.Gaussian:
					{
						float sigma = Mathf.Max(0.0001f, fs.Sigma);
						float x = r / sigma;
						mag = amp * Mathf.Exp(-x * x);
					}
					break;

				case FieldSource3D.ProfileType.Shell:
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
		float t = Mathf.Clamp((x - a) / (b - a), 0.0f, 1.0f);
		return t * t * (3.0f - 2.0f * t);
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
		if (Mathf.Abs(dp) <= slab || Mathf.Abs(dq) <= slab)
			return true;

		return (dp > 0f) != (dq > 0f);
	}

	private void RefreshInsightPlane()
	{
		_hasInsightPlane = false;
		if (InsightPlaneNode == null || InsightPlaneNode.IsEmpty) return;

		var n = GetNodeOrNull<Node3D>(InsightPlaneNode);
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
		if (res == null || res.Length < 2) return false;

		float unsafeFrac = res[1];
		if (!float.IsFinite(unsafeFrac)) return false;

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
		if (len <= 1e-6f) return false;

		int steps = Mathf.Clamp(maxSubsteps, 1, 64);
		Vector3 prev = a;

		for (int i = 1; i <= steps; i++)
		{
			float t = (float)i / steps;
			Vector3 cur = a + d * t;

			var rq = PhysicsRayQueryParameters3D.Create(prev, cur, mask);
			rq.CollideWithBodies = true;
			rq.CollideWithAreas = true;
			rq.HitFromInside = false;	// formerly true

			var hit = space.IntersectRay(rq);
			if (hit.Count > 0)
			{
				hitPos = (Vector3)hit["position"];
				colliderId = (ulong)hit["collider_id"];
				var colliderObj = hit["collider"].AsGodotObject();
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
		if (len <= 1e-6f) return false;

		int steps = Mathf.Clamp(maxSubsteps, 1, 64);
		Vector3 prev = a;

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
			if (hit.Count > 0)
			{
				hitPos = (Vector3)hit["position"];
				if (hit.TryGetValue("normal", out var nObj))
					hitNormal = ((Vector3)nObj).Normalized();

				colliderId = (ulong)hit["collider_id"];
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


	private static Vector3 SafeNormalized(Vector3 v, Vector3 fallback)
	{
		float len = v.Length();
		if (!float.IsFinite(len) || len < 1e-8f) return fallback;
		return v / len;
	}

	private static bool IsFinite(Vector3 v)
	{
		return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
	}

	private void EnsureCapacity(int raysTotal, int samplesTotal)
	{
		if (_rayMeta.Length < raysTotal) Array.Resize(ref _rayMeta, raysTotal);
		if (_hitPayload.Length < raysTotal) Array.Resize(ref _hitPayload, raysTotal); // 1 payload per ray for now

		if (_samplePos.Length < samplesTotal) Array.Resize(ref _samplePos, samplesTotal);
		if (_sampleCol.Length < samplesTotal) Array.Resize(ref _sampleCol, samplesTotal);

		if (_dbgRayOffsets.Length < raysTotal) Array.Resize(ref _dbgRayOffsets, raysTotal);
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

		int every = Mathf.Max(1, RenderEveryNSteps);

		//////////////  SCREEN-SPACE COLLISION CADENCE (SIMULATE)  //////////////
		int ceBase = Mathf.Max(1, CollisionEveryNSteps);
		int ceCurrent = ceBase;              // ✅ must persist across loop
		int stepsSinceCollision = 0;

		var cam = GetCamera();
		float pxPerRad = (UseScreenSpaceCollisionCadence && cam != null) ? GetPixelsPerRadian(cam) : 0f;
		Vector3 camPos = (cam != null) ? cam.GlobalPosition : Vector3.Zero;
		/////////////////////////////////////////////////////////////////////////

		float minStep = Mathf.Min(MinStepLength, MaxStepLength);
		float maxStep = Mathf.Max(MinStepLength, MaxStepLength);
		minStep = Mathf.Max(0.0001f, minStep);

		float hitDist = 0.0f;

		for (int s = 0; s <= StepsPerRay; s++)
		{
			Vector3 a = Vector3.Zero;
			Vector3 next = p;

			if (UseIntegratedField)
			{
				if (hasSources)
					a = ComputeAccelerationAtPoint(p, fieldSources, beta, gamma);
				else
				{
					Vector3 rvec = p - center;
					float rr = Mathf.Max(0.001f, rvec.Length());
					a = (-rvec / rr) * (beta * Mathf.Pow(rr, gamma) * BendScale * FieldStrength);
				}

				float aLen = a.Length();

				if (!float.IsFinite(aLen))
				{
					a = Vector3.Zero;
					aLen = 0.0f;
				}
				else if (aLen > 50.0f)
				{
					a = a * (50.0f / aLen);
					aLen = 50.0f;
				}

				float step = Mathf.Clamp(StepLength / (1.0f + aLen * StepAdaptGain), minStep, maxStep);
				v = SafeNormalized(v + a * step, v);
				next = p + v * step;

				float remaining = e.MaxDistance - traveled;
				if (step > remaining) step = remaining;

				///////////////////////////
				///////////////////////////
				// Update collision cadence for this step (screen-space error model)
				ceCurrent = ceBase;

				if (UseScreenSpaceCollisionCadence && cam != null)
				{
					float aPerp = PerpAccelLen(a, v); // v normalized after SafeNormalized
					float depth = (p - camPos).Length();
					ceCurrent = ComputeCeFromScreenError(ceBase, step, aPerp, depth, pxPerRad, CollisionMaxErrorPixels);
				}
				///////////////////////////
				///////////////////////////

				traveled += step;
				if (traveled > e.MaxDistance)
					break;
			}
			else
			{
				float t = s * StepLength;
				float bend = beta * Mathf.Pow(t, gamma) * BendScale;
				next = origin + dir * t + bendDir * bend;
			}

			float step01 = (StepsPerRay <= 0) ? 0f : (float)s / StepsPerRay;
			float fade = 1.0f - step01;
			fade *= fade;
			float alpha = Alpha * e.Intensity * fade;

			Color c = e.RayColor;
			if (ColorByField)
			{
				float heat = Mathf.Clamp(a.Length() * FieldColorGain, 0f, 1f);
				c = c.Lerp(HotColor, heat);
			}
			c.A = Mathf.Clamp(alpha, 0.0f, 1.0f);

			// Store samples (array-backed)
			if ((s % every) == 0)
			{
				if (!TerminateTrailOnHit || sampleCount < trailStopCount)
				{
					int wi = sampleStart + sampleCount;
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
			if ((StopOnHit || CheckCollisionsEvenIfNotStopping) && s > 0 && stepsSinceCollision >= ceCurrent)
			{
				stepsSinceCollision = 0;
				Vector3 segA = p;
				Vector3 segB = next;
				float segLen = (segB - segA).Length();

				bool allowCollision = true;

				if (UseInsightPlaneFilter && _hasInsightPlane)
				{
					if (!SegmentCrossesPlane(segA, segB, _insightPlane, CollisionRadius))
						allowCollision = false;
				}

				if (allowCollision)
				{
					bool didHit = false;
					Vector3 hp = Vector3.Zero;
					ulong cid = 0;
					string cname = "<none>";

					if (segLen > 1e-6f)
					{
						if (UseSphereSweepCollision)
							didHit = SweepSegmentHit(space, segA, segB, collisionMask, CollisionRadius, out hp);
						else
						{
							int sub = 1;
							if (segLen > CollisionRaySubdivideThreshold)
								sub = Mathf.CeilToInt(segLen / CollisionRaySubdivideThreshold);
							sub = Mathf.Clamp(sub, 1, MaxCollisionSubsteps);

							//didHit = SubdividedRayHit(space, segA, segB, collisionMask, sub, out hp, out cid, out cname);
							Vector3 hn;
							didHit = SubdividedRayHit(space, segA, segB, collisionMask, sub, out hp, out hn, out cid, out cname);
							if (didHit)
							{
								hitColliderId = cid;
								hitColliderName = cname;
								// store normal:
								// (stash hn somewhere for hitOut later)
							}
						}
					}

					if (didHit && !rayHit)
					{
						rayHit = true;
						hadHit = true;
						hitPos = hp;

						hitColliderId = cid;
						hitColliderName = cname;
						
						hitDist = traveled - segLen + (hitPos - segA).Length();

						if (TerminateTrailOnHit)
							trailStopCount = sampleCount;

						if (StopOnHit)
							break;
					}
				}
			}
			p = next;
		}

		// Update write head
		_sampleWriteHead = sampleStart + sampleCount;

		int renderCount = Mathf.Min(sampleCount, Mathf.Max(0, trailStopCount));

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

		int ce = Mathf.Max(1, CollisionEveryNSteps);

		float minStep = Mathf.Min(MinStepLength, MaxStepLength);
		float maxStep = Mathf.Max(MinStepLength, MaxStepLength);
		minStep = Mathf.Max(0.0001f, minStep);

		for (int s = 0; s <= StepsPerRay; s++)
		{
			Vector3 next = p;

			if (UseIntegratedField)
			{
				Vector3 a = Vector3.Zero;

				if (hasSources)
					a = ComputeAccelerationAtPoint(p, fieldSources, beta, gamma);
				else
				{
					Vector3 rvec = p - center;
					float rr = Mathf.Max(0.001f, rvec.Length());
					a = (-rvec / rr) * (beta * Mathf.Pow(rr, gamma) * BendScale * FieldStrength);
				}

				float aLen = a.Length();
				if (!float.IsFinite(aLen)) { a = Vector3.Zero; aLen = 0f; }
				else if (aLen > 50f) { a = a * (50f / aLen); aLen = 50f; }

				float step = Mathf.Clamp(StepLength / (1.0f + aLen * StepAdaptGain), minStep, maxStep);

				v = SafeNormalized(v + a * step, v);
				next = p + v * step;

				//float segLenStep = (next - p).Length();
				//traveled += segLenStep;
				traveled += step;

				if (traveled > maxDistance) break;
			}
			else
			{
				float t = s * StepLength;
				float bend = beta * Mathf.Pow(t, gamma) * BendScale;
				next = origin + dir * t + bendDir * bend;

				traveled = t;
				if (traveled > maxDistance) break;
			}

			// Collision every N steps (ALWAYS for film)
			if (s > 0 && (s % ce) == 0)
			{
				Vector3 segA = p;
				Vector3 segB = next;
				float segLen = (segB - segA).Length();

				bool allowCollision = true;
				if (UseInsightPlaneFilter && _hasInsightPlane)
				{
					if (!SegmentCrossesPlane(segA, segB, _insightPlane, CollisionRadius))
						allowCollision = false;
				}

				if (allowCollision && segLen > 1e-6f)
				{
					Vector3 hp;
					ulong cid;
					string cname;

					bool didHit = false;

					if (UseSphereSweepCollision)
					{
						didHit = SweepSegmentHit(space, segA, segB, collisionMask, CollisionRadius, out hp);
						// sphere sweep doesn't return collider id/name (ok for v0)
					}
					else
					{
						int sub = 1;
						if (segLen > CollisionRaySubdivideThreshold)
							sub = Mathf.CeilToInt(segLen / CollisionRaySubdivideThreshold);
						sub = Mathf.Clamp(sub, 1, MaxCollisionSubsteps);

						didHit = SubdividedRayHit(space, segA, segB, collisionMask, sub, out hp, out cid, out cname);
						if (didHit)
						{
							colliderId = cid;
							colliderName = cname;
						}
					}

					if (didHit)
					{
						hadHit = true;
						hitPos = hp;

						// more accurate path-length to hit within the segment
						hitDistance = traveled - segLen + (hitPos - segA).Length();
						break; // film wants first hit
					}
				}
			}

			p = next;
		}

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
		int every = Mathf.Max(1, RenderEveryNSteps);
		// steps include s=0..StepsPerRay inclusive → StepsPerRay+1 "step indices"
		int samples = (StepsPerRay / every) + 2; // conservative pad
		if (DrawHitMarker) samples += 1;
		return Mathf.Max(4, samples);
	}

	// Build segments at collision cadence (ce). No physics calls here.
	public int BuildRaySegmentsCamera(
		Vector3 origin, Vector3 dir, Vector3 bendDir,
		Vector3 center, float beta, float gamma,
		FieldSourceSnap[] fieldSnaps, bool hasSources,
		float maxDistance,
		RaySeg[] outSegs, int outOffset, int outCapacity,
		Plane insightPlane, bool useInsightPlane, float insightEps)
	{
		/// 
		/////////////////////////
		Vector3 p = origin;
		Vector3 v = dir; // assumed normalized

		float traveled = 0f;

		int ceBase = Mathf.Max(1, CollisionEveryNSteps);
		int ce = ceBase;
		int stepsSinceEmit = 0;

		// camera data for screen-space cadence
		Camera3D cam = GetCamera();
		float pxPerRad = (UseScreenSpaceCollisionCadence && cam != null) ? GetPixelsPerRadian(cam) : 0f;
		Vector3 camPos = (cam != null) ? cam.GlobalPosition : Vector3.Zero;

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


		for (int s = 0; s <= StepsPerRay; s++)
		{
			Vector3 next = p;

			if (UseIntegratedField)
			{
				// Early-out if we are already at/over max distance
				float remaining = maxDistance - traveled;
				if (remaining <= 0f) break;

				Vector3 a;

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
				if (!float.IsFinite(aLen)) { a = Vector3.Zero; aLen = 0f; }
				else if (aLen > 50f) { a *= (50f / aLen); aLen = 50f; }

				// Compute step FIRST
				float step = stepLength / (1.0f + aLen * stepAdaptGain);
				step = Mathf.Clamp(step, minStep, maxStep);

				// Clamp to remaining distance so we don't overshoot maxDistance
				if (step > remaining) step = remaining;

				//////////////////////////////
				/// ////////////
				/// /////////
				// Update segment cadence AFTER step exists
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
				float t = s * stepLength;
				if (t > maxDistance) break;

				float bend = beta * Mathf.Pow(t, gamma) * bendScale;
				next = origin + dir * t + bendDir * bend;
				traveled = t;
			}

			// Only emit segments at adaptive cadence
			stepsSinceEmit++;
			if (s > 0 && stepsSinceEmit >= ce)
			{
				stepsSinceEmit = 0;
				if (useInsightPlane && !SegmentCrossesPlane(p, next, insightPlane, insightEps))
				{
					p = next;
					continue;
				}

				if (written >= outCapacity) break;

				outSegs[outOffset + written] = new RaySeg
				{
					A = p,
					B = next,
					TraveledB = traveled
				};
				written++;
			}

			p = next;
		}

		return written;
	}

	public FieldSourceSnap[] SnapshotFieldSources(Godot.Collections.Array<Node> nodes)
	{
		if (nodes == null || nodes.Count == 0) return Array.Empty<FieldSourceSnap>();

		var list = new List<FieldSourceSnap>(nodes.Count);

		foreach (var n in nodes)
		{
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

		for (int i = 0; i < sources.Length; i++)
		{
			ref readonly var fs = ref sources[i];
			if (!fs.Enabled) continue;

			Vector3 rvec = p - fs.Center;

			float rRaw = rvec.Length();
			float soft = Mathf.Max(0.00001f, fs.Softening);
			float r = Mathf.Sqrt(rRaw * rRaw + soft * soft);

			if (fs.MinRadius > 0.0f && r < fs.MinRadius) continue;
			if (fs.MaxRadius > 0.0f && r > fs.MaxRadius) continue;

			Vector3 dir = (-rvec / r);
			if (!fs.Attract) dir = -dir;

			float gamma = fs.OverrideGamma ? fs.Gamma : globalGamma;
			float betaScale = fs.OverrideBetaScale ? fs.BetaScale : 1.0f;

			float amp = globalBeta * betaScale * bendScale * fieldStrength * fs.Strength;
			float mag = 0.0f;

			switch (fs.Profile)
			{
				case 0: // Power
					mag = amp * Mathf.Pow(r, gamma);
					break;

				case 1: // InversePower
					mag = amp / Mathf.Pow(r, Mathf.Max(0.0001f, gamma));
					break;

				case 2: // Gaussian
				{
					float sigma = Mathf.Max(0.0001f, fs.Sigma);
					float x = r / sigma;
					mag = amp * Mathf.Exp(-x * x);
				}
				break;

				case 3: // Shell
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
		if (cam == null) return 1000f;

		// vertical FOV in radians (Godot 4 uses Degrees for Fov)
		float fovY = Mathf.DegToRad(cam.Fov);

		// viewport height in pixels
		var vp = cam.GetViewport();
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
		if (aPerpLen <= 1e-8f) return ceBase;

		depth = Mathf.Max(MinDepthForError, depth);
		pxPerRad = Mathf.Max(1e-3f, pxPerRad);
		maxErrPx = Mathf.Max(1e-3f, maxErrPx);

		// LsegMax = sqrt( 2*maxErrPx*depth / (aPerpLen*pxPerRad) )
		float LsegMax = Mathf.Sqrt((2f * maxErrPx * depth) / (aPerpLen * pxPerRad));

		int ce = (int)Mathf.Floor(LsegMax / Mathf.Max(1e-6f, stepLen));
		ce = Mathf.Clamp(ce, MinCollisionEveryNSteps, ceBase);
		return ce;
	}

	// =======================
	// Debug Overlay Builder (uses _rayMeta + _samplePos + _hitPayload)
	// =======================
	private void UpdateDebugOverlay(Camera3D cam, int raysWritten)
	{
		GD.Print($"[DBG] Overlay call: mode={DebugMode} raysWritten={raysWritten} dbgMeshNull={_dbgMeshInstance==null}");

		// --- CAN'T-MISS sanity line (1m in front of camera) ---
		var camXform = cam.GlobalTransform;
		Vector3 p0 = camXform.Origin + (-camXform.Basis.Z) * 0.5f; // forward in Godot is -Z
		Vector3 p1 = camXform.Origin + (-camXform.Basis.Z) * 2.5f;
		DbgAddLine(p0, p1, new Color(1, 0, 0, 1));

		if (Engine.GetFramesDrawn() % 60 == 0)
			GD.Print($"[DBG] UpdateDebugOverlay called. mode={DebugMode}");


		if (_dbgMeshInstance == null || _dbgImmediate == null) return;

		// If debug is off, hide and clear.
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
		int rayLimit = Mathf.Min(DebugMaxRays, raysWritten);

		for (int r = 0; r < rayLimit; r++)
		{
			ref RayMeta meta = ref _rayMeta[r];

			if (DebugDrawOnlyHits && !meta.HadHit)
				continue;

			int count = Mathf.Max(0, meta.RenderCount); // RenderCount already respects TerminateTrailOnHit
			if (count < 2)
				continue;

			int segMax = Mathf.Min(DebugMaxSegmentsPerRay, count - 1);

			int start = meta.SampleStart;

			// --- Draw ray polyline from samples ---
			for (int i = 0; i < segMax; i++)
			{
				Vector3 a = _samplePos[start + i];
				Vector3 b = _samplePos[start + i + 1];

				// Fade alpha along the ray for readability
				float t = segMax <= 1 ? 1f : (float)i / (segMax - 1);
				Color c = new Color(1f, 1f, 1f, 0.25f + 0.75f * t);

				DbgAddLine(a, b, c);
			}

			// --- Draw hit normal as RGB (abs normal) ---
			if (DebugMode == DebugDrawMode.RaysAndNormals && meta.HadHit)
			{
				HitPayload hp = _hitPayload[r];
				if (hp.Valid)
				{
					Vector3 p = hp.Position;
					Vector3 n = hp.Normal;

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
		if (_dbgLinePoints.Count < 2) return;

		_dbgImmediate.SurfaceBegin(Mesh.PrimitiveType.Lines, _dbgMaterial);

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
		//GD.Print($"[DBG] Entry to UpdateDebugOverlayFromFile");

		// Fast bail-outs
		if (DebugMode == DebugDrawMode.Off)
		{
			if (_dbgMeshInstance != null) _dbgMeshInstance.Visible = false;
			return;
		}

		if (_dbgImmediate == null || _dbgMeshInstance == null)
		{
			GD.Print($"[DBG] _dbgImmediate or _dbgMeshInstance is null");
			return;
		}

		//GD.Print($"[DBG] Inside UpdateDebugOverlayFromFile. cam = {cam}");
		if (cam == null)
		{
			GD.Print($"[DBG] Cam is Null");
			return;
		}

		_dbgMeshInstance.Visible = true;

		// Optional: keep overlay "always visible"
		// (NoDepthTest already handles depth; these help order if needed)
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
		for (int r = 0; r < rayCount; r += stride)
		{
			int start = rayOffsets[r];
			int count = rayCounts[r];
			//GD.Print($"[DBG] ray r={r} / start = {start} / count = {count}");

			if (start < 0 || count < 2) continue;
			if (start + count > rayPointsWorld.Length) continue;
			// Need at least 2 points to draw a polyline
			if (count < 2)
				continue;

			// Pull hit (if provided)
			bool hadHit = false;
			Vector3 hitPos = default;
			Vector3 hitNrm = default;

			if (r < hits.Length)
			{
				hadHit = HitIsValid(hits[r]);
				//GD.Print($"[DBG] ray r={r} / hadHit= {hadHit}");

				if (hadHit)
				{
					hitPos = hits[r].Position;
					hitNrm = hits[r].Normal;
					//GD.Print($"[DBG] ray r={r} / hitPos= {hitPos} / hitNrm= {hitNrm}");
				}
			}

			if (DebugDrawOnlyHits && !hadHit)
				continue;

			// Ray color
			// - green for non-hit rays, yellow for hit rays (easy to read)
			Color rayC = hadHit ? new Color(1f, 1f, 0f, 1f) : new Color(0f, 1f, 0f, 1f);

			// Draw segments
			for (int i = 0; i < count - 1; i++)
			{
				Vector3 a = rayPointsWorld[start + i];
				Vector3 b = rayPointsWorld[start + i + 1];
				//GD.Print($"[DBG] Addline a={a} / b= {b} / rayC= {rayC}");
				AddLine(a, b, rayC);
			}

			//GD.Print($"[DBG] hitNrm.LengthSquared={hitNrm.LengthSquared()}");
			// Draw hit normal (red)
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
		// Build offsets/counts from _rayMeta (no allocations if arrays already sized)
		int rayCount = _rayWriteHead;

		// Clamp to capacity to be extra safe
		rayCount = Mathf.Clamp(rayCount, 0, _rayMeta.Length);

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
