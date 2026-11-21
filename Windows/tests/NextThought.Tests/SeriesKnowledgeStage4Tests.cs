using Xunit;

namespace NextThought.Tests;

/// <summary>
/// task-62/E18: the series page's knowledge review section must carry the Mac buttons and the AISoft
/// tinted candidate cards (KnowledgeReviewSection.swift). The collect-knowledge command was only
/// reachable from the AppState layer before this test.
/// </summary>
public sealed class SeriesKnowledgeStage4Tests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "SeriesView.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void TheKnowledgeSectionCollectsTheMeetingsCandidates()
    {
        var view = Read("Views", "SeriesView.axaml");
        Assert.Contains("{Binding CollectKnowledgeLabel}", view);
        Assert.Contains("CollectKnowledge_Click", view);
        Assert.Contains("{Binding CanCollectKnowledgeCandidates}", view);
        var state = Read("AppState.Series.cs");
        Assert.Contains("public string CollectKnowledgeLabel => UiText.T(\"收集本场候选\")", state);
        Assert.Contains("public bool CanCollectKnowledgeCandidates", state);
        var code = Read("Views", "SeriesView.axaml.cs");
        Assert.Contains("state.CollectKnowledgeCandidates();", code);
    }

    [Fact]
    public void TheCandidateCardsUseTheMacAISoftTint()
    {
        var view = Read("Views", "SeriesView.axaml");
        var cards = view.Split("Classes=\"card\"").Length - 1;
        Assert.True(cards >= 2);
        var aiSoftCards = view.Split("Classes=\"card\" Background=\"{DynamicResource AiSoft}\"").Length - 1;
        Assert.True(aiSoftCards >= 2, "expected series-change and knowledge candidate cards to use AiSoft, found " + aiSoftCards);
    }
}
