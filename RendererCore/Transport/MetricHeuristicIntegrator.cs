using System;
using System.Numerics;
using RendererCore.SceneSnapshot;

namespace RendererCore.Transport;

public sealed class MetricHeuristicIntegrator : IIntegrator
{
    private const float MinDtFloor = 1e-4f;
    private const float MaxDtCeiling = 1.0f;
    private const float MaxAccelMagnitude = 64.0f;
    private const float MaxTurnRadiansPerStep = 0.35f;
    private const float CurvatureFloor = 1e-4f;

    public float MinDt { get; }
    public float MaxDt { get; }
    public float MaxAccel { get; }
    public float MaxTurnPerStep { get; }

    public MetricHeuristicIntegrator(
        float minDt = 0.0025f,
        float maxDt = 0.05f,
        float maxAccel = 16.0f,
        float maxTurnPerStep = 0.15f)
    {
        MinDt = ClampPositiveFinite(minDt, MinDtFloor, MaxDtCeiling);
        MaxDt = MathF.Max(MinDt, ClampPositiveFinite(maxDt, MinDt, MaxDtCeiling));
        MaxAccel = ClampPositiveFinite(maxAccel, 0.01f, MaxAccelMagnitude);
        MaxTurnPerStep = ClampPositiveFinite(maxTurnPerStep, 0.001f, MaxTurnRadiansPerStep);
    }

    public StepResult Step(
        in MetricRayState state,
        float dt,
        IMetricField field,
        in RendererCore.SceneSnapshot.SceneSnapshot snapshot)
    {
        var safeDirection = NormalizeOrFallback(state.Direction, Vector3.UnitZ);
        var safeDt = SanitizeDt(dt, state.StepLast, MinDt, MaxDt);

        var accel0 = SampleAccel(field, state.Position, safeDirection, snapshot, MaxAccel);
        var turn0 = ClampMagnitude(accel0 * safeDt, MaxTurnPerStep);
        var midDirection = NormalizeOrFallback(safeDirection + (0.5f * turn0), safeDirection);
        var midPosition = state.Position + (midDirection * (0.5f * safeDt));

        var accel1 = SampleAccel(field, midPosition, midDirection, snapshot, MaxAccel);
        var blendedTurn = ClampMagnitude(((accel0 + accel1) * 0.5f) * safeDt, MaxTurnPerStep);
        var newDirection = NormalizeOrFallback(safeDirection + blendedTurn, safeDirection);
        var advanceDirection = NormalizeOrFallback(safeDirection + (0.5f * blendedTurn), safeDirection);
        var newPosition = state.Position + (advanceDirection * safeDt);

        var newState = state.Clone();
        newState.Position = newPosition;
        newState.Direction = newDirection;
        newState.PathLength = AccumulateFinite(state.PathLength, safeDt);
        newState.AffineParameter = AccumulateFinite(state.AffineParameter, safeDt);
        newState.StepLast = safeDt;
        newState.IntegrationSteps = state.IntegrationSteps + 1;
        newState.ErrorEstimate = EstimateError(accel0, accel1, safeDt, blendedTurn);
        newState.ConstraintDrift = EstimateConstraintDrift(newDirection, blendedTurn);
        newState.Terminated = state.Terminated;
        newState.FallbackUsed = state.FallbackUsed;
        newState.ResetFrameFromDirection();

        var recommendedDt = RecommendDt(safeDt, accel1.Length());
        newState.ErrorEstimate = ClampNonNegativeFinite(newState.ErrorEstimate);
        newState.ConstraintDrift = ClampNonNegativeFinite(newState.ConstraintDrift);

        return new StepResult
        {
            NewState = newState,
            ErrorEstimate = newState.ErrorEstimate,
            ConstraintDrift = newState.ConstraintDrift,
            RecommendedDt = recommendedDt,
        };
    }

    private static Vector3 SampleAccel(
        IMetricField field,
        Vector3 position,
        Vector3 direction,
        in RendererCore.SceneSnapshot.SceneSnapshot snapshot,
        float maxAccel)
    {
        if (field == null)
        {
            return Vector3.Zero;
        }

        var accel = field.AccelAt(position, snapshot);
        if (!IsFinite(accel))
        {
            return Vector3.Zero;
        }

        // TODO: Replace this 3-space perpendicular projection with metric-compatible
        // transport once the bounded geodesic RHS is available.
        accel -= direction * Vector3.Dot(accel, direction);
        return ClampMagnitude(accel, maxAccel);
    }

    private float RecommendDt(float currentDt, float accelMagnitude)
    {
        // TODO: Replace this curvature heuristic with an error-controlled policy once
        // the metric integrator exposes a real local truncation estimate.
        var curvatureScale = 1.0f / (CurvatureFloor + accelMagnitude);
        var suggested = currentDt * (0.75f + 0.5f * curvatureScale);
        return Math.Clamp(suggested, MinDt, MaxDt);
    }

    private static float EstimateError(Vector3 accel0, Vector3 accel1, float dt, Vector3 appliedTurn)
    {
        // TODO: Placeholder bridge estimate until an embedded pair or Hamiltonian
        // constraint monitor replaces this heuristic.
        var accelDelta = (accel1 - accel0).Length();
        var turnPenalty = MathF.Max(0.0f, appliedTurn.Length() - 0.5f * MaxTurnRadiansPerStep);
        return ClampNonNegativeFinite((accelDelta * dt * dt * 0.5f) + turnPenalty);
    }

    private static float EstimateConstraintDrift(Vector3 direction, Vector3 appliedTurn)
    {
        // TODO: This is only a normalization drift proxy. Replace with a true null
        // constraint residual when the transport state gains metric momentum.
        var normDrift = MathF.Abs(direction.LengthSquared() - 1.0f);
        var turnDrift = appliedTurn.Length() / MaxTurnRadiansPerStep;
        return ClampNonNegativeFinite(normDrift + (0.01f * turnDrift));
    }

    private static float SanitizeDt(float requestedDt, float previousDt, float minDt, float maxDt)
    {
        var candidate = requestedDt;
        if (!float.IsFinite(candidate) || candidate <= 0.0f)
        {
            candidate = previousDt;
        }

        if (!float.IsFinite(candidate) || candidate <= 0.0f)
        {
            candidate = minDt;
        }

        return Math.Clamp(candidate, minDt, maxDt);
    }

    private static float AccumulateFinite(float value, float delta)
    {
        if (!float.IsFinite(value))
        {
            value = 0.0f;
        }

        return value + delta;
    }

    private static float ClampPositiveFinite(float value, float minValue, float maxValue)
    {
        if (!float.IsFinite(value))
        {
            return minValue;
        }

        return Math.Clamp(value, minValue, maxValue);
    }

    private static float ClampNonNegativeFinite(float value)
    {
        if (!float.IsFinite(value) || value < 0.0f)
        {
            return 0.0f;
        }

        return value;
    }

    private static Vector3 ClampMagnitude(Vector3 value, float maxLength)
    {
        var lengthSq = value.LengthSquared();
        if (lengthSq <= 0.0f || !float.IsFinite(lengthSq))
        {
            return Vector3.Zero;
        }

        var maxLengthSq = maxLength * maxLength;
        if (lengthSq <= maxLengthSq)
        {
            return value;
        }

        var scale = maxLength / MathF.Sqrt(lengthSq);
        return value * scale;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        var lengthSq = value.LengthSquared();
        if (lengthSq <= 1e-20f || !float.IsFinite(lengthSq))
        {
            return fallback;
        }

        return value / MathF.Sqrt(lengthSq);
    }
}
