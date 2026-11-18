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

public static class BackupMerge
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
            var index = result.FindIndex(current => current.Id == template.Id);
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

    public static IReadOnlyList<Guid> SessionsToImport(IReadOnlySet<Guid> existing, IReadOnlyList<Guid> incoming) =>
        incoming.Where(id => !existing.Contains(id)).ToList();
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
        "cueSurface", "cueWidth", "cueStyle", "cueScreen", "cueLauncher", "cueAvoidsSharing",
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
