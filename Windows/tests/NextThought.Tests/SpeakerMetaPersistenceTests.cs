using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E12: the Mac-compatible voice labels in meta.json (voiceNames/meVoices) and the merge that moves
/// every tagged line to another voice without rewriting transcript.jsonl.
/// </summary>
public sealed class SpeakerMetaPersistenceTests
{
    private static async Task<(SessionStore Store, SessionMeta Meta, TranscriptLine[] Lines)> SeedAsync(string root)
    {
        var store = new SessionStore(new AppPaths(root));
        var meta = new SessionMeta { Title = "说话人测试" };
        await store.CreateAsync(meta);
        var lines = new List<TranscriptLine>();
        for (var index = 0; index < 4; index++)
            lines.Add(new TranscriptLine { Date = meta.StartedAt.AddSeconds(index), Speaker = "room", Text = "第一人的发言 " + index });
        for (var index = 0; index < 4; index++)
            lines.Add(new TranscriptLine { Date = meta.StartedAt.AddSeconds(10 + index), Speaker = "room", Text = "第二人的发言 " + index });
        foreach (var line in lines) await store.AppendTranscriptAsync(meta, line);
        for (var index = 0; index < 4; index++) await store.AppendVoiceTagAsync(meta.Id, lines[index].Id, 0);
        for (var index = 4; index < 8; index++) await store.AppendVoiceTagAsync(meta.Id, lines[index].Id, 1);
        return (store, meta, lines.ToArray());
    }

    [Fact]
    public async Task NameAndMeMarkLandInTheMacCompatibleMetaJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-speakers-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (store, meta, _) = await SeedAsync(root);
            await store.SetVoiceLabelAsync(meta.Id, 0, "陈老师");
            await store.SetVoiceLabelAsync(meta.Id, 1, " 小李 ", isMe: true);

            var loaded = store.Load(meta.Id).Meta;
            Assert.Equal("陈老师", loaded.VoiceNames!["0"]);
            Assert.Equal("小李", loaded.VoiceNames["1"]);
            Assert.Equal([1], loaded.MeVoices);
            var json = File.ReadAllText(Path.Combine(store.Folder(meta.Id), "meta.json"));
            Assert.Contains("voiceNames", json);
            Assert.Contains("meVoices", json);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ClearingNameAndMeMarkRemovesTheKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-speakers-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (store, meta, _) = await SeedAsync(root);
            await store.SetVoiceLabelAsync(meta.Id, 0, "陈老师", isMe: true);
            await store.SetVoiceLabelAsync(meta.Id, 0, null, isMe: false);

            var loaded = store.Load(meta.Id).Meta;
            Assert.Null(loaded.VoiceNames);
            Assert.Null(loaded.MeVoices);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MergeMovesEveryTaggedLineAndKeepsTheTranscriptBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-speakers-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (store, meta, lines) = await SeedAsync(root);
            await store.SetVoiceLabelAsync(meta.Id, 1, "小李", isMe: true);
            var transcriptPath = Path.Combine(store.Folder(meta.Id), "transcript.jsonl");
            var originalBytes = File.ReadAllBytes(transcriptPath);

            await store.MergeVoicesAsync(meta.Id, 1, 0);

            var data = store.Load(meta.Id);
            Assert.All(data.Lines, line => Assert.Equal(0, line.Voice));
            Assert.Equal(originalBytes, File.ReadAllBytes(transcriptPath)); // voice tags stay in voices.jsonl
            Assert.Equal("小李", data.Meta.VoiceNames!["0"]);
            Assert.Equal([0], data.Meta.MeVoices);
            Assert.All(lines, line => Assert.Contains(line.Id, data.Lines.Select(row => row.Id)));
        }
        finally { Directory.Delete(root, true); }
    }
}
