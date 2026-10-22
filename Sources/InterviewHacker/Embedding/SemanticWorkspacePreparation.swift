import Foundation
import InterviewHackerCore

actor SemanticWorkspacePreparation {
    static let shared = SemanticWorkspacePreparation()
    private var active: [UUID: (String, Task<SemanticIndex, Error>)] = [:]
    func build(workspaceID: UUID, documents: [ContextDocument], progress: @escaping @Sendable (Int, Int) -> Void) async throws -> SemanticIndex {
        let signature = ContextFingerprint.hash(documents.map { $0.id.uuidString + ($0.content?.sourceHash ?? String($0.characterCount)) + String($0.enabled) }.joined())
        if let (key, task) = active[workspaceID] {
            let result = try await task.value
            if key == signature { return result }
        }
        let task = Task.detached(priority: .utility) { () throws -> SemanticIndex in
            let chunks = ContextChunker.chunks(documents: documents)
            let cached = SemanticIndexStore.load(modelID: ModelCatalog.qwen3Embedding.id, workspaceID: workspaceID)
            let old = Dictionary(cached.entries.map { ($0.fingerprint, $0.vector) }, uniquingKeysWith: { a, _ in a })
            var index = SemanticIndex()
            let embedder = await SemanticRetrievalService.shared.embedder()
            for (position, chunk) in chunks.enumerated() {
                try Task.checkCancellation()
                if await MainActor.run(body: { AppState.shared.isRunning || AppState.shared.isChangingSession }) { throw CancellationError() }
                let vector: [Float]
                if let found = old[chunk.fingerprint], found.count == 1024 { vector = found }
                else { vector = try await embedder.embedPassage(chunk.text) }
                index.setVector(vector, fingerprint: chunk.fingerprint, for: position)
                if position % 10 == 0 || position + 1 == chunks.count {
                    try SemanticIndexStore.save(index, modelID: ModelCatalog.qwen3Embedding.id, workspaceID: workspaceID)
                    progress(position + 1, chunks.count)
                }
            }
            try SemanticIndexStore.save(index, modelID: ModelCatalog.qwen3Embedding.id, workspaceID: workspaceID)
            return index
        }
        active[workspaceID] = (signature, task)
        defer { if active[workspaceID]?.0 == signature { active[workspaceID] = nil } }
        return try await withTaskCancellationHandler { try await task.value } onCancel: { task.cancel() }
    }
    func cancelAll() { for (_, task) in active.values { task.cancel() } }
}
