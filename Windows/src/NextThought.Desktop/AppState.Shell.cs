using CommunityToolkit.Mvvm.Input;

namespace NextThought.Desktop;

/// <summary>
/// E4 / §6.7 shell extras (ContentView.swift ErrorBanner / LevelMeters / FocusModeView):
/// the banner dismisses the error status; the meters read the same audio levels as the live page.
/// </summary>
public partial class AppState
{
    [RelayCommand]
    private void CloseStatus()
    {
        Status = "";
        StatusIsError = false;
    }

    /// <summary>LevelMeters reads the live audio levels (0–1); Windows has no echo-monitor flag.</summary>
    public double ShellSystemLevel => SystemAudioLevelPercent;

    public double ShellMicrophoneLevel => MicrophoneAudioLevelPercent;
}
