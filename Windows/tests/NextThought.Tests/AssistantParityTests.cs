using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E5: the answer-area modules follow AssistantView.swift / AssistantLayout.swift /
/// DetachedModules.swift — answer history (live + archived), detection strip with confidence,
/// screenshot card, module layout and the detached module windows.
/// </summary>
public sealed class AssistantParityTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop(params string[] parts) =>
        Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Desktop(parts));

    [Fact]
    public void EveryAnswerModuleHasASameNamedControl()
    {
        foreach (var name in new[] { "AnswerHistoryPanel", "DetectionStrip", "ScreenshotCard" })
        {
            Assert.True(File.Exists(Desktop("Views", "Assistant", name + ".axaml")), name + ".axaml missing");
            var code = Read("Views", "Assistant", name + ".axaml.cs");
            Assert.Contains("public partial class " + name, code);
        }
        Assert.True(File.Exists(Desktop("Views", "DetachedModuleWindow.axaml")));
        var window = Read("Views", "DetachedModuleWindow.axaml.cs");
        Assert.Contains("public partial class DetachedModuleWindow", window);
        Assert.Contains("public AssistantModule Module", window);
        Assert.Contains("DockDetachedModule", window);
    }

    [Fact]
    public void AnswerHistoryReadsLiveThenArchivedAnswers()
    {
        var state = Read("AppState.Assistant.cs");
        Assert.Contains("public ObservableCollection<AnswerRecord> SessionAnswerHistory", state);
        Assert.Contains("SelectedSessionAnswers", state);
        Assert.Contains("CurrentAnswerHistory", state);
        Assert.Contains("public void RefreshSessionAnswerHistory()", state);
        Assert.Contains("public void SelectAnswerHistory(int index)", state);
        Assert.Contains("AnswerHistoryCursor", state);
        Assert.Contains("HistoryPositionText", state);
        Assert.Contains("DisplayedAnswerText", state);
        Assert.Contains("DisplayedQuestionText", state);
        var rows = Read("AppState.Assistant.cs");
        Assert.Contains("AnswerHistoryRow", rows);
        Assert.Contains("AnswerRefinements.OriginalQuestion", rows);
        var panel = Read("Views", "Assistant", "AnswerHistoryPanel.axaml");
        Assert.Contains("{Binding AnswerHistoryRows}", panel);
        Assert.Contains("{loc:Tr 回到最新}", panel);
        Assert.Contains("{loc:Tr 历史回答}", panel);
        Assert.Contains("Classes.active=\"{Binding Shown}\"", panel);
    }

    [Fact]
    public void DetectionStripShowsTheCandidateAndConfidence()
    {
        var strip = Read("Views", "Assistant", "DetectionStrip.axaml");
        Assert.Contains("{Binding LiveCandidateText}", strip);
        Assert.Contains("{Binding CandidateConfidence}", strip);
        Assert.Contains("{loc:Tr 可能的问题}", strip);
        Assert.Contains("{loc:Tr 回答这句}", strip);
        Assert.Contains("AnswerLiveCandidate", Read("Views", "Assistant", "DetectionStrip.axaml.cs"));
        Assert.Contains("DetectionStrip", Read("Views", "DetachedModuleWindow.axaml"));
    }

    [Fact]
    public void ScreenshotCardShowsTheWaitingScreenshot()
    {
        var card = Read("Views", "Assistant", "ScreenshotCard.axaml");
        Assert.Contains("{Binding HasLiveScreenshot}", card);
        Assert.Contains("{Binding ScreenshotPreview}", card);
        Assert.Contains("{loc:Tr 取消}", card);
        Assert.Contains("{loc:Tr 分析截图}", card);
        var code = Read("Views", "Assistant", "ScreenshotCard.axaml.cs");
        Assert.Contains("ClearScreenshotPreview", code);
        Assert.Contains("AnalyzeScreenshotAsync", code);
        // The composer and the detached answer module both use the one card.
        Assert.Contains("assistant:ScreenshotCard", Read("Views", "LiveView.axaml"));
        Assert.Contains("assistant:ScreenshotCard", Read("Views", "DetachedModuleWindow.axaml"));
    }

    [Fact]
    public void AssistantLayoutPortsTheMacSlotsAndPresets()
    {
        var core = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Core", "AssistantLayout.cs"));
        Assert.Contains("public enum AssistantModule", core);
        foreach (var module in new[] { "Conversation", "Question", "Answer", "History", "Insights" })
            Assert.Contains(module + ",", core);
        Assert.Contains("public sealed record AssistantLayout", core);
        Assert.Contains("public static AssistantLayout Standard", core);
        Assert.Contains("public enum Preset { Standard, AnswerFirst, Presenting, Stacked, AnswerOnly }", core);
        Assert.Contains("UsesTwoColumns", core);
        Assert.Contains("public AssistantLayout Place(", core);
        Assert.Contains("public AssistantLayout Detach(", core);
        Assert.Contains("public AssistantLayout Dock(", core);
        Assert.Contains("public static AssistantLayout FromJson", core);
    }

    [Fact]
    public void LayoutActionsOpenAndCloseDetachedWindows()
    {
        var state = Read("AppState.Assistant.cs");
        Assert.Contains("public void ApplyAssistantPreset", state);
        Assert.Contains("public void OpenDetachedModule", state);
        Assert.Contains("public void DockDetachedModule", state);
        Assert.Contains("SyncDetachedModuleWindows", state);
        Assert.Contains("assistant-layout.json", state);
        Assert.Contains("new DetachedModuleWindow", state);
        var live = Read("Views", "LiveView.axaml.cs");
        Assert.Contains("ModuleWindow_Click", live);
        Assert.Contains("LayoutPreset_Click", live);
        Assert.Contains("LatestAnswer_Click", live);
        Assert.Contains("OpenDetachedModule(AssistantModule.Answer)", live);
    }

    [Fact]
    public void TheAssistantSelfTestAndEvidenceExist()
    {
        var check = Read("SelfTest", "Checks", "RunAssistantUiCheckAsync.cs");
        Assert.Contains("RunAssistantUiCheckAsync", check);
        Assert.Contains("SessionAnswerHistory", check);
        Assert.Contains("SelectAnswerHistory(0)", check);
        Assert.Contains("ApplyAssistantPreset", check);
        Assert.Contains("OpenDetachedModule", check);
        Assert.Contains("assistant-ui-check: passed", check);
        Assert.Contains("CaptureVisual(window", check);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "validation", "screens", "assistant", "compare.md")));
    }
}
