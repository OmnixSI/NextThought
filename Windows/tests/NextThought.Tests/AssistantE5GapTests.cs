using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E5 remaining gaps: the assistant module preset menu uses the Mac sentences, the answer history
/// has its Mac empty-state sentence, the confidence bar follows the Mac thresholds, and the popped
/// out conversation/question windows use the Mac conversation bubbles and sizes.
/// </summary>
public sealed class AssistantE5GapTests
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
    public void TheLayoutPresetsMatchTheMacAssistantLayout()
    {
        // §6.5: shell parity follow-up. Mac defines the five AssistantLayout presets in
        // AssistantLayout.swift and only applies them from the demo/layout state; the old
        // Windows menu copy was removed with the LiveView rewrite, so the presets are asserted
        // where the Mac defines them: the Core layout model and AppState.
        var core = Read(Path.Combine("Windows", "src", "NextThought.Core", "AssistantLayout.cs"));
        Assert.Contains("public enum Preset { Standard, AnswerFirst, Presenting, Stacked, AnswerOnly }", core);
        Assert.Contains("public static AssistantLayout For(Preset preset)", core);
        foreach (var preset in new[] { "Preset.AnswerFirst", "Preset.Presenting", "Preset.Stacked", "Preset.AnswerOnly" })
            Assert.Contains(preset, core);
        Assert.Contains("_ => Standard", core);
        var state = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Assistant.cs"));
        Assert.Contains("public void ApplyAssistantPreset(AssistantLayout.Preset preset)", state);
        // The self-test walks the presets through the real state (Mac's demo applies them too).
        var check = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "SelfTest", "Checks", "RunAssistantUiCheckAsync.cs"));
        Assert.Contains("ApplyAssistantPreset(AssistantLayout.Preset.AnswerOnly)", check);
        Assert.Contains("ApplyAssistantPreset(AssistantLayout.Preset.Standard)", check);
    }

    [Fact]
    public void TheAnswerHistoryHasTheMacEmptyState()
    {
        var xaml = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "Assistant", "AnswerHistoryPanel.axaml"));
        Assert.Contains("{loc:Tr 回答过的问题会按时间列在这里，点一下就能回看当时的回答。}", xaml);
        Assert.Contains("IsVisible=\"{Binding !HasSessionAnswerHistory}\"", xaml);
        Assert.Contains("{loc:Tr 回答记录}", xaml);
    }

    [Fact]
    public void TheConfidenceBarSwitchesColourAtTheMacThresholds()
    {
        var code = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "Assistant", "DetectionStrip.axaml.cs"));
        Assert.Contains("score >= 0.6 ? \"Ready\"", code);
        Assert.Contains("score >= 0.4 ? \"Caution\"", code);
        Assert.Contains(": \"Tertiary\"", code);
        Assert.Contains("问题置信度", code);
        var xaml = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "Assistant", "DetectionStrip.axaml"));
        Assert.Contains("x:Name=\"ConfidenceBar\"", xaml);
    }

    [Fact]
    public void TheDetachedConversationUsesTheMacBubbleViews()
    {
        foreach (var name in new[] { "ConversationPanel", "ConversationBubble", "EmptyConversationHint", "PartialBubbles" })
        {
            var axaml = Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "Assistant", name + ".axaml");
            var code = Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "Assistant", name + ".axaml.cs");
            Assert.Contains($"x:Class=\"NextThought.Desktop.Views.Assistant.{name}\"", Read(axaml));
            Assert.True(File.Exists(Path.Combine(Root(), code)));
        }
        var window = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "DetachedModuleWindow.axaml"));
        Assert.Contains("<assistant:ConversationPanel", window);
        Assert.DoesNotContain("x:Name=\"ConversationPanel\" IsVisible=\"False\" RowDefinitions", window);
        Assert.Contains("{loc:Tr 合并回主窗口}", window);
        var bubble = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "Assistant", "ConversationBubble.axaml.cs"));
        Assert.Contains("Speaker == \"me\"", bubble);
        Assert.Contains("row.IsQuestion", bubble);
        Assert.Contains("AiSoft", bubble);
        Assert.Contains("TranscriptBubble", bubble);
    }

    [Fact]
    public void TheDetachedQuestionWindowUsesTheMacHeightAndTitles()
    {
        var code = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "DetachedModuleWindow.axaml.cs"));
        Assert.Contains("AssistantModule.Question => 160", code); // Mac DetachedModules defaultSize: 520×160
        Assert.Contains("AssistantModule.Question => \"当前问题与候选\"", code);
        Assert.Contains("AssistantModule.History => \"回答记录\"", code);
        // The other Mac default sizes stay (conversation 420×560, answer 560×480, history 380×460, insights 440×520).
        Assert.Contains("AssistantModule.Conversation => 420", code);
        Assert.Contains("AssistantModule.Conversation => 560", code);
        // answer/insights fall back to the default 560×480 in the size switch.
        Assert.Contains("_ => 560", code);
        Assert.Contains("_ => 480", code);
        Assert.Contains("AssistantModule.Insights => 440", code);
        Assert.Contains("AssistantModule.Insights => 520", code);
    }

    [Fact]
    public void TheMacSentencesHaveEnglishWording()
    {
        var table = Read(Path.Combine("Windows", "src", "NextThought.Core", "UiTextWindows.cs"));
        foreach (var key in new[]
                 {
                     "当前问题与候选", "回答记录", "合并回主窗口", "只看回答", "未在采集",
                     "对话在左 · 回答在右（默认）", "回答过的问题会按时间列在这里，点一下就能回看当时的回答。"
                 })
            Assert.Contains($"[\"{key}\"]", table);
    }
}
