using System;
using System.Collections.Generic;
using System.Numerics;
using RendererCore.SceneSnapshot;

namespace RendererCore.Geometry;

public readonly struct GeometryBVHNode
{
    public readonly Aabb3 Bounds;
    public readonly int Left;
    public readonly int Right;

    public GeometryBVHNode(Aabb3 bounds, int left, int right)
    {
        Bounds = bounds;
        Left = left;
        Right = right;
    }
}

public sealed class GeometryTLAS
{
    public readonly GeometryBVHNode[] Nodes;
    public readonly int[] LeafGeometryIds;
    public readonly int RootIndex;

    public GeometryTLAS(GeometryBVHNode[] nodes, int[] leafGeometryIds, int rootIndex)
    {
        Nodes = nodes ?? Array.Empty<GeometryBVHNode>();
        LeafGeometryIds = leafGeometryIds ?? Array.Empty<int>();
        RootIndex = rootIndex;
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

                    results[count++] = LeafGeometryIds[leafStart + i];
                }
            }
            else
            {
                if (sp + 2 > stack.Length)
                {
                    return count;
                }

                stack[sp++] = node.Left;
                stack[sp++] = node.Right;
            }
        }

        return count;
    }

    public static GeometryTLAS Build(in GeometryEntitySOA geometry)
    {
        if (geometry == null)
        {
            return new GeometryTLAS(Array.Empty<GeometryBVHNode>(), Array.Empty<int>(), -1);
        }

        var worldBounds = geometry.WorldBounds;
        var count = geometry.Count;
        if (worldBounds == null || worldBounds.Length == 0 || count <= 0)
        {
            return new GeometryTLAS(Array.Empty<GeometryBVHNode>(), Array.Empty<int>(), -1);
        }

        if (count > worldBounds.Length)
        {
            count = worldBounds.Length;
        }

        if (count <= 0)
        {
            return new GeometryTLAS(Array.Empty<GeometryBVHNode>(), Array.Empty<int>(), -1);
        }

        var indices = new int[count];
        var centroids = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            indices[i] = i;
            centroids[i] = worldBounds[i].Center;
        }

        var nodes = new List<GeometryBVHNode>(Math.Max(1, count * 2));
        var leafGeometryIds = new List<int>(count);

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
                var leafStart = leafGeometryIds.Count;
                for (var i = 0; i < length; i++)
                {
                    leafGeometryIds.Add(indices[start + i]);
                }

                var node = new GeometryBVHNode(bounds, -leafStart - 1, length);
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
            nodes.Add(new GeometryBVHNode(internalBounds, left, right));
            return nodes.Count - 1;
        }

        var root = BuildNode(0, count);
        return new GeometryTLAS(nodes.ToArray(), leafGeometryIds.ToArray(), root);
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
