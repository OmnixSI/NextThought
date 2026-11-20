using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class SeriesEditorWindow : Window
{
    public SeriesEditorWindow() => InitializeComponent();


    private void RemoveDocument_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NextThought.Core.ContextDocument document } && DataContext is AppState state)
            state.RemoveSeriesDocumentCommand.Execute(document);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
