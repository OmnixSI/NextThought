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
    private AppState? observed;
    private bool animating;

    public OnboardingView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Observe();
        Loaded += (_, _) =>
        {
            Observe();
            _ = AnimatePageAsync();
        };
    }

    private void Observe()
    {
        if (observed is { } previous) previous.PropertyChanged -= OnStateChanged;
        observed = DataContext as AppState;
        if (observed is { } state) state.PropertyChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.OnboardingStep)) _ = AnimatePageAsync();
    }

    /// <summary>Mac goto: withAnimation(Motion.phase) around the step change (0.46 s settle).</summary>
    private async Task AnimatePageAsync()
    {
        if (animating || this.FindControl<Grid>("GuidePageHost") is not { } host) return;
        animating = true;
        try
        {
            await GuideMotion.PhaseIn().RunAsync(host);
        }
        catch
        {
            host.Opacity = 1;
        }
        finally
        {
            animating = false;
        }
    }

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
