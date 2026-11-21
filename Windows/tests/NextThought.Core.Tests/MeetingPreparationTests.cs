using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Port of MeetingPreparationTests.swift.</summary>
public sealed class MeetingPreparationTests
{
    private static WorkspaceSeries Series(string name = "组会", string mode = "labMeeting", string? style = null,
        string preferences = "") =>
        new(Guid.NewGuid().ToString("D"), name, DateTimeOffset.UnixEpoch, [], [])
        {
            Mode = mode,
            Style = style,
            Preferences = preferences
        };

    [Fact]
    public void SingleMeetingTypeAppliesWithoutChangingSeriesDefaults()
    {
        var series = Series("周组会", "labMeeting", "detailed");
        var result = new MeetingPlan { Mode = MeetingMode.Academic }.PreparedWorkspace(series);
        Assert.Equal("academic", result.Mode);
        Assert.Equal("concise", result.Style);
        Assert.NotEmpty(result.Preferences!);
        Assert.Equal("labMeeting", series.Mode);
        Assert.Equal("detailed", series.Style);
    }

    [Fact]
    public void SceneStyleAppliesToEveryMeetingUnlessTheSeriesChoseItsOwn()
    {
        var plain = Series();
        Assert.Equal("detailed", new MeetingPlan { Mode = MeetingMode.LabMeeting }.PreparedWorkspace(plain).Style);
        Assert.Equal("concise", new MeetingPlan { Mode = MeetingMode.LabMeeting, SceneStyle = "concise" }
            .PreparedWorkspace(plain).Style);
        var explicitStyle = Series(style: "concise");
        Assert.Equal("concise", new MeetingPlan { Mode = MeetingMode.LabMeeting, SceneStyle = "detailed" }
            .PreparedWorkspace(explicitStyle).Style);
    }

    [Fact]
    public void ExcludedCarryNeverReappearsAndNewFilesStayAvailable()
    {
        var old = new ContextDocument { Id = "old", Name = "旧论文", Pages = ["prior evidence"] };
        var fresh = new ContextDocument { Id = "new", Name = "新实验", Pages = ["current evidence"] };
        var transcript = new ContextDocument { Id = "transcript", Name = "指定的上一场", Pages = ["selected meeting"] };
        var series = Series("研究讨论") with
        {
            DocumentIds = [old.Id, fresh.Id],
            Documents = [old, fresh],
            Lessons = [new WorkspaceLesson { Text = "prior lesson" }],
            QuestionBank = [new QuestionBankItem { Question = "prior question", Answer = "prior answer" }]
        };
        var plan = new MeetingPlan
        {
            Mode = MeetingMode.Discussion,
            Carry = MeetingCarry.Standard with { Materials = false, Review = false, Lessons = false, Transcript = true },
            BaselineDocumentIds = new HashSet<string> { old.Id },
            PreviousSessionId = "previous"
        };
        var preview = plan.PreparedWorkspace(series, transcript);
        var live = plan.PreparedWorkspace(series, transcript);
        Assert.Equal(preview.DocumentIds, live.DocumentIds);
        Assert.Equal(preview.Lessons, live.Lessons);
        Assert.Equal(preview.Mode, live.Mode);
        Assert.Equal(preview.Style, live.Style);
        Assert.Equal([fresh.Id, transcript.Id], preview.DocumentIds);
        Assert.Empty(preview.Lessons);
        Assert.Empty(preview.QuestionBank);
        Assert.Equal("previous", plan.PreviousSessionId);
        Assert.Equal([old.Id, fresh.Id], series.DocumentIds);
    }

    [Fact]
    public void CustomMeetingUsesItsOwnBaseAndKeepsExplicitAnswerRequirements()
    {
        var series = Series("实验室", "meeting", preferences: "只引用已验证的实验数据");
        var result = new MeetingPlan
        {
            Mode = MeetingMode.Custom,
            CustomTypeName = "导师一对一",
            CustomBaseMode = MeetingMode.LabMeeting
        }.PreparedWorkspace(series);
        Assert.Equal("custom", result.Mode);
        Assert.Equal("labMeeting", result.CustomBaseMode);
        Assert.Equal("导师一对一", result.CustomTypeName);
        Assert.Equal(series.Preferences, result.Preferences);
        Assert.Equal("meeting", series.Mode);
        Assert.Null(series.CustomTypeName);
    }

    [Fact]
    public void TranscriptIsOnlyIncludedWhenExplicitlySelected()
    {
        var series = Series();
        var transcript = new ContextDocument { Id = "transcript", Name = "上一场", Pages = ["private prior discussion"] };
        Assert.Empty(new MeetingPlan { Mode = MeetingMode.LabMeeting }.PreparedWorkspace(series, transcript).Documents);
        Assert.Contains(new MeetingPlan
        {
            Mode = MeetingMode.LabMeeting,
            Carry = MeetingCarry.Standard with { Transcript = true }
        }.PreparedWorkspace(series, transcript).Documents, document => document.Id == "transcript");
    }
}
