// Probe-time scene classification used by test auto-calibration. This is distinct
// from RendererCore.Calibration.SceneSignature (the canonical calibration v1 struct).
public enum SceneProbeArchetype
{
	Unknown = 0,
	Tiny = 1,
	Default = 2,
	DenseGeometry = 3,
	FieldHeavy = 4
}

public sealed class SceneProbeReport
{
	public int scene_node_count;
	public int scene_mesh_count;
	public int scene_surface_count_estimate;
	public long scene_triangle_count_estimate = -1;
	public bool scene_triangle_count_known;
	public int scene_children_skipped;
	public int scene_field_source_count;
	public int scene_grin_volume_count;
	public int scene_curvature_field_count;
	public ProbeEarlyExitReason probe_early_exit_reason = ProbeEarlyExitReason.None;
	public double hitRate;
	public double geomSegZeroRate;
	public long geomPixProcessedRaw;
	public long geomRayTestsTotalRaw;
	public double perPxOff;
	public double perPxOn;
	public double savedPct;
	public double p2Rate;
	public int firstTrustedStep;
	public SceneProbeArchetype archetype = SceneProbeArchetype.Unknown;

	public ulong ComputeHash64()
	{
		unchecked
		{
			const ulong offset = 14695981039346656037UL;
			const ulong prime = 1099511628211UL;
			ulong h = offset;

			HashInt32(ref h, scene_node_count, prime);
			HashInt32(ref h, scene_mesh_count, prime);
			HashInt32(ref h, scene_surface_count_estimate, prime);
			HashInt64(ref h, scene_triangle_count_estimate, prime);
			HashBool(ref h, scene_triangle_count_known, prime);
			HashInt32(ref h, scene_children_skipped, prime);
			HashInt32(ref h, scene_field_source_count, prime);
			HashInt32(ref h, scene_grin_volume_count, prime);
			HashInt32(ref h, scene_curvature_field_count, prime);
			HashInt32(ref h, (int)probe_early_exit_reason, prime);
			return h;
		}
	}

	private static void HashBool(ref ulong h, bool value, ulong prime)
		=> HashByte(ref h, value ? (byte)1 : (byte)0, prime);

	private static void HashInt32(ref ulong h, int value, ulong prime)
	{
		uint v = unchecked((uint)value);
		HashByte(ref h, (byte)(v & 0xFF), prime);
		HashByte(ref h, (byte)((v >> 8) & 0xFF), prime);
		HashByte(ref h, (byte)((v >> 16) & 0xFF), prime);
		HashByte(ref h, (byte)((v >> 24) & 0xFF), prime);
	}

	private static void HashInt64(ref ulong h, long value, ulong prime)
	{
		ulong v = unchecked((ulong)value);
		for (int i = 0; i < 8; i++)
		{
			HashByte(ref h, (byte)((v >> (8 * i)) & 0xFF), prime);
		}
	}

	private static void HashByte(ref ulong h, byte value, ulong prime)
	{
		unchecked
		{
			h ^= value;
			h *= prime;
		}
	}
}
