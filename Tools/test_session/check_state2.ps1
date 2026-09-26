. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== settings file now ==="
Get-CompanionData "companion_settings.json"
Write-Output ""
Write-Output "=== last SummonCycle transitions ==="
Find-Log "SummonCycle" | Select-Object -Last 6
Write-Output ""
Write-Output "=== log tail (absolute last 15) ==="
Get-LogTail 15
Write-Output ""
Write-Output "=== timestamped check: last 3 glass lines with line numbers ==="
$log = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Player.log"
$all = Get-Content $log
$total = $all.Count
Write-Output "total log lines: $total"
$idx = @()
for ($i = $total - 1; $i -ge 0 -and $idx.Count -lt 3; $i--) { if ($all[$i] -match "Icon glass") { $idx += $i } }
Write-Output ("last glass lines at file positions: " + ($idx -join ", ") + " (of $total)")
