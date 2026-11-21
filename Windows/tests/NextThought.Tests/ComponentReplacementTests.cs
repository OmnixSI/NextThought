using Xunit;

namespace NextThought.Tests;

/// <summary>
/// Decisions §3.1 components 6: pages use the design-system components instead of hand-written
/// equivalents (status dots, chips, shortcut caps, markdown body).
/// </summary>
public sealed class ComponentReplacementTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", "Views", .. parts]));

    [Fact]
    public void TheLivePageUsesStatusDotsForOnAirAndTheAnswerEngine()
    {
        var live = Read("LiveView.axaml");
        Assert.Contains("<components:StatusDot Tone=\"Live\" IsVisible=\"{Binding IsLiveOnAir}\" />", live, StringComparison.Ordinal);
        Assert.Contains("<components:StatusDot Tone=\"Idle\" IsVisible=\"{Binding !IsLiveOnAir}\" />", live, StringComparison.Ordinal);
        Assert.Contains("<components:StatusDot x:Name=\"LiveAnswerEngineDot\" Tone=\"Caution\"", live, StringComparison.Ordinal);
    }

    [Fact]
    public void TheShortcutCapAndBadgesUseTheDesignSystem()
    {
        var composer = Read("Home", "StartComposer.axaml");
        Assert.Contains("<c:ShortcutHint Text=\"Ctrl+Alt+N\"", composer, StringComparison.Ordinal);
        Assert.Contains("xmlns:c=\"using:NextThought.Desktop.Controls\"", composer, StringComparison.Ordinal);
        var editor = Read("NotesEditorWindow.axaml");
        Assert.Contains("<c:MetricPill Text=\"{Binding Badge}\"", editor, StringComparison.Ordinal);
        var glossary = Read("GlossaryWindow.axaml");
        Assert.Contains("<controls:MetricPill Text=\"{loc:Tr 人名}\"", glossary, StringComparison.Ordinal);
    }

    [Fact]
    public void AnswersRenderThroughMarkdownTextOnTheNotesPage()
    {
        var notes = Read("NotesView.axaml");
        Assert.Contains("<views:MarkdownText Text=\"{Binding AnswerText}\"", notes, StringComparison.Ordinal);
    }
}
