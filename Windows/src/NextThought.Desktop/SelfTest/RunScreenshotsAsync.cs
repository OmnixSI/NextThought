using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>D1: capture the named pages in light and dark into a directory.</summary>
    private async Task RunScreenshotsAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var pages = new[]
        {
            (AppSection.Home, "home"), (AppSection.Live, "live"), (AppSection.Notes, "notes"),
            (AppSection.History, "history"), (AppSection.Series, "series"),
            (AppSection.Library, "library"), (AppSection.Settings, "settings")
        };
        foreach (var dark in new[] { false, true })
        {
            if (Application.Current is { } app)
                app.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
            foreach (var (section, name) in pages)
            {
                state.Section = section;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Task.Delay(250);
                CaptureWindow(Path.Combine(directory, name + (dark ? "-dark.png" : "-light.png")));
            }
        }
        Close();
    }

    private void CaptureWindow(string path)
    {
        var width = Math.Max(1, (int)Math.Round(Bounds.Width));
        var height = Math.Max(1, (int)Math.Round(Bounds.Height));
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(this);
        bitmap.Save(path);
    }
}
