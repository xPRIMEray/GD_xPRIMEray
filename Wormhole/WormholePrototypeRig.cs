using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>
/// Coordinates the first linked-mouth prototype:
/// - keeps only one world visible to the player camera
/// - drives both linked portal cameras
/// - teleports the traveler when crossing the active spherical shell
/// </summary>
public partial class WormholePrototypeRig : Node3D
{
	private sealed class PortalSectorArtifactCell
	{
		public int LayerIndex = -1;
		public int ThetaBin = -1;
		public int RadialBin = -1;
		public bool InvariantRing;
		public int QuerySamples;
		public int Samples;
		public bool HasCandidateState;
		public bool CandidateInvariant = true;
		public int CandidateCount = -1;
		public ulong CandidateHash = 0;
		public bool HasPositiveOverlap;
		public bool PositiveOverlapInvariant = true;
		public int PositiveOverlapCount = -1;
		public int HitSamples;
		public int BackgroundHits;
		public int SourceHits;
		public int UnclassifiedHits;
		public int AbsorbedHits;
		public int MissHits;
	}

	private sealed class PortalRingMetric
	{
		public int LayerIndex;
		public int RadialBin;
		public int OccupiedThetaCells;
		public int AdjacencyPairs;
		public int CandidateContinuityPairs;
		public int HitContinuityPairs;
		public int PositiveOverlapContinuityPairs;
		public int PositiveOverlapCells;
		public int PositiveOverlapInvariantCells;
		public int CandidateInvariantCells;
		public int TotalSamples;
		public int TotalHitSamples;
		public int MinHitSamplesPerTheta;
		public int MaxHitSamplesPerTheta;
		public float MeanSamplesPerTheta;
		public float MeanHitSamplesPerTheta;
		public float PositiveOverlapDensity;
		public float CandidateContinuityRatio;
		public float HitContinuityRatio;
		public float PositiveOverlapContinuityRatio;
		public float AngularHitVariation;
		public float AngularSampleVariation;
		public float RadialHitGradientFromPrev;
		public float RadialSampleGradientFromPrev;
		public float ProtoCausticScore;
		public bool StableAnnularHitBand;
		public bool SharpRadialTransition;
		public bool SharpAngularTransition;
	}

	private sealed class PortalUsefulnessMetric
	{
		public int LayerIndex;
		public int ThetaBin;
		public int RadialBin;
		public bool InvariantRing;
		public int QuerySamples;
		public int CandidateSamples;
		public int HitSamples;
		public int PositiveOverlapSamples;
		public string DominantHitKind = "none";
		public float QueryHitYield;
		public float QueryShare;
		public float LowValueScore;
		public float HighValueScore;
	}

	private readonly struct ProtoCausticInvariantContract
	{
		public readonly int LayerIndex;
		public readonly int RadialBin;
		public readonly float MinimumHitDensity;
		public readonly float MinimumHitContinuityRatio;
		public readonly float MinimumPositiveOverlapContinuityRatio;
		public readonly float MinimumRadialGradient;

		public ProtoCausticInvariantContract(
			int layerIndex,
			int radialBin,
			float minimumHitDensity,
			float minimumHitContinuityRatio,
			float minimumPositiveOverlapContinuityRatio,
			float minimumRadialGradient)
		{
			LayerIndex = layerIndex;
			RadialBin = radialBin;
			MinimumHitDensity = minimumHitDensity;
			MinimumHitContinuityRatio = minimumHitContinuityRatio;
			MinimumPositiveOverlapContinuityRatio = minimumPositiveOverlapContinuityRatio;
			MinimumRadialGradient = minimumRadialGradient;
		}
	}

	private sealed class ProtoCausticInvariantResult
	{
		public bool TargetFound;
		public bool Passed;
		public string FailureReason = "unknown";
		public PortalRingMetric TargetMetric;
		public float HitDensityDelta;
		public float HitContinuityDelta;
		public float PositiveOverlapContinuityDelta;
		public float RadialGradientDelta;
	}

	private readonly struct LowValueSectorBudgetContract
	{
		public readonly int LayerIndex;
		public readonly int RadialBin;
		public readonly float BaselineQueryShare;
		public readonly float MaxQueryShareScale;

		public LowValueSectorBudgetContract(
			int layerIndex,
			int radialBin,
			float baselineQueryShare,
			float maxQueryShareScale)
		{
			LayerIndex = layerIndex;
			RadialBin = radialBin;
			BaselineQueryShare = baselineQueryShare;
			MaxQueryShareScale = maxQueryShareScale;
		}

		public float MaximumAllowedQueryShare => BaselineQueryShare * MaxQueryShareScale;
	}

	private sealed class LowValueSectorBudgetResult
	{
		public bool TargetFound;
		public bool Passed;
		public string FailureReason = "unknown";
		public float ActualQueryShare;
		public float MaximumAllowedQueryShare;
		public float QueryShareDelta;
		public int TotalQuerySamples;
		public int TargetQuerySamples;
	}

	private readonly struct LowValueThrottleProfileSnapshot
	{
		public readonly bool Enabled;
		public readonly int LayerIndex;
		public readonly int RadialBin;
		public readonly string ThetaBinsCsv;
		public readonly int Period;

		public LowValueThrottleProfileSnapshot(
			bool enabled,
			int layerIndex,
			int radialBin,
			string thetaBinsCsv,
			int period)
		{
			Enabled = enabled;
			LayerIndex = layerIndex;
			RadialBin = radialBin;
			ThetaBinsCsv = thetaBinsCsv ?? string.Empty;
			Period = period;
		}
	}

	public enum DualRealityOverlayModeKind
	{
		None = 0,
		FilmHeatmap = 1,
		CurvatureHeatmap = 2
	}

	public enum CurvatureHeatmapMetricMode
	{
		CumulativeTurnAngle = 0,
		MaxLocalTurnAngle = 1,
		CurvatureMean = 2,
		CurvatureMax = 3,
		Pass1StepDensityPlaceholder = 4
	}

	public enum CurvatureHeatmapNormalizationMode
	{
		AutoPercentile = 0,
		AutoFullRange = 1,
		FixedRange = 2
	}

	public enum DualRealityWireframePlacementMode
	{
		FullscreenCurved = 0,
		StraightTransportReference = 1,
		Both = 2
	}

	public enum DualRealityCollisionRadarFilterMode
	{
		AllVisible = 0,
		HitConfirmedOnly = 1,
		PrimaryOnly = 2,
		RemappedOnly = 3,
		BackgroundOnly = 4,
		HelpersOnly = 5
	}

	public enum DualRealityCollisionRadarBoundsMode
	{
		CenterOnly = 0,
		Sphere = 1,
		Aabb = 2,
		LabelOnly = 3
	}

	[ExportGroup("References")]
	[Export] public NodePath TravelerPath;
	[Export] public NodePath MainCameraPath;
	[Export] public NodePath PortalAPath;
	[Export] public NodePath PortalBPath;
	[Export] public NodePath RayBeamRendererPath;
	[Export] public NodePath FilmCameraPath = new("GrinFilmCamera");

	[ExportGroup("Runtime")]
	[Export] public bool StartInSceneA = true;
	[Export(PropertyHint.Range, "0,1,0.01")] public float TeleportCooldownSeconds = 0.2f;

	[ExportGroup("Auto Motion")]
	[Export] public bool EnableAutoMotion = false;
	[Export(PropertyHint.Range, "0.5,64,0.1")] public float AutoMotionOrbitRadius = 8.0f;
	[Export(PropertyHint.Range, "-8,8,0.1")] public float AutoMotionHeight = 1.2f;
	[Export(PropertyHint.Range, "2,120,0.1")] public float AutoMotionPeriodSeconds = 28.0f;
	[Export(PropertyHint.Range, "-180,180,1")] public float AutoMotionStartAngleDegrees = 180.0f;
	[Export] public bool AutoMotionLookAtActivePortal = true;
	[Export(PropertyHint.Range, "0,8,0.1")] public float AutoMotionRadialWaveAmplitude = 0.0f;
	[Export(PropertyHint.Range, "2,120,0.1")] public float AutoMotionRadialWavePeriodSeconds = 18.0f;

	[ExportGroup("Validation Capture")]
	[Export] public bool CaptureValidationScreenshot = true;
	[Export(PropertyHint.Range, "1,120,0.1")] public float ValidationCaptureDelaySeconds = 20.0f;
	[Export(PropertyHint.Range, "1,180,0.1")] public float ValidationCaptureMaxDelaySeconds = 30.0f;
	[Export] public string ValidationCapturePath = "res://output/wormhole_test/wormhole_validation_capture.png";
	[Export] public bool CaptureValidationCompositeScreenshot = true;
	[Export] public string ValidationCompositeCapturePath = "res://output/wormhole_test/wormhole_validation_composed.png";
	[Export] public bool EnableDomainTelemetry = false;
	[Export] public bool EnableDomainAwareFirstHitResolver = false;
	[Export] public bool EmitBoundaryValidationSummaryAfterCapture = true;
	[Export] public string BoundaryValidationLabel = "wormhole_validation";
	[Export] public bool ValidationWaitForFilmWrap = true;
	[Export] public bool LockTravelerInputDuringValidation = true;
	[ExportGroup("Validation Camera")]
	[Export] public string ValidationCameraPosePreset = "validation_nearfield";
	[Export(PropertyHint.Range, "0,64,0.1")] public float ValidationNearfieldBackoffDistance = 0.0f;
	[Export(PropertyHint.Range, "0,64,0.1")] public float PresentationMidBackoffDistance = 5.0f;
	[Export(PropertyHint.Range, "0,64,0.1")] public float PresentationFarBackoffDistance = 10.0f;
	[Export(PropertyHint.Range, "0,64,0.1")] public float ValidationCameraBackoffDistance = 0.0f;

	[ExportGroup("Proto-Caustic Invariant")]
	[Export] public int ProtoCausticInvariantLayer = 1;
	[Export] public int ProtoCausticInvariantRadialBin = 3;
	[Export(PropertyHint.Range, "0,5000,1")] public float ProtoCausticInvariantMinHitDensity = 800.0f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ProtoCausticInvariantMinHitContinuityRatio = 0.95f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ProtoCausticInvariantMinPositiveOverlapContinuityRatio = 0.95f;
	[Export(PropertyHint.Range, "0,5000,1")] public float ProtoCausticInvariantMinRadialGradient = 600.0f;

	[ExportGroup("Low-Value Sector Budget")]
	[Export] public bool LowValueSectorBudgetEnabled = true;
	[Export] public int LowValueSectorBudgetLayer = 0;
	[Export] public int LowValueSectorBudgetRadialBin = 3;
	[Export(PropertyHint.Range, "0,1,0.0001")] public float LowValueSectorBudgetBaselineQueryShare = 0.4011f;
	[Export(PropertyHint.Range, "0,2,0.01")] public float LowValueSectorBudgetMaxQueryShareScale = 0.9f;

	[ExportGroup("Dual Reality Research")]
	[Export] public bool EnableDualRealityResearchMode = false;
	[Export] public bool DualRealityInsetEnabled = true;
	[Export(PropertyHint.Range, "0.15,0.5,0.01")] public float DualRealityInsetScale = 0.25f;
	[Export] public DualRealityOverlayModeKind DualRealityOverlayMode = DualRealityOverlayModeKind.None;
	[Export(PropertyHint.Range, "0,1,0.01")] public float DualRealityOverlayOpacity = 0.58f;
	[Export] public bool EnableCurvatureHeatmap = true;
	[Export(PropertyHint.Range, "0,1,0.01")] public float CurvatureHeatmapOpacity = 0.62f;
	[Export] public DualRealityWireframePlacementMode CurvatureHeatmapPlacement = DualRealityWireframePlacementMode.FullscreenCurved;
	[Export] public CurvatureHeatmapMetricMode CurvatureHeatmapMetric = CurvatureHeatmapMetricMode.CumulativeTurnAngle;
	[Export] public CurvatureHeatmapNormalizationMode CurvatureHeatmapNormalization = CurvatureHeatmapNormalizationMode.AutoPercentile;
	[Export(PropertyHint.Range, "0,10,0.001")] public float CurvatureHeatmapMin = 0.0f;
	[Export(PropertyHint.Range, "0.001,10,0.001")] public float CurvatureHeatmapMax = 0.35f;
	[Export] public bool CurvatureHeatmapShowLegend = true;
	[Export] public bool DualRealityWireframeReferenceOverlayEnabled = false;
	[Export] public DualRealityWireframePlacementMode DualRealityWireframePlacement = DualRealityWireframePlacementMode.FullscreenCurved;
	[Export(PropertyHint.Range, "0,1,0.01")] public float DualRealityWireframeOverlayOpacity = 0.86f;
	[Export] public bool DualRealityWireframeShowFieldGlyphs = true;
	[Export] public bool DualRealityWireframeShowBoundaryLayerGlyphs = true;
	[Export] public bool DualRealityWireframeShowWormholePortalGlyphs = true;
	[Export] public bool DualRealityWireframeShowBackdropAndProbeHelpers = true;
	[Export] public bool DualRealityWireframeShowCenterAnchor = false;
	[Export] public bool DualRealityWireframeAllowEdgeOnPlanesForDebug = false;
	[Export(PropertyHint.Range, "0,0.5,0.01")] public float DualRealityWireframeEdgeOnPlaneDotThreshold = 0.08f;
	[ExportGroup("Dual Reality Collision Radar")]
	[Export] public bool DualRealityCollisionRadarOverlayEnabled = false;
	[Export] public DualRealityWireframePlacementMode DualRealityCollisionRadarPlacement = DualRealityWireframePlacementMode.FullscreenCurved;
	[Export(PropertyHint.Range, "0,1,0.01")] public float DualRealityCollisionRadarOpacity = 0.78f;
	[Export] public bool DualRealityCollisionRadarShowPrimarySceneGeometry = true;
	[Export] public bool DualRealityCollisionRadarShowRemappedSceneGeometry = true;
	[Export] public bool DualRealityCollisionRadarShowBackgroundObjects = true;
	[Export] public bool DualRealityCollisionRadarShowProbeHelpers = true;
	[Export] public DualRealityCollisionRadarFilterMode DualRealityCollisionRadarFilter = DualRealityCollisionRadarFilterMode.AllVisible;
	[Export] public DualRealityCollisionRadarBoundsMode DualRealityCollisionRadarBounds = DualRealityCollisionRadarBoundsMode.Aabb;
	[Export] public bool DualRealityCollisionRadarShowCenterMarkers = true;
	[Export] public bool DualRealityCollisionRadarShowLabels = true;
	[Export] public bool DualRealityCollisionRadarShowLeaderLines = true;
	[Export] public bool DualRealityCollisionRadarShowLegend = true;
	[Export] public bool DualRealityFreezeBaseline = false;
	[Export] public bool DualRealityRefreshBaseline = false;
	[Export] public string DualRealityCapturePath = "res://output/dual_reality/wormhole_inset_baseline.png";

	private Node3D _traveler;
	private Camera3D _mainCamera;
	private WormholePortal _portalA;
	private WormholePortal _portalB;
	private RayBeamRenderer _rayBeamRenderer;
	private GrinFilmCamera _filmCamera;
	private WormholeResearchOverlay _researchOverlay;
	private Label _researchOverlayStatusLabel;
	private WormholePortal _activePortal;
	private float _lastActivePortalDelta;
	private double _teleportCooldownRemaining;
	private double _autoMotionElapsedSeconds;
	private double _validationCaptureElapsedSeconds;
	private bool _validationCaptureCompleted;
	private ulong _validationStartTicksMsec;
	private bool _validationPendingCapture;
	private bool _validationSawFilmProgress;
	private string _validationTriggerReason = "unknown";
	private int _teleportCount;
	private PerfFrameReport _latestPerfFrameReport;
	private bool _hasLatestPerfFrameReport;
	private ProtoCausticInvariantResult _latestProtoCausticInvariantResult;
	private LowValueSectorBudgetResult _latestLowValueSectorBudgetResult;
	private LowValueThrottleProfileSnapshot _latestLowValueThrottleProfile;
	private PortalRingMetric _latestInvariantRingMetric;
	private StraightRayReferenceCache _straightRayReferenceCache;
	private CanvasLayer _hudCanvasLayer;
	private Control _researchOverlayPanel;
	private TextureRect _researchOverlayView;
	private PanelContainer _dualRealityInsetPanel;
	private TextureRect _dualRealityBaselineView;
	private TextureRect _dualRealityOverlayView;
	private TextureRect _dualRealityOverlayFullscreenView;
	private Label _dualRealityTitleLabel;
	private Label _dualRealityModeLabel;
	private Label _dualRealityStateLabel;
	private Label _dualRealityOverlayPanelLegendLabel;
	private PanelContainer _dualRealityOverlayFullscreenLegendPanel;
	private Label _dualRealityOverlayFullscreenLegendLabel;
	private WireframeReferenceOverlay _dualRealityWireframeFullscreenOverlay;
	private WireframeReferenceOverlay _dualRealityWireframePanelOverlay;
	private CameraSpaceCollisionOverlay _dualRealityCollisionFullscreenOverlay;
	private CameraSpaceCollisionOverlay _dualRealityCollisionPanelOverlay;
	private ImageTexture _dualRealityOverlayTexture;
	private string _dualRealityLastOverlayLegendText = string.Empty;
	private ulong _dualRealityObservedStateHash;
	private int _dualRealityStableFrames;
	private double _dualRealityOverlayRefreshSeconds;
	private DualRealityOverlayModeKind _lastDualRealityOverlayMode = (DualRealityOverlayModeKind)(-1);
	private bool _dualRealityStartupHoldActive;
	private bool _dualRealityStartupHoldMainUpdateEveryFrame;
	private ulong _dualRealityStartupHoldTicksMsec;
	private bool _dualRealityPreviousRuntimeMacroHotkeysEnabled;
	private bool _dualRealityPreviousRuntimeMacroSwitchingEnabled;
	private bool _dualRealityRuntimeMacroHotkeysOverridden;
	private string _dualRealityLastWireframeDebugSummary = string.Empty;
	private string _dualRealityLastCollisionRadarDebugSummary = string.Empty;
	private double _dualRealityCollisionActivityRefreshSeconds;
	private float _appliedValidationCameraBackoffDistance;
	private string _appliedValidationCameraPosePreset = "validation_nearfield";
	private bool _validationCameraBackoffOverrideActive;

	public override void _Ready()
	{
		ApplyDualRealityCmdArgs();
		_traveler = GetNodeOrNull<Node3D>(TravelerPath);
		_mainCamera = GetNodeOrNull<Camera3D>(MainCameraPath);
		_portalA = GetNodeOrNull<WormholePortal>(PortalAPath);
		_portalB = GetNodeOrNull<WormholePortal>(PortalBPath);
		_rayBeamRenderer = GetNodeOrNull<RayBeamRenderer>(RayBeamRendererPath);
		_filmCamera = GetNodeOrNull<GrinFilmCamera>(FilmCameraPath);
		if (_filmCamera != null && GodotObject.IsInstanceValid(_filmCamera))
		{
			_filmCamera.EnableDomainTelemetry = EnableDomainTelemetry;
			_filmCamera.EnableDomainAwareFirstHitResolver = EnableDomainAwareFirstHitResolver;
		}
		_researchOverlay = GetNodeOrNull<WormholeResearchOverlay>("ResearchOverlayDebug");
		_researchOverlayStatusLabel = GetNodeOrNull<Label>("CanvasLayer/ResearchOverlayPanel/InsetMargin/InsetVBox/OverlayStatus");

		if (_traveler == null || _mainCamera == null || _portalA == null || _portalB == null)
		{
			GD.PushError($"{Name}: wormhole prototype rig is missing traveler, camera, or portal references.");
			SetProcess(false);
			SetPhysicsProcess(false);
			return;
		}

		SetProcess(true);
		SetPhysicsProcess(true);
		SetProcessUnhandledInput(true);

		_activePortal = StartInSceneA ? _portalA : _portalB;
		ApplyWorldVisibility(_activePortal);
		_lastActivePortalDelta = _activePortal.SignedRadiusDelta(_traveler.GlobalPosition);
		ApplyValidationInputLock();
		ApplyValidationCameraBackoff();
		InitializeDualRealityResearch();
		if (_dualRealityStartupHoldActive)
		{
			_dualRealityStartupHoldTicksMsec = Time.GetTicksMsec();
			_validationPendingCapture = false;
		}
		else
		{
			StartValidationHarnessRun();
		}
	}

	public override void _Process(double delta)
	{
		if (EnableAutoMotion)
		{
			UpdateAutoMotion(delta);
		}

		if (_validationPendingCapture && !_validationCaptureCompleted)
		{
			double elapsedSec = (Time.GetTicksMsec() - _validationStartTicksMsec) / 1000.0;
			double minDelaySec = Mathf.Max(0.1f, ValidationCaptureDelaySeconds);
			double maxDelaySec = Mathf.Max((float)minDelaySec, ValidationCaptureMaxDelaySeconds);
			bool shouldCapture = false;
			if (_filmCamera != null && GodotObject.IsInstanceValid(_filmCamera)
				&& _filmCamera.TryGetFilmCaptureDiagnosticsForTesting(out GrinFilmCamera.FilmCaptureDiagnosticsSnapshot filmSnapshot))
			{
				if (filmSnapshot.RowCursor > 0)
				{
					_validationSawFilmProgress = true;
				}

				if (elapsedSec >= minDelaySec && ValidationWaitForFilmWrap && _validationSawFilmProgress && filmSnapshot.RowCursor == 0)
				{
					_validationTriggerReason = $"film_wrap row_cursor={filmSnapshot.RowCursor} film_h={filmSnapshot.FilmHeight}";
					shouldCapture = true;
				}
				else if (elapsedSec >= maxDelaySec)
				{
					_validationTriggerReason = $"timeout row_cursor={filmSnapshot.RowCursor} film_h={filmSnapshot.FilmHeight}";
					shouldCapture = true;
				}
			}
			else if (elapsedSec >= maxDelaySec)
			{
				_validationTriggerReason = "timeout missing_film_diag";
				shouldCapture = true;
			}

			if (shouldCapture)
			{
				_validationCaptureElapsedSeconds = elapsedSec;
				_validationPendingCapture = false;
				ExecuteValidationCapture();
			}
		}

		_portalA?.UpdatePortalView(_mainCamera);
		_portalB?.UpdatePortalView(_mainCamera);
		UpdateDualRealityResearch(delta);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
		{
			return;
		}

		// Use a modified shortcut chord so deterministic harness runs are not perturbed by
		// ambient function-key events that the engine or desktop environment may emit.
		if (!keyEvent.AltPressed)
		{
			return;
		}

		switch (keyEvent.Keycode)
		{
			case Key.F6:
				DualRealityWireframeReferenceOverlayEnabled = !DualRealityWireframeReferenceOverlayEnabled;
				UpdateDualRealityHudState(forceOverlayRefresh: false);
				GetViewport()?.SetInputAsHandled();
				break;
			case Key.F7:
				EnableDualRealityResearchMode = !EnableDualRealityResearchMode;
				if (EnableDualRealityResearchMode)
				{
					RequestDualRealityBaselineRefresh("hotkey_toggle_on", force: true);
				}
				UpdateDualRealityHudState(forceOverlayRefresh: true);
				GetViewport()?.SetInputAsHandled();
				break;
			case Key.F8:
				DualRealityOverlayMode = DualRealityOverlayMode switch
				{
					DualRealityOverlayModeKind.None => DualRealityOverlayModeKind.FilmHeatmap,
					DualRealityOverlayModeKind.FilmHeatmap => DualRealityOverlayModeKind.CurvatureHeatmap,
					_ => DualRealityOverlayModeKind.None,
				};
				UpdateDualRealityHudState(forceOverlayRefresh: true);
				GetViewport()?.SetInputAsHandled();
				break;
			case Key.F9:
				DualRealityFreezeBaseline = !DualRealityFreezeBaseline;
				UpdateDualRealityHudState(forceOverlayRefresh: false);
				GetViewport()?.SetInputAsHandled();
				break;
			case Key.F10:
				RequestDualRealityBaselineRefresh("hotkey_manual_refresh", force: true);
				GetViewport()?.SetInputAsHandled();
				break;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_traveler == null || _activePortal == null)
		{
			return;
		}

		if (_teleportCooldownRemaining > 0.0)
		{
			_teleportCooldownRemaining = Mathf.Max(0.0f, (float)(_teleportCooldownRemaining - delta));
			_lastActivePortalDelta = _activePortal.SignedRadiusDelta(_traveler.GlobalPosition);
			return;
		}

		float currentDelta = _activePortal.SignedRadiusDelta(_traveler.GlobalPosition);
		bool crossedIntoShell = _lastActivePortalDelta > 0f && currentDelta <= 0f;

		if (crossedIntoShell)
		{
			TeleportThrough(_activePortal);
			_teleportCooldownRemaining = TeleportCooldownSeconds;
			_lastActivePortalDelta = _activePortal.SignedRadiusDelta(_traveler.GlobalPosition);
			return;
		}

		_lastActivePortalDelta = currentDelta;
	}

	private void TeleportThrough(WormholePortal sourcePortal)
	{
		// Demo-only traversal seam. This direct portal remap is not part of the renderer causal
		// validation ledger and should be treated as artistic/non-causal unless explicitly ledgered.
		WormholePortal destinationPortal = sourcePortal.GetLinkedPortal();
		if (destinationPortal == null)
		{
			return;
		}

		_traveler.GlobalTransform = sourcePortal.BuildConfiguredExitTransform(_traveler.GlobalTransform);
		_activePortal = destinationPortal;
		_teleportCount++;
		GD.Print($"[WormholeValidation] teleport_count={_teleportCount} active_portal={_activePortal.Name}");
		ApplyWorldVisibility(destinationPortal);
	}

	private void ApplyWorldVisibility(WormholePortal activePortal)
	{
		if (_mainCamera == null || activePortal == null)
		{
			return;
		}

		_mainCamera.CullMask = activePortal.GeometryMask | activePortal.PortalMask;
	}

	private void UpdateAutoMotion(double delta)
	{
		if (_traveler == null || _activePortal == null)
		{
			return;
		}

		_autoMotionElapsedSeconds += delta;
		float orbitRadius = Mathf.Max(0.5f, AutoMotionOrbitRadius);
		float radialWaveAmp = Mathf.Max(0f, AutoMotionRadialWaveAmplitude);
		if (radialWaveAmp > 0f)
		{
			float radialWavePeriod = Mathf.Max(0.1f, AutoMotionRadialWavePeriodSeconds);
			orbitRadius += radialWaveAmp * Mathf.Sin(Mathf.Tau * (float)(_autoMotionElapsedSeconds / radialWavePeriod));
			orbitRadius = Mathf.Max(0.25f, orbitRadius);
		}
		float orbitPeriod = Mathf.Max(0.1f, AutoMotionPeriodSeconds);
		float angle = Mathf.DegToRad(AutoMotionStartAngleDegrees)
			+ (Mathf.Tau * (float)(_autoMotionElapsedSeconds / orbitPeriod));
		Vector3 center = _activePortal.GlobalPosition;
		Vector3 orbitPos = center
			+ new Vector3(Mathf.Cos(angle) * orbitRadius, AutoMotionHeight, Mathf.Sin(angle) * orbitRadius);

		_traveler.GlobalPosition = orbitPos;
		if (AutoMotionLookAtActivePortal)
		{
			_traveler.LookAt(center, Vector3.Up);
		}
	}

	private void ApplyValidationInputLock()
	{
		if (!LockTravelerInputDuringValidation)
		{
			return;
		}

		if (_traveler is FreeFlyCamera freeFlyTraveler)
		{
			freeFlyTraveler.SetInputEnabled(false, releaseMouse: true);
		}
		else if (_mainCamera is FreeFlyCamera freeFlyCamera)
		{
			freeFlyCamera.SetInputEnabled(false, releaseMouse: true);
		}
		else if (Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		GD.Print("[WormholeValidation] input_lock traveler_input=disabled mouse_mode=visible");
	}

	private void StartValidationHarnessRun()
	{
		_rayBeamRenderer?.BeginBoundaryValidationRun();
		_validationStartTicksMsec = Time.GetTicksMsec();
		_validationPendingCapture = true;
	}

	private void ApplyValidationCameraBackoff()
	{
		_appliedValidationCameraPosePreset = NormalizeValidationCameraPosePreset(ValidationCameraPosePreset);
		float presetBackoff = ResolveValidationCameraPresetBackoff(_appliedValidationCameraPosePreset);
		_appliedValidationCameraBackoffDistance = _validationCameraBackoffOverrideActive
			? Mathf.Max(0f, ValidationCameraBackoffDistance)
			: presetBackoff;
		if (_mainCamera == null)
		{
			return;
		}

		if (_appliedValidationCameraBackoffDistance > 0f && _traveler != null)
		{
			Vector3 backward = _mainCamera.GlobalTransform.Basis.Z.Normalized();
			Vector3 delta = backward * _appliedValidationCameraBackoffDistance;
			_traveler.GlobalPosition += delta;
			if (!ReferenceEquals(_traveler, _mainCamera))
			{
				_mainCamera.GlobalPosition += delta;
			}
		}

		Vector3 position = _mainCamera.GlobalPosition;
		Vector3 forward = -_mainCamera.GlobalTransform.Basis.Z.Normalized();
		GD.Print(
			$"[WormholeValidation] camera_pose preset={_appliedValidationCameraPosePreset} " +
			$"override_active={(_validationCameraBackoffOverrideActive ? "true" : "false")} " +
			$"amount={_appliedValidationCameraBackoffDistance:0.###} " +
			$"position=({position.X:0.###},{position.Y:0.###},{position.Z:0.###}) " +
			$"forward=({forward.X:0.###},{forward.Y:0.###},{forward.Z:0.###})");
	}

	private string NormalizeValidationCameraPosePreset(string preset)
	{
		if (string.IsNullOrWhiteSpace(preset))
		{
			return "validation_nearfield";
		}

		return preset.Trim().ToLowerInvariant() switch
		{
			"validation" or "validation_near" or "validation_nearfield" or "near" or "nearfield" => "validation_nearfield",
			"presentation_mid" or "presentation-mid" or "mid" or "mid_backoff" => "presentation_mid",
			"presentation_far" or "presentation-far" or "far" or "far_backoff" => "presentation_far",
			_ => "validation_nearfield",
		};
	}

	private float ResolveValidationCameraPresetBackoff(string preset)
	{
		return preset switch
		{
			"presentation_mid" => Mathf.Max(0f, PresentationMidBackoffDistance),
			"presentation_far" => Mathf.Max(0f, PresentationFarBackoffDistance),
			_ => Mathf.Max(0f, ValidationNearfieldBackoffDistance),
		};
	}

	private void InitializeDualRealityResearch()
	{
		_hudCanvasLayer = GetNodeOrNull<CanvasLayer>("CanvasLayer");
		_researchOverlayPanel = GetNodeOrNull<Control>("CanvasLayer/ResearchOverlayPanel");
		_researchOverlayView = GetNodeOrNull<TextureRect>("CanvasLayer/ResearchOverlayPanel/InsetMargin/InsetVBox/ResearchOverlayView");
		if (_filmCamera == null || _rayBeamRenderer == null)
		{
			return;
		}

		_straightRayReferenceCache = new StraightRayReferenceCache
		{
			Name = "StraightRayReferenceCache"
		};
		AddChild(_straightRayReferenceCache);
		_straightRayReferenceCache.Configure(_filmCamera, _rayBeamRenderer);
		EnsureDualRealityInsetUi();
		ApplyDualRealityRuntimeHotkeyGuard();
		UpdateDualRealityHudState(forceOverlayRefresh: true);
		if (EnableDualRealityResearchMode)
		{
			_dualRealityStartupHoldActive = true;
			_dualRealityStartupHoldMainUpdateEveryFrame = _filmCamera.UpdateEveryFrame;
			_filmCamera.UpdateEveryFrame = false;
			DualRealityRefreshBaseline = false;
			RequestDualRealityBaselineRefresh("startup", force: true);
		}
	}

	private void EnsureDualRealityInsetUi()
	{
		if (_hudCanvasLayer == null)
		{
			return;
		}

		if (_dualRealityInsetPanel == null)
		{
			_dualRealityInsetPanel = new PanelContainer
			{
				Name = "DualRealityInsetPanel",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Visible = false,
				ZIndex = 15,
			};
			_dualRealityInsetPanel.AnchorLeft = 1f;
			_dualRealityInsetPanel.AnchorTop = 0f;
			_dualRealityInsetPanel.AnchorRight = 1f;
			_dualRealityInsetPanel.AnchorBottom = 0f;
			_hudCanvasLayer.AddChild(_dualRealityInsetPanel);

			MarginContainer margin = new()
			{
				Name = "InsetMargin",
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			margin.AddThemeConstantOverride("margin_left", 8);
			margin.AddThemeConstantOverride("margin_top", 8);
			margin.AddThemeConstantOverride("margin_right", 8);
			margin.AddThemeConstantOverride("margin_bottom", 8);
			_dualRealityInsetPanel.AddChild(margin);

			VBoxContainer vbox = new()
			{
				Name = "InsetVBox",
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			vbox.AddThemeConstantOverride("separation", 4);
			margin.AddChild(vbox);

			_dualRealityTitleLabel = new Label
			{
				Name = "DualRealityTitleLabel",
				Text = "STRAIGHT TRANSPORT REFERENCE",
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			_dualRealityTitleLabel.AddThemeFontSizeOverride("font_size", 11);
			vbox.AddChild(_dualRealityTitleLabel);

			// The straight transport reference remains a literal cached render. Diagnostic and
			// wireframe layers are optional overlays on top of that image rather than substitutes.
			Control stack = new()
			{
				Name = "DualRealityTextureStack",
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			vbox.AddChild(stack);

			_dualRealityBaselineView = new TextureRect
			{
				Name = "DualRealityBaselineView",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			};
			_dualRealityBaselineView.AnchorLeft = 0f;
			_dualRealityBaselineView.AnchorTop = 0f;
			_dualRealityBaselineView.AnchorRight = 1f;
			_dualRealityBaselineView.AnchorBottom = 1f;
			stack.AddChild(_dualRealityBaselineView);

			_dualRealityOverlayView = new TextureRect
			{
				Name = "DualRealityOverlayView",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				Visible = false,
			};
			_dualRealityOverlayView.AnchorLeft = 0f;
			_dualRealityOverlayView.AnchorTop = 0f;
			_dualRealityOverlayView.AnchorRight = 1f;
			_dualRealityOverlayView.AnchorBottom = 1f;
			stack.AddChild(_dualRealityOverlayView);

			_dualRealityWireframePanelOverlay = new WireframeReferenceOverlay
			{
				Name = "WireframeReferenceOverlayPanel",
				Visible = false,
				ZIndex = 2,
			};
			stack.AddChild(_dualRealityWireframePanelOverlay);

			_dualRealityCollisionPanelOverlay = new CameraSpaceCollisionOverlay
			{
				Name = "CameraSpaceCollisionOverlayPanel",
				Visible = false,
				ZIndex = 3,
			};
			stack.AddChild(_dualRealityCollisionPanelOverlay);

			_dualRealityModeLabel = new Label
			{
				Name = "DualRealityModeLabel",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Modulate = new Color(0.86f, 0.92f, 0.98f, 0.9f),
			};
			_dualRealityModeLabel.AddThemeFontSizeOverride("font_size", 9);
			vbox.AddChild(_dualRealityModeLabel);

			_dualRealityOverlayPanelLegendLabel = new Label
			{
				Name = "DualRealityOverlayPanelLegendLabel",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Visible = false,
				Modulate = new Color(0.9f, 0.94f, 1f, 0.88f),
			};
			_dualRealityOverlayPanelLegendLabel.AddThemeFontSizeOverride("font_size", 9);
			vbox.AddChild(_dualRealityOverlayPanelLegendLabel);

			_dualRealityStateLabel = new Label
			{
				Name = "DualRealityStateLabel",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Modulate = new Color(0.82f, 0.9f, 0.84f, 0.9f),
			};
			_dualRealityStateLabel.AddThemeFontSizeOverride("font_size", 9);
			vbox.AddChild(_dualRealityStateLabel);
		}

		if (_dualRealityWireframeFullscreenOverlay == null)
		{
			_dualRealityWireframeFullscreenOverlay = new WireframeReferenceOverlay
			{
				Name = "WireframeReferenceOverlayFullscreen",
				Visible = false,
				ZIndex = 6,
			};
			_hudCanvasLayer.AddChild(_dualRealityWireframeFullscreenOverlay);
		}

		if (_dualRealityOverlayFullscreenView == null)
		{
			_dualRealityOverlayFullscreenView = new TextureRect
			{
				Name = "DualRealityOverlayFullscreenView",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				Visible = false,
				ZIndex = 4,
			};
			_dualRealityOverlayFullscreenView.AnchorLeft = 0f;
			_dualRealityOverlayFullscreenView.AnchorTop = 0f;
			_dualRealityOverlayFullscreenView.AnchorRight = 1f;
			_dualRealityOverlayFullscreenView.AnchorBottom = 1f;
			_hudCanvasLayer.AddChild(_dualRealityOverlayFullscreenView);
		}

		if (_dualRealityCollisionFullscreenOverlay == null)
		{
			_dualRealityCollisionFullscreenOverlay = new CameraSpaceCollisionOverlay
			{
				Name = "CameraSpaceCollisionOverlayFullscreen",
				Visible = false,
				ZIndex = 5,
			};
			_hudCanvasLayer.AddChild(_dualRealityCollisionFullscreenOverlay);
		}

		if (_dualRealityOverlayFullscreenLegendPanel == null)
		{
			_dualRealityOverlayFullscreenLegendPanel = new PanelContainer
			{
				Name = "DualRealityOverlayFullscreenLegendPanel",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Visible = false,
				ZIndex = 8,
			};
			_dualRealityOverlayFullscreenLegendPanel.AnchorLeft = 1f;
			_dualRealityOverlayFullscreenLegendPanel.AnchorTop = 1f;
			_dualRealityOverlayFullscreenLegendPanel.AnchorRight = 1f;
			_dualRealityOverlayFullscreenLegendPanel.AnchorBottom = 1f;
			_hudCanvasLayer.AddChild(_dualRealityOverlayFullscreenLegendPanel);

			MarginContainer legendMargin = new()
			{
				Name = "OverlayLegendMargin",
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			legendMargin.AddThemeConstantOverride("margin_left", 8);
			legendMargin.AddThemeConstantOverride("margin_top", 6);
			legendMargin.AddThemeConstantOverride("margin_right", 8);
			legendMargin.AddThemeConstantOverride("margin_bottom", 6);
			_dualRealityOverlayFullscreenLegendPanel.AddChild(legendMargin);

			_dualRealityOverlayFullscreenLegendLabel = new Label
			{
				Name = "DualRealityOverlayFullscreenLegendLabel",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				Modulate = new Color(0.95f, 0.97f, 1f, 0.94f),
			};
			_dualRealityOverlayFullscreenLegendLabel.AddThemeFontSizeOverride("font_size", 10);
			legendMargin.AddChild(_dualRealityOverlayFullscreenLegendLabel);
		}

		Node3D probeWall = GetNodeOrNull<Node3D>("SceneB/ProbeWallB");
		Node3D backdropA = GetNodeOrNull<Node3D>("SceneA/BackdropA");
		Node3D backdropB = GetNodeOrNull<Node3D>("SceneB/BackdropB");
		Node3D sceneA = GetNodeOrNull<Node3D>("SceneA");
		Node3D sceneB = GetNodeOrNull<Node3D>("SceneB");
		_dualRealityWireframeFullscreenOverlay?.Configure(_mainCamera, _portalA, _portalB, probeWall, backdropA, backdropB);
		_dualRealityWireframePanelOverlay?.Configure(_mainCamera, _portalA, _portalB, probeWall, backdropA, backdropB);
		_dualRealityCollisionFullscreenOverlay?.Configure(_mainCamera, sceneA, sceneB, _portalA, _portalB);
		_dualRealityCollisionPanelOverlay?.Configure(_mainCamera, sceneA, sceneB, _portalA, _portalB);

		if (_dualRealityWireframePanelOverlay != null)
		{
			_dualRealityWireframePanelOverlay.ModeLabel = "SEMANTIC REFERENCE GLYPHS · BASELINE";
		}
		if (_dualRealityWireframeFullscreenOverlay != null)
		{
			_dualRealityWireframeFullscreenOverlay.ModeLabel = "SEMANTIC REFERENCE GLYPHS · CURVED";
		}
		if (_dualRealityCollisionPanelOverlay != null)
		{
			_dualRealityCollisionPanelOverlay.ModeLabel = "COLLISION RADAR · BASELINE";
		}
		if (_dualRealityCollisionFullscreenOverlay != null)
		{
			_dualRealityCollisionFullscreenOverlay.ModeLabel = "COLLISION RADAR · CURVED";
		}
	}

	private void UpdateDualRealityInsetLayout()
	{
		if (_dualRealityInsetPanel == null)
		{
			return;
		}

		Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1280f, 720f);
		float targetWidth = Mathf.Clamp(viewportSize.X * Mathf.Clamp(DualRealityInsetScale, 0.15f, 0.5f), 180f, 320f);
		float textureHeight = targetWidth / (16f / 9f);
		float panelHeight = textureHeight + 62f;
		float panelTop = 16f;
		if (_researchOverlayPanel != null && _researchOverlayPanel.Visible)
		{
			panelTop = _researchOverlayPanel.GetRect().End.Y + 12f;
		}

		_dualRealityInsetPanel.OffsetLeft = -targetWidth - 16f;
		_dualRealityInsetPanel.OffsetTop = panelTop;
		_dualRealityInsetPanel.OffsetRight = -16f;
		_dualRealityInsetPanel.OffsetBottom = panelTop + panelHeight;

		Control textureStack = _dualRealityBaselineView?.GetParent() as Control;
		if (textureStack != null)
		{
			textureStack.CustomMinimumSize = new Vector2(targetWidth - 16f, textureHeight);
		}

		if (_dualRealityOverlayFullscreenLegendPanel != null)
		{
			_dualRealityOverlayFullscreenLegendPanel.OffsetLeft = -236f;
			_dualRealityOverlayFullscreenLegendPanel.OffsetTop = -92f;
			_dualRealityOverlayFullscreenLegendPanel.OffsetRight = -16f;
			_dualRealityOverlayFullscreenLegendPanel.OffsetBottom = -16f;
		}
	}

	private void UpdateDualRealityInsetVisibility()
	{
		if (_dualRealityInsetPanel == null && _dualRealityWireframeFullscreenOverlay == null && _dualRealityWireframePanelOverlay == null)
		{
			return;
		}

		bool panelVisible = EnableDualRealityResearchMode && DualRealityInsetEnabled;
		if (_dualRealityInsetPanel != null)
		{
			_dualRealityInsetPanel.Visible = panelVisible;
		}

		bool wireframeVisible = EnableDualRealityResearchMode && DualRealityWireframeReferenceOverlayEnabled;
		bool wireframeFullscreen = wireframeVisible && (DualRealityWireframePlacement == DualRealityWireframePlacementMode.FullscreenCurved || DualRealityWireframePlacement == DualRealityWireframePlacementMode.Both);
		bool wireframePanel = wireframeVisible && panelVisible && (DualRealityWireframePlacement == DualRealityWireframePlacementMode.StraightTransportReference || DualRealityWireframePlacement == DualRealityWireframePlacementMode.Both);
		ApplyWireframeOverlayState(_dualRealityWireframeFullscreenOverlay, wireframeFullscreen, "SEMANTIC REFERENCE GLYPHS · CURVED");
		ApplyWireframeOverlayState(_dualRealityWireframePanelOverlay, wireframePanel, "SEMANTIC REFERENCE GLYPHS · BASELINE");

		bool collisionVisible = EnableDualRealityResearchMode && DualRealityCollisionRadarOverlayEnabled;
		bool collisionFullscreen = collisionVisible && (DualRealityCollisionRadarPlacement == DualRealityWireframePlacementMode.FullscreenCurved || DualRealityCollisionRadarPlacement == DualRealityWireframePlacementMode.Both);
		bool collisionPanel = collisionVisible && panelVisible && (DualRealityCollisionRadarPlacement == DualRealityWireframePlacementMode.StraightTransportReference || DualRealityCollisionRadarPlacement == DualRealityWireframePlacementMode.Both);
		ApplyCollisionOverlayState(_dualRealityCollisionFullscreenOverlay, collisionFullscreen, "COLLISION RADAR · CURVED");
		ApplyCollisionOverlayState(_dualRealityCollisionPanelOverlay, collisionPanel, "COLLISION RADAR · BASELINE");

		bool diagnosticVisible = IsDualRealityDiagnosticOverlayEnabled();
		DualRealityWireframePlacementMode diagnosticPlacement = ResolveDiagnosticOverlayPlacementMode();
		bool diagnosticFullscreen = diagnosticVisible && (diagnosticPlacement == DualRealityWireframePlacementMode.FullscreenCurved || diagnosticPlacement == DualRealityWireframePlacementMode.Both);
		bool diagnosticPanel = diagnosticVisible && panelVisible && (diagnosticPlacement == DualRealityWireframePlacementMode.StraightTransportReference || diagnosticPlacement == DualRealityWireframePlacementMode.Both);
		ApplyDiagnosticOverlayState(_dualRealityOverlayFullscreenView, diagnosticFullscreen);
		ApplyDiagnosticOverlayState(_dualRealityOverlayView, diagnosticPanel);
		bool showLegend = diagnosticVisible && ResolveDualRealityDiagnosticOverlayShowLegend();
		if (_dualRealityOverlayPanelLegendLabel != null)
		{
			_dualRealityOverlayPanelLegendLabel.Visible = diagnosticPanel && showLegend;
		}
		if (_dualRealityOverlayFullscreenLegendPanel != null)
		{
			_dualRealityOverlayFullscreenLegendPanel.Visible = diagnosticFullscreen && showLegend;
		}
	}

	private bool IsDualRealityDiagnosticOverlayEnabled()
	{
		if (!EnableDualRealityResearchMode)
		{
			return false;
		}

		return DualRealityOverlayMode switch
		{
			DualRealityOverlayModeKind.FilmHeatmap => true,
			DualRealityOverlayModeKind.CurvatureHeatmap => EnableCurvatureHeatmap,
			_ => false,
		};
	}

	private DualRealityWireframePlacementMode ResolveDiagnosticOverlayPlacementMode()
	{
		return DualRealityOverlayMode == DualRealityOverlayModeKind.CurvatureHeatmap
			? CurvatureHeatmapPlacement
			: DualRealityWireframePlacementMode.StraightTransportReference;
	}

	private float ResolveDiagnosticOverlayOpacity()
	{
		return DualRealityOverlayMode == DualRealityOverlayModeKind.CurvatureHeatmap
			? Mathf.Clamp(CurvatureHeatmapOpacity, 0f, 1f)
			: Mathf.Clamp(DualRealityOverlayOpacity, 0f, 1f);
	}

	private bool ResolveDualRealityDiagnosticOverlayShowLegend()
	{
		return DualRealityOverlayMode == DualRealityOverlayModeKind.CurvatureHeatmap && CurvatureHeatmapShowLegend;
	}

	private void ApplyDiagnosticOverlayState(TextureRect overlay, bool visible)
	{
		if (overlay == null)
		{
			return;
		}

		overlay.Visible = visible;
		overlay.Modulate = new Color(1f, 1f, 1f, ResolveDiagnosticOverlayOpacity());
	}

	private void ApplyWireframeOverlayState(WireframeReferenceOverlay overlay, bool visible, string label)
	{
		if (overlay == null)
		{
			return;
		}

		overlay.Visible = visible;
		overlay.OverlayEnabled = visible;
		overlay.OverlayOpacity = Mathf.Clamp(DualRealityWireframeOverlayOpacity, 0f, 1f);
		overlay.ModeLabel = label;
		overlay.ShowFieldGlyphs = DualRealityWireframeShowFieldGlyphs;
		overlay.ShowBoundaryLayerGlyphs = DualRealityWireframeShowBoundaryLayerGlyphs;
		overlay.ShowWormholePortalGlyphs = DualRealityWireframeShowWormholePortalGlyphs;
		overlay.ShowBackdropAndProbeHelpers = DualRealityWireframeShowBackdropAndProbeHelpers;
		overlay.ShowCenterAnchor = DualRealityWireframeShowCenterAnchor;
		overlay.AllowEdgeOnPlanesForDebug = DualRealityWireframeAllowEdgeOnPlanesForDebug;
		overlay.EdgeOnPlaneDotThreshold = Mathf.Clamp(DualRealityWireframeEdgeOnPlaneDotThreshold, 0f, 0.5f);
	}

	private void ApplyCollisionOverlayState(CameraSpaceCollisionOverlay overlay, bool visible, string label)
	{
		if (overlay == null)
		{
			return;
		}

		bool primarySceneIsA = _activePortal == _portalA;
		overlay.SetPrimarySceneIsA(primarySceneIsA);
		overlay.Visible = visible;
		overlay.OverlayEnabled = visible;
		overlay.OverlayOpacity = Mathf.Clamp(DualRealityCollisionRadarOpacity, 0f, 1f);
		overlay.ModeLabel = label;
		overlay.ShowPrimarySceneGeometry = DualRealityCollisionRadarShowPrimarySceneGeometry;
		overlay.ShowRemappedSceneGeometry = DualRealityCollisionRadarShowRemappedSceneGeometry;
		overlay.ShowBackgroundObjects = DualRealityCollisionRadarShowBackgroundObjects;
		overlay.ShowProbeHelpers = DualRealityCollisionRadarShowProbeHelpers;
		overlay.DisplayFilterMode = (CameraSpaceCollisionOverlay.CollisionRadarDisplayFilterMode)DualRealityCollisionRadarFilter;
		overlay.BoundsMode = (CameraSpaceCollisionOverlay.CollisionRadarBoundsMode)DualRealityCollisionRadarBounds;
		overlay.ShowCenterMarkers = DualRealityCollisionRadarShowCenterMarkers;
		overlay.ShowLabels = DualRealityCollisionRadarShowLabels;
		overlay.ShowLeaderLines = DualRealityCollisionRadarShowLeaderLines;
		overlay.ShowLegend = DualRealityCollisionRadarShowLegend;
	}

	private static void AppendQuantizedTransform(ref HashCode hash, Transform3D transform)
	{
		static float Quantize(float value) => Mathf.Snapped(value, 0.001f);
		hash.Add(Quantize(transform.Origin.X));
		hash.Add(Quantize(transform.Origin.Y));
		hash.Add(Quantize(transform.Origin.Z));
		hash.Add(Quantize(transform.Basis.X.X));
		hash.Add(Quantize(transform.Basis.X.Y));
		hash.Add(Quantize(transform.Basis.X.Z));
		hash.Add(Quantize(transform.Basis.Y.X));
		hash.Add(Quantize(transform.Basis.Y.Y));
		hash.Add(Quantize(transform.Basis.Y.Z));
		hash.Add(Quantize(transform.Basis.Z.X));
		hash.Add(Quantize(transform.Basis.Z.Y));
		hash.Add(Quantize(transform.Basis.Z.Z));
	}

	private ulong ComputeDualRealityStateHash()
	{
		HashCode hash = new();
		if (_mainCamera != null)
		{
			AppendQuantizedTransform(ref hash, _mainCamera.GlobalTransform);
		}
		if (_portalA != null)
		{
			AppendQuantizedTransform(ref hash, _portalA.GlobalTransform);
		}
		if (_portalB != null)
		{
			AppendQuantizedTransform(ref hash, _portalB.GlobalTransform);
		}
		hash.Add(_teleportCount);
		hash.Add(_activePortal == _portalA ? 0 : 1);
		hash.Add(_validationCaptureCompleted);
		return unchecked((ulong)hash.ToHashCode());
	}

	private float ResolveDualRealityReferenceResolutionScale()
	{
		if (_filmCamera == null)
		{
			return 0.3f;
		}

		float scaled = _filmCamera.FilmResolutionScale * 0.5f;
		return Mathf.Clamp(scaled, 0.18f, _filmCamera.FilmResolutionScale);
	}

	private void RequestDualRealityBaselineRefresh(string reason, bool force)
	{
		if (!EnableDualRealityResearchMode || _straightRayReferenceCache == null)
		{
			return;
		}

		ulong stateHash = ComputeDualRealityStateHash();
		if (!force && DualRealityFreezeBaseline && _straightRayReferenceCache.TryGetBaselineSnapshot(out _))
		{
			return;
		}

		_straightRayReferenceCache.RequestRefresh(stateHash, ResolveDualRealityReferenceResolutionScale());
		GD.Print($"[DualReality] baseline_refresh_requested reason={reason} state_hash={stateHash} freeze={(DualRealityFreezeBaseline ? 1 : 0)}");
	}

	private GrinFilmCamera.TelemetryHeatmapKind ResolveDualRealityHeatmapKind()
	{
		if (DualRealityOverlayMode == DualRealityOverlayModeKind.CurvatureHeatmap)
		{
			return CurvatureHeatmapMetric switch
			{
				CurvatureHeatmapMetricMode.MaxLocalTurnAngle => GrinFilmCamera.TelemetryHeatmapKind.TurnMax,
				CurvatureHeatmapMetricMode.CurvatureMean => GrinFilmCamera.TelemetryHeatmapKind.CurvatureMean,
				CurvatureHeatmapMetricMode.CurvatureMax => GrinFilmCamera.TelemetryHeatmapKind.CurvatureMax,
				CurvatureHeatmapMetricMode.Pass1StepDensityPlaceholder => GrinFilmCamera.TelemetryHeatmapKind.Pass1Steps,
				_ => GrinFilmCamera.TelemetryHeatmapKind.TurnSum,
			};
		}

		return GrinFilmCamera.TelemetryHeatmapKind.Work;
	}

	private string ResolveCurvatureHeatmapNormalizationToken()
	{
		return CurvatureHeatmapNormalization switch
		{
			CurvatureHeatmapNormalizationMode.AutoFullRange => "full",
			CurvatureHeatmapNormalizationMode.FixedRange => "fixed",
			_ => "basic",
		};
	}

	private string ResolveCurvatureHeatmapMetricLabel()
	{
		return CurvatureHeatmapMetric switch
		{
			CurvatureHeatmapMetricMode.MaxLocalTurnAngle => "MAX LOCAL TURN",
			CurvatureHeatmapMetricMode.CurvatureMean => "MEAN CURVATURE",
			CurvatureHeatmapMetricMode.CurvatureMax => "MAX CURVATURE",
			CurvatureHeatmapMetricMode.Pass1StepDensityPlaceholder => "STEP DENSITY",
			_ => "CUMULATIVE TURN",
		};
	}

	private string ResolveCurvatureHeatmapNormalizationLabel()
	{
		return CurvatureHeatmapNormalization switch
		{
			CurvatureHeatmapNormalizationMode.AutoFullRange => "FULL",
			CurvatureHeatmapNormalizationMode.FixedRange => "FIXED",
			_ => "AUTO",
		};
	}

	private string ResolveDualRealityOverlayModeLabel()
	{
		return DualRealityOverlayMode switch
		{
			DualRealityOverlayModeKind.FilmHeatmap => "DIAGNOSTIC OVERLAY · FILM HEATMAP",
			DualRealityOverlayModeKind.CurvatureHeatmap => "DIAGNOSTIC OVERLAY · DISTORTION HEAT MAP",
			_ => "DIAGNOSTIC OVERLAY · NONE",
		};
	}

	private string ResolveDiagnosticOverlayPlacementLabel()
	{
		if (DualRealityOverlayMode != DualRealityOverlayModeKind.CurvatureHeatmap)
		{
			return "SCALAR OVERLAY · BASELINE PANEL";
		}

		return CurvatureHeatmapPlacement switch
		{
			DualRealityWireframePlacementMode.StraightTransportReference => "SCALAR OVERLAY · BASELINE PANEL",
			DualRealityWireframePlacementMode.Both => "SCALAR OVERLAY · CURVED + BASELINE",
			_ => "SCALAR OVERLAY · CURVED FULLSCREEN",
		};
	}

	private string ResolveWireframePlacementLabel()
	{
		return DualRealityWireframePlacement switch
		{
			DualRealityWireframePlacementMode.StraightTransportReference => "SEMANTIC GLYPHS · BASELINE PANEL",
			DualRealityWireframePlacementMode.Both => "SEMANTIC GLYPHS · CURVED + BASELINE",
			_ => "SEMANTIC GLYPHS · CURVED FULLSCREEN",
		};
	}

	private string ResolveCollisionPlacementLabel()
	{
		return DualRealityCollisionRadarPlacement switch
		{
			DualRealityWireframePlacementMode.StraightTransportReference => "COLLISION RADAR · BASELINE PANEL",
			DualRealityWireframePlacementMode.Both => "COLLISION RADAR · CURVED + BASELINE",
			_ => "COLLISION RADAR · CURVED FULLSCREEN",
		};
	}

	private void RefreshCollisionRadarHitActivity(bool force)
	{
		if (!EnableDualRealityResearchMode || _filmCamera == null)
		{
			return;
		}

		bool anyCollisionOverlayVisible =
			(_dualRealityCollisionFullscreenOverlay?.Visible == true) ||
			(_dualRealityCollisionPanelOverlay?.Visible == true);
		if (!anyCollisionOverlayVisible)
		{
			return;
		}

		if (!force && _dualRealityCollisionActivityRefreshSeconds < 0.35)
		{
			return;
		}

		_dualRealityCollisionActivityRefreshSeconds = 0.0;
		if (_filmCamera.TryGetColliderHitActivityForTesting(out GrinFilmCamera.ColliderHitActivityEntry[] entries))
		{
			_dualRealityCollisionFullscreenOverlay?.SetHitActivity(entries);
			_dualRealityCollisionPanelOverlay?.SetHitActivity(entries);
		}
		else
		{
			_dualRealityCollisionFullscreenOverlay?.SetHitActivity(Array.Empty<GrinFilmCamera.ColliderHitActivityEntry>());
			_dualRealityCollisionPanelOverlay?.SetHitActivity(Array.Empty<GrinFilmCamera.ColliderHitActivityEntry>());
		}
	}

	private void UpdateDualRealityTelemetryConfig()
	{
		if (_filmCamera == null)
		{
			return;
		}

		bool needTelemetry = IsDualRealityDiagnosticOverlayEnabled();
		_filmCamera.ExportTelemetryHeatmaps = needTelemetry;
		if (needTelemetry)
		{
			_filmCamera.TelemetryHeatmapMode = DualRealityOverlayMode == DualRealityOverlayModeKind.CurvatureHeatmap
				? ResolveCurvatureHeatmapNormalizationToken()
				: "basic";
		}
	}

	private void UpdateDualRealityOverlayTexture(bool force)
	{
		if ((_dualRealityOverlayView == null && _dualRealityOverlayFullscreenView == null) || _filmCamera == null)
		{
			return;
		}

		if (!IsDualRealityDiagnosticOverlayEnabled())
		{
			if (_dualRealityOverlayView != null)
			{
				_dualRealityOverlayView.Visible = false;
			}
			if (_dualRealityOverlayFullscreenView != null)
			{
				_dualRealityOverlayFullscreenView.Visible = false;
			}
			if (_dualRealityOverlayPanelLegendLabel != null)
			{
				_dualRealityOverlayPanelLegendLabel.Visible = false;
			}
			if (_dualRealityOverlayFullscreenLegendPanel != null)
			{
				_dualRealityOverlayFullscreenLegendPanel.Visible = false;
			}
			_dualRealityLastOverlayLegendText = string.Empty;
			return;
		}

		if (!force && _dualRealityOverlayRefreshSeconds < 0.5)
		{
			return;
		}

		_dualRealityOverlayRefreshSeconds = 0.0;
		string normalizationToken = DualRealityOverlayMode == DualRealityOverlayModeKind.CurvatureHeatmap
			? ResolveCurvatureHeatmapNormalizationToken()
			: "basic";
		float fixedMin = DualRealityOverlayMode == DualRealityOverlayModeKind.CurvatureHeatmap
			? CurvatureHeatmapMin
			: float.NaN;
		float fixedMax = DualRealityOverlayMode == DualRealityOverlayModeKind.CurvatureHeatmap
			? CurvatureHeatmapMax
			: float.NaN;
		if (!_filmCamera.TryCopyTelemetryHeatmapImageForTesting(ResolveDualRealityHeatmapKind(), out Image image, out GrinFilmCamera.TelemetryHeatmapStats stats, normalizationToken, fixedMin, fixedMax)
			|| image == null)
		{
			if (_dualRealityOverlayView != null)
			{
				_dualRealityOverlayView.Visible = false;
			}
			if (_dualRealityOverlayFullscreenView != null)
			{
				_dualRealityOverlayFullscreenView.Visible = false;
			}
			return;
		}

		if (_dualRealityOverlayTexture == null || _dualRealityOverlayTexture.GetSize() != image.GetSize())
		{
			_dualRealityOverlayTexture = ImageTexture.CreateFromImage(image);
		}
		else
		{
			_dualRealityOverlayTexture.Update(image);
		}

		if (_dualRealityOverlayView != null)
		{
			_dualRealityOverlayView.Texture = _dualRealityOverlayTexture;
			_dualRealityOverlayView.Modulate = new Color(1f, 1f, 1f, ResolveDiagnosticOverlayOpacity());
		}
		if (_dualRealityOverlayFullscreenView != null)
		{
			_dualRealityOverlayFullscreenView.Texture = _dualRealityOverlayTexture;
			_dualRealityOverlayFullscreenView.Modulate = new Color(1f, 1f, 1f, ResolveDiagnosticOverlayOpacity());
		}

		string legendText = BuildDualRealityOverlayLegendText(stats);
		if (_dualRealityOverlayPanelLegendLabel != null)
		{
			_dualRealityOverlayPanelLegendLabel.Text = legendText;
		}
		if (_dualRealityOverlayFullscreenLegendLabel != null)
		{
			_dualRealityOverlayFullscreenLegendLabel.Text = legendText;
		}
		if (!string.Equals(legendText, _dualRealityLastOverlayLegendText, StringComparison.Ordinal))
		{
			_dualRealityLastOverlayLegendText = legendText;
			GD.Print($"[DualReality] diagnostic_overlay mode={ResolveDualRealityOverlayModeLabel()} placement={ResolveDiagnosticOverlayPlacementLabel()} legend=\"{legendText}\"");
		}
	}

	private string BuildDualRealityOverlayLegendText(GrinFilmCamera.TelemetryHeatmapStats stats)
	{
		if (DualRealityOverlayMode != DualRealityOverlayModeKind.CurvatureHeatmap)
		{
			return "FILM HEATMAP";
		}

		string rangeLabel = CurvatureHeatmapNormalization == CurvatureHeatmapNormalizationMode.FixedRange
			? $"{CurvatureHeatmapMin:0.###}..{CurvatureHeatmapMax:0.###}"
			: $"{stats.Min:0.###}..{stats.Max:0.###}";
		return $"DISTORTION HEAT MAP · metric {ResolveCurvatureHeatmapMetricLabel()} · {ResolveCurvatureHeatmapNormalizationLabel()} · range {rangeLabel}";
	}

	private void UpdateDualRealityHudState(bool forceOverlayRefresh)
	{
		UpdateDualRealityTelemetryConfig();
		UpdateDualRealityInsetVisibility();
		UpdateDualRealityInsetLayout();
		RefreshCollisionRadarHitActivity(forceOverlayRefresh);

		if (_dualRealityTitleLabel == null || _dualRealityModeLabel == null || _dualRealityStateLabel == null)
		{
			return;
		}

		if (_straightRayReferenceCache != null && _straightRayReferenceCache.TryGetBaselineSnapshot(out StraightRayReferenceCache.BaselineSnapshot snapshot))
		{
			_dualRealityBaselineView.Texture = snapshot.Texture;
			_dualRealityStateLabel.Text = snapshot.RefreshInFlight
				? "BASELINE · REFRESHING"
				: $"BASELINE · {(DualRealityFreezeBaseline ? "FROZEN" : "LIVE")} · REF #{snapshot.RefreshCount}";
		}
		else
		{
			_dualRealityBaselineView.Texture = null;
			_dualRealityStateLabel.Text = _straightRayReferenceCache?.RefreshInFlight == true
				? "BASELINE · REFRESHING"
				: "BASELINE · PENDING";
		}

		string wireframeMode = DualRealityWireframeReferenceOverlayEnabled
			? ResolveWireframePlacementLabel()
			: "SEMANTIC GLYPHS · OFF";
		string collisionMode = DualRealityCollisionRadarOverlayEnabled
			? ResolveCollisionPlacementLabel()
			: "COLLISION RADAR · OFF";
		string diagnosticMode = IsDualRealityDiagnosticOverlayEnabled()
			? ResolveDiagnosticOverlayPlacementLabel()
			: "SCALAR OVERLAY · OFF";
		_dualRealityModeLabel.Text = $"{ResolveDualRealityOverlayModeLabel()} · {diagnosticMode} · {wireframeMode} · {collisionMode}";
		_dualRealityTitleLabel.Modulate = EnableDualRealityResearchMode
			? new Color(0.96f, 0.98f, 1f, 0.96f)
			: new Color(0.82f, 0.84f, 0.88f, 0.72f);
		UpdateDualRealityOverlayTexture(forceOverlayRefresh);
		if (_dualRealityWireframeFullscreenOverlay != null || _dualRealityWireframePanelOverlay != null)
		{
			string categorySummary =
				$"fields={(DualRealityWireframeShowFieldGlyphs ? 1 : 0)} blv={(DualRealityWireframeShowBoundaryLayerGlyphs ? 1 : 0)} portals={(DualRealityWireframeShowWormholePortalGlyphs ? 1 : 0)} helpers={(DualRealityWireframeShowBackdropAndProbeHelpers ? 1 : 0)} anchor={(DualRealityWireframeShowCenterAnchor ? 1 : 0)} edgeDebug={(DualRealityWireframeAllowEdgeOnPlanesForDebug ? 1 : 0)}";
			string primitiveSummary = _dualRealityWireframeFullscreenOverlay?.Visible == true
				? _dualRealityWireframeFullscreenOverlay.DebugPrimitiveSummary
				: _dualRealityWireframePanelOverlay?.DebugPrimitiveSummary ?? string.Empty;
			string summary = $"placement={ResolveWireframePlacementLabel()} opacity={DualRealityWireframeOverlayOpacity:0.00} {categorySummary} glyphs=\"{primitiveSummary}\"";
			if (!string.Equals(summary, _dualRealityLastWireframeDebugSummary, StringComparison.Ordinal))
			{
				_dualRealityLastWireframeDebugSummary = summary;
				GD.Print($"[DualReality] wireframe {summary}");
			}
		}

		if (_dualRealityCollisionFullscreenOverlay != null || _dualRealityCollisionPanelOverlay != null)
		{
			string categorySummary =
				$"filter={DualRealityCollisionRadarFilter} bounds={DualRealityCollisionRadarBounds} primary={(DualRealityCollisionRadarShowPrimarySceneGeometry ? 1 : 0)} remapped={(DualRealityCollisionRadarShowRemappedSceneGeometry ? 1 : 0)} background={(DualRealityCollisionRadarShowBackgroundObjects ? 1 : 0)} helpers={(DualRealityCollisionRadarShowProbeHelpers ? 1 : 0)} centers={(DualRealityCollisionRadarShowCenterMarkers ? 1 : 0)} labels={(DualRealityCollisionRadarShowLabels ? 1 : 0)} leaders={(DualRealityCollisionRadarShowLeaderLines ? 1 : 0)} legend={(DualRealityCollisionRadarShowLegend ? 1 : 0)}";
			string visibleSummary = _dualRealityCollisionFullscreenOverlay?.Visible == true
				? _dualRealityCollisionFullscreenOverlay.DebugVisibleSummary
				: _dualRealityCollisionPanelOverlay?.DebugVisibleSummary ?? string.Empty;
			string summary = $"placement={ResolveCollisionPlacementLabel()} opacity={DualRealityCollisionRadarOpacity:0.00} {categorySummary} visible=\"{visibleSummary}\"";
			if (!string.Equals(summary, _dualRealityLastCollisionRadarDebugSummary, StringComparison.Ordinal))
			{
				_dualRealityLastCollisionRadarDebugSummary = summary;
				GD.Print($"[DualReality] collision_radar {summary}");
			}
		}
	}

	private void UpdateDualRealityResearch(double delta)
	{
		if (_straightRayReferenceCache == null)
		{
			return;
		}

		ApplyDualRealityRuntimeHotkeyGuard();
		_dualRealityOverlayRefreshSeconds += delta;
		_dualRealityCollisionActivityRefreshSeconds += delta;
		if (DualRealityRefreshBaseline)
		{
			DualRealityRefreshBaseline = false;
			RequestDualRealityBaselineRefresh("inspector_refresh", force: true);
		}

		UpdateDualRealityInsetVisibility();
		if (!EnableDualRealityResearchMode)
		{
			return;
		}

		if (_dualRealityStartupHoldActive)
		{
			bool startupReady = _straightRayReferenceCache.TryGetBaselineSnapshot(out _)
				&& !_straightRayReferenceCache.RefreshInFlight;
			bool startupTimedOut = Time.GetTicksMsec() - _dualRealityStartupHoldTicksMsec > 15000;
			if (startupReady || startupTimedOut)
			{
				_dualRealityStartupHoldActive = false;
				if (_filmCamera != null)
				{
					_filmCamera.UpdateEveryFrame = _dualRealityStartupHoldMainUpdateEveryFrame;
				}
				StartValidationHarnessRun();
				GD.Print($"[DualReality] startup_hold_released ready={(startupReady ? 1 : 0)} timed_out={(startupTimedOut ? 1 : 0)}");
			}

			UpdateDualRealityHudState(forceOverlayRefresh: true);
			return;
		}

		ulong stateHash = ComputeDualRealityStateHash();
		if (stateHash != _dualRealityObservedStateHash)
		{
			_dualRealityObservedStateHash = stateHash;
			_dualRealityStableFrames = 0;
		}
		else
		{
			_dualRealityStableFrames++;
		}

		bool forceOverlayRefresh = _lastDualRealityOverlayMode != DualRealityOverlayMode;
		_lastDualRealityOverlayMode = DualRealityOverlayMode;
		bool validationRunActive = _validationPendingCapture && !_validationCaptureCompleted;

		bool hasBaseline = _straightRayReferenceCache.TryGetBaselineSnapshot(out StraightRayReferenceCache.BaselineSnapshot baselineSnapshot);
		if (!hasBaseline && !_straightRayReferenceCache.RefreshInFlight && _dualRealityStableFrames >= 2)
		{
			RequestDualRealityBaselineRefresh("initial_fill", force: true);
		}
		else if (!validationRunActive
			&& !DualRealityFreezeBaseline
			&& hasBaseline
			&& baselineSnapshot.StateHash != stateHash
			&& !_straightRayReferenceCache.RefreshInFlight
			&& _dualRealityStableFrames >= 6)
		{
			RequestDualRealityBaselineRefresh("camera_settled", force: true);
		}

		UpdateDualRealityHudState(forceOverlayRefresh);
	}

	private void ApplyDualRealityRuntimeHotkeyGuard()
	{
		if (_filmCamera == null)
		{
			return;
		}

		if (EnableDualRealityResearchMode)
		{
			if (!_dualRealityRuntimeMacroHotkeysOverridden)
			{
				_dualRealityPreviousRuntimeMacroHotkeysEnabled = _filmCamera.RuntimeMacroHotkeysEnabled;
				_dualRealityPreviousRuntimeMacroSwitchingEnabled = _filmCamera.RuntimeMacroModeSwitchingEnabled;
				_dualRealityRuntimeMacroHotkeysOverridden = true;
			}

			_filmCamera.RuntimeMacroHotkeysEnabled = false;
			_filmCamera.RuntimeMacroModeSwitchingEnabled = false;
		}
		else if (_dualRealityRuntimeMacroHotkeysOverridden)
		{
			_filmCamera.RuntimeMacroHotkeysEnabled = _dualRealityPreviousRuntimeMacroHotkeysEnabled;
			_filmCamera.RuntimeMacroModeSwitchingEnabled = _dualRealityPreviousRuntimeMacroSwitchingEnabled;
			_dualRealityRuntimeMacroHotkeysOverridden = false;
		}
	}

	private static bool TryGetRigArgValue(string arg, string prefix, out string value)
	{
		if (!string.IsNullOrWhiteSpace(arg) && arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			value = arg[prefix.Length..];
			return true;
		}

		value = string.Empty;
		return false;
	}

	private static string[] GetRigCmdArgs()
	{
		string[] userArgs = OS.GetCmdlineUserArgs();
		string[] args = OS.GetCmdlineArgs();
		if (userArgs.Length == 0)
		{
			return args;
		}
		if (args.Length == 0)
		{
			return userArgs;
		}

		string[] merged = new string[userArgs.Length + args.Length];
		Array.Copy(userArgs, merged, userArgs.Length);
		Array.Copy(args, 0, merged, userArgs.Length, args.Length);
		return merged;
	}

	private static bool ParseCliBool(string value)
	{
		string normalized = value.Trim().ToLowerInvariant();
		return normalized is "1" or "true" or "on" or "yes";
	}

	private static bool TryParseDualRealityOverlayMode(string value, out DualRealityOverlayModeKind mode)
	{
		string normalized = value.Trim().ToLowerInvariant();
		switch (normalized)
		{
			case "none":
			case "off":
				mode = DualRealityOverlayModeKind.None;
				return true;
			case "filmheatmap":
			case "heatmap":
			case "film":
				mode = DualRealityOverlayModeKind.FilmHeatmap;
				return true;
			case "curvature":
			case "curvatureheatmap":
			case "curvatureplaceholder":
			case "curvatureproxy":
				mode = DualRealityOverlayModeKind.CurvatureHeatmap;
				return true;
			default:
				mode = DualRealityOverlayModeKind.None;
				return false;
		}
	}

	private static DualRealityWireframePlacementMode ParsePlacementMode(string value)
	{
		string normalized = value.Trim().ToLowerInvariant();
		return normalized switch
		{
			"panel" or "baseline" or "straight" => DualRealityWireframePlacementMode.StraightTransportReference,
			"both" => DualRealityWireframePlacementMode.Both,
			_ => DualRealityWireframePlacementMode.FullscreenCurved,
		};
	}

	private void ApplyDualRealityCmdArgs()
	{
		_validationCameraBackoffOverrideActive = false;
		foreach (string arg in GetRigCmdArgs())
		{
			if (TryGetRigArgValue(arg, "--dual-reality=", out string enabledValue))
			{
				EnableDualRealityResearchMode = ParseCliBool(enabledValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--enable-domain-telemetry=", out string domainTelemetryValue))
			{
				EnableDomainTelemetry = ParseCliBool(domainTelemetryValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--enable-domain-aware-first-hit-resolver=", out string domainAwareFirstHitValue))
			{
				EnableDomainAwareFirstHitResolver = ParseCliBool(domainAwareFirstHitValue);
				if (EnableDomainAwareFirstHitResolver)
				{
					EnableDomainTelemetry = true;
				}

				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-inset=", out string insetValue))
			{
				DualRealityInsetEnabled = ParseCliBool(insetValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-scale=", out string scaleValue)
				&& float.TryParse(scaleValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedScale))
			{
				DualRealityInsetScale = Mathf.Clamp(parsedScale, 0.15f, 0.5f);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-overlay=", out string overlayValue)
				&& TryParseDualRealityOverlayMode(overlayValue, out DualRealityOverlayModeKind overlayMode))
			{
				DualRealityOverlayMode = overlayMode;
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-overlay-placement=", out string overlayPlacementValue))
			{
				CurvatureHeatmapPlacement = ParsePlacementMode(overlayPlacementValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-curvature-enable=", out string curvatureEnabledValue))
			{
				EnableCurvatureHeatmap = ParseCliBool(curvatureEnabledValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-curvature-placement=", out string curvaturePlacementValue))
			{
				CurvatureHeatmapPlacement = ParsePlacementMode(curvaturePlacementValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-curvature-opacity=", out string curvatureOpacityValue)
				&& float.TryParse(curvatureOpacityValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedCurvatureOpacity))
			{
				CurvatureHeatmapOpacity = Mathf.Clamp(parsedCurvatureOpacity, 0f, 1f);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-curvature-metric=", out string curvatureMetricValue))
			{
				string normalized = curvatureMetricValue.Trim().ToLowerInvariant();
				CurvatureHeatmapMetric = normalized switch
				{
					"turnmax" or "maxturn" => CurvatureHeatmapMetricMode.MaxLocalTurnAngle,
					"curvaturemean" or "mean" => CurvatureHeatmapMetricMode.CurvatureMean,
					"curvaturemax" or "max" => CurvatureHeatmapMetricMode.CurvatureMax,
					"steps" or "stepdensity" or "placeholder" => CurvatureHeatmapMetricMode.Pass1StepDensityPlaceholder,
					_ => CurvatureHeatmapMetricMode.CumulativeTurnAngle,
				};
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-curvature-normalization=", out string curvatureNormalizationValue))
			{
				string normalized = curvatureNormalizationValue.Trim().ToLowerInvariant();
				CurvatureHeatmapNormalization = normalized switch
				{
					"full" or "fullrange" => CurvatureHeatmapNormalizationMode.AutoFullRange,
					"fixed" or "fixedrange" => CurvatureHeatmapNormalizationMode.FixedRange,
					_ => CurvatureHeatmapNormalizationMode.AutoPercentile,
				};
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-curvature-min=", out string curvatureMinValue)
				&& float.TryParse(curvatureMinValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedCurvatureMin))
			{
				CurvatureHeatmapMin = parsedCurvatureMin;
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-curvature-max=", out string curvatureMaxValue)
				&& float.TryParse(curvatureMaxValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedCurvatureMax))
			{
				CurvatureHeatmapMax = parsedCurvatureMax;
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-curvature-legend=", out string curvatureLegendValue))
			{
				CurvatureHeatmapShowLegend = ParseCliBool(curvatureLegendValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-wireframe=", out string wireframeValue))
			{
				DualRealityWireframeReferenceOverlayEnabled = ParseCliBool(wireframeValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-collision=", out string collisionValue))
			{
				DualRealityCollisionRadarOverlayEnabled = ParseCliBool(collisionValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-collision-placement=", out string collisionPlacementValue))
			{
				DualRealityCollisionRadarPlacement = ParsePlacementMode(collisionPlacementValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-freeze=", out string freezeValue))
			{
				DualRealityFreezeBaseline = ParseCliBool(freezeValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--dual-reality-refresh=", out string refreshValue))
			{
				DualRealityRefreshBaseline = ParseCliBool(refreshValue);
				continue;
			}

			if (TryGetRigArgValue(arg, "--camera-backoff=", out string backoffValue)
				&& float.TryParse(backoffValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedBackoff))
			{
				_validationCameraBackoffOverrideActive = true;
				ValidationCameraBackoffDistance = Mathf.Max(0f, parsedBackoff);
				continue;
			}

			if (TryGetRigArgValue(arg, "--camera-preset=", out string presetValue))
			{
				ValidationCameraPosePreset = NormalizeValidationCameraPosePreset(presetValue);
			}
		}
	}

	private async void ExecuteValidationCapture()
	{
		if (!IsInsideTree() || _validationCaptureCompleted)
		{
			return;
		}

		GD.Print($"[WormholeValidation] trigger elapsed={_validationCaptureElapsedSeconds:0.00}s reason={_validationTriggerReason}");
		LogFilmDiagnostics();

		if (CaptureValidationScreenshot)
		{
			CaptureFilmScreenshot();
		}

		SavePortalSectorArtifacts();
		if (CaptureValidationScreenshot && CaptureValidationCompositeScreenshot)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			CaptureCompositeScreenshot();
			if (EnableDualRealityResearchMode && DualRealityInsetEnabled && !string.IsNullOrWhiteSpace(DualRealityCapturePath))
			{
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				CaptureViewportScreenshot(DualRealityCapturePath.Trim(), "dual_reality_composite");
			}
		}
		await GenerateFigureQuartetArtifactsAsync();

		if (EmitBoundaryValidationSummaryAfterCapture)
		{
			_rayBeamRenderer?.EmitBoundaryValidationSummary(BoundaryValidationLabel);
		}
		GD.Print($"[WormholeValidation] remap_summary teleports={_teleportCount} active_portal={_activePortal?.Name ?? "none"}");

		_validationCaptureCompleted = true;
	}

	private void CaptureFilmScreenshot()
	{
		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera))
		{
			GD.PushWarning("[WormholeValidation] failed to save capture reason=missing_film_camera");
			return;
		}

		if (!_filmCamera.TryCopyFilmImageForTesting(out Image image) || image == null)
		{
			GD.PushWarning("[WormholeValidation] failed to save capture reason=missing_film_image");
			return;
		}

		string projectPath = string.IsNullOrWhiteSpace(ValidationCapturePath)
			? "res://output/wormhole_test/wormhole_validation_capture.png"
			: ValidationCapturePath.Trim();
		string absolutePath = ProjectSettings.GlobalizePath(projectPath);
		string directory = Path.GetDirectoryName(absolutePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			DirAccess.MakeDirRecursiveAbsolute(directory);
		}

		Error saveError = image.SavePng(absolutePath);
		if (saveError == Error.Ok)
		{
			GD.Print($"[WormholeValidation] capture_saved path={absolutePath} source=film_buffer");
			TryWriteDomainTelemetryArtifacts(absolutePath);
		}
		else
		{
			GD.PushWarning($"[WormholeValidation] failed to save capture path={absolutePath} error={saveError}");
		}
	}

	private void TryWriteDomainTelemetryArtifacts(string capturePath)
	{
		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera) || !_filmCamera.EnableDomainTelemetry)
		{
			return;
		}

		string outputDir = Path.GetDirectoryName(capturePath) ?? string.Empty;
		string captureStem = Path.GetFileNameWithoutExtension(capturePath);
		if (_filmCamera.TryWriteDomainTelemetryArtifactsForTesting(outputDir, captureStem, "wormhole_validation", out string summaryPath))
		{
			GD.Print($"[WormholeValidation][DomainTelemetry] summary_path={summaryPath}");
		}
		else
		{
			GD.PushWarning("[WormholeValidation][DomainTelemetry] no artifacts written");
		}
	}

	private void CaptureCompositeScreenshot()
	{
		string projectPath = string.IsNullOrWhiteSpace(ValidationCompositeCapturePath)
			? "res://output/wormhole_test/wormhole_validation_composed.png"
			: ValidationCompositeCapturePath.Trim();
		CaptureViewportScreenshot(projectPath, "viewport_composite");
	}

	private void CaptureViewportScreenshot(string projectPath, string sourceToken)
	{
		Viewport viewport = GetViewport();
		Image image = viewport?.GetTexture()?.GetImage();
		if (image == null)
		{
			GD.PushWarning($"[WormholeValidation] failed to save composite capture reason=missing_viewport_image source={sourceToken}");
			return;
		}

		string absolutePath = ProjectSettings.GlobalizePath(projectPath);
		string directory = Path.GetDirectoryName(absolutePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			DirAccess.MakeDirRecursiveAbsolute(directory);
		}

		Error saveError = image.SavePng(absolutePath);
		if (saveError == Error.Ok)
		{
			GD.Print($"[WormholeValidation] capture_saved path={absolutePath} source={sourceToken}");
		}
		else
		{
			GD.PushWarning($"[WormholeValidation] failed to save composite capture path={absolutePath} error={saveError} source={sourceToken}");
		}
	}

	private void SavePortalSectorArtifacts()
	{
		GD.Print("[WormholeValidation] portal_sector_artifacts begin");
		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera))
		{
			return;
		}

		if (!_filmCamera.TryGetWormholePortalSectorDetailedSnapshotForTesting(out GrinFilmCamera.WormholePortalSectorDetailedSnapshot snapshot)
			|| snapshot.Entries == null
			|| snapshot.Entries.Length == 0)
		{
			GD.Print("[WormholeValidation] portal_sector_artifacts skipped reason=no_sector_entries");
			return;
		}

		string jsonPath = BuildValidationArtifactAbsolutePath("wormhole_portal_sector_report.json");
		string heatmapPath = BuildValidationArtifactAbsolutePath("wormhole_portal_sector_heatmap.png");
		string ringDensityPath = BuildValidationArtifactAbsolutePath("wormhole_portal_ring_density.png");
		string usefulnessPath = BuildValidationArtifactAbsolutePath("wormhole_portal_usefulness.png");
		ProtoCausticInvariantContract invariantContract = BuildProtoCausticInvariantContract();
		LowValueSectorBudgetContract lowValueContract = BuildLowValueSectorBudgetContract();
		LowValueThrottleProfileSnapshot throttleProfile = BuildLowValueThrottleProfileSnapshot();
		WritePortalSectorJsonReport(snapshot, jsonPath, invariantContract,
			out PortalRingMetric[] ringMetrics,
			out ProtoCausticInvariantResult invariantResult,
			out PortalUsefulnessMetric[] usefulnessMetrics,
			out float invariantRingQueryShare,
			out float nonInvariantQueryShare,
			out LowValueSectorBudgetResult lowValueResult,
			lowValueContract,
			throttleProfile);
		WritePortalSectorHeatmap(snapshot, heatmapPath);
		WritePortalRingDensityVisualization(ringMetrics, snapshot.RadialBins, ringDensityPath, invariantContract, invariantResult);
		WritePortalUsefulnessVisualization(usefulnessMetrics, snapshot.ThetaBins, snapshot.RadialBins, usefulnessPath, invariantContract);
		LogProtoCausticInvariantResult(invariantContract, invariantResult);
		LogLowValueSectorBudgetResult(lowValueContract, lowValueResult);
		LogLowValueThrottleProfile(throttleProfile);
		_latestProtoCausticInvariantResult = invariantResult;
		_latestLowValueSectorBudgetResult = lowValueResult;
		_latestLowValueThrottleProfile = throttleProfile;
		_latestInvariantRingMetric = invariantResult?.TargetMetric;
		_hasLatestPerfFrameReport = _filmCamera.TryGetLatestPerfFrameReportForTesting(out _latestPerfFrameReport);
		_researchOverlay?.SetValidationContractStatus(invariantResult.Passed, lowValueResult.Passed);
		if (_researchOverlayStatusLabel != null)
		{
			_researchOverlayStatusLabel.Text =
				$"{(invariantResult.Passed ? "CAUSTIC PASS" : "CAUSTIC FAIL")} · {(lowValueResult.Passed ? "BUDGET PASS" : "BUDGET FAIL")}";
			_researchOverlayStatusLabel.Modulate = invariantResult.Passed && lowValueResult.Passed
				? new Color(0.82f, 0.98f, 0.86f, 0.96f)
				: new Color(1f, 0.76f, 0.68f, 0.96f);
		}
		GD.Print(
			$"[WormholeValidation] portal_usefulness invariant_ring_query_share={FormatFloat(invariantRingQueryShare)} " +
			$"non_invariant_query_share={FormatFloat(nonInvariantQueryShare)} sector_count={usefulnessMetrics?.Length ?? 0}");
	}

	private string BuildValidationArtifactAbsolutePath(string fileName)
	{
		string projectPath = string.IsNullOrWhiteSpace(ValidationCapturePath)
			? "res://output/wormhole_test/wormhole_validation_capture.png"
			: ValidationCapturePath.Trim();
		string absoluteCapturePath = ProjectSettings.GlobalizePath(projectPath);
		string directory = Path.GetDirectoryName(absoluteCapturePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			DirAccess.MakeDirRecursiveAbsolute(directory);
		}
		return Path.Combine(directory ?? ProjectSettings.GlobalizePath("res://output/wormhole_test"), fileName);
	}

	private string BuildValidationFigureAbsolutePath(string fileName)
	{
		string directory = Path.Combine(
			Path.GetDirectoryName(BuildValidationArtifactAbsolutePath("wormhole_validation_capture.png"))
				?? ProjectSettings.GlobalizePath("res://output/wormhole_test"),
			"figures");
		DirAccess.MakeDirRecursiveAbsolute(directory);
		return Path.Combine(directory, fileName);
	}

	private async System.Threading.Tasks.Task GenerateFigureQuartetArtifactsAsync()
	{
		string figureAPath = BuildValidationFigureAbsolutePath("figure_A_main_render.png");
		string figureBPath = BuildValidationFigureAbsolutePath("figure_B_composed_overlay.png");
		string figureCPath = BuildValidationFigureAbsolutePath("figure_C_ring_density.png");
		string figureDPath = BuildValidationFigureAbsolutePath("figure_D_metrics_table.png");
		string captionsPath = BuildValidationFigureAbsolutePath("figure_captions.md");
		CopyArtifactIfAvailable(ProjectSettings.GlobalizePath(ValidationCapturePath), figureAPath, "figure_A_main_render");
		CopyArtifactIfAvailable(ProjectSettings.GlobalizePath(ValidationCompositeCapturePath), figureBPath, "figure_B_composed_overlay");
		CopyArtifactIfAvailable(BuildValidationArtifactAbsolutePath("wormhole_portal_ring_density.png"), figureCPath, "figure_C_ring_density");
		await WriteMetricsTableFigureAsync(figureDPath);
		WriteFigureCaptionsMarkdown(captionsPath);
		GD.Print(
			$"[WormholeValidation] figure_quartet_saved dir={Path.GetDirectoryName(figureAPath)} " +
			$"A={Path.GetFileName(figureAPath)} B={Path.GetFileName(figureBPath)} " +
			$"C={Path.GetFileName(figureCPath)} D={Path.GetFileName(figureDPath)}");
	}

	private static void CopyArtifactIfAvailable(string sourceAbsolutePath, string destinationAbsolutePath, string label)
	{
		if (string.IsNullOrWhiteSpace(sourceAbsolutePath) || !File.Exists(sourceAbsolutePath))
		{
			GD.PushWarning($"[WormholeValidation] quartet_copy_missing label={label} path={sourceAbsolutePath}");
			return;
		}

		string directory = Path.GetDirectoryName(destinationAbsolutePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			DirAccess.MakeDirRecursiveAbsolute(directory);
		}

		File.Copy(sourceAbsolutePath, destinationAbsolutePath, overwrite: true);
		GD.Print($"[WormholeValidation] quartet_copy_saved label={label} path={destinationAbsolutePath}");
	}

	private async System.Threading.Tasks.Task WriteMetricsTableFigureAsync(string absolutePath)
	{
		string directory = Path.GetDirectoryName(absolutePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			DirAccess.MakeDirRecursiveAbsolute(directory);
		}

		GrinFilmCamera.WormholePostRemapDiagnosticsSnapshot wormholeSnapshot = default;
		bool hasWormholeSnapshot = _filmCamera != null
			&& GodotObject.IsInstanceValid(_filmCamera)
			&& _filmCamera.TryGetWormholePostRemapDiagnosticsForTesting(out wormholeSnapshot);
		if (_filmCamera != null && GodotObject.IsInstanceValid(_filmCamera)
			&& _filmCamera.TryGetLatestPerfFrameReportForTesting(out PerfFrameReport latestPerf))
		{
			_latestPerfFrameReport = latestPerf;
			_hasLatestPerfFrameReport = true;
		}

		SubViewport viewport = new()
		{
			Disable3D = true,
			TransparentBg = false,
			Size = new Vector2I(920, 420),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			HandleInputLocally = false
		};
		AddChild(viewport);

		Control root = new()
		{
			CustomMinimumSize = viewport.Size,
			Size = viewport.Size,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		viewport.AddChild(root);

		ColorRect background = new()
		{
			Color = new Color(0.045f, 0.05f, 0.07f, 1f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(background);

		MarginContainer margin = new()
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 22);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_right", 22);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		root.AddChild(margin);

		VBoxContainer stack = new()
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		margin.AddChild(stack);

		stack.AddChild(CreateMetricsLabel(
			"WORMHOLE VALIDATION QUARTET · FIGURE D",
			19,
			new Color(0.95f, 0.97f, 1f, 0.98f)));
		stack.AddChild(CreateMetricsLabel(
			"Deterministic static harness metrics for the current kept wormhole profile.",
			12,
			new Color(0.78f, 0.84f, 0.92f, 0.92f)));
		stack.AddChild(CreateMetricsSpacer(8));

		HBoxContainer statusRow = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		statusRow.AddThemeConstantOverride("separation", 12);
		statusRow.AddChild(CreateBadgeLabel(
			_latestProtoCausticInvariantResult?.Passed == true ? "PROTO-CAUSTIC PASS" : "PROTO-CAUSTIC FAIL",
			_latestProtoCausticInvariantResult?.Passed == true
				? new Color(0.22f, 0.74f, 0.38f, 1f)
				: new Color(0.86f, 0.28f, 0.24f, 1f)));
		statusRow.AddChild(CreateBadgeLabel(
			_latestLowValueSectorBudgetResult?.Passed == true ? "LOW-VALUE BUDGET PASS" : "LOW-VALUE BUDGET FAIL",
			_latestLowValueSectorBudgetResult?.Passed == true
				? new Color(0.18f, 0.63f, 0.84f, 1f)
				: new Color(0.86f, 0.28f, 0.24f, 1f)));
		stack.AddChild(statusRow);
		stack.AddChild(CreateMetricsSpacer(10));

		PortalRingMetric targetMetric = _latestInvariantRingMetric;
		stack.AddChild(CreateMetricsLabel("Optical / Contract", 15, new Color(0.92f, 0.95f, 1f, 0.96f)));
		stack.AddChild(CreateMetricsLabel(
			BuildMetricsBlock(new (string, string)[]
			{
				("Throttle", BuildThrottleProfileSummary(_latestLowValueThrottleProfile)),
				("Hit density", targetMetric != null ? FormatFloat(targetMetric.MeanHitSamplesPerTheta) : "n/a"),
				("Hit continuity", targetMetric != null ? FormatFloat(targetMetric.HitContinuityRatio) : "n/a"),
				("Overlap continuity", targetMetric != null ? FormatFloat(targetMetric.PositiveOverlapContinuityRatio) : "n/a"),
				("Radial gradient", targetMetric != null ? FormatFloat(targetMetric.RadialHitGradientFromPrev) : "n/a"),
				("Caustic margin", _latestProtoCausticInvariantResult != null ? FormatFloat(_latestProtoCausticInvariantResult.HitDensityDelta) : "n/a"),
				("Budget margin", _latestLowValueSectorBudgetResult != null ? FormatFloat(_latestLowValueSectorBudgetResult.QueryShareDelta) : "n/a")
			}),
			12,
			new Color(0.90f, 0.94f, 1f, 0.96f),
			wrap: false));
		stack.AddChild(CreateMetricsSpacer(10));
		stack.AddChild(CreateMetricsLabel("Performance / Output", 15, new Color(0.92f, 0.95f, 1f, 0.96f)));
		stack.AddChild(CreateMetricsLabel(
			BuildMetricsBlock(new (string, string)[]
			{
				("pass2.query", _hasLatestPerfFrameReport ? $"{_latestPerfFrameReport.Pass2QueryMs:0.00} ms" : "n/a"),
				("pass2.physics", _hasLatestPerfFrameReport ? $"{_latestPerfFrameReport.Pass2PhysMs:0.00} ms" : "n/a"),
				("pass2.candidate", _hasLatestPerfFrameReport ? $"{_latestPerfFrameReport.Pass2CandidateEvalMs:0.00} ms" : "n/a"),
				("geom hits", hasWormholeSnapshot ? wormholeSnapshot.PostRemapGeometryHits.ToString(CultureInfo.InvariantCulture) : "n/a"),
				("final write px", hasWormholeSnapshot ? wormholeSnapshot.PostRemapFinalWritePixels.ToString(CultureInfo.InvariantCulture) : "n/a"),
				("candidate segs", hasWormholeSnapshot ? wormholeSnapshot.PostRemapCandidateSegments.ToString(CultureInfo.InvariantCulture) : "n/a"),
				("post-remap queries", hasWormholeSnapshot ? wormholeSnapshot.PostRemapQueries.ToString(CultureInfo.InvariantCulture) : "n/a")
			}),
			12,
			new Color(0.90f, 0.94f, 1f, 0.96f),
			wrap: false));

		stack.AddChild(CreateMetricsSpacer(10));
		stack.AddChild(CreateMetricsLabel(
			"Figure D summarizes the active optical contracts, throttle profile, and deterministic pass-2 measurements.",
			11,
			new Color(0.72f, 0.78f, 0.86f, 0.9f)));

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		Image image = viewport.GetTexture()?.GetImage();
		if (image == null)
		{
			GD.PushWarning("[WormholeValidation] failed to save figure_D_metrics_table reason=missing_viewport_image");
			viewport.QueueFree();
			return;
		}

		Error saveError = image.SavePng(absolutePath);
		if (saveError == Error.Ok)
		{
			GD.Print($"[WormholeValidation] quartet_metrics_saved path={absolutePath}");
		}
		else
		{
			GD.PushWarning($"[WormholeValidation] failed to save figure_D_metrics_table path={absolutePath} error={saveError}");
		}

		viewport.QueueFree();
	}

	private static string BuildMetricsBlock((string Label, string Value)[] rows)
	{
		StringBuilder sb = new();
		for (int i = 0; i < rows.Length; i++)
		{
			if (i > 0)
			{
				sb.Append('\n');
			}
			sb.Append(rows[i].Label.PadRight(20)).Append("  ").Append(rows[i].Value);
		}
		return sb.ToString();
	}

	private static Control CreateMetricsSpacer(int height)
	{
		return new Control
		{
			CustomMinimumSize = new Vector2(0, height),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}

	private static Label CreateMetricsLabel(string text, int fontSize, Color color, bool wrap = true)
	{
		Label label = new()
		{
			Text = text ?? string.Empty,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off
		};
		LabelSettings settings = new()
		{
			FontSize = fontSize,
			FontColor = color
		};
		label.LabelSettings = settings;
		return label;
	}

	private static Label CreateBadgeLabel(string text, Color badgeColor)
	{
		Label label = CreateMetricsLabel(text, 11, new Color(0.98f, 0.99f, 1f, 0.98f));
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		label.AddThemeColorOverride("font_outline_color", badgeColor.Darkened(0.55f));
		label.AddThemeConstantOverride("outline_size", 6);
		return label;
	}

	private static string BuildThrottleProfileSummary(LowValueThrottleProfileSnapshot profile)
	{
		if (!profile.Enabled)
		{
			return "disabled";
		}

		return $"L{profile.LayerIndex} · R{profile.RadialBin} · th {{{profile.ThetaBinsCsv}}} · p={profile.Period}";
	}

	private void WriteFigureCaptionsMarkdown(string absolutePath)
	{
		string directory = Path.GetDirectoryName(absolutePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			DirAccess.MakeDirRecursiveAbsolute(directory);
		}

		string text =
			"# Wormhole Validation Figure Captions\n\n" +
			"**Figure A.** Raw accumulated film-buffer capture from the deterministic wormhole validation harness. The image is saved directly from the film path without the research overlay.\n\n" +
			"**Figure B.** Composed deterministic validation capture showing the film result together with the research-view inset, HUD text, and contract status line.\n\n" +
			"**Figure C.** Portal-centric ring-density visualization derived from the saved sector aggregation. The highlighted destination-side annulus corresponds to the active proto-caustic invariant target ring.\n\n" +
			"**Figure D.** Compact summary card for the deterministic wormhole harness, reporting contract status, active low-value throttle profile, optical metrics for the invariant ring, and the current pass-2 performance buckets.\n";
		File.WriteAllText(absolutePath, text);
		GD.Print($"[WormholeValidation] quartet_captions_saved path={absolutePath}");
	}

	private void WritePortalSectorJsonReport(
		GrinFilmCamera.WormholePortalSectorDetailedSnapshot snapshot,
		string absolutePath,
		ProtoCausticInvariantContract invariantContract,
		out PortalRingMetric[] ringMetrics,
		out ProtoCausticInvariantResult invariantResult,
		out PortalUsefulnessMetric[] usefulnessMetrics,
		out float invariantRingQueryShare,
		out float nonInvariantQueryShare,
		out LowValueSectorBudgetResult lowValueResult,
		LowValueSectorBudgetContract lowValueContract,
		LowValueThrottleProfileSnapshot throttleProfile)
	{
		PortalSectorArtifactCell[,,] cells = BuildPortalSectorArtifactCells(snapshot, out int layerCount, out int maxSamples, out int occupiedCells);
		ringMetrics = BuildPortalRingMetrics(cells, layerCount, snapshot.RadialBins, snapshot.ThetaBins);
		usefulnessMetrics = BuildPortalUsefulnessMetrics(cells, layerCount, snapshot.RadialBins, snapshot.ThetaBins, invariantContract);
		ComputePortalUsefulnessShares(usefulnessMetrics, out invariantRingQueryShare, out nonInvariantQueryShare);
		lowValueResult = EvaluateLowValueSectorBudget(usefulnessMetrics, lowValueContract);
		ComputeProtoCausticFlags(ringMetrics,
			out int stableAnnularBands,
			out int sharpRadialTransitions,
			out int sharpAngularTransitions,
			out float maxHitDensity,
			out float maxRadialGradient,
			out float maxAngularVariation);
		invariantResult = EvaluateProtoCausticInvariant(ringMetrics, invariantContract);
		StringBuilder sb = new();
		sb.AppendLine("{");
		sb.AppendLine($"  \"theta_bins\": {snapshot.ThetaBins},");
		sb.AppendLine($"  \"radial_bins\": {snapshot.RadialBins},");
		sb.AppendLine($"  \"direction_bins\": {snapshot.DirectionBins},");
		sb.AppendLine($"  \"layer_count\": {layerCount},");
		sb.AppendLine($"  \"entry_count\": {snapshot.Entries.Length},");
		sb.AppendLine($"  \"occupied_cells\": {occupiedCells},");
		sb.AppendLine($"  \"max_cell_samples\": {maxSamples},");
		AppendValidationCameraPoseProfileJson(sb);
		sb.AppendLine(",");
		AppendLowValueThrottleProfileJson(sb, throttleProfile);
		sb.AppendLine(",");
		sb.AppendLine("  \"invariant_contract\": {");
		sb.AppendLine($"    \"layer\": {invariantContract.LayerIndex},");
		sb.AppendLine($"    \"radial_bin\": {invariantContract.RadialBin},");
		sb.AppendLine($"    \"min_hit_density\": {FormatFloat(invariantContract.MinimumHitDensity)},");
		sb.AppendLine($"    \"min_hit_continuity_ratio\": {FormatFloat(invariantContract.MinimumHitContinuityRatio)},");
		sb.AppendLine($"    \"min_positive_overlap_continuity_ratio\": {FormatFloat(invariantContract.MinimumPositiveOverlapContinuityRatio)},");
		sb.AppendLine($"    \"min_radial_gradient\": {FormatFloat(invariantContract.MinimumRadialGradient)}");
		sb.AppendLine("  },");
		sb.AppendLine("  \"invariant_result\": {");
		sb.AppendLine($"    \"target_found\": {invariantResult.TargetFound.ToString().ToLowerInvariant()},");
		sb.AppendLine($"    \"passed\": {invariantResult.Passed.ToString().ToLowerInvariant()},");
		sb.AppendLine($"    \"failure_reason\": \"{invariantResult.FailureReason}\",");
		sb.AppendLine($"    \"hit_density_delta\": {FormatFloat(invariantResult.HitDensityDelta)},");
		sb.AppendLine($"    \"hit_continuity_delta\": {FormatFloat(invariantResult.HitContinuityDelta)},");
		sb.AppendLine($"    \"positive_overlap_continuity_delta\": {FormatFloat(invariantResult.PositiveOverlapContinuityDelta)},");
		sb.AppendLine($"    \"radial_gradient_delta\": {FormatFloat(invariantResult.RadialGradientDelta)}");
		sb.AppendLine("  },");
		AppendLowValueSectorBudgetJson(sb, lowValueContract, lowValueResult);
		sb.AppendLine(",");
		sb.AppendLine("  \"proto_caustic\": {");
		sb.AppendLine($"    \"stable_annular_bands\": {stableAnnularBands},");
		sb.AppendLine($"    \"sharp_radial_transitions\": {sharpRadialTransitions},");
		sb.AppendLine($"    \"sharp_angular_transitions\": {sharpAngularTransitions},");
		sb.AppendLine($"    \"max_hit_density\": {FormatFloat(maxHitDensity)},");
		sb.AppendLine($"    \"max_radial_gradient\": {FormatFloat(maxRadialGradient)},");
		sb.AppendLine($"    \"max_angular_variation\": {FormatFloat(maxAngularVariation)}");
		sb.AppendLine("  },");
		AppendUsefulnessSummaryJson(sb, usefulnessMetrics, invariantRingQueryShare, nonInvariantQueryShare);
		sb.AppendLine(",");
		sb.AppendLine("  \"ring_metrics\": [");
		for (int i = 0; i < ringMetrics.Length; i++)
		{
			PortalRingMetric metric = ringMetrics[i];
			if (i > 0)
			{
				sb.AppendLine(",");
			}
			sb.Append("    {");
			sb.Append($"\"layer\": {metric.LayerIndex}, ");
			sb.Append($"\"radial_bin\": {metric.RadialBin}, ");
			sb.Append($"\"occupied_theta_cells\": {metric.OccupiedThetaCells}, ");
			sb.Append($"\"adjacency_pairs\": {metric.AdjacencyPairs}, ");
			sb.Append($"\"candidate_continuity_pairs\": {metric.CandidateContinuityPairs}, ");
			sb.Append($"\"hit_continuity_pairs\": {metric.HitContinuityPairs}, ");
			sb.Append($"\"positive_overlap_continuity_pairs\": {metric.PositiveOverlapContinuityPairs}, ");
			sb.Append($"\"candidate_invariant_cells\": {metric.CandidateInvariantCells}, ");
			sb.Append($"\"positive_overlap_cells\": {metric.PositiveOverlapCells}, ");
			sb.Append($"\"positive_overlap_invariant_cells\": {metric.PositiveOverlapInvariantCells}, ");
			sb.Append($"\"total_samples\": {metric.TotalSamples}, ");
			sb.Append($"\"total_hit_samples\": {metric.TotalHitSamples}, ");
			sb.Append($"\"min_hit_samples_per_theta\": {metric.MinHitSamplesPerTheta}, ");
			sb.Append($"\"max_hit_samples_per_theta\": {metric.MaxHitSamplesPerTheta}, ");
			sb.Append($"\"mean_samples_per_theta\": {FormatFloat(metric.MeanSamplesPerTheta)}, ");
			sb.Append($"\"mean_hit_samples_per_theta\": {FormatFloat(metric.MeanHitSamplesPerTheta)}, ");
			sb.Append($"\"positive_overlap_density\": {FormatFloat(metric.PositiveOverlapDensity)}, ");
			sb.Append($"\"candidate_continuity_ratio\": {FormatFloat(metric.CandidateContinuityRatio)}, ");
			sb.Append($"\"hit_continuity_ratio\": {FormatFloat(metric.HitContinuityRatio)}, ");
			sb.Append($"\"positive_overlap_continuity_ratio\": {FormatFloat(metric.PositiveOverlapContinuityRatio)}, ");
			sb.Append($"\"angular_hit_variation\": {FormatFloat(metric.AngularHitVariation)}, ");
			sb.Append($"\"angular_sample_variation\": {FormatFloat(metric.AngularSampleVariation)}, ");
			sb.Append($"\"radial_hit_gradient_from_prev\": {FormatFloat(metric.RadialHitGradientFromPrev)}, ");
			sb.Append($"\"radial_sample_gradient_from_prev\": {FormatFloat(metric.RadialSampleGradientFromPrev)}, ");
			sb.Append($"\"proto_caustic_score\": {FormatFloat(metric.ProtoCausticScore)}, ");
			sb.Append($"\"stable_annular_hit_band\": {metric.StableAnnularHitBand.ToString().ToLowerInvariant()}, ");
			sb.Append($"\"sharp_radial_transition\": {metric.SharpRadialTransition.ToString().ToLowerInvariant()}, ");
			sb.Append($"\"sharp_angular_transition\": {metric.SharpAngularTransition.ToString().ToLowerInvariant()}");
			sb.Append("}");
		}
		sb.AppendLine();
		sb.AppendLine("  ],");
		sb.AppendLine("  \"rings\": [");

		bool wroteRing = false;
		for (int layer = 0; layer < layerCount; layer++)
		{
			for (int radial = 0; radial < snapshot.RadialBins; radial++)
			{
				ComputeRingContinuity(cells, layer, radial, snapshot.ThetaBins,
					out int occupiedThetaCells,
					out int adjacencyPairs,
					out int candidateContinuityPairs,
					out int hitContinuityPairs,
					out int positiveOverlapContinuityPairs);

				if (wroteRing)
				{
					sb.AppendLine(",");
				}
				wroteRing = true;
				sb.Append("    {");
				sb.Append($"\"layer\": {layer}, ");
				sb.Append($"\"radial_bin\": {radial}, ");
				sb.Append($"\"occupied_theta_cells\": {occupiedThetaCells}, ");
				sb.Append($"\"adjacency_pairs\": {adjacencyPairs}, ");
				sb.Append($"\"candidate_continuity_pairs\": {candidateContinuityPairs}, ");
				sb.Append($"\"hit_continuity_pairs\": {hitContinuityPairs}, ");
				sb.Append($"\"positive_overlap_continuity_pairs\": {positiveOverlapContinuityPairs}");
				sb.Append("}");
			}
		}

		sb.AppendLine();
		sb.AppendLine("  ],");
		sb.AppendLine("  \"entries\": [");
		for (int i = 0; i < snapshot.Entries.Length; i++)
		{
			ref readonly var entry = ref snapshot.Entries[i];
			if (i > 0)
			{
				sb.AppendLine(",");
			}
			sb.Append("    {");
			sb.Append($"\"layer\": {entry.LayerIndex}, ");
			sb.Append($"\"theta_bin\": {entry.ThetaBin}, ");
			sb.Append($"\"radial_bin\": {entry.RadialBin}, ");
			sb.Append($"\"direction_bin\": {entry.DirectionBin}, ");
			sb.Append($"\"remap_bin\": {entry.RemapBin}, ");
			sb.Append($"\"query_samples\": {entry.QuerySamples}, ");
			sb.Append($"\"candidate_samples\": {entry.CandidateSamples}, ");
			sb.Append($"\"first_candidate_count\": {entry.FirstCandidateCount}, ");
			sb.Append($"\"first_candidate_hash\": \"{entry.FirstCandidateHash}\", ");
			sb.Append($"\"candidate_invariant\": {entry.CandidateInvariant.ToString().ToLowerInvariant()}, ");
			sb.Append($"\"positive_overlap_samples\": {entry.PositiveOverlapSamples}, ");
			sb.Append($"\"first_positive_overlap_count\": {entry.FirstPositiveOverlapCount}, ");
			sb.Append($"\"positive_overlap_invariant\": {entry.PositiveOverlapInvariant.ToString().ToLowerInvariant()}, ");
			sb.Append($"\"hit_samples\": {entry.HitSamples}, ");
			sb.Append($"\"background_hits\": {entry.BackgroundHits}, ");
			sb.Append($"\"source_hits\": {entry.SourceHits}, ");
			sb.Append($"\"unclassified_hits\": {entry.UnclassifiedHits}, ");
			sb.Append($"\"absorbed_hits\": {entry.AbsorbedHits}, ");
			sb.Append($"\"miss_hits\": {entry.MissHits}");
			sb.Append("}");
		}
		sb.AppendLine();
		sb.AppendLine("  ]");
		sb.AppendLine("}");

		File.WriteAllText(absolutePath, sb.ToString());
		GD.Print($"[WormholeValidation] portal_sector_report_saved path={absolutePath}");
	}

	private void WritePortalSectorHeatmap(GrinFilmCamera.WormholePortalSectorDetailedSnapshot snapshot, string absolutePath)
	{
		PortalSectorArtifactCell[,,] cells = BuildPortalSectorArtifactCells(snapshot, out int layerCount, out int maxSamples, out _);
		int cellSize = 12;
		int panelCount = 4;
		int width = Math.Max(1, snapshot.ThetaBins * cellSize);
		int height = Math.Max(1, layerCount * snapshot.RadialBins * panelCount * cellSize);
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(new Color(0.03f, 0.03f, 0.04f, 1f));

		for (int layer = 0; layer < layerCount; layer++)
		{
			for (int radial = 0; radial < snapshot.RadialBins; radial++)
			{
				for (int theta = 0; theta < snapshot.ThetaBins; theta++)
				{
					PortalSectorArtifactCell cell = cells[layer, radial, theta];
					int samples = cell?.Samples ?? 0;
					Color populationColor = ComputePopulationColor(samples, maxSamples);
					Color candidateColor = ComputeCandidateColor(cell);
					Color overlapColor = ComputePositiveOverlapColor(cell);
					Color hitColor = ComputeHitColor(cell);
					PaintHeatmapCell(image, theta, layer, radial, 0, snapshot.RadialBins, cellSize, populationColor);
					PaintHeatmapCell(image, theta, layer, radial, 1, snapshot.RadialBins, cellSize, candidateColor);
					PaintHeatmapCell(image, theta, layer, radial, 2, snapshot.RadialBins, cellSize, overlapColor);
					PaintHeatmapCell(image, theta, layer, radial, 3, snapshot.RadialBins, cellSize, hitColor);
				}
			}
		}

		Error saveError = image.SavePng(absolutePath);
		if (saveError == Error.Ok)
		{
			GD.Print($"[WormholeValidation] portal_sector_heatmap_saved path={absolutePath}");
		}
		else
		{
			GD.PushWarning($"[WormholeValidation] failed to save portal sector heatmap path={absolutePath} error={saveError}");
		}
	}

	private static PortalSectorArtifactCell[,,] BuildPortalSectorArtifactCells(
		GrinFilmCamera.WormholePortalSectorDetailedSnapshot snapshot,
		out int layerCount,
		out int maxSamples,
		out int occupiedCells)
	{
		layerCount = 0;
		foreach (var entry in snapshot.Entries)
		{
			layerCount = Mathf.Max(layerCount, entry.LayerIndex + 1);
		}
		layerCount = Math.Max(1, layerCount);
		PortalSectorArtifactCell[,,] cells = new PortalSectorArtifactCell[layerCount, Math.Max(1, snapshot.RadialBins), Math.Max(1, snapshot.ThetaBins)];
		maxSamples = 0;
		occupiedCells = 0;

		foreach (var entry in snapshot.Entries)
		{
			if (entry.LayerIndex < 0 || entry.LayerIndex >= layerCount
				|| entry.RadialBin < 0 || entry.RadialBin >= snapshot.RadialBins
				|| entry.ThetaBin < 0 || entry.ThetaBin >= snapshot.ThetaBins)
			{
				continue;
			}

			PortalSectorArtifactCell cell = cells[entry.LayerIndex, entry.RadialBin, entry.ThetaBin];
			if (cell == null)
			{
				cell = new PortalSectorArtifactCell();
				cell.LayerIndex = entry.LayerIndex;
				cell.RadialBin = entry.RadialBin;
				cell.ThetaBin = entry.ThetaBin;
				cells[entry.LayerIndex, entry.RadialBin, entry.ThetaBin] = cell;
				occupiedCells++;
			}

			cell.QuerySamples += entry.QuerySamples;
			cell.Samples += entry.CandidateSamples;
			if (!cell.HasCandidateState)
			{
				cell.HasCandidateState = true;
				cell.CandidateInvariant = entry.CandidateInvariant;
				cell.CandidateCount = entry.FirstCandidateCount;
				cell.CandidateHash = entry.FirstCandidateHash;
			}
			else if (!entry.CandidateInvariant
				|| !cell.CandidateInvariant
				|| cell.CandidateCount != entry.FirstCandidateCount
				|| cell.CandidateHash != entry.FirstCandidateHash)
			{
				cell.CandidateInvariant = false;
			}

			if (entry.PositiveOverlapSamples > 0)
			{
				if (!cell.HasPositiveOverlap)
				{
					cell.HasPositiveOverlap = true;
					cell.PositiveOverlapInvariant = entry.PositiveOverlapInvariant;
					cell.PositiveOverlapCount = entry.FirstPositiveOverlapCount;
				}
				else if (!entry.PositiveOverlapInvariant
					|| !cell.PositiveOverlapInvariant
					|| cell.PositiveOverlapCount != entry.FirstPositiveOverlapCount)
				{
					cell.PositiveOverlapInvariant = false;
				}
			}

			cell.HitSamples += entry.HitSamples;
			cell.BackgroundHits += entry.BackgroundHits;
			cell.SourceHits += entry.SourceHits;
			cell.UnclassifiedHits += entry.UnclassifiedHits;
			cell.AbsorbedHits += entry.AbsorbedHits;
			cell.MissHits += entry.MissHits;
			maxSamples = Mathf.Max(maxSamples, cell.Samples);
		}

		return cells;
	}

	private static PortalUsefulnessMetric[] BuildPortalUsefulnessMetrics(
		PortalSectorArtifactCell[,,] cells,
		int layerCount,
		int radialBins,
		int thetaBins,
		ProtoCausticInvariantContract invariantContract)
	{
		List<PortalUsefulnessMetric> metrics = new();
		double totalQuerySamples = 0.0;
		for (int layer = 0; layer < layerCount; layer++)
		{
			for (int radial = 0; radial < radialBins; radial++)
			{
				for (int theta = 0; theta < thetaBins; theta++)
				{
					PortalSectorArtifactCell cell = cells[layer, radial, theta];
					if (cell == null || cell.QuerySamples <= 0)
						continue;

					bool invariantRing = layer == invariantContract.LayerIndex && radial == invariantContract.RadialBin;
					cell.InvariantRing = invariantRing;
					PortalUsefulnessMetric metric = new()
					{
						LayerIndex = layer,
						ThetaBin = theta,
						RadialBin = radial,
						InvariantRing = invariantRing,
						QuerySamples = cell.QuerySamples,
						CandidateSamples = cell.Samples,
						HitSamples = cell.HitSamples,
						PositiveOverlapSamples = cell.PositiveOverlapCount > 0 ? cell.PositiveOverlapCount : 0,
						DominantHitKind = DetermineDominantHitKind(cell)
					};
					metric.QueryHitYield = metric.QuerySamples > 0
						? (float)metric.HitSamples / metric.QuerySamples
						: 0f;
					metric.LowValueScore = metric.QuerySamples * (1f - metric.QueryHitYield) * (metric.InvariantRing ? 0.25f : 1f);
					metric.HighValueScore = metric.QuerySamples * metric.QueryHitYield * (metric.InvariantRing ? 1.35f : 1f);
					totalQuerySamples += metric.QuerySamples;
					metrics.Add(metric);
				}
			}
		}

		float totalQuery = (float)Math.Max(1.0, totalQuerySamples);
		foreach (PortalUsefulnessMetric metric in metrics)
		{
			metric.QueryShare = metric.QuerySamples / totalQuery;
		}

		return metrics.ToArray();
	}

	private static void ComputePortalUsefulnessShares(
		PortalUsefulnessMetric[] metrics,
		out float invariantRingQueryShare,
		out float nonInvariantQueryShare)
	{
		invariantRingQueryShare = 0f;
		nonInvariantQueryShare = 0f;
		if (metrics == null || metrics.Length == 0)
			return;

		double totalQuery = 0.0;
		double invariantQuery = 0.0;
		foreach (PortalUsefulnessMetric metric in metrics)
		{
			totalQuery += metric.QuerySamples;
			if (metric.InvariantRing)
				invariantQuery += metric.QuerySamples;
		}

		if (totalQuery <= 0.0)
			return;

		invariantRingQueryShare = (float)(invariantQuery / totalQuery);
		nonInvariantQueryShare = 1f - invariantRingQueryShare;
	}

	private LowValueSectorBudgetContract BuildLowValueSectorBudgetContract()
	{
		return new LowValueSectorBudgetContract(
			LowValueSectorBudgetLayer,
			LowValueSectorBudgetRadialBin,
			LowValueSectorBudgetBaselineQueryShare,
			LowValueSectorBudgetMaxQueryShareScale);
	}

	private LowValueThrottleProfileSnapshot BuildLowValueThrottleProfileSnapshot()
	{
		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera))
		{
			return new LowValueThrottleProfileSnapshot(false, 0, 0, string.Empty, 0);
		}

		return new LowValueThrottleProfileSnapshot(
			_filmCamera.WormholePortalSectorLowValueThrottleEnabled,
			_filmCamera.WormholePortalSectorLowValueThrottleLayer,
			_filmCamera.WormholePortalSectorLowValueThrottleRadialBin,
			_filmCamera.WormholePortalSectorLowValueThetaBinsCsv,
			_filmCamera.WormholePortalSectorLowValueThrottlePeriod);
	}

	private LowValueSectorBudgetResult EvaluateLowValueSectorBudget(
		PortalUsefulnessMetric[] metrics,
		LowValueSectorBudgetContract contract)
	{
		LowValueSectorBudgetResult result = new()
		{
			MaximumAllowedQueryShare = contract.MaximumAllowedQueryShare
		};
		if (!LowValueSectorBudgetEnabled || metrics == null || metrics.Length == 0)
		{
			result.TargetFound = metrics != null && metrics.Length > 0;
			result.Passed = true;
			result.FailureReason = LowValueSectorBudgetEnabled ? "no_metrics" : "disabled";
			return result;
		}

		int totalQuerySamples = 0;
		int targetQuerySamples = 0;
		foreach (PortalUsefulnessMetric metric in metrics)
		{
			totalQuerySamples += metric.QuerySamples;
			if (metric.LayerIndex == contract.LayerIndex && metric.RadialBin == contract.RadialBin)
			{
				targetQuerySamples += metric.QuerySamples;
				result.TargetFound = true;
			}
		}

		result.TotalQuerySamples = totalQuerySamples;
		result.TargetQuerySamples = targetQuerySamples;
		if (!result.TargetFound || totalQuerySamples <= 0)
		{
			result.Passed = !LowValueSectorBudgetEnabled;
			result.FailureReason = result.TargetFound ? "no_total_query_samples" : "target_ring_missing";
			return result;
		}

		result.ActualQueryShare = (float)targetQuerySamples / totalQuerySamples;
		result.QueryShareDelta = result.MaximumAllowedQueryShare - result.ActualQueryShare;
		result.Passed = result.QueryShareDelta >= 0f;
		result.FailureReason = result.Passed ? "ok" : "query_share_above_budget";
		return result;
	}

	private static void AppendUsefulnessSummaryJson(
		StringBuilder sb,
		PortalUsefulnessMetric[] metrics,
		float invariantRingQueryShare,
		float nonInvariantQueryShare)
	{
		sb.AppendLine("  \"usefulness_summary\": {");
		if (metrics == null || metrics.Length == 0)
		{
			sb.AppendLine("    \"total_query_samples\": 0,");
			sb.AppendLine("    \"invariant_ring_query_share\": 0,");
			sb.AppendLine("    \"non_invariant_query_share\": 0,");
			sb.AppendLine("    \"high_cost_low_value\": [],");
			sb.AppendLine("    \"high_value\": []");
			sb.Append("  }");
			return;
		}

		int totalQuerySamples = 0;
		int totalHitSamples = 0;
		foreach (PortalUsefulnessMetric metric in metrics)
		{
			totalQuerySamples += metric.QuerySamples;
			totalHitSamples += metric.HitSamples;
		}

		PortalUsefulnessMetric[] lowValue = (PortalUsefulnessMetric[])metrics.Clone();
		Array.Sort(lowValue, (a, b) =>
		{
			int scoreCmp = b.LowValueScore.CompareTo(a.LowValueScore);
			if (scoreCmp != 0) return scoreCmp;
			return b.QuerySamples.CompareTo(a.QuerySamples);
		});
		PortalUsefulnessMetric[] highValue = (PortalUsefulnessMetric[])metrics.Clone();
		Array.Sort(highValue, (a, b) =>
		{
			int scoreCmp = b.HighValueScore.CompareTo(a.HighValueScore);
			if (scoreCmp != 0) return scoreCmp;
			return b.HitSamples.CompareTo(a.HitSamples);
		});

		sb.AppendLine($"    \"total_query_samples\": {totalQuerySamples},");
		sb.AppendLine($"    \"total_hit_samples\": {totalHitSamples},");
		sb.AppendLine($"    \"invariant_ring_query_share\": {FormatFloat(invariantRingQueryShare)},");
		sb.AppendLine($"    \"non_invariant_query_share\": {FormatFloat(nonInvariantQueryShare)},");
		sb.AppendLine("    \"high_cost_low_value\": [");
		AppendUsefulnessMetricArrayJson(sb, lowValue, 3);
		sb.AppendLine();
		sb.AppendLine("    ],");
		sb.AppendLine("    \"high_value\": [");
		AppendUsefulnessMetricArrayJson(sb, highValue, 3);
		sb.AppendLine();
		sb.AppendLine("    ]");
		sb.Append("  }");
	}

	private static void AppendLowValueSectorBudgetJson(
		StringBuilder sb,
		LowValueSectorBudgetContract contract,
		LowValueSectorBudgetResult result)
	{
		sb.AppendLine("  \"low_value_sector_budget_contract\": {");
		sb.AppendLine($"    \"layer\": {contract.LayerIndex},");
		sb.AppendLine($"    \"radial_bin\": {contract.RadialBin},");
		sb.AppendLine($"    \"baseline_query_share\": {FormatFloat(contract.BaselineQueryShare)},");
		sb.AppendLine($"    \"max_query_share_scale\": {FormatFloat(contract.MaxQueryShareScale)},");
		sb.AppendLine($"    \"maximum_allowed_query_share\": {FormatFloat(contract.MaximumAllowedQueryShare)}");
		sb.AppendLine("  },");
		sb.AppendLine("  \"low_value_sector_budget_result\": {");
		sb.AppendLine($"    \"target_found\": {result.TargetFound.ToString().ToLowerInvariant()},");
		sb.AppendLine($"    \"passed\": {result.Passed.ToString().ToLowerInvariant()},");
		sb.AppendLine($"    \"failure_reason\": \"{result.FailureReason}\",");
		sb.AppendLine($"    \"actual_query_share\": {FormatFloat(result.ActualQueryShare)},");
		sb.AppendLine($"    \"maximum_allowed_query_share\": {FormatFloat(result.MaximumAllowedQueryShare)},");
		sb.AppendLine($"    \"query_share_delta\": {FormatFloat(result.QueryShareDelta)},");
		sb.AppendLine($"    \"target_query_samples\": {result.TargetQuerySamples},");
		sb.AppendLine($"    \"total_query_samples\": {result.TotalQuerySamples}");
		sb.Append("  }");
	}

	private void AppendValidationCameraPoseProfileJson(StringBuilder sb)
	{
		sb.AppendLine("  \"validation_camera_pose_profile\": {");
		sb.AppendLine($"    \"preset\": \"{_appliedValidationCameraPosePreset}\",");
		sb.AppendLine($"    \"override_active\": {_validationCameraBackoffOverrideActive.ToString().ToLowerInvariant()},");
		sb.AppendLine($"    \"effective_backoff\": {FormatFloat(_appliedValidationCameraBackoffDistance)},");
		sb.AppendLine($"    \"validation_nearfield_backoff\": {FormatFloat(ValidationNearfieldBackoffDistance)},");
		sb.AppendLine($"    \"presentation_mid_backoff\": {FormatFloat(PresentationMidBackoffDistance)},");
		sb.AppendLine($"    \"presentation_far_backoff\": {FormatFloat(PresentationFarBackoffDistance)}");
		sb.Append("  }");
	}

	private static void AppendLowValueThrottleProfileJson(
		StringBuilder sb,
		LowValueThrottleProfileSnapshot profile)
	{
		sb.AppendLine("  \"low_value_throttle_profile\": {");
		sb.AppendLine($"    \"enabled\": {profile.Enabled.ToString().ToLowerInvariant()},");
		sb.AppendLine($"    \"layer\": {profile.LayerIndex},");
		sb.AppendLine($"    \"radial_bin\": {profile.RadialBin},");
		sb.AppendLine($"    \"theta_bins_csv\": \"{profile.ThetaBinsCsv}\",");
		sb.AppendLine($"    \"period\": {profile.Period}");
		sb.Append("  }");
	}

	private static void AppendUsefulnessMetricArrayJson(
		StringBuilder sb,
		PortalUsefulnessMetric[] metrics,
		int maxCount)
	{
		int written = 0;
		for (int i = 0; i < metrics.Length && written < maxCount; i++)
		{
			PortalUsefulnessMetric metric = metrics[i];
			if (written > 0)
				sb.AppendLine(",");
			sb.Append("      {");
			sb.Append($"\"layer\": {metric.LayerIndex}, ");
			sb.Append($"\"radial_bin\": {metric.RadialBin}, ");
			sb.Append($"\"theta_bin\": {metric.ThetaBin}, ");
			sb.Append($"\"invariant_ring\": {metric.InvariantRing.ToString().ToLowerInvariant()}, ");
			sb.Append($"\"query_samples\": {metric.QuerySamples}, ");
			sb.Append($"\"candidate_samples\": {metric.CandidateSamples}, ");
			sb.Append($"\"hit_samples\": {metric.HitSamples}, ");
			sb.Append($"\"query_share\": {FormatFloat(metric.QueryShare)}, ");
			sb.Append($"\"query_hit_yield\": {FormatFloat(metric.QueryHitYield)}, ");
			sb.Append($"\"dominant_hit_kind\": \"{metric.DominantHitKind}\", ");
			sb.Append($"\"low_value_score\": {FormatFloat(metric.LowValueScore)}, ");
			sb.Append($"\"high_value_score\": {FormatFloat(metric.HighValueScore)}");
			sb.Append("}");
			written++;
		}
	}

	private static void ComputeRingContinuity(
		PortalSectorArtifactCell[,,] cells,
		int layer,
		int radial,
		int thetaBins,
		out int occupiedThetaCells,
		out int adjacencyPairs,
		out int candidateContinuityPairs,
		out int hitContinuityPairs,
		out int positiveOverlapContinuityPairs)
	{
		occupiedThetaCells = 0;
		adjacencyPairs = 0;
		candidateContinuityPairs = 0;
		hitContinuityPairs = 0;
		positiveOverlapContinuityPairs = 0;

		for (int theta = 0; theta < thetaBins; theta++)
		{
			PortalSectorArtifactCell current = cells[layer, radial, theta];
			PortalSectorArtifactCell next = cells[layer, radial, (theta + 1) % thetaBins];
			if (current != null && current.Samples > 0)
			{
				occupiedThetaCells++;
			}
			if (current == null || next == null || current.Samples <= 0 || next.Samples <= 0)
			{
				continue;
			}

			adjacencyPairs++;
			if (current.HasCandidateState && next.HasCandidateState
				&& current.CandidateInvariant && next.CandidateInvariant
				&& current.CandidateCount == next.CandidateCount
				&& current.CandidateHash == next.CandidateHash)
			{
				candidateContinuityPairs++;
			}
			if (DetermineDominantHitKind(current) == DetermineDominantHitKind(next))
			{
				hitContinuityPairs++;
			}
			if (current.HasPositiveOverlap && next.HasPositiveOverlap
				&& current.PositiveOverlapInvariant && next.PositiveOverlapInvariant
				&& current.PositiveOverlapCount == next.PositiveOverlapCount)
			{
				positiveOverlapContinuityPairs++;
			}
		}
	}

	private static PortalRingMetric[] BuildPortalRingMetrics(
		PortalSectorArtifactCell[,,] cells,
		int layerCount,
		int radialBins,
		int thetaBins)
	{
		PortalRingMetric[] metrics = new PortalRingMetric[Math.Max(1, layerCount) * Math.Max(1, radialBins)];
		int index = 0;
		for (int layer = 0; layer < layerCount; layer++)
		{
			for (int radial = 0; radial < radialBins; radial++)
			{
				PortalRingMetric metric = new()
				{
					LayerIndex = layer,
					RadialBin = radial,
					MinHitSamplesPerTheta = int.MaxValue
				};

				int[] hitValues = new int[Math.Max(1, thetaBins)];
				int[] sampleValues = new int[Math.Max(1, thetaBins)];
				int[] overlapValues = new int[Math.Max(1, thetaBins)];

				for (int theta = 0; theta < thetaBins; theta++)
				{
					PortalSectorArtifactCell cell = cells[layer, radial, theta];
					if (cell == null || cell.Samples <= 0)
					{
						metric.MinHitSamplesPerTheta = 0;
						continue;
					}

					metric.OccupiedThetaCells++;
					metric.TotalSamples += cell.Samples;
					metric.TotalHitSamples += cell.HitSamples;
					metric.MinHitSamplesPerTheta = Math.Min(metric.MinHitSamplesPerTheta, cell.HitSamples);
					metric.MaxHitSamplesPerTheta = Math.Max(metric.MaxHitSamplesPerTheta, cell.HitSamples);
					if (cell.HasCandidateState && cell.CandidateInvariant)
					{
						metric.CandidateInvariantCells++;
					}
					if (cell.HasPositiveOverlap)
					{
						metric.PositiveOverlapCells++;
						if (cell.PositiveOverlapInvariant)
						{
							metric.PositiveOverlapInvariantCells++;
						}
					}

					hitValues[theta] = cell.HitSamples;
					sampleValues[theta] = cell.Samples;
					overlapValues[theta] = cell.HasPositiveOverlap ? Math.Max(cell.PositiveOverlapCount, 1) : 0;
				}

				if (metric.OccupiedThetaCells == 0)
				{
					metric.MinHitSamplesPerTheta = 0;
					metrics[index++] = metric;
					continue;
				}

				metric.MeanSamplesPerTheta = (float)metric.TotalSamples / metric.OccupiedThetaCells;
				metric.MeanHitSamplesPerTheta = (float)metric.TotalHitSamples / metric.OccupiedThetaCells;
				metric.PositiveOverlapDensity = (float)metric.PositiveOverlapCells / metric.OccupiedThetaCells;

				float hitVariationAccum = 0f;
				float sampleVariationAccum = 0f;
				for (int theta = 0; theta < thetaBins; theta++)
				{
					PortalSectorArtifactCell current = cells[layer, radial, theta];
					PortalSectorArtifactCell next = cells[layer, radial, (theta + 1) % thetaBins];
					if (current == null || next == null || current.Samples <= 0 || next.Samples <= 0)
					{
						continue;
					}

					metric.AdjacencyPairs++;
					if (current.HasCandidateState && next.HasCandidateState
						&& current.CandidateInvariant && next.CandidateInvariant
						&& current.CandidateCount == next.CandidateCount
						&& current.CandidateHash == next.CandidateHash)
					{
						metric.CandidateContinuityPairs++;
					}
					if (DetermineDominantHitKind(current) == DetermineDominantHitKind(next))
					{
						metric.HitContinuityPairs++;
					}
					if (current.HasPositiveOverlap && next.HasPositiveOverlap
						&& current.PositiveOverlapInvariant && next.PositiveOverlapInvariant
						&& current.PositiveOverlapCount == next.PositiveOverlapCount)
					{
						metric.PositiveOverlapContinuityPairs++;
					}

					float hitDenom = Math.Max(1f, Math.Max(metric.MaxHitSamplesPerTheta, 1));
					float sampleDenom = Math.Max(1f, metric.MeanSamplesPerTheta);
					hitVariationAccum += Math.Abs(hitValues[theta] - hitValues[(theta + 1) % thetaBins]) / hitDenom;
					sampleVariationAccum += Math.Abs(sampleValues[theta] - sampleValues[(theta + 1) % thetaBins]) / sampleDenom;
				}

				if (metric.AdjacencyPairs > 0)
				{
					metric.CandidateContinuityRatio = (float)metric.CandidateContinuityPairs / metric.AdjacencyPairs;
					metric.HitContinuityRatio = (float)metric.HitContinuityPairs / metric.AdjacencyPairs;
					metric.PositiveOverlapContinuityRatio = (float)metric.PositiveOverlapContinuityPairs / metric.AdjacencyPairs;
					metric.AngularHitVariation = hitVariationAccum / metric.AdjacencyPairs;
					metric.AngularSampleVariation = sampleVariationAccum / metric.AdjacencyPairs;
				}

				metrics[index++] = metric;
			}
		}

		for (int layer = 0; layer < layerCount; layer++)
		{
			for (int radial = 1; radial < radialBins; radial++)
			{
				PortalRingMetric current = metrics[layer * radialBins + radial];
				PortalRingMetric previous = metrics[layer * radialBins + (radial - 1)];
				current.RadialHitGradientFromPrev = current.MeanHitSamplesPerTheta - previous.MeanHitSamplesPerTheta;
				current.RadialSampleGradientFromPrev = current.MeanSamplesPerTheta - previous.MeanSamplesPerTheta;
			}
		}

		return metrics;
	}

	private static void ComputeProtoCausticFlags(
		PortalRingMetric[] metrics,
		out int stableAnnularBands,
		out int sharpRadialTransitions,
		out int sharpAngularTransitions,
		out float maxHitDensity,
		out float maxRadialGradient,
		out float maxAngularVariation)
	{
		stableAnnularBands = 0;
		sharpRadialTransitions = 0;
		sharpAngularTransitions = 0;
		maxHitDensity = 0f;
		maxRadialGradient = 0f;
		maxAngularVariation = 0f;
		if (metrics == null || metrics.Length == 0)
		{
			return;
		}

		float hitMeanSum = 0f;
		float hitMeanSqSum = 0f;
		int hitMeanCount = 0;
		float gradAbsSum = 0f;
		float gradAbsSqSum = 0f;
		int gradCount = 0;
		float angVarSum = 0f;
		float angVarSqSum = 0f;
		int angVarCount = 0;

		foreach (PortalRingMetric metric in metrics)
		{
			maxHitDensity = Math.Max(maxHitDensity, metric.MeanHitSamplesPerTheta);
			maxRadialGradient = Math.Max(maxRadialGradient, Math.Abs(metric.RadialHitGradientFromPrev));
			maxAngularVariation = Math.Max(maxAngularVariation, metric.AngularHitVariation);
			if (metric.OccupiedThetaCells > 0)
			{
				hitMeanSum += metric.MeanHitSamplesPerTheta;
				hitMeanSqSum += metric.MeanHitSamplesPerTheta * metric.MeanHitSamplesPerTheta;
				hitMeanCount++;
				angVarSum += metric.AngularHitVariation;
				angVarSqSum += metric.AngularHitVariation * metric.AngularHitVariation;
				angVarCount++;
			}
			if (metric.RadialBin > 0)
			{
				float absGrad = Math.Abs(metric.RadialHitGradientFromPrev);
				gradAbsSum += absGrad;
				gradAbsSqSum += absGrad * absGrad;
				gradCount++;
			}
		}

		float hitMeanAvg = hitMeanCount > 0 ? hitMeanSum / hitMeanCount : 0f;
		float hitMeanStd = hitMeanCount > 0 ? MathF.Sqrt(Math.Max(0f, hitMeanSqSum / hitMeanCount - hitMeanAvg * hitMeanAvg)) : 0f;
		float gradAvg = gradCount > 0 ? gradAbsSum / gradCount : 0f;
		float gradStd = gradCount > 0 ? MathF.Sqrt(Math.Max(0f, gradAbsSqSum / gradCount - gradAvg * gradAvg)) : 0f;
		float angAvg = angVarCount > 0 ? angVarSum / angVarCount : 0f;
		float angStd = angVarCount > 0 ? MathF.Sqrt(Math.Max(0f, angVarSqSum / angVarCount - angAvg * angAvg)) : 0f;

		foreach (PortalRingMetric metric in metrics)
		{
			metric.StableAnnularHitBand = metric.OccupiedThetaCells > 0
				&& metric.HitContinuityRatio >= 0.95f
				&& metric.MeanHitSamplesPerTheta >= hitMeanAvg + hitMeanStd;
			metric.SharpRadialTransition = metric.RadialBin > 0
				&& Math.Abs(metric.RadialHitGradientFromPrev) >= gradAvg + gradStd
				&& Math.Abs(metric.RadialHitGradientFromPrev) > 0f;
			metric.SharpAngularTransition = metric.OccupiedThetaCells > 0
				&& metric.AngularHitVariation >= angAvg + angStd
				&& metric.AngularHitVariation > 0f;
			float hitDensityNorm = maxHitDensity > 1e-5f ? metric.MeanHitSamplesPerTheta / maxHitDensity : 0f;
			float gradNorm = maxRadialGradient > 1e-5f ? Math.Abs(metric.RadialHitGradientFromPrev) / maxRadialGradient : 0f;
			float angularContrast = maxAngularVariation > 1e-5f ? metric.AngularHitVariation / maxAngularVariation : 0f;
			metric.ProtoCausticScore = 0.45f * hitDensityNorm + 0.35f * gradNorm + 0.20f * angularContrast;

			if (metric.StableAnnularHitBand) stableAnnularBands++;
			if (metric.SharpRadialTransition) sharpRadialTransitions++;
			if (metric.SharpAngularTransition) sharpAngularTransitions++;
		}
	}

	private void WritePortalRingDensityVisualization(
		PortalRingMetric[] metrics,
		int radialBins,
		string absolutePath,
		ProtoCausticInvariantContract invariantContract,
		ProtoCausticInvariantResult invariantResult)
	{
		if (metrics == null || metrics.Length == 0)
		{
			return;
		}

		int layerCount = 0;
		float maxHitDensity = 0f;
		float maxOverlapDensity = 0f;
		float maxRadialGradient = 0f;
		float maxAngularVariation = 0f;
		foreach (PortalRingMetric metric in metrics)
		{
			layerCount = Math.Max(layerCount, metric.LayerIndex + 1);
			maxHitDensity = Math.Max(maxHitDensity, metric.MeanHitSamplesPerTheta);
			maxOverlapDensity = Math.Max(maxOverlapDensity, metric.PositiveOverlapDensity);
			maxRadialGradient = Math.Max(maxRadialGradient, Math.Abs(metric.RadialHitGradientFromPrev));
			maxAngularVariation = Math.Max(maxAngularVariation, metric.AngularHitVariation);
		}

		int cellWidth = 48;
		int cellHeight = 40;
		int panelCount = 4;
		int width = Math.Max(1, radialBins * cellWidth);
		int height = Math.Max(1, layerCount * panelCount * cellHeight);
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(new Color(0.02f, 0.02f, 0.03f, 1f));

		foreach (PortalRingMetric metric in metrics)
		{
			int x = metric.RadialBin * cellWidth;
			int basePanelY = metric.LayerIndex * panelCount * cellHeight;
			PaintMetricCell(image, x, basePanelY + 0 * cellHeight, cellWidth, cellHeight,
				new Color(0.08f, 0.08f, 0.12f, 1f).Lerp(new Color(0.95f, 0.78f, 0.16f, 1f),
					maxHitDensity > 1e-5f ? metric.MeanHitSamplesPerTheta / maxHitDensity : 0f));
			PaintMetricCell(image, x, basePanelY + 1 * cellHeight, cellWidth, cellHeight,
				new Color(0.08f, 0.08f, 0.12f, 1f).Lerp(new Color(0.18f, 0.72f, 0.92f, 1f),
					maxOverlapDensity > 1e-5f ? metric.PositiveOverlapDensity / maxOverlapDensity : 0f));
			PaintMetricCell(image, x, basePanelY + 2 * cellHeight, cellWidth, cellHeight,
				new Color(0.08f, 0.08f, 0.12f, 1f).Lerp(new Color(0.95f, 0.32f, 0.18f, 1f),
					maxRadialGradient > 1e-5f ? Math.Abs(metric.RadialHitGradientFromPrev) / maxRadialGradient : 0f));
			PaintMetricCell(image, x, basePanelY + 3 * cellHeight, cellWidth, cellHeight,
				new Color(0.08f, 0.08f, 0.12f, 1f).Lerp(new Color(0.86f, 0.26f, 0.78f, 1f),
					maxAngularVariation > 1e-5f ? metric.AngularHitVariation / maxAngularVariation : 0f));

			if (metric.LayerIndex == invariantContract.LayerIndex && metric.RadialBin == invariantContract.RadialBin)
			{
				Color outline = invariantResult.Passed
					? new Color(0.24f, 0.95f, 0.34f, 1f)
					: new Color(0.95f, 0.24f, 0.20f, 1f);
				for (int panel = 0; panel < panelCount; panel++)
				{
					DrawMetricOutline(image, x, basePanelY + panel * cellHeight, cellWidth, cellHeight, outline);
				}
			}
		}

		Error saveError = image.SavePng(absolutePath);
		if (saveError == Error.Ok)
		{
			GD.Print($"[WormholeValidation] portal_ring_density_saved path={absolutePath}");
		}
		else
		{
			GD.PushWarning($"[WormholeValidation] failed to save portal ring density path={absolutePath} error={saveError}");
		}
	}

	private void WritePortalUsefulnessVisualization(
		PortalUsefulnessMetric[] metrics,
		int thetaBins,
		int radialBins,
		string absolutePath,
		ProtoCausticInvariantContract invariantContract)
	{
		if (metrics == null || metrics.Length == 0)
			return;

		int layerCount = 0;
		int maxQueries = 0;
		float maxYield = 0f;
		foreach (PortalUsefulnessMetric metric in metrics)
		{
			layerCount = Math.Max(layerCount, metric.LayerIndex + 1);
			maxQueries = Math.Max(maxQueries, metric.QuerySamples);
			maxYield = Math.Max(maxYield, metric.QueryHitYield);
		}

		int cellSize = 12;
		int panelCount = 3;
		int width = Math.Max(1, thetaBins * cellSize);
		int height = Math.Max(1, layerCount * radialBins * panelCount * cellSize);
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(new Color(0.03f, 0.03f, 0.04f, 1f));

		foreach (PortalUsefulnessMetric metric in metrics)
		{
			Color queryColor = ComputeUsefulnessQueryColor(metric.QuerySamples, maxQueries);
			Color yieldColor = ComputeUsefulnessYieldColor(metric.QueryHitYield, maxYield);
			Color contributionColor = ComputeUsefulnessContributionColor(metric);
			PaintPortalUsefulnessCell(image, metric.ThetaBin, metric.LayerIndex, metric.RadialBin, 0, radialBins, panelCount, cellSize, queryColor);
			PaintPortalUsefulnessCell(image, metric.ThetaBin, metric.LayerIndex, metric.RadialBin, 1, radialBins, panelCount, cellSize, yieldColor);
			PaintPortalUsefulnessCell(image, metric.ThetaBin, metric.LayerIndex, metric.RadialBin, 2, radialBins, panelCount, cellSize, contributionColor);

			if (metric.LayerIndex == invariantContract.LayerIndex && metric.RadialBin == invariantContract.RadialBin)
			{
				int startX = metric.ThetaBin * cellSize;
				for (int panel = 0; panel < panelCount; panel++)
				{
					int startY = ((metric.LayerIndex * panelCount + panel) * radialBins + metric.RadialBin) * cellSize;
					DrawMetricOutline(image, startX, startY, cellSize, cellSize, new Color(0.24f, 0.95f, 0.34f, 1f));
				}
			}
		}

		Error saveError = image.SavePng(absolutePath);
		if (saveError == Error.Ok)
		{
			GD.Print($"[WormholeValidation] portal_usefulness_heatmap_saved path={absolutePath}");
		}
		else
		{
			GD.PushWarning($"[WormholeValidation] failed to save portal usefulness heatmap path={absolutePath} error={saveError}");
		}
	}

	private static void PaintPortalUsefulnessCell(
		Image image,
		int theta,
		int layer,
		int radial,
		int panelIndex,
		int radialBins,
		int panelCount,
		int cellSize,
		Color color)
	{
		int startX = theta * cellSize;
		int startY = ((layer * panelCount + panelIndex) * radialBins + radial) * cellSize;
		for (int y = 0; y < cellSize; y++)
		{
			for (int x = 0; x < cellSize; x++)
			{
				bool border = x == 0 || y == 0;
				image.SetPixel(startX + x, startY + y, border ? color.Darkened(0.35f) : color);
			}
		}
	}

	private static void PaintMetricCell(Image image, int startX, int startY, int width, int height, Color color)
	{
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
				image.SetPixel(startX + x, startY + y, border ? color.Darkened(0.35f) : color);
			}
		}
	}

	private static void DrawMetricOutline(Image image, int startX, int startY, int width, int height, Color color)
	{
		for (int x = 0; x < width; x++)
		{
			image.SetPixel(startX + x, startY, color);
			image.SetPixel(startX + x, startY + height - 1, color);
		}
		for (int y = 0; y < height; y++)
		{
			image.SetPixel(startX, startY + y, color);
			image.SetPixel(startX + width - 1, startY + y, color);
		}
	}

	private ProtoCausticInvariantContract BuildProtoCausticInvariantContract()
	{
		return new ProtoCausticInvariantContract(
			ProtoCausticInvariantLayer,
			ProtoCausticInvariantRadialBin,
			ProtoCausticInvariantMinHitDensity,
			ProtoCausticInvariantMinHitContinuityRatio,
			ProtoCausticInvariantMinPositiveOverlapContinuityRatio,
			ProtoCausticInvariantMinRadialGradient);
	}

	private static ProtoCausticInvariantResult EvaluateProtoCausticInvariant(
		PortalRingMetric[] metrics,
		ProtoCausticInvariantContract invariantContract)
	{
		ProtoCausticInvariantResult result = new();
		if (metrics == null)
		{
			result.FailureReason = "missing_metrics";
			return result;
		}

		foreach (PortalRingMetric metric in metrics)
		{
			if (metric.LayerIndex != invariantContract.LayerIndex || metric.RadialBin != invariantContract.RadialBin)
			{
				continue;
			}

			result.TargetFound = true;
			result.TargetMetric = metric;
			result.HitDensityDelta = metric.MeanHitSamplesPerTheta - invariantContract.MinimumHitDensity;
			result.HitContinuityDelta = metric.HitContinuityRatio - invariantContract.MinimumHitContinuityRatio;
			result.PositiveOverlapContinuityDelta = metric.PositiveOverlapContinuityRatio - invariantContract.MinimumPositiveOverlapContinuityRatio;
			result.RadialGradientDelta = metric.RadialHitGradientFromPrev - invariantContract.MinimumRadialGradient;

			if (result.HitDensityDelta < 0f)
			{
				result.FailureReason = "hit_density_below_threshold";
				return result;
			}
			if (result.HitContinuityDelta < 0f)
			{
				result.FailureReason = "hit_continuity_below_threshold";
				return result;
			}
			if (result.PositiveOverlapContinuityDelta < 0f)
			{
				result.FailureReason = "positive_overlap_continuity_below_threshold";
				return result;
			}
			if (result.RadialGradientDelta < 0f)
			{
				result.FailureReason = "radial_gradient_below_threshold";
				return result;
			}

			result.Passed = true;
			result.FailureReason = "ok";
			return result;
		}

		result.FailureReason = "target_ring_missing";
		return result;
	}

	private static void LogProtoCausticInvariantResult(
		ProtoCausticInvariantContract invariantContract,
		ProtoCausticInvariantResult invariantResult)
	{
		if (!invariantResult.TargetFound)
		{
			GD.Print(
				$"[WormholeValidation] proto_caustic_invariant pass=false target_layer={invariantContract.LayerIndex} " +
				$"target_radial_bin={invariantContract.RadialBin} reason={invariantResult.FailureReason}");
			return;
		}

		GD.Print(
			$"[WormholeValidation] proto_caustic_invariant pass={invariantResult.Passed.ToString().ToLowerInvariant()} " +
			$"target_layer={invariantContract.LayerIndex} target_radial_bin={invariantContract.RadialBin} " +
			$"reason={invariantResult.FailureReason} hit_density={FormatFloat(invariantResult.TargetMetric.MeanHitSamplesPerTheta)} " +
			$"hit_continuity={FormatFloat(invariantResult.TargetMetric.HitContinuityRatio)} " +
			$"positive_overlap_continuity={FormatFloat(invariantResult.TargetMetric.PositiveOverlapContinuityRatio)} " +
			$"radial_gradient={FormatFloat(invariantResult.TargetMetric.RadialHitGradientFromPrev)} " +
			$"d_hit_density={FormatFloat(invariantResult.HitDensityDelta)} " +
			$"d_hit_continuity={FormatFloat(invariantResult.HitContinuityDelta)} " +
			$"d_positive_overlap={FormatFloat(invariantResult.PositiveOverlapContinuityDelta)} " +
			$"d_radial_gradient={FormatFloat(invariantResult.RadialGradientDelta)}");
	}

	private static void LogLowValueSectorBudgetResult(
		LowValueSectorBudgetContract contract,
		LowValueSectorBudgetResult result)
	{
		GD.Print(
			$"[WormholeValidation] low_value_sector_budget pass={result.Passed.ToString().ToLowerInvariant()} " +
			$"target_layer={contract.LayerIndex} target_radial_bin={contract.RadialBin} " +
			$"reason={result.FailureReason} actual_query_share={FormatFloat(result.ActualQueryShare)} " +
			$"max_query_share={FormatFloat(result.MaximumAllowedQueryShare)} " +
			$"d_query_share={FormatFloat(result.QueryShareDelta)} target_query_samples={result.TargetQuerySamples} " +
			$"total_query_samples={result.TotalQuerySamples}");
	}

	private static void LogLowValueThrottleProfile(LowValueThrottleProfileSnapshot profile)
	{
		GD.Print(
			$"[WormholeValidation] low_value_throttle enabled={profile.Enabled.ToString().ToLowerInvariant()} " +
			$"layer={profile.LayerIndex} radial_bin={profile.RadialBin} " +
			$"theta_bins={profile.ThetaBinsCsv} period={profile.Period}");
	}

	private static string FormatFloat(float value)
	{
		return value.ToString("0.####", CultureInfo.InvariantCulture);
	}

	private static void PaintHeatmapCell(
		Image image,
		int theta,
		int layer,
		int radial,
		int panelIndex,
		int radialBins,
		int cellSize,
		Color color)
	{
		int startX = theta * cellSize;
		int startY = ((layer * 4 + panelIndex) * radialBins + radial) * cellSize;
		for (int y = 0; y < cellSize; y++)
		{
			for (int x = 0; x < cellSize; x++)
			{
				bool border = x == 0 || y == 0;
				image.SetPixel(startX + x, startY + y, border ? color.Darkened(0.35f) : color);
			}
		}
	}

	private static Color ComputePopulationColor(int samples, int maxSamples)
	{
		if (samples <= 0 || maxSamples <= 0)
		{
			return new Color(0.08f, 0.08f, 0.1f, 1f);
		}
		float normalized = Mathf.Clamp((float)System.Math.Log(samples + 1) / (float)System.Math.Log(maxSamples + 1), 0f, 1f);
		return new Color(0.1f + 0.8f * normalized, 0.12f + 0.55f * normalized, 0.08f + 0.2f * normalized, 1f);
	}

	private static Color ComputeCandidateColor(PortalSectorArtifactCell cell)
	{
		if (cell == null || cell.Samples <= 0 || !cell.HasCandidateState)
		{
			return new Color(0.08f, 0.08f, 0.1f, 1f);
		}
		return cell.CandidateInvariant
			? new Color(0.16f, 0.78f, 0.32f, 1f)
			: new Color(0.82f, 0.24f, 0.18f, 1f);
	}

	private static Color ComputePositiveOverlapColor(PortalSectorArtifactCell cell)
	{
		if (cell == null || !cell.HasPositiveOverlap)
		{
			return new Color(0.08f, 0.08f, 0.1f, 1f);
		}
		return cell.PositiveOverlapInvariant
			? new Color(0.15f, 0.65f, 0.88f, 1f)
			: new Color(0.85f, 0.45f, 0.18f, 1f);
	}

	private static Color ComputeUsefulnessQueryColor(int querySamples, int maxQuerySamples)
	{
		if (querySamples <= 0 || maxQuerySamples <= 0)
			return new Color(0.08f, 0.08f, 0.1f, 1f);
		float normalized = Mathf.Clamp((float)Math.Log(querySamples + 1) / (float)Math.Log(maxQuerySamples + 1), 0f, 1f);
		return new Color(0.1f, 0.12f + 0.65f * normalized, 0.16f + 0.72f * normalized, 1f);
	}

	private static Color ComputeUsefulnessYieldColor(float queryHitYield, float maxYield)
	{
		if (queryHitYield <= 0f || maxYield <= 1e-5f)
			return new Color(0.08f, 0.08f, 0.1f, 1f);
		float normalized = Mathf.Clamp(queryHitYield / maxYield, 0f, 1f);
		return new Color(0.18f + 0.72f * normalized, 0.12f + 0.70f * normalized, 0.10f, 1f);
	}

	private static Color ComputeUsefulnessContributionColor(PortalUsefulnessMetric metric)
	{
		if (metric == null || metric.QuerySamples <= 0)
			return new Color(0.08f, 0.08f, 0.1f, 1f);
		if (metric.InvariantRing && metric.HitSamples > 0)
			return new Color(0.24f, 0.92f, 0.34f, 1f);
		if (metric.HitSamples > 0)
			return new Color(0.92f, 0.76f, 0.20f, 1f);
		return new Color(0.78f, 0.18f, 0.16f, 1f);
	}

	private static Color ComputeHitColor(PortalSectorArtifactCell cell)
	{
		if (cell == null || cell.HitSamples <= 0)
		{
			return new Color(0.08f, 0.08f, 0.1f, 1f);
		}

		return DetermineDominantHitKind(cell) switch
		{
			"background" => new Color(0.20f, 0.52f, 0.96f, 1f),
			"source" => new Color(0.94f, 0.80f, 0.20f, 1f),
			"absorbed" => new Color(0.55f, 0.22f, 0.65f, 1f),
			"miss" => new Color(0.85f, 0.20f, 0.20f, 1f),
			"unclassified" => new Color(0.55f, 0.55f, 0.60f, 1f),
			_ => new Color(0.92f, 0.22f, 0.72f, 1f),
		};
	}

	private static string DetermineDominantHitKind(PortalSectorArtifactCell cell)
	{
		if (cell == null || cell.HitSamples <= 0)
		{
			return "none";
		}

		int best = cell.BackgroundHits;
		string kind = "background";
		if (cell.SourceHits > best) { best = cell.SourceHits; kind = "source"; }
		if (cell.UnclassifiedHits > best) { best = cell.UnclassifiedHits; kind = "unclassified"; }
		if (cell.AbsorbedHits > best) { best = cell.AbsorbedHits; kind = "absorbed"; }
		if (cell.MissHits > best) { best = cell.MissHits; kind = "miss"; }

		int nonZeroKinds = 0;
		if (cell.BackgroundHits > 0) nonZeroKinds++;
		if (cell.SourceHits > 0) nonZeroKinds++;
		if (cell.UnclassifiedHits > 0) nonZeroKinds++;
		if (cell.AbsorbedHits > 0) nonZeroKinds++;
		if (cell.MissHits > 0) nonZeroKinds++;
		return nonZeroKinds > 1 ? "mixed" : kind;
	}

	private void LogFilmDiagnostics()
	{
		if (_filmCamera == null || !GodotObject.IsInstanceValid(_filmCamera))
		{
			GD.Print("[WormholeValidation] film_diag missing_film_camera");
			return;
		}

		if (_filmCamera.TryGetFilmCaptureDiagnosticsForTesting(out GrinFilmCamera.FilmCaptureDiagnosticsSnapshot filmSnapshot))
		{
			GD.Print(
				$"[WormholeValidation] film_diag size={filmSnapshot.FilmWidth}x{filmSnapshot.FilmHeight} " +
				$"row_cursor={filmSnapshot.RowCursor} frame_rays={filmSnapshot.FrameRaysTraced} " +
				$"seg_integrated={filmSnapshot.FrameSegmentsIntegrated} seg_tested={filmSnapshot.FrameSegmentsTested} " +
				$"physics_queries={filmSnapshot.FramePhysicsQueries}");
		}

		if (_filmCamera.TryGetFixtureDebugStatsForTesting(out GrinFilmCamera.FixtureDebugStatsSnapshot fixtureStats))
		{
			GD.Print(
				$"[WormholeValidation] fixture_hits traced={fixtureStats.TracedPixels} source={fixtureStats.SourceHits} " +
				$"background={fixtureStats.BackgroundHits} absorbed={fixtureStats.AbsorbedHits} miss={fixtureStats.MissHits}");
		}
		else
		{
			GD.Print("[WormholeValidation] fixture_hits traced=0 source=0 background=0 absorbed=0 miss=0");
		}

		if (_filmCamera.TryGetFixtureWriteDiagnosticsForTesting(out GrinFilmCamera.FixtureWriteDiagnosticsSnapshot writeSnapshot))
		{
			GD.Print(
				$"[WormholeValidation] fixture_writes rows_started={writeSnapshot.RowsStarted} " +
				$"rows_completed={writeSnapshot.RowsCompleted} rows_partial={writeSnapshot.RowsPartiallyWritten} " +
				$"rows_early_term={writeSnapshot.RowsEarlyTerminated} final_hit_px={writeSnapshot.FinalHitPixelCount} " +
				$"traversal_px={writeSnapshot.TraversalWritePixelCount}");
		}
		else
		{
			GD.Print("[WormholeValidation] fixture_writes rows_started=0 rows_completed=0 rows_partial=0 rows_early_term=0 final_hit_px=0 traversal_px=0");
		}

		if (_filmCamera.TryGetWormholePostRemapDiagnosticsForTesting(out GrinFilmCamera.WormholePostRemapDiagnosticsSnapshot wormholeSnapshot))
		{
			GD.Print(
				$"[WormholeValidation] post_remap_funnel pixels={wormholeSnapshot.PixelsWithPostRemapSegments} " +
				$"multi_remap_pixels={wormholeSnapshot.PixelsWithMultiRemap} remap_segments={wormholeSnapshot.PostRemapSegments} " +
				$"candidate_segments={wormholeSnapshot.PostRemapCandidateSegments} candidate_gate_pass={wormholeSnapshot.PostRemapCandidateGatePassedSegments} " +
				$"insight_reject={wormholeSnapshot.PostRemapInsightRejectedSegments} skip_broadphase={wormholeSnapshot.PostRemapSkipBroadphaseSegments} " +
				$"quickray_softgate_reject={wormholeSnapshot.PostRemapQuickRaySoftGateRejectedSegments} " +
				$"stride_reject={wormholeSnapshot.PostRemapStrideRejectedSegments} budget_reject={wormholeSnapshot.PostRemapBudgetRejectedSegments} " +
				$"query_eligible={wormholeSnapshot.PostRemapQueryEligibleSegments} queries={wormholeSnapshot.PostRemapQueries} " +
				$"geom_hits={wormholeSnapshot.PostRemapGeometryHits} final_hit_pixels={wormholeSnapshot.PostRemapFinalHitPixels} " +
				$"source={wormholeSnapshot.PostRemapSourceHits} background={wormholeSnapshot.PostRemapBackgroundHits} " +
				$"unclassified={wormholeSnapshot.PostRemapUnclassifiedHits} absorbed={wormholeSnapshot.PostRemapAbsorbedHits} " +
				$"miss_pixels={wormholeSnapshot.PostRemapMissPixels} final_write_px={wormholeSnapshot.PostRemapFinalWritePixels} " +
				$"max_remaps_seen={wormholeSnapshot.MaxBoundaryRemapCountSeen}");
		}
		else
		{
			GD.Print("[WormholeValidation] post_remap_funnel pixels=0 multi_remap_pixels=0 remap_segments=0 candidate_segments=0 candidate_gate_pass=0 insight_reject=0 skip_broadphase=0 quickray_softgate_reject=0 stride_reject=0 budget_reject=0 query_eligible=0 queries=0 geom_hits=0 final_hit_pixels=0 source=0 background=0 unclassified=0 absorbed=0 miss_pixels=0 final_write_px=0 max_remaps_seen=0");
		}

		if (_filmCamera.TryGetWormholePortalSectorDiagnosticsForTesting(out GrinFilmCamera.WormholePortalSectorDiagnosticsSnapshot sectorSnapshot))
		{
			GD.Print(
				$"[WormholeValidation] portal_sector sectors={sectorSnapshot.SectorCount} " +
				$"cand_samples={sectorSnapshot.CandidateSamples} cand_invariant={sectorSnapshot.CandidateInvariantSectors} " +
				$"cand_variant={sectorSnapshot.CandidateVariantSectors} hit_samples={sectorSnapshot.HitSamples} " +
				$"hit_invariant={sectorSnapshot.HitInvariantSectors} hit_variant={sectorSnapshot.HitVariantSectors} " +
				$"positive_overlap_invariant={sectorSnapshot.PositiveOverlapInvariantSectors} " +
				$"rep_eligible={sectorSnapshot.RepresentativeEligibleSectors} rep_applied={sectorSnapshot.RepresentativeQueriesApplied} " +
				$"rep_saved={sectorSnapshot.RepresentativeQueriesSaved} max_sector_samples={sectorSnapshot.MaxSamplesPerSector}");
		}
		else
		{
			GD.Print("[WormholeValidation] portal_sector sectors=0 cand_samples=0 cand_invariant=0 cand_variant=0 hit_samples=0 hit_invariant=0 hit_variant=0 positive_overlap_invariant=0 rep_eligible=0 rep_applied=0 rep_saved=0 max_sector_samples=0");
		}

		if (_filmCamera.TryGetLatestRenderHealthDiagnosticsForTesting(out GrinFilmCamera.RenderHealthDiagnosticsSnapshot renderHealth))
		{
			GD.Print(
				$"[WormholeValidation] render_health step={renderHealth.StepIndex} row_cursor={renderHealth.RowCursorAfter} " +
				$"rows_advanced={renderHealth.RowsAdvanced} traced_px={renderHealth.TracedPixels} " +
				$"geom_seg={renderHealth.GeomSegmentsQueried} geom_ray_tests={renderHealth.GeomRayTestsTotal} " +
				$"p2_samp={renderHealth.Pass2SampledSegments} budget_exit={renderHealth.BudgetExitReason}");
		}
	}
}
