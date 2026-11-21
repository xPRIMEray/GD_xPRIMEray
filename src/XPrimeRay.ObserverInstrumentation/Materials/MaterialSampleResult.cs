namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// Result of one <see cref="MaterialSampler"/> call.
/// <para><see cref="Color"/>: the authored colour when <see cref="MaterialSampleStatus.Matched"/>; otherwise the
/// <see cref="MaterialSamplingConvention"/> sentinel. For <see cref="MaterialSampleStatus.NoHit"/> the caller
/// substitutes its own sky colour.</para>
/// <para><see cref="DescriptorIndex"/>: the index into the table's descriptors when matched; otherwise -1.</para>
/// <para><see cref="NearCellBoundary"/>, <see cref="BoundaryDistanceCells"/>, <see cref="Parity"/> and
/// <see cref="BiasChangedParity"/> apply only to matched checker samples. The distance is measured at the
/// original (unbiased) hit position; the parity is the presentation (inward-biased) parity. In every other
/// case they are false, -1, -1 and false.</para>
/// </summary>
public readonly record struct MaterialSampleResult(
    MaterialSampleStatus Status,
    Rgb Color,
    int DescriptorIndex,
    MaterialUnsupportedReason Reason,
    bool NearCellBoundary,
    float BoundaryDistanceCells,
    int Parity,
    bool BiasChangedParity)
{
    internal static MaterialSampleResult Unmatched(
        MaterialSampleStatus status, Rgb color, MaterialUnsupportedReason reason) =>
        new(status, color, -1, reason, false, -1f, -1, false);
}
