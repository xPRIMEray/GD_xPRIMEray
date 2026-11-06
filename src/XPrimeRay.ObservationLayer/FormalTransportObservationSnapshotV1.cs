using System.Collections.ObjectModel;

namespace XPrimeRay.ObservationLayer;

/// <summary>
/// Immutable, acquisition-owned view of the provisional Formal transport recorder.
/// This is an export input, not a sealed scientific ledger.
/// </summary>
public sealed class FormalTransportObservationPixelV1
{
    private readonly ReadOnlyCollection<TransportEventKindV1> _eventKinds;

    public FormalTransportObservationPixelV1(
        int pixelIndex,
        ulong captureGeneration,
        uint commitVersion,
        bool finalized,
        TransportRecorderCompletionV1 recorderCompletion,
        string provisionalReason,
        int eventCapacity,
        bool overflowed,
        IEnumerable<TransportEventKindV1> eventKinds)
    {
        if (pixelIndex < 0) throw new ArgumentOutOfRangeException(nameof(pixelIndex));
        if (captureGeneration == 0) throw new ArgumentOutOfRangeException(nameof(captureGeneration));
        if (commitVersion == 0) throw new ArgumentOutOfRangeException(nameof(commitVersion));
        if (!Enum.IsDefined(recorderCompletion)) throw new ArgumentOutOfRangeException(nameof(recorderCompletion));
        if (string.IsNullOrWhiteSpace(provisionalReason)) throw new ArgumentException("Provisional reason is required.", nameof(provisionalReason));
        if (eventCapacity < 0) throw new ArgumentOutOfRangeException(nameof(eventCapacity));

        TransportEventKindV1[] kinds = (eventKinds ?? throw new ArgumentNullException(nameof(eventKinds))).ToArray();
        foreach (TransportEventKindV1 kind in kinds)
            if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(eventKinds), kind, "Undefined event kind.");
        if (kinds.Length > eventCapacity) throw new ArgumentException("Event count exceeds recorder capacity.", nameof(eventKinds));

        PixelIndex = pixelIndex;
        CaptureGeneration = captureGeneration;
        CommitVersion = commitVersion;
        Finalized = finalized;
        RecorderCompletion = recorderCompletion;
        ProvisionalReason = provisionalReason;
        EventCapacity = eventCapacity;
        Overflowed = overflowed;
        _eventKinds = Array.AsReadOnly(kinds);
    }

    public int PixelIndex { get; }
    public ulong CaptureGeneration { get; }
    public uint CommitVersion { get; }
    public bool Finalized { get; }
    public TransportRecorderCompletionV1 RecorderCompletion { get; }
    public string ProvisionalReason { get; }
    public int EventCapacity { get; }
    public bool Overflowed { get; }
    public IReadOnlyList<TransportEventKindV1> EventKinds => _eventKinds;

    public bool RecordedCompleteForAttempt =>
        Finalized &&
        !Overflowed &&
        string.Equals(ProvisionalReason, "provisional_event_coverage", StringComparison.Ordinal);
}

/// <summary>
/// Immutable snapshot of the current whole-pixel recorder slots for one capture generation.
/// </summary>
public sealed class FormalTransportObservationSnapshotV1
{
    private readonly ReadOnlyCollection<FormalTransportObservationPixelV1> _pixels;

    public FormalTransportObservationSnapshotV1(
        ulong captureGeneration,
        bool recorderEnabled,
        int attemptAllocations,
        int recordedEvents,
        int finalizationEligibleCommits,
        int acceptedFinalizations,
        int staleFinalizationRejects,
        IEnumerable<FormalTransportObservationPixelV1> pixels,
        FormalTransportPixelMeasurementsV1? measurements = null)
    {
        if (captureGeneration == 0) throw new ArgumentOutOfRangeException(nameof(captureGeneration));
        if (attemptAllocations < 0 || recordedEvents < 0 || finalizationEligibleCommits < 0 ||
            acceptedFinalizations < 0 || staleFinalizationRejects < 0)
            throw new ArgumentOutOfRangeException(nameof(attemptAllocations), "Recorder counters cannot be negative.");

        FormalTransportObservationPixelV1[] ordered = (pixels ?? throw new ArgumentNullException(nameof(pixels)))
            .OrderBy(pixel => pixel.PixelIndex)
            .ToArray();
        for (int i = 0; i < ordered.Length; i++)
        {
            if (i > 0 && ordered[i - 1].PixelIndex == ordered[i].PixelIndex)
                throw new ArgumentException("Duplicate pixel index.", nameof(pixels));
        }

        CaptureGeneration = captureGeneration;
        RecorderEnabled = recorderEnabled;
        AttemptAllocationCount = attemptAllocations;
        RecordedEventCount = recordedEvents;
        FinalizationEligibleCommitCount = finalizationEligibleCommits;
        AcceptedFinalizationCount = acceptedFinalizations;
        StaleFinalizationRejectCount = staleFinalizationRejects;
        Measurements = measurements;
        _pixels = Array.AsReadOnly(ordered);
    }

    public ulong CaptureGeneration { get; }
    public bool RecorderEnabled { get; }
    public int AttemptAllocationCount { get; }
    public int RecordedEventCount { get; }
    public int FinalizationEligibleCommitCount { get; }
    public int AcceptedFinalizationCount { get; }
    public int StaleFinalizationRejectCount { get; }
    public FormalTransportPixelMeasurementsV1? Measurements { get; }
    public IReadOnlyList<FormalTransportObservationPixelV1> Pixels => _pixels;
}

public sealed class FormalTransportEventMeasurementV1
{
    public FormalTransportEventMeasurementV1(
        int eventOrdinal,
        int integrationStepOrdinal,
        TransportEventKindV1 kind,
        TransportReasonV1 reason)
    {
        if (eventOrdinal < 0 || integrationStepOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(eventOrdinal));
        if (!Enum.IsDefined(kind) || kind == TransportEventKindV1.Unknown) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        EventOrdinal = eventOrdinal;
        IntegrationStepOrdinal = integrationStepOrdinal;
        Kind = kind;
        Reason = reason;
    }

    public int EventOrdinal { get; }
    public int IntegrationStepOrdinal { get; }
    public TransportEventKindV1 Kind { get; }
    public TransportReasonV1 Reason { get; }
}

public sealed class FormalTransportPixelMeasurementV1
{
    private readonly ReadOnlyCollection<FormalTransportEventMeasurementV1> _events;
    private readonly ReadOnlyCollection<int> _querySteps;

    public FormalTransportPixelMeasurementV1(
        int pixelIndex,
        ulong captureGeneration,
        uint commitVersion,
        Pass1TransportTerminationV1 termination,
        RawTerminalFlagsV1 rawTerminalFlags,
        int stepsIntegrated,
        uint traveledBits,
        int questionCount,
        uint queriedChordBits,
        uint maxUnqueriedGapBits,
        IEnumerable<int> querySteps,
        IEnumerable<FormalTransportEventMeasurementV1> events)
    {
        if (pixelIndex < 0 || captureGeneration == 0 || commitVersion == 0 || stepsIntegrated < 0 || questionCount < 0)
            throw new ArgumentOutOfRangeException(nameof(pixelIndex));
        if (!Enum.IsDefined(termination)) throw new ArgumentOutOfRangeException(nameof(termination));
        int[] steps = (querySteps ?? throw new ArgumentNullException(nameof(querySteps))).ToArray();
        if (steps.Any(step => step < 0)) throw new ArgumentException("Query steps must be nonnegative.", nameof(querySteps));
        FormalTransportEventMeasurementV1[] eventArray = (events ?? throw new ArgumentNullException(nameof(events))).ToArray();
        for (int i = 0; i < eventArray.Length; i++)
            if (eventArray[i].EventOrdinal != i) throw new ArgumentException("Event ordinals must be contiguous.", nameof(events));
        PixelIndex = pixelIndex;
        CaptureGeneration = captureGeneration;
        CommitVersion = commitVersion;
        Termination = termination;
        RawTerminalFlags = rawTerminalFlags;
        StepsIntegrated = stepsIntegrated;
        TraveledBits = traveledBits;
        QuestionCount = questionCount;
        QueriedChordBits = queriedChordBits;
        MaxUnqueriedGapBits = maxUnqueriedGapBits;
        _querySteps = Array.AsReadOnly(steps);
        _events = Array.AsReadOnly(eventArray);
    }

    public int PixelIndex { get; }
    public ulong CaptureGeneration { get; }
    public uint CommitVersion { get; }
    public Pass1TransportTerminationV1 Termination { get; }
    public RawTerminalFlagsV1 RawTerminalFlags { get; }
    public int StepsIntegrated { get; }
    public uint TraveledBits { get; }
    public int QuestionCount { get; }
    public uint QueriedChordBits { get; }
    public uint MaxUnqueriedGapBits { get; }
    public IReadOnlyList<int> QuerySteps => _querySteps;
    public IReadOnlyList<FormalTransportEventMeasurementV1> Events => _events;
}

public sealed class FormalTransportPixelMeasurementsV1
{
    private readonly ReadOnlyCollection<FormalTransportPixelMeasurementV1> _pixels;

    public FormalTransportPixelMeasurementsV1(IEnumerable<FormalTransportPixelMeasurementV1> pixels)
    {
        FormalTransportPixelMeasurementV1[] ordered = (pixels ?? throw new ArgumentNullException(nameof(pixels)))
            .OrderBy(pixel => pixel.PixelIndex).ToArray();
        for (int i = 1; i < ordered.Length; i++)
            if (ordered[i - 1].PixelIndex == ordered[i].PixelIndex) throw new ArgumentException("Duplicate pixel measurement.", nameof(pixels));
        _pixels = Array.AsReadOnly(ordered);
    }

    public IReadOnlyList<FormalTransportPixelMeasurementV1> Pixels => _pixels;
}
