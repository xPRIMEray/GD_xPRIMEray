using Godot;

public partial class RayViz : MeshInstance3D
{
	[Export] public int RayCount = 9;              // 9 = corners + mids + center (default pattern)
	[Export] public int StepsPerRay = 64;          // polyline resolution
	[Export] public float MaxDistance = 20.0f;     // how far to draw
	[Export] public float BendScale = 0.15f;       // overall bend visibility (tune this)
	[Export] public bool DrawEveryFrame = false;   // set true for continuous redraw

	private ImmediateMesh _im;
	private StandardMaterial3D _mat;

	private float _lastBeta = float.NaN;
	private float _lastGamma = float.NaN;
	private Vector2I _lastViewportSize = new(-1, -1);

	public override void _Ready()
	{
		_im = new ImmediateMesh();
		Mesh = _im;

		_mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			Transparency = BaseMaterial3D.TransparencyEnum.Disabled
		};
		MaterialOverride = _mat;

		Rebuild();
	}

	public override void _Process(double delta)
	{
		if (DrawEveryFrame)
		{
			Rebuild();
			return;
		}

		// Rebuild only when Beta/Gamma or viewport size changes
		var cam = GetViewport()?.GetCamera3D();
		if (cam == null) return;

		float beta = ReadFloat(cam, "Beta", 0f);
		float gamma = ReadFloat(cam, "Gamma", 2f);

		Vector2 vpSizeF = GetViewport().GetVisibleRect().Size;
		Vector2I vpSize = new Vector2I((int)vpSizeF.X, (int)vpSizeF.Y);

		if (!Mathf.IsEqualApprox(beta, _lastBeta) ||
			!Mathf.IsEqualApprox(gamma, _lastGamma) ||
			vpSize != _lastViewportSize)
		{
			Rebuild();
		}
	}

	private void Rebuild()
	{
		var viewport = GetViewport();
		if (viewport == null) return;

		var cam = viewport.GetCamera3D();
		if (cam == null) return;

		float beta = ReadFloat(cam, "Beta", 0f);
		float gamma = ReadFloat(cam, "Gamma", 2f);

		_lastBeta = beta;
		_lastGamma = gamma;
		Vector2 vpSizeF = viewport.GetVisibleRect().Size;
		_lastViewportSize = new Vector2I((int)vpSizeF.X, (int)vpSizeF.Y);

		_im.ClearSurfaces();

		// Build a set of screen sample points (pixels)
		Vector2I sizeI = _lastViewportSize;
		Vector2 size = new(sizeI.X, sizeI.Y);
		Vector2 center = size * 0.5f;

		// Default 9-point pattern: corners, edge mids, center
		Vector2[] uv = new Vector2[]
		{
			new(0.05f, 0.05f),
			new(0.95f, 0.05f),
			new(0.05f, 0.95f),
			new(0.95f, 0.95f),
			new(0.50f, 0.05f),
			new(0.50f, 0.95f),
			new(0.05f, 0.50f),
			new(0.95f, 0.50f),
			new(0.50f, 0.50f),
		};
		Vector2[] samples = new Vector2[uv.Length];
		for (int i = 0; i < uv.Length; i++)
			samples[i] = new Vector2(uv[i].X * size.X, uv[i].Y * size.Y);

		// If RayCount != 9, we’ll just use the first N of this list (or clamp).
		//int n = Mathf.Clamp(RayCount, 1, samples.Length);
		int n = 9;

		// Camera basis vectors for “screen plane” directions
		Vector3 right = cam.GlobalTransform.Basis.X.Normalized();
		Vector3 up = cam.GlobalTransform.Basis.Y.Normalized();

		// Draw each ray as a LineStrip
		for (int i = 0; i < n; i++)
		{
			Vector2 sp = samples[i];
			Vector3 origin = cam.ProjectRayOrigin(sp);
			Vector3 forward = -cam.GlobalTransform.Basis.Z.Normalized();
			origin += forward * (0.01f * i);

			Vector3 dir = cam.ProjectRayNormal(sp).Normalized();

			// Screen offset direction → maps to world bend direction in camera plane
			Vector2 offset = sp - center;
			Vector2 offsetN = offset.Length() > 1e-6f ? offset / offset.Length() : Vector2.Zero;

			// Bend direction in world, within the camera plane
			Vector3 bendDir = (right * offsetN.X + up * -offsetN.Y).Normalized();
			if (bendDir.Length() < 1e-6f) bendDir = right; // fallback

			// Choose a per-ray color (simple gradient)
			Color c = Color.FromHsv((float)i / Mathf.Max(1, n), 0.9f, 1.0f);

			_im.SurfaceBegin(Mesh.PrimitiveType.LineStrip, _mat);
			float m = 0.05f; // marker size
			_im.SurfaceSetColor(c);
			_im.SurfaceAddVertex(origin - right * m);
			_im.SurfaceAddVertex(origin + right * m);
			_im.SurfaceAddVertex(origin - up * m);
			_im.SurfaceAddVertex(origin + up * m);

			for (int s = 0; s <= StepsPerRay; s++)
			{
				float t01 = (float)s / StepsPerRay;
				float t = t01 * MaxDistance;

				// Analytic “curved ray” prototype:
				// p(t) = o + d*t + bendDir * (beta * (t^gamma)) * BendScale
				float bend = beta * Mathf.Pow(t, gamma) * BendScale;

				Vector3 p = origin + dir * t + bendDir * bend;

				_im.SurfaceSetColor(c);
				_im.SurfaceAddVertex(p);
			}

			_im.SurfaceEnd();
		}
	}

	private static float ReadFloat(Node obj, StringName prop, float fallback)
	{
		Variant v = obj.Get(prop);
		return v.VariantType switch
		{
			Variant.Type.Float => (float)v,
			Variant.Type.Int => (int)v,
			_ => fallback
		};
	}
}
