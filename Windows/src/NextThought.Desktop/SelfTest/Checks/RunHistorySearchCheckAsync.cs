namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunHistorySearchCheckAsync(string path)
    {
        await Task.Delay(100);
        var found = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var terms = new NextThought.Core.Glossary([new NextThought.Core.GlossaryEntry("Kubernetes", variants: ["K8s"])])
            .Normalized().Search("Kubernetes");
        var snapshots = new Dictionary<Guid, NextThought.Core.SessionSearchSnapshot>
        {
            [found] = new([new NextThought.Core.SessionSearchField(NextThought.Core.SessionSearchFieldKind.Transcript,
                "Kubernetes 部署", Location: "12:00", Id: "line:1", LineId: lineId)])
        };
        var progress = NextThought.Core.SessionSearch.Search([found, missing], terms, snapshots);
        var hit = progress.Hits.GetValueOrDefault(found);
        var passed = progress.Checked == 2 && progress.Unreadable == 1
                     && hit is not null && hit.Label() == "转写"
                     && hit.Target() is NextThought.Core.SessionContentTarget.Transcript target && target.LineId == lineId
                     && hit.Snippet().Contains("Kubernetes", StringComparison.Ordinal);
        var report = new { passed, hits = progress.Hits.Count, unreadable = progress.Unreadable, label = hit?.Label() ?? "", target = hit?.Target().GetType().Name ?? "" };
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
        Console.WriteLine(passed ? "history-search-check: passed" : "history-search-check: failed");
        Close();
    }

    /// <summary>Validates a manifest, merges a template and maps legacy settings.</summary>
}
