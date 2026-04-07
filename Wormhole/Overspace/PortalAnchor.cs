using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PortalAnchor : Resource
{
	[Export] public string AnchorId = string.Empty;
	[Export] public string DisplayName = string.Empty;
	[Export] public string WorldId = string.Empty;
	[Export] public string DensityLayerId = string.Empty;
	[Export] public NodePath SceneNodePath = new NodePath();
	[Export] public Transform3D LocalFrame = Transform3D.Identity;
	[Export(PropertyHint.Range, "0,100000,0.01,or_greater")] public float InfluenceRadius = 2.0f;
	[Export] public OverspaceProfile AnchorProfile;
	[Export] public Array<string> LinkIds = new();
	[Export(PropertyHint.MultilineText)] public string Notes = string.Empty;

	public string GetAddressLabel()
	{
		return $"{WorldId}:{DensityLayerId}:{AnchorId}";
	}
}
