namespace XPrimeRay.ObserverInstrumentation.Tests;

internal static class ProbeViewTests
{
	public static void Run()
	{
		TestAssert.False(FieldStructureVisibilityPolicy.ResolveToggleTarget(new[] { true, true }), "all visible toggles hidden");
		TestAssert.True(FieldStructureVisibilityPolicy.ResolveToggleTarget(new[] { false, false }), "all hidden toggles visible");
		TestAssert.True(FieldStructureVisibilityPolicy.ResolveToggleTarget(new[] { true, false }), "mixed states normalize visible");
		TestAssert.False(FieldStructureVisibilityPolicy.ResolveToggleTarget(new[] { true, true }), "normalized visible toggles hidden");
		TestAssert.False(FieldStructureVisibilityPolicy.ResolveToggleTarget(Array.Empty<bool>()), "no eligible sources remain hidden");

		// FIELDVIZ-R0: three-state F cycle, mixed normalization, attribution-safe source flags.
		TestAssert.Equal(FieldStructureMode.Vectors, FieldStructureVisibilityPolicy.Next(FieldStructureMode.Hidden), "F Hidden -> Vectors");
		TestAssert.Equal(FieldStructureMode.Rays, FieldStructureVisibilityPolicy.Next(FieldStructureMode.Vectors), "F Vectors -> Rays");
		TestAssert.Equal(FieldStructureMode.Hidden, FieldStructureVisibilityPolicy.Next(FieldStructureMode.Rays), "F Rays -> Hidden");
		TestAssert.Equal(FieldStructureMode.Hidden, FieldStructureVisibilityPolicy.Next((FieldStructureMode)99), "undefined mode resolves Hidden");
		TestAssert.Equal(FieldStructureMode.Hidden, FieldStructureVisibilityPolicy.ResolveNext(FieldStructureMode.Vectors, false), "mixed Vectors normalizes Hidden");
		TestAssert.Equal(FieldStructureMode.Hidden, FieldStructureVisibilityPolicy.ResolveNext(FieldStructureMode.Rays, false), "mixed Rays normalizes Hidden");
		TestAssert.Equal(FieldStructureMode.Vectors, FieldStructureVisibilityPolicy.ResolveNext(FieldStructureMode.Hidden, false), "mixed Hidden still advances");
		TestAssert.Equal(FieldStructureMode.Rays, FieldStructureVisibilityPolicy.ResolveNext(FieldStructureMode.Vectors, true), "consistent Vectors advances");
		TestAssert.Equal("Hidden", FieldStructureVisibilityPolicy.DisplayName(FieldStructureMode.Hidden), "Hidden label");
		TestAssert.Equal("Vectors", FieldStructureVisibilityPolicy.DisplayName(FieldStructureMode.Vectors), "Vectors label");
		TestAssert.Equal("Rays", FieldStructureVisibilityPolicy.DisplayName(FieldStructureMode.Rays), "Rays label");
		TestAssert.Equal("Mixed", FieldStructureVisibilityPolicy.DisplayName(FieldStructureMode.Rays, false), "Mixed label");
		FieldStructureVisibilityPolicy.ResolveSourceFlags(FieldStructureMode.Vectors, true, out bool vDraw, out bool vVec, out bool vRay);
		TestAssert.True(vDraw && vVec && !vRay, "Vectors draws vectors only");
		FieldStructureVisibilityPolicy.ResolveSourceFlags(FieldStructureMode.Rays, true, out bool rDraw, out bool rVec, out bool rRay);
		TestAssert.True(rDraw && !rVec && rRay, "Rays draws rays only");
		FieldStructureVisibilityPolicy.ResolveSourceFlags(FieldStructureMode.Rays, false, out bool dDraw, out _, out _);
		TestAssert.False(dDraw, "disabled physical field is never drawn");
		FieldStructureVisibilityPolicy.ResolveSourceFlags(FieldStructureMode.Vectors, false, out bool dvDraw, out _, out _);
		TestAssert.False(dvDraw, "disabled physical field draws no vectors");

		TestAssert.Equal(ProbeViewMode.ContactEvents, ProbeViewCycle.Next(ProbeViewMode.Outcome), "Q next outcome");
		TestAssert.Equal(ProbeViewMode.TransportEffort, ProbeViewCycle.Next(ProbeViewMode.ContactEvents), "Q next contacts");
		TestAssert.Equal(ProbeViewMode.Outcome, ProbeViewCycle.Next(ProbeViewMode.TransportEffort), "Q wraps outcome");
		TestAssert.Equal(ProbeViewMode.Outcome, ProbeViewCycle.Previous(ProbeViewMode.ContactEvents), "Shift+Q contacts");
		TestAssert.Equal(ProbeViewMode.ContactEvents, ProbeViewCycle.Previous(ProbeViewMode.TransportEffort), "Shift+Q effort");
		TestAssert.Equal(ProbeViewMode.TransportEffort, ProbeViewCycle.Previous(ProbeViewMode.Outcome), "Shift+Q reverse");

		ProbeViewColor[] outcomeColors = Enum.GetValues<ProbeOutcomeCode>()
			.Select(ProbeViewMapper.MapOutcome).ToArray();
		TestAssert.Equal(7, outcomeColors.Length, "all outcome categories mapped");
		TestAssert.Equal(7, outcomeColors.Distinct().Count(), "outcome colors distinct");

		TestAssert.False(ProbeViewMapper.MapContactEvents(0).Equals(ProbeViewMapper.MapContactEvents(1)), "zero contacts distinct");
		TestAssert.False(ProbeViewMapper.MapContactEvents(1).Equals(ProbeViewMapper.MapContactEvents(2)), "one contact distinct");
		TestAssert.False(ProbeViewMapper.MapContactEvents(2).Equals(ProbeViewMapper.MapContactEvents(3)), "two contacts distinct");
		TestAssert.Equal(ProbeViewMapper.MapContactEvents(3), ProbeViewMapper.MapContactEvents(4), "3+ bucket");
		TestAssert.Equal(ProbeViewMapper.MapContactEvents(3), ProbeViewMapper.MapContactEvents(99), "all 3+ values consistent");
		TestAssert.True(ProbeViewMapper.IsAvailable(true, 0, 4, 4), "complete snapshot available");
		TestAssert.False(ProbeViewMapper.IsAvailable(true, 0, 4, 3), "short source rejected");
		TestAssert.False(ProbeViewMapper.IsAvailable(false, 0, 4, 4), "incomplete snapshot rejected");
		TestAssert.False(ProbeViewMapper.IsAvailable(true, 1, 4, 4), "unprocessed snapshot rejected");
		TestAssert.True(ProbeViewMapper.IsSealedStorageValid(true, 2, 2, 4, 4, 4, 4, 4, 4), "exact sealed dimensions accepted");
		TestAssert.False(ProbeViewMapper.IsSealedStorageValid(false, 2, 2, 4, 4, 4, 4, 4, 4), "unavailable sealed source rejected");
		TestAssert.False(ProbeViewMapper.IsSealedStorageValid(true, 2, 2, 4, 3, 4, 4, 4, 4), "sealed dimension mismatch rejected");
		TestAssert.False(ProbeViewMapper.IsSealedStorageValid(true, 2, 2, 4, 4, 4, 4, 4, 3), "sealed effort source mismatch rejected");

		ProbeViewColor effortZero = ProbeViewMapper.MapTransportEffort(0, 81, true);
		ProbeViewColor effortPartial = ProbeViewMapper.MapTransportEffort(40, 81, true);
		ProbeViewColor effortFull = ProbeViewMapper.MapTransportEffort(81, 81, true);
		TestAssert.False(effortZero.Equals(effortPartial), "effort zero differs from partial");
		TestAssert.False(effortPartial.Equals(effortFull), "effort partial differs from full");
		TestAssert.Equal(effortFull, ProbeViewMapper.MapTransportEffort(162, 81, true), "effort clamps to one");
		TestAssert.False(ProbeViewMapper.MapTransportEffort(40, 81, false).Equals(effortZero), "NumericalFailure unavailable");

		ProbeOutcomeCode[] outcomes = { ProbeOutcomeCode.MaxStepsExhausted, ProbeOutcomeCode.HitGeometry };
		int[] contacts = { 0, 3 };
		ProbeOutcomeCode[] outcomesBefore = outcomes.ToArray();
		int[] contactsBefore = contacts.ToArray();
		int generation = 17;
		ProbeViewMode mode = ProbeViewMode.Outcome;
		mode = ProbeViewCycle.Next(mode);
		_ = ProbeViewMapper.Map(mode, outcomes[0], contacts[0], 81, 81, true);
		mode = ProbeViewCycle.Next(mode);
		_ = ProbeViewMapper.Map(mode, outcomes[0], contacts[0], 81, 81, true);
		TestAssert.Equal(17, generation, "mapping preserves generation");
		TestAssert.True(outcomes.SequenceEqual(outcomesBefore), "mapping does not mutate outcomes");
		TestAssert.True(contacts.SequenceEqual(contactsBefore), "mapping does not mutate contacts");
		TestAssert.True(ProbeViewMapper.IsAvailable(true, 0, 4, 4), "formally complete snapshot remains view authority");

		TestAssert.Equal("Outcome", ProbeViewMapper.DisplayName(ProbeViewMode.Outcome), "outcome public name");
		TestAssert.Equal("Contact Events", ProbeViewMapper.DisplayName(ProbeViewMode.ContactEvents), "contact public name");
		TestAssert.Equal("Transport Effort", ProbeViewMapper.DisplayName(ProbeViewMode.TransportEffort), "effort public name");
	}
}
