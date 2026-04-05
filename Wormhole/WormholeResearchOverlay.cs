using Godot;
using System;

public partial class WormholeResearchOverlay : Node3D
{
	public enum ResearchViewMode
	{
		TopDown = 0,
		Oblique = 1
	}

	public readonly struct ResearchOverlaySnapshot
	{
		public readonly bool Valid;
		public readonly ResearchViewMode ViewMode;
		public readonly bool ShowFieldShells;
		public readonly bool ShowProbeGeometry;
		public readonly bool ShowBackdrops;
		public readonly Vector3 PortalAPosition;
		public readonly Vector3 PortalBPosition;
		public readonly Vector3 ProbeWallPosition;
		public readonly Vector3 BackdropAPosition;
		public readonly Vector3 BackdropBPosition;
		public readonly Vector3 CameraPosition;
		public readonly Vector3 CameraForward;

		public ResearchOverlaySnapshot(
			bool valid,
			ResearchViewMode viewMode,
			bool showFieldShells,
			bool showProbeGeometry,
			bool showBackdrops,
			Vector3 portalAPosition,
			Vector3 portalBPosition,
			Vector3 probeWallPosition,
			Vector3 backdropAPosition,
			Vector3 backdropBPosition,
			Vector3 cameraPosition,
			Vector3 cameraForward)
		{
			Valid = valid;
			ViewMode = viewMode;
			ShowFieldShells = showFieldShells;
			ShowProbeGeometry = showProbeGeometry;
			ShowBackdrops = showBackdrops;
			PortalAPosition = portalAPosition;
			PortalBPosition = portalBPosition;
			ProbeWallPosition = probeWallPosition;
			BackdropAPosition = backdropAPosition;
			BackdropBPosition = backdropBPosition;
			CameraPosition = cameraPosition;
			CameraForward = cameraForward;
		}
	}

	[ExportGroup("References")]
	[Export] public NodePath MainCameraPath = new("../PlayerCamera");
	[Export] public NodePath PortalAPath = new("../SceneA/PortalA");
	[Export] public NodePath PortalBPath = new("../SceneB/PortalB");
	[Export] public NodePath ProbeWallPath = new("../SceneB/ProbeWallB");
	[Export] public NodePath BackdropAPath = new("../SceneA/BackdropA");
	[Export] public NodePath BackdropBPath = new("../SceneB/BackdropB");
	[Export] public NodePath OverlayViewportPath = new("../ResearchOverlayViewport");
	[Export] public NodePath OverlayCameraPath = new("../ResearchOverlayViewport/ResearchOverlayCamera");
	[Export] public NodePath OverlayTextureRectPath = new("../CanvasLayer/ResearchOverlayPanel/InsetMargin/InsetVBox/ResearchOverlayView");
	[Export] public NodePath OverlayLabelPath = new("../CanvasLayer/ResearchOverlayPanel/InsetMargin/InsetVBox/OverlayLabel");
	[Export] public NodePath OverlayStatusPath = new("../CanvasLayer/ResearchOverlayPanel/InsetMargin/InsetVBox/OverlayStatus");
	[Export] public NodePath OverlayLegendPath = new("../CanvasLayer/ResearchOverlayPanel/InsetMargin/InsetVBox/OverlayLegend");

	[ExportGroup("Display")]
	[Export] public ResearchViewMode ViewMode = ResearchViewMode.TopDown;
	[Export] public bool ShowFieldShells = true;
	[Export] public bool ShowProbeGeometry = true;
	[Export] public bool ShowBackdrops = true;
	[Export] public int ResearchLayerMask = 16;
	[Export] public Vector2I InsetSize = new(320, 180);
	[Export(PropertyHint.Range, "20,300,1")] public float TopDownHeight = 120f;
	[Export(PropertyHint.Range, "20,220,1")] public float TopDownOrthoSize = 125f;
	[Export(PropertyHint.Range, "10,220,1")] public float ObliqueDistance = 96f;
	[Export(PropertyHint.Range, "10,220,1")] public float ObliqueHeight = 58f;
	[Export(PropertyHint.Range, "10,220,1")] public float ObliqueOrthoSize = 90f;
	[Export(PropertyHint.Range, "1,24,0.1")] public float CameraForwardLength = 8f;

	private Camera3D _mainCamera;
	private Node3D _portalA;
	private Node3D _portalB;
	private Node3D _probeWall;
	private Node3D _backdropA;
	private Node3D _backdropB;
	private SubViewport _overlayViewport;
	private Camera3D _overlayCamera;
	private TextureRect _overlayTextureRect;
	private Label _overlayLabel;
	private Label _overlayStatus;
	private Label _overlayLegend;
	private bool _protoCausticPass = true;
	private bool _lowValueBudgetPass = true;

	private MeshInstance3D _portalAMouth;
	private MeshInstance3D _portalAShell;
	private MeshInstance3D _portalAField;
	private MeshInstance3D _portalBMouth;
	private MeshInstance3D _portalBShell;
	private MeshInstance3D _portalBField;
	private MeshInstance3D _probeWallProxy;
	private MeshInstance3D _backdropAProxy;
	private MeshInstance3D _backdropBProxy;
	private MeshInstance3D _cameraMarker;
	private MeshInstance3D _cameraForward;
	private Node3D _proxyRoot;

	public override void _Ready()
	{
		_mainCamera = GetNodeOrNull<Camera3D>(MainCameraPath);
		_portalA = GetNodeOrNull<Node3D>(PortalAPath);
		_portalB = GetNodeOrNull<Node3D>(PortalBPath);
		_probeWall = GetNodeOrNull<Node3D>(ProbeWallPath);
		_backdropA = GetNodeOrNull<Node3D>(BackdropAPath);
		_backdropB = GetNodeOrNull<Node3D>(BackdropBPath);
		_overlayViewport = GetNodeOrNull<SubViewport>(OverlayViewportPath);
		_overlayCamera = GetNodeOrNull<Camera3D>(OverlayCameraPath);
		_overlayTextureRect = GetNodeOrNull<TextureRect>(OverlayTextureRectPath);
		_overlayLabel = GetNodeOrNull<Label>(OverlayLabelPath);
		_overlayStatus = GetNodeOrNull<Label>(OverlayStatusPath);
		_overlayLegend = GetNodeOrNull<Label>(OverlayLegendPath);

		ConfigureOverlayViewport();
		BuildProxyGeometry();
		UpdateOverlayLabel();
		UpdateProxyGeometry();
	}

	public override void _Process(double delta)
	{
		UpdateProxyGeometry();
	}

	private void ConfigureOverlayViewport()
	{
		if (_overlayViewport != null)
		{
			_overlayViewport.Size = InsetSize;
			_overlayViewport.TransparentBg = false;
			_overlayViewport.OwnWorld3D = true;
			_overlayViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		}

		if (_overlayTextureRect != null && _overlayViewport != null)
		{
			_overlayTextureRect.Texture = _overlayViewport.GetTexture();
			_overlayTextureRect.CustomMinimumSize = new Vector2(InsetSize.X, InsetSize.Y);
		}

		if (_overlayCamera != null)
		{
			_overlayCamera.Current = true;
			_overlayCamera.CullMask = (uint)ResearchLayerMask;
			_overlayCamera.Projection = Camera3D.ProjectionType.Orthogonal;
			_overlayCamera.Size = ViewMode == ResearchViewMode.TopDown ? TopDownOrthoSize : ObliqueOrthoSize;
			UpdateOverlayCameraTransform();
		}
	}

	private void BuildProxyGeometry()
	{
		if (_proxyRoot != null && GodotObject.IsInstanceValid(_proxyRoot))
		{
			_proxyRoot.QueueFree();
		}

		_proxyRoot = new Node3D
		{
			Name = "ResearchOverlayProxyRoot"
		};
		if (_overlayViewport != null)
		{
			_overlayViewport.AddChild(_proxyRoot);
		}
		else
		{
			AddChild(_proxyRoot);
		}

		_portalAMouth = CreateSphereProxy("PortalAMouth", 2f, new Color(0.98f, 0.58f, 0.24f, 0.55f));
		_portalAShell = CreateSphereProxy("PortalAShell", 2.12f, new Color(1f, 1f, 1f, 0.18f));
		_portalAField = CreateSphereProxy("PortalAField", 3.15f, new Color(0.38f, 0.92f, 0.62f, 0.10f));
		_portalBMouth = CreateSphereProxy("PortalBMouth", 2f, new Color(0.28f, 0.86f, 1f, 0.55f));
		_portalBShell = CreateSphereProxy("PortalBShell", 2.12f, new Color(1f, 1f, 1f, 0.18f));
		_portalBField = CreateSphereProxy("PortalBField", 3.15f, new Color(0.22f, 0.72f, 1f, 0.10f));
		_probeWallProxy = CreateBoxProxy("ProbeWallProxy", new Vector3(24f, 14f, 0.3f), new Color(0.24f, 0.52f, 1f, 0.26f));
		_backdropAProxy = CreateBoxProxy("BackdropAProxy", new Vector3(24f, 14f, 0.3f), new Color(1f, 0.46f, 0.18f, 0.14f));
		_backdropBProxy = CreateBoxProxy("BackdropBProxy", new Vector3(24f, 14f, 0.3f), new Color(0.24f, 0.52f, 1f, 0.14f));
		_cameraMarker = CreateSphereProxy("CameraMarker", 0.45f, new Color(0.98f, 0.9f, 0.28f, 0.9f));
		_cameraForward = CreateBoxProxy("CameraForward", new Vector3(0.24f, 0.24f, CameraForwardLength), new Color(1f, 0.92f, 0.3f, 0.92f));
	}

	private MeshInstance3D CreateSphereProxy(string name, float radius, Color color)
	{
		SphereMesh mesh = new()
		{
			Radius = radius,
			Height = radius * 2f,
			RadialSegments = 32,
			Rings = 16
		};
		return CreateMeshProxy(name, mesh, color);
	}

	private MeshInstance3D CreateBoxProxy(string name, Vector3 size, Color color)
	{
		BoxMesh mesh = new()
		{
			Size = size
		};
		return CreateMeshProxy(name, mesh, color);
	}

	private MeshInstance3D CreateMeshProxy(string name, Mesh mesh, Color color)
	{
		StandardMaterial3D material = new()
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color,
			NoDepthTest = false
		};
		MeshInstance3D instance = new()
		{
			Name = name,
			Mesh = mesh,
			MaterialOverride = material,
			Layers = (uint)ResearchLayerMask,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		(_proxyRoot ?? this).AddChild(instance);
		return instance;
	}

	private void UpdateProxyGeometry()
	{
		if (_portalA == null || _portalB == null || _mainCamera == null)
			return;

		SyncProxyTransform(_portalAMouth, _portalA.GlobalTransform);
		SyncProxyTransform(_portalAShell, _portalA.GlobalTransform);
		SyncProxyTransform(_portalAField, _portalA.GlobalTransform, ShowFieldShells);
		SyncProxyTransform(_portalBMouth, _portalB.GlobalTransform);
		SyncProxyTransform(_portalBShell, _portalB.GlobalTransform);
		SyncProxyTransform(_portalBField, _portalB.GlobalTransform, ShowFieldShells);

		if (_probeWallProxy != null)
		{
			_probeWallProxy.Visible = ShowProbeGeometry && _probeWall != null;
			if (_probeWall != null)
				_probeWallProxy.GlobalTransform = _probeWall.GlobalTransform;
		}

		if (_backdropAProxy != null)
		{
			_backdropAProxy.Visible = ShowBackdrops && _backdropA != null;
			if (_backdropA != null)
				_backdropAProxy.GlobalTransform = _backdropA.GlobalTransform;
		}
		if (_backdropBProxy != null)
		{
			_backdropBProxy.Visible = ShowBackdrops && _backdropB != null;
			if (_backdropB != null)
				_backdropBProxy.GlobalTransform = _backdropB.GlobalTransform;
		}

		if (_cameraMarker != null)
		{
			_cameraMarker.GlobalPosition = _mainCamera.GlobalPosition;
		}
		if (_cameraForward != null)
		{
			Vector3 origin = _mainCamera.GlobalPosition;
			Vector3 forward = -_mainCamera.GlobalTransform.Basis.Z.Normalized();
			Vector3 midpoint = origin + forward * (CameraForwardLength * 0.5f);
			Basis basis = Basis.LookingAt(forward, Vector3.Up);
			_cameraForward.GlobalTransform = new Transform3D(basis, midpoint);
		}

		UpdateOverlayCameraTransform();
		UpdateOverlayLabel();
	}

	private void SyncProxyTransform(MeshInstance3D proxy, Transform3D transform, bool visible = true)
	{
		if (proxy == null)
			return;
		proxy.Visible = visible;
		if (visible)
			proxy.GlobalTransform = transform;
	}

	private void UpdateOverlayCameraTransform()
	{
		if (_overlayCamera == null || _portalA == null || _portalB == null)
			return;

		Vector3 focus = (_portalA.GlobalPosition + _portalB.GlobalPosition) * 0.5f;
		focus.Z = (_portalA.GlobalPosition.Z + _portalB.GlobalPosition.Z) * 0.5f;
		if (ViewMode == ResearchViewMode.TopDown)
		{
			_overlayCamera.Projection = Camera3D.ProjectionType.Orthogonal;
			_overlayCamera.Size = TopDownOrthoSize;
			_overlayCamera.GlobalPosition = focus + new Vector3(0f, TopDownHeight, 0f);
			_overlayCamera.LookAt(focus, Vector3.Forward);
		}
		else
		{
			_overlayCamera.Projection = Camera3D.ProjectionType.Orthogonal;
			_overlayCamera.Size = ObliqueOrthoSize;
			_overlayCamera.GlobalPosition = focus + new Vector3(-ObliqueDistance, ObliqueHeight, ObliqueDistance * 0.22f);
			_overlayCamera.LookAt(focus, Vector3.Up);
		}
	}

	private void UpdateOverlayLabel()
	{
		if (_overlayLabel == null)
		{
			if (_overlayLegend == null && _overlayStatus == null)
				return;
		}

		string modeText = ViewMode == ResearchViewMode.TopDown ? "TOP-DOWN MAP" : "OBLIQUE MAP";
		if (_overlayLabel != null)
		{
			_overlayLabel.Text = $"RESEARCH VIEW · {modeText}";
		}

		if (_overlayStatus != null)
		{
			string protoText = _protoCausticPass ? "CAUSTIC PASS" : "CAUSTIC FAIL";
			string budgetText = _lowValueBudgetPass ? "BUDGET PASS" : "BUDGET FAIL";
			_overlayStatus.Text = $"{protoText} · {budgetText}";
			_overlayStatus.Modulate = _protoCausticPass && _lowValueBudgetPass
				? new Color(0.82f, 0.98f, 0.86f, 0.96f)
				: new Color(1f, 0.76f, 0.68f, 0.96f);
		}

		if (_overlayLegend != null)
		{
			_overlayLegend.Text = "A mouth/shell  B mouth/shell  yellow=camera  blue=probe";
		}
	}

	public void SetValidationContractStatus(bool protoCausticPass, bool lowValueBudgetPass)
	{
		_protoCausticPass = protoCausticPass;
		_lowValueBudgetPass = lowValueBudgetPass;
		UpdateOverlayLabel();
	}

	public bool TryGetSnapshot(out ResearchOverlaySnapshot snapshot)
	{
		if (_mainCamera == null || _portalA == null || _portalB == null)
		{
			snapshot = default;
			return false;
		}

		snapshot = new ResearchOverlaySnapshot(
			valid: true,
			viewMode: ViewMode,
			showFieldShells: ShowFieldShells,
			showProbeGeometry: ShowProbeGeometry,
			showBackdrops: ShowBackdrops,
			portalAPosition: _portalA.GlobalPosition,
			portalBPosition: _portalB.GlobalPosition,
			probeWallPosition: _probeWall?.GlobalPosition ?? Vector3.Zero,
			backdropAPosition: _backdropA?.GlobalPosition ?? Vector3.Zero,
			backdropBPosition: _backdropB?.GlobalPosition ?? Vector3.Zero,
			cameraPosition: _mainCamera.GlobalPosition,
			cameraForward: -_mainCamera.GlobalTransform.Basis.Z.Normalized());
		return true;
	}
}
