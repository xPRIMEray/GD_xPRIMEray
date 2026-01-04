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
		if ((_rowCursor % Height) == 0)
			GD.Print($"🎥 Film RenderStep running. rowCursor={_rowCursor} cam={( _cam != null ? _cam.GetPath() : "<null>")}");

		if (_rbr == null || _cam == null) return;

		var space = _cam.GetWorld3D().DirectSpaceState;

		var fieldSources = GetTree().GetNodesInGroup("field_sources");
		bool hasSources = fieldSources.Count > 0;

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

					int count = _rbr.BuildRaySegmentsCamera(
						camPos, dirWorld, bendDir,
						center, beta, gamma,
						fieldSources, hasSources,
						MaxDistance,
						_segBuf, segOffset, maxSeg,
						insightPlane, useInsightPlane, insightEps
					);

					_segCountPerPixel[pi] = count;
				}
			});
		}

		System.Threading.Tasks.Task.WaitAll(tasks);

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

				int segCount = _segCountPerPixel[pi];
				int segOffset = pi * maxSeg;

				for (int si = 0; si < segCount; si++)
				{
					var seg = _segBuf[segOffset + si];
					Vector3 segA = seg.A;
					Vector3 segB = seg.B;
					float segLen = (segB - segA).Length();

					if (segLen <= 1e-6f) continue;

					Vector3 hp;
					ulong cid;
					string cname;

					bool didHit = false;

					if (_rbr.UseSphereSweepCollision)
					{
						didHit = RayBeamRenderer.SweepSegmentHit(space, segA, segB, _rbr.CollisionMask, _rbr.CollisionRadius, out hp);
					}
					else
					{
						int sub = 1;
						if (segLen > _rbr.CollisionRaySubdivideThreshold)
							sub = Mathf.CeilToInt(segLen / _rbr.CollisionRaySubdivideThreshold);
						sub = Mathf.Clamp(sub, 1, _rbr.MaxCollisionSubsteps);

						didHit = RayBeamRenderer.SubdividedRayHit(space, segA, segB, _rbr.CollisionMask, sub, out hp, out cid, out cname);
						if (didHit) hitName = cname;
					}

					if (didHit)
					{
						hadHit = true;

						// distance along curved path up to hit point inside this segment
						hitDistance = seg.TraveledB - segLen + (hp - segA).Length();
						break;
					}
				}

				Color col = SkyColor;

				if (hadHit)
				{
					bandHits++;
					float d = Mathf.Clamp(hitDistance / MaxDistance, 0f, 1f);
					col = Color.FromHsv(0.66f * (1f - d), 1f, 1f);

					if (x == Width / 2 && y == (yStart + (bandH / 2)))
						GD.Print($"🎯 Film hit: dist={hitDistance:0.000} name={hitName}");
				}

				_img.SetPixel(x, y, col);
			}
		}

		GD.Print($"🎞️ Film band y=[{yStart},{yEnd}) hits={bandHits}");
		_tex.Update(_img);

		_rowCursor = yEnd;
		if (_rowCursor >= Height) _rowCursor = 0;
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
}
