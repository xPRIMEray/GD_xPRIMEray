using System.Numerics;
using System.Security.Cryptography;
using XPrimeRay.Core.Geometry;
using XPrimeRay.Spatial;

namespace XPrimeRay.ObserverInstrumentation.Tests;

public static class SpatialKernelTests
{
    public static void Run()
    {
        SameGeometryHasSameSnapshotHash();
        PrimitivePermutationHasSameSnapshotHash();
        DuplicateCanonicalIdRejected();
        ProcessStableCanonicalBytes();
        SpatialAuthorityContextIsDeterministic();
        BvhAuthorityContextIsDeterministic();
        GeometryMutationChangesSpatialIdentity();
        SemanticMismatchIsNotHiddenByEqualCounts();
        StaleSpatialSnapshotRejected();
        PromotionGateRequiresQualifiedValidation();
        InsideSegment();
        FaceTouch();
        EndOnFace();
        EdgeTouch();
        CornerTouch();
        GrazingTangent();
        ParallelSlab();
        ZeroLengthInsideAndOutside();
        TransformedBox();
        NearestPrimitive();
        EqualDistanceTieBreak();
        ExcludedPrimitive();
        SphereIntersectionContract();
        MixedPrimitiveContract();
        BvhRejectsInvalidBounds();
        RepeatedQueriesStable();
        BvhBuildAndQueryParity();
        BvhScalingBenchmark();
        Console.WriteLine("PASS SpatialKernelTests");
    }

    private static void SameGeometryHasSameSnapshotHash()
    {
        FrozenGeometrySnapshot a = Snapshot(Box("/World/Wall", Vector3.Zero));
        FrozenGeometrySnapshot b = Snapshot(Box("/World/Wall", Vector3.Zero));
        Assert(a.GeometrySnapshotSha256 == b.GeometrySnapshotSha256, "same geometry hash");
    }

    private static void PrimitivePermutationHasSameSnapshotHash()
    {
        FrozenOrientedBox a = Box("/World/A", new Vector3(-2, 0, 0));
        FrozenOrientedBox b = Box("/World/B", new Vector3(2, 0, 0));
        Assert(Snapshot(a, b).GeometrySnapshotSha256 == Snapshot(b, a).GeometrySnapshotSha256, "permuted primitive order hash");
    }

    private static void DuplicateCanonicalIdRejected()
    {
        FrozenOrientedBox box = Box("/World/Duplicate", Vector3.Zero);
        bool rejected = false;
        try { _ = Snapshot(box, box); }
        catch (ArgumentException) { rejected = true; }
        Assert(rejected, "duplicate canonical ID rejected");
    }

    private static void ProcessStableCanonicalBytes()
    {
        FrozenGeometrySnapshot snapshot = Snapshot(Box("/World/Wall", Vector3.Zero));
        string a = Convert.ToHexString(SHA256.HashData(FrozenGeometrySnapshotCanonicalSerializer.Serialize(snapshot)));
        string b = Convert.ToHexString(SHA256.HashData(FrozenGeometrySnapshotCanonicalSerializer.Serialize(snapshot)));
        Assert(a == b, "canonical serialization repeat");
        Assert(a == "2CCDB17B271350D160F1CB953C89D552D34D215733E03EFCEBF37E45EE89D341", "pinned little-endian canonical fixture");
    }

    private static void SpatialAuthorityContextIsDeterministic()
    {
        FrozenGeometrySnapshot snapshot = Snapshot(Box("/World/Wall", Vector3.Zero));
        SpatialAuthorityContext a = new(snapshot.GeometrySnapshotSha256, LinearScanSpatialQuery.AuthorityTokenValue, LinearScanSpatialQuery.IntersectionPolicyVersion);
        SpatialAuthorityContext b = new(snapshot.GeometrySnapshotSha256, LinearScanSpatialQuery.AuthorityTokenValue, LinearScanSpatialQuery.IntersectionPolicyVersion);
        Assert(a.CanonicalSha256 == b.CanonicalSha256, "spatial authority context hash");
        SpatialAuthorityContext changed = new(snapshot.GeometrySnapshotSha256, "other", LinearScanSpatialQuery.IntersectionPolicyVersion);
        Assert(a.CanonicalSha256 != changed.CanonicalSha256, "spatial authority context distinguishes kernel");
    }

    private static void BvhAuthorityContextIsDeterministic()
    {
        FrozenGeometrySnapshot snapshot = Snapshot(Box("/World/A", new Vector3(-2, 0, 0)), Box("/World/B", new Vector3(2, 0, 0)));
        SpatialBvhQuery bvh = new(snapshot);
        BvhSpatialAuthorityContext a = BvhContext(snapshot, bvh.BuildSha256);
        BvhSpatialAuthorityContext b = BvhContext(snapshot, bvh.BuildSha256);
        Assert(a.CanonicalSha256 == b.CanonicalSha256, "BVH context is deterministic");
        FrozenGeometrySnapshot reordered = Snapshot(Box("/World/B", new Vector3(2, 0, 0)), Box("/World/A", new Vector3(-2, 0, 0)));
        Assert(a.CanonicalSha256 == BvhContext(reordered).CanonicalSha256, "BVH context survives input reorder");
        Assert(a.CanonicalSha256 != BvhContext(snapshot, "altered-build").CanonicalSha256, "BVH topology changes context");
        Assert(a.CanonicalSha256 != new BvhSpatialAuthorityContext(snapshot.GeometrySnapshotSha256, a.AuthorityToken, a.IntersectionPolicyVersion, "altered-policy", a.BvhBuildSha256).CanonicalSha256, "BVH build policy changes context");
        FrozenGeometrySnapshot changedGeometry = Snapshot(Box("/World/Wall", Vector3.Zero, halfExtents: new Vector3(2, 1, 1)));
        Assert(a.CanonicalSha256 != BvhContext(changedGeometry, bvh.BuildSha256).CanonicalSha256, "BVH geometry changes context");
    }

    private static void GeometryMutationChangesSpatialIdentity()
    {
        FrozenGeometrySnapshot original = Snapshot(Box("/World/Wall", Vector3.Zero, halfExtents: Vector3.One));
        FrozenGeometrySnapshot resized = Snapshot(Box("/World/Wall", Vector3.Zero, halfExtents: new Vector3(1.1f, 1, 1)));
        SpatialAuthorityContext a = new(original.GeometrySnapshotSha256, LinearScanSpatialQuery.AuthorityTokenValue, LinearScanSpatialQuery.IntersectionPolicyVersion);
        SpatialAuthorityContext b = new(resized.GeometrySnapshotSha256, LinearScanSpatialQuery.AuthorityTokenValue, LinearScanSpatialQuery.IntersectionPolicyVersion);
        Assert(a.CanonicalSha256 != b.CanonicalSha256, "box resize changes spatial identity");
    }

    private static void SemanticMismatchIsNotHiddenByEqualCounts()
    {
        SpatialContactComparison comparison = SpatialContactComparer.Compare(
            new[] { 1 }, new[] { 1 }, new byte[] { 1 }, new byte[] { 0 }, new byte[] { 0 }, new byte[] { 1 });
        Assert(comparison.ContactCountMismatchPixels == 0, "equal counts remain equal");
        Assert(comparison.SurfaceSemanticMismatchPixels == 1 && comparison.TotalAuthorityMismatchPixels == 1, "semantic mismatch detected");
    }

    private static void StaleSpatialSnapshotRejected()
    {
        FrozenGeometrySnapshot original = Snapshot(Box("/World/Wall", Vector3.Zero));
        FrozenGeometrySnapshot resized = Snapshot(Box("/World/Wall", Vector3.Zero, halfExtents: new Vector3(2, 1, 1)));
        SpatialAuthorityContext context = new(original.GeometrySnapshotSha256, LinearScanSpatialQuery.AuthorityTokenValue, LinearScanSpatialQuery.IntersectionPolicyVersion);
        BvhSpatialAuthorityContext bvhContext = BvhContext(original);
        Assert(!Promote(resized, context, bvhContext, context.CanonicalSha256, bvhContext.CanonicalSha256, new(), out string reason) && reason == "frozen_geometry_drifted", "stale frozen snapshot rejected");
    }

    private static void PromotionGateRequiresQualifiedValidation()
    {
        FrozenGeometrySnapshot snapshot = Snapshot(Box("/World/Wall", Vector3.Zero));
        SpatialAuthorityContext context = new(snapshot.GeometrySnapshotSha256, LinearScanSpatialQuery.AuthorityTokenValue, LinearScanSpatialQuery.IntersectionPolicyVersion);
        BvhSpatialAuthorityContext bvhContext = BvhContext(snapshot);
        Assert(Promote(snapshot, context, bvhContext, context.CanonicalSha256, bvhContext.CanonicalSha256, new(), out _), "qualified promotion accepted");
        Assert(!Promote(snapshot, context, bvhContext, context.CanonicalSha256, bvhContext.CanonicalSha256, new(1, 0, 0, 0, 0, 0), out string pixelReason) && pixelReason == "bvh_parity_failure", "BVH pixel mismatch blocks promotion");
        Assert(!Promote(snapshot, context, bvhContext, context.CanonicalSha256, bvhContext.CanonicalSha256, new(0, 1, 0, 0, 0, 0), out string queryReason) && queryReason == "bvh_parity_failure", "BVH query mismatch blocks promotion");
        Assert(!Promote(snapshot, context, bvhContext, context.CanonicalSha256, bvhContext.CanonicalSha256, new(0, 0, 0, 0, 1, 0), out string semanticReason) && semanticReason == "bvh_parity_failure", "BVH semantic mismatch blocks promotion");
        Assert(!Promote(snapshot, context, bvhContext, context.CanonicalSha256, bvhContext.CanonicalSha256, new(0, 0, 0, 0, 0, 1), out string tReason) && tReason == "bvh_parity_failure", "BVH SegmentT mismatch blocks promotion");
        Assert(!Promote(snapshot, context, bvhContext, context.CanonicalSha256, "altered-bvh-context", new(), out string bvhContextReason) && bvhContextReason == "bvh_context_drifted", "BVH context drift blocks promotion");
        Assert(!Promote(snapshot, context, bvhContext, context.CanonicalSha256, bvhContext.CanonicalSha256, new(), out _, dualValidationPresent: false), "missing validation blocks promotion");
        Assert(!Promote(snapshot, context, bvhContext, context.CanonicalSha256, bvhContext.CanonicalSha256, new(), out string unsupportedReason, supportedGeometry: false) && unsupportedReason == "unsupported_geometry", "unsupported geometry blocks promotion");
        Assert(!Promote(snapshot, context, bvhContext, context.CanonicalSha256, bvhContext.CanonicalSha256, new(), out string diagnosticReason, diagnosticFailure: "diagnostic_failed") && diagnosticReason == "spatial_diagnostic_failure", "diagnostic failure blocks promotion");
        Assert(!Promote(snapshot, context, bvhContext, "stale-context", bvhContext.CanonicalSha256, new(), out string staleContextReason) && staleContextReason == "spatial_context_drifted", "stale context blocks promotion");
    }

    private static void InsideSegment()
    {
        LinearScanSpatialQuery query = new(Snapshot(Box("/World/Box", Vector3.Zero)));
        Assert(query.IntersectsSegment(Vector3.Zero, new Vector3(2, 0, 0), 1, out SurfaceHit hit), "inside hit");
        Assert(hit.SegmentT == 0f && !hit.NormalValid, "inside endpoint semantics");
    }

    private static void FaceTouch() => AssertHit(new Vector3(-2, 0, 0), new Vector3(-1, 0, 0), 1f, "face touch");
    private static void EndOnFace() => AssertHit(new Vector3(-2, 0, 0), new Vector3(1, 0, 0), 1f / 3f, "end on face");
    private static void EdgeTouch() => AssertHit(new Vector3(-2, 1, 0), new Vector3(0, 1, 0), 0.5f, "edge touch");
    private static void CornerTouch() => AssertHit(new Vector3(-2, 1, 1), new Vector3(0, 1, 1), 0.5f, "corner touch");
    private static void GrazingTangent() => AssertHit(new Vector3(-2, 1, 0), new Vector3(2, 1, 0), 0.25f, "grazing tangent");
    private static void ParallelSlab()
    {
        LinearScanSpatialQuery query = new(Snapshot(Box("/World/Box", Vector3.Zero)));
        Assert(!query.IntersectsSegment(new Vector3(-2, 2, 0), new Vector3(2, 2, 0), 1, out _), "parallel outside slab");
    }

    private static void ZeroLengthInsideAndOutside()
    {
        LinearScanSpatialQuery query = new(Snapshot(Box("/World/Box", Vector3.Zero)));
        Assert(query.IntersectsSegment(Vector3.Zero, Vector3.Zero, 1, out _), "zero length inside");
        Assert(!query.IntersectsSegment(new Vector3(2, 0, 0), new Vector3(2, 0, 0), 1, out _), "zero length outside");
    }

    private static void TransformedBox()
    {
        Matrix4x4 world = Matrix4x4.CreateRotationZ(MathF.PI / 4f) * Matrix4x4.CreateTranslation(3, 0, 0);
        LinearScanSpatialQuery query = new(Snapshot(Box("/World/Rotated", Vector3.Zero, world)));
        Assert(query.IntersectsSegment(Vector3.Zero, new Vector3(6, 0, 0), 1, out _), "transformed oriented box");
    }

    private static void NearestPrimitive()
    {
        LinearScanSpatialQuery query = new(Snapshot(Box("/World/Far", new Vector3(4, 0, 0)), Box("/World/Near", new Vector3(2, 0, 0))));
        Assert(query.IntersectsSegment(Vector3.Zero, new Vector3(8, 0, 0), 1, out SurfaceHit hit), "nearest query hit");
        Assert(hit.CanonicalPrimitiveId.StartsWith("/World/Near", StringComparison.Ordinal), "nearest primitive");
    }

    private static void EqualDistanceTieBreak()
    {
        LinearScanSpatialQuery query = new(Snapshot(Box("/World/Z", new Vector3(2, 0, 0)), Box("/World/A", new Vector3(2, 0, 0))));
        Assert(query.IntersectsSegment(Vector3.Zero, new Vector3(8, 0, 0), 1, out SurfaceHit hit), "tie query hit");
        Assert(hit.CanonicalPrimitiveId.StartsWith("/World/A", StringComparison.Ordinal), "ordinal tie break");
    }

    private static void ExcludedPrimitive()
    {
        FrozenOrientedBox source = Box("/World/Excluded", Vector3.Zero);
        FrozenOrientedBox excluded = new(source.CanonicalPrimitiveId, source.SurfaceClass, source.WorldFromLocal,
            source.LocalFromWorld, source.HalfExtents, source.WorldBounds, source.CollisionLayer, FrozenPrimitiveFlags.Excluded);
        LinearScanSpatialQuery query = new(Snapshot(excluded));
        Assert(!query.IntersectsSegment(new Vector3(-2, 0, 0), new Vector3(2, 0, 0), 1, out _), "excluded primitive");
    }

    private static void SphereIntersectionContract()
    {
        FrozenSphere sphere = Sphere("/World/Sphere", Vector3.Zero, 1f);
        LinearScanSpatialQuery query = new(Mixed(Array.Empty<FrozenOrientedBox>(), new[] { sphere }));
        Assert(query.IntersectsSegment(new Vector3(-2, 0, 0), new Vector3(2, 0, 0), 1, out SurfaceHit center), "sphere centerline hit");
        Assert(center.SegmentT == 0.25f && center.Position == new Vector3(-1, 0, 0) && center.Normal == -Vector3.UnitX && center.NormalValid,
            "sphere centerline t/position/normal");
        Assert(!query.IntersectsSegment(new Vector3(-2, 1.1f, 0), new Vector3(2, 1.1f, 0), 1, out _), "sphere miss");
        Assert(query.IntersectsSegment(new Vector3(-2, 1, 0), new Vector3(2, 1, 0), 1, out SurfaceHit tangent) && tangent.SegmentT == 0.5f,
            "sphere tangent");
        Assert(query.IntersectsSegment(Vector3.Zero, new Vector3(2, 0, 0), 1, out SurfaceHit inside) && inside.SegmentT == 0f && !inside.NormalValid,
            "sphere start inside");
        Assert(query.IntersectsSegment(new Vector3(-1, 0, 0), new Vector3(-2, 0, 0), 1, out SurfaceHit surface) && surface.SegmentT == 0f && !surface.NormalValid,
            "sphere start on surface");
        Assert(query.IntersectsSegment(new Vector3(-2, 0, 0), new Vector3(-1, 0, 0), 1, out SurfaceHit endpoint) && endpoint.SegmentT == 1f,
            "sphere endpoint contact");
        Assert(!query.IntersectsSegment(new Vector3(-2, 0, 0), new Vector3(-1.1f, 0, 0), 1, out _), "sphere beyond endpoint");
        Assert(!query.IntersectsSegment(new Vector3(-2, 0, 0), new Vector3(2, 0, 0), 2, out _), "sphere collision mask");

        FrozenSphere translated = Sphere("/World/Translated", new Vector3(4, -2, 3), 1.5f);
        LinearScanSpatialQuery translatedQuery = new(Mixed(Array.Empty<FrozenOrientedBox>(), new[] { translated }));
        Assert(translatedQuery.IntersectsSegment(new Vector3(1, -2, 3), new Vector3(7, -2, 3), 1, out SurfaceHit translatedHit) &&
            translatedHit.Position == new Vector3(2.5f, -2, 3), "translated sphere");
    }

    private static void MixedPrimitiveContract()
    {
        FrozenOrientedBox box = Box("/World/ZBox", Vector3.Zero);
        FrozenSphere sphere = Sphere("/World/ASphere", Vector3.Zero, 1f);
        FrozenOrientedBox secondBox = Box("/World/BBox", new Vector3(8, 0, 0));
        FrozenSphere secondSphere = Sphere("/World/YSphere", new Vector3(-8, 0, 0), 2f);
        FrozenGeometrySnapshot mixed = Mixed(new[] { box, secondBox }, new[] { sphere, secondSphere });
        FrozenGeometrySnapshot permuted = Mixed(new[] { secondBox, box }, new[] { secondSphere, sphere });
        Assert(mixed.EffectiveSchemaVersion == FrozenGeometrySnapshot.MixedSchemaVersion, "mixed schema version");
        Assert(mixed.IntersectionPolicyVersion == LinearScanSpatialQuery.MixedIntersectionPolicyVersion, "mixed policy version");
        Assert(mixed.GeometrySnapshotSha256 == permuted.GeometrySnapshotSha256, "mixed snapshot stable");
        Assert(new SpatialBvhQuery(mixed).BuildSha256 == new SpatialBvhQuery(permuted).BuildSha256, "mixed BVH stable");

        LinearScanSpatialQuery linear = new(mixed);
        SpatialBvhQuery bvh = new(mixed);
        Assert(linear.IntersectsSegment(new Vector3(-2, 0, 0), new Vector3(2, 0, 0), 1, out SurfaceHit linearHit), "mixed linear hit");
        Assert(bvh.IntersectsSegment(new Vector3(-2, 0, 0), new Vector3(2, 0, 0), 1, out SurfaceHit bvhHit), "mixed BVH hit");
        Assert(linearHit.CanonicalPrimitiveId == "/World/ASphere" && linearHit.CanonicalPrimitiveId == bvhHit.CanonicalPrimitiveId &&
            linearHit.SegmentT == bvhHit.SegmentT, "mixed equal-t canonical tie and parity");

        bool duplicateRejected = false;
        try { _ = Mixed(new[] { Box("/World/Duplicate", Vector3.Zero) }, new[] { Sphere("/World/Duplicate", Vector3.Zero, 1f) }); }
        catch (ArgumentException) { duplicateRejected = true; }
        Assert(duplicateRejected, "cross-kind duplicate canonical ID rejected");

        FrozenGeometrySnapshot legacy = Snapshot(box);
        FrozenGeometrySnapshot explicitEmptySphere = Mixed(new[] { box }, Array.Empty<FrozenSphere>());
        Assert(legacy.EffectiveSchemaVersion == FrozenGeometrySnapshot.SchemaVersion &&
            FrozenGeometrySnapshotCanonicalSerializer.Serialize(legacy).SequenceEqual(FrozenGeometrySnapshotCanonicalSerializer.Serialize(explicitEmptySphere)),
            "box-only constructor and bytes remain compatible");
        Assert(new SpatialBvhQuery(legacy).BuildSha256 == new SpatialBvhQuery(explicitEmptySphere).BuildSha256,
            "box-only BVH remains compatible");
    }

    private static void RepeatedQueriesStable()
    {
        LinearScanSpatialQuery query = new(Snapshot(Box("/World/Box", Vector3.Zero)));
        for (int i = 0; i < 20; i++)
        {
            Assert(query.IntersectsSegment(new Vector3(-2, 0, 0), new Vector3(2, 0, 0), 1, out SurfaceHit hit), "repeat query hit");
            Assert(hit.SegmentT == 0.25f, "repeat query result");
        }
    }

    private static void BvhBuildAndQueryParity()
    {
        FrozenOrientedBox[] boxes = Enumerable.Range(0, 17)
            .Select(i => Box($"/World/Box{i:D3}", new Vector3((i % 5) * 3f, (i / 5) * 2f, 0f)))
            .ToArray();
        FrozenGeometrySnapshot snapshot = Snapshot(boxes);
        FrozenGeometrySnapshot permuted = Snapshot(boxes.Reverse().ToArray());
        SpatialBvhQuery bvh = new(snapshot);
        SpatialBvhQuery bvhPermuted = new(permuted);
        Assert(bvh.BuildSha256 == bvhPermuted.BuildSha256, "BVH fingerprint is input-order independent");
        LinearScanSpatialQuery linear = new(snapshot);
        BenchmarkRay[] corpus = BuildBenchmarkCorpus(17);
        foreach (BenchmarkRay ray in corpus)
        {
            bool linearHit = linear.IntersectsSegment(ray.From, ray.To, 1, out SurfaceHit linearResult);
            bool bvhHit = bvh.IntersectsSegment(ray.From, ray.To, 1, out SurfaceHit bvhResult);
            Assert(linearHit == bvhHit, "BVH hit parity");
            if (linearHit)
            {
                Assert(linearResult.CanonicalPrimitiveId == bvhResult.CanonicalPrimitiveId, "BVH primitive parity");
                Assert(linearResult.SurfaceClass == bvhResult.SurfaceClass, "BVH surface parity");
                Assert(linearResult.SegmentT == bvhResult.SegmentT, "BVH SegmentT parity");
            }
        }
        Assert(bvh.PrimitiveTestCount <= linear.PrimitiveTestCount, "BVH does not increase global primitive tests");

        LinearScanSpatialQuery intersectingLinear = new(snapshot);
        SpatialBvhQuery intersectingBvh = new(snapshot);
        foreach (BenchmarkRay ray in corpus.Where(ray => ray.ExpectedIntersecting))
        {
            intersectingLinear.IntersectsSegment(ray.From, ray.To, 1, out _);
            intersectingBvh.IntersectsSegment(ray.From, ray.To, 1, out _);
        }
        Assert(intersectingBvh.PrimitiveTestCount < intersectingLinear.PrimitiveTestCount, "BVH reduces known intersecting subset");
    }

    private static void BvhScalingBenchmark()
    {
        foreach (int size in new[] { 12, 64, 256, 1024, 4096, 10000 })
        {
            FrozenGeometrySnapshot snapshot = Snapshot(Enumerable.Range(0, size)
                .Select(i => Box($"/Bench/Box{i:D5}", new Vector3((i % 100) * 3f, (i / 100) * 3f, 0f)))
                .ToArray());
            BenchmarkRay[] corpus = BuildBenchmarkCorpus(size);
            // Warm both implementations before measuring. Timed instances keep
            // correctness counters limited to the measured corpus only.
            SpatialBvhQuery warmBvh = new(snapshot);
            LinearScanSpatialQuery warmLinear = new(snapshot);
            for (int warmup = 0; warmup < 3; warmup++)
                foreach (BenchmarkRay ray in corpus)
                {
                    warmLinear.IntersectsSegment(ray.From, ray.To, 1, out _);
                    warmBvh.IntersectsSegment(ray.From, ray.To, 1, out _);
                }

            SpatialBvhQuery bvh = new(snapshot);
            Assert(warmBvh.BuildSha256 == bvh.BuildSha256, "BVH build fingerprint stable after warmup");
            LinearScanSpatialQuery linear = new(snapshot);
            const int repetitions = 128;
            var linearTimer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < repetitions; i++)
                foreach (BenchmarkRay ray in corpus) linear.IntersectsSegment(ray.From, ray.To, 1, out _);
            linearTimer.Stop();
            var bvhTimer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < repetitions; i++)
                foreach (BenchmarkRay ray in corpus) bvh.IntersectsSegment(ray.From, ray.To, 1, out _);
            bvhTimer.Stop();
            double linearMs = linearTimer.Elapsed.TotalMilliseconds;
            double bvhMs = bvhTimer.Elapsed.TotalMilliseconds;
            double queryTotal = repetitions * corpus.Length;
            Console.WriteLine($"BVH_BENCH size={size} queries={queryTotal} buildMs={bvh.BuildElapsedMilliseconds:0.###} linearMs={linearMs:0.###} bvhMs={bvhMs:0.###} linearQps={queryTotal / System.Math.Max(0.001, linearMs) * 1000.0:0.###} bvhQps={queryTotal / System.Math.Max(0.001, bvhMs) * 1000.0:0.###} speedup={linearMs / System.Math.Max(0.001, bvhMs):0.###} linearPrim={linear.PrimitiveTestCount} bvhNodes={bvh.NodeTestCount} bvhPrim={bvh.PrimitiveTestCount} depth={bvh.MaxDepth} buildSha={bvh.BuildSha256}");
            Assert(bvh.PrimitiveTestCount <= linear.PrimitiveTestCount, "BVH benchmark primitive tests do not increase");
        }
    }

    private static void BvhRejectsInvalidBounds()
    {
        FrozenOrientedBox template = Box("/World/Invalid", Vector3.Zero);
        AssertBvhRejects(new Aabb3(new Vector3(float.NaN, 0, 0), new Vector3(1, 1, 1)), "NaN bounds");
        AssertBvhRejects(new Aabb3(new Vector3(0, 0, 0), new Vector3(float.PositiveInfinity, 1, 1)), "infinite bounds");
        AssertBvhRejects(new Aabb3(new Vector3(2, 0, 0), new Vector3(1, 1, 1)), "reversed degenerate bounds");

        void AssertBvhRejects(Aabb3 bounds, string label)
        {
            FrozenOrientedBox invalid = new(template.CanonicalPrimitiveId, template.SurfaceClass,
                template.WorldFromLocal, template.LocalFromWorld, template.HalfExtents, bounds,
                template.CollisionLayer, template.Flags);
            bool rejected = false;
            try { _ = new SpatialBvhQuery(Snapshot(invalid)); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, label + " rejected");
        }
    }

    private readonly record struct BenchmarkRay(Vector3 From, Vector3 To, string Label, bool ExpectedIntersecting);

    private static BenchmarkRay[] BuildBenchmarkCorpus(int size)
    {
        float maxX = ((size - 1) % 100) * 3f;
        float maxY = ((size - 1) / 100) * 3f;
        float farX = maxX + 4f;
        float farY = MathF.Max(4f, maxY + 4f);
        return new[]
        {
            new BenchmarkRay(new Vector3(-4, -4, -2), new Vector3(farX, farY, 2), "full-scene-diagonal", true),
            new BenchmarkRay(new Vector3(-4, -4, 1.05f), new Vector3(farX, farY, 1.05f), "near-miss", false),
            new BenchmarkRay(new Vector3(-4, 0, 0), new Vector3(farX, 0, 0), "axis-aligned", true),
            new BenchmarkRay(new Vector3(-4, farY, -0.5f), new Vector3(farX, -2, 0.5f), "oblique", true),
            new BenchmarkRay(Vector3.Zero, new Vector3(farX, farY, 0), "start-inside", true),
            new BenchmarkRay(new Vector3(0, 0, -4), new Vector3(0, 0, 4), "axis-z", true),
            new BenchmarkRay(new Vector3(-4, maxY * 0.25f, 0), new Vector3(farX, maxY * 0.25f, 0), "sparse-sweep-a", true),
            new BenchmarkRay(new Vector3(-4, maxY * 0.75f + 0.37f, 0), new Vector3(farX, maxY * 0.75f + 0.37f, 0), "sparse-sweep-b", false)
        };
    }

    private static void AssertHit(Vector3 from, Vector3 to, float expectedT, string name)
    {
        LinearScanSpatialQuery query = new(Snapshot(Box("/World/Box", Vector3.Zero)));
        Assert(query.IntersectsSegment(from, to, 1, out SurfaceHit hit), name);
        Assert(hit.SegmentT == expectedT && hit.NormalValid, name + " t/normal");
    }

    private static FrozenGeometrySnapshot Snapshot(params FrozenOrientedBox[] boxes) => new(boxes);

    private static FrozenGeometrySnapshot Mixed(IEnumerable<FrozenOrientedBox> boxes, IEnumerable<FrozenSphere> spheres) => new(boxes, spheres);

    private static BvhSpatialAuthorityContext BvhContext(FrozenGeometrySnapshot snapshot, string? buildSha = null)
    {
        SpatialBvhQuery bvh = new(snapshot);
        return new BvhSpatialAuthorityContext(
            snapshot.GeometrySnapshotSha256,
            SpatialBvhQuery.AuthorityTokenValue,
            snapshot.IntersectionPolicyVersion,
            SpatialBvhQuery.BuildPolicyVersion,
            buildSha ?? bvh.BuildSha256);
    }

    private static bool Promote(
        FrozenGeometrySnapshot snapshot,
        SpatialAuthorityContext context,
        BvhSpatialAuthorityContext bvhContext,
        string validationContext,
        string validationBvhContext,
        BvhParityStatus parity,
        out string reason,
        bool dualValidationPresent = true,
        bool supportedGeometry = true,
        string diagnosticFailure = "") => SpatialAuthorityPromotionGate.CanPromote(
            snapshot,
            context,
            bvhContext,
            dualValidationPresent,
            validationContext,
            validationBvhContext,
            0,
            0,
            supportedGeometry,
            diagnosticFailure,
            parity,
            out reason);

    private static FrozenOrientedBox Box(string id, Vector3 center, Matrix4x4? transform = null, Vector3? halfExtents = null)
    {
        Matrix4x4 world = transform ?? Matrix4x4.CreateTranslation(center);
        Matrix4x4.Invert(world, out Matrix4x4 inverse);
        Vector3 half = halfExtents ?? Vector3.One;
        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            Vector3 local = new((i & 1) != 0 ? half.X : -half.X, (i & 2) != 0 ? half.Y : -half.Y, (i & 4) != 0 ? half.Z : -half.Z);
            Vector3 p = Vector3.Transform(local, world);
            min = Vector3.Min(min, p); max = Vector3.Max(max, p);
        }
        return new FrozenOrientedBox(id, SpatialSurfaceClass.Geometry, world, inverse, half, new Aabb3(min, max), 1, FrozenPrimitiveFlags.CollideWithBodies);
    }

    private static FrozenSphere Sphere(string id, Vector3 center, float radius, uint collisionLayer = 1) =>
        new(id, SpatialSurfaceClass.Geometry, center, radius, collisionLayer, FrozenPrimitiveFlags.CollideWithBodies);

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("SpatialKernelTests: " + message);
    }
}
