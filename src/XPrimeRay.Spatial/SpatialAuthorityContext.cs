using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace XPrimeRay.Spatial;

/// <summary>
/// Versioned diagnostic sub-context for the future spatial authority handoff.
/// It is deliberately separate from the existing ProbeContextKey in v0.
/// </summary>
public sealed class SpatialAuthorityContext
{
    public const string SchemaVersion = "SpatialAuthorityContext-v1";

    public string GeometrySnapshotSha256 { get; }
    public string SpatialKernelVersion { get; }
    public string IntersectionPolicyVersion { get; }
    public string CanonicalSha256 { get; }

    public SpatialAuthorityContext(string geometrySnapshotSha256, string spatialKernelVersion, string intersectionPolicyVersion)
    {
        if (string.IsNullOrWhiteSpace(geometrySnapshotSha256)) throw new ArgumentException("Geometry hash is required.", nameof(geometrySnapshotSha256));
        if (string.IsNullOrWhiteSpace(spatialKernelVersion)) throw new ArgumentException("Kernel version is required.", nameof(spatialKernelVersion));
        if (string.IsNullOrWhiteSpace(intersectionPolicyVersion)) throw new ArgumentException("Intersection policy version is required.", nameof(intersectionPolicyVersion));
        GeometrySnapshotSha256 = geometrySnapshotSha256;
        SpatialKernelVersion = spatialKernelVersion;
        IntersectionPolicyVersion = intersectionPolicyVersion;
        CanonicalSha256 = Convert.ToHexString(SHA256.HashData(Serialize(this))).ToLowerInvariant();
    }

    public static byte[] Serialize(SpatialAuthorityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        using MemoryStream stream = new();
        WriteString(stream, SchemaVersion);
        WriteString(stream, context.GeometrySnapshotSha256);
        WriteString(stream, context.SpatialKernelVersion);
        WriteString(stream, context.IntersectionPolicyVersion);
        return stream.ToArray();
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        stream.Write(length);
        stream.Write(bytes);
    }
}

/// <summary>
/// Versioned authority identity for the diagnostic BVH implementation.
/// BVH topology is intentionally kept out of the generic LinearScan context.
/// </summary>
public sealed class BvhSpatialAuthorityContext
{
    public const string SchemaVersion = "BvhSpatialAuthorityContext-v1";

    public string GeometrySnapshotSha256 { get; }
    public string AuthorityToken { get; }
    public string IntersectionPolicyVersion { get; }
    public string BuildPolicyVersion { get; }
    public string BvhBuildSha256 { get; }
    public string CanonicalSha256 { get; }

    public BvhSpatialAuthorityContext(
        string geometrySnapshotSha256,
        string authorityToken,
        string intersectionPolicyVersion,
        string buildPolicyVersion,
        string bvhBuildSha256)
    {
        if (string.IsNullOrWhiteSpace(geometrySnapshotSha256)) throw new ArgumentException("Geometry hash is required.", nameof(geometrySnapshotSha256));
        if (string.IsNullOrWhiteSpace(authorityToken)) throw new ArgumentException("Authority token is required.", nameof(authorityToken));
        if (string.IsNullOrWhiteSpace(intersectionPolicyVersion)) throw new ArgumentException("Intersection policy version is required.", nameof(intersectionPolicyVersion));
        if (string.IsNullOrWhiteSpace(buildPolicyVersion)) throw new ArgumentException("Build policy version is required.", nameof(buildPolicyVersion));
        if (string.IsNullOrWhiteSpace(bvhBuildSha256)) throw new ArgumentException("BVH build hash is required.", nameof(bvhBuildSha256));
        GeometrySnapshotSha256 = geometrySnapshotSha256;
        AuthorityToken = authorityToken;
        IntersectionPolicyVersion = intersectionPolicyVersion;
        BuildPolicyVersion = buildPolicyVersion;
        BvhBuildSha256 = bvhBuildSha256;
        CanonicalSha256 = Convert.ToHexString(SHA256.HashData(Serialize(this))).ToLowerInvariant();
    }

    public static byte[] Serialize(BvhSpatialAuthorityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        using MemoryStream stream = new();
        WriteString(stream, SchemaVersion);
        WriteString(stream, context.GeometrySnapshotSha256);
        WriteString(stream, context.AuthorityToken);
        WriteString(stream, context.IntersectionPolicyVersion);
        WriteString(stream, context.BuildPolicyVersion);
        WriteString(stream, context.BvhBuildSha256);
        return stream.ToArray();
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        stream.Write(length);
        stream.Write(bytes);
    }
}

public readonly record struct BvhParityStatus(
    int TotalMismatchPixelCount,
    int MismatchQueryCount,
    int HitMismatchQueryCount,
    int PrimitiveIdentityMismatchQueryCount,
    int SurfaceSemanticMismatchQueryCount,
    int SegmentTMismatchQueryCount)
{
    public bool IsZero => TotalMismatchPixelCount == 0 &&
        MismatchQueryCount == 0 &&
        HitMismatchQueryCount == 0 &&
        PrimitiveIdentityMismatchQueryCount == 0 &&
        SurfaceSemanticMismatchQueryCount == 0 &&
        SegmentTMismatchQueryCount == 0;
}

public readonly record struct SpatialContactComparison(
    int ContactCountMismatchPixels,
    int SurfaceSemanticMismatchPixels,
    int TotalAuthorityMismatchPixels);

public static class SpatialContactComparer
{
    public static SpatialContactComparison Compare(
        int[] authorityCounts,
        int[] candidateCounts,
        byte[] authorityGeometry,
        byte[] authorityBackground,
        byte[] candidateGeometry,
        byte[] candidateBackground)
    {
        ArgumentNullException.ThrowIfNull(authorityCounts);
        ArgumentNullException.ThrowIfNull(candidateCounts);
        ArgumentNullException.ThrowIfNull(authorityGeometry);
        ArgumentNullException.ThrowIfNull(authorityBackground);
        ArgumentNullException.ThrowIfNull(candidateGeometry);
        ArgumentNullException.ThrowIfNull(candidateBackground);
        int count = authorityCounts.Length;
        if (candidateCounts.Length < count || authorityGeometry.Length < count || authorityBackground.Length < count ||
            candidateGeometry.Length < count || candidateBackground.Length < count)
            throw new ArgumentException("Contact comparison channels must have equal or greater capacity.");
        int countMismatch = 0;
        int semanticMismatch = 0;
        int totalMismatch = 0;
        for (int i = 0; i < count; i++)
        {
            bool countDiff = authorityCounts[i] != candidateCounts[i];
            bool semanticDiff = authorityGeometry[i] != candidateGeometry[i] || authorityBackground[i] != candidateBackground[i];
            if (countDiff) countMismatch++;
            if (semanticDiff) semanticMismatch++;
            if (countDiff || semanticDiff) totalMismatch++;
        }
        return new SpatialContactComparison(countMismatch, semanticMismatch, totalMismatch);
    }
}

public static class SpatialAuthorityPromotionGate
{
    public static bool CanPromote(
        FrozenGeometrySnapshot snapshot,
        SpatialAuthorityContext authorityContext,
        BvhSpatialAuthorityContext bvhAuthorityContext,
        bool dualValidationPresent,
        string validationContextSha256,
        string validationBvhContextSha256,
        int contactCountMismatchPixels,
        int surfaceSemanticMismatchPixels,
        bool supportedGeometry,
        string diagnosticFailure,
        BvhParityStatus bvhParity,
        out string reason)
    {
        if (snapshot == null) { reason = "snapshot_missing"; return false; }
        if (authorityContext == null) { reason = "spatial_context_missing"; return false; }
        if (bvhAuthorityContext == null) { reason = "bvh_context_missing"; return false; }
        if (!dualValidationPresent) { reason = "dual_validation_missing"; return false; }
        if (!supportedGeometry) { reason = "unsupported_geometry"; return false; }
        if (!string.IsNullOrEmpty(diagnosticFailure)) { reason = "spatial_diagnostic_failure"; return false; }
        if (!string.Equals(authorityContext.GeometrySnapshotSha256, snapshot.GeometrySnapshotSha256, StringComparison.Ordinal))
        {
            reason = "frozen_geometry_drifted";
            return false;
        }
        if (!string.Equals(authorityContext.CanonicalSha256, validationContextSha256, StringComparison.Ordinal))
        {
            reason = "spatial_context_drifted";
            return false;
        }
        if (!string.Equals(bvhAuthorityContext.GeometrySnapshotSha256, snapshot.GeometrySnapshotSha256, StringComparison.Ordinal) ||
            !string.Equals(bvhAuthorityContext.AuthorityToken, SpatialBvhQuery.AuthorityTokenValue, StringComparison.Ordinal) ||
            !string.Equals(bvhAuthorityContext.IntersectionPolicyVersion, snapshot.IntersectionPolicyVersion, StringComparison.Ordinal) ||
            !string.Equals(bvhAuthorityContext.BuildPolicyVersion, SpatialBvhQuery.BuildPolicyVersion, StringComparison.Ordinal) ||
            !string.Equals(bvhAuthorityContext.CanonicalSha256, validationBvhContextSha256, StringComparison.Ordinal))
        {
            reason = "bvh_context_drifted";
            return false;
        }
        if (!bvhParity.IsZero) { reason = "bvh_parity_failure"; return false; }
        if (contactCountMismatchPixels != 0) { reason = "contact_count_mismatch"; return false; }
        if (surfaceSemanticMismatchPixels != 0) { reason = "surface_semantic_mismatch"; return false; }
        reason = "qualified";
        return true;
    }
}
