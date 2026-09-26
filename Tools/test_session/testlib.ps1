# Shared helpers for the AnimeAssistant live test session.
$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder t, int c);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public struct POINT { public int X; public int Y; }
}
"@
[void][Win32]::SetProcessDPIAware()
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$script:PlayerLog = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Player.log"
$script:OutDir = "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\logs"
New-Item -ItemType Directory -Force -Path $script:OutDir | Out-Null

# SendInput-based button injection: mouse_event is invisible to GetAsyncKeyState
# in this session after the RDP reconnect, SendInput still works.
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class SiInput {
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION u; }
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
    public static void Button(uint flags) {
        INPUT[] inp = new INPUT[1];
        inp[0].type = 0;
        inp[0].u.mi.dwFlags = flags;
        SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
    }
}
"@

function Move-MouseTo { param([int]$x, [int]$y)
  [void][Win32]::SetCursorPos($x, $y)
  Start-Sleep -Milliseconds 150
}

function Send-LeftDown { param([int]$delayMs = 60)
  [SiInput]::Button(0x0002)
  Start-Sleep -Milliseconds $delayMs
  [SiInput]::Button(0x0004)
}

function Invoke-Click { param([int]$x, [int]$y)
  Move-MouseTo $x $y
  Send-LeftDown
  Start-Sleep -Milliseconds 250
}

function Invoke-DoubleClick { param([int]$x, [int]$y)
  Move-MouseTo $x $y
  Send-LeftDown 50
  Start-Sleep -Milliseconds 110
  Send-LeftDown 50
  Start-Sleep -Milliseconds 300
}

function Invoke-Hotkey { param([byte[]]$keys)
  foreach ($k in $keys) { [Win32]::keybd_event($k, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40 }
  Start-Sleep -Milliseconds 120
  [array]::Reverse($keys)
  foreach ($k in $keys) { [Win32]::keybd_event($k, 0, 2, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40 }
}

function Save-Screen { param([string]$name)
  $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
  $path = Join-Path $script:OutDir $name
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Output "screenshot: $path ($($b.Width)x$($b.Height))"
}

function Get-ForegroundTitle {
  $h = [Win32]::GetForegroundWindow()
  $sb = New-Object System.Text.StringBuilder 512
  [void][Win32]::GetWindowText($h, $sb, 512)
  return $sb.ToString()
}

function Get-LogTail { param([int]$n = 40)
  if (Test-Path $script:PlayerLog) { Get-Content $script:PlayerLog -Tail $n } else { Write-Output "(no Player.log)" }
}

function Find-Log { param([string]$pattern)
  if (-not (Test-Path $script:PlayerLog)) { Write-Output "(no Player.log)"; return }
  $matches = Select-String -Path $script:PlayerLog -Pattern $pattern
  if ($matches) { $matches | ForEach-Object { $_.Line } } else { Write-Output "(no match: $pattern)" }
}

function Copy-Log { param([string]$name)
  if (Test-Path $script:PlayerLog) {
    Copy-Item $script:PlayerLog (Join-Path $script:OutDir $name) -Force
    Write-Output "log saved: $name"
  }
}

function Get-VisibleWindowCount {
  # Mirrors DesktopActivityProbe.CountVisibleTopLevelWindows
  Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class WinEnum {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder t, int c);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  public static int Count() {
    int count = 0;
    IntPtr fg = GetForegroundWindow();
    EnumWindows((h, l) => {
      if (!IsWindowVisible(h) || h == fg) return true;
      int len = GetWindowText(h, null, 0);
      if (len <= 0) return true;
      var sb = new StringBuilder(len + 1);
      GetWindowText(h, sb, sb.Capacity);
      var title = sb.ToString();
      if (title.Length == 0 || title.Contains("Program Manager") || title.Contains("Windows Input Experience") || title.Contains("Settings")) return true;
      count++;
      return true;
    }, IntPtr.Zero);
    return count;
  }
  public static System.Collections.Generic.List<string> Titles() {
    var list = new System.Collections.Generic.List<string>();
    IntPtr fg = GetForegroundWindow();
    EnumWindows((h, l) => {
      if (!IsWindowVisible(h) || h == fg) return true;
      int len = GetWindowText(h, null, 0);
      if (len <= 0) return true;
      var sb = new StringBuilder(len + 1);
      GetWindowText(h, sb, sb.Capacity);
      var title = sb.ToString();
      if (title.Length == 0 || title.Contains("Program Manager") || title.Contains("Windows Input Experience") || title.Contains("Settings")) return true;
      list.Add(title);
      return true;
    }, IntPtr.Zero);
    return list;
  }
}
"@ -ErrorAction SilentlyContinue
  return [WinEnum]::Count()
}

function Get-VisibleWindowTitles {
  return [WinEnum]::Titles()
}

function Get-CompanionData { param([string]$file)
  $p = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Companion\$file"
  if (Test-Path $p) { Get-Content $p -Raw } else { Write-Output "(missing $file)" }
}

function Show-WallpaperRegistry {
  $dp = Get-ItemProperty "HKCU:\Control Panel\Desktop"
  Write-Output ("WallPaper: [" + $dp.WallPaper + "]")
  $colors = Get-ItemProperty "HKCU:\Control Panel\Colors"
  Write-Output ("Desktop color: [" + $colors.Background + "]")
}
