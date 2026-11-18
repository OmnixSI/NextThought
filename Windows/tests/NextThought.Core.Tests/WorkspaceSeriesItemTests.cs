using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for series decisions/actions in the briefing.</summary>
public sealed class WorkspaceSeriesItemTests
{
    private static readonly string SessionId = Guid.NewGuid().ToString("D");

    private static WorkspaceSeries Series(params WorkspaceSeriesItem[] items) => new(
        "series", "组会", DateTimeOffset.UtcNow, [SessionId], []) { SeriesItems = items };

    private static WorkspaceSeriesItem Item(WorkspaceSeriesItem.KindValue kind, string text,
        string sessionId = "", WorkspaceSeriesItem.ActionStateValue? state = null,
        DateTimeOffset? due = null, DateTimeOffset? updated = null,
        bool? sourceMissing = null, bool? retained = null) => new()
    {
        Kind = kind,
        Text = text,
        SourceSessionId = sessionId.Length == 0 ? SessionId : sessionId,
        EvidenceId = "e",
        ClaimRevision = "c",
        SourceRevision = "s",
        SourceQuote = "quote",
        Location = "05:20",
        ActionState = state,
        DueDate = due,
        UpdatedAt = updated ?? DateTimeOffset.UtcNow,
        SourceMissing = sourceMissing,
        IndependentlyRetained = retained
    };

    private static SessionMeta Session() => new()
    {
        Id = Guid.Parse(SessionId),
        Title = "会议",
        StartedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void DecisionsAreNewestFirst()
    {
        var old = Item(WorkspaceSeriesItem.KindValue.Decision, "旧决定", updated: DateTimeOffset.UtcNow.AddDays(-3));
        var recent = Item(WorkspaceSeriesItem.KindValue.Decision, "新决定", updated: DateTimeOffset.UtcNow);
        var briefing = SeriesBriefingBuilder.Build(Series(old, recent), [Session()]);
        Assert.Equal(["新决定", "旧决定"], briefing.Decisions.Select(item => item.Text));
        Assert.Empty(briefing.OpenActions);
        Assert.Empty(briefing.ResolvedActions);
    }

    [Fact]
    public void OpenActionsSortByDueDateThenUpdateAndPutUndatedLast()
    {
        var undated = Item(WorkspaceSeriesItem.KindValue.Action, "无期限", state: WorkspaceSeriesItem.ActionStateValue.Open);
        var later = Item(WorkspaceSeriesItem.KindValue.Action, "后天", state: WorkspaceSeriesItem.ActionStateValue.InProgress,
            due: DateTimeOffset.UtcNow.AddDays(2));
        var earlier = Item(WorkspaceSeriesItem.KindValue.Action, "明天", due: DateTimeOffset.UtcNow.AddDays(1));
        var briefing = SeriesBriefingBuilder.Build(Series(undated, later, earlier), [Session()]);
        Assert.Equal(["明天", "后天", "无期限"], briefing.OpenActions.Select(item => item.Text));
    }

    [Fact]
    public void DoneCancelledAndSupersededActionsAreResolved()
    {
        var done = Item(WorkspaceSeriesItem.KindValue.Action, "完成", state: WorkspaceSeriesItem.ActionStateValue.Done,
            updated: DateTimeOffset.UtcNow.AddDays(-1));
        var cancelled = Item(WorkspaceSeriesItem.KindValue.Action, "取消", state: WorkspaceSeriesItem.ActionStateValue.Cancelled,
            updated: DateTimeOffset.UtcNow);
        var superseded = Item(WorkspaceSeriesItem.KindValue.Action, "被替代", state: WorkspaceSeriesItem.ActionStateValue.Superseded,
            updated: DateTimeOffset.UtcNow.AddDays(-2));
        var briefing = SeriesBriefingBuilder.Build(Series(done, cancelled, superseded), [Session()]);
        Assert.Equal(3, briefing.ResolvedActions.Count);
        Assert.Equal("取消", briefing.ResolvedActions[0].Text);
        Assert.Empty(briefing.OpenActions);
    }

    [Fact]
    public void DeletedSourceIsStaleUnlessIndependentlyRetained()
    {
        var retained = Item(WorkspaceSeriesItem.KindValue.Decision, "保留", sourceMissing: true, retained: true);
        var orphan = Item(WorkspaceSeriesItem.KindValue.Decision, "待确认", sourceMissing: true, retained: false)
            with { SourceQuote = null, Location = null };
        var briefing = SeriesBriefingBuilder.Build(Series(retained, orphan), []);
        Assert.Single(briefing.Decisions);
        Assert.Equal("保留", briefing.Decisions[0].Text);
        Assert.Single(briefing.MissingSources());
        Assert.Null(briefing.MissingSources()[0].SourceQuote);
        Assert.Null(briefing.MissingSources()[0].Location);
        Assert.True(briefing.MissingSources()[0].SourceMissing);
    }

    [Fact]
    public void EvidenceMustBeSupportedCurrentAndOnTheSameRevisions()
    {
        var item = Item(WorkspaceSeriesItem.KindValue.Decision, "决定") with
        {
            EvidenceId = "e",
            ClaimRevision = "c",
            SourceRevision = "s",
            TranscriptSourceRevision = "t"
        };
        var current = new SessionEvidenceReference("e", "标题", "决定", SessionEvidenceSupport.Quoted,
            SessionEvidenceOrigin.TextNotes, Quote: "原话", ReviewDecision: SessionEvidenceDecision.Supported,
            ClaimRevision: "c", SourceRevision: "s", TranscriptSourceRevision: "t");
        var withEvidence = SeriesBriefingBuilder.Build(Series(item), [Session()], evidenceReferences: [current]);
        Assert.Single(withEvidence.Decisions);
        Assert.Empty(withEvidence.StaleItems);

        var staleReview = current with { ReviewIsStale = true };
        Assert.Empty(SeriesBriefingBuilder.Build(Series(item), [Session()], evidenceReferences: [staleReview]).Decisions);

        var unsupported = current with { ReviewDecision = SessionEvidenceDecision.Unsupported };
        Assert.Empty(SeriesBriefingBuilder.Build(Series(item), [Session()], evidenceReferences: [unsupported]).Decisions);

        var oldRevision = current with { ClaimRevision = "old" };
        Assert.Empty(SeriesBriefingBuilder.Build(Series(item), [Session()], evidenceReferences: [oldRevision]).Decisions);

        var missingEvidence = SeriesBriefingBuilder.Build(Series(item), [Session()], evidenceReferences: []);
        Assert.Empty(missingEvidence.Decisions);
        Assert.Single(missingEvidence.StaleItems);
    }

    [Fact]
    public void RetainedItemsNeedNoEvidenceWhenTheirSourceIsGone()
    {
        var retained = Item(WorkspaceSeriesItem.KindValue.Decision, "保留", sourceMissing: true, retained: true);
        var briefing = SeriesBriefingBuilder.Build(Series(retained), [], evidenceReferences: []);
        Assert.Single(briefing.Decisions);
    }

    [Fact]
    public void ItemsFromSessionsOutsideTheSeriesBecomeStale()
    {
        var unknown = Item(WorkspaceSeriesItem.KindValue.Decision, "别的会议", sessionId: Guid.NewGuid().ToString("D"));
        var briefing = SeriesBriefingBuilder.Build(Series(unknown), [Session()]);
        Assert.Empty(briefing.Decisions);
        Assert.Single(briefing.StaleItems);
    }

    [Fact]
    public void KnowledgeOffHidesItems()
    {
        var series = Series(Item(WorkspaceSeriesItem.KindValue.Decision, "决定")) with { UseKnowledge = false };
        var briefing = SeriesBriefingBuilder.Build(series, [Session()]);
        Assert.Empty(briefing.Decisions);
        Assert.Empty(briefing.StaleItems);
    }

    [Fact]
    public void RevisionsKeepTheirHistory()
    {
        var item = Item(WorkspaceSeriesItem.KindValue.Action, "新文本") with
        {
            Revision = 2,
            History = [new WorkspaceSeriesItemRevision { Text = "旧文本", Revision = 1 }]
        };
        Assert.Equal(2, item.Revision);
        Assert.Equal("旧文本", Assert.Single(item.History).Text);
    }
}

internal static class SeriesBriefingTestExtensions
{
    /// <summary>Stale items are those the builder could not verify or keep.</summary>
    public static IReadOnlyList<WorkspaceSeriesItem> MissingSources(this SeriesBriefing briefing) => briefing.StaleItems;
}
