. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== A5: click the door (physical center of doorPixels) ==="
Invoke-Click 1330 640
Start-Sleep -Seconds 4

Write-Output "=== A6: log after door click ==="
Find-Log "SummonState|Summon|say|Say|Companion|glass"
Start-Sleep -Seconds 4
Write-Output "=== A7: screenshot after summon (avatar should be visible + speech bubble) ==="
Save-Screen "A7_after_summon.png"
