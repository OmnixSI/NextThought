using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B6: translated from SessionEvidenceTests.swift.</summary>
public sealed class SessionEvidenceBuilderTests
{
    private static SessionMeta Meta(DateTimeOffset start) => new() { StartedAt = start, EndedAt = start.AddSeconds(100) };

    private static StructuredNotesDoc Doc(params NotesSectionResult[] sections) =>
        new("general", "通用", "zh", "纪要", sections);

    [Fact]
    public void ValidTimestampAloneNeverClaimsThatAModelFactIsSupported()
    {
        var start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var line = new TranscriptLine
        {
            Date = start.AddSeconds(32), StartedAt = start.AddSeconds(32), Speaker = "remote",
            Text = "我们会等外部数据确认后再提交。"
        };
        var notes = Doc(new NotesSectionResult("decisions", "决定", NotesSectionKind.Items, false,
            [new NotesItem { Text = "已决定周五提交。", Time = "00:32" }]));
        var evidence = SessionEvidenceBuilder.Build(Meta(start), [line], notes, null);
        var reference = Assert.Single(evidence);
        Assert.Equal(SessionEvidenceSupport.TimeOnly, reference.Support);
        Assert.Equal(line.Id, reference.LineId);
        Assert.Equal("已决定周五提交。", reference.Claim);
        Assert.Null(reference.ReviewDecision);
        Assert.Equal(64, reference.ClaimRevision?.Length);
        Assert.True(reference.NeedsConfirmation);
    }

    [Fact]
    public void ExactQuoteLinksToItsStableLineAndKeepsTheSourceRevision()
    {
        var start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var line = new TranscriptLine
        {
            Date = start.AddSeconds(65), StartedAt = start.AddSeconds(65), Speaker = "remote",
            Text = "等外部数据确认后再提交。"
        };
        var notes = Doc(new NotesSectionResult("actions", "待办", NotesSectionKind.Actions, false,
            [new NotesItem { Text = "周五前提交", Time = "01:05", Quote = "等外部数据确认后再提交。" }]));
        var reference = Assert.Single(SessionEvidenceBuilder.Build(Meta(start), [line], notes, null));
        Assert.Equal(SessionEvidenceSupport.Quoted, reference.Support);
        Assert.Equal(line.Id, reference.LineId);
        Assert.Equal(SessionEvidenceSourceVariant.Corrected, reference.SourceVariant);
        Assert.Equal(TranscriptRevisions.Of(line.Id, line.Text), reference.SourceRevision);
    }
}
