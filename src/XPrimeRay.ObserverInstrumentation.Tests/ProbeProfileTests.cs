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
		TestAssert.Equal("pixel-work-unit-commit-v1", ProbeProfileCatalog.FormalQueryCanonicalizationPolicy, "Formal retry transaction policy is versioned");
		TestAssert.Equal("band-evidence-commit-v1", ProbeProfileCatalog.FormalAdaptiveWorkingStatePolicy, "Formal adaptive commit policy is versioned");
		TestAssert.Equal(ProbeProfileId.Motion, ProbeProfileCatalog.Next(ProbeProfileId.Cathedral), "cycle Cathedral to Motion");
		TestAssert.Equal(ProbeProfileId.Cathedral, ProbeProfileCatalog.Next(ProbeProfileId.Motion), "cycle Motion to Cathedral");
		TestAssert.Equal(ProbeProfileId.Motion, ProbeProfileCatalog.FromIndex(-1), "negative index wraps deterministically");
	}
}
