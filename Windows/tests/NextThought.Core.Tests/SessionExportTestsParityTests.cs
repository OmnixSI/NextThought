using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Translated from Tests/NextThoughtCoreTests/SessionExportTests.swift.</summary>
public sealed class SessionExportTestsParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static SessionMeta Meta(int endSeconds = 20) => new()
    {
        StartedAt = Start,
        EndedAt = Start.AddSeconds(endSeconds),
        Mode = "meeting"
    };

    [Fact]
    public void SubtitleExportUsesProjectionAndReportsEstimatedEnds()
    {
        var first = new TranscriptLine
        {
            Date = Start.AddSeconds(1), StartedAt = Start.AddSeconds(1), Speaker = "remote",
            Text = "原始识别", TranslatedText = "source text"
        };
        var second = new TranscriptLine
        {
            Date = Start.AddSeconds(4), StartedAt = Start.AddSeconds(4), EndedAt = Start.AddSeconds(5),
            Speaker = "me", Text = "校正后的内容"
        };
        var notes = new StructuredNotesDoc("general", "通用", "zh", "纪要", [],
            [new TranscriptCorrection { Line = 1, Original = "校正后的内容", Corrected = "当前校正版本" }]);
        var meta = Meta(6);

        var projection = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, [first, second], [],
            corrections: notes.Corrections);
        var result = SessionSubtitleExporter.ExportProjected(meta, projection.Transcript, SessionSubtitleFormat.Vtt);

        Assert.StartsWith("WEBVTT\n\n", result.Text);
        Assert.Contains("00:00:01.000 --> 00:00:02.200", result.Text);
        Assert.Contains("对方：原始识别", result.Text);
        Assert.Contains("当前校正版本", result.Text);
        Assert.Equal(1, result.EstimatedTimingCount);
    }

    [Fact]
    public void SubtitleExportLabelsStaleTranslations()
    {
        var line = new TranscriptLine
        {
            Date = Start.AddSeconds(10), StartedAt = Start.AddSeconds(10), Speaker = "remote",
            Text = "校正文本", TranslatedText = "old translation", TranslationInputRevision = "old-source"
        };
        var notes = new StructuredNotesDoc("general", "通用", "zh", "纪要", [],
            [new TranscriptCorrection { Line = 0, Original = "校正文本", Corrected = "新校正文本" }]);
        var meta = Meta(20);

        var projection = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, [line], [],
            corrections: notes.Corrections);
        var result = SessionSubtitleExporter.ExportProjected(meta, projection.Transcript, SessionSubtitleFormat.Srt);

        Assert.Contains("译文（可能已过期）：old translation", result.Text);
        Assert.Equal(1, result.StaleTranslationCount);
    }

    [Fact]
    public void SubtitleLanguageChoiceDoesNotLeakOrDuplicateTranslations()
    {
        var line = new TranscriptLine
        {
            Date = Start.AddSeconds(2), StartedAt = Start.AddSeconds(2), EndedAt = Start.AddSeconds(4),
            Speaker = "remote", Text = "原文内容", TranslatedText = "translated text"
        };
        var meta = Meta(5);
        var source = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, [line], [],
            options: new SessionProjectionOptions { LanguageView = SessionLanguageView.Source });
        var translated = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, [line], [],
            options: new SessionProjectionOptions { LanguageView = SessionLanguageView.Translated });
        var bilingual = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, [line], [],
            options: new SessionProjectionOptions { LanguageView = SessionLanguageView.Bilingual });

        var sourceExport = SessionSubtitleExporter.ExportProjected(meta, source.Transcript, SessionSubtitleFormat.Srt,
            SessionSubtitleContent.Original);
        var translatedExport = SessionSubtitleExporter.ExportProjected(meta, translated.Transcript,
            SessionSubtitleFormat.Srt, SessionSubtitleContent.Translation);
        var bilingualExport = SessionSubtitleExporter.ExportProjected(meta, bilingual.Transcript,
            SessionSubtitleFormat.Vtt, SessionSubtitleContent.Bilingual);

        Assert.Contains("原文内容", sourceExport.Text);
        Assert.DoesNotContain("translated text", sourceExport.Text);
        Assert.Contains("译文：translated text", translatedExport.Text);
        Assert.DoesNotContain("原文内容", translatedExport.Text);
        var sourcePosition = bilingualExport.Text.IndexOf("原文内容", StringComparison.Ordinal);
        var translationPosition = bilingualExport.Text.IndexOf("译文：translated text", StringComparison.Ordinal);
        Assert.True(sourcePosition >= 0 && translationPosition >= 0);
        Assert.True(sourcePosition < translationPosition);
        Assert.Equal(2, bilingualExport.Text.Split("translated text").Length);
    }

    [Fact(Skip = "Missing C# API: SessionMarkdownExporter only renders stale translation labels in the bilingual view. " +
                 "A translated-only projection (SessionLanguageView.Translated) does not emit '译文（待更新）：…'.")]
    public void TranslatedOnlyMarkdownLabelsStaleTranslations()
    {
    }

    [Fact]
    public void MarkdownAndSubtitlesRespectSelectedOriginalOrCorrectedProjection()
    {
        var lineId = Guid.NewGuid();
        const string originalText = "王小明说周五提交。";
        var line = new TranscriptLine
        {
            Id = lineId, Date = Start.AddSeconds(12), StartedAt = Start.AddSeconds(12),
            EndedAt = Start.AddSeconds(14), Speaker = "remote", Text = originalText,
            TranslatedText = "Wang said Friday.",
            TranslationInputRevision = TranscriptRevisions.Of(lineId, originalText)
        };
        var correction = new TranscriptCorrection
        {
            Line = 0,
            LineId = lineId,
            BaseRevision = TranscriptRevisions.Of(lineId, originalText),
            Original = "周五",
            Corrected = "下周一"
        };
        var meta = Meta(20);
        var corrected = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, [line], [],
            options: new SessionProjectionOptions
            {
                TranscriptVariant = SessionTranscriptVariant.Corrected, LanguageView = SessionLanguageView.Source
            },
            corrections: [correction]);
        var original = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, [line], [],
            options: new SessionProjectionOptions
            {
                TranscriptVariant = SessionTranscriptVariant.Original, LanguageView = SessionLanguageView.Source
            },
            corrections: [correction]);

        var correctedMarkdown = SessionMarkdownExporter.Export("会议", "会议", meta.StartedAt, 20, "",
            corrected, [line]);
        var originalMarkdown = SessionMarkdownExporter.Export("会议", "会议", meta.StartedAt, 20, "",
            original, [line]);
        var correctedVtt = SessionSubtitleExporter.ExportProjected(meta, corrected.Transcript,
            SessionSubtitleFormat.Vtt, SessionSubtitleContent.Original).Text;
        var originalVtt = SessionSubtitleExporter.ExportProjected(meta, original.Transcript,
            SessionSubtitleFormat.Vtt, SessionSubtitleContent.Original).Text;

        Assert.Contains("王小明说下周一提交。", correctedMarkdown);
        Assert.DoesNotContain("王小明说周五提交。", correctedMarkdown);
        Assert.Contains("王小明说周五提交。", originalMarkdown);
        Assert.DoesNotContain("王小明说下周一提交。", originalMarkdown);
        Assert.Contains("王小明说下周一提交。", correctedVtt);
        Assert.DoesNotContain("王小明说周五提交。", correctedVtt);
        Assert.Contains("王小明说周五提交。", originalVtt);
        Assert.DoesNotContain("王小明说下周一提交。", originalVtt);
    }

    [Fact]
    public void MarkdownFixturePreservesChineseNamesLongURLsNestedListsAndTables()
    {
        var longUrl = "https://example.invalid/releases/nextthought/2026/11/09/"
                      + string.Concat(Enumerable.Repeat("review-and-approval-", 12))
                      + "?owner=wang-xiaoming&reviewer=chen-xiaoyu&build=20261109";
        var summary = new SessionProjectedSummary("王小明与陈晓雨的交付复盘", $$"""
            ## 决定与行动
            - 王小明负责预发布验证。
              - 陈晓雨复核长会记录。
                - 如果失败，先保留旧版并联系值班负责人。

            | 负责人 | 交付物 | 日期 |
            | --- | --- | --- |
            | 王小明 | 预发布验证清单 | 周五 |
            | 陈晓雨 | 双语会议摘要 | 下周一 |

            ## 参考链接
            {{longUrl}}
            """);
        var line = new TranscriptLine
        {
            Date = Start.AddSeconds(6), StartedAt = Start.AddSeconds(6), Speaker = "remote",
            Text = "王小明确认预发布验证从周五开始。"
        };
        var meta = Meta(30);
        var projection = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, [line], [], summary);

        var markdown = SessionMarkdownExporter.Export("交付复盘", "工作会议", meta.StartedAt, 30, "", projection,
            [line]);

        Assert.Contains("王小明与陈晓雨的交付复盘", markdown);
        Assert.Contains("王小明负责预发布验证。", markdown);
        Assert.Contains("陈晓雨复核长会记录。", markdown);
        Assert.Contains("如果失败，先保留旧版并联系值班负责人。", markdown);
        Assert.Contains("| 王小明 | 预发布验证清单 | 周五 |", markdown);
        Assert.Contains("| 陈晓雨 | 双语会议摘要 | 下周一 |", markdown);
        Assert.Contains(longUrl, markdown);
        Assert.Contains("王小明确认预发布验证从周五开始。", markdown);
    }
}

/// <summary>NotesShapeTests in SessionExportTests.swift.</summary>
public sealed class NotesShapeTestsParityTests
{
    private static NotesItem Item(string text, string? topic = null) => new() { Text = text, Topic = topic };

    [Fact]
    public void MarkdownIsSavedAsMd()
    {
        Assert.Equal("md", SessionExportFormat.Markdown.FileExtension());
        Assert.Equal("docx", SessionExportFormat.Docx.FileExtension());
        Assert.Equal("srt", SessionExportFormat.Srt.FileExtension());
    }

    [Fact(Skip = "Missing C# API: NotesShape.groups (grouped view/list of groups). NotesJson.Tidy exposes ordering " +
                 "but not the group/topic projection asserted by the Swift test.")]
    public void PointsOfOneTopicSitTogether()
    {
    }

    [Fact]
    public void AListWithoutRealTopicsStaysFlat()
    {
        // One topic for everything, or topics on some items only, is no grouping.
        Assert.All(NotesJson.Tidy([Item("a", "会议"), Item("b", "会议")], NotesSectionKind.Items, false),
            item => Assert.Null(item.Topic));
        Assert.All(NotesJson.Tidy([Item("a", "x"), Item("b"), Item("c", "y")], NotesSectionKind.Items, false),
            item => Assert.Null(item.Topic));
    }

    [Fact]
    public void OpenQuestionsDoNotBuryTheDecisions()
    {
        var many = Enumerable.Range(1, 12).Select(index => Item("未决 " + index)).ToList();
        Assert.Equal(6, NotesJson.Tidy(many, NotesSectionKind.Items, true).Count);
        Assert.Equal(14, NotesJson.Tidy(Enumerable.Range(1, 30).Select(index => Item("要点 " + index)),
            NotesSectionKind.Items, false).Count);
        // Action items and question replays are things that happened: all kept.
        Assert.Equal(12, NotesJson.Tidy(many, NotesSectionKind.Actions, false).Count);
    }

    [Fact]
    public void TopicsShowAsLabelsInMarkdown()
    {
        var section = new NotesSectionResult("points", "要点", NotesSectionKind.Items, false,
        [
            new NotesItem { Text = "先上线 SaaS", Time = "12:19", Topic = "上线顺序" },
            new NotesItem { Text = "收入先投入研发", Topic = "资金安排" }
        ]);
        Assert.Equal(new[] { "**上线顺序**", "", "- 先上线 SaaS [12:19]", "", "**资金安排**", "", "- 收入先投入研发" },
            NotesMarkdown.Lines(section, english: false));
        var flat = new NotesSectionResult("points", "要点", NotesSectionKind.Items, false,
            [new NotesItem { Text = "一条" }]);
        Assert.Equal(new[] { "- 一条" }, NotesMarkdown.Lines(flat, english: false));
    }

    [Fact]
    public void ThePromptAsksForShortGroupedNotes()
    {
        var prompt = NotesPrompt.OrganizeSystem(NotesTemplate.BuiltIn(NotesTemplateKind.General),
            english: false, inPerson: false, meetingDate: DateTimeOffset.FromUnixTimeSeconds(1_794_000_000));
        Assert.Contains("topic", prompt);
        Assert.Contains("最多 5 条", prompt);
        Assert.DoesNotContain("详细", prompt);
    }
}
