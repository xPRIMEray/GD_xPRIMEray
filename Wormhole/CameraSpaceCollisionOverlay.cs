using Godot;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Camera-space collision radar overlay.
/// This is distinct from:
/// - StraightTransportReference: literal cached straight render
/// - WireframeReferenceOverlay: semantic field / BLV / portal glyphs
/// - DiagnosticOverlay: scalar film or curvature heatmaps
/// It only draws lightweight projected bounds for scene collision objects.
/// </summary>
public partial class CameraSpaceCollisionOverlay : Control
{
	public enum CollisionRadarDisplayFilterMode
	{
		AllVisible = 0,
		HitConfirmedOnly = 1,
		PrimaryOnly = 2,
		RemappedOnly = 3,
		BackgroundOnly = 4,
		HelpersOnly = 5,
	}

	public enum CollisionRadarBoundsMode
	{
		CenterOnly = 0,
		Sphere = 1,
		Aabb = 2,
		LabelOnly = 3,
	}

	private enum CollisionRadarCategory
	{
		Primary = 0,
		Remapped = 1,
		Background = 2,
		Helper = 3,
	}

	private readonly struct CollisionRadarEntry
	{
		public readonly Node3D Node;
		public readonly CollisionRadarCategory Category;
		public readonly string Label;
		public readonly bool RequiresPortalRemap;
		public readonly ulong ColliderId;

		public CollisionRadarEntry(Node3D node, CollisionRadarCategory category, string label, bool requiresPortalRemap, ulong colliderId)
		{
			Node = node;
			Category = category;
			Label = label ?? string.Empty;
			RequiresPortalRemap = requiresPortalRemap;
			ColliderId = colliderId;
		}
	}

	private readonly struct VisibleCollisionEntry
	{
		public readonly CollisionRadarEntry Entry;
		public readonly Rect2 Bounds;
		public readonly Vector2 Center;
		public readonly int HitCount;

		public VisibleCollisionEntry(CollisionRadarEntry entry, Rect2 bounds, Vector2 center, int hitCount)
		{
			Entry = entry;
			Bounds = bounds;
			Center = center;
			HitCount = hitCount;
		}
	}

	private sealed class LabelPlacement
	{
		public Rect2 Rect;
		public Vector2 TextPosition;
		public Vector2 LeaderTarget;
	}

	private Camera3D _mainCamera;
	private Node3D _sceneA;
	private Node3D _sceneB;
	private WormholePortal _portalA;
	private WormholePortal _portalB;
	private bool _primarySceneIsA = true;
	private string _debugVisibleSummary = string.Empty;
	private readonly Dictionary<ulong, int> _hitCounts = new();

	public bool OverlayEnabled { get; set; }
	public float OverlayOpacity { get; set; } = 0.78f;
	public bool ShowPrimarySceneGeometry { get; set; } = true;
	public bool ShowRemappedSceneGeometry { get; set; } = true;
	public bool ShowBackgroundObjects { get; set; } = true;
	public bool ShowProbeHelpers { get; set; } = true;
	public bool ShowCenterMarkers { get; set; } = true;
	public bool ShowLabels { get; set; } = true;
	public bool ShowLegend { get; set; } = true;
	public bool ShowLeaderLines { get; set; } = true;
	public int MaxVisibleLabels { get; set; } = 8;
	public CollisionRadarDisplayFilterMode DisplayFilterMode { get; set; } = CollisionRadarDisplayFilterMode.AllVisible;
	public CollisionRadarBoundsMode BoundsMode { get; set; } = CollisionRadarBoundsMode.Aabb;
	public string ModeLabel { get; set; } = "COLLISION RADAR · CURVED";
	public string DebugVisibleSummary => _debugVisibleSummary;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		AnchorLeft = 0f;
		AnchorTop = 0f;
		AnchorRight = 1f;
		AnchorBottom = 1f;
		OffsetLeft = 0f;
		OffsetTop = 0f;
		OffsetRight = 0f;
		OffsetBottom = 0f;
	}

	public override void _Process(double delta)
	{
		if (Visible)
		{
			QueueRedraw();
		}
	}

	public void Configure(Camera3D mainCamera, Node3D sceneA, Node3D sceneB, WormholePortal portalA, WormholePortal portalB)
	{
		_mainCamera = mainCamera;
		_sceneA = sceneA;
		_sceneB = sceneB;
		_portalA = portalA;
		_portalB = portalB;
	}

	public void SetPrimarySceneIsA(bool primarySceneIsA)
	{
		_primarySceneIsA = primarySceneIsA;
	}

	public void SetHitActivity(IReadOnlyList<GrinFilmCamera.ColliderHitActivityEntry> entries)
	{
		_hitCounts.Clear();
		if (entries == null)
		{
			return;
		}

		for (int i = 0; i < entries.Count; i++)
		{
			GrinFilmCamera.ColliderHitActivityEntry entry = entries[i];
			if (entry.ColliderId == 0 || entry.HitCount <= 0)
			{
				continue;
			}

			_hitCounts[entry.ColliderId] = entry.HitCount;
		}
	}

	public override void _Draw()
	{
		if (!OverlayEnabled || _mainCamera == null)
		{
			return;
		}

		List<CollisionRadarEntry> entries = new();
		CollectCollisionEntries(entries);

		List<VisibleCollisionEntry> visibleEntries = new(entries.Count);
		foreach (CollisionRadarEntry entry in entries)
		{
			if (!ShouldDrawEntry(entry))
			{
				continue;
			}

			if (!TryGetProjectedBounds(entry, out Rect2 bounds, out Vector2 center))
			{
				continue;
			}

			visibleEntries.Add(new VisibleCollisionEntry(
				entry,
				bounds,
				center,
				_hitCounts.TryGetValue(entry.ColliderId, out int hitCount) ? hitCount : 0));
		}

		visibleEntries.Sort(static (a, b) =>
		{
			int hitOrder = b.HitCount.CompareTo(a.HitCount);
			if (hitOrder != 0)
			{
				return hitOrder;
			}

			int categoryOrder = a.Entry.Category.CompareTo(b.Entry.Category);
			if (categoryOrder != 0)
			{
				return categoryOrder;
			}

			return string.CompareOrdinal(a.Entry.Label, b.Entry.Label);
		});

		int primaryCount = 0;
		int remappedCount = 0;
		int backgroundCount = 0;
		int helperCount = 0;
		int hitConfirmedCount = 0;
		List<string> visibleLabels = new();

		for (int i = 0; i < visibleEntries.Count; i++)
		{
			VisibleCollisionEntry visible = visibleEntries[i];
			Color color = ResolveCategoryColor(visible.Entry.Category, visible.HitCount > 0);
			DrawTrackedBounds(visible.Bounds, visible.Center, color, visible.Entry.Category);

			switch (visible.Entry.Category)
			{
				case CollisionRadarCategory.Primary:
					primaryCount++;
					break;
				case CollisionRadarCategory.Remapped:
					remappedCount++;
					break;
				case CollisionRadarCategory.Background:
					backgroundCount++;
					break;
				case CollisionRadarCategory.Helper:
					helperCount++;
					break;
			}

			if (visible.HitCount > 0)
			{
				hitConfirmedCount++;
			}
		}

		if (ShowLabels)
		{
			List<Rect2> occupied = new();
			AddReservedLabelZones(occupied);
			int labelCount = 0;
			for (int i = 0; i < visibleEntries.Count && labelCount < MaxVisibleLabels; i++)
			{
				VisibleCollisionEntry visible = visibleEntries[i];
				Color color = ResolveCategoryColor(visible.Entry.Category, visible.HitCount > 0);
				LabelPlacement placement = ResolveLabelPlacement(visible, occupied);
				if (placement == null)
				{
					continue;
				}

				string labelText = visible.HitCount > 0
					? $"{visible.Entry.Label} · h{visible.HitCount}"
					: visible.Entry.Label;
				DrawObjectLabel(placement, visible.Center, labelText, color);
				occupied.Add(placement.Rect);
				labelCount++;
				if (visibleLabels.Count < 5)
				{
					visibleLabels.Add(labelText);
				}
			}
		}

		_debugVisibleSummary = BuildVisibleSummary(entries.Count, primaryCount, remappedCount, backgroundCount, helperCount, hitConfirmedCount, visibleLabels);
		DrawOverlayLabel();
		DrawActivitySummary(visibleEntries.Count, hitConfirmedCount, primaryCount, remappedCount, backgroundCount, helperCount, visibleEntries);
		if (ShowLegend)
		{
			DrawLegend(hitConfirmedCount);
		}
	}

	private void CollectCollisionEntries(List<CollisionRadarEntry> entries)
	{
		Node3D primary = _primarySceneIsA ? _sceneA : _sceneB;
		Node3D remapped = _primarySceneIsA ? _sceneB : _sceneA;
		CollectCollisionEntriesRecursive(primary, entries, true);
		CollectCollisionEntriesRecursive(remapped, entries, false);
	}

	private void CollectCollisionEntriesRecursive(Node node, List<CollisionRadarEntry> entries, bool isPrimaryScene)
	{
		if (node == null)
		{
			return;
		}

		foreach (Node child in node.GetChildren())
		{
			if (child is CollisionObject3D && child is Node3D node3D && HasCollisionShape(node3D))
			{
				CollisionRadarCategory category = ClassifyCollisionNode(node3D, isPrimaryScene);
				string label = BuildLabel(node3D, category, isPrimaryScene);
				entries.Add(new CollisionRadarEntry(node3D, category, label, !isPrimaryScene, node3D.GetInstanceId()));
			}

			CollectCollisionEntriesRecursive(child, entries, isPrimaryScene);
		}
	}

	private static bool HasCollisionShape(Node3D node)
	{
		return FindFirstCollisionShape(node) != null;
	}

	private static CollisionShape3D FindFirstCollisionShape(Node node)
	{
		if (node == null)
		{
			return null;
		}

		foreach (Node child in node.GetChildren())
		{
			if (child is CollisionShape3D collisionShape)
			{
				return collisionShape;
			}

			CollisionShape3D nested = FindFirstCollisionShape(child);
			if (nested != null)
			{
				return nested;
			}
		}

		return null;
	}

	private CollisionRadarCategory ClassifyCollisionNode(Node3D node, bool isPrimaryScene)
	{
		string name = node.Name.ToString().ToLowerInvariant();
		if (name.Contains("probe"))
		{
			return CollisionRadarCategory.Helper;
		}

		if (name.Contains("backdrop"))
		{
			return CollisionRadarCategory.Background;
		}

		return isPrimaryScene ? CollisionRadarCategory.Primary : CollisionRadarCategory.Remapped;
	}

	private static string BuildLabel(Node3D node, CollisionRadarCategory category, bool isPrimaryScene)
	{
		string prefix = category switch
		{
			CollisionRadarCategory.Background => "BG",
			CollisionRadarCategory.Helper => "HELPER",
			_ => isPrimaryScene ? "A" : "B",
		};
		return $"{prefix} · {node.Name}";
	}

	private bool ShouldDrawEntry(CollisionRadarEntry entry)
	{
		bool categoryEnabled = entry.Category switch
		{
			CollisionRadarCategory.Primary => ShowPrimarySceneGeometry,
			CollisionRadarCategory.Remapped => ShowRemappedSceneGeometry,
			CollisionRadarCategory.Background => ShowBackgroundObjects,
			CollisionRadarCategory.Helper => ShowProbeHelpers,
			_ => false,
		};
		if (!categoryEnabled)
		{
			return false;
		}

		bool hitConfirmed = _hitCounts.ContainsKey(entry.ColliderId);
		return DisplayFilterMode switch
		{
			CollisionRadarDisplayFilterMode.HitConfirmedOnly => hitConfirmed,
			CollisionRadarDisplayFilterMode.PrimaryOnly => entry.Category == CollisionRadarCategory.Primary,
			CollisionRadarDisplayFilterMode.RemappedOnly => entry.Category == CollisionRadarCategory.Remapped,
			CollisionRadarDisplayFilterMode.BackgroundOnly => entry.Category == CollisionRadarCategory.Background,
			CollisionRadarDisplayFilterMode.HelpersOnly => entry.Category == CollisionRadarCategory.Helper,
			_ => true,
		};
	}

	private Color ResolveCategoryColor(CollisionRadarCategory category, bool hitConfirmed)
	{
		Color baseColor = category switch
		{
			CollisionRadarCategory.Primary => new Color(1f, 0.76f, 0.28f, 0.92f * OverlayOpacity),
			CollisionRadarCategory.Remapped => new Color(0.34f, 0.86f, 1f, 0.92f * OverlayOpacity),
			CollisionRadarCategory.Background => new Color(0.70f, 0.76f, 0.90f, 0.52f * OverlayOpacity),
			CollisionRadarCategory.Helper => new Color(0.92f, 0.58f, 1f, 0.74f * OverlayOpacity),
			_ => new Color(1f, 1f, 1f, 0.8f * OverlayOpacity),
		};

		if (!hitConfirmed)
		{
			baseColor.A *= 0.8f;
			return baseColor;
		}

		return new Color(
			Mathf.Clamp(baseColor.R + 0.08f, 0f, 1f),
			Mathf.Clamp(baseColor.G + 0.08f, 0f, 1f),
			Mathf.Clamp(baseColor.B + 0.08f, 0f, 1f),
			Mathf.Clamp(baseColor.A + 0.08f, 0f, 1f));
	}

	private void DrawTrackedBounds(Rect2 bounds, Vector2 center, Color color, CollisionRadarCategory category)
	{
		switch (BoundsMode)
		{
			case CollisionRadarBoundsMode.LabelOnly:
				return;
			case CollisionRadarBoundsMode.CenterOnly:
				if (ShowCenterMarkers)
				{
					DrawCenterMarker(center, color, category);
				}
				return;
			case CollisionRadarBoundsMode.Sphere:
			{
				float radius = Mathf.Max(8f, Mathf.Max(bounds.Size.X, bounds.Size.Y) * 0.5f);
				DrawArc(center, radius, 0f, Mathf.Tau, 28, color, ResolveLineWidth(category), true);
				if (ShowCenterMarkers)
				{
					DrawCenterMarker(center, color, category);
				}
				return;
			}
			default:
			{
				Rect2 clipped = bounds.Intersection(new Rect2(Vector2.Zero, Size));
				if (clipped.Size.X > 1f && clipped.Size.Y > 1f)
				{
					DrawRect(clipped, color, false, ResolveLineWidth(category));
				}
				if (ShowCenterMarkers)
				{
					DrawCenterMarker(center, color, category);
				}
				return;
			}
		}
	}

	private static float ResolveLineWidth(CollisionRadarCategory category)
	{
		return category switch
		{
			CollisionRadarCategory.Primary => 2.2f,
			CollisionRadarCategory.Remapped => 2.0f,
			CollisionRadarCategory.Helper => 1.8f,
			_ => 1.4f,
		};
	}

	private void DrawCenterMarker(Vector2 center, Color color, CollisionRadarCategory category)
	{
		float arm = category == CollisionRadarCategory.Background ? 4f : 5.5f;
		Vector2 h = new(arm, 0f);
		Vector2 v = new(0f, arm);
		DrawLine(center - h, center + h, color, 1.5f, true);
		DrawLine(center - v, center + v, color, 1.5f, true);
		DrawCircle(center, 2.2f, new Color(color, Mathf.Clamp(color.A + 0.08f, 0f, 1f)));
	}

	private void DrawObjectLabel(LabelPlacement placement, Vector2 center, string text, Color color)
	{
		if (placement == null || string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		if (ShowLeaderLines)
		{
			DrawLine(center, placement.LeaderTarget, new Color(color, 0.72f), 1.2f, true);
		}

		Color background = new(0.04f, 0.06f, 0.1f, 0.82f * OverlayOpacity);
		Color outline = new(0.01f, 0.02f, 0.05f, 0.92f * OverlayOpacity);
		Color textColor = new Color(color, Mathf.Clamp(color.A + 0.08f, 0f, 1f));
		DrawRect(placement.Rect, background, true);
		DrawRect(placement.Rect, new Color(color, 0.52f), false, 1.0f);

		Vector2[] outlineOffsets =
		{
			new Vector2(-1f, 0f),
			new Vector2(1f, 0f),
			new Vector2(0f, -1f),
			new Vector2(0f, 1f),
		};
		foreach (Vector2 offset in outlineOffsets)
		{
			DrawString(ThemeDB.FallbackFont, placement.TextPosition + offset, text, HorizontalAlignment.Left, -1f, 10, outline);
		}
		DrawString(ThemeDB.FallbackFont, placement.TextPosition, text, HorizontalAlignment.Left, -1f, 10, textColor);
	}

	private LabelPlacement ResolveLabelPlacement(VisibleCollisionEntry visible, List<Rect2> occupied)
	{
		string labelText = visible.HitCount > 0
			? $"{visible.Entry.Label} · h{visible.HitCount}"
			: visible.Entry.Label;
		Font font = ThemeDB.FallbackFont;
		Vector2 textSize = font.GetStringSize(labelText, HorizontalAlignment.Left, -1, 10);
		Vector2 labelSize = new(Mathf.Max(64f, textSize.X + 12f), Mathf.Max(16f, textSize.Y + 8f));

		Vector2[] offsets =
		{
			new Vector2(12f, 10f),
			new Vector2(12f, -labelSize.Y - 10f),
			new Vector2(-labelSize.X - 12f, 10f),
			new Vector2(-labelSize.X - 12f, -labelSize.Y - 10f),
			new Vector2(18f, 28f),
			new Vector2(-labelSize.X - 18f, 28f),
		};

		for (int i = 0; i < offsets.Length; i++)
		{
			Rect2 rect = new Rect2(visible.Center + offsets[i], labelSize);
			rect.Position = ClampLabelPosition(rect.Position, rect.Size);
			if (IntersectsAny(rect, occupied))
			{
				continue;
			}

			return BuildLabelPlacement(rect);
		}

		Rect2 fallback = new Rect2(ClampLabelPosition(visible.Center + new Vector2(14f, 18f), labelSize), labelSize);
		return BuildLabelPlacement(fallback);
	}

	private static bool IntersectsAny(Rect2 rect, List<Rect2> occupied)
	{
		for (int i = 0; i < occupied.Count; i++)
		{
			if (rect.Intersects(occupied[i]))
			{
				return true;
			}
		}

		return false;
	}

	private LabelPlacement BuildLabelPlacement(Rect2 rect)
	{
		return new LabelPlacement
		{
			Rect = rect,
			TextPosition = rect.Position + new Vector2(6f, rect.Size.Y - 5f),
			LeaderTarget = rect.Position + new Vector2(rect.Size.X * 0.5f, rect.Size.Y * 0.5f),
		};
	}

	private Vector2 ClampLabelPosition(Vector2 pos, Vector2 size)
	{
		float minX = 8f;
		float minY = 14f;
		float maxX = Math.Max(minX, Size.X - size.X - 8f);
		float maxY = Math.Max(minY, Size.Y - size.Y - 8f);
		return new Vector2(
			Mathf.Clamp(pos.X, minX, maxX),
			Mathf.Clamp(pos.Y, minY, maxY));
	}

	private void AddReservedLabelZones(List<Rect2> occupied)
	{
		if (Size.X >= 600f && Size.Y >= 300f)
		{
			occupied.Add(new Rect2(0f, 0f, Math.Min(540f, Size.X * 0.54f), Math.Min(180f, Size.Y * 0.30f)));
		}
	}

	private string BuildVisibleSummary(int totalEntries, int primaryCount, int remappedCount, int backgroundCount, int helperCount, int hitConfirmedCount, List<string> labels)
	{
		StringBuilder sb = new();
		sb.Append($"entries={totalEntries} primary={primaryCount} remapped={remappedCount} background={backgroundCount} helpers={helperCount} hitConfirmed={hitConfirmedCount}");
		if (labels.Count > 0)
		{
			sb.Append(" labels=");
			sb.Append(string.Join(" / ", labels));
		}
		return sb.ToString();
	}

	private void DrawOverlayLabel()
	{
		Color panel = new(0.05f, 0.07f, 0.11f, 0.78f * OverlayOpacity);
		Color title = new(0.94f, 0.98f, 1f, 0.96f * OverlayOpacity);
		Color note = new(0.86f, 0.92f, 0.98f, 0.90f * OverlayOpacity);
		DrawRect(new Rect2(16f, 16f, Mathf.Min(560f, Size.X - 24f), 50f), panel, true);
		DrawString(ThemeDB.FallbackFont, new Vector2(26f, 36f), ModeLabel, HorizontalAlignment.Left, -1f, 14, title);
		DrawString(ThemeDB.FallbackFont, new Vector2(26f, 52f), _debugVisibleSummary, HorizontalAlignment.Left, -1f, 11, note);
	}

	private void DrawActivitySummary(
		int visibleCount,
		int hitConfirmedCount,
		int primaryCount,
		int remappedCount,
		int backgroundCount,
		int helperCount,
		List<VisibleCollisionEntry> visibleEntries)
	{
		float width = Mathf.Min(300f, Size.X * 0.38f);
		float height = 102f;
		float rightMargin = ShowLegend ? 276f : 16f;
		float x = Mathf.Max(16f, Size.X - width - rightMargin);
		float y = Math.Max(76f, Size.Y - height - 16f);
		Rect2 rect = new(x, y, width, height);
		Color bg = new(0.05f, 0.07f, 0.11f, 0.76f * OverlayOpacity);
		Color frame = new(0.34f, 0.44f, 0.62f, 0.50f * OverlayOpacity);
		Color title = new(0.90f, 0.96f, 1f, 0.96f * OverlayOpacity);
		Color body = new(0.84f, 0.92f, 0.98f, 0.92f * OverlayOpacity);
		Color note = new(0.76f, 0.84f, 0.92f, 0.90f * OverlayOpacity);
		DrawRect(rect, bg, true);
		DrawRect(rect, frame, false, 1f);

		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(10f, 16f), $"OBJECT ACTIVITY · {DisplayFilterMode}", HorizontalAlignment.Left, -1f, 10, title);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(10f, 31f), $"visible {visibleCount} · hit-confirmed {hitConfirmedCount}", HorizontalAlignment.Left, -1f, 10, body);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(10f, 46f), BuildActiveCategorySummary(primaryCount, remappedCount, backgroundCount, helperCount), HorizontalAlignment.Left, -1f, 10, note);

		string topLine = BuildTopHitLabelSummary(visibleEntries, 3);
		if (!string.IsNullOrEmpty(topLine))
		{
			DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(10f, 64f), topLine, HorizontalAlignment.Left, -1f, 10, body);
		}
		else if (DisplayFilterMode == CollisionRadarDisplayFilterMode.HitConfirmedOnly)
		{
			DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(10f, 64f), "no visible hit-confirmed objects", HorizontalAlignment.Left, -1f, 10, new Color(0.90f, 0.78f, 0.62f, 0.95f * OverlayOpacity));
		}

		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(10f, 82f), $"bounds {BoundsMode} · labels {(ShowLabels ? "on" : "off")} · leaders {(ShowLeaderLines ? "on" : "off")}", HorizontalAlignment.Left, -1f, 10, note);
	}

	private static string BuildActiveCategorySummary(int primaryCount, int remappedCount, int backgroundCount, int helperCount)
	{
		List<string> active = new(4);
		if (primaryCount > 0) active.Add("primary");
		if (remappedCount > 0) active.Add("remapped");
		if (backgroundCount > 0) active.Add("background");
		if (helperCount > 0) active.Add("helpers");
		return active.Count > 0
			? $"active {string.Join(" · ", active)}"
			: "active none";
	}

	private static string BuildTopHitLabelSummary(List<VisibleCollisionEntry> visibleEntries, int maxCount)
	{
		if (visibleEntries == null || visibleEntries.Count == 0 || maxCount <= 0)
		{
			return string.Empty;
		}

		List<string> labels = new(maxCount);
		for (int i = 0; i < visibleEntries.Count && labels.Count < maxCount; i++)
		{
			VisibleCollisionEntry visible = visibleEntries[i];
			if (visible.HitCount <= 0)
			{
				continue;
			}

			labels.Add($"{visible.Entry.Label} h{visible.HitCount}");
		}

		return labels.Count > 0
			? $"top hits {string.Join(" / ", labels)}"
			: string.Empty;
	}

	private void DrawLegend(int hitConfirmedCount)
	{
		float width = Mathf.Min(250f, Size.X * 0.34f);
		float height = 90f;
		Rect2 rect = new(Size.X - width - 16f, Size.Y - height - 16f, width, height);
		Color bg = new(0.05f, 0.07f, 0.11f, 0.74f * OverlayOpacity);
		Color text = new(0.9f, 0.95f, 1f, 0.94f * OverlayOpacity);
		DrawRect(rect, bg, true);
		DrawRect(rect, new Color(0.38f, 0.46f, 0.62f, 0.52f * OverlayOpacity), false, 1f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(10f, 16f), $"FILTER {DisplayFilterMode} · BOUNDS {BoundsMode}", HorizontalAlignment.Left, -1f, 10, text);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(10f, 30f), $"HIT-CONFIRMED {hitConfirmedCount}", HorizontalAlignment.Left, -1f, 10, text);

		DrawLegendSwatch(rect.Position + new Vector2(10f, 44f), ResolveCategoryColor(CollisionRadarCategory.Primary, true), "primary");
		DrawLegendSwatch(rect.Position + new Vector2(10f, 58f), ResolveCategoryColor(CollisionRadarCategory.Remapped, true), "remapped");
		DrawLegendSwatch(rect.Position + new Vector2(110f, 44f), ResolveCategoryColor(CollisionRadarCategory.Background, false), "background");
		DrawLegendSwatch(rect.Position + new Vector2(110f, 58f), ResolveCategoryColor(CollisionRadarCategory.Helper, false), "helpers");
	}

	private void DrawLegendSwatch(Vector2 pos, Color color, string text)
	{
		DrawRect(new Rect2(pos, new Vector2(8f, 8f)), color, true);
		DrawString(ThemeDB.FallbackFont, pos + new Vector2(14f, 8f), text, HorizontalAlignment.Left, -1f, 10, new Color(0.88f, 0.94f, 1f, 0.92f * OverlayOpacity));
	}

	private bool TryGetProjectedBounds(CollisionRadarEntry entry, out Rect2 screenBounds, out Vector2 center)
	{
		screenBounds = default;
		center = default;
		if (!TryGetWorldBoundPoints(entry.Node, out Vector3[] points))
		{
			return false;
		}

		Vector3 remappedCenterWorld = ApplyPortalRemapIfNeeded(entry, entry.Node.GlobalPosition);
		bool hasCenter = TryProject(remappedCenterWorld, out Vector2 projectedCenter);
		bool any = false;
		Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
		Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
		float maxRadius = 0f;

		for (int i = 0; i < points.Length; i++)
		{
			Vector3 worldPoint = ApplyPortalRemapIfNeeded(entry, points[i]);
			if (!TryProject(worldPoint, out Vector2 projected))
			{
				continue;
			}

			any = true;
			min = min.Min(projected);
			max = max.Max(projected);
			if (hasCenter)
			{
				maxRadius = Mathf.Max(maxRadius, projected.DistanceTo(projectedCenter));
			}
		}

		if (!any && !hasCenter)
		{
			return false;
		}

		center = hasCenter ? projectedCenter : (min + max) * 0.5f;
		if (!any)
		{
			float fallbackRadius = Mathf.Max(10f, maxRadius > 0f ? maxRadius : 14f);
			screenBounds = new Rect2(center - new Vector2(fallbackRadius, fallbackRadius), new Vector2(fallbackRadius * 2f, fallbackRadius * 2f));
			if (screenBounds.Intersects(new Rect2(Vector2.Zero, Size)))
			{
				return true;
			}

			center = ClampToViewport(center);
			screenBounds = new Rect2(center - new Vector2(8f, 8f), new Vector2(16f, 16f));
			return true;
		}

		screenBounds = new Rect2(min, max - min).Grow(4f);
		if (screenBounds.Size.X < 10f || screenBounds.Size.Y < 10f)
		{
			float minRadius = Mathf.Max(8f, maxRadius);
			screenBounds = new Rect2(center - new Vector2(minRadius, minRadius), new Vector2(minRadius * 2f, minRadius * 2f));
		}

		if (screenBounds.Intersects(new Rect2(Vector2.Zero, Size)))
		{
			return true;
		}

		center = ClampToViewport(center);
		screenBounds = new Rect2(center - new Vector2(8f, 8f), new Vector2(16f, 16f));
		return true;
	}

	private Vector3 ApplyPortalRemapIfNeeded(CollisionRadarEntry entry, Vector3 worldPoint)
	{
		if (!entry.RequiresPortalRemap)
		{
			return worldPoint;
		}

		WormholePortal sourcePortal = _primarySceneIsA ? _portalB : _portalA;
		if (sourcePortal == null)
		{
			return worldPoint;
		}

		Transform3D mapped = sourcePortal.BuildExitTransform(new Transform3D(Basis.Identity, worldPoint));
		return mapped.Origin;
	}

	private bool TryGetWorldBoundPoints(Node3D node, out Vector3[] points)
	{
		if (TryGetMeshBounds(node, out points))
		{
			return true;
		}

		if (TryGetCollisionShapeBounds(node, out points))
		{
			return true;
		}

		points = Array.Empty<Vector3>();
		return false;
	}

	private bool TryGetMeshBounds(Node3D node, out Vector3[] points)
	{
		MeshInstance3D mesh = FindFirstMeshInstance(node);
		if (mesh == null || mesh.Mesh == null)
		{
			points = Array.Empty<Vector3>();
			return false;
		}

		Aabb aabb = mesh.Mesh.GetAabb();
		points = BuildAabbCornerPoints(mesh.GlobalTransform, aabb);
		return true;
	}

	private static MeshInstance3D FindFirstMeshInstance(Node node)
	{
		if (node == null)
		{
			return null;
		}

		foreach (Node child in node.GetChildren())
		{
			if (child is MeshInstance3D mesh && mesh.Mesh != null)
			{
				return mesh;
			}

			MeshInstance3D nested = FindFirstMeshInstance(child);
			if (nested != null)
			{
				return nested;
			}
		}

		return null;
	}

	private bool TryGetCollisionShapeBounds(Node3D node, out Vector3[] points)
	{
		CollisionShape3D collisionShape = FindFirstCollisionShape(node);
		if (collisionShape == null || collisionShape.Shape == null)
		{
			points = Array.Empty<Vector3>();
			return false;
		}

		Aabb localAabb = collisionShape.Shape switch
		{
			BoxShape3D box => new Aabb(-box.Size * 0.5f, box.Size),
			SphereShape3D sphere => new Aabb(new Vector3(-sphere.Radius, -sphere.Radius, -sphere.Radius), Vector3.One * sphere.Radius * 2f),
			CapsuleShape3D capsule => new Aabb(new Vector3(-capsule.Radius, -(capsule.Height * 0.5f + capsule.Radius), -capsule.Radius), new Vector3(capsule.Radius * 2f, capsule.Height + capsule.Radius * 2f, capsule.Radius * 2f)),
			CylinderShape3D cylinder => new Aabb(new Vector3(-cylinder.Radius, -cylinder.Height * 0.5f, -cylinder.Radius), new Vector3(cylinder.Radius * 2f, cylinder.Height, cylinder.Radius * 2f)),
			_ => new Aabb(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.One),
		};

		points = BuildAabbCornerPoints(collisionShape.GlobalTransform, localAabb);
		return true;
	}

	private static Vector3[] BuildAabbCornerPoints(Transform3D transform, Aabb aabb)
	{
		Vector3 pos = aabb.Position;
		Vector3 end = aabb.End;
		Vector3[] local =
		{
			new Vector3(pos.X, pos.Y, pos.Z),
			new Vector3(end.X, pos.Y, pos.Z),
			new Vector3(pos.X, end.Y, pos.Z),
			new Vector3(end.X, end.Y, pos.Z),
			new Vector3(pos.X, pos.Y, end.Z),
			new Vector3(end.X, pos.Y, end.Z),
			new Vector3(pos.X, end.Y, end.Z),
			new Vector3(end.X, end.Y, end.Z),
		};

		Vector3[] world = new Vector3[local.Length];
		for (int i = 0; i < local.Length; i++)
		{
			world[i] = transform * local[i];
		}

		return world;
	}

	private bool TryProject(Vector3 worldPoint, out Vector2 screenPoint)
	{
		screenPoint = default;
		if (_mainCamera == null || _mainCamera.IsPositionBehind(worldPoint))
		{
			return false;
		}

		Vector2 viewportScreen = _mainCamera.UnprojectPosition(worldPoint);
		Vector2 viewportSize = GetViewportRect().Size;
		if (viewportSize.X <= 1f || viewportSize.Y <= 1f)
		{
			return false;
		}

		Vector2 normalized = new(
			viewportScreen.X / viewportSize.X,
			viewportScreen.Y / viewportSize.Y);
		screenPoint = new Vector2(normalized.X * Size.X, normalized.Y * Size.Y);
		return true;
	}

	private Vector2 ClampToViewport(Vector2 point)
	{
		float margin = 18f;
		return new Vector2(
			Mathf.Clamp(point.X, margin, Math.Max(margin, Size.X - margin)),
			Mathf.Clamp(point.Y, margin, Math.Max(margin, Size.Y - margin)));
	}
}
