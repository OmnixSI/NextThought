import Foundation

/// Snapshot shown to the user. A revision is acknowledged only after the file handle
/// successfully writes and synchronizes the corresponding bytes.
public struct SessionPersistenceSnapshot: Sendable, Equatable {
    public enum State: Sendable, Equatable { case saved, saving, blocked }

    public var submittedRevision: Int
    public var persistedRevision: Int
    public var pendingEvents: Int
    public var pendingBytes: Int
    public var lastPersistedAt: Date?
    public var lastFailure: String?
    /// Writes rejected after the bounded queue filled. These bytes were never accepted
    /// and therefore cannot be represented as saved by a later receipt.
    public var unqueuedWriteCount: Int
    public var acknowledgedUnqueuedWriteCount: Int
    public var canAcknowledgeDataLoss: Bool
    public var hasUnacknowledgedDataLoss: Bool { unqueuedWriteCount > acknowledgedUnqueuedWriteCount }
    public var dataLossAcknowledged: Bool {
        unqueuedWriteCount > 0 && acknowledgedUnqueuedWriteCount >= unqueuedWriteCount
    }
    public var state: State {
        if lastFailure != nil { return .blocked }
        return pendingEvents == 0 && submittedRevision == persistedRevision ? .saved : .saving
    }

    public init(submittedRevision: Int, persistedRevision: Int, pendingEvents: Int, pendingBytes: Int,
                lastPersistedAt: Date?, lastFailure: String?, unqueuedWriteCount: Int = 0,
                acknowledgedUnqueuedWriteCount: Int = 0, canAcknowledgeDataLoss: Bool = false) {
        self.submittedRevision = submittedRevision
        self.persistedRevision = persistedRevision
        self.pendingEvents = pendingEvents
        self.pendingBytes = pendingBytes
        self.lastPersistedAt = lastPersistedAt
        self.lastFailure = lastFailure
        self.unqueuedWriteCount = unqueuedWriteCount
        self.acknowledgedUnqueuedWriteCount = acknowledgedUnqueuedWriteCount
        self.canAcknowledgeDataLoss = canAcknowledgeDataLoss
    }
}

public enum SessionArchiveWriteMode: Sendable, Equatable {
    case append
    case replace
}

/// Bounded per-session write-ahead queue. Keeping bytes until an explicit receipt makes
/// transient disk errors retryable and makes overflow visible instead of silently dropping
/// an unbounded amount of meeting content into memory.
public struct SessionArchiveWriteQueue: Sendable {
    public struct Entry: Sendable, Equatable {
        public var revision: Int
        public var fileName: String
        public var data: Data
        public var mode: SessionArchiveWriteMode
        public init(revision: Int, fileName: String, data: Data, mode: SessionArchiveWriteMode = .append) {
            self.revision = revision; self.fileName = fileName; self.data = data; self.mode = mode
        }
    }

    public enum EnqueueResult: Sendable, Equatable {
        case queued(revision: Int)
        case full
    }

    public let maximumPendingBytes: Int
    public let maximumPendingEvents: Int
    public private(set) var entries: [Entry] = []
    public private(set) var submittedRevision = 0
    public private(set) var persistedRevision = 0
    public private(set) var pendingBytes = 0
    public private(set) var lastPersistedAt: Date?
    public private(set) var lastFailure: String?
    public private(set) var unqueuedWriteCount = 0
    public private(set) var acknowledgedUnqueuedWriteCount = 0
    private var overflowed = false
    private var permanentFailure = false
    private var permanentFailureMessage: String?

    public init(maximumPendingBytes: Int = 8 * 1_024 * 1_024, maximumPendingEvents: Int = 16_384) {
        self.maximumPendingBytes = max(1, maximumPendingBytes)
        self.maximumPendingEvents = max(1, maximumPendingEvents)
    }

    public var hasPermanentFailure: Bool { permanentFailure }
    /// The user can acknowledge the missing writes only after every accepted write is safely saved.
    public var canAcknowledgeDataLoss: Bool { overflowed && entries.isEmpty }

    @discardableResult
    public mutating func enqueue(fileName: String, data: Data, mode: SessionArchiveWriteMode = .append) -> EnqueueResult {
        guard !overflowed else {
            recordUnqueuedWrite()
            return .full
        }
        guard entries.count < maximumPendingEvents,
              data.count <= maximumPendingBytes - pendingBytes else {
            recordUnqueuedWrite()
            return .full
        }
        submittedRevision += 1
        entries.append(Entry(revision: submittedRevision, fileName: fileName, data: data, mode: mode))
        pendingBytes += data.count
        return .queued(revision: submittedRevision)
    }

    /// Accepts the recorded loss explicitly and reopens the queue for future writes.
    /// The count remains in every later snapshot so the archive never appears to have
    /// saved the writes that were rejected during overflow.
    @discardableResult
    public mutating func acknowledgeDataLossAndResume() -> Bool {
        guard canAcknowledgeDataLoss else { return false }
        overflowed = false
        permanentFailure = false
        permanentFailureMessage = nil
        lastFailure = nil
        acknowledgedUnqueuedWriteCount = unqueuedWriteCount
        return true
    }

    /// Acknowledge only a completed prefix; entries after a partial batch failure remain.
    public mutating func acknowledge(through revision: Int, at date: Date = Date()) {
        guard revision > persistedRevision else { return }
        let completedCount = entries.prefix { $0.revision <= revision }.count
        guard completedCount > 0, entries[completedCount - 1].revision == revision else { return }
        let completed = entries.prefix(completedCount)
        pendingBytes -= completed.reduce(0) { $0 + $1.data.count }
        entries.removeFirst(completedCount)
        persistedRevision = revision
        lastPersistedAt = date
        if entries.isEmpty && !permanentFailure { lastFailure = nil }
    }

    public mutating func fail(_ message: String, permanent: Bool = false) {
        if permanent {
            permanentFailure = true
            permanentFailureMessage = message
        }
        lastFailure = permanentFailureMessage ?? message
    }

    /// A newer full snapshot supersedes every pending replacement snapshot.
    /// Call only after preserving the source value from which that new snapshot is encoded.
    public mutating func discardPendingForReplacement() {
        entries.removeAll()
        pendingBytes = 0
        submittedRevision = persistedRevision
        if unqueuedWriteCount > acknowledgedUnqueuedWriteCount {
            overflowed = true
            permanentFailure = true
            permanentFailureMessage = "待保存队列已满；部分新写入没有入队。"
            lastFailure = permanentFailureMessage
        } else {
            overflowed = false
            permanentFailure = false
            permanentFailureMessage = nil
            lastFailure = nil
        }
    }

    public var snapshot: SessionPersistenceSnapshot {
        SessionPersistenceSnapshot(submittedRevision: submittedRevision, persistedRevision: persistedRevision,
                                  pendingEvents: entries.count, pendingBytes: pendingBytes,
                                  lastPersistedAt: lastPersistedAt, lastFailure: lastFailure,
                                  unqueuedWriteCount: unqueuedWriteCount,
                                  acknowledgedUnqueuedWriteCount: acknowledgedUnqueuedWriteCount,
                                  canAcknowledgeDataLoss: canAcknowledgeDataLoss)
    }

    private mutating func recordUnqueuedWrite() {
        unqueuedWriteCount += 1
        overflowed = true
        permanentFailure = true
        permanentFailureMessage = "待保存队列已满；新写入没有入队。"
        lastFailure = permanentFailureMessage
    }
}

/// Tracks translation write revisions that should refresh a session after a successful receipt.
/// The archive keeps one tracker per session and only acknowledges revisions reported persisted
/// by `SessionArchiveWriteQueue`.
public struct SessionTranslationWriteReceipts: Sendable, Equatable {
    private var pendingRevisions: Set<Int> = []

    public init() {}

    public var hasPending: Bool { !pendingRevisions.isEmpty }

    public mutating func recordSubmittedRevision(_ revision: Int) {
        guard revision > 0 else { return }
        pendingRevisions.insert(revision)
    }

    /// Returns true when this receipt acknowledges one or more tracked translation writes.
    @discardableResult
    public mutating func acknowledge(persistedRevision: Int) -> Bool {
        let persisted = pendingRevisions.filter { $0 <= persistedRevision }
        guard !persisted.isEmpty else { return false }
        pendingRevisions.subtract(persisted)
        return true
    }
}

/// Injectable archive writer used by the app and fault-injection tests.
public struct SessionArchiveFileAppender: Sendable {
    private let writeBytes: @Sendable (Data, URL, SessionArchiveWriteMode) throws -> Void

    public init(write: @escaping @Sendable (Data, URL, SessionArchiveWriteMode) throws -> Void) {
        self.writeBytes = write
    }

    public init(append: @escaping @Sendable (Data, URL) throws -> Void) {
        self.writeBytes = { data, url, mode in
            if mode == .append { try append(data, url) }
            else { try Self.replace(data, at: url) }
        }
    }

    /// Internal low-level hook for exercising rollback after a file handle has
    /// written only part of an append. Production callers should use `live`.
    static func usingFileHandleAppend(_ writeData: @escaping @Sendable (Data, FileHandle) throws -> Void) -> Self {
        Self(write: { data, url, mode in
            if mode == .append { try Self.append(data, to: url, writeData: writeData) }
            else { try Self.replace(data, at: url) }
        })
    }

    public func append(_ data: Data, to url: URL) throws {
        try writeBytes(data, url, .append)
    }

    public func replace(_ data: Data, at url: URL) throws {
        try writeBytes(data, url, .replace)
    }

    public func write(_ data: Data, to url: URL, mode: SessionArchiveWriteMode) throws {
        try writeBytes(data, url, mode)
    }

    private static func replace(_ data: Data, at url: URL) throws {
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        try data.write(to: url, options: .atomic)
        let handle = try FileHandle(forWritingTo: url)
        do {
            try handle.synchronize()
            try handle.close()
        } catch {
            try? handle.close()
            throw error
        }
    }

    private static func append(_ data: Data, to url: URL,
                              writeData: @Sendable (Data, FileHandle) throws -> Void) throws {
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        if !FileManager.default.fileExists(atPath: url.path) {
            guard FileManager.default.createFile(atPath: url.path, contents: nil) else {
                throw CocoaError(.fileWriteUnknown)
            }
        }
        let handle = try FileHandle(forUpdating: url)
        let originalEnd = try handle.seekToEnd()
        do {
            // Older versions normally wrote a trailing newline, but preserve a
            // valid historical last record that happens to lack one. A torn
            // final record stays isolated so later records remain recoverable.
            if originalEnd > 0 {
                try handle.seek(toOffset: originalEnd - 1)
                if try handle.read(upToCount: 1) != Data([0x0A]) {
                    try handle.seekToEnd()
                    try handle.write(contentsOf: Data([0x0A]))
                }
            }
            try writeData(data, handle)
            try handle.synchronize()
            try handle.close()
        } catch {
            // If a write fails part-way through, roll back the partial line before retrying.
            do { try handle.truncate(atOffset: originalEnd); try handle.close() }
            catch { throw CocoaError(.fileWriteUnknown, userInfo: [NSUnderlyingErrorKey: error]) }
            throw error
        }
    }

    public static let live = SessionArchiveFileAppender.usingFileHandleAppend { data, handle in
        try handle.write(contentsOf: data)
    }
}
