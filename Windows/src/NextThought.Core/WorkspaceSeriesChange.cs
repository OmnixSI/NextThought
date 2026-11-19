namespace NextThought.Core;

public enum WorkspaceSeriesChangeDecision
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("add")]
    Add,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("replace")]
    Replace,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("reject")]
    Reject
}

public sealed record WorkspaceSeriesChangeReviewDetails(string? Owner = null, string? Due = null);

public sealed record WorkspaceSeriesChangeReceipt(
    [property: System.Text.Json.Serialization.JsonPropertyName("operationID")] Guid OperationId,
    [property: System.Text.Json.Serialization.JsonPropertyName("candidateID")] string CandidateId,
    WorkspaceSeriesChangeDecision Decision,
    DateTimeOffset ReviewedAt,
    [property: System.Text.Json.Serialization.JsonPropertyName("resultItemID")] string? ResultItemId);

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
    public static bool MatchesCurrentEvidence(WorkspaceSeriesChangeCandidate candidate, SessionEvidenceReference reference)
    {
        if (reference.Origin != SessionEvidenceOrigin.TextNotes
            || reference.ReviewDecision != SessionEvidenceDecision.Supported || reference.ReviewIsStale
            || reference.LineId is null || string.IsNullOrEmpty(reference.TranscriptSourceRevision))
            return false;
        if (reference.ClaimRevision != candidate.ClaimRevision || reference.SourceRevision != candidate.SourceRevision
            || reference.TranscriptSourceRevision != (candidate.TranscriptSourceRevision ?? ""))
            return false;
        return reference.Id == candidate.EvidenceId && reference.Claim == candidate.Text
            && reference.Owner == candidate.Owner && reference.Due == candidate.Due
            && reference.DueDate == candidate.DueDate && reference.Quote == candidate.SourceQuote
            && reference.Location == candidate.Location && SeriesKind(reference) == candidate.Kind;
    }

    /// <summary>Only explicitly supported, current note evidence becomes a proposal.</summary>
    public static (WorkspaceSeries Series, IReadOnlyList<string> Added) CollectCandidates(
        WorkspaceSeries series, IReadOnlyList<SessionEvidenceReference> evidence, string sessionId,
        DateTimeOffset? now = null)
    {
        if (!series.UseKnowledge) return (series, []);
        var candidates = series.SeriesChangeCandidates.ToList();
        var added = new List<string>();
        foreach (var reference in evidence)
        {
            if (reference.Origin != SessionEvidenceOrigin.TextNotes
                || reference.ReviewDecision != SessionEvidenceDecision.Supported || reference.ReviewIsStale
                || reference.LineId is null || reference.ClaimRevision is null || reference.SourceRevision is null
                || string.IsNullOrEmpty(reference.TranscriptSourceRevision))
                continue;
            if (SeriesKind(reference) is not { } kind) continue;
            var id = CandidateId(sessionId, reference);
            if (candidates.Any(candidate => candidate.Id == id)) continue;
            for (var index = 0; index < candidates.Count; index++)
                if (candidates[index].Status == WorkspaceSeriesChangeCandidate.StatusValue.Pending
                    && candidates[index].SourceSessionId == sessionId
                    && candidates[index].EvidenceId == reference.Id)
                    candidates[index] = candidates[index] with { Status = WorkspaceSeriesChangeCandidate.StatusValue.Superseded };
            candidates.Add(new WorkspaceSeriesChangeCandidate
            {
                Id = id,
                Kind = kind,
                Text = reference.Claim,
                Owner = reference.Owner,
                Due = reference.Due,
                DueDate = reference.DueDate,
                SourceSessionId = sessionId,
                EvidenceId = reference.Id,
                ClaimRevision = reference.ClaimRevision,
                SourceRevision = reference.SourceRevision,
                SourceQuote = reference.Quote,
                Location = reference.Location,
                TranscriptSourceRevision = reference.TranscriptSourceRevision,
                CreatedAt = now ?? DateTimeOffset.UtcNow
            });
            added.Add(id);
        }
        return added.Count == 0 ? (series, added) : (series with { SeriesChangeCandidates = candidates }, added);
    }

    private static WorkspaceSeriesItem.KindValue? SeriesKind(SessionEvidenceReference reference)
    {
        if (reference.Kind == NotesSectionKind.Actions) return WorkspaceSeriesItem.KindValue.Action;
        if (reference.Kind != NotesSectionKind.Items) return null;
        var components = reference.Id.Split(':');
        var section = components.Length > 1 ? components[1].ToLowerInvariant() : "";
        var title = reference.Title.Trim().ToLowerInvariant();
        return section is "decisions" or "decision" || title is "决定" or "decisions"
            ? WorkspaceSeriesItem.KindValue.Decision
            : null;
    }

    private static string CandidateId(string sessionId, SessionEvidenceReference reference)
    {
        var raw = string.Join("\u0000", sessionId, reference.Id, reference.ClaimRevision, reference.SourceRevision,
            reference.TranscriptSourceRevision, reference.Claim, reference.Owner, reference.Due);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)))
            .ToLowerInvariant()[..24];
    }

    /// <summary>Adds, replaces or rejects a pending candidate; a replace checks the item revision.</summary>
    public static (WorkspaceSeries Series, WorkspaceSeriesChangeReceipt? Receipt) Apply(
        WorkspaceSeries series, string candidateId, WorkspaceSeriesChangeDecision decision,
        WorkspaceSeriesChangeReviewDetails? reviewedDetails = null, string? targetItemId = null,
        int? expectedRevision = null, DateTimeOffset? now = null)
    {
        var candidates = series.SeriesChangeCandidates.ToList();
        var index = candidates.FindIndex(candidate => candidate.Id == candidateId);
        if (index < 0 || candidates[index].Status != WorkspaceSeriesChangeCandidate.StatusValue.Pending) return (series, null);
        var candidate = candidates[index];
        if (decision != WorkspaceSeriesChangeDecision.Reject && string.IsNullOrEmpty(candidate.TranscriptSourceRevision))
            return (series, null);
        var reviewedAt = now ?? DateTimeOffset.UtcNow;
        var items = series.SeriesItems.ToList();
        string? resultItemId = null;
        if (decision == WorkspaceSeriesChangeDecision.Add)
        {
            var item = new WorkspaceSeriesItem
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
                ActionState = candidate.Kind == WorkspaceSeriesItem.KindValue.Action
                    ? WorkspaceSeriesItem.ActionStateValue.Open
                    : null,
                ConfirmedAt = reviewedAt,
                UpdatedAt = reviewedAt
            };
            items.Add(item);
            resultItemId = item.Id;
        }
        else if (decision == WorkspaceSeriesChangeDecision.Replace)
        {
            if (targetItemId is null || expectedRevision is null) return (series, null);
            var itemIndex = items.FindIndex(item => item.Id == targetItemId);
            if (itemIndex < 0 || items[itemIndex].Kind != candidate.Kind || items[itemIndex].Revision != expectedRevision)
                return (series, null);
            var item = items[itemIndex];
            var history = item.History.ToList();
            history.Add(new WorkspaceSeriesItemRevision
            {
                Text = item.Text, Owner = item.Owner, Due = item.Due, DueDate = item.DueDate,
                ActionState = item.ActionState, Revision = item.Revision, At = item.UpdatedAt
            });
            items[itemIndex] = item with
            {
                Text = candidate.Text,
                Owner = reviewedDetails?.Owner ?? candidate.Owner,
                Due = reviewedDetails?.Due ?? candidate.Due,
                DueDate = candidate.DueDate,
                SourceSessionId = candidate.SourceSessionId,
                EvidenceId = candidate.EvidenceId,
                ClaimRevision = candidate.ClaimRevision,
                SourceRevision = candidate.SourceRevision,
                SourceQuote = candidate.SourceQuote,
                Location = candidate.Location,
                TranscriptSourceRevision = candidate.TranscriptSourceRevision,
                SourceMissing = false,
                IndependentlyRetained = false,
                ActionState = candidate.Kind == WorkspaceSeriesItem.KindValue.Action
                    ? WorkspaceSeriesItem.ActionStateValue.Open
                    : item.ActionState,
                Revision = item.Revision + 1,
                History = history,
                ConfirmedAt = reviewedAt,
                UpdatedAt = reviewedAt
            };
            resultItemId = item.Id;
        }
        var receipt = new WorkspaceSeriesChangeReceipt(Guid.NewGuid(), candidate.Id, decision, reviewedAt, resultItemId);
        candidates[index] = candidate with
        {
            Status = decision == WorkspaceSeriesChangeDecision.Reject
                ? WorkspaceSeriesChangeCandidate.StatusValue.Rejected
                : WorkspaceSeriesChangeCandidate.StatusValue.Accepted,
            Receipt = receipt,
            ReceiptHistory = [.. candidate.ReceiptHistory ?? [], receipt]
        };
        var updated = series with
        {
            SeriesChangeCandidates = candidates,
            SeriesItems = items
        };
        return (updated, receipt);
    }

    public static IReadOnlyList<WorkspaceSeriesChangeCandidate> Pending(WorkspaceSeries series) =>
        series.SeriesChangeCandidates.Where(candidate => candidate.Status == WorkspaceSeriesChangeCandidate.StatusValue.Pending)
            .OrderByDescending(candidate => candidate.CreatedAt).ToList();
}
