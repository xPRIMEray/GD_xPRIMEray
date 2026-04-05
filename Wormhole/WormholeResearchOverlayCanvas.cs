using Godot;
using System;

public partial class WormholeResearchOverlayCanvas : Control
{
	[Export] public NodePath OverlayControllerPath = new("../../ResearchOverlayDebug");
	[Export] public Color GridColor = new(0.22f, 0.24f, 0.30f, 0.55f);
	[Export] public Color PortalAColor = new(0.98f, 0.58f, 0.24f, 0.95f);
	[Export] public Color PortalBColor = new(0.28f, 0.86f, 1f, 0.95f);
	[Export] public Color ShellColor = new(0.92f, 0.96f, 1f, 0.55f);
	[Export] public Color FieldAColor = new(0.38f, 0.92f, 0.62f, 0.45f);
	[Export] public Color FieldBColor = new(0.22f, 0.72f, 1f, 0.45f);
	[Export] public Color ProbeColor = new(0.24f, 0.52f, 1f, 0.78f);
	[Export] public Color BackdropAColor = new(1f, 0.46f, 0.18f, 0.25f);
	[Export] public Color BackdropBColor = new(0.24f, 0.52f, 1f, 0.25f);
	[Export] public Color CameraColor = new(0.98f, 0.90f, 0.28f, 1f);

	private WormholeResearchOverlay _overlayController;

	public override void _Ready()
	{
		_overlayController = GetNodeOrNull<WormholeResearchOverlay>(OverlayControllerPath);
	}

	public override void _Process(double delta)
	{
		QueueRedraw();
	}

	public override void _Draw()
	{
		Rect2 rect = GetRect();
		DrawRect(rect, new Color(0.04f, 0.05f, 0.08f, 1f), true);
		DrawGrid(rect);

		if (_overlayController == null || !_overlayController.TryGetSnapshot(out WormholeResearchOverlay.ResearchOverlaySnapshot snapshot) || !snapshot.Valid)
		{
			return;
		}

		Vector2 portalA = ProjectPoint(snapshot, snapshot.PortalAPosition, rect);
		Vector2 portalB = ProjectPoint(snapshot, snapshot.PortalBPosition, rect);
		Vector2 probe = ProjectPoint(snapshot, snapshot.ProbeWallPosition, rect);
		Vector2 backdropA = ProjectPoint(snapshot, snapshot.BackdropAPosition, rect);
		Vector2 backdropB = ProjectPoint(snapshot, snapshot.BackdropBPosition, rect);
		Vector2 camera = ProjectPoint(snapshot, snapshot.CameraPosition, rect);
		Vector2 cameraForward = ProjectDirection(snapshot, snapshot.CameraForward, rect.Size) * 18f;

		if (snapshot.ShowBackdrops)
		{
			DrawRect(CenteredRect(backdropA, new Vector2(28f, 14f)), BackdropAColor, true);
			DrawRect(CenteredRect(backdropB, new Vector2(28f, 14f)), BackdropBColor, true);
		}

		if (snapshot.ShowProbeGeometry)
		{
			DrawRect(CenteredRect(probe, new Vector2(26f, 12f)), ProbeColor, true);
		}

		if (snapshot.ShowFieldShells)
		{
			DrawArc(portalA, 18f, 0f, Mathf.Tau, 48, FieldAColor, 3f);
			DrawArc(portalB, 18f, 0f, Mathf.Tau, 48, FieldBColor, 3f);
		}

		DrawArc(portalA, 12f, 0f, Mathf.Tau, 48, ShellColor, 2f);
		DrawArc(portalB, 12f, 0f, Mathf.Tau, 48, ShellColor, 2f);
		DrawCircle(portalA, 8f, PortalAColor);
		DrawCircle(portalB, 8f, PortalBColor);

		DrawCircle(camera, 4.5f, CameraColor);
		DrawLine(camera, camera + cameraForward, CameraColor, 2.5f, true);
		DrawCircle(camera + cameraForward, 2.0f, CameraColor);

		DrawLine(portalA, portalB, new Color(1f, 1f, 1f, 0.18f), 1.5f, true);
	}

	private void DrawGrid(Rect2 rect)
	{
		int columns = 8;
		int rows = 5;
		for (int x = 1; x < columns; x++)
		{
			float px = rect.Position.X + rect.Size.X * x / columns;
			DrawLine(new Vector2(px, rect.Position.Y), new Vector2(px, rect.End.Y), GridColor, 1f, true);
		}
		for (int y = 1; y < rows; y++)
		{
			float py = rect.Position.Y + rect.Size.Y * y / rows;
			DrawLine(new Vector2(rect.Position.X, py), new Vector2(rect.End.X, py), GridColor, 1f, true);
		}
	}

	private static Rect2 CenteredRect(Vector2 center, Vector2 size)
	{
		return new Rect2(center - size * 0.5f, size);
	}

	private static Vector2 ProjectPoint(WormholeResearchOverlay.ResearchOverlaySnapshot snapshot, Vector3 point, Rect2 rect)
	{
		Vector3 focus = (snapshot.PortalAPosition + snapshot.PortalBPosition) * 0.5f;
		Vector3 local = point - focus;
		Vector2 projected = snapshot.ViewMode == WormholeResearchOverlay.ResearchViewMode.TopDown
			? new Vector2(local.X, -local.Z)
			: new Vector2(local.X + local.Z * 0.28f, -local.Y * 1.1f + local.Z * 0.35f);
		float scale = ComputeScale(rect.Size);
		return rect.Position + rect.Size * 0.5f + projected * scale;
	}

	private static Vector2 ProjectDirection(WormholeResearchOverlay.ResearchOverlaySnapshot snapshot, Vector3 direction, Vector2 rectSize)
	{
		Vector3 local = direction.Normalized();
		Vector2 projected = snapshot.ViewMode == WormholeResearchOverlay.ResearchViewMode.TopDown
			? new Vector2(local.X, -local.Z)
			: new Vector2(local.X + local.Z * 0.28f, -local.Y * 1.1f + local.Z * 0.35f);
		if (projected.LengthSquared() <= 1e-6f)
		{
			return Vector2.Right;
		}
		return projected.Normalized();
	}

	private static float ComputeScale(Vector2 rectSize)
	{
		float span = Mathf.Min(rectSize.X / 220f, rectSize.Y / 120f);
		return Math.Max(0.1f, span);
	}
}
