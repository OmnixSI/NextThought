using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for archived-meeting lexical search (fields, hits, excerpts).</summary>
public sealed class SessionContentSearchTests
{
    private static GlossarySearch Terms(string query) => new Glossary([
        new GlossaryEntry("Kubernetes", variants: ["K8s"])
    ]).Normalized().Search(query);

    [Fact]
    public void DirectMatchReturnsKindExcerptAndLocation()
    {
        var fields = new[]
        {
            new SessionSearchField(SessionSearchFieldKind.Transcript, "我们上周把服务迁到了 Kubernetes 集群上。",
                Location: "05:20", Id: "line:1")
        };
        var hit = SessionContentSearch.FirstHit(fields, Terms("Kubernetes"));
        Assert.NotNull(hit);
        Assert.Equal(SessionSearchFieldKind.Transcript, hit!.Kind);
        Assert.Equal("05:20", hit.Location);
        Assert.Equal("Kubernetes", hit.MatchedForm);
        Assert.False(hit.IsMisheard);
        Assert.Equal("line:1", hit.Id);
        Assert.Contains("Kubernetes", hit.Excerpt);
    }

    [Fact]
    public void MatchingIgnoresCaseAndFullWidth()
    {
        var fields = new[] { new SessionSearchField(SessionSearchFieldKind.Transcript, "ＫＵＢＥＲＮＥＴＥＳ is running", Id: "l1") };
        Assert.NotNull(SessionContentSearch.FirstHit(fields, Terms("kubernetes")));
    }

    [Fact]
    public void AlternateTextProducesAnAlternateKind()
    {
        var fields = new[]
        {
            new SessionSearchField(SessionSearchFieldKind.CorrectedTranscript, "corrected line",
                AlternateText: "original k8s line", Id: "line:1", AlternateId: "line:1:original",
                AlternateKind: SessionSearchFieldKind.Transcript, IsStale: true,
                SourceStatus: SessionDerivedContentStatus.Stale)
        };
        var hit = SessionContentSearch.FirstHit(fields, Terms("Kubernetes"));
        Assert.NotNull(hit);
        Assert.Equal(SessionSearchFieldKind.Transcript, hit!.Kind);
        Assert.Equal("K8s", hit.MatchedForm);
        Assert.True(hit.IsMisheard);
        Assert.Equal("line:1:original", hit.Id);
        Assert.False(hit.IsStale);
        Assert.Null(hit.SourceStatus);
    }

    [Fact]
    public void HitsAreDeduplicatedById()
    {
        var fields = new[]
        {
            new SessionSearchField(SessionSearchFieldKind.Notes, "Kubernetes notes", Id: "same"),
            new SessionSearchField(SessionSearchFieldKind.Notes, "Kubernetes again", Id: "same"),
            new SessionSearchField(SessionSearchFieldKind.Notes, "another Kubernetes note", Id: "other"),
            new SessionSearchField(SessionSearchFieldKind.Notes, "no match", Id: "third")
        };
        var hits = SessionContentSearch.Hits(fields, Terms("Kubernetes"));
        Assert.Equal(2, hits.Count);
        Assert.Equal(["same", "other"], hits.Select(hit => hit.Id));
    }

    [Fact]
    public void FieldsWithoutIdsAreAllReturned()
    {
        var fields = new[]
        {
            new SessionSearchField(SessionSearchFieldKind.Question, "Kubernetes 怎么部署？"),
            new SessionSearchField(SessionSearchFieldKind.Answer, "用 Kubernetes 部署。")
        };
        Assert.Equal(2, SessionContentSearch.Hits(fields, Terms("Kubernetes")).Count);
    }

    [Fact]
    public void LongTextIsExcerptedAroundTheMatch()
    {
        var text = new string('前', 100) + "Kubernetes" + new string('后', 100);
        var hit = SessionContentSearch.FirstHit([new SessionSearchField(SessionSearchFieldKind.Notes, text, Id: "n")], Terms("Kubernetes"));
        Assert.NotNull(hit);
        Assert.StartsWith("…", hit!.Excerpt);
        Assert.EndsWith("…", hit.Excerpt);
        Assert.Contains("Kubernetes", hit.Excerpt);
        Assert.True(hit.Excerpt.Length <= 32 + "Kubernetes".Length + 64 + 2);
    }

    [Fact]
    public void ShortTextIsExcerptedFromTheStart()
    {
        var hit = SessionContentSearch.FirstHit([new SessionSearchField(SessionSearchFieldKind.Notes, "Kubernetes", Id: "n")], Terms("Kubernetes"));
        Assert.Equal("Kubernetes", hit!.Excerpt);
    }

    [Fact]
    public void SearchKeepsArchiveOrderAndOneHitPerMeeting()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var documents = new[]
        {
            new SessionSearchDocument(first, [new SessionSearchField(SessionSearchFieldKind.Notes, "Kubernetes", Id: "a")]),
            new SessionSearchDocument(second, [new SessionSearchField(SessionSearchFieldKind.Notes, "nothing", Id: "b")]),
            new SessionSearchDocument(third, [new SessionSearchField(SessionSearchFieldKind.Notes, "K8s", Id: "c")])
        };
        var results = SessionContentSearch.Search(documents, Terms("Kubernetes"));
        Assert.Equal([first, third], results.Select(result => result.SessionId));
    }

    [Fact]
    public void EmptyInputsFindNothing()
    {
        Assert.Empty(SessionContentSearch.Hits([], Terms("Kubernetes")));
        Assert.Null(SessionContentSearch.FirstHit([], Terms("Kubernetes")));
        Assert.Empty(SessionContentSearch.Search([], Terms("Kubernetes")));
        Assert.Empty(SessionContentSearch.Hits([new SessionSearchField(SessionSearchFieldKind.Notes, "text", Id: "x")], Terms("")));
    }
}
