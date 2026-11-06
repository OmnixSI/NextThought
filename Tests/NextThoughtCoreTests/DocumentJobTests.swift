import Foundation
import Testing
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
    @Test func oldDocumentContentDecodesWithoutNewRawText() throws {
        let json = #"{"schemaVersion":1,"pipeline":"system","blocks":[],"warnings":[],"status":"complete","components":{}}"#
        let content = try JSONDecoder().decode(DocumentContent.self, from: Data(json.utf8))
        #expect(content.rawPages == nil)
    }
}
