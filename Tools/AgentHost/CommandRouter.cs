using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AnimeAssistant.AgentHost;

internal sealed class CommandRouter
{
    private readonly AgentSettings settings;
    private readonly CommandCatalog catalog;

    public CommandRouter(AgentSettings settings, CommandCatalog catalog)
    {
        this.settings = settings;
        this.catalog = catalog;
    }

    public AgentCommand? Route(string transcript, bool ignoreWakeWord = false)
    {
        var originalText = transcript.Trim();
        var text = Normalize(transcript);
        var wake = settings.WakeWords.Select(Normalize).FirstOrDefault(word => ContainsPhrase(text, word));
        if (settings.RequireWakeWord && !ignoreWakeWord && wake is null)
            return null;
        if (wake is not null)
            text = Regex.Replace(text, $@"\b{Regex.Escape(wake)}\b", " ").Trim();
        if (text.Length == 0)
            return new("reply", "Em đang nghe đây ạ.");

        var createFolder = Regex.Match(originalText,
            @"(?:tạo|tao|lập|lap)\s+(?:một\s+|mot\s+)?(?:thư\s*mục|thu\s*muc|folder)\s+(?:tên\s+|ten\s+)?(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (createFolder.Success)
        {
            var name = createFolder.Groups[1].Value.Trim();
            return new("create_folder", $"Em tạo thư mục “{name}” trong khu vực Airi nhé.", name);
        }

        var createFile = Regex.Match(originalText,
            @"(?:tạo|tao)\s+(?:một\s+|mot\s+)?(?:file|tệp|tep)\s+(?:tên\s+|ten\s+)?(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (createFile.Success)
        {
            var name = createFile.Groups[1].Value.Trim();
            return new("create_file", $"Em tạo file “{name}” trong khu vực Airi nhé.", name);
        }

        if (ContainsAny(text, "mo thu muc airi", "mo khu vuc airi", "mo workspace"))
            return new("open_workspace", "Em mở khu vực làm việc Airi nhé.");
        if (ContainsAny(text, "mo thu muc tai xuong", "mo downloads", "mo download"))
            return new("open_location", "Em mở thư mục Tải xuống nhé.", "downloads");
        if (ContainsAny(text, "mo thu muc tai lieu", "mo documents"))
            return new("open_location", "Em mở thư mục Tài liệu nhé.", "documents");
        if (ContainsAny(text, "mo thu muc desktop", "mo thu muc man hinh"))
            return new("open_location", "Em mở thư mục Desktop nhé.", "desktop");

        if (ContainsAny(text, "mo project", "mo du an", "thu muc project"))
            return new("open_project", "Em mở thư mục dự án nhé.");

        if (ContainsAny(text, "mo", "bat", "khoi dong"))
        {
            foreach (var app in catalog.Applications)
                if (app.Aliases.Select(Normalize).Any(alias => ContainsPhrase(text, alias)))
                    return new("open_app", app.Reply, app.Target);
        }

        var originalSearch = Regex.Match(originalText,
            @"(?:tìm\s+kiếm|tim\s+kiem|tra\s+cứu|tra\s+cuu|google)\s+(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (originalSearch.Success)
        {
            var query = originalSearch.Groups[1].Value.Trim();
            return new("web_search", $"Em tìm “{query}” trên web nhé.", query);
        }
        if (TryExtractAfter(text, ["tim kiem", "tra cuu", "google"], out var normalizedQuery))
            return normalizedQuery.Length == 0
                ? new("reply", "Anh muốn em tìm nội dung gì ạ?")
                : new("web_search", $"Em tìm “{normalizedQuery}” trên web nhé.", normalizedQuery);

        ExampleEntry? best = null;
        var bestScore = 0d;
        foreach (var example in catalog.Examples)
        foreach (var phrase in example.Phrases)
        {
            var normalizedPhrase = Normalize(phrase);
            var score = ContainsPhrase(text, normalizedPhrase) ? 1d : TokenSimilarity(text, normalizedPhrase);
            if (score > bestScore)
            {
                bestScore = score;
                best = example;
            }
        }

        if (best is null || bestScore < 0.55d)
            return new("reply", "Em chưa hiểu lệnh đó. Anh có thể nói mở ứng dụng, tìm kiếm, chỉnh âm lượng hoặc điều khiển nhạc.");

        if (best.Intent == "tell_time")
            return new("reply", $"Bây giờ là {DateTime.Now:HH 'giờ' mm}.");
        return new(best.Intent, best.Reply);
    }

    internal static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character == 'đ' ? 'd' : character);
        }
        return Regex.Replace(builder.ToString(), @"[^a-z0-9]+", " ").Trim();
    }

    private static bool TryExtractAfter(string text, string[] markers, out string result)
    {
        foreach (var marker in markers)
        {
            var index = text.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) continue;
            result = text[(index + marker.Length)..].Trim();
            return true;
        }
        result = "";
        return false;
    }

    private static bool ContainsAny(string text, params string[] phrases) =>
        phrases.Any(phrase => ContainsPhrase(text, phrase));

    private static bool ContainsPhrase(string text, string phrase) =>
        $" {text} ".Contains($" {phrase} ", StringComparison.Ordinal);

    private static double TokenSimilarity(string left, string right)
    {
        var a = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var b = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (a.Count == 0 || b.Count == 0) return 0;
        var intersection = a.Count(b.Contains);
        return intersection / (double)(a.Count + b.Count - intersection);
    }
}
