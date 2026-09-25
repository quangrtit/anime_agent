using NAudio.Wave;
using System.Diagnostics;
using System.Text;

namespace AnimeAssistant.AgentHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        var baseDirectory = AppContext.BaseDirectory;
        try
        {
            var settings = AgentSettings.Load(baseDirectory);
            var catalogPath = Resolve(baseDirectory, settings.CommandsFile);
            var catalog = CommandCatalog.Load(catalogPath);
            var router = new CommandRouter(settings, catalog);

            if (HasFlag(args, "--self-test"))
                return RunSelfTest(router);

            var dryRun = HasFlag(args, "--dry-run");
            var projectDirectory = ValueOf(args, "--project-directory") ?? Directory.GetCurrentDirectory();
            var workspaceDirectory = ValueOf(args, "--workspace-directory") ?? settings.WorkspaceDirectory;
            var executor = new CommandExecutor(projectDirectory, workspaceDirectory, dryRun);
            var text = ValueOf(args, "--text");
            if (text is not null)
            {
                var command = router.Route(text, ignoreWakeWord: true);
                if (command is null) return 2;
                command = AddObedientConfirmation(command);
                Console.WriteLine($"{command.Intent}|{command.Argument}|{command.Reply}");
                executor.Execute(command);
                return 0;
            }

            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };
            WatchParent(ValueOf(args, "--parent-pid"), cancellation);

            var pipeName = ValueOf(args, "--pipe");
            await using var events = new AgentEventSink(pipeName);
            events.Emit("starting", "Đang khởi động trợ lý giọng nói…");

            var modelDirectory = Resolve(baseDirectory, settings.ModelDirectory);
            using var recognizer = new VietnameseRecognizer(modelDirectory, settings.AsrThreads);

            var transcribePath = ValueOf(args, "--transcribe");
            if (transcribePath is not null)
            {
                var (samples, sampleRate) = ReadAudio(transcribePath);
                Console.WriteLine(recognizer.Recognize(samples, sampleRate));
                return 0;
            }

            var ttsModelDirectory = Resolve(baseDirectory, settings.TtsModelDirectory);
            using var speech = new SpeechOutput(
                settings.TtsEnabled, ttsModelDirectory, settings.TtsThreads, settings.TtsSpeed);
            using var capture = new VoiceCapture(settings, () => speech.IsSpeaking);
            capture.Start();
            const string readyMessage = "Em đã sẵn sàng, chủ nhân. Hãy gọi Airi rồi ra lệnh cho em.";
            events.Emit("speaking", readyMessage, "ready");
            await speech.SpeakAsync(readyMessage, cancellation.Token);
            await Task.Delay(220, cancellation.Token);
            events.Emit("listening", "Sẵn sàng. Hãy gọi “Airi” rồi nói lệnh.");

            await foreach (var samples in capture.Utterances.ReadAllAsync(cancellation.Token))
            {
                string transcript;
                try { transcript = recognizer.Recognize(samples); }
                catch (Exception exception)
                {
                    events.Emit("error", "Em không nhận dạng được đoạn vừa rồi.", exception.Message);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(transcript)) continue;
                events.Emit("heard", transcript);

                var command = router.Route(transcript);
                if (command is null) continue;
                command = AddObedientConfirmation(command);
                events.Emit("speaking", command.Reply, command.Intent);
                var speechTask = speech.SpeakAsync(command.Reply, cancellation.Token);
                try
                {
                    events.Emit("executing", command.Reply, command.Intent);
                    executor.Execute(command);
                }
                catch (Exception exception)
                {
                    events.Emit("error", "Em chưa thực hiện được lệnh này.", exception.Message);
                }
                try { await speechTask; } catch (OperationCanceledException) { }
                await Task.Delay(220, cancellation.Token);
                events.Emit("listening", "Đang nghe…");
            }
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int RunSelfTest(CommandRouter router)
    {
        var cases = new Dictionary<string, string>
        {
            ["Airi mở máy tính"] = "open_app",
            ["trợ lý tăng âm lượng"] = "volume_up",
            ["anime tìm kiếm thời tiết Đà Nẵng"] = "web_search",
            ["Airi mấy giờ rồi"] = "reply",
            ["Airi chuyển bài"] = "media_next",
            ["ARI mở máy tính"] = "open_app",
            ["RI mở máy tính"] = "open_app",
            ["ĐÓ AGRI mở máy tính"] = "open_app",
            ["RI mở Google"] = "open_app",
            ["Airi tạo thư mục Báo cáo"] = "create_folder",
            ["Airi tạo file ghi chú chấm txt"] = "create_file",
            ["Airi chuyển cửa sổ"] = "window_switch"
        };
        foreach (var item in cases)
        {
            var actual = router.Route(item.Key)?.Intent;
            if (actual != item.Value)
            {
                Console.Error.WriteLine($"FAIL: '{item.Key}' => {actual}, expected {item.Value}");
                return 1;
            }
        }
        if (router.Route("mở máy tính") is not null)
        {
            Console.Error.WriteLine("FAIL: wake-word guard did not reject a command.");
            return 1;
        }
        Console.WriteLine($"PASS: {cases.Count + 1} command-router checks.");
        return 0;
    }

    private static AgentCommand AddObedientConfirmation(AgentCommand command) =>
        command.Intent == "reply"
            ? command
            : command with { Reply = "Tuân lệnh chủ nhân. " + command.Reply };

    private static (float[] Samples, int SampleRate) ReadAudio(string path)
    {
        using var reader = new AudioFileReader(path);
        var samples = new List<float>();
        var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (reader.WaveFormat.Channels == 1)
                samples.AddRange(buffer.AsSpan(0, read).ToArray());
            else
                for (var index = 0; index < read; index += reader.WaveFormat.Channels)
                    samples.Add(buffer[index]);
        }
        return (samples.ToArray(), reader.WaveFormat.SampleRate);
    }

    private static void WatchParent(string? rawPid, CancellationTokenSource cancellation)
    {
        if (!int.TryParse(rawPid, out var pid)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                using var parent = Process.GetProcessById(pid);
                await parent.WaitForExitAsync(cancellation.Token);
                cancellation.Cancel();
            }
            catch (ArgumentException) { cancellation.Cancel(); }
            catch (OperationCanceledException) { }
        });
    }

    private static string Resolve(string baseDirectory, string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));

    private static bool HasFlag(string[] args, string name) =>
        args.Any(value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));

    private static string? ValueOf(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        return null;
    }
}
