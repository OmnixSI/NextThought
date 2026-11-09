import AppKit
import SwiftUI
import NextThoughtCore

/// Lightweight markdown for streamed answers: paragraphs, bullets, headings, inline
/// bold/italic/code and fenced code blocks. Re-rendering on every delta is cheap.
struct MarkdownText: View, Equatable {
    var text: String
    var size: CGFloat = 15
    var lineSpacing: CGFloat = 4
    /// Opt in for reading surfaces that must honor macOS accessibility text sizes.
    var scalesWithDynamicType = false

    var body: some View {
        VStack(alignment: .leading, spacing: size * 0.45) {
            ForEach(Array(Self.blocks(text).enumerated()), id: \.offset) { _, block in
                switch block {
                case let .code(code):
                    Text(code)
                        .font(font(size: size * 0.86, design: .monospaced, relativeTo: .title3))
                        .padding(10)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall))
                case let .math(latex):
                    // A display formula (`$$…$$`, `\[…\]`), as Unicode text in a serif face.
                    Text(verbatim: LaTeXMath.unicode(latex))
                        .font(font(size: size * 1.1, design: .serif, relativeTo: .title3))
                        .multilineTextAlignment(.center)
                        .padding(.vertical, 2)
                        .frame(maxWidth: .infinity, alignment: .center)
                case let .table(headers, rows):
                    ScrollView(.horizontal) {
                        Grid(alignment: .leading, horizontalSpacing: 16, verticalSpacing: 10) {
                            GridRow {
                                ForEach(Array(headers.enumerated()), id: \.offset) { _, cell in
                                    Text(Self.inline(cell)).font(font(size: size, weight: .semibold, relativeTo: .title3))
                                        .frame(minWidth: 80, alignment: .leading)
                                }
                            }
                            Divider().gridCellColumns(headers.count)
                            ForEach(Array(rows.enumerated()), id: \.offset) { _, row in
                                GridRow {
                                    ForEach(Array(row.enumerated()), id: \.offset) { _, cell in
                                        Text(Self.inline(cell)).font(font(size: size, relativeTo: .title3))
                                            .frame(minWidth: 80, maxWidth: 280, alignment: .leading)
                                            .fixedSize(horizontal: false, vertical: true)
                                    }
                                }
                            }
                        }.padding(12)
                    }
                    .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall))
                case let .heading(line):
                    Text(Self.inline(line)).font(font(size: size * 1.05, weight: .semibold, relativeTo: .title3))
                case let .bullet(line, level):
                    HStack(alignment: .firstTextBaseline, spacing: 6) {
                        Text(verbatim: "•").foregroundStyle(Ink.ai)
                        Text(Self.inline(line)).font(font(size: size, relativeTo: .title3)).lineSpacing(lineSpacing)
                    }.padding(.leading, CGFloat(level) * 14)
                case let .paragraph(line):
                    Text(Self.inline(line)).font(font(size: size, relativeTo: .title3)).lineSpacing(lineSpacing)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .textSelection(.enabled)
    }

    private func font(size: CGFloat, weight: Font.Weight = .regular, design: Font.Design = .default,
                      relativeTo style: Font.TextStyle = .body) -> Font {
        if scalesWithDynamicType {
            .system(style, design: design).weight(weight)
        } else {
            .system(size: size, weight: weight, design: design)
        }
    }

    enum Block { case paragraph(String), bullet(String, Int), heading(String), code(String), math(String), table([String], [[String]]) }

    static func blocks(_ text: String) -> [Block] {
        var result: [Block] = []
        var code: [String]?
        // A display formula in progress: the marker that ends it and the lines so far.
        var math: (closer: String, lines: [String])?
        for raw in text.components(separatedBy: "\n") {
            if var open = math {
                if let end = LaTeXMath.displayClosing(in: raw, closer: open.closer) {
                    open.lines.append(end.before)
                    result.append(.math(open.lines.joined(separator: "\n")))
                    let after = end.after.trimmingCharacters(in: .whitespaces)
                    if !after.isEmpty { result.append(.paragraph(after)) }
                    math = nil
                } else {
                    open.lines.append(raw)
                    math = open
                }
                continue
            }
            if code == nil, let opening = LaTeXMath.displayOpening(raw.trimmingCharacters(in: .whitespaces)) {
                if let end = LaTeXMath.displayClosing(in: opening.rest, closer: opening.closer) {
                    result.append(.math(end.before))
                    let after = end.after.trimmingCharacters(in: .whitespaces)
                    if !after.isEmpty { result.append(.paragraph(after)) }
                } else {
                    math = (opening.closer, [opening.rest])
                }
                continue
            }
            if raw.trimmingCharacters(in: .whitespaces).hasPrefix("```") {
                if let lines = code { result.append(.code(lines.joined(separator: "\n"))); code = nil } else { code = [] }
                continue
            }
            if code != nil { code?.append(raw); continue }
            let trimmed = raw.trimmingCharacters(in: .whitespaces)
            guard !trimmed.isEmpty else { continue }
            let indent = (raw.prefix { $0 == " " }.count) / 2
            if trimmed.hasPrefix("#") {
                result.append(.heading(trimmed.drop { $0 == "#" }.trimmingCharacters(in: .whitespaces)))
            } else if trimmed.hasPrefix("- ") || trimmed.hasPrefix("* ") || trimmed.hasPrefix("• ") {
                result.append(.bullet(String(trimmed.dropFirst(2)), indent))
            } else if let range = trimmed.range(of: #"^\d{1,2}[.)、] "#, options: .regularExpression) {
                result.append(.bullet(trimmed[range].trimmingCharacters(in: .whitespaces) + " " + trimmed[range.upperBound...], indent))
            } else {
                result.append(.paragraph(trimmed))
            }
        }
        // Unterminated fence while streaming: show what we have.
        if let lines = code, !lines.isEmpty { result.append(.code(lines.joined(separator: "\n"))) }
        if let open = math, !open.lines.joined().trimmingCharacters(in: .whitespaces).isEmpty {
            result.append(.math(open.lines.joined(separator: "\n")))
        }
        var grouped: [Block] = []
        var index = 0
        func cells(_ line: String) -> [String] {
            var value = line.trimmingCharacters(in: .whitespaces)
            if value.hasPrefix("|") { value.removeFirst() }
            if value.hasSuffix("|") { value.removeLast() }
            return value.components(separatedBy: "|").map { $0.trimmingCharacters(in: .whitespaces) }
        }
        while index < result.count {
            if index + 1 < result.count, case let .paragraph(header) = result[index],
               header.contains("|"), case let .paragraph(rule) = result[index + 1] {
                let separators = cells(rule)
                let headings = cells(header)
                if separators.count == headings.count, separators.allSatisfy({
                    let value = $0.replacingOccurrences(of: ":", with: "")
                    return value.count >= 3 && value.allSatisfy { $0 == "-" }
                }) {
                    index += 2
                    var rows: [[String]] = []
                    while index < result.count, case let .paragraph(line) = result[index], line.contains("|") {
                        let row = cells(line)
                        guard row.count == headings.count else { break }
                        rows.append(row)
                        index += 1
                    }
                    grouped.append(.table(headings, rows))
                    continue
                }
            }
            grouped.append(result[index]); index += 1
        }
        return grouped
    }

    static func inline(_ line: String) -> AttributedString {
        // Formulas ($x^2$, \(x^2\)) become Unicode text before Markdown is read.
        let line = LaTeXMath.replacingInlineMath(in: line)
        return (try? AttributedString(markdown: line, options: .init(interpretedSyntax: .inlineOnlyPreservingWhitespace))) ?? AttributedString(line)
    }
}

/// Content card: a glass panel with comfortable padding.
struct CardBackground: ViewModifier {
    var tint: Color? = nil
    func body(content: Content) -> some View {
        content
            .padding(16)
            .frame(maxWidth: .infinity, alignment: .leading)
            .cardSurface(cornerRadius: Space.radiusMedium, tint: tint)
    }
}

extension View {
    func card(tint: Color? = nil) -> some View { modifier(CardBackground(tint: tint)) }
}

/// Small status capsule, e.g. "首字 0.8 s".
struct MetricPill: View {
    var symbol: String
    var text: String
    var tint: Color = .secondary
    var body: some View {
        Label(text, systemImage: symbol)
            .font(.system(size: 11, weight: .medium))
            .foregroundStyle(tint)
            .padding(.horizontal, 9).padding(.vertical, 4)
            .glassCapsule()
    }
}

/// Quick follow-ups on the current answer.
struct RefinementBar: View {
    @Environment(AppState.self) private var state
    var compact = false
    /// Solid chips instead of glass, for a window without a glass background.
    var opaque = false
    var body: some View {
        WrappingHStack(spacing: 6, lineSpacing: 6) {
            ForEach(compact ? [AnswerRefinement.shorter, .example, .english, .star] : AnswerRefinement.allCases) { item in
                Button { state.refine(item) } label: { Label(item.title, systemImage: item.symbol) }
                    .buttonStyle(ChipButtonStyle(opaque: opaque))
                    .fixedSize(horizontal: true, vertical: false)
                    .help(item == .shorter ? "把回答改短 · \(state.shortcutLabel(.refineShorter))" : item.title)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .disabled(state.displayedAnswer.isEmpty || state.isAnswering)
    }
}
