using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E19 (decisions §3.4 export/SessionExportSheet 2/3/4): the user's own notes and highlights are
/// part of the exported pages, and the stale-derived-content notice uses the Mac sentences.
/// </summary>
public sealed class SessionExportUserContentTests
{
    [Fact]
    public void PersonalNotesAndHighlightsAreAppendedLikeTheMac()
    {
        var content = new SessionUserContent
        {
            PersonalNotes = "第一行笔记\n第二行",
            Highlights =
            [
                new SessionHighlight { OffsetSeconds = 5, Quote = "缓存一致性" },
                new SessionHighlight { OffsetSeconds = 3725, Quote = "1:02:05 的结论" }
            ]
        };
        var markdown = SessionExportContent.UserContentMarkdown(content);
        Assert.Contains("\n\n## 我的笔记\n\n第一行笔记\n第二行", markdown, StringComparison.Ordinal);
        Assert.Contains("\n\n## 重点标记\n\n", markdown, StringComparison.Ordinal);
        Assert.Contains("- [00:05] 缓存一致性", markdown, StringComparison.Ordinal);
        Assert.Contains("- [1:02:05] 1:02:05 的结论", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyUserContentAddsNothing()
    {
        Assert.Equal("", SessionExportContent.UserContentMarkdown(new SessionUserContent()));
    }

    [Fact]
    public void TheDerivedContentNoticeUsesTheMacSentences()
    {
        Assert.Equal("", SessionExportContent.DerivedContentNotice(SessionDerivedContentStatus.Current));
        Assert.Equal("纪要或手记可能已过期；导出预览已附上核对提示。",
            SessionExportContent.DerivedContentNotice(SessionDerivedContentStatus.Stale));
        Assert.Equal("旧纪要未记录来源版本；导出预览会说明无法确认是否过期。",
            SessionExportContent.DerivedContentNotice(SessionDerivedContentStatus.Unknown));
    }
}
