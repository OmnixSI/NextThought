namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunHotwordsCheckAsync(string directory)
    {
        await Task.Delay(200);
        var error = "";
        IReadOnlyList<string> material = [];
        IReadOnlyList<string> hotwords = [];
        try
        {
            Directory.CreateDirectory(directory);
            var fixture = Path.Combine(directory, "materials.txt");
            await File.WriteAllTextAsync(fixture,
                "The XL-MIMO testbed uses TSIR and DeepMIMO data; RQ2 is open.\nAgain XL-MIMO and TSIR; RQ2 is answered by the DeepMIMO run.\n",
                new System.Text.UTF8Encoding(false));
            await state.Library.ImportAsync([fixture]);
            state.GlossaryText = "Kubernetes | K8s";
            state.SaveGlossaryCommand.Execute(null);
            material = state.MaterialHotwords();
            hotwords = state.RecognitionHotwords();
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        var passed = error.Length == 0
                     && new[] { "XL-MIMO", "TSIR", "DeepMIMO", "RQ2" }.All(term => material.Contains(term))
                     && hotwords.Contains("Kubernetes")
                     && hotwords.Contains("XL-MIMO");
        var report = new
        {
            passed,
            materialTerms = material,
            hotwords,
            error
        };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "hotwords-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }));
        }
        catch { }
        Console.WriteLine(passed ? "hotwords-check: passed" : "hotwords-check: failed");
        Close();
    }

    /// <summary>
    /// Renders a Markdown answer containing inline and display formulas through the real
    /// MarkdownText control, and reports the text the user would see.
    /// </summary>
}
