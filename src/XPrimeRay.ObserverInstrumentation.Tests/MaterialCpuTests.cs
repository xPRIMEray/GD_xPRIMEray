using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using XPrimeRay.ObserverInstrumentation.Materials;

namespace XPrimeRay.ObserverInstrumentation.Tests;

internal static class MaterialCpuTests
{
    // Golden provenance of the fixture table below. It freezes the documented serialization format, and was
    // reproduced independently outside .NET from the documented layout (280 bytes).
    private const string FixtureProvenanceSha256 = "afaa943bf2c2da03f38a90a6411349f13684506cc72cc5d97ca00a6faafa5fca";

    private static readonly Rgb A = new(0.1f, 0.2f, 0.3f);
    private static readonly Rgb B = new(0.7f, 0.8f, 0.9f);
    private static readonly Rgb FlatF = new(0.5f, 0.25f, 0.125f);
    private static readonly Rgb FlatG = new(0.3f, 0.6f, 0.9f);
    private static readonly WorldCheckerParams FloorChecker = new(A, B, 1f);

    public static long LastHotPathBytes { get; private set; }

    public static void Run()
    {
        PinsActualShaderSourceAndDefaults();
        GlslModDiffersFromCSharpRemainder();
        PositiveCoordinates();
        NegativeCoordinates();
        MixedSignParity();
        UnbiasedShaderMathMatchesIntegerOracle();
        CellBoundariesAndNearBoundaryClassification();
        PresentationBiasIsSeparateFromShaderMath();
        PresentationMatchesShaderAwayFromCellPlanes();
        RejectsInvalidEvaluatorInputs();
        GuardsNonFiniteIntermediates();
        FlatColours();
        UnsupportedAndUnboundAttribution();
        TableRejectsDuplicateAndInvalidEntries();
        DeterministicLookupAndTableIdentity();
        CanonicalSerializationMatchesDocumentedLayout();
        LookupCacheIsCallerOwned();
        PerReasonAggregation();
        ConcurrentReadOnlySampling();
        ZeroAllocationHotPath();
        ReleaseThroughputWindow();
    }

    // ---------------------------------------------------------------- shader pin

    private static void PinsActualShaderSourceAndDefaults()
    {
        string path = LocateShader();
        byte[] bytes = File.ReadAllBytes(path);
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        TestAssert.Equal(WorldCheckerEvaluator.ShaderSourceSha256, hash, "checker shader source SHA-256 is pinned");
        TestAssert.Equal(WorldCheckerEvaluator.ShaderSourceUtf8Length, bytes.Length, "checker shader source length is pinned");
        TestAssert.False(bytes.Contains((byte)'\r'), "pinned checker shader source uses LF line endings");

        string source = Encoding.UTF8.GetString(bytes);
        foreach (string line in new[]
                 {
                     "world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;",
                     "vec3 cell = floor(world_position * checker_scale);",
                     "float checker = mod(cell.x + cell.y + cell.z, 2.0);",
                     "ALBEDO = mix(color_a, color_b, checker);",
                 })
        {
            TestAssert.True(source.Contains(line, StringComparison.Ordinal), $"shader contains replicated statement: {line}");
        }

        WorldCheckerParams defaults = WorldCheckerEvaluator.ShaderDefaults;
        TestAssert.Equal(defaults.ColorA, ParseVec3(source, "color_a"), "pinned color_a default");
        TestAssert.Equal(defaults.ColorB, ParseVec3(source, "color_b"), "pinned color_b default");
        TestAssert.Equal(defaults.Scale, ParseFloat(source, @"uniform float checker_scale = ([0-9.]+);"), "pinned checker_scale default");
        TestAssert.Equal(WorldCheckerEvaluator.ShaderDefaultRoughness,
            ParseFloat(source, @"uniform float roughness_value : hint_range\(0\.0, 1\.0\) = ([0-9.]+);"), "pinned roughness default");
        TestAssert.Equal(WorldCheckerEvaluator.ShaderDefaultMetallic,
            ParseFloat(source, @"uniform float metallic_value : hint_range\(0\.0, 1\.0\) = ([0-9.]+);"), "pinned metallic default");
        TestAssert.True(defaults.IsValid, "pinned defaults are valid parameters");
        TestAssert.Equal("res://Experiences/checkerboard_3d.gdshader", WorldCheckerEvaluator.ShaderResourcePath, "pinned resource path");
    }

    private static string LocateShader()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "Experiences", "checkerboard_3d.gdshader");
            if (File.Exists(candidate)) return candidate;
        }

        throw new InvalidOperationException("Experiences/checkerboard_3d.gdshader not found above the test binary");
    }

    private static Rgb ParseVec3(string source, string name)
    {
        Match match = Regex.Match(source, $@"uniform vec3 {name} : source_color = vec3\(([^)]*)\);");
        TestAssert.True(match.Success, $"shader declares {name}");
        float[] values = match.Groups[1].Value.Split(',').Select(v => float.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
        TestAssert.Equal(3, values.Length, $"{name} has three components");
        return new Rgb(values[0], values[1], values[2]);
    }

    private static float ParseFloat(string source, string pattern)
    {
        Match match = Regex.Match(source, pattern);
        TestAssert.True(match.Success, $"shader matches {pattern}");
        return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    // ---------------------------------------------------------------- unbiased shader mathematics

    private static void GlslModDiffersFromCSharpRemainder()
    {
        TestAssert.Equal(-1f, -3f % 2f, "C# remainder keeps the dividend sign (the wrong parity)");
        TestAssert.Equal(1f, WorldCheckerEvaluator.GlslMod(-3f, 2f), "GLSL mod(-3, 2) = 1");
        TestAssert.Equal(1f, WorldCheckerEvaluator.GlslMod(-1f, 2f), "GLSL mod(-1, 2) = 1");
        TestAssert.Equal(0f, WorldCheckerEvaluator.GlslMod(-2f, 2f), "GLSL mod(-2, 2) = 0");
        TestAssert.Equal(1f, WorldCheckerEvaluator.GlslMod(3f, 2f), "GLSL mod(3, 2) = 1");
        TestAssert.Equal(1.5f, WorldCheckerEvaluator.GlslMod(-0.5f, 2f), "GLSL mod(-0.5, 2) = 1.5");
        TestAssert.Equal(-1, (int)-1.5f, "integer cast truncates toward zero (the wrong cell)");
        ExpectParity(-0.5f, 0.5f, 0.5f, 1f, 1, "floor(-0.5) = -1, not the truncated 0");
    }

    private static void PositiveCoordinates()
    {
        ExpectParity(0.5f, 0.5f, 0.5f, 1f, 0, "origin cell");
        ExpectParity(1.5f, 0.5f, 0.5f, 1f, 1, "+x neighbour");
        ExpectParity(1.5f, 1.5f, 0.5f, 1f, 0, "xy diagonal");
        ExpectParity(1.5f, 1.5f, 1.5f, 1f, 1, "xyz diagonal");
        ExpectParity(0.1f, 0.1f, 0.1f, 8f, 0, "scale 8 origin cell");
        ExpectParity(0.2f, 0.1f, 0.1f, 8f, 1, "scale 8, q.x = 1.6");
        ExpectParity(1000.5f, 0.5f, 0.5f, 1f, 0, "large +x even cell 1000");
        ExpectParity(999.5f, 0.5f, 0.5f, 1f, 1, "large +x odd cell 999");
    }

    private static void NegativeCoordinates()
    {
        ExpectParity(-0.01f, 0f, 0f, 1f, 1, "(-0.01, 0, 0) lies in cell (-1, 0, 0)");
        ExpectParity(-1f, 0f, 0f, 1f, 1, "parity of (-1, 0, 0) is 1");
        ExpectParity(-0.5f, -0.5f, 0.5f, 1f, 0, "cells (-1, -1, 0)");
        ExpectParity(-0.5f, -0.5f, -0.5f, 1f, 1, "cells (-1, -1, -1)");
        ExpectParity(-1.5f, -0.5f, 0.25f, 1f, 1, "cells (-2, -1, 0), sum -3");
        ExpectParity(-9.85f, 0.1f, 0.1f, 0.55f, 0, "-9.85 x 0.55 = -5.4175 lies in cell -6");
        ExpectParity(-1000.5f, 0.5f, 0.5f, 1f, 1, "large -x cell -1001");
        ExpectParity(-999.5f, 0.5f, 0.5f, 1f, 0, "large -x cell -1000");
    }

    private static void MixedSignParity()
    {
        ExpectParity(-0.5f, 0.5f, 0.5f, 1f, 1, "cells (-1, 0, 0)");
        ExpectParity(0.5f, -0.5f, -0.5f, 1f, 0, "cells (0, -1, -1)");
        ExpectParity(-2.5f, 3.5f, -0.5f, 1f, 1, "cells (-3, 3, -1)");
        ExpectParity(2.5f, -3.5f, 0.5f, 1f, 0, "cells (2, -4, 0), sum -2");
        ExpectParity(2.5f, -3.5f, 1.5f, 1f, 1, "cells (2, -4, 1), sum -1");
        ExpectParity(-0.0625f, 0.0625f, -0.0625f, 8f, 0, "scale 8 cells (-1, 0, -1)");
    }

    private static void UnbiasedShaderMathMatchesIntegerOracle()
    {
        // Independent oracle: identical fp32 products, then double floor and int64 parity.
        float[] scales = { 0.55f, 1f, 2.5f, 8f };
        var random = new Random(20261116);
        for (int index = 0; index < 200_000; index++)
        {
            float scale = scales[index % scales.Length];
            var position = new Vector3(NextCoordinate(random, 1000f), NextCoordinate(random, 1000f), NextCoordinate(random, 1000f));
            long sum = (long)System.Math.Floor(position.X * scale) + (long)System.Math.Floor(position.Y * scale) + (long)System.Math.Floor(position.Z * scale);
            int expected = (int)(((sum % 2) + 2) % 2);
            var parameters = new WorldCheckerParams(A, B, scale);
            TestAssert.Equal(WorldCheckerEvaluation.Ok, WorldCheckerEvaluator.EvaluateShader(parameters, position, out WorldCheckerSample sample), "oracle sweep evaluates");
            if (sample.Parity != expected)
            {
                throw new InvalidOperationException($"oracle mismatch at {position} scale {scale}: {sample.Parity} != {expected}");
            }
        }
    }

    private static void CellBoundariesAndNearBoundaryClassification()
    {
        ExpectParity(1f, 0.5f, 0.5f, 1f, 1, "x exactly on plane 1 belongs to cell 1");
        ExpectParity(MathF.BitDecrement(1f), 0.5f, 0.5f, 1f, 0, "one ulp below plane 1 belongs to cell 0");
        ExpectParity(-1f, 0.5f, 0.5f, 1f, 1, "x exactly on plane -1 belongs to cell -1");
        ExpectParity(MathF.BitIncrement(-1f), 0.5f, 0.5f, 1f, 1, "one ulp above -1 stays in cell -1");
        ExpectParity(MathF.BitDecrement(-1f), 0.5f, 0.5f, 1f, 0, "one ulp below -1 enters cell -2");

        WorldCheckerEvaluator.EvaluateShader(FloorChecker, new Vector3(1f, 0.5f, 0.5f), out WorldCheckerSample onPlane);
        TestAssert.Equal(0f, onPlane.BoundaryDistanceCells, "distance on a plane is zero");
        WorldCheckerEvaluator.EvaluateShader(FloorChecker, new Vector3(0.25f, 0.5f, 0.375f), out WorldCheckerSample interior);
        TestAssert.Equal(0.25f, interior.BoundaryDistanceCells, "interior distance is the nearest-axis distance");

        ExpectNear(1.0005f, true, "5e-4 cells above a plane is near");
        ExpectNear(0.9995f, true, "5e-4 cells below a plane is near");
        ExpectNear(1.002f, false, "2e-3 cells above a plane is not near");
        ExpectNear(0.998f, false, "2e-3 cells below a plane is not near");
        ExpectNear(-1.0005f, true, "negative-side near boundary");
        ExpectNear(-1.002f, false, "negative-side clear of the boundary");
    }

    private static void ExpectNear(float x, bool expected, string label)
    {
        TestAssert.Equal(WorldCheckerEvaluation.Ok,
            WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, new Vector3(x, 0.5f, 0.5f), Vector3.UnitY, out WorldCheckerPresentationSample sample), label);
        TestAssert.Equal(expected, sample.NearCellBoundary, label);
        TestAssert.True(expected == sample.HitBoundaryDistanceCells < MaterialSamplingConvention.NearBoundaryCells, $"{label}: flag agrees with distance");
    }

    // ---------------------------------------------------------------- biased presentation sampling

    private static void PresentationBiasIsSeparateFromShaderMath()
    {
        var floorHit = new Vector3(0.3f, 0f, 0.3f);
        TestAssert.Equal(WorldCheckerEvaluation.Ok, WorldCheckerEvaluator.EvaluateShader(FloorChecker, floorHit, out WorldCheckerSample shader), "floor plane shader");
        TestAssert.Equal(0, shader.Parity, "shader at y = 0 chooses cell y = 0");

        WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, floorHit, Vector3.UnitY, out WorldCheckerPresentationSample up);
        TestAssert.Equal(1, up.Parity, "upward-facing floor selects the solid cell y = -1");
        TestAssert.Equal(B, up.Color, "presentation colour follows the biased parity");
        TestAssert.Equal(0, up.ShaderParityAtHit, "unbiased shader parity is reported separately");
        TestAssert.Equal(0f, up.HitBoundaryDistanceCells, "distance is measured at the original hit");
        TestAssert.True(up.NearCellBoundary && up.BiasChangedParity, "divergence from the shader is flagged");
        TestAssert.Equal(new Vector3(0.3f, 0f, 0.3f), floorHit, "hit position is not modified");

        WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, floorHit, -Vector3.UnitY, out WorldCheckerPresentationSample down);
        TestAssert.Equal(0, down.Parity, "downward-facing face at y = 0 selects the solid cell y = 0");
        TestAssert.False(down.BiasChangedParity, "no divergence when the bias stays in the shader cell");

        WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, floorHit, new Vector3(0f, 5f, 0f), out WorldCheckerPresentationSample scaled);
        WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, floorHit, new Vector3(0f, 1e-30f, 0f), out WorldCheckerPresentationSample tiny);
        TestAssert.Equal(up, scaled, "a non-unit normal is normalized");
        TestAssert.Equal(up, tiny, "a tiny normal is normalized without underflow");

        WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, new Vector3(0.3f, 5e-5f, 0.3f), Vector3.UnitY, out WorldCheckerPresentationSample within);
        TestAssert.True(within.BiasChangedParity && within.NearCellBoundary, "a hit within delta of a plane may change parity");
        WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, new Vector3(0.3f, 2e-4f, 0.3f), Vector3.UnitY, out WorldCheckerPresentationSample beyond);
        TestAssert.False(beyond.BiasChangedParity, "a hit beyond delta of a plane keeps the shader parity");

        WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, new Vector3(0.3f, 0.5f, 0.3f), Vector3.Normalize(new Vector3(1f, 2f, 3f)), out WorldCheckerPresentationSample inside);
        TestAssert.False(inside.NearCellBoundary || inside.BiasChangedParity, "an interior hit is unaffected by the bias");
        TestAssert.Equal(inside.ShaderParityAtHit, inside.Parity, "interior presentation equals shader");
    }

    private static void PresentationMatchesShaderAwayFromCellPlanes()
    {
        float[] scales = { 0.55f, 1f, 2.5f, 8f };
        var random = new Random(7);
        int compared = 0;
        int changed = 0;
        for (int index = 0; index < 200_000; index++)
        {
            float scale = scales[index % scales.Length];
            var parameters = new WorldCheckerParams(A, B, scale);
            var hit = new Vector3(NextCoordinate(random, 100f), NextCoordinate(random, 100f), NextCoordinate(random, 100f));
            Vector3 normal = NextNormal(random);
            if (index % 8 == 0)
            {
                // Put one axis on or near a cell plane, so the bias is exercised.
                float plane = MathF.Round(hit.Y * scale) / scale;
                hit = hit with { Y = plane + (float)(random.NextDouble() - 0.5) * 4e-4f };
            }

            TestAssert.Equal(WorldCheckerEvaluation.Ok, WorldCheckerEvaluator.EvaluatePresentation(parameters, hit, normal, out WorldCheckerPresentationSample sample), "presentation sweep evaluates");
            WorldCheckerEvaluator.EvaluateShader(parameters, hit, out WorldCheckerSample shader);
            TestAssert.Equal(shader.Parity, sample.ShaderParityAtHit, "presentation reports the unbiased shader parity");
            TestAssert.Equal(shader.BoundaryDistanceCells, sample.HitBoundaryDistanceCells, "presentation reports the unbiased distance");
            if (sample.BiasChangedParity) changed++;

            // The bias moves each axis by at most delta x scale cells; 1e-3 cells covers fp32 rounding for |p| <= 100 m.
            if (sample.HitBoundaryDistanceCells > MaterialSamplingConvention.InwardBiasMeters * scale + 1e-3f)
            {
                compared++;
                if (sample.Parity != shader.Parity)
                {
                    throw new InvalidOperationException($"bias changed parity away from a plane: {hit} {normal} scale {scale}");
                }
            }
            else if (sample.BiasChangedParity)
            {
                TestAssert.True(sample.NearCellBoundary || sample.HitBoundaryDistanceCells <= MaterialSamplingConvention.InwardBiasMeters * scale + 1e-3f,
                    "a divergent sample lies within the bias band");
            }
        }

        TestAssert.True(compared > 150_000, $"most samples lie away from planes ({compared})");
        TestAssert.True(changed > 0, "the sweep exercises the bias divergence");
        Console.WriteLine($"  presentation sweep: {compared} away-from-plane samples equal the shader; {changed} near-plane samples diverge by convention");
    }

    // ---------------------------------------------------------------- invalid inputs and nonfinite guards

    private static void RejectsInvalidEvaluatorInputs()
    {
        var origin = new Vector3(0.5f, 0.5f, 0.5f);
        foreach (float scale in new[] { 0f, -0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var parameters = new WorldCheckerParams(A, B, scale);
            TestAssert.Equal(WorldCheckerEvaluation.InvalidParameters, WorldCheckerEvaluator.EvaluateShader(parameters, origin, out _), $"scale {scale} rejected (shader)");
            TestAssert.Equal(WorldCheckerEvaluation.InvalidParameters, WorldCheckerEvaluator.EvaluatePresentation(parameters, origin, Vector3.UnitY, out _), $"scale {scale} rejected (presentation)");
        }

        TestAssert.Equal(WorldCheckerEvaluation.InvalidParameters,
            WorldCheckerEvaluator.EvaluateShader(new WorldCheckerParams(new Rgb(float.NaN, 0f, 0f), B, 1f), origin, out _), "NaN colour rejected");
        TestAssert.Equal(WorldCheckerEvaluation.InvalidParameters,
            WorldCheckerEvaluator.EvaluateShader(new WorldCheckerParams(A, new Rgb(0f, float.PositiveInfinity, 0f), 1f), origin, out _), "infinite colour rejected");

        foreach (Vector3 bad in new[] { new Vector3(float.NaN, 0f, 0f), new Vector3(0f, float.PositiveInfinity, 0f), new Vector3(0f, 0f, float.NegativeInfinity) })
        {
            TestAssert.Equal(WorldCheckerEvaluation.NonFiniteInput, WorldCheckerEvaluator.EvaluateShader(FloorChecker, bad, out WorldCheckerSample rejected), $"position {bad} rejected");
            TestAssert.Equal(default, rejected, "a rejected sample is default");
            TestAssert.Equal(WorldCheckerEvaluation.NonFiniteInput, WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, bad, Vector3.UnitY, out _), $"presentation position {bad} rejected");
            TestAssert.Equal(WorldCheckerEvaluation.NonFiniteInput, WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, origin, bad, out _), $"normal {bad} rejected");
        }

        TestAssert.Equal(WorldCheckerEvaluation.InvalidNormal, WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, origin, Vector3.Zero, out _), "zero normal rejected");
        TestAssert.Equal(WorldCheckerEvaluation.InvalidNormal, WorldCheckerEvaluator.EvaluatePresentation(FloorChecker, origin, new Vector3(-0f, 0f, -0f), out _), "negative-zero normal rejected");
    }

    private static void GuardsNonFiniteIntermediates()
    {
        TestAssert.Equal(WorldCheckerEvaluation.NonFiniteIntermediate,
            WorldCheckerEvaluator.EvaluateShader(new WorldCheckerParams(A, B, 8f), new Vector3(3e38f, 0f, 0f), out _), "position x scale overflow");
        TestAssert.Equal(WorldCheckerEvaluation.NonFiniteIntermediate,
            WorldCheckerEvaluator.EvaluateShader(FloorChecker, new Vector3(3e38f, 3e38f, 3e38f), out _), "cell sum overflow");
        TestAssert.Equal(WorldCheckerEvaluation.NonFiniteIntermediate,
            WorldCheckerEvaluator.EvaluatePresentation(new WorldCheckerParams(A, B, 8f), new Vector3(3e38f, 0f, 0f), Vector3.UnitY, out _), "presentation overflow");
        TestAssert.Equal(WorldCheckerEvaluation.Ok,
            WorldCheckerEvaluator.EvaluateShader(new WorldCheckerParams(A, B, float.Epsilon), new Vector3(1e3f, 1e3f, 1e3f), out WorldCheckerSample tiny), "a denormal scale is valid");
        TestAssert.Equal(0, tiny.Parity, "a denormal scale collapses to cell 0");
        WorldCheckerEvaluator.EvaluateShader(FloorChecker, new Vector3(1e8f, 0.5f, 0.5f), out WorldCheckerSample saturated);
        TestAssert.Equal(0f, saturated.BoundaryDistanceCells, "beyond fp32 cell precision the distance reads 0 (flagged near-boundary)");
    }

    // ---------------------------------------------------------------- sampler attribution

    private static void FlatColours()
    {
        MaterialBindingTable table = MaterialBindingTable.Build(1, new[]
        {
            MaterialBindingEntry.Bound(new(10, 0), MaterialDescriptor.CreateFlat(FlatF, GeometryCorrespondence.Exact)),
            MaterialBindingEntry.Bound(new(10, 1), MaterialDescriptor.CreateFlat(FlatG, GeometryCorrespondence.Proxy)),
        });
        var stats = default(MaterialSampleStats);
        foreach (Vector3 position in new[] { new Vector3(0.5f, 0f, 0.5f), new Vector3(-1234.5f, 7f, 1e6f), Vector3.Zero })
        {
            MaterialSampleResult flat = MaterialSampler.Sample(table, true, 10, 0, position, Vector3.UnitY, ref stats);
            TestAssert.Equal(MaterialSampleStatus.Matched, flat.Status, "flat matched");
            TestAssert.Equal(FlatF, flat.Color, "flat colour exactly as authored");
            TestAssert.Equal(-1, flat.Parity, "flat has no parity");
            TestAssert.False(flat.NearCellBoundary || flat.BiasChangedParity, "flat has no boundary");
        }

        MaterialSampleResult zeroNormal = MaterialSampler.Sample(table, true, 10, 0, Vector3.One, Vector3.Zero, ref stats);
        TestAssert.Equal(MaterialSampleStatus.Matched, zeroNormal.Status, "flat ignores a zero-length normal");
        MaterialSampleResult proxy = MaterialSampler.Sample(table, true, 10, 1, Vector3.One, Vector3.UnitY, ref stats);
        TestAssert.Equal(FlatG, proxy.Color, "proxy flat colour");
        TestAssert.Equal(GeometryCorrespondence.Proxy, table.GetDescriptor(proxy.DescriptorIndex).Correspondence, "proxy correspondence retrievable");
        TestAssert.Equal(5L, stats.Matched, "five flat matches");
        TestAssert.Equal(1L, stats.MatchedProxy, "one proxy match");
        TestAssert.Equal(0L, stats.NearBoundary, "flats never count near-boundary");
    }

    private static void UnsupportedAndUnboundAttribution()
    {
        MaterialBindingTable table = Fixture(3);
        var stats = default(MaterialSampleStats);
        Vector3 hit = new(0.3f, 0.4f, 0.5f);

        MaterialSampleResult unsupported = MaterialSampler.Sample(table, true, 1002, 0, hit, Vector3.UnitY, ref stats);
        TestAssert.Equal(MaterialSampleStatus.Unsupported, unsupported.Status, "unsupported status");
        TestAssert.Equal(MaterialUnsupportedReason.EmissionEnabled, unsupported.Reason, "unsupported reason preserved");
        TestAssert.Equal(MaterialSamplingConvention.UnsupportedColor, unsupported.Color, "unsupported sentinel");
        TestAssert.Equal(-1, unsupported.DescriptorIndex, "unsupported has no descriptor");

        MaterialSampleResult unknownCollider = MaterialSampler.Sample(table, true, 999_999, 0, hit, Vector3.UnitY, ref stats);
        MaterialSampleResult wrongShape = MaterialSampler.Sample(table, true, 1001, 7, hit, Vector3.UnitY, ref stats);
        MaterialSampleResult negativeShape = MaterialSampler.Sample(table, true, 1001, -1, hit, Vector3.UnitY, ref stats);
        foreach (MaterialSampleResult unbound in new[] { unknownCollider, wrongShape, negativeShape })
        {
            TestAssert.Equal(MaterialSampleStatus.Unbound, unbound.Status, "unbound status");
            TestAssert.Equal(MaterialUnsupportedReason.None, unbound.Reason, "unbound carries no reason");
            TestAssert.Equal(MaterialSamplingConvention.UnboundColor, unbound.Color, "unbound sentinel");
        }

        MaterialSampleResult nonFinite = MaterialSampler.Sample(table, true, 999_999, 0, new Vector3(float.NaN, 0f, 0f), Vector3.UnitY, ref stats);
        TestAssert.Equal(MaterialSampleStatus.NonFiniteHit, nonFinite.Status, "a nonfinite hit outranks unbound");
        MaterialSampleResult nonFiniteNormal = MaterialSampler.Sample(table, true, 5, 0, hit, new Vector3(0f, float.PositiveInfinity, 0f), ref stats);
        TestAssert.Equal(MaterialSampleStatus.NonFiniteHit, nonFiniteNormal.Status, "a nonfinite normal is a nonfinite hit, even for a flat binding");
        MaterialSampleResult invalidNormal = MaterialSampler.Sample(table, true, 1001, 0, hit, Vector3.Zero, ref stats);
        TestAssert.Equal(MaterialSampleStatus.InvalidHitNormal, invalidNormal.Status, "a checker needs a non-zero normal");
        MaterialSampleResult overflow = MaterialSampler.Sample(table, true, 1001, 0, new Vector3(3e38f, 0f, 0f), Vector3.UnitY, ref stats);
        TestAssert.Equal(MaterialSampleStatus.NonFiniteEvaluation, overflow.Status, "a nonfinite intermediate is attributed");
        MaterialSampleResult noHit = MaterialSampler.Sample(table, false, 1001, 0, new Vector3(float.NaN, 0f, 0f), Vector3.Zero, ref stats);
        TestAssert.Equal(MaterialSampleStatus.NoHit, noHit.Status, "no hit outranks everything");
        foreach (MaterialSampleResult invalid in new[] { nonFinite, nonFiniteNormal, invalidNormal, overflow, noHit })
        {
            TestAssert.Equal(MaterialSamplingConvention.InvalidColor, invalid.Color, $"{invalid.Status} sentinel");
            TestAssert.Equal(-1, invalid.DescriptorIndex, $"{invalid.Status} has no descriptor");
        }

        MaterialSampleResult checker = MaterialSampler.Sample(table, true, 1001, 0, new Vector3(0.15f, 0.01f, 0.01f), Vector3.UnitY, ref stats);
        TestAssert.Equal(MaterialSampleStatus.Matched, checker.Status, "checker matched");
        TestAssert.Equal(1, checker.Parity, "checker parity at scale 8, q.x = 1.2");
        TestAssert.Equal(WorldCheckerEvaluator.ShaderDefaults.ColorB, checker.Color, "checker colour B");

        TestAssert.Equal(1L, stats.UnsupportedTotal, "one unsupported");
        TestAssert.Equal(1L, stats.GetUnsupported(MaterialUnsupportedReason.EmissionEnabled), "attributed to emission");
        TestAssert.Equal(3L, stats.Unbound, "three unbound");
        TestAssert.Equal(2L, stats.NonFiniteHit, "two nonfinite hits");
        TestAssert.Equal(1L, stats.InvalidHitNormal, "one invalid normal");
        TestAssert.Equal(1L, stats.NonFiniteEvaluation, "one nonfinite evaluation");
        TestAssert.Equal(1L, stats.NoHit, "one no-hit");
        TestAssert.Equal(1L, stats.Matched, "one match");
        TestAssert.Equal(10L, stats.Total, "every sample attributed exactly once");
        TestAssert.True(stats.IsConsistent, "stats invariants hold");
        Throws<ArgumentNullException>(() => { var s = default(MaterialSampleStats); MaterialSampler.Sample(null!, true, 1, 0, Vector3.Zero, Vector3.UnitY, ref s); }, "null table rejected");
    }

    // ---------------------------------------------------------------- table

    private static void TableRejectsDuplicateAndInvalidEntries()
    {
        MaterialDescriptor flat = MaterialDescriptor.CreateFlat(FlatF, GeometryCorrespondence.Exact);
        MaterialDescriptor other = MaterialDescriptor.CreateFlat(FlatG, GeometryCorrespondence.Exact);
        Throws<ArgumentException>(() => MaterialBindingTable.Build(0, new[] { MaterialBindingEntry.Bound(new(5, 0), flat), MaterialBindingEntry.Bound(new(5, 0), other) }), "duplicate bound keys rejected");
        Throws<ArgumentException>(() => MaterialBindingTable.Build(0, new[] { MaterialBindingEntry.Bound(new(5, 0), flat), MaterialBindingEntry.Unsupported(new(5, 0), MaterialUnsupportedReason.TextureBound) }), "a bound/unsupported duplicate is rejected");
        Throws<ArgumentException>(() => MaterialBindingTable.Build(0, new[] { MaterialBindingEntry.Bound(new(0, 0), flat) }), "zero collider id rejected");
        Throws<ArgumentException>(() => MaterialBindingTable.Build(0, new[] { MaterialBindingEntry.Bound(new(5, -1), flat) }), "negative shape index rejected");
        Throws<ArgumentException>(() => MaterialBindingTable.Build(0, new[] { default(MaterialBindingEntry) }), "default entry rejected");
        Throws<ArgumentException>(() => MaterialBindingTable.Build(0, new[] { MaterialBindingEntry.Unsupported(new(5, 0), MaterialUnsupportedReason.None) }), "unsupported without a reason rejected");
        Throws<ArgumentException>(() => MaterialBindingTable.Build(0, new[] { MaterialBindingEntry.Unsupported(new(5, 0), (MaterialUnsupportedReason)13) }), "undefined reason rejected");
        Throws<ArgumentOutOfRangeException>(() => MaterialBindingTable.Build(-1, ReadOnlySpan<MaterialBindingEntry>.Empty), "negative generation rejected");

        var invalidDescriptors = new (MaterialDescriptor Descriptor, MaterialDescriptorError Error)[]
        {
            (MaterialDescriptor.CreateWorldChecker(new WorldCheckerParams(A, B, 0f), GeometryCorrespondence.Exact), MaterialDescriptorError.NonPositiveScale),
            (MaterialDescriptor.CreateWorldChecker(new WorldCheckerParams(A, B, -8f), GeometryCorrespondence.Exact), MaterialDescriptorError.NonPositiveScale),
            (MaterialDescriptor.CreateWorldChecker(new WorldCheckerParams(A, B, float.NaN), GeometryCorrespondence.Exact), MaterialDescriptorError.NonFiniteScale),
            (MaterialDescriptor.CreateWorldChecker(new WorldCheckerParams(A, B, float.PositiveInfinity), GeometryCorrespondence.Exact), MaterialDescriptorError.NonFiniteScale),
            (MaterialDescriptor.CreateWorldChecker(new WorldCheckerParams(new Rgb(0f, float.NaN, 0f), B, 1f), GeometryCorrespondence.Exact), MaterialDescriptorError.NonFiniteColor),
            (MaterialDescriptor.CreateFlat(new Rgb(float.NegativeInfinity, 0f, 0f), GeometryCorrespondence.Exact), MaterialDescriptorError.NonFiniteColor),
            (flat with { Kind = (MaterialKind)9 }, MaterialDescriptorError.UndefinedKind),
            (flat with { Encoding = 0 }, MaterialDescriptorError.UndefinedEncoding),
            (flat with { Correspondence = 0 }, MaterialDescriptorError.UndefinedCorrespondence),
            (flat with { Checker = FloorChecker }, MaterialDescriptorError.InactiveFieldNotDefault),
            (MaterialDescriptor.CreateWorldChecker(FloorChecker, GeometryCorrespondence.Exact) with { Flat = FlatF }, MaterialDescriptorError.InactiveFieldNotDefault),
            (default, MaterialDescriptorError.UndefinedEncoding),
        };
        foreach ((MaterialDescriptor descriptor, MaterialDescriptorError error) in invalidDescriptors)
        {
            TestAssert.Equal(error, descriptor.Validate(), $"descriptor validation {error}");
            Throws<ArgumentException>(() => MaterialBindingTable.Build(0, new[] { MaterialBindingEntry.Bound(new(5, 0), descriptor) }), $"invalid descriptor ({error}) rejected");
        }

        MaterialBindingTable empty = MaterialBindingTable.Empty(4);
        var stats = default(MaterialSampleStats);
        TestAssert.Equal(MaterialSampleStatus.Unbound, MaterialSampler.Sample(empty, true, 5, 0, Vector3.One, Vector3.UnitY, ref stats).Status, "an empty table leaves every hit unbound");
    }

    private static void DeterministicLookupAndTableIdentity()
    {
        MaterialBindingEntry[] entries = FixtureEntries();
        MaterialBindingTable reference = MaterialBindingTable.Build(1, entries);
        Console.WriteLine($"  fixture provenance: {reference.ProvenanceSha256}");
        TestAssert.Equal(FixtureProvenanceSha256, reference.ProvenanceSha256, "fixture provenance matches the frozen golden value");
        TestAssert.Equal(6, reference.EntryCount, "six entries");
        TestAssert.Equal(4, reference.BoundCount, "four bound");
        TestAssert.Equal(2, reference.UnsupportedCount, "two unsupported");
        TestAssert.Equal(3, reference.DescriptorCount, "identical descriptors are deduplicated");

        var random = new Random(42);
        for (int trial = 0; trial < 12; trial++)
        {
            MaterialBindingEntry[] shuffled = trial == 0 ? entries.Reverse().ToArray() : entries.OrderBy(_ => random.Next()).ToArray();
            MaterialBindingTable rebuilt = MaterialBindingTable.Build(100 + trial, shuffled);
            TestAssert.Equal(reference.ProvenanceSha256, rebuilt.ProvenanceSha256, "provenance is independent of input order and generation");
            TestAssert.True(reference.CopyCanonicalSerialization().AsSpan().SequenceEqual(rebuilt.CopyCanonicalSerialization()), "serialization is byte-identical");
            TestAssert.Equal(100L + trial, rebuilt.Generation, "the generation is carried");
            for (int index = 0; index < reference.EntryCount; index++)
            {
                TestAssert.Equal(reference.GetEntry(index), rebuilt.GetEntry(index), "inventory order is canonical");
            }
        }

        MaterialBindingKey previous = default;
        for (int index = 0; index < reference.EntryCount; index++)
        {
            MaterialBindingEntry entry = reference.GetEntry(index);
            if (index > 0) TestAssert.True(previous.CompareTo(entry.Key) < 0, "inventory sorted by (collider, shape)");
            previous = entry.Key;
            TestAssert.True(reference.TryLookup(entry.Key, out int descriptorIndex, out MaterialUnsupportedReason reason), "every inventory key is found");
            TestAssert.Equal(reference.GetEntryDescriptorIndex(index), descriptorIndex, "lookup returns the inventory descriptor");
            TestAssert.Equal(entry.Reason, reason, "lookup returns the inventory reason");
            if (entry.IsBound) TestAssert.Equal(entry.Descriptor, reference.GetDescriptor(descriptorIndex), "descriptor round trip");
        }

        TestAssert.False(reference.TryLookup(new(999_999, 0), out int missingIndex, out MaterialUnsupportedReason missingReason), "an unknown collider is not found");
        TestAssert.Equal(-1, missingIndex, "unbound lookup index");
        TestAssert.Equal(MaterialUnsupportedReason.None, missingReason, "unbound lookup reason");
        TestAssert.False(reference.TryLookup(new(1001, 2), out _, out _), "a known collider with the wrong shape is not found");
        TestAssert.False(reference.TryLookup(new(4, 0), out _, out _), "below the smallest key");
        TestAssert.False(reference.TryLookup(new(ulong.MaxValue, int.MaxValue), out _, out _), "above the largest key");

        MaterialBindingTable negativeZero = MaterialBindingTable.Build(1, entries.Select(e => e.Key == new MaterialBindingKey(5, 0)
            ? MaterialBindingEntry.Bound(e.Key, MaterialDescriptor.CreateFlat(new Rgb(-0f, 0f, 0f), GeometryCorrespondence.Exact)) : e).ToArray());
        MaterialBindingTable positiveZero = MaterialBindingTable.Build(1, entries.Select(e => e.Key == new MaterialBindingKey(5, 0)
            ? MaterialBindingEntry.Bound(e.Key, MaterialDescriptor.CreateFlat(new Rgb(0f, 0f, 0f), GeometryCorrespondence.Exact)) : e).ToArray());
        TestAssert.Equal(positiveZero.ProvenanceSha256, negativeZero.ProvenanceSha256, "negative zero is canonicalized");

        AssertProvenanceChanges(reference, entries, new(5, 0), MaterialBindingEntry.Bound(new(5, 0),
            MaterialDescriptor.CreateFlat(FlatF with { R = MathF.BitIncrement(FlatF.R) }, GeometryCorrespondence.Exact)), "a one-ulp colour change");
        AssertProvenanceChanges(reference, entries, new(5, 0), MaterialBindingEntry.Bound(new(5, 0),
            MaterialDescriptor.CreateFlat(FlatF, GeometryCorrespondence.Proxy)), "a correspondence change");
        AssertProvenanceChanges(reference, entries, new(1002, 0), MaterialBindingEntry.Unsupported(new(1002, 0), MaterialUnsupportedReason.TextureBound), "a reason change");
        AssertProvenanceChanges(reference, entries, new(5, 0), MaterialBindingEntry.Unsupported(new(5, 0), MaterialUnsupportedReason.TextureBound), "bound to unsupported");
        AssertProvenanceChanges(reference, entries, new(1001, 0), MaterialBindingEntry.Bound(new(1001, 0),
            MaterialDescriptor.CreateWorldChecker(WorldCheckerEvaluator.ShaderDefaults with { Scale = 4f }, GeometryCorrespondence.Exact)), "a checker scale change");
        TestAssert.False(MaterialBindingTable.Build(1, entries.Take(5).ToArray()).ProvenanceSha256 == reference.ProvenanceSha256, "removing an entry changes provenance");
        TestAssert.False(MaterialBindingTable.Empty(1).ProvenanceSha256 == reference.ProvenanceSha256, "an empty table differs");
    }

    private static void AssertProvenanceChanges(MaterialBindingTable reference, MaterialBindingEntry[] entries, MaterialBindingKey key, MaterialBindingEntry replacement, string label)
    {
        MaterialBindingTable changed = MaterialBindingTable.Build(1, entries.Select(e => e.Key == key ? replacement : e).ToArray());
        TestAssert.False(changed.ProvenanceSha256 == reference.ProvenanceSha256, $"{label} changes provenance");
    }

    private static void CanonicalSerializationMatchesDocumentedLayout()
    {
        // Independent re-creation of the documented layout for the fixture.
        // Sorted keys: (5,0) F, (77,2) concave, (1001,0) C, (1001,1) F, (1001,3) G proxy, (1002,0) emission.
        // Descriptors in first-use order: d0 = F exact, d1 = checker C exact, d2 = G proxy.
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("xprimeray-material-binding-table-v1"));
            writer.Write((byte)0);
            writer.Write(Encoding.ASCII.GetBytes("f9f8e05b67fdf8594888b979d8ae3a35f5ca80fdde52d7c3d91b17e24a4c46e8"));
            writer.Write((byte)0);
            writer.Write(1e-4f);
            writer.Write(1e-3f);
            writer.Write(3u);
            writer.Write((byte)2); writer.Write((byte)1); writer.Write((byte)1); writer.Write(0.5f); writer.Write(0.25f); writer.Write(0.125f);
            writer.Write((byte)1); writer.Write((byte)1); writer.Write((byte)1);
            writer.Write(0.04f); writer.Write(0.05f); writer.Write(0.08f); writer.Write(0.9f); writer.Write(0.92f); writer.Write(0.95f); writer.Write(8f);
            writer.Write((byte)2); writer.Write((byte)1); writer.Write((byte)2); writer.Write(0.3f); writer.Write(0.6f); writer.Write(0.9f);
            writer.Write(6u);
            WriteEntry(writer, 5, 0, 0, 0);
            WriteEntry(writer, 77, 2, -1, 11);
            WriteEntry(writer, 1001, 0, 1, 0);
            WriteEntry(writer, 1001, 1, 0, 0);
            WriteEntry(writer, 1001, 3, 2, 0);
            WriteEntry(writer, 1002, 0, -1, 7);
        }

        byte[] expected = stream.ToArray();
        MaterialBindingTable table = Fixture(9);
        TestAssert.True(expected.AsSpan().SequenceEqual(table.CopyCanonicalSerialization()), "serialization equals the documented layout");
        TestAssert.Equal(Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant(), table.ProvenanceSha256, "provenance is SHA-256 of the documented layout");
        TestAssert.Equal(MaterialKind.Flat, table.GetDescriptor(0).Kind, "descriptor 0 is the first-used flat");
        TestAssert.Equal(MaterialKind.WorldSpaceChecker, table.GetDescriptor(1).Kind, "descriptor 1 is the checker");
    }

    private static void WriteEntry(BinaryWriter writer, ulong collider, int shape, int descriptorIndex, byte reason)
    {
        writer.Write(collider);
        writer.Write(shape);
        writer.Write(descriptorIndex);
        writer.Write(reason);
    }

    private static void LookupCacheIsCallerOwned()
    {
        MaterialBindingTable first = Fixture(1);
        var cache = default(MaterialLookupCache);
        TestAssert.Equal(MaterialBindingLookup.Bound, first.Lookup(new(1001, 0), ref cache, out int d0, out _), "first lookup");
        TestAssert.Equal(MaterialBindingLookup.Bound, first.Lookup(new(1001, 0), ref cache, out int d1, out _), "repeat lookup");
        TestAssert.Equal(d0, d1, "the cached result equals the searched result");
        TestAssert.Equal(MaterialBindingLookup.Unsupported, first.Lookup(new(1002, 0), ref cache, out _, out MaterialUnsupportedReason reason), "unsupported lookup");
        TestAssert.Equal(MaterialUnsupportedReason.EmissionEnabled, reason, "unsupported reason via cache");
        TestAssert.Equal(MaterialBindingLookup.Unsupported, first.Lookup(new(1002, 0), ref cache, out _, out reason), "unsupported cached");
        TestAssert.Equal(MaterialUnsupportedReason.EmissionEnabled, reason, "cached unsupported reason");
        TestAssert.Equal(MaterialBindingLookup.Unbound, first.Lookup(new(31337, 0), ref cache, out _, out _), "unbound lookup");
        TestAssert.Equal(MaterialBindingLookup.Unbound, first.Lookup(new(31337, 0), ref cache, out _, out _), "unbound cached");
        TestAssert.Equal(3L, cache.Hits, "three cache hits");
        TestAssert.Equal(3L, cache.Misses, "three cache misses");

        // The same key in a different table must not reuse the first table's slot.
        MaterialBindingTable second = MaterialBindingTable.Build(2, new[] { MaterialBindingEntry.Unsupported(new(31337, 0), MaterialUnsupportedReason.MultiSurfaceMesh) });
        TestAssert.Equal(MaterialBindingLookup.Unsupported, second.Lookup(new(31337, 0), ref cache, out _, out reason), "the cache is bound to its table instance");
        TestAssert.Equal(MaterialUnsupportedReason.MultiSurfaceMesh, reason, "the second table's own answer");
        TestAssert.Equal(4L, cache.Misses, "a table switch misses");
        cache.Reset();
        TestAssert.Equal(0L, cache.Hits + cache.Misses, "reset clears the cache");

        var independent = default(MaterialLookupCache);
        first.Lookup(new(5, 0), ref independent, out _, out _);
        TestAssert.Equal(0L, cache.Hits + cache.Misses, "caches are independent values");
    }

    private static void PerReasonAggregation()
    {
        MaterialBindingEntry[] entries = Enumerable.Range(1, MaterialUnsupportedReasons.MaxDefinedValue)
            .Select(r => MaterialBindingEntry.Unsupported(new((ulong)(500 + r), 0), (MaterialUnsupportedReason)r))
            .Append(MaterialBindingEntry.Bound(new(9000, 0), MaterialDescriptor.CreateWorldChecker(FloorChecker, GeometryCorrespondence.Proxy)))
            .ToArray();
        MaterialBindingTable table = MaterialBindingTable.Build(1, entries);
        var whole = default(MaterialSampleStats);
        var firstHalf = default(MaterialSampleStats);
        var secondHalf = default(MaterialSampleStats);
        int samples = 0;
        for (int r = 1; r <= MaterialUnsupportedReasons.MaxDefinedValue; r++)
        {
            for (int repeat = 0; repeat < r; repeat++)
            {
                ref MaterialSampleStats half = ref (samples % 2 == 0 ? ref firstHalf : ref secondHalf);
                MaterialSampler.Sample(table, true, (ulong)(500 + r), 0, Vector3.One, Vector3.UnitY, ref whole);
                MaterialSampler.Sample(table, true, (ulong)(500 + r), 0, Vector3.One, Vector3.UnitY, ref half);
                samples++;
            }
        }

        for (int index = 0; index < 7; index++)
        {
            ref MaterialSampleStats half = ref (samples % 2 == 0 ? ref firstHalf : ref secondHalf);
            var hit = new Vector3(0.25f, index == 0 ? 0f : 0.5f, 0.25f);
            MaterialSampler.Sample(table, true, 9000, 0, hit, Vector3.UnitY, ref whole);
            MaterialSampler.Sample(table, true, 9000, 0, hit, Vector3.UnitY, ref half);
            samples++;
        }

        for (int r = 1; r <= MaterialUnsupportedReasons.MaxDefinedValue; r++)
        {
            TestAssert.Equal((long)r, whole.GetUnsupported((MaterialUnsupportedReason)r), $"reason {(MaterialUnsupportedReason)r} counted {r} times");
        }

        long expectedUnsupported = MaterialUnsupportedReasons.MaxDefinedValue * (MaterialUnsupportedReasons.MaxDefinedValue + 1) / 2;
        TestAssert.Equal(expectedUnsupported, whole.UnsupportedTotal, "unsupported total is the sum of reasons");
        TestAssert.Equal(0L, whole.GetUnsupported(MaterialUnsupportedReason.None), "the None bucket stays empty");
        TestAssert.Equal(7L, whole.Matched, "seven matches");
        TestAssert.Equal(7L, whole.MatchedProxy, "every match is proxy");
        TestAssert.Equal(1L, whole.NearBoundary, "one on-plane match");
        TestAssert.Equal(1L, whole.BiasChangedParity, "one bias divergence");
        TestAssert.Equal((long)samples, whole.Total, "total equals sample count");
        TestAssert.True(whole.IsConsistent, "whole-window invariants");

        var merged = default(MaterialSampleStats);
        merged.Add(firstHalf);
        merged.Add(secondHalf);
        TestAssert.True(merged.CountsEqual(whole), "merging partial windows reproduces the whole window");
        TestAssert.True(merged.IsConsistent, "merged invariants");
        merged.Clear();
        TestAssert.Equal(0L, merged.Total, "clear resets");

        var corrupt = whole;
        corrupt.ByReason[(int)MaterialUnsupportedReason.None] = 1;
        TestAssert.False(corrupt.IsConsistent, "a count in the None bucket is detected");
    }

    // ---------------------------------------------------------------- concurrency, allocation, throughput

    private static void ConcurrentReadOnlySampling()
    {
        MaterialBindingTable table = Fixture(5);
        string provenance = table.ProvenanceSha256;
        HitInput[] inputs = MixedInputs(200_000, 99, coherentRuns: false);
        var serial = new MaterialSampleResult[inputs.Length];
        var serialStats = default(MaterialSampleStats);
        SampleRange(table, inputs, 0, inputs.Length, serial, ref serialStats);

        const int threadCount = 8;
        var fullResults = new MaterialSampleResult[threadCount][];
        var fullStats = new MaterialSampleStats[threadCount];
        var chunkStats = new MaterialSampleStats[threadCount];
        var chunked = new MaterialSampleResult[inputs.Length];
        var failures = new Exception?[threadCount];
        using var barrier = new Barrier(threadCount);
        var threads = new Thread[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            int worker = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    var results = new MaterialSampleResult[inputs.Length];
                    var stats = default(MaterialSampleStats);
                    SampleRange(table, inputs, 0, inputs.Length, results, ref stats);
                    fullResults[worker] = results;
                    fullStats[worker] = stats;

                    int chunk = (inputs.Length + threadCount - 1) / threadCount;
                    int start = worker * chunk;
                    int end = System.Math.Min(inputs.Length, start + chunk);
                    var partial = default(MaterialSampleStats);
                    SampleRange(table, inputs, start, end, chunked, ref partial);
                    chunkStats[worker] = partial;
                }
                catch (Exception exception)
                {
                    failures[worker] = exception;
                }
            });
            threads[t].Start();
        }

        foreach (Thread thread in threads) thread.Join();
        foreach (Exception? failure in failures)
        {
            if (failure != null) throw new InvalidOperationException($"worker failed: {failure.Message}");
        }

        var merged = default(MaterialSampleStats);
        for (int t = 0; t < threadCount; t++)
        {
            TestAssert.True(fullStats[t].CountsEqual(serialStats), $"worker {t} stats equal serial");
            for (int i = 0; i < inputs.Length; i++)
            {
                if (fullResults[t][i] != serial[i]) throw new InvalidOperationException($"worker {t} result {i} differs from serial");
            }

            merged.Add(chunkStats[t]);
        }

        TestAssert.True(merged.CountsEqual(serialStats), "per-chunk stats merged after the fact equal serial stats");
        TestAssert.True(chunked.AsSpan().SequenceEqual(serial), "chunked concurrent results equal serial results");
        TestAssert.Equal(provenance, table.ProvenanceSha256, "table identity unchanged by concurrent use");
        TestAssert.True(serialStats.Matched > 0 && serialStats.Unbound > 0 && serialStats.UnsupportedTotal > 0
            && serialStats.NonFiniteHit > 0 && serialStats.NoHit > 0 && serialStats.InvalidHitNormal > 0, "the concurrent mix covers every status");
        Console.WriteLine($"  concurrent: {threadCount} threads x {inputs.Length} samples identical to serial; matched={serialStats.Matched} unbound={serialStats.Unbound} unsupported={serialStats.UnsupportedTotal} nonfinite={serialStats.NonFiniteHit} invalidNormal={serialStats.InvalidHitNormal} noHit={serialStats.NoHit} near={serialStats.NearBoundary} biasChanged={serialStats.BiasChangedParity}");
    }

    private static void ZeroAllocationHotPath()
    {
        MaterialBindingTable table = Fixture(6);
        HitInput[] inputs = MixedInputs(1_000_000, 3, coherentRuns: true);
        var stats = default(MaterialSampleStats);
        var cache = default(MaterialLookupCache);
        float sink = 0f;
        for (int i = 0; i < 20_000; i++)
        {
            ref readonly HitInput input = ref inputs[i];
            sink += MaterialSampler.Sample(table, input.HadHit, input.Collider, input.Shape, input.Position, input.Normal, ref cache, ref stats).Color.R;
            sink += MaterialSampler.Sample(table, input.HadHit, input.Collider, input.Shape, input.Position, input.Normal, ref stats).Color.G;
        }

        stats.Clear();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < inputs.Length; i++)
        {
            ref readonly HitInput input = ref inputs[i];
            sink += MaterialSampler.Sample(table, input.HadHit, input.Collider, input.Shape, input.Position, input.Normal, ref cache, ref stats).Color.R;
        }

        long cached = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < inputs.Length; i++)
        {
            ref readonly HitInput input = ref inputs[i];
            sink += MaterialSampler.Sample(table, input.HadHit, input.Collider, input.Shape, input.Position, input.Normal, ref stats).Color.G;
        }

        long uncached = GC.GetAllocatedBytesForCurrentThread() - before;
        LastHotPathBytes = cached + uncached;
        TestAssert.Equal(0L, cached, "1,000,000 cached samples allocate zero bytes");
        TestAssert.Equal(0L, uncached, "1,000,000 uncached samples allocate zero bytes");
        TestAssert.Equal(2L * inputs.Length, stats.Total, "every allocation-window sample attributed");
        Console.WriteLine($"  allocation delta: cached={cached} uncached={uncached} bytes / {inputs.Length} samples each (sink {sink:F1})");
    }

    private static void ReleaseThroughputWindow()
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        MaterialBindingTable table = Fixture(7);
        HitInput[] inputs = MixedInputs(1_000_000, 11, coherentRuns: true);
        var checkerOnly = new Vector3[inputs.Length];
        for (int i = 0; i < inputs.Length; i++) checkerOnly[i] = inputs[i].Position;

        const int warmup = 3;
        const int windows = 7;
        double[] cachedRates = Measure(windows, warmup, () =>
        {
            var stats = default(MaterialSampleStats);
            var cache = default(MaterialLookupCache);
            float sink = 0f;
            for (int i = 0; i < inputs.Length; i++)
            {
                ref readonly HitInput input = ref inputs[i];
                sink += MaterialSampler.Sample(table, input.HadHit, input.Collider, input.Shape, input.Position, input.Normal, ref cache, ref stats).Color.R;
            }

            return sink + stats.Matched;
        }, inputs.Length);
        double[] uncachedRates = Measure(windows, warmup, () =>
        {
            var stats = default(MaterialSampleStats);
            float sink = 0f;
            for (int i = 0; i < inputs.Length; i++)
            {
                ref readonly HitInput input = ref inputs[i];
                sink += MaterialSampler.Sample(table, input.HadHit, input.Collider, input.Shape, input.Position, input.Normal, ref stats).Color.R;
            }

            return sink + stats.Matched;
        }, inputs.Length);
        WorldCheckerParams defaults = WorldCheckerEvaluator.ShaderDefaults;
        double[] shaderRates = Measure(windows, warmup, () =>
        {
            float sink = 0f;
            for (int i = 0; i < checkerOnly.Length; i++)
            {
                WorldCheckerEvaluator.EvaluateShader(defaults, checkerOnly[i], out WorldCheckerSample sample);
                sink += sample.Color.R;
            }

            return sink;
        }, inputs.Length);

        Console.WriteLine($"  MATERIAL_BENCH build={configuration} threads=1 samples/window={inputs.Length} warmup={warmup} windows={windows} processors={Environment.ProcessorCount} runtime={Environment.Version}");
        Report("sampler_mixed_cached", cachedRates);
        Report("sampler_mixed_uncached", uncachedRates);
        Report("shader_eval_unbiased", shaderRates);
    }

    private static double[] Measure(int windows, int warmup, Func<float> body, int samplesPerWindow)
    {
        float sink = 0f;
        for (int i = 0; i < warmup; i++) sink += body();
        var rates = new double[windows];
        for (int window = 0; window < windows; window++)
        {
            long start = Stopwatch.GetTimestamp();
            sink += body();
            double seconds = Stopwatch.GetElapsedTime(start).TotalSeconds;
            rates[window] = samplesPerWindow / seconds;
        }

        if (float.IsNaN(sink)) Console.WriteLine("  (sink NaN)");
        Array.Sort(rates);
        return rates;
    }

    private static void Report(string name, double[] sortedRates)
    {
        double median = sortedRates[sortedRates.Length / 2];
        Console.WriteLine(
            $"  MATERIAL_BENCH {name} median={median / 1e6:F1}M/s ({1e9 / median:F2} ns/sample) min={sortedRates[0] / 1e6:F1}M/s max={sortedRates[^1] / 1e6:F1}M/s");
    }

    // ---------------------------------------------------------------- fixtures

    private readonly record struct HitInput(bool HadHit, ulong Collider, int Shape, Vector3 Position, Vector3 Normal);

    private static void SampleRange(MaterialBindingTable table, HitInput[] inputs, int start, int end, MaterialSampleResult[] results, ref MaterialSampleStats stats)
    {
        var cache = default(MaterialLookupCache);
        for (int i = start; i < end; i++)
        {
            ref readonly HitInput input = ref inputs[i];
            results[i] = MaterialSampler.Sample(table, input.HadHit, input.Collider, input.Shape, input.Position, input.Normal, ref cache, ref stats);
        }
    }

    /// <summary>
    /// The mix is roughly 70% checker, 15% flat, 5% unbound, 5% unsupported, 2% no-hit, plus a few nonfinite
    /// and zero-normal hits. Coherent runs keep one key for 1–64 consecutive samples, like a film row.
    /// </summary>
    private static HitInput[] MixedInputs(int count, int seed, bool coherentRuns)
    {
        (ulong Collider, int Shape)[] checkers = { (1001, 0) };
        (ulong Collider, int Shape)[] flats = { (5, 0), (1001, 1), (1001, 3) };
        (ulong Collider, int Shape)[] unsupported = { (1002, 0), (77, 2) };
        var random = new Random(seed);
        var inputs = new HitInput[count];
        int remaining = 0;
        (ulong Collider, int Shape) key = default;
        int category = 0;
        for (int i = 0; i < count; i++)
        {
            if (!coherentRuns || remaining == 0)
            {
                int roll = random.Next(100);
                category = roll < 70 ? 0 : roll < 85 ? 1 : roll < 90 ? 2 : roll < 95 ? 3 : roll < 97 ? 4 : 5;
                key = category switch
                {
                    0 => checkers[0],
                    1 => flats[random.Next(flats.Length)],
                    2 => ((ulong)(20_000 + random.Next(100)), 0),
                    3 => unsupported[random.Next(unsupported.Length)],
                    _ => checkers[0],
                };
                remaining = coherentRuns ? 1 + random.Next(64) : 1;
            }

            remaining--;
            var position = new Vector3(NextCoordinate(random, 20f), NextCoordinate(random, 5f), NextCoordinate(random, 20f));
            Vector3 normal = NextNormal(random);
            bool hadHit = category != 4;
            if (category == 5)
            {
                int special = random.Next(3);
                if (special == 0) position = position with { X = float.NaN };
                else if (special == 1) normal = Vector3.Zero;
                else position = position with { Y = 0f };
            }

            inputs[i] = new HitInput(hadHit, key.Collider, key.Shape, position, normal);
        }

        return inputs;
    }

    private static MaterialBindingEntry[] FixtureEntries() => new[]
    {
        MaterialBindingEntry.Bound(new(1001, 0), MaterialDescriptor.CreateWorldChecker(WorldCheckerEvaluator.ShaderDefaults, GeometryCorrespondence.Exact)),
        MaterialBindingEntry.Bound(new(1001, 1), MaterialDescriptor.CreateFlat(FlatF, GeometryCorrespondence.Exact)),
        MaterialBindingEntry.Unsupported(new(1002, 0), MaterialUnsupportedReason.EmissionEnabled),
        MaterialBindingEntry.Bound(new(5, 0), MaterialDescriptor.CreateFlat(FlatF, GeometryCorrespondence.Exact)),
        MaterialBindingEntry.Unsupported(new(77, 2), MaterialUnsupportedReason.ConcaveFaceIndexAmbiguous),
        MaterialBindingEntry.Bound(new(1001, 3), MaterialDescriptor.CreateFlat(FlatG, GeometryCorrespondence.Proxy)),
    };

    private static MaterialBindingTable Fixture(long generation) => MaterialBindingTable.Build(generation, FixtureEntries());

    private static void ExpectParity(float x, float y, float z, float scale, int expected, string label)
    {
        var parameters = new WorldCheckerParams(A, B, scale);
        TestAssert.Equal(WorldCheckerEvaluation.Ok, WorldCheckerEvaluator.EvaluateShader(parameters, new Vector3(x, y, z), out WorldCheckerSample sample), label);
        TestAssert.Equal(expected, sample.Parity, label);
        TestAssert.Equal(expected == 0 ? A : B, sample.Color, $"{label}: colour selected exactly");
    }

    private static float NextCoordinate(Random random, float extent) => (float)((random.NextDouble() * 2.0 - 1.0) * extent);

    private static Vector3 NextNormal(Random random)
    {
        while (true)
        {
            var v = new Vector3(NextCoordinate(random, 1f), NextCoordinate(random, 1f), NextCoordinate(random, 1f));
            float lengthSquared = v.LengthSquared();
            if (lengthSquared > 1e-4f && lengthSquared <= 1f) return v / MathF.Sqrt(lengthSquared);
        }
    }

    private static void Throws<TException>(Action action, string label) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"{label}: expected {typeof(TException).Name}");
    }
}
