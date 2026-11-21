namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunUserContentCheckAsync(string path)
    {
        await Task.Delay(100);
        var directory = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "user-content");
        var saved = NextThought.Core.SessionUserContentStore.Save(new NextThought.Core.SessionUserContent
        {
            PersonalNotes = "我的笔记",
            Highlights = [new NextThought.Core.SessionHighlight { LineId = Guid.NewGuid(), OffsetSeconds = 30, Quote = "重点" }]
        }, directory);
        var loaded = NextThought.Core.SessionUserContentStore.Load(directory);
        var refused = false;
        try
        {
            NextThought.Core.SessionUserContentStore.Save(new NextThought.Core.SessionUserContent { PersonalNotes = "旧草稿" }, directory);
        }
        catch (NextThought.Core.SessionUserContentStore.ChangedElsewhereException)
        {
            refused = true;
        }
        var passed = saved.Revision == 1 && loaded.Revision == 1
                     && loaded.PersonalNotes == "我的笔记" && loaded.Highlights.Count == 1
                     && loaded.HasContent && refused;
        var report = new { passed, revision = loaded.Revision, refused, notes = loaded.PersonalNotes };
        try
        {
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "user-content-check: passed" : "user-content-check: failed");
        Close();
    }

    /// <summary>Stages workspace files, rolls them back, then runs the authorized transaction.</summary>
}
