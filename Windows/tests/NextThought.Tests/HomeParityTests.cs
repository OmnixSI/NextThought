using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E1: the home page follows HomeView.swift — named subviews, Mac order and copy, title-based
/// scene guessing, scene defaults on selection, continue-series/recent-meeting rows and the
/// screenshot card.
/// </summary>
public sealed class HomeParityTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "HomeView.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop(params string[] parts) =>
        Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Desktop(parts));

    [Fact]
    public void EveryMacHomeSubviewHasASameNamedWindowsView()
    {
        // HomeView.swift: StartComposer, ContinueSeries, HomeScreenshotCard, RecentMeetings,
        // OnboardingResumeRow — each is its own file/type on Windows so VIEW_PARITY can map it.
        foreach (var name in new[]
                 {
                     "StartComposer", "ContinueSeries", "HomeScreenshotCard", "RecentMeetings", "OnboardingResumeRow"
                 })
        {
            Assert.True(File.Exists(Desktop("Views", "Home", name + ".axaml")), name + ".axaml missing");
            var code = Read("Views", "Home", name + ".axaml.cs");
            Assert.Contains("public partial class " + name, code);
        }
        var home = Read("Views", "HomeView.axaml");
        foreach (var name in new[] { "StartComposer", "ContinueSeries", "HomeScreenshotCard", "RecentMeetings" })
            Assert.Contains("<home:" + name, home);
        // Mac order: hero, resume row, composer, continue series, screenshot card, recent meetings.
        var order = new[] { "OnboardingResumeRow", "StartComposer", "ContinueSeries", "HomeScreenshotCard", "RecentMeetings" }
            .Select(name => home.IndexOf(name, StringComparison.Ordinal)).ToArray();
        Assert.Equal(order.OrderBy(value => value).ToArray(), order);
        Assert.Contains("MaxWidth=\"760\"", home);
        Assert.Contains("<controls:InkWash Mode=\"{Binding SelectedMode}\" Height=\"380\"", home);
    }

    [Fact]
    public void HeroUsesTheMacGreetingAndQuestion()
    {
        var home = Read("Views", "HomeView.axaml");
        Assert.Contains("{Binding HomeGreeting}", home);
        Assert.Contains("{Binding HomeQuestion}", home);
        var state = Read("AppState.Home.cs");
        foreach (var line in new[] { "夜深了。", "上午好。", "下午好。", "晚上好。" })
            Assert.Contains("UiText.T(\"" + line + "\")", state);
        Assert.Contains("接下来是什么场合？", state);
    }

    [Fact]
    public void TypingATitleGuessesTheScene()
    {
        var composer = Read("Views", "Home", "StartComposer.axaml");
        Assert.Contains("TextChanged=\"Title_Changed\"", composer);
        var code = Read("Views", "Home", "StartComposer.axaml.cs");
        Assert.Contains("state.GuessSceneFromTitle", code);
        Assert.Contains("scenePinned", code);
        var state = Read("AppState.Home.cs");
        Assert.Contains("MeetingTypeGuesser.Guess", state);
        Assert.Contains("public bool GuessSceneFromTitle", state);
    }

    [Fact]
    public void SelectingASceneAppliesItsDefaults()
    {
        var state = Read("AppState.Home.cs");
        Assert.Contains("public void ApplySceneDefaults", state);
        Assert.Contains("Settings.AutomaticAnswers = defaults.LiveMode == \"自动回答\"", state);
        Assert.Contains("SelectedCueWidth", state);
        Assert.Contains("SceneDefaultsFor(mode)", state);
        Assert.Contains("StartMeetingFromHome", state);
        // The composer's buttons go through the same Open(startNow:) equivalent.
        var code = Read("Views", "Home", "StartComposer.axaml.cs");
        Assert.Contains("StartMeetingFromHome(startNow: false", code);
        Assert.Contains("StartMeetingFromHome(startNow: true", code);
    }

    [Fact]
    public void ContinueSeriesListsTheLatestThreeWithMacRows()
    {
        var state = Read("AppState.Home.cs");
        Assert.Contains("HomeSeriesRows", state);
        Assert.Contains("Take(3)", state);
        Assert.Contains("Standing != true", state);
        Assert.Contains("OrderByDescending(item => item.Last?.StartedAt ?? item.Series.CreatedAt)", state);
        Assert.Contains("ThenByDescending(item => item.Series.CreatedAt)", state);
        Assert.Contains("UiText.Format(\"%lld 场 · %@ · %lld 份资料\"", state);
        Assert.Contains("还没开过 · %lld 份资料", state);
        var view = Read("Views", "Home", "ContinueSeries.axaml");
        Assert.Contains("{loc:Tr 继续一个系列}", view);
        Assert.Contains("{loc:Tr 全部系列}", view);
        Assert.Contains("{Binding Name}", view);
        Assert.Contains("{Binding Counts}", view);
        Assert.Contains("IconArrowRight", view);
        Assert.Contains("PrepareNextMeetingInSeries", Read("Views", "Home", "ContinueSeries.axaml.cs"));
    }

    [Fact]
    public void RecentMeetingsShowTheOutcomeAndWhenLines()
    {
        var state = Read("AppState.Home.cs");
        Assert.Contains("Take(4)", state);
        Assert.Contains("MeetingOutcome.Line(", state);
        Assert.Contains("MeetingOutcome.When(meta.StartedAt)", state);
        var view = Read("Views", "Home", "RecentMeetings.axaml");
        Assert.Contains("{loc:Tr 最近记录}", view);
        Assert.Contains("{loc:Tr 全部记录}", view);
        Assert.Contains("{Binding Outcome}", view);
        Assert.Contains("{Binding When}", view);
        Assert.Contains("{loc:Tr 开过的会都在这里：原话、纪要和下一步，随时找得回来。}", view);
        Assert.Contains("Meeting_Click", Read("Views", "Home", "RecentMeetings.axaml.cs"));
    }

    [Fact]
    public void ScreenshotCardWaitsOnTheHomePageUntilAnalyzedOrCancelled()
    {
        var card = Read("Views", "Home", "HomeScreenshotCard.axaml");
        Assert.Contains("{loc:Tr 截图}", card);
        Assert.Contains("{loc:Tr 分析截图}", card);
        Assert.Contains("{loc:Tr 取消}", card);
        Assert.Contains("{Binding ScreenshotPreview}", card);
        var code = Read("Views", "Home", "HomeScreenshotCard.axaml.cs");
        Assert.Contains("AnalyzeScreenshotAsync", code);
        Assert.Contains("ClearScreenshotPreview", code);
        var state = Read("AppState.Home.cs");
        Assert.Contains("public void SetScreenshotPreview", state);
        Assert.Contains("public void ClearScreenshotPreview", state);
        Assert.Contains("public async Task AnalyzeScreenshotAsync", state);
        // Mac commitRegionScreenshot/captureWindow: an idle capture lands on the home card;
        // a capture during a meeting is analyzed immediately (live page behavior).
        var shell = Read("Views", "MainWindow.Shell.cs");
        Assert.Contains("state.SetScreenshotPreview(path, cropped)", shell);
        Assert.Contains("state.AnalyzeScreenshotFileAsync(path)", shell);
    }

    [Fact]
    public void TheResumeRowAndDemoRefreshExist()
    {
        var row = Read("Views", "Home", "OnboardingResumeRow.axaml");
        Assert.Contains("{Binding OnboardingResumeVisible}", row);
        Assert.Contains("{Binding OnboardingResumeLine}", row);
        Assert.Contains("IconXmark", row);
        // E3 moved the first-run resume row to its own partial; the E1 demo refresh stays in Home.
        var resume = Read("AppState.OnboardingResume.cs");
        Assert.Contains("OnboardingResumeVisible", resume);
        var state = Read("AppState.Home.cs");
        Assert.Contains("RefreshHome()", state);
        Assert.Contains("RefreshHome();", Read("AppState.Demo.cs"));
    }
}
