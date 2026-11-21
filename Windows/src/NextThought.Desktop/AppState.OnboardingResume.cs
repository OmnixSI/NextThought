using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E1/E3: the first-run guide can be put aside ("稍后继续") and resumed from the home page,
/// and a recovered interrupted meeting leaves a quiet notice (Mac AppState+Lifecycle /
/// Onboarding.swift deferOnboarding/openOnboarding and AppState.interruptedNotice).
/// </summary>
public partial class AppState
{
    private static string CurrentGuideVersion =>
        OnboardingVersion.GuideVersion(typeof(AppState).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

    /// <summary>Mac OnboardingResumeRow: visible while the guide was deferred for this version.</summary>
    public bool OnboardingResumeVisible
    {
        get
        {
            try
            {
                var settings = SettingsStore.Load();
                var version = CurrentGuideVersion;
                return settings.OnboardingDeferredVersion == version
                       && settings.OnboardingCompletedVersion != version
                       && settings.OnboardingDismissedVersion != version
                       && !OnboardingVisible;
            }
            catch { return false; }
        }
    }

    /// <summary>Mac line: 继续完成设置, naming what is still open (声音 / 回答服务).</summary>
    public string OnboardingResumeLine
    {
        get
        {
            var open = OnboardingPendings.Open(ModelReady, Settings.HasApiKey);
            if (open.Count == 0) return UiText.T("继续完成设置");
            var names = string.Join(" / ", open.Select(area => area switch
            {
                OnboardingPending.Sound => UiText.T("声音"),
                _ => UiText.T("回答服务")
            }));
            return UiText.Format("继续完成设置（还差%@）", names);
        }
    }

    /// <summary>Mac deferOnboarding: remember the version and step, close the guide without completing.</summary>
    [RelayCommand]
    private void DeferOnboarding()
    {
        try
        {
            var settings = SettingsStore.Load();
            settings.OnboardingDeferredVersion = CurrentGuideVersion;
            settings.OnboardingDeferredStep = OnboardingStep;
            _ = SettingsStore.SaveAsync(settings);
        }
        catch { }
        OnboardingVisible = false;
        Section = AppSection.Home;
        RaiseOnboardingResume();
        SetStatus(UiText.T("引导已稍后继续；首页会留一个入口。"));
    }

    /// <summary>Mac openOnboarding: back to the page the guide stopped on.</summary>
    public void OpenDeferredOnboarding()
    {
        try
        {
            var settings = SettingsStore.Load();
            OnboardingStep = Math.Clamp(settings.OnboardingDeferredStep, 0, OnboardingSteps.Count - 1);
        }
        catch { OnboardingStep = 0; }
        OnboardingVisible = true;
        RaiseOnboardingResume();
    }

    /// <summary>Mac OnboardingResumeRow xmark: do not ask again for this version.</summary>
    public void DismissOnboardingResume()
    {
        try
        {
            var settings = SettingsStore.Load();
            settings.OnboardingDismissedVersion = CurrentGuideVersion;
            _ = SettingsStore.SaveAsync(settings);
        }
        catch { }
        RaiseOnboardingResume();
    }

    private void RaiseOnboardingResume()
    {
        OnPropertyChanged(nameof(OnboardingResumeVisible));
        OnPropertyChanged(nameof(OnboardingResumeLine));
        OnPropertyChanged(nameof(HasInterruptedNotice));
        OnPropertyChanged(nameof(InterruptedNotice));
    }

    // ---- interrupted meeting notice (Mac state.interruptedNotice) ----------------------

    [ObservableProperty] private string interruptedNotice = "";

    public bool HasInterruptedNotice => InterruptedNotice.Length > 0;

    /// <summary>Mac AppState.interruptedNotice text set after Sessions.recoverInterrupted.</summary>
    public void SetInterruptedNotice(int recoveredCount)
    {
        if (recoveredCount <= 0) return;
        InterruptedNotice = UiText.T("上次会议被中断，已保存到所有记录。");
        OnPropertyChanged(nameof(HasInterruptedNotice));
    }

    /// <summary>Mac 查看: open the archive and clear the notice.</summary>
    [RelayCommand]
    private void ReviewInterrupted()
    {
        DismissInterruptedNotice();
        Section = AppSection.History;
    }

    /// <summary>Mac 知道了.</summary>
    [RelayCommand]
    private void DismissInterruptedNotice()
    {
        InterruptedNotice = "";
        OnPropertyChanged(nameof(HasInterruptedNotice));
    }
}
