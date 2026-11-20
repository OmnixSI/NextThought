using System.Text.Json;
using System.Text.RegularExpressions;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>A5: summary.json stays complete even when notes are produced in different orders.</summary>
public sealed class SummaryCompletenessTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "nt-summary-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task TextSummaryAlwaysHasTitleMarkdownQuestionsGeneratedAtAndModel()
    {
        var store = new SessionStore(new AppPaths(root));
        var meta = new SessionMeta
        {
            Id = Guid.NewGuid(),
            Title = "Mac 标题",
            StartedAt = DateTimeOffset.UtcNow
        };
        await store.CreateAsync(meta);
        await store.SaveSummaryAsync(meta, "正文", "mac-model");

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(store.Folder(meta.Id), "summary.json")));
        var summary = document.RootElement;
        Assert.Equal("Mac 标题", summary.GetProperty("title").GetString());
        Assert.Equal("正文", summary.GetProperty("markdown").GetString());
        Assert.Equal(JsonValueKind.Array, summary.GetProperty("questions").ValueKind);
        Assert.Equal("mac-model", summary.GetProperty("model").GetString());
        Assert.Matches(new Regex("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}Z$"),
            summary.GetProperty("generatedAt").GetString()!);
    }
}
