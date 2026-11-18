using System.Globalization;
using System.Text.RegularExpressions;

namespace NextThought.Core;

/// <summary>
/// Relative dates ("周四", "明天", "next Monday") worked out from the meeting date.
/// Ported from NotesDates (NotesTemplates.swift).
/// </summary>
public static partial class NotesDates
{
    private static readonly Dictionary<char, int> ChineseDigits = new()
    {
        ['一'] = 1, ['二'] = 2, ['三'] = 3, ['四'] = 4, ['五'] = 5, ['六'] = 6, ['日'] = 7, ['天'] = 7, ['七'] = 7
    };

    private static readonly Dictionary<string, int> EnglishDays = new()
    {
        ["monday"] = 1, ["tuesday"] = 2, ["wednesday"] = 3, ["thursday"] = 4, ["friday"] = 5, ["saturday"] = 6, ["sunday"] = 7,
        ["mon"] = 1, ["tue"] = 2, ["tues"] = 2, ["wed"] = 3, ["thu"] = 4, ["thur"] = 4, ["thurs"] = 4, ["fri"] = 5, ["sat"] = 6, ["sun"] = 7
    };

    /// <summary>
    /// The concrete date a phrase means, counted from <paramref name="reference"/>; null when the
    /// phrase is not a date or is too vague ("下周", "尽快").
    /// </summary>
    public static DateTime? Resolve(string phrase, DateTimeOffset reference)
    {
        var text = phrase.ToLowerInvariant().Replace(" ", "");
        var day = reference.Date;
        DateTime Add(int days) => day.AddDays(days);

        if (text.Contains("大后天")) return Add(3);
        if (text.Contains("后天") || text.Contains("dayaftertomorrow")) return Add(2);
        if (text.Contains("明天") || text.Contains("明日") || text.Contains("tomorrow")) return Add(1);
        if (text.Contains("今天") || text.Contains("今日") || text.Contains("今晚") || text.Contains("today") || text.Contains("tonight") || text.Contains("eod"))
            return Add(0);
        if (text.Contains("月底") || text.Contains("endofthemonth") || text.Contains("endofmonth"))
        {
            var first = new DateTime(day.Year, day.Month, 1);
            return first.AddDays(DateTime.DaysInMonth(day.Year, day.Month) - 1);
        }
        // "10月30日" / "10 月 30 号"
        if (MonthDay().Match(text) is { Success: true } match)
        {
            var numbers = Regex.Split(match.Value, @"\D+").Where(part => part.Length > 0).Select(int.Parse).ToList();
            if (numbers.Count == 2)
            {
                if (numbers[0] is >= 1 and <= 12 && numbers[1] is >= 1 and <= 31)
                {
                    var year = day.Year;
                    if (numbers[1] <= DateTime.DaysInMonth(year, numbers[0]))
                    {
                        var date = new DateTime(year, numbers[0], numbers[1]);
                        // A date long before the meeting most likely means next year.
                        if (date < day && (day - date).TotalDays > 180) return date.AddYears(1);
                        return date;
                    }
                }
            }
        }
        // Weekdays: 周四 / 星期四 / 礼拜四 / next thursday / friday
        int? weekday = null;
        if (ChineseWeekday().Match(text) is { Success: true } chinese && chinese.Value.Length > 0)
            weekday = ChineseDigits.GetValueOrDefault(chinese.Value[^1]);
        else
        {
            foreach (var (name, value) in EnglishDays.OrderByDescending(pair => pair.Key.Length))
            {
                if (Regex.IsMatch(text, "\\b" + name + "\\b") || (name.Length > 4 && text.Contains(name)))
                {
                    weekday = value;
                    break;
                }
            }
        }
        if (weekday is not { } target) return null;
        // Monday-based weekday of the meeting (DateTime.DayOfWeek has Sunday = 0).
        var current = ((int)day.DayOfWeek + 6) % 7 + 1;
        var nextWeek = text.Contains("下周") || text.Contains("下星期") || text.Contains("下礼拜") || text.Contains("nextweek") || text.StartsWith("next");
        var thisWeek = text.Contains("本周") || text.Contains("这周") || text.Contains("这个星期") || text.Contains("thisweek") || text.StartsWith("this");
        if (nextWeek) return Add(7 - current + target);
        if (thisWeek) return Add(target - current);
        // A bare weekday is the next one to come (today counts).
        return Add((target - current + 7) % 7);
    }

    /// <summary>"10 月 30 日（周四）" / "Thu, Oct 30".</summary>
    public static string Format(DateTime date, bool english = false)
    {
        if (english)
        {
            var days = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            return $"{days[(int)date.DayOfWeek]}, {months[date.Month - 1]} {date.Day}";
        }
        var weekdays = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        return string.Format(CultureInfo.InvariantCulture, "{0} 月 {1} 日（{2}）", date.Month, date.Day, weekdays[(int)date.DayOfWeek]);
    }

    /// <summary>A due text that sounds like a time but could not be pinned down ("下周", "月中").</summary>
    public static bool LooksLikeDate(string text)
    {
        var lower = text.ToLowerInvariant();
        return new[] { "周", "星期", "月", "号", "日", "天", "week", "month", "day" }.Any(lower.Contains);
    }

    [GeneratedRegex(@"(\d{1,2})月(\d{1,2})(日|号)")]
    private static partial Regex MonthDay();

    [GeneratedRegex(@"(周|星期|礼拜)[一二三四五六日天]")]
    private static partial Regex ChineseWeekday();
}
