import Foundation

/// A byte-level BPE tokenizer, the kind the Qwen3 / GPT-2 family uses.
///
/// The vocabulary and the merge table are read from the `tokenizer.json` of the
/// model, so the app does not carry a vocabulary of its own and stays in sync
/// with the model it embeds with. The text is first split into words with the
/// model's own pattern, then every word is turned into bytes, the bytes into
/// printable code points (the byte-level trick that keeps every byte
/// representable) and finally the pairs of code points are merged until no
/// merge applies.
public struct ByteLevelBPETokenizer: Sendable {
    /// What the tokenizer needs to know about the model it belongs to.
    public struct Configuration: Sendable, Equatable {
        /// Id of the end-of-text token, appended to every encoded text.
        public var endOfTextToken: Int
        /// Whether the end-of-text token is appended.
        public var appendEndOfText: Bool
        /// The pattern that splits a text into words.
        public var splitPattern: String
        /// Whether the text is normalized to NFC before it is split.
        public var normalizeNFC: Bool

        public init(
            endOfTextToken: Int = 151_643, appendEndOfText: Bool = true,
            splitPattern: String = Configuration.defaultSplitPattern, normalizeNFC: Bool = true
        ) {
            self.endOfTextToken = endOfTextToken
            self.appendEndOfText = appendEndOfText
            self.splitPattern = splitPattern
            self.normalizeNFC = normalizeNFC
        }

        /// The pattern of the Qwen3 / GPT-4 family.
        public static let defaultSplitPattern = #"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+"#
    }

    public enum Failure: LocalizedError {
        case unreadableTokenizer
        case notByteLevelBPE

        public var errorDescription: String? {
            switch self {
            case .unreadableTokenizer: "无法读取分词器文件。"
            case .notByteLevelBPE: "分词器不是字节级 BPE 格式。"
            }
        }
    }

    private let tokenToId: [String: Int]
    private let idToToken: [Int: String]
    private let mergeRanks: [Pair: Int]
    private let configuration: Configuration
    /// Byte -> printable code point, the reversible table of GPT-2.
    private let byteToScalar: [UInt32]
    private let scalarToByte: [UInt32: UInt8]
    private let splitRegex: NSRegularExpression?

    /// A pair of code points that can be merged.
    private struct Pair: Hashable {
        let first: String
        let second: String
    }

    public init(jsonURL: URL, configuration: Configuration = Configuration()) throws {
        let data = try Data(contentsOf: jsonURL)
        guard let root = try JSONSerialization.jsonObject(with: data) as? [String: Any],
            let model = root["model"] as? [String: Any],
            let vocab = model["vocab"] as? [String: Int]
        else { throw Failure.notByteLevelBPE }
        guard (model["type"] as? String) == "BPE" else { throw Failure.notByteLevelBPE }

        var tokenToId = vocab
        var idToToken: [Int: String] = [:]
        for (token, id) in vocab { idToToken[id] = token }

        // Added tokens (the end-of-text marker and the chat markers) are not in
        // the vocabulary of the model but still have to be encodable.
        if let added = root["added_tokens"] as? [[String: Any]] {
            for entry in added {
                guard let content = entry["content"] as? String, let id = entry["id"] as? Int else { continue }
                tokenToId[content] = id
                idToToken[id] = content
            }
        }

        var mergeRanks: [Pair: Int] = [:]
        var rank = 0
        if let merges = model["merges"] as? [[String]] {
            for merge in merges where merge.count == 2 {
                mergeRanks[Pair(first: merge[0], second: merge[1])] = rank
                rank += 1
            }
        } else if let merges = model["merges"] as? [String] {
            for merge in merges {
                let parts = merge.split(separator: " ", maxSplits: 1).map(String.init)
                guard parts.count == 2 else { continue }
                mergeRanks[Pair(first: parts[0], second: parts[1])] = rank
                rank += 1
            }
        }

        let (byteToScalar, scalarToByte) = Self.byteTables()
        self.tokenToId = tokenToId
        self.idToToken = idToToken
        self.mergeRanks = mergeRanks
        self.configuration = configuration
        self.byteToScalar = byteToScalar
        self.scalarToByte = scalarToByte
        self.splitRegex = try? NSRegularExpression(pattern: configuration.splitPattern)
    }

    /// Encodes a text into token ids.
    ///
    /// The end-of-text token is appended, the way the embedding model was
    /// trained: it is the token the model pools its vector from.
    public func encode(_ text: String) -> [Int] {
        let source = configuration.normalizeNFC ? text.precomposedStringWithCanonicalMapping : text
        var ids: [Int] = []
        for piece in split(source) {
            ids.append(contentsOf: encodePiece(piece))
        }
        if configuration.appendEndOfText { ids.append(configuration.endOfTextToken) }
        return ids
    }

    /// Decodes token ids back into text.
    public func decode(_ ids: [Int]) -> String {
        var bytes: [UInt8] = []
        for id in ids {
            guard let token = idToToken[id] else { continue }
            for scalar in token.unicodeScalars {
                if let byte = scalarToByte[scalar.value] { bytes.append(byte) }
            }
        }
        return String(decoding: bytes, as: UTF8.self)
    }

    /// The token id of a text, when it is a single known token.
    public func tokenId(_ token: String) -> Int? { tokenToId[token] }

    // MARK: - Internals

    private func split(_ text: String) -> [String] {
        guard let splitRegex else { return text.isEmpty ? [] : [text] }
        let ns = text as NSString
        var result: [String] = []
        splitRegex.enumerateMatches(in: text, range: NSRange(location: 0, length: ns.length)) { match, _, _ in
            guard let match, match.numberOfRanges > 0, let range = Range(match.range(at: 0), in: text) else { return }
            result.append(String(text[range]))
        }
        return result
    }

    /// One word: bytes, then code points, then merges.
    private func encodePiece(_ piece: String) -> [Int] {
        let bytes = Array(piece.utf8)
        var symbols: [String] = []
        symbols.reserveCapacity(bytes.count)
        for byte in bytes {
            let scalar = byteToScalar[Int(byte)]
            symbols.append(String(Unicode.Scalar(scalar)!))
        }
        let merged = bpe(symbols)
        return merged.compactMap { tokenToId[$0] }
    }

    /// Merges the pairs of code points that the model knows, lowest rank first.
    private func bpe(_ word: [String]) -> [String] {
        guard word.count > 1 else { return word }
        var symbols = word
        while symbols.count > 1 {
            var bestRank = Int.max
            var bestIndex = -1
            for index in 0..<(symbols.count - 1) {
                let rank = mergeRanks[Pair(first: symbols[index], second: symbols[index + 1])] ?? Int.max
                if rank < bestRank {
                    bestRank = rank
                    bestIndex = index
                }
            }
            guard bestIndex >= 0 else { break }
            let first = symbols[bestIndex]
            let second = symbols[bestIndex + 1]
            var merged: [String] = []
            var index = 0
            while index < symbols.count {
                if index < symbols.count - 1, symbols[index] == first, symbols[index + 1] == second {
                    merged.append(first + second)
                    index += 2
                } else {
                    merged.append(symbols[index])
                    index += 1
                }
            }
            symbols = merged
        }
        return symbols
    }

    /// GPT-2's reversible byte table: every byte gets a printable code point.
    private static func byteTables() -> ([UInt32], [UInt32: UInt8]) {
        var bytes: [Int] = Array(33...126) + Array(161...172) + Array(174...255)
        var codes = bytes
        var extra = 0
        for byte in 0..<256 where !bytes.contains(byte) {
            bytes.append(byte)
            codes.append(256 + extra)
            extra += 1
        }
        var byteToScalar = [UInt32](repeating: 0, count: 256)
        var scalarToByte: [UInt32: UInt8] = [:]
        for (byte, code) in zip(bytes, codes) {
            byteToScalar[byte] = UInt32(code)
            scalarToByte[UInt32(code)] = UInt8(byte)
        }
        return (byteToScalar, scalarToByte)
    }
}
