using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views.Home;

/// <summary>
/// E1: "接着上一场" (HomeView.swift ContinueSeries). Rows come from AppState.HomeSeriesRows:
/// latest meeting first, standing rows excluded, at most three.
/// </summary>
public partial class ContinueSeries : UserControl
{
    public ContinueSeries() => InitializeComponent();

    private void Series_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HomeSeriesRow row } || DataContext is not AppState state) return;
        state.PrepareNextMeetingInSeries(row.Id);
    }
}
