using Godot;
using System;

public partial class GrinFilmCamera : Node
{
	[Export] public NodePath RayBeamRendererPath;
	[Export] public NodePath FilmViewPath; // optional: a TextureRect in your UI

	[Export] public int Width = 160;
	[Export] public int Height = 90;

	[Export] public int RowsPerFrame = 8;
	[Export] public bool UpdateEveryFrame = true;
	[Export] public float MaxDistance = 50f;
	[Export] public Color SkyColor = new Color(0, 0, 0, 1);

	[Export] public bool UseCameraPropsBetaGamma = true;

	// --- Auto range depth ---
	[Export] public bool AutoRangeDepth = true;

	// clamp limits (protect against nonsense)
	[Export] public float AutoRangeMin = 0.25f;
	[Export] public float AutoRangeMax = 200f;

	// smoothing (0.05 = slow, 0.2 = snappy)
	[Export] public float AutoRangeSmoothing = 0.15f;

	// "robust far plane" is approx max of recent hits * multiplier
	[Export] public float AutoRangeSafety = 1.15f;

	// how many frames worth of depth to track
	[Export] public int DepthHistoryFrames = 30;


	[Export] public bool UseBroadphaseQuickRay = true;     // cheapest broadphase
	[Export] public bool UseBroadphaseOverlap = false;     // optional 2nd tier
	[Export] public float BroadphaseMargin = 0.03f;
	[Export] public int BroadphaseMaxResults = 8;

	public enum BroadphaseMode
	{
		None = 0,
		QuickRayOnly = 1,
		OverlapOnly = 2,
		Both = 3
	}

	// Optimization toggles (default false)
	[Export] public bool UseFieldSourceCache = false;
	[Export] public int FieldSourceRefreshIntervalFrames = 30;
	[Export] public bool UseBroadphasePolicy = false;
	[Export] public BroadphaseMode BroadphasePolicy = BroadphaseMode.QuickRayOnly;
	[Export] public float TinySegmentSkipLen = 0.0f;
	[Export] public float EarlyOutDistanceEps = 0.0f;
	[Export] public bool NeedColliderNames = false;
	[Export] public bool VerbosePerfLogs = false;
	[Export] public bool UseAdaptiveSubsteps = false;
	[Export] public bool UseSingleProbeThenSubdivide = false;

	[Export] public bool UseBandHitSkip = false;
	[Export] public float BandSkipHitThreshold = 0.001f;
	[Export] public int BandSkipFrames = 3;
	[Export] public float BandSkipInvalidatePosDelta = 0.05f;
	[Export] public float BandSkipInvalidateBasisDelta = 0.02f;
	[Export] public float BandSkipInvalidateRangeDelta = 0.25f;

	[Export] public bool UseInsightPlanePass2 = true;      // PASS2 plane slab reject
	[Export] public float InsightPlaneEps = 0.10f;         // slab thickness in meters-ish

	[Export] public bool NearestHitOnly = true;

	// ✅ ADD inside GrinFilmCamera class (with your other [Export]s)
	[Export] public FilmShadingMode ShadingMode = FilmShadingMode.DepthHeatmap;
	// Note: overlay normals are world-space collision normals (physics mesh).
	// Film distortion is a visualization artifact and does not change collider geometry.
	// For film-surface normals, use a screen-space gradient (see FilmOverlay2D) or a ray-space curvature normal; physics will not provide it.

	// If true, flip normals so they face the camera for consistent film debug
	[Export] public bool FlipNormalToCamera = true;

	// Optional: if you later add smooth normals / mesh cache, this becomes your master switch
	[Export] public bool UseSmoothNormals = false;

	// ✅ ADD near top of GrinFilmCamera.cs (outside the class)
	public enum FilmShadingMode
	{
		DepthHeatmap = 0,   // your current behavior
		NormalRGB = 1,      // (N*0.5 + 0.5)
		NdotV = 2,          // grayscale: saturate(dot(N, V))
	}

	[Export] public float FilmOpacity = 0.7f;
	[Export] public NodePath FilmOverlayPath;
	
	private FilmOverlay2D _filmOverlay;
	private float _rangeFar = 5f; // dynamic far distance used for mapping
	private int _depthHistWrite = 0;
	private float[] _depthHistory = Array.Empty<float>();
	private Image _img;
	private ImageTexture _tex;
	private TextureRect _filmView;   // if user supplies FilmViewPath
	private TextureRect _overlayRect; // auto-created fallback
	private int _rowCursor = 0;
	private Camera3D _cam;
	private RayBeamRenderer _rbr;
	private RayBeamRenderer.RaySeg[] _segBuf;
	private int[] _segCountPerPixel;
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

	[Export] public int DebugEveryNPixels = 8;   // sample density (perf knob)
	[Export] public int DebugMaxFilmRays = 2048; // cap per band
	[Export] public bool EnableProfiling = true;

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

    	// ⛔ Freeze beam rebuilds while film camera is active
		_rbr.AllowRebuild = false;

		_filmView = GetNodeOrNull<TextureRect>(FilmViewPath);
		GD.Print("FilmView found? ", _filmView != null);

		// Create image + texture
		_img = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
		_img.Fill(SkyColor);
		_tex = ImageTexture.CreateFromImage(_img);

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
		//UpdateFilmOpacity();
	}

	public void RenderStep()
	{
		ulong t0 = Time.GetTicksUsec();
		bool perfEnabled = EnableProfiling || VerbosePerfLogs;

		if (_rowCursor == 0)
		{
			_frameIndex++;
			if (perfEnabled)
			{
				_perfFrame.Reset();
				_perfFrame.RequireHitToRender = _rbr != null && _rbr.RequireHitToRender;
			}
		}
		if (VerbosePerfLogs && (_rowCursor % Height) == 0)
			GD.Print($"Film RenderStep running. rowCursor={_rowCursor} cam={(_cam != null ? _cam.GetPath() : "<null>")}");

		if (_rbr == null || _cam == null) return;
		if (_rowCursor == 0) MaybePrintToggleSnapshot();

		var space = _cam.GetWorld3D().DirectSpaceState;

		var fieldSnaps = GetFieldSourceSnaps(_frameIndex, out bool hasSources);

		if (VerbosePerfLogs && (_rowCursor % Height) == 0)
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
		float aspect = (float)Width / Mathf.Max(1f, Height);

		int yStart = _rowCursor;
		int rowsPerFrame = Mathf.Max(1, RowsPerFrame);
		int yEnd = Mathf.Min(Height, _rowCursor + rowsPerFrame);
		int bandH = yEnd - yStart;
		int pixelCount = bandH * Width;

		EnsureDepthHistory();
		float frameMaxHit = 0f; // track deepest hit this RenderStep band

		int bandHits = 0;
		int maxSeg = MaxSegPerRay;
		float farForSim = AutoRangeDepth ? _rangeFar : MaxDistance;
		bool skipBandPhysics = false;
		int bandIndex = 0;
		if (UseBandHitSkip)
		{
			EnsureBandHitHistory(rowsPerFrame);
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
			int sampledW = (Width + pxStride - 1) / pxStride;
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

		System.Threading.Tasks.Parallel.For(
			0,
			pixelCount,
			new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = jobs },
			pi =>
			{
				int localY = pi / Width;   // 0..bandH-1
				int x = pi - localY * Width;
				int y = yStart + localY;

				float v = ((y + 0.5f) / Height) * 2f - 1f;
				v = -v;
				float u = ((x + 0.5f) / Width) * 2f - 1f;

				Vector3 dirCam = new Vector3(
					u * tanHalf * aspect,
					v * tanHalf,
					-1f
				).Normalized();

				Vector3 dirWorld = (basisLocal * dirCam).Normalized();
				Vector3 bendDir = basisLocal.X;

				int segOffset = pi * maxSeg;

				int count = _rbr.BuildRaySegmentsCamera(
					camPos, dirWorld, bendDir,
					center, beta, gamma,
					fieldSnaps, hasSources,
					farForSim,
					_segBuf, segOffset, maxSeg,
					insightPlane, useInsightPlane, insightEps
				);

				_segCountPerPixel[pi] = count;
			});

		ulong a1 = Time.GetTicksUsec(); // after wait

		if (perfEnabled)
		{
			_perfFrame.AddPass1Usec(a1 - a0);
			_perfFrame.Pixels += pixelCount;
		}

		// ---- PASS 2 (main thread): collisions + shading ----
		bandHits = 0;

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
			_quickRayParams.HitFromInside = false;
		}

		if (skipBandPhysics)
		{
			ulong shadeStart = 0;
			if (perfEnabled) shadeStart = Time.GetTicksUsec();

			for (int localY = 0; localY < bandH; localY++)
			{
				int y = yStart + localY;
				for (int x = 0; x < Width; x++)
				{
					if (perfEnabled)
					{
						int pi = localY * Width + x;
						_perfFrame.Segs += _segCountPerPixel[pi];
						if (_rbr != null && _rbr.RequireHitToRender) _perfFrame.ShadingSkippedPixels++;
					}
					_img.SetPixel(x, y, SkyColor);
				}
			}

			if (perfEnabled)
				_perfFrame.AddPass2ShadeUsec(Time.GetTicksUsec() - shadeStart);
		}
		else
		{
			for (int localY = 0; localY < bandH; localY++)
			{
				int y = yStart + localY;

				for (int x = 0; x < Width; x++)
				{
					int pi = localY * Width + x;

				bool hadHit = false;
				float hitDistance = 0f;
				string hitName = "<none>";
				float bestHit = float.PositiveInfinity;
				Vector3 bestHp = Vector3.Zero;
				Vector3 bestHn = Vector3.Up;

				int segCount = _segCountPerPixel[pi];
				int segOffset = pi * maxSeg;

				if (perfEnabled) _perfFrame.Segs += segCount;

				bool isCenterSample = (x == Width / 2 && y == (yStart + (bandH / 2)));
				bool logCenterSample = VerbosePerfLogs && isCenterSample;
				bool needHitName = NeedColliderNames || logCenterSample;

				ulong physStart = 0;
				if (perfEnabled) physStart = Time.GetTicksUsec();

				for (int si = 0; si < segCount; si++)
				{
					var seg = _segBuf[segOffset + si];
					Vector3 segA = seg.A;
					Vector3 segB = seg.B;
					float segLen = (segB - segA).Length();

					if (segLen <= 1e-6f) continue;
					if (TinySegmentSkipLen > 0f && segLen < TinySegmentSkipLen) continue;
					if (hadHit && bestHit < float.PositiveInfinity)
					{
						float segStartDist = seg.TraveledB - segLen;
						if (segStartDist >= bestHit)
							continue;
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
						didHit = RayBeamRenderer.SweepSegmentHit(space, segA, segB, _rbr.CollisionMask, _rbr.CollisionRadius, out hp);
						if (perfEnabled && !segCounted)
						{
							_perfFrame.SegsTested++;
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
							if (perfEnabled) _perfFrame.IntersectShapeCalls++;
							if (perfEnabled && !segCounted)
							{
								_perfFrame.SegsTested++;
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
							if (perfEnabled) _perfFrame.IntersectRayCalls++;
							if (perfEnabled && !segCounted)
							{
								_perfFrame.SegsTested++;
								segCounted = true;
							}
							if (hit0.Count == 0)
							{
								if (UseSingleProbeThenSubdivide)
									_perfFrame.SubdividedRaySkipped++;
								continue;
							}
						}

						if (UseSingleProbeThenSubdivide && !useQuickRay)
						{
							_quickRayParams.From = segA;
							_quickRayParams.To = segB;
							var hit0 = space.IntersectRay(_quickRayParams);
							if (perfEnabled) _perfFrame.IntersectRayCalls++;
							if (perfEnabled && !segCounted)
							{
								_perfFrame.SegsTested++;
								segCounted = true;
							}
							if (hit0.Count == 0)
							{
								_perfFrame.SubdividedRaySkipped++;
								continue;
							}
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
									includeColliderName: needHitName);
						if (perfEnabled)
						{
							_perfFrame.SubdividedRayCalls++;
							_perfFrame.SubdividedRayQueries += rayQueries;
							_perfFrame.SubdividedRaySubsteps += sub;
						}
						if (perfEnabled && !segCounted)
						{
							_perfFrame.SegsTested++;
							segCounted = true;
						}
						
						if (didHit && needHitName)
							hitName = cname;
					}

					////////////
					if (didHit)
					{
						float d = seg.TraveledB - segLen + (hp - segA).Length();

						if (d < bestHit)
						{
							bestHit = d;
							hitDistance = d;
							hadHit = true;
							if (needHitName) hitName = cname;
							bestHp = hp;      // ✅ ADD
							bestHn = hn;      // ✅ ADD
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

				if (perfEnabled)
				{
					ulong physEnd = Time.GetTicksUsec();
					_perfFrame.AddPass2PhysUsec(physEnd - physStart);
				}

				////
				////////////////////////
				ulong shadeStart = 0;
				if (perfEnabled) shadeStart = Time.GetTicksUsec();
				Color col = SkyColor;
				bool skipShading = _rbr != null && _rbr.RequireHitToRender && !hadHit;
				if (skipShading)
				{
					if (perfEnabled) _perfFrame.ShadingSkippedPixels++;
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
							Vector3 v = (camPosPass2 - bestHp).Normalized();
							Vector3 n = bestHn;
							if (FlipNormalToCamera && n.Dot(v) < 0f) n = -n;
							col = ShadeNdotV(n, v);
							break;
						}

					}

					if (logCenterSample)
						GD.Print($"Film hit: dist={hitDistance:0.000} name={hitName} mode={ShadingMode}");
				}

				_img.SetPixel(x, y, col);
				if (perfEnabled)
				{
					ulong shadeEnd = Time.GetTicksUsec();
					_perfFrame.AddPass2ShadeUsec(shadeEnd - shadeStart);
				}
				////////////////////////////
				/// 

				////////////////////////
				/// Debug Block Addition
				///////////
				if (wantDbg)
				{
					ulong dbgStart = 0;
					if (perfEnabled) dbgStart = Time.GetTicksUsec();
					int pxStride = Math.Max(1, DebugEveryNPixels);

					// Sample a sparse grid (keeps overlay readable + fast)
					if ((x % pxStride) == 0 && (localY % pxStride) == 0 && _dbgRayCount < DebugMaxFilmRays)
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
					if (perfEnabled)
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
		ulong b1 = Time.GetTicksUsec(); // after PASS 2
		if (perfEnabled) _perfFrame.Hits += bandHits;
		if (UseBandHitSkip && bandIndex >= 0 && bandIndex < _bandHitRate.Length)
		{
			float hitRate = pixelCount > 0 ? (float)bandHits / pixelCount : 0f;
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
			if (perfEnabled) dbgOverlayStart = Time.GetTicksUsec();

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
				Width,
				Height,
				DebugEveryNPixels
			);

			if (perfEnabled)
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
			_filmOverlay.SetFilmImage(_img, Width, Height, DebugEveryNPixels);
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
		if (perfEnabled) updateStart = Time.GetTicksUsec();
		_tex.Update(_img);
		if (perfEnabled) _perfFrame.AddFilmUpdateUsec(Time.GetTicksUsec() - updateStart);

		_rowCursor = yEnd;
		if (_rowCursor >= Height) _rowCursor = 0;

		ulong t1 = Time.GetTicksUsec();
		if (VerbosePerfLogs)
		{
			GD.Print($"RenderStep {(t1 - t0)/1000.0:0.00} ms  rows={RowsPerFrame}  jobs={jobs}  hits={bandHits}");
			GD.Print($"pass1={(a1-a0)/1000.0:0.00}ms  pass2={(b1-a1)/1000.0:0.00}ms  total={(b1-a0)/1000.0:0.00}ms");
		}
		
		if (perfEnabled && _rowCursor == 0)
		{
			_perfFrame.ShadingSkippedNoHits = _perfFrame.RequireHitToRender
				&& _perfFrame.Hits == 0
				&& _perfFrame.Pixels > 0
				&& _perfFrame.ShadingSkippedPixels >= _perfFrame.Pixels;
			_perfStats.FinalizeAndPrint(ref _perfFrame, VerbosePerfLogs);
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

	private RayBeamRenderer.FieldSourceSnap[] GetFieldSourceSnaps(int frameIndex, out bool hasSources)
	{
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
			RefreshFieldSourceCache(frameIndex);

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

	private void EnsureBandHitHistory(int rowsPerFrame)
	{
		int bandCount = (Height + rowsPerFrame - 1) / rowsPerFrame;
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

	private static Color ShadeNdotV(Vector3 n, Vector3 v)
	{
		n = n.Normalized();
		v = v.Normalized();
		float ndv = Mathf.Clamp(n.Dot(v), 0f, 1f);
		return new Color(ndv, ndv, ndv, 1f);
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
