. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

$compDir = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Companion"

Write-Output "=== H1: prep settings/profile/reminders while app is dead ==="
$settingsPath = Join-Path $compDir "companion_settings.json"
(Get-Content $settingsPath -Raw) -replace '"forceGift": ""', '"forceGift": "charm"' | Set-Content $settingsPath -Encoding UTF8

$profilePath = Join-Path $compDir "companion_profile.json"
(Get-Content $profilePath -Raw) -replace '"lastGiftKey": "gift:20260926"', '"lastGiftKey": ""' | Set-Content $profilePath -Encoding UTF8

$remPath = Join-Path $compDir "reminders.json"
$rem = Get-Content $remPath -Raw
Write-Output "reminders before:"
Write-Output $rem
$rem2 = $rem -replace '"repeatMinutes": 90,', "`"repeatMinutes`": 1,"
Set-Content -Path $remPath -Value $rem2 -Encoding UTF8
Write-Output "reminders after:"
Get-Content $remPath -Raw

Write-Output "=== H2: launch app fresh ==="
cmd /c start "" "C:\Users\hieu\Documents\GitHub\anime_agent\Builds\Windows\AnimeAssistant.exe"
Start-Sleep -Seconds 12

Write-Output "=== H3: click door to summon ==="
Move-MouseTo 700 500
Start-Sleep -Milliseconds 300
Invoke-Click 1330 640
Start-Sleep -Seconds 6
Save-Screen "H3_summoned.png"

Write-Output "=== H4: log check ==="
Find-Log "SummonCycle|Gift|gift|reminder|Say|Icon glass"
Write-Output "=== H5: Desktop\Airi contents ==="
Get-ChildItem "$env:USERPROFILE\Desktop\Airi" -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }
Move-MouseTo 700 500
