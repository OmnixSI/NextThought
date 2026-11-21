namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunEvidenceCheckAsync(string path)
    {
        await Task.Delay(100);
        var lineId = Guid.NewGuid();
        var target = new NextThought.Core.SessionStructuredNotesTarget(NextThought.Core.NotesFormat.Outline, "sections[0]", "rev");
        var reference = new NextThought.Core.SessionEvidenceReference(
            "claim:1", "结论", "缓存命中率提升", NextThought.Core.SessionEvidenceSupport.Quoted,
            NextThought.Core.SessionEvidenceOrigin.StructuredNotes, LineId: lineId, Quote: "命中率提升",
            Location: "05:20", StructuredTarget: target, TranscriptSourceRevision: "t1",
            SourceStatus: NextThought.Core.SessionDerivedContentStatus.Current,
            ReviewDecision: NextThought.Core.SessionEvidenceDecision.Supported);
        var passed = reference.Support.Label() == "引文在所选转写版本中找到"
                     && reference.LineId == lineId
                     && reference.StructuredTarget == target
                     && reference.ReviewDecision == NextThought.Core.SessionEvidenceDecision.Supported
                     && !reference.ReviewIsStale
                     && NextThought.Core.SessionEvidenceSupport.Unlocated.Label().Contains("待核对");
        var report = new { passed, label = reference.Support.Label(), review = reference.ReviewDecision.ToString() };
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
        Console.WriteLine(passed ? "evidence-check: passed" : "evidence-check: failed");
        Close();
    }

    /// <summary>Writes and reloads user content, then checks the revision guard.</summary>
}
