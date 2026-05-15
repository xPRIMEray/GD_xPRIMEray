using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public partial class TestBenchController : Node
{
	private const string RegistryPath = "res://UI/testbench_recipes.json";
	private readonly object _logLock = new();
	private readonly StringBuilder _logBuffer = new();
	private readonly JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	private RecipeRegistry _registry = new();
	private Process _activeProcess;
	private RunPlan _lastPlan;
	private RunManifest _lastManifest;
	private string _repoRoot = string.Empty;
	private string _outputRoot = string.Empty;
	private bool _isRunning;
	private bool _stopRequested;

	public override void _Ready()
	{
		_repoRoot = NormalizePath(ProjectSettings.GlobalizePath("res://"));
		_outputRoot = NormalizePath(Path.Combine(_repoRoot, "output"));
		LoadRegistry();
	}

	public string GetRecipeCatalogJson()
	{
		LoadRegistry();
		var rows = _registry.Recipes.Select(recipe => new
		{
			recipe.id,
				recipe.title,
				recipe.allowedOverlayModes,
				recipe.allowedPlacements,
				qualityPresets = GetQualityPresets(recipe),
				defaultQuality = GetDefaultQuality(recipe),
				recipe.guardrailText
			}).ToList();
		return JsonSerializer.Serialize(rows, _jsonOptions);
	}

	public string DryRunJson(string recipeId, string quality, string overlayMode, string placement)
	{
		try
		{
			RunPlan plan = BuildRunPlan(recipeId, quality, overlayMode, placement);
			_lastPlan = plan;
			_lastManifest = BuildManifest(plan, dryRun: true, exitCode: null, status: "dry_run");
			WriteManifest(_lastManifest);
			ReplaceLog($"[testbench] dry_run recipe={recipeId} quality={quality} overlay={overlayMode} placement={placement}\n{plan.ExactCommand}\n");
			return SerializeResult(true, "Dry run ready.", _lastManifest);
		}
		catch (Exception exc)
		{
			return SerializeResult(false, exc.Message, null);
		}
	}

	public string RunJson(string recipeId, string quality, string overlayMode, string placement)
	{
		try
		{
			if (_isRunning)
			{
				return SerializeResult(false, "A Test Bench run is already active.", _lastManifest);
			}

			RunPlan plan = BuildRunPlan(recipeId, quality, overlayMode, placement);
			if (string.Equals(plan.Quality, "full", StringComparison.OrdinalIgnoreCase) && !HasSuccessfulSmokeManifest(plan.Recipe.id))
			{
				_lastPlan = plan;
				_lastManifest = BuildManifest(plan, dryRun: true, exitCode: null, status: "blocked_full_requires_smoke");
				WriteManifest(_lastManifest);
				return SerializeResult(false, "Run selected smoke first. This recipe has no successful smoke manifest yet.", _lastManifest);
			}

			_lastPlan = plan;
			_lastManifest = BuildManifest(plan, dryRun: false, exitCode: null, status: "running");
			WriteManifest(_lastManifest);
			ReplaceLog($"[testbench] launch recipe={recipeId} quality={quality} overlay={overlayMode} placement={placement}\n{plan.ExactCommand}\n");
			StartProcess(plan);
			return SerializeResult(true, "Run started.", _lastManifest);
		}
		catch (Exception exc)
		{
			return SerializeResult(false, exc.Message, null);
		}
	}

	public string StopRun()
	{
		try
		{
			if (_activeProcess == null || _activeProcess.HasExited)
			{
				return "No active Test Bench run.";
			}

			AppendLog("[testbench] stop requested\n");
			_stopRequested = true;
			_activeProcess.Kill(entireProcessTree: true);
			return "Stop requested.";
		}
		catch (Exception exc)
		{
			return $"Stop failed: {exc.Message}";
		}
	}

	public string DrainLog()
	{
		lock (_logLock)
		{
			return _logBuffer.ToString();
		}
	}

	public string GetStatusJson()
	{
		return JsonSerializer.Serialize(new
		{
			running = _isRunning,
			manifest = _lastManifest,
			command = _lastPlan?.ExactCommand ?? string.Empty
		}, _jsonOptions);
	}

	public string CopyExactCommand()
	{
		if (_lastPlan == null)
		{
			return "No command has been prepared yet.";
		}

		DisplayServer.ClipboardSet(_lastPlan.ExactCommand);
		return "Exact command copied.";
	}

	public string OpenLatestFolder()
	{
		string path = _lastManifest?.artifacts?.folder ?? _lastManifest?.planned_output_folder ?? string.Empty;
		return OpenAllowedPath(path);
	}

	public string OpenReport()
	{
		return OpenAllowedPath(_lastManifest?.artifacts?.report ?? string.Empty);
	}

	public string OpenContactSheet()
	{
		return OpenAllowedPath(_lastManifest?.artifacts?.contact_sheet ?? string.Empty);
	}

	private void LoadRegistry()
	{
		string path = ProjectSettings.GlobalizePath(RegistryPath);
		if (!File.Exists(path))
		{
			_registry = new RecipeRegistry();
			return;
		}

		string json = File.ReadAllText(path);
		_registry = JsonSerializer.Deserialize<RecipeRegistry>(json, _jsonOptions) ?? new RecipeRegistry();
		_registry.Recipes ??= new List<TestBenchRecipe>();
	}

	private RunPlan BuildRunPlan(string recipeId, string quality, string overlayMode, string placement)
	{
		LoadRegistry();
		TestBenchRecipe recipe = _registry.Recipes.FirstOrDefault(r => string.Equals(r.id, recipeId, StringComparison.Ordinal))
			?? throw new InvalidOperationException($"Unknown recipe: {recipeId}");

			quality = NormalizeEnum(quality, GetQualityPresets(recipe), GetDefaultQuality(recipe));
		overlayMode = NormalizeEnum(overlayMode, recipe.allowedOverlayModes, "none");
		placement = NormalizeEnum(placement, recipe.allowedPlacements, "none");

		DateTime startedAtUtc = DateTime.UtcNow;
		string timestamp = startedAtUtc.ToString("yyyyMMddTHHmmssZ");
		string workingDirectory = ResolveRepoPath(string.IsNullOrWhiteSpace(recipe.workingDirectory) ? "." : recipe.workingDirectory);
		string scriptPath = ResolveRepoPath(recipe.script);
		if (!File.Exists(scriptPath))
		{
			throw new FileNotFoundException("Recipe script is missing.", scriptPath);
		}

		string plannedOutputFolder = ResolveOutputFolder(recipe, timestamp);
		Dictionary<string, string> env = BuildEnvironment(recipe, quality, overlayMode, plannedOutputFolder);
		List<string> args = new();
		foreach (string arg in recipe.scriptArgs ?? new List<string>())
		{
			args.Add(ExpandTemplate(arg, timestamp, plannedOutputFolder));
		}

		string fileName = "bash";
		List<string> processArgs = new() { scriptPath };
		processArgs.AddRange(args);
		string exactCommand = BuildExactCommand(env, fileName, processArgs, workingDirectory);
		return new RunPlan(recipe, quality, overlayMode, placement, timestamp, startedAtUtc, workingDirectory, scriptPath, plannedOutputFolder, env, fileName, processArgs, exactCommand);
	}

	private Dictionary<string, string> BuildEnvironment(TestBenchRecipe recipe, string quality, string overlayMode, string plannedOutputFolder)
	{
		Dictionary<string, string> env = new(StringComparer.Ordinal);
		if (recipe.qualityEnv != null && recipe.qualityEnv.TryGetValue(quality, out Dictionary<string, string> qualityEnv))
		{
			foreach ((string key, string value) in qualityEnv)
			{
				env[key] = value;
			}
		}

		if (recipe.overlayEnv != null && recipe.overlayEnv.TryGetValue(overlayMode, out Dictionary<string, string> overlayEnv))
		{
			foreach ((string key, string value) in overlayEnv)
			{
				env[key] = value;
			}
		}

		if (!string.IsNullOrWhiteSpace(recipe.rootEnv))
		{
			env[recipe.rootEnv] = plannedOutputFolder;
		}

		return env;
	}

	private string ResolveOutputFolder(TestBenchRecipe recipe, string timestamp)
	{
		if (recipe.testbenchWrappedOutput)
		{
			return NormalizePath(Path.Combine(_outputRoot, "testbench", recipe.id, timestamp));
		}

		string root = ResolveRepoPath(recipe.outputRoot);
		if ((recipe.scriptArgs ?? new List<string>()).Any(a => a.Contains("{timestamp}", StringComparison.Ordinal)))
		{
			return NormalizePath(Path.Combine(root, timestamp));
		}

		return NormalizePath(Path.Combine(root, timestamp));
	}

	private void StartProcess(RunPlan plan)
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = plan.FileName,
			WorkingDirectory = plan.WorkingDirectory,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		foreach (string arg in plan.ProcessArgs)
		{
			startInfo.ArgumentList.Add(arg);
		}

		foreach ((string key, string value) in plan.Environment)
		{
			startInfo.Environment[key] = value;
		}

		_activeProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
		_activeProcess.OutputDataReceived += (_, e) => { if (e.Data != null) AppendLog(e.Data + "\n"); };
		_activeProcess.ErrorDataReceived += (_, e) => { if (e.Data != null) AppendLog(e.Data + "\n"); };
		_activeProcess.Exited += (_, _) => CallDeferred(nameof(OnProcessExitedDeferred));
		_isRunning = true;
		_stopRequested = false;
		_activeProcess.Start();
		_activeProcess.BeginOutputReadLine();
		_activeProcess.BeginErrorReadLine();
	}

	public void OnProcessExitedDeferred()
	{
		int exitCode = _activeProcess?.ExitCode ?? -1;
		_isRunning = false;
		AppendLog($"[testbench] process exited code={exitCode}\n");
		if (_lastPlan != null)
		{
			string status = (_stopRequested || exitCode == 137 || exitCode == 143) ? "incomplete" : exitCode == 0 ? "completed" : "completed_with_errors";
			_lastManifest = BuildManifest(_lastPlan, dryRun: false, exitCode: exitCode, status: status);
			if (!string.IsNullOrWhiteSpace(_lastManifest.cockpit_summary))
			{
				AppendLog($"[testbench][summary] {_lastManifest.cockpit_summary}\n");
			}
			WriteManifest(_lastManifest);
			UpdateSuccessfulSmokeCache(_lastManifest);
		}
		_activeProcess?.Dispose();
		_activeProcess = null;
		_stopRequested = false;
	}

	private RunManifest BuildManifest(RunPlan plan, bool dryRun, int? exitCode, string status)
	{
		ArtifactSet artifacts = dryRun
			? BuildExpectedArtifacts(plan)
			: DiscoverArtifacts(plan);
		if (!dryRun)
		{
			ApplyPreviewAliases(plan, artifacts);
		}

		DateTime updatedAtUtc = DateTime.UtcNow;
		double? durationSeconds = dryRun ? null : Math.Max(0.0, (updatedAtUtc - plan.StartedAtUtc).TotalSeconds);
		string durationText = durationSeconds.HasValue ? FormatDuration(durationSeconds.Value) : string.Empty;

		return new RunManifest
		{
			schema = "xprimeray.testbench.run_manifest.v1",
			recipe_id = plan.Recipe.id,
			recipe_title = plan.Recipe.title,
			quality = plan.Quality,
			overlay = new OverlaySelection
			{
				mode = plan.OverlayMode,
				placement = plan.Placement,
				postprocess_only = true,
				placement_descriptive_only = true
			},
			exact_command = plan.ExactCommand,
			working_directory = plan.WorkingDirectory,
			env = plan.Environment,
			planned_output_folder = plan.PlannedOutputFolder,
			testbench_wrapped_output = plan.Recipe.testbenchWrappedOutput,
			source_script_artifacts_preserved = true,
			dry_run = dryRun,
			status = status,
			exit_code = exitCode,
			started_utc = plan.StartedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
			updated_utc = updatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
			duration_seconds = durationSeconds,
			duration_text = durationText,
			smoke_success_known = HasSuccessfulSmokeManifest(plan.Recipe.id),
			artifacts = artifacts,
			cockpit_summary = dryRun ? BuildDryRunSummary(plan, artifacts) : BuildCockpitSummary(plan, artifacts, status, durationText),
			guardrail_text = plan.Recipe.guardrailText,
			visual_grammar = VisualGrammar.Build()
		};
	}

	private ArtifactSet BuildExpectedArtifacts(RunPlan plan)
	{
		string folder = plan.PlannedOutputFolder;
		return new ArtifactSet
		{
			folder = folder,
			expected_reports = ExpectedPaths(folder, plan.Recipe.expectedReports),
			expected_contact_sheets = ExpectedPaths(folder, plan.Recipe.expectedContactSheets),
			expected_summaries = ExpectedPaths(folder, plan.Recipe.expectedSummaries),
			expected_previews = ExpectedPaths(folder, plan.Recipe.expectedPreviews)
		};
	}

	private ArtifactSet DiscoverArtifacts(RunPlan plan)
	{
		string folder = Directory.Exists(plan.PlannedOutputFolder)
			? plan.PlannedOutputFolder
			: FindLatestDirectory(ResolveRepoPath(plan.Recipe.outputRoot));

		ArtifactSet set = BuildExpectedArtifacts(plan);
		set.folder = folder;
		if (Directory.Exists(folder))
		{
			set.report = FindFirstArtifact(folder, plan.Recipe.expectedReports, new[] { "*_report.md", "*_summary.md", "storytelling_sequence.md" });
			set.contact_sheet = FindFirstArtifact(folder, plan.Recipe.expectedContactSheets, new[] { "*contact_sheet*.png", "diagnostic_overlay_contact_sheet.png", "*storyboard*.png", "*ladder.png" });
			set.summary = FindFirstArtifact(folder, plan.Recipe.expectedSummaries, new[] { "*_summary.json", "summary.json" });
			set.preview = FindFirstArtifact(folder, plan.Recipe.expectedPreviews, new[] { "*.png" });
		}
		return set;
	}

	private void ApplyPreviewAliases(RunPlan plan, ArtifactSet artifacts)
	{
		if (artifacts == null ||
			string.IsNullOrWhiteSpace(artifacts.folder) ||
			!Directory.Exists(artifacts.folder) ||
			!IsOutputPathAllowed(artifacts.folder))
		{
			return;
		}

		if (string.Equals(plan.Recipe.id, "hermetic_closure_smoke", StringComparison.Ordinal))
		{
			string source = FindPattern(artifacts.folder, "hermetic_closure_ladder.png");
			if (File.Exists(source))
			{
				string alias = Path.Combine(artifacts.folder, "testbench_preview.png");
				File.Copy(source, alias, overwrite: true);
				artifacts.preview = NormalizePath(alias);
			}
		}
	}

	private string BuildDryRunSummary(RunPlan plan, ArtifactSet artifacts)
	{
		return $"{plan.Recipe.title}: dry run prepared; command/env built; expected artifacts listed.";
	}

	private string BuildCockpitSummary(RunPlan plan, ArtifactSet artifacts, string status, string durationText)
	{
		string durationSuffix = string.IsNullOrWhiteSpace(durationText) ? string.Empty : $"; duration {durationText}";
		if (artifacts == null)
		{
			return $"{plan.Recipe.title}: run complete; artifacts not discovered{durationSuffix}.";
		}

		if (string.Equals(status, "incomplete", StringComparison.Ordinal))
		{
			if (string.IsNullOrWhiteSpace(artifacts.contact_sheet))
			{
				return $"{plan.Recipe.title}: run incomplete before contact sheet generation{durationSuffix}.";
			}
			return $"{plan.Recipe.title}: run incomplete; partial artifacts preserved{durationSuffix}.";
		}

		if (string.Equals(plan.Recipe.id, "hermetic_closure_smoke", StringComparison.Ordinal))
		{
			string summary = BuildHermeticCockpitSummary(plan, artifacts);
			if (!string.IsNullOrWhiteSpace(summary))
			{
				return string.IsNullOrWhiteSpace(durationText) ? summary : summary.TrimEnd('.') + $"; duration {durationText}.";
			}
		}

		if (string.Equals(plan.Recipe.id, "wormhole_structure_observatory", StringComparison.Ordinal))
		{
			string summary = BuildWormholeStructureCockpitSummary(plan, artifacts, durationText);
			if (!string.IsNullOrWhiteSpace(summary))
			{
				return summary;
			}
		}

		string reportStatus = !string.IsNullOrWhiteSpace(artifacts.report) ? "report found" : "report missing";
		string contactStatus = !string.IsNullOrWhiteSpace(artifacts.contact_sheet) ? "contact sheet found" : "contact sheet missing";
		return $"{plan.Recipe.title}: {reportStatus}; {contactStatus}{durationSuffix}.";
	}

	private string BuildWormholeStructureCockpitSummary(RunPlan plan, ArtifactSet artifacts, string durationText)
	{
		string summaryPath = artifacts.summary;
		if (string.IsNullOrWhiteSpace(summaryPath) || !File.Exists(summaryPath))
		{
			return string.Empty;
		}

		try
		{
			using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(summaryPath));
			JsonElement root = doc.RootElement;
			int complete = (int)Math.Round(ReadJsonDouble(root, "complete_panel_count", 0.0));
			int total = (int)Math.Round(ReadJsonDouble(root, "panel_count", 0.0));
			List<string> incomplete = new();
			if (root.TryGetProperty("panels", out JsonElement panels) && panels.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement panel in panels.EnumerateArray())
				{
					string status = panel.TryGetProperty("status", out JsonElement statusElement) ? statusElement.GetString() ?? "" : "";
					if (!string.Equals(status, "ok", StringComparison.Ordinal))
					{
						string panelId = panel.TryGetProperty("panel_id", out JsonElement idElement) ? idElement.GetString() ?? "panel" : "panel";
						incomplete.Add(panelId);
					}
				}
			}

			string contactStatus = !string.IsNullOrWhiteSpace(artifacts.contact_sheet) ? "contact sheet found" : "contact sheet missing";
			string durationSuffix = string.IsNullOrWhiteSpace(durationText) ? string.Empty : $"; duration {durationText}";
			if (incomplete.Count == 0)
			{
				return $"{plan.Recipe.title}: panels {complete}/{total}; {contactStatus}{durationSuffix}.";
			}
			return $"{plan.Recipe.title}: panels {complete}/{total} complete; {string.Join(", ", incomplete)} incomplete; {contactStatus}{durationSuffix}.";
		}
		catch
		{
			return string.Empty;
		}
	}

	private string BuildHermeticCockpitSummary(RunPlan plan, ArtifactSet artifacts)
	{
		string summaryPath = artifacts.summary;
		if (string.IsNullOrWhiteSpace(summaryPath) || !File.Exists(summaryPath))
		{
			return string.Empty;
		}

		try
		{
			using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(summaryPath));
			if (!doc.RootElement.TryGetProperty("summary_rows", out JsonElement rows) ||
				rows.ValueKind != JsonValueKind.Array)
			{
				return string.Empty;
			}

			JsonElement? best = null;
			double bestBudget = double.MinValue;
			foreach (JsonElement row in rows.EnumerateArray())
			{
				double budget = ReadJsonDouble(row, "steps_per_ray", 0.0);
				if (budget >= bestBudget)
				{
					bestBudget = budget;
					best = row;
				}
			}

			if (!best.HasValue)
			{
				return string.Empty;
			}

			JsonElement high = best.Value;
			int hits = (int)Math.Round(ReadJsonDouble(high, "hit_pixel_count", 0.0));
			int total = (int)Math.Round(ReadJsonDouble(high, "total_pixels", 0.0));
			double closure = ReadJsonDouble(high, "hit_closure_percent", 0.0);
			string reportStatus = !string.IsNullOrWhiteSpace(artifacts.report) ? "report found" : "report missing";
			return $"{plan.Recipe.title}: recovered {hits}/{total} pixels; high-budget closure {closure:0.###}%; {reportStatus}.";
		}
		catch
		{
			return string.Empty;
		}
	}

	private static double ReadJsonDouble(JsonElement element, string propertyName, double fallback)
	{
		if (!element.TryGetProperty(propertyName, out JsonElement value))
		{
			return fallback;
		}

		return value.ValueKind switch
		{
			JsonValueKind.Number when value.TryGetDouble(out double numeric) => numeric,
			JsonValueKind.String when double.TryParse(value.GetString(), out double numeric) => numeric,
			_ => fallback
		};
	}

	private void WriteManifest(RunManifest manifest)
	{
		string cacheFolder = NormalizePath(Path.Combine(_outputRoot, "testbench", "_manifests", manifest.recipe_id, manifest.started_utc.Replace("-", "").Replace(":", "")));
		Directory.CreateDirectory(cacheFolder);
		string primaryFolder = cacheFolder;
		if (!manifest.dry_run &&
			manifest.artifacts != null &&
			!string.IsNullOrWhiteSpace(manifest.artifacts.folder) &&
			Directory.Exists(manifest.artifacts.folder) &&
			IsOutputPathAllowed(manifest.artifacts.folder))
		{
			primaryFolder = NormalizePath(manifest.artifacts.folder);
		}

		manifest.manifest_path = Path.Combine(primaryFolder, "testbench_run_manifest.json");
		WriteManifestFiles(primaryFolder, manifest);
		if (!string.Equals(primaryFolder, cacheFolder, StringComparison.Ordinal))
		{
			WriteManifestFiles(cacheFolder, manifest);
		}
	}

	private void WriteManifestFiles(string folder, RunManifest manifest)
	{
		Directory.CreateDirectory(folder);
		File.WriteAllText(Path.Combine(folder, "testbench_run_manifest.json"), JsonSerializer.Serialize(manifest, _jsonOptions) + "\n");
		File.WriteAllText(Path.Combine(folder, "testbench_command.txt"), manifest.exact_command + "\n");
		File.WriteAllText(Path.Combine(folder, "testbench_stdout.log"), DrainLog());
	}

	private bool HasSuccessfulSmokeManifest(string recipeId)
	{
		string cache = SmokeCachePath(recipeId);
		if (File.Exists(cache))
		{
			try
			{
				RunManifest cached = JsonSerializer.Deserialize<RunManifest>(File.ReadAllText(cache), _jsonOptions);
				if (cached != null &&
					string.Equals(cached.recipe_id, recipeId, StringComparison.Ordinal) &&
					string.Equals(cached.quality, "smoke", StringComparison.OrdinalIgnoreCase) &&
					cached.exit_code == 0 &&
					!cached.dry_run)
				{
					return true;
				}
			}
			catch
			{
				// Fall back to scanning historical manifests.
			}
		}

		string root = Path.Combine(_outputRoot, "testbench");
		if (!Directory.Exists(root))
		{
			return false;
		}

		foreach (string path in Directory.EnumerateFiles(root, "testbench_run_manifest.json", SearchOption.AllDirectories))
		{
			try
			{
				RunManifest manifest = JsonSerializer.Deserialize<RunManifest>(File.ReadAllText(path), _jsonOptions);
				if (manifest != null &&
					string.Equals(manifest.recipe_id, recipeId, StringComparison.Ordinal) &&
					string.Equals(manifest.quality, "smoke", StringComparison.OrdinalIgnoreCase) &&
					manifest.exit_code == 0 &&
					!manifest.dry_run)
				{
					return true;
				}
			}
			catch
			{
				// Ignore malformed historical manifests.
			}
		}
		return false;
	}

	private void UpdateSuccessfulSmokeCache(RunManifest manifest)
	{
		if (manifest == null ||
			manifest.dry_run ||
			manifest.exit_code != 0 ||
			!string.Equals(manifest.quality, "smoke", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		string path = SmokeCachePath(manifest.recipe_id);
		Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _outputRoot);
		File.WriteAllText(path, JsonSerializer.Serialize(manifest, _jsonOptions) + "\n");
	}

	private string SmokeCachePath(string recipeId)
	{
		string safe = string.Join("_", (recipeId ?? string.Empty).Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
		return NormalizePath(Path.Combine(_outputRoot, "testbench", "_smoke_cache", safe + ".json"));
	}

	private string OpenAllowedPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return "No artifact path is available yet.";
		}

		string full = NormalizePath(Path.IsPathRooted(path) ? path : Path.Combine(_repoRoot, path));
		if (!IsOutputPathAllowed(full))
		{
			return "Refused to open path outside the repo output folder.";
		}

		if (!File.Exists(full) && !Directory.Exists(full))
		{
			return $"Path does not exist: {full}";
		}

		Error err = OS.ShellOpen(full);
		return err == Error.Ok ? $"Opened: {full}" : $"Open failed: {err}";
	}

	private bool IsOutputPathAllowed(string path)
	{
		string full = NormalizePath(path);
		return full.StartsWith(_outputRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
			string.Equals(full, _outputRoot, StringComparison.Ordinal);
	}

	private string FindLatestDirectory(string root)
	{
		if (!Directory.Exists(root))
		{
			return root;
		}

		DirectoryInfo latest = new DirectoryInfo(root).EnumerateDirectories()
			.OrderByDescending(d => d.LastWriteTimeUtc)
			.FirstOrDefault();
		return NormalizePath(latest?.FullName ?? root);
	}

	private string FindFirstArtifact(string folder, List<string> expected, string[] fallbacks)
	{
		foreach (string pattern in expected ?? new List<string>())
		{
			string found = FindPattern(folder, pattern);
			if (!string.IsNullOrWhiteSpace(found))
			{
				return found;
			}
		}
		foreach (string pattern in fallbacks)
		{
			string found = FindPattern(folder, pattern);
			if (!string.IsNullOrWhiteSpace(found))
			{
				return found;
			}
		}
		return string.Empty;
	}

	private string FindPattern(string folder, string pattern)
	{
		if (string.IsNullOrWhiteSpace(pattern) || !Directory.Exists(folder))
		{
			return string.Empty;
		}

		string direct = Path.Combine(folder, pattern);
		if (!pattern.Contains('*') && File.Exists(direct))
		{
			return NormalizePath(direct);
		}

		string search = Path.GetFileName(pattern);
		string relativeDir = Path.GetDirectoryName(pattern);
		string searchRoot = string.IsNullOrWhiteSpace(relativeDir) ? folder : Path.Combine(folder, relativeDir);
		if (!Directory.Exists(searchRoot))
		{
			searchRoot = folder;
		}

		return Directory.EnumerateFiles(searchRoot, search, SearchOption.AllDirectories)
			.OrderBy(p => p.Length)
			.Select(NormalizePath)
			.FirstOrDefault() ?? string.Empty;
	}

	private List<string> ExpectedPaths(string folder, List<string> names)
	{
		return (names ?? new List<string>()).Select(name => NormalizePath(Path.Combine(folder, name))).ToList();
	}

	private string ResolveRepoPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path) || path == ".")
		{
			return _repoRoot;
		}
		return NormalizePath(Path.IsPathRooted(path) ? path : Path.Combine(_repoRoot, path));
	}

	private static string NormalizePath(string path)
	{
		return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}

	private static string NormalizeEnum(string value, IEnumerable<string> allowed, string fallback)
	{
		HashSet<string> allowedSet = new(allowed ?? Array.Empty<string>(), StringComparer.Ordinal);
		return allowedSet.Contains(value) ? value : fallback;
	}

	private static string FormatDuration(double seconds)
	{
		if (seconds < 60.0)
		{
			return $"{seconds:0}s";
		}
		int whole = (int)Math.Round(seconds);
		return $"{whole / 60}m {whole % 60:00}s";
	}

	private static List<string> GetQualityPresets(TestBenchRecipe recipe)
	{
		if (recipe.qualityPresets != null && recipe.qualityPresets.Count > 0)
		{
			return recipe.qualityPresets;
		}
		if (recipe.qualityEnv != null && recipe.qualityEnv.Count > 0)
		{
			return recipe.qualityEnv.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
		}
		return new List<string> { "smoke", "review", "full" };
	}

	private static string GetDefaultQuality(TestBenchRecipe recipe)
	{
		List<string> presets = GetQualityPresets(recipe);
		if (!string.IsNullOrWhiteSpace(recipe.defaultQuality) && presets.Contains(recipe.defaultQuality))
		{
			return recipe.defaultQuality;
		}
		return presets.FirstOrDefault() ?? "smoke";
	}

	private static string ExpandTemplate(string value, string timestamp, string outputFolder)
	{
		return (value ?? string.Empty)
			.Replace("{timestamp}", timestamp, StringComparison.Ordinal)
			.Replace("{outputFolder}", outputFolder, StringComparison.Ordinal);
	}

	private static string BuildExactCommand(Dictionary<string, string> env, string fileName, List<string> args, string workingDirectory)
	{
		StringBuilder builder = new();
		builder.Append("cd ").Append(ShellQuote(workingDirectory)).Append(" && ");
		foreach ((string key, string value) in env.OrderBy(kv => kv.Key, StringComparer.Ordinal))
		{
			builder.Append(key).Append('=').Append(ShellQuote(value)).Append(' ');
		}
		builder.Append(ShellQuote(fileName));
		foreach (string arg in args)
		{
			builder.Append(' ').Append(ShellQuote(arg));
		}
		return builder.ToString();
	}

	private static string ShellQuote(string value)
	{
		return "'" + (value ?? string.Empty).Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
	}

	private string SerializeResult(bool ok, string message, RunManifest manifest)
	{
		return JsonSerializer.Serialize(new
		{
			ok,
			message,
			manifest,
			running = _isRunning
		}, _jsonOptions);
	}

	private void ReplaceLog(string text)
	{
		lock (_logLock)
		{
			_logBuffer.Clear();
			_logBuffer.Append(text);
		}
	}

	private void AppendLog(string text)
	{
		lock (_logLock)
		{
			_logBuffer.Append(text);
			const int maxChars = 120000;
			if (_logBuffer.Length > maxChars)
			{
				_logBuffer.Remove(0, _logBuffer.Length - maxChars);
			}
		}
	}

	private sealed record RunPlan(
		TestBenchRecipe Recipe,
		string Quality,
		string OverlayMode,
		string Placement,
		string Timestamp,
		DateTime StartedAtUtc,
		string WorkingDirectory,
		string ScriptPath,
		string PlannedOutputFolder,
		Dictionary<string, string> Environment,
		string FileName,
		List<string> ProcessArgs,
		string ExactCommand);

	private sealed class RecipeRegistry
	{
		public string schema { get; set; } = string.Empty;
		public List<TestBenchRecipe> Recipes { get; set; } = new();
	}

	private sealed class TestBenchRecipe
	{
		public string id { get; set; } = string.Empty;
		public string title { get; set; } = string.Empty;
		public string script { get; set; } = string.Empty;
		public string workingDirectory { get; set; } = ".";
		public List<string> scriptArgs { get; set; } = new();
		public bool testbenchWrappedOutput { get; set; }
		public string outputRoot { get; set; } = "output";
		public string rootEnv { get; set; } = string.Empty;
		public List<string> qualityPresets { get; set; } = new();
		public string defaultQuality { get; set; } = string.Empty;
		public Dictionary<string, Dictionary<string, string>> qualityEnv { get; set; } = new();
		public List<string> allowedOverlayModes { get; set; } = new() { "none" };
		public List<string> allowedPlacements { get; set; } = new() { "none" };
		public Dictionary<string, Dictionary<string, string>> overlayEnv { get; set; } = new();
		public List<string> expectedReports { get; set; } = new();
		public List<string> expectedContactSheets { get; set; } = new();
		public List<string> expectedSummaries { get; set; } = new();
		public List<string> expectedPreviews { get; set; } = new();
		public string guardrailText { get; set; } = string.Empty;
	}

	private sealed class RunManifest
	{
		public string schema { get; set; }
		public string manifest_path { get; set; }
		public string recipe_id { get; set; }
		public string recipe_title { get; set; }
		public string quality { get; set; }
		public OverlaySelection overlay { get; set; }
		public string exact_command { get; set; }
		public string working_directory { get; set; }
		public Dictionary<string, string> env { get; set; }
		public string planned_output_folder { get; set; }
		public bool testbench_wrapped_output { get; set; }
		public bool source_script_artifacts_preserved { get; set; }
		public bool dry_run { get; set; }
		public string status { get; set; }
		public int? exit_code { get; set; }
		public string started_utc { get; set; }
		public string updated_utc { get; set; }
		public double? duration_seconds { get; set; }
		public string duration_text { get; set; }
		public bool smoke_success_known { get; set; }
		public ArtifactSet artifacts { get; set; }
		public string cockpit_summary { get; set; }
		public string guardrail_text { get; set; }
		public Dictionary<string, object> visual_grammar { get; set; }
	}

	private sealed class OverlaySelection
	{
		public string mode { get; set; }
		public string placement { get; set; }
		public bool postprocess_only { get; set; }
		public bool placement_descriptive_only { get; set; }
	}

	private sealed class ArtifactSet
	{
		public string folder { get; set; }
		public string report { get; set; }
		public string contact_sheet { get; set; }
		public string summary { get; set; }
		public string preview { get; set; }
		public List<string> expected_reports { get; set; }
		public List<string> expected_contact_sheets { get; set; }
		public List<string> expected_summaries { get; set; }
		public List<string> expected_previews { get; set; }
	}

	private static class VisualGrammar
	{
		public static Dictionary<string, object> Build()
		{
			return new Dictionary<string, object>
			{
				["straight_reference"] = "#d8d8d8",
				["curved_transport"] = new[] { "#00d7ff", "#ff4fd8" },
				["density"] = "#2f7dff",
				["curvature"] = "#f2c14e",
				["unresolved_failed_transport"] = new[] { "#ff4b2f", "#ff9a2f" },
				["observer_camera"] = "small_labeled_glyph",
				["field_center_nucleus"] = "crosshair"
			};
		}
	}
}
