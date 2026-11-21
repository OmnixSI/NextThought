using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E9 wiring: the speech follower runs on the user's own final lines, the screen watch classifies
/// each frame and follows on every read, the note cache and manual pause live in the desktop state,
/// and the presenter/insight settings expose the follow switch.
/// </summary>
public sealed class SlideFollowSettingsTests
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

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

    [Fact]
    public void TheFinalUserLineFeedsTheSpeechFollower()
    {
        var live = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("FollowSpeechForLine(line);", live);
        Assert.DoesNotContain("TryFollowSlide", live);
        var follow = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.SlideFollow.cs"));
        Assert.Contains("line.Speaker != \"me\"", follow);
        Assert.Contains("ObserveSpeech", follow);
        Assert.Contains("SlideNotesCacheStore.Fingerprint", follow);
        Assert.Contains("SlideNotesCacheStore.Save", follow);
        Assert.Contains("StepSlide", follow);
        Assert.Contains("SlideSource.Manual", follow);
        Assert.Contains("NoteScreenObservation", follow);
    }

    [Fact]
    public void TheScreenWatchClassifiesAndFollowsEveryRead()
    {
        var watch = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.ScreenWatch.cs"));
        Assert.Contains("NoteScreenObservation(text)", watch);
        Assert.Contains("if (Settings.InsightFollowSlides) FollowSlideFromScreen(text);", watch);
        // Mac: re-evaluate even when the pixels did not change; the hash is kept for diagnostics only.
        Assert.Contains("lastScreenText = hash;", watch);
        Assert.DoesNotContain("if (hash != lastScreenText)", watch);
        // Mac startScreenWatch: 1 s minimum interval and a 20 s retry after a failure.
        Assert.Contains("Math.Max(1, Settings.InsightScreenInterval)", watch);
        Assert.Contains("AddSeconds(20)", watch);
        Assert.Contains("StartupOptions.Demo", watch);
    }

    [Fact]
    public void TheSettingsCardShowsTheFollowSwitchAndObservation()
    {
        var xaml = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        Assert.Contains("{loc:Tr 跟随演示文稿}", xaml);
        Assert.Contains("{Binding ScreenWatchObservation}", xaml);
        var state = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.SlideFollow.cs"));
        Assert.Contains("ScreenClassification.Classify", state);
        Assert.Contains("ScreenText.HeuristicRelevance", state);
        Assert.Contains("ScreenWatchObservation", state);
    }

    [Fact]
    public void TheSlidePagesCarrySpeakerNotesAndTheDocumentIdentity()
    {
        var deck = Read(Path.Combine("Windows", "src", "NextThought.Core", "SlideDeck.cs"));
        Assert.Contains("string DocumentId = \"\", string Notes = \"\"", deck);
        Assert.Contains("SlidePage.NotesHeading", deck);
        Assert.Contains("deck.Id", deck);
    }

    [Fact]
    public void TheSlideFollowSelfCheckCoversTheNewRules()
    {
        var check = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "SelfTest", "Checks", "RunSlideFollowCheckAsync.cs"));
        Assert.Contains("SlideFollowController", check);
        Assert.Contains("rulesPassed", check);
        Assert.Contains("cachePassed", check);
        Assert.Contains("classifyPassed", check);
        Assert.Contains("SlideNotesCacheStore", check);
    }
}
