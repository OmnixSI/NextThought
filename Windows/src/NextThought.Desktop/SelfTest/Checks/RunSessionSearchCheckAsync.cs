namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunSessionSearchCheckAsync(string path)
    {
        await Task.Delay(100);
        var glossary = new NextThought.Core.Glossary([
            new NextThought.Core.GlossaryEntry("Kubernetes", variants: ["K8s"])
        ]).Normalized();
        var terms = glossary.Search("Kubernetes");
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var documents = new[]
        {
            new NextThought.Core.SessionSearchDocument(first, [
                new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Transcript,
                    "去年我们把服务迁到 Kubernetes 上，效果不错。", Location: "12:30", Id: "line:1")]),
            new NextThought.Core.SessionSearchDocument(second, [
                new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Notes, "没有相关词", Id: "n")])
        };
        var results = NextThought.Core.SessionContentSearch.Search(documents, terms);
        var hit = results.FirstOrDefault();
        var passed = results.Count == 1
                     && hit is { SessionId: var id } && id == first
                     && hit.Hit.Kind == NextThought.Core.SessionSearchFieldKind.Transcript
                     && hit.Hit.MatchedForm == "Kubernetes"
                     && hit.Hit.Excerpt.Contains("Kubernetes", StringComparison.Ordinal)
                     && hit.Hit.Location == "12:30";
        var report = new { passed, hits = results.Count, kind = hit?.Hit.Kind.ToString() ?? "", excerpt = hit?.Hit.Excerpt ?? "" };
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
        Console.WriteLine(passed ? "session-search-check: passed" : "session-search-check: failed");
        Close();
    }

    /// <summary>Builds a series briefing from synthetic sessions and knowledge.</summary>
}
