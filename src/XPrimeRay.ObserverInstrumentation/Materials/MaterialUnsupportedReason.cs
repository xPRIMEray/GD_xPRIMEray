namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// Why a known binding cannot be sampled. <see cref="None"/> is used only for bound entries.
/// Numeric values are frozen: they enter provenance serialization and index
/// <see cref="UnsupportedReasonCounts"/>.
/// </summary>
public enum MaterialUnsupportedReason : byte
{
    None = 0,
    NoPresentationMesh = 1,
    AmbiguousPresentationMesh = 2,
    MultiSurfaceMesh = 3,
    ShaderNotRecognized = 4,
    ShaderSourceHashMismatch = 5,
    ParameterMissingOrWrongType = 6,
    EmissionEnabled = 7,
    TextureBound = 8,
    VertexColorAlbedo = 9,
    TransparencyEnabled = 10,
    ConcaveFaceIndexAmbiguous = 11,
    NonFiniteParameter = 12,
}

public static class MaterialUnsupportedReasons
{
    public const int MaxDefinedValue = (int)MaterialUnsupportedReason.NonFiniteParameter;

    /// <summary>True for a defined reason other than <see cref="MaterialUnsupportedReason.None"/>.</summary>
    public static bool IsDefinedUnsupported(MaterialUnsupportedReason reason) =>
        reason != MaterialUnsupportedReason.None && (int)reason <= MaxDefinedValue;
}
