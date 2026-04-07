using Godot;
using System.Collections.Generic;

public partial class OverspaceTrophyRoomDemo : Node3D
{
	[ExportGroup("References")]
	[Export] public NodePath ViewerCameraPath = new("PlayerCamera");
	[Export] public NodePath SummaryLabelPath = new("CanvasLayer/DemoSummary");

	[ExportGroup("Behavior")]
	[Export(PropertyHint.Range, "0,2,0.01")] public float TeleportCooldownSeconds = 0.35f;
	[Export] public bool BuildDefaultGraphIfMissing = true;
	[Export] public UniverseGraph DemoGraph;

	private Camera3D _viewerCamera;
	private Label _summaryLabel;
	private readonly Dictionary<string, WormholePortal> _portalByAnchorId = new();
	private readonly Dictionary<WormholePortal, string> _anchorIdByPortal = new();
	private readonly Dictionary<WormholePortal, float> _lastPortalDelta = new();
	private readonly Dictionary<string, Label3D> _galleryLabelsByAnchorId = new();
	private float _teleportCooldownRemaining;
	private string _currentWorldId = "TrophyRoom";
	private string _currentLayerId = "foyer";

	public override void _Ready()
	{
		_viewerCamera = GetNodeOrNull<Camera3D>(ViewerCameraPath);
		_summaryLabel = GetNodeOrNull<Label>(SummaryLabelPath);

		if (DemoGraph == null && BuildDefaultGraphIfMissing)
		{
			DemoGraph = BuildDefaultGraph();
		}

		BindSceneNodes();
		ConfigureGalleryLabels();
		ApplyGalleryCameraMask();
		PrimePortalDeltas();
		UpdatePortalViews();
		UpdateSummaryLabel();
	}

	public override void _Process(double delta)
	{
		UpdatePortalViews();

		if (_viewerCamera == null)
		{
			return;
		}

		if (_teleportCooldownRemaining > 0f)
		{
			_teleportCooldownRemaining = Mathf.Max(0f, _teleportCooldownRemaining - (float)delta);
			PrimePortalDeltas();
			return;
		}

		foreach (KeyValuePair<WormholePortal, string> entry in _anchorIdByPortal)
		{
			WormholePortal portal = entry.Key;
			float currentDelta = portal.SignedRadiusDelta(_viewerCamera.GlobalPosition);
			float previousDelta = _lastPortalDelta.TryGetValue(portal, out float storedDelta) ? storedDelta : currentDelta;
			_lastPortalDelta[portal] = currentDelta;

			if (previousDelta > 0f && currentDelta <= 0f)
			{
				TeleportVia(portal, entry.Value);
				break;
			}
		}
	}

	private void BindSceneNodes()
	{
		RegisterPortal("gallery_earth_orb", "Gallery/EarthOrbPortal");
		RegisterPortal("gallery_sun_orb", "Gallery/SunOrbPortal");
		RegisterPortal("gallery_moon_orb", "Gallery/MoonOrbPortal");
		RegisterPortal("earth_layer1_arrival", "Worlds/EarthWorld/EarthArrivalPortal");
		RegisterPortal("sun_layer1_arrival", "Worlds/SunWorld/SunArrivalPortal");
		RegisterPortal("moon_layer1_arrival", "Worlds/MoonWorld/MoonArrivalPortal");

		RegisterGalleryLabel("gallery_earth_orb", "Gallery/EarthPedestal/EarthLabel");
		RegisterGalleryLabel("gallery_sun_orb", "Gallery/SunPedestal/SunLabel");
		RegisterGalleryLabel("gallery_moon_orb", "Gallery/MoonPedestal/MoonLabel");
	}

	private void RegisterPortal(string anchorId, string scenePath)
	{
		WormholePortal portal = GetNodeOrNull<WormholePortal>(scenePath);
		if (portal == null)
		{
			return;
		}

		_portalByAnchorId[anchorId] = portal;
		_anchorIdByPortal[portal] = anchorId;
	}

	private void RegisterGalleryLabel(string anchorId, string scenePath)
	{
		Label3D label = GetNodeOrNull<Label3D>(scenePath);
		if (label != null)
		{
			_galleryLabelsByAnchorId[anchorId] = label;
		}
	}

	private void ConfigureGalleryLabels()
	{
		if (DemoGraph == null)
		{
			return;
		}

		foreach (KeyValuePair<string, Label3D> entry in _galleryLabelsByAnchorId)
		{
			string anchorId = entry.Key;
			Label3D label = entry.Value;
			Godot.Collections.Array<PortalLink> links = DemoGraph.FindLinksFromAnchor(anchorId);
			if (links.Count == 0)
			{
				label.Text = anchorId;
				continue;
			}

			PortalLink link = links[0];
			PortalAnchor targetAnchor = DemoGraph.FindAnchor(link.TargetAnchorId);
			if (targetAnchor == null)
			{
				label.Text = anchorId;
				continue;
			}

			label.Text =
				$"{targetAnchor.WorldId.ToUpperInvariant()} LAYER 1\n" +
				$"{targetAnchor.WorldId} / {targetAnchor.DensityLayerId}\n" +
				$"{link.LinkId}";
		}
	}

	private void UpdatePortalViews()
	{
		if (_viewerCamera == null)
		{
			return;
		}

		foreach (WormholePortal portal in _anchorIdByPortal.Keys)
		{
			portal?.UpdatePortalView(_viewerCamera);
		}
	}

	private void TeleportVia(WormholePortal sourcePortal, string sourceAnchorId)
	{
		if (_viewerCamera == null || DemoGraph == null)
		{
			return;
		}

		Godot.Collections.Array<PortalLink> links = DemoGraph.FindLinksFromAnchor(sourceAnchorId);
		if (links.Count == 0)
		{
			return;
		}

		PortalLink link = links[0];
		WormholePortal targetPortal = sourcePortal.GetLinkedPortal();
		if (targetPortal == null)
		{
			return;
		}

		_viewerCamera.GlobalTransform = sourcePortal.BuildExitTransform(_viewerCamera.GlobalTransform);
		_viewerCamera.CullMask = targetPortal.GeometryMask | targetPortal.PortalMask;
		_teleportCooldownRemaining = TeleportCooldownSeconds;

		PortalAnchor targetAnchor = DemoGraph.FindAnchor(link.TargetAnchorId);
		if (targetAnchor != null)
		{
			_currentWorldId = targetAnchor.WorldId;
			_currentLayerId = targetAnchor.DensityLayerId;
		}

		PrimePortalDeltas();
		UpdateSummaryLabel();
	}

	private void PrimePortalDeltas()
	{
		if (_viewerCamera == null)
		{
			return;
		}

		foreach (WormholePortal portal in _anchorIdByPortal.Keys)
		{
			_lastPortalDelta[portal] = portal.SignedRadiusDelta(_viewerCamera.GlobalPosition);
		}
	}

	private void ApplyGalleryCameraMask()
	{
		if (_viewerCamera == null)
		{
			return;
		}

		if (_portalByAnchorId.TryGetValue("gallery_earth_orb", out WormholePortal galleryPortal))
		{
			_viewerCamera.CullMask = galleryPortal.GeometryMask | galleryPortal.PortalMask;
		}
	}

	private void UpdateSummaryLabel()
	{
		if (_summaryLabel == null)
		{
			return;
		}

		_summaryLabel.Text =
			"OVERSPACE TROPHY ROOM\n" +
			$"Current world: {_currentWorldId}\n" +
			$"Current layer: {_currentLayerId}\n" +
			"Live links: Earth Layer 1, Sun Layer 1, Moon Layer 1\n" +
			"Controls: WASD move, E/Q rise-fall, Shift sprint, Esc release mouse\n" +
			"Step into an orb to traverse, then use the return orb to re-enter the foyer.";
	}

	private UniverseGraph BuildDefaultGraph()
	{
		OverspaceProfile identity = new()
		{
			ProfileId = "identity"
		};

		DensityLayer foyerLayer = new()
		{
			LayerId = "foyer",
			DisplayName = "Foyer",
			LayerProfile = new OverspaceProfile
			{
				ProfileId = "foyer",
				ScaleScalar = 1.0f,
				ClockScalar = 1.0f,
				DensityScalar = 1.0f,
				PhaseScalar = 1.0f,
				FieldScalar = 1.0f
			}
		};

		WorldNode trophyRoom = new()
		{
			WorldId = "TrophyRoom",
			DisplayName = "Overspace Trophy Room",
			DefaultLayerId = "foyer",
			WorldProfile = identity
		};
		trophyRoom.DensityLayers.Add(foyerLayer);
		trophyRoom.AnchorIds.Add("gallery_earth_orb");
		trophyRoom.AnchorIds.Add("gallery_sun_orb");
		trophyRoom.AnchorIds.Add("gallery_moon_orb");

		WorldNode solarSystem = new()
		{
			WorldId = "SolarSystem",
			DisplayName = "Solar System",
			ParentWorldId = "PrimeShelf",
			DefaultLayerId = "interplanetary",
			WorldProfile = identity
		};
		solarSystem.ChildWorldIds.Add("Earth");
		solarSystem.ChildWorldIds.Add("Sun");
		solarSystem.ChildWorldIds.Add("Moon");
		solarSystem.DensityLayers.Add(new DensityLayer
		{
			LayerId = "interplanetary",
			DisplayName = "Interplanetary",
			LayerProfile = new OverspaceProfile
			{
				ProfileId = "interplanetary",
				DensityScalar = 1.0f,
				PhaseScalar = 1.0f,
				ClockScalar = 1.0f,
				ScaleScalar = 1.0f,
				FieldScalar = 1.0f
			}
		});

		WorldNode earth = new()
		{
			WorldId = "Earth",
			DisplayName = "Earth",
			ParentWorldId = "SolarSystem",
			DefaultLayerId = "layer_1",
			WorldProfile = new OverspaceProfile
			{
				ProfileId = "earth_profile",
				DensityScalar = 1.0f,
				PhaseScalar = 1.0f,
				ClockScalar = 1.0f,
				ScaleScalar = 1.0f,
				FieldScalar = 1.1f
			}
		};
		earth.DensityLayers.Add(new DensityLayer
		{
			LayerId = "layer_1",
			DisplayName = "Earth Layer 1",
			LayerProfile = new OverspaceProfile
			{
				ProfileId = "earth_layer_1",
				DensityScalar = 1.2f,
				PhaseScalar = 1.02f,
				ClockScalar = 1.0f,
				ScaleScalar = 1.0f,
				FieldScalar = 1.2f
			}
		});
		earth.AnchorIds.Add("earth_layer1_arrival");

		WorldNode sun = new()
		{
			WorldId = "Sun",
			DisplayName = "Sun",
			ParentWorldId = "SolarSystem",
			DefaultLayerId = "layer_1",
			WorldProfile = new OverspaceProfile
			{
				ProfileId = "sun_profile",
				DensityScalar = 1.8f,
				PhaseScalar = 1.08f,
				ClockScalar = 0.94f,
				ScaleScalar = 1.0f,
				FieldScalar = 2.4f
			}
		};
		sun.DensityLayers.Add(new DensityLayer
		{
			LayerId = "layer_1",
			DisplayName = "Sun Layer 1",
			LayerProfile = new OverspaceProfile
			{
				ProfileId = "sun_layer_1",
				DensityScalar = 2.4f,
				PhaseScalar = 1.12f,
				ClockScalar = 0.88f,
				ScaleScalar = 0.95f,
				FieldScalar = 3.2f
			}
		});
		sun.AnchorIds.Add("sun_layer1_arrival");

		WorldNode moon = new()
		{
			WorldId = "Moon",
			DisplayName = "Moon",
			ParentWorldId = "SolarSystem",
			DefaultLayerId = "layer_1",
			WorldProfile = new OverspaceProfile
			{
				ProfileId = "moon_profile",
				DensityScalar = 0.8f,
				PhaseScalar = 0.98f,
				ClockScalar = 1.01f,
				ScaleScalar = 0.9f,
				FieldScalar = 0.85f
			}
		};
		moon.DensityLayers.Add(new DensityLayer
		{
			LayerId = "layer_1",
			DisplayName = "Moon Layer 1",
			LayerProfile = new OverspaceProfile
			{
				ProfileId = "moon_layer_1",
				DensityScalar = 0.9f,
				PhaseScalar = 0.99f,
				ClockScalar = 1.0f,
				ScaleScalar = 0.92f,
				FieldScalar = 0.8f
			}
		});
		moon.AnchorIds.Add("moon_layer1_arrival");

		PortalAnchor galleryEarth = CreateAnchor("gallery_earth_orb", "TrophyRoom", "foyer", "Gallery/EarthOrbPortal");
		PortalAnchor gallerySun = CreateAnchor("gallery_sun_orb", "TrophyRoom", "foyer", "Gallery/SunOrbPortal");
		PortalAnchor galleryMoon = CreateAnchor("gallery_moon_orb", "TrophyRoom", "foyer", "Gallery/MoonOrbPortal");
		PortalAnchor earthArrival = CreateAnchor("earth_layer1_arrival", "Earth", "layer_1", "Worlds/EarthWorld/EarthArrivalPortal");
		PortalAnchor sunArrival = CreateAnchor("sun_layer1_arrival", "Sun", "layer_1", "Worlds/SunWorld/SunArrivalPortal");
		PortalAnchor moonArrival = CreateAnchor("moon_layer1_arrival", "Moon", "layer_1", "Worlds/MoonWorld/MoonArrivalPortal");

		PortalLink galleryToEarth = CreateLink("gallery_to_earth_l1", "gallery_earth_orb", "earth_layer1_arrival", "earth_to_gallery_l1", 1.0f, 1.0f, 1.0f);
		PortalLink earthToGallery = CreateLink("earth_to_gallery_l1", "earth_layer1_arrival", "gallery_earth_orb", "gallery_to_earth_l1", 1.0f, 1.0f, 1.0f);
		PortalLink galleryToSun = CreateLink("gallery_to_sun_l1", "gallery_sun_orb", "sun_layer1_arrival", "sun_to_gallery_l1", 0.95f, 0.88f, 2.1f);
		PortalLink sunToGallery = CreateLink("sun_to_gallery_l1", "sun_layer1_arrival", "gallery_sun_orb", "gallery_to_sun_l1", 1.05f, 1.12f, 0.7f);
		PortalLink galleryToMoon = CreateLink("gallery_to_moon_l1", "gallery_moon_orb", "moon_layer1_arrival", "moon_to_gallery_l1", 0.92f, 1.0f, 0.8f);
		PortalLink moonToGallery = CreateLink("moon_to_gallery_l1", "moon_layer1_arrival", "gallery_moon_orb", "gallery_to_moon_l1", 1.08f, 1.0f, 1.2f);

		galleryEarth.LinkIds.Add(galleryToEarth.LinkId);
		gallerySun.LinkIds.Add(galleryToSun.LinkId);
		galleryMoon.LinkIds.Add(galleryToMoon.LinkId);
		earthArrival.LinkIds.Add(earthToGallery.LinkId);
		sunArrival.LinkIds.Add(sunToGallery.LinkId);
		moonArrival.LinkIds.Add(moonToGallery.LinkId);

		UniverseGraph graph = new()
		{
			GraphId = "overspace_trophy_room_demo",
			DisplayName = "Overspace Trophy Room Demo",
			RootWorldId = "PrimeShelf",
			UniverseProfile = identity
		};
		graph.Worlds.Add(trophyRoom);
		graph.Worlds.Add(solarSystem);
		graph.Worlds.Add(earth);
		graph.Worlds.Add(sun);
		graph.Worlds.Add(moon);
		graph.Anchors.Add(galleryEarth);
		graph.Anchors.Add(gallerySun);
		graph.Anchors.Add(galleryMoon);
		graph.Anchors.Add(earthArrival);
		graph.Anchors.Add(sunArrival);
		graph.Anchors.Add(moonArrival);
		graph.Links.Add(galleryToEarth);
		graph.Links.Add(earthToGallery);
		graph.Links.Add(galleryToSun);
		graph.Links.Add(sunToGallery);
		graph.Links.Add(galleryToMoon);
		graph.Links.Add(moonToGallery);
		return graph;
	}

	private static PortalAnchor CreateAnchor(string anchorId, string worldId, string layerId, string nodePath)
	{
		return new PortalAnchor
		{
			AnchorId = anchorId,
			DisplayName = anchorId,
			WorldId = worldId,
			DensityLayerId = layerId,
			SceneNodePath = new NodePath(nodePath),
			InfluenceRadius = 1.15f
		};
	}

	private static PortalLink CreateLink(
		string linkId,
		string sourceAnchorId,
		string targetAnchorId,
		string reverseLinkId,
		float scaleScalar,
		float clockScalar,
		float fieldScalar)
	{
		return new PortalLink
		{
			LinkId = linkId,
			DisplayName = linkId,
			SourceAnchorId = sourceAnchorId,
			TargetAnchorId = targetAnchorId,
			Bidirectional = false,
			LinkKind = "orb_wormhole",
			PreserveVelocityFrame = true,
			ReverseLinkId = reverseLinkId,
			TransitProfile = new OverspaceProfile
			{
				ProfileId = $"{linkId}_profile",
				DensityScalar = 1.0f,
				PhaseScalar = 1.0f,
				ClockScalar = clockScalar,
				ScaleScalar = scaleScalar,
				FieldScalar = fieldScalar
			}
		};
	}
}
