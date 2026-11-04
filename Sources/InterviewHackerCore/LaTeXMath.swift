import Foundation

// MARK: - Formulas in answers and notes
//
// Models write formulas as LaTeX (`$x^2$`, `$$\sum_{i=1}^n i$$`, `\(…\)`, `\[…\]`). The app shows
// text with plain SwiftUI `Text`, so a formula is turned into Unicode that reads well on its own:
// Greek letters and operators as symbols, `x^2` as `x²`, `a_{ij}` as `aᵢⱼ`, `\frac{a}{b}` as `a/b`,
// `\sqrt{x}` as `√x`, `\sum_{i=1}^{n}` as `∑ᵢ₌₁ⁿ`, matrices and `cases` as short lines. It is not
// a typesetter: stacked fractions and tall brackets are written on one line. Pure Foundation logic
// with no regular expressions, so it is tested on Linux. Input may be half-written (answers
// stream in), so every function accepts broken LaTeX and always finishes.
public enum LaTeXMath {
    // MARK: Entry points

    /// A LaTeX formula as Unicode text. Several rows (`\\`, `aligned`, `cases`) become several lines.
    public static func unicode(_ latex: String) -> String {
        var parser = Parser(Array(latex))
        return tidy(parser.sequence(closing: nil))
    }

    /// `text` with every inline formula (`$…$`, `$$…$$` within a line, `\(…\)`) replaced by its Unicode text,
    /// escaped so that Markdown shows it literally. Code spans are left alone, and `$` that is money
    /// ("$5 and $10") stays as it is.
    public static func replacingInlineMath(in text: String) -> String {
        guard text.contains("$") || text.contains("\\(") else { return text }
        let c = Array(text)
        var out = ""
        var i = 0
        while i < c.count {
            let ch = c[i]
            if ch == "`" {
                // A code span: copy up to its closing backtick.
                var j = i + 1
                while j < c.count, c[j] != "`" { j += 1 }
                let end = min(j + 1, c.count)
                out += String(c[i..<end])
                i = end
            } else if ch == "\\", i + 1 < c.count, c[i + 1] == "$" {
                out += "\\$"
                i += 2
            } else if ch == "\\", i + 1 < c.count, c[i + 1] == "(" {
                if let close = find(["\\", ")"], in: c, from: i + 2) {
                    out += escapeMarkdown(unicode(String(c[(i + 2)..<close])))
                    i = close + 2
                } else { out.append(ch); i += 1 }
            } else if ch == "$", i + 1 < c.count, c[i + 1] == "$" {
                if let close = find(["$", "$"], in: c, from: i + 2), close > i + 2 {
                    out += escapeMarkdown(unicode(String(c[(i + 2)..<close])))
                    i = close + 2
                } else { out.append(ch); i += 1 }
            } else if ch == "$", let close = closingDollar(in: c, openedAt: i) {
                out += escapeMarkdown(unicode(String(c[(i + 1)..<close])))
                i = close + 1
            } else {
                out.append(ch)
                i += 1
            }
        }
        return out
    }

    /// A line that opens a display formula (`$$` or `\[`): the closing marker and the rest of the line after the opener.
    public static func displayOpening(_ trimmedLine: String) -> (closer: String, rest: String)? {
        if trimmedLine.hasPrefix("$$") { return ("$$", String(trimmedLine.dropFirst(2))) }
        if trimmedLine.hasPrefix("\\[") { return ("\\]", String(trimmedLine.dropFirst(2))) }
        return nil
    }

    /// Where `closer` ends a display formula inside `text`: the formula part before it and what follows it.
    public static func displayClosing(in text: String, closer: String) -> (before: String, after: String)? {
        guard let range = text.range(of: closer) else { return nil }
        return (String(text[text.startIndex..<range.lowerBound]), String(text[range.upperBound...]))
    }

    // MARK: Finding formulas

    private static func find(_ pattern: [Character], in c: [Character], from start: Int) -> Int? {
        guard !pattern.isEmpty, c.count >= pattern.count, start <= c.count - pattern.count else { return nil }
        var i = start
        while i <= c.count - pattern.count {
            var matches = true
            for k in 0..<pattern.count where c[i + k] != pattern[k] { matches = false; break }
            if matches { return i }
            i += 1
        }
        return nil
    }

    /// The index of the `$` that closes a formula opened at `open`, or nil when this `$` is not an opener
    /// (money, a lone sign). The opener must be followed by a non-space; the closer must follow a non-space
    /// and must not be followed by a digit.
    private static func closingDollar(in c: [Character], openedAt open: Int) -> Int? {
        guard open + 1 < c.count, !c[open + 1].isWhitespace else { return nil }
        var j = open + 1
        while j < c.count {
            if c[j] == "\\" { j += 2; continue }
            if c[j] == "$" {
                let before = c[j - 1]
                let after: Character? = j + 1 < c.count ? c[j + 1] : nil
                if j > open + 1, !before.isWhitespace, !(after?.isNumber ?? false) { return j }
                return nil
            }
            j += 1
        }
        return nil
    }

    private static func escapeMarkdown(_ s: String) -> String {
        var out = ""
        for ch in s {
            if "\\*_`[]<>~|".contains(ch) { out.append("\\") }
            out.append(ch)
        }
        return out
    }

    // MARK: Tidying

    /// Single spaces, no spaces around line ends, no blank edges.
    static func tidy(_ s: String) -> String {
        var collapsed = ""
        var lastSpace = false
        for ch in s {
            if ch == " " {
                if !lastSpace { collapsed.append(ch) }
                lastSpace = true
            } else {
                collapsed.append(ch)
                lastSpace = false
            }
        }
        let lines = collapsed.split(separator: "\n", omittingEmptySubsequences: false).map { line in
            line.trimmingCharacters(in: CharacterSet(charactersIn: " "))
        }
        return lines.joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    // MARK: Tables

    private static func table(_ pairs: [(String, String)]) -> [String: String] {
        Dictionary(pairs, uniquingKeysWith: { first, _ in first })
    }

    /// Symbols that are written as they are.
    static let plainSymbols: [String: String] = table([
        ("alpha", "α"), ("beta", "β"), ("gamma", "γ"), ("delta", "δ"), ("epsilon", "ε"), ("varepsilon", "ε"),
        ("zeta", "ζ"), ("eta", "η"), ("theta", "θ"), ("vartheta", "ϑ"), ("iota", "ι"), ("kappa", "κ"),
        ("lambda", "λ"), ("mu", "μ"), ("nu", "ν"), ("xi", "ξ"), ("omicron", "ο"), ("pi", "π"), ("varpi", "ϖ"),
        ("rho", "ρ"), ("varrho", "ϱ"), ("sigma", "σ"), ("varsigma", "ς"), ("tau", "τ"), ("upsilon", "υ"),
        ("phi", "φ"), ("varphi", "φ"), ("chi", "χ"), ("psi", "ψ"), ("omega", "ω"),
        ("Gamma", "Γ"), ("Delta", "Δ"), ("Theta", "Θ"), ("Lambda", "Λ"), ("Xi", "Ξ"), ("Pi", "Π"),
        ("Sigma", "Σ"), ("Upsilon", "Υ"), ("Phi", "Φ"), ("Psi", "Ψ"), ("Omega", "Ω"),
        ("infty", "∞"), ("partial", "∂"), ("nabla", "∇"), ("forall", "∀"), ("exists", "∃"), ("nexists", "∄"),
        ("neg", "¬"), ("lnot", "¬"), ("emptyset", "∅"), ("varnothing", "∅"), ("ldots", "…"), ("dots", "…"),
        ("cdots", "⋯"), ("vdots", "⋮"), ("ddots", "⋱"), ("sum", "∑"), ("prod", "∏"), ("coprod", "∐"),
        ("int", "∫"), ("iint", "∬"), ("iiint", "∭"), ("oint", "∮"), ("bigcup", "⋃"), ("bigcap", "⋂"),
        ("degree", "°"), ("angle", "∠"), ("triangle", "△"), ("square", "□"), ("prime", "′"), ("hbar", "ℏ"),
        ("ell", "ℓ"), ("Re", "ℜ"), ("Im", "ℑ"), ("aleph", "ℵ"), ("wp", "℘"), ("imath", "ı"), ("jmath", "ȷ"),
        ("top", "⊤"), ("bot", "⊥"), ("therefore", "∴"), ("because", "∵"), ("pm", "±"), ("mp", "∓"),
        ("langle", "⟨"), ("rangle", "⟩"), ("lfloor", "⌊"), ("rfloor", "⌋"), ("lceil", "⌈"), ("rceil", "⌉"),
        ("lbrace", "{"), ("rbrace", "}"), ("vert", "|"), ("lvert", "|"), ("rvert", "|"),
        ("Vert", "‖"), ("lVert", "‖"), ("rVert", "‖"), ("uparrow", "↑"), ("downarrow", "↓"),
        ("{", "{"), ("}", "}"), ("%", "%"), ("$", "$"), ("&", "&"), ("#", "#"), ("_", "_"), ("|", "‖"),
        ("/", "/"), ("'", "′"), ("`", "`"), ("\"", "\""), (".", "."), ("=", "="), ("(", "("), (")", ")"),
        ("[", "["), ("]", "]"), ("-", ""), ("*", "")
    ])

    /// Operators, relations and arrows: written with a space on each side.
    static let spacedSymbols: [String: String] = table([
        ("times", "×"), ("div", "÷"), ("cdot", "·"), ("ast", "∗"), ("star", "⋆"), ("circ", "∘"),
        ("oplus", "⊕"), ("otimes", "⊗"), ("cup", "∪"), ("cap", "∩"), ("setminus", "∖"),
        ("wedge", "∧"), ("vee", "∨"), ("land", "∧"), ("lor", "∨"),
        ("leq", "≤"), ("le", "≤"), ("geq", "≥"), ("ge", "≥"), ("neq", "≠"), ("ne", "≠"),
        ("approx", "≈"), ("equiv", "≡"), ("sim", "∼"), ("simeq", "≃"), ("cong", "≅"), ("propto", "∝"),
        ("ll", "≪"), ("gg", "≫"), ("in", "∈"), ("notin", "∉"), ("ni", "∋"),
        ("subset", "⊂"), ("subseteq", "⊆"), ("supset", "⊃"), ("supseteq", "⊇"),
        ("to", "→"), ("rightarrow", "→"), ("leftarrow", "←"), ("gets", "←"), ("leftrightarrow", "↔"),
        ("Rightarrow", "⇒"), ("Leftarrow", "⇐"), ("Leftrightarrow", "⇔"), ("implies", "⟹"), ("iff", "⟺"),
        ("mapsto", "↦"), ("longrightarrow", "⟶"), ("perp", "⊥"), ("parallel", "∥"), ("mid", "∣"),
        ("nmid", "∤"), ("vdash", "⊢"), ("models", "⊨")
    ])

    /// Written as their name: `\sin x` → `sin x`.
    static let functionNames: Set<String> = [
        "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "coth",
        "log", "ln", "lg", "exp", "max", "min", "sup", "inf", "lim", "limsup", "liminf", "det", "dim", "ker",
        "gcd", "lcm", "deg", "arg", "Pr", "hom"
    ]

    /// Commands that keep only their argument.
    static let passThrough: Set<String> = [
        "text", "textbf", "textit", "textrm", "mathrm", "mathbf", "mathit", "mathsf", "mathtt", "mathcal",
        "mathscr", "mathfrak", "operatorname", "boldsymbol", "bm", "mbox", "rm", "bf", "it", "underline",
        "underbrace", "overbrace", "boxed", "cancel", "xcancel", "bcancel", "mathop", "mathrel", "mathbin"
    ]

    /// Commands that disappear (their argument too, where they take one).
    static let dropped: Set<String> = [
        "left", "right", "big", "Big", "bigg", "Bigg", "bigl", "bigr", "Bigl", "Bigr", "biggl", "biggr", "middle",
        "limits", "nolimits", "displaystyle", "textstyle", "scriptstyle", "scriptscriptstyle", "nonumber", "notag",
        "not", "allowbreak", "relax"
    ]
    static let droppedWithArgument: Set<String> = ["label", "tag", "phantom", "vphantom", "hphantom", "ensuremath", "hspace", "vspace", "mkern", "kern"]

    private static let combining: [String: String] = [
        "hat": "\u{0302}", "widehat": "\u{0302}", "bar": "\u{0304}", "overline": "\u{0305}", "vec": "\u{20D7}",
        "overrightarrow": "\u{20D7}", "tilde": "\u{0303}", "widetilde": "\u{0303}", "dot": "\u{0307}",
        "ddot": "\u{0308}", "check": "\u{030C}", "acute": "\u{0301}", "grave": "\u{0300}", "breve": "\u{0306}"
    ]

    private static let blackboard: [Character: String] = [
        "R": "ℝ", "N": "ℕ", "Z": "ℤ", "Q": "ℚ", "C": "ℂ", "P": "ℙ", "H": "ℍ", "E": "𝔼", "1": "𝟙"
    ]

    private static let fractions: [String: String] = [
        "1/2": "½", "1/3": "⅓", "2/3": "⅔", "1/4": "¼", "3/4": "¾", "1/5": "⅕", "2/5": "⅖", "3/5": "⅗",
        "4/5": "⅘", "1/6": "⅙", "5/6": "⅚", "1/8": "⅛", "3/8": "⅜", "5/8": "⅝", "7/8": "⅞"
    ]

    private static let superscripts: [Character: Character] = zip(
        Array("0123456789+-−=()abcdefghijklmnoprstuvwxyzABDEGHIJKLMNOPRTUVWβγδθφχ"),
        Array("⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁻⁼⁽⁾ᵃᵇᶜᵈᵉᶠᵍʰⁱʲᵏˡᵐⁿᵒᵖʳˢᵗᵘᵛʷˣʸᶻᴬᴮᴰᴱᴳᴴᴵᴶᴷᴸᴹᴺᴼᴾᴿᵀᵁⱽᵂᵝᵞᵟᶿᵠᵡ")
    ).reduce(into: [:]) { $0[$1.0] = $1.1 }

    private static let subscripts: [Character: Character] = zip(
        Array("0123456789+-−=()aehijklmnoprstuvxβγρφχ"),
        Array("₀₁₂₃₄₅₆₇₈₉₊₋₋₌₍₎ₐₑₕᵢⱼₖₗₘₙₒₚᵣₛₜᵤᵥₓᵦᵧᵨᵩᵪ")
    ).reduce(into: [:]) { $0[$1.0] = $1.1 }

    /// Both maps need to be as long as their key lists; the tests check this.
    static var scriptTableSizes: (superscript: Int, subscript: Int) { (superscripts.count, subscripts.count) }

    static func superscript(_ s: String) -> String {
        let t = s.filter { $0 != " " }
        if t.isEmpty { return "" }
        if t == "∘" { return "°" }
        if t == "′" || t == "″" { return t }
        let mapped = t.map { superscripts[$0] }
        if !mapped.contains(where: { $0 == nil }) { return String(mapped.compactMap { $0 }) }
        return t.count == 1 ? "^" + t : "^(" + s + ")"
    }

    static func subscripted(_ s: String) -> String {
        let t = s.filter { $0 != " " }
        if t.isEmpty { return "" }
        let mapped = t.map { subscripts[$0] }
        if !mapped.contains(where: { $0 == nil }) { return String(mapped.compactMap { $0 }) }
        return t.count == 1 ? "_" + t : "_(" + s + ")"
    }

    /// No operator or space at the top level, so it can stand beside `/` or `√` without brackets.
    static func isAtomic(_ s: String) -> Bool {
        if s.isEmpty { return true }
        var depth = 0
        for ch in s {
            if ch == "(" { depth += 1; continue }
            if ch == ")" { depth -= 1; if depth < 0 { return false }; continue }
            if depth > 0 { continue }
            if ch.isLetter || ch.isNumber || isCombining(ch) || ".′″∞°".contains(ch) { continue }
            return false
        }
        return depth == 0
    }

    private static func isCombining(_ ch: Character) -> Bool {
        ch.unicodeScalars.contains { scalar in
            switch scalar.properties.generalCategory {
            case .nonspacingMark, .spacingMark, .enclosingMark: return true
            default: return false
            }
        }
    }

    static func wrapped(_ s: String) -> String { isAtomic(s) ? s : "(" + s + ")" }

    static func fraction(_ numerator: String, _ denominator: String) -> String {
        let a = numerator.trimmingCharacters(in: .whitespaces), b = denominator.trimmingCharacters(in: .whitespaces)
        if let vulgar = fractions[a + "/" + b] { return vulgar }
        return wrapped(a) + "/" + wrapped(b)
    }

    // MARK: Parser

    private struct Parser {
        let c: [Character]
        var i = 0

        init(_ chars: [Character]) { c = chars }

        /// Everything up to the matching `closing` character (consumed), or to the end.
        mutating func sequence(closing: Character?) -> String {
            var out = ""
            while i < c.count {
                let ch = c[i]
                if ch == "}" {
                    i += 1
                    if closing == "}" { return out }
                    continue
                }
                if ch == "]", closing == "]" { i += 1; return out }
                switch ch {
                case "{":
                    i += 1
                    out += sequence(closing: "}")
                case "^":
                    i += 1
                    out += LaTeXMath.superscript(scriptArgument())
                case "_":
                    i += 1
                    out += LaTeXMath.subscripted(scriptArgument())
                case "\\":
                    out += command()
                case "\n", "\t", "\r":
                    out.append(" "); i += 1
                case "~":
                    out += "\u{00A0}"; i += 1
                case "'":
                    out += "′"; i += 1
                case "$":
                    i += 1
                case "&":
                    out.append(" "); i += 1
                default:
                    out.append(ch); i += 1
                }
            }
            return out
        }

        mutating func skipSpaces() {
            while i < c.count, c[i] == " " || c[i] == "\n" || c[i] == "\t" { i += 1 }
        }

        /// The argument of `^`, `_` or a command: a `{group}`, one command, or one character.
        mutating func argument() -> String {
            skipSpaces()
            guard i < c.count else { return "" }
            if c[i] == "{" {
                i += 1
                return sequence(closing: "}")
            }
            if c[i] == "\\" { return command() }
            let ch = c[i]
            i += 1
            return String(ch)
        }

        mutating func scriptArgument() -> String { argument() }

        /// `[…]` right after a command (the index of a root), if there is one.
        mutating func optionalArgument() -> String? {
            skipSpaces()
            guard i < c.count, c[i] == "[" else { return nil }
            i += 1
            return sequence(closing: "]")
        }

        /// Raw text of a `{…}` group (braces matched, nothing converted).
        mutating func rawGroup() -> String {
            skipSpaces()
            guard i < c.count, c[i] == "{" else { return "" }
            var depth = 0
            var out = ""
            while i < c.count {
                let ch = c[i]
                i += 1
                if ch == "{" { depth += 1; if depth == 1 { continue } }
                if ch == "}" { depth -= 1; if depth == 0 { break } }
                out.append(ch)
            }
            return out
        }

        mutating func command() -> String {
            i += 1   // the backslash
            guard i < c.count else { return "" }
            var name = ""
            if c[i].isASCII, c[i].isLetter {
                while i < c.count, c[i].isASCII, c[i].isLetter { name.append(c[i]); i += 1 }
            } else {
                name = String(c[i])
                i += 1
            }

            if let symbol = LaTeXMath.spacedSymbols[name] { return " " + symbol + " " }
            if let symbol = LaTeXMath.plainSymbols[name] { return symbol }
            if LaTeXMath.functionNames.contains(name) { return name }

            switch name {
            case "\\":
                return "\n"
            case ",", ";", ":", " ", ">":
                return " "
            case "!":
                return ""
            case "quad":
                return "\u{2003}"
            case "qquad":
                return "\u{2003}\u{2003}"
            case "frac", "dfrac", "tfrac", "cfrac":
                let a = argument(), b = argument()
                return LaTeXMath.fraction(a, b)
            case "binom", "dbinom", "tbinom":
                let a = argument(), b = argument()
                return "C(" + a + ", " + b + ")"
            case "sqrt":
                let index = optionalArgument()
                let radicand = argument()
                let root = "√" + (LaTeXMath.isAtomic(radicand) ? radicand : "(" + radicand + ")")
                if let index, !index.isEmpty { return LaTeXMath.superscript(index) + root }
                return root
            case "mathbb":
                let a = argument()
                return a.map { LaTeXMath.blackboard[$0] ?? String($0) }.joined()
            case "pmod":
                return " (mod " + argument() + ")"
            case "bmod":
                return " mod "
            case "begin":
                return environment()
            case "end":
                _ = rawGroup()
                return ""
            default:
                break
            }

            if let mark = LaTeXMath.combining[name] {
                let a = argument()
                if a.count == 1 { return a + mark }
                if name == "overline" || name == "bar" || name == "widehat" || name == "widetilde" {
                    return a.map { String($0) + mark }.joined()
                }
                return name + "(" + a + ")"
            }
            if LaTeXMath.passThrough.contains(name) { return argument() }
            if LaTeXMath.dropped.contains(name) {
                // `\left.` and `\right.` are invisible delimiters.
                if (name == "left" || name == "right" || name.hasPrefix("big") || name.hasPrefix("Big")), i < c.count, c[i] == "." { i += 1 }
                return ""
            }
            if LaTeXMath.droppedWithArgument.contains(name) { _ = argument(); return "" }
            // Unknown command: keep its name so the reader still sees what it was.
            return name
        }

        // MARK: Environments (matrices, cases, aligned)

        mutating func environment() -> String {
            let name = rawGroup()
            var bodyChars: [Character] = []
            let closer = Array("\\end{" + name + "}")
            var found = false
            while i < c.count {
                if c[i] == "\\", i + closer.count <= c.count, Array(c[i..<(i + closer.count)]) == closer {
                    i += closer.count
                    found = true
                    break
                }
                bodyChars.append(c[i])
                i += 1
            }
            _ = found   // an unfinished environment (still streaming) shows what it has
            var body = bodyChars
            if name == "array" || name == "tabular" {
                // Drop the column specification `{cc|c}`.
                var k = 0
                while k < body.count, body[k] == " " || body[k] == "\n" { k += 1 }
                if k < body.count, body[k] == "{" {
                    var depth = 0
                    while k < body.count {
                        if body[k] == "{" { depth += 1 }
                        if body[k] == "}" { depth -= 1; if depth == 0 { k += 1; break } }
                        k += 1
                    }
                    body = Array(body[k...])
                }
            }
            let rows = Self.split(body, by: .row).map { row in
                Self.split(row, by: .cell).map { cell -> String in
                    var parser = Parser(cell)
                    return LaTeXMath.tidy(parser.sequence(closing: nil))
                }
            }.filter { row in !(row.count == 1 && row[0].isEmpty) }

            switch name {
            case "cases":
                return rows.map { "{ " + $0.joined(separator: "\u{2003}") }.joined(separator: "\n")
            case "matrix", "smallmatrix", "pmatrix", "bmatrix", "Bmatrix", "vmatrix", "Vmatrix":
                var open = "", close = ""
                switch name {
                case "pmatrix": open = "("; close = ")"
                case "bmatrix": open = "["; close = "]"
                case "Bmatrix": open = "{"; close = "}"
                case "vmatrix": open = "|"; close = "|"
                case "Vmatrix": open = "‖"; close = "‖"
                default: break
                }
                return open + rows.map { $0.joined(separator: " ") }.joined(separator: "; ") + close
            default:
                return rows.map { $0.joined(separator: " ") }.joined(separator: "\n")
            }
        }

        enum Separator { case row, cell }

        /// Splits at `\\` (rows) or `&` (cells) outside braces and nested environments.
        static func split(_ chars: [Character], by separator: Separator) -> [[Character]] {
            var parts: [[Character]] = [[]]
            var braces = 0
            var environments = 0
            var k = 0
            while k < chars.count {
                let ch = chars[k]
                if ch == "{" { braces += 1 }
                if ch == "}" { braces = max(0, braces - 1) }
                if ch == "\\" {
                    let rest = String(chars[k...].prefix(7))
                    if rest.hasPrefix("\\begin") { environments += 1 }
                    if rest.hasPrefix("\\end") { environments = max(0, environments - 1) }
                    if separator == .row, braces == 0, environments == 0, k + 1 < chars.count, chars[k + 1] == "\\" {
                        parts.append([])
                        k += 2
                        continue
                    }
                    // Keep an escaped character (`\&`, `\{`) together.
                    if k + 1 < chars.count, !chars[k + 1].isLetter {
                        parts[parts.count - 1].append(ch)
                        parts[parts.count - 1].append(chars[k + 1])
                        k += 2
                        continue
                    }
                }
                if separator == .cell, ch == "&", braces == 0, environments == 0 {
                    parts.append([])
                    k += 1
                    continue
                }
                parts[parts.count - 1].append(ch)
                k += 1
            }
            return parts
        }
    }
}
