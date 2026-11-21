namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// One input row for <see cref="MaterialBindingTable.Build"/>: either a bound descriptor or an explicit
/// unsupported reason. A key absent from the table is unbound. <c>default</c> is invalid and is rejected.
/// </summary>
public readonly record struct MaterialBindingEntry
{
    private MaterialBindingEntry(MaterialBindingKey key, bool isBound, MaterialDescriptor descriptor, MaterialUnsupportedReason reason)
    {
        Key = key;
        IsBound = isBound;
        Descriptor = descriptor;
        Reason = reason;
    }

    public MaterialBindingKey Key { get; }
    public bool IsBound { get; }

    /// <summary>The bound descriptor, or <c>default</c> for an unsupported entry.</summary>
    public MaterialDescriptor Descriptor { get; }

    /// <summary><see cref="MaterialUnsupportedReason.None"/> for a bound entry.</summary>
    public MaterialUnsupportedReason Reason { get; }

    public static MaterialBindingEntry Bound(MaterialBindingKey key, in MaterialDescriptor descriptor) =>
        new(key, true, descriptor, MaterialUnsupportedReason.None);

    public static MaterialBindingEntry Unsupported(MaterialBindingKey key, MaterialUnsupportedReason reason) =>
        new(key, false, default, reason);
}
