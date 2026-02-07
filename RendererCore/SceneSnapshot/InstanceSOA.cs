using System;
using System.Numerics;

namespace RendererCore.SceneSnapshot;

public sealed class InstanceSOA
{
    public int Count { get; init; }
    public int[] MeshId { get; init; } = Array.Empty<int>();
    public int[] MaterialId { get; init; } = Array.Empty<int>();
    public Matrix4x4[] WorldFromObject { get; init; } = Array.Empty<Matrix4x4>();
    public Matrix4x4[] ObjectFromWorld { get; init; } = Array.Empty<Matrix4x4>();
    public Aabb3[] WorldBounds { get; init; } = Array.Empty<Aabb3>();

    public static InstanceSOA Empty()
    {
        return new InstanceSOA
        {
            Count = 0,
            MeshId = Array.Empty<int>(),
            MaterialId = Array.Empty<int>(),
            WorldFromObject = Array.Empty<Matrix4x4>(),
            ObjectFromWorld = Array.Empty<Matrix4x4>(),
            WorldBounds = Array.Empty<Aabb3>()
        };
    }
}
