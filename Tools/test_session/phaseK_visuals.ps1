. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== K1: click recall -> catch DoorOpening transition (backdrop in doorway) ==="
Invoke-Click 1310 760
Start-Sleep -Milliseconds 900
Save-Screen "K1_door_transition1.png"
Start-Sleep -Milliseconds 900
Save-Screen "K1_door_transition2.png"
Start-Sleep -Seconds 5
Find-Log "SummonCycle" | Select-Object -Last 3

Write-Output "=== K2: create jealousy bait + 7 extra windows (notepads) ==="
$tmp = "$env:TEMP\airi_test"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
"bait" | Set-Content "$tmp\cute_girl_waifu.jpg.txt" -Encoding ASCII
for ($i = 1; $i -le 7; $i++) { "w$i" | Set-Content "$tmp\extra_window_$i.txt" -Encoding ASCII }
Start-Process notepad "$tmp\cute_girl_waifu.jpg.txt"
Start-Sleep -Milliseconds 600
for ($i = 1; $i -le 7; $i++) { Start-Process notepad "$tmp\extra_window_$i.txt"; Start-Sleep -Milliseconds 400 }
Write-Output ("visible windows now: " + (Get-VisibleWindowCount))

Write-Output "=== K3: wait 25s for jealousy check (needs >=20s after summon, 2s cadence) ==="
Start-Sleep -Seconds 25
Find-Log "jealous|Say" | Select-Object -Last 6

Write-Output "=== K4: Alt+D diary visual ==="
Invoke-Hotkey ([byte[]]@(0x12, 0x44))
Start-Sleep -Milliseconds 1200
Save-Screen "K4_diary.png"

Write-Output "=== K5: Alt+P pomodoro chip visual ==="
Invoke-Hotkey ([byte[]]@(0x12, 0x50))
Start-Sleep -Milliseconds 1200
Save-Screen "K5_pomodoro.png"
Invoke-Hotkey ([byte[]]@(0x12, 0x50))
Start-Sleep -Milliseconds 500
Move-MouseTo 700 500
Find-Log "Pomodoro|pomodoro" | Select-Object -Last 4
