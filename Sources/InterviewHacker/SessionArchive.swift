import AppKit
import Foundation
import InterviewHackerCore
import Observation

/// Everything recorded for one session, loaded on demand for the history view.
struct SessionDetail: Equatable {
    var meta: SessionMeta
    var lines: [SessionTranscriptLine]
    var answers: [SessionAnswerEvent]
    var questions: [SessionQuestionEvent]
    var context: SessionContextSnapshot?
    var summary: SessionSummary?
}

/// Local session archive: one folder per session under
/// `~/Library/Application Support/InterviewHacker/Sessions/<id>/`:
///
///     meta.json          title, times, counts, summary state
///     context.json       mode, answer requirements, background and imported files
///     transcript.jsonl   every final transcript line (both channels), appended live
///     questions.jsonl    every question the detector saw
///     answers.jsonl      every generated answer
///     summary.json       model-written summary and question list (after the session)
///
/// Lines are appended as they happen, so a crash or force-quit loses at most the
/// utterance being spoken. Raw audio is never stored.
@MainActor
@Observable
final class SessionArchive {
    static let shared = SessionArchive()

    private(set) var sessions: [SessionMeta] = []
    private(set) var activeID: UUID?
    /// Sessions whose summary is being generated right now.
    var summarizing: Set<UUID> = []

    private let io = DispatchQueue(label: "app.interviewhacker.archive", qos: .utility)
    @ObservationIgnored private var activeMeta: SessionMeta?
    @ObservationIgnored private var demoDetails: [UUID: SessionDetail] = [:]

    static var root: URL { AppPaths.sessions }

    func folder(_ id: UUID) -> URL { Self.root.appendingPathComponent(id.uuidString, isDirectory: true) }

    private init() {}

    // MARK: Loading

    func load() {
        guard !DemoMode.isEnabled else { return }
        let root = Self.root
        Task {
            let metas = await Task.detached(priority: .utility) { () -> [SessionMeta] in
                Self.migrateLegacySessions(into: root)
                let folders = (try? FileManager.default.contentsOfDirectory(at: root, includingPropertiesForKeys: nil)) ?? []
                return folders.compactMap { folder -> SessionMeta? in
                    guard let data = try? Data(contentsOf: folder.appendingPathComponent("meta.json")),
                          var meta = try? JSONLines.decoder().decode(SessionMeta.self, from: data) else { return nil }
                    if meta.endedAt == nil {
                        // Interrupted session: end it at the last transcript write.
                        let transcript = folder.appendingPathComponent("transcript.jsonl")
                        let modified = (try? FileManager.default.attributesOfItem(atPath: transcript.path)[.modificationDate]) as? Date
                        meta.endedAt = modified ?? meta.startedAt
                        if meta.summaryState == .pending { meta.summaryState = .none }
                    }
                    return meta
                }.sorted { $0.startedAt > $1.startedAt }
            }.value
            sessions = metas.filter { $0.id != activeID } + (activeMeta.map { [$0] } ?? [])
            sessions.sort { $0.startedAt > $1.startedAt }
        }
    }

    /// Screenshot mode: sample sessions that never touch the disk.
    func installDemo(_ details: [SessionDetail]) {
        demoDetails = Dictionary(uniqueKeysWithValues: details.map { ($0.meta.id, $0) })
        sessions = details.map(\.meta)
    }

    func detail(_ id: UUID) async -> SessionDetail? {
        if let demo = demoDetails[id] { return demo }
        let folder = folder(id)
        let fallback = sessions.first { $0.id == id }
        return await Task.detached(priority: .userInitiated) { () -> SessionDetail? in
            let decoder = JSONLines.decoder()
            let meta = (try? Data(contentsOf: folder.appendingPathComponent("meta.json"))).flatMap { try? decoder.decode(SessionMeta.self, from: $0) } ?? fallback
            guard let meta else { return nil }
            func lines<T: Decodable>(_ name: String, _ type: T.Type) -> [T] {
                (try? Data(contentsOf: folder.appendingPathComponent(name))).map { JSONLines.decode(T.self, from: $0) } ?? []
            }
            return SessionDetail(
                meta: meta,
                lines: lines("transcript.jsonl", SessionTranscriptLine.self),
                answers: lines("answers.jsonl", SessionAnswerEvent.self),
                questions: lines("questions.jsonl", SessionQuestionEvent.self),
                context: (try? Data(contentsOf: folder.appendingPathComponent("context.json"))).flatMap { try? decoder.decode(SessionContextSnapshot.self, from: $0) },
                summary: (try? Data(contentsOf: folder.appendingPathComponent("summary.json"))).flatMap { try? decoder.decode(SessionSummary.self, from: $0) })
        }.value
    }

    // MARK: Recording

    func begin(_ meta: SessionMeta, context: SessionContextSnapshot) {
        activeID = meta.id
        activeMeta = meta
        sessions.removeAll { $0.id == meta.id }
        sessions.insert(meta, at: 0)
        let folder = folder(meta.id)
        io.async {
            try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            Self.write(meta, to: folder.appendingPathComponent("meta.json"))
            Self.write(context, to: folder.appendingPathComponent("context.json"))
        }
    }

    func append(_ line: SessionTranscriptLine) {
        guard let id = activeID else { return }
        activeMeta?.transcriptCount += 1
        appendLine(line, file: "transcript.jsonl", session: id)
        publishActiveCounts()
    }

    func append(_ question: SessionQuestionEvent) {
        guard let id = activeID else { return }
        activeMeta?.questionCount += 1
        appendLine(question, file: "questions.jsonl", session: id)
        publishActiveCounts()
    }

    func append(_ answer: SessionAnswerEvent) {
        guard let id = activeID else { return }
        activeMeta?.answerCount += 1
        appendLine(answer, file: "answers.jsonl", session: id)
        publishActiveCounts()
        // Keep meta.json reasonably fresh for crash recovery.
        if let meta = activeMeta { saveMeta(meta) }
    }

    /// Ends the active session. Sessions without any transcript are discarded.
    @discardableResult
    func finish(at date: Date = Date()) -> SessionMeta? {
        guard var meta = activeMeta else { return nil }
        activeID = nil
        activeMeta = nil
        meta.endedAt = date
        if meta.transcriptCount == 0 && meta.answerCount == 0 {
            sessions.removeAll { $0.id == meta.id }
            let folder = folder(meta.id)
            io.async { try? FileManager.default.removeItem(at: folder) }
            return nil
        }
        update(meta)
        return meta
    }

    func update(_ meta: SessionMeta) {
        if let index = sessions.firstIndex(where: { $0.id == meta.id }) { sessions[index] = meta } else { sessions.insert(meta, at: 0) }
        if activeID == meta.id { activeMeta = meta }
        saveMeta(meta)
    }

    func saveSummary(_ summary: SessionSummary, for id: UUID) {
        guard var meta = sessions.first(where: { $0.id == id }) else { return }
        meta.title = summary.title.isEmpty ? meta.title : summary.title
        meta.summaryState = .ready
        meta.summaryError = nil
        let folder = folder(id)
        io.async { Self.write(summary, to: folder.appendingPathComponent("summary.json")) }
        update(meta)
    }

    func rename(_ id: UUID, to title: String) {
        guard var meta = sessions.first(where: { $0.id == id }) else { return }
        meta.title = title.trimmingCharacters(in: .whitespacesAndNewlines)
        update(meta)
    }

    func delete(_ id: UUID) {
        guard id != activeID else { return }
        sessions.removeAll { $0.id == id }
        let folder = folder(id)
        io.async { try? FileManager.default.removeItem(at: folder) }
    }

    func deleteAll() {
        for meta in sessions where meta.id != activeID { delete(meta.id) }
    }

    func revealInFinder(_ id: UUID) {
        NSWorkspace.shared.activateFileViewerSelecting([folder(id)])
    }

    /// Waits until queued writes are on disk (before reading a session back).
    func flush() async {
        await withCheckedContinuation { continuation in io.async { continuation.resume() } }
    }

    // MARK: Private

    private func publishActiveCounts() {
        guard let meta = activeMeta, let index = sessions.firstIndex(where: { $0.id == meta.id }) else { return }
        sessions[index] = meta
    }

    private func saveMeta(_ meta: SessionMeta) {
        let url = folder(meta.id).appendingPathComponent("meta.json")
        io.async { Self.write(meta, to: url) }
    }

    private func appendLine<T: Encodable>(_ value: T, file: String, session id: UUID) {
        let url = folder(id).appendingPathComponent(file)
        guard let data = try? JSONLines.line(value) else { return }
        io.async {
            if !FileManager.default.fileExists(atPath: url.path) {
                try? FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
                FileManager.default.createFile(atPath: url.path, contents: nil)
            }
            guard let handle = try? FileHandle(forWritingTo: url) else { return }
            defer { try? handle.close() }
            _ = try? handle.seekToEnd()
            try? handle.write(contentsOf: data)
        }
    }

    nonisolated private static func write<T: Encodable>(_ value: T, to url: URL) {
        let encoder = JSONLines.encoder()
        encoder.outputFormatting.insert(.prettyPrinted)
        guard let data = try? encoder.encode(value) else { return }
        try? FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        try? data.write(to: url, options: .atomic)
    }

    /// Converts the single `sessions.json` written by earlier versions, once.
    nonisolated private static func migrateLegacySessions(into root: URL) {
        let legacy = AppPaths.legacySessions
        guard FileManager.default.fileExists(atPath: legacy.path),
              let data = try? Data(contentsOf: legacy),
              let old = try? JSONDecoder().decode([SavedSession].self, from: data) else { return }
        for session in old {
            let meta = SessionMeta(startedAt: session.startedAt, endedAt: session.endedAt,
                                   mode: SessionMode(rawValue: session.mode ?? "") ?? .meeting,
                                   transcriptCount: session.entries.count, answerCount: session.records?.count ?? session.answers.count)
            let folder = root.appendingPathComponent(meta.id.uuidString, isDirectory: true)
            try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            write(meta, to: folder.appendingPathComponent("meta.json"))
            let lines = session.entries.compactMap { entry in
                try? JSONLines.line(SessionTranscriptLine(date: entry.date, speaker: entry.speaker, text: entry.text, startedAt: entry.startedAt, endedAt: entry.endedAt))
            }
            try? lines.reduce(Data(), +).write(to: folder.appendingPathComponent("transcript.jsonl"))
            let answers = (session.records ?? []).compactMap { record in
                try? JSONLines.line(SessionAnswerEvent(date: record.date, question: record.question, answer: record.answer, model: record.model,
                                                       seconds: record.seconds, firstTokenSeconds: record.firstTokenSeconds, automatic: record.automatic))
            }
            try? answers.reduce(Data(), +).write(to: folder.appendingPathComponent("answers.jsonl"))
        }
        try? FileManager.default.moveItem(at: legacy, to: legacy.deletingLastPathComponent().appendingPathComponent("sessions.legacy.json"))
    }
}
