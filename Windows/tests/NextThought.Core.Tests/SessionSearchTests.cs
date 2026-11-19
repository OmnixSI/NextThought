using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for hit navigation, progress and the cacheless snapshot search.</summary>
public sealed class SessionSearchTests
{
    private static SessionSearchHit Hit(string id, SessionSearchFieldKind kind = SessionSearchFieldKind.Transcript,
        Guid? lineId = null, string form = "Kubernetes", bool misheard = false) =>
        new(kind, "…Kubernetes…", "12:00", form, misheard, id, lineId);

    [Fact]
    public void FromSessionIndexesTranscriptAnswersAndSummary()
    {
        var meta = new SessionMeta { Id = Guid.NewGuid(), Title = "会议", StartedAt = DateTimeOffset.UtcNow };
        var line = new TranscriptLine { Id = Guid.NewGuid(), Text = "Kubernetes 部署", Date = meta.StartedAt };
        var answer = new AnswerRecord
        {
            Id = Guid.NewGuid(), Date = meta.StartedAt, Question = "怎么部署？", Answer = "用 Kubernetes 部署"
        };
        var snapshot = SessionSearch.FromSession(new SessionData(meta, [line], [answer], "## 摘要\n总结内容"));
        Assert.Contains(snapshot.Fields, field => field.Kind == SessionSearchFieldKind.Transcript);
        Assert.Contains(snapshot.Fields, field => field.Kind == SessionSearchFieldKind.Answer);
        Assert.Contains(snapshot.Fields, field => field.Kind == SessionSearchFieldKind.Notes);
        Assert.True(snapshot.ByteCount > 0);

        var terms = new Glossary([new GlossaryEntry("Kubernetes")]).Normalized().Search("Kubernetes");
        var result = SessionSearch.Search([meta.Id], terms, new Dictionary<Guid, SessionSearchSnapshot> { [meta.Id] = snapshot });
        Assert.Single(result.Hits);
    }

    [Fact]
    public void FromSessionCarriesTheSummaryErrorAsAReadIssue()
    {
        var meta = new SessionMeta { Id = Guid.NewGuid(), Title = "会议", StartedAt = DateTimeOffset.UtcNow, SummaryError = "模型超时" };
        var snapshot = SessionSearch.FromSession(new SessionData(meta, [], [], ""));
        Assert.Empty(snapshot.Fields);
        Assert.Contains(snapshot.ReadIssues!, issue => issue.Contains("模型超时"));
    }

    [Fact]
    public void LabelsCoverKindsAndUserContent()
    {
        Assert.Equal("转写", Hit("a").Label());
        Assert.Equal("校对转写", Hit("a", SessionSearchFieldKind.CorrectedTranscript).Label());
        Assert.Equal("译文", Hit("a", SessionSearchFieldKind.Translation).Label());
        Assert.Equal("问题", Hit("a", SessionSearchFieldKind.Question).Label());
        Assert.Equal("会中回答", Hit("a", SessionSearchFieldKind.Answer).Label());
        Assert.Equal("纪要", Hit("a", SessionSearchFieldKind.Notes).Label());
        Assert.Equal("个人笔记", Hit("user:personal-notes", SessionSearchFieldKind.Notes).Label());
        Assert.Equal("重点标记", Hit("user:highlight:" + Guid.NewGuid(), SessionSearchFieldKind.Notes).Label());
    }

    [Fact]
    public void TargetsFollowTheSource()
    {
        var line = Guid.NewGuid();
        Assert.Equal(new SessionContentTarget.Transcript(line), Hit("x", lineId: line).Target());
        Assert.Equal(new SessionContentTarget.PersonalNotes(), Hit("user:personal-notes").Target());
        var highlight = Guid.NewGuid();
        Assert.Equal(new SessionContentTarget.Highlight(highlight), Hit("user:highlight:" + highlight).Target());
        var answer = Guid.NewGuid();
        Assert.Equal(new SessionContentTarget.Answer(answer), Hit("answer:" + answer).Target());
        var notes = Guid.NewGuid();
        Assert.Equal(new SessionContentTarget.Notes(notes), Hit("notes:" + notes).Target());
        Assert.Equal(new SessionContentTarget.Notes(null), Hit("other").Target());
    }

    [Fact]
    public void SnippetAndMisheardForm()
    {
        Assert.Equal("…Kubernetes…", Hit("a").Snippet());
        Assert.Null(Hit("a").MisheardForm());
        Assert.Equal("K8s", Hit("a", form: "K8s", misheard: true).MisheardForm());
    }

    [Fact]
    public void SnapshotByteCountIncludesAlternateAndId()
    {
        var snapshot = new SessionSearchSnapshot([
            new SessionSearchField(SessionSearchFieldKind.Transcript, "abc", AlternateText: "de", Id: "f"),
            new SessionSearchField(SessionSearchFieldKind.Notes, "g")
        ]);
        Assert.Equal(3 + 2 + 1 + 160 + 1 + 160, snapshot.ByteCount);
    }

    [Fact]
    public void SearchReportsHitsUnreadableAndProgress()
    {
        var found = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var broken = Guid.NewGuid();
        var terms = new Glossary([new GlossaryEntry("Kubernetes", variants: ["K8s"])]).Normalized().Search("Kubernetes");
        var snapshots = new Dictionary<Guid, SessionSearchSnapshot>
        {
            [found] = new([new SessionSearchField(SessionSearchFieldKind.Transcript, "Kubernetes 部署", Id: "l1")]),
            [broken] = new([new SessionSearchField(SessionSearchFieldKind.Notes, "no match", Id: "n")], ["notes.json 读取失败"])
        };
        var progressCalls = 0;
        var progress = SessionSearch.Search([found, missing, broken], terms, snapshots, progress: _ => progressCalls++);
        Assert.Equal(3, progress.Checked);
        Assert.Equal(3, progress.Total);
        Assert.Equal(2, progress.Unreadable);
        Assert.Single(progress.Hits);
        Assert.Single(progress.HitLists);
        Assert.Contains(missing, progress.Issues.Keys);
        Assert.Contains(broken, progress.Issues.Keys);
        Assert.Equal(1, progressCalls);
    }

    [Fact]
    public void PerSessionTermsOverrideTheSharedQuery()
    {
        var id = Guid.NewGuid();
        var shared = new Glossary([new GlossaryEntry("Kubernetes")]).Normalized().Search("Kubernetes");
        var own = new Glossary([new GlossaryEntry("Postgres", variants: ["PG"])]).Normalized().Search("Postgres");
        var snapshots = new Dictionary<Guid, SessionSearchSnapshot>
        {
            [id] = new([new SessionSearchField(SessionSearchFieldKind.Notes, "PG 调优", Id: "n")])
        };
        Assert.Empty(SessionSearch.Search([id], shared, snapshots).Hits);
        var result = SessionSearch.Search([id], shared, snapshots, new Dictionary<Guid, GlossarySearch> { [id] = own });
        Assert.Single(result.Hits);
        Assert.Equal("PG", result.Hits[id].MatchedForm);
    }

    [Fact]
    public void CancellationStopsBeforeStarting()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => SessionSearch.Search([], Glossary.Empty.Search("x"), new Dictionary<Guid, SessionSearchSnapshot>(), cancellationToken: cancellation.Token));
    }

    [Fact]
    public void EmptySearchReturnsEmptyProgress()
    {
        var progress = SessionSearch.Search([], Glossary.Empty.Search("x"), new Dictionary<Guid, SessionSearchSnapshot>());
        Assert.Equal(0, progress.Checked);
        Assert.Empty(progress.Hits);
    }
}
