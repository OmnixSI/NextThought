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
    IReadOnlyList<WorkspaceKnowledgeCandidate> PendingCandidates);

public static class SeriesBriefingBuilder
{
    /// <summary>
    /// Builds the briefing for the next meeting: recent sessions newest first, pinned lessons first
    /// then newest, the most-asked questions, and pending candidates whose source still exists.
    /// </summary>
    public static SeriesBriefing Build(WorkspaceSeries series, IReadOnlyList<SessionMeta> sessions,
        int meetingLimit = 3, int knowledgeLimit = 3)
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

        return new SeriesBriefing(series.Id, series.Name, seriesSessions.Count, series.UseKnowledge,
            recent, lessons, questionBank, pending);
    }
}
