. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== J1: state before ==="
Show-WallpaperRegistry
$compDir = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Companion"
Get-ChildItem $compDir | ForEach-Object { "{0,8} {1}" -f $_.Length, $_.Name }

Write-Output "=== J2: dismiss (click door) -> she goes home -> wallpaper swaps, door stays open (backdrop visible) ==="
Invoke-Click 1330 640
Start-Sleep -Seconds 14
Save-Screen "J2_door_open_backdrop.png"
Show-WallpaperRegistry
Find-Log "SummonCycle|Wallpaper|wallpaper|Say" | Select-Object -Last 8

Write-Output "=== J3: re-summon (click recall) -> restore attempt (color file missing = expect FAIL) ==="
Invoke-Click 1310 760
Start-Sleep -Seconds 7
Show-WallpaperRegistry
Find-Log "Say|Restore|Wallpaper" | Select-Object -Last 4

Write-Output "=== J4: write the color file manually, dismiss+summon again -> restore should succeed ==="
Set-Content -Path (Join-Path $compDir "original_wallpaper_color.txt") -Value "0 0 0" -Encoding ASCII
Invoke-Click 1330 640
Start-Sleep -Seconds 14
Invoke-Click 1310 760
Start-Sleep -Seconds 7
Show-WallpaperRegistry
Get-ChildItem $compDir | ForEach-Object { "{0,8} {1}" -f $_.Length, $_.Name }
Save-Screen "J4_after_restore.png"
