import AppKit
import SwiftUI
import InterviewHackerCore

/// Lightweight markdown for streamed answers: paragraphs, bullets, headings, inline
/// bold/italic/code and fenced code blocks. Re-rendering on every delta is cheap.
struct MarkdownText: View {
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
                        .background(Color.primary.opacity(0.06), in: RoundedRectangle(cornerRadius: 8))
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
            .glassSurface(cornerRadius: 22, tint: tint?.opacity(0.18))
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
    @EnvironmentObject var state: AppState
    var compact = false
    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 6) {
                ForEach(compact ? [AnswerRefinement.shorter, .example, .english, .star] : AnswerRefinement.allCases) { item in
                    Button { state.refine(item) } label: { Label(item.title, systemImage: item.symbol) }
                        .buttonStyle(ChipButtonStyle())
                        .help(item == .shorter ? "把回答改短 · \(state.shortcutLabel(.refineShorter))" : item.title)
                }
            }
        }
        .disabled(state.displayedAnswer.isEmpty || state.isAnswering)
    }
}

/// The question the detector is currently tracking, plus uncertain ones to click.
struct CandidateStrip: View {
    @EnvironmentObject var state: AppState
    var body: some View {
        if state.liveCandidate != nil || !state.suggestedQuestions.isEmpty {
            VStack(alignment: .leading, spacing: 6) {
                if let candidate = state.liveCandidate, candidate.state != .triggered || state.isAnswering == false {
                    HStack(spacing: 6) {
                        Image(systemName: candidate.isFinal ? "text.bubble" : "waveform").foregroundStyle(Color.accentColor)
                        Text(candidate.assessment.intent.title).font(.system(size: 11, weight: .semibold)).foregroundStyle(Color.accentColor)
                        Text(candidate.text).font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(1)
                        Spacer(minLength: 0)
                        if candidate.state != .triggered, candidate.assessment.intent != .statement {
                            Button("回答") { state.answerCandidate(candidate) }.buttonStyle(ChipButtonStyle(prominent: true))
                        }
                    }
                }
                ForEach(state.suggestedQuestions.reversed()) { candidate in
                    HStack(spacing: 6) {
                        Image(systemName: "questionmark.bubble").foregroundStyle(.orange)
                        Text(candidate.text).font(.system(size: 12)).lineLimit(1)
                        Spacer(minLength: 0)
                        Button("回答") { state.answerCandidate(candidate) }.buttonStyle(ChipButtonStyle())
                        Button { state.dismissCandidate(candidate) } label: { Image(systemName: "xmark") }
                            .buttonStyle(.borderless).foregroundStyle(.secondary)
                    }
                }
            }
        }
    }
}
