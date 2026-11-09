import Foundation

public struct SessionSearchField: Sendable, Equatable {
    public var text: String
    public var label: String
    public var target: SessionContentTarget
    public init(_ text: String, label: String, target: SessionContentTarget) { self.text = text; self.label = label; self.target = target }
}

public struct SessionSearchHit: Sendable, Equatable {
    public var sessionID: UUID
    public var snippet: String
    public var label: String
    public var target: SessionContentTarget
    public var misheardForm: String?
}

public struct SessionSearchProgress: Sendable {
    public var checked: Int
    public var total: Int
    public var unreadable: Int
    public var hits: [UUID: SessionSearchHit]
}

/// Searchable text only: never loads a meeting's large materials/context or screenshots.
public struct SessionSearchSnapshot: Sendable {
    public var fields: [SessionSearchField]
    public init(lines: [SessionTranscriptLine], questions: [SessionQuestionEvent], answers: [SessionAnswerEvent],
                notes: StructuredNotes?, summary: SessionSummary?, user: SessionUserContent = .init()) {
        var fields: [SessionSearchField] = []
        let displayedNotes = user.editedNotes ?? notes
        let corrections = user.corrections(generated: displayedNotes?.corrections ?? [], lines: lines)
        let corrected = NotesCorrections.apply(corrections, to: lines)
        for (index, line) in lines.enumerated() {
            fields.append(.init(line.text, label: "转写", target: .transcript(line.id)))
            if corrected[index].text != line.text { fields.append(.init(corrected[index].text, label: "校对转写", target: .transcript(line.id))) }
            if let translation = line.translatedText { fields.append(.init(translation, label: "译文", target: .transcript(line.id))) }
        }
        for question in questions { fields.append(.init(question.text, label: "问题", target: .notes(nil))) }
        var latest: [UUID: SessionAnswerEvent] = [:]
        for answer in answers { latest[answer.id] = answer }
        for answer in latest.values.sorted(by: { $0.date < $1.date }) {
            fields.append(.init(answer.question + "\n" + answer.answer + "\n" + (answer.webAnswer ?? ""), label: "会中回答", target: .answer(answer.id)))
        }
        if let notes = displayedNotes {
            fields.append(.init(notes.title, label: "纪要", target: .notes(nil)))
            for section in notes.sections {
                for item in section.items {
                    let text = [section.title, item.text, item.answer, item.better, item.owner, item.due, item.quote].compactMap { $0 }.joined(separator: "\n")
                    fields.append(.init(text, label: "纪要 · " + section.title, target: .notes(item.id)))
                }
            }
        }
        if let markdown = user.editedMarkdown ?? summary?.markdown { fields.append(.init(markdown, label: "纪要", target: .notes(nil))) }
        if let summary {
            for payload in summary.structuredAll ?? summary.structured.map({ [$0] }) ?? [] {
                fields.append(.init(StructuredNotesMarkdown.render(payload), label: "结构化纪要", target: .notes(nil)))
            }
        }
        fields.append(.init(user.personalNotes, label: "个人笔记", target: .personalNotes))
        fields += user.highlights.map { .init($0.quote, label: "重点标记", target: .highlight($0.id)) }
        self.fields = fields.filter { !$0.text.isEmpty }
    }

    public var byteCount: Int { fields.reduce(0) { $0 + $1.text.utf8.count + $1.label.utf8.count + 96 } }
}

/// An evictable cache accelerates repeated searches; eviction never limits coverage.
public actor SessionSearch {
    public static let defaultCacheLimit = 32 * 1024 * 1024
    private struct Entry { var stamp: String; var snapshot: SessionSearchSnapshot; var used: UInt64 }
    private let root: URL
    private let cacheLimit: Int
    private var cache: [UUID: Entry] = [:]
    private var clock: UInt64 = 0
    private var revision: Int?
    public private(set) var cachedByteCount = 0

    public init(root: URL, cacheLimit: Int = SessionSearch.defaultCacheLimit) { self.root = root; self.cacheLimit = max(0, cacheLimit) }

    public func search(ids: [UUID], terms: GlossarySearch, revision: Int,
                       demo: [UUID: SessionSearchSnapshot] = [:],
                       progress: @Sendable (SessionSearchProgress) -> Void = { _ in }) async throws -> SessionSearchProgress {
        if self.revision != revision { cache.removeAll(); cachedByteCount = 0; self.revision = revision }
        var result = SessionSearchProgress(checked: 0, total: ids.count, unreadable: 0, hits: [:])
        for id in ids {
            try Task.checkCancellation()
            do {
                let snapshot = try demo[id] ?? self.snapshot(id)
                var chosen: SessionSearchHit?
                for field in snapshot.fields {
                    guard let hit = terms.firstMatch(in: field.text) else { continue }
                    let found = SessionSearchHit(sessionID: id, snippet: Self.snippet(field.text, term: hit.form), label: field.label,
                                                 target: field.target, misheardForm: hit.isMisheard ? hit.form : nil)
                    if chosen == nil || (chosen?.misheardForm != nil && !hit.isMisheard) { chosen = found }
                    if !hit.isMisheard { break }
                }
                if let chosen { result.hits[id] = chosen }
            } catch is CancellationError { throw CancellationError() }
            catch { result.unreadable += 1 }
            result.checked += 1
            if result.checked % 20 == 0 || result.checked == result.total { progress(result) }
            await Task.yield()
        }
        try Task.checkCancellation()
        return result
    }

    private func snapshot(_ id: UUID) throws -> SessionSearchSnapshot {
        let folder = root.appendingPathComponent(id.uuidString, isDirectory: true)
        let names = ["transcript.jsonl", "translations.jsonl", "questions.jsonl", "answers.jsonl", "notes.json", "summary.json", SessionUserContent.filename]
        let fm = FileManager.default
        guard fm.fileExists(atPath: folder.path) else { throw CocoaError(.fileNoSuchFile) }
        var stamps: [String] = []
        for name in names {
            if let attrs = try? fm.attributesOfItem(atPath: folder.appendingPathComponent(name).path) {
                stamps.append("\(name):\(attrs[.modificationDate] ?? ""):\(attrs[.size] ?? 0):\(attrs[.systemFileNumber] ?? 0)")
            }
        }
        let stamp = stamps.joined(separator: "|")
        clock &+= 1
        if var entry = cache[id], entry.stamp == stamp { entry.used = clock; cache[id] = entry; return entry.snapshot }
        let decoder = JSONLines.decoder()
        func read<T: Decodable>(_ name: String, as type: T.Type) throws -> T? {
            let url = folder.appendingPathComponent(name)
            guard fm.fileExists(atPath: url.path) else { return nil }
            return try decoder.decode(type, from: Data(contentsOf: url))
        }
        func lines<T: Decodable>(_ name: String, as type: T.Type) throws -> [T] {
            try Task.checkCancellation()
            let url = folder.appendingPathComponent(name)
            guard fm.fileExists(atPath: url.path) else { return [] }
            return JSONLines.decode(type, from: try Data(contentsOf: url, options: .mappedIfSafe))
        }
        var transcript = try lines("transcript.jsonl", as: SessionTranscriptLine.self)
        let translations = Dictionary(try lines("translations.jsonl", as: SessionTranscriptTranslation.self).map { ($0.id, $0.text) }, uniquingKeysWith: { _, last in last })
        for index in transcript.indices { if let value = translations[transcript[index].id] { transcript[index].translatedText = value } }
        let value = try SessionSearchSnapshot(lines: transcript, questions: lines("questions.jsonl", as: SessionQuestionEvent.self),
            answers: lines("answers.jsonl", as: SessionAnswerEvent.self), notes: read("notes.json", as: StructuredNotes.self),
            summary: read("summary.json", as: SessionSummary.self), user: read(SessionUserContent.filename, as: SessionUserContent.self) ?? .init())
        if let previous = cache.removeValue(forKey: id) { cachedByteCount -= previous.snapshot.byteCount }
        if value.byteCount <= cacheLimit {
            while cachedByteCount + value.byteCount > cacheLimit, let victim = cache.min(by: { $0.value.used < $1.value.used }) {
                cachedByteCount -= victim.value.snapshot.byteCount; cache.removeValue(forKey: victim.key)
            }
            cache[id] = Entry(stamp: stamp, snapshot: value, used: clock); cachedByteCount += value.byteCount
        }
        return value
    }

    private static func snippet(_ text: String, term: String) -> String {
        let clean = text.replacingOccurrences(of: "\n", with: " ")
        guard let range = clean.range(of: term, options: [.caseInsensitive, .widthInsensitive]) else { return String(clean.prefix(140)) }
        let start = clean.index(range.lowerBound, offsetBy: -48, limitedBy: clean.startIndex) ?? clean.startIndex
        let end = clean.index(range.upperBound, offsetBy: 92, limitedBy: clean.endIndex) ?? clean.endIndex
        return (start == clean.startIndex ? "" : "…") + String(clean[start..<end]) + (end == clean.endIndex ? "" : "…")
    }
}
