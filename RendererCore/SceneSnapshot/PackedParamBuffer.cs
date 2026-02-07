using System;

namespace RendererCore.SceneSnapshot;

public sealed class PackedParamBuffer
{
    private float[] _data = Array.Empty<float>();

    public float[] Data
    {
        get => _data;
        init => _data = value ?? Array.Empty<float>();
    }

    public int AppendBlock8(float rInner, float rOuter, float amp, float a, float b, float c, float r0, float r1)
    {
        var offset = _data.Length;
        Array.Resize(ref _data, offset + 8);
        _data[offset + 0] = rInner;
        _data[offset + 1] = rOuter;
        _data[offset + 2] = amp;
        _data[offset + 3] = a;
        _data[offset + 4] = b;
        _data[offset + 5] = c;
        _data[offset + 6] = r0;
        _data[offset + 7] = r1;
        return offset;
    }
}
