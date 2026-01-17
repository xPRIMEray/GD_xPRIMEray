using Godot;
using System;

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

	public override void _Ready()
	{
		AddToGroup("field_sources");
	}
}
