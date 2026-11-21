using System.Text;

namespace NextThought.Core;

/// <summary>
/// LaTeX formulas as plain Unicode text (port of LaTeXMath.swift): Greek letters and operators
/// become symbols, x^2 becomes x², a_{ij} becomes aᵢⱼ, \frac{a}{b} becomes a/b, matrices and cases
/// become short lines. Not a typesetter; input may be half-written and every function finishes.
/// </summary>
public static class LaTeXMath
{
    // MARK: Entry points

    /// <summary>A LaTeX formula as Unicode text; several rows become several lines.</summary>
    public static string Unicode(string latex) => Tidy(new Parser(latex.ToCharArray()).Sequence(null));

    /// <summary>
    /// Markdown with display formulas ($$…$$ / \[…\]) and inline formulas replaced by Unicode.
    /// Code fences and `\$` are left alone, and a lone $ that is money stays as it is.
    /// </summary>
    public static string Markdown(string text)
    {
        var output = new StringBuilder();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var inFence = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                output.AppendLine(line);
                continue;
            }
            if (inFence) { output.AppendLine(line); continue; }

            var trimmed = line.Trim();
            if (DisplayOpening(trimmed) is { } display)
            {
                var (closer, rest) = display;
                var formula = new StringBuilder();
                if (DisplayClosing(rest, closer) is { } sameDisplay)
                {
                    var (before, after) = sameDisplay;
                    formula.Append(before);
                    output.AppendLine(Unicode(formula.ToString()));
                    if (after.Length > 0) output.AppendLine(ReplacingInlineMath(after));
                    continue;
                }
                formula.Append(rest).Append('\n');
                var closed = false;
                while (++index < lines.Length)
                {
                    if (DisplayClosing(lines[index], closer) is { } closeDisplay)
                    {
                        var (before, after) = closeDisplay;
                        formula.Append(before);
                        if (after.Length > 0) output.AppendLine(Unicode(formula.ToString())).AppendLine(ReplacingInlineMath(after));
                        else output.AppendLine(Unicode(formula.ToString()));
                        closed = true;
                        break;
                    }
                    formula.Append(lines[index]).Append('\n');
                }
                if (!closed) output.AppendLine(Unicode(formula.ToString()));
                continue;
            }
            output.AppendLine(ReplacingInlineMath(line));
        }
        return output.ToString().Replace("\r\n", "\n").TrimEnd('\n');
    }

    /// <summary>A line that opens a display formula: the closer and the rest after the opener.</summary>
    public static (string Closer, string Content)? DisplayOpening(string trimmedLine)
    {
        if (trimmedLine.StartsWith("$$", StringComparison.Ordinal)) return ("$$", trimmedLine[2..]);
        if (trimmedLine.StartsWith("\\[", StringComparison.Ordinal)) return ("\\]", trimmedLine[2..]);
        return null;
    }

    /// <summary>Where <paramref name="closer"/> ends a display formula: the part before and after.</summary>
    public static (string Before, string After)? DisplayClosing(string text, string closer)
    {
        var at = text.IndexOf(closer, StringComparison.Ordinal);
        return at < 0 ? null : (text[..at], text[(at + closer.Length)..]);
    }

    // MARK: Finding formulas

    private static int Find(char[] pattern, char[] characters, int start)
    {
        if (pattern.Length == 0 || characters.Length < pattern.Length || start > characters.Length - pattern.Length) return -1;
        for (var index = start; index <= characters.Length - pattern.Length; index++)
        {
            var matches = true;
            for (var offset = 0; offset < pattern.Length; offset++)
                if (characters[index + offset] != pattern[offset]) { matches = false; break; }
            if (matches) return index;
        }
        return -1;
    }

    /// <summary>
    /// The index of the $ closing a formula opened at <paramref name="open"/>, or null when this $
    /// is not an opener (money, a lone sign).
    /// </summary>
    private static int? ClosingDollar(char[] characters, int open)
    {
        if (open + 1 >= characters.Length || char.IsWhiteSpace(characters[open + 1])) return null;
        var index = open + 1;
        while (index < characters.Length)
        {
            if (characters[index] == '\\') { index += 2; continue; }
            if (characters[index] == '$')
            {
                var before = characters[index - 1];
                var after = index + 1 < characters.Length ? characters[index + 1] : (char?)null;
                if (index > open + 1 && !char.IsWhiteSpace(before) && !(after is { } value && char.IsNumber(value))) return index;
                return null;
            }
            index++;
        }
        return null;
    }

    private static string EscapeMarkdown(string text)
    {
        var output = new StringBuilder();
        foreach (var character in text)
        {
            if ("\\*_`[]<>~|".Contains(character)) output.Append('\\');
            output.Append(character);
        }
        return output.ToString();
    }

    /// <summary>`text` with every inline formula replaced by its Unicode text, escaped for Markdown.</summary>
    public static string ReplacingInlineMath(string text)
    {
        if (!text.Contains('$') && !text.Contains("\\(")) return text;
        var characters = text.ToCharArray();
        var output = new StringBuilder();
        var index = 0;
        while (index < characters.Length)
        {
            var character = characters[index];
            if (character == '`')
            {
                var end = index + 1;
                while (end < characters.Length && characters[end] != '`') end++;
                end = Math.Min(end + 1, characters.Length);
                output.Append(characters, index, end - index);
                index = end;
            }
            else if (character == '\\' && index + 1 < characters.Length && characters[index + 1] == '$')
            {
                output.Append("\\$");
                index += 2;
            }
            else if (character == '\\' && index + 1 < characters.Length && characters[index + 1] == '(')
            {
                var close = Find(['\\', ')'], characters, index + 2);
                if (close >= 0)
                {
                    output.Append(EscapeMarkdown(Unicode(new string(characters, index + 2, close - index - 2))));
                    index = close + 2;
                }
                else { output.Append(character); index++; }
            }
            else if (character == '$' && index + 1 < characters.Length && characters[index + 1] == '$')
            {
                var close = Find(['$', '$'], characters, index + 2);
                if (close > index + 2)
                {
                    output.Append(EscapeMarkdown(Unicode(new string(characters, index + 2, close - index - 2))));
                    index = close + 2;
                }
                else { output.Append(character); index++; }
            }
            else if (character == '$' && ClosingDollar(characters, index) is { } dollarClose)
            {
                output.Append(EscapeMarkdown(Unicode(new string(characters, index + 1, dollarClose - index - 1))));
                index = dollarClose + 1;
            }
            else
            {
                output.Append(character);
                index++;
            }
        }
        return output.ToString();
    }

    // MARK: Tidying

    internal static string Tidy(string text)
    {
        var collapsed = new StringBuilder();
        var lastSpace = false;
        foreach (var character in text)
        {
            if (character == ' ')
            {
                if (!lastSpace) collapsed.Append(character);
                lastSpace = true;
            }
            else
            {
                collapsed.Append(character);
                lastSpace = false;
            }
        }
        var lines = collapsed.ToString().Split('\n').Select(line => line.Trim(' '));
        return string.Join("\n", lines).Trim();
    }

    // MARK: Tables

    internal static readonly IReadOnlyDictionary<string, string> PlainSymbols = Table(
    [
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
    ]);

    internal static readonly IReadOnlyDictionary<string, string> SpacedSymbols = Table(
    [
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
    ]);

    internal static readonly HashSet<string> FunctionNames =
    [
        "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "coth",
        "log", "ln", "lg", "exp", "max", "min", "sup", "inf", "lim", "limsup", "liminf", "det", "dim", "ker",
        "gcd", "lcm", "deg", "arg", "Pr", "hom"
    ];

    internal static readonly HashSet<string> PassThrough =
    [
        "text", "textbf", "textit", "textrm", "mathrm", "mathbf", "mathit", "mathsf", "mathtt", "mathcal",
        "mathscr", "mathfrak", "operatorname", "boldsymbol", "bm", "mbox", "rm", "bf", "it", "underline",
        "underbrace", "overbrace", "boxed", "cancel", "xcancel", "bcancel", "mathop", "mathrel", "mathbin"
    ];

    internal static readonly HashSet<string> Dropped =
    [
        "left", "right", "big", "Big", "bigg", "Bigg", "bigl", "bigr", "Bigl", "Bigr", "biggl", "biggr", "middle",
        "limits", "nolimits", "displaystyle", "textstyle", "scriptstyle", "scriptscriptstyle", "nonumber", "notag",
        "not", "allowbreak", "relax"
    ];

    internal static readonly HashSet<string> DroppedWithArgument =
        ["label", "tag", "phantom", "vphantom", "hphantom", "ensuremath", "hspace", "vspace", "mkern", "kern"];

    private static readonly IReadOnlyDictionary<string, string> Combining = Table(
    [
        ("hat", "\u0302"), ("widehat", "\u0302"), ("bar", "\u0304"), ("overline", "\u0305"), ("vec", "\u20D7"),
        ("overrightarrow", "\u20D7"), ("tilde", "\u0303"), ("widetilde", "\u0303"), ("dot", "\u0307"),
        ("ddot", "\u0308"), ("check", "\u030C"), ("acute", "\u0301"), ("grave", "\u0300"), ("breve", "\u0306")
    ]);

    private static readonly IReadOnlyDictionary<char, string> Blackboard = new Dictionary<char, string>
    {
        ['R'] = "ℝ", ['N'] = "ℕ", ['Z'] = "ℤ", ['Q'] = "ℚ", ['C'] = "ℂ", ['P'] = "ℙ", ['H'] = "ℍ", ['E'] = "𝔼", ['1'] = "𝟙"
    };

    private static readonly IReadOnlyDictionary<string, string> Fractions = Table(
    [
        ("1/2", "½"), ("1/3", "⅓"), ("2/3", "⅔"), ("1/4", "¼"), ("3/4", "¾"), ("1/5", "⅕"), ("2/5", "⅖"), ("3/5", "⅗"),
        ("4/5", "⅘"), ("1/6", "⅙"), ("5/6", "⅚"), ("1/8", "⅛"), ("3/8", "⅜"), ("5/8", "⅝"), ("7/8", "⅞")
    ]);

    private static readonly IReadOnlyDictionary<char, char> Superscripts = Zip(
        "0123456789+-−=()abcdefghijklmnoprstuvwxyzABDEGHIJKLMNOPRTUVWβγδθφχ",
        "⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁻⁼⁽⁾ᵃᵇᶜᵈᵉᶠᵍʰⁱʲᵏˡᵐⁿᵒᵖʳˢᵗᵘᵛʷˣʸᶻᴬᴮᴰᴱᴳᴴᴵᴶᴷᴸᴹᴺᴼᴾᴿᵀᵁⱽᵂᵝᵞᵟᶿᵠᵡ");

    private static readonly IReadOnlyDictionary<char, char> Subscripts = Zip(
        "0123456789+-−=()aehijklmnoprstuvxβγρφχ",
        "₀₁₂₃₄₅₆₇₈₉₊₋₋₌₍₎ₐₑₕᵢⱼₖₗₘₙₒₚᵣₛₜᵤᵥₓᵦᵧᵨᵩᵪ");

    /// <summary>Both maps need to be as long as their key lists; the tests check this.</summary>
    public static (int Superscript, int Subscript) ScriptTableSizes => (Superscripts.Count, Subscripts.Count);

    public static string Superscript(string text)
    {
        var trimmed = new string(text.Where(character => character != ' ').ToArray());
        if (trimmed.Length == 0) return "";
        if (trimmed == "∘") return "°";
        if (trimmed is "′" or "″") return trimmed;
        if (trimmed.All(Superscripts.ContainsKey)) return new string(trimmed.Select(character => Superscripts[character]).ToArray());
        return trimmed.Length == 1 ? "^" + trimmed : "^(" + text + ")";
    }

    public static string Subscripted(string text)
    {
        var trimmed = new string(text.Where(character => character != ' ').ToArray());
        if (trimmed.Length == 0) return "";
        if (trimmed.All(Subscripts.ContainsKey)) return new string(trimmed.Select(character => Subscripts[character]).ToArray());
        return trimmed.Length == 1 ? "_" + trimmed : "_(" + text + ")";
    }

    /// <summary>No operator or space at the top level, so it can stand beside / or √ without brackets.</summary>
    public static bool IsAtomic(string text)
    {
        if (text.Length == 0) return true;
        var depth = 0;
        foreach (var character in text)
        {
            if (character == '(') { depth++; continue; }
            if (character == ')') { depth--; if (depth < 0) return false; continue; }
            if (depth > 0) continue;
            if (char.IsLetter(character) || char.IsNumber(character) || IsCombining(character) || ".′″∞°".Contains(character)) continue;
            return false;
        }
        return depth == 0;
    }

    private static bool IsCombining(char character) =>
        char.GetUnicodeCategory(character) is System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.SpacingCombiningMark
            or System.Globalization.UnicodeCategory.EnclosingMark;

    public static string Wrapped(string text) => IsAtomic(text) ? text : "(" + text + ")";

    public static string Fraction(string numerator, string denominator)
    {
        var top = numerator.Trim();
        var bottom = denominator.Trim();
        if (Fractions.TryGetValue(top + "/" + bottom, out var vulgar)) return vulgar;
        return Wrapped(top) + "/" + Wrapped(bottom);
    }

    // MARK: Parser

    private sealed class Parser(char[] characters)
    {
        private int index;

        /// <summary>Everything up to the matching closing character (consumed), or to the end.</summary>
        public string Sequence(char? closing)
        {
            var output = new StringBuilder();
            while (index < characters.Length)
            {
                var character = characters[index];
                if (character == '}')
                {
                    index++;
                    if (closing == '}') return output.ToString();
                    continue;
                }
                if (character == ']' && closing == ']') { index++; return output.ToString(); }
                switch (character)
                {
                    case '{':
                        index++;
                        output.Append(Sequence('}'));
                        break;
                    case '^':
                        index++;
                        output.Append(Superscript(ScriptArgument()));
                        break;
                    case '_':
                        index++;
                        output.Append(Subscripted(ScriptArgument()));
                        break;
                    case '\\':
                        output.Append(Command());
                        break;
                    case '\n' or '\t' or '\r':
                        output.Append(' ');
                        index++;
                        break;
                    case '~':
                        output.Append('\u00A0');
                        index++;
                        break;
                    case '\'':
                        output.Append('′');
                        index++;
                        break;
                    case '$':
                        index++;
                        break;
                    case '&':
                        output.Append(' ');
                        index++;
                        break;
                    default:
                        output.Append(character);
                        index++;
                        break;
                }
            }
            return output.ToString();
        }

        private void SkipSpaces()
        {
            while (index < characters.Length && characters[index] is ' ' or '\n' or '\t') index++;
        }

        /// <summary>The argument of ^, _ or a command: a {group}, one command, or one character.</summary>
        private string Argument()
        {
            SkipSpaces();
            if (index >= characters.Length) return "";
            if (characters[index] == '{')
            {
                index++;
                return Sequence('}');
            }
            if (characters[index] == '\\') return Command();
            return characters[index++].ToString();
        }

        private string ScriptArgument() => Argument();

        /// <summary>[...] right after a command (the index of a root), if there is one.</summary>
        private string? OptionalArgument()
        {
            SkipSpaces();
            if (index >= characters.Length || characters[index] != '[') return null;
            index++;
            return Sequence(']');
        }

        /// <summary>Raw text of a {…} group (braces matched, nothing converted).</summary>
        private string RawGroup()
        {
            SkipSpaces();
            if (index >= characters.Length || characters[index] != '{') return "";
            var depth = 0;
            var output = new StringBuilder();
            while (index < characters.Length)
            {
                var character = characters[index++];
                if (character == '{') { depth++; if (depth == 1) continue; }
                if (character == '}') { depth--; if (depth == 0) break; }
                output.Append(character);
            }
            return output.ToString();
        }

        private string Command()
        {
            index++; // the backslash
            if (index >= characters.Length) return "";
            var name = new StringBuilder();
            if (char.IsAsciiLetter(characters[index]))
            {
                while (index < characters.Length && char.IsAsciiLetter(characters[index])) name.Append(characters[index++]);
            }
            else
            {
                name.Append(characters[index++]);
            }
            var command = name.ToString();

            if (SpacedSymbols.TryGetValue(command, out var spaced)) return " " + spaced + " ";
            if (PlainSymbols.TryGetValue(command, out var plain)) return plain;
            if (FunctionNames.Contains(command)) return command;

            switch (command)
            {
                case "\\":
                    return "\n";
                case "," or ";" or ":" or " " or ">":
                    return " ";
                case "!":
                    return "";
                case "quad":
                    return "\u2003";
                case "qquad":
                    return "\u2003\u2003";
                case "frac" or "dfrac" or "tfrac" or "cfrac":
                    return Fraction(Argument(), Argument());
                case "binom" or "dbinom" or "tbinom":
                    return "C(" + Argument() + ", " + Argument() + ")";
                case "sqrt":
                    var rootIndex = OptionalArgument();
                    var radicand = Argument();
                    var root = "√" + (IsAtomic(radicand) ? radicand : "(" + radicand + ")");
                    return rootIndex is { Length: > 0 } ? Superscript(rootIndex) + root : root;
                case "mathbb":
                    return string.Concat(Argument().Select(character =>
                        Blackboard.TryGetValue(character, out var black) ? black : character.ToString()));
                case "pmod":
                    return " (mod " + Argument() + ")";
                case "bmod":
                    return " mod ";
                case "begin":
                    return Environment();
                case "end":
                    _ = RawGroup();
                    return "";
            }

            if (Combining.TryGetValue(command, out var mark))
            {
                var argument = Argument();
                if (argument.Length == 1) return argument + mark;
                if (command is "overline" or "bar" or "widehat" or "widetilde")
                    return string.Concat(argument.Select(character => character + mark));
                return command + "(" + argument + ")";
            }
            if (PassThrough.Contains(command)) return Argument();
            if (Dropped.Contains(command))
            {
                // \left. and \right. are invisible delimiters.
                if ((command is "left" or "right" || command.StartsWith("big", StringComparison.Ordinal) || command.StartsWith("Big", StringComparison.Ordinal))
                    && index < characters.Length && characters[index] == '.') index++;
                return "";
            }
            if (DroppedWithArgument.Contains(command)) { _ = Argument(); return ""; }
            // Unknown command: keep its name so the reader still sees what it was.
            return command;
        }

        // MARK: Environments (matrices, cases, aligned)

        private string Environment()
        {
            var name = RawGroup();
            var body = new List<char>();
            var closer = ("\\end{" + name + "}").ToCharArray();
            while (index < characters.Length)
            {
                if (characters[index] == '\\' && index + closer.Length <= characters.Length
                    && characters.AsSpan(index, closer.Length).SequenceEqual(closer))
                {
                    index += closer.Length;
                    break;
                }
                body.Add(characters[index++]);
            }
            var content = body;
            if (name is "array" or "tabular")
            {
                var position = 0;
                while (position < content.Count && content[position] is ' ' or '\n') position++;
                if (position < content.Count && content[position] == '{')
                {
                    var depth = 0;
                    while (position < content.Count)
                    {
                        if (content[position] == '{') depth++;
                        if (content[position] == '}')
                        {
                            depth--;
                            if (depth == 0) { position++; break; }
                        }
                        position++;
                    }
                    content = content.GetRange(position, content.Count - position);
                }
            }
            var rows = Split(content, Separator.Row)
                .Select(row => Split(row, Separator.Cell)
                    .Select(cell => Tidy(new Parser(cell.ToArray()).Sequence(null)))
                    .ToList())
                .Where(row => !(row.Count == 1 && row[0].Length == 0))
                .ToList();

            switch (name)
            {
                case "cases":
                    return string.Join("\n", rows.Select(row => "{ " + string.Join("\u2003", row)));
                case "matrix" or "smallmatrix" or "pmatrix" or "bmatrix" or "Bmatrix" or "vmatrix" or "Vmatrix":
                    var (open, close) = name switch
                    {
                        "pmatrix" => ("(", ")"),
                        "bmatrix" => ("[", "]"),
                        "Bmatrix" => ("{", "}"),
                        "vmatrix" => ("|", "|"),
                        "Vmatrix" => ("‖", "‖"),
                        _ => ("", "")
                    };
                    return open + string.Join("; ", rows.Select(row => string.Join(" ", row))) + close;
                default:
                    return string.Join("\n", rows.Select(row => string.Join(" ", row)));
            }
        }

        private enum Separator { Row, Cell }

        /// <summary>Splits at \\ (rows) or &amp; (cells) outside braces and nested environments.</summary>
        private static List<List<char>> Split(List<char> characters, Separator separator)
        {
            var parts = new List<List<char>> { new() };
            var braces = 0;
            var environments = 0;
            var index = 0;
            while (index < characters.Count)
            {
                var character = characters[index];
                if (character == '{') braces++;
                if (character == '}') braces = Math.Max(0, braces - 1);
                if (character == '\\')
                {
                    var rest = new string(characters.Skip(index).Take(7).ToArray());
                    if (rest.StartsWith("\\begin", StringComparison.Ordinal)) environments++;
                    if (rest.StartsWith("\\end", StringComparison.Ordinal)) environments = Math.Max(0, environments - 1);
                    if (separator == Separator.Row && braces == 0 && environments == 0 && index + 1 < characters.Count && characters[index + 1] == '\\')
                    {
                        parts.Add([]);
                        index += 2;
                        continue;
                    }
                    // Keep an escaped character (\&, \{) together.
                    if (index + 1 < characters.Count && !char.IsLetter(characters[index + 1]))
                    {
                        parts[^1].Add(character);
                        parts[^1].Add(characters[index + 1]);
                        index += 2;
                        continue;
                    }
                }
                if (separator == Separator.Cell && character == '&' && braces == 0 && environments == 0)
                {
                    parts.Add([]);
                    index++;
                    continue;
                }
                parts[^1].Add(character);
                index++;
            }
            return parts;
        }
    }

    private static IReadOnlyDictionary<string, string> Table((string Key, string Value)[] pairs)
    {
        var table = new Dictionary<string, string>();
        foreach (var (key, value) in pairs) table.TryAdd(key, value);
        return table;
    }

    private static IReadOnlyDictionary<char, char> Zip(string keys, string values)
    {
        if (keys.Length != values.Length) throw new InvalidOperationException("script tables must pair up");
        var map = new Dictionary<char, char>();
        for (var index = 0; index < keys.Length; index++) map[keys[index]] = values[index];
        return map;
    }
}
