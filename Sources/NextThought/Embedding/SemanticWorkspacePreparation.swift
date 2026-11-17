import Foundation
import NextThoughtCore

actor SemanticWorkspacePreparation {
    static let shared = SemanticWorkspacePreparation()
    private var active: [UUID: (String, Task<SemanticIndex, Error>)] = [:]
    func build(workspaceID: UUID, documents: [ContextDocument], progress: @escaping @Sendable (Int, Int) -> Void) async throws -> SemanticIndex {
        // The text counts too: a deck is here with its words first and again once its
        // pictures are read, under the same id and source.
        let signature = ContextFingerprint.hash(documents.map { $0.id.uuidString + ($0.content?.sourceHash ?? "") + String($0.characterCount) + "/" + String($0.pages.count) + String($0.enabled) }.joined())
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
            // The whole index is rewritten at each checkpoint, and it grows with every vector:
            // saving every ten chunks made a large workspace spend most of its time writing
            // the file. A checkpoint every few seconds loses no more work if it is interrupted.
            var savedAt = Date()
            // A meeting cancels this task when it starts. Asking the main actor as well is a
            // safety net, and asking before every chunk made it the slowest step: the hop
            // from a utility task took up to a second in the running app, the vector 0.06 s.
            var checkedAt = Date.distantPast
            // Where the time of an index goes, for the next "it is slow".
            let began = Date()
            var embedded = 0, embedSeconds = 0.0, saveSeconds = 0.0, checkSeconds = 0.0
            for (position, chunk) in chunks.enumerated() {
                try Task.checkCancellation()
                if Date().timeIntervalSince(checkedAt) >= 2 {
                    let asked = Date()
                    if await MainActor.run(body: { AppState.shared.isRunning || AppState.shared.isChangingSession }) { throw CancellationError() }
                    checkedAt = Date(); checkSeconds += checkedAt.timeIntervalSince(asked)
                }
                let vector: [Float]
                if let found = old[chunk.fingerprint], found.count == 1024 { vector = found }
                else {
                    let asked = Date()
                    vector = try await embedder.embedPassage(chunk.text)
                    embedded += 1; embedSeconds += Date().timeIntervalSince(asked)
                }
                index.setVector(vector, fingerprint: chunk.fingerprint, for: position)
                if position % 10 == 0 || position + 1 == chunks.count { progress(position + 1, chunks.count) }
                if Date().timeIntervalSince(savedAt) >= 8 {
                    let asked = Date()
                    try SemanticIndexStore.save(index, modelID: ModelCatalog.qwen3Embedding.id, workspaceID: workspaceID)
                    savedAt = Date(); saveSeconds += savedAt.timeIntervalSince(asked)
                }
            }
            try SemanticIndexStore.save(index, modelID: ModelCatalog.qwen3Embedding.id, workspaceID: workspaceID)
            Diagnostics.record("semantic.built", ["chunks": chunks.count, "embedded": embedded, "seconds": Date().timeIntervalSince(began),
                                                  "embed_seconds": embedSeconds, "save_seconds": saveSeconds, "check_seconds": checkSeconds])
            return index
        }
        active[workspaceID] = (signature, task)
        defer { if active[workspaceID]?.0 == signature { active[workspaceID] = nil } }
        return try await withTaskCancellationHandler { try await task.value } onCancel: { task.cancel() }
    }
    func cancelAll() { for (_, task) in active.values { task.cancel() } }
}
