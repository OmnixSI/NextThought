using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using NextThought.Desktop.Controls;

namespace NextThought.Desktop;

/// <summary>
/// E3: the first-run guide (macOS Onboarding.swift). Four short steps, each one decision: the
/// scene, the sound, an answer service (optional), and the cue island plus the first meeting.
/// The view is Views/OnboardingView.axaml; this file holds its state.
/// </summary>
public partial class AppState
{
    [ObservableProperty] private bool onboardingVisible;
    [ObservableProperty] private int onboardingStep;
    [ObservableProperty] private bool onboardingShowSample;

    public NextThought.Core.OnboardingStep GuideStep => OnboardingSteps.Clamped(OnboardingStep);

    public IReadOnlyList<OnboardingCheckRow> OnboardingChecks =>
    [
        new(UiText.T("声音与识别"), ModelReady ? UiText.T("已就绪") : UiText.T("未准备"), ModelStatusText, ModelReady),
        new(UiText.T("回答服务"), Settings.HasApiKey ? UiText.T("已保存密钥") : UiText.T("可选，未配置"), Settings.KeyStatus, Settings.HasApiKey),
        new(UiText.T("资料库"), Library.Materials.Count > 0 ? UiText.Format("已导入 %lld 份", Library.Materials.Count) : UiText.T("可选，还没有资料"), UiText.T("准备后可在会中引用"), Library.Materials.Count > 0)
    ];

    public string OnboardingStepTitle => OnboardingStepTitleOf(GuideStep);

    public string OnboardingStepBody => GuideStep switch
    {
        NextThought.Core.OnboardingStep.Scene => "首页的场景会决定默认的会中模式、回答风格、纪要模板与提词方式。随时可以改。",
        NextThought.Core.OnboardingStep.Sound => "选择系统输出混音或麦克风，并在设置里准备本机语音模型。",
        NextThought.Core.OnboardingStep.Answers => "接入兼容 Chat Completions 的回答服务；只记录与转写时可以不填。",
        _ => "点「开始记录」就能开会；也可以先到准备页补资料。"
    };

    public string OnboardingPosition => $"{GuideStep.Position()} / {OnboardingSteps.Count}";

    public bool OnboardingIsFirst => GuideStep == NextThought.Core.OnboardingStep.Scene;
    public bool OnboardingIsScene => GuideStep == NextThought.Core.OnboardingStep.Scene;
    public bool OnboardingIsSound => GuideStep == NextThought.Core.OnboardingStep.Sound;
    public bool OnboardingIsAnswers => GuideStep == NextThought.Core.OnboardingStep.Answers;
    public bool OnboardingIsStart => GuideStep == NextThought.Core.OnboardingStep.Start;
    public bool OnboardingIsLast => GuideStep.IsLast();
    public bool OnboardingCanSkipAnswers => GuideStep == NextThought.Core.OnboardingStep.Answers && !PrepareAnswerReady;

    public string OnboardingStepTime => GuideStep.EstimatedSeconds() < 60
        ? UiText.Format("约 %lld 秒", GuideStep.EstimatedSeconds())
        : UiText.Format("约 %lld 分钟", GuideStep.EstimatedSeconds() / 60);

    /// <summary>The scene sheet's serif promise and bullet moments (Mac sceneSheet).</summary>
    public string OnboardingSceneTitle => SelectedScene?.Title ?? UiText.T("通用");
    public string OnboardingSceneSubtitle => SelectedScene?.Subtitle ?? "";
    public string OnboardingScenePromise => SelectedScene?.Promise ?? "";
    public IReadOnlyList<string> OnboardingSceneMoments => SelectedScene?.Moments ?? [];

    public bool OnboardingCanStartFirstMeeting =>
        IsIdle && !Answering && (ModelReady || Settings.SelectedCloudStt is { Value.Length: > 0 } || StartupOptions.Demo);

    public string OnboardingStartHeadline => UiText.Format("第一场按“%@”开始。", OnboardingSceneTitle);

    public string OnboardingReadinessMessage => ModelReady
        ? UiText.T("语音已就绪。可以开始记录，或先导入 WAV 验证。")
        : UiText.T("先到设置准备语音组件；未连接回答服务时也可以只记录。");

    public string OnboardingSampleToggle => OnboardingShowSample ? UiText.T("收起示例") : UiText.T("看一段面试示例");
    public string OnboardingSampleToggleHint => OnboardingShowSample ? UiText.T("收起面试示例") : UiText.T("看一段面试示例");
    public string OnboardingVoiceStatus => MyVoiceHasProfile ? MyVoiceStatus : UiText.T("还没有录入");

    partial void OnOnboardingStepChanged(int value) => RaiseOnboardingStep();

    partial void OnOnboardingShowSampleChanged(bool value)
    {
        OnPropertyChanged(nameof(OnboardingSampleToggle));
        OnPropertyChanged(nameof(OnboardingSampleToggleHint));
        if (value) PlaySample();
        else StopSample();
    }

    private void RaiseOnboardingStep()
    {
        OnPropertyChanged(nameof(GuideStep));
        OnPropertyChanged(nameof(OnboardingStepTitle));
        OnPropertyChanged(nameof(OnboardingStepBody));
        OnPropertyChanged(nameof(OnboardingPosition));
        OnPropertyChanged(nameof(OnboardingIsFirst));
        OnPropertyChanged(nameof(OnboardingIsScene));
        OnPropertyChanged(nameof(OnboardingIsSound));
        OnPropertyChanged(nameof(OnboardingIsAnswers));
        OnPropertyChanged(nameof(OnboardingIsStart));
        OnPropertyChanged(nameof(OnboardingIsLast));
        OnPropertyChanged(nameof(OnboardingCanSkipAnswers));
        OnPropertyChanged(nameof(OnboardingStepTime));
        OnPropertyChanged(nameof(OnboardingSceneTitle));
        OnPropertyChanged(nameof(OnboardingSceneSubtitle));
        OnPropertyChanged(nameof(OnboardingScenePromise));
        OnPropertyChanged(nameof(OnboardingSceneMoments));
        OnPropertyChanged(nameof(OnboardingCanStartFirstMeeting));
        OnPropertyChanged(nameof(OnboardingStartHeadline));
        OnPropertyChanged(nameof(OnboardingReadinessMessage));
        OnPropertyChanged(nameof(OnboardingChecks));
        RaiseOnboardingLayout();
    }

    private void LoadOnboarding()
    {
        try
        {
            var completed = SettingsStore.Load().OnboardingCompletedVersion;
            var version = typeof(AppState).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            OnboardingVisible = StartupOptions.Onboarding
                || (!StartupOptions.Demo && OnboardingVersion.NeedsGuide(version, completed));
        }
        catch { OnboardingVisible = StartupOptions.Onboarding; }
    }

    [RelayCommand]
    private void NextOnboardingStep()
    {
        if (GuideStep.Next() is { } next) OnboardingStep = (int)next;
        else CompleteOnboarding();
    }

    [RelayCommand]
    private void PreviousOnboardingStep()
    {
        if (GuideStep.Previous() is { } previous) OnboardingStep = (int)previous;
    }

    [RelayCommand]
    private void GoToOnboardingStep(string step)
    {
        if (Enum.TryParse<NextThought.Core.OnboardingStep>(step, ignoreCase: true, out var parsed)) OnboardingStep = (int)parsed;
    }

    [RelayCommand]
    private void ToggleOnboardingSample() => OnboardingShowSample = !OnboardingShowSample;

    /// <summary>Mac "看一场示例会议": finish the guide and open the demo shell.</summary>
    [RelayCommand]
    private void OpenOnboardingSample()
    {
        CompleteOnboarding();
        LoadDemo();
        Section = AppSection.Home;
    }

    /// <summary>Mac "稍后继续": put the guide aside; the home page keeps a resume row.</summary>
    [RelayCommand]
    private void SkipOnboarding() => DeferOnboarding();

    /// <summary>Mac "开始第一场": finish the guide and start recording with the chosen scene.</summary>
    [RelayCommand]
    private void BeginFirstMeeting()
    {
        CompleteOnboarding();
        if (StartRecordingCommand.CanExecute(null)) StartRecordingCommand.Execute(null);
    }

    [RelayCommand]
    private void CompleteOnboarding()
    {
        OnboardingVisible = false;
        try
        {
            var settings = SettingsStore.Load();
            settings.OnboardingCompletedVersion = OnboardingVersion.GuideVersion(typeof(AppState).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
            settings.OnboardingDeferredVersion = "";
            settings.OnboardingDismissedVersion = "";
            _ = SettingsStore.SaveAsync(settings);
        }
        catch { }
        RaiseOnboardingResume();
        SetStatus(UiText.T("引导已完成；之后可在设置中重新查看。"));
    }

    private static string OnboardingStepTitleOf(NextThought.Core.OnboardingStep step) => step switch
    {
        NextThought.Core.OnboardingStep.Scene => UiText.T("场合"),
        NextThought.Core.OnboardingStep.Sound => UiText.T("声音"),
        NextThought.Core.OnboardingStep.Answers => UiText.T("回答"),
        _ => UiText.T("提词与开始")
    };
}

// =================================================================================================
// E3 (§6.7 / Onboarding.swift): the four guide pages' bindings. Everything here is read-only or a
// single settings write so the page can be drawn exactly like the Mac screenshots, without asking
// for a permission, downloading or calling a model.
// =================================================================================================

public partial class AppState
{
    private IBrush OnboardingInkBrush => Application.Current?.FindResource("InkBrush") as IBrush ?? Brushes.Black;
    private IBrush OnboardingHairlineBrush => Application.Current?.FindResource("Hairline") as IBrush ?? Brushes.Gray;

    /// <summary>Mac header: four 22×2 capsules — done secondary, current ink, upcoming hairline.</summary>
    public IReadOnlyList<OnboardingProgressCapsule> OnboardingProgressCapsules
    {
        get
        {
            var current = (int)GuideStep;
            return Enumerable.Range(0, NextThought.Core.OnboardingSteps.Count).Select(index => new OnboardingProgressCapsule(
                index < current ? SecondaryBrush : index == current ? OnboardingInkBrush : OnboardingHairlineBrush)).ToArray();
        }
    }

    /// <summary>Mac sceneSheet: the same chips as the home composer, with the picked scene's pigment.</summary>
    public IReadOnlyList<PrepareSceneChip> OnboardingSceneChips
    {
        get
        {
            var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            var current = SelectedMode;
            return Scenes.Select(choice => new PrepareSceneChip(
                choice,
                choice.Mode == current,
                TintAt13(choice.Mode, dark),
                TintBrush(choice.Mode, dark),
                SecondaryBrush)).ToArray();
        }
    }

    /// <summary>Mac sceneSheet moments: a scene-tint dot and the caption, one under the other.</summary>
    public IReadOnlyList<PrepareMomentRow> OnboardingSceneMomentRows
    {
        get
        {
            var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            var tint = TintBrush(SelectedMode, dark);
            return SelectedScene?.Moments.Select(moment => new PrepareMomentRow(moment, tint)).ToArray() ?? [];
        }
    }

    public IBrush OnboardingSceneTintBrush => TintBrush(SelectedMode, Application.Current?.ActualThemeVariant == ThemeVariant.Dark);

    // ---- 2 · sound ---------------------------------------------------------------------------

    /// <summary>Mac MeetingVenue.allCases selection in the guide (0 线上 / 1 线下).</summary>
    public int OnboardingVenueIndex
    {
        get => Settings.CaptureSystem ? 0 : 1;
        set
        {
            // The two-way binding writes the same value back after every layout raise; only a real
            // change may save settings (otherwise the notification loops through SaveAsync).
            if (value is < 0 or > 1 || value == OnboardingVenueIndex) return;
            Settings.CaptureSystem = value == 0;
            if (value == 1) Settings.CaptureMicrophone = true;
            _ = Settings.SaveAsync();
            RaiseOnboardingLayout();
        }
    }

    public string OnboardingVenueDetail => Settings.CaptureSystem
        ? UiText.T("线上：听电脑里会议软件播放的声音。")
        : UiText.T("线下：只用麦克风，声音标为“现场”。");

    /// <summary>The two venue radio buttons (Mac InkSegmented items).</summary>
    public bool OnboardingOnline => Settings.CaptureSystem;
    public bool OnboardingOffline => !Settings.CaptureSystem;

    /// <summary>
    /// Windows has no macOS screen-recording permission: the system output is captured without a
    /// permission prompt. The Mac row's wording is kept, with the platform fact made explicit.
    /// </summary>
    public string OnboardingSystemAudioDetail => UiText.T("无需授权 · 能听到会议里对方的声音。");
    public StatusTone OnboardingSystemAudioTone => StatusTone.Ready;

    /// <summary>Mac microphone row: 记录我的声音 / 只记录对方 (online only).</summary>
    public int OnboardingMicrophoneIndex
    {
        get => Settings.CaptureMicrophone ? 0 : 1;
        set
        {
            if (value is < 0 or > 1 || value == OnboardingMicrophoneIndex) return;
            Settings.CaptureMicrophone = value == 0;
            _ = Settings.SaveAsync();
            RaiseOnboardingLayout();
        }
    }

    public bool OnboardingShowsMicrophoneChoice => Settings.CaptureSystem;

    /// <summary>Mac microphone row's two choices (online only).</summary>
    public IReadOnlyList<string> OnboardingMicrophoneOptions =>
        [UiText.T("记录我的声音"), UiText.T("只记录对方")];

    /// <summary>The two microphone radio buttons (Mac InkSegmented items).</summary>
    public bool OnboardingMicrophoneRecordsMe => Settings.CaptureMicrophone;
    public bool OnboardingMicrophoneOnlyOther => !Settings.CaptureMicrophone;

    public string OnboardingMicrophoneDetail
    {
        get
        {
            if (!Settings.CaptureMicrophone) return UiText.T("不使用麦克风，也不会询问权限。会议里只记录对方的声音。");
            return MicrophonePermission switch
            {
                WindowsPermissions.PermissionState.Allowed => Settings.CaptureSystem
                    ? UiText.T("已允许 · 你说的话会标为“我”。")
                    : UiText.T("已允许 · 现场的声音会标为“现场”。"),
                WindowsPermissions.PermissionState.Denied => Settings.CaptureSystem
                    ? UiText.T("未允许 · 请在 Windows 设置里打开麦克风权限，或选“只记录对方”。")
                    : UiText.T("未允许 · 线下记录需要麦克风，请在 Windows 设置里打开。"),
                _ => Settings.CaptureSystem
                    ? UiText.T("第一次开始记录时才会询问，用于识别你自己说的话。")
                    : UiText.T("线下会议靠它听现场的声音，开始时会询问一次。")
            };
        }
    }

    public StatusTone OnboardingMicrophoneTone
    {
        get
        {
            if (!Settings.CaptureMicrophone) return StatusTone.Idle;
            return MicrophonePermission switch
            {
                WindowsPermissions.PermissionState.Allowed => StatusTone.Ready,
                WindowsPermissions.PermissionState.Denied => StatusTone.Caution,
                _ => StatusTone.Idle
            };
        }
    }

    public bool OnboardingShowsMicrophonePermissionAction => Settings.CaptureMicrophone && !MicrophonePermissionGranted;

    public string OnboardingMicrophoneActionLabel => MicrophonePermission == WindowsPermissions.PermissionState.Denied
        ? UiText.T("打开设置")
        : UiText.T("允许");

    /// <summary>Mac recognition row (X-ASR 中英模型 on this machine, offline).</summary>
    public string OnboardingRecognitionDetail => OnboardingRecognitionReady
        ? UiText.T("X-ASR 中英模型 · 在本机离线识别，已就绪。")
        : UiText.T("把声音转成文字。需要下载本机识别模型，约 270 MB，只需一次。");

    /// <summary>The demo shows the Mac screenshots' ready state (Mac DemoMode ships the model).</summary>
    private bool OnboardingRecognitionReady => ModelReady || StartupOptions.Demo;

    public StatusTone OnboardingRecognitionTone => OnboardingRecognitionReady ? StatusTone.Ready : StatusTone.Caution;
    public bool OnboardingRecognitionNeedsDownload => !OnboardingRecognitionReady;
    public string OnboardingRecognitionDownloadLabel => UiText.T("下载");

    // ---- disclosure rows (Mac DisclosureGroup: a quiet chevron line, no box) -------------------

    [ObservableProperty] private bool onboardingShowRecognitionOptions;
    [ObservableProperty] private bool onboardingShowAnswerOptions;

    [RelayCommand]
    private void ToggleRecognitionOptions() => OnboardingShowRecognitionOptions = !OnboardingShowRecognitionOptions;

    [RelayCommand]
    private void ToggleAnswerOptions() => OnboardingShowAnswerOptions = !OnboardingShowAnswerOptions;

    /// <summary>Mac recordingDataRouteSummary, with 这台 Mac → 这台 Windows.</summary>
    public string OnboardingDataRouteSummary
    {
        get
        {
            var audio = ModelReady || StartupOptions.Demo
                ? UiText.T("声音在这台 Windows 上识别。应用不保存原始录音。")
                : UiText.T("声音在本机识别。应用不保存原始录音。");
            return PrepareAnswerReady
                ? audio + "\n" + UiText.T("使用 AI 时，转写、相关资料片段或你选择的截图会发送给所选回答服务。")
                : audio;
        }
    }

    // ---- 3 · answers -------------------------------------------------------------------------

    /// <summary>Mac AnswerEngineForm's provider list, in the Mac order.</summary>
    public IReadOnlyList<string> OnboardingAnswerProviders =>
    [
        "DeepSeek", UiText.T("通义千问"), "Moonshot Kimi", UiText.T("智谱 GLM"), UiText.T("火山方舟 豆包"),
        "OpenAI", "OpenRouter", "Grok", UiText.T("本机 Ollama"), UiText.T("本机 LM Studio"), UiText.T("其他")
    ];

    public string OnboardingAnswerServiceTitle => StartupOptions.Demo
        ? UiText.T("Codex 已登录 (ChatGPT 账号)")
        : PrepareAnswerReady
            ? Settings.UseCodex ? UiText.T("Codex 已登录 (ChatGPT 账号)") : UiText.T("已连接回答服务")
            : UiText.T("还没有连接回答服务");

    public string OnboardingAnswerServiceDetail => StartupOptions.Demo
        ? "Logged in using ChatGPT · codex-cli 0.160.0"
        : PrepareAnswerReady
            ? (Settings.UseCodex ? "Codex" : "API") + " · " + (Settings.Model.Length > 0 ? Settings.Model : UiText.T("未指定模型"))
            : UiText.T("选一个服务商，粘贴 Key，点一下检查。");

    public StatusTone OnboardingAnswerTone => PrepareAnswerReady ? StatusTone.Ready : StatusTone.Caution;

    public string OnboardingModelLine => StartupOptions.Demo
        ? "GPT-5.5"
        : PrepareAnswerReady
            ? (Settings.Model.Length > 0 ? Settings.Model : UiText.T("未指定模型"))
            : UiText.T("未选择");

    public string OnboardingTestAnswerLabel => UiText.T("测试回答");
    public string OnboardingTestAnswerHint => UiText.T("发一句测试问题，确认回答服务能正常回答。");

    // ---- 4 · cue island and first meeting -----------------------------------------------------

    public string OnboardingCueIslandQuestion => UiText.T("能介绍一下你在这个项目里负责的部分吗？");
    public string OnboardingCueIslandAnswer => UiText.T("我负责订单服务的缓存层。");
    public string OnboardingCuePoint1 => UiText.T("规模：日均 2 亿次读取，P99 降到 18 ms");
    public string OnboardingCuePoint2 => UiText.T("我的角色：方案设计和灰度上线由我主导");

    /// <summary>Mac rememberGuideScene: the scene picked in the guide is the first meeting's scene.</summary>
    public void RefreshGuideScene()
    {
        OnPropertyChanged(nameof(OnboardingSceneTitle));
        OnPropertyChanged(nameof(OnboardingSceneSubtitle));
        OnPropertyChanged(nameof(OnboardingScenePromise));
        OnPropertyChanged(nameof(OnboardingSceneMoments));
        OnPropertyChanged(nameof(OnboardingStartHeadline));
        RaiseOnboardingLayout();
    }

    private void RaiseOnboardingLayout()
    {
        OnPropertyChanged(nameof(OnboardingProgressCapsules));
        OnPropertyChanged(nameof(OnboardingSceneChips));
        OnPropertyChanged(nameof(OnboardingSceneMomentRows));
        OnPropertyChanged(nameof(OnboardingSceneTintBrush));
        OnPropertyChanged(nameof(OnboardingVenueIndex));
        OnPropertyChanged(nameof(OnboardingVenueDetail));
        OnPropertyChanged(nameof(OnboardingOnline));
        OnPropertyChanged(nameof(OnboardingOffline));
        OnPropertyChanged(nameof(OnboardingSystemAudioDetail));
        OnPropertyChanged(nameof(OnboardingSystemAudioTone));
        OnPropertyChanged(nameof(OnboardingMicrophoneIndex));
        OnPropertyChanged(nameof(OnboardingMicrophoneOptions));
        OnPropertyChanged(nameof(OnboardingMicrophoneRecordsMe));
        OnPropertyChanged(nameof(OnboardingMicrophoneOnlyOther));
        OnPropertyChanged(nameof(OnboardingShowsMicrophoneChoice));
        OnPropertyChanged(nameof(OnboardingMicrophoneDetail));
        OnPropertyChanged(nameof(OnboardingMicrophoneTone));
        OnPropertyChanged(nameof(OnboardingShowsMicrophonePermissionAction));
        OnPropertyChanged(nameof(OnboardingMicrophoneActionLabel));
        OnPropertyChanged(nameof(OnboardingRecognitionDetail));
        OnPropertyChanged(nameof(OnboardingRecognitionTone));
        OnPropertyChanged(nameof(OnboardingRecognitionNeedsDownload));
        OnPropertyChanged(nameof(OnboardingDataRouteSummary));
        OnPropertyChanged(nameof(OnboardingAnswerServiceTitle));
        OnPropertyChanged(nameof(OnboardingAnswerServiceDetail));
        OnPropertyChanged(nameof(OnboardingAnswerTone));
        OnPropertyChanged(nameof(OnboardingModelLine));
    }
}

/// <summary>E3: one progress capsule of the guide header (Mac header ForEach).</summary>
public sealed record OnboardingProgressCapsule(IBrush Background);
