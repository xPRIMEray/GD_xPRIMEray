using Godot;

namespace RendererCore.Common;

/// <summary>
/// Serializes the project's direct-space physics queries (B7).
///
/// Godot 4.5's <c>GodotPhysicsDirectSpaceState3D</c> is not reentrant: ray, shape and motion queries write
/// broadphase candidates into per-space shared buffers and read them back. Concurrent queries on one space can
/// therefore return another query's collider or shape index, or abort with
/// <c>Index p_index = N is out of bounds (shapes.size() = 1)</c>. LIVE Pass 1 and threaded Pass-2 query resolve
/// call these queries from <c>Parallel.For</c> workers, so every project query goes through this gate.
///
/// Only the engine call is held. Each call returns a newly allocated result that the caller owns after the
/// call returns. Nothing else is acquired while the gate is held, and the engine does not call back into
/// managed code during these queries, so the gate cannot take part in a lock cycle. One process-wide gate is
/// used because different <see cref="PhysicsDirectSpaceState3D"/> wrappers can refer to the same native space.
/// </summary>
public static class PhysicsQueryGate
{
	private static readonly object Gate = new();

	public static Godot.Collections.Dictionary IntersectRay(
		PhysicsDirectSpaceState3D space,
		PhysicsRayQueryParameters3D query)
	{
		lock (Gate)
			return space.IntersectRay(query);
	}

	public static Godot.Collections.Array<Godot.Collections.Dictionary> IntersectShape(
		PhysicsDirectSpaceState3D space,
		PhysicsShapeQueryParameters3D query,
		int maxResults)
	{
		lock (Gate)
			return space.IntersectShape(query, maxResults);
	}

	public static float[] CastMotion(
		PhysicsDirectSpaceState3D space,
		PhysicsShapeQueryParameters3D query)
	{
		lock (Gate)
			return space.CastMotion(query);
	}
}
