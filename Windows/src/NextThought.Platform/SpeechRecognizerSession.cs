using System.Diagnostics;
using System.Threading.Channels;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NextThought.Core;
using SherpaOnnx;
using SpeechSegment = NextThought.Core.SpeechSegment;

namespace NextThought.Platform;

/// <summary>All native calls run on a single consumer. Each source has independent stream and VAD state.</summary>
public sealed class SpeechRecognizerSession : IDisposable
{
    private readonly Utf8OnlineRecognizer online;
    private readonly Utf8OfflineRecognizer? offline;
    private readonly Dictionary<AudioSource, State> states = [];
    private bool disposed;
    public event Action<TranscriptUpdate>? Updated;
    public event Action<AudioFrame>? FrameObserved;
    public event Action<string>? Warning;
    public double InferenceSeconds { get; private set; }
    public double AudioSeconds { get; private set; }
    private sealed class State(OnlineStream stream)
    {
        public OnlineStream Stream = stream;
        public readonly VoiceActivitySegmenter Vad = new();
        public Guid Utterance = Guid.NewGuid();
        public DateTimeOffset Origin;
        public long StartSample;
        public string LastText = "";
        public long LastPartialSample;
    }
    public SpeechRecognizerSession(ModelStore store, AppSettings settings, IReadOnlyList<string>? extraHotwords = null)
    {
        if (!store.IsInstalled(settings.LocalModel)) throw new InvalidOperationException("请先准备并校验语音模型。");
        if (settings.SecondPass && !store.IsInstalled("xasrOffline")) throw new InvalidOperationException("整句精修模型尚未准备，可先关闭整句精修。");
        if (Native.ActiveCodePage() != 65001 && store.DirectoryFor(settings.LocalModel).Any(c => c > 127))
            throw new InvalidOperationException("中文模型路径需要应用的 UTF-8 清单。请通过 NextThought.exe 或 NextThought.Cli.exe 启动，避免直接用 dotnet 执行 DLL。");
        var hotwords = Path.Combine(Path.GetTempPath(), "nextthought-hotwords-" + Guid.NewGuid().ToString("N") + ".txt");
        var words = settings.Vocabulary.Split([',', '，', '、', '\r', '\n', ';', '；'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Concat(extraHotwords ?? [])
            .Distinct()
            .ToArray();
        try
        {
            if (words.Length > 0) File.WriteAllText(hotwords, string.Join('\n', words));
            var folder = store.DirectoryFor(settings.LocalModel);
            var config = new OnlineRecognizerConfig();
            config.FeatConfig.SampleRate = 16000; config.FeatConfig.FeatureDim = 80;
            config.ModelConfig.Transducer.Encoder = Path.Combine(folder, "encoder.int8.onnx");
            config.ModelConfig.Transducer.Decoder = Path.Combine(folder, "decoder.onnx");
            config.ModelConfig.Transducer.Joiner = Path.Combine(folder, "joiner.int8.onnx");
            config.ModelConfig.Tokens = Path.Combine(folder, "tokens.txt"); config.ModelConfig.Provider = "cpu";
            config.ModelConfig.NumThreads = 2; config.ModelConfig.ModelType = ""; config.ModelConfig.ModelingUnit = "bpe";
            config.ModelConfig.BpeVocab = Path.Combine(folder, "bpe.vocab"); config.EnableEndpoint = 0;
            config.DecodingMethod = "modified_beam_search"; config.MaxActivePaths = 4; config.HotwordsScore = 1.5f;
            if (words.Length > 0) config.HotwordsFile = hotwords;
            online = new(config);
            try
            {
                if (settings.SecondPass)
                {
                    var target = store.DirectoryFor("xasrOffline"); var second = new OfflineRecognizerConfig();
                    second.FeatConfig.SampleRate = 16000; second.FeatConfig.FeatureDim = 80;
                    second.ModelConfig.Transducer.Encoder = Path.Combine(target, "encoder-epoch-99-avg-1.int8.onnx");
                    second.ModelConfig.Transducer.Decoder = Path.Combine(target, "decoder-epoch-99-avg-1.onnx");
                    second.ModelConfig.Transducer.Joiner = Path.Combine(target, "joiner-epoch-99-avg-1.int8.onnx");
                    second.ModelConfig.Tokens = Path.Combine(target, "tokens.txt"); second.ModelConfig.Provider = "cpu";
                    second.ModelConfig.NumThreads = 2; second.ModelConfig.ModelType = "transducer"; second.ModelConfig.ModelingUnit = "bpe";
                    second.ModelConfig.BpeVocab = Path.Combine(target, "bpe.vocab"); second.DecodingMethod = "modified_beam_search";
                    second.MaxActivePaths = 4; second.HotwordsScore = 1.5f;
                    if (words.Length > 0) second.HotwordsFile = hotwords;
                    offline = new(second);
                }
            }
            catch { online.Dispose(); throw; }
        }
        finally { if (File.Exists(hotwords)) File.Delete(hotwords); }
    }
    public void Accept(AudioFrame frame)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        FrameObserved?.Invoke(frame);
        if (!states.TryGetValue(frame.Source, out var state)) { state = new(online.CreateStream()) { Origin = frame.Origin, StartSample = frame.StartSample }; states[frame.Source] = state; }
        var clock = Stopwatch.StartNew();
        var segment = state.Vad.Append(frame.Samples);
        state.Stream.AcceptWaveform(16000, frame.Samples.Select(x => x / 32768f).ToArray()); Decode(state);
        var text = online.GetText(state.Stream).Trim();
        if (text.Length > 0 && text != state.LastText && frame.StartSample-state.LastPartialSample >= 3200)
        {
            state.LastText = text; state.LastPartialSample = frame.StartSample;
            Updated?.Invoke(new(state.Utterance, frame.Source, text, false, state.Origin.AddSeconds(state.StartSample/16000.0), state.Origin.AddSeconds((frame.StartSample+frame.Samples.Length)/16000.0)));
        }
        if (segment != null) Commit(frame.Source, state, segment, frame.StartSample + frame.Samples.Length);
        InferenceSeconds += clock.Elapsed.TotalSeconds; AudioSeconds += frame.Samples.Length/16000.0;
    }
    private void Decode(State state) { while (online.IsReady(state.Stream)) online.Decode(state.Stream); }
    private void Commit(AudioSource source, State state, SpeechSegment segment, long nextStart)
    {
        state.Stream.AcceptWaveform(16000, new float[4800]); state.Stream.InputFinished(); Decode(state);
        var text = online.GetText(state.Stream).Trim();
        if (offline != null)
        {
            try
            {
                using var stream = offline.CreateStream(); stream.AcceptWaveform(16000, segment.Samples.Select(x => x/32768f).ToArray());
                offline.Decode(stream); var refined = offline.GetText(stream).Trim(); if (refined.Length > 0) text = refined;
            }
            catch (Exception ex) { Warning?.Invoke("精修失败，保留流式结果：" + ex.Message); }
        }
        if (text.Length > 0) Updated?.Invoke(new(state.Utterance, source, text, true, state.Origin.AddSeconds(segment.StartSample/16000.0), state.Origin.AddSeconds(segment.EndSample/16000.0)));
        var replacement = online.CreateStream(); state.Stream.Dispose(); state.Stream = replacement;
        state.Utterance = Guid.NewGuid(); state.LastText = ""; state.StartSample = nextStart;
    }
    public void Complete()
    {
        var clock = Stopwatch.StartNew();
        foreach (var (source, state) in states)
            if (state.Vad.Flush() is { } segment) Commit(source, state, segment, segment.EndSample);
        InferenceSeconds += clock.Elapsed.TotalSeconds;
    }
    public void Dispose()
    { if (disposed) return; disposed = true; foreach (var state in states.Values) state.Stream.Dispose(); states.Clear(); offline?.Dispose(); online.Dispose(); }
    public void TranscribeWave(string file, AudioSource source, CancellationToken ct = default)
    {
        using var reader = new WaveFileReader(file);
        ISampleProvider mono = new MonoSampleProvider(reader.ToSampleProvider());
        var resampler = mono.WaveFormat.SampleRate == 16000 ? mono : new WdlResamplingSampleProvider(mono, 16000);
        var buffer = new float[1600]; long start = 0; var origin = DateTimeOffset.UtcNow;
        while (resampler.Read(buffer, 0, buffer.Length) is var count && count > 0)
        {
            ct.ThrowIfCancellationRequested(); var pcm = new short[count];
            for (var i = 0; i < count; i++) pcm[i] = (short)Math.Clamp((int)Math.Round(buffer[i]*32768), short.MinValue, short.MaxValue);
            Accept(new(source, pcm, start, origin)); start += count;
        }
        Complete();
    }
}

public sealed class LiveSpeechEngine : IAsyncDisposable
{
    private readonly Channel<AudioFrame> channel = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(100) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly SpeechRecognizerSession session;
    private readonly Task processing;
    public int QueuedFrames => channel.Reader.Count;
    public event Action<TranscriptUpdate>? Updated;
    public event Action<string>? Failed;
    public event Action<string>? Warning;
    public LiveSpeechEngine(ModelStore store, AppSettings settings)
    {
        session = new(store, settings);
        session.Updated += update => Updated?.Invoke(update); session.Warning += message => Warning?.Invoke(message);
        processing = Task.Run(async () =>
        {
            try { await foreach (var frame in channel.Reader.ReadAllAsync()) session.Accept(frame); session.Complete(); }
            catch (Exception ex) { channel.Writer.TryComplete(ex); Failed?.Invoke("识别停止：" + ex.Message); }
            finally { session.Dispose(); }
        });
    }
    public void Accept(AudioFrame frame) { if (!channel.Writer.TryWrite(frame)) Failed?.Invoke("识别队列已满或已停止，请结束会议后重试。"); }
    public async ValueTask DisposeAsync() { channel.Writer.TryComplete(); await processing; }
}
