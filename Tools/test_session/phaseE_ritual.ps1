. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

$settingsPath = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Companion\companion_settings.json"

Write-Output "=== E1: disable glass briefly to see icons ==="
(Get-Content $settingsPath -Raw) -replace '"iconGlassEnabled": true', '"iconGlassEnabled": false' | Set-Content $settingsPath -Encoding UTF8
Start-Sleep -Seconds 4
Save-Screen "E1_icons_visible.png"

Write-Output "=== E2: re-enable glass ==="
(Get-Content $settingsPath -Raw) -replace '"iconGlassEnabled": false', '"iconGlassEnabled": true' | Set-Content $settingsPath -Encoding UTF8
Start-Sleep -Seconds 4
Find-Log "Icon glass"
