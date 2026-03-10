using System;

namespace RendererCore.Config;

public enum ResearchTier
{
    Tier0_Preview = 0,
    Tier1_ErrorBounded = 1,
    Tier2_InvariantPreserving = 2,
}

public enum TransportModel
{
    GRIN_Optical = 0,
    Metric_NullGeodesic = 1,
    Hybrid_Research = 2,
    Metric_Adapter_EffectiveMedium = Hybrid_Research,
}

public enum MetricSteeringLaw
{
    MetricLaw_CurrentEnvelope = 0,
    MetricLaw_ImpactParameterApprox = 1,
}

public enum IntegratorKind
{
    Heuristic = 0,
    RK45_Embedded = 1,
    Symplectic = 2,
}

public enum ValidationSuite
{
    QuickSanity = 0,
    FullRegression = 1,
    MetricBenchmarks = 2,
}

/// <summary>
/// Research-grade controls for curved-ray transport.
///
/// Design goals:
/// - Explicit tolerances and invariant knobs for academic comparability.
/// - Portable: no Godot dependencies.
/// - Cheap to ignore: when ResearchEnabled=false, callers may treat this as inert.
/// </summary>
[Serializable]
public struct ResearchModeConfig
{
    // Top-level switches
    public bool ResearchEnabled;
    public ResearchTier ResearchTier;
    public TransportModel TransportModel;
    public IntegratorKind IntegratorKind;

    // Adaptive stepping tolerances (Tier1+)
    public float AbsPosTol;
    public float RelTol;
    public float DtMin;
    public float DtMax;
    public int MaxStepsPerRay;

    // Invariants (Metric mode)
    public bool TrackNullConstraint;
    public float NullConstraintTol;
    public bool EnableConstraintProjection;
    public int MaxConstraintProjectionIters;

    // Reproducibility
    public bool DeterministicMode;
    public uint FixedSeed;

    // Budget behavior (determinism may constrain yield points)
    public int MaxWorkPerFrameMs;

    // Validation harness
    public bool ValidationEnabled;
    public ValidationSuite ValidationSuite;
    public string ValidationReportPath;

    public static ResearchModeConfig DefaultsPreview()
    {
        return new ResearchModeConfig
        {
            ResearchEnabled = false,
            ResearchTier = ResearchTier.Tier0_Preview,
            TransportModel = TransportModel.GRIN_Optical,
            IntegratorKind = IntegratorKind.Heuristic,

            AbsPosTol = 1e-3f,
            RelTol = 1e-4f,
            DtMin = 1e-4f,
            DtMax = 0.1f,
            MaxStepsPerRay = 2048,

            TrackNullConstraint = false,
            NullConstraintTol = 1e-3f,
            EnableConstraintProjection = false,
            MaxConstraintProjectionIters = 2,

            DeterministicMode = false,
            FixedSeed = 1,

            MaxWorkPerFrameMs = 16,

            ValidationEnabled = false,
            ValidationSuite = ValidationSuite.QuickSanity,
            ValidationReportPath = string.Empty,
        };
    }
}
