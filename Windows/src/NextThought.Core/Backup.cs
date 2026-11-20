namespace NextThought.Core;

/// <summary>
/// Describes an export archive (.ntbackup, a ZIP of a folder) — port of BackupManifest.
/// </summary>
public sealed record BackupManifest
{
    public enum BackupKind
    {
        Full,
        Workspace
    }

    public const string FormatIdentifier = "nextthought-backup";
    private const string LegacyFormatIdentifier = "interviewhacker-backup";
    public const int CurrentVersion = 1;

    public string Format { get; init; } = FormatIdentifier;
    public int Version { get; init; } = CurrentVersion;
    public BackupKind Kind { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string AppVersion { get; init; } = "";
    public string AppBuild { get; init; } = "";
    public bool IncludesSecrets { get; init; }
    public int WorkspaceCount { get; init; }
    public int SessionCount { get; init; }

    public sealed class ValidationException(string message) : Exception(message);

    public void Validate()
    {
        if (Format != FormatIdentifier && Format != LegacyFormatIdentifier)
            throw new ValidationException("这不是 NextThought 导出的备份文件。");
        if (Version < 1 || WorkspaceCount < 0 || SessionCount < 0)
            throw new ValidationException("这不是 NextThought 导出的备份文件。");
        if (Version > CurrentVersion)
            throw new ValidationException($"备份格式版本 {Version} 比当前应用新，请先更新应用再导入。");
    }
}

public static partial class BackupMerge
{
    public sealed record TemplateResult(IReadOnlyList<NotesTemplate> Templates, int Added, int Replaced, int Kept);

    /// <summary>Notes templates are matched by id; built-in templates never come from a backup.</summary>
    public static TemplateResult NotesTemplates(IReadOnlyList<NotesTemplate> existing, IReadOnlyList<NotesTemplate> incoming,
        IReadOnlySet<string>? useIncoming = null)
    {
        var result = existing.ToList();
        var added = 0;
        var replaced = 0;
        var kept = 0;
        foreach (var template in incoming.Where(template => !template.IsBuiltIn))
        {
            var index = result.FindIndex(current => SameId(current.Id, template.Id));
            if (index >= 0)
            {
                if (result[index] == template) continue;
                if (useIncoming?.Contains(template.Id) == true)
                {
                    result[index] = template;
                    replaced++;
                }
                else
                {
                    kept++;
                }
            }
            else
            {
                result.Add(template);
                added++;
            }
        }
        return new TemplateResult(result, added, replaced, kept);
    }

    public static IReadOnlyList<Guid> SessionsToImport(IReadOnlySet<Guid> existing, IReadOnlyList<Guid> incoming)
    {
        // A plain loop is used deliberately: the LINQ form intermittently tripped a CLR internal
        // error (0x80131506) in the ReadyToRun release build while reading a backed-up manifest.
        var result = new List<Guid>(incoming.Count);
        foreach (var id in incoming)
            if (!existing.Contains(id)) result.Add(id);
        return result;
    }

    /// <summary>A6: identifiers compare as GUIDs, so Mac's upper-case rewrite is the same series.</summary>
    private static bool SameId(string left, string right) =>
        Guid.TryParse(left, out var leftId) && Guid.TryParse(right, out var rightId)
            ? leftId == rightId
            : string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    public sealed record WorkspaceResult(IReadOnlyList<WorkspaceSeries> Merged, int Added, int Updated, int Skipped);

    /// <summary>
    /// Series are matched by id; the more recently touched copy wins, so importing an old backup
    /// never overwrites newer work, and knowledge is unioned either way (port of workspaces merge).
    /// </summary>
    public static WorkspaceResult Workspaces(IReadOnlyList<WorkspaceSeries> existing, IReadOnlyList<WorkspaceSeries> incoming)
    {
        var merged = existing.ToList();
        var added = 0;
        var updated = 0;
        var skipped = 0;
        foreach (var item in incoming)
        {
            var index = merged.FindIndex(current => SameId(current.Id, item.Id));
            if (index < 0)
            {
                var name = item.Name;
                if (merged.Any(existing => string.Equals(existing.Name, name, StringComparison.Ordinal)))
                {
                    var baseName = name + "\uFF08\u5BFC\u5165\uFF09";
                    name = baseName;
                    for (var suffix = 2; merged.Any(existing => string.Equals(existing.Name, name, StringComparison.Ordinal)); suffix++)
                        name = baseName + " " + suffix;
                }
                merged.Add(item with { Name = name });
                added++;
                continue;
            }
            var current = merged[index];
            var primary = Touched(item) > Touched(current) ? item : current;
            var secondary = ReferenceEquals(primary, item) ? current : item;
            var deletedSources = (primary.SourceDeletionTombstones ?? [])
                .Concat(secondary.SourceDeletionTombstones ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal).ToList();
            var union = primary with
            {
                SessionIds = current.SessionIds.Concat(item.SessionIds).Distinct(StringComparer.Ordinal).ToList(),
                Lessons = MergeLessons(current.Lessons, item.Lessons),
                QuestionBank = MergeQuestionBank(current.QuestionBank, item.QuestionBank),
                KnowledgeCandidates = MergeCandidates(current.KnowledgeCandidates ?? [], item.KnowledgeCandidates ?? []),
                SeriesChangeCandidates = MergeSeriesCandidates(current.SeriesChangeCandidates, item.SeriesChangeCandidates),
                SeriesItemSourceTombstones = MergeSeriesItemSourceTombstones(primary.SeriesItemSourceTombstones, secondary.SeriesItemSourceTombstones),
                SourceDeletionTombstones = deletedSources
            };
            union = union with
            {
                SeriesItems = MergeSeriesItems(current.SeriesItems, item.SeriesItems,
                    SeriesItemSourceTombstones([primary, secondary], union.SeriesChangeCandidates))
            };
            if (deletedSources.Count > 0)
            {
                union = WorkspaceSourceIntegrity.RemoveMeetings(union, deletedSources, false, Touched(union)).Series;
                union = union with { SessionIds = union.SessionIds.Where(id => !deletedSources.Contains(id, StringComparer.Ordinal)).ToList() };
            }
            if (ContentEquals(union, current)) { skipped++; continue; }
            merged[index] = union;
            updated++;
        }
        return new WorkspaceResult(merged, added, updated, skipped);
    }


    /// <summary>Deletion tombstones win over any copy restored from an older backup.</summary>
    private static WorkspaceSeries ApplyTombstones(WorkspaceSeries series)
    {
        var deleted = (series.SourceDeletionTombstones ?? []).ToHashSet(StringComparer.Ordinal);
        if (deleted.Count == 0) return series;
        var lessons = new List<WorkspaceLesson>();
        foreach (var lesson in series.Lessons)
        {
            var allSources = lesson.SourceSessionIds ?? (lesson.SourceSessionId is { } source ? [source] : []);
            if (allSources.Count == 0) { lessons.Add(lesson); continue; }
            var sources = allSources.Where(id => !deleted.Contains(id)).ToList();
            if (sources.Count == 0) continue;
            lessons.Add(lesson with { SourceSessionId = sources[0], SourceSessionIds = sources });
        }
        var questions = new List<QuestionBankItem>();
        foreach (var question in series.QuestionBank)
        {
            var allSources = question.SourceSessionIds ?? (question.SourceSessionId is { } source ? [source] : []);
            if (allSources.Count == 0) { questions.Add(question); continue; }
            var sources = allSources.Where(id => !deleted.Contains(id)).ToList();
            if (sources.Count == 0) continue;
            questions.Add(question with { SourceSessionId = sources[0], SourceSessionIds = sources });
        }
        return series with
        {
            SessionIds = series.SessionIds.Where(id => !deleted.Contains(id)).ToList(),
            Lessons = lessons,
            QuestionBank = questions,
            KnowledgeCandidates = (series.KnowledgeCandidates ?? []).Where(candidate => candidate.SourceSessionId is not { } sourceId || !deleted.Contains(sourceId)).ToList(),
            SeriesItems = series.SeriesItems.Select(item => item.SourceSessionId is { } sourceId && deleted.Contains(sourceId)
                ? item with { SourceMissing = true, SourceQuote = null, Location = null }
                : item).ToList()
        };
    }

    private static bool ContentEquals(WorkspaceSeries left, WorkspaceSeries right) =>
        left.Name == right.Name
        && left.UseKnowledge == right.UseKnowledge
        && left.SessionIds.SequenceEqual(right.SessionIds)
        && left.Lessons.Select(lesson => lesson.Id + "\u0000" + lesson.Text + "\u0000" + lesson.Revision)
            .SequenceEqual(right.Lessons.Select(lesson => lesson.Id + "\u0000" + lesson.Text + "\u0000" + lesson.Revision))
        && left.QuestionBank.Select(question => question.Id + "\u0000" + question.Answer + "\u0000" + question.AnswerRevision)
            .SequenceEqual(right.QuestionBank.Select(question => question.Id + "\u0000" + question.Answer + "\u0000" + question.AnswerRevision))
        && (left.KnowledgeCandidates ?? []).Select(candidate => candidate.Id).SequenceEqual((right.KnowledgeCandidates ?? []).Select(candidate => candidate.Id))
        && System.Text.Json.JsonSerializer.Serialize(left.SeriesItems, WorkspaceStore.Options)
            == System.Text.Json.JsonSerializer.Serialize(right.SeriesItems, WorkspaceStore.Options)
        && (left.SourceDeletionTombstones ?? []).SequenceEqual(right.SourceDeletionTombstones ?? []);

    private static DateTimeOffset Touched(WorkspaceSeries series) => new[]
    {
        series.CreatedAt,
        series.Lessons.Select(lesson => lesson.UpdatedAt).DefaultIfEmpty(series.CreatedAt).Max(),
        (series.KnowledgeCandidates ?? []).Select(candidate => candidate.CreatedAt).DefaultIfEmpty(series.CreatedAt).Max(),
        series.SeriesItems.Select(item => item.UpdatedAt).DefaultIfEmpty(series.CreatedAt).Max()
    }.Max();

    private static List<T> UnionById<T>(IReadOnlyList<T> primary, IReadOnlyList<T> secondary, Func<T, string> id)
    {
        var result = primary.ToList();
        var known = result.Select(id).ToHashSet(StringComparer.Ordinal);
        foreach (var item in secondary)
            if (known.Add(id(item))) result.Add(item);
        return result;
    }
}

/// <summary>Which preferences travel in a backup (port of SettingsSnapshot).</summary>
public static class SettingsSnapshot
{
    public static readonly IReadOnlyList<string> ExportedKeys =
    [
        "apiVisionEnabled", "answerBackend", "apiBaseURL", "apiModel", "codexModelID", "codexReasoning", "verifyUncertainWithModel",
        "automaticAnswers", "recordSessions", "autoSummarize", "autoCollectKnowledgeCandidates", "autoApplyLessons",
        "transcriptionBackend", "localStreamingModel", "secondPassEnabled", "recognitionLanguage", "recognitionVocabulary",
        "cloudProvider", "cloudModel", "cloudConsent",
        "echoSuppression", "microphoneVoiceProcessing", "microphoneEnabled",
        "teleprompterOpacity", "teleprompterFontSize", "capturePrivacy", "keepOnTop", "appearance", "reducedEffects",
        "customShortcuts", "companionPort", "companionRelayURL", "keepRecognizerWarm",
        "updateChannel", "automaticUpdateChecks", "updateRepository",
        "activeWorkspaceID", "autoBackup", "advancedSettingsEnabled",
        "apiPresets", "activeAPIPresetID",
        "webSearchMode", "webSearchProvider",
        "insightScreenWatch", "insightScreenInterval", "insightFollowSlides", "insightAnswerProgress",
        "globalSkillIDs", "assistantLayout",
        "documentOCRProvider", "documentProviderWasSelected", "ocrFallbackToVision", "semanticRetrievalEnabled",
        "localOCRModelID", "codexOCRModel", "codexOCREffort", "codexOCRTimeout",
        "notesModelID", "notesReasoning", "notesLanguage", "notesTemplateByType", "customNotesTemplates",
        "pinnedWorkspaceIDs", "projectLibrarySort",
        "cueSurface", "cueWidth", "cueAlwaysOpen", "subtitleBarX", "subtitleBarY", "cueStyle", "cueScreen", "cueLauncher", "cueAvoidsSharing",
        "coachEnabled", "coachWatchesGaze", "coachGazeSensitivity", "lastScene", "sceneOverrides"
    ];

    /// <summary>Keychain accounts exported only on request.</summary>
    public static readonly IReadOnlyList<string> SecretAccounts =
        ["answer.api", "github.token", "websearch.tavily", "stt.soniox", "stt.openai", "stt.deepgram"];

    /// <summary>Fixed accounts plus one per saved API preset (answer.api.&lt;id&gt;).</summary>
    public static bool IsSecretAccount(string account) =>
        SecretAccounts.Contains(account) || account.StartsWith("answer.api.", StringComparison.Ordinal);

    public static Dictionary<string, object?> Filter(IReadOnlyDictionary<string, object?> values) =>
        values.Where(pair => ExportedKeys.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);

    /// <summary>Maps the renamed preference when restoring a backup made before per-item review.</summary>
    public static Dictionary<string, object?> ValuesForRestore(IReadOnlyDictionary<string, object?> values)
    {
        var restored = Filter(values);
        if (!restored.ContainsKey("autoCollectKnowledgeCandidates") && restored.TryGetValue("autoApplyLessons", out var legacy))
            restored["autoCollectKnowledgeCandidates"] = legacy;
        restored.Remove("autoApplyLessons");
        return restored;
    }
}
