using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// F20: a meeting's context.json must be the Mac SessionContextSnapshot (SessionRecord.swift:123)
/// and nothing more, even with a 50-document library. The app builds this snapshot off the UI
/// thread; this test locks the on-disk shape that work produces.
/// </summary>
public sealed class SessionContextPersistenceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "nt-context-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    private static ContextDocument Material(int index) => new()
    {
        Id = Guid.NewGuid().ToString("D").ToUpperInvariant(),
        Name = $"资料 {index}.pdf",
        Category = "论文",
        Enabled = true,
        Pages = Enumerable.Range(0, 20).Select(page => "第 " + (page + 1) + " 页：" + new string('字', 500)).ToList()
    };

    [Fact]
    public async Task ContextJsonIsTheMacSnapshotWithFiftyDocuments()
    {
        var store = new SessionStore(new AppPaths(root));
        var meta = new SessionMeta { Id = Guid.NewGuid(), Title = "面试", Mode = "interview", StartedAt = DateTimeOffset.UnixEpoch };
        var snapshot = new SessionContextSnapshot
        {
            Mode = "interview",
            Style = "concise",
            Preferences = "",
            Profile = "",
            Documents = Enumerable.Range(0, 50).Select(Material).ToList(),
            EffectiveMode = "interview"
        };
        await store.CreateAsync(meta, snapshot);

        var json = await File.ReadAllTextAsync(Path.Combine(store.Folder(meta.Id), "context.json"));
        using var document = JsonDocument.Parse(json);
        // Exactly SessionContextSnapshot's Codable keys; Windows-only fields are not stored.
        var keys = document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet();
        Assert.Equal(
            new[] { "mode", "style", "preferences", "profile", "documents", "effectiveMode" }.OrderBy(name => name),
            keys.OrderBy(name => name));
        Assert.Equal("interview", document.RootElement.GetProperty("mode").GetString());

        var saved = document.RootElement.GetProperty("documents");
        Assert.Equal(50, saved.GetArrayLength());
        var first = saved[0];
        var documentKeys = first.EnumerateObject().Select(property => property.Name).ToHashSet();
        Assert.Equal(
            new[] { "id", "name", "pages", "enabled", "category" }.OrderBy(name => name),
            documentKeys.OrderBy(name => name));
        Assert.Equal(20, first.GetProperty("pages").GetArrayLength());
        Assert.Equal(new string('字', 500), first.GetProperty("pages")[0].GetString()!.Split('：')[^1]);
        Assert.True(first.GetProperty("enabled").GetBoolean());
        Assert.Equal("论文", first.GetProperty("category").GetString());
    }
}
