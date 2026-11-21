namespace NextThought.Desktop;

/// <summary>
/// Production startup switches: demo, screenshot batch and a named self-test. The walkthrough-only
/// data switches follow the Mac and are environment variables, never command-line arguments.
/// </summary>
public static class StartupOptions
{
    public static bool Demo { get; private set; }
    public static string? Screenshots { get; private set; }
    public static string? SelfTest { get; private set; }
    public static bool Onboarding { get; private set; }

    /// <summary>Mac NEXTTHOUGHT_DEMO_VOICES=1: the demo holds its weekly meeting in a room.</summary>
    public static bool DemoVoices => Environment.GetEnvironmentVariable("NEXTTHOUGHT_DEMO_VOICES") == "1";

    /// <summary>Mac NEXTTHOUGHT_DEMO_CHURN=&lt;file&gt;: the walkthrough writes live-page render counts there.</summary>
    public static string? DemoChurn => Environment.GetEnvironmentVariable("NEXTTHOUGHT_DEMO_CHURN") is { Length: > 0 } path ? path : null;

    public static void Parse(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument is "--demo" or "-d") Demo = true;
            else if (argument.StartsWith("--screenshots=", StringComparison.OrdinalIgnoreCase)) Screenshots = argument["--screenshots=".Length..];
            else if (argument == "--screenshots" && index + 1 < args.Count) Screenshots = args[++index];
            else if (argument.StartsWith("--self-test=", StringComparison.OrdinalIgnoreCase)) SelfTest = argument["--self-test=".Length..];
            else if (argument == "--self-test" && index + 1 < args.Count) SelfTest = args[++index];
            else if (argument == "--onboarding") Onboarding = true;
        }
    }
}
