using Godot;
using System.IO;

/// <summary>
/// Coordinates the first linked-mouth prototype:
/// - keeps only one world visible to the player camera
/// - drives both linked portal cameras
/// - teleports the traveler when crossing the active spherical shell
/// </summary>
public partial class WormholePrototypeRig : Node3D
{
	[ExportGroup("References")]
	[Export] public NodePath TravelerPath;
	[Export] public NodePath MainCameraPath;
	[Export] public NodePath PortalAPath;
	[Export] public NodePath PortalBPath;
	[Export] public NodePath RayBeamRendererPath;

	[ExportGroup("Runtime")]
	[Export] public bool StartInSceneA = true;
	[Export(PropertyHint.Range, "0,1,0.01")] public float TeleportCooldownSeconds = 0.2f;

	[ExportGroup("Auto Motion")]
	[Export] public bool EnableAutoMotion = false;
	[Export(PropertyHint.Range, "0.5,64,0.1")] public float AutoMotionOrbitRadius = 8.0f;
	[Export(PropertyHint.Range, "-8,8,0.1")] public float AutoMotionHeight = 1.2f;
	[Export(PropertyHint.Range, "2,120,0.1")] public float AutoMotionPeriodSeconds = 28.0f;
	[Export(PropertyHint.Range, "-180,180,1")] public float AutoMotionStartAngleDegrees = 180.0f;
	[Export] public bool AutoMotionLookAtActivePortal = true;

	[ExportGroup("Validation Capture")]
	[Export] public bool CaptureValidationScreenshot = true;
	[Export(PropertyHint.Range, "0.1,30,0.1")] public float ValidationCaptureDelaySeconds = 3.5f;
	[Export] public string ValidationCapturePath = "res://output/wormhole_test/wormhole_validation_capture.png";
	[Export] public bool EmitBoundaryValidationSummaryAfterCapture = true;
	[Export] public string BoundaryValidationLabel = "wormhole_validation";

	private Node3D _traveler;
	private Camera3D _mainCamera;
	private WormholePortal _portalA;
	private WormholePortal _portalB;
	private RayBeamRenderer _rayBeamRenderer;
	private WormholePortal _activePortal;
	private float _lastActivePortalDelta;
	private double _teleportCooldownRemaining;
	private double _autoMotionElapsedSeconds;
	private double _validationCaptureElapsedSeconds;
	private bool _validationCaptureCompleted;

	public override void _Ready()
	{
		_traveler = GetNodeOrNull<Node3D>(TravelerPath);
		_mainCamera = GetNodeOrNull<Camera3D>(MainCameraPath);
		_portalA = GetNodeOrNull<WormholePortal>(PortalAPath);
		_portalB = GetNodeOrNull<WormholePortal>(PortalBPath);
		_rayBeamRenderer = GetNodeOrNull<RayBeamRenderer>(RayBeamRendererPath);

		if (_traveler == null || _mainCamera == null || _portalA == null || _portalB == null)
		{
			GD.PushError($"{Name}: wormhole prototype rig is missing traveler, camera, or portal references.");
			SetProcess(false);
			SetPhysicsProcess(false);
			return;
		}

		SetProcess(true);
		SetPhysicsProcess(true);

		_activePortal = StartInSceneA ? _portalA : _portalB;
		ApplyWorldVisibility(_activePortal);
		_lastActivePortalDelta = _activePortal.SignedRadiusDelta(_traveler.GlobalPosition);
		_rayBeamRenderer?.BeginBoundaryValidationRun();
		RunValidationCaptureSequence();
	}

	public override void _Process(double delta)
	{
		if (EnableAutoMotion)
		{
			UpdateAutoMotion(delta);
		}

		_portalA?.UpdatePortalView(_mainCamera);
		_portalB?.UpdatePortalView(_mainCamera);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_traveler == null || _activePortal == null)
		{
			return;
		}

		if (_teleportCooldownRemaining > 0.0)
		{
			_teleportCooldownRemaining = Mathf.Max(0.0f, (float)(_teleportCooldownRemaining - delta));
			_lastActivePortalDelta = _activePortal.SignedRadiusDelta(_traveler.GlobalPosition);
			return;
		}

		float currentDelta = _activePortal.SignedRadiusDelta(_traveler.GlobalPosition);
		bool crossedIntoShell = _lastActivePortalDelta > 0f && currentDelta <= 0f;

		if (crossedIntoShell)
		{
			TeleportThrough(_activePortal);
			_teleportCooldownRemaining = TeleportCooldownSeconds;
			_lastActivePortalDelta = _activePortal.SignedRadiusDelta(_traveler.GlobalPosition);
			return;
		}

		_lastActivePortalDelta = currentDelta;
	}

	private void TeleportThrough(WormholePortal sourcePortal)
	{
		WormholePortal destinationPortal = sourcePortal.GetLinkedPortal();
		if (destinationPortal == null)
		{
			return;
		}

		_traveler.GlobalTransform = sourcePortal.BuildExitTransform(_traveler.GlobalTransform);
		_activePortal = destinationPortal;
		ApplyWorldVisibility(destinationPortal);
	}

	private void ApplyWorldVisibility(WormholePortal activePortal)
	{
		if (_mainCamera == null || activePortal == null)
		{
			return;
		}

		_mainCamera.CullMask = activePortal.GeometryMask | activePortal.PortalMask;
	}

	private void UpdateAutoMotion(double delta)
	{
		if (_traveler == null || _activePortal == null)
		{
			return;
		}

		_autoMotionElapsedSeconds += delta;
		float orbitRadius = Mathf.Max(0.5f, AutoMotionOrbitRadius);
		float orbitPeriod = Mathf.Max(0.1f, AutoMotionPeriodSeconds);
		float angle = Mathf.DegToRad(AutoMotionStartAngleDegrees)
			+ (Mathf.Tau * (float)(_autoMotionElapsedSeconds / orbitPeriod));
		Vector3 center = _activePortal.GlobalPosition;
		Vector3 orbitPos = center
			+ new Vector3(Mathf.Cos(angle) * orbitRadius, AutoMotionHeight, Mathf.Sin(angle) * orbitRadius);

		_traveler.GlobalPosition = orbitPos;
		if (AutoMotionLookAtActivePortal)
		{
			_traveler.LookAt(center, Vector3.Up);
		}
	}

	private async void RunValidationCaptureSequence()
	{
		if (_validationCaptureCompleted)
		{
			return;
		}

		double delay = Mathf.Max(0.1f, ValidationCaptureDelaySeconds);
		await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInsideTree() || _validationCaptureCompleted)
		{
			return;
		}

		_validationCaptureElapsedSeconds = delay;
		GD.Print($"[WormholeValidation] trigger elapsed={_validationCaptureElapsedSeconds:0.00}s");

		if (CaptureValidationScreenshot)
		{
			CaptureViewportScreenshot();
		}

		if (EmitBoundaryValidationSummaryAfterCapture)
		{
			_rayBeamRenderer?.EmitBoundaryValidationSummary(BoundaryValidationLabel);
		}

		_validationCaptureCompleted = true;
	}

	private void CaptureViewportScreenshot()
	{
		Viewport viewport = GetViewport();
		if (viewport == null)
		{
			return;
		}

		Image image = viewport.GetTexture()?.GetImage();
		if (image == null)
		{
			return;
		}

		string projectPath = string.IsNullOrWhiteSpace(ValidationCapturePath)
			? "res://output/wormhole_test/wormhole_validation_capture.png"
			: ValidationCapturePath.Trim();
		string absolutePath = ProjectSettings.GlobalizePath(projectPath);
		string directory = Path.GetDirectoryName(absolutePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			DirAccess.MakeDirRecursiveAbsolute(directory);
		}

		Error saveError = image.SavePng(absolutePath);
		if (saveError == Error.Ok)
		{
			GD.Print($"[WormholeValidation] capture_saved path={absolutePath}");
		}
		else
		{
			GD.PushWarning($"[WormholeValidation] failed to save capture path={absolutePath} error={saveError}");
		}
	}
}
