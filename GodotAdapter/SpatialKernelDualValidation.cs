using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XPrimeRay.Spatial;

namespace GodotAdapter;

public readonly record struct GodotReplayQueryObservation(
    bool Hit,
    ulong ColliderInstanceId,
    string ColliderPath,
    string ColliderType,
    int ShapeIndex,
    Godot.Vector3 Position,
    Godot.Vector3 Normal);

public sealed class SpatialKernelDualValidationResult
{
    public required string GeometrySnapshotSha256 { get; init; }
    public required string IntersectionPolicyVersion { get; init; }
    public required int PrimitiveCount { get; init; }
    public required int QueryCount { get; init; }
    public required string GodotContactCountSha256 { get; init; }
    public required string LinearContactCountSha256 { get; init; }
    public required string SpatialAuthorityContextSha256 { get; init; }
    public required string BvhSpatialAuthorityContextSha256 { get; init; }
    public required string GodotContactHistogram { get; init; }
    public required string LinearContactHistogram { get; init; }
    public required int MismatchPixelCount { get; init; }
    public required int ContactCountMismatchPixelCount { get; init; }
    public required int SurfaceSemanticMismatchPixelCount { get; init; }
    public required int TotalAuthorityMismatchPixelCount { get; init; }
    public required int GodotQueryComparisonCount { get; init; }
    public required int MismatchQueryCount { get; init; }
    public required string FirstMismatch { get; init; }
    public required long PrimitiveTestCount { get; init; }
    public required double ElapsedMilliseconds { get; init; }
    public required int[] LinearContactCounts { get; init; }
    public required int[] BvhContactCounts { get; init; }
    public required string BvhContactCountSha256 { get; init; }
    public required int BvhHitMismatchQueryCount { get; init; }
    public required int BvhPrimitiveIdentityMismatchQueryCount { get; init; }
    public required int BvhSurfaceSemanticMismatchQueryCount { get; init; }
    public required int BvhSegmentTMismatchQueryCount { get; init; }
    public required int BvhMismatchQueryCount { get; init; }
    public required int BvhContactCountMismatchPixelCount { get; init; }
    public required int BvhSurfaceSemanticMismatchPixelCount { get; init; }
    public required int BvhTotalMismatchPixelCount { get; init; }
    public required long BvhPrimitiveTestCount { get; init; }
    public required long BvhNodeTestCount { get; init; }
    public required double BvhElapsedMilliseconds { get; init; }
    public required double BvhBuildElapsedMilliseconds { get; init; }
    public required int BvhNodeCount { get; init; }
    public required int BvhLeafCount { get; init; }
    public required int BvhMaxDepth { get; init; }
    public required string BvhBuildSha256 { get; init; }
    public required string QueryDivergenceJson { get; init; }
}

public static class SpatialKernelDualValidator
{
    public const string DiagnosticAuthorityToken = LinearScanSpatialQuery.AuthorityTokenValue;

    public static SpatialKernelDualValidationResult Evaluate(
        Godot.Node sceneRoot,
        RayBeamRenderer.FormalProbeQuery[] queries,
        int[] godotContactCounts,
        int totalPixels,
        bool[]? godotQueryHits = null,
        GodotReplayQueryObservation[]? godotQueryObservations = null,
        byte[]? godotHadAnyGeometryContact = null,
        byte[]? godotHadAnyBackgroundContact = null,
        string sourceGroup = "fixture_source",
        string backgroundGroup = "fixture_background")
    {
        if (sceneRoot == null) throw new ArgumentNullException(nameof(sceneRoot));
        if (queries == null) throw new ArgumentNullException(nameof(queries));
        if (godotContactCounts == null) throw new ArgumentNullException(nameof(godotContactCounts));
        if (totalPixels < 0 || godotContactCounts.Length < totalPixels)
            throw new InvalidDataException("Godot contact channel is shorter than the film.");
        FrozenGeometrySnapshot snapshot = SpatialSnapshotBuilder.BuildFromGodotScene(sceneRoot, sourceGroup, backgroundGroup);
        return Evaluate(snapshot, queries, godotContactCounts, totalPixels, godotQueryHits, godotQueryObservations, godotHadAnyGeometryContact, godotHadAnyBackgroundContact);
    }

    public static SpatialKernelDualValidationResult Evaluate(
        FrozenGeometrySnapshot snapshot,
        RayBeamRenderer.FormalProbeQuery[] queries,
        int[] godotContactCounts,
        int totalPixels,
        bool[]? godotQueryHits = null,
        GodotReplayQueryObservation[]? godotQueryObservations = null,
        byte[]? godotHadAnyGeometryContact = null,
        byte[]? godotHadAnyBackgroundContact = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(godotContactCounts);
        if (totalPixels < 0 || godotContactCounts.Length < totalPixels)
            throw new InvalidDataException("Godot contact channel is shorter than the film.");
        if (godotQueryHits != null && godotQueryHits.Length != queries.Length)
            throw new InvalidDataException("Godot query-hit channel does not match the formal query dataset.");
        if (godotQueryObservations != null && godotQueryObservations.Length != queries.Length)
            throw new InvalidDataException("Godot query-observation channel does not match the formal query dataset.");
        if (godotHadAnyGeometryContact != null && godotHadAnyGeometryContact.Length < totalPixels)
            throw new InvalidDataException("Godot geometry-contact channel is shorter than the film.");
        if (godotHadAnyBackgroundContact != null && godotHadAnyBackgroundContact.Length < totalPixels)
            throw new InvalidDataException("Godot background-contact channel is shorter than the film.");
        LinearScanSpatialQuery query = new(snapshot);
        SpatialBvhQuery bvh = new(snapshot);
        int[] linearCounts = new int[totalPixels];
        byte[] linearGeometry = new byte[totalPixels];
        byte[] linearBackground = new byte[totalPixels];
        int[] bvhCounts = new int[totalPixels];
        byte[] bvhGeometry = new byte[totalPixels];
        byte[] bvhBackground = new byte[totalPixels];
        int countMismatchPixels = 0;
        int semanticMismatchPixels = 0;
        int totalMismatchPixels = 0;
        int mismatchQueries = 0;
        string firstMismatch = "none";
        int bvhHitMismatches = 0;
        int bvhPrimitiveMismatches = 0;
        int bvhSurfaceMismatches = 0;
        int bvhSegmentTMismatches = 0;
        int bvhMismatchQueries = 0;
        long linearTicks = 0;
        long bvhTicks = 0;
        List<QueryDivergenceRecord> divergences = new();
        Dictionary<string, IFrozenSpatialPrimitive> primitivesById = snapshot.AllPrimitives
            .ToDictionary(primitive => primitive.CanonicalPrimitiveId, StringComparer.Ordinal);

        for (int queryIndex = 0; queryIndex < queries.Length; queryIndex++)
        {
            RayBeamRenderer.FormalProbeQuery recorded = queries[queryIndex];
            if (recorded.PixelIndex < 0 || recorded.PixelIndex >= totalPixels)
                throw new InvalidDataException($"formal query pixel out of range: {recorded.PixelIndex}");
            long linearStart = System.Diagnostics.Stopwatch.GetTimestamp();
            bool linearHit = query.IntersectsSegment(ToNumerics(recorded.From), ToNumerics(recorded.To), recorded.CollisionMask, out SurfaceHit surfaceHit);
            linearTicks += System.Diagnostics.Stopwatch.GetTimestamp() - linearStart;
            if (linearHit)
            {
                linearCounts[recorded.PixelIndex]++;
                if (surfaceHit.SurfaceClass == SpatialSurfaceClass.Geometry) linearGeometry[recorded.PixelIndex] = 1;
                if (surfaceHit.SurfaceClass == SpatialSurfaceClass.Background) linearBackground[recorded.PixelIndex] = 1;
            }
            bool godotHit = godotQueryHits != null && godotQueryHits[queryIndex];
            if (godotQueryHits != null && linearHit != godotHit)
                mismatchQueries++;

            long bvhStart = System.Diagnostics.Stopwatch.GetTimestamp();
            bool bvhHit = bvh.IntersectsSegment(ToNumerics(recorded.From), ToNumerics(recorded.To), recorded.CollisionMask, out SurfaceHit bvhResult);
            bvhTicks += System.Diagnostics.Stopwatch.GetTimestamp() - bvhStart;
            if (bvhHit)
            {
                bvhCounts[recorded.PixelIndex]++;
                if (bvhResult.SurfaceClass == SpatialSurfaceClass.Geometry) bvhGeometry[recorded.PixelIndex] = 1;
                if (bvhResult.SurfaceClass == SpatialSurfaceClass.Background) bvhBackground[recorded.PixelIndex] = 1;
            }
            bool bvhQueryMismatch = linearHit != bvhHit;
            if (bvhQueryMismatch) bvhHitMismatches++;
            else if (linearHit)
            {
                if (!string.Equals(surfaceHit.CanonicalPrimitiveId, bvhResult.CanonicalPrimitiveId, StringComparison.Ordinal)) { bvhPrimitiveMismatches++; bvhQueryMismatch = true; }
                if (surfaceHit.SurfaceClass != bvhResult.SurfaceClass) { bvhSurfaceMismatches++; bvhQueryMismatch = true; }
                if (surfaceHit.SegmentT != bvhResult.SegmentT) { bvhSegmentTMismatches++; bvhQueryMismatch = true; }
            }
            if (bvhQueryMismatch) bvhMismatchQueries++;

            if (godotQueryHits != null && godotQueryObservations != null && linearHit != godotHit)
            {
                GodotReplayQueryObservation godot = godotQueryObservations[queryIndex];
                divergences.Add(BuildDivergenceRecord(
                    queryIndex,
                    in recorded,
                    godotHit,
                    in godot,
                    linearHit,
                    in surfaceHit,
                    bvhHit,
                    in bvhResult,
                    primitivesById));
            }
        }
        int bvhCountMismatchPixels = 0;
        int bvhSemanticMismatchPixels = 0;
        int bvhTotalMismatchPixels = 0;
        for (int pixel = 0; pixel < totalPixels; pixel++)
        {
            int godot = pixel < godotContactCounts.Length ? godotContactCounts[pixel] : -1;
            bool countMismatch = godot != linearCounts[pixel];
            bool semanticMismatch = godotHadAnyGeometryContact != null && godotHadAnyBackgroundContact != null &&
                (godotHadAnyGeometryContact[pixel] != linearGeometry[pixel] || godotHadAnyBackgroundContact[pixel] != linearBackground[pixel]);
            if (countMismatch) countMismatchPixels++;
            if (semanticMismatch) semanticMismatchPixels++;
            if (countMismatch || semanticMismatch) totalMismatchPixels++;
            if ((countMismatch || semanticMismatch) && firstMismatch == "none")
                firstMismatch = $"pixel={pixel} count={godot}->{linearCounts[pixel]} geometry={godotHadAnyGeometryContact?[pixel] ?? (byte)255}->{linearGeometry[pixel]} background={godotHadAnyBackgroundContact?[pixel] ?? (byte)255}->{linearBackground[pixel]}";
            bool bvhCountMismatch = linearCounts[pixel] != bvhCounts[pixel];
            bool bvhSemanticMismatch = linearGeometry[pixel] != bvhGeometry[pixel] || linearBackground[pixel] != bvhBackground[pixel];
            if (bvhCountMismatch) bvhCountMismatchPixels++;
            if (bvhSemanticMismatch) bvhSemanticMismatchPixels++;
            if (bvhCountMismatch || bvhSemanticMismatch) bvhTotalMismatchPixels++;
        }
        return new SpatialKernelDualValidationResult
        {
            GeometrySnapshotSha256 = snapshot.GeometrySnapshotSha256,
            IntersectionPolicyVersion = snapshot.IntersectionPolicyVersion,
            PrimitiveCount = snapshot.PrimitiveCount,
            QueryCount = queries.Length,
            GodotContactCountSha256 = HashInt32(godotContactCounts, totalPixels),
            LinearContactCountSha256 = HashInt32(linearCounts, totalPixels),
            SpatialAuthorityContextSha256 = new SpatialAuthorityContext(
                snapshot.GeometrySnapshotSha256,
                LinearScanSpatialQuery.AuthorityTokenValue,
                snapshot.IntersectionPolicyVersion).CanonicalSha256,
            BvhSpatialAuthorityContextSha256 = new BvhSpatialAuthorityContext(
                snapshot.GeometrySnapshotSha256,
                SpatialBvhQuery.AuthorityTokenValue,
                snapshot.IntersectionPolicyVersion,
                SpatialBvhQuery.BuildPolicyVersion,
                bvh.BuildSha256).CanonicalSha256,
            GodotContactHistogram = FormatHistogram(godotContactCounts, totalPixels),
            LinearContactHistogram = FormatHistogram(linearCounts, totalPixels),
            MismatchPixelCount = totalMismatchPixels,
            ContactCountMismatchPixelCount = countMismatchPixels,
            SurfaceSemanticMismatchPixelCount = semanticMismatchPixels,
            TotalAuthorityMismatchPixelCount = totalMismatchPixels,
            GodotQueryComparisonCount = godotQueryHits?.Length ?? 0,
            MismatchQueryCount = godotQueryHits == null ? -1 : mismatchQueries,
            FirstMismatch = firstMismatch,
            PrimitiveTestCount = query.PrimitiveTestCount,
            ElapsedMilliseconds = TicksToMilliseconds(linearTicks),
            LinearContactCounts = linearCounts,
            BvhContactCounts = bvhCounts,
            BvhContactCountSha256 = HashInt32(bvhCounts, totalPixels),
            BvhHitMismatchQueryCount = bvhHitMismatches,
            BvhPrimitiveIdentityMismatchQueryCount = bvhPrimitiveMismatches,
            BvhSurfaceSemanticMismatchQueryCount = bvhSurfaceMismatches,
            BvhSegmentTMismatchQueryCount = bvhSegmentTMismatches,
            BvhMismatchQueryCount = bvhMismatchQueries,
            BvhContactCountMismatchPixelCount = bvhCountMismatchPixels,
            BvhSurfaceSemanticMismatchPixelCount = bvhSemanticMismatchPixels,
            BvhTotalMismatchPixelCount = bvhTotalMismatchPixels,
            BvhPrimitiveTestCount = bvh.PrimitiveTestCount,
            BvhNodeTestCount = bvh.NodeTestCount,
            BvhElapsedMilliseconds = TicksToMilliseconds(bvhTicks),
            BvhBuildElapsedMilliseconds = bvh.BuildElapsedMilliseconds,
            BvhNodeCount = bvh.NodeCount,
            BvhLeafCount = bvh.LeafCount,
            BvhMaxDepth = bvh.MaxDepth,
            BvhBuildSha256 = bvh.BuildSha256,
            QueryDivergenceJson = SerializeDivergences(divergences, queries.Length)
        };
    }

    private static QueryDivergenceRecord BuildDivergenceRecord(
        int queryIndex,
        in RayBeamRenderer.FormalProbeQuery recorded,
        bool godotHit,
        in GodotReplayQueryObservation godot,
        bool linearHit,
        in SurfaceHit linear,
        bool bvhHit,
        in SurfaceHit bvh,
        IReadOnlyDictionary<string, IFrozenSpatialPrimitive> primitivesById)
    {
        List<string> candidates = new();
        List<string> evidence = new();
        float godotT = godotHit ? ProjectSegmentT(recorded.From, recorded.To, godot.Position) : float.NaN;
        string colliderPath = godot.ColliderPath ?? string.Empty;

        IFrozenSpatialPrimitive? godotPrimitive = FindPrimitiveForColliderPath(colliderPath, primitivesById);
        if (godotHit && (godotPrimitive is FrozenSphere || colliderPath.Contains("/ReferenceSphere", StringComparison.Ordinal)))
        {
            candidates.Add("reference_sphere_inclusion");
            evidence.Add("godot_hit_collider_is_reference_sphere");
        }

        FrozenOrientedBox? linearPrimitive = null;
        if (linearHit && primitivesById.TryGetValue(linear.CanonicalPrimitiveId, out IFrozenSpatialPrimitive? matched))
        {
            linearPrimitive = matched as FrozenOrientedBox;
            if (matched is FrozenSphere)
            {
                AddUnique(candidates, "reference_sphere_inclusion");
                evidence.Add("linear_hit_identifies_frozen_sphere_primitive");
            }
        }
        bool godotSharedBox = godotHit && godotPrimitive is FrozenOrientedBox;
        if (linearPrimitive != null || godotSharedBox)
        {
            candidates.Add("shared_box_intersection");
            evidence.Add(linearHit
                ? "linear_hit_identifies_frozen_box_primitive"
                : "godot_collider_path_matches_frozen_box_owner");
        }

        float selectedT = linearHit ? linear.SegmentT : godotT;
        if (float.IsFinite(selectedT) && (selectedT <= 1e-5f || selectedT >= 1f - 1e-5f))
        {
            candidates.Add("endpoint_or_tangent_contact");
            evidence.Add(selectedT <= 1e-5f ? "intersection_t_near_segment_start" : "intersection_t_near_segment_end");
        }

        if (linearPrimitive != null)
        {
            System.Numerics.Vector3 localFrom = System.Numerics.Vector3.Transform(ToNumerics(recorded.From), linearPrimitive.LocalFromWorld);
            bool startInside = IsInside(localFrom, linearPrimitive.HalfExtents);
            bool startOnSurface = startInside && IsOnSurface(localFrom, linearPrimitive.HalfExtents);
            if (startInside)
            {
                candidates.Add("start_on_surface_or_start_inside");
                evidence.Add(startOnSurface ? "linear_segment_starts_on_box_surface" : "linear_segment_starts_inside_box");
            }
            if (IsThinTransformedBox(linearPrimitive))
            {
                candidates.Add("thin_transformed_box_geometry");
                evidence.Add("frozen_box_has_thin_axis_and_non_identity_basis");
            }
            if (IsTangentOrEdgeContact(linearPrimitive, linear.Position))
            {
                AddUnique(candidates, "endpoint_or_tangent_contact");
                evidence.Add("linear_hit_lies_on_multiple_box_boundary_planes");
            }
        }

        if (candidates.Count == 0)
        {
            candidates.Add("unclassified");
            evidence.Add("current_observations_do_not_isolate_a_supported_candidate");
        }

        return new QueryDivergenceRecord(
            queryIndex,
            recorded.PixelIndex,
            recorded.ProbeStep,
            recorded.From,
            recorded.To,
            recorded.CollisionMask,
            recorded.CollideWithBodies,
            recorded.CollideWithAreas,
            recorded.HitFromInside,
            recorded.HitBackFaces,
            godotHit,
            godot.ColliderInstanceId,
            colliderPath,
            godot.ColliderType ?? string.Empty,
            godot.ShapeIndex,
            godot.Position,
            godot.Normal,
            godotT,
            linearHit,
            linearHit ? linear.CanonicalPrimitiveId : string.Empty,
            linearHit ? linear.SegmentT : float.NaN,
            linearHit ? linear.SurfaceClass.ToString() : "None",
            bvhHit,
            bvhHit ? bvh.CanonicalPrimitiveId : string.Empty,
            bvhHit ? bvh.SegmentT : float.NaN,
            bvhHit ? bvh.SurfaceClass.ToString() : "None",
            godotHit ? "godot_only" : "kernel_only",
            candidates.ToArray(),
            evidence.ToArray(),
            "inferred_until_verified");
    }

    private static IFrozenSpatialPrimitive? FindPrimitiveForColliderPath(
        string colliderPath,
        IReadOnlyDictionary<string, IFrozenSpatialPrimitive> primitivesById)
    {
        if (string.IsNullOrEmpty(colliderPath)) return null;
        foreach ((string primitiveId, IFrozenSpatialPrimitive primitive) in primitivesById)
            if (primitiveId.StartsWith(colliderPath + "/", StringComparison.Ordinal)) return primitive;
        return null;
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.Ordinal)) values.Add(value);
    }

    private static bool IsInside(System.Numerics.Vector3 point, System.Numerics.Vector3 halfExtents) =>
        point.X >= -halfExtents.X && point.X <= halfExtents.X &&
        point.Y >= -halfExtents.Y && point.Y <= halfExtents.Y &&
        point.Z >= -halfExtents.Z && point.Z <= halfExtents.Z;

    private static bool IsOnSurface(System.Numerics.Vector3 point, System.Numerics.Vector3 halfExtents)
    {
        float tolerance = Math.Max(1e-6f, Math.Max(halfExtents.X, Math.Max(halfExtents.Y, halfExtents.Z)) * 1e-5f);
        return MathF.Abs(MathF.Abs(point.X) - halfExtents.X) <= tolerance ||
            MathF.Abs(MathF.Abs(point.Y) - halfExtents.Y) <= tolerance ||
            MathF.Abs(MathF.Abs(point.Z) - halfExtents.Z) <= tolerance;
    }

    private static bool IsTangentOrEdgeContact(FrozenOrientedBox primitive, System.Numerics.Vector3 worldPosition)
    {
        System.Numerics.Vector3 local = System.Numerics.Vector3.Transform(worldPosition, primitive.LocalFromWorld);
        float tolerance = Math.Max(1e-6f, Math.Max(primitive.HalfExtents.X, Math.Max(primitive.HalfExtents.Y, primitive.HalfExtents.Z)) * 1e-5f);
        int boundaryPlanes = 0;
        if (MathF.Abs(MathF.Abs(local.X) - primitive.HalfExtents.X) <= tolerance) boundaryPlanes++;
        if (MathF.Abs(MathF.Abs(local.Y) - primitive.HalfExtents.Y) <= tolerance) boundaryPlanes++;
        if (MathF.Abs(MathF.Abs(local.Z) - primitive.HalfExtents.Z) <= tolerance) boundaryPlanes++;
        return boundaryPlanes >= 2;
    }

    private static bool IsThinTransformedBox(FrozenOrientedBox primitive)
    {
        float min = Math.Min(primitive.HalfExtents.X, Math.Min(primitive.HalfExtents.Y, primitive.HalfExtents.Z));
        float max = Math.Max(primitive.HalfExtents.X, Math.Max(primitive.HalfExtents.Y, primitive.HalfExtents.Z));
        bool thin = max > 0f && min / max <= 0.12f;
        Matrix4x4 transform = primitive.WorldFromLocal;
        bool transformed = MathF.Abs(transform.M12) > 1e-6f || MathF.Abs(transform.M13) > 1e-6f ||
            MathF.Abs(transform.M21) > 1e-6f || MathF.Abs(transform.M23) > 1e-6f ||
            MathF.Abs(transform.M31) > 1e-6f || MathF.Abs(transform.M32) > 1e-6f;
        return thin && transformed;
    }

    private static float ProjectSegmentT(Godot.Vector3 from, Godot.Vector3 to, Godot.Vector3 position)
    {
        Godot.Vector3 direction = to - from;
        float lengthSquared = direction.LengthSquared();
        return lengthSquared > 0f ? (position - from).Dot(direction) / lengthSquared : 0f;
    }

    private static string SerializeDivergences(IReadOnlyList<QueryDivergenceRecord> divergences, int queryCount)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "xprimeray.query-divergence-attribution.v1");
            writer.WriteNumber("query_count", queryCount);
            writer.WriteNumber("divergence_count", divergences.Count);
            writer.WriteString("classification_status", "candidate classes are inferred until verified");
            writer.WriteStartArray("records");
            foreach (QueryDivergenceRecord record in divergences) WriteDivergence(writer, record);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteDivergence(Utf8JsonWriter writer, QueryDivergenceRecord value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("query_index", value.QueryIndex);
        writer.WriteNumber("pixel", value.PixelIndex);
        writer.WriteNumber("probe_step", value.ProbeStep);
        WriteVector(writer, "from", value.From);
        WriteVector(writer, "to", value.To);
        writer.WriteNumber("collision_mask", value.CollisionMask);
        writer.WriteStartObject("query_flags");
        writer.WriteBoolean("collide_with_bodies", value.CollideWithBodies);
        writer.WriteBoolean("collide_with_areas", value.CollideWithAreas);
        writer.WriteBoolean("hit_from_inside", value.HitFromInside);
        writer.WriteBoolean("hit_back_faces", value.HitBackFaces);
        writer.WriteEndObject();
        writer.WriteStartObject("godot");
        writer.WriteBoolean("hit", value.GodotHit);
        writer.WriteNumber("collider_instance_id", value.GodotColliderInstanceId);
        writer.WriteString("collider_path", value.GodotColliderPath);
        writer.WriteString("collider_type", value.GodotColliderType);
        writer.WriteNumber("shape_index", value.GodotShapeIndex);
        WriteVector(writer, "position", value.GodotPosition);
        WriteVector(writer, "normal", value.GodotNormal);
        WriteFiniteNumber(writer, "segment_t_projected", value.GodotSegmentT);
        writer.WriteEndObject();
        WriteKernelHit(writer, "linear_scan", value.LinearHit, value.LinearPrimitiveId, value.LinearSegmentT, value.LinearSurfaceClass);
        WriteKernelHit(writer, "bvh", value.BvhHit, value.BvhPrimitiveId, value.BvhSegmentT, value.BvhSurfaceClass);
        writer.WriteString("direction", value.Direction);
        writer.WriteStartArray("candidate_discrepancy_classes");
        foreach (string candidate in value.CandidateClasses) writer.WriteStringValue(candidate);
        writer.WriteEndArray();
        writer.WriteStartArray("classification_evidence");
        foreach (string item in value.ClassificationEvidence) writer.WriteStringValue(item);
        writer.WriteEndArray();
        writer.WriteString("classification_status", value.ClassificationStatus);
        writer.WriteEndObject();
    }

    private static void WriteKernelHit(Utf8JsonWriter writer, string name, bool hit, string primitiveId, float segmentT, string surfaceClass)
    {
        writer.WriteStartObject(name);
        writer.WriteBoolean("hit", hit);
        writer.WriteString("primitive_id", primitiveId);
        WriteFiniteNumber(writer, "segment_t", segmentT);
        writer.WriteString("surface_class", surfaceClass);
        writer.WriteEndObject();
    }

    private static void WriteVector(Utf8JsonWriter writer, string name, Godot.Vector3 value)
    {
        writer.WriteStartArray(name);
        writer.WriteNumberValue(value.X); writer.WriteNumberValue(value.Y); writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }

    private static void WriteVector(Utf8JsonWriter writer, string name, System.Numerics.Vector3 value)
    {
        writer.WriteStartArray(name);
        writer.WriteNumberValue(value.X); writer.WriteNumberValue(value.Y); writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }

    private static void WriteFiniteNumber(Utf8JsonWriter writer, string name, float value)
    {
        if (float.IsFinite(value)) writer.WriteNumber(name, value);
        else writer.WriteNull(name);
    }

    private readonly record struct QueryDivergenceRecord(
        int QueryIndex,
        int PixelIndex,
        int ProbeStep,
        Godot.Vector3 From,
        Godot.Vector3 To,
        uint CollisionMask,
        bool CollideWithBodies,
        bool CollideWithAreas,
        bool HitFromInside,
        bool HitBackFaces,
        bool GodotHit,
        ulong GodotColliderInstanceId,
        string GodotColliderPath,
        string GodotColliderType,
        int GodotShapeIndex,
        Godot.Vector3 GodotPosition,
        Godot.Vector3 GodotNormal,
        float GodotSegmentT,
        bool LinearHit,
        string LinearPrimitiveId,
        float LinearSegmentT,
        string LinearSurfaceClass,
        bool BvhHit,
        string BvhPrimitiveId,
        float BvhSegmentT,
        string BvhSurfaceClass,
        string Direction,
        string[] CandidateClasses,
        string[] ClassificationEvidence,
        string ClassificationStatus);

    private static string FormatHistogram(int[] values, int count)
    {
        int zero = 0, one = 0, two = 0, threePlus = 0;
        for (int i = 0; i < count; i++)
        {
            switch (values[i])
            {
                case 0: zero++; break;
                case 1: one++; break;
                case 2: two++; break;
                default: threePlus++; break;
            }
        }
        return $"0={zero},1={one},2={two},3+={threePlus}";
    }

    private static Vector3 ToNumerics(Godot.Vector3 value) => new(value.X, value.Y, value.Z);

    private static double TicksToMilliseconds(long ticks) => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    private static string HashInt32(int[] values, int count)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        for (int i = 0; i < count; i++) writer.Write(values[i]);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
}
