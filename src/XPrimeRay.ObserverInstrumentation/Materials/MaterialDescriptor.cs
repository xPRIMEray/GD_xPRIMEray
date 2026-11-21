namespace XPrimeRay.ObserverInstrumentation.Materials;

public enum MaterialDescriptorError : byte
{
    None = 0,
    UndefinedKind = 1,
    UndefinedEncoding = 2,
    UndefinedCorrespondence = 3,
    NonFiniteColor = 4,
    NonFiniteScale = 5,
    NonPositiveScale = 6,
    /// <summary>The field not used by <see cref="MaterialDescriptor.Kind"/> is not default.</summary>
    InactiveFieldNotDefault = 7,
}

/// <summary>
/// An immutable description of one supported material. Only the field selected by <see cref="Kind"/> is
/// meaningful. The inactive field must be <c>default</c>, so value equality equals sampling equality and
/// provenance equality. Create descriptors through <see cref="CreateFlat"/> and <see cref="CreateWorldChecker"/>.
/// </summary>
public readonly record struct MaterialDescriptor(
    MaterialKind Kind,
    Rgb Flat,
    WorldCheckerParams Checker,
    ColorEncoding Encoding,
    GeometryCorrespondence Correspondence)
{
    public static MaterialDescriptor CreateFlat(Rgb color, GeometryCorrespondence correspondence) =>
        new(MaterialKind.Flat, color, default, ColorEncoding.SrgbEncodedDisplay, correspondence);

    public static MaterialDescriptor CreateWorldChecker(in WorldCheckerParams checker, GeometryCorrespondence correspondence) =>
        new(MaterialKind.WorldSpaceChecker, default, checker, ColorEncoding.SrgbEncodedDisplay, correspondence);

    public MaterialDescriptorError Validate()
    {
        if (Encoding != ColorEncoding.SrgbEncodedDisplay) return MaterialDescriptorError.UndefinedEncoding;
        if (Correspondence is not (GeometryCorrespondence.Exact or GeometryCorrespondence.Proxy))
        {
            return MaterialDescriptorError.UndefinedCorrespondence;
        }

        switch (Kind)
        {
            case MaterialKind.Flat:
                if (Checker != default) return MaterialDescriptorError.InactiveFieldNotDefault;
                return Flat.IsFinite ? MaterialDescriptorError.None : MaterialDescriptorError.NonFiniteColor;
            case MaterialKind.WorldSpaceChecker:
                if (Flat != default) return MaterialDescriptorError.InactiveFieldNotDefault;
                if (!Checker.ColorA.IsFinite || !Checker.ColorB.IsFinite) return MaterialDescriptorError.NonFiniteColor;
                if (!float.IsFinite(Checker.Scale)) return MaterialDescriptorError.NonFiniteScale;
                return Checker.Scale > 0f ? MaterialDescriptorError.None : MaterialDescriptorError.NonPositiveScale;
            default:
                return MaterialDescriptorError.UndefinedKind;
        }
    }

    /// <summary>Replaces negative zeros with positive zeros. Sampling results are unaffected.</summary>
    public MaterialDescriptor Canonical() => this with { Flat = Flat.Canonical(), Checker = Checker.Canonical() };
}
