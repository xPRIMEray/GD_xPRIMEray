using Godot;
using System;
using System.Collections.Generic;
using RendererCore.Common;
using GdVector2 = Godot.Vector2;

/// <summary>
/// Integrated debug probe that samples all FieldSource3D nodes in "field_sources"
/// and visualizes effective field strength/direction in editor or game.
/// </summary>
[Tool]
public partial class FieldProbe3D : Node3D
{
	public enum ProbeModeKind
	{
		AccelMagnitude = 0,
		AccelVector = 1,
		DivergenceApprox = 2,
		CurlApprox = 3
	}

	[ExportGroup("Probe")]
	[Export] public bool Enabled { get; set; } = true;
	[Export(PropertyHint.Range, "1,120,1")] public float SampleRateHz { get; set; } = 20f;
	[Export] public ProbeModeKind ProbeMode { get; set; } = ProbeModeKind.AccelMagnitude;
	[Export] public bool ShowVectorArrow { get; set; } = true;
	[Export] public bool ShowNumericOverlay { get; set; } = true;
	[Export] public Gradient ProbeColorRamp { get; set; }
	[Export(PropertyHint.Range, "0.02,2.0,0.01")] public float ProbeDrawRadius { get; set; } = 0.15f;
	[Export] public bool ShowOrthogonalCrosshair { get; set; } = true;
	[Export(PropertyHint.Range, "0.01,1.0,0.01")] public float FiniteDifferenceStep { get; set; } = 0.2f;
	[Export] public float GlobalBeta { get; set; } = 0f;
	[Export] public float GlobalGamma { get; set; } = 2f;
	[Export] public float BendScale { get; set; } = 1f;
	[Export] public float FieldStrength { get; set; } = 1f;

	[ExportGroup("Probe Movement")]
	[Export] public bool EnableFreeFlyControls { get; set; } = false;
	[Export(PropertyHint.Range, "0.1,50.0,0.1")] public float FreeFlySpeed { get; set; } = 5f;
	[Export(PropertyHint.Range, "1.0,10.0,0.1")] public float FreeFlyBoostMultiplier { get; set; } = 2f;

	[ExportGroup("Legacy Probe Logging")]
	[Export] public bool Print = false;
	[Export(PropertyHint.Range, "0.05,10.0,0.05")] public float PrintIntervalSec = 0.25f;
	[Export] public float EpsPos = 0.01f;
	[Export] public float DtMin = 0.001f;
	[Export] public float DtMax = 0.05f;

	private const float Epsilon = 1e-6f;
	private MeshInstance3D _sphereInstance;
	private SphereMesh _sphereMesh;
	private StandardMaterial3D _sphereMaterial;
	private MeshInstance3D _lineInstance;
	private ImmediateMesh _lineMesh;
	private StandardMaterial3D _lineMaterial;
	private readonly List<FieldSource3D> _sourceCache = new List<FieldSource3D>(32);

	private double _sampleAccumulatorSec;
	private double _nextPrintTimeSec;
	private double _nextProbeLogTimeSec;
	private Vector3 _lastAccel;
	private Vector3 _lastModeVector;
	private Vector3 _lastCurl;
	private float _lastDivergence;
	private float _lastMetricMagnitude;
	private int _lastSourceCount;
	private bool _hasSample;

	public override void _Ready()
	{
		SetProcess(true);
		EnsureVisualNodes();
	}

	public override void _ExitTree()
	{
		FreeVisualNodes();
	}

	public override void _Process(double delta)
	{
		if (EnableFreeFlyControls && !Engine.IsEditorHint())
		{
			ApplyFreeFly(delta);
		}

		if (!Enabled)
		{
			HideVisuals();
			return;
		}

		EnsureVisualNodes();
		double sampleInterval = 1.0 / Math.Max(1.0, SampleRateHz);
		_sampleAccumulatorSec += delta;

		if (_sampleAccumulatorSec >= sampleInterval)
		{
			_sampleAccumulatorSec = 0.0;
			SampleIntegratedField();
		}

		if (_hasSample)
		{
			UpdateVisuals();
			AddNumericOverlay();
			MaybePrint();
			MaybeProbeReadLog();
		}
	}

	private void SampleIntegratedField()
	{
		GatherFieldSources();
		ResolveGlobalBetaGamma(out float beta, out float gamma);

		Vector3 p = GlobalPosition;
		RayBeamRenderer.FieldSourceSnap[] sourceSnaps = BuildSourceSnaps();
		Vector3 accel = EvaluateIntegratedAcceleration(p, beta, gamma, sourceSnaps);
		_lastAccel = accel;
		_lastSourceCount = _sourceCache.Count;

		switch (ProbeMode)
		{
			case ProbeModeKind.AccelMagnitude:
				_lastModeVector = accel;
				_lastMetricMagnitude = accel.Length();
				break;

			case ProbeModeKind.AccelVector:
				_lastModeVector = accel;
				_lastMetricMagnitude = accel.Length();
				break;

			case ProbeModeKind.DivergenceApprox:
				_lastDivergence = ApproximateDivergence(p, beta, gamma, sourceSnaps);
				_lastModeVector = accel;
				_lastMetricMagnitude = Mathf.Abs(_lastDivergence);
				break;

			case ProbeModeKind.CurlApprox:
				_lastCurl = ApproximateCurl(p, beta, gamma, sourceSnaps);
				_lastModeVector = _lastCurl;
				_lastMetricMagnitude = _lastCurl.Length();
				break;
		}

		_hasSample = true;
	}

	private void GatherFieldSources()
	{
		_sourceCache.Clear();

		var tree = GetTree();
		if (tree == null)
		{
			return;
		}

		var nodes = tree.GetNodesInGroup("field_sources");
		if (nodes == null)
		{
			return;
		}

		foreach (Node node in nodes)
		{
			if (node is FieldSource3D field && IsInstanceValid(field))
			{
				_sourceCache.Add(field);
			}
		}
	}

	private void ResolveGlobalBetaGamma(out float beta, out float gamma)
	{
		beta = GlobalBeta;
		gamma = GlobalGamma;

		Camera3D camera = GetViewport()?.GetCamera3D();
		if (camera == null || !IsInstanceValid(camera))
		{
			return;
		}

		if (TryReadFloat(camera, "Beta", out float camBeta))
		{
			beta = camBeta;
		}
		if (TryReadFloat(camera, "Gamma", out float camGamma))
		{
			gamma = camGamma;
		}
	}

	private RayBeamRenderer.FieldSourceSnap[] BuildSourceSnaps()
	{
		if (_sourceCache.Count <= 0)
		{
			return Array.Empty<RayBeamRenderer.FieldSourceSnap>();
		}

		var snaps = new RayBeamRenderer.FieldSourceSnap[_sourceCache.Count];
		for (int i = 0; i < _sourceCache.Count; i++)
		{
			snaps[i] = RayBeamRenderer.BuildFieldSourceSnap(_sourceCache[i]);
		}

		return snaps;
	}

	private Vector3 EvaluateIntegratedAcceleration(Vector3 p, float globalBeta, float globalGamma, RayBeamRenderer.FieldSourceSnap[] sourceSnaps)
	{
		if (sourceSnaps != null && sourceSnaps.Length > 0)
		{
			return RayBeamRenderer.ComputeAccelerationAtPointSnap(p, sourceSnaps, globalBeta, globalGamma, BendScale, FieldStrength);
		}

		return EvaluateIntegratedAccelerationFallback(p, globalBeta, globalGamma);
	}

	/// <summary>
	/// Debug-only evaluator that mirrors renderer profile behavior at a high level.
	/// </summary>
	private Vector3 EvaluateIntegratedAccelerationFallback(Vector3 p, float globalBeta, float globalGamma)
	{
		Vector3 sum = Vector3.Zero;

		foreach (FieldSource3D fs in _sourceCache)
		{
			RayBeamRenderer.FieldSourceSnap snap = RayBeamRenderer.BuildFieldSourceSnap(fs);
			if (!snap.Enabled)
			{
				continue;
			}

			Vector3 rvec = p - snap.Center;
			float rRaw = rvec.Length();
			float soft = Mathf.Max(0.00001f, snap.Softening);
			float r = Mathf.Sqrt(rRaw * rRaw + soft * soft);

			if (snap.MinRadius > 0f && r < snap.MinRadius)
			{
				continue;
			}
			if (snap.MaxRadius > 0f && r > snap.MaxRadius)
			{
				continue;
			}

			if (r < Epsilon)
			{
				continue;
			}

			Vector3 dir = (-rvec / r);
			if (!snap.Attract)
			{
				dir = -dir;
			}

			float gamma = snap.OverrideGamma ? snap.Gamma : globalGamma;
			float effectiveBeta = globalBeta;
			if (snap.OverrideBetaScale)
			{
				effectiveBeta = Math.Abs(globalBeta) > 1e-6f
					? globalBeta * snap.BetaScale
					: snap.BetaScale;
			}

			float amp = effectiveBeta * BendScale * FieldStrength * snap.Strength;
			float mag = 0f;

			switch (snap.Profile)
			{
				case 0:
					mag = amp * FastPow(r, gamma);
					break;

				case 1:
					mag = amp / FastPow(r, Mathf.Max(0.0001f, gamma));
					break;

				case 2:
				{
					float sigma = Mathf.Max(0.0001f, snap.Sigma);
					float x = r / sigma;
					mag = amp * Mathf.Exp(-x * x);
					break;
				}

				case 3:
				{
					float inner = Mathf.Max(0f, snap.InnerRadius);
					float outer = Mathf.Max(inner + 0.0001f, snap.OuterRadius);
					float edge = Mathf.Max(0.0001f, snap.EdgeSoftness);
					float wIn = SmoothStep(inner - edge, inner + edge, r);
					float wOut = 1.0f - SmoothStep(outer - edge, outer + edge, r);
					float w = Mathf.Clamp(wIn * wOut, 0.0f, 1.0f);
					mag = amp * w * FastPow(r, gamma);
					break;
				}
			}

			sum += dir * mag;
		}

		return sum;
	}

	private float ApproximateDivergence(Vector3 p, float globalBeta, float globalGamma, RayBeamRenderer.FieldSourceSnap[] sourceSnaps)
	{
		float h = Mathf.Max(0.001f, FiniteDifferenceStep);
		Vector3 hx = new Vector3(h, 0f, 0f);
		Vector3 hy = new Vector3(0f, h, 0f);
		Vector3 hz = new Vector3(0f, 0f, h);

		Vector3 px = EvaluateIntegratedAcceleration(p + hx, globalBeta, globalGamma, sourceSnaps);
		Vector3 nx = EvaluateIntegratedAcceleration(p - hx, globalBeta, globalGamma, sourceSnaps);
		Vector3 py = EvaluateIntegratedAcceleration(p + hy, globalBeta, globalGamma, sourceSnaps);
		Vector3 ny = EvaluateIntegratedAcceleration(p - hy, globalBeta, globalGamma, sourceSnaps);
		Vector3 pz = EvaluateIntegratedAcceleration(p + hz, globalBeta, globalGamma, sourceSnaps);
		Vector3 nz = EvaluateIntegratedAcceleration(p - hz, globalBeta, globalGamma, sourceSnaps);

		float dAxDx = (px.X - nx.X) / (2f * h);
		float dAyDy = (py.Y - ny.Y) / (2f * h);
		float dAzDz = (pz.Z - nz.Z) / (2f * h);
		return dAxDx + dAyDy + dAzDz;
	}

	private Vector3 ApproximateCurl(Vector3 p, float globalBeta, float globalGamma, RayBeamRenderer.FieldSourceSnap[] sourceSnaps)
	{
		float h = Mathf.Max(0.001f, FiniteDifferenceStep);
		Vector3 hx = new Vector3(h, 0f, 0f);
		Vector3 hy = new Vector3(0f, h, 0f);
		Vector3 hz = new Vector3(0f, 0f, h);

		Vector3 py = EvaluateIntegratedAcceleration(p + hy, globalBeta, globalGamma, sourceSnaps);
		Vector3 ny = EvaluateIntegratedAcceleration(p - hy, globalBeta, globalGamma, sourceSnaps);
		Vector3 pz = EvaluateIntegratedAcceleration(p + hz, globalBeta, globalGamma, sourceSnaps);
		Vector3 nz = EvaluateIntegratedAcceleration(p - hz, globalBeta, globalGamma, sourceSnaps);
		Vector3 px = EvaluateIntegratedAcceleration(p + hx, globalBeta, globalGamma, sourceSnaps);
		Vector3 nx = EvaluateIntegratedAcceleration(p - hx, globalBeta, globalGamma, sourceSnaps);

		float dFzDy = (py.Z - ny.Z) / (2f * h);
		float dFyDz = (pz.Y - nz.Y) / (2f * h);
		float dFxDz = (pz.X - nz.X) / (2f * h);
		float dFzDx = (px.Z - nx.Z) / (2f * h);
		float dFyDx = (px.Y - nx.Y) / (2f * h);
		float dFxDy = (py.X - ny.X) / (2f * h);

		return new Vector3(
			dFzDy - dFyDz,
			dFxDz - dFzDx,
			dFyDx - dFxDy);
	}

	private void EnsureVisualNodes()
	{
		if (_sphereInstance == null)
		{
			_sphereMesh = new SphereMesh
			{
				Radius = ProbeDrawRadius,
				Height = ProbeDrawRadius * 2f
			};

			_sphereMaterial = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = Colors.Cyan
			};

			_sphereInstance = new MeshInstance3D
			{
				Name = "_ProbeSphere",
				Mesh = _sphereMesh,
				MaterialOverride = _sphereMaterial,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};
			AddChild(_sphereInstance);
		}

		if (_lineInstance == null)
		{
			_lineMesh = new ImmediateMesh();
			_lineMaterial = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = Colors.Cyan,
				NoDepthTest = true,
				RenderPriority = 127
			};

			_lineInstance = new MeshInstance3D
			{
				Name = "_ProbeLines",
				Mesh = _lineMesh,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};
			AddChild(_lineInstance);
		}
	}

	private void FreeVisualNodes()
	{
		if (_lineInstance != null)
		{
			_lineInstance.QueueFree();
			_lineInstance = null;
			_lineMesh = null;
			_lineMaterial = null;
		}

		if (_sphereInstance != null)
		{
			_sphereInstance.QueueFree();
			_sphereInstance = null;
			_sphereMesh = null;
			_sphereMaterial = null;
		}
	}

	private void HideVisuals()
	{
		if (_sphereInstance != null)
		{
			_sphereInstance.Visible = false;
		}
		if (_lineInstance != null)
		{
			_lineInstance.Visible = false;
		}
	}

	private void UpdateVisuals()
	{
		if (_sphereInstance == null || _sphereMesh == null || _sphereMaterial == null || _lineInstance == null || _lineMesh == null || _lineMaterial == null)
		{
			return;
		}

		_sphereInstance.Visible = true;
		_lineInstance.Visible = true;

		float radius = Mathf.Max(0.02f, ProbeDrawRadius);
		_sphereMesh.Radius = radius;
		_sphereMesh.Height = radius * 2f;

		Color probeColor = EvaluateProbeColor(_lastMetricMagnitude);
		_sphereMaterial.AlbedoColor = probeColor;
		_sphereMaterial.Transparency = probeColor.A < 1f
			? BaseMaterial3D.TransparencyEnum.Alpha
			: BaseMaterial3D.TransparencyEnum.Disabled;

		_lineMaterial.AlbedoColor = probeColor;
		_lineMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

		_lineMesh.ClearSurfaces();
		_lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, _lineMaterial);

		if (ShowOrthogonalCrosshair)
		{
			float ringRadius = radius * 1.35f;
			AddRing(ringRadius, 24, Vector3.Right, Vector3.Up);
			AddRing(ringRadius, 24, Vector3.Right, Vector3.Forward);
			AddRing(ringRadius, 24, Vector3.Up, Vector3.Forward);
		}

		if (ShowVectorArrow)
		{
			Vector3 v = _lastModeVector;
			float len = v.Length();
			if (len > Epsilon)
			{
				Vector3 dir = v / len;
				float arrowLen = Mathf.Max(radius * 1.5f, radius * Mathf.Min(6.0f, 1.0f + len));
				Vector3 start = Vector3.Zero;
				Vector3 tip = dir * arrowLen;
				AddLine(start, tip);
				AddArrowHead(tip, dir, radius * 0.35f);
			}
		}

		_lineMesh.SurfaceEnd();
	}

	private void AddArrowHead(Vector3 tip, Vector3 dir, float headSize)
	{
		Vector3 fallbackUp = Mathf.Abs(dir.Dot(Vector3.Up)) > 0.9f ? Vector3.Right : Vector3.Up;
		Vector3 side = dir.Cross(fallbackUp).Normalized();
		Vector3 up = side.Cross(dir).Normalized();
		Vector3 basePoint = tip - dir * headSize;

		AddLine(tip, basePoint + side * (headSize * 0.6f));
		AddLine(tip, basePoint - side * (headSize * 0.6f));
		AddLine(tip, basePoint + up * (headSize * 0.6f));
		AddLine(tip, basePoint - up * (headSize * 0.6f));
	}

	private void AddRing(float radius, int segments, Vector3 axisA, Vector3 axisB)
	{
		int safeSegments = Mathf.Max(8, segments);
		for (int i = 0; i < safeSegments; i++)
		{
			float a0 = Mathf.Tau * i / safeSegments;
			float a1 = Mathf.Tau * (i + 1) / safeSegments;
			Vector3 p0 = axisA * (Mathf.Cos(a0) * radius) + axisB * (Mathf.Sin(a0) * radius);
			Vector3 p1 = axisA * (Mathf.Cos(a1) * radius) + axisB * (Mathf.Sin(a1) * radius);
			AddLine(p0, p1);
		}
	}

	private void AddLine(Vector3 a, Vector3 b)
	{
		_lineMesh.SurfaceAddVertex(a);
		_lineMesh.SurfaceAddVertex(b);
	}

	private void AddNumericOverlay()
	{
		if (!ShowNumericOverlay)
		{
			return;
		}

		var viewport = GetViewport();
		if (viewport == null)
		{
			return;
		}

		var cam = viewport.GetCamera3D();
		if (cam == null || !IsInstanceValid(cam))
		{
			return;
		}

		GdVector2 screenPos = cam.UnprojectPosition(GlobalPosition);
		Color color = EvaluateProbeColor(_lastMetricMagnitude);

		const float crossHalf = 4f;
		DebugOverlayBus.AddLine(
			screenPos + new GdVector2(-crossHalf, 0f),
			screenPos + new GdVector2(crossHalf, 0f),
			color,
			1f);
		DebugOverlayBus.AddLine(
			screenPos + new GdVector2(0f, -crossHalf),
			screenPos + new GdVector2(0f, crossHalf),
			color,
			1f);

		string detail = ProbeMode switch
		{
			ProbeModeKind.DivergenceApprox => $"div={_lastDivergence:0.000}",
			ProbeModeKind.CurlApprox => $"curl=({_lastCurl.X:0.000},{_lastCurl.Y:0.000},{_lastCurl.Z:0.000})",
			ProbeModeKind.AccelVector => $"a=({_lastAccel.X:0.000},{_lastAccel.Y:0.000},{_lastAccel.Z:0.000})",
			_ => $"|a|={_lastAccel.Length():0.000}"
		};

		string firstSummary = _sourceCache.Count > 0 ? _sourceCache[0].EffectiveSummary : "source=none";
		string text = $"mode={ProbeMode} sources={_lastSourceCount} metric={_lastMetricMagnitude:0.000} {detail} {firstSummary}";
		DebugOverlayBus.AddText(screenPos + new GdVector2(6f, -6f), text, Colors.White);
	}

	private void ApplyFreeFly(double delta)
	{
		Vector3 move = Vector3.Zero;
		if (Input.IsKeyPressed(Key.W)) move -= Transform.Basis.Z;
		if (Input.IsKeyPressed(Key.S)) move += Transform.Basis.Z;
		if (Input.IsKeyPressed(Key.A)) move -= Transform.Basis.X;
		if (Input.IsKeyPressed(Key.D)) move += Transform.Basis.X;
		if (Input.IsKeyPressed(Key.E)) move += Transform.Basis.Y;
		if (Input.IsKeyPressed(Key.Q)) move -= Transform.Basis.Y;

		if (move.LengthSquared() < Epsilon)
		{
			return;
		}

		float speed = FreeFlySpeed;
		if (Input.IsKeyPressed(Key.Shift))
		{
			speed *= FreeFlyBoostMultiplier;
		}

		GlobalPosition += move.Normalized() * speed * (float)delta;
	}

	private void MaybePrint()
	{
		if (!Print)
		{
			return;
		}

		double now = Time.GetTicksMsec() * 0.001;
		if (now < _nextPrintTimeSec)
		{
			return;
		}

		_nextPrintTimeSec = now + Math.Max(0.05, PrintIntervalSec);
		GD.Print(
			$"[FieldProbe] pos=({GlobalPosition.X:0.###},{GlobalPosition.Y:0.###},{GlobalPosition.Z:0.###}) " +
			$"mode={ProbeMode} metric={_lastMetricMagnitude:0.###} accel=({_lastAccel.X:0.###},{_lastAccel.Y:0.###},{_lastAccel.Z:0.###}) " +
			$"sources={_lastSourceCount}");
	}

	private void MaybeProbeReadLog()
	{
		if (!DebugLogConfig.EnableProbeLog)
		{
			return;
		}

		double now = Time.GetTicksMsec() * 0.001;
		if (now < _nextProbeLogTimeSec)
		{
			return;
		}

		_nextProbeLogTimeSec = now + Math.Max(0.05, DebugLogConfig.ProbeLogIntervalSec);
		GD.Print($"[PROBE READ] mode={ProbeMode} metric={_lastMetricMagnitude:0.###} sources={_lastSourceCount}");
	}

	private Color EvaluateProbeColor(float metricMagnitude)
	{
		float t = 1f - Mathf.Exp(-Mathf.Max(0f, metricMagnitude));
		t = Mathf.Clamp(t, 0f, 1f);

		if (ProbeColorRamp != null)
		{
			return ProbeColorRamp.Sample(t);
		}

		return Color.FromHsv(Mathf.Lerp(0.62f, 0.02f, t), 0.85f, 1.0f);
	}

	private static float SmoothStep(float a, float b, float x)
	{
		float t = Mathf.Clamp((x - a) / Mathf.Max(Epsilon, b - a), 0.0f, 1.0f);
		return t * t * (3.0f - 2.0f * t);
	}

	private static float FastPow(float r, float gamma)
	{
		if (gamma == -2f) return 1f / (r * r);
		if (gamma == -1f) return 1f / r;
		if (gamma == 0f) return 1f;
		if (gamma == 1f) return r;
		if (gamma == 2f) return r * r;
		return Mathf.Pow(r, gamma);
	}

	private static bool TryReadFloat(Node obj, StringName prop, out float value)
	{
		value = 0f;
		if (obj == null)
		{
			return false;
		}

		Variant v = obj.Get(prop);
		switch (v.VariantType)
		{
			case Variant.Type.Float:
				value = (float)v;
				return true;
			case Variant.Type.Int:
				value = (int)v;
				return true;
			default:
				return false;
		}
	}
}
