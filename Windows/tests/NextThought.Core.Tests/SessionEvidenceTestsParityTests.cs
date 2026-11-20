using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Translated from Tests/NextThoughtCoreTests/SessionEvidenceTests.swift.</summary>
public sealed class SessionEvidenceTestsParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static SessionMeta Meta(DateTimeOffset start) => new()
    {
        StartedAt = start,
        EndedAt = start.AddSeconds(100),
        Mode = "meeting"
    };

    private static StructuredNotesDoc Doc(params NotesSectionResult[] sections) =>
        new("general", "通用", "zh", "纪要", sections);

    private static TranscriptLine Line(DateTimeOffset start, int offsetSeconds, string text) => new()
    {
        Date = start.AddSeconds(offsetSeconds),
        StartedAt = start.AddSeconds(offsetSeconds),
        Speaker = "remote",
        Text = text
    };

    [Fact]
    public void AValidTimestampAloneNeverClaimsThatAModelFactIsSupported()
    {
        var line = Line(Start, 32, "我们会等外部数据确认后再提交。");
        var notes = Doc(new NotesSectionResult("decisions", "决定", NotesSectionKind.Items, false,
            [new NotesItem { Text = "已决定周五提交。", Time = "00:32" }]));

        var evidence = SessionEvidenceBuilder.Build(Meta(Start), [line], notes, null);

        var reference = Assert.Single(evidence);
        Assert.Equal(SessionEvidenceSupport.TimeOnly, reference.Support);
        Assert.Equal(line.Id, reference.LineId);
        Assert.Equal("已决定周五提交。", reference.Claim);
        Assert.Null(reference.ReviewDecision);
        Assert.Equal(64, reference.ClaimRevision?.Length);
        Assert.True(reference.NeedsConfirmation);
        Assert.DoesNotContain("已确认", reference.Support.Label());
        Assert.DoesNotContain("confirmed", reference.Support.Label(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExactQuoteLinksToItsStableLineAndKeepsTheSourceRevision()
    {
        var line = Line(Start, 65, "等外部数据确认后再提交。");
        var notes = Doc(new NotesSectionResult("actions", "待办", NotesSectionKind.Actions, false,
            [new NotesItem { Text = "周五前提交", Time = "01:05", Quote = "等外部数据确认后再提交。" }]));

        var evidence = Assert.Single(SessionEvidenceBuilder.Build(Meta(Start), [line], notes, null));

        Assert.Equal(SessionEvidenceSupport.Quoted, evidence.Support);
        Assert.Equal(line.Id, evidence.LineId);
        Assert.Equal(SessionEvidenceSourceVariant.Corrected, evidence.SourceVariant);
        Assert.Equal(TranscriptRevisions.Of(line.Id, line.Text), evidence.SourceRevision);
    }

    [Fact]
    public void RepeatedQuoteUsesTheOccurrenceNearestItsTimestamp()
    {
        var first = Line(Start, 12, "周五发布新版。");
        var second = Line(Start, 82, "周五发布新版。");
        var notes = Doc(new NotesSectionResult("decisions", "决定", NotesSectionKind.Items, false,
            [new NotesItem { Text = "周五发布新版", Time = "01:20", Quote = "周五发布新版。" }]));

        var evidence = Assert.Single(SessionEvidenceBuilder.Build(Meta(Start), [first, second], notes, null));

        Assert.Equal(SessionEvidenceSupport.Quoted, evidence.Support);
        Assert.Equal(second.Id, evidence.LineId);
        Assert.Equal("01:22", evidence.Location);
    }

    [Fact(Skip = "Missing C# API: SessionEvidenceBuilder.Build has no corrections parameter (it builds the projection without " +
                 "StructuredNotesDoc.Corrections), so original-vs-corrected occurrence matching cannot be exercised.")]
    public void TimestampChoosesNearestOccurrenceAcrossOriginalAndCorrectedVersions()
    {
    }

    [Fact(Skip = "Missing C# API: SessionEvidenceBuilder.Build has no corrections parameter (it builds the projection without " +
                 "StructuredNotesDoc.Corrections), so a corrected-only quote cannot be matched.")]
    public void CorrectedOnlyQuoteIdentifiesTheCorrectedTranscriptVersion()
    {
    }

    [Fact]
    public void PersistedEvidenceDecisionIsBoundToClaimAndBothTranscriptRevisions()
    {
        var line = Line(Start, 32, "周五前提交接口评审。");
        var itemId = Guid.NewGuid().ToString("D");
        var notes = Doc(new NotesSectionResult("actions", "待办", NotesSectionKind.Actions, false,
            [new NotesItem { Id = itemId, Text = "周五前提交接口评审", Time = "00:32", Quote = "周五前提交接口评审。" }]));
        var meta = Meta(Start);
        var original = Assert.Single(SessionEvidenceBuilder.Build(meta, [line], notes, null));
        var review = new SessionEvidenceReview(SessionEvidenceDecision.Supported, original.ClaimRevision!,
            original.TranscriptSourceRevision, original.SourceRevision!, DateTimeOffset.UtcNow);
        var summary = new SessionSummary
        {
            Title = "会议",
            Markdown = "",
            GeneratedAt = Start,
            TranscriptSourceRevision = original.TranscriptSourceRevision,
            EvidenceReviews = new Dictionary<string, SessionEvidenceReview> { [original.Id] = review }
        };
        var json = JsonSerializer.Serialize(summary, JsonDefaults.Options);
        var restored = JsonSerializer.Deserialize<SessionSummary>(json, JsonDefaults.Options)!;

        var current = Assert.Single(SessionEvidenceBuilder.Build(meta, [line], notes, restored));
        Assert.Equal(SessionEvidenceDecision.Supported, current.ReviewDecision);
        Assert.False(current.ReviewIsStale);

        var changedLine = line with { Text = "周五前不提交接口评审。" };
        var transcriptChanged = Assert.Single(SessionEvidenceBuilder.Build(meta, [changedLine], notes, restored));
        Assert.Null(transcriptChanged.ReviewDecision);
        Assert.True(transcriptChanged.ReviewIsStale);

        var changedNotes = notes with
        {
            Sections =
            [
                notes.Sections[0] with
                {
                    Items = [notes.Sections[0].Items[0] with { Text = "下周再提交接口评审" }]
                }
            ]
        };
        var claimChanged = Assert.Single(SessionEvidenceBuilder.Build(meta, [line], changedNotes, restored));
        Assert.Null(claimChanged.ReviewDecision);
        Assert.True(claimChanged.ReviewIsStale);
    }

    [Fact]
    public void UnmatchedQuoteCanShowNearbySpeechButRemainsUnverified()
    {
        var line = Line(Start, 120, "本周先完成接口评审。");
        var payload = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("决定", 120, [new OutlinePoint("正式决定周五发布", 120)])]));
        var summary = new SessionSummary
        {
            Title = "会议",
            Markdown = "",
            GeneratedAt = Start,
            Structured = payload
        };

        var evidence = SessionEvidenceBuilder.Build(Meta(Start), [line], null, summary);
        var decision = evidence.FirstOrDefault(reference => reference.Claim == "正式决定周五发布");

        Assert.NotNull(decision);
        Assert.Equal(SessionEvidenceSupport.TimeOnly, decision!.Support);
        Assert.Equal(line.Id, decision.LineId);
        Assert.Equal(line.Text, decision.SourceText);
    }

    [Fact]
    public void StructuredEvidenceItemCanBeEditedAndKeepsItsSiblingNodes()
    {
        var payload = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("路线图",
                Points: [new OutlinePoint("先完成缓存", 24, [new TimedText("补充监控", 30)])])]));
        var summary = new SessionSummary
        {
            Title = "会议",
            Markdown = "",
            GeneratedAt = Start,
            Structured = payload
        };
        var meta = Meta(Start);
        var references = SessionEvidenceBuilder.Build(meta, [], null, summary);
        var child = references.First(reference => reference.Claim == "补充监控");
        var target = child.StructuredTarget!;

        Assert.Equal(NotesFormat.Outline, target.Format);
        Assert.Equal("section:0:point:0:child:0", target.Path);
        Assert.False(string.IsNullOrEmpty(target.PayloadRevision));

        var edited = SessionNotesEditor.UpdateSummary(summary, target, "补充告警监控")!;
        Assert.Equal("先完成缓存", edited.Structured!.Outline!.Sections[0].Points[0].Text);
        Assert.Equal("补充告警监控", edited.Structured!.Outline!.Sections[0].Points[0].Children[0].Text);
        Assert.Contains(SessionEvidenceBuilder.Build(meta, [], null, edited),
            reference => reference.Claim == "补充告警监控");

        var outline = payload.Outline!;
        var regeneratedPayload = payload with
        {
            Outline = outline with
            {
                Sections =
                [
                    outline.Sections[0] with
                    {
                        Points =
                        [
                            outline.Sections[0].Points[0] with
                            {
                                Children = [new TimedText("新生成的行动项", 30)]
                            }
                        ]
                    }
                ]
            }
        };
        var regenerated = summary with { Structured = regeneratedPayload };
        Assert.Null(SessionNotesEditor.UpdateSummary(regenerated, target, "旧编辑不能覆盖新内容"));
        Assert.Equal("新生成的行动项",
            regenerated.Structured!.Outline!.Sections[0].Points[0].Children[0].Text);

        var unchanged = summary;
        Assert.Null(SessionNotesEditor.UpdateSummary(unchanged,
            new SessionStructuredNotesTarget(NotesFormat.Outline, "section:9", target.PayloadRevision), "不存在"));
    }
}
