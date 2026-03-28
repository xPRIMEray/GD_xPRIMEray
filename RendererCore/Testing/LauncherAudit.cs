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
