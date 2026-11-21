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

    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Swift defaults updatedAt to createdAt when it is absent.</summary>
    [JsonIgnore]
    public DateTimeOffset EffectiveUpdatedAt => UpdatedAt == default ? CreatedAt : UpdatedAt;

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
[method: System.Text.Json.Serialization.JsonConstructor]
public sealed record WorkspaceSeries(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<WorkspaceLesson> Lessons)
{
    /// <summary>
    /// Meetings of this series. Mac workspaces carry no session list (meta.workspaceID points back),
    /// so decoding a Mac file leaves this empty rather than null.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> SessionIds { get; init; } = [];

    public WorkspaceSeries(string id, string name, DateTimeOffset createdAt,
        IReadOnlyList<string> sessionIds, IReadOnlyList<WorkspaceLesson> lessons)
        : this(id, name, createdAt, lessons) => SessionIds = sessionIds;

    /// <summary>Questions asked in these meetings and the best answer known so far.</summary>
    public IReadOnlyList<QuestionBankItem> QuestionBank { get; init; } = [];

    /// <summary>Review queue: suggestions stay inert until accepted or rejected.</summary>
    public IReadOnlyList<WorkspaceKnowledgeCandidate>? KnowledgeCandidates { get; init; }

    /// <summary>Accepted decisions and actions carried by this series.</summary>
    public IReadOnlyList<WorkspaceSeriesItem> SeriesItems { get; init; } = [];

    /// <summary>Materials that belong to this series (Windows ids of enabled documents).</summary>
    [JsonIgnore]
    public IReadOnlyList<string> DocumentIds { get; init; } = [];

    /// <summary>Material metadata of this series (port of Workspace.documents).</summary>
    public IReadOnlyList<ContextDocument> Documents { get; init; } = [];

    /// <summary>
    /// Mac <c>Workspace.updatedAt</c>: the last time the series itself changed. HomeView sorts
    /// 继续一个系列 by <c>lastSessionDate ?? updatedAt</c>; Windows keeps the field so a Mac file
    /// round-trips and the home order matches (Swift defaults it to createdAt when absent).
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Swift: <c>updatedAt ?? createdAt</c>.</summary>
    [JsonIgnore]
    public DateTimeOffset EffectiveUpdatedAt => UpdatedAt == default ? CreatedAt : UpdatedAt;

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
        (SessionIds ?? []).Contains(sessionId) ? this : this with { SessionIds = [.. SessionIds ?? [], sessionId] };

    public WorkspaceSeries WithoutSession(string sessionId) =>
        (SessionIds ?? []).Contains(sessionId) ? this with { SessionIds = SessionIds.Where(id => id != sessionId).ToList() } : this;

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

    /// <summary>B18: a custom type behaves like its base type (Workspace.effectiveMode).</summary>
    [JsonIgnore]
    public MeetingMode EffectiveMode
    {
        get
        {
            var mode = ParseMode(Mode);
            if (mode != MeetingMode.Custom) return mode;
            var baseMode = ParseMode(CustomBaseMode);
            return baseMode == MeetingMode.Custom ? MeetingMode.General : baseMode;
        }
    }

    [JsonIgnore]
    public ModeProfile Profile => ModeProfile.For(EffectiveMode);

    private static MeetingMode ParseMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return MeetingMode.General;
        var normalized = raw.Trim();
        foreach (var value in Enum.GetValues<MeetingMode>())
            if (string.Equals(value.ToString(), normalized, StringComparison.OrdinalIgnoreCase)) return value;
        return normalized.ToLowerInvariant() switch
        {
            "labmeeting" or "lab_meeting" => MeetingMode.LabMeeting,
            "groupproject" or "group_project" => MeetingMode.GroupProject,
            "general" => MeetingMode.General,
            _ => MeetingMode.General
        };
    }

    /// <summary>B18: structured fields, leftover fields, then free notes (Workspace.materialText).</summary>
    public string MaterialText() =>
        MaterialText(Profile.Fields.Select(field => (field.Id, field.Title)).ToList(), Fields ?? new Dictionary<string, string>())
        + (string.IsNullOrWhiteSpace(Notes) ? "" : (Fields is { Count: > 0 } ? "\n" : "") + Notes.Trim());

    /// <summary>B18: the question bank becomes one more searchable document (Workspace.retrievalDocuments).</summary>
    [JsonIgnore]
    public IReadOnlyList<ContextDocument> RetrievalDocuments
    {
        get
        {
            var result = Documents.ToList();
            var reviewed = (QuestionBank ?? []).Where(item =>
                !string.IsNullOrWhiteSpace(item.Question) && !string.IsNullOrWhiteSpace(item.Answer)).ToList();
            if (UseKnowledge && reviewed.Count > 0)
                result.Add(new ContextDocument
                {
                    Id = Id,
                    Name = "题库（历次复盘）",
                    Pages = reviewed.Select(item => "问题：" + item.Question.Trim() + "\n准备好的回答：" + item.Answer.Trim()).ToList(),
                    Enabled = true,
                    Category = "题库"
                });
            return result;
        }
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

    /// <summary>A6: identifiers compare as parsed GUIDs, never as differently-cased strings.</summary>
    public static bool SameId(string? left, string? right)
    {
        if (left is null || right is null) return false;
        if (Guid.TryParse(left, out var a) && Guid.TryParse(right, out var b)) return a == b;
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsId(IReadOnlyList<string>? ids, string id) =>
        ids is not null && ids.Any(candidate => SameId(candidate, id));

    public WorkspaceSeries? SeriesOfSession(string sessionId) =>
        Series.FirstOrDefault(series => ContainsId(series.SessionIds, sessionId));

    public WorkspaceSeries? Find(string seriesId) => Series.FirstOrDefault(series => SameId(series.Id, seriesId));

    /// <summary>Replaces one series in place (knowledge review returns a new record).</summary>
    public WorkspaceData WithSeries(WorkspaceSeries series) => this with
    {
        Series = Series.Select(item => SameId(item.Id, series.Id) ? series : item).ToList()
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
        Series = Series.Where(series => !SameId(series.Id, seriesId)).ToList()
    };

    public WorkspaceData AddLesson(string seriesId, string text, string? sourceSessionId = null)
    {
        if (text.Trim().Length == 0 || Find(seriesId) is not { } series) return this;
        var lesson = WorkspaceLesson.Create(text, sourceSessionId);
        return this with
        {
            Series = Series.Select(item => SameId(item.Id, seriesId)
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
        if (Series.Any(series => !SameId(series.Id, seriesId) && string.Equals(series.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            return this;
        return this with
        {
            Series = Series.Select(series => SameId(series.Id, seriesId) ? series with { Name = trimmed } : series).ToList()
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
    private readonly string windowsStatePath;
    private readonly string sessionStatePath;
    private readonly HashSet<string> loadedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unreadableFiles = new(StringComparer.OrdinalIgnoreCase);
    private bool loadFailed;

    public WorkspaceStore(string dataDirectory)
    {
        path = Path.Combine(dataDirectory, "Workspace", "workspace.json");
        seriesDirectory = Path.Combine(dataDirectory, "Workspaces");
        // A6: DocumentIds are Windows-only state; they live beside the data, never in a Mac file or backup.
        windowsStatePath = Path.Combine(dataDirectory, "WindowsState", "workspace-documents.json");
        // A7/A6: session lists are Windows grouping state, never part of a Mac workspace file.
        sessionStatePath = Path.Combine(dataDirectory, "WindowsState", "workspace-sessions.json");
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
                        var text = File.ReadAllText(file);
                        if (JsonSerializer.Deserialize<WorkspaceSeries>(text, ReadOptions) is { } item)
                        {
                            series.Add(item with { SessionIds = ReadSessionIds(text) });
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
                if (series.Count > 0) return ApplySessionIds(ApplyDocumentIds(SyncSessionMeta(Sanitise(new WorkspaceData(series)))));
                if (unreadableFiles.Count > 0) return WorkspaceData.Empty;
            }
            if (!File.Exists(path)) return WorkspaceData.Empty;
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<WorkspaceData>(json, ReadOptions);
            if (data is null) return WorkspaceData.Empty;
            // The old single Windows file still carries sessionIds; fold them back in memory.
            var legacy = JsonDocument.Parse(json);
            if (legacy.RootElement.TryGetProperty("series", out var seriesNode) && seriesNode.ValueKind == JsonValueKind.Array)
            {
                var sessionsById = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var node in seriesNode.EnumerateArray())
                {
                    if (node.ValueKind != JsonValueKind.Object) continue;
                    if (!node.TryGetProperty("id", out var idNode) || idNode.ValueKind != JsonValueKind.String) continue;
                    var sessions = new List<string>();
                    if (TryGetSessionIdList(node, out var list))
                        sessions.AddRange(list.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String)
                            .Select(value => value.GetString()!).Where(value => value.Length > 0));
                    sessionsById[idNode.GetString()!] = sessions;
                }
                data = data with
                {
                    Series = data.Series.Select(item => sessionsById.TryGetValue(item.Id, out var sessions)
                        ? item with { SessionIds = sessions } : item).ToList()
                };
            }
            return ApplySessionIds(ApplyDocumentIds(SyncSessionMeta(Sanitise(data))));
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
            var name = StableSeriesId(series.Id) + ".json";
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
        SyncSessionMetaFiles(data);
        SaveDocumentIds(data);
        SaveSessionIds(data);
        // Legacy single-file copy for older Windows builds; Mac reads Workspaces/.
        AtomicWrite(path, JsonSerializer.Serialize(data, Options));
    }

    /// <summary>
    /// A7/A6: session ids are Windows grouping state. Mac workspace files never carry them, so the
    /// store keeps them in WindowsState/workspace-sessions.json (beside workspace-documents.json,
    /// never in a Mac file or a portable backup) and merges them back after Load.
    /// </summary>
    private void SaveSessionIds(WorkspaceData data)
    {
        try
        {
            var map = data.Series
                .Where(series => series.SessionIds.Count > 0)
                .ToDictionary(series => StableSeriesId(series.Id), series => series.SessionIds.ToList());
            if (map.Count == 0)
            {
                if (File.Exists(sessionStatePath)) File.Delete(sessionStatePath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(sessionStatePath)!);
            AtomicWrite(sessionStatePath, JsonSerializer.Serialize(map, JsonDefaults.ArchiveOptions));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private WorkspaceData ApplySessionIds(WorkspaceData data)
    {
        if (!File.Exists(sessionStatePath)) return data;
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(sessionStatePath),
                JsonDefaults.ArchiveOptions);
            if (map is null || map.Count == 0) return data;
            return data with
            {
                Series = data.Series.Select(series =>
                {
                    if (!map.TryGetValue(StableSeriesId(series.Id), out var stored) || stored.Count == 0) return series;
                    var merged = series.SessionIds.Concat(stored).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    return series with { SessionIds = merged };
                }).ToList()
            };
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return data;
        }
    }

    /// <summary>A6/§4.3: the on-disk identifier spelling is upper-case with hyphens.</summary>
    private static string StableSeriesId(string id) =>
        Guid.TryParse(id, out var guid) ? guid.ToString("D").ToUpperInvariant() : id.ToUpperInvariant();

    /// <summary>§4.4: Windows-only material ids never enter the Mac workspace file or a backup.</summary>
    private void SaveDocumentIds(WorkspaceData data)
    {
        try
        {
            var map = data.Series
                .Where(series => series.DocumentIds.Count > 0)
                .ToDictionary(series => StableSeriesId(series.Id), series => series.DocumentIds.ToList());
            if (map.Count == 0)
            {
                if (File.Exists(windowsStatePath)) File.Delete(windowsStatePath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(windowsStatePath)!);
            AtomicWrite(windowsStatePath, JsonSerializer.Serialize(map, JsonDefaults.ArchiveOptions));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private WorkspaceData ApplyDocumentIds(WorkspaceData data)
    {
        if (!File.Exists(windowsStatePath)) return data;
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(windowsStatePath),
                JsonDefaults.ArchiveOptions);
            if (map is null) return data;
            return data with
            {
                Series = data.Series.Select(series =>
                {
                    var key = map.Keys.FirstOrDefault(candidate => WorkspaceData.SameId(candidate, series.Id));
                    return key is null ? series : series with { DocumentIds = map[key] };
                }).ToList()
            };
        }
        catch (JsonException) { return data; }
        catch (IOException) { return data; }
        catch (UnauthorizedAccessException) { return data; }
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

    private string SessionsDirectory => Path.Combine(Path.GetDirectoryName(seriesDirectory)!, "Sessions");

    private static IReadOnlyList<string> ReadSessionIds(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!TryGetSessionIdList(document.RootElement, out var list))
                return [];
            return list.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()!).Where(value => value.Length > 0).ToList();
        }
        catch (JsonException) { return []; }
    }

    private static bool TryGetSessionIdList(JsonElement root, out JsonElement list)
    {
        foreach (var name in new[] { "sessionIds", "sessionIDs" })
            if (root.TryGetProperty(name, out list) && list.ValueKind == JsonValueKind.Array) return true;
        list = default;
        return false;
    }

    private static bool SameGuid(string left, string right) =>
        Guid.TryParse(left, out var a) && Guid.TryParse(right, out var b) ? a == b
            : string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>A7: meta.json is the source of truth for series membership; old series files are migrated once.</summary>
    private WorkspaceData SyncSessionMeta(WorkspaceData data)
    {
        var series = data.Series.ToList();
        foreach (var (sessionId, file) in SessionMetaFiles())
        {
            System.Text.Json.Nodes.JsonObject? node = null;
            try { node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file)) as System.Text.Json.Nodes.JsonObject; }
            catch (JsonException) { continue; }
            if (node is null) continue;
            var workspaceId = node["workspaceID"]?.GetValue<string>() ?? "";
            var workspaceName = node["workspaceName"]?.GetValue<string>() ?? "";
            // The folder name is upper-case on disk; meta.id is the canonical spelling used by History.
            var sessionKey = node["id"]?.GetValue<string>() is { Length: > 0 } metaId ? metaId : sessionId;
            var index = series.FindIndex(item => SameGuid(item.Id, workspaceId));
            if (index >= 0)
            {
                if (!(series[index].SessionIds ?? []).Any(id => SameGuid(id, sessionKey)))
                    series[index] = series[index].WithSession(sessionKey);
                if (!SameGuid(workspaceId, series[index].Id) || workspaceName != series[index].Name)
                    WriteSessionMetaWorkspace(file, node, series[index].Id, series[index].Name);
            }
            else
            {
                var owner = series.FindIndex(item => (item.SessionIds ?? []).Any(id => SameGuid(id, sessionKey)));
                if (owner >= 0) WriteSessionMetaWorkspace(file, node, series[owner].Id, series[owner].Name);
            }
        }
        return new WorkspaceData(series);
    }

    /// <summary>A7: every meeting of a series records workspaceID/workspaceName in its own meta.</summary>
    private void SyncSessionMetaFiles(WorkspaceData data)
    {
        var files = SessionMetaFiles().ToDictionary(pair => pair.SessionId, pair => pair.File, StringComparer.OrdinalIgnoreCase);
        foreach (var series in data.Series)
        foreach (var sessionId in series.SessionIds ?? [])
        {
            if (!files.TryGetValue(sessionId, out var file)) continue;
            try
            {
                if (System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file)) is not System.Text.Json.Nodes.JsonObject node) continue;
                WriteSessionMetaWorkspace(file, node, series.Id, series.Name);
            }
            catch (JsonException) { }
        }
    }

    private IEnumerable<(string SessionId, string File)> SessionMetaFiles()
    {
        if (!Directory.Exists(SessionsDirectory)) yield break;
        foreach (var folder in Directory.EnumerateDirectories(SessionsDirectory))
        {
            var file = Path.Combine(folder, "meta.json");
            if (!File.Exists(file)) continue;
            var sessionId = Path.GetFileName(folder);
            try
            {
                if (System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file)) is System.Text.Json.Nodes.JsonObject node
                    && node["id"]?.GetValue<string>() is { Length: > 0 } id) sessionId = id;
            }
            catch (JsonException) { }
            yield return (sessionId, file);
        }
    }

    private static void WriteSessionMetaWorkspace(string file, System.Text.Json.Nodes.JsonObject node, string workspaceId, string workspaceName)
    {
        node["workspaceID"] = workspaceId;
        node["workspaceName"] = workspaceName;
        AtomicWrite(file, node.ToJsonString(JsonDefaults.ArchiveOptions));
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

/// <summary>B22: stable revision id computed from the snapshot content (Swift stableID).</summary>
public static class WorkspaceSeriesItemRevisionIds
{
    public static string RevisionId(this WorkspaceSeriesItem item)
    {
        var recorded = BitConverter.DoubleToInt64Bits(item.EffectiveUpdatedAt.ToUnixTimeMilliseconds() / 1000.0).ToString("x");
        var dueDate = item.DueDate is { } date
            ? BitConverter.DoubleToInt64Bits(date.ToUnixTimeMilliseconds() / 1000.0).ToString("x") : null;
        var values = new string?[]
        {
            item.Id.ToLowerInvariant(), item.Revision.ToString(), System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(item.Kind.ToString()), item.Text, item.Owner, item.Due, dueDate,
            item.ActionState is { } actionState ? System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(actionState.ToString()) : null, item.SourceSessionId?.ToLowerInvariant(), item.EvidenceId, item.ClaimRevision,
            item.SourceRevision, item.SourceQuote, item.Location, item.TranscriptSourceRevision,
            item.SourceMissing?.ToString(), recorded
        };
        var material = string.Join("|", values.Select(value => value is null ? "0:" : "1" + System.Text.Encoding.UTF8.GetByteCount(value) + ":" + value));
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("series-item-revision-v2|" + material))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes).ToString("D");
    }
}
