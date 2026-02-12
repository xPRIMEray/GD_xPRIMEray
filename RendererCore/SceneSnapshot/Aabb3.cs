using System;
using System.Numerics;

namespace RendererCore.SceneSnapshot;

public readonly struct Aabb3
{
    public Vector3 Min { get; init; }
    public Vector3 Max { get; init; }

    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Extents => Max - Min;

    public Aabb3(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public static Aabb3 FromSegment(Vector3 a, Vector3 b)
    {
        var min = new Vector3(
            MathF.Min(a.X, b.X),
            MathF.Min(a.Y, b.Y),
            MathF.Min(a.Z, b.Z));
        var max = new Vector3(
            MathF.Max(a.X, b.X),
            MathF.Max(a.Y, b.Y),
            MathF.Max(a.Z, b.Z));
        return new Aabb3(min, max);
    }

    public Aabb3 Encapsulate(Vector3 p)
    {
        var min = new Vector3(
            MathF.Min(Min.X, p.X),
            MathF.Min(Min.Y, p.Y),
            MathF.Min(Min.Z, p.Z));
        var max = new Vector3(
            MathF.Max(Max.X, p.X),
            MathF.Max(Max.Y, p.Y),
            MathF.Max(Max.Z, p.Z));
        return new Aabb3(min, max);
    }

    public Aabb3 Expand(float r)
    {
        var delta = new Vector3(r, r, r);
        return new Aabb3(Min - delta, Max + delta);
    }

    public bool Contains(Vector3 p)
    {
        return p.X >= Min.X && p.Y >= Min.Y && p.Z >= Min.Z
            && p.X <= Max.X && p.Y <= Max.Y && p.Z <= Max.Z;
    }

    public bool Overlaps(Aabb3 other)
    {
        return Min.X <= other.Max.X && Max.X >= other.Min.X
            && Min.Y <= other.Max.Y && Max.Y >= other.Min.Y
            && Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
    }

    public static Aabb3 Union(Aabb3 a, Aabb3 b)
    {
        var min = new Vector3(
            MathF.Min(a.Min.X, b.Min.X),
            MathF.Min(a.Min.Y, b.Min.Y),
            MathF.Min(a.Min.Z, b.Min.Z));
        var max = new Vector3(
            MathF.Max(a.Max.X, b.Max.X),
            MathF.Max(a.Max.Y, b.Max.Y),
            MathF.Max(a.Max.Z, b.Max.Z));
        return new Aabb3(min, max);
    }
}
