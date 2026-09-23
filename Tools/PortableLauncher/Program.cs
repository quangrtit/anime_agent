using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AnimeAssistant.PortableLauncher;

internal static class Program
{
    private const string PayloadName = "AnimeAssistantRuntime.zip";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadName)
                ?? throw new InvalidOperationException("The embedded Unity runtime payload is missing.");
            var digest = Convert.ToHexString(SHA256.HashData(payload)).Substring(0, 16);
            payload.Position = 0;

            var runtimeDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AnimeDesktopAssistant", "Runtime", digest);
            var readyMarker = Path.Combine(runtimeDirectory, ".ready");
            if (!File.Exists(readyMarker))
            {
                Directory.CreateDirectory(runtimeDirectory);
                ZipFile.ExtractToDirectory(payload, runtimeDirectory, overwriteFiles: true);
                File.WriteAllText(readyMarker, digest);
            }

            CopyEditableConfig(runtimeDirectory);
            var playerPath = Path.Combine(runtimeDirectory, "AnimeAssistant.exe");
            if (!File.Exists(playerPath))
            {
                throw new FileNotFoundException("The extracted Unity player is incomplete.", playerPath);
            }

            var startInfo = new ProcessStartInfo(playerPath)
            {
                WorkingDirectory = runtimeDirectory,
                UseShellExecute = false
            };
            foreach (var argument in args)
            {
                startInfo.ArgumentList.Add(argument);
            }
            Process.Start(startInfo);
            return 0;
        }
        catch (Exception exception)
        {
            MessageBox(IntPtr.Zero, exception.Message, "Anime Desktop Assistant", 0x10);
            return 1;
        }
    }

    private static void CopyEditableConfig(string runtimeDirectory)
    {
        var launcherPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot resolve the portable launcher path.");
        var launcherDirectory = Path.GetDirectoryName(launcherPath)
            ?? throw new InvalidOperationException("Cannot resolve the portable launcher directory.");
        var flatConfig = Path.Combine(launcherDirectory, "desktop_layout.json");
        var nestedConfig = Path.Combine(launcherDirectory, "Config", "desktop_layout.json");
        var source = File.Exists(flatConfig) ? flatConfig : nestedConfig;
        if (!File.Exists(source))
        {
            return;
        }

        var targetDirectory = Path.Combine(runtimeDirectory, "Config");
        Directory.CreateDirectory(targetDirectory);
        File.Copy(source, Path.Combine(targetDirectory, "desktop_layout.json"), overwrite: true);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
