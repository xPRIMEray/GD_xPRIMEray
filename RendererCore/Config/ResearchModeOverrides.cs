using System;
using System.Runtime.CompilerServices;

namespace RendererCore.Config;

/// <summary>
/// Optional overrides for ResearchModeConfig.
///
/// Pattern: per-field override booleans avoid ambiguity with default values
/// (especially important when driven by editor/inspector tools).
/// </summary>
[Serializable]
public struct ResearchModeOverrides
{
    public bool Enabled;

    public bool Override_ResearchEnabled;
    public bool ResearchEnabled;

    public bool Override_ResearchTier;
    public ResearchTier ResearchTier;

    public bool Override_TransportModel;
    public TransportModel TransportModel;

    public bool Override_IntegratorKind;
    public IntegratorKind IntegratorKind;

    public bool Override_AbsPosTol;
    public float AbsPosTol;

    public bool Override_RelTol;
    public float RelTol;

    public bool Override_DtMin;
    public float DtMin;

    public bool Override_DtMax;
    public float DtMax;

    public bool Override_MaxStepsPerRay;
    public int MaxStepsPerRay;

    public bool Override_TrackNullConstraint;
    public bool TrackNullConstraint;

    public bool Override_NullConstraintTol;
    public float NullConstraintTol;

    public bool Override_EnableConstraintProjection;
    public bool EnableConstraintProjection;

    public bool Override_MaxConstraintProjectionIters;
    public int MaxConstraintProjectionIters;

    public bool Override_DeterministicMode;
    public bool DeterministicMode;

    public bool Override_FixedSeed;
    public uint FixedSeed;

    public bool Override_MaxWorkPerFrameMs;
    public int MaxWorkPerFrameMs;

    public bool Override_ValidationEnabled;
    public bool ValidationEnabled;

    public bool Override_ValidationSuite;
    public ValidationSuite ValidationSuite;

    public bool Override_ValidationReportPath;
    public string ValidationReportPath;
}

public static class ResearchModeMerge
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ResearchModeConfig Apply(in ResearchModeConfig baseCfg, in ResearchModeOverrides ov)
    {
        if (!ov.Enabled)
            return baseCfg;

        var cfg = baseCfg;

        if (ov.Override_ResearchEnabled) cfg.ResearchEnabled = ov.ResearchEnabled;
        if (ov.Override_ResearchTier) cfg.ResearchTier = ov.ResearchTier;
        if (ov.Override_TransportModel) cfg.TransportModel = ov.TransportModel;
        if (ov.Override_IntegratorKind) cfg.IntegratorKind = ov.IntegratorKind;

        if (ov.Override_AbsPosTol) cfg.AbsPosTol = ov.AbsPosTol;
        if (ov.Override_RelTol) cfg.RelTol = ov.RelTol;
        if (ov.Override_DtMin) cfg.DtMin = ov.DtMin;
        if (ov.Override_DtMax) cfg.DtMax = ov.DtMax;
        if (ov.Override_MaxStepsPerRay) cfg.MaxStepsPerRay = ov.MaxStepsPerRay;

        if (ov.Override_TrackNullConstraint) cfg.TrackNullConstraint = ov.TrackNullConstraint;
        if (ov.Override_NullConstraintTol) cfg.NullConstraintTol = ov.NullConstraintTol;
        if (ov.Override_EnableConstraintProjection) cfg.EnableConstraintProjection = ov.EnableConstraintProjection;
        if (ov.Override_MaxConstraintProjectionIters) cfg.MaxConstraintProjectionIters = ov.MaxConstraintProjectionIters;

        if (ov.Override_DeterministicMode) cfg.DeterministicMode = ov.DeterministicMode;
        if (ov.Override_FixedSeed) cfg.FixedSeed = ov.FixedSeed;
        if (ov.Override_MaxWorkPerFrameMs) cfg.MaxWorkPerFrameMs = ov.MaxWorkPerFrameMs;

        if (ov.Override_ValidationEnabled) cfg.ValidationEnabled = ov.ValidationEnabled;
        if (ov.Override_ValidationSuite) cfg.ValidationSuite = ov.ValidationSuite;
        if (ov.Override_ValidationReportPath) cfg.ValidationReportPath = ov.ValidationReportPath ?? string.Empty;

        return cfg;
    }
}
