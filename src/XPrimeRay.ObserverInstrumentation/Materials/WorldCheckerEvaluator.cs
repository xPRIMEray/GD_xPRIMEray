using System.Numerics;

namespace XPrimeRay.ObserverInstrumentation.Materials;

public enum WorldCheckerEvaluation : byte
{
    Ok = 1,
    InvalidParameters = 2,
    NonFiniteInput = 3,
    InvalidNormal = 4,
    NonFiniteIntermediate = 5,
}

/// <summary>Unbiased shader evaluation at one world position.</summary>
public readonly record struct WorldCheckerSample(Rgb Color, int Parity, float BoundaryDistanceCells);

/// <summary>
/// Presentation evaluation. <see cref="Color"/> and <see cref="Parity"/> come from the inward-biased position.
/// <see cref="ShaderParityAtHit"/> and <see cref="HitBoundaryDistanceCells"/> come from the original, unbiased hit.
/// </summary>
public readonly record struct WorldCheckerPresentationSample(
    Rgb Color,
    int Parity,
    int ShaderParityAtHit,
    float HitBoundaryDistanceCells,
    bool NearCellBoundary,
    bool BiasChangedParity);

/// <summary>
/// CPU replica of <c>res://Experiences/checkerboard_3d.gdshader</c>, pinned to its exact source by
/// <see cref="ShaderSourceSha256"/>:
/// <code>
/// world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
/// vec3 cell = floor(world_position * checker_scale);
/// float checker = mod(cell.x + cell.y + cell.z, 2.0);
/// ALBEDO = mix(color_a, color_b, checker);
/// </code>
/// It uses fp32 throughout: per-axis <c>q = p·scale</c>, <see cref="MathF.Floor"/> (never an integer cast, which
/// truncates negatives toward zero), the sum <c>(cx + cy) + cz</c> in shader order, and GLSL
/// <c>mod(x, y) = x − y·floor(x/y)</c> (never C# <c>%</c>, which keeps the dividend's sign). Because
/// <c>checker</c> is exactly 0 or 1, colour selection is exact: parity 0 gives <c>color_a</c>, and 1 gives
/// <c>color_b</c>. A GPU <c>mix</c> may differ from the endpoint by an ulp; the CPU side does not interpolate.
/// <para>Beyond 2^23 cells on an axis, fp32 has no fractional part. Parity then degenerates exactly as in fp32
/// shader arithmetic, and the boundary distance reads 0, so such samples are flagged near-boundary.</para>
/// </summary>
public static class WorldCheckerEvaluator
{
    public const string ShaderResourcePath = "res://Experiences/checkerboard_3d.gdshader";

    /// <summary>SHA-256 of the shader file's bytes, equal to Godot's <c>Shader.code</c> as UTF-8 (683 bytes, LF).</summary>
    public const string ShaderSourceSha256 = "f9f8e05b67fdf8594888b979d8ae3a35f5ca80fdde52d7c3d91b17e24a4c46e8";

    public const int ShaderSourceUtf8Length = 683;

    /// <summary>
    /// Uniform defaults pinned together with <see cref="ShaderSourceSha256"/>. Godot reports a null value for an
    /// unset parameter, so a binding builder uses these values in that case.
    /// </summary>
    public static readonly WorldCheckerParams ShaderDefaults =
        new(new Rgb(0.04f, 0.05f, 0.08f), new Rgb(0.9f, 0.92f, 0.95f), 8.0f);

    /// <summary>Pinned for completeness. Irrelevant to unlit base colour.</summary>
    public const float ShaderDefaultRoughness = 0.72f;

    /// <summary>Pinned for completeness. Irrelevant to unlit base colour.</summary>
    public const float ShaderDefaultMetallic = 0.0f;

    /// <summary>GLSL <c>mod</c>: <c>x − y·floor(x/y)</c>. The result has the sign of <paramref name="y"/>.</summary>
    public static float GlslMod(float x, float y) => x - y * MathF.Floor(x / y);

    /// <summary>Exact shader mathematics at <paramref name="worldPosition"/>, with no bias.</summary>
    public static WorldCheckerEvaluation EvaluateShader(
        in WorldCheckerParams parameters, Vector3 worldPosition, out WorldCheckerSample sample)
    {
        sample = default;
        if (!parameters.IsValid) return WorldCheckerEvaluation.InvalidParameters;
        if (!IsFinite(worldPosition)) return WorldCheckerEvaluation.NonFiniteInput;

        WorldCheckerEvaluation result = EvaluateCore(parameters.Scale, worldPosition, out int parity, out float distance);
        if (result != WorldCheckerEvaluation.Ok) return result;

        sample = new WorldCheckerSample(parity == 0 ? parameters.ColorA : parameters.ColorB, parity, distance);
        return WorldCheckerEvaluation.Ok;
    }

    /// <summary>
    /// The presentation convention: parity at <c>hit − n̂·<see cref="MaterialSamplingConvention.InwardBiasMeters"/></c>,
    /// where n̂ is <paramref name="hitNormal"/> normalized (it need not be unit length, but must be finite and
    /// non-zero). The original hit is never modified. Its shader parity and boundary distance are reported alongside.
    /// </summary>
    public static WorldCheckerEvaluation EvaluatePresentation(
        in WorldCheckerParams parameters,
        Vector3 hitPosition,
        Vector3 hitNormal,
        out WorldCheckerPresentationSample sample)
    {
        sample = default;
        if (!parameters.IsValid) return WorldCheckerEvaluation.InvalidParameters;
        if (!IsFinite(hitPosition) || !IsFinite(hitNormal)) return WorldCheckerEvaluation.NonFiniteInput;
        if (!TryNormalize(hitNormal, out Vector3 unitNormal)) return WorldCheckerEvaluation.InvalidNormal;

        WorldCheckerEvaluation result = EvaluateCore(parameters.Scale, hitPosition, out int shaderParity, out float hitDistance);
        if (result != WorldCheckerEvaluation.Ok) return result;

        const float bias = MaterialSamplingConvention.InwardBiasMeters;
        var biased = new Vector3(
            hitPosition.X - unitNormal.X * bias,
            hitPosition.Y - unitNormal.Y * bias,
            hitPosition.Z - unitNormal.Z * bias);
        if (!IsFinite(biased)) return WorldCheckerEvaluation.NonFiniteIntermediate;

        result = EvaluateCore(parameters.Scale, biased, out int parity, out _);
        if (result != WorldCheckerEvaluation.Ok) return result;

        sample = new WorldCheckerPresentationSample(
            parity == 0 ? parameters.ColorA : parameters.ColorB,
            parity,
            shaderParity,
            hitDistance,
            hitDistance < MaterialSamplingConvention.NearBoundaryCells,
            parity != shaderParity);
        return WorldCheckerEvaluation.Ok;
    }

    private static WorldCheckerEvaluation EvaluateCore(float scale, Vector3 position, out int parity, out float boundaryDistance)
    {
        parity = -1;
        boundaryDistance = -1f;
        float qx = position.X * scale;
        float qy = position.Y * scale;
        float qz = position.Z * scale;
        if (!float.IsFinite(qx) || !float.IsFinite(qy) || !float.IsFinite(qz))
        {
            return WorldCheckerEvaluation.NonFiniteIntermediate;
        }

        float cx = MathF.Floor(qx);
        float cy = MathF.Floor(qy);
        float cz = MathF.Floor(qz);
        float sum = (cx + cy) + cz;
        if (!float.IsFinite(sum)) return WorldCheckerEvaluation.NonFiniteIntermediate;

        float checker = GlslMod(sum, 2f);
        if (checker == 0f) parity = 0;
        else if (checker == 1f) parity = 1;
        else return WorldCheckerEvaluation.NonFiniteIntermediate;

        boundaryDistance = MathF.Min(AxisDistance(qx, cx), MathF.Min(AxisDistance(qy, cy), AxisDistance(qz, cz)));
        return WorldCheckerEvaluation.Ok;
    }

    private static float AxisDistance(float q, float cell)
    {
        float fraction = q - cell;
        return MathF.Min(fraction, 1f - fraction);
    }

    private static bool TryNormalize(Vector3 value, out Vector3 unit)
    {
        // Scale by the largest magnitude first, so neither overflow nor underflow can occur.
        float largest = MathF.Max(MathF.Abs(value.X), MathF.Max(MathF.Abs(value.Y), MathF.Abs(value.Z)));
        if (!(largest > 0f))
        {
            unit = default;
            return false;
        }

        float x = value.X / largest;
        float y = value.Y / largest;
        float z = value.Z / largest;
        float length = MathF.Sqrt(x * x + y * y + z * z);
        unit = new Vector3(x / length, y / length, z / length);
        return true;
    }

    internal static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
