using System.Net;
using System.Text;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class StorageAndTransportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nextthought-tests-"+Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        var path = Path.GetFullPath(directory); var root = Path.GetFullPath(Path.GetTempPath());
        if (Directory.Exists(path) && path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).StartsWith("nextthought-tests-")) Directory.Delete(path, true);
    }
    [Fact]
    public async Task ConcurrentAppendsSurviveReloadAndExportWithoutInterleaving()
    {
        var store = new SessionStore(new AppPaths(directory)); var meta = new SessionMeta { Title = "中文路径与会议" }; await store.CreateAsync(meta);
        await Task.WhenAll(Enumerable.Range(0, 30).Select(i => store.AppendTranscriptAsync(meta, new TranscriptLine { Date = DateTimeOffset.UtcNow, Text = "句子 " + i, Speaker = i%2 == 0 ? "remote" : "me" })));
        await store.AppendAnswerAsync(meta, new AnswerRecord { Question = "为什么？", Answer = "依据会议原话。", Model = "test" });
        await store.EndAsync(meta); await store.SaveSummaryAsync(meta, "## 决定\n\n待确认。", "test");
        var loaded = store.Load(meta.Id); Assert.Equal(30, loaded.Meta.TranscriptCount); Assert.Equal(30, loaded.Lines.Count); Assert.Single(loaded.Answers);
        Assert.Equal(30, loaded.Lines.Select(x => x.Text).Distinct().Count()); Assert.Equal("ready", loaded.Meta.SummaryState);
        Assert.DoesNotContain(".0", await File.ReadAllTextAsync(Path.Combine(store.Folder(meta.Id), "meta.json")));
        var export = Path.Combine(directory, "导出.md"); await store.ExportMarkdownAsync(meta.Id, export);
        Assert.Contains("待确认", await File.ReadAllTextAsync(export));
    }
    [Fact]
    public async Task TornLastAppendRecoversAndInteriorCorruptionIsSkipped()
    {
        var store = new SessionStore(new AppPaths(directory)); var meta = new SessionMeta(); await store.CreateAsync(meta);
        await store.AppendTranscriptAsync(meta, new TranscriptLine { Text = "已提交的句子", Date = DateTimeOffset.UtcNow });
        var file = Path.Combine(store.Folder(meta.Id), "transcript.jsonl"); await File.AppendAllTextAsync(file, "{\"incomplete\":");
        Assert.Single(store.Load(meta.Id).Lines);
        await File.AppendAllTextAsync(file, "\n{not json}\n");
        var loaded = store.Load(meta.Id);
        Assert.Equal(2, loaded.SkippedLines);
        Assert.Single(loaded.Lines);
    }
    [Fact]
    public async Task HistoryListingCannotDeadlockBehindAnAppendFromUiContext()
    {
        var store = new SessionStore(new AppPaths(directory)); var meta = new SessionMeta(); await store.CreateAsync(meta);
        await Task.Run(() =>
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new NonPumpingContext());
            try
            {
                var append = store.AppendTranscriptAsync(meta, new TranscriptLine { Text = "写入时刷新历史", Date = DateTimeOffset.UtcNow });
                Assert.Single(store.List());
                append.GetAwaiter().GetResult();
                Assert.Single(store.Load(meta.Id).Lines);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }).WaitAsync(TimeSpan.FromSeconds(5));
    }
    private sealed class NonPumpingContext : SynchronizationContext
    { public override void Post(SendOrPostCallback callback, object? state) { /* A blocked dispatcher cannot process callbacks. */ } }
    [Fact]
    public async Task ApiSecretIsDpapiProtectedAndExcludedFromSettings()
    {
        var paths = new AppPaths(directory); var store = new SettingsStore(paths); const string key = "test-only-secret-123456789";
        store.SaveApiKey(key); await store.SaveAsync(new AppSettings { Model = "test" });
        Assert.Equal(key, store.ReadApiKey()); Assert.DoesNotContain(key, await File.ReadAllTextAsync(paths.Settings));
        Assert.DoesNotContain(key, Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(directory, "Credentials", "answer-api.dpapi"))));
        store.SaveApiKey(""); Assert.Equal("", store.ReadApiKey());
    }
    [Fact]
    public async Task CorruptApiSecretSurvivesSavingOtherSettings()
    {
        var paths = new AppPaths(directory); var store = new SettingsStore(paths);
        store.SaveApiKey("test-only-secret");
        var secret = Path.Combine(directory, "Credentials", "answer-api.dpapi");
        await File.WriteAllBytesAsync(secret, [1, 2, 3, 4]);
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => store.ReadApiKey());
        await store.SaveAsync(new AppSettings { Model = "after-read-failure" });
        Assert.True(File.Exists(secret));
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(secret));
    }

    [Theory]
    [InlineData("https://example.com", "https://example.com/v1/chat/completions")]
    [InlineData("https://example.com/v1/", "https://example.com/v1/chat/completions")]
    [InlineData("http://localhost:11434/v1", "http://localhost:11434/v1/chat/completions")]
    public void EndpointNormalizationPreservesProviderPrefix(string input, string expected) => Assert.Equal(expected, ChatCompletionClient.Endpoint(input).AbsoluteUri);
    [Theory]
    [InlineData("http://example.com/v1")]
    [InlineData("https://key@example.com/v1")]
    [InlineData("https://example.com/v1?api_key=secret")]
    public void RejectsUnsafeOrAmbiguousApiAddresses(string address) => Assert.Throws<ArgumentException>(() => ChatCompletionClient.Endpoint(address));
    [Fact]
    public async Task ParsesUtf8SseAcrossSingleByteFragmentsAndStopsAtDone()
    {
        var payload = "data: {\"choices\":[{\"delta\":{\"content\":\"你好，\"}}]}\r\n\r\ndata: {\"choices\":[{\"delta\":{\"content\":\"Windows\"}}]}\n\ndata: [DONE]\n\n";
        string? requestBody = null;
        using var http = new HttpClient(new Handler(async request => { requestBody = await request.Content!.ReadAsStringAsync(); return new(HttpStatusCode.OK) { Content = new StreamContent(new FragmentedStream(Encoding.UTF8.GetBytes(payload))) }; }));
        var updates = new List<string>(); var client = new ChatCompletionClient(http);
        var answer = await client.AnswerAsync(new("http://localhost/v1", "model", "", "high"), "系统", "问题", updates.Add);
        Assert.Equal("你好，Windows", answer); Assert.Equal(2, updates.Count);
        using var body = JsonDocument.Parse(requestBody!); Assert.Equal("high", body.RootElement.GetProperty("reasoning_effort").GetString());
        Assert.False(body.RootElement.TryGetProperty("temperature", out _));
    }
    [Fact]
    public async Task CancellationInterruptsAnIdleResponse()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new IdleStream()) })));
        using var ct = new CancellationTokenSource(150);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ChatCompletionClient(http).AnswerAsync(new("http://localhost/v1", "model", ""), "s", "u", ct: ct.Token));
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => response(request); }
    private sealed class FragmentedStream(byte[] data) : MemoryStream(data)
    { public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], ct); }
    private sealed class IdleStream : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) { await Task.Delay(Timeout.Infinite, ct); return 0; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(); public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
