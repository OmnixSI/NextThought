import Foundation
import InterviewHackerCore

actor MLXTextEmbedder {
    func embedQuery(_ text: String, timeout: TimeInterval = 0.5) async throws -> [Float] { try await embed(text, query: true, timeout: timeout) }
    func embedPassage(_ text: String) async throws -> [Float] { try await embed(text, query: false, timeout: 120) }
    private func embed(_ text: String, query: Bool, timeout: TimeInterval) async throws -> [Float] {
        let response = try await DocumentWorker.embedding.request("embed", [
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
    func embedder() -> MLXTextEmbedder { instance }
    func prepare() async {
        _ = try? await DocumentWorker.embedding.request("prepare_embedding", ["model_directory": ModelCatalog.qwen3Embedding.directory.path])
    }
    func queryVector(_ question: String) async -> [Float]? { try? await instance.embedQuery(question) }
    func unload() async { await DocumentWorker.embedding.unload() }
}
