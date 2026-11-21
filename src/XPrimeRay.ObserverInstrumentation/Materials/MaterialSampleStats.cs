using System.Runtime.CompilerServices;

namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>Per-reason counters, indexed by <see cref="MaterialUnsupportedReason"/>. An inline buffer with no heap.</summary>
[InlineArray(Length)]
public struct UnsupportedReasonCounts
{
    public const int Length = 16;

    private long _element0;
}

/// <summary>
/// Value-type attribution counters for one sampling window. Each worker or chunk owns its own instance, and the
/// owner merges them with <see cref="Add"/> afterwards; nothing is shared between threads.
/// <para>Exclusive outcome counters: <see cref="Matched"/>, <see cref="Unbound"/>, <see cref="UnsupportedTotal"/>,
/// <see cref="NonFiniteHit"/>, <see cref="InvalidHitNormal"/>, <see cref="NonFiniteEvaluation"/> and
/// <see cref="NoHit"/>. Their sum is <see cref="Total"/>. <see cref="MatchedProxy"/>, <see cref="NearBoundary"/> and
/// <see cref="BiasChangedParity"/> are subsets of <see cref="Matched"/>. <see cref="ByReason"/> partitions
/// <see cref="UnsupportedTotal"/>.</para>
/// </summary>
public struct MaterialSampleStats
{
    public long Matched;
    public long MatchedProxy;
    public long Unbound;
    public long UnsupportedTotal;
    public long NonFiniteHit;
    public long InvalidHitNormal;
    public long NonFiniteEvaluation;
    public long NoHit;
    public long NearBoundary;
    public long BiasChangedParity;
    public UnsupportedReasonCounts ByReason;

    public readonly long Total =>
        Matched + Unbound + UnsupportedTotal + NonFiniteHit + InvalidHitNormal + NonFiniteEvaluation + NoHit;

    public readonly long GetUnsupported(MaterialUnsupportedReason reason) => ByReason[(int)reason];

    /// <summary>Checks the documented partition and subset invariants.</summary>
    public readonly bool IsConsistent
    {
        get
        {
            long byReason = 0;
            for (int index = 0; index < UnsupportedReasonCounts.Length; index++)
            {
                if (ByReason[index] < 0) return false;
                byReason += ByReason[index];
            }

            return ByReason[(int)MaterialUnsupportedReason.None] == 0
                && byReason == UnsupportedTotal
                && MatchedProxy <= Matched
                && NearBoundary <= Matched
                && BiasChangedParity <= Matched;
        }
    }

    public void Add(in MaterialSampleStats other)
    {
        Matched += other.Matched;
        MatchedProxy += other.MatchedProxy;
        Unbound += other.Unbound;
        UnsupportedTotal += other.UnsupportedTotal;
        NonFiniteHit += other.NonFiniteHit;
        InvalidHitNormal += other.InvalidHitNormal;
        NonFiniteEvaluation += other.NonFiniteEvaluation;
        NoHit += other.NoHit;
        NearBoundary += other.NearBoundary;
        BiasChangedParity += other.BiasChangedParity;
        for (int index = 0; index < UnsupportedReasonCounts.Length; index++)
        {
            ByReason[index] += other.ByReason[index];
        }
    }

    public readonly bool CountsEqual(in MaterialSampleStats other)
    {
        if (Matched != other.Matched || MatchedProxy != other.MatchedProxy || Unbound != other.Unbound
            || UnsupportedTotal != other.UnsupportedTotal || NonFiniteHit != other.NonFiniteHit
            || InvalidHitNormal != other.InvalidHitNormal || NonFiniteEvaluation != other.NonFiniteEvaluation
            || NoHit != other.NoHit || NearBoundary != other.NearBoundary || BiasChangedParity != other.BiasChangedParity)
        {
            return false;
        }

        for (int index = 0; index < UnsupportedReasonCounts.Length; index++)
        {
            if (ByReason[index] != other.ByReason[index]) return false;
        }

        return true;
    }

    public void Clear() => this = default;

    internal void RecordUnsupported(MaterialUnsupportedReason reason)
    {
        UnsupportedTotal++;
        ByReason[(int)reason]++;
    }
}
