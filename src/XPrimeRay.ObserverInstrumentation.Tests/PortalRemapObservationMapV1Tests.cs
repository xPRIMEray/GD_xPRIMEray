using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using XPrimeRay.ObservationLayer;

namespace XPrimeRay.ObserverInstrumentation.Tests;

internal static class PortalRemapObservationMapV1Tests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static void Run()
    {
        CountStatusOrientationAndGoldenArtifactsAreStable();
        IncompleteStatesNeverBecomeZero();
        SnapshotCoherenceIsReportedWithoutIndependentAuthorityClaims();
        NonUnitFilmStrideFailsClosed();
        RecorderOffIsIsolated();
        SnapshotOwnsImmutableCopies();
    }

    private static void CountStatusOrientationAndGoldenArtifactsAreStable()
    {
        string root = Path.Combine(Path.GetTempPath(), "portal-remap-map-v1-" + Guid.NewGuid().ToString("N"));
        string first = Path.Combine(root, "first");
        string second = Path.Combine(root, "second");
        try
        {
            FormalTransportObservationSnapshotV1 snapshot = Snapshot(true,
                Pixel(0, 1, true, false, "provisional_event_coverage",
                    TransportEventKindV1.BoundaryRemapApplied,
                    TransportEventKindV1.BoundarySyntheticEgress,
                    TransportEventKindV1.BoundaryRemapApplied,
                    TransportEventKindV1.SuppressedMaskTransition,
                    TransportEventKindV1.GeometryContactObserved),
                Pixel(1, 1, true, false, "provisional_event_coverage"),
                Pixel(2, 1, true, false, "provisional_event_coverage", TransportEventKindV1.BoundaryRemapApplied),
                Pixel(3, 1, true, false, "provisional_event_coverage",
                    TransportEventKindV1.BoundaryRemapApplied,
                    TransportEventKindV1.BoundaryRemapApplied,
                    TransportEventKindV1.BoundaryRemapApplied));
            PortalRemapObservationInputV1 input = Input(first, snapshot);
            TestAssert.True(PortalRemapObservationMapV1.TryWrite(input, out PortalRemapObservationResultV1? a, out string reasonA), reasonA);
            TestAssert.True(PortalRemapObservationMapV1.TryWrite(Input(second, snapshot), out PortalRemapObservationResultV1? b, out string reasonB), reasonB);

            TestAssert.True(a!.Counts.SequenceEqual(new[] { 2, 0, 1, 3 }), "only BoundaryRemapApplied counted in row-major order");
            TestAssert.True(a.Statuses.All(status => status == PortalRemapPixelStatusV1.RecordedCompleteForAttempt), "complete attempts numeric");
            byte[] countBytes = File.ReadAllBytes(Path.Combine(first, "portal_remap_count.bin"));
            TestAssert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(countBytes.AsSpan(0, 4)), "top-left count");
            TestAssert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(countBytes.AsSpan(4, 4)), "top-right count");
            TestAssert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(countBytes.AsSpan(8, 4)), "bottom-left count");
            TestAssert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(countBytes.AsSpan(12, 4)), "bottom-right count");

            foreach (string name in new[] { "portal_remap_count.bin", "portal_remap_status.bin", "portal_remap_count.png", "portal_remap_legend.json", "portal_remap_manifest.json" })
                TestAssert.True(File.ReadAllBytes(Path.Combine(first, name)).SequenceEqual(File.ReadAllBytes(Path.Combine(second, name))), $"deterministic repeated export {name}");

            TestAssert.Equal("7d882e1cd8f1f0b5929481976c2259c82b58eceee353c6f08fdf1798b65a4b18", Hex(countBytes), "golden count bytes");
            TestAssert.Equal("25a5ceb2d1c7459db8847a83cca6032ede031e5068f7b091a97650be7499c7b5", Hex(File.ReadAllBytes(Path.Combine(first, "portal_remap_count.png"))), "golden PNG bytes");
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(a.ManifestPath));
            TestAssert.Equal("provisional_recorded_observation", manifest.RootElement.GetProperty("artifact_class").GetString(), "provisional artifact class");
            TestAssert.False(manifest.RootElement.GetProperty("recorder").GetProperty("scientific_ledger_sealed").GetBoolean(), "not scientifically sealed");
            TestAssert.Equal(6, manifest.RootElement.GetProperty("summary").GetProperty("recorded_portal_remaps").GetInt32(), "exact remap total");
            TestAssert.Equal(a.ProvisionalRecorderArtifactSha256, b!.ProvisionalRecorderArtifactSha256, "snapshot fingerprint stable");
            RetainArtifacts(first, "complete");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void IncompleteStatesNeverBecomeZero()
    {
        string root = Path.Combine(Path.GetTempPath(), "portal-remap-status-v1-" + Guid.NewGuid().ToString("N"));
        try
        {
            FormalTransportObservationSnapshotV1 snapshot = Snapshot(true,
                Pixel(0, 1, false, false, "provisional_event_coverage", TransportEventKindV1.BoundaryRemapApplied),
                Pixel(1, 1, true, true, "provisional_event_coverage;recorder_event_capacity_overflow", TransportEventKindV1.BoundaryRemapApplied),
                Pixel(2, 1, true, false, "provisional_event_coverage;recorder_failure", TransportEventKindV1.BoundaryRemapApplied));
            TestAssert.True(PortalRemapObservationMapV1.TryWrite(Input(root, snapshot), out PortalRemapObservationResultV1? result, out string reason), reason);
            TestAssert.True(result!.Counts.All(count => count == PortalRemapObservationMapV1.IncompleteCount), "all incomplete numeric storage uses signed -1 sentinel");
            TestAssert.True(result.Statuses.SequenceEqual(new[]
            {
                PortalRemapPixelStatusV1.AttemptUnfinalized,
                PortalRemapPixelStatusV1.RecorderOverflow,
                PortalRemapPixelStatusV1.RecorderFailedOrIncomplete,
                PortalRemapPixelStatusV1.RecorderDisabledOrMissing
            }), "all incomplete states explicit");
            byte[] countBytes = File.ReadAllBytes(Path.Combine(root, "portal_remap_count.bin"));
            for (int i = 0; i < 4; i++)
                TestAssert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(countBytes.AsSpan(i * 4, 4)), $"incomplete signed sentinel {i}");
            TestAssert.True(File.ReadAllBytes(Path.Combine(root, "portal_remap_status.bin")).SequenceEqual(new byte[] { 2, 3, 4, 1 }), "incomplete status bytes remain distinct");
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "portal_remap_manifest.json")));
            TestAssert.Equal(0, manifest.RootElement.GetProperty("summary").GetProperty("recorded_portal_remaps").GetInt32(), "incomplete counts excluded from total");
            using JsonDocument legend = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "portal_remap_legend.json")));
            TestAssert.Equal(-1, legend.RootElement.GetProperty("count_binary").GetProperty("incomplete_sentinel").GetInt32(), "legend pins signed sentinel");
            RetainArtifacts(root, "incomplete");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void SnapshotCoherenceIsReportedWithoutIndependentAuthorityClaims()
    {
        string root = Path.Combine(Path.GetTempPath(), "portal-remap-stale-v1-" + Guid.NewGuid().ToString("N"));
        try
        {
            FormalTransportObservationSnapshotV1 current = Snapshot(true,
                Pixel(0, 2, true, false, "provisional_event_coverage", TransportEventKindV1.BoundaryRemapApplied));
            TestAssert.True(PortalRemapObservationMapV1.TryWrite(Input(Path.Combine(root, "current"), current), out PortalRemapObservationResultV1? accepted, out string currentReason), currentReason);
            TestAssert.Equal(PortalRemapPixelStatusV1.RecordedCompleteForAttempt, accepted!.Statuses[0], "current immutable attempt accepted");

            var wrongGenerationPixel = new FormalTransportObservationPixelV1(
                0, 2, 1, true, TransportRecorderCompletionV1.Failed,
                "provisional_event_coverage", 8, false,
                new[] { TransportEventKindV1.BoundaryRemapApplied });
            FormalTransportObservationSnapshotV1 incoherent = Snapshot(true, 1, wrongGenerationPixel);
            TestAssert.True(PortalRemapObservationMapV1.TryWrite(Input(Path.Combine(root, "incoherent"), incoherent), out PortalRemapObservationResultV1? rejected, out string mismatchReason), mismatchReason);
            TestAssert.Equal(PortalRemapPixelStatusV1.SnapshotCoherenceFailure, rejected!.Statuses[0], "internal snapshot generation inconsistency reported");
            TestAssert.Equal(PortalRemapObservationMapV1.IncompleteCount, rejected.Counts[0], "coherence failure retains -1 sentinel");

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(accepted.ManifestPath));
            JsonElement coherence = manifest.RootElement.GetProperty("acquisition").GetProperty("capture_coherence");
            TestAssert.False(coherence.GetProperty("independent_generation_authority").GetBoolean(), "no independent generation authority claimed");
            TestAssert.False(coherence.GetProperty("independent_commit_version_authority").GetBoolean(), "no independent commit-version authority claimed");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void NonUnitFilmStrideFailsClosed()
    {
        string root = Path.Combine(Path.GetTempPath(), "portal-remap-stride-v1-" + Guid.NewGuid().ToString("N"));
        try
        {
            PortalRemapObservationInputV1 input = Input(root, Snapshot(true));
            input = CopyWithStride(input, 2);
            TestAssert.False(PortalRemapObservationMapV1.TryWrite(input, out _, out string reason), "non-unit film stride rejected");
            TestAssert.Equal("film_stride_unsupported", reason, "non-unit stride reason");
            TestAssert.False(Directory.Exists(root), "unsupported stride writes no artifact");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void RecorderOffIsIsolated()
    {
        string root = Path.Combine(Path.GetTempPath(), "portal-remap-off-v1-" + Guid.NewGuid().ToString("N"));
        try
        {
            FormalTransportObservationSnapshotV1 off = Snapshot(false);
            TestAssert.True(PortalRemapObservationMapV1.TryWrite(Input(root, off), out PortalRemapObservationResultV1? result, out string reason), reason);
            TestAssert.True(result!.Statuses.All(status => status == PortalRemapPixelStatusV1.RecorderDisabledOrMissing), "recorder OFF remains missing, never zero evidence");
            TestAssert.True(result.Counts.All(count => count == PortalRemapObservationMapV1.IncompleteCount), "OFF count buffer uses incomplete sentinel");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void SnapshotOwnsImmutableCopies()
    {
        TransportEventKindV1[] kinds = { TransportEventKindV1.BoundaryRemapApplied };
        var pixel = new FormalTransportObservationPixelV1(0, 1, 1, true, TransportRecorderCompletionV1.Failed, "provisional_event_coverage", 8, false, kinds);
        FormalTransportObservationPixelV1[] pixels = { pixel };
        var snapshot = new FormalTransportObservationSnapshotV1(1, true, 1, 1, 1, 1, 0, pixels);
        kinds[0] = TransportEventKindV1.GeometryContactObserved;
        pixels[0] = Pixel(1, 1, true, false, "provisional_event_coverage");
        TestAssert.Equal(TransportEventKindV1.BoundaryRemapApplied, snapshot.Pixels[0].EventKinds[0], "event source array copied");
        TestAssert.Equal(0, snapshot.Pixels[0].PixelIndex, "pixel source array copied");
    }

    private static FormalTransportObservationPixelV1 Pixel(
        int index, uint version, bool finalized, bool overflowed, string reason, params TransportEventKindV1[] kinds) =>
        new(index, 1, version, finalized, TransportRecorderCompletionV1.Failed, reason, System.Math.Max(8, kinds.Length), overflowed, kinds);

    private static FormalTransportObservationSnapshotV1 Snapshot(bool enabled, params FormalTransportObservationPixelV1[] pixels) =>
        Snapshot(enabled, 1, pixels);

    private static FormalTransportObservationSnapshotV1 Snapshot(bool enabled, ulong generation, params FormalTransportObservationPixelV1[] pixels) =>
        new(generation, enabled, pixels.Length, pixels.Sum(pixel => pixel.EventKinds.Count), pixels.Length, pixels.Count(pixel => pixel.Finalized), 0, pixels);

    private static PortalRemapObservationInputV1 Input(
        string output, FormalTransportObservationSnapshotV1 snapshot) => new()
    {
        OutputDirectory = output,
        RunId = "portal-map-test",
        SemanticSceneId = "test.portal-map",
        GodotScenePath = "res://Tests/PortalMap.tscn",
        FormalPolicyId = "cathedral.formal.v1",
        FormalPolicySha256 = Sha,
        GeometrySnapshotSha256 = Sha,
        FrozenBoundaryContentSha256 = Sha,
        BoundaryExecutionAuthoritySha256 = Sha,
        CameraPose = new CameraPoseBitsV1(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13),
        Width = 2,
        Height = 2,
        FilmStride = 1,
        CanonicalQueryDatasetSha256 = Sha,
        EngineCommit = "test-commit",
        SourceTreeState = "dirty",
        AuthoredDiffSha256 = Sha,
        Snapshot = snapshot
    };

    private static PortalRemapObservationInputV1 CopyWithStride(PortalRemapObservationInputV1 input, int stride) => new()
    {
        OutputDirectory = input.OutputDirectory, RunId = input.RunId, SemanticSceneId = input.SemanticSceneId,
        GodotScenePath = input.GodotScenePath, FormalPolicyId = input.FormalPolicyId, FormalPolicySha256 = input.FormalPolicySha256,
        GeometrySnapshotSha256 = input.GeometrySnapshotSha256, FrozenBoundaryContentSha256 = input.FrozenBoundaryContentSha256,
        BoundaryExecutionAuthoritySha256 = input.BoundaryExecutionAuthoritySha256, CameraPose = input.CameraPose,
        Width = input.Width, Height = input.Height, FilmStride = stride,
        CanonicalQueryDatasetSha256 = input.CanonicalQueryDatasetSha256, EngineCommit = input.EngineCommit,
        SourceTreeState = input.SourceTreeState, AuthoredDiffSha256 = input.AuthoredDiffSha256, Snapshot = input.Snapshot
    };

    private static string Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RetainArtifacts(string source, string name)
    {
        string? evidenceRoot = Environment.GetEnvironmentVariable("XPRIMERAY_PORTAL_GOLDEN_OUTPUT");
        if (string.IsNullOrWhiteSpace(evidenceRoot)) return;
        string target = Path.Combine(evidenceRoot, name);
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
    }
}
