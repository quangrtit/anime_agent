. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== D4: single click on Recycle Bin (icon A) with SendInput ==="
Move-MouseTo 55 50
Start-Sleep -Milliseconds 400
Invoke-Click 55 50
Start-Sleep -Seconds 2
Write-Output "--- log ---"
Find-Log "Glass forwarded|Ritual|ritual"
Write-Output "=== screenshot (Recycle Bin should be selected) ==="
Save-Screen "D4_single_click.png"
Move-MouseTo 700 500
