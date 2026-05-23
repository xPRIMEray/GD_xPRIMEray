using Godot;

/// <summary>
/// Runtime-only observability choreography for traversal emergence captures.
/// It stages existing measured overlays over time and never changes transport,
/// scheduler, integrator, hit selection, or oracle semantics.
/// </summary>
public partial class TraversalEmergenceSequencer : Node
{
	public enum TraversalEmergenceStage
	{
		TraversalCompletion = 0,
		OwnershipBasins = 1,
		TransportDisagreement = 2,
		ContinuityRisk = 3,
		StabilizedObservatory = 4,
	}

	[Export] public NodePath OverlayPath = new("../CanvasLayer/FilmOverlay2D");
	[Export] public NodePath HudPath = new("../CanvasLayer/DemoHud");
	[Export] public NodePath RendererPath = new("../FixtureGrinBasicVisual/RayBeamRenderer");
	[Export] public NodePath FilmCameraPath = new("../GrinFilmCamera");

	[Export] public bool AutoStart = false;
	[Export] public bool Loop = false;
	[Export] public bool EnableTimedAdvance = true;
	[Export] public bool EnableManualStepping = true;
	[Export] public bool EnableManualHotkeys = true;
	[Export(PropertyHint.Range, "0.5,30.0,0.1")] public double StageDurationSeconds = 4.0;

	/// <summary>
	/// Off by default: Stage D must not imply localized disagreement unless a
	/// measured disagreement mask exists. This fallback exposes broad continuity
	/// diagnostics for prototype review only.
	/// </summary>
	[Export] public bool EnableFallbackContinuityWithoutDisagreementMask = false;

	private FilmOverlay2D _overlay;
	private GrinObserveDemoHud _hud;
	private RayBeamRenderer _renderer;
	private GrinFilmCamera _filmCamera;
	private TraversalEmergenceStage _currentStage = TraversalEmergenceStage.TraversalCompletion;
	private double _elapsedInStage;
	private bool _running;
	private bool _savedFixtureDebugHitColoringKnown;
	private bool _savedFixtureDebugHitColoring;

	public TraversalEmergenceStage CurrentStage => _currentStage;
	public bool IsRunning => _running;

	public override void _Ready()
	{
		ResolveNodes();

		if (AutoStart)
			StartSequence();
	}

	public override void _Process(double delta)
	{
		if (!_running || !EnableTimedAdvance)
			return;

		_elapsedInStage += Mathf.Max(0.0, delta);
		if (_elapsedInStage >= Mathf.Max(0.1, StageDurationSeconds))
			AdvanceStage();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!EnableManualStepping || !EnableManualHotkeys)
			return;

		if (e is not InputEventKey key || !key.Pressed || key.Echo)
			return;

		if (!key.CtrlPressed || !key.AltPressed || key.ShiftPressed)
			return;

		if (key.Keycode == Key.Right)
		{
			AdvanceStage();
			GetViewport()?.SetInputAsHandled();
		}
		else if (key.Keycode == Key.Left)
		{
			RetreatStage();
			GetViewport()?.SetInputAsHandled();
		}
	}

	public void StartSequence()
	{
		ResolveNodes();
		SaveInitialVisualizationState();
		_running = true;
		SetStage(TraversalEmergenceStage.TraversalCompletion);
	}

	public void StopSequence()
	{
		_running = false;
		_elapsedInStage = 0.0;
		RestoreInitialVisualizationState();
	}

	public void AdvanceStage()
	{
		int next = (int)_currentStage + 1;
		int max = (int)TraversalEmergenceStage.StabilizedObservatory;
		if (next > max)
		{
			if (!Loop)
			{
				_running = false;
				SetStage(TraversalEmergenceStage.StabilizedObservatory);
				return;
			}

			next = 0;
		}

		SetStage((TraversalEmergenceStage)next);
	}

	public void RetreatStage()
	{
		int next = (int)_currentStage - 1;
		if (next < 0)
			next = Loop ? (int)TraversalEmergenceStage.StabilizedObservatory : 0;

		SetStage((TraversalEmergenceStage)next);
	}

	public void SetStage(TraversalEmergenceStage stage)
	{
		ResolveNodes();
		_currentStage = stage;
		_elapsedInStage = 0.0;
		ApplyStage(stage);
		UpdateHudLabel(stage);
		GD.Print($"[TraversalEmergenceSequencer] stage={stage} label=\"{StageLabel(stage)}\"");
	}

	private void ResolveNodes()
	{
		if (_overlay == null || !GodotObject.IsInstanceValid(_overlay))
			_overlay = GetNodeOrNull<FilmOverlay2D>(OverlayPath);
		if (_hud == null || !GodotObject.IsInstanceValid(_hud))
			_hud = GetNodeOrNull<GrinObserveDemoHud>(HudPath);
		if (_renderer == null || !GodotObject.IsInstanceValid(_renderer))
			_renderer = GetNodeOrNull<RayBeamRenderer>(RendererPath);
		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera))
			_filmCamera = GetNodeOrNull<GrinFilmCamera>(FilmCameraPath);
	}

	private void SaveInitialVisualizationState()
	{
		if (_savedFixtureDebugHitColoringKnown)
			return;
		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera))
			return;

		_savedFixtureDebugHitColoring = _filmCamera.FixtureDebugHitColoringEnabled;
		_savedFixtureDebugHitColoringKnown = true;
	}

	private void RestoreInitialVisualizationState()
	{
		if (!_savedFixtureDebugHitColoringKnown)
			return;
		if (_filmCamera != null && GodotObject.IsInstanceValid(_filmCamera))
			_filmCamera.FixtureDebugHitColoringEnabled = _savedFixtureDebugHitColoring;

		_savedFixtureDebugHitColoringKnown = false;
	}

	private void ApplyStage(TraversalEmergenceStage stage)
	{
		SetFixtureClassificationColoring(stage is TraversalEmergenceStage.OwnershipBasins
			or TraversalEmergenceStage.TransportDisagreement);

		switch (stage)
		{
			case TraversalEmergenceStage.TraversalCompletion:
				ApplyOverlay(
					drawRays: false,
					drawHitNormals: false,
					drawFilmGradientNormals: false,
					showComparisonGrid: false,
					showComparisonCrosshair: false,
					showTraversalOverlay: true,
					showTraversalMinimap: true);
				ApplyRenderer(RayBeamRenderer.DebugDrawMode.Off, debugDrawOnlyHits: true);
				break;

			case TraversalEmergenceStage.OwnershipBasins:
				ApplyOverlay(
					drawRays: true,
					drawHitNormals: false,
					drawFilmGradientNormals: false,
					showComparisonGrid: false,
					showComparisonCrosshair: false,
					showTraversalOverlay: false,
					showTraversalMinimap: true);
				ApplyRenderer(RayBeamRenderer.DebugDrawMode.RaysOnly, debugDrawOnlyHits: true);
				break;

			case TraversalEmergenceStage.TransportDisagreement:
				ApplyOverlay(
					drawRays: true,
					drawHitNormals: false,
					drawFilmGradientNormals: false,
					showComparisonGrid: true,
					showComparisonCrosshair: true,
					showTraversalOverlay: false,
					showTraversalMinimap: false);
				ApplyRenderer(RayBeamRenderer.DebugDrawMode.RaysOnly, debugDrawOnlyHits: true);
				break;

			case TraversalEmergenceStage.ContinuityRisk:
				ApplyOverlay(
					drawRays: false,
					drawHitNormals: false,
					drawFilmGradientNormals: EnableFallbackContinuityWithoutDisagreementMask,
					showComparisonGrid: false,
					showComparisonCrosshair: EnableFallbackContinuityWithoutDisagreementMask,
					showTraversalOverlay: false,
					showTraversalMinimap: false);
				ApplyRenderer(
					EnableFallbackContinuityWithoutDisagreementMask
						? RayBeamRenderer.DebugDrawMode.RaysAndNormals
						: RayBeamRenderer.DebugDrawMode.Off,
					debugDrawOnlyHits: true);
				break;

			case TraversalEmergenceStage.StabilizedObservatory:
				ApplyOverlay(
					drawRays: false,
					drawHitNormals: false,
					drawFilmGradientNormals: false,
					showComparisonGrid: false,
					showComparisonCrosshair: false,
					showTraversalOverlay: false,
					showTraversalMinimap: true);
				ApplyRenderer(RayBeamRenderer.DebugDrawMode.Off, debugDrawOnlyHits: true);
				break;
		}
	}

	private void ApplyOverlay(
		bool drawRays,
		bool drawHitNormals,
		bool drawFilmGradientNormals,
		bool showComparisonGrid,
		bool showComparisonCrosshair,
		bool showTraversalOverlay,
		bool showTraversalMinimap)
	{
		if (_overlay == null || !GodotObject.IsInstanceValid(_overlay))
			return;

		_overlay.DrawRays = drawRays;
		_overlay.DrawHitNormals = drawHitNormals;
		_overlay.DrawFilmGradientNormals = drawFilmGradientNormals;
		_overlay.ShowComparisonGrid = showComparisonGrid;
		_overlay.ShowComparisonCrosshair = showComparisonCrosshair;
		_overlay.ShowTraversalOverlay = showTraversalOverlay;
		_overlay.ShowTraversalMinimap = showTraversalMinimap;
		_overlay.QueueRedraw();
	}

	private void ApplyRenderer(RayBeamRenderer.DebugDrawMode debugMode, bool debugDrawOnlyHits)
	{
		if (_renderer == null || !GodotObject.IsInstanceValid(_renderer))
			return;

		_renderer.DebugMode = debugMode;
		_renderer.DebugDrawOnlyHits = debugDrawOnlyHits;
	}

	private void SetFixtureClassificationColoring(bool enabled)
	{
		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera))
			return;

		_filmCamera.FixtureDebugHitColoringEnabled = enabled;
	}

	private void UpdateHudLabel(TraversalEmergenceStage stage)
	{
		if (_hud == null || !GodotObject.IsInstanceValid(_hud))
			return;

		_hud.ObservatoryModeName = $"Traversal Emergence - {StageLabel(stage)}";
	}

	private string StageLabel(TraversalEmergenceStage stage) => stage switch
	{
		TraversalEmergenceStage.TraversalCompletion => "Stage A: Traversal Completion",
		TraversalEmergenceStage.OwnershipBasins => "Stage B: Ownership Basins",
		TraversalEmergenceStage.TransportDisagreement => "Stage C: Transport Disagreement",
		TraversalEmergenceStage.ContinuityRisk => EnableFallbackContinuityWithoutDisagreementMask
			? "Stage D: Continuity / Risk"
			: "Stage D: Continuity / Risk (awaiting measured disagreement mask)",
		TraversalEmergenceStage.StabilizedObservatory => "Stage E: Stabilized Observatory",
		_ => string.Empty,
	};
}
