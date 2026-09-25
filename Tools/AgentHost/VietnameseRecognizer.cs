using SherpaOnnx;

namespace AnimeAssistant.AgentHost;

internal sealed class VietnameseRecognizer : IDisposable
{
    private const int SampleRate = 16000;
    private readonly OfflineRecognizer recognizer;

    public VietnameseRecognizer(string modelDirectory, int threads)
    {
        string FilePath(string name)
        {
            var path = Path.Combine(modelDirectory, name);
            if (!File.Exists(path)) throw new FileNotFoundException($"Missing ASR model file: {path}");
            return path;
        }

        var config = new OfflineRecognizerConfig();
        config.FeatConfig.SampleRate = SampleRate;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Tokens = FilePath("tokens.txt");
        config.ModelConfig.Transducer.Encoder = FilePath("encoder-epoch-12-avg-8.int8.onnx");
        config.ModelConfig.Transducer.Decoder = FilePath("decoder-epoch-12-avg-8.onnx");
        config.ModelConfig.Transducer.Joiner = FilePath("joiner-epoch-12-avg-8.int8.onnx");
        config.ModelConfig.NumThreads = Math.Clamp(threads, 1, 4);
        config.ModelConfig.Debug = 0;
        config.DecodingMethod = "greedy_search";
        config.MaxActivePaths = 4;
        recognizer = new OfflineRecognizer(config);
    }

    public string Recognize(float[] samples, int sampleRate = SampleRate)
    {
        using var stream = recognizer.CreateStream();
        stream.AcceptWaveform(sampleRate, samples);
        recognizer.Decode(stream);
        return stream.Result.Text.Trim();
    }

    public void Dispose() => recognizer.Dispose();
}
