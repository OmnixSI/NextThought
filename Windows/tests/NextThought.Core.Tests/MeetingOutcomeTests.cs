using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E1: the home page's meeting outcome lines (HomeView.swift MeetingOutcome).
/// </summary>
[Collection("UiTextGlobalState")]
public sealed class MeetingOutcomeTests : IDisposable
{
    private readonly bool previousEnglish = UiText.EnglishUi;

    public MeetingOutcomeTests() => UiText.EnglishUi = false;

    public void Dispose() => UiText.EnglishUi = previousEnglish;

    private static SessionMeta Meta(
        string mode = "interview",
        string? typeName = null,
        double? improve = null,
        double? actions = null,
        int minutes = 52,
        string summaryState = "done",
        DateTimeOffset? startedAt = null)
    {
        var start = startedAt ?? DateTimeOffset.Now.AddMinutes(-minutes);
        var extra = new Dictionary<string, JsonElement>();
        if (typeName is not null) extra["typeName"] = JsonSerializer.SerializeToElement(typeName);
        if (improve is not null) extra["improveCount"] = JsonSerializer.SerializeToElement(improve.Value);
        if (actions is not null) extra["actionItemCount"] = JsonSerializer.SerializeToElement(actions.Value);
        return new SessionMeta
        {
            Id = Guid.NewGuid(),
            Title = "字节跳动 后端二面",
            Mode = mode,
            StartedAt = start,
            EndedAt = start.AddMinutes(minutes),
            SummaryState = summaryState,
            AdditionalProperties = extra
        };
    }

    [Fact]
    public void SummarizingAndFailedStatesUseTheMacLines()
    {
        Assert.Equal("正在整理纪要", MeetingOutcome.Line(Meta(), summarizing: true));
        Assert.Equal("纪要没有整理好 · 可以重试", MeetingOutcome.Line(Meta(summaryState: "failed")));
        Assert.Equal("纪要没有整理好 · 可以重试", MeetingOutcome.Line(Meta(summaryState: "pending")));
    }

    [Fact]
    public void ImproveAndActionCountsComeFirst()
    {
        Assert.Equal("2 道题待改进", MeetingOutcome.Line(Meta(improve: 2, actions: 3)));
        Assert.Equal("3 个待办", MeetingOutcome.Line(Meta(actions: 3)));
    }

    [Fact]
    public void OtherwiseTheLineIsSceneAndDuration()
    {
        Assert.Equal("面试 · 52 分钟", MeetingOutcome.Line(Meta()));
        // A Mac custom type name wins over the scene title.
        Assert.Equal("字节跳动二面 · 52 分钟", MeetingOutcome.Line(Meta(typeName: "字节跳动二面")));
        // A zero-minute meeting shows the scene alone.
        Assert.Equal("面试", MeetingOutcome.Line(Meta(minutes: 0)));
    }

    [Fact]
    public void TypeNameFollowsTheMacSceneTitles()
    {
        Assert.Equal("面试", MeetingOutcome.TypeName(Meta("interview")));
        Assert.Equal("组会", MeetingOutcome.TypeName(Meta("labMeeting")));
        Assert.Equal("讲座 / 课程", MeetingOutcome.TypeName(Meta("lesson")));
        Assert.Equal("通用", MeetingOutcome.TypeName(Meta("general")));
        Assert.Equal("自定义", MeetingOutcome.TypeName(Meta("custom")));
    }

    [Fact]
    public void WhenFollowsTodayYesterdayWeekdayAndDate()
    {
        var now = new DateTimeOffset(2026, 11, 21, 18, 0, 0, TimeSpan.FromHours(8));
        Assert.Equal("今天 上午10:00", MeetingOutcome.When(new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.FromHours(8)), now, english: false));
        Assert.Equal("昨天 上午10:00", MeetingOutcome.When(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.FromHours(8)), now, english: false));
        Assert.Equal("周四", MeetingOutcome.When(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.FromHours(8)), now, english: false));
        Assert.Equal("10月2日", MeetingOutcome.When(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(8)), now, english: false));
        // English: localized templates and invariant weekday/month-day.
        Assert.Equal("Today 10:00 AM", MeetingOutcome.When(new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.FromHours(8)), now, english: true));
        Assert.Equal("Thursday", MeetingOutcome.When(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.FromHours(8)), now, english: true));
        Assert.Equal("Oct 2", MeetingOutcome.When(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(8)), now, english: true));
    }
}
