#nullable enable

using System;

/// <summary>
/// Human-selected instrument strategies. A profile persists across acquisitions;
/// acquisition working state does not.
/// </summary>
public enum ProbeProfileId : byte
{
	Cathedral = 0,
	Motion = 1
}

/// <summary>
/// Stable profile metadata. FormalPolicyId identifies the declared formal policy;
/// profiles may intentionally share it when they differ only in LIVE execution.
/// </summary>
public readonly struct ProbeProfileDefinition
{
	public readonly ProbeProfileId Id;
	public readonly string StableId;
	public readonly string DisplayName;
	public readonly string LiveExecutionPolicyId;
	public readonly string FormalPolicyId;

	public ProbeProfileDefinition(
		ProbeProfileId id,
		string stableId,
		string displayName,
		string liveExecutionPolicyId,
		string formalPolicyId)
	{
		Id = id;
		StableId = stableId;
		DisplayName = displayName;
		LiveExecutionPolicyId = liveExecutionPolicyId;
		FormalPolicyId = formalPolicyId;
	}
}

public static class ProbeProfileCatalog
{
	public const string CathedralFormalPolicyId = "cathedral.formal.v1";
	public const string FormalQueryCanonicalizationPolicy = "pixel-work-unit-commit-v1";
	public const string FormalAdaptiveWorkingStatePolicy = "band-evidence-commit-v1";

	private static readonly ProbeProfileDefinition[] Profiles =
	{
		new(
			ProbeProfileId.Cathedral,
			"cathedral",
			"Cathedral",
			"accurate",
			CathedralFormalPolicyId),
		new(
			ProbeProfileId.Motion,
			"motion",
			"Motion",
			"cheap-motion",
			CathedralFormalPolicyId)
	};

	public static int Count => Profiles.Length;

	public static ProbeProfileDefinition Get(ProbeProfileId id)
	{
		int index = (int)id;
		return (uint)index < (uint)Profiles.Length ? Profiles[index] : Profiles[0];
	}

	public static ProbeProfileId FromIndex(int index)
	{
		if (Profiles.Length == 0)
			return ProbeProfileId.Cathedral;
		int normalized = ((index % Profiles.Length) + Profiles.Length) % Profiles.Length;
		return Profiles[normalized].Id;
	}

	public static ProbeProfileId Next(ProbeProfileId current)
	{
		return FromIndex((int)current + 1);
	}
}

#nullable restore
