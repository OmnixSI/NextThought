using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NextThought.Desktop;

/// <summary>
/// E13 / §6.7: 共享可见性测试 (ContentView.swift ShareTestSheet + AppState+Live.runShareVisibilityTest).
/// The sheet shows the whole screen as other participants would see it (this app's own floating
/// windows included), so the user can check whether the cue island is visible while sharing.
/// </summary>
public partial class AppState
{
    [ObservableProperty] private Bitmap? shareTestImage;

    public bool HasShareTestImage => ShareTestImage is not null;

    partial void OnShareTestImageChanged(Bitmap? value) => OnPropertyChanged(nameof(HasShareTestImage));

    /// <summary>Mac captureDisplay(display.id, hidingAppWindows: false): full screen, own windows included.</summary>
    public Task<bool> RunShareVisibilityTestAsync()
    {
        try
        {
            ShareTestImage = ScreenCapture.VirtualScreen().Image;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            SetStatus(UiText.T("操作失败：") + ex.Message, true);
            return Task.FromResult(false);
        }
    }

    public void ClearShareTestImage() => ShareTestImage = null;
}
