Write-Output "=== kill stuck mstsc ==="
Stop-Process -Name mstsc -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Write-Output "=== retest input ==="
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class RetestInput {
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
        bool s1 = SetCursorPos(400, 300);
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
        return string.Format("SetCursorPos(400,300)={0}, read=({1},{2}), SendInput visible={3}",
            s1, p.X, p.Y, (st & 0x8000) != 0);
    }
}
"@
Write-Output ([RetestInput]::Test())
