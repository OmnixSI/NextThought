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
    public var id: String { fingerprint }
    public let locator: DocumentLocator?
    public let blockID: String?

    public init(source: String, page: Int, text: String, order: Int, fingerprint: String, locator: DocumentLocator? = nil, blockID: String? = nil) {
        self.source = source
        self.page = page
        self.text = text
        self.order = order
        self.fingerprint = fingerprint
        self.locator = locator; self.blockID = blockID
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
    public static let version = "2-heading-table"
    public static let chunkCharacters = 900
    public static let overlapCharacters = 120
    public static func chunks(documents: [ContextDocument]) -> [ContextChunk] {
        var result: [ContextChunk] = []
        for document in documents where document.enabled {
            let blocks = document.content?.blocks ?? document.pages.enumerated().flatMap { offset, text in
                DocumentStructure.parse(text, locator: .init(section: offset + 1))
            }
            for block in blocks where block.kind != "heading" {
                for (part, text) in DocumentStructure.pieces(block, target: chunkCharacters).enumerated() {
                    let path = ([document.name] + block.locator.headingPath).joined(separator: " > ")
                    let body = path + "\n" + text
                    let fingerprint = ContextFingerprint.hash(version + document.id.uuidString + "\n" + block.id + "\n" + String(part) + "\n" + body)
                    result.append(ContextChunk(source: document.labeledName,
                        page: block.locator.page ?? block.locator.slide ?? block.locator.section ?? 0,
                        text: body, order: result.count, fingerprint: fingerprint, locator: block.locator, blockID: block.id))
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
    public func vector(for order: Int, fingerprint: String, locator: DocumentLocator? = nil) -> [Float]? {
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
        guard !query.isEmpty, query.allSatisfy({ $0.isFinite }) else { return [] }
        var result: [(order: Int, score: Double)] = []
        for (order, entry) in entries.enumerated() where !entry.vector.isEmpty {
            guard order < fingerprints.count, fingerprints[order] == entry.fingerprint, entry.vector.count == query.count, entry.vector.allSatisfy({ $0.isFinite }) else { continue }
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
public enum RetrievalPolicy {
    /// Validation feasible-interval midpoint (v2); regression evaluated
    /// in Tools/Validation/Retrieval. A low-similarity list may be empty.
    public static let minimumSemanticScore = 0.43
}

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
        question: String, queryVector: [Float]? = nil, maximumCharacters: Int = 8_000,
        minimumSemanticScore: Double = RetrievalPolicy.minimumSemanticScore
    ) -> [ContextExcerpt] {
        guard maximumCharacters > 0 else { return [] }
        let lexicalOrders = lexical.rankedOrders(question: question)
        let fingerprints = chunks.map(\.fingerprint)
        let semanticRanking = queryVector.map { semantic.rankedOrders(query: $0, fingerprints: fingerprints) } ?? []
        let scores = Dictionary(uniqueKeysWithValues: semanticRanking.map { ($0.order, $0.score) })
        let semanticOrders = semanticRanking.filter { $0.score >= minimumSemanticScore }.map(\.order)
        let eligibleLexical = lexicalOrders.filter { scores[$0] == nil || scores[$0]! >= minimumSemanticScore }
        let fused = semanticOrders.isEmpty
            ? eligibleLexical.map { (order: $0, score: 0.0) }
            : HybridRanker.fuse(lexical: eligibleLexical, semantic: semanticOrders)
        // Explicit physical-page/file requests are resolved before similarity.
        let lower = question.lowercased()
        let pageRegex = try? NSRegularExpression(pattern: #"(?:第\s*(\d+)\s*[页張张]|p\.?\s*(\d+)|(?:slide|幻灯片)\s*(\d+))"#, options: .caseInsensitive)
        let ns = lower as NSString
        let match = pageRegex?.firstMatch(in: lower, range: NSRange(location: 0, length: ns.length))
        let page = match.flatMap { m in (1..<m.numberOfRanges).compactMap { i -> Int? in let range = m.range(at: i); return range.location == NSNotFound ? nil : Int(ns.substring(with: range)) }.first }
        func normalize(_ s: String) -> String { String(s.lowercased().filter { $0.isLetter || $0.isNumber }) }
        let questionKey = normalize(lower)
        let named = Set(chunks.filter { chunk in
            let stem = ((chunk.source as NSString).deletingPathExtension.split(separator: "_").first.map(String.init) ?? chunk.source)
            let key = normalize(stem)
            return key.count >= 4 && questionKey.contains(key)
        }.map(\.source))
        let explicit = chunks.filter { chunk in
            let physical = chunk.locator?.page ?? chunk.locator?.slide
            return (page != nil && physical == page && (named.isEmpty || named.contains(chunk.source))) || (page == nil && named.contains(chunk.source))
        }.map { (order: $0.order, score: 1.0) }
        let explicitSet = Set(explicit.map(\.order))
        let ranking = explicit + fused.filter { !explicitSet.contains($0.order) }
        var remaining = min(maximumCharacters, 16_000)
        var result: [ContextExcerpt] = []
        for entry in ranking.prefix(8) where remaining > 0 {
            guard entry.order < chunks.count else { continue }
            let chunk = chunks[entry.order]
            guard chunk.text.count <= remaining else { continue }
            let text = chunk.text
            guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { continue }
            result.append(.init(source: chunk.source, page: chunk.page, text: text, locator: chunk.locator, chunkID: chunk.id, blockID: chunk.blockID))
            remaining -= text.count
        }
        return result
    }
}
