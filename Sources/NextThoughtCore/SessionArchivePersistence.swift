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
    public var state: State {
        if lastFailure != nil { return .blocked }
        return pendingEvents == 0 && submittedRevision == persistedRevision ? .saved : .saving
    }

    public init(submittedRevision: Int, persistedRevision: Int, pendingEvents: Int, pendingBytes: Int,
                lastPersistedAt: Date?, lastFailure: String?) {
        self.submittedRevision = submittedRevision
        self.persistedRevision = persistedRevision
        self.pendingEvents = pendingEvents
        self.pendingBytes = pendingBytes
        self.lastPersistedAt = lastPersistedAt
        self.lastFailure = lastFailure
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
    private var overflowed = false
    private var permanentFailure = false
    private var permanentFailureMessage: String?

    public init(maximumPendingBytes: Int = 8 * 1_024 * 1_024, maximumPendingEvents: Int = 16_384) {
        self.maximumPendingBytes = max(1, maximumPendingBytes)
        self.maximumPendingEvents = max(1, maximumPendingEvents)
    }

    public var hasPermanentFailure: Bool { permanentFailure }

    @discardableResult
    public mutating func enqueue(fileName: String, data: Data, mode: SessionArchiveWriteMode = .append) -> EnqueueResult {
        guard !overflowed else { return .full }
        guard entries.count < maximumPendingEvents,
              data.count <= maximumPendingBytes - pendingBytes else {
            overflowed = true
            permanentFailure = true
            permanentFailureMessage = "本机待保存队列已满；请结束记录并检查磁盘空间。"
            lastFailure = permanentFailureMessage
            return .full
        }
        submittedRevision += 1
        entries.append(Entry(revision: submittedRevision, fileName: fileName, data: data, mode: mode))
        pendingBytes += data.count
        return .queued(revision: submittedRevision)
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
        overflowed = false
        permanentFailure = false
        permanentFailureMessage = nil
        lastFailure = nil
    }

    public var snapshot: SessionPersistenceSnapshot {
        SessionPersistenceSnapshot(submittedRevision: submittedRevision, persistedRevision: persistedRevision,
                                  pendingEvents: entries.count, pendingBytes: pendingBytes,
                                  lastPersistedAt: lastPersistedAt, lastFailure: lastFailure)
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

    public static let live = SessionArchiveFileAppender(append: { data, url in
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
            try handle.write(contentsOf: data)
            try handle.synchronize()
            try handle.close()
        } catch {
            // If a write fails part-way through, roll back the partial line before retrying.
            do { try handle.truncate(atOffset: originalEnd); try handle.close() }
            catch { throw CocoaError(.fileWriteUnknown, userInfo: [NSUnderlyingErrorKey: error]) }
            throw error
        }
    })
}
