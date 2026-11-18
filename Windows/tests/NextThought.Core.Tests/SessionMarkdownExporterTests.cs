using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the Markdown meeting export.</summary>
public sealed class SessionMarkdownExporterTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 9, 30, 0, TimeSpan.FromHours(8));

    private static TranscriptLine Line(string text, int offsetSeconds, string speaker = "remote", string? translation = null) =>
        new()
        {
            Text = text,
            Speaker = speaker,
            StartedAt = Start.AddSeconds(offsetSeconds),
            Date = Start.AddSeconds(offsetSeconds),
            Translation = translation is null
                ? null
                : new SessionTranslation(Guid.NewGuid(), 1, "hash", "zh-en", translation, "model", Start)
        };

    private static SessionTextProjection Projection(params SessionProjectedAnswer[] answers) => new(
        Guid.NewGuid(), "rev", "t1", new SessionProjectionOptions(), answers,
        [new SessionProjectedText("t", SessionProjectionTextKind.Transcript, "x")]);

    private static string Export(SessionTextProjection? projection = null, SessionProjectedSummary? summary = null,
        IReadOnlyList<TranscriptLine>? lines = null, IReadOnlyList<SessionQuestionEvent>? questions = null)
    {
        var value = (projection ?? Projection()) with { Summary = summary };
        return SessionMarkdownExporter.Export("默认标题", "会议", Start, 90 * 60, "系统音频",
            value, lines ?? [Line("第一句", 5)], questions);
    }

    [Fact]
    public void HeaderUsesSummaryTitleAndFacts()
    {
        var markdown = Export(summary: new SessionProjectedSummary("复盘", ""));
        Assert.StartsWith("# 复盘\n", markdown);
        Assert.Contains("> 会议 · 2026-11-18 09:30 · 90 分钟 · 系统音频", markdown);
    }

    [Fact]
    public void SummaryMarkdownDropsItsTitleSection()
    {
        var markdown = Export(summary: new SessionProjectedSummary("复盘", "# 标题\n绪论\n## 结论\n好了"));
        Assert.Contains("## 结论\n好了", markdown);
        Assert.DoesNotContain("# 标题", markdown);
        Assert.DoesNotContain("绪论", markdown);
    }

    [Fact]
    public void FreshnessNoticesAreIncluded()
    {
        var stale = (Projection() with
        {
            Summary = new SessionProjectedSummary("t", "## x", SourceStatus: SessionDerivedContentStatus.Stale)
        });
        Assert.Contains("部分纪要内容来自较旧的转写版本", SessionMarkdownExporter.Export("t", "会议", Start, 0, "", stale, []));
        var unknown = (Projection() with
        {
            Summary = new SessionProjectedSummary("t", "## x", SourceStatus: SessionDerivedContentStatus.Unknown)
        });
        Assert.Contains("旧纪要未记录转写版本", SessionMarkdownExporter.Export("t", "会议", Start, 0, "", unknown, []));
    }

    [Fact]
    public void AnswersIncludeIncompleteWarning()
    {
        var markdown = Export(Projection(
            new SessionProjectedAnswer(Guid.NewGuid(), Start, "问题一", "答案一", "r"),
            new SessionProjectedAnswer(Guid.NewGuid(), Start, "问题二", "答案二", "r", AnswerCompletion.Interrupted)));
        Assert.Contains("## 会中回答", markdown);
        Assert.Contains("### 问题一", markdown);
        Assert.Contains("> 未完成的回答片段，请核实后使用。", markdown);
        Assert.Equal(1, markdown.Split("未完成的回答片段").Length - 1);
    }

    [Fact]
    public void DetectedQuestionsAppearWithoutSummary()
    {
        var markdown = Export(questions:
        [
            new SessionQuestionEvent { Text = "为什么？" },
            new SessionQuestionEvent { Text = "怎么做？" }
        ]);
        Assert.Contains("## 检测到的问题", markdown);
        Assert.Contains("1. 为什么？", markdown);
        Assert.Contains("2. 怎么做？", markdown);
    }

    [Fact]
    public void TranscriptHasTimestampsSpeakersAndTranslations()
    {
        var markdown = Export(lines:
        [
            Line("你好", 5, "me"),
            Line("hello", 65, "remote", "你好（译）")
        ]);
        Assert.Contains("## 完整转录", markdown);
        Assert.Contains("- `00:05` **我**：你好", markdown);
        Assert.Contains("- `01:05` **对方**：hello", markdown);
        Assert.Contains("  *译文：* 你好（译）", markdown);
    }

    [Fact]
    public void EmptyMeetingStillExportsAValidDocument()
    {
        var markdown = SessionMarkdownExporter.Export("空会议", "通用", Start, 0, "", Projection(), []);
        Assert.Contains("# 空会议", markdown);
        Assert.Contains("## 完整转录", markdown);
        Assert.EndsWith("\n", markdown);
    }
}
