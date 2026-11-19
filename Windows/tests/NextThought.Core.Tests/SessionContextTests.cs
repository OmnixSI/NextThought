using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the lexical index, manual questions, Office XML text and hybrid retrieval.</summary>
public sealed class SessionContextTests
{
    [Fact]
    public void ContextSnapshotKeepsUnknownMacFields()
    {
        const string json = "{\"mode\":\"client\",\"style\":\"concise\",\"preferences\":\"\",\"profile\":\"\",\"documents\":[],"
            + "\"previousSessionID\":\"11111111-1111-1111-1111-111111111111\","
            + "\"futureMode\":\"x\",\"futureNested\":{\"a\":1}}";
        var snapshot = System.Text.Json.JsonSerializer.Deserialize<SessionContextSnapshot>(json, NextThought.Core.JsonDefaults.Options)!;
        var saved = System.Text.Json.JsonSerializer.Serialize(snapshot, NextThought.Core.JsonDefaults.Options);
        Assert.Contains("\"previousSessionID\"", saved);
        Assert.DoesNotContain("previousSessionId", saved);
        Assert.Contains("\"futureMode\"", saved);
        Assert.Contains("\"futureNested\"", saved);
        Assert.Contains("\"mode\":\"client\"", saved);
    }

    private static MaterialDocument Document(string name, params DocumentBlock[] blocks) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = name,
        Blocks = blocks.ToList()
    };

    [Fact]
    public void LexicalIndexRanksCjkAndLatinTerms()
    {
        var chunks = ContextChunker.Chunks([
            Document("a.pdf", new DocumentBlock("p.1", "温度补偿的残差分析", 1)),
            Document("b.pdf", new DocumentBlock("p.1", "另一份完全无关的材料", 1))
        ]);
        var index = new SessionContextIndex(chunks);
        var orders = index.RankedOrders("温度补偿怎么做");
        Assert.NotEmpty(orders);
        Assert.Equal(0, orders[0]);
        Assert.Empty(index.RankedOrders(""));
        Assert.Empty(index.RankedOrders("explain the"));
        var excerpts = index.Retrieve("温度补偿", maximumCharacters: 25);
        Assert.NotEmpty(excerpts);
        Assert.True(excerpts.Sum(excerpt => excerpt.Text.Length) <= 25);
    }

    [Fact]
    public void LexicalTermsDropStopWordsAndUseCjkBigrams()
    {
        var terms = SessionContextIndex.Terms("Explain the cache 缓存 consistency");
        Assert.Contains("cache", terms);
        Assert.Contains("consistency", terms);
        Assert.Contains("缓存", terms);
        Assert.DoesNotContain("explain", terms);
        Assert.DoesNotContain("the", terms);
        Assert.Contains("缓", SessionContextIndex.Terms("缓"));
    }

    [Fact]
    public void ManualQuestionUsesTypedTextOrTheLatestTranscript()
    {
        Assert.Equal("why?", ManualQuestionBuilder.Make("  why?  ", "", []));
        Assert.Null(ManualQuestionBuilder.Make("", "", []));
        var made = ManualQuestionBuilder.Make("", "live", ["a", "b", "c", "d"]);
        Assert.NotNull(made);
        Assert.Contains("请根据以下最新转录", made!);
        Assert.EndsWith("c\nd\nlive", made);
    }

    [Fact]
    public void OfficeXmlTextExtractsParagraphsAndDecodesEntities()
    {
        const string pptx = "<p:sp><a:p><a:r><a:t>标题一</a:t></a:r></a:p><a:p><a:r><a:t>a &amp; b</a:t></a:r></a:p></p:sp>";
        Assert.Equal("标题一\na & b", OfficeXMLText.Extract(pptx));

        const string docx = "<w:p><w:r><w:t>Hello</w:t></w:r><w:r><w:t> world</w:t></w:r></w:p>";
        Assert.Equal("Hello world", OfficeXMLText.Extract(docx));

        const string xlsx = "<si><t>Alpha</t></si><si><t>Beta</t></si>";
        Assert.Equal("Alpha\nBeta", OfficeXMLText.Extract(xlsx));
        Assert.Equal("", OfficeXMLText.Extract("<root></root>"));
        Assert.Equal("<&\"'>&", OfficeXMLText.Decode("&lt;&amp;&quot;&apos;&gt;&amp;"));
    }

    [Fact]
    public void OfficePartNamesSortNaturally()
    {
        Assert.Equal(["slide1.xml", "slide2.xml", "slide10.xml"],
            OfficeXMLText.NumericOrder(["slide10.xml", "slide2.xml", "slide1.xml"]));
    }

    [Fact]
    public void HybridRetrievalHonoursExplicitPageRequests()
    {
        var documents = new[]
        {
            Document("a.pdf",
                new DocumentBlock("p.1", "温度补偿方法", 1),
                new DocumentBlock("p.2", "残差分析", 2))
        };
        var index = new HybridContextIndex(documents);
        var ranked = index.Retrieve("残差分析");
        Assert.Equal(2, ranked[0].Page);
        var explicitPage = index.Retrieve("第1页讲了什么");
        Assert.Equal(1, explicitPage[0].Page);
    }

    [Fact]
    public void HybridRetrievalHonoursNamedDocuments()
    {
        var documents = new[]
        {
            Document("paper.pdf", new DocumentBlock("p.1", "正文内容", 1)),
            Document("notes.pdf", new DocumentBlock("p.1", "另一份内容", 1))
        };
        var index = new HybridContextIndex(documents);
        var result = index.Retrieve("paper.pdf 讲了什么");
        Assert.Equal("paper.pdf", result[0].Source);
    }

    [Fact]
    public void LowSimilarityReturnsNothingAndExplicitPageWins()
    {
        var documents = new[]
        {
            Document("paper.pdf", new DocumentBlock("p.7", "cache consistency", 7))
        };
        var index = new HybridContextIndex(documents);
        var semantic = new SemanticIndex();
        semantic.SetVector([1f, 0f], index.Chunks[0].Fingerprint, 0);
        var hybrid = new HybridContextIndex(documents, semantic);
        Assert.Empty(hybrid.Retrieve("unrelated astronomy", queryVector: [0f, 1f]));
        var page = hybrid.Retrieve("paper.pdf 第7页", queryVector: [0f, 1f]);
        Assert.Equal(7, page[0].Page);
    }

    [Fact]
    public void HybridRetrievalFusesSemanticVectors()
    {
        var documents = new[]
        {
            Document("cats.pdf", new DocumentBlock("p.1", "cats and dogs", 1)),
            Document("physics.pdf", new DocumentBlock("p.1", "quantum physics", 2))
        };
        var index = new HybridContextIndex(documents);
        Assert.False(index.HasVectors);
        var semantic = new SemanticIndex();
        semantic.SetVector([1f, 0f], index.Chunks[0].Fingerprint, 0);
        var withVectors = new HybridContextIndex(documents, semantic);
        Assert.True(withVectors.HasVectors);
        var result = withVectors.Retrieve("动物", queryVector: [1f, 0f]);
        Assert.Equal("cats.pdf", result[0].Source);
    }
}
