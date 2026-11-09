import AppKit
import Foundation
import NextThoughtCore
import Observation

/// Everything recorded for one session, loaded on demand for the history view.
struct SessionDetail: Equatable {
    var meta: SessionMeta
    var lines: [SessionTranscriptLine]
    var answers: [SessionAnswerEvent]
    var questions: [SessionQuestionEvent]
    var context: SessionContextSnapshot?
    var summary: SessionSummary?
    /// Structured notes (notes.json); nil for sessions summarized before them.
    var notes: StructuredNotes? = nil
    var readIssues: [SessionArchiveReadIssue] = []
}

enum SessionArchiveEditResult: Equatable {
    case saved
    case busy
    case staleTarget
    case unavailable
    case persistenceBlocked

    var failureMessage: String? {
        switch self {
        case .saved: nil
        case .busy: String(localized: "纪要正在重新整理，本次编辑没有保存。完成后请重新打开再试。")
        case .staleTarget: String(localized: "纪要内容已更新，本次编辑没有保存。请重新打开后再试。")
        case .unavailable: String(localized: "无法读取这份纪要，本次编辑没有保存。请刷新后重试。")
        case .persistenceBlocked: String(localized: "归档保存受阻，本次编辑没有保存。请先重试保存。")
        }
    }
}

enum SessionArchiveReadIssue: Equatable {
    case missingFile(String)
    case unreadableFile(String, String)
    case malformedLines(String, firstLine: Int, count: Int, truncatedTail: Bool)
    case countMismatch(String, expected: Int, actual: Int)

    var message: String {
        switch self {
        case .missingFile(let file): "归档文件缺失：\(file)"
        case .unreadableFile(let file, let reason): "无法读取 \(file)：\(reason)"
        case .malformedLines(let file, let firstLine, let count, let truncatedTail):
            "\(file) 有 \(count) 行无法解析（从第 \(firstLine) 行开始）" + (truncatedTail ? "；最后一行可能未写完。" : "。")
        case .countMismatch(let file, let expected, let actual): "\(file) 只读到 \(actual) 条，记录中应有 \(expected) 条。"
        }
    }
}

enum SessionStorageInfo: Equatable, Sendable {
    case available(path: String, bytes: Int64)
    case missing(path: String)
    case unavailable(path: String)
    case demo
}

/// Local session archive: one folder per session under
/// `~/Library/Application Support/NextThought/Sessions/<id>/`:
///
///     meta.json          title, times, counts, summary state
///     context.json       mode, answer requirements, background and imported files
///     transcript.jsonl   every final transcript line (both channels), appended live
///     translations.jsonl translated final lines, keyed by transcript line ID
///     voices.jsonl       which voice said a line (区分对方的每个人), keyed by transcript line ID; the
///                        latest tag of a line wins, so merging two voices just appends new tags
///     questions.jsonl    every question the detector saw
///     answers.jsonl      every generated answer
///     summary.json       model-written summary and question list (after the session)
///     notes.json         structured notes: template sections, times, corrections
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
    /// The step the notes of a session are at while they are written.
    var notesSteps: [UUID: NotesStep] = [:]
    /// Bumped when notes change on disk, so open pages reload them.
    var notesRevision = 0
    /// Per-session revision for saved translations, so only that meeting page reloads.
    private(set) var translationRevisionBySession: [UUID: Int] = [:]
    private(set) var persistenceBySession: [UUID: SessionPersistenceSnapshot] = [:]
    /// Bumped when finalized, searchable content is durably saved.
    private(set) var searchableContentRevision = 0
    /// Newly finalized meetings stay out of history search until final translations have drained.
    private(set) var pendingTranslationSearchSessionIDs: Set<UUID> = []

    private let io = DispatchQueue(label: "app.nextthought.archive", qos: .utility)
    @ObservationIgnored private var activeMeta: SessionMeta?
    @ObservationIgnored private var activeAnswerIDs: Set<UUID> = []
    @ObservationIgnored private var demoDetails: [UUID: SessionDetail] = [:]
    @ObservationIgnored private var writeQueues: [UUID: SessionArchiveWriteQueue] = [:]
    @ObservationIgnored private var drainingWrites: Set<UUID> = []
    @ObservationIgnored private var discardedSessions: Set<UUID> = []
    @ObservationIgnored private var deletedSessionIDs: Set<UUID> = []
    @ObservationIgnored var summaryTasks: [UUID: Task<Void, Never>] = [:]
    @ObservationIgnored private var retryTasks: [UUID: Task<Void, Never>] = [:]
    @ObservationIgnored private var retryAttempts: [UUID: Int] = [:]
    @ObservationIgnored private let fileAppender = SessionArchiveFileAppender.live
    @ObservationIgnored private var pendingSearchableTranslationSessionIDs: Set<UUID> = []
    @ObservationIgnored private var completedTranslationDrainSessionIDs: Set<UUID> = []
    @ObservationIgnored private var translationWriteReceipts: [UUID: SessionTranslationWriteReceipts] = [:]

    static var root: URL { AppPaths.sessions }

    func folder(_ id: UUID) -> URL { Self.root.appendingPathComponent(id.uuidString, isDirectory: true) }

    private init() {}

    // MARK: Loading

    func load(onRecoveredSummaries: @escaping @MainActor ([UUID]) -> Void = { _ in }) {
        guard !DemoMode.isEnabled else { return }
        let root = Self.root
        // A load can also happen after restoring a backup while the app remains
        // open. Do not mistake work that is genuinely running in this process
        // for an orphan left by a previous launch.
        let activeSummaries = summarizing
        Task {
            let result = await Task.detached(priority: .utility) { () -> (metas: [SessionMeta], recovered: [UUID]) in
                Self.migrateLegacySessions(into: root)
                let folders = (try? FileManager.default.contentsOfDirectory(at: root, includingPropertiesForKeys: nil)) ?? []
                var recovered = Set<UUID>()
                let metas = folders.compactMap { folder -> SessionMeta? in
                    let metaURL = folder.appendingPathComponent("meta.json")
                    guard let data = try? Data(contentsOf: metaURL),
                          var meta = try? JSONLines.decoder().decode(SessionMeta.self, from: data) else { return nil }
                    var changed = false
                    if meta.endedAt == nil {
                        // Interrupted session: end it at the last transcript write.
                        let transcript = folder.appendingPathComponent("transcript.jsonl")
                        let transcriptData = try? Data(contentsOf: transcript)
                        let modified = (try? FileManager.default.attributesOfItem(atPath: transcript.path)[.modificationDate]) as? Date
                        if meta.recoverInterruptedRecording(transcriptData: transcriptData,
                                                            lastTranscriptWrite: modified) {
                            recovered.insert(meta.id)
                        }
                        changed = true
                    } else if meta.summaryState == .pending, !activeSummaries.contains(meta.id) {
                        // A finished meeting cannot still be generating after a
                        // relaunch. Clear the stale state and let AppState retry it.
                        meta.summaryState = .none
                        meta.summaryError = nil
                        changed = true
                        if meta.transcriptCount > 0 { recovered.insert(meta.id) }
                    }
                    if changed { Self.write(meta, to: metaURL) }
                    return meta
                }.sorted { $0.startedAt > $1.startedAt }
                // Process the backlog oldest-first so long-standing meetings are
                // not continually pushed behind newer ones.
                let orderedRecovered = metas.filter { recovered.contains($0.id) }.map(\.id).reversed()
                return (metas, Array(orderedRecovered))
            }.value
            sessions = result.metas.filter { $0.id != activeID && !deletedSessionIDs.contains($0.id) }
                + (activeMeta.map { [$0] } ?? [])
            sessions.sort { $0.startedAt > $1.startedAt }
            if !result.recovered.isEmpty { onRecoveredSummaries(result.recovered) }
        }
    }

    /// Screenshot mode: sample sessions that never touch the disk.
    func installDemo(_ details: [SessionDetail]) {
        demoDetails = Dictionary(uniqueKeysWithValues: details.map { ($0.meta.id, $0) })
        sessions = details.map(\.meta)
    }

    func detail(_ id: UUID) async -> SessionDetail? {
        guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else { return nil }
        if let demo = demoDetails[id] { return demo }
        let folder = folder(id)
        let fallback = sessions.first { $0.id == id }
        let result = await Task.detached(priority: .userInitiated) { () -> SessionDetail? in
            let decoder = JSONLines.decoder()
            var issues: [SessionArchiveReadIssue] = []
            func data(_ name: String, required: Bool = false) -> Data? {
                let url = folder.appendingPathComponent(name)
                do { return try Data(contentsOf: url) }
                catch {
                    let exists = FileManager.default.fileExists(atPath: url.path)
                    if required {
                        let cocoaError = error as NSError
                        if cocoaError.domain == NSCocoaErrorDomain && cocoaError.code == NSFileReadNoSuchFileError {
                            issues.append(.missingFile(name))
                        } else {
                            issues.append(.unreadableFile(name, error.localizedDescription))
                        }
                    } else if exists {
                        issues.append(.unreadableFile(name, error.localizedDescription))
                    }
                    return nil
                }
            }
            let decodedMeta: SessionMeta?
            if let bytes = data("meta.json", required: true) {
                do { decodedMeta = try decoder.decode(SessionMeta.self, from: bytes) }
                catch { issues.append(.unreadableFile("meta.json", error.localizedDescription)); decodedMeta = nil }
            } else { decodedMeta = nil }
            let meta = decodedMeta ?? fallback
            guard let meta else { return nil }
            func lines<T: Decodable>(_ name: String, _ type: T.Type, expected: Int? = nil, required: Bool? = nil) -> [T] {
                guard let bytes = data(name, required: required ?? ((expected ?? 0) > 0)) else { return [] }
                let report = JSONLines.decodeReport(type, from: bytes)
                if let firstLine = report.malformedLineNumbers.first {
                    issues.append(.malformedLines(name, firstLine: firstLine,
                                                  count: report.malformedLineNumbers.count,
                                                  truncatedTail: report.truncatedFinalLine))
                }
                if let expected, report.records.count != expected {
                    issues.append(.countMismatch(name, expected: expected, actual: report.records.count))
                }
                return report.records
            }
            var transcript = lines("transcript.jsonl", SessionTranscriptLine.self, expected: meta.transcriptCount)
            let translations = Dictionary(lines("translations.jsonl", SessionTranscriptTranslation.self).map { ($0.id, $0) },
                                          uniquingKeysWith: { _, latest in latest })
            for index in transcript.indices {
                if let translation = translations[transcript[index].id] {
                    transcript[index].translatedText = translation.text
                    transcript[index].translationInputRevision = translation.inputRevision
                }
            }
            let voices = Dictionary(lines("voices.jsonl", SessionVoiceTag.self).map { ($0.id, $0.voice) },
                                    uniquingKeysWith: { _, latest in latest })
            for index in transcript.indices {
                if let voice = voices[transcript[index].id] { transcript[index].voice = voice }
            }
            let rawAnswers = lines("answers.jsonl", SessionAnswerEvent.self, required: meta.answerCount > 0)
            let answers = Self.latestAnswers(rawAnswers)
            if answers.count != meta.answerCount && !issues.contains(.missingFile("answers.jsonl")) {
                issues.append(.countMismatch("answers.jsonl", expected: meta.answerCount, actual: answers.count))
            }
            let questions = lines("questions.jsonl", SessionQuestionEvent.self, expected: meta.questionCount)
            func snapshot<T: Decodable>(_ name: String, _ type: T.Type, required: Bool = false) -> T? {
                guard let bytes = data(name, required: required) else { return nil }
                do { return try decoder.decode(type, from: bytes) }
                catch { issues.append(.unreadableFile(name, error.localizedDescription)); return nil }
            }
            return SessionDetail(
                meta: meta,
                lines: transcript,
                answers: answers,
                questions: questions,
                context: snapshot("context.json", SessionContextSnapshot.self),
                summary: snapshot("summary.json", SessionSummary.self, required: meta.summaryState == .ready),
                notes: snapshot("notes.json", StructuredNotes.self),
                readIssues: issues)
        }.value
        guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else { return nil }
        return result
    }

    /// Reads the session directory size off the main actor. The reported size is
    /// the total of regular files in this meeting's folder and subfolders.
    func storageInfo(_ id: UUID) async -> SessionStorageInfo {
        guard demoDetails[id] == nil else { return .demo }
        let sessionFolder = folder(id)
        return await Task.detached(priority: .utility) {
            let attributes: [FileAttributeKey: Any]
            do {
                attributes = try FileManager.default.attributesOfItem(atPath: sessionFolder.path)
            } catch let error as NSError {
                if error.domain == NSCocoaErrorDomain && error.code == NSFileReadNoSuchFileError {
                    return .missing(path: sessionFolder.path)
                }
                return .unavailable(path: sessionFolder.path)
            } catch {
                return .unavailable(path: sessionFolder.path)
            }
            guard attributes[.type] as? FileAttributeType == .typeDirectory else {
                return .unavailable(path: sessionFolder.path)
            }
            do {
                let bytes = try Self.directorySize(at: sessionFolder)
                return .available(path: sessionFolder.path, bytes: bytes)
            } catch {
                return .unavailable(path: sessionFolder.path)
            }
        }.value
    }

    nonisolated static func latestAnswers(_ events: [SessionAnswerEvent]) -> [SessionAnswerEvent] {
        var order: [UUID] = [], latest: [UUID: SessionAnswerEvent] = [:]
        for event in events { if latest[event.id] == nil { order.append(event.id) }; latest[event.id] = event }
        return order.compactMap { latest[$0] }
    }

    // MARK: Recording

    func begin(_ meta: SessionMeta, context: SessionContextSnapshot) {
        deletedSessionIDs.remove(meta.id)
        discardedSessions.remove(meta.id)
        translationRevisionBySession[meta.id] = nil
        translationWriteReceipts[meta.id] = nil
        pendingSearchableTranslationSessionIDs.remove(meta.id)
        pendingTranslationSearchSessionIDs.remove(meta.id)
        completedTranslationDrainSessionIDs.remove(meta.id)
        activeID = meta.id
        activeAnswerIDs = []
        activeMeta = meta
        sessions.removeAll { $0.id == meta.id }
        sessions.insert(meta, at: 0)
        writeQueues[meta.id] = SessionArchiveWriteQueue()
        persistenceBySession[meta.id] = writeQueues[meta.id]?.snapshot
        _ = enqueueSnapshot(meta, file: "meta.json", session: meta.id)
        _ = enqueueSnapshot(context, file: "context.json", session: meta.id)
    }

    /// Replaces the session's initial context snapshot after optional carry material has loaded.
    @discardableResult
    func updateContext(_ context: SessionContextSnapshot, for id: UUID) -> Bool {
        if var detail = demoDetails[id] {
            detail.context = context
            demoDetails[id] = detail
            return true
        }
        guard sessions.contains(where: { $0.id == id }) else { return false }
        return enqueueSnapshot(context, file: "context.json", session: id)
    }

    @discardableResult func append(_ line: SessionTranscriptLine) -> Bool {
        guard let id = activeID else { return false }
        activeMeta?.transcriptCount += 1
        let saved = appendLine(line, file: "transcript.jsonl", session: id)
        publishActiveCounts()
        return saved
    }

    @discardableResult func append(_ question: SessionQuestionEvent) -> Bool {
        guard let id = activeID else { return false }
        activeMeta?.questionCount += 1
        let saved = appendLine(question, file: "questions.jsonl", session: id)
        publishActiveCounts()
        return saved
    }

    @discardableResult func append(_ answer: SessionAnswerEvent) -> Bool {
        guard let id = activeID else { return false }
        if activeAnswerIDs.insert(answer.id).inserted { activeMeta?.answerCount += 1 }
        let saved = appendLine(answer, file: "answers.jsonl", session: id)
        publishActiveCounts()
        // Keep meta.json reasonably fresh for crash recovery.
        if let meta = activeMeta { saveMeta(meta) }
        return saved
    }

    func appendTranslation(sessionID: UUID, lineID: UUID, text: String, inputRevision: String? = nil) {
        guard sessionID == activeID || sessions.contains(where: { $0.id == sessionID }) else { return }
        guard appendLine(SessionTranscriptTranslation(id: lineID, text: text, inputRevision: inputRevision),
                         file: "translations.jsonl", session: sessionID) else { return }
        var receipts = translationWriteReceipts[sessionID] ?? SessionTranslationWriteReceipts()
        receipts.recordSubmittedRevision(persistenceBySession[sessionID]?.submittedRevision ?? 0)
        translationWriteReceipts[sessionID] = receipts
        if pendingTranslationSearchSessionIDs.contains(sessionID) {
            pendingSearchableTranslationSessionIDs.insert(sessionID)
        }
    }

    func beginFinalTranslationDrain(for sessionID: UUID) {
        pendingTranslationSearchSessionIDs.insert(sessionID)
        if translationWriteReceipts[sessionID]?.hasPending == true {
            pendingSearchableTranslationSessionIDs.insert(sessionID)
        }
    }

    /// Wait for this session's final translations and archive writes before exposing it to search.
    func finishFinalTranslationDrain(for sessionID: UUID) async {
        completedTranslationDrainSessionIDs.insert(sessionID)
        guard await flush(sessionID: sessionID) else { return }
        completeFinalTranslationDrain(for: sessionID)
    }

    /// Wait until the final-translation drain has also received a durable archive receipt.
    /// This is async so a session backup can wait without blocking the main actor.
    func waitForFinalTranslationDrain(sessionID: UUID) async -> Bool {
        let deadline = ContinuousClock.now.advanced(by: .seconds(120))
        while pendingTranslationSearchSessionIDs.contains(sessionID) {
            guard !Task.isCancelled else { return false }
            if ContinuousClock.now >= deadline {
                return !pendingTranslationSearchSessionIDs.contains(sessionID)
            }
            do { try await Task.sleep(for: .milliseconds(50)) }
            catch { return false }
        }
        return true
    }

    private func completeFinalTranslationDrain(for sessionID: UUID) {
        guard completedTranslationDrainSessionIDs.remove(sessionID) != nil else { return }
        if pendingSearchableTranslationSessionIDs.remove(sessionID) != nil {
            publishTranslationRevision(for: sessionID)
            if sessionID != activeID { searchableContentRevision &+= 1 }
        }
        pendingTranslationSearchSessionIDs.remove(sessionID)
    }

    func translationRevision(for sessionID: UUID) -> Int {
        translationRevisionBySession[sessionID, default: 0]
    }

    private func publishTranslationRevision(for sessionID: UUID) {
        translationRevisionBySession[sessionID, default: 0] &+= 1
    }

    /// Copies a screenshot into the session folder and returns its path
    /// relative to that folder.
    ///
    /// The screenshot is otherwise a temporary file that is deleted right after
    /// the answer; archiving it is what makes "this answer was based on that
    /// picture" visible in the record later.
    @discardableResult
    func archiveScreenshot(_ url: URL?) -> String? {
        guard let url, let id = activeID else { return nil }
        let directory = folder(id).appendingPathComponent("screenshots", isDirectory: true)
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let name = url.lastPathComponent.isEmpty ? "\(UUID().uuidString).jpg" : url.lastPathComponent
            let destination = directory.appendingPathComponent(name)
            try? FileManager.default.removeItem(at: destination)
            try FileManager.default.copyItem(at: url, to: destination)
            return "screenshots/\(name)"
        } catch {
            Diagnostics.record("screenshot.archive_failed")
            return nil
        }
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
            discardPersistence(meta.id)
            io.async { try? FileManager.default.removeItem(at: folder) }
            return nil
        }
        update(meta)
        return meta
    }

    func update(_ meta: SessionMeta) {
        guard !deletedSessionIDs.contains(meta.id) else { return }
        if let index = sessions.firstIndex(where: { $0.id == meta.id }) { sessions[index] = meta } else { sessions.insert(meta, at: 0) }
        if activeID == meta.id { activeMeta = meta }
        saveMeta(meta)
    }

    @discardableResult
    func saveSummary(_ summary: SessionSummary, for id: UUID) -> Bool {
        guard !deletedSessionIDs.contains(id), var meta = sessions.first(where: { $0.id == id }) else { return false }
        // A title the user gave the meeting stays; otherwise the summary names it.
        if (meta.title ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, !summary.title.isEmpty {
            meta.title = summary.title
        }
        meta.summaryState = .ready
        meta.summaryError = nil
        let queued = enqueueSnapshot(summary, file: "summary.json", session: id)
        update(meta)
        Task { [weak self] in
            guard let self, await self.flush(sessionID: id), id != self.activeID else { return }
            guard !self.deletedSessionIDs.contains(id), self.sessions.contains(where: { $0.id == id }) else { return }
            self.searchableContentRevision &+= 1
        }
        return queued
    }

    /// Edits one generated structured-notes node on top of the newest queued summary.
    func updateSummary(_ id: UUID, _ change: @escaping (inout SessionSummary) -> Bool,
                       completion: @escaping @MainActor (SessionArchiveEditResult) -> Void) {
        Task {
            guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else {
                completion(.unavailable)
                return
            }
            guard !summarizing.contains(id) else {
                completion(.busy)
                return
            }
            if var demo = demoDetails[id], var summary = demo.summary {
                guard change(&summary) else {
                    completion(.staleTarget)
                    return
                }
                demo.summary = summary
                demoDetails[id] = demo
                searchableContentRevision &+= 1
                completion(.saved)
                return
            }
            var summary = pendingArchiveData(file: "summary.json", sessionID: id, mode: .replace)
                .last.flatMap { try? JSONLines.decoder().decode(SessionSummary.self, from: $0) }
            if summary == nil {
                guard await flush(sessionID: id) else {
                    completion(.persistenceBlocked)
                    return
                }
                guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else {
                    completion(.unavailable)
                    return
                }
                guard !summarizing.contains(id) else {
                    completion(.busy)
                    return
                }
                summary = await detail(id)?.summary
                guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else {
                    completion(.unavailable)
                    return
                }
                guard !summarizing.contains(id) else {
                    completion(.busy)
                    return
                }
            }
            guard var summary else {
                completion(.unavailable)
                return
            }
            guard change(&summary) else {
                completion(.staleTarget)
                return
            }
            guard saveSummary(summary, for: id) else {
                completion(.persistenceBlocked)
                return
            }
            completion(await flush(sessionID: id) ? .saved : .persistenceBlocked)
        }
    }

    @discardableResult
    func saveNotes(_ notes: StructuredNotes, for id: UUID) -> Bool {
        guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else { return false }
        if demoDetails[id] != nil { demoDetails[id]?.notes = notes; notesRevision += 1; return true }
        return enqueueSnapshot(notes, file: "notes.json", session: id)
    }

    /// Edits saved notes in place (undoing a correction).
    func updateNotes(_ id: UUID, _ change: @escaping (inout StructuredNotes) -> Bool,
                     completion: @escaping @MainActor (SessionArchiveEditResult) -> Void = { _ in }) {
        Task {
            guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else {
                completion(.unavailable)
                return
            }
            guard !summarizing.contains(id) else {
                completion(.busy)
                return
            }
            if var demo = demoDetails[id], var notes = demo.notes {
                let originalNotesRevision = SessionProjectionBuilder.structuredNotesRevision(notes)
                guard change(&notes) else {
                    completion(.staleTarget)
                    return
                }
                if demo.summary?.structuredNotesSourceRevision == nil {
                    demo.summary?.structuredNotesSourceRevision = originalNotesRevision
                }
                demo.notes = notes
                demoDetails[id] = demo
                notesRevision += 1
                completion(.saved)
                return
            }
            // Start from the newest queued edit when an earlier write could not
            // be acknowledged. This lets a new correction build on, and remain
            // recoverable with, that pending snapshot.
            var notes = pendingArchiveData(file: "notes.json", sessionID: id, mode: .replace)
                .last.flatMap { try? JSONLines.decoder().decode(StructuredNotes.self, from: $0) }
            if notes == nil {
                guard await flush(sessionID: id) else {
                    completion(.persistenceBlocked)
                    return
                }
                guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else {
                    completion(.unavailable)
                    return
                }
                guard !summarizing.contains(id) else {
                    completion(.busy)
                    return
                }
                notes = await detail(id)?.notes
                guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else {
                    completion(.unavailable)
                    return
                }
                guard !summarizing.contains(id) else {
                    completion(.busy)
                    return
                }
            }
            guard var notes else {
                completion(.unavailable)
                return
            }
            let originalNotesRevision = SessionProjectionBuilder.structuredNotesRevision(notes)
            guard change(&notes) else {
                completion(.staleTarget)
                return
            }
            // Older summaries did not record the structured-notes revision. Seed it
            // with the pre-edit snapshot so a manual note edit makes their rendered
            // wording explicitly stale instead of leaving its freshness unknown.
            if let originalNotesRevision {
                var summary = pendingArchiveData(file: "summary.json", sessionID: id, mode: .replace)
                    .last.flatMap { try? JSONLines.decoder().decode(SessionSummary.self, from: $0) }
                if summary == nil {
                    summary = await detail(id)?.summary
                    guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else {
                        completion(.unavailable)
                        return
                    }
                    guard !summarizing.contains(id) else {
                        completion(.busy)
                        return
                    }
                }
                // A summary can be queued while detail() is suspended. Prefer that newer
                // snapshot before applying the compatibility stamp.
                if let queued = pendingArchiveData(file: "summary.json", sessionID: id, mode: .replace)
                    .last.flatMap({ try? JSONLines.decoder().decode(SessionSummary.self, from: $0) }) {
                    summary = queued
                }
                if var summary, summary.structuredNotesSourceRevision == nil {
                    summary.structuredNotesSourceRevision = originalNotesRevision
                    guard enqueueSnapshot(summary, file: "summary.json", session: id) else {
                        completion(.persistenceBlocked)
                        return
                    }
                }
            }
            guard saveNotes(notes, for: id) else {
                completion(.persistenceBlocked)
                return
            }
            if await flush(sessionID: id) {
                guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else {
                    completion(.unavailable)
                    return
                }
                notesRevision += 1
                if id != activeID { searchableContentRevision &+= 1 }
                completion(.saved)
            } else {
                completion(.persistenceBlocked)
            }
        }
    }

    /// Pending serialized archive data, including writes that failed before receipt.
    /// Callers use it to build a new user edit on top of the newest queued version.
    func pendingArchiveData(file: String, sessionID: UUID, mode: SessionArchiveWriteMode) -> [Data] {
        writeQueues[sessionID]?.entries
            .filter { $0.fileName == file && $0.mode == mode }
            .map(\.data) ?? []
    }

    func rename(_ id: UUID, to title: String) {
        guard var meta = sessions.first(where: { $0.id == id }) else { return }
        meta.title = title.trimmingCharacters(in: .whitespacesAndNewlines)
        update(meta)
    }

    func workspaceSessionIDs(_ id: UUID, formerName: String?, includeLegacyNameOnly: Bool) -> [UUID] {
        sessions.filter { meta in
            if meta.workspaceID == id { return true }
            guard includeLegacyNameOnly, meta.workspaceID == nil, meta.workspaceDeleted != true,
                  let formerName, !formerName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return false }
            return meta.workspaceName == formerName
        }.map(\.id)
    }

    /// Permanently deletes every archived meeting folder linked to a workspace.
    func deleteWorkspaceSessions(_ id: UUID, formerName: String?, includeLegacyNameOnly: Bool) throws {
        guard !DemoMode.isEnabled else { throw AppFailure.capture("演示资料不可删除。") }
        let sessionIDs = workspaceSessionIDs(id, formerName: formerName, includeLegacyNameOnly: includeLegacyNameOnly)
        if let activeID, sessionIDs.contains(activeID) {
            throw AppFailure.capture("正在记录的会议不能删除。")
        }
        guard !sessionIDs.contains(where: { summarizing.contains($0) }) else {
            throw AppFailure.capture("该系列有纪要正在生成，请完成后再删除。")
        }
        let ids = Set(sessionIDs)
        try io.sync {
            for sessionID in sessionIDs {
                let directory = folder(sessionID)
                if FileManager.default.fileExists(atPath: directory.path) {
                    try FileManager.default.removeItem(at: directory)
                }
            }
        }
        deletedSessionIDs.formUnion(ids)
        for sessionID in ids { summaryTasks[sessionID]?.cancel() }
        for sessionID in ids { discardPersistence(sessionID) }
        for sessionID in ids { summarizing.remove(sessionID); summaryTasks[sessionID] = nil }
        sessions.removeAll { ids.contains($0.id) }
        for sessionID in ids { notesSteps[sessionID] = nil }
        notesRevision &+= 1
        searchableContentRevision &+= 1
    }

    /// Keeps a renamed workspace attached to its meetings, including legacy metadata
    /// that identified the workspace only by its former name.
    func renameWorkspace(_ id: UUID, from formerName: String, to newName: String, includeLegacyNameOnly: Bool) {
        guard !DemoMode.isEnabled else { return }
        let sessionIDs = sessions.filter { meta in
            if meta.workspaceID == id { return true }
            guard includeLegacyNameOnly, meta.workspaceID == nil, meta.workspaceDeleted != true,
                  !formerName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return false }
            return meta.workspaceName == formerName
        }.map(\.id)
        for sessionID in sessionIDs {
            guard var meta = sessions.first(where: { $0.id == sessionID }) else { continue }
            meta.workspaceID = id
            meta.workspaceName = newName
            meta.formerWorkspaceName = nil
            meta.workspaceDeleted = false
            update(meta)
        }
        io.sync { }
    }

    func isTombstoned(_ id: UUID) -> Bool { deletedSessionIDs.contains(id) }

    /// Mark the source unavailable before any asynchronous summary work can finish.
    /// The caller commits dependent-knowledge cleanup before finalizing the delete.
    @discardableResult
    func prepareForDeletion(_ id: UUID) -> Bool {
        guard !DemoMode.isEnabled, id != activeID, sessions.contains(where: { $0.id == id }) else { return false }
        deletedSessionIDs.insert(id)
        summaryTasks[id]?.cancel()
        return true
    }

    func cancelPreparedDeletion(_ id: UUID) {
        guard sessions.contains(where: { $0.id == id }) else { return }
        deletedSessionIDs.remove(id)
    }

    @discardableResult
    func delete(_ id: UUID) throws -> Bool {
        guard !DemoMode.isEnabled, id != activeID, sessions.contains(where: { $0.id == id }) else { return false }
        _ = prepareForDeletion(id)
        let directory = folder(id)
        try io.sync {
            if FileManager.default.fileExists(atPath: directory.path) {
                try FileManager.default.removeItem(at: directory)
            }
        }
        sessions.removeAll { $0.id == id }
        discardPersistence(id)
        summaryTasks[id]?.cancel()
        summaryTasks[id] = nil
        summarizing.remove(id)
        notesSteps[id] = nil
        notesRevision &+= 1
        searchableContentRevision &+= 1
        return true
    }

    func revealInFinder(_ id: UUID) {
        NSWorkspace.shared.activateFileViewerSelecting([folder(id)])
    }

    /// Waits until queued writes are on disk (before reading a session back).
    @discardableResult
    func flush() async -> Bool {
        let ids = Array(writeQueues.keys)
        return await flush(sessionIDs: ids)
    }

    @discardableResult
    func flush(sessionID id: UUID) async -> Bool {
        await flush(sessionIDs: [id])
    }

    private func flush(sessionIDs ids: [UUID]) async -> Bool {
        for id in ids { drainWrites(id) }
        for _ in 0..<500 {
            let states = ids.compactMap { persistenceBySession[$0] }
            if states.allSatisfy({ $0.pendingEvents == 0 }) {
                return states.allSatisfy { $0.lastFailure == nil }
            }
            if states.contains(where: { $0.state == .blocked }) { return false }
            try? await Task.sleep(for: .milliseconds(10))
        }
        return false
    }

    // MARK: Private

    private func publishActiveCounts() {
        guard let meta = activeMeta, let index = sessions.firstIndex(where: { $0.id == meta.id }) else { return }
        sessions[index] = meta
    }

    private func saveMeta(_ meta: SessionMeta) {
        _ = enqueueSnapshot(meta, file: "meta.json", session: meta.id)
    }

    nonisolated private static func directorySize(at directory: URL) throws -> Int64 {
        let fileManager = FileManager.default
        let keys: Set<URLResourceKey> = [.isRegularFileKey, .isDirectoryKey, .isSymbolicLinkKey, .fileSizeKey]
        let children = try fileManager.contentsOfDirectory(at: directory, includingPropertiesForKeys: Array(keys), options: [])
        var total: Int64 = 0
        for child in children {
            let values = try child.resourceValues(forKeys: keys)
            if values.isSymbolicLink == true { continue }
            if values.isRegularFile == true {
                guard let fileSize = values.fileSize else { throw CocoaError(.fileReadUnknown) }
                total += Int64(fileSize)
            } else if values.isDirectory == true {
                total += try directorySize(at: child)
            }
        }
        return total
    }

    @discardableResult
    func appendLine<T: Encodable>(_ value: T, file: String, session id: UUID) -> Bool {
        do { return enqueueWrite(try JSONLines.line(value), file: file, session: id) }
        catch { return recordEncodingFailure(error, session: id) }
    }

    func retryPersistence(_ id: UUID) {
        retryTasks[id]?.cancel(); retryTasks[id] = nil
        retryAttempts[id] = 0
        drainWrites(id)
    }

    /// Reopens an overflowed queue only after all accepted writes have been saved.
    /// The snapshot retains the count of writes that were never queued.
    @discardableResult
    func acknowledgeDataLossAndResume(_ id: UUID) -> Bool {
        guard var queue = writeQueues[id], queue.acknowledgeDataLossAndResume() else { return false }
        writeQueues[id] = queue
        persistenceBySession[id] = queue.snapshot
        return true
    }

    private func enqueueSnapshot<T: Encodable>(_ value: T, file: String, session id: UUID) -> Bool {
        let encoder = JSONLines.encoder(); encoder.outputFormatting.insert(.prettyPrinted)
        do { return enqueueWrite(try encoder.encode(value), file: file, session: id, mode: .replace) }
        catch { return recordEncodingFailure(error, session: id) }
    }

    private func recordEncodingFailure(_ error: Error, session id: UUID) -> Bool {
        var queue = writeQueues[id] ?? SessionArchiveWriteQueue()
        queue.fail("编码归档内容失败：\(error.localizedDescription)", permanent: true)
        writeQueues[id] = queue; persistenceBySession[id] = queue.snapshot
        return false
    }

    private func enqueueWrite(_ data: Data, file: String, session id: UUID, mode: SessionArchiveWriteMode = .append) -> Bool {
        guard !discardedSessions.contains(id), !deletedSessionIDs.contains(id) else { return false }
        var queue = writeQueues[id] ?? SessionArchiveWriteQueue()
        let result = queue.enqueue(fileName: file, data: data, mode: mode)
        writeQueues[id] = queue; persistenceBySession[id] = queue.snapshot
        switch result {
        case .queued: drainWrites(id); return true
        case .full: drainWrites(id); return false
        }
    }

    private func drainWrites(_ id: UUID) {
        guard !drainingWrites.contains(id), let queue = writeQueues[id], !queue.entries.isEmpty else { return }
        drainingWrites.insert(id)
        let batch = Array(queue.entries.prefix(32))
        let directory = folder(id), appender = fileAppender
        io.async { [weak self] in
            var completed: Int?
            var failure: String?
            for entry in batch {
                do { try appender.write(entry.data, to: directory.appendingPathComponent(entry.fileName), mode: entry.mode); completed = entry.revision }
                catch { failure = error.localizedDescription; break }
            }
            let persistedSearchableContent = completed.map { completedRevision in
                batch.contains {
                    $0.revision <= completedRevision
                        && ["transcript.jsonl", "questions.jsonl", "answers.jsonl"].contains($0.fileName)
                }
            } ?? false
            Task { @MainActor [weak self] in
                guard let self else { return }
                self.drainingWrites.remove(id)
                guard !self.discardedSessions.contains(id) else { return }
                var latest = self.writeQueues[id] ?? SessionArchiveWriteQueue()
                if let completed { latest.acknowledge(through: completed) }
                if let failure { latest.fail("保存受阻：\(failure)") }
                self.writeQueues[id] = latest
                self.persistenceBySession[id] = latest.snapshot
                if id != self.activeID && persistedSearchableContent {
                    self.searchableContentRevision &+= 1
                }
                if completed != nil {
                    self.acknowledgeTranslationWrites(for: id, persistedRevision: latest.persistedRevision)
                }
                if failure != nil { self.scheduleRetry(id) }
                else {
                    self.retryAttempts[id] = 0
                    if !latest.entries.isEmpty {
                        self.drainWrites(id)
                    } else if self.completedTranslationDrainSessionIDs.contains(id) {
                        self.completeFinalTranslationDrain(for: id)
                    }
                }
            }
        }
    }

    private func acknowledgeTranslationWrites(for sessionID: UUID, persistedRevision: Int) {
        guard var receipts = translationWriteReceipts[sessionID],
              receipts.acknowledge(persistedRevision: persistedRevision) else { return }
        translationWriteReceipts[sessionID] = receipts.hasPending ? receipts : nil
        if pendingTranslationSearchSessionIDs.contains(sessionID) {
            pendingSearchableTranslationSessionIDs.insert(sessionID)
            return
        }
        publishTranslationRevision(for: sessionID)
        if sessionID != activeID { searchableContentRevision &+= 1 }
    }

    private func scheduleRetry(_ id: UUID) {
        guard retryTasks[id] == nil, !discardedSessions.contains(id) else { return }
        let attempt = retryAttempts[id, default: 0]
        retryAttempts[id] = attempt + 1
        let delay = min(30, 1 << min(attempt, 5))
        retryTasks[id] = Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(delay))
            guard let self, !Task.isCancelled else { return }
            self.retryTasks[id] = nil
            self.drainWrites(id)
        }
    }

    private func discardPersistence(_ id: UUID) {
        discardedSessions.insert(id)
        retryTasks[id]?.cancel(); retryTasks[id] = nil
        retryAttempts[id] = nil
        writeQueues[id] = nil
        persistenceBySession[id] = nil
        translationWriteReceipts[id] = nil
        translationRevisionBySession[id] = nil
        pendingSearchableTranslationSessionIDs.remove(id)
        pendingTranslationSearchSessionIDs.remove(id)
        completedTranslationDrainSessionIDs.remove(id)
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
