using System.Text;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>Append-only transcript/answer files and atomic metadata, compatible with the upstream folder layout.</summary>
public sealed class SessionStore(AppPaths paths)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private sealed record MacTranslation(Guid Id, string Text, string? InputRevision);
    public string Folder(Guid id) => Path.Combine(paths.Sessions, id.ToString().ToUpperInvariant());

    /// <summary>Deletes one archived meeting folder. Missing folders are harmless.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var folder = Folder(id);
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(folder, true);
        }
        finally { gate.Release(); }
    }

    public FileStream AcquireLease(Guid id) => new(Path.Combine(Folder(id), ".active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    public Task CreateAsync(SessionMeta meta, CancellationToken ct = default) => CreateAsync(meta, null, ct);

    public async Task CreateAsync(SessionMeta meta, SessionContextSnapshot? context, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var folder = Folder(meta.Id);
            if (Directory.Exists(folder)) throw new IOException("会议 ID 已存在。");
            Directory.CreateDirectory(folder); await WriteMetaAsync(meta, ct).ConfigureAwait(false);
            // Mac writes a context snapshot per meeting; callers can pass the full input snapshot.
            context ??= new SessionContextSnapshot { Mode = meta.Mode };
            await File.WriteAllTextAsync(Path.Combine(folder, "context.json"),
                JsonSerializer.Serialize(context, JsonDefaults.ArchiveOptions), ct).ConfigureAwait(false);
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
    /// <summary>
    /// Replaces the transcript file with corrected lines (proofreading). Written atomically so a
    /// crash cannot leave half a meeting.
    /// </summary>
    public async Task RewriteTranscriptAsync(SessionMeta meta, IReadOnlyList<TranscriptLine> lines, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = Path.Combine(Folder(meta.Id), "transcript.jsonl");
            // Keep the raw recognizer output recoverable; proofreading must not destroy it.
            var originalPath = Path.Combine(Folder(meta.Id), "transcript.original.jsonl");
            if (File.Exists(path) && !File.Exists(originalPath)) File.Copy(path, originalPath);
            var builder = new System.Text.StringBuilder();
            foreach (var line in lines)
                builder.Append(JsonSerializer.Serialize(line, JsonDefaults.ArchiveOptions)).Append('\n');
            await AppPaths.AtomicWriteAsync(path, builder.ToString(), ct).ConfigureAwait(false);
            var current = ReadMeta(meta.Id);
            current.TranscriptCount = lines.Count;
            await WriteMetaAsync(current, ct).ConfigureAwait(false);
            CopyState(current, meta);
        }
        finally { gate.Release(); }
    }

    public async Task AppendQuestionAsync(Guid session, SessionQuestionEvent question, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { await AppendAsync(Path.Combine(Folder(session), "questions.jsonl"), question, ct).ConfigureAwait(false); }
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
    public async Task AppendVoiceTagAsync(Guid session, Guid line, int voice, CancellationToken ct = default)
    {
        if (voice is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(voice));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { await AppendAsync(Path.Combine(Folder(session), "voices.jsonl"), new SessionVoiceTag(line, voice), ct).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public async Task SaveSummaryAsync(SessionMeta meta, string markdown, string model, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var summaryPath = Path.Combine(Folder(meta.Id), "summary.json");
            System.Text.Json.Nodes.JsonObject summary;
            try
            {
                summary = (File.Exists(summaryPath)
                    ? System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(summaryPath, ct).ConfigureAwait(false)) as System.Text.Json.Nodes.JsonObject
                    : null) ?? new System.Text.Json.Nodes.JsonObject();
            }
            catch (System.Text.Json.JsonException) { summary = new System.Text.Json.Nodes.JsonObject(); }
            // Re-summarizing must keep Mac-only groups (structured notes, evidence, experience, question bank).
            summary["title"] = meta.Title;
            summary["markdown"] = markdown;
            summary["generatedAt"] = DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
            summary["model"] = model;
            if (summary["questions"] is not System.Text.Json.Nodes.JsonArray { Count: > 0 })
            {
                // Mac fills questions from the model summary; Windows has answer records, which are
                // the same user-visible pairing (question + short answer gist).
                var questions = new System.Text.Json.Nodes.JsonArray();
                var answersPath = Path.Combine(Folder(meta.Id), "answers.jsonl");
                if (File.Exists(answersPath))
                {
                    foreach (var line in File.ReadLines(answersPath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            var answer = JsonSerializer.Deserialize<AnswerRecord>(line, JsonDefaults.Options);
                            if (answer is null || string.IsNullOrWhiteSpace(answer.Question)) continue;
                            var gist = answer.Answer.Length <= 240 ? answer.Answer : answer.Answer[..240] + "…";
                            questions.Add(new System.Text.Json.Nodes.JsonObject
                            {
                                ["id"] = answer.Id.ToString("D"),
                                ["text"] = answer.Question,
                                ["answerGist"] = gist
                            });
                        }
                        catch (JsonException) { }
                    }
                }
                summary["questions"] = questions;
            }
            await AppPaths.AtomicWriteAsync(summaryPath, summary.ToJsonString(JsonDefaults.ArchiveOptions), ct).ConfigureAwait(false);
            var current = ReadMeta(meta.Id); current.SummaryState = "ready"; current.SummaryError = null;
            await WriteMetaAsync(current, ct).ConfigureAwait(false); CopyState(current, meta);
        }
        finally { gate.Release(); }
    }
    /// <summary>Writes the lessons/bank a template summary carried, before the user accepts them.</summary>
    public async Task SaveSummaryKnowledgeAsync(SessionMeta meta, IReadOnlyList<string> lessons,
        IReadOnlyList<QuestionBankEntry> bank, bool appliedToWorkspace, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var summaryPath = Path.Combine(Folder(meta.Id), "summary.json");
            System.Text.Json.Nodes.JsonObject summary;
            try
            {
                summary = (File.Exists(summaryPath)
                    ? System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(summaryPath, ct).ConfigureAwait(false)) as System.Text.Json.Nodes.JsonObject
                    : null) ?? new System.Text.Json.Nodes.JsonObject();
            }
            catch (JsonException) { summary = new System.Text.Json.Nodes.JsonObject(); }
            summary["lessons"] = new System.Text.Json.Nodes.JsonArray(lessons.Select(text => System.Text.Json.Nodes.JsonValue.Create(text)).ToArray());
            summary["bank"] = new System.Text.Json.Nodes.JsonArray(bank.Select(entry => new System.Text.Json.Nodes.JsonObject
            {
                ["question"] = entry.Question,
                ["answer"] = entry.Answer
            }).ToArray());
            summary["appliedToWorkspace"] = appliedToWorkspace;
            await AppPaths.AtomicWriteAsync(summaryPath, summary.ToJsonString(JsonDefaults.ArchiveOptions), ct).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task AppendTranslationAsync(Guid session, SessionTranslation translation, CancellationToken ct = default)
    {
        if (translation.Revision < 1 || translation.Direction is not ("zh-en" or "en-zh") || translation.Text.Length > 32000)
            throw new InvalidDataException("译文格式无效。");
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // A late completion must not attach to a revised or missing source line.
            var line = ReadLines<TranscriptLine>(Path.Combine(Folder(session), "transcript.jsonl")).LastOrDefault(l => l.Id == translation.Id);
            if (line is null || MeetingTranslator.SourceHash(line.Text) != translation.SourceHash) return;
            await AppendAsync(Path.Combine(Folder(session), "translations.jsonl"), translation with { InputRevision = translation.InputRevision ?? translation.SourceHash }, ct).ConfigureAwait(false);
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
        var voices = ReadLines<SessionVoiceTag>(Path.Combine(folder, "voices.jsonl")).Where(v => v.Voice is >= 0 and < 16).GroupBy(v => v.Id).ToDictionary(g => g.Key, g => g.Last().Voice);
        var lines = ReadLines<TranscriptLine>(Path.Combine(folder, "transcript.jsonl")).Select(l => voices.TryGetValue(l.Id, out var voice) ? l with { Voice = voice } : l).ToArray();
        var translationPath = Path.Combine(folder, "translations.jsonl");
        var translations = ReadLines<SessionTranslation>(translationPath).GroupBy(t => (t.Id, t.SourceHash)).ToDictionary(g => g.Key, g => g.Last());
        // Mac writes {id, text, inputRevision}; it has no Windows SourceHash. Match by line id and
        // expose the text through TranslatedText/Translation so the transcript row renders it.
        var macTranslations = ReadLines<MacTranslation>(translationPath)
            .GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.Last());
        lines = lines.Select(l =>
        {
            if (translations.TryGetValue((l.Id, MeetingTranslator.SourceHash(l.Text)), out var translation))
                return l with { Translation = translation, TranslatedText = translation.Text, TranslationInputRevision = translation.InputRevision };
            if (macTranslations.TryGetValue(l.Id, out var mac))
                return l with
                {
                    TranslatedText = mac.Text,
                    TranslationInputRevision = mac.InputRevision,
                    Translation = new SessionTranslation(mac.Id, 1, "", "", mac.Text, "", l.Date, mac.InputRevision)
                };
            return l;
        }).ToArray();
        return new(meta, lines, ReadLines<AnswerRecord>(Path.Combine(folder, "answers.jsonl")), summary);
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
        await File.WriteAllTextAsync(destination, BuildMarkdown(Load(id)), new UTF8Encoding(false), ct);
    }

    /// <summary>Word export of the same meeting (headings, lists, quotes).</summary>
    public Task ExportDocxAsync(Guid id, string destination, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        DocxWriter.Write(BuildMarkdown(Load(id)), destination);
        return Task.CompletedTask;
    }

    /// <summary>PDF export of the same meeting (A4, embedded CJK font).</summary>
    public Task ExportPdfAsync(Guid id, string destination, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var data = Load(id);
        PdfWriter.Write(BuildMarkdown(data), destination, data.Meta.Title);
        return Task.CompletedTask;
    }

    private static string BuildMarkdown(SessionData data)
    {
        var builder = new StringBuilder().AppendLine("# " + data.Meta.Title).AppendLine().AppendLine(data.Summary);
        builder.AppendLine().AppendLine("## 转写");
        foreach (var line in data.Lines)
        {
            builder.AppendLine($"- [{line.Date.ToLocalTime():HH:mm:ss}] {(line.Speaker == "me" ? "我" : line.Voice is { } voice ? $"说话人{voice + 1}" : line.Speaker == "room" ? "现场" : "对方")}：{line.Text}");
            if (line.Translation is { } t) builder.AppendLine($"  - {(t.Direction == "zh-en" ? "英译文" : "中译文")}：{t.Text}");
        }
        if (data.Answers.Count > 0) builder.AppendLine().AppendLine("## 问答");
        foreach (var answer in data.Answers)
        {
            builder.AppendLine().AppendLine("### " + answer.Question).AppendLine().AppendLine(answer.Answer);
            foreach (var source in answer.Sources ?? []) builder.AppendLine().AppendLine($"[{source.Label}] {source.DocumentName} · {source.Location}  ").AppendLine($"SHA-256: {source.Sha256}").AppendLine().AppendLine("> " + source.Quote.Replace("\n", "\n> "));
        }
        return builder.ToString();
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
                            + RepairFinalAppend<AnswerRecord>(Path.Combine(Folder(meta.Id), "answers.jsonl"))
                            + RepairFinalAppend<SessionVoiceTag>(Path.Combine(Folder(meta.Id), "voices.jsonl"))
                            + RepairFinalAppend<SessionTranslation>(Path.Combine(Folder(meta.Id), "translations.jsonl"));
                        var data = LoadCore(meta.Id); meta.TranscriptCount = data.Lines.Count; meta.AnswerCount = data.Answers.Count;
                        if (!meta.EndedAt.HasValue) meta.EndedAt = data.Lines.Count > 0 ? data.Lines.Max(l => l.EndedAt ?? l.Date) : meta.StartedAt;
                        if (meta.SummaryState == "pending") { meta.SummaryState = "failed"; meta.SummaryError = "上次纪要整理中断，可继续整理并复用已完成片段。"; }
                        meta.AdditionalProperties ??= [];
                        meta.AdditionalProperties["windowsRecoveredAt"] = JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
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
