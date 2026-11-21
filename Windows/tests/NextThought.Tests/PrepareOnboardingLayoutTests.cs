using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E2/E3 structure tests: the prepare page keeps the Mac layout pieces and the first-run guide is a
/// dedicated view (the old inline overlay in MainWindow.axaml is gone).
/// </summary>
public sealed class PrepareOnboardingLayoutTests
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

    private static string Desktop => Path.Combine(Root(), "Windows", "src", "NextThought.Desktop");

    [Fact]
    public void PreparePageKeepsTheMacLayoutPieces()
    {
        var xaml = File.ReadAllText(Path.Combine(Desktop, "Views", "PrepareView.axaml"));
        // D6 pieces that must survive an E2 rewrite.
        Assert.Contains("InkWash", xaml, StringComparison.Ordinal);
        Assert.Contains("Animated=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SceneGlyph", xaml, StringComparison.Ordinal);
        Assert.Contains("Size=\"44\"", xaml, StringComparison.Ordinal);
        // Mac PrepareComposer / TypePicker / SeriesCarryCard / SeriesBriefingPanel / Materials / CheckCard / StartBar.
        foreach (var element in new[]
                 {
                     "这场会议叫什么", "这是什么会", "根据标题选好了，可以改", "自定义…", "线上", "线下",
                     "DraftVenueHint", "系列与资料", "系列简报", "带入资料", "带入题库", "带入经验", "带入上一场转写",
                     "PrepareCarrySummary", "PrepareMaterialsRows", "PrepareMaterialsLine", "PrepareAudioReady",
                     "SystemAudioLevelPercent", "MicrophoneAudioLevelPercent", "VoiceTest_Click",
                     "PrepareInsightHeading", "PrepareInsightActionCommand", "CancelMaterialsInsightCommand",
                     "PrepareAnswerLine", "PrepareEngineStateLabel", "PrepareEngineActionLabel", "PrepareMicLine",
                     "ImportPreparedDocumentsCommand", "ContinueWithoutMicrophoneCommand", "UseMicrophoneAgainCommand",
                     "TestAnswerEngineCommand", "DragDrop.AllowDrop", "CancelDraftCommand", "开始记录"
                 })
            Assert.Contains(element, xaml, StringComparison.Ordinal);
        var state = File.ReadAllText(Path.Combine(Desktop, "AppState.Prepare.cs"));
        Assert.Contains("PreparedMaterialCount", state, StringComparison.Ordinal);
        Assert.Contains("ImportPreparedDocumentsAsync", state, StringComparison.Ordinal);
        // E2 round 3: the ported MeetingDraft and its page state.
        var draftModel = File.ReadAllText(Path.Combine(Desktop, "MeetingDraft.cs"));
        foreach (var element in new[] { "class MeetingDraft", "TypeChosen", "CustomBaseMode", "Carry", "BaselineDocumentIds", "class SceneSummary", "defaultsSummary" })
            Assert.Contains(element, draftModel, StringComparison.Ordinal);
        var draftState = File.ReadAllText(Path.Combine(Desktop, "AppState.PrepareDraft.cs"));
        foreach (var element in new[]
                 {
                     "EnsureDraft", "DraftMeetingPlan", "SetDraftType", "SetDraftCustomType", "SetDraftCustomBase",
                     "DraftGuessNoteVisible", "PrepareMaterialsRows", "ImportDraftDocumentsAsync",
                     "ContinueWithoutMicrophoneAsync", "UseMicrophoneAgainAsync", "StartPreparedMeetingAsync",
                     "DraftOffline", "DraftOnline"
                 })
            Assert.Contains(element, draftState, StringComparison.Ordinal);
        // The ComboBox re-entrancy fix must stay explicit.
        var code = File.ReadAllText(Path.Combine(Desktop, "Views", "PrepareView.axaml.cs"));
        Assert.Contains("applyingDraft", code, StringComparison.Ordinal);
        Assert.Contains("TryGetFilesAsync", code, StringComparison.Ordinal);
        Assert.Contains("VoiceTest_Click", code, StringComparison.Ordinal);
        // task-33: MaterialsInsight reads the materials through the ported preparation prompt.
        var insight = File.ReadAllText(Path.Combine(Desktop, "AppState.PrepareInsight.cs"));
        foreach (var element in new[]
                 {
                     "ReadMaterialsInsightAsync", "PreparationPrompt.System", "PreparationPrompt.Output",
                     "PrepareInsightState", "PrepareEngineStateLabel", "UpdateAudioLevels", "DraftVenueHint",
                     "还没有配置回答服务"
                 })
            Assert.Contains(element, insight, StringComparison.Ordinal);
        // The start path builds the ported MeetingPlan, like Mac startPreparedMeeting.
        var live = File.ReadAllText(Path.Combine(Desktop, "AppState.Live.cs"));
        Assert.Contains("BeginPreparedMeeting(planSeries)", live, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstRunGuideIsItsOwnView()
    {
        var view = Path.Combine(Desktop, "Views", "OnboardingView.axaml");
        Assert.True(File.Exists(view), view);
        var xaml = File.ReadAllText(view);
        foreach (var element in new[]
                 {
                     "OnboardingIsScene", "OnboardingIsSound", "OnboardingIsAnswers", "OnboardingIsStart",
                     "OnboardingPosition", "OnboardingStepTitle", "OnboardingStepTime", "稍后继续",
                     "看一场示例会议", "OnboardingSampleToggle", "提词岛示意", "提词岛示意", "设置清单",
                     "先看看", "开始第一场", "回到声音"
                 })
            Assert.Contains(element, xaml, StringComparison.Ordinal);
        // task-34: 示例会议面板（Mac OnboardingSampleMeeting）逐元素。
        foreach (var element in new[]
                 {
                     "OnboardingSampleVisibleLines", "OnboardingSampleVisibleTakeaways", "OnboardingSampleProgress",
                     "OnboardingSampleSuggestionQuestion", "OnboardingSampleSuggestionAnswer",
                     "ReplayOnboardingSampleCommand", "实时转写", "面试记录", "重播"
                 })
            Assert.Contains(element, xaml, StringComparison.Ordinal);
        var sample = File.ReadAllText(Path.Combine(Desktop, "OnboardingSample.cs"));
        Assert.Contains("OnboardingSampleLine", sample, StringComparison.Ordinal);
        Assert.Contains("OnboardingSampleTakeaway", sample, StringComparison.Ordinal);
        var sampleState = File.ReadAllText(Path.Combine(Desktop, "AppState.OnboardingSample.cs"));
        Assert.Contains("ScriptTimeline", sampleState, StringComparison.Ordinal);
        Assert.Contains("DispatcherTimer", sampleState, StringComparison.Ordinal);
        // 切步动效: Motion.phase port (0.46 s fade) on every step change.
        var motion = File.ReadAllText(Path.Combine(Desktop, "Views", "Motion.cs"));
        Assert.Contains("PhaseIn", motion, StringComparison.Ordinal);
        Assert.Contains("Phase = TimeSpan.FromMilliseconds(460)", motion, StringComparison.Ordinal);
        var guideCode = File.ReadAllText(Path.Combine(Desktop, "Views", "OnboardingView.axaml.cs"));
        Assert.Contains("AnimatePageAsync", guideCode, StringComparison.Ordinal);
        // 设置清单挂在 Mac 的通用页。
        var settings = File.ReadAllText(Path.Combine(Desktop, "Views", "SettingsView.axaml"));
        Assert.Contains("SettingsSetupChecklist", settings, StringComparison.Ordinal);
        Assert.Contains("Tag=\"General\"", settings, StringComparison.Ordinal);
        var window = File.ReadAllText(Path.Combine(Desktop, "Views", "MainWindow.axaml"));
        Assert.Contains("<views:OnboardingView", window, StringComparison.Ordinal);
        Assert.DoesNotContain("OnboardingStepTitle", window, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Desktop, "Views", "OnboardingView.axaml.cs")));
        Assert.True(File.Exists(Path.Combine(Desktop, "SelfTest", "Checks", "RunPrepareCheckAsync.cs")));
        Assert.True(File.Exists(Path.Combine(Desktop, "SelfTest", "Checks", "RunOnboardingCheckAsync.cs")));
    }
}
