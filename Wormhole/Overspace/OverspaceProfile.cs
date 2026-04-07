using Godot;

[GlobalClass]
public partial class OverspaceProfile : Resource
{
	[Export] public string ProfileId = string.Empty;
	[Export(PropertyHint.Range, "0.0001,100000,0.0001,or_greater")] public float DensityScalar = 1.0f;
	[Export(PropertyHint.Range, "0.0001,100000,0.0001,or_greater")] public float PhaseScalar = 1.0f;
	[Export(PropertyHint.Range, "0.0001,100000,0.0001,or_greater")] public float ClockScalar = 1.0f;
	[Export(PropertyHint.Range, "0.0001,100000,0.0001,or_greater")] public float ScaleScalar = 1.0f;
	[Export(PropertyHint.Range, "0.0001,100000,0.0001,or_greater")] public float FieldScalar = 1.0f;
	[Export(PropertyHint.MultilineText)] public string Notes = string.Empty;

	public OverspaceProfile CombineWith(OverspaceProfile child)
	{
		if (child == null)
		{
			return this;
		}

		return new OverspaceProfile
		{
			ProfileId = string.IsNullOrWhiteSpace(child.ProfileId) ? ProfileId : child.ProfileId,
			DensityScalar = DensityScalar * child.DensityScalar,
			PhaseScalar = PhaseScalar * child.PhaseScalar,
			ClockScalar = ClockScalar * child.ClockScalar,
			ScaleScalar = ScaleScalar * child.ScaleScalar,
			FieldScalar = FieldScalar * child.FieldScalar,
			Notes = child.Notes
		};
	}

	public static OverspaceProfile Combine(OverspaceProfile parent, OverspaceProfile child)
	{
		if (parent == null)
		{
			return child ?? new OverspaceProfile();
		}

		return parent.CombineWith(child);
	}
}
