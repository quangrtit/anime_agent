Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class DeskTest {
    [DllImport("user32.dll")] public static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr OpenDesktop(string name, uint flags, bool inherit, uint access);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern IntPtr GetThreadDesktop(uint tid);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder t, int c);
    public static string Test() {
        var sb = new System.Text.StringBuilder();
        IntPtr inputDesk = OpenInputDesktop(0, false, 0x02000000);
        sb.AppendLine("OpenInputDesktop: " + (inputDesk != IntPtr.Zero ? "OK 0x" + inputDesk.ToInt64().ToString("X") : "FAILED"));
        if (inputDesk != IntPtr.Zero) CloseDesktop(inputDesk);
        IntPtr defDesk = OpenDesktop("Default", 0, false, 0x02000000);
        sb.AppendLine("OpenDesktop(Default): " + (defDesk != IntPtr.Zero ? "OK" : "FAILED"));
        if (defDesk != IntPtr.Zero) CloseDesktop(defDesk);
        IntPtr td = GetThreadDesktop(GetCurrentThreadId());
        sb.AppendLine("thread desktop handle: 0x" + td.ToInt64().ToString("X"));
        IntPtr fg = GetForegroundWindow();
        var t = new System.Text.StringBuilder(256);
        GetWindowText(fg, t, 256);
        sb.AppendLine("foreground: [" + t + "]");
        return sb.ToString();
    }
}
"@
Write-Output ([DeskTest]::Test())
