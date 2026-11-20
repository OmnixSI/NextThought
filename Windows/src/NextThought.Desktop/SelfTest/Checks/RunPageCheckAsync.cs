namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunPageCheckAsync(string path)
    {
        await Task.Delay(150);
        var process = System.Diagnostics.Process.GetCurrentProcess();
        process.Refresh();
        var initialBytes = process.WorkingSet64;
        var initialPages = pages.Count;
        var names = new List<string>();
        foreach (var section in Enum.GetValues<AppSection>())
        {
            state.Section = section;
            await Task.Delay(60);
            if (PageHost.Content is Control page) names.Add(section + ":" + page.GetType().Name);
        }
        state.Section = AppSection.Home;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        process.Refresh();
        var allBytes = process.WorkingSet64;
        var passed = initialPages == 1 && pages.Count == Enum.GetValues<AppSection>().Length
                     && names.Count == pages.Count && names.All(name => name.Contains(':'));
        var report = new
        {
            passed,
            initialPages,
            pageCount = pages.Count,
            initialWorkingSetMiB = Math.Round(initialBytes / 1048576.0, 1),
            allPagesWorkingSetMiB = Math.Round(allBytes / 1048576.0, 1),
            pages = names
        };
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
        Console.WriteLine(passed ? "page-check: passed" : "page-check: failed");
        Close();
    }

    /// <summary>Writes archive marker/lines and checks receipts and leftover classification.</summary>
}
