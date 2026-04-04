using Godot;

/// <summary>
/// Scene-level portal mouth for the first wormhole prototype.
/// Keeps renderer-core changes out of the critical path by using a linked
/// SubViewport camera and a spherical portal surface.
/// </summary>
public partial class WormholePortal : Node3D
{
	[ExportGroup("Link")]
	[Export] public NodePath LinkedPortalPath;

	[ExportGroup("World Layers")]
	[Export(PropertyHint.Range, "1,20,1")] public int GeometryLayerNumber = 1;
	[Export(PropertyHint.Range, "1,20,1")] public int PortalLayerNumber = 2;

	[ExportGroup("Shell")]
	[Export] public float Radius = 2.0f;
	[Export] public float ExitOffset = 0.2f;

	[ExportGroup("Surface")]
	[Export] public NodePath PortalSurfacePath;
	[Export] public NodePath PortalViewportPath;
	[Export] public NodePath PortalCameraPath;
	[Export] public string PortalShaderPath = "res://Wormhole/WormholePortalSurface.gdshader";
	[Export(PropertyHint.Range, "0,0.35,0.001")] public float DistortionStrength = 0.075f;
	[Export] public int PortalTextureSize = 768;

	private static readonly Transform3D PortalFlip = new Transform3D(
		Basis.FromEuler(new Vector3(0f, Mathf.Pi, 0f)),
		Vector3.Zero);

	private WormholePortal _linkedPortal;
	private MeshInstance3D _portalSurface;
	private SubViewport _portalViewport;
	private Camera3D _portalCamera;
	private ShaderMaterial _portalMaterial;

	public uint GeometryMask => LayerNumberToMask(GeometryLayerNumber);
	public uint PortalMask => LayerNumberToMask(PortalLayerNumber);

	public override void _Ready()
	{
		_portalSurface = GetNodeOrNull<MeshInstance3D>(PortalSurfacePath);
		_portalViewport = GetNodeOrNull<SubViewport>(PortalViewportPath);
		_portalCamera = GetNodeOrNull<Camera3D>(PortalCameraPath);
		_linkedPortal = GetNodeOrNull<WormholePortal>(LinkedPortalPath);

		if (_portalViewport != null)
		{
			_portalViewport.Size = new Vector2I(PortalTextureSize, PortalTextureSize);
		}

		if (_portalCamera != null)
		{
			_portalCamera.Current = true;
		}

		ConfigureSurfaceMaterial();
	}

	public void UpdatePortalView(Camera3D viewer)
	{
		if (viewer == null || _linkedPortal == null || _portalViewport == null || _portalCamera == null)
		{
			return;
		}

		_portalViewport.World3D = viewer.GetWorld3D();
		_portalCamera.GlobalTransform = MapTransformToLinked(viewer.GlobalTransform);
		_portalCamera.Projection = viewer.Projection;
		_portalCamera.Fov = viewer.Fov;
		_portalCamera.Size = viewer.Size;
		_portalCamera.Near = viewer.Near;
		_portalCamera.Far = viewer.Far;
		_portalCamera.KeepAspect = viewer.KeepAspect;
		_portalCamera.CullMask = _linkedPortal.GeometryMask;
	}

	public float SignedRadiusDelta(Vector3 worldPoint)
	{
		return ToLocal(worldPoint).Length() - Radius;
	}

	public Transform3D MapTransformToLinked(Transform3D sourceTransform)
	{
		if (_linkedPortal == null)
		{
			return sourceTransform;
		}

		return _linkedPortal.GlobalTransform * PortalFlip * GlobalTransform.AffineInverse() * sourceTransform;
	}

	public Transform3D BuildExitTransform(Transform3D sourceTransform)
	{
		Transform3D mapped = MapTransformToLinked(sourceTransform);
		Vector3 radial = mapped.Origin - _linkedPortal.GlobalPosition;
		if (radial.LengthSquared() < 1e-5f)
		{
			radial = -_linkedPortal.GlobalTransform.Basis.Z;
		}

		mapped.Origin = _linkedPortal.GlobalPosition + radial.Normalized() * (_linkedPortal.Radius + _linkedPortal.ExitOffset);
		return mapped;
	}

	public WormholePortal GetLinkedPortal()
	{
		return _linkedPortal;
	}

	private void ConfigureSurfaceMaterial()
	{
		if (_portalSurface == null || _portalViewport == null)
		{
			return;
		}

		Shader shader = ResourceLoader.Load<Shader>(PortalShaderPath);
		if (shader == null)
		{
			GD.PushWarning($"{Name}: failed to load wormhole portal shader at '{PortalShaderPath}'.");
			return;
		}

		_portalMaterial = new ShaderMaterial
		{
			Shader = shader
		};
		_portalMaterial.SetShaderParameter("portal_tex", _portalViewport.GetTexture());
		_portalMaterial.SetShaderParameter("distortion_strength", DistortionStrength);
		_portalSurface.MaterialOverride = _portalMaterial;
	}

	private static uint LayerNumberToMask(int layerNumber)
	{
		if (layerNumber <= 0)
		{
			return 0u;
		}

		return 1u << (layerNumber - 1);
	}
}
