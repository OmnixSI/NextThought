namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunPrepareModelsCheckAsync(string path)
    {
        await Task.Delay(300);
        var ids = new List<string>();
        var installed = new List<object>();
        var error = "";
        var seconds = 0.0;
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var settings = state.Settings.Build();
            ids.Add(settings.LocalModel);
            if (settings.SecondPass) ids.Add("xasrOffline");
            var progress = new Progress<NextThought.Core.ModelProgress>(item => state.SetStatus(item.Message));
            foreach (var id in ids)
            {
                await state.Models.InstallAsync(id, progress);
                installed.Add(new
                {
                    id,
                    title = NextThought.Platform.ModelStore.Package(id).Title,
                    isInstalled = state.Models.IsInstalled(id),
                    directory = state.Models.DirectoryFor(id),
                    bytes = Directory.Exists(state.Models.DirectoryFor(id))
                        ? Directory.EnumerateFiles(state.Models.DirectoryFor(id), "*", SearchOption.AllDirectories)
                            .Sum(file => new FileInfo(file).Length)
                        : 0
                });
            }
            seconds = Math.Round(watch.Elapsed.TotalSeconds, 1);
        }
        catch (Exception exception)
        {
            error = exception.ToString();
        }
        var passed = error.Length == 0 && ids.Count > 0 && installed.Count == ids.Count;
        var report = new { passed, ids, installed, seconds, error, dataDirectory = state.Paths.Root };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "prepare-models-check: passed" : "prepare-models-check: failed");
        Close();
    }


    /// <summary>Exercises the LAN companion channel without a phone: pairing token, snapshot, action and dedup.</summary>
}
