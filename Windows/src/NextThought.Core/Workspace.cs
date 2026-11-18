using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

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

    /// <summary>Deleted meetings whose knowledge must not be resurrected by a stale copy.</summary>
    [JsonPropertyName("sourceDeletionTombstones")]
    public IReadOnlyList<string>? SourceDeletionTombstones { get; init; }

    /// <summary>Feed lessons and the question bank back into answers (default on).</summary>
    public bool UseKnowledge { get; init; } = true;

    public static WorkspaceSeries Create(string name) =>
        new(Guid.NewGuid().ToString("D"), name.Trim(), DateTimeOffset.UtcNow, [], []);

    public WorkspaceSeries WithSession(string sessionId) =>
        SessionIds.Contains(sessionId) ? this : this with { SessionIds = [.. SessionIds, sessionId] };

    public WorkspaceSeries WithoutSession(string sessionId) =>
        SessionIds.Contains(sessionId) ? this with { SessionIds = SessionIds.Where(id => id != sessionId).ToList() } : this;

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
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions ReadOptions = new(Options) { PropertyNameCaseInsensitive = true };

    private readonly string path;

    public WorkspaceStore(string dataDirectory)
    {
        path = Path.Combine(dataDirectory, "Workspace", "workspace.json");
    }

    public string Path_ => path;

    public WorkspaceData Load()
    {
        try
        {
            if (!File.Exists(path)) return WorkspaceData.Empty;
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<WorkspaceData>(json, ReadOptions);
            return data is null ? WorkspaceData.Empty : Sanitise(data);
        }
        catch (Exception)
        {
            // A damaged file must not stop the app: an empty workspace is recoverable by hand.
            return WorkspaceData.Empty;
        }
    }

    public void Save(WorkspaceData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data, Options));
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
