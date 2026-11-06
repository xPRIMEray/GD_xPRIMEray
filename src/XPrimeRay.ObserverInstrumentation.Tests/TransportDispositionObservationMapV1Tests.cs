using System.Buffers.Binary;
using System.Security.Cryptography;
using XPrimeRay.ObservationLayer;

namespace XPrimeRay.ObserverInstrumentation.Tests;

internal static class TransportDispositionObservationMapV1Tests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static void Run()
    {
        ConstructorCompatibilityAndCoverage();
        AllDispositionsAndOrdering();
        StatusAndCoherenceFailures();
        EveryFailClosedStatus();
        DeterministicGoldenAndOrientation();
        StrideFailsClosed();
    }

    private static void ConstructorCompatibilityAndCoverage()
    {
        var pixel = Pixel(0, 1, true, false, "provisional_event_coverage", TransportEventKindV1.Terminal);
        var legacy = new FormalTransportObservationSnapshotV1(1, true, 1, 1, 1, 1, 0, new[] { pixel });
        TestAssert.True(legacy.Measurements is null, "existing snapshot constructor remains compatible");
        FormalQueryCoverageSummaryV1 coverage = FormalQueryCoverageV1.Measure(10f, new[]
        {
            new FormalQuerySegmentV1(1, 3f, 2f),
            new FormalQuerySegmentV1(2, 7f, 3f)
        });
        TestAssert.NearlyEqual(5f, BitConverter.UInt32BitsToSingle(coverage.QueriedChordBits), 1e-6f, "query chord union");
        TestAssert.NearlyEqual(3f, BitConverter.UInt32BitsToSingle(coverage.MaxUnqueriedGapBits), 1e-6f, "maximum unqueried gap");
    }

    private static void AllDispositionsAndOrdering()
    {
        var specs = new[]
        {
            Spec(0, RawTerminalFlagsV1.ProbeQueryFailed | RawTerminalFlagsV1.MaxStepsReached, Pass1TransportTerminationV1.MaxSteps, 1, 0),
            Spec(1, RawTerminalFlagsV1.MaxStepsReached, Pass1TransportTerminationV1.MaxSteps, 1, 1, remapBefore: true, remapAfter: true),
            Spec(2, RawTerminalFlagsV1.Absorbed, Pass1TransportTerminationV1.AbsorbedAtSample, 1, 0),
            Spec(3, RawTerminalFlagsV1.SegmentCapacityTruncated, Pass1TransportTerminationV1.SegmentCapacityTruncated, 1, 0),
            Spec(4, RawTerminalFlagsV1.MaxStepsReached, Pass1TransportTerminationV1.MaxSteps, 0, 0),
            Spec(5, RawTerminalFlagsV1.MaxStepsReached, Pass1TransportTerminationV1.MaxSteps, 1, 0),
            Spec(6, RawTerminalFlagsV1.MaxDistanceReached, Pass1TransportTerminationV1.MaxDistance, 1, 0)
        };
        using TempDir root = new();
        TransportDispositionObservationInputV1 input = Input(root.Path, specs);
        TestAssert.True(TransportDispositionObservationMapV1.TryWrite(input, out TransportDispositionObservationResultV1? result, out string reason), reason);
        TransportDispositionV1[] expected =
        {
            TransportDispositionV1.FaultNumericalOrQuery,
            TransportDispositionV1.FirstContactObserved_Unqualified,
            TransportDispositionV1.AbsorbedAtSample,
            TransportDispositionV1.SegmentCapacityTruncated,
            TransportDispositionV1.NoProbePerformed,
            TransportDispositionV1.NoContact_BudgetExhausted,
            TransportDispositionV1.NoContact_RangeExhausted
        };
        TestAssert.True(result!.Dispositions.SequenceEqual(expected), "all disposition precedence classes");
        int[] first = ReadInt32(Path.Combine(root.Path, "first_contact_step.bin"));
        int[] remaps = ReadInt32(Path.Combine(root.Path, "remaps_before_first_contact.bin"));
        TestAssert.Equal(2, first[1], "first contact step retained");
        TestAssert.Equal(1, remaps[1], "same-path remap ordering uses captured event ordinal");
        TestAssert.Equal(TransportDispositionObservationMapV1.CompleteNotApplicable, first[0], "no-contact first-step sentinel");
        TestAssert.Equal(TransportDispositionObservationMapV1.CompleteNotApplicable, remaps[0], "no-contact remap sentinel");
    }

    private static void StatusAndCoherenceFailures()
    {
        PixelSpec valid = Spec(0, RawTerminalFlagsV1.MaxStepsReached, Pass1TransportTerminationV1.MaxSteps, 1, 0);
        using TempDir missingRoot = new();
        TransportDispositionObservationInputV1 missing = Input(missingRoot.Path, new[] { valid }, omitMeasurements: true);
        TestAssert.True(TransportDispositionObservationMapV1.TryWrite(missing, out TransportDispositionObservationResultV1? missingResult, out string missingReason), missingReason);
        TestAssert.Equal(TransportDispositionPixelStatusV1.MeasurementsMissing, missingResult!.Statuses[0], "missing measurements fail closed");

        using TempDir contactRoot = new();
        TransportDispositionObservationInputV1 contact = Input(contactRoot.Path, new[] { valid });
        contact.ContactCounts[0] = 1;
        contact.FirstContactSteps[0] = 1;
        TestAssert.True(TransportDispositionObservationMapV1.TryWrite(contact, out TransportDispositionObservationResultV1? contactResult, out string contactReason), contactReason);
        TestAssert.Equal(TransportDispositionPixelStatusV1.ContactChannelMismatch, contactResult!.Statuses[0], "per-pixel sealed contact mismatch");

        PixelSpec replacement = Spec(0, RawTerminalFlagsV1.MaxStepsReached, Pass1TransportTerminationV1.MaxSteps, 1, 0) with { CommitVersion = 2 };
        using TempDir replacementRoot = new();
        TestAssert.True(TransportDispositionObservationMapV1.TryWrite(Input(replacementRoot.Path, new[] { replacement }), out TransportDispositionObservationResultV1? replacementResult, out string replacementReason), replacementReason);
        TestAssert.Equal(TransportDispositionPixelStatusV1.RecordedCompleteForAttempt, replacementResult!.Statuses[0], "commit-version-2 current replacement accepted");

        PixelSpec overflow = valid with { Overflowed = true, Reason = "provisional_event_coverage;recorder_event_capacity_overflow" };
        using TempDir overflowRoot = new();
        TestAssert.True(TransportDispositionObservationMapV1.TryWrite(Input(overflowRoot.Path, new[] { overflow }), out TransportDispositionObservationResultV1? overflowResult, out string overflowReason), overflowReason);
        TestAssert.Equal(TransportDispositionPixelStatusV1.RecorderOverflow, overflowResult!.Statuses[0], "overflow status");
        TestAssert.Equal(TransportDispositionObservationMapV1.IncompleteInteger, ReadInt32(Path.Combine(overflowRoot.Path, "question_count.bin"))[0], "incomplete integer sentinel");

        PixelSpec contactAndAbsorbed = Spec(0, RawTerminalFlagsV1.Absorbed, Pass1TransportTerminationV1.AbsorbedAtSample, 1, 1);
        using TempDir precedenceRoot = new();
        TestAssert.True(TransportDispositionObservationMapV1.TryWrite(Input(precedenceRoot.Path, new[] { contactAndAbsorbed }), out TransportDispositionObservationResultV1? precedence, out string precedenceReason), precedenceReason);
        TestAssert.Equal(TransportDispositionV1.FirstContactObserved_Unqualified, precedence!.Dispositions[0], "contact precedes absorbed disposition without becoming a terminal-hit claim");
    }

    private static void EveryFailClosedStatus()
    {
        using TempDir root = new();
        FormalTransportEventMeasurementV1 question = Event(0, 1, TransportEventKindV1.ProbeQuestionIssued, TransportReasonV1.ProbeCadence);
        FormalTransportEventMeasurementV1 terminal = Event(1, 2, TransportEventKindV1.Terminal, TransportReasonV1.MaxSteps);
        FormalTransportPixelMeasurementV1 GoodMeasurement(ulong generation = 1, uint version = 1, int questionCount = 1, int[]? querySteps = null, FormalTransportEventMeasurementV1[]? events = null) =>
            new(0, generation, version, Pass1TransportTerminationV1.MaxSteps, RawTerminalFlagsV1.MaxStepsReached, 2,
                BitConverter.SingleToUInt32Bits(2f), questionCount, BitConverter.SingleToUInt32Bits(1f), BitConverter.SingleToUInt32Bits(1f),
                querySteps ?? new[] { 1 }, events ?? new[] { question, terminal });
        FormalTransportObservationPixelV1 CompletePixel(ulong generation = 1, uint version = 1, bool finalized = true, bool overflow = false, string reason = "provisional_event_coverage") =>
            new(0, generation, version, finalized, TransportRecorderCompletionV1.Failed, reason, 8, overflow,
                new[] { TransportEventKindV1.ProbeQuestionIssued, TransportEventKindV1.Terminal });
        TransportDispositionPixelStatusV1 Export(FormalTransportObservationSnapshotV1 snapshot, int contact = 0, int first = -1)
        {
            string output = Path.Combine(root.Path, Guid.NewGuid().ToString("N"));
            TestAssert.True(TransportDispositionObservationMapV1.TryWrite(InputForSnapshot(output, snapshot, new[] { contact }, new[] { first }), out TransportDispositionObservationResultV1? value, out string reason), reason);
            return value!.Statuses[0];
        }

        var off = new FormalTransportObservationSnapshotV1(1, false, 0, 0, 0, 0, 0, Array.Empty<FormalTransportObservationPixelV1>());
        TestAssert.Equal(TransportDispositionPixelStatusV1.RecorderDisabledOrMissing, Export(off), "disabled status");
        var unfinalized = SnapshotWith(CompletePixel(finalized: false), GoodMeasurement());
        TestAssert.Equal(TransportDispositionPixelStatusV1.AttemptUnfinalized, Export(unfinalized), "unfinalized status");
        var overflow = SnapshotWith(CompletePixel(overflow: true, reason: "provisional_event_coverage;recorder_event_capacity_overflow"), GoodMeasurement());
        TestAssert.Equal(TransportDispositionPixelStatusV1.RecorderOverflow, Export(overflow), "overflow status code");
        var failed = SnapshotWith(CompletePixel(reason: "provisional_event_coverage;recorder_failure"), GoodMeasurement());
        TestAssert.Equal(TransportDispositionPixelStatusV1.RecorderFailedOrIncomplete, Export(failed), "failed status");
        var incoherent = SnapshotWith(CompletePixel(), GoodMeasurement(generation: 2));
        TestAssert.Equal(TransportDispositionPixelStatusV1.SnapshotCoherenceFailure, Export(incoherent), "generation mismatch status");
        var missing = new FormalTransportObservationSnapshotV1(1, true, 1, 2, 1, 1, 0, new[] { CompletePixel() });
        TestAssert.Equal(TransportDispositionPixelStatusV1.MeasurementsMissing, Export(missing), "measurements missing status");
        var questionMismatch = SnapshotWith(CompletePixel(), GoodMeasurement(questionCount: 1, querySteps: Array.Empty<int>()));
        TestAssert.Equal(TransportDispositionPixelStatusV1.QuestionJoinMismatch, Export(questionMismatch), "question join status");
        TestAssert.Equal(TransportDispositionPixelStatusV1.ContactChannelMismatch, Export(SnapshotWith(CompletePixel(), GoodMeasurement()), contact: 1, first: 1), "contact channel status");
        var terminalMismatch = SnapshotWith(CompletePixel(), GoodMeasurement(events: new[] { question }));
        TestAssert.Equal(TransportDispositionPixelStatusV1.TerminalMismatch, Export(terminalMismatch), "terminal status");

        static FormalTransportObservationSnapshotV1 SnapshotWith(FormalTransportObservationPixelV1 pixel, FormalTransportPixelMeasurementV1 measurement) =>
            new(1, true, 1, pixel.EventKinds.Count, 1, 1, 0, new[] { pixel }, new FormalTransportPixelMeasurementsV1(new[] { measurement }));
    }

    private static void DeterministicGoldenAndOrientation()
    {
        PixelSpec[] specs =
        {
            Spec(0, RawTerminalFlagsV1.MaxStepsReached, Pass1TransportTerminationV1.MaxSteps, 1, 0),
            Spec(1, RawTerminalFlagsV1.Absorbed, Pass1TransportTerminationV1.AbsorbedAtSample, 1, 0),
            Spec(2, RawTerminalFlagsV1.MaxStepsReached, Pass1TransportTerminationV1.MaxSteps, 1, 1),
            Spec(3, RawTerminalFlagsV1.MaxDistanceReached, Pass1TransportTerminationV1.MaxDistance, 1, 0)
        };
        using TempDir first = new(); using TempDir second = new();
        TestAssert.True(TransportDispositionObservationMapV1.TryWrite(Input(first.Path, specs, width: 2, height: 2), out TransportDispositionObservationResultV1? a, out string ar), ar);
        TestAssert.True(TransportDispositionObservationMapV1.TryWrite(Input(second.Path, specs, width: 2, height: 2), out TransportDispositionObservationResultV1? b, out string br), br);
        foreach (string name in new[] { "transport_status.bin", "transport_disposition.bin", "traveled_bits.bin", "steps_integrated.bin", "question_count.bin", "queried_chord_bits.bin", "max_unqueried_gap_bits.bin", "first_contact_step.bin", "remaps_before_first_contact.bin", "transport_disposition.png", "transport_disposition_legend.json" })
            TestAssert.True(File.ReadAllBytes(Path.Combine(first.Path, name)).SequenceEqual(File.ReadAllBytes(Path.Combine(second.Path, name))), "repeat export " + name);
        TestAssert.Equal(a!.MeasurementsSha256, b!.MeasurementsSha256, "measurement hash deterministic");
        byte[] disposition = File.ReadAllBytes(Path.Combine(first.Path, "transport_disposition.bin"));
        TestAssert.True(disposition.SequenceEqual(new byte[] { 6, 3, 2, 7 }), "row-major orientation");
        TestAssert.Equal("af0493129625ad04acc54a241845bc99c5a2514b36a1c4c1104c0b3bb4b70bae", Hex(disposition), "golden disposition bytes");
        TestAssert.Equal("8c9c5291c266444518456597a9210554efb41247c132a1895182613d12ece8aa", Hex(File.ReadAllBytes(Path.Combine(first.Path, "transport_disposition.png"))), "golden PNG bytes");
    }

    private static void StrideFailsClosed()
    {
        using TempDir root = new();
        TransportDispositionObservationInputV1 input = Input(root.Path, new[] { Spec(0, RawTerminalFlagsV1.MaxStepsReached, Pass1TransportTerminationV1.MaxSteps, 1, 0) });
        TransportDispositionObservationInputV1 bad = Copy(input, 2);
        TestAssert.False(TransportDispositionObservationMapV1.TryWrite(bad, out _, out string reason), "non-unit stride rejected");
        TestAssert.Equal("film_stride_unsupported", reason, "stride failure reason");
    }

    private static PixelSpec Spec(int pixel, RawTerminalFlagsV1 flags, Pass1TransportTerminationV1 termination, int questions, int contacts, bool remapBefore = false, bool remapAfter = false)
    {
        var events = new List<FormalTransportEventMeasurementV1>();
        if (remapBefore) events.Add(Event(events.Count, 1, TransportEventKindV1.BoundaryRemapApplied, TransportReasonV1.SceneTransform));
        for (int i = 0; i < questions; i++)
        {
            events.Add(Event(events.Count, i + 1, TransportEventKindV1.ProbeQuestionIssued, TransportReasonV1.ProbeCadence));
            if (contacts > i) events.Add(Event(events.Count, i + 2, TransportEventKindV1.GeometryContactObserved, TransportReasonV1.ProbeCadence));
        }
        if ((flags & RawTerminalFlagsV1.ProbeQueryFailed) != 0)
            events.Add(Event(events.Count, 3, TransportEventKindV1.ProbeQueryFailed, TransportReasonV1.PhysicsQueryFailure));
        if (termination == Pass1TransportTerminationV1.AbsorbedAtSample)
            events.Add(Event(events.Count, 3, TransportEventKindV1.AbsorptionObserved, TransportReasonV1.InsideAbsorbingRadius));
        if (remapAfter) events.Add(Event(events.Count, 3, TransportEventKindV1.BoundaryRemapApplied, TransportReasonV1.SceneTransform));
        events.Add(Event(events.Count, 4, TransportEventKindV1.Terminal, TerminalReason(termination)));
        return new PixelSpec(pixel, 1, false, "provisional_event_coverage", flags, termination, questions, contacts, events.ToArray());
    }

    private static FormalTransportEventMeasurementV1 Event(int ordinal, int step, TransportEventKindV1 kind, TransportReasonV1 reason) => new(ordinal, step, kind, reason);
    private static TransportReasonV1 TerminalReason(Pass1TransportTerminationV1 termination) => termination switch
    {
        Pass1TransportTerminationV1.AbsorbedAtSample => TransportReasonV1.InsideAbsorbingRadius,
        Pass1TransportTerminationV1.MaxDistance => TransportReasonV1.MaxDistance,
        Pass1TransportTerminationV1.MaxSteps => TransportReasonV1.MaxSteps,
        Pass1TransportTerminationV1.SegmentCapacityTruncated => TransportReasonV1.SegmentCapacity,
        _ => TransportReasonV1.MaxSteps
    };

    private readonly record struct PixelSpec(int Pixel, uint CommitVersion, bool Overflowed, string Reason, RawTerminalFlagsV1 Flags, Pass1TransportTerminationV1 Termination, int Questions, int Contacts, FormalTransportEventMeasurementV1[] Events);

    private static TransportDispositionObservationInputV1 Input(string output, PixelSpec[] specs, int width = -1, int height = 1, bool omitMeasurements = false)
    {
        if (width < 0) width = specs.Length;
        var pixels = specs.Select(s => Pixel(s.Pixel, s.CommitVersion, true, s.Overflowed, s.Reason, s.Events.Select(e => e.Kind).ToArray())).ToArray();
        var measured = specs.Select(s => new FormalTransportPixelMeasurementV1(
            s.Pixel, 1, s.CommitVersion, s.Termination, s.Flags, 4, BitConverter.SingleToUInt32Bits(4f), s.Questions,
            BitConverter.SingleToUInt32Bits(s.Questions == 0 ? 0f : 1f), BitConverter.SingleToUInt32Bits(s.Questions == 0 ? 4f : 3f),
            Enumerable.Range(1, s.Questions), s.Events)).ToArray();
        var snapshot = new FormalTransportObservationSnapshotV1(1, true, pixels.Length, pixels.Sum(p => p.EventKinds.Count), pixels.Length, pixels.Length, 0, pixels,
            omitMeasurements ? null : new FormalTransportPixelMeasurementsV1(measured));
        int total = width * height;
        int[] contacts = new int[total]; int[] first = Enumerable.Repeat(-1, total).ToArray();
        foreach (PixelSpec s in specs) { contacts[s.Pixel] = s.Contacts; if (s.Contacts > 0) first[s.Pixel] = s.Events.First(e => e.Kind == TransportEventKindV1.GeometryContactObserved).IntegrationStepOrdinal; }
        return new TransportDispositionObservationInputV1
        {
            OutputDirectory = output, RunId = "disposition-test", SemanticSceneId = "test.disposition", GodotScenePath = "res://test.tscn",
            FormalPolicyId = "cathedral.formal.v1", FormalPolicySha256 = Sha, GeometrySnapshotSha256 = Sha,
            FrozenBoundaryContentSha256 = Sha, BoundaryExecutionAuthoritySha256 = Sha,
            CameraPose = new CameraPoseBitsV1(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13), Width = width, Height = height,
            FilmStride = 1, CanonicalQueryDatasetSha256 = Sha, EngineCommit = "test", SourceTreeState = "dirty", AuthoredDiffSha256 = Sha,
            Snapshot = snapshot, ContactCounts = contacts, FirstContactSteps = first
        };
    }

    private static FormalTransportObservationPixelV1 Pixel(int index, uint version, bool finalized, bool overflowed, string reason, params TransportEventKindV1[] kinds) =>
        new(index, 1, version, finalized, TransportRecorderCompletionV1.Failed, reason, System.Math.Max(8, kinds.Length), overflowed, kinds);

    private static TransportDispositionObservationInputV1 Copy(TransportDispositionObservationInputV1 x, int stride) => new()
    {
        OutputDirectory = x.OutputDirectory, RunId = x.RunId, SemanticSceneId = x.SemanticSceneId, GodotScenePath = x.GodotScenePath,
        FormalPolicyId = x.FormalPolicyId, FormalPolicySha256 = x.FormalPolicySha256, GeometrySnapshotSha256 = x.GeometrySnapshotSha256,
        FrozenBoundaryContentSha256 = x.FrozenBoundaryContentSha256, BoundaryExecutionAuthoritySha256 = x.BoundaryExecutionAuthoritySha256,
        CameraPose = x.CameraPose, Width = x.Width, Height = x.Height, FilmStride = stride, CanonicalQueryDatasetSha256 = x.CanonicalQueryDatasetSha256,
        EngineCommit = x.EngineCommit, SourceTreeState = x.SourceTreeState, AuthoredDiffSha256 = x.AuthoredDiffSha256,
        Snapshot = x.Snapshot, ContactCounts = x.ContactCounts, FirstContactSteps = x.FirstContactSteps
    };

    private static TransportDispositionObservationInputV1 InputForSnapshot(string output, FormalTransportObservationSnapshotV1 snapshot, int[] contacts, int[] first) => new()
    {
        OutputDirectory = output, RunId = "status-test", SemanticSceneId = "test.disposition", GodotScenePath = "res://test.tscn",
        FormalPolicyId = "cathedral.formal.v1", FormalPolicySha256 = Sha, GeometrySnapshotSha256 = Sha,
        FrozenBoundaryContentSha256 = Sha, BoundaryExecutionAuthoritySha256 = Sha,
        CameraPose = new CameraPoseBitsV1(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13), Width = 1, Height = 1,
        FilmStride = 1, CanonicalQueryDatasetSha256 = Sha, EngineCommit = "test", SourceTreeState = "dirty", AuthoredDiffSha256 = Sha,
        Snapshot = snapshot, ContactCounts = contacts, FirstContactSteps = first
    };

    private static int[] ReadInt32(string path) { byte[] b = File.ReadAllBytes(path); int[] v = new int[b.Length / 4]; for (int i = 0; i < v.Length; i++) v[i] = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(i * 4, 4)); return v; }
    private static string Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class TempDir : IDisposable
    {
        public TempDir() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xprimeray-disposition-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }
}
