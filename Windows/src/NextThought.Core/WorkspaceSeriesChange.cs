namespace NextThought.Core;

public enum WorkspaceSeriesChangeDecision
{
    Accepted,
    Rejected
}

public sealed record WorkspaceSeriesChangeReviewDetails(string? Owner = null, string? Due = null);

public sealed record WorkspaceSeriesChangeReceipt(
    Guid OperationId, string CandidateId, WorkspaceSeriesChangeDecision Decision,
    DateTimeOffset ReviewedAt, string? ResultItemId);

/// <summary>
/// A reviewed-evidence proposal stays separate from active series state until accepted
/// (port of WorkspaceSeriesChangeCandidate).
/// </summary>
public sealed record WorkspaceSeriesChangeCandidate
{
    public enum StatusValue
    {
        Pending,
        Accepted,
        Rejected,
        Superseded
    }

    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    public WorkspaceSeriesItem.KindValue Kind { get; init; } = WorkspaceSeriesItem.KindValue.Action;
    public string Text { get; init; } = "";
    public string? Owner { get; init; }
    public string? Due { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public string SourceSessionId { get; init; } = "";
    public string EvidenceId { get; init; } = "";
    public string ClaimRevision { get; init; } = "";
    public string SourceRevision { get; init; } = "";
    public string? SourceQuote { get; init; }
    public string? Location { get; init; }
    public string? TranscriptSourceRevision { get; init; }
    public bool SourceMissing { get; init; }
    public StatusValue Status { get; init; } = StatusValue.Pending;
    public WorkspaceSeriesChangeReceipt? Receipt { get; init; }
    public IReadOnlyList<WorkspaceSeriesChangeReceipt>? ReceiptHistory { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Collects reviewed proposals and applies an accept/reject decision to the series.</summary>
public static class WorkspaceSeriesChangeMerger
{
    /// <summary>A proposal counts only while the review is supported, fresh and on the same revisions.</summary>
    public static bool MatchesCurrentEvidence(WorkspaceSeriesChangeCandidate candidate, SessionEvidenceReference reference) =>
        reference.Id == candidate.EvidenceId
        && reference.ReviewDecision == SessionEvidenceDecision.Supported
        && !reference.ReviewIsStale
        && reference.ClaimRevision == candidate.ClaimRevision
        && reference.SourceRevision == candidate.SourceRevision
        && (reference.TranscriptSourceRevision ?? "") == (candidate.TranscriptSourceRevision ?? "");

    /// <summary>Accepts or rejects a pending candidate; accepted ones become series items.</summary>
    public static (WorkspaceSeries Series, WorkspaceSeriesChangeReceipt? Receipt) Apply(
        WorkspaceSeries series, string candidateId, WorkspaceSeriesChangeDecision decision,
        WorkspaceSeriesChangeReviewDetails? reviewedDetails = null, DateTimeOffset? now = null)
    {
        var candidates = series.SeriesChangeCandidates.ToList();
        var index = candidates.FindIndex(candidate => candidate.Id == candidateId);
        if (index < 0 || candidates[index].Status != WorkspaceSeriesChangeCandidate.StatusValue.Pending) return (series, null);
        var candidate = candidates[index];
        var reviewedAt = now ?? DateTimeOffset.UtcNow;
        WorkspaceSeriesItem? item = null;
        if (decision == WorkspaceSeriesChangeDecision.Accepted)
        {
            item = new WorkspaceSeriesItem
            {
                Kind = candidate.Kind,
                Text = candidate.Text,
                SourceSessionId = candidate.SourceSessionId,
                EvidenceId = candidate.EvidenceId,
                ClaimRevision = candidate.ClaimRevision,
                SourceRevision = candidate.SourceRevision,
                SourceQuote = candidate.SourceQuote,
                Location = candidate.Location,
                TranscriptSourceRevision = candidate.TranscriptSourceRevision,
                Owner = reviewedDetails?.Owner ?? candidate.Owner,
                Due = reviewedDetails?.Due ?? candidate.Due,
                DueDate = candidate.DueDate,
                ConfirmedAt = reviewedAt,
                UpdatedAt = reviewedAt
            };
        }
        var receipt = new WorkspaceSeriesChangeReceipt(Guid.NewGuid(), candidate.Id, decision, reviewedAt, item?.Id);
        candidates[index] = candidate with
        {
            Status = decision == WorkspaceSeriesChangeDecision.Accepted
                ? WorkspaceSeriesChangeCandidate.StatusValue.Accepted
                : WorkspaceSeriesChangeCandidate.StatusValue.Rejected,
            Receipt = receipt,
            ReceiptHistory = [.. candidate.ReceiptHistory ?? [], receipt]
        };
        var updated = series with
        {
            SeriesChangeCandidates = candidates,
            SeriesItems = item is null ? series.SeriesItems : [.. series.SeriesItems, item]
        };
        return (updated, receipt);
    }

    public static IReadOnlyList<WorkspaceSeriesChangeCandidate> Pending(WorkspaceSeries series) =>
        series.SeriesChangeCandidates.Where(candidate => candidate.Status == WorkspaceSeriesChangeCandidate.StatusValue.Pending)
            .OrderByDescending(candidate => candidate.CreatedAt).ToList();
}
