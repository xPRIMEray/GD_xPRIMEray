using Godot;
using System;
using System.Text;

public partial class WireframeReferenceOverlay : Control
{
	private const float PortalRadius = 2.0f;
	private const float ShellRadius = 2.12f;
	private const float FieldRadius = 3.15f;

	private Camera3D _mainCamera;
	private Node3D _portalA;
	private Node3D _portalB;
	private Node3D _probeWall;
	private Node3D _backdropA;
	private Node3D _backdropB;
	private string _debugPrimitiveSummary = string.Empty;
	private bool _suppressedEdgeOnPlanes;
	private float _dominantSegmentLength;
	private string _dominantSegmentLabel = string.Empty;
	private float _dominantVerticalSegmentLength;
	private string _dominantVerticalSegmentLabel = string.Empty;

	public bool OverlayEnabled { get; set; }
	public bool ShowPortalMouthRings { get; set; } = true;
	public bool ShowShellRings { get; set; } = true;
	public bool ShowFieldShellRings { get; set; } = true;
	public bool ShowBackdropAndProbePlanes { get; set; } = true;
	public bool ShowCenterAnchor { get; set; }
	public bool AllowEdgeOnPlanesForDebug { get; set; }
	public float EdgeOnPlaneDotThreshold { get; set; } = 0.08f;
	public float OverlayOpacity { get; set; } = 0.86f;
	public string ModeLabel { get; set; } = "WIRELINE REFERENCE";

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
		_portalA = portalA;
		_portalB = portalB;
		_probeWall = probeWall;
		_backdropA = backdropA;
		_backdropB = backdropB;
	}

	public override void _Draw()
	{
		if (!OverlayEnabled || _mainCamera == null || _portalA == null || _portalB == null)
		{
			return;
		}

		_suppressedEdgeOnPlanes = false;
		_dominantSegmentLength = 0f;
		_dominantSegmentLabel = string.Empty;
		_dominantVerticalSegmentLength = 0f;
		_dominantVerticalSegmentLabel = string.Empty;

		Color portalAColor = new(1f, 0.62f, 0.28f, 0.92f * OverlayOpacity);
		Color portalBColor = new(0.34f, 0.88f, 1f, 0.92f * OverlayOpacity);
		Color shellColor = new(1f, 1f, 1f, 0.68f * OverlayOpacity);
		Color fieldAColor = new(0.42f, 0.92f, 0.66f, 0.48f * OverlayOpacity);
		Color fieldBColor = new(0.28f, 0.74f, 1f, 0.48f * OverlayOpacity);
		Color probeColor = new(0.34f, 0.58f, 1f, 0.74f * OverlayOpacity);
		Color backdropAColor = new(1f, 0.54f, 0.26f, 0.42f * OverlayOpacity);
		Color backdropBColor = new(0.34f, 0.58f, 1f, 0.42f * OverlayOpacity);
		Color centerColor = new(1f, 0.92f, 0.34f, 0.92f * OverlayOpacity);

		if (ShowPortalMouthRings)
		{
			DrawProjectedRing("portalA", _portalA.GlobalTransform, PortalRadius, portalAColor, 2.5f, 36);
			DrawProjectedRing("portalB", _portalB.GlobalTransform, PortalRadius, portalBColor, 2.5f, 36);
		}

		if (ShowShellRings)
		{
			DrawProjectedRing("shellA", _portalA.GlobalTransform, ShellRadius, shellColor, 1.6f, 40);
			DrawProjectedRing("shellB", _portalB.GlobalTransform, ShellRadius, shellColor, 1.6f, 40);
		}

		if (ShowFieldShellRings)
		{
			DrawProjectedRing("fieldA", _portalA.GlobalTransform, FieldRadius, fieldAColor, 1.4f, 44);
			DrawProjectedRing("fieldB", _portalB.GlobalTransform, FieldRadius, fieldBColor, 1.4f, 44);
		}

		if (ShowBackdropAndProbePlanes)
		{
			DrawProjectedPlaneBox("probe", _probeWall, new Vector2(24f, 14f), probeColor, 2f);
			DrawProjectedPlaneBox("backdropA", _backdropA, new Vector2(24f, 14f), backdropAColor, 1.6f);
			DrawProjectedPlaneBox("backdropB", _backdropB, new Vector2(24f, 14f), backdropBColor, 1.6f);
		}

		if (ShowCenterAnchor)
		{
			Vector2 center = GetRect().Size * 0.5f;
			DrawRect(new Rect2(center + new Vector2(-12f, -1f), new Vector2(24f, 2f)), centerColor, true);
			DrawRect(new Rect2(center + new Vector2(-1f, -12f), new Vector2(2f, 24f)), centerColor, true);
			DrawRect(new Rect2(center + new Vector2(-3f, -3f), new Vector2(6f, 6f)), centerColor, true);
			DrawArc(center, 18f, 0f, Mathf.Tau, 32, new Color(centerColor, 0.42f), 1.5f);
		}

		_debugPrimitiveSummary = BuildPrimitiveSummary();
		DrawOverlayLabel();
	}

	private void DrawOverlayLabel()
	{
		Color labelBackdrop = new(0.04f, 0.06f, 0.1f, 0.78f * OverlayOpacity);
		Color labelText = new(0.92f, 0.97f, 1f, 0.95f * OverlayOpacity);
		Color noteText = new(0.86f, 0.92f, 0.98f, 0.88f * OverlayOpacity);
		float labelHeight = _suppressedEdgeOnPlanes ? 52f : 50f;
		DrawRect(new Rect2(16f, 16f, 320f, labelHeight), labelBackdrop, true);
		DrawString(ThemeDB.FallbackFont, new Vector2(26f, 36f), ModeLabel, HorizontalAlignment.Left, -1f, 14, labelText);
		DrawString(ThemeDB.FallbackFont, new Vector2(26f, 52f), _debugPrimitiveSummary, HorizontalAlignment.Left, -1f, 11, noteText);
	}

	private string BuildPrimitiveSummary()
	{
		StringBuilder sb = new();
		if (ShowPortalMouthRings) sb.Append("portal ");
		if (ShowShellRings) sb.Append("shell ");
		if (ShowFieldShellRings) sb.Append("field ");
		if (ShowBackdropAndProbePlanes) sb.Append(_suppressedEdgeOnPlanes ? "planes=culled " : "planes ");
		if (ShowCenterAnchor) sb.Append("yellow=center-anchor ");
		if (!string.IsNullOrWhiteSpace(_dominantVerticalSegmentLabel))
		{
			sb.Append($"dominant-vertical={_dominantVerticalSegmentLabel} ");
		}
		else if (!string.IsNullOrWhiteSpace(_dominantSegmentLabel))
		{
			sb.Append($"dominant={_dominantSegmentLabel} ");
		}
		if (!AllowEdgeOnPlanesForDebug) sb.Append("edge-on-planes=culled");
		return sb.ToString().TrimEnd();
	}

	private void DrawProjectedRing(string label, Transform3D transform, float radius, Color color, float width, int steps)
	{
		Vector2? previous = null;
		for (int i = 0; i <= steps; i++)
		{
			float angle = i / (float)steps * Mathf.Tau;
			Vector3 worldPoint = transform.Origin
				+ transform.Basis.X * Mathf.Cos(angle) * radius
				+ transform.Basis.Y * Mathf.Sin(angle) * radius;
			if (TryProject(worldPoint, out Vector2 screen))
			{
				if (previous.HasValue)
				{
					DrawTrackedLine(label, previous.Value, screen, color, width);
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
		screenPoint = new Vector2(
			normalized.X * Size.X,
			normalized.Y * Size.Y);
		return new Rect2(Vector2.Zero, Size).HasPoint(screenPoint);
	}
}
