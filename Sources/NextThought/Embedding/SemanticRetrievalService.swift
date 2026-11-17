import Foundation
import NextThoughtCore

actor MLXTextEmbedder {
    private let worker: DocumentWorker
    init(worker: DocumentWorker = .embedding) { self.worker = worker }
    func embedQuery(_ text: String, timeout: TimeInterval = 1.0) async throws -> [Float] { try await embed(text, query: true, timeout: timeout) }
    func embedPassage(_ text: String) async throws -> [Float] { try await embed(text, query: false, timeout: 120) }
    private func embed(_ text: String, query: Bool, timeout: TimeInterval) async throws -> [Float] {
        let response = try await worker.request("embed", [
            "model_directory": ModelCatalog.qwen3Embedding.directory.path,
            "text": text, "query": query
        ], timeout: timeout)
        guard let values = response["vector"] as? [NSNumber], values.count == 1024 else {
            throw AppFailure.capture("向量模型输出维度不是 1024。")
        }
        let vector = values.map(\.floatValue)
        let norm = vector.reduce(Float(0)) { $0 + $1 * $1 }.squareRoot()
        guard vector.allSatisfy({ $0.isFinite }), abs(norm - 1) < 0.02 else {
            throw AppFailure.capture("向量模型返回了无效或未归一化的向量。")
        }
        return vector
    }
}

actor SemanticRetrievalService {
    static let shared = SemanticRetrievalService()
    private let instance = MLXTextEmbedder()
    private let queryInstance = MLXTextEmbedder(worker: .queryEmbedding)
    private(set) var lastError: String?
    /// The query model is loaded in its worker.
    private var ready = false
    private var preparing: Task<Void, Never>?
    /// Vectors of the last few questions: a follow-up or a retry asks for the same one again.
    private var recent: [(question: String, vector: [Float])] = []

    func embedder() -> MLXTextEmbedder { instance }

    /// Loads the query model (a few seconds, once). Safe to call again while it loads.
    func prepare() async {
        if ready { return }
        if let preparing { await preparing.value; return }
        let task = Task {
            do {
                _ = try await DocumentWorker.queryEmbedding.request("prepare_embedding", ["model_directory": ModelCatalog.qwen3Embedding.directory.path])
                self.loaded(nil)
            } catch { self.loaded(error.localizedDescription) }
        }
        preparing = task
        await task.value
    }
    private func loaded(_ error: String?) { ready = error == nil; lastError = error; preparing = nil }

    /// The vector of a question, or nil when this answer should go by keywords.
    ///
    /// A question never waits for the model to load and never stops the worker: a query
    /// that gave up used to take the loaded model down with it, so the next one found it
    /// loading again and gave up too, for the rest of the meeting.
    func queryVector(_ question: String) async -> [Float]? {
        if let hit = recent.first(where: { $0.question == question }) { lastError = nil; return hit.vector }
        guard ready else {
            if preparing == nil { Task { await self.prepare() } }
            lastError = String(localized: "语义模型还在加载")
            return nil
        }
        do {
            let result = try await queryInstance.embedQuery(question, timeout: 2)
            lastError = nil
            recent.append((question, result))
            if recent.count > 8 { recent.removeFirst() }
            return result
        } catch { lastError = error.localizedDescription; return nil }
    }
    func unload() async {
        ready = false; preparing?.cancel(); preparing = nil; recent = []
        await DocumentWorker.embedding.unload(); await DocumentWorker.queryEmbedding.unload()
    }
}
