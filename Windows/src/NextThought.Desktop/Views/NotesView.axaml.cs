using Avalonia.Controls;

namespace NextThought.Desktop.Views;

public partial class NotesView : UserControl
{
    public NotesView()
    {
        InitializeComponent();
        Fishbone.Jump += seconds => (DataContext as AppState)?.JumpToTranscript(seconds);
        Viewpoints.Jump += seconds => (DataContext as AppState)?.JumpToTranscript(seconds);
    }
}
