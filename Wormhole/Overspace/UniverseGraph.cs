using Godot;
using Godot.Collections;
using System;

[GlobalClass]
public partial class UniverseGraph : Resource
{
	[Export] public string GraphId = string.Empty;
	[Export] public string DisplayName = string.Empty;
	[Export] public string RootWorldId = string.Empty;
	[Export] public OverspaceProfile UniverseProfile;
	[Export] public Array<WorldNode> Worlds = new();
	[Export] public Array<PortalAnchor> Anchors = new();
	[Export] public Array<PortalLink> Links = new();
	[Export(PropertyHint.MultilineText)] public string Notes = string.Empty;

	public WorldNode FindWorldNode(string worldId)
	{
		foreach (WorldNode world in Worlds)
		{
			if (world != null && string.Equals(world.WorldId, worldId, StringComparison.Ordinal))
			{
				return world;
			}
		}

		return null;
	}

	public DensityLayer FindDensityLayer(string worldId, string layerId)
	{
		WorldNode world = FindWorldNode(worldId);
		return world?.GetLayer(layerId);
	}

	public PortalAnchor FindAnchor(string anchorId)
	{
		foreach (PortalAnchor anchor in Anchors)
		{
			if (anchor != null && string.Equals(anchor.AnchorId, anchorId, StringComparison.Ordinal))
			{
				return anchor;
			}
		}

		return null;
	}

	public PortalLink FindLink(string linkId)
	{
		foreach (PortalLink link in Links)
		{
			if (link != null && string.Equals(link.LinkId, linkId, StringComparison.Ordinal))
			{
				return link;
			}
		}

		return null;
	}

	public Array<PortalLink> FindLinksFromAnchor(string anchorId)
	{
		Array<PortalLink> matches = new();
		foreach (PortalLink link in Links)
		{
			if (link != null && string.Equals(link.SourceAnchorId, anchorId, StringComparison.Ordinal))
			{
				matches.Add(link);
				continue;
			}

			if (link != null && link.Bidirectional && string.Equals(link.TargetAnchorId, anchorId, StringComparison.Ordinal))
			{
				matches.Add(link);
			}
		}

		return matches;
	}

	public OverspaceProfile ResolveProfile(string worldId, string layerId, string anchorId = "")
	{
		OverspaceProfile profile = OverspaceProfile.Combine(UniverseProfile, null);
		WorldNode world = FindWorldNode(worldId);
		if (world != null)
		{
			profile = OverspaceProfile.Combine(profile, world.WorldProfile);

			DensityLayer layer = world.GetLayer(layerId);
			if (layer != null)
			{
				profile = OverspaceProfile.Combine(profile, layer.LayerProfile);
			}
		}

		if (!string.IsNullOrWhiteSpace(anchorId))
		{
			PortalAnchor anchor = FindAnchor(anchorId);
			if (anchor != null)
			{
				profile = OverspaceProfile.Combine(profile, anchor.AnchorProfile);
			}
		}

		return profile;
	}
}
