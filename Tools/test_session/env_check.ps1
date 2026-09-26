$ErrorActionPreference = 'Continue'
$pp = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant"
Write-Output "persistentDataPath: $pp"
Write-Output "--- Companion files ---"
if (Test-Path $pp) {
  Get-ChildItem $pp -Recurse -File | ForEach-Object { "{0,10} {1} {2}" -f $_.Length, $_.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), $_.FullName.Substring($pp.Length) }
} else { Write-Output "(missing)" }
Write-Output "--- Desktop\Airi ---"
$airi = "$env:USERPROFILE\Desktop\Airi"
if (Test-Path $airi) { Get-ChildItem $airi | ForEach-Object { $_.Name } } else { Write-Output "(missing)" }
Write-Output "--- Wallpaper registry ---"
$dp = Get-ItemProperty "HKCU:\Control Panel\Desktop"
Write-Output ("WallPaper: " + $dp.WallPaper)
Write-Output ("WallpaperStyle: " + $dp.WallpaperStyle)
$colors = Get-ItemProperty "HKCU:\Control Panel\Colors"
Write-Output ("Background color: " + $colors.Background)
Write-Output "--- Companion dir (raw) ---"
$comp = Join-Path $pp "Companion"
if (Test-Path $comp) { Get-ChildItem $comp | ForEach-Object { $_.Name } } else { Write-Output "(missing)" }
Write-Output "--- Player.log tail info ---"
$log = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Player.log"
if (Test-Path $log) {
  $fi = Get-Item $log
  Write-Output ("Player.log: " + $fi.Length + " bytes, last write " + $fi.LastWriteTime)
} else { Write-Output "(no Player.log)" }
Write-Output "--- Screen ---"
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Screen]::AllScreens | ForEach-Object { "Screen: $($_.Bounds) Primary=$($_.Primary)" }
Write-Output "--- Desktop icon count (shell COM) ---"
$shell = New-Object -ComObject Shell.Application
$desktop = $shell.Namespace(0x0)
Write-Output ("Desktop items: " + $desktop.Items().Count)
$desktop.Items() | Select-Object -First 30 | ForEach-Object { "icon: " + $_.Name }
Write-Output "--- Processes of interest ---"
Get-Process | Where-Object { $_.MainWindowTitle -ne "" } | Select-Object Id, ProcessName, MainWindowTitle | ForEach-Object { "winproc: $($_.Id) $($_.ProcessName) [$($_.MainWindowTitle)]" }
