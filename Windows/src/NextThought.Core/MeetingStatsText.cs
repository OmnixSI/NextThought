namespace NextThought.Core;

/// <summary>
/// J3: the wording around <see cref="MeetingStats"/> (port of the MeetingStats extension in
/// MeetingStatsViews.swift): the duration line and the one-line series summary.
/// </summary>
public static class MeetingStatsText
{
    /// <summary>"5 小时 12 分", "42 分钟", "不到 1 分钟" or "0 分钟" (Mac durationText).</summary>
    public static string Duration(MeetingStats stats)
    {
        var (hours, minutes) = stats.HoursAndMinutes;
        if (hours > 0) return UiText.Format("%lld 小时 %lld 分", hours, minutes);
        if (minutes > 0) return UiText.Format("%lld 分钟", minutes);
        return stats.Meetings > 0 && stats.Seconds > 0 ? UiText.T("不到 1 分钟") : UiText.T("0 分钟");
    }

    /// <summary>
    /// One line under a series' name (Mac SeriesStatsLine): "共 3 场 · 2 小时 5 分 · 提问 8 次 · 回答 6 次";
    /// empty when the series has no meetings.
    /// </summary>
    public static string SeriesLine(MeetingStats stats) => stats.Meetings > 0
        ? UiText.Format("共 %lld 场 · %@ · 提问 %lld 次 · 回答 %lld 次",
            stats.Meetings, Duration(stats), stats.Questions, stats.Answers)
        : "";
}
