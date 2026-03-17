using Godot;

public partial class RayEmitter3D : Node3D
{
    [Export] public Color RayColor = new Color(1f, 0.2f, 1f); // magenta default
    [Export] public int Rays = 32;
    [Export] public float SpreadDegrees = 45f;   // cone angle
    [Export] public float MaxDistance = 25f;
    [Export] public float Intensity = 1.0f;

    // 🔥 Debug mode: a clean “sheet” of rays (no randomness)
    [Export] public bool UseFan = true;
    [Export] public float FanYawDegrees = 60f;   // left-right spread
    [Export] public float FanPitchDegrees = 0f;  // keep 0 for a flat fan at first

    public override void _Ready() => AddToGroup("ray_emitters");
}
