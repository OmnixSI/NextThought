using System.Globalization;

namespace NextThought.Core;

/// <summary>
/// E1: the short texts about a recorded meeting shown on the home page and in the meeting list
/// (port of <c>MeetingOutcome</c> in HomeView.swift). The improve/action counts come from the Mac
/// meta fields; when an archive predates them the line falls back to the scene and duration.
/// </summary>
public static class MeetingOutcome
{
    /// <summary>"3 个待办" / "2 道题待改进" / "面试 · 52 分钟" (HomeView.swift MeetingOutcome.line).</summary>
    public static string Line(SessionMeta meta, bool summarizing = false)
    {
        if (summarizing) return UiText.T("正在整理纪要");
        if (meta.SummaryState is "failed" or "pending") return UiText.T("纪要没有整理好 · 可以重试");
        if (Number(meta, "improveCount") is { } improve && improve > 0)
            return UiText.Format("%lld 道题待改进", (long)improve);
        if (Number(meta, "actionItemCount") is { } actions && actions > 0)
            return UiText.Format("%lld 个待办", (long)actions);
        var type = TypeName(meta);
        var minutes = (int)((meta.EndedAt ?? meta.StartedAt).Subtract(meta.StartedAt).TotalMinutes);
        return minutes > 0 ? UiText.Format("%@ · %lld 分钟", type, minutes) : type;
    }

    /// <summary>
    /// "今天 10:00" / "昨天 10:00" / "周四" / "10月2日" (HomeView.swift MeetingOutcome.when).
    /// B24: the time zone is an explicit input; null keeps the machine's local zone as before.
    /// </summary>
    public static string When(DateTimeOffset date, DateTimeOffset? now = null, bool? english = null,
        TimeZoneInfo? timeZone = null)
    {
        var isEnglish = english ?? UiText.EnglishUi;
        var zone = timeZone ?? LocalTime.Zone;
        var local = TimeZoneInfo.ConvertTime(date, zone);
        var today = TimeZoneInfo.ConvertTime(now ?? DateTimeOffset.Now, zone).Date;
        if (local.Date == today) return UiText.FormatTemplate(UiText.Lookup("今天 %@", isEnglish), ShortTime(local, isEnglish));
        if (local.Date == today.AddDays(-1)) return UiText.FormatTemplate(UiText.Lookup("昨天 %@", isEnglish), ShortTime(local, isEnglish));
        if (today.AddDays(-6) < local.Date) return Weekday(local, isEnglish);
        return isEnglish
            ? local.ToString("MMM d", CultureInfo.InvariantCulture)
            : $"{local.Month}月{local.Day}日";
    }

    /// <summary>The meeting's own type label: custom name when the Mac meta carries one, else the scene.</summary>
    public static string TypeName(SessionMeta meta)
    {
        if (meta.AdditionalProperties is { } extra
            && extra.TryGetValue("typeName", out var typeName)
            && typeName.ValueKind == System.Text.Json.JsonValueKind.String
            && typeName.GetString() is { Length: > 0 } custom)
            return UiText.T(custom);
        return UiText.T(SceneTitle(meta.Mode));
    }

    /// <summary>The Mac scene display names (SessionMode.displayName).</summary>
    public static string SceneTitle(string mode) => mode switch
    {
        "interview" => "面试",
        "labMeeting" => "组会",
        "academic" => "汇报 / 答辩",
        "groupProject" => "小组作业",
        "client" => "客户沟通",
        "meeting" => "工作会议",
        "lesson" => "讲座 / 课程",
        "discussion" => "小组讨论",
        "custom" => "自定义",
        _ => "通用"
    };

    private static string ShortTime(DateTimeOffset value, bool english) => english
        ? value.ToString("h:mm tt", CultureInfo.InvariantCulture)
        : (value.Hour < 12 ? "上午" : "下午") + value.ToString("h:mm", CultureInfo.InvariantCulture);

    private static string Weekday(DateTimeOffset value, bool english)
    {
        if (english) return value.ToString("dddd", CultureInfo.InvariantCulture);
        return value.DayOfWeek switch
        {
            DayOfWeek.Monday => "周一",
            DayOfWeek.Tuesday => "周二",
            DayOfWeek.Wednesday => "周三",
            DayOfWeek.Thursday => "周四",
            DayOfWeek.Friday => "周五",
            DayOfWeek.Saturday => "周六",
            _ => "周日"
        };
    }

    private static double? Number(SessionMeta meta, string field)
    {
        if (meta.AdditionalProperties is not { } extra || !extra.TryGetValue(field, out var value)) return null;
        return value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetDouble(out var number) ? number : null;
    }
}
