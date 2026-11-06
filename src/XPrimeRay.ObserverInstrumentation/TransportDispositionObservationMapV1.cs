#nullable enable

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using XPrimeRay.ObservationLayer;

namespace XPrimeRay.ObserverInstrumentation;

public enum TransportDispositionPixelStatusV1 : byte
{
    RecordedCompleteForAttempt = 0,
    RecorderDisabledOrMissing = 1,
    AttemptUnfinalized = 2,
    RecorderOverflow = 3,
    RecorderFailedOrIncomplete = 4,
    SnapshotCoherenceFailure = 5,
    MeasurementsMissing = 6,
    QuestionJoinMismatch = 7,
    ContactChannelMismatch = 8,
    TerminalMismatch = 9
}

public enum TransportDispositionV1 : byte
{
    Incomplete = 0,
    FaultNumericalOrQuery = 1,
    FirstContactObserved_Unqualified = 2,
    AbsorbedAtSample = 3,
    SegmentCapacityTruncated = 4,
    NoProbePerformed = 5,
    NoContact_BudgetExhausted = 6,
    NoContact_RangeExhausted = 7
}

public sealed class TransportDispositionObservationInputV1
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
    public required int[] ContactCounts { get; init; }
    public required int[] FirstContactSteps { get; init; }
}

public sealed class TransportDispositionObservationResultV1
{
    public required string ManifestPath { get; init; }
    public required TransportDispositionPixelStatusV1[] Statuses { get; init; }
    public required TransportDispositionV1[] Dispositions { get; init; }
    public required string MeasurementsSha256 { get; init; }
}

public static class TransportDispositionObservationMapV1
{
    public const string SchemaId = "TransportDispositionObservationMap-v1-le";
    public const string ArtifactClass = "provisional_recorded_observation";
    public const int IncompleteInteger = -1;
    public const int CompleteNotApplicable = -2;
    public const uint IncompleteFloatBits = 0xffffffffu;

    private static readonly byte[][] Palette =
    {
        new byte[] { 128, 128, 128, 255 },
        new byte[] { 239, 71, 111, 255 },
        new byte[] { 255, 209, 102, 255 },
        new byte[] { 131, 56, 236, 255 },
        new byte[] { 255, 127, 17, 255 },
        new byte[] { 90, 90, 90, 255 },
        new byte[] { 17, 138, 178, 255 },
        new byte[] { 6, 214, 160, 255 }
    };

    public static bool TryWrite(TransportDispositionObservationInputV1 input, out TransportDispositionObservationResultV1? result, out string reason)
    {
        result = null;
        if (!Validate(input, out reason)) return false;
        int total = checked(input.Width * input.Height);
        var status = Enumerable.Repeat(TransportDispositionPixelStatusV1.RecorderDisabledOrMissing, total).ToArray();
        var disposition = new TransportDispositionV1[total];
        uint[] traveled = Enumerable.Repeat(IncompleteFloatBits, total).ToArray();
        int[] steps = Enumerable.Repeat(IncompleteInteger, total).ToArray();
        int[] questions = Enumerable.Repeat(IncompleteInteger, total).ToArray();
        uint[] queriedChord = Enumerable.Repeat(IncompleteFloatBits, total).ToArray();
        uint[] maxGap = Enumerable.Repeat(IncompleteFloatBits, total).ToArray();
        int[] firstContact = Enumerable.Repeat(IncompleteInteger, total).ToArray();
        int[] remapsBeforeContact = Enumerable.Repeat(IncompleteInteger, total).ToArray();

        Dictionary<int, FormalTransportPixelMeasurementV1> measured = input.Snapshot.Measurements?.Pixels
            .ToDictionary(pixel => pixel.PixelIndex) ?? new Dictionary<int, FormalTransportPixelMeasurementV1>();
        if (input.Snapshot.RecorderEnabled)
        {
            foreach (FormalTransportObservationPixelV1 pixel in input.Snapshot.Pixels)
            {
                if ((uint)pixel.PixelIndex >= (uint)total) continue;
                int index = pixel.PixelIndex;
                TransportDispositionPixelStatusV1 pixelStatus = BaseStatus(pixel, input.Snapshot.CaptureGeneration);
                if (pixelStatus != TransportDispositionPixelStatusV1.RecordedCompleteForAttempt) { status[index] = pixelStatus; continue; }
                if (!measured.TryGetValue(index, out FormalTransportPixelMeasurementV1? measurement)) { status[index] = TransportDispositionPixelStatusV1.MeasurementsMissing; continue; }
                if (measurement.CaptureGeneration != pixel.CaptureGeneration || measurement.CommitVersion != pixel.CommitVersion)
                { status[index] = TransportDispositionPixelStatusV1.SnapshotCoherenceFailure; continue; }
                if (!QuestionsMatch(measurement)) { status[index] = TransportDispositionPixelStatusV1.QuestionJoinMismatch; continue; }
                if (!ContactsMatch(measurement, input.ContactCounts[index], input.FirstContactSteps[index], out int firstOrdinal))
                { status[index] = TransportDispositionPixelStatusV1.ContactChannelMismatch; continue; }
                if (!TerminalMatches(measurement)) { status[index] = TransportDispositionPixelStatusV1.TerminalMismatch; continue; }
                float path = BitConverter.UInt32BitsToSingle(measurement.TraveledBits);
                float chord = BitConverter.UInt32BitsToSingle(measurement.QueriedChordBits);
                float gap = BitConverter.UInt32BitsToSingle(measurement.MaxUnqueriedGapBits);
                if (!float.IsFinite(path) || !float.IsFinite(chord) || !float.IsFinite(gap) || path < 0f || chord < 0f || gap < 0f || chord > path || gap > path)
                { status[index] = TransportDispositionPixelStatusV1.QuestionJoinMismatch; continue; }

                status[index] = TransportDispositionPixelStatusV1.RecordedCompleteForAttempt;
                disposition[index] = Classify(measurement, input.ContactCounts[index]);
                traveled[index] = measurement.TraveledBits;
                steps[index] = measurement.StepsIntegrated;
                questions[index] = measurement.QuestionCount;
                queriedChord[index] = measurement.QueriedChordBits;
                maxGap[index] = measurement.MaxUnqueriedGapBits;
                firstContact[index] = input.ContactCounts[index] == 0 ? CompleteNotApplicable : input.FirstContactSteps[index];
                remapsBeforeContact[index] = input.ContactCounts[index] == 0 ? CompleteNotApplicable :
                    measurement.Events.Count(e => e.Kind == TransportEventKindV1.BoundaryRemapApplied && e.EventOrdinal < firstOrdinal);
            }
        }

        byte[] statusBytes = status.Select(value => (byte)value).ToArray();
        byte[] dispositionBytes = disposition.Select(value => (byte)value).ToArray();
        byte[] traveledBytes = EncodeUInt32(traveled);
        byte[] stepsBytes = EncodeInt32(steps);
        byte[] questionBytes = EncodeInt32(questions);
        byte[] chordBytes = EncodeUInt32(queriedChord);
        byte[] gapBytes = EncodeUInt32(maxGap);
        byte[] firstBytes = EncodeInt32(firstContact);
        byte[] remapBytes = EncodeInt32(remapsBeforeContact);
        byte[] rgba = MapRgba(status, disposition);
        byte[] png = PortalRemapObservationMapV1.EncodePng(input.Width, input.Height, rgba);
        var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["transport_status.bin"] = statusBytes,
            ["transport_disposition.bin"] = dispositionBytes,
            ["traveled_bits.bin"] = traveledBytes,
            ["steps_integrated.bin"] = stepsBytes,
            ["question_count.bin"] = questionBytes,
            ["queried_chord_bits.bin"] = chordBytes,
            ["max_unqueried_gap_bits.bin"] = gapBytes,
            ["first_contact_step.bin"] = firstBytes,
            ["remaps_before_first_contact.bin"] = remapBytes
        };
        string measurementsSha = MeasurementsSha(artifacts);
        string portalSnapshotSha = PortalRemapObservationMapV1.Sha256(PortalRemapObservationMapV1.SerializeProvisionalSnapshot(input.Snapshot));
        string legendJson = PortalRemapObservationMapV1.SerializeJson(BuildLegend());
        string manifestJson = PortalRemapObservationMapV1.SerializeJson(BuildManifest(input, status, disposition, artifacts, rgba, png, legendJson, measurementsSha, portalSnapshotSha));
        Directory.CreateDirectory(input.OutputDirectory);
        foreach ((string name, byte[] bytes) in artifacts) File.WriteAllBytes(Path.Combine(input.OutputDirectory, name), bytes);
        File.WriteAllBytes(Path.Combine(input.OutputDirectory, "transport_disposition.png"), png);
        File.WriteAllText(Path.Combine(input.OutputDirectory, "transport_disposition_legend.json"), legendJson, new UTF8Encoding(false));
        string manifestPath = Path.Combine(input.OutputDirectory, "transport_disposition_manifest.json");
        File.WriteAllText(manifestPath, manifestJson, new UTF8Encoding(false));
        result = new TransportDispositionObservationResultV1
        {
            ManifestPath = manifestPath,
            Statuses = (TransportDispositionPixelStatusV1[])status.Clone(),
            Dispositions = (TransportDispositionV1[])disposition.Clone(),
            MeasurementsSha256 = measurementsSha
        };
        reason = string.Empty;
        return true;
    }

    private static TransportDispositionPixelStatusV1 BaseStatus(FormalTransportObservationPixelV1 pixel, ulong generation)
    {
        if (pixel.CaptureGeneration != generation || pixel.CommitVersion == 0) return TransportDispositionPixelStatusV1.SnapshotCoherenceFailure;
        if (!pixel.Finalized) return TransportDispositionPixelStatusV1.AttemptUnfinalized;
        if (pixel.Overflowed) return TransportDispositionPixelStatusV1.RecorderOverflow;
        return pixel.RecordedCompleteForAttempt ? TransportDispositionPixelStatusV1.RecordedCompleteForAttempt : TransportDispositionPixelStatusV1.RecorderFailedOrIncomplete;
    }

    private static bool QuestionsMatch(FormalTransportPixelMeasurementV1 value)
    {
        FormalTransportEventMeasurementV1[] events = value.Events.Where(e => e.Kind == TransportEventKindV1.ProbeQuestionIssued).ToArray();
        bool failedFlag = (value.RawTerminalFlags & RawTerminalFlagsV1.ProbeQueryFailed) != 0;
        bool failedEvent = value.Events.Any(e => e.Kind == TransportEventKindV1.ProbeQueryFailed);
        return failedFlag == failedEvent && value.QuestionCount == value.QuerySteps.Count && events.Length == value.QuestionCount &&
            events.Select(e => e.IntegrationStepOrdinal).SequenceEqual(value.QuerySteps);
    }

    private static bool ContactsMatch(FormalTransportPixelMeasurementV1 value, int sealedCount, int sealedFirstStep, out int firstOrdinal)
    {
        FormalTransportEventMeasurementV1[] contacts = value.Events.Where(e => e.Kind == TransportEventKindV1.GeometryContactObserved).ToArray();
        firstOrdinal = contacts.Length == 0 ? -1 : contacts[0].EventOrdinal;
        if (sealedCount < 0 || contacts.Length != sealedCount) return false;
        return sealedCount == 0 ? sealedFirstStep == -1 : contacts[0].IntegrationStepOrdinal == sealedFirstStep;
    }

    private static bool TerminalMatches(FormalTransportPixelMeasurementV1 value)
    {
        FormalTransportEventMeasurementV1[] terminals = value.Events.Where(e => e.Kind == TransportEventKindV1.Terminal).ToArray();
        if (value.Termination == Pass1TransportTerminationV1.None) return terminals.Length == 0;
        if (terminals.Length != 1 || terminals[0].EventOrdinal != value.Events.Count - 1) return false;
        TransportReasonV1 expected = value.Termination switch
        {
            Pass1TransportTerminationV1.AbsorbedAtSample => TransportReasonV1.InsideAbsorbingRadius,
            Pass1TransportTerminationV1.MaxDistance => TransportReasonV1.MaxDistance,
            Pass1TransportTerminationV1.MaxSteps => TransportReasonV1.MaxSteps,
            Pass1TransportTerminationV1.SegmentCapacityTruncated => TransportReasonV1.SegmentCapacity,
            _ => TransportReasonV1.None
        };
        RawTerminalFlagsV1 expectedFlag = value.Termination switch
        {
            Pass1TransportTerminationV1.AbsorbedAtSample => RawTerminalFlagsV1.Absorbed,
            Pass1TransportTerminationV1.MaxDistance => RawTerminalFlagsV1.MaxDistanceReached,
            Pass1TransportTerminationV1.MaxSteps => RawTerminalFlagsV1.MaxStepsReached,
            Pass1TransportTerminationV1.SegmentCapacityTruncated => RawTerminalFlagsV1.SegmentCapacityTruncated,
            _ => RawTerminalFlagsV1.None
        };
        bool absorptionEventMatches = value.Termination != Pass1TransportTerminationV1.AbsorbedAtSample ||
            value.Events.Any(e => e.Kind == TransportEventKindV1.AbsorptionObserved);
        return terminals[0].Reason == expected && (value.RawTerminalFlags & expectedFlag) != 0 && absorptionEventMatches;
    }

    private static TransportDispositionV1 Classify(FormalTransportPixelMeasurementV1 value, int contactCount)
    {
        RawTerminalFlagsV1 faults = RawTerminalFlagsV1.NumericalFailure | RawTerminalFlagsV1.ProbeQueryFailed | RawTerminalFlagsV1.ZeroAdvance;
        if ((value.RawTerminalFlags & faults) != 0) return TransportDispositionV1.FaultNumericalOrQuery;
        if (contactCount > 0) return TransportDispositionV1.FirstContactObserved_Unqualified;
        if (value.Termination == Pass1TransportTerminationV1.AbsorbedAtSample) return TransportDispositionV1.AbsorbedAtSample;
        if (value.Termination == Pass1TransportTerminationV1.SegmentCapacityTruncated) return TransportDispositionV1.SegmentCapacityTruncated;
        if (value.QuestionCount == 0) return TransportDispositionV1.NoProbePerformed;
        if (value.Termination == Pass1TransportTerminationV1.MaxSteps) return TransportDispositionV1.NoContact_BudgetExhausted;
        return TransportDispositionV1.NoContact_RangeExhausted;
    }

    private static bool Validate(TransportDispositionObservationInputV1? input, out string reason)
    {
        reason = string.Empty;
        if (input is null || input.Snapshot is null) { reason = "input_missing"; return false; }
        if (input.Width <= 0 || input.Height <= 0) { reason = "dimensions_invalid"; return false; }
        if (input.FilmStride != 1) { reason = "film_stride_unsupported"; return false; }
        int total;
        try { total = checked(input.Width * input.Height); } catch (OverflowException) { reason = "dimensions_overflow"; return false; }
        if (input.ContactCounts?.Length != total || input.FirstContactSteps?.Length != total) { reason = "sealed_channels_invalid"; return false; }
        if (string.IsNullOrWhiteSpace(input.OutputDirectory) || string.IsNullOrWhiteSpace(input.RunId) || string.IsNullOrWhiteSpace(input.EngineCommit)) { reason = "identity_missing"; return false; }
        string[] hashes = { input.FormalPolicySha256, input.GeometrySnapshotSha256, input.FrozenBoundaryContentSha256, input.BoundaryExecutionAuthoritySha256, input.CanonicalQueryDatasetSha256 };
        if (hashes.Any(hash => hash is null || hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))) { reason = "acquisition_sha256_invalid"; return false; }
        if (input.SourceTreeState is not ("clean" or "dirty" or "unknown")) { reason = "source_tree_state_invalid"; return false; }
        if (input.SourceTreeState == "dirty" && (input.AuthoredDiffSha256.Length != 64 || input.AuthoredDiffSha256.Any(c => !Uri.IsHexDigit(c)))) { reason = "dirty_source_requires_fingerprint"; return false; }
        return true;
    }

    private static byte[] EncodeInt32(int[] values) { byte[] b = new byte[checked(values.Length * 4)]; for (int i = 0; i < values.Length; i++) BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(i * 4, 4), values[i]); return b; }
    private static byte[] EncodeUInt32(uint[] values) { byte[] b = new byte[checked(values.Length * 4)]; for (int i = 0; i < values.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i * 4, 4), values[i]); return b; }
    private static byte[] MapRgba(TransportDispositionPixelStatusV1[] status, TransportDispositionV1[] disposition)
    { byte[] b = new byte[checked(status.Length * 4)]; for (int i = 0; i < status.Length; i++) Buffer.BlockCopy(Palette[status[i] == 0 ? (int)disposition[i] : 0], 0, b, i * 4, 4); return b; }

    private static string MeasurementsSha(Dictionary<string, byte[]> artifacts)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(SchemaId + "\0"));
        byte[] lengthBytes = new byte[8];
        foreach ((string name, byte[] bytes) in artifacts.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            byte[] n = Encoding.UTF8.GetBytes(name);
            BinaryPrimitives.WriteInt32LittleEndian(lengthBytes.AsSpan(0, 4), n.Length); hash.AppendData(lengthBytes.AsSpan(0, 4)); hash.AppendData(n);
            BinaryPrimitives.WriteInt64LittleEndian(lengthBytes, bytes.Length); hash.AppendData(lengthBytes); hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static Dictionary<string, object> BuildLegend() => new()
    {
        ["schema"] = SchemaId,
        ["title"] = "Recorded transport disposition — provisional.",
        ["status_values"] = Enum.GetValues<TransportDispositionPixelStatusV1>().ToDictionary(v => ((byte)v).ToString(), v => (object)v.ToString()),
        ["disposition_values"] = new Dictionary<string, object>
        {
            ["0"] = DispositionPalette(TransportDispositionV1.Incomplete, Palette[0]),
            ["1"] = DispositionPalette(TransportDispositionV1.FaultNumericalOrQuery, Palette[1]),
            ["2"] = DispositionPalette(TransportDispositionV1.FirstContactObserved_Unqualified, Palette[2]),
            ["3"] = DispositionPalette(TransportDispositionV1.AbsorbedAtSample, Palette[3]),
            ["4"] = DispositionPalette(TransportDispositionV1.SegmentCapacityTruncated, Palette[4]),
            ["5"] = DispositionPalette(TransportDispositionV1.NoProbePerformed, Palette[5]),
            ["6"] = DispositionPalette(TransportDispositionV1.NoContact_BudgetExhausted, Palette[6]),
            ["7"] = DispositionPalette(TransportDispositionV1.NoContact_RangeExhausted, Palette[7])
        },
        ["status_display_rule"] = new Dictionary<string, object>
        {
            ["rule"] = "Any nonzero transport status is displayed as grey/incomplete regardless of disposition.",
            ["rgba8"] = Palette[0]
        },
        ["sentinels"] = new Dictionary<string, object> { ["incomplete_integer"] = -1, ["not_applicable_integer"] = -2, ["incomplete_float_bits_hex"] = "ffffffff" },
        ["coverage_boundary"] = "queried_chord is measured finite-query chord coverage, not continuous surface-search coverage",
        ["authority_notes"] = new[]
        {
            "Per-pixel contact-count authority is the sealed BVH contact_counts channel.",
            "First-contact-step comparison is sourced from the serial Godot replay.",
            "measurements_sha256 hashes the exported domain-framed channel bytes, not the underlying source measurements."
        }
    };

    private static Dictionary<string, object> DispositionPalette(TransportDispositionV1 value, byte[] rgba) => new()
    {
        ["label"] = value.ToString(),
        ["rgba8"] = rgba
    };

    private static Dictionary<string, object> BuildManifest(TransportDispositionObservationInputV1 input, TransportDispositionPixelStatusV1[] status, TransportDispositionV1[] disposition, Dictionary<string, byte[]> artifacts, byte[] rgba, byte[] png, string legendJson, string measurementsSha, string portalSnapshotSha)
    {
        var hashes = artifacts.ToDictionary(entry => entry.Key, entry => (object)PortalRemapObservationMapV1.HashEntry(entry.Value), StringComparer.Ordinal);
        hashes["transport_disposition_rgba8"] = PortalRemapObservationMapV1.HashEntry(rgba);
        hashes["transport_disposition.png"] = PortalRemapObservationMapV1.HashEntry(png);
        hashes["transport_disposition_legend.json"] = PortalRemapObservationMapV1.HashEntry(Encoding.UTF8.GetBytes(legendJson));
        return new Dictionary<string, object>
        {
            ["schema"] = SchemaId, ["artifact_class"] = ArtifactClass,
            ["title"] = "Recorded transport disposition — provisional.",
            ["scientific_ledger_sealed"] = false,
            ["claim_boundary"] = new[]
            {
                "This artifact is provisional recorder evidence.",
                "The scientific ledger is not sealed.",
                "Disposition 2 represents an unqualified first-contact observation.",
                "Diagnostic contact does not establish a qualified terminal-surface hit.",
                "No hit-coverage or confirmed-escape claim is made.",
                "The sealed Q deck is unchanged."
            },
            ["run_id"] = input.RunId,
            ["scene"] = new Dictionary<string, object> { ["semantic_id"] = input.SemanticSceneId, ["godot_path"] = input.GodotScenePath },
            ["acquisition"] = new Dictionary<string, object> { ["formal_policy_id"] = input.FormalPolicyId, ["formal_policy_sha256"] = input.FormalPolicySha256, ["geometry_snapshot_sha256"] = input.GeometrySnapshotSha256, ["frozen_boundary_content_sha256"] = input.FrozenBoundaryContentSha256, ["boundary_execution_authority_sha256"] = input.BoundaryExecutionAuthoritySha256, ["canonical_query_dataset_sha256"] = input.CanonicalQueryDatasetSha256, ["width"] = input.Width, ["height"] = input.Height, ["film_stride"] = input.FilmStride, ["pixel_order"] = "row-major: pixel = y * width + x", ["camera_pose_raw_u32"] = PortalRemapObservationMapV1.CameraBits(input.CameraPose), ["capture_generation"] = input.Snapshot.CaptureGeneration, ["pixel_commit_versions"] = PortalRemapObservationMapV1.BuildCommitVersions(status.Length, input.Snapshot) },
            ["recorder"] = new Dictionary<string, object>
            {
                ["enabled"] = input.Snapshot.RecorderEnabled,
                ["provisional_recorder_artifact_sha256"] = portalSnapshotSha,
                ["measurements_sha256"] = measurementsSha,
                ["measurements_sha256_scope"] = "exported domain-framed channel bytes; not the underlying source measurements",
                ["contact_count_authority"] = "sealed BVH contact_counts",
                ["first_contact_step_comparison_source"] = "serial Godot replay",
                ["scientific_ledger_sealed"] = false
            },
            ["source"] = new Dictionary<string, object> { ["engine_commit"] = input.EngineCommit, ["tree_state"] = input.SourceTreeState, ["authored_diff_sha256"] = input.AuthoredDiffSha256 },
            ["summary"] = new Dictionary<string, object> { ["status_histogram"] = status.GroupBy(v => v.ToString()).ToDictionary(g => g.Key, g => (object)g.Count()), ["disposition_histogram"] = disposition.GroupBy(v => v.ToString()).ToDictionary(g => g.Key, g => (object)g.Count()) },
            ["artifacts"] = hashes
        };
    }
}
