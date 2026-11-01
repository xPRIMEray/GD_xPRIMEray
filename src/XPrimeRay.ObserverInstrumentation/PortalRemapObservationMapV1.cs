#nullable enable

using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XPrimeRay.ObservationLayer;

namespace XPrimeRay.ObserverInstrumentation;

public enum PortalRemapPixelStatusV1 : byte
{
    RecordedCompleteForAttempt = 0,
    RecorderDisabledOrMissing = 1,
    AttemptUnfinalized = 2,
    RecorderOverflow = 3,
    RecorderFailedOrIncomplete = 4,
    SnapshotCoherenceFailure = 5
}

public sealed class PortalRemapObservationInputV1
{
    public required string OutputDirectory { get; init; }
    public required string RunId { get; init; }
    public required string SemanticSceneId { get; init; }
    public required string GodotScenePath { get; init; }
    public required string FormalPolicyId { get; init; }
    public required string FormalPolicySha256 { get; init; }
    public required string GeometrySnapshotSha256 { get; init; }
    public required string FrozenBoundaryContentSha256 { get; init; }
    public required string BoundaryExecutionAuthoritySha256 { get; init; }
    public required CameraPoseBitsV1 CameraPose { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int FilmStride { get; init; }
    public required string CanonicalQueryDatasetSha256 { get; init; }
    public required string EngineCommit { get; init; }
    public required string SourceTreeState { get; init; }
    public required string AuthoredDiffSha256 { get; init; }
    public required FormalTransportObservationSnapshotV1 Snapshot { get; init; }
}

public sealed class PortalRemapObservationResultV1
{
    public required string OutputDirectory { get; init; }
    public required string ManifestPath { get; init; }
    public required int[] Counts { get; init; }
    public required PortalRemapPixelStatusV1[] Statuses { get; init; }
    public required string ProvisionalRecorderArtifactSha256 { get; init; }
}

/// <summary>
/// Deterministic provisional visualization of recorded BoundaryRemapApplied events.
/// This artifact does not participate in the sealed Q deck or scientific ledger identity.
/// </summary>
public static class PortalRemapObservationMapV1
{
    public const string SchemaId = "PortalRemapObservationMap-v2-signed-count-le";
    public const string ArtifactClass = "provisional_recorded_observation";
    public const string Claim = "Recorded portal remaps — provisional.";
    public const int IncompleteCount = -1;

    private static readonly byte[] Count0 = { 16, 24, 32, 255 };
    private static readonly byte[] Count1 = { 40, 191, 232, 255 };
    private static readonly byte[] Count2 = { 255, 209, 102, 255 };
    private static readonly byte[] Count3Plus = { 239, 71, 111, 255 };
    private static readonly byte[] Incomplete = { 140, 140, 140, 255 };

    public static bool TryWrite(
        PortalRemapObservationInputV1 input,
        out PortalRemapObservationResultV1? result,
        out string reason)
    {
        result = null;
        if (!Validate(input, out reason)) return false;

        int total = checked(input.Width * input.Height);
        int[] counts = new int[total];
        Array.Fill(counts, IncompleteCount);
        var statuses = new PortalRemapPixelStatusV1[total];
        Array.Fill(statuses, PortalRemapPixelStatusV1.RecorderDisabledOrMissing);

        if (input.Snapshot.RecorderEnabled)
        {
            foreach (FormalTransportObservationPixelV1 pixel in input.Snapshot.Pixels)
            {
                if (pixel.PixelIndex < 0 || pixel.PixelIndex >= total) continue;
                int index = pixel.PixelIndex;
                if (pixel.CaptureGeneration != input.Snapshot.CaptureGeneration || pixel.CommitVersion == 0)
                {
                    statuses[index] = PortalRemapPixelStatusV1.SnapshotCoherenceFailure;
                    continue;
                }
                if (!pixel.Finalized)
                {
                    statuses[index] = PortalRemapPixelStatusV1.AttemptUnfinalized;
                    continue;
                }
                if (pixel.Overflowed)
                {
                    statuses[index] = PortalRemapPixelStatusV1.RecorderOverflow;
                    continue;
                }
                if (!pixel.RecordedCompleteForAttempt)
                {
                    statuses[index] = PortalRemapPixelStatusV1.RecorderFailedOrIncomplete;
                    continue;
                }

                int count = 0;
                foreach (TransportEventKindV1 kind in pixel.EventKinds)
                    if (kind == TransportEventKindV1.BoundaryRemapApplied) count++;
                counts[index] = count;
                statuses[index] = PortalRemapPixelStatusV1.RecordedCompleteForAttempt;
            }
        }

        byte[] countBytes = EncodeCounts(counts);
        byte[] statusBytes = statuses.Select(status => (byte)status).ToArray();
        byte[] rgba = MapRgba(counts, statuses);
        byte[] png = EncodePng(input.Width, input.Height, rgba);
        byte[] snapshotBytes = SerializeProvisionalSnapshot(input.Snapshot);
        string snapshotSha = Sha256(snapshotBytes);

        string legendJson = SerializeJson(BuildLegend());
        Dictionary<string, object> manifest = BuildManifest(
            input, counts, statuses, snapshotSha, countBytes, statusBytes, rgba, png, legendJson);
        string manifestJson = SerializeJson(manifest);

        Directory.CreateDirectory(input.OutputDirectory);
        File.WriteAllBytes(Path.Combine(input.OutputDirectory, "portal_remap_count.bin"), countBytes);
        File.WriteAllBytes(Path.Combine(input.OutputDirectory, "portal_remap_status.bin"), statusBytes);
        File.WriteAllBytes(Path.Combine(input.OutputDirectory, "portal_remap_count.png"), png);
        File.WriteAllText(Path.Combine(input.OutputDirectory, "portal_remap_legend.json"), legendJson, new UTF8Encoding(false));
        string manifestPath = Path.Combine(input.OutputDirectory, "portal_remap_manifest.json");
        File.WriteAllText(manifestPath, manifestJson, new UTF8Encoding(false));

        result = new PortalRemapObservationResultV1
        {
            OutputDirectory = input.OutputDirectory,
            ManifestPath = manifestPath,
            Counts = (int[])counts.Clone(),
            Statuses = (PortalRemapPixelStatusV1[])statuses.Clone(),
            ProvisionalRecorderArtifactSha256 = snapshotSha
        };
        reason = string.Empty;
        return true;
    }

    private static bool Validate(PortalRemapObservationInputV1? input, out string reason)
    {
        reason = string.Empty;
        if (input is null) { reason = "input_missing"; return false; }
        if (string.IsNullOrWhiteSpace(input.OutputDirectory)) { reason = "output_directory_missing"; return false; }
        if (string.IsNullOrWhiteSpace(input.RunId) || string.IsNullOrWhiteSpace(input.SemanticSceneId) ||
            string.IsNullOrWhiteSpace(input.GodotScenePath) || string.IsNullOrWhiteSpace(input.FormalPolicyId))
        { reason = "identity_missing"; return false; }
        if (!IsSha256(input.FormalPolicySha256) || !IsSha256(input.GeometrySnapshotSha256) ||
            !IsSha256(input.FrozenBoundaryContentSha256) || !IsSha256(input.BoundaryExecutionAuthoritySha256) ||
            !IsSha256(input.CanonicalQueryDatasetSha256))
        { reason = "acquisition_sha256_invalid"; return false; }
        if (input.Width <= 0 || input.Height <= 0 || input.FilmStride <= 0)
        { reason = "dimensions_invalid"; return false; }
        if (input.FilmStride != 1)
        { reason = "film_stride_unsupported"; return false; }
        int total;
        try { total = checked(input.Width * input.Height); }
        catch (OverflowException) { reason = "dimensions_overflow"; return false; }
        if (input.Snapshot is null) { reason = "recorder_snapshot_missing"; return false; }
        if (input.SourceTreeState is not ("clean" or "dirty" or "unknown"))
        { reason = "source_tree_state_invalid"; return false; }
        if (input.SourceTreeState == "dirty" && !IsSha256(input.AuthoredDiffSha256))
        { reason = "dirty_source_requires_fingerprint"; return false; }
        if (input.SourceTreeState == "clean" && input.AuthoredDiffSha256.Length != 0)
        { reason = "clean_source_has_fingerprint"; return false; }
        if (string.IsNullOrWhiteSpace(input.EngineCommit)) { reason = "source_revision_missing"; return false; }
        return true;
    }

    private static Dictionary<string, object> BuildLegend() => new()
    {
        ["schema"] = SchemaId,
        ["title"] = Claim,
        ["mapping"] = new Dictionary<string, object>
        {
            ["0"] = Palette(Count0, "0 remaps"),
            ["1"] = Palette(Count1, "1 remap"),
            ["2"] = Palette(Count2, "2 remaps"),
            ["3+"] = Palette(Count3Plus, "3 or more remaps; exact count remains in portal_remap_count.bin"),
            ["incomplete"] = Palette(Incomplete, "Incomplete / N/A; signed count is -1 and status is in portal_remap_status.bin")
        },
        ["count_binary"] = new Dictionary<string, object>
        {
            ["encoding"] = "signed-int32-little-endian",
            ["complete_domain"] = "nonnegative exact BoundaryRemapApplied count",
            ["incomplete_sentinel"] = IncompleteCount
        },
        ["status_values"] = Enum.GetValues<PortalRemapPixelStatusV1>()
            .ToDictionary(value => ((byte)value).ToString(), value => (object)value.ToString(), StringComparer.Ordinal)
    };

    private static Dictionary<string, object> Palette(byte[] rgba, string label) => new()
    {
        ["label"] = label,
        ["rgba8"] = rgba
    };

    private static Dictionary<string, object> BuildManifest(
        PortalRemapObservationInputV1 input,
        int[] counts,
        PortalRemapPixelStatusV1[] statuses,
        string snapshotSha,
        byte[] countBytes,
        byte[] statusBytes,
        byte[] rgba,
        byte[] png,
        string legendJson)
    {
        Dictionary<string, int> statusHistogram = Enum.GetValues<PortalRemapPixelStatusV1>()
            .ToDictionary(value => value.ToString(), _ => 0, StringComparer.Ordinal);
        foreach (PortalRemapPixelStatusV1 status in statuses) statusHistogram[status.ToString()]++;
        int exactTotal = 0;
        for (int i = 0; i < counts.Length; i++)
            if (statuses[i] == PortalRemapPixelStatusV1.RecordedCompleteForAttempt) exactTotal += counts[i];

        return new Dictionary<string, object>
        {
            ["schema"] = SchemaId,
            ["artifact_class"] = ArtifactClass,
            ["title"] = Claim,
            ["claim_boundary"] = new[]
            {
                "Counts are recorded BoundaryRemapApplied events only.",
                "This bundle is provisional recorder evidence, not a sealed scientific ledger.",
                "Incomplete pixels use signed int32 sentinel -1 and are never interpreted as zero.",
                "The bundle does not alter or extend the sealed Q deck."
            },
            ["run_id"] = input.RunId,
            ["scene"] = new Dictionary<string, object>
            {
                ["semantic_id"] = input.SemanticSceneId,
                ["godot_path"] = input.GodotScenePath
            },
            ["acquisition"] = new Dictionary<string, object>
            {
                ["formal_policy_id"] = input.FormalPolicyId,
                ["formal_policy_sha256"] = input.FormalPolicySha256,
                ["geometry_snapshot_sha256"] = input.GeometrySnapshotSha256,
                ["frozen_boundary_content_sha256"] = input.FrozenBoundaryContentSha256,
                ["boundary_execution_authority_sha256"] = input.BoundaryExecutionAuthoritySha256,
                ["canonical_query_dataset_sha256"] = input.CanonicalQueryDatasetSha256,
                ["width"] = input.Width,
                ["height"] = input.Height,
                ["film_stride"] = input.FilmStride,
                ["pixel_order"] = "row-major: pixel = y * width + x",
                ["camera_pose_raw_u32"] = CameraBits(input.CameraPose),
                ["capture_generation"] = input.Snapshot.CaptureGeneration,
                ["capture_coherence"] = new Dictionary<string, object>
                {
                    ["guarantee"] = "immutable recorder snapshot captured at the completed Formal seal boundary",
                    ["independent_generation_authority"] = false,
                    ["independent_commit_version_authority"] = false,
                    ["pixel_commit_versions"] = BuildCommitVersions(counts.Length, input.Snapshot)
                }
            },
            ["recorder"] = new Dictionary<string, object>
            {
                ["enabled"] = input.Snapshot.RecorderEnabled,
                ["snapshot_capture_generation"] = input.Snapshot.CaptureGeneration,
                ["attempt_allocations"] = input.Snapshot.AttemptAllocationCount,
                ["recorded_events"] = input.Snapshot.RecordedEventCount,
                ["finalization_eligible_commits"] = input.Snapshot.FinalizationEligibleCommitCount,
                ["accepted_finalizations"] = input.Snapshot.AcceptedFinalizationCount,
                ["stale_finalization_rejects"] = input.Snapshot.StaleFinalizationRejectCount,
                ["provisional_recorder_artifact_sha256"] = snapshotSha,
                ["scientific_ledger_sealed"] = false
            },
            ["source"] = new Dictionary<string, object>
            {
                ["engine_commit"] = input.EngineCommit,
                ["tree_state"] = input.SourceTreeState,
                ["authored_diff_sha256"] = input.AuthoredDiffSha256
            },
            ["summary"] = new Dictionary<string, object>
            {
                ["recorded_portal_remaps"] = exactTotal,
                ["status_histogram"] = statusHistogram
            },
            ["artifacts"] = new Dictionary<string, object>
            {
                ["portal_remap_count.bin"] = HashEntry(countBytes),
                ["portal_remap_status.bin"] = HashEntry(statusBytes),
                ["portal_remap_count_rgba8"] = HashEntry(rgba),
                ["portal_remap_count.png"] = HashEntry(png),
                ["portal_remap_legend.json"] = HashEntry(Encoding.UTF8.GetBytes(legendJson))
            }
        };
    }

    private static uint[] CameraBits(CameraPoseBitsV1 camera) => new[]
    {
        camera.BasisXX, camera.BasisXY, camera.BasisXZ,
        camera.BasisYX, camera.BasisYY, camera.BasisYZ,
        camera.BasisZX, camera.BasisZY, camera.BasisZZ,
        camera.OriginX, camera.OriginY, camera.OriginZ, camera.Fov
    };

    private static uint[] BuildCommitVersions(int total, FormalTransportObservationSnapshotV1 snapshot)
    {
        uint[] versions = new uint[total];
        foreach (FormalTransportObservationPixelV1 pixel in snapshot.Pixels)
            if ((uint)pixel.PixelIndex < (uint)total) versions[pixel.PixelIndex] = pixel.CommitVersion;
        return versions;
    }

    private static Dictionary<string, object> HashEntry(byte[] bytes) => new()
    {
        ["bytes"] = bytes.Length,
        ["sha256"] = Sha256(bytes)
    };

    private static byte[] EncodeCounts(int[] counts)
    {
        byte[] bytes = new byte[checked(counts.Length * 4)];
        for (int i = 0; i < counts.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4, 4), counts[i]);
        return bytes;
    }

    private static byte[] MapRgba(int[] counts, PortalRemapPixelStatusV1[] statuses)
    {
        byte[] bytes = new byte[checked(counts.Length * 4)];
        for (int i = 0; i < counts.Length; i++)
        {
            byte[] color = statuses[i] != PortalRemapPixelStatusV1.RecordedCompleteForAttempt
                ? Incomplete
                : counts[i] switch { 0 => Count0, 1 => Count1, 2 => Count2, _ => Count3Plus };
            Buffer.BlockCopy(color, 0, bytes, i * 4, 4);
        }
        return bytes;
    }

    private static byte[] SerializeProvisionalSnapshot(FormalTransportObservationSnapshotV1 snapshot)
    {
        using MemoryStream stream = new();
        WriteBytes(stream, Encoding.UTF8.GetBytes("PortalRemapObservationRecorderSnapshot-v1\0"));
        WriteUInt64(stream, snapshot.CaptureGeneration);
        WriteByte(stream, snapshot.RecorderEnabled ? (byte)1 : (byte)0);
        WriteInt32(stream, snapshot.AttemptAllocationCount);
        WriteInt32(stream, snapshot.RecordedEventCount);
        WriteInt32(stream, snapshot.FinalizationEligibleCommitCount);
        WriteInt32(stream, snapshot.AcceptedFinalizationCount);
        WriteInt32(stream, snapshot.StaleFinalizationRejectCount);
        WriteInt32(stream, snapshot.Pixels.Count);
        foreach (FormalTransportObservationPixelV1 pixel in snapshot.Pixels)
        {
            WriteInt32(stream, pixel.PixelIndex);
            WriteUInt64(stream, pixel.CaptureGeneration);
            WriteUInt32(stream, pixel.CommitVersion);
            WriteByte(stream, pixel.Finalized ? (byte)1 : (byte)0);
            WriteByte(stream, (byte)pixel.RecorderCompletion);
            WriteString(stream, pixel.ProvisionalReason);
            WriteInt32(stream, pixel.EventCapacity);
            WriteByte(stream, pixel.Overflowed ? (byte)1 : (byte)0);
            WriteInt32(stream, pixel.EventKinds.Count);
            foreach (TransportEventKindV1 kind in pixel.EventKinds) WriteUInt16(stream, (ushort)kind);
        }
        return stream.ToArray();
    }

    private static byte[] EncodePng(int width, int height, byte[] rgba)
    {
        using MemoryStream raw = new();
        for (int y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgba, y * width * 4, width * 4);
        }
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }
        using MemoryStream png = new();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WriteChunk(png, "IHDR", BuildIhdr(width, height));
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static byte[] BuildIhdr(int width, int height)
    {
        byte[] data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), height);
        data[8] = 8; data[9] = 6;
        return data;
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length); output.Write(typeBytes); output.Write(data);
        uint crc = 0xffffffffu;
        foreach (byte value in typeBytes.Concat(data)) crc = Crc32Byte(crc, value);
        crc ^= 0xffffffffu;
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }

    private static uint Crc32Byte(uint crc, byte value)
    {
        crc ^= value;
        for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
        return crc;
    }

    private static string SerializeJson(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + "\n";

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => Uri.IsHexDigit(character));

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void WriteByte(Stream stream, byte value) => stream.WriteByte(value);
    private static void WriteUInt16(Stream stream, ushort value) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, value); stream.Write(b); }
    private static void WriteInt32(Stream stream, int value) => WriteUInt32(stream, unchecked((uint)value));
    private static void WriteUInt32(Stream stream, uint value) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, value); stream.Write(b); }
    private static void WriteUInt64(Stream stream, ulong value) { Span<byte> b = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, value); stream.Write(b); }
    private static void WriteBytes(Stream stream, byte[] bytes) => stream.Write(bytes);
    private static void WriteString(Stream stream, string value) { byte[] b = Encoding.UTF8.GetBytes(value); WriteInt32(stream, b.Length); WriteBytes(stream, b); }
}
