using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>
/// E8: the meeting's personal notes (MeetingUserNotes.swift) — one draft owner, the save state,
/// and the 重点 highlights merged into paragraphs with their times; clicking one jumps back to the
/// transcript line it came from.
/// </summary>
public partial class MeetingUserNotes : UserControl
{
    public MeetingUserNotes()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => (DataContext as AppState)?.FlushMeetingNotes();
    }

    private void Highlight_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MeetingHighlightRow row } || DataContext is not AppState state) return;
        state.OpenMeetingHighlightCommand.Execute(row);
    }
}
