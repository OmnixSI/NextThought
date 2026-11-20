using CommunityToolkit.Mvvm.ComponentModel;

namespace NextThought.Desktop;

/// <summary>
/// E26b: the quick-settings presentation keys (Mac cueSurface / presenterSlideNotes) that change
/// what the floating cue shows without reopening the settings page.
/// </summary>
public partial class AppState
{
    /// <summary>True while PresenterMode keeps the meeting on the camera display's island.</summary>
    [ObservableProperty] private bool presenterLayoutActive;

    partial void OnPresenterLayoutActiveChanged(bool value) => OnPropertyChanged(nameof(HasCurrentSlidePoints));

    /// <summary>The quick panel toggled 讲的时候显示这一页的讲稿; the island follows immediately.</summary>
    public void NotifyPresenterSlideNotesChanged() => OnPropertyChanged(nameof(HasCurrentSlidePoints));
}
