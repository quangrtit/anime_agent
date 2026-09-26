using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// Passive, local-only desktop telemetry for the companion: foreground
    /// window title, user idle time, visible window count, system uptime and
    /// raw key state. No screen capture, no clipboard, nothing leaves the
    /// process. Win32 calls degrade to no-ops on non-Windows editors.
    /// </summary>
    public static class DesktopActivityProbe
    {
        public const int VkMenu = 0x12; // Alt
        public const int VkP = 0x50;
        public const int VkC = 0x43;
        public const int VkD = 0x44;
        public const int VkN = 0x4E;

        public static string GetForegroundWindowTitle()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero)
            {
                return "";
            }

            var builder = new StringBuilder(512);
            return GetWindowText(window, builder, builder.Capacity) > 0 ? builder.ToString() : "";
#else
            return "";
#endif
        }

        public static double GetUserIdleSeconds()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var info = new LastInputInfo { Size = Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref info))
            {
                return 0.0;
            }

            var idleMilliseconds = unchecked((uint)Environment.TickCount - info.Time);
            return idleMilliseconds / 1000.0;
#else
            return 0.0;
#endif
        }

        public static int CountVisibleTopLevelWindows()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var count = 0;
            EnumWindows((window, _) =>
            {
                if (!IsWindowVisible(window) || window == GetForegroundWindow())
                {
                    return true;
                }

                var length = GetWindowTextLength(window);
                if (length <= 0)
                {
                    return true;
                }

                var builder = new StringBuilder(length + 1);
                GetWindowText(window, builder, builder.Capacity);
                var title = builder.ToString();
                if (title.Length == 0 ||
                    title.Contains("Program Manager") ||
                    title.Contains("Windows Input Experience") ||
                    title.Contains("Settings"))
                {
                    return true;
                }

                count++;
                return true;
            }, IntPtr.Zero);
            return count;
#else
            return 0;
#endif
        }

        public static double GetSystemUptimeHours()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return GetTickCount64() / 1000.0 / 3600.0;
#else
            return (double)Environment.TickCount / 1000f / 3600f;
#endif
        }

        public static bool IsKeyDown(int virtualKey)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private delegate bool EnumWindowsProc(IntPtr window, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo
        {
            public int Size;
            public uint Time;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LastInputInfo info);

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
#endif
    }
}
