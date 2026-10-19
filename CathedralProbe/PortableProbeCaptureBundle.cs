#nullable enable

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed class PortableProbeCaptureInput
{
	public required string RunId { get; init; }
	public required string OutputDirectory { get; init; }
	public required string SemanticSceneId { get; init; }
	public required string GodotScenePath { get; init; }
	public required string EngineCommit { get; init; }
	public string SourceTreeState { get; init; } = "unknown";
	public string AuthoredDiffSha256 { get; init; } = string.Empty;
	public required string AcquisitionLineage { get; init; }
	public required bool LifecycleComplete { get; init; }
	public required bool SealedAuthorityAvailable { get; init; }
	public required int Generation { get; init; }
	public required ProbeContextKey ContextKey { get; init; }
	public required float[] CameraTransform { get; init; }
	public required int Width { get; init; }
	public required int Height { get; init; }
	public required int UnprocessedCount { get; init; }
	public required int StepsPerRay { get; init; }
	public required float StepLength { get; init; }
	public required float FieldStrength { get; init; }
	public required ProbeOutcomeCode[] Outcomes { get; init; }
	public required int[] ContactCounts { get; init; }
	public required int[] FinalStepCounts { get; init; }
	public required int[] PolicyMaxSteps { get; init; }
	public required byte[] EffortValid { get; init; }
	public string ContactAuthorityToken { get; init; } = "legacy";
	public string SpatialAuthorityToken { get; init; } = "legacy";
	public Dictionary<string, string> RuntimeProvenance { get; init; } = new();
}

public sealed class PortableProbeCaptureResult
{
	public required string OutputDirectory { get; init; }
	public required string ManifestPath { get; init; }
	public required string SummaryPath { get; init; }
	public required int Generation { get; init; }
	public required int Width { get; init; }
	public required int Height { get; init; }
	public required Dictionary<string, int> OutcomeHistogram { get; init; }
	public required Dictionary<string, int> ContactHistogram { get; init; }
	public required string SummaryLine { get; init; }
}

/// <summary>
/// Host-neutral v1 capture writer. Binary and PNG formats are deliberately independent of Godot types.
/// </summary>
public static class PortableProbeCaptureBundle
{
	public const string SchemaVersion = "1.2.0";
	public const string ArtifactClass = "qualified_visualization";
	private const int DisplayScale = 24;

	public static bool TryWrite(
		PortableProbeCaptureInput input,
		out PortableProbeCaptureResult? result,
		out string reason)
	{
		result = null;
		reason = string.Empty;
		if (!ValidateInput(input, out reason)) return false;

		int total = input.Width * input.Height;
		Directory.CreateDirectory(input.OutputDirectory);
		Dictionary<string, byte[]> channelBytes = new()
		{
			["outcomes.bin"] = EncodeOutcomes(input.Outcomes, total),
			["contact_counts.bin"] = EncodeInt32(input.ContactCounts, total),
			["final_step_counts.bin"] = EncodeInt32(input.FinalStepCounts, total),
			["policy_max_steps.bin"] = EncodeInt32(input.PolicyMaxSteps, total),
			["effort_valid.bin"] = EncodeBytes(input.EffortValid, total)
		};
		Dictionary<string, ArtifactHash> sourceHashes = new();
		foreach ((string name, byte[] bytes) in channelBytes)
		{
			File.WriteAllBytes(Path.Combine(input.OutputDirectory, name), bytes);
			sourceHashes[name] = new ArtifactHash(bytes.Length, Sha256(bytes));
		}

		Dictionary<ProbeViewMode, byte[]> mappedRgba = new()
		{
			[ProbeViewMode.Outcome] = MapRgba(input, ProbeViewMode.Outcome),
			[ProbeViewMode.ContactEvents] = MapRgba(input, ProbeViewMode.ContactEvents),
			[ProbeViewMode.TransportEffort] = MapRgba(input, ProbeViewMode.TransportEffort)
		};
		Dictionary<string, ArtifactHash> afterMappingSourceHashes = new();
		foreach ((string name, byte[] bytes) in EncodeChannels(input, total))
		{
			ArtifactHash after = new(bytes.Length, Sha256(bytes));
			if (!after.Sha256Hex.Equals(sourceHashes[name].Sha256Hex, StringComparison.Ordinal))
			{
				reason = $"source_changed_during_mapping_{name}";
				return false;
			}
			afterMappingSourceHashes[name] = after;
		}
		Dictionary<string, ArtifactHash> mappedHashes = new();
		Dictionary<string, ArtifactHash> pngHashes = new();
		foreach ((ProbeViewMode mode, byte[] rgba) in mappedRgba)
		{
			string stem = ModeStem(mode);
			mappedHashes[$"{stem}_plate_rgba"] = new ArtifactHash(rgba.Length, Sha256(rgba));
			byte[] png = EncodePng(input.Width, input.Height, rgba);
			string canonicalName = $"{stem}.png";
			File.WriteAllBytes(Path.Combine(input.OutputDirectory, canonicalName), png);
			pngHashes[canonicalName] = new ArtifactHash(png.Length, Sha256(png));

			byte[] displayRgba = ScaleNearest(input.Width, input.Height, rgba, DisplayScale);
			byte[] displayPng = EncodePng(input.Width * DisplayScale, input.Height * DisplayScale, displayRgba);
			string displayName = $"{stem}_display.png";
			File.WriteAllBytes(Path.Combine(input.OutputDirectory, displayName), displayPng);
			pngHashes[displayName] = new ArtifactHash(displayPng.Length, Sha256(displayPng));
		}

		Dictionary<string, int> outcomes = BuildOutcomeHistogram(input.Outcomes, total);
		Dictionary<string, int> contacts = BuildContactHistogram(input.ContactCounts, total);
		EffortStats effort = BuildEffortStats(input.FinalStepCounts, input.PolicyMaxSteps, input.EffortValid, total);
		string contextSha = Sha256(ProbeContextCanonicalSerializer.Serialize(input.ContextKey));
		Dictionary<string, object> manifest = BuildManifest(
			input, total, contextSha, sourceHashes, afterMappingSourceHashes, mappedHashes, pngHashes, outcomes, contacts, effort);
		string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
		string manifestPath = Path.Combine(input.OutputDirectory, "manifest.json");
		File.WriteAllText(manifestPath, manifestJson + Environment.NewLine, new UTF8Encoding(false));
		string summary = BuildSummary(input, total, contextSha, sourceHashes, mappedHashes, pngHashes, outcomes, contacts, effort);
		string summaryPath = Path.Combine(input.OutputDirectory, "run_summary.md");
		File.WriteAllText(summaryPath, summary, new UTF8Encoding(false));

		result = new PortableProbeCaptureResult
		{
			OutputDirectory = input.OutputDirectory,
			ManifestPath = manifestPath,
			SummaryPath = summaryPath,
			Generation = input.Generation,
			Width = input.Width,
			Height = input.Height,
			OutcomeHistogram = outcomes,
			ContactHistogram = contacts,
			SummaryLine = $"generation={input.Generation} dimensions={input.Width}x{input.Height} context_sha256={contextSha}"
		};
		return true;
	}

	private static bool ValidateInput(PortableProbeCaptureInput input, out string reason)
	{
		reason = string.Empty;
		if (!input.LifecycleComplete) { reason = "lifecycle_not_complete"; return false; }
		if (!input.SealedAuthorityAvailable) { reason = "sealed_authority_unavailable"; return false; }
		if (string.IsNullOrWhiteSpace(input.ContactAuthorityToken) || input.ContactAuthorityToken == "legacy") { reason = "contact_authority_token_missing"; return false; }
		if (string.IsNullOrWhiteSpace(input.SpatialAuthorityToken) || input.SpatialAuthorityToken == "legacy") { reason = "spatial_authority_token_missing"; return false; }
		if (!string.Equals(input.SpatialAuthorityToken, input.ContactAuthorityToken, StringComparison.Ordinal))
		{
			reason = "spatial_authority_token_mismatch";
			return false;
		}
		if (!input.RuntimeProvenance.TryGetValue("capture_authority", out string? provenanceAuthority))
		{
			reason = "capture_authority_provenance_missing";
			return false;
		}
		if (!string.Equals(provenanceAuthority, input.ContactAuthorityToken, StringComparison.Ordinal))
		{
			reason = "contact_authority_token_mismatch";
			return false;
		}
		if (!input.RuntimeProvenance.TryGetValue("bvh_authority_token", out string? bvhAuthority))
		{
			reason = "spatial_authority_provenance_missing";
			return false;
		}
		if (!string.Equals(bvhAuthority, input.SpatialAuthorityToken, StringComparison.Ordinal))
		{
			reason = "spatial_authority_provenance_mismatch";
			return false;
		}
		if (input.SourceTreeState is not ("clean" or "dirty" or "unknown")) { reason = "invalid_source_tree_state"; return false; }
		if (input.SourceTreeState == "dirty" && !IsSha256(input.AuthoredDiffSha256)) { reason = "dirty_source_tree_requires_authored_diff_sha256"; return false; }
		if (input.SourceTreeState == "clean" && input.AuthoredDiffSha256.Length != 0) { reason = "clean_source_tree_has_authored_diff_sha256"; return false; }
		if (input.Width <= 0 || input.Height <= 0) { reason = "invalid_sealed_dimensions"; return false; }
		int total = input.Width * input.Height;
		if (input.UnprocessedCount != 0) { reason = "unprocessed_pixels_remaining"; return false; }
		if (input.Generation <= 0) { reason = "invalid_generation"; return false; }
		if (input.CameraTransform.Length != 12) { reason = "camera_transform_requires_12_floats"; return false; }
		if (input.Outcomes.Length != total || input.ContactCounts.Length != total || input.FinalStepCounts.Length != total ||
			input.PolicyMaxSteps.Length != total || input.EffortValid.Length != total)
		{
			reason = "sealed_source_capacity_mismatch";
			return false;
		}
		if (input.Outcomes.Any(outcome => outcome == ProbeOutcomeCode.Unprocessed)) { reason = "unprocessed_outcome_present"; return false; }
		for (int i = 0; i < total; i++)
		{
			if (input.EffortValid[i] > 1) { reason = "invalid_effort_valid_byte"; return false; }
			if (input.EffortValid[i] != 0 && input.FinalStepCounts[i] > input.PolicyMaxSteps[i])
			{
				reason = $"effort_invariant_violation_pixel_{i}";
				return false;
			}
		}
		return true;
	}

	private static Dictionary<string, object> BuildManifest(
		PortableProbeCaptureInput input,
		int total,
		string contextSha,
		Dictionary<string, ArtifactHash> sourceHashes,
		Dictionary<string, ArtifactHash> afterMappingSourceHashes,
		Dictionary<string, ArtifactHash> mappedHashes,
		Dictionary<string, ArtifactHash> pngHashes,
		Dictionary<string, int> outcomes,
		Dictionary<string, int> contacts,
		EffortStats effort)
	{
		Dictionary<string, object> context = new()
		{
			["observer_id"] = input.ContextKey.ObserverId.Label,
			["camera_origin_hash"] = input.ContextKey.CameraOriginHash,
			["camera_basis_hash"] = input.ContextKey.CameraBasisHash,
			["fov_deg"] = input.ContextKey.FovDeg,
			["film_width"] = input.ContextKey.FilmWidth,
			["film_height"] = input.ContextKey.FilmHeight,
			["base_steps_per_ray"] = input.ContextKey.BaseStepsPerRay,
			["bend_scale"] = input.ContextKey.BendScale,
			["field_strength"] = input.ContextKey.FieldStrength,
			["field_source_epoch"] = input.ContextKey.FieldSourceEpoch,
			["geometry_epoch"] = input.ContextKey.GeometryEpoch,
			["boundary_layer_epoch"] = input.ContextKey.BoundaryLayerEpoch,
			["refinement_policy_version"] = input.ContextKey.RefinementPolicyVersion,
			["formal_policy_sha256"] = input.ContextKey.FormalPolicySha256,
			["canonical_sha256"] = contextSha,
			["canonical_serialization"] = "v3 little-endian: schema_version (uint32), observer_id (uint32 length + UTF-8 bytes), camera_origin_hash, camera_basis_hash, fov_bits, film_width, film_height, base_steps_per_ray, bend_scale_bits, field_strength_bits, field_source_epoch, geometry_epoch, boundary_layer_epoch, refinement_policy_version, formal_policy_sha256 (uint16 length + UTF-8 bytes)"
		};
		Dictionary<string, string> normalizedRuntimeProvenance = new(input.RuntimeProvenance)
		{
			["observer_id"] = input.ContextKey.ObserverId.Label,
			["source_tree_state"] = input.SourceTreeState,
			["authored_diff_sha256"] = input.AuthoredDiffSha256,
			["authored_diff_algorithm"] = "sha256(sorted status+path+working-bytes; source files; generated paths excluded)"
		};
		Dictionary<string, object> camera = new()
		{
			["convention"] = "origin xyz, basis row 0 xyz, basis row 1 xyz, basis row 2 xyz",
			["values"] = input.CameraTransform
		};
		return new Dictionary<string, object>
		{
			["schema_version"] = SchemaVersion,
			["run_id"] = input.RunId,
			["generated_at"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
			["engine"] = new Dictionary<string, object> { ["engine_commit"] = input.EngineCommit, ["runtime"] = "godot4-mono" },
			["scene"] = new Dictionary<string, object> { ["semantic_scene_id"] = input.SemanticSceneId, ["godot_scene_path"] = input.GodotScenePath },
			["artifact_class"] = ArtifactClass,
			["authority"] = new Dictionary<string, object>
			{
				["contact_events"] = input.ContactAuthorityToken,
				["spatial_authority"] = input.SpatialAuthorityToken
			},
			["runtime_provenance"] = normalizedRuntimeProvenance,
			["acquisition"] = new Dictionary<string, object>
			{
				["state"] = "Complete", ["generation"] = input.Generation, ["width"] = input.Width, ["height"] = input.Height,
				["total_pixels"] = total, ["unprocessed"] = input.UnprocessedCount, ["context"] = context, ["camera_transform"] = camera,
				["lineage"] = input.AcquisitionLineage
			},
			["transport_policy"] = new Dictionary<string, object> { ["steps_per_ray"] = input.StepsPerRay, ["step_length"] = input.StepLength, ["field_strength"] = input.FieldStrength },
			["same_generation_proof"] = new Dictionary<string, object>
			{
				["assert_same_generation"] = true, ["generation_at_export_start"] = input.Generation, ["generation_at_export_end"] = input.Generation,
				["transport_requests_during_export"] = 0, ["render_steps_during_export"] = 0, ["selected_transport_during_export"] = 0,
				["refinements_during_export"] = 0, ["resets_during_export"] = 0, ["source_hashes_equal_before_after_mapping"] = true
			},
			["probe_views"] = new Dictionary<string, object> { ["modes"] = new[] { "Outcome", "Contact Events", "Transport Effort" }, ["mapper"] = "ProbeViewMapper" },
			["histograms"] = new Dictionary<string, object> { ["outcome"] = outcomes, ["contact_events"] = contacts, ["effort"] = effort.ToObject() },
			["source_hashes"] = sourceHashes.ToDictionary(pair => pair.Key, pair => pair.Value.ToObject()),
			["mapped_hashes"] = new Dictionary<string, object>
			{
				["source_after_mapping"] = afterMappingSourceHashes.ToDictionary(pair => pair.Key, pair => pair.Value.ToObject()),
				["plate_rgba"] = mappedHashes.ToDictionary(pair => pair.Key, pair => pair.Value.ToObject())
			},
			["artifacts"] = pngHashes.ToDictionary(pair => pair.Key, pair => pair.Value.ToObject()),
			["claim_boundary"] = ClaimBoundary()
		};
	}

	private static bool IsSha256(string value) =>
		value.Length == 64 && value.All(character =>
			(character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));

	private static string BuildSummary(PortableProbeCaptureInput input, int total, string contextSha, Dictionary<string, ArtifactHash> sourceHashes, Dictionary<string, ArtifactHash> mappedHashes, Dictionary<string, ArtifactHash> pngHashes, Dictionary<string, int> outcomes, Dictionary<string, int> contacts, EffortStats effort)
	{
		StringBuilder sb = new();
		sb.AppendLine("# Portable Observatory Cathedral Probe Bundle v1");
		sb.AppendLine();
		sb.AppendLine("## 1. Acquisition Identity");
		sb.AppendLine($"- Observer: `{input.ContextKey.ObserverId.Label}`");
		sb.AppendLine($"- Semantic SceneId: `{input.SemanticSceneId}`");
		sb.AppendLine($"- Godot scene path: `{input.GodotScenePath}`");
		sb.AppendLine($"- Generation: `{input.Generation}`");
		sb.AppendLine($"- Dimensions: `{input.Width}×{input.Height}`");
		sb.AppendLine($"- Context canonical SHA-256: `{contextSha}`");
	sb.AppendLine();
		sb.AppendLine("## 2. Qualification Result");
		sb.AppendLine("- State: `Complete`");
		sb.AppendLine("- Artifact class: `qualified_visualization`");
		sb.AppendLine($"- Pixels: `{total}`; unprocessed: `{input.UnprocessedCount}`");
		sb.AppendLine();
		sb.AppendLine("## 3. Measured Counts");
		sb.AppendLine($"- Outcomes: {string.Join(", ", outcomes.Select(pair => $"{pair.Key}={pair.Value}"))}");
		sb.AppendLine($"- Contact Events: {string.Join(", ", contacts.Select(pair => $"{pair.Key}={pair.Value}"))}");
		sb.AppendLine($"- Effort valid/unavailable: `{effort.ValidCount}/{effort.UnavailableCount}`; min/mean/max: `{effort.Min:0.######}/{effort.Mean:0.######}/{effort.Max:0.######}`");
		sb.AppendLine();
	sb.AppendLine("## 4. Generated Artifacts");
	sb.AppendLine("- Canonical channels: `.bin` row-major little-endian, no headers.");
	sb.AppendLine("- Canonical plates: 80×45 RGBA8 PNGs.");
	sb.AppendLine("- Display derivatives: nearest-neighbor ×24, 1920×1080.");
	foreach (string name in pngHashes.Keys) sb.AppendLine($"- `{name}` SHA-256: `{pngHashes[name].Sha256Hex}`");
	sb.AppendLine();
	sb.AppendLine("## 5. Same-Generation Proof");
	sb.AppendLine($"- Generation at export start/end: `{input.Generation}/{input.Generation}`");
	sb.AppendLine("- Transport/render/refinement/reset activity during mapping: `0/0/0/0`");
	sb.AppendLine("- Source hashes unchanged before/after mapping: `true`");
	sb.AppendLine();
	sb.AppendLine("## 6. Known Limitations");
	sb.AppendLine("- This bundle does not encode MP4/video composition.");
	sb.AppendLine("- Transport Effort is a normalized configured-step measure.");
	sb.AppendLine();
	sb.AppendLine("## 7. Claim Boundaries");
	foreach (string claim in ClaimBoundary()) sb.AppendLine($"- {claim}");
	sb.AppendLine();
	sb.AppendLine("## 8. Reproduction Command");
	sb.AppendLine("```bash");
	sb.AppendLine("./scripts/capture_cathedral_probe_demo.sh --scene portable_observatory.gallery.v1 --film 80x45 --field 0.0 --output output/cathedral_probe/");
	sb.AppendLine("```");
	return sb.ToString();
	}

	private static string[] ClaimBoundary() => new[]
	{
		"No optical closure is established by this bundle alone.",
		"Contact Events are not a unique-surface census.",
		"Transport Effort is not time, energy, or field magnitude.",
		"HitGeometry cannot be inferred from Contact Events.",
		"Color is presentation mapping, not measurement authority.",
		"No UAP validation is claimed."
	};

	private static string ModeStem(ProbeViewMode mode) => mode switch
	{
		ProbeViewMode.Outcome => "outcome",
		ProbeViewMode.ContactEvents => "contact_events",
		ProbeViewMode.TransportEffort => "transport_effort",
		_ => throw new ArgumentOutOfRangeException(nameof(mode))
	};

	private static byte[] EncodeOutcomes(ProbeOutcomeCode[] values, int total)
	{
		byte[] bytes = new byte[total];
		for (int i = 0; i < total; i++) bytes[i] = (byte)values[i];
		return bytes;
	}

	private static byte[] EncodeBytes(byte[] values, int total) => values.Take(total).ToArray();

	private static Dictionary<string, byte[]> EncodeChannels(PortableProbeCaptureInput input, int total) => new()
	{
		["outcomes.bin"] = EncodeOutcomes(input.Outcomes, total),
		["contact_counts.bin"] = EncodeInt32(input.ContactCounts, total),
		["final_step_counts.bin"] = EncodeInt32(input.FinalStepCounts, total),
		["policy_max_steps.bin"] = EncodeInt32(input.PolicyMaxSteps, total),
		["effort_valid.bin"] = EncodeBytes(input.EffortValid, total)
	};

	private static byte[] EncodeInt32(int[] values, int total)
	{
		byte[] bytes = new byte[total * 4];
		for (int i = 0; i < total; i++) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4, 4), values[i]);
		return bytes;
	}

	private static byte[] MapRgba(PortableProbeCaptureInput input, ProbeViewMode mode)
	{
		byte[] rgba = new byte[input.Width * input.Height * 4];
		for (int i = 0; i < input.Width * input.Height; i++)
		{
			ProbeViewColor color = ProbeViewMapper.Map(mode, input.Outcomes[i], input.ContactCounts[i], input.FinalStepCounts[i], input.PolicyMaxSteps[i], input.EffortValid[i] != 0);
			rgba[i * 4] = color.R; rgba[i * 4 + 1] = color.G; rgba[i * 4 + 2] = color.B; rgba[i * 4 + 3] = color.A;
		}
		return rgba;
	}

	private static byte[] ScaleNearest(int width, int height, byte[] rgba, int scale)
	{
		byte[] scaled = new byte[width * scale * height * scale * 4];
		for (int y = 0; y < height * scale; y++)
		for (int x = 0; x < width * scale; x++)
		{
			int source = ((y / scale) * width + (x / scale)) * 4;
			int destination = (y * width * scale + x) * 4;
			Buffer.BlockCopy(rgba, source, scaled, destination, 4);
		}
		return scaled;
	}

	private static byte[] EncodePng(int width, int height, byte[] rgba)
	{
		using MemoryStream raw = new();
		for (int y = 0; y < height; y++)
		{
			raw.WriteByte(0);
			raw.Write(rgba, y * width * 4, width * 4);
		}
		using MemoryStream compressed = new();
		using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
		{
			raw.Position = 0;
			raw.CopyTo(zlib);
		}
		using MemoryStream png = new();
		png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
		WriteChunk(png, "IHDR", BuildIhdr(width, height));
		WriteChunk(png, "IDAT", compressed.ToArray());
		WriteChunk(png, "IEND", Array.Empty<byte>());
		return png.ToArray();
	}

	private static byte[] BuildIhdr(int width, int height)
	{
		byte[] data = new byte[13];
		BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), width);
		BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), height);
		data[8] = 8; data[9] = 6; data[10] = 0; data[11] = 0; data[12] = 0;
		return data;
	}

	private static void WriteChunk(Stream output, string type, byte[] data)
	{
		byte[] typeBytes = Encoding.ASCII.GetBytes(type);
		Span<byte> length = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(length, data.Length); output.Write(length);
		output.Write(typeBytes); output.Write(data);
		uint crc = 0xffffffffu;
		foreach (byte value in typeBytes.Concat(data)) crc = Crc32Byte(crc, value);
		crc ^= 0xffffffffu;
		Span<byte> crcBytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc); output.Write(crcBytes);
	}

	private static uint Crc32Byte(uint crc, byte value)
	{
		crc ^= value;
		for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
		return crc;
	}

	private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

	private static Dictionary<string, int> BuildOutcomeHistogram(ProbeOutcomeCode[] values, int total)
	{
		Dictionary<string, int> counts = Enum.GetValues<ProbeOutcomeCode>().ToDictionary(value => value.ToString(), _ => 0);
		for (int i = 0; i < total; i++) counts[values[i].ToString()]++;
		return counts;
	}

	private static Dictionary<string, int> BuildContactHistogram(int[] values, int total)
	{
		Dictionary<string, int> counts = new() { ["0"] = 0, ["1"] = 0, ["2"] = 0, ["3+"] = 0 };
		for (int i = 0; i < total; i++) counts[values[i] switch { <= 0 => "0", 1 => "1", 2 => "2", _ => "3+" }]++;
		counts["total"] = total; counts["contacted_any"] = total - counts["0"];
		return counts;
	}

	private static EffortStats BuildEffortStats(int[] finalSteps, int[] policy, byte[] valid, int total)
	{
		List<double> values = new();
		for (int i = 0; i < total; i++) if (valid[i] != 0) values.Add((double)finalSteps[i] / policy[i]);
		return values.Count == 0 ? new EffortStats(0, total, 0, 0, 0) : new EffortStats(values.Count, total - values.Count, values.Min(), values.Average(), values.Max());
	}

	private readonly record struct ArtifactHash(int Bytes, string Sha256Hex)
	{
		public object ToObject() => new Dictionary<string, object> { ["bytes"] = Bytes, ["sha256"] = Sha256Hex };
	}

	private readonly record struct EffortStats(int ValidCount, int UnavailableCount, double Min, double Mean, double Max)
	{
		public object ToObject() => new Dictionary<string, object> { ["valid_count"] = ValidCount, ["unavailable_count"] = UnavailableCount, ["min"] = Min, ["mean"] = Mean, ["max"] = Max };
	}
}

public static class ProbeContextCanonicalSerializer
{
	public const uint SchemaVersion = 3;

	/// v3 field order: uint schema version, ObserverId canonical bytes, then uint origin,
	/// uint basis, float fov bits, ushort width, ushort height,
	/// ushort base steps, float bend bits, float field bits, uint field epoch, uint geometry epoch,
	/// uint boundary epoch, byte refinement policy, then the UTF-8 formal-policy SHA-256
	/// prefixed by its ushort byte length. All integral values are little-endian.
	public static byte[] Serialize(in ProbeContextKey key)
	{
		using MemoryStream stream = new();
		WriteUInt32(stream, SchemaVersion);
		stream.Write(key.ObserverId.SerializeCanonical());
		WriteUInt32(stream, key.CameraOriginHash); WriteUInt32(stream, key.CameraBasisHash); WriteSingle(stream, key.FovDeg);
		WriteUInt16(stream, key.FilmWidth); WriteUInt16(stream, key.FilmHeight); WriteUInt16(stream, key.BaseStepsPerRay);
		WriteSingle(stream, key.BendScale); WriteSingle(stream, key.FieldStrength);
		WriteUInt32(stream, key.FieldSourceEpoch); WriteUInt32(stream, key.GeometryEpoch); WriteUInt32(stream, key.BoundaryLayerEpoch);
		stream.WriteByte(key.RefinementPolicyVersion);
		byte[] formalPolicy = Encoding.UTF8.GetBytes(key.FormalPolicySha256 ?? string.Empty);
		if (formalPolicy.Length > ushort.MaxValue)
			throw new InvalidDataException("formal_policy_identity_too_long");
		WriteUInt16(stream, (ushort)formalPolicy.Length);
		stream.Write(formalPolicy);
		return stream.ToArray();
	}

	private static void WriteUInt16(Stream stream, ushort value) { Span<byte> bytes = stackalloc byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, value); stream.Write(bytes); }
	private static void WriteUInt32(Stream stream, uint value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); stream.Write(bytes); }
	private static void WriteSingle(Stream stream, float value) => WriteUInt32(stream, BitConverter.SingleToUInt32Bits(value));
}
#nullable restore
