namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunApiCheckAsync(string path)
    {
        await Task.Delay(100);
        var failures = new[]
        {
            (401, "", NextThought.Core.APIFailure.BadKey),
            (404, "model not found", NextThought.Core.APIFailure.BadModel),
            (429, "insufficient quota", NextThought.Core.APIFailure.OutOfCredit),
            (503, "", NextThought.Core.APIFailure.ServerBusy)
        };
        var passed = failures.All(item => NextThought.Core.APIFailureExplainer.Classify(item.Item1, item.Item2) == item.Item3)
                     && NextThought.Core.APIFailureExplainer.Classify(null, "", -1001) == NextThought.Core.APIFailure.Timeout
                     && NextThought.Core.APIProviders.Normalized(" HTTPS://API.Deepseek.com/v1/ ") == "https://api.deepseek.com/v1"
                     && NextThought.Core.APIProviders.Guide("https://api.deepseek.com/v1")?.SuggestedModel == "deepseek-chat"
                     && NextThought.Core.APIKeyInput.Masked("sk-abcdef123456").EndsWith("3456", StringComparison.Ordinal)
                     && NextThought.Core.AnswerBackendDefault.RawValue(null, codexInstalled: true) == NextThought.Core.AnswerBackendDefault.CodexAppServer;
        var report = new
        {
            passed,
            badKey = NextThought.Core.APIFailureExplainer.Message(NextThought.Core.APIFailure.BadKey),
            normalized = NextThought.Core.APIProviders.Normalized(" HTTPS://API.Deepseek.com/v1/ "),
            masked = NextThought.Core.APIKeyInput.Masked("sk-abcdef123456")
        };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "api-check: passed" : "api-check: failed");
        Close();
    }

    /// <summary>Exercises chunking, the semantic index and rank fusion in the shipped process.</summary>
}
