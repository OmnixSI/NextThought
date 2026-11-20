using Xunit;

namespace NextThought.Tests;

/// <summary>H1: main window keeps only window code; self-checks live under Desktop/SelfTest.</summary>
public sealed class SelfTestStructureTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }



    [Fact]
    public void LegacyPerCheckFlagsAreGone()
    {
        var root = Root();
        var startup = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "StartupOptions.cs"));
        Assert.DoesNotContain("SelfTestOptions", startup);
        Assert.False(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTestOptions.cs")));
    }

    [Fact]
    public void ProductionStartupOptionsOnlyExposeThreeSwitches()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "StartupOptions.cs");
        var text = File.ReadAllText(path);
        Assert.Contains("Demo", text);
        Assert.Contains("Screenshots", text);
        Assert.Contains("SelfTest", text);
        Assert.DoesNotContain("--knowledge-check", text);
        Assert.True(File.ReadAllLines(path).Length < 40);
    }

    [Fact]
    public void MainWindowFileIsBelow400Lines()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs");
        Assert.True(File.ReadAllLines(path).Length < 400);
    }

    [Fact]
    public void SelfChecksLiveOutsideTheWindowFile()
    {
        var root = Root();
        Assert.True(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTest", "Checks", "GlobalUsings.cs")));
        var window = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs"));
        Assert.DoesNotContain("private async Task RunAcceptanceCheckAsync", window);
        Assert.True(Directory.GetFiles(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTest", "Checks"), "*.cs").Length > 40);
    }
}
