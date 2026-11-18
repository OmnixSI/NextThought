namespace NextThought.Desktop;

/// <summary>Command-line switches that make the interface inspectable without a live meeting.</summary>
public static class StartupOptions
{
    public static bool Demo { get; private set; }
    public static AppSection? Section { get; private set; }
    public static bool Dark { get; private set; }
    public static string? CheckReport { get; private set; }
    public static bool Island { get; private set; }
    public static bool Subtitles { get; private set; }
    public static bool Companion { get; private set; }
    public static string? CaptureTest { get; private set; }
    public static bool Teleprompter { get; private set; }
    public static bool TeleprompterThrough { get; private set; }
    public static string? ImportWave { get; private set; }

    public static void Parse(IReadOnlyList<string> args)
    {
        foreach (var argument in args)
        {
            if (argument is "--demo" or "-d") Demo = true;
            else if (argument is "--dark") Dark = true;
            else if (argument is "--island") Island = true;
            else if (argument is "--subtitles") Subtitles = true;
            else if (argument is "--companion") Companion = true;
            else if (argument is "--teleprompter") Teleprompter = true;
            else if (argument is "--teleprompter-through") { Teleprompter = true; TeleprompterThrough = true; }
            else if (argument.StartsWith("--import-wave=", StringComparison.OrdinalIgnoreCase)) ImportWave = argument["--import-wave=".Length..];
            else if (argument.StartsWith("--capture-test=", StringComparison.OrdinalIgnoreCase)) CaptureTest = argument["--capture-test=".Length..];
            else if (argument.StartsWith("--startup-check=", StringComparison.OrdinalIgnoreCase)) CheckReport = argument["--startup-check=".Length..];
            else if (argument.StartsWith("--section=", StringComparison.OrdinalIgnoreCase)
                     && Enum.TryParse<AppSection>(argument["--section=".Length..], true, out var section)) Section = section;
        }
    }
}
