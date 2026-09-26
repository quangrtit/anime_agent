Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Threading;
public static class InputTest {
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int v);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION u; }

    public static string TestSendInput() {
        INPUT[] down = new INPUT[1];
        down[0].type = 0;
        down[0].u.mi.dwFlags = 0x0002; // LEFTDOWN
        uint r1 = SendInput(1, down, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(120);
        short s1 = GetAsyncKeyState(0x01);
        INPUT[] up = new INPUT[1];
        up[0].type = 0;
        up[0].u.mi.dwFlags = 0x0004; // LEFTUP
        uint r2 = SendInput(1, up, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(80);
        short s2 = GetAsyncKeyState(0x01);
        return string.Format("SendInput: sent={0}/{1}, during=0x{2:X4}(down={3}), afterUp=0x{4:X4}(down={5})",
            r1, r2, s1, (s1 & 0x8000) != 0, s2, (s2 & 0x8000) != 0);
    }

    public static string TestKeybdButton() {
        keybd_event(0x01, 0, 0, UIntPtr.Zero);   // VK_LBUTTON down
        Thread.Sleep(120);
        short s1 = GetAsyncKeyState(0x01);
        keybd_event(0x01, 0, 2, UIntPtr.Zero);   // VK_LBUTTON up
        Thread.Sleep(80);
        short s2 = GetAsyncKeyState(0x01);
        return string.Format("keybd_event: during=0x{0:X4}(down={1}), afterUp=0x{2:X4}(down={3})",
            s1, (s1 & 0x8000) != 0, s2, (s2 & 0x8000) != 0);
    }
}
"@
Write-Output ([InputTest]::TestSendInput())
Write-Output ([InputTest]::TestKeybdButton())
