using NextThought.Core;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E22: the layered glossary (global + series + material terms) that recognition, prompts and the
/// transcript find all share, and the merge bookkeeping behind "已加入 N 项".
/// </summary>
public sealed class GlossaryMaterialTermsTests
{
    private static Glossary Global() => new([
        new GlossaryEntry("Kubernetes", variants: ["K8s", "库伯尼提斯"]),
        new GlossaryEntry("缓存一致性")
    ]);

    private static Glossary Series() => new([
        new GlossaryEntry("订单链路", GlossaryKind.Term, ["定单链路"]),
        new GlossaryEntry("陈老师", GlossaryKind.Person)
    ]);

    [Fact]
    public void LayeredTableSearchesCanonicalAndRecordedMisspellings()
    {
        var layered = Glossary.Layered(Global(), Series());

        Assert.True(layered.Search("订单链路").FirstMatch("我们聊了定单链路的回滚") is { IsMisheard: true } hit
                    && hit.Form == "定单链路");
        Assert.True(layered.Search("Kubernetes").FirstMatch("k8s 的发布顺序") is { IsMisheard: true });
        Assert.True(layered.Search("陈老师").FirstMatch("陈老师课上讲的") is { IsMisheard: false });
        // the typed query itself always wins over another spelling
        var typed = layered.Search("定单链路").FirstMatch("定单链路");
        Assert.Equal("定单链路", typed!.Form);
    }

    [Fact]
    public void MaterialTermsJoinWithoutDuplicatingKnownEntries()
    {
        var withTerms = GlossaryMaterialTerms.WithTerms(Global(), ["Kubernetes", "吞吐量", "P99"]);

        var keys = withTerms.Entries.Select(entry => Glossary.StrictKey(entry.Canonical)).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains(withTerms.Entries, entry => entry.Canonical == "吞吐量");
        Assert.Contains(withTerms.Entries, entry => entry.Canonical == "P99");
        Assert.Single(withTerms.Entries, entry => Glossary.StrictKey(entry.Canonical) == "kubernetes");
    }

    [Fact]
    public void ShortTermsAreIgnored()
    {
        Assert.Equal(Global().Entries.Count, GlossaryMaterialTerms.WithTerms(Global(), ["A", " ", ""]).Entries.Count);
    }

    [Fact]
    public void PromptSectionCarriesSeriesAndMaterialTermsWithinTheLimit()
    {
        var effective = GlossaryMaterialTerms.WithTerms(Glossary.Layered(Global(), Series()), ["吞吐量"]);
        var prompt = effective.PromptSection(1500);

        Assert.Contains("Kubernetes", prompt);
        Assert.Contains("订单链路", prompt);
        Assert.Contains("吞吐量", prompt);
        Assert.True(prompt.Length <= 1500);
    }

    [Fact]
    public void MergeCountCountsEntriesAndVariants()
    {
        var before = Global();
        var after = before.Merging(new Glossary([
            new GlossaryEntry("订单链路", variants: ["定单链路", "订单链"]),
            new GlossaryEntry("吞吐量")
        ]));

        Assert.Equal(4, GlossaryMerge.Count(before, after)); // entry + 2 variants + entry
        Assert.Equal(GlossaryMerge.Size(after), GlossaryMerge.Size(before) + 4);
        Assert.Equal(0, GlossaryMerge.Count(after, after));
    }

    [Fact]
    public void PastedTextMergesIntoTheCurrentTable()
    {
        var incoming = Glossary.ParseText(
            "订单链路 | 定单链路, 订单链 | 术语 | 交易系统\n陈老师 | | 人名\n", GlossaryKind.Term).Normalized();
        var before = Global();
        var merged = before.Merging(incoming);

        Assert.Contains(merged.Entries, entry => entry.Canonical == "订单链路"
                                                 && entry.Variants.Contains("定单链路"));
        Assert.Contains(merged.Entries, entry => entry.Canonical == "陈老师" && entry.Kind == GlossaryKind.Person);
        Assert.True(GlossaryMerge.Count(before, merged) >= 4);
    }
}
