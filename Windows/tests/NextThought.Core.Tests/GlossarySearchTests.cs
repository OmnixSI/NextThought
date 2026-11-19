using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for glossary search expansion, misheard flags and merging.</summary>
public sealed class GlossarySearchTests
{
    private static Glossary Sample() => new Glossary([
        new GlossaryEntry("Kubernetes", variants: ["K8s"]),
        new GlossaryEntry("Open AI", variants: ["open-ai"])
    ]).Normalized();

    [Fact]
    public void ExactSearchListsEverySpellingQueryFirst()
    {
        var search = Sample().Search("Kubernetes");
        Assert.Equal("Kubernetes", search.Query);
        Assert.Equal(["Kubernetes", "K8s"], search.Forms);
        Assert.Equal(["Kubernetes", "K8s"], Sample().Expand("Kubernetes"));
    }

    [Fact]
    public void LooseKeysMatchSpacingVariants()
    {
        var search = Sample().Search("open ai");
        // Spellings that differ only in spacing/punctuation share a strict key, so the query stays alone…
        Assert.Contains("open ai", search.Forms);
        Assert.True(search.Matches("Open AI is a company"));
        // …while a variant with different letters is added.
        Assert.Contains("K8s", Sample().Search("Kubernetes").Forms);
    }

    [Fact]
    public void MisheardSpellingsAreFlaggedExceptTheTypedOne()
    {
        var search = Sample().Search("Kubernetes");
        var hit = search.FirstMatch("we deployed k8s last week");
        Assert.NotNull(hit);
        Assert.Equal("K8s", hit!.Form);
        Assert.True(hit.IsMisheard);

        var canonical = search.FirstMatch("Kubernetes is running");
        Assert.Equal("Kubernetes", canonical!.Form);
        Assert.False(canonical.IsMisheard);

        // The typed query wins even when it is itself a recorded variant.
        var typed = Sample().Search("k8s").FirstMatch("K8S cluster");
        Assert.Equal("k8s", typed!.Form);
        Assert.False(typed.IsMisheard);
    }

    [Fact]
    public void ContainedSpellingExpandsTheQuery()
    {
        var search = Sample().Search("k8s 部署");
        Assert.Contains("Kubernetes 部署", search.Forms);
        Assert.True(search.Matches("Kubernetes 部署"));
        Assert.False(search.Matches("nothing here"));
    }

    [Fact]
    public void ExpansionIsCappedAtThirtyTwoForms()
    {
        var variants = Enumerable.Range(2, 40).Select(index => "variant" + index).ToList();
        var glossary = new Glossary([new GlossaryEntry("term", variants: variants)]).Normalized();
        Assert.True(glossary.Search("term").Forms.Count <= 32);
    }

    [Fact]
    public void EntryMatchingUsesLooseKeys()
    {
        Assert.Equal("Open AI", Sample().EntryMatching("open ai")!.Canonical);
        Assert.Equal("Kubernetes", Sample().EntryMatching("k8s")!.Canonical);
        Assert.Null(Sample().EntryMatching("missing"));
        Assert.Null(Sample().EntryMatching("   "));
    }

    [Fact]
    public void MergingUnitesVariantsAndLayeredPrefersTheGlobalTable()
    {
        var global = new Glossary([new GlossaryEntry("Alpha", variants: ["A1"])]);
        var series = new Glossary([new GlossaryEntry("Alpha", variants: ["A2"]), new GlossaryEntry("Beta")]);
        var merged = global.Merging(series);
        Assert.Equal(2, merged.Entries.Count);
        var alpha = merged.EntryMatching("Alpha")!;
        Assert.Contains("A1", alpha.Variants);
        Assert.Contains("A2", alpha.Variants);
        Assert.Equal(2, Glossary.Layered(global, series).Entries.Count);
        Assert.Single(Glossary.Layered(global, null).Entries);
        Assert.Single(Glossary.Layered(global, Glossary.Empty).Entries);
    }

    [Fact]
    public void EmptyQueriesFindNothing()
    {
        var search = Sample().Search("   ");
        Assert.Empty(search.Forms);
        Assert.False(search.Matches("Kubernetes"));
        Assert.Empty(Sample().Expand(""));
    }

    [Fact]
    public void FullWidthTextStillMatches()
    {
        var search = Sample().Search("Kubernetes");
        Assert.True(search.Matches("Ｋｕｂｅｒｎｅｔｅｓ 集群"));
    }
}
