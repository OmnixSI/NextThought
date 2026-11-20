namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunShareHideCheckAsync(string path)
    {
        var passed = false;
        var detected = false;
        var hidden = false;
        var restored = false;
        try
        {
            state.Settings.SelectedCueWidth = state.Settings.CueWidthChoices.First(choice => choice.Value == "wide");
            state.Recording = true;
            ShowCueIsland();
            await Task.Delay(500);
            var width = island?.Width ?? 0;
            var widthOk = Math.Abs(width - 660) < 0.5;
            var probe = new Window
            {
                Title = "Zoom - Screen Sharing",
                Width = 180, Height = 120, ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Position = new PixelPoint(40, 40)
            };
            probe.Show();
            await Task.Delay(400);
            detected = ScreenShareDetector.IsSharingActive();
            EvaluateScreenSharing();
            await Task.Delay(400);
            hidden = island is not { IsVisible: true };
            probe.Close();
            await Task.Delay(400);
            EvaluateScreenSharing();
            await Task.Delay(500);
            restored = island is { IsVisible: true };
            state.Recording = false;
            passed = widthOk && detected && hidden && restored;
        }
        catch (Exception ex) { Console.WriteLine("share-hide-check: " + ex.Message); }
        var report = new { passed, detected, hidden, restored, width = island?.Width ?? 0 };
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
        Console.WriteLine(passed ? "share-hide-check: passed" : "share-hide-check: failed");
        Close();
    }

    /// <summary>QA: saved subtitle width/font reach the floating bar.</summary>
}
