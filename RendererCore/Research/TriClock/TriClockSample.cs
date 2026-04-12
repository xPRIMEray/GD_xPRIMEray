namespace RendererCore.Research.TriClock;

/// <summary>
/// Single scalar sample emitted by the tri-clock proxy evaluator.
/// Ratios and scores are heuristic comparison helpers for DOE work.
/// </summary>
public readonly struct TriClockSample
{
    public readonly float NuclearClockHz;
    public readonly float OrbitalClockHz;
    public readonly float CollapseClockHz;
    public readonly float Ratio12;
    public readonly float Ratio23;
    public readonly float Ratio13;
    public readonly float ResonanceScore;
    public readonly float BeatScore;
    public readonly float CrossoverScore;
    public readonly float EgProxyJoules;
    public readonly float TauProxySeconds;

    public TriClockSample(
        float nuclearClockHz,
        float orbitalClockHz,
        float collapseClockHz,
        float ratio12,
        float ratio23,
        float ratio13,
        float resonanceScore,
        float beatScore,
        float crossoverScore,
        float egProxyJoules,
        float tauProxySeconds)
    {
        NuclearClockHz = nuclearClockHz;
        OrbitalClockHz = orbitalClockHz;
        CollapseClockHz = collapseClockHz;
        Ratio12 = ratio12;
        Ratio23 = ratio23;
        Ratio13 = ratio13;
        ResonanceScore = resonanceScore;
        BeatScore = beatScore;
        CrossoverScore = crossoverScore;
        EgProxyJoules = egProxyJoules;
        TauProxySeconds = tauProxySeconds;
    }
}
