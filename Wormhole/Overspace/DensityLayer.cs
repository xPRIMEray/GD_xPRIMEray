using Godot;
using Godot.Collections;

[GlobalClass]
public partial class DensityLayer : Resource
{
	[Export] public string LayerId = string.Empty;
	[Export] public string DisplayName = string.Empty;
	[Export] public string ParentLayerId = string.Empty;
	[Export] public Array<string> ChildLayerIds = new();
	[Export] public OverspaceProfile LayerProfile;
	[Export] public Array<string> AnchorIds = new();
	[Export] public bool AllowPhaseTransform = true;
	[Export] public bool AllowClockTransform = true;
	[Export] public bool AllowScaleTransform = true;
	[Export(PropertyHint.MultilineText)] public string Notes = string.Empty;

	public OverspaceProfile ResolveProfile(OverspaceProfile worldProfile)
	{
		return OverspaceProfile.Combine(worldProfile, LayerProfile);
	}
}
