using System;

public struct CalibratedPreset
{
	public const int preset_version = 1;

	public SceneProbeArchetype? recommended_archetype;

	// trust
	public int? min_geom_pixels;
	public long? min_ray_tests;
	public int? pass2_stride;
	public float? gate_confidence_min;

	// prune
	public bool? enable_tlas_prune;
	public int? prune_budget;
	public float? prune_expand;

	// cadence
	public int? target_ms_per_frame;
	public int? render_stride;
	public int? rows_per_frame;
	public float? resolution_scale;

	// misc
	public string notes;
	public ulong hash64;

	public static CalibratedPreset NoOp(ulong sceneSignatureHash64 = 0UL)
	{
		return new CalibratedPreset
		{
			notes = string.Empty,
			hash64 = DeriveHash64(sceneSignatureHash64)
		};
	}

	public bool IsNoOp =>
		!recommended_archetype.HasValue &&
		!min_geom_pixels.HasValue &&
		!min_ray_tests.HasValue &&
		!pass2_stride.HasValue &&
		!gate_confidence_min.HasValue &&
		!enable_tlas_prune.HasValue &&
		!prune_budget.HasValue &&
		!prune_expand.HasValue &&
		!target_ms_per_frame.HasValue &&
		!render_stride.HasValue &&
		!rows_per_frame.HasValue &&
		!resolution_scale.HasValue;

	// Versioned preset hash derived from the canonical calibration scene signature hash.
	public static ulong DeriveHash64(ulong sceneSignatureHash64)
	{
		unchecked
		{
			ulong h = sceneSignatureHash64 ^ 14695981039346656037UL;
			h ^= (ulong)preset_version;
			h *= 1099511628211UL;
			return h;
		}
	}

	public override string ToString() =>
		$"CalibratedPreset/v{preset_version} hash=0x{hash64:x16} arch={(recommended_archetype.HasValue ? recommended_archetype.Value.ToString() : "-")} trust[gpx={Fmt(min_geom_pixels)},rays={Fmt(min_ray_tests)},p2={Fmt(pass2_stride)},gate={Fmt(gate_confidence_min)}] prune[on={Fmt(enable_tlas_prune)},budget={Fmt(prune_budget)},expand={Fmt(prune_expand)}] cadence[ms={Fmt(target_ms_per_frame)},stride={Fmt(render_stride)},rows={Fmt(rows_per_frame)},scale={Fmt(resolution_scale)}] notes={(string.IsNullOrWhiteSpace(notes) ? "-" : notes)}";

	private static string Fmt<T>(T? value) where T : struct
		=> value.HasValue ? value.Value.ToString() ?? "-" : "-";
}
