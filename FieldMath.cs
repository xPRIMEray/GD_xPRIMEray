using Godot;
using RendererCore.Fields;

/// <summary>
/// PR: Academic canonical radial model: u-clamped shell with profile f(u).
/// </summary>
public static class FieldMath
{
	public const float Epsilon = 1e-6f;
	public const uint ModeFlagInvertSign = 1u << 0;

	public readonly struct EvalResult
	{
		public readonly float R;
		public readonly float U;
		public readonly float Profile;
		public readonly float EdgeRamp;
		public readonly float ProfileWithEdge;
		public readonly float BetaEff;
		public readonly float Gamma;
		public readonly float Sigma;
		public readonly float Magnitude;
		public readonly Vector3 Direction;
		public readonly Vector3 Acceleration;

		public float AccelerationMagnitude => Acceleration.Length();

		public EvalResult(
			float r,
			float u,
			float profile,
			float edgeRamp,
			float profileWithEdge,
			float betaEff,
			float gamma,
			float sigma,
			float magnitude,
			Vector3 direction,
			Vector3 acceleration)
		{
			R = r;
			U = u;
			Profile = profile;
			EdgeRamp = edgeRamp;
			ProfileWithEdge = profileWithEdge;
			BetaEff = betaEff;
			Gamma = gamma;
			Sigma = sigma;
			Magnitude = magnitude;
			Direction = direction;
			Acceleration = acceleration;
		}
	}

	public static EvalResult EvalFieldAccel(
		Vector3 samplePosition,
		Vector3 center,
		FieldCurveType curveType,
		float rInner,
		float rOuter,
		float amp,
		float gamma,
		float sigma,
		float polyA,
		float polyB,
		float polyC,
		Curve customCurve,
		uint modeFlags,
		float globalBeta,
		bool overrideBetaScale,
		float localBetaScale,
		float edgeSoftness)
	{
		Vector3 delta = samplePosition - center;
		float r = delta.Length();
		float u = ComputeU(r, rInner, rOuter);
		float safeSigma = Mathf.Max(Epsilon, sigma);
		float profile = EvaluateProfileAtU(curveType, u, gamma, safeSigma, polyA, polyB, polyC, customCurve);
		float edgeRamp = EvaluateEdgeRamp(u, edgeSoftness);
		float profileWithEdge = profile * edgeRamp;
		float betaEff = ResolveBetaEff(globalBeta, overrideBetaScale, localBetaScale);
		float mag = betaEff * amp * profileWithEdge;

		Vector3 dir = Vector3.Zero;
		if (r > Epsilon)
		{
			dir = delta / r;
		}
		if ((modeFlags & ModeFlagInvertSign) == 0u)
		{
			dir = -dir;
		}

		Vector3 accel = dir * mag;
		return new EvalResult(r, u, profile, edgeRamp, profileWithEdge, betaEff, gamma, safeSigma, mag, dir, accel);
	}

	public static float ComputeU(float r, float rInner, float rOuter)
	{
		float inner = Mathf.Max(0f, rInner);
		float outer = Mathf.Max(inner, rOuter);
		float span = Mathf.Max(Epsilon, outer - inner);
		return Mathf.Clamp((r - inner) / span, 0f, 1f);
	}

	public static float EvaluateProfileAtU(
		FieldCurveType curveType,
		float u,
		float gamma,
		float sigma,
		float polyA,
		float polyB,
		float polyC,
		Curve customCurve)
	{
		float clampedU = Mathf.Clamp(u, 0f, 1f);
		float oneMinusU = 1f - clampedU;
		float safeGamma = float.IsFinite(gamma) ? gamma : 1f;
		float safeSigma = Mathf.Max(Epsilon, sigma);

		switch (curveType)
		{
			case FieldCurveType.Linear:
				return 1f - clampedU;

			case FieldCurveType.Power:
				return PowOneMinusU(oneMinusU, safeGamma);

			case FieldCurveType.Exponential:
			{
				// Gaussian profile in canonical u-space: exp(-(u/sigma)^2).
				float x = clampedU / safeSigma;
				return Mathf.Exp(-(x * x));
			}

			case FieldCurveType.Polynomial:
				return Mathf.Clamp(polyA + (polyB * clampedU) + (polyC * clampedU * clampedU), 0f, 1f);

			case FieldCurveType.CustomCurve:
				if (customCurve != null)
				{
					return Mathf.Clamp(customCurve.Sample(clampedU), 0f, 1f);
				}
				return PowOneMinusU(oneMinusU, safeGamma);

			default:
				return 1f - clampedU;
		}
	}

	public static float EvaluateEdgeRamp(float u, float edgeSoftness)
	{
		float edge = Mathf.Clamp(edgeSoftness, 0f, 1f);
		if (edge <= Epsilon)
		{
			return 1f;
		}

		float clampedU = Mathf.Clamp(u, 0f, 1f);
		float rampIn = SmoothStep(0f, edge, clampedU);
		float rampOut = 1f - SmoothStep(1f - edge, 1f, clampedU);
		return Mathf.Clamp(rampIn * rampOut, 0f, 1f);
	}

	public static float ResolveBetaEff(float globalBeta, bool overrideBetaScale, float localBetaScale)
	{
		float safeGlobal = float.IsFinite(globalBeta) ? globalBeta : 0f;
		if (!overrideBetaScale)
		{
			return safeGlobal;
		}

		if (Mathf.Abs(safeGlobal) > Epsilon)
		{
			return safeGlobal * localBetaScale;
		}

		return localBetaScale;
	}

	public static bool IsSigmaMeaningful(FieldCurveType curveType)
	{
		return curveType == FieldCurveType.Exponential;
	}

	private static float SmoothStep(float a, float b, float x)
	{
		float span = Mathf.Max(Epsilon, b - a);
		float t = Mathf.Clamp((x - a) / span, 0f, 1f);
		return t * t * (3f - (2f * t));
	}

	private static float PowOneMinusU(float oneMinusU, float gamma)
	{
		float baseValue = Mathf.Max(0f, oneMinusU);
		if (baseValue <= 0f)
		{
			if (gamma > 0f)
			{
				return 0f;
			}

			if (Mathf.IsEqualApprox(gamma, 0f))
			{
				return 1f;
			}

			baseValue = Epsilon;
		}

		return Mathf.Pow(baseValue, gamma);
	}
}
