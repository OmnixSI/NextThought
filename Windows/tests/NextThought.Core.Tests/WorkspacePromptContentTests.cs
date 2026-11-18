using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for series prompt lessons and summary write-back.</summary>
public sealed class WorkspacePromptContentTests
{
    private static WorkspaceSeries Series(params WorkspaceLesson[] lessons) => new(
        "s", "组会", DateTimeOffset.UtcNow, [], lessons);

    [Fact]
    public void ExperienceNotesPutPinnedFirstThenNewest()
    {
        var now = DateTimeOffset.UtcNow;
        var series = Series(
            new WorkspaceLesson { Text = "旧未置顶", CreatedAt = now.AddDays(-5) },
            new WorkspaceLesson { Text = "新未置顶", CreatedAt = now.AddDays(-1) },
            new WorkspaceLesson { Text = "置顶", Pinned = true, CreatedAt = now.AddDays(-10) });
        Assert.Equal("- 置顶\n- 新未置顶\n- 旧未置顶", series.ExperienceNotes());
    }

    [Fact]
    public void ExperienceNotesRespectTheBudgetAndSkipEmpty()
    {
        var series = Series(
            new WorkspaceLesson { Text = new string('a', 10) },
            new WorkspaceLesson { Text = new string('b', 10) },
            new WorkspaceLesson { Text = new string('c', 10) });
        var notes = series.ExperienceNotes(26);
        Assert.Equal(2, notes.Split('\n').Length);
    }

    [Fact]
    public void ExperienceNotesFollowUseKnowledgeAndEmptyLessons()
    {
        Assert.Equal("", (Series(new WorkspaceLesson { Text = "x" }) with { UseKnowledge = false }).ExperienceNotes());
        Assert.Equal("", Series().ExperienceNotes());
    }

    [Fact]
    public void DocumentsOfASeriesCanFilterDisabledOnes()
    {
        var series = Series() with
        {
            Documents =
            [
                new ContextDocument { Name = "简历", Pages = ["内容"], Enabled = true },
                new ContextDocument { Name = "JD", Pages = ["要求"], Enabled = false }
            ]
        };
        Assert.Equal(2, series.DocumentsOf().Count);
        var enabled = series.DocumentsOf(enabledOnly: true);
        Assert.Single(enabled);
        Assert.Equal("简历", enabled[0].Name);
        Assert.Empty(Series().DocumentsOf(enabledOnly: true));
    }

    [Fact]
    public void MaterialTextUsesProfileOrderThenUnknownKeys()
    {
        var series = Series();
        var fields = new Dictionary<string, string>
        {
            ["role"] = " 后端 ",
            ["company"] = "某公司",
            ["extra"] = "额外",
            ["empty"] = "   "
        };
        var text = series.MaterialText([("company", "公司"), ("role", "岗位")], fields);
        Assert.Equal("公司：某公司\n岗位：后端\nextra：额外", text);
    }

    [Fact]
    public void MaterialTextSkipsEmptyValuesAndUnknownOrderingIsStable()
    {
        var series = Series();
        Assert.Equal("", series.MaterialText([], new Dictionary<string, string>()));
        Assert.Equal("", series.MaterialText([("a", "A")], new Dictionary<string, string> { ["a"] = " " }));
        Assert.Equal("a：1\nb：2", series.MaterialText([], new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" }));
    }

    [Fact]
    public void SummaryWriteBackUpdatesTheStoredPayload()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("旧标题")]));
        var summary = new SessionSummary { Markdown = "#", Structured = payload };
        var target = new SessionStructuredNotesTarget(NotesFormat.Outline, "section:0",
            SessionNotesEditor.PayloadRevision(payload));
        var updated = SessionNotesEditor.UpdateSummary(summary, target, "新标题");
        Assert.NotNull(updated);
        Assert.Equal("新标题", updated!.Structured!.Outline!.Sections[0].Title);
        Assert.Equal("#", updated.Markdown);
    }

    [Fact]
    public void SummaryWriteBackRefusesMissingOrStalePayloads()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("旧标题")]));
        var target = new SessionStructuredNotesTarget(NotesFormat.Outline, "section:0",
            SessionNotesEditor.PayloadRevision(payload));
        Assert.Null(SessionNotesEditor.UpdateSummary(new SessionSummary(), target, "x"));
        var wrongFormat = new SessionSummary
        {
            Structured = new StructuredNotesPayload(NotesFormat.Viewpoints, viewpoints: new ViewpointMatrix("t"))
        };
        Assert.Null(SessionNotesEditor.UpdateSummary(wrongFormat, target, "x"));
        var stale = new SessionSummary { Structured = payload };
        var outdated = target with { PayloadRevision = "old" };
        Assert.Null(SessionNotesEditor.UpdateSummary(stale, outdated, "x"));
    }
}
