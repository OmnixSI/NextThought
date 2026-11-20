using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E18: the history page's projection-backed content search (HistoryView.swift searchResults /
/// searchHitNavigator): every searchable field kind is reported with its location, so a row shows
/// where the hit came from, and deleted meetings disappear from search.
/// </summary>
public sealed class HistorySearchTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "nt-history-search-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    private static SessionMeta NewSession(AppPaths paths, string title, string transcript, string question, string answer)
    {
        var store = new SessionStore(paths);
        var meta = new SessionMeta { Title = title, Mode = "meeting" };
        store.CreateAsync(meta).GetAwaiter().GetResult();
        store.AppendTranscriptAsync(meta, new TranscriptLine
        {
            Date = meta.StartedAt.AddSeconds(5), StartedAt = meta.StartedAt, Speaker = "remote", Text = transcript
        }).GetAwaiter().GetResult();
        store.AppendAnswerAsync(meta, new AnswerRecord
        {
            Date = meta.StartedAt.AddSeconds(20), Question = question, Answer = answer, Model = "m", Automatic = true
        }).GetAwaiter().GetResult();
        store.SaveSummaryAsync(meta, "# 标题\n## 摘要\n缓存一致性要在上线前确认。", "m").GetAwaiter().GetResult();
        store.EndAsync(meta).GetAwaiter().GetResult();
        return meta;
    }

    [Fact]
    public void ContentSearchReportsTranscriptAnswerAndNotesKinds()
    {
        var paths = new AppPaths(Path.Combine(root, "data"));
        var meta = NewSession(paths, "周会", "我们讨论缓存一致性的方案", "缓存怎么保证一致", "先更新数据库再删缓存");
        var results = new SessionStore(paths).SearchContentHits("缓存");
        var result = Assert.Single(results);
        Assert.Equal(meta.Id, result.Meta.Id);
        Assert.True(result.Hits.Any(hit => hit.Kind == SessionSearchFieldKind.Transcript && hit.Excerpt.Contains("缓存一致性")),
            "transcript hit missing: " + string.Join(" | ", result.Hits.Select(hit => hit.Kind + ":" + hit.Excerpt)));
        Assert.True(result.Hits.Any(hit => hit.Kind == SessionSearchFieldKind.Answer && hit.Excerpt.Contains("先更新数据库")),
            "answer hit missing");
        Assert.True(result.Hits.Any(hit => hit.Kind == SessionSearchFieldKind.Notes),
            "notes hit missing");
        Assert.NotNull(result.First);
    }

    [Fact]
    public void DeletedMeetingsLeaveContentSearch()
    {
        var paths = new AppPaths(Path.Combine(root, "data2"));
        var meta = NewSession(paths, "客户会", "客户提到报价单", "报价单什么时候发", "本周五发出");
        var store = new SessionStore(paths);
        Assert.Single(store.SearchContentHits("报价单"));
        store.DeleteAsync(meta.Id).GetAwaiter().GetResult();
        Assert.Empty(store.SearchContentHits("报价单"));
    }
}
