using Godot;
using System;
using System.Collections.Generic;
using RendererCore.Common;
using RendererCore.Fields;
using GdVector2 = Godot.Vector2;

/// <summary>
/// Academic baseline: uses FieldSource3D canonical params first; legacy only via FieldSource3D shim.
/// Integrated field probe for debug sampling and optional unified source-ring visualization.
/// PR: Academic canonical radial model: u-clamped shell with profile f(u).
/// </summary>
[Tool]
public partial class FieldProbe3D : Node3D
{
	public enum ProbeModeKind
	{
		IntegratedSample = 0,
		SourceVizOnly = 1,
		Both = 2
	}

	private const float Epsilon = 1e-6f;
	private const float DefaultSourceRefreshIntervalSec = 0.5f;

	[ExportGroup("Probe")]
	[Export] public bool ProbeEnabled { get; set; } = true;
	[Export] public ProbeModeKind ProbeMode { get; set; } = ProbeModeKind.IntegratedSample;
	[Export] public bool UpdateEveryFrame { get; set; } = false;
	[Export(PropertyHint.Range, "1,240,1")] public float SampleRateHz { get; set; } = 20f;
	[Export(PropertyHint.Range, "0.001,2.0,0.001")] public float FiniteDifferenceStep { get; set; } = 0.1f;
	[Export(PropertyHint.Range, "0.0,5000.0,0.1")] public float SourceFilterRadius { get; set; } = 0f;
	[Export] public bool DrawSourceRingsFromProbe { get; set; } = false;
	[Export] public bool OverlayEnabled { get; set; } = true;

	[ExportGroup("Debug Viz")]
	[Export] public bool DebugVizEnabled { get; set; } = true;
	[Export(PropertyHint.Flags, "XY,XZ,YZ")]
	public int DebugVizPlanes
	{
		get => _debugVizPlanes;
		set => _debugVizPlanes = value & (int)FieldSource3D.DebugVizPlaneFlags.All;
	}
	[Export] public FieldSource3D.DebugVizOpacityModeKind DebugVizOpacityMode { get; set; } = FieldSource3D.DebugVizOpacityModeKind.Wireframe;
	[Export(PropertyHint.Range, "8,256,1")] public int RingSegments { get; set; } = 64;
	[Export(PropertyHint.Range, "0.25,8.0,0.05")] public float LineWidth { get; set; } = 2.0f;
	[Export] public bool AlwaysOnTop { get; set; } = true;
	[Export] public bool InGame { get; set; } = false;
	[Export] public bool ShowInnerOuter { get; set; } = true;
	[Export] public bool ShowSigma { get; set; } = false;
	[Export] public bool UsePartialOpacityRings { get; set; } = true;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float GlobalOpacity { get; set; } = 1.0f;
	[Export] public Color DebugVizColorInner { get; set; } = new Color(0.1f, 0.9f, 0.9f, 1.0f);
	[Export] public Color DebugVizColorOuter { get; set; } = new Color(0.15f, 0.95f, 0.35f, 1.0f);
	[Export] public Color DebugVizColorSigma { get; set; } = new Color(1.0f, 0.85f, 0.25f, 1.0f);

	[ExportGroup("Probe Readout")]
	[Export] public float LastIntegratedMagnitude { get; private set; }
	[Export] public float LastGradientMagnitude { get; private set; }
	[Export] public Vector3 LastGradient { get; private set; }
	[Export] public int LastSourceCount { get; private set; }
	[Export] public float LastNearestSourceDistance { get; private set; }

	private readonly List<FieldSource3D> _sourceCache = new List<FieldSource3D>(32);
	private readonly List<SourceRingInfo> _currentRingInfos = new List<SourceRingInfo>(64);
	private int _debugVizPlanes = (int)FieldSource3D.DebugVizPlaneFlags.All;

	private MeshInstance3D _debugMeshInstance;
	private ImmediateMesh _debugMesh;
	private double _sampleAccumulatorSec;
	private double _sourceRefreshAccumulatorSec;
	private ulong _lastVizSignature;
	private bool _hasVizSignature;
	private bool _hasSample;

	private float _lastIntegratedMagnitude;
	private Vector3 _lastIntegratedAcceleration;
	private Vector3 _lastGradient;
	private int _lastSourceCount;
	private float _lastNearestSourceDistance;
	private ProbeOverlayTerm _lastOverlayTerm;

	public override void _Ready()
	{
		SetProcess(true);
		RefreshSourceCache();
		EnsureDebugDraw();
		GD.Print("[FieldProbe3D] canonical probe sampling uses FieldMath; transport interpretation is selected downstream in renderer integration.");
	}

	public override void _ExitTree()
	{
		ClearDebugDraw();
	}

	public override void _Process(double delta)
	{
		if (!ProbeEnabled)
		{
			HideDebugDraw();
			return;
		}

		UpdateSourceCacheClock(delta);

		if (NeedsSampling())
		{
			if (UpdateEveryFrame)
			{
				SampleIntegratedField();
			}
			else
			{
				float safeRate = Mathf.Max(1f, SampleRateHz);
				double interval = 1.0 / safeRate;
				_sampleAccumulatorSec += delta;
				if (_sampleAccumulatorSec >= interval)
				{
					_sampleAccumulatorSec = 0.0;
					SampleIntegratedField();
				}
			}
		}

		if (OverlayEnabled && _hasSample)
		{
			AddOverlay();
		}

		UpdateDebugViz();
	}

	private bool NeedsSampling()
	{
		return ProbeMode == ProbeModeKind.IntegratedSample || ProbeMode == ProbeModeKind.Both;
	}

	private bool NeedsSourceViz()
	{
		if (ProbeMode == ProbeModeKind.IntegratedSample)
		{
			return false;
		}

		return DrawSourceRingsFromProbe;
	}

	private void UpdateSourceCacheClock(double delta)
	{
		_sourceRefreshAccumulatorSec += delta;
		if (_sourceRefreshAccumulatorSec < DefaultSourceRefreshIntervalSec)
		{
			return;
		}

		_sourceRefreshAccumulatorSec = 0.0;
		RefreshSourceCache();
	}

	private void RefreshSourceCache()
	{
		_sourceCache.Clear();
		SceneTree tree = GetTree();
		if (tree == null)
		{
			return;
		}

		Godot.Collections.Array<Node> nodes = tree.GetNodesInGroup("field_sources");
		if (nodes == null)
		{
			return;
		}

		foreach (Node node in nodes)
		{
			if (node is FieldSource3D source && IsInstanceValid(source))
			{
				_sourceCache.Add(source);
			}
		}
	}

	private void ResolveGlobalBetaGamma(out float beta, out float gamma)
	{
		beta = 0f;
		gamma = 2f;
		Viewport viewport = GetViewport();
		if (viewport == null)
		{
			return;
		}

		Camera3D camera = viewport.GetCamera3D();
		if (camera == null || !IsInstanceValid(camera))
		{
			return;
		}

		beta = ReadFloat(camera, "Beta", 0f);
		gamma = ReadFloat(camera, "Gamma", 2f);
	}

	private static float ReadFloat(Node obj, StringName prop, float fallback)
	{
		if (obj == null)
		{
			return fallback;
		}

		Variant value = obj.Get(prop);
		return value.VariantType switch
		{
			Variant.Type.Float => (float)value,
			Variant.Type.Int => (int)value,
			_ => fallback
		};
	}

	private void SampleIntegratedField()
	{
		if (_sourceCache.Count == 0)
		{
			RefreshSourceCache();
		}

		ResolveGlobalBetaGamma(out float globalBeta, out float globalGamma);
		Vector3 p = GlobalPosition;
		ScalarSample sample = EvaluateScalarAt(p, globalBeta, globalGamma, captureOverlayTerm: true);
		_lastIntegratedAcceleration = sample.Acceleration;
		_lastIntegratedMagnitude = _lastIntegratedAcceleration.Length();
		_lastSourceCount = sample.ConsideredSources;
		_lastNearestSourceDistance = sample.NearestDistance;
		_lastOverlayTerm = sample.OverlayTerm;
		_lastGradient = EvaluateGradientProxy(p, globalBeta, globalGamma);
		LastIntegratedMagnitude = _lastIntegratedMagnitude;
		LastSourceCount = _lastSourceCount;
		LastNearestSourceDistance = _lastNearestSourceDistance;
		LastGradient = _lastGradient;
		LastGradientMagnitude = _lastGradient.Length();
		_hasSample = true;
	}

	private Vector3 EvaluateGradientProxy(Vector3 p, float globalBeta, float globalGamma)
	{
		float h = Mathf.Max(0.001f, FiniteDifferenceStep);
		Vector3 hx = new Vector3(h, 0f, 0f);
		Vector3 hy = new Vector3(0f, h, 0f);
		Vector3 hz = new Vector3(0f, 0f, h);

		float fxp = EvaluateScalarAt(p + hx, globalBeta, globalGamma, captureOverlayTerm: false).Scalar;
		float fxn = EvaluateScalarAt(p - hx, globalBeta, globalGamma, captureOverlayTerm: false).Scalar;
		float fyp = EvaluateScalarAt(p + hy, globalBeta, globalGamma, captureOverlayTerm: false).Scalar;
		float fyn = EvaluateScalarAt(p - hy, globalBeta, globalGamma, captureOverlayTerm: false).Scalar;
		float fzp = EvaluateScalarAt(p + hz, globalBeta, globalGamma, captureOverlayTerm: false).Scalar;
		float fzn = EvaluateScalarAt(p - hz, globalBeta, globalGamma, captureOverlayTerm: false).Scalar;

		float inv2h = 1f / (2f * h);
		return new Vector3(
			(fxp - fxn) * inv2h,
			(fyp - fyn) * inv2h,
			(fzp - fzn) * inv2h);
	}

	private ScalarSample EvaluateScalarAt(Vector3 samplePosition, float globalBeta, float globalGamma, bool captureOverlayTerm)
	{
		_ = globalGamma;
		if (_sourceCache.Count == 0)
		{
			return ScalarSample.Empty;
		}

		Vector3 accelSum = Vector3.Zero;
		int considered = 0;
		float nearest = float.PositiveInfinity;
		ProbeOverlayTerm strongestTerm = ProbeOverlayTerm.Empty;
		float strongestAccel = 0f;

		foreach (FieldSource3D source in _sourceCache)
		{
			if (source == null || !IsInstanceValid(source) || !source.Enabled)
			{
				continue;
			}

			try
			{
				RayBeamRenderer.FieldSourceSnap snap = RayBeamRenderer.BuildFieldSourceSnap(source);
				if (!snap.Enabled)
				{
					continue;
				}

				float r = samplePosition.DistanceTo(snap.Center);

				considered++;
				nearest = Mathf.Min(nearest, r);

				// Probe stays transport-agnostic: canonical FieldMath only.
				FieldMath.EvalResult eval = FieldMath.EvalFieldAccel(
					samplePosition,
					snap.Center,
					snap.CurveType,
					snap.RInner,
					snap.ROuter,
					snap.Amp,
					snap.CurveA,
					snap.Sigma,
					snap.CurveA,
					snap.CurveB,
					snap.CurveC,
					snap.CustomCurve,
					snap.ModeFlags,
					globalBeta,
					snap.OverrideBetaScale,
					snap.BetaScale,
					snap.EdgeSoftness);

				accelSum += eval.Acceleration;

				float accelMag = eval.AccelerationMagnitude;
				if (captureOverlayTerm && accelMag >= strongestAccel)
				{
					strongestAccel = accelMag;
					strongestTerm = new ProbeOverlayTerm
					{
						Valid = true,
						SourceName = source.Name.ToString(),
						R = eval.R,
						U = eval.U,
						Profile = eval.ProfileWithEdge,
						BetaEff = eval.BetaEff,
						Gamma = eval.Gamma,
						Sigma = eval.Sigma,
						FinalMag = eval.Magnitude,
						AccelerationMagnitude = accelMag
					};
				}
			}
			catch
			{
				// Keep probe robust in tool mode if a source is transiently invalid.
			}
		}

		if (!float.IsFinite(nearest))
		{
			nearest = -1f;
		}

		return new ScalarSample
		{
			Scalar = accelSum.Length(),
			Acceleration = accelSum,
			ConsideredSources = considered,
			NearestDistance = nearest,
			OverlayTerm = strongestTerm
		};
	}

	private void UpdateDebugViz()
	{
		if (!ShouldDrawDebugViz())
		{
			HideDebugDraw();
			return;
		}

		EnsureDebugDraw();
		GatherRingInfos();
		ulong signature = BuildVizSignature();
		if (!_hasVizSignature || signature != _lastVizSignature)
		{
			RebuildDebugMesh();
			_lastVizSignature = signature;
			_hasVizSignature = true;
		}

		if (_debugMeshInstance != null)
		{
			_debugMeshInstance.Visible = true;
		}
	}

	private bool ShouldDrawDebugViz()
	{
		if (!DebugVizEnabled)
		{
			return false;
		}

		if (!Engine.IsEditorHint() && !InGame)
		{
			return false;
		}

		return true;
	}

	private void GatherRingInfos()
	{
		_currentRingInfos.Clear();

		if (!NeedsSourceViz())
		{
			return;
		}

		foreach (FieldSource3D source in _sourceCache)
		{
			if (source == null || !IsInstanceValid(source) || !source.Enabled)
			{
				continue;
			}

			try
			{
				RayBeamRenderer.FieldSourceSnap snap = RayBeamRenderer.BuildFieldSourceSnap(source);
				if (!snap.Enabled)
				{
					continue;
				}

				Vector3 sourcePos = snap.Center;
				float dist = GlobalPosition.DistanceTo(sourcePos);
				if (SourceFilterRadius > Epsilon && dist > SourceFilterRadius)
				{
					continue;
				}

				Vector3 center = ToLocal(sourcePos);
				Vector3 axisX = ToLocalDirectionSafe(source, Vector3.Right, center);
				Vector3 axisY = ToLocalDirectionSafe(source, Vector3.Up, center);
				Vector3 axisZ = ToLocalDirectionSafe(source, Vector3.Forward, center);

				_currentRingInfos.Add(new SourceRingInfo
				{
					Center = center,
					AxisX = axisX,
					AxisY = axisY,
					AxisZ = axisZ,
					HasInnerOuter = true,
					InnerRadius = Mathf.Max(0f, Mathf.Min(snap.RInner, snap.ROuter)),
					OuterRadius = Mathf.Max(0f, Mathf.Max(snap.RInner, snap.ROuter)),
					HasSigma = ShowSigma && FieldMath.IsSigmaMeaningful(snap.CurveType) && snap.Sigma > Epsilon,
					SigmaRadius = Mathf.Max(0f, snap.Sigma)
				});
			}
			catch
			{
				// Skip transient invalid source in tool mode.
			}
		}
	}

	private Vector3 ToLocalDirectionSafe(FieldSource3D source, Vector3 basisAxis, Vector3 centerLocal)
	{
		Vector3 worldAxis;
		if (basisAxis == Vector3.Right)
		{
			worldAxis = source.GlobalTransform.Basis.X;
		}
		else if (basisAxis == Vector3.Up)
		{
			worldAxis = source.GlobalTransform.Basis.Y;
		}
		else
		{
			worldAxis = source.GlobalTransform.Basis.Z;
		}

		if (worldAxis.LengthSquared() < Epsilon)
		{
			worldAxis = basisAxis;
		}

		Vector3 sampleWorld = source.GlobalPosition + worldAxis.Normalized();
		Vector3 local = ToLocal(sampleWorld) - centerLocal;
		if (local.LengthSquared() < Epsilon)
		{
			return basisAxis;
		}

		return local.Normalized();
	}

	private ulong BuildVizSignature()
	{
		HashCode hash = new HashCode();
		hash.Add((int)ProbeMode);
		hash.Add(NeedsSourceViz());
		hash.Add(_debugVizPlanes);
		hash.Add((int)DebugVizOpacityMode);
		hash.Add(Mathf.Max(8, RingSegments));
		hash.Add(Mathf.Max(0.25f, LineWidth));
		hash.Add(AlwaysOnTop);
		hash.Add(InGame);
		hash.Add(ShowInnerOuter);
		hash.Add(ShowSigma);
		hash.Add(UsePartialOpacityRings);
		hash.Add(Mathf.Clamp(GlobalOpacity, 0f, 1f));
		hash.Add(Quantize(GlobalPosition.X));
		hash.Add(Quantize(GlobalPosition.Y));
		hash.Add(Quantize(GlobalPosition.Z));
		hash.Add(_currentRingInfos.Count);

		for (int i = 0; i < _currentRingInfos.Count; i++)
		{
			_currentRingInfos[i].AddToHash(ref hash);
		}

		return unchecked((ulong)hash.ToHashCode());
	}

	private static int Quantize(float value)
	{
		return Mathf.RoundToInt(value * 1000f);
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
			Name = "_FieldProbeAcademicViz",
			Mesh = _debugMesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		AddChild(_debugMeshInstance);
	}

	private void HideDebugDraw()
	{
		if (_debugMeshInstance != null)
		{
			_debugMeshInstance.Visible = false;
		}
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
		_hasVizSignature = false;
	}

	private void RebuildDebugMesh()
	{
		if (_debugMesh == null)
		{
			return;
		}

		_debugMesh.ClearSurfaces();

		float markerSize = Mathf.Max(0.025f, 0.05f * Mathf.Max(0.25f, LineWidth));
		Color markerColor = new Color(0.95f, 0.95f, 0.95f, ApplyLineOpacityAlpha(1f));
		AddLineSurface(markerColor, () =>
		{
			AddLine(Vector3.Left * markerSize, Vector3.Right * markerSize);
			AddLine(Vector3.Down * markerSize, Vector3.Up * markerSize);
			AddLine(Vector3.Back * markerSize, Vector3.Forward * markerSize);
		});

		if (!NeedsSourceViz())
		{
			return;
		}

		for (int i = 0; i < _currentRingInfos.Count; i++)
		{
			SourceRingInfo info = _currentRingInfos[i];
			if (DebugVizOpacityMode != FieldSource3D.DebugVizOpacityModeKind.Wireframe)
			{
				if (ShowInnerOuter && info.HasInnerOuter)
				{
					AddFilledRingPlanesForSource(info, info.InnerRadius, DebugVizColorInner);
					AddFilledRingPlanesForSource(info, info.OuterRadius, DebugVizColorOuter);
				}
				if (ShowSigma && info.HasSigma)
				{
					AddFilledRingPlanesForSource(info, info.SigmaRadius, DebugVizColorSigma);
				}
			}

			if (ShowInnerOuter && info.HasInnerOuter)
			{
				AddRingPlanesForSource(info, info.InnerRadius, DebugVizColorInner, dashed: false);
				AddRingPlanesForSource(info, info.OuterRadius, DebugVizColorOuter, dashed: false);
			}

			if (ShowSigma && info.HasSigma)
			{
				AddRingPlanesForSource(info, info.SigmaRadius, DebugVizColorSigma, dashed: true);
			}
		}
	}

	private void AddFilledRingPlanesForSource(SourceRingInfo info, float radius, Color baseColor)
	{
		if (radius <= Epsilon)
		{
			return;
		}

		Color color = ApplyFillOpacity(baseColor);
		float thickness = Mathf.Max(0.01f, 0.01f * Mathf.Max(0.25f, LineWidth));
		AddTriangleSurface(color, () =>
		{
			FieldSource3D.DebugVizPlaneFlags planeMask = (FieldSource3D.DebugVizPlaneFlags)_debugVizPlanes;
			if ((planeMask & FieldSource3D.DebugVizPlaneFlags.XY) != 0)
			{
				AddFilledRing(info.Center, radius, thickness, Mathf.Max(8, RingSegments), info.AxisX, info.AxisY);
			}
			if ((planeMask & FieldSource3D.DebugVizPlaneFlags.XZ) != 0)
			{
				AddFilledRing(info.Center, radius, thickness, Mathf.Max(8, RingSegments), info.AxisX, info.AxisZ);
			}
			if ((planeMask & FieldSource3D.DebugVizPlaneFlags.YZ) != 0)
			{
				AddFilledRing(info.Center, radius, thickness, Mathf.Max(8, RingSegments), info.AxisY, info.AxisZ);
			}
		});
	}

	private void AddRingPlanesForSource(SourceRingInfo info, float radius, Color baseColor, bool dashed)
	{
		if (radius <= Epsilon)
		{
			return;
		}

		Color color = ApplyLineOpacity(baseColor);
		AddLineSurface(color, () =>
		{
			FieldSource3D.DebugVizPlaneFlags planeMask = (FieldSource3D.DebugVizPlaneFlags)_debugVizPlanes;
			if ((planeMask & FieldSource3D.DebugVizPlaneFlags.XY) != 0)
			{
				AddCircle(info.Center, radius, Mathf.Max(8, RingSegments), info.AxisX, info.AxisY, dashed);
			}
			if ((planeMask & FieldSource3D.DebugVizPlaneFlags.XZ) != 0)
			{
				AddCircle(info.Center, radius, Mathf.Max(8, RingSegments), info.AxisX, info.AxisZ, dashed);
			}
			if ((planeMask & FieldSource3D.DebugVizPlaneFlags.YZ) != 0)
			{
				AddCircle(info.Center, radius, Mathf.Max(8, RingSegments), info.AxisY, info.AxisZ, dashed);
			}
		});
	}

	private void AddCircle(Vector3 center, float radius, int segments, Vector3 axisA, Vector3 axisB, bool dashed)
	{
		int step = dashed ? 2 : 1;
		for (int i = 0; i < segments; i += step)
		{
			float a0 = Mathf.Tau * i / segments;
			float a1 = Mathf.Tau * (i + 1) / segments;
			Vector3 p0 = center + axisA * (Mathf.Cos(a0) * radius) + axisB * (Mathf.Sin(a0) * radius);
			Vector3 p1 = center + axisA * (Mathf.Cos(a1) * radius) + axisB * (Mathf.Sin(a1) * radius);
			AddLine(p0, p1);
		}
	}

	private void AddFilledRing(Vector3 center, float radius, float thickness, int segments, Vector3 axisA, Vector3 axisB)
	{
		int safeSegments = Mathf.Max(8, segments);
		float halfT = 0.5f * thickness;
		float inner = Mathf.Max(0.0001f, radius - halfT);
		float outer = radius + halfT;

		for (int i = 0; i < safeSegments; i++)
		{
			float a0 = Mathf.Tau * i / safeSegments;
			float a1 = Mathf.Tau * (i + 1) / safeSegments;

			Vector3 in0 = center + axisA * (Mathf.Cos(a0) * inner) + axisB * (Mathf.Sin(a0) * inner);
			Vector3 out0 = center + axisA * (Mathf.Cos(a0) * outer) + axisB * (Mathf.Sin(a0) * outer);
			Vector3 in1 = center + axisA * (Mathf.Cos(a1) * inner) + axisB * (Mathf.Sin(a1) * inner);
			Vector3 out1 = center + axisA * (Mathf.Cos(a1) * outer) + axisB * (Mathf.Sin(a1) * outer);

			_debugMesh.SurfaceAddVertex(in0);
			_debugMesh.SurfaceAddVertex(out0);
			_debugMesh.SurfaceAddVertex(out1);

			_debugMesh.SurfaceAddVertex(in0);
			_debugMesh.SurfaceAddVertex(out1);
			_debugMesh.SurfaceAddVertex(in1);
		}
	}

	private void AddLineSurface(Color color, Action emitGeometry)
	{
		if (_debugMesh == null)
		{
			return;
		}

		StandardMaterial3D material = CreateLineMaterial(color);
		_debugMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
		emitGeometry();
		_debugMesh.SurfaceEnd();
	}

	private void AddTriangleSurface(Color color, Action emitGeometry)
	{
		if (_debugMesh == null)
		{
			return;
		}

		StandardMaterial3D material = CreateFillMaterial(color);
		_debugMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
		emitGeometry();
		_debugMesh.SurfaceEnd();
	}

	private StandardMaterial3D CreateLineMaterial(Color color)
	{
		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = color,
			NoDepthTest = AlwaysOnTop,
			RenderPriority = AlwaysOnTop ? 127 : 0,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

		if (color.A < 1f)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		}

		return material;
	}

	private StandardMaterial3D CreateFillMaterial(Color color)
	{
		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = color,
			NoDepthTest = AlwaysOnTop,
			RenderPriority = AlwaysOnTop ? 127 : 0,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

		if (color.A < 1f)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		}

		return material;
	}

	private void AddLine(Vector3 start, Vector3 end)
	{
		_debugMesh?.SurfaceAddVertex(start);
		_debugMesh?.SurfaceAddVertex(end);
	}

	private Color ApplyLineOpacity(Color baseColor)
	{
		float alpha = ApplyLineOpacityAlpha(baseColor.A);
		return new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
	}

	private Color ApplyFillOpacity(Color baseColor)
	{
		float alpha = ApplyFillOpacityAlpha(baseColor.A);
		return new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
	}

	private float ApplyLineOpacityAlpha(float baseAlpha)
	{
		float clampedGlobal = UsePartialOpacityRings ? Mathf.Clamp(GlobalOpacity, 0f, 1f) : 1f;
		float modeAlpha = DebugVizOpacityMode switch
		{
			FieldSource3D.DebugVizOpacityModeKind.Wireframe => 1.0f,
			FieldSource3D.DebugVizOpacityModeKind.Ghosted => 0.9f,
			_ => 1.0f
		};
		return Mathf.Clamp(baseAlpha * modeAlpha * clampedGlobal, 0f, 1f);
	}

	private float ApplyFillOpacityAlpha(float baseAlpha)
	{
		float clampedGlobal = UsePartialOpacityRings ? Mathf.Clamp(GlobalOpacity, 0f, 1f) : 1f;
		float modeAlpha = DebugVizOpacityMode switch
		{
			FieldSource3D.DebugVizOpacityModeKind.Ghosted => 0.22f,
			FieldSource3D.DebugVizOpacityModeKind.Solid => 0.9f,
			_ => 0f
		};
		return Mathf.Clamp(baseAlpha * modeAlpha * clampedGlobal, 0f, 1f);
	}

	private void AddOverlay()
	{
		Viewport viewport = GetViewport();
		if (viewport == null)
		{
			return;
		}

		Camera3D camera = viewport.GetCamera3D();
		if (camera == null || !IsInstanceValid(camera))
		{
			return;
		}

		GdVector2 screenPos = camera.UnprojectPosition(GlobalPosition);
		Color color = ApplyLineOpacity(new Color(0.95f, 0.95f, 0.95f, 1f));

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

		string nearestText = _lastNearestSourceDistance >= 0f
			? $"{_lastNearestSourceDistance:0.###}"
			: "n/a";
		string termText = _lastOverlayTerm.Valid
			? $"r={_lastOverlayTerm.R:0.###} u={_lastOverlayTerm.U:0.###} f(u)={_lastOverlayTerm.Profile:0.###} beta_eff={_lastOverlayTerm.BetaEff:0.###} gamma={_lastOverlayTerm.Gamma:0.###} sigma={_lastOverlayTerm.Sigma:0.###} final_mag={_lastOverlayTerm.FinalMag:0.###} |a|={_lastOverlayTerm.AccelerationMagnitude:0.###}"
			: "r=n/a u=n/a f(u)=n/a beta_eff=n/a gamma=n/a sigma=n/a final_mag=n/a |a|=n/a";
		string text =
			$"probe={ProbeMode} |a_sum|={_lastIntegratedMagnitude:0.###} grad={_lastGradient.Length():0.###} sources={_lastSourceCount} nearest={nearestText}\n" +
			$"dominant={_lastOverlayTerm.SourceName} {termText}";

		DebugOverlayBus.AddText(screenPos + new GdVector2(6f, -6f), text, Colors.White);
	}

	private struct ScalarSample
	{
		public float Scalar;
		public Vector3 Acceleration;
		public int ConsideredSources;
		public float NearestDistance;
		public ProbeOverlayTerm OverlayTerm;

		public static ScalarSample Empty => new ScalarSample
		{
			Scalar = 0f,
			Acceleration = Vector3.Zero,
			ConsideredSources = 0,
			NearestDistance = -1f,
			OverlayTerm = ProbeOverlayTerm.Empty
		};
	}

	private struct ProbeOverlayTerm
	{
		public bool Valid;
		public string SourceName;
		public float R;
		public float U;
		public float Profile;
		public float BetaEff;
		public float Gamma;
		public float Sigma;
		public float FinalMag;
		public float AccelerationMagnitude;

		public static ProbeOverlayTerm Empty => new ProbeOverlayTerm
		{
			Valid = false,
			SourceName = "n/a",
			R = 0f,
			U = 0f,
			Profile = 0f,
			BetaEff = 0f,
			Gamma = 0f,
			Sigma = 0f,
			FinalMag = 0f,
			AccelerationMagnitude = 0f
		};
	}

	private struct SourceRingInfo
	{
		public Vector3 Center;
		public Vector3 AxisX;
		public Vector3 AxisY;
		public Vector3 AxisZ;
		public bool HasInnerOuter;
		public float InnerRadius;
		public float OuterRadius;
		public bool HasSigma;
		public float SigmaRadius;

		public void AddToHash(ref HashCode hash)
		{
			hash.Add(Quantize(Center.X));
			hash.Add(Quantize(Center.Y));
			hash.Add(Quantize(Center.Z));
			hash.Add(Quantize(AxisX.X));
			hash.Add(Quantize(AxisX.Y));
			hash.Add(Quantize(AxisX.Z));
			hash.Add(Quantize(AxisY.X));
			hash.Add(Quantize(AxisY.Y));
			hash.Add(Quantize(AxisY.Z));
			hash.Add(Quantize(AxisZ.X));
			hash.Add(Quantize(AxisZ.Y));
			hash.Add(Quantize(AxisZ.Z));
			hash.Add(HasInnerOuter);
			hash.Add(Quantize(InnerRadius));
			hash.Add(Quantize(OuterRadius));
			hash.Add(HasSigma);
			hash.Add(Quantize(SigmaRadius));
		}
	}
}
