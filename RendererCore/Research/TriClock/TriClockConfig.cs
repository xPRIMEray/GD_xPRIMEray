using System;

namespace RendererCore.Research.TriClock;

/// <summary>
/// Portable config for the speculative tri-clock DOE sandbox.
/// This is a research scaffold for proxy sweeps and reporting, not a claim of
/// validated nuclear, relativistic, or Orch-OR physics.
/// </summary>
[Serializable]
public struct TriClockConfig
{
    public bool Enabled;
    public TriClockAnalysisMode AnalysisMode;
    public TriClockScoreMode ScoreMode;
    public bool DeterministicMode;
    public uint FixedSeed;
    public bool ExportCsv;
    public bool ExportMarkdown;
    public string OutputDir;
    public float FieldRadiusMeters;
    public int GridResolution;
    public float SpatialJitterFraction;
    public HeavyAtomProxyParams Atom;
    public OrbitalClockProxyParams Orbital;
    public CollapseClockProxyParams Collapse;

    public static TriClockConfig Defaults()
    {
        return new TriClockConfig
        {
            Enabled = false,
            AnalysisMode = TriClockAnalysisMode.Disabled,
            ScoreMode = TriClockScoreMode.ResonanceScore,
            DeterministicMode = true,
            FixedSeed = 1,
            ExportCsv = true,
            ExportMarkdown = true,
            OutputDir = string.Empty,
            FieldRadiusMeters = 1.0f,
            GridResolution = 32,
            SpatialJitterFraction = 0.0f,
            Atom = HeavyAtomProxyParams.Defaults(),
            Orbital = OrbitalClockProxyParams.Defaults(),
            Collapse = CollapseClockProxyParams.Defaults(),
        };
    }
}

[Serializable]
public struct HeavyAtomProxyParams
{
    public int AtomicNumberZ;
    public int MassNumberA;
    public float NuclearSpin;
    public bool HasIsomerMetadata;
    public float IsomerEnergyOffset;
    public float NuclearClockScale;

    public static HeavyAtomProxyParams Defaults()
    {
        return new HeavyAtomProxyParams
        {
            AtomicNumberZ = 79,
            MassNumberA = 197,
            NuclearSpin = 0.5f,
            HasIsomerMetadata = false,
            IsomerEnergyOffset = 0.0f,
            NuclearClockScale = 1.0f,
        };
    }
}

[Serializable]
public struct OrbitalClockProxyParams
{
    public int AtomicNumberZ;
    public float EffectiveRadiusMeters;
    public float ContractionScale;
    public float CompressionBias;
    public float OrbitalClockScale;
    public string OrbitalFamily;

    public static OrbitalClockProxyParams Defaults()
    {
        return new OrbitalClockProxyParams
        {
            AtomicNumberZ = 79,
            EffectiveRadiusMeters = 1e-10f,
            ContractionScale = 1.0f,
            CompressionBias = 0.0f,
            OrbitalClockScale = 1.0f,
            OrbitalFamily = "6s",
        };
    }
}

[Serializable]
public struct CollapseClockProxyParams
{
    public float MassProxyKg;
    public float DeltaXProxyMeters;
    public float EnergyScale;
    public float TauScale;
    public float MinimumDeltaXEpsilon;

    public static CollapseClockProxyParams Defaults()
    {
        return new CollapseClockProxyParams
        {
            MassProxyKg = 1e-25f,
            DeltaXProxyMeters = 1e-9f,
            EnergyScale = 1.0f,
            TauScale = 1.0f,
            MinimumDeltaXEpsilon = 1e-18f,
        };
    }
}
