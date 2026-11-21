namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// Attribution of one sample. Value 0 is deliberately unassigned so that a default
/// <see cref="MaterialSampleResult"/> is never mistaken for a match. Values are frozen.
/// </summary>
public enum MaterialSampleStatus : byte
{
    Matched = 1,
    Unbound = 2,
    Unsupported = 3,
    /// <summary>The hit position or hit normal has a NaN or infinite component.</summary>
    NonFiniteHit = 4,
    NoHit = 5,
    /// <summary>A finite but zero-length hit normal where the material needs one (checker inward bias).</summary>
    InvalidHitNormal = 6,
    /// <summary>Finite inputs produced a nonfinite intermediate (for example position × scale overflow).</summary>
    NonFiniteEvaluation = 7,
}
