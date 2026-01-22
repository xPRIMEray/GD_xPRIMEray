using Godot;
using System;
using System.Diagnostics;
using System.Threading;
using XPrimeRay.Perf; // adjust namespace new PerfScope.cs

public partial class GrinFilmCamera : Node
{
	[ExportCategory("Film Camera")]
	[ExportGroup("References")]
	/// <summary>NodePath to the RayBeamRenderer used for film segment generation.</summary>
	[Export] public NodePath RayBeamRendererPath;
	/// <summary>Optional TextureRect used to display the film texture.</summary>
	[Export] public NodePath FilmViewPath;
	/// <summary>Optional FilmOverlay2D for debug ray overlay.</summary>
	[Export] public NodePath FilmOverlayPath;

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
	/// <summary>Runs RenderStep every frame when enabled.</summary>
	[Export] public bool UpdateEveryFrame = true;
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

	[ExportGroup("Rendering / Film Output")]
	/// <summary>Number of film rows rendered per frame.</summary>
	[Export] public int RowsPerFrame = 8;
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
	[Export] public bool UseInsightPlanePass2 = true;
	/// <summary>Legacy insight plane slab thickness (no effect).</summary>
	[Obsolete("Deprecated: no effect in current film pass.")]
	[Export] public float InsightPlaneEps = 0.10f;
	/// <summary>Placeholder for future normal smoothing (unused).</summary>
	[Obsolete("Deprecated: reserved for future normal smoothing.")]
	[Export] public bool UseSmoothNormals = false;


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

	private struct Pass1HitInfo
	{
		public bool Found;
		public float Distance;
		public Vector3 Position;
		public Vector3 Normal;
		public ulong ColliderId;
	}

	private sealed class Pass1ThreadLocal
	{
		public PhysicsRayQueryParameters3D QuickRayParams;
		public long PhysQueries;
		public long EarlyStopPixels;
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
		ulong t0 = Time.GetTicksUsec();
		bool statsEnabled = EnableProfiling || VerbosePerfLogs;
		bool framePerfEnabled = EnableFramePerf;
		bool frameStart = _rowCursor == 0;
		PerfScope frameScope = default;
		if (framePerfEnabled) frameScope = new PerfScope(_framePerf, PerfStage.FrameTotal);

		try
		{
			bool resizedFilm = EnsureFilmImageSize();
			int filmW = _filmWidth;
			int filmH = _filmHeight;
			int stride = Mathf.Clamp(PixelStride, 1, 8);

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
					_perfFrame.EffectiveRenderPixels = (filmW * filmH) / Math.Max(1, stride * stride);
				}
				if (framePerfEnabled)
				{
					_framePerf.Reset();
					_framePerf.FrameIndex = _frameIndex;
				}
			}
			if (statsEnabled && resizedFilm)
			{
				_perfFrame.ResizedFilm = true;
			}
			if (VerbosePerfLogs && (_rowCursor % filmH) == 0)
				GD.Print($"Film RenderStep running. rowCursor={_rowCursor} cam={(_cam != null ? _cam.GetPath() : "<null>")}");

			if (_rbr == null || _cam == null) return;
			if (frameStart) MaybePrintToggleSnapshot();

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
		int rowsPerFrame = Mathf.Clamp(RowsPerFrame, 1, filmH);
		int yEnd = Mathf.Min(filmH, _rowCursor + rowsPerFrame);
		int bandH = yEnd - yStart;
		int pixelCount = bandH * filmW;

		EnsureDepthHistory();
		float frameMaxHit = 0f; // track deepest hit this RenderStep band

		int bandHits = 0;
		int maxSeg = MaxSegPerRay;
		float farForSim = AutoRangeDepth ? _rangeFar : MaxDistance;
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

		//////////////
		///  Debug code block drop
		/////////////////////////////
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
		/////////////////////////
		/// 


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

		PerfScope pass1Scope = default;
		if (framePerfEnabled) pass1Scope = new PerfScope(_framePerf, PerfStage.Pass1_Integrate);

		bool pass1StopOnHit = _rbr.StopOnHit || _rbr.TerminateTrailOnHit || _rbr.RequireHitToRender;
		long pass1PhysQueries = 0;
		long pass1EarlyStopPixels = 0;

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

				Pass1HitInfo hitInfo = new Pass1HitInfo
				{
					Found = false,
					Distance = float.PositiveInfinity,
					Position = Vector3.Zero,
					Normal = Vector3.Up,
					ColliderId = 0
				};
				bool stoppedEarly = false;
				int hitSegIndex = -1;

				RayBeamRenderer.SegmentCallback onSegment = (in RayBeamRenderer.RaySeg seg, int segIndex) =>
				{
					local.QuickRayParams.From = seg.A;
					local.QuickRayParams.To = seg.B;
					var hit0 = space.IntersectRay(local.QuickRayParams);
					local.PhysQueries++;
					if (hit0.Count == 0)
						return false;

					Vector3 hp = (Vector3)hit0["position"];
					Vector3 hn = (Vector3)hit0["normal"];
					float segLen = (seg.B - seg.A).Length();
					float d = seg.TraveledB - segLen + (hp - seg.A).Length();
					if (!hitInfo.Found || d < hitInfo.Distance)
					{
						hitInfo.Found = true;
						hitInfo.Distance = d;
						hitInfo.Position = hp;
						hitInfo.Normal = hn;
						if (hit0.ContainsKey("collider_id"))
							hitInfo.ColliderId = (ulong)(long)hit0["collider_id"];
					}
					if (hitSegIndex < 0)
						hitSegIndex = segIndex;

					if (pass1StopOnHit)
					{
						stoppedEarly = true;
						return true;
					}

					return false;
				};

				int count = _rbr.BuildRaySegmentsCamera(
					camPos, dirWorld, bendDir,
					center, beta, gamma,
					fieldSnaps, hasSources,
					farForSim,
					_segBuf, segOffset, maxSeg,
					insightPlane, useInsightPlane, insightEps,
					onSegment
				);

				_segCountPerPixel[pi] = count;
				_pass1HitFound[pi] = hitInfo.Found;
				_pass1StoppedEarly[pi] = stoppedEarly;
				_pass1HitSegIndex[pi] = hitSegIndex;
				_pass1HitDist[pi] = hitInfo.Distance;
				_pass1HitPos[pi] = hitInfo.Position;
				_pass1HitNormal[pi] = hitInfo.Normal;
				_pass1HitColliderId[pi] = hitInfo.ColliderId;
				if (stoppedEarly) local.EarlyStopPixels++;

				return local;
			},
			local =>
			{
				Interlocked.Add(ref pass1PhysQueries, local.PhysQueries);
				Interlocked.Add(ref pass1EarlyStopPixels, local.EarlyStopPixels);
			});

		if (framePerfEnabled) pass1Scope.Dispose();

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
		}

		// ---- PASS 2 (main thread): collisions + shading ----
		bandHits = 0;
		int bandTracedPixels = 0;
		long shadeUsecAccum = 0;
		long bandSegsIntegrated = 0;
		long bandSegsTested = 0;
		long bandPhysicsQueries = 0;
		int bandFilledPixels = 0;
		// Pass-2 stride counters track expensive subdivided tests, not whole segments.
		long subRaysSkippedByPass2Stride = 0;
		long subRaysForcedByPass2Stride = 0;
		long pass2StrideSum = 0;
		long pass2StrideCount = 0;
		long bandFarEarlyOuts = 0;

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
			_quickRayParams.HitFromInside = Pass2HitFromInside;
			_quickRayParams.HitBackFaces = Pass2HitBackFaces;
		}

		PerfScope pass2Scope = default;
		if (framePerfEnabled) pass2Scope = new PerfScope(_framePerf, PerfStage.Pass2_Subdivide);
		bool shadeTimingEnabled = statsEnabled || framePerfEnabled;

		if (skipBandPhysics)
		{
			ulong shadeStart = 0;
			if (shadeTimingEnabled) shadeStart = Time.GetTicksUsec();

			int yAlignedStart = yStart + ((stride - (yStart % stride)) % stride);
			for (int y = yAlignedStart; y < yEnd; y += stride)
			{
				int localY = y - yStart;
				for (int x = 0; x < filmW; x += stride)
				{
					if (statsEnabled)
					{
						int pi = localY * filmW + x;
						_perfFrame.Segs += _segCountPerPixel[pi];
						if (_rbr != null && _rbr.RequireHitToRender) _perfFrame.ShadingSkippedPixels++;
						_perfFrame.TracedPixels++;
					}
					if (framePerfEnabled)
					{
						int pi = localY * filmW + x;
						bandSegsIntegrated += _segCountPerPixel[pi];
					}
					bandTracedPixels++;
					int filled = FillPixelBlock(x, y, stride, SkyColor, filmW, filmH);
					if (statsEnabled) _perfFrame.FilledPixels += filled;
					if (framePerfEnabled) bandFilledPixels += filled;
				}
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
				int localY = y - yStart;
				for (int x = 0; x < filmW; x += stride)
				{
					int pi = localY * filmW + x;
					if (statsEnabled) _perfFrame.TracedPixels++;
					bandTracedPixels++;

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
					if (framePerfEnabled) bandSegsIntegrated += segCount;

					bool isCenterSample = (x == filmW / 2 && y == (yStart + (bandH / 2)));
					bool logCenterSample = VerbosePerfLogs && isCenterSample;
					bool needHitName = NeedColliderNames || logCenterSample;
					bool testedAnyInPass0ThisPixel = false;
					bool skippedAnyByStrideThisPixel = false;
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
						if (pass1StoppedEarly && pass == 1)
							break;
						if (forceStride1 && !pass1StoppedEarly)
						{
							if (hadHit)
								break;
							if (!UsePass2CollisionStride || !skippedAnyByStrideThisPixel || testedAnyInPass0ThisPixel)
								break;
							if (statsEnabled) _perfFrame.Pass2ForceStride1Pixels++;
						}

						for (int si = segStart; si <= segEnd; si++)
						{
							var seg = _segBuf[segOffset + si];
							Vector3 segA = seg.A;
							Vector3 segB = seg.B;
							float segLen = (segB - segA).Length();
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
									break;
								}
							}

							/////////////////////////////////
							bool segCounted = false;
							ulong cid = 0;
							string cname = "<none>";
							Vector3 hp = Vector3.Zero;
							Vector3 hn = Vector3.Up; // hit normal (world-space collider)
							bool didHit = false;
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
									if (framePerfEnabled) bandSegsTested++;
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
										continue;
								}


								// Decision B
								// ---- PASS2 cheap reject 2: optional overlap ----
								if (useOverlap)
								{
									Vector3 mid = (segA + segB) * 0.5f;

									_overlapQuery.Transform = new Transform3D(Basis.Identity, mid);
									var overlaps = space.IntersectShape(_overlapQuery, BroadphaseMaxResults);
									if (statsEnabled) _perfFrame.IntersectShapeCalls++;
									if (framePerfEnabled) bandPhysicsQueries++;
									if ((statsEnabled || framePerfEnabled) && !segCounted)
									{
										if (statsEnabled) _perfFrame.SegsTested++;
										if (framePerfEnabled) bandSegsTested++;
										segCounted = true;
									}
									if (overlaps.Count == 0)
										continue;
								}

								// Decision C
								// ---- PASS2 cheap reject 1: quick ray probe ----
								if (useQuickRay)
								{
									_quickRayParams.From = segA;
									_quickRayParams.To = segB;
									var hit0 = space.IntersectRay(_quickRayParams);
									if (statsEnabled) _perfFrame.IntersectRayCalls++;
									if (framePerfEnabled) bandPhysicsQueries++;
									if ((statsEnabled || framePerfEnabled) && !segCounted)
									{
										if (statsEnabled) _perfFrame.SegsTested++;
										if (framePerfEnabled) bandSegsTested++;
										segCounted = true;
									}
									if (hit0.Count == 0)
									{
										if (UseSingleProbeThenSubdivide)
											_perfFrame.SubdividedRaySkipped++;
										continue;
									}
									Vector3 hitPos = (Vector3)hit0["position"];
									float d = seg.TraveledB - segLen + (hitPos - segA).Length();
									if (d < bestHitDistAlongRay)
										bestHitDistAlongRay = d;
								}

								if (UseSingleProbeThenSubdivide && !useQuickRay)
								{
									_quickRayParams.From = segA;
									_quickRayParams.To = segB;
									var hit0 = space.IntersectRay(_quickRayParams);
									if (statsEnabled) _perfFrame.IntersectRayCalls++;
									if (framePerfEnabled) bandPhysicsQueries++;
									if ((statsEnabled || framePerfEnabled) && !segCounted)
									{
										if (statsEnabled) _perfFrame.SegsTested++;
										if (framePerfEnabled) bandSegsTested++;
										segCounted = true;
									}
									if (hit0.Count == 0)
									{
										_perfFrame.SubdividedRaySkipped++;
										continue;
									}
									Vector3 hitPos = (Vector3)hit0["position"];
									float d = seg.TraveledB - segLen + (hitPos - segA).Length();
									if (d < bestHitDistAlongRay)
										bestHitDistAlongRay = d;
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
										continue;
									}
								}
								if (!forceStride1)
								{
									testedAnyInPass0ThisPixel = true;
									pass2StrideSum += pass2Stride;
									pass2StrideCount++;
								}

								// ---- accurate subdivided ray ----
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
										hitBackFaces: Pass2HitBackFaces,
										hitFromInside: Pass2HitFromInside);
								if (statsEnabled)
								{
									_perfFrame.SubdividedRayCalls++;
									_perfFrame.SubdividedRayQueries += rayQueries;
									_perfFrame.SubdividedRaySubsteps += sub;
								}
								if (framePerfEnabled) bandPhysicsQueries += rayQueries;
								if ((statsEnabled || framePerfEnabled) && !segCounted)
								{
									if (statsEnabled) _perfFrame.SegsTested++;
									if (framePerfEnabled) bandSegsTested++;
									segCounted = true;
								}
								
								if (didHit && needHitName)
									hitName = cname;
							}

							////////////
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
									if (EarlyOutDistanceEps > 0f && bestHit <= EarlyOutDistanceEps)
										break;
									continue;
								}
								
								// Otherwise, first hit wins
								break;
							}
							//////////////////
						}
						if (earlyOutFarThisPixel)
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
		ulong b1 = Time.GetTicksUsec(); // after PASS 2
		if (statsEnabled) _perfFrame.Hits += bandHits;
		if (framePerfEnabled)
		{
			_framePerf.RaysTraced += bandTracedPixels;
			_framePerf.PixelsUpdated += bandFilledPixels;
			_framePerf.SegmentsIntegrated += bandSegsIntegrated;
			_framePerf.SegmentsTested += bandSegsTested;
			_framePerf.PhysicsQueries += bandPhysicsQueries;
			_framePerf.Hits += bandHits;
			_framePerf.EarlyOutFar += bandFarEarlyOuts;
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
			GD.Print($"Film band y=[{yStart},{yEnd}) hits={bandHits}");

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
			GD.Print($"RenderStep {(t1 - t0)/1000.0:0.00} ms  rows={RowsPerFrame}  jobs={jobs}  hits={bandHits}");
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
	}
	finally
	{
		if (framePerfEnabled) frameScope.Dispose();
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
		if (_img != null && _filmWidth == targetW && _filmHeight == targetH) return false;

		_filmWidth = targetW;
		_filmHeight = targetH;
		_img = Image.CreateEmpty(_filmWidth, _filmHeight, false, Image.Format.Rgba8);
		_img.Fill(SkyColor);
		_tex = ImageTexture.CreateFromImage(_img);

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
