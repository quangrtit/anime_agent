. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class FgWin {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr FindWindowW(string cls, string title);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder t, int c);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    public static IntPtr FindByTitlePart(string part) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) => {
            if (!IsWindowVisible(h)) return true;
            var sb = new StringBuilder(256);
            GetWindowText(h, sb, 256);
            if (sb.ToString().Contains(part)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@

Write-Output "=== L1: summon + catch door transition (backdrop) ==="
Invoke-Click 1310 760
Start-Sleep -Milliseconds 800
Save-Screen "L1_transition1.png"
Start-Sleep -Milliseconds 900
Save-Screen "L1_transition2.png"
Start-Sleep -Seconds 5
Find-Log "SummonCycle" | Select-Object -Last 3

Write-Output "=== L2: bring jealousy-bait notepad to foreground ==="
$hwnd = [FgWin]::FindByTitlePart("cute_girl_waifu")
if ($hwnd -ne [IntPtr]::Zero) {
  [void][FgWin]::SetForegroundWindow($hwnd)
  Write-Output "bait window foregrounded"
} else { Write-Output "BAIT WINDOW NOT FOUND" }

Write-Output "=== L3: wait 26s (jealousy needs >=20s after summon, 2s cadence) ==="
Start-Sleep -Seconds 26
Find-Log "jealous|Say|Diary" | Select-Object -Last 6

Write-Output "=== L4: open 8 cmd windows for snooping ==="
for ($i = 1; $i -le 8; $i++) {
  cmd /c start "snoop_window_$i" cmd /c "timeout /t 900 >nul"
  Start-Sleep -Milliseconds 350
}
Start-Sleep -Seconds 2
Write-Output ("visible windows now: " + (Get-VisibleWindowCount))
Write-Output "=== L5: wait 240s+ for the snooping countdown (fires once at 240s after summon) ==="
Start-Sleep -Seconds 235
Find-Log "snooping|Say|Count" | Select-Object -Last 6
Save-Screen "L5_snooping.png"
Move-MouseTo 700 500
