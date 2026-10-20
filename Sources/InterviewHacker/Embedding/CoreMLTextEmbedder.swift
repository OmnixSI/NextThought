import CoreML
import Foundation
import InterviewHackerCore

/// Embeds texts with the Qwen3-Embedding Core ML model.
///
/// The model is a Core ML package with a fixed shape, so every text is padded
/// to the same length and handed over as `input_ids` plus an attention mask.
/// The package already does the pooling and the normalization, so the vector
/// that comes back is the 1024-dimensional unit vector of the text.
///
/// The actor keeps the Core ML calls on one thread: `prediction` is
/// synchronous and not thread-safe.
actor CoreMLTextEmbedder {
    /// Length the Core ML package was converted for.
    static let sequenceLength = 512
    /// The end-of-text token of the model; it is also used as the padding.
    static let endOfTextToken = 151_643
    /// The instruction the model was trained with for retrieval queries.
    static let queryInstruction =
        "Instruct: Given a web search query, retrieve relevant passages that answer the query\nQuery: "

    private let model: MLModel
    private let tokenizer: ByteLevelBPETokenizer

    /// Loads the model from the app data directory.
    ///
    /// The Core ML package is compiled once and kept next to it, so the second
    /// start of the app does not pay the compilation again.
    static func load(modelDirectory: URL) async throws -> CoreMLTextEmbedder {
        let descriptor = ModelCatalog.qwen3Embedding
        guard ManagedModelStore.isInstalled(descriptor) else { throw EmbeddingError.modelMissing }
        let tokenizerURL = modelDirectory.appendingPathComponent("tokenizer/tokenizer.json")
        guard FileManager.default.fileExists(atPath: tokenizerURL.path) else {
            throw EmbeddingError.modelMissing
        }
        let tokenizer = try ByteLevelBPETokenizer(jsonURL: tokenizerURL)
        let package = modelDirectory.appendingPathComponent("packages/b1_s512.mlpackage")
        guard FileManager.default.fileExists(atPath: package.path) else {
            throw EmbeddingError.modelMissing
        }
        let compiledDirectory = modelDirectory.appendingPathComponent("compiled", isDirectory: true)
        try FileManager.default.createDirectory(at: compiledDirectory, withIntermediateDirectories: true)
        let target = compiledDirectory.appendingPathComponent("Qwen3Embedding.mlmodelc")
        if !FileManager.default.fileExists(atPath: target.path) {
            let temporary = try await MLModel.compileModel(at: package)
            try? FileManager.default.removeItem(at: target)
            try FileManager.default.moveItem(at: temporary, to: target)
        }
        let configuration = MLModelConfiguration()
        configuration.computeUnits = .all
        let model = try await MLModel.load(contentsOf: target, configuration: configuration)
        return CoreMLTextEmbedder(model: model, tokenizer: tokenizer)
    }

    private init(model: MLModel, tokenizer: ByteLevelBPETokenizer) {
        self.model = model
        self.tokenizer = tokenizer
    }

    /// Embeds a question, with the instruction the model expects.
    func embedQuery(_ question: String) throws -> [Float] {
        try embed(Self.queryInstruction + question)
    }

    /// Embeds a passage.
    ///
    /// A passage is longer than the fixed shape of the model, so it is split
    /// into pieces that fit; the vector of the passage is the mean of the
    /// vectors of its pieces, normalized again.
    func embedPassage(_ text: String) throws -> [Float] {
        let pieces = Self.split(text, tokenizer: tokenizer)
        guard !pieces.isEmpty else { return [] }
        var sum = [Float](repeating: 0, count: 1024)
        for piece in pieces {
            let vector = try embed(piece)
            for index in sum.indices where index < vector.count { sum[index] += vector[index] }
        }
        let mean = sum.map { $0 / Float(pieces.count) }
        return Self.normalize(mean)
    }

    /// Embeds several passages, one after the other.
    func embedPassages(_ texts: [String], progress: (@Sendable (Int, Int) -> Void)? = nil) throws
        -> [[Float]]
    {
        var result: [[Float]] = []
        result.reserveCapacity(texts.count)
        for (index, text) in texts.enumerated() {
            result.append(try embedPassage(text))
            progress?(index + 1, texts.count)
        }
        return result
    }

    /// One text: tokenize, pad, predict.
    private func embed(_ text: String) throws -> [Float] {
        let tokens = tokenizer.encode(text)
        let (ids, realCount) = Self.padded(tokens)
        let inputIDs = try MLMultiArray(shape: [1, Self.sequenceLength as NSNumber], dataType: .int32)
        let mask = try MLMultiArray(shape: [1, Self.sequenceLength as NSNumber], dataType: .int32)
        let idPointer = inputIDs.dataPointer.assumingMemoryBound(to: Int32.self)
        let maskPointer = mask.dataPointer.assumingMemoryBound(to: Int32.self)
        for index in 0..<Self.sequenceLength {
            idPointer[index] = Int32(ids[index])
            // The padding sits in front; the model pools from the last real token.
            maskPointer[index] = index >= Self.sequenceLength - realCount ? 1 : 0
        }
        let provider = try MLDictionaryFeatureProvider(dictionary: [
            "input_ids": MLFeatureValue(multiArray: inputIDs),
            "attention_mask": MLFeatureValue(multiArray: mask),
        ])
        let output = try model.prediction(from: provider)
        guard let embedding = output.featureValue(for: "embedding")?.multiArrayValue else {
            throw EmbeddingError.noOutput
        }
        var vector = [Float](repeating: 0, count: embedding.shape[1].intValue)
        let pointer = embedding.dataPointer.assumingMemoryBound(to: Float.self)
        for index in vector.indices { vector[index] = pointer[index] }
        return vector
    }

    /// Left-pads the tokens to the fixed length of the model.
    ///
    /// The attention mask marks the padding with zero, so the model ignores it
    /// and pools the vector from the last real token. The second value is the
    /// number of real tokens.
    private static func padded(_ tokens: [Int]) -> (ids: [Int], realCount: Int) {
        var body = tokens
        if body.isEmpty { body = [endOfTextToken] }
        if body.count > sequenceLength {
            // Keep the end-of-text token: it is the token the vector is pooled from.
            body = Array(body.prefix(sequenceLength - 1)) + [endOfTextToken]
        }
        let padding = sequenceLength - body.count
        return ([Int](repeating: endOfTextToken, count: max(0, padding)) + body, body.count)
    }

    /// Splits a text into pieces that fit into the model.
    static func split(_ text: String, tokenizer: ByteLevelBPETokenizer) -> [String] {
        let tokens = tokenizer.encode(text)
        guard tokens.count > sequenceLength else { return text.isEmpty ? [] : [text] }
        var pieces: [String] = []
        var index = 0
        while index < tokens.count {
            let end = min(index + sequenceLength - 1, tokens.count)
            let piece = tokenizer.decode(Array(tokens[index..<end]))
            if !piece.isEmpty { pieces.append(piece) }
            index = end
        }
        return pieces
    }

    /// Scales a vector to length 1; a zero vector is left alone.
    static func normalize(_ vector: [Float]) -> [Float] {
        var sum: Float = 0
        for value in vector { sum += value * value }
        let length = sum.squareRoot()
        guard length > 0 else { return vector }
        return vector.map { $0 / length }
    }

    enum EmbeddingError: LocalizedError {
        case modelMissing
        case noOutput

        var errorDescription: String? {
            switch self {
            case .modelMissing: "向量模型不可用：模型未安装或文件不完整。"
            case .noOutput: "向量模型没有返回结果。"
            }
        }
    }
}
