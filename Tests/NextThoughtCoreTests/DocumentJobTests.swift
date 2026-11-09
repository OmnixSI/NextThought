import Foundation
import Testing
import SQLite3
@testable import NextThoughtCore

struct DocumentJobTests {
    @Test func jobsKeepWorkspaceAndRecoverWithoutLosingFailures() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let store = try DocumentJobStore(url: root.appendingPathComponent("jobs.sqlite"))
        let workspace = UUID()
        var first = DocumentJob(workspaceID: workspace, fileName: "paper.pdf")
        first.originalPath = "/selected/original.pdf"; first.stage = .ocr; first.completed = 17; first.total = 80
        var failed = DocumentJob(workspaceID: UUID(), fileName: "bad.pdf")
        failed.stage = .failed; failed.message = "PDF 已加密"
        try await store.save(first); try await store.save(failed)
        let reopened = try DocumentJobStore(url: root.appendingPathComponent("jobs.sqlite"))
        let recovered = try await reopened.all(recoverInterrupted: true)
        #expect(recovered.count == 2)
        #expect(recovered.first { $0.id == first.id }?.workspaceID == workspace)
        #expect(recovered.first { $0.id == first.id }?.stage == .queued)
        #expect(recovered.first { $0.id == first.id }?.completed == 17)
        #expect(recovered.first { $0.id == failed.id }?.message == "PDF 已加密")
        #expect(recovered.first { $0.id == failed.id }?.stage == .failed)
    }
    @Test func committedDocumentResumesAtEmbeddingInsteadOfDuplicateImport() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let store = try DocumentJobStore(url: root.appendingPathComponent("jobs.sqlite"))
        var job = DocumentJob(workspaceID: UUID(), fileName: "deck.pptx")
        job.stage = .embedding; job.originalPath = "/selected/deck.pptx"; job.documentID = UUID()
        try await store.save(job)
        let all = try await store.all(recoverInterrupted: true)
        #expect(all.first?.documentID == job.documentID)
        #expect(all.first?.stage == .embedding)
        #expect(DocumentLimits.maximumFileBytes == 512 * 1024 * 1024)
        #expect(DocumentLimits.maximumPages == 2000)
    }

    @Test func stagedWorkspaceDeletionCanRestoreQueueRows() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let store = try DocumentJobStore(url: root.appendingPathComponent("jobs.sqlite"))
        var job = DocumentJob(workspaceID: UUID(), fileName: "meeting.pdf")
        job.originalPath = "/selected/meeting.pdf"
        job.stage = .ready
        job.updatedAt = Date(timeIntervalSince1970: 100)
        try await store.save(job)

        try await store.delete(ids: [job.id])
        var staleSave = job
        staleSave.message = "late save"
        staleSave.updatedAt = Date(timeIntervalSince1970: 200)
        try await store.save(staleSave)
        let whileStaged = try await store.all()
        #expect(whileStaged.isEmpty)

        try await store.restoreDeleted([job])
        let restored = try await store.all()
        #expect(restored == [job])
    }

    @Test func journalRowReaderThrowsInsteadOfTreatingSQLiteErrorsAsEndOfRows() throws {
        var results: [Int32] = [SQLITE_ROW, SQLITE_ROW, SQLITE_DONE]
        var rowValue = 0
        let rows = try SQLiteRowReader.collect(step: {
            results.removeFirst()
        }, makeRow: {
            rowValue += 1
            return rowValue
        })
        #expect(rows == [1, 2])

        results = [SQLITE_ROW, SQLITE_BUSY]
        rowValue = 0
        var receivedBusy = false
        do {
            _ = try SQLiteRowReader.collect(step: { results.removeFirst() }, makeRow: {
                rowValue += 1
                return rowValue
            })
        } catch let error as SQLiteQueryFailure {
            receivedBusy = error.resultCode == SQLITE_BUSY
        }
        #expect(receivedBusy)
        #expect(rowValue == 1)
    }

    @Test func failedJournalQueryAbortsStartupRecoveryBeforeTouchingStagedFiles() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("journal-query-failure-\(UUID())", isDirectory: true)
        let databaseURL = root.appendingPathComponent("Preparation/jobs.sqlite")
        let stagingRoot = root.appendingPathComponent(".workspace-delete", isDirectory: true)
        let source = root.appendingPathComponent("Workspaces/workspace.json")
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: source.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data("workspace".utf8).write(to: source)

        let job = DocumentJob(workspaceID: UUID(), fileName: "document.pdf")
        let moves = WorkspaceDeletionFileStaging.manifest(sources: [source], stagingRoot: stagingRoot)
        let journal = WorkspaceDeletionJournal(workspaceID: job.workspaceID, stagingRoot: stagingRoot,
                                               jobs: [job], moves: moves)
        let store = try DocumentJobStore(url: databaseURL)
        try await store.save(job)
        try await store.stageWorkspaceDeletion(journal)
        try WorkspaceDeletionFileStaging.stage(moves)

        var failedClosed = false
        do {
            try DocumentJobStore.recoverWorkspaceDeletionTransactions(at: databaseURL, readJournals: {
                throw SQLiteQueryFailure(resultCode: SQLITE_BUSY)
            })
        } catch let error as SQLiteQueryFailure {
            failedClosed = error.resultCode == SQLITE_BUSY
        }
        #expect(failedClosed)
        #expect(!FileManager.default.fileExists(atPath: source.path))
        #expect(FileManager.default.fileExists(atPath: moves[0].staged.path))
        #expect(try await store.all().isEmpty)
        #expect(try await store.workspaceDeletionJournals() == [journal])

        try DocumentJobStore.recoverWorkspaceDeletionTransactions(at: databaseURL)
        #expect(FileManager.default.fileExists(atPath: source.path))
        #expect(try await store.all() == [job])
    }

    @Test func oldDocumentContentDecodesWithoutNewRawText() throws {
        let json = #"{"schemaVersion":1,"pipeline":"system","blocks":[],"warnings":[],"status":"complete","components":{}}"#
        let content = try JSONDecoder().decode(DocumentContent.self, from: Data(json.utf8))
        #expect(content.rawPages == nil)
    }
}
