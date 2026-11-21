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
        /// <summary>J5: the Mac transcriber's pending watch / coverage / delivery rules.</summary>
        public readonly StreamingUtteranceTracker Tracker = new();
        /// <summary>Absolute sample index where accepted audio currently ends.</summary>
        public long LastAcceptedEnd;
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
        state.LastAcceptedEnd = frame.StartSample + frame.Samples.Length;
        var segments = state.Vad.Append(frame.Samples);
        state.Stream.AcceptWaveform(16000, frame.Samples.Select(x => x / 32768f).ToArray()); Decode(state);
        var text = online.GetText(state.Stream).Trim();
        var now = DateTimeOffset.UtcNow;
        var partialEnd = state.Origin.AddSeconds(state.LastAcceptedEnd / 16000.0);
        if (text.Length > 0) state.Tracker.Heard(text, now);
        if (text.Length > 0 && text != state.LastText && frame.StartSample-state.LastPartialSample >= 3200
            && state.Tracker.ShouldDeliver(false, partialEnd))
        {
            state.LastText = text; state.LastPartialSample = frame.StartSample;
            Updated?.Invoke(new(state.Utterance, frame.Source, text, false, state.Origin.AddSeconds(state.StartSample/16000.0), partialEnd));
        }
        foreach (var segment in segments)
            if (segment.Kind == SpeechSegmentEvent.KindValue.Final)
                Commit(frame.Source, state, new SpeechSegment(segment.Id, segment.StartSample, segment.EndSample, segment.Samples), frame.StartSample + frame.Samples.Length);
        // J5: words the segmenter never closes are closed by the 2.5 s / 20 s watch.
        CheckPending(now);
        InferenceSeconds += clock.Elapsed.TotalSeconds; AudioSeconds += frame.Samples.Length/16000.0;
    }
    private void Decode(State state) { while (online.IsReady(state.Stream)) online.Decode(state.Stream); }
    /// <summary>
    /// J5: close utterances nobody else closed. The offline re-decode only ever replaces the range
    /// its segment covers, so the words the stream heard are kept; the committed range moves with
    /// the closed utterance so the segmenter's overlap is not decoded twice.
    /// </summary>
    public void CheckPending(DateTimeOffset now)
    {
        if (disposed) return;
        foreach (var (source, state) in states)
        {
            if (!state.Tracker.IsOverdue(now)) continue;
            FinalizePending(source, state, now);
        }
    }
    private void FinalizePending(AudioSource source, State state, DateTimeOffset now)
    {
        var pendingText = state.Tracker.Pending.Text;
        // Mac commitUtterance(nil): finish the stream and deliver what it holds.
        state.Stream.AcceptWaveform(16000, new float[4800]); state.Stream.InputFinished(); Decode(state);
        var streaming = online.GetText(state.Stream).Trim();
        var text = streaming.Length > 0 ? streaming : pendingText;
        var started = state.Origin.AddSeconds(state.StartSample / 16000.0);
        var ended = state.Origin.AddSeconds(state.LastAcceptedEnd / 16000.0);
        if (ended <= started) ended = started.AddSeconds(0.01);
        if (text.Length > 0 && state.Tracker.ShouldDeliver(true, ended))
        {
            Updated?.Invoke(new(state.Utterance, source, text, true, started, ended));
            state.Tracker.Delivered(true, ended);
        }
        state.Tracker.MarkClosedWithoutSegment(now);
        state.Tracker.MarkCommittedThrough(state.LastAcceptedEnd);
        state.Tracker.ClosedPending();
        var replacement = online.CreateStream(); state.Stream.Dispose(); state.Stream = replacement;
        state.Utterance = Guid.NewGuid(); state.LastText = ""; state.StartSample = state.LastAcceptedEnd;
    }
    private void Commit(AudioSource source, State state, SpeechSegment segment, long nextStart)
    {
        state.Stream.AcceptWaveform(16000, new float[4800]); state.Stream.InputFinished(); Decode(state);
        var streaming = online.GetText(state.Stream).Trim();
        // The segmenter re-sends half a second when it splits a long utterance: only the uncovered
        // rest is re-decoded, so no words are dropped or written twice.
        var overlap = (int)Math.Min(
            state.Tracker.OverlapToDrop(segment.StartSample, state.Origin,
                state.Origin.AddSeconds(segment.StartSample / 16000.0)),
            segment.Samples.Length);
        var samples = overlap > 0 ? segment.Samples.AsSpan(overlap).ToArray() : segment.Samples;
        var segmentSeconds = samples.Length / 16000.0;
        var streamSeconds = Math.Max(0, (state.LastAcceptedEnd - state.StartSample) / 16000.0);
        var text = streaming;
        if (offline != null && samples.Length > 0)
        {
            try
            {
                using var stream = offline.CreateStream(); stream.AcceptWaveform(16000, samples.Select(x => x/32768f).ToArray());
                offline.Decode(stream); var refined = offline.GetText(stream).Trim();
                if (refined.Length > 0)
                {
                    // J5: the offline text covers the segment only; it may not replace minutes of
                    // quiet speech the stream heard (Mac UtteranceText.final).
                    text = StreamingUtteranceTracker.FinalText(streaming, streamSeconds, refined, segmentSeconds);
                }
            }
            catch (Exception ex) { Warning?.Invoke("精修失败，保留流式结果：" + ex.Message); }
        }
        var started = state.Origin.AddSeconds(segment.StartSample/16000.0);
        var ended = state.Origin.AddSeconds(segment.EndSample/16000.0);
        if (ended <= started) ended = started.AddSeconds(0.01);
        if (text.Length > 0 && state.Tracker.ShouldDeliver(true, ended))
        {
            Updated?.Invoke(new(state.Utterance, source, text, true, started, ended));
            state.Tracker.Delivered(true, ended);
        }
        state.Tracker.MarkCommittedThrough(segment.EndSample);
        state.Tracker.ClosedPending();
        var replacement = online.CreateStream(); state.Stream.Dispose(); state.Stream = replacement;
        state.Utterance = Guid.NewGuid(); state.LastText = ""; state.StartSample = nextStart;
    }
    public void Complete()
    {
        var clock = Stopwatch.StartNew();
        foreach (var (source, state) in states)
        {
            if (state.Vad.Flush() is { } segment)
                Commit(source, state, new SpeechSegment(segment.Id, segment.StartSample, segment.EndSample, segment.Samples), segment.EndSample);
            // Mac finish(): whatever is still pending is a line too (text never lost).
            if (state.Tracker.Pending.Text.Length > 0) FinalizePending(source, state, DateTimeOffset.UtcNow);
        }
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
    public LiveSpeechEngine(ModelStore store, AppSettings settings, IReadOnlyList<string>? extraHotwords = null)
    {
        session = new(store, settings, extraHotwords);
        session.Updated += update => Updated?.Invoke(update); session.Warning += message => Warning?.Invoke(message);
        processing = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    AudioFrame? frame = null;
                    try
                    {
                        using var wait = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                        frame = await channel.Reader.ReadAsync(wait.Token);
                    }
                    catch (OperationCanceledException) { /* quiet: still watch the words left pending */ }
                    catch (ChannelClosedException) { break; }
                    if (frame is { } next) session.Accept(next);
                    // J5: the pending watch must also run when no audio arrives for a while.
                    session.CheckPending(DateTimeOffset.UtcNow);
                }
                session.Complete();
            }
            catch (Exception ex) { channel.Writer.TryComplete(ex); Failed?.Invoke("识别停止：" + ex.Message); }
            finally { session.Dispose(); }
        });
    }
    public void Accept(AudioFrame frame) { if (!channel.Writer.TryWrite(frame)) Failed?.Invoke("识别队列已满或已停止，请结束会议后重试。"); }
    public async ValueTask DisposeAsync() { channel.Writer.TryComplete(); await processing; }
}
