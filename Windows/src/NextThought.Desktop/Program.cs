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
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
        // The app is text-heavy and short-lived: Skia software rendering keeps the working set
        // well under the 150 MiB budget and avoids the GPU compositor's threads and buffers.
        // NEXTTHOUGHT_GPU_RENDER=1 opts back into the platform GPU backend for troubleshooting.
        if (Environment.GetEnvironmentVariable("NEXTTHOUGHT_GPU_RENDER") != "1")
        {
            builder = builder.With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Software]
            });
        }
        return builder;
    }
}
