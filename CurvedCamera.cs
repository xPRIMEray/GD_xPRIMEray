using Godot;

[GlobalClass]
public partial class CurvedCamera : Camera3D
{
	[Export] public float Beta = 0.0f;
	[Export] public float Gamma = 2.0f;

    // --- movement controls ---
    [Export] public float MoveSpeed = 6.0f;        // units / sec
    [Export] public float BoostMultiplier = 3.0f;  // hold Shift
    [Export] public bool MoveInLocalSpace = true;  // move relative to camera facing



    public override void _Process(double delta)
    {
		//GD.Print($"Beta={Beta}, Gamma={Gamma}");
        float dt = (float)delta;

        // Arrow keys: forward/back/strafe
        float x = 0f;
        float z = 0f;
        if (Input.IsKeyPressed(Key.Left))  x -= 1f;
        if (Input.IsKeyPressed(Key.Right)) x += 1f;
        if (Input.IsKeyPressed(Key.Up))    z -= 1f; // forward (Godot forward is -Z)
        if (Input.IsKeyPressed(Key.Down))  z += 1f;

        // PageUp/PageDown: vertical (Y) by default
        float y = 0f;
        if (Input.IsKeyPressed(Key.Pageup))   y += 1f;
        if (Input.IsKeyPressed(Key.Pagedown)) y -= 1f;

        Vector3 input = new Vector3(x, y, z);
        if (input == Vector3.Zero) return;

        input = input.Normalized();

        float speed = MoveSpeed;
        if (Input.IsKeyPressed(Key.Shift))
            speed *= BoostMultiplier;

        Vector3 deltaMove = input * speed * dt;

        if (MoveInLocalSpace)
        {
            // Move relative to camera orientation (basis vectors are local axes in world space)
            GlobalPosition += GlobalBasis * deltaMove;
        }
        else
        {
            // Move in world axes
            GlobalPosition += deltaMove;
        }
    }

	public Vector3 GetCurvedRay(Vector2 ndc)
	{
		Vector3 ray = ProjectRayNormal(ndc);
		float r = ray.Length();
		float k = Mathf.Pow(r, Gamma) * Beta;
		return (ray + ray.Normalized() * k).Normalized();
	}

}
