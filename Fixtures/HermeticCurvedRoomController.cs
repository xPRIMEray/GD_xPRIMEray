using Godot;
using RendererCore.Config;
using RendererCore.Fields;
using System;
using System.Globalization;

public partial class HermeticCurvedRoomController : Node3D
{
	private static readonly NodePath FieldPath = new("FieldSource3D");
	private static readonly NodePath RendererPath = new("RayBeamRenderer");
	private const string CurvatureStrengthPrefix = "--hermetic-curvature-strength=";

	public override void _Ready()
	{
		float strength = ReadCurvatureStrength();
		FieldSource3D field = GetNodeOrNull<FieldSource3D>(FieldPath);
		RayBeamRenderer renderer = GetNodeOrNull<RayBeamRenderer>(RendererPath);

		if (field != null)
		{
			field.Enabled = MathF.Abs(strength) > 1e-7f;
			field.ShapeType = FieldShapeType.SphereRadial;
			field.CurveType = FieldCurveType.Power;
			field.ROuter = 4.75f;
			field.RInner = 0.0f;
			field.CanonicalEnableInnerRadius = false;
			field.CanonicalGamma = 1.0f;
			field.CanonicalOverrideBetaScale = true;
			field.CanonicalBetaScale = 1.0f;
			field.Amp = MathF.Abs(strength);
			field.Strength = MathF.Abs(strength);
			field.Attract = strength >= 0f;
			field.ModeFlags = strength < 0f ? 1u : 0u;
			field.DebugVizInGame = false;
			field.DebugVizEnabled = false;
		}

		if (renderer != null)
		{
			renderer.UseIntegratedField = true;
			renderer.BendScale = 1.0f;
			renderer.FieldStrength = MathF.Abs(strength) > 1e-7f ? strength : 0.0f;
			renderer.StopOnHit = true;
			renderer.TerminateTrailOnHit = true;
			renderer.RequireHitToRender = false;
		}

		FieldSource3D.ResolvedFieldParams resolved = default;
		string resolveReason = "missing_field";
		if (field != null)
		{
			resolved = field.ResolveEffectiveParams(out resolveReason);
		}

		GD.Print(
			"[HermeticClosure][Contract] fixture=hermetic_curved_room sealed_surfaces=6 " +
			"valid_exception_regions=0 expected_hit=1 closure_scope=scene_contract " +
			"physical_truth_claim=0");
		GD.Print(
			$"[HermeticClosure][Curvature] requested_strength={strength.ToString("0.######", CultureInfo.InvariantCulture)} " +
			$"field_present={(field != null ? 1 : 0)} field_enabled={(field != null && field.Enabled ? 1 : 0)} " +
			$"resolved_reason={resolveReason} resolved_enabled={(resolved.enabled ? 1 : 0)} " +
			$"resolved_amp={resolved.amp.ToString("0.######", CultureInfo.InvariantCulture)} " +
			$"curved_transport_enabled={(renderer != null && renderer.UseIntegratedField && MathF.Abs(renderer.FieldStrength) > 1e-7f ? 1 : 0)}");
	}

	private static float ReadCurvatureStrength()
	{
		foreach (string arg in GetCmdArgsForParsing())
		{
			if (string.IsNullOrWhiteSpace(arg) ||
				!arg.StartsWith(CurvatureStrengthPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string raw = arg.Substring(CurvatureStrengthPrefix.Length).Trim();
			if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
			{
				return value;
			}
		}

		return 0.0f;
	}

	private static string[] GetCmdArgsForParsing()
	{
		string[] userArgs = OS.GetCmdlineUserArgs();
		string[] args = OS.GetCmdlineArgs();
		if ((userArgs == null || userArgs.Length == 0) && (args == null || args.Length == 0))
		{
			return Array.Empty<string>();
		}
		if (userArgs == null || userArgs.Length == 0)
		{
			return args ?? Array.Empty<string>();
		}
		if (args == null || args.Length == 0)
		{
			return userArgs;
		}

		var merged = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
		var ordered = new System.Collections.Generic.List<string>(userArgs.Length + args.Length);
		foreach (string raw in userArgs)
		{
			if (string.IsNullOrWhiteSpace(raw))
			{
				continue;
			}
			string token = raw.Trim();
			if (merged.Add(token))
			{
				ordered.Add(token);
			}
		}
		foreach (string raw in args)
		{
			if (string.IsNullOrWhiteSpace(raw))
			{
				continue;
			}
			string token = raw.Trim();
			if (merged.Add(token))
			{
				ordered.Add(token);
			}
		}

		return ordered.ToArray();
	}
}
