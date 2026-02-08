using Godot;
using System;
using System.Numerics;
using System.Reflection;
using GodotAdapter;
using RendererCore.Fields;
using GdVector3 = Godot.Vector3;
using NumVector3 = System.Numerics.Vector3;

public partial class FieldProbe3D : Node3D
{
	[Export] public float LineScale = 1.0f;
	[Export] public bool Print = false;
	[Export] public float PrintIntervalSec = 0.25f;

	private const float Epsilon = 1e-6f;
	private double _printTimer;

	public override void _Process(double delta)
	{
		var snapshot = SnapshotBuilder.BuildFromGodotScene(GetTree()?.CurrentScene);
		var accel = FieldSystem.AccelAt(ToNumerics(GlobalPosition), snapshot);

		var accelMagnitude = accel.Length();
		if (accelMagnitude > Epsilon)
		{
			var dir = accel / accelMagnitude;
			var start = GlobalPosition;
			var end = start + new GdVector3(dir.X, dir.Y, dir.Z) * LineScale;

			if (!TryDebugDrawLine(start, end))
			{
				ThrottlePrint(delta, snapshot, GlobalPosition, accel, accelMagnitude, "DebugDraw unavailable");
			}
		}

		if (Print)
		{
			ThrottlePrint(delta, snapshot, GlobalPosition, accel, accelMagnitude, null);
		}
	}

	private static NumVector3 ToNumerics(GdVector3 v)
	{
		return new NumVector3(v.X, v.Y, v.Z);
	}

	private void ThrottlePrint(double delta, RendererCore.SceneSnapshot.SceneSnapshot snapshot, GdVector3 position, NumVector3 accel, float magnitude, string note)
	{
		var interval = Math.Max(0.0f, PrintIntervalSec);
		if (interval > 0.0f)
		{
			_printTimer += delta;
			if (_printTimer < interval)
			{
				return;
			}

			_printTimer -= interval;
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
		GD.Print($"{prefix}: accel=({accel.X:0.###}, {accel.Y:0.###}, {accel.Z:0.###}) |mag|={magnitude:0.###} {fieldsInfo}");
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
