using Godot;
using System;

public partial class OverspacePortalDebugOverlay : Control
{
	private Camera3D _viewerCamera;
	private WormholePortal _sourcePortal;
	private WormholePortal _targetPortal;
	private string _activeZone = string.Empty;

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

	public void Configure(Camera3D viewerCamera)
	{
		_viewerCamera = viewerCamera;
	}

	public void SetPortalPair(WormholePortal sourcePortal, WormholePortal targetPortal, string activeZone)
	{
		_sourcePortal = sourcePortal;
		_targetPortal = targetPortal;
		_activeZone = activeZone ?? string.Empty;
	}

	public override void _Draw()
	{
		if (_viewerCamera == null || _sourcePortal == null || _targetPortal == null)
		{
			return;
		}

		if (!_sourcePortal.TryBuildThroatDebugSnapshot(_viewerCamera.GlobalPosition, out WormholePortal.ThroatDebugSnapshot sourceSnapshot))
		{
			return;
		}

		if (!_sourcePortal.EnableThroatDiagnostics && !_targetPortal.EnableThroatDiagnostics)
		{
			return;
		}

		_targetPortal.TryBuildThroatDebugSnapshot(sourceSnapshot.DestinationPreviewPoint, out WormholePortal.ThroatDebugSnapshot targetSnapshot);

		DrawPortalAxes(_sourcePortal, "SRC");
		DrawPortalAxes(_targetPortal, "DST");
		DrawZeroPhaseMarker(sourceSnapshot, new Color(1f, 0.92f, 0.36f, 0.95f), "src zero");
		DrawZeroPhaseMarker(targetSnapshot, new Color(0.68f, 0.88f, 1f, 0.92f), "dst zero");
		DrawSpinArrow(sourceSnapshot, new Color(1f, 0.72f, 0.22f, 0.95f), "src spin");
		DrawSpinArrow(targetSnapshot, new Color(0.52f, 0.84f, 1f, 0.92f), "dst spin");
		DrawRadialVector(sourceSnapshot.MouthCenter, sourceSnapshot.BoundarySamplePoint, new Color(0.28f, 0.95f, 0.82f, 0.95f), "sample");
		DrawRadialVector(_targetPortal.GlobalPosition, sourceSnapshot.DestinationPreviewPoint, new Color(0.48f, 0.86f, 1f, 0.95f), "mapped");
		DrawDestinationCompass(sourceSnapshot.DestinationPreviewPoint - _targetPortal.GlobalPosition, sourceSnapshot.PhaseAngleDegrees);
		DrawHud(sourceSnapshot, targetSnapshot);
	}

	private void DrawPortalAxes(WormholePortal portal, string label)
	{
		Vector3 origin = portal.GlobalPosition;
		float axisLength = Mathf.Max(0.6f, portal.ThroatRadius * 1.45f);
		DrawProjectedLine(origin, origin + portal.GlobalTransform.Basis.X * axisLength, new Color(1f, 0.34f, 0.34f, 0.95f), $"{label}+X");
		DrawProjectedLine(origin, origin + portal.GlobalTransform.Basis.Y * axisLength, new Color(0.32f, 1f, 0.42f, 0.95f), $"{label}+Y");
		DrawProjectedLine(origin, origin + portal.GlobalTransform.Basis.Z * axisLength, new Color(0.36f, 0.72f, 1f, 0.95f), $"{label}+Z");
	}

	private void DrawZeroPhaseMarker(WormholePortal.ThroatDebugSnapshot snapshot, Color color, string label)
	{
		if (!snapshot.Valid)
		{
			return;
		}

		DrawProjectedLine(snapshot.MouthCenter, snapshot.ZeroMarkerPoint, color, label);
	}

	private void DrawSpinArrow(WormholePortal.ThroatDebugSnapshot snapshot, Color color, string label)
	{
		if (!snapshot.Valid)
		{
			return;
		}

		DrawProjectedLine(snapshot.SpinArrowStart, snapshot.SpinArrowTip, color, label);
	}

	private void DrawRadialVector(Vector3 center, Vector3 point, Color color, string label)
	{
		DrawProjectedLine(center, point, color, label);
	}

	private void DrawDestinationCompass(Vector3 mappedDirection, float phase)
	{
		Rect2 rect = new Rect2(Size.X - 176f, 18f, 158f, 158f);
		DrawRect(rect, new Color(0.04f, 0.06f, 0.11f, 0.78f), true);
		DrawArc(rect.GetCenter(), 48f, 0f, Mathf.Tau, 48, new Color(0.42f, 0.54f, 0.66f, 0.72f), 2f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(12f, 22f), "MAPPED VECTOR", HorizontalAlignment.Left, -1f, 11, new Color(0.92f, 0.97f, 1f, 0.95f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(12f, 40f), $"phase {phase:0.0} deg", HorizontalAlignment.Left, -1f, 10, new Color(0.82f, 0.90f, 0.97f, 0.92f));

		Vector2 center = rect.GetCenter() + new Vector2(0f, 14f);
		Vector2 dir = new Vector2(mappedDirection.X, mappedDirection.Z);
		if (dir.LengthSquared() < 1e-6f)
		{
			dir = Vector2.Right;
		}

		dir = dir.Normalized();
		Vector2 tip = center + dir * 44f;
		DrawLine(center, tip, new Color(0.48f, 0.86f, 1f, 0.95f), 3f);
		DrawLine(tip, tip - dir.Rotated(0.45f) * 14f, new Color(0.48f, 0.86f, 1f, 0.95f), 3f);
		DrawLine(tip, tip - dir.Rotated(-0.45f) * 14f, new Color(0.48f, 0.86f, 1f, 0.95f), 3f);
	}

	private void DrawHud(WormholePortal.ThroatDebugSnapshot sourceSnapshot, WormholePortal.ThroatDebugSnapshot targetSnapshot)
	{
		Rect2 rect = new Rect2(18f, 18f, 420f, 142f);
		DrawRect(rect, new Color(0.04f, 0.06f, 0.11f, 0.78f), true);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(12f, 24f), "PHASE A THROAT DEBUG", HorizontalAlignment.Left, -1f, 14, new Color(0.94f, 0.98f, 1f, 0.95f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(12f, 44f), $"src {sourceSnapshot.WormholeId} -> dst {targetSnapshot.WormholeId}", HorizontalAlignment.Left, -1f, 11, new Color(0.88f, 0.94f, 1f, 0.95f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(12f, 62f), $"throat r {sourceSnapshot.ThroatRadius:0.00}  zero {sourceSnapshot.ZeroPhaseAngle:0.0} deg  phase {sourceSnapshot.PhaseAngleDegrees:0.0} deg", HorizontalAlignment.Left, -1f, 11, new Color(0.84f, 0.90f, 0.98f, 0.92f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(12f, 80f), $"spin {sourceSnapshot.SpinDirection:+#;-#;0}  handed {ResolveHandednessLabel(sourceSnapshot.Handedness)}  remap {(sourceSnapshot.PhaseLockedRemapEnabled ? "phase-locked" : "direct")}", HorizontalAlignment.Left, -1f, 11, new Color(0.84f, 0.90f, 0.98f, 0.92f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(12f, 98f), $"regions {sourceSnapshot.ParentRegionId} -> {sourceSnapshot.ChildRegionId}", HorizontalAlignment.Left, -1f, 11, new Color(0.82f, 0.96f, 0.86f, 0.94f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(12f, 116f), $"active z-zone {_activeZone}", HorizontalAlignment.Left, -1f, 11, new Color(0.82f, 0.96f, 0.86f, 0.94f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(12f, 134f), $"z-range {sourceSnapshot.ZoneMinZ:0}..{sourceSnapshot.ZoneMaxZ:0} -> {targetSnapshot.ZoneMinZ:0}..{targetSnapshot.ZoneMaxZ:0}", HorizontalAlignment.Left, -1f, 10, new Color(0.82f, 0.90f, 0.97f, 0.88f));
	}

	private static string ResolveHandednessLabel(WormholePortal.WormholeHandednessMode handedness)
	{
		return handedness == WormholePortal.WormholeHandednessMode.LeftHanded ? "LH" : "RH";
	}

	private void DrawProjectedLine(Vector3 fromWorld, Vector3 toWorld, Color color, string label, bool drawLabel = true)
	{
		if (!TryProject(fromWorld, out Vector2 a) || !TryProject(toWorld, out Vector2 b))
		{
			return;
		}

		DrawLine(a, b, color, 2.5f);
		if (drawLabel && !string.IsNullOrWhiteSpace(label))
		{
			DrawString(ThemeDB.FallbackFont, b + new Vector2(6f, -4f), label, HorizontalAlignment.Left, -1f, 10, color);
		}
	}

	private bool TryProject(Vector3 worldPoint, out Vector2 screenPoint)
	{
		screenPoint = default;
		if (_viewerCamera == null || _viewerCamera.IsPositionBehind(worldPoint))
		{
			return false;
		}

		Vector2 viewportScreen = _viewerCamera.UnprojectPosition(worldPoint);
		Vector2 viewportSize = GetViewportRect().Size;
		if (viewportSize.X <= 1f || viewportSize.Y <= 1f)
		{
			return false;
		}

		Vector2 normalized = new Vector2(viewportScreen.X / viewportSize.X, viewportScreen.Y / viewportSize.Y);
		screenPoint = new Vector2(normalized.X * Size.X, normalized.Y * Size.Y);
		return new Rect2(Vector2.Zero, Size).HasPoint(screenPoint);
	}
}
