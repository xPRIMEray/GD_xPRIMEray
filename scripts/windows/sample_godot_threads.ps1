[CmdletBinding()]
param(
    [ValidateSet('A0_FRESH_OFF', 'A3_POST_G_OFF')]
    [string]$Label = 'A0_FRESH_OFF',

    [int]$TargetProcessId = 0,

    [string]$ProcessNamePattern = 'godot|xprime|physical',

    [ValidateRange(1, 3600)]
    [int]$IntervalSeconds = 10
)

$ErrorActionPreference = 'Stop'

function Resolve-TargetProcess {
    if ($TargetProcessId -ne 0) {
        return Get-Process -Id $TargetProcessId
    }

    $candidates = @(Get-Process | Where-Object {
        $_.ProcessName -match $ProcessNamePattern -and
        $_.Id -ne $PID
    })

    if ($candidates.Count -eq 0) {
        throw "No process matched '$ProcessNamePattern'. Supply -TargetProcessId explicitly."
    }
    if ($candidates.Count -gt 1) {
        Write-Host 'Multiple candidates found:'
        $candidates | Select-Object Id, ProcessName, MainWindowTitle, CPU | Format-Table -AutoSize
        throw 'Multiple matching processes; rerun with -TargetProcessId.'
    }
    return $candidates[0]
}

function Read-ThreadSample([System.Diagnostics.Process]$Process) {
    $rows = @{}
    $Process.Refresh()
    foreach ($thread in $Process.Threads) {
        $cpu = 0.0
        try { $cpu = $thread.TotalProcessorTime.TotalMilliseconds } catch { continue }

        $startAddress = $null
        try { $startAddress = $thread.StartAddress } catch { }
        $waitReason = 'n/a'
        try { $waitReason = [string]$thread.WaitReason } catch { }
        $rows[[int]$thread.Id] = [pscustomobject]@{
            ThreadId = [int]$thread.Id
            CpuMs = [double]$cpu
            State = [string]$thread.ThreadState
            WaitReason = $waitReason
            StartAddress = if ($null -eq $startAddress) { 'unavailable' } else { [string]$startAddress }
        }
    }
    return $rows
}

$target = Resolve-TargetProcess
$targetPid = $target.Id
$first = Read-ThreadSample $target
$firstProcessCpu = 0.0
try { $firstProcessCpu = (Get-Process -Id $targetPid).TotalProcessorTime.TotalMilliseconds } catch { }

Write-Host ("THREAD_SAMPLE_START label={0} pid={1} process={2} intervalSeconds={3}" -f $Label, $targetPid, $target.ProcessName, $IntervalSeconds)
Start-Sleep -Seconds $IntervalSeconds

$target = Get-Process -Id $targetPid
$second = Read-ThreadSample $target
$secondProcessCpu = 0.0
try { $secondProcessCpu = $target.TotalProcessorTime.TotalMilliseconds } catch { }
$processCpuDelta = [math]::Max(0.0, $secondProcessCpu - $firstProcessCpu)
$logicalProcessors = [Environment]::ProcessorCount

$results = foreach ($entry in $second.GetEnumerator()) {
    $before = $first[$entry.Key]
    $delta = if ($null -eq $before) { 0.0 } else { [math]::Max(0.0, $entry.Value.CpuMs - $before.CpuMs) }
    [pscustomobject]@{
        Label = $Label
        Pid = $targetPid
        ThreadId = $entry.Value.ThreadId
        CpuDeltaMs = [math]::Round($delta, 1)
        CoreEquivalentPercent = [math]::Round(($delta / ($IntervalSeconds * 1000.0)) * 100.0, 1)
        State = $entry.Value.State
        WaitReason = $entry.Value.WaitReason
        StartAddress = $entry.Value.StartAddress
    }
}

Write-Host ("PROCESS_CPU_DELTA_MS={0:N1} logical_processors={1}" -f $processCpuDelta, $logicalProcessors)
Write-Host 'THREADS_RANKED_BY_CPU_DELTA'
$results | Sort-Object CpuDeltaMs -Descending | Select-Object -First 20 |
    Format-Table ThreadId, CpuDeltaMs, CoreEquivalentPercent, State, WaitReason, StartAddress -AutoSize

$csvPath = Join-Path (Get-Location) ("thread-sample-{0}-{1}.csv" -f $Label, $targetPid)
$results | Sort-Object CpuDeltaMs -Descending | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $csvPath
Write-Host "CSV=$csvPath"
Write-Host 'Native stack ownership is not resolved by this helper; use Process Explorer or WPR/WPA for stacks.'
