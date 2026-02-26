$path = 'logs\\_tmp_curved_minimal_autocal_validation_2026-02-25_postfix.txt'
$text = Get-Content $path -Raw
$events = [regex]::Matches($text, '(?s)\[RenderTestRunner\]\[(AutoCalShadowEval|AutoCalDecision)\]\s+(.*?)(?=\r?\n\[RenderTestRunner\]\[|\z)')
$shadow = @()
$decisions = @()
foreach ($ev in $events) {
  $kind = $ev.Groups[1].Value
  $body = ($ev.Groups[2].Value -replace '\r?\n\s*', ' ').Trim()
  if ($kind -eq 'AutoCalShadowEval' -and $body -match 'baseline_run_id=(\d+)\s+shadow_run_id=(\d+|na)\s+baseline_trust=(\d+|na)\s+shadow_trust=(\d+|na).*?overhead_pct_est=([^ ]+)\s+verdict=([^ ]+)') {
    $shadow += [pscustomobject]@{baseline_run_id=[int]$matches[1];shadow_run_id=$matches[2];baseline_trust=$matches[3];shadow_trust=$matches[4];overhead=$matches[5];verdict=$matches[6]}
  }
  if ($kind -eq 'AutoCalDecision' -and $body -match 'decision=([^ ]+)\s+reason=([^ ]+)\s+overhead_pct_est=([^ ]+)\s+shadow_trust=([^ ]+)\s+verdict=([^ ]+)') {
    $decisions += [pscustomobject]@{decision=$matches[1];reason=$matches[2];overhead=$matches[3];shadow_trust=$matches[4];verdict=$matches[5]}
  }
}
$runs = [regex]::Matches($text, '(?s)\[RUN START\]\s+name=([^ ]+)\s+prune=([^ ]+)') | ForEach-Object {
  [pscustomobject]@{name=$_.Groups[1].Value;prune=$_.Groups[2].Value}
}
Write-Output "ShadowEval rows: $($shadow.Count)"
Write-Output "Decision rows: $($decisions.Count)"
Write-Output "Verdict counts:"
$shadow | Group-Object verdict | Sort-Object Name | ForEach-Object { Write-Output ("  {0}={1}" -f $_.Name,$_.Count) }
Write-Output "Decision reason counts:"
$decisions | Group-Object reason | Sort-Object Name | ForEach-Object { Write-Output ("  {0}={1}" -f $_.Name,$_.Count) }
Write-Output "Shadow trust counts:"
$shadow | Group-Object shadow_trust | Sort-Object Name | ForEach-Object { Write-Output ("  {0}={1}" -f $_.Name,$_.Count) }
$nums = @($shadow | Where-Object { $_.overhead -ne 'na' } | ForEach-Object {[double]$_.overhead} | Sort-Object)
if ($nums.Count -gt 0) {
  $avg = ($nums | Measure-Object -Average).Average
  $p50 = $nums[[int][math]::Floor(($nums.Count-1)/2)]
  Write-Output ("Overhead pct est stats: count={0} min={1} p50={2} max={3} avg={4}" -f $nums.Count,[math]::Round($nums[0],2),[math]::Round($p50,2),[math]::Round($nums[-1],2),[math]::Round($avg,2))
}
Write-Output "Prune deltas by run pair:"
for ($i=0; $i+1 -lt $runs.Count; $i += 2) {
  Write-Output ("  {0}: {1} -> {2}" -f $runs[$i].name,$runs[$i].prune,$runs[$i+1].prune)
}
Write-Output "Shadow detail:"
$shadow | Sort-Object baseline_run_id | ForEach-Object { Write-Output ("  base={0} shadow={1} btrust={2} strust={3} overhead={4} verdict={5}" -f $_.baseline_run_id,$_.shadow_run_id,$_.baseline_trust,$_.shadow_trust,$_.overhead,$_.verdict) }
