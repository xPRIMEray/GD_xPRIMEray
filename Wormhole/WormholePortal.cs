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
	[Export] public string WormholeId = string.Empty;
	[Export] public string LinkedWormholeId = string.Empty;
	[Export] public string ParentRegionId = string.Empty;
	[Export] public string ChildRegionId = string.Empty;

	[ExportGroup("World Layers")]
	[Export(PropertyHint.Range, "1,20,1")] public int GeometryLayerNumber = 1;
	[Export(PropertyHint.Range, "1,20,1")] public int PortalLayerNumber = 2;

	[ExportGroup("Shell")]
	[Export] public float Radius = 2.0f;
	[Export] public float ExitOffset = 0.2f;
	[Export(PropertyHint.Range, "-180,180,0.1")] public float ZeroPhaseAngleDegrees = 0.0f;
	[Export(PropertyHint.Range, "-1,1,1")] public int SpinDirection = 1;
	[Export] public bool RightHanded = true;
	[Export] public float ZoneMinZ = -8.0f;
	[Export] public float ZoneMaxZ = 8.0f;

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

	public Transform3D BuildPhaseLockedExitTransform(Transform3D sourceTransform)
	{
		if (_linkedPortal == null)
		{
			return sourceTransform;
		}

		Vector3 sourceSample = GetBoundarySampleWorldPoint(sourceTransform.Origin);
		Vector3 destinationDirection = MapBoundaryDirectionToLinkedWorld(sourceSample);
		if (destinationDirection.LengthSquared() < 1e-6f)
		{
			destinationDirection = -_linkedPortal.GlobalTransform.Basis.Z;
		}

		Vector3 forward = destinationDirection.Normalized();
		Vector3 upHint = _linkedPortal.GlobalTransform.Basis.Y.Normalized();
		if (Mathf.Abs(forward.Dot(upHint)) > 0.98f)
		{
			upHint = _linkedPortal.GlobalTransform.Basis.X.Normalized();
		}

		Vector3 right = upHint.Cross(forward).Normalized();
		Vector3 trueUp = forward.Cross(right).Normalized();
		Basis basis = new Basis(right, trueUp, -forward).Orthonormalized();

		return new Transform3D(
			basis,
			_linkedPortal.GlobalPosition + forward * (_linkedPortal.Radius + _linkedPortal.ExitOffset));
	}

	public Vector3 GetBoundarySampleWorldPoint(Vector3 worldPoint)
	{
		Vector3 local = ToLocal(worldPoint);
		if (local.LengthSquared() < 1e-6f)
		{
			local = -GlobalTransform.Basis.Z;
		}

		return GlobalTransform * local.Normalized() * Radius;
	}

	public Vector3 GetZeroMarkerWorldPoint(float radiusScale = 1.0f)
	{
		float markerRadius = Mathf.Max(0.05f, Radius * radiusScale);
		Vector3 local = new Vector3(Mathf.Cos(GetZeroPhaseRadians()), 0f, Mathf.Sin(GetZeroPhaseRadians())) * markerRadius;
		return GlobalTransform * local;
	}

	public Vector3 MapBoundarySamplePointToLinkedWorld(Vector3 sourceWorldPoint)
	{
		Vector3 direction = MapBoundaryDirectionToLinkedWorld(sourceWorldPoint);
		if (direction.LengthSquared() < 1e-6f)
		{
			direction = -_linkedPortal.GlobalTransform.Basis.Z;
		}

		return _linkedPortal.GlobalPosition + direction.Normalized() * _linkedPortal.Radius;
	}

	public Vector3 MapBoundaryDirectionToLinkedWorld(Vector3 sourceWorldPoint)
	{
		if (_linkedPortal == null)
		{
			return Vector3.Zero;
		}

		Vector3 local = ResolveSampleLocalDirection(sourceWorldPoint);
		float height = Mathf.Clamp(local.Y, -1.0f, 1.0f);
		float planarLength = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - height * height));
		float sourceAzimuth = planarLength > 1e-5f ? Mathf.Atan2(local.Z, local.X) : 0.0f;
		float relativePhase = WrapAngle(sourceAzimuth - GetZeroPhaseRadians());

		int sourceHand = RightHanded ? 1 : -1;
		int targetHand = _linkedPortal.RightHanded ? 1 : -1;
		int targetSpin = Mathf.Clamp(_linkedPortal.SpinDirection, -1, 1);
		if (targetSpin == 0)
		{
			targetSpin = 1;
		}

		float targetAzimuth = _linkedPortal.GetZeroPhaseRadians() + relativePhase * sourceHand * targetHand * targetSpin;
		Vector3 destinationLocal = new Vector3(
			Mathf.Cos(targetAzimuth) * planarLength,
			height,
			Mathf.Sin(targetAzimuth) * planarLength);
		return (_linkedPortal.GlobalTransform.Basis * destinationLocal).Normalized();
	}

	public float ComputePhaseAngleDegrees(Vector3 sourceWorldPoint)
	{
		Vector3 local = ResolveSampleLocalDirection(sourceWorldPoint);
		float planarLength = Mathf.Sqrt(local.X * local.X + local.Z * local.Z);
		float sourceAzimuth = planarLength > 1e-5f ? Mathf.Atan2(local.Z, local.X) : 0.0f;
		return Mathf.RadToDeg(WrapAngle(sourceAzimuth - GetZeroPhaseRadians()));
	}

	public bool ContainsWorldZ(float worldZ)
	{
		return worldZ >= Mathf.Min(ZoneMinZ, ZoneMaxZ) && worldZ <= Mathf.Max(ZoneMinZ, ZoneMaxZ);
	}

	public string ResolveZoneLabel()
	{
		if (!string.IsNullOrWhiteSpace(ParentRegionId))
		{
			return ParentRegionId;
		}

		if (!string.IsNullOrWhiteSpace(ChildRegionId))
		{
			return ChildRegionId;
		}

		return Name;
	}

	public WormholePortal GetLinkedPortal()
	{
		return _linkedPortal;
	}

	private Vector3 ResolveSampleLocalDirection(Vector3 sourceWorldPoint)
	{
		Vector3 local = ToLocal(sourceWorldPoint);
		if (local.LengthSquared() < 1e-6f)
		{
			local = -GlobalTransform.Basis.Z;
		}

		return local.Normalized();
	}

	private float GetZeroPhaseRadians()
	{
		return Mathf.DegToRad(ZeroPhaseAngleDegrees);
	}

	private static float WrapAngle(float radians)
	{
		return Mathf.Wrap(radians, -Mathf.Pi, Mathf.Pi);
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
