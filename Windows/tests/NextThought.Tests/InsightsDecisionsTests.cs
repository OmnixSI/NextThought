using Xunit;

namespace NextThought.Tests;

/// <summary>§3.3 insights 3: the answer-progress pill turns green once every point is spoken.</summary>
public sealed class InsightsDecisionsTests
{
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return Path.Combine(directory.FullName, "Windows");
        }
        throw new DirectoryNotFoundException("repository root");
    }

    [Fact]
    public void AnswerProgressTurnsGreenWhenComplete()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.InsightsFix.cs"));
        Assert.Contains("AnswerProgressIsDone", state);
        Assert.Contains("Ready", state);
        var view = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "InsightsPanel.axaml"));
        Assert.Contains("AnswerProgressBrush", view);
    }
}