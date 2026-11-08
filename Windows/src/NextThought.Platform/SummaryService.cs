using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

public sealed record SummaryProgress(string Stage, string Text, int Completed, int Total, bool Reused = false);

/// <summary>Persist completed chunk summaries so a failed job can resume without discarding prior work.</summary>
public sealed class SummaryService(SessionStore sessions, IAnswerClient client)
{
    private const string Prompt = "请依据会议转写整理 Markdown 纪要，包括：总结、讨论要点、决定、待办（执行人和时间只写明确提及的内容）、未决问题。为重要结论保留原文时间点；不清楚的内容标为待确认，不编造事实。";
    private sealed record Checkpoint
    {
        public string Fingerprint { get; init; } = "";
        public Dictionary<int, string> Parts { get; init; } = [];
    }
    public async Task<string> GenerateAsync(Guid id, AnswerOptions options, Action<SummaryProgress>? progress = null,
        CancellationToken ct = default, int chunkLimit = 24000)
    {
        var data = sessions.Load(id); var meta = data.Meta;
        using var lease = sessions.AcquireLease(id);
        var content = string.Join("\n", data.Lines.Where(x => x.SuspectedEcho != true)
            .Select(x => $"[{(x.Date - meta.StartedAt).TotalSeconds:F0}s] {x.Speaker}：{x.Text}"));
        if (string.IsNullOrWhiteSpace(content)) throw new InvalidOperationException("这场会议还没有可整理的转写。");
        var chunks = SplitText(content, chunkLimit);
        var signature = JsonSerializer.Serialize(new { version = 1, endpoint = ChatCompletionClient.Endpoint(options.BaseUrl).AbsoluteUri,
            options.Model, options.ReasoningEffort, chunkLimit, meta.Title, meta.StartedAt, content });
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        var checkpointPath = Path.Combine(sessions.Folder(id), "summary-job.json");
        var job = new Checkpoint { Fingerprint = fingerprint };
        if (meta.SummaryState is "pending" or "failed" && File.Exists(checkpointPath))
        {
            try
            {
                var previous = JsonSerializer.Deserialize<Checkpoint>(await File.ReadAllTextAsync(checkpointPath, ct).ConfigureAwait(false), JsonDefaults.Options);
                if (previous?.Fingerprint == fingerprint) job = previous;
            }
            catch (JsonException) { File.Copy(checkpointPath, checkpointPath + ".invalid-" + Guid.NewGuid().ToString("N") + ".bak"); }
        }
        await sessions.SetSummaryStateAsync(meta, "pending").ConfigureAwait(false);
        try
        {
            options = options with { MaxOutputTokens = options.MaxOutputTokens ?? 6000 };
            for (var i = 0; i < chunks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (job.Parts.TryGetValue(i, out var saved)) { progress?.Invoke(new("复用已完成片段", saved, i + 1, chunks.Count, true)); continue; }
                var index = i;
                var part = await client.AnswerAsync(options, Prompt,
                    $"会议：{meta.Title}\n时间：{meta.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm}\n转写片段 {i + 1}/{chunks.Count}：\n{chunks[i]}",
                    text => progress?.Invoke(new("整理片段", text, index, chunks.Count)), ct, 180).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested(); job.Parts[i] = part;
                await AppPaths.AtomicWriteAsync(checkpointPath, JsonSerializer.Serialize(job, JsonDefaults.Options), ct).ConfigureAwait(false);
                progress?.Invoke(new("片段已保存", part, i + 1, chunks.Count));
            }
            IReadOnlyList<string> parts = Enumerable.Range(0, chunks.Count).Select(i => job.Parts[i]).ToArray();
            var round = 0;
            while (parts.Count > 1)
            {
                if (++round > 12) throw new InvalidDataException("分段纪要未能收敛，请缩短会议范围后重试。");
                var batches = SplitText(string.Join("\n\n", parts), chunkLimit);
                var merged = new List<string>();
                foreach (var batch in batches)
                {
                    merged.Add(await client.AnswerAsync(options, Prompt + "合并以下分段纪要，消除重复并压缩篇幅，保留原文时间引用。", batch,
                        text => progress?.Invoke(new("合并纪要", text, chunks.Count, chunks.Count)), ct, 180).ConfigureAwait(false));
                }
                parts = merged;
            }
            ct.ThrowIfCancellationRequested(); var markdown = parts[0];
            await sessions.SaveSummaryAsync(meta, markdown, options.Model, ct).ConfigureAwait(false);
            progress?.Invoke(new("纪要已保存", markdown, chunks.Count, chunks.Count)); return markdown;
        }
        catch (OperationCanceledException)
        {
            await SetFailureAsync(meta, data.Summary.Length > 0 ? "ready" : "failed", "纪要整理已取消，可重试并复用已完成片段。").ConfigureAwait(false); throw;
        }
        catch (Exception ex) { await SetFailureAsync(meta, "failed", ex.Message).ConfigureAwait(false); throw; }
    }
    private async Task SetFailureAsync(SessionMeta meta, string state, string message)
    {
        try { await sessions.SetSummaryStateAsync(meta, state, message).ConfigureAwait(false); }
        catch (IOException) { /* Preserve the original exception. The pending job is recovered on restart. */ }
        catch (UnauthorizedAccessException) { }
    }
    public static IReadOnlyList<string> SplitText(string text, int limit)
    {
        if (limit < 32) throw new ArgumentOutOfRangeException(nameof(limit));
        var result = new List<string>(); var offset = 0;
        while (offset < text.Length)
        {
            var count = Math.Min(limit, text.Length - offset);
            if (offset + count < text.Length)
            {
                var newline = text.LastIndexOf('\n', offset + count - 1, count);
                if (newline > offset + count / 2) count = newline - offset + 1;
                if (char.IsHighSurrogate(text[offset + count - 1])) count--;
            }
            result.Add(text.Substring(offset, count)); offset += count;
        }
        return result;
    }
}
