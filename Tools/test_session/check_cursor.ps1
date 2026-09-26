Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class CurTest {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    public struct POINT { public int X; public int Y; }
    public static string RoundTrip(int x, int y) {
        bool s = SetCursorPos(x, y);
        System.Threading.Thread.Sleep(120);
        POINT p; GetCursorPos(out p);
        return string.Format("Set({0},{1})={2}, read=({3},{4}), match={5}", x, y, s, p.X, p.Y, p.X == x && p.Y == y);
    }
}
"@
Write-Output ([CurTest]::RoundTrip(55, 260))
Write-Output ([CurTest]::RoundTrip(55, 50))
Write-Output ([CurTest]::RoundTrip(700, 500))
