import Foundation
import InterviewHackerCore

/// Keeps the embedding model loaded and hands out vectors.
///
/// Loading a Core ML model takes a moment, so the model is kept around and
/// only loaded when it is actually needed. A failure is not an error the user
/// has to deal with: the search simply stays lexical.
actor SemanticRetrievalService {
    static let shared = SemanticRetrievalService()

    private var embedder: CoreMLTextEmbedder?
    private var loading: Task<CoreMLTextEmbedder, Error>?

    /// The loaded model, loading it if that has not happened yet.
    func embedder() async throws -> CoreMLTextEmbedder {
        if let embedder { return embedder }
        if let loading { return try await loading.value }
        let task = Task<CoreMLTextEmbedder, Error> {
            try await CoreMLTextEmbedder.load(modelDirectory: ModelCatalog.qwen3Embedding.directory)
        }
        loading = task
        do {
            let embedder = try await task.value
            self.embedder = embedder
            loading = nil
            return embedder
        } catch {
            loading = nil
            throw error
        }
    }

    /// Loads the model in the background, so the first question is not slow.
    func prepare() {
        guard embedder == nil, loading == nil else { return }
        Task { _ = try? await self.embedder() }
    }

    /// The vector of a question, or `nil` when the model is not available.
    func queryVector(_ question: String) async -> [Float]? {
        guard let embedder = try? await embedder() else { return nil }
        return try? await embedder.embedQuery(question)
    }
}
