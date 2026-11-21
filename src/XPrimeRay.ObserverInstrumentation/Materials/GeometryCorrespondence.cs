namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// Whether the collision shape coincides with the presentation mesh (<see cref="Exact"/>) or only
/// approximates it (<see cref="Proxy"/>). Proxy bindings are still sampled but must be excluded from
/// raster parity. Values are frozen: they enter provenance serialization.
/// </summary>
public enum GeometryCorrespondence : byte
{
    Exact = 1,
    Proxy = 2,
}
