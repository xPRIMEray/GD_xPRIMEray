using System;
using Godot;

public static class LauncherAudit
{
	public const string RequestedLauncherEnvVar = "XPRIMERAY_REQUESTED_LAUNCHER";

	public static string GetRequestedLauncher()
	{
		return (System.Environment.GetEnvironmentVariable(RequestedLauncherEnvVar) ?? string.Empty).Trim();
	}

	public static string ResolveFixtureTokenFromScenePath(string scenePath)
	{
		string normalized = NormalizeScenePath(scenePath);
		if (normalized.Length == 0)
		{
			return string.Empty;
		}

		if (normalized.Contains("test-straight-basic-visual-offaxis-observe", StringComparison.OrdinalIgnoreCase) ||
			normalized.Contains("test-grin-basic-visual-straight-offaxis-observe", StringComparison.OrdinalIgnoreCase))
		{
			return "grin_basic_visual_straight_offaxis";
		}
		if (normalized.Contains("test-grin-basic-visual-straight-offaxis", StringComparison.OrdinalIgnoreCase))
		{
			return "grin_basic_visual_straight_offaxis";
		}
		if (normalized.Contains("test-grin-basic-visual-minimal-offaxis", StringComparison.OrdinalIgnoreCase))
		{
			return "grin_basic_visual_minimal_offaxis";
		}
		if (normalized.Contains("test-grin-basic-visual-offaxis", StringComparison.OrdinalIgnoreCase))
		{
			return "grin_basic_visual_offaxis";
		}
		if (normalized.Contains("test-metric-basic-visual-minimal-offaxis", StringComparison.OrdinalIgnoreCase))
		{
			return "metric_basic_visual_minimal_offaxis";
		}
		if (normalized.Contains("test-metric-basic-visual-offaxis", StringComparison.OrdinalIgnoreCase))
		{
			return "metric_basic_visual_offaxis";
		}

		if (normalized.Contains("test-grin-basic-visual-straight", StringComparison.OrdinalIgnoreCase))
		{
			return "grin_basic_visual_straight";
		}
		if (normalized.Contains("test-metric-basic-visual-minimal", StringComparison.OrdinalIgnoreCase))
		{
			return "metric_basic_visual_minimal";
		}
		if (normalized.Contains("test-grin-basic-visual-linear-dual-attractor-minimal", StringComparison.OrdinalIgnoreCase))
		{
			return "grin_basic_visual_linear_dual_attractor_minimal";
		}
		if (normalized.Contains("test-grin-basic-visual-linear-offset-minimal", StringComparison.OrdinalIgnoreCase))
		{
			return "grin_basic_visual_linear_offset_minimal";
		}
		if (normalized.Contains("test-grin-basic-visual-linear-minimal", StringComparison.OrdinalIgnoreCase))
		{
			return "grin_basic_visual_linear_minimal";
		}
		if (normalized.Contains("test-metric-basic-visual", StringComparison.OrdinalIgnoreCase))
		{
			return "metric_basic_visual";
		}
		if (normalized.Contains("test-grin-basic-visual-minimal", StringComparison.OrdinalIgnoreCase))
		{
			return "grin_basic_visual_minimal";
		}
		if (normalized.Contains("test-grin-basic-visual", StringComparison.OrdinalIgnoreCase))
		{
			return "grin_basic_visual";
		}
		if (normalized.Contains("test-straight-hermetic-observatory-tile", StringComparison.OrdinalIgnoreCase))
		{
			return normalized.Contains("-quick", StringComparison.OrdinalIgnoreCase)
				? "hermetic_observatory_straight_tile_quick"
				: "hermetic_observatory_straight_tile";
		}
		if (normalized.Contains("test-grin-hermetic-observatory-tile", StringComparison.OrdinalIgnoreCase))
		{
			return normalized.Contains("-quick", StringComparison.OrdinalIgnoreCase)
				? "hermetic_observatory_grin_tile_quick"
				: "hermetic_observatory_grin_tile";
		}
		if (normalized.Contains("test-straight-hermetic-observatory", StringComparison.OrdinalIgnoreCase))
		{
			return normalized.Contains("-quick", StringComparison.OrdinalIgnoreCase)
				? "hermetic_observatory_straight_quick"
				: "hermetic_observatory_straight";
		}
		if (normalized.Contains("test-grin-hermetic-observatory", StringComparison.OrdinalIgnoreCase))
		{
			return normalized.Contains("-quick", StringComparison.OrdinalIgnoreCase)
				? "hermetic_observatory_grin_quick"
				: "hermetic_observatory_grin";
		}
		if (normalized.Contains("test-straight", StringComparison.OrdinalIgnoreCase) ||
			normalized.Contains("test_straight", StringComparison.OrdinalIgnoreCase))
		{
			return "straight";
		}
		if (normalized.Contains("curved-minimal", StringComparison.OrdinalIgnoreCase))
		{
			if (normalized.Contains("backdrop", StringComparison.OrdinalIgnoreCase) ||
				normalized.Contains("detector-plane", StringComparison.OrdinalIgnoreCase))
			{
				return "curved_minimal_backdrop";
			}
			return "curved_minimal";
		}
		if (normalized.Contains("blackhole", StringComparison.OrdinalIgnoreCase))
		{
			return "blackhole_minimal";
		}
		if (normalized.Contains("einstein", StringComparison.OrdinalIgnoreCase))
		{
			return "einstein_ring_minimal";
		}
		if (normalized.Contains("test-domain-resolver-stress", StringComparison.OrdinalIgnoreCase))
		{
			return "domain_resolver_stress";
		}
		if (normalized.Contains("test-hermetic-curved-room", StringComparison.OrdinalIgnoreCase))
		{
			return "hermetic_curved_room";
		}
		if (normalized.Contains("test-atomic-orbital-grin-room", StringComparison.OrdinalIgnoreCase))
		{
			return "atomic_orbital_grin_room";
		}
		if (normalized.Contains("test-atomic-orbital-visual-observatory", StringComparison.OrdinalIgnoreCase))
		{
			return "atomic_orbital_visual_observatory";
		}
		if (normalized.Contains("test-overspace-wormhole-checkpoint-sequence-fixture", StringComparison.OrdinalIgnoreCase))
		{
			return "overspace_wormhole_checkpoint_sequence";
		}
		if (normalized.Contains("test-overspace-wormhole-mouth-throat-interpolation-fixture", StringComparison.OrdinalIgnoreCase))
		{
			return "overspace_wormhole_mouth_throat_interpolation_sequence";
		}
		if (normalized.Contains("test-overspace-wormhole-throat-exit-interpolation-fixture", StringComparison.OrdinalIgnoreCase))
		{
			return "overspace_wormhole_throat_exit_interpolation_sequence";
		}
		if (normalized.Contains("test-overspace-wormhole-witness-mouth-fixture", StringComparison.OrdinalIgnoreCase))
		{
			return "overspace_wormhole_witness_mouth";
		}
		if (normalized.Contains("test-overspace-wormhole-witness-throat-fixture", StringComparison.OrdinalIgnoreCase))
		{
			return "overspace_wormhole_witness_throat";
		}
		if (normalized.Contains("test-overspace-wormhole-witness-exit-fixture", StringComparison.OrdinalIgnoreCase))
		{
			return "overspace_wormhole_witness_exit";
		}
		if (normalized.Contains("test-overspace-wormhole-witness-fixture", StringComparison.OrdinalIgnoreCase))
		{
			return "overspace_wormhole_witness";
		}
		if (normalized.Contains("test-overspace-hermetic-fixture-topology", StringComparison.OrdinalIgnoreCase))
		{
			return "overspace_hermetic_fixture_topology";
		}
		if (normalized.Contains("test-overspace-hermetic-fixture-field", StringComparison.OrdinalIgnoreCase))
		{
			return "overspace_hermetic_fixture_field";
		}
		if (normalized.Contains("test-overspace-hermetic-fixture", StringComparison.OrdinalIgnoreCase))
		{
			return "overspace_hermetic_fixture";
		}
		if (normalized.EndsWith("/test.tscn", StringComparison.OrdinalIgnoreCase))
		{
			return "default";
		}

		return string.Empty;
	}

	public static string GetCanonicalScenePathForFixtureToken(string fixtureToken)
	{
		if (string.IsNullOrWhiteSpace(fixtureToken))
		{
			return string.Empty;
		}

		return fixtureToken.Trim().ToLowerInvariant() switch
		{
			"straight" => "res://test-straight.tscn",
			"curved_minimal" => "res://test-curved-minimal.tscn",
			"curved_minimal_backdrop" => "res://test-curved-minimal-backdrop.tscn",
			"grin_basic_visual" => "res://test-grin-basic-visual.tscn",
			"grin_basic_visual_minimal" => "res://test-grin-basic-visual-minimal.tscn",
			"grin_basic_visual_linear_dual_attractor_minimal" => "res://test-grin-basic-visual-linear-dual-attractor-minimal.tscn",
			"grin_basic_visual_linear_offset_minimal" => "res://test-grin-basic-visual-linear-offset-minimal.tscn",
			"grin_basic_visual_linear_minimal" => "res://test-grin-basic-visual-linear-minimal.tscn",
			"grin_basic_visual_straight" => "res://test-grin-basic-visual-straight.tscn",
			"grin_basic_visual_offaxis" => "res://test-grin-basic-visual-offaxis.tscn",
			"grin_basic_visual_minimal_offaxis" => "res://test-grin-basic-visual-minimal-offaxis.tscn",
			"grin_basic_visual_straight_offaxis" => "res://test-grin-basic-visual-straight-offaxis.tscn",
			"metric_basic_visual" => "res://test-metric-basic-visual.tscn",
			"metric_basic_visual_minimal" => "res://test-metric-basic-visual-minimal.tscn",
			"metric_basic_visual_offaxis" => "res://test-metric-basic-visual-offaxis.tscn",
			"metric_basic_visual_minimal_offaxis" => "res://test-metric-basic-visual-minimal-offaxis.tscn",
			"blackhole_minimal" => "res://test-blackhole-minimal.tscn",
			"einstein_ring_minimal" => "res://test-einstein-ring-minimal.tscn",
			"domain_resolver_stress" => "res://test-domain-resolver-stress.tscn",
			"hermetic_curved_room" => "res://test-hermetic-curved-room.tscn",
			"atomic_orbital_grin_room" => "res://test-atomic-orbital-grin-room.tscn",
			"atomic_orbital_visual_observatory" => "res://test-atomic-orbital-visual-observatory.tscn",
			"overspace_hermetic_fixture" => "res://test-overspace-hermetic-fixture.tscn",
			"overspace_wormhole_witness" => "res://test-overspace-wormhole-witness-fixture.tscn",
			"overspace_wormhole_checkpoint_sequence" => "res://test-overspace-wormhole-checkpoint-sequence-fixture.tscn",
			"overspace_wormhole_mouth_throat_interpolation_sequence" => "res://test-overspace-wormhole-mouth-throat-interpolation-fixture.tscn",
			"overspace_wormhole_throat_exit_interpolation_sequence" => "res://test-overspace-wormhole-throat-exit-interpolation-fixture.tscn",
			"overspace_wormhole_witness_mouth" => "res://test-overspace-wormhole-witness-mouth-fixture.tscn",
			"overspace_wormhole_witness_throat" => "res://test-overspace-wormhole-witness-throat-fixture.tscn",
			"overspace_wormhole_witness_exit" => "res://test-overspace-wormhole-witness-exit-fixture.tscn",
			"overspace_hermetic_fixture_topology" => "res://test-overspace-hermetic-fixture-topology.tscn",
			"overspace_hermetic_fixture_field" => "res://test-overspace-hermetic-fixture-field.tscn",
			"hermetic_observatory_straight" => "res://test-straight-hermetic-observatory-v0-pre.tscn",
			"hermetic_observatory_grin" => "res://test-grin-hermetic-observatory-v0-pre.tscn",
			"hermetic_observatory_straight_quick" => "res://test-straight-hermetic-observatory-quick.tscn",
			"hermetic_observatory_grin_quick" => "res://test-grin-hermetic-observatory-quick.tscn",
			"hermetic_observatory_straight_tile" => "res://test-straight-hermetic-observatory-tile-v0.tscn",
			"hermetic_observatory_grin_tile" => "res://test-grin-hermetic-observatory-tile-v0.tscn",
			"hermetic_observatory_straight_tile_quick" => "res://test-straight-hermetic-observatory-tile-quick.tscn",
			"hermetic_observatory_grin_tile_quick" => "res://test-grin-hermetic-observatory-tile-quick.tscn",
			"default" => "res://test.tscn",
			_ => string.Empty
		};
	}

	public static bool LogAndValidateStartup(
		string expectedScenePath,
		string expectedFixtureToken,
		string actualScenePath,
		string modeToken,
		bool enforceMatch)
	{
		string requestedLauncher = GetRequestedLauncher();
		string normalizedExpectedScene = NormalizeScenePath(expectedScenePath);
		string normalizedActualScene = NormalizeScenePath(actualScenePath);
		string normalizedExpectedFixture = NormalizeFixtureToken(expectedFixtureToken);
		string actualFixture = NormalizeFixtureToken(ResolveFixtureTokenFromScenePath(normalizedActualScene));

		bool sceneMatch = normalizedExpectedScene.Length == 0 ||
			ScenesMatchOrAlias(normalizedExpectedScene, normalizedActualScene);
		bool fixtureMatch = normalizedExpectedFixture.Length == 0 ||
			string.Equals(normalizedExpectedFixture, actualFixture, StringComparison.OrdinalIgnoreCase);
		bool ok = sceneMatch && fixtureMatch;
		string requestedLauncherValue = requestedLauncher.Length == 0 ? "manual" : requestedLauncher;
		string actualSceneValue = normalizedActualScene.Length == 0 ? "unknown" : normalizedActualScene;
		string actualFixtureValue = actualFixture.Length == 0 ? "unknown" : actualFixture;
		string expectedSceneValue = normalizedExpectedScene.Length == 0 ? "n/a" : normalizedExpectedScene;
		string expectedFixtureValue = normalizedExpectedFixture.Length == 0 ? "n/a" : normalizedExpectedFixture;
		string modeValue = string.IsNullOrWhiteSpace(modeToken) ? "UNKNOWN" : modeToken.Trim();
		string message =
			$"[LaunchAudit] requested_launcher={requestedLauncherValue} " +
			$"actual_scene={actualSceneValue} actual_fixture={actualFixtureValue} mode={modeValue} " +
			$"expected_scene={expectedSceneValue} expected_fixture={expectedFixtureValue} " +
			$"scene_match={(sceneMatch ? 1 : 0)} fixture_match={(fixtureMatch ? 1 : 0)}";

		if (ok)
		{
			GD.Print($"{message} status=ok");
			return true;
		}

		if (enforceMatch)
		{
			GD.PrintErr($"{message} status=fail");
			return false;
		}

		GD.Print($"{message} status=warn");
		return false;
	}

	private static string NormalizeFixtureToken(string fixtureToken)
	{
		return string.IsNullOrWhiteSpace(fixtureToken) ? string.Empty : fixtureToken.Trim();
	}

	private static string NormalizeScenePath(string scenePath)
	{
		if (string.IsNullOrWhiteSpace(scenePath))
		{
			return string.Empty;
		}

		return scenePath.Trim().Replace('\\', '/');
	}

	private static bool ScenesMatchOrAlias(string expectedScenePath, string actualScenePath)
	{
		if (string.Equals(expectedScenePath, actualScenePath, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return
			IsObserveAlias(expectedScenePath, actualScenePath, "res://test-grin-basic-visual-offaxis.tscn", "res://test-grin-basic-visual-offaxis-observe.tscn") ||
			IsObserveAlias(expectedScenePath, actualScenePath, "res://test-grin-basic-visual-minimal-offaxis.tscn", "res://test-grin-basic-visual-minimal-offaxis-observe.tscn") ||
			IsObserveAlias(expectedScenePath, actualScenePath, "res://test-metric-basic-visual-offaxis.tscn", "res://test-metric-basic-visual-offaxis-observe.tscn") ||
			IsObserveAlias(expectedScenePath, actualScenePath, "res://test-metric-basic-visual-minimal-offaxis.tscn", "res://test-metric-basic-visual-minimal-offaxis-observe.tscn") ||
			IsObserveAlias(expectedScenePath, actualScenePath, "res://test-grin-basic-visual-straight-offaxis.tscn", "res://test-straight-basic-visual-offaxis-observe.tscn") ||
			IsObserveAlias(expectedScenePath, actualScenePath, "res://test-grin-basic-visual-straight-offaxis.tscn", "res://test-grin-basic-visual-straight-offaxis-observe.tscn");
	}

	private static bool IsObserveAlias(string expectedScenePath, string actualScenePath, string canonicalPath, string observePath)
	{
		return string.Equals(expectedScenePath, canonicalPath, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(actualScenePath, observePath, StringComparison.OrdinalIgnoreCase);
	}
}
