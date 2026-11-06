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
    func embedder() -> MLXTextEmbedder { instance }
    func prepare() async {
        do {
            _ = try await DocumentWorker.queryEmbedding.request("prepare_embedding", ["model_directory": ModelCatalog.qwen3Embedding.directory.path])
            lastError = nil
        } catch { lastError = error.localizedDescription }
    }
    func queryVector(_ question: String) async -> [Float]? {
        do { let result = try await queryInstance.embedQuery(question); lastError = nil; return result }
        catch { lastError = error.localizedDescription; return nil }
    }
    func unload() async {
        await DocumentWorker.embedding.unload(); await DocumentWorker.queryEmbedding.unload()
    }
}
