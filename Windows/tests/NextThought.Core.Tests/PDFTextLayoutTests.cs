using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for PDF text layout (furniture, joining, headings).</summary>
public sealed class PDFTextLayoutTests
{
    [Fact]
    public void FurnitureFindsRepeatedHeadersAndKeepsContent()
    {
        var pages = Enumerable.Range(1, 6)
            .Select(page => $"ACME 年报\n甲{page}\n乙{page}\n丙{page}\n丁{page}\n戊{page}\n第 {page} 页")
            .ToList();
        var furniture = PDFTextLayout.Furniture(pages);
        Assert.Contains("acme 年报", furniture);
        Assert.DoesNotContain("甲1", furniture);
        Assert.Empty(PDFTextLayout.Furniture(pages.Take(3).ToList()));
    }

    [Fact]
    public void ProseNeedsFullPages()
    {
        Assert.True(PDFTextLayout.IsProse([new string('字', 1300)]));
        Assert.False(PDFTextLayout.IsProse([new string('字', 100), new string('字', 100)]));
        Assert.False(PDFTextLayout.IsProse([]));
    }

    [Fact]
    public void PageNumbersAndFurnitureAreRemoved()
    {
        var furniture = new HashSet<string>(StringComparer.Ordinal) { "acme 年报 第 页" };
        var page = "ACME 年报 第 7 页\n摘要\n正文内容。\n- 7 -";
        var markdown = PDFTextLayout.Markdown(page, furniture, numberedHeadings: false);
        Assert.Contains("## 摘要", markdown);
        Assert.Contains("正文内容。", markdown);
        Assert.DoesNotContain("ACME", markdown);
        Assert.True(PDFTextLayout.IsPageNumber("Page 12"));
        Assert.True(PDFTextLayout.IsPageNumber("第 3 页"));
        Assert.True(PDFTextLayout.IsPageNumber("- 12 -"));
        Assert.False(PDFTextLayout.IsPageNumber("3.2 Research questions"));
    }

    [Fact]
    public void KeyIgnoresDigitsAndCollapsesSpaces()
    {
        Assert.Equal("report", PDFTextLayout.Key("Report 12 "));
        Assert.Equal("第 页", PDFTextLayout.Key("第 3 页"));
    }

    [Fact]
    public void WrappedLatinLinesJoinWithASpaceAndHyphensDisappear()
    {
        var body = string.Join("\n", Enumerable.Range(0, 6).Select(index => new string((char)('a' + index), 60)));
        var page = body + "\ninforma-\ntion follows here";
        var markdown = PDFTextLayout.Markdown(page, numberedHeadings: false);
        Assert.Contains("information follows here", markdown);
    }

    [Fact]
    public void ChineseLinesJoinWithoutASpace()
    {
        var body = string.Join("\n", new[] { '甲', '乙', '丙', '丁', '戊', '己' }.Select(character => new string(character, 60)));
        var markdown = PDFTextLayout.Markdown(body, numberedHeadings: false);
        Assert.True(markdown.Contains(new string('甲', 60) + new string('乙', 60)), markdown);
    }

    [Fact]
    public void NamedAndNumberedHeadingsAreMarked()
    {
        var body = string.Join("\n", Enumerable.Range(0, 6).Select(index => new string('X', 60)));
        var page = "Abstract\n" + body + "\n3.2 Research questions\n" + body;
        var markdown = PDFTextLayout.Markdown(page);
        Assert.Contains("## Abstract", markdown);
        Assert.Contains("### 3.2 Research questions", markdown);

        var withoutNumbers = PDFTextLayout.Markdown(page, numberedHeadings: false);
        Assert.Contains("## Abstract", withoutNumbers);
        Assert.DoesNotContain("### 3.2", withoutNumbers);
    }

    [Fact]
    public void JournalHeadingsFollowThePaperRules()
    {
        Assert.Equal((2, "II. SYSTEM MODEL"), PDFTextLayout.JournalHeading("II. SYSTEM MODEL", null));
        Assert.Equal((3, "A. Array and Channel Model"), PDFTextLayout.JournalHeading("A. Array and Channel Model", "The array"));
        Assert.Null(PDFTextLayout.JournalHeading("G. Marcus, and A. Keller, “Title”", null));
        Assert.Equal((2, "APPENDIX B"), PDFTextLayout.JournalHeading("APPENDIX B", null));
    }

    [Fact]
    public void LeaderDotsBecomeAnEllipsis()
    {
        var body = string.Join("\n", Enumerable.Range(0, 6).Select(index => new string('y', 60)));
        var markdown = PDFTextLayout.Markdown(body + "\n目录项 ........ 12", numberedHeadings: false);
        Assert.Contains("目录项 … 12", markdown);
    }
}
