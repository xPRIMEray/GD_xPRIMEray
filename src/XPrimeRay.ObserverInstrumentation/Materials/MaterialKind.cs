namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>Supported material forms. Numeric values are frozen: they enter provenance serialization.</summary>
public enum MaterialKind : byte
{
    WorldSpaceChecker = 1,
    Flat = 2,
}
