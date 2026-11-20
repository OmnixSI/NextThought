namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunNotesJumpCheckAsync(string path)
    {
        state.Section = AppSection.Notes;
        await Task.Delay(500);
        var fishbone = this.GetVisualDescendants().OfType<FishboneView>().FirstOrDefault();
        var passed = false;
        var selected = false;
        var section = "";
        try
        {
            fishbone?.RaiseJumpForCheck(0);
            await Task.Delay(500);
            var live = this.GetVisualDescendants().OfType<LiveView>().FirstOrDefault();
            selected = live?.FindControl<ListBox>("TranscriptList")?.SelectedItem is not null;
            section = state.Section.ToString();
            passed = state.Section == AppSection.Live && selected;
        }
        catch (Exception ex) { Console.WriteLine("notes-jump-check: " + ex.Message); }
        var report = new { passed, section, selected };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "notes-jump-check: passed" : "notes-jump-check: failed");
        Close();
    }

    /// <summary>QA: WAV import goes through the cloud client against a loopback WebSocket.</summary>
}
