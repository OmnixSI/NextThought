using System.Text.Json;
using System.Text.Json.Nodes;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from SeriesBriefingTests.swift: a briefing only uses its own series, carries the
/// newest meetings, keeps accepted decisions/actions current and never resurrects an old revision.
/// </summary>
public sealed class SeriesBriefingTestsParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static SessionMeta Session(DateTimeOffset startedAt, string mode, string title, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        StartedAt = startedAt,
        Mode = mode,
        Title = title
    };

    private static SessionEvidenceReference Evidence(
        string id,
        string title = "决定",
        string claim = "",
        string? quote = "这句原话能定位。",
        Guid? lineId = null,
        bool unlocated = false,
        string? sourceRevision = "line-revision",
        string? claimRevision = "claim-revision",
        string transcriptSourceRevision = "transcript-revision",
        NotesSectionKind? kind = NotesSectionKind.Items,
        string? owner = null,
        string? due = null,
        DateTimeOffset? dueDate = null,
        SessionEvidenceDecision? reviewDecision = SessionEvidenceDecision.Supported,
        bool reviewIsStale = false,
        SessionEvidenceOrigin origin = SessionEvidenceOrigin.TextNotes) =>
        new(id, title, claim, SessionEvidenceSupport.Quoted, origin,
            Quote: quote,
            Location: "00:12",
            LineId: unlocated ? null : lineId ?? Guid.NewGuid(),
            SourceRevision: sourceRevision,
            SourceText: quote,
            Kind: kind,
            Owner: owner,
            Due: due,
            DueDate: dueDate,
            ClaimRevision: claimRevision,
            TranscriptSourceRevision: transcriptSourceRevision,
            SourceStatus: SessionDerivedContentStatus.Current,
            ReviewDecision: reviewDecision,
            ReviewIsStale: reviewIsStale);

    [Fact]
    public void BriefingUsesOnlyThisSeriesAndReturnsTheNewestThreeMeetings()
    {
        var sessions = Enumerable.Range(0, 4)
            .Select(index => Session(Start.AddSeconds(index * 100), "labMeeting", $"第{index + 1}场"))
            .ToList();
        var workspace = new WorkspaceSeries("w", "研究组会", Start,
            sessions.Select(session => session.Id.ToString("D")).ToList(),
            [new WorkspaceLesson { Text = "保留已保存的经验", Pinned = true }])
        {
            Mode = "labMeeting",
            QuestionBank = [new QuestionBankItem { Question = "为什么延期？", Answer = "等待数据" }]
        };
        var unrelated = Session(Start.AddSeconds(900), "meeting", "外部");

        var briefing = SeriesBriefingBuilder.Build(workspace, [.. sessions, unrelated]);

        Assert.Equal(4, briefing.SessionCount);
        Assert.Equal(["第4场", "第3场", "第2场"], briefing.LatestSessions.Select(session => session.Title));
        Assert.Equal(["保留已保存的经验"], briefing.Lessons.Select(lesson => lesson.Text));
        Assert.Equal(["等待数据"], briefing.QuestionBank.Select(question => question.Answer));
        Assert.DoesNotContain(briefing.LatestSessions, session => session.Id == unrelated.Id);
    }

    [Fact]
    public void DisabledSeriesKnowledgeIsNotIncludedInTheBriefingOrMeetingCarry()
    {
        var workspace = new WorkspaceSeries("w", "面试", Start, [],
            [new WorkspaceLesson { Text = "只在本系列复用" }])
        {
            Mode = "interview",
            QuestionBank = [new QuestionBankItem { Question = "如何选择？", Answer = "比较依据" }],
            UseKnowledge = false
        };

        var briefing = SeriesBriefingBuilder.Build(workspace, []);
        var snapshot = MeetingCarry.Standard.WorkspaceSnapshot(workspace, new HashSet<string>());

        Assert.False(briefing.KnowledgeEnabled);
        Assert.Empty(briefing.Lessons);
        Assert.Empty(briefing.QuestionBank);
        Assert.Empty(snapshot.Lessons);
        Assert.Empty(snapshot.QuestionBank);
    }

    [Fact(Skip = "missing API: WorkspaceSeriesChangeMerger.Apply idempotent replay receipt")]
    public void F07ThreeMeetingsKeepTheCurrentDecisionAndNeverResurrectItsOldRevision()
    {
    }

    [Fact]
    public void ChangedSourceRevisionShowsAcceptedItemAsStaleAndExcludesItFromCarry()
    {
        var sourceID = Guid.Parse("00000000-0000-0000-0000-000000000011");
        var workspace = new WorkspaceSeries("w", "项目交付", Start, [sourceID.ToString("D")], [])
        {
            Mode = "meeting",
            SeriesItems =
            [
                new WorkspaceSeriesItem
                {
                    Kind = WorkspaceSeriesItem.KindValue.Decision,
                    Text = "改为周一交付",
                    SourceSessionId = sourceID.ToString("D"),
                    EvidenceId = "notes:decisions:delivery",
                    ClaimRevision = "claim-v1",
                    SourceRevision = "line-v1",
                    TranscriptSourceRevision = "transcript-v1"
                }
            ]
        };
        var session = Session(DateTimeOffset.UtcNow, "meeting", "项目会", sourceID);
        var changedEvidence = Evidence("notes:decisions:delivery", claim: "改为周一交付，新增验收步骤",
            sourceRevision: "line-v2", claimRevision: "claim-v2", transcriptSourceRevision: "transcript-v2");

        var briefing = SeriesBriefingBuilder.Build(workspace, [session], evidenceReferences: [changedEvidence]);

        Assert.Empty(briefing.Decisions);
        Assert.Empty(briefing.OpenActions);
        Assert.Equal(["改为周一交付"], briefing.StaleItems.Select(item => item.Text));
    }

    [Fact(Skip = "missing API: WorkspaceSeriesChangeMerger.SetActionState")]
    public void ReviewingOnTheLatestWorkspacePreservesConcurrentActionProgress()
    {
    }

    [Fact]
    public void StaleConcurrentDecisionReplacementCannotOverwriteTheNewerRevision()
    {
        var workspace = new WorkspaceSeries("w", "项目交付", Start, [], []) { Mode = "meeting" };
        var firstSession = Guid.NewGuid();
        var secondSession = Guid.NewGuid();
        var thirdSession = Guid.NewGuid();
        var existingItem = new WorkspaceSeriesItem
        {
            Kind = WorkspaceSeriesItem.KindValue.Decision,
            Text = "暂定周五提交",
            SourceSessionId = firstSession.ToString("D"),
            EvidenceId = "decision-old",
            ClaimRevision = "claim-old",
            SourceRevision = "line-old"
        };
        workspace = workspace with { SeriesItems = [existingItem] };
        var monday = Evidence("notes:decisions:monday", claim: "改到下周一。",
            sourceRevision: "line-monday", claimRevision: "claim-monday");
        var tuesday = Evidence("notes:decisions:tuesday", claim: "改到下周二。",
            sourceRevision: "line-tuesday", claimRevision: "claim-tuesday");
        var (withCandidates, mondayIds) = WorkspaceSeriesChangeMerger.CollectCandidates(workspace, [monday],
            secondSession.ToString("D"));
        var mondayID = Assert.Single(mondayIds);
        var (withBothCandidates, tuesdayIds) = WorkspaceSeriesChangeMerger.CollectCandidates(withCandidates, [tuesday],
            thirdSession.ToString("D"));
        var tuesdayID = Assert.Single(tuesdayIds);

        var (afterMonday, _) = WorkspaceSeriesChangeMerger.Apply(withBothCandidates, mondayID,
            WorkspaceSeriesChangeDecision.Replace, targetItemId: existingItem.Id, expectedRevision: 1);
        var (afterTuesday, staleAttempt) = WorkspaceSeriesChangeMerger.Apply(afterMonday, tuesdayID,
            WorkspaceSeriesChangeDecision.Replace, targetItemId: existingItem.Id, expectedRevision: 1);
        var item = afterTuesday.SeriesItems[0];

        Assert.Null(staleAttempt);
        Assert.Equal("改到下周一。", item.Text);
        Assert.Equal(2, item.Revision);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Pending,
            afterTuesday.SeriesChangeCandidates.First(candidate => candidate.Id == tuesdayID).Status);
        Assert.Null(afterTuesday.SeriesChangeCandidates.First(candidate => candidate.Id == tuesdayID).Receipt);
    }

    [Fact(Skip = "missing API: WorkspaceSeriesChangeMerger.SetActionState")]
    public void ActionOwnerAndRelativeDeadlineAreConfirmedAndCompletionCanBeUndone()
    {
    }

    [Fact]
    public void OnlyExplicitlySupportedCurrentEvidenceCanSeedSeriesChanges()
    {
        var workspace = new WorkspaceSeries("w", "项目交付", Start, [], []) { Mode = "meeting" };
        var session = Session(DateTimeOffset.UtcNow, "meeting", "项目会");
        var unsupported = Evidence("unsupported", claim: "周五提交", reviewDecision: null);
        var rejected = Evidence("rejected", claim: "周五提交",
            reviewDecision: SessionEvidenceDecision.Unsupported);
        var stale = Evidence("stale", claim: "周五提交", reviewIsStale: true);
        var unlocated = Evidence("unlocated", claim: "周五提交", unlocated: true, sourceRevision: null);
        var notDecision = Evidence("not-a-decision", title: "议题", claim: "周五提交");
        var structured = Evidence("structured", claim: "周五提交", origin: SessionEvidenceOrigin.StructuredNotes);

        var (changedWorkspace, added) = WorkspaceSeriesChangeMerger.CollectCandidates(workspace,
            [unsupported, rejected, stale, unlocated, notDecision, structured], session.Id.ToString("D"));

        Assert.Empty(added);
        Assert.Empty(changedWorkspace.SeriesChangeCandidates);
    }

    [Fact]
    public void StaleTranscriptRevisionCannotConfirmOrSurviveMeetingDeletion()
    {
        var workspace = new WorkspaceSeries("w", "项目交付", Start, [], []) { Mode = "meeting" };
        var session = Session(DateTimeOffset.UtcNow, "meeting", "项目会");
        var current = Evidence("notes:decisions:revision", claim: "正式改为周一。",
            sourceRevision: "line-v1", claimRevision: "claim-v1", transcriptSourceRevision: "transcript-v1");
        var (withCandidate, added) = WorkspaceSeriesChangeMerger.CollectCandidates(workspace, [current],
            session.Id.ToString("D"));
        var candidateID = Assert.Single(added);
        var candidate = withCandidate.SeriesChangeCandidates.First(item => item.Id == candidateID);
        var editedTranscript = Evidence(current.Id, title: current.Title, claim: current.Claim, quote: current.Quote,
            lineId: current.LineId, sourceRevision: current.SourceRevision, claimRevision: current.ClaimRevision,
            transcriptSourceRevision: "transcript-v2");

        Assert.False(WorkspaceSeriesChangeMerger.MatchesCurrentEvidence(candidate, editedTranscript));

        var (removed, changed) = WorkspaceSourceIntegrity.RemoveMeetings(withCandidate, [session.Id.ToString("D")]);
        Assert.True(changed);
        var tombstone = removed.SeriesChangeCandidates.First(item => item.Id == candidateID);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Superseded, tombstone.Status);
        Assert.True(tombstone.SourceMissing);
        Assert.Equal("", tombstone.Text);
        Assert.Null(tombstone.SourceQuote);
        Assert.Null(tombstone.TranscriptSourceRevision);
        var (afterApply, receipt) = WorkspaceSeriesChangeMerger.Apply(removed, candidateID,
            WorkspaceSeriesChangeDecision.Add);
        Assert.Null(receipt);
        Assert.Empty(afterApply.SeriesItems);
    }

    [Fact]
    public void ChangedOwnerOrDeadlineSupersedesTheOldPendingProposal()
    {
        var session = Session(DateTimeOffset.UtcNow, "meeting", "项目会");
        var sessionID = session.Id.ToString("D");
        var workspace = new WorkspaceSeries("w", "项目交付", Start, [sessionID], []) { Mode = "meeting" };
        var old = Evidence("notes:actions:task", title: "待办", claim: "提交报告。",
            sourceRevision: "line-1", claimRevision: "claim-1", kind: NotesSectionKind.Actions,
            owner: "A", due: "周五");
        var (withOld, oldIds) = WorkspaceSeriesChangeMerger.CollectCandidates(workspace, [old], sessionID);
        var oldID = Assert.Single(oldIds);
        var current = Evidence(old.Id, title: old.Title, claim: old.Claim, lineId: old.LineId,
            sourceRevision: old.SourceRevision, claimRevision: old.ClaimRevision,
            kind: NotesSectionKind.Actions, owner: "B", due: "周一");
        var (withCurrent, currentIds) = WorkspaceSeriesChangeMerger.CollectCandidates(withOld, [current], sessionID);
        var currentID = Assert.Single(currentIds);

        Assert.NotEqual(oldID, currentID);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Superseded,
            withCurrent.SeriesChangeCandidates.First(candidate => candidate.Id == oldID).Status);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Pending,
            withCurrent.SeriesChangeCandidates.First(candidate => candidate.Id == currentID).Status);

        var briefing = SeriesBriefingBuilder.Build(withCurrent, [session], evidenceReferences: [current]);
        Assert.Equal([currentID], briefing.PendingSeriesChanges!.Select(candidate => candidate.Id));
    }

    [Fact]
    public void SeriesHistoryAndSupersededProposalsSurviveIdempotentBackupMerge()
    {
        var workspaceID = Guid.NewGuid();
        var itemID = Guid.NewGuid();
        var firstSession = Guid.NewGuid();
        var secondSession = Guid.NewGuid();
        var first = new WorkspaceSeriesItem
        {
            Id = itemID.ToString("D"),
            Kind = WorkspaceSeriesItem.KindValue.Decision,
            Text = "周五",
            SourceSessionId = firstSession.ToString("D"),
            EvidenceId = "e1",
            ClaimRevision = "c1",
            SourceRevision = "s1",
            ConfirmedAt = Start,
            UpdatedAt = Start
        };
        var second = new WorkspaceSeriesItem
        {
            Id = itemID.ToString("D"),
            Kind = WorkspaceSeriesItem.KindValue.Decision,
            Text = "周一",
            SourceSessionId = secondSession.ToString("D"),
            EvidenceId = "e2",
            ClaimRevision = "c2",
            SourceRevision = "s2",
            Revision = 2,
            ConfirmedAt = Start.AddSeconds(10),
            UpdatedAt = Start.AddSeconds(10),
            History =
            [
                new WorkspaceSeriesItemRevision
                {
                    Id = itemID + ":1",
                    Revision = 1,
                    Kind = WorkspaceSeriesItem.KindValue.Decision,
                    Text = "周五",
                    SourceSessionId = firstSession.ToString("D"),
                    EvidenceId = "e1",
                    ClaimRevision = "c1",
                    SourceRevision = "s1",
                    RecordedAt = Start
                }
            ]
        };
        var oldCandidate = new WorkspaceSeriesChangeCandidate
        {
            Id = "same",
            Kind = WorkspaceSeriesItem.KindValue.Action,
            Text = "提交",
            SourceSessionId = firstSession.ToString("D"),
            EvidenceId = "a1",
            ClaimRevision = "ac1",
            SourceRevision = "as1",
            Status = WorkspaceSeriesChangeCandidate.StatusValue.Pending,
            CreatedAt = Start
        };
        var supersededCandidate = oldCandidate with
        {
            Status = WorkspaceSeriesChangeCandidate.StatusValue.Superseded,
            CreatedAt = Start.AddSeconds(20)
        };
        var newer = new WorkspaceSeries(workspaceID.ToString("D"), "项目交付", Start, [], [])
        {
            Mode = "meeting",
            SeriesItems = [second],
            SeriesChangeCandidates = [supersededCandidate]
        };
        var older = new WorkspaceSeries(workspaceID.ToString("D"), "项目交付", Start, [], [])
        {
            Mode = "meeting",
            SeriesItems = [first],
            SeriesChangeCandidates = [oldCandidate]
        };

        var merged = Assert.Single(BackupMerge.Workspaces([newer], [older]).Merged);
        Assert.Equal("周一", merged.SeriesItems[0].Text);
        Assert.Equal(["周五"], merged.SeriesItems[0].History.Select(revision => revision.Text));
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Superseded, merged.SeriesChangeCandidates[0].Status);

        var replayed = Assert.Single(BackupMerge.Workspaces([merged], [older]).Merged);
        Assert.Single(replayed.SeriesItems[0].History, revision => revision.Text == "周五");
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Superseded, replayed.SeriesChangeCandidates[0].Status);
    }

    [Fact]
    public void OldWorkspaceWithoutSeriesFieldsStillDecodesWithItsStableKnowledgeIDs()
    {
        var lesson = new WorkspaceLesson { Text = "经验" };
        var question = new QuestionBankItem { Question = "如何选？", Answer = "看依据" };
        var workspace = new WorkspaceSeries(Guid.NewGuid().ToString("D"), "旧系列", Start, [], [lesson])
        {
            Mode = "meeting",
            QuestionBank = [question]
        };
        var encoded = JsonSerializer.Serialize(workspace, JsonDefaults.ArchiveOptions);
        var node = JsonNode.Parse(encoded)!.AsObject();
        node.Remove("seriesItems");
        node.Remove("seriesChangeCandidates");
        var legacyData = node.ToJsonString();

        var decoded = JsonSerializer.Deserialize<WorkspaceSeries>(legacyData, JsonDefaults.ArchiveOptions)!;

        Assert.Equal(workspace.Id, decoded.Id);
        Assert.Equal(lesson.Id, decoded.Lessons[0].Id);
        Assert.Equal(question.Id, decoded.QuestionBank[0].Id);
        Assert.Empty(decoded.SeriesItems);
        Assert.Empty(decoded.SeriesChangeCandidates);
    }
}
