using Avalonia.Controls;

namespace NextThought.Desktop.Views.Home;

/// <summary>
/// J3: 首页的「用了多少」统计 (Mac MeetingStatsViews.swift HomeStats): this month or all time —
/// meetings, recording time, questions heard and answers given. The numbers come from
/// <see cref="AppState"/> through <see cref="NextThought.Core.MeetingStats"/>.
/// </summary>
public partial class HomeStats : UserControl
{
    public HomeStats() => InitializeComponent();
}
