using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class MaterialReviewWindow : Window
{
    public MaterialReviewWindow() => InitializeComponent();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
