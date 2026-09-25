using NAudio.Wave;
using System.Threading.Channels;

namespace AnimeAssistant.AgentHost;

internal sealed class VoiceCapture : IDisposable
{
    public const int SampleRate = 16000;
    private readonly AgentSettings settings;
    private readonly Func<bool> isSuspended;
    private readonly WaveInEvent input;
    private readonly Channel<float[]> utterances = Channel.CreateBounded<float[]>(
        new BoundedChannelOptions(2) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly List<float> current = [];
    private readonly Queue<float[]> preRoll = new();
    private int preRollSampleCount;
    private bool speaking;
    private double speechMilliseconds;
    private double silenceMilliseconds;
    private float noiseFloor = 0.003f;

    public VoiceCapture(AgentSettings settings, Func<bool> isSuspended)
    {
        this.settings = settings;
        this.isSuspended = isSuspended;
        input = new WaveInEvent
        {
            DeviceNumber = 0,
            WaveFormat = new WaveFormat(SampleRate, 16, 1),
            BufferMilliseconds = 40,
            NumberOfBuffers = 3
        };
        input.DataAvailable += OnDataAvailable;
    }

    public ChannelReader<float[]> Utterances => utterances.Reader;
    public void Start() => input.StartRecording();

    private void OnDataAvailable(object? sender, WaveInEventArgs eventArgs)
    {
        if (isSuspended())
        {
            Reset();
            return;
        }

        var sampleCount = eventArgs.BytesRecorded / 2;
        var samples = new float[sampleCount];
        double energy = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            var sample = BitConverter.ToInt16(eventArgs.Buffer, index * 2) / 32768f;
            samples[index] = sample;
            energy += sample * sample;
        }
        var rms = (float)Math.Sqrt(energy / Math.Max(1, sampleCount));
        var duration = sampleCount * 1000d / SampleRate;
        var threshold = Math.Max(settings.VoiceThreshold, noiseFloor * 3.2f);
        var voice = rms >= threshold;

        if (!speaking)
        {
            noiseFloor = noiseFloor * 0.96f + Math.Min(rms, 0.03f) * 0.04f;
            if (!voice)
            {
                AddPreRoll(samples);
                return;
            }
            speaking = true;
            foreach (var buffered in preRoll) current.AddRange(buffered);
            preRoll.Clear();
            preRollSampleCount = 0;
        }

        current.AddRange(samples);
        speechMilliseconds += duration;
        silenceMilliseconds = voice ? 0 : silenceMilliseconds + duration;

        if (silenceMilliseconds >= settings.EndSilenceMilliseconds ||
            speechMilliseconds >= settings.MaximumSpeechSeconds * 1000d)
        {
            if (speechMilliseconds - silenceMilliseconds >= settings.MinimumSpeechMilliseconds)
                utterances.Writer.TryWrite(current.ToArray());
            Reset();
        }
    }

    private void Reset()
    {
        current.Clear();
        speaking = false;
        speechMilliseconds = 0;
        silenceMilliseconds = 0;
    }

    private void AddPreRoll(float[] samples)
    {
        preRoll.Enqueue(samples);
        preRollSampleCount += samples.Length;
        while (preRollSampleCount > SampleRate / 4 && preRoll.TryDequeue(out var old))
            preRollSampleCount -= old.Length;
    }

    public void Dispose()
    {
        input.StopRecording();
        input.DataAvailable -= OnDataAvailable;
        input.Dispose();
        utterances.Writer.TryComplete();
    }
}
