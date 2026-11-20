using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

public partial class AppState
{
    [ObservableProperty] private bool onboardingVisible;
    [ObservableProperty] private int onboardingStep;

    public string OnboardingStepTitle => OnboardingSteps.Clamped(OnboardingStep) switch
    {
        NextThought.Core.OnboardingStep.Scene => "先选场合",
        NextThought.Core.OnboardingStep.Sound => "准备声音",
        NextThought.Core.OnboardingStep.Answers => "回答服务（可选）",
        _ => "开始第一场"
    };

    public string OnboardingStepBody => OnboardingSteps.Clamped(OnboardingStep) switch
    {
        NextThought.Core.OnboardingStep.Scene => "首页的场景会决定默认的会中模式、回答风格、纪要模板与提词方式。随时可以改。",
        NextThought.Core.OnboardingStep.Sound => "选择系统输出混音或麦克风，并在设置里准备本机语音模型。",
        NextThought.Core.OnboardingStep.Answers => "接入兼容 Chat Completions 的回答服务；只记录与转写时可以不填。",
        _ => "点「开始记录」就能开会；也可以先到准备页补资料。"
    };

    public string OnboardingPosition => $"{OnboardingSteps.Clamped(OnboardingStep).Position()} / {OnboardingSteps.Count}";
    public bool OnboardingIsFirst => OnboardingStep <= 0;
    public bool OnboardingIsLast => OnboardingSteps.Clamped(OnboardingStep).IsLast();

    partial void OnOnboardingStepChanged(int value)
    {
        OnPropertyChanged(nameof(OnboardingStepTitle));
        OnPropertyChanged(nameof(OnboardingStepBody));
        OnPropertyChanged(nameof(OnboardingPosition));
        OnPropertyChanged(nameof(OnboardingIsFirst));
        OnPropertyChanged(nameof(OnboardingIsLast));
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
        if (OnboardingSteps.Clamped(OnboardingStep).Next() is { } next) OnboardingStep = (int)next;
        else CompleteOnboarding();
    }

    [RelayCommand]
    private void PreviousOnboardingStep()
    {
        if (OnboardingSteps.Clamped(OnboardingStep).Previous() is { } previous) OnboardingStep = (int)previous;
    }

    [RelayCommand]
    private void SkipOnboarding() => CompleteOnboarding();

    [RelayCommand]
    private void CompleteOnboarding()
    {
        OnboardingVisible = false;
        try
        {
            var settings = SettingsStore.Load();
            settings.OnboardingCompletedVersion = OnboardingVersion.GuideVersion(typeof(AppState).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
            _ = SettingsStore.SaveAsync(settings);
        }
        catch { }
        SetStatus("引导已完成；之后可在设置中重新查看。");
    }
}
