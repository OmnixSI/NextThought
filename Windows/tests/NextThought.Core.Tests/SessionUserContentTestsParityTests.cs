using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from SessionUserContentTests.swift: user-authored content lives beside the
/// replaceable AI output, keeps a checked revision and never gets silently overwritten.
/// </summary>
public sealed class SessionUserContentTestsParityTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("nt-user-content-parity-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(directory, true); } catch { /* best effort */ }
    }

    [Fact]
    public void SchemaOneDecisionsWithoutIDsHaveAStableFallback()
    {
        var lineId = Guid.NewGuid();
        var json = $$"""
            {"schemaVersion":1,"correctionDecisions":[{"lineID":"{{lineId}}","original":"Mamba","corrected":"曼巴","reverted":true}]}
            """;
        var first = JsonSerializer.Deserialize<SessionUserContent>(json, SessionUserContentStore.Options)!;
        var second = JsonSerializer.Deserialize<SessionUserContent>(json, SessionUserContentStore.Options)!;
        Assert.Equal(first.CorrectionDecisions[0].Id, second.CorrectionDecisions[0].Id);
        Assert.Equal(lineId, first.CorrectionDecisions[0].LineId);
        Assert.Null(first.EditedSummary);
        Assert.True(first.HasContent);
        var encoded = JsonSerializer.Serialize(first, SessionUserContentStore.Options);
        var roundTrip = JsonSerializer.Deserialize<SessionUserContent>(encoded, SessionUserContentStore.Options)!;
        Assert.Equal(first.CorrectionDecisions[0].Id, roundTrip.CorrectionDecisions[0].Id);
    }

    [Fact]
    public void FullSummaryOverrideKeepsStructuredPayloadAcrossAIDraftReplacement()
    {
        var generatedAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var payload = new StructuredNotesPayload(NotesFormat.Outline, outline: new OutlineNotes(
            Sections: [new OutlineSection("Edited section", Points: [new OutlinePoint("User-edited point")])]),
            transcriptSourceRevision: "source-v1");
        var summary = new SessionSummary
        {
            Title = "Edited",
            Markdown = "User-edited summary",
            Questions = [],
            GeneratedAt = generatedAt,
            // Swift's SessionSummary.store(payload) for a single view: the payload is both the
            // current structured view and its only entry in structuredAll.
            Structured = payload,
            StructuredAll = [payload]
        };
        var content = new SessionUserContent { EditedSummary = summary };
        SessionUserContentStore.Save(content, directory);

        var replacement = new SessionSummary
        {
            Title = "New AI", Markdown = "New AI original", Questions = [], GeneratedAt = generatedAt
        };
        File.WriteAllText(Path.Combine(directory, "summary.json"),
            JsonSerializer.Serialize(replacement, SessionUserContentStore.Options));

        var reopened = SessionUserContentStore.Load(directory);
        Assert.True(reopened.HasContent);
        Assert.Equal("User-edited summary", reopened.EditedSummary!.Markdown);
        Assert.Equal("User-edited point",
            reopened.EditedSummary.Structured!.Outline!.Sections[0].Points[0].Text);
        Assert.Equal("source-v1", reopened.EditedSummary.Structured.TranscriptSourceRevision);
    }

    [Fact]
    public void UserDraftRoundTripsIndependentlyOfAIResults()
    {
        var line = Guid.NewGuid();
        var content = new SessionUserContent
        {
            PersonalNotes = "下次验证消融实验",
            Highlights = [new SessionHighlight { LineId = line, OffsetSeconds = 5, Quote = "导师的建议" }],
            EditedNotes = new StructuredNotesPayload(NotesFormat.Outline, outline: new OutlineNotes(
                Sections: [new OutlineSection("研究进展", Points: [new OutlinePoint("确认过的结论", 5)])]))
        };
        var saved = SessionUserContentStore.Save(content, directory);
        Assert.Equal(1, saved.Revision);

        File.WriteAllText(Path.Combine(directory, "notes.json"), """{"title":"新的AI结果"}""");

        var reopened = SessionUserContentStore.Load(directory);
        Assert.Equal(saved.Revision, reopened.Revision);
        Assert.Equal(saved.PersonalNotes, reopened.PersonalNotes);
        Assert.Equal("确认过的结论", reopened.EditedNotes!.Outline!.Sections[0].Points[0].Text);
        Assert.Equal(5, reopened.EditedNotes.Outline.Sections[0].Points[0].At!.Value);
        Assert.Equal(line, reopened.Highlights[0].LineId);
        Assert.True(reopened.HasContent);
        Assert.Throws<SessionUserContentStore.ChangedElsewhereException>(
            () => SessionUserContentStore.Save(content, directory));
        Assert.Equal(saved.Revision, SessionUserContentStore.Load(directory).Revision);
    }

    [Fact]
    public void ExplicitCorrectionSurvivesRegenerationAndLineReordering()
    {
        var first = new TranscriptLine { Date = DateTimeOffset.UtcNow, Speaker = "room", Text = "研究的是 Mamba 结构" };
        var second = new TranscriptLine { Date = DateTimeOffset.UtcNow, Speaker = "room", Text = "开始实验" };
        var content = new SessionUserContent
        {
            CorrectionDecisions = [new SessionCorrectionDecision(null, first.Id, "Mamba", "曼巴", true)]
        };
        var regenerated = new[]
        {
            new TranscriptCorrection { Line = 1, Original = "Mamba", Corrected = "另一种错误译名" }
        };

        var corrections = content.Corrections(regenerated, [second, first]);

        Assert.Single(corrections);
        Assert.True(corrections[0].Reverted);
        Assert.Equal(first.Text, NotesCorrections.Apply(corrections, [second, first])[1].Text);
    }

    [Fact]
    public void CorruptContentIsNeverSilentlyOverwritten()
    {
        var path = Path.Combine(directory, SessionUserContent.FileName);
        File.WriteAllText(path, "{incomplete");

        Assert.ThrowsAny<Exception>(() => SessionUserContentStore.Save(new SessionUserContent(), directory));

        Assert.Equal("{incomplete", File.ReadAllText(path));
    }

    [Fact]
    public void FailedWriteDoesNotPublishASavedRevision()
    {
        var blocked = Path.Combine(directory, "blocked");
        File.WriteAllText(blocked, "existing file");
        var content = new SessionUserContent { PersonalNotes = "不得丢失的笔记" };

        Assert.ThrowsAny<Exception>(() => SessionUserContentStore.Save(content, blocked));

        Assert.Equal(0, content.Revision);
        Assert.Equal("existing file", File.ReadAllText(blocked));
    }

    [Fact(Skip = "missing API: SessionSummaryPrompt.Material; AnswerCompletion tolerant decoding")]
    public void OldContextsDecodeAndIncompleteAnswersAreExcludedFromNotesInput()
    {
    }
}
