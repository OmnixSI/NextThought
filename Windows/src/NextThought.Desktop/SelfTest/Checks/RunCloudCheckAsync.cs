namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunCloudCheckAsync(string path)
    {
        await Task.Delay(100);
        var soniox = NextThought.Core.CloudSTTProvider.Soniox.ConfigurationMessage("", "auto", ["NextThought"]);
        var assembler = new NextThought.Core.CloudTranscriptAssembler(NextThought.Core.CloudSTTProvider.Soniox);
        var updates = assembler.Consume("""{"tokens":[{"text":"你","is_final":true},{"text":"好","is_final":false}]}""");
        var resampled = NextThought.Core.PCMResampler.Upsample16kTo24k([0, 30, 60]);
        var passed = soniox is not null && soniox.Contains("language_hints", StringComparison.Ordinal)
                     && updates.Count == 1 && updates[0].Text == "你好" && !updates[0].IsFinal
                     && resampled.Length == 4
                     && NextThought.Core.CloudSTTProvider.Deepgram.AuthorizationHeader("k") == "Token k";
        var report = new { passed, configuration = soniox, live = updates.Count > 0 ? updates[0].Text : "", resampled = resampled.Length };
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
        Console.WriteLine(passed ? "cloud-check: passed" : "cloud-check: failed");
        Close();
    }

    /// <summary>Exercises the Codex protocol helpers, SSE parsing and model list in the shipped process.</summary>
}
