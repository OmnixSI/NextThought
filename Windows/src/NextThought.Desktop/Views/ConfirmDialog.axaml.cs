using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog() => InitializeComponent();

    public ConfirmDialog(string message) : this() => MessageText.Text = message;

    /// <summary>Mac alert labels: e.g. 继续编辑 / 放弃更改 instead of the generic buttons.</summary>
    public ConfirmDialog(string message, string confirmLabel, string cancelLabel) : this(message)
    {
        ConfirmButton.Content = confirmLabel;
        CancelButton.Content = cancelLabel;
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
