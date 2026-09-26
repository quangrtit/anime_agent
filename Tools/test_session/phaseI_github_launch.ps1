. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== I1: double-click GitHub Desktop icon (y2, center ~(55,40)) ==="
$beforeSet = @{}
Get-Process | ForEach-Object { $beforeSet[$_.Id] = $true }

Move-MouseTo 55 40
Start-Sleep -Milliseconds 500
[SiInput]::Button(0x0002); Start-Sleep -Milliseconds 220; [SiInput]::Button(0x0004)
Start-Sleep -Milliseconds 80
[SiInput]::Button(0x0002); Start-Sleep -Milliseconds 220; [SiInput]::Button(0x0004)
Start-Sleep -Milliseconds 120
Save-Screen "I1_flight_mid.png"
Start-Sleep -Milliseconds 1100
Save-Screen "I1_flight_docked.png"
Start-Sleep -Seconds 5

Write-Output "=== I2: new processes ==="
Get-Process | Where-Object { -not $beforeSet.ContainsKey($_.Id) } | ForEach-Object { "NEW: {0} (pid {1})" -f $_.ProcessName, $_.Id }
Write-Output "--- app still responding? ---"
$proc = Get-Process -Name "AnimeAssistant" -ErrorAction SilentlyContinue
if ($proc) { Write-Output ("responding=" + $proc.Responding) } else { Write-Output "APP GONE" }
Write-Output "--- ritual logs ---"
Find-Log "Icon ritual|Ritual finished|No new process|Glass forwarded"
Move-MouseTo 700 500
