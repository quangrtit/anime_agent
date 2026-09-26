. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== D2: hover over 3 icons to map labels + capture hover highlight ==="
# icon tops at y = 2, 107, 212, 317, 422, 527; center x ~ 55
Move-MouseTo 55 50
Start-Sleep -Milliseconds 800
Save-Screen "D2_hover_iconA.png"

Move-MouseTo 55 260
Start-Sleep -Milliseconds 800
Save-Screen "D2_hover_iconC.png"

Move-MouseTo 55 570
Start-Sleep -Milliseconds 800
Save-Screen "D2_hover_iconF.png"

Write-Output "=== single-click test: click icon A (y~50), selection should be forwarded after 0.28s ==="
Invoke-Click 55 50
Start-Sleep -Seconds 1
Find-Log "Glass forwarded|ritual|Ritual"
Write-Output "=== verify selection via UIA on desktop list ==="
Move-MouseTo 700 500
Start-Sleep -Milliseconds 300
Save-Screen "D2_after_single_click.png"
