using System.Numerics;

namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// Pure, allocation-free, string-free sampling of one resolved hit against an immutable
/// <see cref="MaterialBindingTable"/>. It is stateless: callers own the <see cref="MaterialLookupCache"/> and
/// <see cref="MaterialSampleStats"/>.
/// <para>Attribution precedence, each sample receiving exactly one status:</para>
/// <list type="number">
/// <item><c>!hadHit</c> gives NoHit.</item>
/// <item>A nonfinite hit position or normal gives NonFiniteHit.</item>
/// <item>A key absent from the table gives Unbound.</item>
/// <item>A key present but unsupported gives Unsupported, with its reason.</item>
/// <item>A Flat binding gives Matched, with the authored colour (the normal is not used).</item>
/// <item>A WorldSpaceChecker binding is evaluated with the presentation convention. A zero-length normal gives
/// InvalidHitNormal; a nonfinite intermediate gives NonFiniteEvaluation; otherwise Matched.</item>
/// </list>
/// <para>The hit position is never modified. The checker's inward bias exists only inside the evaluator's
/// presentation path.</para>
/// </summary>
public static class MaterialSampler
{
    public static MaterialSampleResult Sample(
        MaterialBindingTable table,
        bool hadHit,
        ulong colliderInstanceId,
        int shapeIndex,
        Vector3 hitPosition,
        Vector3 hitNormal,
        ref MaterialSampleStats stats)
    {
        var cache = default(MaterialLookupCache);
        return Sample(table, hadHit, colliderInstanceId, shapeIndex, hitPosition, hitNormal, ref cache, ref stats);
    }

    public static MaterialSampleResult Sample(
        MaterialBindingTable table,
        bool hadHit,
        ulong colliderInstanceId,
        int shapeIndex,
        Vector3 hitPosition,
        Vector3 hitNormal,
        ref MaterialLookupCache cache,
        ref MaterialSampleStats stats)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (!hadHit)
        {
            stats.NoHit++;
            return MaterialSampleResult.Unmatched(
                MaterialSampleStatus.NoHit, MaterialSamplingConvention.InvalidColor, MaterialUnsupportedReason.None);
        }

        if (!WorldCheckerEvaluator.IsFinite(hitPosition) || !WorldCheckerEvaluator.IsFinite(hitNormal))
        {
            stats.NonFiniteHit++;
            return MaterialSampleResult.Unmatched(
                MaterialSampleStatus.NonFiniteHit, MaterialSamplingConvention.InvalidColor, MaterialUnsupportedReason.None);
        }

        MaterialBindingLookup lookup = table.Lookup(
            new MaterialBindingKey(colliderInstanceId, shapeIndex),
            ref cache,
            out int descriptorIndex,
            out MaterialUnsupportedReason reason);
        if (lookup == MaterialBindingLookup.Unbound)
        {
            stats.Unbound++;
            return MaterialSampleResult.Unmatched(
                MaterialSampleStatus.Unbound, MaterialSamplingConvention.UnboundColor, MaterialUnsupportedReason.None);
        }

        if (lookup == MaterialBindingLookup.Unsupported)
        {
            stats.RecordUnsupported(reason);
            return MaterialSampleResult.Unmatched(
                MaterialSampleStatus.Unsupported, MaterialSamplingConvention.UnsupportedColor, reason);
        }

        ref readonly MaterialDescriptor descriptor = ref table.DescriptorRef(descriptorIndex);
        if (descriptor.Kind == MaterialKind.Flat)
        {
            RecordMatched(ref stats, in descriptor, nearBoundary: false, biasChangedParity: false);
            return new MaterialSampleResult(
                MaterialSampleStatus.Matched, descriptor.Flat, descriptorIndex, MaterialUnsupportedReason.None,
                false, -1f, -1, false);
        }

        WorldCheckerEvaluation evaluation = WorldCheckerEvaluator.EvaluatePresentation(
            descriptor.Checker, hitPosition, hitNormal, out WorldCheckerPresentationSample sample);
        switch (evaluation)
        {
            case WorldCheckerEvaluation.Ok:
                RecordMatched(ref stats, in descriptor, sample.NearCellBoundary, sample.BiasChangedParity);
                return new MaterialSampleResult(
                    MaterialSampleStatus.Matched, sample.Color, descriptorIndex, MaterialUnsupportedReason.None,
                    sample.NearCellBoundary, sample.HitBoundaryDistanceCells, sample.Parity, sample.BiasChangedParity);
            case WorldCheckerEvaluation.InvalidNormal:
                stats.InvalidHitNormal++;
                return MaterialSampleResult.Unmatched(
                    MaterialSampleStatus.InvalidHitNormal, MaterialSamplingConvention.InvalidColor, MaterialUnsupportedReason.None);
            default:
                // NonFiniteIntermediate. InvalidParameters and NonFiniteInput are excluded earlier, by table
                // validation and by the finite-hit check, and are attributed here defensively.
                stats.NonFiniteEvaluation++;
                return MaterialSampleResult.Unmatched(
                    MaterialSampleStatus.NonFiniteEvaluation, MaterialSamplingConvention.InvalidColor, MaterialUnsupportedReason.None);
        }
    }

    private static void RecordMatched(
        ref MaterialSampleStats stats, in MaterialDescriptor descriptor, bool nearBoundary, bool biasChangedParity)
    {
        stats.Matched++;
        if (descriptor.Correspondence == GeometryCorrespondence.Proxy) stats.MatchedProxy++;
        if (nearBoundary) stats.NearBoundary++;
        if (biasChangedParity) stats.BiasChangedParity++;
    }
}
