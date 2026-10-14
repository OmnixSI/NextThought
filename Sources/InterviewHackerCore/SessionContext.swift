import Foundation

public enum SessionMode: String, Codable, CaseIterable, Sendable, Identifiable {
    case meeting, interview, lesson
    public var id: String { rawValue }
    public var title: String {
        switch self { case .meeting: "会议"; case .interview: "面试"; case .lesson: "课程" }
    }
}

public enum AnswerStyle: String, Codable, CaseIterable, Sendable, Identifiable {
    case concise, detailed
    public var id: String { rawValue }
    public var title: String { self == .concise ? "提词要点" : "详细解释" }
}

public struct ContextDocument: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var name: String
    public var pages: [String]
    public var enabled: Bool
    public var characterCount: Int { pages.reduce(0) { $0 + $1.count } }
    public init(id: UUID = UUID(), name: String, pages: [String], enabled: Bool = true) {
        self.id = id; self.name = name; self.pages = pages; self.enabled = enabled
    }
}

public struct ContextExcerpt: Codable, Sendable, Equatable {
    public let source: String
    public let page: Int
    public let text: String
    public init(source: String, page: Int, text: String) {
        self.source = source; self.page = page; self.text = text
    }
}

/// Small, deterministic, offline lexical index. This is not semantic/vector search.
/// Import work is bounded; tokenization of document chunks happens once per rebuild.
public struct SessionContextIndex: Sendable {
    private struct Chunk: Sendable {
        let excerpt: ContextExcerpt
        let terms: Set<String>
        let order: Int
    }
    private var chunks: [Chunk] = []
    private var documentFrequency: [String: Int] = [:]
    public init(documents: [ContextDocument] = []) {
        for document in documents.prefix(12) where document.enabled {
            for (pageNumber, page) in document.pages.prefix(200).enumerated() {
                let chars = Array(page.prefix(200_000))
                var start = 0
                while start < chars.count && chunks.count < 2_000 {
                    let end = min(start + 900, chars.count)
                    let text = String(chars[start..<end])
                    chunks.append(Chunk(excerpt: .init(source: document.name, page: pageNumber + 1, text: text),
                                        terms: Self.terms(text), order: chunks.count))
                    if end == chars.count { break }
                    start = end - 120
                }
            }
        }
        for chunk in chunks { for term in chunk.terms { documentFrequency[term, default: 0] += 1 } }
    }

    public func retrieve(question: String, maximumCharacters: Int = 8_000) -> [ContextExcerpt] {
        guard maximumCharacters > 0 else { return [] }
        let query = Self.terms(String(question.suffix(2_000)))
        guard !query.isEmpty else { return [] }
        let ranked = chunks.compactMap { chunk -> (Chunk, Double)? in
            let overlap = query.intersection(chunk.terms)
            guard !overlap.isEmpty else { return nil }
            let score = overlap.reduce(0.0) { sum, term in
                let frequency = documentFrequency[term, default: 0]
                return sum + log(1 + Double(chunks.count) / Double(frequency + 1))
            } / sqrt(Double(max(1, chunk.terms.count)))
            return (chunk, score)
        }.sorted { $0.1 == $1.1 ? $0.0.order < $1.0.order : $0.1 > $1.1 }
        var remaining = min(maximumCharacters, 16_000)
        var result: [ContextExcerpt] = []
        for (chunk, _) in ranked.prefix(8) where remaining > 0 {
            let text = String(chunk.excerpt.text.prefix(remaining))
            result.append(.init(source: chunk.excerpt.source, page: chunk.excerpt.page, text: text))
            remaining -= text.count
        }
        return result
    }

    private static func terms(_ text: String) -> Set<String> {
        let stop: Set<String> = ["the", "a", "an", "is", "are", "was", "were", "of", "to", "and", "in", "on", "this", "that", "what", "how", "why", "you", "your", "explain", "can", "could", "please", "does", "do"]
        var result = Set<String>()
        var cjk: [Character] = []
        var word = ""
        func flushWord() { if word.count > 1 && !stop.contains(word) { result.insert(word) }; word = "" }
        func flushCJK() {
            if cjk.count == 1 { result.insert(String(cjk)) }
            if cjk.count > 1 { for i in 0..<(cjk.count - 1) { result.insert(String(cjk[i...i+1])) } }
            cjk.removeAll(keepingCapacity: true)
        }
        for char in text.lowercased() {
            if char.unicodeScalars.contains(where: { (0x3400...0x9FFF).contains($0.value) }) {
                flushWord(); cjk.append(char)
            } else if char.isLetter || char.isNumber { flushCJK(); word.append(char) }
            else { flushWord(); flushCJK() }
        }
        flushWord(); flushCJK()
        return result
    }
}

public enum ManualQuestionBuilder {
    public static func make(typed: String, partial: String, recent: [String]) -> String? {
        let explicit = typed.trimmingCharacters(in: .whitespacesAndNewlines)
        if !explicit.isEmpty { return String(explicit.prefix(8_000)) }
        let live = partial.trimmingCharacters(in: .whitespacesAndNewlines)
        let text = (recent.suffix(3) + (live.isEmpty ? [] : [live])).joined(separator: "\n")
        guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return nil }
        return "请根据以下最新转录，回答其中的最后一个问题或解释最后一个知识点；如果没有明确对象，请指出，不要自造问题。\n" + String(text.suffix(4_000))
    }
}
