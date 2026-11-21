using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// J1: translated from Tests/NextThoughtCoreTests/LiveNotesTests.swift (Mac v3.4.0, f015e8c6).
/// When live notes are due, how a reply becomes notes while settled topics stay put, when a
/// reply that cannot be read is refused, and the stats/features that ship with the same file.
/// </summary>
public sealed class LiveNotesTests
{
    private static List<LiveNotesPrompt.Line> Lines(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new LiveNotesPrompt.Line(
                index * 20, index % 2 == 0 ? "老师" : "我", $"第 {index} 句的内容"))
            .ToList();

    [Fact]
    public void TheFirstNotesComeEarlyAndLaterOnesOnTheInterval()
    {
        var notes = new LiveNotes();
        Assert.False(LiveNotesSchedule.IsDue(notes, 0, 100));
        Assert.True(LiveNotesSchedule.IsDue(notes, 3, 1));
        Assert.False(LiveNotesSchedule.IsDue(notes, 2, 5));
        Assert.True(LiveNotesSchedule.IsDue(notes, 1, 21));
        notes.Summary = "讨论上线计划";
        notes.Covered = 3;
        Assert.False(LiveNotesSchedule.IsDue(notes, 3, 300));
        Assert.False(LiveNotesSchedule.IsDue(notes, 8, 30));
        Assert.True(LiveNotesSchedule.IsDue(notes, 8, 46));
    }

    [Fact]
    public void AReplyBecomesNotesThatCoverTheLinesSent()
    {
        var reply = """
            好的，这是更新后的纪要：
            {"summary": "讨论新版本的上线范围。", "topics": [{"title": "上线计划", "points": ["下周三前上线", "支付问题今天修"]}],
             "decisions": ["周三上线范围不变"], "todos": [{"text": "回归测试", "owner": "我"}, {"text": "起草说明", "owner": null}]}
            """;
        var notes = LiveNotesPrompt.Parse(reply, new LiveNotes(), 6);
        Assert.NotNull(notes);
        Assert.Equal("讨论新版本的上线范围。", notes!.Summary);
        Assert.Equal(new[] { "上线计划" }, notes.Topics.Select(topic => topic.Title));
        Assert.Equal(new[] { new LiveNotes.Todo("回归测试", "我"), new LiveNotes.Todo("起草说明") }, notes.Todos);
        Assert.Equal(6, notes.Covered);
        Assert.Contains("周三上线范围不变", notes.Statements);
    }

    [Fact]
    public void SettledTopicsStayAndAreNotSentBackForRewriting()
    {
        var previous = new LiveNotes
        {
            Summary = "三个议题",
            Covered = 9,
            Topics =
            [
                new("上线计划", ["周三上线"]),
                new("导出功能", ["下个版本做"]),
                new("客户说明", ["周五前给"])
            ]
        };
        var prompt = LiveNotesPrompt.User(previous, Lines(12), "简体中文");
        Assert.Contains("较早的议题（已定稿", prompt);
        Assert.Contains("- 上线计划", prompt);
        Assert.DoesNotContain("\"周三上线\"", prompt);
        Assert.Contains("第 9 句的内容", prompt);
        Assert.DoesNotContain("第 6 句的内容", prompt);
        // The reply holds the last two topics and a new one; the first is laid back in front.
        var reply = """
            {"summary": "四个议题", "topics": [{"title": "导出功能", "points": ["下个版本做", "小王接手"]}, {"title": "客户说明", "points": ["周五前给"]}, {"title": "回归测试", "points": ["周二出结果"]}], "decisions": [], "todos": []}
            """;
        var notes = LiveNotesPrompt.Parse(reply, previous, 12);
        Assert.NotNull(notes);
        Assert.Equal(new[] { "上线计划", "导出功能", "客户说明", "回归测试" },
            notes!.Topics.Select(topic => topic.Title));
        Assert.Equal(2, notes.Topics[1].Points.Count);
    }

    [Fact]
    public void AnUnreadableOrEmptyReplyNeverReplacesTheNotes()
    {
        var previous = new LiveNotes { Summary = "已有内容", Topics = [new("上线计划", ["周三上线"])] };
        Assert.Null(LiveNotesPrompt.Parse("抱歉，我无法处理。", previous, 5));
        Assert.Null(LiveNotesPrompt.Parse("""{"summary": "", "topics": []}""", previous, 5));
        Assert.Null(LiveNotesPrompt.Parse(
            """{"summary": "", "topics": [{"title": "别的", "points": ["一点"]}]}""", previous, 5));
    }

    [Fact]
    public void RecordingTimeAndQuestionsAddUpByMonthAndBySeries()
    {
        var plus8 = TimeZoneInfo.CreateCustomTimeZone("test+08", TimeSpan.FromHours(8), "+08", "+08");
        SessionMeta Meeting(int day, int month, double minutes, int questions, int answers,
            Guid? series = null, bool ended = true)
        {
            var start = new DateTimeOffset(2026, month, day, 10, 0, 0, TimeSpan.FromHours(8));
            return new SessionMeta
            {
                StartedAt = start,
                EndedAt = ended ? start.AddMinutes(minutes) : null,
                Mode = "labMeeting",
                AudioSource = "",
                Recognizer = "",
                AnswerModel = "",
                QuestionCount = questions,
                AnswerCount = answers,
                WorkspaceId = series?.ToString()
            };
        }

        var lab = Guid.NewGuid();
        var all = new[]
        {
            Meeting(3, 11, minutes: 62, questions: 4, answers: 2, series: lab),
            Meeting(18, 11, minutes: 30.7, questions: 9, answers: 9),
            Meeting(20, 10, minutes: 45, questions: 1, answers: 0, series: lab),
            Meeting(19, 11, minutes: 99, questions: 0, answers: 0, ended: false)
        };
        var month = MeetingStats.Month(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.FromHours(8)), plus8);
        var november = MeetingStats.Of(all, month);
        Assert.Equal(3, november.Meetings);
        Assert.Equal(13, november.Questions);
        Assert.Equal(11, november.Answers);
        Assert.Equal(1, november.HoursAndMinutes.Hours);
        Assert.Equal(32, november.HoursAndMinutes.Minutes);
        Assert.Equal(4, MeetingStats.Of(all).Meetings);
        Assert.Equal(new MeetingStats(2, 107 * 60, 5, 2), MeetingStats.Of(all, series: lab));
        Assert.True(MeetingFeatures.Standard(prompting: false, speakers: true).LiveNotes);
        Assert.False(MeetingFeatures.Standard(prompting: true, speakers: true).LiveNotes);
    }
}
