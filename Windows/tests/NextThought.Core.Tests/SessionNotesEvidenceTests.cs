using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for turning structured notes into evidence references.</summary>
public sealed class SessionNotesEvidenceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(8));

    private static readonly TranscriptLine[] Lines =
    [
        new() { Text = "先在预发环境演练扩容", Date = Start.AddSeconds(65) },
        new() { Text = "监控阈值提前配置", Date = Start.AddSeconds(125) }
    ];

    [Fact]
    public void OutlineReferencesMatchQuotesAndLocations()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("扩容",
                Points: [new OutlinePoint("先在预发环境演练扩容", 65), new OutlinePoint("没有出现的结论", 180)])]));
        var references = SessionNotesEvidence.References(payload, Lines, "t1");
        Assert.Equal(2, references.Count);

        var found = references[0];
        Assert.Equal(SessionEvidenceSupport.Quoted, found.Support);
        Assert.False(found.NeedsConfirmation);
        Assert.Equal("01:05", found.Location);
        Assert.Equal("扩容", found.Title);
        Assert.Equal(SessionEvidenceOrigin.StructuredNotes, found.Origin);
        Assert.StartsWith("notes:section:0:point:0", found.Id);

        var missing = references[1];
        Assert.Equal(SessionEvidenceSupport.QuoteNotFound, missing.Support);
        Assert.True(missing.NeedsConfirmation);
        Assert.Equal("03:00", missing.Location);
    }

    [Fact]
    public void TargetsCarryTheCurrentPayloadRevision()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("结论", Points: [new OutlinePoint("先在预发环境演练扩容")])]));
        var reference = Assert.Single(SessionNotesEvidence.References(payload, Lines, "t1"));
        Assert.Equal(SessionNotesEditor.PayloadRevision(payload), reference.StructuredTarget!.PayloadRevision);
        Assert.Equal("section:0:point:0", reference.StructuredTarget.Path);
        Assert.Equal(NotesFormat.Outline, reference.StructuredTarget.Format);
    }

    [Fact]
    public void FishboneChildrenBecomeReferences()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Fishbone,
            fishbone: new FishboneNotes("扩容失败", Categories:
            [
                new FishboneCategory("配置", [new TimedText("监控阈值提前配置", 125)]),
                new FishboneCategory("流程", [])
            ]));
        var references = SessionNotesEvidence.References(payload, Lines, "t1");
        Assert.Equal(2, references.Count);
        Assert.Equal("head", references[0].StructuredTarget!.Path);
        Assert.Equal(SessionEvidenceSupport.Quoted, references[1].Support);
        Assert.Equal("02:05", references[1].Location);
        Assert.Equal("配置", references[1].Title);
    }

    [Fact]
    public void ViewpointPositionsAndOwnersAreCovered()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Viewpoints,
            viewpoints: new ViewpointMatrix("扩容",
                Issues: [new ViewpointIssue("容量", [new ViewpointPosition("后端", "先在预发环境演练扩容", 65)])],
                Consensus: [new TimedText("监控阈值提前配置", 125)],
                Disagreements: [new TimedText("成本", 200)],
                Owners: [new ViewpointOwner("我", "先在预发环境演练扩容", At: 65)]));
        var references = SessionNotesEvidence.References(payload, Lines, "t1");
        Assert.Equal(4, references.Count);
        Assert.All(references, reference => Assert.Equal(SessionEvidenceOrigin.StructuredNotes, reference.Origin));
        Assert.Equal("容量 · 后端", references[0].Title);
        Assert.Contains(references, reference => reference.Id.StartsWith("notes:consensus:") && reference.Support == SessionEvidenceSupport.Quoted);
        Assert.Contains(references, reference => reference.Id.StartsWith("notes:disagreement:") && reference.NeedsConfirmation);
        Assert.Contains(references, reference => reference.StructuredTarget!.Path == "owner:0:task");
    }

    [Fact]
    public void EmptyPayloadOrBlankClaimsProduceNothing()
    {
        Assert.Empty(SessionNotesEvidence.References(new StructuredNotesPayload(NotesFormat.Outline), Lines, "t1"));
        var blank = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("空", Points: [new OutlinePoint("   ")])]));
        Assert.Empty(SessionNotesEvidence.References(blank, Lines, "t1"));
    }

    [Fact]
    public void EmptyTranscriptMakesEverythingNeedConfirmation()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("结论", Points: [new OutlinePoint("先在预发环境演练扩容")])]));
        var reference = Assert.Single(SessionNotesEvidence.References(payload, [], "t1"));
        Assert.Equal(SessionEvidenceSupport.QuoteNotFound, reference.Support);
        Assert.True(reference.NeedsConfirmation);
    }
}
