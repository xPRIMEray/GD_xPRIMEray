using System.Collections.ObjectModel;

namespace XPrimeRay.ObservationLayer;

public enum TransportEventKindV1 : ushort { Unknown, AbsorptionObserved, ContinuousBiasApplied, BoundaryCrossingApplied, BoundaryRemapApplied, BoundarySyntheticEgress, SuppressedMaskTransition, NumericalClampApplied, ProbeQuestionIssued, ProbeQueryFailed, GeometryContactObserved, DielectricApplied, Terminal }
[Flags] public enum TransportObservabilityV1 : uint { None = 0, ExactMutation = 1 << 0, PointSample = 1 << 1, SampledIntervalTransition = 1 << 2, ExecutionOrderedSimultaneous = 1 << 3, SyntheticPairedEgress = 1 << 4, SuppressedMaskTransition = 1 << 5, DiagnosticGodotWitness = 1 << 6, ValidatedSpatialContactAuthority = 1 << 7 }
[Flags] public enum TransportEventPresenceV1 : uint { None = 0, PrePosition = 1 << 0, PostPosition = 1 << 1, PreDirection = 1 << 2, PostDirection = 1 << 3, ContactNormal = 1 << 4, PathDistance = 1 << 5, LocalSegmentParameter = 1 << 6 }
[Flags] public enum TransportEventFlagsV1 : uint { None = 0, DegenerateDirection = 1 << 0, TirCandidate = 1 << 1, NonFiniteState = 1 << 2 }
public enum TransportSubjectKindV1 : byte { None, Boundary, GeometryPrimitive, FieldSource }
public enum CanonicalIdentityAvailabilityV1 : byte { NotApplicable, Available, Unavailable }
public enum TransportDecisionV1 : ushort { None, StateMutated, TransportTerminated, NoTransportEffect, QueryIssued, QueryFailed, DegenerateDirection }
public enum TransportReasonV1 : ushort { None, InsideAbsorbingRadius, BoundaryEntry, BoundaryExit, ContinuousDirectionBias, SceneTransform, PerfectMirror, DielectricRefraction, TotalInternalReflectionCandidate, NonFiniteAcceleration, AccelerationMagnitudeClamp, ProbeCadence, PhysicsQueryFailure, MaxDistance, MaxSteps, SegmentCapacity, ZeroAdvance }

/// <summary>Only genuine pass-1 transport terminations; numerical/zero-advance state remains in RawTerminalFlagsV1.</summary>
public enum Pass1TransportTerminationV1 : byte { None, AbsorbedAtSample, MaxDistance, MaxSteps, SegmentCapacityTruncated }
[Flags] public enum TransportObservationCoverageV1 : uint { None = 0, RecordedDecisionsComplete = 1 << 0, SparseProbeCoverage = 1 << 1, PointSampledAbsorption = 1 << 2, SampledBoundaryTransitions = 1 << 3, CrossingMaskLimited = 1 << 4 }
public enum TransportRecorderCompletionV1 : byte { NotStarted, Complete, Overflow, Failed, Abandoned }
public enum TransportAuthorityStatusV1 : byte { NotRequired, Pending, Validated, Mismatch, Unavailable }
public enum TransportTransactionStatusV1 : byte { Prepared = 1, Pass1Committed, BandFinalized, AuthorityFinalized, Superseded, Abandoned }
[Flags] public enum RawTerminalFlagsV1 : uint { None = 0, Absorbed = 1 << 0, MaxDistanceReached = 1 << 1, MaxStepsReached = 1 << 2, SegmentCapacityTruncated = 1 << 3, NumericalFailure = 1 << 4, ProbeQueryFailed = 1 << 5, DegenerateDirection = 1 << 6, ZeroAdvance = 1 << 7 }
public enum TransportSurfaceClassV1 : byte { None = 0, Geometry = 1, Background = 2, Unknown = 255 }
public enum QuestionAuthorityDispositionV1 : byte { Pending, NoContact, ValidatedContact, Mismatch, Unavailable }
public enum DiagnosticRenderAppendixStatusV1 : byte { Complete = 1, IntentionallySparse, Overflow, Failed, Abandoned }
public enum DiagnosticRenderObservationKindV1 : ushort { Unknown, Pass2GeometryObservation, ContinuationStarted, ContinuationCompleted, ContinuationLimitReached, PostTerminalDiagnostic }
[Flags] public enum DiagnosticRenderObservationFlagsV1 : uint { None = 0, PostTerminalDiagnostic = 1 << 0, RuntimeGodotWitness = 1 << 1, HasPosition = 1 << 2, HasNormal = 1 << 3 }

public static class RayGenerationConventionIdsV1
{
    public const string GodotPerspectiveCameraRays = "godot-perspective-camera-rays-v1";
}

public readonly record struct RawFloat3BitsV1(uint X, uint Y, uint Z)
{
    public static RawFloat3BitsV1 FromSingle(float x, float y, float z) => new(BitConverter.SingleToUInt32Bits(x), BitConverter.SingleToUInt32Bits(y), BitConverter.SingleToUInt32Bits(z));
    internal bool IsCanonicalZero => X == 0 && Y == 0 && Z == 0;
}

/// <summary>
/// Raw values in exact Godot order: Basis.X.X/X.Y/X.Z, Basis.Y.X/Y.Y/Y.Z,
/// Basis.Z.X/Z.Y/Z.Z, Origin.X/Y/Z, Camera3D.Fov. The basis columns are not
/// transposed. IEEE-754 bits are preserved exactly.
/// </summary>
public readonly record struct CameraPoseBitsV1(uint BasisXX, uint BasisXY, uint BasisXZ, uint BasisYX, uint BasisYY, uint BasisYZ, uint BasisZX, uint BasisZY, uint BasisZZ, uint OriginX, uint OriginY, uint OriginZ, uint Fov);

public readonly record struct ScientificAcquisitionKey
{
    public ScientificAcquisitionKey(string formalPolicySha256, string geometrySnapshotSha256, string frozenBoundaryContentSha256, string boundaryExecutionAuthoritySha256, string rayGenerationConventionId, CameraPoseBitsV1 cameraPose, int filmWidth, int filmHeight, int filmStride)
    {
        FormalPolicySha256 = RequireSha256(formalPolicySha256, nameof(formalPolicySha256)); GeometrySnapshotSha256 = RequireSha256(geometrySnapshotSha256, nameof(geometrySnapshotSha256));
        FrozenBoundaryContentSha256 = RequireSha256(frozenBoundaryContentSha256, nameof(frozenBoundaryContentSha256)); BoundaryExecutionAuthoritySha256 = RequireSha256(boundaryExecutionAuthoritySha256, nameof(boundaryExecutionAuthoritySha256));
        RayGenerationConventionId = RequireCanonicalId(rayGenerationConventionId, nameof(rayGenerationConventionId));
        if (filmWidth <= 0 || filmHeight <= 0 || filmStride <= 0) throw new ArgumentOutOfRangeException(nameof(filmWidth));
        CameraPose = cameraPose; FilmWidth = filmWidth; FilmHeight = filmHeight; FilmStride = filmStride; Validate();
    }
    public string FormalPolicySha256 { get; }
    public string GeometrySnapshotSha256 { get; }
    public string FrozenBoundaryContentSha256 { get; }
    public string BoundaryExecutionAuthoritySha256 { get; }
    public string RayGenerationConventionId { get; }
    public CameraPoseBitsV1 CameraPose { get; }
    public int FilmWidth { get; }
    public int FilmHeight { get; }
    public int FilmStride { get; }
    public int RepresentativePixelCount => checked(((FilmWidth + FilmStride - 1) / FilmStride) * ((FilmHeight + FilmStride - 1) / FilmStride));
    public bool IsRepresentativePixelIndex(int pixelIndex) { if (pixelIndex < 0 || pixelIndex >= checked(FilmWidth * FilmHeight)) return false; int x = pixelIndex % FilmWidth, y = pixelIndex / FilmWidth; return x % FilmStride == 0 && y % FilmStride == 0; }
    internal void Validate() { RequireSha256(FormalPolicySha256, nameof(FormalPolicySha256)); RequireSha256(GeometrySnapshotSha256, nameof(GeometrySnapshotSha256)); RequireSha256(FrozenBoundaryContentSha256, nameof(FrozenBoundaryContentSha256)); RequireSha256(BoundaryExecutionAuthoritySha256, nameof(BoundaryExecutionAuthoritySha256)); RequireCanonicalId(RayGenerationConventionId, nameof(RayGenerationConventionId)); if (FilmWidth <= 0 || FilmHeight <= 0 || FilmStride <= 0) throw new ArgumentException("Uninitialized acquisition key."); _ = checked(FilmWidth * FilmHeight); _ = RepresentativePixelCount; }
    internal static string RequireSha256(string value, string name) { if (value is null || value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Expected 64 hexadecimal SHA-256 characters.", name); return value.ToLowerInvariant(); }
    internal static string RequireCanonicalId(string value, string name) { if (string.IsNullOrEmpty(value) || value.IndexOf('\0') >= 0) throw new ArgumentException("Canonical ID is required and cannot contain NUL.", name); return value; }
}

public readonly record struct TransportStatusAxesV1
{
    private readonly byte _initialized;
    public TransportStatusAxesV1(Pass1TransportTerminationV1 termination, TransportObservationCoverageV1 coverage, TransportRecorderCompletionV1 recorder, TransportAuthorityStatusV1 authority, TransportTransactionStatusV1 transaction) { TransportTermination = termination; ObservationCoverage = coverage; RecorderCompletion = recorder; AuthorityStatus = authority; TransactionStatus = transaction; _initialized = 1; Validate(); }
    public Pass1TransportTerminationV1 TransportTermination { get; }
    public TransportObservationCoverageV1 ObservationCoverage { get; }
    public TransportRecorderCompletionV1 RecorderCompletion { get; }
    public TransportAuthorityStatusV1 AuthorityStatus { get; }
    public TransportTransactionStatusV1 TransactionStatus { get; }
    internal void Validate() { if (_initialized != 1) throw new ArgumentException("Default transport status is invalid."); V1.RequireDefined(TransportTermination, nameof(TransportTermination)); V1.RequireFlags(ObservationCoverage, V1.AllCoverage, nameof(ObservationCoverage)); V1.RequireDefined(RecorderCompletion, nameof(RecorderCompletion)); V1.RequireDefined(AuthorityStatus, nameof(AuthorityStatus)); V1.RequireDefined(TransactionStatus, nameof(TransactionStatus)); }
}

public readonly record struct RawTerminalInputsV1
{
    private readonly byte _initialized;
    public RawTerminalInputsV1(RawTerminalFlagsV1 flags, int stepsIntegrated, uint traveledBits, bool found, TransportSurfaceClassV1 surfaceClass) { Flags = flags; StepsIntegrated = stepsIntegrated; TraveledBits = traveledBits; Found = found; SurfaceClass = surfaceClass; _initialized = 1; Validate(); }
    public RawTerminalFlagsV1 Flags { get; }
    public int StepsIntegrated { get; }
    public uint TraveledBits { get; }
    public bool Found { get; }
    public TransportSurfaceClassV1 SurfaceClass { get; }
    internal void Validate() { if (_initialized != 1) throw new ArgumentException("Default terminal inputs are invalid."); V1.RequireFlags(Flags, V1.AllTerminalFlags, nameof(Flags)); if (StepsIntegrated < 0) throw new ArgumentOutOfRangeException(nameof(StepsIntegrated)); V1.RequireDefined(SurfaceClass, nameof(SurfaceClass)); if (Found && SurfaceClass == TransportSurfaceClassV1.None) throw new ArgumentException("Found=true requires Geometry, Background, or Unknown surface class."); if (!Found && SurfaceClass != TransportSurfaceClassV1.None) throw new ArgumentException("Found=false requires None surface class."); }
}

public readonly record struct TransportEventV1
{
    private readonly byte _initialized;
    public TransportEventV1(int eventOrdinal, int integrationStepOrdinal, int sourceSegmentOrdinal, TransportEventKindV1 kind, TransportSubjectKindV1 subjectKind, CanonicalIdentityAvailabilityV1 identityAvailability, string? canonicalSubjectId, int subjectExecutionOrdinal, TransportObservabilityV1 observability, TransportEventPresenceV1 presence, TransportDecisionV1 decision, TransportReasonV1 reason, TransportEventFlagsV1 flags, RawFloat3BitsV1 prePosition, RawFloat3BitsV1 postPosition, RawFloat3BitsV1 preDirection, RawFloat3BitsV1 postDirection, RawFloat3BitsV1 contactNormal, uint pathDistanceBits, uint localSegmentParameterBits)
    { EventOrdinal = eventOrdinal; IntegrationStepOrdinal = integrationStepOrdinal; SourceSegmentOrdinal = sourceSegmentOrdinal; Kind = kind; SubjectKind = subjectKind; IdentityAvailability = identityAvailability; CanonicalSubjectId = canonicalSubjectId; SubjectExecutionOrdinal = subjectExecutionOrdinal; Observability = observability; Presence = presence; Decision = decision; Reason = reason; Flags = flags; PrePosition = prePosition; PostPosition = postPosition; PreDirection = preDirection; PostDirection = postDirection; ContactNormal = contactNormal; PathDistanceBits = pathDistanceBits; LocalSegmentParameterBits = localSegmentParameterBits; _initialized = 1; Validate(); }
    public int EventOrdinal { get; }
    public int IntegrationStepOrdinal { get; }
    public int SourceSegmentOrdinal { get; }
    public TransportEventKindV1 Kind { get; }
    public TransportSubjectKindV1 SubjectKind { get; }
    public CanonicalIdentityAvailabilityV1 IdentityAvailability { get; }
    public string? CanonicalSubjectId { get; }
    public int SubjectExecutionOrdinal { get; }
    public TransportObservabilityV1 Observability { get; }
    public TransportEventPresenceV1 Presence { get; }
    public TransportDecisionV1 Decision { get; }
    public TransportReasonV1 Reason { get; }
    public TransportEventFlagsV1 Flags { get; }
    public RawFloat3BitsV1 PrePosition { get; }
    public RawFloat3BitsV1 PostPosition { get; }
    public RawFloat3BitsV1 PreDirection { get; }
    public RawFloat3BitsV1 PostDirection { get; }
    public RawFloat3BitsV1 ContactNormal { get; }
    public uint PathDistanceBits { get; }
    public uint LocalSegmentParameterBits { get; }
    internal void Validate()
    {
        if (_initialized != 1) throw new ArgumentException("Default transport event is invalid."); if (EventOrdinal < 0 || IntegrationStepOrdinal < 0 || SourceSegmentOrdinal < -1) throw new ArgumentOutOfRangeException(nameof(EventOrdinal));
        V1.RequireDefined(Kind, nameof(Kind)); V1.RequireDefined(SubjectKind, nameof(SubjectKind)); V1.RequireDefined(IdentityAvailability, nameof(IdentityAvailability)); V1.RequireDefined(Decision, nameof(Decision)); V1.RequireDefined(Reason, nameof(Reason));
        if (Kind == TransportEventKindV1.Unknown) throw new ArgumentException("Unknown event kind is not recordable.");
        V1.RequireFlags(Observability, V1.AllObservability, nameof(Observability)); V1.RequireFlags(Presence, V1.AllPresence, nameof(Presence)); V1.RequireFlags(Flags, V1.AllEventFlags, nameof(Flags));
        ValidateIdentity(); Payload(TransportEventPresenceV1.PrePosition, PrePosition, nameof(PrePosition)); Payload(TransportEventPresenceV1.PostPosition, PostPosition, nameof(PostPosition)); Payload(TransportEventPresenceV1.PreDirection, PreDirection, nameof(PreDirection)); Payload(TransportEventPresenceV1.PostDirection, PostDirection, nameof(PostDirection)); Payload(TransportEventPresenceV1.ContactNormal, ContactNormal, nameof(ContactNormal)); Payload(TransportEventPresenceV1.PathDistance, PathDistanceBits, nameof(PathDistanceBits)); Payload(TransportEventPresenceV1.LocalSegmentParameter, LocalSegmentParameterBits, nameof(LocalSegmentParameterBits));
        bool diagnosticOnly = Has(TransportObservabilityV1.DiagnosticGodotWitness) && !Has(TransportObservabilityV1.ValidatedSpatialContactAuthority);
        if (diagnosticOnly && (Presence & (TransportEventPresenceV1.PrePosition | TransportEventPresenceV1.PostPosition | TransportEventPresenceV1.ContactNormal | TransportEventPresenceV1.PathDistance | TransportEventPresenceV1.LocalSegmentParameter)) != 0) throw new ArgumentException("Unvalidated Godot hit details belong in DiagnosticRenderAppendixV1.");
        ValidateKind();
    }
    private void ValidateIdentity() { if (SubjectKind == TransportSubjectKindV1.None) { if (IdentityAvailability != CanonicalIdentityAvailabilityV1.NotApplicable || CanonicalSubjectId is not null || SubjectExecutionOrdinal != -1) throw new ArgumentException("No subject requires NotApplicable identity and ordinal -1."); return; } if (IdentityAvailability == CanonicalIdentityAvailabilityV1.Available) { ScientificAcquisitionKey.RequireCanonicalId(CanonicalSubjectId!, nameof(CanonicalSubjectId)); if (SubjectExecutionOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(SubjectExecutionOrdinal)); } else { if (CanonicalSubjectId is not null || SubjectExecutionOrdinal != -1) throw new ArgumentException("Unavailable identity cannot carry ID or ordinal."); } if (SubjectKind == TransportSubjectKindV1.Boundary && IdentityAvailability != CanonicalIdentityAvailabilityV1.Available) throw new ArgumentException("Boundary requires canonical identity."); if (SubjectKind == TransportSubjectKindV1.FieldSource && IdentityAvailability != CanonicalIdentityAvailabilityV1.Unavailable) throw new ArgumentException("FieldSource identity is explicitly unavailable in V1."); if (SubjectKind == TransportSubjectKindV1.GeometryPrimitive && IdentityAvailability == CanonicalIdentityAvailabilityV1.Available && !Has(TransportObservabilityV1.ValidatedSpatialContactAuthority)) throw new ArgumentException("Geometry identity requires validated authority."); }
    private void Payload(TransportEventPresenceV1 f, RawFloat3BitsV1 v, string n) { if ((Presence & f) == 0 && !v.IsCanonicalZero) throw new ArgumentException($"Absent {n} must be canonical zero."); }
    private void Payload(TransportEventPresenceV1 f, uint v, string n) { if ((Presence & f) == 0 && v != 0) throw new ArgumentException($"Absent {n} must be canonical zero."); }
    private bool Has(TransportObservabilityV1 f) => (Observability & f) != 0; private static void Need(bool c, string m) { if (!c) throw new ArgumentException(m); }
    private void ValidateKind()
    {
        bool exact = Has(TransportObservabilityV1.ExactMutation), point = Has(TransportObservabilityV1.PointSample), sampled = Has(TransportObservabilityV1.SampledIntervalTransition), sim = Has(TransportObservabilityV1.ExecutionOrderedSimultaneous), syn = Has(TransportObservabilityV1.SyntheticPairedEgress), sup = Has(TransportObservabilityV1.SuppressedMaskTransition), diag = Has(TransportObservabilityV1.DiagnosticGodotWitness), valid = Has(TransportObservabilityV1.ValidatedSpatialContactAuthority);
        if (sim && Kind is not (TransportEventKindV1.BoundaryCrossingApplied or TransportEventKindV1.BoundaryRemapApplied or TransportEventKindV1.BoundarySyntheticEgress or TransportEventKindV1.SuppressedMaskTransition or TransportEventKindV1.DielectricApplied)) throw new ArgumentException("Simultaneous ordering applies only to boundary execution events.");
        switch (Kind)
        {
            case TransportEventKindV1.AbsorptionObserved: Need(SubjectKind == TransportSubjectKindV1.FieldSource && point && Decision == TransportDecisionV1.TransportTerminated && Reason == TransportReasonV1.InsideAbsorbingRadius, "Invalid absorption event."); break;
            case TransportEventKindV1.ContinuousBiasApplied: Need(SubjectKind == TransportSubjectKindV1.Boundary && exact && point && Decision == TransportDecisionV1.StateMutated && Reason == TransportReasonV1.ContinuousDirectionBias, "Invalid continuous bias."); break;
            case TransportEventKindV1.BoundaryCrossingApplied: Need(SubjectKind == TransportSubjectKindV1.Boundary && exact && sampled && Decision == TransportDecisionV1.StateMutated && Reason is TransportReasonV1.BoundaryEntry or TransportReasonV1.BoundaryExit && !syn && !sup, "Invalid boundary crossing."); break;
            case TransportEventKindV1.BoundaryRemapApplied: Need(SubjectKind == TransportSubjectKindV1.Boundary && exact && sampled && Decision == TransportDecisionV1.StateMutated && Reason == TransportReasonV1.SceneTransform && !sup, "Invalid boundary remap."); break;
            case TransportEventKindV1.BoundarySyntheticEgress: Need(SubjectKind == TransportSubjectKindV1.Boundary && !exact && sampled && syn && Decision == TransportDecisionV1.NoTransportEffect && Reason == TransportReasonV1.BoundaryExit && (Presence & (TransportEventPresenceV1.PrePosition | TransportEventPresenceV1.PostPosition | TransportEventPresenceV1.PreDirection | TransportEventPresenceV1.PostDirection)) == 0, "Synthetic egress is observed bookkeeping, not a transport mutation."); break;
            case TransportEventKindV1.SuppressedMaskTransition: Need(SubjectKind == TransportSubjectKindV1.Boundary && sampled && sup && !exact && Decision == TransportDecisionV1.NoTransportEffect && Reason is TransportReasonV1.BoundaryEntry or TransportReasonV1.BoundaryExit, "Invalid suppressed transition."); break;
            case TransportEventKindV1.NumericalClampApplied: Need(exact && Decision == TransportDecisionV1.StateMutated && Reason is TransportReasonV1.NonFiniteAcceleration or TransportReasonV1.AccelerationMagnitudeClamp, "Invalid numerical clamp."); break;
            case TransportEventKindV1.ProbeQuestionIssued: Need(SubjectKind == TransportSubjectKindV1.None && point && Decision == TransportDecisionV1.QueryIssued && Reason == TransportReasonV1.ProbeCadence, "Invalid probe question."); break;
            case TransportEventKindV1.ProbeQueryFailed: Need(SubjectKind == TransportSubjectKindV1.None && point && Decision == TransportDecisionV1.QueryFailed && Reason == TransportReasonV1.PhysicsQueryFailure, "Invalid query failure."); break;
            case TransportEventKindV1.GeometryContactObserved:
                Need(SubjectKind == TransportSubjectKindV1.GeometryPrimitive && !exact && Decision == TransportDecisionV1.NoTransportEffect && Reason == TransportReasonV1.ProbeCadence && (diag || valid), "Geometry contact is observation-only.");
                Need((Presence & (TransportEventPresenceV1.PrePosition | TransportEventPresenceV1.PostPosition | TransportEventPresenceV1.ContactNormal | TransportEventPresenceV1.PathDistance | TransportEventPresenceV1.LocalSegmentParameter)) == 0, "Raw geometry-witness hit details are diagnostic-only.");
                break;
            case TransportEventKindV1.DielectricApplied: Need(SubjectKind == TransportSubjectKindV1.Boundary && exact && sampled && Reason is TransportReasonV1.PerfectMirror or TransportReasonV1.DielectricRefraction or TransportReasonV1.TotalInternalReflectionCandidate, "Invalid dielectric event."); if (Reason == TransportReasonV1.TotalInternalReflectionCandidate) Need(Decision == TransportDecisionV1.DegenerateDirection && (Flags & (TransportEventFlagsV1.DegenerateDirection | TransportEventFlagsV1.TirCandidate)) == (TransportEventFlagsV1.DegenerateDirection | TransportEventFlagsV1.TirCandidate), "TIR candidate must remain degenerate."); else Need(Decision == TransportDecisionV1.StateMutated && (Flags & TransportEventFlagsV1.TirCandidate) == 0, "Invalid non-TIR dielectric event."); break;
            case TransportEventKindV1.Terminal: Need(SubjectKind == TransportSubjectKindV1.None && Decision == TransportDecisionV1.TransportTerminated && Reason is TransportReasonV1.InsideAbsorbingRadius or TransportReasonV1.MaxDistance or TransportReasonV1.MaxSteps or TransportReasonV1.SegmentCapacity, "Invalid terminal event."); break;
        }
        if ((diag || valid) && Kind != TransportEventKindV1.GeometryContactObserved) throw new ArgumentException("Geometry witness observability applies only to GeometryContactObserved.");
    }
}

public readonly record struct QuestionAuthorityLinkV1
{
    private readonly byte _initialized;
    public QuestionAuthorityLinkV1(int questionOrdinal, int probeStep, QuestionAuthorityDispositionV1 disposition, string? canonicalPrimitiveId) { QuestionOrdinal = questionOrdinal; ProbeStep = probeStep; Disposition = disposition; CanonicalPrimitiveId = canonicalPrimitiveId; _initialized = 1; Validate(); }
    public int QuestionOrdinal { get; }
    public int ProbeStep { get; }
    public QuestionAuthorityDispositionV1 Disposition { get; }
    public string? CanonicalPrimitiveId { get; }
    internal void Validate() { if (_initialized != 1) throw new ArgumentException("Default question link is invalid."); if (QuestionOrdinal < 0 || ProbeStep < 0) throw new ArgumentOutOfRangeException(nameof(QuestionOrdinal)); V1.RequireDefined(Disposition, nameof(Disposition)); if (Disposition == QuestionAuthorityDispositionV1.ValidatedContact) ScientificAcquisitionKey.RequireCanonicalId(CanonicalPrimitiveId!, nameof(CanonicalPrimitiveId)); else if (CanonicalPrimitiveId is not null) throw new ArgumentException("Only validated contact may carry primitive ID."); }
}

public sealed class ScientificPixelPathV1
{
    private readonly ReadOnlyCollection<TransportEventV1> _events; private readonly ReadOnlyCollection<QuestionAuthorityLinkV1> _questions;
    public ScientificPixelPathV1(int pixelIndex, TransportStatusAxesV1 status, RawTerminalInputsV1 terminalInputs, IEnumerable<TransportEventV1> events, IEnumerable<QuestionAuthorityLinkV1> questions) { if (pixelIndex < 0) throw new ArgumentOutOfRangeException(nameof(pixelIndex)); status.Validate(); terminalInputs.Validate(); var ec = (events ?? throw new ArgumentNullException(nameof(events))).ToArray(); for (int i = 0; i < ec.Length; i++) { ec[i].Validate(); if (ec[i].EventOrdinal != i) throw new ArgumentException("Event ordinals must be contiguous."); } var qc = (questions ?? throw new ArgumentNullException(nameof(questions))).OrderBy(q => q.QuestionOrdinal).ToArray(); for (int i = 0; i < qc.Length; i++) { qc[i].Validate(); if (qc[i].QuestionOrdinal != i) throw new ArgumentException("Question ordinals must be contiguous."); } PixelIndex = pixelIndex; Status = status; TerminalInputs = terminalInputs; _events = Array.AsReadOnly(ec); _questions = Array.AsReadOnly(qc); }
    public int PixelIndex { get; }
    public TransportStatusAxesV1 Status { get; }
    public RawTerminalInputsV1 TerminalInputs { get; }
    public IReadOnlyList<TransportEventV1> Events => _events; public IReadOnlyList<QuestionAuthorityLinkV1> QuestionAuthorityLinks => _questions;
    internal void Validate() { if (PixelIndex < 0) throw new ArgumentOutOfRangeException(nameof(PixelIndex)); Status.Validate(); TerminalInputs.Validate(); for (int i = 0; i < _events.Count; i++) { _events[i].Validate(); if (_events[i].EventOrdinal != i) throw new ArgumentException("Event ordinal gap."); } for (int i = 0; i < _questions.Count; i++) { _questions[i].Validate(); if (_questions[i].QuestionOrdinal != i) throw new ArgumentException("Question ordinal gap."); } }
}

public sealed class ScientificPixelCommitV1 { public ScientificPixelCommitV1(uint commitVersion, ScientificPixelPathV1 path) { if (commitVersion == 0) throw new ArgumentOutOfRangeException(nameof(commitVersion)); CommitVersion = commitVersion; Path = path ?? throw new ArgumentNullException(nameof(path)); } public uint CommitVersion { get; } public ScientificPixelPathV1 Path { get; } }

public sealed class ScientificTransportLedgerV1
{
    public const string SchemaId = "ScientificTransportLedger-v1-le"; private readonly ReadOnlyCollection<ScientificPixelCommitV1> _pixels;
    public ScientificTransportLedgerV1(ScientificAcquisitionKey key, IEnumerable<ScientificPixelCommitV1> pixels) { key.Validate(); var c = (pixels ?? throw new ArgumentNullException(nameof(pixels))).OrderBy(p => p.Path.PixelIndex).ToArray(); for (int i = 0; i < c.Length; i++) { if (c[i] is null) throw new ArgumentException("Null commit."); c[i].Path.Validate(); if (!key.IsRepresentativePixelIndex(c[i].Path.PixelIndex)) throw new ArgumentOutOfRangeException(nameof(pixels), "Pixel is outside film or not a stride representative."); if (i > 0 && c[i - 1].Path.PixelIndex == c[i].Path.PixelIndex) throw new ArgumentException("Duplicate pixel commit."); } AcquisitionKey = key; _pixels = Array.AsReadOnly(c); }
    public ScientificAcquisitionKey AcquisitionKey { get; }
    public IReadOnlyList<ScientificPixelCommitV1> Pixels => _pixels;
    public SealedScientificTransportLedgerV1 Seal() { ValidateStructure(); if (_pixels.Count != AcquisitionKey.RepresentativePixelCount) throw new InvalidOperationException("Empty or partial ledger is not sealable."); int n = 0; for (int y = 0; y < AcquisitionKey.FilmHeight; y += AcquisitionKey.FilmStride) for (int x = 0; x < AcquisitionKey.FilmWidth; x += AcquisitionKey.FilmStride) { int expected = checked(y * AcquisitionKey.FilmWidth + x); var p = _pixels[n++].Path; if (p.PixelIndex != expected) throw new InvalidOperationException("Representative pixel coverage is incomplete."); if (p.Status.RecorderCompletion != TransportRecorderCompletionV1.Complete) throw new InvalidOperationException("Incomplete recorder evidence is not sealable."); if (p.Status.AuthorityStatus is not (TransportAuthorityStatusV1.Validated or TransportAuthorityStatusV1.NotRequired)) throw new InvalidOperationException("Unvalidated authority is not sealable."); if (p.Status.TransactionStatus != TransportTransactionStatusV1.AuthorityFinalized) throw new InvalidOperationException("Only authority-finalized transactions are sealable."); if (p.Status.TransportTermination == Pass1TransportTerminationV1.None) throw new InvalidOperationException("Missing pass-1 termination."); if (p.QuestionAuthorityLinks.Any(q => q.Disposition is QuestionAuthorityDispositionV1.Pending or QuestionAuthorityDispositionV1.Mismatch or QuestionAuthorityDispositionV1.Unavailable)) throw new InvalidOperationException("Unresolved question authority."); ValidateFinalEvidenceCoherence(p); } return new(this); }
    private static void ValidateFinalEvidenceCoherence(ScientificPixelPathV1 path)
    {
        RawTerminalFlagsV1 terminalMask = RawTerminalFlagsV1.Absorbed | RawTerminalFlagsV1.MaxDistanceReached | RawTerminalFlagsV1.MaxStepsReached | RawTerminalFlagsV1.SegmentCapacityTruncated;
        RawTerminalFlagsV1 expected = path.Status.TransportTermination switch { Pass1TransportTerminationV1.AbsorbedAtSample => RawTerminalFlagsV1.Absorbed, Pass1TransportTerminationV1.MaxDistance => RawTerminalFlagsV1.MaxDistanceReached, Pass1TransportTerminationV1.MaxSteps => RawTerminalFlagsV1.MaxStepsReached, Pass1TransportTerminationV1.SegmentCapacityTruncated => RawTerminalFlagsV1.SegmentCapacityTruncated, _ => RawTerminalFlagsV1.None };
        if ((path.TerminalInputs.Flags & terminalMask) != expected) throw new InvalidOperationException($"Pixel {path.PixelIndex} termination and raw terminal flags disagree.");
        TransportEventV1[] terminalEvents = path.Events.Where(e => e.Kind == TransportEventKindV1.Terminal).ToArray();
        if (terminalEvents.Length > 1 || terminalEvents.Length == 1 && terminalEvents[0].EventOrdinal != path.Events.Count - 1) throw new InvalidOperationException($"Pixel {path.PixelIndex} Terminal event must be unique and final.");
        if (terminalEvents.Length == 1)
        {
            TransportReasonV1 expectedReason = path.Status.TransportTermination switch { Pass1TransportTerminationV1.AbsorbedAtSample => TransportReasonV1.InsideAbsorbingRadius, Pass1TransportTerminationV1.MaxDistance => TransportReasonV1.MaxDistance, Pass1TransportTerminationV1.MaxSteps => TransportReasonV1.MaxSteps, Pass1TransportTerminationV1.SegmentCapacityTruncated => TransportReasonV1.SegmentCapacity, _ => TransportReasonV1.None };
            if (terminalEvents[0].Reason != expectedReason) throw new InvalidOperationException($"Pixel {path.PixelIndex} Terminal event disagrees with termination classification.");
        }
        foreach (TransportEventV1 geometry in path.Events.Where(e => e.Kind == TransportEventKindV1.GeometryContactObserved))
        {
            QuestionAuthorityLinkV1[] matches = path.QuestionAuthorityLinks.Where(q => q.ProbeStep == geometry.IntegrationStepOrdinal).ToArray();
            if (matches.Length != 1 || matches[0].Disposition != QuestionAuthorityDispositionV1.ValidatedContact || geometry.IdentityAvailability != CanonicalIdentityAvailabilityV1.Available || !string.Equals(matches[0].CanonicalPrimitiveId, geometry.CanonicalSubjectId, StringComparison.Ordinal)) throw new InvalidOperationException($"Pixel {path.PixelIndex} geometry observation lacks one matching validated question.");
        }
        foreach (QuestionAuthorityLinkV1 question in path.QuestionAuthorityLinks.Where(q => q.Disposition == QuestionAuthorityDispositionV1.ValidatedContact))
        {
            int matches = path.Events.Count(e => e.Kind == TransportEventKindV1.GeometryContactObserved && e.IntegrationStepOrdinal == question.ProbeStep && string.Equals(e.CanonicalSubjectId, question.CanonicalPrimitiveId, StringComparison.Ordinal));
            if (matches != 1) throw new InvalidOperationException($"Pixel {path.PixelIndex} validated contact lacks one matching geometry observation.");
        }
    }
    internal void ValidateStructure() { AcquisitionKey.Validate(); for (int i = 0; i < _pixels.Count; i++) { var c = _pixels[i] ?? throw new ArgumentException("Null commit."); if (c.CommitVersion == 0) throw new ArgumentException("Invalid commit version."); c.Path.Validate(); if (!AcquisitionKey.IsRepresentativePixelIndex(c.Path.PixelIndex)) throw new ArgumentOutOfRangeException(nameof(Pixels)); if (i > 0 && _pixels[i - 1].Path.PixelIndex >= c.Path.PixelIndex) throw new ArgumentException("Pixels are not unique and sorted."); } }
}
public sealed class SealedScientificTransportLedgerV1 { internal SealedScientificTransportLedgerV1(ScientificTransportLedgerV1 ledger) => Ledger = ledger; public ScientificTransportLedgerV1 Ledger { get; } }

public readonly record struct RuntimeProvenanceEntryV1
{ private readonly byte _initialized; public RuntimeProvenanceEntryV1(string key, string value) { Key = key; Value = value; _initialized = 1; Validate(); } public string Key { get; } public string Value { get; } internal void Validate() { if (_initialized != 1) throw new ArgumentException("Default provenance entry is invalid."); if (string.IsNullOrEmpty(Key) || Key.IndexOf('\0') >= 0) throw new ArgumentException("Invalid provenance key."); if (Value is null) throw new ArgumentNullException(nameof(Value)); } }

public readonly record struct DiagnosticRenderObservationV1
{
    private readonly byte _initialized;
    public DiagnosticRenderObservationV1(int ordinal, int sourceSegment, DiagnosticRenderObservationKindV1 kind, DiagnosticRenderObservationFlagsV1 flags, RawFloat3BitsV1 position, RawFloat3BitsV1 normal, ulong collider) { ObservationOrdinal = ordinal; SourceSegmentOrdinal = sourceSegment; Kind = kind; Flags = flags; Position = position; Normal = normal; RuntimeColliderInstanceId = collider; _initialized = 1; Validate(); }
    public int ObservationOrdinal { get; }
    public int SourceSegmentOrdinal { get; }
    public DiagnosticRenderObservationKindV1 Kind { get; }
    public DiagnosticRenderObservationFlagsV1 Flags { get; }
    public RawFloat3BitsV1 Position { get; }
    public RawFloat3BitsV1 Normal { get; }
    public ulong RuntimeColliderInstanceId { get; }
    internal void Validate() { if (_initialized != 1) throw new ArgumentException("Default diagnostic observation is invalid."); if (ObservationOrdinal < 0 || SourceSegmentOrdinal < -1) throw new ArgumentOutOfRangeException(nameof(ObservationOrdinal)); V1.RequireDefined(Kind, nameof(Kind)); if (Kind == DiagnosticRenderObservationKindV1.Unknown) throw new ArgumentException("Unknown diagnostic kind."); V1.RequireFlags(Flags, V1.AllDiagnosticFlags, nameof(Flags)); if ((Flags & DiagnosticRenderObservationFlagsV1.PostTerminalDiagnostic) == 0) throw new ArgumentException("Appendix observation must be post-terminal."); if ((Flags & DiagnosticRenderObservationFlagsV1.HasPosition) == 0 && !Position.IsCanonicalZero) throw new ArgumentException("Absent position must be zero."); if ((Flags & DiagnosticRenderObservationFlagsV1.HasNormal) == 0 && !Normal.IsCanonicalZero) throw new ArgumentException("Absent normal must be zero."); if ((Flags & DiagnosticRenderObservationFlagsV1.RuntimeGodotWitness) == 0 && RuntimeColliderInstanceId != 0) throw new ArgumentException("Collider ID requires runtime witness."); }
}
public sealed class DiagnosticRenderPixelV1
{ private readonly ReadOnlyCollection<DiagnosticRenderObservationV1> _observations; public DiagnosticRenderPixelV1(int pixel, uint version, IEnumerable<DiagnosticRenderObservationV1> observations) { if (pixel < 0 || version == 0) throw new ArgumentOutOfRangeException(nameof(pixel)); var c = (observations ?? throw new ArgumentNullException(nameof(observations))).ToArray(); for (int i = 0; i < c.Length; i++) { c[i].Validate(); if (c[i].ObservationOrdinal != i) throw new ArgumentException("Diagnostic ordinal gap."); } PixelIndex = pixel; LinkedCommitVersion = version; _observations = Array.AsReadOnly(c); } public int PixelIndex { get; } public uint LinkedCommitVersion { get; } public IReadOnlyList<DiagnosticRenderObservationV1> Observations => _observations; internal void Validate() { if (PixelIndex < 0 || LinkedCommitVersion == 0) throw new ArgumentException("Invalid diagnostic link."); for (int i = 0; i < _observations.Count; i++) { _observations[i].Validate(); if (_observations[i].ObservationOrdinal != i) throw new ArgumentException("Diagnostic ordinal gap."); } } }

public sealed class DiagnosticRenderAppendixV1
{
    public const string SchemaId = "DiagnosticRenderAppendix-v1-le"; private readonly ReadOnlyCollection<RuntimeProvenanceEntryV1> _provenance; private readonly ReadOnlyCollection<DiagnosticRenderPixelV1> _pixels;
    public DiagnosticRenderAppendixV1(SealedScientificTransportLedgerV1 sealedScientificLedger, int generation, DiagnosticRenderAppendixStatusV1 status, IEnumerable<RuntimeProvenanceEntryV1> provenance, IEnumerable<DiagnosticRenderPixelV1> pixels) { ArgumentNullException.ThrowIfNull(sealedScientificLedger); ScientificContentSha256 = TransportLedgerV1Serializer.ComputeSealedScientificContentSha256(sealedScientificLedger); if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation)); V1.RequireDefined(status, nameof(status)); var p = (provenance ?? throw new ArgumentNullException(nameof(provenance))).OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(); for (int i = 0; i < p.Length; i++) { p[i].Validate(); if (i > 0 && p[i - 1].Key == p[i].Key) throw new ArgumentException("Duplicate provenance key."); } var q = (pixels ?? throw new ArgumentNullException(nameof(pixels))).OrderBy(x => x.PixelIndex).ToArray(); for (int i = 0; i < q.Length; i++) { if (q[i] is null) throw new ArgumentException("Null diagnostic pixel."); q[i].Validate(); if (i > 0 && q[i - 1].PixelIndex == q[i].PixelIndex) throw new ArgumentException("Duplicate diagnostic pixel."); } RuntimeAcquisitionGeneration = generation; Status = status; _provenance = Array.AsReadOnly(p); _pixels = Array.AsReadOnly(q); }
    public string ScientificContentSha256 { get; }
    public int RuntimeAcquisitionGeneration { get; }
    public DiagnosticRenderAppendixStatusV1 Status { get; }
    public IReadOnlyList<RuntimeProvenanceEntryV1> RuntimeProvenance => _provenance; public IReadOnlyList<DiagnosticRenderPixelV1> Pixels => _pixels;
    internal void Validate() { ScientificAcquisitionKey.RequireSha256(ScientificContentSha256, nameof(ScientificContentSha256)); if (RuntimeAcquisitionGeneration < 0) throw new ArgumentOutOfRangeException(); V1.RequireDefined(Status, nameof(Status)); foreach (var p in _provenance) p.Validate(); foreach (var p in _pixels) p.Validate(); }
}
public sealed class TransportLedgerArtifactV1 { public const string SchemaId = "TransportLedgerArtifact-v1-le"; public TransportLedgerArtifactV1(ScientificTransportLedgerV1 ledger, DiagnosticRenderAppendixV1? appendix) { ScientificLedger = ledger ?? throw new ArgumentNullException(nameof(ledger)); DiagnosticAppendix = appendix; } public ScientificTransportLedgerV1 ScientificLedger { get; } public DiagnosticRenderAppendixV1? DiagnosticAppendix { get; } }

internal static class V1
{
    internal const TransportObservabilityV1 AllObservability = (TransportObservabilityV1)0xff; internal const TransportEventPresenceV1 AllPresence = (TransportEventPresenceV1)0x7f; internal const TransportEventFlagsV1 AllEventFlags = (TransportEventFlagsV1)0x7; internal const TransportObservationCoverageV1 AllCoverage = (TransportObservationCoverageV1)0x1f; internal const RawTerminalFlagsV1 AllTerminalFlags = (RawTerminalFlagsV1)0xff; internal const DiagnosticRenderObservationFlagsV1 AllDiagnosticFlags = (DiagnosticRenderObservationFlagsV1)0xf;
    internal static void RequireDefined<T>(T value, string name) where T : struct, Enum { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(name, value, "Undefined enum value."); }
    internal static void RequireFlags<T>(T value, T allowed, string name) where T : struct, Enum { ulong raw = Convert.ToUInt64(value), mask = Convert.ToUInt64(allowed); if ((raw & ~mask) != 0) throw new ArgumentOutOfRangeException(name, value, "Unknown flag bits."); }
}
