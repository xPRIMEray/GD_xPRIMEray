using Godot;

public partial class FreeFlyCamera : Camera3D
{
    [Export] public float MoveSpeed = 5.0f;
    [Export] public float SprintMultiplier = 3.0f;
    [Export] public float MouseSensitivity = 0.0025f;
    [Export] public float PitchMinDegrees = -89.0f;
    [Export] public float PitchMaxDegrees = 89.0f;
    [Export] public bool CaptureMouseOnStart = true;
    [Export] public bool InputEnabled = true;

    private float _yaw;
    private float _pitch;

    public override void _Ready()
    {
        SetProcessInput(InputEnabled);
        SetProcessUnhandledInput(InputEnabled);
        SetPhysicsProcess(InputEnabled);

        Vector3 rot = Rotation;
        _pitch = rot.X;
        _yaw = rot.Y;

        if (InputEnabled && CaptureMouseOnStart)
            Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public void SetInputEnabled(bool enabled, bool releaseMouse = true)
    {
        InputEnabled = enabled;
        SetProcessInput(enabled);
        SetProcessUnhandledInput(enabled);
        SetPhysicsProcess(enabled);

        if (!enabled && releaseMouse && Input.MouseMode == Input.MouseModeEnum.Captured)
            Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!InputEnabled)
            return;

        if (@event is InputEventMouseMotion motion &&
            Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            _pitch -= motion.Relative.Y * MouseSensitivity;

            float minPitch = Mathf.DegToRad(PitchMinDegrees);
            float maxPitch = Mathf.DegToRad(PitchMaxDegrees);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Rotation = new Vector3(_pitch, _yaw, 0.0f);
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
                Input.MouseMode = Input.MouseModeEnum.Visible;
            else
                Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!InputEnabled)
            return;

        Vector3 direction = Vector3.Zero;

        if (Input.IsActionPressed("move_forward"))
            direction -= GlobalTransform.Basis.Z;

        if (Input.IsActionPressed("move_backward"))
            direction += GlobalTransform.Basis.Z;

        if (Input.IsActionPressed("move_left"))
            direction -= GlobalTransform.Basis.X;

        if (Input.IsActionPressed("move_right"))
            direction += GlobalTransform.Basis.X;

        if (Input.IsActionPressed("move_up"))
            direction += GlobalTransform.Basis.Y;

        if (Input.IsActionPressed("move_down"))
            direction -= GlobalTransform.Basis.Y;

        if (direction != Vector3.Zero)
        {
            direction = direction.Normalized();

            float speed = MoveSpeed;
            if (Input.IsActionPressed("move_sprint"))
                speed *= SprintMultiplier;

            GlobalPosition += direction * speed * (float)delta;
        }
    }
}
