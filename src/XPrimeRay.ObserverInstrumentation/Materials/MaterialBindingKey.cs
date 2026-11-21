namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// Identifies one collision shape of one collider: the Godot collider instance id and the
/// shape index within that collider. Ordering (collider id, then shape index) is the canonical
/// table order used for lookup and for provenance serialization.
/// </summary>
public readonly record struct MaterialBindingKey(ulong ColliderInstanceId, int ShapeIndex)
    : IComparable<MaterialBindingKey>
{
    /// <summary>Godot instance ids are never zero; shape indices are never negative.</summary>
    public bool IsValid => ColliderInstanceId != 0UL && ShapeIndex >= 0;

    public int CompareTo(MaterialBindingKey other)
    {
        int byCollider = ColliderInstanceId.CompareTo(other.ColliderInstanceId);
        return byCollider != 0 ? byCollider : ShapeIndex.CompareTo(other.ShapeIndex);
    }
}
