using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AnimeAssistant.AgentHost;

internal sealed class CommandExecutor
{
    private readonly string projectDirectory;
    private readonly string workspaceDirectory;
    private readonly bool dryRun;

    public CommandExecutor(string projectDirectory, string workspaceDirectory, bool dryRun)
    {
        this.projectDirectory = projectDirectory;
        this.workspaceDirectory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(workspaceDirectory));
        this.dryRun = dryRun;
    }

    public void Execute(AgentCommand command)
    {
        if (dryRun || command.Intent == "reply") return;
        switch (command.Intent)
        {
            case "open_app": Start(command.Argument!); break;
            case "open_project": Start(projectDirectory); break;
            case "open_workspace": Directory.CreateDirectory(workspaceDirectory); Start(workspaceDirectory); break;
            case "open_location": Start(ResolveKnownLocation(command.Argument!)); break;
            case "create_folder": CreateFolder(command.Argument!); break;
            case "create_file": CreateFile(command.Argument!); break;
            case "web_search": Start("https://www.google.com/search?q=" + Uri.EscapeDataString(command.Argument!)); break;
            case "volume_up": PressKey(0xAF, 3); break;
            case "volume_down": PressKey(0xAE, 3); break;
            case "volume_mute": PressKey(0xAD); break;
            case "media_play_pause": PressKey(0xB3); break;
            case "media_next": PressKey(0xB0); break;
            case "media_previous": PressKey(0xB1); break;
            case "show_desktop": PressChord(0x5B, 0x44); break;
            case "window_switch": PressChord(0x12, 0x09); break;
            case "window_minimize": PressChord(0x5B, 0x28); break;
            case "window_maximize": PressChord(0x5B, 0x26); break;
            default: throw new InvalidOperationException($"Blocked unknown action: {command.Intent}");
        }
    }

    private static void Start(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    private void CreateFolder(string spokenName) =>
        Directory.CreateDirectory(SafeWorkspacePath(spokenName, addTextExtension: false));

    private void CreateFile(string spokenName)
    {
        Directory.CreateDirectory(workspaceDirectory);
        var path = SafeWorkspacePath(spokenName, addTextExtension: true);
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
    }

    private string SafeWorkspacePath(string spokenName, bool addTextExtension)
    {
        if (spokenName.Contains("..", StringComparison.Ordinal) ||
            spokenName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            throw new UnauthorizedAccessException("Không chấp nhận đường dẫn tùy ý.");

        var name = spokenName.Trim().Trim('"', '\'', '“', '”', '.', ' ');
        name = System.Text.RegularExpressions.Regex.Replace(name,
            @"\s+(?:chấm|cham)\s+txt$", ".txt", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        if (addTextExtension && string.IsNullOrEmpty(Path.GetExtension(name))) name += ".txt";
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Length > 100)
            throw new InvalidDataException("Tên file hoặc thư mục không hợp lệ.");
        var fullPath = Path.GetFullPath(Path.Combine(workspaceDirectory, name));
        var safeRoot = workspaceDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Đường dẫn nằm ngoài khu vực Airi.");
        return fullPath;
    }

    private static string ResolveKnownLocation(string location) => location switch
    {
        "downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        _ => throw new InvalidOperationException("Blocked unknown location.")
    };

    private static void PressKey(byte key, int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, 2, UIntPtr.Zero);
        }
    }

    private static void PressChord(byte modifier, byte key)
    {
        keybd_event(modifier, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 2, UIntPtr.Zero);
        keybd_event(modifier, 0, 2, UIntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}
