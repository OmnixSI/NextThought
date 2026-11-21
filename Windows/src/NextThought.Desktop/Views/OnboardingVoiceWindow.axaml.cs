using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class OnboardingVoiceWindow : Window
{
    public OnboardingVoiceWindow() => InitializeComponent();

    private async void MyVoice_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        await new MyVoiceWindow { DataContext = state }.ShowDialog(this);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
