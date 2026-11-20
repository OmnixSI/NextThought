namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunCaptureHideCheckAsync(string path)
    {
        await Task.Delay(300);
        var affinity = 0u;
        var passed = false;
        var error = "";
        var showActivated = true;
        var topmost = false;
        var onScreen = false;
        var noTaskbar = false;
        var hidden = false;
        var reshown = false;
        var subtitleAffinity = 0u;
        var teleprompterAffinity = 0u;
        Window? probe = null;
        Window? subtitleProbe = null;
        Window? teleprompterProbe = null;
        try
        {
            probe = new CueIslandWindow(state) { Width = 360, Height = 120 };
            probe.Show();
            await Task.Delay(600);
            affinity = WindowCaptureHiding.Affinity(probe);
            showActivated = probe.ShowActivated;
            topmost = probe.Topmost;
            onScreen = Screens.All.Any(screen => screen.Bounds.X <= probe.Position.X
                && probe.Position.X < screen.Bounds.X + screen.Bounds.Width
                && screen.Bounds.Y <= probe.Position.Y
                && probe.Position.Y < screen.Bounds.Y + screen.Bounds.Height);
            noTaskbar = !probe.ShowInTaskbar;
            probe.Hide();
            await Task.Delay(200);
            hidden = !probe.IsVisible;
            probe.Show();
            await Task.Delay(200);
            reshown = probe.IsVisible;
            state.SystemPartial = "字幕探针";
            subtitleProbe = new SubtitleBarWindow(state) { Width = 520, Height = 140 };
            subtitleProbe.Show();
            await Task.Delay(500);
            subtitleAffinity = WindowCaptureHiding.Affinity(subtitleProbe);
            subtitleProbe.Close();
            state.SystemPartial = "";
            teleprompterProbe = new TeleprompterWindow(state) { Width = 520, Height = 220 };
            teleprompterProbe.Show();
            await Task.Delay(400);
            teleprompterAffinity = WindowCaptureHiding.Affinity(teleprompterProbe);
            teleprompterProbe.Close();
            passed = affinity == 0x11 && !showActivated && topmost && onScreen && noTaskbar && hidden && reshown
                     && subtitleAffinity == 0x11 && teleprompterAffinity == 0x11;
        }
        catch (Exception exception)
        {
            error = exception.ToString();
        }
        finally
        {
            try { subtitleProbe?.Close(); } catch { }
            try { teleprompterProbe?.Close(); } catch { }
            probe?.Close();
        }
        var report = new { passed, affinity, subtitleAffinity, teleprompterAffinity, showActivated, topmost, onScreen, noTaskbar, hidden, reshown, error };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch { }
        Console.WriteLine(passed ? "capture-hide-check: passed" : "capture-hide-check: failed");
        Close();
    }

    // The design-time constructor required by the XAML compiler for previews.
}
