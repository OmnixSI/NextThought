namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunDocumentJobCheckAsync(string path)
    {
        await Task.Delay(100);
        var job = NextThought.Core.DocumentJob.Create("ws", "slides.pptx") with
        {
            Stage = NextThought.Core.DocumentJob.StageValue.Embedding,
            Completed = 3,
            Total = 10
        };
        var passed = job.Provider == "system"
                     && job.Stage.Title() == "建立语义索引"
                     && job.ProgressText == "建立语义索引 3/10"
                     && !job.IsTerminal
                     && (NextThought.Core.DocumentJob.StageValue.Ready).Terminal()
                     && NextThought.Core.DocumentLimits.MaximumPages == 2000;
        var report = new { passed, stage = job.Stage.Title(), progress = job.ProgressText, terminal = job.IsTerminal };
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
        Console.WriteLine(passed ? "document-job-check: passed" : "document-job-check: failed");
        Close();
    }

    /// <summary>Pages are built the first time they are shown; hidden sections cost no controls.</summary>
}
