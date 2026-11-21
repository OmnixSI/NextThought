namespace NextThought.Desktop;

/// <summary>Production startup switches: demo, screenshot batch and a named self-test.</summary>
public static class StartupOptions
{
    public static bool Demo { get; private set; }
    public static string? Screenshots { get; private set; }
    public static string? SelfTest { get; private set; }
    public static bool Onboarding { get; private set; }

    /// <summary>D1: Mac NEXTTHOUGHT_DEMO_VOICES=1 — the demo's weekly meeting held in a room.</summary>
    public static bool DemoVoices { get; private set; }

    /// <summary>
    /// D1/J8: Mac NEXTTHOUGHT_DEMO_CHURN=&lt;file&gt; — the walkthrough copy measures how often the
    /// live page is rebuilt and writes the measurement there. Demo-only, like the Mac switch.
    /// </summary>
    public static string? DemoChurn { get; private set; }

    public static void Parse(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument is "--demo" or "-d") Demo = true;
            else if (argument is "--demo-voices" or "--voices") DemoVoices = true;
            else if (argument.StartsWith("--demo-churn=", StringComparison.OrdinalIgnoreCase)) DemoChurn = argument["--demo-churn=".Length..];
            else if (argument == "--demo-churn" && index + 1 < args.Count) DemoChurn = args[++index];
            else if (argument.StartsWith("--screenshots=", StringComparison.OrdinalIgnoreCase)) Screenshots = argument["--screenshots=".Length..];
            else if (argument == "--screenshots" && index + 1 < args.Count) Screenshots = args[++index];
            else if (argument.StartsWith("--self-test=", StringComparison.OrdinalIgnoreCase)) SelfTest = argument["--self-test=".Length..];
            else if (argument == "--self-test" && index + 1 < args.Count) SelfTest = args[++index];
            else if (argument == "--onboarding") Onboarding = true;
        }
    }
}
