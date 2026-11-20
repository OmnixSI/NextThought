using System.Text.Json;

namespace NextThought.Core;

// B4: the workspace merge functions of Backup.swift (mergeLessons, mergeQuestionBank,
// mergeSeriesItems, mergeSeriesCandidates, mergeCandidates and receipt ordering).
public static partial class BackupMerge
{
    private static List<WorkspaceLesson> MergeLessons(IReadOnlyList<WorkspaceLesson> preferred, IReadOnlyList<WorkspaceLesson> other)
    {
        var result = preferred.ToList();
        foreach (var incoming in other)
        {
            var index = result.FindIndex(lesson => SameId(lesson.Id, incoming.Id));
            if (index < 0) { result.Add(incoming); continue; }
            var existing = result[index];
            var existingRevision = Math.Max(1, existing.Revision);
            var incomingRevision = Math.Max(1, incoming.Revision);
            var incomingWins = incomingRevision > existingRevision
                || (incomingRevision == existingRevision && incoming.EffectiveUpdatedAt > existing.EffectiveUpdatedAt);
            var current = incomingWins ? incoming : existing;
            var fallback = incomingWins ? existing : incoming;
            var deleted = (current.SourceMissingSessionIds ?? [])
                .Concat(fallback.SourceMissingSessionIds ?? [])
                .Concat(current.History.SelectMany(revision => revision.SourceMissingSessionIds ?? []))
                .Concat(fallback.History.SelectMany(revision => revision.SourceMissingSessionIds ?? []))
                .Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
            var history = current.History.ToList();
            foreach (var revision in fallback.History)
                if (!history.Any(existingRevision => SameRevision(existingRevision, revision))) history.Add(revision);
            var fallbackCurrent = WorkspaceLessonHistory.CurrentRevision(fallback);
            if (!SameRevision(fallbackCurrent, WorkspaceLessonHistory.CurrentRevision(current))
                && !history.Any(revision => SameRevision(revision, fallbackCurrent))) history.Add(fallbackCurrent);
            history = history.Select(revision => RemoveDeletedSources(revision, deleted)).ToList();
            var unique = new List<WorkspaceLessonRevision>();
            foreach (var revision in history)
            {
                var existingIndex = unique.FindIndex(candidate => SameRevision(candidate, revision));
                if (existingIndex < 0) unique.Add(revision);
                else if (revision.RecordedAt < unique[existingIndex].RecordedAt) unique[existingIndex] = revision;
            }
            var item = current;
            if (current.Text == fallback.Text)
            {
                var sources = WorkspaceLessonHistory.AllSourceIds(current)
                    .Union(WorkspaceLessonHistory.AllSourceIds(fallback))
                    .Where(id => !deleted.Contains(id)).ToHashSet(StringComparer.Ordinal);
                var removed = WorkspaceLessonHistory.AllSourceIds(current)
                    .Union(WorkspaceLessonHistory.AllSourceIds(fallback))
                    .Where(deleted.Contains).ToHashSet(StringComparer.Ordinal);
                item = item with
                {
                    SourceSessionIds = sources.Count == 0 ? null : SortedSources(sources),
                    SourceSessionId = current.SourceSessionId is { } legacy && sources.Contains(legacy)
                        ? legacy : SortedSources(sources).FirstOrDefault(),
                    SourceMissingSessionIds = SortedOrNull(
                        (current.SourceMissingSessionIds ?? []).Concat(fallback.SourceMissingSessionIds ?? []).Concat(removed)),
                    IndependentlyRetained = sources.Count == 0 && (current.IndependentlyRetained || fallback.IndependentlyRetained)
                };
            }
            else
            {
                var currentSources = WorkspaceLessonHistory.AllSourceIds(current);
                var removed = currentSources.Where(deleted.Contains).ToHashSet(StringComparer.Ordinal);
                var sources = currentSources.Where(id => !deleted.Contains(id)).ToHashSet(StringComparer.Ordinal);
                item = item with
                {
                    SourceSessionIds = sources.Count == 0 ? null : SortedSources(sources),
                    SourceSessionId = current.SourceSessionId is { } legacy && sources.Contains(legacy)
                        ? legacy : SortedSources(sources).FirstOrDefault(),
                    SourceMissingSessionIds = SortedOrNull((current.SourceMissingSessionIds ?? []).Concat(removed)),
                    IndependentlyRetained = sources.Count == 0 && current.IndependentlyRetained
                };
            }
            var currentRevision = WorkspaceLessonHistory.CurrentRevision(item);
            unique.RemoveAll(revision => SameRevision(revision, currentRevision));
            item = item with
            {
                History = unique.OrderBy(revision => revision.Revision)
                    .ThenBy(revision => revision.RecordedAt).ToList(),
                CreatedAt = current.CreatedAt < incoming.CreatedAt ? current.CreatedAt : incoming.CreatedAt
            };
            result[index] = item;
        }
        return result;
    }

    private static bool SameRevision(WorkspaceLessonRevision left, WorkspaceLessonRevision right) =>
        left.Revision == right.Revision && left.Text == right.Text
        && left.SourceSessionId == right.SourceSessionId
        && SameIds(left.SourceSessionIds, right.SourceSessionIds)
        && SameIds(left.SourceMissingSessionIds, right.SourceMissingSessionIds);

    private static bool SameIds(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
        (left ?? []).OrderBy(id => id, StringComparer.Ordinal)
            .SequenceEqual((right ?? []).OrderBy(id => id, StringComparer.Ordinal));

    private static WorkspaceLessonRevision RemoveDeletedSources(WorkspaceLessonRevision revision, IReadOnlySet<string> deleted)
    {
        var sources = WorkspaceLessonHistory.AllSourceIds(revision);
        var missing = sources.Where(deleted.Contains).ToHashSet(StringComparer.Ordinal);
        if (missing.Count == 0) return revision;
        var remaining = sources.Where(id => !deleted.Contains(id)).ToHashSet(StringComparer.Ordinal);
        return revision with
        {
            SourceSessionIds = remaining.Count == 0 ? null : SortedSources(remaining),
            SourceSessionId = revision.SourceSessionId is { } legacy && remaining.Contains(legacy)
                ? legacy : SortedSources(remaining).FirstOrDefault(),
            SourceMissingSessionIds = SortedOrNull((revision.SourceMissingSessionIds ?? []).Concat(missing))
        };
    }

    private static List<QuestionBankItem> MergeQuestionBank(IReadOnlyList<QuestionBankItem> preferred, IReadOnlyList<QuestionBankItem> other)
    {
        var result = preferred.ToList();
        foreach (var incoming in other)
        {
            var index = result.FindIndex(question => SameId(question.Id, incoming.Id));
            if (index < 0) { result.Add(incoming); continue; }
            var existing = result[index];
            var existingRevision = existing.AnswerRevision ?? 0;
            var incomingRevision = incoming.AnswerRevision ?? 0;
            var existingWins = existing.UpdatedAt > incoming.UpdatedAt
                || (existing.UpdatedAt == incoming.UpdatedAt && existingRevision >= incomingRevision);
            var current = existingWins ? existing : incoming;
            var fallback = existingWins ? incoming : existing;
            var sources = new HashSet<string>(current.SourceSessionIds ?? [], StringComparer.Ordinal);
            var fallbackSources = new HashSet<string>(fallback.SourceSessionIds ?? [], StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(current.SourceSessionId)) sources.Add(current.SourceSessionId);
            if (!string.IsNullOrWhiteSpace(fallback.SourceSessionId)) fallbackSources.Add(fallback.SourceSessionId);
            sources.UnionWith(fallbackSources);
            var history = current.AnswerHistory?.ToList() ?? [];
            var historyIds = history.Select(revision => revision.Id).ToHashSet(StringComparer.Ordinal);
            history.AddRange((fallback.AnswerHistory ?? []).Where(revision => historyIds.Add(revision.Id)));
            var item = current with
            {
                SourceSessionId = current.SourceSessionId ?? fallback.SourceSessionId,
                SourceSessionIds = sources.Count == 0 ? null : SortedSources(sources),
                TimesAsked = Math.Max(Math.Max(current.TimesAsked, fallback.TimesAsked), sources.Count)
            };
            if (current.Answer != fallback.Answer)
            {
                item = item with { AnswerSourceSessionId = current.KnownAnswerSourceSessionId };
                var losing = new WorkspaceAnswerRevision
                {
                    Answer = fallback.Answer,
                    RecordedAt = fallback.UpdatedAt,
                    SourceSessionId = fallback.KnownAnswerSourceSessionId
                };
                if (!history.Any(revision => revision.Answer == losing.Answer
                    && revision.RecordedAt == losing.RecordedAt
                    && revision.SourceSessionId == losing.SourceSessionId)) history.Add(losing);
            }
            history.Sort((left, right) => left.RecordedAt == right.RecordedAt
                ? string.CompareOrdinal(left.Id, right.Id) : left.RecordedAt.CompareTo(right.RecordedAt));
            item = item with { AnswerHistory = history.Count == 0 ? null : history };
            if (current.Answer == fallback.Answer)
                item = item with
                {
                    AnswerSourceSessionId = (current.AnswerRevision ?? 0) > 0
                        ? current.AnswerSourceSessionId
                        : current.KnownAnswerSourceSessionId ?? fallback.KnownAnswerSourceSessionId
                };
            var diverged = current.Question != fallback.Question || current.Answer != fallback.Answer;
            var revision = Math.Max(existingRevision, incomingRevision);
            item = item with
            {
                AnswerRevision = diverged ? revision + 1
                    : existing.AnswerRevision is null && incoming.AnswerRevision is null ? null : revision
            };
            result[index] = item;
        }
        return result;
    }

    private static List<WorkspaceKnowledgeCandidate> MergeCandidates(
        IReadOnlyList<WorkspaceKnowledgeCandidate> preferred, IReadOnlyList<WorkspaceKnowledgeCandidate> other)
    {
        var result = preferred.ToList();
        foreach (var incoming in other)
        {
            var index = result.FindIndex(candidate => SameId(candidate.Id, incoming.Id));
            if (index < 0) { result.Add(incoming); continue; }
            var existing = result[index];
            bool Accepted(WorkspaceKnowledgeCandidate candidate) =>
                candidate.Status == WorkspaceKnowledgeStatus.Accepted
                || (candidate.Receipt is { } receipt && receipt.Decision != WorkspaceKnowledgeDecision.Reject);
            if (Accepted(existing) || Accepted(incoming))
            {
                var accepted = new[] { existing, incoming }.Where(Accepted)
                    .OrderBy(candidate => candidate.Receipt?.ReviewedAt ?? candidate.CreatedAt).Last();
                result[index] = accepted;
            }
            else if (existing.Status == WorkspaceKnowledgeStatus.Rejected || incoming.Status == WorkspaceKnowledgeStatus.Rejected)
            {
                result[index] = new[] { existing, incoming }.Where(candidate => candidate.Status == WorkspaceKnowledgeStatus.Rejected)
                    .OrderBy(candidate => candidate.Receipt?.ReviewedAt ?? candidate.CreatedAt).Last();
            }
            else
            {
                var pending = existing;
                if (existing.BaseQuestionId != incoming.BaseQuestionId || existing.BaseAnswerRevision != incoming.BaseAnswerRevision)
                    pending = pending with { BaseQuestionId = null, BaseAnswerRevision = null };
                result[index] = pending;
            }
        }
        return result;
    }

    private sealed record SeriesItemKey(string ItemId, string SourceId)
    {
        public bool Equals(SeriesItemKey? other) => other is not null
            && string.Equals(ItemId, other.ItemId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(SourceId, other.SourceId, StringComparison.OrdinalIgnoreCase);
        public override int GetHashCode() => HashCode.Combine(
            ItemId.ToUpperInvariant(), SourceId.ToUpperInvariant());
    }

    private static Dictionary<SeriesItemKey, bool> SeriesItemSourceTombstones(
        IReadOnlyList<WorkspaceSeries> workspaces, IReadOnlyList<WorkspaceSeriesChangeCandidate> candidates)
    {
        var deleted = new HashSet<SeriesItemKey>();
        var retained = new HashSet<SeriesItemKey>();
        foreach (var workspace in workspaces)
        {
            foreach (var tombstone in workspace.SeriesItemSourceTombstones ?? [])
                deleted.Add(new SeriesItemKey(tombstone.ItemId, tombstone.SourceSessionId));
            var deletedSessions = (workspace.SourceDeletionTombstones ?? []).ToHashSet(StringComparer.Ordinal);
            foreach (var item in workspace.SeriesItems)
            {
                var currentKey = new SeriesItemKey(item.Id, item.SourceSessionId);
                if (deletedSessions.Contains(item.SourceSessionId) || item.SourceMissing == true) deleted.Add(currentKey);
                if (item.SourceMissing == true && item.IndependentlyRetained == true) retained.Add(currentKey);
                foreach (var revision in item.History)
                {
                    var key = new SeriesItemKey(item.Id, revision.SourceSessionId);
                    if (deletedSessions.Contains(revision.SourceSessionId) || revision.SourceMissing == true) deleted.Add(key);
                    if (revision.SourceMissing == true) retained.Add(key);
                }
            }
        }
        foreach (var candidate in candidates.Where(candidate => candidate.SourceMissing))
            if (candidate.Receipt?.ResultItemId is { } resultItemId)
                deleted.Add(new SeriesItemKey(resultItemId, candidate.SourceSessionId));
        return deleted.ToDictionary(key => key, key => retained.Contains(key));
    }

    private static List<WorkspaceSeriesItem> MergeSeriesItems(
        IReadOnlyList<WorkspaceSeriesItem> preferred, IReadOnlyList<WorkspaceSeriesItem> other,
        IReadOnlyDictionary<SeriesItemKey, bool> tombstones)
    {
        var result = preferred.ToList();
        foreach (var incoming in other)
        {
            var index = result.FindIndex(item => SameId(item.Id, incoming.Id));
            if (index < 0) { result.Add(incoming); continue; }
            var existing = result[index];
            var existingKey = new SeriesItemKey(existing.Id, existing.SourceSessionId);
            var incomingKey = new SeriesItemKey(incoming.Id, incoming.SourceSessionId);
            var existingDeleted = tombstones.ContainsKey(existingKey);
            var incomingDeleted = tombstones.ContainsKey(incomingKey);
            var existingRetained = tombstones.TryGetValue(existingKey, out var existingRetain) && existingRetain
                && existing.SourceMissing == true && existing.IndependentlyRetained == true;
            var incomingRetained = tombstones.TryGetValue(incomingKey, out var incomingRetain) && incomingRetain
                && incoming.SourceMissing == true && incoming.IndependentlyRetained == true;
            bool currentIsExisting;
            if (existingRetained != incomingRetained)
            {
                var retainedIsExisting = existingRetained;
                var retained = retainedIsExisting ? existing : incoming;
                var otherItem = retainedIsExisting ? incoming : existing;
                var otherKey = retainedIsExisting ? incomingKey : existingKey;
                var otherLive = !tombstones.ContainsKey(otherKey) && otherItem.SourceMissing != true
                    && otherItem.SourceSessionId != retained.SourceSessionId;
                var otherNewer = otherItem.Revision >= retained.Revision;
                currentIsExisting = otherLive && otherNewer ? !retainedIsExisting : retainedIsExisting;
            }
            else if (existingDeleted != incomingDeleted) currentIsExisting = !existingDeleted;
            else currentIsExisting = !(incoming.Revision > existing.Revision
                || (incoming.Revision == existing.Revision && incoming.UpdatedAt > existing.UpdatedAt));
            var current = currentIsExisting ? existing : incoming;
            var fallback = currentIsExisting ? incoming : existing;
            var history = current.History.Concat(fallback.History).ToList();
            if (fallback.Revision < current.Revision
                || (fallback.Revision == current.Revision && !SameItemRevision(fallback.CurrentRevision(), current.CurrentRevision())))
                history.Add(fallback.CurrentRevision());
            var cleaned = new List<WorkspaceSeriesItemRevision>();
            foreach (var revision in history)
            {
                var key = new SeriesItemKey(current.Id, revision.SourceSessionId);
                if (!tombstones.TryGetValue(key, out var retain)) { cleaned.Add(revision); continue; }
                if (retain) cleaned.Add(Scrub(revision, current.Id));
            }
            result[index] = current with { History = UniqueSeriesHistory(cleaned, current.Id) };
        }
        var final = new List<WorkspaceSeriesItem>();
        foreach (var item in result)
        {
            var key = new SeriesItemKey(item.Id, item.SourceSessionId);
            if (!tombstones.TryGetValue(key, out var retain))
            {
                final.Add(item with
                {
                    History = UniqueSeriesHistory(item.History.Select(revision =>
                    {
                        var revisionKey = new SeriesItemKey(item.Id, revision.SourceSessionId);
                        if (!tombstones.TryGetValue(revisionKey, out var revisionRetain)) return revision;
                        return revisionRetain ? Scrub(revision, item.Id) : null;
                    }).Where(revision => revision is not null).Cast<WorkspaceSeriesItemRevision>().ToList(), item.Id)
                });
                continue;
            }
            if (!retain) continue;
            var scrubbed = Scrub(item);
            final.Add(scrubbed with
            {
                IndependentlyRetained = true,
                History = UniqueSeriesHistory(scrubbed.History.Select(revision =>
                {
                    var revisionKey = new SeriesItemKey(item.Id, revision.SourceSessionId);
                    if (!tombstones.TryGetValue(revisionKey, out var revisionRetain)) return revision;
                    return revisionRetain ? Scrub(revision, item.Id) : null;
                }).Where(revision => revision is not null).Cast<WorkspaceSeriesItemRevision>().ToList(), item.Id)
            });
        }
        return final;
    }

    public static WorkspaceSeriesItemRevision CurrentRevision(this WorkspaceSeriesItem item) => new()
    {
        Id = item.RevisionId(),
        Revision = item.Revision, Kind = item.Kind, Text = item.Text, Owner = item.Owner,
        Due = item.Due, DueDate = item.DueDate, ActionState = item.ActionState,
        SourceSessionId = item.SourceSessionId, EvidenceId = item.EvidenceId,
        ClaimRevision = item.ClaimRevision, SourceRevision = item.SourceRevision,
        SourceQuote = item.SourceQuote, Location = item.Location,
        TranscriptSourceRevision = item.TranscriptSourceRevision,
        SourceMissing = item.SourceMissing, RecordedAt = item.UpdatedAt
    };

    private static bool SameItemRevision(WorkspaceSeriesItemRevision left, WorkspaceSeriesItemRevision right) =>
        left.Revision == right.Revision && left.Text == right.Text && left.Kind == right.Kind
        && left.Owner == right.Owner && left.Due == right.Due && left.DueDate == right.DueDate
        && left.ActionState == right.ActionState && left.SourceSessionId == right.SourceSessionId
        && left.EvidenceId == right.EvidenceId && left.ClaimRevision == right.ClaimRevision
        && left.SourceRevision == right.SourceRevision && left.SourceQuote == right.SourceQuote
        && left.Location == right.Location && left.TranscriptSourceRevision == right.TranscriptSourceRevision
        && left.SourceMissing == right.SourceMissing;

    private static WorkspaceSeriesItemRevision Scrub(WorkspaceSeriesItemRevision revision, string itemId) => revision with
    {
        Id = itemId + ":" + revision.Revision,
        EvidenceId = "", ClaimRevision = "", SourceRevision = "", SourceQuote = null,
        Location = null, TranscriptSourceRevision = null, SourceMissing = true
    };

    private static WorkspaceSeriesItem Scrub(WorkspaceSeriesItem item) => item with
    {
        EvidenceId = "", ClaimRevision = "", SourceRevision = "", SourceQuote = null,
        Location = null, TranscriptSourceRevision = null, SourceMissing = true
    };

    private static List<WorkspaceSeriesItemRevision> UniqueSeriesHistory(
        IReadOnlyList<WorkspaceSeriesItemRevision> history, string itemId)
    {
        var byId = new Dictionary<string, WorkspaceSeriesItemRevision>(StringComparer.Ordinal);
        foreach (var revision in history)
        {
            var key = revision.Revision + "\u0000" + revision.Kind + "\u0000" + revision.Text + "\u0000" + revision.Owner
                + "\u0000" + revision.Due + "\u0000" + revision.SourceSessionId + "\u0000" + revision.EvidenceId
                + "\u0000" + revision.ClaimRevision + "\u0000" + revision.SourceRevision + "\u0000" + revision.SourceMissing;
            if (!byId.TryGetValue(key, out var existing) || revision.RecordedAt < existing.RecordedAt)
                byId[key] = revision;
        }
        return byId.Values.OrderBy(revision => revision.Revision)
            .ThenBy(revision => revision.RecordedAt)
            .ThenBy(revision => revision.Id, StringComparer.Ordinal).ToList();
    }

    private static List<WorkspaceSeriesChangeCandidate> MergeSeriesCandidates(
        IReadOnlyList<WorkspaceSeriesChangeCandidate> preferred,
        IReadOnlyList<WorkspaceSeriesChangeCandidate> other)
    {
        var result = preferred.ToList();
        foreach (var incoming in other)
        {
            var index = result.FindIndex(candidate => SameId(candidate.Id, incoming.Id));
            if (index < 0)
            {
                result.Add(incoming.SourceMissing ? TombstoneCandidate(incoming) : incoming);
                continue;
            }
            var existing = result[index];
            if (existing.SourceMissing || incoming.SourceMissing)
            {
                var both = existing.SourceMissing && incoming.SourceMissing;
                var deletionCopy = existing.SourceMissing ? existing : incoming;
                result[index] = TombstoneCandidate(deletionCopy, both ? [existing, incoming] : [deletionCopy]);
                continue;
            }
            var receipts = MergedReceipts([existing, incoming], existing.Id);
            if (receipts.Current is { } current)
            {
                var merged = incoming.Receipt?.OperationId == current.OperationId ? incoming : existing;
                merged = merged with
                {
                    Receipt = current,
                    ReceiptHistory = receipts.History,
                    Status = current.Decision == WorkspaceSeriesChangeDecision.Reject
                        ? WorkspaceSeriesChangeCandidate.StatusValue.Rejected
                        : WorkspaceSeriesChangeCandidate.StatusValue.Accepted
                };
                result[index] = merged;
            }
            else if (incoming.Status == WorkspaceSeriesChangeCandidate.StatusValue.Superseded
                && existing.Status == WorkspaceSeriesChangeCandidate.StatusValue.Pending)
                result[index] = incoming;
        }
        return result;
    }

    private static WorkspaceSeriesChangeCandidate TombstoneCandidate(
        WorkspaceSeriesChangeCandidate source, IReadOnlyList<WorkspaceSeriesChangeCandidate>? receiptsSource = null)
    {
        var tombstone = source with
        {
            Text = "", Owner = null, Due = null, DueDate = null,
            EvidenceId = "", ClaimRevision = "", SourceRevision = "",
            SourceQuote = null, Location = null, TranscriptSourceRevision = null,
            SourceMissing = true
        };
        var receipts = MergedReceipts(receiptsSource ?? [source], source.Id);
        tombstone = tombstone with
        {
            Receipt = receipts.Current,
            ReceiptHistory = receipts.History,
            Status = receipts.Current is { } current
                ? current.Decision == WorkspaceSeriesChangeDecision.Reject
                    ? WorkspaceSeriesChangeCandidate.StatusValue.Rejected
                    : WorkspaceSeriesChangeCandidate.StatusValue.Accepted
                : WorkspaceSeriesChangeCandidate.StatusValue.Superseded
        };
        return tombstone;
    }

    private static (WorkspaceSeriesChangeReceipt? Current, IReadOnlyList<WorkspaceSeriesChangeReceipt>? History) MergedReceipts(
        IReadOnlyList<WorkspaceSeriesChangeCandidate> candidates, string candidateId)
    {
        var byOperation = new Dictionary<Guid, WorkspaceSeriesChangeReceipt>();
        foreach (var candidate in candidates)
            foreach (var receipt in (candidate.ReceiptHistory ?? []).Concat(candidate.Receipt is { } single ? [single] : []))
                if (receipt.CandidateId == candidateId)
                    byOperation[receipt.OperationId] = receipt;
        var ordered = byOperation.Values.OrderBy(receipt => receipt, Comparer<WorkspaceSeriesChangeReceipt>.Create(ReceiptPrecedes)).ToList();
        if (ordered.Count == 0) return (null, null);
        var history = ordered.Take(ordered.Count - 1).ToList();
        return (ordered[^1], history.Count == 0 ? null : history);
    }

    private static int ReceiptPrecedes(WorkspaceSeriesChangeReceipt left, WorkspaceSeriesChangeReceipt right)
    {
        var result = left.ReviewedAt.CompareTo(right.ReviewedAt);
        if (result != 0) return result;
        result = string.CompareOrdinal(left.OperationId.ToString("N"), right.OperationId.ToString("N"));
        if (result != 0) return result;
        result = left.Decision.CompareTo(right.Decision);
        if (result != 0) return result;
        result = string.CompareOrdinal(left.ResultItemId ?? "", right.ResultItemId ?? "");
        return result != 0 ? result : string.CompareOrdinal(left.CandidateId, right.CandidateId);
    }

    private static List<WorkspaceSeriesItemSourceTombstone>? MergeSeriesItemSourceTombstones(
        IReadOnlyList<WorkspaceSeriesItemSourceTombstone>? preferred,
        IReadOnlyList<WorkspaceSeriesItemSourceTombstone>? other)
    {
        var list = (preferred ?? []).Concat(other ?? [])
            .GroupBy(entry => (entry.ItemId, entry.SourceSessionId))
            .Select(group => group.First())
            .OrderBy(entry => entry.ItemId, StringComparer.Ordinal)
            .ThenBy(entry => entry.SourceSessionId, StringComparer.Ordinal).ToList();
        return list.Count == 0 && preferred is null && other is null ? null : list;
    }

    private static IReadOnlyList<string> SortedSources(IEnumerable<string> sources) =>
        sources.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).ToList();

    private static IReadOnlyList<string>? SortedOrNull(IEnumerable<string> sources)
    {
        var list = SortedSources(sources);
        return list.Count == 0 ? null : list;
    }
}
