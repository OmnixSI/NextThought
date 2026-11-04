import Foundation
import Testing
@testable import InterviewHackerCore

/// LaTeX formulas shown as Unicode text: what `$x^2$` and friends turn into, which `$` are money and
/// stay alone, and that broken (still streaming) input never crashes or hangs.
struct LaTeXMathTests {
    private func u(_ latex: String) -> String { LaTeXMath.unicode(latex) }

    // MARK: Symbols and scripts

    @Test func scriptTablesAreComplete() {
        #expect(LaTeXMath.scriptTableSizes.superscript == 66)
        #expect(LaTeXMath.scriptTableSizes.subscript == 38)
    }

    @Test func superscriptsAndSubscripts() {
        #expect(u("x^2") == "x²")
        #expect(u("x^{n+1}") == "xⁿ⁺¹")
        #expect(u("a_{ij}") == "aᵢⱼ")
        #expect(u("x_0") == "x₀")
        #expect(u("x^{-1}") == "x⁻¹")
        // Not every letter has a Unicode script form: fall back to ^( ).
        #expect(u("e^{i\\pi}") == "e^(iπ)")
        #expect(u("x_{\\max}") == "x_(max)")
        #expect(u("30^\\circ") == "30°")
    }

    @Test func greekAndOperators() {
        #expect(u("\\alpha + \\beta") == "α + β")
        #expect(u("a \\cdot b") == "a · b")
        #expect(u("x \\leq y") == "x ≤ y")
        // Spaces are added around relations when the source has none.
        #expect(u("x\\leq y") == "x ≤ y")
        #expect(u("A \\subseteq B \\cup C") == "A ⊆ B ∪ C")
        #expect(u("f: A \\to B") == "f: A → B")
        #expect(u("\\mathbb{R}^n") == "ℝⁿ")
        #expect(u("\\infty") == "∞")
    }

    @Test func fractionsAndRoots() {
        #expect(u("\\frac{1}{2}") == "½")
        #expect(u("\\frac{a}{b}") == "a/b")
        #expect(u("\\frac{x+1}{y}") == "(x+1)/y")
        #expect(u("\\frac{n(n+1)}{2}") == "n(n+1)/2")
        #expect(u("\\sqrt{x}") == "√x")
        #expect(u("\\sqrt{x+1}") == "√(x+1)")
        #expect(u("\\sqrt[3]{x}") == "³√x")
    }

    @Test func bigOperators() {
        #expect(u("\\sum_{i=1}^{n} i") == "∑ᵢ₌₁ⁿ i")
        #expect(u("\\int_0^1 x\\,dx") == "∫₀¹ x dx")
        #expect(u("\\lim_{x \\to 0} f(x)") == "lim_(x → 0) f(x)")
        #expect(u("\\sin x + \\log n") == "sin x + log n")
    }

    @Test func textAndAccents() {
        #expect(u("\\text{if } x > 0") == "if x > 0")
        #expect(u("\\hat{x}") == "x\u{0302}")
        #expect(u("\\left( \\frac{a}{b} \\right)") == "( a/b )")
        #expect(u("\\left. x \\right|") == "x |")
    }

    @Test func environments() {
        #expect(u("\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}") == "(a b; c d)")
        #expect(u("\\begin{cases} x & x>0 \\\\ -x & \\text{otherwise} \\end{cases}") == "{ x\u{2003}x>0\n{ -x\u{2003}otherwise")
        #expect(u("a = 1 \\\\ b = 2") == "a = 1\nb = 2")
        #expect(u("\\begin{aligned} a &= b \\\\ c &= d \\end{aligned}") == "a = b\nc = d")
    }

    // MARK: Broken input

    @Test func unfinishedFormulasStillConvert() {
        for broken in ["\\frac{a", "\\frac{a}{", "x^", "x_{", "\\", "{{{", "}}}", "\\begin{cases} x &", "\\begin",
                       "\\sqrt[", "\\left(", "a \\\\", "\\unknowncommand{x}", "$", "\\mathbb"] {
            _ = u(broken)
        }
        #expect(u("\\frac{a") == "a/")
        #expect(u("\\unknowncommand") == "unknowncommand")
        #expect(u("") == "")
    }

    // MARK: Finding formulas in text

    @Test func inlineFormulasAreReplaced() {
        #expect(LaTeXMath.replacingInlineMath(in: "设 $x^2$ 为正") == "设 x² 为正")
        #expect(LaTeXMath.replacingInlineMath(in: "令 \\(a_1\\) 等于 1") == "令 a₁ 等于 1")
        #expect(LaTeXMath.replacingInlineMath(in: "有 $$a+b$$ 这个式子") == "有 a+b 这个式子")
        #expect(LaTeXMath.replacingInlineMath(in: "$x$ 和 $y$") == "x 和 y")
        #expect(LaTeXMath.replacingInlineMath(in: "没有公式的一行") == "没有公式的一行")
    }

    @Test func moneyIsNotAFormula() {
        #expect(LaTeXMath.replacingInlineMath(in: "价格是 $5 和 $10") == "价格是 $5 和 $10")
        #expect(LaTeXMath.replacingInlineMath(in: "只有一个 $ 符号") == "只有一个 $ 符号")
        #expect(LaTeXMath.replacingInlineMath(in: "cost $5, then $10 more") == "cost $5, then $10 more")
        #expect(LaTeXMath.replacingInlineMath(in: "转义的 \\$5 不算") == "转义的 \\$5 不算")
    }

    @Test func codeSpansAreLeftAlone() {
        #expect(LaTeXMath.replacingInlineMath(in: "写成 `$x^2$` 即可") == "写成 `$x^2$` 即可")
        #expect(LaTeXMath.replacingInlineMath(in: "`未闭合 $x$") == "`未闭合 $x$")
    }

    @Test func markdownSyntaxInAFormulaIsEscaped() {
        #expect(LaTeXMath.replacingInlineMath(in: "$a*b$") == "a\\*b")
        #expect(LaTeXMath.replacingInlineMath(in: "$x_{\\max}$") == "x\\_(max)")
    }

    @Test func displayFormulaMarkers() {
        let dollars = LaTeXMath.displayOpening("$$x^2")
        #expect(dollars?.closer == "$$" && dollars?.rest == "x^2")
        let brackets = LaTeXMath.displayOpening("\\[")
        #expect(brackets?.closer == "\\]" && brackets?.rest == "")
        #expect(LaTeXMath.displayOpening("普通的一行") == nil)
        let closing = LaTeXMath.displayClosing(in: "a+b$$ 其中", closer: "$$")
        #expect(closing?.before == "a+b" && closing?.after == " 其中")
        #expect(LaTeXMath.displayClosing(in: "a+b", closer: "$$") == nil)
    }
}
