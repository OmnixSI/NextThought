import Foundation

/// User-authored meeting content lives beside, never inside, the replaceable AI output.
public struct SessionHighlight: Codable, Sendable, Equatable, Identifiable {
    public var id: UUID
    public var lineID: UUID
    public var offsetSeconds: Int
    public var quote: String

    public init(id: UUID = UUID(), lineID: UUID, offsetSeconds: Int, quote: String) {
        self.id = id; self.lineID = lineID; self.offsetSeconds = offsetSeconds; self.quote = quote
    }
}

public struct SessionCorrectionDecision: Codable, Sendable, Equatable {
    public var id: UUID
    public var lineID: UUID
    public var original: String
    public var corrected: String
    public var reverted: Bool

    public init(id: UUID = UUID(), lineID: UUID, original: String, corrected: String, reverted: Bool) {
        self.id = id
        self.lineID = lineID; self.original = original; self.corrected = corrected; self.reverted = reverted
    }

    enum CodingKeys: String, CodingKey { case id, lineID, original, corrected, reverted }
    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        lineID = try c.decode(UUID.self, forKey: .lineID)
        original = try c.decode(String.self, forKey: .original)
        corrected = try c.decode(String.self, forKey: .corrected)
        reverted = try c.decodeIfPresent(Bool.self, forKey: .reverted) ?? false
        if let saved = try c.decodeIfPresent(UUID.self, forKey: .id) { id = saved }
        else {
            // Schema-1 decisions originally had no ID. A stable fallback keeps source/search revisions steady across reads.
            let hash = SessionProjectionBuilder.transcriptRevision(id: lineID, text: "\(original.utf8.count):\(original)\(corrected)")
            let compact = String(hash.prefix(32))
            let value = "\(compact.prefix(8))-\(compact.dropFirst(8).prefix(4))-\(compact.dropFirst(12).prefix(4))-\(compact.dropFirst(16).prefix(4))-\(compact.dropFirst(20).prefix(12))"
            id = UUID(uuidString: value) ?? lineID
        }
    }
}

public struct SessionUserContent: Codable, Sendable, Equatable {
    public static let filename = "user-content.json"
    public var schemaVersion = 1
    public var revision = 0
    public var updatedAt: Date = .distantPast
    public var personalNotes = ""
    public var highlights: [SessionHighlight] = []
    public var editedNotes: StructuredNotes?
    public var editedSummary: SessionSummary?
    public var editedMarkdown: String?
    public var basedOnGeneratedAt: Date?
    public var correctionDecisions: [SessionCorrectionDecision] = []

    public init() {}

    public var hasContent: Bool {
        !personalNotes.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || !highlights.isEmpty
            || editedNotes != nil || editedSummary != nil || editedMarkdown != nil || !correctionDecisions.isEmpty
    }

    /// Model corrections can change with a template. Explicit user decisions cannot.
    public func corrections(generated: [TranscriptCorrection], lines: [SessionTranscriptLine]) -> [TranscriptCorrection] {
        var result = generated
        let indices = Dictionary(lines.enumerated().map { ($0.element.id, $0.offset) }, uniquingKeysWith: { first, _ in first })
        for decision in correctionDecisions {
            guard let index = indices[decision.lineID], lines[index].text.contains(decision.original) else { continue }
            if let existing = result.firstIndex(where: { $0.line == index && $0.original == decision.original }) {
                result[existing].corrected = decision.corrected
                result[existing].reverted = decision.reverted
            } else {
                result.append(.init(id: decision.id, line: index, original: decision.original, corrected: decision.corrected, reverted: decision.reverted))
            }
        }
        return result
    }

    enum CodingKeys: String, CodingKey {
        case schemaVersion, revision, updatedAt, personalNotes, highlights, editedNotes, editedSummary, editedMarkdown, basedOnGeneratedAt, correctionDecisions
    }
    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        schemaVersion = try c.decodeIfPresent(Int.self, forKey: .schemaVersion) ?? 1
        guard schemaVersion <= 1 else { throw DecodingError.dataCorruptedError(forKey: .schemaVersion, in: c, debugDescription: "Newer user content format") }
        revision = try c.decodeIfPresent(Int.self, forKey: .revision) ?? 0
        updatedAt = try c.decodeIfPresent(Date.self, forKey: .updatedAt) ?? .distantPast
        personalNotes = try c.decodeIfPresent(String.self, forKey: .personalNotes) ?? ""
        highlights = try c.decodeIfPresent([SessionHighlight].self, forKey: .highlights) ?? []
        editedNotes = try c.decodeIfPresent(StructuredNotes.self, forKey: .editedNotes)
        editedSummary = try c.decodeIfPresent(SessionSummary.self, forKey: .editedSummary)
        editedMarkdown = try c.decodeIfPresent(String.self, forKey: .editedMarkdown)
        basedOnGeneratedAt = try c.decodeIfPresent(Date.self, forKey: .basedOnGeneratedAt)
        correctionDecisions = try c.decodeIfPresent([SessionCorrectionDecision].self, forKey: .correctionDecisions) ?? []
    }
}

public enum SessionContentTarget: Hashable, Sendable {
    case transcript(UUID)
    case answer(UUID)
    case notes(UUID?)
    case personalNotes
    case highlight(UUID)
}

/// Missing status in an old answer means it finished. Unknown future statuses stay visibly incomplete.
public enum AnswerCompletion: String, Codable, Sendable {
    case complete, interrupted, failed

    public init(from decoder: Decoder) throws {
        let raw = try decoder.singleValueContainer().decode(String.self)
        self = Self(rawValue: raw) ?? .interrupted
    }
}

/// Called on the archive's serial IO queue. Corrupt/newer files are never replaced with an empty draft.
public enum SessionUserContentStore {
    public enum Failure: LocalizedError {
        case changedElsewhere
        public var errorDescription: String? { "这场记录已更新，请载入最新内容后重试。你的草稿仍然保留。" }
    }

    public static func load(from directory: URL) throws -> SessionUserContent {
        let url = directory.appendingPathComponent(SessionUserContent.filename)
        guard FileManager.default.fileExists(atPath: url.path) else { return .init() }
        return try JSONLines.decoder().decode(SessionUserContent.self, from: Data(contentsOf: url))
    }

    @discardableResult
    public static func save(_ content: SessionUserContent, to directory: URL) throws -> SessionUserContent {
        let latest = try load(from: directory)
        guard content.revision == latest.revision else { throw Failure.changedElsewhere }
        var committed = content
        committed.revision = latest.revision + 1
        committed.updatedAt = Date()
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let data = try JSONLines.encoder().encode(committed)
        try data.write(to: directory.appendingPathComponent(SessionUserContent.filename), options: .atomic)
        return committed
    }
}
