. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class WinMin2 {
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int cmd);
}
"@

Write-Output "=== F1: minimize ZCode + Unity Hub again ==="
foreach ($name in @("ZCode", "Unity Hub")) {
  $procs = Get-Process -Name $name -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -ne "" }
  foreach ($p in $procs) {
    Write-Output ("minimize " + $name + " hwnd=" + $p.MainWindowHandle + " [" + $p.MainWindowTitle + "]")
    [void][WinMin2]::ShowWindowAsync($p.MainWindowHandle, 6)
  }
}
Start-Sleep -Seconds 2

Write-Output "=== F2: identify icons visually ==="
Save-Screen "F2_icons.png"

Write-Output "=== F3: double-click GitHub Desktop (assume y=212 -> click 55,260) with ritual screenshots ==="
$before = (Get-Process).Id
$beforeSet = @{}
Get-Process | ForEach-Object { $beforeSet[$_.Id] = $true }

Move-MouseTo 55 260
Start-Sleep -Milliseconds 500
# double click: two SendInput presses, each held 250ms, 60ms apart
[SiInput]::Button(0x0002); Start-Sleep -Milliseconds 250; [SiInput]::Button(0x0004)
Start-Sleep -Milliseconds 60
[SiInput]::Button(0x0002); Start-Sleep -Milliseconds 250; [SiInput]::Button(0x0004)
Start-Sleep -Milliseconds 150
Save-Screen "F3_flight_mid.png"
Start-Sleep -Milliseconds 1150
Save-Screen "F3_flight_docked.png"
Start-Sleep -Seconds 4

Write-Output "=== F4: new processes? ==="
Get-Process | Where-Object { -not $beforeSet.ContainsKey($_.Id) } | ForEach-Object { "NEW: {0} (pid {1})" -f $_.ProcessName, $_.Id }
Write-Output "--- ritual logs ---"
Find-Log "Icon ritual|Ritual finished|No new process|Glass forwarded"
Move-MouseTo 700 500
