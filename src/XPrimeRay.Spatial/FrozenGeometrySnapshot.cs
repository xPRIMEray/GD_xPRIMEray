using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using XPrimeRay.Core.Geometry;

namespace XPrimeRay.Spatial;

[Flags]
public enum FrozenPrimitiveFlags : uint
{
    None = 0,
    Excluded = 1,
    CollideWithBodies = 2,
    CollideWithAreas = 4
}

public enum FrozenPrimitiveKind : byte
{
    OrientedBox = 1,
    Sphere = 2
}

public interface IFrozenSpatialPrimitive
{
    string CanonicalPrimitiveId { get; }
    FrozenPrimitiveKind PrimitiveKind { get; }
    SpatialSurfaceClass SurfaceClass { get; }
    Aabb3 WorldBounds { get; }
    uint CollisionLayer { get; }
    FrozenPrimitiveFlags Flags { get; }
    bool IsExcluded { get; }
}

public sealed class FrozenOrientedBox : IFrozenSpatialPrimitive
{
    public string CanonicalPrimitiveId { get; }
    public SpatialSurfaceClass SurfaceClass { get; }
    public Matrix4x4 WorldFromLocal { get; }
    public Matrix4x4 LocalFromWorld { get; }
    public Vector3 HalfExtents { get; }
    public Aabb3 WorldBounds { get; }
    public uint CollisionLayer { get; }
    public FrozenPrimitiveFlags Flags { get; }
    public FrozenPrimitiveKind PrimitiveKind => FrozenPrimitiveKind.OrientedBox;

    public FrozenOrientedBox(
        string canonicalPrimitiveId,
        SpatialSurfaceClass surfaceClass,
        Matrix4x4 worldFromLocal,
        Matrix4x4 localFromWorld,
        Vector3 halfExtents,
        Aabb3 worldBounds,
        uint collisionLayer,
        FrozenPrimitiveFlags flags)
    {
        if (string.IsNullOrWhiteSpace(canonicalPrimitiveId))
            throw new ArgumentException("Canonical primitive identity is required.", nameof(canonicalPrimitiveId));
        if (!Matrix4x4.Invert(worldFromLocal, out Matrix4x4 verifiedInverse))
            throw new ArgumentException("World transform must be invertible.", nameof(worldFromLocal));
        if (halfExtents.X < 0 || halfExtents.Y < 0 || halfExtents.Z < 0)
            throw new ArgumentOutOfRangeException(nameof(halfExtents));

        CanonicalPrimitiveId = canonicalPrimitiveId;
        SurfaceClass = surfaceClass;
        WorldFromLocal = worldFromLocal;
        LocalFromWorld = localFromWorld;
        HalfExtents = halfExtents;
        WorldBounds = worldBounds;
        CollisionLayer = collisionLayer;
        Flags = flags;

        // The adapter supplies the inverse, but reject a divergent inverse so
        // frozen measurement data cannot silently contain two transforms.
        if (!ApproximatelyEqual(verifiedInverse, localFromWorld))
            throw new ArgumentException("LocalFromWorld is not the inverse of WorldFromLocal.", nameof(localFromWorld));
    }

    public bool IsExcluded => (Flags & FrozenPrimitiveFlags.Excluded) != 0;

    private static bool ApproximatelyEqual(Matrix4x4 a, Matrix4x4 b)
    {
        ReadOnlySpan<float> av = stackalloc float[]
        {
            a.M11, a.M12, a.M13, a.M14, a.M21, a.M22, a.M23, a.M24,
            a.M31, a.M32, a.M33, a.M34, a.M41, a.M42, a.M43, a.M44
        };
        ReadOnlySpan<float> bv = stackalloc float[]
        {
            b.M11, b.M12, b.M13, b.M14, b.M21, b.M22, b.M23, b.M24,
            b.M31, b.M32, b.M33, b.M34, b.M41, b.M42, b.M43, b.M44
        };
        for (int i = 0; i < av.Length; i++)
            if (MathF.Abs(av[i] - bv[i]) > 1e-5f) return false;
        return true;
    }
}

public sealed class FrozenSphere : IFrozenSpatialPrimitive
{
    public string CanonicalPrimitiveId { get; }
    public FrozenPrimitiveKind PrimitiveKind => FrozenPrimitiveKind.Sphere;
    public SpatialSurfaceClass SurfaceClass { get; }
    public Vector3 Center { get; }
    public float Radius { get; }
    public Aabb3 WorldBounds { get; }
    public uint CollisionLayer { get; }
    public FrozenPrimitiveFlags Flags { get; }
    public bool IsExcluded => (Flags & FrozenPrimitiveFlags.Excluded) != 0;

    public FrozenSphere(
        string canonicalPrimitiveId,
        SpatialSurfaceClass surfaceClass,
        Vector3 center,
        float radius,
        uint collisionLayer,
        FrozenPrimitiveFlags flags)
    {
        if (string.IsNullOrWhiteSpace(canonicalPrimitiveId))
            throw new ArgumentException("Canonical primitive identity is required.", nameof(canonicalPrimitiveId));
        if (!IsFinite(center)) throw new ArgumentException("Sphere center must be finite.", nameof(center));
        if (!float.IsFinite(radius) || radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius));
        CanonicalPrimitiveId = canonicalPrimitiveId;
        SurfaceClass = surfaceClass;
        Center = center;
        Radius = radius;
        Vector3 extent = new(radius);
        WorldBounds = new Aabb3(center - extent, center + extent);
        CollisionLayer = collisionLayer;
        Flags = flags;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

public sealed class FrozenGeometrySnapshot
{
	// v2 identifies the canonical byte layout. It does not preserve the values
	// produced by the historically transposed Godot-basis adapter. Correcting
	// that adapter intentionally changes hashes for rotated Godot boxes while
	// leaving the v2 layout and manually constructed box snapshots unchanged.
    public const string SchemaVersion = "FrozenGeometrySnapshot-v2-le";
    public const string MixedSchemaVersion = "FrozenGeometrySnapshot-v3-mixed-le";
    public IReadOnlyList<FrozenOrientedBox> Primitives { get; }
    public IReadOnlyList<FrozenSphere> Spheres { get; }
    public IReadOnlyList<IFrozenSpatialPrimitive> AllPrimitives { get; }
    public string EffectiveSchemaVersion => Spheres.Count == 0 ? SchemaVersion : MixedSchemaVersion;
    public string IntersectionPolicyVersion => Spheres.Count == 0
        ? LinearScanSpatialQuery.IntersectionPolicyVersion
        : LinearScanSpatialQuery.MixedIntersectionPolicyVersion;
    public string GeometrySnapshotSha256 { get; }
    public int PrimitiveCount => AllPrimitives.Count;

    public FrozenGeometrySnapshot(IEnumerable<FrozenOrientedBox> primitives) : this(primitives, Array.Empty<FrozenSphere>())
    {
    }

    public FrozenGeometrySnapshot(IEnumerable<FrozenOrientedBox> boxes, IEnumerable<FrozenSphere> spheres)
    {
        if (boxes == null) throw new ArgumentNullException(nameof(boxes));
        if (spheres == null) throw new ArgumentNullException(nameof(spheres));
        FrozenOrientedBox[] orderedBoxes = boxes
            .OrderBy(p => p.CanonicalPrimitiveId, StringComparer.Ordinal)
            .ToArray();
        FrozenSphere[] orderedSpheres = spheres
            .OrderBy(p => p.CanonicalPrimitiveId, StringComparer.Ordinal)
            .ToArray();
        IFrozenSpatialPrimitive[] all = orderedBoxes.Cast<IFrozenSpatialPrimitive>()
            .Concat(orderedSpheres)
            .OrderBy(p => p.CanonicalPrimitiveId, StringComparer.Ordinal)
            .ToArray();
        if (all.Select(p => p.CanonicalPrimitiveId).Distinct(StringComparer.Ordinal).Count() != all.Length)
            throw new ArgumentException("Canonical primitive identities must be unique across all primitive kinds.");
        Primitives = Array.AsReadOnly(orderedBoxes);
        Spheres = Array.AsReadOnly(orderedSpheres);
        AllPrimitives = Array.AsReadOnly(all);
        GeometrySnapshotSha256 = FrozenGeometrySnapshotCanonicalSerializer.ComputeSha256(this);
    }
}

public static class FrozenGeometrySnapshotCanonicalSerializer
{
    public static byte[] Serialize(FrozenGeometrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using MemoryStream stream = new();
        if (snapshot.Spheres.Count == 0)
        {
            // This branch is intentionally byte-for-byte identical to v2.
            WriteString(stream, FrozenGeometrySnapshot.SchemaVersion);
            WriteInt32(stream, snapshot.PrimitiveCount);
            foreach (FrozenOrientedBox primitive in snapshot.Primitives)
            {
                WriteString(stream, primitive.CanonicalPrimitiveId);
                stream.WriteByte((byte)primitive.SurfaceClass);
                WriteUInt32(stream, primitive.CollisionLayer);
                WriteUInt32(stream, (uint)primitive.Flags);
                WriteVector(stream, primitive.HalfExtents);
                WriteAabb(stream, primitive.WorldBounds);
                WriteMatrix(stream, primitive.WorldFromLocal);
                WriteMatrix(stream, primitive.LocalFromWorld);
            }
            return stream.ToArray();
        }

        WriteString(stream, FrozenGeometrySnapshot.MixedSchemaVersion);
        WriteInt32(stream, snapshot.PrimitiveCount);
        foreach (IFrozenSpatialPrimitive primitive in snapshot.AllPrimitives)
        {
            stream.WriteByte((byte)primitive.PrimitiveKind);
            WriteString(stream, primitive.CanonicalPrimitiveId);
            stream.WriteByte((byte)primitive.SurfaceClass);
            WriteUInt32(stream, primitive.CollisionLayer);
            WriteUInt32(stream, (uint)primitive.Flags);
            WriteAabb(stream, primitive.WorldBounds);
            switch (primitive)
            {
                case FrozenOrientedBox box:
                    WriteVector(stream, box.HalfExtents);
                    WriteMatrix(stream, box.WorldFromLocal);
                    WriteMatrix(stream, box.LocalFromWorld);
                    break;
                case FrozenSphere sphere:
                    WriteVector(stream, sphere.Center);
                    WriteSingle(stream, sphere.Radius);
                    break;
                default:
                    throw new InvalidDataException($"Unsupported frozen primitive kind: {primitive.GetType().FullName}");
            }
        }
        return stream.ToArray();
    }

    public static string ComputeSha256(FrozenGeometrySnapshot snapshot)
    {
        return Convert.ToHexString(SHA256.HashData(Serialize(snapshot))).ToLowerInvariant();
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteVector(Stream stream, Vector3 value)
    {
        WriteSingle(stream, value.X); WriteSingle(stream, value.Y); WriteSingle(stream, value.Z);
    }

    private static void WriteAabb(Stream stream, Aabb3 value)
    {
        WriteVector(stream, value.Min);
        WriteVector(stream, value.Max);
    }

    private static void WriteMatrix(Stream stream, Matrix4x4 value)
    {
        WriteSingle(stream, value.M11); WriteSingle(stream, value.M12); WriteSingle(stream, value.M13); WriteSingle(stream, value.M14);
        WriteSingle(stream, value.M21); WriteSingle(stream, value.M22); WriteSingle(stream, value.M23); WriteSingle(stream, value.M24);
        WriteSingle(stream, value.M31); WriteSingle(stream, value.M32); WriteSingle(stream, value.M33); WriteSingle(stream, value.M34);
        WriteSingle(stream, value.M41); WriteSingle(stream, value.M42); WriteSingle(stream, value.M43); WriteSingle(stream, value.M44);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteSingle(Stream stream, float value) => WriteInt32(stream, BitConverter.SingleToInt32Bits(value));
}
