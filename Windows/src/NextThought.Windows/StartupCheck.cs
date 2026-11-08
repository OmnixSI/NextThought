using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using NextThought.Core;

namespace NextThought.Windows;

/// <summary>Opt-in unattended WPF startup check in an explicitly isolated data directory.</summary>
internal static class StartupCheck
{
    public static void Begin(App app, string report)
    {
        var root = Environment.GetEnvironmentVariable("NEXTTHOUGHT_DATA_DIR");
        var actual = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NextThought");
        if (string.IsNullOrWhiteSpace(root) || Path.GetFullPath(root).TrimEnd('\\').Equals(actual.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFullPath(report).StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = 2; app.Shutdown(2); return;
        }
        var window = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false };
        app.MainWindow = window;
        var rendered = false; window.ContentRendered += (_, _) => rendered = true;
        var watch = Stopwatch.StartNew(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += async (_, _) =>
        {
            if ((!window.ReadyForDiagnostics || !rendered) && watch.Elapsed.TotalSeconds < 20) return;
            timer.Stop();
            var passed = window.ReadyForDiagnostics && rendered && window.StartupError is null && !window.RecordingForDiagnostics;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
                await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed,
                    scope = "Off-screen non-activating WPF startup, not visual layout or interactive desktop acceptance", rendered, ready = window.ReadyForDiagnostics,
                    recording = window.RecordingForDiagnostics, window.StartupError, seconds = watch.Elapsed.TotalSeconds,
                    historyCount = window.HistoryList.Items.Count, title = window.PageTitle.Text,
                    displayedVersion = window.VersionText.Text,
                    version = FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion }, JsonDefaults.Options));
            }
            catch { passed = false; }
            Environment.ExitCode = passed ? 0 : 2; window.Close();
        };
        window.Show(); timer.Start();
    }
}
