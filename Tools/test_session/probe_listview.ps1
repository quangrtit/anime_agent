Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class LvProbe {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string win);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string win);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder t, int c);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern int MapWindowPoints(IntPtr from, IntPtr to, ref POINT pt, uint count);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] public static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr addr, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll")] public static extern bool VirtualFreeEx(IntPtr h, IntPtr addr, UIntPtr size, uint type);
    [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr h, IntPtr baseAddr, byte[] buf, UIntPtr size, out IntPtr written);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, IntPtr baseAddr, byte[] buf, UIntPtr size, out IntPtr read);
    [DllImport("user32.dll")] public static extern int GetWindowRect(IntPtr h, out RECT r);

    public struct POINT { public int X; public int Y; }
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct LvItem {
        public uint Mask; public int Item; public int SubItem; public uint State; public uint StateMask;
        public IntPtr Text; public int TextMax; public int Image; public IntPtr Param;
    }

    public static IntPtr FindDesktopList() {
        var progman = FindWindow("Progman", null);
        var defView = progman != IntPtr.Zero ? FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null) : IntPtr.Zero;
        if (defView == IntPtr.Zero) {
            EnumWindows((h, l) => {
                var sb = new StringBuilder(64);
                GetClassName(h, sb, 64);
                if (sb.ToString() != "WorkerW") return true;
                var cand = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (cand != IntPtr.Zero) { defView = cand; return false; }
                return true;
            }, IntPtr.Zero);
        }
        return defView != IntPtr.Zero ? FindWindowEx(defView, IntPtr.Zero, "SysListView32", null) : IntPtr.Zero;
    }

    public static string ProbeAll() {
        var list = FindDesktopList();
        if (list == IntPtr.Zero) return "no list";
        uint pid = 0;
        GetWindowThreadProcessId(list, out pid);
        var proc = OpenProcess(0x0008 | 0x0010 | 0x0400, false, pid);
        if (proc == IntPtr.Zero) return "openprocess failed";
        try {
            var sb = new StringBuilder();
            int count = (int)SendMessage(list, 0x1004, IntPtr.Zero, IntPtr.Zero).ToInt64();
            sb.AppendLine("count=" + count + " hwnd=0x" + list.ToInt64().ToString("X"));
            byte[] buf8 = new byte[8];
            IntPtr written;
            IntPtr remote = VirtualAllocEx(proc, IntPtr.Zero, (UIntPtr)4096, 0x1000 | 0x2000, 0x04);
            SendMessage(list, 0x1029, IntPtr.Zero, remote);
            ReadProcessMemory(proc, remote, buf8, (UIntPtr)8, out written);
            int ox = BitConverter.ToInt32(buf8, 0), oy = BitConverter.ToInt32(buf8, 4);
            sb.AppendLine("GETORIGIN(scroll offset): x=" + ox + " y=" + oy);
            SendMessage(list, 0x1026, IntPtr.Zero, remote);
            ReadProcessMemory(proc, remote, buf8, (UIntPtr)8, out written);
            sb.AppendLine("GETVIEWRECT(content bbox): " + BitConverter.ToInt32(buf8, 0) + "," + BitConverter.ToInt32(buf8, 4));
            RECT wr;
            GetWindowRect(list, out wr);
            sb.AppendLine("list window rect: " + wr.Left + "," + wr.Top + " - " + wr.Right + "," + wr.Bottom);
            int n = count < 8 ? count : 8;
            for (int i = 0; i < n; i++) {
                SendMessage(list, 0x1010, (IntPtr)i, remote);
                ReadProcessMemory(proc, remote, buf8, (UIntPtr)8, out written);
                int px = BitConverter.ToInt32(buf8, 0), py = BitConverter.ToInt32(buf8, 4);
                byte[] rectBytes = new byte[16];
                WriteProcessMemory(proc, remote, rectBytes, (UIntPtr)16, out written);
                SendMessage(list, 0x100E, (IntPtr)i, remote);
                ReadProcessMemory(proc, remote, rectBytes, (UIntPtr)16, out written);
                int rl = BitConverter.ToInt32(rectBytes, 0), rt = BitConverter.ToInt32(rectBytes, 4);
                int rr = BitConverter.ToInt32(rectBytes, 8), rb = BitConverter.ToInt32(rectBytes, 12);
                string label = ReadText(proc, remote, i, list);
                POINT p1 = new POINT(); p1.X = px; p1.Y = py;
                MapWindowPoints(list, IntPtr.Zero, ref p1, 1);
                POINT p2 = new POINT(); p2.X = rl; p2.Y = rt;
                MapWindowPoints(list, IntPtr.Zero, ref p2, 1);
                sb.AppendLine(string.Format("item[{0}] '{1}': POSITION=({2},{3})->screen({4},{5}) | RECT=({6},{7},{8},{9})->screenTL({10},{11})",
                    i, label, px, py, p1.X, p1.Y, rl, rt, rr, rb, p2.X, p2.Y));
            }
            VirtualFreeEx(proc, remote, UIntPtr.Zero, 0x8000);
            return sb.ToString();
        } finally { CloseHandle(proc); }
    }

    static string ReadText(IntPtr proc, IntPtr remote, int index, IntPtr list) {
        var textRemote = IntPtr.Add(remote, 128);
        LvItem lv = new LvItem(); lv.Mask = 0x0001; lv.Item = index; lv.SubItem = 0; lv.Text = textRemote; lv.TextMax = 260;
        int size = Marshal.SizeOf(typeof(LvItem));
        IntPtr local = Marshal.AllocHGlobal(size);
        try {
            Marshal.StructureToPtr(lv, local, false);
            byte[] bytes = new byte[size];
            Marshal.Copy(local, bytes, 0, size);
            byte[] zero = new byte[520];
            IntPtr written;
            WriteProcessMemory(proc, textRemote, zero, (UIntPtr)520, out written);
            WriteProcessMemory(proc, remote, bytes, (UIntPtr)size, out written);
            SendMessage(list, 0x1073, (IntPtr)index, remote);
            byte[] tb = new byte[520];
            if (!ReadProcessMemory(proc, textRemote, tb, (UIntPtr)520, out written)) return "";
            string s = Encoding.Unicode.GetString(tb);
            int t = s.IndexOf('\0');
            return t >= 0 ? s.Substring(0, t) : s;
        } finally { Marshal.FreeHGlobal(local); }
    }

    public static string ScrollTo(int dx, int dy) {
        var list = FindDesktopList();
        if (list == IntPtr.Zero) return "no list";
        SendMessage(list, 0x1014, (IntPtr)dx, (IntPtr)dy);
        return "scrolled " + dx + "," + dy;
    }
}
"@

Write-Output "=== before ==="
[LvProbe]::ProbeAll()
Write-Output "=== scroll back up ==="
[LvProbe]::ScrollTo(0, -2000)
Start-Sleep -Milliseconds 500
Write-Output "=== after ==="
[LvProbe]::ProbeAll()
