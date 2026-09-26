. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== A1: launch app ==="
cmd /c start "" "C:\Users\hieu\Documents\GitHub\anime_agent\Builds\Windows\AnimeAssistant.exe"
Start-Sleep -Seconds 12

Write-Output "=== A2: log after launch ==="
Get-LogTail 60

Write-Output "=== A3: baseline screenshot (door should be visible bottom-right) ==="
Save-Screen "A3_after_launch.png"

Write-Output "=== A4: visible window count baseline ==="
Write-Output ("visible windows: " + (Get-VisibleWindowCount))
Get-VisibleWindowTitles | ForEach-Object { "win: $_" }
Write-Output ("foreground: " + (Get-ForegroundTitle))
