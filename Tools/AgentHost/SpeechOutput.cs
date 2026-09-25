using System.Globalization;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using Microsoft.Win32;
using NAudio.Wave;
using SherpaOnnx;

namespace AnimeAssistant.AgentHost;

internal sealed class SpeechOutput : IDisposable
{
    private SpeechSynthesizer? synthesizer;
    private string? oneCoreVietnameseToken;
    private OfflineTts? localTts;
    private readonly float localVoiceSpeed;
    private int speaking;

    public SpeechOutput(bool enabled, string modelDirectory, int threads, float speed)
    {
        if (!enabled) return;
        localVoiceSpeed = Math.Clamp(speed, 0.75f, 1.35f);
        try
        {
            var config = new OfflineTtsConfig();
            config.Model.Vits.Model = Path.Combine(modelDirectory, "vi_VN-vais1000-medium.onnx");
            config.Model.Vits.Tokens = Path.Combine(modelDirectory, "tokens.txt");
            config.Model.Vits.DataDir = Path.Combine(modelDirectory, "espeak-ng-data");
            config.Model.Vits.NoiseScale = 0.667f;
            config.Model.Vits.NoiseScaleW = 0.8f;
            config.Model.Vits.LengthScale = 1f;
            config.Model.NumThreads = Math.Clamp(threads, 1, 4);
            config.Model.Provider = "cpu";
            config.MaxNumSentences = 1;
            localTts = new OfflineTts(config);
            if (localTts.SampleRate <= 0)
                throw new InvalidOperationException("The local female TTS model could not be initialized.");
            return;
        }
        catch (Exception exception)
        {
            localTts?.Dispose();
            localTts = null;
            Console.Error.WriteLine($"Local female TTS is unavailable; using Windows voice: {exception.Message}");
        }

        try
        {
            oneCoreVietnameseToken = FindOneCoreVietnameseVoice();
            if (oneCoreVietnameseToken is not null) return;

            synthesizer = new SpeechSynthesizer();
            var vietnamese = synthesizer.GetInstalledVoices(new CultureInfo("vi-VN"))
                .FirstOrDefault(voice => voice.Enabled);
            if (vietnamese is not null) synthesizer.SelectVoice(vietnamese.VoiceInfo.Name);
            synthesizer.Rate = 1;
            synthesizer.Volume = 90;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Windows TTS is unavailable: {exception.Message}");
            synthesizer = null;
        }
    }

    public bool IsSpeaking => Volatile.Read(ref speaking) != 0;

    public async Task SpeakAsync(string text, CancellationToken cancellationToken)
    {
        if ((localTts is null && synthesizer is null && oneCoreVietnameseToken is null) ||
            string.IsNullOrWhiteSpace(text)) return;
        Interlocked.Exchange(ref speaking, 1);
        try
        {
            await Task.Run(() => SpeakCore(text), cancellationToken);
        }
        finally
        {
            Interlocked.Exchange(ref speaking, 0);
        }
    }

    public void Dispose()
    {
        localTts?.Dispose();
        synthesizer?.Dispose();
    }

    private void SpeakCore(string text)
    {
        if (localTts is not null)
        {
            var audio = localTts.Generate(text, localVoiceSpeed, 0);
            try
            {
                var samples = audio.Samples;
                var bytes = new byte[samples.Length * sizeof(float)];
                Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
                using var memory = new MemoryStream(bytes, writable: false);
                using var wave = new RawSourceWaveStream(memory,
                    WaveFormat.CreateIeeeFloatWaveFormat(audio.SampleRate, 1));
                using var output = new WaveOutEvent { DesiredLatency = 100 };
                output.Init(wave);
                output.Play();
                while (output.PlaybackState == PlaybackState.Playing)
                    Thread.Sleep(20);
            }
            finally
            {
                audio.Dispose();
            }
            return;
        }

        if (oneCoreVietnameseToken is null)
        {
            synthesizer?.Speak(text);
            return;
        }

        object? voice = null;
        object? token = null;
        try
        {
            var voiceType = Type.GetTypeFromProgID("SAPI.SpVoice", throwOnError: true)!;
            var tokenType = Type.GetTypeFromProgID("SAPI.SpObjectToken", throwOnError: true)!;
            voice = Activator.CreateInstance(voiceType)!;
            token = Activator.CreateInstance(tokenType)!;
            ((dynamic)token).SetId(oneCoreVietnameseToken);
            ((dynamic)voice).Voice = token;
            ((dynamic)voice).Rate = 1;
            ((dynamic)voice).Volume = 90;
            ((dynamic)voice).Speak(text, 0);
        }
        finally
        {
            if (voice is not null && Marshal.IsComObject(voice)) Marshal.FinalReleaseComObject(voice);
            if (token is not null && Marshal.IsComObject(token)) Marshal.FinalReleaseComObject(token);
        }
    }

    private static string? FindOneCoreVietnameseVoice()
    {
        const string baseKey = @"SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens";
        using var tokens = Registry.LocalMachine.OpenSubKey(baseKey);
        if (tokens is null) return null;
        foreach (var tokenName in tokens.GetSubKeyNames())
        {
            using var attributes = tokens.OpenSubKey(tokenName + @"\Attributes");
            var language = attributes?.GetValue("Language")?.ToString();
            if (string.Equals(language, "42A", StringComparison.OrdinalIgnoreCase))
                return @"HKEY_LOCAL_MACHINE\" + baseKey + "\\" + tokenName;
        }
        return null;
    }
}
