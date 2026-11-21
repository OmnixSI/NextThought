using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// §6.5/E4: LiveView's top-bar 会中窗口 menu (LiveWindowController.swift: LiveWindowMenu).
/// One small glass icon button opens an inline picker for the three window modes and shows the
/// shortcut that cycles them; the mode itself is AppState.LiveMode.
/// </summary>
public partial class AppState
{
    /// <summary>Mac ShortcutKeys action "live" (切换会中窗口).</summary>
    public string LiveWindowShortcut => "Ctrl+Alt+Shift+S";

    /// <summary>"按 Ctrl+Alt+Shift+S 在三者之间切换".</summary>
    public string ToggleLiveWindowHint => UiText.Format("按 %@ 在三者之间切换", LiveWindowShortcut);

    public bool LiveModeIsNotes => LiveMode == LiveWindowMode.Notes;

    public bool LiveModeIsSubtitles => LiveMode == LiveWindowMode.Subtitles;

    public bool LiveModeIsHidden => LiveMode == LiveWindowMode.Hidden;

    /// <summary>LiveWindowController.set(mode): pick one of the three modes from the menu.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SetLiveWindowMode(string? mode)
    {
        LiveMode = mode switch
        {
            "subtitles" => LiveWindowMode.Subtitles,
            "hidden" => LiveWindowMode.Hidden,
            _ => LiveWindowMode.Notes
        };
        OnPropertyChanged(nameof(LiveModeIsNotes));
        OnPropertyChanged(nameof(LiveModeIsSubtitles));
        OnPropertyChanged(nameof(LiveModeIsHidden));
    }
}
