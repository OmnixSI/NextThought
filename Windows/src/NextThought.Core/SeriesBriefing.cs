namespace NextThought.Core;

/// <summary>
/// A synchronous local snapshot for continuing a series (port of SeriesBriefing): only meetings
/// in this series, accepted knowledge and pending candidates whose source is still present.
/// </summary>
public sealed record SeriesBriefing(
    string WorkspaceId,
    string WorkspaceName,
    int SessionCount,
    bool KnowledgeEnabled,
    IReadOnlyList<SessionMeta> LatestSessions,
    IReadOnlyList<WorkspaceLesson> Lessons,
    IReadOnlyList<QuestionBankItem> QuestionBank,
    IReadOnlyList<WorkspaceKnowledgeCandidate> PendingCandidates,
    IReadOnlyList<WorkspaceSeriesItem> Decisions,
    IReadOnlyList<WorkspaceSeriesItem> OpenActions,
    IReadOnlyList<WorkspaceSeriesItem> ResolvedActions,
    IReadOnlyList<WorkspaceSeriesItem> StaleItems,
    IReadOnlyList<WorkspaceSeriesChangeCandidate>? PendingSeriesChanges = null);

public static class SeriesBriefingBuilder
{
    /// <summary>
    /// Builds the briefing for the next meeting: recent sessions newest first, pinned lessons first
    /// then newest, the most-asked questions, and pending candidates whose source still exists.
    /// </summary>
    public static SeriesBriefing Build(WorkspaceSeries series, IReadOnlyList<SessionMeta> sessions,
        int meetingLimit = 3, int knowledgeLimit = 3,
        IReadOnlyList<SessionEvidenceReference>? evidenceReferences = null)
    {
        var membership = series.SessionIds.ToHashSet(StringComparer.Ordinal);
        var seriesSessions = sessions
            .Where(session => membership.Contains(session.Id.ToString("D")))
            .OrderByDescending(session => session.StartedAt)
            .ToList();
        var recent = seriesSessions.Take(Math.Max(0, meetingLimit)).ToList();

        var lessons = new List<WorkspaceLesson>();
        var questionBank = new List<QuestionBankItem>();
        if (series.UseKnowledge)
        {
            lessons = series.Lessons.Where(lesson => lesson.Pinned)
                .Concat(series.Lessons.Where(lesson => !lesson.Pinned).OrderByDescending(lesson => lesson.CreatedAt))
                .Take(Math.Max(0, knowledgeLimit))
                .ToList();
            questionBank = series.QuestionBank
                .OrderByDescending(item => item.TimesAsked)
                .ThenByDescending(item => item.UpdatedAt)
                .Take(Math.Max(0, knowledgeLimit))
                .ToList();
        }

        var pending = series.UseKnowledge
            ? series.KnowledgeCandidates
                .Where(candidate => candidate.Status == WorkspaceKnowledgeStatus.Pending
                                    && (candidate.SourceSessionId is null || membership.Contains(candidate.SourceSessionId)))
                .OrderByDescending(candidate => candidate.CreatedAt)
                .ToList()
            : [];

        var active = new List<WorkspaceSeriesItem>();
        var stale = new List<WorkspaceSeriesItem>();
        if (series.UseKnowledge)
        {
            foreach (var item in series.SeriesItems)
            {
                if (item.SourceMissing == true)
                {
                    if (item.IndependentlyRetained == true) active.Add(item);
                    else stale.Add(item);
                    continue;
                }
                if (!membership.Contains(item.SourceSessionId))
                {
                    stale.Add(item with { SourceMissing = true, SourceQuote = null, Location = null });
                    continue;
                }
                if (evidenceReferences is not null && !HasCurrentEvidence(item, evidenceReferences))
                {
                    stale.Add(item);
                    continue;
                }
                active.Add(item);
            }
        }
        stale.Sort((left, right) => right.UpdatedAt.CompareTo(left.UpdatedAt));
        var decisions = active.Where(item => item.Kind == WorkspaceSeriesItem.KindValue.Decision)
            .OrderByDescending(item => item.UpdatedAt).ToList();
        var openActions = active.Where(item => item.Kind == WorkspaceSeriesItem.KindValue.Action
                                               && (item.ActionState is null or WorkspaceSeriesItem.ActionStateValue.Open
                                                   or WorkspaceSeriesItem.ActionStateValue.InProgress))
            .OrderBy(item => item.DueDate is null ? 1 : 0)
            .ThenBy(item => item.DueDate ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.UpdatedAt).ToList();
        var resolvedActions = active.Where(item => item.Kind == WorkspaceSeriesItem.KindValue.Action
                                                   && (item.ActionState is WorkspaceSeriesItem.ActionStateValue.Done
                                                       or WorkspaceSeriesItem.ActionStateValue.Cancelled
                                                       or WorkspaceSeriesItem.ActionStateValue.Superseded))
            .OrderByDescending(item => item.UpdatedAt).ToList();

        var pendingChanges = !series.UseKnowledge
            ? []
            : WorkspaceSeriesChangeMerger.Pending(series).Where(candidate =>
                membership.Contains(candidate.SourceSessionId)
                && (evidenceReferences is null || evidenceReferences.Any(reference =>
                    WorkspaceSeriesChangeMerger.MatchesCurrentEvidence(candidate, reference)))).ToList();
        return new SeriesBriefing(series.Id, series.Name, seriesSessions.Count, series.UseKnowledge,
            recent, lessons, questionBank, pending, decisions, openActions, resolvedActions, stale, pendingChanges);
    }

    /// <summary>A review counts only while it is supported, fresh, and saw the same revisions.</summary>
    private static bool HasCurrentEvidence(WorkspaceSeriesItem item, IReadOnlyList<SessionEvidenceReference> references) =>
        references.Any(reference =>
            reference.Id == item.EvidenceId
            && (reference.SessionId is not { } sessionId
                || string.Equals(sessionId.ToString("D"), item.SourceSessionId, StringComparison.OrdinalIgnoreCase))
            && reference.ReviewDecision == SessionEvidenceDecision.Supported
            && reference.Origin == SessionEvidenceOrigin.TextNotes
            && reference.LineId is not null
            && !reference.ReviewIsStale
            && reference.ClaimRevision == item.ClaimRevision
            && reference.SourceRevision == item.SourceRevision
            && reference.TranscriptSourceRevision == item.TranscriptSourceRevision);

}
