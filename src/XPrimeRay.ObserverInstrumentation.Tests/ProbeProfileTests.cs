namespace XPrimeRay.ObserverInstrumentation.Tests;

internal static class ProbeProfileTests
{
	public static void Run()
	{
		TestAssert.Equal(2, ProbeProfileCatalog.Count, "v0 exposes only archaeology-supported profiles");
		ProbeProfileDefinition cathedral = ProbeProfileCatalog.Get(ProbeProfileId.Cathedral);
		ProbeProfileDefinition motion = ProbeProfileCatalog.Get(ProbeProfileId.Motion);
		TestAssert.Equal("cathedral", cathedral.StableId, "Cathedral stable identity");
		TestAssert.Equal("accurate", cathedral.LiveExecutionPolicyId, "Cathedral LIVE policy");
		TestAssert.Equal("motion", motion.StableId, "Motion stable identity");
		TestAssert.Equal("cheap-motion", motion.LiveExecutionPolicyId, "Motion LIVE policy");
		TestAssert.Equal(cathedral.FormalPolicyId, motion.FormalPolicyId, "LIVE-only profile distinction does not invent a Formal identity");
		TestAssert.Equal("cathedral.formal.v1", cathedral.FormalPolicyId, "Cathedral retains the qualified Formal policy");
		TestAssert.Equal("cathedral.formal.v1", motion.FormalPolicyId, "Motion retains the qualified Formal policy");
		VerifyRequestOwnedDistinctFormalPolicies();
		TestAssert.Equal("pixel-work-unit-commit-v1", ProbeProfileCatalog.FormalQueryCanonicalizationPolicy, "Formal retry transaction policy is versioned");
		TestAssert.Equal("band-evidence-commit-v1", ProbeProfileCatalog.FormalAdaptiveWorkingStatePolicy, "Formal adaptive commit policy is versioned");
		TestAssert.Equal(ProbeProfileId.Motion, ProbeProfileCatalog.Next(ProbeProfileId.Cathedral), "cycle Cathedral to Motion");
		TestAssert.Equal(ProbeProfileId.Cathedral, ProbeProfileCatalog.Next(ProbeProfileId.Motion), "cycle Motion to Cathedral");
		TestAssert.Equal(ProbeProfileId.Motion, ProbeProfileCatalog.FromIndex(-1), "negative index wraps deterministically");
	}

	private static void VerifyRequestOwnedDistinctFormalPolicies()
	{
		ProbeProfileDefinition alpha = new(
			ProbeProfileId.Cathedral,
			"synthetic-alpha",
			"Synthetic Alpha",
			"synthetic-live-alpha",
			"synthetic.formal.alpha.v1");
		ProbeProfileDefinition beta = new(
			ProbeProfileId.Motion,
			"synthetic-beta",
			"Synthetic Beta",
			"synthetic-live-beta",
			"synthetic.formal.beta.v1");

		RequestOwnedProfile request = RequestOwnedProfile.Capture(alpha);
		ProbeProfileDefinition selectedAfterRequest = beta;
		TestAssert.Equal("synthetic.formal.alpha.v1", request.FormalPolicyId,
			"request freezes the selected Formal policy identity");
		TestAssert.Equal("synthetic-alpha", request.ProfileStableId,
			"request freezes profile provenance");
		TestAssert.Equal("synthetic.formal.beta.v1", selectedAfterRequest.FormalPolicyId,
			"later human selection remains independently identifiable");
		TestAssert.True(request.FormalPolicyId != selectedAfterRequest.FormalPolicyId,
			"different Formal policies must not be treated as interchangeable");
		RequestOwnedProfile sealedProvenance = request.Seal();
		TestAssert.Equal(request.FormalPolicyId, sealedProvenance.FormalPolicyId,
			"sealed provenance remains request-owned");
		TestAssert.Equal(request.ProfileStableId, sealedProvenance.ProfileStableId,
			"sealed profile metadata remains request-owned");
	}

	private readonly record struct RequestOwnedProfile(string ProfileStableId, string FormalPolicyId)
	{
		public static RequestOwnedProfile Capture(ProbeProfileDefinition selected) =>
			new(selected.StableId, selected.FormalPolicyId);

		public RequestOwnedProfile Seal() => this;
	}
}
