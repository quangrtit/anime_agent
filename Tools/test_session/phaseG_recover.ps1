. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

Write-Output "=== G1: app hang confirmation ==="
$proc = Get-Process -Name "AnimeAssistant" -ErrorAction SilentlyContinue
if ($proc) {
  Write-Output ("AnimeAssistant pid=" + $proc.Id + " responding=" + $proc.Responding + " CPU=" + $proc.CPU + " threads=" + $proc.Threads.Count)
} else { Write-Output "app not running" }
$log = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Player.log"
$fi = Get-Item $log
Write-Output ("Player.log last write: " + $fi.LastWriteTime.ToString("HH:mm:ss") + " (now: " + (Get-Date).ToString("HH:mm:ss") + ")")

Write-Output "=== G2: Explorer responsive? (SendMessageTimeout probe) ==="
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class ExplProbe {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string w);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string w);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    public static string Probe() {
        var progman = FindWindow("Progman", null);
        var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        var list = FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
        if (list == IntPtr.Zero) return "no list";
        IntPtr res;
        IntPtr ok = SendMessageTimeout(list, 0x1004, IntPtr.Zero, IntPtr.Zero, 0x0002, 3000, out res); // SMTO_ABORTIFHUNG
        return "SendMessageTimeout(GETITEMCOUNT): " + (ok != IntPtr.Zero ? "OK count=" + res.ToInt64() : "TIMEOUT/HUNG");
    }
}
"@
Write-Output ([ExplProbe]::Probe())

Write-Output "=== G3: kill the hung app ==="
if ($proc) { Stop-Process -Id $proc.Id -Force; Start-Sleep -Seconds 3 }
Write-Output "killed"

Write-Output "=== G4: retest input after killing hung app ==="
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class Retest2 {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int v);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
    public struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION u; }
    public static string Test() {
        bool s1 = SetCursorPos(700, 500);
        System.Threading.Thread.Sleep(100);
        POINT p; GetCursorPos(out p);
        INPUT[] down = new INPUT[1];
        down[0].type = 0; down[0].u.mi.dwFlags = 0x0002;
        SendInput(1, down, Marshal.SizeOf(typeof(INPUT)));
        System.Threading.Thread.Sleep(120);
        short st = GetAsyncKeyState(0x01);
        INPUT[] up = new INPUT[1];
        up[0].type = 0; up[0].u.mi.dwFlags = 0x0004;
        SendInput(1, up, Marshal.SizeOf(typeof(INPUT)));
        return string.Format("SetCursorPos={0}, read=({1},{2}), SendInput visible={3}", s1, p.X, p.Y, (st & 0x8000) != 0);
    }
}
"@
Write-Output ([Retest2]::Test())
