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

	// ✅ ADD inside GrinFilmCamera class (with your other [Export]s)
	[Export] public FilmShadingMode ShadingMode = FilmShadingMode.DepthHeatmap;

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

	// perf stats (per full frame)
	private ulong _perfPass1Usec;
	private ulong _perfPass2PhysUsec;
	private ulong _perfPass2ShadeUsec;
	private ulong _perfDbgBuildUsec;
	private ulong _perfDbgOverlayUsec;
	private int _perfPixels;
	private int _perfSegs;
	private int _perfHits;
	private int _perfQueries;

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
		int bandH = yEnd - yStart;
		int pixelCount = bandH * Width;

		EnsureDepthHistory();
		float frameMaxHit = 0f; // track deepest hit this RenderStep band

		int bandHits = 0;
		int maxSeg = MaxSegPerRay;
		float farForSim = AutoRangeDepth ? _rangeFar : MaxDistance;

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

		if (EnableProfiling)
		{
			_perfPass1Usec += (a1 - a0);
			_perfPixels += pixelCount;
		}

		// ---- PASS 2 (main thread): collisions + shading ----
		bandHits = 0;

		Vector3 camPosPass2 = camPos;
		if (UseBroadphaseOverlap)
		{
			_overlapSphere ??= new SphereShape3D();
			_overlapQuery ??= new PhysicsShapeQueryParameters3D();
			_overlapSphere.Radius = _rbr.CollisionRadius + BroadphaseMargin;
			_overlapQuery.Shape = _overlapSphere;
			_overlapQuery.CollisionMask = _rbr.CollisionMask;
			_overlapQuery.CollideWithBodies = true;
			_overlapQuery.CollideWithAreas = true;
		}

		if (UseBroadphaseQuickRay)
		{
			_quickRayParams ??= new PhysicsRayQueryParameters3D();
			_quickRayParams.CollisionMask = _rbr.CollisionMask;
			_quickRayParams.CollideWithBodies = true;
			_quickRayParams.CollideWithAreas = true;
			_quickRayParams.HitFromInside = false;
		}

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

				if (EnableProfiling) _perfSegs += segCount;

				bool isCenterSample = (x == Width / 2 && y == (yStart + (bandH / 2)));
				bool needHitName = wantDbg || isCenterSample;

				ulong physStart = 0;
				if (EnableProfiling) physStart = Time.GetTicksUsec();

				for (int si = 0; si < segCount; si++)
				{
					var seg = _segBuf[segOffset + si];
					Vector3 segA = seg.A;
					Vector3 segB = seg.B;
					float segLen = (segB - segA).Length();

					if (segLen <= 1e-6f) continue;
					if (hadHit && bestHit < float.PositiveInfinity)
					{
						float segStartDist = seg.TraveledB - segLen;
						if (segStartDist >= bestHit)
							continue;
					}

					/////////////////////////////////
					ulong cid = 0;
					string cname = "<none>";
					Vector3 hp = Vector3.Zero;
					Vector3 hn = Vector3.Up; // ✅ NEW: hit normal
					bool didHit = false;
					/////////////////////////////////
					
					if (_rbr.UseSphereSweepCollision)
					{
						didHit = RayBeamRenderer.SweepSegmentHit(space, segA, segB, _rbr.CollisionMask, _rbr.CollisionRadius, out hp);
						if (EnableProfiling) _perfQueries++;
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

							_overlapQuery.Transform = new Transform3D(Basis.Identity, mid);
							var overlaps = space.IntersectShape(_overlapQuery, BroadphaseMaxResults);
							if (EnableProfiling) _perfQueries++;
							if (overlaps.Count == 0)
								continue;
						}

						// Decision C
						// ---- PASS2 cheap reject 1: quick ray probe ----
						if (UseBroadphaseQuickRay)
						{
							_quickRayParams.From = segA;
							_quickRayParams.To = segB;
							var hit0 = space.IntersectRay(_quickRayParams);
							if (EnableProfiling) _perfQueries++;
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
									out hp, out hn, out cid, out cname,
									out int rayQueries,
									includeColliderName: needHitName);
						if (EnableProfiling) _perfQueries += rayQueries;
						
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
							continue;
						
						// Otherwise, first hit wins
						break;
					}
					//////////////////
				}

				if (EnableProfiling)
				{
					ulong physEnd = Time.GetTicksUsec();
					_perfPass2PhysUsec += (physEnd - physStart);
				}

				////
				////////////////////////
				ulong shadeStart = 0;
				if (EnableProfiling) shadeStart = Time.GetTicksUsec();
				Color col = SkyColor;
				if (hadHit)
				{
					bandHits++;

					// track farthest hit seen
					if (hitDistance > frameMaxHit) frameMaxHit = hitDistance;

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
							// hn was captured from the nearest hit segment (see note below)
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

					if (isCenterSample)
						GD.Print($"🎯 Film hit: dist={hitDistance:0.000} name={hitName} mode={ShadingMode}");
				}

				_img.SetPixel(x, y, col);
				if (EnableProfiling)
				{
					ulong shadeEnd = Time.GetTicksUsec();
					_perfPass2ShadeUsec += (shadeEnd - shadeStart);
				}
				////////////////////////////
				/// 

				////////////////////////
				/// Debug Block Addition
				///////////
				if (wantDbg)
				{
					ulong dbgStart = 0;
					if (EnableProfiling) dbgStart = Time.GetTicksUsec();
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
					}
					if (EnableProfiling)
					{
						ulong dbgEnd = Time.GetTicksUsec();
						_perfDbgBuildUsec += (dbgEnd - dbgStart);
					}
				}
				///////////
				////////////////////////

			}
		}
		ulong b1 = Time.GetTicksUsec(); // after PASS 2
		if (EnableProfiling) _perfHits += bandHits;

		// ---- Debug overlay draw ONCE per band ----
		if (wantDbg && _filmOverlay != null)
		{
			// Convert your payload type to FilmOverlay2D.Hit
			// (Or change FilmOverlay2D.Hit to use your existing HitPayload type.)
			var hits = new FilmOverlay2D.Hit[_dbgRayCount];
			for (int i = 0; i < _dbgRayCount; i++)
			{
				var h = _dbgHits[i];
				hits[i] = new FilmOverlay2D.Hit
				{
					Valid = h.Valid,
					Position = h.Position,
					Normal = h.Normal,
					Distance = h.Distance,
					ColliderName = h.ColliderName
				};
			}

			_filmOverlay.SetOverlayData(
				_cam, // or GetViewport().GetCamera3D(), but _cam is correct for film
				_dbgPts.AsSpan(0, _dbgPtWrite),
				_dbgOff.AsSpan(0, _dbgRayCount),
				_dbgCnt.AsSpan(0, _dbgRayCount),
				hits
			);
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
		if (_rowCursor == 0 && AutoRangeDepth)
			GD.Print($"📏 AutoRange Far={_rangeFar:0.###}  (MaxDistance export={MaxDistance:0.###})");


		GD.Print($"🎞️ Film band y=[{yStart},{yEnd}) hits={bandHits}");
		_tex.Update(_img);

		_rowCursor = yEnd;
		if (_rowCursor >= Height) _rowCursor = 0;

		ulong t1 = Time.GetTicksUsec();
		GD.Print($"⏱️ RenderStep {(t1 - t0)/1000.0:0.00} ms  rows={RowsPerFrame}  jobs={jobs}  hits={bandHits}");
		GD.Print($"⏱️ pass1={(a1-a0)/1000.0:0.00}ms  pass2={(b1-a1)/1000.0:0.00}ms  total={(b1-a0)/1000.0:0.00}ms");
		
		if (EnableProfiling && _rowCursor == 0)
		{
			PrintAndResetPerfFrameStats();
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

	private void PrintAndResetPerfFrameStats()
	{
		double pass1Ms = _perfPass1Usec / 1000.0;
		double pass2PhysMs = _perfPass2PhysUsec / 1000.0;
		double pass2ShadeMs = _perfPass2ShadeUsec / 1000.0;
		double dbgBuildMs = _perfDbgBuildUsec / 1000.0;
		double dbgOverlayMs = _perfDbgOverlayUsec / 1000.0;

		GD.Print($"📊 Film frame stats: pixels={_perfPixels} segs={_perfSegs} hits={_perfHits} queries={_perfQueries}");
		GD.Print($"📊 Film timings: pass1={pass1Ms:0.00}ms pass2.physics={pass2PhysMs:0.00}ms pass2.shading={pass2ShadeMs:0.00}ms dbg.build={dbgBuildMs:0.00}ms dbg.overlay={dbgOverlayMs:0.00}ms");

		_perfPass1Usec = 0;
		_perfPass2PhysUsec = 0;
		_perfPass2ShadeUsec = 0;
		_perfDbgBuildUsec = 0;
		_perfDbgOverlayUsec = 0;
		_perfPixels = 0;
		_perfSegs = 0;
		_perfHits = 0;
		_perfQueries = 0;
	}

	void UpdateFilmOpacity()
	{
		_filmView.Modulate = new Color(1,1,1,FilmOpacity);
	}

}
