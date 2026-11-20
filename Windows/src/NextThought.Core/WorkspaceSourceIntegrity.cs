using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>
/// Port of WorkspaceSourceIntegrity.removeMeetings (Workspace.swift:851-1098).
/// Deletes the knowledge that only a removed meeting supported, keeps shared knowledge with
/// rewritten provenance, and stores tombstones so an older backup cannot restore deleted facts.
/// </summary>
public static class WorkspaceSourceIntegrity
{
    private static readonly StringComparer IdOrder = StringComparer.Ordinal;

    public static (WorkspaceSeries Series, bool Changed) RemoveMeetings(
        WorkspaceSeries workspace,
        IReadOnlyCollection<string> deletedSessionIds,
        bool retainAcceptedContent = false,
        DateTimeOffset? now = null)
    {
        var deleted = deletedSessionIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(IdOrder);
        if (deleted.Count == 0) return (workspace, false);
        var recordedAt = now ?? DateTimeOffset.UtcNow;
        var changed = false;

        var tombstones = Sorted((workspace.SourceDeletionTombstones ?? []).Concat(deleted));
        if (!tombstones.SequenceEqual(workspace.SourceDeletionTombstones ?? [], IdOrder)) changed = true;

        var lessons = new List<WorkspaceLesson>();
        foreach (var lesson in workspace.Lessons)
        {
            var updated = lesson;
            var originalSources = AllSources(lesson);
            var removed = originalSources.Intersect(deleted).ToHashSet(IdOrder);
            var remaining = originalSources.Except(deleted).ToHashSet(IdOrder);
            if (removed.Count > 0 && remaining.Count == 0 && !retainAcceptedContent)
            {
                changed = true;
                continue;
            }

            var historyChanged = false;
            var history = new List<WorkspaceLessonRevision>();
            foreach (var revision in lesson.History)
            {
                if (AllSources(revision).Overlaps(deleted) is false) { history.Add(revision); continue; }
                var revisionSources = AllSources(revision);
                var revisionRemaining = revisionSources.Except(deleted).ToHashSet(IdOrder);
                if (revisionRemaining.Count == 0 && !retainAcceptedContent)
                {
                    historyChanged = true;
                    continue;
                }
                var cleaned = revision with
                {
                    SourceSessionIds = revisionRemaining.Count == 0 ? null : Sorted(revisionRemaining),
                    SourceSessionId = revisionRemaining.Contains(revision.SourceSessionId ?? "")
                        ? revision.SourceSessionId
                        : Sorted(revisionRemaining).FirstOrDefault(),
                    SourceMissingSessionIds = Sorted((revision.SourceMissingSessionIds ?? []).Concat(removed.Intersect(revisionSources)))
                };
                historyChanged |= cleaned != revision;
                history.Add(cleaned);
            }

            if (removed.Count > 0)
            {
                updated = WorkspaceLessonHistory.AppendCurrentRevision(updated);
                var cleanedHistory = new List<WorkspaceLessonRevision>();
                foreach (var revision in updated.History)
                {
                    if (AllSources(revision).Overlaps(deleted) is false) { cleanedHistory.Add(revision); continue; }
                    var revisionSources = AllSources(revision);
                    var revisionRemaining = revisionSources.Except(deleted).ToHashSet(IdOrder);
                    cleanedHistory.Add(revision with
                    {
                        SourceSessionIds = revisionRemaining.Count == 0 ? null : Sorted(revisionRemaining),
                        SourceSessionId = revisionRemaining.Contains(revision.SourceSessionId ?? "")
                            ? revision.SourceSessionId
                            : Sorted(revisionRemaining).FirstOrDefault(),
                        SourceMissingSessionIds = Sorted((revision.SourceMissingSessionIds ?? []).Concat(removed.Intersect(revisionSources)))
                    });
                }
                updated = updated with
                {
                    History = cleanedHistory,
                    SourceSessionIds = remaining.Count == 0 ? null : Sorted(remaining),
                    SourceSessionId = lesson.SourceSessionId is { } legacy && remaining.Contains(legacy)
                        ? legacy
                        : Sorted(remaining).FirstOrDefault(),
                    SourceMissingSessionIds = Sorted((lesson.SourceMissingSessionIds ?? []).Concat(removed)),
                    Revision = lesson.Revision + 1,
                    UpdatedAt = recordedAt,
                    IndependentlyRetained = remaining.Count == 0 && retainAcceptedContent
                };
                changed = true;
            }
            else
            {
                updated = updated with { History = history };
                changed |= historyChanged;
            }
            lessons.Add(updated);
        }

        var questionBank = new List<QuestionBankItem>();
        foreach (var item in workspace.QuestionBank)
        {
            var originalSources = SourceIds(item.SourceSessionId, item.SourceSessionIds);
            var removedSources = originalSources.Intersect(deleted).ToHashSet(IdOrder);
            var updated = item;
            if (removedSources.Count > 0)
            {
                var remaining = originalSources.Except(deleted).ToHashSet(IdOrder);
                changed = true;
                if (remaining.Count == 0) continue;
                updated = updated with
                {
                    SourceSessionIds = Sorted(remaining),
                    SourceSessionId = item.SourceSessionId is { } legacy && remaining.Contains(legacy)
                        ? legacy
                        : Sorted(remaining).First(),
                    TimesAsked = Math.Max(remaining.Count, Math.Max(0, item.TimesAsked - removedSources.Count)),
                    UpdatedAt = recordedAt
                };
            }
            if (item.KnownAnswerSourceSessionId is { } answerSource && deleted.Contains(answerSource))
            {
                updated = updated with
                {
                    Answer = "",
                    AnswerSourceSessionId = null,
                    AnswerRevision = (item.AnswerRevision ?? 0) + 1,
                    UpdatedAt = recordedAt
                };
                changed = true;
            }
            if (item.AnswerHistory is { } answerHistory)
            {
                var retained = answerHistory.Where(revision =>
                    revision.SourceSessionId is not { } source || !deleted.Contains(source)).ToList();
                if (!retained.SequenceEqual(answerHistory)) { updated = updated with { AnswerHistory = retained }; changed = true; }
            }
            questionBank.Add(updated);
        }

        var itemTombstones = new HashSet<(string ItemId, string SourceId)>(
            (workspace.SeriesItemSourceTombstones ?? []).Select(entry => (entry.ItemId, SourceId: entry.SourceSessionId)));
        foreach (var item in workspace.SeriesItems)
        {
            if (deleted.Contains(item.SourceSessionId)) itemTombstones.Add((item.Id, item.SourceSessionId));
            foreach (var revision in item.History)
                if (deleted.Contains(revision.SourceSessionId)) itemTombstones.Add((item.Id, revision.SourceSessionId));
        }
        var sortedItemTombstones = itemTombstones
            .OrderBy(entry => entry.ItemId, IdOrder).ThenBy(entry => entry.SourceId, IdOrder)
            .Select(entry => new WorkspaceSeriesItemSourceTombstone { ItemId = entry.ItemId, SourceSessionId = entry.SourceId })
            .ToList();
        if (!SameTombstones(sortedItemTombstones, workspace.SeriesItemSourceTombstones ?? [])) changed = true;

        var seriesItems = new List<WorkspaceSeriesItem>();
        foreach (var item in workspace.SeriesItems)
        {
            var alreadyRetained = item.SourceMissing == true && item.IndependentlyRetained == true;
            var sourceDeleted = deleted.Contains(item.SourceSessionId) && !alreadyRetained;
            if (sourceDeleted && !retainAcceptedContent) { changed = true; continue; }
            var updated = item;
            if (sourceDeleted)
            {
                updated = updated with
                {
                    SourceMissing = true,
                    IndependentlyRetained = true,
                    EvidenceId = "",
                    ClaimRevision = "",
                    SourceRevision = "",
                    SourceQuote = null,
                    Location = null,
                    TranscriptSourceRevision = null,
                    UpdatedAt = recordedAt
                };
                changed = true;
            }
            var retainedHistory = new List<WorkspaceSeriesItemRevision>();
            foreach (var revision in updated.History)
            {
                if (!deleted.Contains(revision.SourceSessionId) || revision.SourceMissing == true) { retainedHistory.Add(revision); continue; }
                changed = true;
                if (!retainAcceptedContent) continue;
                retainedHistory.Add(revision with
                {
                    SourceMissing = true,
                    EvidenceId = "",
                    ClaimRevision = "",
                    SourceRevision = "",
                    SourceQuote = null,
                    Location = null,
                    TranscriptSourceRevision = null
                });
            }
            seriesItems.Add(updated with { History = retainedHistory });
        }

        var knowledgeCandidates = new List<WorkspaceKnowledgeCandidate>();
        foreach (var candidate in workspace.KnowledgeCandidates ?? [])
        {
            var sources = candidate.ContributingSessionIds.ToHashSet(IdOrder);
            if (sources.Count == 0)
            {
                if (candidate.Status == WorkspaceKnowledgeStatus.Pending) { changed = true; continue; }
                knowledgeCandidates.Add(candidate); continue;
            }
            if (!sources.Overlaps(deleted)) { knowledgeCandidates.Add(candidate); continue; }
            changed = true;
            if (candidate.Status == WorkspaceKnowledgeStatus.Pending) continue;
            knowledgeCandidates.Add(candidate with
            {
                Text = "",
                ProposedAnswer = null,
                SourceSessionId = candidate.SourceSessionId is { } source && !deleted.Contains(source) ? source : null,
                TranscriptSourceRevision = null,
                ContributingSources = candidate.ContributingSources?.Where(entry => !deleted.Contains(entry.SessionId)).ToList(),
                BaseQuestionId = null,
                BaseAnswerRevision = null
            });
        }

        var seriesCandidates = new List<WorkspaceSeriesChangeCandidate>();
        foreach (var candidate in workspace.SeriesChangeCandidates)
        {
            if (!deleted.Contains(candidate.SourceSessionId)) { seriesCandidates.Add(candidate); continue; }
            if (candidate.Status == WorkspaceSeriesChangeCandidate.StatusValue.Pending)
            {
                changed = true;
                seriesCandidates.Add(candidate with
                {
                    Text = "", Owner = null, Due = null, DueDate = null,
                    EvidenceId = "", ClaimRevision = "", SourceRevision = "",
                    SourceQuote = null, Location = null, TranscriptSourceRevision = null,
                    SourceMissing = true, Status = WorkspaceSeriesChangeCandidate.StatusValue.Superseded
                });
                continue;
            }
            var requiresScrub = candidate.SourceMissing != true || candidate.Text.Length > 0
                || candidate.Owner is not null || candidate.Due is not null || candidate.DueDate is not null
                || candidate.EvidenceId.Length > 0 || candidate.ClaimRevision.Length > 0
                || candidate.SourceRevision.Length > 0 || candidate.SourceQuote is not null
                || candidate.Location is not null || candidate.TranscriptSourceRevision is not null;
            if (!requiresScrub) { seriesCandidates.Add(candidate); continue; }
            changed = true;
            seriesCandidates.Add(candidate with
            {
                Text = "", Owner = null, Due = null, DueDate = null,
                EvidenceId = "", ClaimRevision = "", SourceRevision = "",
                SourceQuote = null, Location = null, TranscriptSourceRevision = null,
                SourceMissing = true
            });
        }

        if (!changed) return (workspace, false);
        return (workspace with
        {
            Lessons = lessons,
            QuestionBank = questionBank,
            SeriesItems = seriesItems,
            SeriesItemSourceTombstones = sortedItemTombstones.Count == 0 ? null : sortedItemTombstones,
            SourceDeletionTombstones = tombstones,
            KnowledgeCandidates = knowledgeCandidates,
            SeriesChangeCandidates = seriesCandidates
        }, true);
    }

    private static bool SameTombstones(
        IReadOnlyList<WorkspaceSeriesItemSourceTombstone> left,
        IReadOnlyList<WorkspaceSeriesItemSourceTombstone> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.ItemId == pair.Second.ItemId && pair.First.SourceSessionId == pair.Second.SourceSessionId);

    private static IReadOnlySet<string> AllSources(WorkspaceLesson lesson) =>
        SourceIds(lesson.SourceSessionId, lesson.SourceSessionIds);

    private static IReadOnlySet<string> AllSources(WorkspaceLessonRevision revision) =>
        SourceIds(revision.SourceSessionId, revision.SourceSessionIds);

    private static IReadOnlySet<string> SourceIds(string? legacy, IReadOnlyList<string>? all)
    {
        var set = new HashSet<string>(all ?? [], IdOrder);
        if (!string.IsNullOrWhiteSpace(legacy)) set.Add(legacy);
        return set;
    }

    private static IReadOnlyList<string> Sorted(IEnumerable<string> ids) =>
        ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(IdOrder).OrderBy(id => id, IdOrder).ToList();
}
