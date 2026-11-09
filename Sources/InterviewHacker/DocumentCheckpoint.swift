import Foundation
import InterviewHackerCore

/// One page/Office part is the recovery unit. The cache is scoped to both the
/// immutable source and the actual pipeline/model configuration.
struct DocumentPageCheckpoint: Codable {
    var version = 1
    var key: String
    var blocks: [DocumentBlock]
    var warnings: [String]
    var rawText: String?
}
struct DocumentCheckpoint {
    var directory: URL?
    var key: String
    init(options: DocumentConversionOptions, sourceHash: String) {
        directory = options.checkpointDirectory
        key = ContextFingerprint.hash([sourceHash, "pipeline-4", options.provider.rawValue, options.localModelID,
            options.model, options.effort, String(options.fallback)].joined(separator: "|"))
    }
    func load(_ unit: String) -> DocumentPageCheckpoint? {
        guard let directory, let data = try? Data(contentsOf: directory.appendingPathComponent(unit + ".json")),
              let page = try? JSONDecoder().decode(DocumentPageCheckpoint.self, from: data), page.version == 1, page.key == key, page.warnings.isEmpty else { return nil }
        return page
    }
    func save(_ unit: String, blocks: [DocumentBlock], warnings: [String], rawText: String? = nil) throws {
        guard let directory else { return }
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try JSONEncoder().encode(DocumentPageCheckpoint(key: key, blocks: blocks, warnings: warnings, rawText: rawText))
            .write(to: directory.appendingPathComponent(unit + ".json"), options: .atomic)
    }
}
