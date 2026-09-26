. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== C1: wallpaper registry after swap ==="
Show-WallpaperRegistry
Write-Output "=== C2: Companion folder contents ==="
$comp = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Companion"
Get-ChildItem $comp | ForEach-Object { "{0,10} {1}" -f $_.Length, $_.Name }
Write-Output ""
Write-Output "=== C3: move cursor to neutral spot, then click recall button (door) to re-summon ==="
Move-MouseTo 700 500
Start-Sleep -Milliseconds 300
Invoke-Click 1310 760
Start-Sleep -Seconds 5
Find-Log "SummonCycle"
Write-Output "=== C4: cursor away, glass should rebuild; screenshot ==="
Move-MouseTo 700 500
Start-Sleep -Seconds 3
Save-Screen "C4_resummoned.png"
Find-Log "Icon glass"
