using Xunit;

namespace NextThought.Tests;

/// <summary>
/// R8 / §10.8: the formal Windows package carries no QA escape hatches, no startup switch other than
/// --self-test/--demo/--screenshots and none of the debug-only sample pages; the release script
/// audits the published payload for exactly those markers before packaging.
/// </summary>
public sealed class ReleaseCleanlinessTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "NextThought.Windows.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    [Fact]
    public void TheReleaseScriptAuditsThePayloadForBannedMarkers()
    {
        var audit = Read("Windows", "scripts", "release_cleanliness.py");
        foreach (var marker in new[]
                 {
                     "NEXTTHOUGHT_QA", "NEXTTHOUGHT_GPU_RENDER", "NEXTTHOUGHT_UPDATE_LAUNCH",
                     "--update-check=", "--demo-churn", "--demo-voices", "--voices", "--onboarding",
                     "ControlSampleWindow", "TypographySampleWindow", "ComponentSamplesWindow"
                 })
            Assert.Contains(marker, audit);
        var release = Read("Windows", "release.ps1");
        Assert.Contains("release_cleanliness.py", release);
        Assert.Contains("--payload $payload", release);
    }

    [Fact]
    public void OnlyTheDocumentedStartupSwitchesReachTheFormalBuild()
    {
        var startup = Read("Windows", "src", "NextThought.Desktop", "StartupOptions.cs");
        Assert.Contains("#if DEBUG", startup);
        // The demo-evidence switches are environment variables (Mac parity) or inside the debug
        // guard; the only command-line switches in the shipped build are these three.
        var debugGuard = startup.IndexOf("#if DEBUG", StringComparison.Ordinal);
        Assert.True(debugGuard > 0);
        Assert.True(startup.IndexOf("--onboarding", StringComparison.Ordinal) > debugGuard);
        Assert.True(startup.IndexOf("--self-test", StringComparison.Ordinal) < debugGuard);
        Assert.True(startup.IndexOf("--screenshots", StringComparison.Ordinal) < debugGuard);
        Assert.True(startup.IndexOf("--demo", StringComparison.Ordinal) < debugGuard);
        Assert.DoesNotContain("--demo-churn", startup);
        Assert.DoesNotContain("--demo-voices", startup);
        Assert.DoesNotContain("--update-check=", startup);
    }

    [Fact]
    public void TheQaUpdateSwitchesAreDebugOnly()
    {
        // The shipped platform assembly has no QA env names at all.
        var policy = Read("Windows", "src", "NextThought.Platform", "UpdateFeedPolicy.cs");
        Assert.DoesNotContain("NEXTTHOUGHT_QA", policy);
        Assert.DoesNotContain("NEXTTHOUGHT_UPDATE_FEED", policy);
        Assert.Contains("requestedOverride", policy);
        // Only the desktop debug build reads the environment variables.
        var feed = Read("Windows", "src", "NextThought.Desktop", "UpdateFeed.cs");
        Assert.Contains("#if DEBUG", feed);
        Assert.Contains("NEXTTHOUGHT_QA", feed);
        Assert.Contains("NEXTTHOUGHT_UPDATE_FEED", feed);
    }

    [Fact]
    public void TheSamplePagesAreDebugOnly()
    {
        var csproj = Read("Windows", "src", "NextThought.Desktop", "NextThought.Desktop.csproj");
        Assert.Contains("<AvaloniaXaml Remove=\"Views\\ControlSampleWindow.axaml\" />", csproj);
        Assert.Contains("<AvaloniaXaml Remove=\"Views\\ComponentSamplesWindow.axaml\" />", csproj);
        Assert.Contains("<AvaloniaXaml Remove=\"SelfTest\\TypographySampleWindow.axaml\" />", csproj);
        Assert.Contains("<AvaloniaXaml Remove=\"Views\\Shell\\LiveComponentSamplesWindow.axaml\" />", csproj);
        foreach (var file in new[]
                 {
                     Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "ControlSampleWindow.axaml.cs"),
                     Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "ComponentSamplesWindow.axaml.cs"),
                     Path.Combine("Windows", "src", "NextThought.Desktop", "SelfTest", "TypographySampleWindow.axaml.cs"),
                     Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "Shell", "LiveComponentSamplesWindow.axaml.cs")
                 })
            Assert.Contains("#if DEBUG", File.ReadAllText(Path.Combine(Root(), file)));
    }
}
