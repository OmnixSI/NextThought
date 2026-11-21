using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Translated from Tests/NextThoughtCoreTests/MeetingPreparationTests.swift.</summary>
public sealed class MeetingPreparationTestsParityTests
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
        Assert.Equal(ModeProfile.For(MeetingMode.Academic).TemplatePreferences, result.Preferences);
        Assert.Equal("labMeeting", series.Mode);
        Assert.Equal("detailed", series.Style);
    }

    [Fact]
    public void SceneStyleAppliesToEveryMeetingUnlessTheSeriesChoseItsOwn()
    {
        // Same type as the series: a lab meeting answers in detail by default.
        var plain = Series("组会", "labMeeting");
        Assert.Equal("detailed", new MeetingPlan { Mode = MeetingMode.LabMeeting }.PreparedWorkspace(plain).Style);
        // The scene setting wins over the type's default.
        Assert.Equal("concise", new MeetingPlan { Mode = MeetingMode.LabMeeting, SceneStyle = "concise" }
            .PreparedWorkspace(plain).Style);
        // A series whose length differs from its type's default keeps it.
        var explicitStyle = Series("组会", "labMeeting", "concise");
        Assert.Equal("concise", new MeetingPlan { Mode = MeetingMode.LabMeeting, SceneStyle = "detailed" }
            .PreparedWorkspace(explicitStyle).Style);
    }

    [Fact]
    public void ExcludedCarryNeverReappearsAndNewFilesStayAvailable()
    {
        var old = new ContextDocument { Id = Guid.NewGuid().ToString("D"), Name = "旧论文", Pages = ["prior evidence"] };
        var fresh = new ContextDocument { Id = Guid.NewGuid().ToString("D"), Name = "新实验", Pages = ["current evidence"] };
        var transcript = new ContextDocument { Id = Guid.NewGuid().ToString("D"), Name = "指定的上一场", Pages = ["selected meeting"] };
        var series = Series("研究讨论", "labMeeting") with
        {
            Documents = [old, fresh],
            Lessons = [new WorkspaceLesson { Text = "prior lesson" }],
            QuestionBank = [new QuestionBankItem { Question = "prior question", Answer = "prior answer" }]
        };
        var original = series;
        var previous = Guid.NewGuid().ToString("D");
        var plan = new MeetingPlan
        {
            Mode = MeetingMode.Discussion,
            Carry = new MeetingCarry { Materials = false, Review = false, Lessons = false, Transcript = true },
            BaselineDocumentIds = new HashSet<string> { old.Id },
            PreviousSessionId = previous
        };
        var preview = plan.PreparedWorkspace(series, transcript);
        var live = plan.PreparedWorkspace(series, transcript);
        Assert.Equal(preview.DocumentIds, live.DocumentIds);
        Assert.Equal(preview.Documents, live.Documents);
        Assert.Equal(preview.Lessons, live.Lessons);
        Assert.Equal(preview.QuestionBank, live.QuestionBank);
        Assert.Equal([fresh.Id, transcript.Id], preview.Documents.Select(document => document.Id));
        Assert.Empty(preview.Lessons);
        Assert.Empty(preview.QuestionBank);
        Assert.Equal(previous, plan.PreviousSessionId);
        Assert.Equal(original, series);
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
        Assert.Equal(MeetingMode.LabMeeting, result.EffectiveMode);
        Assert.Equal("导师一对一", result.CustomTypeName);
        Assert.Equal(series.Preferences, result.Preferences);
        Assert.Equal("meeting", series.Mode);
        Assert.Null(series.CustomTypeName);
    }

    [Fact]
    public void TranscriptIsOnlyIncludedWhenExplicitlySelected()
    {
        var series = Series("组会", "labMeeting");
        var transcript = new ContextDocument
        {
            Id = Guid.NewGuid().ToString("D"), Name = "上一场", Pages = ["private prior discussion"]
        };
        Assert.Empty(new MeetingPlan { Mode = MeetingMode.LabMeeting }
            .PreparedWorkspace(series, transcript).Documents);
    }
}
