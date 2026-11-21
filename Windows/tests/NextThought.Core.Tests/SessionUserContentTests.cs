using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for user-authored content and its revision-checked store.</summary>
public sealed class SessionUserContentTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("nt-user-content-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(directory, true); } catch { }
    }

    private static TranscriptLine Line(Guid id, string text) => new() { Id = id, Text = text, Date = DateTimeOffset.UtcNow };

    [Fact]
    public void HasContentReflectsEveryKindOfEdit()
    {
        Assert.False(new SessionUserContent().HasContent);
        Assert.True((new SessionUserContent { PersonalNotes = "  " }).HasContent == false);
        Assert.True((new SessionUserContent { PersonalNotes = "笔记" }).HasContent);
        Assert.True((new SessionUserContent { EditedMarkdown = "# 改过" }).HasContent);
        Assert.True((new SessionUserContent { Highlights = [new SessionHighlight { LineId = Guid.NewGuid(), Quote = "原话" }] }).HasContent);
        Assert.True((new SessionUserContent
        {
            CorrectionDecisions = [new SessionCorrectionDecision(null, Guid.NewGuid(), "库伯奈提斯", "Kubernetes", false)]
        }).HasContent);
    }

    [Fact]
    public void CorrectionDecisionsOverrideGeneratedCorrections()
    {
        var lineId = Guid.NewGuid();
        var lines = new[] { Line(lineId, "我们用库伯奈提斯部署") };
        var generated = new[]
        {
            new TranscriptCorrection { Line = 0, LineId = lineId, Original = "库伯奈提斯", Corrected = "Kubernetes" }
        };
        var content = new SessionUserContent
        {
            CorrectionDecisions =
            [
                new SessionCorrectionDecision("decision", lineId, "库伯奈提斯", "K8s", reverted: true),
                new SessionCorrectionDecision(null, lineId, "部署", "上线", reverted: false),
                new SessionCorrectionDecision(null, Guid.NewGuid(), "不存在", "x", false),
                new SessionCorrectionDecision(null, lineId, "不在原话里", "y", false)
            ]
        };
        var result = content.Corrections(generated, lines);
        var overridden = Assert.Single(result, correction => correction.Original == "库伯奈提斯");
        Assert.Equal("K8s", overridden.Corrected);
        Assert.True(overridden.Reverted);
        Assert.Contains(result, correction => correction.Original == "部署" && correction.Line == 0);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void LegacyDecisionIdsAreStableAndDeterministic()
    {
        var lineId = Guid.NewGuid();
        var first = new SessionCorrectionDecision(null, lineId, "原", "改", false);
        var second = new SessionCorrectionDecision(null, lineId, "原", "改", false);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(36, first.Id.Length);
        Assert.True(Guid.TryParse(first.Id, out _));
    }

    [Fact]
    public void StoreRoundTripsAndIncrementsTheRevision()
    {
        var content = new SessionUserContent { PersonalNotes = "我的笔记", EditedMarkdown = "# 摘要" };
        var saved = SessionUserContentStore.Save(content, directory);
        Assert.Equal(1, saved.Revision);
        Assert.True(File.Exists(Path.Combine(directory, SessionUserContent.FileName)));
        var loaded = SessionUserContentStore.Load(directory);
        Assert.Equal("我的笔记", loaded.PersonalNotes);
        Assert.Equal("# 摘要", loaded.EditedMarkdown);
        Assert.Equal(1, loaded.Revision);

        var next = SessionUserContentStore.Save(loaded with { PersonalNotes = "第二版" }, directory);
        Assert.Equal(2, next.Revision);
        Assert.Equal("第二版", SessionUserContentStore.Load(directory).PersonalNotes);
    }

    [Fact]
    public void StaleDraftsAreRefused()
    {
        SessionUserContentStore.Save(new SessionUserContent { PersonalNotes = "第一版" }, directory);
        var stale = new SessionUserContent { PersonalNotes = "旧草稿" };
        var error = Assert.Throws<SessionUserContentStore.ChangedElsewhereException>(() => SessionUserContentStore.Save(stale, directory));
        Assert.Contains("你的草稿仍然保留", error.Message);
        Assert.Equal("第一版", SessionUserContentStore.Load(directory).PersonalNotes);
    }

    [Fact]
    public void MissingFileIsEmptyAndNewerSchemasAreRejected()
    {
        var empty = SessionUserContentStore.Load(Path.Combine(directory, "missing"));
        Assert.False(empty.HasContent);
        Assert.Equal(0, empty.Revision);

        Directory.CreateDirectory(Path.Combine(directory, "newer"));
        File.WriteAllText(Path.Combine(directory, "newer", SessionUserContent.FileName),
            """{"schemaVersion":2,"personalNotes":"x"}""");
        Assert.Throws<InvalidDataException>(() => SessionUserContentStore.Load(Path.Combine(directory, "newer")));
    }

    [Fact]
    public void DecisionsSurviveJsonWithoutAnId()
    {
        var lineId = Guid.NewGuid();
        var json = $$"""{"correctionDecisions":[{"lineId":"{{lineId}}","original":"库伯奈提斯","corrected":"Kubernetes","reverted":false}]}""";
        var path = Path.Combine(directory, SessionUserContent.FileName);
        File.WriteAllText(path, json);
        var loaded = SessionUserContentStore.Load(directory);
        var decision = Assert.Single(loaded.CorrectionDecisions);
        Assert.True(decision.Id.Length == 36);
        Assert.Equal("Kubernetes", decision.Corrected);
    }


    [Fact]
    public void LegacyCorrectionIdsUseTheMacTranscriptRevision()
    {
        var json = """{"lineID":"11111111-2222-3333-3333-444444444444","original":"缓存","corrected":"缓存"}""";
        var decision = System.Text.Json.JsonSerializer.Deserialize<SessionCorrectionDecision>(json, JsonDefaults.Options)!;
        Assert.Equal("98f8941a-7f83-b14a-a2f0-47d2791c5595", decision.Id);
    }

    [Fact]
    public void UserContentWritesTheMacLineIdKey()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new SessionUserContent
        {
            Highlights = [new SessionHighlight { LineId = Guid.NewGuid(), Quote = "quote" }],
            CorrectionDecisions = [new SessionCorrectionDecision("id", Guid.NewGuid(), "a", "b", false)]
        }, JsonDefaults.ArchiveOptions);
        Assert.Contains("\"lineID\"", json);
        Assert.DoesNotContain("\"lineId\"", json);
    }
}
