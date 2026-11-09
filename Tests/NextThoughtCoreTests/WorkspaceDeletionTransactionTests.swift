import Foundation
import Testing
@testable import NextThoughtCore

@MainActor
struct WorkspaceDeletionTransactionTests {
    @Test func readFailureDuringSuspendedArtifactPreparationCancelsBeforeFileDeletion() async throws {
        let artifact = FileManager.default.temporaryDirectory
            .appendingPathComponent("workspace-delete-artifact-\(UUID().uuidString)")
        try Data("keep me".utf8).write(to: artifact)
        defer { try? FileManager.default.removeItem(at: artifact) }

        var archiveReadable = true
        var stagedJobRowsExist = true
        var committed = false

        let didDelete = try await WorkspaceDeletionTransaction.execute(
            prepare: {
                // This models a reversible queue-row staging step. No document
                // artifact is removed until the post-await authorization check.
                stagedJobRowsExist = false
                await Task.yield()
                archiveReadable = false // read failure arrives during preparation's suspension
                return artifact
            },
            isAuthorized: { _ in archiveReadable },
            rollback: { _ in stagedJobRowsExist = true },
            commit: { artifactURL in
                try FileManager.default.removeItem(at: artifactURL)
                committed = true
            })
        #expect(!didDelete)
        #expect(!committed)
        #expect(stagedJobRowsExist)
        #expect(FileManager.default.fileExists(atPath: artifact.path))
    }

    @Test func injectedSynchronousMoveFailureRestoresEveryWorkspaceResource() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("workspace-delete-rollback-\(UUID().uuidString)", isDirectory: true)
        let stagingRoot = root.appendingPathComponent(".workspace-delete-test", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)

        let sources: [(String, URL, Bool)] = [
            ("session archive", root.appendingPathComponent("Sessions/session", isDirectory: true), true),
            ("material archive", root.appendingPathComponent("Materials/workspace", isDirectory: true), true),
            ("semantic index", root.appendingPathComponent("Index/workspace", isDirectory: true), true),
            ("workspace record", root.appendingPathComponent("Workspaces/workspace.json"), false),
            ("document artifact", root.appendingPathComponent("Preparation/job", isDirectory: true), true)
        ]
        for (name, url, isDirectory) in sources {
            if isDirectory {
                try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
                try Data(name.utf8).write(to: url.appendingPathComponent("payload"))
            } else {
                try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
                try Data(name.utf8).write(to: url)
            }
        }
        let moves = WorkspaceDeletionFileStaging.manifest(sources: sources.map(\.1), stagingRoot: stagingRoot)
        let databaseURL = root.appendingPathComponent("Preparation/jobs.sqlite")
        let job = DocumentJob(workspaceID: UUID(), fileName: "source.pdf")
        let journal = WorkspaceDeletionJournal(workspaceID: job.workspaceID, stagingRoot: stagingRoot,
                                               jobs: [job], moves: moves)
        var store: DocumentJobStore?
        var moveCount = 0
        var committed = false
        var failed = false
        do {
            _ = try await WorkspaceDeletionTransaction.execute(
                prepare: {
                    let opened = try DocumentJobStore(url: databaseURL)
                    try await opened.save(job)
                    try await opened.stageWorkspaceDeletion(journal)
                    store = opened
                    return journal
                },
                isAuthorized: { _ in true },
                rollback: { journal in
                    try WorkspaceDeletionFileStaging.rollback(journal.moves, stagingRoot: stagingRoot)
                    try await store?.restoreWorkspaceDeletionRows(journal.transactionID)
                },
                stage: { journal in
                    try WorkspaceDeletionFileStaging.stage(journal.moves) { source, staged in
                        moveCount += 1
                        try FileManager.default.moveItem(at: source, to: staged)
                        if moveCount == 3 { throw CocoaError(.fileWriteUnknown) }
                    }
                },
                commit: { _ in committed = true })
        } catch { failed = true }

        #expect(failed)
        #expect(!committed)
        #expect(moveCount == 3)
        #expect(try await store?.all() == [job])
        #expect(try await store?.workspaceDeletionJournals().isEmpty == true)
        for (name, source, isDirectory) in sources {
            #expect(FileManager.default.fileExists(atPath: source.path))
            let payload = isDirectory ? source.appendingPathComponent("payload") : source
            #expect(try Data(contentsOf: payload) == Data(name.utf8))
        }
        #expect(!FileManager.default.fileExists(atPath: stagingRoot.path))
    }

    @Test func sharedOriginalIsRevalidatedWhenNewJobOrWorkspaceReferencesArrive() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("workspace-delete-shared-original-\(UUID().uuidString)", isDirectory: true)
        let originals = root.appendingPathComponent("Originals", isDirectory: true)
        let original = originals.appendingPathComponent("shared.pdf")
        let stagingRoot = root.appendingPathComponent(".workspace-delete", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: originals, withIntermediateDirectories: true)
        try Data("shared source".utf8).write(to: original)

        var deletingJob = DocumentJob(workspaceID: UUID(), fileName: "shared.pdf")
        deletingJob.originalPath = original.path
        deletingJob.sourceHash = "shared-hash"
        let moves = WorkspaceDeletionFileStaging.manifest(sources: [original], stagingRoot: stagingRoot)
        let initiallyUnshared = WorkspaceDeletionFileStaging.excludingSharedOriginals(
            moves, deletingJobs: [deletingJob], currentJobs: [deletingJob],
            currentWorkspaceSourceHashes: [], originalsRoot: originals)
        #expect(initiallyUnshared == moves)

        var newJob = DocumentJob(workspaceID: UUID(), fileName: "copy.pdf")
        newJob.originalPath = root.appendingPathComponent("elsewhere/copy.pdf").path
        newJob.sourceHash = "shared-hash"
        let sharedByNewJob = WorkspaceDeletionFileStaging.excludingSharedOriginals(
            moves, deletingJobs: [deletingJob], currentJobs: [deletingJob, newJob],
            currentWorkspaceSourceHashes: [], originalsRoot: originals)
        #expect(sharedByNewJob.isEmpty)

        let sharedByNewWorkspace = WorkspaceDeletionFileStaging.excludingSharedOriginals(
            moves, deletingJobs: [deletingJob], currentJobs: [deletingJob],
            currentWorkspaceSourceHashes: ["shared-hash"], originalsRoot: originals)
        #expect(sharedByNewWorkspace.isEmpty)
        try WorkspaceDeletionFileStaging.stage(sharedByNewWorkspace)
        #expect(FileManager.default.fileExists(atPath: original.path))
        #expect(!FileManager.default.fileExists(atPath: stagingRoot.path))
    }

    @Test func restartRecoveryRestoresQueueRowsAndPartiallyMovedWorkspaceFiles() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("workspace-delete-restart-\(UUID().uuidString)", isDirectory: true)
        let databaseURL = root.appendingPathComponent("Preparation/jobs.sqlite")
        let stagingRoot = root.appendingPathComponent("Preparation/.workspace-delete-crash", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }

        let resourceNames = ["Sessions/session", "Materials/workspace", "Index/workspace",
                             "Workspaces/workspace.json", "Preparation/job", "Originals/source.pdf"]
        let sourceURLs = resourceNames.map { relative -> URL in
            let isDirectory = !relative.hasSuffix(".json") && !relative.hasSuffix(".pdf")
            return root.appendingPathComponent(relative, isDirectory: isDirectory)
        }
        for (index, source) in sourceURLs.enumerated() {
            if source.hasDirectoryPath {
                try FileManager.default.createDirectory(at: source, withIntermediateDirectories: true)
                try Data(resourceNames[index].utf8).write(to: source.appendingPathComponent("payload"))
            } else {
                try FileManager.default.createDirectory(at: source.deletingLastPathComponent(), withIntermediateDirectories: true)
                try Data(resourceNames[index].utf8).write(to: source)
            }
        }
        let job = DocumentJob(workspaceID: UUID(), fileName: "source.pdf")
        let journal = WorkspaceDeletionJournal(workspaceID: job.workspaceID, stagingRoot: stagingRoot,
                                               jobs: [job], moves: WorkspaceDeletionFileStaging.manifest(sources: sourceURLs, stagingRoot: stagingRoot))
        do {
            let store = try DocumentJobStore(url: databaseURL)
            try await store.save(job)
            try await store.stageWorkspaceDeletion(journal)
            try FileManager.default.createDirectory(at: stagingRoot, withIntermediateDirectories: true)
            for item in journal.moves.prefix(3) {
                try FileManager.default.moveItem(at: item.source, to: item.staged)
            }
        }

        // A new process opens the same SQLite journal and recovers before it
        // loads workspace or session state.
        try DocumentJobStore.recoverWorkspaceDeletionTransactions(at: databaseURL)
        let restartedStore = try DocumentJobStore(url: databaseURL)
        #expect(try await restartedStore.all() == [job])
        #expect(try await restartedStore.workspaceDeletionJournals().isEmpty)
        for (index, source) in sourceURLs.enumerated() {
            #expect(FileManager.default.fileExists(atPath: source.path))
            let payload = source.hasDirectoryPath ? source.appendingPathComponent("payload") : source
            #expect(try Data(contentsOf: payload) == Data(resourceNames[index].utf8))
        }
        #expect(!FileManager.default.fileExists(atPath: stagingRoot.path))
    }

    @Test func restartRecoveryFinishesCommittedDeletionCleanup() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("workspace-delete-commit-\(UUID().uuidString)", isDirectory: true)
        let databaseURL = root.appendingPathComponent("Preparation/jobs.sqlite")
        let stagingRoot = root.appendingPathComponent(".workspace-delete-committed", isDirectory: true)
        let workspaceFile = root.appendingPathComponent("Workspaces/workspace.json")
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: workspaceFile.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data("workspace".utf8).write(to: workspaceFile)
        let job = DocumentJob(workspaceID: UUID(), fileName: "source.pdf")
        let moves = WorkspaceDeletionFileStaging.manifest(sources: [workspaceFile], stagingRoot: stagingRoot)
        let journal = WorkspaceDeletionJournal(workspaceID: job.workspaceID, stagingRoot: stagingRoot,
                                               jobs: [job], moves: moves)
        do {
            let store = try DocumentJobStore(url: databaseURL)
            try await store.save(job)
            try await store.stageWorkspaceDeletion(journal)
            try WorkspaceDeletionFileStaging.stage(moves)
            try DocumentJobStore.markWorkspaceDeletionCommitted(journal.transactionID, at: databaseURL)
        }

        try DocumentJobStore.recoverWorkspaceDeletionTransactions(at: databaseURL)
        let restartedStore = try DocumentJobStore(url: databaseURL)
        #expect(try await restartedStore.all().isEmpty)
        #expect(try await restartedStore.workspaceDeletionJournals().isEmpty)
        #expect(!FileManager.default.fileExists(atPath: workspaceFile.path))
        #expect(!FileManager.default.fileExists(atPath: stagingRoot.path))
    }

}
