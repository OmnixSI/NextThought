using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B14: translated from GlossaryTests.swift key, hotword and prompt rules.</summary>
public sealed class GlossaryParityTests
{
    private static GlossaryEntry Entry(string canonical, params string[] variants) =>
        new(canonical, GlossaryKind.Term, variants);

    [Fact]
    public void StrictAndLooseKeysFollowMac()
    {
        Assert.Equal("open ai", Glossary.StrictKey("Open AI "));
        Assert.Equal("open ai", Glossary.StrictKey("  open   AI"));
        Assert.Equal("open-ai", Glossary.StrictKey("Open-AI"));
        Assert.Equal(Glossary.LooseKey("Open AI"), Glossary.LooseKey("open-ai"));
        Assert.Equal(Glossary.LooseKey("Ｏｐｅｎ ＡＩ"), Glossary.LooseKey("openai"));
        Assert.NotEqual(Glossary.LooseKey("C++"), Glossary.LooseKey("C"));
        Assert.NotEqual(Glossary.LooseKey("C#"), Glossary.LooseKey("C"));
        Assert.Equal("openai", Glossary.LooseKey("Open AI"));
    }

    [Fact]
    public void HotwordsAreCanonicalOnlyMisheardFirstAndUsable()
    {
        var glossary = new Glossary(
        [
            Entry("Kubernetes", "K8s"),
            Entry("Redis"),
            Entry("Café"),
            Entry("A"),
            Entry("bad:word"),
            Entry("has,comma"),
            Entry("has　space"),
            Entry(new string('长', 41)),
            Entry("Kubernetes", "duplicate")
        ]);
        var words = glossary.Hotwords();
        Assert.Equal(["Kubernetes", "Redis", "Café", "has space"], words);
        Assert.Equal(4, words.Count);
        Assert.DoesNotContain("K8s", words);
        Assert.Equal(2, glossary.Hotwords(2).Count);
        Assert.Empty(glossary.Hotwords(0));
    }

    [Fact]
    public void HotwordStringKeepsTheUsersWordsFirstAndDeduplicates()
    {
        var glossary = new Glossary([Entry("Kubernetes", "K8s"), Entry("Redis")]);
        Assert.Equal("k8s,user,redis", glossary.HotwordString("K8S，user, redis", 3).ToLowerInvariant());
        Assert.Equal("own", glossary.HotwordString("own", 1));
    }

    [Fact]
    public void PromptSectionUsesTheMacOmissionMarker()
    {
        var glossary = new Glossary(Enumerable.Range(0, 40)
            .Select(index => Entry("术语" + index.ToString("D2") + new string('字', 20))).ToList());
        var prompt = glossary.PromptSection(200, english: false);
        Assert.Contains("（另有", prompt);
        Assert.Contains("条未列出）", prompt);
    }
}
