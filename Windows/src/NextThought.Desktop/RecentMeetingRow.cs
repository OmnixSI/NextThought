using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>A row in the recent-meetings list: a saved meeting plus its compact summary line.</summary>
public sealed record SeriesRow(string Id, string Name, int Count, int LessonCount, string LastTitle, string LastLesson)
{
    public string Meta => $"{Count} 场会议 · {LessonCount} 条经验";
    public string Preview => LastLesson.Length > 0 ? LastLesson : LastTitle;
}

public sealed record RecentMeetingRow(SessionMeta Meta)
{
    public string Title => string.IsNullOrWhiteSpace(Meta.Title) ? "未命名会议" : Meta.Title;

    public string Subtitle
    {
        get
        {
            var parts = new List<string> { Meta.LocalStartedAt };
            if (Meta.TranscriptCount > 0) parts.Add($"{Meta.TranscriptCount} 条转写");
            if (Meta.AnswerCount > 0) parts.Add($"{Meta.AnswerCount} 条回答");
            if (Meta.SummaryState is "saved" or "complete") parts.Add("有纪要");
            return string.Join(" · ", parts);
        }
    }

    public bool HasSummary => Meta.SummaryState is "saved" or "complete";
}
