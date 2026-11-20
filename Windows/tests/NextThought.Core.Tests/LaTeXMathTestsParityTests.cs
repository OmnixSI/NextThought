using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated one-for-one from Tests/NextThoughtCoreTests/LaTeXMathTests.swift: what
/// <c>$x^2$</c> and friends turn into, which <c>$</c> are money and stay alone, and that broken
/// (still streaming) input never crashes or hangs. Class name keeps the Swift file's "Tests".
/// </summary>
public sealed class LaTeXMathTestsParityTests
{
    private static string U(string latex) => LaTeXMath.Unicode(latex);

    // MARK: Symbols and scripts

    [Fact]
    public void ScriptTablesAreComplete()
    {
        Assert.Equal(66, LaTeXMath.ScriptTableSizes.Superscript);
        Assert.Equal(38, LaTeXMath.ScriptTableSizes.Subscript);
    }

    [Fact]
    public void SuperscriptsAndSubscripts()
    {
        Assert.Equal("x²", U("x^2"));
        Assert.Equal("xⁿ⁺¹", U("x^{n+1}"));
        Assert.Equal("aᵢⱼ", U("a_{ij}"));
        Assert.Equal("x₀", U("x_0"));
        Assert.Equal("x⁻¹", U("x^{-1}"));
        // Not every letter has a Unicode script form: fall back to ^( ).
        Assert.Equal("e^(iπ)", U("e^{i\\pi}"));
        Assert.Equal("xₘₐₓ", U("x_{\\max}"));
        Assert.Equal("x_(good)", U("x_{good}"));
        Assert.Equal("30°", U("30^\\circ"));
    }

    [Fact]
    public void GreekAndOperators()
    {
        Assert.Equal("α + β", U("\\alpha + \\beta"));
        Assert.Equal("a · b", U("a \\cdot b"));
        Assert.Equal("x ≤ y", U("x \\leq y"));
        // Spaces are added around relations when the source has none.
        Assert.Equal("x ≤ y", U("x\\leq y"));
        Assert.Equal("A ⊆ B ∪ C", U("A \\subseteq B \\cup C"));
        Assert.Equal("f: A → B", U("f: A \\to B"));
        Assert.Equal("ℝⁿ", U("\\mathbb{R}^n"));
        Assert.Equal("∞", U("\\infty"));
    }

    [Fact]
    public void FractionsAndRoots()
    {
        Assert.Equal("½", U("\\frac{1}{2}"));
        Assert.Equal("a/b", U("\\frac{a}{b}"));
        Assert.Equal("(x+1)/y", U("\\frac{x+1}{y}"));
        Assert.Equal("n(n+1)/2", U("\\frac{n(n+1)}{2}"));
        Assert.Equal("√x", U("\\sqrt{x}"));
        Assert.Equal("√(x+1)", U("\\sqrt{x+1}"));
        Assert.Equal("³√x", U("\\sqrt[3]{x}"));
    }

    [Fact]
    public void BigOperators()
    {
        Assert.Equal("∑ᵢ₌₁ⁿ i", U("\\sum_{i=1}^{n} i"));
        Assert.Equal("∫₀¹ x dx", U("\\int_0^1 x\\,dx"));
        Assert.Equal("lim_(x → 0) f(x)", U("\\lim_{x \\to 0} f(x)"));
        Assert.Equal("sin x + log n", U("\\sin x + \\log n"));
    }

    [Fact]
    public void TextAndAccents()
    {
        Assert.Equal("if x > 0", U("\\text{if } x > 0"));
        Assert.Equal("x\u0302", U("\\hat{x}"));
        Assert.Equal("( a/b )", U("\\left( \\frac{a}{b} \\right)"));
        Assert.Equal("x |", U("\\left. x \\right|"));
    }

    [Fact]
    public void Environments()
    {
        Assert.Equal("(a b; c d)", U("\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}"));
        Assert.Equal("{ x\u2003x>0\n{ -x\u2003otherwise",
            U("\\begin{cases} x & x>0 \\\\ -x & \\text{otherwise} \\end{cases}"));
        Assert.Equal("a = 1\nb = 2", U("a = 1 \\\\ b = 2"));
        Assert.Equal("a = b\nc = d", U("\\begin{aligned} a &= b \\\\ c &= d \\end{aligned}"));
    }

    // MARK: Broken input

    [Fact]
    public void UnfinishedFormulasStillConvert()
    {
        foreach (var broken in new[]
                 {
                     "\\frac{a", "\\frac{a}{", "x^", "x_{", "\\", "{{{", "}}}", "\\begin{cases} x &", "\\begin",
                     "\\sqrt[", "\\left(", "a \\\\", "\\unknowncommand{x}", "$", "\\mathbb"
                 })
        {
            _ = U(broken);
        }
        Assert.Equal("a/", U("\\frac{a"));
        Assert.Equal("unknowncommand", U("\\unknowncommand"));
        Assert.Equal("", U(""));
    }

    // MARK: Finding formulas in text

    [Fact]
    public void InlineFormulasAreReplaced()
    {
        Assert.Equal("设 x² 为正", LaTeXMath.ReplacingInlineMath("设 $x^2$ 为正"));
        Assert.Equal("令 a₁ 等于 1", LaTeXMath.ReplacingInlineMath("令 \\(a_1\\) 等于 1"));
        Assert.Equal("有 a+b 这个式子", LaTeXMath.ReplacingInlineMath("有 $$a+b$$ 这个式子"));
        Assert.Equal("x 和 y", LaTeXMath.ReplacingInlineMath("$x$ 和 $y$"));
        Assert.Equal("没有公式的一行", LaTeXMath.ReplacingInlineMath("没有公式的一行"));
    }

    [Fact]
    public void MoneyIsNotAFormula()
    {
        Assert.Equal("价格是 $5 和 $10", LaTeXMath.ReplacingInlineMath("价格是 $5 和 $10"));
        Assert.Equal("只有一个 $ 符号", LaTeXMath.ReplacingInlineMath("只有一个 $ 符号"));
        Assert.Equal("cost $5, then $10 more", LaTeXMath.ReplacingInlineMath("cost $5, then $10 more"));
        Assert.Equal("转义的 \\$5 不算", LaTeXMath.ReplacingInlineMath("转义的 \\$5 不算"));
    }

    [Fact]
    public void CodeSpansAreLeftAlone()
    {
        Assert.Equal("写成 `$x^2$` 即可", LaTeXMath.ReplacingInlineMath("写成 `$x^2$` 即可"));
        Assert.Equal("`未闭合 $x$", LaTeXMath.ReplacingInlineMath("`未闭合 $x$"));
    }

    [Fact]
    public void MarkdownSyntaxInAFormulaIsEscaped()
    {
        Assert.Equal("a\\*b", LaTeXMath.ReplacingInlineMath("$a*b$"));
        Assert.Equal("x\\_(good)", LaTeXMath.ReplacingInlineMath("$x_{good}$"));
    }

    [Fact]
    public void DisplayFormulaMarkers()
    {
        var dollars = LaTeXMath.DisplayOpening("$$x^2");
        Assert.NotNull(dollars);
        Assert.Equal("$$", dollars!.Value.Closer);
        Assert.Equal("x^2", dollars.Value.Content);

        var brackets = LaTeXMath.DisplayOpening("\\[");
        Assert.NotNull(brackets);
        Assert.Equal("\\]", brackets!.Value.Closer);
        Assert.Equal("", brackets.Value.Content);

        Assert.Null(LaTeXMath.DisplayOpening("普通的一行"));

        var closing = LaTeXMath.DisplayClosing("a+b$$ 其中", "$$");
        Assert.NotNull(closing);
        Assert.Equal("a+b", closing!.Value.Before);
        Assert.Equal(" 其中", closing.Value.After);

        Assert.Null(LaTeXMath.DisplayClosing("a+b", "$$"));
    }
}
