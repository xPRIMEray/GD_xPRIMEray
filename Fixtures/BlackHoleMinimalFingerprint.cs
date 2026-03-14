using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Godot;
using RendererCore.Config;
using RendererCore.Fields;

public partial class BlackHoleMinimalFingerprint : Node3D
{
	private static readonly NodePath FieldPath = new("FixtureBlackholeMinimal/FieldSource3D");
	private static readonly NodePath PhotonBandFieldPath = new("FixtureBlackholeMinimal/PhotonBandSource");
	private static readonly NodePath CameraPath = new("FixtureBlackholeMinimal/Camera3D");
	private static readonly NodePath SpherePath = new("FixtureBlackholeMinimal/blackhole_center_marker");
	private static readonly NodePath RendererPath = new("FixtureBlackholeMinimal/RayBeamRenderer");
	private static readonly NodePath SourceTemplatePath = new("FixtureBlackholeMinimal/source_marker_template");
	private static readonly NodePath DetectorPath = new("FixtureBlackholeMinimal/background_screen");
	private static readonly NodePath FilmCameraPath = new("GrinFilmCamera");
	[Export] public SourcePatternMode PatternMode = SourcePatternMode.CrossXY;
	[Export(PropertyHint.Range, "1,101,1")] public int SourceCountX = 25;
	[Export(PropertyHint.Range, "1,101,1")] public int SourceCountY = 25;
	[Export(PropertyHint.Range, "0,20,0.01")] public float SourceSpacingX = 4.50f;
	[Export(PropertyHint.Range, "0,20,0.01")] public float SourceSpacingY = 4.50f;
	[Export(PropertyHint.Range, "0.01,6,0.01")] public float SourceMarkerRadius = 0.60f;
	[Export] public Vector3 PatternOriginLocal = new(0f, 0f, -12f);
	[Export] public bool IncludeCenterPoint = true;

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

	private static readonly Vector2[] ShadowProbeNdc = BuildShadowProbePattern();

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
	private const TransportModel DefaultFixtureTransportModel = TransportModel.GRIN_Optical;
	private const string TransportArgPrefix = "--transport-model=";
	private const string FixtureTransportArgPrefix = "--blackhole-transport-model=";
	private const string PhotonBandArgPrefix = "--photon-band=";
	private const string FixturePhotonBandArgPrefix = "--blackhole-photon-band=";
	private const string FixturePhotonBandRInnerArgPrefix = "--blackhole-photon-band-r-inner=";
	private const string FixturePhotonBandROuterArgPrefix = "--blackhole-photon-band-r-outer=";
	private const string FixturePhotonBandAmpArgPrefix = "--blackhole-photon-band-amp=";
	private const string FixtureMassRInnerArgPrefix = "--blackhole-mass-r-inner=";
	private const string FixtureMassROuterArgPrefix = "--blackhole-mass-r-outer=";
	private const string FixtureMassAmpArgPrefix = "--blackhole-mass-amp=";
	private const string FixtureMassGammaArgPrefix = "--blackhole-mass-gamma=";
	private const string MetricDetectorScaleArgPrefix = "--blackhole-metric-detector-scale=";
	private const string MetricLargeDetectorArg = "--blackhole-metric-large-detector";
	private const string MetricTrajectoryDebugArg = "--blackhole-metric-trajectory-debug";
	private const float MetricLargeDetectorScale = 3.0f;

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
	private const int ShadowSilhouetteBins = 12;
	private const int ShadowAngularBins = 16;
	private const int PhotonRingAmplificationBins = 8;
	private const float ShadowProbeMaxRadius = 0.36f;
	private const float PhotonBandNoticeableAccelMin = 0.002f;
	private const float ProbeAccelChaosBound = 8.0f;
	private const float AccelClampMax = 50.0f;
	private const float PhotonRingAngularAmplificationMax = 24.0f;
	private const float PhotonRingMinTolerance = 0.08f;
	private const float PhotonRingMinLaunchAngle = 1e-3f;
	private const float PhotonRingMinRadialSign = 1e-4f;
	private const int MetricTrajectoryDebugSampleStride = 8;
	private const float MetricTrajectoryRearPlaneDistanceMin = 12.0f;
	private const float MetricTrajectoryRearPlaneDistanceScale = 2.5f;
	private const float MetricTrajectorySidePlaneYawDegrees = 35.0f;
	private const float MetricTrajectorySidePlaneShiftScale = 0.75f;
	private const float MetricTrajectorySidePlaneRearScale = 0.40f;
	private const float MetricTrajectoryAltPlaneExtentScale = 1.35f;

	private bool _invalidFixture;
	private bool _loggedBroadphaseLock;
	private Node3D _sourcePatternRoot;
	private SourceMarkerSphere[] _sourceMarkers = Array.Empty<SourceMarkerSphere>();
	private string _sourcePatternSummary = "mode=unknown;count=0";
	private const float ManualMarkerMeshScale = 1.18f;

	private enum PhotonBandFixtureMode
	{
		SceneAuthored,
		Disabled
	}

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

	private readonly struct PhotonBandCompareOverride
	{
		public readonly bool HasRInner;
		public readonly bool HasROuter;
		public readonly bool HasAmp;
		public readonly float RInner;
		public readonly float ROuter;
		public readonly float Amp;
		public readonly string Source;

		public PhotonBandCompareOverride(
			bool hasRInner,
			bool hasROuter,
			bool hasAmp,
			float rInner,
			float rOuter,
			float amp,
			string source)
		{
			HasRInner = hasRInner;
			HasROuter = hasROuter;
			HasAmp = hasAmp;
			RInner = rInner;
			ROuter = rOuter;
			Amp = amp;
			Source = source ?? "none";
		}

		public bool HasAny => HasRInner || HasROuter || HasAmp;
	}

	private readonly struct MassFieldCompareOverride
	{
		public readonly bool HasRInner;
		public readonly bool HasROuter;
		public readonly bool HasAmp;
		public readonly bool HasGamma;
		public readonly float RInner;
		public readonly float ROuter;
		public readonly float Amp;
		public readonly float Gamma;
		public readonly string Source;

		public MassFieldCompareOverride(
			bool hasRInner,
			bool hasROuter,
			bool hasAmp,
			bool hasGamma,
			float rInner,
			float rOuter,
			float amp,
			float gamma,
			string source)
		{
			HasRInner = hasRInner;
			HasROuter = hasROuter;
			HasAmp = hasAmp;
			HasGamma = hasGamma;
			RInner = rInner;
			ROuter = rOuter;
			Amp = amp;
			Gamma = gamma;
			Source = source ?? "none";
		}

		public bool HasAny => HasRInner || HasROuter || HasAmp || HasGamma;
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

	private readonly struct DetectorDiagnosticPlacement
	{
		public readonly string Name;
		public readonly DetectorPlaneData Plane;

		public DetectorDiagnosticPlacement(string name, DetectorPlaneData plane)
		{
			Name = name ?? string.Empty;
			Plane = plane;
		}
	}

	private readonly struct BlackHoleProbeSummary
	{
		public readonly int ProbeCount;
		public readonly int SourceHits;
		public readonly int BackgroundHits;
		public readonly int AbsorbedHits;
		public readonly int MissHits;
		public readonly float AbsorbRate;
		public readonly float HitRate;
		public readonly float AbsorbedRadiusMean;
		public readonly float AbsorbedRadiusStdDev;
		public readonly float AbsorbedRadiusMin;
		public readonly float AbsorbedRadiusMax;
		public readonly float AbsorbedRadiusRange;
		public readonly string AbsorbedRadiusHistogram;
		public readonly string AbsorbedAngularHistogram;
		public readonly float ShadowAreaFraction;
		public readonly float ShadowAngularCoverage;
		public readonly float ShadowRadiusAnisotropy;
		public readonly int OffscreenDetectorHits;
		public readonly int NoPlaneIntersectHits;
		public readonly float ClosestApproachMean;
		public readonly float ClosestApproachStdDev;
		public readonly float ClosestApproachRange;
		public readonly float ProjectedRadiusMean;
		public readonly float ProjectedRadiusStdDev;
		public readonly float ProjectedRadiusRange;
		public readonly int PhotonRingHits;
		public readonly float PhotonRingNearMissClosestApproach;
		public readonly int PhotonRingOver180DeflectionCount;
		public readonly int PhotonRingMultiPassCount;
		public readonly float PhotonRingAngularAmplificationMean;
		public readonly float PhotonRingAngularAmplificationMax;
		public readonly string PhotonRingAngularAmplificationHistogram;
		public readonly float PhotonRingRadiusEstimate;
		public readonly float PhotonRingRadiusStdDev;
		public readonly float DetectorScale;
		public readonly string MetricPathSummary;

		public BlackHoleProbeSummary(
			int probeCount,
			int sourceHits,
			int backgroundHits,
			int absorbedHits,
			int missHits,
			float absorbRate,
			float hitRate,
			float absorbedRadiusMean,
			float absorbedRadiusStdDev,
			float absorbedRadiusMin,
			float absorbedRadiusMax,
			float absorbedRadiusRange,
			string absorbedRadiusHistogram,
			string absorbedAngularHistogram,
			float shadowAreaFraction,
			float shadowAngularCoverage,
			float shadowRadiusAnisotropy,
			int offscreenDetectorHits,
			int noPlaneIntersectHits,
			float closestApproachMean,
			float closestApproachStdDev,
			float closestApproachRange,
			float projectedRadiusMean,
			float projectedRadiusStdDev,
			float projectedRadiusRange,
			int photonRingHits,
			float photonRingNearMissClosestApproach,
			int photonRingOver180DeflectionCount,
			int photonRingMultiPassCount,
			float photonRingAngularAmplificationMean,
			float photonRingAngularAmplificationMax,
			string photonRingAngularAmplificationHistogram,
			float photonRingRadiusEstimate,
			float photonRingRadiusStdDev,
			float detectorScale,
			string metricPathSummary)
		{
			ProbeCount = probeCount;
			SourceHits = sourceHits;
			BackgroundHits = backgroundHits;
			AbsorbedHits = absorbedHits;
			MissHits = missHits;
			AbsorbRate = absorbRate;
			HitRate = hitRate;
			AbsorbedRadiusMean = absorbedRadiusMean;
			AbsorbedRadiusStdDev = absorbedRadiusStdDev;
			AbsorbedRadiusMin = absorbedRadiusMin;
			AbsorbedRadiusMax = absorbedRadiusMax;
			AbsorbedRadiusRange = absorbedRadiusRange;
			AbsorbedRadiusHistogram = absorbedRadiusHistogram ?? string.Empty;
			AbsorbedAngularHistogram = absorbedAngularHistogram ?? string.Empty;
			ShadowAreaFraction = shadowAreaFraction;
			ShadowAngularCoverage = shadowAngularCoverage;
			ShadowRadiusAnisotropy = shadowRadiusAnisotropy;
			OffscreenDetectorHits = offscreenDetectorHits;
			NoPlaneIntersectHits = noPlaneIntersectHits;
			ClosestApproachMean = closestApproachMean;
			ClosestApproachStdDev = closestApproachStdDev;
			ClosestApproachRange = closestApproachRange;
			ProjectedRadiusMean = projectedRadiusMean;
			ProjectedRadiusStdDev = projectedRadiusStdDev;
			ProjectedRadiusRange = projectedRadiusRange;
			PhotonRingHits = photonRingHits;
			PhotonRingNearMissClosestApproach = photonRingNearMissClosestApproach;
			PhotonRingOver180DeflectionCount = photonRingOver180DeflectionCount;
			PhotonRingMultiPassCount = photonRingMultiPassCount;
			PhotonRingAngularAmplificationMean = photonRingAngularAmplificationMean;
			PhotonRingAngularAmplificationMax = photonRingAngularAmplificationMax;
			PhotonRingAngularAmplificationHistogram = photonRingAngularAmplificationHistogram ?? string.Empty;
			PhotonRingRadiusEstimate = photonRingRadiusEstimate;
			PhotonRingRadiusStdDev = photonRingRadiusStdDev;
			DetectorScale = detectorScale;
			MetricPathSummary = metricPathSummary ?? string.Empty;
		}
	}

	public override void _Ready()
	{
		TransportModel fixtureTransportModel = ResolveFixtureTransportModel(out string transportSource);
		PhotonBandFixtureMode photonBandMode = ResolvePhotonBandFixtureMode(out string photonBandModeSource);
		PhotonBandCompareOverride photonBandOverride = ResolvePhotonBandCompareOverride();
		MassFieldCompareOverride massFieldOverride = ResolveMassFieldCompareOverride();
		LogStartupVariant(fixtureTransportModel, transportSource);
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

		MaybeApplyTransportOverride(massField, fixtureTransportModel, transportSource);
		MaybeApplyTransportOverride(photonBandField, fixtureTransportModel, transportSource);
		ApplyMassFieldOverride(massField, massFieldOverride);
		PhotonBandShell photonBandShell = ResolveEffectivePhotonBandShell(photonBandOverride);

		FieldSource3D.ResolvedFieldParams massResolved = massField.ResolveEffectiveParams(out string massResolveReason);
		FieldSource3D.ResolvedFieldParams bandResolved = photonBandField.ResolveEffectiveParams(out string bandResolveReason);
		FieldSource3D.ResolvedFieldParams effectiveBandResolved = BuildEffectivePhotonBandResolved(bandResolved, photonBandMode, photonBandShell, photonBandOverride);
		LogFieldStartupSummary("mass", massField, massResolved, massResolveReason, transportSource, massFieldOverride.HasAny);
		LogFieldStartupSummary("photon_band", photonBandField, bandResolved, bandResolveReason, transportSource);
		LogPhotonBandMode(photonBandMode, photonBandModeSource, bandResolved.enabled, effectiveBandResolved.enabled);
		WarnUnexpectedLegacyFallback(massField, massResolveReason);
		WarnUnexpectedLegacyFallback(photonBandField, bandResolveReason);
		RayBeamRenderer.FieldSourceSnap massSnap = RayBeamRenderer.BuildFieldSourceSnap(massField);
		RayBeamRenderer.FieldSourceSnap bandSnap = BuildEffectivePhotonBandSnap(
			RayBeamRenderer.BuildFieldSourceSnap(photonBandField),
			photonBandMode,
			photonBandShell,
			photonBandOverride);
		RayBeamRenderer.FieldSourceSnap[] snaps = BuildSourceArray(massSnap, bandSnap);
		TransportModel activeTransportModel = RayBeamRenderer.ResolveActiveTransportModel(snaps);
		bool photonBandActive = IsPhotonBandActive(effectiveBandResolved);
		string effectiveBandSource = ResolveEffectivePhotonBandSource(bandResolveReason, photonBandOverride);

		string massResolvedSummary = BuildResolvedSummary("mass", massResolveReason, massResolved);
		string bandResolvedSummary = BuildResolvedSummary("band", effectiveBandSource, effectiveBandResolved);
		bool massCanonical = IsCanonicalResolve(massResolveReason);
		bool bandCanonical = !photonBandActive || IsCanonicalResolve(bandResolveReason) || photonBandOverride.HasAny;
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
		Node3D sphere = GetNodeOrNull<Node3D>(SpherePath);
		StaticBody3D detectorPlane = GetNodeOrNull<StaticBody3D>(DetectorPath);
		EnforceFixtureBroadphasePolicy(filmCamera);
		float detectorScale = MaybeApplyMetricDetectorScaleOverride(detectorPlane, activeTransportModel);

		float[] massProbeAccels = new float[CurvatureProbeOffsets.Length];
		float[] bandProbeAccels = new float[CurvatureProbeOffsets.Length];
		float[] totalProbeAccels = new float[CurvatureProbeOffsets.Length];
		float massProbeSum = 0f;
		float bandProbeSum = 0f;
		float totalProbeSum = 0f;
		float maxProbeAccel = 0f;
		bool anyProbeAboveEps = false;
		bool bandNoticeable = !photonBandActive;
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

		float maxOuter = photonBandActive ? Mathf.Max(massSnap.ROuter, bandSnap.ROuter) : massSnap.ROuter;
		float farDistance = Mathf.Max(maxOuter + 16f, 20f);
		Vector3 farProbe = massField.GlobalPosition + new Vector3(0f, 0f, farDistance);
		float farAccelMag = ComputeCombinedRawAcceleration(farProbe, snaps).Length();

		bool hasRenderer = rayRenderer != null;
		bool useIntegrated = hasRenderer && rayRenderer.UseIntegratedField;
		float bendScale = hasRenderer ? rayRenderer.BendScale : 0f;
		float fieldStrength = hasRenderer ? rayRenderer.FieldStrength : 0f;
		float transportStrength = Mathf.Abs(bendScale * fieldStrength);
		float effectiveMetricScalar = hasRenderer
			? RayBeamRenderer.ComputeMetricWeakFieldScalarForActiveModel(snaps, FixedBetaScale, bendScale, fieldStrength, activeTransportModel)
			: 0f;
		GD.Print(
			$"[Transport] active={activeTransportModel} fixture=blackhole_minimal effectiveMetricScalar={effectiveMetricScalar:0.######}");
		string rendererSummary = BuildRendererSummary(hasRenderer, useIntegrated, bendScale, fieldStrength, transportStrength);
		float expectedMassRInner = massFieldOverride.HasRInner ? massFieldOverride.RInner : FixedRInner;
		float expectedMassROuter = massFieldOverride.HasROuter ? massFieldOverride.ROuter : FixedROuter;
		float expectedMassAmp = massFieldOverride.HasAmp ? massFieldOverride.Amp : FixedAmp;
		float expectedMassGamma = massFieldOverride.HasGamma ? massFieldOverride.Gamma : FixedGamma;

		bool canonicalMatchesMass =
			massResolved.curveType == FixedCurveType &&
			Mathf.IsEqualApprox(massResolved.rInner, expectedMassRInner) &&
			Mathf.IsEqualApprox(massResolved.rOuter, expectedMassROuter) &&
			Mathf.IsEqualApprox(massResolved.amp, expectedMassAmp) &&
			Mathf.IsEqualApprox(massResolved.a, expectedMassGamma) &&
			massResolved.overrideBetaScale == FixedOverrideBetaScale &&
			Mathf.IsEqualApprox(massResolved.betaScale, FixedBetaScale) &&
			massResolved.modeFlags == FixedModeFlags;

		bool canonicalMatchesBand = !photonBandActive || (
			effectiveBandResolved.enabled &&
			effectiveBandResolved.curveType == FixedPhotonBandCurveType &&
			Mathf.IsEqualApprox(effectiveBandResolved.rInner, photonBandShell.RInner) &&
			Mathf.IsEqualApprox(effectiveBandResolved.rOuter, photonBandShell.ROuter) &&
			Mathf.IsEqualApprox(effectiveBandResolved.amp, photonBandOverride.HasAmp ? photonBandOverride.Amp : FixedPhotonBandAmp) &&
			Mathf.IsEqualApprox(effectiveBandResolved.a, FixedPhotonBandCurveA) &&
			Mathf.IsEqualApprox(effectiveBandResolved.b, FixedPhotonBandCurveB) &&
			Mathf.IsEqualApprox(effectiveBandResolved.c, FixedPhotonBandCurveC) &&
			effectiveBandResolved.overrideBetaScale == FixedPhotonBandOverrideBetaScale &&
			Mathf.IsEqualApprox(effectiveBandResolved.betaScale, FixedPhotonBandBetaScale) &&
			effectiveBandResolved.modeFlags == FixedPhotonBandModeFlags);

		bool massPrimary = !photonBandActive || (
			massResolved.amp > effectiveBandResolved.amp &&
			massProbeSum > bandProbeSum);
		bool bounded = maxProbeAccel <= ProbeAccelChaosBound;
		bool requireBandNoticeable = !photonBandOverride.HasAny;

		if (!canonicalMatchesMass || !canonicalMatchesBand || !massPrimary || (!bandNoticeable && requireBandNoticeable) ||
			!bounded || !anyProbeAboveEps || farAccelMag > FarAccelEpsilon || !useIntegrated || transportStrength <= CurvatureAccelEpsilon)
		{
			FailInvalid(
				"curvature not engaged",
				$"mass_match={(canonicalMatchesMass ? 1 : 0)} band_match={(canonicalMatchesBand ? 1 : 0)} mass_primary={(massPrimary ? 1 : 0)} " +
				$"band_noticeable={(bandNoticeable ? 1 : 0)} require_band_noticeable={(requireBandNoticeable ? 1 : 0)} bounded={(bounded ? 1 : 0)} {rendererSummary} " +
				$"{massResolvedSummary} {bandResolvedSummary} " +
				$"probe_mass=[{FormatFloatArray(massProbeAccels)}] probe_band=[{FormatFloatArray(bandProbeAccels)}] probe_total=[{FormatFloatArray(totalProbeAccels)}] " +
				$"far_accel={farAccelMag:0.######}");
			return;
		}

		GD.Print(
			$"[BlackHoleFixture][CurvatureEngaged] mass_match=1 band_match=1 probe_any=1 band_noticeable={(bandNoticeable ? 1 : 0)} require_band_noticeable={(requireBandNoticeable ? 1 : 0)} mass_primary=1 " +
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
			$"[BlackHoleFixture][CurvatureApplied] deviated_rays={deviatedRayCount}/{RayProbeNdc.Length} " +
			$"max_dev={maxDeviation:0.######} dev_sum={deviationSum:0.######} min_dev_threshold={CurvatureDeviationMin:0.######}");

		float curvatureEnergy = totalProbeSum * transportStrength;
		GD.Print(
			$"[BlackHoleFixture][CurvatureMetric] accel_sum_total={totalProbeSum:0.######} accel_sum_mass={massProbeSum:0.######} accel_sum_band={bandProbeSum:0.######} " +
			$"curvature_energy={curvatureEnergy:0.######} max_probe_accel={maxProbeAccel:0.######} far_accel={farAccelMag:0.######}");
		if (filmCamera != null && filmCamera.SmartScaleEnabled)
		{
			GD.Print(
				$"[BlackHoleFixture][SmartScaleCurvature] smartscale=1 accel_sum_total={totalProbeSum:0.######} accel_sum_mass={massProbeSum:0.######} accel_sum_band={bandProbeSum:0.######} " +
				$"curvature_energy={curvatureEnergy:0.######} max_probe_accel={maxProbeAccel:0.######} far_accel={farAccelMag:0.######}");
		}
		if (!RebuildSourcePatternMarkers())
		{
			return;
		}
		_sourcePatternSummary = BuildSourcePatternSummaryToken();
		ApplyHudMetadata(filmCamera, rayRenderer, activeTransportModel);
		LogSourcePatternSummary();
		LogSourcePatternMarkerDetails();
		BlackHoleProbeSummary probeSummary = RunBlackHoleProbeSummary(
			rayRenderer,
			snaps,
			massSnap.Center,
			massSnap.ROuter,
			photonBandShell.Center,
			Mathf.Max(PhotonRingMinTolerance, photonBandShell.Width * 0.5f),
			Mathf.Clamp(rayRenderer.StepsPerRay, 64, CurvatureRayStepCap),
			_sourceMarkers,
			detectorPlane,
			detectorScale);
		RayBeamRenderer.MetricTransportDiagnosticsSnapshot metricDiagnostics = RunMetricTransportDiagnosticProbe(
			rayRenderer,
			filmCamera,
			camera,
			snaps,
			massSnap.Center,
			massSnap.ROuter,
			Mathf.Clamp(rayRenderer.StepsPerRay, 64, CurvatureRayStepCap));
		GD.Print(
			$"[BlackHoleFixture][Absorption] transportModel={activeTransportModel} effectiveMetricScalar={effectiveMetricScalar:0.######} " +
			$"absorbed_rays={probeSummary.AbsorbedHits}/{probeSummary.ProbeCount} absorb_rate={probeSummary.AbsorbRate:0.###}");
		GD.Print(
			$"[BlackHoleFixture][MetricDiagnostics] metricLaw={metricDiagnostics.MetricSteeringLawToken} metricDeltaZeroCount={metricDiagnostics.MetricDeltaZeroCount} " +
			$"metricDeltaNonzeroCount={metricDiagnostics.MetricDeltaNonzeroCount} metricFallbackCount={metricDiagnostics.MetricFallbackCount} " +
			$"metricContributionRatio={metricDiagnostics.MetricContributionRatio:0.######} zeroReasons={metricDiagnostics.ZeroReasonSummary}");
		GD.Print(
			$"[MetricIsolation] metricLaw={metricDiagnostics.MetricSteeringLawToken} metricDirectSteps={metricDiagnostics.MetricDirectSteps} gridBypassSteps={metricDiagnostics.GridBypassSteps} " +
			$"grinFallbackSteps={metricDiagnostics.GrinFallbackSteps} grinScalarDominatedSteps={metricDiagnostics.GrinScalarDominatedSteps}");
		GD.Print(
			$"[TransportSteering] transportModel={activeTransportModel} metricLaw={metricDiagnostics.MetricSteeringLawToken} meanTurn={metricDiagnostics.MeanTurn:0.######} " +
			$"maxTurn={metricDiagnostics.MaxTurn:0.######} radialBins=[{metricDiagnostics.RadialTurnSummary}]");
		GD.Print(
			$"[BlackHoleFixture][PhotonBandContribution] mode={PhotonBandModeToToken(photonBandMode)} source={photonBandModeSource} enabled={(photonBandActive ? 1 : 0)} " +
			$"probeAccelSum={bandProbeSum:0.######} probeAccelShare={(totalProbeSum > CurvatureAccelEpsilon ? bandProbeSum / totalProbeSum : 0f):0.######} farAccel={(photonBandActive ? ComputeCombinedRawAcceleration(farProbe, new[] { bandSnap }).Length() : 0f):0.######}");
		GD.Print(
			$"[BlackHoleFixture][ComparisonSummary] transportModel={activeTransportModel} metricLaw={metricDiagnostics.MetricSteeringLawToken} effectiveMetricScalar={effectiveMetricScalar:0.######} " +
			$"photonBandMode={PhotonBandModeToToken(photonBandMode)} photonBandModeSource={photonBandModeSource} photonBandEnabled={(photonBandActive ? 1 : 0)} photonBandRInner={effectiveBandResolved.rInner:0.######} photonBandROuter={effectiveBandResolved.rOuter:0.######} photonBandAmp={effectiveBandResolved.amp:0.######} photonBandProbeAccelSum={bandProbeSum:0.######} photonBandProbeAccelShare={(totalProbeSum > CurvatureAccelEpsilon ? bandProbeSum / totalProbeSum : 0f):0.######} " +
			$"sourcePatternSummary={_sourcePatternSummary} probeN={probeSummary.ProbeCount} absorbCount={probeSummary.AbsorbedHits} absorbRate={probeSummary.AbsorbRate:0.######} " +
			$"hitRate={probeSummary.HitRate:0.######} sourceHits={probeSummary.SourceHits} backgroundHits={probeSummary.BackgroundHits} detectorHits={probeSummary.BackgroundHits} " +
			$"absorbedHits={probeSummary.AbsorbedHits} missHits={probeSummary.MissHits} offscreenDetectorHits={probeSummary.OffscreenDetectorHits} noPlaneIntersect={probeSummary.NoPlaneIntersectHits} " +
			$"closestApproachMean={probeSummary.ClosestApproachMean:0.######} projectedRadiusMean={probeSummary.ProjectedRadiusMean:0.######} detectorScale={probeSummary.DetectorScale:0.###} silhouetteRadiusMean={probeSummary.AbsorbedRadiusMean:0.######} " +
			$"silhouetteRadiusStdDev={probeSummary.AbsorbedRadiusStdDev:0.######} silhouetteRadiusMin={probeSummary.AbsorbedRadiusMin:0.######} silhouetteRadiusMax={probeSummary.AbsorbedRadiusMax:0.######} silhouetteRadiusRange={probeSummary.AbsorbedRadiusRange:0.######} " +
			$"shadowAreaFraction={probeSummary.ShadowAreaFraction:0.######} shadowAngularCoverage={probeSummary.ShadowAngularCoverage:0.######} shadowRadiusAnisotropy={probeSummary.ShadowRadiusAnisotropy:0.######} " +
			$"photonRingHits={probeSummary.PhotonRingHits} photonRingNearMissClosestApproach={probeSummary.PhotonRingNearMissClosestApproach:0.######} " +
			$"photonRingOver180DeflectionCount={probeSummary.PhotonRingOver180DeflectionCount} photonRingMultiPassCount={probeSummary.PhotonRingMultiPassCount} " +
			$"photonRingAngularAmplificationMean={probeSummary.PhotonRingAngularAmplificationMean:0.######} photonRingAngularAmplificationMax={probeSummary.PhotonRingAngularAmplificationMax:0.######} " +
			$"photonRingRadiusEstimate={probeSummary.PhotonRingRadiusEstimate:0.######} photonRingRadiusStdDev={probeSummary.PhotonRingRadiusStdDev:0.######} " +
			$"metricDeltaZeroCount={metricDiagnostics.MetricDeltaZeroCount} metricDeltaNonzeroCount={metricDiagnostics.MetricDeltaNonzeroCount} " +
			$"metricFallbackCount={metricDiagnostics.MetricFallbackCount} metricContributionRatio={metricDiagnostics.MetricContributionRatio:0.######} " +
			$"silhouetteHistogram=[{probeSummary.AbsorbedRadiusHistogram}] absorbedAngularHistogram=[{probeSummary.AbsorbedAngularHistogram}] " +
			$"photonRingAngularAmplificationHistogram=[{probeSummary.PhotonRingAngularAmplificationHistogram}]");
		if (activeTransportModel == TransportModel.Metric_NullGeodesic)
		{
			GD.Print(probeSummary.MetricPathSummary);
		}

		string sphereScale = sphere != null ? FormatVec3(sphere.Scale) : "n/a";
		string spherePos = sphere != null ? FormatVec3(sphere.GlobalTransform.Origin) : "n/a";
		string camFov = camera != null ? $"{camera.Fov:0.###}" : "n/a";
		string camPos = camera != null ? FormatVec3(camera.GlobalTransform.Origin) : "n/a";
		string camToSphere = (camera != null && sphere != null)
			? $"{camera.GlobalTransform.Origin.DistanceTo(sphere.GlobalTransform.Origin):0.###}"
			: "n/a";

		GD.Print(
			$"[BlackHoleFixture] mass_strength={massResolved.amp:0.###} mass_radius={massResolved.rOuter:0.###} " +
			$"band_strength={effectiveBandResolved.amp:0.###} band_r_inner={effectiveBandResolved.rInner:0.###} band_r_outer={effectiveBandResolved.rOuter:0.###} " +
			$"mass_node_path={massField.GetPath()} band_node_path={photonBandField.GetPath()} sphere_global_pos={spherePos} sphere_scale={sphereScale} " +
			$"cam_fov={camFov} camera_global_pos={camPos} cam_to_sphere={camToSphere}");

		GD.Print(
			$"[BlackHoleFixture][ResolvedMass] source={massResolveReason} curve={massResolved.curveType} modeFlags={massResolved.modeFlags} " +
			$"rInner={massResolved.rInner:0.###} rOuter={massResolved.rOuter:0.###} amp={massResolved.amp:0.###} " +
			$"gamma={massResolved.a:0.###} beta_mode={(massResolved.overrideBetaScale ? "override" : "global")} beta_scale={massResolved.betaScale:0.###}");

		GD.Print(
			$"[BlackHoleFixture][ResolvedPhotonBand] source={effectiveBandSource} mode={PhotonBandModeToToken(photonBandMode)} enabled={(effectiveBandResolved.enabled ? 1 : 0)} curve={effectiveBandResolved.curveType} modeFlags={effectiveBandResolved.modeFlags} " +
			$"rInner={effectiveBandResolved.rInner:0.###} rOuter={effectiveBandResolved.rOuter:0.###} amp={effectiveBandResolved.amp:0.###} " +
			$"A={effectiveBandResolved.a:0.###} B={effectiveBandResolved.b:0.###} C={effectiveBandResolved.c:0.###} " +
			$"beta_mode={(effectiveBandResolved.overrideBetaScale ? "override" : "global")} beta_scale={effectiveBandResolved.betaScale:0.###}");

		GD.Print(
			$"[BlackHoleFixture][Renderer] useIntegrated={(useIntegrated ? 1 : 0)} bendScale={bendScale:0.###} fieldStrength={fieldStrength:0.###}");

		string fingerprint = BuildBlackHoleMinimalFingerprint();
		GD.Print($"BlackHoleMinimalFingerprint: {fingerprint}");
		string fingerprintHash = ExtractFingerprintHash(fingerprint);
		GD.Print(
			$"[FixtureCompare] fixture=blackhole_minimal transport={activeTransportModel} photonBand={(photonBandActive ? "ON" : "OFF")} " +
			$"photonBandMode={PhotonBandModeToToken(photonBandMode)} photonBandRInner={effectiveBandResolved.rInner:0.######} photonBandROuter={effectiveBandResolved.rOuter:0.######} photonBandAmp={effectiveBandResolved.amp:0.######} absorbCount={probeSummary.AbsorbedHits} " +
			$"absorbRate={probeSummary.AbsorbRate:0.######} hitRate={probeSummary.HitRate:0.######} " +
			$"sourceHits={probeSummary.SourceHits} backgroundHits={probeSummary.BackgroundHits} " +
			$"absorbedHits={probeSummary.AbsorbedHits} missHits={probeSummary.MissHits} " +
			$"silhouetteRadiusMean={probeSummary.AbsorbedRadiusMean:0.######} " +
			$"silhouetteRadiusStdDev={probeSummary.AbsorbedRadiusStdDev:0.######} " +
			$"silhouetteRadiusMin={probeSummary.AbsorbedRadiusMin:0.######} " +
			$"silhouetteRadiusMax={probeSummary.AbsorbedRadiusMax:0.######} " +
			$"silhouetteRadiusRange={probeSummary.AbsorbedRadiusRange:0.######} " +
			$"shadowAreaFraction={probeSummary.ShadowAreaFraction:0.######} " +
			$"shadowAngularCoverage={probeSummary.ShadowAngularCoverage:0.######} " +
			$"shadowRadiusAnisotropy={probeSummary.ShadowRadiusAnisotropy:0.######} " +
			$"photonRingHits={probeSummary.PhotonRingHits} " +
			$"photonRingNearMissClosestApproach={probeSummary.PhotonRingNearMissClosestApproach:0.######} " +
			$"photonRingOver180DeflectionCount={probeSummary.PhotonRingOver180DeflectionCount} " +
			$"photonRingMultiPassCount={probeSummary.PhotonRingMultiPassCount} " +
			$"photonRingAngularAmplificationMean={probeSummary.PhotonRingAngularAmplificationMean:0.######} " +
			$"photonRingRadiusEstimate={probeSummary.PhotonRingRadiusEstimate:0.######} " +
			$"metricContributionRatio={metricDiagnostics.MetricContributionRatio:0.######} " +
			$"sourcePatternMode={PatternMode} sourcePatternCount={_sourceMarkers.Length} fingerprint={fingerprintHash}");
	}

	public override void _Process(double delta)
	{
		EnforceFixtureBroadphasePolicy(GetNodeOrNull<GrinFilmCamera>(FilmCameraPath));
	}

	public string BuildBlackHoleMinimalFingerprint()
	{
		FieldSource3D massField = GetNodeOrNull<FieldSource3D>(FieldPath);
		FieldSource3D photonBandField = GetNodeOrNull<FieldSource3D>(PhotonBandFieldPath);
		RayBeamRenderer rayRenderer = GetNodeOrNull<RayBeamRenderer>(RendererPath);
		if (massField == null || photonBandField == null || rayRenderer == null)
		{
			return
				$"error=missing_nodes mass_field={(massField != null ? 1 : 0)} band_field={(photonBandField != null ? 1 : 0)} renderer={(rayRenderer != null ? 1 : 0)}";
		}

		TransportModel fixtureTransportModel = ResolveFixtureTransportModel(out string transportSource);
		PhotonBandFixtureMode photonBandMode = ResolvePhotonBandFixtureMode(out string photonBandModeSource);
		PhotonBandCompareOverride photonBandOverride = ResolvePhotonBandCompareOverride();
		MassFieldCompareOverride massFieldOverride = ResolveMassFieldCompareOverride();
		MaybeApplyTransportOverride(massField, fixtureTransportModel, transportSource);
		MaybeApplyTransportOverride(photonBandField, fixtureTransportModel, transportSource);
		ApplyMassFieldOverride(massField, massFieldOverride);
		PhotonBandShell photonBandShell = ResolveEffectivePhotonBandShell(photonBandOverride);

		FieldSource3D.ResolvedFieldParams massResolved = massField.ResolveEffectiveParams(out string massResolveReason);
		FieldSource3D.ResolvedFieldParams bandResolved = photonBandField.ResolveEffectiveParams(out string bandResolveReason);
		FieldSource3D.ResolvedFieldParams effectiveBandResolved = BuildEffectivePhotonBandResolved(bandResolved, photonBandMode, photonBandShell, photonBandOverride);
		RayBeamRenderer.FieldSourceSnap massSnap = RayBeamRenderer.BuildFieldSourceSnap(massField);
		RayBeamRenderer.FieldSourceSnap bandSnap = BuildEffectivePhotonBandSnap(
			RayBeamRenderer.BuildFieldSourceSnap(photonBandField),
			photonBandMode,
			photonBandShell,
			photonBandOverride);
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
		TransportModel activeTransportModel = RayBeamRenderer.ResolveActiveTransportModel(snaps);
		string metricSteeringLaw = RayBeamRenderer.GetMetricSteeringLawToken(rayRenderer.GetEffectiveMetricSteeringLaw());
		float effectiveMetricScalar = RayBeamRenderer.ComputeMetricWeakFieldScalarForActiveModel(
			snaps,
			FixedBetaScale,
			rayRenderer.BendScale,
			rayRenderer.FieldStrength,
			activeTransportModel);
		string sourcePatternSummary = BuildSourcePatternSummaryToken();
		BlackHoleProbeSummary probeSummary = RunBlackHoleProbeSummary(
			rayRenderer,
			snaps,
			massSnap.Center,
			massSnap.ROuter,
			photonBandShell.Center,
			Mathf.Max(PhotonRingMinTolerance, photonBandShell.Width * 0.5f),
			Mathf.Clamp(rayRenderer.StepsPerRay, 64, CurvatureRayStepCap),
			_sourceMarkers,
			GetNodeOrNull<StaticBody3D>(DetectorPath),
			ResolveMetricDetectorScaleOverride());

		string effectiveMassSource = ResolveEffectiveMassFieldSource(massResolveReason, massFieldOverride);
		string fingerprintCore =
			$"v=4;" +
			$"sourceCount={(IsPhotonBandActive(effectiveBandResolved) ? 2 : 1)};" +
			$"bandMode={PhotonBandModeToToken(photonBandMode)};" +
			$"bandModeSource={photonBandModeSource};" +
			$"massCanonical={(IsCanonicalResolve(massResolveReason) ? 1 : 0)};" +
			$"bandCanonical={(IsPhotonBandActive(effectiveBandResolved) && IsCanonicalResolve(bandResolveReason) ? 1 : 0)};" +
			$"massSource={effectiveMassSource};" +
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
			$"bandEnabled={(effectiveBandResolved.enabled ? 1 : 0)};" +
			$"bandSource={ResolveEffectivePhotonBandSource(bandResolveReason, photonBandOverride)};" +
			$"bandCurve={effectiveBandResolved.curveType};" +
			$"bandRInner={F(effectiveBandResolved.rInner)};" +
			$"bandROuter={F(effectiveBandResolved.rOuter)};" +
			$"bandAmp={F(effectiveBandResolved.amp)};" +
			$"bandA={F(effectiveBandResolved.a)};" +
			$"bandB={F(effectiveBandResolved.b)};" +
			$"bandC={F(effectiveBandResolved.c)};" +
			$"bandModeFlags={effectiveBandResolved.modeFlags};" +
			$"bandTransport={bandSnap.TransportModel};" +
			$"bandBetaMode={(effectiveBandResolved.overrideBetaScale ? "override" : "global")};" +
			$"bandBetaScale={F(effectiveBandResolved.betaScale)};" +
			$"useIntegrated={(rayRenderer.UseIntegratedField ? 1 : 0)};" +
			$"bendScale={F(rayRenderer.BendScale)};" +
			$"fieldStrength={F(rayRenderer.FieldStrength)};" +
			$"stepLength={F(rayRenderer.StepLength)};" +
			$"minStep={F(Mathf.Min(rayRenderer.MinStepLength, rayRenderer.MaxStepLength))};" +
			$"maxStep={F(Mathf.Max(rayRenderer.MinStepLength, rayRenderer.MaxStepLength))};" +
			$"stepAdaptGain={F(rayRenderer.StepAdaptGain)};" +
			$"maxSteps={rayRenderer.StepsPerRay};" +
			$"transportModel={activeTransportModel};" +
			$"metricLaw={metricSteeringLaw};" +
			$"effectiveMetricScalar={F(effectiveMetricScalar)};" +
			$"sourcePatternSummary={sourcePatternSummary};" +
			$"sourcePatternMode={PatternMode};" +
			$"sourcePatternCount={_sourceMarkers.Length};" +
			$"sourceSpacingX={F(Mathf.Max(0f, SourceSpacingX))};" +
			$"sourceSpacingY={F(Mathf.Max(0f, SourceSpacingY))};" +
			$"sourceMarkerRadius={F(Mathf.Max(0.01f, SourceMarkerRadius))};" +
			$"probeN={probeSummary.ProbeCount};" +
			$"absorbCount={probeSummary.AbsorbedHits};" +
			$"absorbRate={F(probeSummary.AbsorbRate)};" +
			$"hitRate={F(probeSummary.HitRate)};" +
			$"sourceHits={probeSummary.SourceHits};" +
			$"backgroundHits={probeSummary.BackgroundHits};" +
			$"detectorHits={probeSummary.BackgroundHits};" +
			$"absorbedHits={probeSummary.AbsorbedHits};" +
			$"missHits={probeSummary.MissHits};" +
			$"offscreenDetectorHits={probeSummary.OffscreenDetectorHits};" +
			$"noPlaneIntersect={probeSummary.NoPlaneIntersectHits};" +
			$"closestApproachMean={F(probeSummary.ClosestApproachMean)};" +
			$"projectedRadiusMean={F(probeSummary.ProjectedRadiusMean)};" +
			$"detectorScale={F(probeSummary.DetectorScale)};" +
			$"silhouetteRadiusMean={F(probeSummary.AbsorbedRadiusMean)};" +
			$"silhouetteRadiusStdDev={F(probeSummary.AbsorbedRadiusStdDev)};" +
			$"silhouetteRadiusMin={F(probeSummary.AbsorbedRadiusMin)};" +
			$"silhouetteRadiusMax={F(probeSummary.AbsorbedRadiusMax)};" +
			$"silhouetteRadiusRange={F(probeSummary.AbsorbedRadiusRange)};" +
			$"shadowAreaFraction={F(probeSummary.ShadowAreaFraction)};" +
			$"shadowAngularCoverage={F(probeSummary.ShadowAngularCoverage)};" +
			$"shadowRadiusAnisotropy={F(probeSummary.ShadowRadiusAnisotropy)};" +
			$"photonRingHits={probeSummary.PhotonRingHits};" +
			$"photonRingNearMissClosestApproach={F(probeSummary.PhotonRingNearMissClosestApproach)};" +
			$"photonRingOver180DeflectionCount={probeSummary.PhotonRingOver180DeflectionCount};" +
			$"photonRingMultiPassCount={probeSummary.PhotonRingMultiPassCount};" +
			$"photonRingAngularAmplificationMean={F(probeSummary.PhotonRingAngularAmplificationMean)};" +
			$"photonRingAngularAmplificationMax={F(probeSummary.PhotonRingAngularAmplificationMax)};" +
			$"photonRingAngularAmplificationHistogram=[{probeSummary.PhotonRingAngularAmplificationHistogram}];" +
			$"photonRingRadiusEstimate={F(probeSummary.PhotonRingRadiusEstimate)};" +
			$"photonRingRadiusStdDev={F(probeSummary.PhotonRingRadiusStdDev)};" +
			$"silhouetteHistogram=[{probeSummary.AbsorbedRadiusHistogram}];" +
			$"absorbedAngularHistogram=[{probeSummary.AbsorbedAngularHistogram}];" +
			$"rayK={FingerprintRaySteps};" +
			$"accelMass=[{massAccelVector}];" +
			$"accelBand=[{bandAccelVector}];" +
			$"accelTotal=[{totalAccelVector}];" +
			$"rayChecksum={rayEndpointChecksum}";

		string fingerprintHash = ComputeSha256Hex(fingerprintCore);
		GD.Print(
			$"BlackHoleMinimalFingerprintRaw: transportModel={activeTransportModel} metricLaw={metricSteeringLaw} effectiveMetricScalar={effectiveMetricScalar:0.######} " +
			$"sourcePatternSummary={sourcePatternSummary} sourceHits={probeSummary.SourceHits} backgroundHits={probeSummary.BackgroundHits} " +
			$"detectorHits={probeSummary.BackgroundHits} absorbedHits={probeSummary.AbsorbedHits} missHits={probeSummary.MissHits} " +
			$"offscreenDetectorHits={probeSummary.OffscreenDetectorHits} noPlaneIntersect={probeSummary.NoPlaneIntersectHits} closestApproachMean={probeSummary.ClosestApproachMean:0.######} projectedRadiusMean={probeSummary.ProjectedRadiusMean:0.######} detectorScale={probeSummary.DetectorScale:0.######} " +
			$"absorbCount={probeSummary.AbsorbedHits} absorbRate={probeSummary.AbsorbRate:0.######} hitRate={probeSummary.HitRate:0.######} " +
			$"silhouetteRadiusMean={probeSummary.AbsorbedRadiusMean:0.######} silhouetteRadiusStdDev={probeSummary.AbsorbedRadiusStdDev:0.######} " +
			$"silhouetteRadiusMin={probeSummary.AbsorbedRadiusMin:0.######} silhouetteRadiusMax={probeSummary.AbsorbedRadiusMax:0.######} silhouetteRadiusRange={probeSummary.AbsorbedRadiusRange:0.######} " +
			$"shadowAreaFraction={probeSummary.ShadowAreaFraction:0.######} shadowAngularCoverage={probeSummary.ShadowAngularCoverage:0.######} shadowRadiusAnisotropy={probeSummary.ShadowRadiusAnisotropy:0.######} " +
			$"photonRingHits={probeSummary.PhotonRingHits} photonRingNearMissClosestApproach={probeSummary.PhotonRingNearMissClosestApproach:0.######} " +
			$"photonRingOver180DeflectionCount={probeSummary.PhotonRingOver180DeflectionCount} photonRingMultiPassCount={probeSummary.PhotonRingMultiPassCount} " +
			$"photonRingAngularAmplificationMean={probeSummary.PhotonRingAngularAmplificationMean:0.######} photonRingAngularAmplificationMax={probeSummary.PhotonRingAngularAmplificationMax:0.######} " +
			$"photonRingRadiusEstimate={probeSummary.PhotonRingRadiusEstimate:0.######} photonRingRadiusStdDev={probeSummary.PhotonRingRadiusStdDev:0.######} " +
			$"silhouetteHistogram=[{probeSummary.AbsorbedRadiusHistogram}] absorbedAngularHistogram=[{probeSummary.AbsorbedAngularHistogram}] photonRingAngularAmplificationHistogram=[{probeSummary.PhotonRingAngularAmplificationHistogram}] " +
			$"accelMass=[{massAccelVector}] accelBand=[{bandAccelVector}] accelTotal=[{totalAccelVector}] " +
			$"rayEndpoints=[{rayEndpointVector}] rayChecksum={rayEndpointChecksum}");
		return $"{fingerprintCore};sha256={fingerprintHash}";
	}

	private static bool IsCanonicalResolve(string resolveReason)
	{
		return string.Equals(resolveReason, "canonical", StringComparison.Ordinal);
	}

	private static string ExtractFingerprintHash(string fingerprint)
	{
		if (string.IsNullOrWhiteSpace(fingerprint))
		{
			return "n/a";
		}

		const string marker = ";sha256=";
		int markerIndex = fingerprint.LastIndexOf(marker, StringComparison.Ordinal);
		if (markerIndex < 0)
		{
			return fingerprint;
		}

		int hashIndex = markerIndex + marker.Length;
		return hashIndex < fingerprint.Length
			? fingerprint.Substring(hashIndex)
			: "n/a";
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
			GD.Print("[BlackHoleFixture][Broadphase] enforcing policy=OverlapOnly mode=Policy");
		}
	}

	private void MaybeApplyTransportOverride(FieldSource3D field, TransportModel transportModel, string transportSource)
	{
		if (field == null || !IsCmdlineTransportSource(transportSource) || field.TransportModel == transportModel)
		{
			return;
		}

		TransportModel previous = field.TransportModel;
		field.TransportModel = transportModel;
		GD.Print(
			$"[FixtureStartup][TransportOverride] fixture=blackhole_minimal node={field.Name} from={previous} to={transportModel} source={transportSource}");
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

	private static PhotonBandShell ResolveEffectivePhotonBandShell(PhotonBandCompareOverride compareOverride)
	{
		PhotonBandShell canonical = ComputePhotonBandShell(FixedROuter, FixedRInner);
		if (!compareOverride.HasAny)
		{
			return canonical;
		}

		float rInner = compareOverride.HasRInner ? compareOverride.RInner : canonical.RInner;
		float minOuter = rInner + 0.05f;
		float rOuter = compareOverride.HasROuter ? compareOverride.ROuter : canonical.ROuter;
		rOuter = Mathf.Max(minOuter, rOuter);
		float width = Mathf.Max(rOuter - rInner, 0.05f);
		float center = rInner + (width * 0.5f);
		return new PhotonBandShell(center, width, rInner, rOuter);
	}

	private static string ResolveEffectivePhotonBandSource(string resolveReason, PhotonBandCompareOverride compareOverride)
	{
		if (!compareOverride.HasAny)
		{
			return resolveReason;
		}

		return $"compare_override({compareOverride.Source})";
	}

	private void LogFieldStartupSummary(
		string label,
		FieldSource3D field,
		FieldSource3D.ResolvedFieldParams resolved,
		string resolveReason,
		string transportSource,
		bool hasFieldOverride = false)
	{
		string resolvedSource = ResolveFieldParamSource(resolveReason, transportSource, hasFieldOverride);
		bool legacyActive = field.HasActiveLegacyFallbackInputs(out string legacyActiveReason);
		bool legacyIgnored = field.HasIgnoredLegacyInputs(out string legacyIgnoredReason);
		string legacyState = legacyActive
			? $"active({legacyActiveReason})"
			: legacyIgnored ? $"ignored({legacyIgnoredReason})" : "inactive";
		string curveParams = resolved.curveType switch
		{
			FieldCurveType.Power => $" gamma={resolved.a:0.###}",
			FieldCurveType.Polynomial => $" a={resolved.a:0.###} b={resolved.b:0.###} c={resolved.c:0.###}",
			FieldCurveType.Exponential => $" sigma={resolved.sigma:0.###}",
			_ => string.Empty
		};

		GD.Print(
			$"[FixtureField] fixture=blackhole_minimal node={field.Name} label={label} source={resolvedSource} transport={field.TransportModel} " +
			$"curve={resolved.curveType} rInnerEnabled={(field.CanonicalEnableInnerRadius ? 1 : 0)} rInner={resolved.rInner:0.###} rOuter={resolved.rOuter:0.###} " +
			$"amp={resolved.amp:0.###} betaMode={(resolved.overrideBetaScale ? "override" : "global")} betaScale={resolved.betaScale:0.###}{curveParams} legacy={legacyState}");
	}

	private void WarnUnexpectedLegacyFallback(FieldSource3D field, string resolveReason)
	{
		if (!IsLegacyResolve(resolveReason) || field == null || field.IsCanonicalUnset())
		{
			return;
		}

		GD.PushWarning(
			$"[FixtureStartup][WARN] fixture=blackhole_minimal node={field.Name} resolved_from=legacy_fallback despite scene-authored canonical values.");
	}

	private static string ResolveFieldParamSource(string resolveReason, string transportSource, bool hasFieldOverride = false)
	{
		if (IsLegacyResolve(resolveReason))
		{
			return "legacy_fallback";
		}

		return hasFieldOverride || IsCmdlineTransportSource(transportSource) ? "cmdline_override" : "scene_baseline";
	}

	private static bool IsCmdlineTransportSource(string transportSource)
	{
		return string.Equals(transportSource, "cmdline", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(transportSource, "cmdline_user", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsLegacyResolve(string resolveReason)
	{
		return !string.IsNullOrWhiteSpace(resolveReason) &&
			resolveReason.IndexOf("legacy", StringComparison.OrdinalIgnoreCase) >= 0;
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

	private static BlackHoleProbeSummary RunBlackHoleProbeSummary(
		RayBeamRenderer rayRenderer,
		RayBeamRenderer.FieldSourceSnap[] snaps,
		Vector3 launchCenter,
		float launchOuterRadius,
		float photonSphereRadius,
		float photonSphereTolerance,
		int steps,
		SourceMarkerSphere[] markers,
		StaticBody3D detectorPlane,
		float detectorScale)
	{
		int probeCount = ShadowProbeNdc.Length;
		if (snaps == null || snaps.Length == 0 || rayRenderer == null)
		{
			return new BlackHoleProbeSummary(
				probeCount,
				0,
				0,
				0,
				probeCount,
				0f,
				0f,
				0f,
				0f,
				0f,
				0f,
				0f,
				string.Empty,
				string.Empty,
				0f,
				0f,
				0f,
				0,
				probeCount,
				0f,
				0f,
				0f,
				float.NaN,
				0f,
				0f,
				0,
				0f,
				0,
				0,
				0f,
				0f,
				string.Empty,
				0f,
				0f,
				detectorScale,
				string.Empty);
		}

		int sourceHits = 0;
		int backgroundHits = 0;
		int absorbedHits = 0;
		int missHits = 0;
		int offscreenDetectorHits = 0;
		int noPlaneIntersectHits = 0;
		float[] absorbedRadii = new float[probeCount];
		int absorbedRadiiCount = 0;
		float[] closestApproachRadii = new float[probeCount];
		int closestApproachCount = 0;
		float[] projectedRadii = new float[probeCount];
		int projectedRadiiCount = 0;
		float[] photonRingAmplifications = new float[probeCount];
		int photonRingAmplificationCount = 0;
		float[] photonRingDetectorRadii = new float[probeCount];
		int photonRingDetectorRadiusCount = 0;
		Vector3[] noPlaneFinalDirs = new Vector3[probeCount];
		Vector3[] noPlaneFinalPositions = new Vector3[probeCount];
		float[] noPlaneClosestApproach = new float[probeCount];
		float[] noPlanePlaneSignedDistances = new float[probeCount];
		float[] absorbedAngularRadiiSums = new float[ShadowAngularBins];
		int[] absorbedAngularHistogram = new int[ShadowAngularBins];
		int[] absorbedAngularRadiusCounts = new int[ShadowAngularBins];
		int noPlaneSummaryCount = 0;
		int noPlaneFacingHalfSpaceCount = 0;
		int noPlaneExitedHalfSpaceCount = 0;
		int photonRingHits = 0;
		int photonRingOver180DeflectionCount = 0;
		int photonRingMultiPassCount = 0;
		float photonRingNearMissClosestApproach = float.PositiveInfinity;
		float stepLength = Mathf.Max(0.0001f, rayRenderer.StepLength);
		float minStep = Mathf.Max(0.0001f, Mathf.Min(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float maxStep = Mathf.Max(minStep, Mathf.Max(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float stepAdaptGain = Mathf.Max(0f, rayRenderer.StepAdaptGain);
		float bendScale = rayRenderer.BendScale;
		float fieldStrength = rayRenderer.FieldStrength;
		float launchZ = -Mathf.Max(0.25f, launchOuterRadius * 0.5f);
		float launchXY = Mathf.Max(0.1f, launchOuterRadius * 0.2f);
		float escapeDistance = Mathf.Max(launchOuterRadius + 16f, 20f);
		Vector3 baseOrigin = launchCenter + new Vector3(0f, 0f, launchZ);
		Vector3 marchDirection = Vector3.Back;
		DetectorPlaneData detectorData = BuildDetectorPlaneData(detectorPlane);
		DetectorDiagnosticPlacement[] alternatePlanes = BuildAlternateDetectorPlacements(detectorData, launchOuterRadius);
		int[] alternatePlaneHits = alternatePlanes.Length > 0 ? new int[alternatePlanes.Length] : Array.Empty<int>();
		bool debugTrajectories = ResolveMetricTrajectoryDebugEnabled();

		for (int i = 0; i < probeCount; i++)
		{
			Vector2 ndc = ShadowProbeNdc[i];
			Vector3 p = baseOrigin + new Vector3(ndc.X * launchXY, ndc.Y * launchXY, 0f);
			Vector3 v = marchDirection;
			float launchPlaneSignedDistance = GetSignedDistanceToDetectorPlane(detectorData, p);
			bool resolved = false;
			bool crossedDetectorPlane = false;
			float closestApproach = p.DistanceTo(launchCenter);
			float cumulativeTurn = 0f;
			Vector3 previousDirection = v.Normalized();
			float previousRadialSign = ComputeRadialTravelSign(p, v, launchCenter);
			int radialSignChanges = 0;
			float launchAngle = Mathf.Max(
				PhotonRingMinLaunchAngle,
				Mathf.Atan2(ndc.Length() * launchXY, Mathf.Abs(launchZ)));
			bool[] altPlaneHitForRay = alternatePlanes.Length > 0 ? new bool[alternatePlanes.Length] : Array.Empty<bool>();
			StringBuilder trajectorySample = null;
			if (debugTrajectories && ShouldCaptureMetricTrajectorySample(i))
			{
				trajectorySample = new StringBuilder(320);
				AppendTrajectorySamplePoint(trajectorySample, p);
			}
			for (int s = 0; s < steps; s++)
			{
				if (IsAbsorbedAtPoint(p, snaps))
				{
					absorbedHits++;
					float absorbedRadius = Mathf.Sqrt(ndc.X * ndc.X + ndc.Y * ndc.Y);
					absorbedRadii[absorbedRadiiCount++] = absorbedRadius;
					int angularBin = ComputeAngularBin(ndc, ShadowAngularBins);
					absorbedAngularHistogram[angularBin]++;
					absorbedAngularRadiiSums[angularBin] += absorbedRadius;
					absorbedAngularRadiusCounts[angularBin]++;
					closestApproachRadii[closestApproachCount++] = closestApproach;
					resolved = true;
					break;
				}

				Vector3 accel = ComputeTransportAcceleration(p, snaps, bendScale, fieldStrength, out float aLen);
				float step = Mathf.Clamp(stepLength / (1f + aLen * stepAdaptGain), minStep, maxStep);
				Vector3 nextVelocity = v + accel * step;
				if (nextVelocity.LengthSquared() > 1e-12f)
				{
					v = nextVelocity;
				}
				Vector3 nextP = p + v * step;
				Vector3 currentDirection = v.LengthSquared() > 1e-12f ? v.Normalized() : previousDirection;
				cumulativeTurn += SafeAngleBetween(previousDirection, currentDirection);
				previousDirection = currentDirection;
				float currentRadialSign = ComputeRadialTravelSign(nextP, v, launchCenter);
				if (previousRadialSign != 0f && currentRadialSign != 0f && previousRadialSign != currentRadialSign)
				{
					radialSignChanges++;
				}
				if (currentRadialSign != 0f)
				{
					previousRadialSign = currentRadialSign;
				}
				float sourceT = float.PositiveInfinity;
				bool hitSource = markers != null && markers.Length > 0 &&
					TryIntersectSegmentAnySource(p, nextP, markers, out sourceT);
				bool hitDetector = TryIntersectSegmentDetector(
					p,
					nextP,
					detectorData,
					out float detectorT,
					out float detectorRadius,
					out bool detectorInBounds);
				for (int altIndex = 0; altIndex < alternatePlanes.Length; altIndex++)
				{
					if (altPlaneHitForRay[altIndex])
					{
						continue;
					}

					if (TryIntersectSegmentDetector(
						p,
						nextP,
						alternatePlanes[altIndex].Plane,
						out _,
						out _,
						out bool altInBounds) && altInBounds)
					{
						altPlaneHitForRay[altIndex] = true;
					}
				}
				closestApproach = Mathf.Min(closestApproach, DistancePointToSegment(launchCenter, p, nextP));
				if (hitDetector || detectorT >= 0f)
				{
					crossedDetectorPlane = true;
				}
				if (hitSource && (!hitDetector || sourceT <= detectorT))
				{
					sourceHits++;
					resolved = true;
					break;
				}
				if (hitDetector && detectorInBounds)
				{
					backgroundHits++;
					if (IsPhotonRingCandidate(closestApproach, photonSphereRadius, photonSphereTolerance))
					{
						photonRingHits++;
						photonRingNearMissClosestApproach = Mathf.Min(photonRingNearMissClosestApproach, closestApproach);
						if (cumulativeTurn > Mathf.Pi)
						{
							photonRingOver180DeflectionCount++;
						}
						if (radialSignChanges >= 2)
						{
							photonRingMultiPassCount++;
						}
						photonRingAmplifications[photonRingAmplificationCount++] = cumulativeTurn / launchAngle;
						if (float.IsFinite(detectorRadius))
						{
							photonRingDetectorRadii[photonRingDetectorRadiusCount++] = detectorRadius;
						}
					}
					resolved = true;
					break;
				}
				if (detectorT >= 0f && !detectorInBounds)
				{
					offscreenDetectorHits++;
					closestApproachRadii[closestApproachCount++] = closestApproach;
					if (float.IsFinite(detectorRadius))
					{
						projectedRadii[projectedRadiiCount++] = detectorRadius;
					}
					resolved = true;
					break;
				}
				p = nextP;
				if (trajectorySample != null && ((s + 1) % MetricTrajectoryDebugSampleStride == 0 || s == steps - 1))
				{
					AppendTrajectorySamplePoint(trajectorySample, p);
				}
			}

			if (resolved)
			{
				continue;
			}

			if (!crossedDetectorPlane)
			{
				noPlaneIntersectHits++;
				closestApproachRadii[closestApproachCount++] = closestApproach;
				Vector3 finalDir = v.LengthSquared() > 1e-12f ? v.Normalized() : Vector3.Zero;
				float finalPlaneSignedDistance = GetSignedDistanceToDetectorPlane(detectorData, p);
				noPlaneFinalDirs[noPlaneSummaryCount] = finalDir;
				noPlaneFinalPositions[noPlaneSummaryCount] = p;
				noPlaneClosestApproach[noPlaneSummaryCount] = closestApproach;
				noPlanePlaneSignedDistances[noPlaneSummaryCount] = finalPlaneSignedDistance;
				if (IsSameDetectorHalfSpace(launchPlaneSignedDistance, finalPlaneSignedDistance))
				{
					noPlaneFacingHalfSpaceCount++;
				}
				else
				{
					noPlaneExitedHalfSpaceCount++;
				}
				for (int altIndex = 0; altIndex < alternatePlanes.Length; altIndex++)
				{
					if (altPlaneHitForRay[altIndex])
					{
						alternatePlaneHits[altIndex]++;
					}
				}
				if (trajectorySample != null)
				{
					GD.Print(
						$"[BlackHoleMetricTrajectorySample] i={i} ndc={FormatVec2(ndc)} finalPos={FormatVec3(p)} " +
						$"finalDir={FormatVec3(finalDir)} signedPlaneDist={finalPlaneSignedDistance:0.###} closestApproach={closestApproach:0.###} " +
						$"polyline={trajectorySample}");
				}
				noPlaneSummaryCount++;
			}
			missHits++;
		}

		float absorbRate = probeCount > 0 ? absorbedHits / (float)probeCount : 0f;
		float hitRate = probeCount > 0 ? sourceHits / (float)probeCount : 0f;
		ComputeRadiusStats(absorbedRadii, absorbedRadiiCount, out float mean, out float stdDev, out float minRadius, out float maxRadius, out float range);
		ComputeRadiusStats(closestApproachRadii, closestApproachCount, out float closestMean, out float closestStdDev, out _, out _, out float closestRange);
		ComputeRadiusStats(projectedRadii, projectedRadiiCount, out float projectedMean, out float projectedStdDev, out _, out _, out float projectedRange);
		ComputeVector3Stats(noPlaneFinalDirs, noPlaneSummaryCount, out Vector3 finalDirMean, out Vector3 finalDirSpread);
		ComputeVector3Stats(noPlaneFinalPositions, noPlaneSummaryCount, out Vector3 finalPosMean, out _);
		ComputeRadiusStats(noPlaneClosestApproach, noPlaneSummaryCount, out float noPlaneClosestMean, out _, out _, out _, out _);
		ComputeRadiusStats(noPlanePlaneSignedDistances, noPlaneSummaryCount, out float planeSignedDistanceMean, out float planeSignedDistanceSpread, out _, out _, out _);
		ComputeRadiusStats(
			photonRingAmplifications,
			photonRingAmplificationCount,
			out float photonRingAmplificationMean,
			out _,
			out _,
			out float photonRingAmplificationMax,
			out _);
		ComputeRadiusStats(
			photonRingDetectorRadii,
			photonRingDetectorRadiusCount,
			out float photonRingRadiusEstimate,
			out float photonRingRadiusStdDev,
			out _,
			out _,
			out _);
		string histogram = BuildRadialHistogram(absorbedRadii, absorbedRadiiCount, ShadowSilhouetteBins, 0f, ShadowProbeMaxRadius);
		string angularHistogram = BuildIntHistogram(absorbedAngularHistogram);
		string photonRingAmplificationHistogram = BuildRadialHistogram(
			photonRingAmplifications,
			photonRingAmplificationCount,
			PhotonRingAmplificationBins,
			0f,
			PhotonRingAngularAmplificationMax);
		float shadowAreaFraction = ComputeShadowAreaFraction(ShadowProbeNdc, probeCount, absorbedHits);
		float shadowAngularCoverage = ComputeAngularCoverage(absorbedAngularHistogram, ShadowAngularBins);
		float shadowRadiusAnisotropy = ComputeAngularRadiusAnisotropy(absorbedAngularRadiiSums, absorbedAngularRadiusCounts);
		string altPlaneSummary = BuildAlternatePlaneHitSummary(alternatePlanes, alternatePlaneHits, noPlaneSummaryCount);
		string metricPathSummary =
			$"[BlackHoleMetricPath] detectorScale={detectorScale:0.###} absorbed={absorbedHits} offscreenDetectorHits={offscreenDetectorHits} " +
			$"noPlaneIntersect={noPlaneIntersectHits} closestApproachMean={closestMean:0.######} projectedRadiusMean={projectedMean:0.######}";
		if (noPlaneSummaryCount > 0)
		{
			metricPathSummary =
				$"[BlackHoleMetricTrajectory] absorbed={absorbedHits} noPlaneIntersect={noPlaneIntersectHits} " +
				$"finalDirMean={FormatVec3(finalDirMean)} finalDirSpread={FormatVec3(finalDirSpread)} " +
				$"planeDistMean={planeSignedDistanceMean:0.###} planeDistSpread={planeSignedDistanceSpread:0.###} " +
				$"halfSpaceFacing={noPlaneFacingHalfSpaceCount}/{noPlaneSummaryCount} exited={noPlaneExitedHalfSpaceCount} " +
				$"closestApproachMean={noPlaneClosestMean:0.######} finalPosMean={FormatVec3(finalPosMean)} " +
				$"{altPlaneSummary}";
		}
		if (!float.IsFinite(photonRingNearMissClosestApproach))
		{
			photonRingNearMissClosestApproach = 0f;
		}
		metricPathSummary +=
			$" photonRingHits={photonRingHits} photonRingNearMissClosestApproach={photonRingNearMissClosestApproach:0.######} " +
			$"photonRingOver180DeflectionCount={photonRingOver180DeflectionCount} photonRingMultiPassCount={photonRingMultiPassCount} " +
			$"photonRingAngularAmplificationMean={photonRingAmplificationMean:0.######} photonRingRadiusEstimate={photonRingRadiusEstimate:0.######} " +
			$"photonRingAngularAmplificationHistogram=[{photonRingAmplificationHistogram}]";
		return new BlackHoleProbeSummary(
			probeCount,
			sourceHits,
			backgroundHits,
			absorbedHits,
			missHits,
			absorbRate,
			hitRate,
			mean,
			stdDev,
			minRadius,
			maxRadius,
			range,
			histogram,
			angularHistogram,
			shadowAreaFraction,
			shadowAngularCoverage,
			shadowRadiusAnisotropy,
			offscreenDetectorHits,
			noPlaneIntersectHits,
			closestMean,
			closestStdDev,
			closestRange,
			projectedMean,
			projectedStdDev,
			projectedRange,
			photonRingHits,
			photonRingNearMissClosestApproach,
			photonRingOver180DeflectionCount,
			photonRingMultiPassCount,
			photonRingAmplificationMean,
			photonRingAmplificationMax,
			photonRingAmplificationHistogram,
			photonRingRadiusEstimate,
			photonRingRadiusStdDev,
			detectorScale,
			metricPathSummary);
	}

	private static FieldGrid3D BuildMetricDiagnosticFieldGrid(
		GrinFilmCamera filmCamera,
		Camera3D camera,
		RayBeamRenderer rayRenderer,
		RayBeamRenderer.FieldSourceSnap[] snaps,
		Vector3 launchCenter,
		float launchOuterRadius)
	{
		if (filmCamera == null || !filmCamera.UseFieldGrid || rayRenderer == null || snaps == null || snaps.Length == 0)
		{
			return null;
		}

		float cellSize = Mathf.Max(0.001f, filmCamera.FieldGridCellSize);
		float escapeDistance = Mathf.Max(launchOuterRadius + 16f, 20f);
		float radius = Mathf.Max(0.01f, escapeDistance + filmCamera.FieldGridBoundsPadding);
		Vector3 gridCenter = camera != null ? camera.GlobalPosition : launchCenter;
		Vector3 half = new Vector3(radius, radius, radius);
		Aabb bounds = new Aabb(gridCenter - half, half * 2f);

		var fieldGrid = new FieldGrid3D();
		fieldGrid.BuildFromSources(
			snaps,
			FixedBetaScale,
			FixedGamma,
			rayRenderer.BendScale,
			rayRenderer.FieldStrength,
			bounds,
			cellSize);
		return fieldGrid;
	}

	private static RayBeamRenderer.MetricTransportDiagnosticsSnapshot RunMetricTransportDiagnosticProbe(
		RayBeamRenderer rayRenderer,
		GrinFilmCamera filmCamera,
		Camera3D camera,
		RayBeamRenderer.FieldSourceSnap[] snaps,
		Vector3 launchCenter,
		float launchOuterRadius,
		int steps)
	{
		if (rayRenderer == null)
		{
			return default;
		}

		rayRenderer.ResetMetricTransportDiagnostics();
		if (snaps == null || snaps.Length == 0 || steps <= 0)
		{
			return rayRenderer.GetMetricTransportDiagnosticsSnapshot();
		}

		bool hasSources = true;
		FieldGrid3D fieldGrid = BuildMetricDiagnosticFieldGrid(filmCamera, camera, rayRenderer, snaps, launchCenter, launchOuterRadius);
		float minStep = Mathf.Max(0.0001f, Mathf.Min(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float maxStep = Mathf.Max(minStep, Mathf.Max(rayRenderer.MinStepLength, rayRenderer.MaxStepLength));
		float launchZ = -Mathf.Max(0.25f, launchOuterRadius * 0.5f);
		float launchXY = Mathf.Max(0.1f, launchOuterRadius * 0.2f);
		float escapeDistance = Mathf.Max(launchOuterRadius + 16f, 20f);
		Vector3 baseOrigin = launchCenter + new Vector3(0f, 0f, launchZ);
		Vector3 marchDirection = Vector3.Back;

		for (int i = 0; i < RayProbeNdc.Length; i++)
		{
			Vector2 ndc = RayProbeNdc[i];
			Vector3 p = baseOrigin + new Vector3(ndc.X * launchXY, ndc.Y * launchXY, 0f);
			Vector3 v = marchDirection;
			float traveled = 0f;
			for (int s = 0; s < steps; s++)
			{
				if (IsAbsorbedAtPoint(p, snaps) || p.DistanceTo(launchCenter) >= escapeDistance)
				{
					break;
				}

				if (!rayRenderer.TryStepActiveTransportForDiagnostics(
					p,
					v,
					launchCenter,
					FixedBetaScale,
					FixedGamma,
					snaps,
					hasSources,
					escapeDistance,
					traveled,
					minStep,
					maxStep,
					fieldGrid,
					out Vector3 next,
					out Vector3 nextVelocity,
					out float step))
				{
					break;
				}

				traveled += step;
				p = next;
				v = nextVelocity;
			}
		}

		return rayRenderer.GetMetricTransportDiagnosticsSnapshot();
	}

	private static bool TryIntersectSegmentAnySource(Vector3 p0, Vector3 p1, SourceMarkerSphere[] markers, out float t)
	{
		t = 0f;
		if (markers == null || markers.Length == 0)
		{
			return false;
		}

		float bestT = float.PositiveInfinity;
		bool hit = false;
		for (int i = 0; i < markers.Length; i++)
		{
			SourceMarkerSphere marker = markers[i];
			if (TryIntersectSegmentSphere(p0, p1, marker.WorldCenter, Mathf.Max(0.01f, marker.Radius), out float candidateT))
			{
				hit = true;
				if (candidateT < bestT)
				{
					bestT = candidateT;
				}
			}
		}
		if (!hit)
		{
			return false;
		}
		t = bestT;
		return true;
	}

	private static DetectorPlaneData BuildDetectorPlaneData(StaticBody3D detector)
	{
		Vector2 halfExtents = new Vector2(40f, 40f);
		Transform3D transform = Transform3D.Identity;
		if (detector == null)
		{
			return new DetectorPlaneData(transform, halfExtents);
		}

		transform = detector.GlobalTransform;
		CollisionShape3D collisionShape = detector.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (collisionShape?.Shape is BoxShape3D box)
		{
			Vector3 s = detector.Scale;
			halfExtents = new Vector2(
				Mathf.Abs(box.Size.X * 0.5f * (Mathf.Abs(s.X) > 0f ? Mathf.Abs(s.X) : 1f)),
				Mathf.Abs(box.Size.Y * 0.5f * (Mathf.Abs(s.Y) > 0f ? Mathf.Abs(s.Y) : 1f)));
		}
		return new DetectorPlaneData(transform, halfExtents);
	}

	private static DetectorDiagnosticPlacement[] BuildAlternateDetectorPlacements(DetectorPlaneData basePlane, float launchOuterRadius)
	{
		float rearOffset = Mathf.Max(MetricTrajectoryRearPlaneDistanceMin, launchOuterRadius * MetricTrajectoryRearPlaneDistanceScale);
		Vector3 normal = basePlane.Transform.Basis.Z.Normalized();
		Vector3 lateral = basePlane.Transform.Basis.X.Normalized();

		Transform3D rearTransform = basePlane.Transform;
		rearTransform.Origin -= normal * rearOffset;

		Transform3D sideTransform = basePlane.Transform;
		sideTransform.Basis = (sideTransform.Basis * new Basis(Vector3.Up, Mathf.DegToRad(MetricTrajectorySidePlaneYawDegrees))).Orthonormalized();
		sideTransform.Origin += lateral * (rearOffset * MetricTrajectorySidePlaneShiftScale) - normal * (rearOffset * MetricTrajectorySidePlaneRearScale);

		return new[]
		{
			new DetectorDiagnosticPlacement(
				"rearFar",
				new DetectorPlaneData(rearTransform, basePlane.HalfExtents)),
			new DetectorDiagnosticPlacement(
				"angledSide",
				new DetectorPlaneData(sideTransform, basePlane.HalfExtents * MetricTrajectoryAltPlaneExtentScale))
		};
	}

	private static float GetSignedDistanceToDetectorPlane(DetectorPlaneData detector, Vector3 point)
	{
		Transform3D inv = detector.Transform.AffineInverse();
		Vector3 localPoint = inv * point;
		return localPoint.Z;
	}

	private static bool IsSameDetectorHalfSpace(float launchSignedDistance, float finalSignedDistance)
	{
		if (Mathf.Abs(launchSignedDistance) <= 1e-5f || Mathf.Abs(finalSignedDistance) <= 1e-5f)
		{
			return true;
		}

		return Mathf.Sign(launchSignedDistance) == Mathf.Sign(finalSignedDistance);
	}

	private static bool TryIntersectSegmentDetector(
		Vector3 p0,
		Vector3 p1,
		DetectorPlaneData detector,
		out float t,
		out float detectorRadius,
		out bool inBounds)
	{
		t = -1f;
		detectorRadius = float.NaN;
		inBounds = false;

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
		detectorRadius = Mathf.Sqrt(hitLocal.X * hitLocal.X + hitLocal.Y * hitLocal.Y);
		t = hitT;
		inBounds = Mathf.Abs(hitLocal.X) <= detector.HalfExtents.X && Mathf.Abs(hitLocal.Y) <= detector.HalfExtents.Y;
		return inBounds;
	}

	private static bool TryIntersectSegmentSphere(Vector3 p0, Vector3 p1, Vector3 center, float radius, out float t)
	{
		t = 0f;
		if (radius <= 0f)
		{
			return false;
		}

		Vector3 d = p1 - p0;
		float a = d.Dot(d);
		if (a <= 1e-8f)
		{
			return false;
		}

		Vector3 m = p0 - center;
		float b = 2f * m.Dot(d);
		float c = m.Dot(m) - (radius * radius);
		float disc = b * b - 4f * a * c;
		if (disc < 0f)
		{
			return false;
		}

		float sqrtDisc = Mathf.Sqrt(disc);
		float invDenom = 0.5f / a;
		float t0 = (-b - sqrtDisc) * invDenom;
		float t1 = (-b + sqrtDisc) * invDenom;
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
		return true;
	}

	private static void ComputeRadiusStats(
		float[] values,
		int count,
		out float mean,
		out float stdDev,
		out float min,
		out float max,
		out float range)
	{
		mean = 0f;
		stdDev = 0f;
		min = 0f;
		max = 0f;
		range = 0f;
		if (values == null || count <= 0)
		{
			return;
		}

		min = values[0];
		max = values[0];
		for (int i = 0; i < count; i++)
		{
			float v = values[i];
			mean += v;
			if (v < min) min = v;
			if (v > max) max = v;
		}
		mean /= count;

		float variance = 0f;
		for (int i = 0; i < count; i++)
		{
			float d = values[i] - mean;
			variance += d * d;
		}
		stdDev = Mathf.Sqrt(variance / count);
		range = max - min;
	}

	private static void ComputeVector3Stats(Vector3[] values, int count, out Vector3 mean, out Vector3 stdDev)
	{
		mean = Vector3.Zero;
		stdDev = Vector3.Zero;
		if (values == null || count <= 0)
		{
			return;
		}

		for (int i = 0; i < count; i++)
		{
			mean += values[i];
		}
		mean /= count;

		Vector3 variance = Vector3.Zero;
		for (int i = 0; i < count; i++)
		{
			Vector3 d = values[i] - mean;
			variance += new Vector3(d.X * d.X, d.Y * d.Y, d.Z * d.Z);
		}

		variance /= count;
		stdDev = new Vector3(
			Mathf.Sqrt(variance.X),
			Mathf.Sqrt(variance.Y),
			Mathf.Sqrt(variance.Z));
	}

	private static float DistancePointToSegment(Vector3 point, Vector3 segA, Vector3 segB)
	{
		Vector3 ab = segB - segA;
		float denom = ab.LengthSquared();
		if (denom <= 1e-12f)
		{
			return point.DistanceTo(segA);
		}

		float t = Mathf.Clamp((point - segA).Dot(ab) / denom, 0f, 1f);
		Vector3 closest = segA + ab * t;
		return point.DistanceTo(closest);
	}

	private static float SafeAngleBetween(Vector3 a, Vector3 b)
	{
		if (a.LengthSquared() <= 1e-12f || b.LengthSquared() <= 1e-12f)
		{
			return 0f;
		}

		float dot = Mathf.Clamp(a.Normalized().Dot(b.Normalized()), -1f, 1f);
		return Mathf.Acos(dot);
	}

	private static float ComputeRadialTravelSign(Vector3 position, Vector3 velocity, Vector3 center)
	{
		Vector3 radial = position - center;
		if (radial.LengthSquared() <= 1e-12f || velocity.LengthSquared() <= 1e-12f)
		{
			return 0f;
		}

		float radialDot = velocity.Dot(radial.Normalized());
		if (Mathf.Abs(radialDot) <= PhotonRingMinRadialSign)
		{
			return 0f;
		}

		return radialDot > 0f ? 1f : -1f;
	}

	private static bool IsPhotonRingCandidate(float closestApproach, float photonSphereRadius, float photonSphereTolerance)
	{
		if (!float.IsFinite(closestApproach) || !float.IsFinite(photonSphereRadius))
		{
			return false;
		}

		float tolerance = Mathf.Max(PhotonRingMinTolerance, photonSphereTolerance);
		return Mathf.Abs(closestApproach - photonSphereRadius) <= tolerance;
	}

	private static string BuildRadialHistogram(float[] values, int count, int bins, float? minOverride = null, float? maxOverride = null)
	{
		if (values == null || count <= 0 || bins < 2)
		{
			return string.Empty;
		}

		float min = minOverride ?? values[0];
		float max = maxOverride ?? values[0];
		if (!minOverride.HasValue || !maxOverride.HasValue)
		{
			for (int i = 1; i < count; i++)
			{
				float v = values[i];
				if (!minOverride.HasValue && v < min) min = v;
				if (!maxOverride.HasValue && v > max) max = v;
			}
		}

		int safeBins = Mathf.Max(2, bins);
		int[] histogram = new int[safeBins];
		float range = max - min;
		if (range <= 1e-6f)
		{
			histogram[0] = count;
		}
		else
		{
			float inv = 1f / range;
			for (int i = 0; i < count; i++)
			{
				float normalized = (values[i] - min) * inv;
				int index = Mathf.Clamp((int)Mathf.Floor(normalized * safeBins), 0, safeBins - 1);
				histogram[index]++;
			}
		}

		StringBuilder builder = new StringBuilder(safeBins * 4);
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

	private static string BuildIntHistogram(int[] histogram)
	{
		if (histogram == null || histogram.Length == 0)
		{
			return string.Empty;
		}

		StringBuilder builder = new StringBuilder(histogram.Length * 4);
		for (int i = 0; i < histogram.Length; i++)
		{
			if (i > 0)
			{
				builder.Append(',');
			}
			builder.Append(histogram[i]);
		}
		return builder.ToString();
	}

	private static int ComputeAngularBin(Vector2 ndc, int angularBins)
	{
		if (angularBins <= 1)
		{
			return 0;
		}

		float angle = Mathf.Atan2(ndc.Y, ndc.X);
		if (angle < 0f)
		{
			angle += Mathf.Tau;
		}

		return Mathf.Clamp((int)Mathf.Floor((angle / Mathf.Tau) * angularBins), 0, angularBins - 1);
	}

	private static float ComputeAngularCoverage(int[] angularHistogram, int angularBins)
	{
		if (angularHistogram == null || angularBins <= 0)
		{
			return 0f;
		}

		int occupied = 0;
		for (int i = 0; i < angularBins && i < angularHistogram.Length; i++)
		{
			if (angularHistogram[i] > 0)
			{
				occupied++;
			}
		}

		return occupied / (float)angularBins;
	}

	private static float ComputeAngularRadiusAnisotropy(float[] radiusSums, int[] radiusCounts)
	{
		if (radiusSums == null || radiusCounts == null || radiusSums.Length == 0 || radiusCounts.Length == 0)
		{
			return 0f;
		}

		float minMean = float.PositiveInfinity;
		float maxMean = 0f;
		int populated = 0;
		for (int i = 0; i < radiusSums.Length && i < radiusCounts.Length; i++)
		{
			if (radiusCounts[i] <= 0)
			{
				continue;
			}

			float mean = radiusSums[i] / radiusCounts[i];
			if (mean < minMean) minMean = mean;
			if (mean > maxMean) maxMean = mean;
			populated++;
		}

		if (populated <= 1 || !float.IsFinite(minMean) || maxMean <= 1e-6f)
		{
			return 0f;
		}

		return Mathf.Max(0f, maxMean - minMean);
	}

	private static float ComputeShadowAreaFraction(Vector2[] probes, int probeCount, int absorbedCount)
	{
		if (probes == null || probeCount <= 0 || absorbedCount <= 0)
		{
			return 0f;
		}

		return Mathf.Clamp(absorbedCount / (float)probeCount, 0f, 1f);
	}

	private static Vector2[] BuildShadowProbePattern()
	{
		float[] radii = { 0.00f, 0.04f, 0.08f, 0.12f, 0.16f, 0.20f, 0.24f, 0.28f, 0.32f, ShadowProbeMaxRadius };
		int[] samplesPerRing = { 1, 8, 12, 16, 20, 24, 28, 32, 36, 40 };
		int total = 0;
		for (int i = 0; i < samplesPerRing.Length; i++)
		{
			total += samplesPerRing[i];
		}

		Vector2[] probes = new Vector2[total];
		int cursor = 0;
		for (int ringIndex = 0; ringIndex < radii.Length; ringIndex++)
		{
			float radius = radii[ringIndex];
			int samples = samplesPerRing[ringIndex];
			if (samples <= 1 || radius <= 1e-6f)
			{
				probes[cursor++] = Vector2.Zero;
				continue;
			}

			float phase = (ringIndex % 2 == 0) ? 0f : Mathf.Pi / samples;
			for (int sampleIndex = 0; sampleIndex < samples; sampleIndex++)
			{
				float angle = phase + (Mathf.Tau * sampleIndex / samples);
				probes[cursor++] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
			}
		}

		return probes;
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
				$"[BlackHoleFixture][SourcePattern] mode={PatternMode} count=0 spacing=({SourceSpacingX:0.###},{SourceSpacingY:0.###}) " +
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
			$"[BlackHoleFixture][SourcePattern] mode={PatternMode} count={_sourceMarkers.Length} spacing=({Mathf.Max(0f, SourceSpacingX):0.###},{Mathf.Max(0f, SourceSpacingY):0.###}) " +
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
			GD.Print($"[BlackHoleFixture][SourcePatternCheck] cross_expected_count=9 actual={markerCount} ok={(markerCount == 9 ? 1 : 0)}");
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

		GD.Print(
			$"[BlackHoleFixture][SourcePatternLayout] markers={markerCount} overlap_exact={overlapExactCount} min_pair_dist={minDistance:0.######} " +
			$"z_range=({zMin:0.###},{zMax:0.###}) center_z={centerZ:0.###} behind_center={behindCenter}");

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
				$"[BlackHoleFixture][SourcePatternMarker] i={i} name={marker.Name} local={FormatVec3(localPos)} global={FormatVec3(worldPos)} " +
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

	private static bool IsRenderTestMode()
	{
		string[] args = OS.GetCmdlineArgs();
		if (args == null || args.Length == 0)
		{
			return false;
		}
		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (arg.Equals("--render-test", StringComparison.Ordinal) ||
				arg.StartsWith("--render-test-", StringComparison.Ordinal) ||
				arg.StartsWith("--render-test-fixture=", StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private PhotonBandFixtureMode ResolvePhotonBandFixtureMode(out string source)
	{
		if (TryParsePhotonBandModeArg(OS.GetCmdlineUserArgs(), out PhotonBandFixtureMode fromUser))
		{
			source = "cmdline_user";
			return fromUser;
		}
		if (TryParsePhotonBandModeArg(OS.GetCmdlineArgs(), out PhotonBandFixtureMode fromArgs))
		{
			source = "cmdline";
			return fromArgs;
		}

		source = "scene_authored";
		return PhotonBandFixtureMode.SceneAuthored;
	}

	private static PhotonBandCompareOverride ResolvePhotonBandCompareOverride()
	{
		if (TryParsePhotonBandCompareOverride(OS.GetCmdlineUserArgs(), "cmdline_user", out PhotonBandCompareOverride fromUser))
		{
			return fromUser;
		}
		if (TryParsePhotonBandCompareOverride(OS.GetCmdlineArgs(), "cmdline", out PhotonBandCompareOverride fromArgs))
		{
			return fromArgs;
		}

		return new PhotonBandCompareOverride(false, false, false, 0f, 0f, 0f, "none");
	}

	private static MassFieldCompareOverride ResolveMassFieldCompareOverride()
	{
		if (TryParseMassFieldCompareOverride(OS.GetCmdlineUserArgs(), "cmdline_user", out MassFieldCompareOverride fromUser))
		{
			return fromUser;
		}
		if (TryParseMassFieldCompareOverride(OS.GetCmdlineArgs(), "cmdline", out MassFieldCompareOverride fromArgs))
		{
			return fromArgs;
		}

		return new MassFieldCompareOverride(false, false, false, false, 0f, 0f, 0f, 0f, "none");
	}

	private static bool TryParsePhotonBandModeArg(string[] args, out PhotonBandFixtureMode mode)
	{
		mode = PhotonBandFixtureMode.SceneAuthored;
		if (args == null || args.Length == 0)
		{
			return false;
		}

		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (TryParsePhotonBandModeValue(arg, FixturePhotonBandArgPrefix, out mode))
			{
				return true;
			}
			if (TryParsePhotonBandModeValue(arg, PhotonBandArgPrefix, out mode))
			{
				return true;
			}
		}

		return false;
	}

	private static bool TryParsePhotonBandCompareOverride(string[] args, string source, out PhotonBandCompareOverride compareOverride)
	{
		compareOverride = new PhotonBandCompareOverride(false, false, false, 0f, 0f, 0f, source);
		if (args == null || args.Length == 0)
		{
			return false;
		}

		bool hasRInner = false;
		bool hasROuter = false;
		bool hasAmp = false;
		float rInner = 0f;
		float rOuter = 0f;
		float amp = 0f;
		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (TryParseFloatArgValue(arg, FixturePhotonBandRInnerArgPrefix, out float parsedRInner))
			{
				hasRInner = true;
				rInner = Mathf.Max(0f, parsedRInner);
				continue;
			}
			if (TryParseFloatArgValue(arg, FixturePhotonBandROuterArgPrefix, out float parsedROuter))
			{
				hasROuter = true;
				rOuter = Mathf.Max(0f, parsedROuter);
				continue;
			}
			if (TryParseFloatArgValue(arg, FixturePhotonBandAmpArgPrefix, out float parsedAmp))
			{
				hasAmp = true;
				amp = parsedAmp;
			}
		}

		if (!hasRInner && !hasROuter && !hasAmp)
		{
			return false;
		}

		compareOverride = new PhotonBandCompareOverride(hasRInner, hasROuter, hasAmp, rInner, rOuter, amp, source);
		return true;
	}

	private static bool TryParseMassFieldCompareOverride(string[] args, string source, out MassFieldCompareOverride compareOverride)
	{
		compareOverride = new MassFieldCompareOverride(false, false, false, false, 0f, 0f, 0f, 0f, source);
		if (args == null || args.Length == 0)
		{
			return false;
		}

		bool hasRInner = false;
		bool hasROuter = false;
		bool hasAmp = false;
		bool hasGamma = false;
		float rInner = 0f;
		float rOuter = 0f;
		float amp = 0f;
		float gamma = 0f;
		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (TryParseFloatArgValue(arg, FixtureMassRInnerArgPrefix, out float parsedRInner))
			{
				hasRInner = true;
				rInner = Mathf.Max(0f, parsedRInner);
				continue;
			}
			if (TryParseFloatArgValue(arg, FixtureMassROuterArgPrefix, out float parsedROuter))
			{
				hasROuter = true;
				rOuter = Mathf.Max(0f, parsedROuter);
				continue;
			}
			if (TryParseFloatArgValue(arg, FixtureMassAmpArgPrefix, out float parsedAmp))
			{
				hasAmp = true;
				amp = parsedAmp;
				continue;
			}
			if (TryParseFloatArgValue(arg, FixtureMassGammaArgPrefix, out float parsedGamma))
			{
				hasGamma = true;
				gamma = parsedGamma;
			}
		}

		if (!hasRInner && !hasROuter && !hasAmp && !hasGamma)
		{
			return false;
		}

		compareOverride = new MassFieldCompareOverride(hasRInner, hasROuter, hasAmp, hasGamma, rInner, rOuter, amp, gamma, source);
		return true;
	}

	private static bool TryParsePhotonBandModeValue(string arg, string prefix, out PhotonBandFixtureMode mode)
	{
		mode = PhotonBandFixtureMode.SceneAuthored;
		if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string value = arg.Substring(prefix.Length).Trim();
		return TryParsePhotonBandModeToken(value, out mode);
	}

	private static bool TryParsePhotonBandModeToken(string token, out PhotonBandFixtureMode mode)
	{
		mode = PhotonBandFixtureMode.SceneAuthored;
		if (string.IsNullOrWhiteSpace(token))
		{
			return false;
		}

		string normalized = token.Trim().ToLowerInvariant();
		if (normalized == "off" || normalized == "disable" || normalized == "disabled" ||
			normalized == "core" || normalized == "core-only" || normalized == "core_only")
		{
			mode = PhotonBandFixtureMode.Disabled;
			return true;
		}
		if (normalized == "on" || normalized == "enable" || normalized == "enabled" ||
			normalized == "band" || normalized == "core+band" || normalized == "core_plus_band")
		{
			mode = PhotonBandFixtureMode.SceneAuthored;
			return true;
		}

		return false;
	}

	private static string PhotonBandModeToToken(PhotonBandFixtureMode mode)
	{
		return mode == PhotonBandFixtureMode.Disabled ? "core_only" : "core_plus_band";
	}

	private static FieldSource3D.ResolvedFieldParams BuildEffectivePhotonBandResolved(
		FieldSource3D.ResolvedFieldParams resolved,
		PhotonBandFixtureMode mode,
		PhotonBandShell shell,
		PhotonBandCompareOverride compareOverride)
	{
		if (compareOverride.HasAny)
		{
			resolved.rInner = shell.RInner;
			resolved.rOuter = shell.ROuter;
			if (compareOverride.HasAmp)
			{
				resolved.amp = compareOverride.Amp;
			}
		}

		if (mode != PhotonBandFixtureMode.Disabled)
		{
			return resolved;
		}

		resolved.enabled = false;
		resolved.amp = 0f;
		return resolved;
	}

	private static RayBeamRenderer.FieldSourceSnap BuildEffectivePhotonBandSnap(
		RayBeamRenderer.FieldSourceSnap snap,
		PhotonBandFixtureMode mode,
		PhotonBandShell shell,
		PhotonBandCompareOverride compareOverride)
	{
		if (compareOverride.HasAny)
		{
			snap.RInner = shell.RInner;
			snap.ROuter = shell.ROuter;
			if (compareOverride.HasAmp)
			{
				snap.Amp = compareOverride.Amp;
			}
		}

		if (mode != PhotonBandFixtureMode.Disabled)
		{
			return snap;
		}

		snap.Enabled = false;
		snap.Amp = 0f;
		return snap;
	}

	private static void ApplyMassFieldOverride(FieldSource3D field, MassFieldCompareOverride compareOverride)
	{
		if (field == null || !compareOverride.HasAny)
		{
			return;
		}

		if (compareOverride.HasRInner)
		{
			field.CanonicalEnableInnerRadius = true;
			field.RInner = compareOverride.RInner;
		}
		if (compareOverride.HasROuter)
		{
			field.ROuter = compareOverride.ROuter;
		}
		if (field.ROuter > 0f && field.RInner > field.ROuter)
		{
			field.RInner = field.ROuter;
		}
		if (compareOverride.HasAmp)
		{
			field.Amp = compareOverride.Amp;
		}
		if (compareOverride.HasGamma)
		{
			field.CanonicalGamma = compareOverride.Gamma;
		}
	}

	private static string ResolveEffectiveMassFieldSource(string resolveReason, MassFieldCompareOverride compareOverride)
	{
		if (!compareOverride.HasAny)
		{
			return resolveReason;
		}

		return $"compare_override({compareOverride.Source})";
	}

	private static bool IsPhotonBandActive(FieldSource3D.ResolvedFieldParams resolved)
	{
		return resolved.enabled && resolved.amp > CurvatureAccelEpsilon;
	}

	private static void LogPhotonBandMode(
		PhotonBandFixtureMode mode,
		string source,
		bool authoredEnabled,
		bool effectiveEnabled)
	{
		GD.Print(
			$"[BlackHoleFixture][PhotonBandMode] mode={PhotonBandModeToToken(mode)} source={source} authored_enabled={(authoredEnabled ? 1 : 0)} effective_enabled={(effectiveEnabled ? 1 : 0)}");
	}

	private TransportModel ResolveFixtureTransportModel()
	{
		return ResolveFixtureTransportModel(out _);
	}

	private static float ResolveMetricDetectorScaleOverride()
	{
		if (TryParseMetricDetectorScaleArg(OS.GetCmdlineUserArgs(), out float fromUser))
		{
			return fromUser;
		}
		if (TryParseMetricDetectorScaleArg(OS.GetCmdlineArgs(), out float fromArgs))
		{
			return fromArgs;
		}
		return 1f;
	}

	private static bool ResolveMetricTrajectoryDebugEnabled()
	{
		return HasCommandLineArg(OS.GetCmdlineUserArgs(), MetricTrajectoryDebugArg) ||
			HasCommandLineArg(OS.GetCmdlineArgs(), MetricTrajectoryDebugArg);
	}

	private TransportModel ResolveFixtureTransportModel(out string source)
	{
		if (TryParseTransportOverrideArg(OS.GetCmdlineUserArgs(), out TransportModel fromUser))
		{
			source = "cmdline_user";
			return fromUser;
		}
		if (TryParseTransportOverrideArg(OS.GetCmdlineArgs(), out TransportModel fromArgs))
		{
			source = "cmdline";
			return fromArgs;
		}
		if (TryResolveSceneBaselineTransportModel(out TransportModel fromScene))
		{
			source = "scene_baseline";
			return fromScene;
		}

		source = "fixture_default";
		return DefaultFixtureTransportModel;
	}

	private static bool TryParseTransportOverrideArg(string[] args, out TransportModel model)
	{
		model = DefaultFixtureTransportModel;
		if (args == null || args.Length == 0)
		{
			return false;
		}

		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (TryParseTransportArgValue(arg, FixtureTransportArgPrefix, out model))
			{
				return true;
			}
			if (TryParseTransportArgValue(arg, TransportArgPrefix, out model))
			{
				return true;
			}
		}

		return false;
	}

	private static bool TryParseMetricDetectorScaleArg(string[] args, out float scale)
	{
		scale = 1f;
		if (args == null || args.Length == 0)
		{
			return false;
		}

		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (string.Equals(arg, MetricLargeDetectorArg, StringComparison.OrdinalIgnoreCase))
			{
				scale = MetricLargeDetectorScale;
				return true;
			}
			if (!arg.StartsWith(MetricDetectorScaleArgPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string value = arg.Substring(MetricDetectorScaleArgPrefix.Length).Trim();
			if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
			{
				continue;
			}

			scale = Mathf.Max(1f, parsed);
			return true;
		}

		return false;
	}

	private static bool TryParseFloatArgValue(string arg, string prefix, out float value)
	{
		value = 0f;
		if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string token = arg.Substring(prefix.Length).Trim();
		return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	}

	private static bool TryParseTransportArgValue(string arg, string prefix, out TransportModel model)
	{
		model = DefaultFixtureTransportModel;
		if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string value = arg.Substring(prefix.Length).Trim();
		return TryParseTransportModelToken(value, out model);
	}

	private static bool TryParseTransportModelToken(string token, out TransportModel model)
	{
		model = DefaultFixtureTransportModel;
		if (string.IsNullOrWhiteSpace(token))
		{
			return false;
		}

		string normalized = token.Trim().ToLowerInvariant();
		if (normalized == "grin" || normalized == "grin_optical" || normalized == "optical")
		{
			model = TransportModel.GRIN_Optical;
			return true;
		}
		if (normalized == "metric" || normalized == "metric_nullgeodesic" || normalized == "nullgeodesic")
		{
			model = TransportModel.Metric_NullGeodesic;
			return true;
		}

		return Enum.TryParse(token, true, out model);
	}

	private static bool HasCommandLineArg(string[] args, string expected)
	{
		if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(expected))
		{
			return false;
		}

		for (int i = 0; i < args.Length; i++)
		{
			if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private static float MaybeApplyMetricDetectorScaleOverride(StaticBody3D detectorPlane, TransportModel activeTransportModel)
	{
		float scale = ResolveMetricDetectorScaleOverride();
		if (detectorPlane == null || activeTransportModel != TransportModel.Metric_NullGeodesic || !float.IsFinite(scale) || scale <= 1f)
		{
			return 1f;
		}

		Vector3 nextScale = detectorPlane.Scale;
		nextScale.X *= scale;
		nextScale.Y *= scale;
		detectorPlane.Scale = nextScale;
		GD.Print($"[BlackHoleMetricPath][DetectorMode] detectorScale={scale:0.###} transportModel={activeTransportModel}");
		return scale;
	}

	private bool TryResolveSceneBaselineTransportModel(out TransportModel model)
	{
		model = DefaultFixtureTransportModel;
		FieldSource3D massField = GetNodeOrNull<FieldSource3D>(FieldPath);
		FieldSource3D photonBandField = GetNodeOrNull<FieldSource3D>(PhotonBandFieldPath);
		if (massField != null)
		{
			model = massField.TransportModel;
			return true;
		}
		if (photonBandField != null)
		{
			model = photonBandField.TransportModel;
			return true;
		}

		return false;
	}

	private void LogStartupVariant(TransportModel transportModel, string transportSource)
	{
		string scenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
		string variant = string.IsNullOrWhiteSpace(scenePath)
			? "test-blackhole-minimal"
			: Path.GetFileNameWithoutExtension(scenePath);
		GD.Print(
			$"[FixtureStartup] fixture=blackhole_minimal variant={variant} transport={transportModel} source={transportSource}");
		LogMetricVariantTransportMismatchIfNeeded(variant, transportModel, transportSource);
	}

	private static void LogMetricVariantTransportMismatchIfNeeded(
		string variant,
		TransportModel transportModel,
		string transportSource)
	{
		if (string.IsNullOrWhiteSpace(variant) ||
			variant.IndexOf("-metric", StringComparison.OrdinalIgnoreCase) < 0 ||
			transportModel != TransportModel.GRIN_Optical)
		{
			return;
		}

		GD.PrintErr(
			$"[FixtureStartup][WARN] fixture=blackhole_minimal variant={variant} resolved_transport={transportModel} " +
			$"source={transportSource} expected_metric_scene_baseline=Metric_NullGeodesic");
	}

	private void ApplyHudMetadata(GrinFilmCamera filmCamera, RayBeamRenderer rayRenderer, TransportModel activeTransportModel)
	{
		if (filmCamera == null || !GodotObject.IsInstanceValid(filmCamera))
		{
			return;
		}

		filmCamera.SetHudFixtureName("blackhole_minimal");
		filmCamera.SetHudTransportModel(activeTransportModel.ToString());
		filmCamera.SetHudSourcePatternMode(PatternMode.ToString());
		filmCamera.SetHudRenderProbeRayCount(_sourceMarkers?.Length ?? 0);
		string metricSteeringLaw = rayRenderer != null
			? RayBeamRenderer.GetMetricSteeringLawToken(rayRenderer.GetEffectiveMetricSteeringLaw())
			: string.Empty;
		filmCamera.SetHudMetricSteeringLaw(metricSteeringLaw);
		float metricGainOverride = RayBeamRenderer.ResolveMetricComparisonScalarOverride();
		bool metricGainActive = activeTransportModel == TransportModel.Metric_NullGeodesic &&
			float.IsFinite(metricGainOverride) &&
			!Mathf.IsEqualApprox(metricGainOverride, 1.0f);
		filmCamera.SetHudMetricGainOverride(metricGainOverride, metricGainActive);
	}

	private string BuildSourcePatternSummaryToken()
	{
		int count = _sourceMarkers?.Length ?? 0;
		string signature = BuildSourcePatternSignature(_sourceMarkers);
		string checksum = ComputeSha256Hex(signature);
		return
			$"mode={PatternMode}|count={count}|spacingX={F(Mathf.Max(0f, SourceSpacingX))}|spacingY={F(Mathf.Max(0f, SourceSpacingY))}|" +
			$"radius={F(Mathf.Max(0.01f, SourceMarkerRadius))}|origin={FormatVec3(PatternOriginLocal)}|includeCenter={(IncludeCenterPoint ? 1 : 0)}|checksum={checksum}";
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

	private void FailInvalid(string reason, string details)
	{
		_invalidFixture = true;
		string msg = $"BlackHole_Minimal invalid: {reason} | {details}";
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

		if (GetNodeOrNull<Node>("RenderTestRunner") != null && IsRenderTestMode())
		{
			GD.PrintErr("[BlackHoleFixture][FAIL] Requesting quit code=1 due to fixture invariant failure.");
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

	private static string FormatVec2(Vector2 v)
	{
		return $"({v.X:0.###},{v.Y:0.###})";
	}

	private static void AppendTrajectorySamplePoint(StringBuilder builder, Vector3 point)
	{
		if (builder == null)
		{
			return;
		}

		if (builder.Length > 0)
		{
			builder.Append("->");
		}
		builder.Append(FormatVec3(point));
	}

	private static bool ShouldCaptureMetricTrajectorySample(int rayIndex)
	{
		return rayIndex == 0 ||
			rayIndex == RayProbeNdc.Length / 2 ||
			rayIndex == RayProbeNdc.Length - 1 ||
			rayIndex == Mathf.Clamp(RayProbeNdc.Length / 2 - 1, 0, RayProbeNdc.Length - 1);
	}

	private static string BuildAlternatePlaneHitSummary(
		DetectorDiagnosticPlacement[] alternatePlanes,
		int[] hitCounts,
		int sampleCount)
	{
		if (alternatePlanes == null || hitCounts == null || alternatePlanes.Length == 0 || hitCounts.Length == 0)
		{
			return "altPlaneHits=n/a";
		}

		StringBuilder builder = new StringBuilder(64);
		builder.Append("altPlaneHits=");
		for (int i = 0; i < alternatePlanes.Length && i < hitCounts.Length; i++)
		{
			if (i > 0)
			{
				builder.Append(',');
			}
			builder.Append(alternatePlanes[i].Name);
			builder.Append(':');
			builder.Append(hitCounts[i]);
			builder.Append('/');
			builder.Append(sampleCount);
		}
		return builder.ToString();
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
