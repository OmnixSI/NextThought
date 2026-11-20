using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class RenameSessionDialog : Window
{
    public RenameSessionDialog() => InitializeComponent();

    public RenameSessionDialog(string title) : this() => TitleBox.Text = title;

    private void Save_Click(object? sender, RoutedEventArgs e) => Close(TitleBox.Text?.Trim());

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
