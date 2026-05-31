using Godot;
using System;
using RendererCore.Common;

public partial class FilmOverlay2D : TextureRect
{
	public readonly struct OverlayRenderSnapshot
	{
		public readonly bool DrawRaysEnabled;
		public readonly bool DrawHitNormalsEnabled;
		public readonly bool DrawFilmGradientNormalsEnabled;
		public readonly bool ComparisonGridEnabled;
		public readonly bool ComparisonCrosshairEnabled;
		public readonly int RayCount;
		public readonly int PointCount;
		public readonly int DebugOverlayItemCount;
		public readonly int DebugOverlayLineCount;
		public readonly int DebugOverlayTextCount;
		public readonly int FilmWidth;
		public readonly int FilmHeight;
		public readonly bool TraversalOverlayEnabled;
		public readonly bool TraversalMinimapEnabled;
		public readonly string TraversalMode;
		public readonly int TraversalTileCount;
		public readonly int TraversalRowsCompleted;
		public readonly bool CausalDopplerHeatmapEnabled;   // STEP 5 red/blue causal shift + halo

		public OverlayRenderSnapshot(
			bool drawRaysEnabled,
			bool drawHitNormalsEnabled,
			bool drawFilmGradientNormalsEnabled,
			bool comparisonGridEnabled,
			bool comparisonCrosshairEnabled,
			int rayCount,
			int pointCount,
			int debugOverlayItemCount,
			int debugOverlayLineCount,
			int debugOverlayTextCount,
			int filmWidth,
			int filmHeight,
			bool traversalOverlayEnabled,
			bool traversalMinimapEnabled,
			string traversalMode,
			int traversalTileCount,
			int traversalRowsCompleted)
		{
			DrawRaysEnabled = drawRaysEnabled;
			DrawHitNormalsEnabled = drawHitNormalsEnabled;
			DrawFilmGradientNormalsEnabled = drawFilmGradientNormalsEnabled;
			ComparisonGridEnabled = comparisonGridEnabled;
			ComparisonCrosshairEnabled = comparisonCrosshairEnabled;
			RayCount = rayCount;
			PointCount = pointCount;
			DebugOverlayItemCount = debugOverlayItemCount;
			DebugOverlayLineCount = debugOverlayLineCount;
			DebugOverlayTextCount = debugOverlayTextCount;
			FilmWidth = filmWidth;
			FilmHeight = filmHeight;
			TraversalOverlayEnabled = traversalOverlayEnabled;
			TraversalMinimapEnabled = traversalMinimapEnabled;
			TraversalMode = traversalMode ?? string.Empty;
			TraversalTileCount = traversalTileCount;
			TraversalRowsCompleted = traversalRowsCompleted;
		}
	}

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

	/// <summary>STEP 5: Causal Doppler heatmap (red = receding/high OPL, blue = approaching). Diagnostic only.</summary>
	[Export] public bool DrawCausalDopplerHeatmap = false;

	/// <summary>STEP 6: Hermetic failure debug overlay merged on top of doppler. Bright red=no hit, orange=max steps, purple=field escape.</summary>
	[Export] public bool DrawHermeticFailureDebug = false;

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
	/// <summary>Scale applied to debug HUD text while preserving 1.0 as the current maximum/default.</summary>
	[Export(PropertyHint.Range, "0.6,1.0,0.05")] public float HudFontScale = 1.0f;

	[ExportSubgroup("Comparison Overlay")]
	/// <summary>Draws lightweight comparison gridlines.</summary>
	[Export] public bool ShowComparisonGrid = false;
	/// <summary>Draws a center crosshair for alignment checks.</summary>
	[Export] public bool ShowComparisonCrosshair = false;
	/// <summary>Number of grid cells across the viewport.</summary>
	[Export(PropertyHint.Range, "2,8,1")] public int ComparisonGridDivisions = 4;
	/// <summary>Foreground line thickness for the comparison overlay.</summary>
	[Export(PropertyHint.Range, "0.5,3.0,0.25")] public float ComparisonLineThickness = 1.0f;
	/// <summary>Backdrop line thickness used to keep the overlay readable on bright frames.</summary>
	[Export(PropertyHint.Range, "1.0,5.0,0.25")] public float ComparisonBackdropThickness = 2.0f;
	/// <summary>Foreground color for the comparison overlay.</summary>
	[Export] public Color ComparisonLineColor = new Color(1f, 1f, 1f, 0.22f);
	/// <summary>Backdrop color for the comparison overlay.</summary>
	[Export] public Color ComparisonBackdropColor = new Color(0f, 0f, 0f, 0.18f);
	/// <summary>Crosshair arm length as a fraction of the smaller viewport dimension.</summary>
	[Export(PropertyHint.Range, "0.01,0.08,0.005")] public float ComparisonCrosshairArmFraction = 0.035f;

	[ExportSubgroup("Traversal Overlay")]
	/// <summary>Draws passive scheduler completion state over the film.</summary>
	[Export] public bool ShowTraversalOverlay = false;
	/// <summary>Draws a small completion minimap in the corner.</summary>
	[Export] public bool ShowTraversalMinimap = false;
	/// <summary>Tile size used to bin row and tile traversal state for visualization.</summary>
	[Export(PropertyHint.Range, "4,128,1")] public int TraversalOverlayTileSize = 16;
	/// <summary>Untouched tile color. Low opacity keeps the film primary.</summary>
	[Export] public Color TraversalUntouchedColor = new Color(0.55f, 0.60f, 0.65f, 0.10f);
	/// <summary>Pass-1 complete / pass-2 pending color.</summary>
	[Export] public Color TraversalPass1PendingColor = new Color(0.20f, 0.55f, 1.00f, 0.20f);
	/// <summary>Active tile or band border color.</summary>
	[Export] public Color TraversalActiveBorderColor = new Color(1f, 1f, 1f, 0.62f);
	/// <summary>Active traversal marker line width.</summary>
	[Export(PropertyHint.Range, "0.5,4.0,0.1")] public float TraversalActiveBorderWidth = 1.2f;
	/// <summary>Maximum width of the completion minimap.</summary>
	[Export(PropertyHint.Range, "80,260,5")] public float TraversalMinimapMaxWidth = 168f;

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
	private const float MinHudFontScale = 0.6f;
	private const float MaxHudFontScale = 1.0f;

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
	private byte[] _traversalTileStates = Array.Empty<byte>();
	private int _traversalCols;
	private int _traversalRows;
	private int _traversalFilmWidth;
	private int _traversalFilmHeight;
	private int _traversalTileWidth;
	private int _traversalTileHeight;
	private Rect2I _traversalActiveRect;
	private bool _traversalActiveKnown;
	private string _traversalMode = string.Empty;
	private int _traversalRowsCompleted;

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

	public void SetTraversalOverlayState(
		int filmWidth,
		int filmHeight,
		int tileWidth,
		int tileHeight,
		ReadOnlySpan<byte> tileStates,
		int cols,
		int rows,
		Rect2I activeRect,
		bool activeKnown,
		string traversalMode,
		int rowsCompleted)
	{
		_traversalFilmWidth = Math.Max(0, filmWidth);
		_traversalFilmHeight = Math.Max(0, filmHeight);
		_traversalTileWidth = Math.Max(1, tileWidth);
		_traversalTileHeight = Math.Max(1, tileHeight);
		_traversalCols = Math.Max(0, cols);
		_traversalRows = Math.Max(0, rows);
		int expected = _traversalCols * _traversalRows;
		if (expected <= 0 || tileStates.Length < expected)
		{
			ClearTraversalOverlayState();
			return;
		}

		if (_traversalTileStates.Length != expected)
			_traversalTileStates = new byte[expected];
		tileStates.Slice(0, expected).CopyTo(_traversalTileStates);

		_traversalActiveRect = activeRect;
		_traversalActiveKnown = activeKnown;
		_traversalMode = traversalMode ?? string.Empty;
		_traversalRowsCompleted = Math.Max(0, rowsCompleted);
		QueueRedraw();
	}

	public void ClearTraversalOverlayState()
	{
		_traversalCols = 0;
		_traversalRows = 0;
		_traversalFilmWidth = 0;
		_traversalFilmHeight = 0;
		_traversalActiveKnown = false;
		_traversalRowsCompleted = 0;
		_traversalMode = string.Empty;
		if (_traversalTileStates.Length > 0)
			Array.Clear(_traversalTileStates, 0, _traversalTileStates.Length);
		QueueRedraw();
	}

	public OverlayRenderSnapshot GetOverlayRenderSnapshot()
	{
		int lineCount = 0;
		int textCount = 0;
		foreach (var item in DebugOverlayBus.Items)
		{
			if (item.Type == DebugOverlayBus.DebugOverlayItemType.Line)
				lineCount++;
			else if (item.Type == DebugOverlayBus.DebugOverlayItemType.Text)
				textCount++;
		}

		return new OverlayRenderSnapshot(
			DrawRays,
			DrawHitNormals,
			DrawFilmGradientNormals,
			ShowComparisonGrid,
			ShowComparisonCrosshair,
			_rayCount,
			_ptCount,
			DebugOverlayBus.Count,
			lineCount,
			textCount,
			_filmWidth,
			_filmHeight,
			ShowTraversalOverlay,
			ShowTraversalMinimap,
			_traversalMode,
			_traversalCols * _traversalRows,
			_traversalRowsCompleted);
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
		bool drawTraversal = HasTraversalState() && (ShowTraversalOverlay || ShowTraversalMinimap);
		bool drawComparison = ShowComparisonGrid || ShowComparisonCrosshair;
		if (!hasRayData && !drawFilmGradient && !hasOverlayItems && !drawTraversal && !drawComparison) return;

		if (drawTraversal && ShowTraversalOverlay)
			DrawTraversalOverlay();

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

		if (ShowComparisonGrid || ShowComparisonCrosshair)
			DrawComparisonOverlay();

		if (drawTraversal && ShowTraversalMinimap)
			DrawTraversalMinimap();

		if (hasOverlayItems)
		{
			var font = GetThemeDefaultFont();
			int fontSize = Math.Max(1, GetThemeDefaultFontSize());
			float hudScale = ResolveHudFontScale();
			float lineHeight = font != null ? Mathf.Max(1f, font.GetHeight(fontSize)) : (fontSize + 4f);
			Vector2 viewportSize = GetViewportRect().Size;

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
							float maxWidth = Mathf.Max(1f, (viewportSize.X - item.Pos.X - 16f) / hudScale);
							float maxHeight = (viewportSize.Y - item.Pos.Y - 4f) / hudScale;
							int maxLines = lineHeight > 0f
								? Math.Max(0, Mathf.FloorToInt(maxHeight / lineHeight))
								: 0;
							if (maxLines <= 0)
								break;
							string[] rawLines = (item.Text ?? string.Empty).Split('\n');
							int renderedLineIndex = 0;
							DrawSetTransform(pos, 0f, new Vector2(hudScale, hudScale));

							for (int lineIndex = 0; lineIndex < rawLines.Length; lineIndex++)
							{
								if (renderedLineIndex >= maxLines)
									break;

								string rawLine = rawLines[lineIndex] ?? string.Empty;
								if (rawLine.EndsWith('\r'))
									rawLine = rawLine.TrimEnd('\r');
								foreach (string wrapped in WrapOverlayLine(font, fontSize, rawLine, maxWidth))
								{
									if (renderedLineIndex >= maxLines)
										break;
									float drawPosY = renderedLineIndex * lineHeight;
									if ((item.Pos.Y + (drawPosY * hudScale)) > viewportSize.Y)
										break;
									DrawString(font, new Vector2(0f, drawPosY), wrapped, HorizontalAlignment.Left, -1f, fontSize, item.Color);
									renderedLineIndex++;
								}
							}

							DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
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

		// =====================================================================
		// STEP 5/6: Combined Causal Doppler + Hermetic Failure Debug + PortalHaloMario
		// Red   = receding / higher causal distance
		// Blue  = approaching
		// Bright red = no hit / early exit
		// Orange = max steps reached
		// Purple = field escape
		// PortalHaloMario: expanding colorful rings + rotating sparkles on high-priority causal islands
		// Purely diagnostic and fun. Hermetic safety guaranteed.
		// =====================================================================
		if (DrawCausalDopplerHeatmap || DrawHermeticFailureDebug)
		{
			Vector2 sz = GetRect().Size;
			var font = GetThemeDefaultFont();
			int fontSize = font != null ? Math.Max(12, GetThemeDefaultFontSize() - 2) : 14;

			// Base doppler gradient (STEP 5)
			if (DrawCausalDopplerHeatmap)
			{
				for (int i = 0; i < 6; i++)
				{
					float t = i / 5f;
					Color c = new Color(t, 0.1f, 1f - t, 0.35f);
					Vector2 p0 = new Vector2(20 + i * 40, 30);
					Vector2 p1 = new Vector2(20 + i * 40, 80);
					DrawLine(p0, p1, c, 8f);
				}
			}

			// Hermetic failure symbols (STEP 6) - bright, distinct colors
			if (DrawHermeticFailureDebug)
			{
				// Bright red = escaped_no_hit
				Color brightRed = new Color(1f, 0.1f, 0.1f, 0.9f);
				DrawRect(new Rect2(30, 100, 18, 18), brightRed);
				if (font != null) DrawString(font, new Vector2(52, 114), "NO HIT / EARLY EXIT", HorizontalAlignment.Left, -1, fontSize, brightRed);

				// Orange = max_steps
				Color orange = new Color(1f, 0.55f, 0.1f, 0.9f);
				DrawRect(new Rect2(30, 125, 18, 18), orange);
				if (font != null) DrawString(font, new Vector2(52, 139), "MAX STEPS REACHED", HorizontalAlignment.Left, -1, fontSize, orange);

				// Purple = field escape
				Color purple = new Color(0.7f, 0.2f, 0.95f, 0.9f);
				DrawRect(new Rect2(30, 150, 18, 18), purple);
				if (font != null) DrawString(font, new Vector2(52, 164), "FIELD ESCAPE", HorizontalAlignment.Left, -1, fontSize, purple);
			}

			// PortalHaloMario (wild fun STEP 6): expanding rings + rotating sparkle on high causal priority "islands"
			if (DrawCausalDopplerHeatmap)
			{
				float time = (float)Time.GetTicksMsec() / 1000f;
				Color[] ringColors = { new Color(0.2f, 1f, 0.4f, 0.7f), new Color(1f, 0.3f, 0.8f, 0.65f), new Color(0.3f, 0.6f, 1f, 0.7f) };

				for (int ring = 0; ring < 3; ring++)
				{
					float phase = (time * 1.8f + ring * 1.3f) % (Mathf.Pi * 2);
					float radius = 12 + (phase * 8f) % 35;   // expanding
					Vector2 center = new Vector2(sz.X * (0.25f + ring * 0.25f), sz.Y * 0.82f);

					// Mario-pipe / Halo shield style ring
					DrawArc(center, radius, 0, Mathf.Pi * 2, 48, ringColors[ring], 3.5f);

					// Inner sparkle for highest causal priority objects
					if (ring == 0)
					{
						float sparkle = (Mathf.Sin(time * 9f) + 1f) * 0.5f;
						Color star = new Color(1f, 1f, 0.4f, 0.4f + sparkle * 0.5f);
						DrawArc(center, radius * 0.4f, phase, phase + Mathf.Pi, 12, star, 2f);
						DrawArc(center, radius * 0.4f, phase + Mathf.Pi, phase + Mathf.Pi * 2, 12, star, 2f);
					}
				}

				if (font != null)
					DrawString(font, new Vector2(20, 195), "PORTAL HALO MARIO — New islands get expanding rings + sparkle on top causal priority", HorizontalAlignment.Left, -1, fontSize - 1, Colors.White);
			}

			// Combined label
			if (font != null)
			{
				string label = "CAUSAL DOPPLER + HERMETIC FAIL + MARIO HALO";
				DrawString(font, new Vector2(20, 220), label, HorizontalAlignment.Left, -1, fontSize, Colors.White);
			}
		}
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

	private void DrawComparisonOverlay()
	{
		Vector2 size = GetRect().Size;
		if (size.X <= 1f || size.Y <= 1f)
			return;

		int divisions = Math.Max(2, ComparisonGridDivisions);
		bool skipCenterGrid = ShowComparisonCrosshair && (divisions % 2 == 0);
		float centerGridT = skipCenterGrid ? (divisions / 2f) / divisions : -1f;

		if (ShowComparisonGrid)
		{
			for (int i = 1; i < divisions; i++)
			{
				float t = i / (float)divisions;
				if (skipCenterGrid && Mathf.IsEqualApprox(t, centerGridT))
					continue;

				float x = size.X * t;
				float y = size.Y * t;
				DrawComparisonLine(new Vector2(x, 0f), new Vector2(x, size.Y));
				DrawComparisonLine(new Vector2(0f, y), new Vector2(size.X, y));
			}
		}

		if (ShowComparisonCrosshair)
		{
			Vector2 center = size * 0.5f;
			float armLength = Mathf.Clamp(Mathf.Min(size.X, size.Y) * ComparisonCrosshairArmFraction, 12f, 40f);
			DrawComparisonLine(center + new Vector2(-armLength, 0f), center + new Vector2(armLength, 0f));
			DrawComparisonLine(center + new Vector2(0f, -armLength), center + new Vector2(0f, armLength));
		}
	}

	private void DrawComparisonLine(Vector2 start, Vector2 end)
	{
		float backdropThickness = Mathf.Max(ComparisonBackdropThickness, ComparisonLineThickness);
		if (ComparisonBackdropColor.A > 0f && backdropThickness > 0f)
			DrawLine(start, end, ComparisonBackdropColor, backdropThickness);
		if (ComparisonLineColor.A > 0f && ComparisonLineThickness > 0f)
			DrawLine(start, end, ComparisonLineColor, ComparisonLineThickness);
	}

	private bool HasTraversalState()
	{
		return _traversalFilmWidth > 0
			&& _traversalFilmHeight > 0
			&& _traversalCols > 0
			&& _traversalRows > 0
			&& _traversalTileStates.Length >= (_traversalCols * _traversalRows);
	}

	private void DrawTraversalOverlay()
	{
		Vector2 size = GetRect().Size;
		if (size.X <= 1f || size.Y <= 1f)
			return;

		float scaleX = size.X / Mathf.Max(1, _traversalFilmWidth);
		float scaleY = size.Y / Mathf.Max(1, _traversalFilmHeight);
		for (int ty = 0; ty < _traversalRows; ty++)
		{
			int y0 = ty * _traversalTileHeight;
			int y1 = Math.Min(_traversalFilmHeight, y0 + _traversalTileHeight);
			if (y0 >= y1)
				continue;

			for (int tx = 0; tx < _traversalCols; tx++)
			{
				byte state = _traversalTileStates[(ty * _traversalCols) + tx];
				if (state >= 2)
					continue;

				int x0 = tx * _traversalTileWidth;
				int x1 = Math.Min(_traversalFilmWidth, x0 + _traversalTileWidth);
				if (x0 >= x1)
					continue;

				Color color = state == 1 ? TraversalPass1PendingColor : TraversalUntouchedColor;
				if (color.A <= 0f)
					continue;

				Rect2 rect = new Rect2(
					new Vector2(x0 * scaleX, y0 * scaleY),
					new Vector2(Mathf.Max(1f, (x1 - x0) * scaleX), Mathf.Max(1f, (y1 - y0) * scaleY)));
				DrawRect(rect, color, filled: true);
			}
		}

		if (_traversalActiveKnown && TraversalActiveBorderColor.A > 0f)
		{
			Rect2 active = FilmRectToLocal(_traversalActiveRect, scaleX, scaleY);
			if (active.Size.X > 0.5f && active.Size.Y > 0.5f)
				DrawRect(active, TraversalActiveBorderColor, filled: false, width: Math.Max(0.5f, TraversalActiveBorderWidth));
		}
	}

	private void DrawTraversalMinimap()
	{
		Vector2 size = GetRect().Size;
		if (size.X <= 1f || size.Y <= 1f)
			return;

		float aspect = _traversalFilmHeight > 0 ? _traversalFilmWidth / (float)_traversalFilmHeight : 1f;
		float mapW = Mathf.Clamp(TraversalMinimapMaxWidth, 80f, Mathf.Min(260f, size.X * 0.28f));
		float mapH = Mathf.Clamp(mapW / Mathf.Max(0.1f, aspect), 44f, size.Y * 0.24f);
		if (mapH > size.Y * 0.24f)
		{
			mapH = size.Y * 0.24f;
			mapW = mapH * Mathf.Max(0.1f, aspect);
		}

		const float margin = 14f;
		Rect2 bg = new Rect2(
			new Vector2(Mathf.Max(margin, size.X - mapW - margin), Mathf.Max(margin, size.Y - mapH - margin)),
			new Vector2(mapW, mapH));
		DrawRect(bg, new Color(0.02f, 0.025f, 0.035f, 0.68f), filled: true);
		DrawRect(bg, new Color(1f, 1f, 1f, 0.18f), filled: false, width: 1f);

		float cellW = bg.Size.X / Math.Max(1, _traversalCols);
		float cellH = bg.Size.Y / Math.Max(1, _traversalRows);
		Color completeColor = new Color(0.78f, 0.90f, 1.0f, 0.10f);
		for (int ty = 0; ty < _traversalRows; ty++)
		{
			for (int tx = 0; tx < _traversalCols; tx++)
			{
				byte state = _traversalTileStates[(ty * _traversalCols) + tx];
				Color color = state >= 2
					? completeColor
					: (state == 1 ? TraversalPass1PendingColor : TraversalUntouchedColor);
				if (color.A <= 0f)
					continue;

				Rect2 rect = new Rect2(
					bg.Position + new Vector2(tx * cellW, ty * cellH),
					new Vector2(Mathf.Max(1f, cellW), Mathf.Max(1f, cellH)));
				DrawRect(rect, color, filled: true);
			}
		}

		if (_traversalActiveKnown && TraversalActiveBorderColor.A > 0f)
		{
			float sx = bg.Size.X / Mathf.Max(1, _traversalFilmWidth);
			float sy = bg.Size.Y / Mathf.Max(1, _traversalFilmHeight);
			Rect2 active = FilmRectToLocal(_traversalActiveRect, sx, sy);
			active.Position += bg.Position;
			DrawRect(active, TraversalActiveBorderColor, filled: false, width: 1f);
		}

		Font font = GetThemeDefaultFont();
		if (font != null)
		{
			int fontSize = Math.Max(9, Mathf.RoundToInt(GetThemeDefaultFontSize() * 0.72f));
			int totalRows = Math.Max(1, _traversalFilmHeight);
			string label = $"{_traversalMode} {_traversalRowsCompleted}/{totalRows}";
			DrawString(font, bg.Position + new Vector2(6f, Mathf.Min(bg.Size.Y - 5f, fontSize + 4f)), label, HorizontalAlignment.Left, bg.Size.X - 12f, fontSize, new Color(0.92f, 0.97f, 1f, 0.86f));
		}
	}

	private Rect2 FilmRectToLocal(Rect2I rect, float scaleX, float scaleY)
	{
		int x0 = Math.Clamp(rect.Position.X, 0, Math.Max(0, _traversalFilmWidth));
		int y0 = Math.Clamp(rect.Position.Y, 0, Math.Max(0, _traversalFilmHeight));
		int x1 = Math.Clamp(rect.Position.X + Math.Max(1, rect.Size.X), 0, Math.Max(0, _traversalFilmWidth));
		int y1 = Math.Clamp(rect.Position.Y + Math.Max(1, rect.Size.Y), 0, Math.Max(0, _traversalFilmHeight));
		if (x1 <= x0)
			x1 = Math.Min(_traversalFilmWidth, x0 + 1);
		if (y1 <= y0)
			y1 = Math.Min(_traversalFilmHeight, y0 + 1);
		return new Rect2(
			new Vector2(x0 * scaleX, y0 * scaleY),
			new Vector2(Mathf.Max(1f, (x1 - x0) * scaleX), Mathf.Max(1f, (y1 - y0) * scaleY)));
	}

	private float ResolveHudFontScale()
	{
		return Mathf.Clamp(HudFontScale, MinHudFontScale, MaxHudFontScale);
	}

	private static float Luma(Color c)
	{
		return (c.R * 0.2126f) + (c.G * 0.7152f) + (c.B * 0.0722f);
	}

	private static System.Collections.Generic.IEnumerable<string> WrapOverlayLine(Font font, int fontSize, string line, float maxWidth)
	{
		if (string.IsNullOrEmpty(line))
		{
			yield return string.Empty;
			yield break;
		}

		if (!float.IsFinite(maxWidth) || maxWidth <= 1f || MeasureTextWidth(font, fontSize, line) <= maxWidth)
		{
			yield return line;
			yield break;
		}

		string[] words = line.Split(' ');
		string current = string.Empty;
		for (int i = 0; i < words.Length; i++)
		{
			string word = words[i];
			string candidate = current.Length == 0 ? word : $"{current} {word}";
			if (current.Length > 0 && MeasureTextWidth(font, fontSize, candidate) <= maxWidth)
			{
				current = candidate;
				continue;
			}

			if (current.Length > 0)
			{
				yield return current;
				current = string.Empty;
			}

			if (MeasureTextWidth(font, fontSize, word) <= maxWidth)
			{
				current = word;
				continue;
			}

			foreach (string chunk in HardSplitOverlayWord(font, fontSize, word, maxWidth))
				yield return chunk;
		}

		if (current.Length > 0)
			yield return current;
	}

	private static System.Collections.Generic.IEnumerable<string> HardSplitOverlayWord(Font font, int fontSize, string word, float maxWidth)
	{
		if (string.IsNullOrEmpty(word))
		{
			yield return string.Empty;
			yield break;
		}

		int start = 0;
		while (start < word.Length)
		{
			int len = 1;
			int bestLen = 1;
			while ((start + len) <= word.Length)
			{
				string chunk = word.Substring(start, len);
				if (MeasureTextWidth(font, fontSize, chunk) <= maxWidth)
				{
					bestLen = len;
					len++;
					continue;
				}
				break;
			}

			yield return word.Substring(start, bestLen);
			start += bestLen;
		}
	}

	private static float MeasureTextWidth(Font font, int fontSize, string text)
	{
		if (string.IsNullOrEmpty(text))
			return 0f;
		return font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
	}
}
