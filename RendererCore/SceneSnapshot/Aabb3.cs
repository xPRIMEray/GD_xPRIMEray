using System;
using System.Numerics;

namespace RendererCore.SceneSnapshot;

public readonly struct Aabb3
{
    public Vector3 Min { get; init; }
    public Vector3 Max { get; init; }

    public Aabb3(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
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
}
