Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class DiagInput {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool GetClipCursor(out RECT r);
    [DllImport("user32.dll")] public static extern bool ClipCursor(IntPtr rect);
    [DllImport("kernel32.dll")] public static extern uint GetLastError();
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int v);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);

    public struct POINT { public int X; public int Y; }
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION u; }

    public static string Diagnose() {
        var sb = new System.Text.StringBuilder();
        RECT clip; GetClipCursor(out clip);
        sb.AppendLine("clip rect: " + clip.Left + "," + clip.Top + " - " + clip.Right + "," + clip.Bottom);
        bool ok = SetCursorPos(400, 300);
        sb.AppendLine("SetCursorPos(400,300)=" + ok + " lasterror=" + Marshal.GetLastWin32Error());
        POINT p; GetCursorPos(out p);
        sb.AppendLine("cursor now: " + p.X + "," + p.Y);
        INPUT[] down = new INPUT[1];
        down[0].type = 0; down[0].u.mi.dwFlags = 0x0002;
        uint sent = SendInput(1, down, Marshal.SizeOf(typeof(INPUT)));
        System.Threading.Thread.Sleep(150);
        short st = GetAsyncKeyState(0x01);
        sb.AppendLine("SendInput down: sent=" + sent + " asyncState=0x" + (st & 0xFFFF).ToString("X4") + " visible=" + ((st & 0x8000) != 0));
        INPUT[] up = new INPUT[1];
        up[0].type = 0; up[0].u.mi.dwFlags = 0x0004;
        SendInput(1, up, Marshal.SizeOf(typeof(INPUT)));
        return sb.ToString();
    }
}
"@
Write-Output ([DiagInput]::Diagnose())
