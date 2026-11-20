namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunGlossarySearchCheckAsync(string path)
    {
        await Task.Delay(100);
        var glossary = new NextThought.Core.Glossary([
            new NextThought.Core.GlossaryEntry("Kubernetes", variants: ["K8s"]),
            new NextThought.Core.GlossaryEntry("Open AI", variants: ["open-ai"])
        ]).Normalized();
        var search = glossary.Search("k8s 部署");
        var canonicalHit = glossary.Search("Kubernetes").FirstMatch("k8s cluster");
        var passed = search.Forms.Contains("Kubernetes 部署")
                     && search.Matches("Kubernetes 部署")
                     && canonicalHit is { Form: "K8s", IsMisheard: true }
                     && glossary.EntryMatching("open ai")?.Canonical == "Open AI";
        var report = new { passed, forms = search.Forms, misheard = canonicalHit?.IsMisheard ?? false, canonical = canonicalHit?.Form ?? "" };
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
        Console.WriteLine(passed ? "glossary-search-check: passed" : "glossary-search-check: failed");
        Close();
    }

    /// <summary>Reads lines from a stream, waits for a child process and awaits a shared task.</summary>
}
