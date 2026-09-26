using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// Desktop-icon access for the "glass pane" launch ritual: reads icon
    /// positions and labels from Explorer's SysListView32 via cross-process
    /// ListView messages, and can forward synthetic clicks back into the
    /// desktop after the companion has performed her animation.
    /// </summary>
    public sealed class DesktopIconProbe
    {
        private const uint LvmFirst = 0x1000;
        private const uint LvmGetItemCount = LvmFirst + 4;
        private const uint LvmGetItemPosition = LvmFirst + 16;
        private const uint LvmGetItemTextW = LvmFirst + 115;
        private const uint LvmIfText = 0x0001;

        private const uint ProcessVmOperation = 0x0008;
        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessQueryInformation = 0x0400;
        private const uint MemCommit = 0x1000;
        private const uint MemReserve = 0x2000;
        private const uint PageReadWrite = 0x04;
        private const uint MemRelease = 0x8000;

        private const uint WmLButtonDown = 0x0201;
        private const uint WmLButtonUp = 0x0202;
        private const uint WmLButtonDblClk = 0x0203;
        private static readonly IntPtr MkLButton = new IntPtr(0x0001);

        private IntPtr listViewHandle;

        public readonly struct DesktopIconInfo
        {
            public DesktopIconInfo(string label, int clientX, int clientY, int screenX, int screenY)
            {
                Label = label;
                ClientX = clientX;
                ClientY = clientY;
                ScreenX = screenX;
                ScreenY = screenY;
            }

            public string Label { get; }
            public int ClientX { get; }
            public int ClientY { get; }
            public int ScreenX { get; }
            public int ScreenY { get; }
        }

        public static bool IsDesktopForeground()
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                return false;
            }

            var builder = new StringBuilder(64);
            GetClassName(foreground, builder, builder.Capacity);
            var className = builder.ToString();
            return className == "Progman" || className == "WorkerW";
        }

        public static bool TryGetCursorPos(out int x, out int y)
        {
            var ok = GetCursorPos(out var point);
            x = point.X;
            y = point.Y;
            return ok;
        }

        /// <summary>
        /// Reads every desktop icon (label + client/screen position). Returns
        /// false when the desktop ListView cannot be reached (shell
        /// replacements, elevated Explorer, etc.).
        /// </summary>
        public bool TryScrapeIcons(List<DesktopIconInfo> destination)
        {
            destination.Clear();
            try
            {
                if (!EnsureListViewHandle())
                {
                    return false;
                }

                var explorerProcessId = 0u;
                GetWindowThreadProcessId(listViewHandle, out explorerProcessId);
                if (explorerProcessId == 0)
                {
                    return false;
                }

                var explorer = OpenProcess(
                    ProcessVmOperation | ProcessVmRead | ProcessQueryInformation, false, explorerProcessId);
                if (explorer == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    var count = (int)SendMessage(listViewHandle, LvmGetItemCount, IntPtr.Zero, IntPtr.Zero).ToInt64();
                    if (count <= 0 || count > 2000)
                    {
                        return false;
                    }

                    var remote = VirtualAllocEx(explorer, IntPtr.Zero,
                        (UIntPtr)(1024 * 4), MemCommit | MemReserve, PageReadWrite);
                    if (remote == IntPtr.Zero)
                    {
                        return false;
                    }

                    try
                    {
                        var localPoint = new byte[8];
                        var zero = new byte[8];
                        for (var i = 0; i < count; i++)
                        {
                            WriteProcessMemory(explorer, remote, zero, (UIntPtr)zero.Length, out _);
                            SendMessage(listViewHandle, LvmGetItemPosition, (IntPtr)i, remote);
                            if (!ReadProcessMemory(explorer, remote, localPoint, (UIntPtr)8, out _))
                            {
                                continue;
                            }

                            var clientX = BitConverter.ToInt32(localPoint, 0);
                            var clientY = BitConverter.ToInt32(localPoint, 4);
                            var screen = new NativePoint { X = clientX, Y = clientY };
                            MapWindowPoints(listViewHandle, IntPtr.Zero, ref screen, 1);
                            var text = ReadItemText(explorer, remote, i);
                            destination.Add(new DesktopIconInfo(text, clientX, clientY, screen.X, screen.Y));
                        }

                        return destination.Count > 0;
                    }
                    finally
                    {
                        VirtualFreeEx(explorer, remote, UIntPtr.Zero, MemRelease);
                    }
                }
                finally
                {
                    CloseHandle(explorer);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Icon scrape failed: {exception.Message}");
                return false;
            }
        }

        private string ReadItemText(IntPtr explorer, IntPtr remote, int index)
        {
            var textRemote = IntPtr.Add(remote, 128);
            var lvItem = new LvItem
            {
                Mask = LvmIfText,
                Item = index,
                SubItem = 0,
                TextPointer = textRemote,
                TextMax = 260
            };
            var size = Marshal.SizeOf<LvItem>();
            var local = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(lvItem, local, false);
                var bytes = new byte[size];
                Marshal.Copy(local, bytes, 0, size);
                var zeroText = new byte[520];
                WriteProcessMemory(explorer, textRemote, zeroText, (UIntPtr)zeroText.Length, out _);
                WriteProcessMemory(explorer, remote, bytes, (UIntPtr)size, out _);
                SendMessage(listViewHandle, LvmGetItemTextW, (IntPtr)index, remote);
                var textBytes = new byte[520];
                if (!ReadProcessMemory(explorer, textRemote, textBytes, (UIntPtr)520, out _))
                {
                    return "";
                }

                var text = Encoding.Unicode.GetString(textBytes);
                var terminator = text.IndexOf('\0');
                return terminator >= 0 ? text.Substring(0, terminator) : text;
            }
            finally
            {
                Marshal.FreeHGlobal(local);
            }
        }

        /// <summary>
        /// Forwards a synthetic click into the desktop ListView at item-client
        /// coordinates — this is how the glass pane "hands the click back" to
        /// Windows once the animation is done. A double click activates the
        /// item, which is what actually launches the app.
        /// </summary>
        public bool ForwardClick(int clientX, int clientY, bool doubleClick)
        {
            if (!EnsureListViewHandle())
            {
                return false;
            }

            var lParam = (IntPtr)((clientY << 16) | (clientX & 0xFFFF));
            if (doubleClick)
            {
                SendMessage(listViewHandle, WmLButtonDown, MkLButton, lParam);
                SendMessage(listViewHandle, WmLButtonUp, IntPtr.Zero, lParam);
                SendMessage(listViewHandle, WmLButtonDblClk, MkLButton, lParam);
                SendMessage(listViewHandle, WmLButtonUp, IntPtr.Zero, lParam);
            }
            else
            {
                SendMessage(listViewHandle, WmLButtonDown, MkLButton, lParam);
                SendMessage(listViewHandle, WmLButtonUp, IntPtr.Zero, lParam);
            }
            return true;
        }

        /// <summary>
        /// Fallback launch when the ListView forward produced nothing: real
        /// input events at the icon's screen position (cursor restored after).
        /// </summary>
        public void ReplayDoubleClickAt(int screenX, int screenY)
        {
            TryGetCursorPos(out var savedX, out var savedY);
            SetCursorPos(screenX, screenY);
            // Our own glass must not swallow these; the director raises a
            // replay guard so the synthetic clicks are ignored by the ritual.
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); // LEFTDOWN
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); // LEFTUP
            System.Threading.Thread.Sleep(90);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            SetCursorPos(savedX, savedY);
        }

        public static HashSet<int> SnapshotProcessIds()
        {
            var ids = new HashSet<int>();
            try
            {
                foreach (var process in System.Diagnostics.Process.GetProcesses())
                {
                    ids.Add(process.Id);
                    process.Dispose();
                }
            }
            catch (Exception)
            {
            }
            return ids;
        }

        private bool EnsureListViewHandle()
        {
            if (listViewHandle != IntPtr.Zero && IsWindow(listViewHandle))
            {
                return true;
            }

            var progman = FindWindow("Progman", null);
            var defView = progman != IntPtr.Zero
                ? FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null)
                : IntPtr.Zero;
            if (defView == IntPtr.Zero)
            {
                // When the wallpaper host moved to a WorkerW, scan them all.
                EnumWindows((window, _) =>
                {
                    var builder = new StringBuilder(64);
                    GetClassName(window, builder, builder.Capacity);
                    if (builder.ToString() != "WorkerW")
                    {
                        return true;
                    }

                    var candidate = FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (candidate != IntPtr.Zero)
                    {
                        defView = candidate;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }

            if (defView == IntPtr.Zero)
            {
                return false;
            }

            listViewHandle = FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
            return listViewHandle != IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LvItem
        {
            public uint Mask;
            public int Item;
            public int SubItem;
            public uint State;
            public uint StateMask;
            public IntPtr TextPointer;
            public int TextMax;
            public int Image;
            public IntPtr Param;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string windowName);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        private static extern int MapWindowPoints(IntPtr from, IntPtr to, ref NativePoint point, uint count);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extraInfo);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size,
            uint allocationType, uint protect);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, uint freeType);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr process, IntPtr baseAddress,
            byte[] buffer, UIntPtr size, out IntPtr written);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr process, IntPtr baseAddress,
            byte[] buffer, UIntPtr size, out IntPtr read);
    }
}
