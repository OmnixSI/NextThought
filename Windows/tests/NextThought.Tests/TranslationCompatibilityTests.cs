using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>A3: Mac translations.jsonl rows carry {id,text,inputRevision} and have no SourceHash.</summary>
public sealed class TranslationCompatibilityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "nt-translation-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { /* best effort */ }
    }

    [Fact]
    public void MacTranslationRowsAreReadByLineIdAndWrittenWithInputRevision()
    {
        var paths = new AppPaths(root);
        var store = new SessionStore(paths);
        var id = Guid.NewGuid();
        var folder = store.Folder(id);
        Directory.CreateDirectory(folder);
        var started = "2026-11-20T00:00:00Z";
        File.WriteAllText(Path.Combine(folder, "meta.json"),
            "{\"id\":\"" + id.ToString("D") + "\",\"title\":\"t\",\"startedAt\":\"" + started + "\",\"transcriptCount\":1,\"answerCount\":0}");
        File.WriteAllText(Path.Combine(folder, "transcript.jsonl"),
            "{\"id\":\"" + id.ToString("D") + "\",\"date\":\"" + started + "\",\"speaker\":\"remote\",\"text\":\"hello\"}\n");
        File.WriteAllText(Path.Combine(folder, "translations.jsonl"),
            "{\"id\":\"" + id.ToString("D") + "\",\"text\":\"你好\",\"inputRevision\":\"rev-1\"}\n");

        var line = store.Load(id).Lines.Single();
        Assert.Equal("你好", line.TranslatedText);
        Assert.Equal("rev-1", line.TranslationInputRevision);
        Assert.Equal("你好", line.Translation!.Text);

        var json = JsonSerializer.Serialize(new SessionTranslation(id, 1, "hash", "zh-en", "你好", "model", DateTimeOffset.UtcNow, "rev-1"), JsonDefaults.ArchiveOptions);
        Assert.Contains("\"inputRevision\":\"rev-1\"", json);
    }
}
