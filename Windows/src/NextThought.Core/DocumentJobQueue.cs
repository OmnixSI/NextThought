using System.Text.Json;

namespace NextThought.Core;

/// <summary>
/// Durable document-job queue backed by one JSON file (the Mac build uses SQLite; Windows imports
/// synchronously, so a small file queue is enough to survive restarts and keep ordering visible).
/// </summary>
public sealed class DocumentJobQueue
{
    public const int MaximumJobs = 200;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string path;
    private readonly object gate = new();
    private List<DocumentJob> jobs;

    public DocumentJobQueue(string directory)
    {
        Directory.CreateDirectory(directory);
        path = System.IO.Path.Combine(directory, "document-jobs.json");
        jobs = Load();
    }

    public string FilePath => path;
    public int Count { get { lock (gate) return jobs.Count; } }

    public IReadOnlyList<DocumentJob> All { get { lock (gate) return jobs.ToList(); } }

    public IReadOnlyList<DocumentJob> Pending => All
        .Where(job => !job.IsTerminal && job.Stage is not (DocumentJob.StageValue.Cancelled or DocumentJob.StageValue.Failed))
        .OrderBy(job => job.CreatedAt).ToList();

    /// <summary>Adds a job unless its id is already queued; returns whether it was added.</summary>
    public bool Enqueue(DocumentJob job)
    {
        lock (gate)
        {
            if (jobs.Any(existing => existing.Id == job.Id)) return false;
            jobs.Add(job);
            if (jobs.Count > MaximumJobs)
                jobs = jobs.OrderByDescending(item => item.UpdatedAt).Take(MaximumJobs).OrderBy(item => item.CreatedAt).ToList();
            Save();
            return true;
        }
    }

    /// <summary>The oldest job that is neither terminal nor cancelled/failed, or null.</summary>
    public DocumentJob? Next()
    {
        lock (gate)
        {
            return jobs.Where(job => !job.IsTerminal
                                     && job.Stage is not (DocumentJob.StageValue.Cancelled or DocumentJob.StageValue.Failed))
                .OrderBy(job => job.CreatedAt).FirstOrDefault();
        }
    }

    /// <summary>Replaces a job by id; returns false when it is unknown.</summary>
    public bool Update(DocumentJob job)
    {
        lock (gate)
        {
            var index = jobs.FindIndex(item => item.Id == job.Id);
            if (index < 0) return false;
            jobs[index] = job;
            Save();
            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (gate)
        {
            var removed = jobs.RemoveAll(job => job.Id == id) > 0;
            if (removed) Save();
            return removed;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            jobs.Clear();
            Save();
        }
    }

    private List<DocumentJob> Load()
    {
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<DocumentJob>>(File.ReadAllText(path), Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Save()
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(jobs, Options));
        File.Move(temporary, path, overwrite: true);
    }
}
