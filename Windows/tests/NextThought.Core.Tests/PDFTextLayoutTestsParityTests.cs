using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated one-for-one from Tests/NextThoughtCoreTests/PDFTextLayoutTests.swift: which repeated
/// lines are page furniture, how prose becomes headings and whole sentences, and how journal
/// numbering is recognised. Tests that call <c>PDFTextLayout.heading</c> / <c>join</c> are skipped
/// because those methods are internal in NextThought.Core and the test project cannot see them.
/// </summary>
public sealed class PDFTextLayoutTestsParityTests
{
    private static readonly string Prose = string.Join("\n", new[]
    {
        "JANE DOE / RESEARCH REPORT 2026",
        "1 Introduction",
        "1.1 Application and technical background",
        "Artificial intelligence is being introduced to physical layer techniques across many wire-",
        "less systems: the signal processing functions that turn radio hardware into a usable link,",
        "including channel estimation and acquisition, beamforming and beam management, and",
        "more recently radio sensing. Neural networks have been trained to replace the receiver.",
        "This report studies three questions that follow from this observation, one per chapter,",
        "and gives preliminary results for the first two of them.",
        "A second paragraph starts here and also runs over the full width of the printed page,",
        "so that its lines are joined into one sentence again.",
        "4"
    });

    /// <summary>The page under test among others that share only the running header.</summary>
    private static List<string> Pages(int count) =>
        new[] { Prose }
            .Concat(Enumerable.Range(2, count - 1).Select(page =>
                $"JANE DOE / RESEARCH REPORT 2026\nPage {page} has its own text about topic number {page}.\n{page}"))
            .ToList();

    [Fact]
    public void RunningHeaderIsFurnitureWhateverThePageNumber()
    {
        var furniture = PDFTextLayout.Furniture(Pages(6));
        Assert.Contains(PDFTextLayout.Key("JANE DOE / RESEARCH REPORT 2026"), furniture);
        // Three pages are too few to tell a header from a coincidence.
        Assert.Empty(PDFTextLayout.Furniture(Pages(3)));
    }

    [Fact]
    public void ContentThatRepeatsIsNotFurniture()
    {
        // Every page is made of the same kind of line (a log, a table): nothing may be dropped.
        var pages = Enumerable.Range(1, 8)
            .Select(page => string.Join("\n", Enumerable.Range(0, 12)
                .Select(line => $"Research page {page}, line {line}: alpha beta, evidence and next steps.")))
            .ToList();
        var furniture = PDFTextLayout.Furniture(pages);
        Assert.Empty(furniture);
        Assert.Contains("Research page 4, line 11", PDFTextLayout.Markdown(pages[3], furniture));
        // Rows of numbers look alike once the digits are ignored.
        var tables = Enumerable.Range(1, 8)
            .Select(page => $"Results {page}\n12.5 45.1 0.92\n13.5 46.1 0.93\nUnique text for page {page} follows here.")
            .ToList();
        Assert.DoesNotContain(PDFTextLayout.Key("12.5 45.1 0.92"), PDFTextLayout.Furniture(tables));
        // And a page is never mostly removed, whatever was flagged.
        Assert.Contains("Header", PDFTextLayout.Markdown("Header\nHeader two\nBody",
            new HashSet<string> { PDFTextLayout.Key("Header"), PDFTextLayout.Key("Header two") }));
    }

    [Fact]
    public void ProseBecomesHeadingsAndWholeSentences()
    {
        var text = PDFTextLayout.Markdown(Prose, PDFTextLayout.Furniture(Pages(6)));
        Assert.DoesNotContain("JANE DOE", text);
        Assert.Contains("## 1 Introduction", text);
        Assert.Contains("### 1.1 Application and technical background", text);
        // The word broken at the margin is whole again, and the sentence is on one line.
        Assert.Contains("across many wireless systems: the signal processing functions", text);
        Assert.False(text.EndsWith("4", StringComparison.Ordinal));
        var blocks = DocumentStructure.Parse(text, new DocumentLocator { Page = 4 });
        Assert.Equal(["1 Introduction", "1.1 Application and technical background"], blocks[^1].Locator.HeadingPath);
    }

    [Fact]
    public void SlideLikePagesKeepTheirLines()
    {
        const string slide = "Scenario\nA sub-6 GHz-aided mmWave site\nTwo co-sited arrays\n"
                             + "3.5 GHz fully digital, 28 GHz hybrid;\ninstalled with a yaw.";
        Assert.Equal(slide, PDFTextLayout.Markdown(slide));
    }

    [Fact(Skip = "missing API: PDFTextLayout.Heading (internal in NextThought.Core; the test assembly cannot call it)")]
    public void OnlyRealTitlesBecomeHeadings()
    {
        // Source expectations (LaTeXMathTests/PDFTextLayoutTests.swift):
        //   heading("3.2 Research questions", next: "The questions are") != nil
        //   heading("Abstract", next: nil)?.level == 2
        //   heading("参考文献", next: nil) != nil
        //   heading("3.2 Research questions … 11", next: nil) == nil
        //   heading("5 GHz was used in all of the experiments.", next: nil) == nil
        //   heading("3 12.5 45.1 0.92", next: nil) == nil
        //   heading("28 GHz paths are easily blocked", next: "and reflected") == nil
        //   heading("2026 Annual Review", next: nil) == nil
    }

    [Fact]
    public void JournalNumberingIsRecognised()
    {
        // PDFTextLayout.heading delegates journal-numbered lines to JournalHeading, which is public.
        Assert.Equal(2, PDFTextLayout.JournalHeading("II. SYSTEM MODEL AND PROBLEM FORMULATION",
            "A. Array and Channel Model")!.Value.Level);
        Assert.Equal(3, PDFTextLayout.JournalHeading("A. Array and Channel Model", "The base station has")!.Value.Level);
        Assert.Equal(2, PDFTextLayout.JournalHeading("APPENDIX B", null)!.Value.Level);
        // "REFERENCES" is one of the well-known titles, so it is a level-2 heading on its own.
        Assert.Equal("## REFERENCES", PDFTextLayout.Markdown("REFERENCES"));
        // An author in the reference list, an initial in running text.
        Assert.Null(PDFTextLayout.JournalHeading(
            "G. Marcus, and A. Keller, “Sionna RT: Differentiable ray tracing for", null));
        Assert.Null(PDFTextLayout.JournalHeading(
            "H. Yang is with the University of Technology Sydney, Sydney, NSW,", null));
        Assert.Null(PDFTextLayout.JournalHeading("I. Smith", null));
    }

    [Fact(Skip = "missing API: PDFTextLayout.Join (internal in NextThought.Core; the test assembly cannot call it)")]
    public void ChineseLinesJoinWithoutSpaces()
    {
        // Source expectations (PDFTextLayoutTests.swift):
        //   join("这一页看部署条件越差，我们的收", "益越大。") == "这一页看部署条件越差，我们的收益越大。"
        //   join("learned from", "service pilots") == "learned from service pilots"
        //   isPageNumber("12") && isPageNumber("Page 3 of 20") && isPageNumber("- 7 -")
        //   !isPageNumber("2026 results")
    }
}
