using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace RendererCore.Calibration;

public struct SceneSignature
{
    public const int signature_version = 1;

    public string scene_path;
    public string engine_version;

    public int node_count;
    public int mesh_instance_count;
    public int field_source_count;
    public int light_count;
    public int camera_count;

    public int tri_estimate_min;
    public int tri_estimate_max;

    // Optional extents-only bounds hint (cheap to populate when available).
    public Vector3? bounds_hint;

    public ulong hash64;

    public ulong ComputeHash64()
    {
        var h = Fnva64OffsetBasis;

        HashInt32(ref h, signature_version);
        HashString(ref h, scene_path);
        HashString(ref h, engine_version);

        HashInt32(ref h, node_count);
        HashInt32(ref h, mesh_instance_count);
        HashInt32(ref h, field_source_count);
        HashInt32(ref h, light_count);
        HashInt32(ref h, camera_count);

        HashInt32(ref h, tri_estimate_min);
        HashInt32(ref h, tri_estimate_max);

        HashBool(ref h, bounds_hint.HasValue);
        if (bounds_hint is Vector3 ext)
        {
            HashSingle(ref h, ext.X);
            HashSingle(ref h, ext.Y);
            HashSingle(ref h, ext.Z);
        }

        hash64 = h;
        return h;
    }

    public override string ToString()
    {
        var scene = string.IsNullOrWhiteSpace(scene_path) ? "<none>" : scene_path;
        var engine = string.IsNullOrWhiteSpace(engine_version) ? "?" : engine_version;
        var tri = tri_estimate_min == tri_estimate_max
            ? tri_estimate_min.ToString(CultureInfo.InvariantCulture)
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{tri_estimate_min}..{tri_estimate_max}");
        var bounds = bounds_hint is Vector3 ext
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"[{ext.X:0.###},{ext.Y:0.###},{ext.Z:0.###}]")
            : "-";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"SceneSignature/v{signature_version} scene={scene} eng={engine} n={node_count} mesh={mesh_instance_count} field={field_source_count} light={light_count} cam={camera_count} tri={tri} bounds={bounds} hash=0x{hash64:x16}");
    }

    private const ulong Fnva64OffsetBasis = 14695981039346656037UL;
    private const ulong Fnva64Prime = 1099511628211UL;

    private static void HashByte(ref ulong h, byte value)
    {
        h ^= value;
        h *= Fnva64Prime;
    }

    private static void HashBool(ref ulong h, bool value)
        => HashByte(ref h, value ? (byte)1 : (byte)0);

    private static void HashInt32(ref ulong h, int value)
    {
        var v = unchecked((uint)value);
        HashByte(ref h, (byte)(v & 0xFF));
        HashByte(ref h, (byte)((v >> 8) & 0xFF));
        HashByte(ref h, (byte)((v >> 16) & 0xFF));
        HashByte(ref h, (byte)((v >> 24) & 0xFF));
    }

    private static void HashSingle(ref ulong h, float value)
    {
        var bits = unchecked((int)BitConverter.SingleToUInt32Bits(value));
        HashInt32(ref h, bits);
    }

    private static void HashString(ref ulong h, string value)
    {
        if (value is null)
        {
            HashByte(ref h, 0);
            return;
        }

        HashByte(ref h, 1);

        var bytes = Encoding.UTF8.GetBytes(value);
        HashInt32(ref h, bytes.Length);
        for (var i = 0; i < bytes.Length; i++)
        {
            HashByte(ref h, bytes[i]);
        }
    }
}
