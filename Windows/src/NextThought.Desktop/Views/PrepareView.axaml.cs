using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class PrepareView : UserControl
{
    public PrepareView() => InitializeComponent();

    private void PrepareScene_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SceneChoice scene } && DataContext is AppState state)
            state.SetSceneCommand.Execute(scene.Mode);
    }
}
