using Godot;
using System;
using System.Numerics;
using System.Reflection;
using GodotAdapter;
using RendererCore.Common;
using RendererCore.Fields;
using RendererCore.Integrators;
using RendererCore.SceneSnapshot;
using GdVector2 = Godot.Vector2;
using GdVector3 = Godot.Vector3;
using NumVector3 = System.Numerics.Vector3;

public partial class FieldProbe3D : Node3D
{
	[Export] public float LineScale = 1.0f;
	[Export] public bool Print = false;
	[Export] public float PrintIntervalSec = 0.25f;
	[Export] public float EpsPos = 0.01f;
	[Export] public float DtMin = 0.001f;
	[Export] public float DtMax = 0.05f;

	private const float Epsilon = 1e-6f;
	private double _nextProbeLogTimeSec;

	public override void _Process(double delta)
	{
		RendererCore.SceneSnapshot.SceneSnapshot snapshot;
		var busHas = FrameSnapshotBus.HasSnapshot && FrameSnapshotBus.CurrentSnapshot != null;
		var busFrameId = FrameSnapshotBus.FrameId;
		if (busHas)
		{
			snapshot = FrameSnapshotBus.CurrentSnapshot;
		}
		else
		{
			snapshot = SnapshotBuilder.BuildFromGodotScene(GetTree()?.CurrentScene);
		}
		var accel = FieldSystem.AccelAt(ToNumerics(GlobalPosition), snapshot);

		var accelMagnitude = accel.Length();
		AddDebugOverlay(snapshot, GlobalPosition, accel, accelMagnitude, busHas, busFrameId);
		ThrottleBusReadLog(delta, snapshot, busHas, busFrameId);

		if (Print)
		{
			ThrottlePrint(delta, snapshot, GlobalPosition, accel, accelMagnitude, null);
		}
	}

	private static NumVector3 ToNumerics(GdVector3 v)
	{
		return new NumVector3(v.X, v.Y, v.Z);
	}

	private void AddDebugOverlay(RendererCore.SceneSnapshot.SceneSnapshot snapshot, GdVector3 worldPos, NumVector3 accel, float magnitude, bool busHas, ulong busFrameId)
	{
		var viewport = GetViewport();
		if (viewport == null) return;

		var cam = viewport.GetCamera3D();
		if (cam == null || !IsInstanceValid(cam)) return;

		GdVector2 screenPos = cam.UnprojectPosition(worldPos);
		const float crossHalf = 4f;
		Color crossColor = Colors.Cyan;

		DebugOverlayBus.AddLine(
			screenPos + new GdVector2(-crossHalf, 0f),
			screenPos + new GdVector2(crossHalf, 0f),
			crossColor,
			1f);
		DebugOverlayBus.AddLine(
			screenPos + new GdVector2(0f, -crossHalf),
			screenPos + new GdVector2(0f, crossHalf),
			crossColor,
			1f);

		if (magnitude > Epsilon)
		{
			var dir = accel / magnitude;
			var tipWorld = worldPos + new GdVector3(dir.X, dir.Y, dir.Z) * LineScale;
			GdVector2 tipScreen = cam.UnprojectPosition(tipWorld);
			DebugOverlayBus.AddLine(screenPos, tipScreen, Colors.Yellow, 2f);
		}

		int fieldsCount = snapshot.Fields?.Count ?? 0;
		int candidateCount = -1;
		if (snapshot.FieldTLAS != null)
		{
			Span<int> candidates = stackalloc int[256];
			candidateCount = snapshot.FieldTLAS.QueryPoint(ToNumerics(worldPos), candidates);
		}

		var k = 0f;
		var dt = 0f;
		var gridInfo = " grid=NULL";
		if (snapshot.CurvatureGrid != null)
		{
			var pWorld = ToNumerics(worldPos);
			k = snapshot.CurvatureGrid.LookupKmax(pWorld);
			dt = StepPolicy.ComputeDt(k, EpsPos, DtMin, DtMax);
			var inside = snapshot.CurvatureGrid.IsInside(pWorld);
			gridInfo = $" grid=OK inside={inside} cs={snapshot.CurvatureGrid.CellSize:0.###} dims={snapshot.CurvatureGrid.DimX}x{snapshot.CurvatureGrid.DimY}x{snapshot.CurvatureGrid.DimZ}";
		}

		string info = candidateCount >= 0
			? $"fields={fieldsCount} candidates={candidateCount} |a|={magnitude:0.000} K={k:0.000000} dt={dt:0.000000}{gridInfo} busHas={busHas} frameId={busFrameId}"
			: $"fields={fieldsCount} |a|={magnitude:0.000} K={k:0.000000} dt={dt:0.000000}{gridInfo} busHas={busHas} frameId={busFrameId}";
		DebugOverlayBus.AddText(screenPos + new GdVector2(6f, -6f), info, Colors.White);
	}

	private void ThrottleBusReadLog(double delta, RendererCore.SceneSnapshot.SceneSnapshot snapshot, bool busHas, ulong busFrameId)
	{
		if (!TryConsumeProbeLogSlot())
		{
			return;
		}

		var fieldsCount = snapshot.Fields?.Count ?? 0;
		var gridOk = snapshot.CurvatureGrid != null ? "OK" : "NULL";
		GD.Print($"[PROBE READ] frameId={busFrameId} busHas={busHas} grid={gridOk} fields={fieldsCount}");
	}

	private void ThrottlePrint(double delta, RendererCore.SceneSnapshot.SceneSnapshot snapshot, GdVector3 position, NumVector3 accel, float magnitude, string note)
	{
		if (!Print || !TryConsumeProbeLogSlot())
		{
			return;
		}

		var nodeName = Name.ToString();
		var prefix = string.IsNullOrWhiteSpace(note) ? nodeName : $"{nodeName} ({note})";
		var fieldsCount = snapshot.Fields?.Count ?? 0;
		var candidateCount = -1;
		if (snapshot.FieldTLAS != null)
		{
			Span<int> candidates = stackalloc int[256];
			candidateCount = snapshot.FieldTLAS.QueryPoint(ToNumerics(position), candidates);
		}

		var fieldsInfo = candidateCount >= 0
			? $"fields={fieldsCount} candidates={candidateCount}"
			: $"fields={fieldsCount}";

		var k = 0f;
		var dt = 0f;
		var gridInfo = "grid=NULL";
		if (snapshot.CurvatureGrid != null)
		{
			var pWorld = ToNumerics(position);
			k = snapshot.CurvatureGrid.LookupKmax(pWorld);
			dt = StepPolicy.ComputeDt(k, EpsPos, DtMin, DtMax);
			var inside = snapshot.CurvatureGrid.IsInside(pWorld);
			gridInfo = $"grid=OK inside={inside} cs={snapshot.CurvatureGrid.CellSize:0.###} dims={snapshot.CurvatureGrid.DimX}x{snapshot.CurvatureGrid.DimY}x{snapshot.CurvatureGrid.DimZ}";
		}

		GD.Print($"{prefix}: accel=({accel.X:0.###}, {accel.Y:0.###}, {accel.Z:0.###}) |mag|={magnitude:0.###} {fieldsInfo} K={k:0.000000} dt={dt:0.000000} {gridInfo}");
	}

	private bool TryConsumeProbeLogSlot()
	{
		if (!DebugLogConfig.EnableProbeLog)
		{
			return false;
		}

		var now = Time.GetTicksMsec() * 0.001;
		if (now < _nextProbeLogTimeSec)
		{
			return false;
		}

		_nextProbeLogTimeSec = now + Math.Max(0.05, DebugLogConfig.ProbeLogIntervalSec);
		return true;
	}

	private static bool TryDebugDrawLine(GdVector3 start, GdVector3 end)
	{
		var debugType = typeof(Node).Assembly.GetType("Godot.DebugDraw")
			?? typeof(Node).Assembly.GetType("Godot.DebugDraw3D");
		if (debugType == null)
		{
			return false;
		}

		var method = FindLineMethod(debugType);
		if (method == null)
		{
			return false;
		}

		try
		{
			var parameters = method.GetParameters();
			var args = new object[parameters.Length];
			var vectorIndex = 0;

			for (var i = 0; i < parameters.Length; i++)
			{
				var param = parameters[i];
				if (param.ParameterType == typeof(Godot.Vector3))
				{
					args[i] = vectorIndex == 0 ? start : end;
					vectorIndex++;
				}
				else if (param.ParameterType == typeof(Color))
				{
					args[i] = Colors.Cyan;
				}
				else if (param.ParameterType == typeof(float))
				{
					args[i] = 0.0f;
				}
				else if (param.ParameterType == typeof(double))
				{
					args[i] = 0.0;
				}
				else if (param.ParameterType == typeof(int))
				{
					args[i] = 0;
				}
				else if (param.ParameterType == typeof(bool))
				{
					args[i] = false;
				}
				else if (param.HasDefaultValue)
				{
					args[i] = param.DefaultValue;
				}
				else if (param.ParameterType.IsValueType)
				{
					args[i] = Activator.CreateInstance(param.ParameterType);
				}
				else
				{
					args[i] = null;
				}
			}

			method.Invoke(null, args);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static MethodInfo FindLineMethod(Type debugType)
	{
		var methods = debugType.GetMethods(BindingFlags.Public | BindingFlags.Static);
		foreach (var name in new[] { "Line", "DrawLine", "Line3D", "DrawLine3D" })
		{
			foreach (var method in methods)
			{
				if (!string.Equals(method.Name, name, StringComparison.Ordinal))
				{
					continue;
				}

				var parameters = method.GetParameters();
				if (parameters.Length < 2)
				{
					continue;
				}

				if (parameters[0].ParameterType != typeof(Godot.Vector3)
					|| parameters[1].ParameterType != typeof(Godot.Vector3))
				{
					continue;
				}

				return method;
			}
		}

		return null;
	}
}
