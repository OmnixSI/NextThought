import Foundation
import Testing
@testable import InterviewHackerCore

struct StabilityTests {
    @Test func companionCommandsRetryExactlyOnceAcrossTransports() {
        var cache = CompanionCommandCache()
        let id = UUID().uuidString, date = Date()
        let first = cache.accept(id, now: date)
        let duplicate = cache.accept(id, now: date.addingTimeInterval(2))
        let invalid = cache.accept("bad")
        let expired = cache.accept(id, now: date.addingTimeInterval(301))
        #expect(first && !duplicate && !invalid && expired)
    }
    @Test func snapshotFrameCarriesOrderingAndFreshness() throws {
        let snapshot = CompanionSnapshot(status: "就绪", question: "新问题", answer: "", answering: true, candidate: "", position: "", answerID: "new", phase: "generating")
        let envelope = CompanionEnvelope(serverID: "epoch", sequence: 9, snapshot: snapshot)
        let text = String(decoding: envelope.sse(), as: UTF8.self)
        #expect(text.contains("id: epoch:9\n"))
        #expect(text.contains("\"answerID\":\"new\""))
        #expect(try JSONDecoder().decode(CompanionEnvelope.self, from: JSONEncoder().encode(envelope)) == envelope)
    }
    @Test func incrementalBackupDeduplicatesRecoversAndRejectsCorruption() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let store = IncrementalBackupStore(root: root)
        let files = ["Workspaces/a.json": Data("workspace".utf8), "Sessions/a/meta.json": Data("session".utf8)]
        let first = try await store.create(files: files, reason: "session")
        let duplicate = try await store.create(files: files, reason: "update")
        #expect(first == duplicate)
        let second = try await store.create(files: files.merging(["Sessions/b/meta.json": Data("next".utf8)]) { _, b in b }, reason: "session")
        #expect(first != second)
        #expect(try FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("Objects").path).count == 3)
        let recovered = root.appendingPathComponent("recovered")
        try await store.restore(first, into: recovered)
        #expect(try Data(contentsOf: recovered.appendingPathComponent("Workspaces/a.json")) == files["Workspaces/a.json"])
        let object = root.appendingPathComponent("Objects/" + IncrementalBackupStore.digest(Data("workspace".utf8)))
        try Data("broken".utf8).write(to: object)
        await #expect(throws: (any Error).self) { try await store.restore(first, into: root.appendingPathComponent("bad")) }
        #expect(!FileManager.default.fileExists(atPath: root.appendingPathComponent("bad").path))
    }
    @Test func failedBackupNeverPrunesTheLastRecoveryPoint() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let store = IncrementalBackupStore(root: root)
        let first = try await store.create(files: ["a": Data("safe".utf8)], reason: "manual", budget: 0)
        await #expect(throws: (any Error).self) { try await store.create(files: ["../escape": Data()], reason: "import", budget: 0) }
        #expect(FileManager.default.fileExists(atPath: first.path))
        let second = try await store.create(files: ["a": Data("new".utf8)], reason: "session", budget: 0)
        #expect(FileManager.default.fileExists(atPath: second.path))
        #expect(try FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("RestorePoints").path).count == 1)
    }
}
