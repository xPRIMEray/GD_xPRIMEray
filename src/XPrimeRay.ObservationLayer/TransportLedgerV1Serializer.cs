using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace XPrimeRay.ObservationLayer;

/// <summary>
/// Pinned V1 serialization. ScientificContentSha256 hashes exactly SerializeScientificContent():
/// the acquisition key, ordinal UTF-8 canonical string table (without Unicode normalization),
/// pass-1 termination/coverage/recorder completeness, raw terminal inputs, events, and question
/// authority links. It excludes authority/transaction lifecycle, commit versions, runtime state,
/// and the diagnostic appendix. ArtifactSha256 hashes exactly SerializeArtifact(), whose envelope
/// adds whole-pixel commit versions, authority/transaction lifecycle, and optional diagnostics.
/// </summary>
public static class TransportLedgerV1Serializer
{
    private static readonly byte[] ScientificMagic = Encoding.ASCII.GetBytes("XPRIME-SCIENTIFIC-TRANSPORT-LEDGER\0");
    private static readonly byte[] AppendixMagic = Encoding.ASCII.GetBytes("XPRIME-DIAGNOSTIC-RENDER-APPENDIX\0");
    private static readonly byte[] ArtifactMagic = Encoding.ASCII.GetBytes("XPRIME-TRANSPORT-LEDGER-ARTIFACT\0");
    private const ushort Version = 1;

    public static byte[] SerializeScientificContent(ScientificTransportLedgerV1 ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ledger.ValidateStructure();
        using var stream = new MemoryStream();
        WriteBytes(stream, ScientificMagic);
        WriteUInt16(stream, Version);
        WriteAcquisitionKey(stream, ledger.AcquisitionKey);

        string[] stringTable = BuildCanonicalStringTable(ledger);
        WriteUInt32(stream, checked((uint)stringTable.Length));
        foreach (string value in stringTable) WriteString(stream, value);
        var stringIndexes = stringTable.Select((value, index) => (value, index))
            .ToDictionary(pair => pair.value, pair => pair.index, StringComparer.Ordinal);

        WriteUInt32(stream, checked((uint)ledger.Pixels.Count));
        foreach (ScientificPixelCommitV1 commit in ledger.Pixels)
        {
            ScientificPixelPathV1 pixel = commit.Path;
            WriteInt32(stream, pixel.PixelIndex);
            WriteByte(stream, (byte)pixel.Status.TransportTermination);
            WriteUInt32(stream, (uint)pixel.Status.ObservationCoverage);
            WriteByte(stream, (byte)pixel.Status.RecorderCompletion);
            WriteUInt32(stream, (uint)pixel.TerminalInputs.Flags);
            WriteInt32(stream, pixel.TerminalInputs.StepsIntegrated);
            WriteUInt32(stream, pixel.TerminalInputs.TraveledBits);
            WriteByte(stream, pixel.TerminalInputs.Found ? (byte)1 : (byte)0);
            WriteByte(stream, (byte)pixel.TerminalInputs.SurfaceClass);

            WriteUInt32(stream, checked((uint)pixel.Events.Count));
            foreach (TransportEventV1 transportEvent in pixel.Events)
                WriteEvent(stream, transportEvent, stringIndexes);

            WriteUInt32(stream, checked((uint)pixel.QuestionAuthorityLinks.Count));
            foreach (QuestionAuthorityLinkV1 link in pixel.QuestionAuthorityLinks)
            {
                WriteInt32(stream, link.QuestionOrdinal);
                WriteInt32(stream, link.ProbeStep);
                WriteByte(stream, (byte)link.Disposition);
                WriteInt32(stream, StringIndex(link.CanonicalPrimitiveId, stringIndexes));
            }
        }
        return stream.ToArray();
    }

    public static byte[] SerializeDiagnosticAppendix(DiagnosticRenderAppendixV1 appendix)
    {
        ArgumentNullException.ThrowIfNull(appendix);
        appendix.Validate();
        using var stream = new MemoryStream();
        WriteBytes(stream, AppendixMagic);
        WriteUInt16(stream, Version);
        WriteBytes(stream, Convert.FromHexString(appendix.ScientificContentSha256));
        WriteInt32(stream, appendix.RuntimeAcquisitionGeneration);
        WriteByte(stream, (byte)appendix.Status);
        WriteUInt32(stream, checked((uint)appendix.RuntimeProvenance.Count));
        foreach (RuntimeProvenanceEntryV1 entry in appendix.RuntimeProvenance)
        {
            WriteString(stream, entry.Key);
            WriteString(stream, entry.Value);
        }
        WriteUInt32(stream, checked((uint)appendix.Pixels.Count));
        foreach (DiagnosticRenderPixelV1 pixel in appendix.Pixels)
        {
            WriteInt32(stream, pixel.PixelIndex);
            WriteUInt32(stream, pixel.LinkedCommitVersion);
            WriteUInt32(stream, checked((uint)pixel.Observations.Count));
            foreach (DiagnosticRenderObservationV1 observation in pixel.Observations)
            {
                WriteInt32(stream, observation.ObservationOrdinal);
                WriteInt32(stream, observation.SourceSegmentOrdinal);
                WriteUInt16(stream, (ushort)observation.Kind);
                WriteUInt32(stream, (uint)observation.Flags);
                WriteFloat3(stream, observation.Position);
                WriteFloat3(stream, observation.Normal);
                WriteUInt64(stream, observation.RuntimeColliderInstanceId);
            }
        }
        return stream.ToArray();
    }

    public static byte[] SerializeArtifact(TransportLedgerArtifactV1 artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        artifact.ScientificLedger.ValidateStructure();
        byte[] scientific = SerializeScientificContent(artifact.ScientificLedger);
        using var stream = new MemoryStream();
        WriteBytes(stream, ArtifactMagic);
        WriteUInt16(stream, Version);
        WriteUInt64(stream, checked((ulong)scientific.LongLength));
        WriteBytes(stream, scientific);

        WriteUInt32(stream, checked((uint)artifact.ScientificLedger.Pixels.Count));
        foreach (ScientificPixelCommitV1 commit in artifact.ScientificLedger.Pixels)
        {
            WriteInt32(stream, commit.Path.PixelIndex);
            WriteUInt32(stream, commit.CommitVersion);
            WriteByte(stream, (byte)commit.Path.Status.AuthorityStatus);
            WriteByte(stream, (byte)commit.Path.Status.TransactionStatus);
        }

        WriteByte(stream, artifact.DiagnosticAppendix is null ? (byte)0 : (byte)1);
        if (artifact.DiagnosticAppendix is not null)
        {
            SealedScientificTransportLedgerV1 sealedLedger = artifact.ScientificLedger.Seal();
            string expectedScientificHash = ComputeSealedScientificContentSha256(sealedLedger);
            if (!string.Equals(expectedScientificHash, artifact.DiagnosticAppendix.ScientificContentSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Diagnostic appendix references a different scientific content hash.");
            Dictionary<int, uint> commitVersions = artifact.ScientificLedger.Pixels
                .ToDictionary(commit => commit.Path.PixelIndex, commit => commit.CommitVersion);
            foreach (DiagnosticRenderPixelV1 pixel in artifact.DiagnosticAppendix.Pixels)
            {
                if (!commitVersions.TryGetValue(pixel.PixelIndex, out uint version) || version != pixel.LinkedCommitVersion)
                    throw new InvalidOperationException($"Diagnostic pixel {pixel.PixelIndex} does not link to its authoritative scientific pixel commit version.");
            }
            byte[] appendix = SerializeDiagnosticAppendix(artifact.DiagnosticAppendix);
            WriteUInt64(stream, checked((ulong)appendix.LongLength));
            WriteBytes(stream, appendix);
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Hashes serializable working content for diagnostics. This hash is not authoritative evidence;
    /// only ComputeSealedScientificContentSha256 designates a finalized scientific identity.
    /// </summary>
    public static string ComputeScientificContentSha256(ScientificTransportLedgerV1 ledger) =>
        Convert.ToHexString(SHA256.HashData(SerializeScientificContent(ledger))).ToLowerInvariant();

    public static byte[] SerializeSealedScientificContent(SealedScientificTransportLedgerV1 ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        _ = ledger.Ledger.Seal();
        return SerializeScientificContent(ledger.Ledger);
    }

    public static string ComputeSealedScientificContentSha256(SealedScientificTransportLedgerV1 ledger) =>
        Convert.ToHexString(SHA256.HashData(SerializeSealedScientificContent(ledger))).ToLowerInvariant();

    public static string ComputeArtifactSha256(TransportLedgerArtifactV1 artifact) =>
        Convert.ToHexString(SHA256.HashData(SerializeArtifact(artifact))).ToLowerInvariant();

    private static string[] BuildCanonicalStringTable(ScientificTransportLedgerV1 ledger) =>
        ledger.Pixels
            .SelectMany(pixel => pixel.Path.Events.Select(e => e.CanonicalSubjectId)
                .Concat(pixel.Path.QuestionAuthorityLinks.Select(q => q.CanonicalPrimitiveId)))
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static int StringIndex(string? value, IReadOnlyDictionary<string, int> stringIndexes) =>
        value is null ? -1 : stringIndexes[value];

    private static void WriteAcquisitionKey(Stream stream, ScientificAcquisitionKey key)
    {
        WriteString(stream, key.FormalPolicySha256);
        WriteString(stream, key.GeometrySnapshotSha256);
        WriteString(stream, key.FrozenBoundaryContentSha256);
        WriteString(stream, key.BoundaryExecutionAuthoritySha256);
        WriteString(stream, key.RayGenerationConventionId);
        CameraPoseBitsV1 c = key.CameraPose;
        WriteUInt32(stream, c.BasisXX); WriteUInt32(stream, c.BasisXY); WriteUInt32(stream, c.BasisXZ);
        WriteUInt32(stream, c.BasisYX); WriteUInt32(stream, c.BasisYY); WriteUInt32(stream, c.BasisYZ);
        WriteUInt32(stream, c.BasisZX); WriteUInt32(stream, c.BasisZY); WriteUInt32(stream, c.BasisZZ);
        WriteUInt32(stream, c.OriginX); WriteUInt32(stream, c.OriginY); WriteUInt32(stream, c.OriginZ);
        WriteUInt32(stream, c.Fov);
        WriteInt32(stream, key.FilmWidth);
        WriteInt32(stream, key.FilmHeight);
        WriteInt32(stream, key.FilmStride);
    }

    private static void WriteEvent(Stream stream, TransportEventV1 e, IReadOnlyDictionary<string, int> stringIndexes)
    {
        WriteInt32(stream, e.EventOrdinal);
        WriteInt32(stream, e.IntegrationStepOrdinal);
        WriteInt32(stream, e.SourceSegmentOrdinal);
        WriteUInt16(stream, (ushort)e.Kind);
        WriteByte(stream, (byte)e.SubjectKind);
        WriteByte(stream, (byte)e.IdentityAvailability);
        WriteInt32(stream, StringIndex(e.CanonicalSubjectId, stringIndexes));
        WriteInt32(stream, e.SubjectExecutionOrdinal);
        WriteUInt32(stream, (uint)e.Observability);
        WriteUInt32(stream, (uint)e.Presence);
        WriteUInt16(stream, (ushort)e.Decision);
        WriteUInt16(stream, (ushort)e.Reason);
        WriteUInt32(stream, (uint)e.Flags);
        WriteFloat3(stream, e.PrePosition);
        WriteFloat3(stream, e.PostPosition);
        WriteFloat3(stream, e.PreDirection);
        WriteFloat3(stream, e.PostDirection);
        WriteFloat3(stream, e.ContactNormal);
        WriteUInt32(stream, e.PathDistanceBits);
        WriteUInt32(stream, e.LocalSegmentParameterBits);
    }

    private static void WriteFloat3(Stream stream, RawFloat3BitsV1 value)
    {
        WriteUInt32(stream, value.X);
        WriteUInt32(stream, value.Y);
        WriteUInt32(stream, value.Z);
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteUInt32(stream, checked((uint)bytes.Length));
        WriteBytes(stream, bytes);
    }

    private static void WriteByte(Stream stream, byte value) => stream.WriteByte(value);

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteInt32(Stream stream, int value) => WriteUInt32(stream, unchecked((uint)value));

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt64(Stream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteBytes(Stream stream, ReadOnlySpan<byte> bytes) => stream.Write(bytes);
}
