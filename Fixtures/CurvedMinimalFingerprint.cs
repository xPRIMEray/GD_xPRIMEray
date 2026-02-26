using Godot;

public partial class CurvedMinimalFingerprint : Node3D
{
	private static readonly NodePath FieldPath = new("FixtureCurvedMinimal/FieldSource3D");
	private static readonly NodePath CameraPath = new("FixtureCurvedMinimal/Camera3D");
	private static readonly NodePath SpherePath = new("FixtureCurvedMinimal/fixture_target");

	private const float FixedStrength = 1.15f;
	private const float FixedSoftening = 0.1f;
	private const float FixedOuterRadius = 4.5f;
	private const bool FixedOverrideGamma = true;
	private const float FixedGamma = 2.0f;
	private const bool FixedDebugDrawBounds = false;
	private const bool FixedDebugDrawInGame = false;

	public override void _Ready()
	{
		FieldSource3D field = GetNodeOrNull<FieldSource3D>(FieldPath);
		if (field == null)
		{
			GD.PushWarning($"CurvedMinimal fingerprint: missing FieldSource3D at {FieldPath}");
			return;
		}

		// Re-apply fixture values to ensure deterministic test metadata without changing behavior.
		field.Strength = FixedStrength;
		field.Softening = FixedSoftening;
		field.OuterRadius = FixedOuterRadius;
		field.OverrideGamma = FixedOverrideGamma;
		field.Gamma = FixedGamma;
		field.DebugDrawBounds = FixedDebugDrawBounds;
		field.DebugDrawInGame = FixedDebugDrawInGame;

		Camera3D camera = GetNodeOrNull<Camera3D>(CameraPath);
		Node3D sphere = GetNodeOrNull<Node3D>(SpherePath);

		string sphereScale = sphere != null ? FormatVec3(sphere.Scale) : "n/a";
		string spherePos = sphere != null ? FormatVec3(sphere.GlobalTransform.Origin) : "n/a";
		string camFov = camera != null ? $"{camera.Fov:0.###}" : "n/a";
		string camPos = camera != null ? FormatVec3(camera.GlobalTransform.Origin) : "n/a";
		string camToSphere = (camera != null && sphere != null)
			? $"{camera.GlobalTransform.Origin.DistanceTo(sphere.GlobalTransform.Origin):0.###}"
			: "n/a";

		GD.Print(
			$"[CurvedFixture] strength={field.Strength:0.###} radius={field.OuterRadius:0.###} " +
			$"node_path={field.GetPath()} sphere_global_pos={spherePos} sphere_scale={sphereScale} " +
			$"cam_fov={camFov} camera_global_pos={camPos} cam_to_sphere={camToSphere}");
	}

	private static string FormatVec3(Vector3 v)
	{
		return $"({v.X:0.###},{v.Y:0.###},{v.Z:0.###})";
	}
}
