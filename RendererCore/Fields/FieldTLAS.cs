using System;
using System.Collections.Generic;
using System.Numerics;
using RendererCore.SceneSnapshot;

namespace RendererCore.Fields;

public readonly struct BVHNode
{
    public readonly Aabb3 Bounds;
    public readonly int Left;
    public readonly int Right;

    public BVHNode(Aabb3 bounds, int left, int right)
    {
        Bounds = bounds;
        Left = left;
        Right = right;
    }
}

public sealed class FieldTLAS
{
    public readonly BVHNode[] Nodes;
    public readonly int[] LeafFieldIds;
    public readonly int RootIndex;

    public FieldTLAS(BVHNode[] nodes, int[] leafFieldIds, int rootIndex)
    {
        Nodes = nodes ?? Array.Empty<BVHNode>();
        LeafFieldIds = leafFieldIds ?? Array.Empty<int>();
        RootIndex = rootIndex;
    }

    public int QueryPoint(Vector3 pWorld, Span<int> results)
    {
        if (Nodes.Length == 0 || RootIndex < 0)
        {
            return 0;
        }

        Span<int> stack = stackalloc int[256];
        var sp = 0;
        stack[sp++] = RootIndex;

        var count = 0;
        while (sp > 0)
        {
            var nodeIndex = stack[--sp];
            ref readonly var node = ref Nodes[nodeIndex];
            if (!node.Bounds.Contains(pWorld))
            {
                continue;
            }

            if (node.Left < 0)
            {
                var leafStart = -node.Left - 1;
                var leafCount = node.Right;
                for (var i = 0; i < leafCount; i++)
                {
                    if (count >= results.Length)
                    {
                        return count;
                    }

                    results[count++] = LeafFieldIds[leafStart + i];
                }
            }
            else
            {
                stack[sp++] = node.Left;
                stack[sp++] = node.Right;
            }
        }

        return count;
    }

    public int QueryAabb(in Aabb3 region, Span<int> results)
    {
        if (Nodes.Length == 0 || RootIndex < 0)
        {
            return 0;
        }

        Span<int> stack = stackalloc int[128];
        var sp = 0;
        stack[sp++] = RootIndex;

        var count = 0;
        while (sp > 0)
        {
            var nodeIndex = stack[--sp];
            ref readonly var node = ref Nodes[nodeIndex];
            if (!node.Bounds.Overlaps(region))
            {
                continue;
            }

            if (node.Left < 0)
            {
                var leafStart = -node.Left - 1;
                var leafCount = node.Right;
                for (var i = 0; i < leafCount; i++)
                {
                    if (count >= results.Length)
                    {
                        return count;
                    }

                    results[count++] = LeafFieldIds[leafStart + i];
                }
            }
            else
            {
                stack[sp++] = node.Left;
                stack[sp++] = node.Right;
            }
        }

        return count;
    }

    public static FieldTLAS Build(in FieldEntitySOA fields)
    {
        if (fields == null)
        {
            return new FieldTLAS(Array.Empty<BVHNode>(), Array.Empty<int>(), -1);
        }

        var worldBounds = fields.WorldBounds;
        var count = fields.Count;
        if (worldBounds == null || worldBounds.Length == 0 || count <= 0)
        {
            return new FieldTLAS(Array.Empty<BVHNode>(), Array.Empty<int>(), -1);
        }

        if (count > worldBounds.Length)
        {
            count = worldBounds.Length;
        }

        if (count <= 0)
        {
            return new FieldTLAS(Array.Empty<BVHNode>(), Array.Empty<int>(), -1);
        }

        var indices = new int[count];
        var centroids = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            indices[i] = i;
            centroids[i] = worldBounds[i].Center;
        }

        var nodes = new List<BVHNode>(Math.Max(1, count * 2));
        var leafFieldIds = new List<int>(count);

        const int LeafThreshold = 4;

        int BuildNode(int start, int length)
        {
            var bounds = worldBounds[indices[start]];
            for (var i = 1; i < length; i++)
            {
                bounds = Aabb3.Union(bounds, worldBounds[indices[start + i]]);
            }

            if (length <= LeafThreshold)
            {
                var leafStart = leafFieldIds.Count;
                for (var i = 0; i < length; i++)
                {
                    leafFieldIds.Add(indices[start + i]);
                }

                var node = new BVHNode(bounds, -leafStart - 1, length);
                nodes.Add(node);
                return nodes.Count - 1;
            }

            var cmin = centroids[indices[start]];
            var cmax = cmin;
            for (var i = 1; i < length; i++)
            {
                var c = centroids[indices[start + i]];
                cmin = Vector3.Min(cmin, c);
                cmax = Vector3.Max(cmax, c);
            }

            var extents = cmax - cmin;
            var axis = 0;
            if (extents.Y > extents.X && extents.Y >= extents.Z)
            {
                axis = 1;
            }
            else if (extents.Z > extents.X && extents.Z > extents.Y)
            {
                axis = 2;
            }

            Array.Sort(indices, start, length, new CentroidComparer(centroids, axis));

            var leftLength = length / 2;
            var rightLength = length - leftLength;
            var left = BuildNode(start, leftLength);
            var right = BuildNode(start + leftLength, rightLength);
            var internalBounds = Aabb3.Union(nodes[left].Bounds, nodes[right].Bounds);
            nodes.Add(new BVHNode(internalBounds, left, right));
            return nodes.Count - 1;
        }

        var root = BuildNode(0, count);
        return new FieldTLAS(nodes.ToArray(), leafFieldIds.ToArray(), root);
    }

    private sealed class CentroidComparer : IComparer<int>
    {
        private readonly Vector3[] _centroids;
        private readonly int _axis;

        public CentroidComparer(Vector3[] centroids, int axis)
        {
            _centroids = centroids;
            _axis = axis;
        }

        public int Compare(int x, int y)
        {
            var cx = GetAxis(_centroids[x], _axis);
            var cy = GetAxis(_centroids[y], _axis);
            var cmp = cx.CompareTo(cy);
            return cmp != 0 ? cmp : x.CompareTo(y);
        }

        private static float GetAxis(Vector3 v, int axis)
        {
            return axis switch
            {
                1 => v.Y,
                2 => v.Z,
                _ => v.X
            };
        }
    }
}
