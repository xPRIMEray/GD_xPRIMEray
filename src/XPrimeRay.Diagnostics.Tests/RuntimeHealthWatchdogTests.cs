using System.Text.Json;
using XPrimeRay.Diagnostics;

namespace XPrimeRay.Diagnostics.Tests;

internal static class RuntimeHealthWatchdogTests
{
    public static void Run()
    {
        WritesRunAndHeartbeatRecords();
        DisposeCompletesRunWhenNeeded();
        DetectsIncompletePriorRun();
        DetectsIncompletePriorRunWithTornFinalLine();
        OlderIncompleteRunDoesNotMarkLaterStarts();
        MostRecentIncompleteRunIsAbnormal();
        TornLineMidLogDoesNotStopScanning();
        DevelopmentSafeLimitsAreOptIn();
    }

    private static void WritesRunAndHeartbeatRecords()
    {
        string directory = NewDirectory();
        using (var watchdog = new RuntimeHealthWatchdog(new RuntimeHealthWatchdogOptions(directory, TimeSpan.Zero)))
        {
            watchdog.BeginAcquisition("snapshot-7", 3, "ctx-sha");
            watchdog.Heartbeat("capturing", 12, 34, 5, 3, "ctx-sha", "band-1", force: true);
            watchdog.CompleteAcquisition(3, "ctx-sha", "finalize");
            watchdog.CompleteRun();
        }
        string[] lines = File.ReadAllLines(Path.Combine(directory, "runtime_health.ndjson"));
        Assert.True(lines.Any(line => JsonDocument.Parse(line).RootElement.GetProperty("event").GetString() == "RUN_START"), "run start");
        Assert.True(lines.Any(line => JsonDocument.Parse(line).RootElement.GetProperty("event").GetString() == "heartbeat"), "heartbeat");
        Assert.True(lines.Any(line => JsonDocument.Parse(line).RootElement.GetProperty("event").GetString() == "RUN_COMPLETE"), "run complete");
        Assert.True(File.ReadAllLines(Path.Combine(directory, "runtime_health.csv")).Length >= 2, "csv append");
    }

    private static void DetectsIncompletePriorRun()
    {
        string directory = NewDirectory();
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "runtime_health.ndjson");
        File.WriteAllText(path, "{\"event\":\"RUN_START\",\"run_id\":\"crashed\"}\n");
        using var watchdog = new RuntimeHealthWatchdog(new RuntimeHealthWatchdogOptions(directory, TimeSpan.FromSeconds(1)));
        Assert.True(watchdog.AbnormalPreviousRun, "abnormal previous run");
        Assert.True(ReadShared(path).Contains("ABNORMAL_PREVIOUS_RUN", StringComparison.Ordinal), "abnormal marker");
    }

    private static void DisposeCompletesRunWhenNeeded()
    {
        string directory = NewDirectory();
        using (var watchdog = new RuntimeHealthWatchdog(new RuntimeHealthWatchdogOptions(directory, TimeSpan.FromSeconds(1))))
        {
        }
        Assert.True(File.ReadAllText(Path.Combine(directory, "runtime_health.ndjson")).Contains("RUN_COMPLETE", StringComparison.Ordinal), "dispose completes run");
    }

    private static void OlderIncompleteRunDoesNotMarkLaterStarts()
    {
        string directory = NewDirectory();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "runtime_health.ndjson"),
            "{\"event\":\"RUN_START\",\"run_id\":\"crashed\"}\n" +
            "{\"event\":\"RUN_START\",\"run_id\":\"ok\"}\n{\"event\":\"RUN_COMPLETE\",\"run_id\":\"ok\"}\n");
        using var watchdog = new RuntimeHealthWatchdog(new RuntimeHealthWatchdogOptions(directory, TimeSpan.FromSeconds(1)));
        Assert.False(watchdog.AbnormalPreviousRun, "an older incomplete run does not make a later clean run abnormal");
    }

    private static void MostRecentIncompleteRunIsAbnormal()
    {
        string directory = NewDirectory();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "runtime_health.ndjson"),
            "{\"event\":\"RUN_START\",\"run_id\":\"ok\"}\n{\"event\":\"RUN_COMPLETE\",\"run_id\":\"ok\"}\n" +
            "{\"event\":\"RUN_START\",\"run_id\":\"crashed\"}\n{\"event\":\"heartbeat\",\"run_id\":\"crashed\"}\n");
        using var watchdog = new RuntimeHealthWatchdog(new RuntimeHealthWatchdogOptions(directory, TimeSpan.FromSeconds(1)));
        Assert.True(watchdog.AbnormalPreviousRun, "the most recent run without RUN_COMPLETE is abnormal");
    }

    private static void TornLineMidLogDoesNotStopScanning()
    {
        string directory = NewDirectory();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "runtime_health.ndjson"),
            "{\"event\":\"RUN_START\",\"run_id\":\"crashed\"}\n{\"event\":\"heart\n" +
            "{\"event\":\"RUN_START\",\"run_id\":\"ok\"}\n{\"event\":\"RUN_COMPLETE\",\"run_id\":\"ok\"}\n");
        using var watchdog = new RuntimeHealthWatchdog(new RuntimeHealthWatchdogOptions(directory, TimeSpan.FromSeconds(1)));
        Assert.False(watchdog.AbnormalPreviousRun, "a torn line in the middle of the log does not stop the scan");
    }

    private static void DetectsIncompletePriorRunWithTornFinalLine()
    {
        string directory = NewDirectory();
        string path = Path.Combine(directory, "runtime_health.ndjson");
        File.WriteAllText(path, "{\"event\":\"RUN_START\",\"run_id\":\"crashed\"}\n{\"event\":\"heartbeat\"");
        using var watchdog = new RuntimeHealthWatchdog(new RuntimeHealthWatchdogOptions(directory, TimeSpan.FromSeconds(1)));
        Assert.True(watchdog.AbnormalPreviousRun, "torn final line preserves abnormal evidence");
    }

    private static void DevelopmentSafeLimitsAreOptIn()
    {
        string directory = NewDirectory();
        using var disabled = new RuntimeHealthWatchdog(new RuntimeHealthWatchdogOptions(directory, TimeSpan.FromSeconds(1), DevelopmentSafe: false, MaxWorkerCount: 1));
        Assert.False(disabled.CheckLimits(2).Abort, "limits disabled by default");
        Assert.True(disabled.ConfiguredLimitsIgnored, "inactive limits are reported");
        string safeDirectory = NewDirectory();
        using var enabled = new RuntimeHealthWatchdog(new RuntimeHealthWatchdogOptions(safeDirectory, TimeSpan.FromSeconds(1), DevelopmentSafe: true, MaxWorkerCount: 1));
        Assert.True(enabled.CheckLimits(2).Abort, "worker limit abort");
    }

    // The watchdog keeps its log open for append (FileShare.ReadWrite). File.ReadAllText does not allow other
    // writers, which raises a sharing violation on Windows while the watchdog is alive.
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string NewDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "xprimeray-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
