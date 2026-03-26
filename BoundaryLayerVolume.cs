using Godot;

/// <summary>
/// Authoring node for a volumetric region that modifies ray behavior during integration.
/// Conceptually distinct from FieldSource3D (continuous curvature sources) and from
/// geometry collision (discrete surface hits).
///
/// Two execution modes are supported:
///   Continuous   — behavior applied every integration step the ray is inside the volume.
///   CrossingEvent — behavior applied once when the ray enters or exits the volume boundary.
///                   Entry/exit detection uses a per-ray uint bitmask (ComputeInsideMask) in
///                   each integration loop. Entry events dispatch; exit is a future TODO.
///
/// Add nodes to the Godot group "boundary_layer_volumes" to register them with the renderer.
/// The renderer snapshots this group each frame; no direct scene-tree access occurs in the hot loop.
/// </summary>
[Tool]
public partial class BoundaryLayerVolume : Node3D
{
	/// <summary>Shape of the support region.</summary>
	public enum BoundaryShapeType
	{
		Sphere = 0,
		Box    = 1
	}

	/// <summary>
	/// Controls when the behavior is evaluated relative to the ray's path through the volume.
	/// </summary>
	public enum BoundaryExecutionMode
	{
		/// <summary>
		/// Applied every integration step the ray is inside the volume.
		/// Suitable for continuous medium effects (steering bias, index gradients, damping).
		/// </summary>
		Continuous    = 0,

		/// <summary>
		/// Applied once when the ray crosses the volume boundary (entry or exit).
		/// Suitable for interface effects: refraction, reflection, spectral split.
		/// Entry events are dispatched; exit events are a future TODO.
		/// </summary>
		CrossingEvent = 1
	}

	/// <summary>
	/// Behavior applied to rays that interact with this volume.
	/// The execution timing (continuous vs. crossing) is set by ExecutionMode.
	/// </summary>
	public enum BoundaryBehavior
	{
		/// <summary>
		/// Adds a constant directional bias to the ray direction and re-normalizes.
		/// Suitable for simple steering, draft caustic tests, and anisotropic regions.
		/// Effect magnitude is proportional to BiasStrength; keep small for stability.
		/// Works with both Continuous (per-step accumulation) and CrossingEvent (once on entry).
		/// </summary>
		DirectionBias = 0
	}

	[ExportGroup("Boundary Layer Volume")]
	[Export] public bool Enabled = true;
	[Export] public BoundaryShapeType ShapeType = BoundaryShapeType.Sphere;

	[ExportSubgroup("Shape")]
	/// <summary>Radius of the sphere support region (ShapeType = Sphere).</summary>
	[Export] public float Radius = 1.0f;
	/// <summary>Half-extents of the box support region in local space (ShapeType = Box).</summary>
	[Export] public Vector3 BoxExtents = new Vector3(1f, 1f, 1f);

	[ExportSubgroup("Behavior")]
	/// <summary>
	/// Whether the behavior fires continuously while inside the volume (every step),
	/// or once at the entry crossing. Entry events dispatch; exit is a future TODO.
	/// </summary>
	[Export] public BoundaryExecutionMode ExecutionMode = BoundaryExecutionMode.Continuous;
	/// <summary>Which behavior to apply when the execution condition is met.</summary>
	[Export] public BoundaryBehavior Behavior = BoundaryBehavior.DirectionBias;

	[ExportSubgroup("DirectionBias")]
	/// <summary>
	/// Bias direction in local node space. Transformed to world space at snapshot time.
	/// Typical use: Vector3.Up bends rays upward; Vector3.Forward biases forward.
	/// </summary>
	[Export] public Vector3 BiasDirection = Vector3.Up;
	/// <summary>
	/// Strength of the direction bias (0 = no effect, 1 = full replacement).
	/// Keep below 0.1 for subtle effects; larger values cause sharp bends.
	/// </summary>
	[Export(PropertyHint.Range, "0,1,0.001")] public float BiasStrength = 0.02f;

	[ExportSubgroup("Debug")]
	/// <summary>
	/// When true, logs a message to the Godot output each time a CrossingEvent is dispatched
	/// for this layer. Off by default; only set during authoring/investigation.
	/// Has no effect on Continuous layers.
	/// </summary>
	[Export] public bool DebugLogCrossings = false;

	public override void _EnterTree()
	{
		AddToGroup("boundary_layer_volumes");
	}

	public override void _ExitTree()
	{
		RemoveFromGroup("boundary_layer_volumes");
	}
}
