Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class KeyState {
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] public static extern short GetKeyState(int vKey);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public MOUSEINPUT mi; }
}
"@
$async = [KeyState]::GetAsyncKeyState(0x01)
$sync = [KeyState]::GetKeyState(0x01)
Write-Output ("idle  : GetAsyncKeyState(VK_LBUTTON)=0x{0:X4}  GetKeyState=0x{1:X4}" -f ($async -band 0xFFFF), ($sync -band 0xFFFF))

# press and hold via mouse_event, sample in the same thread right after
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Threading;
public static class KeyPress {
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int v);
    public static string PressAndSample() {
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        short s1 = GetAsyncKeyState(0x01);
        Thread.Sleep(150);
        short s2 = GetAsyncKeyState(0x01);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(80);
        short s3 = GetAsyncKeyState(0x01);
        return string.Format("during: 0x{0:X4} (down={1}), after150ms: 0x{2:X4} (down={3}), afterUp: 0x{4:X4} (down={5})",
            s1, (s1 & 0x8000) != 0, s2, (s2 & 0x8000) != 0, s3, (s3 & 0x8000) != 0);
    }
}
"@
Write-Output ("inject: " + [KeyPress]::PressAndSample())
Write-Output ("after all: GetAsyncKeyState=0x{0:X4}" -f ([KeyState]::GetAsyncKeyState(0x01) -band 0xFFFF))
