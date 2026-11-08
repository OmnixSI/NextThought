using System.Diagnostics;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class CodexDiagnostics
{
    public static async Task<int> RunAsync(Func<string, string?> option, CancellationToken ct)
    {
        var client = new CodexAnswerClient(); var options = new AnswerOptions("https://codex.local", "default", "");
        var streamed = 0; var answer = await client.AnswerAsync(options, "Respond with the exact requested text. Do not use any tools.", "只回复：P2连接成功", _ => streamed++, ct, 120);
        bool canceled = false; var clock = Stopwatch.StartNew();
        using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            cancel.CancelAfter(1500);
            try { await client.AnswerAsync(options, "Answer only, no tools.", "列出一千个不同的数字并逐项解释。", ct: cancel.Token, timeoutSeconds: 120); }
            catch (OperationCanceledException) { canceled = true; }
        }
        var cancelSeconds = clock.Elapsed.TotalSeconds;
        var recovered = await client.AnswerAsync(options, "Answer only, no tools.", "只回复：恢复成功", ct: ct, timeoutSeconds: 120);
        var passed = answer.Contains("P2连接成功") && streamed > 0 && canceled && cancelSeconds < 10 && recovered.Contains("恢复成功");
        await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, answer, streamed, canceled, cancelSeconds, recovered }, JsonDefaults.Options), ct);
        Console.WriteLine("Codex stdio and recovery: " + (passed ? "PASS" : "FAIL")); return passed ? 0 : 2;
    }
}
