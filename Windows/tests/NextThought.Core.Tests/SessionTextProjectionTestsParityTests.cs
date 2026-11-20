using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from SessionTextProjectionTests.swift: corrections anchored to stable line ids,
/// the searchable projection (corrections, translations, answers, notes, summary) and the
/// Markdown export sharing the same projection.
/// </summary>
public sealed class SessionTextProjectionTestsParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static SessionTranslation Translation(Guid lineId, string text, int revision, string sourceText) =>
        new(Guid.NewGuid(), revision, "", "zh-en", text, "test", Start,
            TranscriptRevisions.Of(lineId, sourceText));

    [Fact]
    public void CorrectionsFollowStableLineIDsAndRejectChangedSources()
    {
        var first = new TranscriptLine
        {
            Id = Guid.NewGuid(), Date = Start, Speaker = "remote", Text = "用 Redis 缓存数据"
        };
        var second = new TranscriptLine
        {
            Id = Guid.NewGuid(), Date = Start.AddSeconds(3), Speaker = "remote", Text = "Redis 需要一致性校验"
        };
        var anchored = new TranscriptCorrection
        {
            Line = 0,
            LineId = second.Id,
            BaseRevision = TranscriptRevisions.Of(second.Id, second.Text),
            Original = "一致性",
            Corrected = "缓存一致性"
        };

        var reordered = NotesCorrections.Apply([anchored], [second, first]);
        Assert.Equal("Redis 需要缓存一致性校验", reordered[0].Text);
        Assert.Equal(first.Text, reordered[1].Text);

        var changed = second with { Text = "Redis 需要新的校验" };
        var rejected = NotesCorrections.Apply([anchored], [changed]);
        Assert.Equal(changed.Text, rejected[0].Text);
    }

    [Fact]
    public void MultipleCorrectionsShareTheSameOriginalRevision()
    {
        var line = new TranscriptLine
        {
            Id = Guid.NewGuid(), Date = Start, Speaker = "remote", Text = "错误词和错误名"
        };
        var revision = TranscriptRevisions.Of(line.Id, line.Text);
        var corrections = new[]
        {
            new TranscriptCorrection
            {
                Line = 0, LineId = line.Id, BaseRevision = revision, Original = "错误词", Corrected = "正确词"
            },
            new TranscriptCorrection
            {
                Line = 0, LineId = line.Id, BaseRevision = revision, Original = "错误名", Corrected = "正确名"
            }
        };

        Assert.Equal("正确词和正确名", NotesCorrections.Apply(corrections, [line])[0].Text);
        Assert.Equal(2, NotesCorrections.Valid(corrections, [line]).Count);
    }

    [Fact]
    public void ProjectionUnifiesCorrectionsTranslationsAnswersAndNotes()
    {
        var sessionId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var line = new TranscriptLine
        {
            Id = lineId,
            Date = Start.AddSeconds(12),
            Speaker = "remote",
            Text = "周五交付",
            Translation = Translation(lineId, "Deliver Friday", 1, "周五交付")
        };
        var stableCorrection = new TranscriptCorrection
        {
            Line = 0,
            LineId = line.Id,
            BaseRevision = TranscriptRevisions.Of(line.Id, line.Text),
            Original = "周五",
            Corrected = "周一"
        };
        var notes = new StructuredNotesPayload(NotesFormat.Outline, outline: new OutlineNotes(
            Sections:
            [
                new OutlineSection("决定", At: 12, Points: [new OutlinePoint("周一交付", At: 12)])
            ]));
        var answer = new SessionProjectedAnswer(Guid.NewGuid(), Start.AddSeconds(14), "什么时候交付？", "周一", "test");
        var summary = new SessionProjectedSummary("交付", "## 摘要\n周一交付。");

        var projection = SessionProjectionBuilder.Build(sessionId, Start, [line], [answer],
            summary: summary, corrections: [stableCorrection], notes: notes);

        Assert.Equal("周一交付", projection.Transcript[0].DisplayText);
        Assert.Equal("周五交付", projection.Transcript[0].OriginalText);
        Assert.True(projection.Transcript[0].TranslationIsStale);
        Assert.Equal(line.Id, projection.Transcript[0].Id);
        Assert.Equal(TranscriptRevisions.Of(line.Id, "周一交付"), projection.Transcript[0].SourceRevision);
        Assert.Contains(projection.SearchableText,
            item => item.Kind == SessionProjectionTextKind.Transcript && item.Text == "周五交付" && item.LineId == line.Id);
        Assert.Contains(projection.SearchableText,
            item => item.Kind == SessionProjectionTextKind.CorrectedTranscript && item.Text == "周一交付" && item.LineId == line.Id);
        Assert.Contains(projection.SearchableText,
            item => item.Kind == SessionProjectionTextKind.Translation && item.Text == "Deliver Friday" && item.LineId == line.Id);
        Assert.True(projection.SearchableText.First(item => item.Kind == SessionProjectionTextKind.Translation).IsStale);
        Assert.Contains(projection.SearchableText,
            item => item.Kind == SessionProjectionTextKind.Answer && item.Text == "周一" && item.SourceId == answer.Id);
        Assert.Contains(projection.SearchableText,
            item => item.Kind == SessionProjectionTextKind.Notes && item.Text.Contains("周一交付", StringComparison.Ordinal));
        Assert.Contains(projection.SearchableText,
            item => item.Kind == SessionProjectionTextKind.Summary && item.Text.Contains("周一交付", StringComparison.Ordinal));
        Assert.Equal("交付", projection.Summary!.Title);
        Assert.Equal(64, projection.Revision.Length);
    }

    [Fact]
    public void MarkdownExportUsesTheSameCorrectedProjectionAsTheNotesPage()
    {
        var sessionId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var line = new TranscriptLine
        {
            Id = lineId,
            Date = Start.AddSeconds(5),
            Speaker = "remote",
            Text = "缓存鸡穿",
            Translation = Translation(lineId, "Cache failure", 1, "缓存鸡穿")
        };
        var correction = new TranscriptCorrection
        {
            Line = 0,
            LineId = line.Id,
            BaseRevision = TranscriptRevisions.Of(line.Id, line.Text),
            Original = "鸡穿",
            Corrected = "击穿"
        };
        var notes = new StructuredNotesPayload(NotesFormat.Outline);
        var projection = SessionProjectionBuilder.Build(sessionId, Start, [line], [],
            corrections: [correction], notes: notes);

        var text = SessionMarkdownExporter.Export("会议", "会议", Start, 30, "", projection, [line], notes: notes);

        Assert.Contains("**对方**：缓存击穿", text);
        Assert.DoesNotContain("**对方**：缓存鸡穿", text);
        Assert.Contains("*译文（待更新）：* Cache failure", text);
    }

    [Fact]
    public void MarkdownExportUsesEditedStructuredNotesInsteadOfOlderGeneratedText()
    {
        var sessionId = Guid.NewGuid();
        var line = new TranscriptLine
        {
            Id = Guid.NewGuid(), Date = Start.AddSeconds(5), Speaker = "remote", Text = "会议里确认继续观察。"
        };
        var editedNotes = new StructuredNotesPayload(NotesFormat.Outline, outline: new OutlineNotes(
            Sections:
            [
                new OutlineSection("决定", Points: [new OutlinePoint("用户改过的最终措辞")])
            ]), transcriptSourceRevision: SessionProjectionBuilder.TranscriptSourceRevision(sessionId, Start, [line], []));
        var olderSummary = new SessionProjectedSummary("交付安排", "## 决定\n模型原先写的措辞");

        var projection = SessionProjectionBuilder.Build(sessionId, Start, [line], [],
            summary: olderSummary, notes: editedNotes);

        var text = SessionMarkdownExporter.Export("会议", "会议", Start, 30, "", projection, [line], notes: editedNotes);

        Assert.Equal(editedNotes, projection.Notes);
        Assert.Contains("用户改过的最终措辞", text);
        Assert.DoesNotContain("模型原先写的措辞", text);
        Assert.Contains("会议里确认继续观察。", text);
    }

    [Fact]
    public void NoteAndSummarySearchHitsExposeTheirStaleSourceRevision()
    {
        var sessionId = Guid.NewGuid();
        var line = new TranscriptLine
        {
            Id = Guid.NewGuid(), Date = Start.AddSeconds(5), Speaker = "remote", Text = "周一提交"
        };
        var notes = new StructuredNotesPayload(NotesFormat.Outline, outline: new OutlineNotes(
            Sections:
            [
                new OutlineSection("决定", Points: [new OutlinePoint("周五提交")])
            ]), transcriptSourceRevision: "previous-transcript");
        var summary = new SessionProjectedSummary("旧摘要", "周五提交",
            SourceTranscriptRevision: "previous-transcript");

        var projection = SessionProjectionBuilder.Build(sessionId, Start, [line], [],
            summary: summary, notes: notes);

        Assert.Contains(projection.SearchableText,
            item => item.Kind == SessionProjectionTextKind.Notes
                    && item.Text.Contains("周五提交", StringComparison.Ordinal) && item.IsStale);
        Assert.Contains(projection.SearchableText,
            item => item.Kind == SessionProjectionTextKind.Summary && item.Text == "周五提交" && item.IsStale);
        Assert.Equal(SessionDerivedContentStatus.Stale, projection.Summary!.SourceStatus);
        Assert.Contains("较旧的转写版本",
            SessionMarkdownExporter.Export("会议", "会议", Start, 30, "", projection, [line], notes: notes));
    }

    [Fact]
    public void ProjectionSearchHitKeepsLineIdentityAcrossReordering()
    {
        var target = new TranscriptLine
        {
            Id = Guid.NewGuid(), Date = Start.AddSeconds(8), Speaker = "remote", Text = "目标句子"
        };
        var other = new TranscriptLine { Id = Guid.NewGuid(), Date = Start, Speaker = "me", Text = "另一句" };
        var first = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [target, other], []);
        var hit = first.SearchableText.First(item => item.Text == "目标句子");
        var reordered = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [other, target], []);

        Assert.Equal(target.Id, hit.LineId);
        Assert.Equal($"line:{target.Id}:original", hit.Id);
        Assert.Equal(target.Id, reordered.SearchableText.First(item => item.Id == hit.Id).LineId);
    }

    [Fact]
    public void TranslationSearchRevisionChangesWhenTheTranslationChanges()
    {
        var lineId = Guid.NewGuid();
        var first = new TranscriptLine
        {
            Id = lineId, Date = Start, Speaker = "remote", Text = "原文", Translation = Translation(lineId, "First", 1, "原文")
        };
        var updated = new TranscriptLine
        {
            Id = lineId, Date = Start, Speaker = "remote", Text = "原文", Translation = Translation(lineId, "Updated", 2, "原文")
        };
        var firstHit = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [first], [])
            .SearchableText.First(item => item.Kind == SessionProjectionTextKind.Translation);
        var updatedHit = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [updated], [])
            .SearchableText.First(item => item.Kind == SessionProjectionTextKind.Translation);

        Assert.Equal(firstHit.Id, updatedHit.Id);
        Assert.NotEqual(firstHit.SourceRevision, updatedHit.SourceRevision);
    }
}
