namespace NextThought.Core;

public sealed record WorkspaceDeletionResult(
    WorkspaceSeries Series,
    int RemovedLessons,
    int RemovedCandidates,
    int MarkedItems,
    int RemovedSessions,
    int RemovedQuestions = 0);

/// <summary>
/// Removes one meeting from a series and keeps the series' knowledge honest: lessons lose a
/// deleted source (and disappear when nothing else supports them), pending candidates sourced by
/// it are dropped, and accepted decisions/actions keep their text but are marked for re-confirmation.
/// </summary>
public static class WorkspaceSessionDeletion
{
    public static WorkspaceDeletionResult Apply(WorkspaceSeries series, string sessionId, bool retainLessons = false)
    {
        if (!series.SessionIds.Contains(sessionId))
            return new WorkspaceDeletionResult(series, 0, 0, 0, 0);

        var tombstones = (series.SourceDeletionTombstones ?? []).ToList();
        if (!tombstones.Contains(sessionId)) tombstones.Add(sessionId);

        var lessons = new List<WorkspaceLesson>();
        var removedLessons = 0;
        foreach (var lesson in series.Lessons)
        {
            var single = lesson.SourceSessionId == sessionId;
            var sources = lesson.SourceSessionIds ?? [];
            var affected = single || sources.Contains(sessionId);
            if (!affected)
            {
                lessons.Add(lesson);
                continue;
            }
            if (retainLessons)
            {
                var missing = (lesson.SourceMissingSessionIds ?? []).ToList();
                if (!missing.Contains(sessionId)) missing.Add(sessionId);
                lessons.Add(lesson with
                {
                    SourceMissingSessionIds = missing,
                    IndependentlyRetained = true,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                continue;
            }
            var remaining = sources.Where(id => id != sessionId).ToList();
            if (remaining.Count == 0)
            {
                removedLessons++;
                continue;
            }
            lessons.Add(lesson with
            {
                SourceSessionId = remaining[0],
                SourceSessionIds = remaining,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        var candidates = series.KnowledgeCandidates.Where(candidate => candidate.SourceSessionId != sessionId).ToList();
        var removedCandidates = series.KnowledgeCandidates.Count - candidates.Count;
        var items = new List<WorkspaceSeriesItem>();
        var marked = 0;
        foreach (var item in series.SeriesItems)
        {
            if (item.SourceSessionId != sessionId)
            {
                items.Add(item);
                continue;
            }
            marked++;
            items.Add(item with { SourceMissing = true, SourceQuote = null, Location = null, UpdatedAt = DateTimeOffset.UtcNow });
        }

        var questions = new List<QuestionBankItem>();
        var removedQuestions = 0;
        foreach (var question in series.QuestionBank)
        {
            var sources = question.SourceSessionIds ?? (question.SourceSessionId is { } source ? [source] : []);
            if (!sources.Contains(sessionId)) { questions.Add(question); continue; }
            var remaining = sources.Where(id => id != sessionId).ToList();
            if (remaining.Count == 0) { removedQuestions++; continue; }
            questions.Add(question with
            {
                SourceSessionId = remaining[0],
                SourceSessionIds = remaining,
                AnswerSourceSessionId = question.AnswerSourceSessionId == sessionId ? null : question.AnswerSourceSessionId,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        var result = series with
        {
            SessionIds = series.SessionIds.Where(id => id != sessionId).ToList(),
            Lessons = lessons,
            QuestionBank = questions,
            KnowledgeCandidates = candidates,
            SeriesItems = items,
            SourceDeletionTombstones = tombstones
        };
        return new WorkspaceDeletionResult(result, removedLessons, removedCandidates, marked, 1, removedQuestions);
    }
}
