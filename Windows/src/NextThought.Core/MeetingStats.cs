namespace NextThought.Core;

/// <summary>
/// J3/J9: how much was recorded — the port of MeetingStats.swift (3.4.0). The time is recording
/// time from the start of a meeting to its end; a meeting without an end (still running or
/// interrupted) counts as a meeting but adds no time.
/// </summary>
public sealed record MeetingStats(int Meetings = 0, double Seconds = 0, int Questions = 0, int Answers = 0)
{
    /// <summary>
    /// The meetings that started inside <paramref name="interval"/> (all of them without one),
    /// optionally of one series only. The interval includes the start and excludes the end.
    /// </summary>
    public static MeetingStats Of(IEnumerable<SessionMeta> metas,
        (DateTimeOffset Start, DateTimeOffset EndExclusive)? interval = null, Guid? series = null)
    {
        var stats = new MeetingStats();
        foreach (var meta in metas)
        {
            if (interval is { } window && (meta.StartedAt < window.Start || meta.StartedAt >= window.EndExclusive)) continue;
            if (series is { } wanted && !MatchesSeries(meta, wanted)) continue;
            stats = stats with
            {
                Meetings = stats.Meetings + 1,
                Seconds = stats.Seconds + Duration(meta),
                Questions = stats.Questions + meta.QuestionCount,
                Answers = stats.Answers + meta.AnswerCount
            };
        }
        return stats;
    }

    /// <summary>The calendar month that holds <paramref name="date"/> (Mac MeetingStats.month).</summary>
    public static (DateTimeOffset Start, DateTimeOffset EndExclusive) Month(DateTimeOffset date, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        var local = TimeZoneInfo.ConvertTime(date, zone);
        var startLocal = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(startLocal, zone.GetUtcOffset(startLocal));
        var endLocal = startLocal.AddMonths(1);
        var end = new DateTimeOffset(endLocal, zone.GetUtcOffset(endLocal));
        return (start, end);
    }

    /// <summary>Mac durationText: "5 小时 12 分", "42 分钟", "不到 1 分钟" or "0 分钟".</summary>
    public string DurationText => MeetingStatsText.Duration(this);

    /// <summary>Whole hours and minutes: (5, 12) for 5 h 12 min 40 s.</summary>
    public (int Hours, int Minutes) HoursAndMinutes
    {
        get
        {
            var minutes = (int)Math.Floor(Seconds / 60);
            return (minutes / 60, minutes % 60);
        }
    }

    /// <summary>Mac SessionMeta.duration: max(0, endedAt ?? startedAt - startedAt).</summary>
    private static double Duration(SessionMeta meta) =>
        Math.Max(0, ((meta.EndedAt ?? meta.StartedAt) - meta.StartedAt).TotalSeconds);

    private static bool MatchesSeries(SessionMeta meta, Guid series) =>
        Guid.TryParse(meta.WorkspaceId, out var id) && id == series;
}
