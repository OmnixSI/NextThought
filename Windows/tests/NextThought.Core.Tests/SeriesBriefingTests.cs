using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the series briefing builder.</summary>
public sealed class SeriesBriefingTests
{
    private static SessionMeta Session(string id, int day) => new()
    {
        Id = Guid.Parse(id),
        Title = "会议" + day,
        StartedAt = new DateTimeOffset(2026, 11, day, 10, 0, 0, TimeSpan.FromHours(8))
    };

    private static readonly string IdA = Guid.NewGuid().ToString("D");
    private static readonly string IdB = Guid.NewGuid().ToString("D");
    private static readonly string IdC = Guid.NewGuid().ToString("D");

    private static WorkspaceSeries Series(
        IReadOnlyList<WorkspaceLesson>? lessons = null,
        IReadOnlyList<QuestionBankItem>? questions = null,
        IReadOnlyList<WorkspaceKnowledgeCandidate>? candidates = null,
        bool useKnowledge = true) =>
        new("series", "结构组会", DateTimeOffset.UtcNow, [IdA, IdB, IdC], lessons ?? [])
        {
            QuestionBank = questions ?? [],
            KnowledgeCandidates = candidates ?? [],
            UseKnowledge = useKnowledge
        };

    [Fact]
    public void LatestSessionsAreNewestFirstAndLimited()
    {
        var sessions = new[] { Session(IdA, 1), Session(IdB, 2), Session(IdC, 3) };
        var briefing = SeriesBriefingBuilder.Build(Series(), sessions, meetingLimit: 2);
        Assert.Equal(3, briefing.SessionCount);
        Assert.Equal(["会议3", "会议2"], briefing.LatestSessions.Select(session => session.Title));
        Assert.Equal("series", briefing.WorkspaceId);
        Assert.True(briefing.KnowledgeEnabled);
    }

    [Fact]
    public void OnlyKnownSessionsCount()
    {
        var sessions = new[] { Session(IdA, 1), Session(Guid.NewGuid().ToString("D"), 2) };
        Assert.Equal(1, SeriesBriefingBuilder.Build(Series(), sessions).SessionCount);
        Assert.Empty(SeriesBriefingBuilder.Build(Series(), []).LatestSessions);
    }

    [Fact]
    public void PinnedLessonsComeFirstThenNewest()
    {
        var now = DateTimeOffset.UtcNow;
        var lessons = new[]
        {
            new WorkspaceLesson { Text = "旧但置顶", Pinned = true, CreatedAt = now.AddDays(-10) },
            new WorkspaceLesson { Text = "新未置顶", CreatedAt = now.AddDays(-1) },
            new WorkspaceLesson { Text = "更旧未置顶", CreatedAt = now.AddDays(-5) }
        };
        var briefing = SeriesBriefingBuilder.Build(Series(lessons), [], knowledgeLimit: 2);
        Assert.Equal(["旧但置顶", "新未置顶"], briefing.Lessons.Select(lesson => lesson.Text));
    }

    [Fact]
    public void QuestionBankIsOrderedByTimesAsked()
    {
        var now = DateTimeOffset.UtcNow;
        var questions = new[]
        {
            new QuestionBankItem { Question = "少问", Answer = "a", TimesAsked = 1, UpdatedAt = now },
            new QuestionBankItem { Question = "多问", Answer = "b", TimesAsked = 5, UpdatedAt = now.AddDays(-1) }
        };
        var briefing = SeriesBriefingBuilder.Build(Series(questions: questions), [], knowledgeLimit: 1);
        Assert.Equal("多问", Assert.Single(briefing.QuestionBank).Question);
    }

    [Fact]
    public void KnowledgeCanBeSwitchedOff()
    {
        var lessons = new[] { new WorkspaceLesson { Text = "经验" } };
        var candidates = new[] { new WorkspaceKnowledgeCandidate { Status = WorkspaceKnowledgeStatus.Pending } };
        var briefing = SeriesBriefingBuilder.Build(Series(lessons, candidates: candidates, useKnowledge: false), [Session(IdA, 1)]);
        Assert.False(briefing.KnowledgeEnabled);
        Assert.Empty(briefing.Lessons);
        Assert.Empty(briefing.QuestionBank);
        Assert.Empty(briefing.PendingCandidates);
        Assert.Single(briefing.LatestSessions);
    }

    [Fact]
    public void PendingCandidatesNeedAValidSource()
    {
        var now = DateTimeOffset.UtcNow;
        var candidates = new[]
        {
            new WorkspaceKnowledgeCandidate { Status = WorkspaceKnowledgeStatus.Pending, SourceSessionId = IdA, CreatedAt = now.AddDays(-1) },
            new WorkspaceKnowledgeCandidate { Status = WorkspaceKnowledgeStatus.Pending, SourceSessionId = null, CreatedAt = now.AddDays(-2) },
            new WorkspaceKnowledgeCandidate { Status = WorkspaceKnowledgeStatus.Pending, SourceSessionId = Guid.NewGuid().ToString("D"), CreatedAt = now },
            new WorkspaceKnowledgeCandidate { Status = WorkspaceKnowledgeStatus.Accepted, SourceSessionId = IdA, CreatedAt = now }
        };
        var briefing = SeriesBriefingBuilder.Build(Series(candidates: candidates), []);
        Assert.Equal(2, briefing.PendingCandidates.Count);
        Assert.Equal(now.AddDays(-1), briefing.PendingCandidates[0].CreatedAt);
    }

    [Fact]
    public void ZeroLimitsProduceEmptyLists()
    {
        var briefing = SeriesBriefingBuilder.Build(
            Series([new WorkspaceLesson { Text = "x" }], [new QuestionBankItem { Question = "q", Answer = "a" }]),
            [Session(IdA, 1)], meetingLimit: 0, knowledgeLimit: 0);
        Assert.Empty(briefing.LatestSessions);
        Assert.Empty(briefing.Lessons);
        Assert.Empty(briefing.QuestionBank);
    }
}
