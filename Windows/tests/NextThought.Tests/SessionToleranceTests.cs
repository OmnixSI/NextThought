using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>A9: one damaged transcript line or an older evidence envelope must not hide a meeting.</summary>
public sealed class SessionToleranceTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(), "nt-tolerance-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CorruptMiddleTranscriptLineIsSkippedAndHighVoiceKept()
    {
        var root = Root();
        try
        {
            var paths = new AppPaths(root);
            var id = Guid.NewGuid();
            var folder = paths.Sessions + "\\" + id.ToString().ToUpperInvariant();
            Directory.CreateDirectory(folder);
            var meta = new SessionMeta { Id = id, Title = "t", StartedAt = DateTimeOffset.UtcNow };
            File.WriteAllText(Path.Combine(folder, "meta.json"), JsonSerializer.Serialize(meta, JsonDefaults.ArchiveOptions));
            var first = new TranscriptLine { Id = Guid.NewGuid(), Text = "one", Speaker = "them", Date = DateTimeOffset.UtcNow, Voice = 0 };
            var second = new TranscriptLine { Id = Guid.NewGuid(), Text = "two", Speaker = "them", Date = DateTimeOffset.UtcNow, Voice = 31 };
            var third = new TranscriptLine { Id = Guid.NewGuid(), Text = "three", Speaker = "them", Date = DateTimeOffset.UtcNow, Voice = 7 };
            File.WriteAllLines(Path.Combine(folder, "transcript.jsonl"),
            [
                JsonSerializer.Serialize(first, JsonDefaults.ArchiveOptions),
                "{ this is not json",
                JsonSerializer.Serialize(second, JsonDefaults.ArchiveOptions),
                JsonSerializer.Serialize(third, JsonDefaults.ArchiveOptions)
            ]);
            File.WriteAllText(Path.Combine(folder, "voices.jsonl"),
                JsonSerializer.Serialize(new SessionVoiceTag(second.Id, 31), JsonDefaults.ArchiveOptions) + "\n");

            var data = new SessionStore(paths).Load(id);
            Assert.Equal(1, data.SkippedLines);
            Assert.Equal(3, data.Lines.Count);
            Assert.Equal(31, data.Lines.Single(line => line.Id == second.Id).Voice);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void AnswerWithAnOlderEvidenceEnvelopeStillLoads()
    {
        var root = Root();
        try
        {
            var paths = new AppPaths(root);
            var id = Guid.NewGuid();
            var folder = paths.Sessions + "\\" + id.ToString().ToUpperInvariant();
            Directory.CreateDirectory(folder);
            var meta = new SessionMeta { Id = id, Title = "t", StartedAt = DateTimeOffset.UtcNow };
            File.WriteAllText(Path.Combine(folder, "meta.json"), JsonSerializer.Serialize(meta, JsonDefaults.ArchiveOptions));
            File.WriteAllText(Path.Combine(folder, "answers.jsonl"),
                "{\"id\":\"" + Guid.NewGuid() + "\",\"question\":\"q\",\"answer\":\"a\",\"evidence\":{\"materials\":[]}}\n");

            var data = new SessionStore(paths).Load(id);
            var answer = Assert.Single(data.Answers);
            Assert.Equal("q", answer.Question);
            Assert.Equal(0, data.SkippedLines);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task RewritingAMacMetaDoesNotAddWindowsDefaults()
    {
        var root = Root();
        try
        {
            var paths = new AppPaths(root);
            var id = Guid.NewGuid();
            var folder = paths.Sessions + "\\" + id.ToString().ToUpperInvariant();
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "meta.json"),
                "{\"id\":\"" + id + "\",\"startedAt\":\"2025-01-02T03:04:05Z\"}");
            var store = new SessionStore(paths);
            var meta = Assert.Single(store.List());
            await store.SetSummaryStateAsync(meta, "none");
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "meta.json")));
            var keys = document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet();
            Assert.DoesNotContain("title", keys);
            Assert.DoesNotContain("platform", keys);
            Assert.DoesNotContain("inPerson", keys);

            await store.AppendQuestionAsync(id, new SessionQuestionEvent { Text = "q", Date = DateTimeOffset.UtcNow });
            using var after = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "meta.json")));
            Assert.Equal(1, after.RootElement.GetProperty("questionCount").GetInt32());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
