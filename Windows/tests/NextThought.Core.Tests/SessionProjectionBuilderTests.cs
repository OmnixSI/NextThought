using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B7: translated from SessionTextProjectionTests.swift.</summary>
public sealed class SessionProjectionBuilderTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Fact]
    public void NoteAndSummarySearchHitsExposeTheirStaleSourceRevision()
    {
        var meta = new SessionMeta { Id = Guid.NewGuid(), StartedAt = Start, EndedAt = Start.AddSeconds(30) };
        var line = new TranscriptLine { Date = Start.AddSeconds(5), StartedAt = Start.AddSeconds(5), Speaker = "remote", Text = "周一提交" };
        var notes = new StructuredNotesDoc("general", "会议", "zh", "安排",
            [new NotesSectionResult("decisions", "决定", NotesSectionKind.Items, false,
                [new NotesItem { Text = "周五提交" }])])
        {
            TranscriptSourceRevision = "previous-transcript"
        };
        var summary = new SessionProjectedSummary("旧摘要", "周五提交", "previous-transcript",
            SessionDerivedContentStatus.Unknown);
        var projection = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, [line], [], summary,
            textNotes: notes);
        Assert.Contains(projection.SearchableText, text => text.Kind == SessionProjectionTextKind.Notes
            && text.Text == "周五提交" && text.IsStale);
        Assert.Contains(projection.SearchableText, text => text.Kind == SessionProjectionTextKind.Summary
            && text.Text == "周五提交" && text.IsStale);
        Assert.Equal(SessionDerivedContentStatus.Stale, projection.Summary?.SourceStatus);
        Assert.NotEmpty(projection.TranscriptSourceRevision);
    }

    [Fact]
    public void ProjectionSearchHitKeepsLineIdentityAndTranslationRevisionChanges()
    {
        var id = Guid.NewGuid();
        var first = new TranscriptLine
        {
            Id = id, Date = Start, StartedAt = Start, Speaker = "remote", Text = "原文",
            Translation = new SessionTranslation(Guid.NewGuid(), 1, "hash", "zh-en", "First", "model", Start),
            TranslatedText = "First", TranslationInputRevision = TranscriptRevisions.Of(id, "原文")
        };
        var updated = first with
        {
            Translation = new SessionTranslation(Guid.NewGuid(), 2, "hash2", "zh-en", "Updated", "model", Start),
            TranslatedText = "Updated"
        };
        var a = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [first], []);
        var b = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [updated], []);
        var hitA = a.SearchableText.First(text => text.Kind == SessionProjectionTextKind.Transcript);
        Assert.Equal(id, hitA.LineId);
        Assert.Equal("line:" + id + ":original", hitA.Id);
        var translationA = a.SearchableText.First(text => text.Kind == SessionProjectionTextKind.Translation);
        var translationB = b.SearchableText.First(text => text.Kind == SessionProjectionTextKind.Translation);
        Assert.Equal(translationA.Id, translationB.Id);
        Assert.NotEqual(translationA.SourceRevision, translationB.SourceRevision);
    }

    [Fact]
    public void SummaryQuestionsLessonsAndBankBecomeSearchableText()
    {
        var id = Guid.NewGuid();
        var summary = new SessionProjectedSummary("摘要", "正文", null, SessionDerivedContentStatus.Unknown,
            Questions: [new SummaryQuestion { Id = "q1", Text = "问题一", AnswerGist = "回答一" }],
            Lessons: ["经验一"],
            Bank: [new BankEntry { Question = "题库问题", Answer = "题库答案" }]);
        var projection = SessionProjectionBuilder.Build(id, Start, [], [], summary);
        Assert.Contains(projection.SearchableText, text => text.Id == "summary:q1:question"
            && text.Kind == SessionProjectionTextKind.Question && text.Text == "问题一");
        Assert.Contains(projection.SearchableText, text => text.Id == "summary:q1:answer"
            && text.Kind == SessionProjectionTextKind.Answer && text.Text == "回答一");
        Assert.Contains(projection.SearchableText, text => text.Id == "summary:" + id + ":lesson:0"
            && text.Text == "经验一");
        Assert.Contains(projection.SearchableText, text => text.Text == "题库答案"
            && text.Kind == SessionProjectionTextKind.Answer);
    }

}
