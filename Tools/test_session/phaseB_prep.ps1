. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== B1: all Say/gift/anniversary lines so far ==="
Find-Log "Say:|anniversary|Gift|gift|farewell|greeting"
Write-Output ""
Write-Output "=== B2: stop dance via Alt+N (dance still enabled) ==="
Invoke-Hotkey ([byte[]]@(0x12, 0x4E))
Start-Sleep -Seconds 2
Find-Log "Music detected|dance"
Write-Output ""
Write-Output "=== B3: disable dance via settings hot-reload ==="
$settingsPath = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Companion\companion_settings.json"
@'
{
    "enabled": true,
    "jealousyEnabled": true,
    "tabSnoopingEnabled": true,
    "sleepClockEnabled": true,
    "pomodoroEnabled": true,
    "remindersEnabled": true,
    "diaryEnabled": true,
    "festivalsEnabled": true,
    "danceEnabled": false,
    "isekaiGiftsEnabled": true,
    "midnightKnocksEnabled": true,
    "wallpaperWorldEnabled": true,
    "iconGlassEnabled": true,
    "desktopIconPixelSize": 96,
    "forceGift": "",
    "forceKnockNow": false
}
'@ | Set-Content -Path $settingsPath -Encoding UTF8
Write-Output "settings written"
Start-Sleep -Seconds 5
Find-Log "Hot-reloaded"
Write-Output ""
Write-Output "=== B4: screenshot after dance stopped ==="
Save-Screen "B4_no_dance.png"
Write-Output "=== B5: profile so far ==="
Get-CompanionData "companion_profile.json"
