using System;
using System.Numerics;

namespace RendererCore.SceneSnapshot;

public sealed class FieldEntitySOA
{
    public int Count { get; init; }
    public int[] MetricModel { get; init; } = Array.Empty<int>();
    public int[] ShapeType { get; init; } = Array.Empty<int>();
    public int[] CurveType { get; init; } = Array.Empty<int>();
    public Matrix4x4[] WorldFromLocal { get; init; } = Array.Empty<Matrix4x4>();
    public Matrix4x4[] LocalFromWorld { get; init; } = Array.Empty<Matrix4x4>();
    public Aabb3[] WorldBounds { get; init; } = Array.Empty<Aabb3>();
    public int[] ParamOffset { get; init; } = Array.Empty<int>();
    public int[] ParamLength { get; init; } = Array.Empty<int>();
    public uint[] Flags { get; init; } = Array.Empty<uint>();
}
