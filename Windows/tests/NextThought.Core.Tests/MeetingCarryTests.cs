using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for meeting carry selection and the meeting plan.</summary>
public sealed class MeetingCarryTests
{
    private static WorkspaceSeries Series(bool useKnowledge = true) => new(
        "series", "组会", DateTimeOffset.UtcNow, ["s1"],
        [new WorkspaceLesson { Text = "经验一" }])
    {
        UseKnowledge = useKnowledge,
        QuestionBank = [new QuestionBankItem { Question = "问", Answer = "答" }]
    };

    [Fact]
    public void StandardCarryKeepsEverythingExceptTheTranscript()
    {
        Assert.True(MeetingCarry.Standard.Materials);
        Assert.True(MeetingCarry.Standard.Review);
        Assert.True(MeetingCarry.Standard.Lessons);
        Assert.False(MeetingCarry.Standard.Transcript);
    }

    [Fact]
    public void SnapshotKeepsKnowledgeWhenEnabled()
    {
        var series = Series();
        var snapshot = MeetingCarry.Standard.WorkspaceSnapshot(series, new HashSet<string>());
        Assert.Single(snapshot.Lessons);
        Assert.Single(snapshot.QuestionBank);
        Assert.Equal("series", snapshot.Id);
    }

    [Fact]
    public void ReviewAndLessonsCanBeDroppedIndependently()
    {
        var noReview = MeetingCarry.Standard with { Review = false };
        var snapshot = noReview.WorkspaceSnapshot(Series(), new HashSet<string>());
        Assert.Empty(snapshot.QuestionBank);
        Assert.Single(snapshot.Lessons);

        var noLessons = MeetingCarry.Standard with { Lessons = false };
        snapshot = noLessons.WorkspaceSnapshot(Series(), new HashSet<string>());
        Assert.Single(snapshot.QuestionBank);
        Assert.Empty(snapshot.Lessons);
    }

    [Fact]
    public void DisabledKnowledgeDropsBothRegardlessOfFlags()
    {
        var snapshot = MeetingCarry.Standard.WorkspaceSnapshot(Series(useKnowledge: false), new HashSet<string>());
        Assert.Empty(snapshot.Lessons);
        Assert.Empty(snapshot.QuestionBank);
    }

    [Fact]
    public void MaterialsFlagIsInertWithoutSeriesDocuments()
    {
        var snapshot = (MeetingCarry.Standard with { Materials = false }).WorkspaceSnapshot(Series(), new HashSet<string> { "doc" });
        Assert.Single(snapshot.Lessons);
        Assert.Single(snapshot.QuestionBank);
    }

    [Fact]
    public void EffectiveModeFallsBackToTheCustomBase()
    {
        Assert.Equal(MeetingMode.Academic, new MeetingPlan { Mode = MeetingMode.Custom, CustomBaseMode = MeetingMode.Academic }.EffectiveMode);
        Assert.Equal(MeetingMode.Interview, new MeetingPlan { Mode = MeetingMode.Interview }.EffectiveMode);
        Assert.Equal(MeetingMode.General, new MeetingPlan().EffectiveMode);
    }

    [Fact]
    public void PreparedWorkspaceDoesNotTouchTheSeries()
    {
        var series = Series();
        var plan = new MeetingPlan
        {
            Title = "下一次",
            Carry = MeetingCarry.Standard with { Review = false },
            BaselineDocumentIds = new HashSet<string> { "doc-1" },
            PreviousSessionId = "s1"
        };
        var prepared = plan.PreparedWorkspace(series);
        Assert.Empty(prepared.QuestionBank);
        Assert.Single(prepared.Lessons);
        Assert.Single(series.QuestionBank);
        Assert.Equal("下一次", plan.Title);
        Assert.Equal("s1", plan.PreviousSessionId);
        Assert.Contains("doc-1", plan.BaselineDocumentIds);
    }
}
