using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class EditTextDialog : Window
{
    public EditTextDialog() => InitializeComponent();

    public EditTextDialog(string text) : this() => TextEditor.Text = text;

    private void Save_Click(object? sender, RoutedEventArgs e) => Close(TextEditor.Text);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
