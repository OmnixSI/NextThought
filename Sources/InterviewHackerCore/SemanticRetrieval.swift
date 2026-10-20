import Foundation

/// One piece of a material: the unit both the lexical and the semantic index
/// work on.
///
/// The chunk boundaries are shared on purpose. A hybrid search can only fuse
/// the two rankings when the lexical score and the vector score belong to the
/// same piece of text, so both indexes are built from the same chunker.
public struct ContextChunk: Sendable, Equatable {
    public let source: String
    public let page: Int
    public let text: String
    /// Position in the chunk list; the stable key of a chunk.
    public let order: Int
    /// Hash of the text: it tells whether the vector of this chunk is still
    /// the vector of this text.
    public let fingerprint: String

    public init(source: String, page: Int, text: String, order: Int, fingerprint: String) {
        self.source = source
        self.page = page
        self.text = text
        self.order = order
        self.fingerprint = fingerprint
    }
}

/// A hash of a text, used to tell whether a stored vector still belongs to
/// the text it was made for.
public enum ContextFingerprint {
    /// FNV-1a; it only has to notice a change, so it does not have to be
    /// cryptographic.
    public static func hash(_ text: String) -> String {
        var value: UInt64 = 0xcbf2_9ce4_8422_2325
        for byte in text.utf8 {
            value ^= UInt64(byte)
            value = value &* 0x0000_0100_0000_01b3
        }
        return String(value, radix: 16)
    }
}

/// Splits the materials of a scenario into the chunks that get indexed.
///
/// The same bounds as the lexical index: 900 characters per chunk with 120
/// characters of overlap, so a sentence that straddles two chunks is still
/// found by both.
public enum ContextChunker {
    public static let chunkCharacters = 900
    public static let overlapCharacters = 120
    /// Materials per scenario; kept in step with `SessionContextIndex.maximumDocuments`.
    public static let maximumDocuments = 40
    /// Sections read per material.
    public static let maximumSectionsPerDocument = 500
    /// Upper bound on the number of chunks, so a huge workspace cannot blow up the index.
    public static let maximumChunks = 2_000

    public static func chunks(documents: [ContextDocument]) -> [ContextChunk] {
        var result: [ContextChunk] = []
        for document in documents.prefix(maximumDocuments) where document.enabled {
            for (pageNumber, page) in document.pages.prefix(maximumSectionsPerDocument).enumerated() {
                let chars = Array(page.prefix(200_000))
                var start = 0
                while start < chars.count && result.count < maximumChunks {
                    let end = min(start + chunkCharacters, chars.count)
                    let text = String(chars[start..<end])
                    result.append(
                        ContextChunk(
                            source: document.labeledName, page: pageNumber + 1,
                            text: text, order: result.count, fingerprint: ContextFingerprint.hash(text)))
                    if end == chars.count { break }
                    start = end - overlapCharacters
                }
            }
        }
        return result
    }
}

/// The embeddings of the material chunks.
///
/// Vectors are stored in the order of `ContextChunker.chunks`, so a vector can
/// be addressed by the order of its chunk. Every vector remembers the hash of
/// the text it was made for: when a material changes, the vector no longer
/// belongs to the text and counts as missing. They are L2-normalized when they
/// are written, which turns the cosine similarity into a plain dot product.
public struct SemanticIndex: Sendable, Codable, Equatable {
    /// One chunk and its vector.
    public struct Entry: Sendable, Codable, Equatable {
        /// Hash of the text the vector was made for.
        public var fingerprint: String
        public var vector: [Float]

        public init(fingerprint: String, vector: [Float]) {
            self.fingerprint = fingerprint
            self.vector = vector
        }
    }

    /// One entry per chunk, in chunk order.
    public private(set) var entries: [Entry]

    public init(entries: [Entry] = []) {
        self.entries = entries
    }

    public var isEmpty: Bool { entries.allSatisfy { $0.vector.isEmpty } }
    public var embeddedCount: Int { entries.filter { !$0.vector.isEmpty }.count }
    public var dimension: Int { entries.first?.vector.count ?? 0 }

    /// Writes one chunk vector, growing the storage when needed.
    public mutating func setVector(_ vector: [Float], fingerprint: String, for order: Int) {
        guard order >= 0 else { return }
        if entries.count <= order {
            entries.append(
                contentsOf: Array(repeating: Entry(fingerprint: "", vector: []), count: order - entries.count + 1))
        }
        entries[order] = Entry(fingerprint: fingerprint, vector: vector)
    }

    /// The vector of a chunk, but only when it was made for the current text.
    public func vector(for order: Int, fingerprint: String) -> [Float]? {
        guard order >= 0, order < entries.count else { return nil }
        let entry = entries[order]
        guard !entry.vector.isEmpty, entry.fingerprint == fingerprint else { return nil }
        return entry.vector
    }

    /// Removes every vector, e.g. when the material was replaced.
    public mutating func removeAll() { entries = [] }

    /// Cosine similarity between the query and every embedded chunk, best first.
    ///
    /// Only chunks that carry a vector for their current text take part; a
    /// chunk that was changed since it was embedded does not show up in the
    /// semantic ranking.
    public func rankedOrders(query: [Float], fingerprints: [String]) -> [(order: Int, score: Double)] {
        guard !query.isEmpty else { return [] }
        var result: [(order: Int, score: Double)] = []
        for (order, entry) in entries.enumerated() where !entry.vector.isEmpty {
            guard order < fingerprints.count, fingerprints[order] == entry.fingerprint else { continue }
            let score = Self.dot(query, entry.vector)
            result.append((order, score))
        }
        return result.sorted { $0.score == $1.score ? $0.order < $1.order : $0.score > $1.score }
    }

    static func dot(_ lhs: [Float], _ rhs: [Float]) -> Double {
        let count = min(lhs.count, rhs.count)
        guard count > 0 else { return 0 }
        var sum = 0.0
        for index in 0..<count { sum += Double(lhs[index]) * Double(rhs[index]) }
        return sum
    }
}

/// Fuses the lexical and the semantic ranking.
///
/// Reciprocal Rank Fusion: every list contributes `1 / (k + rank)` to a chunk,
/// so a chunk that both rankings like wins, and a chunk that only one of them
/// likes can still come through. No score normalization is needed, which is
/// what makes it work with a lexical score and a cosine similarity at once.
public enum HybridRanker {
    /// The constant of the formula; the usual choice from the original paper.
    public static let constant = 60.0

    public static func fuse(lexical: [Int], semantic: [Int], constant: Double = HybridRanker.constant)
        -> [(order: Int, score: Double)]
    {
        var scores: [Int: Double] = [:]
        for (rank, order) in lexical.enumerated() {
            scores[order, default: 0] += 1 / (constant + Double(rank + 1))
        }
        for (rank, order) in semantic.enumerated() {
            scores[order, default: 0] += 1 / (constant + Double(rank + 1))
        }
        return scores.map { (order: $0.key, score: $0.value) }
            .sorted { $0.score == $1.score ? $0.order < $1.order : $0.score > $1.score }
    }
}

/// The retrieval the app uses: the lexical index, plus the semantic index when
/// an embedding model is installed and enabled.
public struct HybridContextIndex: Sendable {
    public let chunks: [ContextChunk]
    public let lexical: SessionContextIndex
    public let semantic: SemanticIndex

    public init(documents: [ContextDocument] = [], semantic: SemanticIndex = SemanticIndex()) {
        let chunks = ContextChunker.chunks(documents: documents)
        self.chunks = chunks
        self.lexical = SessionContextIndex(chunks: chunks)
        self.semantic = semantic
    }

    /// True when the semantic index can contribute to the ranking.
    public var hasVectors: Bool { !semantic.isEmpty }

    /// Retrieves the passages that answer the question.
    ///
    /// - Parameter queryVector: embedding of the question; when it is missing the
    ///   search falls back to the lexical ranking alone.
    public func retrieve(
        question: String, queryVector: [Float]? = nil, maximumCharacters: Int = 8_000
    ) -> [ContextExcerpt] {
        guard maximumCharacters > 0 else { return [] }
        let lexical = lexical.rankedOrders(question: question)
        let fingerprints = chunks.map(\.fingerprint)
        let semanticOrders = queryVector.map { semantic.rankedOrders(query: $0, fingerprints: fingerprints).map(\.order) } ?? []
        let fused = semanticOrders.isEmpty
            ? lexical.map { (order: $0, score: 0.0) }
            : HybridRanker.fuse(lexical: lexical, semantic: semanticOrders)
        var remaining = min(maximumCharacters, 16_000)
        var result: [ContextExcerpt] = []
        for entry in fused.prefix(8) where remaining > 0 {
            guard entry.order < chunks.count else { continue }
            let chunk = chunks[entry.order]
            let text = String(chunk.text.prefix(remaining))
            guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { continue }
            result.append(.init(source: chunk.source, page: chunk.page, text: text))
            remaining -= text.count
        }
        return result
    }
}
