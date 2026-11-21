using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>One applied correction on a transcript line (Mac <c>TranscriptCorrection</c> as a menu action).</summary>
public sealed record CorrectedTranscriptFix(string Id, string ActionLabel, bool Reverted);

/// <summary>E17: one row of the corrected transcript (Mac <c>SessionProjectedTranscriptLine</c>).</summary>
public sealed record CorrectedTranscriptRow(
    Guid LineId,
    string Time,
    string Speaker,
    string Text,
    IReadOnlyList<CorrectedTranscriptFix> Fixes)
{
    public bool HasFixes => Fixes.Count > 0;
}

/// <summary>
/// E17: 校正原话 — the transcript with the proofreading corrections shown and one-click undo
/// (Mac NotesPageView.swift <c>CorrectedTranscript</c>, opened from HistoryView's「校正原话」).
/// </summary>
public partial class CorrectedTranscript : Window
{
    public CorrectedTranscript() => InitializeComponent();

    /// <summary>Opens (or re-focuses) the window for the meeting the app currently shows.</summary>
    public static void ShowFor(AppState state)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var existing = desktop.Windows.OfType<CorrectedTranscript>().FirstOrDefault();
            if (existing is not null)
            {
                state.RefreshCorrectedTranscript();
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }
        }
        state.RefreshCorrectedTranscript();
        var window = new CorrectedTranscript { DataContext = state };
        window.Show();
    }

    private void ToggleFix_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CorrectedTranscriptFix fix } && DataContext is AppState state)
            state.ToggleCorrectedTranscriptFixCommand.Execute(fix);
    }

    private void ClearFind_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.CorrectedTranscriptFind = "";
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
