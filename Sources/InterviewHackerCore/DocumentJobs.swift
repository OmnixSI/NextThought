import Foundation
import SQLite3

public enum DocumentLimits {
    public static let maximumFileBytes = 512 * 1024 * 1024
    public static let maximumPages = 2000
    public static let maximumExpandedBytes = 2 * 1024 * 1024 * 1024
}

public struct DocumentJob: Codable, Sendable, Equatable, Identifiable {
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
        try execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS jobs (id TEXT PRIMARY KEY, body BLOB NOT NULL);")
    }
    func execute(_ sql: String) throws {
        guard sqlite3_exec(handle, sql, nil, nil, nil) == SQLITE_OK else { throw CocoaError(.fileWriteUnknown) }
    }
    deinit { sqlite3_close(handle) }
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
