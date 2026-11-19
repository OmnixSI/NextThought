using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for path-based structured-notes editing with revision guards.</summary>
public sealed class SessionNotesEditorTests
{
    private static StructuredNotesPayload Outline() => new(NotesFormat.Outline,
        outline: new OutlineNotes(
            [new OutlineSection("结论", Points: [new OutlinePoint("先演练", Children: [new TimedText("再上线")])])],
            [new OutlineConcept("缓存", "命中率")],
            [new TimedText("复习并发")]));

    private static SessionStructuredNotesTarget Target(StructuredNotesPayload payload, string path) =>
        new(payload.Format, path, SessionNotesEditor.PayloadRevision(payload));

    [Fact]
    public void RevisionIsStableAndChangesWithContent()
    {
        var payload = Outline();
        Assert.Equal(SessionNotesEditor.PayloadRevision(payload), SessionNotesEditor.PayloadRevision(payload));
        var changed = payload with { Outline = payload.Outline! with { Sections = [] } };
        Assert.NotEqual(SessionNotesEditor.PayloadRevision(payload), SessionNotesEditor.PayloadRevision(changed));
    }

    [Fact]
    public void OutlineSectionPointAndChildAreEditable()
    {
        var payload = Outline();
        var section = SessionNotesEditor.Apply(payload, Target(payload, "section:0"), "新结论")!;
        Assert.Equal("新结论", section.Outline!.Sections[0].Title);

        var point = SessionNotesEditor.Apply(payload, Target(payload, "section:0:point:0"), "先灰度")!;
        Assert.Equal("先灰度", point.Outline!.Sections[0].Points[0].Text);
        Assert.Equal("再上线", point.Outline.Sections[0].Points[0].Children[0].Text);

        var child = SessionNotesEditor.Apply(payload, Target(payload, "section:0:point:0:child:0"), "全量")!;
        Assert.Equal("全量", child.Outline!.Sections[0].Points[0].Children[0].Text);
    }

    [Fact]
    public void ConceptReviewAndInvalidPaths()
    {
        var payload = Outline();
        var term = SessionNotesEditor.Apply(payload, Target(payload, "concept:0:term"), "缓存层")!;
        Assert.Equal("缓存层", term.Outline!.Concepts[0].Term);
        var meaning = SessionNotesEditor.Apply(payload, Target(payload, "concept:0:meaning"), "提升命中")!;
        Assert.Equal("提升命中", meaning.Outline!.Concepts[0].Meaning);
        var review = SessionNotesEditor.Apply(payload, Target(payload, "review:0"), "复习缓存")!;
        Assert.Equal("复习缓存", review.Outline!.Review[0].Text);

        Assert.Null(SessionNotesEditor.Apply(payload, Target(payload, "section:9"), "x"));
        Assert.Null(SessionNotesEditor.Apply(payload, Target(payload, "concept:0:unknown"), "x"));
        Assert.Null(SessionNotesEditor.Apply(payload, Target(payload, "review:9"), "x"));
    }

    [Fact]
    public void FishboneHeadAndCauseAreEditable()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Fishbone,
            fishbone: new FishboneNotes("页面慢", Categories: [new FishboneCategory("后端", [new TimedText("慢查询")])]));
        var head = SessionNotesEditor.Apply(payload, Target(payload, "head"), "接口慢")!;
        Assert.Equal("接口慢", head.Fishbone!.Head);
        var cause = SessionNotesEditor.Apply(payload, Target(payload, "category:0:cause:0"), "缺索引")!;
        Assert.Equal("缺索引", cause.Fishbone!.Categories[0].Causes[0].Text);
        Assert.Null(SessionNotesEditor.Apply(payload, Target(payload, "category:2:cause:0"), "x"));
    }

    [Fact]
    public void ViewpointTopicPositionsListsAndOwnersAreEditable()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Viewpoints,
            viewpoints: new ViewpointMatrix("选型",
                Issues: [new ViewpointIssue("数据库", [new ViewpointPosition("后端", "用 PG")])],
                Consensus: [new TimedText("先评估")],
                Disagreements: [new TimedText("成本")],
                Owners: [new ViewpointOwner("我", "写方案")]));
        Assert.Equal("技术选型", SessionNotesEditor.Apply(payload, Target(payload, "topic"), "技术选型")!.Viewpoints!.Topic);
        var position = SessionNotesEditor.Apply(payload, Target(payload, "issue:0:position:0:party"), "DBA")!;
        Assert.Equal("DBA", position.Viewpoints!.Issues[0].Positions[0].Party);
        var text = SessionNotesEditor.Apply(payload, Target(payload, "issue:0:position:0:text"), "用 MySQL")!;
        Assert.Equal("用 MySQL", text.Viewpoints!.Issues[0].Positions[0].Text);
        Assert.Equal("同意", SessionNotesEditor.Apply(payload, Target(payload, "consensus:0"), "同意")!.Viewpoints!.Consensus[0].Text);
        Assert.Equal("费用高", SessionNotesEditor.Apply(payload, Target(payload, "disagreement:0"), "费用高")!.Viewpoints!.Disagreements[0].Text);
        var owner = SessionNotesEditor.Apply(payload, Target(payload, "owner:0:who"), "对方")!;
        Assert.Equal("对方", owner.Viewpoints!.Owners[0].Who);
        Assert.Equal("评审", SessionNotesEditor.Apply(payload, Target(payload, "owner:0:task"), "评审")!.Viewpoints!.Owners[0].Task);
    }

    [Fact]
    public void StaleRevisionOrUnsupportedFormatIsRefused()
    {
        var payload = Outline();
        var stale = new SessionStructuredNotesTarget(NotesFormat.Outline, "section:0", "outdated");
        Assert.Null(SessionNotesEditor.Apply(payload, stale, "x"));
        var changed = payload with { Outline = payload.Outline! with { Sections = [] } };
        Assert.Null(SessionNotesEditor.Apply(changed, Target(payload, "section:0"), "x"));
        var standard = new StructuredNotesPayload(NotesFormat.Standard);
        Assert.Null(SessionNotesEditor.Apply(standard, Target(standard, "section:0"), "x"));
    }
}
