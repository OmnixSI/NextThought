using Xunit;

namespace NextThought.Tests;

/// <summary>H1: main window keeps only window code; self-checks live under Desktop/SelfTest.</summary>
public sealed class SelfTestStructureTests
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



    [Fact]
    public void LegacyPerCheckFlagsAreGone()
    {
        var root = Root();
        var startup = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "StartupOptions.cs"));
        Assert.DoesNotContain("SelfTestOptions", startup);
        Assert.False(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTestOptions.cs")));
    }











    [Fact]
    public void RecordingHidesNavigationForFocusMode()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        var marker = "IsVisible=\"{Binding !Recording}\"";
        Assert.True(main.Split(marker).Length - 1 >= 3);
    }



    [Fact]
    public void AcademicAnswersUseWholeSeriesMaterial()
    {
        // G8 moved the answer prompt builder next to the live request path (AppState.Live.cs).
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("SelectedMode == MeetingMode.Academic", live);
        Assert.Contains("WholeMaterialOf(DocumentsForRetrieval())", live);
    }

    [Fact]
    public void ContextSidebarIsCollapsibleAndPersisted()
    {
        var root = Root();
        var main = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("ContextSidebarVisible", main);
        Assert.Contains("ToggleContextSidebarCommand", main);
        var nav = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "AppState.Navigation.cs"));
        Assert.Contains("ContextSidebarHidden", nav);
        var settings = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Core", "Models.cs"));
        Assert.Contains("ContextSidebarHidden", settings);
        var model = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "ViewModels", "SettingsViewModel.cs"));
        Assert.Contains("ContextSidebarHidden", model);
    }












































    [Fact]
    public void OnboardingHasVoiceTestPage()
    {
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "OnboardingVoiceWindow.axaml")));
        var view = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "OnboardingView.axaml"));
        var code = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "OnboardingView.axaml.cs"));
        Assert.Contains("声音测试", view);
        Assert.Contains("VoiceTest_Click", code);
    }

    [Fact]
    public void OnboardingOffersASampleMeeting()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "OnboardingView.axaml"));
        Assert.Contains("看一场示例会议", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Onboarding.cs"));
        Assert.Contains("OpenOnboardingSample", state);
    }

    [Fact]
    public void OnboardingHasChecklistAndCueMock()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "OnboardingView.axaml"));
        Assert.Contains("OnboardingChecks", xaml);
        Assert.Contains("提词岛示意", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Onboarding.cs"));
        Assert.Contains("OnboardingCheckRow", state);
    }

    [Fact]
    public void OnboardingOverlayExists()
    {
        var window = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("OnboardingVisible", window);
        Assert.Contains("<views:OnboardingView", window);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "OnboardingView.axaml"));
        Assert.Contains("NextOnboardingStepCommand", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Onboarding.cs"));
        Assert.Contains("OnboardingVersion.NeedsGuide", state);
    }


























































    [Fact]
    public void MeetingDeletionUsesWorkspaceSourceIntegrity()
    {
        var deletion = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Deletion.cs"));
        Assert.Contains("WorkspaceSourceIntegrity.RemoveMeetings", deletion);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Core", "WorkspaceSourceIntegrity.cs")));
    }

    [Fact]
    public void ScreenWatchIsSingleFrameDefaultOffAndVisible()
    {
        var models = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Core", "Models.cs"));
        Assert.Contains("InsightScreenWatch", models);
        Assert.Contains("InsightScreenInterval", models);
        Assert.Contains("= 1;", models);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.ScreenWatch.cs"));
        Assert.Contains("ScreenOcrReader", state);
        Assert.Contains("StartupOptions.Demo", state);
        Assert.Contains("AddSeconds(20)", state);
        Assert.Contains("ScreenWatchCapturing", state);
        var reader = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "ScreenOcrReader.cs"));
        Assert.Contains("InMemoryRandomAccessStream", reader);
        Assert.DoesNotContain("File.WriteAllBytes", reader);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("正在截屏读取屏幕", live);
    }

    [Fact]
    public void MyVoiceUsesTheExistingSpeakerEmbedder()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.MyVoice.cs"));
        Assert.Contains("SpeakerEmbedder", state);
        Assert.Contains("VoiceEnrollment.Make", state);
        Assert.Contains("VoicePrompts", state);
        Assert.Contains("Voiceprint", state);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MyVoiceWindow.axaml")));
        var recorder = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "VoiceRecorder.cs"));
        Assert.Contains("AudioCaptureService", recorder);
        Assert.Contains("16000", recorder.Replace("16 kHz", "16000"));
    }

    [Fact]
    public void HistoryListIsReadOffTheUiThread()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Archive.cs"));
        Assert.Contains("RefreshHistoryAsync", state);
        Assert.Contains("public void RefreshHistory(Guid? select = null) => _ = RefreshHistoryAsync(select);", state);
        Assert.DoesNotContain("var list = Sessions.List();", state);
        var startup = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.cs"));
        Assert.Contains("_ = RefreshHistoryAsync()", startup);
    }

    [Fact]
    public void HistoryAsyncHandlersShowErrors()
    {
        var view = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml.cs"));
        Assert.Contains("操作失败：", view);
    }

    [Fact]
    public void UpdatesArePrefetchedBeforeInstall()
    {
        var update = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Update.cs"));
        Assert.Contains("PrefetchInstallerAsync", update);
        Assert.Contains("prefetchedInstaller", update);
        // F16: the check kicks off the background download, and the button waits for it.
        Assert.Contains("_ = StartInstallerPrefetch(report)", update);
        Assert.Contains("UpdateInstallerReady", update);
        Assert.Contains("UpdateInstaller.PrepareAsync", update);
        Assert.Contains("UpdateInstaller.VerifyAsync", update);
        Assert.Contains("Dispatcher.UIThread.InvokeAsync", update);
        var helper = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "UpdateInstaller.cs"));
        Assert.Contains("UpdateChecksums.Verify", helper);
        Assert.Contains("SHA-256", helper);
    }

    [Fact]
    public void SettingsAsyncHandlersShowErrors()
    {
        var view = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        Assert.Contains("刷新声音设备失败", view);
        Assert.Contains("打开术语表失败", view);
    }

    [Fact]
    public void SessionContextIsBuiltOffTheUiThread()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("Task.Run(() => new SessionContextSnapshot", live);
        // F20: the prepared plan may read the previous meeting's transcript; that read is off the UI thread too.
        Assert.Contains("await Task.Run(() => BeginPreparedMeeting(planSeries))", live);
    }

    [Fact]
    public void ExitWaitsForShutdownWithATimeout()
    {
        var window = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs"));
        Assert.Contains("e.Cancel = true;", window);
        // Closing while recording goes to the tray (the meeting keeps running); only a real exit waits.
        Assert.Contains("if (state.Recording && !ExitRequested)", window);
        Assert.Contains("ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(15))", window);
        var app = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "App.axaml.cs"));
        Assert.Contains("ShutdownRequested", app);
        Assert.Contains("args.Cancel = true", app);
        Assert.Contains("state.AllowClose", app);
        // Ending a meeting also waits for every pending write before meta is closed out.
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        var stop = live.IndexOf("private async Task StopRecordingCoreAsync", StringComparison.Ordinal);
        Assert.True(stop >= 0);
        var stopBody = live[stop..Math.Min(live.Length, stop + 2600)];
        Assert.Contains("await Task.WhenAll(writes.ToArray())", stopBody);
        Assert.Contains("await Sessions.EndAsync(meta)", stopBody);
    }

    [Fact]
    public void TitleBarOffersInstallAndRestart()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("重启以更新", xaml);
        Assert.Contains("CanInstallUpdateInApp", xaml);
        // F16: Updater.phase == .ready — the button only appears once the download finished.
        var update = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Update.cs"));
        Assert.Contains("UpdateInstallerReady && !InstallingUpdate", update);
    }

    [Fact]
    public void RollingUpdateUsesTheManifest()
    {
        var catalog = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Core", "UpdateCatalog.cs"));
        Assert.Contains("ManifestAsset", catalog);
        var check = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "UpdateCheck.cs"));
        Assert.Contains("UpdateManifest.Decode", check);
        Assert.Contains("UpdateCatalog.IsNewer(manifest", check);
        Assert.Contains("ManifestAsset", check);
        // Updater.check: the manifest must name this release before it decides anything.
        Assert.Contains("Describes(manifest, candidate)", check);
    }

    [Fact]
    public void CompanionScopeMatchesMac()
    {
        var server = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "CompanionServer.cs"));
        Assert.DoesNotContain("100.64", server);
        Assert.Contains("CancellationTokenSource lifetime = new();", server);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "validation", "companion", "COMPARISON.md")));
    }

    [Fact]
    public void CompanionSnapshotCarriesPositionAnswerIdAndPhase()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("Position: companionHistory.Count", live);
        Assert.Contains("AnswerID:", live);
        Assert.Contains("Phase:", live);
        Assert.Contains("重新生成一版与上一版不同的回答", live);
    }

    [Fact]
    public void CompanionActionsAckImmediately()
    {
        var server = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "CompanionServer.cs"));
        Assert.Contains("F10", server);
        Assert.Contains("Task.Run(async () =>", server);
        Assert.DoesNotContain("await action(query[", server);
    }

    [Fact]
    public void CloudSttSettingsTextMatchesBehavior()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        Assert.Contains("实时记录与导入 WAV 都走云端识别", xaml);
        Assert.DoesNotContain("实时记录仍用本机", xaml);
    }

    [Fact]
    public void CloudSttKeyIsNotErasedAfterReadFailure()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("CloudSttKeyReadFailed", live);
        var policy = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "CloudSttEndpointPolicy.cs"));
        Assert.Contains("wss", policy);
        Assert.Contains("AllowedHosts", policy);
    }

    [Fact]
    public void CloudMessagesAreReassembled()
    {
        var session = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "CloudSttSession.cs"));
        Assert.Contains("EndOfMessage", session);
        var client = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "CloudSttClient.cs"));
        Assert.Contains("EndOfMessage", client);
        Assert.Contains("TimeSpan.FromMinutes(30)", client);
    }

    [Fact]
    public void CloudModeCanUseBothAudioSources()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("cloudSttMicrophone", live);
        Assert.Contains("AudioSource.Microphone", live);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.cs"));
        Assert.Contains("cloudSttMicrophone", state);
    }

    [Fact]
    public void UiExceptionIsShownInStatus()
    {
        var app = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "App.axaml.cs"));
        Assert.Contains("界面操作出错", app);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.cs"));
        Assert.Contains("public static AppState? Current", state);
    }

    [Fact]
    public void DeviceSwitchStopsTheOldPipe()
    {
        var audio = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "AudioCaptureService.cs"));
        Assert.Contains("previous?.Stop();", audio);
        Assert.Contains("previous?.Dispose();", audio);
        Assert.Contains("explicitly chosen device is not overridden", audio);
    }

    [Fact]
    public void CloudSttFinalizesBeforeCancel()
    {
        var cloud = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "CloudSttSession.cs"));
        Assert.Contains("await StopAsync();", cloud);
        Assert.Contains("lifetime.Cancel();", cloud);
        Assert.True(cloud.IndexOf("await StopAsync();", StringComparison.Ordinal) < cloud.IndexOf("lifetime.Cancel();", StringComparison.Ordinal));
    }

    [Fact]
    public void AudioStallContinuesAndStopDrainsTail()
    {
        var audio = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "AudioCaptureService.cs"));
        Assert.Contains("短暂卡顿", audio);
        Assert.Contains("deadline.Elapsed < TimeSpan.FromSeconds(5)", audio);
        Assert.DoesNotContain("drain++ < 30", audio);
    }

    [Fact]
    public void ReleaseBuildHasNoUpdateLaunchBypass()
    {
        var dir = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop");
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            Assert.DoesNotContain("NEXTTHOUGHT_UPDATE_LAUNCH", File.ReadAllText(file));
    }

    [Fact]
    public void UiThreadExceptionsAreHandled()
    {
        var app = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "App.axaml.cs"));
        Assert.Contains("UnhandledException", app);
        Assert.Contains("e.Handled = true", app);
    }

    [Fact]
    public void BackgroundUpdateCheckMarshalsToUiThread()
    {
        var update = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Update.cs"));
        Assert.Contains("Dispatcher.UIThread.InvokeAsync", update);
    }

    [Fact]
    public void SpeakerVoicesCanBeMerged()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Speakers.cs"));
        Assert.Contains("MergeSpeakerVoices", state);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SpeakerEditorWindow.axaml"));
        Assert.Contains("MergePrevious_Click", xaml);
    }

    [Fact]
    public void SpeakerNamesPersistPerSession()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Speakers.cs"));
        Assert.Contains("speakers.json", state);
        Assert.Contains("LoadSpeakerNames", state);
    }

    [Fact]
    public void MaterialReviewSheetExists()
    {
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MaterialReviewWindow.axaml")));
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LibraryView.axaml"));
        Assert.Contains("Review_Click", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.MaterialReview.cs"));
        Assert.Contains("SaveMaterialReview", state);
    }

    [Fact]
    public void SlidePointsAreCachedAndShown()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Prepare.cs"));
        Assert.Contains("slideNotesCache", state);
        Assert.Contains("CurrentSlidePointsText", state);
        var panel = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "InsightsPanel.axaml"));
        Assert.Contains("讲稿要点", panel);
    }

    [Fact]
    public void SlidesCanFollowScreenshots()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Prepare.cs"));
        Assert.Contains("FollowSlideFromScreen", state);
        // E1: the capture tail (follow the slide) lives in the home state's screenshot analysis.
        var homeState = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Home.cs"));
        Assert.Contains("FollowSlideFromCapture", homeState);
    }

    [Fact]
    public void SlidesCanFollowSpeech()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Prepare.cs"));
        Assert.Contains("TryFollowSlide", state);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("TryFollowSlide", live);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "QuickSettingsPanel.axaml"));
        Assert.Contains("跟着讲话翻幻灯片", xaml);
    }

    [Fact]
    public void CoachSummaryIsBuiltAndShown()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Coach.cs"));
        Assert.Contains("CoachSummaryAccumulator", state);
        Assert.Contains("coach-summary.json", state);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml"));
        Assert.Contains("CoachSummaryText", xaml);
    }

    [Fact]
    public void ScreenshotTempFilesAreCleaned()
    {
        var shell = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.Shell.cs"));
        Assert.Contains("TryDeleteCapture", shell);
        Assert.Contains("File.Delete(path)", shell);
    }

    [Fact]
    public void ScreenshotOcrFeedsTheAnswerPrompt()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("ScreenContextText", live);
        Assert.Contains("screen: ScreenContextText", live);
        // E1: the screenshot analysis moved to AppState.Home.cs (home card / live capture tail).
        var homeState = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Home.cs"));
        Assert.Contains("SelectedPreview", homeState);
    }

    [Fact]
    public void ScreenshotCanBecomeAQuestion()
    {
        var homeState = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Home.cs"));
        Assert.Contains("PromptAboutScreenshot", homeState);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("PromptAboutScreenshot", live);
    }

    [Fact]
    public void SpeakerEditorExists()
    {
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SpeakerEditorWindow.axaml")));
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("Speakers_Click", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Speakers.cs"));
        Assert.Contains("ApplySpeakerNames", state);
    }

    [Fact]
    public void ForeignSpeechCanAutoTranslate()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("LooksForeign", live);
        Assert.Contains("AutoTranslateForeign", live);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "QuickSettingsPanel.axaml"));
        Assert.Contains("听到外语自动翻译", xaml);
    }

    [Fact]
    public void LiveCanLookUpTheDisplayedAnswer()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("LookUpDisplayedAnswerCommand", xaml);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("LookUpDisplayedAnswerAsync", live);
    }

    [Fact]
    public void SettingsCanSaveTavilyKey()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        Assert.Contains("TavilyKey", xaml);
        Assert.Contains("SaveTavily_Click", xaml);
        var store = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Platform", "SettingsStore.cs"));
        Assert.Contains("ReadTavilyKey", store);
    }

    [Fact]
    public void LiveTranscriptMarksHighlights()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("IsHighlighted", xaml);
        Assert.Contains("MeetingHighlights", xaml);
        var row = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "TranscriptRow.cs"));
        Assert.Contains("isHighlighted", row);
    }

    [Fact]
    public void LivePageHasMeetingNotesAndHighlights()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("MeetingNoteDraft", xaml);
        Assert.Contains("HighlightLine_Click", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.MeetingNotes.cs"));
        Assert.Contains("SessionUserContentStore.Save", state);
    }

    [Fact]
    public void InsightsPanelExists()
    {
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "InsightsPanel.axaml")));
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("Insights_Click", xaml);
        var panel = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "InsightsPanel.axaml"));
        Assert.Contains("幻灯片", panel);
        Assert.Contains("屏幕与截图", panel);
        Assert.Contains("回答进度", panel);
    }

    [Fact]
    public void LivePageShowsBilingualQuestionGloss()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("BilingualQuestionText", xaml);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("BilingualAnswer.Question", live);
    }

    [Fact]
    public void AnswerLanguageAndGlossAreWired()
    {
        var models = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Core", "Models.cs"));
        Assert.Contains("AnswerLanguage", models);
        Assert.Contains("AnswerGloss", models);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "QuickSettingsPanel.axaml"));
        Assert.Contains("{loc:Tr 回答语言}", xaml);
        // G8 moved the prompt builder next to the live request path.
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("Settings.AnswerGloss", state);
    }

    [Fact]
    public void SeriesEditorHasFieldsAndReview()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SeriesEditorWindow.axaml"));
        Assert.Contains("SeriesEditorFieldRows", xaml);
        Assert.Contains("SeriesEditorStageReview", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.SeriesEditor.cs"));
        Assert.Contains("StageReview", state);
    }

    [Fact]
    public void SeriesEditorCanArchiveAndDelete()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SeriesEditorWindow.axaml"));
        Assert.Contains("ArchiveSeriesCommand", xaml);
        Assert.Contains("DeleteSeries_Click", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.SeriesEditor.cs"));
        Assert.Contains("ArchiveSeries", state);
    }

    [Fact]
    public void SeriesEditorListsDocuments()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SeriesEditorWindow.axaml"));
        Assert.Contains("SeriesEditorDocuments", xaml);
        Assert.Contains("RemoveDocument_Click", xaml);
        Assert.Contains("ImportDocuments_Click", xaml);
        Assert.Contains("SeriesEditorPreparationRows", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.SeriesEditor.cs"));
        Assert.Contains("SeriesEditorDocuments", state);
    }

    [Fact]
    public void SeriesEditorWindowExists()
    {
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SeriesEditorWindow.axaml")));
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SeriesView.axaml"));
        Assert.Contains("编辑系列", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.SeriesEditor.cs"));
        Assert.Contains("SaveSeriesEditor", state);
    }

    [Fact]
    public void SeriesChangeCardsCanBeReviewed()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SeriesView.axaml"));
        Assert.Contains("SeriesChangeRows", xaml);
        Assert.Contains("AcceptSeriesChange_Click", xaml);
        var series = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Series.cs"));
        Assert.Contains("ReviewSeriesChange", series);
    }

    [Fact]
    public void SeriesMeetingRowsShowRecordTag()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SeriesView.axaml"));
        Assert.Contains("有纪要", xaml);
    }

    [Fact]
    public void GlossaryHasSeriesOverlay()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "GlossaryWindow.axaml"));
        Assert.Contains("系列术语", xaml);
        Assert.Contains("SeriesGlossaryRows", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Glossary.cs"));
        Assert.Contains("AddGlossaryToSeries", state);
    }

    [Fact]
    public void GlossaryMarksMisheardEntries()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "GlossaryWindow.axaml"));
        Assert.Contains("误听", xaml);
        var row = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "GlossaryEntryRow.cs"));
        Assert.Contains("MisheardBadge", row);
    }

    [Fact]
    public void GlossaryCanSearchAndAddFromTranscript()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "GlossaryWindow.axaml"));
        Assert.Contains("在转写中查找", xaml);
        Assert.Contains("GlossaryTranscriptHits", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Glossary.cs"));
        Assert.Contains("RefreshGlossaryTranscriptHits", state);
    }

    [Fact]
    public void GlossaryWindowExists()
    {
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "GlossaryWindow.axaml")));
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        Assert.Contains("打开术语表", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Glossary.cs"));
        Assert.Contains("GlossaryRows", state);
    }

    [Fact]
    public void LiveCanDetachTheAnswerWindow()
    {
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "DetachedAnswerWindow.axaml")));
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("DetachAnswer_Click", xaml);
        Assert.Contains("独立窗口", xaml);
    }

    [Fact]
    public void LivePageHasScreenshotCard()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("CurrentScreenshots", xaml);
        Assert.Contains("截图", xaml);
        var archive = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Archive.cs"));
        Assert.Contains("CurrentScreenshots.Add", archive);
    }

    [Fact]
    public void LiveCandidateShowsConfidence()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("CandidateConfidence", xaml);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("CandidateConfidence", live);
    }

    [Fact]
    public void LivePageHasAnswerHistoryPanel()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("CurrentAnswerHistory", xaml);
        Assert.Contains("历史回答", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("CurrentAnswerHistory.Add", state);
    }

    [Fact]
    public void LiveTopBarHasCompanionAndAnswerSettings()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("ToggleCompanionCommand", xaml);
        Assert.Contains("回答设置", xaml);
        Assert.Contains("UseSemanticRetrieval", xaml);
    }

    [Fact]
    public void LivePageHasNotesOnlyColumn()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        // E4: 自动回答 / 只记录 is a segmented switch; the 只记录 mode shows the notes column.
        Assert.Contains("{loc:Tr 只记录}", xaml);
        Assert.Contains("AnswerModeIsAuto", xaml);
        Assert.Contains("NotesPanel", xaml);
        Assert.Contains("{loc:Tr 我的笔记}", xaml);
    }

    [Fact]
    public void LivePageHasEvidenceAndWebSources()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        // E4: 联网状态与来源 + 证据行/详情 (Mac WebStatusLine/WebSources/EvidenceLine).
        Assert.Contains("LiveWebStatusLine", xaml);
        Assert.Contains("LiveWebSources", xaml);
        Assert.Contains("EvidenceSummary", xaml);
        Assert.Contains("EvidenceMaterialRows", xaml);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("WebResults.Clear", live);
        Assert.Contains("new EvidenceBundle", live);
    }

    [Fact]
    public void LivePageHasCandidateStrip()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("LiveCandidateText", xaml);
        Assert.Contains("回答这句", xaml);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("CandidateQuestion", live);
    }

    [Fact]
    public void LivePageHasTimerAndAnswerActions()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml"));
        Assert.Contains("RecordingElapsedText", xaml);
        Assert.Contains("LiveListeningStatus", xaml);
        foreach (var action in new[] { "重新生成", "更简短", "上一条", "下一条", "停止生成" })
            Assert.Contains(action, xaml);
    }

    [Fact]
    public void NotesEvidenceItemsCanBeEdited()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml"));
        Assert.Contains("EditEvidence_Click", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.NotesEditor.cs"));
        Assert.Contains("ApplyNotesEdit", state);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "EditTextDialog.axaml")));
    }

    [Fact]
    public void NotesEvidenceHasSupportBadges()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml"));
        Assert.Contains("NotesEvidence", xaml);
        Assert.Contains("Badge", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.StructuredNotes.cs"));
        Assert.Contains("BuildNotesEvidence", state);
    }

    [Fact]
    public void NotesShowsGenerationProgressLine()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml"));
        Assert.Contains("Binding Status", xaml);
        Assert.Contains("ProgressBar", xaml);
    }

    [Fact]
    public void NotesAnswersShowAutomaticBadgeAndRegenerate()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml"));
        Assert.Contains("自动", xaml);
        Assert.Contains("重新整理", xaml);
    }

    [Fact]
    public void NotesPageHasFourTabs()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml"));
        // E15: MeetingPage has exactly four tabs; the evidence pane and the outline view live inside 纪要.
        foreach (var header in new[] { "{loc:Tr 纪要}", "{loc:Tr 转写}", "{loc:Tr 回答}", "{loc:Tr 个人笔记}" })
            Assert.Contains(header, xaml);
        Assert.DoesNotContain("{loc:Tr 工作纪要}", xaml);
        Assert.DoesNotContain("{loc:Tr 提纲}", xaml);
        // 证据栏常驻纪要页右侧，不再是单独标签。
        Assert.Contains("NotesEvidence", xaml);
        Assert.Contains("JumpEvidence_Click", xaml);
        // 真提纲视图（不是把 Markdown 当提纲）。
        Assert.Contains("NotesOutlineRows", xaml);
        Assert.Contains("OutlineTime_Click", xaml);
        // 会议信息与重生成确认。
        Assert.Contains("Info_Click", xaml);
        Assert.Contains("RegenerateNotes_Click", xaml);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SessionInfoWindow.axaml")));
    }

    [Fact]
    public void NotesEditorExists()
    {
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "NotesEditorWindow.axaml")));
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.NotesEditor.cs"));
        Assert.Contains("SessionUserContentStore", state);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml"));
        Assert.Contains("编辑纪要", xaml);
    }

    [Fact]
    public void HistorySupportsMultiSelectDelete()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("BeginHistorySeriesSelectionCommand", xaml);
        Assert.Contains("DeleteSelectedSeriesCommand", xaml);
        Assert.Contains("SeriesCheckBox_Click", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Meeting.cs"));
        Assert.Contains("DeleteSelectedSeriesAsync", state);
        Assert.Contains("ToggleHistorySeriesSelection", state);
    }

    [Fact]
    public void DeletingASessionAsksForConfirmation()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("DeleteSession_Tapped", xaml);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "ConfirmDialog.axaml")));
    }

    [Fact]
    public void ArchivedScreenshotCanBeEnlarged()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("Screenshot_Click", xaml);
        var code = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml.cs"));
        Assert.Contains("Screenshot_Click", code);
    }

    [Fact]
    public void ArchiveScreenshotsArePreviewed()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("SelectedSessionScreenshots", xaml);
        var archive = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Archive.cs"));
        Assert.Contains("LoadSessionScreenshots", archive);
    }

    [Fact]
    public void ArchiveScreenshotsAreShownInHistory()
    {
        var archive = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Archive.cs"));
        Assert.Contains("ArchiveScreenshotAsync", archive);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("SelectedSessionScreenshots", xaml);
        Assert.Contains("HasSelectedSessionScreenshots", xaml);
    }


    [Fact]
    public void HistorySeriesMenuCanRenameAndDelete()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("编辑系列信息", xaml);
        Assert.Contains("删除系列…", xaml);
        Assert.Contains("DeleteSeries_Tapped", xaml);
        var series = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Series.cs"));
        Assert.Contains("RenameSeries(string seriesId", series);
        var meeting = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Meeting.cs"));
        Assert.Contains("DeleteSeriesWithMeetingsAsync", meeting);
    }

    [Fact]
    public void HistoryGroupHasSeriesMenu()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("打开系列", xaml);
        Assert.Contains("OpenSeries_Tapped", xaml);
        var series = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Series.cs"));
        Assert.Contains("OpenSeries(string? seriesId)", series);
    }

    [Fact]
    public void HistoryMeetingActionsIncludeOpenAndExport()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("打开纪要", xaml);
        Assert.Contains("导出 Markdown", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Archive.cs"));
        Assert.Contains("OpenSession(RecentMeetingRow row)", state);
        Assert.Contains("ExportSession(RecentMeetingRow row)", state);
    }

    [Fact]
    public void HistoryShowsArchiveOverviewAndAnswers()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("归档概览", xaml);
        Assert.Contains("HistoryAnswers", xaml);
        var archive = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Archive.cs"));
        Assert.Contains("SelectedSessionAnswers", archive);
        var meeting = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Meeting.cs"));
        Assert.Contains("SelectedSessionFacts", meeting);
    }

    [Fact]
    public void HistoryHasFullTextSearchUi()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("HistorySearchStatus", xaml);
        Assert.Contains("HistoryHitPosition", xaml);
        Assert.Contains("PreviousHistoryHitCommand", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Meeting.cs"));
        Assert.Contains("ScheduleHistoryContentSearch", state);
        Assert.Contains("SearchContentHits", state);
    }

    [Fact]
    public void HistoryIsGroupedBySeries()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("HistoryGroups", xaml);
        Assert.Contains("HistoryGroupRow", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.StructuredNotes.cs"));
        Assert.Contains("HistoryGroups.Add", state);
    }

    [Fact]
    public void HistoryHasFilteredBatchDelete()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("DeleteAll_Tapped", xaml);
        Assert.Contains("批量删除", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Deletion.cs"));
        Assert.Contains("DeleteAllMeetingsAsync", state);
        Assert.Contains("DeleteFilteredSessionsAsync", state);
    }

    [Fact]
    public void HistoryRowsOfferSessionRename()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("重命名…", xaml);
        Assert.Contains("RenameSession_Tapped", xaml);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "RenameSessionDialog.axaml")));
    }

    [Fact]
    public void HistoryRowsOfferSessionDeletion()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "HistoryView.axaml"));
        Assert.Contains("删除这场会议", xaml);
        Assert.Contains("DeleteSession_Tapped", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Deletion.cs"));
        Assert.Contains("Sessions.DeleteAsync", state);
        Assert.Contains("retainAcceptedContent", state);
    }

    [Fact]
    public void PrepareBuildsAMeetingPlan()
    {
        var prepare = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Prepare.cs"));
        Assert.Contains("ActivePlan", prepare);
        Assert.Contains("PreparedWorkspace", prepare);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("BeginPreparedMeeting(planSeries)", live);
    }

    [Fact]
    public void PrepareCheckCardsArePresent()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml"));
        Assert.Contains("采音", xaml);
        Assert.Contains("场地", xaml);
        Assert.Contains("回答服务", xaml);
        Assert.Contains("手机伴侣", xaml);
        Assert.Contains("资料与模型", xaml);
    }

    [Fact]
    public void PrepareHasStickyStartBar()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml"));
        Assert.Contains("<Grid RowDefinitions=\"*,Auto\">", xaml);
        Assert.Contains("Grid.Row=\"1\"", xaml);
        Assert.Contains("开始记录", xaml);
    }

    [Fact]
    public void PrepareMaterialsInsightIsShown()
    {
        var library = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "ViewModels", "LibraryViewModel.cs"));
        Assert.Contains("MaterialsInsight", library);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml"));
        Assert.Contains("Library.MaterialsInsight", xaml);
    }

    [Fact]
    public void PrepareSeriesBriefingIsExposed()
    {
        var series = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Series.cs"));
        Assert.Contains("SelectedSeriesBriefing", series);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml"));
        Assert.Contains("系列简报", xaml);
        Assert.Contains("SelectedSeriesBriefing.LatestSessions", xaml);
    }

    [Fact]
    public void PrepareTranscriptCarryAttachesPreviousMeeting()
    {
        var prepare = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Prepare.cs"));
        Assert.Contains("PreviousTranscriptContext", prepare);
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("PreparedDocuments()", live);
    }

    [Fact]
    public void PrepareCarrySwitchesGateMeetingContext()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("BeginPreparedMeeting", live);
        Assert.Contains("ActiveCarry?.Materials == false", live);
        var series = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Series.cs"));
        Assert.Contains("ActiveCarry?.Lessons == false", series);
        Assert.Contains("ActiveCarry?.Review == false", series);
    }

    [Fact]
    public void PrepareCarryPlanIsExposed()
    {
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Prepare.cs"));
        Assert.Contains("CarryPlan", state);
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml"));
        Assert.Contains("CarryMaterials", xaml);
        Assert.Contains("CarryTranscript", xaml);
    }

    [Fact]
    public void PreparePageIsHosted()
    {
        var shell = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "SelfTest", "Checks", "ShowSection.cs"));
        Assert.Contains("AppSection.Prepare => new PrepareView()", shell);
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "PrepareView.axaml")));
        // E1: the home page's prepare action now lives in the StartComposer subview.
        var composer = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "Home", "StartComposer.axaml"));
        Assert.Contains("Click=\"Prepare_Click\"", composer);
    }

    [Fact]
    public void QuickSettingsPanelExists()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "QuickSettingsPanel.axaml"));
        Assert.Contains("自动回答", xaml);
        Assert.Contains("提词宽度", xaml);
        Assert.Contains("全部设置", xaml);
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("QuickSettingsPanel", main);
    }

    [Fact]
    public void SceneDefaultsAlsoSetTemplateAndCue()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("sceneDefaults.NotesTemplate", live);
        Assert.Contains("sceneDefaults.CueStyle", live);
        Assert.Contains("SelectedTemplateId = TemplateKind.Raw()", live);
    }

    [Fact]
    public void SceneDefaultsAreAppliedAtMeetingStart()
    {
        var live = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.Live.cs"));
        Assert.Contains("SceneDefaultsFor(SelectedMode)", live);
        Assert.Contains("Settings.AutomaticAnswers = sceneDefaults.LiveMode", live);
        Assert.Contains("sceneDefaults.AnswerStyle == \"详细\"", live);
    }

    [Fact]
    public void SceneSettingsRowsAreWired()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        Assert.Contains("SceneSettingsRows", xaml);
        Assert.Contains("SceneOption_Click", xaml);
        var state = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.SceneSettings.cs"));
        Assert.Contains("LoadSceneSettings", state);
        Assert.Contains("SceneDefaults", state);
    }

    [Fact]
    public void SettingsCardsAreAssignedToSixPages()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        foreach (var page in new[] { "Scene", "Answer", "Coach", "Notes", "General", "Advanced" })
            Assert.Contains("Tag=\"" + page + "\"", xaml);
        var code = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        Assert.Contains("ShowPage", code);
        Assert.Contains("card.Tag as string == currentPage", code);
    }

    [Fact]
    public void SettingsRailHasSixGroups()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        foreach (var tag in new[] { "Scene", "Answer", "Coach", "Notes", "General", "Advanced" })
            Assert.Contains("Tag=\"" + tag + "\"", xaml);
        var code = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        Assert.Contains("SettingsPage_Click", code);
    }

    [Fact]
    public void SettingsIsAnOverlay()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("IsVisible=\"{Binding OnSettings}\"", main);
        Assert.Contains("<views:SettingsView", main);
        Assert.Contains("CommandParameter=\"Home\"", main);
    }

    [Fact]
    public void SidebarIsHiddenAndSettingsIsInTopBar()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("ColumnDefinitions=\"Auto,*\"", main);
        Assert.Contains("CommandParameter=\"Settings\"", main);
        Assert.Contains("IsVisible=\"False\"", main);
    }


    [Fact]
    public void TopNavigationHasHoverBrushTransition()
    {
        // D5: hover is the Mac Motion.hover token — 0.14 s, timing curve (0.2, 0.8, 0.2, 1).
        var controls = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));
        Assert.Contains("BrushTransition Property=\"Background\" Duration=\"0:0:0.14\"", controls);
        Assert.Contains("MotionHoverEasing", controls);
    }


    [Fact]
    public void CueIslandHasFiveStateEvidenceHook()
    {
        var code = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "CueIslandWindow.axaml.cs"));
        Assert.Contains("ApplyStateForCheck", code);
        Assert.Contains("\"collapsed\"", code);
        Assert.Contains("\"launcher\"", code);
        var runner = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "SelfTest", "RunScreenshotsAsync.cs"));
        Assert.Contains("island-\" + islandState", runner);
    }

    [Fact]
    public void TopNavigationCapsuleExists()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("GroupName=\"topnav\"", main);
        Assert.Contains("CommandParameter=\"Home\"", main);
        Assert.Contains("CommandParameter=\"Live\"", main);
        Assert.Contains("CommandParameter=\"Library\"", main);
        var controls = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));
        Assert.Contains("RadioButton.nav:checked", controls);
    }

    [Fact]
    public void ViewsDoNotUseTextCharactersForIcons()
    {
        var views = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views");
        foreach (var file in Directory.GetFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var symbol in new[] { "✕", "✓", "⚠" })
                Assert.DoesNotContain(symbol, text);
        }
        var tp = File.ReadAllText(Path.Combine(views, "TeleprompterWindow.axaml"));
        // D7: icons are resource references now, not literal glyph codepoints.
        Assert.Contains("{StaticResource IconXmark}", tp);
        Assert.Contains("{StaticResource IconFont}", tp);
    }

    [Fact]
    public void FontsAndLicensesAreBundled()
    {
        var dir = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Assets", "Fonts");
        foreach (var file in new[] { "InterVariable.ttf", "NotoSansSC-VF.ttf", "SourceSerif4-VF.ttf", "NotoSerifSC-VF.ttf",
                     "LICENSE-Inter.txt", "OFL-NotoSansSC.txt", "OFL-SourceSerif4.txt", "OFL-NotoSerifSC.txt" })
            Assert.True(File.Exists(Path.Combine(dir, file)), file);
        var theme = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml"));
        Assert.Contains("InterVariable.ttf#Inter Variable", theme);
        Assert.Contains("SourceSerif4-VF.ttf#Source Serif 4", theme);
    }

    [Fact]
    public void IconMapCoversEverySwiftSymbol()
    {
        var root = Root();
        var sw = Directory.GetFiles(Path.Combine(root, "Sources", "NextThought"), "*.swift", SearchOption.AllDirectories)
            .SelectMany(path => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), "systemName: \"([^\"]+)\"")
                .Select(match => match.Groups[1].Value))
            .Distinct().ToList();
        var icons = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Styles", "Icons.axaml"));
        var map = File.ReadAllText(Path.Combine(root, "Windows", "ICON_MAP.md"));
        Assert.True(sw.Count >= 50);
        foreach (var symbol in sw)
            Assert.Contains(symbol, map);
        // D7: the table also carries the scene-theme symbols and Windows-only icons, so it has at
        // least one entry per Swift symbol and every entry is a Fluent glyph.
        var entries = System.Text.RegularExpressions.Regex.Matches(icons, "<x:String x:Key=\"Icon[A-Za-z0-9]+\">&#x[0-9A-F]+;</x:String>");
        Assert.True(entries.Count >= sw.Count, $"icons: {entries.Count}, swift symbols: {sw.Count}");
        Assert.Contains("FluentSystemIcons-Regular.ttf", icons);
    }

    [Fact]
    public void SceneGlyphTagAndInkWashAreWiredIntoHome()
    {
        var root = Root();
        foreach (var name in new[] { "SceneGlyph.cs", "SceneTag.cs", "InkWash.cs" })
            Assert.True(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "Controls", name)));
        // E1: the glyph lives in the composer subview; the page keeps the scene ink wash.
        var composer = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "Home", "StartComposer.axaml"));
        Assert.Contains("<controls:SceneGlyph", composer);
        var home = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "HomeView.axaml"));
        Assert.Contains("<controls:InkWash", home);
    }

    [Fact]
    public void MotionTokensAndReduceMotionArePresent()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Motion.cs"));
        foreach (var token in new[] { "Hover", "Expand", "Phase", "Snappy", "Morph", "SystemParametersInfo", "FadeIn", "FadeOut" })
            Assert.Contains(token, text);
        var shell = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.Shell.cs"));
        Assert.Contains("Motion.FadeIn()", shell);
        Assert.Contains("Motion.FadeOut()", shell);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(160)", shell);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(120)", shell);
    }

    [Fact]
    public void AppStateMainFileIsBelow600Lines()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.cs");
        Assert.True(File.ReadAllLines(path).Length < 600);
        Assert.True(Directory.GetFiles(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop"), "AppState.*.cs").Length >= 15);
    }

    [Fact]
    public void ProductionStartupOptionsOnlyExposeThreeSwitches()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "StartupOptions.cs");
        var text = File.ReadAllText(path);
        Assert.Contains("Demo", text);
        Assert.Contains("Screenshots", text);
        Assert.Contains("SelfTest", text);
        Assert.DoesNotContain("--knowledge-check", text);
        Assert.True(File.ReadAllLines(path).Length < 40);
    }

    [Fact]
    public void MainWindowFileIsBelow400Lines()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs");
        Assert.True(File.ReadAllLines(path).Length < 400);
    }

    [Fact]
    public void SelfChecksLiveOutsideTheWindowFile()
    {
        var root = Root();
        Assert.True(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTest", "Checks", "GlobalUsings.cs")));
        var window = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs"));
        Assert.DoesNotContain("private async Task RunAcceptanceCheckAsync", window);
        Assert.True(Directory.GetFiles(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTest", "Checks"), "*.cs").Length > 40);
    }
}
