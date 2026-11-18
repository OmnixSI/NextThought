using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for LaTeXMath (LaTeXMath.swift).</summary>
public sealed class LaTeXMathTests
{
    [Theory]
    [InlineData("x^2", "x²")]
    [InlineData("a_{ij}", "aᵢⱼ")]
    [InlineData("\\frac{a}{b}", "a/b")]
    [InlineData("\\frac{1}{2}", "½")]
    [InlineData("\\sqrt{x}", "√x")]
    [InlineData("\\sqrt[3]{x}", "³√x")]
    [InlineData("\\alpha + \\beta", "α + β")]
    [InlineData("a \\times b", "a × b")]
    [InlineData("\\sum_{i=1}^{n} i", "∑ᵢ₌₁ⁿ i")]
    public void FormulasBecomeUnicode(string latex, string expected)
    {
        Assert.Equal(expected, LaTeXMath.Unicode(latex));
    }

    [Fact]
    public void ScriptFallbacksKeepTheMeaning()
    {
        Assert.Equal("x^(ab?)", LaTeXMath.Unicode("x^{ab?}"));
        Assert.Equal("x^q", LaTeXMath.Unicode("x^q"));
        Assert.Equal("x_(ij?)", LaTeXMath.Unicode("x_{ij?}"));
    }

    [Fact]
    public void MatricesAndCasesBecomeShortLines()
    {
        Assert.Equal("(a b; c d)", LaTeXMath.Unicode("\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}"));
        Assert.Equal("{ x\u2003x>0\n{ -x\u2003x<0", LaTeXMath.Unicode("\\begin{cases} x & x>0 \\\\ -x & x<0 \\end{cases}"));
    }

    [Fact]
    public void InlineMathIsReplacedButMoneyCodeAndEscapesStay()
    {
        Assert.Equal("The answer is x².", LaTeXMath.ReplacingInlineMath("The answer is $x^2$."));
        Assert.Equal("It costs $5 and $10.", LaTeXMath.ReplacingInlineMath("It costs $5 and $10."));
        Assert.Equal("`$x^2$`", LaTeXMath.ReplacingInlineMath("`$x^2$`"));
        Assert.Equal("\\$5", LaTeXMath.ReplacingInlineMath("\\$5"));
        Assert.Equal("α", LaTeXMath.ReplacingInlineMath("\\(\\alpha\\)"));
    }

    [Fact]
    public void DisplayHelpersFindOpenersAndClosers()
    {
        var opening = LaTeXMath.DisplayOpening("$$x+1$$");
        Assert.NotNull(opening);
        Assert.Equal("$$", opening!.Value.Closer);
        Assert.Equal("x+1$$", opening.Value.Content);
        Assert.Null(LaTeXMath.DisplayOpening("x+1"));

        var closing = LaTeXMath.DisplayClosing("x+1$$ rest", "$$");
        Assert.Equal("x+1", closing!.Value.Before);
        Assert.Equal(" rest", closing.Value.After);
    }

    [Fact]
    public void MarkdownConvertsDisplayAndInlineFormulas()
    {
        Assert.Equal("½", LaTeXMath.Markdown("$$\\frac{1}{2}$$").Trim());
        var multiline = LaTeXMath.Markdown("$$\n\\sum_{i=1}^{n} i\n$$");
        Assert.Equal("∑ᵢ₌₁ⁿ i", multiline.Trim());
        Assert.Equal("The answer is x².", LaTeXMath.Markdown("The answer is $x^2$.").Trim());
    }

    [Fact]
    public void MarkdownLeavesCodeFencesAlone()
    {
        var input = "```\n$x^2$\n```";
        Assert.Equal(input, LaTeXMath.Markdown(input));
    }

    [Fact]
    public void ScriptTablesPairEveryKey()
    {
        Assert.Equal((66, 38), LaTeXMath.ScriptTableSizes);
    }

    [Fact]
    public void HalfWrittenFormulasStillFinish()
    {
        Assert.NotEmpty(LaTeXMath.Unicode("\\frac{1}{"));
        Assert.NotEmpty(LaTeXMath.Unicode("\\begin{cases} x &"));
        Assert.Equal("x²", LaTeXMath.Unicode("x^2 \\"));
    }
}
