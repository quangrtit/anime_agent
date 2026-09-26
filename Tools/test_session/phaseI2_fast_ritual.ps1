. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== I3: recover (kill hung app + relaunch) ==="
Stop-Process -Name "AnimeAssistant" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
cmd /c start "" "C:\Users\hieu\Documents\GitHub\anime_agent\Builds\Windows\AnimeAssistant.exe"
Start-Sleep -Seconds 12
Invoke-Click 1330 640
Start-Sleep -Seconds 5
Find-Log "SummonCycle|Icon glass" | Select-Object -Last 4

Write-Output "=== I4: FAST double-click GitHub Desktop (press2 at +60ms) ==="
Move-MouseTo 55 40
Start-Sleep -Milliseconds 400
[SiInput]::Button(0x0002); Start-Sleep -Milliseconds 110; [SiInput]::Button(0x0004)
Start-Sleep -Milliseconds 60
[SiInput]::Button(0x0002); Start-Sleep -Milliseconds 110; [SiInput]::Button(0x0004)
Start-Sleep -Milliseconds 130
Save-Screen "I4_flight_mid.png"
Start-Sleep -Milliseconds 1050
Save-Screen "I4_flight_docked.png"
Start-Sleep -Seconds 6

Write-Output "=== I5: outcome ==="
Get-Process -Name "GitHubDesktop" -ErrorAction SilentlyContinue | ForEach-Object { "GitHubDesktop pid " + $_.Id }
$proc = Get-Process -Name "AnimeAssistant" -ErrorAction SilentlyContinue
if ($proc) { Write-Output ("app responding=" + $proc.Responding) } else { Write-Output "APP GONE" }
Find-Log "Icon ritual|Ritual finished|No new process" | Select-Object -Last 6
Save-Screen "I5_after_launch.png"
