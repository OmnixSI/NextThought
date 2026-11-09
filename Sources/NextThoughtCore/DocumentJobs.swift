import Foundation
import SQLite3

public enum DocumentLimits {
    public static let maximumFileBytes = 512 * 1024 * 1024
    public static let maximumPages = 2000
    public static let maximumExpandedBytes = 2 * 1024 * 1024 * 1024
}

public struct DocumentJob: Codable, Sendable, Equatable, Identifiable {
    public enum WaitingReason: String, Codable, Sendable {
        case runtime, ocrModels, codexConnection, legacyOffice, semantic
    }
    public enum Stage: String, Codable, Sendable {
        case receiving, queued, parsing, ocr, validating, lexical, embedding, ready, warning, waiting, paused, failed, cancelled
        public var title: String {
            switch self {
            case .receiving: "正在接收"
            case .queued: "排队中"
            case .parsing: "格式解析"
            case .ocr: "OCR / 文件代读"
            case .validating: "内容检查"
            case .lexical: "建立文字索引"
            case .embedding: "建立语义索引"
            case .ready: "资料已就绪"
            case .warning: "已完成，需核对"
            case .waiting: "等待组件"
            case .paused: "会议结束后继续"
            case .failed: "处理失败"
            case .cancelled: "已取消"
            }
        }
        public var terminal: Bool { [.ready, .warning, .failed, .cancelled].contains(self) }
    }
    public var id: UUID
    public var workspaceID: UUID
    public var fileName: String
    public var originalPath: String?
    public var sourceHash: String?
    public var bookmark: Data?
    public var category: String?
    public var categories: [String]
    public var provider: String
    public var model: String
    public var localModelID: String?
    public var effort: String
    public var fallback: Bool
    public var timeout: Double
    public var stage: Stage
    public var completed: Int
    public var total: Int?
    public var message: String
    public var waitingReason: WaitingReason?
    public var documentID: UUID?
    public var createdAt: Date
    public var updatedAt: Date
    public init(workspaceID: UUID, fileName: String, category: String? = nil, categories: [String] = [],
                provider: String = "system", model: String = "gpt-6-luna", effort: String = "medium", fallback: Bool = true, timeout: Double = 600) {
        id = UUID(); self.workspaceID = workspaceID; self.fileName = fileName; self.category = category; self.categories = categories
        self.provider = provider; self.model = model; self.effort = effort; self.fallback = fallback; self.timeout = timeout
        stage = .receiving; completed = 0; message = ""; createdAt = Date(); updatedAt = createdAt
    }
    public var progressText: String { stage.title + (total.map { " \(completed)/\($0)" } ?? "") }
}

/// Conversion discovers dependencies lazily, so a text-only PDF never needs an
/// OCR runtime merely because an enhanced reader is selected.
public struct DocumentDependencyError: LocalizedError, Sendable {
    public var reason: DocumentJob.WaitingReason
    public var message: String
    public init(reason: DocumentJob.WaitingReason, message: String) { self.reason = reason; self.message = message }
    public var errorDescription: String? { message }
}

public struct DocumentJobEvent: Sendable {
    public var stage: DocumentJob.Stage
    public var completed: Int
    public var total: Int?
    public var message: String
    public init(_ stage: DocumentJob.Stage, completed: Int = 0, total: Int? = nil, message: String = "") {
        self.stage = stage; self.completed = completed; self.total = total; self.message = message
    }
}

public struct DocumentArtifact: Codable, Sendable {
    public var version = 1
    public var content: DocumentContent
    public var sourceHash: String
    public var converter: String
    public init(content: DocumentContent, sourceHash: String, converter: String) {
        self.content = content; self.sourceHash = sourceHash; self.converter = converter
    }
}

private final class JobDatabase: @unchecked Sendable {
    var handle: OpaquePointer?
    init(url: URL) throws {
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        guard sqlite3_open_v2(url.path, &handle, SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE | SQLITE_OPEN_FULLMUTEX, nil) == SQLITE_OK else { throw CocoaError(.fileWriteUnknown) }
        sqlite3_busy_timeout(handle, 5000)
        try execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS jobs (id TEXT PRIMARY KEY, body BLOB NOT NULL); CREATE TABLE IF NOT EXISTS workspace_deletion_journal (id TEXT PRIMARY KEY, body BLOB NOT NULL);")
    }
    func execute(_ sql: String) throws {
        guard sqlite3_exec(handle, sql, nil, nil, nil) == SQLITE_OK else { throw CocoaError(.fileWriteUnknown) }
    }
    deinit { sqlite3_close(handle) }
}

struct SQLiteQueryFailure: Error, Equatable {
    let resultCode: Int32
}

enum SQLiteRowReader {
    /// SQLITE_DONE is the only successful terminal result. In particular,
    /// SQLITE_BUSY/SQLITE_ERROR must not be mistaken for an empty result set.
    static func collect<Row>(step: () -> Int32, makeRow: () throws -> Row) throws -> [Row] {
        var rows: [Row] = []
        while true {
            switch step() {
            case SQLITE_ROW: rows.append(try makeRow())
            case SQLITE_DONE: return rows
            case let resultCode: throw SQLiteQueryFailure(resultCode: resultCode)
            }
        }
    }
}

/// The database persists intent before processing starts. Completed pages and
/// vectors live in separate versioned artifacts and survive cancellation.
public actor DocumentJobStore {
    private let database: JobDatabase
    private var latest: [UUID: Date] = [:]
    public init(url: URL) throws { database = try JobDatabase(url: url) }
    public func save(_ job: DocumentJob) throws {
        if let date = latest[job.id], date > job.updatedAt { return }
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(database.handle, "INSERT OR REPLACE INTO jobs(id,body) VALUES(?,?)", -1, &statement, nil) == SQLITE_OK else { throw CocoaError(.fileWriteUnknown) }
        defer { sqlite3_finalize(statement) }
        let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
        let data = try JSONEncoder().encode(job)
        guard sqlite3_bind_text(statement, 1, job.id.uuidString, -1, transient) == SQLITE_OK else { throw CocoaError(.fileWriteUnknown) }
        _ = data.withUnsafeBytes { sqlite3_bind_blob(statement, 2, $0.baseAddress, Int32(data.count), transient) }
        guard sqlite3_step(statement) == SQLITE_DONE else { throw CocoaError(.fileWriteUnknown) }
        latest[job.id] = job.updatedAt
    }
    public func delete(ids: Set<UUID>) throws {
        guard !ids.isEmpty else { return }
        let previous = Dictionary(uniqueKeysWithValues: ids.compactMap { id in latest[id].map { (id, $0) } })
        for id in ids { latest[id] = .distantFuture }
        do {
            try database.execute("BEGIN IMMEDIATE")
            var statement: OpaquePointer?
            guard sqlite3_prepare_v2(database.handle, "DELETE FROM jobs WHERE id=?", -1, &statement, nil) == SQLITE_OK else {
                throw CocoaError(.fileWriteUnknown)
            }
            defer { sqlite3_finalize(statement) }
            let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
            for id in ids {
                sqlite3_reset(statement)
                sqlite3_clear_bindings(statement)
                guard sqlite3_bind_text(statement, 1, id.uuidString, -1, transient) == SQLITE_OK,
                      sqlite3_step(statement) == SQLITE_DONE else { throw CocoaError(.fileWriteUnknown) }
            }
            try database.execute("COMMIT")
        } catch {
            try? database.execute("ROLLBACK")
            for id in ids { latest[id] = previous[id] }
            throw error
        }
    }

    /// Atomically records a recoverable deletion intent and removes its queue
    /// rows. The journal contains both the row snapshots and every file move, so
    /// a new process can roll back if it exits during file staging.
    public func stageWorkspaceDeletion(_ journal: WorkspaceDeletionJournal) throws {
        guard journal.phase == .staging else { throw CocoaError(.fileWriteInvalidFileName) }
        try database.execute("BEGIN IMMEDIATE")
        do {
            try Self.write(journal, to: database)
            try Self.deleteJobRows(Set(journal.jobs.map(\.id)), from: database)
            try database.execute("COMMIT")
            for job in journal.jobs { latest[job.id] = .distantFuture }
        } catch {
            try? database.execute("ROLLBACK")
            throw error
        }
    }

    /// Rolls back a staged transaction. File restoration precedes the SQLite
    /// commit and is idempotent; the journal remains available if either step
    /// fails so launch recovery can retry it.
    public func rollbackWorkspaceDeletion(_ transactionID: UUID) throws {
        guard let journal = try Self.read(transactionID, from: database) else { return }
        guard journal.phase == .staging else { throw CocoaError(.fileWriteNoPermission) }
        try WorkspaceDeletionFileStaging.rollback(journal.moves, stagingRoot: journal.stagingRoot)
        try restoreWorkspaceDeletionRows(transactionID)
    }

    /// Restores SQLite state after the caller synchronously restored staged
    /// paths on the main actor. Keeping this separate closes the window where a
    /// workspace autosave could recreate a source path before file rollback.
    public func restoreWorkspaceDeletionRows(_ transactionID: UUID) throws {
        guard let journal = try Self.read(transactionID, from: database) else { return }
        guard journal.phase == .staging else { throw CocoaError(.fileWriteNoPermission) }
        try database.execute("BEGIN IMMEDIATE")
        do {
            try Self.insertJobs(journal.jobs, into: database)
            try Self.deleteJournal(transactionID, from: database)
            try database.execute("COMMIT")
            for job in journal.jobs { latest[job.id] = job.updatedAt }
        } catch {
            try? database.execute("ROLLBACK")
            throw error
        }
    }

    public func workspaceDeletionJournals() throws -> [WorkspaceDeletionJournal] {
        try Self.readAll(from: database)
    }

    /// Commits the deletion marker synchronously so the UI commit section has
    /// no suspension point. Staged files are now authoritative recovery state:
    /// a launch will finish cleanup if the process exits before it does.
    public static func markWorkspaceDeletionCommitted(_ transactionID: UUID, at url: URL) throws {
        let database = try JobDatabase(url: url)
        guard var journal = try read(transactionID, from: database) else { throw CocoaError(.fileNoSuchFile) }
        guard journal.phase == .staging else { return }
        journal.phase = .committed
        try database.execute("BEGIN IMMEDIATE")
        do {
            try write(journal, to: database)
            try database.execute("COMMIT")
        } catch {
            try? database.execute("ROLLBACK")
            throw error
        }
    }

    /// Replaces the prepared manifest before filesystem staging. This lets the
    /// caller prune shared originals after all suspension points and persist
    /// exactly the moves that are about to happen.
    public static func updateWorkspaceDeletionJournal(_ journal: WorkspaceDeletionJournal, at url: URL) throws {
        let database = try JobDatabase(url: url)
        guard let existing = try read(journal.transactionID, from: database),
              existing.phase == .staging, journal.phase == .staging,
              existing.workspaceID == journal.workspaceID,
              existing.stagingRoot == journal.stagingRoot else {
            throw CocoaError(.fileWriteNoPermission)
        }
        try database.execute("BEGIN IMMEDIATE")
        do {
            try write(journal, to: database)
            try database.execute("COMMIT")
        } catch {
            try? database.execute("ROLLBACK")
            throw error
        }
    }

    /// Best-effort post-commit cleanup. If deleting staged files fails, leave
    /// the committed journal for launch recovery instead of reverting a delete
    /// whose commit point has already been recorded.
    public static func finishWorkspaceDeletion(_ transactionID: UUID, at url: URL) {
        do {
            let database = try JobDatabase(url: url)
            guard let journal = try read(transactionID, from: database), journal.phase == .committed else { return }
            try WorkspaceDeletionFileStaging.removeStagedFiles(at: journal.stagingRoot)
            try database.execute("BEGIN IMMEDIATE")
            do {
                try deleteJournal(transactionID, from: database)
                try database.execute("COMMIT")
            } catch {
                try? database.execute("ROLLBACK")
                throw error
            }
        } catch {
            NSLog("Workspace deletion cleanup is pending recovery: %@", String(describing: error))
        }
    }

    /// Runs before workspace/archive loading at launch. Prepared transactions
    /// restore files and queue rows; committed ones remove their staging area.
    public static func recoverWorkspaceDeletionTransactions(at url: URL) throws {
        let database = try JobDatabase(url: url)
        try recoverWorkspaceDeletionTransactions(in: database, journals: readAll(from: database))
    }

    /// Injection seam for proving a failed journal query aborts recovery before
    /// it touches any staged file or queue row.
    static func recoverWorkspaceDeletionTransactions(at url: URL,
                                                     readJournals: () throws -> [WorkspaceDeletionJournal]) throws {
        let database = try JobDatabase(url: url)
        try recoverWorkspaceDeletionTransactions(in: database, journals: readJournals())
    }

    private static func recoverWorkspaceDeletionTransactions(in database: JobDatabase,
                                                              journals: [WorkspaceDeletionJournal]) throws {
        for journal in journals {
            switch journal.phase {
            case .staging:
                try WorkspaceDeletionFileStaging.rollback(journal.moves, stagingRoot: journal.stagingRoot)
                try database.execute("BEGIN IMMEDIATE")
                do {
                    try insertJobs(journal.jobs, into: database)
                    try deleteJournal(journal.transactionID, from: database)
                    try database.execute("COMMIT")
                } catch {
                    try? database.execute("ROLLBACK")
                    throw error
                }
            case .committed:
                try WorkspaceDeletionFileStaging.removeStagedFiles(at: journal.stagingRoot)
                try database.execute("BEGIN IMMEDIATE")
                do {
                    try deleteJournal(journal.transactionID, from: database)
                    try database.execute("COMMIT")
                } catch {
                    try? database.execute("ROLLBACK")
                    throw error
                }
            }
        }
    }

    private static func readAll(from database: JobDatabase) throws -> [WorkspaceDeletionJournal] {
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(database.handle, "SELECT body FROM workspace_deletion_journal", -1, &statement, nil) == SQLITE_OK else {
            throw CocoaError(.fileReadUnknown)
        }
        defer { sqlite3_finalize(statement) }
        return try SQLiteRowReader.collect(step: { sqlite3_step(statement) }) {
            guard let bytes = sqlite3_column_blob(statement, 0) else { throw CocoaError(.fileReadCorruptFile) }
            return try JSONDecoder().decode(WorkspaceDeletionJournal.self,
                                            from: Data(bytes: bytes, count: Int(sqlite3_column_bytes(statement, 0))))
        }
    }

    private static func read(_ id: UUID, from database: JobDatabase) throws -> WorkspaceDeletionJournal? {
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(database.handle, "SELECT body FROM workspace_deletion_journal WHERE id=?", -1, &statement, nil) == SQLITE_OK else {
            throw CocoaError(.fileReadUnknown)
        }
        defer { sqlite3_finalize(statement) }
        let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
        guard sqlite3_bind_text(statement, 1, id.uuidString, -1, transient) == SQLITE_OK else { throw CocoaError(.fileReadUnknown) }
        switch sqlite3_step(statement) {
        case SQLITE_ROW:
            guard let bytes = sqlite3_column_blob(statement, 0) else { throw CocoaError(.fileReadCorruptFile) }
            return try JSONDecoder().decode(WorkspaceDeletionJournal.self,
                                            from: Data(bytes: bytes, count: Int(sqlite3_column_bytes(statement, 0))))
        case SQLITE_DONE:
            return nil
        case let resultCode:
            throw SQLiteQueryFailure(resultCode: resultCode)
        }
    }

    private static func write(_ journal: WorkspaceDeletionJournal, to database: JobDatabase) throws {
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(database.handle, "INSERT OR REPLACE INTO workspace_deletion_journal(id,body) VALUES(?,?)", -1, &statement, nil) == SQLITE_OK else {
            throw CocoaError(.fileWriteUnknown)
        }
        defer { sqlite3_finalize(statement) }
        let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
        let data = try JSONEncoder().encode(journal)
        guard sqlite3_bind_text(statement, 1, journal.transactionID.uuidString, -1, transient) == SQLITE_OK,
              data.withUnsafeBytes({ sqlite3_bind_blob(statement, 2, $0.baseAddress, Int32(data.count), transient) }) == SQLITE_OK,
              sqlite3_step(statement) == SQLITE_DONE else { throw CocoaError(.fileWriteUnknown) }
    }

    private static func deleteJournal(_ id: UUID, from database: JobDatabase) throws {
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(database.handle, "DELETE FROM workspace_deletion_journal WHERE id=?", -1, &statement, nil) == SQLITE_OK else {
            throw CocoaError(.fileWriteUnknown)
        }
        defer { sqlite3_finalize(statement) }
        let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
        guard sqlite3_bind_text(statement, 1, id.uuidString, -1, transient) == SQLITE_OK,
              sqlite3_step(statement) == SQLITE_DONE else { throw CocoaError(.fileWriteUnknown) }
    }

    private static func deleteJobRows(_ ids: Set<UUID>, from database: JobDatabase) throws {
        guard !ids.isEmpty else { return }
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(database.handle, "DELETE FROM jobs WHERE id=?", -1, &statement, nil) == SQLITE_OK else {
            throw CocoaError(.fileWriteUnknown)
        }
        defer { sqlite3_finalize(statement) }
        let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
        for id in ids {
            sqlite3_reset(statement)
            sqlite3_clear_bindings(statement)
            guard sqlite3_bind_text(statement, 1, id.uuidString, -1, transient) == SQLITE_OK,
                  sqlite3_step(statement) == SQLITE_DONE else { throw CocoaError(.fileWriteUnknown) }
        }
    }

    private static func insertJobs(_ jobs: [DocumentJob], into database: JobDatabase) throws {
        guard !jobs.isEmpty else { return }
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(database.handle, "INSERT OR REPLACE INTO jobs(id,body) VALUES(?,?)", -1, &statement, nil) == SQLITE_OK else {
            throw CocoaError(.fileWriteUnknown)
        }
        defer { sqlite3_finalize(statement) }
        let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
        for job in jobs {
            let data = try JSONEncoder().encode(job)
            sqlite3_reset(statement)
            sqlite3_clear_bindings(statement)
            guard sqlite3_bind_text(statement, 1, job.id.uuidString, -1, transient) == SQLITE_OK,
                  data.withUnsafeBytes({ sqlite3_bind_blob(statement, 2, $0.baseAddress, Int32(data.count), transient) }) == SQLITE_OK,
                  sqlite3_step(statement) == SQLITE_DONE else { throw CocoaError(.fileWriteUnknown) }
        }
    }

    /// Restores job snapshots after a staged workspace deletion is cancelled.
    /// The deletion tombstone is cleared only after the rows have been written
    /// successfully, so a failed rollback cannot admit stale background saves.
    public func restoreDeleted(_ jobs: [DocumentJob]) throws {
        let uniqueJobs = Dictionary(jobs.map { ($0.id, $0) }, uniquingKeysWith: { _, latest in latest }).values
        guard !uniqueJobs.isEmpty else { return }
        var statement: OpaquePointer?
        try database.execute("BEGIN IMMEDIATE")
        do {
            guard sqlite3_prepare_v2(database.handle, "INSERT OR REPLACE INTO jobs(id,body) VALUES(?,?)", -1, &statement, nil) == SQLITE_OK else {
                throw CocoaError(.fileWriteUnknown)
            }
            defer { sqlite3_finalize(statement) }
            let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
            for job in uniqueJobs {
                let data = try JSONEncoder().encode(job)
                sqlite3_reset(statement)
                sqlite3_clear_bindings(statement)
                guard sqlite3_bind_text(statement, 1, job.id.uuidString, -1, transient) == SQLITE_OK,
                      data.withUnsafeBytes({ sqlite3_bind_blob(statement, 2, $0.baseAddress, Int32(data.count), transient) }) == SQLITE_OK,
                      sqlite3_step(statement) == SQLITE_DONE else {
                    throw CocoaError(.fileWriteUnknown)
                }
            }
            try database.execute("COMMIT")
            for job in uniqueJobs { latest[job.id] = job.updatedAt }
        } catch {
            try? database.execute("ROLLBACK")
            throw error
        }
    }

    public func all(recoverInterrupted: Bool = false) throws -> [DocumentJob] {
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(database.handle, "SELECT body FROM jobs", -1, &statement, nil) == SQLITE_OK else { throw CocoaError(.fileReadUnknown) }
        defer { sqlite3_finalize(statement) }
        var jobs: [DocumentJob] = []
        while sqlite3_step(statement) == SQLITE_ROW {
            guard let bytes = sqlite3_column_blob(statement, 0) else { throw CocoaError(.fileReadCorruptFile) }
            var job = try JSONDecoder().decode(DocumentJob.self, from: Data(bytes: bytes, count: Int(sqlite3_column_bytes(statement, 0))))
            if recoverInterrupted && !job.stage.terminal {
                if job.originalPath != nil { job.stage = job.documentID == nil ? .queued : .embedding; job.message = "已恢复任务，将复用完成的页面与片段。" }
                else if job.bookmark != nil { job.stage = .receiving }
                else { job.stage = .failed; job.message = "文件尚未接收完成，请重新选择文件。" }
            }
            jobs.append(job)
        }
        if recoverInterrupted { for job in jobs { try save(job) } }
        return jobs.sorted { $0.createdAt < $1.createdAt }
    }
}
