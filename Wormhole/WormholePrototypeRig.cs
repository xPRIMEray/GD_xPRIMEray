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
	[Export] public bool EmitBoundaryValidationSummaryAfterCapture = true;
	[Export] public string BoundaryValidationLabel = "wormhole_validation";
	[Export] public bool ValidationWaitForFilmWrap = true;
	[Export] public bool LockTravelerInputDuringValidation = true;

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
		ApplyValidationInputLock();
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
			if (CaptureValidationCompositeScreenshot)
			{
				CaptureCompositeScreenshot();
			}
		}

		SavePortalSectorArtifacts();

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

	private void CaptureCompositeScreenshot()
	{
		Viewport viewport = GetViewport();
		Image image = viewport?.GetTexture()?.GetImage();
		if (image == null)
		{
			GD.PushWarning("[WormholeValidation] failed to save composite capture reason=missing_viewport_image");
			return;
		}

		string projectPath = string.IsNullOrWhiteSpace(ValidationCompositeCapturePath)
			? "res://output/wormhole_test/wormhole_validation_composed.png"
			: ValidationCompositeCapturePath.Trim();
		string absolutePath = ProjectSettings.GlobalizePath(projectPath);
		string directory = Path.GetDirectoryName(absolutePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			DirAccess.MakeDirRecursiveAbsolute(directory);
		}

		Error saveError = image.SavePng(absolutePath);
		if (saveError == Error.Ok)
		{
			GD.Print($"[WormholeValidation] capture_saved path={absolutePath} source=viewport_composite");
		}
		else
		{
			GD.PushWarning($"[WormholeValidation] failed to save composite capture path={absolutePath} error={saveError}");
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
