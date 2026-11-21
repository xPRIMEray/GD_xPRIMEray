using System;

/// <summary>
/// Presentation-only field-structure modes cycled by F. They select which existing
/// FieldSource3D diagnostic drawing is shown; they never change field physics.
/// Rays are illustrative local single-source density rays, not per-pixel transport.
/// </summary>
public enum FieldStructureMode
{
	Hidden = 0,
	Vectors = 1,
	Rays = 2,
}

public static class FieldStructureVisibilityPolicy
{
	/// <summary>
	/// Resolves one deterministic target for a group toggle. Mixed or all-hidden
	/// sources become visible; an already all-visible group becomes hidden.
	/// </summary>
	public static bool ResolveToggleTarget(ReadOnlySpan<bool> eligibleVisibleStates)
	{
		if (eligibleVisibleStates.Length == 0)
			return false;

		for (int i = 0; i < eligibleVisibleStates.Length; i++)
		{
			if (!eligibleVisibleStates[i])
				return true;
		}

		return false;
	}

	/// <summary>F cycle: Hidden, Vectors, Rays, Hidden.</summary>
	public static FieldStructureMode Next(FieldStructureMode current) => current switch
	{
		FieldStructureMode.Hidden => FieldStructureMode.Vectors,
		FieldStructureMode.Vectors => FieldStructureMode.Rays,
		_ => FieldStructureMode.Hidden,
	};

	/// <summary>
	/// Next mode given whether the sources still match the current mode. When another
	/// writer has left a Vectors or Rays presentation mixed, F first normalizes to Hidden
	/// (restoring authored flags). Hidden always advances, so the cycle cannot stall.
	/// </summary>
	public static FieldStructureMode ResolveNext(FieldStructureMode current, bool sourcesConsistent) =>
		!sourcesConsistent && current != FieldStructureMode.Hidden ? FieldStructureMode.Hidden : Next(current);

	/// <summary>Operator-facing name; "Mixed" reports sources that no longer match the mode.</summary>
	public static string DisplayName(FieldStructureMode mode, bool sourcesConsistent = true)
	{
		if (!sourcesConsistent)
			return "Mixed";
		return mode switch
		{
			FieldStructureMode.Vectors => "Vectors",
			FieldStructureMode.Rays => "Rays",
			_ => "Hidden",
		};
	}

	/// <summary>
	/// Per-source presentation flags required by a non-Hidden mode. Disabled physical
	/// fields are never drawn, so they cannot be read as active transport.
	/// Hidden restores each source's authored flags instead.
	/// </summary>
	public static void ResolveSourceFlags(
		FieldStructureMode mode,
		bool sourceEnabled,
		out bool drawInGame,
		out bool showVectors,
		out bool showRays)
	{
		drawInGame = sourceEnabled && mode != FieldStructureMode.Hidden;
		showVectors = mode == FieldStructureMode.Vectors;
		showRays = mode == FieldStructureMode.Rays;
	}
}
