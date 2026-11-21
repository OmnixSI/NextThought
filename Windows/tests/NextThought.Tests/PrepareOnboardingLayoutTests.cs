using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E2/E3 structure tests: the prepare page follows Mac PrepareView.swift 3.4.0 and the first-run
/// guide follows Onboarding.swift; both are dedicated views (the old inline overlay in
/// MainWindow.axaml is gone). The per-element visual comparison lives in
/// validation/screens/{prepare,onboarding}/compare.md.
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
        // Mac PrepareComposer / VenuePicker / TypePicker, top to bottom.
        foreach (var element in new[]
                 {
                     "这场会议叫什么", "这是什么会", "根据标题选好了，可以改", "自定义…", "线上", "线下",
                     "DraftVenueIndex", "DraftSceneChips", "DraftPromise", "DraftSummary", "DraftMomentRows",
                     "PrepaiView", "prepareSectionTitle"
                 })
            Assert.Contains(element, xaml, StringComparison.Ordinal);
        // Mac sections between hairlines: 这场用什么 / 接着上一场 / 这次新加的资料 / 开始前检查.
        foreach (var element in new[]
                 {
                     "这场用什么", "不用的可以关掉", "恢复这个场景的默认", "始终开启",
                     "实时转写", "自动回答", "实时纪要", "同声传译", "区分说话人",
                     "DraftCarryHasMaterials", "DraftCarryHasReview", "DraftCarryHasLessons", "DraftCarryHasTranscript",
                     "接着上一场", "系列简报", "这次新加的资料", "可跳过",
                     "PrepareMaterialsRows", "把文件拖到这里", "PrepareInsightActionCommand",
                     "开始前检查", "PrepareAudioTone", "PrepareAiTone", "PrepareMicLine",
                     "TestAnswerEngineCommand", "VoiceTest_Click"
                 })
            Assert.Contains(element, xaml, StringComparison.Ordinal);
        foreach (var element in new[] { "PrepareStartDetail", "PrepareStartLabel", "CancelDraftCommand", "StartPreparedMeetingCommand" })
            Assert.Contains(element, xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("系列与资料", xaml, StringComparison.Ordinal);
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
                     "OnboardingProgressCapsules", "OnboardingSceneChips", "OnboardingSceneMomentRows",
                     "OnboardingSampleToggle", "OnboardingSampleVisibleLines", "OnboardingSampleVisibleTakeaways",
                     "OnboardingSampleProgress", "OnboardingSampleSuggestionQuestion", "OnboardingSampleSuggestionAnswer",
                     "ReplayOnboardingSampleCommand", "实时转写", "面试记录", "重播",
                     "OnboardingVenueDetail", "OnboardingOnline", "OnboardingOffline",
                     "OnboardingMicrophoneDetail", "OnboardingMicrophoneRecordsMe", "OnboardingMicrophoneOnlyOther",
                     "OnboardingRecognitionDetail", "OnboardingDataRouteSummary",
                     "OnboardingAnswerProviders", "OnboardingAnswerServiceTitle", "OnboardingModelLine",
                     "OnboardingTestAnswerHint", "ToggleAnswerOptionsCommand", "ToggleRecognitionOptionsCommand",
                     "OnboardingCueIslandQuestion", "OnboardingCueIslandAnswer", "OnboardingCuePoint1", "OnboardingCuePoint2",
                     "先看看", "开始第一场", "回到声音"
                 })
            Assert.Contains(element, xaml, StringComparison.Ordinal);
        // Mac frame: header 32/32, content 720 wide, footer 32/16 with the actions on the right.
        Assert.Contains("Width=\"720\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"32,32,32,12\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"32,16\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("设置清单", xaml, StringComparison.Ordinal);
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
        // 设置清单挂在 Mac 的通用页, not in the guide.
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
