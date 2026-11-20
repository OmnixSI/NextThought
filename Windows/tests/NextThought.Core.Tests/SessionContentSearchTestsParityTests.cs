using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Translated from Tests/NextThoughtCoreTests/SessionContentSearchTests.swift.</summary>
public sealed class SessionContentSearchTestsParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    /// <summary>
    /// The C# port splits the Swift <c>SessionContentSearch.fields(meta:lines:questions:answers:summary:notes:)</c>
    /// into a projection builder plus the field readers. The structured payload is searched through
    /// the notes-evidence fields (as the Swift builder does), so its rendered Markdown copy from the
    /// projection is left out here instead of shadowing the precise locations.
    /// </summary>
    private static IReadOnlyList<SessionSearchField> FieldsFor(
        SessionMeta meta,
        IReadOnlyList<TranscriptLine> lines,
        IReadOnlyList<SessionQuestionEvent>? questions,
        IReadOnlyList<SessionProjectedAnswer>? answers,
        SessionProjectedSummary? summary,
        StructuredNotesDoc? notes,
        StructuredNotesPayload? structured = null)
    {
        var projection = SessionProjectionBuilder.Build(meta.Id, meta.StartedAt, lines, answers ?? [], summary,
            questions ?? [], corrections: notes?.Corrections, textNotes: notes);
        var fields = SessionContentSearch.Fields(projection).ToList();
        if (structured is not null)
        {
            var structuredId = "summary:" + meta.Id + ":structured:" + structured.Format.Raw();
            fields.RemoveAll(field => field.Id == structuredId);
            fields.AddRange(SessionContentSearch.Fields(projection, structured, lines,
                projection.TranscriptSourceRevision).Where(field =>
                field.Id != structuredId && !fields.Any(existing => existing.Id == field.Id)));
        }
        return fields;
    }

    [Fact]
    public void SearchesEveryMeetingIncludingThe201stAnd501st()
    {
        const string typedTerm = "needle-only-in-transcript";
        var terms = new Glossary().Search(typedTerm);
        var documents = Enumerable.Range(1, 501).Select(number => new SessionSearchDocument(Guid.NewGuid(),
            number == 201 || number == 501
                ? [new SessionSearchField(SessionSearchFieldKind.Transcript, $"会中原话：{typedTerm}-meeting-{number}",
                    Location: "12:34")]
                : [new SessionSearchField(SessionSearchFieldKind.Transcript, $"普通会议第 {number} 场")])).ToList();

        var results = SessionContentSearch.Search(documents, terms);

        Assert.Equal(2, results.Count);
        Assert.Contains("meeting-201", results[0].Hit.Excerpt);
        Assert.Equal("12:34", results[0].Hit.Location);
        Assert.Contains("meeting-501", results[1].Hit.Excerpt);
    }

    [Fact(Skip = "missing API: SessionSearchArchiveStamp")]
    public void LiveSessionUpdatesDoNotChangeTheArchiveSearchStamp()
    {
    }

    [Fact(Skip = "missing API: SessionSearchArchiveStamp")]
    public void SavedArchivedContentChangesTheArchiveSearchStamp()
    {
    }

    [Fact(Skip = "missing API: SessionSearchArchiveStamp")]
    public void FinalizedSessionStaysOutOfSearchUntilFinalTranslationsDrain()
    {
    }

    [Fact]
    public void SearchesOriginalCorrectedAndTranslatedTranscriptAsDistinctSources()
    {
        var line = new TranscriptLine
        {
            Date = Start.AddSeconds(81),
            Speaker = "remote",
            Text = "缓存鸡穿",
            Translation = new SessionTranslation(Guid.NewGuid(), 1, "source-hash",
                "zh-en", "cache penetration", "test", Start.AddSeconds(81))
        };
        var notes = new StructuredNotesDoc("general", "通用", "zh", "校对", [],
            Corrections: [new TranscriptCorrection { Line = 0, Original = "缓存鸡穿", Corrected = "缓存击穿" }]);
        var meta = new SessionMeta { StartedAt = Start, EndedAt = Start.AddSeconds(100), Mode = "meeting" };

        var fields = FieldsFor(meta, [line], [], [], null, notes);

        var original = SessionContentSearch.FirstHit(fields, new Glossary().Search("缓存鸡穿"));
        var corrected = SessionContentSearch.FirstHit(fields, new Glossary().Search("缓存击穿"));
        var translated = SessionContentSearch.FirstHit(fields, new Glossary().Search("cache penetration"));
        Assert.Equal(SessionSearchFieldKind.Transcript, original!.Kind);
        Assert.Equal($"line:{line.Id}:original", original.Id);
        Assert.Equal(line.Id, original.LineId);
        Assert.Equal(TranscriptRevisions.Of(line.Id, line.Text), original.SourceRevision);
        Assert.Equal(SessionSearchFieldKind.CorrectedTranscript, corrected!.Kind);
        Assert.Equal($"line:{line.Id}:corrected", corrected.Id);
        Assert.Equal(line.Id, corrected.LineId);
        Assert.Equal("01:21", corrected.Location);
        Assert.Equal(SessionSearchFieldKind.Translation, translated!.Kind);
        Assert.Equal($"line:{line.Id}:translation", translated.Id);
        Assert.Equal(line.Id, translated.LineId);
        Assert.True(translated.IsStale == true);
    }

    [Fact]
    public void AlternateTextHitUsesTheOriginalSourceIdentity()
    {
        var lineId = Guid.NewGuid();
        var field = new SessionSearchField(SessionSearchFieldKind.CorrectedTranscript, "王小明负责周一提交。",
            AlternateText: "王小明负责周五提交。", Id: "line:corrected",
            LineId: lineId, SourceRevision: "corrected-revision",
            AlternateId: "line:original", AlternateKind: SessionSearchFieldKind.Transcript,
            AlternateLineId: lineId, AlternateSourceRevision: "original-revision");

        var hit = SessionContentSearch.FirstHit([field], new Glossary().Search("周五提交"));

        Assert.Equal(SessionSearchFieldKind.Transcript, hit!.Kind);
        Assert.Equal("line:original", hit.Id);
        Assert.Equal(lineId, hit.LineId);
        Assert.Equal("original-revision", hit.SourceRevision);
        Assert.True(hit.Excerpt.Contains("周五提交"));
    }

    [Fact]
    public void ReturnsEveryMatchingSourceFieldForPerMeetingNavigation()
    {
        var terms = new Glossary().Search("发布计划");
        var first = new SessionSearchField(SessionSearchFieldKind.Transcript, "发布计划先等数据", Id: "line-1");
        var second = new SessionSearchField(SessionSearchFieldKind.Notes, "发布计划改到周一", Id: "note-1");

        var hits = SessionContentSearch.Hits([first, second], terms);

        Assert.Equal(2, hits.Count);
        Assert.Equal(["line-1", "note-1"], hits.Select(hit => hit.Id));
        Assert.Equal([SessionSearchFieldKind.Transcript, SessionSearchFieldKind.Notes], hits.Select(hit => hit.Kind));
    }

    [Fact]
    public void IncludesQuestionsAnswersAndStructuredNotes()
    {
        var meta = new SessionMeta { StartedAt = Start, Mode = "meeting" };
        var question = new SessionQuestionEvent
        {
            Date = Start.AddSeconds(30),
            Text = "什么叫一致性",
            Intent = "why",
            Score = 1,
            Disposition = "manual"
        };
        var answer = new SessionProjectedAnswer(Guid.NewGuid(), Start.AddSeconds(40), "怎么处理", "逐条检查蓝鲸缓存", "test");
        var notes = new StructuredNotesDoc("general", "通用", "zh", "纪要",
        [
            new NotesSectionResult("actions", "待办", NotesSectionKind.Actions, false,
            [
                new NotesItem { Text = "联系海鸥团队", Time = "02:15", Owner = "小王" }
            ])
        ]);
        var structured = new StructuredNotesPayload(NotesFormat.Outline,
            outline: new OutlineNotes([new OutlineSection("风险", 345, [new OutlinePoint("蜂鸟回滚路径", 350)])]));
        var summary = new SessionProjectedSummary("周会", "已确认缓存策略", Structured: structured,
            StructuredAll: [structured]);
        var fields = FieldsFor(meta, [], [question], [answer], summary, notes, structured);

        Assert.Equal(SessionSearchFieldKind.Question,
            SessionContentSearch.FirstHit(fields, new Glossary().Search("一致性"))!.Kind);
        Assert.Equal(SessionSearchFieldKind.Answer,
            SessionContentSearch.FirstHit(fields, new Glossary().Search("蓝鲸缓存"))!.Kind);
        Assert.Equal("02:15",
            SessionContentSearch.FirstHit(fields, new Glossary().Search("海鸥团队"))!.Location);
        Assert.Equal(SessionSearchFieldKind.Notes,
            SessionContentSearch.FirstHit(fields, new Glossary().Search("缓存策略"))!.Kind);
        Assert.Equal("05:50",
            SessionContentSearch.FirstHit(fields, new Glossary().Search("蜂鸟回滚"))!.Location);
    }

    [Fact(Skip = "missing API: SessionContentSearch.Fields(meta:lines:questions:answers:summary:notes:) nearest-transcript-line linking (SessionSearchField.lineID)")]
    public void TimedNoteAndStructuredSearchHitsNavigateToNearbyTranscriptLine()
    {
    }

    [Fact]
    public void NotesAndSummarySearchHitsPreserveStaleSourceStatus()
    {
        var meta = new SessionMeta { StartedAt = Start, EndedAt = Start.AddSeconds(30), Mode = "meeting" };
        var line = new TranscriptLine { Date = Start.AddSeconds(5), Speaker = "remote", Text = "当前会议内容" };
        var notes = new StructuredNotesDoc("general", "会议", "zh", "待核对纪要",
        [
            new NotesSectionResult("actions", "待办", NotesSectionKind.Items, false,
            [
                new NotesItem { Text = "旧版本待办词" }
            ])
        ], TranscriptSourceRevision: "older-transcript-revision");
        var summary = new SessionProjectedSummary("旧版总结", "旧版本摘要词")
        {
            SourceTranscriptRevision = "older-transcript-revision"
        };

        var fields = FieldsFor(meta, [line], [], [], summary, notes);
        var noteHit = SessionContentSearch.FirstHit(fields, new Glossary().Search("旧版本待办词"));
        var summaryHit = SessionContentSearch.FirstHit(fields, new Glossary().Search("旧版本摘要词"));

        Assert.Equal(SessionSearchFieldKind.Notes, noteHit!.Kind);
        Assert.Equal(SessionDerivedContentStatus.Stale, noteHit.SourceStatus);
        Assert.True(noteHit.IsStale == true);
        Assert.Equal(SessionSearchFieldKind.Notes, summaryHit!.Kind);
        Assert.Equal(SessionDerivedContentStatus.Stale, summaryHit.SourceStatus);
        Assert.True(summaryHit.IsStale == true);
    }

    [Fact(Skip = "missing API: SessionProjectionBuilder.structuredNotesRevision(StructuredNotes)")]
    public void EditedNotesMarkTheirPreviouslyRenderedSummaryAsStaleInSearch()
    {
    }

    [Fact]
    public void LegacyNoteAndSummarySearchHitsKeepUnknownFreshnessDistinctFromStale()
    {
        var meta = new SessionMeta { StartedAt = Start, Mode = "meeting" };
        var notes = new StructuredNotesDoc("general", "会议", "zh", "旧纪要",
        [
            new NotesSectionResult("decisions", "决定", NotesSectionKind.Items, false,
            [
                new NotesItem { Text = "未标记版本的决定" }
            ])
        ]);
        var summary = new SessionProjectedSummary("旧摘要", "未标记版本的摘要");
        var fields = FieldsFor(meta, [], [], [], summary, notes);

        var noteHit = SessionContentSearch.FirstHit(fields, new Glossary().Search("未标记版本的决定"));
        var summaryHit = SessionContentSearch.FirstHit(fields, new Glossary().Search("未标记版本的摘要"));

        Assert.Equal(SessionDerivedContentStatus.Unknown, noteHit!.SourceStatus);
        Assert.True(noteHit.IsStale == false);
        Assert.Equal(SessionDerivedContentStatus.Unknown, summaryHit!.SourceStatus);
        Assert.True(summaryHit.IsStale == false);
    }

    [Fact]
    public void GlossaryVariantsRemainSearchableAndMarkedAsMisheard()
    {
        var terms = new Glossary([new GlossaryEntry("Kubernetes", variants: ["K8s"])]).Search("Kubernetes");
        var hit = SessionContentSearch.FirstHit(
            [new SessionSearchField(SessionSearchFieldKind.Transcript, "准备把 K8s 放进集群")], terms);
        Assert.Equal("K8s", hit!.MatchedForm);
        Assert.True(hit.IsMisheard);
        Assert.True(hit.Excerpt.Contains("K8s"));
    }
}
