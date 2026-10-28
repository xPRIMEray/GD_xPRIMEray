using System.Buffers.Binary;
using System.Text;

namespace XPrimeRay.ObservationLayer.Tests;

internal static class TransportLedgerV1Tests
{
    private static readonly string A = new('a', 64), B = new('b', 64), C = new('c', 64), D = new('d', 64);
    private const string RayConvention = RayGenerationConventionIdsV1.GodotPerspectiveCameraRays;

    public static void Run()
    {
        GoldenEmptyAndNonemptyBytesAndHashes();
        ScientificHashExcludesLifecycleAndRuntimeEnvelope();
        CanonicalStringsAreOrdinalUtf8WithoutNormalization();
        RawBitsAndCameraOrderArePinned();
        ContractsAreImmutableCopies();
        PresenceFlagsAreAuthoritative();
        RawGodotWitnessCannotLeakIntoScience();
        DefaultStructsAndUnknownValuesAreRejected();
        PerKindSemanticsAreValidated();
        SealEligibilityIsExplicit();
        FoundAndSurfaceClassAreScientificInputs();
        FinalEvidenceCoherenceIsEnforced();
        PixelBoundsAndStrideRepresentativesAreEnforced();
        QuestionOrdinalsAreContiguous();
        DiagnosticFailureIsIsolatedAndStaleLinksFail();
        LegacyTransportEventRecordRemainsCompatible();
    }

    private static void GoldenEmptyAndNonemptyBytesAndHashes()
    {
        byte[] empty = TransportLedgerV1Serializer.SerializeScientificContent(new ScientificTransportLedgerV1(Key(1, 1), Array.Empty<ScientificPixelCommitV1>()));
        ScientificTransportLedgerV1 ledger = Ledger(Key(1, 1), Commit(0, 7, CompleteStatus(), new[] { TirEvent(0, "boundary-v1:portal"), GeometryEvent(1, "geometry-v1:wall") }, new[] { new QuestionAuthorityLinkV1(0, 4, QuestionAuthorityDispositionV1.ValidatedContact, "geometry-v1:wall") }));
        string sha = TransportLedgerV1Serializer.ComputeScientificContentSha256(ledger);
        DiagnosticRenderAppendixV1 appendix = Appendix(ledger, 7, DiagnosticRenderAppendixStatusV1.Complete, 7, 42);
        byte[] artifact = TransportLedgerV1Serializer.SerializeArtifact(new TransportLedgerArtifactV1(ledger, appendix));
        const string empty64 = "WFBSSU1FLVNDSUVOVElGSUMtVFJBTlNQT1JULUxFREdFUgABAEAAAABhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhQAAAAGJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJAAAAAY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY0AAAABkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkIAAAAGdvZG90LXBlcnNwZWN0aXZlLWNhbWVyYS1yYXlzLXYxAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAAAAAAAAAAAAIA/AAAAAAAAAAAAAAAAAABwQgEAAAABAAAAAQAAAAAAAAAAAAAA";
        const string emptySha = "48ac2f1e230de41d6f36c12c605c80c26704d30b6381ff3f524557174bf06048";
        const string science64 = "WFBSSU1FLVNDSUVOVElGSUMtVFJBTlNQT1JULUxFREdFUgABAEAAAABhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhQAAAAGJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJAAAAAY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY0AAAABkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkIAAAAGdvZG90LXBlcnNwZWN0aXZlLWNhbWVyYS1yYXlzLXYxAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAAAAAAAAAAAAIA/AAAAAAAAAAAAAAAAAABwQgEAAAABAAAAAQAAAAIAAAASAAAAYm91bmRhcnktdjE6cG9ydGFsEAAAAGdlb21ldHJ5LXYxOndhbGwBAAAAAAAAAAIDAAAAAQIAAAAIAAAAAAAAQAAAAgAAAAAAAAAEAAAA/////wsAAQEAAAAAAgAAAAUAAAAtAAAABgAIAAMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIA/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/AAAAAAEAAAAEAAAA/////woAAgEBAAAAAAAAAIAAAAAAAAAAAwALAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAABAAAAAIBAAAA";
        const string scienceSha = "b5a792886522deec2e494b1ef3253b35721138b238180d9ff85becc0098e0a63";
        const string artifact64 = "WFBSSU1FLVRSQU5TUE9SVC1MRURHRVItQVJUSUZBQ1QAAQDQAgAAAAAAAFhQUklNRS1TQ0lFTlRJRklDLVRSQU5TUE9SVC1MRURHRVIAAQBAAAAAYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYUAAAABiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiQAAAAGNjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NAAAAAZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZGRkZCAAAABnb2RvdC1wZXJzcGVjdGl2ZS1jYW1lcmEtcmF5cy12MQAAgD8AAAAAAAAAAAAAAAAAAIA/AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAcEIBAAAAAQAAAAEAAAACAAAAEgAAAGJvdW5kYXJ5LXYxOnBvcnRhbBAAAABnZW9tZXRyeS12MTp3YWxsAQAAAAAAAAACAwAAAAECAAAACAAAAAAAAEAAAAIAAAAAAAAABAAAAP////8LAAEBAAAAAAIAAAAFAAAALQAAAAYACAADAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPwAAAAABAAAABAAAAP////8KAAIBAQAAAAAAAACAAAAAAAAAAAMACwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAQAAAACAQAAAAEAAAAAAAAABwAAAAIEAaEAAAAAAAAAWFBSSU1FLURJQUdOT1NUSUMtUkVOREVSLUFQUEVORElYAAEAtaeSiGUi3uwuSUse8yU7NXIROLI4GA2f+FvswAmOCmMHAAAAAQEAAAAIAAAAcmVuZGVyZXIGAAAAbmF0aXZlAQAAAAAAAAAHAAAAAQAAAAAAAAAFAAAAAwADAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAKgAAAAAAAAA=";
        const string artifactSha = "54505313c2ba6f283d0273ce1beabaf5c113dd3ffec393a758181ebb8ef269cb";
        Eq(empty64, Convert.ToBase64String(empty), "empty golden bytes"); Eq(emptySha, TransportLedgerV1Serializer.ComputeScientificContentSha256(new ScientificTransportLedgerV1(Key(1, 1), Array.Empty<ScientificPixelCommitV1>())), "empty golden SHA");
        Eq(science64, Convert.ToBase64String(TransportLedgerV1Serializer.SerializeScientificContent(ledger)), "nonempty scientific golden bytes"); Eq(scienceSha, sha, "nonempty scientific SHA");
        Eq(artifact64, Convert.ToBase64String(artifact), "nonempty artifact golden bytes"); Eq(artifactSha, TransportLedgerV1Serializer.ComputeArtifactSha256(new TransportLedgerArtifactV1(ledger, appendix)), "nonempty artifact SHA");
    }

    private static void ScientificHashExcludesLifecycleAndRuntimeEnvelope()
    {
        var pending = Status(TransportAuthorityStatusV1.Pending, TransportTransactionStatusV1.Pass1Committed);
        var finalized = Status(TransportAuthorityStatusV1.Validated, TransportTransactionStatusV1.BandFinalized);
        ScientificTransportLedgerV1 first = Ledger(Key(1, 1), Commit(0, 1, pending));
        ScientificTransportLedgerV1 second = Ledger(Key(1, 1), Commit(0, 99, finalized));
        Eq(TransportLedgerV1Serializer.ComputeScientificContentSha256(first), TransportLedgerV1Serializer.ComputeScientificContentSha256(second), "lifecycle-independent science SHA");
        NotEq(TransportLedgerV1Serializer.ComputeArtifactSha256(new(first, null)), TransportLedgerV1Serializer.ComputeArtifactSha256(new(second, null)), "artifact contains lifecycle and commit version");
        ScientificTransportLedgerV1 sealedBase = Ledger(Key(1, 1), Commit(0, 1, CompleteStatus()));
        string hash = TransportLedgerV1Serializer.ComputeSealedScientificContentSha256(sealedBase.Seal());
        var a = Appendix(sealedBase, 1, DiagnosticRenderAppendixStatusV1.IntentionallySparse, 1, 11);
        var b = Appendix(sealedBase, 2, DiagnosticRenderAppendixStatusV1.Failed, 1, 22);
        Eq(hash, TransportLedgerV1Serializer.ComputeScientificContentSha256(sealedBase), "appendix excluded from science");
        NotEq(TransportLedgerV1Serializer.ComputeArtifactSha256(new(sealedBase, a)), TransportLedgerV1Serializer.ComputeArtifactSha256(new(sealedBase, b)), "runtime appendix changes artifact");
    }

    private static void CanonicalStringsAreOrdinalUtf8WithoutNormalization()
    {
        string composed = "boundary-v1:\u00e9", decomposed = "boundary-v1:e\u0301";
        var ledger = Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), new[] { TirEvent(0, composed), TirEvent(1, decomposed) }));
        byte[] bytes = TransportLedgerV1Serializer.SerializeScientificContent(ledger);
        int ci = bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(composed)), di = bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(decomposed));
        True(ci >= 0 && di >= 0, "both Unicode spellings retained");
        True((StringComparer.Ordinal.Compare(composed, decomposed) < 0) == (ci < di), "ordinal string ordering");
        NotEq(TransportLedgerV1Serializer.ComputeScientificContentSha256(Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), new[] { TirEvent(0, composed) }))), TransportLedgerV1Serializer.ComputeScientificContentSha256(Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), new[] { TirEvent(0, decomposed) }))), "no Unicode normalization");
    }

    private static void RawBitsAndCameraOrderArePinned()
    {
        const uint negZero = 0x80000000, nan = 0x7fc01234;
        var e = TirEvent(0, "boundary-v1:a", new(negZero, nan, 0x3f800000));
        byte[] bytes = TransportLedgerV1Serializer.SerializeScientificContent(Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), new[] { e })));
        Span<byte> n = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(n, negZero); True(bytes.AsSpan().IndexOf(n) >= 0, "negative zero retained"); BinaryPrimitives.WriteUInt32LittleEndian(n, nan); True(bytes.AsSpan().IndexOf(n) >= 0, "NaN payload retained");
        ScientificAcquisitionKey key = Key(1, 1, new(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13));
        byte[] camera = TransportLedgerV1Serializer.SerializeScientificContent(new(key, Array.Empty<ScientificPixelCommitV1>()));
        byte[] convention = Encoding.UTF8.GetBytes(RayConvention);
        int cameraOffset = camera.AsSpan().IndexOf(convention) + convention.Length;
        True(cameraOffset >= convention.Length, "ray convention precedes camera bits");
        for (uint i = 1; i <= 13; i++)
        {
            uint actual = BinaryPrimitives.ReadUInt32LittleEndian(camera.AsSpan(cameraOffset + checked((int)((i - 1) * 4)), 4));
            Eq(i, actual, $"camera raw component order {i}");
        }
    }

    private static void ContractsAreImmutableCopies()
    {
        TransportEventV1 original = TirEvent(0, "boundary-v1:a"); TransportEventV1[] events = { original }; QuestionAuthorityLinkV1[] questions = { new(0, 2, QuestionAuthorityDispositionV1.NoContact, null) };
        var path = Path(0, CompleteStatus(), events, questions); events[0] = TirEvent(0, "boundary-v1:b"); questions[0] = new(0, 9, QuestionAuthorityDispositionV1.NoContact, null);
        Eq("boundary-v1:a", path.Events[0].CanonicalSubjectId, "event copied"); Eq(2, path.QuestionAuthorityLinks[0].ProbeStep, "question copied");
        ScientificPixelCommitV1[] commits = { new(1, path) }; var ledger = new ScientificTransportLedgerV1(Key(1, 1), commits); commits[0] = new(2, Path(0, CompleteStatus())); Eq((uint)1, ledger.Pixels[0].CommitVersion, "commit array copied");
    }

    private static void PresenceFlagsAreAuthoritative()
    {
        Throws<ArgumentException>(() => Event(TransportEventKindV1.ProbeQuestionIssued, TransportSubjectKindV1.None, CanonicalIdentityAvailabilityV1.NotApplicable, null, -1, TransportObservabilityV1.PointSample, TransportEventPresenceV1.None, TransportDecisionV1.QueryIssued, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None, pre: new(1, 0, 0)), "undeclared nonzero payload");
        var zero = Event(TransportEventKindV1.ProbeQuestionIssued, TransportSubjectKindV1.None, CanonicalIdentityAvailabilityV1.NotApplicable, null, -1, TransportObservabilityV1.PointSample, TransportEventPresenceV1.None, TransportDecisionV1.QueryIssued, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None);
        _ = TransportLedgerV1Serializer.SerializeScientificContent(Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), new[] { zero })));
    }

    private static void RawGodotWitnessCannotLeakIntoScience()
    {
        Throws<ArgumentException>(() => Event(TransportEventKindV1.GeometryContactObserved, TransportSubjectKindV1.GeometryPrimitive, CanonicalIdentityAvailabilityV1.Unavailable, null, -1, TransportObservabilityV1.DiagnosticGodotWitness, TransportEventPresenceV1.PrePosition, TransportDecisionV1.NoTransportEffect, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None, pre: new(0x3f800000, 0, 0)), "raw Godot hit position");
        Throws<ArgumentException>(() => Event(TransportEventKindV1.GeometryContactObserved, TransportSubjectKindV1.GeometryPrimitive, CanonicalIdentityAvailabilityV1.Unavailable, null, -1, TransportObservabilityV1.DiagnosticGodotWitness, TransportEventPresenceV1.None, TransportDecisionV1.NoTransportEffect, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None, normal: new(0, 0x3f800000, 0)), "hidden raw normal");
        Throws<ArgumentException>(() => Event(TransportEventKindV1.GeometryContactObserved, TransportSubjectKindV1.GeometryPrimitive, CanonicalIdentityAvailabilityV1.Available, "geometry-v1:g", 0, TransportObservabilityV1.ValidatedSpatialContactAuthority, TransportEventPresenceV1.ContactNormal, TransportDecisionV1.NoTransportEffect, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None, normal: new(0, 0x3f800000, 0)), "validated identity still excludes raw witness normal");
        Throws<ArgumentException>(() => Event(TransportEventKindV1.GeometryContactObserved, TransportSubjectKindV1.GeometryPrimitive, CanonicalIdentityAvailabilityV1.Unavailable, null, -1, TransportObservabilityV1.DiagnosticGodotWitness, TransportEventPresenceV1.PathDistance, TransportDecisionV1.NoTransportEffect, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None, distance: 0x3f800000), "raw Godot hit distance presence");
        Throws<ArgumentException>(() => Event(TransportEventKindV1.GeometryContactObserved, TransportSubjectKindV1.GeometryPrimitive, CanonicalIdentityAvailabilityV1.Unavailable, null, -1, TransportObservabilityV1.DiagnosticGodotWitness, TransportEventPresenceV1.None, TransportDecisionV1.NoTransportEffect, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None, distance: 0x3f800000), "hidden raw Godot hit distance");
        _ = Event(TransportEventKindV1.GeometryContactObserved, TransportSubjectKindV1.GeometryPrimitive, CanonicalIdentityAvailabilityV1.Unavailable, null, -1, TransportObservabilityV1.DiagnosticGodotWitness, TransportEventPresenceV1.None, TransportDecisionV1.NoTransportEffect, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None);
    }

    private static void DefaultStructsAndUnknownValuesAreRejected()
    {
        Throws<ArgumentException>(() => Path(0, CompleteStatus(), new[] { default(TransportEventV1) }), "default event");
        Throws<ArgumentException>(() => Path(0, CompleteStatus(), questions: new[] { default(QuestionAuthorityLinkV1) }), "default question");
        Throws<ArgumentException>(() => new ScientificPixelPathV1(0, default, Terminal(), Array.Empty<TransportEventV1>(), Array.Empty<QuestionAuthorityLinkV1>()), "default status");
        Throws<ArgumentOutOfRangeException>(() => Event((TransportEventKindV1)999, TransportSubjectKindV1.None, CanonicalIdentityAvailabilityV1.NotApplicable, null, -1, TransportObservabilityV1.PointSample, TransportEventPresenceV1.None, TransportDecisionV1.QueryIssued, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None), "undefined event enum");
        Throws<ArgumentOutOfRangeException>(() => Event(TransportEventKindV1.ProbeQuestionIssued, TransportSubjectKindV1.None, CanonicalIdentityAvailabilityV1.NotApplicable, null, -1, (TransportObservabilityV1)0x80000000u, TransportEventPresenceV1.None, TransportDecisionV1.QueryIssued, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None), "unknown observability bit");
    }

    private static void PerKindSemanticsAreValidated()
    {
        _ = Event(TransportEventKindV1.ContinuousBiasApplied, TransportSubjectKindV1.Boundary, CanonicalIdentityAvailabilityV1.Available, "boundary-v1:p", 0, TransportObservabilityV1.ExactMutation | TransportObservabilityV1.PointSample, TransportEventPresenceV1.None, TransportDecisionV1.StateMutated, TransportReasonV1.ContinuousDirectionBias, TransportEventFlagsV1.None);
        _ = Event(TransportEventKindV1.BoundaryCrossingApplied, TransportSubjectKindV1.Boundary, CanonicalIdentityAvailabilityV1.Available, "boundary-v1:p", 0, TransportObservabilityV1.ExactMutation | TransportObservabilityV1.SampledIntervalTransition | TransportObservabilityV1.ExecutionOrderedSimultaneous, TransportEventPresenceV1.None, TransportDecisionV1.StateMutated, TransportReasonV1.BoundaryEntry, TransportEventFlagsV1.None);
        _ = Event(TransportEventKindV1.BoundarySyntheticEgress, TransportSubjectKindV1.Boundary, CanonicalIdentityAvailabilityV1.Available, "boundary-v1:p", 0, TransportObservabilityV1.SampledIntervalTransition | TransportObservabilityV1.SyntheticPairedEgress, TransportEventPresenceV1.None, TransportDecisionV1.NoTransportEffect, TransportReasonV1.BoundaryExit, TransportEventFlagsV1.None);
        Throws<ArgumentException>(() => Event(TransportEventKindV1.BoundarySyntheticEgress, TransportSubjectKindV1.Boundary, CanonicalIdentityAvailabilityV1.Available, "boundary-v1:p", 0, TransportObservabilityV1.ExactMutation | TransportObservabilityV1.SampledIntervalTransition, TransportEventPresenceV1.None, TransportDecisionV1.StateMutated, TransportReasonV1.BoundaryExit, TransportEventFlagsV1.None), "synthetic egress cannot claim mutation");
        Throws<ArgumentException>(() => Event(TransportEventKindV1.BoundarySyntheticEgress, TransportSubjectKindV1.Boundary, CanonicalIdentityAvailabilityV1.Available, "boundary-v1:p", 0, TransportObservabilityV1.SampledIntervalTransition | TransportObservabilityV1.SyntheticPairedEgress, TransportEventPresenceV1.PrePosition, TransportDecisionV1.NoTransportEffect, TransportReasonV1.BoundaryExit, TransportEventFlagsV1.None, pre: new(0x3f800000, 0, 0)), "synthetic egress cannot fabricate state");
        Throws<ArgumentException>(() => Event(TransportEventKindV1.SuppressedMaskTransition, TransportSubjectKindV1.Boundary, CanonicalIdentityAvailabilityV1.Available, "boundary-v1:p", 0, TransportObservabilityV1.SampledIntervalTransition, TransportEventPresenceV1.None, TransportDecisionV1.NoTransportEffect, TransportReasonV1.BoundaryEntry, TransportEventFlagsV1.None), "missing suppressed label");
        Throws<ArgumentException>(() => Event(TransportEventKindV1.GeometryContactObserved, TransportSubjectKindV1.GeometryPrimitive, CanonicalIdentityAvailabilityV1.Available, "geometry-v1:g", 0, TransportObservabilityV1.ValidatedSpatialContactAuthority | TransportObservabilityV1.ExactMutation, TransportEventPresenceV1.None, TransportDecisionV1.StateMutated, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None), "geometry observation cannot claim bounce");
        Throws<ArgumentException>(() => Event(TransportEventKindV1.DielectricApplied, TransportSubjectKindV1.Boundary, CanonicalIdentityAvailabilityV1.Available, "boundary-v1:p", 0, TransportObservabilityV1.ExactMutation | TransportObservabilityV1.SampledIntervalTransition, TransportEventPresenceV1.None, TransportDecisionV1.StateMutated, TransportReasonV1.TotalInternalReflectionCandidate, TransportEventFlagsV1.None), "TIR cannot masquerade as reflection");
        _ = Event(TransportEventKindV1.Terminal, TransportSubjectKindV1.None, CanonicalIdentityAvailabilityV1.NotApplicable, null, -1, TransportObservabilityV1.None, TransportEventPresenceV1.None, TransportDecisionV1.TransportTerminated, TransportReasonV1.MaxDistance, TransportEventFlagsV1.None);
        Throws<ArgumentException>(() => Event(TransportEventKindV1.ProbeQuestionIssued, TransportSubjectKindV1.None, CanonicalIdentityAvailabilityV1.NotApplicable, null, -1, TransportObservabilityV1.PointSample | TransportObservabilityV1.ExecutionOrderedSimultaneous, TransportEventPresenceV1.None, TransportDecisionV1.QueryIssued, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None), "simultaneous flag outside boundary execution");
    }

    private static void SealEligibilityIsExplicit()
    {
        Throws<InvalidOperationException>(() => new ScientificTransportLedgerV1(Key(2, 2), Array.Empty<ScientificPixelCommitV1>()).Seal(), "empty non-sealable");
        Throws<InvalidOperationException>(() => Ledger(Key(2, 2), Commit(0, 1, CompleteStatus())).Seal(), "partial non-sealable");
        foreach (TransportTransactionStatusV1 s in new[] { TransportTransactionStatusV1.Prepared, TransportTransactionStatusV1.Superseded, TransportTransactionStatusV1.Abandoned })
            Throws<InvalidOperationException>(() => FullLedger(Status(TransportAuthorityStatusV1.Validated, s)).Seal(), $"transaction {s}");
        foreach (TransportAuthorityStatusV1 a in new[] { TransportAuthorityStatusV1.Pending, TransportAuthorityStatusV1.Mismatch, TransportAuthorityStatusV1.Unavailable })
            Throws<InvalidOperationException>(() => FullLedger(Status(a, TransportTransactionStatusV1.AuthorityFinalized)).Seal(), $"authority {a}");
        Throws<InvalidOperationException>(() => FullLedger(new(Pass1TransportTerminationV1.MaxDistance, TransportObservationCoverageV1.RecordedDecisionsComplete, TransportRecorderCompletionV1.Overflow, TransportAuthorityStatusV1.Validated, TransportTransactionStatusV1.AuthorityFinalized)).Seal(), "overflow non-sealable");
        var provisionalPass1 = new TransportStatusAxesV1(
            Pass1TransportTerminationV1.MaxDistance,
            TransportObservationCoverageV1.SparseProbeCoverage,
            TransportRecorderCompletionV1.Failed,
            TransportAuthorityStatusV1.Pending,
            TransportTransactionStatusV1.Pass1Committed);
        Throws<InvalidOperationException>(() => FullLedger(provisionalPass1).Seal(), "A0.4b2a provisional pass-1 record non-sealable");
        SealedScientificTransportLedgerV1 sealedLedger = FullLedger(CompleteStatus()).Seal();
        Eq(TransportLedgerV1Serializer.ComputeScientificContentSha256(sealedLedger.Ledger), TransportLedgerV1Serializer.ComputeSealedScientificContentSha256(sealedLedger), "explicit sealed bytes");
        _ = TransportLedgerV1Serializer.SerializeArtifact(new(FullLedger(Status(TransportAuthorityStatusV1.Pending, TransportTransactionStatusV1.Prepared)), null));
    }

    private static void FoundAndSurfaceClassAreScientificInputs()
    {
        foreach (TransportSurfaceClassV1 surface in new[] { TransportSurfaceClassV1.Geometry, TransportSurfaceClassV1.Background, TransportSurfaceClassV1.Unknown })
            _ = new RawTerminalInputsV1(RawTerminalFlagsV1.MaxDistanceReached, 8, 0x40000000, true, surface);
        _ = new RawTerminalInputsV1(RawTerminalFlagsV1.MaxDistanceReached, 8, 0x40000000, false, TransportSurfaceClassV1.None);
        Throws<ArgumentException>(() => new RawTerminalInputsV1(RawTerminalFlagsV1.None, 0, 0, true, TransportSurfaceClassV1.None), "found=true with None");
        foreach (TransportSurfaceClassV1 surface in new[] { TransportSurfaceClassV1.Geometry, TransportSurfaceClassV1.Background, TransportSurfaceClassV1.Unknown })
            Throws<ArgumentException>(() => new RawTerminalInputsV1(RawTerminalFlagsV1.None, 0, 0, false, surface), $"found=false with {surface}");
        var background = Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), terminal: new(RawTerminalFlagsV1.MaxDistanceReached, 8, 0x40000000, true, TransportSurfaceClassV1.Background)));
        var miss = Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), terminal: new(RawTerminalFlagsV1.MaxDistanceReached, 8, 0x40000000, false, TransportSurfaceClassV1.None)));
        NotEq(TransportLedgerV1Serializer.ComputeScientificContentSha256(background), TransportLedgerV1Serializer.ComputeScientificContentSha256(miss), "background hit versus miss scientific inputs");
    }

    private static void FinalEvidenceCoherenceIsEnforced()
    {
        _ = FullLedger(CompleteStatus()).Seal();
        var wrongFlags = new RawTerminalInputsV1(RawTerminalFlagsV1.MaxStepsReached, 8, 0x40000000, false, TransportSurfaceClassV1.None);
        Throws<InvalidOperationException>(() => Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), terminal: wrongFlags)).Seal(), "termination versus flags");
        var wrongTerminal = Event(TransportEventKindV1.Terminal, TransportSubjectKindV1.None, CanonicalIdentityAvailabilityV1.NotApplicable, null, -1, TransportObservabilityV1.None, TransportEventPresenceV1.None, TransportDecisionV1.TransportTerminated, TransportReasonV1.MaxSteps, TransportEventFlagsV1.None);
        Throws<InvalidOperationException>(() => Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), new[] { wrongTerminal })).Seal(), "terminal event versus classification");
        var question = new QuestionAuthorityLinkV1(0, 4, QuestionAuthorityDispositionV1.ValidatedContact, "geometry-v1:g");
        _ = Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), new[] { GeometryEvent(0, "geometry-v1:g") }, new[] { question })).Seal();
        Throws<InvalidOperationException>(() => Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), new[] { GeometryEvent(0, "geometry-v1:g") }, new[] { new QuestionAuthorityLinkV1(0, 4, QuestionAuthorityDispositionV1.NoContact, null) })).Seal(), "geometry observation versus no-contact question");
        Throws<InvalidOperationException>(() => Ledger(Key(1, 1), Commit(0, 1, CompleteStatus(), questions: new[] { question })).Seal(), "validated question without geometry observation");
    }

    private static void PixelBoundsAndStrideRepresentativesAreEnforced()
    {
        ScientificAcquisitionKey key = Key(5, 3, stride: 2); Eq(6, key.RepresentativePixelCount, "stride representative count");
        foreach (int p in new[] { 0, 2, 4, 10, 12, 14 }) True(key.IsRepresentativePixelIndex(p), $"representative {p}");
        foreach (int p in new[] { 1, 5, 9, 15 }) False(key.IsRepresentativePixelIndex(p), $"nonrepresentative {p}");
        Throws<ArgumentOutOfRangeException>(() => Ledger(key, Commit(1, 1, CompleteStatus())), "nonrepresentative ledger pixel");
    }

    private static void QuestionOrdinalsAreContiguous()
    {
        Throws<ArgumentException>(() => Path(0, CompleteStatus(), questions: new[] { new QuestionAuthorityLinkV1(1, 1, QuestionAuthorityDispositionV1.NoContact, null) }), "first question ordinal");
        Throws<ArgumentException>(() => Path(0, CompleteStatus(), questions: new[] { new QuestionAuthorityLinkV1(0, 1, QuestionAuthorityDispositionV1.NoContact, null), new QuestionAuthorityLinkV1(2, 2, QuestionAuthorityDispositionV1.NoContact, null) }), "question gap");
    }

    private static void DiagnosticFailureIsIsolatedAndStaleLinksFail()
    {
        var ledger = Ledger(Key(1, 1), Commit(0, 3, CompleteStatus())); string hash = TransportLedgerV1Serializer.ComputeScientificContentSha256(ledger);
        var failed = Appendix(ledger, 5, DiagnosticRenderAppendixStatusV1.Failed, 3, 77); _ = TransportLedgerV1Serializer.SerializeArtifact(new(ledger, failed)); Eq(hash, TransportLedgerV1Serializer.ComputeScientificContentSha256(ledger), "failed appendix isolated");
        var pending = Ledger(Key(1, 1), Commit(0, 3, Status(TransportAuthorityStatusV1.Pending, TransportTransactionStatusV1.Pass1Committed)));
        Throws<InvalidOperationException>(() => Appendix(pending, 5, DiagnosticRenderAppendixStatusV1.IntentionallySparse, 3, 0), "appendix cannot bind pending ledger");
        var other = Ledger(Key(1, 1), Commit(0, 3, CompleteStatus(), terminal: new(RawTerminalFlagsV1.MaxDistanceReached, 8, 0x40000000, true, TransportSurfaceClassV1.Background)));
        Throws<InvalidOperationException>(() => TransportLedgerV1Serializer.SerializeArtifact(new(ledger, Appendix(other, 5, DiagnosticRenderAppendixStatusV1.Complete, 3, 0))), "wrong science link");
        Throws<InvalidOperationException>(() => TransportLedgerV1Serializer.SerializeArtifact(new(ledger, Appendix(ledger, 5, DiagnosticRenderAppendixStatusV1.Complete, 4, 0))), "stale commit link");
    }

    private static void LegacyTransportEventRecordRemainsCompatible()
    {
        var legacy = new TransportEventRecord(7, 12, TransportEventKind.BoundaryRemap, new BoundaryIdentityCode(9), new FieldSourceIdentityCode(0), 1.25f); Eq(7, legacy.RayIndex, "legacy ray"); Eq(5, (int)TransportEventKind.BoundaryRemap, "legacy enum"); Eq(1.25f, legacy.AccumulatedPathLength, "legacy path");
    }

    private static ScientificAcquisitionKey Key(int w, int h, CameraPoseBitsV1? camera = null, int stride = 1) => new(A, B, C, D, RayConvention, camera ?? new(0x3f800000, 0, 0, 0, 0x3f800000, 0, 0, 0, 0x3f800000, 0, 0, 0, 0x42700000), w, h, stride);
    private static RawTerminalInputsV1 Terminal() => new(RawTerminalFlagsV1.MaxDistanceReached, 8, 0x40000000, false, TransportSurfaceClassV1.None);
    private static TransportStatusAxesV1 CompleteStatus() => Status(TransportAuthorityStatusV1.Validated, TransportTransactionStatusV1.AuthorityFinalized);
    private static TransportStatusAxesV1 Status(TransportAuthorityStatusV1 a, TransportTransactionStatusV1 t) => new(Pass1TransportTerminationV1.MaxDistance, TransportObservationCoverageV1.RecordedDecisionsComplete | TransportObservationCoverageV1.SparseProbeCoverage, TransportRecorderCompletionV1.Complete, a, t);
    private static ScientificPixelPathV1 Path(int pixel, TransportStatusAxesV1 status, IEnumerable<TransportEventV1>? events = null, IEnumerable<QuestionAuthorityLinkV1>? questions = null, RawTerminalInputsV1? terminal = null) => new(pixel, status, terminal ?? Terminal(), events ?? Array.Empty<TransportEventV1>(), questions ?? Array.Empty<QuestionAuthorityLinkV1>());
    private static ScientificPixelCommitV1 Commit(int pixel, uint version, TransportStatusAxesV1 status, IEnumerable<TransportEventV1>? events = null, IEnumerable<QuestionAuthorityLinkV1>? questions = null, RawTerminalInputsV1? terminal = null) => new(version, Path(pixel, status, events, questions, terminal));
    private static ScientificTransportLedgerV1 Ledger(ScientificAcquisitionKey key, params ScientificPixelCommitV1[] commits) => new(key, commits);
    private static ScientificTransportLedgerV1 FullLedger(TransportStatusAxesV1 status) => Ledger(Key(2, 2), Commit(0, 1, status), Commit(1, 1, status), Commit(2, 1, status), Commit(3, 1, status));
    private static TransportEventV1 TirEvent(int ordinal, string id, RawFloat3BitsV1 pre = default) => Event(TransportEventKindV1.DielectricApplied, TransportSubjectKindV1.Boundary, CanonicalIdentityAvailabilityV1.Available, id, 2, TransportObservabilityV1.ExactMutation | TransportObservabilityV1.SampledIntervalTransition, TransportEventPresenceV1.PrePosition | TransportEventPresenceV1.PreDirection | TransportEventPresenceV1.PostDirection | TransportEventPresenceV1.PathDistance, TransportDecisionV1.DegenerateDirection, TransportReasonV1.TotalInternalReflectionCandidate, TransportEventFlagsV1.DegenerateDirection | TransportEventFlagsV1.TirCandidate, ordinal, pre: pre, preDir: new(0x3f800000, 0, 0), distance: 0x3f000000);
    private static TransportEventV1 GeometryEvent(int ordinal, string id) => Event(TransportEventKindV1.GeometryContactObserved, TransportSubjectKindV1.GeometryPrimitive, CanonicalIdentityAvailabilityV1.Available, id, 0, TransportObservabilityV1.ValidatedSpatialContactAuthority, TransportEventPresenceV1.None, TransportDecisionV1.NoTransportEffect, TransportReasonV1.ProbeCadence, TransportEventFlagsV1.None, ordinal);
    private static TransportEventV1 Event(TransportEventKindV1 kind, TransportSubjectKindV1 subject, CanonicalIdentityAvailabilityV1 availability, string? id, int subjectOrdinal, TransportObservabilityV1 observability, TransportEventPresenceV1 presence, TransportDecisionV1 decision, TransportReasonV1 reason, TransportEventFlagsV1 flags, int ordinal = 0, RawFloat3BitsV1 pre = default, RawFloat3BitsV1 post = default, RawFloat3BitsV1 preDir = default, RawFloat3BitsV1 postDir = default, RawFloat3BitsV1 normal = default, uint distance = 0, uint t = 0) => new(ordinal, 4, -1, kind, subject, availability, id, subjectOrdinal, observability, presence, decision, reason, flags, pre, post, preDir, postDir, normal, distance, t);
    private static DiagnosticRenderAppendixV1 Appendix(ScientificTransportLedgerV1 ledger, int generation, DiagnosticRenderAppendixStatusV1 status, uint version, ulong collider) => new(ledger.Seal(), generation, status, new[] { new RuntimeProvenanceEntryV1("renderer", "native") }, new[] { new DiagnosticRenderPixelV1(0, version, new[] { new DiagnosticRenderObservationV1(0, 5, DiagnosticRenderObservationKindV1.ContinuationCompleted, DiagnosticRenderObservationFlagsV1.PostTerminalDiagnostic | (collider == 0 ? 0 : DiagnosticRenderObservationFlagsV1.RuntimeGodotWitness), default, default, collider) }) });

    private static void True(bool c, string l) { if (!c) throw new InvalidOperationException("Expected true: " + l); }
    private static void False(bool c, string l) => True(!c, l);
    private static void Eq<T>(T e, T a, string l) { if (!EqualityComparer<T>.Default.Equals(e, a)) throw new InvalidOperationException($"{l}: expected '{e}', got '{a}'"); }
    private static void NotEq<T>(T e, T a, string l) { if (EqualityComparer<T>.Default.Equals(e, a)) throw new InvalidOperationException(l + ": unexpectedly equal"); }
    private static void Throws<T>(Action action, string label) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}: {label}"); }
}
