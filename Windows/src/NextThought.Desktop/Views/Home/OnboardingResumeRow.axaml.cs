using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views.Home;

/// <summary>
/// E1: the first-run resume row (HomeView.swift OnboardingResumeRow). It appears after the guide
/// was deferred; the Windows guide does not store that step yet, so AppState keeps it hidden.
/// </summary>
public partial class OnboardingResumeRow : UserControl
{
    public OnboardingResumeRow() => InitializeComponent();

    private void Resume_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.OpenDeferredOnboarding();

    private void Dismiss_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.DismissOnboardingResume();
}
