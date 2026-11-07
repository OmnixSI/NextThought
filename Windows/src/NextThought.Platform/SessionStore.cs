using System.Text;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>Append-only transcript/answer files and atomic metadata, compatible with the upstream folder layout.</summary>
public sealed class SessionStore(AppPaths paths)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public string Folder(Guid id) => Path.Combine(paths.Sessions, id.ToString().ToUpperInvariant());
    public FileStream AcquireLease(Guid id) => new(Path.Combine(Folder(id), ".active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
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
        try
        {
            await AppendAsync(Path.Combine(Folder(meta.Id), "transcript.jsonl"), line, ct).ConfigureAwait(false);
            var current = ReadMeta(meta.Id); current.TranscriptCount++;
            await WriteMetaAsync(current, ct).ConfigureAwait(false); CopyState(current, meta);
        }
        finally { gate.Release(); }
    }
    public async Task AppendAnswerAsync(SessionMeta meta, AnswerRecord answer, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await AppendAsync(Path.Combine(Folder(meta.Id), "answers.jsonl"), answer, ct).ConfigureAwait(false);
            var current = ReadMeta(meta.Id); current.AnswerCount++;
            await WriteMetaAsync(current, ct).ConfigureAwait(false); CopyState(current, meta);
        }
        finally { gate.Release(); }
    }
    public async Task EndAsync(SessionMeta meta, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = ReadMeta(meta.Id); current.EndedAt ??= DateTimeOffset.UtcNow;
            await WriteMetaAsync(current, ct).ConfigureAwait(false); CopyState(current, meta);
        }
        finally { gate.Release(); }
    }
    public async Task SaveSummaryAsync(SessionMeta meta, string markdown, string model, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var summary = new { title = meta.Title, markdown, questions = Array.Empty<object>(), generatedAt = DateTimeOffset.UtcNow, model };
            await AppPaths.AtomicWriteAsync(Path.Combine(Folder(meta.Id), "summary.json"), JsonSerializer.Serialize(summary, JsonDefaults.ArchiveOptions), ct).ConfigureAwait(false);
            var current = ReadMeta(meta.Id); current.SummaryState = "ready"; current.SummaryError = null;
            await WriteMetaAsync(current, ct).ConfigureAwait(false); CopyState(current, meta);
        }
        finally { gate.Release(); }
    }
    public async Task SetSummaryStateAsync(SessionMeta meta, string state, string? error = null)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var current = ReadMeta(meta.Id); current.SummaryState = state; current.SummaryError = error;
            await WriteMetaAsync(current).ConfigureAwait(false); CopyState(current, meta);
        }
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
    private SessionMeta ReadMeta(Guid id) => JsonSerializer.Deserialize<SessionMeta>(
        File.ReadAllText(Path.Combine(Folder(id), "meta.json")), JsonDefaults.Options) ?? throw new InvalidDataException("会议信息无法读取。");
    private static void CopyState(SessionMeta source, SessionMeta destination)
    {
        destination.EndedAt = source.EndedAt; destination.TranscriptCount = source.TranscriptCount;
        destination.AnswerCount = source.AnswerCount; destination.SummaryState = source.SummaryState; destination.SummaryError = source.SummaryError;
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
    public async Task<IReadOnlyList<SessionRecovery>> RecoverInterruptedAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        var recovered = new List<SessionRecovery>();
        try
        {
            foreach (var meta in ListCore())
            {
                ct.ThrowIfCancellationRequested();
                if (meta.EndedAt.HasValue && meta.SummaryState != "pending") continue;
                // New sessions leave a lease marker. Do not automatically change legacy sessions from older apps.
                if (!File.Exists(Path.Combine(Folder(meta.Id), ".active.lock"))) continue;
                FileStream lease;
                try { lease = AcquireLease(meta.Id); } catch (IOException) { continue; }
                using (lease)
                {
                    try
                    {
                        var repaired = RepairFinalAppend<TranscriptLine>(Path.Combine(Folder(meta.Id), "transcript.jsonl"))
                            + RepairFinalAppend<AnswerRecord>(Path.Combine(Folder(meta.Id), "answers.jsonl"));
                        var data = LoadCore(meta.Id); meta.TranscriptCount = data.Lines.Count; meta.AnswerCount = data.Answers.Count;
                        if (!meta.EndedAt.HasValue) meta.EndedAt = data.Lines.Count > 0 ? data.Lines.Max(l => l.EndedAt ?? l.Date) : meta.StartedAt;
                        if (meta.SummaryState == "pending") { meta.SummaryState = "failed"; meta.SummaryError = "上次纪要整理中断，可继续整理并复用已完成片段。"; }
                        meta.AdditionalProperties ??= [];
                        meta.AdditionalProperties["windowsRecoveredAt"] = JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow, JsonDefaults.Options);
                        var metadata = Path.Combine(Folder(meta.Id), "meta.json");
                        File.Copy(metadata, metadata + ".recovery-" + Guid.NewGuid().ToString("N") + ".bak");
                        await WriteMetaAsync(meta, ct).ConfigureAwait(false);
                        recovered.Add(new(meta.Id, meta.Title, meta.TranscriptCount, repaired));
                    }
                    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
                    { recovered.Add(new(meta.Id, meta.Title, meta.TranscriptCount, 0, ex.Message)); }
                }
            }
        }
        finally { gate.Release(); }
        return recovered;
    }
    private static int RepairFinalAppend<T>(string file)
    {
        if (!File.Exists(file)) return 0;
        var bytes = File.ReadAllBytes(file); var offset = 0; var valid = 0;
        while (offset < bytes.Length)
        {
            var newline = Array.IndexOf(bytes, (byte)'\n', offset); var end = newline < 0 ? bytes.Length : newline;
            var text = Encoding.UTF8.GetString(bytes, offset, end - offset).Trim();
            try { if (text.Length > 0) _ = JsonSerializer.Deserialize<T>(text, JsonDefaults.Options); }
            catch (JsonException)
            {
                var remainder = Encoding.UTF8.GetString(bytes, end, bytes.Length - end);
                if (!string.IsNullOrWhiteSpace(remainder)) throw;
                File.Copy(file, file + ".recovery-" + Guid.NewGuid().ToString("N") + ".bak");
                File.WriteAllBytes(file, bytes.AsSpan(0, valid).ToArray()); return 1;
            }
            offset = newline < 0 ? bytes.Length : newline + 1; valid = offset;
        }
        return 0;
    }
}

public sealed record SessionRecovery(Guid Id, string Title, int TranscriptCount, int RepairedFiles, string? Error = null);
