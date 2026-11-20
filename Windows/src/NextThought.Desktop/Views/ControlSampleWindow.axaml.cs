using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NextThought.Desktop.Views;

public partial class ControlSampleWindow : Window
{
    public ControlSampleWindow() => AvaloniaXamlLoader.Load(this);
}
