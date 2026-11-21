using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>E23: Mac DocumentReadingSettings — one choice for how documents are read.</summary>
public partial class DocumentReadingSettings : UserControl
{
    public DocumentReadingSettings() => InitializeComponent();

    /// <summary>Mac DocumentReadingSettings 先用系统识别.</summary>
    private void UseSystemDocumentReader_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        state.Settings.SelectedDocumentProvider =
            state.Settings.DocumentProviderChoices.First(choice => choice.Value == "system");
    }

    /// <summary>E23: the Mac first-run guide's DocumentChoiceSummary, opened from the settings card.</summary>
    private void ShowChoiceSummary_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        var summary = new DocumentChoiceSummary { DataContext = state };
        if (TopLevel.GetTopLevel(this) is Window owner) summary.Show(owner); else summary.Show();
    }
}
