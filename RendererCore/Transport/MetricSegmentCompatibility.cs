using System;
using Godot;
using NumericsVector3 = System.Numerics.Vector3;

namespace RendererCore.Transport;

// Portable transport-side mirror of the renderer RaySeg contract.
public struct RaySegCompatibleSegmentPayload
{
    public NumericsVector3 Start;
    public NumericsVector3 End;
    public float TraveledB;
    public float RadiusBound;

    public readonly RayBeamRenderer.RaySeg ToRaySeg()
    {
        return new RayBeamRenderer.RaySeg
        {
            A = MetricSegmentCompatibility.ToGodot(Start),
            B = MetricSegmentCompatibility.ToGodot(End),
            TraveledB = TraveledB,
            RadiusBound = RadiusBound,
        };
    }
}

public struct MetricSegmentMetadata
{
    public NumericsVector3 StartTangent;
    public NumericsVector3 EndTangent;
    public float AcceptedStepSize;
    public float ErrorEstimate;
    public float RadiusBound;
    public float ConstraintDrift;
    public int SegmentIndex;
}

public struct MetricSegmentChainResult
{
    public RaySegCompatibleSegmentPayload[] Segments;
    public MetricSegmentMetadata[] Metadata;
    public int Count;
    public bool TerminatedEarly;
    public bool FallbackUsed;
}

public readonly struct MetricSegmentEmission
{
    public readonly RaySegCompatibleSegmentPayload Segment;
    public readonly MetricSegmentMetadata Metadata;

    public MetricSegmentEmission(
        in RaySegCompatibleSegmentPayload segment,
        in MetricSegmentMetadata metadata)
    {
        Segment = segment;
        Metadata = metadata;
    }

    public RayBeamRenderer.RaySeg ToRaySeg()
    {
        return Segment.ToRaySeg();
    }
}

public static class MetricSegmentCompatibility
{
    public static void BuildSegmentPayloads(
        in MetricRayState previousState,
        in MetricRayState nextState,
        float acceptedStepSize,
        float errorEstimate,
        float constraintDrift,
        float radiusBound,
        int segmentIndex,
        out RaySegCompatibleSegmentPayload segment,
        out MetricSegmentMetadata metadata)
    {
        var safeAcceptedStep = ClampNonNegativeFinite(acceptedStepSize);
        var safeRadiusBound = ClampNonNegativeFinite(radiusBound);

        segment = new RaySegCompatibleSegmentPayload
        {
            Start = previousState.Position,
            End = nextState.Position,
            TraveledB = ClampNonNegativeFinite(nextState.PathLength),
            RadiusBound = safeRadiusBound,
        };

        metadata = new MetricSegmentMetadata
        {
            StartTangent = NormalizeOrFallback(previousState.Direction, NumericsVector3.UnitZ),
            EndTangent = NormalizeOrFallback(nextState.Direction, NumericsVector3.UnitZ),
            AcceptedStepSize = safeAcceptedStep,
            ErrorEstimate = ClampNonNegativeFinite(errorEstimate),
            RadiusBound = safeRadiusBound,
            ConstraintDrift = ClampNonNegativeFinite(constraintDrift),
            SegmentIndex = Math.Max(0, segmentIndex),
        };
    }

    public static void BuildSegmentPayloads(
        in MetricRayState previousState,
        in StepResult step,
        float radiusBound,
        int segmentIndex,
        out RaySegCompatibleSegmentPayload segment,
        out MetricSegmentMetadata metadata)
    {
        BuildSegmentPayloads(
            previousState,
            step.NewState,
            step.NewState.StepLast,
            step.ErrorEstimate,
            step.ConstraintDrift,
            radiusBound,
            segmentIndex,
            out segment,
            out metadata);
    }

    public static void BuildSegmentPayloads(
        in MetricRayState previousState,
        in MetricStepResult step,
        float radiusBound,
        int segmentIndex,
        out RaySegCompatibleSegmentPayload segment,
        out MetricSegmentMetadata metadata)
    {
        BuildSegmentPayloads(
            previousState,
            step.NewState,
            step.NewState.StepLast,
            step.ErrorEstimate,
            step.ConstraintDrift,
            radiusBound,
            segmentIndex,
            out segment,
            out metadata);
    }

    public static MetricSegmentEmission BuildEmission(
        in MetricRayState previousState,
        in MetricRayState nextState,
        float acceptedStepSize,
        float errorEstimate,
        float constraintDrift,
        float radiusBound,
        int segmentIndex)
    {
        BuildSegmentPayloads(
            previousState,
            nextState,
            acceptedStepSize,
            errorEstimate,
            constraintDrift,
            radiusBound,
            segmentIndex,
            out var segment,
            out var metadata);
        return new MetricSegmentEmission(segment, metadata);
    }

    public static MetricSegmentEmission BuildEmission(
        in MetricRayState previousState,
        in StepResult step,
        float radiusBound,
        int segmentIndex)
    {
        return BuildEmission(
            previousState,
            step.NewState,
            step.NewState.StepLast,
            step.ErrorEstimate,
            step.ConstraintDrift,
            radiusBound,
            segmentIndex);
    }

    public static MetricSegmentEmission BuildEmission(
        in MetricRayState previousState,
        in MetricStepResult step,
        float radiusBound,
        int segmentIndex)
    {
        return BuildEmission(
            previousState,
            step.NewState,
            step.NewState.StepLast,
            step.ErrorEstimate,
            step.ConstraintDrift,
            radiusBound,
            segmentIndex);
    }

    public static NumericsVector3 NormalizeOrFallback(NumericsVector3 value, NumericsVector3 fallback)
    {
        var lengthSq = value.LengthSquared();
        if (lengthSq <= 1e-20f || !float.IsFinite(lengthSq))
        {
            return fallback;
        }

        return value / MathF.Sqrt(lengthSq);
    }

    public static Godot.Vector3 ToGodot(NumericsVector3 value)
    {
        return new Godot.Vector3(value.X, value.Y, value.Z);
    }

    public static NumericsVector3 ToNumerics(Godot.Vector3 value)
    {
        return new NumericsVector3(value.X, value.Y, value.Z);
    }

    // TODO(metric-pass1): Thread MetricSegmentMetadata sidecar storage through
    // BuildRaySegmentsCamera_Pass1 without changing RaySeg[] consumers.
    // TODO(metric-pass1-probes): Allow pass-1 quick probes to read accepted-step/error
    // metadata while keeping current probe hit-testing behavior unchanged.
    // TODO(metric-pass2): Feed pass-2 narrowphase/adaptive subdivision from the emitted
    // metadata once metric-aware hit testing is introduced.

    private static float ClampNonNegativeFinite(float value)
    {
        if (!float.IsFinite(value) || value < 0.0f)
        {
            return 0.0f;
        }

        return value;
    }
}
