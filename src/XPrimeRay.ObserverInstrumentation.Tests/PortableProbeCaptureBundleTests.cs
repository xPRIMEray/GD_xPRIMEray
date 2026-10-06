using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using XPrimeRay.ObservationLayer;

namespace XPrimeRay.ObserverInstrumentation.Tests;

internal static class PortableProbeCaptureBundleTests
{
	public static void Run()
	{
		ContextSerializationIsStable();
		ObserverIdentityIsStructuralAndContextScoped();
		ManifestSealsObserverAttribution();
		BundleEncodingAndHashesAreStable();
		QualificationRejectsInvalidEffortAndIncompleteSources();
		AuthorityTokenMismatchIsRejected();
	}

	private static void ContextSerializationIsStable()
	{
		ProbeContextKey key = new(ObserverId.A, 1, 2, 70.0f, 80, 45, 80, 0.12f, 0.0f, 3, 4, 5, 1, "abc123");
		byte[] first = ProbeContextCanonicalSerializer.Serialize(key);
		byte[] second = ProbeContextCanonicalSerializer.Serialize(key);
		TestAssert.True(first.SequenceEqual(second), "context canonical bytes deterministic");
		TestAssert.Equal(56, first.Length, "context canonical byte length");
		ProbeContextKey changed = new(ObserverId.A, 1, 2, 70.0f, 80, 45, 80, 0.12f, 0.5f, 3, 4, 5, 1);
		TestAssert.False(SHA256.HashData(first).SequenceEqual(SHA256.HashData(ProbeContextCanonicalSerializer.Serialize(changed))), "changed field changes context hash");
		TestAssert.Equal((byte)0x03, first[0], "context schema little-endian low byte");
		TestAssert.Equal((byte)0x01, first[9], "context origin low byte after observer identity");
		ProbeContextKey changedPolicy = new(ObserverId.A, 1, 2, 70.0f, 80, 45, 80, 0.12f, 0.0f, 3, 4, 5, 1, "def456");
		TestAssert.False(SHA256.HashData(first).SequenceEqual(SHA256.HashData(ProbeContextCanonicalSerializer.Serialize(changedPolicy))), "changed formal policy changes context hash");
	}

	private static void ObserverIdentityIsStructuralAndContextScoped()
	{
		TestAssert.Equal(ObserverId.A, new ObserverId("A"), "observer A structural equality");
		TestAssert.Equal(ObserverId.A.GetHashCode(), new ObserverId("A").GetHashCode(), "observer A stable hash");
		TestAssert.False(ObserverId.A == ObserverId.B, "observer A differs from B");
		TestAssert.True(ObserverId.A.SerializeCanonical().SequenceEqual(new ObserverId("A").SerializeCanonical()), "observer canonical bytes stable");
		ProbeContextKey a = new(ObserverId.A, 1, 2, 70f, 80, 45, 80, 0.12f, 0f, 3, 4, 5, 1);
		ProbeContextKey b = new(ObserverId.B, 1, 2, 70f, 80, 45, 80, 0.12f, 0f, 3, 4, 5, 1);
		TestAssert.False(a == b, "observer identity changes context equality");
		TestAssert.False(SHA256.HashData(ProbeContextCanonicalSerializer.Serialize(a)).SequenceEqual(SHA256.HashData(ProbeContextCanonicalSerializer.Serialize(b))), "observer identity changes context hash");
		ObservationPolicyFingerprint policyA = new(80, 0.07f, 0.01f, 0.07f, 1f, 0.5f, 3, 4, 5, 1, "runtime_current");
		ObservationPolicyFingerprint policyB = new(80, 0.07f, 0.01f, 0.07f, 1f, 0.5f, 3, 4, 5, 1, "runtime_current");
		TestAssert.True(policyA.Equals(policyB), "observer identity absent from policy fingerprint");
	}

	private static void ManifestSealsObserverAttribution()
	{
		string root = Path.Combine(Path.GetTempPath(), "observer-attribution-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			PortableProbeCaptureInput input = CreateInput(Path.Combine(root, "b"));
			ProbeContextKey observerB = new(ObserverId.B, 1, 2, 70f, 2, 2, 80, 0.12f, 0f, 3, 4, 5, 1);
			input = CopyWith(input, context: observerB, observerProvenance: "A");
			TestAssert.True(PortableProbeCaptureBundle.TryWrite(input, out PortableProbeCaptureResult? result, out string reason), reason);
			using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(result!.ManifestPath));
			TestAssert.Equal("1.2.0", manifest.RootElement.GetProperty("schema_version").GetString(), "bundle schema version");
			TestAssert.Equal("B", manifest.RootElement.GetProperty("acquisition").GetProperty("context").GetProperty("observer_id").GetString(), "context observer attribution");
			TestAssert.Equal("B", manifest.RootElement.GetProperty("runtime_provenance").GetProperty("observer_id").GetString(), "normalized provenance attribution");
			TestAssert.Equal("compute-policy-test", manifest.RootElement.GetProperty("runtime_provenance").GetProperty("compute_marker").GetString(), "compute provenance preserved");
			TestAssert.True(File.ReadAllText(result.SummaryPath).Contains("- Observer: `B`", StringComparison.Ordinal), "summary observer line");
			string description = manifest.RootElement.GetProperty("acquisition").GetProperty("context").GetProperty("canonical_serialization").GetString() ?? string.Empty;
			TestAssert.True(description.StartsWith("v3 little-endian: schema_version (uint32), observer_id (uint32 length + UTF-8 bytes)", StringComparison.Ordinal), "v3 serialization description");

			PortableProbeCaptureInput inputA = CreateInput(Path.Combine(root, "a"));
			TestAssert.True(PortableProbeCaptureBundle.TryWrite(inputA, out _, out string reasonA), reasonA);
			TestAssert.True(File.ReadAllBytes(Path.Combine(root, "a", "contact_counts.bin")).SequenceEqual(File.ReadAllBytes(Path.Combine(root, "b", "contact_counts.bin"))), "observer does not change contact bytes");
		}
		finally
		{
			if (Directory.Exists(root)) Directory.Delete(root, true);
		}
	}

	private static void BundleEncodingAndHashesAreStable()
	{
		string root = Path.Combine(Path.GetTempPath(), "portable-probe-bundle-tests-" + Guid.NewGuid().ToString("N"));
		string firstDir = Path.Combine(root, "first");
		string secondDir = Path.Combine(root, "second");
		Directory.CreateDirectory(root);
		try
		{
			PortableProbeCaptureInput input = CreateInput(firstDir);
			TestAssert.True(PortableProbeCaptureBundle.TryWrite(input, out PortableProbeCaptureResult? first, out string firstReason), firstReason);
			PortableProbeCaptureInput repeat = CreateInput(secondDir);
			TestAssert.True(PortableProbeCaptureBundle.TryWrite(repeat, out PortableProbeCaptureResult? second, out string secondReason), secondReason);
			TestAssert.Equal(first!.Generation, second!.Generation, "repeated export generation");
			foreach (string name in new[] { "outcomes.bin", "contact_counts.bin", "final_step_counts.bin", "policy_max_steps.bin", "effort_valid.bin", "outcome.png", "contact_events.png", "transport_effort.png", "outcome_display.png", "contact_events_display.png", "transport_effort_display.png" })
				TestAssert.True(File.ReadAllBytes(Path.Combine(firstDir, name)).SequenceEqual(File.ReadAllBytes(Path.Combine(secondDir, name))), $"repeated artifact stable: {name}");
			byte[] contact = File.ReadAllBytes(Path.Combine(firstDir, "contact_counts.bin"));
			TestAssert.Equal(16, contact.Length, "binary length matches dimensions");
			TestAssert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(contact.AsSpan(4, 4)), "signed int32 little-endian encoding");
			using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(first.ManifestPath));
			TestAssert.Equal(4, manifest.RootElement.GetProperty("histograms").GetProperty("contact_events").GetProperty("total").GetInt32(), "contact histogram conserves total");
			TestAssert.Equal(3, manifest.RootElement.GetProperty("histograms").GetProperty("effort").GetProperty("valid_count").GetInt32(), "effort excludes unavailable");
			TestAssert.Equal(48, ReadPngWidth(Path.Combine(firstDir, "contact_events_display.png")), "display width scale");
			TestAssert.Equal(48, ReadPngHeight(Path.Combine(firstDir, "contact_events_display.png")), "display height scale");
			TestAssert.Equal(1920, 80 * 24, "80x45 display width");
			TestAssert.Equal(1080, 45 * 24, "80x45 display height");
		}
		finally
		{
			if (Directory.Exists(root)) Directory.Delete(root, true);
		}
	}

	private static void QualificationRejectsInvalidEffortAndIncompleteSources()
	{
		PortableProbeCaptureInput invalid = CreateInput(Path.Combine(Path.GetTempPath(), "not-written"));
		invalid = CopyWith(invalid, effortValid: new byte[] { 1, 1, 1, 1 }, finalSteps: new[] { 81, 82, 81, 81 });
		TestAssert.False(PortableProbeCaptureBundle.TryWrite(invalid, out _, out string effortReason), "effort overrun rejected");
		TestAssert.True(effortReason.Contains("effort_invariant", StringComparison.Ordinal), "effort rejection reason");
		PortableProbeCaptureInput shortSource = CopyWith(CreateInput(Path.Combine(Path.GetTempPath(), "not-written")), contacts: new[] { 0, 1, 2 });
		TestAssert.False(PortableProbeCaptureBundle.TryWrite(shortSource, out _, out string shortReason), "short source rejected");
		TestAssert.True(shortReason.Contains("capacity", StringComparison.Ordinal), "short source reason");
		PortableProbeCaptureInput incomplete = CopyWith(CreateInput(Path.Combine(Path.GetTempPath(), "not-written")), complete: false);
		TestAssert.False(PortableProbeCaptureBundle.TryWrite(incomplete, out _, out string incompleteReason), "incomplete acquisition rejected");
		TestAssert.Equal("lifecycle_not_complete", incompleteReason, "incomplete rejection reason");
	}

	private static void AuthorityTokenMismatchIsRejected()
	{
		PortableProbeCaptureInput input = CreateInput(Path.Combine(Path.GetTempPath(), "not-written"));
		input = CopyWith(input, authority: "XPrimeRaySpatialKernel/LinearScan-v0", provenanceAuthority: "XPrimeRaySpatialKernel/BVH-v0");
		TestAssert.False(PortableProbeCaptureBundle.TryWrite(input, out _, out string reason), "authority mismatch rejected");
		TestAssert.Equal("contact_authority_token_mismatch", reason, "authority mismatch reason");
	}

	private static PortableProbeCaptureInput CreateInput(string output)
	{
		PortableProbeCaptureInput result = new PortableProbeCaptureInput
		{
			RunId = "test",
			OutputDirectory = output,
			SemanticSceneId = "portable_observatory.gallery.v1",
			GodotScenePath = "res://ObservatoryWorkbench.tscn",
			EngineCommit = "test",
			AcquisitionLineage = "test",
			LifecycleComplete = true,
			SealedAuthorityAvailable = true,
			Generation = 1,
			ContextKey = new ProbeContextKey(ObserverId.A, 1, 2, 70f, 2, 2, 80, 0.12f, 0f, 3, 4, 5, 1),
			CameraTransform = Enumerable.Repeat(0f, 12).ToArray(),
			Width = 2,
			Height = 2,
			UnprocessedCount = 0,
			StepsPerRay = 80,
			StepLength = 0.07f,
			FieldStrength = 0f,
			Outcomes = new[] { ProbeOutcomeCode.MaxStepsExhausted, ProbeOutcomeCode.HitGeometry, ProbeOutcomeCode.BackgroundResolved, ProbeOutcomeCode.Invalid },
			ContactCounts = new[] { 0, 1, 2, 3 },
			FinalStepCounts = new[] { 0, 40, 81, 80 },
			PolicyMaxSteps = new[] { 81, 81, 81, 81 },
			EffortValid = new byte[] { 1, 1, 1, 0 },
			ContactAuthorityToken = "XPrimeRaySpatialKernel/LinearScan-v0",
			RuntimeProvenance = new Dictionary<string, string> { ["capture_authority"] = "XPrimeRaySpatialKernel/LinearScan-v0", ["compute_marker"] = "compute-policy-test" }
		};
		return result;
	}

	private static PortableProbeCaptureInput CopyWith(PortableProbeCaptureInput input, bool? complete = null, int[]? contacts = null, int[]? finalSteps = null, byte[]? effortValid = null, string? authority = null, string? provenanceAuthority = null, ProbeContextKey? context = null, string? observerProvenance = null)
	{
		PortableProbeCaptureInput result = new PortableProbeCaptureInput
		{
			RunId = input.RunId, OutputDirectory = input.OutputDirectory, SemanticSceneId = input.SemanticSceneId, GodotScenePath = input.GodotScenePath,
			EngineCommit = input.EngineCommit, AcquisitionLineage = input.AcquisitionLineage, LifecycleComplete = complete ?? input.LifecycleComplete,
			SealedAuthorityAvailable = input.SealedAuthorityAvailable, Generation = input.Generation, ContextKey = context ?? input.ContextKey,
			CameraTransform = input.CameraTransform, Width = input.Width, Height = input.Height, UnprocessedCount = input.UnprocessedCount,
			StepsPerRay = input.StepsPerRay, StepLength = input.StepLength, FieldStrength = input.FieldStrength,
			Outcomes = input.Outcomes, ContactCounts = contacts ?? input.ContactCounts, FinalStepCounts = finalSteps ?? input.FinalStepCounts,
			PolicyMaxSteps = input.PolicyMaxSteps, EffortValid = effortValid ?? input.EffortValid,
			ContactAuthorityToken = authority ?? input.ContactAuthorityToken,
			RuntimeProvenance = new Dictionary<string, string>(input.RuntimeProvenance)
		};
		if (provenanceAuthority != null) result.RuntimeProvenance["capture_authority"] = provenanceAuthority;
		if (observerProvenance != null) result.RuntimeProvenance["observer_id"] = observerProvenance;
		return result;
	}

	private static int ReadPngWidth(string path) => BinaryPrimitives.ReadInt32BigEndian(File.ReadAllBytes(path).AsSpan(16, 4));
	private static int ReadPngHeight(string path) => BinaryPrimitives.ReadInt32BigEndian(File.ReadAllBytes(path).AsSpan(20, 4));
}
