using Godot;
using System.IO;

/// <summary>
/// Coordinates the first linked-mouth prototype:
/// - keeps only one world visible to the player camera
/// - drives both linked portal cameras
/// - teleports the traveler when crossing the active spherical shell
/// </summary>
public partial class WormholePrototypeRig : Node3D
{
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
	[Export] public bool EmitBoundaryValidationSummaryAfterCapture = true;
	[Export] public string BoundaryValidationLabel = "wormhole_validation";
	[Export] public bool ValidationWaitForFilmWrap = true;

	private Node3D _traveler;
	private Camera3D _mainCamera;
	private WormholePortal _portalA;
	private WormholePortal _portalB;
	private RayBeamRenderer _rayBeamRenderer;
	private GrinFilmCamera _filmCamera;
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

	public override void _Ready()
	{
		_traveler = GetNodeOrNull<Node3D>(TravelerPath);
		_mainCamera = GetNodeOrNull<Camera3D>(MainCameraPath);
		_portalA = GetNodeOrNull<WormholePortal>(PortalAPath);
		_portalB = GetNodeOrNull<WormholePortal>(PortalBPath);
		_rayBeamRenderer = GetNodeOrNull<RayBeamRenderer>(RayBeamRendererPath);
		_filmCamera = GetNodeOrNull<GrinFilmCamera>(FilmCameraPath);

		if (_traveler == null || _mainCamera == null || _portalA == null || _portalB == null)
		{
			GD.PushError($"{Name}: wormhole prototype rig is missing traveler, camera, or portal references.");
			SetProcess(false);
			SetPhysicsProcess(false);
			return;
		}

		SetProcess(true);
		SetPhysicsProcess(true);

		_activePortal = StartInSceneA ? _portalA : _portalB;
		ApplyWorldVisibility(_activePortal);
		_lastActivePortalDelta = _activePortal.SignedRadiusDelta(_traveler.GlobalPosition);
		_rayBeamRenderer?.BeginBoundaryValidationRun();
		_validationStartTicksMsec = Time.GetTicksMsec();
		_validationPendingCapture = true;
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
		WormholePortal destinationPortal = sourcePortal.GetLinkedPortal();
		if (destinationPortal == null)
		{
			return;
		}

		_traveler.GlobalTransform = sourcePortal.BuildExitTransform(_traveler.GlobalTransform);
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

	private void ExecuteValidationCapture()
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
		}
		else
		{
			GD.PushWarning($"[WormholeValidation] failed to save capture path={absolutePath} error={saveError}");
		}
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
