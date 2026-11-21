using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class SummaryAndAudioTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "nextthought-p1-" + Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        var path = Path.GetFullPath(root);
        if (Directory.Exists(path) && path.StartsWith(Path.GetFullPath(Path.GetTempPath()) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Directory.Delete(path, true);
    }
    [Fact]
    public void TimerJitterDoesNotCompressTimeAndLongPauseIsReported()
    {
        var clock = new AudioFrameClock();
        Assert.Equal(1, clock.Advance(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(2, clock.Advance(TimeSpan.FromMilliseconds(360)));
        Assert.Equal(1, clock.Advance(TimeSpan.FromMilliseconds(400)));
        Assert.Throws<IOException>(() => clock.Advance(TimeSpan.FromMilliseconds(1901)));
    }
    [Fact]
    public void QuietMicrophoneGainIsBoundedAndCannotOverflowPcm()
    {
        short[] data = [1000, -1000, 10000, -10000];
        PcmAudio.ApplyGain(data, 4); Assert.Equal(new short[] { 4000, -4000, short.MaxValue, short.MinValue }, data);
        Assert.Throws<ArgumentOutOfRangeException>(() => PcmAudio.ApplyGain(data, double.NaN));
    }
    [Fact]
    public void EchoUsesOverlappingIntervalsIncludingLongUtterances()
    {
        var now = DateTimeOffset.UtcNow; const string text = "先验证 Windows 录音，再生成会议纪要。";
        var line = new TranscriptLine { Date = now, StartedAt = now, EndedAt = now.AddSeconds(12), Text = text, Speaker = "remote" };
        var update = new TranscriptUpdate(Guid.NewGuid(), AudioSource.Microphone, text, true, now.AddMilliseconds(200), now.AddSeconds(12.2));
        Assert.True(EchoDetector.IsLikelyEcho(update, [line], false));
        Assert.False(EchoDetector.IsLikelyEcho(update with { Start = now.AddMinutes(1), End = now.AddSeconds(72) }, [line], false));
        Assert.False(EchoDetector.IsLikelyEcho(update, [line], true));
    }
    [Fact]
    public void LongSingleLineAndSurrogatePairsRespectChunkBoundsWithoutDataLoss()
    {
        var text = string.Concat(Enumerable.Repeat("长会议🙂", 200));
        var chunks = SummaryService.SplitText(text, 101);
        Assert.Equal(text, string.Concat(chunks));
        Assert.All(chunks, chunk => { Assert.True(chunk.Length <= 101); Assert.False(char.IsHighSurrogate(chunk[^1])); Assert.False(char.IsLowSurrogate(chunk[0])); });
    }
    [Fact]
    public async Task FailedSummaryResumesSavedChunksInsteadOfRepeatingThem()
    {
        var store = new SessionStore(new AppPaths(root)); var meta = new SessionMeta(); await store.CreateAsync(meta);
        for (var i = 0; i < 4; i++) await store.AppendTranscriptAsync(meta, new TranscriptLine { Date = meta.StartedAt.AddSeconds(i), Text = new string((char)('A' + i), 70) });
        var firstChunkCalls = 0; var calls = 0;
        var client = new FakeClient((user, _) =>
        {
            calls++; if (user.Contains("转写片段 1/")) firstChunkCalls++;
            if (calls == 2) throw new HttpRequestException("fixture network interruption");
            return Task.FromResult("## 总结\n片段内容，待确认。");
        });
        var service = new SummaryService(store, client); var options = new AnswerOptions("http://localhost/v1", "fixture", "");
        await Assert.ThrowsAsync<HttpRequestException>(() => service.GenerateAsync(meta.Id, options, chunkLimit: 100));
        Assert.Equal("failed", store.Load(meta.Id).Meta.SummaryState);
        var progress = new List<SummaryProgress>(); await service.GenerateAsync(meta.Id, options, progress.Add, chunkLimit: 100);
        Assert.Equal(1, firstChunkCalls); Assert.Contains(progress, p => p.Reused); Assert.Equal("ready", store.Load(meta.Id).Meta.SummaryState);
    }
    [Fact]
    public async Task CancellingRegenerationPreservesExistingSummary()
    {
        var store = new SessionStore(new AppPaths(root)); var meta = new SessionMeta(); await store.CreateAsync(meta);
        await store.AppendTranscriptAsync(meta, new TranscriptLine { Date = meta.StartedAt, Text = "已有会议内容" });
        await store.SaveSummaryAsync(meta, "原有纪要", "fixture");
        var client = new FakeClient((_, token) => Task.FromCanceled<string>(new CancellationToken(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SummaryService(store, client).GenerateAsync(meta.Id, new("http://localhost/v1", "fixture", "")));
        var data = store.Load(meta.Id); Assert.Equal("原有纪要", data.Summary); Assert.Equal("ready", data.Meta.SummaryState);
    }
    [Fact]
    public async Task StaleMetadataCannotEraseSummaryStateOrNewerCounts()
    {
        var store = new SessionStore(new AppPaths(root)); var meta = new SessionMeta(); await store.CreateAsync(meta);
        var stale = store.Load(meta.Id).Meta;
        await store.SaveSummaryAsync(meta, "已完成纪要", "fixture");
        await store.AppendAnswerAsync(stale, new AnswerRecord { Question = "测试？", Answer = "测试。" });
        await store.AppendTranscriptAsync(meta, new TranscriptLine { Date = meta.StartedAt, Text = "补充内容" });
        var data = store.Load(meta.Id); Assert.Equal("ready", data.Meta.SummaryState); Assert.Equal(1, data.Meta.AnswerCount); Assert.Equal(1, data.Meta.TranscriptCount);
    }
    private sealed class FakeClient(Func<string, CancellationToken, Task<string>> respond) : IAnswerClient
    {
        public Task<string> AnswerAsync(AnswerOptions options, string system, string user, Action<string>? update = null, CancellationToken ct = default, int timeoutSeconds = 90, string? imagePath = null) => respond(user, ct);
    }
    [Fact]
    public async Task RecoverySkipsAnActiveLeaseAndRepairsATornAppendAfterRelease()
    {
        var store = new SessionStore(new AppPaths(root)); var meta = new SessionMeta(); await store.CreateAsync(meta);
        using (store.AcquireLease(meta.Id))
        {
            await store.AppendTranscriptAsync(meta, new TranscriptLine { Date = meta.StartedAt, EndedAt = meta.StartedAt.AddSeconds(2), Text = "已完整写入" });
            await store.SetSummaryStateAsync(meta, "pending");
            Assert.Empty(await store.RecoverInterruptedAsync());
        }
        var file = Path.Combine(store.Folder(meta.Id), "transcript.jsonl"); await File.AppendAllTextAsync(file, "{\"incomplete\":");
        var original = await File.ReadAllBytesAsync(file);
        var recovered = Assert.Single(await store.RecoverInterruptedAsync());
        Assert.Null(recovered.Error); Assert.Equal(1, recovered.RepairedFiles);
        Assert.Equal(original, await File.ReadAllBytesAsync(Directory.GetFiles(store.Folder(meta.Id), "transcript.jsonl.recovery-*.bak").Single()));
        var data = store.Load(meta.Id); Assert.Single(data.Lines); Assert.Equal("failed", data.Meta.SummaryState); Assert.NotNull(data.Meta.EndedAt);
        Assert.Empty(await store.RecoverInterruptedAsync());
    }
    [Fact]
    public async Task LegacySessionWithoutLeaseMarkerIsPreserved()
    {
        var store = new SessionStore(new AppPaths(root)); var meta = new SessionMeta(); await store.CreateAsync(meta);
        var file = Path.Combine(store.Folder(meta.Id), "meta.json"); var before = await File.ReadAllBytesAsync(file);
        Assert.Empty(await store.RecoverInterruptedAsync()); Assert.Equal(before, await File.ReadAllBytesAsync(file));
    }
}
