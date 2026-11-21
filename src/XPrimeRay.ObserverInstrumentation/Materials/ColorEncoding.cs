namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// Encoding of descriptor colours. Values are frozen: they enter provenance serialization.
/// <see cref="SrgbEncodedDisplay"/>: authored sRGB-encoded values, written to the display-encoded
/// film unchanged, with no transfer function. A linear working space is reserved for a later milestone.
/// </summary>
public enum ColorEncoding : byte
{
    SrgbEncodedDisplay = 1,
}
