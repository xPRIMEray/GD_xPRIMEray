#nullable enable

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XPrimeRay.ObservationLayer;

namespace XPrimeRay.ObserverInstrumentation;

/// <summary>One recorded Formal probe question (chord) exactly as retained by the recorder.</summary>
public readonly record struct PixelPathQueryV1(
    int ProbeStep,
    RawFloat3BitsV1 From,
    RawFloat3BitsV1 To,
    uint TraveledBBits,
    uint ChordLengthBits,
    uint CollisionMask,
    bool CollideWithBodies,
    bool CollideWithAreas,
    bool HitFromInside,
    bool HitBackFaces);

/// <summary>
/// Recorder-owned per-pixel data copied after the seal (event arrays cloned under the recorder lock).
/// All fields are recorded values except PixelPathQueryV1.ChordLengthBits, which is derived from the
/// recorded chord endpoints with the same expression as the existing sealed coverage measurement.
/// </summary>
public sealed class PixelPathSourceV1
{
    public required int PixelIndex { get; init; }
    public required ulong CaptureGeneration { get; init; }
    public required uint CommitVersion { get; init; }
    public required bool Finalized { get; init; }
    public required bool Overflowed { get; init; }
    public required int EventCapacity { get; init; }
    public required string ProvisionalReason { get; init; }
    public required Pass1TransportTerminationV1 Termination { get; init; }
    public required RawTerminalFlagsV1 RawTerminalFlags { get; init; }
    public required int StepsIntegrated { get; init; }
    public required uint TraveledBits { get; init; }
    public required bool Found { get; init; }
    public required int QuestionCount { get; init; }
    public required IReadOnlyList<TransportEventV1> Events { get; init; }
    public required IReadOnlyList<PixelPathQueryV1> Queries { get; init; }
}

public sealed class PixelPathObservationInputV1
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
    public required string LiveQueryDatasetSha256 { get; init; }
    public required ulong LiveCaptureGeneration { get; init; }
    public required string EngineCommit { get; init; }
    public required string SourceTreeState { get; init; }
    public required string AuthoredDiffSha256 { get; init; }
    public required FormalTransportObservationSnapshotV1 Snapshot { get; init; }
    public required int[] ContactCounts { get; init; }
    public required int[] FirstContactSteps { get; init; }
    public required IReadOnlyList<PixelPathSourceV1> Sources { get; init; }
    public required int[] SelectedPixels { get; init; }

    /// <summary>
    /// Test-only fault injection: invoked with a publication stage name ("staged_paths", "staged_manifest",
    /// "verified", "before_publish"); throwing simulates a filesystem failure. Production callers leave it null.
    /// </summary>
    public Action<string>? PublicationFaultInjectionForTesting { get; init; }
}

public sealed class PixelPathObservationResultV1
{
    public required string ManifestPath { get; init; }
    public required string PixelPathsPath { get; init; }
    public required string FullFilmDigestSha256 { get; init; }
    public required int ValidatedPixelCount { get; init; }
}

/// <summary>INSPECT-R0.2: an in-memory, read-only inspection of selected pixels (nothing is written).</summary>
public sealed class PixelPathInspectionV1
{
    /// <summary>Compact JSON document (schema PixelLifeInspection-v1) for a read-only viewer.</summary>
    public required string Json { get; init; }
    public required int ValidatedPixelCount { get; init; }
    public required ulong CaptureGeneration { get; init; }
    /// <summary>SHA-256 of each selected pixel's compact canonical JSON (identical to the TryWrite manifest values).</summary>
    public required IReadOnlyDictionary<int, string> PixelCompactJsonSha256 { get; init; }
}

/// <summary>
/// INSPECT-R0.1: passive, ordered per-pixel event export from a completed, sealed Formal acquisition.
/// Observational only. Every pixel of the sealed acquisition is validated against the sealed snapshot
/// before anything is written; any mismatch refuses the whole export and writes nothing.
/// </summary>
public static class PixelPathObservationMapV1
{
    public const string SchemaId = "PixelPathObservation-v1";
    public const string ArtifactClass = "provisional_recorded_observation";

    public static readonly string[] Unavailable =
    {
        "positions/directions of integration steps and of segments that were not probed (the recorder does not retain them)",
        "field samples, accelerations and per-step step lengths (not retained)",
        "per-question hit position, normal and collider identity (raw Godot witness details are diagnostic-only and not retained)",
        "per-question validated authority (no runtime QuestionAuthorityLinkV1 is produced; per-pixel authority is the sealed BVH contact_counts channel)",
        "numerical-clamp events (NumericalClampApplied is never emitted; only RawTerminalFlags.NumericalFailure)",
        "the integration step at which zero-advance occurred (only RawTerminalFlags.ZeroAdvance)",
        "boundary or field crossings between samples (membership is point-sampled; see observability flags)",
        "continuous surface-search coverage between probe chords (query coverage is finite chords only)"
    };

    public static readonly string[] ProvenanceWarnings =
    {
        "P1: field_source_epoch omits transport-relevant field parameters (e.g. CanonicalEdgeSoftness); identical epochs do not prove identical field inputs. Not fixed in INSPECT-R0.1.",
        "P2: derivative-aware stepping is outside the Formal policy hash and the canonical reset; identical policy hashes do not prove identical stepping when non-default settings are used. Not fixed in INSPECT-R0.1.",
        "P4: field/geometry/boundary epochs are process-local keys (include node instance IDs); compare runs by array and query hashes."
    };

    public static bool TryWrite(PixelPathObservationInputV1 input, out PixelPathObservationResultV1? result, out string reason)
    {
        result = null;
        if (!ValidateInput(input, requireAttestedPublication: true, out reason)) return false;
        if (!TryValidateFilm(input, digestEveryPixel: true, out FilmBuild? build, out reason)) return false;
        int total = input.Width * input.Height;
        var pathsDoc = new Dictionary<string, object?>
        {
            ["schema"] = SchemaId,
            ["run_id"] = input.RunId,
            ["pixels"] = build!.SelectedJson.Values.ToArray()
        };
        string pathsJson = JsonSerializer.Serialize(pathsDoc, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        byte[] pathsBytes = new UTF8Encoding(false).GetBytes(pathsJson);
        var manifest = BuildManifest(input, build.Digest, build.SelectedSha, build.KindTotals, build.Questions, build.Queries, build.ContactEvents, build.SealedContacts, pathsBytes);
        string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n";

        byte[] manifestBytes = new UTF8Encoding(false).GetBytes(manifestJson);
        if (!TryPublishAtomically(input, pathsBytes, manifestBytes, out reason)) return false;
        string pathsPath = Path.Combine(input.OutputDirectory, PathsFileName);
        string manifestPath = Path.Combine(input.OutputDirectory, ManifestFileName);
        result = new PixelPathObservationResultV1
        {
            ManifestPath = manifestPath,
            PixelPathsPath = pathsPath,
            FullFilmDigestSha256 = build.Digest,
            ValidatedPixelCount = total
        };
        reason = string.Empty;
        return true;
    }

    public const string InspectionSchemaId = "PixelLifeInspection-v1";
    public const string InspectionSourceAttestation =
        "not attested: interactive in-memory inspection; source commit and tree state are attested only by a published export";

    /// <summary>
    /// INSPECT-R0.2: the same whole-film validation and per-pixel model as <see cref="TryWrite"/>, returned in
    /// memory for a read-only viewer. Nothing is written. Publication identity (output directory, run id) and
    /// source attestation are not required and are reported as not attested. Any validation failure refuses.
    /// The per-pixel objects are exactly those TryWrite would publish for the same input and selection.
    /// </summary>
    public static bool TryInspect(PixelPathObservationInputV1 input, out PixelPathInspectionV1? inspection, out string reason)
    {
        inspection = null;
        if (!ValidateInput(input, requireAttestedPublication: false, out reason)) return false;
        if (!TryValidateFilm(input, digestEveryPixel: false, out FilmBuild? build, out reason)) return false;
        int total = input.Width * input.Height;
        var doc = new Dictionary<string, object?>
        {
            ["schema"] = InspectionSchemaId,
            ["status"] = "ok",
            ["pixel_model_schema"] = SchemaId,
            ["artifact_class"] = ArtifactClass,
            ["scientific_ledger_sealed"] = false,
            ["claim_boundary"] = ClaimBoundary(),
            ["field_classes"] = FieldClasses(),
            ["provenance_warnings"] = ProvenanceWarnings,
            ["source_attestation"] = InspectionSourceAttestation,
            ["acquisition"] = Acquisition(input),
            ["validation"] = new Dictionary<string, object>
            {
                ["scope"] = "every pixel of the sealed acquisition validated against the sealed snapshot before display; any failure refuses",
                ["validated_pixels"] = total,
                ["full_film_digest_sha256"] = null,
                ["full_film_digest_scope"] = "not computed by the in-memory inspection (computed by the published export); every pixel is still validated",
                ["totals"] = new Dictionary<string, object>
                {
                    ["events_by_kind"] = build!.KindTotals,
                    ["question_events"] = build.Questions,
                    ["recorded_queries"] = build.Queries,
                    ["geometry_contact_events"] = build.ContactEvents,
                    ["sealed_bvh_contacts"] = build.SealedContacts
                }
            },
            ["unavailable"] = Unavailable,
            ["pixels"] = build!.SelectedJson.Values.ToArray()
        };
        inspection = new PixelPathInspectionV1
        {
            Json = JsonSerializer.Serialize(doc),
            ValidatedPixelCount = total,
            CaptureGeneration = input.Snapshot.CaptureGeneration,
            PixelCompactJsonSha256 = new SortedDictionary<int, string>(build.SelectedSha)
        };
        reason = string.Empty;
        return true;
    }

    private sealed class FilmBuild
    {
        public required string Digest;
        public required SortedDictionary<int, Dictionary<string, object?>> SelectedJson;
        public required SortedDictionary<int, string> SelectedSha;
        public required SortedDictionary<string, int> KindTotals;
        public long Questions, Queries, ContactEvents, SealedContacts;
    }

    // Whole-film validation shared by TryWrite and TryInspect. Every pixel is validated against the seal in both cases;
    // digestEveryPixel (TryWrite) also builds and hashes every pixel's model for the full-film digest, while TryInspect
    // builds models only for the selected pixels (identical objects) and computes no digest.
    private static bool TryValidateFilm(PixelPathObservationInputV1 input, bool digestEveryPixel, out FilmBuild? build, out string reason)
    {
        build = null;
        int total = input.Width * input.Height;

        var sealedPixels = input.Snapshot.Pixels.ToDictionary(p => p.PixelIndex);
        var sealedMeasurements = input.Snapshot.Measurements!.Pixels.ToDictionary(p => p.PixelIndex);
        var sources = new Dictionary<int, PixelPathSourceV1>();
        foreach (PixelPathSourceV1 source in input.Sources)
        {
            if (source is null || !sources.TryAdd(source.PixelIndex, source)) { reason = "live_source_duplicate_or_null"; return false; }
        }
        if (sealedPixels.Count != total || sealedMeasurements.Count != total || sources.Count != total)
        { reason = "pixel_coverage_incomplete"; return false; }

        var kindTotals = new SortedDictionary<string, int>(StringComparer.Ordinal);
        long questionTotal = 0, queryTotal = 0, contactEventTotal = 0, sealedContactTotal = 0;
        var selected = new HashSet<int>(input.SelectedPixels);
        var selectedJson = new SortedDictionary<int, Dictionary<string, object?>>();
        var selectedSha = new SortedDictionary<int, string>();
        using IncrementalHash film = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        film.AppendData(Encoding.UTF8.GetBytes(SchemaId + "\0"));

        for (int index = 0; index < total; index++)
        {
            if (!sealedPixels.TryGetValue(index, out FormalTransportObservationPixelV1? sealedPixel) ||
                !sealedMeasurements.TryGetValue(index, out FormalTransportPixelMeasurementV1? measured) ||
                !sources.TryGetValue(index, out PixelPathSourceV1? live))
            { reason = $"pixel_unsealed:{index}"; return false; }
            if (!ValidatePixel(input, sealedPixel, measured, live, input.ContactCounts[index], input.FirstContactSteps[index], out string pixelReason, out PixelDerived derived))
            { reason = $"{pixelReason}:{index}"; return false; }

            bool isSelected = selected.Contains(index);
            if (digestEveryPixel || isSelected)
            {
                Dictionary<string, object?> pixelJson = BuildPixel(input, live, derived, input.ContactCounts[index], input.FirstContactSteps[index]);
                byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(pixelJson);
                if (digestEveryPixel) film.AppendData(canonical);
                if (isSelected)
                {
                    selectedJson[index] = pixelJson;
                    selectedSha[index] = Hex(SHA256.HashData(canonical));
                }
            }
            foreach (TransportEventV1 e in live.Events)
                kindTotals[e.Kind.ToString()] = kindTotals.TryGetValue(e.Kind.ToString(), out int n) ? n + 1 : 1;
            questionTotal += live.QuestionCount;
            queryTotal += live.Queries.Count;
            contactEventTotal += derived.ContactEventCount;
            sealedContactTotal += input.ContactCounts[index];
        }

        build = new FilmBuild
        {
            Digest = digestEveryPixel ? Hex(film.GetHashAndReset()) : string.Empty,
            SelectedJson = selectedJson,
            SelectedSha = selectedSha,
            KindTotals = kindTotals,
            Questions = questionTotal,
            Queries = queryTotal,
            ContactEvents = contactEventTotal,
            SealedContacts = sealedContactTotal
        };
        reason = string.Empty;
        return true;
    }

    public const string PathsFileName = "pixel_paths.json";
    public const string ManifestFileName = "pixel_path_manifest.json";

    /// <summary>
    /// Conservative cap on selected pixels per export (about 92 KB of JSON per canonical pixel, so at most
    /// roughly 24 MB). Whole-film validation and the full-film digest always cover every pixel regardless.
    /// </summary>
    public const int MaxSelectedPixels = 256;

    /// <summary>
    /// Publishes only a complete, verified package: the destination must not exist; both files are written
    /// to a fresh sibling staging directory, re-read and hash-verified, then the staging directory is renamed
    /// to the destination in one step. Any failure removes the staging directory and returns a refusal;
    /// the destination is never created partially.
    /// </summary>
    private static bool TryPublishAtomically(PixelPathObservationInputV1 input, byte[] pathsBytes, byte[] manifestBytes, out string reason)
    {
        string destination = Path.GetFullPath(input.OutputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (Directory.Exists(destination) || File.Exists(destination)) { reason = "destination_exists"; return false; }
        string? parent = Path.GetDirectoryName(destination);
        if (string.IsNullOrEmpty(parent)) { reason = "destination_invalid"; return false; }
        string staging = Path.Combine(parent, "." + Path.GetFileName(destination) + ".staging-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(parent);
            Directory.CreateDirectory(staging);
            File.WriteAllBytes(Path.Combine(staging, PathsFileName), pathsBytes);
            input.PublicationFaultInjectionForTesting?.Invoke("staged_paths");
            File.WriteAllBytes(Path.Combine(staging, ManifestFileName), manifestBytes);
            input.PublicationFaultInjectionForTesting?.Invoke("staged_manifest");
            if (!File.ReadAllBytes(Path.Combine(staging, PathsFileName)).AsSpan().SequenceEqual(pathsBytes) ||
                !File.ReadAllBytes(Path.Combine(staging, ManifestFileName)).AsSpan().SequenceEqual(manifestBytes))
                throw new IOException("staged artifact verification failed");
            input.PublicationFaultInjectionForTesting?.Invoke("verified");
            if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("destination appeared during staging");
            input.PublicationFaultInjectionForTesting?.Invoke("before_publish");
            Directory.Move(staging, destination);
            reason = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            reason = "publication_failed:" + exception.GetType().Name;
            return false;
        }
    }

    private sealed class PixelDerived
    {
        public List<(float Start, float End)> Queried = new();
        public List<(float Start, float End)> Unqueried = new();
        public uint QueriedChordBits;
        public uint MaxUnqueriedGapBits;
        public int ContactEventCount;
        public int FirstContactEventOrdinal = -1;
        public int RemapsBeforeFirstContact = -2;
        public int[] QuestionEventOrdinals = Array.Empty<int>();
        public Dictionary<int, int> ContactQuestionOrdinal = new();
    }

    // requireAttestedPublication: TryWrite (publication identity + source attestation required, unchanged order);
    // TryInspect skips exactly those checks and validates everything else identically.
    private static bool ValidateInput(PixelPathObservationInputV1? input, bool requireAttestedPublication, out string reason)
    {
        reason = string.Empty;
        if (input is null || input.Snapshot is null || input.Sources is null || input.SelectedPixels is null) { reason = "input_missing"; return false; }
        if (!input.Snapshot.RecorderEnabled) { reason = "recorder_disabled_for_acquisition"; return false; }
        if (input.Snapshot.Measurements is null) { reason = "sealed_measurements_missing"; return false; }
        if (input.Width <= 0 || input.Height <= 0) { reason = "dimensions_invalid"; return false; }
        if (input.FilmStride != 1) { reason = "film_stride_unsupported"; return false; }
        int total;
        try { total = checked(input.Width * input.Height); } catch (OverflowException) { reason = "dimensions_overflow"; return false; }
        if (input.ContactCounts?.Length != total || input.FirstContactSteps?.Length != total) { reason = "sealed_channels_invalid"; return false; }
        if (requireAttestedPublication &&
            (string.IsNullOrWhiteSpace(input.OutputDirectory) || string.IsNullOrWhiteSpace(input.RunId) || string.IsNullOrWhiteSpace(input.EngineCommit))) { reason = "identity_missing"; return false; }
        string[] hashes = { input.FormalPolicySha256, input.GeometrySnapshotSha256, input.FrozenBoundaryContentSha256, input.BoundaryExecutionAuthoritySha256, input.CanonicalQueryDatasetSha256 };
        if (hashes.Any(h => !IsSha256(h))) { reason = "acquisition_sha256_invalid"; return false; }
        if (requireAttestedPublication)
        {
            // Source provenance must identify a real source state: a full 40-hex commit, and either "clean" with no
            // fingerprint or "dirty" with a real (non-placeholder) authored-diff SHA-256. "unknown" is refused.
            if (!IsHex(input.EngineCommit, 40)) { reason = "engine_commit_invalid"; return false; }
            if (input.SourceTreeState is not ("clean" or "dirty" or "unknown")) { reason = "source_tree_state_invalid"; return false; }
            if (input.SourceTreeState == "unknown") { reason = "source_state_unverified"; return false; }
            if (input.SourceTreeState == "clean" && !string.IsNullOrEmpty(input.AuthoredDiffSha256)) { reason = "clean_source_must_not_carry_fingerprint"; return false; }
            if (input.SourceTreeState == "dirty" && !IsSha256(input.AuthoredDiffSha256)) { reason = "dirty_source_requires_fingerprint"; return false; }
            if (input.SourceTreeState == "dirty" && input.AuthoredDiffSha256.All(c => c == '0')) { reason = "dirty_source_placeholder_fingerprint"; return false; }
        }
        if (input.LiveCaptureGeneration != input.Snapshot.CaptureGeneration) { reason = "live_capture_generation_mismatch"; return false; }
        if (!string.Equals(input.LiveQueryDatasetSha256, input.CanonicalQueryDatasetSha256, StringComparison.Ordinal)) { reason = "live_query_dataset_mismatch"; return false; }
        if (input.SelectedPixels.Length == 0) { reason = "pixel_selection_empty"; return false; }
        if (input.SelectedPixels.Length > MaxSelectedPixels) { reason = "pixel_selection_too_large"; return false; }
        if (input.SelectedPixels.Distinct().Count() != input.SelectedPixels.Length) { reason = "pixel_selection_duplicate"; return false; }
        if (input.SelectedPixels.Any(p => (uint)p >= (uint)total)) { reason = "pixel_selection_out_of_range"; return false; }
        return true;
    }

    private static bool ValidatePixel(
        PixelPathObservationInputV1 input,
        FormalTransportObservationPixelV1 sealedPixel,
        FormalTransportPixelMeasurementV1 measured,
        PixelPathSourceV1 live,
        int sealedContactCount,
        int sealedFirstContactStep,
        out string reason,
        out PixelDerived derived)
    {
        derived = new PixelDerived();
        ulong generation = input.Snapshot.CaptureGeneration;
        // Acquisition ownership: the live recorder slot must be the sealed slot.
        if (sealedPixel.CaptureGeneration != generation || measured.CaptureGeneration != generation || live.CaptureGeneration != generation)
        { reason = "pixel_generation_mismatch"; return false; }
        if (live.CommitVersion != sealedPixel.CommitVersion || live.CommitVersion != measured.CommitVersion)
        { reason = "pixel_commit_mismatch"; return false; }
        if (!sealedPixel.Finalized || !live.Finalized) { reason = "pixel_unfinalized"; return false; }
        if (sealedPixel.Overflowed || live.Overflowed) { reason = "pixel_recorder_overflow"; return false; }
        if (!sealedPixel.RecordedCompleteForAttempt || !string.Equals(live.ProvisionalReason, sealedPixel.ProvisionalReason, StringComparison.Ordinal))
        { reason = "pixel_recorder_incomplete"; return false; }
        if (live.Termination != measured.Termination || live.RawTerminalFlags != measured.RawTerminalFlags ||
            live.StepsIntegrated != measured.StepsIntegrated || live.TraveledBits != measured.TraveledBits || live.QuestionCount != measured.QuestionCount)
        { reason = "pixel_terminal_inputs_mismatch"; return false; }

        // Selected-field reconciliation of the ordered event sequence against the sealed snapshot: ordinal, kind,
        // integration step and reason (the fields the sealed measurement retains). Other exported attributes
        // (segment ordinal, subject identity, observability, presence, decision, flags, payload bits) are copied
        // from the frozen recorder slot and are not individually authenticated against the seal.
        if (live.Events.Count != measured.Events.Count || live.Events.Count != sealedPixel.EventKinds.Count)
        { reason = "pixel_event_sequence_mismatch"; return false; }
        for (int i = 0; i < live.Events.Count; i++)
        {
            TransportEventV1 e = live.Events[i];
            FormalTransportEventMeasurementV1 m = measured.Events[i];
            if (e.EventOrdinal != i || m.EventOrdinal != i || e.Kind != m.Kind || e.Kind != sealedPixel.EventKinds[i] ||
                e.IntegrationStepOrdinal != m.IntegrationStepOrdinal || e.Reason != m.Reason)
            { reason = "pixel_event_sequence_mismatch"; return false; }
        }

        // Question events <-> recorded probe chords (one-to-one, same order and step).
        var questionOrdinals = new List<int>();
        for (int i = 0; i < live.Events.Count; i++)
            if (live.Events[i].Kind == TransportEventKindV1.ProbeQuestionIssued) questionOrdinals.Add(i);
        if (questionOrdinals.Count != live.QuestionCount || live.Queries.Count != live.QuestionCount || measured.QuerySteps.Count != live.QuestionCount)
        { reason = "question_join_mismatch"; return false; }
        for (int q = 0; q < live.Queries.Count; q++)
        {
            int step = live.Events[questionOrdinals[q]].IntegrationStepOrdinal;
            if (live.Queries[q].ProbeStep != step || measured.QuerySteps[q] != step) { reason = "question_join_mismatch"; return false; }
        }
        bool failedFlag = (live.RawTerminalFlags & RawTerminalFlagsV1.ProbeQueryFailed) != 0;
        if (failedFlag != live.Events.Any(e => e.Kind == TransportEventKindV1.ProbeQueryFailed)) { reason = "question_join_mismatch"; return false; }
        derived.QuestionEventOrdinals = questionOrdinals.ToArray();

        // Diagnostic geometry-contact events <-> sealed BVH contact count and serial-replay first-contact step.
        var contacts = new List<int>();
        for (int i = 0; i < live.Events.Count; i++)
            if (live.Events[i].Kind == TransportEventKindV1.GeometryContactObserved) contacts.Add(i);
        if (sealedContactCount < 0 || contacts.Count != sealedContactCount) { reason = "contact_join_mismatch"; return false; }
        if (contacts.Count == 0 ? sealedFirstContactStep != -1 : live.Events[contacts[0]].IntegrationStepOrdinal != sealedFirstContactStep)
        { reason = "contact_join_mismatch"; return false; }
        // Recorder contract (RayBeamRenderer Pass-1 probe): RecordQuestion precedes the query and its
        // RecordGeometryObservation, so a contact belongs to the IMMEDIATELY PRECEDING question, which must have
        // the same integration step and source segment. Anything else is refused; no wider search is attempted.
        foreach (int c in contacts)
        {
            TransportEventV1 contact = live.Events[c];
            int latest = -1;
            for (int q = questionOrdinals.Count - 1; q >= 0; q--)
            {
                if (questionOrdinals[q] < c) { latest = q; break; }
            }
            if (latest < 0) { reason = "contact_question_association_failed"; return false; }
            TransportEventV1 question = live.Events[questionOrdinals[latest]];
            if (question.IntegrationStepOrdinal != contact.IntegrationStepOrdinal || question.SourceSegmentOrdinal != contact.SourceSegmentOrdinal)
            { reason = "contact_question_association_failed"; return false; }
            derived.ContactQuestionOrdinal[c] = latest;
        }
        derived.ContactEventCount = contacts.Count;
        if (contacts.Count > 0)
        {
            derived.FirstContactEventOrdinal = contacts[0];
            derived.RemapsBeforeFirstContact = live.Events.Take(contacts[0]).Count(e => e.Kind == TransportEventKindV1.BoundaryRemapApplied);
        }

        // Terminal: exactly one, last, consistent with termination and flags.
        int terminals = live.Events.Count(e => e.Kind == TransportEventKindV1.Terminal);
        if (live.Termination == Pass1TransportTerminationV1.None ? terminals != 0
            : terminals != 1 || live.Events[^1].Kind != TransportEventKindV1.Terminal)
        { reason = "terminal_mismatch"; return false; }

        // Coverage reconciliation: recompute the sealed coverage measurement exactly (same algorithm, same inputs).
        float traveled = BitConverter.UInt32BitsToSingle(live.TraveledBits);
        if (!float.IsFinite(traveled) || traveled < 0f) { reason = "traveled_invalid"; return false; }
        var segments = live.Queries.Select(q => new FormalQuerySegmentV1(q.ProbeStep, BitConverter.UInt32BitsToSingle(q.TraveledBBits), BitConverter.UInt32BitsToSingle(q.ChordLengthBits))).ToArray();
        FormalQueryCoverageSummaryV1 coverage;
        try { coverage = FormalQueryCoverageV1.Measure(traveled, segments); }
        catch (ArgumentException) { reason = "coverage_inputs_invalid"; return false; }
        if (coverage.QueriedChordBits != measured.QueriedChordBits || coverage.MaxUnqueriedGapBits != measured.MaxUnqueriedGapBits)
        { reason = "coverage_reconciliation_mismatch"; return false; }
        derived.QueriedChordBits = coverage.QueriedChordBits;
        derived.MaxUnqueriedGapBits = coverage.MaxUnqueriedGapBits;
        BuildIntervals(traveled, segments, derived);
        reason = string.Empty;
        return true;
    }

    // Same interval construction as FormalQueryCoverageV1.Measure, retaining the intervals.
    private static void BuildIntervals(float traveled, FormalQuerySegmentV1[] segments, PixelDerived derived)
    {
        var intervals = new List<(float Start, float End)>();
        foreach (FormalQuerySegmentV1 s in segments)
        {
            float end = System.Math.Clamp(s.TraveledB, 0f, traveled);
            float start = System.Math.Clamp(s.TraveledB - s.ChordLength, 0f, traveled);
            if (end < start) (start, end) = (end, start);
            intervals.Add((start, end));
        }
        intervals.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));
        float cursor = 0f;
        foreach ((float start, float end) in intervals)
        {
            if (start > cursor) derived.Unqueried.Add((cursor, start));
            if (end > cursor)
            {
                float from = System.Math.Max(cursor, start);
                if (derived.Queried.Count > 0 && derived.Queried[^1].End == from) derived.Queried[^1] = (derived.Queried[^1].Start, end);
                else derived.Queried.Add((from, end));
                cursor = end;
            }
        }
        if (traveled > cursor) derived.Unqueried.Add((cursor, traveled));
    }

    private static Dictionary<string, object?> BuildPixel(PixelPathObservationInputV1 input, PixelPathSourceV1 live, PixelDerived d, int sealedContacts, int sealedFirstContact)
    {
        var questionOrdinalByEvent = new Dictionary<int, int>();
        for (int q = 0; q < d.QuestionEventOrdinals.Length; q++) questionOrdinalByEvent[d.QuestionEventOrdinals[q]] = q;
        var events = new List<Dictionary<string, object?>>(live.Events.Count);
        foreach (TransportEventV1 e in live.Events)
        {
            var ev = new Dictionary<string, object?>
            {
                ["event_ordinal"] = e.EventOrdinal,
                ["integration_step_ordinal"] = e.IntegrationStepOrdinal,
                ["source_segment_ordinal"] = e.SourceSegmentOrdinal,
                ["kind"] = e.Kind.ToString(),
                ["subject_kind"] = e.SubjectKind.ToString(),
                ["identity_availability"] = e.IdentityAvailability.ToString(),
                ["canonical_subject_id"] = e.CanonicalSubjectId,
                ["subject_execution_ordinal"] = e.SubjectExecutionOrdinal,
                ["observability"] = FlagNames(e.Observability),
                ["presence"] = FlagNames(e.Presence),
                ["decision"] = e.Decision.ToString(),
                ["reason"] = e.Reason.ToString(),
                ["flags"] = FlagNames(e.Flags),
            };
            if ((e.Presence & TransportEventPresenceV1.PrePosition) != 0) ev["pre_position_bits"] = Bits3(e.PrePosition);
            if ((e.Presence & TransportEventPresenceV1.PostPosition) != 0) ev["post_position_bits"] = Bits3(e.PostPosition);
            if ((e.Presence & TransportEventPresenceV1.PreDirection) != 0) ev["pre_direction_bits"] = Bits3(e.PreDirection);
            if ((e.Presence & TransportEventPresenceV1.PostDirection) != 0) ev["post_direction_bits"] = Bits3(e.PostDirection);
            if ((e.Presence & TransportEventPresenceV1.ContactNormal) != 0) ev["contact_normal_bits"] = Bits3(e.ContactNormal);
            if ((e.Presence & TransportEventPresenceV1.PathDistance) != 0) ev["path_distance_bits"] = BitsHex(e.PathDistanceBits);
            if ((e.Presence & TransportEventPresenceV1.LocalSegmentParameter) != 0) ev["local_segment_parameter_bits"] = BitsHex(e.LocalSegmentParameterBits);
            if (questionOrdinalByEvent.TryGetValue(e.EventOrdinal, out int qo)) ev["derived_question_ordinal"] = qo;
            if (d.ContactQuestionOrdinal.TryGetValue(e.EventOrdinal, out int cq)) ev["derived_contact_question_ordinal"] = cq;
            events.Add(ev);
        }
        var queries = new List<Dictionary<string, object?>>(live.Queries.Count);
        for (int q = 0; q < live.Queries.Count; q++)
        {
            PixelPathQueryV1 query = live.Queries[q];
            queries.Add(new Dictionary<string, object?>
            {
                ["question_ordinal"] = q,
                ["question_event_ordinal"] = d.QuestionEventOrdinals[q],
                ["probe_step"] = query.ProbeStep,
                ["chord_from_bits"] = Bits3(query.From),
                ["chord_to_bits"] = Bits3(query.To),
                ["traveled_b_bits"] = BitsHex(query.TraveledBBits),
                ["collision_mask"] = query.CollisionMask,
                ["collide_with_bodies"] = query.CollideWithBodies,
                ["collide_with_areas"] = query.CollideWithAreas,
                ["hit_from_inside"] = query.HitFromInside,
                ["hit_back_faces"] = query.HitBackFaces,
                ["authority_disposition"] = "unavailable",
                ["display_chord_from"] = Display3(query.From),
                ["display_chord_to"] = Display3(query.To)
            });
        }
        return new Dictionary<string, object?>
        {
            ["pixel_index"] = live.PixelIndex,
            ["x"] = live.PixelIndex % input.Width,
            ["y"] = live.PixelIndex / input.Width,
            ["recorded"] = new Dictionary<string, object?>
            {
                ["capture_generation"] = live.CaptureGeneration,
                ["commit_version"] = live.CommitVersion,
                ["finalized"] = live.Finalized,
                ["event_capacity"] = live.EventCapacity,
                ["overflowed"] = live.Overflowed,
                ["provisional_reason"] = live.ProvisionalReason,
                ["termination"] = live.Termination.ToString(),
                ["raw_terminal_flags"] = FlagNames(live.RawTerminalFlags),
                ["steps_integrated"] = live.StepsIntegrated,
                ["traveled_bits"] = BitsHex(live.TraveledBits),
                ["found"] = live.Found,
                ["question_count"] = live.QuestionCount,
                ["sealed_bvh_contact_count"] = sealedContacts,
                ["sealed_replay_first_contact_step"] = sealedFirstContact,
                ["events"] = events,
                ["queries"] = queries
            },
            ["derived"] = new Dictionary<string, object?>
            {
                ["rule"] = "computed only from recorded fields above; path intervals use the chord-length proxy of FormalQueryCoverageV1",
                ["query_chord_length_bits"] = live.Queries.Select(q => BitsHex(q.ChordLengthBits)).ToArray(),
                ["query_chord_length_rule"] = "derived from recorded chord endpoints: float32 |chord_to - chord_from| (same expression as the sealed coverage measurement); question order",
                ["queried_chord_bits"] = BitsHex(d.QueriedChordBits),
                ["max_unqueried_gap_bits"] = BitsHex(d.MaxUnqueriedGapBits),
                ["queried_path_intervals"] = d.Queried.Select(i => new[] { Display(i.Start), Display(i.End) }).ToArray(),
                ["unqueried_path_intervals"] = d.Unqueried.Select(i => new[] { Display(i.Start), Display(i.End) }).ToArray(),
                ["contact_event_count"] = d.ContactEventCount,
                ["first_contact_event_ordinal"] = d.FirstContactEventOrdinal,
                ["remaps_before_first_contact"] = d.RemapsBeforeFirstContact,
                ["portal_remap_count"] = live.Events.Count(e => e.Kind == TransportEventKindV1.BoundaryRemapApplied),
                ["display_traveled"] = Display(BitConverter.UInt32BitsToSingle(live.TraveledBits)),
                ["joins"] = "questions<->queries exact; contacts==sealed BVH count; first contact step==serial replay; coverage bits==sealed measurement"
            },
            ["unavailable"] = Unavailable
        };
    }

    private static Dictionary<string, object> BuildManifest(PixelPathObservationInputV1 input, string digest, SortedDictionary<int, string> selectedSha,
        SortedDictionary<string, int> kindTotals, long questions, long queries, long contactEvents, long sealedContacts, byte[] pathsBytes) => new()
    {
        ["schema"] = SchemaId,
        ["artifact_class"] = ArtifactClass,
        ["title"] = "Ordered per-pixel recorded transport events — provisional, observational export.",
        ["scientific_ledger_sealed"] = false,
        ["claim_boundary"] = ClaimBoundary(),
        ["field_classes"] = FieldClasses(),
        ["provenance_warnings"] = ProvenanceWarnings,
        ["run_id"] = input.RunId,
        ["scene"] = new Dictionary<string, object> { ["semantic_id"] = input.SemanticSceneId, ["godot_path"] = input.GodotScenePath },
        ["acquisition"] = Acquisition(input),
        ["source"] = new Dictionary<string, object> { ["engine_commit"] = input.EngineCommit, ["tree_state"] = input.SourceTreeState, ["authored_diff_sha256"] = input.AuthoredDiffSha256 },
        ["validation"] = new Dictionary<string, object>
        {
            ["scope"] = "every pixel of the sealed acquisition is validated before writing; any failure refuses the whole export",
            ["event_sequence_reconciliation"] = "selected fields only: event ordinal, kind, integration step and reason are compared with the sealed snapshot; segment ordinal, subject identity, observability, presence, decision, flags and payload bits are copied from the frozen recorder slot and are not individually authenticated; this is not cryptographic sealing",
            ["contact_association_rule"] = "a contact belongs to the immediately preceding question, which must share its integration step and source segment (recorder contract); otherwise refused",
            ["selection_limit"] = MaxSelectedPixels,
            ["publication"] = "staged in a sibling directory, re-read and hash-verified, then renamed into a destination that must not already exist",
            ["validated_pixels"] = input.Width * input.Height,
            ["full_film_digest_sha256"] = digest,
            ["full_film_digest_scope"] = "SHA-256 over schema id + compact canonical JSON of every pixel in index order",
            ["totals"] = new Dictionary<string, object>
            {
                ["events_by_kind"] = kindTotals,
                ["question_events"] = questions,
                ["recorded_queries"] = queries,
                ["geometry_contact_events"] = contactEvents,
                ["sealed_bvh_contacts"] = sealedContacts
            }
        },
        ["selection"] = new Dictionary<string, object>
        {
            ["pixels"] = selectedSha.Keys.ToArray(),
            ["pixel_compact_json_sha256"] = selectedSha.ToDictionary(kv => kv.Key.ToString(CultureInfo.InvariantCulture), kv => (object)kv.Value)
        },
        ["artifacts"] = new Dictionary<string, object> { ["pixel_paths.json"] = PortalRemapObservationMapV1.HashEntry(pathsBytes) }
    };

    private static string[] ClaimBoundary() => new[]
    {
        "This artifact copies provisional recorder evidence from one sealed Formal acquisition; it adds no measurement.",
        "GeometryContactObserved events are diagnostic Godot witnesses; per-pixel contact authority is the sealed BVH contact_counts channel.",
        "Diagnostic contact does not establish a qualified terminal-surface hit.",
        "Unavailable details are listed explicitly and are never inferred or fabricated.",
        "The sealed Q deck and all existing artifacts are unchanged."
    };

    private static Dictionary<string, object> FieldClasses() => new()
    {
        ["recorded"] = "byte-exact copies of recorder-owned values (floats as IEEE-754 bit patterns, hex); keys prefixed derived_ or display_ inside recorded objects are not recorded values",
        ["derived_*"] = "keys inside recorded events computed by the exporter (question/contact joins)",
        ["derived"] = "computed only from recorded values in the same pixel (joins, path intervals, counts)",
        ["display_*"] = "decimal renderings of recorded bits for reading; not authoritative",
        ["unavailable"] = "not retained by the recorder at this commit"
    };

    private static Dictionary<string, object> Acquisition(PixelPathObservationInputV1 input) => new()
    {
        ["formal_policy_id"] = input.FormalPolicyId,
        ["formal_policy_sha256"] = input.FormalPolicySha256,
        ["geometry_snapshot_sha256"] = input.GeometrySnapshotSha256,
        ["frozen_boundary_content_sha256"] = input.FrozenBoundaryContentSha256,
        ["boundary_execution_authority_sha256"] = input.BoundaryExecutionAuthoritySha256,
        ["canonical_query_dataset_sha256"] = input.CanonicalQueryDatasetSha256,
        ["live_query_dataset_sha256_at_export"] = input.LiveQueryDatasetSha256,
        ["capture_generation"] = input.Snapshot.CaptureGeneration,
        ["width"] = input.Width,
        ["height"] = input.Height,
        ["film_stride"] = input.FilmStride,
        ["pixel_order"] = "row-major: pixel = y * width + x",
        ["camera_pose_raw_u32"] = PortalRemapObservationMapV1.CameraBits(input.CameraPose)
    };

    private static string[] FlagNames<T>(T value) where T : struct, Enum
    {
        ulong bits = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        if (bits == 0) return Array.Empty<string>();
        var names = new List<string>();
        foreach (T flag in Enum.GetValues<T>())
        {
            ulong f = Convert.ToUInt64(flag, CultureInfo.InvariantCulture);
            if (f != 0 && (bits & f) == f) names.Add(flag.ToString());
        }
        return names.ToArray();
    }

    private static string BitsHex(uint bits) => bits.ToString("x8", CultureInfo.InvariantCulture);
    private static string[] Bits3(RawFloat3BitsV1 v) => new[] { BitsHex(v.X), BitsHex(v.Y), BitsHex(v.Z) };
    private static string Display(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string[] Display3(RawFloat3BitsV1 v) => new[] { Display(BitConverter.UInt32BitsToSingle(v.X)), Display(BitConverter.UInt32BitsToSingle(v.Y)), Display(BitConverter.UInt32BitsToSingle(v.Z)) };
    private static bool IsSha256(string? value) => IsHex(value, 64);
    private static bool IsHex(string? value, int length) => value is not null && value.Length == length && value.All(Uri.IsHexDigit);
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
