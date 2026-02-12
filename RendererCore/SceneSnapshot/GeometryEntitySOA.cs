using System;

namespace RendererCore.SceneSnapshot;

public sealed class GeometryEntitySOA
{
    public int Count { get; }
    public Aabb3[] WorldBounds { get; }
    public long[] GodotInstanceIds { get; }

    public GeometryEntitySOA(int count)
    {
        Count = Math.Max(0, count);
        WorldBounds = Count > 0 ? new Aabb3[Count] : Array.Empty<Aabb3>();
        GodotInstanceIds = Count > 0 ? new long[Count] : Array.Empty<long>();
    }
}
