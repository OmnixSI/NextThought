using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views.Home;

/// <summary>E1: the recent-meetings list on the home page (HomeView.swift RecentMeetings).</summary>
public partial class RecentMeetings : UserControl
{
    public RecentMeetings() => InitializeComponent();

    private void Meeting_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HomeRecentRow row } || DataContext is not AppState state) return;
        state.OpenRecentMeeting(row);
    }
}
