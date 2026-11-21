namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// A caller-owned, one-entry lookup cache. It is never shared by the table: each worker or chunk keeps its own
/// (for example, a local variable passed by <c>ref</c>). It remembers the table instance it was filled from,
/// so using it with a different table simply misses. <c>default</c> is an empty cache.
/// </summary>
public struct MaterialLookupCache
{
    internal MaterialBindingTable? Table;
    internal MaterialBindingKey Key;
    internal int Slot;

    public long Hits { get; internal set; }
    public long Misses { get; internal set; }

    public void Reset() => this = default;
}
