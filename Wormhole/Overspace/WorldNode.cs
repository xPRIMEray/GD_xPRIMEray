using Godot;
using Godot.Collections;
using System;

[GlobalClass]
public partial class WorldNode : Resource
{
	[Export] public string WorldId = string.Empty;
	[Export] public string DisplayName = string.Empty;
	[Export] public string ParentWorldId = string.Empty;
	[Export] public Array<string> ChildWorldIds = new();
	[Export] public string SceneResourcePath = string.Empty;
	[Export] public string DefaultLayerId = string.Empty;
	[Export] public OverspaceProfile WorldProfile;
	[Export] public Array<DensityLayer> DensityLayers = new();
	[Export] public Array<string> AnchorIds = new();
	[Export(PropertyHint.MultilineText)] public string Notes = string.Empty;

	public DensityLayer GetLayer(string layerId)
	{
		foreach (DensityLayer layer in DensityLayers)
		{
			if (layer != null && string.Equals(layer.LayerId, layerId, StringComparison.Ordinal))
			{
				return layer;
			}
		}

		return null;
	}

	public OverspaceProfile ResolveLayerProfile(string layerId)
	{
		DensityLayer layer = GetLayer(layerId);
		if (layer == null)
		{
			return OverspaceProfile.Combine(WorldProfile, null);
		}

		return layer.ResolveProfile(WorldProfile);
	}
}
