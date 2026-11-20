using NextThought.Core;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E19: the export sheet builds one projection for 格式 / 转写版本 / 语言, and the same text is
/// previewed and saved (SessionExportUI.swift + SessionExportPreviewContent.make).
/// </summary>
public sealed class SessionExportContentTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 4, 9, 0, 0, TimeSpan.Zero);

    private static (SessionMeta Meta, IReadOnlyList<TranscriptLine> Lines, SessionTextProjection Projection) Build(
        SessionProjectionOptions options)
    {
        var meta = new SessionMeta { Title = "导出测试 / 会议: 缓存", StartedAt = Start, EndedAt = Start.AddMinutes(30) };
        var line = new TranscriptLine
        {
            Date = Start.AddSeconds(10),
            StartedAt = Start.AddSeconds(10),
            EndedAt = Start.AddSeconds(15),
            Speaker = "remote",
            Text = "原始说法：先删缓存",
            Translation = new SessionTranslation(Guid.NewGuid(), 1, "hash", "zh-en", "Delete the cache first.", "m", Start)
        };
        var second = new TranscriptLine
        {
            Date = Start.AddSeconds(30),
            StartedAt = Start.AddSeconds(30),
            EndedAt = Start.AddSeconds(36),
            Speaker = "me",
            Text = "结论：先更新数据库"
        };
        var lines = new[] { line, second };
        var corrections = new[]
        {
            new TranscriptCorrection
            {
                Line = 0,
                LineId = line.Id,
                Original = "原始说法：先删缓存",
                Corrected = "先更新数据库再删缓存",
                Source = TranscriptCorrectionSource.User
            }
        };
        var projection = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, lines,
            [new SessionProjectedAnswer(Guid.NewGuid(), Start.AddSeconds(40), "缓存怎么保证一致？", "先更新数据库再删缓存。", "rev")],
            summary: new SessionProjectedSummary(meta.Title, "## 摘要\n缓存一致性要在上线前确认。"),
            options: options, corrections: corrections);
        return (meta, lines, projection);
    }

    [Fact]
    public void CorrectedVariantUsesTheCorrectionAndOriginalKeepsTheSourceText()
    {
        var corrected = Build(new SessionProjectionOptions { TranscriptVariant = SessionTranscriptVariant.Corrected });
        var correctedText = SessionExportContent.Build(SessionExportFormat.Markdown, corrected.Projection,
            corrected.Meta, corrected.Lines, "会议", 1800, "line-in");
        Assert.Contains("先更新数据库再删缓存", correctedText);
        Assert.DoesNotContain("原始说法：先删缓存", correctedText);

        var original = Build(new SessionProjectionOptions { TranscriptVariant = SessionTranscriptVariant.Original });
        var originalText = SessionExportContent.Build(SessionExportFormat.Markdown, original.Projection,
            original.Meta, original.Lines, "会议", 1800, "line-in");
        Assert.Contains("原始说法：先删缓存", originalText);
    }

    [Fact]
    public void LanguageViewControlsWhetherTheTranslationAppears()
    {
        var source = Build(new SessionProjectionOptions { LanguageView = SessionLanguageView.Source });
        var sourceText = SessionExportContent.Build(SessionExportFormat.Markdown, source.Projection, source.Meta,
            source.Lines, "会议", 1800, "line-in");
        Assert.DoesNotContain("Delete the cache first.", sourceText);

        var bilingual = Build(new SessionProjectionOptions { LanguageView = SessionLanguageView.Bilingual });
        var bilingualText = SessionExportContent.Build(SessionExportFormat.Markdown, bilingual.Projection,
            bilingual.Meta, bilingual.Lines, "会议", 1800, "line-in");
        Assert.Contains("Delete the cache first.", bilingualText);
    }

    [Fact]
    public void SubtitleFormatsUseTheSameProjection()
    {
        var built = Build(new SessionProjectionOptions());
        var srt = SessionExportContent.Build(SessionExportFormat.Srt, built.Projection, built.Meta, built.Lines,
            "会议", 1800, "line-in");
        var vtt = SessionExportContent.Build(SessionExportFormat.Vtt, built.Projection, built.Meta, built.Lines,
            "会议", 1800, "line-in");
        Assert.StartsWith("1\n00:00:10,000 --> ", srt);
        Assert.StartsWith("WEBVTT", vtt);
        Assert.Contains("先更新数据库再删缓存", srt);
    }

    [Fact]
    public void SubtitleLanguageMappingMatchesTheMacPicker()
    {
        Assert.Equal(SessionSubtitleContent.Bilingual, SessionExportContent.SubtitleContent(SessionLanguageView.Bilingual));
        Assert.Equal(SessionSubtitleContent.Original, SessionExportContent.SubtitleContent(SessionLanguageView.Source));
        Assert.Equal(SessionSubtitleContent.Translation, SessionExportContent.SubtitleContent(SessionLanguageView.Translated));
    }

    [Fact]
    public void SuggestedFileNameSanitisesPathSeparatorsAndAddsTheExtension()
    {
        Assert.Equal("导出测试 - 会议- 缓存.md", SessionExportContent.SuggestedFileName("导出测试 / 会议: 缓存", SessionExportFormat.Markdown));
        Assert.Equal("会议.docx", SessionExportContent.SuggestedFileName("会议", SessionExportFormat.Docx));
        Assert.Equal("会议.vtt", SessionExportContent.SuggestedFileName("会议", SessionExportFormat.Vtt));
    }

    [Fact]
    public void OnlyDocumentFormatsUsePageLayout()
    {
        Assert.True(SessionExportContent.UsesPageLayout(SessionExportFormat.Markdown));
        Assert.True(SessionExportContent.UsesPageLayout(SessionExportFormat.Docx));
        Assert.True(SessionExportContent.UsesPageLayout(SessionExportFormat.Pdf));
        Assert.False(SessionExportContent.UsesPageLayout(SessionExportFormat.Srt));
        Assert.False(SessionExportContent.UsesPageLayout(SessionExportFormat.Vtt));
    }
}
