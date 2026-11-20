using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>Minimal deletion metadata for one accepted series item (Mac WorkspaceSeriesItemSourceTombstone).</summary>
public sealed record WorkspaceSeriesItemSourceTombstone
{
    [JsonPropertyName("itemID")]
    public string ItemId { get; init; } = "";

    [JsonPropertyName("sourceSessionID")]
    public string SourceSessionId { get; init; } = "";
}

/// <summary>
/// 一条经验：这场（或这个系列）总结出来、下次还用得上的一句话。
/// Port of <c>WorkspaceLesson</c> (Workspace.swift): accepted text plus the provenance and
/// revision history that make it auditable.
/// </summary>
public sealed record WorkspaceLesson
{
    public string Id { get; init; } = Guid.NewGuid().ToString("D");

    public string Text { get; init; } = "";

    [JsonPropertyName("sourceSessionID")]
    public string? SourceSessionId { get; init; }

    /// <summary>All meetings that independently support this lesson (older files omit it).</summary>
    [JsonPropertyName("sourceSessionIDs")]
    public IReadOnlyList<string>? SourceSessionIds { get; init; }

    /// <summary>Deleted source ids are retained only as provenance tombstones.</summary>
    [JsonPropertyName("sourceMissingSessionIDs")]
    public IReadOnlyList<string>? SourceMissingSessionIds { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool Pinned { get; init; }

    public int Revision { get; init; } = 1;

    public IReadOnlyList<WorkspaceLessonRevision> History { get; init; } = [];

    /// <summary>True only when the user explicitly kept this text after deleting its source.</summary>
    public bool IndependentlyRetained { get; init; }

    public static WorkspaceLesson Create(string text, string? sourceSessionId = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkspaceLesson
        {
            Id = Guid.NewGuid().ToString("D"),
            Text = text.Trim(),
            SourceSessionId = sourceSessionId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}

/// <summary>
/// 一个系列：同一条线上的若干次会议（同一门课、同一个项目、同一个岗位的面试），
/// 加上从这些会议里沉淀下来的经验、题库和待确认的候选。
/// Partial port of <c>Workspace</c>: the Windows app groups meetings by series, so the
/// series carries the knowledge that the Mac workspace carries.
/// </summary>
public sealed record WorkspaceSeries(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> SessionIds,
    IReadOnlyList<WorkspaceLesson> Lessons)
{
    /// <summary>Questions asked in these meetings and the best answer known so far.</summary>
    public IReadOnlyList<QuestionBankItem> QuestionBank { get; init; } = [];

    /// <summary>Review queue: suggestions stay inert until accepted or rejected.</summary>
    public IReadOnlyList<WorkspaceKnowledgeCandidate> KnowledgeCandidates { get; init; } = [];

    /// <summary>Accepted decisions and actions carried by this series.</summary>
    public IReadOnlyList<WorkspaceSeriesItem> SeriesItems { get; init; } = [];

    /// <summary>Materials that belong to this series (Windows ids of enabled documents).</summary>
    public IReadOnlyList<string> DocumentIds { get; init; } = [];

    /// <summary>Material metadata of this series (port of Workspace.documents).</summary>
    public IReadOnlyList<ContextDocument> Documents { get; init; } = [];

    /// <summary>Reviewed-evidence proposals awaiting accept/reject.</summary>
    public IReadOnlyList<WorkspaceSeriesChangeCandidate> SeriesChangeCandidates { get; init; } = [];

    /// <summary>Deleted meetings whose knowledge must not be resurrected by a stale copy.</summary>
    [JsonPropertyName("sourceDeletionTombstones")]
    public IReadOnlyList<string>? SourceDeletionTombstones { get; init; }

    /// <summary>Mac seriesItemSourceTombstones: deleted accepted item/source pairs.</summary>
    [JsonPropertyName("seriesItemSourceTombstones")]
    public IReadOnlyList<WorkspaceSeriesItemSourceTombstone>? SeriesItemSourceTombstones { get; init; }

    /// <summary>Unknown/future Mac fields are kept so a backup round-trip stays lossless.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }

    /// <summary>Skills from the library attached to this series (Mac Workspace.skillIDs).</summary>
    [JsonPropertyName("skillIDs")]
    public IReadOnlyList<string>? SkillIds { get; init; }

    /// <summary>Latest stage review across all sessions of this series.</summary>
    [JsonPropertyName("stageReview")]
    public string? StageReview { get; init; }

    [JsonPropertyName("stageReviewAt")]
    public DateTimeOffset? StageReviewAt { get; init; }

    // Mac Workspace fields the Windows UI does not expose yet. They are kept so a Mac backup
    // round-trips without silently dropping the scene's setup.
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("style")]
    public string? Style { get; init; }

    [JsonPropertyName("preferences")]
    public string? Preferences { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("customTypeName")]
    public string? CustomTypeName { get; init; }

    [JsonPropertyName("customBaseMode")]
    public string? CustomBaseMode { get; init; }

    [JsonPropertyName("glossary")]
    public Glossary? Glossary { get; init; }

    [JsonPropertyName("archived")]
    public bool? Archived { get; init; }

    /// <summary>Mac Workspace.fields: per-scene labelled values.</summary>
    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, string>? Fields { get; init; }

    /// <summary>A standing scene reused for recurring meetings.</summary>
    [JsonPropertyName("standing")]
    public bool? Standing { get; init; }

    /// <summary>Feed lessons and the question bank back into answers (default on).</summary>
    public bool UseKnowledge { get; init; } = true;

    public static WorkspaceSeries Create(string name) =>
        new(Guid.NewGuid().ToString("D"), name.Trim(), DateTimeOffset.UtcNow, [], []);

    public WorkspaceSeries WithSession(string sessionId) =>
        SessionIds.Contains(sessionId) ? this : this with { SessionIds = [.. SessionIds, sessionId] };

    public WorkspaceSeries WithoutSession(string sessionId) =>
        SessionIds.Contains(sessionId) ? this with { SessionIds = SessionIds.Where(id => id != sessionId).ToList() } : this;

    /// <summary>
    /// Template fields as prompt text: known fields in profile order, then values from another
    /// <summary>Documents of this series, optionally only the enabled ones.</summary>
    public IReadOnlyList<ContextDocument> DocumentsOf(bool enabledOnly = false) =>
        enabledOnly ? Documents.Where(document => document.Enabled).ToList() : Documents;

    /// mode in key order (port of Workspace.materialText; empty values are skipped).
    /// </summary>
    public string MaterialText(IReadOnlyList<(string Id, string Title)> profileFields,
        IReadOnlyDictionary<string, string> fields)
    {
        var lines = new List<string>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, title) in profileFields)
        {
            known.Add(id);
            if (fields.TryGetValue(id, out var value) && value.Trim().Length > 0)
                lines.Add(title + "：" + value.Trim());
        }
        foreach (var (key, value) in fields.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (known.Contains(key) || value.Trim().Length == 0) continue;
            lines.Add(key + "：" + value.Trim());
        }
        return string.Join("\n", lines);
    }

    /// <summary>Lessons as prompt bullets: pinned first, then newest, bounded (port of experienceNotes).</summary>
    public string ExperienceNotes(int limit = 2_000)
    {
        if (!UseKnowledge) return "";
        var ordered = Lessons.Where(lesson => lesson.Pinned)
            .Concat(Lessons.Where(lesson => !lesson.Pinned).OrderByDescending(lesson => lesson.CreatedAt));
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

    /// <summary>Pending suggestions, newest first (what the series page lists).</summary>
    [JsonIgnore]
    public IReadOnlyList<WorkspaceKnowledgeCandidate> PendingCandidates =>
        (KnowledgeCandidates ?? [])
            .Where(candidate => candidate.Status == WorkspaceKnowledgeStatus.Pending)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .ToList();
}

/// <summary>The whole workspace file: series plus which session belongs where.</summary>
public sealed record WorkspaceData(IReadOnlyList<WorkspaceSeries> Series)
{
    public static readonly WorkspaceData Empty = new([]);

    public WorkspaceSeries? SeriesOfSession(string sessionId) =>
        Series.FirstOrDefault(series => series.SessionIds.Contains(sessionId));

    public WorkspaceSeries? Find(string seriesId) => Series.FirstOrDefault(series => series.Id == seriesId);

    /// <summary>Replaces one series in place (knowledge review returns a new record).</summary>
    public WorkspaceData WithSeries(WorkspaceSeries series) => this with
    {
        Series = Series.Select(item => item.Id == series.Id ? series : item).ToList()
    };

    /// <summary>Adds a series (names are unique, case-insensitively).</summary>
    public WorkspaceData AddSeries(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0 || Series.Any(series => string.Equals(series.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            return this;
        return this with { Series = [.. Series, WorkspaceSeries.Create(trimmed)] };
    }

    /// <summary>A session belongs to at most one series: assigning moves it.</summary>
    public WorkspaceData Assign(string sessionId, string seriesId)
    {
        if (Find(seriesId) is null) return this;
        return this with
        {
            Series = Series.Select(series => series.Id == seriesId
                ? series.WithSession(sessionId)
                : series.WithoutSession(sessionId)).ToList()
        };
    }

    public WorkspaceData Remove(string sessionId) => this with
    {
        Series = Series.Select(series => series.WithoutSession(sessionId)).ToList()
    };

    /// <summary>Deletes a whole series (used by the throw-away series of the self-check).</summary>
    public WorkspaceData RemoveSeries(string seriesId) => this with
    {
        Series = Series.Where(series => series.Id != seriesId).ToList()
    };

    public WorkspaceData AddLesson(string seriesId, string text, string? sourceSessionId = null)
    {
        if (text.Trim().Length == 0 || Find(seriesId) is not { } series) return this;
        var lesson = WorkspaceLesson.Create(text, sourceSessionId);
        return this with
        {
            Series = Series.Select(item => item.Id == seriesId
                ? item with { Lessons = [.. item.Lessons, lesson] }
                : item).ToList()
        };
    }

    public WorkspaceData RemoveLesson(string seriesId, string lessonId) => this with
    {
        Series = Series.Select(item => item.Id == seriesId
            ? item with { Lessons = item.Lessons.Where(lesson => lesson.Id != lessonId).ToList() }
            : item).ToList()
    };

    public WorkspaceData Rename(string seriesId, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) return this;
        if (Series.Any(series => series.Id != seriesId && string.Equals(series.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            return this;
        return this with
        {
            Series = Series.Select(series => series.Id == seriesId ? series with { Name = trimmed } : series).ToList()
        };
    }
}

/// <summary>Reads and writes <c>Workspace/workspace.json</c> in the data directory.</summary>
public sealed class WorkspaceStore
{
    // Mac writes workspace files through JSONLines (ISO-8601 UTC dates and camel-case enum
    // values), so this must use the same archive options rather than the default date writer.
    public static readonly JsonSerializerOptions Options = new(JsonDefaults.ArchiveOptions)
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions ReadOptions = new(Options) { PropertyNameCaseInsensitive = true };

    private readonly string path;
    private readonly string seriesDirectory;
    private readonly HashSet<string> loadedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unreadableFiles = new(StringComparer.OrdinalIgnoreCase);
    private bool loadFailed;

    public WorkspaceStore(string dataDirectory)
    {
        path = Path.Combine(dataDirectory, "Workspace", "workspace.json");
        seriesDirectory = Path.Combine(dataDirectory, "Workspaces");
    }

    public string Path_ => path;

    public WorkspaceData Load()
    {
        loadedFiles.Clear();
        unreadableFiles.Clear();
        loadFailed = false;
        try
        {
            // Mac keeps one file per series. Prefer that layout when present, then fall back to
            // the single Windows workspace.json so older data keeps working.
            if (Directory.Exists(seriesDirectory))
            {
                var series = new List<WorkspaceSeries>();
                foreach (var file in Directory.EnumerateFiles(seriesDirectory, "*.json"))
                {
                    var name = Path.GetFileName(file);
                    try
                    {
                        if (JsonSerializer.Deserialize<WorkspaceSeries>(File.ReadAllText(file), ReadOptions) is { } item)
                        {
                            series.Add(item);
                            loadedFiles.Add(name);
                        }
                    }
                    catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
                    {
                        // Never let Save delete a file just because it could not be read.
                        unreadableFiles.Add(name);
                        LogLoadFailure(name, error);
                    }
                }
                if (series.Count > 0) return Sanitise(new WorkspaceData(series));
                if (unreadableFiles.Count > 0) return WorkspaceData.Empty;
            }
            if (!File.Exists(path)) return WorkspaceData.Empty;
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<WorkspaceData>(json, ReadOptions);
            return data is null ? WorkspaceData.Empty : Sanitise(data);
        }
        catch (Exception error)
        {
            // Refuse the next save rather than interpreting a read failure as an empty workspace.
            loadFailed = true;
            LogLoadFailure(Path.GetFileName(path), error);
            return WorkspaceData.Empty;
        }
    }

    public void Save(WorkspaceData data)
    {
        if (loadFailed) throw new InvalidOperationException("工作区读取失败，已拒绝保存以免覆盖原文件。");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Directory.CreateDirectory(seriesDirectory);
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var series in data.Series)
        {
            var name = series.Id.ToUpperInvariant() + ".json";
            keep.Add(name);
            AtomicWrite(Path.Combine(seriesDirectory, name), JsonSerializer.Serialize(series, Options));
        }
        // Delete only files that were read successfully and are no longer present in memory.
        // Unreadable or unknown files stay untouched; they may be hand-edited Mac data.
        foreach (var name in loadedFiles)
        {
            if (keep.Contains(name)) continue;
            var file = Path.Combine(seriesDirectory, name);
            if (File.Exists(file) && !unreadableFiles.Contains(name)) File.Delete(file);
        }
        // Legacy single-file copy for older Windows builds; Mac reads Workspaces/.
        AtomicWrite(path, JsonSerializer.Serialize(data, Options));
    }

    private static void AtomicWrite(string target, string content)
    {
        var temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, content);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void LogLoadFailure(string name, Exception error)
    {
        try
        {
            var log = Path.Combine(Path.GetDirectoryName(path)!, "workspace-load.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"{DateTimeOffset.UtcNow:O}\t{name}\t{error.GetType().Name}\t{error.Message}\n");
        }
        catch { /* logging must never start a data-loss path */ }
    }

    /// <summary>
    /// Older or hand-edited files may repeat ids, point a session at two series, carry empty
    /// lessons or suggestions whose text went missing. Keep the valid parts.
    /// </summary>
    private static WorkspaceData Sanitise(WorkspaceData data)
    {
        var seenSessions = new HashSet<string>();
        var series = new List<WorkspaceSeries>();
        foreach (var item in data.Series ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name)) continue;
            var sessions = new List<string>();
            foreach (var sessionId in item.SessionIds ?? [])
            {
                if (string.IsNullOrWhiteSpace(sessionId) || !seenSessions.Add(sessionId)) continue;
                sessions.Add(sessionId);
            }
            var lessons = new List<WorkspaceLesson>();
            foreach (var lesson in item.Lessons ?? [])
            {
                if (string.IsNullOrWhiteSpace(lesson.Text)) continue;
                lessons.Add(lesson with
                {
                    Revision = Math.Max(1, lesson.Revision),
                    UpdatedAt = lesson.UpdatedAt == default ? lesson.CreatedAt : lesson.UpdatedAt,
                    History = lesson.History ?? []
                });
            }
            var bank = (item.QuestionBank ?? [])
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Question))
                .ToList();
            var candidates = (item.KnowledgeCandidates ?? [])
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Id) && !string.IsNullOrWhiteSpace(candidate.Text))
                .ToList();
            series.Add(item with
            {
                SessionIds = sessions,
                Lessons = lessons,
                QuestionBank = bank,
                KnowledgeCandidates = candidates
            });
        }
        return new WorkspaceData(series);
    }
}
