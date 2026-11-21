using System.Security.Cryptography;
using System.Text;

namespace XPrimeRay.ObserverInstrumentation.Materials;

public enum MaterialBindingLookup : byte
{
    Unbound = 1,
    Bound = 2,
    Unsupported = 3,
}

/// <summary>
/// An immutable binding table: each key maps to a descriptor or to an explicit unsupported reason; absent keys
/// are unbound. Lookup uses sorted parallel arrays and binary search, with no strings, no allocation and no
/// shared mutable state. Concurrent read-only use from any number of threads is safe.
/// <para><b>Provenance.</b> <see cref="ProvenanceSha256"/> is the lowercase hex SHA-256 of
/// <see cref="CopyCanonicalSerialization"/>. All integers and floats are little-endian; floats are IEEE-754 binary32
/// bit patterns with negative zero canonicalized to positive zero. The layout is:</para>
/// <code>
/// ascii "xprimeray-material-binding-table-v1" 0x00
/// ascii WorldCheckerEvaluator.ShaderSourceSha256 (64 lowercase hex chars) 0x00
/// f32 MaterialSamplingConvention.InwardBiasMeters
/// f32 MaterialSamplingConvention.NearBoundaryCells
/// u32 descriptorCount
///   per descriptor, in index order:
///   u8 kind, u8 encoding, u8 correspondence,
///   Flat:              f32 r, g, b
///   WorldSpaceChecker: f32 aR, aG, aB, bR, bG, bB, scale
/// u32 entryCount
///   per entry, sorted by (collider id, shape index):
///   u64 colliderInstanceId, i32 shapeIndex, i32 descriptorIndex (-1 if unsupported), u8 reason
/// </code>
/// <para>Descriptors are deduplicated by value and numbered in first-use order over the sorted entries, so the
/// serialization does not depend on input order. <see cref="Generation"/> is deliberately excluded, so restoring
/// identical bindings restores the identical provenance.</para>
/// </summary>
public sealed class MaterialBindingTable
{
    public const string SerializationFormat = "xprimeray-material-binding-table-v1";

    private readonly ulong[] _colliderIds;
    private readonly int[] _shapeIndices;
    private readonly int[] _descriptorIndices;
    private readonly MaterialUnsupportedReason[] _reasons;
    private readonly MaterialDescriptor[] _descriptors;
    private readonly byte[] _canonical;

    private MaterialBindingTable(
        long generation,
        ulong[] colliderIds,
        int[] shapeIndices,
        int[] descriptorIndices,
        MaterialUnsupportedReason[] reasons,
        MaterialDescriptor[] descriptors,
        int boundCount)
    {
        Generation = generation;
        _colliderIds = colliderIds;
        _shapeIndices = shapeIndices;
        _descriptorIndices = descriptorIndices;
        _reasons = reasons;
        _descriptors = descriptors;
        BoundCount = boundCount;
        UnsupportedCount = colliderIds.Length - boundCount;
        _canonical = Serialize();
        ProvenanceSha256 = Convert.ToHexString(SHA256.HashData(_canonical)).ToLowerInvariant();
    }

    /// <summary>Supplied by the owner (monotonic per camera). Not part of provenance.</summary>
    public long Generation { get; }

    public string ProvenanceSha256 { get; }
    public int EntryCount => _colliderIds.Length;
    public int BoundCount { get; }
    public int UnsupportedCount { get; }
    public int DescriptorCount => _descriptors.Length;

    public static MaterialBindingTable Empty(long generation) => Build(generation, ReadOnlySpan<MaterialBindingEntry>.Empty);

    /// <summary>
    /// Validates, sorts and freezes <paramref name="entries"/>. Throws <see cref="ArgumentException"/> for a negative
    /// generation, an invalid key, a duplicate key, an invalid descriptor, a bound entry carrying a reason, or an
    /// unsupported entry without a defined reason. Building happens on the main thread, never in the sampling path.
    /// </summary>
    public static MaterialBindingTable Build(long generation, ReadOnlySpan<MaterialBindingEntry> entries)
    {
        if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation), generation, "generation must be non-negative");

        var sorted = new MaterialBindingEntry[entries.Length];
        for (int index = 0; index < entries.Length; index++)
        {
            MaterialBindingEntry entry = entries[index];
            if (!entry.Key.IsValid)
            {
                throw new ArgumentException($"entry {index}: invalid key {entry.Key}", nameof(entries));
            }

            if (entry.IsBound)
            {
                if (entry.Reason != MaterialUnsupportedReason.None)
                {
                    throw new ArgumentException($"entry {index}: bound entry carries reason {entry.Reason}", nameof(entries));
                }

                MaterialDescriptorError error = entry.Descriptor.Validate();
                if (error != MaterialDescriptorError.None)
                {
                    throw new ArgumentException($"entry {index}: invalid descriptor ({error}) for {entry.Key}", nameof(entries));
                }

                sorted[index] = MaterialBindingEntry.Bound(entry.Key, entry.Descriptor.Canonical());
            }
            else
            {
                if (!MaterialUnsupportedReasons.IsDefinedUnsupported(entry.Reason))
                {
                    throw new ArgumentException($"entry {index}: unsupported entry needs a defined reason, got {(int)entry.Reason}", nameof(entries));
                }

                if (entry.Descriptor != default)
                {
                    throw new ArgumentException($"entry {index}: unsupported entry carries a descriptor", nameof(entries));
                }

                sorted[index] = entry;
            }
        }

        Array.Sort(sorted, static (left, right) => left.Key.CompareTo(right.Key));

        var colliderIds = new ulong[sorted.Length];
        var shapeIndices = new int[sorted.Length];
        var descriptorIndices = new int[sorted.Length];
        var reasons = new MaterialUnsupportedReason[sorted.Length];
        var descriptors = new List<MaterialDescriptor>();
        var descriptorLookup = new Dictionary<MaterialDescriptor, int>();
        int boundCount = 0;
        for (int index = 0; index < sorted.Length; index++)
        {
            MaterialBindingEntry entry = sorted[index];
            if (index > 0 && sorted[index - 1].Key == entry.Key)
            {
                throw new ArgumentException($"duplicate key {entry.Key}", nameof(entries));
            }

            colliderIds[index] = entry.Key.ColliderInstanceId;
            shapeIndices[index] = entry.Key.ShapeIndex;
            reasons[index] = entry.Reason;
            if (entry.IsBound)
            {
                if (!descriptorLookup.TryGetValue(entry.Descriptor, out int descriptorIndex))
                {
                    descriptorIndex = descriptors.Count;
                    descriptors.Add(entry.Descriptor);
                    descriptorLookup.Add(entry.Descriptor, descriptorIndex);
                }

                descriptorIndices[index] = descriptorIndex;
                boundCount++;
            }
            else
            {
                descriptorIndices[index] = -1;
            }
        }

        return new MaterialBindingTable(
            generation, colliderIds, shapeIndices, descriptorIndices, reasons, descriptors.ToArray(), boundCount);
    }

    /// <summary>
    /// Returns true when the key is present: a bound key yields its descriptor index and
    /// <see cref="MaterialUnsupportedReason.None"/>; an unsupported key yields -1 and its reason. Returns false
    /// (-1, None) for an unbound key.
    /// </summary>
    public bool TryLookup(MaterialBindingKey key, out int descriptorIndex, out MaterialUnsupportedReason reason) =>
        Resolve(Find(key.ColliderInstanceId, key.ShapeIndex), out descriptorIndex, out reason) != MaterialBindingLookup.Unbound;

    /// <summary>The same as <see cref="TryLookup"/>, using a caller-owned one-entry cache.</summary>
    public MaterialBindingLookup Lookup(
        MaterialBindingKey key,
        ref MaterialLookupCache cache,
        out int descriptorIndex,
        out MaterialUnsupportedReason reason)
    {
        int slot;
        if (ReferenceEquals(cache.Table, this) && cache.Key == key)
        {
            slot = cache.Slot;
            cache.Hits++;
        }
        else
        {
            slot = Find(key.ColliderInstanceId, key.ShapeIndex);
            cache.Table = this;
            cache.Key = key;
            cache.Slot = slot;
            cache.Misses++;
        }

        return Resolve(slot, out descriptorIndex, out reason);
    }

    public MaterialDescriptor GetDescriptor(int descriptorIndex) => _descriptors[descriptorIndex];

    internal ref readonly MaterialDescriptor DescriptorRef(int descriptorIndex) => ref _descriptors[descriptorIndex];

    /// <summary>The inventory in canonical (sorted) order, with canonicalized descriptors.</summary>
    public MaterialBindingEntry GetEntry(int entryIndex)
    {
        var key = new MaterialBindingKey(_colliderIds[entryIndex], _shapeIndices[entryIndex]);
        int descriptorIndex = _descriptorIndices[entryIndex];
        return descriptorIndex >= 0
            ? MaterialBindingEntry.Bound(key, _descriptors[descriptorIndex])
            : MaterialBindingEntry.Unsupported(key, _reasons[entryIndex]);
    }

    public int GetEntryDescriptorIndex(int entryIndex) => _descriptorIndices[entryIndex];

    public byte[] CopyCanonicalSerialization() => (byte[])_canonical.Clone();

    private MaterialBindingLookup Resolve(int slot, out int descriptorIndex, out MaterialUnsupportedReason reason)
    {
        if (slot < 0)
        {
            descriptorIndex = -1;
            reason = MaterialUnsupportedReason.None;
            return MaterialBindingLookup.Unbound;
        }

        descriptorIndex = _descriptorIndices[slot];
        reason = _reasons[slot];
        return descriptorIndex >= 0 ? MaterialBindingLookup.Bound : MaterialBindingLookup.Unsupported;
    }

    private int Find(ulong colliderId, int shapeIndex)
    {
        ulong[] ids = _colliderIds;
        int low = 0;
        int high = ids.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            ulong id = ids[middle];
            int comparison = id < colliderId ? -1 : id > colliderId ? 1 : _shapeIndices[middle].CompareTo(shapeIndex);
            if (comparison == 0) return middle;
            if (comparison < 0) low = middle + 1;
            else high = middle - 1;
        }

        return -1;
    }

    private byte[] Serialize()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            WriteAsciiZ(writer, SerializationFormat);
            WriteAsciiZ(writer, WorldCheckerEvaluator.ShaderSourceSha256);
            WriteFloat(writer, MaterialSamplingConvention.InwardBiasMeters);
            WriteFloat(writer, MaterialSamplingConvention.NearBoundaryCells);
            writer.Write((uint)_descriptors.Length);
            foreach (MaterialDescriptor descriptor in _descriptors)
            {
                writer.Write((byte)descriptor.Kind);
                writer.Write((byte)descriptor.Encoding);
                writer.Write((byte)descriptor.Correspondence);
                if (descriptor.Kind == MaterialKind.Flat)
                {
                    WriteRgb(writer, descriptor.Flat);
                }
                else
                {
                    WriteRgb(writer, descriptor.Checker.ColorA);
                    WriteRgb(writer, descriptor.Checker.ColorB);
                    WriteFloat(writer, descriptor.Checker.Scale);
                }
            }

            writer.Write((uint)_colliderIds.Length);
            for (int index = 0; index < _colliderIds.Length; index++)
            {
                writer.Write(_colliderIds[index]);
                writer.Write(_shapeIndices[index]);
                writer.Write(_descriptorIndices[index]);
                writer.Write((byte)_reasons[index]);
            }
        }

        return stream.ToArray();
    }

    private static void WriteAsciiZ(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.ASCII.GetBytes(value));
        writer.Write((byte)0);
    }

    private static void WriteRgb(BinaryWriter writer, Rgb value)
    {
        WriteFloat(writer, value.R);
        WriteFloat(writer, value.G);
        WriteFloat(writer, value.B);
    }

    private static void WriteFloat(BinaryWriter writer, float value) =>
        writer.Write(BitConverter.SingleToUInt32Bits(Rgb.CanonicalZero(value)));
}
