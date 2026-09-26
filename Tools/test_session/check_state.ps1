. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== processes of interest now ==="
Get-Process | Where-Object { $_.ProcessName -match "GitHub|Unity|ZCode|notepad|msedge|Explorer" } | Select-Object Id, ProcessName, StartTime, MainWindowTitle | ForEach-Object { "{0,8} {1,-16} start={2} [{3}]" -f $_.Id, $_.ProcessName, $_.StartTime.ToString("HH:mm:ss"), $_.MainWindowTitle }
Write-Output ""
Write-Output "=== log: fallback / new process lines ==="
Find-Log "No new process|fallback|real-input|replay"
Write-Output ""
Write-Output "=== full recent log ==="
Get-LogTail 20
