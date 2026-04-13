using Godot;
using System;
using System.Collections.Generic;

public partial class OverspaceTrophyRoomDemo : Node3D
{
	private const string GalleryEarthAnchorId = "gallery_earth_orb";
	private const string EarthArrivalAnchorId = "earth_layer1_arrival";

	[ExportGroup("References")]
	[Export] public NodePath ViewerCameraPath = new("PlayerCamera");
	[Export] public NodePath SummaryLabelPath = new("CanvasLayer/DemoSummary");
	[Export] public NodePath DebugOverlayPath = new("CanvasLayer/OverspaceDebugOverlay");

	[ExportGroup("Behavior")]
	[Export(PropertyHint.Range, "0,2,0.01")] public float TeleportCooldownSeconds = 0.35f;
	[Export] public bool BuildDefaultGraphIfMissing = true;
	[Export] public UniverseGraph DemoGraph;
	[Export] public bool EnableAutoValidation = false;
	[Export] public string ValidationCapturePath = "res://output/overspace/overspace_first_milestone.png";
	[Export(PropertyHint.Range, "0.5,20,0.1")] public float ValidationMoveSpeed = 3.2f;
	[Export(PropertyHint.Range, "0,10,0.1")] public float ValidationCaptureDelaySeconds = 1.0f;
	[Export(PropertyHint.Range, "1,30,0.5")] public float ValidationFallbackCaptureSeconds = 8.0f;

	private Camera3D _viewerCamera;
	private Label _summaryLabel;
	private OverspacePortalDebugOverlay _debugOverlay;
	private readonly Dictionary<string, WormholePortal> _portalByAnchorId = new();
	private readonly Dictionary<WormholePortal, string> _anchorIdByPortal = new();
	private readonly Dictionary<WormholePortal, float> _lastPortalDelta = new();
	private readonly Dictionary<string, Label3D> _galleryLabelsByAnchorId = new();
	private float _teleportCooldownRemaining;
	private string _currentWorldId = "TrophyRoom";
	private string _currentLayerId = "foyer";
	private string _currentZoneLabel = "gallery_z0";
	private string _activeAnchorId = GalleryEarthAnchorId;
	private bool _validationMode;
	private bool _validationTeleported;
	private bool _validationCaptureSaved;
	private bool _validationPathSampleMode;
	private double _validationCaptureCountdown;
	private double _validationElapsedSeconds;
	private float _validationCaptureProgressTarget = -1f;
	private Vector3 _validationStartPosition = Vector3.Zero;

	public override void _Ready()
	{
		GD.Print("[OverspaceDemo] _Ready entered");
		_viewerCamera = GetNodeOrNull<Camera3D>(ViewerCameraPath);
		_summaryLabel = GetNodeOrNull<Label>(SummaryLabelPath);
		_debugOverlay = GetNodeOrNull<OverspacePortalDebugOverlay>(DebugOverlayPath);

		if (DemoGraph == null && BuildDefaultGraphIfMissing)
		{
			DemoGraph = BuildDefaultGraph();
		}

		BindSceneNodes();
		ConfigureGalleryLabels();
		EnsureViewerSeesAllZones();
		PrimePortalDeltas();
		UpdatePortalViews();
		_debugOverlay?.Configure(_viewerCamera);
		UpdateDebugOverlay();
		UpdateSummaryLabel();
		InitializeValidationMode();
	}

	public override void _Process(double delta)
	{
		UpdatePortalViews();

		if (_viewerCamera == null)
		{
			return;
		}

		RunValidationStep(delta);

		if (_teleportCooldownRemaining > 0f)
		{
			_teleportCooldownRemaining = Mathf.Max(0f, _teleportCooldownRemaining - (float)delta);
			PrimePortalDeltas();
			UpdateDebugOverlay();
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

		UpdateDebugOverlay();
	}

	private void BindSceneNodes()
	{
		RegisterPortal(GalleryEarthAnchorId, "Gallery/EarthOrbPortal");
		RegisterPortal(EarthArrivalAnchorId, "Worlds/EarthWorld/EarthArrivalPortal");
		RegisterGalleryLabel(GalleryEarthAnchorId, "Gallery/EarthPedestal/EarthLabel");
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
				$"{targetAnchor.WorldId.ToUpperInvariant()} Z-ZONE\n" +
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
		// Demo-only traversal seam. This direct portal remap is not part of the renderer causal
		// validation ledger and should be treated as artistic/non-causal unless explicitly ledgered.
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

		_viewerCamera.GlobalTransform = sourcePortal.BuildConfiguredExitTransform(_viewerCamera.GlobalTransform);
		_teleportCooldownRemaining = TeleportCooldownSeconds;
		_activeAnchorId = link.TargetAnchorId;

		PortalAnchor targetAnchor = DemoGraph.FindAnchor(link.TargetAnchorId);
		if (targetAnchor != null)
		{
			_currentWorldId = targetAnchor.WorldId;
			_currentLayerId = targetAnchor.DensityLayerId;
		}
		_currentZoneLabel = targetPortal.ResolveZoneLabel();
		if (!_validationTeleported && _validationMode)
		{
			_validationTeleported = true;
			_validationCaptureCountdown = ValidationCaptureDelaySeconds;
		}

		PrimePortalDeltas();
		UpdateDebugOverlay();
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

	private void EnsureViewerSeesAllZones()
	{
		if (_viewerCamera == null)
		{
			return;
		}

		_viewerCamera.CullMask = uint.MaxValue;
	}

	private void UpdateSummaryLabel()
	{
		if (_summaryLabel == null)
		{
			return;
		}

		WormholePortal activePortal = ResolveActivePortal();
		string remapMode = activePortal != null && activePortal.EnablePhaseLockedRemap ? "Phase-locked throat remap" : "Direct linked-mouth remap";
		string diagnosticsMode = activePortal != null && activePortal.EnableThroatDiagnostics ? "throat diagnostics on" : "throat diagnostics off";

		_summaryLabel.Text =
			"OVERSPACE TROPHY ROOM\n" +
			$"Current world: {_currentWorldId}\n" +
			$"Current layer: {_currentLayerId}\n" +
			$"Active z-zone: {_currentZoneLabel}\n" +
			"Live link: Earth z-zone enclosure\n" +
			$"Traversal mode: {remapMode} ({diagnosticsMode})\n" +
			"Controls: WASD move, E/Q rise-fall, Shift sprint, Esc release mouse\n" +
			"Step into the orb to traverse, then re-enter the return orb to come back.";
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
		trophyRoom.AnchorIds.Add(GalleryEarthAnchorId);

		WorldNode earth = new()
		{
			WorldId = "Earth",
			DisplayName = "Earth",
			ParentWorldId = "TrophyRoom",
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
		earth.AnchorIds.Add(EarthArrivalAnchorId);

		PortalAnchor galleryEarth = CreateAnchor(GalleryEarthAnchorId, "TrophyRoom", "foyer", "Gallery/EarthOrbPortal");
		PortalAnchor earthArrival = CreateAnchor(EarthArrivalAnchorId, "Earth", "layer_1", "Worlds/EarthWorld/EarthArrivalPortal");

		PortalLink galleryToEarth = CreateLink("gallery_to_earth_z1", GalleryEarthAnchorId, EarthArrivalAnchorId, "earth_to_gallery_z1", 1.0f, 1.0f, 1.0f);
		PortalLink earthToGallery = CreateLink("earth_to_gallery_z1", EarthArrivalAnchorId, GalleryEarthAnchorId, "gallery_to_earth_z1", 1.0f, 1.0f, 1.0f);

		galleryEarth.LinkIds.Add(galleryToEarth.LinkId);
		earthArrival.LinkIds.Add(earthToGallery.LinkId);

		UniverseGraph graph = new()
		{
			GraphId = "overspace_trophy_room_demo",
			DisplayName = "Overspace Trophy Room Demo",
			RootWorldId = "TrophyRoom",
			UniverseProfile = identity
		};
		graph.Worlds.Add(trophyRoom);
		graph.Worlds.Add(earth);
		graph.Anchors.Add(galleryEarth);
		graph.Anchors.Add(earthArrival);
		graph.Links.Add(galleryToEarth);
		graph.Links.Add(earthToGallery);
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
			InfluenceRadius = 1.15f,
			Notes = $"z-zone milestone anchor for {worldId}"
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

	private void UpdateDebugOverlay()
	{
		if (_debugOverlay == null)
		{
			return;
		}

		WormholePortal sourcePortal = ResolveActivePortal();
		WormholePortal targetPortal = sourcePortal?.GetLinkedPortal();
		_debugOverlay.SetPortalPair(sourcePortal, targetPortal, _currentZoneLabel);
	}

	private WormholePortal ResolveActivePortal()
	{
		if (_portalByAnchorId.TryGetValue(_activeAnchorId, out WormholePortal portal))
		{
			return portal;
		}

		if (_portalByAnchorId.TryGetValue(GalleryEarthAnchorId, out WormholePortal fallback))
		{
			return fallback;
		}

		return null;
	}

	private void InitializeValidationMode()
	{
		string envValidation = System.Environment.GetEnvironmentVariable("OVERSPACE_AUTOVALIDATE") ?? string.Empty;
		string envCapturePath = System.Environment.GetEnvironmentVariable("OVERSPACE_CAPTURE_PATH") ?? string.Empty;
		string envCaptureProgress = System.Environment.GetEnvironmentVariable("OVERSPACE_CAPTURE_PROGRESS") ?? string.Empty;
		_validationMode = EnableAutoValidation || HasCliFlag("--overspace-autovalidate") || envValidation == "1";
		string capturePathOverride = GetCliValue("--overspace-capture-path");
		string captureProgressOverride = GetCliValue("--overspace-capture-progress");
		if (string.IsNullOrWhiteSpace(capturePathOverride) && !string.IsNullOrWhiteSpace(envCapturePath))
		{
			capturePathOverride = envCapturePath;
		}
		if (string.IsNullOrWhiteSpace(captureProgressOverride) && !string.IsNullOrWhiteSpace(envCaptureProgress))
		{
			captureProgressOverride = envCaptureProgress;
		}
		if (!string.IsNullOrWhiteSpace(capturePathOverride))
		{
			ValidationCapturePath = capturePathOverride;
		}
		if (!string.IsNullOrWhiteSpace(captureProgressOverride)
			&& float.TryParse(captureProgressOverride, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float captureProgress))
		{
			_validationCaptureProgressTarget = Mathf.Clamp(captureProgress, 0f, 1f);
			_validationPathSampleMode = true;
		}

		GD.Print($"[OverspaceValidation] mode={(_validationMode ? "enabled" : "disabled")} capture_path={ValidationCapturePath} capture_progress={_validationCaptureProgressTarget:F2}");
		if (!_validationMode || _viewerCamera == null)
		{
			return;
		}

		_validationStartPosition = _viewerCamera.GlobalPosition;

		if (_viewerCamera is FreeFlyCamera flyCamera)
		{
			flyCamera.SetInputEnabled(false);
		}

		GD.Print("[OverspaceValidation] mode=enabled");
	}

	private void RunValidationStep(double delta)
	{
		if (!_validationMode || _viewerCamera == null || _validationCaptureSaved)
		{
			return;
		}

		_validationElapsedSeconds += delta;

		if (!_validationTeleported)
		{
			if (_portalByAnchorId.TryGetValue(GalleryEarthAnchorId, out WormholePortal galleryPortal))
			{
				Vector3 target = galleryPortal.GlobalPosition + new Vector3(0f, 0f, -1.6f);
				float progress = ComputeValidationProgress(target);
				if (_validationPathSampleMode && progress >= _validationCaptureProgressTarget - 0.0005f)
				{
					GD.Print($"[OverspaceValidation] progress_capture progress={progress:F3} target={_validationCaptureProgressTarget:F3}");
					SaveValidationCapture();
					return;
				}

				Vector3 toTarget = target - _viewerCamera.GlobalPosition;
				_viewerCamera.LookAt(galleryPortal.GlobalPosition, Vector3.Up);
				if (toTarget.LengthSquared() > 0.01f)
				{
					_viewerCamera.GlobalPosition += toTarget.Normalized() * ValidationMoveSpeed * (float)delta;
				}
			}

			if (_validationElapsedSeconds >= ValidationFallbackCaptureSeconds)
			{
				GD.Print($"[OverspaceValidation] fallback_capture elapsed={_validationElapsedSeconds:F2}s");
				SaveValidationCapture();
			}

			return;
		}

		if (_validationCaptureCountdown > 0.0)
		{
			_validationCaptureCountdown -= delta;
			return;
		}

		SaveValidationCapture();
	}

	private void SaveValidationCapture()
	{
		Viewport viewport = GetViewport();
		if (viewport == null)
		{
			return;
		}

		Image image = viewport.GetTexture()?.GetImage();
		if (image == null)
		{
			return;
		}

		string absolutePath = ProjectSettings.GlobalizePath(ValidationCapturePath);
		string directory = System.IO.Path.GetDirectoryName(absolutePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			System.IO.Directory.CreateDirectory(directory);
		}

		Error saveError = image.SavePng(absolutePath);
		GD.Print($"[OverspaceValidation] capture path={absolutePath} save_error={saveError} zone={_currentZoneLabel} world={_currentWorldId} sample_mode={_validationPathSampleMode} progress_target={_validationCaptureProgressTarget:F2}");
		_validationCaptureSaved = saveError == Error.Ok;
		if (_validationCaptureSaved)
		{
			GetTree()?.Quit(0);
		}
	}

	private float ComputeValidationProgress(Vector3 target)
	{
		Vector3 path = target - _validationStartPosition;
		float pathLengthSquared = path.LengthSquared();
		if (pathLengthSquared <= Mathf.Epsilon)
		{
			return 1f;
		}

		Vector3 fromStart = _viewerCamera.GlobalPosition - _validationStartPosition;
		float projected = fromStart.Dot(path) / pathLengthSquared;
		return Mathf.Clamp(projected, 0f, 1f);
	}

	private static bool HasCliFlag(string flag)
	{
		foreach (string arg in EnumerateCliArgs())
		{
			if (string.Equals(arg, flag))
			{
				return true;
			}
		}

		return false;
	}

	private static string GetCliValue(string key)
	{
		List<string> args = EnumerateCliArgs();
		for (int i = 0; i < args.Count; i++)
		{
			string arg = args[i] ?? string.Empty;
			if (arg.StartsWith(key + "=", StringComparison.Ordinal))
			{
				return arg.Substring(key.Length + 1);
			}

			if (string.Equals(arg, key, StringComparison.Ordinal) && i + 1 < args.Count)
			{
				return args[i + 1];
			}
		}

		return string.Empty;
	}

	private static List<string> EnumerateCliArgs()
	{
		List<string> args = new();
		args.AddRange(OS.GetCmdlineArgs());
		string[] userArgs = OS.GetCmdlineUserArgs();
		for (int i = 0; i < userArgs.Length; i++)
		{
			if (!args.Contains(userArgs[i]))
			{
				args.Add(userArgs[i]);
			}
		}

		return args;
	}
}
