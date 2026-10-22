import Foundation

/// Scenario the assistant works in. Raw values are stored in settings, sessions and
/// workspaces; `meeting` keeps its old raw value and is presented as business meetings.
public enum SessionMode: String, Codable, CaseIterable, Sendable, Identifiable {
    case general, interview, academic, meeting, lesson
    public var id: String { rawValue }
    public var title: String {
        switch self {
        case .general: "通用"
        case .interview: "面试"
        case .academic: "学术会议"
        case .meeting: "商务会议"
        case .lesson: "课程"
        }
    }
    public var symbol: String {
        switch self {
        case .general: "sparkles"
        case .interview: "person.crop.rectangle"
        case .academic: "graduationcap"
        case .meeting: "briefcase"
        case .lesson: "book"
        }
    }
    /// Unknown raw values (e.g. from a newer version) fall back to general.
    public init(from decoder: Decoder) throws {
        let raw = try decoder.singleValueContainer().decode(String.self)
        self = SessionMode(rawValue: raw) ?? .general
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
    /// Material slot within the workspace (简历 / 论文 / 议程 …); nil for older files.
    public var category: String?
    /// Absolute path of the archived Markdown file of this material, when the file
    /// was imported through the Markdown pipeline. Nil for older files.
    public var markdownPath: String?
    /// Source format of the imported file (pdf / docx / pptx / md …), for the UI.
    public var sourceFormat: String?
    public var content: DocumentContent?
    public var characterCount: Int { pages.reduce(0) { $0 + $1.count } }
    public init(id: UUID = UUID(), name: String, pages: [String], enabled: Bool = true, category: String? = nil,
                markdownPath: String? = nil, sourceFormat: String? = nil, content: DocumentContent? = nil) {
        self.id = id; self.name = name; self.pages = pages; self.enabled = enabled; self.category = category
        self.markdownPath = markdownPath; self.sourceFormat = sourceFormat; self.content = content
    }

    // Tolerant decoding: materials imported before the Markdown pipeline have no path.
    enum CodingKeys: String, CodingKey {
        case id, name, pages, enabled, category, markdownPath, sourceFormat, content
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = try c.decode(UUID.self, forKey: .id)
        name = try c.decode(String.self, forKey: .name)
        pages = try c.decodeIfPresent([String].self, forKey: .pages) ?? []
        enabled = try c.decodeIfPresent(Bool.self, forKey: .enabled) ?? true
        category = try c.decodeIfPresent(String.self, forKey: .category)
        markdownPath = try c.decodeIfPresent(String.self, forKey: .markdownPath)
        sourceFormat = try c.decodeIfPresent(String.self, forKey: .sourceFormat)
        content = try c.decodeIfPresent(DocumentContent.self, forKey: .content)
    }
    /// Name shown to the model, e.g. "[简历] resume.pdf".
    public var labeledName: String {
        guard let category, !category.isEmpty else { return name }
        return "[\(category)] \(name)"
    }
}

public struct ContextExcerpt: Codable, Sendable, Equatable {
    public let source: String
    public let page: Int
    public let text: String
    public let locator: DocumentLocator?
    public let chunkID: String?
    public let blockID: String?
    public init(source: String, page: Int, text: String, locator: DocumentLocator? = nil, chunkID: String? = nil, blockID: String? = nil) {
        self.source = source; self.page = page; self.text = text; self.locator = locator; self.chunkID = chunkID; self.blockID = blockID
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
    /// Materials per scenario. Kept in step with `AppState.maxDocuments`.
    public static let maximumDocuments = 1000
    public init(documents: [ContextDocument] = []) {
        self.init(chunks: ContextChunker.chunks(documents: documents))
    }

    /// Builds the index from a chunk list that was cut elsewhere.
    ///
    /// The semantic index needs the very same chunks, so both are built from
    /// `ContextChunker` and the chunk order is the key that ties them together.
    public init(chunks source: [ContextChunk]) {
        for chunk in source {
            chunks.append(
                Chunk(
                    excerpt: .init(source: chunk.source, page: chunk.page, text: chunk.text, locator: chunk.locator, chunkID: chunk.id, blockID: chunk.blockID),
                    terms: Self.terms(chunk.text), order: chunk.order))
        }
        for chunk in chunks { for term in chunk.terms { documentFrequency[term, default: 0] += 1 } }
    }

    public func retrieve(question: String, maximumCharacters: Int = 8_000) -> [ContextExcerpt] {
        guard maximumCharacters > 0 else { return [] }
        var remaining = min(maximumCharacters, 16_000)
        var result: [ContextExcerpt] = []
        for order in rankedOrders(question: question).prefix(8) where remaining > 0 {
            guard order < chunks.count else { continue }
            let excerpt = chunks[order].excerpt
            guard excerpt.text.count <= remaining else { continue }
            result.append(excerpt)
            remaining -= excerpt.text.count
        }
        return result
    }

    /// The chunk orders that match the question, best first.
    ///
    /// The hybrid search needs the ranking itself, not only the excerpts, so it
    /// can fuse it with the ranking of the vector index.
    public func rankedOrders(question: String) -> [Int] {
        let query = Self.terms(String(question.suffix(2_000)))
        guard !query.isEmpty else { return [] }
        return chunks.compactMap { chunk -> (Chunk, Double)? in
            let overlap = query.intersection(chunk.terms)
            guard !overlap.isEmpty else { return nil }
            let score = overlap.reduce(0.0) { sum, term in
                let frequency = documentFrequency[term, default: 0]
                return sum + log(1 + Double(chunks.count) / Double(frequency + 1))
            } / sqrt(Double(max(1, chunk.terms.count)))
            return (chunk, score)
        }.sorted { $0.1 == $1.1 ? $0.0.order < $1.0.order : $0.1 > $1.1 }.map(\.0.order)
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

/// Extracts visible text from Office Open XML parts (PowerPoint slides, Excel shared
/// strings, Word documents). Regex-based so it works without an XML parser and never
/// resolves external entities.
public enum OfficeXMLText {
    public static func extract(_ xml: String) -> String {
        var paragraphs: [String] = []
        // Paragraph containers: a:p (DrawingML), w:p (Word), si (Excel shared strings).
        let paragraphPattern = #"<(a:p|w:p|si)[ >].*?</\1>"#
        let textPattern = #"<(?:a:t|w:t|t)(?: [^>]*)?>(.*?)</(?:a:t|w:t|t)>"#
        guard let paragraphRegex = try? NSRegularExpression(pattern: paragraphPattern, options: [.dotMatchesLineSeparators]),
              let textRegex = try? NSRegularExpression(pattern: textPattern, options: [.dotMatchesLineSeparators]) else { return "" }
        let ns = xml as NSString
        for match in paragraphRegex.matches(in: xml, range: NSRange(location: 0, length: ns.length)) {
            let paragraph = ns.substring(with: match.range) as NSString
            let pieces = textRegex.matches(in: paragraph as String, range: NSRange(location: 0, length: paragraph.length))
                .map { decode(paragraph.substring(with: $0.range(at: 1))) }
            let line = pieces.joined().trimmingCharacters(in: .whitespacesAndNewlines)
            if !line.isEmpty { paragraphs.append(line) }
        }
        return paragraphs.joined(separator: "\n")
    }

    static func decode(_ text: String) -> String {
        text.replacingOccurrences(of: "&lt;", with: "<")
            .replacingOccurrences(of: "&gt;", with: ">")
            .replacingOccurrences(of: "&quot;", with: "\"")
            .replacingOccurrences(of: "&apos;", with: "'")
            .replacingOccurrences(of: "&amp;", with: "&")
    }

    /// Sorts "ppt/slides/slide10.xml" after "slide9.xml".
    public static func numericOrder(_ names: [String]) -> [String] {
        names.sorted { lhs, rhs in
            let l = Int(lhs.filter(\.isNumber)) ?? 0, r = Int(rhs.filter(\.isNumber)) ?? 0
            return l == r ? lhs < rhs : l < r
        }
    }
}

/// A named, reusable context: prompt, mode, style, background and (optionally) files.
public struct SessionPreset: Codable, Sendable, Identifiable, Equatable {
    public var id: UUID
    public var name: String
    public var mode: SessionMode
    public var style: AnswerStyle
    public var preferences: String
    public var profile: String
    public var documents: [ContextDocument]
    public var updatedAt: Date
    public init(id: UUID = UUID(), name: String, mode: SessionMode, style: AnswerStyle, preferences: String, profile: String, documents: [ContextDocument], updatedAt: Date = Date()) {
        self.id = id; self.name = name; self.mode = mode; self.style = style
        self.preferences = preferences; self.profile = profile; self.documents = documents; self.updatedAt = updatedAt
    }

    /// Starting points for the answer-requirements field.
    public static func templatePreferences(for mode: SessionMode) -> String {
        ModeProfile.for(mode).templatePreferences
    }
}
