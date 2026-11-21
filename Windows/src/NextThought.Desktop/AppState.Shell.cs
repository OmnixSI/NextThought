using CommunityToolkit.Mvvm.Input;

namespace NextThought.Desktop;

/// <summary>
/// E4 / §6.7 shell extras (ContentView.swift ErrorBanner / LevelMeters / FocusModeView):
/// the banner dismisses the error status; the meters read the same audio levels as the live page.
/// </summary>
public partial class AppState
{
    /// <summary>decisions §3.1 shell 1: the error banner also carries the archive-save queue state.</summary>
    public bool ShowErrorBanner => StatusIsError || SaveIsBlocked || forcedSaveBlocked;
    public string ErrorBannerText => forcedSaveBlocked || SaveIsBlocked
        ? (SaveStatusText.Length > 0 ? SaveStatusText : UiText.Format("保存受阻 · 有 %lld 项未保存 · 重试", forcedSaveBlocked ? 3 : Math.Max(1, SaveUnqueuedCount)))
        : Status;
    public bool ShowRetrySave => forcedSaveBlocked || SaveIsBlocked;
    public bool ShowAcknowledgeLoss => !forcedSaveBlocked && SaveIsBlocked && CanAcknowledgeSaveLoss;
    private bool forcedSaveBlocked;

    /// <summary>Evidence hook: draw the blocked banner without a real failed write.</summary>
    public void SetSaveBlockedForCheck(bool value)
    {
        forcedSaveBlocked = value;
        RaiseBannerUi();
    }

    private void RaiseBannerUi()
    {
        OnPropertyChanged(nameof(ShowErrorBanner));
        OnPropertyChanged(nameof(ErrorBannerText));
    }

    public void SaveStatusChanged() => RaiseBannerUi();

    [RelayCommand]
    private void CloseStatus()
    {
        if (SaveIsBlocked && CanAcknowledgeSaveLoss) AcknowledgeSaveLossCommand.Execute(null);
        Status = "";
        StatusIsError = false;
        RaiseBannerUi();
    }

    partial void OnStatusChanged(string value) => RaiseBannerUi();
    partial void OnStatusIsErrorChanged(bool value) => RaiseBannerUi();

    /// <summary>LevelMeters reads the live audio levels (0–1); Windows has no echo-monitor flag.</summary>
    public double ShellSystemLevel => SystemAudioLevelPercent;

    public double ShellMicrophoneLevel => MicrophoneAudioLevelPercent;
}
