using Godot;
using RendererCore.Fields;
using System;
using System.Globalization;

/// <summary>
/// V1 macro-scale atomic-orbital GRIN authoring node.
/// This intentionally fits the existing FieldSource3D packed field path.
/// </summary>
[Tool]
public partial class AtomicEigenmodeFieldSource3D : FieldSource3D
{
	[ExportGroup("Atomic Orbital V1")]
	[Export(PropertyHint.Range, "0,3,1")] public int ElectronCount { get; set; } = 1;
	[Export] public string AtomicPreset { get; set; } = "hydrogen";
	[Export] public float OrbitalRadius { get; set; } = 3.5f;
	[Export] public float CurvatureStrength { get; set; } = 0.002f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ModulationDepth { get; set; } = 0.0f;
	[Export] public float FieldClockHz { get; set; } = 0.25f;
	[Export] public float UpdateIntervalSeconds { get; set; } = 1.0f;
	[Export] public bool TimeEnabled { get; set; } = false;
	[Export] public int FieldTickIndex { get; set; } = 0;
	[Export] public float Phase { get; set; } = 0.0f;
	[Export] public bool ProtonCoreEnabled { get; set; } = false;

	public float EffectiveTemporalModulation
	{
		get
		{
			if (!TimeEnabled)
			{
				return 1.0f;
			}

			float depth = Mathf.Clamp(ModulationDepth, 0f, 1f);
			return Mathf.Max(0f, 1.0f + (depth * Mathf.Sin(Phase)));
		}
	}

	public float SupportRadius => Mathf.Max(0.001f, OrbitalRadius * 4.0f);

	public override ResolvedFieldParams ResolveEffectiveParams(out string reason)
	{
		ResolvedFieldParams resolved = base.ResolveEffectiveParams(out _);
		int electrons = Mathf.Clamp(ElectronCount, 0, 3);
		float strength = Mathf.Max(0f, CurvatureStrength);
		float radius = Mathf.Max(0.001f, OrbitalRadius);
		bool hasElectronCloud = electrons > 0 && strength > 1e-7f;
		bool hasCore = ProtonCoreEnabled && strength > 1e-7f;

		resolved.enabled = Enabled && (hasElectronCloud || hasCore);
		resolved.shapeType = FieldShapeType.SphereRadial;
		resolved.curveType = FieldCurveType.AtomicOrbital;
		resolved.rInner = 0.0f;
		resolved.rOuter = SupportRadius;
		resolved.amp = hasElectronCloud ? strength : 0.0f;
		resolved.a = electrons;
		resolved.b = radius;
		resolved.c = EffectiveTemporalModulation;
		resolved.sigma = Phase;
		resolved.overrideBetaScale = true;
		resolved.betaScale = 1.0f;
		resolved.edgeSoftness = 0.0f;
		reason = "atomic_orbital_v1";
		return resolved;
	}

	public float EvaluateDensity(float worldRadius)
	{
		int electrons = Mathf.Clamp(ElectronCount, 0, 3);
		if (electrons <= 0)
		{
			return 0.0f;
		}

		float radius = Mathf.Max(0.001f, OrbitalRadius);
		float density = Mathf.Exp((-2f * Mathf.Max(0f, worldRadius)) / radius);
		return Mathf.Clamp(density * EffectiveTemporalModulation, 0f, 1f);
	}

	public string BuildStateToken()
	{
		return string.Format(
			CultureInfo.InvariantCulture,
			"preset={0} electrons={1} radius={2:0.######} strength={3:0.######} modulation={4:0.######} time={5} tick={6} phase={7:0.######} core={8}",
			AtomicPreset,
			Mathf.Clamp(ElectronCount, 0, 3),
			OrbitalRadius,
			CurvatureStrength,
			ModulationDepth,
			TimeEnabled ? 1 : 0,
			FieldTickIndex,
			Phase,
			ProtonCoreEnabled ? 1 : 0);
	}
}
