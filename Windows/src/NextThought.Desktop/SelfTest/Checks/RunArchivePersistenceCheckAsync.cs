namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunArchivePersistenceCheckAsync(string path)
    {
        await Task.Delay(100);
        var directory = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "archive");
        Directory.CreateDirectory(directory);
        var marker = Path.Combine(directory, ".sessions-archive");
        NextThought.Core.SessionArchiveContentMarker.Record(marker);
        var transcript = Path.Combine(directory, "transcript.jsonl");
        var appender = NextThought.Core.SessionArchiveFileAppender.Live();
        appender.AppendLine("{\"text\":\"一\"}", transcript);
        appender.AppendLine("{\"text\":\"二\"}", transcript);
        appender.Replace(System.Text.Encoding.UTF8.GetBytes("{}"), Path.Combine(directory, "meta.json"));
        var receipts = new NextThought.Core.SessionTranslationWriteReceipts();
        receipts.RecordSubmittedRevision(3);
        var acknowledged = receipts.Acknowledge(3);
        var passed = NextThought.Core.SessionArchiveContentMarker.WasPreviouslyPopulated(marker)
                     && File.ReadAllLines(transcript).Length == 2
                     && File.ReadAllText(Path.Combine(directory, "meta.json")) == "{}"
                     && acknowledged && !receipts.HasPending
                     && NextThought.Core.SessionArchiveLeftover.IsLeftover(["notes.json"])
                     && !NextThought.Core.SessionArchiveLeftover.IsLeftover(["transcript.jsonl"]);
        var report = new { passed, lines = File.ReadAllLines(transcript).Length, acknowledged, leftover = true };
        try
        {
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "archive-persistence-check: passed" : "archive-persistence-check: failed");
        Close();
    }

    /// <summary>One process run over the portable Core features added since 0.47.0.</summary>
}
