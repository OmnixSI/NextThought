import Foundation

public struct WorkspaceDeletionFileMove: Codable, Sendable, Equatable {
    public var source: URL
    public var staged: URL

    public init(source: URL, staged: URL) {
        self.source = source
        self.staged = staged
    }
}

public struct WorkspaceDeletionJournal: Codable, Sendable, Equatable {
    public enum Phase: String, Codable, Sendable {
        case staging
        case committed
    }

    public var transactionID: UUID
    public var workspaceID: UUID
    public var phase: Phase
    public var stagingRoot: URL
    public var jobs: [DocumentJob]
    public var moves: [WorkspaceDeletionFileMove]

    public init(transactionID: UUID = UUID(), workspaceID: UUID, stagingRoot: URL,
                jobs: [DocumentJob], moves: [WorkspaceDeletionFileMove], phase: Phase = .staging) {
        self.transactionID = transactionID
        self.workspaceID = workspaceID
        self.phase = phase
        self.stagingRoot = stagingRoot
        self.jobs = jobs
        self.moves = moves
    }
}

public enum WorkspaceDeletionFileStaging {
    /// Builds a durable ordered move manifest. Pass the workspace record last so
    /// its absence marks the point at which recovery can treat deletion as committed.
    public static func manifest(sources: [URL], stagingRoot: URL) -> [WorkspaceDeletionFileMove] {
        var seen = Set<String>()
        let unique = sources.filter { seen.insert($0.standardizedFileURL.path).inserted }
        return unique.enumerated().map { index, source in
            let staged = stagingRoot.appendingPathComponent("\(index)-\(source.lastPathComponent)",
                                                            isDirectory: source.hasDirectoryPath)
            return WorkspaceDeletionFileMove(source: source, staged: staged)
        }
    }

    /// Removes an original from the move set if a current, non-deleted job or
    /// workspace references its hash/path. Call immediately before staging,
    /// after all awaits that could add a reference.
    public static func excludingSharedOriginals(
        _ moves: [WorkspaceDeletionFileMove],
        deletingJobs: [DocumentJob],
        currentJobs: [DocumentJob],
        currentWorkspaceSourceHashes: Set<String>,
        originalsRoot: URL
    ) -> [WorkspaceDeletionFileMove] {
        let deletingIDs = Set(deletingJobs.map(\.id))
        let remainingJobs = currentJobs.filter { !deletingIDs.contains($0.id) }
        let retainedHashes = Set(remainingJobs.compactMap(\.sourceHash)).union(currentWorkspaceSourceHashes)
        let retainedPaths = Set(remainingJobs.compactMap(\.originalPath).map {
            URL(fileURLWithPath: $0).standardizedFileURL.path
        })
        let originalsPrefix = originalsRoot.standardizedFileURL.path + "/"
        let deletingHashesByPath = Dictionary(deletingJobs.compactMap { job -> (String, String)? in
            guard let path = job.originalPath, let hash = job.sourceHash else { return nil }
            return (URL(fileURLWithPath: path).standardizedFileURL.path, hash)
        }, uniquingKeysWith: { _, latest in latest })

        return moves.filter { move in
            let path = move.source.standardizedFileURL.path
            guard path.hasPrefix(originalsPrefix) else { return true }
            guard let hash = deletingHashesByPath[path] else { return true }
            return !retainedPaths.contains(path) && !retainedHashes.contains(hash)
        }
    }

    public static func stage(_ moves: [WorkspaceDeletionFileMove],
                             move: (URL, URL) throws -> Void = { try FileManager.default.moveItem(at: $0, to: $1) }) throws {
        guard let stagingRoot = moves.first?.staged.deletingLastPathComponent() else { return }
        try FileManager.default.createDirectory(at: stagingRoot, withIntermediateDirectories: true)
        for item in moves where FileManager.default.fileExists(atPath: item.source.path) {
            try FileManager.default.createDirectory(at: item.staged.deletingLastPathComponent(), withIntermediateDirectories: true)
            try move(item.source, item.staged)
        }
    }

    /// Restores whichever paths were already moved; untouched sources are skipped.
    public static func rollback(_ moves: [WorkspaceDeletionFileMove], stagingRoot explicitStagingRoot: URL? = nil,
                                move: (URL, URL) throws -> Void = { try FileManager.default.moveItem(at: $0, to: $1) }) throws {
        for item in moves.reversed() where FileManager.default.fileExists(atPath: item.staged.path) {
            guard !FileManager.default.fileExists(atPath: item.source.path) else {
                throw CocoaError(.fileWriteFileExists, userInfo: [NSFilePathErrorKey: item.source.path])
            }
            try FileManager.default.createDirectory(at: item.source.deletingLastPathComponent(), withIntermediateDirectories: true)
            try move(item.staged, item.source)
        }
        if let stagingRoot = explicitStagingRoot ?? moves.first?.staged.deletingLastPathComponent(),
           FileManager.default.fileExists(atPath: stagingRoot.path) {
            try FileManager.default.removeItem(at: stagingRoot)
        }
    }

    /// Discards the moved files after a committed deletion. The journal remains
    /// until this succeeds, so a later launch can retry cleanup safely.
    public static func removeStagedFiles(at stagingRoot: URL) throws {
        guard FileManager.default.fileExists(atPath: stagingRoot.path) else { return }
        try FileManager.default.removeItem(at: stagingRoot)
    }

}

/// Runs a reversible preparation step, checks authorization after that step's
/// suspension points, and only then enters a synchronous commit section.
/// The caller keeps any prepared side effects reversible until `commit` returns.
@MainActor
public enum WorkspaceDeletionTransaction {
    @discardableResult
    public static func execute<Plan>(
        prepare: @MainActor () async throws -> Plan,
        isAuthorized: @MainActor (Plan) -> Bool,
        rollback: @MainActor (Plan) async throws -> Void,
        stage: @MainActor (Plan) throws -> Void = { _ in },
        commit: @MainActor (Plan) throws -> Void
    ) async throws -> Bool {
        let plan = try await prepare()
        guard isAuthorized(plan) else {
            try await rollback(plan)
            return false
        }

        do { try stage(plan) }
        catch {
            try await rollback(plan)
            throw error
        }
        guard isAuthorized(plan) else {
            try await rollback(plan)
            return false
        }
        do {
            try commit(plan)
            return true
        } catch {
            try await rollback(plan)
            throw error
        }
    }
}
