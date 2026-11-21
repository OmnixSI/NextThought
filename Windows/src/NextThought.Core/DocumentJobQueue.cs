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
    private readonly Dictionary<string, DateTimeOffset> latest = new(StringComparer.Ordinal);
    private readonly HashSet<string> deleted = new(StringComparer.Ordinal);
    private bool corrupt;

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
            latest[job.Id] = job.UpdatedAt;
            if (jobs.Count > MaximumJobs)
            {
                // B20: never silently discard unfinished work; only old terminal rows may be pruned.
                foreach (var stale in jobs.Where(item => item.IsTerminal).OrderBy(item => item.UpdatedAt)
                             .Take(jobs.Count - MaximumJobs).ToList())
                    jobs.Remove(stale);
            }
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
            if (deleted.Contains(job.Id)) return false;
            if (latest.TryGetValue(job.Id, out var savedAt) && savedAt > job.UpdatedAt) return true;
            var index = jobs.FindIndex(item => item.Id == job.Id);
            if (index < 0) return false;
            jobs[index] = job;
            latest[job.Id] = job.UpdatedAt;
            Save();
            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (gate)
        {
            var removed = jobs.RemoveAll(job => job.Id == id) > 0;
            if (removed)
            {
                deleted.Add(id);
                latest[id] = DateTimeOffset.MaxValue;
                Save();
            }
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

    /// <summary>B20: reopen-time recovery; unfinished stages restart at Queued but keep progress.</summary>
    public IReadOnlyList<DocumentJob> AllJobs(bool recoverInterrupted)
    {
        lock (gate)
        {
            if (!recoverInterrupted) return jobs.ToList();
            var changed = false;
            jobs = jobs.Select(job =>
            {
                if (job.IsTerminal || job.Stage is DocumentJob.StageValue.Failed or DocumentJob.StageValue.Cancelled)
                    return job;
                // A committed import resumes where it stopped; only uncommitted work restarts.
                if (job.DocumentId is { Length: > 0 } && job.Stage is DocumentJob.StageValue.Embedding
                    or DocumentJob.StageValue.Ready or DocumentJob.StageValue.Warning)
                    return job;
                changed = true;
                return job with { Stage = DocumentJob.StageValue.Queued };
            }).ToList();
            if (changed) Save();
            return jobs.ToList();
        }
    }

    /// <summary>B20: delete rows and remember tombstones so a late save cannot resurrect them.</summary>
    public bool Delete(IReadOnlyCollection<string> ids)
    {
        lock (gate)
        {
            if (ids.Count == 0) return false;
            var removed = 0;
            foreach (var id in ids)
            {
                removed += jobs.RemoveAll(job => job.Id == id);
                deleted.Add(id);
                latest[id] = DateTimeOffset.MaxValue;
            }
            if (removed == 0) return false;
            Save();
            return true;
        }
    }

    /// <summary>B20: restore rows deleted in a staged workspace-deletion transaction.</summary>
    public bool RestoreDeleted(IEnumerable<DocumentJob> restored)
    {
        lock (gate)
        {
            var changed = false;
            foreach (var job in restored)
            {
                deleted.Remove(job.Id);
                latest[job.Id] = job.UpdatedAt;
                if (jobs.Any(item => item.Id == job.Id)) continue;
                jobs.Add(job);
                changed = true;
            }
            if (!changed) return false;
            jobs = jobs.OrderBy(item => item.CreatedAt).ToList();
            Save();
            return true;
        }
    }

    private List<DocumentJob> Load()
    {
        if (!File.Exists(path)) return [];
        try
        {
            var loaded = JsonSerializer.Deserialize<List<DocumentJob>>(File.ReadAllText(path), Options) ?? [];
            foreach (var job in loaded) latest[job.Id] = job.UpdatedAt;
            return loaded;
        }
        catch (JsonException)
        {
            // B20: a damaged queue file is evidence, not something to overwrite with an empty queue.
            corrupt = true;
            return [];
        }
    }

    private void Save()
    {
        if (corrupt) throw new InvalidDataException("Document job queue file is damaged; refusing to overwrite it.");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(jobs, Options));
        File.Move(temporary, path, overwrite: true);
    }
}
