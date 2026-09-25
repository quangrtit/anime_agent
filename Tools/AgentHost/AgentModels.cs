using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnimeAssistant.AgentHost;

internal sealed class AgentSettings
{
    public bool Enabled { get; set; } = true;
    public bool RequireWakeWord { get; set; } = true;
    public string[] WakeWords { get; set; } =
        ["airi", "ari", "agri", "ri", "ai ri", "a ri", "a i ri", "trợ lý", "anime"];
    public int AsrThreads { get; set; } = 1;
    public int MinimumSpeechMilliseconds { get; set; } = 320;
    public int EndSilenceMilliseconds { get; set; } = 700;
    public int MaximumSpeechSeconds { get; set; } = 12;
    public float VoiceThreshold { get; set; } = 0.012f;
    public bool TtsEnabled { get; set; } = true;
    public int TtsThreads { get; set; } = 1;
    public float TtsSpeed { get; set; } = 1.08f;
    public string TtsModelDirectory { get; set; } = "Models/vits-piper-vi_VN-vais1000-medium";
    public string WorkspaceDirectory { get; set; } = "%USERPROFILE%/Desktop/Airi";
    public string ModelDirectory { get; set; } = "Models/sherpa-onnx-zipformer-vi-int8-2025-04-20";
    public string CommandsFile { get; set; } = "commands.vi.json";

    public static AgentSettings Load(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "agent_settings.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<AgentSettings>(File.ReadAllText(path), Json.Options) ?? new()
            : new();
    }
}

internal sealed class CommandCatalog
{
    public List<ApplicationEntry> Applications { get; set; } = [];
    public List<ExampleEntry> Examples { get; set; } = [];

    public static CommandCatalog Load(string path) =>
        JsonSerializer.Deserialize<CommandCatalog>(File.ReadAllText(path), Json.Options)
        ?? throw new InvalidDataException($"Cannot parse command catalog: {path}");
}

internal sealed class ApplicationEntry
{
    public string Id { get; set; } = "";
    public string[] Aliases { get; set; } = [];
    public string Target { get; set; } = "";
    public string Reply { get; set; } = "";
}

internal sealed class ExampleEntry
{
    public string Intent { get; set; } = "";
    public string[] Phrases { get; set; } = [];
    public string Reply { get; set; } = "";
}

internal sealed record AgentCommand(string Intent, string Reply, string? Argument = null);
internal sealed record AgentEvent(string Type, string Text, string? Detail = null);

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
