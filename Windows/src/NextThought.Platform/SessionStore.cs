using System.Text;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>Append-only transcript/answer files and atomic metadata, compatible with the upstream folder layout.</summary>
public sealed class SessionStore(AppPaths paths)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public string Folder(Guid id) => Path.Combine(paths.Sessions, id.ToString().ToUpperInvariant());
    public async Task CreateAsync(SessionMeta meta, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var folder = Folder(meta.Id);
            if (Directory.Exists(folder)) throw new IOException("会议 ID 已存在。");
            Directory.CreateDirectory(folder); await WriteMetaAsync(meta, ct).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    public async Task AppendTranscriptAsync(SessionMeta meta, TranscriptLine line, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { await AppendAsync(Path.Combine(Folder(meta.Id), "transcript.jsonl"), line, ct).ConfigureAwait(false); meta.TranscriptCount++; await WriteMetaAsync(meta, ct).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public async Task AppendAnswerAsync(SessionMeta meta, AnswerRecord answer, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { await AppendAsync(Path.Combine(Folder(meta.Id), "answers.jsonl"), answer, ct).ConfigureAwait(false); meta.AnswerCount++; await WriteMetaAsync(meta, ct).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public async Task EndAsync(SessionMeta meta, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { meta.EndedAt ??= DateTimeOffset.UtcNow; await WriteMetaAsync(meta, ct).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public async Task SaveSummaryAsync(SessionMeta meta, string markdown, string model, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var summary = new { title = meta.Title, markdown, questions = Array.Empty<object>(), generatedAt = DateTimeOffset.UtcNow, model };
            await AppPaths.AtomicWriteAsync(Path.Combine(Folder(meta.Id), "summary.json"), JsonSerializer.Serialize(summary, JsonDefaults.ArchiveOptions), ct).ConfigureAwait(false);
            meta.SummaryState = "ready"; meta.SummaryError = null; await WriteMetaAsync(meta, ct).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    public async Task SetSummaryStateAsync(SessionMeta meta, string state, string? error = null)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try { meta.SummaryState = state; meta.SummaryError = error; await WriteMetaAsync(meta).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public IReadOnlyList<SessionMeta> List()
    {
        gate.Wait();
        try { return ListCore(); }
        finally { gate.Release(); }
    }
    private IReadOnlyList<SessionMeta> ListCore()
    {
        if (!Directory.Exists(paths.Sessions)) return [];
        return Directory.EnumerateDirectories(paths.Sessions).Select(folder =>
        {
            var file = Path.Combine(folder, "meta.json");
            if (!File.Exists(file)) return null;
            try { return JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(file), JsonDefaults.Options); }
            catch (JsonException) { return null; }
        }).OfType<SessionMeta>().OrderByDescending(x => x.StartedAt).ToArray();
    }
    public SessionData Load(Guid id)
    {
        gate.Wait();
        try { return LoadCore(id); }
        finally { gate.Release(); }
    }
    private SessionData LoadCore(Guid id)
    {
        var folder = Folder(id);
        var meta = JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(Path.Combine(folder, "meta.json")), JsonDefaults.Options) ?? throw new InvalidDataException("会议信息无法读取。");
        var summary = ""; var summaryPath = Path.Combine(folder, "summary.json");
        if (File.Exists(summaryPath)) { using var doc = JsonDocument.Parse(File.ReadAllText(summaryPath)); summary = doc.RootElement.GetProperty("markdown").GetString() ?? ""; }
        return new(meta, ReadLines<TranscriptLine>(Path.Combine(folder, "transcript.jsonl")), ReadLines<AnswerRecord>(Path.Combine(folder, "answers.jsonl")), summary);
    }
    public async Task ExportMarkdownAsync(Guid id, string destination, CancellationToken ct = default)
    {
        var data = Load(id); var builder = new StringBuilder().AppendLine("# " + data.Meta.Title).AppendLine().AppendLine(data.Summary);
        builder.AppendLine().AppendLine("## 转写");
        foreach (var line in data.Lines)
            builder.AppendLine($"- [{line.Date.ToLocalTime():HH:mm:ss}] {(line.Speaker == "me" ? "我" : line.Speaker == "room" ? "现场" : "对方")}：{line.Text}");
        if (data.Answers.Count > 0) builder.AppendLine().AppendLine("## 问答");
        foreach (var answer in data.Answers) builder.AppendLine().AppendLine("### " + answer.Question).AppendLine().AppendLine(answer.Answer);
        await File.WriteAllTextAsync(destination, builder.ToString(), new UTF8Encoding(false), ct);
    }
    private Task WriteMetaAsync(SessionMeta meta, CancellationToken ct = default) => AppPaths.AtomicWriteAsync(Path.Combine(Folder(meta.Id), "meta.json"), JsonSerializer.Serialize(meta, JsonDefaults.ArchiveOptions), ct);
    private static Task AppendAsync<T>(string path, T item, CancellationToken ct) => File.AppendAllTextAsync(path, JsonSerializer.Serialize(item, JsonDefaults.ArchiveOptions) + "\n", new UTF8Encoding(false), ct);
    private static IReadOnlyList<T> ReadLines<T>(string file)
    {
        if (!File.Exists(file)) return [];
        var lines = File.ReadAllLines(file); var results = new List<T>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            try { if (JsonSerializer.Deserialize<T>(lines[i], JsonDefaults.Options) is { } item) results.Add(item); }
            catch (JsonException) when (i == lines.Length - 1) { /* Recover a torn final append after an abrupt shutdown. */ }
        }
        return results;
    }
}
