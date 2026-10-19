using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using XPrimeRay.Core.Geometry;

namespace XPrimeRay.Spatial;

/// <summary>
/// Deterministic acceleration backend for the validated LinearScan query.
/// Build policy: canonical primitive order, median split on the longest
/// centroid axis, ordinal primitive-ID tie-breaks, and two-primitive leaves.
/// </summary>
public sealed class SpatialBvhQuery : IPass1SpatialQuery
{
    public const string AuthorityTokenValue = "XPrimeRaySpatialKernel/BVH-v0";
    public const string BuildPolicyVersion = "median-longest-centroid-axis-leaf2-v0";

    private readonly FrozenGeometrySnapshot _snapshot;
    private readonly Node[] _nodes;
    private readonly int[] _primitiveIndices;

    public string AuthorityToken => AuthorityTokenValue;
    public long QueryCount { get; private set; }
    public long NodeTestCount { get; private set; }
    public long PrimitiveTestCount { get; private set; }
    public int NodeCount => _nodes.Length;
    public int LeafCount { get; }
    public int MaxDepth { get; }
    public string BuildSha256 { get; }
    public double BuildElapsedMilliseconds { get; }

    public SpatialBvhQuery(FrozenGeometrySnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        for (int i = 0; i < snapshot.AllPrimitives.Count; i++)
        {
            Aabb3 bounds = snapshot.AllPrimitives[i].WorldBounds;
            if (!IsFinite(bounds.Min) || !IsFinite(bounds.Max))
                throw new ArgumentException($"BVH WorldBounds must be finite for primitive index {i}.", nameof(snapshot));
            if (bounds.Min.X > bounds.Max.X || bounds.Min.Y > bounds.Max.Y || bounds.Min.Z > bounds.Max.Z)
                throw new ArgumentException($"BVH WorldBounds must be ordered for primitive index {i}.", nameof(snapshot));
        }
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        List<int> indices = Enumerable.Range(0, snapshot.AllPrimitives.Count)
            .Where(index => !snapshot.AllPrimitives[index].IsExcluded)
            .ToList();
        List<Node> nodes = new();
        List<int> leafIndices = new();
        int maxDepth = 0;
        int leafCount = 0;
        if (indices.Count > 0)
            BuildNode(indices, 0, nodes, leafIndices, ref leafCount, ref maxDepth);
        _nodes = nodes.ToArray();
        _primitiveIndices = leafIndices.ToArray();
        LeafCount = leafCount;
        MaxDepth = maxDepth;
        BuildSha256 = ComputeBuildSha256(_nodes, _primitiveIndices, snapshot);
        stopwatch.Stop();
        BuildElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
    }

    public bool IntersectsSegment(Vector3 from, Vector3 to, uint collisionMask, out SurfaceHit hit)
    {
        QueryCount++;
        if (_nodes.Length == 0)
        {
            hit = default;
            return false;
        }

        Span<int> stack = stackalloc int[64];
        int[]? overflow = null;
        int stackCount = 1;
        stack[0] = 0;
        bool found = false;
        SurfaceHit nearest = default;
        while (stackCount > 0)
        {
            int nodeIndex = Pop(stack, ref overflow, ref stackCount);
            Node node = _nodes[nodeIndex];
            NodeTestCount++;
            if (!SegmentIntersectsAabb(node.Bounds, from, to))
                continue;

            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Count; i++)
                {
                    IFrozenSpatialPrimitive primitive = _snapshot.AllPrimitives[_primitiveIndices[node.Start + i]];
                    if ((primitive.CollisionLayer & collisionMask) == 0)
                        continue;
                    PrimitiveTestCount++;
                    if (!FrozenPrimitiveIntersection.TryIntersectSegment(primitive, from, to, out SurfaceHit candidate))
                        continue;
                    if (!found || candidate.SegmentT < nearest.SegmentT ||
                        (candidate.SegmentT == nearest.SegmentT &&
                         string.CompareOrdinal(candidate.CanonicalPrimitiveId, nearest.CanonicalPrimitiveId) < 0))
                    {
                        found = true;
                        nearest = candidate;
                    }
                }
            }
            else
            {
                // Child indices are pushed in reverse canonical build order;
                // traversal is deterministic but result selection is order-free.
                if (node.Right >= 0) Push(stack, ref overflow, ref stackCount, node.Right);
                if (node.Left >= 0) Push(stack, ref overflow, ref stackCount, node.Left);
            }
        }

        hit = nearest;
        return found;
    }

    private int BuildNode(
        List<int> indices,
        int depth,
        List<Node> nodes,
        List<int> leafIndices,
        ref int leafCount,
        ref int maxDepth)
    {
        maxDepth = Math.Max(maxDepth, depth);
        int nodeIndex = nodes.Count;
        nodes.Add(default);
        Aabb3 bounds = Bounds(indices);
        if (indices.Count <= 2)
        {
            int start = leafIndices.Count;
            leafIndices.AddRange(indices);
            nodes[nodeIndex] = Node.Leaf(bounds, start, indices.Count);
            leafCount++;
            return nodeIndex;
        }

        Vector3 extent = bounds.Extents;
        int axis = LongestAxis(extent);
        indices.Sort((a, b) =>
        {
            float av = Axis(_snapshot.AllPrimitives[a].WorldBounds.Center, axis);
            float bv = Axis(_snapshot.AllPrimitives[b].WorldBounds.Center, axis);
            int result = av.CompareTo(bv);
            return result != 0
                ? result
                : string.CompareOrdinal(_snapshot.AllPrimitives[a].CanonicalPrimitiveId, _snapshot.AllPrimitives[b].CanonicalPrimitiveId);
        });
        int midpoint = indices.Count / 2;
        int left = BuildNode(indices.GetRange(0, midpoint), depth + 1, nodes, leafIndices, ref leafCount, ref maxDepth);
        int right = BuildNode(indices.GetRange(midpoint, indices.Count - midpoint), depth + 1, nodes, leafIndices, ref leafCount, ref maxDepth);
        nodes[nodeIndex] = Node.Branch(bounds, left, right);
        return nodeIndex;
    }

    private Aabb3 Bounds(List<int> indices)
    {
        Aabb3 bounds = _snapshot.AllPrimitives[indices[0]].WorldBounds;
        for (int i = 1; i < indices.Count; i++)
            bounds = Aabb3.Union(bounds, _snapshot.AllPrimitives[indices[i]].WorldBounds);
        return bounds;
    }

    private static int LongestAxis(Vector3 extent) =>
        extent.X >= extent.Y && extent.X >= extent.Z ? 0 : extent.Y >= extent.Z ? 1 : 2;

    private static float Axis(Vector3 value, int axis) => axis switch { 1 => value.Y, 2 => value.Z, _ => value.X };

    /// <summary>
    /// Conservative broad-phase test for a finite inclusive segment [from,to].
    /// This only decides whether a BVH node may contain a candidate; the OBB
    /// truth source remains <see cref="OrientedBoxIntersection.TryIntersectSegment"/>.
    /// It follows the finite-segment policy without adding an epsilon or changing
    /// endpoint, parallel-slab, or start-inside semantics.
    /// </summary>
    private static bool SegmentIntersectsAabb(Aabb3 bounds, Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        double enter = 0.0;
        double exit = 1.0;
        for (int axis = 0; axis < 3; axis++)
        {
            double origin = Axis(from, axis);
            double delta = Axis(direction, axis);
            double min = Axis(bounds.Min, axis);
            double max = Axis(bounds.Max, axis);
            if (delta == 0.0)
            {
                if (origin < min || origin > max) return false;
                continue;
            }
            double a = (min - origin) / delta;
            double b = (max - origin) / delta;
            if (a > b) (a, b) = (b, a);
            enter = Math.Max(enter, a);
            exit = Math.Min(exit, b);
            if (enter > exit) return false;
        }
        return exit >= 0.0 && enter <= 1.0;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static int Pop(Span<int> stack, ref int[]? overflow, ref int count)
    {
        if (count <= stack.Length) return stack[--count];
        int index = overflow![--count - stack.Length];
        return index;
    }

    private static void Push(Span<int> stack, ref int[]? overflow, ref int count, int value)
    {
        if (count < stack.Length)
        {
            stack[count++] = value;
            return;
        }
        if (overflow == null) overflow = new int[128];
        if (count - stack.Length >= overflow.Length) Array.Resize(ref overflow, overflow.Length * 2);
        overflow[count++ - stack.Length] = value;
    }

    private static string ComputeBuildSha256(Node[] nodes, int[] leafIndices, FrozenGeometrySnapshot snapshot)
    {
        using MemoryStream stream = new();
        WriteString(stream, BuildPolicyVersion);
        WriteInt32(stream, nodes.Length);
        foreach (Node node in nodes)
        {
            WriteVector(stream, node.Bounds.Min); WriteVector(stream, node.Bounds.Max);
            WriteInt32(stream, node.Left); WriteInt32(stream, node.Right);
            WriteInt32(stream, node.Start); WriteInt32(stream, node.Count);
        }
        WriteInt32(stream, leafIndices.Length);
        foreach (int index in leafIndices) WriteString(stream, snapshot.AllPrimitives[index].CanonicalPrimitiveId);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32(stream, bytes.Length); stream.Write(bytes);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value); stream.Write(bytes);
    }

    private static void WriteVector(Stream stream, Vector3 value)
    {
        WriteInt32(stream, BitConverter.SingleToInt32Bits(value.X));
        WriteInt32(stream, BitConverter.SingleToInt32Bits(value.Y));
        WriteInt32(stream, BitConverter.SingleToInt32Bits(value.Z));
    }

    private readonly struct Node
    {
        public Aabb3 Bounds { get; }
        public int Left { get; }
        public int Right { get; }
        public int Start { get; }
        public int Count { get; }
        public bool IsLeaf => Left < 0;

        private Node(Aabb3 bounds, int left, int right, int start, int count)
        {
            Bounds = bounds; Left = left; Right = right; Start = start; Count = count;
        }

        public static Node Branch(Aabb3 bounds, int left, int right) => new(bounds, left, right, -1, 0);
        public static Node Leaf(Aabb3 bounds, int start, int count) => new(bounds, -1, -1, start, count);
    }
}
