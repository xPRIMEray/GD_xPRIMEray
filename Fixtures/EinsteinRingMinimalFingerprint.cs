using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Godot;
using RendererCore.Config;
using RendererCore.Fields;

public partial class EinsteinRingMinimalFingerprint : Node3D
{
	private static readonly NodePath FieldPath = new("FixtureEinsteinRingMinimal/FieldSource3D");
	private static readonly NodePath PhotonBandFieldPath = new("FixtureEinsteinRingMinimal/PhotonBandSource");
	private static readonly NodePath CameraPath = new("FixtureEinsteinRingMinimal/Camera3D");
	private static readonly NodePath SpherePath = new("FixtureEinsteinRingMinimal/blackhole_center_marker");
	private static readonly NodePath RendererPath = new("FixtureEinsteinRingMinimal/RayBeamRenderer");
	private static readonly NodePath SourceTemplatePath = new("FixtureEinsteinRingMinimal/source_marker_template");
	private static readonly NodePath DetectorPath = new("FixtureEinsteinRingMinimal/background_screen");
	private static readonly NodePath FilmCameraPath = new("GrinFilmCamera");
	[Export] public SourcePatternMode PatternMode = SourcePatternMode.CrossXY;
	[Export(PropertyHint.Range, "1,101,1")] public int SourceCountX = 25;
	[Export(PropertyHint.Range, "1,101,1")] public int SourceCountY = 25;
	[Export(PropertyHint.Range, "0,20,0.01")] public float SourceSpacingX = 6.00f;
	[Export(PropertyHint.Range, "0,20,0.01")] public float SourceSpacingY = 6.00f;
	[Export(PropertyHint.Range, "0.01,6,0.01")] public float SourceMarkerRadius = 1.00f;
	[Export] public Vector3 PatternOriginLocal = new(0f, 0f, -12f);
	[Export] public bool IncludeCenterPoint = true;
	[Export] public float OffAxisSourceOffsetX = 0.2f;
	[Export] public float OffAxisSourceOffsetX2 = 0.35f;
	[Export] public bool EnableOffAxisSourceCase = true;
	[Export] public bool EnableSecondOffAxisSourceCase = true;
	[Export] public bool EnableRingRadialHistogram = true;
	[Export(PropertyHint.Range, "2,32,1")] public int RingRadialHistogramBins = 8;
	[Export(PropertyHint.Range, "2,32,1")] public int RingProbeColumns = 8;
	[Export(PropertyHint.Range, "2,32,1")] public int RingProbeRows = 8;
	[Export] public bool EnableRingProbePatternRotation = true;
	[Export(PropertyHint.Range, "0,30,0.1")] public float RingProbePatternRotationDeg = 7.5f;
	[Export] public bool EnableSourceRadiusSweep = true;

	private static readonly Vector3[] CurvatureProbeOffsets =
	{
		new Vector3(0.75f, 0.00f, -0.50f),
		new Vector3(-1.20f, 0.30f, 0.40f),
		new Vector3(0.20f, -0.80f, 1.10f),
		new Vector3(1.00f, 0.60f, 0.20f)
	};

	private static readonly Vector2[] RayProbeNdc =
	{
		new Vector2(-0.30f, -0.30f), new Vector2(-0.10f, -0.30f), new Vector2(0.10f, -0.30f), new Vector2(0.30f, -0.30f),
		new Vector2(-0.30f, -0.10f), new Vector2(-0.10f, -0.10f), new Vector2(0.10f, -0.10f), new Vector2(0.30f, -0.10f),
		new Vector2(-0.30f, 0.10f), new Vector2(-0.10f, 0.10f), new Vector2(0.10f, 0.10f), new Vector2(0.30f, 0.10f),
		new Vector2(-0.30f, 0.30f), new Vector2(-0.10f, 0.30f), new Vector2(0.10f, 0.30f), new Vector2(0.30f, 0.30f)
	};

	private static readonly Vector3[] FingerprintAccelOffsets =
	{
		new Vector3(0.20f, 0.00f, 0.00f),
		new Vector3(1.20f, 0.30f, -0.40f),
		new Vector3(-1.10f, 0.70f, 0.10f),
		new Vector3(0.50f, -1.30f, 0.60f),
		new Vector3(1.60f, 0.20f, 1.10f)
	};

	private static readonly Vector2[] FingerprintRayNdc =
	{
		new Vector2(-0.35f, -0.35f),
		new Vector2(-0.10f, -0.35f),
		new Vector2(0.15f, -0.35f),
		new Vector2(0.35f, -0.10f),
		new Vector2(0.35f, 0.15f),
		new Vector2(0.10f, 0.35f),
		new Vector2(-0.15f, 0.35f),
		new Vector2(-0.35f, 0.10f)
	};

	private const bool FixedEnabled = true;
	private const FieldCurveType FixedCurveType = FieldCurveType.Power;
	private const float FixedAmp = 0.151f;
	private const bool FixedEnableInnerRadius = true;
	private const float FixedRInner = 0.12f;
	private const float FixedROuter = 4.5f;
	private const float FixedGamma = 8.0f;
	private const bool FixedOverrideBetaScale = true;
	private const float FixedBetaScale = 1.0f;
	private const uint FixedModeFlags = FieldMath.ModeFlagAbsorbInsideInnerRadius;
	private const float FixedSoftening = 0.1f;
	private const float FixedEdgeSoftness = 0.0f;
	private const bool FixedDebugDrawBounds = false;
	private const bool FixedDebugDrawInGame = false;
	private const TransportModel FixedTransportModel = TransportModel.GRIN_Optical;

	private const bool FixedPhotonBandEnabled = true;
	private const FieldCurveType FixedPhotonBandCurveType = FieldCurveType.Polynomial;
	private const float FixedPhotonBandAmp = 0.03f;
	private const float FixedPhotonBandCenterRadiusOuterFraction = 0.35f;
	private const float FixedPhotonBandWidthOuterFraction = 0.12f;
	private const float FixedPhotonBandMinWidth = 0.20f;
	private const float FixedPhotonBandCurveA = 0.0f;
	private const float FixedPhotonBandCurveB = 4.0f;
	private const float FixedPhotonBandCurveC = -4.0f;
	private const bool FixedPhotonBandOverrideBetaScale = true;
	private const float FixedPhotonBandBetaScale = 1.0f;
	private const uint FixedPhotonBandModeFlags = 0u;
	private const TransportModel FixedPhotonBandTransportModel = TransportModel.GRIN_Optical;
	private const float FixedPhotonBandSoftening = 0.05f;
	private const float FixedPhotonBandEdgeSoftness = 0.0f;

	private const float CurvatureAccelEpsilon = 1e-6f;
	private const float FarAccelEpsilon = 1e-6f;
	private const float CurvatureDeviationMin = 1e-3f;
	private const float CurvatureDeviationMax = 12.0f;
	private const int CurvatureRayStepCap = 200;
	private const int CurvatureRayMinDeviations = 6;
	private const int FingerprintRaySteps = 48;
	private const int FingerprintDecimals = 6;
	private const float PhotonBandNoticeableAccelMin = 0.002f;
	private const float ProbeAccelChaosBound = 8.0f;
	private const float AccelClampMax = 50.0f;
	private const float AbsorptionRateMin = 0.10f;
	private const float AbsorptionRateMax = 0.40f;
	private const int RingProbeStepCap = 500;
	private const int RingMinSourceHits = 4;
	private const float RingRadiusStdDevMax = 8.0f;
	private const float RingRadiusStdDevMin = 0.05f;
	private const float RingRadiusMeanMin = 1.0f;
	private const float RingRadiusRangeMin = 0.20f;
	private const float RingRadiusCollapseRatioMin = 0.85f;

	private bool _invalidFixture;
	private bool _loggedBroadphaseLock;
	private bool _loggedFixtureDebugColoringLock;
	private bool _loggedRingProbePattern;
	private Node3D _sourcePatternRoot;
	private SourceMarkerSphere[] _sourceMarkers = Array.Empty<SourceMarkerSphere>();
	private Vector2[] _ringProbeNdc = Array.Empty<Vector2>();
	private const float ManualMarkerMeshScale = 1.18f;

	private readonly struct PhotonBandShell
	{
		public readonly float Center;
		public readonly float Width;
		public readonly float RInner;
		public readonly float ROuter;

		public PhotonBandShell(float center, float width, float rInner, float rOuter)
		{
			Center = center;
			Width = width;
			RInner = rInner;
			ROuter = rOuter;
		}
	}

	private enum RingProbeOutcome
	{
		Miss = 0,
		Absorbed = 1,
		Background = 2,
		Source = 3
	}

	private readonly struct DetectorPlaneData
	{
		public readonly Transform3D Transform;
		public readonly Vector2 HalfExtents;

		public DetectorPlaneData(Transform3D transform, Vector2 halfExtents)
		{
			Transform = transform;
			HalfExtents = halfExtents;
		}
	}

	private readonly struct RingProbeSummary
	{
		public readonly int SourceHits;
		public readonly int BackgroundHits;
		public readonly int AbsorbedHits;
		public readonly int MissHits;
		public readonly int RadiiCount;
		public readonly float RadiusMean;
		public readonly float RadiusStdDev;
		public readonly float RadiusMin;
		public readonly float RadiusMax;
		public readonly float RadiusRange;
		public readonly string RadiusHistogram;
		public readonly string OutcomeVector;
		public readonly string RadiusVector;
		public readonly string Checksum;

		public RingProbeSummary(
			int sourceHits,
			int backgroundHits,
			int absorbedHits,
			int missHits,
			int radiiCount,
			float radiusMean,
			float radiusStdDev,
			float radiusMin,
			float radiusMax,
			float radiusRange,
			string radiusHistogram,
			string outcomeVector,
			string radiusVector,
			string checksum)
		{
			SourceHits = sourceHits;
			BackgroundHits = backgroundHits;
			AbsorbedHits = absorbedHits;
			MissHits = missHits;
			RadiiCount = radiiCount;
			RadiusMean = radiusMean;
			RadiusStdDev = radiusStdDev;
			RadiusMin = radiusMin;
			RadiusMax = radiusMax;
			RadiusRange = radiusRange;
			RadiusHistogram = radiusHistogram ?? string.Empty;
			OutcomeVector = outcomeVector ?? string.Empty;
			RadiusVector = radiusVector ?? string.Empty;
			Checksum = checksum ?? string.Empty;
		}
	}

	public override void _Ready()
	{
		FieldSource3D massField = GetNodeOrNull<FieldSource3D>(FieldPath);
		if (massField == null)
		{
			FailInvalid("missing FieldSource3D", $"field_path={FieldPath}");
			return;
		}

		FieldSource3D photonBandField = GetNodeOrNull<FieldSource3D>(PhotonBandFieldPath);
		if (photonBandField == null)
		{
			FailInvalid("missing PhotonBandSource", $"field_path={PhotonBandFieldPath}");
			return;
		}

		ApplyCanonicalFixtureParams(massField);
		PhotonBandShell photonBandShell = ComputePhotonBandShell(FixedROuter, FixedRInner);
		ApplyPhotonBandFixtureParams(photonBandField, photonBandShell);

		FieldSource3D.ResolvedFieldParams massResolved = massField.ResolveEffectiveParams(out string massResolveReason);
		FieldSource3D.ResolvedFieldParams bandResolved = photonBandField.ResolveEffectiveParams(out string bandResolveReason);
		RayBeamRenderer.FieldSourceSnap massSnap = RayBeamRenderer.BuildFieldSourceSnap(massField);
		RayBeamRenderer.FieldSourceSnap bandSnap = RayBeamRenderer.BuildFieldSourceSnap(photonBandField);
		RayBeamRenderer.FieldSourceSnap[] snaps = BuildSourceArray(massSnap, bandSnap);

		string massResolvedSummary = BuildResolvedSummary("mass", massResolveReason, massResolved);
		string bandResolvedSummary = BuildResolvedSummary("band", bandResolveReason, bandResolved);
		bool massCanonical = IsCanonicalResolve(massResolveReason);
		bool bandCanonical = !FixedPhotonBandEnabled || IsCanonicalResolve(bandResolveReason);
		if (!massCanonical || !bandCanonical)
		{
			FailInvalid(
				"curvature not engaged",
				$"mass_canonical={(massCanonical ? 1 : 0)} band_canonical={(bandCanonical ? 1 : 0)} {massResolvedSummary} {bandResolvedSummary}");
			return;
		}

		Camera3D camera = GetNodeOrNull<Camera3D>(CameraPath);
		RayBeamRenderer rayRenderer = GetNodeOrNull<RayBeamRenderer>(RendererPath);
		GrinFilmCamera filmCamera = GetNodeOrNull<GrinFilmCamera>(FilmCameraPath);
		StaticBody3D sourceTemplate = GetNodeOrNull<StaticBody3D>(SourceTemplatePath);
		StaticBody3D detectorPlane = GetNodeOrNull<StaticBody3D>(DetectorPath);
		Node3D sphere = GetNodeOrNull<Node3D>(SpherePath);
		if (camera == null || rayRenderer == null || sourceTemplate == null || detectorPlane == null)
		{
			FailInvalid(
				"missing fixture nodes",
				$"camera={(camera != null ? 1 : 0)} renderer={(rayRenderer != null ? 1 : 0)} source_template={(sourceTemplate != null ? 1 : 0)} detector={(detectorPlane != null ? 1 : 0)}");
			return;
		}
		EnforceFixtureBroadphasePolicy(filmCamera);
		EnforceFixtureDebugHitColoring(filmCamera);
		if (!RebuildSourcePatternMarkers())
		{
			return;
		}
		LogSourcePatternSummary();
		LogSourcePatternMarkerDetails();

		float[] massProbeAccels = new float[CurvatureProbeOffsets.Length];
		float[] bandProbeAccels = new float[CurvatureProbeOffsets.Length];
		float[] totalProbeAccels = new float[CurvatureProbeOffsets.Length];
		float massProbeSum = 0f;
		float bandProbeSum = 0f;
		float totalProbeSum = 0f;
		float maxProbeAccel = 0f;
		bool anyProbeAboveEps = false;
		bool bandNoticeable = !FixedPhotonBandEnabled;
		for (int i = 0; i < CurvatureProbeOffsets.Length; i++)
		{
			Vector3 probe = massField.GlobalPosition + CurvatureProbeOffsets[i];
			FieldMath.EvalResult massEval = EvaluateSnapAccel(probe, massSnap);
			FieldMath.EvalResult bandEval = EvaluateSnapAccel(probe, bandSnap);
			Vector3 totalAccel = massEval.Acceleration + bandEval.Acceleration;
			float massMag = massEval.AccelerationMagnitude;
			float bandMag = bandEval.AccelerationMagnitude;
			float totalMag = totalAccel.Length();
			massProbeAccels[i] = massMag;
			bandProbeAccels[i] = bandMag;
			totalProbeAccels[i] = totalMag;
			massProbeSum += massMag;
			bandProbeSum += bandMag;
			totalProbeSum += totalMag;
			if (totalMag > maxProbeAccel)
			{
				maxProbeAccel = totalMag;
			}
			if (totalMag > CurvatureAccelEpsilon)
			{
				anyProbeAboveEps = true;
			}
			if (bandMag > PhotonBandNoticeableAccelMin)
			{
				bandNoticeable = true;
			}
		}

		float maxOuter = Mathf.Max(massSnap.ROuter, bandSnap.ROuter);
		float farDistance = Mathf.Max(maxOuter + 16f, 20f);
		Vector3 farProbe = massField.GlobalPosition + new Vector3(0f, 0f, farDistance);
		float farAccelMag = ComputeCombinedRawAcceleration(farProbe, snaps).Length();

		bool hasRenderer = rayRenderer != null;
		bool useIntegrated = hasRenderer && rayRenderer.UseIntegratedField;
		float bendScale = hasRenderer ? rayRenderer.BendScale : 0f;
		float fieldStrength = hasRenderer ? rayRenderer.FieldStrength : 0f;
		float transportStrength = Mathf.Abs(bendScale * fieldStrength);
		string rendererSummary = BuildRendererSummary(hasRenderer, useIntegrated, bendScale, fieldStrength, transportStrength);

		bool canonicalMatchesMass =
			massResolved.curveType == FixedCurveType &&
			Mathf.IsEqualApprox(massResolved.rInner, FixedRInner) &&
			Mathf.IsEqualApprox(massResolved.rOuter, FixedROuter) &&
			Mathf.IsEqualApprox(massResolved.amp, FixedAmp) &&
			Mathf.IsEqualApprox(massResolved.a, FixedGamma) &&
			massResolved.overrideBetaScale == FixedOverrideBetaScale &&
			Mathf.IsEqualApprox(massResolved.betaScale, FixedBetaScale) &&
			massResolved.modeFlags == FixedModeFlags;

		bool canonicalMatchesBand = !FixedPhotonBandEnabled || (
			bandResolved.enabled &&
			bandResolved.curveType == FixedPhotonBandCurveType &&
			Mathf.IsEqualApprox(bandResolved.rInner, photonBandShell.RInner) &&
			Mathf.IsEqualApprox(bandResolved.rOuter, photonBandShell.ROuter) &&
			Mathf.IsEqualApprox(bandResolved.amp, FixedPhotonBandAmp) &&
			Mathf.IsEqualApprox(bandResolved.a, FixedPhotonBandCurveA) &&
			Mathf.IsEqualApprox(bandResolved.b, FixedPhotonBandCurveB) &&
			Mathf.IsEqualApprox(bandResolved.c, FixedPhotonBandCurveC) &&
			bandResolved.overrideBetaScale == FixedPhotonBandOverrideBetaScale &&
			Mathf.IsEqualApprox(bandResolved.betaScale, FixedPhotonBandBetaScale) &&
			bandResolved.modeFlags == FixedPhotonBandModeFlags);

		bool massPrimary = !FixedPhotonBandEnabled || (
			massResolved.amp > bandResolved.amp &&
			massProbeSum > bandProbeSum);
		bool bounded = maxProbeAccel <= ProbeAccelChaosBound;

		if (!canonicalMatchesMass || !canonicalMatchesBand || !massPrimary || !bandNoticeable ||
			!bounded || !anyProbeAboveEps || farAccelMag > FarAccelEpsilon || !useIntegrated || transportStrength <= CurvatureAccelEpsilon)
		{
			FailInvalid(
				"curvature not engaged",
				$"mass_match={(canonicalMatchesMass ? 1 : 0)} band_match={(canonicalMatchesBand ? 1 : 0)} mass_primary={(massPrimary ? 1 : 0)} " +
				$"band_noticeable={(bandNoticeable ? 1 : 0)} bounded={(bounded ? 1 : 0)} {rendererSummary} " +
				$"{massResolvedSummary} {bandResolvedSummary} " +
				$"probe_mass=[{FormatFloatArray(massProbeAccels)}] probe_band=[{FormatFloatArray(bandProbeAccels)}] probe_total=[{FormatFloatArray(totalProbeAccels)}] " +
				$"far_accel={farAccelMag:0.######}");
			return;
		}

		GD.Print(
			$"[EinsteinFixture][CurvatureEngaged] mass_match=1 band_match=1 probe_any=1 band_noticeable=1 mass_primary=1 " +
			$"far_accel={farAccelMag:0.######} transport={transportStrength:0.######} " +
			$"probe_mass=[{FormatFloatArray(massProbeAccels)}] probe_band=[{FormatFloatArray(bandProbeAccels)}] probe_total=[{FormatFloatArray(totalProbeAccels)}]");

		int deviatedRayCount = 0;
		float maxDeviation = 0f;
		float deviationSum = 0f;
		MeasureRayCurvature(
			snaps,
			massSnap.Center,
			massSnap.ROuter,
			rayRenderer,
			Mathf.Clamp(rayRenderer.StepsPerRay, 64, CurvatureRayStepCap),
			out deviatedRayCount,
			out maxDeviation,
			out deviationSum);

		if (deviatedRayCount < CurvatureRayMinDeviations || maxDeviation > CurvatureDeviationMax)
		{
			string reason = maxDeviation > CurvatureDeviationMax ? "curvature unstable" : "curvature not applied";
			FailInvalid(
				reason,
				$"deviated_rays={deviatedRayCount}/{RayProbeNdc.Length} min_required={CurvatureRayMinDeviations} " +
				$"d_min={CurvatureDeviationMin:0.######} d_max={CurvatureDeviationMax:0.######} max_dev={maxDeviation:0.######} dev_sum={deviationSum:0.######} " +
				$"{massResolvedSummary} {bandResolvedSummary} {rendererSummary} " +
				$"probe_mass=[{FormatFloatArray(massProbeAccels)}] probe_band=[{FormatFloatArray(bandProbeAccels)}] probe_total=[{FormatFloatArray(totalProbeAccels)}]");
			return;
		}

		GD.Print(
			$"[EinsteinFixture][CurvatureApplied] deviated_rays={deviatedRayCount}/{RayProbeNdc.Length} " +
			$"max_dev={maxDeviation:0.######} dev_sum={deviationSum:0.######} min_dev_threshold={CurvatureDeviationMin:0.######}");

		int absorbedRayCount = CountAbsorbedRayProbes(
			snaps,
			massSnap.Center,
			massSnap.ROuter,
			rayRenderer,
			Mathf.Clamp(rayRenderer.StepsPerRay, 64, CurvatureRayStepCap));
		float absorbedRate = absorbedRayCount / (float)RayProbeNdc.Length;
		GD.Print($"[EinsteinFixture][Absorption] absorbed_rays={absorbedRayCount}/{RayProbeNdc.Length} rate={absorbedRate:0.###}");
		if (absorbedRate < AbsorptionRateMin || absorbedRate > AbsorptionRateMax)
		{
			FailInvalid(
				"absorption out of range",
				$"absorbed_rays={absorbedRayCount}/{RayProbeNdc.Length} rate={absorbedRate:0.######} " +
				$"expected=[{AbsorptionRateMin:0.###},{AbsorptionRateMax:0.###}]");
			return;
		}

		DetectorPlaneData detectorData = BuildDetectorPlaneData(detectorPlane);
		_ringProbeNdc = BuildRingProbeNdc(
			Mathf.Clamp(RingProbeColumns, 2, 32),
			Mathf.Clamp(RingProbeRows, 2, 32),
			EnableRingProbePatternRotation ? RingProbePatternRotationDeg : 0f);
		if (!_loggedRingProbePattern)
		{
			_loggedRingProbePattern = true;
			GD.Print(
				$"[EinsteinFixture][RingProbePattern] probes={_ringProbeNdc.Length} cols={Mathf.Clamp(RingProbeColumns, 2, 32)} rows={Mathf.Clamp(RingProbeRows, 2, 32)} " +
				$"rotation_deg={(EnableRingProbePatternRotation ? RingProbePatternRotationDeg : 0f):0.###} deterministic=1");
		}

		float sourceRadius = ResolveSourceRadius(_sourceMarkers);
		int histogramBins = EnableRingRadialHistogram ? Mathf.Clamp(RingRadialHistogramBins, 2, 32) : 0;
		int ringSteps = Mathf.Clamp(rayRenderer.StepsPerRay, 128, RingProbeStepCap);
		RingProbeSummary baseOnAxisSummary = RunRingProbePass(
			_ringProbeNdc,
			rayRenderer,
			snaps,
			massSnap.Center,
			massSnap.ROuter,
			detectorData,
			_sourceMarkers,
			Vector3.Zero,
			sourceRadius,
			ringSteps,
			histogramBins);
		LogRingRadialSummary($"on_axis_r{sourceRadius:0.00}", Vector3.Zero, baseOnAxisSummary, histogramBins);
		RingProbeSummary onAxisRingSummary = baseOnAxisSummary;
		if (EnableSourceRadiusSweep)
		{
			float[] sourceRadiusCandidates = BuildSourceRadiusCandidates(sourceRadius);
			for (int i = 0; i < sourceRadiusCandidates.Length; i++)
			{
				float candidateRadius = sourceRadiusCandidates[i];
				if (Mathf.IsEqualApprox(candidateRadius, sourceRadius))
				{
					continue;
				}

				RingProbeSummary candidateSummary = RunRingProbePass(
					_ringProbeNdc,
					rayRenderer,
					snaps,
					massSnap.Center,
					massSnap.ROuter,
					detectorData,
					_sourceMarkers,
					Vector3.Zero,
					candidateRadius,
					ringSteps,
					histogramBins);
				LogRingRadialSummary($"on_axis_r{candidateRadius:0.00}", Vector3.Zero, candidateSummary, histogramBins);

				bool nonCollapsed = baseOnAxisSummary.RadiusMean <= 1e-6f ||
					candidateSummary.RadiusMean >= baseOnAxisSummary.RadiusMean * RingRadiusCollapseRatioMin;
				bool betterHits = candidateSummary.SourceHits > onAxisRingSummary.SourceHits;
				bool equalHitsWider = candidateSummary.SourceHits == onAxisRingSummary.SourceHits &&
					candidateSummary.RadiusMean > onAxisRingSummary.RadiusMean;
				if (nonCollapsed && (betterHits || equalHitsWider))
				{
					sourceRadius = candidateRadius;
					onAxisRingSummary = candidateSummary;
				}
			}
		}

		Vector3 offAxisOffset = EnableOffAxisSourceCase ? new Vector3(OffAxisSourceOffsetX, 0f, 0f) : Vector3.Zero;
		RingProbeSummary offAxisRingSummary = RunRingProbePass(
			_ringProbeNdc,
			rayRenderer,
			snaps,
			massSnap.Center,
			massSnap.ROuter,
			detectorData,
			_sourceMarkers,
			offAxisOffset,
			sourceRadius,
			ringSteps,
			histogramBins);
		Vector3 offAxisOffset2 = EnableSecondOffAxisSourceCase ? new Vector3(OffAxisSourceOffsetX2, 0f, 0f) : Vector3.Zero;
		RingProbeSummary offAxisRingSummary2 = RunRingProbePass(
			_ringProbeNdc,
			rayRenderer,
			snaps,
			massSnap.Center,
			massSnap.ROuter,
			detectorData,
			_sourceMarkers,
			offAxisOffset2,
			sourceRadius,
			ringSteps,
			histogramBins);

		int ringRayCount = _ringProbeNdc.Length;
		float sourceHitRate = onAxisRingSummary.SourceHits / (float)ringRayCount;
		float backgroundHitRate = onAxisRingSummary.BackgroundHits / (float)ringRayCount;
		float ringAbsorbedRate = onAxisRingSummary.AbsorbedHits / (float)ringRayCount;
		float missRate = onAxisRingSummary.MissHits / (float)ringRayCount;
		GD.Print(
			$"[EinsteinFixture][HitRate] rays={ringRayCount} source={onAxisRingSummary.SourceHits} background={onAxisRingSummary.BackgroundHits} " +
			$"absorbed={onAxisRingSummary.AbsorbedHits} miss={onAxisRingSummary.MissHits} source_rate={sourceHitRate:0.###} " +
			$"background_rate={backgroundHitRate:0.###} absorbed_rate={ringAbsorbedRate:0.###} miss_rate={missRate:0.###}");
		LogRingRadialSummary($"on_axis_selected_r{sourceRadius:0.00}", Vector3.Zero, onAxisRingSummary, histogramBins);
		if (EnableOffAxisSourceCase)
		{
			LogRingRadialSummary("off_axis", offAxisOffset, offAxisRingSummary, histogramBins);
		}
		if (EnableSecondOffAxisSourceCase)
		{
			LogRingRadialSummary("off_axis_2", offAxisOffset2, offAxisRingSummary2, histogramBins);
		}

		bool ringLike =
			onAxisRingSummary.SourceHits >= RingMinSourceHits &&
			onAxisRingSummary.RadiiCount >= RingMinSourceHits &&
			onAxisRingSummary.RadiusMean >= RingRadiusMeanMin &&
			onAxisRingSummary.RadiusStdDev >= RingRadiusStdDevMin &&
			onAxisRingSummary.RadiusStdDev <= RingRadiusStdDevMax &&
			onAxisRingSummary.RadiusRange >= RingRadiusRangeMin;
		if (!ringLike)
		{
			FailInvalid(
				"ring invariant failed",
				$"source_hits={onAxisRingSummary.SourceHits} min_source_hits={RingMinSourceHits} radii_count={onAxisRingSummary.RadiiCount} " +
				$"radius_mean={onAxisRingSummary.RadiusMean:0.######} min_mean={RingRadiusMeanMin:0.######} " +
				$"radius_stddev={onAxisRingSummary.RadiusStdDev:0.######} stddev_range=[{RingRadiusStdDevMin:0.######},{RingRadiusStdDevMax:0.######}] " +
				$"radius_range={onAxisRingSummary.RadiusRange:0.######} min_range={RingRadiusRangeMin:0.######} checksum={onAxisRingSummary.Checksum}");
			return;
		}

		float curvatureEnergy = totalProbeSum * transportStrength;
		GD.Print(
			$"[EinsteinFixture][CurvatureMetric] accel_sum_total={totalProbeSum:0.######} accel_sum_mass={massProbeSum:0.######} accel_sum_band={bandProbeSum:0.######} " +
			$"curvature_energy={curvatureEnergy:0.######} max_probe_accel={maxProbeAccel:0.######} far_accel={farAccelMag:0.######}");
		if (filmCamera != null && filmCamera.SmartScaleEnabled)
		{
			GD.Print(
				$"[EinsteinFixture][SmartScaleCurvature] smartscale=1 accel_sum_total={totalProbeSum:0.######} accel_sum_mass={massProbeSum:0.######} accel_sum_band={bandProbeSum:0.######} " +
				$"curvature_energy={curvatureEnergy:0.######} max_probe_accel={maxProbeAccel:0.######} far_accel={farAccelMag:0.######}");
		}

		massField.Strength = FixedAmp;
		massField.Softening = FixedSoftening;
		massField.OuterRadius = FixedROuter;
		massField.OverrideGamma = true;
		massField.Gamma = FixedGamma;
		massField.DebugDrawBounds = FixedDebugDrawBounds;
		massField.DebugDrawInGame = FixedDebugDrawInGame;

		photonBandField.Strength = FixedPhotonBandAmp;
		photonBandField.Softening = FixedPhotonBandSoftening;
		photonBandField.InnerRadius = photonBandShell.RInner;
		photonBandField.OuterRadius = photonBandShell.ROuter;
		photonBandField.OverrideGamma = false;
		photonBandField.Gamma = 1.0f;
		photonBandField.DebugDrawBounds = FixedDebugDrawBounds;
		photonBandField.DebugDrawInGame = FixedDebugDrawInGame;

		string sphereScale = sphere != null ? FormatVec3(sphere.Scale) : "n/a";
		string spherePos = sphere != null ? FormatVec3(sphere.GlobalTransform.Origin) : "n/a";
		string camFov = camera != null ? $"{camera.Fov:0.###}" : "n/a";
		string camPos = camera != null ? FormatVec3(camera.GlobalTransform.Origin) : "n/a";
		string camToSphere = (camera != null && sphere != null)
			? $"{camera.GlobalTransform.Origin.DistanceTo(sphere.GlobalTransform.Origin):0.###}"
			: "n/a";

		GD.Print(
			$"[EinsteinFixture] mass_strength={massResolved.amp:0.###} mass_radius={massResolved.rOuter:0.###} " +
			$"band_strength={bandResolved.amp:0.###} band_r_inner={bandResolved.rInner:0.###} band_r_outer={bandResolved.rOuter:0.###} " +
			$"mass_node_path={massField.GetPath()} band_node_path={photonBandField.GetPath()} sphere_global_pos={spherePos} sphere_scale={sphereScale} " +
			$"cam_fov={camFov} camera_global_pos={camPos} cam_to_sphere={camToSphere}");

		GD.Print(
			$"[EinsteinFixture][ResolvedMass] source={massResolveReason} curve={massResolved.curveType} modeFlags={massResolved.modeFlags} " +
			$"rInner={massResolved.rInner:0.###} rOuter={massResolved.rOuter:0.###} amp={massResolved.amp:0.###} " +
			$"gamma={massResolved.a:0.###} beta_mode={(massResolved.overrideBetaScale ? "override" : "global")} beta_scale={massResolved.betaScale:0.###}");

		GD.Print(
			$"[EinsteinFixture][ResolvedPhotonBand] source={bandResolveReason} enabled={(bandResolved.enabled ? 1 : 0)} curve={bandResolved.curveType} modeFlags={bandResolved.modeFlags} " +
			$"rInner={bandResolved.rInner:0.###} rOuter={bandResolved.rOuter:0.###} amp={bandResolved.amp:0.###} " +
			$"A={bandResolved.a:0.###} B={bandResolved.b:0.###} C={bandResolved.c:0.###} " +
			$"beta_mode={(bandResolved.overrideBetaScale ? "override" : "global")} beta_scale={bandResolved.betaScale:0.###}");

		GD.Print(
			$"[EinsteinFixture][Renderer] useIntegrated={(useIntegrated ? 1 : 0)} bendScale={bendScale:0.###} fieldStrength={fieldStrength:0.###}");

		string fingerprint = BuildEinsteinRingMinimalFingerprint(
			absorbedRayCount,
			absorbedRate,
			sourceRadius,
			onAxisRingSummary,
			offAxisRingSummary,
			offAxisOffset,
			offAxisRingSummary2,
			offAxisOffset2);
		GD.Print($"EinsteinRingMinimalFingerprint: {fingerprint}");
	}

	private readonly struct SourceMarkerSphere
	{
		public readonly Vector3 LocalCenter;
		public readonly Vector3 WorldCenter;
		public readonly float Radius;

		public SourceMarkerSphere(Vector3 localCenter, Vector3 worldCenter, float radius)
		{
			LocalCenter = localCenter;
			WorldCenter = worldCenter;
			Radius = radius;
		}
	}

	public override void _Process(double delta)
	{
		GrinFilmCamera filmCamera = GetNodeOrNull<GrinFilmCamera>(FilmCameraPath);
		EnforceFixtureBroadphasePolicy(filmCamera);
		EnforceFixtureDebugHitColoring(filmCamera);
	}

	private string BuildEinsteinRingMinimalFingerprint(
		int absorbedRayCount,
		float absorbedRate,
		float sourceRadius,
		RingProbeSummary onAxisRingSummary,
		RingProbeSummary offAxisRingSummary,
		Vector3 offAxisOffset,
		RingProbeSummary offAxisRingSummary2,
		Vector3 offAxisOffset2)
	{
		FieldSource3D massField = GetNodeOrNull<FieldSource3D>(FieldPath);
		FieldSource3D photonBandField = GetNodeOrNull<FieldSource3D>(PhotonBandFieldPath);
		RayBeamRenderer rayRenderer = GetNodeOrNull<RayBeamRenderer>(RendererPath);
		if (massField == null || photonBandField == null || rayRenderer == null)
		{
			return
				$"error=missing_nodes mass_field={(massField != null ? 1 : 0)} band_field={(photonBandField != null ? 1 : 0)} renderer={(rayRenderer != null ? 1 : 0)}";
		}

		ApplyCanonicalFixtureParams(massField);
		PhotonBandShell photonBandShell = ComputePhotonBandShell(FixedROuter, FixedRInner);
		ApplyPhotonBandFixtureParams(photonBandField, photonBandShell);

		FieldSource3D.ResolvedFieldParams massResolved = massField.ResolveEffectiveParams(out string massResolveReason);
		FieldSource3D.ResolvedFieldParams bandResolved = photonBandField.ResolveEffectiveParams(out string bandResolveReason);
		RayBeamRenderer.FieldSourceSnap massSnap = RayBeamRenderer.BuildFieldSourceSnap(massField);
		RayBeamRenderer.FieldSourceSnap bandSnap = RayBeamRenderer.BuildFieldSourceSnap(photonBandField);
		RayBeamRenderer.FieldSourceSnap[] snaps = BuildSourceArray(massSnap, bandSnap);

		float[] massAccelMags = BuildFingerprintAccelMagnitudes(massSnap.Center, massSnap);
		float[] bandAccelMags = BuildFingerprintAccelMagnitudes(massSnap.Center, bandSnap);
		float[] totalAccelMags = BuildFingerprintCombinedAccelMagnitudes(massSnap.Center, snaps);
		Vector3[] rayEndpoints = BuildFingerprintRayEndpoints(
			snaps,
			massSnap.Center,
			massSnap.ROuter,
			rayRenderer,
			FingerprintRaySteps);
		string massAccelVector = FormatRoundedFloatVector(massAccelMags);
		string bandAccelVector = FormatRoundedFloatVector(bandAccelMags);
		string totalAccelVector = FormatRoundedFloatVector(totalAccelMags);
		string rayEndpointVector = FormatRoundedVec3Vector(rayEndpoints);
		string rayEndpointChecksum = ComputeSha256Hex(rayEndpointVector);

		string fingerprintCore =
			$"v=4;" +
			$"sourceCount=2;" +
			$"massCanonical={(IsCanonicalResolve(massResolveReason) ? 1 : 0)};" +
			$"bandCanonical={(IsCanonicalResolve(bandResolveReason) ? 1 : 0)};" +
			$"massSource={massResolveReason};" +
			$"massCurve={massResolved.curveType};" +
			$"massRInner={F(massResolved.rInner)};" +
			$"massROuter={F(massResolved.rOuter)};" +
			$"massAmp={F(massResolved.amp)};" +
			$"massGamma={F(massResolved.a)};" +
			$"massA={F(massResolved.a)};" +
			$"massB={F(massResolved.b)};" +
			$"massC={F(massResolved.c)};" +
			$"massModeFlags={massResolved.modeFlags};" +
			$"massTransport={massSnap.TransportModel};" +
			$"massBetaMode={(massResolved.overrideBetaScale ? "override" : "global")};" +
			$"massBetaScale={F(massResolved.betaScale)};" +
			$"bandEnabled={(bandResolved.enabled ? 1 : 0)};" +
			$"bandSource={bandResolveReason};" +
			$"bandCurve={bandResolved.curveType};" +
			$"bandRInner={F(bandResolved.rInner)};" +
			$"bandROuter={F(bandResolved.rOuter)};" +
			$"bandAmp={F(bandResolved.amp)};" +
			$"bandA={F(bandResolved.a)};" +
			$"bandB={F(bandResolved.b)};" +
			$"bandC={F(bandResolved.c)};" +
			$"bandModeFlags={bandResolved.modeFlags};" +
			$"bandTransport={bandSnap.TransportModel};" +
			$"bandBetaMode={(bandResolved.overrideBetaScale ? "override" : "global")};" +
			$"bandBetaScale={F(bandResolved.betaScale)};" +
			$"useIntegrated={(rayRenderer.UseIntegratedField ? 1 : 0)};" +
			$"bendScale={F(rayRenderer.BendScale)};" +
			$"fieldStrength={F(rayRenderer.FieldStrength)};" +
			$"stepLength={F(rayRenderer.StepLength)};" +
			$"minStep={F(Mathf.Min(rayRenderer.MinStepLength, rayRenderer.MaxStepLength))};" +
			$"maxStep={F(Mathf.Max(rayRenderer.MinStepLength, rayRenderer.MaxStepLength))};" +
			$"stepAdaptGain={F(rayRenderer.StepAdaptGain)};" +
			$"maxSteps={rayRenderer.StepsPerRay};" +
			$"sourcePatternMode={PatternMode};" +
			$"sourcePatternCount={_sourceMarkers.Length};" +
			$"sourceSpacingX={F(Mathf.Max(0f, SourceSpacingX))};" +
			$"sourceSpacingY={F(Mathf.Max(0f, SourceSpacingY))};" +
			$"sourceMarkerRadius={F(Mathf.Max(0.01f, SourceMarkerRadius))};" +
			$"sourceRadius={F(sourceRadius)};" +
			$"absorbCount={absorbedRayCount};" +
			$"absorbRate={F(absorbedRate)};" +
			$"ringProbeN={_ringProbeNdc.Length};" +
			$"ringProbeCols={Mathf.Clamp(RingProbeColumns, 2, 32)};" +
			$"ringProbeRows={Mathf.Clamp(RingProbeRows, 2, 32)};" +
			$"ringProbeRotDeg={F(EnableRingProbePatternRotation ? RingProbePatternRotationDeg : 0f)};" +
			$"sourceHits={onAxisRingSummary.SourceHits};" +
			$"backgroundHits={onAxisRingSummary.BackgroundHits};" +
			$"absorbedHits={onAxisRingSummary.AbsorbedHits};" +
			$"missHits={onAxisRingSummary.MissHits};" +
			$"radiiN={onAxisRingSummary.RadiiCount};" +
			$"radiusMean={F(onAxisRingSummary.RadiusMean)};" +
			$"radiusStdDev={F(onAxisRingSummary.RadiusStdDev)};" +
			$"radiusRange={F(onAxisRingSummary.RadiusRange)};" +
			$"ringChecksum={onAxisRingSummary.Checksum};" +
			$"ringHist=[{onAxisRingSummary.RadiusHistogram}];" +
			$"offAxisEnabled={(EnableOffAxisSourceCase ? 1 : 0)};" +
			$"offAxisX={F(offAxisOffset.X)};" +
			$"offAxisSourceHits={offAxisRingSummary.SourceHits};" +
			$"offAxisRadiiN={offAxisRingSummary.RadiiCount};" +
			$"offAxisRadiusMean={F(offAxisRingSummary.RadiusMean)};" +
			$"offAxisRadiusStdDev={F(offAxisRingSummary.RadiusStdDev)};" +
			$"offAxisRadiusRange={F(offAxisRingSummary.RadiusRange)};" +
			$"offAxisRingChecksum={offAxisRingSummary.Checksum};" +
			$"offAxisRingHist=[{offAxisRingSummary.RadiusHistogram}];" +
			$"offAxis2Enabled={(EnableSecondOffAxisSourceCase ? 1 : 0)};" +
			$"offAxis2X={F(offAxisOffset2.X)};" +
			$"offAxis2SourceHits={offAxisRingSummary2.SourceHits};" +
			$"offAxis2RadiiN={offAxisRingSummary2.RadiiCount};" +
			$"offAxis2RadiusMean={F(offAxisRingSummary2.RadiusMean)};" +
			$"offAxis2RadiusStdDev={F(offAxisRingSummary2.RadiusStdDev)};" +
			$"offAxis2RadiusRange={F(offAxisRingSummary2.RadiusRange)};" +
			$"offAxis2RingChecksum={offAxisRingSummary2.Checksum};" +
			$"offAxis2RingHist=[{offAxisRingSummary2.RadiusHistogram}];" +
			$"rayK={FingerprintRaySteps};" +
			$"accelMass=[{massAccelVector}];" +
			$"accelBand=[{bandAccelVector}];" +
			$"accelTotal=[{totalAccelVector}];" +
			$"rayChecksum={rayEndpointChecksum}";

		string fingerprintHash = ComputeSha256Hex(fingerprintCore);
		GD.Print(
			$"EinsteinRingMinimalFingerprintRaw: accelMass=[{massAccelVector}] accelBand=[{bandAccelVector}] accelTotal=[{totalAccelVector}] " +
			$"rayEndpoints=[{rayEndpointVector}] rayChecksum={rayEndpointChecksum} " +
			$"onAxisOutcome=[{onAxisRingSummary.OutcomeVector}] onAxisRadii=[{onAxisRingSummary.RadiusVector}] onAxisChecksum={onAxisRingSummary.Checksum} " +
			$"offAxisOutcome=[{offAxisRingSummary.OutcomeVector}] offAxisRadii=[{offAxisRingSummary.RadiusVector}] offAxisChecksum={offAxisRingSummary.Checksum} " +
			$"offAxis2Outcome=[{offAxisRingSummary2.OutcomeVector}] offAxis2Radii=[{offAxisRingSummary2.RadiusVector}] offAxis2Checksum={offAxisRingSummary2.Checksum}");
		return $"{fingerprintCore};sha256={fingerprintHash}";
	}

	private static bool IsCanonicalResolve(string resolveReason)
	{
		return string.Equals(resolveReason, "canonical", StringComparison.Ordinal);
	}

	private static string BuildResolvedSummary(string sourceLabel, string resolveReason, FieldSource3D.ResolvedFieldParams resolved)
	{
		return
			$"{sourceLabel}_resolve_reason={resolveReason} {sourceLabel}_enabled={(resolved.enabled ? 1 : 0)} {sourceLabel}_curve={resolved.curveType} {sourceLabel}_modeFlags={resolved.modeFlags} " +
			$"{sourceLabel}_amp={resolved.amp:0.######} {sourceLabel}_rInner={resolved.rInner:0.######} {sourceLabel}_rOuter={resolved.rOuter:0.######} " +
			$"{sourceLabel}_A={resolved.a:0.######} {sourceLabel}_B={resolved.b:0.######} {sourceLabel}_C={resolved.c:0.######} " +
			$"{sourceLabel}_beta_mode={(resolved.overrideBetaScale ? "override" : "global")} {sourceLabel}_beta_scale={resolved.betaScale:0.######}";
	}

	private static string BuildRendererSummary(bool hasRenderer, bool useIntegrated, float bendScale, float fieldStrength, float transportStrength)
	{
		return
			$"hasRenderer={(hasRenderer ? 1 : 0)} useIntegrated={(useIntegrated ? 1 : 0)} " +
			$"bendScale={bendScale:0.######} fieldStrength={fieldStrength:0.######} transport={transportStrength:0.######}";
	}

	private void EnforceFixtureBroadphasePolicy(GrinFilmCamera filmCamera)
	{
		if (filmCamera == null)
		{
			return;
		}

		bool changed =
			filmCamera.BroadphaseControlMode != GrinFilmCamera.BroadphaseMode.Policy ||
			filmCamera.BroadphasePolicy != GrinFilmCamera.BroadphasePolicyMode.OverlapOnly ||
			filmCamera.UseBroadphaseQuickRay ||
			!filmCamera.UseBroadphaseOverlap;

		filmCamera.BroadphaseControlMode = GrinFilmCamera.BroadphaseMode.Policy;
		filmCamera.BroadphasePolicy = GrinFilmCamera.BroadphasePolicyMode.OverlapOnly;
		filmCamera.UseBroadphaseQuickRay = false;
		filmCamera.UseBroadphaseOverlap = true;

		if (changed && !_loggedBroadphaseLock)
		{
			_loggedBroadphaseLock = true;
			GD.Print("[EinsteinFixture][Broadphase] enforcing policy=OverlapOnly mode=Policy");
		}
	}

	private void EnforceFixtureDebugHitColoring(GrinFilmCamera filmCamera)
	{
		if (filmCamera == null)
		{
			return;
		}
		bool renderTestMode = IsRenderTestMode() || GetNodeOrNull<Node>("RenderTestRunner") != null;

		bool changed =
			!filmCamera.FixtureDebugHitColoringEnabled ||
			!string.Equals(filmCamera.FixtureDebugSourceGroup, "fixture_source", StringComparison.Ordinal) ||
			filmCamera.SkyColor != new Color(0f, 0f, 0f, 1f) ||
			filmCamera.FixtureDebugColorAuthorityEnabled != renderTestMode ||
			filmCamera.FixtureDebugTraceEnabled != renderTestMode;

		filmCamera.FixtureDebugHitColoringEnabled = true;
		filmCamera.FixtureDebugSourceGroup = "fixture_source";
		filmCamera.FixtureDebugSourceHitColor = new Color(1f, 1f, 0.75f, 1f);
		filmCamera.FixtureDebugBackgroundHitColor = new Color(0.12f, 0.20f, 0.34f, 1f);
		filmCamera.FixtureDebugAbsorbedColor = new Color(0f, 0f, 0f, 1f);
		filmCamera.FixtureDebugColorAuthorityEnabled = renderTestMode;
		filmCamera.FixtureDebugTraceEnabled = renderTestMode;
		filmCamera.FixtureDebugTraceSampleModulo = 41;
		filmCamera.FixtureDebugTraceMaxLogsPerStep = 12;
		filmCamera.SkyColor = new Color(0f, 0f, 0f, 1f);

		if (changed && !_loggedFixtureDebugColoringLock)
		{
			_loggedFixtureDebugColoringLock = true;
			GD.Print(
				$"[EinsteinFixture][DebugHitColor] enabled=1 source_group=fixture_source source_color=(1,1,0.75) background_color=(0.12,0.2,0.34) absorbed=(0,0,0) " +
				$"authority={(renderTestMode ? 1 : 0)} trace={(renderTestMode ? 1 : 0)}");
		}
	}

	private static bool IsRenderTestMode()
	{
		if (HasRenderTestFlag(OS.GetCmdlineUserArgs()))
		{
			return true;
		}

		if (HasRenderTestFlag(OS.GetCmdlineArgs()))
		{
			return true;
		}

		return false;
	}

	private static bool HasRenderTestFlag(string[] args)
	{
		if (args == null || args.Length == 0)
		{
			return false;
		}

		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (string.Equals(arg, "--render-test", StringComparison.OrdinalIgnoreCase) ||
				arg.StartsWith("--render-test-fixture=", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private void LogRingRadialSummary(string caseLabel, Vector3 sourceOffset, RingProbeSummary summary, int histogramBins)
	{
		string histogramToken = EnableRingRadialHistogram && histogramBins > 1
			? $" histogram_bins={histogramBins} histogram=[{summary.RadiusHistogram}]"
			: string.Empty;
		GD.Print(
			$"[EinsteinFixture][RadialSummary] case={caseLabel} sourceOffset={FormatVec3(sourceOffset)} sourceHitCount={summary.SourceHits} " +
			$"meanDetectorRadius={summary.RadiusMean:0.###} stddevDetectorRadius={summary.RadiusStdDev:0.###} " +
			$"minDetectorRadius={summary.RadiusMin:0.###} maxDetectorRadius={summary.RadiusMax:0.###}{histogramToken}");
	}

	private static void ApplyCanonicalFixtureParams(FieldSource3D field)
	{
		field.Enabled = FixedEnabled;
		field.ShapeType = FieldShapeType.SphereRadial;
		field.CurveType = FixedCurveType;
		field.CanonicalEnableInnerRadius = FixedEnableInnerRadius;
		field.RInner = FixedRInner;
		field.ROuter = FixedROuter;
		field.Amp = FixedAmp;
		field.CanonicalGamma = FixedGamma;
		field.CanonicalOverrideBetaScale = FixedOverrideBetaScale;
		field.CanonicalBetaScale = FixedBetaScale;
		field.CurveA = FixedGamma;
		field.CurveB = 0.0f;
		field.CurveC = 0.0f;
		field.ModeFlags = FixedModeFlags;
		field.TransportModel = FixedTransportModel;
		field.Softening = FixedSoftening;
		field.CanonicalEdgeSoftness = FixedEdgeSoftness;
		field.DebugDrawBounds = FixedDebugDrawBounds;
		field.DebugDrawInGame = FixedDebugDrawInGame;
	}

	private static PhotonBandShell ComputePhotonBandShell(float massOuterRadius, float massInnerRadius)
	{
		float center = Mathf.Max(massInnerRadius + 0.05f, massOuterRadius * FixedPhotonBandCenterRadiusOuterFraction);
		float width = Mathf.Max(FixedPhotonBandMinWidth, massOuterRadius * FixedPhotonBandWidthOuterFraction);
		float halfWidth = width * 0.5f;
		float rInner = Mathf.Max(massInnerRadius + 0.05f, center - halfWidth);
		float rOuter = Mathf.Max(rInner + 0.05f, center + halfWidth);
		return new PhotonBandShell(center, width, rInner, rOuter);
	}

	private static void ApplyPhotonBandFixtureParams(FieldSource3D photonBandField, PhotonBandShell shell)
	{
		photonBandField.Enabled = FixedPhotonBandEnabled;
		photonBandField.ShapeType = FieldShapeType.SphereRadial;
		photonBandField.CurveType = FixedPhotonBandCurveType;
		photonBandField.CanonicalEnableInnerRadius = true;
		photonBandField.RInner = shell.RInner;
		photonBandField.ROuter = shell.ROuter;
		photonBandField.Amp = FixedPhotonBandAmp;
		photonBandField.CanonicalGamma = 1.0f;
		photonBandField.CurveA = FixedPhotonBandCurveA;
		photonBandField.CurveB = FixedPhotonBandCurveB;
		photonBandField.CurveC = FixedPhotonBandCurveC;
		photonBandField.CanonicalOverrideBetaScale = FixedPhotonBandOverrideBetaScale;
		photonBandField.CanonicalBetaScale = FixedPhotonBandBetaScale;
		photonBandField.ModeFlags = FixedPhotonBandModeFlags;
		photonBandField.TransportModel = FixedPhotonBandTransportModel;
		photonBandField.Softening = FixedPhotonBandSoftening;
		photonBandField.CanonicalEdgeSoftness = FixedPhotonBandEdgeSoftness;
		photonBandField.DebugDrawBounds = FixedDebugDrawBounds;
		photonBandField.DebugDrawInGame = FixedDebugDrawInGame;
	}

	private static RayBeamRenderer.FieldSourceSnap[] BuildSourceArray(
		RayBeamRenderer.FieldSourceSnap massSnap,
		RayBeamRenderer.FieldSourceSnap bandSnap)
	{
		return new[] { massSnap, bandSnap };
	}

	private void MeasureRayCurvature(
		RayBeamRenderer.FieldSourceSnap[] snaps,
		Vector3 launchCenter,
		float launchOuterRadius,
		RayBeamRenderer rayRenderer,
		int steps,
		out int deviatedRayCount,
		out float maxDeviation,
		out float deviationSum)
	{
		deviatedRayCount = 0;
		maxDeviation = 0f;
		deviationSum = 0f;

		float stepLength = Mathf.Max(0.0001f, rayRenderer.StepLength);
		float minStep = Mathf.Max(0.0001f, Mathf.Min(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float maxStep = Mathf.Max(minStep, Mathf.Max(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float stepAdaptGain = Mathf.Max(0f, rayRenderer.StepAdaptGain);
		float bendScale = rayRenderer.BendScale;
		float fieldStrength = rayRenderer.FieldStrength;

		float launchZ = -Mathf.Max(0.25f, launchOuterRadius * 0.5f);
		float launchXY = Mathf.Max(0.1f, launchOuterRadius * 0.2f);
		Vector3 baseOrigin = launchCenter + new Vector3(0f, 0f, launchZ);
		Vector3 marchDirection = Vector3.Back;

		for (int i = 0; i < RayProbeNdc.Length; i++)
		{
			Vector2 ndc = RayProbeNdc[i];
			Vector3 origin = baseOrigin + new Vector3(ndc.X * launchXY, ndc.Y * launchXY, 0f);
			Vector3 dir = marchDirection;

			Vector3 pCurved = origin;
			Vector3 vCurved = dir;
			Vector3 pStraight = origin;
			for (int s = 0; s < steps; s++)
			{
				Vector3 accel = ComputeTransportAcceleration(pCurved, snaps, bendScale, fieldStrength, out float aLen);
				float step = Mathf.Clamp(stepLength / (1f + aLen * stepAdaptGain), minStep, maxStep);
				Vector3 nextVelocity = vCurved + accel * step;
				if (nextVelocity.LengthSquared() > 1e-12f)
				{
					vCurved = nextVelocity;
				}
				pCurved += vCurved * step;
				pStraight += dir * step;
			}

			float deviation = pCurved.DistanceTo(pStraight);
			deviationSum += deviation;
			if (deviation > maxDeviation)
			{
				maxDeviation = deviation;
			}
			if (deviation > CurvatureDeviationMin)
			{
				deviatedRayCount++;
			}
		}
	}

	private static int CountAbsorbedRayProbes(
		RayBeamRenderer.FieldSourceSnap[] snaps,
		Vector3 launchCenter,
		float launchOuterRadius,
		RayBeamRenderer rayRenderer,
		int steps)
	{
		if (!HasAbsorbingSource(snaps))
		{
			return 0;
		}

		int absorbedRayCount = 0;
		float stepLength = Mathf.Max(0.0001f, rayRenderer.StepLength);
		float minStep = Mathf.Max(0.0001f, Mathf.Min(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float maxStep = Mathf.Max(minStep, Mathf.Max(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float stepAdaptGain = Mathf.Max(0f, rayRenderer.StepAdaptGain);
		float bendScale = rayRenderer.BendScale;
		float fieldStrength = rayRenderer.FieldStrength;
		float launchZ = -Mathf.Max(0.25f, launchOuterRadius * 0.5f);
		float launchXY = Mathf.Max(0.1f, launchOuterRadius * 0.2f);
		Vector3 baseOrigin = launchCenter + new Vector3(0f, 0f, launchZ);
		Vector3 marchDirection = Vector3.Back;

		for (int i = 0; i < RayProbeNdc.Length; i++)
		{
			Vector2 ndc = RayProbeNdc[i];
			Vector3 p = baseOrigin + new Vector3(ndc.X * launchXY, ndc.Y * launchXY, 0f);
			Vector3 v = marchDirection;
			for (int s = 0; s < steps; s++)
			{
				if (IsAbsorbedAtPoint(p, snaps))
				{
					absorbedRayCount++;
					break;
				}

				Vector3 accel = ComputeTransportAcceleration(p, snaps, bendScale, fieldStrength, out float aLen);
				float step = Mathf.Clamp(stepLength / (1f + aLen * stepAdaptGain), minStep, maxStep);
				Vector3 nextVelocity = v + accel * step;
				if (nextVelocity.LengthSquared() > 1e-12f)
				{
					v = nextVelocity;
				}
				p += v * step;
			}
		}

		return absorbedRayCount;
	}

	private void FailInvalid(string reason, string details)
	{
		_invalidFixture = true;
		string msg = $"Einstein_Ring_Minimal invalid: {reason} | {details}";
		GD.PrintErr(msg);
		GD.PushError(msg);
		CallDeferred(nameof(QuitInvalidFixtureIfHarnessed));
	}

	private void QuitInvalidFixtureIfHarnessed()
	{
		if (!_invalidFixture)
		{
			return;
		}

		if (GetNodeOrNull<Node>("RenderTestRunner") != null)
		{
			GD.PrintErr("[EinsteinFixture][FAIL] Requesting quit code=1 due to fixture invariant failure.");
			GetTree()?.Quit(1);
		}
	}

	private static string FormatFloatArray(float[] values)
	{
		if (values == null || values.Length == 0)
		{
			return string.Empty;
		}

		string[] parts = new string[values.Length];
		for (int i = 0; i < values.Length; i++)
		{
			parts[i] = $"{values[i]:0.######}";
		}

		return string.Join(",", parts);
	}

	private static string FormatVec3(Vector3 v)
	{
		return $"({v.X:0.###},{v.Y:0.###},{v.Z:0.###})";
	}

	private static float[] BuildFingerprintAccelMagnitudes(Vector3 fieldCenter, RayBeamRenderer.FieldSourceSnap snap)
	{
		float[] values = new float[FingerprintAccelOffsets.Length];
		for (int i = 0; i < FingerprintAccelOffsets.Length; i++)
		{
			FieldMath.EvalResult eval = EvaluateSnapAccel(fieldCenter + FingerprintAccelOffsets[i], snap);
			values[i] = eval.AccelerationMagnitude;
		}

		return values;
	}

	private static float[] BuildFingerprintCombinedAccelMagnitudes(
		Vector3 fieldCenter,
		RayBeamRenderer.FieldSourceSnap[] snaps)
	{
		float[] values = new float[FingerprintAccelOffsets.Length];
		for (int i = 0; i < FingerprintAccelOffsets.Length; i++)
		{
			Vector3 total = ComputeCombinedRawAcceleration(fieldCenter + FingerprintAccelOffsets[i], snaps);
			values[i] = total.Length();
		}

		return values;
	}

	private static Vector3[] BuildFingerprintRayEndpoints(
		RayBeamRenderer.FieldSourceSnap[] snaps,
		Vector3 launchCenter,
		float launchOuterRadius,
		RayBeamRenderer rayRenderer,
		int steps)
	{
		Vector3[] endpoints = new Vector3[FingerprintRayNdc.Length];
		float stepLength = Mathf.Max(0.0001f, rayRenderer.StepLength);
		float minStep = Mathf.Max(0.0001f, Mathf.Min(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float maxStep = Mathf.Max(minStep, Mathf.Max(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float stepAdaptGain = Mathf.Max(0f, rayRenderer.StepAdaptGain);
		float bendScale = rayRenderer.BendScale;
		float fieldStrength = rayRenderer.FieldStrength;
		float launchZ = -Mathf.Max(0.25f, launchOuterRadius * 0.5f);
		float launchXY = Mathf.Max(0.1f, launchOuterRadius * 0.2f);
		Vector3 baseOrigin = launchCenter + new Vector3(0f, 0f, launchZ);
		Vector3 baseDirection = Vector3.Back;

		for (int i = 0; i < FingerprintRayNdc.Length; i++)
		{
			Vector2 ndc = FingerprintRayNdc[i];
			Vector3 p = baseOrigin + new Vector3(ndc.X * launchXY, ndc.Y * launchXY, 0f);
			Vector3 v = baseDirection;
			for (int s = 0; s < steps; s++)
			{
				if (IsAbsorbedAtPoint(p, snaps))
				{
					break;
				}

				Vector3 accel = ComputeTransportAcceleration(p, snaps, bendScale, fieldStrength, out float aLen);
				float step = Mathf.Clamp(stepLength / (1f + aLen * stepAdaptGain), minStep, maxStep);
				Vector3 nextVelocity = v + accel * step;
				if (nextVelocity.LengthSquared() > 1e-12f)
				{
					v = nextVelocity;
				}
				p += v * step;
			}
			endpoints[i] = p;
		}

		return endpoints;
	}

	private static FieldMath.EvalResult EvaluateSnapAccel(Vector3 samplePosition, RayBeamRenderer.FieldSourceSnap snap)
	{
		return FieldMath.EvalFieldAccel(
			samplePosition,
			snap.Center,
			snap.CurveType,
			snap.RInner,
			snap.ROuter,
			snap.Amp,
			snap.CurveA,
			snap.Sigma,
			snap.CurveA,
			snap.CurveB,
			snap.CurveC,
			snap.CustomCurve,
			snap.ModeFlags,
			globalBeta: 0f,
			snap.OverrideBetaScale,
			snap.BetaScale,
			snap.EdgeSoftness);
	}

	private static Vector3 ComputeCombinedRawAcceleration(Vector3 samplePosition, RayBeamRenderer.FieldSourceSnap[] snaps)
	{
		Vector3 total = Vector3.Zero;
		if (snaps == null || snaps.Length == 0)
		{
			return total;
		}

		for (int i = 0; i < snaps.Length; i++)
		{
			ref readonly RayBeamRenderer.FieldSourceSnap snap = ref snaps[i];
			if (!snap.Enabled)
			{
				continue;
			}

			FieldMath.EvalResult eval = EvaluateSnapAccel(samplePosition, snap);
			total += eval.Acceleration;
		}

		return total;
	}

	private static Vector3 ComputeTransportAcceleration(
		Vector3 samplePosition,
		RayBeamRenderer.FieldSourceSnap[] snaps,
		float bendScale,
		float fieldStrength,
		out float accelLen)
	{
		Vector3 accel = RayBeamRenderer.ComputeAccelerationAtPointSnap(
			samplePosition,
			snaps,
			globalBeta: 0f,
			globalGamma: 0f,
			bendScale,
			fieldStrength);

		accelLen = accel.Length();
		if (!float.IsFinite(accelLen))
		{
			accel = Vector3.Zero;
			accelLen = 0f;
			return accel;
		}

		if (accelLen > AccelClampMax)
		{
			accel *= AccelClampMax / accelLen;
			accelLen = AccelClampMax;
		}

		return accel;
	}

	private static bool HasAbsorbingSource(RayBeamRenderer.FieldSourceSnap[] snaps)
	{
		if (snaps == null || snaps.Length == 0)
		{
			return false;
		}

		for (int i = 0; i < snaps.Length; i++)
		{
			ref readonly RayBeamRenderer.FieldSourceSnap snap = ref snaps[i];
			if (!snap.Enabled)
			{
				continue;
			}

			if ((snap.ModeFlags & FieldMath.ModeFlagAbsorbInsideInnerRadius) != 0u && snap.RInner > 0f)
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsAbsorbedAtPoint(Vector3 p, RayBeamRenderer.FieldSourceSnap[] snaps)
	{
		if (snaps == null || snaps.Length == 0)
		{
			return false;
		}

		for (int i = 0; i < snaps.Length; i++)
		{
			ref readonly RayBeamRenderer.FieldSourceSnap snap = ref snaps[i];
			if (!snap.Enabled)
			{
				continue;
			}

			if ((snap.ModeFlags & FieldMath.ModeFlagAbsorbInsideInnerRadius) == 0u || snap.RInner <= 0f)
			{
				continue;
			}

			if (p.DistanceSquaredTo(snap.Center) < (snap.RInner * snap.RInner))
			{
				return true;
			}
		}

		return false;
	}

	private static Vector2[] BuildRingProbeNdc(int columns, int rows, float rotationDeg)
	{
		int safeCols = Mathf.Clamp(columns, 2, 32);
		int safeRows = Mathf.Clamp(rows, 2, 32);
		float cos = Mathf.Cos(Mathf.DegToRad(rotationDeg));
		float sin = Mathf.Sin(Mathf.DegToRad(rotationDeg));
		Vector2[] probes = new Vector2[safeCols * safeRows];
		int index = 0;
		for (int y = 0; y < safeRows; y++)
		{
			float yT = safeRows <= 1 ? 0f : y / (float)(safeRows - 1);
			float ny = Mathf.Lerp(-0.525f, 0.525f, yT);
			for (int x = 0; x < safeCols; x++)
			{
				float xT = safeCols <= 1 ? 0f : x / (float)(safeCols - 1);
				float nx = Mathf.Lerp(-0.70f, 0.70f, xT);
				float rx = nx * cos - ny * sin;
				float ry = nx * sin + ny * cos;
				probes[index++] = new Vector2(
					Mathf.Clamp(rx, -0.90f, 0.90f),
					Mathf.Clamp(ry, -0.90f, 0.90f));
			}
		}
		return probes;
	}

	private static DetectorPlaneData BuildDetectorPlaneData(StaticBody3D detector)
	{
		Vector2 halfExtents = new Vector2(40f, 40f);
		CollisionShape3D collisionShape = detector.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (collisionShape?.Shape is BoxShape3D box)
		{
			Vector3 s = detector.Scale;
			halfExtents = new Vector2(
				Mathf.Abs(box.Size.X * 0.5f * (Mathf.Abs(s.X) > 0f ? Mathf.Abs(s.X) : 1f)),
				Mathf.Abs(box.Size.Y * 0.5f * (Mathf.Abs(s.Y) > 0f ? Mathf.Abs(s.Y) : 1f)));
		}
		return new DetectorPlaneData(detector.GlobalTransform, halfExtents);
	}

	private static float ResolveSourceRadius(SourceMarkerSphere[] sources)
	{
		if (sources == null || sources.Length == 0)
		{
			return 0.24f;
		}

		float r = Mathf.Max(0.01f, sources[0].Radius);
		for (int i = 1; i < sources.Length; i++)
		{
			r = Mathf.Max(r, Mathf.Max(0.01f, sources[i].Radius));
		}
		return r;
	}

	private static float[] BuildSourceRadiusCandidates(float baseRadius)
	{
		float safe = Mathf.Max(0.01f, baseRadius);
		return new[]
		{
			safe,
			safe * 1.25f,
			safe * 1.5f
		};
	}

	private bool RebuildSourcePatternMarkers()
	{
		StaticBody3D template = GetNodeOrNull<StaticBody3D>(SourceTemplatePath);
		if (template == null)
		{
			FailInvalid("missing source marker template", $"template_path={SourceTemplatePath}");
			return false;
		}
		template.Visible = false;
		CollisionShape3D templateCollision = template.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (templateCollision != null)
		{
			templateCollision.Disabled = true;
		}

		if (_sourcePatternRoot == null || !IsInstanceValid(_sourcePatternRoot))
		{
			_sourcePatternRoot = new Node3D
			{
				Name = "source_pattern_runtime"
			};
			Node parent = template.GetParent();
			if (parent == null)
			{
				FailInvalid("missing source marker template parent", $"template_path={SourceTemplatePath}");
				return false;
			}
			parent.AddChild(_sourcePatternRoot);
		}

		for (int i = _sourcePatternRoot.GetChildCount() - 1; i >= 0; i--)
		{
			_sourcePatternRoot.GetChild(i).QueueFree();
		}

		Mesh templateMesh = template.GetNodeOrNull<MeshInstance3D>("MeshInstance3D")?.Mesh;
		if (templateMesh == null)
		{
			FailInvalid("invalid source marker template", "missing MeshInstance3D mesh");
			return false;
		}

		Shape3D templateShape = templateCollision?.Shape;
		if (templateShape == null)
		{
			FailInvalid("invalid source marker template", "missing CollisionShape3D shape");
			return false;
		}

		float safeRadius = Mathf.Max(0.01f, SourceMarkerRadius);
		bool manualMode = !IsRenderTestMode();
		SourcePatternConfig cfg = new(
			PatternMode,
			SourceCountX,
			SourceCountY,
			SourceSpacingX,
			SourceSpacingY,
			IncludeCenterPoint);
		Vector3[] offsets = SourcePatternHelper.BuildLocalOffsets(in cfg);
		SourceMarkerSphere[] markers = new SourceMarkerSphere[offsets.Length];
		for (int i = 0; i < offsets.Length; i++)
		{
			Vector3 localPos = PatternOriginLocal + offsets[i];
			StaticBody3D marker = new StaticBody3D
			{
				Name = $"source_marker_{i:000}",
				Transform = new Transform3D(Basis.Identity, localPos)
			};
			marker.AddToGroup("raytrace_geometry");
			marker.AddToGroup("fixture_geometry");
			marker.AddToGroup("fixture_source");

			MeshInstance3D mesh = new MeshInstance3D
			{
				Name = "MeshInstance3D",
				Mesh = templateMesh.Duplicate() as Mesh ?? templateMesh
			};
			Vector3 localDelta = localPos - PatternOriginLocal;
			Color axisColor = ResolveAxisColor(localDelta, manualMode);
			if (mesh.Mesh is SphereMesh sphereMesh)
			{
				float meshRadius = manualMode ? safeRadius * ManualMarkerMeshScale : safeRadius;
				sphereMesh.Radius = meshRadius;
				sphereMesh.Height = meshRadius * 2f;
			}
			if (manualMode)
			{
				mesh.MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = axisColor,
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
				};
			}
			marker.AddChild(mesh);

			CollisionShape3D collision = new CollisionShape3D
			{
				Name = "CollisionShape3D",
				Shape = templateShape.Duplicate() as Shape3D ?? templateShape
			};
			if (collision.Shape is SphereShape3D sphereShape)
			{
				sphereShape.Radius = safeRadius;
			}
			marker.AddChild(collision);
			_sourcePatternRoot.AddChild(marker);

			markers[i] = new SourceMarkerSphere(localPos, marker.GlobalPosition, safeRadius);
		}

		_sourceMarkers = markers;
		return true;
	}

	private void LogSourcePatternSummary()
	{
		if (_sourceMarkers == null || _sourceMarkers.Length == 0)
		{
			GD.Print(
				$"[EinsteinFixture][SourcePattern] mode={PatternMode} count=0 spacing=({SourceSpacingX:0.###},{SourceSpacingY:0.###}) " +
				$"radius={Mathf.Max(0.01f, SourceMarkerRadius):0.###} originLocal={FormatVec3(PatternOriginLocal)} includeCenter={(IncludeCenterPoint ? 1 : 0)}");
			return;
		}

		int showCount = Mathf.Min(6, _sourceMarkers.Length);
		StringBuilder localBuilder = new StringBuilder(showCount * 20);
		StringBuilder worldBuilder = new StringBuilder(showCount * 20);
		for (int i = 0; i < showCount; i++)
		{
			if (i > 0)
			{
				localBuilder.Append('|');
				worldBuilder.Append('|');
			}
			localBuilder.Append(FormatVec3(_sourceMarkers[i].LocalCenter));
			worldBuilder.Append(FormatVec3(_sourceMarkers[i].WorldCenter));
		}

		string signature = BuildSourcePatternSignature(_sourceMarkers);
		string checksum = ComputeSha256Hex(signature);
		GD.Print(
			$"[EinsteinFixture][SourcePattern] mode={PatternMode} count={_sourceMarkers.Length} spacing=({Mathf.Max(0f, SourceSpacingX):0.###},{Mathf.Max(0f, SourceSpacingY):0.###}) " +
			$"radius={Mathf.Max(0.01f, SourceMarkerRadius):0.###} originLocal={FormatVec3(PatternOriginLocal)} includeCenter={(IncludeCenterPoint ? 1 : 0)} " +
			$"localSample=[{localBuilder}] worldSample=[{worldBuilder}] checksum={checksum}");
	}

	private void LogSourcePatternMarkerDetails()
	{
		if (_sourcePatternRoot == null || !IsInstanceValid(_sourcePatternRoot))
		{
			return;
		}

		int markerCount = _sourcePatternRoot.GetChildCount();
		bool crossExpectedCheck = PatternMode == SourcePatternMode.CrossXY && SourceCountX == 5 && SourceCountY == 5 && IncludeCenterPoint;
		if (crossExpectedCheck)
		{
			GD.Print($"[EinsteinFixture][SourcePatternCheck] cross_expected_count=9 actual={markerCount} ok={(markerCount == 9 ? 1 : 0)}");
		}

		int overlapExactCount = 0;
		float minDistance = float.PositiveInfinity;
		for (int i = 0; i < _sourceMarkers.Length; i++)
		{
			for (int j = i + 1; j < _sourceMarkers.Length; j++)
			{
				float d = _sourceMarkers[i].WorldCenter.DistanceTo(_sourceMarkers[j].WorldCenter);
				if (d < minDistance)
				{
					minDistance = d;
				}
				if (d <= 1e-4f)
				{
					overlapExactCount++;
				}
			}
		}
		if (!float.IsFinite(minDistance))
		{
			minDistance = 0f;
		}

		Node3D centerMarker = GetNodeOrNull<Node3D>(SpherePath);
		float centerZ = centerMarker?.GlobalPosition.Z ?? 0f;
		float zMin = float.PositiveInfinity;
		float zMax = float.NegativeInfinity;
		for (int i = 0; i < _sourceMarkers.Length; i++)
		{
			float z = _sourceMarkers[i].WorldCenter.Z;
			if (z < zMin) zMin = z;
			if (z > zMax) zMax = z;
		}
		if (!float.IsFinite(zMin)) zMin = 0f;
		if (!float.IsFinite(zMax)) zMax = 0f;
		int behindCenter = zMax <= centerZ ? 1 : 0;

		float detectorZ = GetNodeOrNull<StaticBody3D>(DetectorPath)?.GlobalPosition.Z ?? float.NaN;
		string detectorZText = float.IsFinite(detectorZ) ? detectorZ.ToString("0.###", CultureInfo.InvariantCulture) : "n/a";
		GD.Print(
			$"[EinsteinFixture][SourcePatternLayout] markers={markerCount} overlap_exact={overlapExactCount} min_pair_dist={minDistance:0.######} " +
			$"z_range=({zMin:0.###},{zMax:0.###}) center_z={centerZ:0.###} behind_center={behindCenter} detector_z={detectorZText}");

		for (int i = 0; i < _sourcePatternRoot.GetChildCount(); i++)
		{
			if (_sourcePatternRoot.GetChild(i) is not StaticBody3D marker)
			{
				continue;
			}
			CollisionShape3D collision = marker.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
			MeshInstance3D mesh = marker.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
			string groups = BuildMarkerGroups(marker);
			Vector3 localPos = marker.Position;
			Vector3 worldPos = marker.GlobalPosition;
			int collisionEnabled = (collision != null && !collision.Disabled) ? 1 : 0;
			int visible = (marker.Visible && (mesh?.Visible ?? true)) ? 1 : 0;
			GD.Print(
				$"[EinsteinFixture][SourcePatternMarker] i={i} name={marker.Name} local={FormatVec3(localPos)} global={FormatVec3(worldPos)} " +
				$"groups=[{groups}] collision_enabled={collisionEnabled} visible={visible}");
		}
	}

	private static string BuildMarkerGroups(Node marker)
	{
		StringBuilder b = new StringBuilder(48);
		if (marker.IsInGroup("raytrace_geometry")) b.Append("raytrace_geometry");
		if (marker.IsInGroup("fixture_geometry"))
		{
			if (b.Length > 0) b.Append(',');
			b.Append("fixture_geometry");
		}
		if (marker.IsInGroup("fixture_source"))
		{
			if (b.Length > 0) b.Append(',');
			b.Append("fixture_source");
		}
		return b.ToString();
	}

	private static Color ResolveAxisColor(Vector3 localDelta, bool manualMode)
	{
		if (!manualMode)
		{
			return Colors.White;
		}

		bool onX = Mathf.Abs(localDelta.Y) <= 1e-4f;
		bool onY = Mathf.Abs(localDelta.X) <= 1e-4f;
		if (onX && onY)
		{
			return new Color(1f, 1f, 1f, 1f);
		}
		if (onX)
		{
			return new Color(1f, 0.6f, 0.2f, 1f);
		}
		if (onY)
		{
			return new Color(0.2f, 0.95f, 1f, 1f);
		}
		return new Color(0.9f, 0.9f, 0.9f, 1f);
	}

	private static string BuildSourcePatternSignature(SourceMarkerSphere[] markers)
	{
		if (markers == null || markers.Length == 0)
		{
			return string.Empty;
		}

		StringBuilder builder = new StringBuilder(markers.Length * 32);
		for (int i = 0; i < markers.Length; i++)
		{
			if (i > 0)
			{
				builder.Append('|');
			}
			builder.Append(F(markers[i].LocalCenter.X));
			builder.Append(',');
			builder.Append(F(markers[i].LocalCenter.Y));
			builder.Append(',');
			builder.Append(F(markers[i].LocalCenter.Z));
			builder.Append(',');
			builder.Append(F(markers[i].Radius));
		}
		return builder.ToString();
	}

	private static float ResolveAspect(GrinFilmCamera filmCamera, Camera3D camera)
	{
		if (filmCamera != null && filmCamera.Height > 0)
		{
			return Mathf.Max(0.1f, filmCamera.Width / (float)filmCamera.Height);
		}

		Viewport viewport = camera?.GetViewport();
		if (viewport != null)
		{
			Vector2 size = viewport.GetVisibleRect().Size;
			if (size.Y > 0f)
			{
				return Mathf.Max(0.1f, size.X / (float)size.Y);
			}
		}

		return 16f / 9f;
	}

	private static RingProbeSummary RunRingProbePass(
		Vector2[] ringProbeNdc,
		RayBeamRenderer rayRenderer,
		RayBeamRenderer.FieldSourceSnap[] snaps,
		Vector3 launchCenter,
		float launchOuterRadius,
		DetectorPlaneData detector,
		SourceMarkerSphere[] sources,
		Vector3 sourceOffset,
		float sourceRadiusOverride,
		int steps,
		int histogramBins)
	{
		float stepLength = Mathf.Max(0.0001f, rayRenderer.StepLength);
		float minStep = Mathf.Max(0.0001f, Mathf.Min(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float maxStep = Mathf.Max(minStep, Mathf.Max(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float stepAdaptGain = Mathf.Max(0f, rayRenderer.StepAdaptGain);
		float bendScale = rayRenderer.BendScale;
		float fieldStrength = rayRenderer.FieldStrength;
		float launchZ = -Mathf.Max(0.25f, launchOuterRadius * 0.5f);
		float launchXY = Mathf.Max(0.1f, launchOuterRadius * 0.4f);
		Vector3 baseOrigin = launchCenter + new Vector3(0f, 0f, launchZ);

		int sourceHits = 0;
		int backgroundHits = 0;
		int absorbedHits = 0;
		int missHits = 0;

		int ringProbeCount = ringProbeNdc?.Length ?? 0;
		if (ringProbeCount <= 0)
		{
			return new RingProbeSummary(0, 0, 0, 0, 0, 0f, 0f, 0f, 0f, 0f, string.Empty, string.Empty, string.Empty, string.Empty);
		}

		float[] sourceRadii = new float[ringProbeCount];
		int radiusCount = 0;
		StringBuilder outcomeBuilder = new StringBuilder(ringProbeCount * 2);

		for (int i = 0; i < ringProbeCount; i++)
		{
			Vector2 ndc = ringProbeNdc[i];
			Vector3 origin = baseOrigin + new Vector3(ndc.X * launchXY, ndc.Y * launchXY, 0f);
			float projectedDetectorRadius;
			RingProbeOutcome outcome = TraceRingProbeRay(
				origin,
				snaps,
				detector,
				sources,
				sourceOffset,
				sourceRadiusOverride,
				stepLength,
				minStep,
				maxStep,
				stepAdaptGain,
				bendScale,
				fieldStrength,
				steps,
				out projectedDetectorRadius);

			if (i > 0)
			{
				outcomeBuilder.Append(',');
			}
			outcomeBuilder.Append((int)outcome);

			switch (outcome)
			{
				case RingProbeOutcome.Source:
					sourceHits++;
					if (float.IsFinite(projectedDetectorRadius))
					{
						sourceRadii[radiusCount++] = Mathf.Abs(projectedDetectorRadius);
					}
					break;
				case RingProbeOutcome.Background:
					backgroundHits++;
					break;
				case RingProbeOutcome.Absorbed:
					absorbedHits++;
					break;
				default:
					missHits++;
					break;
			}
		}

		float radiusMean = 0f;
		float radiusStdDev = 0f;
		float radiusMin = 0f;
		float radiusMax = 0f;
		float radiusRange = 0f;
		string radiusVector = string.Empty;
		string radiusHistogram = string.Empty;
		if (radiusCount > 0)
		{
			radiusMin = sourceRadii[0];
			radiusMax = sourceRadii[0];
			for (int i = 0; i < radiusCount; i++)
			{
				float r = sourceRadii[i];
				radiusMean += r;
				if (r < radiusMin)
				{
					radiusMin = r;
				}
				if (r > radiusMax)
				{
					radiusMax = r;
				}
			}
			radiusMean /= radiusCount;
			float variance = 0f;
			for (int i = 0; i < radiusCount; i++)
			{
				float d = sourceRadii[i] - radiusMean;
				variance += d * d;
			}
			radiusStdDev = Mathf.Sqrt(variance / radiusCount);
			radiusRange = radiusMax - radiusMin;

			StringBuilder radiusBuilder = new StringBuilder(radiusCount * 10);
			for (int i = 0; i < radiusCount; i++)
			{
				if (i > 0)
				{
					radiusBuilder.Append(',');
				}
				radiusBuilder.Append(F(sourceRadii[i]));
			}
			radiusVector = radiusBuilder.ToString();
			radiusHistogram = BuildRadialHistogram(sourceRadii, radiusCount, radiusMin, radiusMax, histogramBins);
		}

		string outcomeVector = outcomeBuilder.ToString();
		string checksum = ComputeSha256Hex(outcomeVector + "|" + radiusVector);
		return new RingProbeSummary(
			sourceHits,
			backgroundHits,
			absorbedHits,
			missHits,
			radiusCount,
			radiusMean,
			radiusStdDev,
			radiusMin,
			radiusMax,
			radiusRange,
			radiusHistogram,
			outcomeVector,
			radiusVector,
			checksum);
	}

	private static string BuildRadialHistogram(float[] values, int count, float min, float max, int bins)
	{
		if (values == null || count <= 0 || bins <= 1 || !float.IsFinite(min) || !float.IsFinite(max))
		{
			return string.Empty;
		}

		int safeBins = Mathf.Clamp(bins, 2, 32);
		int[] histogram = new int[safeBins];
		float range = max - min;
		if (range <= 1e-6f)
		{
			histogram[0] = count;
		}
		else
		{
			float scale = safeBins / range;
			for (int i = 0; i < count; i++)
			{
				int index = Mathf.FloorToInt((values[i] - min) * scale);
				index = Mathf.Clamp(index, 0, safeBins - 1);
				histogram[index]++;
			}
		}

		StringBuilder builder = new StringBuilder(safeBins * 2);
		for (int i = 0; i < safeBins; i++)
		{
			if (i > 0)
			{
				builder.Append(',');
			}
			builder.Append(histogram[i]);
		}
		return builder.ToString();
	}

	private static RingProbeOutcome TraceRingProbeRay(
		Vector3 origin,
		RayBeamRenderer.FieldSourceSnap[] snaps,
		DetectorPlaneData detector,
		SourceMarkerSphere[] sources,
		Vector3 sourceOffset,
		float sourceRadiusOverride,
		float stepLength,
		float minStep,
		float maxStep,
		float stepAdaptGain,
		float bendScale,
		float fieldStrength,
		int steps,
		out float projectedDetectorRadius)
	{
		projectedDetectorRadius = float.NaN;
		Vector3 p = origin;
		Vector3 v = Vector3.Forward;

		for (int s = 0; s < steps; s++)
		{
			if (IsAbsorbedAtPoint(p, snaps))
			{
				return RingProbeOutcome.Absorbed;
			}

			Vector3 accel = ComputeTransportAcceleration(p, snaps, bendScale, fieldStrength, out float aLen);
			float step = Mathf.Clamp(stepLength / (1f + aLen * stepAdaptGain), minStep, maxStep);
			Vector3 nextVelocity = v + accel * step;
			if (nextVelocity.LengthSquared() > 1e-12f)
			{
				v = nextVelocity;
			}

			Vector3 pNext = p + v * step;
			bool hitSource = TryIntersectSegmentAnySource(
				p,
				pNext,
				sources,
				sourceOffset,
				sourceRadiusOverride,
				out float sourceT,
				out Vector3 sourceHitPoint);
			bool hitDetector = TryIntersectSegmentDetector(
				p,
				pNext,
				detector,
				out float detectorT,
				out Vector3 detectorHitPoint);

			if (hitSource && (!hitDetector || sourceT <= detectorT))
			{
				if (TryProjectPointToDetectorRadius(sourceHitPoint, v, detector, out float radius))
				{
					projectedDetectorRadius = radius;
				}
				return RingProbeOutcome.Source;
			}
			if (hitDetector)
			{
				projectedDetectorRadius = ComputeDetectorLocalRadius(detectorHitPoint, detector);
				return RingProbeOutcome.Background;
			}

			p = pNext;
		}

		return RingProbeOutcome.Miss;
	}

	private static Vector3 BuildRayDirectionFromNdc(Camera3D camera, Vector2 ndc, float aspect)
	{
		float fovY = Mathf.DegToRad(camera.Fov);
		float tanY = Mathf.Tan(fovY * 0.5f);
		float tanX = tanY * Mathf.Max(0.1f, aspect);
		Vector3 local = new Vector3(ndc.X * tanX, ndc.Y * tanY, -1f).Normalized();
		return (camera.GlobalTransform.Basis * local).Normalized();
	}

	private static bool TryIntersectSegmentSphere(
		Vector3 p0,
		Vector3 p1,
		Vector3 center,
		float radius,
		out float t,
		out Vector3 hitPoint)
	{
		t = 0f;
		hitPoint = Vector3.Zero;
		if (radius <= 0f)
		{
			return false;
		}

		Vector3 d = p1 - p0;
		float a = d.Dot(d);
		if (a <= 1e-12f)
		{
			return false;
		}

		Vector3 m = p0 - center;
		float b = 2f * m.Dot(d);
		float c = m.Dot(m) - (radius * radius);
		float disc = b * b - (4f * a * c);
		if (disc < 0f)
		{
			return false;
		}

		float sqrtDisc = Mathf.Sqrt(disc);
		float invDen = 1f / (2f * a);
		float t0 = (-b - sqrtDisc) * invDen;
		float t1 = (-b + sqrtDisc) * invDen;
		float best = float.PositiveInfinity;
		if (t0 >= 0f && t0 <= 1f)
		{
			best = t0;
		}
		if (t1 >= 0f && t1 <= 1f && t1 < best)
		{
			best = t1;
		}

		if (!float.IsFinite(best))
		{
			return false;
		}

		t = best;
		hitPoint = p0 + d * best;
		return true;
	}

	private static bool TryIntersectSegmentAnySource(
		Vector3 p0,
		Vector3 p1,
		SourceMarkerSphere[] sources,
		Vector3 sourceOffset,
		float radiusOverride,
		out float t,
		out Vector3 hitPoint)
	{
		t = 0f;
		hitPoint = Vector3.Zero;
		if (sources == null || sources.Length == 0)
		{
			return false;
		}

		bool hit = false;
		float bestT = float.PositiveInfinity;
		Vector3 bestPoint = Vector3.Zero;
		float safeOverride = Mathf.Max(0.01f, radiusOverride);
		for (int i = 0; i < sources.Length; i++)
		{
			Vector3 center = sources[i].WorldCenter + sourceOffset;
			float radius = safeOverride > 0f ? safeOverride : Mathf.Max(0.01f, sources[i].Radius);
			if (!TryIntersectSegmentSphere(p0, p1, center, radius, out float candidateT, out Vector3 candidatePoint))
			{
				continue;
			}

			if (candidateT < bestT)
			{
				hit = true;
				bestT = candidateT;
				bestPoint = candidatePoint;
			}
		}

		if (!hit)
		{
			return false;
		}

		t = bestT;
		hitPoint = bestPoint;
		return true;
	}

	private static bool TryIntersectSegmentDetector(
		Vector3 p0,
		Vector3 p1,
		DetectorPlaneData detector,
		out float t,
		out Vector3 hitPointWorld)
	{
		t = 0f;
		hitPointWorld = Vector3.Zero;

		Transform3D inv = detector.Transform.AffineInverse();
		Vector3 p0Local = inv * p0;
		Vector3 p1Local = inv * p1;
		Vector3 dLocal = p1Local - p0Local;
		if (Mathf.Abs(dLocal.Z) <= 1e-6f)
		{
			return false;
		}

		float hitT = -p0Local.Z / dLocal.Z;
		if (hitT < 0f || hitT > 1f)
		{
			return false;
		}

		Vector3 hitLocal = p0Local + dLocal * hitT;
		if (Mathf.Abs(hitLocal.X) > detector.HalfExtents.X || Mathf.Abs(hitLocal.Y) > detector.HalfExtents.Y)
		{
			return false;
		}

		t = hitT;
		hitPointWorld = detector.Transform * hitLocal;
		return true;
	}

	private static bool TryProjectPointToDetectorRadius(
		Vector3 worldPoint,
		Vector3 direction,
		DetectorPlaneData detector,
		out float radius)
	{
		radius = 0f;
		Transform3D inv = detector.Transform.AffineInverse();
		Vector3 pLocal = inv * worldPoint;
		Vector3 dirLocal = detector.Transform.Basis.Inverse() * direction;
		if (Mathf.Abs(dirLocal.Z) <= 1e-6f)
		{
			return false;
		}

		float t = -pLocal.Z / dirLocal.Z;
		if (t <= 0f)
		{
			return false;
		}

		Vector3 projected = pLocal + dirLocal * t;
		if (Mathf.Abs(projected.X) > detector.HalfExtents.X || Mathf.Abs(projected.Y) > detector.HalfExtents.Y)
		{
			return false;
		}

		radius = Mathf.Sqrt(projected.X * projected.X + projected.Y * projected.Y);
		return float.IsFinite(radius);
	}

	private static float ComputeDetectorLocalRadius(Vector3 worldPoint, DetectorPlaneData detector)
	{
		Vector3 local = detector.Transform.AffineInverse() * worldPoint;
		return Mathf.Sqrt(local.X * local.X + local.Y * local.Y);
	}

	private static string F(float value)
	{
		double rounded = Math.Round(value, FingerprintDecimals, MidpointRounding.AwayFromZero);
		return rounded.ToString($"F{FingerprintDecimals}", CultureInfo.InvariantCulture);
	}

	private static string FormatRoundedFloatVector(float[] values)
	{
		if (values == null || values.Length == 0)
		{
			return string.Empty;
		}

		string[] parts = new string[values.Length];
		for (int i = 0; i < values.Length; i++)
		{
			parts[i] = F(values[i]);
		}

		return string.Join(",", parts);
	}

	private static string FormatRoundedVec3Vector(Vector3[] values)
	{
		if (values == null || values.Length == 0)
		{
			return string.Empty;
		}

		string[] parts = new string[values.Length];
		for (int i = 0; i < values.Length; i++)
		{
			parts[i] = $"{F(values[i].X)},{F(values[i].Y)},{F(values[i].Z)}";
		}

		return string.Join("|", parts);
	}

	private static string ComputeSha256Hex(string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
		using SHA256 sha = SHA256.Create();
		byte[] hash = sha.ComputeHash(bytes);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}
}
