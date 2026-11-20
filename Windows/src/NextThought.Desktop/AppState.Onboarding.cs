using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

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
    public bool OnboardingCanSkipAnswers => GuideStep == NextThought.Core.OnboardingStep.Answers;

    public string OnboardingStepTime => GuideStep.EstimatedSeconds() < 60
        ? UiText.Format("约 %lld 秒", GuideStep.EstimatedSeconds())
        : UiText.Format("约 %lld 分钟", GuideStep.EstimatedSeconds() / 60);

    /// <summary>The scene sheet's serif promise and bullet moments (Mac sceneSheet).</summary>
    public string OnboardingSceneTitle => SelectedScene?.Title ?? UiText.T("通用");
    public string OnboardingSceneSubtitle => SelectedScene?.Subtitle ?? "";
    public string OnboardingScenePromise => SelectedScene?.Promise ?? "";
    public IReadOnlyList<string> OnboardingSceneMoments => SelectedScene?.Moments ?? [];

    public bool OnboardingCanStartFirstMeeting =>
        IsIdle && !Answering && (ModelReady || Settings.SelectedCloudStt is { Value.Length: > 0 });

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
