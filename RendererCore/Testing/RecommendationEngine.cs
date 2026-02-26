using System;

public static class RecommendationEngine
{
	public static CalibratedPreset BuildPreset(SceneProbeReport probe, SceneProbeArchetype archetype, ulong canonicalSignatureHash64, bool? baselinePruneEnabled = null)
	{
		CalibratedPreset preset = CalibratedPreset.NoOp(canonicalSignatureHash64);

		switch (archetype)
		{
			case SceneProbeArchetype.Tiny:
				preset.enable_tlas_prune = false;
				preset.target_ms_per_frame = 2;
				preset.notes = "tiny:prune_off";
				return preset;

			case SceneProbeArchetype.Default:
				preset.notes = "default:no_change";
				return preset;

			case SceneProbeArchetype.DenseGeometry:
				preset.enable_tlas_prune = false;
				preset.target_ms_per_frame = CalibrationTuning.DenseGeometrySamplingMs;
				preset.notes = "dense:prune_off";
				return preset;

			case SceneProbeArchetype.FieldHeavy:
				if (IsWeakFieldHeavySignal(probe))
				{
					bool baselinePruneOff = baselinePruneEnabled.HasValue && !baselinePruneEnabled.Value;
					preset.target_ms_per_frame = baselinePruneOff ? 20 : 10;
					preset.notes = "fieldheavy:weak_signal:adaptive_target_by_baseline_prune:no_prune_delta";
					return preset;
				}

				preset.enable_tlas_prune = true;
				preset.target_ms_per_frame = CalibrationTuning.FieldHeavySamplingMs;
				preset.notes = "fieldheavy:prune_on";
				return preset;

			default:
				preset.notes = "unknown:no_change";
				return preset;
		}
	}

	private static bool IsWeakFieldHeavySignal(SceneProbeReport probe)
	{
		if (probe == null)
		{
			return false;
		}

		int fields = Math.Max(0, probe.scene_field_source_count);
		int grin = Math.Max(0, probe.scene_grin_volume_count);
		int curvature = Math.Max(0, probe.scene_curvature_field_count);
		return fields <= 1 && grin == 0 && curvature == 0;
	}
}
