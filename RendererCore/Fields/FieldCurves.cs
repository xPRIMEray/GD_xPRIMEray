using System;

namespace RendererCore.Fields;

public static class FieldCurves
{
    public static float Eval(FieldCurveType type, float u, float a, float b, float c, bool clamp01)
    {
        u = Clamp01(u);

        var value = type switch
        {
            FieldCurveType.Linear => 1f - u,
            FieldCurveType.Power => MathF.Pow(1f - u, a),
            FieldCurveType.Polynomial => a + (b * u) + (c * u * u),
            FieldCurveType.Exponential => MathF.Exp(-a * u),
            FieldCurveType.AtomicOrbital => EvalAtomicOrbital(u, a, b, c),
            _ => 1f - u
        };

        if (clamp01)
        {
            value = Clamp01(value);
        }

        return value;
    }

    private static float Clamp01(float v)
    {
        if (v < 0f) return 0f;
        if (v > 1f) return 1f;
        return v;
    }

    private static float EvalAtomicOrbital(float u, float electronCount, float orbitalRadius, float modulation)
    {
        var radius = MathF.Max(1e-6f, orbitalRadius);
        var r = MathF.Max(0f, u * radius);
        var count = Math.Clamp(MathF.Round(electronCount), 0f, 3f);
        var density = count <= 0f ? 0f : MathF.Exp((-2f * r) / radius);
        return Clamp01(density * MathF.Max(0f, modulation));
    }
}
