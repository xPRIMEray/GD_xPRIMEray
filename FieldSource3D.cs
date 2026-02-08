using Godot;
using System;
using RendererCore.Fields;

public partial class FieldSource3D : Node3D
{
	public enum ProfileType
	{
		Power,          // ~ r^gamma
		InversePower,   // ~ 1 / r^gamma
		Gaussian,       // ~ exp(-(r/sigma)^2)
		Shell           // ring/shell band between InnerRadius..OuterRadius
	}

	[Export] public bool Enabled = true;

	[Export] public bool Attract = true;     // true = pulls toward center, false = pushes away
	[Export] public float Strength = 1.0f;   // per-source multiplier

	// Spatial shaping
	[Export] public float Softening = 0.05f; // avoids singularities
	[Export] public float MinRadius = 0.0f;  // ignore inside
	[Export] public float MaxRadius = 0.0f;  // 0 = infinite

	// Falloff profile
	[Export] public ProfileType Profile = ProfileType.Power;

	// Per-source law controls
	[Export] public bool OverrideGamma = false;
	[Export] public float Gamma = 2.0f;

	[Export] public bool OverrideBetaScale = false;
	[Export] public float BetaScale = 1.0f; // multiplies global beta (or acts as local beta if you want)

	// Gaussian params
	[Export] public float Sigma = 5.0f;      // for Gaussian

	// Shell params
	[Export] public float InnerRadius = 3.0f;
	[Export] public float OuterRadius = 6.0f;
	[Export] public float EdgeSoftness = 0.5f;  // smoothstep thickness at edges

	[ExportGroup("Field Model")]
	[Export] public MetricModel MetricModel { get; set; } = MetricModel.GRIN;
	[Export] public float RInner { get; set; } = 0f;
	[Export] public float ROuter { get; set; } = 10f;
	[Export] public float Amp { get; set; } = 1f;
	[Export] public uint ModeFlags { get; set; } = 0;

	[ExportGroup("Shape")]
	[Export] public FieldShapeType ShapeType { get; set; } = FieldShapeType.SphereRadial;
	[Export] public Vector3 BoxExtents { get; set; } = new Vector3(10, 10, 10);

	[ExportGroup("Curve")]
	[Export] public FieldCurveType CurveType { get; set; } = FieldCurveType.Power;
	[Export] public float CurveA { get; set; } = 1f;
	[Export] public float CurveB { get; set; } = 0f;
	[Export] public float CurveC { get; set; } = 0f;

	[ExportGroup("Debug")]
	[Export] public bool DebugDrawBounds { get; set; } = false;
	[Export] public Color DebugColor { get; set; } = new Color(0.4f, 0.9f, 1.0f);

	// Runtime debug visualization
	[Export] public bool DebugDrawInGame = false;
	[Export] public bool DebugDrawAlwaysOnTop = true;
	[Export] public int DebugRingSegments = 32;
	[Export] public float DebugLineWidth = 2.0f;

	private MeshInstance3D _debugMeshInstance;
	private ImmediateMesh _debugMesh;
	private DebugState _debugState;
	private bool _debugStateValid;

	public override void _Ready()
	{
		AddToGroup("field_sources");
		ApplyLegacyMappings();
		ValidateAndClamp();

		if (!Engine.IsEditorHint() && DebugDrawInGame)
		{
			EnsureDebugDraw();
			DebugState state = GetDebugState();
			RebuildDebugMesh(state);
			_debugState = state;
			_debugStateValid = true;
			SetProcess(true);
		}
	}

	public override void _ExitTree()
	{
		if (!Engine.IsEditorHint())
		{
			ClearDebugDraw();
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
		{
			return;
		}

		if (!DebugDrawInGame)
		{
			if (_debugMeshInstance != null)
			{
				_debugMeshInstance.Visible = false;
			}
			return;
		}

		if (_debugMeshInstance != null && !_debugMeshInstance.Visible)
		{
			_debugMeshInstance.Visible = true;
		}

		if (DebugDrawBounds)
		{
			// TODO: Hook into a shared debug draw system if one exists.
		}

		DebugState currentState = GetDebugState();
		if (!_debugStateValid || !_debugState.Equals(currentState))
		{
			RebuildDebugMesh(currentState);
			_debugState = currentState;
			_debugStateValid = true;
		}
	}

	private DebugState GetDebugState()
	{
		return new DebugState
		{
			Enabled = Enabled,
			Attract = Attract,
			Profile = Profile,
			InnerRadius = InnerRadius,
			OuterRadius = OuterRadius,
			MaxRadius = MaxRadius,
			Segments = Mathf.Max(3, DebugRingSegments),
			LineWidth = Mathf.Max(1.0f, DebugLineWidth),
			AlwaysOnTop = DebugDrawAlwaysOnTop
		};
	}

	private void ApplyLegacyMappings()
	{
		const float defaultROuter = 10f;
		const float defaultAmp = 1f;

		if (OuterRadius > 0.0f && Mathf.IsEqualApprox(ROuter, defaultROuter))
		{
			ROuter = OuterRadius;
		}

		if (InnerRadius > 0.0f && Mathf.IsEqualApprox(RInner, 0.0f))
		{
			RInner = InnerRadius;
		}

		if (!Mathf.IsEqualApprox(Strength, defaultAmp) && Mathf.IsEqualApprox(Amp, defaultAmp))
		{
			Amp = Strength;
		}

		if (OverrideGamma && Mathf.IsEqualApprox(CurveA, 1f))
		{
			CurveA = Gamma;
		}

		if (Profile == ProfileType.Gaussian && CurveType == FieldCurveType.Power)
		{
			CurveType = FieldCurveType.Exponential;
		}
	}

	private void ValidateAndClamp()
	{
		bool warned = false;

		if (ROuter < 0.0f)
		{
			ROuter = 0.0f;
			warned = true;
		}

		if (ROuter < RInner)
		{
			ROuter = RInner;
			warned = true;
		}

		if (ShapeType == FieldShapeType.BoxVolume)
		{
			Vector3 clamped = new Vector3(
				Mathf.Max(0.0f, BoxExtents.X),
				Mathf.Max(0.0f, BoxExtents.Y),
				Mathf.Max(0.0f, BoxExtents.Z));

			if (clamped != BoxExtents)
			{
				BoxExtents = clamped;
				warned = true;
			}
		}

		if (warned)
		{
			GD.PushWarning($"{Name}: FieldSource3D parameters were clamped to safe ranges.");
		}
	}

	public void GetPackedParams8(out float rInner, out float rOuter, out float amp, out float a, out float b, out float c, out float reserved0, out float reserved1)
	{
		rInner = RInner;
		rOuter = ROuter;
		amp = Amp;
		a = CurveA;
		b = CurveB;
		c = CurveC;
		reserved0 = 0f;
		reserved1 = 0f;
	}

	public Aabb GetLocalInfluenceAabb()
	{
		if (ShapeType == FieldShapeType.BoxVolume)
		{
			Vector3 size = BoxExtents * 2.0f;
			return new Aabb(-BoxExtents, size);
		}

		Vector3 half = new Vector3(ROuter, ROuter, ROuter);
		return new Aabb(-half, half * 2.0f);
	}

	public Aabb GetWorldInfluenceAabbConservative()
	{
		Aabb local = GetLocalInfluenceAabb();
		Vector3 min = Vector3.Zero;
		Vector3 max = Vector3.Zero;
		bool initialized = false;

		for (int z = 0; z <= 1; z++)
		{
			for (int y = 0; y <= 1; y++)
			{
				for (int x = 0; x <= 1; x++)
				{
					Vector3 corner = new Vector3(
						x == 0 ? local.Position.X : local.Position.X + local.Size.X,
						y == 0 ? local.Position.Y : local.Position.Y + local.Size.Y,
						z == 0 ? local.Position.Z : local.Position.Z + local.Size.Z);

					Vector3 world = GlobalTransform * corner;

					if (!initialized)
					{
						min = world;
						max = world;
						initialized = true;
					}
					else
					{
						min = new Vector3(
							Mathf.Min(min.X, world.X),
							Mathf.Min(min.Y, world.Y),
							Mathf.Min(min.Z, world.Z));

						max = new Vector3(
							Mathf.Max(max.X, world.X),
							Mathf.Max(max.Y, world.Y),
							Mathf.Max(max.Z, world.Z));
					}
				}
			}
		}

		return new Aabb(min, max - min);
	}

	private void EnsureDebugDraw()
	{
		if (_debugMeshInstance != null)
		{
			return;
		}

		_debugMesh = new ImmediateMesh();
		_debugMeshInstance = new MeshInstance3D
		{
			Name = "_FieldSourceDebugDraw",
			Mesh = _debugMesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		AddChild(_debugMeshInstance);
	}

	private void ClearDebugDraw()
	{
		if (_debugMeshInstance == null)
		{
			return;
		}

		_debugMeshInstance.QueueFree();
		_debugMeshInstance = null;
		_debugMesh = null;
		_debugStateValid = false;
	}

	private void RebuildDebugMesh(DebugState state)
	{
		if (_debugMesh == null)
		{
			return;
		}

		_debugMesh.ClearSurfaces();

		Color markerColor = GetMarkerColor(state);
		float markerScale = state.Profile == ProfileType.Shell ? 1.1f : 1.0f;
		float markerSize = 0.06f * markerScale;
		float arrowLength = 0.3f;

		AddSurface(markerColor, state, 2.5f, () =>
		{
			AddAxisMarker(markerSize);
			if (state.Enabled)
			{
				AddArrow(arrowLength, state.Attract);
			}
		});

		GetRingStyle(state, out RingStyle innerStyle, out RingStyle outerStyle, out RingStyle maxStyle);

		if (state.InnerRadius > 0.0f)
		{
			AddSurface(innerStyle.Color, state, innerStyle.LineWidth, () => AddCircle(state.InnerRadius, state.Segments));
		}

		if (state.OuterRadius > 0.0f)
		{
			AddSurface(outerStyle.Color, state, outerStyle.LineWidth, () => AddCircle(state.OuterRadius, state.Segments));
		}

		if (state.MaxRadius > 0.0f)
		{
			AddSurface(maxStyle.Color, state, maxStyle.LineWidth, () => AddDashedCircle(state.MaxRadius, state.Segments));
		}
	}

	private void AddSurface(Color color, DebugState state, float lineWidth, Action addGeometry)
	{
		var material = CreateLineMaterial(color, state, lineWidth);
		_debugMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
		addGeometry();
		_debugMesh.SurfaceEnd();
	}

	private StandardMaterial3D CreateLineMaterial(Color color, DebugState state, float lineWidth)
	{
		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = color,
			RenderPriority = state.AlwaysOnTop ? 127 : 0,
			NoDepthTest = state.AlwaysOnTop
		};

		if (color.A < 1.0f)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		}

		//material.LineWidth = lineWidth; //obsolete
		return material;
	}

	private Color GetMarkerColor(DebugState state)
	{
		if (!state.Enabled)
		{
			return new Color(0.533f, 0.533f, 0.533f, 0.25f);
		}

		if (state.Attract)
		{
			return new Color(0.0f, 0.85f, 0.95f, 1.0f);
		}

		return new Color(1.0f, 0.55f, 0.1f, 1.0f);
	}

	private void AddAxisMarker(float size)
	{
		AddLine(new Vector3(-size, 0.0f, 0.0f), new Vector3(size, 0.0f, 0.0f));
		AddLine(new Vector3(0.0f, -size, 0.0f), new Vector3(0.0f, size, 0.0f));
		AddLine(new Vector3(0.0f, 0.0f, -size), new Vector3(0.0f, 0.0f, size));
	}

	private void AddArrow(float length, bool attract)
	{
		float headSize = length * 0.3f;
		float headOffset = length * 0.8f;
		Vector3 tip = attract ? Vector3.Zero : new Vector3(length, 0.0f, 0.0f);
		Vector3 tail = attract ? new Vector3(length, 0.0f, 0.0f) : Vector3.Zero;
		float headDir = attract ? -1.0f : 1.0f;

		AddLine(tail, tip);
		AddLine(tip, new Vector3(tip.X - headDir * (length - headOffset), headSize, 0.0f));
		AddLine(tip, new Vector3(tip.X - headDir * (length - headOffset), -headSize, 0.0f));
	}

	private void AddCircle(float radius, int segments)
	{
		int safeSegments = Mathf.Max(3, segments);
		for (int i = 0; i < safeSegments; i++)
		{
			float a0 = Mathf.Tau * i / safeSegments;
			float a1 = Mathf.Tau * (i + 1) / safeSegments;
			Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0.0f, Mathf.Sin(a0) * radius);
			Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0.0f, Mathf.Sin(a1) * radius);
			AddLine(p0, p1);
		}
	}

	private void AddDashedCircle(float radius, int segments)
	{
		int safeSegments = Mathf.Max(3, segments);
		for (int i = 0; i < safeSegments; i += 2)
		{
			float a0 = Mathf.Tau * i / safeSegments;
			float a1 = Mathf.Tau * (i + 1) / safeSegments;
			Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0.0f, Mathf.Sin(a0) * radius);
			Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0.0f, Mathf.Sin(a1) * radius);
			AddLine(p0, p1);
		}
	}

	private void AddLine(Vector3 start, Vector3 end)
	{
		_debugMesh.SurfaceAddVertex(start);
		_debugMesh.SurfaceAddVertex(end);
	}

	private void GetRingStyle(DebugState state, out RingStyle innerStyle, out RingStyle outerStyle, out RingStyle maxStyle)
	{
		if (!state.Enabled)
		{
			Color disabled = new Color(0.533f, 0.533f, 0.533f, 0.25f);
			innerStyle = new RingStyle(disabled, state.LineWidth * 1.2f);
			outerStyle = new RingStyle(disabled, state.LineWidth);
			maxStyle = new RingStyle(disabled, state.LineWidth * 0.75f);
			return;
		}

		Color innerColor;
		Color outerColor;
		if (state.Attract)
		{
			innerColor = new Color(0.15f, 0.85f, 0.8f, 0.85f);
			outerColor = new Color(0.2f, 0.85f, 0.3f, 0.65f);
		}
		else
		{
			innerColor = new Color(1.0f, 0.6f, 0.2f, 0.85f);
			outerColor = new Color(0.95f, 0.2f, 0.15f, 0.65f);
		}

		float innerWidth = state.LineWidth * 1.2f;
		float outerWidth = state.LineWidth;

		switch (state.Profile)
		{
			case ProfileType.Shell:
				innerWidth *= 1.1f;
				outerWidth *= 1.1f;
				break;
			case ProfileType.Power:
			case ProfileType.InversePower:
				innerWidth *= 0.9f;
				outerWidth *= 1.1f;
				break;
			case ProfileType.Gaussian:
				innerColor = new Color(innerColor.R, innerColor.G, innerColor.B, 0.5f);
				outerColor = new Color(outerColor.R, outerColor.G, outerColor.B, 0.25f);
				break;
		}

		innerStyle = new RingStyle(innerColor, innerWidth);
		outerStyle = new RingStyle(outerColor, outerWidth);
		maxStyle = new RingStyle(new Color(0.6f, 0.6f, 0.6f, 0.35f), state.LineWidth * 0.75f);
	}

	private struct DebugState
	{
		public bool Enabled;
		public bool Attract;
		public ProfileType Profile;
		public float InnerRadius;
		public float OuterRadius;
		public float MaxRadius;
		public int Segments;
		public float LineWidth;
		public bool AlwaysOnTop;

		public bool Equals(DebugState other)
		{
			return Enabled == other.Enabled
				&& Attract == other.Attract
				&& Profile == other.Profile
				&& InnerRadius == other.InnerRadius
				&& OuterRadius == other.OuterRadius
				&& MaxRadius == other.MaxRadius
				&& Segments == other.Segments
				&& LineWidth == other.LineWidth
				&& AlwaysOnTop == other.AlwaysOnTop;
		}
	}

	private readonly struct RingStyle
	{
		public readonly Color Color;
		public readonly float LineWidth;

		public RingStyle(Color color, float lineWidth)
		{
			Color = color;
			LineWidth = lineWidth;
		}
	}
}
