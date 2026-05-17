using Godot;

/// <summary>
/// Lightweight overlay-preset orchestrator for the xPRIMEray observatory mode system.
///
/// Responds to Ctrl+1 through Ctrl+6 and applies a coherent overlay configuration
/// across FilmOverlay2D and RayBeamRenderer without touching the F-key cockpit map.
///
/// This controller is optional — scenes without it operate identically to v0.0-pre.
/// Add it to a scene as a sibling of PlayModeVerifier to enable mode switching.
///
/// Node paths are relative to the controller's parent (typically the scene root).
/// Adjust exports in the Inspector when wiring to scenes with different layouts.
/// </summary>
public partial class ObservatoryModeController : Node
{
	public enum ObservatoryMode
	{
		None         = 0,
		Observer     = 1,  // Ctrl+1 — what the observer sees
		Geometry     = 2,  // Ctrl+2 — boundary geometry and hit structure
		Ownership    = 3,  // Ctrl+3 — transport lineage and hit-filtered overlay
		Risk         = 4,  // Ctrl+4 — continuity vectors and film gradient
		Oracle       = 5,  // Ctrl+5 — maximum diagnostic density
		Presentation = 6,  // Ctrl+6 — educational/public, minimal overlays
	}

	[Export] public NodePath OverlayPath     = new("../CanvasLayer/FilmOverlay2D");
	[Export] public NodePath HudPath         = new("../CanvasLayer/DemoHud");
	[Export] public NodePath RendererPath    = new("../FixtureGrinBasicVisual/RayBeamRenderer");
	[Export] public bool EnableModeHotkeys   = true;
	[Export] public ObservatoryMode InitialMode = ObservatoryMode.None;

	private FilmOverlay2D    _overlay;
	private GrinObserveDemoHud _hud;
	private RayBeamRenderer  _renderer;
	private ObservatoryMode  _currentMode = ObservatoryMode.None;

	public ObservatoryMode CurrentMode => _currentMode;

	public override void _Ready()
	{
		_overlay  = GetNodeOrNull<FilmOverlay2D>(OverlayPath);
		_hud      = GetNodeOrNull<GrinObserveDemoHud>(HudPath);
		_renderer = GetNodeOrNull<RayBeamRenderer>(RendererPath);

		if (InitialMode != ObservatoryMode.None)
		{
			SetMode(InitialMode);
		}
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!EnableModeHotkeys)
		{
			return;
		}

		if (e is not InputEventKey k || !k.Pressed || k.Echo)
		{
			return;
		}

		if (!k.CtrlPressed || k.AltPressed || k.ShiftPressed)
		{
			return;
		}

		ObservatoryMode? next = k.Keycode switch
		{
			Key.Key1 => ObservatoryMode.Observer,
			Key.Key2 => ObservatoryMode.Geometry,
			Key.Key3 => ObservatoryMode.Ownership,
			Key.Key4 => ObservatoryMode.Risk,
			Key.Key5 => ObservatoryMode.Oracle,
			Key.Key6 => ObservatoryMode.Presentation,
			_        => null,
		};

		if (next.HasValue)
		{
			SetMode(next.Value);
			GetViewport()?.SetInputAsHandled();
		}
	}

	public void SetMode(ObservatoryMode mode)
	{
		_currentMode = mode;
		ApplyPreset(mode);

		if (_hud != null && GodotObject.IsInstanceValid(_hud))
		{
			_hud.ObservatoryModeName = ModeLabel(mode);
		}

		GD.Print($"[ObservatoryModeController] mode={mode}  ({ModeLabel(mode)})");
	}

	// ---------------------------------------------------------------------------
	// Presets — each mode sets a coherent, named combination of overlay states.
	// Individual F-key toggles remain available and override preset state freely.
	// ---------------------------------------------------------------------------

	private void ApplyPreset(ObservatoryMode mode)
	{
		bool haveOverlay  = _overlay  != null && GodotObject.IsInstanceValid(_overlay);
		bool haveRenderer = _renderer != null && GodotObject.IsInstanceValid(_renderer);

		switch (mode)
		{
			case ObservatoryMode.Observer:
				// Minimal signal. Show where rays land; suppress geometry detail.
				if (haveOverlay)
				{
					_overlay.DrawRays               = true;
					_overlay.DrawHitNormals         = false;
					_overlay.DrawFilmGradientNormals = false;
					_overlay.ShowComparisonGrid      = false;
					_overlay.ShowComparisonCrosshair = true;
					_overlay.QueueRedraw();
				}
				if (haveRenderer)
				{
					_renderer.DebugMode        = RayBeamRenderer.DebugDrawMode.RaysOnly;
					_renderer.DebugDrawOnlyHits = true;
				}
				break;

			case ObservatoryMode.Geometry:
				// Boundary geometry and hit structure. Normals on, grid reference active.
				if (haveOverlay)
				{
					_overlay.DrawRays               = true;
					_overlay.DrawHitNormals         = true;
					_overlay.DrawFilmGradientNormals = false;
					_overlay.ShowComparisonGrid      = true;
					_overlay.ShowComparisonCrosshair = true;
					_overlay.QueueRedraw();
				}
				if (haveRenderer)
				{
					_renderer.DebugMode        = RayBeamRenderer.DebugDrawMode.RaysAndNormals;
					_renderer.DebugDrawOnlyHits = false;
				}
				break;

			case ObservatoryMode.Ownership:
				// Hit-filtered overlay surfaces transport ownership distribution.
				if (haveOverlay)
				{
					_overlay.DrawRays               = true;
					_overlay.DrawHitNormals         = true;
					_overlay.DrawFilmGradientNormals = false;
					_overlay.ShowComparisonGrid      = true;
					_overlay.ShowComparisonCrosshair = true;
					_overlay.QueueRedraw();
				}
				if (haveRenderer)
				{
					_renderer.DebugMode        = RayBeamRenderer.DebugDrawMode.RaysOnly;
					_renderer.DebugDrawOnlyHits = true;
				}
				break;

			case ObservatoryMode.Risk:
				// Film gradient normals surface continuity structure. Full overlay stack.
				if (haveOverlay)
				{
					_overlay.DrawRays               = true;
					_overlay.DrawHitNormals         = true;
					_overlay.DrawFilmGradientNormals = true;
					_overlay.ShowComparisonGrid      = true;
					_overlay.ShowComparisonCrosshair = true;
					_overlay.QueueRedraw();
				}
				if (haveRenderer)
				{
					_renderer.DebugMode        = RayBeamRenderer.DebugDrawMode.RaysAndNormals;
					_renderer.DebugDrawOnlyHits = false;
				}
				break;

			case ObservatoryMode.Oracle:
				// Maximum diagnostic density. All overlay layers active simultaneously.
				if (haveOverlay)
				{
					_overlay.DrawRays               = true;
					_overlay.DrawHitNormals         = true;
					_overlay.DrawFilmGradientNormals = true;
					_overlay.ShowComparisonGrid      = true;
					_overlay.ShowComparisonCrosshair = true;
					_overlay.QueueRedraw();
				}
				if (haveRenderer)
				{
					_renderer.DebugMode        = RayBeamRenderer.DebugDrawMode.RaysAndNormals;
					_renderer.DebugDrawOnlyHits = false;
				}
				break;

			case ObservatoryMode.Presentation:
				// Clean, readable. Rays only; no geometry noise; crosshair suppressed.
				if (haveOverlay)
				{
					_overlay.DrawRays               = true;
					_overlay.DrawHitNormals         = false;
					_overlay.DrawFilmGradientNormals = false;
					_overlay.ShowComparisonGrid      = false;
					_overlay.ShowComparisonCrosshair = false;
					_overlay.QueueRedraw();
				}
				if (haveRenderer)
				{
					_renderer.DebugMode        = RayBeamRenderer.DebugDrawMode.Off;
					_renderer.DebugDrawOnlyHits = false;
				}
				break;
		}
	}

	private static string ModeLabel(ObservatoryMode mode) => mode switch
	{
		ObservatoryMode.Observer     => "Observer Mode",
		ObservatoryMode.Geometry     => "Geometry Mode",
		ObservatoryMode.Ownership    => "Ownership Mode",
		ObservatoryMode.Risk         => "Risk / Continuity Mode",
		ObservatoryMode.Oracle       => "Oracle / Microscopy Mode",
		ObservatoryMode.Presentation => "Presentation Mode",
		_                            => string.Empty,
	};
}
