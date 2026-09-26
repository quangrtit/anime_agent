. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class WinMin {
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder t, int c);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder t, int c);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
}
"@

Write-Output "=== D0: minimize ZCode + mstsc windows ==="
$zcodeProc = Get-Process -Name "ZCode" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
foreach ($p in $zcodeProc) {
  Write-Output ("minimize ZCode hwnd=" + $p.MainWindowHandle)
  [void][WinMin]::ShowWindowAsync($p.MainWindowHandle, 6)  # SW_MINIMIZE
}
$rdpProc = Get-Process -Name "mstsc" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
foreach ($p in $rdpProc) {
  Write-Output ("minimize mstsc hwnd=" + $p.MainWindowHandle)
  [void][WinMin]::ShowWindowAsync($p.MainWindowHandle, 6)
}
Start-Sleep -Seconds 2

Write-Output "=== D0b: enumerate visible top-level windows now ==="
$cb = {
  param($h, $l)
  if ([WinMin]::IsWindowVisible($h)) {
    $t = New-Object System.Text.StringBuilder 256
    [void][WinMin]::GetWindowText($h, $t, 256)
    $c = New-Object System.Text.StringBuilder 128
    [void][WinMin]::GetClassName($h, $c, 128)
    $pid2 = 0
    [void][WinMin]::GetWindowThreadProcessId($h, [ref]$pid2)
    if ($t.ToString() -ne "") { Write-Output ("hwnd=0x{0:X} pid={1} class={2} title=[{3}]" -f $h.ToInt64(), $pid2, $c, $t) }
  }
  return $true
}
[void][WinMin]::EnumWindows($cb, [IntPtr]::Zero)

Write-Output "=== D1: screenshot clean desktop ==="
Save-Screen "D1_clean_desktop.png"
