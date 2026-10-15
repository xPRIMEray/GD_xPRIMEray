using Godot;
using System.Collections.Generic;

/// <summary>
/// Test-only Variant adapter for the immutable C# LIVE importance snapshot.
/// It exposes decisions already recorded by the production scheduler without
/// reconstructing or mutating scheduler state.
/// </summary>
public partial class LiveImportanceScheduleProbe : RefCounted
{
	public Godot.Collections.Dictionary Capture(GrinFilmCamera film)
	{
		var result = new Godot.Collections.Dictionary();
		if (film == null)
			return result;

		GrinFilmCamera.LiveImportanceScheduleTestingSnapshot snapshot =
			film.GetLiveImportanceScheduleForTesting();
		film.TryGetLiveMeanderStateForTesting(out GrinFilmCamera.LiveMeanderStateSnapshot meander);
		var gardenIds = new HashSet<ulong>();
		int completedCount = 0;
		int frontierEligibleCount = 0;
		int assignedFrontierCount = 0;
		foreach (GrinFilmCamera.LiveImportancePreparedBandRecord band in snapshot.PreparedBands)
		{
			if (band.FrontierEligible)
				frontierEligibleCount++;
			if (band.SelectionPath == GrinFilmCamera.LiveImportanceBandSelectionPath.PrioritizedFrontier
				&& band.DominantEntity != 0UL)
			{
				gardenIds.Add(band.DominantEntity);
				assignedFrontierCount++;
			}
		}
		foreach (GrinFilmCamera.LiveImportanceExecutedBandRecord band in snapshot.ExecutedBands)
		{
			if (band.Completed)
				completedCount++;
		}

		result["state"] = snapshot.State.ToString();
		result["context_generation"] = (long)snapshot.ContextGeneration;
		result["live_pass_id"] = (long)snapshot.LivePassId;
		result["schedule_id"] = (long)snapshot.ScheduleId;
		result["rotation_before"] = snapshot.RotationOffsetBefore;
		result["rotation_after"] = snapshot.RotationOffsetAfter;
		result["sequential_fallback"] = snapshot.SequentialFallbackActivated;
		result["terminal_reason"] = snapshot.TerminalReason;
		result["prepared_count"] = snapshot.PreparedBands.Count;
		result["executed_count"] = snapshot.ExecutedBands.Count;
		result["completed_count"] = completedCount;
		result["frontier_eligible_count"] = frontierEligibleCount;
		result["assigned_frontier_count"] = assignedFrontierCount;
		result["prioritized_garden_count"] = gardenIds.Count;
		result["resolved_band_height"] = film.BandHeightRowsResolved;
		result["schedule_active"] = meander.ScheduleActive;
		result["schedule_total_bands"] = meander.ScheduleTotalBands;
		result["schedule_cursor"] = meander.ScheduleCursor;
		result["schedule_completed_bands"] = meander.ScheduleCompletedBands;
		return result;
	}
}
