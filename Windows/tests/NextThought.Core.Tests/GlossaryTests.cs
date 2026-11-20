using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for Glossary.swift.</summary>
public sealed class GlossaryTests
{
    private static Glossary Sample() => new Glossary([
        new GlossaryEntry("Kubernetes", GlossaryKind.Product, ["K8s", "库伯奈提斯"]),
        new GlossaryEntry("OpenAI", GlossaryKind.Product, ["open ai", "open-ai"]),
        new GlossaryEntry("GPT", GlossaryKind.Acronym, ["gpt4"]),
        new GlossaryEntry("C++", GlossaryKind.Term, ["cpp"]),
        new GlossaryEntry("C#", GlossaryKind.Term),
        new GlossaryEntry("残差", GlossaryKind.Term, ["残查"])
    ]);

    [Fact]
    public void LooseAndStrictKeysFoldWidthAndPunctuation()
    {
        Assert.Equal(Glossary.LooseKey("Open AI"), Glossary.LooseKey("open-ai"));
        Assert.Equal(Glossary.LooseKey("ＯｐｅｎＡＩ"), Glossary.LooseKey("openai"));
        Assert.NotEqual(Glossary.LooseKey("C++"), Glossary.LooseKey("C"));
        Assert.Equal(Glossary.LooseKey("C++"), Glossary.LooseKey("c++"));
        Assert.Equal("残差", Glossary.StrictKey("残差"));
    }

    [Fact]
    public void CanonicalizeFixesVariantsButKeepsCanonicalSpelling()
    {
        var glossary = Sample();
        Assert.Equal("用 Kubernetes 部署，残差很小", glossary.Canonicalize("用 k8s 部署，残查很小"));
        Assert.Equal("OpenAI 的模型", glossary.Canonicalize("open ai 的模型"));
        Assert.Equal("写 C++ 和 C# 都可以", glossary.Canonicalize("写 cpp 和 C# 都可以"));
        // A canonical spelling is protected, not rewritten.
        Assert.Equal("OpenAI Gym", glossary.Canonicalize("OpenAI Gym"));
    }

    [Fact]
    public void LatinBoundariesStopMatchesInsideWords()
    {
        var glossary = Sample();
        Assert.Equal("GPTs are here", glossary.Canonicalize("GPTs are here"));
        Assert.Equal("用 GPT 写", glossary.Canonicalize("用 gpt4 写").Replace("gpt4", "GPT4"));
        var detailed = glossary.CanonicalizeDetailed("这里的GPT很好用");
        Assert.Equal("这里的GPT很好用", detailed.Text);
    }

    [Fact]
    public void LongestSpellingWins()
    {
        var glossary = new Glossary([
            new GlossaryEntry("NextThought", GlossaryKind.Product, ["next", "next thought"])
        ]);
        // "next thought" is longer than "next", so it is tried first; both become the canonical form.
        Assert.Equal("NextThought 和 NextThought", glossary.Canonicalize("next thought 和 next"));
    }

    [Fact]
    public void CanonicalizeReportsWhatItChanged()
    {
        var detailed = Sample().CanonicalizeDetailed("k8s 和 K8s 都是 k8s");
        Assert.Equal(3, detailed.ReplacementCount);
        Assert.Contains(detailed.Replacements, item => item.From == "k8s" && item.To == "Kubernetes" && item.Count == 2);
        Assert.Equal("Kubernetes 和 Kubernetes 都是 Kubernetes", detailed.Text);
    }

    [Fact]
    public void HotwordsUseCanonicalSpellingsOnly()
    {
        var words = Sample().Hotwords();
        Assert.Contains("Kubernetes", words);
        Assert.DoesNotContain("K8s", words);
        Assert.DoesNotContain("库伯奈提斯", words);
    }

    [Fact]
    public void PromptSectionListsSpellingsAndNotes()
    {
        var glossary = new Glossary([new GlossaryEntry("NextThought", GlossaryKind.Product, ["next thought"], "应用名")]);
        var prompt = glossary.PromptSection();
        Assert.Contains("术语与人名对照", prompt);
        Assert.Contains("NextThought ← next thought：应用名", prompt);
        Assert.Equal("", Glossary.Empty.PromptSection());
    }


    [Fact]
    public void ExportTextRoundTripsMacFormat()
    {
        var glossary = new Glossary([
            new GlossaryEntry("Kubernetes", GlossaryKind.Product, ["K8s", "库伯奈提斯"]),
            new GlossaryEntry("张三", GlossaryKind.Person),
            new GlossaryEntry("OpenAI", GlossaryKind.Term, ["open ai"], "模型")
        ]);
        var text = GlossaryStore.ToText(glossary);
        Assert.Equal("Kubernetes | K8s, 库伯奈提斯\n张三 |  | 人名\nOpenAI | open ai | 术语 | 模型", text);
        var parsed = Glossary.ParseText(text);
        Assert.Equal(3, parsed.Entries.Count);
        Assert.Equal(["K8s", "库伯奈提斯"], parsed.Entries[0].Variants);
        Assert.Equal(GlossaryKind.Person, parsed.Entries[1].Kind);
        Assert.Equal("模型", parsed.Entries[2].Note);
    }

    [Fact]
    public void ParseTextReadsMacKindAndNoteColumns()
    {
        var text = "Kubernetes | K8s, kube | 产品 | 容器平台\n张三 | | 人名\nOpenAI | open ai | 术语";
        var glossary = Glossary.ParseText(text);
        var kubernetes = glossary.Entries.Single(entry => entry.Canonical == "Kubernetes");
        Assert.Equal(GlossaryKind.Product, kubernetes.Kind);
        Assert.Equal("容器平台", kubernetes.Note);
        Assert.Equal(["K8s", "kube"], kubernetes.Variants);
        Assert.Equal(GlossaryKind.Person, glossary.Entries.Single(entry => entry.Canonical == "张三").Kind);
        Assert.Equal(GlossaryKind.Term, glossary.Entries.Single(entry => entry.Canonical == "OpenAI").Kind);
    }

    [Fact]
    public void StoreReadsTheMacEnvelopeAndWritesItBack()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nt-glossary-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "Glossary"));
            var store = new GlossaryStore(directory);
            File.WriteAllText(store.FilePath,
                "{\"entries\":[{\"canonical\":\"Kubernetes\",\"kind\":\"product\",\"variants\":[\"K8s\"],\"note\":\"容器平台\"}]}");
            var loaded = store.Load();
            var entry = Assert.Single(loaded.Entries);
            Assert.Equal("Kubernetes", entry.Canonical);
            Assert.Equal(GlossaryKind.Product, entry.Kind);
            Assert.Equal("容器平台", entry.Note);
            store.Save(loaded);
            var text = File.ReadAllText(store.FilePath);
            Assert.Contains("\"entries\"", text);
            Assert.Equal("Kubernetes", new GlossaryStore(directory).Load().Entries.Single().Canonical);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void StoreWritesMacKindsAndKeepsExtendedKindsInSourceKind()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nt-glossary-kind-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new GlossaryStore(directory);
            store.Save(new Glossary([
                new GlossaryEntry("Kubernetes", GlossaryKind.Product),
                new GlossaryEntry("张三", GlossaryKind.Person)
            ]));
            var json = File.ReadAllText(store.FilePath);
            Assert.Contains("\"kind\": \"term\"", json);
            Assert.Contains("\"kind\": \"person\"", json);
            Assert.Contains("\"sourceKind\": \"product\"", json);
            var loaded = store.Load();
            Assert.Equal(GlossaryKind.Product, loaded.Entries.Single(entry => entry.Canonical == "Kubernetes").Kind);
            Assert.Equal(GlossaryKind.Person, loaded.Entries.Single(entry => entry.Canonical == "张三").Kind);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void StoreRoundTripsAndRecoversFromDamage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nt-glossary-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new GlossaryStore(directory);
            Assert.True(store.Load().IsEmpty);
            store.Save(Sample());
            var loaded = store.Load();
            Assert.Equal(6, loaded.Entries.Count);
            Assert.Equal("Kubernetes", loaded.Entries[0].Canonical);
            Assert.Equal(2, loaded.Entries[0].Variants.Count);
            Assert.Equal("Kubernetes | K8s, 库伯奈提斯", GlossaryStore.ToText(loaded).Split('\n')[0]);

            File.WriteAllText(store.FilePath, "{ not json");
            Assert.True(store.Load().IsEmpty);
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void PromptSectionCapsAt1500AndHasAnEnglishVersion()
    {
        var glossary = new Glossary([
            new GlossaryEntry("Kubernetes", GlossaryKind.Product, ["K8s"]),
            new GlossaryEntry("Alice", GlossaryKind.Person, ["alice"]),
            new GlossaryEntry("C#", GlossaryKind.Term, ["c sharp"])
        ]);
        var chinese = glossary.PromptSection();
        Assert.Contains("术语与人名对照", chinese);
        Assert.Contains("←", chinese);
        Assert.Contains("（人名）", chinese);
        var english = glossary.PromptSection(english: true);
        Assert.Contains("canonical spelling", english);
        Assert.Contains("(person)", english);
        Assert.DoesNotContain("未列出", english);

        var many = new Glossary(Enumerable.Range(0, 100)
            .Select(index => new GlossaryEntry(new string((char)('a' + index % 26), 60) + index, GlossaryKind.Term))
            .ToList());
        var capped = many.PromptSection();
        Assert.True(capped.Length <= 1500, capped.Length.ToString());
        Assert.Contains("未列出", capped);
    }
}
