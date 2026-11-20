namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunUpdateInstallCheckAsync(string path)
    {
        await Task.Delay(200);
        var update = await state.CheckForUpdatesAsync();
        // F16: the title-bar button appears only after the background download + SHA-256 check.
        await state.EnsureInstallerPrefetchedAsync(update);
        var markerPath = System.IO.Path.Combine(state.Paths.Root, "update-launch.json");
        var marker = "";
        var installerExists = false;
        string? sha = null;
        var error = "";
        try
        {
            if (!state.CanInstallUpdateInApp)
            {
                error = update.Error ?? "这个发布没有安装包或校验文件，不能应用内安装。";
            }
            else
            {
                if (File.Exists(markerPath)) File.Delete(markerPath);
                await state.InstallUpdateCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(90));
                if (File.Exists(markerPath))
                {
                    marker = await File.ReadAllTextAsync(markerPath);
                    using var document = System.Text.Json.JsonDocument.Parse(marker);
                    var root = document.RootElement;
                    installerExists = root.TryGetProperty("installer", out var installer) && File.Exists(installer.GetString());
                    sha = root.TryGetProperty("sha256", out var hash) ? hash.GetString() : null;
                }
                else
                {
                    error = "安装步骤没有写出启动标记。";
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        var passed = state.UpdateInstallSucceeded && installerExists && sha is { Length: 64 } && error.Length == 0;
        var report = new
        {
            passed,
            update = state.LastUpdateReport,
            installSucceeded = state.UpdateInstallSucceeded,
            installStatus = state.UpdateInstallStatus,
            installerExists,
            sha256 = sha,
            marker,
            error
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch { }
        Console.WriteLine(passed ? "update-install-check: passed" : "update-install-check: failed");
        Close();
    }

    /// <summary>
    /// Exports the loaded session as SRT and VTT and checks the files are real subtitles
    /// (sequential cues, WEBVTT header, UTF-8) without opening a save dialog.
    /// </summary>
}
