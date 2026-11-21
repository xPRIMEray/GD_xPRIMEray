namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// Presentation conventions of the CPU material path. These are documented conventions, not shader semantics.
/// <para><see cref="InwardBiasMeters"/>: the checker is evaluated at <c>hit − n̂·δ</c>, so that faces lying exactly
/// on a cell plane select the cell on the solid side deterministically. This is NOT exactly equivalent to the
/// shader at every coordinate. Within δ·scale cells of a cell plane, the presentation parity can differ from the
/// shader's parity at the unbiased hit. Every such sample is reported through
/// <see cref="MaterialSampleResult.BiasChangedParity"/>, and the unbiased boundary distance is reported
/// separately. In fp32, once the ulp of a hit coordinate exceeds δ (|coordinate| above about 2048 m), the bias
/// may no longer move the position at all.</para>
/// <para><see cref="NearBoundaryCells"/>: a matched checker sample whose unbiased hit lies closer than this to a
/// cell plane on any axis is flagged with <see cref="MaterialSampleResult.NearCellBoundary"/>.</para>
/// </summary>
public static class MaterialSamplingConvention
{
    public const float InwardBiasMeters = 1e-4f;
    public const float NearBoundaryCells = 1e-3f;

    public static readonly Rgb UnboundColor = new(0.25f, 0.25f, 0.25f);
    public static readonly Rgb UnsupportedColor = new(1f, 0f, 1f);

    /// <summary>Used for NonFiniteHit, InvalidHitNormal and NonFiniteEvaluation, and returned with NoHit.</summary>
    public static readonly Rgb InvalidColor = new(0f, 0f, 0f);
}
