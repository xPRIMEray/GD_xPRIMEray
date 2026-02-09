using Godot;
using System;
using RendererCore.Common;

public partial class FilmOverlay2D : TextureRect
{
	[ExportCategory("Film Overlay")]
	[ExportGroup("References")]
	/// <summary>Optional camera override for projection.</summary>
	[Export] public NodePath CameraPath;

	[ExportGroup("Debug Visualization")]
	/// <summary>Draws ray polylines.</summary>
	[Export] public bool DrawRays = true;
	/// <summary>Draws physics hit normals.</summary>
	[Export] public bool DrawHitNormals = true;
	/// <summary>Draws film gradient normals from image.</summary>
	[Export] public bool DrawFilmGradientNormals = false;

	/// <summary>Line width for rays.</summary>
	[Export] public float RayWidth = 1.0f;
	/// <summary>Line width for world hit normals.</summary>
	[Export] public float WorldNormalWidth = 2.0f;
	/// <summary>World-space normal length for hit normals.</summary>
	[Export] public float WorldNormalLen = 0.25f;
	/// <summary>Line width for film gradient normals.</summary>
	[Export] public float FilmNormalWidth = 2.0f;
	/// <summary>Scale for film gradient normal lines.</summary>
	[Export] public float FilmGradientScale = 6.0f;

	/// <summary>Base ray color.</summary>
	[Export] public Color RayColor = new Color(0.6f, 1.0f, 0.6f, 0.9f);
	/// <summary>Color for rays that hit.</summary>
	[Export] public Color HitRayColor = new Color(1.0f, 0.9f, 0.2f, 1.0f);
	/// <summary>Color for world hit normals.</summary>
	[Export] public Color WorldNormalColor = new Color(1.0f, 0.2f, 0.2f, 1.0f);
	/// <summary>Color for film gradient normals.</summary>
	[Export] public Color FilmNormalColor = new Color(1.0f, 0.2f, 0.2f, 1.0f);
	/// <summary>Legacy normal width (used as fallback).</summary>
	[Export] public float NormalWidth = 2.0f;
	/// <summary>Legacy world normal length (used as fallback).</summary>
	[Export] public float NormalLenWorld = 0.25f;
	/// <summary>Legacy normal color (used as fallback).</summary>
	[Export] public Color NormalColor = new Color(1.0f, 0.2f, 0.2f, 1.0f);

	private Camera3D _cam;

	private static readonly Color DefaultNormalColor = new Color(1.0f, 0.2f, 0.2f, 1.0f);
	private const float DefaultNormalWidth = 2.0f;
	private const float DefaultNormalLenWorld = 0.25f;

	private Vector3[] _pts = Array.Empty<Vector3>();
	private int[] _offsets = Array.Empty<int>();
	private int[] _counts = Array.Empty<int>();
	private RayBeamRenderer.HitPayload[] _hits = Array.Empty<RayBeamRenderer.HitPayload>();
	private int _rayCount;
	private int _ptCount;
	private float _normalLenWorld;
	private Image _filmImage;
	private int _filmWidth;
	private int _filmHeight;
	private int _filmSampleStride = 1;

	public override void _Ready()
	{
		_cam = GetNodeOrNull<Camera3D>(CameraPath);
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = false;
	}

	public void ClearOverlay()
	{
		_rayCount = 0;
		_ptCount = 0;
		QueueRedraw();
	}

	public void SetData(
		Camera3D cam,
		ReadOnlySpan<Vector3> pts,
		ReadOnlySpan<int> offsets,
		ReadOnlySpan<int> counts,
		ReadOnlySpan<RayBeamRenderer.HitPayload> hits,
		float normalLen,
		Image filmImage,
		int filmWidth,
		int filmHeight,
		int filmSampleStride)
	{
		_cam = cam ?? _cam;
		_normalLenWorld = normalLen > 0f ? normalLen : ResolveWorldNormalLen();
		_filmImage = filmImage;
		_filmWidth = filmWidth;
		_filmHeight = filmHeight;
		_filmSampleStride = Math.Max(1, filmSampleStride);

		int rayCount = Math.Min(offsets.Length, counts.Length);
		rayCount = Math.Min(rayCount, hits.Length);

		if (rayCount <= 0 || pts.Length <= 0)
		{
			_rayCount = 0;
			_ptCount = 0;
			QueueRedraw();
			return;
		}

		EnsureCapacity(rayCount, pts.Length);

		pts.CopyTo(_pts.AsSpan(0, pts.Length));
		offsets.Slice(0, rayCount).CopyTo(_offsets.AsSpan(0, rayCount));
		counts.Slice(0, rayCount).CopyTo(_counts.AsSpan(0, rayCount));
		hits.Slice(0, rayCount).CopyTo(_hits.AsSpan(0, rayCount));

		_rayCount = rayCount;
		_ptCount = pts.Length;
		QueueRedraw();
	}

	public void SetFilmImage(Image filmImage, int filmWidth, int filmHeight, int filmSampleStride)
	{
		_filmImage = filmImage;
		_filmWidth = filmWidth;
		_filmHeight = filmHeight;
		_filmSampleStride = Math.Max(1, filmSampleStride);
		QueueRedraw();
	}

	private Transform2D GetCanvasToLocalTransform()
	{
		return GetGlobalTransformWithCanvas().AffineInverse();
	}

	public Vector2 ScreenToLocal(Vector2 screen)
	{
		return GetCanvasToLocalTransform() * screen;
	}

	public Vector2 LocalToScreen(Vector2 local)
	{
		return GetCanvasToLocalTransform().AffineInverse() * local;
	}

	public override void _Process(double delta)
	{
		if (DebugOverlayBus.Count > 0)
			QueueRedraw();
	}
	
	public override void _Draw()
	{
		bool hasCam = _cam != null && IsInstanceValid(_cam);
		bool hasRayData = hasCam && _rayCount > 0 && _ptCount > 0;
		bool drawFilmGradient = DrawFilmGradientNormals && _filmImage != null && _filmWidth > 2 && _filmHeight > 2;
		bool hasOverlayItems = DebugOverlayBus.Count > 0;
		if (!hasRayData && !drawFilmGradient && !hasOverlayItems) return;

		if (hasRayData && DrawRays)
		{
			for (int r = 0; r < _rayCount; r++)
			{
				int start = _offsets[r];
				int count = _counts[r];
				if (count < 2) continue;
				if (start < 0 || (start + count) > _ptCount) continue;

				bool hadHit = r < _hits.Length && _hits[r].Valid;
				Color c = hadHit ? HitRayColor : GetRayColor(r);

				Vector3 prevW = _pts[start];
				for (int i = 1; i < count; i++)
				{
					Vector3 curW = _pts[start + i];

					bool prevBehind = _cam.IsPositionBehind(prevW);
					bool curBehind  = _cam.IsPositionBehind(curW);

					if (!(prevBehind && curBehind))
					{
						Vector2 prev = ScreenToLocal(_cam.UnprojectPosition(prevW));
						Vector2 cur  = ScreenToLocal(_cam.UnprojectPosition(curW));
						DrawLine(prev, cur, c, RayWidth);
					}

					prevW = curW;
				}
			}
		}

		// Hit normals are world-space collision normals from physics, not film-space distortion.
		// Film-surface normals require a screen-space gradient (DrawFilmGradientNormals) or ray-space curvature; physics will not provide them.
		if (hasRayData && DrawHitNormals)
		{
			Color worldColor = ResolveWorldNormalColor();
			float worldWidth = ResolveWorldNormalWidth();
			float worldLen = _normalLenWorld > 0f ? _normalLenWorld : ResolveWorldNormalLen();
			int n = Mathf.Min(_rayCount, _hits.Length);
			for (int i = 0; i < n; i++)
			{
				var h = _hits[i];
				if (!h.Valid) continue;

				Vector3 p0w = h.Position;
				Vector3 p1w = h.Position + h.Normal * worldLen;

				Vector2 p0 = ScreenToLocal(_cam.UnprojectPosition(p0w));
				Vector2 p1 = ScreenToLocal(_cam.UnprojectPosition(p1w));

				DrawLine(p0, p1, worldColor, worldWidth);
			}
		}

		// Film gradient normals are a 2D visualization derived from the film image.
		if (drawFilmGradient)
		{
			int stride = Math.Max(1, _filmSampleStride);
			//float scaleX = RectSize.X / Mathf.Max(1, _filmWidth);
			//float scaleY = RectSize.Y / Mathf.Max(1, _filmHeight);
			Vector2 sz = GetRect().Size;
			float scaleX = sz.X / Mathf.Max(1, _filmWidth);
			float scaleY = sz.Y / Mathf.Max(1, _filmHeight);

			float lineScale = FilmGradientScale;
			Color filmColor = ResolveFilmNormalColor();
			float filmWidth = ResolveFilmNormalWidth();

			for (int y = 1; y < _filmHeight - 1; y += stride)
			{
				for (int x = 1; x < _filmWidth - 1; x += stride)
				{
					float dL = Luma(_filmImage.GetPixel(x - 1, y));
					float dR = Luma(_filmImage.GetPixel(x + 1, y));
					float dU = Luma(_filmImage.GetPixel(x, y - 1));
					float dD = Luma(_filmImage.GetPixel(x, y + 1));

					float nx = dR - dL;
					float ny = dD - dU;
					Vector2 dir = new Vector2(nx, ny);
					if (dir.LengthSquared() < 1e-6f) continue;

					dir = dir.Normalized();
					Vector2 p0 = new Vector2((x + 0.5f) * scaleX, (y + 0.5f) * scaleY);
					Vector2 p1 = p0 + dir * lineScale;
					DrawLine(p0, p1, filmColor, filmWidth);
				}
			}
		}

		if (hasOverlayItems)
		{
			var font = GetThemeDefaultFont();
			int fontSize = GetThemeDefaultFontSize();

			foreach (var item in DebugOverlayBus.Items)
			{
				switch (item.Type)
				{
					case DebugOverlayBus.DebugOverlayItemType.Line:
					{
						Vector2 a = ScreenToLocal(item.A);
						Vector2 b = ScreenToLocal(item.B);
						float thickness = item.Thickness > 0f ? item.Thickness : 1f;
						DrawLine(a, b, item.Color, thickness);
						break;
					}
					case DebugOverlayBus.DebugOverlayItemType.Text:
					{
						if (font != null)
						{
							Vector2 pos = ScreenToLocal(item.Pos);
							DrawString(font, pos, item.Text, HorizontalAlignment.Left, -1f, fontSize, item.Color);
						}
						else
						{
							GD.Print(item.Text);
						}
						break;
					}
				}
			}
		}

		if (hasOverlayItems)
			DebugOverlayBus.ClearFrame();
	}


	private void EnsureCapacity(int rays, int pts)
	{
		if (_offsets.Length < rays) _offsets = new int[rays];
		if (_counts.Length < rays) _counts = new int[rays];
		if (_hits.Length < rays) _hits = new RayBeamRenderer.HitPayload[rays];
		if (_pts.Length < pts) _pts = new Vector3[pts];
	}

	private Color GetRayColor(int rayIndex)
	{
		float h = (rayIndex * 0.6180339f) % 1f;
		Color c = Color.FromHsv(h, 0.65f, 1.0f);
		c.A = RayColor.A;
		return c;
	}

	private Color ResolveWorldNormalColor()
	{
		if (WorldNormalColor == DefaultNormalColor && NormalColor != DefaultNormalColor)
			return NormalColor;
		return WorldNormalColor;
	}

	private float ResolveWorldNormalWidth()
	{
		if (Mathf.IsEqualApprox(WorldNormalWidth, DefaultNormalWidth) && !Mathf.IsEqualApprox(NormalWidth, DefaultNormalWidth))
			return NormalWidth;
		return WorldNormalWidth;
	}

	private float ResolveWorldNormalLen()
	{
		if (Mathf.IsEqualApprox(WorldNormalLen, DefaultNormalLenWorld) && !Mathf.IsEqualApprox(NormalLenWorld, DefaultNormalLenWorld))
			return NormalLenWorld;
		return WorldNormalLen;
	}

	private Color ResolveFilmNormalColor()
	{
		if (FilmNormalColor == DefaultNormalColor && NormalColor != DefaultNormalColor)
			return NormalColor;
		return FilmNormalColor;
	}

	private float ResolveFilmNormalWidth()
	{
		if (Mathf.IsEqualApprox(FilmNormalWidth, DefaultNormalWidth) && !Mathf.IsEqualApprox(NormalWidth, DefaultNormalWidth))
			return NormalWidth;
		return FilmNormalWidth;
	}

	private static float Luma(Color c)
	{
		return (c.R * 0.2126f) + (c.G * 0.7152f) + (c.B * 0.0722f);
	}
}
