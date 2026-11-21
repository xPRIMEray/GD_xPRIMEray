namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>An RGB triple exactly as authored. No conversion, clamping or quantization is applied here.</summary>
public readonly record struct Rgb(float R, float G, float B)
{
    public bool IsFinite => float.IsFinite(R) && float.IsFinite(G) && float.IsFinite(B);

    /// <summary>Replaces negative zero with positive zero; every other value is unchanged.</summary>
    public Rgb Canonical() => new(CanonicalZero(R), CanonicalZero(G), CanonicalZero(B));

    internal static float CanonicalZero(float value) => value == 0f ? 0f : value;
}
