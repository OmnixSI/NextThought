using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>E12/Mac MyVoiceSettings: summary, record again, delete with confirmation; setup panel.</summary>
public partial class MyVoiceWindow : Window
{
    public MyVoiceWindow() => InitializeComponent();

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state || TopLevel.GetTopLevel(this) is not Window owner) return;
        var dialog = new ConfirmDialog(UiText.T("删除后，会议里不会再自动认出你；也可以随时重新录入。"));
        if (await dialog.ShowDialog<bool>(owner) is false) return;
        state.DeleteMyVoiceCommand.Execute(null);
        Close();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}