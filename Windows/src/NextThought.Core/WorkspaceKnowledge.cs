using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace NextThought.Core;

// Port of the knowledge half of Workspace.swift: lessons with provenance history,
// the question bank, unreviewed review candidates and KnowledgeMerger. The series
// record lives in Workspace.cs; everything here hangs off it.

/// <summary>An immutable version of an accepted lesson (WorkspaceLessonRevision).</summary>
public sealed record WorkspaceLessonRevision
{
    public string Id { get; init; } = "";

    public int Revision { get; init; }

    public string Text { get; init; } = "";

    [JsonPropertyName("sourceSessionID")]
    public string? SourceSessionId { get; init; }

    [JsonPropertyName("sourceSessionIDs")]
    public IReadOnlyList<string>? SourceSessionIds { get; init; }

    [JsonPropertyName("sourceMissingSessionIDs")]
    public IReadOnlyList<string>? SourceMissingSessionIds { get; init; }

    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>
/// Applies lesson changes while keeping the accepted text and its provenance history
/// (WorkspaceLessonHistory). Records are immutable, so every operation returns a new lesson.
/// </summary>
public static class WorkspaceLessonHistory
{
    /// <summary>Snapshot of the current state, with the deterministic revision id of Workspace.swift.</summary>
    public static WorkspaceLessonRevision CurrentRevision(WorkspaceLesson lesson) => new()
    {
        Id = RevisionIdentifier(lesson),
        Revision = Math.Max(1, lesson.Revision),
        Text = lesson.Text,
        SourceSessionId = lesson.SourceSessionId,
        SourceSessionIds = lesson.SourceSessionIds,
        SourceMissingSessionIds = lesson.SourceMissingSessionIds,
        RecordedAt = lesson.UpdatedAt
    };

    /// <summary>Records the version about to be replaced; a repeated snapshot is not stored twice.</summary>
    public static WorkspaceLesson AppendCurrentRevision(WorkspaceLesson lesson)
    {
        var current = CurrentRevision(lesson);
        if (lesson.History.Any(revision => revision.Id == current.Id)) return lesson;
        return lesson with { History = [.. lesson.History, current] };
    }

    /// <summary>Edits the text; null when nothing changed.</summary>
    public static WorkspaceLesson? EditText(WorkspaceLesson lesson, string text, DateTimeOffset now)
    {
        if (lesson.Text == text) return null;
        var updated = AppendCurrentRevision(lesson) with
        {
            Text = text,
            Revision = lesson.Revision + 1,
            UpdatedAt = now,
            IndependentlyRetained = false
        };
        return updated;
    }

    /// <summary>Adds meeting sources; returns the lesson and whether anything new was counted.</summary>
    public static (WorkspaceLesson Lesson, bool Changed) AddSources(WorkspaceLesson lesson, IReadOnlyCollection<string> newSources, DateTimeOffset now)
    {
        var existing = AllSourceIds(lesson);
        var additions = newSources.Where(source => !existing.Contains(source)).ToHashSet();
        if (additions.Count == 0) return (lesson, false);
        var sources = existing.Union(additions).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var updated = AppendCurrentRevision(lesson) with
        {
            SourceSessionIds = sources,
            SourceSessionId = lesson.SourceSessionId is { } legacy && sources.Contains(legacy) ? legacy : sources.FirstOrDefault(),
            Revision = lesson.Revision + 1,
            UpdatedAt = now,
            IndependentlyRetained = false
        };
        return (updated, true);
    }

    /// <summary>Restores an old snapshot, keeping the replaced version in history.</summary>
    public static WorkspaceLesson? Restore(WorkspaceLesson lesson, string revisionId, DateTimeOffset now)
    {
        var revision = lesson.History.FirstOrDefault(item => item.Id == revisionId);
        if (revision is null) return null;
        var same = lesson.Text == revision.Text
                   && lesson.SourceSessionId == revision.SourceSessionId
                   && SameIds(lesson.SourceSessionIds, revision.SourceSessionIds)
                   && SameIds(lesson.SourceMissingSessionIds, revision.SourceMissingSessionIds);
        if (same) return null;
        var updated = AppendCurrentRevision(lesson) with
        {
            Text = revision.Text,
            SourceSessionId = revision.SourceSessionId,
            SourceSessionIds = revision.SourceSessionIds,
            SourceMissingSessionIds = revision.SourceMissingSessionIds,
            Revision = lesson.Revision + 1,
            UpdatedAt = now,
            IndependentlyRetained = AllSourceIds(revision).Count == 0 && (revision.SourceMissingSessionIds?.Count ?? 0) > 0
        };
        return updated;
    }

    public static IReadOnlySet<string> AllSourceIds(WorkspaceLesson lesson) => AllSourceIds(lesson.SourceSessionId, lesson.SourceSessionIds);

    public static IReadOnlySet<string> AllSourceIds(WorkspaceLessonRevision revision) => AllSourceIds(revision.SourceSessionId, revision.SourceSessionIds);

    private static IReadOnlySet<string> AllSourceIds(string? legacy, IReadOnlyList<string>? many)
    {
        var set = new HashSet<string>(many ?? []);
        if (!string.IsNullOrWhiteSpace(legacy)) set.Add(legacy);
        return set;
    }

    private static bool SameIds(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
        (left ?? []).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual((right ?? []).OrderBy(id => id, StringComparer.Ordinal));

    /// <summary>
    /// A UUID-shaped id derived from the revision's own content, so two devices that hold the
    /// same version agree on it without storing an extra key (WorkspaceLesson.currentRevision).
    /// </summary>
    private static string RevisionIdentifier(WorkspaceLesson lesson)
    {
        var parts = new List<string?>
        {
            lesson.Id.ToLowerInvariant(),
            lesson.Revision.ToString(),
            lesson.Text,
            lesson.SourceSessionId?.ToLowerInvariant(),
            string.Join(",", (lesson.SourceSessionIds ?? []).Select(id => id.ToLowerInvariant()).OrderBy(id => id, StringComparer.Ordinal)),
            string.Join(",", (lesson.SourceMissingSessionIds ?? []).Select(id => id.ToLowerInvariant()).OrderBy(id => id, StringComparer.Ordinal)),
            BitConverter.DoubleToInt64Bits(lesson.UpdatedAt.ToUnixTimeMilliseconds() / 1000.0).ToString("x")
        };
        var material = string.Join("|", parts.Select(part => $"{Encoding.UTF8.GetByteCount(part ?? "")}:{part}"));
        return DigestUuid("workspace-lesson-revision|" + material);
    }

    internal static string DigestUuid(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var bytes = hash[..16].ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        // Swift's UUID(uuid:) takes the 16 bytes in order; new Guid(byte[]) reorders them.
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}

/// <summary>One answer a question has had before (WorkspaceAnswerRevision).</summary>
public sealed record WorkspaceAnswerRevision
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");

    public string Answer { get; init; } = "";

    public DateTimeOffset RecordedAt { get; init; }

    [JsonPropertyName("sourceSessionID")]
    public string? SourceSessionId { get; init; }
}

/// <summary>A question that came up, with the best answer so far (QuestionBankItem).</summary>
public sealed record QuestionBankItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");

    public string Question { get; init; } = "";

    public string Answer { get; init; } = "";

    public int TimesAsked { get; init; } = 1;

    [JsonPropertyName("sourceSessionID")]
    public string? SourceSessionId { get; init; }

    /// <summary>Unique meeting sources; regenerated or retried reviews do not count twice.</summary>
    [JsonPropertyName("sourceSessionIDs")]
    public IReadOnlyList<string>? SourceSessionIds { get; init; }

    /// <summary>Meeting that supplied the current answer; question sources can include many meetings.</summary>
    [JsonPropertyName("answerSourceSessionID")]
    public string? AnswerSourceSessionId { get; init; }

    public IReadOnlyList<WorkspaceAnswerRevision>? AnswerHistory { get; init; }

    public int? AnswerRevision { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Legacy records used the question's source as the answer source. Once the answer has
    /// been edited, a missing answer source means it is user-authored.
    /// </summary>
    [JsonIgnore]
    public string? KnownAnswerSourceSessionId => AnswerSourceSessionId ?? (AnswerRevision is null ? SourceSessionId : null);
}

public enum WorkspaceKnowledgeKind
{
    Lesson,
    Question
}

public enum WorkspaceKnowledgeDecision
{
    Add,
    KeepExisting,
    ReplaceAnswer,
    Reject
}

public enum WorkspaceKnowledgeStatus
{
    Pending,
    Accepted,
    Rejected
}

/// <summary>Record of one reviewed candidate (WorkspaceKnowledgeReceipt).</summary>
public sealed record WorkspaceKnowledgeReceipt
{
    [JsonPropertyName("operationID")]
    public Guid OperationId { get; init; }

    [JsonPropertyName("candidateID")]
    public string CandidateId { get; init; } = "";

    public WorkspaceKnowledgeDecision Decision { get; init; }

    public DateTimeOffset ReviewedAt { get; init; }

    [JsonPropertyName("resultItemID")]
    public string? ResultItemId { get; init; }

    /// <summary>True when the review actually credited a new meeting source.</summary>
    public bool SourceCounted { get; init; }
}

/// <summary>An archived snapshot that contributed to a series-wide review suggestion.</summary>
public sealed record WorkspaceKnowledgeCandidateSource
{
    [JsonPropertyName("sessionID")]
    public string SessionId { get; init; } = "";

    public DateTimeOffset StartedAt { get; init; }

    public string TranscriptSourceRevision { get; init; } = "";

    public string ProjectionRevision { get; init; } = "";

    [JsonIgnore]
    public string Id => SessionId;
}

/// <summary>
/// A generated suggestion stays inert until the user accepts, keeps, replaces or rejects it
/// (WorkspaceKnowledgeCandidate).
/// </summary>
public sealed record WorkspaceKnowledgeCandidate
{
    /// <summary>Deterministic content id: repeating the same summary cannot enqueue it twice.</summary>
    public string Id { get; init; } = "";

    /// <summary>Stable logical target, independent of the suggested answer revision.</summary>
    [JsonPropertyName("logicalItemID")]
    public string LogicalItemId { get; init; } = "";

    public WorkspaceKnowledgeKind Kind { get; init; }

    /// <summary>Lesson text or question text, depending on <see cref="Kind"/>.</summary>
    public string Text { get; init; } = "";

    public string? ProposedAnswer { get; init; }

    [JsonPropertyName("sourceSessionID")]
    public string? SourceSessionId { get; init; }

    /// <summary>Transcript snapshot from the archived summary that produced this candidate.</summary>
    public string? TranscriptSourceRevision { get; init; }

    public IReadOnlyList<WorkspaceKnowledgeCandidateSource>? ContributingSources { get; init; }

    [JsonPropertyName("baseQuestionID")]
    public string? BaseQuestionId { get; init; }

    public int? BaseAnswerRevision { get; init; }

    public WorkspaceKnowledgeStatus Status { get; init; } = WorkspaceKnowledgeStatus.Pending;

    public WorkspaceKnowledgeReceipt? Receipt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>All meetings behind the suggestion, sorted like Workspace.swift does.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> ContributingSessionIds
    {
        get
        {
            var ids = new HashSet<string>(ContributingSources?.Select(source => source.SessionId) ?? []);
            if (!string.IsNullOrWhiteSpace(SourceSessionId)) ids.Add(SourceSessionId);
            return ids.OrderBy(id => id, StringComparer.Ordinal).ToList();
        }
    }

    [JsonIgnore]
    public string? PrimarySourceSessionId => SourceSessionId
        ?? ContributingSources?.Select(source => source.SessionId).OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault();
}

/// <summary>One question-and-answer pair pulled out of a review (KnowledgeMerger.BankEntry).</summary>
public sealed record QuestionBankEntry(string Question, string Answer);

/// <summary>
/// Turns reviewed notes into inert knowledge suggestions and applies a user's decision
/// (KnowledgeMerger in Workspace.swift, lines 1111-1460).
/// </summary>
public static class KnowledgeMerger
{
    /// <summary>Collects lesson and question suggestions from one review.</summary>
    public static (WorkspaceSeries Series, IReadOnlyList<string> Added) CollectCandidates(
        WorkspaceSeries series,
        IReadOnlyList<string> lessons,
        IReadOnlyList<QuestionBankEntry> bank,
        string? sessionId,
        DateTimeOffset? now = null,
        string? transcriptSourceRevision = null,
        IReadOnlyList<WorkspaceKnowledgeCandidateSource>? contributingSources = null,
        string? originId = null)
    {
        var recordedAt = now ?? DateTimeOffset.UtcNow;
        var originIdValue = originId ?? sessionId ?? series.Id;
        var sourceSnapshots = CanonicalSources(contributingSources);
        var sourceManifest = SourceManifest(sourceSnapshots);
        var candidates = (series.KnowledgeCandidates ?? []).ToList();
        var known = candidates.Select(candidate => candidate.Id).ToHashSet(StringComparer.Ordinal);
        var added = new List<string>();

        foreach (var raw in lessons)
        {
            var text = raw.Trim();
            if (text.Length < 4) continue;
            var logicalId = LogicalItemId(WorkspaceKnowledgeKind.Lesson, text);
            var id = CandidateId(originIdValue, sessionId, WorkspaceKnowledgeKind.Lesson, logicalId, "", transcriptSourceRevision, sourceManifest);
            if (!known.Add(id)) continue;
            candidates.Add(new WorkspaceKnowledgeCandidate
            {
                Id = id,
                LogicalItemId = logicalId,
                Kind = WorkspaceKnowledgeKind.Lesson,
                Text = text,
                SourceSessionId = sessionId,
                TranscriptSourceRevision = transcriptSourceRevision,
                ContributingSources = sourceSnapshots.Count == 0 ? null : sourceSnapshots,
                CreatedAt = recordedAt
            });
            added.Add(id);
        }

        foreach (var item in bank)
        {
            var question = item.Question.Trim();
            var answer = item.Answer.Trim();
            if (question.Length < 3 || answer.Length == 0) continue;
            var logicalId = LogicalItemId(WorkspaceKnowledgeKind.Question, question);
            var id = CandidateId(originIdValue, sessionId, WorkspaceKnowledgeKind.Question, logicalId, answer, transcriptSourceRevision, sourceManifest);
            if (!known.Add(id)) continue;
            var match = series.QuestionBank?.FirstOrDefault(entry => SimilarQuestion(entry.Question, question));
            candidates.Add(new WorkspaceKnowledgeCandidate
            {
                Id = id,
                LogicalItemId = logicalId,
                Kind = WorkspaceKnowledgeKind.Question,
                Text = question,
                ProposedAnswer = answer,
                SourceSessionId = sessionId,
                TranscriptSourceRevision = transcriptSourceRevision,
                ContributingSources = sourceSnapshots.Count == 0 ? null : sourceSnapshots,
                BaseQuestionId = match?.Id,
                BaseAnswerRevision = match?.AnswerRevision ?? 0,
                CreatedAt = recordedAt
            });
            added.Add(id);
        }

        var updated = added.Count == 0 ? series : series with { KnowledgeCandidates = candidates };
        return (updated, added);
    }

    /// <summary>B17: the queued candidate still matches the archived summary and the live transcript.</summary>
    public static bool MatchesCurrentSummary(WorkspaceKnowledgeCandidate candidate, SessionSummary summary,
        string sessionId, string liveTranscriptSourceRevision)
    {
        if (candidate.SourceSessionId != sessionId) return false;
        var revision = candidate.TranscriptSourceRevision;
        if (string.IsNullOrEmpty(revision)) return false;
        if (revision != summary.TranscriptSourceRevision || revision != liveTranscriptSourceRevision) return false;
        var generated = new WorkspaceSeries("candidate-check", "候选核查", DateTimeOffset.UtcNow, [], []);
        var bank = (summary.Bank ?? []).Select(entry => new QuestionBankEntry(entry.Question, entry.Answer)).ToList();
        var ids = CollectCandidates(generated, summary.Lessons ?? [], bank, sessionId,
            originId: sessionId, transcriptSourceRevision: revision);
        return ids.Added.Contains(candidate.Id);
    }

    /// <summary>Applies one reviewed candidate. Returns the receipt, or null when the decision is not valid.</summary>
    public static (WorkspaceSeries Series, WorkspaceKnowledgeReceipt? Receipt) ApplyCandidate(
        WorkspaceSeries series,
        string candidateId,
        WorkspaceKnowledgeDecision decision,
        DateTimeOffset now,
        Guid? operationId = null)
    {
        var candidates = (series.KnowledgeCandidates ?? []).ToList();
        var index = candidates.FindIndex(candidate => candidate.Id == candidateId);
        if (index < 0) return (series, null);
        var candidate = candidates[index];
        if (candidate.Receipt is { } existing) return (series, existing);
        if (candidate.Status != WorkspaceKnowledgeStatus.Pending) return (series, null);

        var sources = candidate.ContributingSessionIds;
        if (decision != WorkspaceKnowledgeDecision.Reject && sources.Count == 0) return (series, null);
        var primary = candidate.PrimarySourceSessionId;

        if (decision == WorkspaceKnowledgeDecision.Reject)
        {
            var rejected = new WorkspaceKnowledgeReceipt
            {
                OperationId = operationId ?? Guid.NewGuid(),
                CandidateId = candidate.Id,
                Decision = decision,
                ReviewedAt = now,
                SourceCounted = false
            };
            candidates[index] = candidate with { Status = WorkspaceKnowledgeStatus.Rejected, Receipt = rejected };
            return (series with { KnowledgeCandidates = candidates }, rejected);
        }

        string? resultItemId;
        var sourceCounted = false;
        var lessons = series.Lessons.ToList();
        var bank = (series.QuestionBank ?? []).ToList();

        switch (candidate.Kind)
        {
            case WorkspaceKnowledgeKind.Lesson:
            {
                var matchIndex = lessons.FindIndex(lesson => Similarity(lesson.Text, candidate.Text) >= 0.6);
                if (matchIndex >= 0)
                {
                    if (decision is not (WorkspaceKnowledgeDecision.Add or WorkspaceKnowledgeDecision.KeepExisting)) return (series, null);
                    resultItemId = lessons[matchIndex].Id;
                    var (lesson, changed) = WorkspaceLessonHistory.AddSources(lessons[matchIndex], sources, now);
                    lessons[matchIndex] = lesson;
                    sourceCounted = changed;
                }
                else
                {
                    if (decision != WorkspaceKnowledgeDecision.Add) return (series, null);
                    var id = Guid.NewGuid().ToString("D");
                    lessons.Add(new WorkspaceLesson
                    {
                        Id = id,
                        Text = candidate.Text,
                        SourceSessionId = primary,
                        SourceSessionIds = sources.Count == 0 ? null : sources,
                        SourceMissingSessionIds = null,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    resultItemId = id;
                    sourceCounted = sources.Count > 0;
                }
                break;
            }
            case WorkspaceKnowledgeKind.Question:
            {
                if (string.IsNullOrWhiteSpace(candidate.ProposedAnswer)) return (series, null);
                var proposed = candidate.ProposedAnswer;
                var matchIndex = bank.FindIndex(item => SimilarQuestion(item.Question, candidate.Text));
                if (matchIndex >= 0)
                {
                    if (decision is not (WorkspaceKnowledgeDecision.KeepExisting or WorkspaceKnowledgeDecision.ReplaceAnswer)) return (series, null);
                    var item = bank[matchIndex];
                    if (candidate.BaseQuestionId != item.Id || candidate.BaseAnswerRevision != (item.AnswerRevision ?? 0)) return (series, null);
                    if (decision == WorkspaceKnowledgeDecision.ReplaceAnswer && item.Answer != proposed)
                    {
                        var history = (item.AnswerHistory ?? []).ToList();
                        history.Add(new WorkspaceAnswerRevision
                        {
                            Answer = item.Answer,
                            RecordedAt = item.UpdatedAt,
                            SourceSessionId = item.KnownAnswerSourceSessionId
                        });
                        item = item with
                        {
                            AnswerHistory = history,
                            Answer = proposed,
                            AnswerSourceSessionId = primary,
                            AnswerRevision = (item.AnswerRevision ?? 0) + 1,
                            UpdatedAt = now
                        };
                    }
                    var (updatedItem, counted) = RecordQuestionSources(item, sources);
                    bank[matchIndex] = updatedItem;
                    resultItemId = updatedItem.Id;
                    sourceCounted = counted;
                }
                else
                {
                    if (decision != WorkspaceKnowledgeDecision.Add) return (series, null);
                    var id = Guid.NewGuid().ToString("D");
                    bank.Add(new QuestionBankItem
                    {
                        Id = id,
                        Question = candidate.Text,
                        Answer = proposed,
                        TimesAsked = sources.Count,
                        SourceSessionId = primary,
                        SourceSessionIds = sources.Count == 0 ? null : sources,
                        AnswerSourceSessionId = primary,
                        AnswerRevision = 0,
                        UpdatedAt = now
                    });
                    resultItemId = id;
                    sourceCounted = sources.Count > 0;
                }
                break;
            }
            default:
                return (series, null);
        }

        var receipt = new WorkspaceKnowledgeReceipt
        {
            OperationId = operationId ?? Guid.NewGuid(),
            CandidateId = candidate.Id,
            Decision = decision,
            ReviewedAt = now,
            ResultItemId = resultItemId,
            SourceCounted = sourceCounted
        };
        candidates[index] = candidate with { Status = WorkspaceKnowledgeStatus.Accepted, Receipt = receipt };
        return (series with { Lessons = lessons, QuestionBank = bank, KnowledgeCandidates = candidates }, receipt);
    }

    /// <summary>
    /// Applies a manual edit without letting a stale editor snapshot erase provenance or answer
    /// history. The revision advances when the wording changes too, so an open suggestion cannot
    /// replace an answer for a question the user has since changed.
    /// </summary>
    public static QuestionBankItem EditQuestionBankItem(QuestionBankItem item, string question, string answer, DateTimeOffset now)
    {
        var questionChanged = item.Question != question;
        var answerChanged = item.Answer != answer;
        var updated = item;
        if (questionChanged && !answerChanged)
        {
            // A legacy item used the question source as its answer source; make that explicit
            // before advancing the revision disables the legacy fallback.
            updated = updated with { AnswerSourceSessionId = item.KnownAnswerSourceSessionId };
        }
        if (answerChanged)
        {
            var history = (item.AnswerHistory ?? []).ToList();
            history.Add(new WorkspaceAnswerRevision
            {
                Answer = item.Answer,
                RecordedAt = item.UpdatedAt,
                SourceSessionId = item.KnownAnswerSourceSessionId
            });
            updated = updated with { AnswerHistory = history, AnswerSourceSessionId = null };
        }
        if (questionChanged || answerChanged) updated = updated with { AnswerRevision = (item.AnswerRevision ?? 0) + 1 };
        return updated with { Question = question, Answer = answer, UpdatedAt = now };
    }

    /// <summary>Restores a saved answer while keeping the answer it replaces and its source.</summary>
    public static (QuestionBankItem Item, bool Restored) RestoreAnswerRevision(QuestionBankItem item, string revisionId, DateTimeOffset now)
    {
        var revision = (item.AnswerHistory ?? []).FirstOrDefault(entry => entry.Id == revisionId);
        if (revision is null) return (item, false);
        if (item.Answer == revision.Answer && item.AnswerSourceSessionId == revision.SourceSessionId) return (item, false);
        var history = (item.AnswerHistory ?? []).ToList();
        history.Add(new WorkspaceAnswerRevision
        {
            Answer = item.Answer,
            RecordedAt = item.UpdatedAt,
            SourceSessionId = item.KnownAnswerSourceSessionId
        });
        return (item with
        {
            AnswerHistory = history,
            Answer = revision.Answer,
            AnswerSourceSessionId = revision.SourceSessionId,
            AnswerRevision = (item.AnswerRevision ?? 0) + 1,
            UpdatedAt = now
        }, true);
    }

    /// <summary>Refreshes the snapshot after a concurrent edit, forcing a second informed choice.</summary>
    public static WorkspaceSeries? RefreshCandidateBase(WorkspaceSeries series, string candidateId)
    {
        var candidates = (series.KnowledgeCandidates ?? []).ToList();
        var index = candidates.FindIndex(candidate => candidate.Id == candidateId && candidate.Kind == WorkspaceKnowledgeKind.Question);
        if (index < 0) return null;
        var match = (series.QuestionBank ?? []).FirstOrDefault(item => SimilarQuestion(item.Question, candidates[index].Text));
        if (match is null) return null;
        candidates[index] = candidates[index] with { BaseQuestionId = match.Id, BaseAnswerRevision = match.AnswerRevision ?? 0 };
        return series with { KnowledgeCandidates = candidates };
    }

    public static bool SimilarQuestion(string left, string right)
    {
        var leftIntent = QuestionIntent(left);
        var rightIntent = QuestionIntent(right);
        if (leftIntent is not null && rightIntent is not null && leftIntent != rightIntent) return false;
        return Similarity(QuestionKey(left), QuestionKey(right)) >= 0.55;
    }

    /// <summary>Preserves the difference between question intents ("why" versus "how").</summary>
    public static string? QuestionIntent(string question)
    {
        var text = question.ToLowerInvariant();
        var groups = new (string Intent, string[] Markers, bool Regex)[]
        {
            ("why", ["为什么", "为啥", "(?<![a-z])why(?![a-z])"], true),
            ("how", ["怎么样", "怎么", "如何", "怎样", "(?<![a-z])how(?![a-z])"], true),
            ("what", ["是什么", "什么", "(?<![a-z])what(?![a-z])"], true),
            ("which", ["哪一个", "哪些", "哪个", "(?<![a-z])which(?![a-z])"], true),
            ("when", ["什么时候", "何时", "(?<![a-z])when(?![a-z])"], true),
            ("where", ["在哪里", "在哪", "哪里", "(?<![a-z])where(?![a-z])"], true),
            ("who", ["谁", "(?<![a-z])who(?![a-z])"], true)
        };
        (int Offset, string Intent)? earliest = null;
        foreach (var group in groups)
        {
            foreach (var marker in group.Markers)
            {
                var offset = group.Regex && marker.StartsWith("(?<", StringComparison.Ordinal)
                    ? FirstRegexOffset(text, marker)
                    : text.IndexOf(marker, StringComparison.Ordinal);
                if (offset < 0) continue;
                if (earliest is null || offset < earliest.Value.Offset) earliest = (offset, group.Intent);
            }
        }
        return earliest?.Intent;
    }

    private static int FirstRegexOffset(string text, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern);
        return match.Success ? match.Index : -1;
    }

    /// <summary>Drops wording that varies between askings of the same question.</summary>
    public static string QuestionKey(string question)
    {
        var text = question.ToLowerInvariant();
        foreach (var word in new[]
                 {
                     "请问", "请你", "请", "麻烦", "你们", "您", "你", "一下", "怎么样", "怎么", "如何", "怎样", "为什么", "为啥",
                     "是什么", "什么", "哪些", "有没有", "能不能", "可不可以", "是否", "吗", "呢", "吧", "啊",
                     "can you", "could you", "please", "how do you", "how would you", "what is", "what's", "tell me about"
                 })
        {
            text = text.Replace(word, " ", StringComparison.Ordinal);
        }
        return text;
    }

    /// <summary>Character-bigram Jaccard similarity (works for Chinese, English and mixed text).</summary>
    public static double Similarity(string a, string b)
    {
        static HashSet<string> Grams(string text)
        {
            var chars = text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
            if (chars.Length <= 1) return chars.Select(character => character.ToString()).ToHashSet(StringComparer.Ordinal);
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < chars.Length - 1; index++) set.Add(new string([chars[index], chars[index + 1]]));
            return set;
        }

        var left = Grams(a);
        var right = Grams(b);
        if (left.Count == 0 && right.Count == 0) return 1;
        var intersection = left.Count(right.Contains);
        var union = left.Count + right.Count - intersection;
        return (double)intersection / Math.Max(1, union);
    }

    private static (QuestionBankItem Item, bool Counted) RecordQuestionSources(QuestionBankItem item, IReadOnlyList<string> sessions)
    {
        if (sessions.Count == 0) return (item, false);
        var sources = new HashSet<string>(item.SourceSessionIds ?? []);
        if (!string.IsNullOrWhiteSpace(item.SourceSessionId)) sources.Add(item.SourceSessionId);
        var before = sources.Count;
        sources.UnionWith(sessions);
        var added = sources.Count - before;
        var sorted = sources.OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (added == 0) return (item with { SourceSessionIds = sorted }, false);
        return (item with
        {
            SourceSessionIds = sorted,
            SourceSessionId = item.SourceSessionId ?? sessions[0],
            TimesAsked = item.TimesAsked + added
        }, true);
    }

    private static string LogicalItemId(WorkspaceKnowledgeKind kind, string text) =>
        StableDigest($"{KindRaw(kind)}|{Normalized(text)}");

    private static string CandidateId(string originId, string? session, WorkspaceKnowledgeKind kind, string logicalId,
        string answer, string? transcriptSourceRevision, string sourceManifest)
    {
        var baseId = $"{originId.ToLowerInvariant()}|{session?.ToLowerInvariant() ?? ""}|{KindRaw(kind)}|{logicalId}|{Normalized(answer)}|{transcriptSourceRevision ?? ""}";
        return StableDigest(sourceManifest.Length == 0 ? baseId : $"{baseId}|sources:{sourceManifest}");
    }

    private static string KindRaw(WorkspaceKnowledgeKind kind) => kind == WorkspaceKnowledgeKind.Lesson ? "lesson" : "question";

    private static string Normalized(string text) =>
        new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>FNV-1a in both directions, 32 hex digits; stable across processes and platforms.</summary>
    internal static string StableDigest(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var left = 0xcbf29ce484222325UL;
        var right = 0x84222325cbf29ce4UL;
        foreach (var value in bytes) left = (left ^ value) * 0x100000001b3UL;
        for (var index = bytes.Length - 1; index >= 0; index--) right = (right ^ (ulong)(bytes[index] + 31)) * 0x100000001b3UL;
        return left.ToString("x16") + right.ToString("x16");
    }

    private static IReadOnlyList<WorkspaceKnowledgeCandidateSource> CanonicalSources(IReadOnlyList<WorkspaceKnowledgeCandidateSource>? sources)
    {
        if (sources is null || sources.Count == 0) return [];
        return sources
            .GroupBy(source => source.SessionId)
            .Select(group => group.Last())
            .OrderBy(source => source.SessionId, StringComparer.Ordinal)
            .ToList();
    }

    private static string SourceManifest(IReadOnlyList<WorkspaceKnowledgeCandidateSource> sources) =>
        string.Join("|", sources.Select(source =>
            $"{source.SessionId.ToLowerInvariant()}:{BitConverter.DoubleToInt64Bits((source.StartedAt - DateTimeOffset.UnixEpoch).TotalSeconds).ToString("x")}:{source.TranscriptSourceRevision}:{source.ProjectionRevision}"));
}

/// <summary>
/// Pulls the sections a template marks with NotesCarry into lessons and question-bank entries.
/// This is the Mac behaviour that turns a reviewed summary into review candidates.
/// </summary>
public static class NotesKnowledge
{
    public static (IReadOnlyList<string> Lessons, IReadOnlyList<QuestionBankEntry> Bank) Extract(StructuredNotesDoc doc, NotesTemplate template)
    {
        // B11: the notes model is the single source (StructuredNotes.lessons/bankEntries).
        var lessons = doc.Lessons(template);
        var bank = doc.BankEntries().Select(entry => new QuestionBankEntry(entry.Question, entry.Answer)).ToList();
        return (lessons, bank);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(value => value?.Trim()).FirstOrDefault(value => !string.IsNullOrEmpty(value));
}

/// <summary>The two places accepted knowledge feeds back into the meeting (Workspace.experienceNotes / retrievalDocuments).</summary>
public static class WorkspaceKnowledgePrompt
{
    /// <summary>Lessons for the answer prompt: pinned first, then newest, capped like Swift's 2,000 characters.</summary>
    public static string ExperienceNotes(WorkspaceSeries series, int limit = 2000)
    {
        if (!series.UseKnowledge || series.Lessons.Count == 0) return "";
        var ordered = series.Lessons.Where(lesson => lesson.Pinned)
            .Concat(series.Lessons.Where(lesson => !lesson.Pinned).OrderByDescending(lesson => lesson.CreatedAt));
        var lines = new List<string>();
        var used = 0;
        foreach (var lesson in ordered)
        {
            var line = "- " + lesson.Text;
            if (used + line.Length > limit) break;
            lines.Add(line);
            used += line.Length;
        }
        return string.Join("\n", lines);
    }

    /// <summary>The question bank as a compact block for the answer prompt (top questions first).</summary>
    public static string QuestionBankContext(WorkspaceSeries series, int limit = 3)
    {
        if (!series.UseKnowledge) return "";
        var items = (series.QuestionBank ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Question) && !string.IsNullOrWhiteSpace(item.Answer))
            .OrderByDescending(item => item.TimesAsked)
            .ThenByDescending(item => item.UpdatedAt)
            .Take(limit)
            .ToList();
        if (items.Count == 0) return "";
        return string.Join("\n\n", items.Select(item => $"问题：{item.Question}\n准备好的回答：{item.Answer}"));
    }
}
