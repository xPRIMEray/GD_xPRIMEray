using Godot;
using System;
using System.IO;

public partial class WormholeValidationProbe : Node
{
	[ExportGroup("References")]
	[Export] public NodePath FilmCameraPath;
	[Export] public NodePath RayBeamRendererPath;

	[ExportGroup("Capture")]
	[Export] public bool CaptureFilmPng = true;
	[Export] public bool CaptureViewportPng = true;
	[Export(PropertyHint.Range, "0.1,30,0.1")] public float TriggerDelaySeconds = 4.0f;
	[Export] public string FilmCapturePath = "res://output/wormhole_test/wormhole_validation_film.png";
	[Export] public string ViewportCapturePath = "res://output/wormhole_test/wormhole_validation_viewport.png";
	[Export] public string SummaryLabel = "wormhole_validation";

	private GrinFilmCamera _filmCamera;
	private RayBeamRenderer _rayBeamRenderer;
	private ulong _startTicksMsec;
	private bool _triggered;

	public override void _Ready()
	{
		_filmCamera = GetNodeOrNull<GrinFilmCamera>(FilmCameraPath);
		_rayBeamRenderer = GetNodeOrNull<RayBeamRenderer>(RayBeamRendererPath);
		_startTicksMsec = Time.GetTicksMsec();
		SetProcess(true);
		_rayBeamRenderer?.BeginBoundaryValidationRun();
		GD.Print("[WormholeValidation] probe_ready");
	}

	public override void _Process(double delta)
	{
		if (_triggered)
		{
			return;
		}

		double elapsedSec = (Time.GetTicksMsec() - _startTicksMsec) / 1000.0;
		if (elapsedSec < Mathf.Max(0.1f, TriggerDelaySeconds))
		{
			return;
		}

		_triggered = true;
		GD.Print($"[WormholeValidation] probe_trigger elapsed={elapsedSec:0.00}s");
		if (CaptureFilmPng)
			CaptureFilm();
		if (CaptureViewportPng)
			CaptureViewport();
		_rayBeamRenderer?.EmitBoundaryValidationSummary(SummaryLabel);
	}

	private void CaptureFilm()
	{
		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera))
		{
			GD.Print("[WormholeValidation] film_capture result=missing_film_camera");
			return;
		}

		if (!_filmCamera.TryCopyFilmImageForTesting(out Image image) || image == null)
		{
			GD.Print("[WormholeValidation] film_capture result=missing_film_image");
			return;
		}

		SaveImage(image, FilmCapturePath, "film_capture");
	}

	private void CaptureViewport()
	{
		Viewport viewport = GetViewport();
		if (viewport == null)
		{
			GD.Print("[WormholeValidation] viewport_capture result=missing_viewport");
			return;
		}

		Image image = viewport.GetTexture()?.GetImage();
		if (image == null)
		{
			GD.Print("[WormholeValidation] viewport_capture result=missing_viewport_image");
			return;
		}

		SaveImage(image, ViewportCapturePath, "viewport_capture");
	}

	private void SaveImage(Image image, string projectPath, string label)
	{
		try
		{
			string resolvedProjectPath = string.IsNullOrWhiteSpace(projectPath)
				? $"res://output/wormhole_test/{label}.png"
				: projectPath.Trim();
			string absolutePath = ProjectSettings.GlobalizePath(resolvedProjectPath);
			string directory = Path.GetDirectoryName(absolutePath);
			if (!string.IsNullOrWhiteSpace(directory))
				Directory.CreateDirectory(directory);

			Error error = image.SavePng(absolutePath);
			if (error == Error.Ok)
				GD.Print($"[WormholeValidation] {label} result=saved path={absolutePath}");
			else
				GD.Print($"[WormholeValidation] {label} result=save_failed path={absolutePath} error={error}");
		}
		catch (Exception ex)
		{
			GD.Print($"[WormholeValidation] {label} result=exception type={ex.GetType().Name}");
		}
	}
}
