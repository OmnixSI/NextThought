import Foundation
import Testing
@testable import NextThoughtCore

struct DocumentDependencyTests {
    @Test func olderJobsWithoutTypedDependencyStillDecode() throws {
        let original = DocumentJob(workspaceID: UUID(), fileName: "paper.pdf")
        var object = try #require(JSONSerialization.jsonObject(with: JSONEncoder().encode(original)) as? [String: Any])
        object.removeValue(forKey: "waitingReason")
        let data = try JSONSerialization.data(withJSONObject: object)
        let recovered = try JSONDecoder().decode(DocumentJob.self, from: data)
        #expect(recovered == original)
        #expect(recovered.waitingReason == nil)
    }
    @Test func dependencyReasonSurvivesPersistenceWithReadyText() throws {
        var job = DocumentJob(workspaceID: UUID(), fileName: "paper.pdf")
        job.stage = .waiting
        job.documentID = UUID()
        job.waitingReason = .semantic
        job.message = "文字可用，等待语义组件"
        let recovered = try JSONDecoder().decode(DocumentJob.self, from: JSONEncoder().encode(job))
        #expect(recovered == job)
        #expect(recovered.documentID != nil && recovered.waitingReason == .semantic)
        #expect(DocumentDependencyError(reason: .runtime, message: "missing runtime").errorDescription == "missing runtime")
    }
}
