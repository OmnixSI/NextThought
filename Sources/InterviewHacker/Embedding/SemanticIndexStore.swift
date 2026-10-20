import Foundation
import InterviewHackerCore

/// Keeps the vector index of the materials on disk.
///
/// The vectors are written next to the other app data, in a binary property
/// list: 1024 floats per chunk turn into megabytes of JSON, which would make
/// the file slow to read and impossible to inspect.
enum SemanticIndexStore {
    /// The file the vectors of one model are stored in.
    static func url(for modelID: String) -> URL {
        AppPaths.index.appendingPathComponent("semantic-\(modelID).plist")
    }

    /// Reads the vectors of a model, if there are any.
    static func load(modelID: String) -> SemanticIndex {
        let file = url(for: modelID)
        guard let data = try? Data(contentsOf: file) else { return SemanticIndex() }
        let decoder = PropertyListDecoder()
        return (try? decoder.decode(SemanticIndex.self, from: data)) ?? SemanticIndex()
    }

    /// Writes the vectors of a model.
    static func save(_ index: SemanticIndex, modelID: String) throws {
        try FileManager.default.createDirectory(at: AppPaths.index, withIntermediateDirectories: true)
        let encoder = PropertyListEncoder()
        encoder.outputFormat = .binary
        let data = try encoder.encode(index)
        try data.write(to: url(for: modelID), options: .atomic)
    }

    /// Removes the vectors of a model, e.g. when the model is deleted.
    static func remove(modelID: String) {
        try? FileManager.default.removeItem(at: url(for: modelID))
    }
}
