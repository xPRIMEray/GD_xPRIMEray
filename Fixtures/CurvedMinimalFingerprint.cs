using Godot;
using RendererCore.Fields;

public partial class CurvedMinimalFingerprint : Node3D
{
	private static readonly NodePath FieldPath = new("FixtureCurvedMinimal/FieldSource3D");
	private static readonly NodePath CameraPath = new("FixtureCurvedMinimal/Camera3D");
	private static readonly NodePath SpherePath = new("FixtureCurvedMinimal/fixture_target");
	private static readonly NodePath RendererPath = new("RayBeamRenderer");

	// Invariants summary:
	// 1) CurvatureEngaged: canonical field params must resolve and yield non-zero local acceleration
	//    while far-field acceleration stays near zero; renderer must be integrated with non-zero transport strength.
	// 2) CurvatureApplied: deterministic rays must diverge from straight-line endpoints.
	// This prevents false green when curvature is accidentally disabled by stale serialized values.
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

	private const bool FixedEnabled = true;
	private const FieldCurveType FixedCurveType = FieldCurveType.Power;
	private const float FixedAmp = 1.15f;
	private const bool FixedEnableInnerRadius = true;
	private const float FixedRInner = 0.0f;
	private const float FixedROuter = 4.5f;
	private const float FixedGamma = 2.0f;
	private const bool FixedOverrideBetaScale = true;
	private const float FixedBetaScale = 1.0f;
	private const uint FixedModeFlags = 0u;
	private const float FixedSoftening = 0.1f;
	private const float FixedEdgeSoftness = 0.0f;
	private const bool FixedDebugDrawBounds = false;
	private const bool FixedDebugDrawInGame = false;
	private const float CurvatureAccelEpsilon = 1e-6f;
	private const float FarAccelEpsilon = 1e-6f;
	private const float CurvatureDeviationMin = 1e-3f;
	private const int CurvatureRayStepCap = 200;
	private const int CurvatureRayMinDeviations = 6;

	private bool _invalidFixture;

	public override void _Ready()
	{
		FieldSource3D field = GetNodeOrNull<FieldSource3D>(FieldPath);
		if (field == null)
		{
			FailInvalid("missing FieldSource3D", $"field_path={FieldPath}");
			return;
		}

		ApplyCanonicalFixtureParams(field);

		FieldSource3D.ResolvedFieldParams resolved = field.ResolveEffectiveParams(out string resolveReason);
		RayBeamRenderer.FieldSourceSnap snap = RayBeamRenderer.BuildFieldSourceSnap(field);

		if (!string.Equals(resolveReason, "canonical", System.StringComparison.Ordinal))
		{
			FailInvalid(
				"curvature not engaged",
				$"resolve_reason={resolveReason} amp={resolved.amp:0.######} curve={resolved.curveType} rInner={resolved.rInner:0.######} rOuter={resolved.rOuter:0.######}");
			return;
		}

		Camera3D camera = GetNodeOrNull<Camera3D>(CameraPath);
		RayBeamRenderer rayRenderer = GetNodeOrNull<RayBeamRenderer>(RendererPath);
		Node3D sphere = GetNodeOrNull<Node3D>(SpherePath);

		float[] accelMags = new float[CurvatureProbeOffsets.Length];
		float accelSum = 0f;
		float maxProbeAccel = 0f;
		bool anyProbeAboveEps = false;
		for (int i = 0; i < CurvatureProbeOffsets.Length; i++)
		{
			Vector3 probe = field.GlobalPosition + CurvatureProbeOffsets[i];
			FieldMath.EvalResult eval = FieldMath.EvalFieldAccel(
				probe,
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

			float mag = eval.AccelerationMagnitude;
			accelMags[i] = mag;
			accelSum += mag;
			if (mag > maxProbeAccel)
			{
				maxProbeAccel = mag;
			}
			if (mag > CurvatureAccelEpsilon)
			{
				anyProbeAboveEps = true;
			}
		}

		float farDistance = Mathf.Max(snap.ROuter + 16f, 20f);
		Vector3 farProbe = field.GlobalPosition + new Vector3(0f, 0f, farDistance);
		FieldMath.EvalResult farEval = FieldMath.EvalFieldAccel(
			farProbe,
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
		float farAccelMag = farEval.AccelerationMagnitude;

		bool hasRenderer = rayRenderer != null;
		bool useIntegrated = hasRenderer && rayRenderer.UseIntegratedField;
		float bendScale = hasRenderer ? rayRenderer.BendScale : 0f;
		float fieldStrength = hasRenderer ? rayRenderer.FieldStrength : 0f;
		float transportStrength = Mathf.Abs(bendScale * fieldStrength);

		if (!anyProbeAboveEps || farAccelMag > FarAccelEpsilon || !useIntegrated || transportStrength <= CurvatureAccelEpsilon)
		{
			FailInvalid(
				"curvature not engaged",
				$"resolve_reason={resolveReason} curve={resolved.curveType} modeFlags={resolved.modeFlags} amp={resolved.amp:0.######} rInner={resolved.rInner:0.######} rOuter={resolved.rOuter:0.######} " +
				$"useIntegrated={(useIntegrated ? 1 : 0)} bendScale={bendScale:0.######} fieldStrength={fieldStrength:0.######} transport={transportStrength:0.######} " +
				$"probe_accel=[{FormatFloatArray(accelMags)}] far_accel={farAccelMag:0.######}");
			return;
		}

		int deviatedRayCount = 0;
		float maxDeviation = 0f;
		float deviationSum = 0f;
		MeasureRayCurvature(
			snap,
			rayRenderer,
			Mathf.Clamp(rayRenderer.StepsPerRay, 64, CurvatureRayStepCap),
			out deviatedRayCount,
			out maxDeviation,
			out deviationSum);

		if (deviatedRayCount < CurvatureRayMinDeviations)
		{
			FailInvalid(
				"curvature not applied",
				$"deviated_rays={deviatedRayCount}/{RayProbeNdc.Length} min_required={CurvatureRayMinDeviations} d_min={CurvatureDeviationMin:0.######} max_dev={maxDeviation:0.######} dev_sum={deviationSum:0.######} " +
				$"bendScale={bendScale:0.######} fieldStrength={fieldStrength:0.######} transport={transportStrength:0.######} " +
				$"probe_accel=[{FormatFloatArray(accelMags)}]");
			return;
		}

		float curvatureEnergy = accelSum * transportStrength;
		GD.Print(
			$"[CurvedFixture][CurvatureMetric] field_accel_sum={accelSum:0.######} curvature_energy={curvatureEnergy:0.######} " +
			$"max_probe_accel={maxProbeAccel:0.######} far_accel={farAccelMag:0.######}");

		field.Strength = FixedAmp;
		field.Softening = FixedSoftening;
		field.OuterRadius = FixedROuter;
		field.OverrideGamma = true;
		field.Gamma = FixedGamma;
		field.DebugDrawBounds = FixedDebugDrawBounds;
		field.DebugDrawInGame = FixedDebugDrawInGame;

		string sphereScale = sphere != null ? FormatVec3(sphere.Scale) : "n/a";
		string spherePos = sphere != null ? FormatVec3(sphere.GlobalTransform.Origin) : "n/a";
		string camFov = camera != null ? $"{camera.Fov:0.###}" : "n/a";
		string camPos = camera != null ? FormatVec3(camera.GlobalTransform.Origin) : "n/a";
		string camToSphere = (camera != null && sphere != null)
			? $"{camera.GlobalTransform.Origin.DistanceTo(sphere.GlobalTransform.Origin):0.###}"
			: "n/a";

		GD.Print(
			$"[CurvedFixture] strength={resolved.amp:0.###} radius={resolved.rOuter:0.###} " +
			$"node_path={field.GetPath()} sphere_global_pos={spherePos} sphere_scale={sphereScale} " +
			$"cam_fov={camFov} camera_global_pos={camPos} cam_to_sphere={camToSphere}");

		GD.Print(
			$"[CurvedFixture][Resolved] source={resolveReason} curve={resolved.curveType} modeFlags={resolved.modeFlags} " +
			$"rInner={resolved.rInner:0.###} rOuter={resolved.rOuter:0.###} amp={resolved.amp:0.###} " +
			$"gamma={resolved.a:0.###} beta_mode={(resolved.overrideBetaScale ? "override" : "global")} beta_scale={resolved.betaScale:0.###} " +
			$"useIntegrated={(useIntegrated ? 1 : 0)} bendScale={bendScale:0.###} fieldStrength={fieldStrength:0.###}");
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
		field.ModeFlags = FixedModeFlags;
		field.Softening = FixedSoftening;
		field.CanonicalEdgeSoftness = FixedEdgeSoftness;
		field.DebugDrawBounds = FixedDebugDrawBounds;
		field.DebugDrawInGame = FixedDebugDrawInGame;
	}

	private void MeasureRayCurvature(
		RayBeamRenderer.FieldSourceSnap snap,
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

		float launchZ = -Mathf.Max(0.25f, snap.ROuter * 0.5f);
		float launchXY = Mathf.Max(0.1f, snap.ROuter * 0.2f);
		Vector3 baseOrigin = snap.Center + new Vector3(0f, 0f, launchZ);
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
				FieldMath.EvalResult eval = FieldMath.EvalFieldAccel(
					pCurved,
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
				Vector3 accel = eval.Acceleration * bendScale * fieldStrength;
				float aLen = eval.AccelerationMagnitude * Mathf.Abs(bendScale * fieldStrength);
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

	private void FailInvalid(string reason, string details)
	{
		_invalidFixture = true;
		string msg = $"Curved_Minimal invalid: {reason} | {details}";
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
			GD.PrintErr("[CurvedFixture][FAIL] Requesting quit code=1 due to fixture invariant failure.");
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
}
