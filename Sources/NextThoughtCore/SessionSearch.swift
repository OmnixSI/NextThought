import Foundation

/// Compatibility conveniences for the source-aware hits shared by history, projection and export.
public extension SessionSearchHit {
    var userContentTarget: SessionContentTarget? {
        if id == "user:personal-notes" { return .personalNotes }
        if let id, id.hasPrefix("user:highlight:"), let uuid = UUID(uuidString: String(id.dropFirst("user:highlight:".count))) { return .highlight(uuid) }
        return nil
    }
    var target: SessionContentTarget {
        if let target = userContentTarget { return target }
        if let lineID { return .transcript(lineID) }
        let components = id?.split(separator: ":").map(String.init) ?? []
        let uuid = components.compactMap(UUID.init(uuidString:)).last
        if components.first == "answer", let uuid { return .answer(uuid) }
        return .notes(components.first == "notes" ? uuid : nil)
    }
    var snippet: String { excerpt }
    var misheardForm: String? { isMisheard ? matchedForm : nil }
    var label: String {
        if id == "user:personal-notes" { return "个人笔记" }
        if id?.hasPrefix("user:highlight:") == true { return "重点标记" }
        switch kind {
        case .transcript: return "转写"
        case .correctedTranscript: return "校对转写"
        case .translation: return "译文"
        case .question: return "问题"
        case .answer: return "会中回答"
        case .notes: return "纪要"
        }
    }
}

public struct SessionSearchProgress: Sendable {
    public var checked: Int
    public var total: Int
    public var unreadable: Int
    public var hits: [UUID: SessionSearchHit]
    public var hitLists: [UUID: [SessionSearchHit]] = [:]
    public var issues: [UUID: String] = [:]
}

/// Searchable text only: canonical projection without materials/context or screenshots.
public struct SessionSearchSnapshot: Sendable {
    public var fields: [SessionSearchField]
    public var readIssues: [String] = []
    public init(meta: SessionMeta? = nil, lines: [SessionTranscriptLine], questions: [SessionQuestionEvent], answers: [SessionAnswerEvent],
                notes: StructuredNotes?, summary: SessionSummary?, user: SessionUserContent = .init()) {
        let meta = meta ?? SessionMeta(startedAt: lines.first?.date ?? Date(timeIntervalSince1970: 0), mode: .general)
        var effectiveNotes = user.editedNotes ?? notes
        effectiveNotes?.corrections = user.corrections(generated: notes?.corrections ?? [], lines: lines)
        var effectiveSummary = user.editedSummary ?? summary
        if user.editedSummary == nil {
            if let edited = user.editedNotes { effectiveSummary?.markdown = NotesMarkdown.render(edited, title: meta.displayTitle, facts: "") }
            else if let edited = user.editedMarkdown { effectiveSummary?.markdown = edited }
        }
        var answerOrder: [UUID] = []
        var finalAnswers: [UUID: SessionAnswerEvent] = [:]
        for answer in answers {
            if finalAnswers[answer.id] == nil { answerOrder.append(answer.id) }
            finalAnswers[answer.id] = answer
        }
        let latestAnswers = answerOrder.compactMap { finalAnswers[$0] }
        fields = SessionContentSearch.fields(meta: meta, lines: lines, questions: questions, answers: latestAnswers,
                                             summary: effectiveSummary, notes: effectiveNotes)
        if !user.personalNotes.isEmpty {
            fields.append(.init(kind: .notes, text: user.personalNotes, id: "user:personal-notes"))
        }
        for highlight in user.highlights {
            fields.append(.init(kind: .notes, text: highlight.quote, location: NotesTime.text(highlight.offsetSeconds),
                                id: "user:highlight:" + highlight.id.uuidString, lineID: highlight.lineID))
        }
    }
    public var byteCount: Int {
        fields.reduce(0) { $0 + $1.text.utf8.count + ($1.alternateText?.utf8.count ?? 0) + ($1.id?.utf8.count ?? 0) + 160 }
    }
}

/// Cache eviction affects latency, never search coverage. Matching preserves source revisions and all hits.
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
                       termsBySession: [UUID: GlossarySearch] = [:], demo: [UUID: SessionSearchSnapshot] = [:],
                       progress: @Sendable (SessionSearchProgress) -> Void = { _ in }) async throws -> SessionSearchProgress {
        try Task.checkCancellation()
        if self.revision != revision { cache.removeAll(); cachedByteCount = 0; self.revision = revision }
        var result = SessionSearchProgress(checked: 0, total: ids.count, unreadable: 0, hits: [:])
        for id in ids {
            try Task.checkCancellation()
            do {
                let snapshot = try demo[id] ?? self.snapshot(id)
                let hits = SessionContentSearch.hits(in: snapshot.fields, terms: termsBySession[id] ?? terms)
                if let hit = hits.first { result.hits[id] = hit; result.hitLists[id] = hits }
                if !snapshot.readIssues.isEmpty { result.unreadable += 1; result.issues[id] = snapshot.readIssues.joined(separator: "\n") }
            } catch is CancellationError { throw CancellationError() }
            catch { result.unreadable += 1; result.issues[id] = error.localizedDescription }
            result.checked += 1
            if result.checked % 20 == 0 || result.checked == result.total { progress(result) }
            await Task.yield()
        }
        try Task.checkCancellation()
        return result
    }

    private func snapshot(_ id: UUID) throws -> SessionSearchSnapshot {
        let folder = root.appendingPathComponent(id.uuidString, isDirectory: true), fm = FileManager.default
        guard fm.fileExists(atPath: folder.path) else { throw CocoaError(.fileNoSuchFile) }
        let names = ["meta.json", "transcript.jsonl", "translations.jsonl", "voices.jsonl", "questions.jsonl", "answers.jsonl", "notes.json", "summary.json", SessionUserContent.filename]
        var stamps: [String] = []
        for name in names {
            if let attrs = try? fm.attributesOfItem(atPath: folder.appendingPathComponent(name).path) {
                stamps.append("\(name):\(attrs[.modificationDate] ?? ""):\(attrs[.size] ?? 0):\(attrs[.systemFileNumber] ?? 0)")
            }
        }
        let stamp = stamps.joined(separator: "|")
        clock &+= 1
        if var entry = cache[id], entry.stamp == stamp { entry.used = clock; cache[id] = entry; return entry.snapshot }
        var issues: [String] = []
        let decoder = JSONLines.decoder()
        func read<T: Decodable>(_ name: String, as type: T.Type, required: Bool = false) -> T? {
            let url = folder.appendingPathComponent(name)
            guard fm.fileExists(atPath: url.path) else { if required { issues.append("归档文件缺失：" + name) }; return nil }
            do { return try decoder.decode(type, from: Data(contentsOf: url)) }
            catch { issues.append("无法读取 \(name)：\(error.localizedDescription)"); return nil }
        }
        func lines<T: Decodable>(_ name: String, as type: T.Type, expected: Int? = nil) throws -> [T] {
            try Task.checkCancellation()
            let url = folder.appendingPathComponent(name)
            guard fm.fileExists(atPath: url.path) else { if (expected ?? 0) > 0 { issues.append("归档文件缺失：" + name) }; return [] }
            do {
                let report = JSONLines.decodeReport(type, from: try Data(contentsOf: url, options: .mappedIfSafe))
                if !report.malformedLineNumbers.isEmpty { issues.append("\(name) 有 \(report.malformedLineNumbers.count) 行无法解析。") }
                if let expected, report.records.count != expected { issues.append("\(name) 读取条数与归档不一致。") }
                return report.records
            } catch { issues.append("无法读取 \(name)：\(error.localizedDescription)"); return [] }
        }
        let meta = read("meta.json", as: SessionMeta.self)
        var transcript = try lines("transcript.jsonl", as: SessionTranscriptLine.self, expected: meta?.transcriptCount)
        let translations = Dictionary(try lines("translations.jsonl", as: SessionTranscriptTranslation.self).map { ($0.id, $0) }, uniquingKeysWith: { _, last in last })
        let voices = Dictionary(try lines("voices.jsonl", as: SessionVoiceTag.self).map { ($0.id, $0.voice) }, uniquingKeysWith: { _, last in last })
        for index in transcript.indices {
            if let translation = translations[transcript[index].id] {
                transcript[index].translatedText = translation.text; transcript[index].translationInputRevision = translation.inputRevision
            }
            if let voice = voices[transcript[index].id] { transcript[index].voice = voice }
        }
        let questions = try lines("questions.jsonl", as: SessionQuestionEvent.self, expected: meta?.questionCount)
        let answers = try lines("answers.jsonl", as: SessionAnswerEvent.self)
        if let meta, Set(answers.map(\.id)).count != meta.answerCount { issues.append("answers.jsonl 读取条数与归档不一致。") }
        let notes = read("notes.json", as: StructuredNotes.self)
        let summary = read("summary.json", as: SessionSummary.self, required: meta?.summaryState == .ready)
        let user = read(SessionUserContent.filename, as: SessionUserContent.self) ?? .init()
        var value = SessionSearchSnapshot(meta: meta, lines: transcript, questions: questions, answers: answers, notes: notes, summary: summary, user: user)
        value.readIssues = issues
        if let previous = cache.removeValue(forKey: id) { cachedByteCount -= previous.snapshot.byteCount }
        if value.byteCount <= cacheLimit {
            while cachedByteCount + value.byteCount > cacheLimit, let victim = cache.min(by: { $0.value.used < $1.value.used }) {
                cachedByteCount -= victim.value.snapshot.byteCount; cache.removeValue(forKey: victim.key)
            }
            cache[id] = Entry(stamp: stamp, snapshot: value, used: clock); cachedByteCount += value.byteCount
        }
        return value
    }
}
