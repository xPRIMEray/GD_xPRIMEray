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

	[Export] public bool UseInsightPlanePass2 = true;      // PASS2 plane slab reject
	[Export] public float InsightPlaneEps = 0.10f;         // slab thickness in meters-ish

	[Export] public bool NearestHitOnly = true;


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

	// conservative: max segments per ray = StepsPerRay / CollisionEveryNSteps + 2
	private int MaxSegPerRay => (_rbr != null)
		? (Mathf.Max(1, _rbr.StepsPerRay / Mathf.Max(1, _rbr.CollisionEveryNSteps)) + 2)
		: 64;


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

		if ((_rowCursor % Height) == 0)
			GD.Print($"🎥 Film RenderStep running. rowCursor={_rowCursor} cam={( _cam != null ? _cam.GetPath() : "<null>")}");

		if (_rbr == null || _cam == null) return;

		var space = _cam.GetWorld3D().DirectSpaceState;

		var fieldSources = GetTree().GetNodesInGroup("field_sources");
		var fieldSnaps = _rbr.SnapshotFieldSources(fieldSources);
		bool hasSources = fieldSnaps.Length > 0;

		if ((_rowCursor % Height) == 0)
			GD.Print($"🧲 fieldSnaps={fieldSnaps.Length} hasSources={hasSources}");


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
		int yEnd = Mathf.Min(Height, _rowCursor + Mathf.Max(1, RowsPerFrame));

		EnsureDepthHistory();
		float frameMaxHit = 0f; // track deepest hit this RenderStep band

		int bandHits = 0;

		int bandH = yEnd - yStart;
		int pixelCount = bandH * Width;

		int maxSeg = MaxSegPerRay;

		// allocate / reuse buffers
		int segTotal = pixelCount * maxSeg;
		_segBuf ??= new RayBeamRenderer.RaySeg[segTotal];
		if (_segBuf.Length < segTotal) _segBuf = new RayBeamRenderer.RaySeg[segTotal];

		_segCountPerPixel ??= new int[pixelCount];
		if (_segCountPerPixel.Length < pixelCount) _segCountPerPixel = new int[pixelCount];

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
		int chunk = Mathf.CeilToInt((float)pixelCount / jobs);

		var basisLocal = basis; // capture for lambda
		Vector3 camPos = _cam.GlobalPosition;

		var tasks = new System.Threading.Tasks.Task[jobs];

		ulong a0 = Time.GetTicksUsec(); // before Task.Run loop

		for (int j = 0; j < jobs; j++)
		{
			int start = j * chunk;
			int end = Mathf.Min(pixelCount, start + chunk);
			if (start >= end) { tasks[j] = System.Threading.Tasks.Task.CompletedTask; continue; }

			tasks[j] = System.Threading.Tasks.Task.Run(() =>
			{
				for (int pi = start; pi < end; pi++)
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

					float farForSim = AutoRangeDepth ? _rangeFar : MaxDistance;

					int count = _rbr.BuildRaySegmentsCamera(
						camPos, dirWorld, bendDir,
						center, beta, gamma,
						fieldSnaps, hasSources,
						farForSim,
						_segBuf, segOffset, maxSeg,
						insightPlane, useInsightPlane, insightEps
					);

					_segCountPerPixel[pi] = count;
				}
			});
		}

		System.Threading.Tasks.Task.WaitAll(tasks);
		ulong a1 = Time.GetTicksUsec(); // after wait

		// ---- PASS 2 (main thread): collisions + shading ----
		bandHits = 0;

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

				int segCount = _segCountPerPixel[pi];
				int segOffset = pi * maxSeg;

				for (int si = 0; si < segCount; si++)
				{
					var seg = _segBuf[segOffset + si];
					Vector3 segA = seg.A;
					Vector3 segB = seg.B;
					float segLen = (segB - segA).Length();

					if (segLen <= 1e-6f) continue;

					/////////////////////////////////
					ulong cid = 0;
					string cname = "<none>";
					Vector3 hp = Vector3.Zero;
					bool didHit = false;
					/////////////////////////////////

					if (_rbr.UseSphereSweepCollision)
					{
						didHit = RayBeamRenderer.SweepSegmentHit(space, segA, segB, _rbr.CollisionMask, _rbr.CollisionRadius, out hp);
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
						if (UseBroadphaseOverlap)
						{
							Vector3 mid = (segA + segB) * 0.5f;

							var sphere = new SphereShape3D { Radius = _rbr.CollisionRadius + BroadphaseMargin };
							var qp = new PhysicsShapeQueryParameters3D
							{
								Shape = sphere,
								Transform = new Transform3D(Basis.Identity, mid),
								CollisionMask = _rbr.CollisionMask,
								CollideWithBodies = true,
								CollideWithAreas = true
							};

							var overlaps = space.IntersectShape(qp, BroadphaseMaxResults);
							if (overlaps.Count == 0)
								continue;
						}

						// Decision C
						// ---- PASS2 cheap reject 1: quick ray probe ----
						if (UseBroadphaseQuickRay)
						{
							var rq0 = PhysicsRayQueryParameters3D.Create(segA, segB, _rbr.CollisionMask);
							rq0.CollideWithBodies = true;
							rq0.CollideWithAreas = true;
							rq0.HitFromInside = false;

							var hit0 = space.IntersectRay(rq0);
							if (hit0.Count == 0)
								continue;
						}

						// ---- accurate subdivided ray ----
						int sub = 1;
						if (segLen > _rbr.CollisionRaySubdivideThreshold)
							sub = Mathf.CeilToInt(segLen / _rbr.CollisionRaySubdivideThreshold);
						sub = Mathf.Clamp(sub, 1, _rbr.MaxCollisionSubsteps);

						didHit = RayBeamRenderer.SubdividedRayHit(
											space, segA, segB,
											_rbr.CollisionMask,
											sub,
											out hp, out cid, out cname);

						if (didHit)
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
						    hitName = cname;
						}

						// If you only want the nearest hit, keep scanning segments
						if (NearestHitOnly)
							continue;
						
						// Otherwise, first hit wins
						break;
					}
					//////////////////
				}

				Color col = SkyColor;

				if (hadHit)
				{
					bandHits++;

					// track farthest hit seen
					if (hitDistance > frameMaxHit) frameMaxHit = hitDistance;
					// use dynamic range for coloring (NOT MaxDistance)
					float far = AutoRangeDepth ? _rangeFar : MaxDistance;
					float d = Mathf.Clamp(hitDistance / Mathf.Max(0.001f, far), 0f, 1f);
					col = Color.FromHsv(0.66f * (1f - d), 1f, 1f);

					if (x == Width / 2 && y == (yStart + (bandH / 2)))
						GD.Print($"🎯 Film hit: dist={hitDistance:0.000} name={hitName}");
				}

				_img.SetPixel(x, y, col);
			}
		}
		ulong b1 = Time.GetTicksUsec(); // after PASS 2

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
		if (_rowCursor == 0 && AutoRangeDepth)
			GD.Print($"📏 AutoRange Far={_rangeFar:0.###}  (MaxDistance export={MaxDistance:0.###})");


		GD.Print($"🎞️ Film band y=[{yStart},{yEnd}) hits={bandHits}");
		_tex.Update(_img);

		_rowCursor = yEnd;
		if (_rowCursor >= Height) _rowCursor = 0;

		ulong t1 = Time.GetTicksUsec();
		GD.Print($"⏱️ RenderStep {(t1 - t0)/1000.0:0.00} ms  rows={RowsPerFrame}  jobs={jobs}  hits={bandHits}");
		GD.Print($"⏱️ pass1={(a1-a0)/1000.0:0.00}ms  pass2={(b1-a1)/1000.0:0.00}ms  total={(b1-a0)/1000.0:0.00}ms");
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

}
