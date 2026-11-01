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
        IEnumerable<FormalTransportObservationPixelV1> pixels)
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
        _pixels = Array.AsReadOnly(ordered);
    }

    public ulong CaptureGeneration { get; }
    public bool RecorderEnabled { get; }
    public int AttemptAllocationCount { get; }
    public int RecordedEventCount { get; }
    public int FinalizationEligibleCommitCount { get; }
    public int AcceptedFinalizationCount { get; }
    public int StaleFinalizationRejectCount { get; }
    public IReadOnlyList<FormalTransportObservationPixelV1> Pixels => _pixels;
}
