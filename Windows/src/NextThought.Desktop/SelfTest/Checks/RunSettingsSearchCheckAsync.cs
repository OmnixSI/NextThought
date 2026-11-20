namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunSettingsSearchCheckAsync(string path)
    {
        state.Section = AppSection.Settings;
        await Task.Delay(600);
        var view = this.GetVisualDescendants().OfType<SettingsView>().FirstOrDefault();
        var box = view?.FindControl<TextBox>("SettingsSearch");
        var cards = view?.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains("card") && border.Name != "SettingsSearchCard").ToList() ?? [];
        var before = cards.Count(card => card.IsVisible);
        if (box is not null) box.Text = "快捷键";
        await Task.Delay(300);
        var after = cards.Count(card => card.IsVisible);
        var passed = box is not null && after >= 1 && after < cards.Count;
        var report = new { passed, totalCards = cards.Count, before, after };
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
        Console.WriteLine(passed ? "settings-search-check: passed" : "settings-search-check: failed");
        Close();
    }

    /// <summary>QA: with language=en the shell actually shows translated labels.</summary>
}
