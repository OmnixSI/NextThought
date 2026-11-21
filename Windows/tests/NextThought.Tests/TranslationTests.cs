using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;
public sealed class TranslationTests
{
    private sealed class DeferredClient : IAnswerClient
    {
        public readonly Dictionary<string, TaskCompletionSource<string>> Requests = [];
        public async Task<string> AnswerAsync(AnswerOptions options, string system, string user, Action<string>? update = null, CancellationToken ct = default, int timeoutSeconds = 90, string? imagePath = null)
        {
            var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Requests[user] = reply; return await reply.Task.WaitAsync(ct);
        }
    }
    [Fact]
    public async Task LateOldRevisionAndCanceledResponseCannotOverwriteLatest()
    {
        var client = new DeferredClient(); using var service = new MeetingTranslator(client, new("https://example.invalid", "fixture", "")); var id = Guid.NewGuid();
        var old = service.TranslateAsync(id, "会议星期二召开"); var latest = service.TranslateAsync(id, "会议星期三召开");
        client.Requests["会议星期三召开"].SetResult("The meeting is on Wednesday."); Assert.Equal("The meeting is on Wednesday.", (await latest)!.Text);
        client.Requests["会议星期二召开"].SetResult("The meeting is on Tuesday."); Assert.Null(await old);
        var canceled = service.TranslateAsync(Guid.NewGuid(), "Cancel this utterance."); service.Cancel(); Assert.Null(await canceled);
        Assert.Equal("zh-en", MeetingTranslator.Direction("先录音，再生成会议纪要。")); Assert.Equal("en-zh", MeetingTranslator.Direction("Do not change 42 MPa.")); Assert.Null(MeetingTranslator.Direction("1234 !"));
    }
    [Fact]
    public async Task TranslationReloadExportBackupKeepOriginalAndRejectMismatchedRevision()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-translate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root); var store = new SessionStore(paths); var meta = new SessionMeta(); await store.CreateAsync(meta);
            var line = new TranscriptLine { Text = "限速不是 42 公里。", Date = meta.StartedAt }; await store.AppendTranscriptAsync(meta, line);
            var file = Path.Combine(store.Folder(meta.Id), "transcript.jsonl"); var original = File.ReadAllBytes(file);
            var t = new SessionTranslation(line.Id, 2, MeetingTranslator.SourceHash(line.Text), "zh-en", "The speed limit is not 42 km/h.", "fixture", DateTimeOffset.UtcNow);
            await store.AppendTranslationAsync(meta.Id, t with { SourceHash = "mismatched" }); Assert.Null(store.Load(meta.Id).Lines.Single().Translation);
            await store.AppendTranslationAsync(meta.Id, t); Assert.Equal(t.Text, store.Load(meta.Id).Lines.Single().Translation!.Text); Assert.Equal(original, File.ReadAllBytes(file));
            var md = Path.Combine(root, "export.md"); await store.ExportMarkdownAsync(meta.Id, md); Assert.Contains(line.Text, File.ReadAllText(md)); Assert.Contains(t.Text, File.ReadAllText(md));
            await store.EndAsync(meta); var backup = Path.Combine(root, "fixture.ntbackup"); await new BackupService(paths).ExportAsync(backup);
            var restored = new AppPaths(Path.Combine(root, "restored")); await new BackupService(restored).ImportAsync(backup);
            Assert.Equal(t.Text, new SessionStore(restored).Load(meta.Id).Lines.Single().Translation!.Text);
        }
        finally { Directory.Delete(root, true); }
    }
}
