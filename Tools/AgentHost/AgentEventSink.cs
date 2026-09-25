using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace AnimeAssistant.AgentHost;

internal sealed class AgentEventSink : IAsyncDisposable
{
    private readonly string? pipeName;
    private readonly Channel<string> messages = Channel.CreateUnbounded<string>();
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task serverTask;

    public AgentEventSink(string? pipeName)
    {
        this.pipeName = string.IsNullOrWhiteSpace(pipeName) ? null : pipeName;
        serverTask = this.pipeName is null ? Task.CompletedTask : Task.Run(RunServerAsync);
    }

    public void Emit(string type, string text, string? detail = null)
    {
        var json = JsonSerializer.Serialize(new AgentEvent(type, text, detail), Json.Options);
        Console.WriteLine(json);
        if (pipeName is not null) messages.Writer.TryWrite(json);
    }

    private async Task RunServerAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(pipeName!, PipeDirection.Out, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(cancellation.Token);
                await using var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
                while (pipe.IsConnected && await messages.Reader.WaitToReadAsync(cancellation.Token))
                    while (messages.Reader.TryRead(out var message))
                        await writer.WriteLineAsync(message);
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        messages.Writer.TryComplete();
        try { await serverTask; } catch (OperationCanceledException) { }
        cancellation.Dispose();
    }
}
