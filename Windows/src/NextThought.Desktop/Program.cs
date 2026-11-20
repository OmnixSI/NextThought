using Avalonia;

namespace NextThought.Desktop;

internal static class Program
{
    public static readonly System.Diagnostics.Stopwatch Clock = new();
    public static DateTime StartedAt { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        Clock.Start();
        StartedAt = DateTime.Now;
        StartupOptions.Parse(args);
#if DEBUG
#else
#endif
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration; also used by the visual designer and headless tests.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
