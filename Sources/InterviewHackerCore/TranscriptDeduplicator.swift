import Foundation

public enum TranscriptDeduplicator {
    private struct Token {
        var value: String
        var range: Range<String.Index>
    }

    public static func removeRepeatedPrefix(previous: String, incoming: String) -> String {
        let text = incoming.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return "" }
        let left = tokens(in: previous)
        let right = tokens(in: text)
        if left.count == right.count, !left.isEmpty,
           zip(left, right).allSatisfy({ $0.0.value.caseInsensitiveCompare($0.1.value) == .orderedSame }) {
            return ""
        }
        if containsCJK(previous) && containsCJK(text) {
            return removeCJKOverlap(previous: previous, incoming: text)
        }

        let limit = min(6, min(left.count, right.count))
        guard limit >= 2 else { return text }
        for count in stride(from: limit, through: 2, by: -1) {
            var matches = true
            for idx in 0..<count {
                let first = left[left.count - count + idx].value
                let second = right[idx].value
                if first.caseInsensitiveCompare(second) != .orderedSame {
                    matches = false
                    break
                }
            }
            if matches {
                return trimLeadingSeparators(String(text[right[count - 1].range.upperBound...]))
            }
        }
        return text
    }

    private static func removeCJKOverlap(previous: String, incoming: String) -> String {
        let left = Array(previous)
        let right = Array(incoming)
        let limit = min(60, min(left.count, right.count))
        if limit >= 4 {
            for count in stride(from: limit, through: 4, by: -1) {
                if Array(left.suffix(count)) == Array(right.prefix(count)) {
                    return trimLeadingSeparators(String(right.dropFirst(count)))
                }
            }
        }
        return incoming
    }

    private static func tokens(in text: String) -> [Token] {
        var result: [Token] = []
        var start: String.Index?
        var idx = text.startIndex
        while idx < text.endIndex {
            let char = text[idx]
            if char.isLetter || char.isNumber {
                if start == nil { start = idx }
            } else {
                if let tokenStart = start {
                    result.append(Token(value: String(text[tokenStart..<idx]), range: tokenStart..<idx))
                }
                start = nil
            }
            idx = text.index(after: idx)
        }
        if let start {
            result.append(Token(value: String(text[start..<text.endIndex]), range: start..<text.endIndex))
        }
        return result
    }

    private static func containsCJK(_ text: String) -> Bool {
        text.unicodeScalars.contains { (0x4E00...0x9FFF).contains($0.value) }
    }

    private static func trimLeadingSeparators(_ text: String) -> String {
        var idx = text.startIndex
        while idx < text.endIndex {
            let char = text[idx]
            if char.isWhitespace || char.isPunctuation {
                idx = text.index(after: idx)
            } else {
                break
            }
        }
        return String(text[idx...]).trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
