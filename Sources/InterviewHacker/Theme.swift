import AppKit
import SwiftUI
import InterviewHackerCore

/// Lightweight markdown for streamed answers: paragraphs, bullets, headings, inline
/// bold/italic/code and fenced code blocks. Re-rendering on every delta is cheap.
struct MarkdownText: View, Equatable {
    var text: String
    var size: CGFloat = 15
    var lineSpacing: CGFloat = 4

    var body: some View {
        VStack(alignment: .leading, spacing: size * 0.45) {
            ForEach(Array(Self.blocks(text).enumerated()), id: \.offset) { _, block in
                switch block {
                case let .code(code):
                    Text(code)
                        .font(.system(size: size * 0.86, design: .monospaced))
                        .padding(10)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(Brand.sunken, in: RoundedRectangle(cornerRadius: 6, style: .continuous))
                case let .heading(line):
                    Text(Self.inline(line)).font(.system(size: size * 1.05, weight: .semibold))
                case let .bullet(line, level):
                    HStack(alignment: .firstTextBaseline, spacing: 6) {
                        Text("•").foregroundStyle(Color.accentColor)
                        Text(Self.inline(line)).font(.system(size: size)).lineSpacing(lineSpacing)
                    }.padding(.leading, CGFloat(level) * 14)
                case let .paragraph(line):
                    Text(Self.inline(line)).font(.system(size: size)).lineSpacing(lineSpacing)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .textSelection(.enabled)
    }

    enum Block { case paragraph(String), bullet(String, Int), heading(String), code(String) }

    static func blocks(_ text: String) -> [Block] {
        var result: [Block] = []
        var code: [String]?
        for raw in text.components(separatedBy: "\n") {
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
        return result
    }

    static func inline(_ line: String) -> AttributedString {
        (try? AttributedString(markdown: line, options: .init(interpretedSyntax: .inlineOnlyPreservingWhitespace))) ?? AttributedString(line)
    }
}

/// Content card: a glass panel with comfortable padding.
struct CardBackground: ViewModifier {
    var tint: Color? = nil
    func body(content: Content) -> some View {
        content
            .padding(16)
            .frame(maxWidth: .infinity, alignment: .leading)
            .cardSurface(cornerRadius: UI.cardRadius, tint: tint)
    }
}

extension View {
    func card(tint: Color? = nil) -> some View { modifier(CardBackground(tint: tint)) }
}

/// Small status line, e.g. "首字 0.8 s": icon + text, no capsule (capsules are for actions).
struct MetricPill: View {
    var symbol: String
    var text: String
    var tint: Color = .secondary
    var body: some View {
        Label(text, systemImage: symbol)
            .font(.system(size: 12, weight: .medium))
            .foregroundStyle(tint)
    }
}

/// Quick follow-ups on the current answer. Three common rewrites stay visible; the rest,
/// plus regenerate and web lookup, live under "更多" so the row never wraps or clips.
struct RefinementBar: View {
    @Environment(AppState.self) private var state
    var compact = false
    /// Bordered chips, for a window without a background.
    var opaque = false
    private static let visible: [AnswerRefinement] = [.shorter, .example, .english]

    var body: some View {
        HStack(spacing: 6) {
            ForEach(Self.visible) { item in
                Button { state.refine(item) } label: { Label(item.title, systemImage: item.symbol) }
                    .buttonStyle(ChipButtonStyle(opaque: opaque))
                    .help(item == .shorter ? "把回答改短 · \(state.shortcutLabel(.refineShorter))" : item.title)
            }
            Menu {
                ForEach(AnswerRefinement.allCases.filter { !Self.visible.contains($0) }) { item in
                    Button { state.refine(item) } label: { Label(item.title, systemImage: item.symbol) }
                }
                if !compact {
                    Divider()
                    Button { state.regenerate() } label: { Label("重新生成", systemImage: "arrow.clockwise") }
                        .disabled(state.isAnswering || state.currentQuestion.isEmpty)
                    if state.webSearchMode != .off, state.displayedWebLookup == nil {
                        Button { state.lookUpDisplayedAnswer() } label: { Label("联网查一下", systemImage: "globe") }
                            .disabled(state.displayedQuestion.isEmpty)
                    }
                }
            } label: {
                Label("更多", systemImage: "ellipsis")
            }
            .menuStyle(.button)
            .menuIndicator(.hidden)
            .buttonStyle(ChipButtonStyle(opaque: opaque))
            .fixedSize()
        }
        .disabled(state.displayedAnswer.isEmpty || state.isAnswering)
    }
}
