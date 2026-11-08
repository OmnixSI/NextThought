using NextThought.Core;

namespace NextThought.Platform;

/// <summary>One meeting's temporary PCM window and speaker groups; never writes audio or embeddings.</summary>
public sealed class MeetingVoiceTagger(AppPaths paths, AudioSource source) : IDisposable
{
    private readonly SpeakerEmbedder model = new(paths);
    private readonly Queue<AudioFrame> frames = new();
    private readonly object sync = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly VoiceGroups groups = new();
    private int queued;
    public void Add(AudioFrame frame)
    {
        if (frame.Source != source) return;
        lock (sync)
        {
            frames.Enqueue(frame with { Samples = (short[])frame.Samples.Clone() });
            while (frames.Count > 0 && frame.StartSample - frames.Peek().StartSample > 60 * 16000) frames.Dequeue();
        }
    }
    public async Task<int?> LabelAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        if (Interlocked.Increment(ref queued) > 16) { Interlocked.Decrement(ref queued); return null; }
        try
        {
            short[] pcm;
            lock (sync)
            {
                if (frames.Count == 0) return null;
                var origin = frames.Peek().Origin; long first = (long)Math.Round((start - origin).TotalSeconds * 16000), last = (long)Math.Round((end - origin).TotalSeconds * 16000);
                if (first < frames.Peek().StartSample || last <= first || last - first > 20 * 16000) return null;
                pcm = new short[last - first]; long copied = 0;
                foreach (var frame in frames)
                {
                    var from = Math.Max(first, frame.StartSample); var to = Math.Min(last, frame.StartSample + frame.Samples.Length);
                    if (to <= from) continue; frame.Samples.AsSpan((int)(from - frame.StartSample), (int)(to - from)).CopyTo(pcm.AsSpan((int)(from - first))); copied += to - from;
                }
                if (copied != pcm.Length) return null;
            }
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try { return await Task.Run(() => model.Embed(pcm, ct) is { } vector ? groups.Assign(vector, pcm.Length / 16000.0) : null, ct).ConfigureAwait(false); }
            finally { gate.Release(); }
        }
        finally { Interlocked.Decrement(ref queued); }
    }
    public void Dispose() { gate.Wait(); try { lock (sync) frames.Clear(); model.Dispose(); } finally { gate.Release(); } }
}
