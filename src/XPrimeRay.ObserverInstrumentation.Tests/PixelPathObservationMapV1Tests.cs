using System.Text.Json;
using XPrimeRay.ObservationLayer;

namespace XPrimeRay.ObserverInstrumentation.Tests;

/// <summary>INSPECT-R0.1: ordered per-pixel event export — content, determinism and fail-closed refusals.</summary>
internal static class PixelPathObservationMapV1Tests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string QuerySha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const ulong Generation = 7;
    private const string EngineCommit = "e1879170d628826bb323b1f78a4004ac98ff6f9a";

    public static void Run()
    {
        HappyPathContentAndUnavailableDetails();
        Determinism();
        EveryRefusalWritesNothing();
        ExistingDestinationIsRefusedAndUntouched();
        InjectedFilesystemFailuresLeaveNothing();
    }

    private static void HappyPathContentAndUnavailableDetails()
    {
        using TempDir root = new();
        PixelPathObservationInputV1 input = Build(root.Out);
        TestAssert.True(PixelPathObservationMapV1.TryWrite(input, out PixelPathObservationResultV1? result, out string reason), "happy path: " + reason);
        TestAssert.Equal(2, result!.ValidatedPixelCount, "every sealed pixel validated");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(result.PixelPathsPath));
        JsonElement pixel = doc.RootElement.GetProperty("pixels")[0];
        JsonElement recorded = pixel.GetProperty("recorded");
        TestAssert.Equal(0, pixel.GetProperty("pixel_index").GetInt32(), "selected pixel exported");
        TestAssert.Equal(4, recorded.GetProperty("events").GetArrayLength(), "all ordered events exported");
        TestAssert.Equal(2, recorded.GetProperty("queries").GetArrayLength(), "all recorded queries exported");
        int ordinal = 0;
        foreach (JsonElement e in recorded.GetProperty("events").EnumerateArray())
            TestAssert.Equal(ordinal++, e.GetProperty("event_ordinal").GetInt32(), "recorder execution order preserved");
        JsonElement contact = recorded.GetProperty("events")[2];
        TestAssert.Equal("GeometryContactObserved", contact.GetProperty("kind").GetString(), "contact event kind");
        foreach (string key in new[] { "pre_position_bits", "post_position_bits", "contact_normal_bits", "path_distance_bits" })
            TestAssert.False(contact.TryGetProperty(key, out _), "unrecorded contact detail is not fabricated: " + key);
        TestAssert.Equal(1, contact.GetProperty("derived_contact_question_ordinal").GetInt32(), "contact joined to its question");
        foreach (JsonElement q in recorded.GetProperty("queries").EnumerateArray())
            TestAssert.Equal("unavailable", q.GetProperty("authority_disposition").GetString(), "per-question authority marked unavailable");
        TestAssert.True(pixel.GetProperty("unavailable").GetArrayLength() >= 6, "unavailable channels listed");
        TestAssert.Equal(1, pixel.GetProperty("derived").GetProperty("contact_event_count").GetInt32(), "derived contact count");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath));
        JsonElement totals = manifest.RootElement.GetProperty("validation").GetProperty("totals");
        TestAssert.Equal(3, totals.GetProperty("question_events").GetInt32(), "full-film question total");
        TestAssert.Equal(1, totals.GetProperty("sealed_bvh_contacts").GetInt32(), "full-film sealed contact total");
        TestAssert.False(manifest.RootElement.GetProperty("scientific_ledger_sealed").GetBoolean(), "export is not a sealed ledger");
        foreach (JsonElement q in recorded.GetProperty("queries").EnumerateArray())
            TestAssert.False(q.TryGetProperty("chord_length_bits", out _), "chord length is not labelled recorded");
        TestAssert.Equal(2, pixel.GetProperty("derived").GetProperty("query_chord_length_bits").GetArrayLength(), "chord length is exported as derived, one per query");
        TestAssert.Equal(EngineCommit, manifest.RootElement.GetProperty("source").GetProperty("engine_commit").GetString(), "source commit exported");
        TestAssert.Equal(0, Directory.GetDirectories(root.Path, ".out.staging-*").Length, "no staging directory left after success");
    }

    private static void Determinism()
    {
        using TempDir a = new();
        using TempDir b = new();
        TestAssert.True(PixelPathObservationMapV1.TryWrite(Build(a.Out), out PixelPathObservationResultV1? ra, out string r1), r1);
        TestAssert.True(PixelPathObservationMapV1.TryWrite(Build(b.Out), out PixelPathObservationResultV1? rb, out string r2), r2);
        TestAssert.True(File.ReadAllBytes(ra!.PixelPathsPath).SequenceEqual(File.ReadAllBytes(rb!.PixelPathsPath)), "pixel_paths.json byte-identical");
        TestAssert.True(File.ReadAllBytes(ra.ManifestPath).SequenceEqual(File.ReadAllBytes(rb.ManifestPath)), "manifest byte-identical");
        TestAssert.Equal(ra.FullFilmDigestSha256, rb.FullFilmDigestSha256, "full-film digest identical");
    }

    private static void EveryRefusalWritesNothing()
    {
        var cases = new (string Expected, Func<string, PixelPathObservationInputV1> Make)[]
        {
            ("recorder_disabled_for_acquisition", o => Build(o, recorderEnabled: false)),
            ("sealed_measurements_missing", o => Build(o, omitMeasurements: true)),
            ("live_capture_generation_mismatch", o => Build(o, liveGeneration: Generation + 1)),
            ("live_query_dataset_mismatch", o => Build(o, liveQuerySha: Sha)),
            ("pixel_selection_empty", o => Build(o, selection: Array.Empty<int>())),
            ("pixel_selection_duplicate", o => Build(o, selection: new[] { 0, 0 })),
            ("pixel_selection_out_of_range", o => Build(o, selection: new[] { 2 })),
            ("dirty_source_requires_fingerprint", o => Build(o, authoredDiff: "")),
            ("dirty_source_placeholder_fingerprint", o => Build(o, authoredDiff: new string('0', 64))),
            ("clean_source_must_not_carry_fingerprint", o => Build(o, treeState: "clean")),
            ("source_state_unverified", o => Build(o, treeState: "unknown")),
            ("engine_commit_invalid", o => Build(o, engineCommit: "81dbb1ad")),
            ("engine_commit_invalid", o => Build(o, engineCommit: "working-tree")),
            ("pixel_selection_too_large", o => Build(o, selection: Enumerable.Range(0, PixelPathObservationMapV1.MaxSelectedPixels + 1).ToArray())),
            ("contact_question_association_failed:0", o => Build(o, contactStep: 2, contactSealedSegment: 0, contactSegment: 0, sealedFirst0: 2)),
            ("pixel_coverage_incomplete", o => Build(o, dropLivePixel1: true)),
            ("pixel_commit_mismatch:0", o => Build(o, liveCommit: 2)),
            ("pixel_unfinalized:0", o => Build(o, liveFinalized: false)),
            ("pixel_recorder_overflow:0", o => Build(o, liveOverflowed: true)),
            ("pixel_recorder_incomplete:0", o => Build(o, sealedReason: "provisional_event_coverage;recorder_failure")),
            ("pixel_terminal_inputs_mismatch:0", o => Build(o, liveTraveled: 9f)),
            ("pixel_event_sequence_mismatch:0", o => Build(o, swapLiveEvents: true)),
            ("question_join_mismatch:0", o => Build(o, liveQueryStepOffset: 1)),
            ("contact_join_mismatch:0", o => Build(o, sealedContact0: 0)),
            ("contact_join_mismatch:0", o => Build(o, sealedFirst0: 2)),
            ("contact_question_association_failed:0", o => Build(o, contactSegment: 5)),
            ("coverage_reconciliation_mismatch:0", o => Build(o, corruptCoverage: true)),
            ("terminal_mismatch:1", o => Build(o, dropTerminalPixel1: true)),
            ("input_missing", o => Build(o, nullSources: true)),
        };
        foreach ((string expected, Func<string, PixelPathObservationInputV1> make) in cases)
        {
            using TempDir root = new();
            bool ok = PixelPathObservationMapV1.TryWrite(make(root.Out), out _, out string reason);
            TestAssert.False(ok, "refused: " + expected);
            TestAssert.Equal(expected, reason, "refusal reason");
            TestAssert.False(Directory.Exists(root.Out), "refusal wrote nothing: " + expected);
        }
    }

    // Synthetic sealed acquisition, 2x1 film:
    // pixel 0: Q(step2,seg0) Q(step4,seg1) Contact(step4,seg1) Terminal(MaxSteps, step5); sealed contacts 1, first step 4.
    // pixel 1: Q(step2,seg0) Terminal(MaxSteps, step5); no contact.
    private static void ExistingDestinationIsRefusedAndUntouched()
    {
        using TempDir root = new();
        Directory.CreateDirectory(root.Out);
        File.WriteAllText(System.IO.Path.Combine(root.Out, "pixel_path_manifest.json"), "old");
        TestAssert.False(PixelPathObservationMapV1.TryWrite(Build(root.Out), out _, out string reason), "existing destination refused");
        TestAssert.Equal("destination_exists", reason, "existing destination reason");
        TestAssert.Equal("old", File.ReadAllText(System.IO.Path.Combine(root.Out, "pixel_path_manifest.json")), "existing destination untouched");
        TestAssert.Equal(1, Directory.GetFiles(root.Out).Length, "nothing added to existing destination");
    }

    private static void InjectedFilesystemFailuresLeaveNothing()
    {
        foreach (string stage in new[] { "staged_paths", "staged_manifest", "verified", "before_publish" })
        {
            using TempDir root = new();
            PixelPathObservationInputV1 input = Build(root.Out, fault: s => { if (s == stage) throw new IOException("injected failure at " + s); });
            TestAssert.False(PixelPathObservationMapV1.TryWrite(input, out _, out string reason), "injected failure refused: " + stage);
            TestAssert.Equal("publication_failed:IOException", reason, "injected failure reason: " + stage);
            TestAssert.False(Directory.Exists(root.Out), "no destination after failure: " + stage);
            TestAssert.Equal(0, Directory.GetDirectories(root.Path).Length, "no staging directory left after failure: " + stage);
            TestAssert.Equal(0, Directory.GetFiles(root.Path).Length, "no stray file after failure: " + stage);
        }
    }

    private static PixelPathObservationInputV1 Build(
        string output,
        bool recorderEnabled = true, bool omitMeasurements = false, ulong liveGeneration = Generation, string liveQuerySha = QuerySha,
        int[]? selection = null, string authoredDiff = Sha, bool dropLivePixel1 = false, uint liveCommit = 1, bool liveFinalized = true,
        bool liveOverflowed = false, string sealedReason = "provisional_event_coverage", float liveTraveled = 8f, bool swapLiveEvents = false,
        int liveQueryStepOffset = 0, int sealedContact0 = 1, int sealedFirst0 = 4, int contactSegment = 1, bool corruptCoverage = false,
        bool dropTerminalPixel1 = false, bool nullSources = false, string treeState = "dirty", string engineCommit = EngineCommit,
        int contactStep = 4, int contactSealedSegment = 1, Action<string>? fault = null)
    {
        TransportEventV1 Q(int ordinal, int step, int segment) => new(ordinal, step, segment, TransportEventKindV1.ProbeQuestionIssued,
            TransportSubjectKindV1.None, CanonicalIdentityAvailabilityV1.NotApplicable, null, -1, TransportObservabilityV1.PointSample,
            TransportEventPresenceV1.None, TransportDecisionV1.QueryIssued, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None,
            default, default, default, default, default, 0, 0);
        TransportEventV1 C(int ordinal, int step, int segment) => new(ordinal, step, segment, TransportEventKindV1.GeometryContactObserved,
            TransportSubjectKindV1.GeometryPrimitive, CanonicalIdentityAvailabilityV1.Unavailable, null, -1, TransportObservabilityV1.DiagnosticGodotWitness,
            TransportEventPresenceV1.None, TransportDecisionV1.NoTransportEffect, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None,
            default, default, default, default, default, 0, 0);
        TransportEventV1 T(int ordinal, int step) => new(ordinal, step, -1, TransportEventKindV1.Terminal,
            TransportSubjectKindV1.None, CanonicalIdentityAvailabilityV1.NotApplicable, null, -1, TransportObservabilityV1.None,
            TransportEventPresenceV1.None, TransportDecisionV1.TransportTerminated, TransportReasonV1.MaxSteps, TransportEventFlagsV1.None,
            default, default, default, default, default, 0, 0);
        PixelPathQueryV1 Query(int step, float traveledB, float chord) => new(step, RawFloat3BitsV1.FromSingle(0f, 0f, traveledB - chord),
            RawFloat3BitsV1.FromSingle(0f, 0f, traveledB), BitConverter.SingleToUInt32Bits(traveledB), BitConverter.SingleToUInt32Bits(chord), 1, true, true, true, false);

        var events0 = new[] { Q(0, 2, 0), Q(1, 4, 1), C(2, contactStep, contactSealedSegment), T(3, 5) };
        var liveEvents0 = new[] { Q(0, 2, 0), Q(1, 4, 1), C(2, contactStep, contactSegment), T(3, 5) };
        if (swapLiveEvents) liveEvents0 = new[] { Q(0, 2, 0), C(1, 4, 1), Q(2, 4, 1), T(3, 5) };
        TransportEventV1[] events1 = dropTerminalPixel1 ? new[] { Q(0, 2, 0) } : new[] { Q(0, 2, 0), T(1, 5) };
        var queries0 = new[] { Query(2, 2f, 1f), Query(4, 4f, 1f) };
        var queries1 = new[] { Query(2, 2f, 1f) };
        var liveQueries0 = new[] { Query(2 + liveQueryStepOffset, 2f, 1f), Query(4, 4f, 1f) };

        FormalTransportPixelMeasurementV1 Measure(int pixel, TransportEventV1[] events, PixelPathQueryV1[] queries)
        {
            FormalQueryCoverageSummaryV1 coverage = FormalQueryCoverageV1.Measure(8f, queries.Select(q =>
                new FormalQuerySegmentV1(q.ProbeStep, BitConverter.UInt32BitsToSingle(q.TraveledBBits), BitConverter.UInt32BitsToSingle(q.ChordLengthBits))));
            uint chordBits = corruptCoverage && pixel == 0 ? coverage.QueriedChordBits + 1 : coverage.QueriedChordBits;
            return new FormalTransportPixelMeasurementV1(pixel, Generation, 1,
                dropTerminalPixel1 && pixel == 1 ? Pass1TransportTerminationV1.MaxSteps : Pass1TransportTerminationV1.MaxSteps,
                RawTerminalFlagsV1.MaxStepsReached, 5, BitConverter.SingleToUInt32Bits(8f), queries.Length, chordBits, coverage.MaxUnqueriedGapBits,
                queries.Select(q => q.ProbeStep), events.Select(e => new FormalTransportEventMeasurementV1(e.EventOrdinal, e.IntegrationStepOrdinal, e.Kind, e.Reason)));
        }
        PixelPathSourceV1 Live(int pixel, TransportEventV1[] events, PixelPathQueryV1[] queries, uint commit, bool finalized, bool overflowed, float traveled) => new()
        {
            PixelIndex = pixel, CaptureGeneration = Generation, CommitVersion = commit, Finalized = finalized, Overflowed = overflowed,
            EventCapacity = 32, ProvisionalReason = "provisional_event_coverage", Termination = Pass1TransportTerminationV1.MaxSteps,
            RawTerminalFlags = RawTerminalFlagsV1.MaxStepsReached, StepsIntegrated = 5, TraveledBits = BitConverter.SingleToUInt32Bits(traveled),
            Found = false, QuestionCount = queries.Length, Events = events, Queries = queries
        };

        var sealedPixels = new[]
        {
            new FormalTransportObservationPixelV1(0, Generation, 1, true, TransportRecorderCompletionV1.Failed, sealedReason, 32, false, events0.Select(e => e.Kind)),
            new FormalTransportObservationPixelV1(1, Generation, 1, true, TransportRecorderCompletionV1.Failed, "provisional_event_coverage", 32, false, events1.Select(e => e.Kind))
        };
        var measurements = new FormalTransportPixelMeasurementsV1(new[] { Measure(0, events0, queries0), Measure(1, events1, queries1) });
        var snapshot = new FormalTransportObservationSnapshotV1(Generation, recorderEnabled, 2, events0.Length + events1.Length, 2, 2, 0, sealedPixels,
            omitMeasurements ? null : measurements);
        var sources = new List<PixelPathSourceV1>
        {
            Live(0, liveEvents0, liveQueries0, liveCommit, liveFinalized, liveOverflowed, liveTraveled == 8f ? 8f : liveTraveled)
        };
        if (!dropLivePixel1) sources.Add(Live(1, events1, queries1, 1, true, false, 8f));
        return new PixelPathObservationInputV1
        {
            OutputDirectory = output, RunId = "unit", SemanticSceneId = "scene", GodotScenePath = "res://scene.tscn",
            FormalPolicyId = "policy", FormalPolicySha256 = Sha, GeometrySnapshotSha256 = Sha, FrozenBoundaryContentSha256 = Sha,
            BoundaryExecutionAuthoritySha256 = Sha, CameraPose = new CameraPoseBitsV1(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 55),
            Width = 2, Height = 1, FilmStride = 1, CanonicalQueryDatasetSha256 = QuerySha, LiveQueryDatasetSha256 = liveQuerySha,
            LiveCaptureGeneration = liveGeneration, EngineCommit = engineCommit, SourceTreeState = treeState, AuthoredDiffSha256 = authoredDiff,
            Snapshot = snapshot, ContactCounts = new[] { sealedContact0, 0 }, FirstContactSteps = new[] { sealedContact0 == 0 ? sealedFirst0 : sealedFirst0, -1 },
            Sources = nullSources ? null! : sources, SelectedPixels = selection ?? new[] { 0 },
            PublicationFaultInjectionForTesting = fault
        };
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xprimeray-pixelpath-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public string Out => System.IO.Path.Combine(Path, "out");
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }
}
