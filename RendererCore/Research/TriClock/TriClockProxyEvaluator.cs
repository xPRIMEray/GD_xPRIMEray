using System;

namespace RendererCore.Research.TriClock;

/// <summary>
/// Proxy-only scalar evaluator for speculative three-clock analysis.
/// The formulas here are deliberately simple and traceable so DOE sweeps can
/// compare relative behavior without implying validated microphysics.
/// </summary>
public static class TriClockProxyEvaluator
{
    private const double ReducedPlanck = 1.054571817e-34;
    private const double Gravitation = 6.67430e-11;
    private const double TwoPi = Math.PI * 2.0;
    private const double RatioEpsilon = 1e-30;

    public static TriClockSample Evaluate(in TriClockConfig config)
    {
        double nuclearHz = EvaluateNuclearClockHz(config.Atom);
        double orbitalHz = EvaluateOrbitalClockHz(config.Orbital);
        double egProxy = EvaluateEgProxyJoules(config.Collapse);
        double tauProxy = config.Collapse.TauScale * ReducedPlanck / Math.Max(egProxy, RatioEpsilon);
        double collapseHz = tauProxy > RatioEpsilon ? 1.0 / tauProxy : 0.0;

        double ratio12 = SafeRatio(nuclearHz, orbitalHz);
        double ratio23 = SafeRatio(orbitalHz, collapseHz);
        double ratio13 = SafeRatio(nuclearHz, collapseHz);

        double resonanceScore = ComputeResonanceScore(ratio12, ratio23, ratio13);
        double beatScore = ComputeBeatScore(nuclearHz, orbitalHz, collapseHz);
        double crossoverScore = ComputeCrossoverScore(nuclearHz, orbitalHz, collapseHz);

        return new TriClockSample(
            (float)nuclearHz,
            (float)orbitalHz,
            (float)collapseHz,
            (float)ratio12,
            (float)ratio23,
            (float)ratio13,
            (float)resonanceScore,
            (float)beatScore,
            (float)crossoverScore,
            (float)egProxy,
            (float)tauProxy);
    }

    public static double EvaluateNuclearClockHz(in HeavyAtomProxyParams atom)
    {
        double zTerm = Math.Max(1.0, atom.AtomicNumberZ);
        double aTerm = Math.Max(1.0, atom.MassNumberA);
        double spinTerm = 1.0 + Math.Abs(atom.NuclearSpin);
        double isomerTerm = atom.HasIsomerMetadata ? 1.0 + Math.Abs(atom.IsomerEnergyOffset) : 1.0;
        return atom.NuclearClockScale * zTerm * spinTerm * isomerTerm / aTerm;
    }

    public static double EvaluateOrbitalClockHz(in OrbitalClockProxyParams orbital)
    {
        double zTerm = Math.Max(1.0, orbital.AtomicNumberZ);
        double radius = Math.Max(1e-15, orbital.EffectiveRadiusMeters);
        double contraction = Math.Max(0.01, orbital.ContractionScale + orbital.CompressionBias);
        double familyScale = ResolveOrbitalFamilyScale(orbital.OrbitalFamily);
        return orbital.OrbitalClockScale * familyScale * contraction * zTerm * zTerm / (TwoPi * radius);
    }

    public static double EvaluateEgProxyJoules(in CollapseClockProxyParams collapse)
    {
        double deltaX = Math.Max(collapse.MinimumDeltaXEpsilon, collapse.DeltaXProxyMeters);
        double mass = Math.Max(0.0, collapse.MassProxyKg);
        return collapse.EnergyScale * Gravitation * mass * mass / deltaX;
    }

    private static double ResolveOrbitalFamilyScale(string orbitalFamily)
    {
        if (string.IsNullOrWhiteSpace(orbitalFamily))
            return 1.0;

        return orbitalFamily.Trim().ToLowerInvariant() switch
        {
            "1s" => 1.00,
            "2p" => 0.85,
            "3d" => 0.70,
            "4f" => 0.55,
            "5f" => 0.50,
            "6s" => 0.65,
            _ => 1.0,
        };
    }

    private static double SafeRatio(double lhs, double rhs)
    {
        return lhs / Math.Max(Math.Abs(rhs), RatioEpsilon);
    }

    private static double ComputeResonanceScore(double ratio12, double ratio23, double ratio13)
    {
        double logDistance = Math.Abs(Math.Log10(Math.Max(ratio12, RatioEpsilon)))
            + Math.Abs(Math.Log10(Math.Max(ratio23, RatioEpsilon)))
            + Math.Abs(Math.Log10(Math.Max(ratio13, RatioEpsilon)));
        return 1.0 / (1.0 + logDistance);
    }

    private static double ComputeBeatScore(double c1, double c2, double c3)
    {
        double beat12 = Math.Abs(c1 - c2);
        double beat23 = Math.Abs(c2 - c3);
        double beat13 = Math.Abs(c1 - c3);
        return 1.0 / (1.0 + beat12 + beat23 + beat13);
    }

    private static double ComputeCrossoverScore(double c1, double c2, double c3)
    {
        double max = Math.Max(c1, Math.Max(c2, c3));
        double min = Math.Min(c1, Math.Min(c2, c3));
        return max <= RatioEpsilon ? 0.0 : 1.0 - ((max - min) / max);
    }
}
