using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Godot;
using RendererCore.Fields;

public partial class BlackHoleMinimalFingerprint : Node3D
{
	private static readonly NodePath FieldPath = new("FixtureBlackholeMinimal/FieldSource3D");
	private static readonly NodePath PhotonBandFieldPath = new("FixtureBlackholeMinimal/PhotonBandSource");
	private static readonly NodePath CameraPath = new("FixtureBlackholeMinimal/Camera3D");
	private static readonly NodePath SpherePath = new("FixtureBlackholeMinimal/blackhole_center_marker");
	private static readonly NodePath RendererPath = new("FixtureBlackholeMinimal/RayBeamRenderer");
	private static readonly NodePath FilmCameraPath = new("GrinFilmCamera");

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
	private const float PhotonBandNoticeableAccelMin = 0.002f;
	private const float ProbeAccelChaosBound = 8.0f;
	private const float AccelClampMax = 50.0f;

	private bool _invalidFixture;
	private bool _loggedBroadphaseLock;

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
		Node3D sphere = GetNodeOrNull<Node3D>(SpherePath);
		EnforceFixtureBroadphasePolicy(filmCamera);

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
			$"[BlackHoleFixture][CurvatureEngaged] mass_match=1 band_match=1 probe_any=1 band_noticeable=1 mass_primary=1 " +
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

		int absorbedRayCount = CountAbsorbedRayProbes(
			snaps,
			massSnap.Center,
			massSnap.ROuter,
			rayRenderer,
			Mathf.Clamp(rayRenderer.StepsPerRay, 64, CurvatureRayStepCap));
		GD.Print($"[BlackHoleFixture][Absorption] absorbed_rays={absorbedRayCount}/{RayProbeNdc.Length}");

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
			$"[BlackHoleFixture] mass_strength={massResolved.amp:0.###} mass_radius={massResolved.rOuter:0.###} " +
			$"band_strength={bandResolved.amp:0.###} band_r_inner={bandResolved.rInner:0.###} band_r_outer={bandResolved.rOuter:0.###} " +
			$"mass_node_path={massField.GetPath()} band_node_path={photonBandField.GetPath()} sphere_global_pos={spherePos} sphere_scale={sphereScale} " +
			$"cam_fov={camFov} camera_global_pos={camPos} cam_to_sphere={camToSphere}");

		GD.Print(
			$"[BlackHoleFixture][ResolvedMass] source={massResolveReason} curve={massResolved.curveType} modeFlags={massResolved.modeFlags} " +
			$"rInner={massResolved.rInner:0.###} rOuter={massResolved.rOuter:0.###} amp={massResolved.amp:0.###} " +
			$"gamma={massResolved.a:0.###} beta_mode={(massResolved.overrideBetaScale ? "override" : "global")} beta_scale={massResolved.betaScale:0.###}");

		GD.Print(
			$"[BlackHoleFixture][ResolvedPhotonBand] source={bandResolveReason} enabled={(bandResolved.enabled ? 1 : 0)} curve={bandResolved.curveType} modeFlags={bandResolved.modeFlags} " +
			$"rInner={bandResolved.rInner:0.###} rOuter={bandResolved.rOuter:0.###} amp={bandResolved.amp:0.###} " +
			$"A={bandResolved.a:0.###} B={bandResolved.b:0.###} C={bandResolved.c:0.###} " +
			$"beta_mode={(bandResolved.overrideBetaScale ? "override" : "global")} beta_scale={bandResolved.betaScale:0.###}");

		GD.Print(
			$"[BlackHoleFixture][Renderer] useIntegrated={(useIntegrated ? 1 : 0)} bendScale={bendScale:0.###} fieldStrength={fieldStrength:0.###}");

		string fingerprint = BuildBlackHoleMinimalFingerprint();
		GD.Print($"BlackHoleMinimalFingerprint: {fingerprint}");
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
			$"v=2;" +
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
			$"rayK={FingerprintRaySteps};" +
			$"accelMass=[{massAccelVector}];" +
			$"accelBand=[{bandAccelVector}];" +
			$"accelTotal=[{totalAccelVector}];" +
			$"rayChecksum={rayEndpointChecksum}";

		string fingerprintHash = ComputeSha256Hex(fingerprintCore);
		GD.Print(
			$"BlackHoleMinimalFingerprintRaw: accelMass=[{massAccelVector}] accelBand=[{bandAccelVector}] accelTotal=[{totalAccelVector}] " +
			$"rayEndpoints=[{rayEndpointVector}] rayChecksum={rayEndpointChecksum}");
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
			GD.Print("[BlackHoleFixture][Broadphase] enforcing policy=OverlapOnly mode=Policy");
		}
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

		if (GetNodeOrNull<Node>("RenderTestRunner") != null)
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
