using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E13/E14 (决议 §3.3 screenshot-question and web-search 2/3): the screenshot travels to the
/// model as an image input on both answer backends, and the original answer ↔ web version state is
/// kept per answer id so the live page only switches the answer the lookup belongs to.
/// </summary>
public sealed class ScreenshotAndWebAnswerWiringTests
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
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    private static string ReadPlatform(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Platform", .. parts]));

    [Fact]
    public void TheScreenshotIsKeptUntilTheQuestionAndSentAsTheModelImage()
    {
        var live = Read("AppState.Live.cs");
        Assert.Contains("pendingScreenshotImagePath", live, StringComparison.Ordinal);
        Assert.Contains("PromptAboutScreenshot(string? screenText = null, string? imagePath = null)", live, StringComparison.Ordinal);
        Assert.Contains("hasScreenshot: hasScreenshot", live, StringComparison.Ordinal);
        Assert.Contains("imagePath: screenshotImagePath", live, StringComparison.Ordinal);
        Assert.Contains("ScreenImageAttached = screenshotImagePath is not null", live, StringComparison.Ordinal);
        Assert.Contains("ClearPendingScreenshotImage()", live, StringComparison.Ordinal);
        var home = Read("AppState.Home.cs");
        Assert.Contains("PromptAboutScreenshot(Library.SelectedPreview, path)", home, StringComparison.Ordinal);
        // The old flow deleted the picture during the analysis, before the model call.
        var analyze = home[home.IndexOf("public async Task AnalyzeScreenshotFileAsync", StringComparison.Ordinal)..];
        var body = analyze[..analyze.IndexOf("    }", StringComparison.Ordinal)];
        Assert.DoesNotContain("File.Delete", body, StringComparison.Ordinal);
        var chat = ReadPlatform("ChatCompletionClient.cs");
        Assert.Contains("image_url", chat, StringComparison.Ordinal);
        Assert.Contains("base64", chat, StringComparison.Ordinal);
        var codex = ReadPlatform("CodexAnswerClient.cs");
        Assert.Contains("CodexAppServerProtocol.TurnStartParams", codex, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWebVersionIsPerAnswerAndSwitchesOnlyThatAnswer()
    {
        var web = Read("AppState.WebSearch.cs");
        Assert.Contains("WebLookupStore", web, StringComparison.Ordinal);
        Assert.Contains("ShowsWebAnswer", web, StringComparison.Ordinal);
        Assert.Contains("CanToggleWebAnswer", web, StringComparison.Ordinal);
        Assert.Contains("WebAnswerToggleLabel", web, StringComparison.Ordinal);
        Assert.Contains("ToggleWebAnswerCommand", web, StringComparison.Ordinal);
        Assert.Contains("WebLookupStatusText", web, StringComparison.Ordinal);
        Assert.Contains("WebSearchClassifier.AssessAsync", web, StringComparison.Ordinal);
        Assert.Contains("WebAnswer = webAnswer", web, StringComparison.Ordinal);
        var assistant = Read("AppState.Assistant.cs");
        Assert.Contains("ShowsWebAnswer", assistant, StringComparison.Ordinal);
        var live = Read("AppState.Live.cs");
        Assert.Contains("MaybeStartWebLookup(answerRecord, searchDecision)", live, StringComparison.Ordinal);
        Assert.Contains("AssessWebSearchAsync(question, cancellation.Token)", live, StringComparison.Ordinal);
        Assert.Contains("Search = searchDecision", live, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWebSearchModeIsTheMacThreeState()
    {
        var settings = Read("AppState.E4.cs");
        Assert.Contains("WebSearchModes.Parse(Settings.WebSearchMode)", settings, StringComparison.Ordinal);
        Assert.Contains("SetLiveWebSearchMode", settings, StringComparison.Ordinal);
        Assert.Contains("LiveWebSearchIsOff", settings, StringComparison.Ordinal);
        Assert.Contains("LiveWebSearchIsAuto", settings, StringComparison.Ordinal);
        Assert.Contains("LiveWebSearchIsAlways", settings, StringComparison.Ordinal);
    }
}
