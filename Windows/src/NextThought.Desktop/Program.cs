using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

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
            .With(new FontManagerOptions
            {
                // §5.1: the bundled static faces are the app's fonts; the mappings keep the legacy
                // plain family names working (MarkdownText and other controls name "Inter" etc).
                DefaultFamilyName = "avares://NextThought/Assets/Fonts/Inter-Regular.ttf#Inter",
                FontFamilyMappings = new Dictionary<string, FontFamily>
                {
                    ["Inter"] = new("avares://NextThought/Assets/Fonts/Inter-Regular.ttf#Inter, avares://NextThought/Assets/Fonts/NotoSansSC-Regular.otf#Noto Sans SC"),
                    ["Noto Sans SC"] = new("avares://NextThought/Assets/Fonts/NotoSansSC-Regular.otf#Noto Sans SC"),
                    ["Source Serif 4"] = new("avares://NextThought/Assets/Fonts/SourceSerif4-Medium.ttf#Source Serif 4, avares://NextThought/Assets/Fonts/NotoSerifSC-Medium.otf#Noto Serif SC"),
                    ["Noto Serif SC"] = new("avares://NextThought/Assets/Fonts/NotoSerifSC-Medium.otf#Noto Serif SC")
                }
            })
            .LogToTrace();
    }
}
