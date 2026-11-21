using Avalonia;
using Avalonia.Media;

namespace NextThought.Desktop;

/// <summary>
/// §3.3 insights 3: the Mac AnswerProgressStrip metric pill turns green when every point is spoken.
/// </summary>
public partial class AppState
{
    public bool AnswerProgressIsDone => AnswerProgressTotal > 0 && AnswerProgressCoveredCount >= AnswerProgressTotal;

    public IBrush AnswerProgressBrush =>
        AnswerProgressIsDone && Application.Current?.TryFindResource("Ready", out var ready) == true && ready is IBrush brush
            ? brush
            : Application.Current?.TryFindResource("Secondary", out var secondary) == true && secondary is IBrush secondaryBrush
                ? secondaryBrush
                : Brushes.Gray;
}
