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
    /// Structured notes (notes.json); nil for sessions summarized before them.
    var notes: StructuredNotes? = nil
    var userContent: SessionUserContent = .init()
    var userContentError: String? = nil

    var effectiveNotes: StructuredNotes? {
        var result = userContent.editedNotes ?? notes
        result?.corrections = userContent.corrections(generated: notes?.corrections ?? [], lines: lines)
        return result
    }

    var effectiveLines: [SessionTranscriptLine] {
        NotesCorrections.apply(userContent.corrections(generated: notes?.corrections ?? [], lines: lines), to: lines)
    }

    var effectiveSummary: SessionSummary? {
        guard var result = summary else { return nil }
        result.title = meta.displayTitle
        if let edited = userContent.editedNotes {
            result.markdown = NotesMarkdown.render(edited, title: meta.displayTitle, facts: "")
        } else if let markdown = userContent.editedMarkdown {
            result.markdown = markdown
        }
        return result
    }

    var fullMarkdown: String {
        var result = SessionMarkdownExporter.export(meta: meta, summary: effectiveSummary, lines: effectiveLines,
                                                    answers: answers, detected: questions)
        if !userContent.personalNotes.isEmpty { result += "\n\n## 我的笔记\n\n" + userContent.personalNotes }
        if !userContent.highlights.isEmpty {
            result += "\n\n## 重点标记\n\n" + userContent.highlights.map { "- [\(NotesTime.text($0.offsetSeconds))] \($0.quote)" }.joined(separator: "\n")
        }
        return result
    }
}

enum SessionStorageInfo: Equatable, Sendable {
    case available(path: String, bytes: Int64)
    case missing(path: String)
    case unavailable(path: String)
    case demo
}

/// Local session archive: one folder per session under
/// `~/Library/Application Support/InterviewHacker/Sessions/<id>/`:
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
    /// Search readers observe this only after a content write has reached disk.
    var searchRevision = 0

    private let io = DispatchQueue(label: "app.interviewhacker.archive", qos: .utility)
    @ObservationIgnored private var activeMeta: SessionMeta?
    @ObservationIgnored private var activeAnswerIDs: Set<UUID> = []
    @ObservationIgnored private var demoDetails: [UUID: SessionDetail] = [:]
    @ObservationIgnored private var cachedUserContent: [UUID: SessionUserContent] = [:]
    @ObservationIgnored private var userSaveTasks: [UUID: Task<Void, Never>] = [:]
    private(set) var userContentDrafts: [UUID: String] = [:]
    private(set) var userSaveErrors: [UUID: String] = [:]
    private(set) var savingUserContent: Set<UUID> = []
    /// Sticky until acknowledged: a later successful write must not hide an earlier missing segment.
    private(set) var storageErrors: [UUID: String] = [:]

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
                        let modified = (try? FileManager.default.attributesOfItem(atPath: transcript.path)[.modificationDate]) as? Date
                        meta.endedAt = modified ?? meta.startedAt
                        if meta.summaryState == .pending {
                            meta.summaryState = .none
                            meta.summaryError = nil
                            changed = true
                            if meta.transcriptCount > 0 { recovered.insert(meta.id) }
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
            sessions = result.metas.filter { $0.id != activeID } + (activeMeta.map { [$0] } ?? [])
            sessions.sort { $0.startedAt > $1.startedAt }
            cachedUserContent.removeAll()
            contentDidChange()
            if !result.recovered.isEmpty { onRecoveredSummaries(result.recovered) }
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
            var transcript = lines("transcript.jsonl", SessionTranscriptLine.self)
            let translations = Dictionary(lines("translations.jsonl", SessionTranscriptTranslation.self).map { ($0.id, $0.text) },
                                          uniquingKeysWith: { _, latest in latest })
            for index in transcript.indices {
                if let translation = translations[transcript[index].id] {
                    transcript[index].translatedText = translation
                }
            }
            let voices = Dictionary(lines("voices.jsonl", SessionVoiceTag.self).map { ($0.id, $0.voice) },
                                    uniquingKeysWith: { _, latest in latest })
            for index in transcript.indices {
                if let voice = voices[transcript[index].id] { transcript[index].voice = voice }
            }
            var content = SessionUserContent()
            var contentError: String?
            do { content = try SessionUserContentStore.load(from: folder) }
            catch { contentError = error.localizedDescription }
            return SessionDetail(
                meta: meta,
                lines: transcript,
                answers: Self.latestAnswers(lines("answers.jsonl", SessionAnswerEvent.self)),
                questions: lines("questions.jsonl", SessionQuestionEvent.self),
                context: (try? Data(contentsOf: folder.appendingPathComponent("context.json"))).flatMap { try? decoder.decode(SessionContextSnapshot.self, from: $0) },
                summary: (try? Data(contentsOf: folder.appendingPathComponent("summary.json"))).flatMap { try? decoder.decode(SessionSummary.self, from: $0) },
                notes: (try? Data(contentsOf: folder.appendingPathComponent("notes.json"))).flatMap { try? decoder.decode(StructuredNotes.self, from: $0) },
                userContent: content, userContentError: contentError)
        }.value
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
        activeID = meta.id
        activeAnswerIDs = []
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
        if activeAnswerIDs.insert(answer.id).inserted { activeMeta?.answerCount += 1 }
        appendLine(answer, file: "answers.jsonl", session: id)
        publishActiveCounts()
        // Keep meta.json reasonably fresh for crash recovery.
        if let meta = activeMeta { saveMeta(meta) }
    }

    func appendTranslation(sessionID: UUID, lineID: UUID, text: String) {
        guard sessionID == activeID || sessions.contains(where: { $0.id == sessionID }) else { return }
        appendLine(SessionTranscriptTranslation(id: lineID, text: text), file: "translations.jsonl", session: sessionID)
        Task { [weak self] in
            await self?.flush()
            self?.notesRevision &+= 1
        }
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
        let hasUserContent = cachedUserContent[meta.id]?.hasContent == true
            || !(userContentDrafts[meta.id] ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            || ((try? SessionUserContentStore.load(from: folder(meta.id)).hasContent) ?? false)
        if meta.transcriptCount == 0 && meta.answerCount == 0 && !hasUserContent {
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
        // A title the user gave the meeting stays; otherwise the summary names it.
        if (meta.title ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, !summary.title.isEmpty {
            meta.title = summary.title
        }
        meta.summaryState = .ready
        meta.summaryError = nil
        let folder = folder(id)
        io.async {
            if Self.write(summary, to: folder.appendingPathComponent("summary.json")) {
                Task { @MainActor in self.contentDidChange(id) }
            }
        }
        update(meta)
    }

    func saveNotes(_ notes: StructuredNotes, for id: UUID) {
        if demoDetails[id] != nil { demoDetails[id]?.notes = notes; notesRevision += 1; return }
        let folder = folder(id)
        io.async {
            if Self.write(notes, to: folder.appendingPathComponent("notes.json")) {
                Task { @MainActor in self.contentDidChange(id) }
            }
        }
    }

    /// Edits saved notes in place (undoing a correction).
    func updateNotes(_ id: UUID, _ change: @escaping (inout StructuredNotes) -> Void) {
        Task {
            guard var notes = await detail(id)?.notes else { return }
            change(&notes)
            saveNotes(notes, for: id)
        }
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
        sessions.removeAll { ids.contains($0.id) }
        for sessionID in ids { notesSteps[sessionID] = nil }
        contentDidChange()
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

    func delete(_ id: UUID) {
        guard id != activeID else { return }
        userSaveTasks[id]?.cancel(); userSaveTasks[id] = nil
        cachedUserContent[id] = nil; userContentDrafts[id] = nil; userSaveErrors[id] = nil
        sessions.removeAll { $0.id == id }
        let folder = folder(id)
        io.async {
            do {
                if FileManager.default.fileExists(atPath: folder.path) { try FileManager.default.removeItem(at: folder) }
                Task { @MainActor in self.contentDidChange(id) }
            } catch { }
        }
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

    func contentDidChange(_ id: UUID? = nil) {
        notesRevision &+= 1
        searchRevision &+= 1
    }

    func acknowledgeStorageErrors() { storageErrors.removeAll() }

    func userContent(_ id: UUID) async throws -> SessionUserContent {
        if let content = demoDetails[id]?.userContent { return content }
        let directory = folder(id)
        let result: SessionUserContent = try await withCheckedThrowingContinuation { continuation in
            io.async {
                do { continuation.resume(returning: try SessionUserContentStore.load(from: directory)) }
                catch { continuation.resume(throwing: error) }
            }
        }
        cachedUserContent[id] = result
        return result
    }

    @discardableResult
    func saveUserContent(_ content: SessionUserContent, for id: UUID) async throws -> SessionUserContent {
        try await updateUserContent(for: id) { current in
            guard content.revision == current.revision else { throw SessionUserContentStore.Failure.changedElsewhere }
            current = content
        }
    }

    /// A field edit reads and commits on one queue, so typing and marking a line cannot overwrite each other.
    @discardableResult
    func updateUserContent(for id: UUID, _ change: @escaping @Sendable (inout SessionUserContent) throws -> Void) async throws -> SessionUserContent {
        guard sessions.contains(where: { $0.id == id }) else { throw CocoaError(.fileNoSuchFile) }
        if var demo = demoDetails[id] {
            try change(&demo.userContent)
            demo.userContent.revision += 1; demo.userContent.updatedAt = Date()
            demoDetails[id] = demo; cachedUserContent[id] = demo.userContent; contentDidChange(id)
            return demo.userContent
        }
        let directory = folder(id)
        let committed: SessionUserContent = try await withCheckedThrowingContinuation { continuation in
            io.async {
                do {
                    var current = try SessionUserContentStore.load(from: directory)
                    try change(&current)
                    continuation.resume(returning: try SessionUserContentStore.save(current, to: directory))
                } catch { continuation.resume(throwing: error) }
            }
        }
        cachedUserContent[id] = committed
        contentDidChange(id)
        return committed
    }

    func personalNotes(for id: UUID) -> String { userContentDrafts[id] ?? cachedUserContent[id]?.personalNotes ?? demoDetails[id]?.userContent.personalNotes ?? "" }

    func isHighlighted(_ lineID: UUID, in id: UUID) -> Bool {
        _ = notesRevision
        return (cachedUserContent[id] ?? demoDetails[id]?.userContent)?.highlights.contains(where: { $0.lineID == lineID }) == true
    }

    func primeUserContent(_ content: SessionUserContent, for id: UUID) {
        if (cachedUserContent[id]?.revision ?? -1) <= content.revision { cachedUserContent[id] = content }
    }

    func editPersonalNotes(_ text: String, for id: UUID) {
        userContentDrafts[id] = text
        userSaveErrors[id] = nil
        userSaveTasks[id]?.cancel()
        userSaveTasks[id] = Task {
            do { try await Task.sleep(for: .milliseconds(350)); try Task.checkCancellation() }
            catch { return }
            await persistPersonalNotes(for: id)
        }
    }

    func persistPersonalNotes(for id: UUID) async {
        guard let draft = userContentDrafts[id] else { return }
        savingUserContent.insert(id)
        defer { savingUserContent.remove(id) }
        do {
            _ = try await updateUserContent(for: id) { $0.personalNotes = draft }
            if userContentDrafts[id] == draft { userContentDrafts[id] = nil }
            userSaveErrors[id] = nil
        } catch { userSaveErrors[id] = "笔记尚未保存：\(error.localizedDescription)" }
    }

    /// Called before leaving or finishing a meeting. Failed drafts remain in memory for retry.
    func flushUserContent() async {
        for task in userSaveTasks.values { task.cancel() }
        userSaveTasks.removeAll()
        for id in Array(userContentDrafts.keys) { await persistPersonalNotes(for: id) }
        await flush()
    }

    // MARK: Private

    func saveGeneratedContent(_ summary: SessionSummary, notes: StructuredNotes?, for id: UUID) async throws {
        guard var meta = sessions.first(where: { $0.id == id }) else { throw CocoaError(.fileNoSuchFile) }
        if (meta.title ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, !summary.title.isEmpty { meta.title = summary.title }
        meta.summaryState = .ready; meta.summaryError = nil
        if let notes { meta.actionItemCount = notes.actionItemCount; meta.improveCount = notes.improveCount }
        if var demo = demoDetails[id] {
            demo.summary = summary; if let notes { demo.notes = notes }; demo.meta = meta
            demo.userContent.revision += 1
            demoDetails[id] = demo; cachedUserContent[id] = demo.userContent
        } else {
            let directory = folder(id)
            let savedMeta = meta
            let content: SessionUserContent = try await withCheckedThrowingContinuation { continuation in
                io.async {
                    do {
                        if let notes { try JSONLines.encoder().encode(notes).write(to: directory.appendingPathComponent("notes.json"), options: .atomic) }
                        try JSONLines.encoder().encode(summary).write(to: directory.appendingPathComponent("summary.json"), options: .atomic)
                        let content = try SessionUserContentStore.save(SessionUserContentStore.load(from: directory), to: directory)
                        try JSONLines.encoder().encode(savedMeta).write(to: directory.appendingPathComponent("meta.json"), options: .atomic)
                        continuation.resume(returning: content)
                    } catch { continuation.resume(throwing: error) }
                }
            }
            cachedUserContent[id] = content
        }
        if let index = sessions.firstIndex(where: { $0.id == id }) { sessions[index] = meta }
        contentDidChange(id)
    }

    func saveDerivedSummary(_ summary: SessionSummary, for id: UUID) async throws {
        if demoDetails[id] != nil { demoDetails[id]?.summary = summary; contentDidChange(id); return }
        let url = folder(id).appendingPathComponent("summary.json")
        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
            io.async {
                do { try JSONLines.encoder().encode(summary).write(to: url, options: .atomic); continuation.resume() }
                catch { continuation.resume(throwing: error) }
            }
        }
        contentDidChange(id)
    }

    private func publishActiveCounts() {
        guard let meta = activeMeta, let index = sessions.firstIndex(where: { $0.id == meta.id }) else { return }
        sessions[index] = meta
    }

    private func saveMeta(_ meta: SessionMeta) {
        let url = folder(meta.id).appendingPathComponent("meta.json")
        io.async {
            if Self.write(meta, to: url) { Task { @MainActor in self.searchRevision &+= 1 } }
        }
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

    func appendLine<T: Encodable>(_ value: T, file: String, session id: UUID) {
        let url = folder(id).appendingPathComponent(file)
        let data: Data
        do { data = try JSONLines.line(value) }
        catch { storageErrors[id] = "记录写入失败，部分内容尚未保存：\(error.localizedDescription)"; return }
        io.async {
            do {
                if !FileManager.default.fileExists(atPath: url.path) {
                    try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
                    try Data().write(to: url, options: .atomic)
                }
                let handle = try FileHandle(forWritingTo: url)
                defer { try? handle.close() }
                _ = try handle.seekToEnd()
                try handle.write(contentsOf: data)
            } catch {
                Task { @MainActor in self.storageErrors[id] = "记录写入失败，部分内容尚未保存：\(error.localizedDescription)" }
            }
        }
    }

    @discardableResult
    nonisolated private static func write<T: Encodable>(_ value: T, to url: URL) -> Bool {
        let encoder = JSONLines.encoder()
        encoder.outputFormatting.insert(.prettyPrinted)
        do {
            let data = try encoder.encode(value)
            try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try data.write(to: url, options: .atomic)
            return true
        } catch { return false }
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
