using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E7: the 屏幕与讲稿 insights panel (InsightsPanel.swift) — slide section, screen section and
/// the answer-progress strip.
/// </summary>
public sealed class InsightsPanelParityTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "InsightsPanel.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop(params string[] parts) =>
        Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Desktop(parts));

    [Fact]
    public void ThePanelHasTheMacHeaderAndSections()
    {
        var panel = Read("Views", "InsightsPanel.axaml");
        Assert.Contains("{loc:Tr 屏幕与讲稿}", panel);
        Assert.Contains("{loc:Tr 立即识别}", panel);
        Assert.Contains("{Binding RequestScreenReadCommand}", panel);
        Assert.Contains("{Binding ScreenIsReading}", panel);
        Assert.Contains("{Binding ScreenWatchOff}", panel);
        Assert.Contains("EnableScreenWatch_Click", panel);
        Assert.Contains("{Binding HasSlide}", panel);
        Assert.Contains("{Binding ScreenWatchRunning}", panel);
        Assert.Contains("{Binding HasAnswerProgress}", panel);
        Assert.Contains("{Binding ScreenTitleText}", panel);
        Assert.Contains("{Binding AnswerProgressMetricText}", panel);
    }

    [Fact]
    public void TheSlideSectionShowsPagePointsTransitionAndQuestions()
    {
        var panel = Read("Views", "InsightsPanel.axaml");
        Assert.Contains("{Binding SlidePositionText}", panel);
        Assert.Contains("{Binding SlideDocumentName}", panel);
        Assert.Contains("{Binding PreviousSlideCommand}", panel);
        Assert.Contains("{Binding NextSlideCommand}", panel);
        Assert.Contains("{Binding SlideHasPrevious}", panel);
        Assert.Contains("{Binding SlideHasNext}", panel);
        Assert.Contains("{Binding RegenerateNotesCommand}", panel);
        Assert.Contains("{loc:Tr 重新生成}", panel);
        Assert.Contains("{loc:Tr 改用 AI 要点}", panel);
        Assert.Contains("{Binding SlidePointRows}", panel);
        Assert.Contains("{Binding Done}", panel);
        Assert.Contains("{Binding Next}", panel);
        Assert.Contains("{Binding Pending}", panel);
        Assert.Contains("{Binding SlideTransitionText}", panel);
        Assert.Contains("{Binding SlideQuestionRows}", panel);
        Assert.Contains("AskSlideQuestion_Click", panel);
        var state = Read("AppState.Insights.cs");
        Assert.Contains("AnswerPoints.HeuristicCovered", state);
        Assert.Contains("AnswerPoints.Next", state);
        Assert.Contains("public void RegenerateSlideNotes()", state);
        Assert.Contains("SlideNotesCacheStore.Save", state);
        Assert.Contains("AskSlideQuestionAsync", state);
    }

    [Fact]
    public void TheScreenSectionShowsTheSourceStatusAndRecognisedText()
    {
        var panel = Read("Views", "InsightsPanel.axaml");
        Assert.Contains("{Binding ScreenStatusLine}", panel);
        Assert.Contains("{Binding ScreenErrorText}", panel);
        Assert.Contains("{Binding HasScreenError}", panel);
        Assert.Contains("{Binding ScreenText}", panel);
        Assert.Contains("{Binding ScreenTextMaxLines}", panel);
        Assert.Contains("{Binding HasScreenText}", panel);
        Assert.Contains("ToggleScreenText_Click", panel);
        var state = Read("AppState.Insights.cs");
        Assert.Contains("public string ScreenStatusLine", state);
        Assert.Contains("public void EnableScreenWatch()", state);
        Assert.Contains("RequestScreenReadAsync", state);
        Assert.Contains("ScreenTextExpanded", state);
    }

    [Fact]
    public void TheAnswerProgressStripFollowsMac()
    {
        var panel = Read("Views", "InsightsPanel.axaml");
        Assert.Contains("{Binding AnswerProgressCounterText}", panel);
        Assert.Contains("{Binding NextAnswerPointText}", panel);
        var state = Read("AppState.Insights.cs");
        Assert.Contains("public bool HasAnswerProgress", state);
        Assert.Contains("public IReadOnlyList<string> AnswerProgressPointTexts", state);
        Assert.Contains("public void RaiseAnswerProgress()", state);
        Assert.Contains("public void RefreshInsights()", state);
        var live = Read("AppState.Live.cs");
        Assert.Contains("RaiseAnswerProgress();", live);
    }

    [Fact]
    public void ThePanelRefreshesWhenThePageOrScreenChanges()
    {
        var code = Read("Views", "InsightsPanel.axaml.cs");
        Assert.Contains("state.RefreshInsights()", code);
        Assert.Contains("nameof(AppState.SlideStatus)", code);
        Assert.Contains("nameof(AppState.ScreenWatchStatus)", code);
        Assert.Contains("nameof(AppState.AnswerText)", code);
        Assert.True(File.Exists(Desktop("Views", "InsightsPanelWindow.axaml")));
        var window = Read("Views", "InsightsPanelWindow.axaml");
        Assert.Contains("<views:InsightsPanel />", window);
    }

    [Fact]
    public void ThePanelIsEmbeddedInTheLiveTranscriptColumn()
    {
        // Mac LiveSessionView TranscriptColumn: InsightsPanel().frame(height: 240) while
        // screenWatch && typeDefaults.followsSlides (the academic/report scenes).
        var live = Read("Views", "LiveView.axaml");
        Assert.Contains("<views:InsightsPanel", live);
        Assert.Contains("IsVisible=\"{Binding LiveShowsInsightsPanel}\"", live);
        Assert.Contains("Height=\"240\"", live);
        var state = Read("AppState.Insights.cs");
        Assert.Contains("public bool LiveShowsInsightsPanel", state);
        Assert.Contains("MeetingMode.Academic", state);
    }

    [Fact]
    public void TheMacNotesAndSectionTextsArePorted()
    {
        var panel = Read("Views", "InsightsPanel.axaml");
        // ScreenWatchOffHint: the long explanation and the 打开屏幕感知 button.
        Assert.Contains("{loc:Tr 打开“屏幕感知”后，每隔几秒在本机识别屏幕文字：跟随你讲到 PPT 的哪一页并给出这一页的讲稿提示；屏幕上出现题目或代码时，判断是否和对话相关并纳入回答。屏幕在本机识别；相关文字与必要的当前画面会提供给回答引擎。}", panel);
        Assert.Contains("{loc:Tr 打开屏幕感知}", panel);
        // ScreenSection: the kind title, the "N 秒前" clock and 展开识别的文字.
        Assert.Contains("{Binding ScreenAgeText}", panel);
        Assert.Contains("{loc:Tr 已识别 · 是否用于回答见“本次依据”}", panel);
        Assert.Contains("{loc:Tr 展开识别的文字}", panel);
        Assert.Contains("{loc:Tr 收起}", panel);
        // SlideSection: the points, 听众可能会问 and the generating note.
        Assert.Contains("{loc:Tr 听众可能会问（点一下提前准备回答）}", panel);
        Assert.Contains("{loc:Tr 正在根据这一页和你的资料写讲稿提示…}", panel);
        Assert.DoesNotContain("讲稿要点", panel);
        // AnswerProgressStrip: MetricPill 已讲 x/y and 下一句.
        Assert.Contains("{Binding AnswerProgressMetricText}", panel);
        Assert.Contains("{loc:Tr 已讲}", panel);
        Assert.Contains("{loc:Tr 下一句：}", panel);
        Assert.DoesNotContain("回答进度", panel);
        var state = Read("AppState.Insights.cs");
        Assert.Contains("public string AnswerProgressMetricText", state);
        Assert.Contains("UiText.Format(\"第 %lld / %lld 页\"", state);
    }

    [Fact]
    public void MacReferenceAndEvidenceExist()
    {
        var mac = File.ReadAllText(Path.Combine(Root(), "Sources", "NextThought", "InsightsPanel.swift"));
        foreach (var name in new[] { "struct InsightsPanel", "ScreenWatchOffHint", "SlideSection", "ScreenSection", "AnswerProgressStrip" })
            Assert.Contains(name, mac);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "validation", "screens", "insights", "compare.md")));
        // The old placeholder partial file must now carry the real port.
        var state = Read("AppState.Insights.cs");
        Assert.DoesNotContain("Mac extension placeholder", state);
    }
}
