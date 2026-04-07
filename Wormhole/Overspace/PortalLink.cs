using Godot;

[GlobalClass]
public partial class PortalLink : Resource
{
	[Export] public string LinkId = string.Empty;
	[Export] public string DisplayName = string.Empty;
	[Export] public string SourceAnchorId = string.Empty;
	[Export] public string TargetAnchorId = string.Empty;
	[Export] public bool Bidirectional = true;
	[Export] public string LinkKind = "portal";
	[Export] public bool PreserveVelocityFrame = true;
	[Export] public OverspaceProfile TransitProfile;
	[Export] public string ReverseLinkId = string.Empty;
	[Export(PropertyHint.MultilineText)] public string Notes = string.Empty;

	public bool ConnectsAnchor(string anchorId)
	{
		return SourceAnchorId == anchorId || TargetAnchorId == anchorId;
	}
}
