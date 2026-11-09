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

    func effectiveProjection(options: SessionProjectionOptions = .init()) -> SessionTextProjection {
        SessionProjectionBuilder.build(meta: meta, lines: lines, answers: answers, detected: questions,
                                       summary: effectiveSummary, notes: effectiveNotes, options: options)
    }

    var userContentMarkdown: String {
        var result = ""
        if !userContent.personalNotes.isEmpty { result += "\n\n## 我的笔记\n\n" + userContent.personalNotes }
        if !userContent.highlights.isEmpty {
            result += "\n\n## 重点标记\n\n" + userContent.highlights.map { "- [\(NotesTime.text($0.offsetSeconds))] \($0.quote)" }.joined(separator: "\n")
        }
        return result
    }

    var fullMarkdown: String {
        SessionMarkdownExporter.export(meta: meta, projection: effectiveProjection()) + userContentMarkdown
    }

    var readIssues: [SessionArchiveReadIssue] = []
}

enum SessionArchiveEditResult: Equatable {
    case saved
    case busy
    case staleTarget
    case unavailable
    case readOnlyFallback
    case persistenceBlocked

    var failureMessage: String? {
        switch self {
        case .saved: nil
        case .busy: String(localized: "纪要正在重新整理，本次编辑没有保存。完成后请重新打开再试。")
        case .staleTarget: String(localized: "纪要内容已更新，本次编辑没有保存。请重新打开后再试。")
        case .unavailable: String(localized: "无法读取这份纪要，本次编辑没有保存。请刷新后重试。")
        case .readOnlyFallback: String(localized: "旧版会议记录正处于只读恢复状态，本次编辑没有保存。修复归档后重试。")
        case .persistenceBlocked: String(localized: "归档保存受阻，本次编辑没有保存。请先重试保存。")
        }
    }
}

private struct LegacySessionArchiveFallback: Sendable {
    let meta: SessionMeta
    let lines: [SessionTranscriptLine]
    let answers: [SessionAnswerEvent]

    var detail: SessionDetail {
        SessionDetail(meta: meta, lines: lines, answers: answers, questions: [], context: nil, summary: nil)
    }
}

private struct LegacySessionMigrationPayload {
    let meta: SessionMeta
    let metaBytes: Data
    let transcriptBytes: Data
    let answerBytes: Data
}

enum SessionArchiveReadIssue: Equatable, Sendable {
    case missingFile(String)
    case unreadableFile(String, String)
    case malformedLines(String, firstLine: Int, count: Int, truncatedTail: Bool)
    case countMismatch(String, expected: Int, actual: Int)
    case unreadableDirectory(String, String)
    case missingSessionMetadata(String)
    case unreadableSessionMetadata(String, String)
    case malformedSessionMetadata(String, String)
    case unreadableLegacyArchive(String)
    case malformedLegacyArchive(String)
    case legacyMigrationFailed(String)
    case migrationRecoveryRequired(String)

    var message: String {
        switch self {
        case .missingFile(let file): String(localized: "归档文件缺失：\(file)")
        case .unreadableFile(let file, let reason): String(localized: "无法读取 \(file)：\(reason)")
        case .malformedLines(let file, let firstLine, let count, let truncatedTail):
            String(localized: "\(file) 有 \(count) 行无法解析（从第 \(firstLine) 行开始）")
                + (truncatedTail ? String(localized: "；最后一行可能未写完。") : String(localized: "。"))
        case .countMismatch(let file, let expected, let actual):
            String(localized: "\(file) 只读到 \(actual) 条，记录中应有 \(expected) 条。")
        case .unreadableDirectory(_, let reason):
            String(localized: "会议归档目录暂时无法读取：\(reason)。请检查磁盘权限后重试。")
        case .missingSessionMetadata(let folder):
            String(localized: "会议归档缺少元数据文件：\(folder)/meta.json。请勿删除或重建该目录。")
        case .unreadableSessionMetadata(let folder, let reason):
            String(localized: "无法读取会议 \(folder) 的元数据：\(reason)。")
        case .malformedSessionMetadata(let folder, let reason):
            String(localized: "会议 \(folder) 的元数据无法解析：\(reason)。")
        case .unreadableLegacyArchive(let reason):
            String(localized: "无法读取旧版会议归档：\(reason)。原文件已保留，请修复后重试。")
        case .malformedLegacyArchive(let reason):
            String(localized: "旧版会议归档无法解析：\(reason)。原文件已保留，请勿删除。")
        case .legacyMigrationFailed(let reason):
            String(localized: "旧版会议归档迁移未完成：\(reason)。原文件已保留，请修复后重试。")
        case .migrationRecoveryRequired(let reason):
            String(localized: "旧版会议归档需要恢复：\(reason)。暂存备份已保留，请先恢复后再删除会议或系列。")
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
    /// Root or per-session metadata failures found during archive discovery.
    /// A failed root read preserves the previous list and never becomes “empty”.
    private(set) var archiveReadIssues: [SessionArchiveReadIssue] = []
    private(set) var isLoading = false
    private(set) var archivePresenceIssue: String?
    private(set) var workspaceDeletionRecoveryIssue: String?
    private(set) var activeID: UUID?
    /// Sessions whose summary is being generated right now.
    var summarizing: Set<UUID> = []
    /// The step the notes of a session are at while they are written.
    var notesSteps: [UUID: NotesStep] = [:]
    /// Bumped when notes change on disk, so open pages reload them.
    var notesRevision = 0
    /// Search readers observe this only after a content write has reached disk.
    var searchRevision = 0
    /// Per-session revision for saved translations, so only that meeting page reloads.
    private(set) var translationRevisionBySession: [UUID: Int] = [:]
    private(set) var persistenceBySession: [UUID: SessionPersistenceSnapshot] = [:]
    /// Bumped when finalized, searchable content is durably saved.
    private(set) var searchableContentRevision = 0
    /// Newly finalized meetings stay out of history search until final translations have drained.
    private(set) var pendingTranslationSearchSessionIDs: Set<UUID> = []

    private let io = DispatchQueue(label: "app.nextthought.archive", qos: .utility)
    @ObservationIgnored private var activeMeta: SessionMeta?
    @ObservationIgnored private var loadGeneration = 0
    @ObservationIgnored private var activeAnswerIDs: Set<UUID> = []
    @ObservationIgnored private var demoDetails: [UUID: SessionDetail] = [:]
    @ObservationIgnored private var cachedUserContent: [UUID: SessionUserContent] = [:]
    @ObservationIgnored private var userSaveTasks: [UUID: Task<Void, Never>] = [:]
    private(set) var userContentDrafts: [UUID: String] = [:]
    private(set) var userSaveErrors: [UUID: String] = [:]
    private(set) var savingUserContent: Set<UUID> = []
    /// Sticky until acknowledged: a later successful write must not hide an earlier missing segment.
    private(set) var storageErrors: [UUID: String] = [:]
    @ObservationIgnored private var legacyFallbackDetails: [UUID: SessionDetail] = [:]
    @ObservationIgnored private var writeQueues: [UUID: SessionArchiveWriteQueue] = [:]
    @ObservationIgnored private var drainingWrites: Set<UUID> = []
    @ObservationIgnored private var discardedSessions: Set<UUID> = []
    @ObservationIgnored private var deletedSessionIDs: Set<UUID> = []
    @ObservationIgnored var summaryTasks: [UUID: Task<Void, Never>] = [:]
    @ObservationIgnored private var retryTasks: [UUID: Task<Void, Never>] = [:]
    @ObservationIgnored private var retryAttempts: [UUID: Int] = [:]
    /// In-memory failure/retry receipts used by the native acceptance renderer.
    /// Demo retries never reach the disk-backed writer.
    @ObservationIgnored private var demoRetryReceipts: [UUID: SessionPersistenceSnapshot] = [:]
    @ObservationIgnored private let fileAppender = SessionArchiveFileAppender.live
    @ObservationIgnored private var pendingSearchableTranslationSessionIDs: Set<UUID> = []
    @ObservationIgnored private var completedTranslationDrainSessionIDs: Set<UUID> = []
    @ObservationIgnored private var translationWriteReceipts: [UUID: SessionTranslationWriteReceipts] = [:]

    static var root: URL { AppPaths.sessions }

    func folder(_ id: UUID) -> URL { Self.root.appendingPathComponent(id.uuidString, isDirectory: true) }

    private init() {}

    var destructiveOperationDisabledReason: String? {
        if isLoading { return String(localized: "会议记录正在读取，暂时不能删除会议或系列。读取完成后再试。") }
        if let archivePresenceIssue { return archivePresenceIssue }
        if let workspaceDeletionRecoveryIssue { return workspaceDeletionRecoveryIssue }
        return archiveReadIssues.isEmpty
            ? nil
            : String(localized: "会议记录未完整读取，暂时不能删除会议或系列。修复读取问题并重试后再试。")
    }

    func isReadOnlyFallback(_ id: UUID) -> Bool {
        legacyFallbackDetails[id] != nil
    }

    func reportWorkspaceDeletionRecoveryIssue(_ issue: String?) {
        workspaceDeletionRecoveryIssue = issue
    }

    private func recordArchivePresence() {
        do {
            try SessionArchiveContentMarker.record(at: AppPaths.sessionsPresenceMarker)
            archivePresenceIssue = nil
        } catch {
            archivePresenceIssue = String(localized: "无法记录会议归档已创建的状态：") + error.localizedDescription
        }
    }

    // MARK: Loading

    func load(onRecoveredSummaries: @escaping @MainActor ([UUID]) -> Void = { _ in }) {
        guard !DemoMode.isEnabled else { return }
        loadGeneration &+= 1
        let generation = loadGeneration
        isLoading = true
        let root = Self.root
        let hasPersistedArchiveHistory = SessionArchiveContentMarker.wasPreviouslyPopulated(at: AppPaths.sessionsPresenceMarker)
        let knownSessionContent = hasPersistedArchiveHistory || !sessions.isEmpty || activeMeta != nil || activeID != nil
        // A load can also happen after restoring a backup while the app remains
        // open. Do not mistake work that is genuinely running in this process
        // for an orphan left by a previous launch.
        let activeSummaries = summarizing
        Task {
            let result = await Task.detached(priority: .utility) { () -> (metas: [SessionMeta]?, recovered: [UUID], issues: [SessionArchiveReadIssue], legacyFallbacks: [LegacySessionArchiveFallback]) in
                let migration = Self.migrateLegacySessions(into: root)
                let directoryResult = SessionArchiveDirectoryReader.read(
                    at: root,
                    knownContentExpected: knownSessionContent,
                    list: { try FileManager.default.contentsOfDirectory(at: $0, includingPropertiesForKeys: nil) },
                    isMissing: { error in
                        let cocoaError = error as NSError
                        return cocoaError.domain == NSCocoaErrorDomain && cocoaError.code == NSFileReadNoSuchFileError
                    })
                let folders: [URL]
                switch directoryResult {
                case .contents(let value): folders = value
                case .unavailable(let path, let reason):
                    return (nil, [], migration.issues + [.unreadableDirectory(path, reason)], migration.fallbacks)
                }
                var recovered = Set<UUID>()
                var issues = migration.issues
                let legacyFolderNames = Set(migration.fallbacks.map { $0.meta.id.uuidString })
                let metas = folders.compactMap { folder -> SessionMeta? in
                    // Earlier migration attempts could leave a partial folder at
                    // the legacy UUID path. The decoded source owns that identity
                    // until the complete staged replacement is committed.
                    guard !legacyFolderNames.contains(folder.lastPathComponent) else { return nil }
                    do {
                        let values = try folder.resourceValues(forKeys: [.isDirectoryKey])
                        guard let isDirectory = values.isDirectory else {
                            issues.append(.unreadableSessionMetadata(folder.lastPathComponent, "无法确认归档条目的类型。"))
                            return nil
                        }
                        guard isDirectory else { return nil }
                    } catch {
                        issues.append(.unreadableSessionMetadata(folder.lastPathComponent, error.localizedDescription))
                        return nil
                    }
                    let metaURL = folder.appendingPathComponent("meta.json")
                    let metadata = SessionArchiveMetadataReader.read(
                        at: metaURL,
                        load: { try Data(contentsOf: $0) },
                        decode: { try JSONLines.decoder().decode(SessionMeta.self, from: $0) },
                        isMissing: { error in
                            let cocoaError = error as NSError
                            return cocoaError.domain == NSCocoaErrorDomain && cocoaError.code == NSFileReadNoSuchFileError
                        })
                    var meta: SessionMeta
                    switch metadata {
                    case .value(let value): meta = value
                    case .failure(.missing):
                        issues.append(.missingSessionMetadata(folder.lastPathComponent))
                        return nil
                    case .failure(.unreadable(_, let reason)):
                        issues.append(.unreadableSessionMetadata(folder.lastPathComponent, reason))
                        return nil
                    case .failure(.malformed(_, let reason)):
                        issues.append(.malformedSessionMetadata(folder.lastPathComponent, reason))
                        return nil
                    }
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
                return (metas, Array(orderedRecovered), issues, migration.fallbacks)
            }.value
            guard loadGeneration == generation else { return }
            isLoading = false
            archiveReadIssues = result.issues
            legacyFallbackDetails = Dictionary(
                result.legacyFallbacks.map { ($0.meta.id, $0.detail) },
                uniquingKeysWith: { _, latest in latest })
            let fallbackMetas = result.legacyFallbacks.map(\.meta)
                .filter { !deletedSessionIDs.contains($0.id) }
            let fallbackIDs = Set(fallbackMetas.map(\.id))
            if let loadedMetas = result.metas {
                let failedFolders = Set(result.issues.compactMap { issue -> String? in
                    switch issue {
                    case .missingSessionMetadata(let folder),
                         .unreadableSessionMetadata(let folder, _),
                         .malformedSessionMetadata(let folder, _): folder
                    default: nil
                    }
                })
                let visibleMetas = loadedMetas.filter {
                    !fallbackIDs.contains($0.id) && !deletedSessionIDs.contains($0.id)
                }
                let loadedIDs = Set(visibleMetas.map(\.id))
                let preservedMetas = sessions.filter {
                    failedFolders.contains($0.id.uuidString) && !loadedIDs.contains($0.id)
                        && !fallbackIDs.contains($0.id) && $0.id != activeID
                        && !deletedSessionIDs.contains($0.id)
                }
                sessions = visibleMetas.filter { $0.id != activeID } + preservedMetas + fallbackMetas
                    + (activeMeta.map { [$0] } ?? [])
            } else if !fallbackMetas.isEmpty {
                sessions = sessions.filter {
                    !fallbackIDs.contains($0.id) && $0.id != activeID && !deletedSessionIDs.contains($0.id)
                } + fallbackMetas + (activeMeta.map { [$0] } ?? [])
            } else {
                // A missing root is a valid empty archive only on first use. If
                // known sessions or an active session existed, keep them visible
                // while the read issue blocks destructive operations.
                var preserved = sessions.filter { $0.id != activeID && !deletedSessionIDs.contains($0.id) }
                if let activeMeta, !preserved.contains(where: { $0.id == activeMeta.id }) {
                    preserved.append(activeMeta)
                }
                sessions = preserved
            }
            sessions.sort { $0.startedAt > $1.startedAt }
            cachedUserContent.removeAll()
            contentDidChange()
            if !sessions.isEmpty { recordArchivePresence() }
            if !result.recovered.isEmpty { onRecoveredSummaries(result.recovered) }
        }
    }

    /// Screenshot mode: sample sessions that never touch the disk.
    func installDemo(_ details: [SessionDetail]) {
        loadGeneration &+= 1
        isLoading = false
        demoDetails = Dictionary(uniqueKeysWithValues: details.map { ($0.meta.id, $0) })
        sessions = details.map(\.meta)
        legacyFallbackDetails = [:]
        archiveReadIssues = []
        archivePresenceIssue = nil
    }

    /// Seeds a persistence indicator for a deterministic UI fixture without writing
    /// to the user's archive. The acceptance renderer runs with DemoMode enabled.
    func installDemoPersistence(_ snapshot: SessionPersistenceSnapshot, for sessionID: UUID) {
        guard DemoMode.isEnabled else { return }
        persistenceBySession[sessionID] = snapshot
    }

    /// Seeds one blocked snapshot and the receipt the demo retry action will publish.
    /// This preserves the real button/action path without writing an archive file.
    func installDemoPersistenceRetry(_ blocked: SessionPersistenceSnapshot,
                                     receipt: SessionPersistenceSnapshot,
                                     for sessionID: UUID) {
        guard DemoMode.isEnabled else { return }
        persistenceBySession[sessionID] = blocked
        demoRetryReceipts[sessionID] = receipt
    }

    func detail(_ id: UUID) async -> SessionDetail? {
        guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else { return nil }
        if let demo = demoDetails[id] { return demo }
        if let fallback = legacyFallbackDetails[id] { return fallback }
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
            var content = SessionUserContent()
            var contentError: String?
            do { content = try SessionUserContentStore.load(from: folder) }
            catch { contentError = error.localizedDescription; issues.append(.unreadableFile(SessionUserContent.filename, error.localizedDescription)) }
            return SessionDetail(
                meta: meta,
                lines: transcript,
                answers: answers,
                questions: questions,
                context: snapshot("context.json", SessionContextSnapshot.self),
                summary: snapshot("summary.json", SessionSummary.self, required: meta.summaryState == .ready),
                notes: snapshot("notes.json", StructuredNotes.self),
                userContent: content, userContentError: contentError, readIssues: issues)
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
        recordArchivePresence()
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
        let hasUserContent = cachedUserContent[meta.id]?.hasContent == true
            || !(userContentDrafts[meta.id] ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            || ((try? SessionUserContentStore.load(from: folder(meta.id)).hasContent) ?? false)
        if meta.transcriptCount == 0 && meta.answerCount == 0 && !hasUserContent {
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
        guard !isReadOnlyFallback(meta.id), !deletedSessionIDs.contains(meta.id) else { return }
        if let index = sessions.firstIndex(where: { $0.id == meta.id }) { sessions[index] = meta } else { sessions.insert(meta, at: 0) }
        if activeID == meta.id { activeMeta = meta }
        saveMeta(meta)
    }

    @discardableResult
    func saveSummary(_ summary: SessionSummary, for id: UUID) -> Bool {
        guard !isReadOnlyFallback(id), !deletedSessionIDs.contains(id),
              var meta = sessions.first(where: { $0.id == id }) else { return false }
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
            guard !isReadOnlyFallback(id) else {
                completion(.readOnlyFallback)
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
        guard !isReadOnlyFallback(id), !deletedSessionIDs.contains(id),
              sessions.contains(where: { $0.id == id }) else { return false }
        if demoDetails[id] != nil { demoDetails[id]?.notes = notes; notesRevision += 1; return true }
        return enqueueSnapshot(notes, file: "notes.json", session: id)
    }

    /// Human edits are revision checked and survive future AI generations.
    func updateNotes(_ id: UUID, _ change: @escaping (inout StructuredNotes) -> Bool,
                     completion: @escaping @MainActor (SessionArchiveEditResult) -> Void = { _ in }) {
        Task {
            guard !isReadOnlyFallback(id) else { completion(.readOnlyFallback); return }
            guard !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else { completion(.unavailable); return }
            guard !summarizing.contains(id) else { completion(.busy); return }
            guard await flush(sessionID: id) else { completion(.persistenceBlocked); return }
            guard let detail = await detail(id), var notes = detail.effectiveNotes else { completion(.unavailable); return }
            guard !summarizing.contains(id) else { completion(.busy); return }
            guard change(&notes) else { completion(.staleTarget); return }
            let edited = notes, expectedRevision = detail.userContent.revision
            do {
                _ = try await updateUserContent(for: id) { current in
                    guard current.revision == expectedRevision else { throw SessionUserContentStore.Failure.changedElsewhere }
                    current.editedNotes = edited
                    current.basedOnGeneratedAt = detail.notes?.generatedAt ?? detail.summary?.generatedAt
                }
                userSaveErrors[id] = nil
                completion(.saved)
            } catch SessionUserContentStore.Failure.changedElsewhere { completion(.staleTarget) }
            catch { userSaveErrors[id] = "纪要修改尚未保存：\(error.localizedDescription)"; completion(.persistenceBlocked) }
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
        guard !isReadOnlyFallback(id) else { return }
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
        if let reason = destructiveOperationDisabledReason { throw AppFailure.capture(reason) }
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

    /// Updates only live archive state after the transaction journal has
    /// committed and the corresponding session folders have been staged.
    func forgetWorkspaceSessionsAfterStaging(_ sessionIDs: Set<UUID>) {
        for sessionID in sessionIDs { discardPersistence(sessionID) }
        sessions.removeAll { sessionIDs.contains($0.id) }
        for sessionID in sessionIDs { notesSteps[sessionID] = nil }
        notesRevision &+= 1
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

    /// Mark the source unavailable before asynchronous summary work can finish.
    /// A prepared deletion can be retried after a file-system failure.
    @discardableResult
    func prepareForDeletion(_ id: UUID) -> Bool {
        guard !DemoMode.isEnabled, destructiveOperationDisabledReason == nil,
              !isReadOnlyFallback(id), id != activeID,
              sessions.contains(where: { $0.id == id }) else { return false }
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
        guard !DemoMode.isEnabled, destructiveOperationDisabledReason == nil,
              !isReadOnlyFallback(id), id != activeID,
              sessions.contains(where: { $0.id == id }) else { return false }
        guard prepareForDeletion(id) else { return false }
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

    func contentDidChange(_ id: UUID? = nil) {
        notesRevision &+= 1
        searchRevision &+= 1
        searchableContentRevision &+= 1
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
        guard !isReadOnlyFallback(id), !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else { throw CocoaError(.fileNoSuchFile) }
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

    /// Snapshot consumers must include debounced personal drafts as well as queued events.
    func flushForSnapshot() async -> Bool {
        await flushUserContent()
        let saved = await flush()
        return saved && userContentDrafts.isEmpty && userSaveErrors.isEmpty
    }

    // MARK: Private

    func saveGeneratedContent(_ summary: SessionSummary, notes: StructuredNotes?, for id: UUID) async throws {
        guard !isReadOnlyFallback(id), !deletedSessionIDs.contains(id),
              var meta = sessions.first(where: { $0.id == id }) else { throw CocoaError(.fileNoSuchFile) }
        if (meta.title ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, !summary.title.isEmpty { meta.title = summary.title }
        meta.summaryState = .ready; meta.summaryError = nil
        if let notes { meta.actionItemCount = notes.actionItemCount; meta.improveCount = notes.improveCount }
        if var demo = demoDetails[id] {
            demo.summary = summary; if let notes { demo.notes = notes }; demo.meta = meta
            demo.userContent.revision += 1; demoDetails[id] = demo; cachedUserContent[id] = demo.userContent
        } else {
            guard await flush(sessionID: id) else { throw CocoaError(.fileWriteUnknown) }
            if let notes, !enqueueSnapshot(notes, file: "notes.json", session: id) { throw CocoaError(.fileWriteUnknown) }
            guard enqueueSnapshot(summary, file: "summary.json", session: id),
                  enqueueSnapshot(meta, file: "meta.json", session: id), await flush(sessionID: id) else { throw CocoaError(.fileWriteUnknown) }
            // Invalidate in-progress editors while preserving every authored field.
            _ = try await updateUserContent(for: id) { _ in }
        }
        if let index = sessions.firstIndex(where: { $0.id == id }) { sessions[index] = meta }
        contentDidChange(id)
    }

    func saveDerivedSummary(_ summary: SessionSummary, for id: UUID) async throws {
        guard !isReadOnlyFallback(id), !deletedSessionIDs.contains(id), sessions.contains(where: { $0.id == id }) else { throw CocoaError(.fileNoSuchFile) }
        if demoDetails[id] != nil { demoDetails[id]?.summary = summary; contentDidChange(id); return }
        guard enqueueSnapshot(summary, file: "summary.json", session: id), await flush(sessionID: id) else { throw CocoaError(.fileWriteUnknown) }
        contentDidChange(id)
    }

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
        if DemoMode.isEnabled, let receipt = demoRetryReceipts.removeValue(forKey: id) {
            persistenceBySession[id] = receipt
            return
        }
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
        userSaveTasks[id]?.cancel(); userSaveTasks[id] = nil
        cachedUserContent[id] = nil; userContentDrafts[id] = nil; userSaveErrors[id] = nil

        discardedSessions.insert(id)
        retryTasks[id]?.cancel(); retryTasks[id] = nil
        retryAttempts[id] = nil
        demoRetryReceipts[id] = nil
        writeQueues[id] = nil
        persistenceBySession[id] = nil
        translationWriteReceipts[id] = nil
        translationRevisionBySession[id] = nil
        pendingSearchableTranslationSessionIDs.remove(id)
        pendingTranslationSearchSessionIDs.remove(id)
        completedTranslationDrainSessionIDs.remove(id)
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
    /// The source is moved aside only after every staged destination is validated.
    nonisolated private static func migrateLegacySessions(into root: URL) -> (issues: [SessionArchiveReadIssue], fallbacks: [LegacySessionArchiveFallback]) {
        let pendingRecovery: URL?
        do {
            pendingRecovery = try SessionArchiveMigrationPublisher.pendingRecoveryDirectory(for: root)
        } catch {
            return ([.unreadableLegacyArchive(error.localizedDescription)], [])
        }
        let legacy = AppPaths.legacySessions
        do {
            _ = try FileManager.default.attributesOfItem(atPath: legacy.path)
        } catch {
            let cocoaError = error as NSError
            if cocoaError.domain == NSCocoaErrorDomain && cocoaError.code == NSFileReadNoSuchFileError {
                return pendingRecovery.map { ([.migrationRecoveryRequired($0.path)], []) } ?? ([], [])
            }
            return ([.unreadableLegacyArchive(error.localizedDescription)], [])
        }

        let old: [SavedSession]
        do {
            let data = try Data(contentsOf: legacy)
            old = try JSONDecoder().decode([SavedSession].self, from: data)
        } catch let error as DecodingError {
            return ([.malformedLegacyArchive(error.localizedDescription)], [])
        } catch {
            return ([.unreadableLegacyArchive(error.localizedDescription)], [])
        }

        // Construct the read-only fallback before encoding or touching destinations.
        // If any later write fails, these exact legacy records remain available.
        let fallbacks: [LegacySessionArchiveFallback] = old.map { session in
            let answers: [SessionAnswerEvent]
            if let records = session.records {
                answers = records.map { record in
                    SessionAnswerEvent(id: record.id, date: record.date, question: record.question,
                                       answer: record.answer, model: record.model, seconds: record.seconds,
                                       firstTokenSeconds: record.firstTokenSeconds, automatic: record.automatic,
                                       screenshotPath: record.screenshotPath, webAnswer: record.webAnswer,
                                       webSources: record.webSources, evidence: record.evidence)
                }
            } else {
                answers = LegacySessionAnswerMigration.events(from: session.answers, date: session.startedAt)
            }
            let lines = session.entries.map {
                SessionTranscriptLine(id: $0.id, date: $0.date, speaker: $0.speaker, text: $0.text,
                                      translatedText: $0.translatedText, startedAt: $0.startedAt,
                                      endedAt: $0.endedAt, voice: $0.voice)
            }
            let meta = SessionMeta(id: session.id, startedAt: session.startedAt, endedAt: session.endedAt,
                                   mode: SessionMode(rawValue: session.mode ?? "") ?? .meeting,
                                   transcriptCount: lines.count, answerCount: answers.count)
            return LegacySessionArchiveFallback(meta: meta, lines: lines, answers: answers)
        }

        if let pendingRecovery {
            return ([.migrationRecoveryRequired(pendingRecovery.path)], fallbacks)
        }

        do {
            let payloads = try fallbacks.map { fallback -> LegacySessionMigrationPayload in
                let encoder = JSONLines.encoder()
                encoder.outputFormatting.insert(.prettyPrinted)
                let metaBytes = try encoder.encode(fallback.meta)
                let transcriptBytes = try fallback.lines.map { try JSONLines.line($0) }.reduce(Data(), +)
                let answerBytes = try fallback.answers.map { try JSONLines.line($0) }.reduce(Data(), +)
                return LegacySessionMigrationPayload(meta: fallback.meta, metaBytes: metaBytes,
                                                     transcriptBytes: transcriptBytes, answerBytes: answerBytes)
            }
            let payloadByID = Dictionary(payloads.map { ($0.meta.id.uuidString, $0) }, uniquingKeysWith: { _, latest in latest })
            let fallbackByID = Dictionary(fallbacks.map { ($0.meta.id.uuidString, $0) }, uniquingKeysWith: { _, latest in latest })
            let destinationNames = ["meta.json", "transcript.jsonl", "answers.jsonl"]
            try SessionArchiveMigrationPublisher.publish(
                sessionIDs: payloads.map { $0.meta.id.uuidString },
                into: root,
                stage: { id, folder in
                    guard let payload = payloadByID[id] else { throw CocoaError(.fileWriteUnknown) }
                    try Self.writeMigrationData(payload.metaBytes, to: folder.appendingPathComponent("meta.json"))
                    try Self.writeMigrationData(payload.transcriptBytes, to: folder.appendingPathComponent("transcript.jsonl"))
                    try Self.writeMigrationData(payload.answerBytes, to: folder.appendingPathComponent("answers.jsonl"))
                },
                validate: { id, folder in
                    guard let payload = payloadByID[id], let fallback = fallbackByID[id] else { throw CocoaError(.fileWriteUnknown) }
                    let decoder = JSONLines.decoder()
                    let meta = try decoder.decode(SessionMeta.self, from: Data(contentsOf: folder.appendingPathComponent("meta.json")))
                    let transcriptData = try Data(contentsOf: folder.appendingPathComponent("transcript.jsonl"))
                    let transcript = JSONLines.decodeReport(SessionTranscriptLine.self, from: transcriptData)
                    let answerData = try Data(contentsOf: folder.appendingPathComponent("answers.jsonl"))
                    let answers = JSONLines.decodeReport(SessionAnswerEvent.self, from: answerData)
                    guard meta == payload.meta,
                          transcript.records == fallback.lines, transcript.malformedLineNumbers.isEmpty,
                          !transcript.truncatedFinalLine,
                          answers.records == fallback.answers, answers.malformedLineNumbers.isEmpty,
                          !answers.truncatedFinalLine else {
                        throw CocoaError(.fileReadCorruptFile)
                    }
                },
                canReplaceExisting: { id, destination in
                    guard let payload = payloadByID[id] else { return false }
                    let attributes = try FileManager.default.attributesOfItem(atPath: destination.path)
                    guard attributes[.type] as? FileAttributeType == .typeDirectory else { return false }
                    let entries = try FileManager.default.contentsOfDirectory(at: destination, includingPropertiesForKeys: nil)
                    guard Set(entries.map(\.lastPathComponent)).isSubset(of: Set(destinationNames)) else { return false }
                    if let metaURL = entries.first(where: { $0.lastPathComponent == "meta.json" }) {
                        let existing = try JSONLines.decoder().decode(SessionMeta.self, from: Data(contentsOf: metaURL))
                        guard existing == payload.meta else { return false }
                    }
                    return true
                },
                commit: {
                    let backupRoot = legacy.deletingLastPathComponent()
                    let conventionalBackup = backupRoot.appendingPathComponent("sessions.legacy.json")
                    let preservedLegacy = FileManager.default.fileExists(atPath: conventionalBackup.path)
                        ? backupRoot.appendingPathComponent("sessions.legacy-\(UUID().uuidString).json")
                        : conventionalBackup
                    try FileManager.default.moveItem(at: legacy, to: preservedLegacy)
                })
            return ([], [])
        } catch let error as SessionArchiveMigrationPublisher.RecoveryRequired {
            return ([.migrationRecoveryRequired(error.localizedDescription)], fallbacks)
        } catch {
            return ([.legacyMigrationFailed(error.localizedDescription)], fallbacks)
        }
    }

    nonisolated private static func writeMigrationData(_ data: Data, to url: URL) throws {
        try SessionArchiveFileAppender.live.write(data, to: url, mode: .replace)
    }
}
