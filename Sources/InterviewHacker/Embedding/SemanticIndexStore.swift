import Foundation
import InterviewHackerCore

enum SemanticIndexStore {
    private struct Envelope: Codable {
        var version = 2
        var chunkVersion = ContextChunker.version
        var workspaceID: UUID
        var modelRevision: String
        var index: SemanticIndex
    }
    static func url(for modelID: String, workspaceID: UUID) -> URL {
        AppPaths.index.appendingPathComponent(workspaceID.uuidString, isDirectory: true)
            .appendingPathComponent("semantic-\(modelID).plist")
    }
    static func load(modelID: String, workspaceID: UUID) -> SemanticIndex {
        guard let data = try? Data(contentsOf: url(for: modelID, workspaceID: workspaceID)),
              let value = try? PropertyListDecoder().decode(Envelope.self, from: data), value.version == 2,
              value.workspaceID == workspaceID, value.chunkVersion == ContextChunker.version, value.modelRevision == ModelCatalog.qwen3Embedding.revision else { return SemanticIndex() }
        return value.index
    }
    static func save(_ index: SemanticIndex, modelID: String, workspaceID: UUID) throws {
        let file = url(for: modelID, workspaceID: workspaceID)
        try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
        let encoder = PropertyListEncoder(); encoder.outputFormat = .binary
        try encoder.encode(Envelope(workspaceID: workspaceID, modelRevision: ModelCatalog.qwen3Embedding.revision, index: index))
            .write(to: file, options: .atomic)
    }
    static func removeWorkspace(_ workspaceID: UUID) throws {
        let directory = AppPaths.index.appendingPathComponent(workspaceID.uuidString, isDirectory: true)
        if FileManager.default.fileExists(atPath: directory.path) {
            try FileManager.default.removeItem(at: directory)
        }
    }
    static func remove(modelID: String) throws {
        let root = AppPaths.index
        guard FileManager.default.fileExists(atPath: root.path) else { return }
        guard let files = FileManager.default.enumerator(at: root, includingPropertiesForKeys: nil) else { return }
        for case let file as URL in files where file.lastPathComponent == "semantic-\(modelID).plist" {
            try FileManager.default.removeItem(at: file)
        }
    }
}
