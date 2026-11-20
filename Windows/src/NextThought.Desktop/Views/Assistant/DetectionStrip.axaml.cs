using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views.Assistant;

/// <summary>E5: the question the detector is following, with its confidence bar (Mac DetectionStrip/ConfidenceBar).</summary>
public partial class DetectionStrip : UserControl
{
    public DetectionStrip() => InitializeComponent();

    private void Answer_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.AnswerLiveCandidate();
}
