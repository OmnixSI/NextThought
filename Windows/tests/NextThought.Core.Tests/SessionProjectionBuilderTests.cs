using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the measurable projection builder.</summary>
public sealed class SessionProjectionBuilderTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(8));

    private static TranscriptLine Line(string text, int offset, string? translation = null) => new()
    {
        Text = text,
        Date = Start.AddSeconds(offset),
        StartedAt = Start.AddSeconds(offset),
        Translation = translation is null
            ? null
            : new SessionTranslation(Guid.NewGuid(), 2, "hash", "zh-en", translation, "model", Start)
    };

    [Fact]
    public void TranscriptLinesBecomeLocatedEntries()
    {
        var line = Line("Kubernetes 部署", 65);
        var projection = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [line], []);
        var text = Assert.Single(projection.SearchableText);
        Assert.Equal("line:" + line.Id, text.Id);
        Assert.Equal(SessionProjectionTextKind.Transcript, text.Kind);
        Assert.Equal("01:05", text.Location);
        Assert.Equal(line.Id, text.LineId);
        Assert.False(string.IsNullOrEmpty(text.SourceRevision));
    }

    [Fact]
    public void TranslationsAppearUnlessSourceOnly()
    {
        var line = Line("hello", 1, "你好");
        var bilingual = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [line], []);
        Assert.Equal(2, bilingual.SearchableText.Count);
        Assert.Equal(SessionProjectionTextKind.Translation, bilingual.SearchableText[1].Kind);
        var sourceOnly = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [line], [],
            options: new SessionProjectionOptions { LanguageView = SessionLanguageView.Source });
        Assert.Single(sourceOnly.SearchableText);
    }

    [Fact]
    public void QuestionsAnswersAndSummaryHaveStableIds()
    {
        var answer = new SessionProjectedAnswer(Guid.NewGuid(), Start, "为什么？", "因为。", "rev");
        var question = new SessionQuestionEvent { Text = "怎么做？" };
        var summary = new SessionProjectedSummary("复盘", "## 摘要\n内容", "t1", SessionDerivedContentStatus.Stale);
        var projection = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [], [answer], summary, [question]);
        Assert.Contains(projection.SearchableText, text => text.Id == "question:" + question.Id && text.Kind == SessionProjectionTextKind.Question);
        Assert.Contains(projection.SearchableText, text => text.Id == "answer:" + answer.Id && text.AlternateText == "为什么？");
        Assert.Contains(projection.SearchableText, text => text.Id == "summary" && text.IsStale);
        Assert.Equal(SessionDerivedContentStatus.Stale, projection.DerivedContentStatus);
    }

    [Fact]
    public void NotesBecomeANotesEntryAndStayOnTheProjection()
    {
        var notes = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("结论", Points: [new OutlinePoint("先演练")])]));
        var projection = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [], [], notes: notes);
        Assert.Same(notes, projection.Notes);
        var entry = Assert.Single(projection.SearchableText, item => item.Kind == SessionProjectionTextKind.Notes);
        Assert.Contains("先演练", entry.Text);
        Assert.Equal("notes:Outline", entry.Id);
        Assert.Equal(SessionDerivedContentStatus.Current, projection.DerivedContentStatus);

        var without = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [], []);
        Assert.Null(without.Notes);
        Assert.DoesNotContain(without.SearchableText, item => item.Kind == SessionProjectionTextKind.Notes);
        Assert.NotEqual(projection.Revision, without.Revision);
    }

    [Fact]
    public void RevisionIsStableAndChangesWithText()
    {
        var line = Line("同一句", 1);
        var first = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [line], []);
        var second = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [line], []);
        Assert.Equal(first.Revision, second.Revision);
        var changed = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [line with { Text = "改过" }], []);
        Assert.NotEqual(first.Revision, changed.Revision);
        Assert.Equal(64, first.Revision.Length);
    }

    [Fact]
    public void EmptyMeetingIsCurrentWithARevision()
    {
        var projection = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [], []);
        Assert.Empty(projection.SearchableText);
        Assert.Equal(SessionDerivedContentStatus.Current, projection.DerivedContentStatus);
        Assert.Equal(64, projection.Revision.Length);
    }

    [Fact]
    public void AnswersCarryCompletionState()
    {
        var answer = new SessionProjectedAnswer(Guid.NewGuid(), Start, "q", "a", "r", AnswerCompletion.Failed);
        var projection = SessionProjectionBuilder.Build(Guid.NewGuid(), Start, [], [answer]);
        Assert.False(projection.Answers[0].IsComplete);
    }
}
