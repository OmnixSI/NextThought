using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for reviewed series-change proposals.</summary>
public sealed class WorkspaceSeriesChangeTests
{
    private static readonly Guid SessionId = Guid.NewGuid();

    private static WorkspaceSeriesChangeCandidate Candidate() => new()
    {
        Id = "c1",
        Kind = WorkspaceSeriesItem.KindValue.Action,
        Text = "补充压测",
        Owner = "小王",
        Due = "下周五",
        SourceSessionId = SessionId.ToString("D"),
        EvidenceId = "e1",
        ClaimRevision = "c1",
        SourceRevision = "s1",
        SourceQuote = "补充压测",
        Location = "12:00",
        TranscriptSourceRevision = "t1"
    };

    private static WorkspaceSeries Series(params WorkspaceSeriesChangeCandidate[] candidates) =>
        new("w", "系列", DateTimeOffset.UtcNow, [SessionId.ToString("D")], []) { SeriesChangeCandidates = candidates };

    private static SessionEvidenceReference Evidence(bool supported = true, bool stale = false) => new(
        "e1", "标题", "补充压测", SessionEvidenceSupport.Quoted, SessionEvidenceOrigin.TextNotes,
        ReviewDecision: supported ? SessionEvidenceDecision.Supported : SessionEvidenceDecision.Unsupported,
        Quote: "补充压测", Location: "12:00", LineId: Guid.NewGuid(), Kind: NotesSectionKind.Actions, Owner: "小王", Due: "下周五",
        ReviewIsStale: stale, ClaimRevision: "c1", SourceRevision: "s1", TranscriptSourceRevision: "t1");

    [Fact]
    public void AcceptedCandidatesBecomeSeriesItemsWithAReceipt()
    {
        var series = Series(Candidate());
        var (updated, receipt) = WorkspaceSeriesChangeMerger.Apply(series, "c1",
            WorkspaceSeriesChangeDecision.Accepted, new WorkspaceSeriesChangeReviewDetails("小李", "本周五"));
        Assert.NotNull(receipt);
        Assert.Equal(WorkspaceSeriesChangeDecision.Accepted, receipt!.Decision);
        Assert.Equal("c1", receipt.CandidateId);
        var item = Assert.Single(updated.SeriesItems);
        Assert.Equal("补充压测", item.Text);
        Assert.Equal("小李", item.Owner);
        Assert.Equal("本周五", item.Due);
        Assert.Equal(receipt.ResultItemId, item.Id);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Accepted, updated.SeriesChangeCandidates[0].Status);
        Assert.Equal(receipt, updated.SeriesChangeCandidates[0].Receipt);
        Assert.Single(updated.SeriesChangeCandidates[0].ReceiptHistory!);
    }

    [Fact]
    public void RejectedCandidatesLeaveTheSeriesUnchangedButRecordTheDecision()
    {
        var series = Series(Candidate());
        var (updated, receipt) = WorkspaceSeriesChangeMerger.Apply(series, "c1", WorkspaceSeriesChangeDecision.Rejected);
        Assert.NotNull(receipt);
        Assert.Empty(updated.SeriesItems);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Rejected, updated.SeriesChangeCandidates[0].Status);
        Assert.Null(receipt!.ResultItemId);
    }

    [Fact]
    public void ACandidateCanOnlyBeReviewedOnce()
    {
        var (updated, receipt) = WorkspaceSeriesChangeMerger.Apply(Series(Candidate()), "c1",
            WorkspaceSeriesChangeDecision.Accepted);
        Assert.NotNull(receipt);
        var (again, second) = WorkspaceSeriesChangeMerger.Apply(updated, "c1", WorkspaceSeriesChangeDecision.Rejected);
        Assert.Null(second);
        Assert.Single(again.SeriesItems);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Accepted, again.SeriesChangeCandidates[0].Status);
        Assert.Null(WorkspaceSeriesChangeMerger.Apply(Series(Candidate()), "missing", WorkspaceSeriesChangeDecision.Accepted).Receipt);
    }

    [Fact]
    public void BriefingsListOnlyPendingCandidatesWithCurrentEvidence()
    {
        var series = Series(Candidate());
        var session = new SessionMeta { Id = SessionId, Title = "会议", StartedAt = DateTimeOffset.UtcNow };
        var current = SeriesBriefingBuilder.Build(series, [session], evidenceReferences: [Evidence()]);
        Assert.Single(current.PendingSeriesChanges!);
        var stale = SeriesBriefingBuilder.Build(series, [session], evidenceReferences: [Evidence(stale: true)]);
        Assert.Empty(stale.PendingSeriesChanges!);
        var unsupported = SeriesBriefingBuilder.Build(series, [session], evidenceReferences: [Evidence(supported: false)]);
        Assert.Empty(unsupported.PendingSeriesChanges!);
        Assert.Empty(SeriesBriefingBuilder.Build(series with { UseKnowledge = false }, [session]).PendingSeriesChanges!);
    }

    [Fact]
    public void SupportedCurrentNotesBecomeOneProposalPerRevision()
    {
        var series = Series();
        var reference = Evidence() with { Id = "notes:actions:0", Title = "待办", Kind = NotesSectionKind.Actions };
        var (once, added) = WorkspaceSeriesChangeMerger.CollectCandidates(series, [reference], SessionId.ToString("D"));
        Assert.Single(added);
        var candidate = Assert.Single(once.SeriesChangeCandidates);
        Assert.Equal(WorkspaceSeriesItem.KindValue.Action, candidate.Kind);
        Assert.Equal("补充压测", candidate.Text);
        Assert.Equal("小王", candidate.Owner);
        Assert.Equal(added[0], candidate.Id);
        Assert.Empty(once.SeriesItems);

        var (twice, again) = WorkspaceSeriesChangeMerger.CollectCandidates(once, [reference], SessionId.ToString("D"));
        Assert.Empty(again);
        Assert.Single(twice.SeriesChangeCandidates);

        var regenerated = reference with { Owner = "小李" };
        var (updated, newIds) = WorkspaceSeriesChangeMerger.CollectCandidates(twice, [regenerated], SessionId.ToString("D"));
        Assert.Single(newIds);
        Assert.Equal(2, updated.SeriesChangeCandidates.Count);
        Assert.Single(updated.SeriesChangeCandidates, item => item.Status == WorkspaceSeriesChangeCandidate.StatusValue.Pending);
        Assert.Single(updated.SeriesChangeCandidates, item => item.Status == WorkspaceSeriesChangeCandidate.StatusValue.Superseded);

        Assert.Empty(WorkspaceSeriesChangeMerger.CollectCandidates(series with { UseKnowledge = false }, [reference],
            SessionId.ToString("D")).Added);
    }

    [Fact]
    public void PendingOrdersNewestFirstAndIgnoresReviewed()
    {
        var older = Candidate() with { Id = "c0", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var newer = Candidate() with { Id = "c2", CreatedAt = DateTimeOffset.UtcNow };
        var reviewed = Candidate() with { Id = "c3", Status = WorkspaceSeriesChangeCandidate.StatusValue.Accepted };
        var pending = WorkspaceSeriesChangeMerger.Pending(Series(older, newer, reviewed));
        Assert.Equal(["c2", "c0"], pending.Select(candidate => candidate.Id));
    }
}
