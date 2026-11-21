using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace XPrimeRay.Diagnostics;

public sealed record RuntimeHealthWatchdogOptions(
    string OutputDirectory,
    TimeSpan HeartbeatInterval,
    bool DevelopmentSafe = false,
    int MaxWorkerCount = 0,
    long WorkingSetWarningBytes = 0,
    long WorkingSetAbortBytes = 0,
    long PrivateMemoryAbortBytes = 0);

public readonly record struct RuntimeHealthLimits(
    bool Abort,
    bool Warning,
    string Reason,
    int WorkerCount,
    long WorkingSetBytes,
    long PrivateMemoryBytes);

/// <summary>
/// Best-effort, append-only health breadcrumbs. It is deliberately independent
/// of observation authority: it observes a run and never owns measurement data.
/// </summary>
public sealed class RuntimeHealthWatchdog : IDisposable
{
    private readonly RuntimeHealthWatchdogOptions _options;
    private readonly object _gate = new();
    private readonly StreamWriter? _ndjson;
    private readonly StreamWriter? _csv;
    private readonly Process? _process;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private DateTime _lastHeartbeatUtc = DateTime.MinValue;
    private bool _runComplete;
    private bool _disposed;

    public string RunId { get; }
    public string? CurrentAcquisitionId { get; private set; }
    public bool AbnormalPreviousRun { get; }
    public bool ConfiguredLimitsIgnored { get; }
    public string OutputDirectory => _options.OutputDirectory;

    public RuntimeHealthWatchdog(RuntimeHealthWatchdogOptions options)
    {
        _options = options with
        {
            HeartbeatInterval = options.HeartbeatInterval <= TimeSpan.Zero
                ? TimeSpan.FromSeconds(1)
                : options.HeartbeatInterval
        };
        RunId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        try { _process = Process.GetCurrentProcess(); } catch { _process = null; }
        ConfiguredLimitsIgnored = !_options.DevelopmentSafe &&
            (_options.MaxWorkerCount > 0 || _options.WorkingSetWarningBytes > 0 ||
             _options.WorkingSetAbortBytes > 0 || _options.PrivateMemoryAbortBytes > 0);

        try
        {
            Directory.CreateDirectory(_options.OutputDirectory);
            string ndjsonPath = Path.Combine(_options.OutputDirectory, "runtime_health.ndjson");
            AbnormalPreviousRun = HasIncompletePriorRun(ndjsonPath);
            _ndjson = new StreamWriter(new FileStream(ndjsonPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };
            string csvPath = Path.Combine(_options.OutputDirectory, "runtime_health.csv");
            bool writeHeader = !File.Exists(csvPath) || new FileInfo(csvPath).Length == 0;
            _csv = new StreamWriter(new FileStream(csvPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };
            if (writeHeader)
                _csv.WriteLine("timestamp_utc,event,run_id,acquisition_id,phase,elapsed_ms,working_set_bytes,private_memory_bytes,gc_heap_bytes,gc_allocated_bytes,thread_count,worker_count,query_count,primitive_tests,node_tests,generation,context_identity,last_completed_work_unit,status,reason");
            if (AbnormalPreviousRun)
                Append("ABNORMAL_PREVIOUS_RUN", phase: "startup", status: "warning", reason: "RUN_START without RUN_COMPLETE");
            if (ConfiguredLimitsIgnored)
                Append("CONFIG_WARNING", phase: "startup", status: "warning", reason: "DevelopmentSafe=false; configured health limits are inactive");
            Append("RUN_START", phase: "startup", status: "started");
        }
        catch
        {
            // Health logging must never prevent the host from starting.
        }
    }

    public void BeginAcquisition(string acquisitionId, long generation, string contextIdentity)
    {
        CurrentAcquisitionId = acquisitionId;
        Append("ACQUISITION_START", "snapshot", generation: generation, contextIdentity: contextIdentity, status: "started");
    }

    public void Heartbeat(
        string phase,
        long queryCount,
        long primitiveTests,
        long nodeTests,
        long generation,
        string contextIdentity,
        string lastCompletedWorkUnit,
        int workerCount = -1,
        bool force = false)
    {
        if (!force && _lastHeartbeatUtc != DateTime.MinValue && DateTime.UtcNow - _lastHeartbeatUtc < _options.HeartbeatInterval)
            return;
        _lastHeartbeatUtc = DateTime.UtcNow;
        Append("heartbeat", phase, queryCount, primitiveTests, nodeTests, generation, contextIdentity, lastCompletedWorkUnit, status: "ok", workerCount: workerCount);
    }

    public void CompleteAcquisition(long generation, string contextIdentity, string lastCompletedWorkUnit)
    {
        Append("ACQUISITION_COMPLETE", "snapshot", generation: generation, contextIdentity: contextIdentity, lastCompletedWorkUnit: lastCompletedWorkUnit, status: "complete");
        CurrentAcquisitionId = null;
    }

    public void AbortAcquisition(long generation, string contextIdentity, string reason)
    {
        Append("ACQUISITION_ABORT", "snapshot", generation: generation, contextIdentity: contextIdentity, status: "failed", reason: reason);
        CurrentAcquisitionId = null;
    }

    public RuntimeHealthLimits CheckLimits(int workerCount)
    {
        ProcessMetrics metrics = ReadMetrics();
        if (!_options.DevelopmentSafe)
            return new RuntimeHealthLimits(false, false, string.Empty, workerCount, metrics.WorkingSetBytes, metrics.PrivateMemoryBytes);

        bool workerOverLimit = _options.MaxWorkerCount > 0 && workerCount > _options.MaxWorkerCount;
        bool memoryWarning = _options.WorkingSetWarningBytes > 0 && metrics.WorkingSetBytes >= _options.WorkingSetWarningBytes;
        bool abort = (_options.WorkingSetAbortBytes > 0 && metrics.WorkingSetBytes >= _options.WorkingSetAbortBytes) ||
            (_options.PrivateMemoryAbortBytes > 0 && metrics.PrivateMemoryBytes >= _options.PrivateMemoryAbortBytes) || workerOverLimit;
        bool warning = workerOverLimit || memoryWarning;
        string reason = abort
            ? (workerOverLimit ? "worker_limit" : metrics.WorkingSetBytes >= _options.WorkingSetAbortBytes ? "working_set_limit" : "private_memory_limit")
            : memoryWarning ? "working_set_warning" : string.Empty;
        if (warning)
            Append("RESOURCE_WARNING", "snapshot", status: abort ? "abort" : "warning", reason: reason);
        return new RuntimeHealthLimits(abort, warning, reason, workerCount, metrics.WorkingSetBytes, metrics.PrivateMemoryBytes);
    }

    public void CompleteRun(string status = "complete", string reason = "")
    {
        lock (_gate)
        {
            if (_runComplete) return;
            _runComplete = true;
            Append("RUN_COMPLETE", "shutdown", status: status, reason: reason);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (!_runComplete) CompleteRun("disposed");
            _disposed = true;
            _ndjson?.Dispose();
            _csv?.Dispose();
            _process?.Dispose();
        }
    }

    private void Append(
        string eventName,
        string phase,
        long queryCount = 0,
        long primitiveTests = 0,
        long nodeTests = 0,
        long generation = 0,
        string contextIdentity = "",
        string lastCompletedWorkUnit = "",
        string status = "",
        string reason = "",
        int workerCount = -1)
    {
        lock (_gate)
        {
            if (_disposed) return;
            ProcessMetrics metrics = ReadMetrics();
            var record = new Dictionary<string, object?>
            {
                ["timestamp_utc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["event"] = eventName,
                ["run_id"] = RunId,
                ["acquisition_id"] = CurrentAcquisitionId ?? "",
                ["phase"] = phase,
                ["elapsed_ms"] = _clock.Elapsed.TotalMilliseconds,
                ["working_set_bytes"] = metrics.WorkingSetBytes,
                ["private_memory_bytes"] = metrics.PrivateMemoryBytes,
                ["gc_heap_bytes"] = metrics.GcHeapBytes,
                ["gc_allocated_bytes"] = metrics.GcAllocatedBytes,
                ["thread_count"] = metrics.ThreadCount,
                ["worker_count"] = workerCount >= 0 ? workerCount : 0,
                ["query_count"] = queryCount,
                ["primitive_tests"] = primitiveTests,
                ["node_tests"] = nodeTests,
                ["generation"] = generation,
                ["context_identity"] = contextIdentity,
                ["last_completed_work_unit"] = lastCompletedWorkUnit,
                ["status"] = status,
                ["reason"] = reason
            };
            try
            {
                _ndjson?.WriteLine(JsonSerializer.Serialize(record));
                _csv?.WriteLine(string.Join(',', record.Values.Select(value => Csv(value))));
            }
            catch
            {
                // A full disk or failed pipe must not affect observation execution.
            }
        }
    }

    private static string Csv(object? value) =>
        '"' + (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).Replace("\"", "\"\"") + '"';

    // "Previous run" is the most recent RUN_START recorded before this instance starts. Older incomplete runs stay
    // in the log as evidence, but must not mark every later start abnormal (the log is append-only and shared by
    // every run of the project). Unparseable (torn) lines are skipped; a torn line written after the most recent
    // RUN_START without a matching RUN_COMPLETE still leaves that run incomplete.
    private static bool HasIncompletePriorRun(string path)
    {
        if (!File.Exists(path)) return false;
        string lastStart = "";
        bool lastCompleted = false;
        try
        {
            foreach (string line in File.ReadLines(path))
            {
                if (line.Length == 0) continue;
                try
                {
                    using JsonDocument json = JsonDocument.Parse(line);
                    string runId = json.RootElement.TryGetProperty("run_id", out var id) ? id.GetString() ?? "" : "";
                    string eventName = json.RootElement.TryGetProperty("event", out var evt) ? evt.GetString() ?? "" : "";
                    if (eventName == "RUN_START" && runId.Length > 0)
                    {
                        lastStart = runId;
                        lastCompleted = false;
                    }
                    else if (eventName == "RUN_COMPLETE" && runId.Length > 0 && runId == lastStart)
                    {
                        lastCompleted = true;
                    }
                }
                catch (JsonException)
                {
                    // A torn line (interrupted writer) is not a record; keep scanning later lines.
                }
            }
        }
        catch { return false; }
        return lastStart.Length > 0 && !lastCompleted;
    }

    private readonly record struct ProcessMetrics(long WorkingSetBytes, long PrivateMemoryBytes, long GcHeapBytes, long GcAllocatedBytes, int ThreadCount);

    private ProcessMetrics ReadMetrics()
    {
        try
        {
            if (_process == null)
                throw new InvalidOperationException("process_metrics_unavailable");
            return new ProcessMetrics(_process.WorkingSet64, _process.PrivateMemorySize64, GC.GetTotalMemory(false), GC.GetTotalAllocatedBytes(false), _process.Threads.Count);
        }
        catch
        {
            return new ProcessMetrics(0, 0, GC.GetTotalMemory(false), GC.GetTotalAllocatedBytes(false), 0);
        }
    }
}
