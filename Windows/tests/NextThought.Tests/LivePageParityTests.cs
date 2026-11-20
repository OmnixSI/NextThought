using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E4: the live page follows LiveSessionView.swift — top bar (timer/status, scene, mode switch,
/// phone/screen status, answer settings, tools), transcript (question marker, candidate strip,
/// status line), answers column (header, web status/sources, refinement row, evidence
/// line/details), the notes tab and the bottom composer.
/// </summary>
public sealed class LivePageParityTests
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

    private static string Live() => Read("Views", "LiveView.axaml");

    [Fact]
    public void TopBarShowsTheMacRecordingTimerAndStatus()
    {
        var live = Live();
        Assert.Contains("{Binding RecordingElapsedText}", live);
        Assert.Contains("{Binding LiveListeningStatus}", live);
        Assert.Contains("{Binding LivePauseButtonText}", live);
        Assert.Contains("TogglePauseListeningCommand", live);
        Assert.Contains("{Binding !IsLiveOnAir}", live);
        Assert.Contains("{Binding IsLiveOnAir}", live);
        var code = Read("Views", "LiveView.axaml.cs");
        Assert.Contains("AnswerModeAuto_Click", code);
        Assert.Contains("AnswerModeRecord_Click", code);
        Assert.Contains("SetAnswerMode(true)", code);
    }

    [Fact]
    public void TopBarHasTheModeSwitchPhoneScreenAndAnswerSettings()
    {
        var live = Live();
        // 自动回答 / 只记录
        Assert.Contains("{loc:Tr 自动回答}", live);
        Assert.Contains("{loc:Tr 只记录}", live);
        Assert.Contains("AnswerModeIsAuto", live);
        // phone and screen status icons with their dots (Mac PhoneStatusIcon/ScreenStatusIcon)
        Assert.Contains("IconIphone", live);
        Assert.Contains("LiveCompanionOn", live);
        Assert.Contains("IconDisplay", live);
        Assert.Contains("LiveScreenWatchOn", live);
        // AnswerSettingsMenu: model / fast / web / answer length / applies next answer
        Assert.Contains("LiveModelChoices", live);
        Assert.Contains("LiveModel_SelectionChanged", live);
        Assert.Contains("LiveFastMode", live);
        Assert.Contains("LiveWebSearchIsOn", live);
        Assert.Contains("{loc:Tr 回答长度}", live);
        Assert.Contains("{loc:Tr 从下一个回答开始生效}", live);
        Assert.Contains("{loc:Tr 更多回答设置…}", live);
        // more menu: cue island, presenter mode, answer language, window mode, settings
        Assert.Contains("{loc:Tr 提词岛一直展开}", live);
        Assert.Contains("{loc:Tr 双屏演讲}", live);
        Assert.Contains("{loc:Tr 回答语言}", live);
        Assert.Contains("{loc:Tr 英文回答附中文对照}", live);
        Assert.Contains("{loc:Tr 屏幕感知}", live);
        Assert.Contains("{loc:Tr 手机伴侣}", live);
        Assert.Contains("{loc:Tr 回答浮窗}", live);
        Assert.Contains("{loc:Tr 会中窗口}", live);
        Assert.Contains("{loc:Tr 设置…}", live);
    }

    [Fact]
    public void TranscriptHasTheQuestionMarkerCandidateStripAndStatusLine()
    {
        var live = Live();
        // QuestionMark: a "问题" capsule driven by the row's detection state, using the scene pigment.
        Assert.Contains("{Binding IsQuestion}", live);
        Assert.Contains("{loc:Tr 问题}", live);
        Assert.Contains("LiveSceneTintSoftBrush", live);
        Assert.Contains("LiveSceneTintBrush", live);
        // CandidateStrip
        Assert.Contains("{Binding LiveCandidateText}", live);
        Assert.Contains("HasLiveCandidate", live);
        Assert.Contains("{loc:Tr 可能的问题}", live);
        Assert.Contains("ForceAnswerCommand", live);
        Assert.Contains("AnswerLine_Click", live);
        Assert.Contains("AnswerCandidate_Click", Read("Views", "LiveView.axaml.cs"));
        Assert.Contains("LiveSeriesName", live);
        // TranscriptStatus
        Assert.Contains("{Binding AudioSourceText}", live);
        Assert.Contains("{Binding LiveAudioEngineLabel}", live);
        Assert.Contains("IconLock", live);
        // partial lines with speaker labels
        Assert.Contains("{Binding SystemPartial}", live);
        Assert.Contains("{Binding MicrophonePartial}", live);
    }

    [Fact]
    public void QuestionMarkerComesFromTheQuestionDetector()
    {
        var row = Read("TranscriptRow.cs");
        Assert.Contains("public bool IsQuestion", row);
        Assert.Contains("QuestionDetector.LooksLikeQuestion", row);
        Assert.Contains("Speaker != \"me\"", row);
        var state = Read("AppState.LiveUi.cs");
        Assert.Contains("LiveSceneTintBrush", state);
        Assert.Contains("SceneTheme.For(SelectedMode).Tint(", state);
    }

    [Fact]
    public void RightColumnSwitchesBetweenNotesAndAnswersLikeMac()
    {
        var live = Live();
        Assert.Contains("{loc:Tr 我的笔记}", live);
        Assert.Contains("{loc:Tr 问答}", live);
        Assert.Contains("NotesTab_Click", live);
        Assert.Contains("AnswerTab_Click", live);
        Assert.Contains("{Binding AnswerModeIsAuto, Mode=OneWay}", live);
        var code = Read("Views", "LiveView.axaml.cs");
        Assert.Contains("ShowRightTab", code);
        Assert.Contains("AnswerPanel.IsVisible", code);
        Assert.Contains("NotesPanel.IsVisible", code);
    }

    [Fact]
    public void AnswerColumnHasHeaderHistoryWebStatusSourcesAndRefinement()
    {
        var live = Live();
        Assert.Contains("{Binding AnswerColumnTitle}", live);
        Assert.Contains("PreviousAnswerCommand", live);
        Assert.Contains("NextAnswerCommand", live);
        Assert.Contains("{Binding HasWebResults}", live);
        Assert.Contains("{Binding LiveWebStatusLine}", live);
        Assert.Contains("{Binding LiveWebSources}", live);
        Assert.Contains("WebSource_Click", live);
        Assert.Contains("{loc:Tr 更简短}", live);
        Assert.Contains("{loc:Tr 举例}", live);
        Assert.Contains("{loc:Tr 英文版}", live);
        Assert.Contains("{loc:Tr 展开}", live);
        Assert.Contains("{loc:Tr STAR}", live);
        Assert.Contains("{loc:Tr 中文版}", live);
        Assert.Contains("{loc:Tr 澄清问题}", live);
        Assert.Contains("{loc:Tr 我可以反问}", live);
        Assert.Contains("{loc:Tr 重新生成}", live);
        Assert.Contains("{loc:Tr 联网查一下}", live);
        Assert.Contains("{loc:Tr 复制回答}", live);
        Assert.Contains("{loc:Tr 停止生成}", live);
        Assert.Contains("RefineAsync", Read("Views", "LiveView.axaml.cs"));
    }

    [Fact]
    public void EvidenceLineAndDetailsShowWhatTheAnswerWasBasedOn()
    {
        var live = Live();
        Assert.Contains("{Binding EvidenceSummary}", live);
        Assert.Contains("{Binding EvidenceRetrievalText}", live);
        Assert.Contains("{Binding EvidenceMaterialRows}", live);
        Assert.Contains("{Binding EvidenceScreenSource}", live);
        Assert.Contains("{Binding EvidenceWebSourceRows}", live);
        Assert.Contains("{Binding EvidenceNoteRows}", live);
        Assert.Contains("EvidenceHasLimits", live);
        var state = Read("AppState.LiveUi.cs");
        Assert.Contains("public string EvidenceSummary", state);
        Assert.Contains("UiText.Format(\"资料 %lld\"", state);
        Assert.Contains("UiText.Format(\"依据：%@\"", state);
        Assert.Contains("项限制", state);
        var liveState = Read("AppState.Live.cs");
        Assert.Contains("new EvidenceBundle(Guid.NewGuid())", liveState);
        Assert.Contains("Evidence = evidence", liveState);
    }

    [Fact]
    public void ComposerIsTheMacBottomInputRow()
    {
        var live = Live();
        Assert.Contains("{Binding LiveComposerPlaceholder}", live);
        Assert.Contains("{Binding CanSubmitLiveComposer}", live);
        Assert.Contains("Composer_KeyDown", live);
        Assert.Contains("Composer_Send_Click", live);
        Assert.Contains("IconCameraViewfinder", live);
        Assert.Contains("{loc:Tr 框选区域}", live);
        Assert.Contains("{loc:Tr 选择窗口}", live);
        Assert.Contains("{loc:Tr 立即回答}", live);
        Assert.Contains("{Binding ForceAnswerShortcut}", live);
        // E5: the screenshot strip is the shared 截图卡 module.
        Assert.Contains("assistant:ScreenshotCard", live);
        var state = Read("AppState.LiveUi.cs");
        Assert.Contains("public void SubmitLiveComposer()", state);
        Assert.Contains("AnalyzeScreenshotAsync", state);
    }

    [Fact]
    public void TheLiveSelfTestExercisesThePage()
    {
        var check = Read("SelfTest", "Checks", "RunLiveUiCheckAsync.cs");
        Assert.Contains("RunLiveUiCheckAsync", check);
        Assert.Contains("DemoStages.Live", check);
        Assert.Contains("SetAnswerMode(false)", check);
        Assert.Contains("HasDisplayedEvidence", check);
        Assert.Contains("LiveWebSources", check);
        Assert.Contains("SetScreenshotPreview", check);
        Assert.Contains("live-ui-check: passed", check);
        Assert.True(File.Exists(Desktop("..", "..", "..", "..", "Windows", "validation", "screens", "live", "compare.md"))
                    || File.Exists(Path.Combine(Root(), "Windows", "validation", "screens", "live", "compare.md")));
    }
}
