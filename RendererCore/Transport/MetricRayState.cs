using System;
using System.Numerics;

namespace RendererCore.Transport;

public enum MetricFallbackCause
{
    None = 0,
    UnsupportedMetricRhs = 1,
    InvalidState = 2,
    StepRejected = 3,
    CompatibilityFallback = 4,
}

public struct MetricRayState
{
    public Vector3 Position;
    public Vector3 Direction;
    public float AffineParameter;
    public float PathLength;
    public float StepLast;
    public float ConstraintDrift;
    public float ErrorEstimate;
    public Vector3 TransportFrameU;
    public Vector3 TransportFrameV;
    public int IntegrationSteps;
    public bool Terminated;
    public bool FallbackUsed;
    public int? DominantSourceIndex;

    public static MetricRayState Initialize(Vector3 origin, Vector3 direction, float initialStep)
    {
        var state = new MetricRayState
        {
            Position = origin,
            Direction = NormalizeOrFallback(direction, Vector3.UnitZ),
            StepLast = initialStep,
            DominantSourceIndex = null,
        };

        state.ResetFrameFromDirection();
        return state;
    }

    public readonly MetricRayState Clone()
    {
        return this;
    }

    public void ResetFrameFromDirection()
    {
        Direction = NormalizeOrFallback(Direction, Vector3.UnitZ);

        var referenceAxis = MathF.Abs(Direction.Y) < 0.999f
            ? Vector3.UnitY
            : Vector3.UnitX;

        var u = Vector3.Cross(referenceAxis, Direction);
        u = NormalizeOrFallback(u, Vector3.UnitX);

        var v = Vector3.Cross(Direction, u);
        v = NormalizeOrFallback(v, Vector3.UnitY);

        TransportFrameU = u;
        TransportFrameV = v;
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

public struct MetricStepResult
{
    public MetricRayState NewState;
    public bool Accepted;
    public float RecommendedStep;
    public float ErrorEstimate;
    public float ConstraintDrift;
    public MetricFallbackCause FallbackCause;
}
