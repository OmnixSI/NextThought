using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>
/// 一条经验：这场（或这个系列）总结出来、下次还用得上的一句话。
/// Ported from <c>WorkspaceLesson</c> (Workspace.swift) without the revision history.
/// </summary>
public sealed record WorkspaceLesson(string Id, string Text, DateTimeOffset CreatedAt, string? SourceSessionId = null)
{
    public static WorkspaceLesson Create(string text, string? sourceSessionId = null) =>
        new(Guid.NewGuid().ToString("D"), text.Trim(), DateTimeOffset.UtcNow, sourceSessionId);
}

/// <summary>
/// 一个系列：同一条线上的若干次会议（同一门课、同一个项目、同一个岗位的面试），
/// 加上从这些会议里沉淀下来的经验。Port of the series part of <c>Workspace</c>.
/// </summary>
public sealed record WorkspaceSeries(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> SessionIds,
    IReadOnlyList<WorkspaceLesson> Lessons)
{
    public static WorkspaceSeries Create(string name) =>
        new(Guid.NewGuid().ToString("D"), name.Trim(), DateTimeOffset.UtcNow, [], []);

    public WorkspaceSeries WithSession(string sessionId) =>
        SessionIds.Contains(sessionId) ? this : this with { SessionIds = [.. SessionIds, sessionId] };

    public WorkspaceSeries WithoutSession(string sessionId) =>
        SessionIds.Contains(sessionId) ? this with { SessionIds = SessionIds.Where(id => id != sessionId).ToList() } : this;
}

/// <summary>The whole workspace file: series plus which session belongs where.</summary>
public sealed record WorkspaceData(IReadOnlyList<WorkspaceSeries> Series)
{
    public static readonly WorkspaceData Empty = new([]);

    public WorkspaceSeries? SeriesOfSession(string sessionId) =>
        Series.FirstOrDefault(series => series.SessionIds.Contains(sessionId));

    public WorkspaceSeries? Find(string seriesId) => Series.FirstOrDefault(series => series.Id == seriesId);

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

    /// <summary>Older or hand-edited files may repeat ids or point a session at two series.</summary>
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
            series.Add(item with
            {
                SessionIds = sessions,
                Lessons = (item.Lessons ?? []).Where(lesson => !string.IsNullOrWhiteSpace(lesson.Text)).ToList()
            });
        }
        return new WorkspaceData(series);
    }
}
