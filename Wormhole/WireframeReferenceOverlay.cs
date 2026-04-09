using Godot;
using System;
using System.Text;

/// <summary>
/// Projected semantic glyph overlay for dual-reality research. This remains distinct from:
/// - StraightTransportReference: literal cached straight-path render
/// - DiagnosticOverlay: scalar heatmaps / curvature-like overlays
/// It only draws projected scene/entity symbols using the active camera and straight projection.
/// </summary>
public partial class WireframeReferenceOverlay : Control
{
	private const float DefaultPortalRadius = 2.0f;
	private const float DefaultFieldRadius = 3.15f;

	private Camera3D _mainCamera;
	private WormholePortal _portalA;
	private WormholePortal _portalB;
	private Node3D _probeWall;
	private Node3D _backdropA;
	private Node3D _backdropB;
	private FieldSource3D _fieldA;
	private FieldSource3D _fieldB;
	private BoundaryLayerVolume _boundaryA;
	private BoundaryLayerVolume _boundaryB;

	private string _debugPrimitiveSummary = string.Empty;
	private bool _suppressedEdgeOnPlanes;
	private float _dominantSegmentLength;
	private string _dominantSegmentLabel = string.Empty;
	private float _dominantVerticalSegmentLength;
	private string _dominantVerticalSegmentLabel = string.Empty;

	public bool OverlayEnabled { get; set; }
	public bool ShowFieldGlyphs { get; set; } = true;
	public bool ShowBoundaryLayerGlyphs { get; set; } = true;
	public bool ShowWormholePortalGlyphs { get; set; } = true;
	public bool ShowBackdropAndProbeHelpers { get; set; } = true;
	public bool ShowCenterAnchor { get; set; }
	public bool AllowEdgeOnPlanesForDebug { get; set; }
	public float EdgeOnPlaneDotThreshold { get; set; } = 0.08f;
	public float OverlayOpacity { get; set; } = 0.86f;
	public string ModeLabel { get; set; } = "WIREFRAME REFERENCE OVERLAY";
	public string DebugPrimitiveSummary => _debugPrimitiveSummary;
	public string DominantVerticalPrimitiveLabel => _dominantVerticalSegmentLabel;

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

	public void Configure(
		Camera3D mainCamera,
		Node3D portalA,
		Node3D portalB,
		Node3D probeWall,
		Node3D backdropA,
		Node3D backdropB)
	{
		_mainCamera = mainCamera;
		_portalA = portalA as WormholePortal;
		_portalB = portalB as WormholePortal;
		_probeWall = probeWall;
		_backdropA = backdropA;
		_backdropB = backdropB;

		_fieldA = ResolveFieldGlyphSource(_portalA);
		_fieldB = ResolveFieldGlyphSource(_portalB);
		_boundaryA = ResolveBoundaryGlyphSource(_portalA);
		_boundaryB = ResolveBoundaryGlyphSource(_portalB);
	}

	public override void _Draw()
	{
		if (!OverlayEnabled || _mainCamera == null)
		{
			return;
		}

		_suppressedEdgeOnPlanes = false;
		_dominantSegmentLength = 0f;
		_dominantSegmentLabel = string.Empty;
		_dominantVerticalSegmentLength = 0f;
		_dominantVerticalSegmentLabel = string.Empty;

		if (ShowFieldGlyphs)
		{
			DrawFieldGlyph("fieldA", _fieldA, new Color(0.36f, 0.94f, 0.68f, 0.92f * OverlayOpacity));
			DrawFieldGlyph("fieldB", _fieldB, new Color(0.28f, 0.76f, 1f, 0.92f * OverlayOpacity));
		}

		if (ShowBoundaryLayerGlyphs)
		{
			DrawBoundaryGlyph("boundaryA", _boundaryA, new Color(1f, 0.92f, 0.42f, 0.92f * OverlayOpacity), "BLV A");
			DrawBoundaryGlyph("boundaryB", _boundaryB, new Color(1f, 0.74f, 0.26f, 0.92f * OverlayOpacity), "BLV B");
		}

		if (ShowWormholePortalGlyphs)
		{
			DrawPortalGlyph("portalA", _portalA, new Color(1f, 0.58f, 0.28f, 0.96f * OverlayOpacity), "PORTAL A");
			DrawPortalGlyph("portalB", _portalB, new Color(0.34f, 0.88f, 1f, 0.96f * OverlayOpacity), "PORTAL B");
		}

		if (ShowBackdropAndProbeHelpers)
		{
			DrawProjectedPlaneBox("probe", _probeWall, new Vector2(24f, 14f), new Color(0.34f, 0.58f, 1f, 0.70f * OverlayOpacity), 1.8f);
			DrawProjectedPlaneBox("backdropA", _backdropA, new Vector2(24f, 14f), new Color(1f, 0.52f, 0.24f, 0.38f * OverlayOpacity), 1.4f);
			DrawProjectedPlaneBox("backdropB", _backdropB, new Vector2(24f, 14f), new Color(0.34f, 0.58f, 1f, 0.38f * OverlayOpacity), 1.4f);
		}

		if (ShowCenterAnchor)
		{
			DrawCenterAnchor();
		}

		_debugPrimitiveSummary = BuildPrimitiveSummary();
		DrawOverlayLabel();
	}

	private static FieldSource3D ResolveFieldGlyphSource(Node node)
	{
		return node?.GetNodeOrNull<FieldSource3D>("FieldSource3D");
	}

	private static BoundaryLayerVolume ResolveBoundaryGlyphSource(Node node)
	{
		return node?.GetNodeOrNull<BoundaryLayerVolume>("BoundaryShell");
	}

	private void DrawOverlayLabel()
	{
		Color labelBackdrop = new(0.04f, 0.06f, 0.1f, 0.78f * OverlayOpacity);
		Color labelText = new(0.92f, 0.97f, 1f, 0.95f * OverlayOpacity);
		Color noteText = new(0.86f, 0.92f, 0.98f, 0.88f * OverlayOpacity);
		float labelHeight = _suppressedEdgeOnPlanes ? 54f : 50f;
		DrawRect(new Rect2(16f, 16f, 380f, labelHeight), labelBackdrop, true);
		DrawString(ThemeDB.FallbackFont, new Vector2(26f, 36f), ModeLabel, HorizontalAlignment.Left, -1f, 14, labelText);
		DrawString(ThemeDB.FallbackFont, new Vector2(26f, 52f), _debugPrimitiveSummary, HorizontalAlignment.Left, -1f, 11, noteText);
	}

	private string BuildPrimitiveSummary()
	{
		StringBuilder sb = new();
		if (ShowFieldGlyphs) sb.Append("fields(xy/xz/yz) ");
		if (ShowBoundaryLayerGlyphs) sb.Append("blv(double/dashed) ");
		if (ShowWormholePortalGlyphs) sb.Append("wormhole(arcs/notch) ");
		if (ShowBackdropAndProbeHelpers) sb.Append(_suppressedEdgeOnPlanes ? "helpers=culled " : "helpers ");
		if (ShowCenterAnchor) sb.Append("center-anchor ");
		if (!string.IsNullOrWhiteSpace(_dominantVerticalSegmentLabel))
		{
			sb.Append($"dominant-vertical={_dominantVerticalSegmentLabel} ");
		}
		else if (!string.IsNullOrWhiteSpace(_dominantSegmentLabel))
		{
			sb.Append($"dominant={_dominantSegmentLabel} ");
		}
		if (!AllowEdgeOnPlanesForDebug) sb.Append("edge-on-helpers=culled");
		return sb.ToString().TrimEnd();
	}

	private void DrawFieldGlyph(string label, FieldSource3D field, Color baseColor)
	{
		if (field == null || !GodotObject.IsInstanceValid(field) || !field.Enabled)
		{
			return;
		}

		float radius = ResolveFieldRadius(field);
		Transform3D t = field.GlobalTransform;
		DrawProjectedPlanarCircle($"{label}.xy", t.Origin, t.Basis.X, t.Basis.Y, radius, baseColor, 1.7f, 48);
		DrawProjectedPlanarCircle($"{label}.xz", t.Origin, t.Basis.X, t.Basis.Z, radius, new Color(0.46f, 0.88f, 1f, baseColor.A), 1.5f, 48);
		DrawProjectedPlanarCircle($"{label}.yz", t.Origin, t.Basis.Y, t.Basis.Z, radius, new Color(1f, 0.84f, 0.36f, baseColor.A * 0.92f), 1.5f, 48);
		DrawProjectedCenterLabel(field.GlobalPosition, field.Name, baseColor);
	}

	private void DrawBoundaryGlyph(string label, BoundaryLayerVolume boundary, Color color, string shortLabel)
	{
		if (boundary == null || !GodotObject.IsInstanceValid(boundary) || !boundary.Enabled)
		{
			return;
		}

		float radius = Mathf.Max(0.25f, boundary.Radius);
		Transform3D t = boundary.GlobalTransform;
		DrawProjectedPlanarCircle($"{label}.outer", t.Origin, t.Basis.X, t.Basis.Y, radius, color, 2.4f, 48);
		DrawProjectedPlanarCircle($"{label}.inner", t.Origin, t.Basis.X, t.Basis.Y, radius * 0.88f, new Color(color, 0.72f), 1.6f, 48, dashed: true, dashPeriod: 2);
		DrawProjectedPlanarCircle($"{label}.cross", t.Origin, t.Basis.X, t.Basis.Z, radius * 0.92f, new Color(color, 0.48f), 1.3f, 36, dashed: true, dashPeriod: 3);
		DrawProjectedCenterLabel(boundary.GlobalPosition, shortLabel, color);
	}

	private void DrawPortalGlyph(string label, WormholePortal portal, Color color, string shortLabel)
	{
		if (portal == null || !GodotObject.IsInstanceValid(portal))
		{
			return;
		}

		float radius = Mathf.Max(0.35f, portal.Radius);
		Transform3D t = portal.GlobalTransform;
		DrawProjectedPlanarCircle($"{label}.arcOuterA", t.Origin, t.Basis.X, t.Basis.Y, radius, color, 2.6f, 40, startAngle: -2.55f, endAngle: -0.42f);
		DrawProjectedPlanarCircle($"{label}.arcOuterB", t.Origin, t.Basis.X, t.Basis.Y, radius, color, 2.6f, 40, startAngle: 0.42f, endAngle: 2.15f);
		DrawProjectedPlanarCircle($"{label}.arcInner", t.Origin + t.Basis.Z * 0.08f, t.Basis.X, t.Basis.Y, radius * 0.82f, new Color(color, 0.66f), 1.6f, 30, startAngle: -1.75f, endAngle: 1.25f);

		Vector3 notchBase = t.Origin + t.Basis.X * radius * 1.03f;
		Vector3 notchTip = notchBase + t.Basis.Z * (radius * 0.42f);
		Vector3 notchWing = notchBase + t.Basis.Y * (radius * 0.18f);
		if (TryProject(notchBase, out Vector2 a) && TryProject(notchTip, out Vector2 b))
		{
			DrawTrackedLine($"{label}.notch", a, b, color, 2.2f);
		}
		if (TryProject(notchBase, out Vector2 c) && TryProject(notchWing, out Vector2 d))
		{
			DrawTrackedLine($"{label}.notchWing", c, d, new Color(color, 0.8f), 1.7f);
		}

		DrawProjectedCenterLabel(portal.GlobalPosition, shortLabel, color);
	}

	private void DrawCenterAnchor()
	{
		Color centerColor = new(1f, 0.92f, 0.34f, 0.92f * OverlayOpacity);
		Vector2 center = GetRect().Size * 0.5f;
		DrawRect(new Rect2(center + new Vector2(-12f, -1f), new Vector2(24f, 2f)), centerColor, true);
		DrawRect(new Rect2(center + new Vector2(-1f, -12f), new Vector2(2f, 24f)), centerColor, true);
		DrawRect(new Rect2(center + new Vector2(-3f, -3f), new Vector2(6f, 6f)), centerColor, true);
		DrawArc(center, 18f, 0f, Mathf.Tau, 32, new Color(centerColor, 0.42f), 1.5f);
	}

	private void DrawProjectedCenterLabel(Vector3 worldPosition, string text, Color color)
	{
		if (!TryProject(worldPosition, out Vector2 center))
		{
			return;
		}

		Vector2 pos = center + new Vector2(8f, -8f);
		DrawString(ThemeDB.FallbackFont, pos, text, HorizontalAlignment.Left, -1f, 10, new Color(color, 0.9f));
	}

	private float ResolveFieldRadius(FieldSource3D field)
	{
		float radius = field.ROuter > 0f ? field.ROuter : DefaultFieldRadius;
		return Mathf.Max(0.25f, radius);
	}

	private void DrawProjectedPlanarCircle(
		string label,
		Vector3 origin,
		Vector3 axisA,
		Vector3 axisB,
		float radius,
		Color color,
		float width,
		int steps,
		float startAngle = 0f,
		float endAngle = Mathf.Tau,
		bool dashed = false,
		int dashPeriod = 2)
	{
		Vector2? previous = null;
		int dashCounter = 0;
		Vector3 a = axisA.Normalized();
		Vector3 b = axisB.Normalized();
		for (int i = 0; i <= steps; i++)
		{
			float t = i / (float)steps;
			float angle = Mathf.Lerp(startAngle, endAngle, t);
			Vector3 worldPoint = origin + a * Mathf.Cos(angle) * radius + b * Mathf.Sin(angle) * radius;
			if (TryProject(worldPoint, out Vector2 screen))
			{
				if (previous.HasValue)
				{
					bool drawSegment = !dashed || ((dashCounter / Mathf.Max(1, dashPeriod)) % 2 == 0);
					if (drawSegment)
					{
						DrawTrackedLine(label, previous.Value, screen, color, width);
					}
					dashCounter++;
				}
				previous = screen;
			}
			else
			{
				previous = null;
			}
		}
	}

	private void DrawProjectedPlaneBox(string label, Node3D node, Vector2 size, Color color, float width)
	{
		if (node == null || !GodotObject.IsInstanceValid(node))
		{
			return;
		}

		if (!AllowEdgeOnPlanesForDebug && IsPlaneNearlyEdgeOn(node))
		{
			_suppressedEdgeOnPlanes = true;
			return;
		}

		Vector3 hx = node.GlobalTransform.Basis.X * (size.X * 0.5f);
		Vector3 hy = node.GlobalTransform.Basis.Y * (size.Y * 0.5f);
		Vector3 origin = node.GlobalTransform.Origin;
		Vector3[] corners =
		{
			origin - hx - hy,
			origin + hx - hy,
			origin + hx + hy,
			origin - hx + hy,
		};

		Vector2[] projected = new Vector2[corners.Length];
		for (int i = 0; i < corners.Length; i++)
		{
			if (!TryProject(corners[i], out projected[i]))
			{
				return;
			}
		}

		for (int i = 0; i < projected.Length; i++)
		{
			Vector2 a = projected[i];
			Vector2 b = projected[(i + 1) % projected.Length];
			DrawTrackedLine(label, a, b, color, width);
		}
	}

	private void DrawTrackedLine(string label, Vector2 a, Vector2 b, Color color, float width)
	{
		DrawLine(a, b, color, width, true);
		float length = a.DistanceTo(b);
		if (length > _dominantSegmentLength)
		{
			_dominantSegmentLength = length;
			_dominantSegmentLabel = label;
		}

		float dx = Mathf.Abs(a.X - b.X);
		float dy = Mathf.Abs(a.Y - b.Y);
		if (dy > Size.Y * 0.35f && dx < 6f && length > _dominantVerticalSegmentLength)
		{
			_dominantVerticalSegmentLength = length;
			_dominantVerticalSegmentLabel = label;
		}
	}

	private bool IsPlaneNearlyEdgeOn(Node3D node)
	{
		if (_mainCamera == null)
		{
			return false;
		}

		Vector3 planeNormal = node.GlobalTransform.Basis.Z.Normalized();
		Vector3 toCamera = (_mainCamera.GlobalPosition - node.GlobalPosition).Normalized();
		float facing = Mathf.Abs(planeNormal.Dot(toCamera));
		return facing < Mathf.Clamp(EdgeOnPlaneDotThreshold, 0f, 0.5f);
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
		return new Rect2(Vector2.Zero, Size).HasPoint(screenPoint);
	}
}
