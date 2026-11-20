using Avalonia.Controls;
using Avalonia.Interactivity;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E3: the first-run guide overlay (macOS Onboarding.swift). The page content and state live in
/// AppState.Onboarding.cs; this code-behind only opens the sound test window.
/// </summary>
public partial class OnboardingView : UserControl
{
    public OnboardingView() => InitializeComponent();

    private async void VoiceTest_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state) return;
            if (TopLevel.GetTopLevel(this) is not Window owner) return;
            await new OnboardingVoiceWindow { DataContext = state }.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true);
        }
    }

    private void SceneChip_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SceneChoice choice } || DataContext is not AppState state) return;
        if (state.SetSceneCommand.CanExecute(choice.Mode)) state.SetSceneCommand.Execute(choice.Mode);
    }
}
