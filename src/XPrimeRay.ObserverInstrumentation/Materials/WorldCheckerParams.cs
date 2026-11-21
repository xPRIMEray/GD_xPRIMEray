namespace XPrimeRay.ObserverInstrumentation.Materials;

/// <summary>
/// Parameters of <c>res://Experiences/checkerboard_3d.gdshader</c>: <c>color_a</c>, <c>color_b</c> and
/// <c>checker_scale</c>. Valid only with finite colours and a finite, strictly positive scale.
/// </summary>
public readonly record struct WorldCheckerParams(Rgb ColorA, Rgb ColorB, float Scale)
{
    public bool IsValid => ColorA.IsFinite && ColorB.IsFinite && float.IsFinite(Scale) && Scale > 0f;

    public WorldCheckerParams Canonical() => new(ColorA.Canonical(), ColorB.Canonical(), Scale);
}
